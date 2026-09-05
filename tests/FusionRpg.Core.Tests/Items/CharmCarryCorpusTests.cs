using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Thresholds;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `charm-carry` (item module 22) against the REAL shipped corpus — `data/seed/items/charms/**`, the
/// 60 authored charms plus the 10 resonance containers module 12 already validated. Nothing here is
/// synthetic: every number is measured off the files on disk, so a corpus edit that breaks a carry rule
/// is a red test rather than a discovery in play.
/// </summary>
public class CharmCarryCorpusTests
{
    // ⭐ Module 12's OWN corpus readers, reused rather than copied. Its test class already parses the
    // real charm files exactly the way the shipped importer does, and a second reader here would be the
    // forked copy this program keeps refusing — including the risk that the two drift on which file
    // counts as the resonance population.
    static IReadOnlyList<CharmDef> Charms() => ThresholdGrantCorpusTests.Charms();
    static IReadOnlyList<CharmResonanceRow> Resonance() => ThresholdGrantCorpusTests.Resonances();
    static CharmAttunementTuning Tuning() => CharmCarryTests.Tuning();

    [Fact]
    public void Every_shipped_charm_passes_the_carry_gates_import_rules()
    {
        // The whole point of an import check: it runs over the real population, not over a fixture.
        var t = Tuning();
        var fails = Charms().SelectMany(c => CharmPouchGate.ValidateForCarry(c, t)).ToList();
        Assert.Empty(fails);
    }

    [Fact]
    public void Every_shipped_ap_cost_is_inside_the_authored_domain_and_every_domain_value_is_used()
    {
        // Both directions. A domain value nothing uses is a rung the packing decision never sees; a
        // cost outside it is a size the budget cannot price.
        var t = Tuning();
        var costs = Charms().Select(c => c.ApCost).ToList();

        Assert.All(costs, c => Assert.Contains(c, t.ApCostDomain));
        Assert.Equal(t.ApCostDomain.OrderBy(v => v), costs.Distinct().OrderBy(v => v));

        // The measured shape, 2026-09-05: 1x21, 2x21, 3x11, 5x7.
        Assert.Equal(21, costs.Count(c => c == 1));
        Assert.Equal(21, costs.Count(c => c == 2));
        Assert.Equal(11, costs.Count(c => c == 3));
        Assert.Equal(7, costs.Count(c => c == 5));
    }

    [Fact]
    public void No_shipped_charm_costs_more_than_the_starting_capacity()
    {
        // §6.1's "a signet is 5 of 6". A charm larger than the first rung would be content the player
        // owns and can never carry, which is the exact shape of a dead row.
        var t = Tuning();
        Assert.All(Charms(), c => Assert.True(c.ApCost <= t.StartingCapacityAp,
            $"'{c.ContainerId}' costs {c.ApCost} AP against a starting capacity of {t.StartingCapacityAp}"));
    }

    [Fact]
    public void Exactly_the_seven_signets_are_unique_carry_so_the_tighter_copy_cap_is_class_shaped()
    {
        var t = Tuning();
        var charms = Charms();

        Assert.Equal(7, charms.Count(c => c.Class == CharmClass.Signet));
        Assert.Equal(7, charms.Count(c => c.UniqueCarry));
        Assert.All(charms, c => Assert.Equal(c.Class == CharmClass.Signet, c.UniqueCarry));

        Assert.All(charms, c => Assert.Equal(
            c.Class == CharmClass.Signet ? t.UniqueCarryCopyCap : t.CopyCapPerContainer,
            t.CopyCapFor(c.UniqueCarry)));
    }

    [Fact]
    public void Every_shipped_charms_axis_has_a_resonance_ladder_and_no_ladder_is_orphaned()
    {
        // ssot-charms §3.5: resonance is OPEN — "any charm tagged with that axis". An axis with charms
        // and no ladder is a lean that never pays; a ladder with no charms is a tier nobody can reach.
        var charmAxes = Charms().Select(c => c.Axis).Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal).ToList();
        var ladderAxes = CharmResonance.AxesOf(Resonance()).OrderBy(a => a, StringComparer.Ordinal).ToList();

        Assert.Equal(charmAxes, ladderAxes);
        Assert.Equal(5, charmAxes.Count);   // the five power categories, §3.5
    }

    [Fact]
    public void Every_axis_has_enough_shipped_charms_to_reach_its_top_resonance_tier()
    {
        // The 3-tier is unreachable on an axis with two charms — and worse, invisibly so.
        var perAxis = Charms().GroupBy(c => c.Axis, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var row in Resonance())
            Assert.True(perAxis.TryGetValue(row.Axis, out var n) && n >= row.CountRequired,
                $"'{row.ContainerId}' needs {row.CountRequired} charms of axis '{row.Axis}' and the " +
                $"corpus ships {(perAxis.TryGetValue(row.Axis, out var m) ? m : 0)}");
    }

    [Fact]
    public void No_axis_can_be_starved_by_the_axis_cap_and_economy_is_the_deepest_pool()
    {
        // Measured, and it is the one distribution fact worth pinning: economy ships 20 charms and each
        // of the other four ships 10. Not a defect — §3.5's axes are open categories, not quotas — but
        // it is a real content asymmetry a balance pass should be able to see move.
        var perAxis = Charms().GroupBy(c => c.Axis, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(20, perAxis["economy"]);
        foreach (var axis in new[] { "offense", "survivability", "control", "utility" })
            Assert.Equal(10, perAxis[axis]);

        // Every axis still clears the cap comfortably, so the cap binds on the PLAYER's packing rather
        // than on what the corpus can supply.
        Assert.All(perAxis.Values, n => Assert.True(n > Tuning().AxisCapPerSnapshot));
    }

    [Fact]
    public void A_pouch_built_from_real_charms_at_the_starting_capacity_is_admitted()
    {
        // End to end on shipped data: pick the cheapest real charms of three different axes, price them
        // against the real starting capacity, and run the real gate.
        var t = Tuning();
        var picks = Charms()
            .GroupBy(c => c.Axis, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.OrderBy(c => c.ApCost).ThenBy(c => c.ContainerId, StringComparer.Ordinal).First())
            .Take(3)
            .Select((c, i) => new AttunedCharm($"inst-{i}", c.ContainerId, c.Axis, c.ApCost, c.UniqueCarry))
            .ToList();

        Assert.True(CharmPouchGate.TotalAp(picks) <= t.StartingCapacityAp);
        Assert.Empty(CharmPouchGate.Explain(picks, t.StartingCapacityAp, t,
            attunableContainerIds: Charms().Select(c => c.ContainerId).ToHashSet(StringComparer.Ordinal)));
    }

    [Fact]
    public void A_real_signet_plus_a_real_second_charm_is_the_five_of_six_squeeze_section_6_1_describes()
    {
        // "Five AP is 62% of a starting player's entire capacity, so a signet is a build, not a stat
        // stick." Driven off a REAL signet, so a corpus that re-priced signets breaks this.
        var t = Tuning();
        var signet = Charms().First(c => c.Class == CharmClass.Signet);
        var twoAp = Charms().First(c => c.ApCost == 2);

        var fits = new[]
        {
            new AttunedCharm("a", signet.ContainerId, signet.Axis, signet.ApCost, signet.UniqueCarry),
            new AttunedCharm("b", Charms().First(c => c.ApCost == 1).ContainerId,
                Charms().First(c => c.ApCost == 1).Axis, 1, false),
        };
        Assert.Empty(CharmPouchGate.Explain(fits, t.StartingCapacityAp, t));

        var doesNot = new[]
        {
            new AttunedCharm("a", signet.ContainerId, signet.Axis, signet.ApCost, signet.UniqueCarry),
            new AttunedCharm("b", twoAp.ContainerId, twoAp.Axis, twoAp.ApCost, twoAp.UniqueCarry),
        };
        var fail = Assert.Single(CharmPouchGate.Explain(doesNot, t.StartingCapacityAp, t));
        Assert.Equal(CharmCarryRefusalReason.CharmBudgetExceeded, fail.Reason);
    }

    [Fact]
    public void All_ten_shipped_resonance_containers_are_refused_by_the_pouch_gate()
    {
        // ⛔ The divergence module 12 measured rather than renamed: every shipped resonance id is
        // UNPADDED. The gate must refuse the AUTHORED spelling, because that is the id an instance of
        // one would actually carry.
        var t = Tuning();
        var rows = Resonance();
        Assert.Equal(10, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsAuthoredUnpadded, r.AuthoredContainerId));

        foreach (var r in rows)
        {
            Assert.True(CharmPouchGate.IsResonanceContainer(r.AuthoredContainerId), r.AuthoredContainerId);
            Assert.True(CharmPouchGate.IsResonanceContainer(r.ContainerId), r.ContainerId);

            var fail = Assert.Single(CharmPouchGate.Explain(
                new[] { new AttunedCharm("x", r.AuthoredContainerId, r.Axis, 1, false) }, 20, t));
            Assert.Equal(CharmCarryRefusalReason.CharmNotCarryable, fail.Reason);
        }
    }

    [Fact]
    public void No_shipped_charm_id_is_mistaken_for_a_resonance_container()
    {
        // The other direction of the same predicate: a false positive here would silently make an
        // authored charm unattunable, which is the invisible version of the bug.
        Assert.All(Charms(), c => Assert.False(CharmPouchGate.IsResonanceContainer(c.ContainerId),
            c.ContainerId));
    }

    [Fact]
    public void No_shipped_charm_declares_a_level_req_so_the_player_level_gap_is_inert_today()
    {
        // ⏸ ssot-charms §8 item 6 is unanswered and `players` carries no level. This measures why that
        // has not bitten: no charm in the corpus authors one. The day one does, the gate refuses by
        // name rather than passing — CharmCarryTests covers that arm.
        var json = Directory
            .EnumerateFiles(Path.Combine(CharmCarryTests.RepoRoot(), "data", "seed", "items", "charms"), "*.json")
            .Select(File.ReadAllText)
            .ToList();

        Assert.All(json, j => Assert.DoesNotContain("levelReq", j, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_shipped_charm_declares_frame_hint_any_so_section_3_7s_check_is_inert_and_that_is_measured()
    {
        // ⚠ Not a defect and not a rule: an observation about today's corpus, pinned so a later session
        // does not read "charms are frame-blind in the data" as "the frame_hint check is dead code".
        var json = Directory
            .EnumerateFiles(Path.Combine(CharmCarryTests.RepoRoot(), "data", "seed", "items", "charms"), "*.json")
            .Where(p => !Path.GetFileName(p).Equals("resonance.json", StringComparison.Ordinal))
            .Select(p => System.Text.Json.JsonDocument.Parse(File.ReadAllText(p)))
            .ToList();

        var hints = new List<string>();
        foreach (var doc in json)
            using (doc)
                foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
                    hints.Add(e.GetProperty("frameHint").GetString()!);

        Assert.Equal(60, hints.Count);
        Assert.All(hints, h => Assert.Equal("any", h));
    }

    [Fact]
    public void The_full_corpus_can_never_be_carried_at_once_and_that_is_the_mechanic_working()
    {
        // §7.1's guard, measured: the pouch is a choice, not a collection. 60 charms cost far more than
        // the top authored rung, so "collect more" is never a build.
        var t = Tuning();
        var all = Charms()
            .Select((c, i) => new AttunedCharm($"i{i}", c.ContainerId, c.Axis, c.ApCost, c.UniqueCarry))
            .ToList();

        var total = CharmPouchGate.TotalAp(all);
        Assert.True(total > t.CapacityLadder[^1] * 4,
            $"the whole corpus costs {total} AP against a top authored rung of {t.CapacityLadder[^1]}");

        var fails = CharmPouchGate.Explain(all, t.CapacityLadder[^1], t);
        Assert.Contains(fails, f => f.Reason == CharmCarryRefusalReason.CharmBudgetExceeded);
        Assert.Contains(fails, f => f.Reason == CharmCarryRefusalReason.CharmAxisOverflow);
    }

    [Fact]
    public void A_run_snapshot_over_real_charms_binds_one_row_per_deployed_actor_per_held_thing()
    {
        var t = Tuning();
        var survivability = Charms().Where(c => c.Axis == "survivability").Take(3).ToList();
        var attuned = survivability
            .Select((c, i) => new AttunedCharm($"inst-{i}", c.ContainerId, c.Axis, c.ApCost, c.UniqueCarry))
            .ToList();

        var snapshot = CharmRunBinder.Snapshot(attuned);
        var squad = new[] { "spec-a", "spec-b" };
        var bindings = CharmRunBinder.Bindings(snapshot, squad, Resonance(), t);

        // 3 charms + both satisfied survivability tiers (cumulative), per actor
        Assert.Equal(squad.Length * (3 + 2), bindings.Count);
        Assert.All(bindings, b => Assert.Equal("unique-actor", b.OwnerKind));
        Assert.Contains(bindings, b => b.ContainerId == ThresholdContainerIds.CharmResonance("survivability", 2));
        Assert.Contains(bindings, b => b.ContainerId == ThresholdContainerIds.CharmResonance("survivability", 3));
    }
}
