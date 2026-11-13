using Coordin.Engine.Capacity;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>Margem percentual sobre a duração em dias úteis, sempre para cima (RF-09, DT-22).</summary>
public class SafetyMarginTests
{
    private static readonly WorkCalendar NoHolidays = new([]);

    [Fact]
    public void Apply_20PercentOverTenBusinessDays_AddsTwoBusinessDays()
    {
        var (days, end) = SafetyMargin.Apply(Day.Monday, Day.Plus(11), 20m, NoHolidays);

        Assert.Equal(2, days);
        Assert.Equal(Day.Plus(15), end);
    }

    [Fact]
    public void Apply_20PercentOverOneBusinessDay_RoundsUpToOneDay()
    {
        var (days, end) = SafetyMargin.Apply(Day.Monday, Day.Monday, 20m, NoHolidays);

        Assert.Equal(1, days);
        Assert.Equal(Day.Plus(1), end);
    }

    [Fact]
    public void Apply_ZeroPercent_KeepsTheEndDate()
    {
        var (days, end) = SafetyMargin.Apply(Day.Monday, Day.Plus(11), 0m, NoHolidays);

        Assert.Equal(0, days);
        Assert.Equal(Day.Plus(11), end);
    }

    [Fact]
    public void Apply_MarginCrossingAWeekendAndAHoliday_SkipsBoth()
    {
        var holiday = new WorkCalendar([Day.Plus(7)]);

        var (days, end) = SafetyMargin.Apply(Day.Plus(1), Day.Plus(3), 100m, holiday);

        Assert.Equal(3, days);
        Assert.Equal(Day.Plus(9), end);
    }

    [Fact]
    public void Apply_NegativePercent_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SafetyMargin.Apply(Day.Monday, Day.Monday, -1m, NoHolidays));
    }

    [Fact]
    public void Apply_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SafetyMargin.Apply(Day.Plus(1), Day.Monday, 20m, NoHolidays));
    }
}
