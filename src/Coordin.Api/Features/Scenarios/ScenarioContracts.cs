using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Features.Scenarios;

/// <summary>Corpo de <c>POST /api/scenarios/simulate</c> e <c>POST /api/scenarios/apply</c>: a ordem de execução proposta.</summary>
public sealed record ScenarioOrderRequest(
    [property: Required(ErrorMessage = "A ordem de execução é obrigatória.")]
    List<Guid>? ExecutionOrder);

/// <summary>Um cenário de sequenciamento (RF-13 a RF-15; TDD 5.3). Números e datas no formato da API.</summary>
public sealed record ScenarioResponse(
    IReadOnlyList<Guid> ExecutionOrder,
    IReadOnlyList<ScenarioItemResponse> Items,
    DateOnly? SectorEndDate,
    int SectorDeltaDays,
    ScenarioScoreResponse Score,
    bool IsComparable);

/// <summary>Uma demanda no cenário, com a prioridade registrada ao lado da nova posição (RF-13).</summary>
public sealed record ScenarioItemResponse(
    Guid DemandId,
    string Title,
    int PriorityRank,
    int NewExecutionRank,
    DateOnly CurrentForecastEndDate,
    DateOnly ForecastStartDate,
    DateOnly ForecastEndDate,
    int DeltaDays,
    int Weight,
    long ScoreContributionDelta);

/// <summary>O score do DT-25 no cronograma atual e no cenário; menor é melhor.</summary>
public sealed record ScenarioScoreResponse(long Current, long Scenario, long Delta);
