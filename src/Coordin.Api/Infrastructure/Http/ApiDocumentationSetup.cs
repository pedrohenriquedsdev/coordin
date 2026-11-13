namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// Documento OpenAPI (<c>/openapi/v1.json</c>) e documentação interativa (<c>/swagger</c>), só em
/// desenvolvimento (DT-19). O documento é o contrato que gera os tipos do Angular; em produção
/// nada disso é exposto.
/// </summary>
public static class ApiDocumentationSetup
{
    public const string DocumentPath = "/openapi/v1.json";

    public static IServiceCollection AddCoordinApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    public static WebApplication UseCoordinApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        // Só em desenvolvimento e sem dados do planejamento: precisa ser anônimo para o Swagger UI
        // e para o gerador do cliente do Angular (DT-19) lerem o contrato.
        app.MapOpenApi(DocumentPath).AllowAnonymous();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentPath, "Coordin API v1"));

        return app;
    }
}
