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

/// <summary>O corpo do carregamento do conjunto. Sem <see cref="ConfirmReplaceAll"/> verdadeiro, nada é gravado (DT-37).</summary>
public sealed record LoadDatasetRequest(bool ConfirmReplaceAll);

/// <summary>Uma demanda com a sua linha de base (nula enquanto o gestor não informou).</summary>
public sealed record BaselineResponse(Guid DemandId, string Title, DateOnly? BaselineDate);

/// <summary>Uma data de linha de base para uma demanda.</summary>
public sealed record BaselineItem(Guid DemandId, DateOnly BaselineDate);

/// <summary>A linha de base completa: substitui a anterior (PRD 7.3, passo 2).</summary>
public sealed record SaveBaselineRequest(IReadOnlyList<BaselineItem>? Baselines);

/// <summary>O planejamento do processo atual, guardado (PRD 7.3, passo 3).</summary>
public sealed record CurrentPlanResponse(DateTimeOffset CapturedAt);
