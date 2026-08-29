using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Status;

public sealed class StatusInstance
{
    public string InstanceId { get; init; } = "";
    public string StatusId { get; init; } = "";
    public string HostPtr { get; init; } = "";
    public string? AttackerPtr { get; init; }
    public string GrantId { get; init; } = "";
    public string? EffectId { get; init; }
    public string? PluginId { get; init; }
    public string Family { get; init; } = "";
    public StatusKind Kind { get; init; }
    public double EffectiveMagnitude { get; set; }
    public double EffectiveDuration { get; set; }
    public DateTimeOffset AppliedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int PeriodMs { get; init; }
    public int TickBudget { get; init; } = 1;
    public int StatusIcdMs { get; init; }
    public double SpreadChance { get; init; }
    public string? SpreadStatusId { get; init; }
    public int SpreadMaxHops { get; init; }

    /// <summary>
    /// Timed stat contributions this instance makes while active (E17). Empty for every status that
    /// does not declare a <c>stat</c> payload, which is most of them.
    /// </summary>
    public IReadOnlyList<StatusStatMod> StatMods { get; init; } = Array.Empty<StatusStatMod>();

    /// <summary>
    /// Whether this status actually prevents acting (E17).
    ///
    /// <para>Copied from the def's <b>category</b>, not inferred from <see cref="Kind"/>.
    /// <c>StatusKind</c> conflates two axes — semantic role (Buff / Debuff / Meter / CrowdControl)
    /// and execution path (<c>UnityCc</c>) — so "is this crowd control" cannot be read off it. Nine
    /// statuses carry a CC-ish kind and exactly one, <c>poison</c>, is categorised <c>dot</c>: it is
    /// damage over time that happens to execute through a Unity method, and it was locking actors
    /// out of their turn in battle because of how it is delivered rather than what it does.</para>
    /// </summary>
    public bool IsCrowdControl { get; init; }

    /// <summary>Copied from <see cref="StatusDef.PulseHealsAttacker"/> at apply time (spec-healing-pair.md
    /// §3) — leech's heal half. True only for <c>leech</c>.</summary>
    public bool PulseHealsAttacker { get; init; }
    public int SpreadIcdMs { get; init; }
    public TargetSpec? SpreadTarget { get; init; }
    public int HopDepth { get; init; }
    public DateTimeOffset NextPulse { get; set; }
    public DateTimeOffset LastApplied { get; set; }
    public DateTimeOffset LastSpread { get; set; }
    public int PulsesFired { get; set; }
}

public sealed record StatusApplyInput(
    string StatusId,
    string HostPtr,
    string? AttackerPtr,
    string GrantId,
    double BaseMagnitude,
    double BaseDuration,
    int PeriodMs = 1000,
    int DurationMs = 5000,
    int StatusIcdMs = 0,
    int TickBudget = 1,
    double GrantChance = 1.0,
    string? EffectId = null,
    string? PluginId = null,
    bool AttackerLess = false,
    IReadOnlyList<string>? ImmunityTags = null,
    double SpreadChance = 0,
    string? SpreadStatusId = null,
    int SpreadMaxHops = 0,
    int SpreadIcdMs = 0,
    TargetSpec? SpreadTarget = null,
    int HopDepth = 0,
    IReadOnlyList<StatusStatMod>? StatMods = null);

public sealed record StatusApplyOutcome(
    bool Applied,
    StatusResistReason? ResistReason,
    StatusInstance? Instance,
    StatusApplyResult EvalResult);

public interface IStatusPulseSink
{
    void PulseHp(StatusInstance instance, double amount);

    /// <summary>
    /// Leech's heal half (spec-healing-pair.md §3) — a SEPARATE signed packet to the attacker, not a
    /// negative damage folded into the same one. Default no-op so every existing implementer (test
    /// harnesses, <see cref="Battle.BattleEngine"/>) is unaffected unless it opts in; only
    /// <see cref="StatusFunnelPulseSink"/> — the real Funnel dispatch path — overrides it.
    /// </summary>
    void PulseHealAttacker(StatusInstance instance, double baseHealAmount) { }
}

public delegate ActorDerivedSnapshot ActorDerivedResolve(string? entityPtr, bool attackerLess);

/// <summary>L2 status instance bag — single SSOT per entity:{ptr}.</summary>
public sealed class StatusRuntime
{
    readonly StatusCatalog _catalog;
    readonly ResistanceEvaluator _evaluator = new();
    readonly ActorDerivedResolve _resolveDerived;
    readonly Dictionary<string, List<StatusInstance>> _byHost = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _counterHits = new(StringComparer.OrdinalIgnoreCase);
    readonly List<StatusResistedEvent> _resisted = new();
    int _instanceSeq;

    public Action<StatusResistedEvent>? OnResisted { get; set; }

    /// <summary>Fires on every definitive apply (spread hops included) — VFX cue producer seam (SPEC W5).</summary>
    public Action<StatusInstance>? OnApplied { get; set; }

    /// <summary>
    /// Fires when an instance definitively ENDS mid-life: expiry prune, ClearGrant, family mutex.
    /// Deliberately silent on Refresh/Replace re-applies (would flicker sustained visuals),
    /// WithdrawEntity (host death — VFX reaps via anchor), and Clear (match teardown — VFX ClearAll).
    /// Handlers must only enqueue — the expiry prune can run mid-spread (vfx-v3 V1).
    /// </summary>
    public Action<StatusInstance>? OnEnded { get; set; }

    public IReadOnlyList<StatusResistedEvent> ResistedEvents => _resisted;

    public IReadOnlyDictionary<string, int> CounterSnapshot() =>
        new Dictionary<string, int>(_counterHits, StringComparer.OrdinalIgnoreCase);

    public StatusRuntime(StatusCatalog catalog, ActorDerivedResolve resolveDerived)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _resolveDerived = resolveDerived ?? throw new ArgumentNullException(nameof(resolveDerived));
    }

    public StatusCatalog Catalog => _catalog;

    public IReadOnlyList<StatusInstance> ForHost(string hostPtr)
    {
        if (!_byHost.TryGetValue(hostPtr, out var list))
            return Array.Empty<StatusInstance>();
        return list.ToList();
    }

    public IReadOnlyList<StatusInstance> AllInstances() =>
        _byHost.Values.SelectMany(v => v).ToList();

    /// <summary>Allocation-free liveness probe — checked per frame by the DoT tick.</summary>
    public bool HasAnyInstances()
    {
        foreach (var list in _byHost.Values)
        {
            if (list.Count > 0) return true;
        }
        return false;
    }

    public StatusApplyOutcome Apply(StatusApplyInput input, IStatusRng rng, DateTimeOffset now)
    {
        var def = _catalog.GetRequired(input.StatusId);

        if (IsStatusIcdBlocked(input, now, out _))
        {
            return new StatusApplyOutcome(false, StatusResistReason.StatusIcd, null,
                new StatusApplyResult(false, StatusResistReason.StatusIcd, 0, 0, 0, 0, 0, 0, 0));
        }

        var attacker = _resolveDerived(input.AttackerPtr, input.AttackerLess);
        var defender = _resolveDerived(input.HostPtr, false);

        var eval = _evaluator.Evaluate(
            new StatusApplyRequest(
                input.StatusId,
                input.HostPtr,
                input.AttackerPtr,
                input.BaseMagnitude,
                input.BaseDuration,
                input.GrantChance,
                input.AttackerLess,
                input.ImmunityTags),
            input.AttackerLess ? null : attacker,
            defender,
            rng,
            statusElement: def.Element);

        if (!eval.Applied)
        {
            RecordResisted(input, eval, now);
            return new StatusApplyOutcome(false, eval.ResistReason, null, eval);
        }

        ApplyFamilyMutex(input.HostPtr, def, now);

        var durationMs = input.DurationMs > 0
            ? (int)Math.Round(input.BaseDuration > 0 ? eval.EffectiveDuration : input.DurationMs)
            : (int)Math.Round(eval.EffectiveDuration);
        if (durationMs <= 0 && def.Kind != StatusKind.Counter)
            durationMs = input.DurationMs;

        var instance = new StatusInstance
        {
            InstanceId = $"{input.GrantId}:{input.StatusId}:{++_instanceSeq}",
            StatusId = input.StatusId,
            HostPtr = input.HostPtr,
            AttackerPtr = input.AttackerPtr,
            GrantId = input.GrantId,
            EffectId = input.EffectId,
            PluginId = input.PluginId,
            Family = def.Family,
            Kind = def.Kind,
            EffectiveMagnitude = eval.EffectiveMagnitude,
            EffectiveDuration = eval.EffectiveDuration,
            AppliedAt = now,
            ExpiresAt = durationMs > 0 ? now.AddMilliseconds(durationMs) : DateTimeOffset.MaxValue,
            PeriodMs = input.PeriodMs,
            TickBudget = input.TickBudget > 0 ? input.TickBudget : 1,
            StatusIcdMs = input.StatusIcdMs,
            SpreadChance = input.SpreadChance,
            SpreadStatusId = input.SpreadStatusId,
            SpreadMaxHops = input.SpreadMaxHops,
            SpreadIcdMs = input.SpreadIcdMs,
            SpreadTarget = input.SpreadTarget,
            HopDepth = input.HopDepth,
            StatMods = input.StatMods ?? Array.Empty<StatusStatMod>(),
            IsCrowdControl = def.Categories.Contains(StatusL2bCategory.Cc),
            PulseHealsAttacker = def.PulseHealsAttacker,
            NextPulse = input.PeriodMs > 0 ? now.AddMilliseconds(input.PeriodMs) : DateTimeOffset.MaxValue,
            LastApplied = now,
            LastSpread = DateTimeOffset.MinValue
        };

        UpsertInstance(input.HostPtr, instance, def.Stacking);
        OnApplied?.Invoke(instance);
        return new StatusApplyOutcome(true, null, instance, eval);
    }

    void UpsertInstance(string hostPtr, StatusInstance instance, StatusStacking stacking)
    {
        if (!_byHost.TryGetValue(hostPtr, out var list))
        {
            list = new List<StatusInstance>();
            _byHost[hostPtr] = list;
        }

        if (stacking == StatusStacking.Refresh)
        {
            var idx = list.FindIndex(i =>
                string.Equals(i.StatusId, instance.StatusId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.GrantId, instance.GrantId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                list[idx] = instance;
            else
                list.Add(instance);
            return;
        }

        if (stacking == StatusStacking.Replace)
        {
            list.RemoveAll(i =>
                string.Equals(i.StatusId, instance.StatusId, StringComparison.OrdinalIgnoreCase));
            list.Add(instance);
            return;
        }

        list.Add(instance);
    }

    void ApplyFamilyMutex(string hostPtr, StatusDef def, DateTimeOffset now)
    {
        if (!string.Equals(def.Family, "elemental", StringComparison.OrdinalIgnoreCase))
            return;
        if (!_byHost.TryGetValue(hostPtr, out var list))
            return;
        List<StatusInstance>? displaced = null;
        foreach (var i in list)
        {
            if (string.Equals(i.Family, "elemental", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(i.StatusId, def.StatusId, StringComparison.OrdinalIgnoreCase))
                (displaced ??= new List<StatusInstance>()).Add(i);
        }

        if (displaced == null) return;
        list.RemoveAll(i =>
            string.Equals(i.Family, "elemental", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(i.StatusId, def.StatusId, StringComparison.OrdinalIgnoreCase));
        foreach (var e in displaced)
            OnEnded?.Invoke(e);
    }

    bool IsStatusIcdBlocked(StatusApplyInput input, DateTimeOffset now, out StatusInstance? existing)
    {
        existing = null;
        if (input.StatusIcdMs <= 0 || !_byHost.TryGetValue(input.HostPtr, out var list))
            return false;
        existing = list.FirstOrDefault(i =>
            string.Equals(i.StatusId, input.StatusId, StringComparison.OrdinalIgnoreCase));
        if (existing == null) return false;
        return (now - existing.LastApplied).TotalMilliseconds < input.StatusIcdMs;
    }

    public int Tick(
        DateTimeOffset now,
        IStatusPulseSink? sink,
        Combat.BoardSnapshot? board = null,
        IStatusRng? spreadRng = null)
    {
        var rng = spreadRng ?? new FixedStatusRng(0);
        var pulses = 0;

        // Ordinal-sorted, not raw dictionary order: host order decides the order pulses reach
        // the shield gate, which decides the order shield.absorbed aggregates land in a
        // BattleReport. Battle advertises byte-identical replay, so that ordering must come
        // from us and not from Dictionary's internals (mirrors ShieldRuntime.Tick's own sort).
        var hosts = _byHost.Keys.ToList();
        hosts.Sort(StringComparer.Ordinal);
        foreach (var host in hosts)
        {
            if (!_byHost.TryGetValue(host, out var list)) continue;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var inst = list[i];
                if (inst.ExpiresAt < now)
                {
                    list.RemoveAt(i);
                    OnEnded?.Invoke(inst);
                    continue;
                }

                if (inst.Kind != StatusKind.OverTime && inst.Kind != StatusKind.Contagion)
                    continue;
                if (inst.PeriodMs <= 0) continue;
                if (now < inst.NextPulse) continue;

                var budget = inst.TickBudget > 0 ? inst.TickBudget : 1;
                var firedThisPulse = 0;
                while (now >= inst.NextPulse && inst.NextPulse <= inst.ExpiresAt && firedThisPulse < budget)
                {
                    sink?.PulseHp(inst, inst.EffectiveMagnitude);

                    // spec-healing-pair.md §3: leech's heal half, a SEPARATE signed packet to the
                    // attacker -- never folded into the damage pulse above (§4.3's "one mailbox" rule
                    // is about the mailbox, not about merging two effects into one packet). No one to
                    // heal on an attacker-less application; skip rather than invent a target.
                    if (inst.PulseHealsAttacker && !string.IsNullOrWhiteSpace(inst.AttackerPtr))
                        sink?.PulseHealAttacker(inst, Math.Abs(inst.EffectiveMagnitude));

                    inst.PulsesFired++;
                    firedThisPulse++;
                    pulses++;
                    inst.NextPulse = inst.NextPulse.AddMilliseconds(inst.PeriodMs);

                    if (inst.Kind == StatusKind.Contagion
                        && inst.SpreadChance > 0
                        && board != null
                        && inst.SpreadMaxHops > 0
                        && inst.HopDepth < inst.SpreadMaxHops)
                    {
                        TrySpread(inst, board, rng, now);
                    }
                }

                continue;
            }

            if (list.Count == 0)
                _byHost.Remove(host);
        }

        return pulses;
    }

    public void WithdrawEntity(string hostPtr)
    {
        if (string.IsNullOrWhiteSpace(hostPtr)) return;
        _byHost.Remove(hostPtr.Trim());
    }

    public void ClearGrant(string grantId)
    {
        if (string.IsNullOrWhiteSpace(grantId)) return;
        List<StatusInstance>? ended = null;
        foreach (var host in _byHost.Keys.ToList())
        {
            if (!_byHost.TryGetValue(host, out var list)) continue;
            foreach (var i in list)
            {
                if (string.Equals(i.GrantId, grantId, StringComparison.OrdinalIgnoreCase))
                    (ended ??= new List<StatusInstance>()).Add(i);
            }

            list.RemoveAll(i => string.Equals(i.GrantId, grantId, StringComparison.OrdinalIgnoreCase));
            if (list.Count == 0)
                _byHost.Remove(host);
        }

        if (ended != null)
        {
            foreach (var e in ended)
                OnEnded?.Invoke(e);
        }

        var prefix = grantId.Trim() + "|";
        foreach (var key in _counterHits.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            _counterHits.Remove(key);
    }

    /// <summary>
    /// Counter delivery hit meter — returns true when burst threshold reached.
    /// <paramref name="hits"/> supports v2 coalesced records (N merged hits advance by N,
    /// one burst per call on crossing — event-pipeline-v2-spec.md decision #2).
    /// </summary>
    public bool RecordCounterHit(string grantId, string scopeKey, int everyHits, bool resetOnBurst, int hits = 1)
    {
        if (everyHits <= 0 || string.IsNullOrWhiteSpace(grantId)) return false;
        if (hits <= 0) return false;
        var key = grantId.Trim() + "|" + (scopeKey ?? "").Trim();
        _counterHits.TryGetValue(key, out var n);
        n += hits;
        if (n >= everyHits)
        {
            // I3 (review): keep the residual (n % everyHits) instead of zeroing — merged
            // records must not eat progress toward the next burst. hits=1 unchanged (0).
            if (resetOnBurst) _counterHits[key] = n % everyHits;
            else _counterHits[key] = n;
            return true;
        }

        _counterHits[key] = n;
        return false;
    }

    public void Clear() {
        _byHost.Clear();
        _counterHits.Clear();
        _resisted.Clear();
    }

    void RecordResisted(StatusApplyInput input, StatusApplyResult eval, DateTimeOffset now)
    {
        if (eval.ResistReason is null or StatusResistReason.StatusIcd) return;
        var ev = new StatusResistedEvent(
            input.StatusId,
            input.HostPtr,
            input.AttackerPtr,
            input.GrantId,
            eval.ResistReason.Value,
            eval.Delta,
            now);
        _resisted.Add(ev);
        if (_resisted.Count > 256)
            _resisted.RemoveAt(0);
        OnResisted?.Invoke(ev);
    }

    void TrySpread(StatusInstance source, BoardSnapshot board, IStatusRng rng, DateTimeOffset now)
    {
        var hop = source.HopDepth + 1;
        var hopLimit = source.SpreadMaxHops > 0
            ? Math.Min(source.SpreadMaxHops, StatusPolicy.ProcDepthLimitDefault)
            : StatusPolicy.ProcDepthLimitDefault;
        if (hop > hopLimit)
            return;

        if (source.SpreadIcdMs > 0
            && source.LastSpread > DateTimeOffset.MinValue
            && (now - source.LastSpread).TotalMilliseconds < source.SpreadIcdMs)
            return;

        var spreadId = string.IsNullOrWhiteSpace(source.SpreadStatusId)
            ? source.StatusId
            : source.SpreadStatusId!;
        var candidates = StatusSpread.ResolveCandidates(source, source.SpreadTarget, board);
        if (candidates.Count == 0) return;

        var template = new StatusApplyInput(
            spreadId,
            "",
            source.AttackerPtr,
            source.GrantId,
            source.EffectiveMagnitude,
            source.EffectiveDuration,
            source.PeriodMs,
            (int)Math.Max(0, (source.ExpiresAt - now).TotalMilliseconds),
            source.StatusIcdMs,
            source.TickBudget,
            GrantChance: 1.0,
            source.EffectId,
            source.PluginId,
            SpreadChance: source.SpreadChance,
            SpreadStatusId: spreadId,
            SpreadMaxHops: source.SpreadMaxHops,
            SpreadIcdMs: source.SpreadIcdMs,
            SpreadTarget: source.SpreadTarget,
            HopDepth: hop);

        var outcomes = StatusSpread.Execute(
            this,
            new StatusSpreadRequest(source, candidates, hop, source.SpreadChance, template),
            rng,
            now);
        if (outcomes.Count > 0)
            source.LastSpread = now;
    }
}
