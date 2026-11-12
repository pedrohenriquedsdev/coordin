namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// Cabeçalhos de segurança em todas as respostas (DT-30, D7): CSP restritiva como segunda
/// barreira contra XSS, <c>nosniff</c> e <c>Referrer-Policy: no-referrer</c>.
/// </summary>
public static class SecurityHeadersSetup
{
    // Sem scripts inline. Estilos inline ficam liberados porque o Angular injeta <style> dos
    // componentes em tempo de execução; isso não permite executar código (D7).
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    // A Swagger UI usa script inline; só ela, e só em desenvolvimento, recebe a CSP relaxada.
    private const string SwaggerContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'";

    public static WebApplication UseCoordinSecurityHeaders(this WebApplication app)
    {
        var isDevelopment = app.Environment.IsDevelopment();

        app.Use(async (context, next) =>
        {
            // OnStarting (e não escrever antes do next) porque o handler de exceções limpa os
            // cabeçalhos da resposta; assim eles também valem para os erros 500.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy =
                    isDevelopment && context.Request.Path.StartsWithSegments("/swagger")
                        ? SwaggerContentSecurityPolicy
                        : ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });

            await next();
        });

        return app;
    }
}
