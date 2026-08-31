using FusionRpg.Core.Match;

namespace FusionRpg.Core.Hud;

/// <summary>Pure assembly of Hot read inputs into <see cref="ActorHudSnapshot"/>.</summary>
public static class ActorHudComposer
{
    public sealed record ActorHudComposeInput(
        bool IsUniquePlant,
        UniqueBindingPhase? BindingPhase,
        int? LevelBand,
        IReadOnlyList<ActorHudShieldStack>? ShieldStacks,
        long ShieldHp,
        long ShieldMax,
        IReadOnlyList<ActorHudStatusToken> StatusTokens,
        int StatusStripMax,
        bool HpSliverEnabled,
        double? HpSliverRatio = null);

    public static ActorHudSnapshot Compose(ActorHudComposeInput input)
    {
        var flags = new List<string>();
        if (input.IsUniquePlant)
            flags.Add("unique");

        var bound = input.BindingPhase == UniqueBindingPhase.Bound;
        var role = bound ? "specimen" : "vanilla";
        var tier = input.IsUniquePlant || bound ? ActorHudTier.Unique : ActorHudTier.Normal;

        ActorHudResources? resources = null;
        if (input.ShieldStacks is not null)
        {
            resources = new ActorHudResources(
                new ActorHudShield(input.ShieldHp, input.ShieldMax, input.ShieldStacks),
                input.HpSliverEnabled && input.HpSliverRatio is not null
                    ? new ActorHudHpSliver(input.HpSliverRatio.Value)
                    : null,
                null);
        }
        else if (input.HpSliverEnabled && input.HpSliverRatio is not null)
        {
            resources = new ActorHudResources(null, new ActorHudHpSliver(input.HpSliverRatio.Value), null);
        }

        var (visible, overflow) = ActorHudLayout.Prioritize(input.StatusTokens, input.StatusStripMax);

        return new ActorHudSnapshot(
            new ActorHudIdentity(tier, role, input.LevelBand, flags),
            resources,
            visible,
            new ActorHudOverflow(overflow));
    }
}
