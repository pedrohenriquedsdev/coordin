using Testcontainers.PostgreSql;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Um banco de testes recém-criado dentro do container.</summary>
public sealed record TestDatabase(string Name, string ConnectionString);

/// <summary>
/// Um único PostgreSQL de verdade (mesma versão principal da produção, DT-31) compartilhado por
/// todo o assembly de testes (<c>AssemblyFixture</c>). Cada factory cria o seu próprio banco
/// dentro dele, para os testes não interferirem entre si (TDD 8.3).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Cria um banco vazio com nome único.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = $"coordin_test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE {name};", cancellationToken);

        var connectionString = _container.GetConnectionString()
            .Replace("Database=postgres", $"Database={name}", StringComparison.Ordinal);
        return new TestDatabase(name, connectionString);
    }

    /// <summary>Apaga o banco (derrubando as conexões abertas), para simular um banco indisponível.</summary>
    public Task DropDatabaseAsync(string name, CancellationToken cancellationToken = default) =>
        ExecuteAsync($"DROP DATABASE {name} WITH (FORCE);", cancellationToken);

    /// <summary>Executa um comando SQL no servidor de testes.</summary>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var result = await _container.ExecScriptAsync(sql, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Falha ao executar '{sql}' no PostgreSQL de testes: {result.Stderr}");
        }
    }
}
