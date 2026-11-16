using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Calendar;

/// <summary>
/// Spec capacity-calendar (delta da change demand-planning): ausências e feriados recalculam as previsões
/// (RF-03; DT-03). Os feriados usam datas de 2030 para não afetar as demandas de 2027 de outros testes.
/// </summary>
public class CalendarRecalculationTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private async Task<DateOnly> ForecastEndAsync(Guid demandId)
    {
        var schedule = await ReadAsync(await _client.GetAsync("/api/schedule", Ct));
        var item = schedule.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("demandId").GetGuid() == demandId);
        return PlanningTestData.ParseDate(item.GetProperty("forecastEndDate").GetString()!);
    }

    /// <summary>Demanda de 16 h, com a pessoa de 8 h por dia: termina na terça e, com margem, na quarta.</summary>
    private async Task<(Guid Person, Guid Demand)> SeedAsync()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var demand = await CreateDemandAsync(PlanningTestData.Body("Execução", [(team, 16m)], [person], PlanningTestData.StartMonday));
        return (person, demand);
    }

    [Fact]
    public async Task AbsenceInTheMiddle_PushesTheForecast_AndDeletingItRestoresIt()
    {
        var (person, demand) = await SeedAsync();
        var before = await ForecastEndAsync(demand);

        var created = await _client.PostAbsenceAsync(person, new { type = "Vacation", startDate = PlanningTestData.StartMonday, endDate = PlanningTestData.StartMonday });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var absenceId = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var pushed = await ForecastEndAsync(demand);

        Assert.True(pushed > before, $"Esperava depois de {before}, veio {pushed}.");

        await _client.DeleteAsync($"/api/absences/{absenceId}", Ct);

        Assert.Equal(before, await ForecastEndAsync(demand));
    }

    [Fact]
    public async Task HolidayOnAWorkingDay_ofTheDemand_PushesTheEnd()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var demand = await CreateDemandAsync(PlanningTestData.Body("Três dias", [(team, 24m)], [person], PlanningTestData.StartMonday));
        var before = await ForecastEndAsync(demand);
        var holiday = PlanningTestData.StartMonday.AddDays(1); // terça-feira de 2027, no meio da execução

        var created = await _client.PostAsJsonAsync("/api/holidays", new { date = holiday, description = "Feriado de teste" }, Ct);
        var holidayId = (await ReadAsync(created)).GetProperty("id").GetGuid();
        try
        {
            Assert.True(await ForecastEndAsync(demand) > before, "Feriado no meio da execução devia empurrar o fim.");
        }
        finally
        {
            await _client.DeleteAsync($"/api/holidays/{holidayId}", Ct);
        }

        Assert.Equal(before, await ForecastEndAsync(demand));
    }

    [Fact]
    public async Task RejectedHoliday_LeavesTheForecastsAsTheyWere()
    {
        var (_, demand) = await SeedAsync();
        var date = new DateOnly(2030, 3, 4);
        var first = await _client.PostAsJsonAsync("/api/holidays", new { date, description = "Primeiro" }, Ct);
        var holidayId = (await ReadAsync(first)).GetProperty("id").GetGuid();
        var before = await ForecastEndAsync(demand);

        try
        {
            var repeated = await _client.PostAsJsonAsync("/api/holidays", new { date, description = "Repetido" }, Ct);

            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
            Assert.Equal(before, await ForecastEndAsync(demand));
        }
        finally
        {
            await _client.DeleteAsync($"/api/holidays/{holidayId}", Ct);
        }
    }

    [Fact]
    public async Task RejectedAbsence_LeavesTheForecastsAsTheyWere()
    {
        var (person, demand) = await SeedAsync();
        var before = await ForecastEndAsync(demand);

        var response = await _client.PostAbsenceAsync(person, new { type = "Vacation", startDate = PlanningTestData.StartMonday.AddDays(3), endDate = PlanningTestData.StartMonday });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await ForecastEndAsync(demand));
    }
}
