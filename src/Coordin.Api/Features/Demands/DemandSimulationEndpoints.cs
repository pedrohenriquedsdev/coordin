using Coordin.Api.Features.Planning;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Simulação de nova demanda (RF-16; DT-17; design D10): mesma validação de <c>POST /api/demands</c>, mesmo
/// corpo, sem gravar nada. Quem monta o snapshot é o <see cref="PlanningSnapshotLoader"/>; a conta é do motor.
/// </summary>
public static class DemandSimulationEndpoints
{
    public static IEndpointRouteBuilder MapDemandSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/demands/simulate", SimulateDemandAsync)
            .WithName("SimulateDemand")
            .WithSummary("Simula incluir uma demanda na posição escolhida, sem gravar nada (RF-16).")
            .WithTags("Demands")
            .Produces<DemandSimulationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> SimulateDemandAsync(
        DemandRequest request,
        CoordinDbContext db,
        PlanningSnapshotLoader loader,
        CancellationToken cancellationToken)
    {
        var invalid = await DemandEndpoints.ValidateNewDemandAsync(request, db, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var id = Guid.CreateVersion7(); // id temporário: a demanda nunca é gravada
        // Sem times também é válido (a demanda fica sem parte e não é previsível): a lista vazia cobre o caso.
        var parts = (request.Teams ?? []).Select(part => new DemandPartInput(part.TeamId!.Value, part.EstimatedEffortHours)).ToList();
        var demand = new DemandInput(id, 0, 0, request.StartDate, false, parts);
        var assignments = (request.AssignedPersonIds ?? []).Select(person => new AssignmentInput(id, person)).ToList();
        var dependencies = (request.PrerequisiteIds ?? []).Select(prerequisite => new DependencyInput(id, prerequisite)).ToList();

        var impact = NewDemandSimulation.Simulate(snapshot, demand, assignments, dependencies, request.Position);

        var titles = await db.Demands.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken);
        var changes = impact.Changes
            .Select(change => new DemandChangeResponse(
                change.DemandId,
                titles.GetValueOrDefault(change.DemandId, string.Empty),
                change.CurrentForecastEndDate,
                change.NewForecastEndDate,
                change.DeltaDays))
            .ToList();

        var forecast = impact.NewDemand;
        return Results.Ok(new DemandSimulationResponse(
            forecast.Status == DemandStatus.Predictable,
            forecast.Reason?.ToString(),
            forecast.ForecastStartDate,
            forecast.EndDateWithoutMargin,
            forecast.ForecastEndDate,
            impact.Position,
            changes,
            impact.SectorEndDate,
            impact.SectorDeltaDays));
    }
}
