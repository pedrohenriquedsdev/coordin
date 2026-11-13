using Coordin.Engine.Planning;

namespace Coordin.Engine.Tests.Planning;

/// <summary>Datas fixas dos testes: 2026-11-02 é uma segunda-feira.</summary>
public static class Day
{
    public static readonly DateOnly Monday = new(2026, 11, 2);

    /// <summary>A data <paramref name="offset"/> dias depois da primeira segunda-feira.</summary>
    public static DateOnly Plus(int offset) => Monday.AddDays(offset);
}

/// <summary>
/// Monta cenários legíveis para os testes do motor (TDD 8.2). Cada <c>Add*</c> devolve o
/// <see cref="Guid"/> criado, em sequência, para os testes se referirem aos itens sem repetir números.
/// </summary>
public sealed class PlanningSnapshotBuilder
{
    private readonly List<TeamInput> _teams = [];
    private readonly List<PersonInput> _people = [];
    private readonly List<AbsenceInput> _absences = [];
    private readonly List<HolidayInput> _holidays = [];
    private readonly List<DemandInput> _demands = [];
    private readonly List<AssignmentInput> _assignments = [];
    private ExecutionOrderMode _mode = ExecutionOrderMode.FollowPriority;
    private decimal _marginPercent;
    private int _nextId = 1;

    public Guid AddTeam()
    {
        var id = NextId();
        _teams.Add(new TeamInput(id));
        return id;
    }

    public Guid AddPerson(Guid teamId, decimal dailyHours)
    {
        var id = NextId();
        _people.Add(new PersonInput(id, teamId, dailyHours));
        return id;
    }

    /// <summary>
    /// Cria uma demanda. Sem <paramref name="priorityRank"/> e <paramref name="executionRank"/>, os dois
    /// rankings são a ordem de criação (1, 2, 3...). <paramref name="parts"/> traz time e esforço
    /// (<c>null</c> = o time ainda não estimou).
    /// </summary>
    public Guid AddDemand(
        DateOnly? startDate,
        (Guid TeamId, decimal? Hours)[] parts,
        int? priorityRank = null,
        int? executionRank = null,
        bool completed = false)
    {
        var id = NextId();
        var order = _demands.Count + 1;
        _demands.Add(new DemandInput(
            id,
            priorityRank ?? order,
            executionRank ?? order,
            startDate,
            completed,
            [.. parts.Select(part => new DemandPartInput(part.TeamId, part.Hours))]));
        return id;
    }

    public PlanningSnapshotBuilder Assign(Guid demandId, params Guid[] personIds)
    {
        _assignments.AddRange(personIds.Select(personId => new AssignmentInput(demandId, personId)));
        return this;
    }

    public PlanningSnapshotBuilder AddAbsence(Guid personId, DateOnly start, DateOnly end)
    {
        _absences.Add(new AbsenceInput(personId, start, end));
        return this;
    }

    public PlanningSnapshotBuilder AddHoliday(DateOnly date)
    {
        _holidays.Add(new HolidayInput(date));
        return this;
    }

    public PlanningSnapshotBuilder Mode(ExecutionOrderMode mode)
    {
        _mode = mode;
        return this;
    }

    public PlanningSnapshotBuilder Margin(decimal percent)
    {
        _marginPercent = percent;
        return this;
    }

    public PlanningSnapshot Build() => new(_teams, _people, _absences, _holidays, _demands, _assignments, _mode, _marginPercent);

    private Guid NextId() => new(_nextId++, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
}
