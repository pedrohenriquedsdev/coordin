using System.Globalization;
using System.Text.Json;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Dados e corpos de demanda para os testes do planejamento.</summary>
public static class PlanningTestData
{
    /// <summary>Uma segunda-feira: início das demandas de teste.</summary>
    public static readonly DateOnly StartMonday = new(2027, 1, 4);

    /// <summary>Lê uma data de calendário no formato da API (yyyy-MM-dd), sem depender da cultura.</summary>
    public static DateOnly ParseDate(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Apaga todas as demandas (e, em cascata, partes, alocações e previsões) da classe de teste.</summary>
    public static async Task ClearDemandsAsync(CoordinApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        await db.Demands.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Corpo de <c>POST /api/demands</c> com os times (e estimativas) e as pessoas alocadas.</summary>
    public static object Body(
        string title,
        IEnumerable<(Guid TeamId, decimal? Hours)> teams,
        IEnumerable<Guid>? people = null,
        DateOnly? start = null,
        DateOnly? informedEnd = null) => new
        {
            title,
            startDate = start,
            informedEndDate = informedEnd,
            teams = teams.Select(part => new { teamId = part.TeamId, estimatedEffortHours = part.Hours }).ToArray(),
            assignedPersonIds = (people ?? []).ToArray(),
        };
}
