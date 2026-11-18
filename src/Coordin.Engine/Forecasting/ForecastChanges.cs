namespace Coordin.Engine.Forecasting;

/// <summary>
/// O que pode ter mudado a data prevista de uma demanda (RF-21). Cada escrita que dispara o recálculo informa a sua
/// causa; o motor não a deduz.
/// </summary>
public enum ForecastChangeCause
{
    Estimate,
    Allocation,
    Capacity,
    Absence,
    Dependency,
    ApprovedScenario,
    NewDemand,
    /// <summary>A margem de segurança mudou (configuração, DT-22); fora da lista original de RF-21, a aprovar.</summary>
    SafetyMargin,
}

/// <summary>
/// Uma mudança na data prevista de uma demanda: a data anterior (nula na primeira previsão), a nova (nula quando a
/// demanda deixou de ser previsível) e a causa.
/// </summary>
public sealed record ForecastChange(
    Guid DemandId,
    DateOnly? PreviousEndDate,
    DateOnly? NewEndDate,
    ForecastChangeCause Cause);

/// <summary>
/// Compara a data prevista guardada antes do recálculo com a calculada agora, e devolve só o que mudou (RF-21;
/// DT-14). Função pura: a lista volta ordenada pelo identificador da demanda, para o resultado não depender da
/// ordem em que o banco devolveu as linhas.
/// </summary>
public static class ForecastChanges
{
    /// <param name="previous">A data prevista guardada por demanda; a demanda ausente é uma primeira previsão.</param>
    /// <param name="current">O resultado do cálculo atual.</param>
    /// <param name="cause">A causa da escrita que disparou o recálculo.</param>
    public static IReadOnlyList<ForecastChange> Between(
        IReadOnlyDictionary<Guid, DateOnly?> previous,
        ForecastResult current,
        ForecastChangeCause cause)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var changes = new List<ForecastChange>();
        foreach (var forecast in current.Demands)
        {
            // Demanda concluída mantém a previsão congelada (DT-27): não entra no histórico.
            if (forecast.Status == DemandStatus.Completed)
            {
                continue;
            }

            var before = previous.TryGetValue(forecast.DemandId, out var stored) ? stored : null;
            var after = forecast.ForecastEndDate;

            // Demanda sem previsão antes e sem previsão agora não é mudança.
            if (before is null && after is null)
            {
                continue;
            }

            if (before == after)
            {
                continue;
            }

            changes.Add(new ForecastChange(forecast.DemandId, before, after, cause));
        }

        return changes.OrderBy(change => change.DemandId).ToList();
    }
}
