using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Contrato dos cadastros de capacidade: OpenAPI e proteção por login de todas as rotas (design D6; RNF-04).</summary>
public class RegistryContractTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    private static readonly Dictionary<string, string> ExpectedOperations = new()
    {
        ["GetTeams"] = "Teams",
        ["CreateTeam"] = "Teams",
        ["UpdateTeam"] = "Teams",
        ["DeleteTeam"] = "Teams",
        ["GetPeople"] = "People",
        ["CreatePerson"] = "People",
        ["UpdatePerson"] = "People",
        ["DeletePerson"] = "People",
        ["GetPersonAbsences"] = "Absences",
        ["GetAbsences"] = "Absences",
        ["CreateAbsence"] = "Absences",
        ["UpdateAbsence"] = "Absences",
        ["DeleteAbsence"] = "Absences",
        ["GetHolidays"] = "Holidays",
        ["CreateHoliday"] = "Holidays",
        ["DeleteHoliday"] = "Holidays",
    };

    [Fact]
    public async Task TheOpenApiDocument_ListsTheSixteenOperations_EachWithItsResourceTag_WithoutLogin()
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

    [Fact]
    public async Task TheAbsenceType_IsDescribedAsTextValues()
    {
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        var schema = document.GetProperty("components").GetProperty("schemas").GetProperty("AbsenceType");

        Assert.Equal(["Vacation", "Other"], schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToList());
    }

    [Theory]
    [InlineData("GET", "/api/teams")]
    [InlineData("POST", "/api/teams")]
    [InlineData("PUT", "/api/teams/{id}")]
    [InlineData("DELETE", "/api/teams/{id}")]
    [InlineData("GET", "/api/people")]
    [InlineData("POST", "/api/people")]
    [InlineData("PUT", "/api/people/{id}")]
    [InlineData("DELETE", "/api/people/{id}")]
    [InlineData("GET", "/api/people/{id}/absences")]
    [InlineData("POST", "/api/people/{id}/absences")]
    [InlineData("GET", "/api/absences")]
    [InlineData("PUT", "/api/absences/{id}")]
    [InlineData("DELETE", "/api/absences/{id}")]
    [InlineData("GET", "/api/holidays")]
    [InlineData("POST", "/api/holidays")]
    [InlineData("DELETE", "/api/holidays/{id}")]
    public async Task EveryNewRoute_RequiresALogin(string method, string template)
    {
        using var client = factory.CreateClient();
        var url = template.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
