using Coordin.Api.Features.Teams;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// A parte de um time em uma demanda, com a estimativa de esforço que o próprio time informou
/// (RF-05, RF-06; DT-11). Sem estimativa (<see cref="EstimatedEffortHours"/> nulo), o time ainda não estimou.
/// </summary>
public sealed class DemandTeam
{
    public Guid DemandId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>Horas estimadas pelo time; quando informadas, maiores que zero.</summary>
    public decimal? EstimatedEffortHours { get; set; }

    public Demand? Demand { get; set; }

    public Team? Team { get; set; }
}
