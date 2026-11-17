namespace Coordin.Engine.Scenarios;

/// <summary>
/// A vizinhança da busca local (DT-26): as ordens que se obtêm movendo <b>uma</b> demanda de 1 a 3
/// posições para cima ou para baixo. É gerada sempre na mesma ordem, para a busca ser determinística
/// (RNF-02): para cada demanda na ordem recebida, as distâncias 1, 2 e 3, subindo antes de descer.
/// </summary>
internal static class NeighborhoodGenerator
{
    private const int MaxDistance = 3;

    /// <summary>
    /// Os vizinhos de <paramref name="order"/>, sem repetir sequências (mover B uma posição para cima dá a
    /// mesma ordem que mover A uma posição para baixo, e só aparece uma vez) e sem incluir a própria ordem.
    /// </summary>
    public static IEnumerable<IReadOnlyList<Guid>> Moves(IReadOnlyList<Guid> order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var seen = new HashSet<OrderKey> { new(order) };
        for (var index = 0; index < order.Count; index++)
        {
            for (var distance = 1; distance <= MaxDistance; distance++)
            {
                foreach (var target in new[] { index - distance, index + distance })
                {
                    if (target < 0 || target >= order.Count)
                    {
                        continue;
                    }

                    var neighbor = MoveTo(order, index, target);
                    if (seen.Add(new OrderKey(neighbor)))
                    {
                        yield return neighbor;
                    }
                }
            }
        }
    }

    /// <summary>A ordem com o elemento de <paramref name="from"/> levado para <paramref name="to"/>.</summary>
    private static Guid[] MoveTo(IReadOnlyList<Guid> order, int from, int to)
    {
        var list = new List<Guid>(order);
        var moved = list[from];
        list.RemoveAt(from);
        list.Insert(to, moved);
        return [.. list];
    }
}
