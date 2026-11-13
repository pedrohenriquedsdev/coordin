using System.Net;
using System.Text.Json;
using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Auth;

/// <summary>
/// Spec authentication: renovação com rotação, reuso que derruba a sessão e logout (DT-28, RT-07).
/// Cada teste faz o próprio login, então os refresh tokens de um teste não interferem nos de outro.
/// </summary>
public class RefreshTokenTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---- Renovação (task 6.1) ----

    [Fact]
    public async Task ValidRefreshToken_ReturnsANewAccessTokenAndANewCookie()
    {
        var session = await _client.LoginAsync();

        var response = await RefreshAsync(session.RefreshToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("accessToken", Assert.Single(body.RootElement.EnumerateObject()).Name);

        var setCookie = AuthTestClient.SetCookieHeader(response);
        Assert.NotNull(setCookie);
        var newValue = AuthTestClient.CookieValue(setCookie);
        Assert.NotEqual(session.RefreshToken, newValue);
        Assert.DoesNotContain(newValue, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheNewCookie_HasTheSameRestrictionsAsTheLoginCookie()
    {
        var session = await _client.LoginAsync();

        var response = await RefreshAsync(session.RefreshToken);

        var attributes = AuthTestClient.SetCookieHeader(response)!.ToLowerInvariant();
        Assert.Contains("httponly", attributes, StringComparison.Ordinal);
        Assert.Contains("secure", attributes, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", attributes, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", attributes, StringComparison.Ordinal);
        Assert.Contains("max-age=604800", attributes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheNewAccessToken_IsAcceptedByTheApi()
    {
        var session = await _client.LoginAsync();
        var refreshed = await RefreshAsync(session.RefreshToken);
        var accessToken = (await refreshed.Content.ReadFromJsonElementAsync())
            .GetProperty("accessToken").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithBearer(accessToken);
        var me = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Rotation_MarksTheOldTokenAsReplaced_AndLinksItToTheNewOne()
    {
        var session = await _client.LoginAsync();

        var response = await RefreshAsync(session.RefreshToken);
        var newValue = AuthTestClient.CookieValue(AuthTestClient.SetCookieHeader(response)!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var oldHash = RefreshTokenService.Hash(session.RefreshToken);
        var newHash = RefreshTokenService.Hash(newValue);
        var oldToken = await db.RefreshTokens.SingleAsync(token => token.TokenHash == oldHash, TestContext.Current.CancellationToken);
        var newToken = await db.RefreshTokens.SingleAsync(token => token.TokenHash == newHash, TestContext.Current.CancellationToken);

        Assert.NotNull(oldToken.RevokedAt);
        Assert.Equal(newToken.Id, oldToken.ReplacedById);
        Assert.Null(newToken.RevokedAt);
        Assert.Null(newToken.ReplacedById);
    }

    [Fact]
    public async Task ATokenThatWasAlreadyReplaced_NoLongerWorks()
    {
        var session = await _client.LoginAsync();
        await RefreshAsync(session.RefreshToken);

        var again = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task WithoutTheCookie_Returns401ProblemDetails_AndDeletesTheCookie()
    {
        var response = await RefreshAsync(null);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Sessão inválida ou expirada", text, StringComparison.Ordinal);
        AssertCookieDeleted(response);
    }

    [Fact]
    public async Task AnUnknownToken_Returns401()
    {
        var response = await RefreshAsync("um-valor-que-nao-existe-no-banco");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCookieDeleted(response);
    }

    [Fact]
    public async Task AnExpiredToken_Returns401_AndDeletesTheCookie()
    {
        const string value = "valor-de-um-token-expirado-so-para-teste";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = RefreshTokenService.Hash(value),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-8), // emitido há mais de 7 dias
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await RefreshAsync(value);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCookieDeleted(response);
    }

    // ---- Reuso e logout (task 6.2) ----

    [Fact]
    public async Task ReusingAReplacedToken_RevokesEveryRefreshTokenOfTheUser()
    {
        var stolen = await _client.LoginAsync(); // o token que será "roubado"
        var otherDevice = await _client.LoginAsync(); // outra sessão legítima do gestor
        var legitimate = await RefreshAsync(stolen.RefreshToken); // o gestor renova: troca o token
        var newest = AuthTestClient.CookieValue(AuthTestClient.SetCookieHeader(legitimate)!);

        var replay = await RefreshAsync(stolen.RefreshToken); // o ladrão apresenta o token antigo

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(newest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(otherDevice.RefreshToken)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        Assert.All(
            await db.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken),
            token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task Logout_WithAnActiveSession_Returns204_DeletesTheCookie_AndStopsTheRefresh()
    {
        var session = await _client.LoginAsync();

        var logout = await LogoutAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertCookieDeleted(logout);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsOnlyThatSession_AndReusingItDoesNotAffectTheOthers()
    {
        var closing = await _client.LoginAsync();
        var staying = await _client.LoginAsync();
        await LogoutAsync(closing.RefreshToken);

        var reuse = await RefreshAsync(closing.RefreshToken);
        var other = await RefreshAsync(staying.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("um-valor-que-nao-existe-no-banco")]
    public async Task Logout_WithoutASessionOrWithAnUnknownToken_Returns204(string? cookie)
    {
        var response = await LogoutAsync(cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_IsIdempotent_AndNeedsNoAccessToken()
    {
        var session = await _client.LoginAsync();

        // Nenhuma das chamadas leva Authorization: o token de acesso pode já ter expirado.
        var first = await LogoutAsync(session.RefreshToken);
        var second = await LogoutAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task TwoSimultaneousRefreshes_WithTheSameCookie_OnlyOneSucceeds()
    {
        // Várias rodadas para dar chance real à corrida; o resultado correto é o mesmo se as
        // chamadas por acaso chegarem em sequência.
        for (var round = 1; round <= 5; round++)
        {
            var session = await _client.LoginAsync();

            var responses = await Task.WhenAll(
                RefreshAsync(session.RefreshToken),
                RefreshAsync(session.RefreshToken));

            var statuses = responses.Select(response => response.StatusCode).Order().ToList();
            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Unauthorized], statuses);
        }
    }

    // ---- Auxiliares ----

    private Task<HttpResponseMessage> RefreshAsync(string? cookie) => PostAsync("/api/auth/refresh", cookie);

    private Task<HttpResponseMessage> LogoutAsync(string? cookie) => PostAsync("/api/auth/logout", cookie);

    private Task<HttpResponseMessage> PostAsync(string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (cookie is not null)
        {
            request.WithRefreshCookie(cookie);
        }

        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // O navegador só apaga um cookie se o Set-Cookie repetir o mesmo Path e já vier vencido.
    private static void AssertCookieDeleted(HttpResponseMessage response)
    {
        var setCookie = AuthTestClient.SetCookieHeader(response);
        Assert.NotNull(setCookie);
        var attributes = setCookie.ToLowerInvariant();
        Assert.StartsWith("refresh_token=;", attributes, StringComparison.Ordinal);
        Assert.Contains("expires=thu, 01 jan 1970", attributes, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", attributes, StringComparison.Ordinal);
    }
}

internal static class HttpContentJsonExtensions
{
    public static async Task<JsonElement> ReadFromJsonElementAsync(this HttpContent content) =>
        await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<JsonElement>(
            content,
            cancellationToken: TestContext.Current.CancellationToken);
}
