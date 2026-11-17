using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// Confere o cronograma calculado contra as dependências (RF-18). O motor já faz a dependente esperar o
/// pré-requisito em qualquer ordem, então nenhum cenário deveria violar uma dependência; esta verificação
/// é a garantia explícita (defesa em profundidade) antes de gravar uma ordem aprovada.
/// </summary>
public static class DependencyCheck
{
    /// <summary>
    /// As dependências, entre demandas previsíveis, em que o início previsto da dependente não é posterior
    /// ao fim previsto, com margem, do pré-requisito. Vazia quando tudo está respeitado; em ordem estável.
    /// </summary>
    public static IReadOnlyList<DependencyInput> Violations(PlanningSnapshot snapshot, ForecastResult result)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(result);

        var forecasts = result.Demands
            .Where(forecast => forecast.Status == DemandStatus.Predictable)
            .ToDictionary(forecast => forecast.DemandId);

        return [.. snapshot.Dependencies
            .Where(dependency =>
                forecasts.TryGetValue(dependency.DemandId, out var dependent)
                && forecasts.TryGetValue(dependency.PrerequisiteId, out var prerequisite)
                && dependent.ForecastStartDate <= prerequisite.ForecastEndDate)
            .OrderBy(dependency => dependency.DemandId)
            .ThenBy(dependency => dependency.PrerequisiteId)];
    }
}
