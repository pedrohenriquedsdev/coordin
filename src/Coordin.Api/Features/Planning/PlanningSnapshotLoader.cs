using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Monta o <see cref="PlanningSnapshot"/> do motor a partir do banco (DT-02). Só lê: nenhuma regra de
/// cálculo mora aqui. As alocações de demandas concluídas ficam de fora (design D5): o motor já ignora
/// essas demandas, e uma pessoa pode ter mudado de time depois da conclusão.
/// </summary>
public sealed class PlanningSnapshotLoader
{
    public async Task<PlanningSnapshot> LoadAsync(CoordinDbContext db, CancellationToken cancellationToken)
    {
        var teams = await db.Teams.AsNoTracking()
            .Select(team => new TeamInput(team.Id))
            .ToListAsync(cancellationToken);

        var people = await db.People.AsNoTracking()
            .Select(person => new PersonInput(person.Id, person.TeamId, person.DailyCapacityHours))
            .ToListAsync(cancellationToken);

        var absences = await db.Absences.AsNoTracking()
            .Select(absence => new AbsenceInput(absence.PersonId, absence.StartDate, absence.EndDate))
            .ToListAsync(cancellationToken);

        var holidays = await db.Holidays.AsNoTracking()
            .Select(holiday => new HolidayInput(holiday.Date))
            .ToListAsync(cancellationToken);

        var partRows = await db.DemandTeams.AsNoTracking()
            .Select(part => new { part.DemandId, part.TeamId, part.EstimatedEffortHours })
            .ToListAsync(cancellationToken);

        var partsByDemand = partRows
            .GroupBy(part => part.DemandId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(part => new DemandPartInput(part.TeamId, part.EstimatedEffortHours)).ToList());

        var demandRows = await db.Demands.AsNoTracking()
            .Select(demand => new { demand.Id, demand.PriorityRank, demand.ExecutionRank, demand.StartDate, demand.CompletedOn })
            .ToListAsync(cancellationToken);

        var demands = demandRows
            .Select(demand => new DemandInput(
                demand.Id,
                demand.PriorityRank,
                demand.ExecutionRank,
                demand.StartDate,
                demand.CompletedOn is not null,
                partsByDemand.TryGetValue(demand.Id, out var parts) ? parts : []))
            .ToList();

        var assignments = await db.DemandAssignments.AsNoTracking()
            .Where(assignment => assignment.Demand!.CompletedOn == null)
            .Select(assignment => new AssignmentInput(assignment.DemandId, assignment.PersonId))
            .ToListAsync(cancellationToken);

        var settings = await db.PlanningSettings.AsNoTracking()
            .SingleAsync(item => item.Id == PlanningSettings.SingletonId, cancellationToken);

        return new PlanningSnapshot(
            teams,
            people,
            absences,
            holidays,
            demands,
            assignments,
            settings.ExecutionOrderMode,
            settings.SafetyMarginPercent);
    }
}
