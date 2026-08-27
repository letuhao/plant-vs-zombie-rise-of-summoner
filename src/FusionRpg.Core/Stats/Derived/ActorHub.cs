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
        var snapshot = _composer.Compose(mods);
        // class-system-todo.md V5/P1.10 — makes Theta=0 (an unhydrated IPowerIndexProvider) detectable
        // from emitted metrics rather than only by reading a snapshot value in a debugger.
        PerfProbe.RecordValue(DerivedStatChannels.ProgressionPower, snapshot.Get(DerivedStatChannels.ProgressionPower, 0));
        // class-system-todo.md V5/P8.2 — the regen half of "stamina binds"; the cost half has no
        // emitter anywhere in this codebase yet (action-costs is a separate, unimplemented program).
        PerfProbe.RecordValue(DerivedStatChannels.ResourceRegen("stamina"), snapshot.Get(DerivedStatChannels.ResourceRegen("stamina"), 0));
        // class-system-todo.md V5/P7.1 — poiseRegenPerRound, so guard-economy's own r = poiseRegen /
        // peerDamage is measurable. No aptitude edge feeds this channel in the shipped config yet
        // (P7.2's own named gap, still open), so this reads 0 on the real tree today — the mechanism
        // is live and correct, proven by Assert_terminationInvariant_actuallyReadsPoiseRegen_endToEnd's
        // own planted, absurd-rate fixture, not by a live nonzero reading that does not exist yet.
        PerfProbe.RecordValue(DerivedStatChannels.ResourceRegen("poise"), snapshot.Get(DerivedStatChannels.ResourceRegen("poise"), 0));
        return snapshot;
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
    /// <summary><paramref name="powerIndex"/> feeds <c>progression.power</c> (T3.2, defaults to Θ=0).
    /// class-system-todo.md P3.3 (2026-08-27) retired the `level`-gated bonus-flat curve
    /// <see cref="Subsystems.RpgProgressionSubsystem"/> used to own — this method no longer accepts a
    /// `level` delegate; the five `progression.bonus.*` bridge channels are allocation-sourced through
    /// <paramref name="aptitudeTuning"/>/<paramref name="aptitudeAllocation"/> below instead.
    ///
    /// <para><paramref name="aptitudeTuning"/> is opt-in (class-system-todo.md P2.4): omitting it
    /// registers no <see cref="Subsystems.AptitudeSubsystem"/> at all, so every existing caller —
    /// including the hundreds of tests that call this with no tuning hub configured — is unaffected.
    /// Pass it (typically <c>AptitudeTuningHub.Tuning</c>) to wire aptitudes in; <paramref
    /// name="aptitudeAllocation"/> defaults to <see cref="AptitudeAllocation.Empty"/>, matching P2.4's
    /// own proof that the wiring is inert until `point-economy` gives players something to spend.</para>
    /// </summary>
    public static ActorHub CreateDefault(StatSystem? stats = null,
        FusionRpg.Core.Power.IPowerIndexProvider? powerIndex = null,
        Aptitudes.AptitudeTuning? aptitudeTuning = null,
        Func<StatContext, Aptitudes.AptitudeAllocation>? aptitudeAllocation = null)
    {
        var sys = stats ?? StatSystemBootstrap.CreateDefault();
        var hub = new ActorHub(sys);
        hub.Register(new Subsystems.RpgProgressionSubsystem(powerIndex));
        if (aptitudeTuning is not null)
        {
            hub.Register(new Subsystems.AptitudeSubsystem(
                aptitudeTuning,
                new FusionRpg.Core.Power.PowerLadder(FusionRpg.Core.Power.PowerTuningHub.Tuning),
                powerIndex,
                aptitudeAllocation));
        }
        return hub;
    }
}
