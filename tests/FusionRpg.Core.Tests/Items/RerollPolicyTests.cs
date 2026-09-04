using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Items.Mutation;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `enhance-reroll` (item module 15) — the reroll half, the craft-pity guarantee, transfer, and the
/// op_kind namespace. Container shapes come from the REAL shipped rarity ladder
/// (`data/seed/rarity/ladder.v1.json`) rather than invented counts.
/// </summary>
public class RerollPolicyTests
{
    static EnhancementTuning Tuning() => EnhancePolicyTests.Tuning();

    static ContainerRow Container(int prefixRolls, int suffixRolls, int minTier, int maxTier, params string[] pool) =>
        new()
        {
            ContainerId = "item.test",
            Kind = ContainerKind.Item,
            Rarity = "almanac",
            PrefixRolls = prefixRolls,
            SuffixRolls = suffixRolls,
            MinTier = minTier,
            MaxTier = maxTier,
            Pool = pool.Select(p => new ContainerPoolRow(p, 100, "grp." + p)).ToList(),
        };

    // ---- §2, the platform correction ----------------------------------------------------------------

    [Fact]
    public void Anchoring_is_computed_per_budget_not_from_pool_rolls()
    {
        // ⛔ pool_rolls does not exist. K = (PrefixRolls − T_prefix) + (SuffixRolls − T_suffix), and
        // ANCHOR_MULT = 2^K — superlinear, unchanged in shape, summed over the two budgets.
        Assert.Equal(1L, RerollPolicy.AnchorMultiplier(new BudgetTargets(3, 2, 3, 2)));
        Assert.Equal(2L, RerollPolicy.AnchorMultiplier(new BudgetTargets(3, 2, 2, 2)));
        Assert.Equal(16L, RerollPolicy.AnchorMultiplier(new BudgetTargets(3, 2, 1, 0)));
    }

    [Fact]
    public void No_source_file_in_this_module_mentions_pool_rolls()
    {
        // The success criterion says "proven by grep AND by test" — this is the test half, run over
        // the real source directory so a later edit cannot quietly bring the stale algebra back.
        var dir = Path.Combine(MaterialCorpusTests.RepoRoot(), "src", "FusionRpg.Core", "Items", "Mutation");
        Assert.True(Directory.Exists(dir), dir);
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            // A comment is allowed to NAME the dead column — recording the correction is the point.
            // USING it is what this forbids, so the check runs over the non-comment lines only.
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                Assert.False(line.Contains("PoolRolls", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} uses PoolRolls — the column is gone, anchoring is per budget");
            }
        }
    }

    [Fact]
    public void A_reroll_with_no_target_is_a_paid_no_op_and_is_refused()
    {
        var refusal = RerollPolicy.ValidateTargets(new BudgetTargets(3, 2, 0, 0));
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, refusal.Reason);
        Assert.Contains("reroll.no-target", refusal.Detail);
    }

    [Fact]
    public void More_targets_than_the_budget_holds_is_refused_per_budget()
    {
        Assert.Contains("reroll.target-exceeds-budget", RerollPolicy.ValidateTargets(new BudgetTargets(3, 2, 4, 0)).Detail);
        Assert.Contains("reroll.target-exceeds-budget", RerollPolicy.ValidateTargets(new BudgetTargets(3, 2, 0, 3)).Detail);
        Assert.True(RerollPolicy.ValidateTargets(new BudgetTargets(3, 2, 3, 2)).IsOk);
    }

    [Fact]
    public void An_anchor_count_that_cannot_be_represented_throws_rather_than_saturating()
    {
        Assert.Throws<OverflowException>(() => RerollPolicy.AnchorMultiplier(new BudgetTargets(64, 0, 1, 0)));
    }

    [Fact]
    public void Rerolling_a_mixed_affix_redraws_into_both_budgets_or_is_refused()
    {
        // §2's Mixed hazard, DECIDED rather than discovered: until module 2 (resolution-order) lands
        // the real "one draw consumes both budgets" semantics, a reroll targeting a Mixed affix is
        // refused by name instead of building a second simplification on top of the first.
        var mixed = new DrawnAffix(3, "affix.mixed", "grp.mixed", AffixClass.Mixed, 4);
        var refusal = RerollPolicy.ValidateRerollable(new[] { mixed }, resolutionOrderLanded: false);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, refusal.Reason);
        Assert.Contains("reroll.mixed-affix-undefined", refusal.Detail);
        Assert.Contains("module 2", refusal.Detail);

        // And it stops being a refusal the day that module lands — the gate is a parameter, not a
        // permanent wall.
        Assert.True(RerollPolicy.ValidateRerollable(new[] { mixed }, resolutionOrderLanded: true).IsOk);
    }

    [Fact]
    public void A_partial_redraw_seeds_the_exclusion_set_with_retained_groups()
    {
        var drawn = new[]
        {
            new DrawnAffix(1, "affix.p1", "grp.a", AffixClass.Prefix, 3),
            new DrawnAffix(2, "affix.p2", "grp.b", AffixClass.Prefix, 4),
            new DrawnAffix(3, "affix.s1", "grp.c", AffixClass.Suffix, 5),
        };

        var prefixExcluded = RerollPolicy.RetainedGroups(drawn, new[] { 1 }, AffixClass.Prefix);
        Assert.Equal(new[] { "grp.b" }, prefixExcluded.OrderBy(g => g, StringComparer.Ordinal).ToArray());

        // The suffix budget's exclusion set is its OWN — a retained prefix never blocks a suffix draw.
        var suffixExcluded = RerollPolicy.RetainedGroups(drawn, new[] { 1 }, AffixClass.Suffix);
        Assert.Equal(new[] { "grp.c" }, suffixExcluded.ToArray());
    }

    // ---- the post-op invariant ----------------------------------------------------------------------

    [Fact]
    public void A_reforge_preserves_prefix_rolls_and_suffix_rolls_exactly()
    {
        var container = Container(2, 2, 3, 5, "affix.a", "affix.b", "affix.c", "affix.d");
        var good = new[]
        {
            new DrawnAffix(1, "affix.a", "grp.a", AffixClass.Prefix, 3),
            new DrawnAffix(2, "affix.b", "grp.b", AffixClass.Prefix, 4),
            new DrawnAffix(3, "affix.c", "grp.c", AffixClass.Suffix, 5),
            new DrawnAffix(4, "affix.d", "grp.d", AffixClass.Suffix, 5),
        };
        Assert.True(RerollPolicy.ValidatePostOp(container, good).IsOk);

        var short1 = good.Take(3).ToList();
        Assert.Contains("reroll.suffix-count-changed", RerollPolicy.ValidatePostOp(container, short1).Detail);
    }

    [Fact]
    public void A_rerolled_item_always_validates_as_freshly_instantiated()
    {
        // The "impossible item" failure, structurally: out-of-pool, out-of-window and duplicate-group
        // outcomes are all refused, so a reroll can only ever produce something the generator could
        // have dropped.
        var container = Container(1, 1, 3, 5, "affix.a", "affix.c");

        var outsidePool = new[]
        {
            new DrawnAffix(1, "affix.NOPE", "grp.x", AffixClass.Prefix, 3),
            new DrawnAffix(2, "affix.c", "grp.c", AffixClass.Suffix, 4),
        };
        Assert.Contains("reroll.affix-outside-pool", RerollPolicy.ValidatePostOp(container, outsidePool).Detail);

        var outsideWindow = new[]
        {
            new DrawnAffix(1, "affix.a", "grp.a", AffixClass.Prefix, 1),
            new DrawnAffix(2, "affix.c", "grp.c", AffixClass.Suffix, 4),
        };
        Assert.Contains("reroll.tier-outside-window", RerollPolicy.ValidatePostOp(container, outsideWindow).Detail);

        var groupCollision = new[]
        {
            new DrawnAffix(1, "affix.a", "grp.same", AffixClass.Prefix, 3),
            new DrawnAffix(2, "affix.c", "grp.same", AffixClass.Suffix, 4),
        };
        Assert.Contains("reroll.group-collision", RerollPolicy.ValidatePostOp(container, groupCollision).Detail);
    }

    // ---- the price ----------------------------------------------------------------------------------

    [Fact]
    public void Reroll_cost_mult_scales_with_affix_count_not_rung_alone()
    {
        // ⭐ reroll_cost_mult's decided shape. ssot-rarity.md §9.7's constraint, asserted where it
        // bites: adding an affix costs more than climbing a rung.
        var t = Tuning();
        var chaff1 = RerollPolicy.CostMultMilli(0, 1, t);
        var almanac1 = RerollPolicy.CostMultMilli(9, 1, t);
        var chaff5 = RerollPolicy.CostMultMilli(0, 5, t);

        Assert.Equal(1000, chaff1);
        Assert.True(almanac1 > chaff1, "a higher rung must cost more at the same affix count");
        Assert.True(chaff5 > almanac1,
            "five affixes at the bottom rung must out-cost one affix at the top — otherwise a low rung is " +
            "cheap to own and expensive to use, and §8.1's crafting-base mechanism inverts");
    }

    [Fact]
    public void The_rung_leg_is_the_integer_the_rarity_budget_row_stores()
    {
        var t = Tuning();
        for (var i = 0; i < RarityLadder.RungIds.Count; i++)
            Assert.Equal(1000 + 220 * i, RerollPolicy.RungLegMilli(i, t));

        // The rung INDEX, never rarity.ordinal — the 10× defect module 14 named by name.
        Assert.Throws<ArgumentOutOfRangeException>(() => RerollPolicy.RungLegMilli(60, t));
    }

    [Fact]
    public void Reroll_cost_mult_is_registered_with_a_decided_shape()
    {
        Assert.True(RarityBudgetKeys.IsRegistered("reroll_cost_mult"));
        var def = RarityBudgetKeys.All.Single(k => k.Key == "reroll_cost_mult");
        Assert.Equal("enhance-reroll (15)", def.ConsumerModule);
        // The two sockets keys stay pinned as unregistered exactly as hard as before.
        Assert.False(RarityBudgetKeys.IsRegistered("socket_min"));
        Assert.False(RarityBudgetKeys.IsRegistered("socket_max"));
    }

    // ---- §5, craft pity -----------------------------------------------------------------------------

    [Fact]
    public void The_pity_counter_guarantees_max_tier_at_the_threshold()
    {
        // D7 — the top tier is reachable by cost, on every affix group, with no luck floor.
        var t = Tuning();
        var decision = CraftPityCounter.TierFor(t.CraftPityThreshold, 3, 5,
            (_, _) => throw new InvalidOperationException("the weighted draw must not run at the threshold"), t);

        Assert.True(decision.Guaranteed);
        Assert.Equal(5, decision.Tier);
        Assert.Equal(0, decision.CounterAfter);
    }

    [Fact]
    public void Craft_pity_shifts_no_draw_weight()
    {
        // §5's resolution: the guarantee REPLACES the draw, it does not bias it — so ssot-rarity.md
        // §3.5's measured overlap invariant (2×10^5 rolls per rung, seed 20260822) still stands.
        // Proven by the draw delegate: below the threshold it is called and its answer is used
        // unchanged; at the threshold it is never called at all.
        var t = Tuning();
        var calls = 0;

        for (var counter = 0; counter < t.CraftPityThreshold; counter++)
        {
            var d = CraftPityCounter.TierFor(counter, 3, 5, (min, _) => { calls++; return min; }, t);
            Assert.False(d.Guaranteed);
            Assert.Equal(3, d.Tier); // exactly what the draw returned, unmodified
            Assert.Equal(counter + 1, d.CounterAfter);
        }

        Assert.Equal(t.CraftPityThreshold, calls);
        CraftPityCounter.TierFor(t.CraftPityThreshold, 3, 5, (_, _) => { calls++; return 3; }, t);
        Assert.Equal(t.CraftPityThreshold, calls); // unchanged: the guaranteed draw is not a draw
    }

    [Fact]
    public void Pity_resets_on_a_guaranteed_draw_and_on_a_natural_max_tier()
    {
        var t = Tuning();
        Assert.Equal(0, CraftPityCounter.TierFor(t.CraftPityThreshold, 3, 5, (_, _) => 3, t).CounterAfter);
        Assert.Equal(0, CraftPityCounter.TierFor(5, 3, 5, (_, max) => max, t).CounterAfter);
        Assert.Equal(6, CraftPityCounter.TierFor(5, 3, 5, (min, _) => min, t).CounterAfter);
    }

    [Fact]
    public void A_draw_that_leaves_the_container_window_is_a_loud_failure()
    {
        var t = Tuning();
        Assert.Throws<InvalidOperationException>(() => CraftPityCounter.TierFor(0, 3, 5, (_, _) => 1, t));
    }

    // ---- §6a, transfer -------------------------------------------------------------------------------

    [Fact]
    public void A_transfer_grants_the_lossy_ratio_and_empties_the_donor()
    {
        var t = Tuning();
        var outcome = TransferPolicy.Resolve(
            new TransferSide("role.blade", "humanoid", 200, 10),
            new TransferSide("role.blade", "humanoid", 200, 0), t);

        Assert.True(outcome.Allowed);
        Assert.Equal(7, outcome.GrantedLevels); // floor(10 × 700 / 1000)
        Assert.Equal(0, outcome.DonorLevelAfter);
    }

    [Fact]
    public void A_transfer_across_unequal_roles_or_outside_the_ilvl_window_is_refused_by_name()
    {
        var t = Tuning();
        var roleMismatch = TransferPolicy.Resolve(
            new TransferSide("role.blade", "humanoid", 200, 10),
            new TransferSide("role.crown", "humanoid", 200, 0), t);
        Assert.False(roleMismatch.Allowed);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, roleMismatch.Refusal.Reason);
        Assert.Contains("enhance.transfer-role-mismatch", roleMismatch.Refusal.Detail);

        var tooFar = TransferPolicy.Resolve(
            new TransferSide("role.blade", "humanoid", 200, 10),
            new TransferSide("role.blade", "humanoid", 209, 0), t);
        Assert.False(tooFar.Allowed);
        Assert.Contains("enhance.transfer-level-window", tooFar.Refusal.Detail);

        // ±8 exactly is inside the window.
        Assert.True(TransferPolicy.Resolve(
            new TransferSide("role.blade", "humanoid", 200, 10),
            new TransferSide("role.blade", "humanoid", 208, 0), t).Allowed);
    }

    [Fact]
    public void A_transfer_touching_a_hybrid_frame_is_refused_until_module_3_lands()
    {
        var t = Tuning();
        var outcome = TransferPolicy.Resolve(
            new TransferSide("role.blade", "hybrid", 200, 10),
            new TransferSide("role.blade", "humanoid", 200, 0), t);
        Assert.False(outcome.Allowed);
        Assert.Contains("enhance.transfer-hybrid-frame-undefined", outcome.Refusal.Detail);
        Assert.Contains("module 3", outcome.Refusal.Detail);
    }

    [Fact]
    public void A_transfer_is_clamped_to_the_recipients_own_item_level_cap()
    {
        // Not a progression ceiling: the same rule a direct enhancement of that item would hit.
        var t = Tuning();
        var outcome = TransferPolicy.Resolve(
            new TransferSide("role.blade", "humanoid", 8, 10),
            new TransferSide("role.blade", "humanoid", 8, 4), t);
        Assert.True(outcome.Allowed);
        Assert.Equal(2, outcome.GrantedLevels); // cap(8) = 6, recipient already at 4
    }

    // ---- the op_kind namespace ----------------------------------------------------------------------

    [Fact]
    public void Socket_imbue_exists_in_the_op_kind_namespace_before_module_16_needs_it()
    {
        // ⭐ D24's operation had no op_kind. Module 14 priced `imbue` and deliberately minted none;
        // inventing it in module 16 would fork the namespace, so it is added HERE.
        Assert.True(MutationOpKinds.TryParse("socket-imbue", out var kind));
        Assert.Equal(MutationOpKind.SocketImbue, kind);
        Assert.Contains("socket-imbue", MutationOpKinds.AllIds);

        // And the operation it performs is still module 14's priced verb, unchanged.
        Assert.True(CraftOperations.TryParse("imbue", out var op));
        Assert.Equal(CraftOperation.Imbue, op);
    }

    [Fact]
    public void The_op_kind_namespace_is_the_closed_ten()
    {
        Assert.Equal(10, MutationOpKinds.All.Count);
        Assert.Equal(
            new[]
            {
                "enhance", "reroll-value", "reroll-affix", "enhance-transfer-out", "enhance-transfer-in",
                "restore", "socket-add", "socket-insert", "socket-remove", "socket-imbue",
            },
            MutationOpKinds.AllIds.ToArray());
        Assert.False(MutationOpKinds.TryParse("reroll", out _)); // the corpus's old verb is not an op_kind
    }

    [Fact]
    public void Each_op_kind_has_its_own_named_rng_stream()
    {
        var streams = MutationOpKinds.All.Select(MutationOpKinds.StreamName).ToList();
        Assert.Equal(streams.Count, streams.Distinct(StringComparer.Ordinal).Count());
        Assert.All(streams, s => Assert.StartsWith("item.", s, StringComparison.Ordinal));
    }
}
