using Coordin.Engine.Forecasting;
using Coordin.Engine.Tests.Planning;
using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>Os desvios da validação (PRD 7.1 e 7.3; DT-27), com previsões escritas à mão.</summary>
public sealed class DeviationCalculatorTests
{
    private static Guid IdAt(int index) => new(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    private static DemandForecast Predictable(Guid id, DateOnly end) =>
        new(id, DemandStatus.Predictable, null, Day.Monday, end, 0, end);

    private static ForecastResult Forecast(params DemandForecast[] demands) => new(demands, []);

    [Fact]
    public void Deviations_are_signed_in_days_and_their_averages_show_the_bias()
    {
        var first = IdAt(1);
        var second = IdAt(2);
        var actuals = new Dictionary<Guid, DateOnly> { [first] = Day.Plus(20), [second] = Day.Plus(20) };
        var baseline = new Dictionary<Guid, DateOnly> { [first] = Day.Plus(10), [second] = Day.Plus(30) };
        var coordin = Forecast(Predictable(first, Day.Plus(20)), Predictable(second, Day.Plus(20)));

        var metrics = DeviationCalculator.Calculate(coordin, actuals, baseline);

        // Primeira: 10 dias de atraso frente à linha de base; segunda: 10 dias antes. A média com sinal é zero,
        // mas a absoluta mostra o erro de 10 dias em cada uma.
        Assert.Equal(0m, metrics.AverageBaselineDeviationDays);
        Assert.Equal(10m, metrics.AverageBaselineAbsoluteDeviationDays);
        Assert.Equal(10, metrics.Items.Single(item => item.DemandId == first).BaselineDeviationDays);
        Assert.Equal(-10, metrics.Items.Single(item => item.DemandId == second).BaselineDeviationDays);
    }

    [Fact]
    public void Coordin_deviation_uses_the_planning_with_the_coordin()
    {
        var id = IdAt(3);
        var actuals = new Dictionary<Guid, DateOnly> { [id] = Day.Plus(20) };
        var baseline = new Dictionary<Guid, DateOnly> { [id] = Day.Plus(20) };
        // A previsão com o Coordin foi 3 dias antes da data real.
        var coordin = Forecast(Predictable(id, Day.Plus(17)));

        var metrics = DeviationCalculator.Calculate(coordin, actuals, baseline);

        var item = metrics.Items.Single();
        Assert.Equal(3, item.CoordinDeviationDays);
        Assert.Equal(3m, metrics.AverageCoordinDeviationDays);
        Assert.Equal(0, item.BaselineDeviationDays);
        Assert.Equal(Day.Plus(20), item.BaselineDate);
        Assert.Equal(Day.Plus(17), item.CoordinForecastEndDate);
        Assert.Equal(Day.Plus(20), item.SimulatedActualEndDate);
    }

    [Fact]
    public void Demand_without_a_real_date_is_left_out()
    {
        var predictable = IdAt(4);
        var unpredictable = IdAt(5);
        var actuals = new Dictionary<Guid, DateOnly> { [predictable] = Day.Plus(5) };
        var baseline = new Dictionary<Guid, DateOnly> { [predictable] = Day.Plus(5), [unpredictable] = Day.Plus(5) };
        var coordin = Forecast(Predictable(predictable, Day.Plus(5)), Predictable(unpredictable, Day.Plus(5)));

        var metrics = DeviationCalculator.Calculate(coordin, actuals, baseline);

        Assert.Single(metrics.Items);
        Assert.Equal(predictable, metrics.Items.Single().DemandId);
    }

    [Fact]
    public void Missing_baseline_for_a_predictable_demand_is_refused()
    {
        var id = IdAt(6);
        var actuals = new Dictionary<Guid, DateOnly> { [id] = Day.Plus(5) };
        var coordin = Forecast(Predictable(id, Day.Plus(5)));

        Assert.Throws<ArgumentException>(() =>
            DeviationCalculator.Calculate(coordin, actuals, new Dictionary<Guid, DateOnly>()));
    }

    [Fact]
    public void No_predictable_demand_gives_zero_averages_without_error()
    {
        var metrics = DeviationCalculator.Calculate(
            Forecast(),
            new Dictionary<Guid, DateOnly>(),
            new Dictionary<Guid, DateOnly>());

        Assert.Empty(metrics.Items);
        Assert.Equal(0m, metrics.AverageCoordinAbsoluteDeviationDays);
    }

    [Fact]
    public void A_demand_unpredictable_with_the_coordin_is_left_out()
    {
        var id = IdAt(7);
        var actuals = new Dictionary<Guid, DateOnly> { [id] = Day.Plus(5) };
        var baseline = new Dictionary<Guid, DateOnly> { [id] = Day.Plus(5) };
        var unpredictable = new DemandForecast(id, DemandStatus.Unpredictable, UnpredictableReason.NoCapacity, null, null, null, null);

        var metrics = DeviationCalculator.Calculate(Forecast(unpredictable), actuals, baseline);

        Assert.Empty(metrics.Items);
    }
}
