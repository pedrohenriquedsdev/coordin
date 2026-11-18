using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// A avaliação pela API (RF-24; DT-37; RNF-02): respostas, os 409 de pré-requisito e o que a avaliação não grava.
/// Cada teste começa recarregando o conjunto, porque a fábrica compartilha o banco da classe.
/// </summary>
public sealed class ValidationEvaluateTests(ValidationEnabledApiFactory factory) : IClassFixture<ValidationEnabledApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object Parameters = new { minEffortFactor = 0.9m, maxEffortFactor = 1.1m, seed = 20261126L };

    private async Task<HttpClient> ResetAsync(bool captureCurrentPlan, bool completeBaseline = true)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var load = await client.PostAsJsonAsync("/api/validation/dataset", new { confirmReplaceAll = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, load.StatusCode);

        if (completeBaseline)
        {
            var demands = await client.GetFromJsonAsync<JsonElement>("/api/demands", Ct);
            var baselines = demands.EnumerateArray()
                .Select(demand => new { demandId = demand.GetProperty("id").GetGuid(), baselineDate = "2027-06-30" })
                .ToArray();
            var put = await client.PutAsJsonAsync("/api/validation/baseline", new { baselines }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        }

        if (captureCurrentPlan)
        {
            var capture = await client.PostAsync("/api/validation/current-plan", null, Ct);
            Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        }

        return client;
    }

    [Fact]
    public async Task Evaluate_WithoutCapturedPlan_IsConflict()
    {
        using var client = await ResetAsync(captureCurrentPlan: false);

        var response = await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("validation-current-plan-missing", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evaluate_WithAnIncompleteBaseline_IsConflict()
    {
        using var client = await ResetAsync(captureCurrentPlan: true, completeBaseline: false);

        var response = await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("validation-baseline-incomplete", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0, 1.1)]
    [InlineData(1.2, 1.1)]
    public async Task Evaluate_WithInvalidFactors_IsBadRequest(double min, double max)
    {
        using var client = await ResetAsync(captureCurrentPlan: true);

        var response = await client.PostAsJsonAsync("/api/validation/evaluate", new { minEffortFactor = min, maxEffortFactor = max, seed = 1L }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_ReturnsBothPlannings_TheDeviations_AndThePriorityCheck()
    {
        using var client = await ResetAsync(captureCurrentPlan: true);

        var response = await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DayOfWeek.Monday, DateOnly.ParseExact(result.GetProperty("periodStart").GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).DayOfWeek);
        Assert.True(result.GetProperty("efficiency").GetProperty("currentProcess").GetProperty("idleHours").GetDecimal() >= 0m);
        Assert.True(result.GetProperty("efficiency").GetProperty("withCoordin").GetProperty("idleHours").GetDecimal() >= 0m);
        Assert.Equal(30, result.GetProperty("deviation").GetProperty("items").GetArrayLength());
        Assert.Equal(5, result.GetProperty("priorityCheck").GetProperty("items").GetArrayLength());
        Assert.InRange(result.GetProperty("priorityCheck").GetProperty("worsenedCount").GetInt32(), 0, 5);
    }

    [Fact]
    public async Task Evaluate_IsReproducible_AndWritesNothing()
    {
        using var client = await ResetAsync(captureCurrentPlan: true);
        var statusBefore = await client.GetStringAsync("/api/validation/status", Ct);

        var first = await (await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct)).Content.ReadAsStringAsync(Ct);
        var second = await (await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(first, second);
        Assert.Equal(statusBefore, await client.GetStringAsync("/api/validation/status", Ct));
    }

    [Fact]
    public async Task Evaluate_UsesTheCapturedPlan_NotTheCurrentSettings()
    {
        using var client = await ResetAsync(captureCurrentPlan: true);
        var before = await (await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        // A margem muda depois de guardado o processo atual: o processo atual guardado não pode mudar.
        var settings = await client.PutAsJsonAsync("/api/settings", new { safetyMarginPercent = 0m }, Ct);
        Assert.True(settings.IsSuccessStatusCode);
        var after = await (await client.PostAsJsonAsync("/api/validation/evaluate", Parameters, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(
            before.GetProperty("efficiency").GetProperty("currentProcess").GetRawText(),
            after.GetProperty("efficiency").GetProperty("currentProcess").GetRawText());
    }
}
