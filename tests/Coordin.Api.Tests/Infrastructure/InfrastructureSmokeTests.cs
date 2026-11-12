namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Provam que a base de testes de integração funciona (host em memória e Docker).</summary>
public class InfrastructureSmokeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Factory_Starts_AndHostAnswersRequests()
    {
        using var factory = new CoordinApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True((int)response.StatusCode < 500);
    }

    [Fact]
    public async Task PostgresFixture_Starts_AndAcceptsCommands()
    {
        Assert.True(await postgres.CanExecuteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostgresFixture_CreateDatabase_ReturnsConnectionStringForNewDatabase()
    {
        var connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Database=coordin_test_", connectionString, StringComparison.Ordinal);
    }
}
