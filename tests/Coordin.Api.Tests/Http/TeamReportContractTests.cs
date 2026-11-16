using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Contrato do relatório por time: OpenAPI com a operação e a tag, e login obrigatório (DT-19; RNF-04).</summary>
public class TeamReportContractTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    [Fact]
    public async Task TheOpenApiDocument_ListsGetTeamReport_UnderTheReportsTag_WithoutLogin()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var found = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Single(operation => operation.Value.GetProperty("operationId").GetString() == "GetTeamReport");
        Assert.Equal("Reports", found.Value.GetProperty("tags")[0].GetString());
    }

    [Fact]
    public async Task TheReportRoute_RespondsUnauthorized_WithoutAToken()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/teams?from=2027-01-04&to=2027-01-10", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
