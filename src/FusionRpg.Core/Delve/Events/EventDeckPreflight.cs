using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Delve.Events;

/// <summary>
/// `event-deck` D3.9 (spec-event-deck.md §9, "Refusals and preflight") — PARTIALLY BUILT: the rules
/// that are pure over an already-loaded <see cref="EventCatalog"/> alone. Spec's own full rule list
/// names ten checks; four are buildable here, model-free, exactly matching the spec's own framing
/// ("model-free, run by the domain importer and the tests, refusing the domain with the row named").
/// The rest need data this program has not built yet — see each method's own doc comment, and D3.9's
/// own todo entry for the honest, named accounting of which rule is where and why.
/// </summary>
public static class EventDeckPreflight
{
    /// <summary>Spec §9, verbatim: "≥ 1 `good` and ≥ 1 `bad`-or-`mixed` per event." `nothing` neither
    /// satisfies nor disqualifies either side.</summary>
    public static IReadOnlyList<AtomRejection> CheckOutcomeMix(EventCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            var hasGood = row.Outcomes.Any(o => string.Equals(o.Ordinal, "good", StringComparison.Ordinal));
            var hasBadOrMixed = row.Outcomes.Any(o =>
                string.Equals(o.Ordinal, "bad", StringComparison.Ordinal) ||
                string.Equals(o.Ordinal, "mixed", StringComparison.Ordinal));

            if (!hasGood || !hasBadOrMixed)
                fails.Add(EventRules.Fail(EventRules.MissingRequiredOutcomeMix,
                    $"'{row.EventId}' needs >= 1 'good' and >= 1 'bad'-or-'mixed' outcome (has good={hasGood}, bad-or-mixed={hasBadOrMixed})"));
        }
        return fails;
    }

    /// <summary>
    /// Spec §9, verbatim: "`chainRef` acyclic, same kind." An unresolved `chainRef` (pointing at an id
    /// absent from the catalog) is a DIFFERENT rule — referential integrity, not named in this section
    /// of the spec — and is silently skipped here rather than invented.
    /// </summary>
    public static IReadOnlyList<AtomRejection> CheckChainRefs(EventCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            if (row.ChainRef is null) continue;

            var target = catalog.Resolve(row.ChainRef);
            if (target is not null && !string.Equals(target.Kind, row.Kind, StringComparison.Ordinal))
                fails.Add(EventRules.Fail(EventRules.ChainRefKindMismatch,
                    $"'{row.EventId}' (kind '{row.Kind}') chains to '{row.ChainRef}' (kind '{target.Kind}') -- chainRef must stay the same kind"));

            if (HasCycleFrom(catalog, row.EventId))
                fails.Add(EventRules.Fail(EventRules.ChainRefCycle, $"'{row.EventId}' is part of a chainRef cycle"));
        }
        return fails;
    }

    static bool HasCycleFrom(EventCatalog catalog, string startId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = startId;
        while (current is not null)
        {
            if (!seen.Add(current)) return true;
            current = catalog.Resolve(current)?.ChainRef;
        }
        return false;
    }

    /// <summary>Spec §9, verbatim: "`RoomKindIs boss` refused (*"no event may gate the boss"*)" —
    /// scans every event's own compiled-once eligibility tree for a <see cref="LeafId.RoomKindIs"/>
    /// leaf whose <paramref name="bossRoomKindOrdinal"/> matches, wherever it sits under `And`/`Or`/
    /// `Not`. <paramref name="bossRoomKindOrdinal"/> is a plain, caller-resolved ordinal — this module
    /// never imports `RoomKindCatalog` (D3.7's own "no second owner of a domain vocabulary" posture).</summary>
    public static IReadOnlyList<AtomRejection> CheckNoRoomKindIsBoss(EventCatalog catalog, int bossRoomKindOrdinal)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
            if (row.Eligibility is not null && ContainsRoomKindIsBoss(row.Eligibility, bossRoomKindOrdinal))
                fails.Add(EventRules.Fail(EventRules.RoomKindIsBossForbidden,
                    $"'{row.EventId}' gates eligibility on RoomKindIs(boss) -- no event may gate the boss"));
        return fails;
    }

    static bool ContainsRoomKindIsBoss(PredicateNode node, int bossOrdinal) => node switch
    {
        PredicateNode.And a => a.Children.Any(c => ContainsRoomKindIsBoss(c, bossOrdinal)),
        PredicateNode.Or o => o.Children.Any(c => ContainsRoomKindIsBoss(c, bossOrdinal)),
        PredicateNode.Not n => ContainsRoomKindIsBoss(n.Child, bossOrdinal),
        PredicateNode.Leaf l => l.Id == LeafId.RoomKindIs && l.Value == bossOrdinal,
        _ => false,
    };

    /// <summary>
    /// Spec §9, verbatim: "known status ids." `PredicateCompiler.ValidateLeaf` does NOT itself refuse
    /// an unknown one — an unresolvable `HasStatus` interns to bit -1 and evaluates permanently false
    /// (confirmed by reading `PredicateCompiler.cs`'s own `ValidateLeaf`), so this is a genuinely
    /// separate, event-deck-owned check over the SAME `statusBit` function the catalog itself compiled
    /// against, catching a typo that would otherwise ship as a silently-dead condition.
    /// </summary>
    public static IReadOnlyList<AtomRejection> CheckKnownStatusIds(EventCatalog catalog, Func<string, int> statusBit)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (statusBit is null) throw new ArgumentNullException(nameof(statusBit));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            if (row.Eligibility is null) continue;
            foreach (var badId in UnknownStatusIds(row.Eligibility, statusBit).Distinct(StringComparer.Ordinal))
                fails.Add(EventRules.Fail(EventRules.UnknownStatusId,
                    $"'{row.EventId}' HasStatus references unknown status id '{badId}'"));
        }
        return fails;
    }

    static IEnumerable<string> UnknownStatusIds(PredicateNode node, Func<string, int> statusBit) => node switch
    {
        PredicateNode.And a => a.Children.SelectMany(c => UnknownStatusIds(c, statusBit)),
        PredicateNode.Or o => o.Children.SelectMany(c => UnknownStatusIds(c, statusBit)),
        PredicateNode.Not n => UnknownStatusIds(n.Child, statusBit),
        PredicateNode.Leaf { Id: LeafId.HasStatus, Text: { } text } when statusBit(text) < 0 => new[] { text },
        _ => Array.Empty<string>(),
    };

    /// <summary>Every buildable rule, run together — never throws, one <see cref="AtomRejection"/> per
    /// violation found, matching <see cref="EventCatalog.Load"/>'s own "N bad rows, N rejections"
    /// shape. The six rules spec §9 also names (archetype/pool coverage, `supplyOverride` support,
    /// recent-cells headroom, `>= 1 encounter-event per rest archetype`, and container-content
    /// inspection) are not run here — see D3.9's own todo entry for exactly which upstream type each
    /// one is still missing.</summary>
    public static IReadOnlyList<AtomRejection> Run(EventCatalog catalog, int bossRoomKindOrdinal, Func<string, int> statusBit)
    {
        var fails = new List<AtomRejection>();
        fails.AddRange(CheckOutcomeMix(catalog));
        fails.AddRange(CheckChainRefs(catalog));
        fails.AddRange(CheckNoRoomKindIsBoss(catalog, bossRoomKindOrdinal));
        fails.AddRange(CheckKnownStatusIds(catalog, statusBit));
        return fails;
    }
}
