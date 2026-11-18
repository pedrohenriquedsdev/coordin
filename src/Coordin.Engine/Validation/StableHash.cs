namespace Coordin.Engine.Validation;

/// <summary>
/// Hash FNV-1a de 64 bits de um identificador (DT-37). Lê os bytes do <see cref="Guid"/> sempre na mesma ordem
/// (big-endian), para o valor ser o mesmo em qualquer máquina e em qualquer versão do .NET.
/// </summary>
internal static class StableHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Of(Guid id)
    {
        var hash = OffsetBasis;
        foreach (var source in id.ToByteArray(bigEndian: true))
        {
            hash ^= source;
            hash *= Prime;
        }

        return hash;
    }
}
