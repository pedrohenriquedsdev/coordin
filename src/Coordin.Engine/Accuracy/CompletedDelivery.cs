namespace Coordin.Engine.Accuracy;

/// <summary>
/// Uma demanda concluída, com o que a conclusão gravou (RF-22) e a linha de base (RF-12). Os valores nulos são
/// os que a demanda não tem: uma demanda concluída antes desta change não tem as previsões e o esforço de conclusão.
/// </summary>
public sealed record CompletedDelivery(
    Guid DemandId,
    DateOnly CompletedOn,
    DateOnly? BaselineEndDate,
    DateOnly? ForecastEndDateAtCompletion,
    decimal? EstimatedEffortHoursAtCompletion,
    decimal? ActualEffortHours,
    IReadOnlyList<Guid> TeamIds);
