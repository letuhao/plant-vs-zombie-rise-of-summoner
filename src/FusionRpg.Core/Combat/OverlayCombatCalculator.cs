using FusionRpg.Contracts;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat;

public sealed class OverlayCombatRequest
{
    public double BaseOverlayDamage { get; init; }
    public IReadOnlyList<ElementPayloadComponent> Components { get; init; } = Array.Empty<ElementPayloadComponent>();
    public CombatActorSnapshot Attacker { get; init; } = CombatActorSnapshot.AttackerLess();
    public CombatActorSnapshot Defender { get; init; } = new(ActorDerivedSnapshot.StubNeutral(), ActorElementTypes.Neutral);

    /// <summary>Debug/test: when set, overrides the hit roll.</summary>
    public bool? ForceHit { get; init; }

    /// <summary>Debug/test: when set and hit, overrides the crit roll.</summary>
    public bool? ForceCrit { get; init; }

    /// <summary>Host resolution profile — overlay default keeps behavior byte-identical.</summary>
    public CombatProfile Profile { get; init; } = CombatProfile.Overlay;
}

/// <summary>Overlay damage pipeline — combat-damage-ssot.md §6.</summary>
public sealed class OverlayCombatCalculator
{
    readonly IElementHub _elementHub;

    public OverlayCombatCalculator(IElementHub? elementHub = null) =>
        _elementHub = elementHub ?? ElementHub.Default;

    public (long SignedDelta, OverlayCombatBreakdown Breakdown) Compute(
        OverlayCombatRequest request,
        ICombatRng rng)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        // Omni fallback (combat-unification, resolver-core): an EMPTY component list is a
        // legal untyped attack resolved over the omni halves only — matchup 0. Replaces the
        // former hard throw; content-boundary validation (ElementPayload.Validate) stays
        // strict, and the overlay dispatcher never builds an empty request (payload-null is
        // pass-through in OverlayCombatMath).
        var omniFallback = request.Components.Count == 0;
        if (!omniFallback)
            ElementPayload.Validate(request.Components);

        var matchupBonus = omniFallback
            ? 0.0
            : _elementHub.ResolvePayloadBonus(
                request.Components,
                request.Defender.ElementTypes,
                request.BaseOverlayDamage);

        var weightedDelta = 0.0;
        var pHitFinal = 0.0;
        var pCritFinal = 0.0;
        var critMultFinal = 0.0;

        if (omniFallback)
        {
            var atk = request.Attacker.Derived;
            var def = request.Defender.Derived;
            weightedDelta = atk.Get(DerivedStatChannels.CombatPowerOmni)
                            - def.Get(DerivedStatChannels.CombatDefenseOmni);
            pHitFinal = CombatProbability.Sigmoid(
                atk.Get(DerivedStatChannels.CombatAccuracyOmni) - def.Get(DerivedStatChannels.CombatDodgeOmni),
                CombatProbabilityPolicy.AccuracyScale);
            pCritFinal = CombatProbability.Sigmoid(
                atk.Get(DerivedStatChannels.CombatCritRateOmni) - def.Get(DerivedStatChannels.CombatCritResistOmni),
                CombatProbabilityPolicy.CritRateScale);
            critMultFinal = 1.0 + CombatProbability.Sigmoid(
                atk.Get(DerivedStatChannels.CombatCritDamageOmni) - def.Get(DerivedStatChannels.CombatCritResistDamageOmni),
                CombatProbabilityPolicy.CritDamageScale);
        }

        foreach (var c in request.Components)
        {
            var componentBonus = _elementHub.ResolveComponentBonus(
                c.Element,
                request.Defender.ElementTypes,
                request.BaseOverlayDamage);

            var power = CombatDerivedReader.Power(request.Attacker.Derived, c.Element);
            var defense = CombatDerivedReader.Defense(request.Defender.Derived, c.Element);
            var effectiveDelta = (power - defense) + componentBonus;
            weightedDelta += c.Weight * effectiveDelta;

            var accuracyDelta = CombatDerivedReader.Accuracy(request.Attacker.Derived, c.Element)
                                - CombatDerivedReader.Dodge(request.Defender.Derived, c.Element);
            pHitFinal += c.Weight * CombatProbability.Sigmoid(accuracyDelta, CombatProbabilityPolicy.AccuracyScale);

            var critRateDelta = CombatDerivedReader.CritRate(request.Attacker.Derived, c.Element)
                                - CombatDerivedReader.CritResist(request.Defender.Derived, c.Element);
            pCritFinal += c.Weight * CombatProbability.Sigmoid(critRateDelta, CombatProbabilityPolicy.CritRateScale);

            var critDmgDelta = CombatDerivedReader.CritDamage(request.Attacker.Derived, c.Element)
                               - CombatDerivedReader.CritResistDamage(request.Defender.Derived, c.Element);
            critMultFinal += c.Weight * (1.0 + CombatProbability.Sigmoid(critDmgDelta, CombatProbabilityPolicy.CritDamageScale));
        }

        var hit = request.ForceHit ?? CombatProbability.RollSuccess(rng, pHitFinal);
        var crit = hit && (request.ForceCrit ?? CombatProbability.RollSuccess(rng, pCritFinal));

        var powerAdjusted = request.BaseOverlayDamage + weightedDelta;
        double finalDamage;
        if (!hit)
            finalDamage = 0;
        else
        {
            finalDamage = Math.Max(0, powerAdjusted);
            if (crit)
                finalDamage *= critMultFinal;

            // Min-chip floor (owner decision 6): profile-scoped — a landed hit deals at
            // least ceil(share × base), min 1. Overlay profile is 0 → this branch is dead
            // there and behavior stays byte-identical.
            if (request.Profile.MinChipShareKPm > 0)
            {
                var chip = Math.Max(1.0,
                    Math.Ceiling(request.BaseOverlayDamage * request.Profile.MinChipShareKPm / 1000.0));
                if (finalDamage < chip)
                    finalDamage = chip;
            }
        }

        var signedDelta = finalDamage > 0 ? -(long)Math.Round(finalDamage) : 0L;
        var breakdown = new OverlayCombatBreakdown
        {
            Hit = hit,
            Crit = crit,
            MatchupBonus = matchupBonus,
            WeightedDelta = weightedDelta,
            PowerAdjustedDamage = powerAdjusted,
            FinalSignedDelta = signedDelta,
            PHitFinal = pHitFinal,
            PCritFinal = pCritFinal,
            CritMultiplierFinal = critMultFinal
        };

        return (signedDelta, breakdown);
    }

    public static IReadOnlyList<ElementPayloadComponent> ParseComponents(IEnumerable<ElementPayloadComponentDto>? dtos)
    {
        if (dtos == null || !dtos.Any())
            return Array.Empty<ElementPayloadComponent>();

        var list = new List<ElementPayloadComponent>();
        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Element))
                throw new ArgumentException("Element payload component missing element id.");
            if (!ElementRoster.TryParse(dto.Element, out var parsed))
                throw new ArgumentException($"Unknown element id '{dto.Element}'.");
            list.Add(new ElementPayloadComponent(parsed, dto.Weight));
        }

        return list;
    }
}
