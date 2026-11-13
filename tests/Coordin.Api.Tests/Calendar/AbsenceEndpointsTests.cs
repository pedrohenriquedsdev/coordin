using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;
using static Coordin.Api.Tests.Infrastructure.RegistryTestData;

namespace Coordin.Api.Tests.Calendar;

/// <summary>Spec capacity-calendar: ausências das pessoas (RF-03; RNF-05).</summary>
public class AbsenceEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task<(Guid TeamId, Guid PersonId)> NewPersonInNewTeamAsync()
    {
        var teamId = await _client.NewTeamAsync();
        return (teamId, await _client.NewPersonAsync(teamId));
    }

    private async Task<JsonElement> AllAbsencesAsync() =>
        await _client.GetFromJsonAsync<JsonElement>("/api/absences", TestContext.Current.CancellationToken);

    // ---- Registrar ----

    [Fact]
    public async Task RegisteringVacation_Returns201_WithLocationAndThePersonAndTeamData()
    {
        var (teamId, personId) = await NewPersonInNewTeamAsync();

        var response = await _client.PostAbsenceAsync(personId, new { type = "Vacation", startDate = "2027-01-04", endDate = "2027-01-15" });
        var body = await response.ReadAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/absences/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(personId, body.GetProperty("personId").GetGuid());
        Assert.Equal(teamId, body.GetProperty("teamId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("personName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("teamName").GetString()));
        Assert.Equal("Vacation", body.GetProperty("type").GetString());
        Assert.Equal("2027-01-04", body.GetProperty("startDate").GetString());
        Assert.Equal("2027-01-15", body.GetProperty("endDate").GetString());
    }

    [Fact]
    public async Task AnUnknownPerson_Returns404_OnRegisteringAndOnListing()
    {
        var unknown = Guid.CreateVersion7();

        var post = await _client.PostAbsenceAsync(unknown, new { type = "Vacation", startDate = "2027-01-04", endDate = "2027-01-15" });
        var get = await _client.GetAsync($"/api/people/{unknown}/absences", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task OverlappingAbsencesOfTheSamePerson_AreBothAccepted()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();

        await _client.NewAbsenceAsync(personId, start: "2027-01-04", end: "2027-01-15");
        await _client.NewAbsenceAsync(personId, "Other", start: "2027-01-10", end: "2027-01-20");
    }

    [Fact]
    public async Task APropertyCalledReason_IsIgnored_AndNoResponseEverCarriesIt()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();
        const string secret = "consulta-medica-motivo-secreto";

        var created = await _client.PostAbsenceAsync(personId, new { type = "Other", startDate = "2027-02-08", endDate = "2027-02-08", reason = secret });
        var createdText = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var listText = await (await _client.GetAsync($"/api/people/{personId}/absences", TestContext.Current.CancellationToken))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("reason", createdText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, createdText + listText, StringComparison.Ordinal);
    }

    // ---- Período e tipo ----

    [Fact]
    public async Task AnEndBeforeTheStart_Returns400OnTheEndDateField_AndNothingIsStored()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();

        var response = await _client.PostAbsenceAsync(personId, new { type = "Vacation", startDate = "2027-01-15", endDate = "2027-01-04" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync();
        Assert.Equal("O fim não pode ser anterior ao início.", problem.GetProperty("errors").GetProperty("endDate")[0].GetString());
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/people/{personId}/absences", TestContext.Current.CancellationToken);
        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task AnAbsenceOfASingleDay_IsAccepted()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();

        await _client.NewAbsenceAsync(personId, "Other", "2027-02-08", "2027-02-08");
    }

    [Theory]
    [InlineData("""{"type":"Holiday","startDate":"2027-01-04","endDate":"2027-01-15"}""")] // tipo desconhecido
    [InlineData("""{"startDate":"2027-01-04","endDate":"2027-01-15"}""")] // sem tipo
    [InlineData("""{"type":"Vacation","endDate":"2027-01-15"}""")] // sem início
    [InlineData("""{"type":"Vacation","startDate":"2027-01-04"}""")] // sem fim
    [InlineData("""{"type":"Vacation","startDate":"04/01/2027","endDate":"15/01/2027"}""")] // formato inválido
    public async Task AnInvalidBody_Returns400(string json)
    {
        var (_, personId) = await NewPersonInNewTeamAsync();

        var response = await _client.PostAsync(
            $"/api/people/{personId}/absences",
            new StringContent(json, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CalendarDates_GoInAndComeBackExactly_WithoutShiftingADay()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();
        var id = await _client.NewAbsenceAsync(personId, start: "2026-12-31", end: "2027-01-02");

        var all = await AllAbsencesAsync();
        var absence = Assert.Single(all.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

        Assert.Equal("2026-12-31", absence.GetProperty("startDate").GetString());
        Assert.Equal("2027-01-02", absence.GetProperty("endDate").GetString());
    }

    // ---- Consultar ----

    [Fact]
    public async Task ListingOnePerson_ShowsOnlyTheirAbsences_OrderedByStart()
    {
        var teamId = await _client.NewTeamAsync();
        var ana = await _client.NewPersonAsync(teamId);
        var bruno = await _client.NewPersonAsync(teamId);
        await _client.NewAbsenceAsync(ana, start: "2027-03-01", end: "2027-03-05");
        await _client.NewAbsenceAsync(ana, start: "2027-01-01", end: "2027-01-05");
        await _client.NewAbsenceAsync(bruno, start: "2027-02-01", end: "2027-02-05");

        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/people/{ana}/absences", TestContext.Current.CancellationToken);

        var starts = list.EnumerateArray().Select(item => item.GetProperty("startDate").GetString()).ToList();
        Assert.Equal(["2027-01-01", "2027-03-01"], starts);
        Assert.All(list.EnumerateArray(), item => Assert.Equal(ana, item.GetProperty("personId").GetGuid()));
    }

    [Fact]
    public async Task ListingEveryone_ShowsThePersonAndTheTeamOfEachAbsence()
    {
        var (teamId, personId) = await NewPersonInNewTeamAsync();
        var id = await _client.NewAbsenceAsync(personId);

        var absence = Assert.Single((await AllAbsencesAsync()).EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

        Assert.Equal(personId, absence.GetProperty("personId").GetGuid());
        Assert.Equal(teamId, absence.GetProperty("teamId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(absence.GetProperty("personName").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(absence.GetProperty("teamName").GetString()));
    }

    // ---- Editar e excluir ----

    [Fact]
    public async Task ChangingThePeriod_Returns200WithTheUpdatedAbsence()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();
        var id = await _client.NewAbsenceAsync(personId);

        var response = await _client.PutAsJsonAsync(
            $"/api/absences/{id}",
            new { type = "Other", startDate = "2027-05-03", endDate = "2027-05-07" },
            TestContext.Current.CancellationToken);
        var body = await response.ReadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Other", body.GetProperty("type").GetString());
        Assert.Equal("2027-05-03", body.GetProperty("startDate").GetString());
        Assert.Equal("2027-05-07", body.GetProperty("endDate").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("teamName").GetString()));
    }

    [Fact]
    public async Task AnInvalidPeriodOnUpdate_Returns400_AndTheAbsenceDoesNotChange()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();
        var id = await _client.NewAbsenceAsync(personId, start: "2027-01-04", end: "2027-01-15");

        var response = await _client.PutAsJsonAsync(
            $"/api/absences/{id}",
            new { type = "Vacation", startDate = "2027-01-15", endDate = "2027-01-04" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadAsync()).GetProperty("errors").TryGetProperty("endDate", out _));
        var absence = Assert.Single((await AllAbsencesAsync()).EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal("2027-01-15", absence.GetProperty("endDate").GetString());
    }

    [Fact]
    public async Task DeletingAnAbsence_Returns204_AndItDisappears()
    {
        var (_, personId) = await NewPersonInNewTeamAsync();
        var id = await _client.NewAbsenceAsync(personId);

        var response = await _client.DeleteAsync($"/api/absences/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain((await AllAbsencesAsync()).EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task AnUnknownAbsence_Returns404_OnPutAndOnDelete()
    {
        var unknown = Guid.CreateVersion7();

        var put = await _client.PutAsJsonAsync(
            $"/api/absences/{unknown}",
            new { type = "Vacation", startDate = "2027-01-04", endDate = "2027-01-15" },
            TestContext.Current.CancellationToken);
        var delete = await _client.DeleteAsync($"/api/absences/{unknown}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}

/// <summary>Spec capacity-calendar: listas vazias e ordem das ausências. Usa uma API própria, para as listas começarem vazias.</summary>
public class AbsenceListTests(PostgresFixture postgres)
{
    [Fact]
    public async Task WithoutAbsences_BothListingsAreEmpty()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var personId = await client.NewPersonAsync(await client.NewTeamAsync());

        var all = await client.GetFromJsonAsync<JsonElement>("/api/absences", TestContext.Current.CancellationToken);
        var one = await client.GetFromJsonAsync<JsonElement>($"/api/people/{personId}/absences", TestContext.Current.CancellationToken);

        Assert.Empty(all.EnumerateArray());
        Assert.Empty(one.EnumerateArray());
    }

    [Fact]
    public async Task AbsencesStartingOnTheSameDay_AreOrderedByThePersonName()
    {
        await using var factory = new TestingApiFactory(postgres);
        await factory.InitializeAsync();
        using var client = await factory.CreateAuthenticatedClientAsync();
        var teamId = await client.NewTeamAsync();
        var zelia = await client.NewPersonAsync(teamId, "zélia");
        var ana = await client.NewPersonAsync(teamId, "Ana");
        await client.NewAbsenceAsync(zelia, start: "2027-01-04", end: "2027-01-05");
        await client.NewAbsenceAsync(ana, start: "2027-01-04", end: "2027-01-05");
        await client.NewAbsenceAsync(ana, start: "2026-12-21", end: "2026-12-22");

        var all = await client.GetFromJsonAsync<JsonElement>("/api/absences", TestContext.Current.CancellationToken);

        var order = all.EnumerateArray()
            .Select(item => $"{item.GetProperty("startDate").GetString()}/{item.GetProperty("personName").GetString()}")
            .ToList();
        Assert.Equal(["2026-12-21/Ana", "2027-01-04/Ana", "2027-01-04/zélia"], order);
    }
}
