using Coordin.Engine.Accuracy;

namespace Coordin.Engine.Tests.Accuracy;

/// <summary>Os indicadores de precisão (RF-23; DT-27): cada regra das specs, com números conferidos à mão.</summary>
public sealed class AccuracyCalculatorTests
{
    private static readonly DateOnly From = new(2027, 1, 1);
    private static readonly DateOnly To = new(2027, 1, 31);

    private static Guid IdAt(int index) => new(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    /// <summary>Uma demanda concluída completa: linha de base, previsão de conclusão, estimado e real.</summary>
    private static CompletedDelivery Delivery(
        int index,
        DateOnly completedOn,
        DateOnly? baseline = null,
        DateOnly? forecast = null,
        decimal? estimated = 40m,
        decimal? actual = 40m,
        params Guid[] teams) =>
        new(
            IdAt(index),
            completedOn,
            baseline ?? new DateOnly(2027, 1, 15),
            forecast ?? new DateOnly(2027, 1, 15),
            estimated,
            actual,
            teams.Length == 0 ? [IdAt(100)] : teams);

    [Fact]
    public void The_average_deviation_from_the_baseline_is_signed_in_calendar_days()
    {
        // +4 e −2 dias em relação à linha de base: média +1.
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 19), baseline: new DateOnly(2027, 1, 15)),
             Delivery(2, new DateOnly(2027, 1, 13), baseline: new DateOnly(2027, 1, 15))],
            From, To);

        Assert.Equal(1m, result.Sector.AverageDeviationFromBaselineDays);
    }

    [Fact]
    public void A_demand_completed_before_its_forecast_gives_a_negative_deviation()
    {
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), forecast: new DateOnly(2027, 1, 15))],
            From, To);

        Assert.Equal(-5m, result.Sector.AverageDeviationFromCompletionForecastDays);
    }

    [Fact]
    public void On_time_percentage_uses_the_baseline_and_is_66_point_7_for_two_of_three()
    {
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), baseline: new DateOnly(2027, 1, 15)),
             Delivery(2, new DateOnly(2027, 1, 12), baseline: new DateOnly(2027, 1, 15)),
             Delivery(3, new DateOnly(2027, 1, 20), baseline: new DateOnly(2027, 1, 15))],
            From, To);

        Assert.Equal(66.7m, Math.Round(result.Sector.OnTimePercentage!.Value, 1));
    }

    [Fact]
    public void Estimate_difference_is_the_mean_of_each_demand_percentage_so_each_demand_weighs_the_same()
    {
        // Uma demanda estimada em 40 e feita em 50 (+25%); outra estimada em 100 e feita em 100 (0%).
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), estimated: 40m, actual: 50m),
             Delivery(2, new DateOnly(2027, 1, 10), estimated: 100m, actual: 100m)],
            From, To);

        Assert.Equal(12.5m, result.Sector.AverageEstimateDifferencePercent);
    }

    [Fact]
    public void A_demand_without_actual_effort_is_left_out_of_the_estimate_difference_only_and_counted()
    {
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), actual: null),
             Delivery(2, new DateOnly(2027, 1, 10), estimated: 40m, actual: 40m)],
            From, To);

        Assert.Equal(0m, result.Sector.AverageEstimateDifferencePercent);
        Assert.Equal(1, result.Sector.ExcludedWithoutActualEffort);
        Assert.Equal(2, result.Sector.UsedCount);
    }

    [Fact]
    public void A_demand_without_baseline_is_left_out_of_baseline_indicators_and_counted()
    {
        var withoutBaseline = new CompletedDelivery(
            IdAt(1), new DateOnly(2027, 1, 10), null, new DateOnly(2027, 1, 15), 40m, 40m, [IdAt(100)]);

        var result = AccuracyCalculator.Calculate(
            [withoutBaseline, Delivery(2, new DateOnly(2027, 1, 10), baseline: new DateOnly(2027, 1, 15))],
            From, To);

        Assert.Equal(1, result.Sector.ExcludedWithoutBaseline);
        Assert.Equal(100m, result.Sector.OnTimePercentage);
    }

    [Fact]
    public void A_demand_completed_before_this_change_has_no_snapshot_and_is_counted_apart()
    {
        var old = new CompletedDelivery(IdAt(9), new DateOnly(2027, 1, 5), null, null, null, null, [IdAt(100)]);

        var result = AccuracyCalculator.Calculate([old, Delivery(1, new DateOnly(2027, 1, 10))], From, To);

        Assert.Equal(1, result.Sector.ExcludedWithoutSnapshot);
        Assert.Equal(1, result.Sector.UsedCount);
    }

    [Fact]
    public void Only_completions_inside_the_period_count_and_the_limits_are_inclusive()
    {
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, From), Delivery(2, To), Delivery(3, To.AddDays(1)), Delivery(4, From.AddDays(-1))],
            From, To);

        Assert.Equal(2, result.Sector.UsedCount);
    }

    [Fact]
    public void An_inverted_period_is_refused()
    {
        Assert.Throws<ArgumentException>(() => AccuracyCalculator.Calculate([], To, From));
    }

    [Fact]
    public void A_demand_with_two_teams_counts_once_in_each_team_and_once_in_the_sector()
    {
        var alfa = IdAt(200);
        var beta = IdAt(201);
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), teams: [alfa, beta])],
            From, To);

        Assert.Equal(1, result.Sector.UsedCount);
        Assert.Equal(1, result.Teams.Single(team => team.TeamId == alfa).Indicators.UsedCount);
        Assert.Equal(1, result.Teams.Single(team => team.TeamId == beta).Indicators.UsedCount);
    }

    [Fact]
    public void A_team_whose_absolute_estimate_difference_passes_the_sector_is_marked_above_the_sector()
    {
        var steady = IdAt(200);
        var off = IdAt(201);
        var result = AccuracyCalculator.Calculate(
            [Delivery(1, new DateOnly(2027, 1, 10), estimated: 40m, actual: 40m, teams: [steady]),
             Delivery(2, new DateOnly(2027, 1, 10), estimated: 40m, actual: 80m, teams: [off])],
            From, To);

        Assert.True(result.Teams.Single(team => team.TeamId == off).IsAboveSector);
        Assert.False(result.Teams.Single(team => team.TeamId == steady).IsAboveSector);
    }

    [Fact]
    public void Nothing_in_the_period_gives_empty_averages_not_zero()
    {
        var result = AccuracyCalculator.Calculate([], From, To);

        Assert.Equal(0, result.Sector.UsedCount);
        Assert.Null(result.Sector.AverageDeviationFromBaselineDays);
        Assert.Null(result.Sector.OnTimePercentage);
        Assert.Empty(result.Teams);
    }

    [Fact]
    public void The_same_deliveries_in_another_order_give_the_same_result()
    {
        var deliveries = Enumerable.Range(1, 15)
            .Select(index => Delivery(index, new DateOnly(2027, 1, 1).AddDays(index), estimated: 10m + index, actual: 8m + index, teams: [IdAt(200 + index % 3)]))
            .ToList();

        var forward = AccuracyCalculator.Calculate(deliveries, From, To);
        var backward = AccuracyCalculator.Calculate(Enumerable.Reverse(deliveries).ToList(), From, To);

        // O record traz listas, e o Equals do record as compara por referência: compara-se elemento a elemento.
        Assert.Equal(forward.Sector, backward.Sector);
        Assert.Equal(forward.Teams, backward.Teams);
    }
}
