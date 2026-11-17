using Coordin.Engine.Capacity;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// Avalia uma ordem de execução contra o cronograma atual e calcula o score do DT-25 (RF-13, RF-14).
/// Função pura: não altera o snapshot, não grava nada e nunca decide uma ordem sozinha (regra inviolável 2).
/// Esta é a versão simples e correta; o caminho acelerado vem depois, sem mudar nenhum resultado.
/// </summary>
public static class ScenarioEvaluator
{
    /// <summary>
    /// Calcula o cronograma atual e o da ordem <paramref name="order"/> e compara os dois.
    /// </summary>
    /// <param name="snapshot">O planejamento vigente.</param>
    /// <param name="order">Os ids de todas as demandas, uma vez cada, na ordem de execução proposta.</param>
    /// <exception cref="ArgumentException">A ordem não tem todas as demandas, uma única vez cada.</exception>
    public static Scenario Evaluate(PlanningSnapshot snapshot, IReadOnlyList<Guid> order)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(order);

        var scenarioSnapshot = ExecutionOrders.WithOrder(snapshot, order); // também valida a ordem
        var current = ForecastEngine.Calculate(snapshot);
        var proposed = ForecastEngine.Calculate(scenarioSnapshot);
        var calendar = new WorkCalendar(snapshot.Holidays.Select(holiday => holiday.Date));

        return Build(snapshot, order, current, proposed, calendar);
    }

    private static Scenario Build(
        PlanningSnapshot snapshot,
        IReadOnlyList<Guid> order,
        ForecastResult current,
        ForecastResult proposed,
        WorkCalendar calendar)
    {
        var currentById = current.Demands.ToDictionary(forecast => forecast.DemandId);
        var proposedById = proposed.Demands.ToDictionary(forecast => forecast.DemandId);
        var currentPredictable = PredictableIds(current);
        var proposedPredictable = PredictableIds(proposed);
        var isComparable = currentPredictable.SetEquals(proposedPredictable);

        // O score só considera o que é previsível nos dois cronogramas; concluídas e não previsíveis ficam de fora.
        var scored = order.Where(id => currentPredictable.Contains(id) && proposedPredictable.Contains(id)).ToList();
        if (scored.Count == 0)
        {
            return new Scenario([.. order], [], null, 0, new ScenarioScore(0, 0, 0), isComparable);
        }

        var demandsById = snapshot.Demands.ToDictionary(demand => demand.Id);
        var positionOf = order.Select((id, index) => (id, position: index + 1)).ToDictionary(item => item.id, item => item.position);
        var baseline = scored.Min(id => demandsById[id].StartDate!.Value);
        var highestPriority = snapshot.Demands.Max(demand => demand.PriorityRank);

        var items = new List<ScenarioItem>();
        long currentScore = 0;
        long scenarioScore = 0;
        foreach (var id in scored)
        {
            var before = currentById[id];
            var after = proposedById[id];
            var weight = highestPriority - demandsById[id].PriorityRank + 1;
            var currentPart = (long)weight * calendar.CountBusinessDays(baseline, before.ForecastEndDate!.Value);
            var scenarioPart = (long)weight * calendar.CountBusinessDays(baseline, after.ForecastEndDate!.Value);
            currentScore += currentPart;
            scenarioScore += scenarioPart;

            items.Add(new ScenarioItem(
                id,
                demandsById[id].PriorityRank,
                positionOf[id],
                before.ForecastEndDate!.Value,
                after.ForecastStartDate!.Value,
                after.ForecastEndDate!.Value,
                after.ForecastEndDate!.Value.DayNumber - before.ForecastEndDate!.Value.DayNumber,
                weight,
                scenarioPart - currentPart));
        }

        var currentSector = scored.Max(id => currentById[id].ForecastEndDate!.Value);
        var scenarioSector = scored.Max(id => proposedById[id].ForecastEndDate!.Value);

        return new Scenario(
            [.. order],
            items,
            scenarioSector,
            scenarioSector.DayNumber - currentSector.DayNumber,
            new ScenarioScore(currentScore, scenarioScore, scenarioScore - currentScore),
            isComparable);
    }

    private static HashSet<Guid> PredictableIds(ForecastResult result) =>
        [.. result.Demands.Where(forecast => forecast.Status == DemandStatus.Predictable).Select(forecast => forecast.DemandId)];
}
