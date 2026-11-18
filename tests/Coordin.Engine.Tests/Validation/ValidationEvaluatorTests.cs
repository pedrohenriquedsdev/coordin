using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;
using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>A avaliação completa da validação (PRD 7.3; DT-37; RNF-02), com os invariantes do TDD 8.</summary>
public sealed class ValidationEvaluatorTests
{
    private static readonly ValidationParameters Range = new(0.9m, 1.1m, 20261126L);

    /// <summary>A linha de base igual à previsão do processo atual: os dois desvios começam iguais.</summary>
    private static Dictionary<Guid, DateOnly> BaselineFrom(PlanningSnapshot snapshot)
    {
        var baseline = new Dictionary<Guid, DateOnly>();
        foreach (var forecast in ForecastEngine.Calculate(snapshot).Demands)
        {
            if (forecast.ForecastEndDate is { } end)
            {
                baseline[forecast.DemandId] = end;
            }
        }

        return baseline;
    }

    private static ValidationInput Input(PlanningSnapshot current, PlanningSnapshot withCoordin) =>
        new(current, withCoordin, BaselineFrom(current), Range);

    [Fact]
    public void Same_input_gives_the_same_result_every_time()
    {
        var snapshot = ReferencePlanning.Build();
        var input = Input(snapshot, snapshot);

        var first = ValidationEvaluator.Evaluate(input);
        var second = ValidationEvaluator.Evaluate(input);

        // Listas dentro dos records comparam por referência: compara-se cada parte pelo conteúdo.
        Assert.Equal(first.PeriodStart, second.PeriodStart);
        Assert.Equal(first.PeriodEnd, second.PeriodEnd);
        Assert.Equal(first.Efficiency, second.Efficiency);
        Assert.Equal(first.Deviation.Items, second.Deviation.Items);
        Assert.Equal(first.Deviation.AverageBaselineDeviationDays, second.Deviation.AverageBaselineDeviationDays);
        Assert.Equal(first.PriorityCheck.Items, second.PriorityCheck.Items);
    }

    [Fact]
    public void Shuffled_lists_give_the_same_result()
    {
        var snapshot = ReferencePlanning.Build();
        var shuffled = RandomPlanningGenerator.Shuffled(snapshot, 17);

        var inOrder = ValidationEvaluator.Evaluate(Input(snapshot, snapshot));
        var reordered = ValidationEvaluator.Evaluate(Input(shuffled, shuffled));

        Assert.Equal(inOrder.Efficiency, reordered.Efficiency);
        Assert.Equal(inOrder.Deviation.Items, reordered.Deviation.Items);
        Assert.Equal(inOrder.PriorityCheck.Items, reordered.PriorityCheck.Items);
    }

    [Fact]
    public void Identical_plannings_give_identical_deviations_and_no_worsened_priority()
    {
        var snapshot = ReferencePlanning.Build();

        var result = ValidationEvaluator.Evaluate(Input(snapshot, snapshot));

        Assert.All(result.Deviation.Items, item => Assert.Equal(item.BaselineDeviationDays, item.CoordinDeviationDays));
        Assert.Equal(0, result.PriorityCheck.WorsenedCount);
    }

    [Fact]
    public void Period_starts_on_a_monday_and_ends_on_a_sunday()
    {
        var snapshot = ReferencePlanning.Build();

        var result = ValidationEvaluator.Evaluate(Input(snapshot, snapshot));

        Assert.Equal(DayOfWeek.Monday, result.PeriodStart.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, result.PeriodEnd.DayOfWeek);
        Assert.True(result.PeriodStart <= result.PeriodEnd);
    }

    [Fact]
    public void Without_any_predictable_demand_the_comparison_is_refused()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddDemand(Day.Monday, [(team, 8m)]);
        var snapshot = builder.Build();

        Assert.Throws<ArgumentException>(() =>
            ValidationEvaluator.Evaluate(new ValidationInput(snapshot, snapshot, new Dictionary<Guid, DateOnly>(), Range)));
    }

    [Fact]
    public void A_coordin_planning_with_more_people_brings_the_forecasts_earlier()
    {
        // Mesmas demandas: o processo atual tem uma pessoa por time; o planejamento com o Coordin, duas.
        var current = BuildTeamPlanning(peoplePerTeam: 1);
        var withCoordin = BuildTeamPlanning(peoplePerTeam: 2);

        var result = ValidationEvaluator.Evaluate(Input(current, withCoordin));

        Assert.True(result.Efficiency.WithCoordin.SectorEndDate <= result.Efficiency.CurrentProcess.SectorEndDate);
        Assert.True(result.Efficiency.WithCoordin.IdleHours >= 0m);
    }

    /// <summary>Quatro demandas de 40 h em dois times, com a quantidade de pessoas dada em cada time.</summary>
    private static PlanningSnapshot BuildTeamPlanning(int peoplePerTeam)
    {
        var builder = new PlanningSnapshotBuilder();
        var web = builder.AddTeam();
        var data = builder.AddTeam();
        var webPeople = Enumerable.Range(0, peoplePerTeam).Select(_ => builder.AddPerson(web, 8m)).ToArray();
        var dataPeople = Enumerable.Range(0, peoplePerTeam).Select(_ => builder.AddPerson(data, 8m)).ToArray();
        for (var index = 0; index < 4; index++)
        {
            var team = index % 2 == 0 ? web : data;
            var people = index % 2 == 0 ? webPeople : dataPeople;
            var demand = builder.AddDemand(Day.Monday, [(team, 40m)]);
            builder.Assign(demand, people);
        }

        return builder.Build();
    }

    [Fact]
    public void Completed_and_unstarted_demands_do_not_set_the_period()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var ready = builder.AddDemand(Day.Monday, [(team, 16m)]);
        builder.Assign(ready, person);
        builder.AddDemand(Day.Plus(40), [(team, 8m)], completed: true);
        builder.AddDemand(null, [(team, 8m)]);
        var snapshot = builder.Build();

        var result = ValidationEvaluator.Evaluate(Input(snapshot, snapshot));

        Assert.Equal(Day.Monday, result.PeriodStart);
    }

    [Fact]
    public void Only_completed_demands_refuse_the_comparison()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        builder.AddPerson(team, 8m);
        builder.AddDemand(Day.Monday, [(team, 8m)], completed: true);
        var snapshot = builder.Build();

        Assert.Throws<ArgumentException>(() =>
            ValidationEvaluator.Evaluate(new ValidationInput(snapshot, snapshot, new Dictionary<Guid, DateOnly>(), Range)));
    }
}
