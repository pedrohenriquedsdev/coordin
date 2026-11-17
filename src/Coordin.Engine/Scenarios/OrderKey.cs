namespace Coordin.Engine.Scenarios;

/// <summary>
/// A chave de uma sequência de ids, para guardar o resultado de cada sequência já avaliada (design D6). A
/// igualdade compara a sequência inteira, então duas sequências diferentes nunca se confundem; o código
/// de hash é calculado à mão, sem <see cref="HashCode"/> (que muda a cada execução), para o comportamento
/// ser o mesmo em toda máquina (RNF-02).
/// </summary>
internal readonly struct OrderKey : IEquatable<OrderKey>
{
    private readonly Guid[] _ids;
    private readonly int _hash;

    public OrderKey(IReadOnlyList<Guid> ids)
    {
        _ids = [.. ids];
        var hash = 17;
        foreach (var id in _ids)
        {
            hash = unchecked((hash * 31) + id.GetHashCode());
        }

        _hash = hash;
    }

    public bool Equals(OrderKey other) => _hash == other._hash && _ids.AsSpan().SequenceEqual(other._ids);

    public override bool Equals(object? obj) => obj is OrderKey other && Equals(other);

    public override int GetHashCode() => _hash;
}
