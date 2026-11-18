using Coordin.Api.Features.Demands;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coordin.Api.Tests.Persistence;

/// <summary>
/// Esquema dos históricos e das colunas de entrega (RF-20 a RF-22; DT-14): o banco recusa alterar e apagar um
/// registro enquanto a demanda existir, recusa uma mudança que não mudou nada, e recusa esforço real zero. A única
/// exclusão aceita é a cascata da demanda. Os testes usam SQL direto, porque o change tracker do EF não deixa chegar
/// ao banco o caso que se quer provar.
/// </summary>
public class DeliveryInsightsSchemaTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private const string CheckViolation = "23514";

    // Ranks únicos entre os testes desta classe (que compartilham o banco com as outras classes).
    private static int _nextRank = 7000;

    private static int NextRank() => Interlocked.Increment(ref _nextRank);

    private async Task<(AsyncServiceScope Scope, CoordinDbContext Db)> NewContextAsync()
    {
        using var client = factory.CreateClient(); // sobe a API e aplica as migrations
        var scope = factory.Services.CreateAsyncScope();
        return (scope, scope.ServiceProvider.GetRequiredService<CoordinDbContext>());
    }

    private static async Task<Demand> AddDemandAsync(CoordinDbContext db)
    {
        var rank = NextRank();
        var demand = new Demand { Title = $"Demanda {Guid.NewGuid():N}", PriorityRank = rank, ExecutionRank = rank };
        db.Demands.Add(demand);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return demand;
    }

    private static async Task<ForecastHistory> AddForecastChangeAsync(CoordinDbContext db, Guid demandId)
    {
        var change = new ForecastHistory
        {
            DemandId = demandId,
            PreviousEndDate = new DateOnly(2027, 3, 10),
            NewEndDate = new DateOnly(2027, 3, 17),
            Cause = "Estimate",
            ChangedAt = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero),
        };
        db.ForecastHistories.Add(change);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return change;
    }

    private static async Task<EstimateHistory> AddEstimateChangeAsync(CoordinDbContext db, Guid demandId)
    {
        var change = new EstimateHistory
        {
            DemandId = demandId,
            TeamId = Guid.NewGuid(),
            PreviousHours = 40m,
            NewHours = 60m,
            Reason = "escopo maior",
            ChangedAt = new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero),
        };
        db.EstimateHistories.Add(change);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return change;
    }

    [Fact]
    public async Task TheDatabaseRefuses_UpdatingAForecastChange()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);
        var change = await AddForecastChangeAsync(db, demand.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ForecastHistory\" SET \"Cause\" = 'Capacity' WHERE \"Id\" = {change.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
        var cause = await db.Database.SqlQuery<string>(
            $"SELECT \"Cause\" AS \"Value\" FROM \"ForecastHistory\" WHERE \"Id\" = {change.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Estimate", cause);
    }

    [Fact]
    public async Task TheDatabaseRefuses_DeletingAForecastChangeWhileTheDemandExists()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);
        var change = await AddForecastChangeAsync(db, demand.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"ForecastHistory\" WHERE \"Id\" = {change.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRefuses_UpdatingAnEstimateChange()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);
        var change = await AddEstimateChangeAsync(db, demand.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"EstimateHistory\" SET \"NewHours\" = 999 WHERE \"Id\" = {change.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRefuses_ADeletionOfAnEstimateChangeWhileTheDemandExists()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);
        var change = await AddEstimateChangeAsync(db, demand.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM \"EstimateHistory\" WHERE \"Id\" = {change.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRefuses_AForecastChangeThatChangedNothing()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);
        var same = new DateOnly(2027, 3, 10);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"ForecastHistory\" (\"Id\", \"DemandId\", \"PreviousEndDate\", \"NewEndDate\", \"Cause\", \"ChangedAt\") VALUES ({Guid.NewGuid()}, {demand.Id}, {same}, {same}, 'Estimate', now())",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRefuses_AnEstimateChangeThatChangedNothing()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"EstimateHistory\" (\"Id\", \"DemandId\", \"TeamId\", \"PreviousHours\", \"NewHours\", \"ChangedAt\") VALUES ({Guid.NewGuid()}, {demand.Id}, {Guid.NewGuid()}, 40, 40, now())",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task TheDatabaseRefuses_AZeroActualEffort()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Demands\" SET \"ActualEffortHours\" = 0 WHERE \"Id\" = {demand.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task DeletingTheDemand_CascadesItsHistory_AndNothingElse()
    {
        Guid demandId, otherId;
        {
            var (scope, db) = await NewContextAsync();
            await using var _ = scope;
            var demand = await AddDemandAsync(db);
            var other = await AddDemandAsync(db);
            await AddForecastChangeAsync(db, demand.Id);
            await AddEstimateChangeAsync(db, demand.Id);
            await AddForecastChangeAsync(db, other.Id);
            demandId = demand.Id;
            otherId = other.Id;
        }

        {
            var (scope, db) = await NewContextAsync();
            await using var _ = scope;
            await db.Demands.Where(item => item.Id == demandId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

            Assert.False(await db.ForecastHistories.AnyAsync(change => change.DemandId == demandId, TestContext.Current.CancellationToken));
            Assert.False(await db.EstimateHistories.AnyAsync(change => change.DemandId == demandId, TestContext.Current.CancellationToken));
            Assert.True(await db.ForecastHistories.AnyAsync(change => change.DemandId == otherId, TestContext.Current.CancellationToken));
        }
    }
}
