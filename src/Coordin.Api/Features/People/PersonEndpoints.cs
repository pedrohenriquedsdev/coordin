using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.People;

/// <summary>
/// Cadastro de pessoas e da capacidade delas (RF-02). Excluir uma pessoa remove as ausências dela
/// (<c>CASCADE</c> no banco). A recusa de excluir ou mudar de time quem está alocado a demandas
/// chega com as demandas (change 5).
/// </summary>
public static class PersonEndpoints
{
    private const string UnknownTeam = "O time informado não existe.";

    public static IEndpointRouteBuilder MapPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/people").WithTags("People");

        group.MapGet("", GetPeopleAsync)
            .WithName("GetPeople")
            .WithSummary("Lista as pessoas, ordenadas pelo time e depois pelo nome.")
            .Produces<IReadOnlyList<PersonResponse>>(StatusCodes.Status200OK);

        group.MapPost("", CreatePersonAsync)
            .WithName("CreatePerson")
            .WithSummary("Cadastra uma pessoa em um time, com a capacidade em horas por dia útil.")
            .Produces<PersonResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdatePersonAsync)
            .WithName("UpdatePerson")
            .WithSummary("Altera nome, time e capacidade de uma pessoa.")
            .Produces<PersonResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeletePersonAsync)
            .WithName("DeletePerson")
            .WithSummary("Exclui uma pessoa e as ausências dela.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetPeopleAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.People
            .AsNoTracking()
            .Select(person => new
            {
                person.Id,
                person.Name,
                person.TeamId,
                TeamName = person.Team!.Name,
                TeamNormalizedName = person.Team.NormalizedName,
                person.DailyCapacityHours,
            })
            .ToListAsync(cancellationToken);

        // Ordenado aqui, e não no SQL, pelo mesmo motivo da lista de times: a collation do servidor varia.
        var people = rows
            .OrderBy(row => row.TeamNormalizedName, StringComparer.Ordinal)
            .ThenBy(row => row.Name.ToUpperInvariant(), StringComparer.Ordinal)
            .Select(row => new PersonResponse(row.Id, row.Name, row.TeamId, row.TeamName, row.DailyCapacityHours))
            .ToList();

        return Results.Ok(people);
    }

    private static async Task<IResult> CreatePersonAsync(
        PersonRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var team = await db.Teams.FindAsync([request.TeamId], cancellationToken);
        if (team is null)
        {
            return ApiProblems.InvalidField("teamId", UnknownTeam);
        }

        var person = new Person
        {
            Name = request.Name!.Trim(),
            TeamId = team.Id,
            DailyCapacityHours = request.DailyCapacityHours!.Value,
        };

        db.People.Add(person);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsForeignKeyViolation(error))
        {
            // O time foi excluído entre a consulta e a gravação.
            return ApiProblems.InvalidField("teamId", UnknownTeam);
        }

        return Results.Created($"/api/people/{person.Id}", ToResponse(person, team));
    }

    private static async Task<IResult> UpdatePersonAsync(
        Guid id,
        PersonRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var person = await db.People.FindAsync([id], cancellationToken);
        if (person is null)
        {
            return Results.NotFound();
        }

        var team = await db.Teams.FindAsync([request.TeamId], cancellationToken);
        if (team is null)
        {
            return ApiProblems.InvalidField("teamId", UnknownTeam);
        }

        person.Name = request.Name!.Trim();
        person.TeamId = team.Id;
        person.DailyCapacityHours = request.DailyCapacityHours!.Value;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsForeignKeyViolation(error))
        {
            return ApiProblems.InvalidField("teamId", UnknownTeam);
        }

        return Results.Ok(ToResponse(person, team));
    }

    private static async Task<IResult> DeletePersonAsync(
        Guid id,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var person = await db.People.FindAsync([id], cancellationToken);
        if (person is null)
        {
            return Results.NotFound();
        }

        // As ausências saem junto, por CASCADE no banco.
        db.People.Remove(person);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static PersonResponse ToResponse(Person person, Team team) =>
        new(person.Id, person.Name, team.Id, team.Name, person.DailyCapacityHours);
}
