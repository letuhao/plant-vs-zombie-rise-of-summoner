using FusionRpg.Core.Delve.Quests;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Tests.Dungeon;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Quests;

/// <summary>D4.13 (spec-delve-quests.md §8) — `QuestPreflight`'s three buildable checks and
/// `QuestCoverage.WithinRegressionBand`. The full 256-seed satisfiability sweep and the live
/// autopilot-completion measurement both need `domain-catalog` (D4.15+, genuinely unbuilt) — "preflight
/// refuses a domain whose quests cannot complete" and "the band holds over the sweep" are tested here
/// at the scope this task actually built (the non-sink-anchor-count check; the band predicate's own
/// correctness), named explicitly rather than silently claimed at the full sweep's scope.</summary>
public class QuestPreflightTests
{
    static RarityRung Rung(string id, int ordinal) => new(id, ordinal, 0, 0, 0, 0, 100);
    static readonly IReadOnlyList<RarityRung> Ladder = new[] { Rung("staple", 10), Rung("frequent", 20), Rung("occasional", 30) };

    static readonly IReadOnlyDictionary<string, RewardWindow> RewardBands = new Dictionary<string, RewardWindow>(StringComparer.Ordinal)
    {
        ["modest"] = new RewardWindow("staple", "occasional"),
        ["inverted"] = new RewardWindow("occasional", "staple"), // floor above ceil -- deliberately malformed
    };

    static QuestRow Row(string id, string template, string rewardBand = "modest", PredicateNode? predicate = null) =>
        new(id, template, null, null, rewardBand, "delve", predicate);

    // ---- TreeUsesLeaf ----

    [Fact]
    public void TreeUsesLeaf_finds_a_bare_matching_leaf()
    {
        var leaf = new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: 7);
        Assert.True(QuestPreflight.TreeUsesLeaf(leaf, l => l.Id == LeafId.RoomKindIs && l.Value == 7));
    }

    [Fact]
    public void TreeUsesLeaf_returns_false_for_a_null_tree()
    {
        Assert.False(QuestPreflight.TreeUsesLeaf(null, _ => true));
    }

    [Fact]
    public void TreeUsesLeaf_recurses_through_And_Or_and_Not()
    {
        var target = new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: 7);
        var other = new PredicateNode.Leaf(LeafId.HpBelowMilli, Subject.Target, Value: 300);
        var tree = new PredicateNode.And(new PredicateNode[]
        {
            other,
            new PredicateNode.Not(new PredicateNode.Or(new PredicateNode[] { other, target })),
        });
        bool IsBoss(PredicateNode.Leaf l) => l.Id == LeafId.RoomKindIs && l.Value == 7;
        Assert.True(QuestPreflight.TreeUsesLeaf(tree, IsBoss));
        Assert.False(QuestPreflight.TreeUsesLeaf(other, IsBoss));
    }

    // ---- CheckNoRoomKindIsBoss ----

    static bool IsBossLeaf(PredicateNode.Leaf l) => l.Id == LeafId.RoomKindIs && l.Value == 99;

    [Fact]
    public void CheckNoRoomKindIsBoss_refuses_a_quest_gating_on_the_boss_room_kind()
    {
        var quest = Row("q1", "kill-boss", predicate: new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: 99));
        var ex = Assert.Throws<QuestRefusal>(() => QuestPreflight.CheckNoRoomKindIsBoss("domain.forest", new[] { quest }, IsBossLeaf));
        Assert.Equal(QuestPreflightRules.RoomKindIsBossForbidden, ex.Rule);
        Assert.Equal("q1", ex.QuestId);
        Assert.Equal("domain.forest", ex.DomainId);
    }

    [Fact]
    public void CheckNoRoomKindIsBoss_passes_a_quest_with_no_predicate_at_all()
    {
        var quest = Row("q1", "kill-boss");
        QuestPreflight.CheckNoRoomKindIsBoss("domain.forest", new[] { quest }, IsBossLeaf); // does not throw
    }

    [Fact]
    public void CheckNoRoomKindIsBoss_passes_a_predicate_that_never_names_the_boss_kind()
    {
        var quest = Row("q1", "kill-boss", predicate: new PredicateNode.Leaf(LeafId.HpBelowMilli, Subject.Target, Value: 300));
        QuestPreflight.CheckNoRoomKindIsBoss("domain.forest", new[] { quest }, IsBossLeaf); // does not throw
    }

    // ---- CheckFloorNotAboveCeil ----

    [Fact]
    public void CheckFloorNotAboveCeil_refuses_an_inverted_window()
    {
        var quest = Row("q1", "kill-boss", rewardBand: "inverted");
        var ex = Assert.Throws<QuestRefusal>(() => QuestPreflight.CheckFloorNotAboveCeil("domain.forest", new[] { quest }, RewardBands, Ladder));
        Assert.Equal(QuestPreflightRules.FloorAboveCeil, ex.Rule);
    }

    [Fact]
    public void CheckFloorNotAboveCeil_passes_a_well_formed_window()
    {
        var quest = Row("q1", "kill-boss", rewardBand: "modest");
        QuestPreflight.CheckFloorNotAboveCeil("domain.forest", new[] { quest }, RewardBands, Ladder); // does not throw
    }

    [Fact]
    public void CheckFloorNotAboveCeil_skips_a_row_whose_rewardBand_QuestCatalog_would_already_have_refused()
    {
        var quest = Row("q1", "kill-boss", rewardBand: "not-a-real-band");
        QuestPreflight.CheckFloorNotAboveCeil("domain.forest", new[] { quest }, RewardBands, Ladder); // does not throw -- not this check's job
    }

    // ---- CheckEnoughNonSinkAnchors / "preflight refuses a domain whose quests cannot complete" ----

    [Fact]
    public void CheckEnoughNonSinkAnchors_refuses_a_pool_that_can_never_fill_the_offer()
    {
        var templates = ObjectiveTemplateCatalog.All;
        var pool = new[] { Row("q1", "finish-under-hunger"), Row("q2", "survive-no-downed") }; // both sink-avoidance
        var ex = Assert.Throws<QuestRefusal>(() => QuestPreflight.CheckEnoughNonSinkAnchors("domain.forest", pool, templates, offeredAtEntry: 2));
        Assert.Equal(QuestPreflightRules.TooFewNonSinkAnchors, ex.Rule);
    }

    [Fact]
    public void CheckEnoughNonSinkAnchors_passes_a_pool_with_enough_non_sink_anchors()
    {
        var templates = ObjectiveTemplateCatalog.All;
        var pool = new[] { Row("q1", "kill-boss"), Row("q2", "bring-demon-home-alive"), Row("q3", "finish-under-hunger") };
        QuestPreflight.CheckEnoughNonSinkAnchors("domain.forest", pool, templates, offeredAtEntry: 2); // does not throw
    }

    // ---- QuestCoverage.WithinRegressionBand / "the band holds over the sweep" ----

    [Theory]
    [InlineData(300, 300, 900, true)]  // exactly at the floor -- inclusive
    [InlineData(900, 300, 900, true)]  // exactly at the ceiling -- inclusive
    [InlineData(600, 300, 900, true)]
    [InlineData(299, 300, 900, false)] // below -- a template that quietly got harder
    [InlineData(901, 300, 900, false)] // above -- content drift, still flagged, "never a target"
    public void WithinRegressionBand_matches_the_bands_own_inclusive_bounds(long completionMilli, long min, long max, bool expected)
    {
        Assert.Equal(expected, QuestCoverage.WithinRegressionBand(completionMilli, min, max));
    }

    [Fact]
    public void WithinRegressionBand_throws_on_an_inverted_band()
    {
        Assert.Throws<ArgumentException>(() => QuestCoverage.WithinRegressionBand(500, 900, 300));
    }

    [Fact]
    public void The_real_shipped_autopilotCompletionBand_is_a_real_two_sided_range_not_a_collapsed_target()
    {
        // Reads the real, shipped dungeon.v1.json rather than a hand-copied literal (a fixture copy
        // could drift from what ships) -- pinned so a future edit that collapses the band to a single
        // value (making it a de facto target) is visible here, the acceptance line's own "never a
        // target" as a checked fact against real content, not an assumption.
        var registries = DungeonRegistryLoader.LoadAll(DungeonTestFiles.RegistryDir());
        var tuning = DungeonTuningLoader.Parse(File.ReadAllText(DungeonTestFiles.DungeonTuningPath()), registries);
        Assert.True(tuning.QuestsAutopilotCompletionBandMinMilli < tuning.QuestsAutopilotCompletionBandMaxMilli);
        Assert.True(QuestCoverage.WithinRegressionBand(
            (tuning.QuestsAutopilotCompletionBandMinMilli + tuning.QuestsAutopilotCompletionBandMaxMilli) / 2,
            tuning.QuestsAutopilotCompletionBandMinMilli, tuning.QuestsAutopilotCompletionBandMaxMilli));
    }
}
