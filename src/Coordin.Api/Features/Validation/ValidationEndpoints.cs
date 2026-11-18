using Coordin.Api.Features.Demands;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Features.Validation;

/// <summary>
/// A validação por simulação (RF-24; DT-37). Só existe com <c>Validation:Enabled</c> verdadeiro: com a área
/// desligada, as rotas não são mapeadas, e o caminho responde <c>404</c>, sem nem aparecer na documentação.
/// </summary>
public static class ValidationEndpoints
{
    public static IServiceCollection AddCoordinValidation(this IServiceCollection services)
    {
        services.AddOptions<ValidationOptions>()
            .BindConfiguration(ValidationOptions.SectionName);
        return services;
    }

    public static IEndpointRouteBuilder MapValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ValidationOptions>>().Value;
        if (!options.Enabled)
        {
            return app;
        }

        var group = app.MapGroup("/api/validation").WithTags("Validation");

        group.MapGet("/status", GetStatusAsync)
            .WithName("GetValidationStatus")
            .WithSummary("Estado da validação: demandas, linhas de base e planejamento do processo atual guardado (RF-24).")
            .Produces<ValidationStatusResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> GetStatusAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var demandCount = await db.Demands.AsNoTracking().CountAsync(cancellationToken);
        var baselineCount = await db.ValidationBaselines.AsNoTracking().CountAsync(cancellationToken);
        var plan = await db.ValidationCurrentPlans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == ValidationCurrentPlan.SingletonId, cancellationToken);

        return Results.Ok(new ValidationStatusResponse(
            demandCount,
            baselineCount,
            plan is not null,
            plan?.CapturedAt));
    }
}
