using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>Spec demand-registry: cadastro e consulta de demandas (RF-04 a RF-06; DT-17).</summary>
public class DemandCreateTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private static readonly string[] ExpectedTitles = ["Primeira", "Segunda", "Terceira"];

    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        await PlanningTestData.ClearDemandsAsync(factory);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private Task<HttpResponseMessage> PostAsync(object body) => _client.PostAsJsonAsync("/api/demands", body, Ct);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task CreateFullDemand_Returns201WithLocation_AndGetReturnsTheSameDemand()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var body = PlanningTestData.Body("Novo checkout", [(team, 80m)], [person], PlanningTestData.StartMonday, new DateOnly(2027, 2, 1));

        var response = await PostAsync(body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await ReadAsync(response);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Novo checkout", created.GetProperty("title").GetString());
        Assert.Equal(1, created.GetProperty("priorityRank").GetInt32());
        Assert.Equal(1, created.GetProperty("executionRank").GetInt32());

        var fetched = await ReadAsync(await _client.GetAsync($"/api/demands/{id}", Ct));
        Assert.Equal(id, fetched.GetProperty("id").GetGuid());
        Assert.Equal(80m, fetched.GetProperty("teams")[0].GetProperty("estimatedEffortHours").GetDecimal());
        Assert.Equal(person, fetched.GetProperty("assignedPersonIds")[0].GetGuid());
    }

    [Fact]
    public async Task DemandWithoutEstimate_IsAccepted()
    {
        var team = await _client.NewTeamAsync();

        var response = await PostAsync(PlanningTestData.Body("Sem estimativa", [(team, null)]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(response)).GetProperty("teams")[0].GetProperty("estimatedEffortHours").ValueKind);
    }

    [Fact]
    public async Task DemandWithoutTeams_IsAccepted()
    {
        var response = await PostAsync(PlanningTestData.Body("Só o título", []));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, (await ReadAsync(response)).GetProperty("teams").GetArrayLength());
    }

    [Fact]
    public async Task List_IsInPriorityOrder()
    {
        var team = await _client.NewTeamAsync();
        foreach (var title in new[] { "Primeira", "Segunda", "Terceira" })
        {
            Assert.Equal(HttpStatusCode.Created, (await PostAsync(PlanningTestData.Body(title, [(team, 8m)]))).StatusCode);
        }

        var list = await ReadAsync(await _client.GetAsync("/api/demands", Ct));

        var titles = list.EnumerateArray().Select(item => item.GetProperty("title").GetString()).ToList();
        Assert.Equal(ExpectedTitles, titles);
    }

    [Fact]
    public async Task UnknownDemand_Returns404()
    {
        var response = await _client.GetAsync($"/api/demands/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankTitle_Returns400_WithTheFieldError(string title)
    {
        var team = await _client.NewTeamAsync();

        var response = await PostAsync(PlanningTestData.Body(title, [(team, 8m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("title", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task EffortZeroOrNegative_Returns400_AndNothingIsSaved(double hours)
    {
        var team = await _client.NewTeamAsync();

        var response = await PostAsync(PlanningTestData.Body("Inválida", [(team, (decimal)hours)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await ReadAsync(await _client.GetAsync("/api/demands", Ct))).GetArrayLength());
    }

    [Fact]
    public async Task RepeatedTeam_Returns400()
    {
        var team = await _client.NewTeamAsync();

        var response = await PostAsync(PlanningTestData.Body("Repetido", [(team, 8m), (team, 4m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTeam_Returns400()
    {
        var response = await PostAsync(PlanningTestData.Body("Time fantasma", [(Guid.NewGuid(), 8m)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("teams", out _));
    }

    [Fact]
    public async Task UnknownPerson_Returns400()
    {
        var team = await _client.NewTeamAsync();

        var response = await PostAsync(PlanningTestData.Body("Pessoa fantasma", [(team, 8m)], [Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PersonOfAnotherTeam_Returns409WithItsCode()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var personOfB = await _client.NewPersonAsync(teamB);

        var response = await PostAsync(PlanningTestData.Body("Pessoa de fora", [(teamA, 8m)], [personOfB]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("person-not-in-demand-teams", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task NewDemands_EnterAtTheEndOfBothOrders()
    {
        var team = await _client.NewTeamAsync();
        for (var index = 1; index <= 3; index++)
        {
            await PostAsync(PlanningTestData.Body($"Demanda {index}", [(team, 8m)]));
        }

        var list = await ReadAsync(await _client.GetAsync("/api/demands", Ct));

        var ranks = list.EnumerateArray().Select(item => (item.GetProperty("priorityRank").GetInt32(), item.GetProperty("executionRank").GetInt32())).ToList();
        Assert.Equal([(1, 1), (2, 2), (3, 3)], ranks);
    }

    [Fact]
    public async Task TenConcurrentCreations_GetRanksOneToTen_WithoutFailure()
    {
        var team = await _client.NewTeamAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, 10)
            .Select(index => PostAsync(PlanningTestData.Body($"Simultânea {index}", [(team, 8m)]))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var list = await ReadAsync(await _client.GetAsync("/api/demands", Ct));
        var priorities = list.EnumerateArray().Select(item => item.GetProperty("priorityRank").GetInt32()).ToList();
        Assert.Equal(Enumerable.Range(1, 10), priorities);
    }

    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/demands", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
