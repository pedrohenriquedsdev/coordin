using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Cronograma (RF-11). Não recalcula nada: lê as previsões gravadas (DT-03) e monta a resposta. O alerta
/// de data de fim informada e os desvios são comparações de datas já gravadas, não regras de previsão.
/// </summary>
public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedule").WithTags("Schedule");

        group.MapGet("", GetScheduleAsync)
            .WithName("GetSchedule")
            .WithSummary("Cronograma de todas as demandas na ordem de execução, com previsões e desvios.")
            .Produces<ScheduleResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> GetScheduleAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.PlanningSettings.AsNoTracking()
            .SingleAsync(item => item.Id == PlanningSettings.SingletonId, cancellationToken);

        var demands = await db.Demands.AsNoTracking()
            .Select(demand => new DemandRow(
                demand.Id,
                demand.Title,
                demand.PriorityRank,
                demand.ExecutionRank,
                demand.StartDate,
                demand.InformedEndDate,
                demand.CompletedOn))
            .ToListAsync(cancellationToken);

        var forecasts = (await db.Forecasts.AsNoTracking().ToListAsync(cancellationToken))
            .ToDictionary(forecast => forecast.DemandId);

        var parts = await db.DemandTeams.AsNoTracking()
            .Select(part => new PartRow(part.DemandId, part.TeamId, part.Team!.Name, part.EstimatedEffortHours))
            .ToListAsync(cancellationToken);

        var partsByDemand = parts
            .GroupBy(part => part.DemandId)
            .ToDictionary(group => group.Key, group => group.OrderBy(part => part.TeamName, StringComparer.Ordinal).ToList());

        var customOrder = settings.ExecutionOrderMode == ExecutionOrderMode.Custom;
        var ordered = customOrder
            ? demands.OrderBy(demand => demand.ExecutionRank)
            : demands.OrderBy(demand => demand.PriorityRank);

        var items = ordered
            .ThenBy(demand => demand.Id)
            .Select(demand => ToItem(
                demand,
                customOrder,
                forecasts.GetValueOrDefault(demand.Id),
                partsByDemand.GetValueOrDefault(demand.Id) ?? []))
            .ToList();

        return Results.Ok(new ScheduleResponse(settings.ExecutionOrderMode, items));
    }

    private static ScheduleItemResponse ToItem(
        DemandRow demand,
        bool customOrder,
        Forecast? forecast,
        List<PartRow> parts)
    {
        var isPredictable = forecast?.IsPredictable ?? false;
        var exceeds = isPredictable
            && demand.InformedEndDate is { } informed
            && forecast!.ForecastEndDate is { } end
            && end > informed;

        // Desvios em dias corridos (DT-27): conclusão menos a linha de base e menos a previsão vigente
        // na conclusão. A previsão de uma demanda concluída fica congelada, então o valor é o da conclusão.
        int? baselineDeviation = null;
        int? forecastDeviation = null;
        if (demand.CompletedOn is { } completedOn)
        {
            if (forecast?.BaselineEndDate is { } baseline)
            {
                baselineDeviation = completedOn.DayNumber - baseline.DayNumber;
            }

            if (forecast?.ForecastEndDate is { } vigente)
            {
                forecastDeviation = completedOn.DayNumber - vigente.DayNumber;
            }
        }

        return new ScheduleItemResponse(
            demand.Id,
            demand.Title,
            demand.PriorityRank,
            demand.ExecutionRank,
            customOrder && demand.ExecutionRank != demand.PriorityRank,
            parts.Select(part => new ScheduleTeamResponse(part.TeamId, part.TeamName, part.EstimatedEffortHours)).ToList(),
            parts.Sum(part => part.EstimatedEffortHours ?? 0m),
            demand.StartDate,
            isPredictable,
            forecast?.UnpredictableReason,
            forecast?.ForecastStartDate,
            forecast?.EndDateWithoutMargin,
            forecast?.ForecastEndDate,
            demand.InformedEndDate,
            exceeds,
            demand.CompletedOn,
            forecast?.BaselineEndDate,
            baselineDeviation,
            forecastDeviation);
    }

    /// <summary>Uma linha de demanda, como o cronograma precisa dela.</summary>
    private sealed record DemandRow(
        Guid Id,
        string Title,
        int PriorityRank,
        int ExecutionRank,
        DateOnly? StartDate,
        DateOnly? InformedEndDate,
        DateOnly? CompletedOn);

    /// <summary>Uma parte de demanda, com o nome do time.</summary>
    private sealed record PartRow(Guid DemandId, Guid TeamId, string TeamName, decimal? EstimatedEffortHours);
}
