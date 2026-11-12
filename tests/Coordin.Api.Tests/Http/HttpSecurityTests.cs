using System.Net;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec http-security: cabeçalhos, HTTPS em produção, origem única e sem CORS (DT-30).</summary>
public class HttpSecurityTests(
    TestingApiFactory testing,
    DevelopmentApiFactory development,
    ProductionApiFactory production)
    : IClassFixture<TestingApiFactory>, IClassFixture<DevelopmentApiFactory>, IClassFixture<ProductionApiFactory>
{
    private const string Hsts = "Strict-Transport-Security";

    [Theory]
    [InlineData("/api/rota-inexistente")] // API: 404 em Problem Details
    [InlineData("/api/_test/conflict")] // erro de negócio: 409
    [InlineData("/api/_test/error")] // exceção: 500 (o handler limpa os cabeçalhos)
    public async Task Responses_CarrySecurityHeaders(string path)
    {
        using var client = testing.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Csp_NeverAllowsInlineScripts_OutsideSwagger()
    {
        using var client = testing.CreateClient();

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        var scriptSrc = Directive(response, "script-src");
        Assert.Equal("script-src 'self'", scriptSrc);
    }

    [Fact]
    public async Task Csp_AllowsInlineScripts_OnlyOnSwaggerInDevelopment()
    {
        using var client = development.CreateClient();

        var swagger = await client.GetAsync("/swagger", TestContext.Current.CancellationToken);
        var api = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.Contains("'unsafe-inline'", Directive(swagger, "script-src"), StringComparison.Ordinal);
        Assert.Equal("script-src 'self'", Directive(api, "script-src"));
    }

    [Fact]
    public async Task Production_Https_SendsHsts()
    {
        using var client = production.CreateHttpsClient();

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.Contains(Hsts));
    }

    [Fact]
    public async Task Production_RequestForwardedAsHttps_IsNotRedirected()
    {
        using var client = production.CreateHttpClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains(Hsts));
    }

    [Fact]
    public async Task Production_PlainHttp_RedirectsToHttps()
    {
        using var client = production.CreateHttpClient();

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https://coordin.example/api/rota-inexistente", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Development_DoesNotSendHsts()
    {
        using var client = development.CreateHttpsClient();

        var response = await client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains(Hsts));
    }

    [Fact]
    public async Task RequestFromAnotherOrigin_DoesNotGetCorsHeaders()
    {
        using var client = testing.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/_test/json");
        request.Headers.Add("Origin", "https://outro-site.example");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    private static string Directive(HttpResponseMessage response, string name)
    {
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        return csp.Split(';', StringSplitOptions.TrimEntries).Single(d => d.StartsWith(name, StringComparison.Ordinal));
    }
}
