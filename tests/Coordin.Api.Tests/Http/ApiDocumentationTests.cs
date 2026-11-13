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

    [Theory]
    [InlineData("/api/auth/login", "post", "Login", new[] { "200", "400", "401", "429" })]
    [InlineData("/api/auth/refresh", "post", "Refresh", new[] { "200", "401", "429" })]
    [InlineData("/api/auth/logout", "post", "Logout", new[] { "204" })]
    [InlineData("/api/auth/me", "get", "GetCurrentUser", new[] { "200", "401" })]
    public async Task Development_OpenApiDocument_DescribesTheAuthenticationEndpoints_WithoutRequiringLogin(
        string path,
        string method,
        string operationId,
        string[] responses)
    {
        // Sem Authorization: o Swagger UI e o gerador do cliente do Angular leem o contrato anonimamente.
        using var client = development.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        Assert.Equal("Auth", Assert.Single(operation.GetProperty("tags").EnumerateArray()).GetString());
        var documented = operation.GetProperty("responses").EnumerateObject().Select(property => property.Name);
        Assert.Equivalent(responses, documented.ToArray());
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
