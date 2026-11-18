using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Configurações do planejamento (RF-09). Nenhum endpoint declara regra de autorização: a política
/// padrão exige o gestor autenticado.
/// </summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("", GetSettingsAsync)
            .WithName("GetSettings")
            .WithSummary("Lista as configurações do planejamento: margem de segurança e modo de ordem.")
            .Produces<SettingsResponse>(StatusCodes.Status200OK);

        group.MapPut("", UpdateSettingsAsync)
            .WithName("UpdateSettings")
            .WithSummary("Altera a margem de segurança e recalcula as previsões. O modo de ordem não muda aqui.")
            .Produces<SettingsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSettingsAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.PlanningSettings
            .AsNoTracking()
            .SingleAsync(item => item.Id == PlanningSettings.SingletonId, cancellationToken);

        return Results.Ok(ToResponse(settings));
    }

    private static Task<IResult> UpdateSettingsAsync(
        SettingsRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.SafetyMargin, async () =>
        {
            var settings = await db.PlanningSettings
                .SingleAsync(item => item.Id == PlanningSettings.SingletonId, cancellationToken);

            // Só a margem muda: o modo de ordem, mesmo que venha no corpo, fica como está.
            settings.SafetyMarginPercent = request.SafetyMarginPercent!.Value;
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(settings));
        }, cancellationToken);

    private static SettingsResponse ToResponse(PlanningSettings settings) =>
        new(settings.SafetyMarginPercent, settings.ExecutionOrderMode);
}
