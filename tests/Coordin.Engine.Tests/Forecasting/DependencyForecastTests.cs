using Coordin.Engine.Capacity;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>
/// Dependências entre demandas (RF-18; DT-23): quem depende só começa no dia útil seguinte ao fim
/// previsto, com a margem, do pré-requisito. Datas: <see cref="Day.Plus"/> conta a partir da
/// segunda-feira 2026-11-02; +4 é sexta, +5 e +6 são o fim de semana, +7 é a segunda seguinte.
/// Cada demanda tem a sua própria pessoa, para que só a dependência (e não a fila) decida as datas.
/// </summary>
public class DependencyForecastTests
{
    private readonly PlanningSnapshotBuilder _builder = new();
    private readonly Guid _team;

    public DependencyForecastTests()
    {
        _team = _builder.AddTeam();
    }

    private static DemandForecast Of(ForecastResult result, Guid demandId) =>
        result.Demands.Single(forecast => forecast.DemandId == demandId);

    /// <summary>Uma demanda de <paramref name="effort"/> horas, com uma pessoa de 8 h por dia só dela.</summary>
    private Guid AddDemand(decimal effort, DateOnly? start = null, bool completed = false)
    {
        var person = _builder.AddPerson(_team, 8m);
        var demand = _builder.AddDemand(start ?? Day.Monday, [(_team, effort)], completed: completed);
        _builder.Assign(demand, person);
        return demand;
    }

    private ForecastResult Calculate() => ForecastEngine.Calculate(_builder.Build());

    [Fact]
    public void Dependent_WithEarlierRegisteredStart_StartsOnTheBusinessDayAfterThePrerequisiteEnds()
    {
        var a = AddDemand(8m);
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Monday, Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(1), Of(result, b).ForecastStartDate);
    }

    [Fact]
    public void Dependent_OfAPrerequisiteEndingOnAFriday_StartsOnTheNextMonday()
    {
        var a = AddDemand(40m);
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Plus(4), Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(7), Of(result, b).ForecastStartDate);
    }

    [Fact]
    public void Dependent_WaitsForThePrerequisiteMargin_NotOnlyItsEndWithoutMargin()
    {
        // A termina na terça sem margem; com 150% de 2 dias (3 dias de margem) termina na sexta.
        _builder.Margin(150m);
        var a = AddDemand(16m);
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Plus(1), Of(result, a).EndDateWithoutMargin);
        Assert.Equal(Day.Plus(4), Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(7), Of(result, b).ForecastStartDate);
    }

    [Fact]
    public void Dependent_WithLaterRegisteredStart_KeepsItsOwnStart()
    {
        var a = AddDemand(8m);
        var b = AddDemand(8m, start: Day.Plus(3));
        _builder.DependsOn(b, a);

        Assert.Equal(Day.Plus(3), Of(Calculate(), b).ForecastStartDate);
    }

    [Fact]
    public void Dependent_NextDayIsAHoliday_StartsOnTheFirstBusinessDayAfterIt()
    {
        _builder.AddHoliday(Day.Plus(4));
        var a = AddDemand(8m, start: Day.Plus(3)); // termina na quinta (+3)
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Plus(3), Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(7), Of(result, b).ForecastStartDate);
    }

    [Fact]
    public void Chain_EachDemandStartsAfterThePreviousOneEnds()
    {
        var a = AddDemand(8m);
        var b = AddDemand(8m);
        var c = AddDemand(8m);
        _builder.DependsOn(b, a).DependsOn(c, b);

        var result = Calculate();

        Assert.Equal(Day.Monday, Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(1), Of(result, b).ForecastStartDate);
        Assert.Equal(Day.Plus(1), Of(result, b).ForecastEndDate);
        Assert.Equal(Day.Plus(2), Of(result, c).ForecastStartDate);
    }

    [Fact]
    public void DemandWithTwoPrerequisites_WaitsForTheLastOne()
    {
        var a = AddDemand(8m); // termina na segunda
        var b = AddDemand(24m); // termina na quarta
        var c = AddDemand(8m);
        _builder.DependsOn(c, a, b);

        Assert.Equal(Day.Plus(3), Of(Calculate(), c).ForecastStartDate);
    }

    [Fact]
    public void PersonIsFreeWhileADemandWaits_AndMayWorkOnAnotherOne()
    {
        // B tem maior prioridade e espera A; D tem menor prioridade, sem dependência, e é da mesma pessoa.
        var shared = _builder.AddPerson(_team, 8m);
        var b = _builder.AddDemand(Day.Monday, [(_team, 16m)]);
        var d = _builder.AddDemand(Day.Monday, [(_team, 8m)]);
        _builder.Assign(b, shared).Assign(d, shared);
        var a = AddDemand(8m); // pessoa própria, termina na segunda
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Monday, Of(result, d).ForecastEndDate); // a pessoa trabalha em D enquanto B espera
        Assert.Equal(Day.Plus(1), Of(result, b).ForecastStartDate);
        Assert.Equal(Day.Plus(2), Of(result, b).ForecastEndDate);
    }

    [Fact]
    public void ADependencyMayFreeCapacityForAnUnrelatedDemand_WithoutBreakingAnyRule()
    {
        // Sem a dependência, B (maior prioridade) ocupa a pessoa na segunda e na terça e D só termina na quarta.
        var withoutDependency = BuildSharedPerson(withDependency: false);
        var withDependency = BuildSharedPerson(withDependency: true);

        Assert.Equal(Day.Plus(2), withoutDependency);
        Assert.Equal(Day.Monday, withDependency);
    }

    private static DateOnly? BuildSharedPerson(bool withDependency)
    {
        var builder = new PlanningSnapshotBuilder();
        var team = builder.AddTeam();
        var shared = builder.AddPerson(team, 8m);
        var other = builder.AddPerson(team, 8m);
        var b = builder.AddDemand(Day.Monday, [(team, 16m)]);
        var d = builder.AddDemand(Day.Monday, [(team, 8m)]);
        var a = builder.AddDemand(Day.Monday, [(team, 8m)]);
        builder.Assign(b, shared).Assign(d, shared).Assign(a, other);
        if (withDependency)
        {
            builder.DependsOn(b, a);
        }

        return Of(ForecastEngine.Calculate(builder.Build()), d).ForecastEndDate;
    }

    [Theory]
    [InlineData(ExecutionOrderMode.FollowPriority)]
    [InlineData(ExecutionOrderMode.Custom)]
    public void DependentBeforeThePrerequisiteInTheOrder_StillStartsAfterIt_AndNothingIsReordered(ExecutionOrderMode mode)
    {
        _builder.Mode(mode);
        var b = AddDemand(8m); // rank 1 nos dois modos: vem antes na fila
        var a = AddDemand(8m); // rank 2
        _builder.DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Monday, Of(result, a).ForecastEndDate);
        Assert.Equal(Day.Plus(1), Of(result, b).ForecastStartDate);
        Assert.Equal([b, a], result.Demands.Select(forecast => forecast.DemandId).ToArray()); // a ordem da fila continua a do gestor
    }

    [Fact]
    public void CompletedPrerequisite_IsFulfilled_AndTheDependentDoesNotWait()
    {
        var a = AddDemand(8m, completed: true);
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var forecast = Of(Calculate(), b);

        Assert.Equal(DemandStatus.Predictable, forecast.Status);
        Assert.Equal(Day.Monday, forecast.ForecastStartDate);
    }

    [Fact]
    public void UnpredictablePrerequisite_MakesTheDependentUnpredictable_WithoutDates()
    {
        var a = _builder.AddDemand(Day.Monday, [(_team, null)]); // falta a estimativa
        var b = AddDemand(8m);
        _builder.DependsOn(b, a);

        var forecast = Of(Calculate(), b);

        Assert.Equal(DemandStatus.Unpredictable, forecast.Status);
        Assert.Equal(UnpredictableReason.PrerequisiteUnpredictable, forecast.Reason);
        Assert.Null(forecast.ForecastStartDate);
        Assert.Null(forecast.ForecastEndDate);
    }

    [Fact]
    public void UnpredictableDependent_DoesNotConsumeCapacity_NorDelayAnyone()
    {
        // B espera A, que não tem estimativa. B e C dividem a mesma pessoa: C não pode ser atrasada por B.
        var shared = _builder.AddPerson(_team, 8m);
        var a = _builder.AddDemand(Day.Monday, [(_team, null)]);
        var b = _builder.AddDemand(Day.Monday, [(_team, 16m)]);
        var c = _builder.AddDemand(Day.Monday, [(_team, 8m)]);
        _builder.Assign(b, shared).Assign(c, shared).DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(Day.Monday, Of(result, c).ForecastEndDate);
        Assert.DoesNotContain(result.ConsumedHours, hours => hours.DemandId == b);
    }

    [Fact]
    public void PrerequisiteThatOverflowsTheHorizon_IsNoCapacity_AndItsHigherPriorityDependentIsRetiredWithIt()
    {
        // B (prioridade 1) depende de A (prioridade 3), que nunca termina. Se o motor apontasse a de
        // maior prioridade que não terminou, escolheria B por engano. A causa é A.
        var personB = _builder.AddPerson(_team, 8m);
        var personC = _builder.AddPerson(_team, 8m);
        var personA = _builder.AddPerson(_team, 1m);
        var b = _builder.AddDemand(Day.Monday, [(_team, 8m)]);
        var c = _builder.AddDemand(Day.Monday, [(_team, 8m)]);
        var a = _builder.AddDemand(Day.Monday, [(_team, 100_000m)]);
        _builder.Assign(b, personB).Assign(c, personC).Assign(a, personA).DependsOn(b, a);

        var result = Calculate();

        Assert.Equal(UnpredictableReason.NoCapacity, Of(result, a).Reason);
        Assert.Equal(UnpredictableReason.PrerequisiteUnpredictable, Of(result, b).Reason);
        Assert.Equal(DemandStatus.Predictable, Of(result, c).Status);
    }

    [Fact]
    public void RetiredChain_OnlyTheCauseIsNoCapacity_EveryDependentIsPrerequisiteUnpredictable()
    {
        var a = _builder.AddDemand(Day.Monday, [(_team, 100_000m)]);
        var personA = _builder.AddPerson(_team, 1m);
        _builder.Assign(a, personA);
        var b = AddDemand(8m);
        var c = AddDemand(8m);
        _builder.DependsOn(b, a).DependsOn(c, b);

        var result = Calculate();

        Assert.Equal(UnpredictableReason.NoCapacity, Of(result, a).Reason);
        Assert.Equal(UnpredictableReason.PrerequisiteUnpredictable, Of(result, b).Reason);
        Assert.Equal(UnpredictableReason.PrerequisiteUnpredictable, Of(result, c).Reason);
    }

    [Fact]
    public void UnrelatedDemand_IsNotAffectedByTheRetirementOfAnOverflowingPair()
    {
        var withPair = new PlanningSnapshotBuilder();
        var team = withPair.AddTeam();
        var slow = withPair.AddPerson(team, 1m);
        var fast = withPair.AddPerson(team, 8m);
        var free = withPair.AddPerson(team, 8m);
        var a = withPair.AddDemand(Day.Monday, [(team, 100_000m)]);
        var b = withPair.AddDemand(Day.Monday, [(team, 8m)]);
        var unrelated = withPair.AddDemand(Day.Monday, [(team, 24m)]);
        withPair.Assign(a, slow).Assign(b, fast).Assign(unrelated, free).DependsOn(b, a);

        var alone = new PlanningSnapshotBuilder();
        var aloneTeam = alone.AddTeam();
        var aloneFree = alone.AddPerson(aloneTeam, 8m);
        var aloneDemand = alone.AddDemand(Day.Monday, [(aloneTeam, 24m)]);
        alone.Assign(aloneDemand, aloneFree);

        var expected = Of(ForecastEngine.Calculate(alone.Build()), aloneDemand);
        var actual = Of(ForecastEngine.Calculate(withPair.Build()), unrelated);

        Assert.Equal(expected.ForecastStartDate, actual.ForecastStartDate);
        Assert.Equal(expected.ForecastEndDate, actual.ForecastEndDate);
    }

    [Fact]
    public void MarginKeptByTheSimulation_IsTheSameThatSafetyMarginComputes()
    {
        _builder.Margin(20m).AddHoliday(Day.Plus(9));
        var a = AddDemand(40m);
        var b = AddDemand(24m);
        var c = AddDemand(16m);
        _builder.DependsOn(b, a).DependsOn(c, b);

        var calendar = new WorkCalendar([Day.Plus(9)]);
        foreach (var forecast in Calculate().Demands)
        {
            var (marginDays, end) = SafetyMargin.Apply(forecast.ForecastStartDate!.Value, forecast.EndDateWithoutMargin!.Value, 20m, calendar);
            Assert.Equal(marginDays, forecast.MarginBusinessDays);
            Assert.Equal(end, forecast.ForecastEndDate);
        }
    }

    [Fact]
    public void Calculate_WithADependencyCycle_Throws()
    {
        var a = AddDemand(8m);
        var b = AddDemand(8m);
        _builder.DependsOn(a, b).DependsOn(b, a);

        Assert.Throws<ArgumentException>(Calculate);
    }
}
