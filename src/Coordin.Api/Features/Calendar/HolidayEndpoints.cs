using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Calendar;

/// <summary>Feriados do setor (RF-03): em um feriado ninguém contribui com capacidade. A data é única.</summary>
public static class HolidayEndpoints
{
    private const string DateTaken = "Essa data já é feriado.";

    public static IEndpointRouteBuilder MapHolidayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/holidays").WithTags("Holidays");

        group.MapGet("", GetHolidaysAsync)
            .WithName("GetHolidays")
            .WithSummary("Lista os feriados, por data.")
            .Produces<IReadOnlyList<HolidayResponse>>(StatusCodes.Status200OK);

        group.MapPost("", CreateHolidayAsync)
            .WithName("CreateHoliday")
            .WithSummary("Cadastra um feriado. A data é única; a descrição é opcional.")
            .Produces<HolidayResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteHolidayAsync)
            .WithName("DeleteHoliday")
            .WithSummary("Remove um feriado.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetHolidaysAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var holidays = await db.Holidays
            .AsNoTracking()
            .OrderBy(holiday => holiday.Date) // data não depende de collation
            .Select(holiday => new HolidayResponse(holiday.Id, holiday.Date, holiday.Description))
            .ToListAsync(cancellationToken);

        return Results.Ok(holidays);
    }

    private static async Task<IResult> CreateHolidayAsync(
        HolidayRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var holiday = new Holiday { Date = request.Date!.Value, Description = description };
        db.Holidays.Add(holiday);

        try
        {
            // O índice único da data decide, inclusive em corrida; sem "já existe?" antes.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            return ApiProblems.Conflict("holiday-date-taken", DateTaken);
        }

        return Results.Created($"/api/holidays/{holiday.Id}", new HolidayResponse(holiday.Id, holiday.Date, holiday.Description));
    }

    private static async Task<IResult> DeleteHolidayAsync(Guid id, CoordinDbContext db, CancellationToken cancellationToken)
    {
        var holiday = await db.Holidays.FindAsync([id], cancellationToken);
        if (holiday is null)
        {
            return Results.NotFound();
        }

        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
