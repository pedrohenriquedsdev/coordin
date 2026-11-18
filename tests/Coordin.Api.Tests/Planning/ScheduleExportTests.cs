using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Planning;

/// <summary>
/// Spec schedule-export (RF-19; DT-20; RNF-04): a exportação do cronograma em CSV, com o banco real. Confere o
/// arquivo que sai, a ordem, a proteção por login e que a exportação não grava nada.
/// </summary>
public class ScheduleExportTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
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

    private async Task<Guid> CreateAsync(string title, Guid team, Guid person)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/demands",
            PlanningTestData.Body(title, [(team, 40m)], [person], PlanningTestData.StartMonday),
            Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> ExportAsync() => await _client.GetAsync("/api/schedule/export", Ct);

    /// <summary>Os bytes do arquivo, sem o BOM, como texto.</summary>
    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task Export_ReturnsCsvWithBom_HeaderAndOneRowPerDemand()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        await CreateAsync("Alfa", team, person);
        await CreateAsync("Bravo", team, person);

        var response = await ExportAsync();
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());

        var lines = Lines(Decode(bytes));
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Prioridade;Ordem de execução;", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("1;1;Não;Alfa;", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2;2;Não;Bravo;", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_FollowsTheCurrentOrder_AfterAReorder()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        var alfa = await CreateAsync("Alfa", team, person);
        var bravo = await CreateAsync("Bravo", team, person);

        var reorder = await _client.PutAsJsonAsync("/api/demands/priority-order", new { demandIds = new[] { bravo, alfa } }, Ct);
        Assert.True(reorder.IsSuccessStatusCode);

        var lines = Lines(Decode(await (await ExportAsync()).Content.ReadAsByteArrayAsync(Ct)));
        Assert.StartsWith("1;1;Não;Bravo;", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2;2;Não;Alfa;", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WithNoDemands_ReturnsOnlyTheHeader()
    {
        var lines = Lines(Decode(await (await ExportAsync()).Content.ReadAsByteArrayAsync(Ct)));

        Assert.Single(lines);
        Assert.StartsWith("Prioridade;Ordem de execução;", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_IsAnAttachment_WithTheDatedFileName()
    {
        var response = await ExportAsync();

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Matches(@"^coordin-cronograma-\d{4}-\d{2}-\d{2}\.csv$", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
    }

    [Fact]
    public async Task Export_RespondsUnauthorized_WithoutAToken()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/schedule/export", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_DoesNotChangeAnything()
    {
        var team = await _client.NewTeamAsync();
        var person = await _client.NewPersonAsync(team);
        await CreateAsync("Alfa", team, person);
        var demandsBefore = await _client.GetStringAsync("/api/demands", Ct);
        var scheduleBefore = await _client.GetStringAsync("/api/schedule", Ct);

        await ExportAsync();

        Assert.Equal(demandsBefore, await _client.GetStringAsync("/api/demands", Ct));
        Assert.Equal(scheduleBefore, await _client.GetStringAsync("/api/schedule", Ct));
    }
}
