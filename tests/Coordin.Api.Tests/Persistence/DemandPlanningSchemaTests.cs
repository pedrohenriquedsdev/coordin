using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coordin.Api.Tests.Persistence;

/// <summary>
/// Esquema do planejamento (TDD 4.2, 4.4): o banco garante rankings únicos, datas coerentes e as
/// exclusões em cascata ou restritas. Os testes gravam direto no <c>DbContext</c>.
/// </summary>
public class DemandPlanningSchemaTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";
    private const string RestrictViolation = "23001";
    private const string StringTruncation = "22001";

    // Ranks únicos entre os testes desta classe (que compartilham o banco).
    private static int _nextRank = 1000;

    private static int NextRank() => Interlocked.Increment(ref _nextRank);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<(AsyncServiceScope Scope, CoordinDbContext Db)> NewContextAsync()
    {
        using var client = factory.CreateClient(); // sobe a API e aplica as migrations
        var scope = factory.Services.CreateAsyncScope();
        return (scope, scope.ServiceProvider.GetRequiredService<CoordinDbContext>());
    }

    private static async Task<Team> AddTeamAsync(CoordinDbContext db)
    {
        var team = Team.Create(Unique("Time"));
        db.Teams.Add(team);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return team;
    }

    private static async Task<Person> AddPersonAsync(CoordinDbContext db, Team team)
    {
        var person = new Person { Name = Unique("Pessoa"), TeamId = team.Id, DailyCapacityHours = 8m };
        db.People.Add(person);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person;
    }

    private static async Task<Demand> AddDemandAsync(CoordinDbContext db, DateOnly? start = null, DateOnly? completed = null)
    {
        var rank = NextRank();
        var demand = new Demand
        {
            Title = Unique("Demanda"),
            PriorityRank = rank,
            ExecutionRank = rank,
            StartDate = start,
            CompletedOn = completed,
        };
        db.Demands.Add(demand);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return demand;
    }

    private static string SqlStateOf(Exception exception) =>
        (exception as PostgresException ?? exception.InnerException as PostgresException)?.SqlState
        ?? throw new InvalidOperationException($"Esperava uma PostgresException, veio {exception.GetType().Name}.");

    private static async Task<DbUpdateException> SaveAndFailAsync(CoordinDbContext db) =>
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

    // ---- Demandas ----

    [Fact]
    public async Task TheDatabaseRejects_TwoDemandsWithTheSamePriorityRank()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var rank = NextRank();
        db.Demands.Add(new Demand { Title = Unique("A"), PriorityRank = rank, ExecutionRank = NextRank() });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Demands.Add(new Demand { Title = Unique("B"), PriorityRank = rank, ExecutionRank = NextRank() });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(UniqueViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_TwoDemandsWithTheSameExecutionRank()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var rank = NextRank();
        db.Demands.Add(new Demand { Title = Unique("A"), PriorityRank = NextRank(), ExecutionRank = rank });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Demands.Add(new Demand { Title = Unique("B"), PriorityRank = NextRank(), ExecutionRank = rank });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(UniqueViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_ARankBelowOne()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        db.Demands.Add(new Demand { Title = Unique("A"), PriorityRank = 0, ExecutionRank = NextRank() });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_ACompletionBeforeTheStart()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        db.Demands.Add(new Demand
        {
            Title = Unique("A"),
            PriorityRank = NextRank(),
            ExecutionRank = NextRank(),
            StartDate = new DateOnly(2026, 12, 10),
            CompletedOn = new DateOnly(2026, 12, 9),
        });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_ATitleLongerThan200Characters()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        db.Demands.Add(new Demand { Title = new string('x', 201), PriorityRank = NextRank(), ExecutionRank = NextRank() });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(StringTruncation, SqlStateOf(error));
    }

    // ---- Partes e alocações ----

    [Fact]
    public async Task TheDatabaseRejects_AnEstimateOfZeroOrLess()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);
        var demand = await AddDemandAsync(db);

        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = 0m });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseAccepts_AnEmptyEstimate()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);
        var demand = await AddDemandAsync(db);

        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = null });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(await db.DemandTeams.AsNoTracking().AnyAsync(part => part.DemandId == demand.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheDatabaseRejects_APartForAnUnknownTeam()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);

        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = Guid.NewGuid(), EstimatedEffortHours = 8m });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(ForeignKeyViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_AnAssignmentOfAnUnknownPerson()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);

        db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = Guid.NewGuid() });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(ForeignKeyViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task DeletingADemand_RemovesItsParts_AssignmentsAndForecast()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);
        var person = await AddPersonAsync(db, team);
        var demand = await AddDemandAsync(db);
        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = 8m });
        db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = person.Id });
        db.Forecasts.Add(new Forecast { DemandId = demand.Id, IsPredictable = false, UnpredictableReason = "MissingStartDate", CalculatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await db.Demands.Where(item => item.Id == demand.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.False(await db.DemandTeams.AnyAsync(part => part.DemandId == demand.Id, TestContext.Current.CancellationToken));
        Assert.False(await db.DemandAssignments.AnyAsync(item => item.DemandId == demand.Id, TestContext.Current.CancellationToken));
        Assert.False(await db.Forecasts.AnyAsync(item => item.DemandId == demand.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheDatabaseRestricts_DeletingATeamThatIsPartOfADemand()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);
        var demand = await AddDemandAsync(db);
        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = 8m });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // ExecuteDeleteAsync não embrulha o erro em DbUpdateException: vem a PostgresException direto.
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await db.Teams.Where(item => item.Id == team.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal(RestrictViolation, SqlStateOf(error));
    }

    // ---- Previsão ----

    [Fact]
    public async Task TheDatabaseRejects_AForecastEndingBeforeTheEndWithoutMargin()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var demand = await AddDemandAsync(db);

        db.Forecasts.Add(new Forecast
        {
            DemandId = demand.Id,
            IsPredictable = true,
            ForecastStartDate = new DateOnly(2026, 12, 1),
            EndDateWithoutMargin = new DateOnly(2026, 12, 10),
            ForecastEndDate = new DateOnly(2026, 12, 9),
            CalculatedAt = DateTimeOffset.UtcNow,
        });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    // ---- Configurações ----

    [Fact]
    public async Task ThePlanningSettings_HaveOneRowWithFollowPriorityAndTwentyPercent()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        var settings = await db.PlanningSettings.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PlanningSettings.SingletonId, settings.Id);
        Assert.Equal(ExecutionOrderMode.FollowPriority, settings.ExecutionOrderMode);
        Assert.Equal(20m, settings.SafetyMarginPercent);
    }

    [Fact]
    public async Task TheDatabaseRejects_ASecondPlanningSettingsRow()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        db.PlanningSettings.Add(new PlanningSettings { Id = 2, ExecutionOrderMode = ExecutionOrderMode.FollowPriority, SafetyMarginPercent = 10m });
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_ANegativeSafetyMargin()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var settings = await db.PlanningSettings.SingleAsync(TestContext.Current.CancellationToken);

        settings.SafetyMarginPercent = -1m;
        var error = await SaveAndFailAsync(db);

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }
}
