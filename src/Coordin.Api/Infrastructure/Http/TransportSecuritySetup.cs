using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// HTTPS em produção (DT-30): a hospedagem termina o TLS (DT-09) e encaminha a requisição como
/// HTTP com <c>X-Forwarded-Proto</c>; a aplicação reconhece o esquema original, redireciona
/// HTTP para HTTPS e envia HSTS.
/// </summary>
public static class TransportSecuritySetup
{
    public static IServiceCollection AddCoordinTransportSecurity(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // O IP do proxy da PaaS não é fixo, então não há lista de proxies confiáveis.
            // O esquema e o IP só servem ao redirecionamento HTTPS e aos logs.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Atrás do proxy a porta HTTPS pública é a 443.
        services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);

        return services;
    }

    public static WebApplication UseCoordinTransportSecurity(this WebApplication app)
    {
        app.UseForwardedHeaders();

        if (app.Environment.IsProduction())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        return app;
    }
}
