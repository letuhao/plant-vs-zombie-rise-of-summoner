using FusionRpg.Contracts;
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
            var def = _catalog.Get(grant.EffectId);
            if (def == null || !def.Enabled) continue;
            FireGrant(grant, def, ev, forceTrigger: null, planned);
        }

        Funnel?.Flush();
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
}
