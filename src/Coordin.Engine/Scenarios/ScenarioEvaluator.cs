using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// Avalia uma ordem de execução contra o cronograma atual e calcula o score do DT-25 (RF-13, RF-14).
/// Função pura: não altera o snapshot, não grava nada e nunca decide uma ordem sozinha (regra inviolável 2).
/// Usa o mesmo caminho de cálculo do <see cref="ForecastEngine"/>, então não pode divergir dele. Para avaliar
/// muitas ordens do mesmo planejamento, a busca usa o <see cref="ScenarioContext"/> diretamente.
/// </summary>
public static class ScenarioEvaluator
{
    /// <summary>Calcula o cronograma atual e o da ordem <paramref name="order"/> e compara os dois.</summary>
    /// <param name="snapshot">O planejamento vigente.</param>
    /// <param name="order">Os ids de todas as demandas, uma vez cada, na ordem de execução proposta.</param>
    /// <exception cref="ArgumentException">A ordem não tem todas as demandas, uma única vez cada.</exception>
    public static Scenario Evaluate(PlanningSnapshot snapshot, IReadOnlyList<Guid> order)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(order);

        return new ScenarioContext(snapshot).Evaluate(order);
    }
}
