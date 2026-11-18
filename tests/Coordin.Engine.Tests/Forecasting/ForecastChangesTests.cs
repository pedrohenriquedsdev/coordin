using Coordin.Engine.Forecasting;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>O histórico da data prevista (RF-21): o que muda, o que não muda, e a causa (DT-14).</summary>
public sealed class ForecastChangesTests
{
    private static Guid IdAt(int index) => new(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    private static DemandForecast Predictable(Guid id, DateOnly end) =>
        new(id, DemandStatus.Predictable, null, end.AddDays(-5), end.AddDays(-2), 0, end);

    private static DemandForecast Unpredictable(Guid id) =>
        new(id, DemandStatus.Unpredictable, UnpredictableReason.MissingEstimate, null, null, null, null);

    private static DemandForecast Completed(Guid id) =>
        new(id, DemandStatus.Completed, null, null, null, null, null);

    private static ForecastResult Result(params DemandForecast[] demands) =>
        new(demands, []);

    [Fact]
    public void A_first_forecast_is_recorded_with_no_previous_date()
    {
        var id = IdAt(1);
        var end = new DateOnly(2027, 3, 10);

        var changes = ForecastChanges.Between(new Dictionary<Guid, DateOnly?>(), Result(Predictable(id, end)), ForecastChangeCause.NewDemand);

        Assert.Equal([new ForecastChange(id, null, end, ForecastChangeCause.NewDemand)], changes);
    }

    [Fact]
    public void A_moved_date_is_recorded_with_both_dates_and_the_cause()
    {
        var id = IdAt(2);
        var before = new DateOnly(2027, 3, 10);
        var after = new DateOnly(2027, 3, 17);

        var changes = ForecastChanges.Between(
            new Dictionary<Guid, DateOnly?> { [id] = before },
            Result(Predictable(id, after)),
            ForecastChangeCause.Dependency);

        Assert.Equal([new ForecastChange(id, before, after, ForecastChangeCause.Dependency)], changes);
    }

    [Fact]
    public void A_date_that_does_not_move_records_nothing()
    {
        var id = IdAt(3);
        var end = new DateOnly(2027, 3, 10);

        var changes = ForecastChanges.Between(
            new Dictionary<Guid, DateOnly?> { [id] = end },
            Result(Predictable(id, end)),
            ForecastChangeCause.Estimate);

        Assert.Empty(changes);
    }

    [Fact]
    public void A_demand_that_becomes_unpredictable_is_recorded_with_no_new_date()
    {
        var id = IdAt(4);
        var before = new DateOnly(2027, 3, 10);

        var changes = ForecastChanges.Between(
            new Dictionary<Guid, DateOnly?> { [id] = before },
            Result(Unpredictable(id)),
            ForecastChangeCause.Allocation);

        Assert.Equal([new ForecastChange(id, before, null, ForecastChangeCause.Allocation)], changes);
    }

    [Fact]
    public void A_demand_that_stays_unpredictable_records_nothing()
    {
        var id = IdAt(5);

        var changes = ForecastChanges.Between(new Dictionary<Guid, DateOnly?>(), Result(Unpredictable(id)), ForecastChangeCause.Capacity);

        Assert.Empty(changes);
    }

    [Fact]
    public void Completed_demands_keep_their_frozen_forecast_and_record_nothing()
    {
        var id = IdAt(6);

        var changes = ForecastChanges.Between(
            new Dictionary<Guid, DateOnly?> { [id] = new DateOnly(2027, 3, 10) },
            Result(Completed(id)),
            ForecastChangeCause.Absence);

        Assert.Empty(changes);
    }

    [Fact]
    public void The_order_of_the_input_does_not_change_the_result()
    {
        var ids = Enumerable.Range(1, 12).Select(IdAt).ToList();
        var previous = ids.ToDictionary(id => id, _ => (DateOnly?)new DateOnly(2027, 3, 1));
        var demands = ids.Select((id, index) => Predictable(id, new DateOnly(2027, 3, 1).AddDays(index % 3))).ToList();

        var forward = ForecastChanges.Between(previous, new ForecastResult(demands, []), ForecastChangeCause.Estimate);
        var backward = ForecastChanges.Between(previous, new ForecastResult(Enumerable.Reverse(demands).ToList(), []), ForecastChangeCause.Estimate);

        Assert.Equal(forward, backward);
        Assert.Equal(forward.OrderBy(change => change.DemandId).Select(change => change.DemandId), forward.Select(change => change.DemandId));
    }
}
