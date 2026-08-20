using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector.Effects;

/// <summary>Bridge ActorHub resolve for overlay combat L2b.</summary>
public static class InjectorCombatBridge
{
    public static CombatActorSnapshot ResolveActor(string? entityPtr, bool attackerLess)
    {
        if (attackerLess || string.IsNullOrWhiteSpace(entityPtr))
            return CombatActorSnapshot.AttackerLess();

        var key = CombatPtr.Normalize(entityPtr);
        ActorDerivedSnapshot derived;
        if (InjectorDerivedOverride.TryGet(key, out var pinnedDerived))
            derived = pinnedDerived;
        else
            derived = InjectorStatusBridge.ResolveDerived(key, attackerLess: false);

        var elementTypes = InjectorElementOverride.TryGet(key, out var pinnedTypes)
            ? pinnedTypes
            : ResolveElementTypesFromHub(key);

        return new CombatActorSnapshot(derived, elementTypes);
    }

    static ActorElementTypes ResolveElementTypesFromHub(string key)
    {
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

        return hub.Resolve(ctx).ElementTypes;
    }

    public static void EmitOverlayBreakdown(
        OverlayCombatBreakdown breakdown,
        DamagePacket packet,
        string targetPtr,
        IReadOnlyDictionary<string, object>? extras = null)
    {
        // Prove-pack telemetry — outside a debug session every overlay hit paid this emit.
        if (!DebugRuntime.SessionActive) return;
        var source = "overlay";
        if (string.Equals(packet.PluginId, "debug", StringComparison.OrdinalIgnoreCase))
            source = "enqueue-delta";
        else if (!string.IsNullOrWhiteSpace(packet.SourceGrantId)
                 && packet.SourceGrantId.StartsWith("debug.", StringComparison.OrdinalIgnoreCase))
            source = packet.SourceGrantId;

        var dump = new Dictionary<string, object>
        {
            ["source"] = source,
            ["actorPtr"] = packet.ActorPtr ?? "",
            ["targetPtr"] = targetPtr,
            ["baseOverlayDamage"] = packet.SignedAmount < 0 ? -packet.SignedAmount : 0,
            ["matchupBonus"] = breakdown.MatchupBonus,
            ["weightedDelta"] = breakdown.WeightedDelta,
            ["powerAdjustedDamage"] = breakdown.PowerAdjustedDamage,
            ["hit"] = breakdown.Hit,
            ["crit"] = breakdown.Crit,
            ["pHitFinal"] = breakdown.PHitFinal,
            ["pCritFinal"] = breakdown.PCritFinal,
            ["critMultiplierFinal"] = breakdown.CritMultiplierFinal,
            ["finalSignedDelta"] = breakdown.FinalSignedDelta,
            ["elementPayload"] = packet.ElementPayload ?? new List<ElementPayloadComponentDto>()
        };

        if (!string.IsNullOrWhiteSpace(DebugRuntime.ScenarioId))
            dump["scenarioId"] = DebugRuntime.ScenarioId;

        if (extras != null)
        {
            foreach (var kv in extras)
                dump[kv.Key] = kv.Value;
        }

        // Enrich elements when not provided by probe extras.
        if (!dump.ContainsKey("defenderElements") && !string.IsNullOrWhiteSpace(targetPtr))
        {
            var def = ResolveActor(targetPtr, attackerLess: false);
            dump["defenderElements"] = new Dictionary<string, object>
            {
                ["primary"] = def.ElementTypes.Primary?.ToString() ?? "",
                ["secondary"] = def.ElementTypes.Secondary?.ToString() ?? "",
                ["neutral"] = def.ElementTypes.IsNeutral
            };
        }

        if (!dump.ContainsKey("attackerElements") && !string.IsNullOrWhiteSpace(packet.ActorPtr))
        {
            var atk = ResolveActor(packet.ActorPtr, attackerLess: false);
            dump["attackerElements"] = new Dictionary<string, object>
            {
                ["primary"] = atk.ElementTypes.Primary?.ToString() ?? "",
                ["secondary"] = atk.ElementTypes.Secondary?.ToString() ?? "",
                ["neutral"] = atk.ElementTypes.IsNeutral
            };
        }

        CombatDebugObservability.RememberOverlay(dump);
        DebugRuntime.Emit("debug.combat.overlay", dump);
    }
}
