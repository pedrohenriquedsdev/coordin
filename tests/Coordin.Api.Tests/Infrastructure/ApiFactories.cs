using Microsoft.AspNetCore.Mvc.Testing;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>API no ambiente <c>Testing</c> (com os endpoints de diagnóstico), compartilhada por classe de teste.</summary>
public sealed class TestingApiFactory() : CoordinApiFactory("Testing");

/// <summary>API no ambiente <c>Production</c>, para HSTS, HTTPS e documentação bloqueada.</summary>
public sealed class ProductionApiFactory() : CoordinApiFactory("Production")
{
    /// <summary>Cliente que fala HTTPS, para a requisição não ser redirecionada em produção.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
}
