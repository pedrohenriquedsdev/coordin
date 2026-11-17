using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Corpo de <c>POST /api/demands</c> e <c>PUT /api/demands/{id}</c> (RF-04 a RF-07; DT-17). O agregado
/// inteiro vai no corpo. Os times e as pessoas são validados pela API, porque dependem do banco.
/// </summary>
public sealed record DemandRequest(
    [property: Required(ErrorMessage = "O título da demanda é obrigatório.")]
    [property: StringLength(Demand.TitleMaxLength, ErrorMessage = "O título deve ter no máximo 200 caracteres.")]
    string? Title,
    DateOnly? StartDate,
    DateOnly? InformedEndDate,
    List<DemandTeamRequest>? Teams,
    List<Guid>? AssignedPersonIds,
    List<Guid>? PrerequisiteIds = null);

/// <summary>Uma parte: o time e a estimativa que ele informou (nula = ainda não estimou).</summary>
public sealed record DemandTeamRequest(Guid? TeamId, decimal? EstimatedEffortHours);

/// <summary>Corpo de <c>PUT /api/demands/priority-order</c>: todas as demandas, na nova ordem.</summary>
public sealed record PriorityOrderRequest(List<Guid>? DemandIds);

/// <summary>Corpo de <c>PUT /api/demands/{id}/completion</c> (RF-12).</summary>
public sealed record CompletionRequest(
    [property: Required(ErrorMessage = "A data de conclusão é obrigatória.")]
    DateOnly? CompletedOn);

/// <summary>Uma demanda com seus times, estimativas e pessoas alocadas.</summary>
public sealed record DemandResponse(
    Guid Id,
    string Title,
    int PriorityRank,
    int ExecutionRank,
    DateOnly? StartDate,
    DateOnly? InformedEndDate,
    DateOnly? CompletedOn,
    IReadOnlyList<DemandTeamResponse> Teams,
    IReadOnlyList<Guid> AssignedPersonIds,
    IReadOnlyList<Guid> PrerequisiteIds);

/// <summary>Uma parte da demanda, com o nome do time para a tela não precisar cruzar listas.</summary>
public sealed record DemandTeamResponse(Guid TeamId, string TeamName, decimal? EstimatedEffortHours);
