namespace FusionRpg.Core.Match;

/// <summary>
/// RAM cap config for our Intent / FA4 / debug Create (not vanilla waves).
/// Same defaults as match-runtime CapPolicyConfig — W1 absorbs this type.
/// </summary>
public sealed class CapPolicyConfig
{
    // Config-backed (tunables-ssot.md T1) — data/tuning/match.v1.json. Property initializers (not
    // default parameters) can read a method, so a one-off override via the object initializer (as
    // tests do) still gets the real configured value for any field it leaves unset.
    public int MaxLivingPlants { get; set; } = MatchTuningPolicy.MaxLivingPlants;
    public int MaxLivingZombies { get; set; } = MatchTuningPolicy.MaxLivingZombies;
    /// <summary><c>-1</c> = unlimited.</summary>
    public int MaxLivingBullets { get; set; } = -1;

    public static CapPolicyConfig Defaults() => new();
}

/// <summary>Host-configured match tuning (tunables-ssot.md T1/T5) — data/tuning/match.v1.json.</summary>
public static class MatchTuningPolicy
{
    static MatchTuning? _tuning;

    public static void Configure(MatchTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static MatchTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "MatchTuningPolicy.Configure(...) has not run. CapPolicyConfig.Defaults() reads " +
        "data/tuning/match.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static int MaxLivingPlants => Tuning.MaxLivingPlants;
    public static int MaxLivingZombies => Tuning.MaxLivingZombies;
}

public readonly struct LivingCounts
{
    public LivingCounts(int plants, int zombies, int bullets = 0)
    {
        Plants = plants;
        Zombies = zombies;
        Bullets = bullets;
    }

    public int Plants { get; }
    public int Zombies { get; }
    public int Bullets { get; }
}

public readonly struct GateResult
{
    public GateResult(bool ok, string reason = "")
    {
        Ok = ok;
        Reason = reason ?? "";
    }

    public bool Ok { get; }
    public string Reason { get; }

    public static GateResult Allowed() => new(true, "");
    public static GateResult Reject(string reason) => new(false, reason);
}

/// <summary>Stable Admit reject reason codes (match-runtime §7.8).</summary>
public static class GateReasons
{
    public const string CapPlants = "cap.plants";
    public const string CapZombies = "cap.zombies";
    public const string CapBullets = "cap.bullets";
    public const string CapInvalidSide = "cap.invalid_side";
    public const string PhaseIdle = "phase.idle";
    public const string PhaseStarting = "phase.starting";
    public const string PhasePaused = "phase.paused";
    public const string PhaseEnding = "phase.ending";

    /// <summary>Reject reason for a non-InMatch phase; empty string when InMatch (not a reject).</summary>
    public static string ForPhase(MatchPhase phase) => phase switch
    {
        MatchPhase.Starting => PhaseStarting,
        MatchPhase.Paused => PhasePaused,
        MatchPhase.Ending => PhaseEnding,
        MatchPhase.InMatch => "",
        _ => PhaseIdle
    };
}

/// <summary>
/// Cap count gate (W0-B / W1-C). Phase checks live on MatchRuntime.TryAdmitSpawn.
/// Never throws; never reads Data.
/// </summary>
public static class CapPolicy
{
    public static GateResult TryAdmit(string? side, LivingCounts counts, CapPolicyConfig? config = null)
    {
        config ??= CapPolicyConfig.Defaults();
        var s = (side ?? "").Trim();
        if (string.Equals(s, "plant", StringComparison.OrdinalIgnoreCase))
            return Check(counts.Plants, config.MaxLivingPlants, GateReasons.CapPlants);
        if (string.Equals(s, "zombie", StringComparison.OrdinalIgnoreCase))
            return Check(counts.Zombies, config.MaxLivingZombies, GateReasons.CapZombies);
        if (string.Equals(s, "bullet", StringComparison.OrdinalIgnoreCase))
            return Check(counts.Bullets, config.MaxLivingBullets, GateReasons.CapBullets);
        return GateResult.Reject(GateReasons.CapInvalidSide);
    }

    static GateResult Check(int living, int max, string capReason)
    {
        if (max < 0) return GateResult.Allowed();
        if (living >= max) return GateResult.Reject(capReason);
        return GateResult.Allowed();
    }
}
