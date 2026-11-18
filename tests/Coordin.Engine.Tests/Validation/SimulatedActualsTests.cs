using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;
using Coordin.Engine.Validation;

namespace Coordin.Engine.Tests.Validation;

/// <summary>O realizado simulado: esforço escalado, margem zero e a previsão do processo atual intacta (PRD 7.3).</summary>
public sealed class SimulatedActualsTests
{
    /// <summary>Um time com uma pessoa de 8 horas por dia e uma demanda de 16 horas, de segunda a terça.</summary>
    private static (PlanningSnapshot Snapshot, Guid DemandId) OneDemand(decimal effort, decimal margin = 0m)
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var demand = builder.AddDemand(Day.Monday, [(team, effort)]);
        builder.Assign(demand, person).Margin(margin);
        return (builder.Build(), demand);
    }

    [Fact]
    public void A_factor_of_one_reproduces_the_forecast_without_margin()
    {
        var (snapshot, demand) = OneDemand(16m);
        var parameters = new ValidationParameters(1m, 1m, 1L);

        var actuals = SimulatedActuals.Calculate(snapshot, parameters);

        var forecast = ForecastEngine.Calculate(snapshot).Demands.Single(item => item.DemandId == demand);
        Assert.Equal(forecast.EndDateWithoutMargin, actuals[demand]);
    }

    [Fact]
    public void A_doubled_effort_ends_when_the_doubled_plan_ends()
    {
        var (snapshot, demand) = OneDemand(16m);
        var doubled = OneDemand(32m).Snapshot;
        var parameters = new ValidationParameters(2m, 2m, 1L);

        var actuals = SimulatedActuals.Calculate(snapshot, parameters);

        var expected = ForecastEngine.Calculate(doubled).Demands.Single().EndDateWithoutMargin;
        Assert.Equal(expected, actuals[demand]);
    }

    [Fact]
    public void A_larger_factor_never_ends_earlier()
    {
        var (snapshot, demand) = OneDemand(16m);

        var low = SimulatedActuals.Calculate(snapshot, new ValidationParameters(1m, 1m, 1L))[demand];
        var high = SimulatedActuals.Calculate(snapshot, new ValidationParameters(1.5m, 1.5m, 1L))[demand];

        Assert.True(high >= low);
    }

    [Fact]
    public void The_current_process_forecast_with_margin_is_not_changed()
    {
        var (snapshot, demand) = OneDemand(16m, margin: 20m);
        var before = ForecastEngine.Calculate(snapshot);

        SimulatedActuals.Calculate(snapshot, new ValidationParameters(1.2m, 1.2m, 5L));

        // Listas em records comparam por referência (lição da change 4): compara-se o conteúdo das previsões.
        Assert.Equal(before.Demands, ForecastEngine.Calculate(snapshot).Demands);
        Assert.Equal(20m, snapshot.SafetyMarginPercent);
        Assert.NotNull(before.Demands.Single(item => item.DemandId == demand).ForecastEndDate);
    }

    [Fact]
    public void Shuffled_demands_give_the_same_actual_dates()
    {
        var snapshot = ReferencePlanning.Build();
        var parameters = new ValidationParameters(0.9m, 1.1m, 20261126L);

        var inOrder = SimulatedActuals.Calculate(snapshot, parameters);
        var shuffled = SimulatedActuals.Calculate(RandomPlanningGenerator.Shuffled(snapshot, 11), parameters);

        Assert.Equal(inOrder.OrderBy(item => item.Key), shuffled.OrderBy(item => item.Key));
    }

    [Fact]
    public void Only_predictable_demands_have_an_actual_date()
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var person = builder.AddPerson(team, 8m);
        var ready = builder.AddDemand(Day.Monday, [(team, 16m)]);
        builder.Assign(ready, person);
        var unestimated = builder.AddDemand(Day.Monday, [(team, null)]);
        builder.Assign(unestimated, person);

        var actuals = SimulatedActuals.Calculate(builder.Build(), new ValidationParameters(1m, 1m, 1L));

        Assert.True(actuals.ContainsKey(ready));
        Assert.False(actuals.ContainsKey(unestimated));
    }

    [Fact]
    public void A_scaled_effort_never_goes_below_one_hundredth_of_an_hour()
    {
        // Um esforço de 0,01 h multiplicado por 0,1 daria zero: o piso mantém a demanda previsível.
        var (snapshot, demand) = OneDemand(0.01m);

        var actuals = SimulatedActuals.Calculate(snapshot, new ValidationParameters(0.1m, 0.1m, 1L));

        Assert.True(actuals.ContainsKey(demand));
    }
}
