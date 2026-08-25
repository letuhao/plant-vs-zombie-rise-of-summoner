using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>spec-skill-modifiers.md — ten channels (already registered, T2), plus the two mechanisms
/// their spec requires: effectiveness applied pre-mitigation, and a cooldown-reduction formula with a
/// structural one-tick floor.</summary>
public class SkillModifiersTests
{
    static OverlayCombatRequest BaseRequest(double effectiveness = 1.0) => new()
    {
        BaseOverlayDamage = 100,
        Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
        Attacker = new CombatActorSnapshot(
            ActorDerivedSnapshot.FromValues(new[]
            {
                new KeyValuePair<string, double>(DerivedStatChannels.CombatPowerFire, 20)
            }),
            ActorElementTypes.Neutral),
        Defender = new CombatActorSnapshot(
            ActorDerivedSnapshot.FromValues(new[]
            {
                new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseFire, 10)
            }),
            ActorElementTypes.Neutral),
        ForceHit = true,
        ForceCrit = false,
        EffectivenessMultiplier = effectiveness
    };

    [Fact]
    public void EffectivenessIsPreMitigation()
    {
        // Raising effectiveness and raising defense by the SAME ratio should not cancel if
        // effectiveness only scaled the flat base and defense only scaled the delta -- the proof that
        // effectiveness lands where combat-damage-ssot.md §6.7 says (inside baseDamage, before the
        // weightedDelta defense already answers), not as an independent post-mitigation multiplier.
        var calculator = new OverlayCombatCalculator();
        var rng = new FixedCombatRng(0.0);

        var (baseline, _) = calculator.Compute(BaseRequest(1.0), rng);
        var (doubled, _) = calculator.Compute(BaseRequest(2.0), rng);

        // BaseOverlayDamage=100 doubled to 200 adds +100 to the pre-delta base, which flows straight
        // into powerAdjusted (weightedDelta unchanged: power 20 - defense 10 = 10 either way) --
        // exactly the "+100 to the pre-mitigation term" a Feeder application produces.
        Assert.Equal(baseline - 100, doubled); // signed deltas: both negative, doubled is 100 more damage
    }

    [Fact]
    public void EffectivenessCannotBypassDefense()
    {
        // The executable form of §2's contract: effectiveness scales the ATTACKER's base, but an
        // overwhelming DEFENSE still floors total damage at 0 -- effectiveness never creates damage
        // out of a defense wall the way a post-mitigation multiplier would (multiplying zero stays
        // zero either way, but this proves the pre-mitigation delta is what actually floors, not a
        // multiplier papering over a negative).
        var calculator = new OverlayCombatCalculator();
        var rng = new FixedCombatRng(0.0);

        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 10,
            EffectivenessMultiplier = 1000.0, // absurdly high -- still must not bypass defense
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            Attacker = new CombatActorSnapshot(
                ActorDerivedSnapshot.FromValues(new[]
                {
                    new KeyValuePair<string, double>(DerivedStatChannels.CombatPowerFire, 0)
                }),
                ActorElementTypes.Neutral),
            Defender = new CombatActorSnapshot(
                ActorDerivedSnapshot.FromValues(new[]
                {
                    // Defense large enough to exceed even the 1000x-scaled base (10 * 1000 = 10,000).
                    new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseFire, 50_000)
                }),
                ActorElementTypes.Neutral),
            ForceHit = true,
            ForceCrit = false
        };

        var (signedDelta, breakdown) = calculator.Compute(request, rng);
        Assert.Equal(0, signedDelta);
        Assert.Equal(0, breakdown.FinalSignedDelta);
    }

    [Fact]
    public void EffectivenessDefaultsToOneAndMovesNoGolden()
    {
        // NoGoldensMoveAtDefaults: EffectivenessMultiplier's default (1.0) must be a true no-op.
        var calculator = new OverlayCombatCalculator();
        var withDefault = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            ForceHit = true,
            ForceCrit = false
        };
        var withExplicitOne = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            ForceHit = true,
            ForceCrit = false,
            EffectivenessMultiplier = 1.0
        };

        var (a, _) = calculator.Compute(withDefault, new FixedCombatRng(0.0));
        var (b, _) = calculator.Compute(withExplicitOne, new FixedCombatRng(0.0));
        Assert.Equal(a, b);
    }

    [Fact]
    public void CooldownFloorsAtOneTick()
    {
        Assert.Equal(1, CooldownMath.ApplyReduction(100, 990));   // 99%
        Assert.Equal(1, CooldownMath.ApplyReduction(100, 1000));  // 100%
        Assert.Equal(1, CooldownMath.ApplyReduction(1, 0));
    }

    [Fact]
    public void CooldownReductionUncapped()
    {
        // Arbitrarily large reduction never yields 0 or negative, and never throws -- only the
        // duration is floored, the reduction ratio itself has no ceiling (§3).
        Assert.Equal(1, CooldownMath.ApplyReduction(1000, 50_000));        // 5,000%
        Assert.Equal(1, CooldownMath.ApplyReduction(1000, 1_000_000_000)); // 100,000,000%
    }

    [Fact]
    public void CooldownReductionScalesNormallyBelowTheFloor()
    {
        Assert.Equal(50, CooldownMath.ApplyReduction(100, 500));  // 50%
        Assert.Equal(75, CooldownMath.ApplyReduction(100, 250));  // 25%
        Assert.Equal(100, CooldownMath.ApplyReduction(100, 0));
    }

    [Fact]
    public void NoCooldownCounterpartRegistered()
    {
        // skill.cooldown.* is Race -- guard-stat-pairs.ps1 enforces this at the catalog level; this is
        // the same claim proven directly against the live registry.
        var registry = DerivedStatRegistry.CreateDefault();
        foreach (var category in DerivedStatChannels.ActionCategories)
        {
            var channel = DerivedStatChannels.SkillCooldown(category);
            Assert.True(registry.TryGet(channel, out var def), $"missing {channel}");
            Assert.Equal(StatClass.Race, def.Class);
            Assert.Null(def.CounterpartOf);
        }
    }

    [Fact]
    public void SkillEffectivenessIsFeederWithNoCounterpart()
    {
        var registry = DerivedStatRegistry.CreateDefault();
        foreach (var category in DerivedStatChannels.ActionCategories)
        {
            var channel = DerivedStatChannels.SkillEffectiveness(category);
            Assert.True(registry.TryGet(channel, out var def), $"missing {channel}");
            Assert.Equal(StatClass.Feeder, def.Class);
            Assert.Null(def.CounterpartOf);
        }
    }

    [Fact]
    public void EnvelopeReferencesCatalog()
    {
        var envelope = ActionEnvelope.NoOp with { CooldownChannel = DerivedStatChannels.SkillCooldown("attack") };
        var registry = DerivedStatRegistry.CreateDefault();

        Assert.NotNull(envelope.CooldownChannel);
        Assert.True(registry.IsKnown(envelope.CooldownChannel!));
    }

    [Fact]
    public void EnvelopeDefaultsToNoCooldownChannel()
    {
        // No universal default the way SpeedChannel has one -- which category applies is the action's
        // own choice, so declaring none must not silently pick one.
        Assert.Null(ActionEnvelope.NoOp.CooldownChannel);
    }

    sealed class FixedCombatRng : ICombatRng
    {
        readonly double _value;
        public FixedCombatRng(double value) => _value = value;
        public int Next(int maxExclusive) => (int)(_value * maxExclusive);
    }
}
