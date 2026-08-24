using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// Composes a per-actor derived snapshot for the battle engine — the web-mode analogue of the
/// ActorHub compose path. Level formulas fill the omni halves, element affinity fills the actor's
/// own element channels, and ChannelMods (trait stat mods, later equipment) overlay additively.
/// Every value is an integer; reads go back out through CombatDerivedReader so channel semantics
/// stay identical to the PvZ overlay.
/// </summary>
public static class BattleStatComposer
{
    static readonly HashSet<string> KnownChannels = BuildKnownChannels();

    static HashSet<string> BuildKnownChannels()
    {
        // Combat channels plus the status power/resist families the ResistanceEvaluator reads.
        var set = new HashSet<string>(DerivedStatChannels.AllCombatChannelIds, StringComparer.Ordinal)
        {
            DerivedStatChannels.StatusPowerOmni,
            DerivedStatChannels.StatusPowerDot,
            DerivedStatChannels.StatusPowerCc,
            DerivedStatChannels.StatusPowerContagion,
            DerivedStatChannels.StatusResistOmni,
            DerivedStatChannels.StatusResistDot,
            DerivedStatChannels.StatusResistCc,
            DerivedStatChannels.StatusResistContagion
        };
        return set;
    }

    static BattleTuning? _tuning;

    public static void Configure(BattleTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static BattleTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "BattleStatComposer.Configure(...) has not run. The affinity divisors read " +
        "data/tuning/battle.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    /// <summary>Affinity share of Atk/Defense granted on the actor's own element channels.</summary>
    public static int PrimaryAffinityDivisor => Tuning.PrimaryAffinityDivisor;     // +25% on the primary element
    public static int SecondaryAffinityDivisor => Tuning.SecondaryAffinityDivisor; // +12.5% on the secondary element

    /// <summary>
    /// Where trait channel mods are read from (E12). Defaults to the migrated set, which supplies
    /// `critical-hunter` and falls through to `TraitBattleCatalog` for the other thirteen — so one
    /// trait moves and the rest are untouched.
    /// </summary>
    public static TraitAtomSource Traits { get; private set; } = TraitAtomSource.Shipped();

    public static void UseTraits(TraitAtomSource source) =>
        Traits = source ?? throw new ArgumentNullException(nameof(source));

    public static void ResetTraits() => Traits = TraitAtomSource.Shipped();

    public static ActorDerivedSnapshot Compose(BattleActorSetup setup) => Compose(setup, Traits);

    public static ActorDerivedSnapshot Compose(BattleActorSetup setup, TraitAtomSource traits)
    {
        // battle-rates (T2.2) / content-authoring (T2.3): setup.Index is Theta — an alias for Level,
        // not a new source (no real power-index composition is wired through BattleActorSetup yet;
        // that is a later wave's job). Named here so the read is honest about what it means now, per
        // spec-battle-rates.md §2.3's "pass Theta" framing.
        int theta = setup.Index;

        // battle-adoption mapping table: Atk is the resolver's BaseOverlayDamage — it must
        // NOT also sit in power.omni (double count). Defense stays: the defense channel is
        // its only consumer. Affinity shares remain genuine adjustments on both sides.
        var snap = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseOmni, setup.Defense),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, BattleRuleset.BaseAccuracy(theta)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDodgeOmni, BattleRuleset.BaseDodge(theta)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatCritRateOmni, BattleRuleset.BaseCritRate(theta)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatCritResistOmni, BattleRuleset.BaseCritResist(theta))
        });

        if (setup.ElementPrimary is { } primary)
            AddAffinity(snap, primary, setup, PrimaryAffinityDivisor);
        if (setup.ElementSecondary is { } secondary)
            AddAffinity(snap, secondary, setup, SecondaryAffinityDivisor);

        // Trait stat mods (the Funnel-routed traits' static half) merge once per distinct trait.
        var seenTraits = new HashSet<string>(StringComparer.Ordinal);
        foreach (var traitId in setup.TraitIds)
        {
            if (!seenTraits.Add(traitId)) continue;

            // E12: a migrated trait's mods come from its bound stat.derived atoms; every other
            // trait still reads the catalog. This is the ONE consumption path the module adds —
            // an earlier draft had battle reading bindings in three places, which would have been a
            // fourth path bypassing both the compiler and the runner and appearing in no spec.
            foreach (var mod in traits.ModsFor(traitId))
                snap.Set(mod.ChannelId, snap.Get(mod.ChannelId) + mod.Amount);
        }

        foreach (var mod in setup.ChannelMods)
        {
            if (!KnownChannels.Contains(mod.ChannelId))
                throw new ArgumentException($"Unknown combat channel id '{mod.ChannelId}'.");
            snap.Set(mod.ChannelId, snap.Get(mod.ChannelId) + mod.Amount);
        }

        return snap;
    }

    static void AddAffinity(ActorDerivedSnapshot snap, ElementTypeId element, BattleActorSetup setup, int divisor)
    {
        var powerCh = $"combat.power.{element.ToElementId()}";
        var defenseCh = $"combat.defense.{element.ToElementId()}";
        snap.Set(powerCh, snap.Get(powerCh) + setup.Atk / divisor);
        snap.Set(defenseCh, snap.Get(defenseCh) + setup.Defense / divisor);
    }
}
