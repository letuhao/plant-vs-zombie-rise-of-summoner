using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat.Shield;

public readonly record struct ShieldLayerResult(long Spent, long Remainder, long DamageToShield);

/// <summary>
/// Pure single-layer absorb math — shield-system-spec.md §2.4. All arithmetic is 64-bit
/// integer at permille scale; rounding is half away from zero via (num + d/2) / d on
/// non-negative operands. No rolls, no floats in any game-affecting branch.
/// </summary>
public static class ShieldMath
{
    /// <summary>Gate-entry clamp so input × damageToShield can never overflow long.</summary>
    public const long MaxInput = 1_000_000_000;

    /// <param name="input">Damage reaching this layer, HP units (≥ 0).</param>
    /// <param name="shieldHp">This shield's current pool, HP units (≥ 0).</param>
    /// <param name="weightedRelationUnitPm">Σ(componentWeightPm × relUnit) in [−1000, 1000].</param>
    /// <param name="breakerDelta">pen − toughness in HP units (may be negative).</param>
    /// <param name="hitCount">Coalesced hit count (≥ 1); scales the flat breaker term so
    /// coalesced ≡ n× uncoalesced.</param>
    public static ShieldLayerResult AbsorbLayer(
        long input, long shieldHp, long weightedRelationUnitPm, long breakerDelta, long hitCount)
    {
        if (input <= 0 || shieldHp <= 0)
            return new ShieldLayerResult(0, input < 0 ? 0 : input, 0);
        if (input > MaxInput) input = MaxInput;
        if (hitCount < 1) hitCount = 1;

        // elemMod = relUnitPm × KPm × input / 1e6 (permille × permille), half away from zero.
        var elemMod = RoundDivSigned(weightedRelationUnitPm * ShieldPolicy.MatchupShareKPm * input, 1_000_000);
        var raw = input + elemMod + hitCount * breakerDelta;

        var floor = CeilDiv(ShieldPolicy.ChipFloorKPm * input, 1000);
        var cap = ShieldPolicy.PenCapKPm * input / 1000;
        var damageToShield = Math.Clamp(raw, floor, cap);

        var spent = Math.Min(shieldHp, damageToShield);
        // Proportional remainder, half away from zero; operands non-negative here.
        var remainder = damageToShield == 0
            ? input
            : (input * (damageToShield - spent) + damageToShield / 2) / damageToShield;
        return new ShieldLayerResult(spent, remainder, damageToShield);
    }

    /// <summary>
    /// Σ(componentWeightPm × relUnit) against one shield's element. Untyped shield → 0.
    /// Weights convert to permille once (half away from zero) — the only float boundary.
    /// </summary>
    public static long WeightedRelationUnitPm(
        IReadOnlyList<ElementPayloadComponent> components, ElementTypeId? shieldElement)
    {
        if (shieldElement is not { } shieldEl || components.Count == 0)
            return 0;
        long sum = 0;
        for (var i = 0; i < components.Count; i++)
        {
            var weightPm = (long)Math.Round(components[i].Weight * 1000.0, MidpointRounding.AwayFromZero);
            sum += weightPm * ShieldElementMatrix.RelationUnit(components[i].Element, shieldEl);
        }

        return sum;
    }

    static long CeilDiv(long num, long div) => (num + div - 1) / div;

    static long RoundDivSigned(long num, long div)
    {
        // div > 0; num may be negative — round half away from zero.
        return num >= 0 ? (num + div / 2) / div : -((-num + div / 2) / div);
    }
}
