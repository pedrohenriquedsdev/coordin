using System.Diagnostics;
using System.Text.Json;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>
/// RNF-01: com 15 times, 150 pessoas e 200 demandas, os cenários recomendados saem em até 10 segundos.
/// O orçamento de avaliações (<see cref="ScenarioSearch.EvaluationBudget"/>) foi escolhido por medição para
/// caber com folga; este teste trava o limite do requisito e confere que o resultado é sempre o mesmo.
/// Tem a categoria <c>Performance</c> porque a coleta de cobertura instrumenta o código e o deixa uma ordem
/// de grandeza mais lento: o passo de cobertura do CI o ignora, e um passo próprio o roda sem cobertura
/// (TDD 8.6, "Só desempenho").
/// </summary>
[Trait("Category", "Performance")]
public class ScenarioSearchPerformanceTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact]
    public void TheRecommendation_OnTheReferencePlanning_FinishesInUnderTenSeconds_AndIsAlwaysTheSame()
    {
        var snapshot = ReferencePlanning.Build();
        var results = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            var clock = Stopwatch.StartNew();
            var outcome = ScenarioSearch.RecommendWithStats(snapshot, ScenarioSearch.EvaluationBudget, TestContext.Current.CancellationToken);
            clock.Stop();

            Assert.True(clock.Elapsed < Limit, $"A recomendação levou {clock.Elapsed.TotalSeconds:F1} s (limite {Limit.TotalSeconds:F0} s).");
            Assert.Equal(ScenarioSearch.EvaluationBudget, outcome.Evaluations); // o planejamento de referência esgota o orçamento
            Assert.NotEmpty(outcome.Scenarios);
            results.Add(JsonSerializer.Serialize(outcome.Scenarios));
        }

        Assert.Equal(results[0], results[1]);
    }
}
