using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Demands;

/// <summary>
/// Spec demand-dependencies (RF-18; DT-17, DT-23): os pré-requisitos entram e saem pelo agregado da
/// demanda. Os testes não apagam as demandas das outras classes: cada um usa títulos e ids próprios.
/// </summary>
public class DemandDependencyTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    /// <summary>Corpo com <c>prerequisiteIds</c>; <c>null</c> manda o campo como <c>null</c>.</summary>
    private static object Body(string title, Guid team, IEnumerable<Guid>? prerequisites) => new
    {
        title,
        startDate = PlanningTestData.StartMonday,
        teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)8m } },
        assignedPersonIds = Array.Empty<Guid>(),
        prerequisiteIds = prerequisites?.ToArray(),
    };

    /// <summary>Corpo sem a propriedade <c>prerequisiteIds</c> de jeito nenhum.</summary>
    private static object BodyWithoutField(string title, Guid team) => new
    {
        title,
        startDate = PlanningTestData.StartMonday,
        teams = new[] { new { teamId = team, estimatedEffortHours = (decimal?)8m } },
        assignedPersonIds = Array.Empty<Guid>(),
    };

    private async Task<Guid> CreateAsync(string title, Guid team, params Guid[] prerequisites)
    {
        var response = await _client.PostAsJsonAsync("/api/demands", Body(title, team, prerequisites), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> GetAsync(Guid id) =>
        await (await _client.GetAsync($"/api/demands/{id}", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static List<Guid> PrerequisitesOf(JsonElement demand) =>
        demand.GetProperty("prerequisiteIds").EnumerateArray().Select(item => item.GetGuid()).ToList();

    [Fact]
    public async Task CreateWithAPrerequisite_Returns201_AndGetReturnsIt()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);

        var b = await CreateAsync(Unique("B"), team, a);

        Assert.Equal([a], PrerequisitesOf(await GetAsync(b)));
    }

    [Fact]
    public async Task DemandWithoutThePrerequisiteField_HasNoPrerequisites()
    {
        var team = await _client.NewTeamAsync();

        var response = await _client.PostAsJsonAsync("/api/demands", BodyWithoutField(Unique("Sem campo"), team), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Empty(PrerequisitesOf(created));
    }

    [Fact]
    public async Task UpdateWithoutThePrerequisiteField_RemovesThePrerequisites()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team, a);

        var response = await _client.PutAsJsonAsync($"/api/demands/{b}", BodyWithoutField(Unique("B"), team), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(PrerequisitesOf(await GetAsync(b)));
    }

    [Fact]
    public async Task UpdateWithNewPrerequisites_ReplacesTheWholeSet()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var c = await CreateAsync(Unique("C"), team);
        var b = await CreateAsync(Unique("B"), team, a);

        var response = await _client.PutAsJsonAsync($"/api/demands/{b}", Body(Unique("B"), team, [c]), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([c], PrerequisitesOf(await GetAsync(b)));
    }

    [Fact]
    public async Task PrerequisiteOfAnUnknownDemand_Returns400_OnThePrerequisiteField_AndSavesNothing()
    {
        var team = await _client.NewTeamAsync();
        var title = Unique("Sem pai");

        var response = await _client.PostAsJsonAsync("/api/demands", Body(title, team, [Guid.NewGuid()]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("prerequisiteIds", out _));
        Assert.DoesNotContain(
            (await (await _client.GetAsync("/api/demands", Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray(),
            demand => demand.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task RepeatedPrerequisite_Returns400_OnThePrerequisiteField()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);

        var response = await _client.PostAsJsonAsync("/api/demands", Body(Unique("B"), team, [a, a]), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("prerequisiteIds", out _));
    }

    [Fact]
    public async Task CompletedPrerequisite_IsAccepted()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A concluída"), team);
        var completion = await _client.PutAsJsonAsync($"/api/demands/{a}/completion", new { completedOn = PlanningTestData.StartMonday }, Ct);
        Assert.Equal(HttpStatusCode.OK, completion.StatusCode);

        var b = await CreateAsync(Unique("B"), team, a);

        Assert.Equal([a], PrerequisitesOf(await GetAsync(b)));
    }

    /// <summary>O corpo do Problem Details, lido uma única vez (o conteúdo da resposta só pode ser lido uma vez).</summary>
    private static async Task<JsonElement> ProblemOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static string CodeOf(JsonElement problem) => problem.GetProperty("code").GetString()!;

    private static string DetailOf(JsonElement problem) => problem.GetProperty("detail").GetString()!;

    [Fact]
    public async Task DirectCycle_OnUpdate_Returns409_AndWritesNothing()
    {
        var team = await _client.NewTeamAsync();
        var titleA = Unique("Ciclo A");
        var titleB = Unique("Ciclo B");
        var a = await CreateAsync(titleA, team);
        var b = await CreateAsync(titleB, team, a); // B depende de A

        var response = await _client.PutAsJsonAsync($"/api/demands/{a}", Body(titleA, team, [b]), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ProblemOf(response);
        Assert.Equal("dependency-cycle", CodeOf(problem));
        Assert.Contains(titleA, DetailOf(problem));
        Assert.Contains(titleB, DetailOf(problem));
        Assert.Empty(PrerequisitesOf(await GetAsync(a)));
        Assert.Equal([a], PrerequisitesOf(await GetAsync(b)));
    }

    [Fact]
    public async Task IndirectCycle_OnUpdate_Returns409()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team, a);
        var c = await CreateAsync(Unique("C"), team, b); // C → B → A

        var response = await _client.PutAsJsonAsync($"/api/demands/{a}", Body(Unique("A"), team, [c]), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("dependency-cycle", CodeOf(await ProblemOf(response)));
        Assert.Empty(PrerequisitesOf(await GetAsync(a)));
    }

    [Fact]
    public async Task DemandDependingOnItself_OnUpdate_Returns409()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);

        var response = await _client.PutAsJsonAsync($"/api/demands/{a}", Body(Unique("A"), team, [a]), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("dependency-cycle", CodeOf(await ProblemOf(response)));
    }

    [Fact]
    public async Task ChainWithoutCycle_IsAccepted_OnCreate()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team, a);

        var c = await CreateAsync(Unique("C"), team, b);

        Assert.Equal([b], PrerequisitesOf(await GetAsync(c)));
    }

    [Fact]
    public async Task CompletedDemand_CannotChangeItsPrerequisites_Returns409Locked()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team);
        var completed = await CreateAsync(Unique("Concluída"), team);
        await _client.PutAsJsonAsync($"/api/demands/{completed}/completion", new { completedOn = PlanningTestData.StartMonday }, Ct);

        var response = await _client.PutAsJsonAsync($"/api/demands/{completed}", Body(Unique("Concluída"), team, [a, b]), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("completed-demand-locked", CodeOf(await ProblemOf(response)));
    }

    [Fact]
    public async Task DeletingAPrerequisite_Returns409_NamingTheDependents_AndKeepsIt()
    {
        var team = await _client.NewTeamAsync();
        var titleA = Unique("Pré-requisito");
        var titleB = Unique("Dependente");
        var a = await CreateAsync(titleA, team);
        var b = await CreateAsync(titleB, team, a);

        var response = await _client.DeleteAsync($"/api/demands/{a}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ProblemOf(response);
        Assert.Equal("demand-has-dependents", CodeOf(problem));
        Assert.Contains(titleB, DetailOf(problem));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/demands/{a}", Ct)).StatusCode);
        Assert.Equal([a], PrerequisitesOf(await GetAsync(b)));
    }

    [Fact]
    public async Task DeletingAfterRemovingTheDependency_Returns204()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team, a);
        await _client.PutAsJsonAsync($"/api/demands/{b}", Body(Unique("B"), team, []), Ct);

        var response = await _client.DeleteAsync($"/api/demands/{a}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeletingADependent_Returns204_AndThePrerequisiteStays()
    {
        var team = await _client.NewTeamAsync();
        var a = await CreateAsync(Unique("A"), team);
        var b = await CreateAsync(Unique("B"), team, a);

        var response = await _client.DeleteAsync($"/api/demands/{b}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/demands/{a}", Ct)).StatusCode);
        Assert.Empty(PrerequisitesOf(await GetAsync(a)));
    }
}
