using System.Globalization;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// O nome do arquivo do cronograma exportado (DT-20; RNF-07): <c>coordin-cronograma-AAAA-MM-DD.csv</c>, com a data
/// do dia da exportação no fuso de Brasília. Função pura: o instante e o fuso chegam por parâmetro.
/// </summary>
public static class ScheduleExportName
{
    private const string BrasiliaZoneId = "America/Sao_Paulo";

    /// <summary>Brasil não tem horário de verão desde 2019, então UTC-3 fixo é a regra de reserva.</summary>
    private static readonly TimeSpan FixedBrasiliaOffset = TimeSpan.FromHours(-3);

    /// <summary>O nome do arquivo para o instante dado, com o fuso de Brasília do sistema (ou a reserva UTC-3).</summary>
    public static string For(DateTimeOffset utcNow) => For(utcNow, ResolveBrasilia());

    /// <summary>O nome do arquivo para o instante dado, com o fuso informado.</summary>
    public static string For(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        "coordin-cronograma-" + BrasiliaDate(utcNow, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv";

    /// <summary>A data de calendário do instante no fuso informado.</summary>
    public static DateOnly BrasiliaDate(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);

    private static TimeZoneInfo ResolveBrasilia()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(BrasiliaZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("UTC-3", FixedBrasiliaOffset, "UTC-3", "UTC-3");
        }
    }
}
