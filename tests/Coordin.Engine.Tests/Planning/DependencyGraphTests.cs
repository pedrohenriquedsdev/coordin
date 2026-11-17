using Coordin.Engine.Planning;

namespace Coordin.Engine.Tests.Planning;

public class DependencyGraphTests
{
    private static readonly Guid A = Id(1);
    private static readonly Guid B = Id(2);
    private static readonly Guid C = Id(3);
    private static readonly Guid D = Id(4);
    private static readonly Guid E = Id(5);

    private static Guid Id(int number) => new(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    private static DependencyInput Edge(Guid demand, Guid prerequisite) => new(demand, prerequisite);

    [Fact]
    public void NoDependencies_HasNoCycle()
    {
        Assert.Null(DependencyGraph.FindCycle([A, B, C], []));
    }

    [Fact]
    public void NoDemands_HasNoCycle()
    {
        Assert.Null(DependencyGraph.FindCycle([], []));
    }

    [Fact]
    public void Chain_HasNoCycle()
    {
        // C depende de B, que depende de A.
        Assert.Null(DependencyGraph.FindCycle([A, B, C], [Edge(C, B), Edge(B, A)]));
    }

    [Fact]
    public void Diamond_TwoPathsToTheSameDemand_HasNoCycle()
    {
        // D depende de B e de C; os dois dependem de A. Chegar a A por dois caminhos não é ciclo.
        var edges = new[] { Edge(D, B), Edge(D, C), Edge(B, A), Edge(C, A) };

        Assert.Null(DependencyGraph.FindCycle([A, B, C, D], edges));
    }

    [Fact]
    public void DirectCycle_IsFound_WithTheFirstDemandRepeatedAtTheEnd()
    {
        var cycle = DependencyGraph.FindCycle([A, B], [Edge(A, B), Edge(B, A)]);

        Assert.Equal([A, B, A], cycle);
    }

    [Fact]
    public void IndirectCycleOfThree_IsFound()
    {
        // A depende de B, B de C, C de A.
        var cycle = DependencyGraph.FindCycle([A, B, C], [Edge(A, B), Edge(B, C), Edge(C, A)]);

        Assert.Equal([A, B, C, A], cycle);
    }

    [Fact]
    public void DemandThatDependsOnItself_IsACycleOfOne()
    {
        var cycle = DependencyGraph.FindCycle([A, B], [Edge(A, A)]);

        Assert.Equal([A, A], cycle);
    }

    [Fact]
    public void Cycle_InsideALargerGraph_ReportsOnlyTheCycle()
    {
        // E e A formam uma cadeia sem ciclo que chega ao ciclo B → C → B; D é independente.
        var edges = new[] { Edge(E, A), Edge(A, B), Edge(B, C), Edge(C, B) };

        var cycle = DependencyGraph.FindCycle([A, B, C, D, E], edges);

        Assert.Equal([B, C, B], cycle);
    }

    [Fact]
    public void RepeatedEdges_AreNotACycle()
    {
        Assert.Null(DependencyGraph.FindCycle([A, B], [Edge(B, A), Edge(B, A)]));
    }

    [Fact]
    public void EdgesToDemandsOutsideTheList_AreIgnored()
    {
        // O validador é quem reclama de referência inexistente; aqui a aresta simplesmente não conta.
        Assert.Null(DependencyGraph.FindCycle([A, B], [Edge(A, C), Edge(C, A)]));
    }

    [Fact]
    public void SameResult_WhateverTheOrderOfTheLists()
    {
        var ids = new[] { A, B, C, D, E };
        var edges = new[] { Edge(E, A), Edge(A, B), Edge(B, C), Edge(C, B), Edge(D, C) };

        var expected = DependencyGraph.FindCycle(ids, edges);

        for (var seed = 0; seed < 25; seed++)
        {
            var random = new Random(seed);
            var shuffledIds = ids.OrderBy(_ => random.Next()).ToList();
            var shuffledEdges = edges.OrderBy(_ => random.Next()).ToList();

            Assert.Equal(expected, DependencyGraph.FindCycle(shuffledIds, shuffledEdges));
        }
    }
}
