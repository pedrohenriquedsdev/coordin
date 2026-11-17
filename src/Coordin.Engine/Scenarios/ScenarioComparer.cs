namespace Coordin.Engine.Scenarios;

/// <summary>
/// Compara dois cenários do mesmo planejamento: o menor vem primeiro, ou seja, o melhor. Segue o DT-25:
/// o <c>score</c>, depois o prazo total do setor, depois o menor número de demandas deslocadas em relação
/// à ordem atual e, por último, a ordem lexicográfica da sequência de ids, que torna o desempate sempre o
/// mesmo, seja qual for a ordem das listas (RNF-02).
/// </summary>
/// <param name="currentOrder">A ordem de execução vigente, contra a qual se contam as demandas deslocadas.</param>
public sealed class ScenarioComparer(IReadOnlyList<Guid> currentOrder) : IComparer<Scenario>
{
    public int Compare(Scenario? x, Scenario? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var byScore = x.Score.Scenario.CompareTo(y.Score.Scenario);
        if (byScore != 0)
        {
            return byScore;
        }

        var bySector = Nullable.Compare(x.SectorEndDate, y.SectorEndDate);
        if (bySector != 0)
        {
            return bySector;
        }

        var byMoved = MovedCount(x).CompareTo(MovedCount(y));
        return byMoved != 0 ? byMoved : CompareSequences(x.ExecutionOrder, y.ExecutionOrder);
    }

    /// <summary>Quantas demandas estão em uma posição diferente da que têm na ordem atual.</summary>
    private int MovedCount(Scenario scenario)
    {
        var moved = 0;
        for (var index = 0; index < scenario.ExecutionOrder.Count; index++)
        {
            if (index >= currentOrder.Count || scenario.ExecutionOrder[index] != currentOrder[index])
            {
                moved++;
            }
        }

        return moved;
    }

    private static int CompareSequences(IReadOnlyList<Guid> first, IReadOnlyList<Guid> second)
    {
        var length = Math.Min(first.Count, second.Count);
        for (var index = 0; index < length; index++)
        {
            var byId = first[index].CompareTo(second[index]);
            if (byId != 0)
            {
                return byId;
            }
        }

        return first.Count.CompareTo(second.Count);
    }
}
