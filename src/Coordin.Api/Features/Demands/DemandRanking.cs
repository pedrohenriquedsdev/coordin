using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Os dois rankings das demandas (prioridade e execução): leitura da ordem atual e gravação em duas fases
/// (design D7). Compartilhado pelo cadastro, pela reordenação e pelos cenários de sequenciamento, para que
/// nenhum deles grave ranking de um jeito diferente.
/// </summary>
internal static class DemandRanking
{
    /// <summary>Folga usada para tirar os ranks da faixa final antes de gravá-los (design D7).</summary>
    internal const int RankShift = 1_000_000;

    internal static async Task<List<Guid>> OrderedIdsAsync(CoordinDbContext db, bool byPriority, CancellationToken cancellationToken)
    {
        var query = db.Demands.AsNoTracking();
        var ordered = byPriority
            ? query.OrderBy(demand => demand.PriorityRank)
            : query.OrderBy(demand => demand.ExecutionRank);
        return await ordered.ThenBy(demand => demand.Id).Select(demand => demand.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Grava os dois rankings em duas fases (design D7): primeiro tira todos da faixa final, para o índice
    /// único nunca ser violado no meio da troca; depois grava os valores finais, um por demanda.
    /// </summary>
    internal static async Task SetRanksAsync(
        CoordinDbContext db,
        List<Guid> priorityOrder,
        List<Guid> executionOrder,
        CancellationToken cancellationToken)
    {
        await db.Demands.ExecuteUpdateAsync(setters => setters
            .SetProperty(demand => demand.PriorityRank, demand => demand.PriorityRank + RankShift)
            .SetProperty(demand => demand.ExecutionRank, demand => demand.ExecutionRank + RankShift),
            cancellationToken);

        var executionRankOf = executionOrder
            .Select((id, index) => (id, rank: index + 1))
            .ToDictionary(item => item.id, item => item.rank);

        for (var index = 0; index < priorityOrder.Count; index++)
        {
            var id = priorityOrder[index];
            var priorityRank = index + 1;
            var executionRank = executionRankOf[id];
            await db.Demands
                .Where(demand => demand.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(demand => demand.PriorityRank, priorityRank)
                    .SetProperty(demand => demand.ExecutionRank, executionRank),
                    cancellationToken);
        }
    }
}
