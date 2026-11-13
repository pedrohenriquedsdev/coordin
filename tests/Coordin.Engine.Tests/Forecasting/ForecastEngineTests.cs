using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>
/// Os casos obrigatórios do motor (TDD 8.2) sobre a simulação dia a dia. Datas: <see cref="Day.Plus"/>
/// conta a partir da segunda-feira 2026-11-02; +4 é sexta, +5 e +6 são o fim de semana, +7 é a segunda seguinte.
/// </summary>
public class ForecastEngineTests
{
    private readonly PlanningSnapshotBuilder _builder = new();

    private static DemandForecast Of(ForecastResult result, Guid demandId) =>
        result.Demands.Single(forecast => forecast.DemandId == demandId);

    private (Guid Team, Guid Person, Guid Demand) OneTeamOnePerson(decimal dailyHours, decimal effort, DateOnly? start = null)
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, dailyHours);
        var demand = _builder.AddDemand(start ?? Day.Monday, [(team, effort)]);
        _builder.Assign(demand, person);
        return (team, person, demand);
    }

    [Fact]
    public void Calculate_SixteenHoursOneEightHourPerson_EndsOnTheSecondDay()
    {
        var (_, _, demand) = OneTeamOnePerson(8m, 16m);

        var forecast = Of(ForecastEngine.Calculate(_builder.Build()), demand);

        Assert.Equal(DemandStatus.Predictable, forecast.Status);
        Assert.Equal(Day.Monday, forecast.ForecastStartDate);
        Assert.Equal(Day.Plus(1), forecast.EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_EffortNotMultipleOfTheDay_EndsOnTheThirdDay()
    {
        var (_, _, demand) = OneTeamOnePerson(8m, 20m);

        Assert.Equal(Day.Plus(2), Of(ForecastEngine.Calculate(_builder.Build()), demand).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_StartOnASaturday_StartsWorkingOnMonday()
    {
        var (_, _, demand) = OneTeamOnePerson(8m, 8m, start: Day.Plus(5));

        var forecast = Of(ForecastEngine.Calculate(_builder.Build()), demand);

        Assert.Equal(Day.Plus(7), forecast.ForecastStartDate);
        Assert.Equal(Day.Plus(7), forecast.EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_TwoPeopleInTheSameTeam_FinishSooner()
    {
        var team = _builder.AddTeam();
        var first = _builder.AddPerson(team, 8m);
        var second = _builder.AddPerson(team, 8m);
        var demand = _builder.AddDemand(Day.Monday, [(team, 32m)]);
        _builder.Assign(demand, first, second);

        Assert.Equal(Day.Plus(1), Of(ForecastEngine.Calculate(_builder.Build()), demand).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_AbsenceInTheMiddle_PushesTheEnd()
    {
        var (_, person, demand) = OneTeamOnePerson(8m, 40m);
        _builder.AddAbsence(person, Day.Plus(2), Day.Plus(2));

        Assert.Equal(Day.Plus(7), Of(ForecastEngine.Calculate(_builder.Build()), demand).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_HolidayInTheMiddle_PushesTheEndAndNobodyWorksOnIt()
    {
        var (_, _, demand) = OneTeamOnePerson(8m, 24m);
        _builder.AddHoliday(Day.Plus(1));

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Plus(3), Of(result, demand).EndDateWithoutMargin);
        Assert.DoesNotContain(result.ConsumedHours, entry => entry.Date == Day.Plus(1));
    }

    [Fact]
    public void Calculate_TwoTeams_EndsWhenTheLastPartEnds()
    {
        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var personA = _builder.AddPerson(teamA, 8m);
        var personB = _builder.AddPerson(teamB, 4m);
        var demand = _builder.AddDemand(Day.Monday, [(teamA, 40m), (teamB, 20m)]);
        _builder.Assign(demand, personA, personB);

        Assert.Equal(Day.Plus(4), Of(ForecastEngine.Calculate(_builder.Build()), demand).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_SplittingTheEffortAcrossTwoTeams_DoesNotHalveTheDeadline()
    {
        var single = new PlanningSnapshotBuilder();
        var team = single.AddTeam();
        var person = single.AddPerson(team, 10m);
        var demand = single.AddDemand(Day.Monday, [(team, 100m)]);
        single.Assign(demand, person);
        var oneTeamEnd = Of(ForecastEngine.Calculate(single.Build()), demand).EndDateWithoutMargin;

        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var personA = _builder.AddPerson(teamA, 10m);
        var personB = _builder.AddPerson(teamB, 5m);
        var split = _builder.AddDemand(Day.Monday, [(teamA, 50m), (teamB, 50m)]);
        _builder.Assign(split, personA, personB);
        var twoTeamsEnd = Of(ForecastEngine.Calculate(_builder.Build()), split).EndDateWithoutMargin;

        Assert.Equal(Day.Plus(11), oneTeamEnd);
        Assert.Equal(oneTeamEnd, twoTeamsEnd);
    }

    [Fact]
    public void Calculate_SecondDemandOfTheSamePerson_WaitsForTheFirst()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 16m)]);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(first, person).Assign(second, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Plus(1), Of(result, first).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(2), Of(result, second).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(2), Of(result, second).ForecastStartDate);
    }

    [Fact]
    public void Calculate_LeftoverOfTheDay_GoesToTheNextDemand()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 12m)]);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(first, person).Assign(second, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Plus(1), Of(result, first).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(1), Of(result, second).ForecastStartDate);
        Assert.Equal(Day.Plus(2), Of(result, second).EndDateWithoutMargin);
        Assert.Contains(result.ConsumedHours, e => e.DemandId == second && e.Date == Day.Plus(1) && e.Hours == 4m);
    }

    [Fact]
    public void Calculate_LaterDemand_NeverChangesTheEarlierOne()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 20m)]);
        _builder.Assign(first, person);
        var alone = Of(ForecastEngine.Calculate(_builder.Build()), first);

        var second = _builder.AddDemand(Day.Monday, [(team, 40m)]);
        _builder.Assign(second, person);
        var together = Of(ForecastEngine.Calculate(_builder.Build()), first);

        Assert.Equal(alone, together);
    }

    [Fact]
    public void Calculate_DemandThatHasNotStarted_DoesNotHoldTheQueue()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var later = _builder.AddDemand(Day.Plus(7), [(team, 8m)]);
        var earlier = _builder.AddDemand(Day.Monday, [(team, 16m)]);
        _builder.Assign(later, person).Assign(earlier, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Plus(1), Of(result, earlier).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(7), Of(result, later).ForecastStartDate);
    }

    [Fact]
    public void Calculate_PersonWithNothingEligible_ConsumesNoHours()
    {
        var (_, _, _) = OneTeamOnePerson(8m, 8m, start: Day.Plus(7));

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.All(result.ConsumedHours, entry => Assert.True(entry.Date >= Day.Plus(7)));
    }

    [Fact]
    public void Calculate_FollowPriorityMode_FollowsThePriorityRanking()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 2, executionRank: 1);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 2);
        _builder.Assign(a, person).Assign(b, person).Mode(ExecutionOrderMode.FollowPriority);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Monday, Of(result, b).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(1), Of(result, a).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_CustomMode_FollowsTheExecutionRanking()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 2, executionRank: 1);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 2);
        _builder.Assign(a, person).Assign(b, person).Mode(ExecutionOrderMode.Custom);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Monday, Of(result, a).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(1), Of(result, b).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_TiedRankings_SmallerIdGoesFirst()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 1);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 1);
        _builder.Assign(first, person).Assign(second, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.True(first.CompareTo(second) < 0);
        Assert.Equal(Day.Monday, Of(result, first).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(1), Of(result, second).EndDateWithoutMargin);
    }

    [Fact]
    public void Calculate_DemandThatNeverEnds_BecomesNoCapacityAndDoesNotDelayTheNext()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 1m);
        var huge = _builder.AddDemand(Day.Monday, [(team, 100_000m)]);
        var small = _builder.AddDemand(Day.Monday, [(team, 1m)]);
        _builder.Assign(huge, person).Assign(small, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(new DemandForecast(huge, DemandStatus.Unpredictable, UnpredictableReason.NoCapacity, null, null, null, null), Of(result, huge));
        Assert.Equal(Day.Monday, Of(result, small).EndDateWithoutMargin);
        Assert.DoesNotContain(result.ConsumedHours, entry => entry.DemandId == huge);
    }

    [Fact]
    public void Calculate_MarginOverTenDays_IsTwoBusinessDaysAfterTheEnd()
    {
        var (_, _, demand) = OneTeamOnePerson(10m, 100m);
        _builder.Margin(20m);

        var forecast = Of(ForecastEngine.Calculate(_builder.Build()), demand);

        Assert.Equal(Day.Plus(11), forecast.EndDateWithoutMargin);
        Assert.Equal(2, forecast.MarginBusinessDays);
        Assert.Equal(Day.Plus(15), forecast.ForecastEndDate);
    }

    [Fact]
    public void Calculate_UnpredictableDemandBefore_DoesNotChangeTheOther()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var predictable = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 2, executionRank: 2);
        _builder.Assign(predictable, person);
        var alone = Of(ForecastEngine.Calculate(_builder.Build()), predictable);

        var withoutEstimate = _builder.AddDemand(Day.Monday, [(team, null)], priorityRank: 1, executionRank: 1);
        _builder.Assign(withoutEstimate, person);
        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(alone, Of(result, predictable));
        Assert.Equal(UnpredictableReason.MissingEstimate, Of(result, withoutEstimate).Reason);
        Assert.DoesNotContain(result.ConsumedHours, entry => entry.DemandId == withoutEstimate);
    }

    [Fact]
    public void Calculate_EstimateArrivesLater_TheDemandBecomesPredictable()
    {
        var before = new PlanningSnapshotBuilder();
        var team = before.AddTeam();
        var person = before.AddPerson(team, 8m);
        var demand = before.AddDemand(Day.Monday, [(team, null)]);
        before.Assign(demand, person);
        Assert.Equal(DemandStatus.Unpredictable, Of(ForecastEngine.Calculate(before.Build()), demand).Status);

        var after = new PlanningSnapshotBuilder();
        var teamAfter = after.AddTeam();
        var personAfter = after.AddPerson(teamAfter, 8m);
        var demandAfter = after.AddDemand(Day.Monday, [(teamAfter, 40m)]);
        after.Assign(demandAfter, personAfter);

        Assert.Equal(DemandStatus.Predictable, Of(ForecastEngine.Calculate(after.Build()), demandAfter).Status);
    }

    [Fact]
    public void Calculate_Margin_DoesNotConsumeCapacity()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 32m)]);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(first, person).Assign(second, person).Margin(50m);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(Day.Plus(7), Of(result, first).ForecastEndDate);
        Assert.Equal(Day.Plus(4), Of(result, second).ForecastStartDate);
    }

    [Fact]
    public void Calculate_Consumption_MatchesTheEffortAndNeverExceedsTheCapacity()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 6m);
        var first = _builder.AddDemand(Day.Monday, [(team, 14m)]);
        var second = _builder.AddDemand(Day.Plus(1), [(team, 9m)]);
        _builder.Assign(first, person).Assign(second, person).AddAbsence(person, Day.Plus(3), Day.Plus(3));

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(14m, result.ConsumedHours.Where(e => e.DemandId == first).Sum(e => e.Hours));
        Assert.Equal(9m, result.ConsumedHours.Where(e => e.DemandId == second).Sum(e => e.Hours));
        Assert.All(result.ConsumedHours.GroupBy(e => e.Date), day => Assert.True(day.Sum(e => e.Hours) <= 6m));
        Assert.DoesNotContain(result.ConsumedHours, entry => entry.Date == Day.Plus(3));
    }

    [Fact]
    public void Calculate_CompletedDemand_HasNoForecastAndConsumesNothing()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var done = _builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);
        _builder.Assign(done, person);

        var result = ForecastEngine.Calculate(_builder.Build());

        Assert.Equal(DemandStatus.Completed, Of(result, done).Status);
        Assert.Empty(result.ConsumedHours);
    }

    [Fact]
    public void Calculate_NoDemands_ReturnsAnEmptyResult()
    {
        var result = ForecastEngine.Calculate(new PlanningSnapshotBuilder().Build());

        Assert.Empty(result.Demands);
        Assert.Empty(result.ConsumedHours);
    }

    [Fact]
    public void Calculate_InvalidSnapshot_Throws()
    {
        _builder.Margin(-5m);

        Assert.Throws<ArgumentOutOfRangeException>(() => ForecastEngine.Calculate(_builder.Build()));
    }
}
