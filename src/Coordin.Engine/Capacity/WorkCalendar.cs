namespace Coordin.Engine.Capacity;

/// <summary>
/// Calendário de dias úteis do setor (DT-10): um dia útil é qualquer data de segunda a sexta que
/// não seja feriado. Trabalha só com datas de calendário (<see cref="DateOnly"/>), sem fuso e sem relógio.
/// </summary>
public sealed class WorkCalendar
{
    private readonly HashSet<DateOnly> _holidays;

    public WorkCalendar(IEnumerable<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        _holidays = [.. holidays];
    }

    public bool IsBusinessDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !_holidays.Contains(date);

    /// <summary>A própria data, se for dia útil; senão, o primeiro dia útil depois dela.</summary>
    public DateOnly OnOrAfter(DateOnly date)
    {
        while (!IsBusinessDay(date))
        {
            date = date.AddDays(1);
        }

        return date;
    }

    /// <summary>O primeiro dia útil estritamente depois da data.</summary>
    public DateOnly NextBusinessDay(DateOnly date) => OnOrAfter(date.AddDays(1));

    /// <summary>Avança a data em <paramref name="count"/> dias úteis, pulando fins de semana e feriados.</summary>
    public DateOnly AddBusinessDays(DateOnly date, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (var step = 0; step < count; step++)
        {
            date = NextBusinessDay(date);
        }

        return date;
    }

    /// <summary>Quantos dias úteis há entre as duas datas, com as duas pontas incluídas.</summary>
    public int CountBusinessDays(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (IsBusinessDay(day))
            {
                count++;
            }
        }

        return count;
    }
}
