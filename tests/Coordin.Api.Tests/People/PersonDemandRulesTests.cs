using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.People;

/// <summary>Spec people-registry (delta da change demand-planning): pessoas alocadas a demandas (RF-02, RF-05, RF-08).</summary>
public class PersonDemandRulesTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<Guid> CreateDemandAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ScheduleItemAsync(Guid demandId)
    {
        var schedule = await ReadAsync(await _client.GetAsync("/api/schedule", Ct));
        return schedule.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("demandId").GetGuid() == demandId).Clone();
    }

    private Task<HttpResponseMessage> MovePersonAsync(Guid personId, string name, Guid teamId, decimal hours) =>
        _client.PutAsJsonAsync($"/api/people/{personId}", new { name, teamId, dailyCapacityHours = hours }, Ct);

    [Fact]
    public async Task DeletingAPersonInAnOpenDemand_Returns409()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        await CreateDemandAsync(PlanningTestData.Body("Aberta", [(team, 8m)], [person]));

        var response = await _client.DeleteAsync($"/api/people/{person}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("person-has-open-assignments", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeletingAPersonOnlyInCompletedDemands_Returns204()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var demand = await CreateDemandAsync(PlanningTestData.Body("Concluída", [(team, 8m)], [person], PlanningTestData.StartMonday));
        await _client.PutAsJsonAsync($"/api/demands/{demand}/completion", new { completedOn = PlanningTestData.StartMonday }, Ct);

        var response = await _client.DeleteAsync($"/api/people/{person}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MovingToATeamOutsideTheDemand_Returns409_AndThePersonStays()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(teamA);
        await CreateDemandAsync(PlanningTestData.Body("Só do time A", [(teamA, 8m)], [person]));

        var response = await MovePersonAsync(person, "Pessoa", teamB, 8m);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("person-not-in-demand-teams", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task MovingToATeamInsideTheDemand_Returns200()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(teamA);
        await CreateDemandAsync(PlanningTestData.Body("Dois times", [(teamA, 8m), (teamB, 8m)], [person]));

        var response = await MovePersonAsync(person, "Pessoa", teamB, 8m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MovingAPersonWithoutAllocations_Returns200()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(teamA);

        var response = await MovePersonAsync(person, "Livre", teamB, 8m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReducingACapacity_PushesTheForecastLater()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team); // 8 h por dia
        var demand = await CreateDemandAsync(PlanningTestData.Body("Capacidade", [(team, 16m)], [person], PlanningTestData.StartMonday));
        var before = PlanningTestData.ParseDate((await ScheduleItemAsync(demand)).GetProperty("forecastEndDate").GetString()!);

        var response = await MovePersonAsync(person, "Pessoa", team, 4m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = PlanningTestData.ParseDate((await ScheduleItemAsync(demand)).GetProperty("forecastEndDate").GetString()!);
        Assert.True(after > before, $"Esperava depois de {before}, veio {after}.");
    }

    [Fact]
    public async Task ZeroingTheOnlyPerson_MakesTheDemandNoCapacity()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var demand = await CreateDemandAsync(PlanningTestData.Body("Zerada", [(team, 16m)], [person], PlanningTestData.StartMonday));

        await MovePersonAsync(person, "Pessoa", team, 0m);

        var item = await ScheduleItemAsync(demand);
        Assert.False(item.GetProperty("isPredictable").GetBoolean());
        Assert.Equal("NoCapacity", item.GetProperty("unpredictableReason").GetString());
    }
}
