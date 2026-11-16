using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>Spec demand-registry: alteração, exclusão e ordem de prioridade (RF-04, RF-05; DT-13).</summary>
public class DemandUpdateDeleteReorderTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
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

    private async Task<Guid> CreateAsync(string title, object body)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<List<JsonElement>> ListAsync() =>
        (await ReadAsync(await _client.GetAsync("/api/demands", Ct))).EnumerateArray().Select(item => item.Clone()).ToList();

    [Fact]
    public async Task RegisteringTheMissingEstimate_UpdatesTheDemand()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync("Sem estimativa", PlanningTestData.Body("Sem estimativa", [(team, null)]));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}", PlanningTestData.Body("Sem estimativa", [(team, 40m)]), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(40m, (await ReadAsync(response)).GetProperty("teams")[0].GetProperty("estimatedEffortHours").GetDecimal());
    }

    [Fact]
    public async Task RemovingATeam_RemovesItsPart()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var id = await CreateAsync("Dois times", PlanningTestData.Body("Dois times", [(teamA, 80m), (teamB, 40m)]));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}", PlanningTestData.Body("Dois times", [(teamA, 80m)]), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await ReadAsync(response)).GetProperty("teams").GetArrayLength());
    }

    [Fact]
    public async Task UpdatingAnUnknownDemand_Returns404()
    {
        var team = await _client.NewTeamAsync();

        var response = await _client.PutAsJsonAsync($"/api/demands/{Guid.NewGuid()}", PlanningTestData.Body("X", [(team, 8m)]), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingTheMiddleDemand_RecompactsTheRanks()
    {
        var team = await _client.NewTeamAsync();
        await CreateAsync("1", PlanningTestData.Body("1", [(team, 8m)]));
        var middle = await CreateAsync("2", PlanningTestData.Body("2", [(team, 8m)]));
        await CreateAsync("3", PlanningTestData.Body("3", [(team, 8m)]));

        var response = await _client.DeleteAsync($"/api/demands/{middle}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await ListAsync();
        Assert.Equal(["1", "3"], list.Select(item => item.GetProperty("title").GetString()));
        Assert.Equal([1, 2], list.Select(item => item.GetProperty("priorityRank").GetInt32()));
        Assert.Equal([1, 2], list.Select(item => item.GetProperty("executionRank").GetInt32()));
    }

    [Fact]
    public async Task DeletingAnUnknownDemand_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/demands/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingADemand_RecalculatesTheDemandBehindIt()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team); // 8 h por dia
        var first = await CreateAsync("Primeira", PlanningTestData.Body("Primeira", [(team, 16m)], [person], PlanningTestData.StartMonday));
        var second = await CreateAsync("Segunda", PlanningTestData.Body("Segunda", [(team, 8m)], [person], PlanningTestData.StartMonday));

        // A segunda só começa depois das 16 h da primeira: quarta, com margem, quinta.
        Assert.Equal(new DateOnly(2027, 1, 7), await ForecastEndAsync(second));

        await _client.DeleteAsync($"/api/demands/{first}", Ct);

        // Sem a primeira, a segunda cabe na segunda-feira: com margem, terça.
        Assert.Equal(new DateOnly(2027, 1, 5), await ForecastEndAsync(second));
    }

    [Fact]
    public async Task ReorderingToTheInverseOrder_SetsTheNewRanks()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync("A", PlanningTestData.Body("A", [(team, 8m)]));
        var b = await CreateAsync("B", PlanningTestData.Body("B", [(team, 8m)]));
        var c = await CreateAsync("C", PlanningTestData.Body("C", [(team, 8m)]));

        var response = await _client.PutAsJsonAsync("/api/demands/priority-order", new { demandIds = new[] { c, b, a } }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var list = await ListAsync();
        Assert.Equal(["C", "B", "A"], list.Select(item => item.GetProperty("title").GetString()));
        Assert.Equal([1, 2, 3], list.Select(item => item.GetProperty("priorityRank").GetInt32()));
    }

    [Fact]
    public async Task Reordering_InFollowPriorityMode_MakesTheExecutionRankEqualTheNewPriority()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync("A", PlanningTestData.Body("A", [(team, 8m)]));
        var b = await CreateAsync("B", PlanningTestData.Body("B", [(team, 8m)]));

        await _client.PutAsJsonAsync("/api/demands/priority-order", new { demandIds = new[] { b, a } }, Ct);

        var list = await ListAsync();
        Assert.All(list, item => Assert.Equal(item.GetProperty("priorityRank").GetInt32(), item.GetProperty("executionRank").GetInt32()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("repeated")]
    [InlineData("unknown")]
    public async Task InvalidReorderList_Returns400_AndTheOrderDoesNotChange(string kind)
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync("A", PlanningTestData.Body("A", [(team, 8m)]));
        var b = await CreateAsync("B", PlanningTestData.Body("B", [(team, 8m)]));
        var ids = kind switch
        {
            "missing" => new[] { a },
            "repeated" => new[] { a, a },
            _ => new[] { a, Guid.NewGuid() },
        };

        var response = await _client.PutAsJsonAsync("/api/demands/priority-order", new { demandIds = ids }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["A", "B"], (await ListAsync()).Select(item => item.GetProperty("title").GetString()));
        _ = b;
    }

    /// <summary>Lê a data de fim com margem de uma demanda pelo cronograma.</summary>
    private async Task<DateOnly> ForecastEndAsync(Guid demandId)
    {
        var schedule = await ReadAsync(await _client.GetAsync("/api/schedule", Ct));
        var item = schedule.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("demandId").GetGuid() == demandId);
        return PlanningTestData.ParseDate(item.GetProperty("forecastEndDate").GetString()!);
    }
}
