namespace Coordin.Api.Features.Demands;

/// <summary>
/// Uma dependência entre demandas inteiras (RF-18; DT-23): <see cref="DemandId"/> só pode começar
/// depois do fim previsto, com margem, de <see cref="PrerequisiteDemandId"/>. A API garante que não há
/// ciclo (pelo motor); o banco garante unicidade e que ninguém depende de si mesmo.
/// </summary>
public sealed class DemandDependency
{
    /// <summary>A demanda que espera.</summary>
    public Guid DemandId { get; set; }

    /// <summary>A demanda de que se depende.</summary>
    public Guid PrerequisiteDemandId { get; set; }

    public Demand? Demand { get; set; }

    public Demand? Prerequisite { get; set; }
}
