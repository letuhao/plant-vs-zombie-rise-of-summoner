using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Vfx;

/// <summary>
/// Locked policy constants — vfx-ssot.md §7. Floater curve values stay in
/// <see cref="DamageFxFloaterRules"/> (test-locked); these alias them so there is one source.
/// </summary>
public static class VfxRules
{
    public const int FloaterCap = DamageFxFloaterRules.Cap;                       // 64
    public const int BurstCap = 24;
    public const float FloaterLifeSeconds = DamageFxFloaterRules.LifeSeconds;     // 0.9
    public const float BurstLifeSeconds = 0.55f;
    public const float RisePixels = DamageFxFloaterRules.RisePixels;              // 56

    public const float FloaterRateLimitSeconds = 0.05f;   // per (cueId, TargetPtr)
    public const float BurstRateLimitSeconds = 0.15f;     // per (cueId, cell)
    public const int GlobalCuePerTickCap = 32;
    public const int CueQueueCap = 256;

    public const float AnchorSweepMinIntervalSeconds = 0.5f;

    /// <summary>Crit keeps distinctness via size, not color — vfx-ssot.md §16.4.</summary>
    public const float CritFontScale = 1.25f;
}

/// <summary>Enumerated skip reasons — debug.fx.skipped payload contract (vfx-ssot.md §11).</summary>
public static class VfxSkipReasons
{
    public const string Disabled = "disabled";
    public const string UnknownCue = "unknown-cue";
    public const string Muted = "muted";
    public const string RateLimited = "rate-limited";
    public const string Cap = "cap";
    public const string Missing = "missing";
    public const string NoShader = "no-shader";
    public const string ParticleFail = "particle-fail";
}
