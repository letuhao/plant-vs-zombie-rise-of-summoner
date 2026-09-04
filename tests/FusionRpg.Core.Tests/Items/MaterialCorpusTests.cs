using System.Text.Json;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Materials;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `salvage-craft` (item module 14) against the REAL shipped files — `data/tuning/materials.v1.json`,
/// `data/seed/items/recipes/recipes.json`, `data/seed/items/materials/materials.json` and the FROZEN
/// `data/seed/items/_registry/bands.v1.json`. Nothing here is synthetic.
/// </summary>
public class MaterialCorpusTests
{
    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }

    internal static string TuningJson() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "materials.v1.json"));

    internal static MaterialTuning Tuning() => MaterialTuning.Parse(TuningJson());

    internal static IEnumerable<string> RecipeCorpus() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "data", "seed", "items", "recipes"), "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(File.ReadAllText);

    internal static MaterialRecipeCatalog Catalog() => MaterialRecipeCatalog.Load(RecipeCorpus(), Tuning());

    // ---- the tuning file ---------------------------------------------------------------------------

    [Fact]
    public void The_shipped_tuning_parses_and_prices_all_ten_operations()
    {
        var t = Tuning();
        Assert.Equal(10, t.Operations.Count);
        foreach (var op in CraftOperations.All)
            Assert.True(t.Operations.ContainsKey(op), CraftOperations.Id(op));
        Assert.Equal(RarityLadder.RungIds.Count, t.Salvage.Count);
    }

    [Fact]
    public void Socket_imbue_has_a_cost_row_and_prices_like_bore()
    {
        // ⚠ The gap D24 left: I9 §7.4 has nine operations and no row for imbuing a crafted socket's
        // affinity at all. The souls and substrate legs are `bore`'s VERBATIM, per D24's ruling that
        // it prices on the same curve; the one addition is the essence leg, because essence is the
        // class whose whole job is direction without magnitude.
        var t = Tuning();
        var bore = t.Operations[CraftOperation.Bore];
        var imbue = t.Operations[CraftOperation.Imbue];

        Assert.Equal(bore.Souls, imbue.Souls);
        Assert.Equal(bore.Substrate, imbue.Substrate);
        Assert.Equal(bore.CatalystId, imbue.CatalystId);
        Assert.Null(bore.Essence);
        Assert.NotNull(imbue.Essence);
        Assert.Equal(CostVariable.Rung, imbue.Essence!.Value.Variable);
    }

    [Fact]
    public void A_tuning_that_breaks_D24_is_refused_at_load_not_at_the_first_crafted_socket()
    {
        var broken = TuningJson().Replace(
            "\"imbue\": {\n      \"owner\": \"sockets (16)\",\n      \"souls\": { \"coefficient\": 50, \"variable\": \"rung\" }",
            "\"imbue\": {\n      \"owner\": \"sockets (16)\",\n      \"souls\": { \"coefficient\": 51, \"variable\": \"rung\" }");
        Assert.NotEqual(TuningJson(), broken); // the substitution really landed
        var ex = Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(broken));
        Assert.Contains("D24", ex.Message);
    }

    [Fact]
    public void The_parser_refuses_rather_than_defaults()
    {
        // A missing key must throw at load. A generator or a price silently running on a default is
        // how an unreviewed number reaches content (module 13's own lesson, applied here).
        using var doc = JsonDocument.Parse(TuningJson());
        var root = doc.RootElement;
        foreach (var key in new[] { "grade", "upcycle", "costBandMultiplierPerMille", "operations", "salvageCoefficient" })
        {
            var stripped = StripTopLevel(root, key);
            Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(stripped));
        }

        Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse("{}"));
        Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(""));
    }

    static string StripTopLevel(JsonElement root, string dropKey)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            foreach (var p in root.EnumerateObject())
                if (p.Name != dropKey)
                    p.WriteTo(w);
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void The_cost_band_table_mirrors_the_frozen_registry_value_for_value()
    {
        // bands.v1.json is FROZEN at registryVersion 1 and is the authority; materials.v1.json
        // mirrors it because Core never reads a file. A drift is a silent 2x price change, so it is
        // a red test instead.
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "data", "seed", "items", "_registry", "bands.v1.json")));

        var costBand = doc.RootElement.GetProperty("costBand");
        Assert.True(doc.RootElement.GetProperty("frozen").GetBoolean(), "bands.v1.json must still be frozen");

        var registry = costBand.GetProperty("multiplierTable").EnumerateArray()
            .ToDictionary(e => e.GetProperty("band").GetString()!, e => e.GetProperty("multiplierPerMille").GetInt32(),
                StringComparer.Ordinal);

        var mirrored = Tuning().BandMultipliersPerMille;
        Assert.Equal(registry.Count, mirrored.Count);
        foreach (var (band, m) in registry)
            Assert.Equal(m, mirrored[band]);

        // The enum in the registry and the mirrored keys are the same set, so a sixth band added
        // there is caught here rather than resolving to "unknown cost band" at the first recipe.
        Assert.Equal(
            costBand.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).OrderBy(x => x, StringComparer.Ordinal),
            mirrored.Keys.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void The_salvage_coefficients_reproduce_I9s_four_anchors_on_the_shipped_band_to_rung_map()
    {
        // The four anchors are not chosen — they are LegacyDemonRarityIds.ForwardMap, the SHIPPED
        // one-way band->rung map. I9 §5.1's four-row table lands on those four rungs value for value.
        var t = Tuning();
        var i9 = new Dictionary<string, (long Substrate, long Essence, long Shard)>(StringComparer.Ordinal)
        {
            ["common"] = (2, 0, 0),
            ["rare"] = (4, 2, 1),
            ["epic"] = (6, 3, 2),
            ["legendary"] = (9, 3, 3),
        };

        foreach (var (legacyId, expected) in i9)
        {
            Assert.True(LegacyDemonRarityIds.ForwardMap.TryGetValue(legacyId, out var rung), legacyId);
            var row = t.Salvage[rung.ToId()];
            Assert.Equal(expected.Substrate, row.SubstrateBase);
            Assert.Equal(expected.Essence, row.EssenceCap);
            Assert.Equal(expected.Shard, row.ShardBack);
        }
    }

    [Fact]
    public void Every_non_anchor_rung_is_the_stated_floor_interpolation_not_a_number_someone_liked()
    {
        // The derivation, re-computed from the four anchors and asserted against the file: integer
        // linear interpolation with FLOOR between anchors (rounding a salvage yield UP is the only
        // direction that can break R2), the last segment's slope continued above the top anchor for
        // substrateBase/shardBack, and a FLAT tail for essenceCap because I9's own table already
        // stopped it growing at epic.
        var t = Tuning();
        int[] anchorIdx = { 0, 3, 6, 8 };
        long[] substrate = { 2, 4, 6, 9 };
        long[] essence = { 0, 2, 3, 3 };
        long[] shard = { 0, 1, 2, 3 };

        for (var r = 0; r < RarityLadder.RungIds.Count; r++)
        {
            var row = t.Salvage[RarityLadder.RungIds[r]];
            Assert.Equal(Interp(r, anchorIdx, substrate, extrapolate: true), row.SubstrateBase);
            Assert.Equal(Interp(r, anchorIdx, essence, extrapolate: false), row.EssenceCap);
            Assert.Equal(Interp(r, anchorIdx, shard, extrapolate: true), row.ShardBack);
        }
    }

    static long Interp(int rung, int[] anchorIdx, long[] anchorVal, bool extrapolate)
    {
        for (var a = 0; a < anchorIdx.Length; a++)
        {
            if (rung == anchorIdx[a]) return anchorVal[a];
            if (rung < anchorIdx[a])
            {
                var lo = anchorIdx[a - 1];
                var span = anchorIdx[a] - lo;
                return anchorVal[a - 1] + (rung - lo) * (anchorVal[a] - anchorVal[a - 1]) / span; // integer FLOOR
            }
        }

        var last = anchorIdx.Length - 1;
        if (!extrapolate) return anchorVal[last];
        var slopeSpan = anchorIdx[last] - anchorIdx[last - 1];
        var rise = anchorVal[last] - anchorVal[last - 1];
        return anchorVal[last] + (rung - anchorIdx[last]) * rise / slopeSpan;
    }

    [Fact]
    public void Salvage_coefficients_are_monotone_and_a_falling_row_is_refused()
    {
        var t = Tuning();
        long ps = 0, pe = 0, psh = 0;
        foreach (var id in RarityLadder.RungIds)
        {
            var r = t.Salvage[id];
            Assert.True(r.SubstrateBase >= ps && r.EssenceCap >= pe && r.ShardBack >= psh, id);
            (ps, pe, psh) = (r.SubstrateBase, r.EssenceCap, r.ShardBack);
        }

        var broken = TuningJson().Replace(
            "\"firstseed\":  { \"substrateBase\": 7,  \"essenceCap\": 3, \"shardBack\": 2 }",
            "\"firstseed\":  { \"substrateBase\": 5,  \"essenceCap\": 3, \"shardBack\": 2 }");
        var ex = Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(broken));
        Assert.Contains("monotone", ex.Message);
    }

    [Fact]
    public void Upcycle_is_capped_at_grade_two_and_the_cap_is_a_bounded_ratio()
    {
        var t = Tuning();
        Assert.Equal(2, t.UpcycleMaxInputGrade);
        Assert.True(t.UpcycleMaxInputGrade < t.MaxGrade, "prime must have no upcycle path");

        // The cap's exemption is stated in the file a balance pass edits, not only in code — AGENTS.md
        // requires a bounded ratio to SAY it is one.
        using var doc = JsonDocument.Parse(TuningJson());
        var capNote = doc.RootElement.GetProperty("upcycle").GetProperty("capNote").GetString()!;
        Assert.Contains("BOUNDED RATIO", capNote);
        Assert.Contains("not a ceiling on how much a player may earn", capNote);

        // Upcycling INTO the top grade is the leak the cap closes — refused at load, not clamped.
        var broken = TuningJson().Replace("\"maxInputGrade\": 2", "\"maxInputGrade\": 4");
        Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(broken));
    }

    [Fact]
    public void Upcycles_own_strict_loss_is_its_conversion_ratio_and_a_free_ratio_is_refused()
    {
        // `upcycle`'s output is a MATERIAL, not a salvageable item, so R2's salvage-based form has
        // nothing to compare against — its guarantee is the ratio itself: five of grade g become one
        // of grade g+1. That is why the R2 property test excludes it and this asserts it instead.
        var t = Tuning();
        Assert.Equal(5, t.UpcycleInputPerOutput);
        Assert.Equal(5, t.Operations[CraftOperation.Upcycle].Substrate!.Value.Coefficient);
        Assert.Equal(CostVariable.Flat, t.Operations[CraftOperation.Upcycle].Substrate!.Value.Variable);

        // A ratio of 1 makes the conversion free, and anything below that makes it profitable.
        foreach (var bad in new[] { "1", "0" })
            Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(
                TuningJson().Replace("\"inputPerOutput\": 5", $"\"inputPerOutput\": {bad}")
                            .Replace("\"coefficient\": 5, \"variable\": \"flat\"", $"\"coefficient\": {bad}, \"variable\": \"flat\"")));

        // And the two places the same ratio is written must agree — a balance pass that moves the
        // drain valve and forgets the cost row is refused by name.
        var drifted = TuningJson().Replace("\"inputPerOutput\": 5", "\"inputPerOutput\": 6");
        var ex = Assert.Throws<MaterialTuningRejection>(() => MaterialTuning.Parse(drifted));
        Assert.Contains("same conversion ratio", ex.Message);
    }

    [Fact]
    public void A_quantity_can_never_resolve_to_zero_and_overflow_throws_it_never_wraps()
    {
        // Ceiling always, so no band can make a cost free.
        Assert.Equal(1, MaterialTuning.ApplyBand(1, 500));
        Assert.Equal(1, MaterialTuning.ApplyBand(1, 1));
        Assert.Equal(2, MaterialTuning.ApplyBand(3, 500));
        Assert.Equal(4, MaterialTuning.ApplyBand(4, 1000));
        Assert.Equal(8, MaterialTuning.ApplyBand(4, 2000));

        // long, widened before multiplying, divided by 1000 last and exactly once — a magnitude far
        // past int's 2,147,483,647 ceiling still resolves exactly rather than wrapping.
        const long past_int_max = 3_000_000_000L;
        Assert.Equal(24_000_000_000L, MaterialTuning.ApplyBand(past_int_max, 8000));

        // And checked, so an absurd quantity THROWS rather than wrapping into a free or negative
        // cost (CLAUDE.md rule 5: overflow throws, never wraps).
        Assert.Throws<OverflowException>(() => MaterialTuning.ApplyBand(long.MaxValue / 1000, 8000));
        Assert.Throws<OverflowException>(() => MaterialTuning.ApplyBand(long.MaxValue, 8000));
        Assert.Throws<OverflowException>(() => new CostLeg(long.MaxValue, CostVariable.Rung, false).BaseQty(1, 9, 0));
    }

    [Fact]
    public void The_grade_function_is_the_grade_lock_and_it_reads_only_the_item_level()
    {
        var t = Tuning();
        Assert.Equal(1, t.GradeForItemLevel(0));
        Assert.Equal(1, t.GradeForItemLevel(10));   // a level-10 zone returns crude forever
        Assert.Equal(1, t.GradeForItemLevel(24));
        Assert.Equal(2, t.GradeForItemLevel(25));
        Assert.Equal(3, t.GradeForItemLevel(60));   // I9 §7.5 example 2's own arithmetic
        Assert.Equal(4, t.GradeForItemLevel(75));
        Assert.Equal(4, t.GradeForItemLevel(100_000)); // volume cannot substitute for difficulty
    }

    // ---- the recipe corpus -------------------------------------------------------------------------

    [Fact]
    public void The_shipped_recipe_corpus_loads_and_every_refusal_is_named_with_the_module_that_unblocks_it()
    {
        var catalog = Catalog();

        // Measured against the real file, not estimated. 30 authored entries.
        Assert.Equal(30, catalog.Recipes.Count + catalog.Refusals.Count);

        // ⭐ Defect 1 CLOSED 2026-09-05 by module 15 (`enhance-reroll`), which owns the op_kind
        // namespace the reroll-one/reroll-all split lives in. The seven `reroll` rows were re-authored
        // against the split verb — 015/016/026/027/028 → `reroll-one` (their nameKeys say
        // `reroll-single-*` / `reroll-essence-*`) and 017/018 → `reroll-all` (`reroll-all-rare` /
        // `reroll-all-epic`). NOT ONE recipe is refused on the verb any more.
        var verbRefusals = catalog.Refusals.Where(r => r.Rule == MaterialRecipeCatalog.OperationUnavailableRule).ToList();
        Assert.Empty(verbRefusals);

        // ⛔ Defect 2 — the RETIRED band shard ids, which resolve but are never minted, so they can
        // never be paid. Five `elevate` rows carry one, and TWO of the re-authored `reroll-all` rows
        // (017 `shard.rare`, 018 `shard.epic`) now surface their own legacy shard, which the verb
        // refusal was previously masking: a refusal names ONE reason, and the verb was checked first.
        // 5 → 7 is the split landing, not a new defect. Both need the same corpus re-author the ten
        // missing display rows need, which is module 14's own deferred item.
        var legacyRefusals = catalog.Refusals.Where(r => r.Rule == MaterialRecipeCatalog.MaterialUnissuableRule).ToList();
        Assert.Equal(7, legacyRefusals.Count);
        Assert.All(legacyRefusals, r => Assert.Contains("retired band shard", r.Detail));

        // 30 authored − 7 legacy-shard refusals = 23 resolvable, up from 18.
        Assert.Equal(23, catalog.Recipes.Count);

        // Nothing is refused for a reason the module invented: every rule is one of the five it
        // registered, all namespaced `material.*` under the ONE ContentRuleViolated code.
        Assert.All(catalog.Refusals, r =>
        {
            Assert.StartsWith("material.", r.Rule);
            var rejection = r.AsRejection();
            Assert.Equal(FusionRpg.Core.Effects.Atoms.AtomRejectionReason.ContentRuleViolated, rejection.Reason);
        });

        Assert.NotEmpty(catalog.Recipes);
    }

    [Fact]
    public void No_new_member_of_the_closed_rejection_code_list()
    {
        // Module 11's rule, kept: 33 codes + None + ContentRuleViolated = 35. This module mints none.
        Assert.Equal(35, Enum.GetNames<FusionRpg.Core.Effects.Atoms.AtomRejectionReason>().Length);
        _ = Catalog(); // forces the namespace registration
        Assert.Contains(MaterialRecipeCatalog.Namespace, FusionRpg.Core.Effects.Atoms.ContentRuleNamespaces.All);
    }

    [Fact]
    public void Two_builds_of_the_recipe_catalog_are_byte_identical()
    {
        // The fusion-catalog golden precedent: a content hash that only moves when the content does.
        Assert.Equal(Catalog().ContentHash(), Catalog().ContentHash());
        Assert.Equal(64, Catalog().ContentHash().Length);
    }

    [Fact]
    public void Every_loadable_recipe_resolves_to_positive_integer_costs_in_fixed_class_order()
    {
        var catalog = Catalog();
        foreach (var recipe in catalog.Recipes.Values)
        {
            for (var rung = 0; rung < RarityLadder.RungIds.Count; rung++)
            {
                var ctx = new RecipeContext(rung, 3, 60, "humanoid", 5);
                var lines = catalog.Resolve(recipe.RecipeId, ctx);
                Assert.All(lines, l => Assert.True(l.Qty >= 1, $"{recipe.RecipeId} {l.MaterialId} = {l.Qty}"));
                Assert.Equal(
                    lines.Select(l => MaterialCatalog.ClassRank(l.Class)).ToArray(),
                    lines.Select(l => MaterialCatalog.ClassRank(l.Class)).OrderBy(x => x).ToArray());
            }
        }
    }

    [Fact]
    public void Cost_rises_with_the_target_and_theta_is_not_an_input_at_all()
    {
        // D26's positive half on the axis the shipped reference table actually prices. `bore` is
        // rung-linear, so a chaff chassis and an almanac pay differently for the same hole — D23's
        // pricing ruling, made real.
        var catalog = Catalog();
        var bore = catalog.Recipes.Values.First(r => r.Operation == CraftOperation.Bore);

        long Souls(int rung) => catalog.Resolve(bore.RecipeId, new RecipeContext(rung, 1, 40, "humanoid", 0))
            .Single(l => l.Class == MaterialClass.Souls).Qty;

        for (var r = 1; r < RarityLadder.RungIds.Count; r++)
            Assert.True(Souls(r) > Souls(r - 1), $"{RarityLadder.RungIds[r]} must cost more than {RarityLadder.RungIds[r - 1]}");

        // ⭐ D23: any rarity can extend a socket slot, and the bottom of the ladder pays a real,
        // payable price rather than being granted zero — the failure the per-rarity table had.
        Assert.True(Souls(0) > 0);
    }

    [Fact]
    public void Socketing_an_insert_costs_ten_souls_and_nothing_else_at_every_rung()
    {
        // I9 §7.4 states this as a RULE. The shipped recipe authors soulsCostBand `cheap`, which
        // would resolve it to 5; the row is bandImmune so the rule survives the author's band.
        var catalog = Catalog();
        var socket = catalog.Recipes.Values.Single(r => r.Operation == CraftOperation.Socket);
        Assert.Equal("cheap", socket.SoulsCostBand);

        for (var rung = 0; rung < RarityLadder.RungIds.Count; rung++)
        {
            var lines = catalog.Resolve(socket.RecipeId, new RecipeContext(rung, 5, 90, "plant", 12));
            var line = Assert.Single(lines);
            Assert.Equal(MaterialClass.Souls, line.Class);
            Assert.Equal(10, line.Qty);
        }
    }

    [Fact]
    public void I9s_worked_example_one_reproduces_off_the_reference_table_and_the_band_scales_it()
    {
        // I9 §7.5 example 1 computes the UNBANDED reference cost: forge a plant base at grade 2 —
        // souls 40 x 2 = 80, substrate 4 x 2 = 8, catalyst.forge 1. That is the reference row, and it
        // reproduces exactly off the shipped tuning.
        var t = Tuning();
        var forge = t.Operations[CraftOperation.Forge];
        Assert.Equal(80, forge.Souls!.Value.BaseQty(grade: 2, rungIndex: 0, enhanceLevel: 0));
        Assert.Equal(8, forge.Substrate!.Value.BaseQty(grade: 2, rungIndex: 0, enhanceLevel: 0));
        Assert.Equal(1, forge.Catalyst!.Value.BaseQty(grade: 2, rungIndex: 0, enhanceLevel: 0));

        // ⚠ No shipped recipe reproduces those numbers verbatim, and that is the band mechanism
        // working rather than a mismatch: `modest` is the x1.000 baseline, and the corpus's grade-2
        // plant forge (recipe.004) authors `standard` (x2.000) on both scaled legs. So the SAME
        // reference row resolves to exactly 2x, which is what makes the authored band a real
        // decision instead of decoration.
        var catalog = Catalog();
        var recipe = catalog.Recipes.Values.Single(r =>
            r.Operation == CraftOperation.Forge && r.CostLines.Any(l => l.MaterialId == "substrate.plant.sound"));
        Assert.Equal("standard", recipe.SoulsCostBand);

        var lines = catalog.Resolve(recipe.RecipeId, new RecipeContext(0, 1, 25, "plant", 0));
        Assert.Equal(160, lines.Single(l => l.Class == MaterialClass.Souls).Qty);
        Assert.Equal(16, lines.Single(l => l.MaterialId == "substrate.plant.sound").Qty);

        // The catalyst leg is authored `cheap` (x0.500) against a base of 1 — and the ceiling floor
        // holds it at 1 rather than resolving a cost to zero.
        Assert.Equal(1, lines.Single(l => l.MaterialId == "catalyst.forge").Qty);
    }

    [Fact]
    public void The_shipped_materials_display_corpus_is_measured_not_assumed()
    {
        // ⛔ Defect 3, pinned rather than silently absorbed: data/seed/items/materials/materials.json
        // authors 21 display rows for a 27-id vocabulary. The four `shard.{legacy}` rows point at ids
        // that are never minted, and the ten `shard.{rung}` ids that ARE minted have no display row
        // at all. Recorded in tasks/item-todo.md P4.1 with its owner; measured here so it cannot
        // quietly change size.
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "data", "seed", "items", "materials", "materials.json")));

        var runtimeIds = doc.RootElement.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("runtimeId").GetString()!).ToList();

        Assert.Equal(21, runtimeIds.Count);
        Assert.Equal(4, runtimeIds.Count(MaterialCatalog.IsLegacyShardId));
        Assert.Equal(0, runtimeIds.Count(id => id.StartsWith("shard.", StringComparison.Ordinal) && MaterialCatalog.IsIssuable(id)));

        // Everything that is NOT a shard row is already correct and issuable — the gap is exactly the
        // shard class, which is what makes it a re-author of ten rows rather than a corpus rebuild.
        Assert.All(runtimeIds.Where(id => !id.StartsWith("shard.", StringComparison.Ordinal)),
            id => Assert.True(MaterialCatalog.IsIssuable(id), id));
    }
}
