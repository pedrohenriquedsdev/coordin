using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>O fator de esforço por demanda do realizado simulado (PRD 7.3, passo 6; DT-37).</summary>
public sealed class EffortFactorsTests
{
    private static readonly ValidationParameters Range = new(0.9m, 1.1m, 20261126L);

    /// <summary>Identificadores fixos e distintos, para os testes não dependerem de Guid.NewGuid.</summary>
    private static Guid IdAt(int index) => new(index, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    [Fact]
    public void Same_seed_and_demand_give_the_same_factor()
    {
        var id = IdAt(5);

        Assert.Equal(EffortFactors.FactorFor(id, Range), EffortFactors.FactorFor(id, Range));
    }

    [Fact]
    public void The_order_of_the_demands_does_not_change_the_factors()
    {
        var ids = Enumerable.Range(1, 30).Select(IdAt).ToList();

        var inOrder = ids.ToDictionary(id => id, id => EffortFactors.FactorFor(id, Range));
        var reversed = ids.AsEnumerable().Reverse().ToDictionary(id => id, id => EffortFactors.FactorFor(id, Range));

        foreach (var id in ids)
        {
            Assert.Equal(inOrder[id], reversed[id]);
        }
    }

    [Fact]
    public void Every_factor_stays_inside_the_range_with_the_upper_limit_exclusive()
    {
        for (var index = 1; index <= 300; index++)
        {
            var factor = EffortFactors.FactorFor(IdAt(index), Range);

            Assert.True(factor >= 0.9m, $"fator {factor} abaixo do mínimo");
            Assert.True(factor < 1.1m, $"fator {factor} não é menor que o máximo");
        }
    }

    [Fact]
    public void A_different_seed_changes_the_factor()
    {
        var id = IdAt(9);
        var other = new ValidationParameters(0.9m, 1.1m, 99L);

        Assert.NotEqual(EffortFactors.FactorFor(id, Range), EffortFactors.FactorFor(id, other));
    }

    [Fact]
    public void A_degenerate_range_gives_exactly_that_factor()
    {
        var fixedFactor = new ValidationParameters(1m, 1m, 3L);

        Assert.Equal(1m, EffortFactors.FactorFor(IdAt(4), fixedFactor));
    }

    [Theory]
    [InlineData(0, 1.1, 1L)]
    [InlineData(-0.5, 1.1, 1L)]
    [InlineData(1.2, 1.1, 1L)]
    public void Invalid_range_is_refused(double min, double max, long seed)
    {
        var parameters = new ValidationParameters((decimal)min, (decimal)max, seed);

        Assert.Throws<ArgumentException>(() => EffortFactors.FactorFor(IdAt(1), parameters));
    }
}
