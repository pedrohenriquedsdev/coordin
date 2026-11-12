using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec api-conventions: erros em Problem Details, em português (DT-06, DT-18).</summary>
public class ProblemDetailsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UnknownApiRoute_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = await ReadJsonAsync(response);
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetailsWithoutInternals()
    {
        var response = await _client.GetAsync("/api/_test/error", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("inesperado", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Segredo interno", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("senha-secreta", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BusinessConflict_Returns409WithStableCode()
    {
        var response = await _client.GetAsync("/api/_test/conflict", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = await ReadJsonAsync(response);
        Assert.Equal("example-conflict", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Conflito de exemplo.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvalidInput_Returns400WithErrorsByCamelCaseField()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/_test/validation",
            new { name = "" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = await ReadJsonAsync(response);
        var messages = body.RootElement.GetProperty("errors").GetProperty("name");
        Assert.Equal("O campo é obrigatório.", messages[0].GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
}
