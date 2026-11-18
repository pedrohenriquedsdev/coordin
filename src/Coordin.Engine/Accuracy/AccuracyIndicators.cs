namespace Coordin.Engine.Accuracy;

/// <summary>
/// Os indicadores de precisão de um conjunto de demandas concluídas (RF-23). Cada média vem nula quando nenhuma
/// demanda entrou nela. Os contadores dizem quantas demandas ficaram de fora e por qual motivo.
/// </summary>
public sealed record AccuracyIndicators(
    int UsedCount,
    decimal? AverageDeviationFromBaselineDays,
    decimal? AverageDeviationFromCompletionForecastDays,
    decimal? OnTimePercentage,
    decimal? AverageEstimateDifferencePercent,
    int ExcludedWithoutSnapshot,
    int ExcludedWithoutBaseline,
    int ExcludedWithoutActualEffort,
    int ExcludedWithoutEstimate);

/// <summary>Os indicadores de um time, com a marca de quando o desvio entre estimado e real passa do setor.</summary>
public sealed record TeamAccuracy(Guid TeamId, AccuracyIndicators Indicators, bool IsAboveSector);

/// <summary>O resultado do período: o setor e cada time que aparece nas demandas usadas.</summary>
public sealed record AccuracyResult(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    AccuracyIndicators Sector,
    IReadOnlyList<TeamAccuracy> Teams);
