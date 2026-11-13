using Coordin.Engine.Capacity;

namespace Coordin.Engine.Tests.Capacity;

/// <summary>Regra de capacidade do time (RF-02, DT-10): a soma da capacidade das pessoas.</summary>
public class TeamCapacityTests
{
    [Fact]
    public void NoPeople_HasZeroCapacity()
    {
        Assert.Equal(0m, TeamCapacity.DailyHours([]));
    }

    [Fact]
    public void SeveralPeople_AreSummed()
    {
        Assert.Equal(20m, TeamCapacity.DailyHours([8m, 6m, 6m]));
    }

    [Fact]
    public void APersonWithZeroCapacity_DoesNotContribute()
    {
        Assert.Equal(8m, TeamCapacity.DailyHours([8m, 0m]));
    }

    [Fact]
    public void OnlyPeopleWithZeroCapacity_MeansATeamWithoutCapacity()
    {
        Assert.Equal(0m, TeamCapacity.DailyHours([0m, 0m, 0m]));
    }

    [Fact]
    public void DecimalHours_AreSummedWithoutRoundingErrors()
    {
        // 0,1 + 0,2 é o caso clássico em que double erra; decimal não.
        Assert.Equal(0.3m, TeamCapacity.DailyHours([0.1m, 0.2m]));
        Assert.Equal(0.75m, TeamCapacity.DailyHours([0.5m, 0.25m]));
    }

    [Fact]
    public void TheOrderOfThePeople_DoesNotMatter()
    {
        Assert.Equal(
            TeamCapacity.DailyHours([8m, 6m, 4.5m]),
            TeamCapacity.DailyHours([4.5m, 8m, 6m]));
    }

    [Fact]
    public void ANegativeCapacity_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamCapacity.DailyHours([8m, -1m]));
    }

    [Fact]
    public void ANullList_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => TeamCapacity.DailyHours(null!));
    }
}
