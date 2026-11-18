namespace Coordin.Engine.Accuracy;

/// <summary>
/// Calcula os indicadores de precisão de RF-23 sobre as demandas concluídas no período (DT-27). Função pura e
/// determinística: não lê o relógio, e o resultado não depende da ordem das demandas.
/// </summary>
public static class AccuracyCalculator
{
    /// <summary>
    /// Os indicadores do período, com data inicial e final inclusive pela data de conclusão.
    /// </summary>
    /// <exception cref="ArgumentException">Quando a data inicial é posterior à final.</exception>
    public static AccuracyResult Calculate(IEnumerable<CompletedDelivery> deliveries, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        if (from > to)
        {
            throw new ArgumentException("A data inicial é posterior à final.", nameof(from));
        }

        var inPeriod = deliveries
            .Where(delivery => delivery.CompletedOn >= from && delivery.CompletedOn <= to)
            .OrderBy(delivery => delivery.DemandId)
            .ToList();

        var sector = Indicators(inPeriod);

        var teamIds = inPeriod.SelectMany(delivery => delivery.TeamIds).Distinct().OrderBy(id => id).ToList();
        var teams = new List<TeamAccuracy>();
        foreach (var teamId in teamIds)
        {
            // Demanda com vários times entra em cada um deles (premissa aprovada, spec accuracy-indicators).
            var ofTeam = inPeriod.Where(delivery => delivery.TeamIds.Contains(teamId)).ToList();
            var indicators = Indicators(ofTeam);
            var isAbove = indicators.AverageEstimateDifferencePercent is { } team
                && sector.AverageEstimateDifferencePercent is { } whole
                && Math.Abs(team) > Math.Abs(whole);
            teams.Add(new TeamAccuracy(teamId, indicators, isAbove));
        }

        return new AccuracyResult(from, to, sector, teams);
    }

    private static AccuracyIndicators Indicators(List<CompletedDelivery> deliveries)
    {
        // Demanda sem previsão de conclusão (anterior a esta change) fica de fora de tudo.
        var withSnapshot = deliveries.Where(delivery => delivery.ForecastEndDateAtCompletion is not null).ToList();
        var excludedWithoutSnapshot = deliveries.Count - withSnapshot.Count;

        var withBaseline = withSnapshot.Where(delivery => delivery.BaselineEndDate is not null).ToList();
        var excludedWithoutBaseline = withSnapshot.Count - withBaseline.Count;

        var withActual = withSnapshot.Where(delivery => delivery.ActualEffortHours is not null).ToList();
        var excludedWithoutActual = withSnapshot.Count - withActual.Count;

        var withEstimate = withActual
            .Where(delivery => delivery.EstimatedEffortHoursAtCompletion is > 0m)
            .ToList();
        var excludedWithoutEstimate = withActual.Count - withEstimate.Count;

        return new AccuracyIndicators(
            UsedCount: withSnapshot.Count,
            AverageDeviationFromBaselineDays: Average(withBaseline.Select(delivery =>
                (decimal)delivery.CompletedOn.DayNumber - delivery.BaselineEndDate!.Value.DayNumber)),
            AverageDeviationFromCompletionForecastDays: Average(withSnapshot.Select(delivery =>
                (decimal)delivery.CompletedOn.DayNumber - delivery.ForecastEndDateAtCompletion!.Value.DayNumber)),
            OnTimePercentage: withBaseline.Count == 0
                ? null
                : 100m * withBaseline.Count(delivery => delivery.CompletedOn <= delivery.BaselineEndDate!.Value) / withBaseline.Count,
            AverageEstimateDifferencePercent: Average(withEstimate.Select(delivery =>
                100m * (delivery.ActualEffortHours!.Value - delivery.EstimatedEffortHoursAtCompletion!.Value)
                / delivery.EstimatedEffortHoursAtCompletion!.Value)),
            ExcludedWithoutSnapshot: excludedWithoutSnapshot,
            ExcludedWithoutBaseline: excludedWithoutBaseline,
            ExcludedWithoutActualEffort: excludedWithoutActual,
            ExcludedWithoutEstimate: excludedWithoutEstimate);
    }

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Sum() / list.Count;
    }
}
