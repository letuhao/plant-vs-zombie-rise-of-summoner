using System.Reflection;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.ActorHub;

/// <summary>
/// spec-stat-taxonomy.md §6.1 — the four-class rule made executable. Operates on the static registry
/// (<see cref="DerivedStatRegistry.AllRegistered"/>); the dynamic sparse status ids resolved through
/// <see cref="DerivedStatRegistry.TryResolveChannel"/> are covered separately below.
/// </summary>
public class StatTaxonomyTests
{
    static IReadOnlyCollection<DerivedStatDef> AllRegistered => DerivedStatRegistry.CreateDefault().AllRegistered;

    [Fact]
    public void EveryContestFamilyHasACounterpart()
    {
        foreach (var def in AllRegistered.Where(d => d.Class == StatClass.Contest))
            Assert.False(string.IsNullOrWhiteSpace(def.CounterpartOf), $"{def.ChannelId}: Contest with no counterpart");
    }

    [Fact]
    public void CounterpartsAreSymmetric()
    {
        var registry = DerivedStatRegistry.CreateDefault();
        foreach (var def in AllRegistered.Where(d => d.CounterpartOf is not null))
        {
            Assert.True(registry.TryGet(def.CounterpartOf!, out var other),
                $"{def.ChannelId}: counterpart '{def.CounterpartOf}' does not resolve");
            Assert.Equal(def.ChannelId, other.CounterpartOf);
        }
    }

    [Fact]
    public void RaceFamiliesDeclareNoCounterpart()
    {
        foreach (var def in AllRegistered.Where(d => d.Class == StatClass.Race))
            Assert.Null(def.CounterpartOf);
    }

    [Fact]
    public void ContestHalvesAreUncapped()
    {
        // Scoped to the two true magnitude unit classes (GameUnits, GameUnitsPerSecond) — SigmoidPoints
        // and SigmoidMultiplierPoints are uncapped INPUTS to a bounded output by design (§2.5), and
        // StatusPotencyPoints' shipped 0.95 resist cap is a pre-existing, documented bounded-ratio-
        // shaped magnitude cap (ssot-power-scale.md §11.6), not the PS-8 violation this test guards.
        var magnitudeUnits = new[] { UnitClass.GameUnits, UnitClass.GameUnitsPerSecond };
        foreach (var def in AllRegistered.Where(d => d.Class == StatClass.Contest && magnitudeUnits.Contains(d.Unit ?? default)))
            Assert.Null(def.Cap);
    }

    [Fact]
    public void EveryCapIsClassified()
    {
        // A capped channel must be classified, not left ambiguous — the PS-8 exemption comment itself
        // lives at the registration site (DerivedStatRegistry.RegisterDefaults), per spec §5's style.
        foreach (var def in AllRegistered.Where(d => d.Cap.HasValue))
            Assert.NotNull(def.Class);
    }

    [Fact]
    public void UnitClassIsReferencedNotRedefined()
    {
        // Architecture guard: exactly one taxonomy enum and one ledger enum in Core, each with its
        // exact expected member set — the retracted magnitude/bounded-ratio/structural scheme (or any
        // future third scheme) never gets a home here (§2.5).
        var asm = typeof(DerivedStatRegistry).Assembly;
        var unitClassTypes = asm.GetTypes().Where(t => t.IsEnum && t.Name == "UnitClass").ToList();
        var statClassTypes = asm.GetTypes().Where(t => t.IsEnum && t.Name == "StatClass").ToList();
        Assert.Single(unitClassTypes);
        Assert.Single(statClassTypes);

        var expectedUnits = new[]
        {
            "GameUnits", "GameUnitsPerSecond", "SigmoidPoints", "SigmoidMultiplierPoints",
            "StatusPotencyPoints", "PerMilleRatio", "Milliseconds", "Count", "Flag", "LadderIndex"
        };
        Assert.Equal(expectedUnits.OrderBy(x => x), Enum.GetNames(unitClassTypes[0]).OrderBy(x => x));

        var expectedClasses = new[] { "Contest", "Race", "Pool", "Feeder" };
        Assert.Equal(expectedClasses.OrderBy(x => x), Enum.GetNames(statClassTypes[0]).OrderBy(x => x));
    }

    /// <summary>The 99 channel ids shipped before catalog-extension (T2) — every one has a real reader
    /// and must keep a non-null Unit. Listed explicitly (not "everything registered before some date")
    /// so a regression on any ONE of them is a named failure, not a count that could hide which id broke.</summary>
    static IEnumerable<string> OriginalNinetyNineIds()
    {
        yield return DerivedStatChannels.ProgressionBonusMaxHp;
        yield return DerivedStatChannels.ProgressionBonusAtk;
        yield return DerivedStatChannels.ProgressionBonusDefense;
        yield return DerivedStatChannels.ProgressionBonusArm1;
        yield return DerivedStatChannels.ProgressionBonusArm2;
        yield return DerivedStatChannels.ProgressionPower;
        yield return DerivedStatChannels.ProgressionRealm;
        yield return DerivedStatChannels.StatusPowerOmni;
        yield return DerivedStatChannels.StatusPowerDot;
        yield return DerivedStatChannels.StatusPowerCc;
        yield return DerivedStatChannels.StatusPowerContagion;
        yield return DerivedStatChannels.StatusResistOmni;
        yield return DerivedStatChannels.StatusResistDot;
        yield return DerivedStatChannels.StatusResistCc;
        yield return DerivedStatChannels.StatusResistContagion;
        // The 12 ORIGINAL family prefixes, hardcoded rather than read from
        // DerivedStatChannels.CombatChannelFamilies — that list is now 28 (H.1 added 16), so it can no
        // longer stand in for "what shipped before T2".
        string[] originalTwelveFamilies =
        {
            "combat.power", "combat.defense", "combat.crit.rate", "combat.crit.resist",
            "combat.crit.damage", "combat.crit.resist.damage", "combat.accuracy", "combat.dodge",
            DerivedStatChannels.CombatShieldCapacityPrefix, DerivedStatChannels.CombatShieldToughnessPrefix,
            DerivedStatChannels.CombatShieldPenPrefix, DerivedStatChannels.CombatShieldRegenPrefix
        };
        var slots = new[] { ElementRoster.OmniId }.Concat(ElementRoster.Concrete.Select(e => e.ToElementId()));
        foreach (var family in originalTwelveFamilies)
        foreach (var slot in slots)
            yield return $"{family}.{slot}";
    }

    [Fact]
    public void NoPlaceholderUnitClass()
    {
        // The 99 shipped-before-T2 channels each have a real reader and must keep their non-null Unit
        // — a regression here would be silent data loss on an existing consumer.
        var original = OriginalNinetyNineIds().ToHashSet(StringComparer.Ordinal);
        var registry = DerivedStatRegistry.CreateDefault();
        foreach (var id in original)
        {
            Assert.True(registry.TryGet(id, out var def), $"missing originally-shipped channel {id}");
            Assert.NotNull(def.Unit);
        }

        // Every one of catalog-extension's 157 new channels carries Unit: null — §2.7 forbids inventing
        // a placeholder for a channel with no nameable consumer at registration time. Counted, not
        // spot-checked, so a channel that slipped in with a guessed Unit is caught even if no single
        // test names it.
        var newNullUnitCount = AllRegistered.Count(d => !original.Contains(d.ChannelId) && d.Unit is null);
        Assert.Equal(157, newNullUnitCount);
    }

    [Fact]
    public void ShippedFamiliesClassify()
    {
        // 9 progression (7 + H.7's 2) + 24 status constants (8 + H.2's 16) + 196 combat (84 + H.1's 112)
        // + 1 healing + 15 resource + 1 move.range + 10 action-category = 256 (99 -> 256, T2).
        Assert.Equal(256, AllRegistered.Count);

        // Every def classifies except three non-combat Theta/progression channels the counterbalance
        // rule does not apply to (actor-hub-ssot.md §H.0's "Non-combat" row). breakthroughSuccess
        // (T4.4) is NOT among them despite being "progression" by name: unlike power/realm's
        // LadderIndex shape, it is the actor's own roll probability with no pair, so it classifies as
        // Pool -- and EveryCapIsClassified requires that, since it also carries a Cap.
        var unclassified = AllRegistered.Where(d => d.Class is null).Select(d => d.ChannelId).ToList();
        Assert.Equal(
            new[]
            {
                DerivedStatChannels.ProgressionPower, DerivedStatChannels.ProgressionRealm,
                DerivedStatChannels.ProgressionXpRate
            }.OrderBy(x => x),
            unclassified.OrderBy(x => x));

        // shield.capacity/regen are the precedent that unpaired is legitimate — if they fail here, the
        // classification is wrong, not them.
        var shieldPools = AllRegistered.Where(d =>
            d.ChannelId.StartsWith(DerivedStatChannels.CombatShieldCapacityPrefix, StringComparison.Ordinal) ||
            d.ChannelId.StartsWith(DerivedStatChannels.CombatShieldRegenPrefix, StringComparison.Ordinal));
        Assert.All(shieldPools, d => Assert.Equal(StatClass.Pool, d.Class));

        // H.5/H.6's resource + move.range and H.3's action-category channels are also unpaired Pool/
        // Race/Feeder — none of them should carry a CounterpartOf.
        var neverPaired = AllRegistered.Where(d =>
            d.ChannelId.StartsWith("resource.", StringComparison.Ordinal) ||
            d.ChannelId == DerivedStatChannels.MoveRange ||
            d.ChannelId.StartsWith("skill.", StringComparison.Ordinal));
        Assert.All(neverPaired, d => Assert.Null(d.CounterpartOf));
    }
}

/// <summary>Sparse status ids resolved dynamically through <see cref="DerivedStatRegistry.TryResolveChannel"/>
/// carry the same classification as their fixed-category siblings — not covered by <see cref="StatTaxonomyTests"/>
/// since they never enter <c>AllRegistered</c>.</summary>
public class DynamicChannelTaxonomyTests
{
    [Theory]
    [InlineData("status.power.wither", "status.resist.wither")]
    [InlineData("status.resist.wither", "status.power.wither")]
    public void SparseStatusIdsClassifyAsContestWithSymmetricCounterpart(string channelId, string expectedCounterpart)
    {
        var registry = DerivedStatRegistry.CreateDefault();
        Assert.True(registry.TryResolveChannel(channelId, out var def));
        Assert.Equal(StatClass.Contest, def.Class);
        Assert.Equal(UnitClass.StatusPotencyPoints, def.Unit);
        Assert.Equal(expectedCounterpart, def.CounterpartOf);
    }

    [Theory]
    [InlineData("status.immune.stun")]
    [InlineData("status.immuneReduction.stun")]
    public void SparseImmuneIdsClassifyAsPoolFlag(string channelId)
    {
        var registry = DerivedStatRegistry.CreateDefault();
        Assert.True(registry.TryResolveChannel(channelId, out var def));
        Assert.Equal(StatClass.Pool, def.Class);
        Assert.Equal(UnitClass.Flag, def.Unit);
        Assert.Null(def.CounterpartOf);
    }

    [Fact]
    public void SparseExposeIdsCarryNoPlaceholderClassification()
    {
        var registry = DerivedStatRegistry.CreateDefault();
        Assert.True(registry.TryResolveChannel("status.expose.dot", out var def));
        Assert.Null(def.Class);
        Assert.Null(def.Unit);
    }
}
