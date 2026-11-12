using Microsoft.AspNetCore.Mvc.Testing;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>API no ambiente <c>Testing</c> (com os endpoints de diagnóstico), compartilhada por classe de teste.</summary>
public sealed class TestingApiFactory(PostgresFixture postgres) : CoordinApiFactory("Testing", postgres);

/// <summary>API no ambiente <c>Development</c>, para a CSP da Swagger UI e a ausência de HSTS.</summary>
public sealed class DevelopmentApiFactory(PostgresFixture postgres) : CoordinApiFactory("Development", postgres)
{
    /// <summary>Cliente HTTPS com host público (o HSTS ignora <c>localhost</c> por padrão).</summary>
    public HttpClient CreateHttpsClient() => CreateClient(Options("https"));

    private static WebApplicationFactoryClientOptions Options(string scheme) =>
        new() { BaseAddress = new Uri($"{scheme}://coordin.example"), AllowAutoRedirect = false };
}

/// <summary>API no ambiente <c>Production</c>, para HSTS, HTTPS e documentação bloqueada.</summary>
public sealed class ProductionApiFactory(PostgresFixture postgres) : CoordinApiFactory("Production", postgres)
{
    /// <summary>Cliente que fala HTTPS, para a requisição não ser redirecionada em produção.</summary>
    public HttpClient CreateHttpsClient() => CreateClient(Options("https"));

    /// <summary>Cliente que fala HTTP puro, para provar o redirecionamento para HTTPS.</summary>
    public HttpClient CreateHttpClient() => CreateClient(Options("http"));

    private static WebApplicationFactoryClientOptions Options(string scheme) =>
        new() { BaseAddress = new Uri($"{scheme}://coordin.example"), AllowAutoRedirect = false };
}
