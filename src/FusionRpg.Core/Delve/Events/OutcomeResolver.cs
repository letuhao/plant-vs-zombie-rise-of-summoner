using FusionRpg.Core.Actions.Seeding;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Delve.Roll;

namespace FusionRpg.Core.Delve.Events;

/// <summary>
/// `event-deck` D3.5 (spec-event-deck.md §5) — PARTIALLY BUILT: the severity band-shift and the
/// weighted `:outcome` draw over an already-resolved event's own outcome rows. Every dropBand/weight
/// vocabulary is a plain caller-supplied parameter, never hardcoded — `dropBand` is the ITEM registry's
/// own vocabulary (D3.1's own citation: "this module has no business owning" it), so this class reads
/// it exactly the way `EventCatalog.Load` already does. `TryInstantiate`, the five-way atom-kind
/// dispatch table and the forced-outcome-via-`supplyOverride` path are NOT built here — see the honest
/// gaps named on D3.5's own todo entry.
/// </summary>
public static class OutcomeResolver
{
    public const string GoodOrdinal = "good";
    public const string BadOrdinal = "bad";
    // mixed / nothing: no shift (spec §5, verbatim: "mixed/nothing stay").

    static int IndexOfBand(IReadOnlyList<string> dropBandOrder, string dropBand)
    {
        for (var i = 0; i < dropBandOrder.Count; i++)
            if (string.Equals(dropBandOrder[i], dropBand, StringComparison.Ordinal)) return i;
        throw new ArgumentException($"'{dropBand}' is not a member of the supplied dropBand order", nameof(dropBand));
    }

    /// <summary>
    /// Spec §5's "Severity" paragraph, verbatim: for severity tier `t`, a `good` outcome's `dropBand`
    /// moves `t` steps toward the END of <paramref name="dropBandOrder"/> (rarer/stronger — production
    /// order is `staple·frequent·occasional·seldom·exceptional`, but this function never hardcodes it);
    /// a `bad` outcome moves `t` steps toward the START (commoner/weaker); `mixed`/`nothing` never move.
    /// The index is clamped at both ends of the caller's own list — "bounded by the five-member enum,
    /// an ordinal rail" (spec, verbatim): a STRUCTURAL clamp on a closed enum's own ends, exempt from
    /// the no-silent-clamp rule the same way a `PredicateCompiler.MaxDepth` bound is, never a
    /// progression ceiling on a magnitude.
    /// </summary>
    public static string ShiftDropBand(string dropBand, string ordinal, int severityTier, IReadOnlyList<string> dropBandOrder)
    {
        if (dropBand is null) throw new ArgumentNullException(nameof(dropBand));
        if (ordinal is null) throw new ArgumentNullException(nameof(ordinal));
        if (dropBandOrder is null) throw new ArgumentNullException(nameof(dropBandOrder));
        if (dropBandOrder.Count == 0) throw new ArgumentException("dropBandOrder must not be empty", nameof(dropBandOrder));
        if (severityTier < 0) throw new ArgumentOutOfRangeException(nameof(severityTier), severityTier, "severity tier cannot be negative");

        var index = IndexOfBand(dropBandOrder, dropBand);
        var shifted = ordinal switch
        {
            GoodOrdinal => Math.Min(dropBandOrder.Count - 1, index + severityTier),
            BadOrdinal => Math.Max(0, index - severityTier),
            _ => index,
        };
        return dropBandOrder[shifted];
    }

    /// <summary>Spec §5: "Weight = `weightTable[band]`" — the item registry's own `dropBand.weightTable`
    /// (`bands.v1.json`), read as a plain caller-supplied map, never re-derived here.</summary>
    public static int WeightFor(string dropBand, IReadOnlyDictionary<string, int> weightTable)
    {
        if (dropBand is null) throw new ArgumentNullException(nameof(dropBand));
        if (weightTable is null) throw new ArgumentNullException(nameof(weightTable));
        return weightTable.TryGetValue(dropBand, out var weight)
            ? weight
            : throw new ArgumentException($"'{dropBand}' has no entry in the supplied dropBand weight table", nameof(dropBand));
    }

    /// <summary>
    /// The `:outcome` stream (spec §3's own table row: "which outcome | …:outcome | `Pick` over §5's
    /// shifted bands"). Each candidate's weight is its OWN <see cref="ShiftDropBand"/>-shifted band's
    /// weight — never the row's raw authored band — so severity actually changes the odds, not just the
    /// label. Reuses <see cref="EventDeckRefusal"/> (this module's own "the deck has nothing to give"
    /// exception, `EventDraw.cs`) for an exhausted/all-zero-weight outcome set, rather than minting a
    /// second exception for the identical fact.
    /// </summary>
    public static EventOutcomeRow PickOutcome(
        IReadOnlyList<EventOutcomeRow> outcomes, int severityTier, IReadOnlyList<string> dropBandOrder,
        IReadOnlyDictionary<string, int> weightTable, int row, int col, ulong seed)
    {
        if (outcomes is null) throw new ArgumentNullException(nameof(outcomes));
        if (outcomes.Count == 0) throw new ArgumentException("outcomes must not be empty", nameof(outcomes));

        var options = outcomes
            .Select(o => new WeightedOption<EventOutcomeRow>(o,
                WeightFor(ShiftDropBand(o.DropBand, o.Ordinal, severityTier, dropBandOrder), weightTable)))
            .ToList();

        var streamName = DelveStreams.Event(row, col) + ":outcome";
        var stream = SeededRng.DeriveStream(seed, streamName);
        var rollSeed = unchecked((long)stream.NextULong());

        try
        {
            return WeightedChoice.Pick(options, rollSeed, streamName);
        }
        catch (NoDrawableWeightedOptionException ex)
        {
            throw new EventDeckRefusal($"room ({row},{col}): {ex.Message}");
        }
    }
}
