using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Diagnostics;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Plugins;
using FusionRpg.Core.Status;
using FusionRpg.Injector.Fx;
using FusionRpg.Injector.Stats;

namespace FusionRpg.Injector.Effects;

/// <summary>Injector-hosted Foundation EffectBag + Unity action sink.</summary>
public static class EffectRuntime
{
    static readonly object Gate = new();
    static EffectBag? _bag;
    static EffectPluginHost? _plugins;
    static StatusRuntime? _status;
    static EffectEventDedupe _dedupe = new();
    static long _tick;
    /// <summary>
    /// Last OnDamageDealt tick per <c>matchKey|targetPtr</c> (A2 — skip redundant taken).
    /// Keyed by target, not target+actor: the taken-side check only asks "was this target dealt
    /// to within 8 ticks", and the newest tick is always the closest — O(1) instead of the old
    /// full-table prefix scan per damage event (~1000 events/s in heavy combat).
    /// </summary>
    static readonly Dictionary<string, long> DealtIdentity = new(StringComparer.OrdinalIgnoreCase);
    static float _dotAccum;

    public static StatusRuntime Status
    {
        get
        {
            Ensure();
            return _status!;
        }
    }

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
            _status = InjectorStatusBridge.CreateRuntime();
            _status.OnResisted = ev => DebugRuntime.Emit("debug.status.resisted", new Dictionary<string, object>
            {
                ["statusId"] = ev.StatusId,
                ["hostPtr"] = ev.HostPtr,
                ["attackerPtr"] = ev.AttackerPtr ?? "",
                ["grantId"] = ev.GrantId,
                ["reason"] = ev.Reason.ToString(),
                ["delta"] = ev.Delta,
                ["at"] = ev.At.ToString("o")
            });
            _bag = new EffectBag(catalog, grants, proc, new InjectorEffectActionSink());
            _bag.Status = _status;
            WireCombatMath(_bag);
            _ = new EffectFunnel(_bag, DamageFxCueAdapter.Sink);
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
            _status = null;
            DealtIdentity.Clear();
            _dotAccum = 0;
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

    /// <summary>Producer gate for *.damage emission — melee OnDamageTaken effects need it even with telemetry off.</summary>
    public static bool HasOnDamageTakenGrant() => Bag.HasGrantWithTrigger(EffectTriggers.OnDamageTaken);

    /// <summary>Producer gate for bullet.init emission (OnSpawn trigger).</summary>
    public static bool HasOnSpawnGrant() => Bag.HasGrantWithTrigger(EffectTriggers.OnSpawn);

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
        _status?.WithdrawEntity(ptrHex.Trim());
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
        try { CheatActions.ReapplyAllLiving(); } catch { }
        InjectorDerivedOverride.Clear();
        InjectorElementOverride.Clear();

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
        using var _perf = PerfProbe.Measure(PerfSection.EffectOnCapture);
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
            var id = (ev.MatchKey ?? "") + "|" + (ev.TargetPtr ?? "");
            DealtIdentity[id] = tick;
            if (DealtIdentity.Count > 2048) DealtIdentity.Clear();
        }
        else if (string.Equals(ev.Trigger, EffectTriggers.OnDamageTaken, StringComparison.OrdinalIgnoreCase))
        {
            var id = (ev.MatchKey ?? "") + "|" + (ev.TargetPtr ?? "");
            if (DealtIdentity.TryGetValue(id, out var dealtTick) && Math.Abs(dealtTick - tick) < 8)
                return;
        }

        // EffectEventDedupe removed from this path: with a per-event tick its window (1) made
        // it always-pass dead code (v2 audit §D2), while still costing a dict insert per event.
        try
        {
            FreezeBoard();
            var plan = Bag.OnEvent(ev);
            MaybeEmitCombatPacketTrace(plan, "capture");
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
        FreezeBoard();
        var plan = Bag.OnEvent(ev);
        MaybeEmitCombatPacketTrace(plan, "synthetic");
        return plan;
    }

    public static void FreezeBoard()
    {
        Ensure();
        Bag.BoardSnapshot = InjectorBoardSnapshot.Capture();
        Bag.SelectedPtr = CheatState.SelectedPtr == IntPtr.Zero
            ? null
            : CheatState.SelectedPtr.ToString("X");
    }

    /// <summary>Coalesce status/OverTime ticks on a ~100ms grid.</summary>
    public static void TickDots(float unscaledDeltaTime)
    {
        using var _perf = PerfProbe.Measure(PerfSection.EffectTickDots);
        Ensure();
        var hasStatus = Status.HasAnyInstances();
        if (!hasStatus)
        {
            _dotAccum = 0;
            return;
        }

        _dotAccum += unscaledDeltaTime;
        if (_dotAccum < 0.1f) return;
        _dotAccum = 0;
        FreezeBoard();
        var n = Bag.TickDots();
        if (n > 0)
        {
            var actions = Bag.Funnel?.LastFlushedActions ?? Array.Empty<EffectActionPlanItem>();
            MaybeEmitCombatPacketTrace(new IntentPlanDto
            {
                Trigger = EffectTriggers.OnTimer,
                Actions = actions.ToList(),
                Skipped = Bag.LastSkipped.ToList()
            }, "dot");
        }
    }

    public static void BindSelectedTarget(DamagePacket packet)
    {
        if (packet?.Target == null) return;
        if (!string.Equals(packet.Target.Mode, TargetModes.Selected, StringComparison.OrdinalIgnoreCase))
            return;
        if (CheatState.SelectedPtr == IntPtr.Zero)
        {
            packet.Target.Mode = TargetModes.Single;
            packet.Target.Ptr = null;
            return;
        }

        packet.Target.Mode = TargetModes.Single;
        packet.Target.Ptr = CheatState.SelectedPtr.ToString("X");
    }

    static void MaybeEmitCombatPacketTrace(IntentPlanDto plan, string source)
    {
        // LIVE-prove trace only — in normal play this fired per hit (dict + list allocs + queue).
        if (!DebugRuntime.SessionActive) return;
        var fa = plan.Actions
            .Where(a => string.Equals(a.Action, EffectActions.ApplyResourceDelta, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (fa.Count <= 0) return;
        var ptrs = fa
            .Select(a => a.Params.TryGetValue("targetPtr", out var p) ? p?.ToString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();
        DebugRuntime.Emit("debug.combat.packet", new Dictionary<string, object>
        {
            ["source"] = source,
            ["fa10"] = fa.Count,
            ["skipped"] = plan.Skipped.Count,
            ["trigger"] = plan.Trigger ?? "",
            ["ptrs"] = ptrs,
            ["chainDepth"] = 0
        });
    }

    static void WireCombatMath(EffectBag bag)
    {
        var overlay = OverlayCombatMath.Create(
            InjectorCombatBridge.ResolveActor,
            ElementHub.Default,
            bag.CombatRng,
            (breakdown, packet, targetPtr) =>
                InjectorCombatBridge.EmitOverlayBreakdown(breakdown, packet, targetPtr));
        bag.CombatMath = new ConditionalOverlayCombatMath(overlay)
        {
            IsEnabled = () => OverlayCombatFeature.Enabled
        };
    }
}

/// <summary>Thin injector wrapper — mapping lives in <see cref="EffectEventAdapterCore"/>.</summary>
public static class EffectEventAdapter
{
    public static EffectEventDto? TryMap(string kind, Dictionary<string, object> p, long tick) =>
        EffectEventAdapterCore.TryMap(kind, p, tick, GameHooks.MatchKey);
}
