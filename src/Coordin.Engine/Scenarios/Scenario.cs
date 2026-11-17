namespace Coordin.Engine.Scenarios;

/// <summary>
/// Uma ordem de execução avaliada contra o cronograma atual (RF-13, RF-14; DT-25). O título da demanda
/// não vive no motor: quem monta a resposta o junta.
/// </summary>
/// <param name="ExecutionOrder">Os ids de todas as demandas, na ordem de execução do cenário.</param>
/// <param name="Items">Uma linha por demanda previsível no cronograma atual e no cenário, na ordem do cenário.</param>
/// <param name="SectorEndDate">A maior data prevista, com margem, entre as demandas dos itens; <c>null</c> sem itens.</param>
/// <param name="SectorDeltaDays">Diferença em dias corridos do prazo total do setor contra o cronograma atual.</param>
/// <param name="Score">O critério do DT-25 no cronograma atual e no cenário; menor é melhor.</param>
/// <param name="IsComparable">
/// Falso quando o conjunto de demandas previsíveis do cenário difere do atual (uma demanda estoura o
/// horizonte de cinco anos nessa ordem); esse cenário nunca é recomendado.
/// </param>
public sealed record Scenario(
    IReadOnlyList<Guid> ExecutionOrder,
    IReadOnlyList<ScenarioItem> Items,
    DateOnly? SectorEndDate,
    int SectorDeltaDays,
    ScenarioScore Score,
    bool IsComparable);

/// <summary>A situação de uma demanda no cenário, ao lado da prioridade registrada (RF-13).</summary>
/// <param name="DemandId">A demanda.</param>
/// <param name="PriorityRank">A prioridade registrada, que o cenário nunca altera (regra inviolável 3).</param>
/// <param name="NewExecutionRank">A posição na ordem do cenário, a partir de 1.</param>
/// <param name="CurrentForecastEndDate">A data prevista, com margem, no cronograma atual.</param>
/// <param name="ForecastStartDate">O início previsto no cenário.</param>
/// <param name="ForecastEndDate">A data prevista, com margem, no cenário.</param>
/// <param name="DeltaDays">Cenário menos atual, em dias corridos (DT-27): positivo é atraso.</param>
/// <param name="Weight">O peso <c>wᵢ = n − PriorityRankᵢ + 1</c> do DT-25.</param>
/// <param name="ScoreContributionDelta">A parcela <c>wᵢ × dᵢ</c> no cenário menos a parcela no cronograma atual.</param>
public sealed record ScenarioItem(
    Guid DemandId,
    int PriorityRank,
    int NewExecutionRank,
    DateOnly CurrentForecastEndDate,
    DateOnly ForecastStartDate,
    DateOnly ForecastEndDate,
    int DeltaDays,
    int Weight,
    long ScoreContributionDelta);

/// <summary>
/// O score do DT-25 (<c>Σ wᵢ × dᵢ</c>): <see cref="Current"/> no cronograma atual, <see cref="Scenario"/> no
/// cenário e <see cref="Delta"/> igual à diferença, que é a soma das parcelas dos itens.
/// </summary>
public sealed record ScenarioScore(long Current, long Scenario, long Delta);
