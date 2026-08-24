using FusionRpg.Core.Activity;
using FusionRpg.Core.Progression;
using Xunit;

namespace FusionRpg.Core.Tests;

public class RpgXpAwardMapTests
{
    // [InlineData] requires compile-time constants, so RpgXpAwards' config-backed properties
    // (data/tuning/progression.v1.json) can't sit here directly — literals mirror its current
    // awards.kill/mower/plantPlace/zombieSpawn; Kill_award_uses_power_scale_one below asserts the
    // live value instead.
    [Theory]
    [InlineData(PvzActivityKinds.ZombieKilled, null, null, RpgActorKinds.Player, 0, 12.0, RpgXpReasons.Kill)]
    [InlineData(PvzActivityKinds.MowerUsed, null, null, RpgActorKinds.Player, 0, -30.0, RpgXpReasons.Mower)]
    [InlineData(PvzActivityKinds.PlantPlaced, null, 7, RpgActorKinds.Plant, 7, 8.0, RpgXpReasons.PlantPlace)]
    [InlineData(PvzActivityKinds.ZombieSpawned, null, 3, RpgActorKinds.Zombie, 3, 9.0, RpgXpReasons.ZombieSpawn)]
    public void Maps_activity_to_single_award(
        string kind, string? result, int? typeId,
        string expectKind, int expectTypeId, double expectDelta, string expectReason)
    {
        var awards = RpgXpAwardMap.FromActivity(kind, result, typeId);
        var a = Assert.Single(awards);
        Assert.Equal(expectKind, a.Kind);
        Assert.Equal(expectTypeId, a.TypeId);
        Assert.Equal(expectDelta, a.Delta);
        Assert.Equal(expectReason, a.Reason);
        Assert.Equal(1.0, a.PowerScale);
    }

    [Fact]
    public void Kill_award_uses_power_scale_one()
    {
        var a = Assert.Single(RpgXpAwardMap.FromActivity(PvzActivityKinds.ZombieKilled, null, 3, """{"type":3}"""));
        Assert.Equal(1.0, a.PowerScale);
        Assert.Equal(RpgXpAwards.Kill, a.Delta);
    }

    [Theory]
    [InlineData("defeat")]
    [InlineData("lose")]
    [InlineData("loss")]
    [InlineData("lost")]
    public void MatchEnded_defeat_aliases_award_defeat(string result)
    {
        var a = Assert.Single(RpgXpAwardMap.FromActivity(PvzActivityKinds.MatchEnded, result, null));
        Assert.Equal(RpgXpAwards.Defeat, a.Delta);
        Assert.Equal(RpgXpReasons.Defeat, a.Reason);
    }

    [Theory]
    [InlineData("victory")]
    [InlineData("win")]
    [InlineData(null)]
    public void MatchEnded_non_defeat_no_award(string? result)
    {
        Assert.Empty(RpgXpAwardMap.FromActivity(PvzActivityKinds.MatchEnded, result, null));
    }

    [Fact]
    public void Unknown_kind_empty()
    {
        Assert.Empty(RpgXpAwardMap.FromActivity("Nope", null, null));
    }
}
