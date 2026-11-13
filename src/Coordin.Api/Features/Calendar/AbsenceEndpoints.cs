using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Calendar;

/// <summary>
/// Ausências das pessoas (RF-03): durante o período a pessoa não contribui com capacidade. Aqui só se
/// guardam os dados; o recálculo das previsões chega com o motor e as demandas (changes 4 e 5).
/// </summary>
public static class AbsenceEndpoints
{
    public static IEndpointRouteBuilder MapAbsenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/people/{personId:guid}/absences", GetPersonAbsencesAsync)
            .WithName("GetPersonAbsences").WithTags("Absences")
            .WithSummary("Lista as ausências de uma pessoa, por data de início.")
            .Produces<IReadOnlyList<AbsenceResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/people/{personId:guid}/absences", CreateAbsenceAsync)
            .WithName("CreateAbsence").WithTags("Absences")
            .WithSummary("Registra uma ausência (férias ou outra) de uma pessoa: só tipo e período, nunca o motivo.")
            .Produces<AbsenceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/absences", GetAbsencesAsync)
            .WithName("GetAbsences").WithTags("Absences")
            .WithSummary("Lista as ausências de todas as pessoas, por data de início e depois por nome.")
            .Produces<IReadOnlyList<AbsenceResponse>>(StatusCodes.Status200OK);

        app.MapPut("/api/absences/{id:guid}", UpdateAbsenceAsync)
            .WithName("UpdateAbsence").WithTags("Absences")
            .WithSummary("Altera o tipo e o período de uma ausência.")
            .Produces<AbsenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/api/absences/{id:guid}", DeleteAbsenceAsync)
            .WithName("DeleteAbsence").WithTags("Absences")
            .WithSummary("Exclui uma ausência.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static IQueryable<AbsenceResponse> Project(IQueryable<Absence> absences) =>
        absences.Select(absence => new AbsenceResponse(
            absence.Id,
            absence.PersonId,
            absence.Person!.Name,
            absence.Person.TeamId,
            absence.Person.Team!.Name,
            absence.Type,
            absence.StartDate,
            absence.EndDate));

    // Ordenado em memória, e não no SQL: a collation do servidor varia (como nas listas de times e pessoas).
    private static List<AbsenceResponse> Order(IEnumerable<AbsenceResponse> absences) =>
        [.. absences
            .OrderBy(absence => absence.StartDate)
            .ThenBy(absence => absence.PersonName.ToUpperInvariant(), StringComparer.Ordinal)];

    private static async Task<IResult> GetAbsencesAsync(CoordinDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(Order(await Project(db.Absences.AsNoTracking()).ToListAsync(cancellationToken)));

    private static async Task<IResult> GetPersonAbsencesAsync(
        Guid personId,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.People.AnyAsync(person => person.Id == personId, cancellationToken))
        {
            return Results.NotFound();
        }

        var absences = await Project(db.Absences.AsNoTracking().Where(absence => absence.PersonId == personId))
            .ToListAsync(cancellationToken);

        return Results.Ok(Order(absences));
    }

    private static async Task<IResult> CreateAbsenceAsync(
        Guid personId,
        AbsenceRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var person = await db.People.Include(item => item.Team).SingleOrDefaultAsync(item => item.Id == personId, cancellationToken);
        if (person is null)
        {
            return Results.NotFound();
        }

        var absence = new Absence
        {
            PersonId = person.Id,
            Type = request.Type!.Value,
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate!.Value,
        };

        db.Absences.Add(absence);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/absences/{absence.Id}", ToResponse(absence, person));
    }

    private static async Task<IResult> UpdateAbsenceAsync(
        Guid id,
        AbsenceRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var absence = await db.Absences
            .Include(item => item.Person!).ThenInclude(person => person.Team)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (absence is null)
        {
            return Results.NotFound();
        }

        absence.Type = request.Type!.Value;
        absence.StartDate = request.StartDate!.Value;
        absence.EndDate = request.EndDate!.Value;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(absence, absence.Person!));
    }

    private static async Task<IResult> DeleteAbsenceAsync(Guid id, CoordinDbContext db, CancellationToken cancellationToken)
    {
        var absence = await db.Absences.FindAsync([id], cancellationToken);
        if (absence is null)
        {
            return Results.NotFound();
        }

        db.Absences.Remove(absence);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static AbsenceResponse ToResponse(Absence absence, People.Person person) =>
        new(
            absence.Id,
            person.Id,
            person.Name,
            person.TeamId,
            person.Team!.Name,
            absence.Type,
            absence.StartDate,
            absence.EndDate);
}
