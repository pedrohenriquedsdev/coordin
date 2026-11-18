using Coordin.Api.Features.Validation;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// O planejamento guardado (DT-37): o texto volta ao mesmo planejamento e ao mesmo cálculo, e o enum sai como texto.
/// </summary>
public sealed class ValidationSnapshotJsonTests
{
    private static readonly Guid Team = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Person = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Demand = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PlanningSnapshot Snapshot() => new(
        [new TeamInput(Team)],
        [new PersonInput(Person, Team, 8m)],
        [new AbsenceInput(Person, new DateOnly(2027, 1, 11), new DateOnly(2027, 1, 12))],
        [new HolidayInput(new DateOnly(2027, 1, 18))],
        [new DemandInput(Demand, 1, 1, new DateOnly(2027, 1, 4), false, [new DemandPartInput(Team, 16m)])],
        [new AssignmentInput(Demand, Person)],
        ExecutionOrderMode.FollowPriority,
        20m,
        []);

    [Fact]
    public void Serializing_twice_gives_the_same_text()
    {
        var first = ValidationSnapshotJson.Serialize(Snapshot());

        var second = ValidationSnapshotJson.Serialize(ValidationSnapshotJson.Deserialize(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void The_enum_is_written_as_text_and_the_dates_as_iso()
    {
        var json = ValidationSnapshotJson.Serialize(Snapshot());

        Assert.Contains("\"FollowPriority\"", json, StringComparison.Ordinal);
        Assert.Contains("\"2027-01-04\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_restored_planning_gives_the_same_forecast()
    {
        var original = Snapshot();
        var restored = ValidationSnapshotJson.Deserialize(ValidationSnapshotJson.Serialize(original));

        // Listas em records comparam por referência: compara-se o conteúdo de cada previsão.
        Assert.Equal(ForecastEngine.Calculate(original).Demands, ForecastEngine.Calculate(restored).Demands);
    }
}
