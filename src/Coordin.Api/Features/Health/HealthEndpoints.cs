using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

    // Um endpoint comum de Minimal API (e não MapHealthChecks) para constar no documento OpenAPI,
    // de onde sai o cliente tipado do Angular (DT-19).
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
            {
                var report = await healthChecks.CheckHealthAsync(cancellationToken);
                var response = new HealthResponse(report.Status.ToString());

                return report.Status == HealthStatus.Unhealthy
                    ? Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok(response);
            })
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Verifica se a aplicação e o banco estão no ar.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();

        return app;
    }
}

/// <summary>Estado geral da aplicação: <c>Healthy</c>, <c>Degraded</c> ou <c>Unhealthy</c>.</summary>
public sealed record HealthResponse(string Status);
