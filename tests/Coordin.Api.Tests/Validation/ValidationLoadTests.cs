using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Validation;
using Coordin.Api.Features.Validation.Dataset;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// O carregamento do conjunto, a linha de base e o plano atual (RF-24; DT-37; RT-11). Cada teste começa recarregando
/// o conjunto, porque a fábrica compartilha o banco entre os testes da classe e a ordem não é garantida.
/// </summary>
public sealed class ValidationLoadTests(ValidationEnabledApiFactory factory) : IClassFixture<ValidationEnabledApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<HttpResponseMessage> LoadAsync(HttpClient client, bool confirm) =>
        await client.PostAsJsonAsync("/api/validation/dataset", new { confirmReplaceAll = confirm }, Ct);

    private async Task<HttpClient> ResetAsync()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await LoadAsync(client, confirm: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Load_WithoutConfirmation_IsRefused_AndChangesNothing()
    {
        using var client = await ResetAsync();
        var demandsBefore = await client.GetStringAsync("/api/demands", Ct);

        var response = await LoadAsync(client, confirm: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("substitui o planejamento inteiro", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(demandsBefore, await client.GetStringAsync("/api/demands", Ct));
    }

    [Fact]
    public async Task Load_WithConfirmation_ReplacesThePlanning_WithTheDataset()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await LoadAsync(client, confirm: true);
        var counts = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, counts.GetProperty("teams").GetInt32());
        Assert.Equal(35, counts.GetProperty("people").GetInt32());
        Assert.Equal(30, counts.GetProperty("demands").GetInt32());
        var schedule = await client.GetFromJsonAsync<JsonElement>("/api/schedule", Ct);
        var items = schedule.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(30, items.Count);
        Assert.All(items, item => Assert.True(item.GetProperty("isPredictable").GetBoolean()));
        Assert.Equal("FollowPriority", schedule.GetProperty("executionOrderMode").GetString());
    }

    [Fact]
    public async Task Load_SetsTheMarginOfTheDataset()
    {
        using var client = await ResetAsync();

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings", Ct);

        Assert.Equal(20m, settings.GetProperty("safetyMarginPercent").GetDecimal());
    }

    [Fact]
    public async Task Load_KeepsTheManagerAccountAndTheSession()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        int usersBefore, sessionsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            usersBefore = await db.Users.CountAsync(Ct);
            sessionsBefore = await db.RefreshTokens.CountAsync(Ct);
        }

        var response = await LoadAsync(client, confirm: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<CoordinDbContext>();
        Assert.Equal(usersBefore, await db2.Users.CountAsync(Ct));
        Assert.Equal(sessionsBefore, await db2.RefreshTokens.CountAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/teams", Ct)).StatusCode);
    }

    [Fact]
    public async Task Two_loads_give_the_same_schedule()
    {
        using var client = await ResetAsync();
        var first = await client.GetStringAsync("/api/schedule", Ct);

        var response = await LoadAsync(client, confirm: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(first, await client.GetStringAsync("/api/schedule", Ct));
    }

    [Fact]
    public async Task Load_ClearsTheBaselineAndTheCurrentPlan()
    {
        using var client = await ResetAsync();
        var demands = await client.GetFromJsonAsync<JsonElement>("/api/demands", Ct);
        var demandId = demands.EnumerateArray().First().GetProperty("id").GetGuid();
        await client.PutAsJsonAsync("/api/validation/baseline", new { baselines = new[] { new { demandId, baselineDate = "2027-03-01" } } }, Ct);
        await client.PostAsync("/api/validation/current-plan", null, Ct);

        var response = await LoadAsync(client, confirm: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/validation/status", Ct);
        Assert.Equal(0, status.GetProperty("baselineCount").GetInt32());
        Assert.False(status.GetProperty("currentPlanCaptured").GetBoolean());
    }

    [Fact]
    public async Task Baseline_ListsEveryDemand_EmptyUntilTheManagerInformsIt()
    {
        using var client = await ResetAsync();

        var baseline = await client.GetFromJsonAsync<JsonElement>("/api/validation/baseline", Ct);

        Assert.Equal(30, baseline.GetArrayLength());
        Assert.All(baseline.EnumerateArray(), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("baselineDate").ValueKind));
    }

    [Fact]
    public async Task Baseline_Save_ReplacesThePreviousOne()
    {
        using var client = await ResetAsync();
        var demands = await client.GetFromJsonAsync<JsonElement>("/api/demands", Ct);
        var first = demands.EnumerateArray().First().GetProperty("id").GetGuid();
        var second = demands.EnumerateArray().Skip(1).First().GetProperty("id").GetGuid();
        await client.PutAsJsonAsync("/api/validation/baseline", new { baselines = new[] { new { demandId = first, baselineDate = "2027-03-01" }, new { demandId = second, baselineDate = "2027-03-02" } } }, Ct);

        var response = await client.PutAsJsonAsync("/api/validation/baseline", new { baselines = new[] { new { demandId = first, baselineDate = "2027-04-01" } } }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var baseline = await client.GetFromJsonAsync<JsonElement>("/api/validation/baseline", Ct);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/validation/status", Ct);
        Assert.Equal(1, status.GetProperty("baselineCount").GetInt32());
        var saved = baseline.EnumerateArray().Single(item => item.GetProperty("demandId").GetGuid() == first);
        Assert.Equal("2027-04-01", saved.GetProperty("baselineDate").GetString());
    }

    [Fact]
    public async Task Baseline_RefusesAnUnknownOrRepeatedDemand_AndSavesNothing()
    {
        using var client = await ResetAsync();
        var demands = await client.GetFromJsonAsync<JsonElement>("/api/demands", Ct);
        var known = demands.EnumerateArray().First().GetProperty("id").GetGuid();

        var unknown = await client.PutAsJsonAsync("/api/validation/baseline", new { baselines = new[] { new { demandId = Guid.NewGuid(), baselineDate = "2027-03-01" } } }, Ct);
        var repeated = await client.PutAsJsonAsync("/api/validation/baseline", new { baselines = new[] { new { demandId = known, baselineDate = "2027-03-01" }, new { demandId = known, baselineDate = "2027-03-02" } } }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/validation/status", Ct);
        Assert.Equal(0, status.GetProperty("baselineCount").GetInt32());
    }

    [Fact]
    public async Task CurrentPlan_IsCapturedOnce_AndTheSecondCaptureIsRefused()
    {
        using var client = await ResetAsync();

        var first = await client.PostAsync("/api/validation/current-plan", null, Ct);
        var firstStatus = await client.GetFromJsonAsync<JsonElement>("/api/validation/status", Ct);
        var second = await client.PostAsync("/api/validation/current-plan", null, Ct);
        var secondStatus = await client.GetFromJsonAsync<JsonElement>("/api/validation/status", Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(firstStatus.GetProperty("currentPlanCaptured").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("validation-current-plan-exists", await second.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(firstStatus.GetProperty("currentPlanCapturedAt").GetDateTimeOffset(), secondStatus.GetProperty("currentPlanCapturedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_failed_load_changes_nothing()
    {
        using var client = await ResetAsync();
        var before = await client.GetStringAsync("/api/demands", Ct);

        // Uma parte que aponta para um time inexistente: o banco recusa, e a transação inteira é desfeita.
        var dataset = ValidationDataset.Read();
        var broken = dataset with
        {
            Demands = [.. dataset.Demands.Select((demand, index) => index == 0
                ? demand with { Parts = [new DatasetPart(Guid.NewGuid(), 10m)] }
                : demand)],
        };
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();

        await Assert.ThrowsAnyAsync<Exception>(() => PlanningTransaction.RunAsync(
            db,
            recalculator,
            Coordin.Engine.Forecasting.ForecastChangeCause.NewDemand,
            () => ValidationDatasetLoader.ReplaceAsync(db, broken, Ct),
            Ct));

        Assert.Equal(before, await client.GetStringAsync("/api/demands", Ct));
    }
}
