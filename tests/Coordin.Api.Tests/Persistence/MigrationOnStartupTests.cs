using System.Net;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Persistence;

/// <summary>Spec service-health: o esquema do banco é atualizado na inicialização (DT-07).</summary>
public class MigrationOnStartupTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EmptyDatabase_GetsInitialMigrationApplied_AndHealthIsOk()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient(); // sobe a aplicação e aplica as migrations

        var health = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var applied = Assert.Single(await AppliedMigrationsAsync(factory));
        Assert.EndsWith("_InitialCreate", applied, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlreadyMigratedDatabase_RestartsWithoutReapplyingMigrations()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        // A primeira execução migra; a segunda reinicia sobre o banco já atualizado.
        for (var run = 1; run <= 2; run++)
        {
            await using var factory = new CoordinApiFactory("Testing", connectionString: database.ConnectionString);
            using var client = factory.CreateClient();

            var health = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Single(await AppliedMigrationsAsync(factory));
        }
    }

    private static async Task<IReadOnlyList<string>> AppliedMigrationsAsync(CoordinApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        return applied.ToList();
    }
}
