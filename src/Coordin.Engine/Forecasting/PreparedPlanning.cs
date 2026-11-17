using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Um planejamento preparado para ser calculado várias vezes com ordens de execução diferentes (design D2).
/// Guarda o que não depende da ordem (snapshot validado, classificação, calendário e capacidade) e roda uma
/// ordem por chamada. É o <b>único</b> caminho de cálculo: <see cref="ForecastEngine.Calculate"/> e a
/// avaliação de cenários passam por aqui, então não podem divergir. Cada <see cref="Run"/> descarta o
/// próprio estado, e uma rodada nunca contamina a seguinte.
/// </summary>
internal sealed class PreparedPlanning
{
    private readonly PlanningSnapshot _snapshot;
    private readonly IReadOnlyDictionary<Guid, Classification> _classification;
    private readonly Dictionary<Guid, DemandInput> _demandsById;
    private readonly HashSet<Guid> _predictable;
    private readonly SimulationTables _tables;
    private readonly ILookup<Guid, Guid> _prerequisitesOf;
    private readonly ILookup<Guid, Guid> _dependentsOf;

    /// <exception cref="ArgumentException">O snapshot é incoerente (referência inexistente, ciclo etc.).</exception>
    public PreparedPlanning(PlanningSnapshot snapshot)
    {
        PlanningSnapshotValidator.Validate(snapshot);

        _snapshot = snapshot;
        _classification = PredictabilityClassifier.Classify(snapshot);
        Calendar = new WorkCalendar(snapshot.Holidays.Select(holiday => holiday.Date));
        _tables = new SimulationTables(snapshot, new PersonCapacityCalendar(Calendar, snapshot.People, snapshot.Absences));
        _demandsById = snapshot.Demands.ToDictionary(demand => demand.Id);
        _predictable = [.. _classification.Where(pair => pair.Value.Status == DemandStatus.Predictable).Select(pair => pair.Key)];
        _prerequisitesOf = snapshot.Dependencies.ToLookup(dependency => dependency.DemandId, dependency => dependency.PrerequisiteId);
        _dependentsOf = snapshot.Dependencies.ToLookup(dependency => dependency.PrerequisiteId, dependency => dependency.DemandId);
    }

    /// <summary>O calendário de dias úteis do planejamento.</summary>
    public WorkCalendar Calendar { get; }

    /// <summary>
    /// Calcula as previsões para a fila na ordem <paramref name="executionOrder"/>, com os ids de todas as
    /// demandas, uma vez cada. Sem <paramref name="collectConsumption"/>, o consumo diário vem vazio.
    /// </summary>
    /// <exception cref="ArgumentException">A ordem não tem todas as demandas, uma única vez cada.</exception>
    public ForecastResult Run(IReadOnlyList<Guid> executionOrder, bool collectConsumption)
    {
        ArgumentNullException.ThrowIfNull(executionOrder);

        var queueOrder = ToQueue(executionOrder);
        var position = new int[_tables.DemandCount];
        for (var index = 0; index < queueOrder.Count; index++)
        {
            position[_tables.DemandIndex[queueOrder[index].Id]] = index;
        }

        var included = new HashSet<Guid>(_predictable);
        var outcome = Simulate(included, position, collectConsumption);

        // Demanda que não termina em 5 anos vira NoCapacity (design D7). Com dependências, "não terminou"
        // não basta para apontar a causa: uma dependente de maior prioridade fica sem terminar só por
        // esperar o pré-requisito. A causa é a de maior prioridade entre as não terminadas que não
        // esperam nenhuma outra não terminada (sempre existe uma, pois o grafo não tem ciclo). Ela
        // não é atrasada por ninguém, então a falha é dela; as de trás podem só estar sem vez. A causa
        // é retirada com todas as que dependem dela, e a simulação é refeita até todas as restantes
        // terminarem.
        var overflowed = new HashSet<Guid>();
        var blockedByOverflow = new HashSet<Guid>();
        while (outcome.Unfinished.Count > 0)
        {
            var culprit = queueOrder
                .First(demand => outcome.Unfinished.Contains(demand.Id)
                    && !_prerequisitesOf[demand.Id].Any(outcome.Unfinished.Contains))
                .Id;
            overflowed.Add(culprit);
            included.Remove(culprit);

            foreach (var dependent in TransitiveDependents(culprit))
            {
                if (included.Remove(dependent))
                {
                    blockedByOverflow.Add(dependent);
                }
            }

            outcome = Simulate(included, position, collectConsumption);
        }

        var forecasts = queueOrder
            .Select(demand => ToForecast(demand.Id, overflowed, blockedByOverflow, outcome))
            .ToList();

        var consumed = collectConsumption
            ? outcome.Consumed
                .OrderBy(entry => entry.Date)
                .ThenBy(entry => entry.PersonId)
                .ThenBy(entry => entry.DemandId)
                .ToList()
            : [];

        return new ForecastResult(forecasts, consumed);
    }

    private ForecastSimulator.Outcome Simulate(HashSet<Guid> included, int[] position, bool collectConsumption)
    {
        var flags = new bool[_tables.DemandCount];
        foreach (var id in included)
        {
            flags[_tables.DemandIndex[id]] = true;
        }

        return ForecastSimulator.Run(_tables, flags, position, _snapshot.SafetyMarginPercent, Calendar, collectConsumption);
    }

    /// <summary>As demandas na ordem pedida, conferindo que é uma permutação completa.</summary>
    private List<DemandInput> ToQueue(IReadOnlyList<Guid> executionOrder)
    {
        if (executionOrder.Count != _demandsById.Count)
        {
            throw new ArgumentException("A ordem precisa conter todas as demandas, uma única vez cada.", nameof(executionOrder));
        }

        var seen = new HashSet<Guid>();
        var queue = new List<DemandInput>(executionOrder.Count);
        foreach (var id in executionOrder)
        {
            if (!_demandsById.TryGetValue(id, out var demand) || !seen.Add(id))
            {
                throw new ArgumentException("A ordem precisa conter todas as demandas, uma única vez cada.", nameof(executionOrder));
            }

            queue.Add(demand);
        }

        return queue;
    }

    /// <summary>Todas as demandas que dependem de <paramref name="demandId"/>, direta ou indiretamente.</summary>
    private IEnumerable<Guid> TransitiveDependents(Guid demandId)
    {
        var seen = new HashSet<Guid>();
        var pending = new Stack<Guid>([demandId]);
        while (pending.Count > 0)
        {
            foreach (var dependent in _dependentsOf[pending.Pop()])
            {
                if (seen.Add(dependent))
                {
                    pending.Push(dependent);
                    yield return dependent;
                }
            }
        }
    }

    private DemandForecast ToForecast(
        Guid demandId,
        HashSet<Guid> overflowed,
        HashSet<Guid> blockedByOverflow,
        ForecastSimulator.Outcome outcome)
    {
        var classification = _classification[demandId];
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
