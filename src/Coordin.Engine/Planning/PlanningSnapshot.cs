namespace Coordin.Engine.Planning;

/// <summary>
/// Retrato imutável do planejamento que o motor recebe (DT-02): tudo o que o cálculo precisa e nada
/// além disso. A API monta o snapshot a partir do banco; o motor nunca acessa banco, relógio ou rede.
/// </summary>
/// <param name="Teams">Os times do setor.</param>
/// <param name="People">As pessoas, cada uma em um único time, com a capacidade em horas por dia útil.</param>
/// <param name="Absences">Períodos em que uma pessoa não trabalha.</param>
/// <param name="Holidays">Datas em que ninguém trabalha.</param>
/// <param name="Demands">As demandas, com as partes por time (DT-11).</param>
/// <param name="Assignments">Quem está alocado a qual demanda (DT-12), sem percentual.</param>
/// <param name="OrderMode">Qual ranking forma a fila de execução (DT-13).</param>
/// <param name="SafetyMarginPercent">Percentual da margem de segurança, sempre maior ou igual a zero (DT-22).</param>
public sealed record PlanningSnapshot(
    IReadOnlyList<TeamInput> Teams,
    IReadOnlyList<PersonInput> People,
    IReadOnlyList<AbsenceInput> Absences,
    IReadOnlyList<HolidayInput> Holidays,
    IReadOnlyList<DemandInput> Demands,
    IReadOnlyList<AssignmentInput> Assignments,
    ExecutionOrderMode OrderMode,
    decimal SafetyMarginPercent);

/// <summary>Um time do setor.</summary>
public sealed record TeamInput(Guid Id);

/// <summary>Uma pessoa, em um único time, com a capacidade em horas por dia útil (de 0 a 24).</summary>
public sealed record PersonInput(Guid Id, Guid TeamId, decimal DailyHours);

/// <summary>Período de ausência de uma pessoa, com as duas pontas incluídas.</summary>
public sealed record AbsenceInput(Guid PersonId, DateOnly StartDate, DateOnly EndDate);

/// <summary>Um feriado do setor.</summary>
public sealed record HolidayInput(DateOnly Date);

/// <summary>
/// Uma demanda. <see cref="StartDate"/> ausente torna a demanda não previsível (RF-10). Uma demanda
/// concluída não é prevista e não consome capacidade.
/// </summary>
public sealed record DemandInput(
    Guid Id,
    int PriorityRank,
    int ExecutionRank,
    DateOnly? StartDate,
    bool IsCompleted,
    IReadOnlyList<DemandPartInput> Parts);

/// <summary>
/// A parte de um time em uma demanda. <see cref="EstimatedEffortHours"/> ausente significa
/// "o time ainda não estimou" (regra inviolável 5); quando informado, é maior que zero.
/// </summary>
public sealed record DemandPartInput(Guid TeamId, decimal? EstimatedEffortHours);

/// <summary>Uma pessoa alocada a uma demanda.</summary>
public sealed record AssignmentInput(Guid DemandId, Guid PersonId);
