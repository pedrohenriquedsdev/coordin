using System.Net;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec api-conventions: prefixo /api, JSON em camelCase e rotas de diagnóstico só em Testing.</summary>
public class ApiConventionsTests(TestingApiFactory testing, ProductionApiFactory production)
    : IClassFixture<TestingApiFactory>, IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task Json_UsesCamelCasePropertiesAndIsoDates()
    {
        using var client = testing.CreateClient();

        var raw = await client.GetStringAsync("/api/_test/json", TestContext.Current.CancellationToken);

        Assert.Contains("\"databaseStatus\":\"ok\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"planningDate\":\"2026-11-28\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("DatabaseStatus", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsProblemDetails_NotTheSpa()
    {
        using var client = testing.CreateClient();

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<html", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/_test/json")]
    [InlineData("/api/_test/conflict")]
    [InlineData("/api/_test/error")]
    public async Task TestOnlyEndpoints_AreNotMappedInProduction(string path)
    {
        using var client = production.CreateHttpsClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
