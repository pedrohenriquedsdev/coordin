using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Ponto de entrada do motor de previsão (DT-02): função pura, sem banco, relógio ou aleatoriedade.
/// Valida o snapshot, classifica as demandas (RF-10), simula a execução (DT-21), aplica a margem
/// (DT-22) e devolve as previsões e o consumo diário por pessoa, sempre na mesma ordem (RNF-02).
/// </summary>
public static class ForecastEngine
{
    public static ForecastResult Calculate(PlanningSnapshot snapshot)
    {
        PlanningSnapshotValidator.Validate(snapshot);

        var classification = PredictabilityClassifier.Classify(snapshot);
        var calendar = new WorkCalendar(snapshot.Holidays.Select(holiday => holiday.Date));
        var capacity = new PersonCapacityCalendar(calendar, snapshot.People, snapshot.Absences);

        var included = classification
            .Where(pair => pair.Value.Status == DemandStatus.Predictable)
            .Select(pair => pair.Key)
            .ToHashSet();

        var queueOrder = snapshot.Demands
            .OrderBy(demand => snapshot.OrderMode == ExecutionOrderMode.FollowPriority ? demand.PriorityRank : demand.ExecutionRank)
            .ThenBy(demand => demand.Id)
            .ToList();

        var outcome = ForecastSimulator.Run(snapshot, included, calendar, capacity);

        // Demanda que não termina em 5 anos vira NoCapacity (design D7). Só a de maior prioridade entre
        // as que não terminaram é retirada por rodada: ela não é atrasada por nenhuma anterior, então a
        // falha é dela; as de trás podem só estar sem vez por causa dela. Retirar libera capacidade, e
        // a simulação é refeita até todas as restantes terminarem.
        var overflowed = new HashSet<Guid>();
        while (outcome.Unfinished.Count > 0)
        {
            var culprit = queueOrder.First(demand => outcome.Unfinished.Contains(demand.Id)).Id;
            overflowed.Add(culprit);
            included.Remove(culprit);
            outcome = ForecastSimulator.Run(snapshot, included, calendar, capacity);
        }

        var forecasts = queueOrder
            .Select(demand => ToForecast(demand.Id, classification[demand.Id], overflowed, outcome, snapshot, calendar))
            .ToList();

        var consumed = outcome.Consumed
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.PersonId)
            .ThenBy(entry => entry.DemandId)
            .ToList();

        return new ForecastResult(forecasts, consumed);
    }

    private static DemandForecast ToForecast(
        Guid demandId,
        Classification classification,
        HashSet<Guid> overflowed,
        ForecastSimulator.Outcome outcome,
        PlanningSnapshot snapshot,
        WorkCalendar calendar)
    {
        if (classification.Status == DemandStatus.Completed)
        {
            return new DemandForecast(demandId, DemandStatus.Completed, null, null, null, null, null);
        }

        if (overflowed.Contains(demandId))
        {
            return Unpredictable(demandId, UnpredictableReason.NoCapacity);
        }

        if (classification.Status == DemandStatus.Unpredictable)
        {
            return Unpredictable(demandId, classification.Reason!.Value);
        }

        var (start, end) = outcome.Finished[demandId];
        var (marginDays, endWithMargin) = SafetyMargin.Apply(start, end, snapshot.SafetyMarginPercent, calendar);
        return new DemandForecast(demandId, DemandStatus.Predictable, null, start, end, marginDays, endWithMargin);
    }

    private static DemandForecast Unpredictable(Guid demandId, UnpredictableReason reason) =>
        new(demandId, DemandStatus.Unpredictable, reason, null, null, null, null);
}
