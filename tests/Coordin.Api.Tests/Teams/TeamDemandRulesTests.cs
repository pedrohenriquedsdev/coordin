using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Teams;

/// <summary>Spec team-registry (delta da change demand-planning): time usado por demandas (RF-01, RF-05).</summary>
public class TeamDemandRulesTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    [Fact]
    public async Task TeamInADemand_CannotBeDeleted_EvenWithoutPeople()
    {
        var team = await _client.NewTeamAsync();
        await _client.PostAsJsonAsync("/api/demands", PlanningTestData.Body("Com time", [(team, 8m)]), Ct);

        var response = await _client.DeleteAsync($"/api/teams/{team}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("team-has-demands", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task TeamInNoDemand_CanBeDeleted()
    {
        var team = await _client.NewTeamAsync();

        var response = await _client.DeleteAsync($"/api/teams/{team}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TeamWithPeopleAndDemands_CannotBeDeleted()
    {
        var team = await _client.NewTeamAsync();
        await _client.NewPersonAsync(team);
        await _client.PostAsJsonAsync("/api/demands", PlanningTestData.Body("Com pessoas", [(team, 8m)]), Ct);

        var response = await _client.DeleteAsync($"/api/teams/{team}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
