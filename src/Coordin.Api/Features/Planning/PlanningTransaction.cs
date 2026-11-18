using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Executa uma escrita que afeta o cálculo, com a regra de DT-03 em um lugar só: pega o lock do planejamento, faz a
/// escrita, e só se ela deu certo (resposta 2xx) recalcula, registra o histórico e confirma. Qualquer outro resultado,
/// ou uma exceção, desfaz tudo: nem a escrita, nem as previsões, nem o histórico são gravados.
/// </summary>
public static class PlanningTransaction
{
    /// <summary>A escrita tem uma causa fixa (a maioria delas).</summary>
    public static Task<IResult> RunAsync(
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        ForecastChangeCause cause,
        Func<Task<IResult>> write,
        CancellationToken cancellationToken) =>
        RunAsync(db, recalculator, () => cause, write, cancellationToken);

    /// <summary>
    /// A causa só se conhece depois da escrita (a atualização de uma demanda pode mudar estimativas ou dependências).
    /// <paramref name="afterRecalculation"/> roda depois do recálculo, ainda dentro da transação: é onde a conclusão
    /// grava a previsão vigente (RF-22).
    /// </summary>
    public static async Task<IResult> RunAsync(
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        Func<ForecastChangeCause> cause,
        Func<Task<IResult>> write,
        CancellationToken cancellationToken,
        Func<Task>? afterRecalculation = null)
    {
        // Sem Commit, a transação é desfeita quando o using termina.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PlanningLocks.Planning})", cancellationToken);

        var result = await write();
        if (!IsSuccess(result))
        {
            return result;
        }

        await recalculator.RecalculateAsync(db, cause(), cancellationToken);
        if (afterRecalculation is not null)
        {
            await afterRecalculation();
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool IsSuccess(IResult result) =>
        result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 };
}
