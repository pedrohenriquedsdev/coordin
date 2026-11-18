using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Validation;

/// <summary>
/// O realizado simulado da validação (PRD 7.3, passo 6; DT-37). O esforço de cada parte é multiplicado pelo fator da
/// demanda e o planejamento do processo atual é simulado de novo com <b>margem zero</b>: a data de fim sem margem é a
/// data real simulada. A margem zero só serve para medir o real; a previsão do processo atual não muda (regra 1).
/// </summary>
public static class SimulatedActuals
{
    /// <summary>O esforço escalado tem no mínimo um centésimo de hora, para uma parte estimada nunca virar zero.</summary>
    private const decimal MinimumScaledHours = 0.01m;

    /// <summary>A data real simulada de cada demanda previsível do processo atual.</summary>
    public static IReadOnlyDictionary<Guid, DateOnly> Calculate(PlanningSnapshot currentProcess, ValidationParameters parameters)
    {
        var scaledDemands = currentProcess.Demands
            .Select(demand => demand with { Parts = ScaleParts(demand, parameters) })
            .ToList();

        var scaled = currentProcess with { Demands = scaledDemands, SafetyMarginPercent = 0m };
        var result = ForecastEngine.Calculate(scaled);

        return result.Demands
            .Where(forecast => forecast.Status == DemandStatus.Predictable)
            .ToDictionary(forecast => forecast.DemandId, forecast => forecast.EndDateWithoutMargin!.Value);
    }

    private static List<DemandPartInput> ScaleParts(DemandInput demand, ValidationParameters parameters)
    {
        var factor = EffortFactors.FactorFor(demand.Id, parameters);
        return demand.Parts
            .Select(part => part with
            {
                EstimatedEffortHours = part.EstimatedEffortHours is { } hours
                    ? Math.Max(MinimumScaledHours, Math.Round(hours * factor, 2, MidpointRounding.AwayFromZero))
                    : null,
            })
            .ToList();
    }
}
