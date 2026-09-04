using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Items.Display;

/// <summary>
/// G3 §2.1's projection (spec-item-card.md) — the ONLY line producer. Pure: no Unity value, no
/// browser, testable with the game closed (SC8). Three levels build on this one: Line here, Card and
/// Compare are ordered collections of what this produces — there is no second path to a line.
/// </summary>
public static class ItemDisplayRenderer
{
    // ---- Rule 1 — the shipped percent conversion, adopted, not reinvented -------------------------
    // web/fusion-rpg-web/src/features/demons/patronView.ts:23 (verified): (milli/10).toFixed(1),
    // trailing ".0" stripped. One convention; patronView is asked to call this instead of owning it.
    public static string FormatPerMille(int milli)
    {
        var tenths = milli / 10.0;
        var text = tenths.ToString("F1");
        return (text.EndsWith(".0", StringComparison.Ordinal) ? text[..^2] : text) + "%";
    }

    /// <summary>Rule 2: never render a non-zero per-mille as 0% — round away from zero, the direction
    /// the engine itself uses (<c>CurveTable.DivRoundHalfAway</c>).</summary>
    public static long RoundAwayFromZero(long numerator, long denominator) =>
        CurveTable.DivRoundHalfAway(numerator, denominator);

    public static string FormatMilliseconds(int ms) =>
        ms < 1000 ? $"{ms} ms" : $"{ms / 1000.0:F1} s";

    /// <summary>Rule P for a sigmoid CONTEXT read (not the power scalar, which is module 9's own
    /// `CardPower`) — one decimal in percentage points, with the "this is approximate" marker.</summary>
    public static string FormatSigmoidContext(int deltaPoints, double scale) =>
        $"≈ {(deltaPoints / scale):F1} pp";

    /// <summary>
    /// The roll-quality bar. Only <see cref="RollPolicy.OnInstantiate"/> gets one — `Fixed` has no
    /// luck to show, `OnApply` shows the band the hit rolled, not the item's own luck.
    /// </summary>
    public static RollBar? BarFor(RollPolicy roll, int qualityPerMille) => roll switch
    {
        RollPolicy.Fixed => null,
        RollPolicy.OnApply => null,
        RollPolicy.OnInstantiate => new RollBar(
            Math.Clamp((qualityPerMille * RollBar.MaxSegments + 999) / 1000, 1, RollBar.MaxSegments)),
        _ => null,
    };

    /// <summary>
    /// Produce one line. <paramref name="frozenValue"/> is the integer already in
    /// <c>effect_instance_atom.values_json</c> — this NEVER re-applies a curve or re-rolls (Rule 3).
    /// Never a tier number, an atom id, a family id, or a name band in <paramref name="args"/> (§2.4).
    /// </summary>
    public static DisplayLine Line(
        DisplayTemplateRow template, AtomRow atom, string frame, long frozenValue,
        SourceKind sourceKind, int groupOrder, UnitClass unit, string? elementVariant = null,
        RollPolicy roll = RollPolicy.Fixed, int qualityPerMille = 1000, string? contextRead = null)
    {
        if (template.Status != "live")
            throw new DisplayTemplateRejection(
                $"'{template.RuntimeFamily}' has no live display template (status='{template.Status}') "
                + "-- rendering it would show a status the content is not ready to show");

        var args = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["value"] = FormatValue(unit, frozenValue),
        };
        if (elementVariant is not null) args["element"] = elementVariant;

        var rendered = DisplayTemplates.Render(template, frame, args);

        return new DisplayLine(
            Key: template.NameKey,
            Args: new Dictionary<string, string>(args) { ["__rendered"] = rendered },
            Unit: unit,
            SourceKind: sourceKind,
            GroupOrder: groupOrder,
            RollBar: BarFor(roll, qualityPerMille),
            ContextRead: contextRead,
            RollQualityPerMille: roll == RollPolicy.Fixed ? null : qualityPerMille);
    }

    /// <summary>Rule P by unit class — precision never exceeds the source's claimed accuracy.
    /// Frozen integer -> exact (GameUnits/Count/GameUnitsPerSecond); per-mille -> one decimal;
    /// ms -> formatted duration; sigmoid/status/reciprocal -> one decimal, approximate.</summary>
    static string FormatValue(UnitClass unit, long value) => unit switch
    {
        UnitClass.GameUnits or UnitClass.GameUnitsPerSecond or UnitClass.Count or UnitClass.LadderIndex => value.ToString(),
        UnitClass.PerMilleRatio => FormatPerMille(checked((int)value)),
        UnitClass.Milliseconds => FormatMilliseconds(checked((int)value)),
        UnitClass.Flag => value != 0 ? "" : throw new DisplayTemplateRejection("a Flag unit atom rendered a line but is not set"),
        UnitClass.SigmoidPoints or UnitClass.SigmoidMultiplierPoints or UnitClass.StatusPotencyPoints
            or UnitClass.ReciprocalPoints or UnitClass.AptitudePoints => value.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "unhandled unit class"),
    };
}
