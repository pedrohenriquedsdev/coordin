using Coordin.Engine.Capacity;
using Coordin.Engine.Planning;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// As tabelas indexadas que a simulação consulta, montadas <b>uma vez</b> por planejamento (design D3):
/// demandas e partes viram números de índice, as alocações de cada pessoa viram listas de índices e a
/// capacidade de cada pessoa por dia é guardada à medida que é pedida. Nada aqui depende da ordem de
/// execução, então todas as rodadas de um <see cref="PreparedPlanning"/> as reaproveitam. Não é seguro
/// para uso em várias threads ao mesmo tempo (o cache de capacidade é preenchido sob demanda).
/// </summary>
internal sealed class SimulationTables
{
    private readonly PersonCapacityCalendar _capacity;
    private readonly int _baseDay;
    private readonly decimal[]?[] _capacityByDay;

    public SimulationTables(PlanningSnapshot snapshot, PersonCapacityCalendar capacity)
    {
        _capacity = capacity;

        DemandCount = snapshot.Demands.Count;
        DemandIds = new Guid[DemandCount];
        StartDay = new int[DemandCount];
        FirstPart = new int[DemandCount];
        PartCount = new int[DemandCount];
        Prerequisites = new int[DemandCount][];
        var demandIndex = new Dictionary<Guid, int>(DemandCount);
        var partTeam = new List<Guid>();
        var partEffort = new List<decimal>();
        var partDemand = new List<int>();
        var partIndex = new Dictionary<(int Demand, Guid Team), int>();

        for (var index = 0; index < DemandCount; index++)
        {
            var demand = snapshot.Demands[index];
            DemandIds[index] = demand.Id;
            demandIndex[demand.Id] = index;
            StartDay[index] = demand.StartDate?.DayNumber ?? int.MinValue;
            FirstPart[index] = partTeam.Count;
            PartCount[index] = demand.Parts.Count;
            foreach (var part in demand.Parts)
            {
                partIndex[(index, part.TeamId)] = partTeam.Count;
                partTeam.Add(part.TeamId);
                partEffort.Add(part.EstimatedEffortHours ?? 0m); // sem estimativa a demanda nem é simulada
                partDemand.Add(index);
            }
        }

        DemandIndex = demandIndex;
        PartTeamId = [.. partTeam];
        PartEffort = [.. partEffort];
        PartDemand = [.. partDemand];

        var prerequisites = snapshot.Dependencies
            .GroupBy(dependency => dependency.DemandId)
            .ToDictionary(group => group.Key, group => group.Select(dependency => demandIndex[dependency.PrerequisiteId]).ToArray());
        for (var index = 0; index < DemandCount; index++)
        {
            Prerequisites[index] = prerequisites.TryGetValue(DemandIds[index], out var list) ? list : [];
        }

        // As pessoas na ordem de id: a mesma em que a simulação sempre as percorreu (RNF-02).
        People = [.. snapshot.People.OrderBy(person => person.Id)];
        var assignments = snapshot.Assignments.ToLookup(assignment => assignment.PersonId);
        AssignedDemands = new int[People.Length][];
        AssignedParts = new int[People.Length][];
        for (var index = 0; index < People.Length; index++)
        {
            var person = People[index];
            var mine = assignments[person.Id].Select(assignment => demandIndex[assignment.DemandId]).ToArray();
            AssignedDemands[index] = mine;
            AssignedParts[index] = [.. mine.Select(demand => partIndex[(demand, person.TeamId)])];
        }

        var starts = snapshot.Demands.Where(demand => demand.StartDate is not null).Select(demand => demand.StartDate!.Value).ToList();
        if (starts.Count == 0)
        {
            _baseDay = 0;
            _capacityByDay = [];
        }
        else
        {
            _baseDay = starts.Min().DayNumber;
            _capacityByDay = new decimal[]?[starts.Max().AddYears(5).DayNumber - _baseDay + 2];
        }
    }

    public int DemandCount { get; }

    public Guid[] DemandIds { get; }

    public IReadOnlyDictionary<Guid, int> DemandIndex { get; }

    /// <summary>Dia de início de cada demanda (número do dia); só vale para as previsíveis.</summary>
    public int[] StartDay { get; }

    /// <summary>Índice global da primeira parte de cada demanda; as partes de uma demanda são seguidas.</summary>
    public int[] FirstPart { get; }

    public int[] PartCount { get; }

    public Guid[] PartTeamId { get; }

    public decimal[] PartEffort { get; }

    public int[] PartDemand { get; }

    /// <summary>Os pré-requisitos de cada demanda, por índice (todos; quem não está na simulação é ignorado).</summary>
    public int[][] Prerequisites { get; }

    /// <summary>As pessoas na ordem de id.</summary>
    public PersonInput[] People { get; }

    /// <summary>As demandas alocadas a cada pessoa, e, em paralelo, a parte do time dela em cada uma.</summary>
    public int[][] AssignedDemands { get; }

    public int[][] AssignedParts { get; }

    /// <summary>
    /// As horas que a pessoa pode trabalhar no dia, iguais a <see cref="PersonCapacityCalendar.CapacityOf"/>,
    /// calculadas na primeira vez que são pedidas e guardadas para as rodadas seguintes.
    /// </summary>
    public decimal CapacityOf(int person, int dayNumber)
    {
        var column = _capacityByDay[dayNumber - _baseDay];
        if (column is null)
        {
            column = new decimal[People.Length];
            Array.Fill(column, decimal.MinusOne); // -1 = ainda não calculada (a capacidade nunca é negativa)
            _capacityByDay[dayNumber - _baseDay] = column;
        }

        if (column[person] == decimal.MinusOne)
        {
            column[person] = _capacity.CapacityOf(People[person].Id, DateOnly.FromDayNumber(dayNumber));
        }

        return column[person];
    }
}
