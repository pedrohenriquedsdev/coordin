using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>
/// Spec new-demand-simulation (RF-16; design D10): simular a inclusão na posição escolhida, sem gravar nada,
/// e confirmar com o mesmo corpo produz as mesmas datas. Usa times e pessoas próprios e não limpa o banco,
/// porque as outras classes também gravam demandas (a contagem total é lida antes de cada cenário).
/// </summary>
public class DemandSimulationTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private static object Body(string title, Guid team, Guid person, decimal? hours, int? position = null) => new
    {
        title,
        startDate = PlanningTestData.StartMonday,
        teams = new[] { new { teamId = team, estimatedEffortHours = hours } },
        assignedPersonIds = new[] { person },
        position,
    };

    private async Task<Guid> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", body, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> SimulateAsync(object body) =>
        await (await _client.PostAsJsonAsync("/api/demands/simulate", body, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<int> CountAsync() =>
        (await _client.GetFromJsonAsync<JsonElement>("/api/demands", Ct)).GetArrayLength();

    private async Task<string> PlanningSnapshotAsync() =>
        (await _client.GetStringAsync("/api/demands", Ct)) + (await _client.GetStringAsync("/api/schedule", Ct));

    /// <summary>Um time com uma pessoa só, e duas demandas dessa pessoa: a de 8 h e a de 16 h.</summary>
    private async Task<(Guid Team, Guid Person, Guid Small, Guid Large)> SharedPersonAsync()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var small = await CreateAsync(Body(Unique("Pequena"), team, person, 8m));
        var large = await CreateAsync(Body(Unique("Grande"), team, person, 16m));
        return (team, person, small, large);
    }

    [Fact]
    public async Task WithoutPosition_TheNewDemandGoesToTheEnd_AndNoExistingDemandChanges()
    {
        var (team, person, _, _) = await SharedPersonAsync();

        var simulation = await SimulateAsync(Body(Unique("Nova"), team, person, 8m));

        Assert.Equal(await CountAsync() + 1, simulation.GetProperty("position").GetInt32());
        Assert.Empty(simulation.GetProperty("changes").EnumerateArray());
    }

    [Fact]
    public async Task InFrontOfTheQueue_DisplacesTheDemandsThatShareThePerson_WithTitlesAndDays()
    {
        var (team, person, small, large) = await SharedPersonAsync();

        var simulation = await SimulateAsync(Body(Unique("Nova"), team, person, 8m, position: 1));

        Assert.Equal(1, simulation.GetProperty("position").GetInt32());
        Assert.True(simulation.GetProperty("isPredictable").GetBoolean());
        var changes = simulation.GetProperty("changes").EnumerateArray().ToList();
        Assert.Contains(changes, change => change.GetProperty("demandId").GetGuid() == small && change.GetProperty("deltaDays").GetInt32() > 0);
        var smallChange = changes.Single(change => change.GetProperty("demandId").GetGuid() == small);
        Assert.False(string.IsNullOrWhiteSpace(smallChange.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Simulating_WritesNothing()
    {
        var (team, person, _, _) = await SharedPersonAsync();
        var before = await PlanningSnapshotAsync();

        await SimulateAsync(Body(Unique("Nova"), team, person, 8m, position: 1));
        await SimulateAsync(Body(Unique("Nova"), team, person, 8m));

        Assert.Equal(before, await PlanningSnapshotAsync());
    }

    [Fact]
    public async Task AnUnpredictableNewDemand_ShowsTheReason_AndDisplacesNobody()
    {
        var (team, person, _, _) = await SharedPersonAsync();

        var simulation = await SimulateAsync(Body(Unique("Sem estimativa"), team, person, null, position: 1));

        Assert.False(simulation.GetProperty("isPredictable").GetBoolean());
        Assert.Equal("MissingEstimate", simulation.GetProperty("unpredictableReason").GetString());
        Assert.Empty(simulation.GetProperty("changes").EnumerateArray());
    }

    [Fact]
    public async Task SimulateThenCreate_InTheSamePosition_GiveTheSameDates()
    {
        var (team, person, small, large) = await SharedPersonAsync();
        var body = Body(Unique("Confirmada"), team, person, 8m, position: 1);

        var simulation = await SimulateAsync(body);
        var id = await CreateAsync(body);

        var created = await (await _client.GetAsync($"/api/demands/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        var simulatedChange = simulation.GetProperty("changes").EnumerateArray().Single(change => change.GetProperty("demandId").GetGuid() == small);
        Assert.Equal(1, created.GetProperty("priorityRank").GetInt32());
        Assert.Equal(simulation.GetProperty("forecastEndDate").GetString(), (await ScheduleItemAsync(id)).GetProperty("forecastEndDate").GetString());
        Assert.Equal(simulatedChange.GetProperty("newForecastEndDate").GetString(), (await ScheduleItemAsync(small)).GetProperty("forecastEndDate").GetString());
    }

    private async Task<JsonElement> ScheduleItemAsync(Guid demandId)
    {
        var schedule = await (await _client.GetAsync("/api/schedule", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        return schedule.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("demandId").GetGuid() == demandId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task APositionBelowOne_Returns400_OnThePositionField(int position)
    {
        var (team, person, _, _) = await SharedPersonAsync();

        var response = await _client.PostAsJsonAsync("/api/demands/simulate", Body(Unique("Fora"), team, person, 8m, position), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("position", out _));
    }

    [Fact]
    public async Task APositionAboveNPlusOne_Returns400_AndWritesNothing()
    {
        var (team, person, _, _) = await SharedPersonAsync();
        var before = await PlanningSnapshotAsync();

        var response = await _client.PostAsJsonAsync("/api/demands/simulate", Body(Unique("Fora"), team, person, 8m, await CountAsync() + 2), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await PlanningSnapshotAsync());
    }

    [Fact]
    public async Task AnEmptyTitle_Returns400_OnTheTitleField()
    {
        var (team, person, _, _) = await SharedPersonAsync();

        var response = await _client.PostAsJsonAsync("/api/demands/simulate", Body("  ", team, person, 8m, position: 1), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task APersonOutsideTheTeams_Returns409_WithTheCode()
    {
        var (team, _, _, _) = await SharedPersonAsync();
        var otherTeam = await _client.NewTeamAsync();
        var outsider = await _client.NewPersonAsync(otherTeam);

        var response = await _client.PostAsJsonAsync("/api/demands/simulate", Body(Unique("Fora do time"), team, outsider, 8m, position: 1), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("person-not-in-demand-teams", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ADemandWithoutTeams_IsSimulated_AsUnpredictable_WithoutFailing()
    {
        await SharedPersonAsync(); // o banco já tem demandas: a simulação não pode depender disso
        var body = new
        {
            title = Unique("Sem times"),
            startDate = PlanningTestData.StartMonday,
            position = 1,
        };

        var response = await _client.PostAsJsonAsync("/api/demands/simulate", body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var simulation = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.False(simulation.GetProperty("isPredictable").GetBoolean());
        Assert.Empty(simulation.GetProperty("changes").EnumerateArray());
    }
}
