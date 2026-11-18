using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Validation.Dataset;

/// <summary>Um time do conjunto fictício.</summary>
public sealed record DatasetTeam(Guid Id, string Name);

/// <summary>Uma pessoa do conjunto, em um time, com a capacidade em horas por dia útil.</summary>
public sealed record DatasetPerson(Guid Id, string Name, Guid TeamId, decimal DailyCapacityHours);

/// <summary>Um período em que uma pessoa não trabalha, com as duas pontas incluídas.</summary>
public sealed record DatasetAbsence(Guid PersonId, DateOnly StartDate, DateOnly EndDate);

/// <summary>Um feriado do setor.</summary>
public sealed record DatasetHoliday(DateOnly Date);

/// <summary>A parte de um time em uma demanda, com a estimativa do time.</summary>
public sealed record DatasetPart(Guid TeamId, decimal EstimatedEffortHours);

/// <summary>Uma demanda do conjunto: prioridade, início, partes, pessoas alocadas e pré-requisitos.</summary>
public sealed record DatasetDemand(
    Guid Id,
    string Title,
    int PriorityRank,
    DateOnly StartDate,
    DateOnly? InformedEndDate,
    IReadOnlyList<DatasetPart> Parts,
    IReadOnlyList<Guid> AssignedPersonIds,
    IReadOnlyList<Guid> PrerequisiteIds);

/// <summary>
/// O conjunto fictício da simulação (PRD 7.3; DT-37), lido do recurso embutido <c>validation-dataset.json</c>. É o
/// único conteúdo que a validação carrega; nenhum dado real entra no sistema (RNF-05).
/// </summary>
public sealed record ValidationDataset(
    string Name,
    int MarginPercent,
    IReadOnlyList<DatasetTeam> Teams,
    IReadOnlyList<DatasetPerson> People,
    IReadOnlyList<DatasetAbsence> Absences,
    IReadOnlyList<DatasetHoliday> Holidays,
    IReadOnlyList<DatasetDemand> Demands)
{
    /// <summary>O nome do recurso embutido, como definido no projeto da API.</summary>
    public const string ResourceName = "validation-dataset.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Lê o conjunto embutido. Sem o recurso, a aplicação não compila a validação.</summary>
    public static ValidationDataset Read()
    {
        using var stream = typeof(ValidationDataset).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("O conjunto fictício da validação não está embutido na API.");
        return JsonSerializer.Deserialize<ValidationDataset>(stream, Options)
            ?? throw new InvalidOperationException("O conjunto fictício da validação está vazio.");
    }

    /// <summary>
    /// O planejamento que o motor calcula: a ordem de prioridade, a margem do conjunto e as previsões
    /// sem nenhuma demanda concluída (regra 3: a prioridade é a do conjunto).
    /// </summary>
    public PlanningSnapshot ToSnapshot() => new(
        [.. Teams.Select(team => new TeamInput(team.Id))],
        [.. People.Select(person => new PersonInput(person.Id, person.TeamId, person.DailyCapacityHours))],
        [.. Absences.Select(absence => new AbsenceInput(absence.PersonId, absence.StartDate, absence.EndDate))],
        [.. Holidays.Select(holiday => new HolidayInput(holiday.Date))],
        [.. Demands.Select(demand => new DemandInput(
            demand.Id,
            demand.PriorityRank,
            demand.PriorityRank,
            demand.StartDate,
            false,
            [.. demand.Parts.Select(part => new DemandPartInput(part.TeamId, part.EstimatedEffortHours))]))],
        [.. Demands.SelectMany(demand => demand.AssignedPersonIds.Select(personId => new AssignmentInput(demand.Id, personId)))],
        ExecutionOrderMode.FollowPriority,
        MarginPercent,
        [.. Demands.SelectMany(demand => demand.PrerequisiteIds.Select(prerequisiteId => new DependencyInput(demand.Id, prerequisiteId)))]);
}
