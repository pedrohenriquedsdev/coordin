using Coordin.Api.Features.Planning;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Cadastro de demandas (RF-04 a RF-07, RF-12; DT-17). Toda escrita passa por
/// <see cref="PlanningTransaction"/>, que recalcula as previsões na mesma transação. Nenhum endpoint
/// declara regra de autorização: a política padrão exige o gestor autenticado.
/// </summary>
public static class DemandEndpoints
{
    public static IEndpointRouteBuilder MapDemandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/demands").WithTags("Demands");

        group.MapGet("", GetDemandsAsync)
            .WithName("GetDemands")
            .WithSummary("Lista as demandas na ordem de prioridade, com times, estimativas e pessoas alocadas.")
            .Produces<IReadOnlyList<DemandResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetDemandAsync)
            .WithName("GetDemand")
            .WithSummary("Consulta uma demanda.")
            .Produces<DemandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateDemandAsync)
            .WithName("CreateDemand")
            .WithSummary("Cadastra uma demanda. Ela entra no fim da ordem de prioridade e de execução.")
            .Produces<DemandResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateDemandAsync)
            .WithName("UpdateDemand")
            .WithSummary("Altera uma demanda inteira (dados, times, estimativas e pessoas). Os rankings não mudam.")
            .Produces<DemandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteDemandAsync)
            .WithName("DeleteDemand")
            .WithSummary("Exclui uma demanda não concluída e recompacta os rankings.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/estimate-history", DemandHistoryEndpoints.GetEstimateHistoryAsync)
            .WithName("GetEstimateHistory")
            .WithSummary("Alterações de estimativa de uma demanda, por time, em ordem cronológica (RF-20).")
            .Produces<List<EstimateHistoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/forecast-history", DemandHistoryEndpoints.GetForecastHistoryAsync)
            .WithName("GetForecastHistory")
            .WithSummary("Mudanças da data prevista de uma demanda, com a causa, em ordem cronológica (RF-21).")
            .Produces<List<ForecastHistoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/priority-order", ReorderDemandsAsync)
            .WithName("ReorderDemands")
            .WithSummary("Define a ordem de prioridade com a lista completa dos ids das demandas.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}/completion", CompleteDemandAsync)
            .WithName("CompleteDemand")
            .WithSummary("Registra a data real de conclusão. A demanda fica travada e a previsão, congelada.")
            .Produces<DemandResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetDemandsAsync(CoordinDbContext db, CancellationToken cancellationToken) =>
        Results.Ok(await ReadAsync(db, null, cancellationToken));

    private static async Task<IResult> GetDemandAsync(Guid id, CoordinDbContext db, CancellationToken cancellationToken)
    {
        var demand = (await ReadAsync(db, id, cancellationToken)).SingleOrDefault();
        return demand is null ? Results.NotFound() : Results.Ok(demand);
    }

    private static Task<IResult> CreateDemandAsync(
        DemandRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.NewDemand, async () =>
        {
            var invalid = await ValidateNewDemandAsync(request, db, cancellationToken);
            if (invalid is not null)
            {
                return invalid;
            }

            // Sem "já existe?": o lock do planejamento faz as criações andarem em fila (design D6).
            var lastPriority = await db.Demands.MaxAsync(demand => (int?)demand.PriorityRank, cancellationToken) ?? 0;
            var lastExecution = await db.Demands.MaxAsync(demand => (int?)demand.ExecutionRank, cancellationToken) ?? 0;

            var demand = new Demand { PriorityRank = lastPriority + 1, ExecutionRank = lastExecution + 1 };
            var cycle = await CycleProblemAsync(db, demand.Id, request.Title!.Trim(), request.PrerequisiteIds ?? [], cancellationToken);
            if (cycle is not null)
            {
                return cycle;
            }

            ApplyRequest(demand, request, db, clock.GetUtcNow());
            db.Demands.Add(demand);
            await db.SaveChangesAsync(cancellationToken);

            // Com posição, a demanda entra ali nas duas ordens e as demais descem (design D10). Sem posição, fica no fim.
            if (request.Position is { } position && position < await db.Demands.CountAsync(cancellationToken))
            {
                await PlaceAsync(db, demand.Id, position, cancellationToken);
            }

            var response = (await ReadAsync(db, demand.Id, cancellationToken)).Single();
            return Results.Created($"/api/demands/{demand.Id}", response);
        }, cancellationToken);

    private static Task<IResult> UpdateDemandAsync(
        Guid id,
        DemandRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Preenchido pela escrita, lido pelo recálculo logo depois, dentro da mesma transação.
        RequestChanges? changes = null;

        return PlanningTransaction.RunAsync(
            db,
            recalculator,
            () => CauseOf(changes ?? RequestChanges.None),
            async () =>
            {
                var demand = await db.Demands
                    .Include(item => item.Teams)
                    .Include(item => item.Assignments)
                    .Include(item => item.Prerequisites)
                    .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (demand is null)
                {
                    return Results.NotFound();
                }

                if (demand.CompletedOn is not null)
                {
                    return ApiProblems.Conflict(DemandErrors.LockedCode, DemandErrors.LockedMessage);
                }

                var invalid = await ValidateAsync(request, db, cancellationToken);
                if (invalid is not null)
                {
                    return invalid;
                }

                var cycle = await CycleProblemAsync(db, id, request.Title!.Trim(), request.PrerequisiteIds ?? [], cancellationToken);
                if (cycle is not null)
                {
                    return cycle;
                }

                changes = ApplyRequest(demand, request, db, clock.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok((await ReadAsync(db, id, cancellationToken)).Single());
            },
            cancellationToken);
    }

    /// <summary>
    /// A causa de uma atualização de demanda: a estimativa tem prioridade sobre a dependência, e as duas sobre a
    /// alocação (datas de início e pessoas). Premissa aprovada na change <c>delivery-insights</c>.
    /// </summary>
    private static ForecastChangeCause CauseOf(RequestChanges changes) =>
        changes.Estimates ? ForecastChangeCause.Estimate
        : changes.Prerequisites ? ForecastChangeCause.Dependency
        : ForecastChangeCause.Allocation;

    private static Task<IResult> DeleteDemandAsync(
        Guid id,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.Allocation, async () =>
        {
            var demand = await db.Demands.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (demand is null)
            {
                return Results.NotFound();
            }

            if (demand.CompletedOn is not null)
            {
                return ApiProblems.Conflict(DemandErrors.LockedCode, DemandErrors.LockedMessage);
            }

            // Uma demanda de que outra depende não sai: o gestor remove a dependência antes (DT-18, RF-18).
            var dependents = await db.DemandDependencies.AsNoTracking()
                .Where(dependency => dependency.PrerequisiteDemandId == id)
                .OrderBy(dependency => dependency.Demand!.PriorityRank)
                .Select(dependency => dependency.Demand!.Title)
                .ToListAsync(cancellationToken);
            if (dependents.Count > 0)
            {
                return ApiProblems.Conflict(
                    DemandErrors.DependentsCode,
                    $"A demanda é pré-requisito de: {string.Join(", ", dependents)}. Remova essas dependências antes de excluir.");
            }

            // As partes, alocações, dependências e previsão saem em cascata no banco (TDD 4.4).
            db.Demands.Remove(demand);
            await db.SaveChangesAsync(cancellationToken);

            // Recompacta os rankings: cada ordem continua relativa, mas sem buracos.
            var priorityOrder = await DemandRanking.OrderedIdsAsync(db, byPriority: true, cancellationToken);
            var executionOrder = await DemandRanking.OrderedIdsAsync(db, byPriority: false, cancellationToken);
            await DemandRanking.SetRanksAsync(db, priorityOrder, executionOrder, cancellationToken);

            return Results.NoContent();
        }, cancellationToken);

    private static Task<IResult> ReorderDemandsAsync(
        PriorityOrderRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(db, recalculator, ForecastChangeCause.Allocation, async () =>
        {
            var currentIds = (await db.Demands.AsNoTracking().Select(demand => demand.Id).ToListAsync(cancellationToken)).ToHashSet();
            var requested = request.DemandIds;

            var valid = requested is not null
                && requested.Count == currentIds.Count
                && requested.Distinct().Count() == requested.Count
                && requested.All(currentIds.Contains);
            if (!valid)
            {
                return ApiProblems.InvalidField("demandIds", DemandErrors.ReorderInvalid);
            }

            // No modo FollowPriority a execução segue a prioridade; no Custom, a ordem de execução aprovada fica como está.
            var mode = await db.PlanningSettings.AsNoTracking()
                .Where(settings => settings.Id == PlanningSettings.SingletonId)
                .Select(settings => settings.ExecutionOrderMode)
                .SingleAsync(cancellationToken);
            var executionOrder = mode == ExecutionOrderMode.FollowPriority
                ? requested!
                : await DemandRanking.OrderedIdsAsync(db, byPriority: false, cancellationToken);

            await DemandRanking.SetRanksAsync(db, requested!, executionOrder, cancellationToken);
            return Results.NoContent();
        }, cancellationToken);

    private static Task<IResult> CompleteDemandAsync(
        Guid id,
        CompletionRequest request,
        CoordinDbContext db,
        ForecastRecalculator recalculator,
        CancellationToken cancellationToken) =>
        PlanningTransaction.RunAsync(
            db,
            recalculator,
            () => ForecastChangeCause.Allocation,
            async () =>
            {
                var demand = await db.Demands.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (demand is null)
                {
                    return Results.NotFound();
                }

                if (demand.CompletedOn is not null)
                {
                    return ApiProblems.Conflict(DemandErrors.LockedCode, DemandErrors.LockedMessage);
                }

                var completedOn = request.CompletedOn!.Value;
                if (demand.StartDate is { } start && completedOn < start)
                {
                    return ApiProblems.InvalidField("completedOn", DemandErrors.CompletionBeforeStart);
                }

                if (request.ActualEffortHours is { } actual && actual <= 0m)
                {
                    return ApiProblems.InvalidField("actualEffortHours", DemandErrors.ActualEffortNotPositive);
                }

                demand.CompletedOn = completedOn;
                demand.ActualEffortHours = request.ActualEffortHours;
                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok((await ReadAsync(db, id, cancellationToken)).Single());
            },
            cancellationToken,
            afterRecalculation: () => RecordCompletionSnapshotAsync(db, id, cancellationToken));

    /// <summary>
    /// Grava, na conclusão, a previsão e a estimativa vigentes (RF-22, RF-23). Roda depois do recálculo, dentro da
    /// mesma transação. A previsão de uma demanda concluída fica congelada (DT-27), então o valor lido é o da conclusão.
    /// </summary>
    private static async Task RecordCompletionSnapshotAsync(CoordinDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var demand = await db.Demands.Include(item => item.Teams).SingleAsync(item => item.Id == id, cancellationToken);
        var forecast = await db.Forecasts.AsNoTracking().SingleOrDefaultAsync(row => row.DemandId == id, cancellationToken);

        demand.ForecastEndDateAtCompletion = forecast?.ForecastEndDate;

        var estimates = demand.Teams
            .Where(part => part.EstimatedEffortHours is not null)
            .Select(part => part.EstimatedEffortHours!.Value)
            .ToList();
        demand.EstimatedEffortHoursAtCompletion = estimates.Count == 0 ? null : estimates.Sum();

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Valida o corpo contra o banco. Devolve a resposta de erro, ou <c>null</c> se estiver tudo certo.</summary>
    /// <summary>
    /// Valida o corpo de uma demanda nova, para criar ou simular: as mesmas regras de sempre, mais a posição,
    /// que tem de estar entre 1 e o número de demandas mais 1 (design D10).
    /// </summary>
    internal static async Task<IResult?> ValidateNewDemandAsync(DemandRequest request, CoordinDbContext db, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, db, cancellationToken);
        if (invalid is not null || request.Position is null)
        {
            return invalid;
        }

        var count = await db.Demands.CountAsync(cancellationToken);
        return request.Position is >= 1 && request.Position <= count + 1
            ? null
            : ApiProblems.InvalidField("position", DemandErrors.PositionOutOfRange);
    }

    /// <summary>Move a demanda para a posição (de 1 em diante) nas duas ordens e regrava os rankings em duas fases.</summary>
    private static async Task PlaceAsync(CoordinDbContext db, Guid id, int position, CancellationToken cancellationToken)
    {
        var priority = await DemandRanking.OrderedIdsAsync(db, byPriority: true, cancellationToken);
        var execution = await DemandRanking.OrderedIdsAsync(db, byPriority: false, cancellationToken);
        priority.Remove(id);
        priority.Insert(position - 1, id);
        execution.Remove(id);
        execution.Insert(position - 1, id);
        await DemandRanking.SetRanksAsync(db, priority, execution, cancellationToken);
    }

    private static async Task<IResult?> ValidateAsync(DemandRequest request, CoordinDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ApiProblems.InvalidField("title", DemandErrors.TitleRequired);
        }

        var teams = request.Teams ?? [];
        if (teams.Any(part => part.TeamId is null))
        {
            return ApiProblems.InvalidField("teams", DemandErrors.TeamMissing);
        }

        if (teams.Any(part => part.EstimatedEffortHours is { } hours && (hours <= 0m || decimal.Round(hours, 2) != hours || hours > 1_000_000m)))
        {
            return ApiProblems.InvalidField("teams", DemandErrors.EffortInvalid);
        }

        var teamIds = teams.Select(part => part.TeamId!.Value).ToList();
        if (teamIds.Distinct().Count() != teamIds.Count)
        {
            return ApiProblems.InvalidField("teams", DemandErrors.TeamRepeated);
        }

        var knownTeams = await db.Teams.AsNoTracking()
            .Where(team => teamIds.Contains(team.Id))
            .CountAsync(cancellationToken);
        if (knownTeams != teamIds.Count)
        {
            return ApiProblems.InvalidField("teams", DemandErrors.TeamUnknown);
        }

        var personIds = request.AssignedPersonIds ?? [];
        if (personIds.Distinct().Count() != personIds.Count)
        {
            return ApiProblems.InvalidField("assignedPersonIds", DemandErrors.PersonRepeated);
        }

        var people = await db.People.AsNoTracking()
            .Where(person => personIds.Contains(person.Id))
            .Select(person => new { person.Id, person.TeamId })
            .ToListAsync(cancellationToken);
        if (people.Count != personIds.Count)
        {
            return ApiProblems.InvalidField("assignedPersonIds", DemandErrors.PersonUnknown);
        }

        var demandTeams = teamIds.ToHashSet();
        if (people.Any(person => !demandTeams.Contains(person.TeamId)))
        {
            return ApiProblems.Conflict(DemandErrors.PersonNotInTeamsCode, DemandErrors.PersonNotInTeamsMessage);
        }

        // Só existência e repetição: ciclo (inclusive a autodependência) é regra do motor e responde 409 (DT-23).
        var prerequisiteIds = request.PrerequisiteIds ?? [];
        if (prerequisiteIds.Distinct().Count() != prerequisiteIds.Count)
        {
            return ApiProblems.InvalidField("prerequisiteIds", DemandErrors.PrerequisiteRepeated);
        }

        var knownPrerequisites = await db.Demands.AsNoTracking()
            .Where(demand => prerequisiteIds.Contains(demand.Id))
            .CountAsync(cancellationToken);
        if (knownPrerequisites != prerequisiteIds.Count)
        {
            return ApiProblems.InvalidField("prerequisiteIds", DemandErrors.PrerequisiteUnknown);
        }

        return null;
    }

    /// <summary>
    /// Confere, com o motor, se as dependências pedidas para <paramref name="demandId"/> fecham um ciclo
    /// com as já gravadas das outras demandas (DT-23). Devolve o <c>409 dependency-cycle</c> com os títulos
    /// do caminho, ou <c>null</c>. Roda antes de qualquer escrita: nada é gravado quando há ciclo.
    /// </summary>
    private static async Task<IResult?> CycleProblemAsync(
        CoordinDbContext db,
        Guid demandId,
        string title,
        IReadOnlyList<Guid> prerequisiteIds,
        CancellationToken cancellationToken)
    {
        var stored = await db.DemandDependencies.AsNoTracking()
            .Where(dependency => dependency.DemandId != demandId)
            .Select(dependency => new DependencyInput(dependency.DemandId, dependency.PrerequisiteDemandId))
            .ToListAsync(cancellationToken);
        var requested = prerequisiteIds.Select(prerequisiteId => new DependencyInput(demandId, prerequisiteId));
        var allDependencies = stored.Concat(requested).ToList();

        var demandIds = await db.Demands.AsNoTracking().Select(demand => demand.Id).ToListAsync(cancellationToken);
        demandIds.Add(demandId); // a demanda nova ainda não existe no banco

        var cycle = DependencyGraph.FindCycle(demandIds, allDependencies);
        if (cycle is null)
        {
            return null;
        }

        var titles = await db.Demands.AsNoTracking()
            .Where(demand => cycle.Contains(demand.Id))
            .ToDictionaryAsync(demand => demand.Id, demand => demand.Title, cancellationToken);
        titles[demandId] = title; // o título que o gestor acabou de digitar, ainda não gravado

        var path = string.Join(" → ", cycle.Select(id => titles[id]));
        return ApiProblems.Conflict(DemandErrors.CycleCode, $"As dependências formariam um ciclo: {path}.");
    }

    /// <summary>
    /// Copia o corpo para a demanda: dados, partes por time e alocações. As linhas que saíram do corpo
    /// são removidas, as novas são criadas e as que continuam só mudam a estimativa.
    /// </summary>
    private static RequestChanges ApplyRequest(Demand demand, DemandRequest request, CoordinDbContext db, DateTimeOffset changedAt)
    {
        demand.Title = request.Title!.Trim();
        demand.StartDate = request.StartDate;
        demand.InformedEndDate = request.InformedEndDate;

        var reason = string.IsNullOrWhiteSpace(request.EstimateChangeReason) ? null : request.EstimateChangeReason.Trim();
        var estimatesChanged = false;

        // Cada mudança de estimativa vira um registro (RF-20): retirar um time também conta, porque a estimativa some.
        var requestedTeams = (request.Teams ?? []).ToDictionary(part => part.TeamId!.Value, part => part.EstimatedEffortHours);
        foreach (var part in demand.Teams.Where(part => !requestedTeams.ContainsKey(part.TeamId)).ToList())
        {
            if (part.EstimatedEffortHours is not null)
            {
                db.EstimateHistories.Add(EstimateChange(demand.Id, part.TeamId, part.EstimatedEffortHours, null, reason, changedAt));
                estimatesChanged = true;
            }

            db.DemandTeams.Remove(part);
        }

        foreach (var (teamId, hours) in requestedTeams)
        {
            var existing = demand.Teams.SingleOrDefault(part => part.TeamId == teamId);
            if (existing is null)
            {
                db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = teamId, EstimatedEffortHours = hours });
                if (hours is not null)
                {
                    db.EstimateHistories.Add(EstimateChange(demand.Id, teamId, null, hours, reason, changedAt));
                    estimatesChanged = true;
                }
            }
            else if (existing.EstimatedEffortHours != hours)
            {
                db.EstimateHistories.Add(EstimateChange(demand.Id, teamId, existing.EstimatedEffortHours, hours, reason, changedAt));
                existing.EstimatedEffortHours = hours;
                estimatesChanged = true;
            }
        }

        var requestedPeople = (request.AssignedPersonIds ?? []).ToHashSet();
        foreach (var assignment in demand.Assignments.Where(item => !requestedPeople.Contains(item.PersonId)).ToList())
        {
            db.DemandAssignments.Remove(assignment);
        }

        foreach (var personId in requestedPeople.Where(personId => !demand.Assignments.Any(item => item.PersonId == personId)))
        {
            db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = personId });
        }

        // O corpo substitui o conjunto inteiro; sem o campo, a demanda fica sem pré-requisitos.
        var prerequisitesChanged = false;
        var requestedPrerequisites = (request.PrerequisiteIds ?? []).ToHashSet();
        foreach (var dependency in demand.Prerequisites.Where(item => !requestedPrerequisites.Contains(item.PrerequisiteDemandId)).ToList())
        {
            db.DemandDependencies.Remove(dependency);
            prerequisitesChanged = true;
        }

        foreach (var prerequisiteId in requestedPrerequisites.Where(id => !demand.Prerequisites.Any(item => item.PrerequisiteDemandId == id)))
        {
            db.DemandDependencies.Add(new DemandDependency { DemandId = demand.Id, PrerequisiteDemandId = prerequisiteId });
            prerequisitesChanged = true;
        }

        return new RequestChanges(estimatesChanged, prerequisitesChanged);
    }

    private static EstimateHistory EstimateChange(
        Guid demandId,
        Guid teamId,
        decimal? previous,
        decimal? next,
        string? reason,
        DateTimeOffset changedAt) =>
        new()
        {
            DemandId = demandId,
            TeamId = teamId,
            PreviousHours = previous,
            NewHours = next,
            Reason = reason,
            ChangedAt = changedAt,
        };

    /// <summary>Lê as demandas (todas, ou só uma) com as partes e as alocações, na ordem de prioridade.</summary>
    private static async Task<List<DemandResponse>> ReadAsync(CoordinDbContext db, Guid? onlyId, CancellationToken cancellationToken)
    {
        var query = db.Demands.AsNoTracking();
        if (onlyId is { } id)
        {
            query = query.Where(demand => demand.Id == id);
        }

        var demands = await query.OrderBy(demand => demand.PriorityRank).ToListAsync(cancellationToken);
        var demandIds = demands.Select(demand => demand.Id).ToList();

        var parts = await db.DemandTeams.AsNoTracking()
            .Where(part => demandIds.Contains(part.DemandId))
            .Select(part => new { part.DemandId, part.TeamId, TeamName = part.Team!.Name, part.EstimatedEffortHours })
            .ToListAsync(cancellationToken);

        var assignments = await db.DemandAssignments.AsNoTracking()
            .Where(assignment => demandIds.Contains(assignment.DemandId))
            .Select(assignment => new { assignment.DemandId, assignment.PersonId })
            .ToListAsync(cancellationToken);

        var prerequisites = await db.DemandDependencies.AsNoTracking()
            .Where(dependency => demandIds.Contains(dependency.DemandId))
            .Select(dependency => new { dependency.DemandId, dependency.PrerequisiteDemandId, dependency.Prerequisite!.PriorityRank })
            .ToListAsync(cancellationToken);

        return demands.Select(demand => new DemandResponse(
            demand.Id,
            demand.Title,
            demand.PriorityRank,
            demand.ExecutionRank,
            demand.StartDate,
            demand.InformedEndDate,
            demand.CompletedOn,
            parts
                .Where(part => part.DemandId == demand.Id)
                .OrderBy(part => part.TeamName, StringComparer.Ordinal)
                .Select(part => new DemandTeamResponse(part.TeamId, part.TeamName, part.EstimatedEffortHours))
                .ToList(),
            assignments
                .Where(assignment => assignment.DemandId == demand.Id)
                .Select(assignment => assignment.PersonId)
                .OrderBy(personId => personId)
                .ToList(),
            prerequisites
                .Where(dependency => dependency.DemandId == demand.Id)
                .OrderBy(dependency => dependency.PriorityRank)
                .ThenBy(dependency => dependency.PrerequisiteDemandId)
                .Select(dependency => dependency.PrerequisiteDemandId)
                .ToList()))
            .ToList();
    }
}

/// <summary>O que uma escrita de demanda mudou, para escolher a causa do recálculo (RF-21).</summary>
internal sealed record RequestChanges(bool Estimates, bool Prerequisites)
{
    public static readonly RequestChanges None = new(false, false);
}
