namespace Coordin.Api.Features.Planning;

/// <summary>
/// Previsão gravada de uma demanda (RF-08 a RF-10, RF-12; TDD 4.2). É recalculada pelo motor a cada
/// escrita que afeta o cálculo (DT-03). <see cref="BaselineEndDate"/> é a primeira previsão e nunca
/// muda (DT-14). Uma demanda concluída mantém a previsão que tinha na conclusão (DT-27).
/// </summary>
public sealed class Forecast
{
    public Guid DemandId { get; set; }

    public bool IsPredictable { get; set; }

    /// <summary>Motivo quando não previsível: <c>MissingStartDate</c>, <c>MissingEstimate</c> ou <c>NoCapacity</c>.</summary>
    public string? UnpredictableReason { get; set; }

    public DateOnly? ForecastStartDate { get; set; }

    public DateOnly? EndDateWithoutMargin { get; set; }

    public DateOnly? ForecastEndDate { get; set; }

    /// <summary>Primeira previsão calculada, gravada uma única vez (RF-12).</summary>
    public DateOnly? BaselineEndDate { get; set; }

    /// <summary>Instante do cálculo, em UTC.</summary>
    public DateTimeOffset CalculatedAt { get; set; }
}
