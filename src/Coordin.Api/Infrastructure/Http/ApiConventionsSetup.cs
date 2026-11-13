using System.Text.Json;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>Convenções comuns a todos os endpoints: JSON em camelCase e prefixo /api (DT-16, TDD 5.1).</summary>
public static class ApiConventionsSetup
{
    public static IServiceCollection AddCoordinJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        });
        return services;
    }

    /// <summary>
    /// Qualquer rota sob <c>/api</c> que nenhum endpoint atenda responde 404 em Problem Details.
    /// Essa rota é mais específica que a da SPA, então uma rota de API inexistente nunca devolve o
    /// <c>index.html</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapApiFallback(this IEndpointRouteBuilder app)
    {
        // Anônimo de propósito: uma rota inexistente responde 404 para qualquer um, e não 401.
        app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
        return app;
    }
}
