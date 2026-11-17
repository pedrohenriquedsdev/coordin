using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Scenarios;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Scenarios;

/// <summary>
/// Spec scenario-approval (RF-15; regras invioláveis 2 e 3): aprovar grava a ordem e o modo na mesma
/// transação, a prioridade fica intacta, e voltar à prioridade recalcula. Ao terminar, cada teste volta o
/// planejamento para a prioridade, porque o modo é uma configuração global que outras classes leem.
/// </summary>
public class ScenarioApplyEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        await PlanningTestData.ClearDemandsAsync(factory);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.PostAsync("/api/scenarios/reset-to-priority", JsonContent.Create(new { }), Ct);
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private async Task<Guid> CreateAsync(string title, Guid team, Guid person, decimal hours)
    {
        var body = new
        {
            title,
            startDate = PlanningTestData.StartMonday,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)hours } },
            assignedPersonIds = new[] { person },
        };
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

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

    private async Task<Dictionary<Guid, (int Priority, int Execution)>> RanksAsync()
    {
        var demands = await ReadAsync(await _client.GetAsync("/api/demands", Ct));
        return demands.EnumerateArray().ToDictionary(
            demand => demand.GetProperty("id").GetGuid(),
            demand => (demand.GetProperty("priorityRank").GetInt32(), demand.GetProperty("executionRank").GetInt32()));
    }

    private async Task<string> ModeAsync() =>
        (await ReadAsync(await _client.GetAsync("/api/schedule", Ct))).GetProperty("executionOrderMode").GetString()!;

    private async Task<string> ScheduleSnapshotAsync() =>
        (await ReadAsync(await _client.GetAsync("/api/schedule", Ct))).ToString()
        + (await ReadAsync(await _client.GetAsync("/api/demands", Ct))).ToString();

    [Fact]
    public async Task Apply_WritesTheOrderAndTheCustomMode_AndKeepsThePriority()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        var priorityBefore = (await RanksAsync()).ToDictionary(item => item.Key, item => item.Value.Priority);

        var response = await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { small, big } }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ranks = await RanksAsync();
        Assert.Equal(1, ranks[small].Execution);
        Assert.Equal(2, ranks[big].Execution);
        Assert.All(priorityBefore, pair => Assert.Equal(pair.Value, ranks[pair.Key].Priority));
        Assert.Equal("Custom", await ModeAsync());
    }

    [Fact]
    public async Task Apply_TheScheduleShowsTheNewDatesAndTheDisplacedDemands()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { small, big } }, Ct);

        var items = (await ReadAsync(await _client.GetAsync("/api/schedule", Ct))).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(small, items[0].GetProperty("demandId").GetGuid());
        Assert.True(items[0].GetProperty("isOutOfPriorityOrder").GetBoolean());
        Assert.True(items[1].GetProperty("isOutOfPriorityOrder").GetBoolean());
        // A pequena passou à frente: termina antes da grande, que agora espera por ela.
        Assert.True(items[0].GetProperty("forecastEndDate").GetDateTime() < items[1].GetProperty("forecastEndDate").GetDateTime());
        Assert.Equal(PlanningTestData.StartMonday, PlanningTestData.ParseDate(items[0].GetProperty("forecastStartDate").GetString()!));
    }

    [Fact]
    public async Task Apply_ReturnsTheScenario_AgainstThePreviousSchedule()
    {
        var (big, small) = await BlockedSmallDemandAsync();

        var response = await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { small, big } }, Ct);

        var scenario = await ReadAsync(response);
        Assert.True(scenario.GetProperty("score").GetProperty("delta").GetInt64() < 0);
        Assert.Equal(small, scenario.GetProperty("executionOrder")[0].GetGuid());
    }

    [Fact]
    public async Task Apply_AnInvalidOrder_Returns400_AndWritesNothing()
    {
        var (big, _) = await BlockedSmallDemandAsync();
        var before = await ScheduleSnapshotAsync();

        var response = await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { big } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await ScheduleSnapshotAsync());
    }

    [Fact]
    public async Task ResetToPriority_AfterApply_GoesBackToFollowPriority_AndRecalculates()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        var priorityOrderSchedule = await ScheduleSnapshotAsync();
        await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { small, big } }, Ct);

        var response = await _client.PostAsync("/api/scenarios/reset-to-priority", JsonContent.Create(new { }), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("FollowPriority", await ModeAsync());
        Assert.All((await RanksAsync()).Values, pair => Assert.Equal(pair.Priority, pair.Execution));
        Assert.Equal(priorityOrderSchedule, await ScheduleSnapshotAsync());
    }

    [Fact]
    public async Task ResetToPriority_WhenAlreadyInPriority_Returns204_AndChangesNothing()
    {
        await BlockedSmallDemandAsync();
        var before = await ScheduleSnapshotAsync();

        var response = await _client.PostAsync("/api/scenarios/reset-to-priority", JsonContent.Create(new { }), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await ScheduleSnapshotAsync());
    }

    [Fact]
    public async Task ScenariosSeenButNotApplied_ChangeNothing()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        var before = await ScheduleSnapshotAsync();

        await _client.PostAsync("/api/scenarios/recommendations", JsonContent.Create(new { }), Ct);
        await _client.PostAsJsonAsync("/api/scenarios/simulate", new { executionOrder = new[] { small, big } }, Ct);

        Assert.Equal(before, await ScheduleSnapshotAsync());
        Assert.Equal("FollowPriority", await ModeAsync());
    }

    [Fact]
    public async Task TheRankWrite_IsUndoneWhenTheTransactionFails()
    {
        var (big, small) = await BlockedSmallDemandAsync();
        var before = await RanksAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<Coordin.Api.Features.Planning.ForecastRecalculator>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Coordin.Api.Features.Planning.PlanningTransaction.RunAsync(db, recalculator, async () =>
                {
                    // Grava a ordem de execução invertida em duas fases, como apply faz para não bater no índice único.
                    await db.Demands.ExecuteUpdateAsync(setters => setters.SetProperty(demand => demand.ExecutionRank, demand => demand.ExecutionRank + 1000000), Ct);
                    await db.Demands.Where(demand => demand.Id == small).ExecuteUpdateAsync(setters => setters.SetProperty(demand => demand.ExecutionRank, 1), Ct);
                    await db.Demands.Where(demand => demand.Id == big).ExecuteUpdateAsync(setters => setters.SetProperty(demand => demand.ExecutionRank, 2), Ct);
                    throw new InvalidOperationException("falha simulada depois de gravar a ordem");
                }, Ct));
        }

        Assert.Equal(before, await RanksAsync());
        Assert.Equal("FollowPriority", await ModeAsync());
    }
}

/// <summary>O erro de dependência, testado sem servidor (a API não consegue produzi-lo, porque o motor já a respeita).</summary>
public class ScenarioErrorsTests
{
    [Fact]
    public void NoViolations_GiveNoError()
    {
        Assert.Null(ScenarioErrors.Describe([]));
    }

    [Fact]
    public void AViolation_GivesTheStableCodeAndAPortugueseMessage()
    {
        var (code, message) = Assert.NotNull(ScenarioErrors.Describe([new DependencyInput(Guid.NewGuid(), Guid.NewGuid())]));

        Assert.Equal("scenario-violates-dependencies", code);
        Assert.Contains("pré-requisito", message);
    }
}
