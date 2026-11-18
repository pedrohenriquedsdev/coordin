using Coordin.Api.Features.Demands;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Validation.Dataset;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Planning;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Features.Validation;

/// <summary>
/// A validação por simulação (RF-24; DT-37). Só existe com <c>Validation:Enabled</c> verdadeiro: com a área
/// desligada, as rotas não são mapeadas, e o caminho responde <c>404</c>, sem nem aparecer na documentação.
/// </summary>
public static class ValidationEndpoints
{
    public static IServiceCollection AddCoordinValidation(this IServiceCollection services)
    {
        services.AddOptions<ValidationOptions>()
            .BindConfiguration(ValidationOptions.SectionName);
        return services;
    }

    public static IEndpointRouteBuilder MapValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ValidationOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        var group = app.MapGroup("/api/validation").WithTags("Validation");

        group.MapGet("/status", GetStatusAsync)
            .WithName("GetValidationStatus")
            .WithSummary("Estado da validação: demandas, linhas de base e planejamento do processo atual guardado (RF-24).")
            .Produces<ValidationStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/dataset", LoadDatasetAsync)
            .WithName("LoadValidationDataset")
            .WithSummary("Substitui o planejamento inteiro pelo conjunto fictício da simulação. Exige confirmReplaceAll (RF-24).")
            .Produces<LoadDatasetResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/baseline", GetBaselineAsync)
            .WithName("GetValidationBaseline")
            .WithSummary("Linha de base do gestor, uma data por demanda (RF-24, passo 2).")
            .Produces<IReadOnlyList<BaselineResponse>>(StatusCodes.Status200OK);

        group.MapPut("/baseline", SaveBaselineAsync)
            .WithName("PutValidationBaseline")
            .WithSummary("Substitui a linha de base inteira (RF-24, passo 2).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/current-plan", CaptureCurrentPlanAsync)
            .WithName("CaptureCurrentPlan")
            .WithSummary("Guarda o planejamento do processo atual, na ordem de prioridade, uma única vez (RF-24, passo 3).")
            .Produces<CurrentPlanResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetStatusAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var demandCount = await db.Demands.AsNoTracking().CountAsync(cancellationToken);
        var baselineCount = await db.ValidationBaselines.AsNoTracking().CountAsync(cancellationToken);
        var plan = await db.ValidationCurrentPlans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == ValidationCurrentPlan.SingletonId, cancellationToken);

        return Results.Ok(new ValidationStatusResponse(
            demandCount,
            baselineCount,
            plan is not null,
            plan?.CapturedAt));
    }

    /// <summary>
    /// Sem <c>confirmReplaceAll</c> verdadeiro, responde 400 com o que seria substituído e não grava nada. Com a
    /// confirmação, o carregamento roda dentro da transação de planejamento: recalcula e confirma, ou desfaz tudo.
    /// </summary>
    private static async Task<IResult> LoadDatasetAsync(
        LoadDatasetRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken)
    {
        if (!request.ConfirmReplaceAll)
        {
            var current = await ValidationDatasetLoader.CountCurrentAsync(db, cancellationToken);
            var plan = current.CurrentPlanCaptured ? "o plano atual guardado" : "nenhum plano guardado";
            return ApiProblems.InvalidField(
                "confirmReplaceAll",
                $"Carregar o conjunto substitui o planejamento inteiro: {current.Teams} times, {current.People} pessoas, "
                + $"{current.Absences} ausências, {current.Holidays} feriados, {current.Demands} demandas, "
                + $"{current.Baselines} linhas de base e {plan}. Envie confirmReplaceAll: true para confirmar.");
        }

        var dataset = ValidationDataset.Read();
        return await PlanningTransaction.RunAsync(
            db,
            recalculator,
            () => ValidationDatasetLoader.ReplaceAsync(db, dataset, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> GetBaselineAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var demands = await db.Demands.AsNoTracking()
            .OrderBy(demand => demand.PriorityRank)
            .Select(demand => new { demand.Id, demand.Title })
            .ToListAsync(cancellationToken);
        var baselines = await db.ValidationBaselines.AsNoTracking()
            .ToDictionaryAsync(baseline => baseline.DemandId, baseline => baseline.BaselineDate, cancellationToken);

        return Results.Ok(demands
            .Select(demand => new BaselineResponse(
                demand.Id,
                demand.Title,
                baselines.TryGetValue(demand.Id, out var date) ? date : null))
            .ToList());
    }

    /// <summary>Substitui a linha de base inteira. Demanda desconhecida ou repetida é recusada, e nada é gravado.</summary>
    private static async Task<IResult> SaveBaselineAsync(
        SaveBaselineRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var items = request.Baselines ?? [];
        var known = (await db.Demands.AsNoTracking().Select(demand => demand.Id).ToListAsync(cancellationToken)).ToHashSet();

        if (items.Any(item => !known.Contains(item.DemandId)))
        {
            return ApiProblems.InvalidField("baselines", "A linha de base tem uma demanda que não existe.");
        }

        if (items.Select(item => item.DemandId).Distinct().Count() != items.Count)
        {
            return ApiProblems.InvalidField("baselines", "A linha de base tem a mesma demanda mais de uma vez.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ValidationBaselines.ExecuteDeleteAsync(cancellationToken);
        db.ValidationBaselines.AddRange(items.Select(item => new ValidationBaseline
        {
            DemandId = item.DemandId,
            BaselineDate = item.BaselineDate,
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Guarda o planejamento atual uma única vez, com a ordem forçada a prioridade (PRD 7.3, passo 3). Uma segunda
    /// chamada responde 409, e o plano guardado não muda.
    /// </summary>
    private static async Task<IResult> CaptureCurrentPlanAsync(
        CoordinDbContext db,
        PlanningSnapshotLoader loader,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (await db.ValidationCurrentPlans.AnyAsync(plan => plan.Id == ValidationCurrentPlan.SingletonId, cancellationToken))
        {
            return ApiProblems.Conflict(
                "validation-current-plan-exists",
                "O planejamento do processo atual já foi guardado; só um novo carregamento do conjunto o apaga.");
        }

        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var plan = new ValidationCurrentPlan
        {
            SnapshotJson = ValidationSnapshotJson.Serialize(snapshot with { OrderMode = ExecutionOrderMode.FollowPriority }),
            CapturedAt = clock.GetUtcNow(),
        };

        try
        {
            db.ValidationCurrentPlans.Add(plan);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Duas chamadas simultâneas: a restrição do banco decide quem ganha, e a outra recebe o 409.
            return ApiProblems.Conflict(
                "validation-current-plan-exists",
                "O planejamento do processo atual já foi guardado; só um novo carregamento do conjunto o apaga.");
        }

        return Results.Created("/api/validation/status", new CurrentPlanResponse(plan.CapturedAt));
    }
}
