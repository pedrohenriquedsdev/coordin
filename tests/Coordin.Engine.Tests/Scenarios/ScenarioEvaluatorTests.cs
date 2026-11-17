using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>
/// Avaliação de uma ordem e score do DT-25 (RF-13, RF-14). Datas: <see cref="Day.Plus"/> conta a partir da
/// segunda-feira 2026-11-02; +4 é sexta, +5 e +6 são o fim de semana, +7 é a segunda seguinte.
/// </summary>
public class ScenarioEvaluatorTests
{
    private readonly PlanningSnapshotBuilder _builder = new();

    private static ScenarioItem ItemOf(Scenario scenario, Guid demandId) =>
        scenario.Items.Single(item => item.DemandId == demandId);

    /// <summary>Duas demandas de uma pessoa só: A (16 h, prioridade 1) e B (8 h, prioridade 2).</summary>
    private (Guid A, Guid B) OnePersonTwoDemands(decimal hoursA = 16m, decimal hoursB = 8m)
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, hoursA)]);
        var b = _builder.AddDemand(Day.Monday, [(team, hoursB)]);
        _builder.Assign(a, person).Assign(b, person);
        return (a, b);
    }

    [Fact]
    public void CurrentOrder_ReproducesTheCurrentSchedule_WithZeroDifference()
    {
        var (a, b) = OnePersonTwoDemands();
        var snapshot = _builder.Build();
        var result = ForecastEngine.Calculate(snapshot);

        var scenario = ScenarioEvaluator.Evaluate(snapshot, ExecutionOrders.Current(snapshot));

        Assert.Equal(result.Demands.Single(f => f.DemandId == a).ForecastEndDate, ItemOf(scenario, a).ForecastEndDate);
        Assert.Equal(result.Demands.Single(f => f.DemandId == b).ForecastEndDate, ItemOf(scenario, b).ForecastEndDate);
        Assert.All(scenario.Items, item => Assert.Equal(0, item.DeltaDays));
        Assert.Equal(0, scenario.Score.Delta);
        Assert.Equal(scenario.Score.Current, scenario.Score.Scenario);
        Assert.Equal(0, scenario.SectorDeltaDays);
        Assert.True(scenario.IsComparable);
    }

    [Fact]
    public void AnotherOrder_ChangesTheDatesByTheCapacity_AndDeltaDaysHasTheRightSign()
    {
        var (a, b) = OnePersonTwoDemands();
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [b, a]);

        Assert.Equal(Day.Plus(2), ItemOf(scenario, b).CurrentForecastEndDate); // hoje B é a segunda da fila
        Assert.Equal(Day.Monday, ItemOf(scenario, b).ForecastEndDate);
        Assert.Equal(-2, ItemOf(scenario, b).DeltaDays);
        Assert.Equal(Day.Plus(1), ItemOf(scenario, a).CurrentForecastEndDate);
        Assert.Equal(Day.Plus(2), ItemOf(scenario, a).ForecastEndDate);
        Assert.Equal(1, ItemOf(scenario, a).DeltaDays);
        Assert.Equal([b, a], scenario.Items.Select(item => item.DemandId));
    }

    [Fact]
    public void Weights_AreHighestPriorityMinusRankPlusOne()
    {
        var team = _builder.AddTeam();
        var ids = Enumerable.Range(1, 3).Select(_ =>
        {
            var person = _builder.AddPerson(team, 8m);
            var demand = _builder.AddDemand(Day.Monday, [(team, 8m)]);
            _builder.Assign(demand, person);
            return demand;
        }).ToList();
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, ExecutionOrders.Current(snapshot));

        Assert.Equal([3, 2, 1], ids.Select(id => ItemOf(scenario, id).Weight));
    }

    [Fact]
    public void AnticipatingTheMostPrioritized_ImprovesTheScoreByTwiceTheWeightGap()
    {
        // A (prioridade 1) e C (prioridade 3) dividem uma pessoa; B (prioridade 2) tem a sua e não muda.
        var team = _builder.AddTeam();
        var shared = _builder.AddPerson(team, 8m);
        var own = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 1, executionRank: 2);
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 2, executionRank: 3);
        var c = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 3, executionRank: 1);
        _builder.Assign(a, shared).Assign(c, shared).Assign(b, own).Mode(ExecutionOrderMode.Custom);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [a, b, c]);

        Assert.Equal(Day.Plus(3), ItemOf(scenario, a).CurrentForecastEndDate);
        Assert.Equal(Day.Plus(1), ItemOf(scenario, a).ForecastEndDate); // A antecipa 2 dias úteis
        Assert.Equal(Day.Plus(3), ItemOf(scenario, c).ForecastEndDate); // C atrasa 2 dias úteis
        Assert.Equal(0, ItemOf(scenario, b).DeltaDays);
        Assert.Equal(-((3 - 1) * 2), scenario.Score.Delta); // 3 × (−2) + 1 × (+2)
    }

    [Fact]
    public void Weights_StayAtLeastOne_WhenThePrioritiesHaveGaps()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var first = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 2, executionRank: 2);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 5, executionRank: 5);
        _builder.Assign(first, person).Assign(second, person);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, ExecutionOrders.Current(snapshot));

        Assert.Equal(4, ItemOf(scenario, first).Weight);
        Assert.Equal(1, ItemOf(scenario, second).Weight);
        Assert.All(scenario.Items, item => Assert.True(item.Weight >= 1));
    }

    [Fact]
    public void TheContributions_SumToTheScoreDelta_WithSeveralTeamsAndDependencies()
    {
        var teamA = _builder.AddTeam();
        var teamB = _builder.AddTeam();
        var personA = _builder.AddPerson(teamA, 8m);
        var personB = _builder.AddPerson(teamB, 6m);
        var first = _builder.AddDemand(Day.Monday, [(teamA, 30m), (teamB, 20m)]);
        var second = _builder.AddDemand(Day.Monday, [(teamA, 12m)]);
        var third = _builder.AddDemand(Day.Plus(1), [(teamB, 18m)]);
        _builder.Assign(first, personA, personB).Assign(second, personA).Assign(third, personB).DependsOn(third, second);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [third, second, first]);

        Assert.Equal(scenario.Score.Delta, scenario.Items.Sum(item => item.ScoreContributionDelta));
        Assert.Equal(scenario.Score.Scenario - scenario.Score.Current, scenario.Score.Delta);
    }

    [Fact]
    public void TheContributions_SumToTheScoreDelta_InManyGeneratedPlannings()
    {
        for (var seed = 1; seed <= 150; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var random = new Random(seed);
            var order = ExecutionOrders.Current(snapshot).OrderBy(_ => random.Next()).ToList();

            var scenario = ScenarioEvaluator.Evaluate(snapshot, order);

            Assert.True(scenario.Score.Delta == scenario.Items.Sum(item => item.ScoreContributionDelta), $"A soma das parcelas não fecha na semente {seed}.");
        }
    }

    [Fact]
    public void ADemandThatFinishesEarlier_HasANegativeContribution()
    {
        var (a, b) = OnePersonTwoDemands();

        var scenario = ScenarioEvaluator.Evaluate(_builder.Build(), [b, a]);

        Assert.True(ItemOf(scenario, b).ScoreContributionDelta < 0);
        Assert.True(ItemOf(scenario, a).ScoreContributionDelta > 0);
    }

    [Fact]
    public void ThePriorityIsShownBesideTheNewPosition()
    {
        var team = _builder.AddTeam();
        var demands = Enumerable.Range(1, 3).Select(_ =>
        {
            var person = _builder.AddPerson(team, 8m);
            var demand = _builder.AddDemand(Day.Monday, [(team, 8m)]);
            _builder.Assign(demand, person);
            return demand;
        }).ToList();
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [demands[2], demands[0], demands[1]]);

        Assert.Equal(3, ItemOf(scenario, demands[2]).PriorityRank);
        Assert.Equal(1, ItemOf(scenario, demands[2]).NewExecutionRank);
        Assert.Equal(1, ItemOf(scenario, demands[0]).PriorityRank);
        Assert.Equal(2, ItemOf(scenario, demands[0]).NewExecutionRank);
    }

    [Fact]
    public void TheSectorEnd_IsTheLastForecastEnd_AndItsDifferenceIsInCalendarDays()
    {
        // Com 100% de margem a duração de cada demanda pesa no fim comunicado, então a ordem muda o prazo do setor.
        _builder.Margin(100m);
        var (a, b) = OnePersonTwoDemands(hoursA: 8m, hoursB: 24m);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [b, a]);

        Assert.Equal(Day.Plus(7), scenario.SectorEndDate);
        Assert.Equal(-1, scenario.SectorDeltaDays);
    }

    [Fact]
    public void CompletedAndUnpredictableDemands_StayOutOfTheItemsAndTheScore()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var open = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        var completed = _builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);
        var withoutEstimate = _builder.AddDemand(Day.Monday, [(team, null)]);
        _builder.Assign(open, person).Assign(withoutEstimate, person);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [withoutEstimate, completed, open]);

        Assert.Equal([open], scenario.Items.Select(item => item.DemandId));
        Assert.Equal(3, scenario.ExecutionOrder.Count);
        Assert.True(scenario.IsComparable);
    }

    [Fact]
    public void AnOrderThatMakesADemandOverflowTheHorizon_IsNotComparable()
    {
        // Uma pessoa de 1 h por dia: cada demanda de 1.000 h cabe em cinco anos, mas as duas juntas não.
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 1m);
        var x = _builder.AddDemand(Day.Monday, [(team, 1000m)]);
        var y = _builder.AddDemand(Day.Monday, [(team, 1000m)]);
        _builder.Assign(x, person).Assign(y, person);
        var snapshot = _builder.Build();

        var scenario = ScenarioEvaluator.Evaluate(snapshot, [y, x]);

        Assert.False(scenario.IsComparable);
        Assert.Empty(scenario.Items); // nenhuma das duas é previsível nos dois cronogramas
    }

    [Fact]
    public void TheEvaluation_IsTheSame_WithTheListsOfTheSnapshotShuffled()
    {
        for (var seed = 1; seed <= 60; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var random = new Random(seed);
            var order = ExecutionOrders.Current(snapshot).OrderBy(_ => random.Next()).ToList();

            var first = JsonSerializer.Serialize(ScenarioEvaluator.Evaluate(snapshot, order));
            var second = JsonSerializer.Serialize(ScenarioEvaluator.Evaluate(RandomPlanningGenerator.Shuffled(snapshot, seed + 500), order));
            var again = JsonSerializer.Serialize(ScenarioEvaluator.Evaluate(snapshot, order));

            Assert.True(first == second, $"A avaliação mudou com as listas embaralhadas na semente {seed}.");
            Assert.True(first == again, $"A avaliação mudou entre duas execuções na semente {seed}.");
        }
    }

    [Fact]
    public void Evaluate_NeverChangesTheSnapshot()
    {
        var (a, b) = OnePersonTwoDemands();
        var snapshot = _builder.Build();
        var before = JsonSerializer.Serialize(snapshot);

        ScenarioEvaluator.Evaluate(snapshot, [b, a]);

        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
    }
}
