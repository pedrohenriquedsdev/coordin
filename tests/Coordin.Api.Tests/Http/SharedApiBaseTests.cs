using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coordin.Api.Tests.Http;

/// <summary>Base comum dos cadastros: enums como texto, datas de calendário, casas decimais e erros do banco (design D4, D3).</summary>
public class SharedApiBaseTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---- Casas decimais ----

    private sealed class Hours
    {
        [MaxDecimalPlaces(2)]
        public decimal? Value { get; init; }
    }

    private static bool IsValid(decimal? value)
    {
        var instance = new Hours { Value = value };
        return Validator.TryValidateObject(instance, new ValidationContext(instance), null, validateAllProperties: true);
    }

    [Theory]
    [InlineData("8")]
    [InlineData("8.5")]
    [InlineData("8.50")]
    [InlineData("8.500")]
    [InlineData("0")]
    [InlineData("24")]
    [InlineData("0.01")]
    public void UpToTwoDecimalPlaces_IsValid(string text)
    {
        Assert.True(IsValid(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("8.123")]
    [InlineData("0.001")]
    [InlineData("24.005")]
    public void MoreThanTwoDecimalPlaces_IsInvalid(string text)
    {
        Assert.False(IsValid(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ANullValue_IsLeftToTheRequiredAttribute()
    {
        Assert.True(IsValid(null));
    }

    [Fact]
    public void TheDefaultMessage_IsInPortuguese()
    {
        var message = new MaxDecimalPlacesAttribute(2).FormatErrorMessage("DailyCapacityHours");

        Assert.Equal("Use no máximo 2 casas decimais.", message);
    }

    // ---- Erros do banco ----

    private static PostgresException Postgres(string sqlState) =>
        new("mensagem", "ERROR", "ERROR", sqlState);

    [Fact]
    public void EachSqlState_IsRecognizedAsItsOwnRule()
    {
        Assert.True(PostgresErrors.IsUniqueViolation(Postgres("23505")));
        Assert.True(PostgresErrors.IsForeignKeyViolation(Postgres("23503")));
        Assert.True(PostgresErrors.IsRestrictViolation(Postgres("23001")));

        Assert.False(PostgresErrors.IsUniqueViolation(Postgres("23503")));
        Assert.False(PostgresErrors.IsForeignKeyViolation(Postgres("23001")));
        Assert.False(PostgresErrors.IsRestrictViolation(Postgres("23505")));
    }

    [Fact]
    public void AnErrorWrappedByTheDbUpdateException_IsRecognizedToo()
    {
        var wrapped = new DbUpdateException("falhou", Postgres("23505"));

        Assert.True(PostgresErrors.IsUniqueViolation(wrapped));
    }

    [Fact]
    public void DeletingWithDependents_AcceptsBothRestrictAndForeignKeyCodes()
    {
        Assert.True(PostgresErrors.IsDependentRecordsViolation(Postgres("23001")));
        Assert.True(PostgresErrors.IsDependentRecordsViolation(Postgres("23503")));
        Assert.False(PostgresErrors.IsDependentRecordsViolation(Postgres("23505")));
    }

    [Fact]
    public void AnUnrelatedException_IsNotRecognized()
    {
        Assert.False(PostgresErrors.IsUniqueViolation(new InvalidOperationException("x")));
        Assert.False(PostgresErrors.IsDependentRecordsViolation(new DbUpdateException("x")));
    }

    // ---- Enum e data de calendário no corpo JSON ----

    private Task<HttpResponseMessage> PostAsync(string json) =>
        _client.PostAsync(
            "/api/_test/enum-date",
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AnEnum_GoesInAndComesBackAsText_AndACalendarDateIsNotShifted()
    {
        var response = await PostAsync("""{"type":"Vacation","date":"2026-12-31"}""");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("Vacation", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("2026-12-31", body.RootElement.GetProperty("date").GetString());
    }

    [Theory]
    [InlineData("""{"type":"Holiday","date":"2026-12-31"}""")] // valor desconhecido
    [InlineData("""{"type":7,"date":"2026-12-31"}""")] // número sem correspondência
    [InlineData("""{"type":1,"date":"2026-12-31"}""")] // o número de um valor válido: só texto é aceito
    [InlineData("""{"type":"Vacation","date":"31/12/2026"}""")] // data em formato inválido
    [InlineData("""{"type":"Vacation","date":"2026-02-30"}""")] // dia que não existe
    [InlineData("{\"type\":\"Vacation\",\"date\":")] // corpo cortado
    [InlineData("não é JSON")]
    public async Task AnInvalidBody_ReturnsA400ProblemDetails_NeverA500(string json)
    {
        var response = await PostAsync(json);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(text);
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Requisição inválida", body.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
    }
}
