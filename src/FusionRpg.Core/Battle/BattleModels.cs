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

    /// <summary>Additive derived-channel adjustments (trait stat mods, equipment later). Integer amounts only.</summary>
    public IReadOnlyList<BattleChannelMod> ChannelMods { get; init; } = Array.Empty<BattleChannelMod>();

    /// <summary>Statuses applied attacker-less at battle start (test seams now, trait/attack riders later).</summary>
    public IReadOnlyList<BattleStatusSpec> InitialStatuses { get; init; } = Array.Empty<BattleStatusSpec>();

    /// <summary>Innate shield content row (battle-adoption) — applied at setup, no expiry unless set.</summary>
    public BattleInnateShield? InnateShield { get; init; }
}

/// <summary>
/// Innate shield spec — durations in MILLISECONDS at the content boundary (host-converted:
/// battle ticks are rounds). Null duration = persists until broken.
/// </summary>
public sealed record BattleInnateShield(
    long BaseHp, ElementTypeId? Element = null, int Priority = 10, int? DurationMs = null);

/// <summary>One additive adjustment to a combat derived channel — validated against the generated channel list.</summary>
public sealed record BattleChannelMod(string ChannelId, int Amount);

/// <summary>
/// One status application: id from the locked catalog, signed HP per pulse (negative = DoT,
/// positive = regen; 0 for pure CC), millisecond duration/period on the round clock.
/// </summary>
public sealed record BattleStatusSpec(
    string StatusId, int MagnitudePerPulse, int DurationMs, int PeriodMs = 1000, int GrantChanceMilli = 1000);

/// <summary>Locked engine constants (spec-match-source-core.md). Changing any bumps RulesetVersion.</summary>
public static class BattleRuleset
{
    public const int EngineVersion = 1;

    /// <summary>v2 (combat-unification battle-adoption): the SSOT resolver replaced the
    /// per-mille curves; baselines re-expressed in resolver-scale points (owner decision 5).</summary>
    public const int RulesetVersion = 2;

    /// <summary>Synthetic clock per round — every reused subsystem is millisecond-based.</summary>
    public const int RoundDurationMs = 1000;
    public const int MaxRounds = 50;

    /// <summary>Baseline combat stats from specimen level — integer only.</summary>
    public static int BaseHp(int level) => 80 + 30 * level;
    public static int BaseAtk(int level) => 12 + 4 * level;
    public static int BaseDefense(int level) => 2 + level;

    /// <summary>
    /// Accuracy-family baselines in RESOLVER-scale points (sigmoid scale 100). Derived from
    /// the owner's rate targets, locked by BattleRateTests:
    ///   parity hit: σ((220+26L−26L)/100) = σ(2.2) ≈ 0.900   (target 0.90 ± 0.02)
    ///   parity crit: σ((10L−(10L+250))/100) = σ(−2.5) ≈ 0.076 (target 0.05–0.10)
    ///   +5-level attacker: σ(3.5) ≈ 0.971 hit (target ≥ 0.97), σ(−2.0) ≈ 0.119 crit.
    /// Crit magnitude needs no baseline: delta 0 → ×1.5, the old CritMultBase.
    /// </summary>
    public static int BaseAccuracy(int level) => 220 + 26 * level;
    public static int BaseDodge(int level) => 26 * level;
    public static int BaseCritRate(int level) => 10 * level;
    public static int BaseCritResist(int level) => 10 * level + 250;

    // The v1 per-mille Hit*/Crit* constants are retired (combat-unification ban test);
    // the SSOT resolver's sigmoid + CombatProbabilityPolicy own hit/crit math now.
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

/// <summary>
/// One structured battle occurrence — the emitter maps these onto the event vocabulary.
/// Shield events (battle-adoption) carry the optional tail fields; spawn/die leave them default.
/// </summary>
public sealed record BattleEventRec(
    int Round, string Kind, string ActorKey, int TypeId, string Side,
    long Amount = 0, string? Element = null, string? ShieldId = null);

public static class BattleEventKinds
{
    public const string Spawn = "spawn";
    public const string Die = "die";
    // Shield vocabulary (battle-adoption) — deliberate expansion; absorbed entries are
    // per-round aggregates from ShieldRuntime.DrainEvents, never per-attack spam.
    public const string ShieldGranted = "shield.granted";
    public const string ShieldAbsorbed = "shield.absorbed";
    public const string ShieldBroken = "shield.broken";
    public const string ShieldExpired = "shield.expired";
}

/// <summary>Per-actor tallies. XpMilli is a per-mille XP MULTIPLIER (base 1000; genius raises
/// it), not an XP amount — consumers scale their own rates by it. ShieldAbsorbed = damage the
/// actor's own shields ate (battle-adoption).</summary>
public sealed record BattleActorResult(
    string Key, string Side, string SpeciesId, int TypeId,
    int HpRemaining, int DamageDealt, int Kills, bool Survived,
    bool Retreated, int XpMilli, long ShieldAbsorbed = 0);

/// <summary>
/// Process environment fingerprint for the report's platform stamp — the coordinates that
/// actually move <c>Math.Exp</c>'s last ULP, and nothing else.
///
/// OS is part of the identity: on CoreCLR x64 there is no hardware `exp`, so Math.Exp calls
/// the platform libm (ucrtbase / glibc / Apple libm) and those differ. Architecture alone
/// would give Windows-x64 and Linux-x64 the same stamp — a collision on exactly the case the
/// guard exists to catch.
///
/// The runtime MAJOR version only: a servicing update (8.0.11 → 8.0.30) rebinds nothing in
/// this path, so including the patch number would strand every logged match behind a routine
/// `dotnet` upgrade. Keep this stable — widening it invalidates stamps already in the DB.
/// </summary>
public static class BattleEnvironment
{
    public static readonly string Stamp = string.Join("/",
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        OsToken(),
        "net" + Environment.Version.Major.ToString(System.Globalization.CultureInfo.InvariantCulture));

    static string OsToken()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "macos";
        return "other";
    }
}

public sealed record BattleReport
{
    public int EngineVersion { get; init; } = BattleRuleset.EngineVersion;
    public int RngAlgoVersion { get; init; } = SeededRng.RngAlgoVersion;
    public int RulesetVersion { get; init; } = BattleRuleset.RulesetVersion;

    /// <summary>
    /// Platform stamp (owner decision 7): Math.Exp is not bit-identical across architectures,
    /// so replay/sweep guards refuse cross-platform re-resolution like a version mismatch.
    /// </summary>
    public string EnvironmentStamp { get; init; } = BattleEnvironment.Stamp;
    public ulong Seed { get; init; }
    public string WaveId { get; init; } = "";
    public BattleOutcome Outcome { get; init; }
    public int Rounds { get; init; }

    /// <summary>Battle-level Souls multiplier (‰, base 1000) — greedy squad survivors raise it.</summary>
    public int SoulLootMilli { get; init; } = 1000;
    public IReadOnlyList<BattleEventRec> Events { get; init; } = Array.Empty<BattleEventRec>();
    public IReadOnlyList<BattleActorResult> Actors { get; init; } = Array.Empty<BattleActorResult>();
}
