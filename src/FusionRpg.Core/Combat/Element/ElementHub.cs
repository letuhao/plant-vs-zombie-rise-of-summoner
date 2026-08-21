using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat.Element;

public sealed class ElementHub : IElementHub
{
    public static ElementHub Default { get; } = new();

    public double ResolveComponentBonus(
        ElementTypeId attackerElement,
        ActorElementTypes defenderTypes,
        double baseOverlayDamage)
    {
        if (defenderTypes.IsNeutral)
            return 0.0;

        var combinedMult = 1.0;
        if (defenderTypes.Primary is { } primary)
            combinedMult *= SlotMultiplier(attackerElement, primary);
        if (defenderTypes.Secondary is { } secondary)
            combinedMult *= SlotMultiplier(attackerElement, secondary);

        return (combinedMult - 1.0) * baseOverlayDamage;
    }

    public double ResolvePayloadBonus(
        IReadOnlyList<ElementPayloadComponent> components,
        ActorElementTypes defenderTypes,
        double baseOverlayDamage)
    {
        // Omni fallback: empty payloads are legal untyped attacks — zero matchup by rule.
        if (components == null || components.Count == 0)
            return 0.0;
        ElementPayload.Validate(components);
        var total = 0.0;
        foreach (var c in components)
        {
            total += c.Weight * ResolveComponentBonus(c.Element, defenderTypes, baseOverlayDamage);
        }

        return total;
    }

    static double SlotMultiplier(ElementTypeId attacker, ElementTypeId defenderSlot)
    {
        var relation = ElementRingMatrix.GetRelation(attacker, defenderSlot);
        return 1.0 + ElementRingMatrix.RelationShare(relation);
    }
}
