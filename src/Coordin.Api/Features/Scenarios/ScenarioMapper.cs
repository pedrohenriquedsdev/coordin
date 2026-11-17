using Motor = Coordin.Engine.Scenarios;

namespace Coordin.Api.Features.Scenarios;

/// <summary>
/// Converte um cenário do motor na resposta da API, juntando o título de cada demanda (lido do banco).
/// Nenhuma regra de cálculo mora aqui (DT-02): só a forma da resposta.
/// </summary>
internal static class ScenarioMapper
{
    public static ScenarioResponse ToResponse(Motor.Scenario scenario, IReadOnlyDictionary<Guid, string> titles) =>
        new(
            scenario.ExecutionOrder,
            [.. scenario.Items.Select(item => new ScenarioItemResponse(
                item.DemandId,
                titles.GetValueOrDefault(item.DemandId, string.Empty),
                item.PriorityRank,
                item.NewExecutionRank,
                item.CurrentForecastEndDate,
                item.ForecastStartDate,
                item.ForecastEndDate,
                item.DeltaDays,
                item.Weight,
                item.ScoreContributionDelta))],
            scenario.SectorEndDate,
            scenario.SectorDeltaDays,
            new ScenarioScoreResponse(scenario.Score.Current, scenario.Score.Scenario, scenario.Score.Delta),
            scenario.IsComparable);
}
