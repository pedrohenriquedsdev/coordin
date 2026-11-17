using System.Text.Json;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>A busca local e as recomendações (RF-13; DT-26; RNF-02). Datas a partir da segunda-feira <see cref="Day.Monday"/>.</summary>
public class ScenarioSearchTests
{
    private const int Budget = 200;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly PlanningSnapshotBuilder _builder = new();

    /// <summary>
    /// Três demandas de 16 h dividindo uma pessoa, com a ordem de execução invertida em relação à prioridade:
    /// a fila atual é [C, B, A] (score 28) e a melhor ordem é [A, B, C] (score 20), que não é um vizinho
    /// direto da atual, então a busca precisa de mais de um passo.
    /// </summary>
    private (Guid A, Guid B, Guid C) ReversedOnOnePerson()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 1, executionRank: 3);
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 2, executionRank: 2);
        var c = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 3, executionRank: 1);
        _builder.Assign(a, person).Assign(b, person).Assign(c, person).Mode(ExecutionOrderMode.Custom);
        return (a, b, c);
    }

    [Fact]
    public void WhenThereIsAnImprovement_RecommendsAtMostThreeScenarios_AllBetterThanTheCurrent_BestFirst()
    {
        var (a, b, c) = ReversedOnOnePerson();

        var scenarios = ScenarioSearch.Recommend(_builder.Build(), Budget, Ct);

        Assert.InRange(scenarios.Count, 1, 3);
        Assert.All(scenarios, scenario => Assert.True(scenario.Score.Delta < 0));
        Assert.Equal(scenarios.Select(s => s.Score.Scenario).OrderBy(score => score), scenarios.Select(s => s.Score.Scenario));
        Assert.Equal([a, b, c], scenarios[0].ExecutionOrder); // exige a subida de encosta: não é vizinho direto da atual
        Assert.Equal(28, scenarios[0].Score.Current);
        Assert.Equal(20, scenarios[0].Score.Scenario);
        Assert.Equal(-8, scenarios[0].Score.Delta);
        Assert.NotEmpty(scenarios[0].Items); // os cenários finais vêm completos, com os itens
    }

    [Fact]
    public void RecommendedSequences_NeverRepeat()
    {
        ReversedOnOnePerson();

        var scenarios = ScenarioSearch.Recommend(_builder.Build(), Budget, Ct);
        var keys = scenarios.Select(scenario => string.Join(",", scenario.ExecutionOrder)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void WhenTheBestOrderIsASingleMove_ItAppearsOnlyOnce()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 1, executionRank: 2);
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 2, executionRank: 1);
        _builder.Assign(a, person).Assign(b, person).Mode(ExecutionOrderMode.Custom);

        var scenarios = ScenarioSearch.Recommend(_builder.Build(), Budget, Ct);

        var only = Assert.Single(scenarios);
        Assert.Equal([a, b], only.ExecutionOrder);
    }

    [Fact]
    public void WhenTheCurrentScheduleIsAlreadyTheBest_TheListIsEmpty()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)]); // prioridade 1, curta
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)]); // prioridade 2, longa
        _builder.Assign(a, person).Assign(b, person);

        Assert.Empty(ScenarioSearch.Recommend(_builder.Build(), Budget, Ct));
    }

    [Fact]
    public void WithNoDemand_OneMovableDemand_OrAZeroBudget_TheListIsEmpty()
    {
        Assert.Empty(ScenarioSearch.Recommend(new PlanningSnapshotBuilder().Build(), Budget, Ct));

        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var open = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        var completed = _builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);
        _builder.Assign(open, person);
        var oneMovable = _builder.Build();
        Assert.Empty(ScenarioSearch.Recommend(oneMovable, Budget, Ct));
        Assert.Contains(completed, ExecutionOrders.Current(oneMovable));

        var other = new PlanningSnapshotBuilder();
        var otherTeam = other.AddTeam();
        var otherPerson = other.AddPerson(otherTeam, 8m);
        var first = other.AddDemand(Day.Monday, [(otherTeam, 16m)], priorityRank: 1, executionRank: 2);
        var second = other.AddDemand(Day.Monday, [(otherTeam, 16m)], priorityRank: 2, executionRank: 1);
        other.Assign(first, otherPerson).Assign(second, otherPerson).Mode(ExecutionOrderMode.Custom);
        Assert.Empty(ScenarioSearch.Recommend(other.Build(), 0, Ct)); // havia melhoria, mas sem orçamento nada se avalia
    }

    [Fact]
    public void TheClimb_ReusesTheNeighborsAlreadyEvaluated_WithoutSpendingBudget()
    {
        ReversedOnOnePerson();

        var outcome = ScenarioSearch.RecommendWithStats(_builder.Build(), Budget, Ct);

        Assert.True(outcome.CacheHits > 0, "A subida deveria reaproveitar vizinhos já avaliados na fase 1.");
        Assert.True(outcome.Evaluations <= Budget);
    }

    [Fact]
    public void WhenTheBudgetRunsOut_ReturnsTheBestSoFar_AndAlwaysStopsAtTheSamePoint()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        for (var index = 1; index <= 10; index++)
        {
            var demand = _builder.AddDemand(Day.Monday, [(team, 8m * (11 - index))], priorityRank: index, executionRank: 11 - index);
            _builder.Assign(demand, person);
        }

        var snapshot = _builder.Mode(ExecutionOrderMode.Custom).Build();

        var first = ScenarioSearch.RecommendWithStats(snapshot, 5, Ct);
        var second = ScenarioSearch.RecommendWithStats(snapshot, 5, Ct);

        Assert.Equal(5, first.Evaluations);
        Assert.Equal(first.Evaluations, second.Evaluations);
        Assert.Equal(JsonSerializer.Serialize(first.Scenarios), JsonSerializer.Serialize(second.Scenarios));
    }

    [Fact]
    public void ACompletedDemand_KeepsItsPosition_InEveryRecommendation()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 1, executionRank: 4);
        var completed = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 2, executionRank: 3, completed: true);
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 3, executionRank: 2);
        var c = _builder.AddDemand(Day.Monday, [(team, 16m)], priorityRank: 4, executionRank: 1);
        _builder.Assign(a, person).Assign(b, person).Assign(c, person).Mode(ExecutionOrderMode.Custom);
        var snapshot = _builder.Build();
        var currentIndex = ExecutionOrders.Current(snapshot).ToList().IndexOf(completed);

        var scenarios = ScenarioSearch.Recommend(snapshot, Budget, Ct);

        Assert.NotEmpty(scenarios);
        Assert.All(scenarios, scenario => Assert.Equal(completed, scenario.ExecutionOrder[currentIndex]));
    }

    [Fact]
    public void AScenarioThatMakesADemandOverflowTheHorizon_IsDiscarded()
    {
        // Cada demanda de 1.000 h cabe em cinco anos com 1 h por dia, mas as duas juntas não: trocar a ordem
        // troca quem não termina, e esse cenário não é comparável.
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 1m);
        var x = _builder.AddDemand(Day.Monday, [(team, 1000m)]);
        var y = _builder.AddDemand(Day.Monday, [(team, 1000m)]);
        _builder.Assign(x, person).Assign(y, person);

        var outcome = ScenarioSearch.RecommendWithStats(_builder.Build(), Budget, Ct);

        Assert.Empty(outcome.Scenarios);
        Assert.True(outcome.Evaluations >= 1);
    }

    [Fact]
    public void ACancelledToken_StopsTheSearch()
    {
        ReversedOnOnePerson();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => ScenarioSearch.Recommend(_builder.Build(), Budget, cancellation.Token));
    }

    [Fact]
    public void TheRecommendation_IsTheSame_TwiceAndWithTheListsOfTheSnapshotShuffled()
    {
        var withScenarios = 0;
        for (var seed = 1; seed <= 80; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);

            var first = JsonSerializer.Serialize(ScenarioSearch.Recommend(snapshot, 60, Ct));
            var again = JsonSerializer.Serialize(ScenarioSearch.Recommend(snapshot, 60, Ct));
            var shuffled = JsonSerializer.Serialize(ScenarioSearch.Recommend(RandomPlanningGenerator.Shuffled(snapshot, seed + 700), 60, Ct));

            Assert.True(first == again, $"A recomendação mudou entre duas execuções na semente {seed}.");
            Assert.True(first == shuffled, $"A recomendação mudou com as listas embaralhadas na semente {seed}.");
            withScenarios += first == "[]" ? 0 : 1;
        }

        Assert.True(withScenarios > 5, $"Poucos planejamentos com recomendação: {withScenarios}.");
    }

    [Fact]
    public void TheLeanEvaluation_AgreesWithTheFullOne_ExceptForTheItems()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var context = new ScenarioContext(snapshot);
            var random = new Random(seed);
            var order = context.CurrentOrder.OrderBy(_ => random.Next()).ToList();

            var lean = context.EvaluateLean(order);
            var full = context.Evaluate(order);

            Assert.Empty(lean.Items);
            Assert.Equal(full.Score, lean.Score);
            Assert.Equal(full.SectorEndDate, lean.SectorEndDate);
            Assert.Equal(full.SectorDeltaDays, lean.SectorDeltaDays);
            Assert.Equal(full.IsComparable, lean.IsComparable);
        }
    }
}
