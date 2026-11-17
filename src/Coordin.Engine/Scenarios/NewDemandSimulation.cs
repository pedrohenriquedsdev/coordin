using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Scenarios;

/// <summary>Uma demanda existente cuja data prevista muda quando a nova demanda entra (RF-16).</summary>
/// <param name="DemandId">A demanda existente.</param>
/// <param name="CurrentForecastEndDate">A data prevista, com margem, sem a nova demanda.</param>
/// <param name="NewForecastEndDate">A data prevista, com margem, com a nova demanda.</param>
/// <param name="DeltaDays">Nova menos atual, em dias corridos (DT-27): positivo é atraso.</param>
public sealed record DemandChange(Guid DemandId, DateOnly CurrentForecastEndDate, DateOnly NewForecastEndDate, int DeltaDays);

/// <summary>O efeito de incluir uma nova demanda no planejamento (RF-16).</summary>
/// <param name="NewDemand">A previsão da nova demanda, ou o motivo de ela não ser previsível.</param>
/// <param name="Position">A posição, a partir de 1, em que ela entrou nas ordens de prioridade e de execução.</param>
/// <param name="Changes">As demandas existentes cuja data prevista muda, na ordem de execução com a nova demanda.</param>
/// <param name="SectorEndDate">O prazo total do setor com a nova demanda; <c>null</c> se nada é previsível.</param>
/// <param name="SectorDeltaDays">A diferença do prazo total do setor em dias corridos; zero quando um dos lados não tem data.</param>
public sealed record NewDemandImpact(
    DemandForecast NewDemand,
    int Position,
    IReadOnlyList<DemandChange> Changes,
    DateOnly? SectorEndDate,
    int SectorDeltaDays);

/// <summary>
/// Simula a inclusão de uma nova demanda sem alterar nada (RF-16; DT-17; design D10): calcula o cronograma
/// sem e com ela, posta em uma posição das ordens de prioridade e de execução, e compara demanda a demanda.
/// No fim da fila ela só usa as horas que sobram e não desloca ninguém; na frente, desloca as que dividem
/// pessoas com ela. Função pura: confirmar a inclusão é o cadastro de demanda que já existe.
/// </summary>
public static class NewDemandSimulation
{
    /// <summary>Calcula o impacto de incluir <paramref name="newDemand"/> em <paramref name="position"/>.</summary>
    /// <param name="snapshot">O planejamento vigente (não é alterado).</param>
    /// <param name="newDemand">A nova demanda, com as partes por time; os rankings que ela trouxer são ignorados.</param>
    /// <param name="assignments">As pessoas alocadas à nova demanda.</param>
    /// <param name="dependencies">As dependências da nova demanda (de quem ela depende).</param>
    /// <param name="position">De 1 a <c>n + 1</c>, com <c>n</c> o número de demandas; sem valor, o fim.</param>
    /// <exception cref="ArgumentOutOfRangeException">A posição está fora de 1 a <c>n + 1</c>.</exception>
    /// <exception cref="ArgumentException">O snapshot com a nova demanda é incoerente (por exemplo, um id repetido).</exception>
    public static NewDemandImpact Simulate(
        PlanningSnapshot snapshot,
        DemandInput newDemand,
        IReadOnlyList<AssignmentInput> assignments,
        IReadOnlyList<DependencyInput> dependencies,
        int? position = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(newDemand);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(dependencies);

        var resolved = position ?? snapshot.Demands.Count + 1;
        ArgumentOutOfRangeException.ThrowIfLessThan(resolved, 1, nameof(position));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resolved, snapshot.Demands.Count + 1, nameof(position));

        // A nova demanda ocupa a posição nas duas ordens, e quem estava dali em diante desce uma.
        var withNew = snapshot with
        {
            Demands =
            [
                .. snapshot.Demands.Select(demand => demand with
                {
                    PriorityRank = demand.PriorityRank >= resolved ? demand.PriorityRank + 1 : demand.PriorityRank,
                    ExecutionRank = demand.ExecutionRank >= resolved ? demand.ExecutionRank + 1 : demand.ExecutionRank,
                }),
                newDemand with { PriorityRank = resolved, ExecutionRank = resolved },
            ],
            Assignments = [.. snapshot.Assignments, .. assignments],
            Dependencies = [.. snapshot.Dependencies, .. dependencies],
        };

        var before = ForecastEngine.Calculate(snapshot);
        var after = ForecastEngine.Calculate(withNew);
        var beforeById = before.Demands.ToDictionary(forecast => forecast.DemandId);

        // Só as previsíveis nos dois cronogramas: uma demanda existente que passaria a não previsível
        // (estouro do horizonte de cinco anos) é um caso extremo e não entra na lista de mudanças.
        var changes = after.Demands
            .Where(forecast => forecast.DemandId != newDemand.Id
                && forecast.Status == DemandStatus.Predictable
                && beforeById[forecast.DemandId].Status == DemandStatus.Predictable
                && forecast.ForecastEndDate != beforeById[forecast.DemandId].ForecastEndDate)
            .Select(forecast => new DemandChange(
                forecast.DemandId,
                beforeById[forecast.DemandId].ForecastEndDate!.Value,
                forecast.ForecastEndDate!.Value,
                forecast.ForecastEndDate!.Value.DayNumber - beforeById[forecast.DemandId].ForecastEndDate!.Value.DayNumber))
            .ToList();

        var sectorBefore = LastEnd(before);
        var sectorAfter = LastEnd(after);

        return new NewDemandImpact(
            after.Demands.Single(forecast => forecast.DemandId == newDemand.Id),
            resolved,
            changes,
            sectorAfter,
            sectorBefore is { } was && sectorAfter is { } now ? now.DayNumber - was.DayNumber : 0);
    }

    private static DateOnly? LastEnd(ForecastResult result)
    {
        var ends = result.Demands
            .Where(forecast => forecast.Status == DemandStatus.Predictable)
            .Select(forecast => forecast.ForecastEndDate!.Value)
            .ToList();
        return ends.Count == 0 ? null : ends.Max();
    }
}
