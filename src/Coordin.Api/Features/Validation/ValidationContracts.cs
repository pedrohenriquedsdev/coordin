namespace Coordin.Api.Features.Validation;

/// <summary>O estado da validação: o que já foi carregado e guardado (RF-24).</summary>
/// <param name="DemandCount">Quantas demandas há no planejamento.</param>
/// <param name="BaselineCount">Quantas têm data de linha de base.</param>
/// <param name="CurrentPlanCaptured">Se o planejamento do processo atual já foi guardado.</param>
/// <param name="CurrentPlanCapturedAt">Quando foi guardado; nulo enquanto não foi.</param>
public sealed record ValidationStatusResponse(
    int DemandCount,
    int BaselineCount,
    bool CurrentPlanCaptured,
    DateTimeOffset? CurrentPlanCapturedAt);
