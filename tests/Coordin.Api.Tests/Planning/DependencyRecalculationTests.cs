using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Planning;

/// <summary>
/// O carregador do snapshot lê as dependências gravadas, e o recálculo grava a previsão da dependente
/// depois do fim previsto, com margem, do pré-requisito (RF-18; DT-23). Cada teste usa o próprio time
/// e as próprias pessoas, como em <see cref="ForecastRecalculatorTests"/>.
/// </summary>
public class DependencyRecalculationTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private static readonly DateOnly StartMonday = new(2027, 2, 1); // segunda-feira

    private static int _nextRank = 3000;

    private static int NextRank() => Interlocked.Increment(ref _nextRank);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AsyncServiceScope NewScope()
    {
        using var client = factory.CreateClient(); // sobe a API e aplica as migrations
        return factory.Services.CreateAsyncScope();
    }

    private static async Task<(Team Team, Person Person)> AddTeamWithPersonAsync(CoordinDbContext db, decimal hours = 8m)
    {
        var team = Team.Create(Unique("Time"));
        db.Teams.Add(team);
        var person = new Person { Name = Unique("Pessoa"), TeamId = team.Id, DailyCapacityHours = hours };
        db.People.Add(person);
        await db.SaveChangesAsync(Ct);
        return (team, person);
    }

    private static async Task<Demand> AddDemandAsync(CoordinDbContext db, Team team, Person person, DateOnly start, decimal effort)
    {
        var rank = NextRank();
        var demand = new Demand { Title = Unique("Demanda"), PriorityRank = rank, ExecutionRank = rank, StartDate = start };
        db.Demands.Add(demand);
        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = effort });
        db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = person.Id });
        await db.SaveChangesAsync(Ct);
        return demand;
    }

    private static Task<Forecast> ForecastOfAsync(CoordinDbContext db, Guid demandId) =>
        db.Forecasts.AsNoTracking().SingleAsync(forecast => forecast.DemandId == demandId, Ct);

    [Fact]
    public async Task LoaderReadsTheStoredDependencies_AndTheDependentStartsAfterThePrerequisiteEndsWithMargin()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var loader = scope.ServiceProvider.GetRequiredService<PlanningSnapshotLoader>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var a = await AddDemandAsync(db, team, person, StartMonday, 16m); // dois dias úteis
        var b = await AddDemandAsync(db, team, person, StartMonday, 8m); // mesmo início registrado
        db.DemandDependencies.Add(new DemandDependency { DemandId = b.Id, PrerequisiteDemandId = a.Id });
        await db.SaveChangesAsync(Ct);

        var snapshot = await loader.LoadAsync(db, Ct);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        Assert.Contains(snapshot.Dependencies, dependency => dependency.DemandId == b.Id && dependency.PrerequisiteId == a.Id);
        var prerequisite = await ForecastOfAsync(db, a.Id);
        var dependent = await ForecastOfAsync(db, b.Id);
        Assert.True(prerequisite.IsPredictable);
        Assert.True(dependent.IsPredictable);
        Assert.True(dependent.ForecastStartDate > prerequisite.ForecastEndDate, "A dependente começou antes do fim com margem do pré-requisito.");
    }

    [Fact]
    public async Task DependencyOnAnUnpredictablePrerequisite_StoresPrerequisiteUnpredictable()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var a = await AddDemandAsync(db, team, person, StartMonday, 8m);
        var b = await AddDemandAsync(db, team, person, StartMonday, 8m);
        db.DemandTeams.Single(part => part.DemandId == a.Id).EstimatedEffortHours = null; // falta a estimativa de A
        db.DemandDependencies.Add(new DemandDependency { DemandId = b.Id, PrerequisiteDemandId = a.Id });
        await db.SaveChangesAsync(Ct);

        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var dependent = await ForecastOfAsync(db, b.Id);
        Assert.False(dependent.IsPredictable);
        Assert.Equal("PrerequisiteUnpredictable", dependent.UnpredictableReason);
        Assert.Null(dependent.ForecastStartDate);
    }
}
