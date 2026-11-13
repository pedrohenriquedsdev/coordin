namespace Coordin.Engine.Planning;

/// <summary>
/// Qual ranking define a fila de execução (DT-13): a prioridade combinada com o produto ou a ordem
/// de execução aprovada pelo gestor. O motor só lê o ranking; nunca o altera (regras invioláveis 2 e 3).
/// </summary>
public enum ExecutionOrderMode
{
    /// <summary>A fila segue <see cref="DemandInput.PriorityRank"/>.</summary>
    FollowPriority,

    /// <summary>A fila segue <see cref="DemandInput.ExecutionRank"/>.</summary>
    Custom,
}
