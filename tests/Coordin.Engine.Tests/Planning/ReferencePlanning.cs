using Coordin.Engine.Planning;

namespace Coordin.Engine.Tests.Planning;

/// <summary>
/// O planejamento de referência do RNF-01: 15 times, 150 pessoas (10 por time) e 200 demandas, de semente
/// fixa, com vários times por demanda e cerca de um quarto das demandas dependendo de uma anterior.
/// </summary>
public static class ReferencePlanning
{
    public static PlanningSnapshot Build(int seed = 3)
    {
        var builder = new PlanningSnapshotBuilder();
        var teams = Enumerable.Range(0, 15).Select(_ => builder.AddTeam()).ToList();
        var people = teams.ToDictionary(team => team, team => Enumerable.Range(0, 10).Select(_ => builder.AddPerson(team, 8m)).ToList());
        var random = new Random(seed);
        var demands = new List<Guid>();
        for (var index = 0; index < 200; index++)
        {
            var partTeams = teams.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
            var demand = builder.AddDemand(Day.Plus(random.Next(0, 20)), [.. partTeams.Select(team => (team, (decimal?)random.Next(8, 400)))]);
            foreach (var team in partTeams)
            {
                builder.Assign(demand, [.. people[team].Take(random.Next(1, 4))]);
            }

            if (demands.Count > 0 && random.Next(4) == 0)
            {
                builder.DependsOn(demand, demands[random.Next(demands.Count)]);
            }

            demands.Add(demand);
        }

        return builder.Build();
    }
}
