using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Planning;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Planning;

/// <summary>Spec schedule-view (RF-08, RF-11, RF-12; DT-13, DT-27): o cronograma e os desvios.</summary>
public class ScheduleEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private async Task<JsonElement> ScheduleAsync() => await ReadAsync(await _client.GetAsync("/api/schedule", Ct));

    private async Task<JsonElement> ItemAsync(Guid demandId) =>
        (await ScheduleAsync()).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("demandId").GetGuid() == demandId).Clone();

    private static DateOnly? DateOf(JsonElement item, string property) =>
        item.GetProperty(property).ValueKind == JsonValueKind.Null
            ? null
            : PlanningTestData.ParseDate(item.GetProperty(property).GetString()!);

    [Fact]
    public async Task Schedule_IsEmpty_WhenThereAreNoDemands()
    {
        var schedule = await ScheduleAsync();

        Assert.Equal(0, schedule.GetProperty("items").GetArrayLength());
        Assert.Equal("FollowPriority", schedule.GetProperty("executionOrderMode").GetString());
    }

    [Fact]
    public async Task Schedule_IsInPriorityOrder_WithBothRanks()
    {
        var team = await _client.NewTeamAsync();
        await CreateAsync(PlanningTestData.Body("Primeira", [(team, 8m)]));
        await CreateAsync(PlanningTestData.Body("Segunda", [(team, 8m)]));

        var items = (await ScheduleAsync()).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["Primeira", "Segunda"], items.Select(item => item.GetProperty("title").GetString()));
        Assert.All(items, item => Assert.False(item.GetProperty("isOutOfPriorityOrder").GetBoolean()));
    }

    [Fact]
    public async Task PredictableDemand_HasTheDatesAndTheMarginDifference()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Previsível", [(team, 16m)], [person], PlanningTestData.StartMonday));

        var item = await ItemAsync(id);

        Assert.True(item.GetProperty("isPredictable").GetBoolean());
        Assert.Equal(PlanningTestData.StartMonday, DateOf(item, "forecastStartDate"));
        Assert.Equal(new DateOnly(2027, 1, 5), DateOf(item, "endDateWithoutMargin"));
        Assert.Equal(new DateOnly(2027, 1, 6), DateOf(item, "forecastEndDate"));
    }

    [Fact]
    public async Task UnpredictableDemand_HasTheReason_AndNoDates()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Sem estimativa", [(team, null)], start: PlanningTestData.StartMonday));

        var item = await ItemAsync(id);

        Assert.False(item.GetProperty("isPredictable").GetBoolean());
        Assert.Equal("MissingEstimate", item.GetProperty("unpredictableReason").GetString());
        Assert.Null(DateOf(item, "forecastEndDate"));
    }

    [Fact]
    public async Task TotalEffort_SumsTheEstimates_AndKeepsTheMissingOneNull()
    {
        var teamA = await _client.NewTeamAsync();
        var teamB = await _client.NewTeamAsync();
        var teamC = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Três times", [(teamA, 80m), (teamB, 40m), (teamC, null)]));

        var item = await ItemAsync(id);

        Assert.Equal(120m, item.GetProperty("totalEstimatedEffortHours").GetDecimal());
        var missing = item.GetProperty("teams").EnumerateArray().Single(part => part.GetProperty("teamId").GetGuid() == teamC);
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("estimatedEffortHours").ValueKind);
    }

    [Fact]
    public async Task ForecastPastTheInformedEnd_IsFlagged_AndOnTheDayItIsNot()
    {
        // Cada demanda tem o seu time e a sua pessoa, para uma não atrasar a outra.
        var late = await CreatePredictableAsync("Atrasada", new DateOnly(2027, 1, 5));
        var onTime = await CreatePredictableAsync("No prazo", new DateOnly(2027, 1, 6));
        var none = await CreatePredictableAsync("Sem data", null);

        // Previsão com margem: quarta-feira, 6 de janeiro.
        Assert.True((await ItemAsync(late)).GetProperty("exceedsInformedEndDate").GetBoolean());
        Assert.False((await ItemAsync(onTime)).GetProperty("exceedsInformedEndDate").GetBoolean());
        Assert.False((await ItemAsync(none)).GetProperty("exceedsInformedEndDate").GetBoolean());
    }

    [Fact]
    public async Task InformedEnd_DoesNotChangeTheForecast()
    {
        var without = await CreatePredictableAsync("Sem data", null);
        var with = await CreatePredictableAsync("Com data", new DateOnly(2030, 1, 1));

        var itemWithout = await ItemAsync(without);
        var itemWith = await ItemAsync(with);

        Assert.Equal(DateOf(itemWithout, "forecastEndDate"), DateOf(itemWith, "forecastEndDate"));
        Assert.Equal(new DateOnly(2030, 1, 1), DateOf(itemWith, "informedEndDate"));
    }

    /// <summary>Demanda previsível de 16 h, com time e pessoa próprios (8 h por dia).</summary>
    private async Task<Guid> CreatePredictableAsync(string title, DateOnly? informedEnd)
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        return await CreateAsync(PlanningTestData.Body(title, [(team, 16m)], [person], PlanningTestData.StartMonday, informedEnd));
    }

    [Fact]
    public async Task CustomMode_FlagsTheDemandsOutOfPriorityOrder()
    {
        var team = await _client.NewTeamAsync();
        var first = await CreateAsync(PlanningTestData.Body("Primeira", [(team, 8m)]));
        await CreateAsync(PlanningTestData.Body("Segunda", [(team, 8m)]));
        await SetModeAsync(ExecutionOrderMode.Custom);
        try
        {
            await SetExecutionRankAsync(first, 3); // a primeira passa para o fim da execução (ranking 3, livre)

            var items = (await ScheduleAsync()).GetProperty("items").EnumerateArray().ToList();

            Assert.Equal("Custom", (await ScheduleAsync()).GetProperty("executionOrderMode").GetString());
            Assert.True(items.Single(item => item.GetProperty("title").GetString() == "Primeira").GetProperty("isOutOfPriorityOrder").GetBoolean());
        }
        finally
        {
            await SetModeAsync(ExecutionOrderMode.FollowPriority);
        }
    }

    [Fact]
    public async Task CompletedLate_HasPositiveDeviations()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Atrasou", [(team, 16m)], [person], PlanningTestData.StartMonday));

        await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = new DateOnly(2027, 1, 8) }, Ct);

        var item = await ItemAsync(id);
        // Previsão e linha de base: quarta-feira (6 de janeiro). Concluída na sexta (8): dois dias de atraso.
        Assert.Equal(2, item.GetProperty("baselineDeviationDays").GetInt32());
        Assert.Equal(2, item.GetProperty("forecastDeviationDays").GetInt32());
    }

    [Fact]
    public async Task CompletedEarly_HasNegativeDeviations()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Antecipou", [(team, 16m)], [person], PlanningTestData.StartMonday));

        await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = PlanningTestData.StartMonday }, Ct);

        var item = await ItemAsync(id);
        Assert.Equal(-2, item.GetProperty("baselineDeviationDays").GetInt32());
        Assert.Equal(-2, item.GetProperty("forecastDeviationDays").GetInt32());
    }

    [Fact]
    public async Task CompletedWithoutEverBeingPredictable_HasNoBaselineAndNoDeviations()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Nunca previsível", [(team, null)], start: PlanningTestData.StartMonday));
        await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = PlanningTestData.StartMonday }, Ct);

        var item = await ItemAsync(id);

        Assert.Equal(JsonValueKind.Null, item.GetProperty("baselineEndDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("baselineDeviationDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("forecastDeviationDays").ValueKind);
    }

    [Fact]
    public async Task Schedule_WithoutAToken_Returns401()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/schedule", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SetModeAsync(ExecutionOrderMode mode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var settings = await db.PlanningSettings.SingleAsync(item => item.Id == PlanningSettings.SingletonId, Ct);
        settings.ExecutionOrderMode = mode;
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Muda o ranking de execução direto no banco, só para montar o cenário do modo Custom.</summary>
    private async Task SetExecutionRankAsync(Guid demandId, int rank)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        await db.Demands.Where(demand => demand.Id == demandId).ExecuteUpdateAsync(setters => setters.SetProperty(demand => demand.ExecutionRank, rank), Ct);
    }
}
