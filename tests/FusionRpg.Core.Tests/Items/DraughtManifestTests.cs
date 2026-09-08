using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Consumables;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// The dispatch gate and the run-start projection — ssot-consumables.md §4.3/§4.4 (the manifest and
/// its two structural defences) and §5.4 (why a draught is a projection and not a binding).
/// </summary>
public class DraughtManifestTests
{
    static ConsumableTuning Tuning() => ConsumableTests.Tuning();

    static ConsumableDefRow Def(string id, string group, int cost = 1) =>
        new(id, ConsumableClass.Draught, new[] { UseContext.Dispatch }, 3, group, cost);

    static ConsumableCatalog Catalog(params ConsumableDefRow[] defs)
    {
        var load = ConsumableCatalog.Load(defs, Tuning());
        Assert.Empty(load.Rejections);
        return load.Catalog;
    }

    // ---- the carry limit is a belt --------------------------------------------------------------------

    [Fact]
    public void The_manifest_gate_refuses_above_the_belts_slots_at_dispatch()
    {
        var cat = Catalog(
            Def("consumable.k2-001", "atom.might|"),
            Def("consumable.k2-002", "atom.quickening|"),
            Def("consumable.k2-003", "atom.ferocity|"));

        var manifest = new[]
        {
            new DraughtManifestEntry("consumable.k2-001", 1),
            new DraughtManifestEntry("consumable.k2-002", 1),
            new DraughtManifestEntry("consumable.k2-003", 1),
        };

        Assert.Empty(cat.GateManifest(manifest, BeltCapacity.FromEquippedGirdle(3)));

        var refused = cat.GateManifest(manifest, BeltCapacity.FromEquippedGirdle(2));
        var fail = Assert.Single(refused);
        Assert.StartsWith(ConsumableRules.LimitExceeded, fail.Detail, StringComparison.Ordinal);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, fail.Reason);
    }

    [Fact]
    public void With_no_belt_equipped_the_count_is_zero_so_any_manifest_is_refused()
    {
        // ⭐ D37: "With no belt equipped the count is 0, not a default." The refusal says so, because a
        // player told only "limit exceeded" would go looking for a setting that does not exist.
        var cat = Catalog(Def("consumable.k2-001", "atom.might|"));
        var fail = Assert.Single(cat.GateManifest(
            new[] { new DraughtManifestEntry("consumable.k2-001", 1) }, BeltCapacity.Unequipped));
        Assert.StartsWith(ConsumableRules.LimitExceeded, fail.Detail, StringComparison.Ordinal);
        Assert.Contains("no girdle is equipped", fail.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_manifest_passes_even_with_no_belt()
    {
        var cat = Catalog(Def("consumable.k2-001", "atom.might|"));
        Assert.Empty(cat.GateManifest(Array.Empty<DraughtManifestEntry>(), BeltCapacity.Unequipped));
    }

    [Fact]
    public void A_manifest_cost_of_two_occupies_two_places_without_a_second_table()
    {
        var cat = Catalog(Def("consumable.k1-019", "atom.vitality|", cost: 2));
        var manifest = new[] { new DraughtManifestEntry("consumable.k1-019", 1) };
        Assert.Empty(cat.GateManifest(manifest, BeltCapacity.FromEquippedGirdle(2)));
        Assert.Single(cat.GateManifest(manifest, BeltCapacity.FromEquippedGirdle(1)));
    }

    [Fact]
    public void The_summed_cost_is_a_long_and_widens_before_multiplying_so_a_huge_qty_throws_rather_than_wrapping()
    {
        // int.MaxValue * 2 overflows an int into a NEGATIVE total, which would pass any belt. The gate
        // widens before multiplying and runs checked, so the answer is an exception, never a free pass.
        var cat = Catalog(Def("consumable.k2-001", "atom.might|", cost: int.MaxValue));

        // 3 x int.MaxValue is 6,442,450,941 — it does not fit an int, and as a long it is correctly
        // ABOVE a belt of int.MaxValue, so the only refusal is the honest one.
        var wide = cat.GateManifest(
            new[] { new DraughtManifestEntry("consumable.k2-001", 3) },
            BeltCapacity.FromEquippedGirdle(int.MaxValue));
        Assert.Single(wide);
        Assert.StartsWith(ConsumableRules.LimitExceeded, wide[0].Detail, StringComparison.Ordinal);
        Assert.Contains("6442450941", wide[0].Detail, StringComparison.Ordinal);

        // …and past `long` itself it THROWS rather than wrapping into a total that fits (AGENTS.md:
        // "overflow throws, never wraps"). Three maxed lines is ~1.4e19, past long.MaxValue's 9.2e18.
        Assert.Throws<OverflowException>(() => cat.GateManifest(
            new[]
            {
                new DraughtManifestEntry("consumable.k2-001", int.MaxValue),
                new DraughtManifestEntry("consumable.k2-001", int.MaxValue),
                new DraughtManifestEntry("consumable.k2-001", int.MaxValue),
            },
            BeltCapacity.FromEquippedGirdle(1)));
    }

    // ---- one per exclusion group ----------------------------------------------------------------------

    [Fact]
    public void Two_manifest_entries_sharing_an_exclusion_group_are_refused()
    {
        var cat = Catalog(
            Def("consumable.k2-004", "atom.elemental-power|fire"),
            Def("consumable.k2-005", "atom.elemental-power|fire"));

        var fail = Assert.Single(cat.GateManifest(
            new[]
            {
                new DraughtManifestEntry("consumable.k2-004", 1),
                new DraughtManifestEntry("consumable.k2-005", 1),
            },
            BeltCapacity.FromEquippedGirdle(4)));

        Assert.StartsWith(ConsumableRules.FamilyConflict, fail.Detail, StringComparison.Ordinal);
        Assert.Contains("atom.elemental-power|fire", fail.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_elements_of_one_family_do_NOT_collide_because_the_key_is_family_plus_variant()
    {
        // The shipped pool-group default (definitions §4), reused: a container may roll FIRE power and
        // ICE power, but never two tiers of the same variant. Draughts inherit exactly that.
        var cat = Catalog(
            Def("consumable.k2-004", "atom.elemental-power|fire"),
            Def("consumable.k2-006", "atom.elemental-power|ice"));

        Assert.Empty(cat.GateManifest(
            new[]
            {
                new DraughtManifestEntry("consumable.k2-004", 1),
                new DraughtManifestEntry("consumable.k2-006", 1),
            },
            BeltCapacity.FromEquippedGirdle(4)));
    }

    [Fact]
    public void The_gate_returns_every_refusal_so_three_bad_lines_report_three_problems()
    {
        var cat = Catalog(
            Def("consumable.k2-004", "atom.elemental-power|fire"),
            Def("consumable.k2-005", "atom.elemental-power|fire"));

        var fails = cat.GateManifest(
            new[]
            {
                new DraughtManifestEntry("consumable.k2-004", 1),
                new DraughtManifestEntry("consumable.k2-005", 1),
                new DraughtManifestEntry("consumable.k9-999", 1),
                new DraughtManifestEntry("consumable.k2-004", 0),
            },
            BeltCapacity.Unequipped);

        Assert.Contains(fails, f => f.Detail.StartsWith(ConsumableRules.FamilyConflict, StringComparison.Ordinal));
        Assert.Contains(fails, f => f.Detail.StartsWith(ConsumableRules.UnknownConsumable, StringComparison.Ordinal));
        Assert.Contains(fails, f => f.Detail.StartsWith(ConsumableRules.BadValue, StringComparison.Ordinal));
        Assert.Contains(fails, f => f.Detail.StartsWith(ConsumableRules.LimitExceeded, StringComparison.Ordinal));
    }

    [Fact]
    public void A_consumable_that_does_not_name_dispatch_is_refused_at_the_dispatch_gate()
    {
        var menuOnly = new ConsumableDefRow(
            "consumable.k1-001", ConsumableClass.Restore, new[] { UseContext.Menu }, 2, "atom.vitality|");
        var cat = Catalog(menuOnly);

        var fail = Assert.Single(cat.GateManifest(
            new[] { new DraughtManifestEntry("consumable.k1-001", 1) },
            BeltCapacity.FromEquippedGirdle(4)));
        Assert.StartsWith(ConsumableRules.UseContextUnsupported, fail.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_orphan_container_is_reported_at_load_because_it_is_not_usable_content()
    {
        var load = ConsumableCatalog.Load(
            new[] { Def("consumable.k2-001", "atom.might|") }, Tuning(),
            orphanContainerIds: new[] { "consumable.k9-001" });
        var fail = Assert.Single(load.Rejections);
        Assert.StartsWith(ConsumableRules.Orphan, fail.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_answers_for_a_known_row_and_null_for_an_unknown_one()
    {
        var cat = Catalog(Def("consumable.k2-001", "atom.might|"));
        Assert.NotNull(cat.Resolve("consumable.k2-001"));
        Assert.Null(cat.Resolve("consumable.k2-999"));
        Assert.Equal(1, cat.Count);
    }

    // ---- the projection ---------------------------------------------------------------------------------

    static BattleActorSetup Member(string key) => new()
    {
        Key = key, Side = "squad", SpeciesId = "demon.test", Level = 10, MaxHp = 1000, Atk = 100,
    };

    [Fact]
    public void A_draught_is_ApplyInjuries_with_the_opposite_sign_and_reaches_every_squad_member()
    {
        var squad = new[] { Member("squad:0"), Member("squad:1"), Member("squad:2") };
        var after = DraughtProjection.Apply(squad, new[]
        {
            new DraughtMod("consumable.k2-004", DerivedStatChannels.CombatPowerOmni, 120L),
        });

        Assert.Equal(3, after.Count);
        foreach (var m in after)
        {
            var mod = Assert.Single(m.ChannelMods);
            Assert.Equal(DerivedStatChannels.CombatPowerOmni, mod.ChannelId);
            Assert.Equal(120L, mod.Amount);
        }

        // pure: the inputs are untouched, exactly as ApplyInjuries behaves
        Assert.All(squad, m => Assert.Empty(m.ChannelMods));
    }

    [Fact]
    public void The_projection_appends_rather_than_replacing_so_injuries_and_draughts_coexist()
    {
        var injured = Member("squad:0") with
        {
            ChannelMods = new[] { new BattleChannelMod(DerivedStatChannels.CombatPowerOmni, -25L) },
        };

        var after = DraughtProjection.Apply(new[] { injured }, new[]
        {
            new DraughtMod("consumable.k2-001", DerivedStatChannels.CombatPowerOmni, 120L),
        });

        Assert.Equal(new[] { -25L, 120L }, after[0].ChannelMods.Select(m => m.Amount).ToArray());
    }

    [Fact]
    public void An_empty_manifest_returns_the_squad_unchanged_by_reference()
    {
        var squad = new[] { Member("squad:0") };
        Assert.Same(squad, DraughtProjection.Apply(squad, Array.Empty<DraughtMod>()));
    }

    [Fact]
    public void A_non_positive_draught_throws_rather_than_being_clamped_to_nothing()
    {
        var squad = new[] { Member("squad:0") };
        foreach (var amount in new[] { 0L, -50L })
            Assert.Throws<ArgumentOutOfRangeException>(() => DraughtProjection.Apply(
                squad, new[] { new DraughtMod("consumable.k2-001", DerivedStatChannels.CombatPowerOmni, amount) }));
    }

    [Fact]
    public void The_projection_carries_a_long_past_the_int_ceiling_without_narrowing()
    {
        var squad = new[] { Member("squad:0") };
        const long huge = 3_000_000_000L;   // past int.MaxValue
        var after = DraughtProjection.Apply(squad, new[]
        {
            new DraughtMod("consumable.k2-001", DerivedStatChannels.CombatPowerOmni, huge),
        });
        Assert.Equal(huge, after[0].ChannelMods[0].Amount);
    }

    [Fact]
    public void A_draught_naming_no_channel_is_refused()
    {
        var squad = new[] { Member("squad:0") };
        Assert.Throws<ArgumentException>(() => DraughtProjection.Apply(
            squad, new[] { new DraughtMod("consumable.k2-001", "", 10L) }));
    }
}
