using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Validation;
using Coordin.Api.Infrastructure.Persistence;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// As tabelas da validação (DT-37; RT-11): a linha de base some com a demanda, e o planejamento do processo atual é um
/// registro único. As restrições são testadas com SQL direto, para o banco ser quem recusa (lição da change 8).
/// </summary>
public sealed class ValidationStorageTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateDemandAsync(HttpClient client, string title)
    {
        var team = await client.NewTeamAsync();
        var person = await client.NewPersonAsync(team);
        var response = await client.PostAsJsonAsync(
            "/api/demands",
            PlanningTestData.Body(title, [(team, 8m)], [person], PlanningTestData.StartMonday),
            Ct);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Deleting_a_demand_removes_its_baseline()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var demandId = await CreateDemandAsync(client, "Com linha de base");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
            db.ValidationBaselines.Add(new ValidationBaseline { DemandId = demandId, BaselineDate = PlanningTestData.StartMonday.AddDays(10) });
            await db.SaveChangesAsync(Ct);
        }

        var delete = await client.DeleteAsync($"/api/demands/{demandId}", Ct);

        Assert.True(delete.IsSuccessStatusCode, $"a exclusão falhou com {(int)delete.StatusCode}");
        using var check = factory.Services.CreateScope();
        var remaining = await check.ServiceProvider.GetRequiredService<CoordinDbContext>()
            .ValidationBaselines.CountAsync(baseline => baseline.DemandId == demandId, Ct);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task The_database_refuses_a_second_row_in_the_current_plan_table()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ValidationCurrentPlans\"", Ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"ValidationCurrentPlans\" (\"Id\", \"SnapshotJson\", \"CapturedAt\") VALUES (1, '\"x\"', now())", Ct);

        // O registro único é garantido pelo banco: um segundo Id é recusado pela restrição CHECK.
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"ValidationCurrentPlans\" (\"Id\", \"SnapshotJson\", \"CapturedAt\") VALUES (2, '\"x\"', now())", Ct));

        Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    public async Task The_database_refuses_two_baselines_for_the_same_demand()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();
        var demandId = await CreateDemandAsync(client, "Linha de base dupla");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        var insert = $"INSERT INTO \"ValidationBaselines\" (\"DemandId\", \"BaselineDate\") VALUES ('{demandId}', '2027-02-10')";
        await db.Database.ExecuteSqlRawAsync(insert, Ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(insert, Ct));

        Assert.Equal("23505", error.SqlState);
    }
}
