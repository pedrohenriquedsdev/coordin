namespace Coordin.Api.Features.Reports;

/// <summary>
/// Relatório por time de um período (RF-17; DT-24). As datas são de calendário (`yyyy-MM-dd`); a ocupação é
/// nula quando a capacidade da semana é zero.
/// </summary>
public sealed record TeamReportResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<TeamReportTeamResponse> Teams);

/// <summary>Um time: as semanas em ordem e a quantidade de demandas não previsíveis que o envolvem.</summary>
public sealed record TeamReportTeamResponse(
    Guid TeamId,
    string TeamName,
    int UnpredictableDemandCount,
    IReadOnlyList<TeamReportWeekResponse> Weeks);

/// <summary>Uma semana completa (segunda a domingo) de um time.</summary>
public sealed record TeamReportWeekResponse(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal CapacityHours,
    decimal AllocatedHours,
    decimal? Occupancy,
    decimal IdleHours,
    bool IsOverloaded,
    IReadOnlyList<ReportDemandResponse> OverloadedDemands);

/// <summary>Uma demanda que deixou a fila do time acima da capacidade na semana.</summary>
public sealed record ReportDemandResponse(Guid Id, string Title);
