namespace Coordin.Engine.Capacity;

/// <summary>
/// Capacidade diária de um time (DT-10): a soma da capacidade diária, em horas por dia útil, das
/// pessoas que pertencem a ele. Regra pura: sem banco, sem relógio, sem I/O (DT-02). A capacidade
/// por data, com ausências e feriados, é construída sobre esta soma na change <c>forecast-engine</c>.
/// </summary>
public static class TeamCapacity
{
    /// <summary>
    /// Soma a capacidade diária das pessoas de um time. Um time sem pessoas, ou só com pessoas de
    /// capacidade zero, tem capacidade zero (RF-02): as demandas que dependem só dele não recebem previsão.
    /// </summary>
    /// <param name="peopleDailyHours">Horas por dia útil de cada pessoa do time; nunca negativas.</param>
    /// <exception cref="ArgumentOutOfRangeException">Alguma capacidade é negativa.</exception>
    public static decimal DailyHours(IEnumerable<decimal> peopleDailyHours)
    {
        ArgumentNullException.ThrowIfNull(peopleDailyHours);

        var total = 0m;
        foreach (var hours in peopleDailyHours)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(hours);
            total += hours;
        }

        return total;
    }
}
