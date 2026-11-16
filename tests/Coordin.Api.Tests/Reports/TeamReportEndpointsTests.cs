using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Reports;

/// <summary>Spec team-report-api: relatório por time no período (RF-17; DT-24). Cada teste usa o próprio time.</summary>
public class TeamReportEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static readonly DateOnly Monday = PlanningTestData.StartMonday; // 2027-01-04

    private async Task<JsonElement> ReportAsync(DateOnly from, DateOnly to) =>
        await _client.GetFromJsonAsync<JsonElement>($"/api/reports/teams?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", Ct);

    private static JsonElement TeamOf(JsonElement report, Guid teamId) =>
        report.GetProperty("teams").EnumerateArray().Single(team => team.GetProperty("teamId").GetGuid() == teamId);

    private async Task<Guid> CreateDemandAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Period_FromTheMiddleOfAWeek_IsAlignedToWholeWeeks_AndHasTwoWeeks()
    {
        var team = await _client.NewTeamAsync();
        await _client.NewPersonAsync(team);

        var report = await ReportAsync(Monday.AddDays(2), Monday.AddDays(8)); // quarta a terça seguinte

        Assert.Equal(Monday, DateOnly.ParseExact(report.GetProperty("periodStart").GetString()!, "yyyy-MM-dd"));
        Assert.Equal(Monday.AddDays(13), DateOnly.ParseExact(report.GetProperty("periodEnd").GetString()!, "yyyy-MM-dd"));
        Assert.Equal(2, TeamOf(report, team).GetProperty("weeks").GetArrayLength());
    }

    [Fact]
    public async Task EachWeek_CarriesTheCapacityAllocationOccupancyAndIdleness()
    {
        var team = await _client.NewTeamAsync();
        await _client.NewPersonAsync(team); // 8 h por dia útil: 40 h na semana

        var week = TeamOf(await ReportAsync(Monday, Monday.AddDays(6)), team).GetProperty("weeks")[0];

        Assert.Equal(40m, week.GetProperty("capacityHours").GetDecimal());
        Assert.Equal(0m, week.GetProperty("allocatedHours").GetDecimal());
        Assert.Equal(0m, week.GetProperty("occupancy").GetDecimal());
        Assert.Equal(40m, week.GetProperty("idleHours").GetDecimal());
        Assert.False(week.GetProperty("isOverloaded").GetBoolean());
    }

    [Fact]
    public async Task TeamWithoutPeople_HasZeroCapacity_AndNullOccupancy()
    {
        var team = await _client.NewTeamAsync();

        var week = TeamOf(await ReportAsync(Monday, Monday.AddDays(6)), team).GetProperty("weeks")[0];

        Assert.Equal(0m, week.GetProperty("capacityHours").GetDecimal());
        Assert.Equal(JsonValueKind.Null, week.GetProperty("occupancy").ValueKind);
    }

    [Fact]
    public async Task AllocatedHours_AreTheForecastConsumption_OfTheWeek()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        await CreateDemandAsync(PlanningTestData.Body("Dezesseis horas", [(team, 16m)], [person], Monday));

        var week = TeamOf(await ReportAsync(Monday, Monday.AddDays(6)), team).GetProperty("weeks")[0];

        Assert.Equal(16m, week.GetProperty("allocatedHours").GetDecimal());
        Assert.Equal(24m, week.GetProperty("idleHours").GetDecimal());
    }

    [Fact]
    public async Task OverloadedWeek_ListsTheDemandsWaitingForCapacity()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var title = RegistryTestData.Unique("Fila");
        var demand = await CreateDemandAsync(PlanningTestData.Body(title, [(team, 100m)], [person], Monday));

        var week = TeamOf(await ReportAsync(Monday, Monday.AddDays(6)), team).GetProperty("weeks")[0];

        Assert.True(week.GetProperty("isOverloaded").GetBoolean());
        var listed = week.GetProperty("overloadedDemands").EnumerateArray().Single();
        Assert.Equal(demand, listed.GetProperty("id").GetGuid());
        Assert.Equal(title, listed.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CompletedDemand_StopsCountingAsAllocated()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var demand = await CreateDemandAsync(PlanningTestData.Body("Concluída", [(team, 16m)], [person], Monday));
        await _client.PutAsJsonAsync($"/api/demands/{demand}/completion", new { completedOn = Monday }, Ct);

        var week = TeamOf(await ReportAsync(Monday, Monday.AddDays(6)), team).GetProperty("weeks")[0];

        Assert.Equal(0m, week.GetProperty("allocatedHours").GetDecimal());
    }

    [Fact]
    public async Task UnpredictableDemands_AreCountedForTheTeam()
    {
        var team = await _client.NewTeamAsync();
        await _client.NewPersonAsync(team);
        await CreateDemandAsync(PlanningTestData.Body("Sem estimativa", [(team, null)], start: Monday));

        var report = await ReportAsync(Monday, Monday.AddDays(6));

        Assert.Equal(1, TeamOf(report, team).GetProperty("unpredictableDemandCount").GetInt32());
    }

    [Fact]
    public async Task TeamsAreListedInAlphabeticalOrder_IgnoringCase()
    {
        var zebra = await _client.NewTeamAsync("zebra-" + Guid.NewGuid().ToString("N")[..6]);
        var alfa = await _client.NewTeamAsync("Alfa-" + Guid.NewGuid().ToString("N")[..6]);

        var names = (await ReportAsync(Monday, Monday.AddDays(6))).GetProperty("teams").EnumerateArray()
            .Where(team => team.GetProperty("teamId").GetGuid() == zebra || team.GetProperty("teamId").GetGuid() == alfa)
            .Select(team => team.GetProperty("teamName").GetString()!)
            .ToList();

        Assert.True(string.CompareOrdinal(names[0].ToUpperInvariant(), names[1].ToUpperInvariant()) < 0);
    }

    [Fact]
    public async Task Report_DoesNotChangeTheForecasts()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        await CreateDemandAsync(PlanningTestData.Body("Não muda", [(team, 16m)], [person], Monday));
        var before = await _client.GetFromJsonAsync<JsonElement>("/api/schedule", Ct);

        await ReportAsync(Monday, Monday.AddDays(13));

        var after = await _client.GetFromJsonAsync<JsonElement>("/api/schedule", Ct);
        Assert.Equal(before.ToString(), after.ToString());
    }

    [Theory]
    [InlineData("/api/reports/teams")]
    [InlineData("/api/reports/teams?from=2027-01-04")]
    [InlineData("/api/reports/teams?from=2027-13-01&to=2027-01-10")]
    public async Task MissingOrInvalidDates_Return400_OnTheField(string path)
    {
        var response = await _client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync()).GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task EndBeforeStart_Returns400_OnTheEndField()
    {
        var response = await _client.GetAsync($"/api/reports/teams?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(-1):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync()).GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task PeriodLongerThan52Weeks_Returns400_OnTheEndField()
    {
        var response = await _client.GetAsync($"/api/reports/teams?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(400):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync()).GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/reports/teams?from={Monday:yyyy-MM-dd}&to={Monday:yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
