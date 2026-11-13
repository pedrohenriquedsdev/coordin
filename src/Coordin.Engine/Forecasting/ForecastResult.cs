namespace Coordin.Engine.Forecasting;

/// <summary>Situação de uma demanda no resultado do cálculo.</summary>
public enum DemandStatus
{
    /// <summary>Já concluída: sem previsão e sem consumo de capacidade.</summary>
    Completed,

    /// <summary>Tem datas previstas.</summary>
    Predictable,

    /// <summary>Sem datas previstas; o motivo vem em <see cref="DemandForecast.Reason"/> (RF-10).</summary>
    Unpredictable,
}

/// <summary>Por que uma demanda não é previsível; quando mais de um se aplica vale o primeiro (RF-10).</summary>
public enum UnpredictableReason
{
    /// <summary>Sem data de início.</summary>
    MissingStartDate,

    /// <summary>Algum time alocado ainda não estimou o esforço.</summary>
    MissingEstimate,

    /// <summary>Alguma parte sem pessoa alocada, só com capacidade zero, ou que não termina em 5 anos.</summary>
    NoCapacity,
}

/// <summary>
/// A previsão de uma demanda. As datas só existem quando <see cref="Status"/> é
/// <see cref="DemandStatus.Predictable"/>; a margem é a diferença entre as duas datas finais (RF-09).
/// </summary>
public sealed record DemandForecast(
    Guid DemandId,
    DemandStatus Status,
    UnpredictableReason? Reason,
    DateOnly? ForecastStartDate,
    DateOnly? EndDateWithoutMargin,
    int? MarginBusinessDays,
    DateOnly? ForecastEndDate);

/// <summary>Horas que uma pessoa dedicou, em um dia, à parte de um time em uma demanda.</summary>
public sealed record ConsumedHours(Guid PersonId, Guid DemandId, Guid TeamId, DateOnly Date, decimal Hours);

/// <summary>O resultado completo do cálculo: previsões por demanda e consumo diário por pessoa.</summary>
public sealed record ForecastResult(
    IReadOnlyList<DemandForecast> Demands,
    IReadOnlyList<ConsumedHours> ConsumedHours);
