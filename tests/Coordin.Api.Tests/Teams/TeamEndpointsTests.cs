using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Teams;

/// <summary>Spec team-registry: cadastro de times (RF-01, RF-02; DT-10, DT-18).</summary>
public class TeamEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private Task<HttpResponseMessage> CreateAsync(string? name) =>
        _client.PostAsJsonAsync("/api/teams", new { name }, TestContext.Current.CancellationToken);

    private async Task<(Guid Id, string Name)> CreatedTeamAsync(string? name = null)
    {
        var response = await CreateAsync(name ?? Unique("Time"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("name").GetString()!);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private async Task AddPersonDirectlyAsync(Guid teamId, decimal capacity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        db.People.Add(new Person { Name = Unique("Pessoa"), TeamId = teamId, DailyCapacityHours = capacity });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // ---- Cadastrar ----

    [Fact]
    public async Task CreatingATeam_Returns201_WithLocationAndAnEmptyTeam()
    {
        var name = Unique("Pagamentos");

        var response = await CreateAsync(name);
        var body = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/teams/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(name, body.GetProperty("name").GetString());
        Assert.Equal(0, body.GetProperty("peopleCount").GetInt32());
        Assert.Equal(0m, body.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task ARepeatedName_EvenInAnotherCase_Returns409WithAStableCode()
    {
        var name = Unique("Pagamentos");
        await CreatedTeamAsync(name);

        var response = await CreateAsync(name.ToLowerInvariant());
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("team-name-taken", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Já existe um time com esse nome.", body.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AnEmptyOrBlankName_Returns400WithTheErrorOnTheNameField(string? name)
    {
        var response = await CreateAsync(name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    [Fact]
    public async Task ANameWithMoreThanOneHundredCharacters_Returns400_AndExactlyOneHundredIsAccepted()
    {
        var tooLong = await CreateAsync(new string('a', 101));
        var limit = await CreateAsync(new string('b', 99) + Guid.NewGuid().ToString("N")[..1]);

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.True((await ReadAsync(tooLong)).GetProperty("errors").TryGetProperty("name", out _));
        Assert.Equal(HttpStatusCode.Created, limit.StatusCode);
    }

    [Fact]
    public async Task SpacesAroundTheName_AreDiscarded()
    {
        var name = Unique("Web");

        var response = await CreateAsync($"   {name}   ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(name, (await ReadAsync(response)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task TwoSimultaneousCreationsOfTheSameName_ExactlyOneWins()
    {
        for (var round = 1; round <= 5; round++)
        {
            var name = Unique("Corrida");

            var responses = await Task.WhenAll(CreateAsync(name), CreateAsync(name));

            var statuses = responses.Select(response => response.StatusCode).Order().ToList();
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
            var loser = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("team-name-taken", (await ReadAsync(loser)).GetProperty("code").GetString());
        }
    }

    // ---- Editar ----

    [Fact]
    public async Task RenamingATeam_Returns200WithTheNewName()
    {
        var (id, _) = await CreatedTeamAsync();
        var newName = Unique("Novo");

        var response = await _client.PutAsJsonAsync($"/api/teams/{id}", new { name = newName }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(newName, body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task SavingTheSameNameOnlyChangingTheCase_IsNotAConflictWithItself()
    {
        var (id, name) = await CreatedTeamAsync(Unique("Mobile"));

        var response = await _client.PutAsJsonAsync($"/api/teams/{id}", new { name = name.ToUpperInvariant() }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(name.ToUpperInvariant(), (await ReadAsync(response)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task RenamingToTheNameOfAnotherTeam_Returns409()
    {
        var (_, takenName) = await CreatedTeamAsync();
        var (id, _) = await CreatedTeamAsync();

        var response = await _client.PutAsJsonAsync($"/api/teams/{id}", new { name = takenName }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("team-name-taken", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task RenamingWithABlankName_Returns400_AndTheTeamKeepsItsName()
    {
        var (id, name) = await CreatedTeamAsync();

        var response = await _client.PutAsJsonAsync($"/api/teams/{id}", new { name = "  " }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        Assert.Contains(teams.EnumerateArray(), team => team.GetProperty("name").GetString() == name);
    }

    // ---- Excluir ----

    [Fact]
    public async Task DeletingATeamWithoutPeople_Returns204_AndItDisappearsFromTheList()
    {
        var (id, name) = await CreatedTeamAsync();

        var response = await _client.DeleteAsync($"/api/teams/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        Assert.DoesNotContain(teams.EnumerateArray(), team => team.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task DeletingATeamThatHasPeople_Returns409_AndNothingChanges()
    {
        var (id, name) = await CreatedTeamAsync();
        await AddPersonDirectlyAsync(id, 8m);

        var response = await _client.DeleteAsync($"/api/teams/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("team-has-people", body.GetProperty("code").GetString());
        Assert.Contains("pessoas", body.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        var team = Assert.Single(teams.EnumerateArray(), item => item.GetProperty("name").GetString() == name);
        Assert.Equal(1, team.GetProperty("peopleCount").GetInt32());
    }

    [Fact]
    public async Task AnUnknownTeam_Returns404_OnPutAndOnDelete()
    {
        var unknown = Guid.CreateVersion7();

        var put = await _client.PutAsJsonAsync($"/api/teams/{unknown}", new { name = "Qualquer" }, TestContext.Current.CancellationToken);
        var delete = await _client.DeleteAsync($"/api/teams/{unknown}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal("application/problem+json", put.Content.Headers.ContentType?.MediaType);
    }

    // ---- Capacidade derivada (DT-10) ----

    [Fact]
    public async Task TheCapacityOfATeam_IsTheSumOfItsPeople()
    {
        var (id, name) = await CreatedTeamAsync();
        foreach (var hours in new[] { 8m, 6m, 6m })
        {
            await AddPersonDirectlyAsync(id, hours);
        }

        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        var team = Assert.Single(teams.EnumerateArray(), item => item.GetProperty("name").GetString() == name);

        Assert.Equal(3, team.GetProperty("peopleCount").GetInt32());
        Assert.Equal(20m, team.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task APersonWithZeroCapacity_CountsAsAPersonButAddsNoHours()
    {
        var (id, name) = await CreatedTeamAsync();
        await AddPersonDirectlyAsync(id, 8m);
        await AddPersonDirectlyAsync(id, 0m);

        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        var team = Assert.Single(teams.EnumerateArray(), item => item.GetProperty("name").GetString() == name);

        Assert.Equal(2, team.GetProperty("peopleCount").GetInt32());
        Assert.Equal(8m, team.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task ATeamWhoseEveryPersonHasZeroCapacity_HasNoCapacity()
    {
        var (id, name) = await CreatedTeamAsync();
        await AddPersonDirectlyAsync(id, 0m);
        await AddPersonDirectlyAsync(id, 0m);

        var teams = await _client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);
        var team = Assert.Single(teams.EnumerateArray(), item => item.GetProperty("name").GetString() == name);

        Assert.Equal(2, team.GetProperty("peopleCount").GetInt32());
        Assert.Equal(0m, team.GetProperty("dailyCapacityHours").GetDecimal());
    }

    [Fact]
    public async Task RenamingATeam_ReturnsItsCurrentPeopleAndCapacity()
    {
        var (id, _) = await CreatedTeamAsync();
        await AddPersonDirectlyAsync(id, 4.5m);

        var response = await _client.PutAsJsonAsync($"/api/teams/{id}", new { name = Unique("Renomeado") }, TestContext.Current.CancellationToken);
        var body = await ReadAsync(response);

        Assert.Equal(1, body.GetProperty("peopleCount").GetInt32());
        Assert.Equal(4.5m, body.GetProperty("dailyCapacityHours").GetDecimal());
    }

    // ---- Proteção ----

    [Fact]
    public async Task EveryTeamEndpoint_RequiresALogin()
    {
        using var anonymous = factory.CreateClient();
        var id = Guid.CreateVersion7();

        var responses = new[]
        {
            await anonymous.GetAsync("/api/teams", TestContext.Current.CancellationToken),
            await anonymous.PostAsJsonAsync("/api/teams", new { name = "X" }, TestContext.Current.CancellationToken),
            await anonymous.PutAsJsonAsync($"/api/teams/{id}", new { name = "X" }, TestContext.Current.CancellationToken),
            await anonymous.DeleteAsync($"/api/teams/{id}", TestContext.Current.CancellationToken),
        };

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.All(responses, response => Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType));
    }
}

/// <summary>Spec team-registry: a lista de times (RF-01). Cada teste usa a sua API, para a lista começar vazia.</summary>
public class TeamListTests(PostgresFixture postgres)
{
    private async Task<(TestingApiFactory Factory, HttpClient Client)> NewAsync()
    {
        var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        return (factory, await factory.CreateAuthenticatedClientAsync());
    }

    [Fact]
    public async Task WithoutTeams_TheListIsEmpty()
    {
        var (factory, client) = await NewAsync();
        await using var _ = factory;
        using var __ = client;

        var response = await client.GetAsync("/api/teams", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task EightTeams_AreListedOrderedByNameIgnoringCase()
    {
        var (factory, client) = await NewAsync();
        await using var _ = factory;
        using var __ = client;
        var names = new[] { "Web", "backend", "Pagamentos", "Mobile", "dados", "Infra", "design", "Qualidade" };
        foreach (var name in names)
        {
            var created = await client.PostAsJsonAsync("/api/teams", new { name }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var body = await client.GetFromJsonAsync<JsonElement>("/api/teams", TestContext.Current.CancellationToken);

        var listed = body.EnumerateArray().Select(team => team.GetProperty("name").GetString()).ToList();
        Assert.Equal(8, listed.Count);
        Assert.Equal(["backend", "dados", "design", "Infra", "Mobile", "Pagamentos", "Qualidade", "Web"], listed);
    }
}
