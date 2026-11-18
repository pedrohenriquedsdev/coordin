using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Demands;

/// <summary>Uma alteração de estimativa de um time, com o nome do time e o motivo (RF-20). Somente leitura.</summary>
public sealed record EstimateHistoryResponse(
    Guid TeamId,
    string? TeamName,
    decimal? PreviousHours,
    decimal? NewHours,
    string? Reason,
    DateTimeOffset ChangedAt);

/// <summary>Uma mudança na data prevista, com a causa (RF-21). Somente leitura.</summary>
public sealed record ForecastHistoryResponse(
    DateOnly? PreviousEndDate,
    DateOnly? NewEndDate,
    string Cause,
    DateTimeOffset ChangedAt);

/// <summary>
/// Consulta dos históricos de uma demanda (RF-20, RF-21; DT-14). Só lê: não há rota de edição nem de exclusão. As
/// listas saem em ordem cronológica, do registro mais antigo ao mais recente.
/// </summary>
public static class DemandHistoryEndpoints
{
    public static async Task<IResult> GetEstimateHistoryAsync(Guid id, CoordinDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Demands.AsNoTracking().AnyAsync(demand => demand.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var history = await db.EstimateHistories.AsNoTracking()
            .Where(change => change.DemandId == id)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);

        var teamNames = await db.Teams.AsNoTracking().ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);

        return Results.Ok(history.Select(change => new EstimateHistoryResponse(
            change.TeamId,
            teamNames.TryGetValue(change.TeamId, out var name) ? name : null,
            change.PreviousHours,
            change.NewHours,
            change.Reason,
            change.ChangedAt)).ToList());
    }

    public static async Task<IResult> GetForecastHistoryAsync(Guid id, CoordinDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Demands.AsNoTracking().AnyAsync(demand => demand.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var history = await db.ForecastHistories.AsNoTracking()
            .Where(change => change.DemandId == id)
            .OrderBy(change => change.ChangedAt)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(history.Select(change => new ForecastHistoryResponse(
            change.PreviousEndDate,
            change.NewEndDate,
            change.Cause,
            change.ChangedAt)).ToList());
    }
}
