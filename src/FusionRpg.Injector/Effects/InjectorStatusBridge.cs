using FusionRpg.Core.Combat;
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

        var board = InjectorBoardSnapshot.Capture();
        var side = "plant";
        var typeId = 0;
        foreach (var e in board.Entities)
        {
            if (!CombatPtr.EqualsPtr(e.Ptr, key)) continue;
            side = e.Side ?? "plant";
            typeId = e.TypeId;
            break;
        }

        if (typeId == 0
            && CheatState.SelectedPtr != IntPtr.Zero
            && CombatPtr.EqualsPtr(CheatState.SelectedPtr.ToString("X"), key)
            && !string.IsNullOrWhiteSpace(CheatState.SelectedSide))
            side = CheatState.SelectedSide;

        var ctx = string.Equals(side, "zombie", StringComparison.OrdinalIgnoreCase)
            ? hub.Stats.Contexts.ForZombie(
                key,
                baseline,
                typeId,
                matchKey: GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: CheatState.EffectiveStats(),
                pvzStatsMods: CheatState.PvzStatsMods)
            : hub.Stats.Contexts.ForPlant(
                key,
                baseline,
                typeId,
                matchKey: GameHooks.MatchKey,
                playerId: CheatState.PvzStatsPlayerId > 0 ? CheatState.PvzStatsPlayerId : null,
                cheatScale: CheatState.EffectiveStats(),
                pvzStatsMods: CheatState.PvzStatsMods);

        return hub.ResolveDerived(ctx);
    }
}
