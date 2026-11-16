using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Recalcula com o motor e grava as previsões (DT-03, DT-14, DT-27). Não abre transação: quem chama
/// (<see cref="PlanningTransaction"/>) é responsável por isso, para que a escrita e o recálculo sejam
/// atômicos. Uma demanda concluída mantém a previsão que tinha (congelada), e a linha de base é gravada
/// uma única vez, no primeiro cálculo em que a demanda é previsível.
/// </summary>
public sealed class ForecastRecalculator(PlanningSnapshotLoader loader, TimeProvider clock)
{
    public async Task RecalculateAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var result = ForecastEngine.Calculate(snapshot);
        var stored = (await db.Forecasts.ToListAsync(cancellationToken)).ToDictionary(forecast => forecast.DemandId);
        var calculatedAt = clock.GetUtcNow();

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

        await db.SaveChangesAsync(cancellationToken);
    }
}
