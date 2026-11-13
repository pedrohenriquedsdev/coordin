using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Capacity;

/// <summary>Capacidade de pessoas e times por data, com ausências e feriados (RF-02, RF-03, DT-10).</summary>
public class PersonCapacityCalendarTests
{
    private static readonly Guid TeamId = Guid.NewGuid();
    private static readonly Guid PersonA = Guid.NewGuid();
    private static readonly Guid PersonB = Guid.NewGuid();

    private static PersonCapacityCalendar Build(
        decimal hoursA = 8m,
        decimal hoursB = 8m,
        IEnumerable<AbsenceInput>? absences = null,
        IEnumerable<DateOnly>? holidays = null) =>
        new(
            new WorkCalendar(holidays ?? []),
            [new PersonInput(PersonA, TeamId, hoursA), new PersonInput(PersonB, TeamId, hoursB)],
            absences ?? []);

    [Fact]
    public void CapacityOf_BusinessDayWithoutAbsence_IsTheDailyHours()
    {
        Assert.Equal(8m, Build(hoursA: 8m).CapacityOf(PersonA, Day.Plus(2)));
    }

    [Fact]
    public void CapacityOf_DayInsideAnAbsence_IsZero()
    {
        var calendar = Build(absences: [new AbsenceInput(PersonA, Day.Plus(1), Day.Plus(3))]);

        Assert.Equal(0m, calendar.CapacityOf(PersonA, Day.Plus(2)));
    }

    [Fact]
    public void CapacityOf_AbsenceEnds_AreIncludedAndTheNeighboursAreNot()
    {
        var calendar = Build(absences: [new AbsenceInput(PersonA, Day.Plus(1), Day.Plus(3))]);

        Assert.Equal(8m, calendar.CapacityOf(PersonA, Day.Plus(0)));
        Assert.Equal(0m, calendar.CapacityOf(PersonA, Day.Plus(1)));
        Assert.Equal(0m, calendar.CapacityOf(PersonA, Day.Plus(3)));
        Assert.Equal(8m, calendar.CapacityOf(PersonA, Day.Plus(4)));
    }

    [Fact]
    public void CapacityOf_AbsenceOfAnotherPerson_DoesNotChangeIt()
    {
        var calendar = Build(absences: [new AbsenceInput(PersonB, Day.Plus(1), Day.Plus(3))]);

        Assert.Equal(8m, calendar.CapacityOf(PersonA, Day.Plus(2)));
    }

    [Fact]
    public void CapacityOf_ZeroDailyHours_IsZeroOnAnyDate()
    {
        Assert.Equal(0m, Build(hoursA: 0m).CapacityOf(PersonA, Day.Plus(2)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void CapacityOf_Weekend_IsZero(int offset)
    {
        Assert.Equal(0m, Build().CapacityOf(PersonA, Day.Plus(offset)));
    }

    [Fact]
    public void CapacityOf_Holiday_IsZero()
    {
        Assert.Equal(0m, Build(holidays: [Day.Plus(2)]).CapacityOf(PersonA, Day.Plus(2)));
    }

    [Fact]
    public void CapacityOf_UnknownPerson_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build().CapacityOf(Guid.NewGuid(), Day.Monday));
    }

    [Fact]
    public void TeamCapacityOn_OnePersonAbsent_IsTheOthersHours()
    {
        var calendar = Build(absences: [new AbsenceInput(PersonA, Day.Plus(1), Day.Plus(1))]);

        Assert.Equal(8m, calendar.TeamCapacityOn(TeamId, Day.Plus(1)));
        Assert.Equal(16m, calendar.TeamCapacityOn(TeamId, Day.Plus(2)));
    }

    [Fact]
    public void TeamCapacityOn_TeamWithoutPeople_IsZero()
    {
        Assert.Equal(0m, Build().TeamCapacityOn(Guid.NewGuid(), Day.Monday));
    }
}
