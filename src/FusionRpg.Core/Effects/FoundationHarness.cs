using FusionRpg.Contracts;
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

    public FoundationHarness(int seed = 42)
    {
        _clock = new FakeEffectClock();
        _rng = new SeededEffectRandom(seed);
        _sink = new RecordingEffectSink();
        _fx = new RecordingDamageFxSink();
        var catalog = new InMemoryEffectCatalog();
        catalog.ReplaceAll(EffectSeedCatalog.CreateAll());
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

    public IntentPlanDto OnEvent(EffectEventDto ev)
    {
        _sink.Items.Clear();
        _sink.Fired.Clear();
        _fx.Items.Clear();
        return _bag.OnEvent(ev);
    }

    public (IntentPlanDto Plan, IReadOnlyList<EffectFiredDto> Fired) Run(EffectEventDto ev)
    {
        var plan = OnEvent(ev);
        return (plan, _sink.Fired.ToList());
    }
}

public static class EffectSeedCatalog
{
    public static IReadOnlyList<EffectDef> CreateAll() => new List<EffectDef>
    {
        ButterOnHit(),
        FreezeOnHit(),
        ColdOnHit(),
        PoisonOnHit(),
        ClearButter(),
        PassiveAtkFlat(),
        SpawnZombieOnDeath(),
        SpawnPlantBullet(),
        BoardCherry(),
        SpawnAndClearGrid(),
        SetDirtBox(),
        EconomySun(),
        IcdButter(),
        OnSpawnButter(),
        OverlayDamage()
    };

    public static EffectDef ButterOnHit() => Triggered(
        "fx.butter_on_hit", "Butter on hit", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "butter", ["duration"] = 4f });

    public static EffectDef FreezeOnHit() => Triggered(
        "fx.freeze_on_hit", "Freeze on hit", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "freeze", ["duration"] = 3f });

    public static EffectDef ColdOnHit() => Triggered(
        "fx.cold_on_hit", "Cold on hit", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "cold", ["duration"] = 5f });

    public static EffectDef PoisonOnHit() => Triggered(
        "fx.poison_on_hit", "Poison on hit", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "poison", ["duration"] = 5f });

    public static EffectDef ClearButter() => Triggered(
        "fx.clear_butter", "Clear butter", EffectTriggers.OnDamageDealt,
        EffectActions.ClearStatus, new Dictionary<string, object?> { ["status"] = "butter" });

    public static EffectDef PassiveAtkFlat() => new()
    {
        EffectId = "fx.passive_atk_flat",
        EffectType = EffectTypes.Passive,
        Name = "Passive ATK +10",
        Enabled = true,
        SourceTag = "seed",
        Triggers = new List<string> { EffectTriggers.OnGranted, EffectTriggers.OnRemoved },
        Actions = new List<EffectActionRow>
        {
            new()
            {
                Seq = 1,
                Action = EffectActions.ModifyStat,
                Params = new Dictionary<string, object?>
                {
                    ["channel"] = "atk",
                    ["flat"] = 10.0
                }
            }
        }
    };

    public static EffectDef SpawnZombieOnDeath() => Triggered(
        "fx.spawn_zombie_ondeath", "Spawn zombie on death", EffectTriggers.OnDeath,
        EffectActions.SpawnEntity, new Dictionary<string, object?>
        {
            ["kind"] = "zombie",
            ["typeId"] = 0,
            ["hp"] = 100,
            ["maxHp"] = 100
        });

    public static EffectDef SpawnPlantBullet() => new()
    {
        EffectId = "fx.spawn_plant_bullet",
        EffectType = EffectTypes.Triggered,
        Name = "Spawn plant + bullet",
        Enabled = true,
        SourceTag = "seed",
        Triggers = new List<string> { EffectTriggers.OnDamageDealt },
        Actions = new List<EffectActionRow>
        {
            new()
            {
                Seq = 1,
                Action = EffectActions.SpawnEntity,
                Params = new Dictionary<string, object?> { ["kind"] = "plant", ["typeId"] = 0, ["row"] = 2, ["col"] = 3 }
            },
            new()
            {
                Seq = 2,
                Action = EffectActions.SpawnEntity,
                Params = new Dictionary<string, object?> { ["kind"] = "bullet", ["typeId"] = 0, ["row"] = 2, ["x"] = 400 }
            }
        }
    };

    public static EffectDef BoardCherry() => Triggered(
        "fx.board_cherry", "Board cherry bomb", EffectTriggers.OnDamageDealt,
        EffectActions.BoardAction, new Dictionary<string, object?> { ["op"] = "cherry", ["row"] = 2, ["col"] = 4 });

    public static EffectDef SpawnAndClearGrid() => new()
    {
        EffectId = "fx.grid_item_cycle",
        EffectType = EffectTypes.Triggered,
        Name = "Spawn + clear grid item",
        Enabled = true,
        SourceTag = "seed",
        Triggers = new List<string> { EffectTriggers.OnDamageDealt },
        Actions = new List<EffectActionRow>
        {
            new()
            {
                Seq = 1,
                Action = EffectActions.SpawnGridItem,
                Params = new Dictionary<string, object?> { ["gridItemType"] = 7, ["row"] = 2, ["col"] = 3 }
            },
            new()
            {
                Seq = 2,
                Action = EffectActions.ClearGridItem,
                Params = new Dictionary<string, object?> { ["selector"] = "last", ["gridItemType"] = 7 }
            }
        }
    };

    public static EffectDef SetDirtBox() => Triggered(
        "fx.set_dirt_box", "Set dirt box", EffectTriggers.OnDamageDealt,
        EffectActions.SetBoxType, new Dictionary<string, object?> { ["boxType"] = 1, ["row"] = 2, ["col"] = 3 });

    public static EffectDef EconomySun() => Triggered(
        "fx.economy_sun", "Add sun", EffectTriggers.OnDamageDealt,
        EffectActions.Economy, new Dictionary<string, object?> { ["currency"] = "sun", ["op"] = "add", ["amount"] = 25 });

    public static EffectDef IcdButter() => Triggered(
        "fx.icd_butter", "ICD butter", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "butter", ["duration"] = 2f });

    public static EffectDef OnSpawnButter() => Triggered(
        "fx.spawn_butter", "Butter on spawn", EffectTriggers.OnSpawn,
        EffectActions.ApplyStatus, new Dictionary<string, object?> { ["status"] = "butter", ["duration"] = 3f });

    public static EffectDef OverlayDamage() => Triggered(
        "fx.overlay_damage", "Overlay HP delta", EffectTriggers.OnDamageDealt,
        EffectActions.ApplyResourceDelta, new Dictionary<string, object?> { ["channel"] = "hp" });

    static EffectDef Triggered(string id, string name, string trigger, string action, Dictionary<string, object?> p) => new()
    {
        EffectId = id,
        EffectType = EffectTypes.Triggered,
        Name = name,
        Enabled = true,
        SourceTag = "seed",
        Triggers = new List<string> { trigger },
        Actions = new List<EffectActionRow> { new() { Seq = 1, Action = action, Params = p } }
    };
}
