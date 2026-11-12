using System.Net;
using System.Text.Json;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec api-conventions: documentação da API só em desenvolvimento (DT-19).</summary>
public class ApiDocumentationTests(DevelopmentApiFactory development, ProductionApiFactory production)
    : IClassFixture<DevelopmentApiFactory>, IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task Development_OpenApiDocument_IncludesTheHealthEndpoint()
    {
        using var client = development.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.RootElement.TryGetProperty("openapi", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/health", out _));
    }

    [Fact]
    public async Task Development_SwaggerUi_IsServed()
    {
        using var client = development.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("swagger", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task Production_DoesNotExposeApiDocumentation(string path)
    {
        using var client = production.CreateHttpsClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Não pode haver documento OpenAPI nem Swagger UI (mais tarde, a rota da SPA pode responder).
        Assert.DoesNotContain("\"openapi\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"paths\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("swagger-ui", body, StringComparison.OrdinalIgnoreCase);
    }
}
