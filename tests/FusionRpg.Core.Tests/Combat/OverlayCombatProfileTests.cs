using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Status;
using Xunit;

namespace FusionRpg.Core.Tests.Combat;

public class OverlayCombatProfileTests
{
    [Fact]
    public void Combat_glass_vs_neutral_increases_damage()
    {
        var calc = new OverlayCombatCalculator();
        var glass = ActorDerivedProfiles.Get(ActorDerivedProfiles.CombatGlass);
        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            Attacker = CombatActorSnapshot.AttackerLess(),
            Defender = new CombatActorSnapshot(glass, ActorElementTypes.Neutral),
            ForceHit = true,
            ForceCrit = false
        };
        var (_, breakdown) = calc.Compute(request, new SeededCombatRng(1));
        // glass: defense.omni = -50 → powerAdjusted = 100 - (-50) = 150
        Assert.Equal(150, breakdown.PowerAdjustedDamage, 3);
        Assert.Equal(-150, breakdown.FinalSignedDelta);
    }

    [Fact]
    public void Ice_tank_ice_defense_vs_ice_payload_not_fire()
    {
        var calc = new OverlayCombatCalculator();
        var tank = ActorDerivedProfiles.Get(ActorDerivedProfiles.CombatIceTank);
        OverlayCombatRequest Req(ElementTypeId el) => new()
        {
            BaseOverlayDamage = 100,
            Components = new[] { new ElementPayloadComponent(el, 1.0) },
            Attacker = new CombatActorSnapshot(
                ActorDerivedSnapshot.StubNeutral().Overlay(new[]
                {
                    new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, 500)
                }),
                ActorElementTypes.Neutral),
            Defender = new CombatActorSnapshot(tank, ActorElementTypes.Create(ElementTypeId.Ice)),
            ForceHit = true,
            ForceCrit = false
        };

        var ice = calc.Compute(Req(ElementTypeId.Ice), new SeededCombatRng(1));
        var fire = calc.Compute(Req(ElementTypeId.Fire), new SeededCombatRng(1));

        // ice def 30 + fire STR vs ice (+25 matchup) → fire powerAdjusted = 100+25 = 125
        Assert.Equal(70, ice.Breakdown.PowerAdjustedDamage, 3);
        Assert.Equal(125, fire.Breakdown.PowerAdjustedDamage, 3);
        Assert.Equal(25, fire.Breakdown.MatchupBonus, 3);
        Assert.Equal(0, ice.Breakdown.MatchupBonus, 3);
    }

    [Fact]
    public void Dual_type_calculator_product_rule_e2e()
    {
        // Fire vs Ice+Air: (1.25)*(0.75)-1 = -0.0625 → bonus -6.25
        var calc = new OverlayCombatCalculator();
        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            Attacker = CombatActorSnapshot.AttackerLess(),
            Defender = new CombatActorSnapshot(
                ActorDerivedSnapshot.StubNeutral(),
                ActorElementTypes.Create(ElementTypeId.Ice, ElementTypeId.Air)),
            ForceHit = true,
            ForceCrit = false
        };
        var (_, breakdown) = calc.Compute(request, new SeededCombatRng(1));
        Assert.Equal(-6.25, breakdown.MatchupBonus, 3);
        Assert.Equal(-94, breakdown.FinalSignedDelta);
    }
}
