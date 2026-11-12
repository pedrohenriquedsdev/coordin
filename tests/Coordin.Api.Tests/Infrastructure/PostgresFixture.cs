using Testcontainers.PostgreSql;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// Um único PostgreSQL de verdade (mesma versão principal da produção, DT-31) compartilhado
/// por todas as classes de teste que precisam de banco. Cada classe cria o seu próprio banco
/// dentro dele, para os testes não interferirem entre si (TDD 8.3).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Cria um banco vazio com nome único e devolve a string de conexão dele.</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = $"coordin_test_{Guid.NewGuid():N}";
        var result = await _container.ExecScriptAsync($"CREATE DATABASE {name};", cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Não foi possível criar o banco de testes: {result.Stderr}");
        }

        return _container.GetConnectionString().Replace("Database=postgres", $"Database={name}", StringComparison.Ordinal);
    }

    /// <summary>Executa um comando SQL no servidor de testes (usado para provar que o Docker responde).</summary>
    public async Task<bool> CanExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = await _container.ExecScriptAsync("SELECT 1;", cancellationToken);
        return result.ExitCode == 0;
    }
}
