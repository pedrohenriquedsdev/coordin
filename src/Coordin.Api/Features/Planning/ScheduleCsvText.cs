namespace Coordin.Api.Features.Planning;

/// <summary>
/// Escapamento de campos CSV (RFC 4180) e neutralização de fórmulas (DT-20; RF-19). Separado do formatador para
/// ser testado sozinho. Um campo de texto digitado (título, nome de time) passa pela neutralização e pelo
/// escapamento; um campo gerado pelo sistema (número, data, situação) só passa pelo escapamento.
/// </summary>
internal static class ScheduleCsvText
{
    /// <summary>
    /// Um texto digitado pelo gestor ou pelo time, como vai para uma célula: se começar com <c>=</c>, <c>+</c>,
    /// <c>-</c>, <c>@</c> ou tabulação, ganha um apóstrofo antes, para o Excel não calcular nada.
    /// </summary>
    public static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return IsFormulaStart(value[0]) ? "'" + value : value;
    }

    /// <summary>Entre aspas quando há separador, aspas ou quebra de linha; as aspas internas são duplicadas.</summary>
    public static string Quote(string value)
    {
        if (value.IndexOfAny([';', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static bool IsFormulaStart(char c) => c is '=' or '+' or '-' or '@' or '\t';
}
