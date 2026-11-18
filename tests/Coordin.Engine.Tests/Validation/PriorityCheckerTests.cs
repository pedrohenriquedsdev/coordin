using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;
using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>A checagem das demandas mais prioritárias (PRD 7.2), com previsões escritas à mão.</summary>
public sealed class PriorityCheckerTests
{
    /// <summary>Seis demandas com prioridades 1 a 6, todas previsíveis, criadas na ordem de prioridade.</summary>
    private static (PlanningSnapshot Snapshot, Guid[] Ids) SixDemands()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var ids = Enumerable.Range(1, 6).Select(_ => builder.AddDemand(Day.Monday, [(team, 8m)])).ToArray();
        return (builder.Build(), ids);
    }

    private static ForecastResult Forecast(Guid[] ids, Func<int, int> dayOf) =>
        new(ids.Select((id, index) => new DemandForecast(
            id,
            DemandStatus.Predictable,
            null,
            Day.Monday,
            Day.Plus(dayOf(index)),
            0,
            Day.Plus(dayOf(index)))).ToList(), []);

    [Fact]
    public void Only_the_five_highest_priorities_are_checked()
    {
        var (snapshot, ids) = SixDemands();
        var current = Forecast(ids, index => 10 + index);
        // O Coordin atrasa só a sexta (menos prioritária): ela não entra na checagem.
        var coordin = Forecast(ids, index => index == 5 ? 30 : 10 + index);

        var check = PriorityChecker.Calculate(snapshot, current, coordin);

        Assert.Equal(PriorityChecker.PriorityCheckSize, check.Items.Count);
        Assert.Equal(0, check.WorsenedCount);
        Assert.DoesNotContain(check.Items, item => item.DemandId == ids[5]);
    }

    [Fact]
    public void A_top_priority_that_gets_later_counts_as_worsened_with_the_positive_difference()
    {
        var (snapshot, ids) = SixDemands();
        var current = Forecast(ids, index => 10 + index);
        // A segunda prioridade (índice 1) passa de 11 para 15: 4 dias de atraso.
        var coordin = Forecast(ids, index => index == 1 ? 15 : 10 + index);

        var check = PriorityChecker.Calculate(snapshot, current, coordin);

        Assert.Equal(1, check.WorsenedCount);
        var worsened = check.Items.Single(item => item.DemandId == ids[1]);
        Assert.Equal(4, worsened.DeltaDays);
        Assert.Equal(2, worsened.PriorityRank);
        Assert.Equal(Day.Plus(11), worsened.CurrentForecastEndDate);
        Assert.Equal(Day.Plus(15), worsened.CoordinForecastEndDate);
    }

    [Fact]
    public void Fewer_than_five_predictable_demands_are_checked_without_error()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var ids = Enumerable.Range(1, 3).Select(_ => builder.AddDemand(Day.Monday, [(team, 8m)])).ToArray();
        var forecast = Forecast(ids, index => 10 + index);

        var check = PriorityChecker.Calculate(builder.Build(), forecast, forecast);

        Assert.Equal(3, check.Items.Count);
        Assert.Equal(0, check.WorsenedCount);
    }

    [Fact]
    public void Equal_priorities_are_broken_by_the_id()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var first = builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1);
        var second = builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1);
        var snapshot = builder.Build();
        var forecast = Forecast([first, second], _ => 10);

        var check = PriorityChecker.Calculate(snapshot, forecast, forecast);

        var expected = new[] { first, second }.OrderBy(id => id).ToArray();
        Assert.Equal(expected, check.Items.Select(item => item.DemandId).ToArray());
    }

    [Fact]
    public void A_demand_unpredictable_in_the_current_process_is_not_checked()
    {
        var (snapshot, ids) = SixDemands();
        var current = new ForecastResult(ids.Select((id, index) => index == 0
            ? new DemandForecast(id, DemandStatus.Unpredictable, UnpredictableReason.NoCapacity, null, null, null, null)
            : new DemandForecast(id, DemandStatus.Predictable, null, Day.Monday, Day.Plus(10 + index), 0, Day.Plus(10 + index))).ToList(), []);
        var coordin = Forecast(ids, index => 10 + index);

        var check = PriorityChecker.Calculate(snapshot, current, coordin);

        Assert.DoesNotContain(check.Items, item => item.DemandId == ids[0]);
    }
}
