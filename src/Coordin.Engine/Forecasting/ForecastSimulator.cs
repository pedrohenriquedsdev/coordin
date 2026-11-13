using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Simulação dia a dia (DT-21): em cada dia útil, cada pessoa entrega as horas da sua capacidade à
/// primeira parte elegível da sua fila (na ordem de execução) e a sobra vai para a próxima parte.
/// A parte de um time termina quando o esforço restante chega a zero; a demanda termina quando
/// termina a última parte. Cada time trabalha só na sua parte, então adicionar um time nunca divide
/// o prazo automaticamente (regra inviolável 4).
/// </summary>
internal static class ForecastSimulator
{
    /// <summary>O resultado de uma rodada da simulação.</summary>
    internal sealed record Outcome(
        IReadOnlyDictionary<Guid, (DateOnly Start, DateOnly End)> Finished,
        IReadOnlySet<Guid> Unfinished,
        IReadOnlyList<ConsumedHours> Consumed);

    private sealed class PartState(Guid demandId, Guid teamId, DateOnly startDate, decimal remaining)
    {
        public Guid DemandId { get; } = demandId;

        public Guid TeamId { get; } = teamId;

        public DateOnly StartDate { get; } = startDate;

        public decimal Remaining { get; set; } = remaining;
    }

    private sealed class PersonQueue(Guid personId, List<PartState> parts)
    {
        public Guid PersonId { get; } = personId;

        public List<PartState> Parts { get; } = parts;

        public int Head { get; set; }
    }

    /// <param name="snapshot">O planejamento.</param>
    /// <param name="included">As demandas previsíveis a simular; todas as outras ficam de fora.</param>
    public static Outcome Run(
        PlanningSnapshot snapshot,
        IReadOnlySet<Guid> included,
        WorkCalendar calendar,
        PersonCapacityCalendar capacity)
    {
        var demands = snapshot.Demands.Where(demand => included.Contains(demand.Id)).ToList();
        if (demands.Count == 0)
        {
            return new Outcome(new Dictionary<Guid, (DateOnly, DateOnly)>(), new HashSet<Guid>(), []);
        }

        var parts = new Dictionary<(Guid DemandId, Guid TeamId), PartState>();
        var openParts = new Dictionary<Guid, int>();
        foreach (var demand in demands)
        {
            foreach (var part in demand.Parts)
            {
                parts[(demand.Id, part.TeamId)] = new PartState(demand.Id, part.TeamId, demand.StartDate!.Value, part.EstimatedEffortHours!.Value);
            }

            openParts[demand.Id] = demand.Parts.Count;
        }

        var queues = BuildQueues(snapshot, demands, included, parts);
        var firstWorked = new Dictionary<Guid, DateOnly>();
        var finished = new Dictionary<Guid, (DateOnly Start, DateOnly End)>();
        var consumed = new List<ConsumedHours>();

        var earliestStart = demands.Min(demand => demand.StartDate!.Value);
        var horizon = earliestStart.AddYears(5);
        var partsLeft = parts.Count;

        for (var day = calendar.OnOrAfter(earliestStart); day <= horizon && partsLeft > 0; day = day.AddDays(1))
        {
            if (!calendar.IsBusinessDay(day))
            {
                continue;
            }

            foreach (var queue in queues)
            {
                var hours = capacity.CapacityOf(queue.PersonId, day);
                SkipFinishedHead(queue);

                for (var index = queue.Head; index < queue.Parts.Count && hours > 0m; index++)
                {
                    var part = queue.Parts[index];
                    if (!IsEligible(part, day))
                    {
                        continue;
                    }

                    var taken = Math.Min(hours, part.Remaining);
                    hours -= taken;
                    part.Remaining -= taken;
                    consumed.Add(new ConsumedHours(queue.PersonId, part.DemandId, part.TeamId, day, taken));
                    firstWorked.TryAdd(part.DemandId, day);

                    if (part.Remaining == 0m)
                    {
                        partsLeft--;
                        if (--openParts[part.DemandId] == 0)
                        {
                            finished[part.DemandId] = (firstWorked[part.DemandId], day);
                        }
                    }
                }
            }
        }

        var unfinished = included.Where(id => !finished.ContainsKey(id)).ToHashSet();
        return new Outcome(finished, unfinished, consumed);
    }

    /// <summary>
    /// Regra de elegibilidade de uma parte em um dia: já começou e ainda tem esforço. É o único lugar
    /// onde a change de dependências acrescentará "os pré-requisitos já terminaram" (DT-23).
    /// </summary>
    private static bool IsEligible(PartState part, DateOnly day) =>
        part.Remaining > 0m && part.StartDate <= day;

    private static void SkipFinishedHead(PersonQueue queue)
    {
        while (queue.Head < queue.Parts.Count && queue.Parts[queue.Head].Remaining == 0m)
        {
            queue.Head++;
        }
    }

    /// <summary>Monta, uma vez, a fila de cada pessoa: as partes do time dela, na ordem de execução (DT-13).</summary>
    private static List<PersonQueue> BuildQueues(
        PlanningSnapshot snapshot,
        List<DemandInput> demands,
        IReadOnlySet<Guid> included,
        Dictionary<(Guid DemandId, Guid TeamId), PartState> parts)
    {
        var position = demands
            .OrderBy(demand => snapshot.OrderMode == ExecutionOrderMode.FollowPriority ? demand.PriorityRank : demand.ExecutionRank)
            .ThenBy(demand => demand.Id)
            .Select((demand, index) => (demand.Id, index))
            .ToDictionary(item => item.Id, item => item.index);

        var assignmentsByPerson = snapshot.Assignments
            .Where(assignment => included.Contains(assignment.DemandId))
            .ToLookup(assignment => assignment.PersonId);

        var queues = new List<PersonQueue>();
        foreach (var person in snapshot.People.OrderBy(person => person.Id))
        {
            var queue = assignmentsByPerson[person.Id]
                .OrderBy(assignment => position[assignment.DemandId])
                .Select(assignment => parts[(assignment.DemandId, person.TeamId)])
                .ToList();

            if (queue.Count > 0)
            {
                queues.Add(new PersonQueue(person.Id, queue));
            }
        }

        return queues;
    }
}
