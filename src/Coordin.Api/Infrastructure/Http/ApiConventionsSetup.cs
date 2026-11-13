using System.Text.Json;
using System.Text.Json.Serialization;

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

            // Enums como texto ("Vacation", "Other"): legível no contrato e estável se um valor for acrescentado.
            // Um valor desconhecido na entrada vira 400, e o OpenAPI descreve os valores permitidos.
            // allowIntegerValues: false, senão "type": 7 entraria como um valor que não existe.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
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
