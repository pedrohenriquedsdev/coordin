using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;
using static Coordin.Api.Tests.Infrastructure.RegistryTestData;

namespace Coordin.Api.Tests.Calendar;

/// <summary>Spec capacity-calendar: feriados do setor (RF-03; DT-18).</summary>
public class HolidayEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private static int _nextDay;

    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // Uma data única por chamada, longe das datas dos outros testes, porque a data do feriado é única no banco.
    private static string NewDate() => new DateOnly(2200, 1, 1).AddDays(Interlocked.Increment(ref _nextDay)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _client.PostAsJsonAsync("/api/holidays", body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task RegisteringAHoliday_Returns201_WithLocationAndTheData()
    {
        var date = NewDate();

        var response = await PostAsync(new { date, description = "Natal" });
        var body = await response.ReadAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/holidays/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(date, body.GetProperty("date").GetString());
        Assert.Equal("Natal", body.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AHolidayWithoutADescription_IsAccepted_AndTheDescriptionComesBackEmpty(string? description)
    {
        var response = await PostAsync(new { date = NewDate(), description });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await response.ReadAsync()).GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task ARepeatedDate_Returns409WithAStableCode()
    {
        var date = NewDate();
        await PostAsync(new { date });

        var response = await PostAsync(new { date, description = "Outro" });
        var body = await response.ReadAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("holiday-date-taken", body.GetProperty("code").GetString());
        Assert.Equal("Essa data já é feriado.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TwoSimultaneousRegistrationsOfTheSameDate_ExactlyOneWins()
    {
        for (var round = 1; round <= 5; round++)
        {
            var date = NewDate();

            var responses = await Task.WhenAll(PostAsync(new { date }), PostAsync(new { date }));

            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order().ToList());
            var loser = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("holiday-date-taken", (await loser.ReadAsync()).GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData("""{"description":"sem data"}""")]
    [InlineData("""{"date":"25/12/2200"}""")]
    [InlineData("""{"date":"2200-02-30"}""")]
    public async Task InvalidData_Returns400(string json)
    {
        var response = await _client.PostAsync("/api/holidays", new StringContent(json, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ADescriptionOverTwoHundredCharacters_Returns400OnTheDescriptionField()
    {
        var response = await PostAsync(new { date = NewDate(), description = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync()).GetProperty("errors").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task RemovingAHoliday_Returns204_AndTheDateIsFreeAgain()
    {
        var date = NewDate();
        var id = (await (await PostAsync(new { date })).ReadAsync()).GetProperty("id").GetGuid();

        var delete = await _client.DeleteAsync($"/api/holidays/{id}", TestContext.Current.CancellationToken);
        var again = await PostAsync(new { date });

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task RemovingAnUnknownHoliday_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/holidays/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ACalendarDate_GoesInAndComesBackExactly()
    {
        // Datas de ano novo são as que mais sofrem deslocamento de fuso; aqui nenhum fuso é aplicado.
        var response = await PostAsync(new { date = "2201-12-31", description = "Réveillon" });
        var listed = (await _client.GetFromJsonAsync<JsonElement>("/api/holidays", TestContext.Current.CancellationToken)).EnumerateArray();

        Assert.Equal("2201-12-31", (await response.ReadAsync()).GetProperty("date").GetString());
        Assert.Contains(listed, item => item.GetProperty("date").GetString() == "2201-12-31");
    }
}

/// <summary>Spec capacity-calendar: a lista de feriados por data. Usa uma API própria, para a lista começar vazia.</summary>
public class HolidayListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task HolidaysAreListedByDate_NoMatterTheOrderTheyWereRegistered()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        foreach (var date in new[] { "2027-05-01", "2026-12-25", "2027-01-01", "2027-04-21" })
        {
            await client.PostAsJsonAsync("/api/holidays", new { date }, TestContext.Current.CancellationToken);
        }

        var list = await client.GetFromJsonAsync<JsonElement>("/api/holidays", TestContext.Current.CancellationToken);

        Assert.Equal(
            ["2026-12-25", "2027-01-01", "2027-04-21", "2027-05-01"],
            list.EnumerateArray().Select(item => item.GetProperty("date").GetString()).ToList());
    }

    [Fact]
    public async Task WithoutHolidays_TheListIsEmpty()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/holidays", TestContext.Current.CancellationToken);

        Assert.Empty(list.EnumerateArray());
    }
}
