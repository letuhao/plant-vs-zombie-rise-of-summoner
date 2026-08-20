using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Combat;

public class DamagePacketBuilderElementTests
{
    [Fact]
    public void Parses_element_payload_from_overlay()
    {
        var overlay = new Dictionary<string, object?>
        {
            ["amount"] = -100L,
            ["elementPayload"] = new object[]
            {
                new Dictionary<string, object?> { ["element"] = "fire", ["weight"] = 0.7 },
                new Dictionary<string, object?> { ["element"] = "air", ["weight"] = 0.3 }
            }
        };
        var packet = DamagePacketBuilder.FromOverlay(overlay);
        Assert.NotNull(packet.ElementPayload);
        Assert.Equal(2, packet.ElementPayload!.Count);
        Assert.Equal("fire", packet.ElementPayload[0].Element);
        Assert.Equal(0.7, packet.ElementPayload[0].Weight);
    }
}

public class ConditionalOverlayCombatMathTests
{
    [Fact]
    public void Disabled_flag_passes_through()
    {
        var inner = OverlayCombatMath.Create(
            (_, _) => new CombatActorSnapshot(
                ActorDerivedSnapshot.StubNeutral(),
                ActorElementTypes.Neutral));
        var math = new ConditionalOverlayCombatMath(inner) { IsEnabled = () => false };
        var packet = new DamagePacket
        {
            SignedAmount = -100,
            ElementPayload = new List<ElementPayloadComponentDto>
            {
                new() { Element = "fire", Weight = 1.0 }
            }
        };
        Assert.Equal(-100, math.Finalize(-100, "Z1", packet, null));
    }

    [Fact]
    public void Enabled_flag_applies_overlay_math()
    {
        var inner = OverlayCombatMath.Create(
            (ptr, attackerLess) =>
            {
                if (attackerLess)
                    return CombatActorSnapshot.AttackerLess();
                if (string.Equals(ptr, "Z1", StringComparison.OrdinalIgnoreCase))
                    return new CombatActorSnapshot(
                        ActorDerivedSnapshot.StubNeutral(),
                        ActorElementTypes.Create(ElementTypeId.Ice));
                return new CombatActorSnapshot(
                    ActorDerivedSnapshot.StubNeutral().Overlay(new[]
                    {
                        new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, 500),
                        new KeyValuePair<string, double>(DerivedStatChannels.CombatCritRateOmni, -500)
                    }),
                    ActorElementTypes.Neutral);
            },
            rng: new SeededCombatRng(1));
        var math = new ConditionalOverlayCombatMath(inner) { IsEnabled = () => true };
        var packet = new DamagePacket
        {
            SignedAmount = -100,
            ActorPtr = "P1",
            ElementPayload = new List<ElementPayloadComponentDto>
            {
                new() { Element = "fire", Weight = 1.0 }
            }
        };
        var delta = math.Finalize(-100, "Z1", packet, null);
        Assert.NotEqual(-100, delta);
        Assert.Equal(-125, delta);
    }
}

public class OverlayCombatEdgeCaseTests
{
    static CombatActorSnapshot AttackerForHit() =>
        new(
            ActorDerivedSnapshot.StubNeutral().Overlay(new[]
            {
                new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, 500),
                new KeyValuePair<string, double>(DerivedStatChannels.CombatCritRateOmni, -500)
            }),
            ActorElementTypes.Neutral);

    [Fact]
    public void Dual_type_defender_with_hybrid_attacker()
    {
        var calc = new OverlayCombatCalculator();
        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 100,
            Components = new[]
            {
                new ElementPayloadComponent(ElementTypeId.Fire, 0.5),
                new ElementPayloadComponent(ElementTypeId.Air, 0.5)
            },
            Attacker = AttackerForHit(),
            Defender = new CombatActorSnapshot(
                ActorDerivedSnapshot.StubNeutral(),
                ActorElementTypes.Create(ElementTypeId.Ice, ElementTypeId.Air))
        };
        var (_, breakdown) = calc.Compute(request, new SeededCombatRng(1));
        Assert.Equal(-3.125, breakdown.MatchupBonus, 3);
    }

    [Fact]
    public void Final_damage_never_negative_after_floor()
    {
        var calc = new OverlayCombatCalculator();
        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = 10,
            Components = new[] { new ElementPayloadComponent(ElementTypeId.Fire, 1.0) },
            Attacker = AttackerForHit(),
            Defender = new CombatActorSnapshot(
                ActorDerivedSnapshot.StubNeutral().Overlay(new[]
                {
                    new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseOmni, 9999),
                    new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseFire, 9999)
                }),
                ActorElementTypes.Create(ElementTypeId.Air))
        };
        var (_, breakdown) = calc.Compute(request, new SeededCombatRng(1));
        Assert.Equal(0, breakdown.FinalSignedDelta);
        Assert.True(breakdown.Hit);
        Assert.True(breakdown.PowerAdjustedDamage < 0);
    }
}
