using FusionRpg.Core.Delve.Events;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Events;

/// <summary>D3.9 (spec-event-deck.md §9, "Refusals and preflight"): the four rules buildable today,
/// pure over an already-loaded `EventCatalog`.</summary>
public class EventDeckPreflightTests
{
    const int BossOrdinal = 10;
    static readonly Func<string, int> StatusBit = id => id switch { "chilled" => 1, "burning" => 2, _ => -1 };

    static EventCatalog CatalogOf(params EventRow[] rows)
    {
        var eventKinds = new[] { "curio", "encounter-event", "shrine", "trap", "bargain", "story" };
        var repeatScopes = new[] { "per-delve", "per-domain", "once-per-player" };
        var ordinals = new[] { "good", "mixed", "bad", "nothing" };
        var dropBands = new[] { "staple", "frequent", "occasional", "seldom", "exceptional" };
        var overrideTags = new[] { "herbs", "key", "holy", "bait", "watch" };
        var result = EventCatalog.Load(rows, eventKinds, repeatScopes, ordinals, dropBands, overrideTags, StatusBit);
        Assert.Empty(result.Rejections);
        return result.Catalog;
    }

    static EventOutcomeRow Outcome(string ordinal) => new(ordinal, "staple", "none", Array.Empty<EventEffectRef>());

    static EventRow Row(string id, string kind = "curio", PredicateNode? eligibility = null,
        string? chainRef = null, params string[] outcomeOrdinals) => new(
        EventId: id, Kind: kind, Theme: null, ClimateAffinity: null, RepeatScope: "per-delve",
        Eligibility: eligibility,
        Outcomes: outcomeOrdinals.Length > 0
            ? outcomeOrdinals.Select(Outcome).ToArray()
            : new[] { Outcome("good"), Outcome("bad") },
        SupplyOverride: null, ChainRef: chainRef);

    static string DetailOf(AtomRejection r) => r.Detail;

    // ---- CheckOutcomeMix ----

    [Fact]
    public void CheckOutcomeMix_null_catalog_throws()
    {
        Assert.Throws<ArgumentNullException>(() => EventDeckPreflight.CheckOutcomeMix(null!));
    }

    [Theory]
    [InlineData("good", "bad")]
    [InlineData("good", "mixed")]
    [InlineData("good", "bad", "mixed")]
    public void A_good_plus_bad_or_mixed_event_passes(params string[] ordinals)
    {
        var catalog = CatalogOf(Row("e1", outcomeOrdinals: ordinals));
        Assert.Empty(EventDeckPreflight.CheckOutcomeMix(catalog));
    }

    [Fact]
    public void A_nothing_outcome_on_a_story_event_does_not_interfere_with_the_good_bad_check()
    {
        // "nothing" is legal only on kind:story (EventCatalog.Load's own D3.1 rule), and story
        // separately requires its own chainRef -- both satisfied here so this fixture actually loads.
        var catalog = CatalogOf(Row("e1", "story", chainRef: "e1", outcomeOrdinals: new[] { "good", "bad", "nothing" }));
        Assert.Empty(EventDeckPreflight.CheckOutcomeMix(catalog));
    }

    [Fact]
    public void An_event_with_only_good_outcomes_fails()
    {
        var catalog = CatalogOf(Row("e1", outcomeOrdinals: new[] { "good", "good" }));
        var fails = EventDeckPreflight.CheckOutcomeMix(catalog);
        var f = Assert.Single(fails);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, f.Reason);
        Assert.Contains(EventRules.MissingRequiredOutcomeMix, DetailOf(f));
        Assert.Contains("e1", DetailOf(f));
    }

    [Fact]
    public void An_event_with_no_good_outcome_fails()
    {
        var catalog = CatalogOf(Row("e1", outcomeOrdinals: new[] { "bad", "mixed" }));
        Assert.Single(EventDeckPreflight.CheckOutcomeMix(catalog));
    }

    [Fact]
    public void Multiple_bad_events_each_produce_their_own_named_rejection()
    {
        var catalog = CatalogOf(
            Row("bad1", outcomeOrdinals: new[] { "good", "good" }),
            Row("bad2", outcomeOrdinals: new[] { "bad", "mixed" }),
            Row("ok1", outcomeOrdinals: new[] { "good", "bad" }));

        var fails = EventDeckPreflight.CheckOutcomeMix(catalog);
        Assert.Equal(2, fails.Count);
        Assert.Contains(fails, f => DetailOf(f).Contains("bad1"));
        Assert.Contains(fails, f => DetailOf(f).Contains("bad2"));
    }

    // ---- CheckChainRefs ----

    [Fact]
    public void CheckChainRefs_null_catalog_throws()
    {
        Assert.Throws<ArgumentNullException>(() => EventDeckPreflight.CheckChainRefs(null!));
    }

    [Fact]
    public void No_chainRef_at_all_passes()
    {
        var catalog = CatalogOf(Row("e1"));
        Assert.Empty(EventDeckPreflight.CheckChainRefs(catalog));
    }

    [Fact]
    public void A_same_kind_non_cyclic_chain_passes()
    {
        // "curio" throughout, not "story": D3.1's own separate rule requires every story-kind event to
        // carry a chainRef, so a story chain can never terminate on a plain story row with none -- an
        // unrelated concern this test isn't exercising. "curio" isolates CheckChainRefs' own kind-match
        // + cycle-detection mechanic from that already-tested rule.
        var catalog = CatalogOf(Row("a", "curio", chainRef: "b"), Row("b", "curio"));
        Assert.Empty(EventDeckPreflight.CheckChainRefs(catalog));
    }

    [Fact]
    public void A_chain_to_a_different_kind_fails_with_kind_mismatch()
    {
        var catalog = CatalogOf(Row("a", "story", chainRef: "b"), Row("b", "curio"));
        var fails = EventDeckPreflight.CheckChainRefs(catalog);
        Assert.Contains(fails, f => DetailOf(f).Contains(EventRules.ChainRefKindMismatch));
    }

    [Fact]
    public void An_unresolved_chainRef_is_silently_skipped_not_a_rejection()
    {
        var catalog = CatalogOf(Row("a", "story", chainRef: "does-not-exist"));
        Assert.Empty(EventDeckPreflight.CheckChainRefs(catalog));
    }

    [Fact]
    public void A_two_cycle_flags_both_participating_events()
    {
        var catalog = CatalogOf(Row("a", "story", chainRef: "b"), Row("b", "story", chainRef: "a"));
        var fails = EventDeckPreflight.CheckChainRefs(catalog);
        var cycleFails = fails.Where(f => DetailOf(f).Contains(EventRules.ChainRefCycle)).ToList();
        Assert.Equal(2, cycleFails.Count);
        Assert.Contains(cycleFails, f => DetailOf(f).Contains("'a'"));
        Assert.Contains(cycleFails, f => DetailOf(f).Contains("'b'"));
    }

    [Fact]
    public void A_three_link_chain_with_no_cycle_passes()
    {
        var catalog = CatalogOf(
            Row("a", "curio", chainRef: "b"), Row("b", "curio", chainRef: "c"), Row("c", "curio"));
        Assert.Empty(EventDeckPreflight.CheckChainRefs(catalog));
    }

    // ---- CheckNoRoomKindIsBoss ----

    [Fact]
    public void CheckNoRoomKindIsBoss_null_catalog_throws()
    {
        Assert.Throws<ArgumentNullException>(() => EventDeckPreflight.CheckNoRoomKindIsBoss(null!, BossOrdinal));
    }

    [Fact]
    public void No_eligibility_tree_at_all_passes()
    {
        var catalog = CatalogOf(Row("e1"));
        Assert.Empty(EventDeckPreflight.CheckNoRoomKindIsBoss(catalog, BossOrdinal));
    }

    [Fact]
    public void A_top_level_RoomKindIs_boss_leaf_fails()
    {
        var eligibility = new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: BossOrdinal);
        var catalog = CatalogOf(Row("e1", eligibility: eligibility));
        var fails = EventDeckPreflight.CheckNoRoomKindIsBoss(catalog, BossOrdinal);
        var f = Assert.Single(fails);
        Assert.Contains(EventRules.RoomKindIsBossForbidden, DetailOf(f));
    }

    [Fact]
    public void A_RoomKindIs_leaf_for_a_different_kind_passes()
    {
        var eligibility = new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: 3);
        var catalog = CatalogOf(Row("e1", eligibility: eligibility));
        Assert.Empty(EventDeckPreflight.CheckNoRoomKindIsBoss(catalog, BossOrdinal));
    }

    [Fact]
    public void A_RoomKindIs_boss_leaf_buried_under_And_Or_Not_is_still_caught()
    {
        var buried = new PredicateNode.And(new PredicateNode[]
        {
            new PredicateNode.Leaf(LeafId.HpBelowMilli, Subject.Self, Value: 500),
            new PredicateNode.Or(new PredicateNode[]
            {
                new PredicateNode.Leaf(LeafId.BandIs, Subject.Target, Value: 1),
                new PredicateNode.Not(new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: BossOrdinal)),
            }),
        });
        var catalog = CatalogOf(Row("e1", eligibility: buried));
        Assert.Single(EventDeckPreflight.CheckNoRoomKindIsBoss(catalog, BossOrdinal));
    }

    // ---- CheckKnownStatusIds ----

    [Fact]
    public void CheckKnownStatusIds_null_arguments_throw()
    {
        var catalog = CatalogOf(Row("e1"));
        Assert.Throws<ArgumentNullException>(() => EventDeckPreflight.CheckKnownStatusIds(null!, StatusBit));
        Assert.Throws<ArgumentNullException>(() => EventDeckPreflight.CheckKnownStatusIds(catalog, null!));
    }

    [Fact]
    public void A_known_status_id_passes()
    {
        var eligibility = new PredicateNode.Leaf(LeafId.HasStatus, Subject.Self, Text: "chilled");
        var catalog = CatalogOf(Row("e1", eligibility: eligibility));
        Assert.Empty(EventDeckPreflight.CheckKnownStatusIds(catalog, StatusBit));
    }

    [Fact]
    public void An_unknown_status_id_fails_naming_the_id()
    {
        var eligibility = new PredicateNode.Leaf(LeafId.HasStatus, Subject.Self, Text: "typo-status");
        var catalog = CatalogOf(Row("e1", eligibility: eligibility));
        var fails = EventDeckPreflight.CheckKnownStatusIds(catalog, StatusBit);
        var f = Assert.Single(fails);
        Assert.Contains("typo-status", DetailOf(f));
        Assert.Contains(EventRules.UnknownStatusId, DetailOf(f));
    }

    [Fact]
    public void An_unknown_status_id_buried_under_And_Or_Not_is_still_caught()
    {
        var buried = new PredicateNode.Not(new PredicateNode.And(new PredicateNode[]
        {
            new PredicateNode.Leaf(LeafId.HpBelowMilli, Subject.Self, Value: 500),
            new PredicateNode.Leaf(LeafId.HasStatus, Subject.Self, Text: "typo-status"),
        }));
        var catalog = CatalogOf(Row("e1", eligibility: buried));
        Assert.Single(EventDeckPreflight.CheckKnownStatusIds(catalog, StatusBit));
    }

    [Fact]
    public void The_same_unknown_status_id_appearing_twice_reports_once()
    {
        var tree = new PredicateNode.Or(new PredicateNode[]
        {
            new PredicateNode.Leaf(LeafId.HasStatus, Subject.Self, Text: "typo-status"),
            new PredicateNode.Leaf(LeafId.HasStatus, Subject.Target, Text: "typo-status"),
        });
        var catalog = CatalogOf(Row("e1", eligibility: tree));
        Assert.Single(EventDeckPreflight.CheckKnownStatusIds(catalog, StatusBit));
    }

    // ---- Run: everything together ----

    [Fact]
    public void Run_combines_every_rule_and_reports_every_violation()
    {
        var bossGate = new PredicateNode.Leaf(LeafId.RoomKindIs, Subject.Target, Value: BossOrdinal);
        var catalog = CatalogOf(
            Row("onlyGood", outcomeOrdinals: new[] { "good", "good" }),
            Row("gatesBoss", eligibility: bossGate),
            Row("clean", outcomeOrdinals: new[] { "good", "bad" }));

        var fails = EventDeckPreflight.Run(catalog, BossOrdinal, StatusBit);
        Assert.Contains(fails, f => DetailOf(f).Contains("onlyGood") && DetailOf(f).Contains(EventRules.MissingRequiredOutcomeMix));
        Assert.Contains(fails, f => DetailOf(f).Contains("gatesBoss") && DetailOf(f).Contains(EventRules.RoomKindIsBossForbidden));
        Assert.DoesNotContain(fails, f => DetailOf(f).Contains("clean"));
    }
}
