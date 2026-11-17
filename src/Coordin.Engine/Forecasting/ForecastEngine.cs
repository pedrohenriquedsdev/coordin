using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Ponto de entrada do motor de previsão (DT-02): função pura, sem banco, relógio ou aleatoriedade.
/// Valida o snapshot, classifica as demandas (RF-10), simula a execução (DT-21), aplica a margem
/// (DT-22) e devolve as previsões e o consumo diário por pessoa, sempre na mesma ordem (RNF-02).
/// Uma demanda só começa depois do fim previsto, com margem, dos seus pré-requisitos (RF-18; DT-23).
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

        // Demanda que não termina em 5 anos vira NoCapacity (design D7). Com dependências, "não terminou"
        // não basta para apontar a causa: uma dependente de maior prioridade fica sem terminar só por
        // esperar o pré-requisito. A causa é a de maior prioridade entre as não terminadas que não
        // esperam nenhuma outra não terminada (sempre existe uma, pois o grafo não tem ciclo). Ela
        // não é atrasada por ninguém, então a falha é dela; as de trás podem só estar sem vez. A causa
        // é retirada com todas as que dependem dela, e a simulação é refeita até todas as restantes
        // terminarem.
        var prerequisitesOf = snapshot.Dependencies.ToLookup(dependency => dependency.DemandId, dependency => dependency.PrerequisiteId);
        var dependentsOf = snapshot.Dependencies.ToLookup(dependency => dependency.PrerequisiteId, dependency => dependency.DemandId);
        var overflowed = new HashSet<Guid>();
        var blockedByOverflow = new HashSet<Guid>();
        while (outcome.Unfinished.Count > 0)
        {
            var culprit = queueOrder
                .First(demand => outcome.Unfinished.Contains(demand.Id)
                    && !prerequisitesOf[demand.Id].Any(outcome.Unfinished.Contains))
                .Id;
            overflowed.Add(culprit);
            included.Remove(culprit);

            foreach (var dependent in TransitiveDependents(culprit, dependentsOf))
            {
                if (included.Remove(dependent))
                {
                    blockedByOverflow.Add(dependent);
                }
            }

            outcome = ForecastSimulator.Run(snapshot, included, calendar, capacity);
        }

        var forecasts = queueOrder
            .Select(demand => ToForecast(demand.Id, classification[demand.Id], overflowed, blockedByOverflow, outcome))
            .ToList();

        var consumed = outcome.Consumed
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.PersonId)
            .ThenBy(entry => entry.DemandId)
            .ToList();

        return new ForecastResult(forecasts, consumed);
    }

    /// <summary>Todas as demandas que dependem de <paramref name="demandId"/>, direta ou indiretamente.</summary>
    private static IEnumerable<Guid> TransitiveDependents(Guid demandId, ILookup<Guid, Guid> dependentsOf)
    {
        var seen = new HashSet<Guid>();
        var pending = new Stack<Guid>([demandId]);
        while (pending.Count > 0)
        {
            foreach (var dependent in dependentsOf[pending.Pop()])
            {
                if (seen.Add(dependent))
                {
                    pending.Push(dependent);
                    yield return dependent;
                }
            }
        }
    }

    private static DemandForecast ToForecast(
        Guid demandId,
        Classification classification,
        HashSet<Guid> overflowed,
        HashSet<Guid> blockedByOverflow,
        ForecastSimulator.Outcome outcome)
    {
        if (classification.Status == DemandStatus.Completed)
        {
            return new DemandForecast(demandId, DemandStatus.Completed, null, null, null, null, null);
        }

        if (overflowed.Contains(demandId))
        {
            return Unpredictable(demandId, UnpredictableReason.NoCapacity);
        }

        if (blockedByOverflow.Contains(demandId))
        {
            return Unpredictable(demandId, UnpredictableReason.PrerequisiteUnpredictable);
        }

        if (classification.Status == DemandStatus.Unpredictable)
        {
            return Unpredictable(demandId, classification.Reason!.Value);
        }

        // A margem já foi calculada pela simulação, no dia em que a demanda terminou (design D4).
        var finished = outcome.Finished[demandId];
        return new DemandForecast(demandId, DemandStatus.Predictable, null, finished.Start, finished.End, finished.MarginBusinessDays, finished.EndWithMargin);
    }

    private static DemandForecast Unpredictable(Guid demandId, UnpredictableReason reason) =>
        new(demandId, DemandStatus.Unpredictable, reason, null, null, null, null);
}
