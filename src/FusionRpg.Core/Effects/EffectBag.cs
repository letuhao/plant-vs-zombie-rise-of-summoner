using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
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
        _defs[def.EffectId] = def;
        _revision++;
    }

    public void ReplaceAll(IEnumerable<EffectDef> defs)
    {
        _defs.Clear();
        foreach (var d in defs)
            _defs[d.EffectId] = d;
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

    public EffectGrant? Get(string grantId) =>
        _grants.TryGetValue(grantId, out var g) ? g : null;

    public IReadOnlyList<EffectGrant> All() =>
        _grants.Values.OrderByDescending(g => g.Priority).ThenBy(g => g.GrantId).ToList();

    public IReadOnlyList<EffectGrant> Matching(EffectEventDto ev) =>
        All().Where(g => EffectOwnerKey.MatchesEvent(g, ev)).ToList();

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

    public void Upsert(EffectGrant grant) => _grants[grant.GrantId] = grant;

    public bool Withdraw(string grantId) => _grants.Remove(grantId);

    public void Clear() => _grants.Clear();
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
    public CounterProcState Counters { get; } = new();
    public DoTTickScheduler Dots { get; } = new();
    public Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;
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
        Counters.ClearGrant(grantId);
        Dots.ClearGrant(grantId);
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
        Counters.Clear();
        Dots.Clear();
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

    public bool HasGrantForEffect(string effectId) =>
        _grants.All().Any(g => string.Equals(g.EffectId, effectId, StringComparison.OrdinalIgnoreCase));

    public bool HasAnyGrant() => _grants.All().Count > 0;

    public bool HasGrantWithTrigger(string trigger) =>
        _grants.All().Any(g =>
        {
            var def = _catalog.Get(g.EffectId);
            return def != null && def.Enabled &&
                   def.Triggers.Any(t => string.Equals(t, trigger, StringComparison.OrdinalIgnoreCase));
        });

    public IntentPlanDto OnEvent(EffectEventDto ev)
    {
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

            if (!_proc.TryPass(grant, trigger, out var skip))
            {
                _lastSkipped.Add(grant.GrantId + ":" + (skip ?? "proc"));
                return;
            }
        }

        var actions = def.Actions.OrderBy(a => a.Seq).ToList();
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

            if (string.Equals(action.Action, EffectActions.ApplyResourceDelta, StringComparison.OrdinalIgnoreCase))
            {
                var packet = DamagePacketBuilder.FromOverlay(
                    grant.Overlay,
                    ev,
                    grant.GrantId,
                    def.EffectId,
                    grant.PluginId);
                if (merged.ContainsKey("amount") && packet.SignedAmount == 0)
                    packet.SignedAmount = (long)JsonOverlay.GetDouble(merged, "amount");
                BindSelected(packet);

                var delivery = packet.Delivery.Mode ?? DeliveryModes.Instant;
                if (string.Equals(delivery, DeliveryModes.OverTime, StringComparison.OrdinalIgnoreCase))
                {
                    RegisterDot(packet, ev);
                    continue;
                }

                if (string.Equals(delivery, DeliveryModes.Counter, StringComparison.OrdinalIgnoreCase))
                {
                    TryFireCounterBurst(packet, grant, ev);
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
                    _lastSkipped);
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
        var every = packet.Delivery.EveryHits ?? 0;
        var scope = packet.Delivery.CounterScope ?? CounterScopes.Target;
        var scopeKey = string.Equals(scope, CounterScopes.Actor, StringComparison.OrdinalIgnoreCase)
            ? (ev.ActorPtr ?? "")
            : (ev.TargetPtr ?? "");
        if (!Counters.TryBurst(grant.GrantId, scopeKey, every, packet.Delivery.ResetOnBurst))
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
            _lastSkipped);
    }

    void RegisterDot(DamagePacket packet, EffectEventDto ev)
    {
        packet.ActorPtr ??= ev.ActorPtr;
        if (!CanArmDot(packet, ev))
        {
            _lastSkipped.Add((packet.SourceGrantId ?? "") + ":dot-no-target");
            return;
        }

        var period = CombatPolicy.ResolveDotPeriodMs(packet.Delivery.PeriodMs);
        var duration = CombatPolicy.ResolveDotDurationMs(packet.Delivery.DurationMs);
        var now = UtcNow();
        Dots.Register(new DoTEntry
        {
            GrantId = packet.SourceGrantId ?? "",
            Template = packet,
            Event = new EffectEventDto
            {
                Trigger = EffectTriggers.OnTimer,
                MatchKey = ev.MatchKey,
                Side = ev.Side,
                ActorPtr = ev.ActorPtr,
                TargetPtr = ev.TargetPtr,
                TypeId = ev.TypeId,
                TargetTypeId = ev.TargetTypeId,
                Tick = ev.Tick,
                ScenarioId = ev.ScenarioId,
                ChainDepth = packet.ChainDepth
            },
            TargetPtr = ev.TargetPtr ?? ev.ActorPtr ?? packet.Target?.Ptr ?? "",
            PeriodMs = period,
            DurationMs = duration,
            TickBudget = packet.Delivery.TickBudget is > 0 ? packet.Delivery.TickBudget.Value : 1,
            Start = now,
            Next = now.AddMilliseconds(period),
            End = now.AddMilliseconds(duration)
        });
    }

    static bool CanArmDot(DamagePacket packet, EffectEventDto ev)
    {
        var mode = packet.Target?.Mode ?? TargetModes.EventTarget;
        if (string.Equals(mode, TargetModes.EventTarget, StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(ev.TargetPtr);
        if (string.Equals(mode, TargetModes.Actor, StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(ev.ActorPtr);
        if (string.Equals(mode, TargetModes.Single, StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(packet.Target?.Ptr);
        if (string.Equals(mode, TargetModes.Selected, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
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
                        Damage = (int)Math.Min(int.MaxValue, Math.Abs(n.Amount)),
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

    public int TickDots()
    {
        var n = Dots.Tick(
            UtcNow(),
            BoardSnapshot,
            Funnel,
            CombatPolicy,
            CombatRng,
            CombatMath,
            _lastSkipped);
        Funnel?.Flush();
        if (!_drainingOverlay)
            DrainOverlayProcs();
        return n;
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
