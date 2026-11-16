using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Planning;

/// <summary>Cronograma consolidado (RF-11): as demandas na ordem de execução, com os dados já calculados.</summary>
public sealed record ScheduleResponse(ExecutionOrderMode ExecutionOrderMode, IReadOnlyList<ScheduleItemResponse> Items);

/// <summary>
/// Uma demanda no cronograma. As datas previstas vêm da previsão gravada (DT-03). Os desvios são em
/// dias corridos, positivos quando houve atraso (RF-12; DT-27), e só existem para demandas concluídas.
/// </summary>
public sealed record ScheduleItemResponse(
    Guid DemandId,
    string Title,
    int PriorityRank,
    int ExecutionRank,
    bool IsOutOfPriorityOrder,
    IReadOnlyList<ScheduleTeamResponse> Teams,
    decimal TotalEstimatedEffortHours,
    DateOnly? StartDate,
    bool IsPredictable,
    string? UnpredictableReason,
    DateOnly? ForecastStartDate,
    DateOnly? EndDateWithoutMargin,
    DateOnly? ForecastEndDate,
    DateOnly? InformedEndDate,
    bool ExceedsInformedEndDate,
    DateOnly? CompletedOn,
    DateOnly? BaselineEndDate,
    int? BaselineDeviationDays,
    int? ForecastDeviationDays);

/// <summary>Uma parte do cronograma: time, nome e estimativa (nula se o time ainda não estimou).</summary>
public sealed record ScheduleTeamResponse(Guid TeamId, string TeamName, decimal? EstimatedEffortHours);
