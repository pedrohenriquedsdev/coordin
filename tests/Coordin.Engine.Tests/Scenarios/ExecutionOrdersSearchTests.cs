using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>As posições fixas, a lista movível e a vizinhança da busca local (DT-26; design D5, D6).</summary>
public class ExecutionOrdersSearchTests
{
    private static readonly Guid A = Id(1);
    private static readonly Guid B = Id(2);
    private static readonly Guid C = Id(3);
    private static readonly Guid D = Id(4);

    private static Guid Id(int number) => new(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    [Fact]
    public void Movable_KeepsOnlyOpenAndPredictableDemands_InTheCurrentOrder()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var open1 = builder.AddDemand(Day.Monday, [(team, 8m)]);
        var completed = builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);
        var withoutEstimate = builder.AddDemand(Day.Monday, [(team, null)]);
        var open2 = builder.AddDemand(Day.Monday, [(team, 8m)]);
        builder.Assign(open1, person).Assign(open2, person);
        var snapshot = builder.Build();

        var movable = ExecutionOrders.Movable(snapshot, ExecutionOrders.Current(snapshot));

        Assert.Equal([open1, open2], movable);
        Assert.DoesNotContain(completed, movable);
        Assert.DoesNotContain(withoutEstimate, movable);
    }

    [Fact]
    public void Compose_KeepsCompletedAndUnpredictableInTheSameAbsolutePosition()
    {
        // A fila atual é [A, X, B, Y, C], com X e Y fora do jogo.
        var x = Id(10);
        var y = Id(11);
        Guid[] current = [A, x, B, y, C];

        var composed = ExecutionOrders.Compose(current, [C, A, B]);

        Assert.Equal([C, x, A, y, B], composed);
    }

    [Fact]
    public void Compose_WithTheSameMovableOrder_ReturnsTheCurrentOrder()
    {
        Guid[] current = [A, Id(10), B, C];

        Assert.Equal(current, ExecutionOrders.Compose(current, [A, B, C]));
    }

    [Fact]
    public void Compose_WithAWrongMovableList_Throws()
    {
        Guid[] current = [A, Id(10), B];

        Assert.Throws<ArgumentException>(() => ExecutionOrders.Compose(current, [A, A]));
        Assert.Throws<ArgumentException>(() => ExecutionOrders.Compose(current, [A, C])); // C não está na ordem atual
        Assert.Equal([A, Id(10), B], ExecutionOrders.Compose(current, [A])); // só A é movível: B fica onde está
    }

    [Fact]
    public void Neighborhood_OfFourDemands_HasTheExpectedSequence_InTheExpectedOrder()
    {
        var neighbors = NeighborhoodGenerator.Moves([A, B, C, D]).Select(order => order.ToArray()).ToList();

        Assert.Equal(
            [
                [B, A, C, D],
                [B, C, A, D],
                [B, C, D, A],
                [A, C, B, D],
                [A, C, D, B],
                [A, B, D, C],
                [C, A, B, D],
                [A, D, B, C],
                [D, A, B, C],
            ],
            neighbors);
    }

    [Fact]
    public void Neighborhood_NeverRepeatsASequence_NorIncludesTheOriginal()
    {
        var ids = Enumerable.Range(1, 12).Select(Id).ToList();

        var neighbors = NeighborhoodGenerator.Moves(ids).ToList();
        var keys = neighbors.Select(order => string.Join(",", order)).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(string.Join(",", ids), keys);
        Assert.All(neighbors, neighbor => Assert.Equal(ids.Count, neighbor.Count));
    }

    [Fact]
    public void Neighborhood_IsTheSameEveryTime()
    {
        var ids = Enumerable.Range(1, 9).Select(Id).ToList();

        Assert.Equal(
            NeighborhoodGenerator.Moves(ids).Select(order => string.Join(",", order)),
            NeighborhoodGenerator.Moves(ids).Select(order => string.Join(",", order)));
    }

    [Fact]
    public void Neighborhood_OfOneOrNoDemand_IsEmpty()
    {
        Assert.Empty(NeighborhoodGenerator.Moves([]));
        Assert.Empty(NeighborhoodGenerator.Moves([A]));
    }

    [Fact]
    public void OrderKey_ComparesWholeSequences()
    {
        var first = new OrderKey([A, B, C]);

        Assert.Equal(first, new OrderKey([A, B, C]));
        Assert.NotEqual(first, new OrderKey([A, C, B]));
        Assert.NotEqual(first, new OrderKey([A, B]));
        Assert.True(first.Equals((object)new OrderKey([A, B, C])));
        Assert.False(first.Equals((object)"not a key"));
        Assert.Equal(first.GetHashCode(), new OrderKey([A, B, C]).GetHashCode());
    }
}
