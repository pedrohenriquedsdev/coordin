using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Coordin.Api.Features.Health;

/// <summary>
/// <c>GET /api/health</c>: confere que a aplicação está no ar e que o banco aceita conexões.
/// É anônimo e a resposta só traz o estado geral: nada de string de conexão, servidor ou
/// mensagem de exceção (DT-30, RNF-05).
/// </summary>
public static class HealthEndpoints
{
    public static IServiceCollection AddCoordinHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<CoordinDbContext>();
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            // 200 para saudável e 503 para não saudável (padrão do ASP.NET Core).
            ResponseWriter = (context, report) =>
            {
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(new HealthResponse(report.Status.ToString()));
            },
        }).AllowAnonymous();

        return app;
    }
}

public sealed record HealthResponse(string Status);
