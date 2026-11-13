using Coordin.Engine.Forecasting;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>Quais demandas são previsíveis e por que as outras não (RF-10; regra inviolável 5).</summary>
public class PredictabilityClassifierTests
{
    private readonly PlanningSnapshotBuilder _builder = new();

    private Classification ClassifyOnly(Guid demandId) =>
        PredictabilityClassifier.Classify(_builder.Build())[demandId];

    [Fact]
    public void Classify_CompletedDemand_IsCompleted()
    {
        var team = _builder.AddTeam();
        var demand = _builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);

        Assert.Equal(new Classification(DemandStatus.Completed, null), ClassifyOnly(demand));
    }

    [Fact]
    public void Classify_NoStartDate_IsMissingStartDate()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var demand = _builder.AddDemand(null, [(team, 8m)]);
        _builder.Assign(demand, person);

        Assert.Equal(UnpredictableReason.MissingStartDate, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_OneOfTwoTeamsWithoutEstimate_IsMissingEstimate()
    {
        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var demand = _builder.AddDemand(Day.Monday, [(teamA, 80m), (teamB, null)]);

        Assert.Equal(new Classification(DemandStatus.Unpredictable, UnpredictableReason.MissingEstimate), ClassifyOnly(demand));
    }

    [Fact]
    public void Classify_DemandRegisteredWithoutAnyEstimate_IsAcceptedAsMissingEstimate()
    {
        var team = _builder.AddTeam();
        var demand = _builder.AddDemand(Day.Monday, [(team, null)]);

        Assert.Equal(UnpredictableReason.MissingEstimate, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_DemandWithoutTeams_IsMissingEstimate()
    {
        var demand = _builder.AddDemand(Day.Monday, []);

        Assert.Equal(UnpredictableReason.MissingEstimate, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_PartWithoutAssignedPerson_IsNoCapacity()
    {
        var team = _builder.AddTeam();
        _builder.AddPerson(team, 8m);
        var demand = _builder.AddDemand(Day.Monday, [(team, 40m)]);

        Assert.Equal(new Classification(DemandStatus.Unpredictable, UnpredictableReason.NoCapacity), ClassifyOnly(demand));
    }

    [Fact]
    public void Classify_AssignedPersonWithZeroCapacity_IsNoCapacity()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 0m);
        var demand = _builder.AddDemand(Day.Monday, [(team, 40m)]);
        _builder.Assign(demand, person);

        Assert.Equal(UnpredictableReason.NoCapacity, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_PersonOnlyAbsentForAWhile_StaysPredictable()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var demand = _builder.AddDemand(Day.Monday, [(team, 40m)]);
        _builder.Assign(demand, person).AddAbsence(person, Day.Plus(1), Day.Plus(20));

        Assert.Equal(DemandStatus.Predictable, ClassifyOnly(demand).Status);
    }

    [Fact]
    public void Classify_OneTeamWithCapacityAndAnotherWithout_IsNoCapacity()
    {
        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var personA = _builder.AddPerson(teamA, 8m);
        var demand = _builder.AddDemand(Day.Monday, [(teamA, 8m), (teamB, 8m)]);
        _builder.Assign(demand, personA);

        Assert.Equal(UnpredictableReason.NoCapacity, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_NoStartDateAndNoEstimate_ReportsMissingStartDateFirst()
    {
        var team = _builder.AddTeam();
        var demand = _builder.AddDemand(null, [(team, null)]);

        Assert.Equal(UnpredictableReason.MissingStartDate, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_NoEstimateAndNoPerson_ReportsMissingEstimateFirst()
    {
        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var demand = _builder.AddDemand(Day.Monday, [(teamA, null), (teamB, 8m)]);

        Assert.Equal(UnpredictableReason.MissingEstimate, ClassifyOnly(demand).Reason);
    }

    [Fact]
    public void Classify_AllDataPresent_IsPredictable()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var demand = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(demand, person);

        Assert.Equal(new Classification(DemandStatus.Predictable, null), ClassifyOnly(demand));
    }
}
