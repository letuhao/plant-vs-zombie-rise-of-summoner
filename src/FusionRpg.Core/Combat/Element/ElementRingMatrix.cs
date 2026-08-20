using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Combat.Element;

/// <summary>
/// Normative ring-cycle STR/WEK/NEU table — fire → ice → earth → air → fire.
/// Single SSOT for matchup relations; combat must not duplicate this table.
/// </summary>
public static class ElementRingMatrix
{
    public static ElementMatchupRelation GetRelation(ElementTypeId attacker, ElementTypeId defender)
    {
        if (attacker == defender)
            return ElementMatchupRelation.Same;

        return (attacker, defender) switch
        {
            (ElementTypeId.Fire, ElementTypeId.Ice) => ElementMatchupRelation.Strong,
            (ElementTypeId.Fire, ElementTypeId.Air) => ElementMatchupRelation.Weak,
            (ElementTypeId.Fire, ElementTypeId.Earth) => ElementMatchupRelation.Neutral,

            (ElementTypeId.Ice, ElementTypeId.Earth) => ElementMatchupRelation.Strong,
            (ElementTypeId.Ice, ElementTypeId.Fire) => ElementMatchupRelation.Weak,
            (ElementTypeId.Ice, ElementTypeId.Air) => ElementMatchupRelation.Neutral,

            (ElementTypeId.Earth, ElementTypeId.Air) => ElementMatchupRelation.Strong,
            (ElementTypeId.Earth, ElementTypeId.Ice) => ElementMatchupRelation.Weak,
            (ElementTypeId.Earth, ElementTypeId.Fire) => ElementMatchupRelation.Neutral,

            (ElementTypeId.Air, ElementTypeId.Fire) => ElementMatchupRelation.Strong,
            (ElementTypeId.Air, ElementTypeId.Earth) => ElementMatchupRelation.Weak,
            (ElementTypeId.Air, ElementTypeId.Ice) => ElementMatchupRelation.Neutral,

            _ => ElementMatchupRelation.Neutral
        };
    }

    /// <summary>Relation share added to 1.0 multiplier: STR +k, WEK −k, NEU/SAME 0.</summary>
    public static double RelationShare(ElementMatchupRelation relation) =>
        relation switch
        {
            ElementMatchupRelation.Strong => ElementMatchupPolicy.MatchupShareK,
            ElementMatchupRelation.Weak => -ElementMatchupPolicy.MatchupShareK,
            _ => 0.0
        };
}
