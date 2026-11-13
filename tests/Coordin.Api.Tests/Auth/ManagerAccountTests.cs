using System.Net;
using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coordin.Api.Tests.Auth;

/// <summary>Spec authentication: conta do gestor criada por configuração (DT-29, RNF-04, RNF-05).</summary>
public class ManagerAccountTests(PostgresFixture postgres)
{
    // Valores fictícios, só destes testes.
    private const string AnotherPassword = "outra-senha-de-teste-99";
    private const string ShortPassword = "curta-123";

    [Fact]
    public async Task FirstStartup_CreatesTheManagerAccount_WithAHashedPassword()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(TestManager.Email);

        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
        Assert.True(await users.CheckPasswordAsync(user, TestManager.Password));
        Assert.NotEqual(TestManager.Password, user.PasswordHash);
        Assert.Single(users.Users);
    }

    [Fact]
    public async Task ExistingAccount_KeepsItsPassword_WhenResetIsNotRequested()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await StartAsync(database.ConnectionString);

        await using var factory = Factory(database.ConnectionString, new() { ["Manager:Password"] = AnotherPassword });
        using var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(TestManager.Email);
        Assert.NotNull(user);
        Assert.True(await users.CheckPasswordAsync(user, TestManager.Password));
        Assert.False(await users.CheckPasswordAsync(user, AnotherPassword));
        Assert.Single(users.Users);
    }

    [Fact]
    public async Task ResetPassword_ReplacesThePassword_ClearsLockoutAndRevokesRefreshTokens()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using (var first = Factory(database.ConnectionString))
        {
            using var firstClient = first.CreateClient();
            await using var scope = first.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var user = (await users.FindByEmailAsync(TestManager.Email))!;

            await users.AccessFailedAsync(user);
            await users.AccessFailedAsync(user);
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = new string('b', RefreshToken.TokenHashLength),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var logs = new ListLoggerProvider();
        await using var factory = Factory(
            database.ConnectionString,
            new() { ["Manager:ResetPassword"] = "true", ["Manager:Password"] = AnotherPassword },
            logs);
        using var client = factory.CreateClient();

        await using var check = factory.Services.CreateAsyncScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokens = check.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var reset = (await manager.FindByEmailAsync(TestManager.Email))!;
        Assert.True(await manager.CheckPasswordAsync(reset, AnotherPassword));
        Assert.False(await manager.CheckPasswordAsync(reset, TestManager.Password));
        Assert.Null(reset.LockoutEnd);
        Assert.Equal(0, reset.AccessFailedCount);
        Assert.All(await tokens.RefreshTokens.ToListAsync(TestContext.Current.CancellationToken), token => Assert.NotNull(token.RevokedAt));
        Assert.DoesNotContain(AnotherPassword, logs.AllText, StringComparison.Ordinal);
        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("ResetPassword", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShortPassword_OnCreation_FailsStartup_WithoutRepeatingThePassword()
    {
        await using var factory = new CoordinApiFactory(
            "Testing",
            postgres,
            configuration: new Dictionary<string, string?> { ["Manager:Password"] = ShortPassword });
        await factory.InitializeAsync();

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var text = Flatten(error);
        Assert.Contains("Manager:Password", text, StringComparison.Ordinal);
        Assert.DoesNotContain(ShortPassword, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortPassword_OnReset_FailsStartup_AndKeepsTheCurrentPassword()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await StartAsync(database.ConnectionString);

        await using (var failing = Factory(
            database.ConnectionString,
            new() { ["Manager:ResetPassword"] = "true", ["Manager:Password"] = ShortPassword }))
        {
            var error = Assert.ThrowsAny<Exception>(() => failing.CreateClient());
            Assert.DoesNotContain(ShortPassword, Flatten(error), StringComparison.Ordinal);
        }

        // O gestor não pode ficar trancado para fora por uma redefinição inválida.
        await using var factory = Factory(database.ConnectionString);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(TestManager.Email))!;
        Assert.True(await users.CheckPasswordAsync(user, TestManager.Password));
    }

    [Fact]
    public async Task ResetPassword_WithoutANewPassword_FailsStartup()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await StartAsync(database.ConnectionString);

        await using var factory = Factory(
            database.ConnectionString,
            new() { ["Manager:ResetPassword"] = "true", ["Manager:Password"] = null });

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Manager:ResetPassword", Flatten(error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfiguration_StartsTheApi_LogsAWarning_AndCreatesNoAccount()
    {
        var logs = new ListLoggerProvider();
        await using var factory = Factory(
            (await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken)).ConnectionString,
            new() { ["Manager:Email"] = null, ["Manager:Password"] = null },
            logs);
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(users.Users);
        Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Manager:Email", StringComparison.Ordinal));
    }

    private static async Task StartAsync(string connectionString)
    {
        await using var factory = Factory(connectionString);
        using var client = factory.CreateClient();
    }

    private static LoggingFactory Factory(
        string connectionString,
        Dictionary<string, string?>? configuration = null,
        ListLoggerProvider? logs = null) => new(connectionString, configuration, logs);

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }

    /// <summary>Uma <see cref="CoordinApiFactory"/> sobre um banco já criado, com logs opcionais capturados.</summary>
    private sealed class LoggingFactory : CoordinApiFactory
    {
        private readonly ListLoggerProvider? _logs;

        public LoggingFactory(
            string connectionString,
            Dictionary<string, string?>? configuration,
            ListLoggerProvider? logs)
            : base("Testing", connectionString: connectionString, configuration: configuration)
        {
            _logs = logs;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            if (_logs is not null)
            {
                builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            }
        }
    }
}
