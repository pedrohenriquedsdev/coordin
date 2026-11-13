using Coordin.Engine.Planning;

namespace Coordin.Engine.Tests.Planning;

/// <summary>
/// Gerador de planejamentos de semente fixa (DT-34): a mesma semente gera sempre o mesmo snapshot.
/// O <see cref="Random"/> vive só nos testes; o motor continua sem aleatoriedade (RNF-02).
/// Mistura de propósito casos difíceis: demandas sem data, sem estimativa, sem pessoa, pessoas com
/// capacidade zero, ausências, feriados, vários times na mesma demanda e rankings em ordem aleatória.
/// </summary>
public static class RandomPlanningGenerator
{
    private static readonly decimal[] DailyHoursOptions = [0m, 2m, 4m, 6m, 8m];

    public static PlanningSnapshot Generate(int seed)
    {
        var random = new Random(seed);
        var builder = new PlanningSnapshotBuilder();

        var teams = Enumerable.Range(0, random.Next(1, 6)).Select(_ => builder.AddTeam()).ToList();
        var peopleByTeam = teams.ToDictionary(
            team => team,
            team => Enumerable.Range(0, random.Next(0, 5))
                .Select(_ => builder.AddPerson(team, DailyHoursOptions[random.Next(DailyHoursOptions.Length)]))
                .ToList());

        var demandCount = random.Next(0, 13);
        var priorityRanks = Enumerable.Range(1, demandCount).OrderBy(_ => random.Next()).ToList();
        var executionRanks = Enumerable.Range(1, demandCount).OrderBy(_ => random.Next()).ToList();

        for (var index = 0; index < demandCount; index++)
        {
            DateOnly? start = random.Next(10) == 0 ? null : Day.Plus(random.Next(0, 31));
            var partTeams = teams.OrderBy(_ => random.Next()).Take(random.Next(1, Math.Min(3, teams.Count) + 1)).ToList();
            var parts = partTeams
                .Select(team => (team, (decimal?)(random.Next(10) == 0 ? null : random.Next(1, 161) / 2m)))
                .ToArray();

            var demand = builder.AddDemand(start, parts, priorityRanks[index], executionRanks[index], completed: random.Next(12) == 0);
            foreach (var team in partTeams)
            {
                var chosen = peopleByTeam[team].Where(_ => random.Next(10) != 0).ToArray();
                builder.Assign(demand, chosen);
            }
        }

        var everyone = peopleByTeam.Values.SelectMany(people => people).ToList();
        if (everyone.Count > 0)
        {
            for (var i = random.Next(0, 6); i > 0; i--)
            {
                var first = random.Next(0, 40);
                builder.AddAbsence(everyone[random.Next(everyone.Count)], Day.Plus(first), Day.Plus(first + random.Next(0, 15)));
            }
        }

        for (var i = random.Next(0, 5); i > 0; i--)
        {
            builder.AddHoliday(Day.Plus(random.Next(0, 60)));
        }

        return builder
            .Margin(random.Next(0, 101))
            .Mode(random.Next(2) == 0 ? ExecutionOrderMode.FollowPriority : ExecutionOrderMode.Custom)
            .Build();
    }

    /// <summary>O mesmo planejamento com todas as listas em outra ordem (para testar o determinismo).</summary>
    public static PlanningSnapshot Shuffled(PlanningSnapshot snapshot, int seed)
    {
        var random = new Random(seed);
        return snapshot with
        {
            Teams = Shuffle(snapshot.Teams, random),
            People = Shuffle(snapshot.People, random),
            Absences = Shuffle(snapshot.Absences, random),
            Holidays = Shuffle(snapshot.Holidays, random),
            Assignments = Shuffle(snapshot.Assignments, random),
            Demands = Shuffle(snapshot.Demands.Select(demand => demand with { Parts = Shuffle(demand.Parts, random) }).ToList(), random),
        };
    }

    private static List<T> Shuffle<T>(IReadOnlyList<T> items, Random random) =>
        [.. items.OrderBy(_ => random.Next())];
}
