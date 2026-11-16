using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Executa uma escrita que afeta o cálculo, com a regra de DT-03 em um lugar só: pega o lock do
/// planejamento, faz a escrita, e só se ela deu certo (resposta 2xx) recalcula e confirma. Qualquer
/// outro resultado, ou uma exceção, desfaz tudo: nem a escrita nem as previsões são gravadas.
/// </summary>
public static class PlanningTransaction
{
    public static async Task<IResult> RunAsync(
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        Func<Task<IResult>> write,
        CancellationToken cancellationToken)
    {
        // Sem Commit, a transação é desfeita quando o using termina.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PlanningLocks.Planning})", cancellationToken);

        var result = await write();
        if (!IsSuccess(result))
        {
            return result;
        }

        await recalculator.RecalculateAsync(db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool IsSuccess(IResult result) =>
        result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 };
}
