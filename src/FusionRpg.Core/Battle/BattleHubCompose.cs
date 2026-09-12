using FusionRpg.Core.Power;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;

namespace FusionRpg.Core.Battle;

/// <summary>
/// battle-hub-fuse T5 — the battle-side Hub composer replacing
/// <see cref="BattleStatComposer.Compose"/>. Builds a Hub from the setup's own fields (level,
/// defense, elements, traits, tempo) plus the builders' <see cref="BattleHubInputs"/>, and returns
/// the composed <see cref="ActorDerivedSnapshot"/> the engine resolves with.
///
/// <para>Deliberately NOT <c>ActorHubBootstrap.CreateDefault</c>: battle composes no progression or
/// status channels, so those subsystems stay unregistered and parity with the old composer is
/// channel-exact. Arguments are read from the same statics the composer reads
/// (<c>BattleStatComposer.Tuning</c>, <c>BattleStatComposer.Traits</c>,
/// <c>AptitudeTuningHub</c>/<c>PowerTuningHub</c>); T6 relocates them with the composer's deletion.
/// A setup without inputs composes baseline + affinity + traits + tempo only — the same totals a
/// ChannelMods-free setup composed to before.</para>
/// </summary>
public static class BattleHubCompose
{
    /// <summary>Where trait channel mods are read from when a caller does not pass one (E12, re-homed
    /// off the deleted <c>BattleStatComposer.Traits</c> in T6). Defaults to the migrated set, which
    /// supplies `critical-hunter` and falls through to `TraitBattleCatalog` for the other thirteen.</summary>
    public static TraitAtomSource Traits { get; private set; } = TraitAtomSource.Shipped();

    public static void UseTraits(TraitAtomSource source) =>
        Traits = source ?? throw new ArgumentNullException(nameof(source));

    public static void ResetTraits() => Traits = TraitAtomSource.Shipped();

    public static ActorDerivedSnapshot Compose(BattleActorSetup setup, TraitAtomSource? traits = null)
    {
        if (setup is null) throw new ArgumentNullException(nameof(setup));
        var inputs = setup.HubInputs;
        var theta = setup.ThetaActor ?? setup.Level;

        var hub = new ActorHub(StatSystemBootstrap.CreateDefault());
        hub.Register(new BattleBaselineSubsystem(_ => (theta, setup.Defense)));
        hub.Register(new BattleAffinitySubsystem(_ =>
            (setup.Atk, setup.Defense, setup.ElementPrimary, setup.ElementSecondary)));
        hub.Register(new BattleTraitSubsystem(traits ?? Traits, _ => setup.TraitIds));
        hub.Register(new BattleTempoSubsystem(_ => setup.AttackIntervalMs));
        hub.Register(new ResourceBaselineSubsystem(new FixedPowerIndexProvider(theta)));

        if (inputs?.Aptitude is { } allocation)
        {
            hub.Register(new AptitudeSubsystem(
                AptitudeTuningHub.Tuning,
                new PowerLadder(PowerTuningHub.Tuning),
                new FixedPowerIndexProvider(theta),
                _ => allocation));
        }
        if (inputs?.BoundAtoms is { } bound)
            hub.Register(new AtomDerivedSubsystem(_ => bound));
        if (inputs?.StarLoyalty is { } starLoyalty)
            hub.Register(new StarLoyaltySubsystem(_ => starLoyalty));
        if (inputs?.Draughts is { } draughts)
            hub.Register(new DraughtSubsystem(_ => draughts));
        if (inputs?.Injuries is { } injuries)
            hub.Register(new ExpeditionInjurySubsystem(_ => injuries));

        var ctx = new StatContextFactory().ForBattle(
            setup.Key,
            new EntityBaseline { MaxHp = setup.MaxHp, Atk = setup.Atk },
            side: string.Equals(setup.Side, "wave", StringComparison.Ordinal) ? StatSide.Zombie : StatSide.Plant,
            typeId: setup.TypeId);
        var snapshot = hub.ResolveDerived(ctx);

        // DEBT — battle-hub-fuse: the caller-overlay fold, kept byte-identical until T6 deletes the
        // ChannelMods producers with the composer. Unknown-channel refusal matches the old composer.
        foreach (var mod in setup.ChannelMods)
        {
            if (!hub.Composer.Registry.TryResolveChannel(mod.ChannelId, out _))
                throw new ArgumentException($"Unknown combat channel id '{mod.ChannelId}'.");
            snapshot.Set(mod.ChannelId, snapshot.Get(mod.ChannelId, 0) + mod.Amount);
        }

        return snapshot;
    }
}
