using System.Globalization;
using Coordin.Api.Features.Planning;
using Coordin.Api.Infrastructure.Http;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Reports;

/// <summary>
/// Relatório por time (RF-17; DT-24). Calcula sob demanda sobre o planejamento vigente e não grava nada:
/// só lê o planejamento e devolve o resultado. Nenhum endpoint declara regra de autorização: a política
/// padrão exige o gestor autenticado.
/// </summary>
public static class ReportEndpoints
{
    /// <summary>Limite de período (DT-18): mais que isso é recusado, para a consulta continuar leve.</summary>
    private const int MaxWeeks = 52;

    private const string DateFormat = "yyyy-MM-dd";

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports");

        group.MapGet("/teams", GetTeamReportAsync)
            .WithName("GetTeamReport")
            .WithSummary("Relatório por time no período: capacidade, horas alocadas, ocupação, ociosidade e sobrecarga por semana.")
            .Produces<TeamReportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetTeamReportAsync(
        string? from,
        string? to,
        CoordinDbContext db,
        PlanningSnapshotLoader loader,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(from, out var fromDate))
        {
            return ApiProblems.InvalidField("from", "Informe a data de início no formato aaaa-mm-dd.");
        }

        if (!TryParseDate(to, out var toDate))
        {
            return ApiProblems.InvalidField("to", "Informe a data de fim no formato aaaa-mm-dd.");
        }

        if (toDate < fromDate)
        {
            return ApiProblems.InvalidField("to", "A data de fim não pode ser anterior à data de início.");
        }

        var (start, end) = TeamReport.AlignedPeriod(fromDate, toDate);
        if ((end.DayNumber - start.DayNumber + 1) / 7 > MaxWeeks)
        {
            return ApiProblems.InvalidField("to", $"O período pode ter no máximo {MaxWeeks} semanas.");
        }

        var snapshot = await loader.LoadAsync(db, cancellationToken);
        var report = TeamReport.Build(snapshot, ForecastEngine.Calculate(snapshot), fromDate, toDate);

        var teamNames = await db.Teams.AsNoTracking().ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);
        var demandTitles = await db.Demands.AsNoTracking().ToDictionaryAsync(demand => demand.Id, demand => demand.Title, cancellationToken);

        // Times em ordem alfabética, sem diferenciar maiúsculas (como em TeamEndpoints).
        var teams = report.Teams
            .OrderBy(team => teamNames[team.TeamId].ToUpperInvariant(), StringComparer.Ordinal)
            .Select(team => new TeamReportTeamResponse(
                team.TeamId,
                teamNames[team.TeamId],
                team.UnpredictableDemandCount,
                team.Weeks.Select(week => new TeamReportWeekResponse(
                    week.WeekStart,
                    week.WeekEnd,
                    week.CapacityHours,
                    week.AllocatedHours,
                    week.Occupancy,
                    week.IdleHours,
                    week.IsOverloaded,
                    week.OverloadedDemandIds
                        .Select(id => new ReportDemandResponse(id, demandTitles[id]))
                        .ToList())).ToList()))
            .ToList();

        return Results.Ok(new TeamReportResponse(report.PeriodStart, report.PeriodEnd, teams));
    }

    private static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
