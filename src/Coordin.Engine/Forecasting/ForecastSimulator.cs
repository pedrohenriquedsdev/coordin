using Coordin.Engine.Capacity;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Simulação dia a dia (DT-21): em cada dia útil, cada pessoa entrega as horas da sua capacidade à
/// primeira parte elegível da sua fila (na ordem de execução) e a sobra vai para a próxima parte.
/// A parte de um time termina quando o esforço restante chega a zero; a demanda termina quando
/// termina a última parte. Cada time trabalha só na sua parte, então adicionar um time nunca divide
/// o prazo automaticamente (regra inviolável 4). Uma parte só é elegível quando os pré-requisitos da
/// sua demanda já terminaram e o dia é posterior ao fim, com margem, de todos eles (RF-18; DT-23).
/// Trabalha com índices e arrays preparados em <see cref="SimulationTables"/>, percorrendo pessoas,
/// filas e partes sempre na mesma ordem para o resultado nunca depender da ordem das listas (RNF-02).
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

    private sealed class PersonQueue(int person, int[] parts)
    {
        public int Person { get; } = person;

        public int[] Parts { get; } = parts;

        public int Head { get; set; }
    }

    private const int Unset = -1;

    /// <param name="tables">As tabelas do planejamento.</param>
    /// <param name="included">Por índice de demanda: as previsíveis a simular; todas as outras ficam de fora.</param>
    /// <param name="position">Por índice de demanda: a posição na ordem de execução (menor vem primeiro na fila).</param>
    /// <param name="marginPercent">O percentual da margem de segurança (DT-22).</param>
    /// <param name="collectConsumption">Se falso, o consumo diário nem é montado (a avaliação de cenários não o usa).</param>
    public static Outcome Run(
        SimulationTables tables,
        bool[] included,
        int[] position,
        decimal marginPercent,
        WorkCalendar calendar,
        bool collectConsumption)
    {
        var earliestStart = int.MaxValue;
        var partsLeft = 0;
        var openParts = new int[tables.DemandCount];
        var remaining = new decimal[tables.PartEffort.Length];
        for (var demand = 0; demand < tables.DemandCount; demand++)
        {
            if (!included[demand])
            {
                continue;
            }

            earliestStart = Math.Min(earliestStart, tables.StartDay[demand]);
            partsLeft += tables.PartCount[demand];
            openParts[demand] = tables.PartCount[demand];
            Array.Copy(tables.PartEffort, tables.FirstPart[demand], remaining, tables.FirstPart[demand], tables.PartCount[demand]);
        }

        if (earliestStart == int.MaxValue) // nenhuma demanda para simular
        {
            return new Outcome(new Dictionary<Guid, FinishedDemand>(), new HashSet<Guid>(), []);
        }

        var queues = BuildQueues(tables, included, position);
        var firstWorked = new int[tables.DemandCount];
        Array.Fill(firstWorked, Unset);
        var endWithMargin = new int[tables.DemandCount];
        Array.Fill(endWithMargin, Unset);
        var finished = new Dictionary<Guid, FinishedDemand>();
        var consumed = new List<ConsumedHours>();

        var startDate = DateOnly.FromDayNumber(earliestStart);
        var horizon = startDate.AddYears(5).DayNumber;

        for (var day = calendar.OnOrAfter(startDate).DayNumber; day <= horizon && partsLeft > 0; day++)
        {
            if (!calendar.IsBusinessDay(DateOnly.FromDayNumber(day)))
            {
                continue;
            }

            foreach (var queue in queues)
            {
                var hours = tables.CapacityOf(queue.Person, day);
                SkipFinishedHead(queue, remaining);

                for (var index = queue.Head; index < queue.Parts.Length && hours > 0m; index++)
                {
                    var part = queue.Parts[index];
                    var demand = tables.PartDemand[part];
                    if (!IsEligible(tables, included, remaining, endWithMargin, part, demand, day))
                    {
                        continue;
                    }

                    var taken = Math.Min(hours, remaining[part]);
                    hours -= taken;
                    remaining[part] -= taken;
                    if (collectConsumption)
                    {
                        consumed.Add(new ConsumedHours(tables.People[queue.Person].Id, tables.DemandIds[demand], tables.PartTeamId[part], DateOnly.FromDayNumber(day), taken));
                    }

                    if (firstWorked[demand] == Unset)
                    {
                        firstWorked[demand] = day;
                    }

                    if (remaining[part] == 0m)
                    {
                        partsLeft--;
                        if (--openParts[demand] == 0)
                        {
                            var start = DateOnly.FromDayNumber(firstWorked[demand]);
                            var end = DateOnly.FromDayNumber(day);
                            var (marginDays, withMargin) = SafetyMargin.Apply(start, end, marginPercent, calendar);
                            endWithMargin[demand] = withMargin.DayNumber;
                            finished[tables.DemandIds[demand]] = new FinishedDemand(start, end, marginDays, withMargin);
                        }
                    }
                }
            }
        }

        var unfinished = new HashSet<Guid>();
        for (var demand = 0; demand < tables.DemandCount; demand++)
        {
            if (included[demand] && endWithMargin[demand] == Unset)
            {
                unfinished.Add(tables.DemandIds[demand]);
            }
        }

        return new Outcome(finished, unfinished, consumed);
    }

    /// <summary>
    /// Regra de elegibilidade de uma parte em um dia: já começou, ainda tem esforço e os pré-requisitos
    /// da demanda já terminaram, com o dia posterior ao fim <b>com margem</b> de cada um (DT-23). O fim
    /// com margem nunca é anterior ao dia em que a demanda terminou, então um dependente nunca fica
    /// elegível no mesmo dia do pré-requisito, qualquer que seja a ordem em que as pessoas são
    /// percorridas: o resultado não depende dessa ordem (RNF-02). O dia seguinte é o próximo dia útil
    /// porque a simulação só visita dias úteis. Um pré-requisito fora da simulação (concluído) está cumprido.
    /// </summary>
    private static bool IsEligible(SimulationTables tables, bool[] included, decimal[] remaining, int[] endWithMargin, int part, int demand, int day)
    {
        if (remaining[part] <= 0m || tables.StartDay[demand] > day)
        {
            return false;
        }

        foreach (var prerequisite in tables.Prerequisites[demand])
        {
            if (included[prerequisite] && (endWithMargin[prerequisite] == Unset || day <= endWithMargin[prerequisite]))
            {
                return false;
            }
        }

        return true;
    }

    private static void SkipFinishedHead(PersonQueue queue, decimal[] remaining)
    {
        while (queue.Head < queue.Parts.Length && remaining[queue.Parts[queue.Head]] == 0m)
        {
            queue.Head++;
        }
    }

    /// <summary>Monta, uma vez por rodada, a fila de cada pessoa: as partes do time dela, na ordem de execução (DT-13).</summary>
    private static List<PersonQueue> BuildQueues(SimulationTables tables, bool[] included, int[] position)
    {
        var queues = new List<PersonQueue>();
        for (var person = 0; person < tables.People.Length; person++)
        {
            var demands = tables.AssignedDemands[person];
            var parts = tables.AssignedParts[person];
            var count = 0;
            foreach (var demand in demands)
            {
                count += included[demand] ? 1 : 0;
            }

            if (count == 0)
            {
                continue;
            }

            var keys = new int[count];
            var queued = new int[count];
            var next = 0;
            for (var index = 0; index < demands.Length; index++)
            {
                if (included[demands[index]])
                {
                    keys[next] = position[demands[index]];
                    queued[next] = parts[index];
                    next++;
                }
            }

            Array.Sort(keys, queued); // as posições são únicas, então a ordem é a mesma de sempre
            queues.Add(new PersonQueue(person, queued));
        }

        return queues;
    }
}
