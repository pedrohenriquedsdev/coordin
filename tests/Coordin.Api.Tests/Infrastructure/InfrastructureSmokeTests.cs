namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Provam que a base de testes de integração funciona (host em memória e Docker).</summary>
public class InfrastructureSmokeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Factory_Starts_AndHostAnswersRequests()
    {
        await using var factory = new CoordinApiFactory("Testing", postgres);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True((int)response.StatusCode < 500);
    }

    [Fact]
    public async Task PostgresFixture_Starts_AndAcceptsCommands()
    {
        await postgres.ExecuteAsync("SELECT 1;", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PostgresFixture_CreateDatabase_ReturnsConnectionStringForNewDatabase()
    {
        var database = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"Database={database.Name}", database.ConnectionString, StringComparison.Ordinal);
    }
}
