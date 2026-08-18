using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Plugins;
using FusionRpg.Core.Status;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Effects;

/// <summary>
/// Offline Effect simulator for Secondary/FE/Server — EffectBag + RecordingSink, no Unity.
/// </summary>
public sealed class SimEffectHost
{
    readonly FakeEffectClock _clock;
    readonly SeededEffectRandom _rng;
    readonly RecordingEffectSink _sink;
    readonly RecordingDamageFxSink _fx;
    readonly EffectBag _bag;
    readonly ActorDerivedLookup _derived = new();
    long _tick;

    public SimEffectHost(int seed = 42, string matchKey = "sim-match")
    {
        MatchKey = matchKey;
        _clock = new FakeEffectClock();
        _rng = new SeededEffectRandom(seed);
        _sink = new RecordingEffectSink();
        _fx = new RecordingDamageFxSink();
        var catalog = new InMemoryEffectCatalog();
        catalog.ReplaceAll(EffectSeedCatalog.CreateAll());
        _bag = new EffectBag(catalog, new InMemoryEffectGrantStore(), new EffectProcPolicy(_clock, _rng), _sink);
        _bag.UtcNow = () => _clock.UtcNow;
        _bag.Status = new StatusRuntime(
            StatusCatalogBootstrap.CreateDefault(),
            (ptr, attackerLess) => _derived.Resolve(ptr, attackerLess));
        Funnel = new EffectFunnel(_bag, _fx);
        Plugins = EffectPluginHostFactory.Create(_bag);
    }

    public string MatchKey { get; set; }
    public EffectPluginHost Plugins { get; }
    public EffectBag Bag => _bag;
    public EffectFunnel Funnel { get; }
    public RecordingEffectSink Sink => _sink;
    public RecordingDamageFxSink Fx => _fx;
    public FakeEffectClock Clock => _clock;
    public long Tick => _tick;

    public SimEffectHost WithCatalog(IEnumerable<EffectDef> defs)
    {
        _bag.Catalog.ReplaceAll(defs);
        return this;
    }

    /// <summary>Bag-only clear — does not run Secondary plugin end hooks (use <see cref="EndMatch"/>).</summary>
    public void ClearAll()
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        _bag.ClearAll();
    }

    /// <summary>Match start: set key → ClearAll → plugin grants (mirrors LIVE board.start order).</summary>
    public void BeginMatch(string? matchKey = null)
    {
        if (!string.IsNullOrWhiteSpace(matchKey))
            MatchKey = matchKey;
        ClearAll();
        Plugins.NotifyMatchStart(MatchKey);
    }

    /// <summary>Match end: plugin withdraw → ClearAll (mirrors LIVE board.end / match.result order).</summary>
    public void EndMatch()
    {
        Plugins.NotifyRemoved(MatchKey);
        ClearAll();
    }

    public EffectGrant Grant(EffectGrantDto dto)
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        return _bag.Grant(dto);
    }

    public bool Withdraw(string grantId)
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        return _bag.Withdraw(grantId);
    }

    public void AdvanceMs(int ms)
    {
        _clock.AdvanceMs(ms);
        _bag.TickDots();
    }

    public long NextTick() => Interlocked.Increment(ref _tick);

    public Combat.BoardSnapshot BoardSnapshot
    {
        get => _bag.BoardSnapshot;
        set => _bag.BoardSnapshot = value ?? Combat.BoardSnapshot.Empty;
    }

    public void SetBoard(IEnumerable<Combat.BoardEntitySnap> entities) =>
        BoardSnapshot = new Combat.BoardSnapshot(entities);

    public void PinDerived(string ptr, ActorDerivedSnapshot snapshot) =>
        _derived.Pin(ptr, snapshot);

    public IntentPlanDto OnEvent(EffectEventDto ev)
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        if (string.IsNullOrEmpty(ev.MatchKey)) ev.MatchKey = MatchKey;
        if (ev.Tick <= 0) ev.Tick = NextTick();
        return _bag.OnEvent(ev);
    }

    public IntentPlanDto? FireFromCapture(string kind, Dictionary<string, object> payload)
    {
        var tick = NextTick();
        var ev = EffectEventAdapterCore.TryMap(kind, payload, tick, MatchKey);
        if (ev == null) return null;
        return OnEvent(ev);
    }

    public IntentPlanDto HitDealt(
        string actorPtr = "0xA",
        string targetPtr = "0xB",
        string attackerSide = "plant",
        int? typeId = 0,
        int? targetTypeId = 0,
        int damage = 20)
    {
        return OnEvent(new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            MatchKey = MatchKey,
            Side = attackerSide,
            ActorPtr = actorPtr,
            TargetPtr = targetPtr,
            TypeId = typeId,
            TargetTypeId = targetTypeId,
            Damage = damage,
            Tick = NextTick()
        });
    }

    public IntentPlanDto Die(
        string side = "zombie",
        string ptr = "0xZ",
        int? typeId = 0,
        string? killerPtr = "0xK")
    {
        // Mirror LIVE: OnDeath while entity grants live, then withdraw entity:{ptr}.
        var plan = OnEvent(new EffectEventDto
        {
            Trigger = EffectTriggers.OnDeath,
            MatchKey = MatchKey,
            Side = side,
            ActorPtr = ptr,
            TargetPtr = ptr,
            TypeId = typeId,
            KillerPtr = killerPtr,
            Tick = NextTick()
        });
        _bag.WithdrawForOwner(null, EffectOwnerKeys.Entity(ptr));
        return plan;
    }

    public IntentPlanDto Spawn(string side = "plant", string ptr = "0xP", int? typeId = 0)
    {
        return OnEvent(new EffectEventDto
        {
            Trigger = EffectTriggers.OnSpawn,
            MatchKey = MatchKey,
            Side = side,
            ActorPtr = ptr,
            TypeId = typeId,
            Tick = NextTick()
        });
    }

    public EffectCatalogSnapshotDto Snapshot() => _bag.Snapshot();
}
