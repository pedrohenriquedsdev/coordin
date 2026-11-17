using Coordin.Api.Features.Demands;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coordin.Api.Tests.Persistence;

/// <summary>
/// Esquema das dependências entre demandas (RF-18; DT-23; design D6): o banco garante a unicidade da
/// aresta, que ninguém depende de si mesmo, e as exclusões: a dependente sai com as suas arestas e o
/// pré-requisito não sai enquanto alguém depender dele. Os testes gravam direto no <c>DbContext</c>.
/// </summary>
public class DemandDependencySchemaTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string RestrictViolation = "23001";

    // Ranks únicos entre os testes desta classe (que compartilham o banco).
    private static int _nextRank = 5000;

    private static int NextRank() => Interlocked.Increment(ref _nextRank);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<(AsyncServiceScope Scope, CoordinDbContext Db)> NewContextAsync()
    {
        using var client = factory.CreateClient(); // sobe a API e aplica as migrations
        var scope = factory.Services.CreateAsyncScope();
        return (scope, scope.ServiceProvider.GetRequiredService<CoordinDbContext>());
    }

    private static async Task<Demand> AddDemandAsync(CoordinDbContext db)
    {
        var rank = NextRank();
        var demand = new Demand { Title = Unique("Demanda"), PriorityRank = rank, ExecutionRank = rank };
        db.Demands.Add(demand);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return demand;
    }

    private static async Task<DemandDependency> AddDependencyAsync(CoordinDbContext db, Guid demandId, Guid prerequisiteId)
    {
        var dependency = new DemandDependency { DemandId = demandId, PrerequisiteDemandId = prerequisiteId };
        db.DemandDependencies.Add(dependency);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return dependency;
    }

    [Fact]
    public async Task TheDatabaseRejects_TheSameDependencyTwice()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var a = await AddDemandAsync(db);
        var b = await AddDemandAsync(db);
        await AddDependencyAsync(db, b.Id, a.Id);

        // SQL direto: o change tracker do EF recusaria a chave repetida antes do banco.
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"DemandDependencies\" (\"DemandId\", \"PrerequisiteDemandId\") VALUES ({b.Id}, {a.Id})",
                TestContext.Current.CancellationToken));

        Assert.Equal(UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRejects_ADemandThatDependsOnItself()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var a = await AddDemandAsync(db);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"DemandDependencies\" (\"DemandId\", \"PrerequisiteDemandId\") VALUES ({a.Id}, {a.Id})",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task DeletingTheDependent_RemovesItsDependencies_AndLeavesThePrerequisite()
    {
        Guid a, b;
        {
            var (scope, db) = await NewContextAsync();
            await using var _ = scope;
            a = (await AddDemandAsync(db)).Id;
            b = (await AddDemandAsync(db)).Id;
            await AddDependencyAsync(db, b, a);
        }

        {
            var (scope, db) = await NewContextAsync();
            await using var _ = scope;
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Demands\" WHERE \"Id\" = {b}", TestContext.Current.CancellationToken);
        }

        var (checkScope, check) = await NewContextAsync();
        await using var __ = checkScope;
        Assert.False(await check.DemandDependencies.AnyAsync(d => d.DemandId == b, TestContext.Current.CancellationToken));
        Assert.True(await check.Demands.AnyAsync(d => d.Id == a, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingAPrerequisite_IsRestricted_WhileAnotherDemandDependsOnIt()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var a = await AddDemandAsync(db);
        var b = await AddDemandAsync(db);
        await AddDependencyAsync(db, b.Id, a.Id);

        var (deleteScope, deleteDb) = await NewContextAsync();
        await using var __ = deleteScope;
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            deleteDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Demands\" WHERE \"Id\" = {a.Id}", TestContext.Current.CancellationToken));

        Assert.Equal(RestrictViolation, error.SqlState);
    }
}
