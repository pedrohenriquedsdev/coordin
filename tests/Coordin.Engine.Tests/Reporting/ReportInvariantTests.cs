using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Reporting;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Reporting;

/// <summary>
/// Invariantes do relatório em planejamentos gerados com semente fixa (DT-34, RNF-02, RNF-08): valem para
/// qualquer planejamento, não só para os exemplos escritos à mão.
/// </summary>
public class ReportInvariantTests
{
    private const int Snapshots = 300;

    private static readonly DateOnly From = Day.Monday;
    private static readonly DateOnly To = Day.Plus(60);

    private static IEnumerable<(int Seed, PlanningSnapshot Snapshot, TeamReportResult Report)> Cases() =>
        Enumerable.Range(1, Snapshots).Select(seed =>
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var report = TeamReport.Build(snapshot, ForecastEngine.Calculate(snapshot), From, To);
            return (seed, snapshot, report);
        });

    [Fact]
    public void EveryWeek_SatisfiesTheHoursIdentities()
    {
        foreach (var (seed, _, report) in Cases())
        {
            foreach (var week in report.Teams.SelectMany(team => team.Weeks))
            {
                Assert.True(week.AllocatedHours <= week.CapacityHours, $"Alocadas acima da capacidade na semente {seed}.");
                Assert.True(week.AllocatedHours + week.IdleHours == week.CapacityHours, $"Alocadas + ociosas diferente da capacidade na semente {seed}.");
                Assert.True(week.Occupancy is null || week.Occupancy <= 1m, $"Ocupação acima de 100% na semente {seed}.");
            }
        }
    }

    [Fact]
    public void Overload_OnlyHappensWhenAllCapacityIsUsed()
    {
        foreach (var (seed, _, report) in Cases())
        {
            foreach (var week in report.Teams.SelectMany(team => team.Weeks).Where(week => week.IsOverloaded))
            {
                Assert.True(week.CapacityHours > 0m && week.AllocatedHours == week.CapacityHours, $"Sobrecarga sem capacidade toda usada na semente {seed}.");
                Assert.NotEmpty(week.OverloadedDemandIds);
            }
        }
    }

    [Fact]
    public void Report_IsTheSame_WhateverTheOrderOfTheInputLists()
    {
        foreach (var (seed, snapshot, report) in Cases())
        {
            var shuffled = TeamReport.Build(
                RandomPlanningGenerator.Shuffled(snapshot, seed + 1000),
                ForecastEngine.Calculate(RandomPlanningGenerator.Shuffled(snapshot, seed + 1000)),
                From,
                To);

            Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(shuffled));
        }
    }
}
