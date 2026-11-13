using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>O resultado da classificação de uma demanda: a situação e, se não previsível, o motivo.</summary>
public sealed record Classification(DemandStatus Status, UnpredictableReason? Reason);

/// <summary>
/// Decide quais demandas o sistema consegue prever (RF-10; TDD 6.3). O primeiro motivo que se aplica
/// vence: sem data de início, sem estimativa de algum time, sem capacidade. Demandas concluídas ou
/// não previsíveis ficam fora da simulação e não consomem capacidade (regra inviolável 5).
/// </summary>
public static class PredictabilityClassifier
{
    public static IReadOnlyDictionary<Guid, Classification> Classify(PlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var peopleById = snapshot.People.ToDictionary(person => person.Id);
        var assignmentsByDemand = snapshot.Assignments.ToLookup(assignment => assignment.DemandId);

        return snapshot.Demands.ToDictionary(
            demand => demand.Id,
            demand => Classify(demand, assignmentsByDemand[demand.Id].Select(a => peopleById[a.PersonId]).ToList()));
    }

    private static Classification Classify(DemandInput demand, IReadOnlyList<PersonInput> assignedPeople)
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
