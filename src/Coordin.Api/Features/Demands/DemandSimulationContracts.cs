namespace Coordin.Api.Features.Demands;

/// <summary>
/// Resposta de <c>POST /api/demands/simulate</c> (RF-16; TDD 5.2): a previsão da nova demanda, a posição usada e o
/// impacto nas demais. Nada foi gravado. Datas no formato da API (<c>yyyy-MM-dd</c>).
/// </summary>
public sealed record DemandSimulationResponse(
    bool IsPredictable,
    string? UnpredictableReason,
    DateOnly? ForecastStartDate,
    DateOnly? EndDateWithoutMargin,
    DateOnly? ForecastEndDate,
    int Position,
    IReadOnlyList<DemandChangeResponse> Changes,
    DateOnly? SectorEndDate,
    int SectorDeltaDays);

/// <summary>Uma demanda existente cuja data prevista muda com a nova demanda, com o título para a tela (RF-16).</summary>
public sealed record DemandChangeResponse(
    Guid DemandId,
    string Title,
    DateOnly CurrentForecastEndDate,
    DateOnly NewForecastEndDate,
    int DeltaDays);
