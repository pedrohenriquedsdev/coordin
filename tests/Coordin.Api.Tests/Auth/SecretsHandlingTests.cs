using System.Net;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Auth;

/// <summary>Spec authentication: senhas e tokens nunca em texto puro, nem no banco, nem nas respostas, nem nos logs (RNF-04).</summary>
public class SecretsHandlingTests(PostgresFixture postgres)
{
    // Valores únicos o bastante para uma busca de texto não ter falso positivo.
    private const string WrongPassword = "senha-errada-unica-7f3a91";

    [Fact]
    public async Task Database_HoldsNeitherThePasswordNorTheRefreshToken_InPlainText()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var session = await client.LoginAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        // Cada linha da tabela como texto JSON: uma busca de texto vale para qualquer coluna.
        var users = await db.Database
            .SqlQueryRaw<string>("SELECT row_to_json(t)::text AS \"Value\" FROM \"AspNetUsers\" t")
            .ToListAsync(TestContext.Current.CancellationToken);
        var tokens = await db.Database
            .SqlQueryRaw<string>("SELECT row_to_json(t)::text AS \"Value\" FROM \"RefreshTokens\" t")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(users);
        Assert.NotEmpty(tokens);
        Assert.DoesNotContain(users, row => row.Contains(TestManager.Password, StringComparison.Ordinal));
        Assert.DoesNotContain(tokens, row => row.Contains(session.RefreshToken, StringComparison.Ordinal));
        Assert.Contains(users, row => row.Contains("PasswordHash", StringComparison.Ordinal));
        Assert.Contains(tokens, row => row.Contains(RefreshTokenHash(session), StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedLogin_LeaksTheSubmittedPassword_NeitherInTheResponseNorInTheLogs()
    {
        var logs = new ListLoggerProvider();
        await using var factory = new LoggingApiFactory(logs, postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var response = await client.PostLoginAsync(TestManager.Email, WrongPassword);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(WrongPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, logs.AllText, StringComparison.Ordinal);
        Assert.NotEmpty(logs.Entries); // o captador funciona: houve logs para conferir
    }

    [Fact]
    public async Task ACompleteSession_WritesNoPasswordOrTokenToTheLogs()
    {
        var logs = new ListLoggerProvider();
        await using var factory = new LoggingApiFactory(logs, postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var session = await client.LoginAsync();
        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh").WithRefreshCookie(session.RefreshToken);
        var renewed = await client.SendAsync(refresh, TestContext.Current.CancellationToken);
        var newRefreshToken = AuthTestClient.CookieValue(AuthTestClient.SetCookieHeader(renewed)!);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout").WithRefreshCookie(newRefreshToken);
        await client.SendAsync(logout, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(TestManager.Password, logs.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, logs.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(newRefreshToken, logs.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(session.AccessToken, logs.AllText, StringComparison.Ordinal);
        Assert.NotEmpty(logs.Entries);
    }

    private static string RefreshTokenHash(TestSession session) =>
        Coordin.Api.Features.Auth.RefreshTokenService.Hash(session.RefreshToken);
}
