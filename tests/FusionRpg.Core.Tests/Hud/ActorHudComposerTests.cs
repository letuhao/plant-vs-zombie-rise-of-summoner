using FusionRpg.Core.Hud;
using FusionRpg.Core.Match;
using Xunit;

namespace FusionRpg.Core.Tests.Hud;

public sealed class ActorHudComposerTests
{
    static ActorHudStatusToken Tok(string id, bool cc = false, MagnitudeBand band = MagnitudeBand.Low) =>
        new(id, cc, band);

    [Fact]
    public void Compose_cc_priority_and_overflow()
    {
        var input = new ActorHudComposer.ActorHudComposeInput(
            IsUniquePlant: false,
            BindingPhase: null,
            LevelBand: null,
            ShieldStacks: null,
            ShieldHp: 0,
            ShieldMax: 0,
            StatusTokens: new[]
            {
                Tok("expose"),
                Tok("freeze", cc: true),
                Tok("command"),
                Tok("spark"),
            },
            StatusStripMax: 2,
            HpSliverEnabled: false);

        var snap = ActorHudComposer.Compose(input);

        Assert.Equal(2, snap.Statuses.Count);
        Assert.Equal("freeze", snap.Statuses[0].Id);
        Assert.Equal(2, snap.Overflow.StatusCount);
    }

    [Fact]
    public void Compose_omits_levelBand_when_null()
    {
        var snap = ActorHudComposer.Compose(new ActorHudComposer.ActorHudComposeInput(
            false, null, null, null, 0, 0,
            Array.Empty<ActorHudStatusToken>(), 3, false));

        Assert.Null(snap.Identity.LevelBand);
        var wire = ActorHudWireSerializer.ToDictionary(snap);
        var identity = Assert.IsType<Dictionary<string, object>>(wire["identity"]);
        Assert.False(identity.ContainsKey("levelBand"));
    }

    [Fact]
    public void Compose_never_emits_boss_tier()
    {
        var snap = ActorHudComposer.Compose(new ActorHudComposer.ActorHudComposeInput(
            false, UniqueBindingPhase.Bound, 42,
            Array.Empty<ActorHudShieldStack>(), 0, 0,
            Array.Empty<ActorHudStatusToken>(), 3, false));

        Assert.NotEqual(ActorHudTier.Boss, snap.Identity.Tier);
        Assert.Equal("specimen", snap.Identity.Role);
    }

    [Fact]
    public void Compose_unique_plant_flag_and_tier()
    {
        var snap = ActorHudComposer.Compose(new ActorHudComposer.ActorHudComposeInput(
            true, null, 5,
            null, 0, 0,
            Array.Empty<ActorHudStatusToken>(), 3, false));

        Assert.Equal(ActorHudTier.Unique, snap.Identity.Tier);
        Assert.Contains("unique", snap.Identity.Flags);
    }

    [Fact]
    public void Compose_shield_gate_wired_emits_zero_totals()
    {
        var snap = ActorHudComposer.Compose(new ActorHudComposer.ActorHudComposeInput(
            false, null, null,
            Array.Empty<ActorHudShieldStack>(), 0, 0,
            Array.Empty<ActorHudStatusToken>(), 3, false));

        Assert.NotNull(snap.Resources);
        Assert.NotNull(snap.Resources!.Shield);
        Assert.Equal(0, snap.Resources.Shield!.Hp);
    }

    [Fact]
    public void Build_status_priority_uses_layout_prioritize()
    {
        var snap = ActorHudComposer.Compose(new ActorHudComposer.ActorHudComposeInput(
            false, null, null, null, 0, 0,
            new[]
            {
                Tok("expose"),
                Tok("freeze", cc: true),
                Tok("command"),
            },
            StatusStripMax: 2,
            HpSliverEnabled: false));

        Assert.Equal("freeze", snap.Statuses[0].Id);
        Assert.Equal(1, snap.Overflow.StatusCount);
    }
}
