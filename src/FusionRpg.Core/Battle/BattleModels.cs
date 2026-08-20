using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>One combatant entering a battle — a demon specimen snapshot or a wave enemy.</summary>
public sealed record BattleActorSetup
{
    public string Key { get; init; } = "";                 // stable within the battle (e.g. "squad:0", "wave:3")
    public string Side { get; init; } = "";                // "squad" | "wave"
    public string SpeciesId { get; init; } = "";
    public int TypeId { get; init; }                        // demon type id (disjoint space) for event emission
    public int Level { get; init; } = 1;
    public ElementTypeId? ElementPrimary { get; init; }
    public ElementTypeId? ElementSecondary { get; init; }
    public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    public int MaxHp { get; init; }
    public int Atk { get; init; }
    public int Defense { get; init; }
}

/// <summary>Locked engine constants (spec-match-source-core.md). Changing any bumps RulesetVersion.</summary>
public static class BattleRuleset
{
    public const int EngineVersion = 1;
    public const int RulesetVersion = 1;

    /// <summary>Synthetic clock per round — every reused subsystem is millisecond-based.</summary>
    public const int RoundDurationMs = 1000;
    public const int MaxRounds = 50;

    /// <summary>Baseline combat stats from specimen level — integer only.</summary>
    public static int BaseHp(int level) => 80 + 30 * level;
    public static int BaseAtk(int level) => 12 + 4 * level;
    public static int BaseDefense(int level) => 2 + level;
}

public sealed record BattleSetup
{
    public IReadOnlyList<BattleActorSetup> Squad { get; init; } = Array.Empty<BattleActorSetup>();
    public IReadOnlyList<BattleActorSetup> Wave { get; init; } = Array.Empty<BattleActorSetup>();
    public string WaveId { get; init; } = "";
}

public enum BattleOutcome
{
    Victory,   // wave wiped
    Defeat,    // squad wiped
    Stalemate  // MaxRounds hit
}

/// <summary>One structured battle occurrence — the emitter maps these onto the event vocabulary.</summary>
public sealed record BattleEventRec(int Round, string Kind, string ActorKey, int TypeId, string Side);

public static class BattleEventKinds
{
    public const string Spawn = "spawn";
    public const string Die = "die";
}

public sealed record BattleActorResult(
    string Key, string Side, string SpeciesId, int TypeId,
    int HpRemaining, int DamageDealt, int Kills, bool Survived);

public sealed record BattleReport
{
    public int EngineVersion { get; init; } = BattleRuleset.EngineVersion;
    public int RngAlgoVersion { get; init; } = SeededRng.RngAlgoVersion;
    public int RulesetVersion { get; init; } = BattleRuleset.RulesetVersion;
    public ulong Seed { get; init; }
    public string WaveId { get; init; } = "";
    public BattleOutcome Outcome { get; init; }
    public int Rounds { get; init; }
    public IReadOnlyList<BattleEventRec> Events { get; init; } = Array.Empty<BattleEventRec>();
    public IReadOnlyList<BattleActorResult> Actors { get; init; } = Array.Empty<BattleActorResult>();
}
