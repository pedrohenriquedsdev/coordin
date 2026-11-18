using Coordin.Api.Features.Planning;
using Coordin.Engine.Planning;

namespace Coordin.Api.Tests.Planning;

/// <summary>
/// O formatador do CSV do cronograma (DT-20; RF-19; RNF-07), testado sem servidor nem banco. Cada teste monta a
/// resposta do cronograma direto e confere o texto que sai.
/// </summary>
public sealed class ScheduleCsvTests
{
    private const string ExpectedHeader =
        "Prioridade;Ordem de execução;Fora da ordem de prioridade;Demanda;Times;Esforço total (h);Data de início;"
        + "Início previsto;Fim sem margem;Fim com margem;Fim informado;Passa do prazo informado;Situação;"
        + "Concluída em;Desvio contra a primeira previsão (dias);Desvio contra a previsão vigente (dias);Depende de";

    private static readonly Guid WebTeam = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DataTeam = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Uma demanda previsível, com dois times (um com horas), sem concluir e sem pré-requisitos.</summary>
    private static ScheduleItemResponse PredictableItem() => new(
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "Checkout",
        1,
        1,
        false,
        [
            new ScheduleTeamResponse(WebTeam, "Web", 80m),
            new ScheduleTeamResponse(DataTeam, "Dados", null),
        ],
        80m,
        new DateOnly(2026, 12, 1),
        true,
        null,
        new DateOnly(2026, 12, 1),
        new DateOnly(2026, 12, 14),
        new DateOnly(2026, 12, 16),
        new DateOnly(2026, 12, 20),
        false,
        null,
        null,
        null,
        null,
        []);

    private static ScheduleResponse Response(params ScheduleItemResponse[] items) =>
        new(ExecutionOrderMode.FollowPriority, items);

    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Conta as colunas de uma linha, sem contar os separadores que estão dentro de aspas.</summary>
    private static int CountFields(string line)
    {
        var count = 1;
        var quoted = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ';' && !quoted)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Header_has_the_columns_in_the_agreed_order()
    {
        var csv = ScheduleCsv.Build(Response());

        Assert.Equal(ExpectedHeader, Lines(csv)[0]);
    }

    [Fact]
    public void Lines_end_with_crlf_and_have_no_bare_line_feed()
    {
        var csv = ScheduleCsv.Build(Response(PredictableItem()));

        Assert.EndsWith("\r\n", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", csv.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Bytes_start_with_the_utf8_bom_and_decode_to_the_same_text()
    {
        var csv = ScheduleCsv.Build(Response(PredictableItem()));

        var bytes = ScheduleCsv.ToBytes(csv);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Equal(csv, System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Empty_schedule_has_only_the_header()
    {
        var csv = ScheduleCsv.Build(Response());

        Assert.Equal(ExpectedHeader + "\r\n", csv);
    }

    [Fact]
    public void Every_row_has_as_many_columns_as_the_header()
    {
        var csv = ScheduleCsv.Build(Response(PredictableItem()));

        foreach (var line in Lines(csv))
        {
            Assert.Equal(17, CountFields(line));
        }
    }

    [Fact]
    public void Dates_are_day_month_year()
    {
        var row = Lines(ScheduleCsv.Build(Response(PredictableItem())))[1];

        Assert.Contains(";01/12/2026;01/12/2026;14/12/2026;16/12/2026;20/12/2026;", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Hours_use_a_decimal_comma_and_no_trailing_zeros()
    {
        var item = PredictableItem() with
        {
            Teams = [new ScheduleTeamResponse(WebTeam, "Web", 12.5m)],
            TotalEstimatedEffortHours = 12.5m,
        };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(";12,5;", row, StringComparison.Ordinal);
        Assert.Contains("Web: 12,5 h", row, StringComparison.Ordinal);
        Assert.DoesNotContain("12.5", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Team_without_estimate_says_so_in_the_teams_cell()
    {
        var row = Lines(ScheduleCsv.Build(Response(PredictableItem())))[1];

        Assert.Contains("Web: 80 h, Dados: sem estimativa", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_fields_are_blank_and_never_null()
    {
        var csv = ScheduleCsv.Build(Response(PredictableItem()));

        Assert.DoesNotContain("null", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(";;", Lines(csv)[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Yes_and_no_are_written_in_portuguese()
    {
        var item = PredictableItem() with { IsOutOfPriorityOrder = true, ExceedsInformedEndDate = true };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.StartsWith("1;1;Sim;Checkout;", row, StringComparison.Ordinal);
        Assert.Contains(";Sim;Previsível;", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Field_with_separator_quotes_and_newline_is_quoted_with_doubled_quotes()
    {
        var item = PredictableItem() with { Title = "Pagar; \"urgente\"\nagora" };

        var csv = ScheduleCsv.Build(Response(item));

        Assert.Contains("\"Pagar; \"\"urgente\"\"\nagora\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_with_a_separator_inside_a_quoted_title_keeps_the_column_count()
    {
        var item = PredictableItem() with { Title = "Pagar; agora" };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Equal(17, CountFields(row));
    }

    [Theory]
    [InlineData("=SOMA(A1:A2)", "'=SOMA(A1:A2)")]
    [InlineData("+1-1", "'+1-1")]
    [InlineData("-x", "'-x")]
    [InlineData("@usuario", "'@usuario")]
    [InlineData("\tcom tab", "'\tcom tab")]
    public void Typed_title_starting_with_a_formula_character_gets_an_apostrophe(string title, string expected)
    {
        var item = PredictableItem() with { Title = title };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(expected, row, StringComparison.Ordinal);
    }

    [Fact]
    public void Typed_title_in_the_middle_is_not_changed()
    {
        var item = PredictableItem() with { Title = "Plano = meta" };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(";Plano = meta;", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Team_name_with_a_formula_character_is_neutralized_too()
    {
        var item = PredictableItem() with { Teams = [new ScheduleTeamResponse(WebTeam, "=Web", 8m)] };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains("'=Web: 8 h", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_negative_deviation_is_not_neutralized()
    {
        var item = PredictableItem() with
        {
            CompletedOn = new DateOnly(2026, 12, 13),
            BaselineDeviationDays = -3,
            ForecastDeviationDays = null,
        };

        var csv = ScheduleCsv.Build(Response(item));

        Assert.Contains(";-3;", Lines(csv)[1], StringComparison.Ordinal);
        Assert.DoesNotContain("'-3", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_demand_shows_concluded_and_both_deviations_with_their_sign()
    {
        var item = PredictableItem() with
        {
            CompletedOn = new DateOnly(2026, 12, 17),
            BaselineDeviationDays = 1,
            ForecastDeviationDays = -2,
        };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(";Concluída;17/12/2026;1;-2;", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Prerequisites_are_listed_in_order_separated_by_comma()
    {
        var item = PredictableItem() with
        {
            Prerequisites =
            [
                new ScheduleDependencyResponse(Guid.NewGuid(), "Alfa pagamentos"),
                new ScheduleDependencyResponse(Guid.NewGuid(), "Bravo"),
            ],
        };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.EndsWith(";Alfa pagamentos, Bravo", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Unpredictable_demand_has_its_reason_in_portuguese_and_no_predicted_dates()
    {
        var item = PredictableItem() with
        {
            IsPredictable = false,
            UnpredictableReason = "MissingEstimate",
            ForecastStartDate = null,
            EndDateWithoutMargin = null,
            ForecastEndDate = null,
        };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(";Falta a estimativa de algum time;", row, StringComparison.Ordinal);
        Assert.Contains(";01/12/2026;;;;20/12/2026;", row, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MissingEstimate", "Falta a estimativa de algum time")]
    [InlineData("MissingStartDate", "Sem data de início")]
    [InlineData("NoCapacity", "Sem capacidade: falta pessoa alocada ou a capacidade é zero")]
    [InlineData("PrerequisiteUnpredictable", "Depende de uma demanda que não tem previsão")]
    [InlineData("Other", "Sem previsão")]
    [InlineData(null, "Sem previsão")]
    public void Each_unpredictable_reason_has_its_fixed_portuguese_text(string? reason, string expected)
    {
        // Texto fixo: se a tela mudar o texto, este teste avisa, porque o CSV é gerado no servidor com a mesma frase.
        var item = PredictableItem() with { IsPredictable = false, UnpredictableReason = reason };

        var row = Lines(ScheduleCsv.Build(Response(item)))[1];

        Assert.Contains(";" + expected + ";", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Predictable_demand_says_previsivel()
    {
        var row = Lines(ScheduleCsv.Build(Response(PredictableItem())))[1];

        Assert.Contains(";Previsível;", row, StringComparison.Ordinal);
    }
}
