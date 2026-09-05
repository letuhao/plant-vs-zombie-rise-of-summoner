using FusionRpg.Core.Hud;
using Xunit;

namespace FusionRpg.Core.Tests.Hud;

public sealed class ActorHudDisplayTokensTests
{
    [Theory]
    [InlineData("command", "CO")]
    [InlineData("expose", "EX")]
    [InlineData("pact_mark", "PM")]
    [InlineData("spark", "SP")]
    public void StatusInitials_matches_phaser_rules(string id, string expect) =>
        Assert.Equal(expect, ActorHudDisplayTokens.StatusInitials(id));

    [Theory]
    [InlineData(ActorHudTier.Elite, "E")]
    [InlineData(ActorHudTier.Boss, "B")]
    [InlineData(ActorHudTier.Unique, "U")]
    [InlineData(ActorHudTier.Normal, "")]
    public void TierLetter_enum(ActorHudTier tier, string expect) =>
        Assert.Equal(expect, ActorHudDisplayTokens.TierLetter(tier));

    [Theory]
    [InlineData("elite", "E")]
    [InlineData("boss", "B")]
    [InlineData("unique", "U")]
    [InlineData("normal", "")]
    public void TierLetter_string(string tier, string expect) =>
        Assert.Equal(expect, ActorHudDisplayTokens.TierLetter(tier));

    [Theory]
    [InlineData(null, "?")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    [InlineData("x", "X")]
    [InlineData("ab", "AB")]
    public void StatusInitials_empty_and_short(string? id, string expect) =>
        Assert.Equal(expect, ActorHudDisplayTokens.StatusInitials(id));
}
