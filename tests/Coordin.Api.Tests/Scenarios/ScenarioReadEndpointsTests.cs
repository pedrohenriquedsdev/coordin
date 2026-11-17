using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Scenarios;

/// <summary>
/// Spec scenario-recommendation e scenario-simulation (RF-13, RF-14; DT-25, DT-26): as rotas de leitura
/// recomendam e simulam sem gravar nada. Limpam as demandas na inicialização, como a classe de cronograma,
/// porque a recomendação olha para todo o planejamento.
/// </summary>
public class ScenarioReadEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>Cria uma demanda com uma pessoa alocada, nas datas de <see cref="PlanningTestData.StartMonday"/>.</summary>
    private async Task<Guid> CreateAsync(string title, Guid team, Guid person, decimal hours, params Guid[] prerequisites)
    {
        var body = new
        {
            title,
            startDate = PlanningTestData.StartMonday,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)hours } },
            assignedPersonIds = new[] { person },
            prerequisiteIds = prerequisites,
        };
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Duas demandas de uma pessoa só: a grande de maior prioridade (80 h) bloqueia a pequena (8 h). Trocar
    /// a ordem melhora o score, então a recomendação tem algo a devolver.
    /// </summary>
    private async Task<(Guid Big, Guid Small)> BlockedSmallDemandAsync()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var big = await CreateAsync(Unique("Grande"), team, person, 80m);
        var small = await CreateAsync(Unique("Pequena"), team, person, 8m);
        return (big, small);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<string> SnapshotOfPlanningAsync()
    {
        var demands = await ReadAsync(await _client.GetAsync("/api/demands", Ct));
        var schedule = await ReadAsync(await _client.GetAsync("/api/schedule", Ct));
        return demands.ToString() + schedule.ToString();
    }

    [Fact]
    public async Task Recommendations_ReturnAtMostThree_AllImprovingTheScore_AndTheSmallOneFirst()
    {
        var (big, small) = await BlockedSmallDemandAsync();

        var response = await _client.PostAsync("/api/scenarios/recommendations", JsonContent.Create(new { }), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scenarios = await ReadAsync(response);
        Assert.InRange(scenarios.GetArrayLength(), 1, 3);
        foreach (var scenario in scenarios.EnumerateArray())
        {
            Assert.True(scenario.GetProperty("score").GetProperty("delta").GetInt64() < 0);
            Assert.True(scenario.GetProperty("isComparable").GetBoolean());
        }

        var best = scenarios[0].GetProperty("executionOrder").EnumerateArray().Select(item => item.GetGuid()).ToList();
        Assert.True(best.IndexOf(small) < best.IndexOf(big));
        var titles = scenarios[0].GetProperty("items").EnumerateArray().Select(item => item.GetProperty("title").GetString()).ToList();
        Assert.Contains(titles, title => title!.StartsWith("Pequena", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recommendations_DoNotWriteAnything()
    {
        await BlockedSmallDemandAsync();
        var before = await SnapshotOfPlanningAsync();

        await _client.PostAsync("/api/scenarios/recommendations", JsonContent.Create(new { }), Ct);
        await _client.PostAsync("/api/scenarios/recommendations", JsonContent.Create(new { }), Ct);

        Assert.Equal(before, await SnapshotOfPlanningAsync());
    }

    [Fact]
    public async Task Simulate_TheCurrentOrder_GivesZeroDifference()
    {
        await BlockedSmallDemandAsync();
        var current = (await ReadAsync(await _client.GetAsync("/api/schedule", Ct))).GetProperty("items")
            .EnumerateArray().Select(item => item.GetProperty("demandId").GetGuid()).ToList();

        var response = await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = current }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scenario = await ReadAsync(response);
        Assert.Equal(0, scenario.GetProperty("score").GetProperty("delta").GetInt64());
        Assert.All(scenario.GetProperty("items").EnumerateArray(), item => Assert.Equal(0, item.GetProperty("deltaDays").GetInt32()));
    }

    [Fact]
    public async Task Simulate_SwappingTwoDemands_ShowsTheImpactPerDemand()
    {
        var (big, small) = await BlockedSmallDemandAsync();

        var response = await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = new[] { small, big } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scenario = await ReadAsync(response);
        Assert.True(scenario.GetProperty("score").GetProperty("delta").GetInt64() < 0);
        var items = scenario.GetProperty("items").EnumerateArray().ToList();
        var smallItem = items.Single(item => item.GetProperty("demandId").GetGuid() == small);
        Assert.Equal(1, smallItem.GetProperty("newExecutionRank").GetInt32());
        Assert.True(smallItem.GetProperty("deltaDays").GetInt32() < 0);
        var bigItem = items.Single(item => item.GetProperty("demandId").GetGuid() == big);
        Assert.True(bigItem.GetProperty("deltaDays").GetInt32() > 0);
        Assert.Equal(
            scenario.GetProperty("score").GetProperty("delta").GetInt64(),
            items.Sum(item => item.GetProperty("scoreContributionDelta").GetInt64()));
    }

    [Fact]
    public async Task Simulate_DoesNotWriteAnything()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        var before = await SnapshotOfPlanningAsync();

        await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = new[] { small, big } }, Ct);

        Assert.Equal(before, await SnapshotOfPlanningAsync());
    }

    [Fact]
    public async Task Simulate_DependentPutBeforeItsPrerequisite_StartsAfterIt_AndTheSequenceIsTheProposed()
    {
        var team = await _client.NewTeamAsync();
        var personA = await _client.NewPersonAsync(team);
        var personB = await _client.NewPersonAsync(team);
        var a = await CreateAsync(Unique("Pré-requisito"), team, personA, 16m);
        var b = await CreateAsync(Unique("Dependente"), team, personB, 8m, a);

        var response = await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = new[] { b, a } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scenario = await ReadAsync(response);
        Assert.Equal(new[] { b, a }, scenario.GetProperty("executionOrder").EnumerateArray().Select(item => item.GetGuid()).ToArray());
        var items = scenario.GetProperty("items").EnumerateArray().ToList();
        var dependent = items.Single(item => item.GetProperty("demandId").GetGuid() == b);
        var prerequisite = items.Single(item => item.GetProperty("demandId").GetGuid() == a);
        Assert.True(dependent.GetProperty("forecastStartDate").GetDateTime() > prerequisite.GetProperty("forecastEndDate").GetDateTime());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("repeated")]
    [InlineData("unknown")]
    public async Task Simulate_AnInvalidOrder_Returns400_OnTheOrderField(string kind)
    {
        var (big, _) = await BlockedSmallDemandAsync();
        var order = kind switch
        {
            "missing" => new[] { big },
            "repeated" => new[] { big, big },
            _ => new[] { big, Guid.NewGuid() },
        };

        var response = await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = order }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("executionOrder", out _));
    }

    [Fact]
    public async Task Simulate_WithoutTheOrder_Returns400()
    {
        await BlockedSmallDemandAsync();

        var response = await _client.PostAsJsonAsync("/api/scenarios/simulate", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
