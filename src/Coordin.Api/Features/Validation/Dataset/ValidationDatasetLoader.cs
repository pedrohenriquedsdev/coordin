using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Planning;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Validation.Dataset;

/// <summary>Quantos registros do planejamento atual seriam substituídos pelo carregamento (DT-37; RT-11).</summary>
public sealed record ReplacementCount(
    int Teams,
    int People,
    int Absences,
    int Holidays,
    int Demands,
    int Baselines,
    bool CurrentPlanCaptured);

/// <summary>O que o carregamento gravou: as quantidades do conjunto.</summary>
public sealed record LoadDatasetResponse(int Teams, int People, int Absences, int Holidays, int Demands);

/// <summary>
/// Substitui o planejamento inteiro pelo conjunto fictício (DT-37). Apaga, na ordem que respeita as chaves
/// estrangeiras, o que é do planejamento, a linha de base e o plano guardado. A conta do gestor e os tokens de
/// sessão não são tocados. Deve rodar dentro de <see cref="PlanningTransaction"/>, que recalcula e confirma.
/// </summary>
public static class ValidationDatasetLoader
{
    public static async Task<ReplacementCount> CountCurrentAsync(CoordinDbContext db, CancellationToken cancellationToken) => new(
        await db.Teams.CountAsync(cancellationToken),
        await db.People.CountAsync(cancellationToken),
        await db.Absences.CountAsync(cancellationToken),
        await db.Holidays.CountAsync(cancellationToken),
        await db.Demands.CountAsync(cancellationToken),
        await db.ValidationBaselines.CountAsync(cancellationToken),
        await db.ValidationCurrentPlans.AnyAsync(cancellationToken));

    public static async Task<IResult> ReplaceAsync(CoordinDbContext db, ValidationDataset dataset, CancellationToken cancellationToken)
    {
        await DeleteCurrentAsync(db, cancellationToken);

        var teams = dataset.Teams.Select(item => WithId(Team.Create(item.Name), item.Id)).ToList();
        var people = dataset.People.Select(item => WithId(new Person
        {
            Name = item.Name,
            TeamId = item.TeamId,
            DailyCapacityHours = item.DailyCapacityHours,
        }, item.Id)).ToList();
        var absences = dataset.Absences.Select(item => new Absence
        {
            PersonId = item.PersonId,
            Type = AbsenceType.Vacation,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
        }).ToList();
        var holidays = dataset.Holidays.Select(item => new Holiday { Date = item.Date }).ToList();
        var demands = dataset.Demands.Select(item => WithId(new Demand
        {
            Title = item.Title,
            PriorityRank = item.PriorityRank,
            ExecutionRank = item.PriorityRank,
            StartDate = item.StartDate,
            InformedEndDate = item.InformedEndDate,
        }, item.Id)).ToList();
        var demandTeams = dataset.Demands.SelectMany(demand => demand.Parts.Select(part => new DemandTeam
        {
            DemandId = demand.Id,
            TeamId = part.TeamId,
            EstimatedEffortHours = part.EstimatedEffortHours,
        })).ToList();
        var assignments = dataset.Demands.SelectMany(demand => demand.AssignedPersonIds.Select(personId => new DemandAssignment
        {
            DemandId = demand.Id,
            PersonId = personId,
        })).ToList();
        var dependencies = dataset.Demands.SelectMany(demand => demand.PrerequisiteIds.Select(prerequisiteId => new DemandDependency
        {
            DemandId = demand.Id,
            PrerequisiteDemandId = prerequisiteId,
        })).ToList();

        db.Teams.AddRange(teams);
        db.People.AddRange(people);
        db.Absences.AddRange(absences);
        db.Holidays.AddRange(holidays);
        db.Demands.AddRange(demands);
        db.DemandTeams.AddRange(demandTeams);
        db.DemandAssignments.AddRange(assignments);
        db.DemandDependencies.AddRange(dependencies);
        await db.SaveChangesAsync(cancellationToken);

        // A margem e a ordem de execução do conjunto: a validação começa na ordem de prioridade (regra 3).
        await db.PlanningSettings
            .Where(settings => settings.Id == PlanningSettings.SingletonId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(settings => settings.ExecutionOrderMode, ExecutionOrderMode.FollowPriority)
                    .SetProperty(settings => settings.SafetyMarginPercent, (decimal)dataset.MarginPercent),
                cancellationToken);

        return Results.Ok(new LoadDatasetResponse(teams.Count, people.Count, absences.Count, holidays.Count, demands.Count));
    }

    /// <summary>Apaga o planejamento, a linha de base e o plano guardado, do mais dependente para o menos.</summary>
    private static async Task DeleteCurrentAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        await db.DemandDependencies.ExecuteDeleteAsync(cancellationToken);
        await db.DemandAssignments.ExecuteDeleteAsync(cancellationToken);
        await db.DemandTeams.ExecuteDeleteAsync(cancellationToken);
        await db.Forecasts.ExecuteDeleteAsync(cancellationToken);
        await db.ValidationBaselines.ExecuteDeleteAsync(cancellationToken);
        await db.ValidationCurrentPlans.ExecuteDeleteAsync(cancellationToken);
        await db.Demands.ExecuteDeleteAsync(cancellationToken);
        await db.Absences.ExecuteDeleteAsync(cancellationToken);
        await db.People.ExecuteDeleteAsync(cancellationToken);
        await db.Teams.ExecuteDeleteAsync(cancellationToken);
        await db.Holidays.ExecuteDeleteAsync(cancellationToken);
    }

    private static Team WithId(Team team, Guid id)
    {
        team.UseId(id);
        return team;
    }

    private static Person WithId(Person person, Guid id)
    {
        person.UseId(id);
        return person;
    }

    private static Demand WithId(Demand demand, Guid id)
    {
        demand.UseId(id);
        return demand;
    }
}
