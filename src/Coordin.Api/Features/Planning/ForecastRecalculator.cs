using Coordin.Api.Features.Demands;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Recalcula com o motor, grava as previsões e registra no histórico cada mudança de data prevista (RF-21; DT-03,
/// DT-14, DT-27). Não abre transação: quem chama (<see cref="PlanningTransaction"/>) é responsável por isso, para que
/// a escrita, o recálculo e o histórico sejam atômicos. Uma demanda concluída mantém a previsão que tinha (congelada),
/// e a linha de base é gravada uma única vez, no primeiro cálculo em que a demanda é previsível.
/// </summary>
public sealed class ForecastRecalculator(PlanningSnapshotLoader loader, TimeProvider clock)
{
    /// <param name="cause">A causa da escrita que disparou o recálculo; vai para cada registro do histórico.</param>
    public async Task RecalculateAsync(CoordinDbContext db, ForecastChangeCause cause, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var result = ForecastEngine.Calculate(snapshot);
        var stored = (await db.Forecasts.ToListAsync(cancellationToken)).ToDictionary(forecast => forecast.DemandId);
        var calculatedAt = clock.GetUtcNow();

        // A data guardada antes do recálculo é tirada aqui: as linhas são alteradas logo abaixo.
        var previous = stored.ToDictionary(pair => pair.Key, pair => pair.Value.ForecastEndDate);
        var changes = ForecastChanges.Between(previous, result, cause);

        foreach (var forecast in result.Demands)
        {
            if (forecast.Status == DemandStatus.Completed)
            {
                continue; // previsão congelada no valor da conclusão (DT-27)
            }

            if (!stored.TryGetValue(forecast.DemandId, out var row))
            {
                row = new Forecast { DemandId = forecast.DemandId };
                db.Forecasts.Add(row);
            }

            row.IsPredictable = forecast.Status == DemandStatus.Predictable;
            row.UnpredictableReason = forecast.Reason?.ToString();
            row.ForecastStartDate = forecast.ForecastStartDate;
            row.EndDateWithoutMargin = forecast.EndDateWithoutMargin;
            row.ForecastEndDate = forecast.ForecastEndDate;
            row.CalculatedAt = calculatedAt;

            // A primeira previsão vira a linha de base e nunca mais muda (RF-12).
            if (row.IsPredictable && row.BaselineEndDate is null)
            {
                row.BaselineEndDate = forecast.ForecastEndDate;
            }
        }

        foreach (var change in changes)
        {
            db.ForecastHistories.Add(new ForecastHistory
            {
                DemandId = change.DemandId,
                PreviousEndDate = change.PreviousEndDate,
                NewEndDate = change.NewEndDate,
                Cause = change.Cause.ToString(),
                ChangedAt = calculatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
