using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Contrato da exportação do cronograma: OpenAPI com a operação, a tag e o tipo CSV, e login obrigatório (RNF-04).</summary>
public class ScheduleExportContractTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    [Fact]
    public async Task TheOpenApiDocument_ListsExportSchedule_AsCsv_UnderTheScheduleTag()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var found = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Single(operation => operation.Value.GetProperty("operationId").GetString() == "ExportSchedule");
        Assert.Equal("Schedule", found.Value.GetProperty("tags")[0].GetString());
        Assert.True(found.Value.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("text/csv", out _));
    }

    [Fact]
    public async Task TheExportRoute_RespondsUnauthorized_WithoutAToken()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/schedule/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
