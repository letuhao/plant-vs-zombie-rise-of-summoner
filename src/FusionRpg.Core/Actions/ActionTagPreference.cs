namespace FusionRpg.Core.Actions;

/// <summary>
/// T34's preference key (spec-action-selection.md §3): "tag preference (`offensive` before
/// `utility`), then `action_id` ordinal. Never catalog or dictionary order." The spec names one
/// example pair, not a full order — the rest of the ranking below is a decided-now placeholder (same
/// posture as T20's discard-tax coefficient): the RULE is fixed (full order, offensive first, no two
/// tags tie), the exact ranking is content to rebalance once a real stub AI plays real matches.
/// <b>Explicitly not <see cref="FusionRpg.Core.Battle.Timeline.ActionEnvelope.PriorityBand"/></b> —
/// that field is a scheduling override baked into the event queue's own sort key.
/// </summary>
public static class ActionTagPreference
{
    static readonly IReadOnlyDictionary<ActionTag, int> Rank = new Dictionary<ActionTag, int>
    {
        [ActionTag.Offensive] = 0,
        [ActionTag.Debuff] = 1,
        [ActionTag.Buff] = 2,
        [ActionTag.Heal] = 3,
        [ActionTag.Summon] = 4,
        [ActionTag.Defensive] = 5,
        [ActionTag.Movement] = 6,
        [ActionTag.Utility] = 7,
    };

    /// <summary>An action's own rank is its BEST (lowest) tag rank — an offensive-tagged heal is
    /// still preferred over a pure utility action. An untagged action ranks last of all, tied only by
    /// <c>action_id</c>.</summary>
    public static int RankOf(CompiledAction action)
    {
        var best = int.MaxValue;
        foreach (var tag in action.Tags)
            if (Rank.TryGetValue(tag, out var r) && r < best)
                best = r;
        return best;
    }

    /// <summary>Total order: tag rank, then <c>action_id</c> ordinal — never catalog/dictionary
    /// iteration order.</summary>
    public static int Compare(CompiledAction a, CompiledAction b)
    {
        var byRank = RankOf(a).CompareTo(RankOf(b));
        return byRank != 0 ? byRank : string.CompareOrdinal(a.ActionId, b.ActionId);
    }
}
