using System.Text.Json;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>A ordem de execução vigente e como aplicar outra ordem a um snapshot (DT-13; regras invioláveis 2 e 3).</summary>
public class ExecutionOrdersTests
{
    private readonly PlanningSnapshotBuilder _builder = new();

    /// <summary>Três demandas com prioridade 1, 2, 3 e ordem de execução 3, 1, 2.</summary>
    private (Guid A, Guid B, Guid C) ThreeDemands()
    {
        var team = _builder.AddTeam();
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 3);
        var b = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 2, executionRank: 1);
        var c = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 3, executionRank: 2);
        return (a, b, c);
    }

    [Fact]
    public void Current_FollowPriority_UsesThePriorityRanking()
    {
        var (a, b, c) = ThreeDemands();

        Assert.Equal([a, b, c], ExecutionOrders.Current(_builder.Mode(ExecutionOrderMode.FollowPriority).Build()));
    }

    [Fact]
    public void Current_Custom_UsesTheExecutionRanking()
    {
        var (a, b, c) = ThreeDemands();

        Assert.Equal([b, c, a], ExecutionOrders.Current(_builder.Mode(ExecutionOrderMode.Custom).Build()));
    }

    [Fact]
    public void Current_BreaksTiesById_WhateverTheOrderOfTheList()
    {
        var team = _builder.AddTeam();
        var first = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 1);
        var second = _builder.AddDemand(Day.Monday, [(team, 8m)], priorityRank: 1, executionRank: 1);
        var snapshot = _builder.Build();
        var reversed = snapshot with { Demands = [.. snapshot.Demands.Reverse()] };

        Assert.Equal([first, second], ExecutionOrders.Current(snapshot));
        Assert.Equal([first, second], ExecutionOrders.Current(reversed));
    }

    [Fact]
    public void WithOrder_PutsTheSnapshotInCustomMode_WithRanksEqualToThePositions()
    {
        var (a, b, c) = ThreeDemands();
        var snapshot = _builder.Mode(ExecutionOrderMode.FollowPriority).Build();

        var changed = ExecutionOrders.WithOrder(snapshot, [c, a, b]);

        Assert.Equal(ExecutionOrderMode.Custom, changed.OrderMode);
        Assert.Equal(1, changed.Demands.Single(demand => demand.Id == c).ExecutionRank);
        Assert.Equal(2, changed.Demands.Single(demand => demand.Id == a).ExecutionRank);
        Assert.Equal(3, changed.Demands.Single(demand => demand.Id == b).ExecutionRank);
        Assert.Equal([c, a, b], ExecutionOrders.Current(changed));
    }

    [Fact]
    public void WithOrder_NeverChangesThePriority_NorTheOriginalSnapshot()
    {
        var (a, b, c) = ThreeDemands();
        var snapshot = _builder.Build();
        var before = JsonSerializer.Serialize(snapshot);

        var changed = ExecutionOrders.WithOrder(snapshot, [c, b, a]);

        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
        Assert.Equal(
            snapshot.Demands.Select(demand => (demand.Id, demand.PriorityRank)),
            changed.Demands.Select(demand => (demand.Id, demand.PriorityRank)));
    }

    [Fact]
    public void WithOrder_MissingDemand_Throws()
    {
        var (a, b, _) = ThreeDemands();

        Assert.Throws<ArgumentException>(() => ExecutionOrders.WithOrder(_builder.Build(), [a, b]));
    }

    [Fact]
    public void WithOrder_RepeatedDemand_Throws()
    {
        var (a, b, c) = ThreeDemands();

        Assert.Throws<ArgumentException>(() => ExecutionOrders.WithOrder(_builder.Build(), [a, b, c, a]));
    }

    [Fact]
    public void WithOrder_UnknownDemand_Throws()
    {
        var (a, b, _) = ThreeDemands();

        Assert.Throws<ArgumentException>(() => ExecutionOrders.WithOrder(_builder.Build(), [a, b, Guid.NewGuid()]));
    }
}
