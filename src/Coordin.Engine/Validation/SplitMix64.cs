namespace Coordin.Engine.Validation;

/// <summary>
/// Gerador de números de SplitMix64 (DT-37; RNF-02). Uma semente gera sempre a mesma sequência, em qualquer versão
/// do .NET, o que a classe de números aleatórios do framework não garante. Sem estado global e sem relógio.
/// </summary>
internal sealed class SplitMix64
{
    private const ulong Increment = 0x9E3779B97F4A7C15UL;
    private const ulong MixA = 0xBF58476D1CE4E5B9UL;
    private const ulong MixB = 0x94D049BB133111EBUL;
    private const double TwoToTheFiftyThird = 9007199254740992.0;

    private ulong _state;

    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    /// <summary>O próximo valor de 64 bits da sequência.</summary>
    public ulong NextUInt64()
    {
        _state += Increment;
        var z = _state;
        z = (z ^ (z >> 30)) * MixA;
        z = (z ^ (z >> 27)) * MixB;
        return z ^ (z >> 31);
    }

    /// <summary>O próximo número em [0, 1), com 53 bits de precisão.</summary>
    public double NextDouble() => (NextUInt64() >> 11) / TwoToTheFiftyThird;
}
