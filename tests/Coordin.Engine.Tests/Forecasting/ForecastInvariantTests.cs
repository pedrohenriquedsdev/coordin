using Coordin.Engine.Capacity;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>
/// Regras que valem para qualquer planejamento, não só para os exemplos escritos à mão (DT-34,
/// RNF-02, RNF-08). Cada teste roda em centenas de snapshots gerados com semente fixa.
/// </summary>
public class ForecastInvariantTests
{
    private const int Snapshots = 400;

    private static IEnumerable<(int Seed, PlanningSnapshot Snapshot, ForecastResult Result)> Cases() =>
        Enumerable.Range(1, Snapshots).Select(seed =>
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            return (seed, snapshot, ForecastEngine.Calculate(snapshot));
        });

    /// <summary>Demandas cujo fim com margem é anterior ao fim sem margem (deve ser sempre vazio).</summary>
    private static List<Guid> MarginViolations(ForecastResult result) =>
        [.. result.Demands
            .Where(d => d.Status == DemandStatus.Predictable && d.ForecastEndDate < d.EndDateWithoutMargin)
            .Select(d => d.DemandId)];

    [Fact]
    public void Generator_SameSeed_ProducesTheSameSnapshot()
    {
        var first = System.Text.Json.JsonSerializer.Serialize(RandomPlanningGenerator.Generate(7));
        var second = System.Text.Json.JsonSerializer.Serialize(RandomPlanningGenerator.Generate(7));
        var other = System.Text.Json.JsonSerializer.Serialize(RandomPlanningGenerator.Generate(8));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Generator_CoversTheHardCases()
    {
        var results = Cases().ToList();
        var forecasts = results.SelectMany(c => c.Result.Demands).ToList();

        Assert.Contains(forecasts, f => f.Status == DemandStatus.Predictable);
        Assert.Contains(forecasts, f => f.Status == DemandStatus.Completed);
        Assert.Contains(forecasts, f => f.Reason == UnpredictableReason.MissingStartDate);
        Assert.Contains(forecasts, f => f.Reason == UnpredictableReason.MissingEstimate);
        Assert.Contains(forecasts, f => f.Reason == UnpredictableReason.NoCapacity);
        Assert.Contains(results, c => c.Snapshot.Absences.Count > 0);
        Assert.Contains(results, c => c.Snapshot.Holidays.Count > 0);
        Assert.Contains(results, c => c.Snapshot.Demands.Any(d => d.Parts.Count > 1));
        Assert.Contains(results, c => c.Snapshot.OrderMode == ExecutionOrderMode.Custom);
    }

    [Fact]
    public void Margin_NeverReducesTheDeadline()
    {
        foreach (var (seed, _, result) in Cases())
        {
            Assert.True(MarginViolations(result).Count == 0, $"Margem reduziu o prazo na semente {seed}.");
        }
    }

    [Fact]
    public void MarginInvariant_CatchesACorruptedResult()
    {
        var team = new PlanningSnapshotBuilder();
        var teamId = team.AddTeam();
        var person = team.AddPerson(teamId, 8m);
        var demand = team.AddDemand(Day.Monday, [(teamId, 16m)]);
        team.Assign(demand, person).Margin(20m);
        var good = ForecastEngine.Calculate(team.Build());
        var broken = new ForecastResult(
            [good.Demands[0] with { ForecastEndDate = good.Demands[0].EndDateWithoutMargin!.Value.AddDays(-1) }],
            good.ConsumedHours);

        Assert.Empty(MarginViolations(good));
        Assert.Single(MarginViolations(broken));
    }

    [Fact]
    public void Consumption_NeverExceedsTheCapacityOfThePersonOnTheDay()
    {
        foreach (var (seed, snapshot, result) in Cases())
        {
            var calendar = new WorkCalendar(snapshot.Holidays.Select(h => h.Date));
            var capacity = new PersonCapacityCalendar(calendar, snapshot.People, snapshot.Absences);

            foreach (var group in result.ConsumedHours.GroupBy(e => (e.PersonId, e.Date)))
            {
                Assert.True(
                    group.Sum(e => e.Hours) <= capacity.CapacityOf(group.Key.PersonId, group.Key.Date),
                    $"Capacidade ultrapassada na semente {seed}.");
            }
        }
    }

    [Fact]
    public void Consumption_EqualsTheEstimatedEffortOfPredictableDemandsAndIsZeroForTheRest()
    {
        foreach (var (seed, snapshot, result) in Cases())
        {
            foreach (var forecast in result.Demands)
            {
                var consumed = result.ConsumedHours.Where(e => e.DemandId == forecast.DemandId).Sum(e => e.Hours);
                var expected = forecast.Status == DemandStatus.Predictable
                    ? snapshot.Demands.Single(d => d.Id == forecast.DemandId).Parts.Sum(p => p.EstimatedEffortHours!.Value)
                    : 0m;

                Assert.True(expected == consumed, $"Consumo diferente do esforço na semente {seed}.");
            }
        }
    }

    [Fact]
    public void Result_DoesNotDependOnTheOrderOfTheInputLists()
    {
        foreach (var (seed, snapshot, result) in Cases())
        {
            var shuffled = ForecastEngine.Calculate(RandomPlanningGenerator.Shuffled(snapshot, seed + 1000));

            Assert.True(result.Demands.SequenceEqual(shuffled.Demands), $"Previsões mudaram com listas embaralhadas na semente {seed}.");
            Assert.True(result.ConsumedHours.SequenceEqual(shuffled.ConsumedHours), $"Consumo mudou com listas embaralhadas na semente {seed}.");
        }
    }

    [Fact]
    public void Result_IsTheSameWhenCalculatedTwice()
    {
        var snapshot = RandomPlanningGenerator.Generate(11);

        var first = ForecastEngine.Calculate(snapshot);
        var second = ForecastEngine.Calculate(snapshot);

        Assert.True(first.Demands.SequenceEqual(second.Demands));
        Assert.True(first.ConsumedHours.SequenceEqual(second.ConsumedHours));
    }

    [Fact]
    public void AddingATeamToADemand_NeverBringsItsEndForward()
    {
        var checkedCases = 0;
        foreach (var (seed, snapshot, result) in Cases())
        {
            var target = result.Demands.FirstOrDefault(f =>
                f.Status == DemandStatus.Predictable && snapshot.Demands.Single(d => d.Id == f.DemandId).Parts.Count > 1);
            if (target is null)
            {
                continue;
            }

            var demand = snapshot.Demands.Single(d => d.Id == target.DemandId);
            var removed = demand.Parts[^1];
            var removedTeamPeople = snapshot.People.Where(p => p.TeamId == removed.TeamId).Select(p => p.Id).ToHashSet();
            var without = snapshot with
            {
                Demands = [.. snapshot.Demands.Select(d => d.Id == demand.Id ? d with { Parts = [.. d.Parts.Take(d.Parts.Count - 1)] } : d)],
                Assignments = [.. snapshot.Assignments.Where(a => !(a.DemandId == demand.Id && removedTeamPeople.Contains(a.PersonId)))],
            };

            var smaller = ForecastEngine.Calculate(without).Demands.Single(f => f.DemandId == demand.Id);

            Assert.Equal(DemandStatus.Predictable, smaller.Status);
            Assert.True(smaller.EndDateWithoutMargin <= target.EndDateWithoutMargin, $"Adicionar um time antecipou o fim na semente {seed}.");
            checkedCases++;
        }

        Assert.True(checkedCases > 20, $"Poucos casos com vários times: {checkedCases}.");
    }

    [Fact]
    public void Calculate_WithTheLargestPlan_StaysFast()
    {
        var builder = new PlanningSnapshotBuilder();
        var teams = Enumerable.Range(0, 15).Select(_ => builder.AddTeam()).ToList();
        var people = teams.ToDictionary(t => t, t => Enumerable.Range(0, 10).Select(_ => builder.AddPerson(t, 8m)).ToList());
        var random = new Random(3);
        for (var i = 0; i < 200; i++)
        {
            var partTeams = teams.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
            var demand = builder.AddDemand(Day.Plus(random.Next(0, 20)), [.. partTeams.Select(t => (t, (decimal?)random.Next(8, 400)))]);
            foreach (var team in partTeams)
            {
                builder.Assign(demand, [.. people[team].Take(random.Next(1, 4))]);
            }
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var result = ForecastEngine.Calculate(builder.Build());
        clock.Stop();

        Assert.Equal(200, result.Demands.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"Demorou {clock.Elapsed.TotalSeconds:F1} s.");
    }
}
