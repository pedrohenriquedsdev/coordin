using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// A busca local que recomenda cenários melhores que o cronograma atual (RF-13; DT-26): determinística e
/// com limite por <b>número de avaliações</b>, nunca por tempo de relógio, para o resultado não variar com
/// a máquina (RNF-02). Só recomenda: nada é gravado nem aplicado (regras invioláveis 2 e 3).
/// </summary>
public static class ScenarioSearch
{
    /// <summary>
    /// O orçamento de avaliações distintas da busca (design D6). O DT-26 previa 3.000, mas, medido com 15
    /// times, 150 pessoas e 200 demandas, cada avaliação custa cerca de 4 ms e 3.000 levariam cerca de 13 s,
    /// acima dos 10 s do RNF-01. Com 1.000, a recomendação de referência leva cerca de 4 s, uma folga de
    /// 2,4× para o CI, que pode ser mais lento (1.200 chegaria a cerca de 5 s, sem essa folga). Ver as
    /// <c>## Medições</c> do <c>design.md</c> da change <c>sequencing-scenarios</c>.
    /// </summary>
    public const int EvaluationBudget = 1000;

    private const int MaxRecommendations = 3;

    /// <summary>
    /// Até 3 cenários com <c>score</c> menor que o atual: a melhor ordem encontrada e os dois melhores
    /// movimentos únicos a partir da ordem atual, sem repetir sequências, do melhor para o pior; lista vazia
    /// quando nada melhora.
    /// </summary>
    /// <param name="snapshot">O planejamento vigente (não é alterado).</param>
    /// <param name="evaluationBudget">Quantas ordens distintas, no máximo, podem ser avaliadas.</param>
    /// <param name="cancellationToken">Cancela a busca entre duas avaliações; não afeta o resultado quando não cancela.</param>
    /// <exception cref="OperationCanceledException">A busca foi cancelada.</exception>
    public static IReadOnlyList<Scenario> Recommend(
        PlanningSnapshot snapshot,
        int evaluationBudget,
        CancellationToken cancellationToken = default) =>
        RecommendWithStats(snapshot, evaluationBudget, cancellationToken).Scenarios;

    /// <summary>O resultado da busca com as contagens que os testes conferem.</summary>
    /// <param name="Scenarios">As recomendações.</param>
    /// <param name="Evaluations">Quantas ordens distintas foram avaliadas (o que o orçamento limita).</param>
    /// <param name="CacheHits">Quantas vezes uma ordem já avaliada foi reaproveitada sem gastar orçamento.</param>
    internal sealed record SearchOutcome(IReadOnlyList<Scenario> Scenarios, int Evaluations, int CacheHits);

    internal static SearchOutcome RecommendWithStats(
        PlanningSnapshot snapshot,
        int evaluationBudget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var context = new ScenarioContext(snapshot);
        var currentOrder = context.CurrentOrder;
        var movable = ExecutionOrders.Movable(snapshot, currentOrder);
        if (movable.Count < 2 || evaluationBudget <= 0)
        {
            return new SearchOutcome([], 0, 0);
        }

        var cache = new Dictionary<OrderKey, Scenario>();
        var evaluations = 0;
        var hits = 0;

        // Devolve o cenário da lista movível, do cache ou calculado; nulo se o orçamento (até o limite
        // pedido) já acabou. A checagem do cancelamento fica aqui, uma vez por consulta.
        Scenario? Lookup(IReadOnlyList<Guid> movableOrder, int limit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = new OrderKey(movableOrder);
            if (cache.TryGetValue(key, out var known))
            {
                hits++;
                return known;
            }

            if (evaluations >= limit)
            {
                return null;
            }

            var scenario = context.EvaluateLean(ExecutionOrders.Compose(currentOrder, movableOrder));
            evaluations++;
            cache[key] = scenario;
            return scenario;
        }

        static bool Improves(Scenario scenario) => scenario.IsComparable && scenario.Score.Delta < 0;

        // Fase 1: os vizinhos da ordem atual, no máximo metade do orçamento; os que melhoram são os "movimentos únicos".
        var singles = new List<(IReadOnlyList<Guid> Movable, Scenario Scenario)>();
        var phaseOneLimit = Math.Max(1, evaluationBudget / 2);
        foreach (var neighbor in NeighborhoodGenerator.Moves(movable))
        {
            var scenario = Lookup(neighbor, phaseOneLimit);
            if (scenario is null)
            {
                break;
            }

            if (Improves(scenario))
            {
                singles.Add((neighbor, scenario));
            }
        }

        // Fase 2: subida de encosta com primeira melhoria a partir da ordem atual. Os vizinhos já avaliados
        // na fase 1 saem do cache e não gastam orçamento.
        var climbing = movable;
        Scenario? best = null;
        var exhausted = false;
        var improved = true;
        while (improved && !exhausted)
        {
            improved = false;
            foreach (var neighbor in NeighborhoodGenerator.Moves(climbing))
            {
                var scenario = Lookup(neighbor, evaluationBudget);
                if (scenario is null)
                {
                    exhausted = true;
                    break;
                }

                if (scenario.IsComparable && scenario.Score.Scenario < (best?.Score.Scenario ?? scenario.Score.Current))
                {
                    best = scenario;
                    climbing = neighbor;
                    improved = true;
                    break;
                }
            }
        }

        var comparer = new ScenarioComparer(currentOrder);
        var chosen = new List<(IReadOnlyList<Guid> Movable, Scenario Scenario)>();
        if (best is not null)
        {
            chosen.Add((climbing, best));
        }

        var chosenKeys = chosen.Select(item => new OrderKey(item.Movable)).ToHashSet();
        foreach (var single in singles.OrderBy(item => item.Scenario, comparer).Take(2))
        {
            if (chosenKeys.Add(new OrderKey(single.Movable)))
            {
                chosen.Add(single);
            }
        }

        var recommendations = chosen
            .OrderBy(item => item.Scenario, comparer)
            .Take(MaxRecommendations)
            .Select(item => context.Evaluate(ExecutionOrders.Compose(currentOrder, item.Movable)))
            .ToList();

        return new SearchOutcome(recommendations, evaluations, hits);
    }
}
