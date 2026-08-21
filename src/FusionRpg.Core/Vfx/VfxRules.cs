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

    /// <summary>Crit keeps distinctness via size, not color — vfx-ssot.md §16.4.</summary>
    public const float CritFontScale = 1.25f;

    /// <summary>Crit pop: start big, settle to CritFontScale by this normalized life t.</summary>
    public const float CritPopStartScale = 1.5f;
    public const float CritPopSettleT = 0.3f;

    /// <summary>Amount tiers (|amount|): small hits shrink, big hits grow — numeric labels only.</summary>
    public const long AmountTierSmallBelow = 50;
    public const long AmountTierBigFrom = 200;
    public const float AmountTierSmallScale = 0.9f;
    public const float AmountTierBigScale = 1.15f;

    public static float PopScale(float t)
    {
        if (t <= 0f) return CritPopStartScale;
        if (t >= CritPopSettleT) return CritFontScale;
        return CritPopStartScale - (CritPopStartScale - CritFontScale) * (t / CritPopSettleT);
    }

    public static float AmountScale(long amount)
    {
        var a = Math.Abs(amount);
        if (a < AmountTierSmallBelow) return AmountTierSmallScale;
        if (a < AmountTierBigFrom) return 1f;
        return AmountTierBigScale;
    }
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
    /// <summary>Every renderable spec required an element color and the cue had none.</summary>
    public const string NoElement = "no-element";
}

/// <summary>Sustained-visual policy — SPEC vfx-v3 §3 (tight budget locked by owner).</summary>
public static class VfxSustainedRules
{
    public const int GlobalCap = 24;
    public const int PerHostCap = 2;
    /// <summary>TTL = status duration + this grace; the expire cue normally ends the visual first.</summary>
    public const double TtlGraceSeconds = 2.0;
    /// <summary>Statuses with no known duration re-confirm via re-apply within this window.</summary>
    public const double InfiniteTtlSeconds = 60.0;
    public const float AuraPulseSeconds = 0.3f;
    public const int AuraMaxParticles = 6;
}

/// <summary>Enumerated sustained end reasons — debug.fx.state.ended payload contract.</summary>
public static class VfxStateEndReasons
{
    public const string Expired = "expired";
    public const string HostGone = "host-gone";
    public const string TtlCap = "ttl-cap";
    public const string Evicted = "evicted";
    public const string MatchEnd = "match-end";
    public const string Disabled = "disabled";
}
