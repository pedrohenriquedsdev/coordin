using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>Nenhum cenário viola uma dependência, e a verificação enxerga uma violação quando ela existe (RF-18; RNF-08).</summary>
public class DependencyCheckTests
{
    private readonly PlanningSnapshotBuilder _builder = new();

    [Fact]
    public void DependentPutBeforeItsPrerequisite_StillStartsAfterIt_AndNothingIsListed()
    {
        var team = _builder.AddTeam();
        var personA = _builder.AddPerson(team, 8m);
        var personB = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 16m)]);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(a, personA).Assign(b, personB).DependsOn(b, a);
        var snapshot = _builder.Build();

        var result = ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, [b, a]));

        Assert.Empty(DependencyCheck.Violations(snapshot, result));
        Assert.True(result.Demands.Single(f => f.DemandId == b).ForecastStartDate > result.Demands.Single(f => f.DemandId == a).ForecastEndDate);
    }

    [Fact]
    public void AHandMadeViolation_IsListed()
    {
        var team = _builder.AddTeam();
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.DependsOn(b, a);
        var snapshot = _builder.Build();
        var result = new ForecastResult(
            [
                new DemandForecast(a, DemandStatus.Predictable, null, Day.Monday, Day.Plus(2), 0, Day.Plus(2)),
                new DemandForecast(b, DemandStatus.Predictable, null, Day.Plus(2), Day.Plus(3), 0, Day.Plus(3)), // começa no fim de A
            ],
            []);

        var violation = Assert.Single(DependencyCheck.Violations(snapshot, result));

        Assert.Equal(new DependencyInput(b, a), violation);
    }

    [Fact]
    public void AnUnpredictablePrerequisite_IsNotAViolation()
    {
        var team = _builder.AddTeam();
        var a = _builder.AddDemand(Day.Monday, [(team, null)]);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.DependsOn(b, a);
        var snapshot = _builder.Build();

        Assert.Empty(DependencyCheck.Violations(snapshot, ForecastEngine.Calculate(snapshot)));
    }

    [Fact]
    public void RandomOrders_InManyGeneratedPlannings_NeverViolateADependency()
    {
        var withDependencies = 0;
        for (var seed = 1; seed <= 400; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var random = new Random(seed);
            var order = ExecutionOrders.Current(snapshot).OrderBy(_ => random.Next()).ToList();

            var result = ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, order));

            Assert.True(DependencyCheck.Violations(snapshot, result).Count == 0, $"Violação de dependência na semente {seed}.");
            withDependencies += snapshot.Dependencies.Count > 0 ? 1 : 0;
        }

        Assert.True(withDependencies > 100, $"Poucos planejamentos com dependências: {withDependencies}.");
    }

    [Fact]
    public void AnUnpredictableDependent_IsNotAViolation()
    {
        var team = _builder.AddTeam();
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        var b = _builder.AddDemand(null, [(team, 8m)]); // sem data de início
        _builder.DependsOn(b, a);
        var snapshot = _builder.Build();

        Assert.Empty(DependencyCheck.Violations(snapshot, ForecastEngine.Calculate(snapshot)));
    }

    [Fact]
    public void NullArguments_AreRefused()
    {
        var snapshot = _builder.Build();

        Assert.Throws<ArgumentNullException>(() => DependencyCheck.Violations(null!, new ForecastResult([], [])));
        Assert.Throws<ArgumentNullException>(() => DependencyCheck.Violations(snapshot, null!));
    }
}

