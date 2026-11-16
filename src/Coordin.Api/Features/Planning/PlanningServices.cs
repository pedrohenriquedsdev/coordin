using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coordin.Api.Features.Planning;

/// <summary>Registro dos serviços do planejamento (recálculo e leitura do snapshot).</summary>
public static class PlanningServices
{
    public static IServiceCollection AddCoordinPlanning(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<PlanningSnapshotLoader>();
        services.AddScoped<ForecastRecalculator>();
        return services;
    }
}
