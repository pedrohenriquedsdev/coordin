using System.Globalization;
using System.Text;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Formata o cronograma como CSV no padrão pt-BR (DT-20; RF-19; RNF-07): separador <c>;</c>, quebra de linha CRLF,
/// datas <c>dd/MM/yyyy</c>, horas com vírgula decimal e cabeçalhos em português. A codificação UTF-8 com BOM é
/// aplicada por <see cref="ToBytes"/>, para o Excel reconhecer os acentos. Função pura: não lê relógio nem banco.
/// </summary>
public static class ScheduleCsv
{
    private const string Separator = ";";
    private const string NewLine = "\r\n";
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] Header =
    [
        "Prioridade",
        "Ordem de execução",
        "Fora da ordem de prioridade",
        "Demanda",
        "Times",
        "Esforço total (h)",
        "Data de início",
        "Início previsto",
        "Fim sem margem",
        "Fim com margem",
        "Fim informado",
        "Passa do prazo informado",
        "Situação",
        "Concluída em",
        "Desvio contra a primeira previsão (dias)",
        "Desvio contra a previsão vigente (dias)",
        "Depende de",
    ];

    /// <summary>O CSV do cronograma, com o cabeçalho e uma linha por demanda, na ordem de execução recebida.</summary>
    public static string Build(ScheduleResponse response)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(Separator, Header)).Append(NewLine);
        foreach (var item in response.Items)
        {
            builder.Append(Row(item)).Append(NewLine);
        }

        return builder.ToString();
    }

    /// <summary>O texto do CSV em bytes UTF-8, com o BOM no começo (DT-20).</summary>
    public static byte[] ToBytes(string csv) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];

    private static string Row(ScheduleItemResponse item) =>
        string.Join(
            Separator,
            Integer(item.PriorityRank),
            Integer(item.ExecutionRank),
            YesNo(item.IsOutOfPriorityOrder),
            ScheduleCsvText.Quote(ScheduleCsvText.Neutralize(item.Title)),
            TeamsCell(item),
            Hours(item.TotalEstimatedEffortHours),
            Date(item.StartDate),
            Date(item.ForecastStartDate),
            Date(item.EndDateWithoutMargin),
            Date(item.ForecastEndDate),
            Date(item.InformedEndDate),
            YesNo(item.ExceedsInformedEndDate),
            ScheduleCsvText.Quote(Situation(item)),
            Date(item.CompletedOn),
            Integer(item.BaselineDeviationDays),
            Integer(item.ForecastDeviationDays),
            DependenciesCell(item));

    /// <summary>Os times da demanda, com as horas de cada um: <c>Web: 80 h, Dados: sem estimativa</c>.</summary>
    private static string TeamsCell(ScheduleItemResponse item)
    {
        var teams = item.Teams.Select(team =>
            ScheduleCsvText.Neutralize(team.TeamName)
            + ": "
            + (team.EstimatedEffortHours is { } hours ? Hours(hours) + " h" : "sem estimativa"));

        return ScheduleCsvText.Quote(string.Join(", ", teams));
    }

    /// <summary>Os títulos dos pré-requisitos, na ordem da API (RF-18).</summary>
    private static string DependenciesCell(ScheduleItemResponse item) =>
        ScheduleCsvText.Quote(string.Join(", ", item.Prerequisites.Select(dependency => ScheduleCsvText.Neutralize(dependency.Title))));

    /// <summary>A situação em texto: concluída, previsível ou o motivo de não ser previsível (RF-10).</summary>
    private static string Situation(ScheduleItemResponse item)
    {
        if (item.CompletedOn is not null)
        {
            return "Concluída";
        }

        return item.IsPredictable ? "Previsível" : UnpredictableReasonText(item.UnpredictableReason);
    }

    /// <summary>
    /// O motivo de uma demanda não previsível, em português. Os mesmos textos da tela (<c>planning/labels.ts</c>);
    /// o teste de texto fixo protege a repetição, porque o CSV é gerado no servidor.
    /// </summary>
    private static string UnpredictableReasonText(string? reason) => reason switch
    {
        "MissingEstimate" => "Falta a estimativa de algum time",
        "MissingStartDate" => "Sem data de início",
        "NoCapacity" => "Sem capacidade: falta pessoa alocada ou a capacidade é zero",
        "PrerequisiteUnpredictable" => "Depende de uma demanda que não tem previsão",
        _ => "Sem previsão",
    };

    private static string Date(DateOnly? value) =>
        value is { } date ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Horas com vírgula decimal e sem zeros à direita: <c>80</c> e <c>12,5</c>.</summary>
    private static string Hours(decimal value) => value.ToString("0.##", PtBr);

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Integer(int? value) => value is { } number ? Integer(number) : string.Empty;

    private static string YesNo(bool value) => value ? "Sim" : "Não";
}
