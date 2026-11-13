using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Cria times e pessoas pela API nos testes dos cadastros, com nomes únicos.</summary>
public static class RegistryTestData
{
    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    public static async Task<Guid> NewTeamAsync(this HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/teams", new { name = name ?? Unique("Time") }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> NewPersonAsync(this HttpClient client, Guid teamId, string? name = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/people",
            new { name = name ?? Unique("Pessoa"), teamId, dailyCapacityHours = 8 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> PostAbsenceAsync(this HttpClient client, Guid personId, object body) =>
        client.PostAsJsonAsync($"/api/people/{personId}/absences", body, TestContext.Current.CancellationToken);

    public static async Task<Guid> NewAbsenceAsync(
        this HttpClient client,
        Guid personId,
        string type = "Vacation",
        string start = "2027-01-04",
        string end = "2027-01-15")
    {
        var response = await client.PostAbsenceAsync(personId, new { type, startDate = start, endDate = end });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync()).GetProperty("id").GetGuid();
    }
}
