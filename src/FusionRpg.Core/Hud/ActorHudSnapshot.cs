namespace FusionRpg.Core.Hud;

/// <summary>Presentation DTO — view of Hot snapshot data at observe time, not a new SSOT store.</summary>
public sealed record ActorHudSnapshot(
    ActorHudIdentity Identity,
    ActorHudResources? Resources,
    IReadOnlyList<ActorHudStatusToken> Statuses,
    ActorHudOverflow Overflow);

public sealed record ActorHudIdentity(
    ActorHudTier Tier,
    string Role,
    int? LevelBand,
    IReadOnlyList<string> Flags);

public sealed record ActorHudResources(
    ActorHudShield? Shield,
    ActorHudHpSliver? HpSliver,
    IReadOnlyList<ActorHudMeter>? Meters);

public sealed record ActorHudShield(
    long Hp,
    long Max,
    IReadOnlyList<ActorHudShieldStack> Stacks);

public sealed record ActorHudShieldStack(
    string Element,
    long Hp,
    long Max);

public sealed record ActorHudHpSliver(double Ratio);

public sealed record ActorHudMeter(string Id, double Ratio);

public sealed record ActorHudStatusToken(
    string Id,
    bool Cc,
    MagnitudeBand MagnitudeBand);

public sealed record ActorHudOverflow(int StatusCount);
