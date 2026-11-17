using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Contrato dos cenários (TDD 5.2, 5.3): OpenAPI com a tag e os campos, e login em todas as rotas (RNF-04).</summary>
public class ScenarioContractTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    private static readonly string[] ExpectedOperations =
    [
        "GetScenarioRecommendations",
        "SimulateScenario",
        "ApplyScenario",
        "ResetToPriority",
    ];

    [Fact]
    public async Task TheOpenApiDocument_ListsTheScenarioOperations_WithTheirTag_AndTheResponseFields()
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
                if (ExpectedOperations.Contains(id))
                {
                    found[id] = operation.Value.GetProperty("tags")[0].GetString()!;
                }
            }
        }
        Assert.Equal(ExpectedOperations.Length, found.Count);
        Assert.All(found.Values, tag => Assert.Equal("Scenarios", tag));

        var schemas = document.GetProperty("components").GetProperty("schemas");
        var scenario = schemas.GetProperty("ScenarioResponse").GetProperty("properties");
        foreach (var field in new[] { "executionOrder", "items", "score", "sectorEndDate", "sectorDeltaDays", "isComparable" })
        {
            Assert.True(scenario.TryGetProperty(field, out _), $"Falta o campo {field} em ScenarioResponse.");
        }
    }

    [Fact]
    public async Task TheNewRoutes_RespondUnauthorized_WithoutAToken()
    {
        using var client = factory.CreateClient();
        var routes = new[]
        {
            ("POST", "/api/scenarios/recommendations"),
            ("POST", "/api/scenarios/simulate"),
            ("POST", "/api/scenarios/apply"),
            ("POST", "/api/scenarios/reset-to-priority"),
        };

        foreach (var (method, path) in routes)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                // Os clientes sempre enviam corpo; sem ele a rota de escrita responde 404, não 401.
                Content = JsonContent.Create(new { }),
            };

            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path} respondeu {(int)response.StatusCode} sem token.");
        }
    }
}
