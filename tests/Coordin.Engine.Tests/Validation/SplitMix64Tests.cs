using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>O gerador e o hash da validação: os mesmos valores em qualquer máquina (RNF-02).</summary>
public sealed class SplitMix64Tests
{
    [Fact]
    public void Sequence_for_seed_42_matches_the_reference_values()
    {
        // Valores calculados por uma implementação independente de SplitMix64 (Python).
        var generator = new SplitMix64(42UL);

        Assert.Equal(13679457532755275413UL, generator.NextUInt64());
        Assert.Equal(2949826092126892291UL, generator.NextUInt64());
        Assert.Equal(5139283748462763858UL, generator.NextUInt64());
    }

    [Fact]
    public void Same_seed_gives_the_same_sequence()
    {
        var first = new SplitMix64(7UL);
        var second = new SplitMix64(7UL);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void NextDouble_stays_in_zero_to_one_exclusive()
    {
        var generator = new SplitMix64(123UL);

        for (var i = 0; i < 2000; i++)
        {
            var value = generator.NextDouble();
            Assert.True(value >= 0d && value < 1d);
        }
    }

    [Fact]
    public void StableHash_of_a_known_guid_matches_the_reference_value()
    {
        // FNV-1a de 64 bits sobre os 16 bytes do Guid em ordem big-endian, calculado em Python.
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal(887287649562449685UL, StableHash.Of(id));
    }

    [Fact]
    public void StableHash_differs_for_different_guids()
    {
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(StableHash.Of(first), StableHash.Of(second));
    }
}
