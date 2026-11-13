using Coordin.Engine.Capacity;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Margem de segurança (DT-22): percentual sobre a duração em dias úteis, arredondado para cima.
/// Como o percentual nunca é negativo, a data com margem nunca é anterior à data sem margem
/// (regra inviolável 1). A margem só avança a data comunicada: não ocupa horas de ninguém.
/// </summary>
public static class SafetyMargin
{
    /// <param name="forecastStart">Primeiro dia em que a demanda recebe horas (um dia útil).</param>
    /// <param name="endWithoutMargin">Dia em que o esforço termina.</param>
    /// <param name="percent">Percentual da margem, maior ou igual a zero.</param>
    /// <param name="calendar">Calendário de dias úteis.</param>
    /// <returns>Os dias úteis de margem e a data final com margem.</returns>
    public static (int MarginBusinessDays, DateOnly EndDate) Apply(
        DateOnly forecastStart,
        DateOnly endWithoutMargin,
        decimal percent,
        WorkCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfLessThan(endWithoutMargin, forecastStart);

        var duration = calendar.CountBusinessDays(forecastStart, endWithoutMargin);
        var marginDays = (int)Math.Ceiling(duration * percent / 100m);

        return (marginDays, calendar.AddBusinessDays(endWithoutMargin, marginDays));
    }
}
