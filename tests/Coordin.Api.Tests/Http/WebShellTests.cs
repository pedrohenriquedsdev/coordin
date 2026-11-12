using System.Net;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec web-shell: a SPA é servida pela própria API, na mesma origem (DT-01).</summary>
public class WebShellTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Root_ReturnsTheSpaIndexHtml()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>Coordin</title>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexHtml_IsNotCached_SoANewDeployIsPickedUp()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/main.js", "text/javascript")]
    [InlineData("/styles.css", "text/css")]
    public async Task BuildFiles_AreServedAsStaticFiles(string path, string contentType)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/cronograma")]
    [InlineData("/demandas/123/editar")]
    public async Task SpaRoute_ReturnsIndexHtml_SoReloadingAndDirectLinksWork(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Coordin</title>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownApiRoute_IsNotTheSpa()
    {
        var response = await _client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingFileWithExtension_Returns404_InsteadOfTheSpa()
    {
        var response = await _client.GetAsync("/nao-existe.js", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
