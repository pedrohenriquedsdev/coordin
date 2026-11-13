using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coordin.Api.Tests.Persistence;

/// <summary>
/// Os cadastros de capacidade (TDD 4.2, 4.4): as regras de integridade são garantidas pelo próprio
/// banco, não só pela API. Aqui se tenta gravar o que não pode, direto no <c>DbContext</c>.
/// </summary>
public class CapacityRegistrySchemaTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    // Códigos SQLSTATE do PostgreSQL.
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503"; // inserir com chave estrangeira inexistente
    private const string RestrictViolation = "23001"; // excluir o que ainda é referenciado, com ON DELETE RESTRICT

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<(AsyncServiceScope Scope, CoordinDbContext Db)> NewContextAsync()
    {
        using var client = factory.CreateClient(); // sobe a API e aplica as migrations
        var scope = factory.Services.CreateAsyncScope();
        return (scope, scope.ServiceProvider.GetRequiredService<CoordinDbContext>());
    }

    private static async Task<Team> AddTeamAsync(CoordinDbContext db, string? name = null)
    {
        var team = Team.Create(name ?? Unique("Time"));
        db.Teams.Add(team);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return team;
    }

    private static async Task<Person> AddPersonAsync(CoordinDbContext db, Team team, decimal capacity = 8m)
    {
        var person = new Person { Name = Unique("Pessoa"), TeamId = team.Id, DailyCapacityHours = capacity };
        db.People.Add(person);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person;
    }

    private static string SqlStateOf(Exception exception) =>
        (exception as PostgresException ?? exception.InnerException as PostgresException)?.SqlState
        ?? throw new InvalidOperationException($"Esperava uma PostgresException, veio {exception.GetType().Name}.");

    // ---- Times ----

    [Fact]
    public async Task TheDatabaseRejects_ATeamNameThatDiffersOnlyByCase()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var name = Unique("Pagamentos");
        await AddTeamAsync(db, name);

        db.Teams.Add(Team.Create(name.ToLowerInvariant()));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(UniqueViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task ATeamName_IsStoredWithoutSurroundingSpaces_AndComparedInUpperCase()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var name = Unique("Web");

        var team = await AddTeamAsync(db, $"  {name}  ");
        var stored = await db.Teams.AsNoTracking().SingleAsync(item => item.Id == team.Id, TestContext.Current.CancellationToken);

        Assert.Equal(name, stored.Name);
        Assert.Equal(name.ToUpperInvariant(), stored.NormalizedName);
    }

    // ---- Pessoas ----

    [Theory]
    [InlineData(-0.01)]
    [InlineData(24.01)]
    [InlineData(100)]
    public async Task TheDatabaseRejects_ACapacityOutsideZeroToTwentyFour(double invalid)
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);

        db.People.Add(new Person { Name = Unique("Pessoa"), TeamId = team.Id, DailyCapacityHours = (decimal)invalid });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(8)]
    [InlineData(24)]
    public async Task TheDatabaseAccepts_CapacitiesInsideTheRange_WithTwoDecimals(double valid)
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);

        var person = await AddPersonAsync(db, team, (decimal)valid);
        var stored = await db.People.AsNoTracking().SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);

        Assert.Equal((decimal)valid, stored.DailyCapacityHours);
    }

    [Fact]
    public async Task TheDatabaseRejects_APersonWithoutAnExistingTeam()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;

        db.People.Add(new Person { Name = Unique("Pessoa"), TeamId = Guid.CreateVersion7(), DailyCapacityHours = 8m });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ForeignKeyViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseRejects_DeletingATeamThatHasPeople()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var team = await AddTeamAsync(db);
        await AddPersonAsync(db, team);

        // ExecuteDelete vai direto ao banco, sem o EF checar antes: é o banco que recusa (RESTRICT).
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => db.Teams.Where(item => item.Id == team.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        Assert.Equal(RestrictViolation, error.SqlState);
        Assert.True(await db.Teams.AnyAsync(item => item.Id == team.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingAPerson_RemovesTheirAbsences()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var person = await AddPersonAsync(db, await AddTeamAsync(db));
        db.Absences.Add(new Absence
        {
            PersonId = person.Id,
            Type = AbsenceType.Vacation,
            StartDate = new DateOnly(2027, 1, 4),
            EndDate = new DateOnly(2027, 1, 15),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await db.People.Where(item => item.Id == person.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.False(await db.Absences.AnyAsync(item => item.PersonId == person.Id, TestContext.Current.CancellationToken));
    }

    // ---- Ausências ----

    [Fact]
    public async Task TheDatabaseRejects_AnAbsenceThatEndsBeforeItStarts()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var person = await AddPersonAsync(db, await AddTeamAsync(db));

        db.Absences.Add(new Absence
        {
            PersonId = person.Id,
            Type = AbsenceType.Other,
            StartDate = new DateOnly(2027, 1, 15),
            EndDate = new DateOnly(2027, 1, 4),
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(CheckViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task TheDatabaseAccepts_AnAbsenceOfASingleDay()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var person = await AddPersonAsync(db, await AddTeamAsync(db));

        db.Absences.Add(new Absence
        {
            PersonId = person.Id,
            Type = AbsenceType.Other,
            StartDate = new DateOnly(2027, 2, 8),
            EndDate = new DateOnly(2027, 2, 8),
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheAbsenceType_IsStoredAsText_AndOnlyTheTypeAndThePeriodAreKept()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var person = await AddPersonAsync(db, await AddTeamAsync(db));
        var absence = new Absence
        {
            PersonId = person.Id,
            Type = AbsenceType.Vacation,
            StartDate = new DateOnly(2026, 12, 31),
            EndDate = new DateOnly(2027, 1, 2),
        };
        db.Absences.Add(absence);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var type = await db.Database
            .SqlQuery<string>($"SELECT \"Type\" AS \"Value\" FROM \"Absences\" WHERE \"Id\" = {absence.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);
        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'Absences' ORDER BY ordinal_position")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Vacation", type);
        // Nenhuma coluna de motivo (RNF-05): só identificação, tipo e período.
        Assert.Equal(["Id", "PersonId", "Type", "StartDate", "EndDate"], columns);
    }

    // ---- Feriados e datas de calendário ----

    [Fact]
    public async Task TheDatabaseRejects_TwoHolidaysOnTheSameDate()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var date = new DateOnly(2090, 5, 1);
        db.Holidays.Add(new Holiday { Date = date, Description = "Dia do Trabalho" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Holidays.Add(new Holiday { Date = date });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(UniqueViolation, SqlStateOf(error));
    }

    [Fact]
    public async Task ACalendarDate_RoundTripsWithoutShiftingADay()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var holiday = new Holiday { Date = new DateOnly(2091, 12, 31), Description = "Réveillon" };
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var stored = await db.Holidays.AsNoTracking().SingleAsync(item => item.Id == holiday.Id, TestContext.Current.CancellationToken);
        var asText = await db.Database
            .SqlQuery<string>($"SELECT to_char(\"Date\", 'YYYY-MM-DD') AS \"Value\" FROM \"Holidays\" WHERE \"Id\" = {holiday.Id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2091, 12, 31), stored.Date);
        Assert.Equal("2091-12-31", asText);
    }

    [Fact]
    public async Task AHolidayDescription_IsOptional()
    {
        var (scope, db) = await NewContextAsync();
        await using var _ = scope;
        var holiday = new Holiday { Date = new DateOnly(2092, 3, 3) };
        db.Holidays.Add(holiday);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Null((await db.Holidays.AsNoTracking().SingleAsync(item => item.Id == holiday.Id, TestContext.Current.CancellationToken)).Description);
    }
}
