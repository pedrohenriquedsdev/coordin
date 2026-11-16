using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>Spec demand-registry (conclusão) e forecast-recalculation (congelamento; DT-27, RF-12).</summary>
public class DemandCompletionTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private async Task<Guid> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private Task<HttpResponseMessage> CompleteAsync(Guid id, DateOnly completedOn) =>
        _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn }, Ct);

    private async Task<JsonElement> ScheduleItemAsync(Guid demandId)
    {
        var schedule = await ReadAsync(await _client.GetAsync("/api/schedule", Ct));
        return schedule.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("demandId").GetGuid() == demandId).Clone();
    }

    [Fact]
    public async Task CompletingADemand_Returns200_WithTheCompletionDate()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Concluir", [(team, 16m)], [person], PlanningTestData.StartMonday));

        var response = await CompleteAsync(id, new DateOnly(2027, 1, 6));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2027-01-06", (await ReadAsync(response)).GetProperty("completedOn").GetString());
    }

    [Fact]
    public async Task CompletionBeforeTheStart_Returns400_OnTheCompletionField()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Antes", [(team, 8m)], start: PlanningTestData.StartMonday));

        var response = await CompleteAsync(id, PlanningTestData.StartMonday.AddDays(-1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("completedOn", out _));
    }

    [Fact]
    public async Task CompletionDate_ComesBackUnchanged_WhateverTheServerZone()
    {
        var team = await _client.NewTeamAsync();
        var day = new DateOnly(2026, 12, 31);
        var id = await CreateAsync(PlanningTestData.Body("Virada", [(team, 8m)], start: day));
        await CompleteAsync(id, day);

        var fetched = await ReadAsync(await _client.GetAsync($"/api/demands/{id}", Ct));

        Assert.Equal("2026-12-31", fetched.GetProperty("completedOn").GetString());
    }

    [Fact]
    public async Task UpdatingOrDeletingACompletedDemand_Returns409_WithTheLockCode()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Travada", [(team, 8m)], start: PlanningTestData.StartMonday));
        await CompleteAsync(id, PlanningTestData.StartMonday);

        var update = await _client.PutAsJsonAsync($"/api/demands/{id}", PlanningTestData.Body("Travada", [(team, 9m)], start: PlanningTestData.StartMonday), Ct);
        var delete = await _client.DeleteAsync($"/api/demands/{id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal("completed-demand-locked", (await ReadAsync(update)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task CompletingTwice_Returns409_WithTheLockCode()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Duas vezes", [(team, 8m)], start: PlanningTestData.StartMonday));
        await CompleteAsync(id, PlanningTestData.StartMonday);

        var second = await CompleteAsync(id, PlanningTestData.StartMonday);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("completed-demand-locked", (await ReadAsync(second)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompletingAnUnknownDemand_Returns404()
    {
        var response = await CompleteAsync(Guid.NewGuid(), PlanningTestData.StartMonday);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Completing_FreesTheCapacity_ForTheDemandBehindIt()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team); // 8 h por dia
        var first = await CreateAsync(PlanningTestData.Body("Primeira", [(team, 16m)], [person], PlanningTestData.StartMonday));
        var second = await CreateAsync(PlanningTestData.Body("Segunda", [(team, 8m)], [person], PlanningTestData.StartMonday));
        Assert.Equal(new DateOnly(2027, 1, 7), PlanningTestData.ParseDate((await ScheduleItemAsync(second)).GetProperty("forecastEndDate").GetString()!));

        await CompleteAsync(first, new DateOnly(2027, 1, 5));

        // A primeira saiu da fila: a segunda começa na segunda-feira e termina com margem na terça.
        Assert.Equal(new DateOnly(2027, 1, 5), PlanningTestData.ParseDate((await ScheduleItemAsync(second)).GetProperty("forecastEndDate").GetString()!));
    }

    [Fact]
    public async Task CompletedDemand_KeepsItsForecast_WhenTheMarginChanges()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Congelada", [(team, 16m)], [person], PlanningTestData.StartMonday));
        await CompleteAsync(id, PlanningTestData.StartMonday.AddDays(1));
        var before = (await ScheduleItemAsync(id)).GetProperty("forecastEndDate").GetString();

        try
        {
            await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 100 }, Ct);

            Assert.Equal(before, (await ScheduleItemAsync(id)).GetProperty("forecastEndDate").GetString());
        }
        finally
        {
            await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 20 }, Ct);
        }
    }
}
