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

    /// <summary>Affinity share of Atk/Defense granted on the actor's own element channels.</summary>
    public const int PrimaryAffinityDivisor = 4;    // +25% on the primary element
    public const int SecondaryAffinityDivisor = 8;  // +12.5% on the secondary element

    public static ActorDerivedSnapshot Compose(BattleActorSetup setup)
    {
        // battle-adoption mapping table: Atk is the resolver's BaseOverlayDamage — it must
        // NOT also sit in power.omni (double count). Defense stays: the defense channel is
        // its only consumer. Affinity shares remain genuine adjustments on both sides.
        var snap = ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseOmni, setup.Defense),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, BattleRuleset.BaseAccuracy(setup.Level)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDodgeOmni, BattleRuleset.BaseDodge(setup.Level)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatCritRateOmni, BattleRuleset.BaseCritRate(setup.Level)),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatCritResistOmni, BattleRuleset.BaseCritResist(setup.Level))
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
            foreach (var mod in TraitBattleCatalog.Get(traitId).ChannelMods)
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
