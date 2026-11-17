using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>
/// A ordem de execução das demandas (DT-13): quem entra primeiro na fila de cada pessoa. O motor só lê
/// a ordem; mudar a ordem é sempre um cenário que o gestor aprova (regras invioláveis 2 e 3).
/// </summary>
public static class ExecutionOrders
{
    /// <summary>
    /// Os ids de <b>todas</b> as demandas na ordem de execução vigente: o ranking do modo (prioridade em
    /// <see cref="ExecutionOrderMode.FollowPriority"/>, ordem de execução em <see cref="ExecutionOrderMode.Custom"/>),
    /// com desempate pelo id para o resultado nunca depender da ordem das listas (RNF-02).
    /// </summary>
    public static IReadOnlyList<Guid> Current(PlanningSnapshot snapshot) =>
        [.. CurrentDemands(snapshot).Select(demand => demand.Id)];

    /// <summary>As mesmas demandas de <see cref="Current"/>, como objetos; é a fila que o cálculo usa.</summary>
    internal static List<DemandInput> CurrentDemands(PlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return [.. snapshot.Demands
            .OrderBy(demand => snapshot.OrderMode == ExecutionOrderMode.FollowPriority ? demand.PriorityRank : demand.ExecutionRank)
            .ThenBy(demand => demand.Id)];
    }

    /// <summary>
    /// O snapshot em modo <see cref="ExecutionOrderMode.Custom"/>, com o <c>ExecutionRank</c> de cada
    /// demanda igual à sua posição em <paramref name="order"/> (a partir de 1). A prioridade e o snapshot
    /// original não são alterados.
    /// </summary>
    /// <exception cref="ArgumentException">A lista não tem todas as demandas, uma única vez cada.</exception>
    public static PlanningSnapshot WithOrder(PlanningSnapshot snapshot, IReadOnlyList<Guid> order)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(order);

        var positionOf = new Dictionary<Guid, int>();
        for (var index = 0; index < order.Count; index++)
        {
            if (!positionOf.TryAdd(order[index], index + 1))
            {
                throw new ArgumentException("A ordem repete uma demanda.", nameof(order));
            }
        }

        if (order.Count != snapshot.Demands.Count || snapshot.Demands.Any(demand => !positionOf.ContainsKey(demand.Id)))
        {
            throw new ArgumentException("A ordem precisa conter todas as demandas, uma única vez cada.", nameof(order));
        }

        return snapshot with
        {
            OrderMode = ExecutionOrderMode.Custom,
            Demands = [.. snapshot.Demands.Select(demand => demand with { ExecutionRank = positionOf[demand.Id] })],
        };
    }
}
