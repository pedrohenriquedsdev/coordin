namespace Coordin.Api.Features.Planning;

/// <summary>
/// Chave do <i>advisory lock</i> do planejamento (design D6). Toda escrita que recalcula pega esse
/// lock dentro da transação: escritas simultâneas andam em fila e os rankings continuam únicos.
/// </summary>
public static class PlanningLocks
{
    public const long Planning = 5_116_001L;
}
