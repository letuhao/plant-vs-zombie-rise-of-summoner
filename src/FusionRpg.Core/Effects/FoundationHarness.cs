using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Effects;

/// <summary>Offline Foundation harness — Secondary/FE/Server Effect tests use this; never opens PVZ.</summary>
public sealed class FoundationHarness
{
    readonly FakeEffectClock _clock;
    readonly SeededEffectRandom _rng;
    readonly RecordingEffectSink _sink;
    readonly RecordingDamageFxSink _fx;
    readonly EffectBag _bag;
    readonly ActorDerivedLookup _derived = new();
    readonly ActorElementLookup _elements = new();
    readonly List<OverlayCombatBreakdown> _breakdowns = new();

    public FoundationHarness(int seed = 42)
    {
        _clock = new FakeEffectClock();
        _rng = new SeededEffectRandom(seed);
        _sink = new RecordingEffectSink();
        _fx = new RecordingDamageFxSink();
        var catalog = new InMemoryEffectCatalog();
        catalog.ReplaceAll(EffectAtomCatalog.CreateAll());
        var grants = new InMemoryEffectGrantStore();
        var proc = new EffectProcPolicy(_clock, _rng);
        _bag = new EffectBag(catalog, grants, proc, _sink);
        _bag.UtcNow = () => _clock.UtcNow;
        _bag.Status = new StatusRuntime(
            StatusCatalogBootstrap.CreateDefault(),
            (ptr, attackerLess) => _derived.Resolve(ptr, attackerLess));
        Funnel = new EffectFunnel(_bag, _fx);
    }

    public FoundationHarness WithCatalog(IEnumerable<EffectDef> defs)
    {
        _bag.Catalog.ReplaceAll(defs);
        return this;
    }

    public EffectBag Bag => _bag;
    public EffectFunnel Funnel { get; }
    public RecordingEffectSink Sink => _sink;
    public RecordingDamageFxSink Fx => _fx;
    public FakeEffectClock Clock => _clock;

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

    public void AdvanceTime(int ms)
    {
        _clock.AdvanceMs(ms);
        _bag.TickDots();
    }

    public void ClearAll()
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        _bag.ClearAll();
    }

    public EffectCatalogSnapshotDto Snapshot() => _bag.Snapshot();

    public Combat.BoardSnapshot BoardSnapshot
    {
        get => _bag.BoardSnapshot;
        set => _bag.BoardSnapshot = value ?? Combat.BoardSnapshot.Empty;
    }

    public void SetBoard(IEnumerable<Combat.BoardEntitySnap> entities) =>
        BoardSnapshot = new Combat.BoardSnapshot(entities);

    public void PinDerived(string ptr, ActorDerivedSnapshot snapshot) =>
        _derived.Pin(ptr, snapshot);

    public void PinElementTypes(string ptr, ActorElementTypes types) =>
        _elements.Pin(ptr, types);

    public IReadOnlyList<OverlayCombatBreakdown> CombatBreakdowns => _breakdowns;

    public FoundationHarness WithOverlayCombatMath(int combatSeed = 42)
    {
        _breakdowns.Clear();
        _bag.CombatRng = new SeededCombatRng(combatSeed);
        _bag.CombatMath = OverlayCombatMath.Create(
            ResolveCombatActor,
            ElementHub.Default,
            _bag.CombatRng,
            (breakdown, _, _) => _breakdowns.Add(breakdown));
        return this;
    }

    /// <summary>Shield runtime when <see cref="WithShieldGate"/> was called; null otherwise.</summary>
    public Combat.Shield.ShieldRuntime? ShieldRuntime { get; private set; }

    public FoundationHarness WithShieldGate()
    {
        ShieldRuntime = new Combat.Shield.ShieldRuntime();
        _bag.ShieldGate = new Combat.Shield.ShieldGate(ShieldRuntime, ResolveCombatActor);
        return this;
    }

    /// <summary>Grant a shield to a board ptr (normalized like dispatcher targets are).</summary>
    public Combat.Shield.ShieldApplyResult GrantShield(
        string ptr, long baseHp, ElementTypeId? element = null,
        int priority = Combat.Shield.ShieldPolicy.PrioritySkill,
        string sourceId = "test-shield", long? durationTicks = null, bool refillOnMerge = true)
    {
        if (ShieldRuntime == null)
            throw new InvalidOperationException("Call WithShieldGate() first.");
        var ownerKey = EffectOwnerKeys.Entity(CombatPtr.Normalize(ptr));
        return ShieldRuntime.Apply(new Combat.Shield.ShieldGrant
        {
            OwnerKey = ownerKey,
            SourceId = sourceId,
            Element = element,
            BaseHp = baseHp,
            Priority = priority,
            DurationTicks = durationTicks,
            RefillOnMerge = refillOnMerge
        }, _derived.Resolve(CombatPtr.Normalize(ptr), attackerLess: false), nowTick: 0);
    }

    CombatActorSnapshot ResolveCombatActor(string? ptr, bool attackerLess)
    {
        if (attackerLess)
            return CombatActorSnapshot.AttackerLess();
        return new CombatActorSnapshot(
            _derived.Resolve(ptr, attackerLess: false),
            _elements.Resolve(ptr));
    }

    public IntentPlanDto OnEvent(EffectEventDto ev)
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        _breakdowns.Clear();
        return _bag.OnEvent(ev);
    }

    public (IntentPlanDto Plan, IReadOnlyList<EffectFiredDto> Fired) Run(EffectEventDto ev)
    {
        var plan = OnEvent(ev);
        return (plan, _sink.Fired.ToList());
    }
}
