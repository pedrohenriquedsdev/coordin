using Coordin.Api.Infrastructure.Persistence;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Cronograma (RF-11) e a exportação em planilha (RF-19). Não recalcula nada: lê as previsões gravadas (DT-03) pela
/// mesma função (<see cref="ScheduleReader"/>), e a exportação só formata o que o cronograma mostra.
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

        group.MapGet("/export", ExportScheduleAsync)
            .WithName("ExportSchedule")
            .WithSummary("Cronograma vigente em CSV pt-BR (separador ';', UTF-8 com BOM), para abrir em planilha (RF-19).")
            .Produces<byte[]>(StatusCodes.Status200OK, "text/csv");

        return app;
    }

    private static async Task<IResult> GetScheduleAsync(CoordinDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await ScheduleReader.ReadAsync(db, cancellationToken));

    /// <summary>
    /// Entrega o cronograma vigente como arquivo. O arquivo é gerado uma vez a partir da leitura, então ele é
    /// consistente consigo mesmo; o nome usa a data do dia da exportação em Brasília (DT-20).
    /// </summary>
    private static async Task<IResult> ExportScheduleAsync(
        CoordinDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var schedule = await ScheduleReader.ReadAsync(db, cancellationToken);
        var bytes = ScheduleCsv.ToBytes(ScheduleCsv.Build(schedule));
        return Results.File(bytes, "text/csv; charset=utf-8", ScheduleExportName.For(clock.GetUtcNow()));
    }
}
