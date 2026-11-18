using Coordin.Api.Features.Demands;
using Coordin.Api.Features.Planning;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Microsoft.EntityFrameworkCore;
using Motor = Coordin.Engine.Scenarios;

namespace Coordin.Api.Features.Scenarios;

/// <summary>
/// Cenários de sequenciamento (RF-13 a RF-15; DT-25, DT-26). As rotas de leitura (recomendar e simular)
/// nunca gravam. As de escrita (aprovar e voltar à prioridade) rodam no <see cref="PlanningTransaction"/>:
/// gravam a ordem e o modo e recalculam, ou não gravam nada. A regra de cálculo mora no motor; aqui só se
/// valida o corpo, se lê o planejamento e se traduz a resposta.
/// </summary>
public static class ScenarioEndpoints
{
    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scenarios").WithTags("Scenarios");

        group.MapPost("/recommendations", GetRecommendationsAsync)
            .WithName("GetScenarioRecommendations")
            .WithSummary("Recomenda até três ordens de execução melhores que a atual, sem gravar nada (RF-13).")
            .Produces<IReadOnlyList<ScenarioResponse>>(StatusCodes.Status200OK);

        group.MapPost("/simulate", SimulateScenarioAsync)
            .WithName("SimulateScenario")
            .WithSummary("Simula uma ordem de execução proposta pelo gestor, sem gravar nada (RF-14).")
            .Produces<ScenarioResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/apply", ApplyScenarioAsync)
            .WithName("ApplyScenario")
            .WithSummary("Aprova um cenário: grava a ordem de execução e o modo Custom e recalcula. A prioridade não muda (RF-15).")
            .Produces<ScenarioResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/reset-to-priority", ResetToPriorityAsync)
            .WithName("ResetToPriority")
            .WithSummary("Volta a ordem de execução para a ordem de prioridade e recalcula (RF-15).")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> GetRecommendationsAsync(
        CoordinDbContext db,
        PlanningSnapshotLoader loader,
        CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var scenarios = ScenarioSearch.Recommend(snapshot, ScenarioSearch.EvaluationBudget, cancellationToken);
        var titles = await TitlesAsync(db, cancellationToken);

        return Results.Ok(scenarios.Select(scenario => ScenarioMapper.ToResponse(scenario, titles)).ToList());
    }

    private static async Task<IResult> SimulateScenarioAsync(
        ScenarioOrderRequest request,
        CoordinDbContext db,
        PlanningSnapshotLoader loader,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateOrderAsync(request.ExecutionOrder, db, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var scenario = ScenarioEvaluator.Evaluate(snapshot, request.ExecutionOrder!);
        var titles = await TitlesAsync(db, cancellationToken);

        return Results.Ok(ScenarioMapper.ToResponse(scenario, titles));
    }

    /// <summary>
    /// Grava a ordem aprovada e o modo <c>Custom</c>, e recalcula; a prioridade registrada não muda. Antes de
    /// gravar, confere no cronograma do cenário se alguma dependência fica violada: se ficar, responde
    /// <c>409</c> e a <c>PlanningTransaction</c> desfaz tudo (não há nada a gravar).
    /// </summary>
    private static Task<IResult> ApplyScenarioAsync(
        ScenarioOrderRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        PlanningSnapshotLoader loader,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.ApprovedScenario, async () =>
        {
            var invalid = await ValidateOrderAsync(request.ExecutionOrder, db, cancellationToken);
            if (invalid is not null)
            {
                return invalid;
            }

            var order = request.ExecutionOrder!;
            var snapshot = await loader.LoadAsync(db, cancellationToken);
            var scenario = ScenarioEvaluator.Evaluate(snapshot, order); // a resposta é contra o cronograma anterior

            var proposed = ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, order));
            if (ScenarioErrors.Describe(DependencyCheck.Violations(snapshot, proposed)) is var (code, message))
            {
                return ApiProblems.Conflict(code, message);
            }

            var priorityOrder = await DemandRanking.OrderedIdsAsync(db, byPriority: true, cancellationToken);
            await DemandRanking.SetRanksAsync(db, priorityOrder, order.ToList(), cancellationToken);
            _ = await SetModeAsync(db, ExecutionOrderMode.Custom, cancellationToken);

            var titles = await TitlesAsync(db, cancellationToken);
            return Results.Ok(ScenarioMapper.ToResponse(scenario, titles));
        }, cancellationToken);

    /// <summary>Volta o modo para <c>FollowPriority</c> e recalcula; se já está nele, não grava nada (204).</summary>
    private static Task<IResult> ResetToPriorityAsync(
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.Allocation, async () =>
        {
            var mode = await db.PlanningSettings.AsNoTracking()
                .Where(settings => settings.Id == PlanningSettings.SingletonId)
                .Select(settings => settings.ExecutionOrderMode)
                .SingleAsync(cancellationToken);
            if (mode == ExecutionOrderMode.FollowPriority)
            {
                return Results.NoContent();
            }

            var priorityOrder = await DemandRanking.OrderedIdsAsync(db, byPriority: true, cancellationToken);
            await DemandRanking.SetRanksAsync(db, priorityOrder, priorityOrder, cancellationToken);
            _ = await SetModeAsync(db, ExecutionOrderMode.FollowPriority, cancellationToken);
            return Results.NoContent();
        }, cancellationToken);

    /// <summary>Grava o modo de ordem; devolve quantas linhas mudaram (sempre uma, o registro único de configurações).</summary>
    private static Task<int> SetModeAsync(CoordinDbContext db, ExecutionOrderMode mode, CancellationToken cancellationToken) =>
        db.PlanningSettings
            .Where(settings => settings.Id == PlanningSettings.SingletonId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(settings => settings.ExecutionOrderMode, mode), cancellationToken);

    /// <summary>
    /// A ordem precisa conter todas as demandas, uma única vez cada, e só ids existentes. Mesma regra de
    /// <c>PUT /api/demands/priority-order</c>, com a mesma mensagem.
    /// </summary>
    internal static async Task<IResult?> ValidateOrderAsync(List<Guid>? order, CoordinDbContext db, CancellationToken cancellationToken)
    {
        var known = (await db.Demands.AsNoTracking().Select(demand => demand.Id).ToListAsync(cancellationToken)).ToHashSet();
        var valid = order is not null
            && order.Count == known.Count
            && order.Distinct().Count() == order.Count
            && order.All(known.Contains);

        return valid ? null : ApiProblems.InvalidField("executionOrder", DemandErrors.ReorderInvalid);
    }

    private static Task<Dictionary<Guid, string>> TitlesAsync(CoordinDbContext db, CancellationToken cancellationToken) =>
        db.Demands.AsNoTracking().ToDictionaryAsync(demand => demand.Id, demand => demand.Title, cancellationToken);
}
