using Coordin.Engine.Capacity;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Reporting;

/// <summary>
/// Uma semana (segunda a domingo) de um time: capacidade, horas alocadas, ocupação, ociosidade e sobrecarga
/// (RF-17; DT-24). <see cref="Occupancy"/> é nula quando a capacidade é zero.
/// </summary>
public sealed record TeamReportWeek(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal CapacityHours,
    decimal AllocatedHours,
    decimal? Occupancy,
    decimal IdleHours,
    bool IsOverloaded,
    IReadOnlyList<Guid> OverloadedDemandIds);

/// <summary>As semanas de um time, em ordem cronológica, e as demandas não previsíveis que o envolvem.</summary>
public sealed record TeamReportTeam(
    Guid TeamId,
    int UnpredictableDemandCount,
    IReadOnlyList<TeamReportWeek> Weeks);

/// <summary>O relatório de um período: as semanas completas que o cobrem e cada time.</summary>
public sealed record TeamReportResult(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<TeamReportTeam> Teams);

/// <summary>
/// Relatório por time (RF-17; DT-24). É derivado do consumo diário que a simulação já calcula, sem um
/// segundo modelo: a ociosidade e a sobrecarga saem do mesmo cálculo que gera a previsão. Função pura:
/// sem banco, relógio ou aleatoriedade (DT-02).
/// </summary>
public static class TeamReport
{
    /// <summary>
    /// Ajusta o período pedido para semanas completas: o início vira a segunda-feira da sua semana e o fim,
    /// o domingo da sua semana.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">O fim é anterior ao início.</exception>
    public static (DateOnly Start, DateOnly End) AlignedPeriod(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "O fim do período é anterior ao início.");
        }

        return (MondayOf(from), MondayOf(to).AddDays(6));
    }

    /// <summary>Monta o relatório dos times no período pedido, a partir do planejamento e da sua previsão.</summary>
    public static TeamReportResult Build(PlanningSnapshot snapshot, ForecastResult result, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(result);

        var (start, end) = AlignedPeriod(from, to);
        var calendar = new WorkCalendar(snapshot.Holidays.Select(holiday => holiday.Date));
        var capacity = new PersonCapacityCalendar(calendar, snapshot.People, snapshot.Absences);
        var statusOf = result.Demands.ToDictionary(forecast => forecast.DemandId, forecast => forecast.Status);

        var teams = snapshot.Teams
            .OrderBy(team => team.Id)
            .Select(team => BuildTeam(team.Id, snapshot, result, capacity, statusOf, start, end))
            .ToList();

        return new TeamReportResult(start, end, teams);
    }

    private static TeamReportTeam BuildTeam(
        Guid teamId,
        PlanningSnapshot snapshot,
        ForecastResult result,
        PersonCapacityCalendar capacity,
        Dictionary<Guid, DemandStatus> statusOf,
        DateOnly start,
        DateOnly end)
    {
        var weeks = new List<TeamReportWeek>();
        for (var weekStart = start; weekStart <= end; weekStart = weekStart.AddDays(7))
        {
            weeks.Add(BuildWeek(teamId, snapshot, result, capacity, statusOf, weekStart));
        }

        // Demanda não previsível que envolve o time: não entra nas horas alocadas, mas o gestor precisa saber.
        var unpredictable = snapshot.Demands.Count(demand =>
            demand.Parts.Any(part => part.TeamId == teamId)
            && statusOf.TryGetValue(demand.Id, out var status)
            && status == DemandStatus.Unpredictable);

        return new TeamReportTeam(teamId, unpredictable, weeks);
    }

    private static TeamReportWeek BuildWeek(
        Guid teamId,
        PlanningSnapshot snapshot,
        ForecastResult result,
        PersonCapacityCalendar capacity,
        Dictionary<Guid, DemandStatus> statusOf,
        DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(6);

        var capacityHours = 0m;
        for (var day = weekStart; day <= weekEnd; day = day.AddDays(1))
        {
            capacityHours += capacity.TeamCapacityOn(teamId, day);
        }

        var allocatedHours = result.ConsumedHours
            .Where(entry => entry.TeamId == teamId && entry.Date >= weekStart && entry.Date <= weekEnd)
            .Sum(entry => entry.Hours);

        var occupancy = capacityHours > 0m ? allocatedHours / capacityHours : (decimal?)null;
        var idle = Math.Max(capacityHours - allocatedHours, 0m);

        var overloaded = IsOverloaded(teamId, snapshot, result, statusOf, capacityHours, allocatedHours, weekEnd);
        var overloadedIds = overloaded ? WaitingDemands(teamId, snapshot, result, statusOf, weekEnd) : [];

        return new TeamReportWeek(weekStart, weekEnd, capacityHours, allocatedHours, occupancy, idle, overloaded, overloadedIds);
    }

    /// <summary>
    /// Sobrecarga (DT-24, definição do autor de 2026-11-16): toda a capacidade foi usada na semana e, ao fim
    /// dela, ainda há parte elegível do time com esforço por fazer.
    /// </summary>
    private static bool IsOverloaded(
        Guid teamId,
        PlanningSnapshot snapshot,
        ForecastResult result,
        Dictionary<Guid, DemandStatus> statusOf,
        decimal capacityHours,
        decimal allocatedHours,
        DateOnly weekEnd)
    {
        if (capacityHours <= 0m || allocatedHours != capacityHours)
        {
            return false;
        }

        return WaitingDemands(teamId, snapshot, result, statusOf, weekEnd).Count > 0;
    }

    /// <summary>
    /// As demandas elegíveis do time com esforço por fazer ao fim da semana: previsíveis, já iniciadas e
    /// com a parte do time ainda não consumida. Na ordem da fila (modo de ordem, depois o Id).
    /// </summary>
    private static List<Guid> WaitingDemands(
        Guid teamId,
        PlanningSnapshot snapshot,
        ForecastResult result,
        Dictionary<Guid, DemandStatus> statusOf,
        DateOnly weekEnd)
    {
        var waiting = new List<Guid>();
        foreach (var demand in snapshot.Demands)
        {
            var part = demand.Parts.FirstOrDefault(item => item.TeamId == teamId);
            if (part?.EstimatedEffortHours is not { } estimate
                || demand.StartDate is not { } start
                || start > weekEnd
                || !statusOf.TryGetValue(demand.Id, out var status)
                || status != DemandStatus.Predictable)
            {
                continue;
            }

            var consumedSoFar = result.ConsumedHours
                .Where(entry => entry.DemandId == demand.Id && entry.TeamId == teamId && entry.Date <= weekEnd)
                .Sum(entry => entry.Hours);
            if (estimate - consumedSoFar > 0m)
            {
                waiting.Add(demand.Id);
            }
        }

        var rankOf = snapshot.OrderMode == ExecutionOrderMode.FollowPriority
            ? snapshot.Demands.ToDictionary(demand => demand.Id, demand => demand.PriorityRank)
            : snapshot.Demands.ToDictionary(demand => demand.Id, demand => demand.ExecutionRank);
        return waiting.OrderBy(id => rankOf[id]).ThenBy(id => id).ToList();
    }

    /// <summary>A segunda-feira da semana de uma data (a semana vai de segunda a domingo).</summary>
    private static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
