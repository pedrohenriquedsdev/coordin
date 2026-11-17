using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>
/// Regras que valem para toda recomendação, em planejamentos gerados com semente fixa e com dependências
/// (DT-34, RNF-02, RNF-08; regras invioláveis 2 e 3; RF-18). Orçamento pequeno: o que se verifica é a
/// forma e a segurança do resultado, não a qualidade da busca.
/// </summary>
public class ScenarioInvariantTests
{
    private const int Snapshots = 400;
    private const int Budget = 40;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<(int Seed, PlanningSnapshot Snapshot, IReadOnlyList<Scenario> Scenarios)> Cases() =>
        Enumerable.Range(1, Snapshots).Select(seed =>
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            return (seed, snapshot, ScenarioSearch.Recommend(snapshot, Budget, Ct));
        });

    [Fact]
    public void Generator_ProducesRecommendations_InEnoughPlannings()
    {
        var withScenarios = Cases().Count(item => item.Scenarios.Count > 0);

        Assert.True(withScenarios > 20, $"Poucos planejamentos com recomendação: {withScenarios}.");
    }

    [Fact]
    public void EveryRecommendedScenario_IsBetterThanTheCurrent_AndComparable()
    {
        foreach (var (seed, _, scenarios) in Cases())
        {
            Assert.InRange(scenarios.Count, 0, 3);
            foreach (var scenario in scenarios)
            {
                Assert.True(scenario.Score.Delta < 0 && scenario.IsComparable, $"Cenário que não melhora o score na semente {seed}.");
            }
        }
    }

    [Fact]
    public void TheContributions_SumToTheScoreDelta_InEveryRecommendation()
    {
        foreach (var (seed, _, scenarios) in Cases())
        {
            foreach (var scenario in scenarios)
            {
                Assert.True(
                    scenario.Items.Sum(item => item.ScoreContributionDelta) == scenario.Score.Delta,
                    $"A soma das parcelas não fecha na semente {seed}.");
            }
        }
    }

    [Fact]
    public void NoRecommendedScenario_ViolatesADependency()
    {
        foreach (var (seed, snapshot, scenarios) in Cases())
        {
            foreach (var scenario in scenarios)
            {
                var result = ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, scenario.ExecutionOrder));

                Assert.True(DependencyCheck.Violations(snapshot, result).Count == 0, $"Violação de dependência na semente {seed}.");
            }
        }
    }

    [Fact]
    public void TheOrderOfEachScenario_IsACompletePermutation_WithTheFixedDemandsInPlace()
    {
        foreach (var (seed, snapshot, scenarios) in Cases())
        {
            var current = ExecutionOrders.Current(snapshot);
            var movable = ExecutionOrders.Movable(snapshot, current).ToHashSet();

            foreach (var scenario in scenarios)
            {
                Assert.True(scenario.ExecutionOrder.Order().SequenceEqual(current.Order()), $"A ordem não é uma permutação completa na semente {seed}.");
                for (var index = 0; index < current.Count; index++)
                {
                    if (!movable.Contains(current[index]))
                    {
                        Assert.True(scenario.ExecutionOrder[index] == current[index], $"Uma demanda fixa mudou de posição na semente {seed}.");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheSearch_NeverChangesTheSnapshot_NorThePriorities()
    {
        for (var seed = 1; seed <= Snapshots; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var before = JsonSerializer.Serialize(snapshot);

            ScenarioSearch.Recommend(snapshot, Budget, Ct);

            Assert.True(before == JsonSerializer.Serialize(snapshot), $"A busca alterou o snapshot na semente {seed}.");
        }
    }

    [Fact]
    public void TheRecommendation_DoesNotDependOnTheOrderOfTheInputLists()
    {
        foreach (var (seed, snapshot, scenarios) in Cases())
        {
            var shuffled = ScenarioSearch.Recommend(RandomPlanningGenerator.Shuffled(snapshot, seed + 5000), Budget, Ct);

            Assert.True(
                JsonSerializer.Serialize(scenarios) == JsonSerializer.Serialize(shuffled),
                $"A recomendação mudou com as listas embaralhadas na semente {seed}.");
        }
    }
}
