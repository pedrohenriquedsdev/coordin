using System.Net;
using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Health;

/// <summary>Spec service-health: verificação pública, sem vazar detalhes internos (DT-30, RNF-05).</summary>
public class HealthEndpointTests(TestingApiFactory testing, PostgresFixture postgres) : IClassFixture<TestingApiFactory>
{
    [Fact]
    public async Task HealthyApplicationAndDatabase_Returns200Healthy_WithoutCredentials()
    {
        using var client = testing.CreateClient(); // sem Authorization

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("""{"status":"Healthy"}""", raw);
    }

    [Fact]
    public async Task UnavailableDatabase_Returns503Unhealthy()
    {
        await using var factory = await StartWithDatabaseThenDropItAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            """{"status":"Unhealthy"}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failure_DoesNotLeakConnectionOrExceptionDetails()
    {
        await using var factory = await StartWithDatabaseThenDropItAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(factory.DatabaseName!, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Host", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sobe a aplicação com um banco saudável e depois apaga o banco, simulando uma queda.</summary>
    private async Task<CoordinApiFactory> StartWithDatabaseThenDropItAsync()
    {
        var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();

        using var client = factory.CreateClient();
        var warmUp = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, warmUp.StatusCode);

        await postgres.DropDatabaseAsync(factory.DatabaseName!, TestContext.Current.CancellationToken);
        return factory;
    }
}
