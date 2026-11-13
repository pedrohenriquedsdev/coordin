using Coordin.Engine.Planning;

namespace Coordin.Engine.Tests.Planning;

/// <summary>Entradas inválidas viram erro de argumento, nunca previsão errada (design D6, RNF-08).</summary>
public class PlanningSnapshotValidatorTests
{
    [Fact]
    public void ReferenceDate_Monday_IsAMonday()
    {
        Assert.Equal(DayOfWeek.Monday, Day.Monday.DayOfWeek);
    }

    [Fact]
    public void EmptySnapshot_IsAccepted()
    {
        var snapshot = new PlanningSnapshotBuilder().Build();

        PlanningSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void ValidSnapshot_IsAccepted()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 16m)]);
        builder.Assign(demand, person).AddAbsence(person, Day.Plus(1), Day.Plus(2)).AddHoliday(Day.Plus(7)).Margin(20m);

        PlanningSnapshotValidator.Validate(builder.Build());
    }

    [Fact]
    public void DemandWithoutEstimateAndWithoutTeams_IsAccepted()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddDemand(null, [(team, null)]);
        builder.AddDemand(null, []);

        PlanningSnapshotValidator.Validate(builder.Build());
    }

    [Fact]
    public void NullSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PlanningSnapshotValidator.Validate(null!));
    }

    [Fact]
    public void NegativeCapacity_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddPerson(builder.AddTeam(), -1m);

        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void EffortZeroOrNegative_Throws(int hours)
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddDemand(Day.Monday, [(builder.AddTeam(), hours)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void NegativeMargin_Throws()
    {
        var snapshot = new PlanningSnapshotBuilder().Margin(-1m).Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void DuplicatedId_Throws()
    {
        var snapshot = new PlanningSnapshotBuilder().Build() with
        {
            Teams = [new TeamInput(Guid.Empty), new TeamInput(Guid.Empty)],
        };

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void AssignmentToUnknownDemand_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var person = builder.AddPerson(builder.AddTeam(), 8m);
        builder.Assign(Guid.NewGuid(), person);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void AssignmentOfUnknownPerson_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var demand = builder.AddDemand(Day.Monday, [(builder.AddTeam(), 8m)]);
        builder.Assign(demand, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void AssignmentOfPersonFromAnotherTeam_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var teamA = builder.AddTeam();
        var teamB = builder.AddTeam();
        var personOfB = builder.AddPerson(teamB, 8m);
        var demand = builder.AddDemand(Day.Monday, [(teamA, 8m)]);
        builder.Assign(demand, personOfB);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void RepeatedAssignment_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 8m)]);
        builder.Assign(demand, person, person);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void AbsenceEndingBeforeItStarts_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var person = builder.AddPerson(builder.AddTeam(), 8m);
        builder.AddAbsence(person, Day.Plus(3), Day.Plus(1));

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void AbsenceOfUnknownPerson_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddAbsence(Guid.NewGuid(), Day.Monday, Day.Monday);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void PersonOfUnknownTeam_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        builder.AddPerson(Guid.NewGuid(), 8m);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }

    [Fact]
    public void DemandWithTwoPartsOfTheSameTeam_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddDemand(Day.Monday, [(team, 8m), (team, 4m)]);

        Assert.Throws<ArgumentException>(() => PlanningSnapshotValidator.Validate(builder.Build()));
    }
}
