using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Demands;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.DeliveryInsights;

/// <summary>
/// Histórico, esforço real e indicadores pela API (RF-20 a RF-23; DT-14, DT-27): cada escrita grava o histórico
/// com a causa certa, a conclusão grava os snapshots e aceita o esforço real opcional, e a consulta só lê.
/// </summary>
public class DeliveryInsightsApiTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetJsonAsync(string url) =>
        await (await _client.GetAsync(url, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<(DateOnly? Forecast, decimal? Estimate, decimal? Actual)> SnapshotAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var demand = await db.Demands.AsNoTracking().SingleAsync(item => item.Id == id, Ct);
        return (demand.ForecastEndDateAtCompletion, demand.EstimatedEffortHoursAtCompletion, demand.ActualEffortHours);
    }

    [Fact]
    public async Task CreatingAPredictableDemand_RecordsTheFirstForecast_WithTheNewDemandCause()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Primeira", [(team, 16m)], [person], PlanningTestData.StartMonday));

        var history = await GetJsonAsync($"/api/demands/{id}/forecast-history");

        var first = Assert.Single(history.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("previousEndDate").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, first.GetProperty("newEndDate").ValueKind);
        Assert.Equal("NewDemand", first.GetProperty("cause").GetString());
    }

    [Fact]
    public async Task ChangingAnEstimate_RecordsTheEstimateChange_WithTheReason_AndTheDateChange_WithTheEstimateCause()
    {
        var team = await _client.NewTeamAsync("Alfa");
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Alterar", [(team, 16m)], [person], PlanningTestData.StartMonday));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}", new
        {
            title = "Alterar",
            startDate = PlanningTestData.StartMonday,
            informedEndDate = (DateOnly?)null,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)32m } },
            assignedPersonIds = new[] { person },
            estimateChangeReason = "escopo maior",
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A criação grava a primeira estimativa (anterior vazia); a alteração é o segundo registro, o último.
        var estimates = await GetJsonAsync($"/api/demands/{id}/estimate-history");
        Assert.Equal(2, estimates.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, estimates[0].GetProperty("previousHours").ValueKind);
        var change = estimates[1];
        Assert.Equal(16m, change.GetProperty("previousHours").GetDecimal());
        Assert.Equal(32m, change.GetProperty("newHours").GetDecimal());
        Assert.Equal("escopo maior", change.GetProperty("reason").GetString());
        Assert.Equal("Alfa", change.GetProperty("teamName").GetString());

        var forecasts = await GetJsonAsync($"/api/demands/{id}/forecast-history");
        Assert.Equal("Estimate", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
    }

    [Fact]
    public async Task SavingTheSameEstimate_RecordsNoEstimateChange()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Igual", [(team, 16m)], [person], PlanningTestData.StartMonday));
        var before = (await GetJsonAsync($"/api/demands/{id}/estimate-history")).GetArrayLength();

        await _client.PutAsJsonAsync($"/api/demands/{id}", new
        {
            title = "Igual",
            startDate = PlanningTestData.StartMonday,
            informedEndDate = (DateOnly?)null,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)16m } },
            assignedPersonIds = new[] { person },
        }, Ct);

        Assert.Equal(before, (await GetJsonAsync($"/api/demands/{id}/estimate-history")).GetArrayLength());
    }

    [Fact]
    public async Task TheHistoryOfAnUnknownDemand_Returns404()
    {
        var unknown = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/demands/{unknown}/estimate-history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/demands/{unknown}/forecast-history", Ct)).StatusCode);
    }

    [Fact]
    public async Task CompletingWithActualEffort_StoresTheCompletionSnapshot()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Concluir", [(team, 16m)], [person], PlanningTestData.StartMonday));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new
        {
            completedOn = new DateOnly(2027, 1, 6),
            actualEffortHours = (decimal?)20m,
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await SnapshotAsync(id);
        Assert.NotNull(snapshot.Forecast);
        Assert.Equal(16m, snapshot.Estimate);
        Assert.Equal(20m, snapshot.Actual);
    }

    [Fact]
    public async Task CompletingWithoutActualEffort_IsAccepted_AndLeavesItEmpty()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Sem esforço", [(team, 8m)], start: PlanningTestData.StartMonday));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = new DateOnly(2027, 1, 6) }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await SnapshotAsync(id)).Actual);
    }

    [Fact]
    public async Task CompletingWithZeroActualEffort_Returns400_OnTheEffortField()
    {
        var team = await _client.NewTeamAsync();
        var id = await CreateAsync(PlanningTestData.Body("Zero", [(team, 8m)], start: PlanningTestData.StartMonday));

        var response = await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new
        {
            completedOn = new DateOnly(2027, 1, 6),
            actualEffortHours = (decimal?)0m,
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("actualEffortHours", out _));
        Assert.Null((await SnapshotAsync(id)).Actual);
    }

    [Fact]
    public async Task SecondCompletion_Returns409_AndKeepsTheSnapshot()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Duas vezes", [(team, 16m)], [person], PlanningTestData.StartMonday));
        await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = new DateOnly(2027, 1, 6), actualEffortHours = (decimal?)20m }, Ct);
        var first = await SnapshotAsync(id);

        var again = await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = new DateOnly(2027, 1, 7), actualEffortHours = (decimal?)99m }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(first, await SnapshotAsync(id));
    }

    [Fact]
    public async Task TheAccuracyReport_CountsACompletedDemand_WithItsEstimateDifference()
    {
        var team = await _client.NewTeamAsync("Beta");
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Medir", [(team, 16m)], [person], PlanningTestData.StartMonday));
        await _client.PutAsJsonAsync($"/api/demands/{id}/completion", new { completedOn = new DateOnly(2027, 1, 6), actualEffortHours = (decimal?)20m }, Ct);

        var report = await GetJsonAsync("/api/reports/accuracy?from=2027-01-01&to=2027-01-31");

        var sector = report.GetProperty("sector");
        Assert.True(sector.GetProperty("usedCount").GetInt32() >= 1);
        // A diferença de cada demanda entra com o mesmo peso: a média inclui 25% (20 sobre 16), e a conta é de quem entrou.
        Assert.NotEqual(JsonValueKind.Null, sector.GetProperty("averageEstimateDifferencePercent").ValueKind);
        var beta = report.GetProperty("teams").EnumerateArray().Single(team => team.GetProperty("teamName").GetString() == "Beta");
        Assert.Equal(25m, beta.GetProperty("indicators").GetProperty("averageEstimateDifferencePercent").GetDecimal());
    }

    [Fact]
    public async Task AnAccuracyPeriodThatEndsBeforeItStarts_Returns400()
    {
        var response = await _client.GetAsync("/api/reports/accuracy?from=2027-02-01&to=2027-01-01", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingTheStartDate_RecordsTheForecastChange_WithTheAllocationCause()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Mover", [(team, 16m)], [person], PlanningTestData.StartMonday));

        await _client.PutAsJsonAsync($"/api/demands/{id}", new
        {
            title = "Mover",
            startDate = PlanningTestData.StartMonday.AddDays(7),
            informedEndDate = (DateOnly?)null,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)16m } },
            assignedPersonIds = new[] { person },
        }, Ct);

        var forecasts = await GetJsonAsync($"/api/demands/{id}/forecast-history");
        Assert.Equal("Allocation", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
    }

    [Fact]
    public async Task WaitingForAPrerequisite_RecordsTheForecastChange_WithTheDependencyCause()
    {
        var team = await _client.NewTeamAsync();
        var personA = await _client.NewPersonAsync(team);
        var personB = await _client.NewPersonAsync(team);
        var a = await CreateAsync(PlanningTestData.Body("Pré-requisito", [(team, 8m)], [personA], PlanningTestData.StartMonday));
        var b = await CreateAsync(PlanningTestData.Body("Dependente", [(team, 8m)], [personB], PlanningTestData.StartMonday));

        var response = await _client.PutAsJsonAsync($"/api/demands/{b}", new
        {
            title = "Dependente",
            startDate = PlanningTestData.StartMonday,
            informedEndDate = (DateOnly?)null,
            teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)8m } },
            assignedPersonIds = new[] { personB },
            prerequisiteIds = new[] { a },
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forecasts = await GetJsonAsync($"/api/demands/{b}/forecast-history");
        Assert.Equal("Dependency", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
    }

    [Fact]
    public async Task AHolidayOnTheStartDay_RecordsTheForecastChange_WithTheCapacityCause()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var monday = PlanningTestData.StartMonday.AddDays(7 * 40);
        var id = await CreateAsync(PlanningTestData.Body("Feriado", [(team, 8m)], [person], monday));

        var holiday = await _client.PostAsJsonAsync("/api/holidays", new { date = monday, description = "Teste" }, Ct);
        Assert.Equal(HttpStatusCode.Created, holiday.StatusCode);

        var forecasts = await GetJsonAsync($"/api/demands/{id}/forecast-history");
        Assert.Equal("Capacity", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
    }

    [Fact]
    public async Task ApplyingAScenario_RecordsTheForecastChange_WithTheApprovedScenarioCause()
    {
        // A grande vem antes na prioridade e segura a pessoa; ao aprovar a pequena primeiro, ela muda de data.
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var big = await CreateAsync(PlanningTestData.Body("Grande", [(team, 80m)], [person], PlanningTestData.StartMonday));
        var small = await CreateAsync(PlanningTestData.Body("Pequena", [(team, 8m)], [person], PlanningTestData.StartMonday));

        var response = await _client.PostAsJsonAsync("/api/scenarios/apply", new { executionOrder = new[] { small, big } }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var forecasts = await GetJsonAsync($"/api/demands/{small}/forecast-history");
        Assert.Equal("ApprovedScenario", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
    }

    [Fact]
    public async Task ChangingTheSafetyMargin_RecordsTheForecastChange_WithTheSafetyMarginCause()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var id = await CreateAsync(PlanningTestData.Body("Margem", [(team, 40m)], [person], PlanningTestData.StartMonday));
        var original = (await GetJsonAsync("/api/settings")).GetProperty("safetyMarginPercent").GetDecimal();

        try
        {
            var response = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = original + 30m }, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var forecasts = await GetJsonAsync($"/api/demands/{id}/forecast-history");
            Assert.Equal("SafetyMargin", forecasts.EnumerateArray().Last().GetProperty("cause").GetString());
        }
        finally
        {
            // A margem é compartilhada pelas outras classes de teste: volta ao valor que tinha.
            await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = original }, Ct);
        }
    }
}
