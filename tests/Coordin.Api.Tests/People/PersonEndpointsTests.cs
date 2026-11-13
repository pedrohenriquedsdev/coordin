using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Calendar;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.People;

/// <summary>Spec people-registry: cadastro de pessoas e da capacidade (RF-02; DT-10, DT-18).</summary>
public class PersonEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private async Task<Guid> NewTeamAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/teams", new { name = Unique("Time") }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostPersonAsync(object body) =>
        _client.PostAsJsonAsync("/api/people", body, TestContext.Current.CancellationToken);

    private async Task<Guid> NewPersonAsync(Guid teamId, decimal capacity = 8m, string? name = null)
    {
        var response = await PostPersonAsync(new { name = name ?? Unique("Pessoa"), teamId, dailyCapacityHours = capacity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> TeamAsync(Guid teamId)
    {
        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        return Assert.Single(teams.EnumerateArray(), team => team.GetProperty("id").GetGuid() == teamId);
    }

    private async Task<IReadOnlyList<JsonElement>> PeopleAsync()
    {
        var people = await _client.GetFromJsonAsync<JsonElement>("/api/people", TestContext.Current.CancellationToken);
        return [.. people.EnumerateArray()];
    }

    private static void AssertFieldError(JsonElement problem, string field)
    {
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Requisição inválida", problem.GetProperty("title").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var messages), $"sem erro no campo {field}");
        Assert.NotEmpty(messages.EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    // ---- Cadastrar ----

    [Fact]
    public async Task CreatingAPerson_Returns201_WithLocationAndTheTeamName()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("Ana");

        var response = await PostPersonAsync(new { name, teamId, dailyCapacityHours = 8 });
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/people/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(teamId, body.GetProperty("teamId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("teamName").GetString()));
        Assert.Equal(8m, body.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task PeopleWithTheSameName_AreBothAccepted()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("Ana");

        var first = await PostPersonAsync(new { name, teamId, dailyCapacityHours = 8 });
        var second = await PostPersonAsync(new { name, teamId, dailyCapacityHours = 6 });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task SpacesAroundTheName_AreDiscarded()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("Bruno");

        var response = await PostPersonAsync(new { name = $"  {name}  ", teamId, dailyCapacityHours = 8 });

        Assert.Equal(name, (await ReadAsync(response)).GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AnEmptyOrBlankName_Returns400OnTheNameField(string? name)
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name, teamId, dailyCapacityHours = 8 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "name");
    }

    [Fact]
    public async Task ANameWithMoreThanOneHundredCharacters_Returns400OnTheNameField()
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name = new string('a', 101), teamId, dailyCapacityHours = 8 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "name");
    }

    // ---- Capacidade ----

    [Fact]
    public async Task WithoutACapacity_Returns400OnTheCapacityField_AndNothingIsStored()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("SemCapacidade");

        var response = await PostPersonAsync(new { name, teamId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "dailyCapacityHours");
        Assert.DoesNotContain(await PeopleAsync(), person => person.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task ACapacityOfZero_IsAccepted_AndMeansThePersonDoesNotContribute()
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name = Unique("Diego"), teamId, dailyCapacityHours = 0 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0m, (await ReadAsync(response)).GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Theory]
    [InlineData(24)]
    [InlineData(0.5)]
    [InlineData(8.25)]
    public async Task CapacitiesInsideTheRange_AreAccepted(double hours)
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name = Unique("Pessoa"), teamId, dailyCapacityHours = hours });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((decimal)hours, (await ReadAsync(response)).GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(24.01)]
    [InlineData(100)]
    public async Task ACapacityOutsideZeroToTwentyFour_Returns400OnTheCapacityField(double hours)
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name = Unique("Pessoa"), teamId, dailyCapacityHours = hours });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "dailyCapacityHours");
    }

    [Fact]
    public async Task MoreThanTwoDecimalPlaces_Returns400OnTheCapacityField()
    {
        var teamId = await NewTeamAsync();

        var response = await PostPersonAsync(new { name = Unique("Pessoa"), teamId, dailyCapacityHours = 8.123 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "dailyCapacityHours");
    }

    // ---- Time ----

    [Fact]
    public async Task WithoutATeam_Returns400OnTheTeamIdField()
    {
        var response = await PostPersonAsync(new { name = Unique("Pessoa"), dailyCapacityHours = 8 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "teamId");
    }

    [Theory]
    [InlineData("random")]
    [InlineData("empty")]
    public async Task ATeamThatDoesNotExist_Returns400OnTheTeamIdField_AndNothingIsStored(string kind)
    {
        var name = Unique("SemTime");
        var teamId = kind == "empty" ? Guid.Empty : Guid.CreateVersion7();

        var response = await PostPersonAsync(new { name, teamId, dailyCapacityHours = 8 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "teamId");
        Assert.DoesNotContain(await PeopleAsync(), person => person.GetProperty("name").GetString() == name);
    }

    // ---- Editar ----

    [Fact]
    public async Task ChangingTheCapacity_Returns200_AndTheTeamCapacityFollows()
    {
        var teamId = await NewTeamAsync();
        var personId = await NewPersonAsync(teamId, 8m);
        await NewPersonAsync(teamId, 6m);
        Assert.Equal(14m, (await TeamAsync(teamId)).GetProperty("dailyCapacityHours").GetDecimal());

        var response = await _client.PutAsJsonAsync(
            $"/api/people/{personId}",
            new { name = Unique("Ana"), teamId, dailyCapacityHours = 4 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4m, (await ReadAsync(response)).GetProperty("dailyCapacityHours").GetDecimal());
        Assert.Equal(10m, (await TeamAsync(teamId)).GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task MovingAPersonToAnotherTeam_UpdatesBothTeams()
    {
        var origin = await NewTeamAsync();
        var destination = await NewTeamAsync();
        var personId = await NewPersonAsync(origin, 8m);
        await NewPersonAsync(origin, 6m);
        await NewPersonAsync(destination, 5m);

        var response = await _client.PutAsJsonAsync(
            $"/api/people/{personId}",
            new { name = Unique("Carla"), teamId = destination, dailyCapacityHours = 8 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(destination, (await ReadAsync(response)).GetProperty("teamId").GetGuid());
        var from = await TeamAsync(origin);
        var to = await TeamAsync(destination);
        Assert.Equal(1, from.GetProperty("peopleCount").GetInt32());
        Assert.Equal(6m, from.GetProperty("dailyCapacityHours").GetDecimal());
        Assert.Equal(2, to.GetProperty("peopleCount").GetInt32());
        Assert.Equal(13m, to.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task UpdatingAnUnknownPerson_Returns404()
    {
        var teamId = await NewTeamAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/people/{Guid.CreateVersion7()}",
            new { name = "Ninguém", teamId, dailyCapacityHours = 8 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdatingToATeamThatDoesNotExist_Returns400_AndThePersonDoesNotChange()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("Elisa");
        var personId = await NewPersonAsync(teamId, 8m, name);

        var response = await _client.PutAsJsonAsync(
            $"/api/people/{personId}",
            new { name, teamId = Guid.CreateVersion7(), dailyCapacityHours = 8 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "teamId");
        var person = Assert.Single(await PeopleAsync(), item => item.GetProperty("id").GetGuid() == personId);
        Assert.Equal(teamId, person.GetProperty("teamId").GetGuid());
    }

    [Fact]
    public async Task UpdatingWithAnInvalidCapacity_Returns400_AndThePersonDoesNotChange()
    {
        var teamId = await NewTeamAsync();
        var name = Unique("Fábio");
        var personId = await NewPersonAsync(teamId, 8m, name);

        var response = await _client.PutAsJsonAsync(
            $"/api/people/{personId}",
            new { name, teamId, dailyCapacityHours = 30 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertFieldError(await ReadAsync(response), "dailyCapacityHours");
        var person = Assert.Single(await PeopleAsync(), item => item.GetProperty("id").GetGuid() == personId);
        Assert.Equal(8m, person.GetProperty("dailyCapacityHours").GetDecimal());
    }

    // ---- Excluir ----

    [Fact]
    public async Task DeletingAPerson_Returns204_RemovesTheirAbsences_AndLowersTheTeamCapacity()
    {
        var teamId = await NewTeamAsync();
        var personId = await NewPersonAsync(teamId, 8m);
        await NewPersonAsync(teamId, 6m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            db.Absences.Add(new Absence
            {
                PersonId = personId,
                Type = AbsenceType.Vacation,
                StartDate = new DateOnly(2027, 1, 4),
                EndDate = new DateOnly(2027, 1, 15),
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _client.DeleteAsync($"/api/people/{personId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await PeopleAsync(), person => person.GetProperty("id").GetGuid() == personId);
        await using var check = factory.Services.CreateAsyncScope();
        var context = check.ServiceProvider.GetRequiredService<CoordinDbContext>();
        Assert.False(await context.Absences.AnyAsync(absence => absence.PersonId == personId, TestContext.Current.CancellationToken));
        var team = await TeamAsync(teamId);
        Assert.Equal(1, team.GetProperty("peopleCount").GetInt32());
        Assert.Equal(6m, team.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task DeletingAnUnknownPerson_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/people/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Proteção ----

    [Fact]
    public async Task EveryPersonEndpoint_RequiresALogin()
    {
        using var anonymous = factory.CreateClient();
        var id = Guid.CreateVersion7();
        var body = new { name = "X", teamId = id, dailyCapacityHours = 8 };

        var responses = new[]
        {
            await anonymous.GetAsync("/api/people", TestContext.Current.CancellationToken),
            await anonymous.PostAsJsonAsync("/api/people", body, TestContext.Current.CancellationToken),
            await anonymous.PutAsJsonAsync($"/api/people/{id}", body, TestContext.Current.CancellationToken),
            await anonymous.DeleteAsync($"/api/people/{id}", TestContext.Current.CancellationToken),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
    }
}

/// <summary>Spec people-registry: a lista de pessoas em ordem (RF-02). Usa uma API própria, para a lista começar vazia.</summary>
public class PersonListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ThePeopleAreOrderedByTeamAndThenByNameIgnoringCase()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var web = await CreateTeamAsync(client, "Web");
        var backend = await CreateTeamAsync(client, "backend");
        foreach (var (team, name) in new[] { (web, "Zélia"), (backend, "carla"), (web, "ana"), (backend, "Bruno"), (web, "Ana") })
        {
            var created = await client.PostAsJsonAsync(
                "/api/people",
                new { name, teamId = team, dailyCapacityHours = 8 },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/people", TestContext.Current.CancellationToken);

        var listed = body.EnumerateArray()
            .Select(person => $"{person.GetProperty("teamName").GetString()}/{person.GetProperty("name").GetString()}")
            .ToList();
        // Os dois "ana" (em caixas diferentes) ficam juntos; a ordem entre eles não importa.
        Assert.Equal("backend/Bruno", listed[0]);
        Assert.Equal("backend/carla", listed[1]);
        Assert.Equal(["Web/ana", "Web/Ana"], listed.Skip(2).Take(2).Order().ToArray());
        Assert.Equal("Web/Zélia", listed[4]);
    }

    private static async Task<Guid> CreateTeamAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/teams", new { name }, TestContext.Current.CancellationToken);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
    }
}
