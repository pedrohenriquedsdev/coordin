namespace Coordin.Engine.Planning;

/// <summary>
/// Confere se o snapshot faz sentido antes de calcular (design D6). A API já valida o mesmo nas
/// bordas, então um erro aqui é defeito de quem montou o snapshot: falhar alto é melhor que
/// devolver uma previsão silenciosamente errada.
/// </summary>
public static class PlanningSnapshotValidator
{
    /// <exception cref="ArgumentException">Referência inexistente, repetição de <c>Id</c> ou dado incoerente.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Capacidade negativa, esforço menor ou igual a zero ou margem negativa.</exception>
    public static void Validate(PlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.SafetyMarginPercent, nameof(snapshot.SafetyMarginPercent));

        var teamIds = UniqueIds(snapshot.Teams.Select(team => team.Id), "time");
        var people = UniqueIds(snapshot.People.Select(person => person.Id), "pessoa");
        var demandIds = UniqueIds(snapshot.Demands.Select(demand => demand.Id), "demanda");

        foreach (var person in snapshot.People)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(person.DailyHours, nameof(person.DailyHours));
            Require(teamIds.Contains(person.TeamId), "A pessoa pertence a um time que não está no snapshot.");
        }

        foreach (var absence in snapshot.Absences)
        {
            Require(people.Contains(absence.PersonId), "Ausência de uma pessoa que não está no snapshot.");
            Require(absence.EndDate >= absence.StartDate, "O fim da ausência não pode ser anterior ao início.");
        }

        var teamsByDemand = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var demand in snapshot.Demands)
        {
            var partTeams = new HashSet<Guid>();
            foreach (var part in demand.Parts)
            {
                Require(teamIds.Contains(part.TeamId), "A demanda envolve um time que não está no snapshot.");
                Require(partTeams.Add(part.TeamId), "A demanda tem duas partes para o mesmo time.");

                if (part.EstimatedEffortHours is { } effort)
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effort, nameof(part.EstimatedEffortHours));
                }
            }

            teamsByDemand[demand.Id] = partTeams;
        }

        var teamOfPerson = snapshot.People.ToDictionary(person => person.Id, person => person.TeamId);
        var seenAssignments = new HashSet<(Guid, Guid)>();
        foreach (var assignment in snapshot.Assignments)
        {
            Require(demandIds.Contains(assignment.DemandId), "Alocação a uma demanda que não está no snapshot.");
            Require(people.Contains(assignment.PersonId), "Alocação de uma pessoa que não está no snapshot.");
            Require(
                teamsByDemand[assignment.DemandId].Contains(teamOfPerson[assignment.PersonId]),
                "A pessoa alocada não é de nenhum dos times da demanda.");
            Require(seenAssignments.Add((assignment.DemandId, assignment.PersonId)), "Alocação repetida.");
        }
    }

    private static HashSet<Guid> UniqueIds(IEnumerable<Guid> ids, string what)
    {
        var set = new HashSet<Guid>();
        foreach (var id in ids)
        {
            Require(set.Add(id), $"Há dois itens do tipo {what} com o mesmo Id.");
        }

        return set;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}
