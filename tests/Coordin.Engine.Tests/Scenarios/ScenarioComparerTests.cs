using Coordin.Engine.Scenarios;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>O desempate entre cenários, na ordem do DT-25 (RNF-02).</summary>
public class ScenarioComparerTests
{
    private static readonly Guid One = Id(1);
    private static readonly Guid Two = Id(2);
    private static readonly Guid Three = Id(3);
    private static readonly Guid[] CurrentOrder = [One, Two, Three];

    private static Guid Id(int number) => new(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    private static Scenario Make(Guid[] order, long score, DateOnly? sectorEnd = null) =>
        new(order, [], sectorEnd ?? new DateOnly(2026, 12, 1), 0, new ScenarioScore(100, score, score - 100), true);

    private static ScenarioComparer Comparer() => new(CurrentOrder);

    [Fact]
    public void LowerScore_IsBetter()
    {
        var better = Make([Two, One, Three], 80);
        var worse = Make([Three, Two, One], 90);

        Assert.True(Comparer().Compare(better, worse) < 0);
        Assert.True(Comparer().Compare(worse, better) > 0);
    }

    [Fact]
    public void SameScore_TheEarlierSectorEnd_IsBetter()
    {
        var earlier = Make([Two, One, Three], 80, new DateOnly(2026, 12, 1));
        var later = Make([One, Three, Two], 80, new DateOnly(2026, 12, 8));

        Assert.True(Comparer().Compare(earlier, later) < 0);
    }

    [Fact]
    public void SameScoreAndSector_FewerMovedDemands_IsBetter()
    {
        var oneSwap = Make([One, Three, Two], 80); // duas demandas fora do lugar
        var rotation = Make([Two, Three, One], 80); // três demandas fora do lugar

        Assert.True(Comparer().Compare(oneSwap, rotation) < 0);
    }

    [Fact]
    public void FullTie_TheLexicographicallySmallerSequence_Wins_AlwaysTheSame()
    {
        var startsWithTwo = Make([Two, One, Three], 80); // dois deslocados
        var startsWithOne = Make([One, Three, Two], 80); // dois deslocados

        Assert.True(Comparer().Compare(startsWithOne, startsWithTwo) < 0);
        Assert.True(Comparer().Compare(startsWithTwo, startsWithOne) > 0);
    }

    [Fact]
    public void IdenticalScenarios_Compare_AsEqual()
    {
        Assert.Equal(0, Comparer().Compare(Make([Two, One, Three], 80), Make([Two, One, Three], 80)));
    }

    [Fact]
    public void Sorting_GivesTheSameSequence_WhateverTheOrderOfTheInput()
    {
        Scenario[] scenarios =
        [
            Make([Two, One, Three], 80),
            Make([One, Three, Two], 80),
            Make([Three, Two, One], 70),
            Make([Two, Three, One], 80, new DateOnly(2026, 12, 8)),
            Make([One, Two, Three], 100),
            Make([Three, One, Two], 80),
        ];
        var expected = scenarios.OrderBy(scenario => scenario, Comparer()).Select(scenario => string.Join(",", scenario.ExecutionOrder)).ToList();

        for (var seed = 0; seed < 25; seed++)
        {
            var random = new Random(seed);
            var shuffled = scenarios.OrderBy(_ => random.Next()).ToList();

            Assert.Equal(expected, shuffled.OrderBy(scenario => scenario, Comparer()).Select(scenario => string.Join(",", scenario.ExecutionOrder)).ToList());
        }
    }

    [Fact]
    public void ALongerSequenceThanTheCurrentOrder_CountsTheExtraPositionsAsMoved()
    {
        var comparer = new ScenarioComparer([One, Two]);

        Assert.True(comparer.Compare(Make([One, Two], 80), Make([One, Two, Three], 80)) < 0);
    }

    [Fact]
    public void ANullScenario_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Comparer().Compare(null, Make([One, Two, Three], 80)));
        Assert.Throws<ArgumentNullException>(() => Comparer().Compare(Make([One, Two, Three], 80), null));
    }
}

