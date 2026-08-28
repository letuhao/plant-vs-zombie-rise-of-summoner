namespace FusionRpg.Core.Stats.Derived;

/// <summary>
/// Reads <c>resource.max.{id}</c> / <c>resource.regen.{id}</c> (spec-action-costs.md §1) as
/// <c>long</c>. The composer stores every derived channel as a <c>double</c> internally, but a
/// resource pool is a magnitude a balance pass can push toward the overflow ceiling (CLAUDE.md
/// "Numeric overflow"), so the round-to-long happens here, once, at the boundary — the same point
/// <c>BattleRuleset.BaseHp</c>/<c>BattleChannelMod</c> already round at.
///
/// Lives outside <c>Core/Actions/</c> deliberately: <c>KernelPurityScan</c> bans a bare
/// <c>double</c> declaration in that tree so the tick-driving action layer can never pick up
/// floating-point drift, and this is the one place that boundary is crossed on purpose.
/// </summary>
public static class ResourceChannelReader
{
    public static long Max(ActorDerivedSnapshot snap, string resourceId) =>
        (long)Math.Round(snap.Get(DerivedStatChannels.ResourceMax(resourceId)), MidpointRounding.AwayFromZero);

    public static long RegenPerTick(ActorDerivedSnapshot snap, string resourceId) =>
        (long)Math.Round(snap.Get(DerivedStatChannels.ResourceRegen(resourceId)), MidpointRounding.AwayFromZero);
}
