using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Reporting;

namespace Coordin.Engine.Validation;

/// <summary>
/// Os dados da avaliação: o planejamento do processo atual (guardado), o planejamento com o Coordin (vigente), a
/// linha de base do gestor e os parâmetros do realizado simulado (PRD 7.3; DT-37).
/// </summary>
public sealed record ValidationInput(
    PlanningSnapshot CurrentProcess,
    PlanningSnapshot WithCoordin,
    IReadOnlyDictionary<Guid, DateOnly> Baseline,
    ValidationParameters Parameters);

/// <summary>A eficiência dos dois planejamentos, no mesmo período (PRD 7.2).</summary>
public sealed record ValidationEfficiency(EfficiencyMetrics CurrentProcess, EfficiencyMetrics WithCoordin);

/// <summary>O resultado completo da validação, no período comparado.</summary>
public sealed record ValidationResult(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ValidationEfficiency Efficiency,
    DeviationMetrics Deviation,
    PriorityCheck PriorityCheck);

/// <summary>
/// A avaliação da validação por simulação (PRD 7.3; DT-37; seção 6.6). Função pura e determinística: o mesmo
/// <see cref="ValidationInput"/> produz sempre o mesmo resultado, independentemente da ordem das listas (RNF-02).
/// </summary>
public static class ValidationEvaluator
{
    public static ValidationResult Evaluate(ValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var currentForecast = ForecastEngine.Calculate(input.CurrentProcess);
        var coordinForecast = ForecastEngine.Calculate(input.WithCoordin);
        var actuals = SimulatedActuals.Calculate(input.CurrentProcess, input.Parameters);

        var (periodStart, periodEnd) = Period(input, currentForecast, coordinForecast);

        var efficiency = new ValidationEfficiency(
            EfficiencyCalculator.Calculate(input.CurrentProcess, currentForecast, periodStart, periodEnd),
            EfficiencyCalculator.Calculate(input.WithCoordin, coordinForecast, periodStart, periodEnd));

        var deviation = DeviationCalculator.Calculate(coordinForecast, actuals, input.Baseline);
        var priorityCheck = PriorityChecker.Calculate(input.CurrentProcess, currentForecast, coordinForecast);

        return new ValidationResult(periodStart, periodEnd, efficiency, deviation, priorityCheck);
    }

    /// <summary>
    /// O período comparado (DT-24): da menor data de início das demandas não concluídas à maior previsão dos dois
    /// planejamentos, em semanas completas.
    /// </summary>
    private static (DateOnly Start, DateOnly End) Period(ValidationInput input, ForecastResult currentForecast, ForecastResult coordinForecast)
    {
        var starts = input.CurrentProcess.Demands.Concat(input.WithCoordin.Demands)
            .Where(demand => !demand.IsCompleted && demand.StartDate is not null)
            .Select(demand => demand.StartDate!.Value)
            .ToList();

        var ends = currentForecast.Demands.Concat(coordinForecast.Demands)
            .Where(demand => demand.Status == DemandStatus.Predictable && demand.ForecastEndDate is not null)
            .Select(demand => demand.ForecastEndDate!.Value)
            .ToList();

        if (starts.Count == 0 || ends.Count == 0)
        {
            throw new ArgumentException("Não há demanda previsível para comparar.");
        }

        return TeamReport.AlignedPeriod(starts.Min(), ends.Max());
    }
}
