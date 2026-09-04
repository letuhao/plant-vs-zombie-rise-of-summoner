using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Diagnostics;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Effects;

public interface IEffectCatalog
{
    EffectDef? Get(string effectId);
    IReadOnlyList<EffectDef> All();
    void Upsert(EffectDef def);
    void ReplaceAll(IEnumerable<EffectDef> defs);
    int Revision { get; }
    EffectCatalogSnapshotDto ToSnapshot(IEnumerable<EffectGrantDto>? grants = null);
}

public sealed class InMemoryEffectCatalog : IEffectCatalog
{
    readonly Dictionary<string, EffectDef> _defs = new(StringComparer.OrdinalIgnoreCase);
    int _revision;

    public int Revision => _revision;

    public EffectDef? Get(string effectId) =>
        _defs.TryGetValue(effectId, out var d) ? d : null;

    public IReadOnlyList<EffectDef> All() => _defs.Values.OrderBy(d => d.EffectId).ToList();

    public void Upsert(EffectDef def)
    {
        // Actions sorted once here so FireGrant can iterate without a per-fire OrderBy.
        def.Actions.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        _defs[def.EffectId] = def;
        _revision++;
    }

    public void ReplaceAll(IEnumerable<EffectDef> defs)
    {
        _defs.Clear();
        foreach (var d in defs)
        {
            d.Actions.Sort((a, b) => a.Seq.CompareTo(b.Seq));
            _defs[d.EffectId] = d;
        }
        _revision++;
    }

    /// <summary>Bump revision without changing defs (grant upsert/withdraw).</summary>
    public void TouchRevision() => _revision++;

    public EffectCatalogSnapshotDto ToSnapshot(IEnumerable<EffectGrantDto>? grants = null) => new()
    {
        ContractVersion = FoundationContractVersion.Current,
        Defs = All().Select(d => d.ToDto()).ToList(),
        Grants = grants?.ToList() ?? new List<EffectGrantDto>(),
        Revision = _revision
    };
}

public interface IEffectGrantStore
{
    EffectGrant? Get(string grantId);
    IReadOnlyList<EffectGrant> All();
    IReadOnlyList<EffectGrant> Matching(EffectEventDto ev);
    IReadOnlyList<EffectGrant> ForOwner(string? ownerKind, string ownerKey);
    void Upsert(EffectGrant grant);
    bool Withdraw(string grantId);
    void Clear();
}

public sealed class InMemoryEffectGrantStore : IEffectGrantStore
{
    readonly Dictionary<string, EffectGrant> _grants = new(StringComparer.OrdinalIgnoreCase);
    // All()/Matching() run per combat event (~1000/s in heavy boards) — the sorted view is
    // cached and rebuilt only on grant mutation, never per read.
    List<EffectGrant>? _sorted;

    public EffectGrant? Get(string grantId) =>
        _grants.TryGetValue(grantId, out var g) ? g : null;

    List<EffectGrant> Sorted() =>
        _sorted ??= _grants.Values.OrderByDescending(g => g.Priority).ThenBy(g => g.GrantId).ToList();

    public IReadOnlyList<EffectGrant> All() => Sorted();

    public IReadOnlyList<EffectGrant> Matching(EffectEventDto ev)
    {
        var all = Sorted();
        if (all.Count == 0) return Array.Empty<EffectGrant>();
        List<EffectGrant>? matched = null;
        foreach (var g in all)
        {
            if (EffectOwnerKey.MatchesEvent(g, ev))
                (matched ??= new List<EffectGrant>()).Add(g);
        }
        return (IReadOnlyList<EffectGrant>?)matched ?? Array.Empty<EffectGrant>();
    }

    public IReadOnlyList<EffectGrant> ForOwner(string? ownerKind, string ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey)) return Array.Empty<EffectGrant>();
        var want = StatApplyScope.Normalize(ownerKey);
        return All().Where(g =>
        {
            if (!string.Equals(StatApplyScope.Normalize(g.OwnerKey), want, StringComparison.Ordinal))
                return false;
            if (string.IsNullOrWhiteSpace(ownerKind)) return true;
            return string.Equals(g.OwnerKind, ownerKind, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    public void Upsert(EffectGrant grant)
    {
        _grants[grant.GrantId] = grant;
        _sorted = null;
    }

    public bool Withdraw(string grantId)
    {
        var ok = _grants.Remove(grantId);
        if (ok) _sorted = null;
        return ok;
    }

    public void Clear()
    {
        _grants.Clear();
        _sorted = null;
    }
}

/// <summary>Foundation Effect bag — sole planner of FA* IntentPlan items for Secondary/LIVE.</summary>
public sealed class EffectBag
{
    readonly IEffectCatalog _catalog;
    readonly IEffectGrantStore _grants;
    readonly EffectProcPolicy _proc;
    readonly IEffectActionSink _sink;
    readonly List<string> _lastSkipped = new();

    public EffectBag(
        IEffectCatalog catalog,
        IEffectGrantStore grants,
        EffectProcPolicy proc,
        IEffectActionSink sink)
    {
        _catalog = catalog;
        _grants = grants;
        _proc = proc;
        _sink = sink;
    }

    public IEffectCatalog Catalog => _catalog;
    public IEffectGrantStore Grants => _grants;
    public EffectProcPolicy Proc => _proc;
    public IEffectActionSink Sink => _sink;
    public EffectFunnel? Funnel { get; private set; }
    public IReadOnlyList<string> LastSkipped => _lastSkipped;
    public BoardSnapshot BoardSnapshot { get; set; } = BoardSnapshot.Empty;
    public CombatPolicy CombatPolicy { get; set; } = CombatPolicy.Default;
    public ICombatRng CombatRng { get; set; } = new SeededCombatRng(42);
    public ICombatMath CombatMath { get; set; } = PassThroughCombatMath.Instance;

    /// <summary>Shield layer above the Funnel — null keeps combat byte-identical (no shields).</summary>
    public FusionRpg.Core.Combat.Shield.ShieldGate? ShieldGate { get; set; }

    /// <summary>
    /// E41 (spec-ui-attach-point.md §2b): the <c>op:meter</c>/<c>op:banner</c> collaborator — null
    /// skips with a named reason (<c>:hud-runtime-missing</c>), the same optional-collaborator shape
    /// <see cref="ShieldGate"/>/<see cref="Status"/> already use. <c>op:number</c> needs no collaborator
    /// here — it reuses the Funnel's existing <see cref="IDamageFxSink"/> present path.
    /// </summary>
    public IUiPresentSink? UiPresent { get; set; }

    /// <summary>aura-skill T20: the same actor-resolution function wired into `CombatMath`/
    /// `ShieldGate` ("same resolve as combat" — `EffectRuntime.WireCombatMath`'s own comment) —
    /// threaded through to <see cref="Combat.CombatDamageDispatcher.DispatchInstant"/>'s
    /// `actorResolve` parameter so Retribution/reflect actually fires on a real damage packet. Every
    /// production call site previously omitted this argument, so the shipped-looking reflect math
    /// never ran outside the offline test harness. Null keeps combat byte-identical (no reflect),
    /// matching every other optional collaborator on this class.</summary>
    public FusionRpg.Core.Combat.CombatActorResolve? ActorResolve { get; set; }
    public StatusRuntime? Status { get; set; }
    public IStatusRng StatusRng { get; set; } = new FixedStatusRng(0.0);

    /// <summary>
    /// base-defense Gate 0 (audit C4): moved from an implicit wall-clock default at the FIELD to a
    /// loud failure that forces every caller to choose explicitly at its own COMPOSITION ROOT — the
    /// fix the audit itself names. The old default (`() => DateTimeOffset.UtcNow`) worked correctly
    /// for the one caller that legitimately wants real time (`FusionRpg.Injector`'s live-PvZ host,
    /// which now sets this explicitly, matching the three deterministic hosts that already did) — but
    /// it meant a NEW deterministic composition root (a siege resolver, an expedition harness) could
    /// silently inherit wall-clock status timing by forgetting one line, and nothing would fail until
    /// a replay disagreed with itself on a different machine, weeks later.
    ///
    /// <para>Read only when a status-timed feature (<see cref="TickDots"/>,
    /// <see cref="StatusEffectBridge.TryApplyFromGrant"/>) is actually exercised — a battle with no
    /// `Status` wired never reads this and never throws, so boardless/statusless harnesses are
    /// unaffected.</para>
    /// </summary>
    public Func<DateTimeOffset> UtcNow
    {
        get => _utcNow ?? throw new InvalidOperationException(
            "EffectBag.UtcNow has not been set. Every composition root that times statuses must " +
            "choose explicitly: a deterministic host wires this to its own SimulationClock/IEffectClock " +
            "(see BattleEffects.cs, FoundationHarness.cs, SimEffectHost.cs); a live, non-replayed host " +
            "(the injector) wires it to the real wall clock explicitly, on purpose, at its own " +
            "composition root — never as a silent field default.");
        set => _utcNow = value ?? throw new ArgumentNullException(nameof(value));
    }
    Func<DateTimeOffset>? _utcNow;
    /// <summary>Debug Selected ptr. Grant overlays with <c>target.mode=Selected</c> rewrite to Single.</summary>
    public string? SelectedPtr { get; set; }

    readonly List<OverlayProcNote> _overlayProcs = new();
    bool _drainingOverlay;

    internal void AttachFunnel(EffectFunnel funnel) => Funnel = funnel;

    public EffectGrant Grant(EffectGrantDto dto)
    {
        var grant = EffectGrant.FromDto(dto);
        if (StatApplyScope.IsInstanceOwnerKey(grant.OwnerKey))
            throw new InvalidOperationException(
                "hot ownerKey instance: forbidden; bind to entity:{ptr} first");

        var def = _catalog.Get(grant.EffectId)
                  ?? throw new InvalidOperationException("unknown effect_id: " + grant.EffectId);

        if (!EffectOverlayMerge.TryValidateOverlayForDef(def.Actions, grant.Overlay, out var err))
            throw new InvalidOperationException(err);

        _grants.Upsert(grant);
        BumpRevision();

        if (string.Equals(def.EffectType, EffectTypes.Passive, StringComparison.OrdinalIgnoreCase) ||
            def.Triggers.Any(t => string.Equals(t, EffectTriggers.OnGranted, StringComparison.OrdinalIgnoreCase)))
        {
            var synth = new EffectEventDto
            {
                Trigger = EffectTriggers.OnGranted,
                MatchKey = "grant",
                Tick = 0
            };
            FireGrant(grant, def, synth, forceTrigger: EffectTriggers.OnGranted);
        }

        return grant;
    }

    public bool Withdraw(string grantId)
    {
        var grant = _grants.Get(grantId);
        if (grant == null) return false;
        var def = _catalog.Get(grant.EffectId);
        _grants.Withdraw(grantId);
        _proc.ClearGrant(grantId);
        Status?.ClearGrant(grantId);
        BumpRevision();
        if (def != null &&
            (string.Equals(def.EffectType, EffectTypes.Passive, StringComparison.OrdinalIgnoreCase) ||
             def.Triggers.Any(t => string.Equals(t, EffectTriggers.OnRemoved, StringComparison.OrdinalIgnoreCase))))
        {
            var synth = new EffectEventDto { Trigger = EffectTriggers.OnRemoved, MatchKey = "withdraw", Tick = 0 };
            FireGrant(grant, def, synth, forceTrigger: EffectTriggers.OnRemoved);
        }

        return true;
    }

    /// <summary>Withdraw all grants (fires OnRemoved), clear proc clocks/stacks.</summary>
    public void ClearAll()
    {
        foreach (var g in _grants.All().ToList())
            Withdraw(g.GrantId);
        _proc.Clear();
        Status?.Clear();
        ShieldGate?.Runtime.Clear();
        _overlayProcs.Clear();
    }

    public IReadOnlyList<EffectGrant> ForOwner(string? ownerKind, string ownerKey) =>
        _grants.ForOwner(ownerKind, ownerKey);

    /// <summary>
    /// Withdraw every grant whose owner matches <paramref name="ownerKey"/> (optional kind filter).
    /// Keys compared via <see cref="StatApplyScope.Normalize"/> (<c>entity:0xAAA</c> ≡ <c>entity:aaa</c>).
    /// Fail-closed: null/whitespace key → 0.
    /// </summary>
    public int WithdrawForOwner(string? ownerKind, string? ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey)) return 0;
        var targets = ForOwner(ownerKind, ownerKey);
        var n = 0;
        foreach (var g in targets)
        {
            if (Withdraw(g.GrantId)) n++;
        }

        return n;
    }

    public EffectCatalogSnapshotDto Snapshot() =>
        _catalog.ToSnapshot(_grants.All().Select(g => g.ToDto()));

    void BumpRevision()
    {
        // Catalog revision tracks def changes; grant mutations also bump via catalog Upsert no-op —
        // use a grant-aware bump by re-reading: store revision on catalog via empty touch.
        if (_catalog is InMemoryEffectCatalog mem)
            mem.TouchRevision();
    }

    public bool HasGrantForEffect(string effectId)
    {
        using var _perf = PerfProbe.Measure(PerfSection.GrantsScan);
        return _grants.All().Any(g => string.Equals(g.EffectId, effectId, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyGrant() => _grants.All().Count > 0;

    // Trigger index — event-pipeline-v2-ssot.md §3.1. Hooks consult this per game event to
    // decide whether to emit at all, so it must be O(1). BumpRevision (grant mutations) and
    // catalog Upsert/ReplaceAll both advance _catalog.Revision, which keys the cache.
    Dictionary<string, int>? _triggerCounts;
    int _triggerIndexRevision = -1;

    Dictionary<string, int> TriggerIndex()
    {
        var rev = _catalog.Revision;
        if (_triggerCounts != null && _triggerIndexRevision == rev) return _triggerCounts;
        var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in _grants.All())
        {
            var def = _catalog.Get(g.EffectId);
            if (def == null || !def.Enabled) continue;
            foreach (var t in def.Triggers)
                idx[t] = idx.TryGetValue(t, out var n) ? n + 1 : 1;
        }
        _triggerCounts = idx;
        _triggerIndexRevision = rev;
        return idx;
    }

    public bool HasGrantWithTrigger(string trigger)
    {
        using var _perf = PerfProbe.Measure(PerfSection.GrantsScan);
        return TriggerIndex().TryGetValue(trigger, out var n) && n > 0;
    }

    public IntentPlanDto OnEvent(EffectEventDto ev)
    {
        using var _perf = PerfProbe.Measure(PerfSection.EffectOnEvent);
        _lastSkipped.Clear();
        var planned = new List<EffectActionPlanItem>();
        var sinkRecorder = _sink as RecordingEffectSink;
        var before = sinkRecorder?.Items.Count ?? 0;

        foreach (var grant in _grants.Matching(ev))
        {
            if (!string.IsNullOrWhiteSpace(ev.SourceGrantId) &&
                string.Equals(grant.GrantId, ev.SourceGrantId, StringComparison.OrdinalIgnoreCase))
                continue;
            var def = _catalog.Get(grant.EffectId);
            if (def == null || !def.Enabled) continue;
            FireGrant(grant, def, ev, forceTrigger: null, planned);
        }

        Funnel?.Flush();
        if (!_drainingOverlay)
            DrainOverlayProcs();
        if (Funnel != null)
        {
            if (sinkRecorder == null)
                planned.AddRange(Funnel.LastFlushedActions);
            _lastSkipped.AddRange(Funnel.LastSkipped);
        }

        IntentPlanDto plan;
        if (sinkRecorder != null)
        {
            plan = new IntentPlanDto
            {
                ContractVersion = FoundationContractVersion.Current,
                Trigger = ev.Trigger,
                Actions = sinkRecorder.Items.Skip(before).ToList(),
                Skipped = _lastSkipped.ToList()
            };
        }
        else
        {
            plan = new IntentPlanDto
            {
                ContractVersion = FoundationContractVersion.Current,
                Trigger = ev.Trigger,
                Actions = planned,
                Skipped = _lastSkipped.ToList()
            };
        }

        Funnel?.AcknowledgeWindow();
        return plan;
    }

    void FireGrant(
        EffectGrant grant,
        EffectDef def,
        EffectEventDto ev,
        string? forceTrigger,
        List<EffectActionPlanItem>? plannedOut = null)
    {
        var trigger = forceTrigger ?? ev.Trigger;
        var isLifecycle = string.Equals(trigger, EffectTriggers.OnGranted, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(trigger, EffectTriggers.OnRemoved, StringComparison.OrdinalIgnoreCase);

        if (!isLifecycle)
        {
            if (!def.Triggers.Any(t => string.Equals(t, trigger, StringComparison.OrdinalIgnoreCase)))
                return;
            if (!EffectOwnerKey.PassesOverlayFilters(grant.Overlay, ev, grant))
            {
                _lastSkipped.Add(grant.GrantId + ":filter");
                return;
            }

            if (!_proc.TryPass(grant, trigger, out var skip, Math.Max(1, ev.HitCount)))
            {
                _lastSkipped.Add(grant.GrantId + ":" + (skip ?? "proc"));
                return;
            }
        }

        // InMemoryEffectCatalog sorts Actions by Seq at Upsert/ReplaceAll; defs from other
        // sources (tests building EffectDef inline) are seeded in Seq order already.
        var actions = def.Actions;
        // Passive OnRemoved: reverse ModifyStat polarity via overlay flag remove=true
        var ctx = new EffectExecuteContext { Event = ev, Grant = grant, Def = def };

        foreach (var action in actions)
        {
            if (!EffectOverlayMerge.TryMerge(action.Action, action.Params, grant.Overlay, out var merged, out var err))
            {
                _lastSkipped.Add(grant.GrantId + ":overlay:" + err);
                return;
            }

            if (string.Equals(trigger, EffectTriggers.OnRemoved, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action.Action, EffectActions.ModifyStat, StringComparison.OrdinalIgnoreCase))
            {
                merged["remove"] = true;
            }

            if (string.Equals(action.Action, EffectActions.GrantShield, StringComparison.OrdinalIgnoreCase))
            {
                ExecGrantShield(grant, def, ev, merged);
                continue;
            }

            // E41 (spec-ui-attach-point.md §2a): bag-side, the same shape as GrantShield immediately
            // above and ApplyResourceDelta below — a Ui-attached kind's action never becomes an
            // EffectActionPlanItem and never reaches _sink.Execute (InjectorEffectActionSink's stat/
            // resource/status/shield/board arms). That is the module's central read-only invariant,
            // made structural by this branch existing rather than left as a convention nobody enforces
            // (Ui_attached_kinds_never_reach_the_generic_sink, tests/.../UiPresentTests.cs).
            if (string.Equals(action.Action, EffectActions.PresentUi, StringComparison.OrdinalIgnoreCase))
            {
                ExecPresentUi(grant, ev, merged);
                continue;
            }

            if (string.Equals(action.Action, EffectActions.ApplyResourceDelta, StringComparison.OrdinalIgnoreCase))
            {
                var packet = DamagePacketBuilder.FromOverlay(
                    merged,
                    ev,
                    grant.GrantId,
                    def.EffectId,
                    grant.PluginId);
                // `P0.2`: skip the re-read when "amount" is the event-linked marker object, not a
                // number — DamagePacketBuilder already resolved it correctly (possibly a genuine
                // zero, e.g. no damage this tick), and JsonOverlay.GetDouble cannot convert an object.
                if (merged.ContainsKey("amount") && packet.SignedAmount == 0
                    && merged["amount"] is not (Dictionary<string, object?> or Dictionary<string, object>)
                    && merged["amount"] is not JsonElement { ValueKind: JsonValueKind.Object })
                    packet.SignedAmount = (long)JsonOverlay.GetDouble(merged, "amount");
                BindSelected(packet);

                if (Status != null)
                {
                    var statusPlanStart = plannedOut?.Count ?? 0;
                    if (StatusEffectBridge.TryApplyFromGrant(
                            Status, grant, ev, grant.Overlay, BoardSnapshot, StatusRng, UtcNow(),
                            _lastSkipped, plannedOut, def))
                    {
                        if (StatusEffectBridge.TryResolveStatusId(grant.Overlay, out var sid)
                            && string.Equals(sid, "bond", StringComparison.OrdinalIgnoreCase))
                        {
                            TryFireCounterBurst(packet, grant, ev);
                        }

                        if (plannedOut != null)
                        {
                            for (var i = statusPlanStart; i < plannedOut.Count; i++)
                            {
                                if (!_sink.Execute(ctx, plannedOut[i]))
                                {
                                    _lastSkipped.Add(grant.GrantId + ":executor-stop");
                                    return;
                                }
                            }
                        }

                        continue;
                    }
                }

                if (Status == null && JsonOverlay.GetString(grant.Overlay, "statusId") is { Length: > 0 })
                {
                    _lastSkipped.Add(grant.GrantId + ":status-runtime-missing");
                    continue;
                }

                var delivery = packet.Delivery.Mode ?? DeliveryModes.Instant;
                if (string.Equals(delivery, DeliveryModes.OverTime, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(delivery, DeliveryModes.Counter, StringComparison.OrdinalIgnoreCase))
                {
                    _lastSkipped.Add(grant.GrantId + ":status-runtime-missing");
                    continue;
                }

                CombatDamageDispatcher.DispatchInstant(
                    packet,
                    BoardSnapshot,
                    ev,
                    Funnel,
                    CombatPolicy,
                    CombatRng,
                    CombatMath,
                    _lastSkipped,
                    ShieldGate,
                    ActorResolve);
                continue;
            }

            var item = new EffectActionPlanItem
            {
                Seq = action.Seq,
                Action = action.Action,
                EffectId = def.EffectId,
                GrantId = grant.GrantId,
                SourceTag = "effect:" + def.EffectId,
                Params = merged,
                Tags = new Dictionary<string, string>
                {
                    ["effect_id"] = def.EffectId,
                    ["grant_id"] = grant.GrantId,
                    ["plugin"] = grant.PluginId,
                    ["trigger"] = trigger
                }
            };

            plannedOut?.Add(item);
            if (!_sink.Execute(ctx, item))
            {
                _lastSkipped.Add(grant.GrantId + ":executor-stop");
                return; // stop sequence on first failure (A7)
            }
        }
    }

    void TryFireCounterBurst(DamagePacket packet, EffectGrant grant, EffectEventDto ev)
    {
        if (Status == null)
        {
            _lastSkipped.Add(grant.GrantId + ":status-runtime-missing");
            return;
        }

        var every = packet.Delivery.EveryHits ?? 0;
        var scope = packet.Delivery.CounterScope ?? CounterScopes.Target;
        var scopeKey = string.Equals(scope, CounterScopes.Actor, StringComparison.OrdinalIgnoreCase)
            ? (ev.ActorPtr ?? "")
            : (ev.TargetPtr ?? "");
        if (!Status.RecordCounterHit(grant.GrantId, scopeKey, every, packet.Delivery.ResetOnBurst, Math.Max(1, ev.HitCount)))
            return;

        var burst = packet.Burst;
        if (burst == null)
        {
            _lastSkipped.Add(grant.GrantId + ":counter-no-burst");
            return;
        }

        burst.ChainDepth = packet.ChainDepth + 1;
        burst.ProcDepthLimit ??= packet.ProcDepthLimit;
        burst.SourceGrantId = string.IsNullOrWhiteSpace(burst.SourceGrantId) ? grant.GrantId : burst.SourceGrantId;
        burst.EffectId ??= packet.EffectId;
        burst.PluginId ??= packet.PluginId;
        BindSelected(burst);
        CombatDamageDispatcher.DispatchInstant(
            burst,
            BoardSnapshot,
            ev,
            Funnel,
            CombatPolicy,
            CombatRng,
            CombatMath,
            _lastSkipped,
            ShieldGate,
            ActorResolve);
    }

    /// <summary>
    /// GrantShield action — bag-side like ApplyResourceDelta (shield state is Core runtime,
    /// never a Funnel mutation, never a sink plan item). Targets use the damage-packet
    /// grammar; each resolved owner gets one idempotent ShieldRuntime apply.
    /// </summary>
    void ExecGrantShield(EffectGrant grant, EffectDef def, EffectEventDto ev, Dictionary<string, object?> merged)
    {
        if (ShieldGate == null)
        {
            _lastSkipped.Add(grant.GrantId + ":shield-runtime-missing");
            return;
        }

        var baseHp = (long)Math.Abs(JsonOverlay.GetDouble(merged, "amount"));
        var elementStr = JsonOverlay.GetString(merged, "element");
        FusionRpg.Core.Stats.Derived.ElementTypeId? element = null;
        if (!string.IsNullOrWhiteSpace(elementStr))
        {
            if (!FusionRpg.Core.Stats.Derived.ElementRoster.TryParse(elementStr, out var parsed))
            {
                _lastSkipped.Add(grant.GrantId + ":shield-element");
                return;
            }

            element = parsed;
        }

        var sourceClass = JsonOverlay.GetString(merged, "sourceClass");
        var isAura = string.Equals(sourceClass, "aura", StringComparison.OrdinalIgnoreCase);
        var isInnate = string.Equals(sourceClass, "innate", StringComparison.OrdinalIgnoreCase);
        var priority = merged.ContainsKey("priority")
            ? JsonOverlay.GetInt(merged, "priority")
            : isAura ? FusionRpg.Core.Combat.Shield.ShieldPolicy.PriorityAura
            : isInnate ? FusionRpg.Core.Combat.Shield.ShieldPolicy.PriorityInnate
            : FusionRpg.Core.Combat.Shield.ShieldPolicy.PrioritySkill;
        var refill = merged.ContainsKey("refillOnMerge")
            ? JsonOverlay.GetBool(merged, "refillOnMerge")
            : !isAura;   // aura re-asserts are idempotent (spec §2.5); skill recasts refill
        long? durationTicks = merged.ContainsKey("durationTicks")
            ? (long)JsonOverlay.GetDouble(merged, "durationTicks")
            : null;

        var packet = DamagePacketBuilder.FromOverlay(merged, ev, grant.GrantId, def.EffectId, grant.PluginId);
        BindSelected(packet);
        var resolvedOwners = TargetResolver.Resolve(packet.Target, BoardSnapshot, ev, CombatPolicy, CombatRng);
        foreach (var raw in resolvedOwners)
        {
            var ptr = CombatPtr.Normalize(raw);
            if (string.IsNullOrWhiteSpace(ptr)) continue;
            var applied = ShieldGate.ApplyGrant(ptr, new FusionRpg.Core.Combat.Shield.ShieldGrant
            {
                SourceId = grant.GrantId,
                Element = element,
                BaseHp = baseHp,
                Priority = priority,
                DurationTicks = durationTicks,
                RefillOnMerge = refill,
                IsInnate = isInnate
            });
            // Spec §2.5: drops/rejections are debug-line observability, no event.
            if (applied.Outcome == FusionRpg.Core.Combat.Shield.ShieldApplyOutcome.Rejected)
                _lastSkipped.Add(grant.GrantId + ":shield-rejected");
            else if (applied.Outcome == FusionRpg.Core.Combat.Shield.ShieldApplyOutcome.DroppedWeaker)
                _lastSkipped.Add(grant.GrantId + ":shield-dropped-weaker");
        }
    }

    /// <summary>
    /// E41 (spec-ui-attach-point.md §2b): <c>ui.present</c>'s own executor — read-only by construction
    /// (see the caller's own comment). No target resolution needs a <c>target</c> param (the kind
    /// declares none, matching <c>resource.delta</c>/<c>status.apply</c>'s "the target comes from the
    /// event" precedent) — <see cref="ResolvePresentTargetPtr"/> mirrors the injector's own
    /// <c>ResolveStatusTargetPtr</c> exactly (event TargetPtr, falling back to ActorPtr).
    ///
    /// <para><c>op:number</c> reuses the Funnel's existing merge/throttle-tested floater path
    /// (<see cref="IDamageFxSink"/>/<c>DamageFxDto.MergedCount</c>) rather than a bespoke one — the
    /// 2026-08 perf audit's own discipline, restated in §3's "no present on the per-hit path
    /// uncached" rule. <c>op:meter</c>/<c>op:banner</c> go through <see cref="UiPresent"/>, null-safe
    /// with a named skip (matching <see cref="ShieldGate"/>'s own "runtime-missing" shape) since
    /// neither has a Funnel-level merge queue of its own.</para>
    /// </summary>
    void ExecPresentUi(EffectGrant grant, EffectEventDto ev, Dictionary<string, object?> merged)
    {
        var op = JsonOverlay.GetString(merged, "op");
        switch (op)
        {
            case "number":
            {
                var targetPtr = ResolvePresentTargetPtr(ev);
                if (string.IsNullOrEmpty(targetPtr))
                {
                    _lastSkipped.Add(grant.GrantId + ":present-no-target");
                    return;
                }

                var tagStr = JsonOverlay.GetString(merged, "tag");
                var tag = !string.IsNullOrEmpty(tagStr)
                    && Enum.TryParse<DamageFxTag>(tagStr, ignoreCase: true, out var parsed)
                        ? parsed
                        : DamageFxTag.Neutral;

                Funnel?.EnqueuePresent(new DamageFxDto
                {
                    TargetPtr = targetPtr,
                    Amount = (long)JsonOverlay.GetDouble(merged, "amount"),
                    Tag = tag,
                    Fx = "float",
                    MergedCount = 1,
                });
                return;
            }

            case "meter":
            {
                if (UiPresent == null)
                {
                    _lastSkipped.Add(grant.GrantId + ":hud-runtime-missing");
                    return;
                }

                var targetPtr = ResolvePresentTargetPtr(ev);
                if (string.IsNullOrEmpty(targetPtr))
                {
                    _lastSkipped.Add(grant.GrantId + ":present-no-target");
                    return;
                }

                var meterId = JsonOverlay.GetString(merged, "meterId") ?? "";
                // §3: ratio's per-mille magnitude divides by 1000 exactly once, last, right here at
                // the boundary into the 0..1 ratio IUiPresentSink/ActorHudMeter carries.
                var ratio = JsonOverlay.GetDouble(merged, "ratio") / 1000.0;
                UiPresent.SetMeter(targetPtr, meterId, ratio);
                return;
            }

            case "banner":
            {
                if (UiPresent == null)
                {
                    _lastSkipped.Add(grant.GrantId + ":hud-runtime-missing");
                    return;
                }

                var bannerId = JsonOverlay.GetString(merged, "bannerId") ?? "";
                int? durationMs = merged.ContainsKey("durationMs")
                    ? JsonOverlay.GetInt(merged, "durationMs")
                    : null;
                UiPresent.ShowBanner(bannerId, durationMs);
                return;
            }

            default:
                // AtomKindRegistry.Validate's own op vocabulary already refuses this at bind time --
                // defence in depth, a named skip rather than a silent no-op if reached anyway.
                _lastSkipped.Add(grant.GrantId + ":present-unknown-op");
                return;
        }
    }

    /// <summary>Prefer event TargetPtr; if empty, use ActorPtr — the same precedence
    /// <c>InjectorEffectActionSink.ResolveStatusTargetPtr</c> uses for FA2/FA10.</summary>
    static string ResolvePresentTargetPtr(EffectEventDto ev)
    {
        if (!string.IsNullOrEmpty(ev.TargetPtr)) return ev.TargetPtr!;
        if (!string.IsNullOrEmpty(ev.ActorPtr)) return ev.ActorPtr!;
        return "";
    }

    public int TickDots()
    {
        var n = 0;
        if (Status != null)
        {
            // Gate 0: UtcNow() moved inside this branch — it now throws if unset (see the property's
            // own doc comment), and a caller with no Status wired must not pay for a clock it never
            // uses. Reading it unconditionally, as this used to, meant every TickDots() call — even
            // on a boardless/statusless harness — required a wired clock for a value it then discarded.
            var now = UtcNow();
            var sink = new StatusFunnelPulseSink(
                BoardSnapshot,
                new EffectEventDto { Trigger = EffectTriggers.OnTimer, ChainDepth = 0 },
                Funnel,
                CombatPolicy,
                CombatRng,
                CombatMath,
                _lastSkipped,
                effectId: null,
                pluginId: null,
                shieldGate: ShieldGate,
                actorResolve: ActorResolve);
            n = Status.Tick(now, sink, BoardSnapshot, StatusRng);
        }

        Funnel?.Flush();
        if (!_drainingOverlay)
            DrainOverlayProcs();
        return n;
    }

    void BindSelected(DamagePacket packet)
    {
        if (packet?.Target == null) return;
        if (!string.Equals(packet.Target.Mode, TargetModes.Selected, StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(SelectedPtr))
        {
            packet.Target.Mode = TargetModes.Single;
            packet.Target.Ptr = null;
            return;
        }

        packet.Target.Mode = TargetModes.Single;
        packet.Target.Ptr = CombatPtr.Normalize(SelectedPtr);
    }

    internal void NoteOverlayDamage(
        string? actorPtr,
        string targetPtr,
        long amount,
        int chainDepth,
        string? sourceGrantId,
        EffectEventDto? ev)
    {
        if (amount >= 0 || string.IsNullOrWhiteSpace(targetPtr)) return;
        _overlayProcs.Add(new OverlayProcNote
        {
            ActorPtr = actorPtr,
            TargetPtr = targetPtr,
            Amount = amount,
            ChainDepth = chainDepth,
            SourceGrantId = sourceGrantId,
            MatchKey = ev?.MatchKey,
            Side = ev?.Side,
            TypeId = ev?.TypeId,
            TargetTypeId = ev?.TargetTypeId,
            ScenarioId = ev?.ScenarioId
        });
    }

    void DrainOverlayProcs()
    {
        if (_drainingOverlay) return;
        _drainingOverlay = true;
        try
        {
            while (_overlayProcs.Count > 0)
            {
                var batch = _overlayProcs.ToList();
                _overlayProcs.Clear();
                foreach (var n in batch)
                {
                    OnEvent(new EffectEventDto
                    {
                        Trigger = EffectTriggers.OnDamageDealt,
                        MatchKey = n.MatchKey,
                        Side = n.Side,
                        ActorPtr = n.ActorPtr,
                        TargetPtr = n.TargetPtr,
                        TypeId = n.TypeId,
                        TargetTypeId = n.TargetTypeId,
                        Damage = Math.Abs(n.Amount),
                        Tick = 0,
                        ScenarioId = n.ScenarioId,
                        ChainDepth = n.ChainDepth + 1,
                        SourceGrantId = n.SourceGrantId
                    });
                }
            }
        }
        finally
        {
            _drainingOverlay = false;
        }
    }

    sealed class OverlayProcNote
    {
        public string? ActorPtr { get; init; }
        public string TargetPtr { get; init; } = "";
        public long Amount { get; init; }
        public int ChainDepth { get; init; }
        public string? SourceGrantId { get; init; }
        public string? MatchKey { get; init; }
        public string? Side { get; init; }
        public int? TypeId { get; init; }
        public int? TargetTypeId { get; init; }
        public string? ScenarioId { get; init; }
    }
}
