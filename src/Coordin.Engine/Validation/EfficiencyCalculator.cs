using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Reporting;

namespace Coordin.Engine.Validation;

/// <summary>As métricas de eficiência de um planejamento no período comparado (PRD 7.2; DT-37).</summary>
/// <param name="IdleHours">Horas de capacidade sem trabalho, somadas de todos os times e semanas.</param>
/// <param name="OccupancySpreadPoints">Maior menos menor ocupação média dos times, em pontos percentuais.</param>
/// <param name="OverloadedPeriods">Pares time × semana com sobrecarga (RF-17).</param>
/// <param name="SectorEndDate">Data de conclusão prevista da última demanda previsível.</param>
public sealed record EfficiencyMetrics(
    decimal IdleHours,
    decimal OccupancySpreadPoints,
    int OverloadedPeriods,
    DateOnly? SectorEndDate);

/// <summary>
/// Calcula as métricas de eficiência de um planejamento a partir do relatório por time (DT-24). A ocupação média
/// de um time é a média ponderada: horas alocadas ÷ capacidade do período, e não a média das semanas, para que
/// semanas de pouca capacidade não pesem como as grandes (DT-37; ver design, D4).
/// </summary>
public static class EfficiencyCalculator
{
    public static EfficiencyMetrics Calculate(PlanningSnapshot snapshot, ForecastResult result, DateOnly from, DateOnly to)
    {
        var report = TeamReport.Build(snapshot, result, from, to);

        var idleHours = report.Teams.Sum(team => team.Weeks.Sum(week => week.IdleHours));
        var overloadedPeriods = report.Teams.Sum(team => team.Weeks.Count(week => week.IsOverloaded));

        // Times sem capacidade no período ficam de fora da dispersão, para não dividir por zero.
        var occupancies = report.Teams
            .Select(team => (Capacity: team.Weeks.Sum(week => week.CapacityHours), Allocated: team.Weeks.Sum(week => week.AllocatedHours)))
            .Where(totals => totals.Capacity > 0m)
            .Select(totals => totals.Allocated / totals.Capacity)
            .ToList();

        var spread = occupancies.Count == 0 ? 0m : (occupancies.Max() - occupancies.Min()) * 100m;

        var sectorEndDate = result.Demands
            .Where(demand => demand.Status == DemandStatus.Predictable && demand.ForecastEndDate is not null)
            .Select(demand => demand.ForecastEndDate!.Value)
            .Cast<DateOnly?>()
            .Max();

        return new EfficiencyMetrics(idleHours, spread, overloadedPeriods, sectorEndDate);
    }
}
