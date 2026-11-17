namespace Coordin.Engine.Planning;

/// <summary>
/// O grafo de dependências entre demandas (RF-18; DT-23). Cada aresta aponta da demanda para o seu
/// pré-requisito. Serve para descobrir ciclos (A depende de B, que depende de A), que tornariam a
/// previsão impossível: nenhuma das duas poderia começar primeiro.
/// </summary>
public static class DependencyGraph
{
    private enum Mark
    {
        /// <summary>Ainda não visitado.</summary>
        Unvisited,

        /// <summary>Está na pilha da busca: voltar a ele fecha um ciclo.</summary>
        InProgress,

        /// <summary>Já explorado por inteiro e sem ciclo.</summary>
        Done,
    }

    /// <summary>
    /// Procura um ciclo com busca em profundidade e marcação em três estados. Raízes e vizinhos são
    /// percorridos em ordem de <see cref="Guid"/>, então o mesmo grafo sempre devolve o mesmo ciclo,
    /// seja qual for a ordem das listas (RNF-02). Arestas que citam uma demanda fora de
    /// <paramref name="demandIds"/> são ignoradas: conferir referências é papel do validador.
    /// </summary>
    /// <returns>
    /// Os ids do ciclo na ordem em que se encadeiam, com o primeiro repetido no fim (A, B, A); uma
    /// demanda que depende dela mesma devolve (A, A). <c>null</c> se não há ciclo.
    /// </returns>
    public static IReadOnlyList<Guid>? FindCycle(IEnumerable<Guid> demandIds, IEnumerable<DependencyInput> dependencies)
    {
        ArgumentNullException.ThrowIfNull(demandIds);
        ArgumentNullException.ThrowIfNull(dependencies);

        var ids = demandIds.ToHashSet();
        var prerequisitesOf = dependencies
            .Where(edge => ids.Contains(edge.DemandId) && ids.Contains(edge.PrerequisiteId))
            .GroupBy(edge => edge.DemandId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(edge => edge.PrerequisiteId).Distinct().Order().ToList());

        var marks = ids.ToDictionary(id => id, _ => Mark.Unvisited);
        var path = new List<Guid>();

        foreach (var root in ids.Order())
        {
            if (marks[root] == Mark.Unvisited && Visit(root, prerequisitesOf, marks, path) is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }

    private static List<Guid>? Visit(
        Guid demand,
        Dictionary<Guid, List<Guid>> prerequisitesOf,
        Dictionary<Guid, Mark> marks,
        List<Guid> path)
    {
        marks[demand] = Mark.InProgress;
        path.Add(demand);

        if (prerequisitesOf.TryGetValue(demand, out var prerequisites))
        {
            foreach (var prerequisite in prerequisites)
            {
                if (marks[prerequisite] == Mark.InProgress)
                {
                    // A pilha tem o caminho até aqui; do prerequisite em diante é o ciclo.
                    var cycle = path.Skip(path.IndexOf(prerequisite)).ToList();
                    cycle.Add(prerequisite);
                    return cycle;
                }

                if (marks[prerequisite] == Mark.Unvisited && Visit(prerequisite, prerequisitesOf, marks, path) is { } found)
                {
                    return found;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        marks[demand] = Mark.Done;
        return null;
    }
}
