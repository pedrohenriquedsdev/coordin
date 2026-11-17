using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// Um planejamento pronto para avaliar muitas ordens (RF-13; DT-25, DT-26): o plano é preparado e o
/// cronograma atual é calculado <b>uma vez</b>, e cada ordem avaliada só paga o cálculo da própria ordem.
/// A busca usa <see cref="EvaluateLean"/>, que calcula o score e o prazo do setor sem montar os itens, e só
/// os cenários finais passam por <see cref="Evaluate"/>. Não é seguro para várias threads ao mesmo tempo.
/// </summary>
internal sealed class ScenarioContext
{
    private readonly PlanningSnapshot _snapshot;
    private readonly PreparedPlanning _plan;
    private readonly Dictionary<Guid, DemandForecast> _currentById;
    private readonly HashSet<Guid> _currentPredictable;
    private readonly Dictionary<Guid, DemandInput> _demandsById;
    private readonly int _highestPriority;

    /// <exception cref="ArgumentException">O snapshot é incoerente.</exception>
    public ScenarioContext(PlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshot = snapshot;
        _plan = new PreparedPlanning(snapshot);
        CurrentOrder = ExecutionOrders.Current(snapshot);
        var current = _plan.Run(CurrentOrder, collectConsumption: false);
        _currentById = current.Demands.ToDictionary(forecast => forecast.DemandId);
        _currentPredictable = PredictableIds(current);
        _demandsById = snapshot.Demands.ToDictionary(demand => demand.Id);
        _highestPriority = snapshot.Demands.Count == 0 ? 0 : snapshot.Demands.Max(demand => demand.PriorityRank);
    }

    /// <summary>A ordem de execução vigente, de todas as demandas.</summary>
    public IReadOnlyList<Guid> CurrentOrder { get; }

    /// <summary>O cenário completo da ordem, com os itens por demanda.</summary>
    /// <exception cref="ArgumentException">A ordem não tem todas as demandas, uma única vez cada.</exception>
    public Scenario Evaluate(IReadOnlyList<Guid> order) => Build(order, withItems: true);

    /// <summary>Como <see cref="Evaluate"/>, mas sem os itens: só o score, o prazo do setor e a comparabilidade.</summary>
    public Scenario EvaluateLean(IReadOnlyList<Guid> order) => Build(order, withItems: false);

    private Scenario Build(IReadOnlyList<Guid> order, bool withItems)
    {
        ArgumentNullException.ThrowIfNull(order);

        var proposed = _plan.Run(order, collectConsumption: false); // também valida a ordem
        var proposedById = proposed.Demands.ToDictionary(forecast => forecast.DemandId);
        var proposedPredictable = PredictableIds(proposed);
        var isComparable = _currentPredictable.SetEquals(proposedPredictable);

        // O score só considera o que é previsível nos dois cronogramas; concluídas e não previsíveis ficam de fora.
        var scored = order.Where(id => _currentPredictable.Contains(id) && proposedPredictable.Contains(id)).ToList();
        if (scored.Count == 0)
        {
            return new Scenario([.. order], [], null, 0, new ScenarioScore(0, 0, 0), isComparable);
        }

        var baseline = scored.Min(id => _demandsById[id].StartDate!.Value);

        // dᵢ = dias úteis de baseline até o fim, pontas incluídas (igual a CountBusinessDays): uma contagem
        // acumulada feita uma vez por avaliação, em vez de percorrer os dias para cada demanda.
        var lastDay = scored.Max(id => Math.Max(_currentById[id].ForecastEndDate!.Value.DayNumber, proposedById[id].ForecastEndDate!.Value.DayNumber));
        var businessDaysUntil = new int[lastDay - baseline.DayNumber + 1];
        var running = 0;
        for (var offset = 0; offset < businessDaysUntil.Length; offset++)
        {
            running += _plan.Calendar.IsBusinessDay(baseline.AddDays(offset)) ? 1 : 0;
            businessDaysUntil[offset] = running;
        }

        var positionOf = withItems ? order.Select((id, index) => (id, position: index + 1)).ToDictionary(item => item.id, item => item.position) : null;
        var items = withItems ? new List<ScenarioItem>(scored.Count) : null;
        long currentScore = 0;
        long scenarioScore = 0;
        var currentSector = DateOnly.MinValue;
        var scenarioSector = DateOnly.MinValue;
        foreach (var id in scored)
        {
            var before = _currentById[id].ForecastEndDate!.Value;
            var after = proposedById[id].ForecastEndDate!.Value;
            var weight = _highestPriority - _demandsById[id].PriorityRank + 1;
            var currentPart = (long)weight * businessDaysUntil[before.DayNumber - baseline.DayNumber];
            var scenarioPart = (long)weight * businessDaysUntil[after.DayNumber - baseline.DayNumber];
            currentScore += currentPart;
            scenarioScore += scenarioPart;
            currentSector = before > currentSector ? before : currentSector;
            scenarioSector = after > scenarioSector ? after : scenarioSector;

            items?.Add(new ScenarioItem(
                id,
                _demandsById[id].PriorityRank,
                positionOf![id],
                before,
                proposedById[id].ForecastStartDate!.Value,
                after,
                after.DayNumber - before.DayNumber,
                weight,
                scenarioPart - currentPart));
        }

        return new Scenario(
            [.. order],
            items ?? [],
            scenarioSector,
            scenarioSector.DayNumber - currentSector.DayNumber,
            new ScenarioScore(currentScore, scenarioScore, scenarioScore - currentScore),
            isComparable);
    }

    private static HashSet<Guid> PredictableIds(ForecastResult result) =>
        [.. result.Demands.Where(forecast => forecast.Status == DemandStatus.Predictable).Select(forecast => forecast.DemandId)];
}
