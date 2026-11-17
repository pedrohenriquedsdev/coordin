using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>O resultado da classificação de uma demanda: a situação e, se não previsível, o motivo.</summary>
public sealed record Classification(DemandStatus Status, UnpredictableReason? Reason);

/// <summary>
/// Decide quais demandas o sistema consegue prever (RF-10; TDD 6.3). O primeiro motivo que se aplica
/// vence: sem data de início, sem estimativa de algum time, sem capacidade e, por último, um
/// pré-requisito sem previsão (RF-18). Demandas concluídas ou não previsíveis ficam fora da simulação
/// e não consomem capacidade (regra inviolável 5).
/// </summary>
public static class PredictabilityClassifier
{
    public static IReadOnlyDictionary<Guid, Classification> Classify(PlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var peopleById = snapshot.People.ToDictionary(person => person.Id);
        var assignmentsByDemand = snapshot.Assignments.ToLookup(assignment => assignment.DemandId);
        var demandsById = snapshot.Demands.ToDictionary(demand => demand.Id);
        var prerequisitesByDemand = snapshot.Dependencies.ToLookup(dependency => dependency.DemandId, dependency => dependency.PrerequisiteId);

        var decided = new Dictionary<Guid, Classification>();
        var inProgress = new HashSet<Guid>();

        Classification Decide(DemandInput demand)
        {
            if (decided.TryGetValue(demand.Id, out var known))
            {
                return known;
            }

            // O grafo é acíclico (o validador garante), mas este método é público: falhar alto é melhor
            // que recursão infinita.
            if (!inProgress.Add(demand.Id))
            {
                throw new ArgumentException("As dependências entre as demandas formam um ciclo.");
            }

            var assignedPeople = assignmentsByDemand[demand.Id].Select(assignment => peopleById[assignment.PersonId]).ToList();
            var classification = ClassifyOwn(demand, assignedPeople);

            if (classification.Status == DemandStatus.Predictable
                && HasUnpredictablePrerequisite(demand, prerequisitesByDemand, demandsById, Decide))
            {
                classification = Unpredictable(UnpredictableReason.PrerequisiteUnpredictable);
            }

            inProgress.Remove(demand.Id);
            decided[demand.Id] = classification;
            return classification;
        }

        foreach (var demand in snapshot.Demands)
        {
            Decide(demand);
        }

        // Devolve na ordem do snapshot, e não na ordem em que a recursão decidiu.
        return snapshot.Demands.ToDictionary(demand => demand.Id, demand => decided[demand.Id]);
    }

    /// <summary>
    /// Um pré-requisito concluído está cumprido e nunca bloqueia (RF-12). Qualquer outro precisa ser
    /// previsível; os pré-requisitos são percorridos em ordem de <see cref="Guid"/> só para o resultado
    /// não depender da ordem das listas.
    /// </summary>
    private static bool HasUnpredictablePrerequisite(
        DemandInput demand,
        ILookup<Guid, Guid> prerequisitesByDemand,
        Dictionary<Guid, DemandInput> demandsById,
        Func<DemandInput, Classification> decide)
    {
        foreach (var prerequisiteId in prerequisitesByDemand[demand.Id].Order())
        {
            if (!demandsById.TryGetValue(prerequisiteId, out var prerequisite))
            {
                throw new ArgumentException("Dependência de um pré-requisito que não está no snapshot.");
            }

            if (!prerequisite.IsCompleted && decide(prerequisite).Status != DemandStatus.Predictable)
            {
                return true;
            }
        }

        return false;
    }

    private static Classification ClassifyOwn(DemandInput demand, IReadOnlyList<PersonInput> assignedPeople)
    {
        if (demand.IsCompleted)
        {
            return new Classification(DemandStatus.Completed, null);
        }

        if (demand.StartDate is null)
        {
            return Unpredictable(UnpredictableReason.MissingStartDate);
        }

        if (demand.Parts.Count == 0 || demand.Parts.Any(part => part.EstimatedEffortHours is null))
        {
            return Unpredictable(UnpredictableReason.MissingEstimate);
        }

        var everyPartHasCapacity = demand.Parts.All(part =>
            assignedPeople.Any(person => person.TeamId == part.TeamId && person.DailyHours > 0m));

        return everyPartHasCapacity
            ? new Classification(DemandStatus.Predictable, null)
            : Unpredictable(UnpredictableReason.NoCapacity);
    }

    private static Classification Unpredictable(UnpredictableReason reason) =>
        new(DemandStatus.Unpredictable, reason);
}
