using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Planning;

/// <summary>Spec planning-settings (RF-09; DT-22, DT-13): margem de segurança e modo de ordem.</summary>
public class SettingsEndpointsTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>, IAsyncLifetime
{
    private static readonly DateOnly StartMonday = new(2026, 12, 7);

    private static int _nextRank = 2000;

    private HttpClient _client = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _client = await factory.CreateAuthenticatedClientAsync();
        // Os testes compartilham a configuração única: cada um começa com a margem de 20%.
        var reset = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 20 }, Ct);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    /// <summary>Cria uma demanda previsível direto no banco (2 dias úteis de duração, 16 h em 8 h por dia).</summary>
    private async Task<Guid> SeedPredictableDemandAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var team = Team.Create($"Time-{Guid.NewGuid():N}"[..30]);
        db.Teams.Add(team);
        var person = new Person { Name = $"Pessoa-{Guid.NewGuid():N}"[..30], TeamId = team.Id, DailyCapacityHours = 8m };
        db.People.Add(person);
        var rank = Interlocked.Increment(ref _nextRank);
        var demand = new Demand { Title = $"Demanda-{rank}", PriorityRank = rank, ExecutionRank = rank, StartDate = StartMonday };
        db.Demands.Add(demand);
        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = 16m });
        db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = person.Id });
        await db.SaveChangesAsync(Ct);

        // Uma escrita de demanda passa pelo recálculo; aqui basta pedir o recálculo pela configuração.
        var recalc = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 20 }, Ct);
        Assert.Equal(HttpStatusCode.OK, recalc.StatusCode);
        return demand.Id;
    }

    private async Task<(DateOnly EndWithoutMargin, DateOnly EndWithMargin)> ForecastOfAsync(Guid demandId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var forecast = await db.Forecasts.AsNoTracking().SingleAsync(item => item.DemandId == demandId, Ct);
        return (forecast.EndDateWithoutMargin!.Value, forecast.ForecastEndDate!.Value);
    }

    [Fact]
    public async Task InitialValues_AreTwentyPercentAndFollowPriority()
    {
        var body = await ReadAsync(await _client.GetAsync("/api/settings", Ct));

        Assert.Equal(20m, body.GetProperty("safetyMarginPercent").GetDecimal());
        Assert.Equal("FollowPriority", body.GetProperty("executionOrderMode").GetString());
    }

    [Fact]
    public async Task ChangingTheMargin_RecalculatesTheForecasts()
    {
        var demandId = await SeedPredictableDemandAsync();

        var response = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 100 }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100m, (await ReadAsync(response)).GetProperty("safetyMarginPercent").GetDecimal());
        // 16 h a 8 h por dia terminam na terça. Com 100% = 2 dias de margem, a data vai para quinta (com 20%, seria quarta).
        var (endWithoutMargin, endWithMargin) = await ForecastOfAsync(demandId);
        Assert.Equal(new DateOnly(2026, 12, 8), endWithoutMargin);
        Assert.Equal(new DateOnly(2026, 12, 10), endWithMargin);
    }

    [Fact]
    public async Task ZeroMargin_KeepsTheEndDateEqualToTheEndWithoutMargin()
    {
        var demandId = await SeedPredictableDemandAsync();

        var response = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 0 }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (endWithoutMargin, endWithMargin) = await ForecastOfAsync(demandId);
        Assert.Equal(endWithoutMargin, endWithMargin);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(100)]
    public async Task TheMargin_NeverReducesTheDeadline(double percent)
    {
        var demandId = await SeedPredictableDemandAsync();

        await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = (decimal)percent }, Ct);

        var (endWithoutMargin, endWithMargin) = await ForecastOfAsync(demandId);
        Assert.True(endWithMargin >= endWithoutMargin);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000.01)]
    [InlineData(10.555)]
    public async Task InvalidMargin_IsRejected_AndNothingChanges(double invalid)
    {
        var response = await _client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = (decimal)invalid }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.True(body.GetProperty("errors").TryGetProperty("safetyMarginPercent", out _));
        var current = await ReadAsync(await _client.GetAsync("/api/settings", Ct));
        Assert.Equal(20m, current.GetProperty("safetyMarginPercent").GetDecimal());
    }

    [Fact]
    public async Task ExecutionOrderMode_SentInTheBody_IsIgnored()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/settings",
            new { safetyMarginPercent = 20, executionOrderMode = "Custom" },
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("FollowPriority", body.GetProperty("executionOrderMode").GetString());
    }

    [Fact]
    public async Task WithoutAToken_BothRoutesRespond401()
    {
        using var anonymous = factory.CreateClient();

        var read = await anonymous.GetAsync("/api/settings", Ct);
        var write = await anonymous.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 20 }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }
}
