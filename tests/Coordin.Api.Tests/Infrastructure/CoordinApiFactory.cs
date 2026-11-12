using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória para os testes de integração, com um banco PostgreSQL próprio.
/// Permite escolher o ambiente (<c>Testing</c> por padrão; <c>Production</c> nos testes de HSTS e
/// de documentação) e, se preciso, uma string de conexão específica. Serve a SPA a partir de uma
/// pasta temporária com um <c>index.html</c> falso, para os testes não dependerem do build do
/// Angular (D13).
/// </summary>
public class CoordinApiFactory(string environment, PostgresFixture? postgres = null, string? connectionString = null)
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _webRoot = CreateWebRoot();
    private string? _connectionString = connectionString;

    /// <summary>Nome do banco criado para esta factory (nulo quando se passou uma string de conexão).</summary>
    public string? DatabaseName { get; private set; }

    /// <summary>Cria o banco desta factory. Chamado pelo xUnit nas fixtures; chame à mão nos demais casos.</summary>
    public async ValueTask InitializeAsync()
    {
        if (_connectionString is null && postgres is not null)
        {
            var database = await postgres.CreateDatabaseAsync();
            DatabaseName = database.Name;
            _connectionString = database.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseWebRoot(_webRoot);

        // A fonte em memória entra por último, então vence os user-secrets da máquina de quem
        // roda os testes (em Development) e qualquer outra configuração.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            if (_connectionString is not null)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _connectionString,
                });
            }
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteWebRoot();
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            DeleteWebRoot();
        }
    }

    private static string CreateWebRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"coordin-web-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, "index.html"),
            "<!doctype html><html lang=\"pt-BR\"><head><title>Coordin</title></head><body>Coordin</body></html>");
        return path;
    }

    private void DeleteWebRoot()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
