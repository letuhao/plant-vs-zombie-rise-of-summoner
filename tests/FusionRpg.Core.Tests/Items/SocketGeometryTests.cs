using System.Text.Json;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Items.Sockets;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// spec-sockets.md §3/§5 against the REAL shipped <c>data/tuning/sockets.v1.json</c> and the real
/// 740-entry base-type corpus — not a synthetic fixture. A ceiling table that agreed with a fixture
/// and disagreed with the corpus would be exactly the defect module 6 already hit once.
/// </summary>
public class SocketGeometryTests
{
    static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    static string TuningPath => Path.Combine(RepoRoot(), "data", "tuning", "sockets.v1.json");

    internal static SocketTuning Shipped() => SocketTuning.Parse(File.ReadAllText(TuningPath));

    // ── §3, the re-issued fifteen-role table ────────────────────────────────────────────────────

    [Fact]
    public void Socket_max_is_defined_for_all_fifteen_roles()
    {
        var tuning = Shipped();
        Assert.Equal(15, tuning.SocketCeiling.Count);

        foreach (ItemRole role in Enum.GetValues(typeof(ItemRole)))
        {
            if (role == ItemRole.Standard) continue;
            Assert.True(tuning.SocketCeiling.ContainsKey(role), $"no ceiling for '{ItemRoles.Id(role)}'");
        }

        // ⭐ The three §3 added to the stale twelve-id table, named individually so a regression to
        // the old table is a specific failure rather than a count mismatch.
        Assert.Equal(3, tuning.CeilingFor(ItemRole.WardArray));
        Assert.Equal(2, tuning.CeilingFor(ItemRole.Infusion));
        Assert.Equal(2, tuning.CeilingFor(ItemRole.Retinue));
    }

    [Fact]
    public void No_socket_max_row_exists_for_commander_standard()
    {
        Assert.False(Shipped().SocketCeiling.ContainsKey(ItemRole.Standard));
        Assert.Equal(0, Shipped().CeilingFor(ItemRole.Standard));

        // D14 — out of scope, not silently included. A row (even a zero one) reads as "in scope".
        var withStandard = File.ReadAllText(TuningPath)
            .Replace("\"jewel-minor-b\": 1", "\"jewel-minor-b\": 1,\n    \"standard\": 2");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(withStandard));
        Assert.Contains("D14", ex.Message);
    }

    [Fact]
    public void The_old_twelve_suffixed_role_ids_appear_nowhere()
    {
        var raw = File.ReadAllText(TuningPath);
        foreach (var stale in new[]
                 {
                     "core-protective", "sense-utility", "mantle-utility",
                     "manipulator-offense", "girdle-resource", "head-protective",
                 })
            Assert.DoesNotContain(stale, raw, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ceiling_above_the_structural_maximum_throws_rather_than_clamping()
    {
        var raised = File.ReadAllText(TuningPath).Replace("\"armament-primary\": 4", "\"armament-primary\": 6");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(raised));
        Assert.Contains("THROWS rather than clamping", ex.Message);
        Assert.Equal(4, SocketLimits.SocketMaxCeiling);
    }

    [Fact]
    public void The_structural_ceiling_in_the_file_and_in_code_cannot_drift_apart()
    {
        var moved = File.ReadAllText(TuningPath).Replace("\"structuralCeiling\": 4", "\"structuralCeiling\": 5");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(moved));
        Assert.Contains("SocketLimits.SocketMaxCeiling", ex.Message);
    }

    [Fact]
    public void The_structural_ceiling_says_it_is_structural_and_names_why()
    {
        // AGENTS.md requires a structural limit to say so in the artefact a balance pass edits, not
        // only in code. Asserting the words is what stops the note being deleted in a tidy-up.
        using var doc = JsonDocument.Parse(File.ReadAllText(TuningPath));
        var note = doc.RootElement.GetProperty("structuralCeilingNote").GetString()!;
        Assert.Contains("STRUCTURAL", note, StringComparison.Ordinal);
        Assert.Contains("LEGIBILITY", note, StringComparison.Ordinal);
        Assert.Contains("contentScale", note, StringComparison.Ordinal);
    }

    // ── §3's second property, corrected against the corpus ──────────────────────────────────────

    [Fact]
    public void Socket_max_is_a_role_ceiling_and_a_base_type_may_vary_beneath_it()
    {
        // ⛔ spec-sockets.md §3 asks for `socket_max_is_fixed_per_role_and_never_varies_by_base_type`.
        // That test is UNWRITABLE against the shipped corpus: module 6 measured `armament-primary` at
        // {0:18, 1:26, 2:4}. The enforceable invariant — and the one that actually defends §8.1 — is
        // "never EXCEEDS its role's ceiling", which is what SocketGeometry.ValidateEntry checks.
        var tuning = Shipped();

        Assert.True(SocketGeometry.ValidateEntry(ItemRole.ArmamentPrimary, 0, tuning).IsOk);
        Assert.True(SocketGeometry.ValidateEntry(ItemRole.ArmamentPrimary, 2, tuning).IsOk);
        Assert.True(SocketGeometry.ValidateEntry(ItemRole.ArmamentPrimary, 4, tuning).IsOk);

        var over = SocketGeometry.ValidateEntry(ItemRole.JewelMajor, 2, tuning);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, over.Reason);
        Assert.Contains(SocketRules.EntryExceedsRoleCeiling, over.Detail);
    }

    [Fact]
    public void The_shipped_corpus_never_exceeds_a_role_ceiling()
    {
        var tuning = Shipped();
        var root = Path.Combine(RepoRoot(), "data", "seed", "items", "base-types");
        var checkedCount = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (!doc.RootElement.TryGetProperty("entries", out var entries)) continue;

            foreach (var e in entries.EnumerateArray())
            {
                if (e.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.False) continue;
                if (!e.TryGetProperty("role", out var roleEl) || !ItemRoles.TryParse(roleEl.GetString(), out var role))
                    continue;
                if (role == ItemRole.Standard) continue; // D14, retired rows
                if (!e.TryGetProperty("socketMax", out var smEl) || smEl.ValueKind != JsonValueKind.Number) continue;

                checkedCount++;
                var rejection = SocketGeometry.ValidateEntry(role, smEl.GetInt32(), tuning);
                Assert.True(rejection.IsOk,
                    $"{e.GetProperty("id").GetString()}: {rejection.Detail}");
            }
        }

        Assert.Equal(720, checkedCount); // the live, non-`standard`, socketMax-carrying corpus
        Assert.True(checkedCount > 600, $"only {checkedCount} live entries checked — the corpus read is wrong");
    }

    [Fact]
    public void A_four_ingredient_combo_only_fits_armament_primary_or_core_guard()
    {
        var tuning = Shipped();
        Assert.Equal(4, tuning.StrainSpliceIngredientCount);
        Assert.Equal(
            new[] { ItemRole.ArmamentPrimary, ItemRole.CoreGuard },
            SocketGeometry.RolesThatCanHostAStrain(tuning));
    }

    // ── §5, where sockets come from ─────────────────────────────────────────────────────────────

    [Fact]
    public void Every_rung_carries_a_grant_window_and_adjacent_windows_overlap()
    {
        var tuning = Shipped();
        Assert.Equal(RarityLadder.RungIds.Count, tuning.RarityGrant.Count);

        for (var i = 1; i < RarityLadder.RungIds.Count; i++)
        {
            var low = tuning.RarityGrant[RarityLadder.RungIds[i - 1]];
            var high = tuning.RarityGrant[RarityLadder.RungIds[i]];
            Assert.True(high.Min >= low.Min && high.Max >= low.Max, "grant windows must be monotonic");
            Assert.True(high.Min <= low.Max, "OD4: adjacent windows must overlap");
        }

        // The overlap is not decorative: a mid-band item CAN out-socket the band above it.
        Assert.True(tuning.RarityGrant["heirloom"].Max > tuning.RarityGrant["sunwoven"].Min);
    }

    [Fact]
    public void A_non_overlapping_grant_table_is_refused_at_load()
    {
        var gapped = File.ReadAllText(TuningPath)
            .Replace("\"sunwoven\":   { \"socketMin\": 2, \"socketMax\": 4 }",
                     "\"sunwoven\":   { \"socketMin\": 4, \"socketMax\": 4 }");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(gapped));
        Assert.Contains("do not overlap", ex.Message);
    }

    [Fact]
    public void Sockets_at_drop_never_exceeds_the_base_types_own_socket_max()
    {
        var tuning = Shipped();
        for (ulong seed = 1; seed <= 500; seed++)
        foreach (var rung in RarityLadder.RungIds)
        foreach (var entryMax in new[] { 0, 1, 2, 3, 4 })
        {
            var n = SocketGeometry.SocketsAtDrop(entryMax, rung, seed, tuning);
            Assert.InRange(n, 0, entryMax);
            Assert.True(n <= tuning.RarityGrant[rung].Max);
        }
    }

    [Fact]
    public void Sockets_at_drop_is_reproducible_and_uses_the_owned_prng()
    {
        var tuning = Shipped();
        var a = SocketGeometry.SocketsAtDrop(4, "almanac", 8812349UL, tuning);
        var b = SocketGeometry.SocketsAtDrop(4, "almanac", 8812349UL, tuning);
        Assert.Equal(a, b);

        // The draw is the LootStreams.Sockets stream off the roll seed, spelled once — reproduce it
        // here from SeededRng directly so a change to either side is a red test rather than a drift.
        var window = tuning.RarityGrant["almanac"];
        var rng = SeededRng.DeriveStream(8812349UL, LootStreams.Sockets);
        Assert.Equal(window.Min + rng.NextInt(window.Max - window.Min + 1), a);
    }

    [Fact]
    public void Socket_seed_is_domain_separated_from_the_affix_pool_stream()
    {
        // Adding a socket later cannot move an item's affixes: the socket stream is derived from the
        // instance's own roll_seed under its own name and shares no state with any item.* stream.
        Assert.Equal("item.socket", LootStreams.Sockets);
        Assert.NotEqual(LootStreams.Sockets, LootStreams.Rolls(0));
        Assert.NotEqual(LootStreams.Sockets, LootStreams.Rarity(0));

        var socket = SeededRng.DeriveStream(4242UL, LootStreams.Sockets);
        var rolls = SeededRng.DeriveStream(4242UL, LootStreams.Rolls(0));
        Assert.NotEqual(socket.NextULong(), rolls.NextULong());
    }

    [Fact]
    public void The_grant_windows_span_the_whole_zero_to_ceiling_range_across_the_ladder()
    {
        var tuning = Shipped();
        Assert.Equal(0, tuning.RarityGrant[RarityLadder.RungIds[0]].Max);
        Assert.Equal(
            SocketLimits.SocketMaxCeiling,
            tuning.RarityGrant[RarityLadder.RungIds[^1]].Max);
    }

    // ── §5's crafting layer (D23) ───────────────────────────────────────────────────────────────

    [Fact]
    public void Crafting_tops_the_count_up_and_stops_at_the_base_types_own_max()
    {
        Assert.Equal(3, SocketGeometry.SocketsNow(socketsAtDrop: 1, socketAddOperations: 2, entrySocketMax: 4));
        Assert.Equal(4, SocketGeometry.SocketsNow(1, 9, 4));
        // D23: available at EVERY rarity — a chaff item that rolled 0 can still be bored to its cap.
        Assert.Equal(2, SocketGeometry.SocketsNow(0, 2, 2));
    }

    [Fact]
    public void A_negative_count_is_refused_rather_than_absorbed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SocketGeometry.SocketsNow(1, -1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SocketGeometry.SocketsAtDrop(-1, "chaff", 1UL, Shipped()));
    }

    // ── §10, removal ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Removal_is_tiered_and_the_item_always_survives()
    {
        var tuning = Shipped();
        Assert.Equal(SocketRemovalOutcome.Free, tuning.RemovalFor(1));
        Assert.Equal(SocketRemovalOutcome.Free, tuning.RemovalFor(2));
        Assert.Equal(SocketRemovalOutcome.Costed, tuning.RemovalFor(3));
        Assert.Equal(SocketRemovalOutcome.DestroysInsert, tuning.RemovalFor(4));
        Assert.Equal(SocketRemovalOutcome.DestroysInsert, tuning.RemovalFor(5));

        // There is deliberately no DestroysItem outcome — the whole enum is three values, and the
        // absence is the design: "you can always empty a socket; what varies is what you keep".
        Assert.Equal(3, Enum.GetValues<SocketRemovalOutcome>().Length);
    }

    [Fact]
    public void A_removal_table_with_no_commitment_tier_is_refused_at_load()
    {
        var toothless = File.ReadAllText(TuningPath).Replace("\"costedThroughTier\": 3", "\"costedThroughTier\": 5");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(toothless));
        Assert.Contains("unreachable", ex.Message);
    }

    // ── §4, the per-actor backstop ──────────────────────────────────────────────────────────────

    [Fact]
    public void The_per_actor_combo_cap_is_read_from_tuning_and_is_currently_non_binding()
    {
        var tuning = Shipped();
        Assert.Equal(3, tuning.MaxCombosPerActor);

        // Honest about being inert: the GEOMETRIC ceiling is the number of roles that can host a
        // four-ingredient combination at all, and it is below the cap. When that stops being true
        // this assertion goes red, which is the reminder the backstop exists for.
        var geometric = SocketGeometry.RolesThatCanHostAStrain(tuning).Count;
        Assert.Equal(2, geometric);
        Assert.True(geometric < tuning.MaxCombosPerActor, "the cap is meant to be non-binding today");
    }

    // ── Tuning hygiene ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_section_is_required_and_none_defaults()
    {
        var raw = File.ReadAllText(TuningPath);
        foreach (var section in new[] { "socketCeiling", "rarityGrant", "insertTiers", "removal", "resonance", "strainSplice" })
        {
            using var doc = JsonDocument.Parse(raw);
            var stripped = doc.RootElement.EnumerateObject()
                .Where(p => p.Name != section)
                .ToDictionary(p => p.Name, p => (object?)JsonSerializer.Deserialize<JsonElement>(p.Value.GetRawText()));
            var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(JsonSerializer.Serialize(stripped)));
            Assert.Contains(section, ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Omni_is_never_accepted_as_a_resonance_element()
    {
        var omni = File.ReadAllText(TuningPath).Replace("\"eclipsePair\": [\"light\", \"dark\"]", "\"eclipsePair\": [\"omni\", \"dark\"]");
        var ex = Assert.Throws<SocketTuningRejection>(() => SocketTuning.Parse(omni));
        Assert.Contains("omni", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_insert_tier_count_is_a_soft_content_axis_not_a_cap()
    {
        // AGENTS.md: a magnitude ceiling must be configurable. Raising the tier ladder is a file edit
        // and nothing in the module refuses the higher tier.
        var extended = File.ReadAllText(TuningPath).Replace("\"count\": 5", "\"count\": 12");
        var tuning = SocketTuning.Parse(extended);
        Assert.Equal(12, tuning.InsertTierCount);
        Assert.Equal(SocketRemovalOutcome.DestroysInsert, tuning.RemovalFor(12));
    }
}
