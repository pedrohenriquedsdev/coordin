using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.Demands;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Planning;
using Coordin.Api.Features.Teams;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coordin.Api.Tests.Planning;

/// <summary>
/// Spec forecast-recalculation (RF-08 a RF-12; DT-03, DT-14, DT-27): as previsões gravadas batem com o
/// motor, a linha de base não muda, a concluída fica congelada e uma escrita que falha não grava nada.
/// Cada teste usa o próprio time e a própria pessoa, para não depender dos outros dados da classe.
/// </summary>
public class ForecastRecalculatorTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private static readonly DateOnly StartMonday = new(2026, 12, 7); // segunda-feira

    private static int _nextRank = 1000;

    private static int NextRank() => Interlocked.Increment(ref _nextRank);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Sobe a API (que aplica as migrations) e abre um escopo com o banco e o recalculador.</summary>
    private AsyncServiceScope NewScope()
    {
        using var client = factory.CreateClient();
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

    private static async Task<Demand> AddDemandAsync(
        CoordinDbContext db,
        Team team,
        Person? person,
        DateOnly? start,
        decimal? effort)
    {
        var rank = NextRank();
        var demand = new Demand { Title = Unique("Demanda"), PriorityRank = rank, ExecutionRank = rank, StartDate = start };
        db.Demands.Add(demand);
        db.DemandTeams.Add(new DemandTeam { DemandId = demand.Id, TeamId = team.Id, EstimatedEffortHours = effort });
        if (person is not null)
        {
            db.DemandAssignments.Add(new DemandAssignment { DemandId = demand.Id, PersonId = person.Id });
        }

        await db.SaveChangesAsync(Ct);
        return demand;
    }

    private static Task<Forecast> ForecastOfAsync(CoordinDbContext db, Guid demandId) =>
        db.Forecasts.AsNoTracking().SingleAsync(forecast => forecast.DemandId == demandId, Ct);

    [Fact]
    public async Task NewDemand_GetsAForecast_EqualToTheEngineOnTheSamePlan()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);

        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var stored = await ForecastOfAsync(db, demand.Id);
        Assert.True(stored.IsPredictable);
        Assert.Equal(StartMonday, stored.ForecastStartDate);
        Assert.Equal(new DateOnly(2026, 12, 8), stored.EndDateWithoutMargin);
        Assert.Equal(new DateOnly(2026, 12, 9), stored.ForecastEndDate); // 20% de 2 dias úteis = 1 dia
        Assert.Equal(stored.ForecastEndDate, stored.BaselineEndDate);

        // O mesmo planejamento, montado à mão, dá o mesmo resultado no motor.
        var byHand = ForecastEngine.Calculate(new PlanningSnapshot(
            [new TeamInput(team.Id)],
            [new PersonInput(person.Id, team.Id, 8m)],
            [],
            [],
            [new DemandInput(demand.Id, demand.PriorityRank, demand.ExecutionRank, StartMonday, false, [new DemandPartInput(team.Id, 16m)])],
            [new AssignmentInput(demand.Id, person.Id)],
            ExecutionOrderMode.FollowPriority,
            20m,
            []));
        Assert.Equal(byHand.Demands.Single(forecast => forecast.DemandId == demand.Id).ForecastEndDate, stored.ForecastEndDate);
    }

    [Fact]
    public async Task UnpredictableDemands_StoreTheirReason_WithoutDates()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var (zeroTeam, zeroPerson) = await AddTeamWithPersonAsync(db, 0m);
        var noStart = await AddDemandAsync(db, team, person, null, 16m);
        var noEstimate = await AddDemandAsync(db, team, person, StartMonday, null);
        var noCapacity = await AddDemandAsync(db, zeroTeam, zeroPerson, StartMonday, 16m);

        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var missingStart = await ForecastOfAsync(db, noStart.Id);
        Assert.False(missingStart.IsPredictable);
        Assert.Equal(UnpredictableReason.MissingStartDate.ToString(), missingStart.UnpredictableReason);
        Assert.Null(missingStart.ForecastEndDate);
        Assert.Equal(UnpredictableReason.MissingEstimate.ToString(), (await ForecastOfAsync(db, noEstimate.Id)).UnpredictableReason);
        Assert.Equal(UnpredictableReason.NoCapacity.ToString(), (await ForecastOfAsync(db, noCapacity.Id)).UnpredictableReason);
    }

    [Fact]
    public async Task MissingEstimateRegistered_MakesTheDemandPredictable_WithABaseline()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, null);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
        Assert.Null((await ForecastOfAsync(db, demand.Id)).BaselineEndDate);

        (await db.DemandTeams.SingleAsync(part => part.DemandId == demand.Id, Ct)).EstimatedEffortHours = 16m;
        await db.SaveChangesAsync(Ct);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var stored = await ForecastOfAsync(db, demand.Id);
        Assert.True(stored.IsPredictable);
        Assert.NotNull(stored.BaselineEndDate);
    }

    [Fact]
    public async Task Replanning_MovesTheForecast_ButNotTheBaseline()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
        var baseline = (await ForecastOfAsync(db, demand.Id)).BaselineEndDate;

        // Uma ausência na segunda empurra a execução: terça e quarta, e o fim com margem vai para quinta.
        db.Absences.Add(new Absence { PersonId = person.Id, Type = AbsenceType.Vacation, StartDate = StartMonday, EndDate = StartMonday });
        await db.SaveChangesAsync(Ct);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var stored = await ForecastOfAsync(db, demand.Id);
        Assert.Equal(new DateOnly(2026, 12, 9), stored.EndDateWithoutMargin);
        Assert.Equal(new DateOnly(2026, 12, 10), stored.ForecastEndDate);
        Assert.Equal(baseline, stored.BaselineEndDate);
    }

    [Fact]
    public async Task DemandThatLosesItsEstimate_KeepsItsBaseline()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
        var baseline = (await ForecastOfAsync(db, demand.Id)).BaselineEndDate;

        (await db.DemandTeams.SingleAsync(part => part.DemandId == demand.Id, Ct)).EstimatedEffortHours = null;
        await db.SaveChangesAsync(Ct);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var stored = await ForecastOfAsync(db, demand.Id);
        Assert.False(stored.IsPredictable);
        Assert.Equal(UnpredictableReason.MissingEstimate.ToString(), stored.UnpredictableReason);
        Assert.Equal(baseline, stored.BaselineEndDate);
    }

    [Fact]
    public async Task CompletedDemand_KeepsItsFrozenForecast()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
        var before = await ForecastOfAsync(db, demand.Id);

        var tracked = await db.Demands.SingleAsync(item => item.Id == demand.Id, Ct);
        tracked.CompletedOn = new DateOnly(2026, 12, 9);
        (await db.People.SingleAsync(item => item.Id == person.Id, Ct)).DailyCapacityHours = 1m;
        await db.SaveChangesAsync(Ct);
        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);

        var after = await ForecastOfAsync(db, demand.Id);
        Assert.Equal(before.ForecastEndDate, after.ForecastEndDate);
        Assert.Equal(before.BaselineEndDate, after.BaselineEndDate);
        Assert.Equal(before.CalculatedAt, after.CalculatedAt);
    }

    [Fact]
    public async Task CompletedDemand_WhosePersonChangedTeam_DoesNotBreakTheRecalculation()
    {
        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
        var (team, person) = await AddTeamWithPersonAsync(db, 8m);
        var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
        db.Demands.Single(item => item.Id == demand.Id).CompletedOn = new DateOnly(2026, 12, 9);
        await db.SaveChangesAsync(Ct);
        var otherTeam = Team.Create(Unique("Outro"));
        db.Teams.Add(otherTeam);
        await db.SaveChangesAsync(Ct);
        (await db.People.SingleAsync(item => item.Id == person.Id, Ct)).TeamId = otherTeam.Id;
        await db.SaveChangesAsync(Ct);

        await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct); // não pode lançar

        // A concluída nunca foi previsível, então não ganha previsão nova.
        Assert.False(await db.Forecasts.AsNoTracking().AnyAsync(forecast => forecast.DemandId == demand.Id, Ct));
    }

    [Fact]
    public async Task RejectedWrite_LeavesTheStoredForecastAndTheDataAsTheyWere()
    {
        Guid demandId;
        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            var (team, person) = await AddTeamWithPersonAsync(db, 8m);
            var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
            await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
            demandId = demand.Id;
        }

        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            var result = await PlanningTransaction.RunAsync(db, recalculator, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, async () =>
            {
                var demand = await db.Demands.SingleAsync(item => item.Id == demandId, Ct);
                demand.StartDate = null;
                await db.SaveChangesAsync(Ct);
                return Results.BadRequest();
            }, Ct);
            Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest>(result);
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var readDb = verify.ServiceProvider.GetRequiredService<CoordinDbContext>();
        Assert.Equal(StartMonday, (await readDb.Demands.AsNoTracking().SingleAsync(item => item.Id == demandId, Ct)).StartDate);
        var forecast = await ForecastOfAsync(readDb, demandId);
        Assert.True(forecast.IsPredictable);
        Assert.Equal(new DateOnly(2026, 12, 9), forecast.ForecastEndDate);
    }

    [Fact]
    public async Task WriteThatThrows_LeavesEverythingAsItWas()
    {
        Guid demandId;
        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            var (team, person) = await AddTeamWithPersonAsync(db, 8m);
            var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
            await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
            demandId = demand.Id;
        }

        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => PlanningTransaction.RunAsync(db, recalculator, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, () =>
            {
                db.Demands.Single(item => item.Id == demandId).Title = "Título que não deve ficar";
                throw new InvalidOperationException("falha simulada");
            }, Ct));
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var readDb = verify.ServiceProvider.GetRequiredService<CoordinDbContext>();
        Assert.NotEqual("Título que não deve ficar", (await readDb.Demands.AsNoTracking().SingleAsync(item => item.Id == demandId, Ct)).Title);
    }

    [Fact]
    public async Task SuccessfulWrite_IsFollowedByARecalculation_AndCommitted()
    {
        Guid demandId;
        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            var (team, person) = await AddTeamWithPersonAsync(db, 8m);
            var demand = await AddDemandAsync(db, team, person, StartMonday, 16m);
            await recalculator.RecalculateAsync(db, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, Ct);
            demandId = demand.Id;
        }

        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            var recalculator = scope.ServiceProvider.GetRequiredService<ForecastRecalculator>();
            var result = await PlanningTransaction.RunAsync(db, recalculator, Coordin.Engine.Forecasting.ForecastChangeCause.Estimate, async () =>
            {
                (await db.DemandTeams.SingleAsync(part => part.DemandId == demandId, Ct)).EstimatedEffortHours = 32m;
                await db.SaveChangesAsync(Ct);
                return Results.Ok();
            }, Ct);
            Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var readDb = verify.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var forecast = await ForecastOfAsync(readDb, demandId);
        // 32 h a 8 h por dia: quatro dias úteis (segunda a quinta); 20% de 4 dias = 1 dia de margem.
        Assert.Equal(new DateOnly(2026, 12, 10), forecast.EndDateWithoutMargin);
        Assert.Equal(new DateOnly(2026, 12, 11), forecast.ForecastEndDate);
    }
}
