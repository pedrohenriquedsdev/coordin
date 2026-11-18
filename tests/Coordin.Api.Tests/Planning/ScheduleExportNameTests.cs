using Coordin.Api.Features.Planning;

namespace Coordin.Api.Tests.Planning;

/// <summary>A data do nome do arquivo exportado é a do dia em Brasília, não a do UTC (DT-20; RNF-07).</summary>
public sealed class ScheduleExportNameTests
{
    /// <summary>Brasília sem horário de verão: UTC-3 fixo, como o próprio fuso é desde 2019.</summary>
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.CreateCustomTimeZone(
        "Brasilia-test", TimeSpan.FromHours(-3), "Brasília", "Brasília");

    [Fact]
    public void Name_uses_the_brasilia_date_when_utc_is_already_the_next_day()
    {
        // 02:30 UTC de 24/11 ainda é 23:30 de 23/11 em Brasília.
        var utc = new DateTimeOffset(2026, 11, 24, 2, 30, 0, TimeSpan.Zero);

        Assert.Equal("coordin-cronograma-2026-11-23.csv", ScheduleExportName.For(utc, Brasilia));
    }

    [Fact]
    public void Name_uses_the_same_day_when_brasilia_is_already_past_midnight()
    {
        // 15:00 UTC de 24/11 é 12:00 de 24/11 em Brasília.
        var utc = new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal("coordin-cronograma-2026-11-24.csv", ScheduleExportName.For(utc, Brasilia));
    }

    [Fact]
    public void Name_switches_exactly_at_midnight_in_brasilia()
    {
        // 03:00 UTC é meia-noite em Brasília: é o instante em que o dia muda.
        Assert.Equal(
            new DateOnly(2026, 11, 23),
            ScheduleExportName.BrasiliaDate(new DateTimeOffset(2026, 11, 24, 2, 59, 59, TimeSpan.Zero), Brasilia));
        Assert.Equal(
            new DateOnly(2026, 11, 24),
            ScheduleExportName.BrasiliaDate(new DateTimeOffset(2026, 11, 24, 3, 0, 0, TimeSpan.Zero), Brasilia));
    }

    [Fact]
    public void Name_with_the_system_zone_has_the_expected_shape()
    {
        // Com o fuso de Brasília do sistema (ou a reserva UTC-3), o nome segue o formato do DT-20.
        var name = ScheduleExportName.For(new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.Zero));

        Assert.Matches(@"^coordin-cronograma-\d{4}-\d{2}-\d{2}\.csv$", name);
    }
}
