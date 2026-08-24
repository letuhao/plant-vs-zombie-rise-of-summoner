namespace FusionRpg.Core.Progression;

public static class RpgActorKinds
{
    public const string Player = "player";
    public const string Plant = "plant";
    public const string Zombie = "zombie";

    public static bool IsKnown(string? kind) =>
        kind is Player or Plant or Zombie;
}

public static class RpgXpReasons
{
    public const string Kill = "kill";
    public const string Defeat = "defeat";
    public const string Mower = "mower";
    public const string PlantPlace = "plant_place";
    public const string ZombieSpawn = "zombie_spawn";
}

/// <summary>Arithmetic XP curve per actor kind (POC-tuned; faster early levels). Config-backed
/// (tunables-ssot.md T1) — data/tuning/progression.v1.json's xpCurve.</summary>
public static class RpgXpCurve
{
    static ProgressionTuning? _tuning;

    public static void Configure(ProgressionTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static ProgressionTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "RpgXpCurve.Configure(...) has not run. Every XP curve reads " +
        "data/tuning/progression.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static (double First, double Step) ParamsFor(string kind) => kind switch
    {
        RpgActorKinds.Plant => (Tuning.PlantCurve.First, Tuning.PlantCurve.Step),
        RpgActorKinds.Zombie => (Tuning.ZombieCurve.First, Tuning.ZombieCurve.Step),
        // player — first match clears L1; mid-game paces ~L12–18 / 20 wins
        _ => (Tuning.PlayerCurve.First, Tuning.PlayerCurve.Step)
    };

    public static double XpToNext(string kind, long level)
    {
        if (level < 1) level = 1;
        var (first, step) = ParamsFor(kind);
        var need = first + (level - 1) * step;
        if (double.IsNaN(need) || double.IsInfinity(need) || need < 1.0)
            return 1.0;
        return need;
    }

    public static double TotalToReach(string kind, long level)
    {
        if (level <= 1) return 0;
        var (first, step) = ParamsFor(kind);
        // sum_{i=0}^{L-2} (first + i*step)
        var n = level - 1;
        return n * (2 * first + (n - 1) * step) / 2.0;
    }
}

/// <summary>Base award deltas (POC-tuned). Kill is multiplied by a power scale (T3.3: the stub class
/// that used to carry this is deleted — see RpgXpAwardMap.NoKillPowerScaleYet).
/// Config-backed (tunables-ssot.md T1) — data/tuning/progression.v1.json's awards. Not a `const`:
/// RpgXpAwardMapTests' [InlineData] rows hardcode the current values instead (attributes require
/// compile-time constants), asserted separately against the live value.</summary>
public static class RpgXpAwards
{
    static ProgressionTuning? _tuning;

    public static void Configure(ProgressionTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static XpAwardsTuning Tuning => (_tuning ?? throw new InvalidOperationException(
        "RpgXpAwards.Configure(...) has not run. Every award reads data/tuning/progression.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.")).Awards;

    public static double Kill => Tuning.Kill;
    public static double Defeat => Tuning.Defeat;
    public static double Mower => Tuning.Mower;
    public static double PlantPlace => Tuning.PlantPlace;
    public static double ZombieSpawn => Tuning.ZombieSpawn;
}

public sealed class RpgActorState
{
    public long Level { get; set; } = 1;
    public double Xp { get; set; }
    public long HighestLevel { get; set; } = 1;
    public long DemotionCount { get; set; }
    public long Revision { get; set; }
}

public sealed class LevelChangeEvent
{
    public long PlayerId { get; init; }
    public string Kind { get; init; } = RpgActorKinds.Player;
    public int TypeId { get; init; }
    public long LevelBefore { get; init; }
    public long LevelAfter { get; init; }
    public double XpAfter { get; init; }
    public long DemotionCount { get; init; }
    public string Reason { get; init; } = "";
    public string Direction { get; init; } = "up"; // up | down
}

public sealed class RpgXpApplyResult
{
    public RpgActorState State { get; init; } = new();
    public IReadOnlyList<LevelChangeEvent> LevelChanges { get; init; } = Array.Empty<LevelChangeEvent>();
}

public static class RpgXpApply
{
    public static RpgXpApplyResult Apply(
        string kind,
        RpgActorState state,
        double delta,
        long playerId = 0,
        int typeId = 0,
        string reason = "")
    {
        var beforeLevel = state.Level;
        var beforeXp = state.Xp;
        var demotion = state.DemotionCount;
        var changes = new List<LevelChangeEvent>();

        state.Xp += delta;

        if (delta > 0)
        {
            while (state.Level < long.MaxValue)
            {
                var need = RpgXpCurve.XpToNext(kind, state.Level);
                if (state.Xp < need) break;
                state.Xp -= need;
                var from = state.Level;
                state.Level++;
                if (state.Level > state.HighestLevel)
                    state.HighestLevel = state.Level;
                changes.Add(new LevelChangeEvent
                {
                    PlayerId = playerId,
                    Kind = kind,
                    TypeId = typeId,
                    LevelBefore = from,
                    LevelAfter = state.Level,
                    XpAfter = state.Xp,
                    DemotionCount = demotion,
                    Reason = reason,
                    Direction = "up"
                });
            }
        }

        while (state.Xp < 0)
        {
            if (state.Level <= 1)
            {
                state.Xp = 0;
                break;
            }
            state.Level--;
            demotion++;
            state.DemotionCount = demotion;
            state.Xp += RpgXpCurve.XpToNext(kind, state.Level);
            changes.Add(new LevelChangeEvent
            {
                PlayerId = playerId,
                Kind = kind,
                TypeId = typeId,
                LevelBefore = state.Level + 1,
                LevelAfter = state.Level,
                XpAfter = state.Xp,
                DemotionCount = demotion,
                Reason = reason,
                Direction = "down"
            });
        }

        if (state.Level > state.HighestLevel)
            state.HighestLevel = state.Level;
        state.Revision++;

        _ = beforeLevel;
        _ = beforeXp;
        return new RpgXpApplyResult { State = state, LevelChanges = changes };
    }
}

public interface ILevelChangeHandler
{
    int Order { get; }
    void Handle(LevelChangeEvent e, Action next);
}

public sealed class LevelChangePipeline
{
    readonly List<ILevelChangeHandler> _handlers;

    public LevelChangePipeline(IEnumerable<ILevelChangeHandler>? handlers = null)
    {
        _handlers = (handlers ?? Array.Empty<ILevelChangeHandler>())
            .OrderBy(h => h.Order)
            .ToList();
    }

    public void Run(LevelChangeEvent e)
    {
        var i = 0;
        void Next()
        {
            if (i >= _handlers.Count) return;
            var h = _handlers[i++];
            h.Handle(e, Next);
        }
        Next();
    }

    public void RunAll(IEnumerable<LevelChangeEvent> events)
    {
        foreach (var e in events)
            Run(e);
    }
}
