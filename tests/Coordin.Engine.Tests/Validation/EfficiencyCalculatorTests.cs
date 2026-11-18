using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Reporting;
using Coordin.Engine.Tests.Planning;
using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>As métricas de eficiência (PRD 7.2), conferidas em cenários montados à mão.</summary>
public sealed class EfficiencyCalculatorTests
{
    private static (DateOnly From, DateOnly To) Week() => TeamReport.AlignedPeriod(Day.Monday, Day.Monday);

    [Fact]
    public void Idle_hours_and_spread_match_a_hand_built_scenario()
    {
        // Time 1: uma pessoa de 8 h por dia útil (40 h na semana). Uma demanda de 16 h: ocupação 16/40 = 0,4.
        // Time 2: uma pessoa, sem demanda: ocupação 0. Ociosidade: 24 + 40 = 64 h. Dispersão: (0,4 − 0) × 100 = 40 pontos.
        var builder = new PlanningSnapshotBuilder();
        var team1 = builder.AddTeam();
        var person1 = builder.AddPerson(team1, 8m);
        var team2 = builder.AddTeam();
        builder.AddPerson(team2, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team1, 16m)]);
        builder.Assign(demand, person1);
        var snapshot = builder.Build();

        var (from, to) = Week();
        var metrics = EfficiencyCalculator.Calculate(snapshot, ForecastEngine.Calculate(snapshot), from, to);

        Assert.Equal(64m, metrics.IdleHours);
        Assert.Equal(40m, metrics.OccupancySpreadPoints);
        Assert.Equal(0, metrics.OverloadedPeriods);
        Assert.Equal(Day.Plus(1), metrics.SectorEndDate);
    }

    [Fact]
    public void A_team_without_capacity_is_left_out_of_the_spread()
    {
        var builder = new PlanningSnapshotBuilder();
        var team1 = builder.AddTeam();
        var person1 = builder.AddPerson(team1, 8m);
        var team2 = builder.AddTeam();
        builder.AddPerson(team2, 8m);
        var team3 = builder.AddTeam();
        builder.AddPerson(team3, 0m);
        var demand = builder.AddDemand(Day.Monday, [(team1, 16m)]);
        builder.Assign(demand, person1);
        var snapshot = builder.Build();

        var (from, to) = Week();
        var metrics = EfficiencyCalculator.Calculate(snapshot, ForecastEngine.Calculate(snapshot), from, to);

        // O time sem capacidade não entra na dispersão: continua 40 pontos, e não há divisão por zero.
        Assert.Equal(40m, metrics.OccupancySpreadPoints);
    }

    [Fact]
    public void A_week_with_full_work_and_a_waiting_demand_counts_as_one_overloaded_period()
    {
        // Duas demandas de 40 h para a mesma pessoa: a primeira usa a semana inteira e a segunda espera.
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var first = builder.AddDemand(Day.Monday, [(team, 40m)]);
        var second = builder.AddDemand(Day.Monday, [(team, 40m)]);
        builder.Assign(first, person).Assign(second, person);
        var snapshot = builder.Build();

        var (from, to) = TeamReport.AlignedPeriod(Day.Monday, Day.Plus(13));
        var metrics = EfficiencyCalculator.Calculate(snapshot, ForecastEngine.Calculate(snapshot), from, to);

        Assert.Equal(1, metrics.OverloadedPeriods);
    }

    [Fact]
    public void The_same_snapshot_gives_the_same_metrics_every_time()
    {
        var snapshot = ReferencePlanning.Build();
        var result = ForecastEngine.Calculate(snapshot);
        var (from, to) = TeamReport.AlignedPeriod(Day.Monday, Day.Plus(90));

        var first = EfficiencyCalculator.Calculate(snapshot, result, from, to);
        var second = EfficiencyCalculator.Calculate(snapshot, result, from, to);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Without_capacity_the_spread_is_zero_and_without_forecast_there_is_no_sector_end()
    {
        // Uma demanda sem pessoa alocada não é previsível, e o time não tem capacidade: nada para medir.
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddPerson(team, 0m);
        builder.AddDemand(Day.Monday, [(team, 8m)]);
        var snapshot = builder.Build();

        var (from, to) = Week();
        var metrics = EfficiencyCalculator.Calculate(snapshot, ForecastEngine.Calculate(snapshot), from, to);

        Assert.Equal(0m, metrics.OccupancySpreadPoints);
        Assert.Null(metrics.SectorEndDate);
    }
}
