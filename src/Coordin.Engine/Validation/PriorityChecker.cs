using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Validation;

/// <summary>Uma das demandas mais prioritárias, com as duas previsões e a diferença em dias corridos (positiva é atraso).</summary>
public sealed record PriorityCheckItem(
    Guid DemandId,
    int PriorityRank,
    DateOnly CurrentForecastEndDate,
    DateOnly CoordinForecastEndDate,
    int DeltaDays);

/// <summary>As demandas mais prioritárias e quantas delas pioraram com o Coordin (PRD 7.2).</summary>
public sealed record PriorityCheck(IReadOnlyList<PriorityCheckItem> Items, int WorsenedCount);

/// <summary>
/// Confere se o planejamento com o Coordin não piora o prazo das demandas mais prioritárias (PRD 7.2; regra 3).
/// Considera as 5 de menor prioridade registrada entre as que são previsíveis nos dois planejamentos.
/// </summary>
public static class PriorityChecker
{
    /// <summary>Quantas demandas entram na checagem.</summary>
    public const int PriorityCheckSize = 5;

    public static PriorityCheck Calculate(PlanningSnapshot currentProcess, ForecastResult currentForecast, ForecastResult coordinForecast)
    {
        var ranks = currentProcess.Demands.ToDictionary(demand => demand.Id, demand => demand.PriorityRank);
        var currentEnds = PredictableEnds(currentForecast);
        var coordinEnds = PredictableEnds(coordinForecast);

        var items = currentEnds.Keys
            .Where(coordinEnds.ContainsKey)
            .OrderBy(id => ranks[id])
            .ThenBy(id => id)
            .Take(PriorityCheckSize)
            .Select(id => new PriorityCheckItem(
                id,
                ranks[id],
                currentEnds[id],
                coordinEnds[id],
                coordinEnds[id].DayNumber - currentEnds[id].DayNumber))
            .ToList();

        return new PriorityCheck(items, items.Count(item => item.DeltaDays > 0));
    }

    private static Dictionary<Guid, DateOnly> PredictableEnds(ForecastResult result) =>
        result.Demands
            .Where(demand => demand.Status == DemandStatus.Predictable && demand.ForecastEndDate is not null)
            .ToDictionary(demand => demand.DemandId, demand => demand.ForecastEndDate!.Value);
}
