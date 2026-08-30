using FusionRpg.Core.Commanders;
using Xunit;

namespace FusionRpg.Core.Tests.Commanders;

public class PlayerEmpireCommandersTests
{
    [Fact]
    public void ForPlayer_returns_Dave_for_positive_player_id()
    {
        var roster = PlayerEmpireCommanders.ForPlayer(1);
        Assert.Single(roster);
        Assert.Equal(CommanderId.Dave, roster[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForPlayer_returns_empty_for_non_positive_player_id(long playerId)
    {
        Assert.Empty(PlayerEmpireCommanders.ForPlayer(playerId));
    }

    [Fact]
    public void IsPlayerDefaultAllowed_allows_Dave_only()
    {
        Assert.True(PlayerEmpireCommanders.IsPlayerDefaultAllowed(CommanderId.Dave));
        Assert.False(PlayerEmpireCommanders.IsPlayerDefaultAllowed(CommanderId.Zomboss));
    }

    [Fact]
    public void DisplayName_maps_Dave_to_player_facing_copy()
    {
        Assert.Equal("Crazy Dave", PlayerEmpireCommanders.DisplayName(CommanderId.Dave));
    }
}
