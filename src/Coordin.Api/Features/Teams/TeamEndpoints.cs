using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Capacity;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Teams;

/// <summary>
/// Cadastro de times (RF-01). Nenhum endpoint declara regra de autorização: a política padrão da
/// change 2 exige o gestor autenticado.
/// </summary>
public static class TeamEndpoints
{
    private const string NameTaken = "Já existe um time com esse nome.";
    private const string HasPeople = "Não é possível excluir um time que tem pessoas. Remova ou mova as pessoas antes.";

    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams").WithTags("Teams");

        group.MapGet("", GetTeamsAsync)
            .WithName("GetTeams")
            .WithSummary("Lista os times, com a quantidade de pessoas e a capacidade diária de cada um.")
            .Produces<IReadOnlyList<TeamResponse>>(StatusCodes.Status200OK);

        group.MapPost("", CreateTeamAsync)
            .WithName("CreateTeam")
            .WithSummary("Cadastra um time. O nome é único, sem diferenciar maiúsculas.")
            .Produces<TeamResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateTeamAsync)
            .WithName("UpdateTeam")
            .WithSummary("Altera o nome de um time.")
            .Produces<TeamResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteTeamAsync)
            .WithName("DeleteTeam")
            .WithSummary("Exclui um time que não tem pessoas.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetTeamsAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.Teams
            .AsNoTracking()
            .Select(team => new
            {
                team.Id,
                team.Name,
                team.NormalizedName,
                Capacities = team.People.Select(person => person.DailyCapacityHours).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Ordenado aqui, e não no SQL: a ordem do PostgreSQL depende da collation do servidor, e o
        // resultado precisa ser o mesmo em qualquer ambiente. Poucos times, custo desprezível.
        var teams = rows
            .OrderBy(row => row.NormalizedName, StringComparer.Ordinal)
            .Select(row => ToResponse(row.Id, row.Name, row.Capacities))
            .ToList();

        return Results.Ok(teams);
    }

    private static async Task<IResult> CreateTeamAsync(
        TeamRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var team = Team.Create(request.Name!);
        db.Teams.Add(team);

        try
        {
            // Sem "já existe?" antes: o índice único do banco decide, inclusive em corrida.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            return ApiProblems.Conflict("team-name-taken", NameTaken);
        }

        return Results.Created($"/api/teams/{team.Id}", ToResponse(team.Id, team.Name, []));
    }

    private static async Task<IResult> UpdateTeamAsync(
        Guid id,
        TeamRequest request,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var team = await db.Teams.FindAsync([id], cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        team.Rename(request.Name!);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsUniqueViolation(error))
        {
            return ApiProblems.Conflict("team-name-taken", NameTaken);
        }

        var capacities = await db.People
            .AsNoTracking()
            .Where(person => person.TeamId == id)
            .Select(person => person.DailyCapacityHours)
            .ToListAsync(cancellationToken);

        return Results.Ok(ToResponse(team.Id, team.Name, capacities));
    }

    private static async Task<IResult> DeleteTeamAsync(
        Guid id,
        CoordinDbContext db,
        CancellationToken cancellationToken)
    {
        var team = await db.Teams.FindAsync([id], cancellationToken);
        if (team is null)
        {
            return Results.NotFound();
        }

        if (await db.People.AnyAsync(person => person.TeamId == id, cancellationToken))
        {
            return ApiProblems.Conflict("team-has-people", HasPeople);
        }

        db.Teams.Remove(team);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (PostgresErrors.IsDependentRecordsViolation(error))
        {
            // Uma pessoa foi adicionada entre a consulta e a exclusão: o RESTRICT do banco a barrou.
            return ApiProblems.Conflict("team-has-people", HasPeople);
        }

        return Results.NoContent();
    }

    private static TeamResponse ToResponse(Guid id, string name, List<decimal> capacities) =>
        new(id, name, capacities.Count, TeamCapacity.DailyHours(capacities));
}
