using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Coordin.Api.Tests.Auth;

/// <summary>Spec authentication: login com e-mail e senha (RNF-04; DT-28).</summary>
public class LoginTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ValidCredentials_Return200_WithTheAccessTokenOnly()
    {
        var response = await _client.PostLoginAsync(TestManager.Email, TestManager.Password);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(text);
        var properties = body.RootElement.EnumerateObject().Select(property => property.Name).ToList();
        Assert.Equal(["accessToken"], properties);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(body.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(TestManager.Email, token.GetClaim("email").Value);
    }

    [Fact]
    public async Task ValidCredentials_SetTheRefreshCookie_WithTheRequiredAttributes_AndNotInTheBody()
    {
        var response = await _client.PostLoginAsync(TestManager.Email, TestManager.Password);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var setCookie = AuthTestClient.SetCookieHeader(response);
        Assert.NotNull(setCookie);
        var attributes = setCookie.ToLowerInvariant();
        Assert.Contains("httponly", attributes, StringComparison.Ordinal);
        Assert.Contains("secure", attributes, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", attributes, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", attributes, StringComparison.Ordinal);
        Assert.Contains("max-age=604800", attributes, StringComparison.Ordinal); // 7 dias

        // O valor do cookie (256 bits em base64url = 43 caracteres) nunca aparece no corpo.
        var value = AuthTestClient.CookieValue(setCookie);
        Assert.Equal(43, value.Length);
        Assert.DoesNotContain(value, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidCredentials_ProduceATokenThatTheApiAccepts()
    {
        var session = await _client.LoginAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/_test/protected").WithBearer(session.AccessToken);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmailIsMatchedIgnoringCase()
    {
        var response = await _client.PostLoginAsync(TestManager.Email.ToUpperInvariant(), TestManager.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_StoresOnlyTheHashOfTheRefreshToken_ValidForSevenDays()
    {
        var session = await _client.LoginAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var hash = RefreshTokenService.Hash(session.RefreshToken);
        var stored = await db.RefreshTokens.SingleAsync(token => token.TokenHash == hash, TestContext.Current.CancellationToken);

        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
        Assert.InRange(
            stored.ExpiresAt - DateTimeOffset.UtcNow,
            TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1),
            TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task WrongPassword_Returns401ProblemDetails_WithoutCookie()
    {
        var response = await _client.PostLoginAsync(TestManager.Email, "senha-errada-123456");
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(AuthTestClient.SetCookieHeader(response));
        using var body = JsonDocument.Parse(text);
        Assert.Equal("E-mail ou senha inválidos.", body.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("senha-errada-123456", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownEmail_GetsTheSameResponseAsAWrongPassword()
    {
        var wrongPassword = await _client.PostLoginAsync(TestManager.Email, "senha-errada-123456");
        var unknownEmail = await _client.PostLoginAsync("ninguem@coordin.test", TestManager.Password);

        Assert.Equal(wrongPassword.StatusCode, unknownEmail.StatusCode);
        Assert.Equal(await Describe(wrongPassword), await Describe(unknownEmail));
    }

    [Fact]
    public async Task MissingFields_Return400_WithErrorsPerFieldInCamelCase()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task OnlyTheEmailMissing_ReportsOnlyTheEmail()
    {
        var response = await _client.PostLoginAsync(null, TestManager.Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.False(errors.TryGetProperty("password", out _));
    }

    // Status, título e mensagem, sem o traceId (que muda a cada requisição).
    private static async Task<string> Describe(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;
        return $"{root.GetProperty("status")}|{root.GetProperty("title")}|{root.GetProperty("detail")}";
    }
}
