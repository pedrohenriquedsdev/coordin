using Coordin.Engine.Planning;

namespace Coordin.Engine.Capacity;

/// <summary>
/// Capacidade por data (DT-10): quantas horas uma pessoa, e por soma um time, pode trabalhar em um
/// dia. É a capacidade diária cadastrada se o dia é útil e a pessoa não está ausente; senão, zero.
/// </summary>
public sealed class PersonCapacityCalendar
{
    private readonly WorkCalendar _calendar;
    private readonly Dictionary<Guid, PersonInput> _people;
    private readonly Dictionary<Guid, List<AbsenceInput>> _absencesByPerson;
    private readonly Dictionary<Guid, List<Guid>> _peopleByTeam;

    public PersonCapacityCalendar(
        WorkCalendar calendar,
        IEnumerable<PersonInput> people,
        IEnumerable<AbsenceInput> absences)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(absences);

        _calendar = calendar;
        _people = people.ToDictionary(person => person.Id);
        _absencesByPerson = absences.GroupBy(absence => absence.PersonId).ToDictionary(g => g.Key, g => g.ToList());
        _peopleByTeam = _people.Values.GroupBy(person => person.TeamId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
    }

    /// <summary>Horas que a pessoa pode trabalhar na data.</summary>
    /// <exception cref="ArgumentException">A pessoa não existe.</exception>
    public decimal CapacityOf(Guid personId, DateOnly date)
    {
        if (!_people.TryGetValue(personId, out var person))
        {
            throw new ArgumentException("Pessoa desconhecida.", nameof(personId));
        }

        if (!_calendar.IsBusinessDay(date) || IsAbsent(personId, date))
        {
            return 0m;
        }

        return person.DailyHours;
    }

    /// <summary>Soma das capacidades, na data, das pessoas do time (RF-02). Um time sem pessoas tem zero.</summary>
    public decimal TeamCapacityOn(Guid teamId, DateOnly date)
    {
        if (!_peopleByTeam.TryGetValue(teamId, out var members))
        {
            return 0m;
        }

        return TeamCapacity.DailyHours(members.Select(member => CapacityOf(member, date)));
    }

    private bool IsAbsent(Guid personId, DateOnly date) =>
        _absencesByPerson.TryGetValue(personId, out var absences)
        && absences.Any(absence => absence.StartDate <= date && date <= absence.EndDate);
}
