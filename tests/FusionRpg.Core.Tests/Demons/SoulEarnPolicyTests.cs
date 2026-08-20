using FusionRpg.Core.Demons;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>Golden table for the v2 earn policy — owner-approved numbers, change = ask-first.</summary>
public class SoulEarnPolicyTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 0)]  // the stall-farm cap
    [InlineData(500, 0)]
    public void Kill_earn_caps_at_50_per_match(int already, int expected)
    {
        Assert.Equal(expected, SoulEarnPolicy.KillEarn(already));
    }

    [Theory]
    [InlineData(true, 0, 100)]
    [InlineData(true, 2, 100)]
    [InlineData(true, 3, 50)]   // repeat decay after the 3rd win of the day
    [InlineData(true, 10, 50)]
    [InlineData(false, 0, 25)]
    [InlineData(false, 10, 25)]
    public void Match_end_earn_decays_repeat_victories(bool victory, int prior, int expected)
    {
        Assert.Equal(expected, SoulEarnPolicy.MatchEndEarn(victory, prior));
    }

    [Fact]
    public void A_full_stall_defeat_never_beats_a_clean_win()
    {
        // The exploit the cap exists to kill: 80-kill stall + defeat vs 30-kill fresh win.
        var stallDefeat = 50 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.MatchEndEarn(false, 0);
        var cleanWin = 30 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.MatchEndEarn(true, 0);
        Assert.True(cleanWin > stallDefeat, $"win {cleanWin} must beat stall-defeat {stallDefeat}");
    }

    [Theory]
    [InlineData(DemonRarity.Common, 25)]
    [InlineData(DemonRarity.Rare, 75)]
    [InlineData(DemonRarity.Epic, 200)]
    [InlineData(DemonRarity.Legendary, 500)]
    public void Discovery_faucet_by_rarity(DemonRarity rarity, int expected)
    {
        Assert.Equal(expected, SoulEarnPolicy.DiscoveryDelta(rarity));
    }
}
