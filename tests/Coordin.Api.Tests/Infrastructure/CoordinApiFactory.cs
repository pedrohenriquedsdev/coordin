using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// Sobe a API inteira em memória para os testes de integração. Permite escolher o ambiente
/// (padrão <c>Testing</c>; <c>Production</c> nos testes de HSTS e de documentação) e a string
/// de conexão do banco. Serve a SPA a partir de uma pasta temporária com um <c>index.html</c>
/// falso, para os testes não dependerem do build do Angular (D13).
/// </summary>
public class CoordinApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly string? _connectionString;
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"coordin-web-{Guid.NewGuid():N}");

    public CoordinApiFactory(string environment = "Testing", string? connectionString = null)
    {
        _environment = environment;
        _connectionString = connectionString;

        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            "<!doctype html><html lang=\"pt-BR\"><head><title>Coordin</title></head><body>Coordin</body></html>");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseWebRoot(_webRoot);

        if (_connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Default", _connectionString);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
