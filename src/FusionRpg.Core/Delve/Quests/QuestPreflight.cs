using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;

namespace FusionRpg.Core.Delve.Quests;

/// <summary>Named preflight rules this file raises (spec §8's own list). Three of the spec's own
/// bulleted checks — "a template not in the registry," "a `targetRef` mismatching `targetKind`," "a
/// `countBand` on a count-less template that is not `none`," "a tree failing `TryCompile` [or] using
/// a refused leaf" — are ALREADY enforced by `QuestCatalog.Load` itself (D4.9), so are deliberately
/// not re-checked here; this file owns only the checks `Load` structurally cannot make (a raw-tree
/// leaf scan, a cross-field floor/ceil compare, and a pool-wide count).</summary>
public static class QuestPreflightRules
{
    public const string RoomKindIsBossForbidden = "quest.room-kind-is-boss-forbidden";
    public const string FloorAboveCeil = "quest.floor-above-ceil";
    public const string TooFewNonSinkAnchors = "quest.too-few-non-sink-anchors";
}

/// <summary>
/// D4.13 (spec-delve-quests.md §8) — the buildable slice of preflight. `QuestPreflight.Run(corpus,
/// domains, layouts, tuning)` — the spec's own full signature, including the 256-seed satisfiability
/// sweep per `(domain, layout, raidMode, rung)` — needs `domain-catalog`'s own `DomainAnchor`/
/// `LayoutTemplate`/corpus types (D4.15+), which do not exist anywhere in the tree yet (confirmed via
/// grep — `domain-catalog` is a later, still-unbuilt module in this same Phase 4). This file instead
/// exposes the THREE row/pool-level checks that need no domain-catalog type at all, each independently
/// callable once a real pool exists; whichever task builds `domain-catalog`'s own importer composes
/// these together with the sweep into the spec's own full `Run` signature.
/// </summary>
public static class QuestPreflight
{
    /// <summary>A plain recursive walk over And/Or/Not/Leaf — no leaf-encoding knowledge of its own,
    /// so it never has to guess at `RoomKindCatalog`'s real ordinal scheme for "boss."</summary>
    public static bool TreeUsesLeaf(PredicateNode? node, Func<PredicateNode.Leaf, bool> matches)
    {
        if (matches is null) throw new ArgumentNullException(nameof(matches));
        return node switch
        {
            null => false,
            PredicateNode.Leaf leaf => matches(leaf),
            PredicateNode.Not not => TreeUsesLeaf(not.Child, matches),
            PredicateNode.And and => and.Children.Any(c => TreeUsesLeaf(c, matches)),
            PredicateNode.Or or => or.Children.Any(c => TreeUsesLeaf(c, matches)),
            _ => false,
        };
    }

    /// <summary>Spec §8, verbatim: "[a tree] naming `RoomKindIs boss`" refuses — "a boss gate by
    /// another door" (§3). <paramref name="isRoomKindBossLeaf"/> names which leaf means "boss" —
    /// a plain caller-supplied predicate, since `RoomKindIs`'s own compiled `Value` is a raw ordinal
    /// (`PredicateCompiler.cs`) this file has no business re-deriving.</summary>
    public static void CheckNoRoomKindIsBoss(string domainId, IReadOnlyList<QuestRow> pool, Func<PredicateNode.Leaf, bool> isRoomKindBossLeaf)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        foreach (var q in pool)
            if (TreeUsesLeaf(q.Predicate, isRoomKindBossLeaf))
                throw new QuestRefusal(domainId, q.QuestId, QuestPreflightRules.RoomKindIsBossForbidden,
                    "a quest predicate must never gate on RoomKindIs boss -- that is a door gate by another name");
    }

    /// <summary>Spec §8, verbatim: "`floorRung > ceilRung`" refuses. Rows whose `RewardBand` is
    /// itself unknown are skipped — `QuestCatalog.Load` already refused those, this is not a second
    /// check for the same defect.</summary>
    public static void CheckFloorNotAboveCeil(
        string domainId, IReadOnlyList<QuestRow> pool, IReadOnlyDictionary<string, RewardWindow> rewardBandsByMember, IReadOnlyList<RarityRung> ladder)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        if (rewardBandsByMember is null) throw new ArgumentNullException(nameof(rewardBandsByMember));
        if (ladder is null) throw new ArgumentNullException(nameof(ladder));

        foreach (var q in pool)
        {
            if (!rewardBandsByMember.TryGetValue(q.RewardBand, out var window)) continue;
            var floorOrdinal = RarityDraw.OrdinalOf(ladder, window.FloorRung);
            var ceilOrdinal = RarityDraw.OrdinalOf(ladder, window.CeilRung);
            if (floorOrdinal > ceilOrdinal)
                throw new QuestRefusal(domainId, q.QuestId, QuestPreflightRules.FloorAboveCeil,
                    $"rewardBand '{q.RewardBand}' has floorRung '{window.FloorRung}' (ordinal {floorOrdinal}) above ceilRung '{window.CeilRung}' (ordinal {ceilOrdinal})");
        }
    }

    /// <summary>Spec §8, verbatim: "fewer than `offeredAtEntry` non-sink anchors in a pool" refuses —
    /// D14's own floor requirement (§5): non-sink quests must be able to fill the offer on their own
    /// below `hard`, before any sink-avoidance quest can ever unlock.</summary>
    public static void CheckEnoughNonSinkAnchors(
        string domainId, IReadOnlyList<QuestRow> pool, IReadOnlyList<ObjectiveTemplateDef> objectiveTemplates, int offeredAtEntry)
    {
        if (pool is null) throw new ArgumentNullException(nameof(pool));
        if (objectiveTemplates is null) throw new ArgumentNullException(nameof(objectiveTemplates));

        var sinkAvoidanceByTemplate = objectiveTemplates.ToDictionary(t => t.ObjectiveTemplateId, t => t.SinkAvoidance, StringComparer.Ordinal);
        var nonSinkCount = pool.Count(q => !(sinkAvoidanceByTemplate.TryGetValue(q.TemplateId, out var s) && s));
        if (nonSinkCount < offeredAtEntry)
            throw new QuestRefusal(domainId, "(pool)", QuestPreflightRules.TooFewNonSinkAnchors,
                $"only {nonSinkCount} non-sink-avoidance anchor(s), need at least {offeredAtEntry}");
    }
}
