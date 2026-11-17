using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>
/// Simular a inclusão de uma nova demanda em uma posição (RF-16; design D10). Datas: <see cref="Day.Plus"/>
/// conta a partir da segunda-feira 2026-11-02; +4 é sexta, +5 e +6 são o fim de semana, +7 é a segunda seguinte.
/// </summary>
public class NewDemandSimulationTests
{
    private static readonly Guid NewId = new(9999, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
    private readonly PlanningSnapshotBuilder _builder = new();

    private static DemandInput NewDemand(Guid team, decimal? hours, DateOnly? start = null) =>
        new(NewId, 0, 0, start ?? Day.Monday, false, [new DemandPartInput(team, hours)]);

    private static NewDemandImpact Simulate(PlanningSnapshot snapshot, DemandInput demand, Guid[] people, int? position = null, DependencyInput[]? dependencies = null) =>
        NewDemandSimulation.Simulate(snapshot, demand, [.. people.Select(person => new AssignmentInput(demand.Id, person))], dependencies ?? [], position);

    /// <summary>Uma pessoa só, com A (8 h, prioridade 1) e B (16 h, prioridade 2).</summary>
    private (Guid Team, Guid Person, Guid A, Guid B) OnePersonTwoDemands()
    {
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        var b = _builder.AddDemand(Day.Monday, [(team, 16m)]);
        _builder.Assign(a, person).Assign(b, person);
        return (team, person, a, b);
    }

    [Fact]
    public void AtTheEndOfTheQueue_NoExistingDemandChanges_AndThePositionIsNPlusOne()
    {
        var (team, person, _, _) = OnePersonTwoDemands();

        var impact = Simulate(_builder.Build(), NewDemand(team, 16m), [person]);

        Assert.Equal(3, impact.Position);
        Assert.Empty(impact.Changes);
        Assert.Equal(DemandStatus.Predictable, impact.NewDemand.Status);
        Assert.Equal(Day.Plus(4), impact.NewDemand.ForecastEndDate); // depois de A (segunda) e B (terça e quarta): quinta e sexta
        Assert.Equal(Day.Plus(4), impact.SectorEndDate); // o setor passa a terminar com a nova demanda
        Assert.Equal(2, impact.SectorDeltaDays); // antes terminava na quarta (+2)
    }

    [Fact]
    public void InFrontOfTheQueue_DisplacesTheDemandsThatShareThePerson_WithTheDatesAndTheDifference()
    {
        var (team, person, a, b) = OnePersonTwoDemands();

        var impact = Simulate(_builder.Build(), NewDemand(team, 16m), [person], position: 1);

        Assert.Equal(1, impact.Position);
        Assert.Equal(Day.Plus(1), impact.NewDemand.ForecastEndDate); // a nova usa segunda e terça
        var changeA = impact.Changes.Single(change => change.DemandId == a);
        Assert.Equal(Day.Monday, changeA.CurrentForecastEndDate);
        Assert.Equal(Day.Plus(2), changeA.NewForecastEndDate);
        Assert.Equal(2, changeA.DeltaDays);
        var changeB = impact.Changes.Single(change => change.DemandId == b);
        Assert.Equal(Day.Plus(2), changeB.CurrentForecastEndDate);
        Assert.Equal(Day.Plus(4), changeB.NewForecastEndDate);
        Assert.Equal(2, changeB.DeltaDays);
        Assert.Equal(Day.Plus(4), impact.SectorEndDate);
        Assert.Equal(2, impact.SectorDeltaDays);
    }

    [Theory]
    [InlineData(ExecutionOrderMode.FollowPriority)]
    [InlineData(ExecutionOrderMode.Custom)]
    public void InTheMiddle_TheNewDemandTakesThatPosition_AndTheOthersMoveDown_InBothOrders(ExecutionOrderMode mode)
    {
        _builder.Mode(mode);
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var existing = Enumerable.Range(1, 4).Select(_ =>
        {
            var demand = _builder.AddDemand(Day.Monday, [(team, 8m)]);
            _builder.Assign(demand, person);
            return demand;
        }).ToList();

        var impact = Simulate(_builder.Build(), NewDemand(team, 8m), [person], position: 3);

        Assert.Equal(Day.Plus(2), impact.NewDemand.ForecastEndDate); // terceira posição: quarta-feira
        Assert.Equal([existing[2], existing[3]], impact.Changes.Select(change => change.DemandId)); // só a antiga terceira e a quarta descem
        Assert.Equal([Day.Plus(3), Day.Plus(4)], impact.Changes.Select(change => change.NewForecastEndDate));
        Assert.All(impact.Changes, change => Assert.Equal(1, change.DeltaDays));
    }

    [Fact]
    public void OnlyTheDemandsThatChange_AppearInTheList()
    {
        var (team, person, a, b) = OnePersonTwoDemands();
        var otherPerson = _builder.AddPerson(team, 8m);
        var independent = _builder.AddDemand(Day.Monday, [(team, 8m)]);
        _builder.Assign(independent, otherPerson);

        var impact = Simulate(_builder.Build(), NewDemand(team, 8m), [person], position: 1);

        Assert.DoesNotContain(impact.Changes, change => change.DemandId == independent);
        Assert.Contains(impact.Changes, change => change.DemandId == a);
        Assert.Contains(impact.Changes, change => change.DemandId == b);
    }

    [Fact]
    public void TheDifference_IsInCalendarDays_NotBusinessDays()
    {
        // A (8 h) começa na sexta; a nova, também na sexta e na frente, a empurra para a segunda: 3 dias corridos.
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 8m);
        var a = _builder.AddDemand(Day.Plus(4), [(team, 8m)]);
        _builder.Assign(a, person);

        var impact = Simulate(_builder.Build(), NewDemand(team, 8m, Day.Plus(4)), [person], position: 1);

        var change = Assert.Single(impact.Changes);
        Assert.Equal(Day.Plus(4), change.CurrentForecastEndDate);
        Assert.Equal(Day.Plus(7), change.NewForecastEndDate);
        Assert.Equal(3, change.DeltaDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)] // n = 2, então o máximo é 3
    public void APositionOutsideOneToNPlusOne_Throws(int position)
    {
        var (team, person, _, _) = OnePersonTwoDemands();

        Assert.Throws<ArgumentOutOfRangeException>(() => Simulate(_builder.Build(), NewDemand(team, 8m), [person], position));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AnUnpredictableNewDemand_NeverDisplacesAnyone_InAnyPosition(int position)
    {
        var (team, person, _, _) = OnePersonTwoDemands();

        var impact = Simulate(_builder.Build(), NewDemand(team, null), [person], position);

        Assert.Equal(DemandStatus.Unpredictable, impact.NewDemand.Status);
        Assert.Equal(UnpredictableReason.MissingEstimate, impact.NewDemand.Reason);
        Assert.Empty(impact.Changes);
        Assert.Equal(0, impact.SectorDeltaDays);
    }

    [Fact]
    public void ANewDemandThatDependsOnAnExistingOne_StartsAfterItsEnd()
    {
        var (team, person, a, _) = OnePersonTwoDemands();
        var otherPerson = _builder.AddPerson(team, 8m);

        var impact = Simulate(_builder.Build(), NewDemand(team, 8m), [otherPerson], position: 1, dependencies: [new DependencyInput(NewId, a)]);

        Assert.True(impact.NewDemand.ForecastStartDate > Day.Monday); // A termina na segunda, então a nova só começa depois
        Assert.Empty(impact.Changes); // a pessoa da nova demanda não divide nada com as outras
    }

    [Fact]
    public void AnExistingDemandThatWouldOverflowTheHorizon_IsLeftOutOfTheChanges()
    {
        // 1 h por dia: cada demanda de 1.000 h cabe em cinco anos, as duas juntas não. Na frente, a nova toma o lugar da existente.
        var team = _builder.AddTeam();
        var person = _builder.AddPerson(team, 1m);
        var existing = _builder.AddDemand(Day.Monday, [(team, 1000m)]);
        _builder.Assign(existing, person);

        var impact = Simulate(_builder.Build(), NewDemand(team, 1000m), [person], position: 1);

        Assert.Equal(DemandStatus.Predictable, impact.NewDemand.Status);
        Assert.Empty(impact.Changes);
    }

    [Fact]
    public void Simulate_NeverChangesTheSnapshot()
    {
        var (team, person, _, _) = OnePersonTwoDemands();
        var snapshot = _builder.Build();
        var before = JsonSerializer.Serialize(snapshot);

        Simulate(snapshot, NewDemand(team, 8m), [person], position: 1);

        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void ADuplicatedId_IsRefused()
    {
        var (team, person, a, _) = OnePersonTwoDemands();
        var duplicated = new DemandInput(a, 0, 0, Day.Monday, false, [new DemandPartInput(team, 8m)]);

        Assert.Throws<ArgumentException>(() => Simulate(_builder.Build(), duplicated, [person]));
    }

    [Fact]
    public void NullArguments_AreRefused()
    {
        var snapshot = _builder.Build();
        var demand = NewDemand(Guid.NewGuid(), 8m);

        Assert.Throws<ArgumentNullException>(() => NewDemandSimulation.Simulate(null!, demand, [], []));
        Assert.Throws<ArgumentNullException>(() => NewDemandSimulation.Simulate(snapshot, null!, [], []));
        Assert.Throws<ArgumentNullException>(() => NewDemandSimulation.Simulate(snapshot, demand, null!, []));
        Assert.Throws<ArgumentNullException>(() => NewDemandSimulation.Simulate(snapshot, demand, [], null!));
    }

    [Fact]
    public void TheImpact_IsTheSame_WithTheListsOfTheSnapshotShuffled()
    {
        var checkedCases = 0;
        for (var seed = 1; seed <= 80; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            if (snapshot.Teams.Count == 0 || snapshot.People.Count == 0)
            {
                continue;
            }

            var random = new Random(seed);
            var person = snapshot.People[random.Next(snapshot.People.Count)];
            var demand = new DemandInput(NewId, 0, 0, Day.Plus(random.Next(0, 10)), false, [new DemandPartInput(person.TeamId, random.Next(8, 200))]);
            var position = random.Next(1, snapshot.Demands.Count + 2);

            var first = JsonSerializer.Serialize(Simulate(snapshot, demand, [person.Id], position));
            var shuffled = JsonSerializer.Serialize(Simulate(RandomPlanningGenerator.Shuffled(snapshot, seed + 900), demand, [person.Id], position));

            Assert.True(first == shuffled, $"O impacto mudou com as listas embaralhadas na semente {seed}.");
            checkedCases++;
        }

        Assert.True(checkedCases > 40, $"Poucos casos: {checkedCases}.");
    }

    /// <summary>
    /// O snapshot como ficaria depois de cadastrar a demanda na posição, montado de forma independente do motor:
    /// as duas listas de ranking são reconstruídas, a nova entra no índice pedido e tudo é renumerado de 1 a n + 1.
    /// </summary>
    private static PlanningSnapshot WithTheDemandInserted(PlanningSnapshot snapshot, DemandInput demand, Guid[] people, int position)
    {
        List<Guid> InsertedOrder(Func<DemandInput, int> rank)
        {
            var order = snapshot.Demands.OrderBy(rank).ThenBy(item => item.Id).Select(item => item.Id).ToList();
            order.Insert(position - 1, demand.Id);
            return order;
        }

        var priority = InsertedOrder(item => item.PriorityRank);
        var execution = InsertedOrder(item => item.ExecutionRank);
        return snapshot with
        {
            Demands =
            [
                .. snapshot.Demands.Select(item => item with { PriorityRank = priority.IndexOf(item.Id) + 1, ExecutionRank = execution.IndexOf(item.Id) + 1 }),
                demand with { PriorityRank = priority.IndexOf(demand.Id) + 1, ExecutionRank = execution.IndexOf(demand.Id) + 1 },
            ],
            Assignments = [.. snapshot.Assignments, .. people.Select(person => new AssignmentInput(demand.Id, person))],
        };
    }

    [Fact]
    public void TheSimulation_GivesTheSameDatesAsCalculatingThePlanningThatAlreadyHasTheDemand()
    {
        var checkedCases = 0;
        var displacedInFront = 0;
        for (var seed = 1; seed <= 100; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            if (snapshot.People.Count == 0)
            {
                continue;
            }

            var random = new Random(seed);
            var person = snapshot.People[random.Next(snapshot.People.Count)];
            var demand = new DemandInput(NewId, 0, 0, Day.Plus(random.Next(0, 10)), false, [new DemandPartInput(person.TeamId, random.Next(8, 200))]);

            foreach (var position in new[] { random.Next(1, snapshot.Demands.Count + 2), 1 })
            {
                var impact = Simulate(snapshot, demand, [person.Id], position);
                var created = ForecastEngine.Calculate(WithTheDemandInserted(snapshot, demand, [person.Id], position));
                var before = ForecastEngine.Calculate(snapshot).Demands.ToDictionary(forecast => forecast.DemandId);
                var after = created.Demands.ToDictionary(forecast => forecast.DemandId);

                Assert.True(
                    JsonSerializer.Serialize(impact.NewDemand) == JsonSerializer.Serialize(after[NewId]),
                    $"A nova demanda difere da criação na semente {seed}, posição {position}.");

                var expected = before
                    .Where(pair => after[pair.Key].Status == DemandStatus.Predictable
                        && pair.Value.Status == DemandStatus.Predictable
                        && after[pair.Key].ForecastEndDate != pair.Value.ForecastEndDate)
                    .Select(pair => (pair.Key, after[pair.Key].ForecastEndDate))
                    .Order()
                    .ToList();
                var shown = impact.Changes.Select(change => (change.DemandId, (DateOnly?)change.NewForecastEndDate)).Order().ToList();

                Assert.True(expected.SequenceEqual(shown), $"As demandas deslocadas diferem da criação na semente {seed}, posição {position}.");
                displacedInFront += position == 1 && impact.Changes.Count > 0 ? 1 : 0;
            }

            checkedCases++;
        }

        Assert.True(checkedCases > 60, $"Poucos casos: {checkedCases}.");
        Assert.True(displacedInFront > 10, $"A posição 1 deslocou alguém em poucos planejamentos ({displacedInFront}): o teste não teria dentes.");
    }
}

