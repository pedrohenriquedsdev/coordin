using Coordin.Api.Infrastructure.Persistence;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Cronograma (RF-11). Não recalcula nada: lê as previsões gravadas (DT-03) pela mesma função que a exportação
/// em planilha usa (<see cref="ScheduleReader"/>).
/// </summary>
public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/schedule").WithTags("Schedule");

        group.MapGet("", GetScheduleAsync)
            .WithName("GetSchedule")
            .WithSummary("Cronograma de todas as demandas na ordem de execução, com previsões e desvios.")
            .Produces<ScheduleResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> GetScheduleAsync(CoordinDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await ScheduleReader.ReadAsync(db, cancellationToken));
}
