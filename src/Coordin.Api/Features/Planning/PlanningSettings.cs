using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Configurações do planejamento (RF-09, RF-15; DT-13, DT-22). Há um único registro, com <see cref="Id"/> 1.
/// </summary>
public sealed class PlanningSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    public ExecutionOrderMode ExecutionOrderMode { get; set; }

    /// <summary>Percentual da margem de segurança, maior ou igual a zero (DT-22).</summary>
    public decimal SafetyMarginPercent { get; set; }
}
