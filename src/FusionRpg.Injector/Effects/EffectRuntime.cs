using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Plugins;
using FusionRpg.Core.Stats;
using FusionRpg.Injector.Fx;

namespace FusionRpg.Injector.Effects;

/// <summary>Injector-hosted Foundation EffectBag + Unity action sink.</summary>
public static class EffectRuntime
{
    static readonly object Gate = new();
    static EffectBag? _bag;
    static EffectPluginHost? _plugins;
    static EffectEventDedupe _dedupe = new();
    static long _tick;
    /// <summary>Targets that already got OnDamageDealt this tick window (A2 — skip redundant taken).</summary>
    static readonly Dictionary<string, long> DealtIdentity = new(StringComparer.OrdinalIgnoreCase);

    public static EffectBag Bag
    {
        get
        {
            Ensure();
            return _bag!;
        }
    }

    public static void Ensure()
    {
        lock (Gate)
        {
            if (_bag != null) return;
            var catalog = new InMemoryEffectCatalog();
            catalog.ReplaceAll(EffectSeedCatalog.CreateAll());
            var grants = new InMemoryEffectGrantStore();
            var proc = new EffectProcPolicy(new SystemEffectClock(), new SeededEffectRandom(Environment.TickCount));
            _bag = new EffectBag(catalog, grants, proc, new InjectorEffectActionSink());
            _ = new EffectFunnel(_bag, DamageFxOverlay.Sink);
            _plugins = EffectPluginHostFactory.Create(_bag);
            _dedupe = new EffectEventDedupe();
            _tick = 0;
        }
    }

    public static void ResetForTests()
    {
        lock (Gate)
        {
            _bag = null;
            _plugins = null;
            DealtIdentity.Clear();
            Ensure();
        }
    }

    public static void NotifyMatchStart(string matchKey, long playerId = 0)
    {
        Ensure();
        _plugins!.NotifyMatchStart(matchKey, playerId);
    }

    public static void NotifyMatchEnd(string? matchKey)
    {
        Ensure();
        _plugins!.NotifyRemoved(matchKey ?? "");
    }

    public static long NextTick() => Interlocked.Increment(ref _tick);

    public static bool HasActiveGrants() => Bag.HasAnyGrant();

    public static bool HasGrantForEffect(string effectId) => Bag.HasGrantForEffect(effectId);

    public static bool HasOnDamageDealtGrant() => Bag.HasGrantWithTrigger(EffectTriggers.OnDamageDealt);

    /// <summary>
    /// Product <c>combat.hit</c> when debug/LogDamage hit-capture is on, or bag has OnDamageDealt grants.
    /// </summary>
    public static bool ShouldEmitCombatHit() =>
        DebugRuntime.ShouldEmitHit() || HasOnDamageDealtGrant();

    public static bool HasOnDeathGrant() => Bag.HasGrantWithTrigger(EffectTriggers.OnDeath);

    public static EffectGrant Grant(EffectGrantDto dto)
    {
        Ensure();
        var g = Bag.Grant(dto);
        DebugRuntime.Emit("debug.effect.granted", new Dictionary<string, object>
        {
            ["grantId"] = g.GrantId,
            ["effectId"] = g.EffectId,
            ["ownerKey"] = g.OwnerKey
        });
        return g;
    }

    public static bool Withdraw(string grantId)
    {
        Ensure();
        // Bag.Withdraw fires OnRemoved → FA1 remove + ReapplyLivingForOwner(owner).
        var ok = Bag.Withdraw(grantId);
        if (ok)
            DebugRuntime.Emit("debug.effect.withdrawn", new Dictionary<string, object> { ["grantId"] = grantId });
        return ok;
    }

    /// <summary>
    /// Withdraw all grants owned by <c>entity:{ptr}</c> (normalized). Call on die before ForgetEntity.
    /// </summary>
    public static int WithdrawEntity(string? ptrHex)
    {
        if (string.IsNullOrWhiteSpace(ptrHex)) return 0;
        Ensure();
        var n = Bag.WithdrawForOwner(null, EffectOwnerKeys.Entity(ptrHex.Trim()));
        if (n > 0)
        {
            DebugRuntime.Emit("debug.effect.withdrawn_entity", new Dictionary<string, object>
            {
                ["ptr"] = ptrHex.Trim(),
                ["count"] = n
            });
        }

        return n;
    }

    /// <summary>Withdraw all grants, clear proc/dedupe, strip session effect mods.</summary>
    public static void ClearAll(string reason = "clear")
    {
        Ensure();
        lock (Gate)
        {
            Bag.ClearAll();
            _dedupe.Clear();
            DealtIdentity.Clear();
        }

        try { CheatState.Stats.WithdrawAllBySourceKind("effect"); } catch { }
        // Match-wide clear: every living unit may have composed effect mods.
        try { CheatActions.ReapplyAllLiving(); } catch { }

        DebugRuntime.Emit("debug.effect.cleared", new Dictionary<string, object>
        {
            ["reason"] = reason,
            ["contractVersion"] = FoundationContractVersion.Current
        });
    }

    public static EffectCatalogSnapshotDto Snapshot()
    {
        Ensure();
        return Bag.Snapshot();
    }

    public static void ReplaceCatalog(IEnumerable<EffectDef> defs)
    {
        ClearAll("reload");
        Ensure();
        Bag.Catalog.ReplaceAll(defs);
        DebugRuntime.Emit("debug.effect.reload", new Dictionary<string, object>
        {
            ["contractVersion"] = FoundationContractVersion.Current,
            ["count"] = Bag.Catalog.All().Count
        });
    }

    public static void OnCapture(string kind, Dictionary<string, object> payload)
    {
        Ensure();
        if (!Bag.HasAnyGrant() && !(Bag.Funnel?.HasPending ?? false)) return;

        // A2: when TakeDamage will also emit combat.hit (bullet), skip OnDamageTaken from *.damage.
        if (CombatHitEmitPolicy.WillSkipTakenFromDamage(kind, payload, ShouldEmitCombatHit()))
            return;

        var tick = NextTick();
        var ev = EffectEventAdapter.TryMap(kind, payload, tick);
        if (ev == null) return;

        if (string.Equals(ev.Trigger, EffectTriggers.OnDamageDealt, StringComparison.OrdinalIgnoreCase))
        {
            var id = (ev.MatchKey ?? "") + "|" + (ev.TargetPtr ?? "") + "|" + (ev.ActorPtr ?? "");
            DealtIdentity[id] = tick;
            if (DealtIdentity.Count > 2048) DealtIdentity.Clear();
        }
        else if (string.Equals(ev.Trigger, EffectTriggers.OnDamageTaken, StringComparison.OrdinalIgnoreCase))
        {
            var id = (ev.MatchKey ?? "") + "|" + (ev.TargetPtr ?? "") + "|";
            foreach (var kv in DealtIdentity)
            {
                if (kv.Key.StartsWith(id, StringComparison.OrdinalIgnoreCase) && Math.Abs(kv.Value - tick) < 8)
                    return;
            }
        }

        if (!_dedupe.ShouldEmit(ev)) return;
        try
        {
            var plan = Bag.OnEvent(ev);
            if (plan.Actions.Count > 0)
            {
                DebugRuntime.Emit("debug.effect.plan", new Dictionary<string, object>
                {
                    ["trigger"] = plan.Trigger,
                    ["actions"] = plan.Actions.Count,
                    ["skipped"] = plan.Skipped.Count,
                    ["contractVersion"] = plan.ContractVersion
                });
            }
        }
        catch (Exception ex)
        {
            CheatState.Error("effect OnEvent: " + ex.Message);
            DebugRuntime.Emit("debug.effect.error", new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["kind"] = kind
            });
        }
    }

    public static IntentPlanDto FireSynthetic(EffectEventDto ev)
    {
        Ensure();
        if (ev.Tick <= 0) ev.Tick = NextTick();
        return Bag.OnEvent(ev);
    }
}

/// <summary>Thin injector wrapper — mapping lives in <see cref="EffectEventAdapterCore"/>.</summary>
public static class EffectEventAdapter
{
    public static EffectEventDto? TryMap(string kind, Dictionary<string, object> p, long tick) =>
        EffectEventAdapterCore.TryMap(kind, p, tick, GameHooks.MatchKey);
}
