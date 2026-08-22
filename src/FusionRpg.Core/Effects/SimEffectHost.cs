using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
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

    /// <param name="catalog">
    /// The defs this host runs. Null takes the seeded ones — E11 passes the compiled atom catalog
    /// instead, which is how the same fixture can be run down both paths and the plans compared.
    /// </param>
    public SimEffectHost(int seed = 42, string matchKey = "sim-match", IEnumerable<EffectDef>? catalog = null)
    {
        MatchKey = matchKey;
        _clock = new FakeEffectClock();
        _rng = new SeededEffectRandom(seed);
        _sink = new RecordingEffectSink();
        _fx = new RecordingDamageFxSink();
        var defs = new InMemoryEffectCatalog();
        defs.ReplaceAll(catalog ?? EffectSeedCatalog.CreateAll());
        _bag = new EffectBag(defs, new InMemoryEffectGrantStore(), new EffectProcPolicy(_clock, _rng), _sink);
        _bag.UtcNow = () => _clock.UtcNow;
        _bag.Status = new StatusRuntime(
            StatusCatalogBootstrap.CreateDefault(),
            (ptr, attackerLess) => _derived.Resolve(ptr, attackerLess));
        Funnel = new EffectFunnel(_bag, _fx);
        Plugins = EffectPluginHostFactory.Create(_bag);
    }

    public string MatchKey { get; set; }

    /// <summary>
    /// The Secondary runner (E15), once bindings are installed. Null until <see cref="UseRunner"/> —
    /// a host with no runner atoms should carry no runner state at all.
    /// </summary>
    public AtomRunner? Runner { get; private set; }

    /// <summary>Supplies HP/element/status facts the board snapshot does not carry. Optional.</summary>
    public Func<string, int>? HpMilliOf { get; set; }
    public Func<string, int>? ElementIdOf { get; set; }
    public Func<string, ulong>? StatusMaskOf { get; set; }

    /// <summary>
    /// Install runner bindings and build the index. The proc and apply streams are derived from one
    /// run seed, so a gate roll can never shift a magnitude roll (E2's named streams).
    /// </summary>
    public AtomRunner UseRunner(IEnumerable<RunnerBinding> bindings, ulong runSeed = 42)
    {
        var index = TriggerIndex.Build(bindings);
        Runner = new AtomRunner(
            Funnel, index,
            new AtomRandom(runSeed, AtomStreams.Proc),
            new AtomRandom(runSeed, AtomStreams.Apply),
            NowMs,
            MatchKey);
        return Runner;
    }

    /// <summary>The fake clock in milliseconds — the runner's only notion of time.</summary>
    public long NowMs() => _clock.UtcNow.ToUnixTimeMilliseconds();
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
        Runner?.BeginMatch(MatchKey);
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
        // The runner runs BEFORE the bag, not after. EffectBag.OnEvent calls Funnel.Flush() inside
        // itself, so a dispatch enqueued afterwards would sit in the mailbox until the next event —
        // a silent one-event lag on every proc. Secondary enqueues; the bag drains.
        //
        // It never touches the bag directly, so Foundation's path is untouched whether a runner
        // exists or not.
        Runner?.OnEvent(RunnerEventMapper.From(ev, BoardSnapshot, HpMilliOf, ElementIdOf, StatusMaskOf));
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
