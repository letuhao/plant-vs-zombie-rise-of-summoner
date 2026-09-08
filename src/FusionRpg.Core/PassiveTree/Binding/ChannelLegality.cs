using FusionRpg.Core.PassiveTree.Catalog;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.PassiveTree.Binding;

/// <summary>
/// The thirteen-`UnitClass` verdict table, centralized (task D1, spec-tree-binder.md §4.1, §4.2 item
/// 3, §6 M3). One place that answers "does a node atom targeting this channel, with this scaleAxis
/// and this op, bind" — so the rule is never re-derived a second time the way R4's `tierWeight`
/// duplication happened once already.
///
/// <para><b>Counted, not asserted:</b> of the thirteen <see cref="UnitClass"/> values, three accept a
/// ladder-scaled `+X` (<see cref="Verdict.LadderScaled"/>), a fourth accepts a flat per-mille `+X` but
/// never a ladder-scaled one (<see cref="Verdict.FlatPermilleOnly"/>), three accept a `Θ`-linear point
/// grant and never a `P(Θ)` one (<see cref="Verdict.ThetaLinear"/>), and six refuse outright
/// (<see cref="Verdict.Refused"/>). 3 + 1 + 3 + 6 = 13 — <c>ChannelLegalityTests</c> proves this by
/// enumerating <c>Enum.GetValues&lt;UnitClass&gt;()</c>, never by asserting the bare count, so a
/// fourteenth class added to <c>UnitClass</c> without a verdict here fails loudly
/// (<see cref="VerdictFor"/>'s default arm throws) instead of silently falling through.</para>
///
/// <para><b>Relationship to <see cref="PassiveTreeCatalogLoader"/>'s own inline check.</b> C3 already
/// shipped an axis/`UnitClass` agreement refusal inside `LoadAtom` (the outright-refuse set, and the
/// expected-axis switch) with its own 34 passing tests. This class is the same rule, generalized and
/// independently testable — it does not replace that check and the catalog loader is left untouched
/// by this task, so C3's tests keep passing unmodified.</para>
/// </summary>
public static class ChannelLegality
{
    /// <summary>What a `+X` node atom is allowed to do against a channel of a given <see cref="UnitClass"/>.</summary>
    public enum Verdict
    {
        /// <summary><see cref="UnitClass.GameUnits"/>, <see cref="UnitClass.GameUnitsPerSecond"/>,
        /// <see cref="UnitClass.ReciprocalPoints"/> — binds `kMicro · P(Θ_node) / 1e6`, scaleAxis
        /// <see cref="ScaleAxis.PTheta"/>. The canonical case.</summary>
        LadderScaled,

        /// <summary><see cref="UnitClass.PerMilleRatio"/> — flat per-mille points planned against the
        /// clamp, scaleAxis <see cref="ScaleAxis.FlatPermille"/>. Never ladder- or `Θ`-scaled: it is a
        /// bounded ratio and `P(Θ)`-scaling saturates it in a few tiers, killing the soul track on
        /// that node (§4.1's `PerMilleRatio` row).</summary>
        FlatPermilleOnly,

        /// <summary><see cref="UnitClass.SigmoidPoints"/>, <see cref="UnitClass.SigmoidMultiplierPoints"/>,
        /// <see cref="UnitClass.StatusPotencyPoints"/> — binds `kMicro · Θ_node / 1e6`, scaleAxis
        /// <see cref="ScaleAxis.Theta"/>. A `P(Θ)` (PTheta-axis) amount is a design error (PS-1/PS-3):
        /// `contentScale` never touches a rate input and contests read `Θ`, linear.</summary>
        ThetaLinear,

        /// <summary><see cref="UnitClass.Milliseconds"/>, <see cref="UnitClass.Count"/>,
        /// <see cref="UnitClass.Flag"/>, <see cref="UnitClass.LadderIndex"/>,
        /// <see cref="UnitClass.AptitudePoints"/>, <see cref="UnitClass.LoamUnits"/> — refused
        /// outright as a magnitude target, regardless of scaleAxis (§4.1's six-class refuse row).</summary>
        Refused,
    }

    /// <summary>Five primary channels where lower is better (`ModifierOp.DirectionOf`,
    /// `ModifierOp.cs:94,98`, counted at five per §4.2 item 3, superseding the predecessor research's
    /// count of one). A "+X" node against any of these is a self-nerf the cost function correctly
    /// prices as negative while the node's own copy reads as a buff.</summary>
    public static readonly IReadOnlyList<string> LowerIsBetterPrimaries = new[]
    {
        StatChannels.AttackInterval, StatChannels.ProduceInterval, StatChannels.AttackCountdown,
        StatChannels.ProduceCountdown, StatChannels.TakeDmgMultiplier,
    };

    /// <summary>§6 M3, named for the message this rule's own refusal carries: "there is no `More` on
    /// the derived side" (`AtomKindRegistry.cs:537`, `AtomDerivedSubsystem.TryParseOp`). Derived ops
    /// are `Flat | Increased | Replace | Flag` — <see cref="NodeAtomOp"/> itself has no `More` member,
    /// so a node atom authoring `"op": "more"` fails `Enum.TryParse&lt;NodeAtomOp&gt;` at catalog load
    /// before it ever reaches this class. The refusal is structural (a member that does not exist),
    /// not a runtime check this class needs to duplicate — <c>ChannelLegalityTests</c> names the rule
    /// against the shipped enum rather than re-implementing the parse.</summary>
    public const string NoMoreOnDerivedRuleName = "M3";

    /// <summary>Maps a <see cref="UnitClass"/> to its verdict (§4.1's rule table). Enumerable by
    /// design: every one of the thirteen values must appear in exactly one arm below, and the default
    /// arm throws rather than silently defaulting a fourteenth class to <see cref="Verdict.Refused"/>
    /// — a class added to <c>UnitClass</c> without a verdict here must fail loudly, the same discipline
    /// <c>UnitClassContractParityTests</c> already applies to the TypeScript union.</summary>
    public static Verdict VerdictFor(UnitClass unitClass) => unitClass switch
    {
        UnitClass.GameUnits or UnitClass.GameUnitsPerSecond or UnitClass.ReciprocalPoints => Verdict.LadderScaled,
        UnitClass.PerMilleRatio => Verdict.FlatPermilleOnly,
        UnitClass.SigmoidPoints or UnitClass.SigmoidMultiplierPoints or UnitClass.StatusPotencyPoints => Verdict.ThetaLinear,
        UnitClass.Milliseconds or UnitClass.Count or UnitClass.Flag or UnitClass.LadderIndex
            or UnitClass.AptitudePoints or UnitClass.LoamUnits => Verdict.Refused,
        _ => throw new ArgumentOutOfRangeException(nameof(unitClass), unitClass,
            "ChannelLegality: a UnitClass with no verdict — spec-tree-binder.md §4.1 must name its " +
            "row before this switch can bind it; never fall through to a default"),
    };

    /// <summary>The scaleAxis a bindable (non-<see cref="Verdict.Refused"/>) verdict requires.
    /// <c>null</c> for <see cref="Verdict.Refused"/>, which accepts no axis at all.</summary>
    public static ScaleAxis? ExpectedAxis(UnitClass unitClass) => VerdictFor(unitClass) switch
    {
        Verdict.LadderScaled => ScaleAxis.PTheta,
        Verdict.FlatPermilleOnly => ScaleAxis.FlatPermille,
        Verdict.ThetaLinear => ScaleAxis.Theta,
        _ => null,
    };

    /// <summary>
    /// The full bind-time legality check for one node atom (§4.1, §4.2 item 3). Refuses, in order:
    /// (1) one of the six outright-refuse classes, regardless of axis; (2) an axis that disagrees with
    /// the class's one legal axis — the same silent-failure pairing the catalog loader's own inline
    /// check already catches, generalized here so it is independently testable by enumeration; (3) a
    /// `Flat`/`Increased` "+X" grant against one of the five <see cref="LowerIsBetterPrimaries"/> —
    /// this schema's `kMicro` is always a non-negative share of budget, so there is no way to author
    /// the "-X" such a channel would actually need, and a "+X" against it is a self-nerf by
    /// construction, not merely by an unlucky sign.
    /// </summary>
    public static void CheckBind(NodeAtom atom)
    {
        var verdict = VerdictFor(atom.UnitClass);
        if (verdict == Verdict.Refused)
            throw new BindRefusal(
                $"channel '{atom.ChannelId}': unitClass '{atom.UnitClass}' is refused as a magnitude " +
                "target outright (§4.1's six-class refuse set: Milliseconds, Count, Flag, LadderIndex, " +
                "AptitudePoints, LoamUnits)");

        var expected = ExpectedAxis(atom.UnitClass)!.Value;
        if (atom.ScaleAxis != expected)
        {
            var rule = verdict switch
            {
                Verdict.FlatPermilleOnly => "binds flat per-mille only, never ladder- or Θ-scaled",
                Verdict.ThetaLinear => "binds Θ-linear only — a P(Θ) amount is refused",
                _ => "must carry a ladder-scaled (PTheta) amount",
            };
            throw new BindRefusal(
                $"channel '{atom.ChannelId}': unitClass '{atom.UnitClass}' {rule}, got scaleAxis " +
                $"'{atom.ScaleAxis}' (§4.1, class named)");
        }

        if ((atom.Op == NodeAtomOp.Flat || atom.Op == NodeAtomOp.Increased) && StatChannels.IsLowerBetter(atom.ChannelId))
            throw new BindRefusal(
                $"channel '{atom.ChannelId}' is LowerIsBetter — a {atom.Op} '+X' node is a self-nerf the " +
                "cost function prices as a penalty while the node's own copy reads as a buff (§4.2 item " +
                "3); this schema's amount is always a non-negative share and has no way to author the " +
                "'-X' this channel actually needs");
    }
}
