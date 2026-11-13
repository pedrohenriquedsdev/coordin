using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Auth;

/// <summary>
/// Spec authentication: no máximo 10 requisições por minuto por IP em <c>login</c> e <c>refresh</c> (DT-28).
/// Cada teste usa a sua própria API, porque o contador do limite vive na instância. Os testes não esperam
/// o minuto virar: provam o limite e a recusa, não a janela.
/// </summary>
public class RateLimitTests(PostgresFixture postgres)
{
    // E-mail inexistente: responde 401 sem nunca bloquear a conta, então só o limite entra em jogo.
    private const string UnknownEmail = "ninguem@coordin.test";

    [Fact]
    public async Task TheEleventhLoginInAMinute_Returns429ProblemDetails_WithRetryAfter()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var allowed = await client.PostLoginAsync(UnknownEmail, "qualquer-senha-123");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode); // 401 da credencial, não 429
        }

        var limited = await client.PostLoginAsync(UnknownEmail, "qualquer-senha-123");
        var text = await limited.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.InRange(int.Parse(retryAfter.Single(), System.Globalization.CultureInfo.InvariantCulture), 1, 60);

        using var body = JsonDocument.Parse(text);
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Muitas requisições", body.RootElement.GetProperty("title").GetString());
        Assert.Contains("Aguarde", body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task TheRejection_StillCarriesTheSecurityHeaders()
    {
        await using var factory = await NewFactoryAsync("1");
        using var client = factory.CreateClient();
        await client.PostLoginAsync(UnknownEmail, "qualquer-senha-123");

        var limited = await client.PostLoginAsync(UnknownEmail, "qualquer-senha-123");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", limited.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task TheRefreshEndpoint_IsLimitedToo()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await RefreshAsync(client)).StatusCode);
    }

    [Fact]
    public async Task LoginAndRefresh_ShareTheSameBudget()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await client.PostLoginAsync(UnknownEmail, "qualquer-senha-123");
            await RefreshAsync(client);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostLoginAsync(UnknownEmail, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await RefreshAsync(client)).StatusCode);
    }

    [Fact]
    public async Task OtherEndpoints_AreNotLimited()
    {
        await using var factory = await NewFactoryAsync();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 15; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", content: null, TestContext.Current.CancellationToken)).StatusCode);
        }
    }

    [Fact]
    public async Task TheLimitIsPerIp_SoAnotherClientIsNotAffected()
    {
        await using var factory = await NewFactoryAsync("2");
        using var client = factory.CreateClient();

        // O IP chega pelo X-Forwarded-For, como na hospedagem (o UseForwardedHeaders da change 1 o lê).
        await LoginFromAsync(client, "203.0.113.10");
        await LoginFromAsync(client, "203.0.113.10");
        var blocked = await LoginFromAsync(client, "203.0.113.10");
        var anotherClient = await LoginFromAsync(client, "203.0.113.20");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anotherClient.StatusCode);
    }

    [Fact]
    public async Task TheLimitCanBeAdjustedByConfiguration()
    {
        await using var factory = await NewFactoryAsync("3");
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostLoginAsync(UnknownEmail, "x")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostLoginAsync(UnknownEmail, "x")).StatusCode);
    }

    // permitLimit nulo = sem configuração: vale o padrão do TDD (10). A fábrica de testes eleva o limite
    // para 1000, então aqui ele é desfeito explicitamente.
    private async Task<CoordinApiFactory> NewFactoryAsync(string? permitLimit = null)
    {
        var factory = new CoordinApiFactory(
            "Testing",
            postgres,
            configuration: new Dictionary<string, string?> { ["Auth:RateLimit:PermitLimit"] = permitLimit });
        await factory.InitializeAsync();
        return factory;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client) =>
        client.PostAsync("/api/auth/refresh", content: null, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = UnknownEmail, password = "qualquer-senha-123" }),
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
