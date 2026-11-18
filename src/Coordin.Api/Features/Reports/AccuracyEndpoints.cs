using System.Globalization;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Accuracy;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Reports;

/// <summary>Os indicadores de precisão de um conjunto de demandas (RF-23). Médias nulas quando ninguém entrou nelas.</summary>
public sealed record AccuracyIndicatorsResponse(
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
public sealed record TeamAccuracyResponse(
    Guid TeamId,
    string? TeamName,
    AccuracyIndicatorsResponse Indicators,
    bool IsAboveSector);

/// <summary>O resultado do período: o setor e os times que aparecem nas demandas usadas.</summary>
public sealed record AccuracyResponse(
    DateOnly From,
    DateOnly To,
    AccuracyIndicatorsResponse Sector,
    IReadOnlyList<TeamAccuracyResponse> Teams);

/// <summary>
/// Indicadores de precisão (RF-23): lê as demandas concluídas e calcula no motor. Não grava nada. O período filtra
/// pela data de conclusão, com início e fim inclusive.
/// </summary>
public static class AccuracyEndpoints
{
    private const string DateFormat = "yyyy-MM-dd";

    public static async Task<IResult> GetAccuracyAsync(string? from, string? to, CoordinDbContext db, CancellationToken cancellationToken)
    {
        if (!TryParseDate(from, out var fromDate))
        {
            return ApiProblems.InvalidField("from", "Informe a data de início no formato aaaa-mm-dd.");
        }

        if (!TryParseDate(to, out var toDate))
        {
            return ApiProblems.InvalidField("to", "Informe a data de fim no formato aaaa-mm-dd.");
        }

        if (toDate < fromDate)
        {
            return ApiProblems.InvalidField("to", "A data de fim não pode ser anterior à data de início.");
        }

        var demands = await db.Demands.AsNoTracking()
            .Include(demand => demand.Teams)
            .Include(demand => demand.Forecast)
            .Where(demand => demand.CompletedOn != null)
            .ToListAsync(cancellationToken);

        var deliveries = demands.Select(demand => new CompletedDelivery(
            demand.Id,
            demand.CompletedOn!.Value,
            demand.Forecast?.BaselineEndDate,
            demand.ForecastEndDateAtCompletion,
            demand.EstimatedEffortHoursAtCompletion,
            demand.ActualEffortHours,
            demand.Teams.Select(part => part.TeamId).ToList())).ToList();

        var result = AccuracyCalculator.Calculate(deliveries, fromDate, toDate);

        var teamNames = await db.Teams.AsNoTracking().ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);
        var teams = result.Teams
            .OrderBy(team => teamNames.TryGetValue(team.TeamId, out var name) ? name.ToUpperInvariant() : "", StringComparer.Ordinal)
            .Select(team => new TeamAccuracyResponse(
                team.TeamId,
                teamNames.TryGetValue(team.TeamId, out var name) ? name : null,
                ToResponse(team.Indicators),
                team.IsAboveSector))
            .ToList();

        return Results.Ok(new AccuracyResponse(fromDate, toDate, ToResponse(result.Sector), teams));
    }

    private static AccuracyIndicatorsResponse ToResponse(AccuracyIndicators indicators) =>
        new(
            indicators.UsedCount,
            indicators.AverageDeviationFromBaselineDays,
            indicators.AverageDeviationFromCompletionForecastDays,
            indicators.OnTimePercentage,
            indicators.AverageEstimateDifferencePercent,
            indicators.ExcludedWithoutSnapshot,
            indicators.ExcludedWithoutBaseline,
            indicators.ExcludedWithoutActualEffort,
            indicators.ExcludedWithoutEstimate);

    private static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
