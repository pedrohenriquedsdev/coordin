using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Tests.Auth;

/// <summary>Persistência da autenticação: tabelas do Identity, <c>RefreshTokens</c> e regras do Identity (DT-28).</summary>
public class AuthenticationSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_CreatesIdentityAndRefreshTokenTables()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("AspNetUsers", tables);
        Assert.Contains("RefreshTokens", tables);
    }

    [Fact]
    public async Task RefreshTokens_RejectsTwoTokensWithTheSameHash()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "fk@coordin.test", Email = "fk@coordin.test" };
        var created = await users.CreateAsync(user, "uma-senha-de-teste-123");
        Assert.True(created.Succeeded);
        var hash = new string('a', RefreshToken.TokenHashLength);

        db.RefreshTokens.Add(NewToken(user.Id, hash));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.RefreshTokens.Add(NewToken(user.Id, hash));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Identity_IsConfiguredWithTheRulesOfTheTdd()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(AuthenticationSetup.MinimumPasswordLength, options.Password.RequiredLength);
        Assert.Equal(12, options.Password.RequiredLength);
        Assert.True(options.User.RequireUniqueEmail);
    }

    private static RefreshToken NewToken(string userId, string hash) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = hash,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
    };
}
