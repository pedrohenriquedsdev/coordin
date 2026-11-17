using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>
/// Criar com posição (design D10; RF-04): sem <c>position</c>, a demanda vai para o fim; com ele, entra naquela
/// posição nas duas ordens e as demais descem. Times e pessoas próprios, sem limpar o banco.
/// </summary>
public class DemandCreatePositionTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static object Body(string title, Guid team, Guid person, int? position = null) => new
    {
        title,
        startDate = PlanningTestData.StartMonday,
        teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)8m } },
        assignedPersonIds = new[] { person },
        position,
    };

    private async Task<JsonElement> DemandsAsync() => await _client.GetFromJsonAsync<JsonElement>("/api/demands", Ct);

    private async Task<int> CountAsync() => (await DemandsAsync()).GetArrayLength();

    private async Task<JsonElement> DemandAsync(Guid id) =>
        await (await _client.GetAsync($"/api/demands/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task WithoutPosition_TheDemandGoesToTheEnd()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var before = await CountAsync();

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("Sem posição"), team, person), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(before + 1, created.GetProperty("priorityRank").GetInt32());
        Assert.Equal(before + 1, created.GetProperty("executionRank").GetInt32());
    }

    [Fact]
    public async Task WithAPosition_TheDemandTakesIt_InBothOrders_AndTheOthersMoveDown()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var last = await CountAsync(); // n: a última antiga ocupa o ranking n
        var lastId = (await DemandsAsync()).EnumerateArray().Single(item => item.GetProperty("priorityRank").GetInt32() == last).GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("Na penúltima"), team, person, last), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var created = await DemandAsync(id);
        Assert.Equal(last, created.GetProperty("priorityRank").GetInt32());
        Assert.Equal(last, created.GetProperty("executionRank").GetInt32());
        var moved = await DemandAsync(lastId);
        Assert.Equal(last + 1, moved.GetProperty("priorityRank").GetInt32());
        Assert.Equal(last + 1, moved.GetProperty("executionRank").GetInt32());
    }

    [Fact]
    public async Task APositionEqualToNPlusOne_IsTheSameAsNotSendingIt()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var before = await CountAsync();

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("Fim explícito"), team, person, before + 1), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(before + 1, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("priorityRank").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task APositionBelowOne_Returns400_AndSavesNothing(int position)
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var before = await CountAsync();

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("Inválida"), team, person, position), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task APositionAboveNPlusOne_Returns400_AndSavesNothing()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var before = await CountAsync();

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("Acima"), team, person, before + 2), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountAsync());
    }

    [Fact]
    public async Task SimulateThenCreate_AtTheSamePosition_GiveTheSameForecastForTheNewDemand()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var body = Body(Unique("Simulada e criada"), team, person, 2);

        var simulated = await (await _client.PostAsJsonAsync("/api/demands/simulate", body, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var created = await (await _client.PostAsJsonAsync("/api/demands", body, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        var id = created.GetProperty("id").GetGuid();
        var schedule = await _client.GetFromJsonAsync<JsonElement>("/api/schedule", Ct);
        var item = schedule.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("demandId").GetGuid() == id);
        Assert.Equal(simulated.GetProperty("forecastEndDate").GetString(), item.GetProperty("forecastEndDate").GetString());
        Assert.Equal(simulated.GetProperty("forecastStartDate").GetString(), item.GetProperty("forecastStartDate").GetString());
    }
}
