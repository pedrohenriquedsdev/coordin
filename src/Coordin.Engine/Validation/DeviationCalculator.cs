using Coordin.Engine.Forecasting;

namespace Coordin.Engine.Validation;

/// <summary>
/// O desvio de uma demanda, em dias corridos, positivo quando há atraso (DT-27). <see cref="BaselineDeviationDays"/>
/// mede a linha de base do gestor contra a data real simulada; <see cref="CoordinDeviationDays"/> mede a previsão
/// do planejamento com o Coordin contra a mesma data real (PRD 7.3, passo 7).
/// </summary>
public sealed record DemandDeviation(
    Guid DemandId,
    DateOnly BaselineDate,
    DateOnly CoordinForecastEndDate,
    DateOnly SimulatedActualEndDate,
    int BaselineDeviationDays,
    int CoordinDeviationDays);

/// <summary>
/// Os desvios por demanda e as médias. As médias com sinal dizem se há viés; as médias absolutas dizem o tamanho do
/// erro, porque +10 e −10 se anulam com sinal (PRD 7.1). Ver design, D5.
/// </summary>
public sealed record DeviationMetrics(
    IReadOnlyList<DemandDeviation> Items,
    decimal AverageBaselineDeviationDays,
    decimal AverageCoordinDeviationDays,
    decimal AverageBaselineAbsoluteDeviationDays,
    decimal AverageCoordinAbsoluteDeviationDays);

/// <summary>
/// Calcula os desvios da validação (PRD 7.1 e 7.3; DT-27). Considera as demandas previsíveis no processo atual, que
/// são as que têm data real simulada, e só as que também têm previsão no planejamento com o Coordin.
/// </summary>
public static class DeviationCalculator
{
    public static DeviationMetrics Calculate(
        ForecastResult coordinForecast,
        IReadOnlyDictionary<Guid, DateOnly> actuals,
        IReadOnlyDictionary<Guid, DateOnly> baseline)
    {
        var coordinEnds = coordinForecast.Demands
            .Where(demand => demand.Status == DemandStatus.Predictable && demand.ForecastEndDate is not null)
            .ToDictionary(demand => demand.DemandId, demand => demand.ForecastEndDate!.Value);

        var items = new List<DemandDeviation>();
        foreach (var demandId in actuals.Keys.OrderBy(id => id))
        {
            if (!coordinEnds.TryGetValue(demandId, out var coordinEnd))
            {
                continue;
            }

            if (!baseline.TryGetValue(demandId, out var baselineDate))
            {
                throw new ArgumentException("Há demanda previsível sem linha de base.");
            }

            var actual = actuals[demandId];
            items.Add(new DemandDeviation(
                demandId,
                baselineDate,
                coordinEnd,
                actual,
                actual.DayNumber - baselineDate.DayNumber,
                actual.DayNumber - coordinEnd.DayNumber));
        }

        var baselineDeviations = items.Select(item => item.BaselineDeviationDays).ToList();
        var coordinDeviations = items.Select(item => item.CoordinDeviationDays).ToList();

        return new DeviationMetrics(
            items,
            Average(baselineDeviations),
            Average(coordinDeviations),
            Average(baselineDeviations.Select(Math.Abs)),
            Average(coordinDeviations.Select(Math.Abs)));
    }

    /// <summary>A média com sinal de uma sequência de dias; zero quando não há valor, sem divisão por zero.</summary>
    private static decimal Average(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0m : (decimal)list.Sum(value => (long)value) / list.Count;
    }
}
