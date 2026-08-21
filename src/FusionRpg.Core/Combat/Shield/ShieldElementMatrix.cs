using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat.Shield;

/// <summary>
/// Shield-owned matchup table — attack element vs the shield's element. Returns the UNIT
/// relation (+1 strong / −1 weak / 0 neutral or same); K is applied exactly once in ShieldMath,
/// unlike ElementRingMatrix.RelationShare which bakes K in (shield-system-spec.md §2.4).
/// v1 content is seeded identical to the ring + light/dark mutual counter but is independently
/// editable — diverging from the seed is an Ask-first balance decision (spec §8). The default
/// arm is fail-open 0; the roster-generated golden is what guarantees no pair falls through.
/// </summary>
public static class ShieldElementMatrix
{
    public static int RelationUnit(ElementTypeId attacker, ElementTypeId shieldElement)
    {
        if (attacker == shieldElement)
            return 0;

        return (attacker, shieldElement) switch
        {
            (ElementTypeId.Light, ElementTypeId.Dark) => 1,
            (ElementTypeId.Dark, ElementTypeId.Light) => 1,

            (ElementTypeId.Fire, ElementTypeId.Ice) => 1,
            (ElementTypeId.Fire, ElementTypeId.Air) => -1,

            (ElementTypeId.Ice, ElementTypeId.Earth) => 1,
            (ElementTypeId.Ice, ElementTypeId.Fire) => -1,

            (ElementTypeId.Earth, ElementTypeId.Air) => 1,
            (ElementTypeId.Earth, ElementTypeId.Ice) => -1,

            (ElementTypeId.Air, ElementTypeId.Fire) => 1,
            (ElementTypeId.Air, ElementTypeId.Earth) => -1,

            _ => 0
        };
    }
}
