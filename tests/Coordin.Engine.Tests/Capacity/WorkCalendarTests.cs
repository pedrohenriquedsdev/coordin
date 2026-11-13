using Coordin.Engine.Capacity;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Capacity;

/// <summary>Dias úteis e avanço em dias úteis (RF-03, DT-10).</summary>
public class WorkCalendarTests
{
    private static readonly WorkCalendar NoHolidays = new([]);

    [Fact]
    public void IsBusinessDay_WeekdayWithoutHoliday_IsTrue()
    {
        Assert.True(NoHolidays.IsBusinessDay(Day.Plus(2)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void IsBusinessDay_Weekend_IsFalse(int offset)
    {
        Assert.False(NoHolidays.IsBusinessDay(Day.Plus(offset)));
    }

    [Fact]
    public void IsBusinessDay_HolidayOnAFriday_IsFalse()
    {
        var calendar = new WorkCalendar([Day.Plus(4)]);

        Assert.False(calendar.IsBusinessDay(Day.Plus(4)));
    }

    [Fact]
    public void AddBusinessDays_FromFriday_SkipsTheWeekend()
    {
        Assert.Equal(Day.Plus(7), NoHolidays.AddBusinessDays(Day.Plus(4), 1));
    }

    [Fact]
    public void AddBusinessDays_WithAHolidayInTheWay_SkipsIt()
    {
        var calendar = new WorkCalendar([Day.Plus(3)]);

        Assert.Equal(Day.Plus(7), calendar.AddBusinessDays(Day.Plus(2), 2));
    }

    [Fact]
    public void AddBusinessDays_Zero_ReturnsTheSameDate()
    {
        Assert.Equal(Day.Plus(2), NoHolidays.AddBusinessDays(Day.Plus(2), 0));
    }

    [Fact]
    public void AddBusinessDays_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NoHolidays.AddBusinessDays(Day.Monday, -1));
    }

    [Fact]
    public void OnOrAfter_Saturday_ReturnsTheNextMonday()
    {
        Assert.Equal(Day.Plus(7), NoHolidays.OnOrAfter(Day.Plus(5)));
    }

    [Fact]
    public void CountBusinessDays_ACalendarWeek_IsFiveWithBothEndsIncluded()
    {
        Assert.Equal(5, NoHolidays.CountBusinessDays(Day.Monday, Day.Plus(4)));
        Assert.Equal(6, NoHolidays.CountBusinessDays(Day.Monday, Day.Plus(7)));
    }
}
