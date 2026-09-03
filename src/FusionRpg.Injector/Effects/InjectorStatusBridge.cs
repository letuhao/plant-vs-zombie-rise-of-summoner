using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector.Effects;

/// <summary>Bridge ActorHub derived resolve for StatusRuntime L2b.</summary>
public static class InjectorStatusBridge
{
    public static StatusRuntime CreateRuntime()
    {
        return new StatusRuntime(
            StatusCatalogBootstrap.CreateDefault(),
            ResolveDerived);
    }

    public static ActorDerivedSnapshot ResolveDerived(string? entityPtr, bool attackerLess)
    {
        if (attackerLess || string.IsNullOrWhiteSpace(entityPtr))
            return ActorDerivedSnapshot.AttackerLess();

        var key = entityPtr.Trim();
        if (InjectorDerivedOverride.TryGet(key, out var pinned))
            return pinned;

        var hub = CheatState.ActorHub;
        if (!hub.Stats.TryGetBaseline(key, out var baseline)
            && !hub.Stats.TryGetBaseline(key.ToUpperInvariant(), out baseline))
            baseline = new EntityBaseline { Hp = 100, MaxHp = 100, Atk = 10 };

        // E27 (spec-lawn-element-bind.md §2.4): the shared LawnElementResolverHost replaces this
        // bridge's own board scan — a cache hit here is free when InjectorCombatBridge already
        // resolved the same ptr for the same match, and either bridge's first call for a ptr warms it
        // for the other.
        var (side, typeId, elementTypes) = LawnElementResolverHost.Resolve(key);

        var ctx = string.Equals(side, "zombie", StringComparison.OrdinalIgnoreCase)
            ? hub.Stats.Contexts.ForZombie(
                key,
                baseline,
                typeId,
                matchKey: GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: CheatState.EffectiveStats(),
                pvzStatsMods: CheatState.PvzStatsMods,
                elementTypes: elementTypes)
            : hub.Stats.Contexts.ForPlant(
                key,
                baseline,
                typeId,
                matchKey: GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: CheatState.EffectiveStats(),
                pvzStatsMods: CheatState.PvzStatsMods,
                elementTypes: elementTypes);

        return hub.ResolveDerived(ctx);
    }
}
