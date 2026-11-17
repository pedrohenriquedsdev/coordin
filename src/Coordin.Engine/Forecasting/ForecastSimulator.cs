using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Simulação dia a dia (DT-21): em cada dia útil, cada pessoa entrega as horas da sua capacidade à
/// primeira parte elegível da sua fila (na ordem de execução) e a sobra vai para a próxima parte.
/// A parte de um time termina quando o esforço restante chega a zero; a demanda termina quando
/// termina a última parte. Cada time trabalha só na sua parte, então adicionar um time nunca divide
/// o prazo automaticamente (regra inviolável 4). Uma parte só é elegível quando os pré-requisitos da
/// sua demanda já terminaram e o dia é posterior ao fim, com margem, de todos eles (RF-18; DT-23).
/// </summary>
internal static class ForecastSimulator
{
    /// <summary>
    /// Como uma demanda terminou. A margem é calculada aqui, no dia em que ela termina, porque os
    /// dependentes esperam o fim <b>com</b> margem (DT-23), e o resultado final reaproveita estes valores.
    /// </summary>
    internal sealed record FinishedDemand(DateOnly Start, DateOnly End, int MarginBusinessDays, DateOnly EndWithMargin);

    /// <summary>O resultado de uma rodada da simulação.</summary>
    internal sealed record Outcome(
        IReadOnlyDictionary<Guid, FinishedDemand> Finished,
        IReadOnlySet<Guid> Unfinished,
        IReadOnlyList<ConsumedHours> Consumed);

    private sealed class PartState(Guid demandId, Guid teamId, DateOnly startDate, decimal remaining, IReadOnlyList<Guid> prerequisites)
    {
        public Guid DemandId { get; } = demandId;

        public Guid TeamId { get; } = teamId;

        public DateOnly StartDate { get; } = startDate;

        public decimal Remaining { get; set; } = remaining;

        /// <summary>Os pré-requisitos da demanda que ainda precisam terminar (os concluídos não entram).</summary>
        public IReadOnlyList<Guid> Prerequisites { get; } = prerequisites;
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
            return new Outcome(new Dictionary<Guid, FinishedDemand>(), new HashSet<Guid>(), []);
        }

        // Só espera quem está na simulação: pré-requisito concluído está cumprido, e a classificação já
        // tirou do jogo os dependentes de quem não tem previsão.
        var prerequisitesByDemand = snapshot.Dependencies
            .Where(dependency => included.Contains(dependency.PrerequisiteId))
            .ToLookup(dependency => dependency.DemandId, dependency => dependency.PrerequisiteId);

        var parts = new Dictionary<(Guid DemandId, Guid TeamId), PartState>();
        var openParts = new Dictionary<Guid, int>();
        foreach (var demand in demands)
        {
            var prerequisites = prerequisitesByDemand[demand.Id].Order().ToList();
            foreach (var part in demand.Parts)
            {
                parts[(demand.Id, part.TeamId)] = new PartState(demand.Id, part.TeamId, demand.StartDate!.Value, part.EstimatedEffortHours!.Value, prerequisites);
            }

            openParts[demand.Id] = demand.Parts.Count;
        }

        var queues = BuildQueues(snapshot, demands, included, parts);
        var firstWorked = new Dictionary<Guid, DateOnly>();
        var finished = new Dictionary<Guid, FinishedDemand>();
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
                    if (!IsEligible(part, day, finished))
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
                            var start = firstWorked[part.DemandId];
                            var (marginDays, endWithMargin) = SafetyMargin.Apply(start, day, snapshot.SafetyMarginPercent, calendar);
                            finished[part.DemandId] = new FinishedDemand(start, day, marginDays, endWithMargin);
                        }
                    }
                }
            }
        }

        var unfinished = included.Where(id => !finished.ContainsKey(id)).ToHashSet();
        return new Outcome(finished, unfinished, consumed);
    }

    /// <summary>
    /// Regra de elegibilidade de uma parte em um dia: já começou, ainda tem esforço e os pré-requisitos
    /// da demanda já terminaram, com o dia posterior ao fim <b>com margem</b> de cada um (DT-23). O fim
    /// com margem nunca é anterior ao dia em que a demanda terminou, então um dependente nunca fica
    /// elegível no mesmo dia do pré-requisito, qualquer que seja a ordem em que as pessoas são
    /// percorridas: o resultado não depende dessa ordem (RNF-02). O dia seguinte é o próximo dia útil
    /// porque a simulação só visita dias úteis.
    /// </summary>
    private static bool IsEligible(PartState part, DateOnly day, Dictionary<Guid, FinishedDemand> finished) =>
        part.Remaining > 0m
        && part.StartDate <= day
        && part.Prerequisites.All(id => finished.TryGetValue(id, out var prerequisite) && day > prerequisite.EndWithMargin);

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
