using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Scenarios;

/// <summary>
/// O caminho preparado (<see cref="PreparedPlanning"/>) é o único caminho de cálculo e reaproveita o que não
/// depende da ordem; estes testes provam que isso nunca muda um resultado (RNF-01, RNF-02; DT-02), além do
/// <c>ResultGoldenTests</c>, que guarda os hashes gravados antes da aceleração.
/// </summary>
public class PreparedPlanningEquivalenceTests
{
    private const int Snapshots = 400;

    private static string Json(ForecastResult result) => JsonSerializer.Serialize(result);

    private static List<Guid> RandomOrder(PlanningSnapshot snapshot, int seed)
    {
        var random = new Random(seed);
        return [.. ExecutionOrders.Current(snapshot).OrderBy(_ => random.Next())];
    }

    [Fact]
    public void Run_WithAnyOrder_EqualsTheFullCalculationOfThatOrder()
    {
        for (var seed = 1; seed <= Snapshots; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var order = RandomOrder(snapshot, seed + 11);

            var prepared = new PreparedPlanning(snapshot).Run(order, collectConsumption: true);
            var full = ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, order));

            Assert.True(Json(prepared) == Json(full), $"O caminho preparado difere do cálculo completo na semente {seed}.");
        }
    }

    [Fact]
    public void Run_WithoutConsumption_KeepsTheForecasts_AndReturnsNoConsumption()
    {
        for (var seed = 1; seed <= Snapshots; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var order = RandomOrder(snapshot, seed + 23);
            var plan = new PreparedPlanning(snapshot);

            var with = plan.Run(order, collectConsumption: true);
            var without = plan.Run(order, collectConsumption: false);

            Assert.True(
                JsonSerializer.Serialize(with.Demands) == JsonSerializer.Serialize(without.Demands),
                $"As previsões mudaram sem o consumo na semente {seed}.");
            Assert.Empty(without.ConsumedHours);
        }
    }

    [Fact]
    public void Run_IsTheSame_WithTheListsOfTheSnapshotShuffled_AndWhenCalledTwice()
    {
        for (var seed = 1; seed <= Snapshots; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var order = RandomOrder(snapshot, seed + 37);

            var first = Json(new PreparedPlanning(snapshot).Run(order, true));
            var shuffled = Json(new PreparedPlanning(RandomPlanningGenerator.Shuffled(snapshot, seed + 1000)).Run(order, true));
            var plan = new PreparedPlanning(snapshot);
            var again = Json(plan.Run(order, true));
            var twice = Json(plan.Run(order, true));

            Assert.True(first == shuffled, $"O resultado mudou com as listas embaralhadas na semente {seed}.");
            Assert.True(first == again && first == twice, $"O resultado mudou entre execuções na semente {seed}.");
        }
    }

    [Fact]
    public void ReusingThePlanWithOtherOrders_NeverContaminatesAnotherRun()
    {
        for (var seed = 1; seed <= 150; seed++)
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var orderA = RandomOrder(snapshot, seed + 1);
            var orderB = RandomOrder(snapshot, seed + 2);
            var orderC = ExecutionOrders.Current(snapshot);
            var reused = new PreparedPlanning(snapshot);

            var a1 = Json(reused.Run(orderA, true));
            var b = Json(reused.Run(orderB, true));
            var c = Json(reused.Run(orderC, true));
            var a2 = Json(reused.Run(orderA, true));

            Assert.True(a1 == a2, $"Uma rodada contaminou a seguinte na semente {seed}.");
            Assert.True(b == Json(new PreparedPlanning(snapshot).Run(orderB, true)), $"A ordem B reutilizada difere da nova na semente {seed}.");
            Assert.True(c == Json(ForecastEngine.Calculate(snapshot)), $"A ordem atual reutilizada difere do cálculo na semente {seed}.");
        }
    }

    [Fact]
    public void Run_WithAnInvalidOrder_Throws()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var first = builder.AddDemand(Day.Monday, [(team, 8m)]);
        var second = builder.AddDemand(Day.Monday, [(team, 8m)]);
        var plan = new PreparedPlanning(builder.Build());

        Assert.Throws<ArgumentException>(() => plan.Run([first], true));
        Assert.Throws<ArgumentException>(() => plan.Run([first, first], true));
        Assert.Throws<ArgumentException>(() => plan.Run([first, Guid.NewGuid()], true));
        Assert.Throws<ArgumentException>(() => plan.Run([first, second, second], true));
        Assert.Throws<ArgumentNullException>(() => plan.Run(null!, true));
    }

    [Fact]
    public void ADemandWithoutStartDate_AndAnEmptyPlanning_AreCalculatedWithoutSimulating()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var demand = builder.AddDemand(null, [(team, 8m)]);

        var result = new PreparedPlanning(builder.Build()).Run([demand], true);
        var empty = new PreparedPlanning(new PlanningSnapshotBuilder().Build()).Run([], true);

        Assert.Equal(UnpredictableReason.MissingStartDate, result.Demands.Single().Reason);
        Assert.Empty(result.ConsumedHours);
        Assert.Empty(empty.Demands);
    }

    [Fact]
    public void ThePreparedCapacity_RespectsAbsencesAndHolidays_LikeTheCalendar()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, 40m)]);
        builder.Assign(demand, person).AddAbsence(person, Day.Plus(1), Day.Plus(2)).AddHoliday(Day.Plus(3));
        var snapshot = builder.Build();

        var forecast = new PreparedPlanning(snapshot).Run([demand], true).Demands.Single();

        // 40 h a 8 h/dia são 5 dias de trabalho: segunda (+0), ausente terça e quarta, feriado na quinta,
        // então sexta (+4) e, depois do fim de semana, segunda (+7), terça (+8) e quarta (+9).
        Assert.Equal(Day.Plus(9), forecast.EndDateWithoutMargin);
    }
}
