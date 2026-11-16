using Coordin.Engine.Forecasting;
using Coordin.Engine.Reporting;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Reporting;

/// <summary>Relatório por time (RF-17; DT-24): capacidade, horas alocadas, ocupação, ociosidade e não previsíveis.</summary>
public class TeamReportTests
{
    private static readonly DateOnly Wednesday = Day.Plus(2);
    private static readonly DateOnly NextTuesday = Day.Plus(8);

    /// <summary>Calcula o relatório de um planejamento montado à mão, no período pedido.</summary>
    private static TeamReportResult Report(PlanningSnapshotBuilder builder, DateOnly from, DateOnly to)
    {
        var snapshot = builder.Build();
        return TeamReport.Build(snapshot, ForecastEngine.Calculate(snapshot), from, to);
    }

    [Fact]
    public void AlignedPeriod_FromMiddleOfTheWeek_CoversWholeWeeks()
    {
        var (start, end) = TeamReport.AlignedPeriod(Wednesday, NextTuesday);

        Assert.Equal(Day.Monday, start);
        Assert.Equal(Day.Plus(13), end); // domingo da segunda semana
    }

    [Fact]
    public void Build_OneWeek_HasOneWeek()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddTeam();

        var report = Report(builder, Day.Monday, Day.Plus(4));

        Assert.Equal(Day.Monday, report.PeriodStart);
        Assert.Equal(Day.Plus(6), report.PeriodEnd);
        Assert.Single(report.Teams[0].Weeks);
    }

    [Fact]
    public void Build_TwoWeeksFromTheMiddleOfTheFirst_HasTwoWeeks()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddTeam();

        var report = Report(builder, Wednesday, NextTuesday);

        Assert.Equal(2, report.Teams[0].Weeks.Count);
        Assert.Equal(Day.Monday, report.Teams[0].Weeks[0].WeekStart);
        Assert.Equal(Day.Plus(13), report.Teams[0].Weeks[1].WeekEnd);
    }

    [Fact]
    public void AlignedPeriod_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamReport.AlignedPeriod(Day.Plus(3), Day.Plus(1)));
    }

    [Fact]
    public void Capacity_OfTwoPeopleWithEightHours_IsEightyHoursInAWeek()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddPerson(team, 8m);
        builder.AddPerson(team, 8m);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(80m, week.CapacityHours);
    }

    [Fact]
    public void Capacity_WithAnAbsenceAndAHoliday_DropsBothFromTheWeek()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        builder.AddAbsence(person, Day.Monday, Day.Plus(1)); // segunda e terça
        builder.AddHoliday(Day.Plus(2)); // quarta, feriado para todos

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(16m, week.CapacityHours); // só quinta e sexta
    }

    [Fact]
    public void Capacity_OfATeamWithoutPeople_IsZero_AndOccupancyIsNull()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddTeam();

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(0m, week.CapacityHours);
        Assert.Null(week.Occupancy);
    }

    [Fact]
    public void AllocatedHours_AreWhatTheForecastConsumesInTheWeek()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 40m)]);
        builder.Assign(demand, person);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(40m, week.AllocatedHours);
    }

    [Fact]
    public void Occupancy_OfSixtyHoursOutOfEightyHours_IsZeroPointSevenFive()
    {
        var (builder, team) = TwoPeopleWithSixtyHoursOfWork();

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(60m, week.AllocatedHours);
        Assert.Equal(0.75m, week.Occupancy);
        Assert.Equal(team, Report(builder, Day.Monday, Day.Plus(6)).Teams[0].TeamId);
    }

    [Fact]
    public void IdleHours_AreTheCapacityNotAllocated()
    {
        var (builder, _) = TwoPeopleWithSixtyHoursOfWork();

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(20m, week.IdleHours);
    }

    [Fact]
    public void IdleHours_OfATeamWithoutDemands_EqualTheCapacity()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddPerson(team, 8m);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(week.CapacityHours, week.IdleHours);
        Assert.Equal(0m, week.AllocatedHours);
    }

    [Fact]
    public void UnpredictableDemand_DoesNotCountAsAllocated()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, null)]); // sem estimativa
        builder.Assign(demand, person);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(0m, week.AllocatedHours);
    }

    [Fact]
    public void UnpredictableCount_CountsOnlyTheUnpredictableDemandsOfTheTeam()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        builder.AddDemand(Day.Monday, [(team, null)]);
        builder.AddDemand(Day.Monday, [(team, null)]);
        var predictable = builder.AddDemand(Day.Monday, [(team, 8m)]);
        builder.Assign(predictable, person);

        var report = Report(builder, Day.Monday, Day.Plus(6));

        Assert.Equal(2, report.Teams[0].UnpredictableDemandCount);
    }

    [Fact]
    public void Build_WithTheSameInputs_IsRepeatable()
    {
        var (builder, _) = TwoPeopleWithSixtyHoursOfWork();

        var first = Report(builder, Day.Monday, Day.Plus(13));
        var second = Report(builder, Day.Monday, Day.Plus(13));

        // Os records guardam listas: compare o conteúdo serializado, não a referência das listas.
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Overload_WhenTheQueueExceedsTheCapacity_MarksTheWeeksAndListsTheDemand()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m); // 40 h por semana
        var big = builder.AddDemand(Day.Monday, [(team, 100m)]);
        builder.Assign(big, person);

        var weeks = Report(builder, Day.Monday, Day.Plus(20)).Teams[0].Weeks; // três semanas

        Assert.True(weeks[0].IsOverloaded);
        Assert.Contains(big, weeks[0].OverloadedDemandIds);
        Assert.True(weeks[1].IsOverloaded); // ainda sobram 20 h na fila ao fim da segunda
        Assert.False(weeks[2].IsOverloaded); // a última semana termina o trabalho com folga
    }

    [Fact]
    public void Overload_OfWorkInProgressWithSpareCapacity_IsNotMarked()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m); // 40 h por semana
        var demand = builder.AddDemand(Day.Monday, [(team, 30m)]);
        builder.Assign(demand, person);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.False(week.IsOverloaded); // 30 h de 40 h: sobrou capacidade
    }

    [Fact]
    public void Overload_WhenAllCapacityIsUsedAndNothingWaits_IsNotMarked()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 40m)]);
        builder.Assign(demand, person);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(week.CapacityHours, week.AllocatedHours);
        Assert.False(week.IsOverloaded);
    }

    [Fact]
    public void Overload_IgnoresDemandsThatHaveNotStartedYet()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var now = builder.AddDemand(Day.Monday, [(team, 40m)]);
        var later = builder.AddDemand(Day.Plus(7), [(team, 100m)]); // começa só na semana seguinte
        builder.Assign(now, person).Assign(later, person);

        var week = Report(builder, Day.Monday, Day.Plus(6)).Teams[0].Weeks[0];

        Assert.Equal(week.CapacityHours, week.AllocatedHours);
        Assert.False(week.IsOverloaded);
        Assert.Empty(week.OverloadedDemandIds);
    }

    /// <summary>Um time de duas pessoas de 8 h e uma demanda de 60 h: 80 h de capacidade, 60 h de trabalho.</summary>
    private static (PlanningSnapshotBuilder Builder, Guid Team) TwoPeopleWithSixtyHoursOfWork()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var first = builder.AddPerson(team, 8m);
        var second = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 60m)]);
        builder.Assign(demand, first, second);
        return (builder, team);
    }
}
