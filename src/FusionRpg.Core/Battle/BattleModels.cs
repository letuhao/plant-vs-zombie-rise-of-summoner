using System.Text.Json.Serialization;
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

    /// <summary>Alias for <see cref="Level"/> — the actor's Θ (content-authoring, T2.3,
    /// spec-content-authoring.md §2.2). <c>Level</c> stays the real/serialized name: audit F7 and
    /// decisions.md:42 are explicit that a field rename here moves all four expedition hashes; "the
    /// alias is the rule, not a fallback." Read this for new Θ-flavored code; <c>Level</c> for
    /// anything that touches serialization. <see cref="JsonIgnoreAttribute"/> is load-bearing, not
    /// decoration — found the hard way: a first draft without it moved
    /// ExpeditionResolverTests.Tier_goldens_are_locked's hash, because System.Text.Json serializes
    /// get-only computed properties by default.</summary>
    [JsonIgnore]
    public int Index => Level;

    public ElementTypeId? ElementPrimary { get; init; }
    public ElementTypeId? ElementSecondary { get; init; }
    public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    public long MaxHp { get; init; }
    public long Atk { get; init; }
    public long Defense { get; init; }

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
public sealed record BattleChannelMod(string ChannelId, long Amount);

/// <summary>
/// One status application: id from the locked catalog, signed HP per pulse (negative = DoT,
/// positive = regen; 0 for pure CC), millisecond duration/period on the round clock.
/// </summary>
public sealed record BattleStatusSpec(
    string StatusId, long MagnitudePerPulse, int DurationMs, int PeriodMs = 1000, int GrantChanceMilli = 1000);

/// <summary>Locked engine constants (spec-match-source-core.md). Changing RoundDurationMs or
/// MaxRounds (tunables-ssot.md T1 — data/tuning/battle.v1.json) still needs a RulesetVersion bump,
/// same as any other engine-shape change; EngineVersion/RulesetVersion themselves stay structural.</summary>
public static class BattleRuleset
{
    public const int EngineVersion = 1;

    /// <summary>v2 (combat-unification battle-adoption): the SSOT resolver replaced the
    /// per-mille curves; baselines re-expressed in resolver-scale points (owner decision 5).
    /// v3 (T4.2, power-dial, 2026-08-24): power-scale.v2.json's bMilli 0 -> 400 — every magnitude
    /// away from the Theta=20 pin moves; the pin itself and every rate golden must not (PS-3).</summary>
    public const int RulesetVersion = 3;

    static BattleTuning? _tuning;

    public static void Configure(BattleTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static BattleTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "BattleRuleset.Configure(...) has not run. RoundDurationMs/MaxRounds read " +
        "data/tuning/battle.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    /// <summary>Synthetic clock per round — every reused subsystem is millisecond-based.</summary>
    public static int RoundDurationMs => Tuning.RoundDurationMs;
    public static int MaxRounds => Tuning.MaxRounds;

    // battle-magnitude (T2.1, spec-battle-magnitude.md): delegate to the Θ ladder. Lazy (`??=`), not
    // an eager static field initializer — FusionRpg.Core.Power.PowerTuningHub.Configure runs during
    // host startup, and an eager field could race it depending on static-init order. Read PowerTuningHub
    // directly rather than adding a second Configure — the data already lives there once, one host call.
    static FusionRpg.Core.Power.PowerLadder? _hpLadder;
    static FusionRpg.Core.Power.ChannelLadder? _atkLadder;
    static FusionRpg.Core.Power.ChannelLadder? _defenseLadder;

    static FusionRpg.Core.Power.ChannelLadder ChannelLadderFor(string channelId)
    {
        var tuning = FusionRpg.Core.Power.PowerTuningHub.Tuning;
        if (!tuning.ChannelsOrEmpty.TryGetValue(channelId, out var channel))
            throw new InvalidOperationException(
                $"BattleRuleset: no '{channelId}' entry in data/tuning/power-scale.v{{n}}.json's channels block " +
                "(spec-battle-magnitude.md §2.1) — there is no built-in default to fall back to.");
        return new FusionRpg.Core.Power.ChannelLadder(tuning.Curve.BMilli, FusionRpg.Core.Power.PowerTuning.FixedPinValue, channel);
    }

    /// <summary>Baseline combat stats from specimen level — integer only. `level` is treated as Θ
    /// directly (T2.1 keeps call sites unchanged — spec-battle-magnitude.md §2.3; a real caller-supplied
    /// Θ arrives in a later wave).</summary>
    public static long BaseHp(int level) =>
        (_hpLadder ??= new FusionRpg.Core.Power.PowerLadder(FusionRpg.Core.Power.PowerTuningHub.Tuning)).Value(level);
    public static long BaseAtk(int level) => (_atkLadder ??= ChannelLadderFor("atk")).Value(level);
    public static long BaseDefense(int level) => (_defenseLadder ??= ChannelLadderFor("defense")).Value(level);

    /// <summary>
    /// Accuracy-family baselines in RESOLVER-scale points (sigmoid scale 100). Derived from
    /// the owner's rate targets, locked by BattleRateTests:
    ///   parity hit: σ((220+26Θ−26Θ)/100) = σ(2.2) ≈ 0.900   (target 0.90 ± 0.02)
    ///   parity crit: σ((10Θ−(10Θ+250))/100) = σ(−2.5) ≈ 0.076 (target 0.05–0.10)
    ///   +5-index attacker: σ(3.5) ≈ 0.971 hit (target ≥ 0.97), σ(−2.0) ≈ 0.119 crit.
    /// Crit magnitude needs no baseline: delta 0 → ×1.5, the old CritMultBase.
    ///
    /// <para>battle-rates (T2.2, spec-battle-rates.md): a rename, not a formula change — these four
    /// read Θ <b>directly</b> (PS-3: contests read Θ, magnitudes read P(Θ)), never through
    /// <see cref="FusionRpg.Core.Power.PowerLadder"/>/<see cref="FusionRpg.Core.Power.ChannelLadder"/>.
    /// Under B&gt;0 both accuracy and dodge would grow quadratically, and so would their difference —
    /// the only thing the sigmoid sees — turning a fixed one-index gap into a dial-dependent blowout.
    /// <see cref="BaseHp"/>/<see cref="BaseAtk"/>/<see cref="BaseDefense"/> above are the only three
    /// that may touch the ladder.</para>
    /// </summary>
    public static int BaseAccuracy(int theta) => 220 + 26 * theta;
    public static int BaseDodge(int theta) => 26 * theta;
    public static int BaseCritRate(int theta) => 10 * theta;
    public static int BaseCritResist(int theta) => 10 * theta + 250;

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
    long HpRemaining, long DamageDealt, int Kills, bool Survived,
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

    /// <summary>
    /// Which content produced this report (E12, deferred here from E8).
    ///
    /// <para><b>Carried, and deliberately outside the determinism hash</b> — the same treatment the
    /// platform stamp gets, for the same reason. It is provenance, not battle math. Fold it into the
    /// hash input and every content edit anywhere — a new item, a seventh element, a re-priced
    /// coefficient — moves every battle golden, and a real determinism break becomes indistinguishable
    /// from someone adding a row. That is the machine-dependence problem in a different coat.</para>
    ///
    /// <para><b>Null</b> when the host has no catalog: battle does not require one, and inventing a
    /// hash for content that was not consulted would be a claim rather than a record. Null and not
    /// empty-string, because only null is a string's default — and the omission below keys on that.</para>
    ///
    /// <para><b>Omitted from the JSON when empty</b>, which is what let this land without re-blessing
    /// a single golden. Blanking the value was not enough — the property NAME alone moved all four
    /// hashes. Absent-when-empty means the determinism view (which blanks it) serializes to exactly
    /// the bytes it did before E12 existed.</para>
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string? ContentHash { get; init; }

    public ulong Seed { get; init; }
    public string WaveId { get; init; } = "";
    public BattleOutcome Outcome { get; init; }
    public int Rounds { get; init; }

    /// <summary>Battle-level Souls multiplier (‰, base 1000) — greedy squad survivors raise it.</summary>
    public int SoulLootMilli { get; init; } = 1000;
    public IReadOnlyList<BattleEventRec> Events { get; init; } = Array.Empty<BattleEventRec>();
    public IReadOnlyList<BattleActorResult> Actors { get; init; } = Array.Empty<BattleActorResult>();
}
