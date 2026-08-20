using FusionRpg.Core.Diagnostics;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Stats.Derived;

public sealed record ActorResolveResult(
    EntityFinal RuntimePrimary,
    ActorDerivedSnapshot Derived,
    EntityFinal AppliedCombat)
{
    public ActorElementTypes ElementTypes { get; init; } = ActorElementTypes.Neutral;
}

/// <summary>Wraps StatSystem primary resolve + derived compose — actor-hub-ssot.md §7.</summary>
public sealed class ActorHub
{
    readonly StatSystem _stats;
    readonly DerivedComposer _composer;
    readonly List<IActorStatSubsystem> _subsystems = new();

    public ActorHub(StatSystem stats, DerivedComposer? composer = null)
    {
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _composer = composer ?? new DerivedComposer();
    }

    public StatSystem Stats => _stats;
    public DerivedComposer Composer => _composer;
    public IReadOnlyList<IActorStatSubsystem> Subsystems => _subsystems;

    public void Register(IActorStatSubsystem subsystem)
    {
        if (subsystem == null) throw new ArgumentNullException(nameof(subsystem));
        _subsystems.RemoveAll(s => string.Equals(s.SubsystemId, subsystem.SubsystemId, StringComparison.OrdinalIgnoreCase));
        _subsystems.Add(subsystem);
        _subsystems.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public ActorResolveResult Resolve(StatContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        var primary = _stats.Resolve(ctx);
        var derived = ResolveDerived(ctx);
        var applied = MergeAppliedCombat(primary, derived);
        return new ActorResolveResult(primary, derived, applied)
        {
            ElementTypes = ctx.ElementTypes
        };
    }

    /// <summary>L2b Apply-scoped derived compose — fresh per call in v1.</summary>
    public ActorDerivedSnapshot ResolveDerived(StatContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        using var _perf = PerfProbe.Measure(PerfSection.HubResolveDerived);
        var mods = new List<DerivedModifier>();
        foreach (var subsystem in _subsystems)
            subsystem.ContributeDerived(ctx, mods);
        return _composer.Compose(mods);
    }

    static EntityFinal MergeAppliedCombat(EntityFinal primary, ActorDerivedSnapshot derived)
    {
        var bonusMaxHp = (long)Math.Round(derived.Get(DerivedStatChannels.ProgressionBonusMaxHp, 0));
        var bonusAtk = (int)Math.Round(derived.Get(DerivedStatChannels.ProgressionBonusAtk, 0));
        var bonusDefenseFlat = (int)Math.Round(derived.Get(DerivedStatChannels.ProgressionBonusDefense, 0));
        var bonusArm1 = (int)Math.Round(derived.Get(DerivedStatChannels.ProgressionBonusArm1, 0));
        var bonusArm2 = (int)Math.Round(derived.Get(DerivedStatChannels.ProgressionBonusArm2, 0));

        if (bonusMaxHp == 0 && bonusAtk == 0 && bonusDefenseFlat == 0 && bonusArm1 == 0 && bonusArm2 == 0)
            return primary;

        return new EntityFinal
        {
            Hp = primary.Hp + bonusMaxHp,
            MaxHp = primary.MaxHp + bonusMaxHp,
            Atk = primary.Atk + bonusAtk,
            Arm1 = primary.Arm1 + bonusArm1,
            Arm1Max = primary.Arm1Max + bonusArm1,
            Arm2 = primary.Arm2 + bonusArm2,
            Arm2Max = primary.Arm2Max + bonusArm2,
            DefensePercent = primary.DefensePercent,
            DefenseFlat = primary.DefenseFlat + bonusDefenseFlat,
            Contributions = primary.Contributions
        };
    }
}

public static class ActorHubBootstrap
{
    public static ActorHub CreateDefault(StatSystem? stats = null, IProgressionPowerProvider? power = null)
    {
        var sys = stats ?? StatSystemBootstrap.CreateDefault();
        var hub = new ActorHub(sys);
        hub.Register(new Subsystems.RpgProgressionSubsystem(power));
        return hub;
    }
}
