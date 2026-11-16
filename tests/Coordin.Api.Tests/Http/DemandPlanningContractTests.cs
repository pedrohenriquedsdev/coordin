using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Contrato do planejamento (demanda, cronograma e configurações): OpenAPI e login em todas as rotas (DT-19; RNF-04).</summary>
public class DemandPlanningContractTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    private static readonly Dictionary<string, string> ExpectedOperations = new()
    {
        ["GetDemands"] = "Demands",
        ["CreateDemand"] = "Demands",
        ["GetDemand"] = "Demands",
        ["UpdateDemand"] = "Demands",
        ["DeleteDemand"] = "Demands",
        ["ReorderDemands"] = "Demands",
        ["CompleteDemand"] = "Demands",
        ["GetSchedule"] = "Schedule",
        ["GetSettings"] = "Settings",
        ["UpdateSettings"] = "Settings",
    };

    [Fact]
    public async Task TheOpenApiDocument_ListsTheTenNewOperations_EachWithItsTag_WithoutLogin()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var found = new Dictionary<string, string>();
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var id = operation.Value.GetProperty("operationId").GetString()!;
                if (ExpectedOperations.ContainsKey(id))
                {
                    found[id] = operation.Value.GetProperty("tags")[0].GetString()!;
                }
            }
        }

        Assert.Equal(ExpectedOperations, found);
    }

    [Theory]
    [InlineData("GET", "/api/demands")]
    [InlineData("POST", "/api/demands")]
    [InlineData("GET", "/api/demands/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/demands/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/demands/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/demands/priority-order")]
    [InlineData("PUT", "/api/demands/00000000-0000-0000-0000-000000000001/completion")]
    [InlineData("GET", "/api/schedule")]
    [InlineData("GET", "/api/settings")]
    [InlineData("PUT", "/api/settings")]
    public async Task EveryNewRoute_RespondsUnauthorized_WithoutAToken(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            // Os clientes sempre enviam corpo; sem ele o roteamento não escolhe a rota de escrita e responde 404.
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
