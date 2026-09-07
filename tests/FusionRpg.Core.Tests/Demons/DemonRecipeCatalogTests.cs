using System.Text.Json;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Tests.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>F2: the recipe catalog — deterministic, exhaustive over summonable rare+ species,
/// band-below inputs, capture-only species appearing nowhere. `All` reads whatever
/// `ContractTuningTestBootstrap` configured (T8.4: `DemonRecipeCatalog.Configure(BuildDeterministicOnly())`
/// against the compiled default species roster, zero shortfall there) — every test below reads
/// that SAME data exactly as it did before the `Configure` seam existed; only
/// `Catalog_is_deterministic_and_ids_are_stable`'s own `BuildForTest()` call needed renaming to
/// `BuildDeterministicOnly()`. New tests cover the seam itself: `Configure`'s validation (rejects
/// empty/duplicate/self-paired/cross-claimed/unknown/`CaptureOnly` recipes, every rejection
/// happening BEFORE any process-wide state mutation so these tests can never corrupt the shared
/// bootstrap roster other tests in this assembly depend on), `UseScoped`'s isolation, and the T8.4
/// diff test against the real corpus.</summary>
public class DemonRecipeCatalogTests
{
    /// <summary>Mirrors `DemonRecipeCatalog.InputPoolBelow`'s walk-down search: the nearest
    /// POPULATED rung below `r`, not necessarily the rung exactly one below. Today's catalog only
    /// populates Chaff/Cultivated/Heirloom/Sunwoven (seed-to-concrete T4.1's mechanical remap), so
    /// e.g. Cultivated's own "one rung below" (Grafted) is empty and the real search continues
    /// down to Chaff — a bare `(DemonRarity)((int)r - 1)` cast here would assert the wrong thing
    /// and is exactly landmine class 1 this migration's own guard test forbids in `src/`.</summary>
    static DemonRarity NearestPopulatedBandBelow(DemonRarity r)
    {
        var cursor = r;
        while (!DemonRarityLadder.IsBottomRung(cursor))
        {
            cursor = DemonRarityLadder.OneRungBelow(cursor);
            if (DemonSpeciesCatalog.All.Any(s => s.BaseRarity == cursor))
                return cursor;
        }
        return DemonRarity.Chaff;
    }

    /// <summary>spec-rarity-migration.md §3: "Rare or better" meant three quarters of the old
    /// four-rung ladder; naively widening the SAME comparison to ten rungs would silently grow the
    /// fusion-output-eligible set from ~75% of the roster to ~90%, with no compiler error and no test
    /// failure (both expressions are valid at both widths). This pins the floor to a NAMED rung
    /// (Cultivated), not a ratio recomputed from <see cref="DemonRarityLadder.RungCount"/>, so a
    /// future ladder widening cannot silently re-expand the eligible proportion again.</summary>
    [Fact]
    public void Fusion_output_set_is_pinned_by_rung_not_by_proportion()
    {
        Assert.Equal(DemonRarity.Cultivated, DemonRecipeCatalog.OutputEligibilityFloor);
        // A fixed ordinal (3), not a proportion of RungCount — proves the floor is a pinned rung.
        Assert.Equal(3, (int)DemonRecipeCatalog.OutputEligibilityFloor);

        var eligible = DemonSpeciesCatalog.All.Count(s =>
            DemonRarityLadder.AtLeast(s.BaseRarity, DemonRecipeCatalog.OutputEligibilityFloor) &&
            s.Acquisition != DemonAcquisition.CaptureOnly);
        Assert.Equal(eligible, DemonRecipeCatalog.All.Count);
    }

    [Fact]
    public void Every_summonable_rare_plus_species_has_exactly_one_recipe()
    {
        var eligible = DemonSpeciesCatalog.All
            .Where(s => s.BaseRarity >= DemonRarity.Cultivated && s.Acquisition != DemonAcquisition.CaptureOnly)
            .Select(s => s.SpeciesId)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var outputs = DemonRecipeCatalog.All
            .Select(r => r.OutputSpeciesId)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(eligible, outputs);
        Assert.Contains(DemonRecipeCatalog.All, r =>
            DemonSpeciesCatalog.Get(r.OutputSpeciesId).BaseRarity == DemonRarity.Sunwoven);
    }

    [Fact]
    public void Inputs_are_distinct_band_below_and_never_capture_only()
    {
        foreach (var recipe in DemonRecipeCatalog.All)
        {
            var output = DemonSpeciesCatalog.Get(recipe.OutputSpeciesId);
            var a = DemonSpeciesCatalog.Get(recipe.InputSpeciesIdA);
            var b = DemonSpeciesCatalog.Get(recipe.InputSpeciesIdB);
            Assert.NotEqual(recipe.InputSpeciesIdA, recipe.InputSpeciesIdB);
            Assert.Equal(NearestPopulatedBandBelow(output.BaseRarity), a.BaseRarity);
            Assert.Equal(NearestPopulatedBandBelow(output.BaseRarity), b.BaseRarity);
            Assert.NotEqual(DemonAcquisition.CaptureOnly, a.Acquisition);
            Assert.NotEqual(DemonAcquisition.CaptureOnly, b.Acquisition);
        }

        // Orderless input pairs must be unique — TryMatch would otherwise be ambiguous.
        var pairs = DemonRecipeCatalog.All
            .Select(r => string.Join("+", new[] { r.InputSpeciesIdA, r.InputSpeciesIdB }
                .OrderBy(x => x, StringComparer.Ordinal)))
            .ToList();
        Assert.Equal(pairs.Count, pairs.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalog_is_deterministic_and_ids_are_stable()
    {
        var again = DemonRecipeCatalog.BuildDeterministicOnly();
        Assert.Equal(
            DemonRecipeCatalog.All.Select(r => $"{r.RecipeId}|{r.InputSpeciesIdA}|{r.InputSpeciesIdB}"),
            again.Select(r => $"{r.RecipeId}|{r.InputSpeciesIdA}|{r.InputSpeciesIdB}"));
        Assert.All(DemonRecipeCatalog.All, r => Assert.Equal("recipe." + r.OutputSpeciesId, r.RecipeId));
    }

    [Fact]
    public void Lookups_follow_catalog_discipline()
    {
        var first = DemonRecipeCatalog.All[0];
        Assert.True(DemonRecipeCatalog.IsKnown(first.RecipeId));
        Assert.Same(first, DemonRecipeCatalog.Get(first.RecipeId));
        Assert.False(DemonRecipeCatalog.IsKnown("recipe.no-such"));
        Assert.Throws<ArgumentException>(() => DemonRecipeCatalog.Get("recipe.no-such"));
    }

    [Fact]
    public void Recipes_can_be_found_by_their_input_pair()
    {
        var recipe = DemonRecipeCatalog.All[0];
        Assert.Same(recipe, DemonRecipeCatalog.TryMatch(recipe.InputSpeciesIdA, recipe.InputSpeciesIdB));
        Assert.Same(recipe, DemonRecipeCatalog.TryMatch(recipe.InputSpeciesIdB, recipe.InputSpeciesIdA)); // orderless
        Assert.Null(DemonRecipeCatalog.TryMatch(recipe.InputSpeciesIdA, recipe.InputSpeciesIdA));
    }

    // ==============================================================================================
    // T8.4 (`ds 18` `fusion-recipe-runtime`, spec-fusion-recipe-runtime.md §1-2) — the Configure seam.
    //
    // Every `Configure` call below is deliberately built so its OWN rejection fires before
    // `_configured` is ever reassigned (Configure's empty-check throws first; every other rejection
    // throws from inside `Validate(recipes)`, whose result never reaches the assignment when it
    // throws) — so these tests can never corrupt the process-wide bootstrap roster every OTHER test
    // in this class (and this whole assembly) depends on. `UseScoped` tests use its own async-local
    // isolation instead, restored automatically on `Dispose`.
    // ==============================================================================================

    [Fact]
    public void Configure_throws_a_named_message_on_an_empty_recipe_list()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(Array.Empty<DemonRecipeDef>()));
        Assert.Contains("reconcile.py", ex.Message); // names the fix command, per T8.4's own acceptance criterion
    }

    [Fact]
    public void Configure_rejects_a_duplicate_recipe_id()
    {
        var real = DemonRecipeCatalog.All[0];
        var duplicateId = new DemonRecipeDef(real.RecipeId, "bogus-output-dup-id", "bogus-input-a", "bogus-input-b");
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { real, duplicateId }));
        Assert.Contains("Duplicate fusion recipe id", ex.Message);
    }

    [Fact]
    public void Configure_rejects_the_same_species_as_both_inputs()
    {
        var real = DemonRecipeCatalog.All[0];
        var selfPaired = real with { RecipeId = "recipe.self-paired-test", OutputSpeciesId = "bogus-output-self", InputSpeciesIdB = real.InputSpeciesIdA };
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { selfPaired }));
        Assert.Contains("same species as both inputs", ex.Message);
    }

    [Fact]
    public void Configure_rejects_a_pair_already_claimed_by_another_recipe()
    {
        var real = DemonRecipeCatalog.All[0];
        var samePairDifferentOutput = real with { RecipeId = "recipe.dup-pair-test", OutputSpeciesId = "bogus-output-dup-pair" };
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { real, samePairDifferentOutput }));
        Assert.Contains("already claimed by another recipe", ex.Message);
    }

    [Fact]
    public void Configure_rejects_an_unknown_output_species()
    {
        var bogus = new DemonRecipeDef("recipe.bogus-output-test", "no-such-species-xyz", "bogus-input-a", "bogus-input-b");
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { bogus }));
        Assert.Contains("outputs unknown species", ex.Message);
    }

    [Fact]
    public void Configure_rejects_an_unknown_input_species()
    {
        var realOutput = DemonRecipeCatalog.All[0].OutputSpeciesId;
        var bogus = new DemonRecipeDef("recipe.bogus-input-test", realOutput, "no-such-input-species", "bogus-input-b");
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { bogus }));
        Assert.Contains("is not a known species", ex.Message);
    }

    [Fact]
    public void Configure_rejects_a_capture_only_input_species_owner_lock_6()
    {
        var realOutput = DemonRecipeCatalog.All[0].OutputSpeciesId;
        var captureOnly = DemonSpeciesCatalog.All.First(s => s.Acquisition == DemonAcquisition.CaptureOnly).SpeciesId;
        var otherInput = DemonSpeciesCatalog.All.First(s => s.Acquisition != DemonAcquisition.CaptureOnly && s.SpeciesId != captureOnly).SpeciesId;
        var bogus = new DemonRecipeDef("recipe.capture-only-test", realOutput, captureOnly, otherInput);
        var ex = Assert.Throws<InvalidOperationException>(() => DemonRecipeCatalog.Configure(new[] { bogus }));
        Assert.Contains("CaptureOnly", ex.Message);
    }

    [Fact]
    public void UseScoped_swaps_the_recipe_list_for_this_async_context_and_restores_on_dispose()
    {
        var real = DemonRecipeCatalog.All[0];
        var scopedList = new[] { real with { RecipeId = "recipe.scoped-test-only" } };

        using (DemonRecipeCatalog.UseScoped(scopedList))
        {
            Assert.Single(DemonRecipeCatalog.All);
            Assert.True(DemonRecipeCatalog.IsKnown("recipe.scoped-test-only"));
        }

        // Restored to the process-wide (bootstrap) roster outside the scope — proves UseScoped's
        // own isolation, the same property SpeciesCatalogDiffTests already pins for DemonSpeciesCatalog.
        Assert.True(DemonRecipeCatalog.All.Count > 1);
        Assert.False(DemonRecipeCatalog.IsKnown("recipe.scoped-test-only"));
    }

    /// <summary>
    /// T8.4's own named acceptance criterion: a store-backed (here: seed-file-round-tripped) recipe
    /// list must match a fresh `BuildDeterministicOnly()` byte-for-byte for every NON-gap-fill
    /// output. Built against the real ~829-species corpus rather than the small compiled default —
    /// this is the scenario the real committed seed (module 18, not yet generated — Checkpoint 8a
    /// is owner-run) will actually face: hundreds of deterministic recipes plus a handful of
    /// gap-fills the deterministic pass could never itself produce.
    /// </summary>
    [Fact]
    public void The_loaded_catalog_matches_a_fresh_deterministic_build_for_every_non_gap_fill_recipe()
    {
        using (DemonSpeciesCatalog.UseScoped(RealCorpusFixture.Snapshot))
        {
            var deterministic = DemonRecipeCatalog.BuildDeterministicOnly();
            Assert.True(deterministic.Count > 600); // real number today is 695 (T8.3) — sanity floor, not the literal

            // Round-trip through the EXACT committed JSON shape (spec §4), proving the reader too —
            // not just comparing in-memory records to themselves.
            var seedObject = deterministic.ToDictionary(
                r => r.RecipeId,
                object (r) => new { outputSpeciesId = r.OutputSpeciesId, inputSpeciesIdA = r.InputSpeciesIdA, inputSpeciesIdB = r.InputSpeciesIdB, crossRungGapFill = false });

            // One synthetic gap-fill entry standing in for what a real reconcile run against the
            // corpus's own real deficits would add (T8.3: 14 real Almanac outputs, "jacksonzombie"
            // among them, have NO deterministic recipe at all) — proves the diff logic below
            // correctly leaves a gap-fill entry out of the "must match a fresh build" comparison
            // rather than needing every LOADED entry to be reproducible.
            var deficitOutput = DemonRecipeCatalog.UnresolvedOutputs().First(o => o.SpeciesId == "jacksonzombie");
            // A genuinely CROSS-rung pair (one candidate from the nearest populated rung, one from
            // the next populated rung down) — never same-rung, because every possible same-rung
            // pair among the nearest rung's own small population is already claimed by the
            // deterministic pass itself (that exhaustion is exactly WHY this output is a deficit at
            // all), so a same-rung pick here would collide with an existing recipe by construction.
            var candidatePool = DemonRecipeCatalog.CandidatePoolBelow(deficitOutput.BaseRarity, maxPopulatedRungs: 2);
            var inputA = candidatePool.First(c => c.RungDistance == 1).Species.SpeciesId;
            var inputB = candidatePool.First(c => c.RungDistance == 2).Species.SpeciesId;
            seedObject["recipe.jacksonzombie"] = new { outputSpeciesId = deficitOutput.SpeciesId, inputSpeciesIdA = inputA, inputSpeciesIdB = inputB, crossRungGapFill = true };

            var loaded = FusionRecipeSeedReader.Parse(JsonSerializer.Serialize(seedObject));
            Assert.Equal(deterministic.Count + 1, loaded.Count); // every deterministic recipe, plus the one gap-fill

            using (DemonRecipeCatalog.UseScoped(loaded))
            {
                var loadedById = DemonRecipeCatalog.All.ToDictionary(r => r.RecipeId, StringComparer.Ordinal);
                var freshById = DemonRecipeCatalog.BuildDeterministicOnly().ToDictionary(r => r.RecipeId, StringComparer.Ordinal);

                Assert.Equal(freshById.Count, deterministic.Count); // the fresh build is stable across the two calls in this test
                foreach (var (recipeId, freshRecipe) in freshById)
                {
                    var loadedRecipe = loadedById[recipeId];
                    Assert.False(loadedRecipe.CrossRungGapFill); // every key iterated here came from the deterministic pass itself
                    Assert.Equal(freshRecipe.OutputSpeciesId, loadedRecipe.OutputSpeciesId);
                    Assert.Equal(freshRecipe.InputSpeciesIdA, loadedRecipe.InputSpeciesIdA);
                    Assert.Equal(freshRecipe.InputSpeciesIdB, loadedRecipe.InputSpeciesIdB);
                }

                // The gap-fill entry is real, loaded, and correctly flagged — just never required to
                // match a deterministic build that (by definition) never produces it.
                Assert.True(loadedById["recipe.jacksonzombie"].CrossRungGapFill);
                Assert.DoesNotContain("jacksonzombie", freshById.Values.Select(r => r.OutputSpeciesId));

                // A gap-fill recipe is reachable through the SAME All/Get/IsKnown/TryMatch api as
                // every deterministic one — no second surface for gap-fills (spec-fusion-recipe-runtime.md
                // §Testing strategy, `crossRungGapFill_recipes_are_reachable_through_the_same_All_Get_TryMatch_api`).
                Assert.True(DemonRecipeCatalog.IsKnown("recipe.jacksonzombie"));
                Assert.Same(loadedById["recipe.jacksonzombie"], DemonRecipeCatalog.Get("recipe.jacksonzombie"));
                Assert.Same(loadedById["recipe.jacksonzombie"], DemonRecipeCatalog.TryMatch(inputA, inputB));
                Assert.Same(loadedById["recipe.jacksonzombie"], DemonRecipeCatalog.TryMatch(inputB, inputA)); // orderless
            }
        }
    }

    // ==============================================================================================
    // T8.4 — FusionRecipeSeedReader: the pure parser `Configure`/`UseScoped` load a committed seed
    // through. Mirrors `SpeciesBuildPlanReader`'s own discipline (no file I/O, explicit rejections).
    // ==============================================================================================

    [Fact]
    public void FusionRecipeSeedReader_rejects_an_empty_document()
    {
        Assert.Throws<FusionRecipeSeedRejection>(() => FusionRecipeSeedReader.Parse(""));
    }

    [Fact]
    public void FusionRecipeSeedReader_rejects_invalid_json()
    {
        Assert.Throws<FusionRecipeSeedRejection>(() => FusionRecipeSeedReader.Parse("{ not valid json"));
    }

    [Fact]
    public void FusionRecipeSeedReader_rejects_a_non_object_top_level_value()
    {
        Assert.Throws<FusionRecipeSeedRejection>(() => FusionRecipeSeedReader.Parse("[1,2,3]"));
    }

    [Fact]
    public void FusionRecipeSeedReader_rejects_an_entry_missing_a_required_field()
    {
        var ex = Assert.Throws<FusionRecipeSeedRejection>(() => FusionRecipeSeedReader.Parse(
            "{\"recipe.x\": {\"outputSpeciesId\": \"x\", \"inputSpeciesIdA\": \"a\"}}")); // missing inputSpeciesIdB and crossRungGapFill
        Assert.Contains("recipe.x", ex.Message);
    }

    [Fact]
    public void FusionRecipeSeedReader_rejects_a_wrong_typed_field()
    {
        Assert.Throws<FusionRecipeSeedRejection>(() => FusionRecipeSeedReader.Parse(
            "{\"recipe.x\": {\"outputSpeciesId\": 123, \"inputSpeciesIdA\": \"a\", \"inputSpeciesIdB\": \"b\", \"crossRungGapFill\": false}}"));
    }

    [Fact]
    public void FusionRecipeSeedReader_parses_the_exact_committed_shape_including_crossRungGapFill()
    {
        var parsed = FusionRecipeSeedReader.Parse(
            "{\"recipe.jacksonzombie\": {\"outputSpeciesId\": \"jacksonzombie\", \"inputSpeciesIdA\": \"a\", " +
            "\"inputSpeciesIdB\": \"b\", \"crossRungGapFill\": true, " +
            "\"provenance\": {\"corpusContentHash\": \"sha256:whatever\", \"promptVersion\": 1}}}"); // provenance silently ignored at read time

        var recipe = Assert.Single(parsed);
        Assert.Equal("recipe.jacksonzombie", recipe.RecipeId);
        Assert.Equal("jacksonzombie", recipe.OutputSpeciesId);
        Assert.Equal("a", recipe.InputSpeciesIdA);
        Assert.Equal("b", recipe.InputSpeciesIdB);
        Assert.True(recipe.CrossRungGapFill);
    }

    // ==============================================================================================
    // T8.5 (`ds 18` §3 step 3) — the diff test against the REAL committed file on disk, not an
    // in-memory round-trip. Scoped to DemonSpeciesCatalog from the exact real corpus
    // (RealCorpusFixture) the committed file was generated against on 2026-09-06, never the live
    // database, per spec-fusion-recipe-runtime.md §3 step 3's own binding precondition.
    // ==============================================================================================

    static string RepoRootForCommittedSeed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    [Fact]
    public void The_real_committed_seed_matches_a_fresh_deterministic_build_for_every_output()
    {
        var path = Path.Combine(RepoRootForCommittedSeed(), "data", "generated", "demons", "_fusion-recipes.json");
        Assert.True(File.Exists(path), $"missing committed seed at {path} — run `python tools/seedsmith/seedsmith/adapters/demons/fusion/reconcile.py --deterministic-only`");

        var committed = FusionRecipeSeedReader.Parse(File.ReadAllText(path));
        Assert.NotEmpty(committed);

        using (DemonSpeciesCatalog.UseScoped(RealCorpusFixture.Snapshot))
        {
            var fresh = DemonRecipeCatalog.BuildDeterministicOnly();
            var freshById = fresh.ToDictionary(r => r.RecipeId, StringComparer.Ordinal);
            var committedById = committed.ToDictionary(r => r.RecipeId, StringComparer.Ordinal);

            // Every non-gap-fill entry must match a fresh deterministic build byte-for-byte — the
            // diff test's own binding claim (spec §3 step 3). The committed file also carries real
            // gap-fill entries (Checkpoint 8a's own reasoned pass, 2026-09-06 — 14 real Almanac
            // deficits, resolved via Claude's own proposals run through the unmodified reconcile
            // pipeline, not a live LM Studio model this environment cannot reach) — those are
            // correctly EXCLUDED from this comparison, never required to match a method that by
            // definition cannot produce them.
            var nonGapFill = committedById.Values.Where(r => !r.CrossRungGapFill).ToList();
            var gapFill = committedById.Values.Where(r => r.CrossRungGapFill).ToList();

            Assert.Equal(freshById.Count, nonGapFill.Count);
            foreach (var (recipeId, freshRecipe) in freshById)
            {
                Assert.True(committedById.ContainsKey(recipeId), $"committed seed is missing '{recipeId}', which a fresh deterministic build still produces");
                var committedRecipe = committedById[recipeId];
                Assert.False(committedRecipe.CrossRungGapFill);
                Assert.Equal(freshRecipe.OutputSpeciesId, committedRecipe.OutputSpeciesId);
                Assert.Equal(freshRecipe.InputSpeciesIdA, committedRecipe.InputSpeciesIdA);
                Assert.Equal(freshRecipe.InputSpeciesIdB, committedRecipe.InputSpeciesIdB);
            }

            // Every gap-fill targets a real, known deficit — the deterministic build still cannot
            // produce it (that is exactly why it needed a gap-fill), and it is not already present
            // among the non-gap-fill entries (no output has two recipes).
            var deficitIds = DemonRecipeCatalog.UnresolvedOutputs().Select(o => o.SpeciesId).ToHashSet(StringComparer.Ordinal);
            foreach (var gapFillRecipe in gapFill)
            {
                Assert.Contains(gapFillRecipe.OutputSpeciesId, deficitIds);
                Assert.DoesNotContain(gapFillRecipe.OutputSpeciesId, freshById.Values.Select(r => r.OutputSpeciesId));
            }
        }
    }

    [Fact]
    public void The_real_committed_seed_honestly_ships_zero_gap_fills_pending_a_fresh_vote()
    {
        // 2026-09-07: T2.11's own full classification run completed (840 -> 903 species), which grew
        // EVERY existing Almanac deficit's own candidate pool (more Sunwoven/Firstseed/Heirloom
        // species now populate the nearest-2-3-rungs-below window) — the reconciler's own §3a
        // freeze-on-commit correctly invalidated all 14 previously-resolved gap-fills (their
        // corpusContentHash no longer matches a changed pool) rather than carrying stale picks
        // forward. Re-run in `--deterministic-only` mode deliberately (no live-model vote authorized
        // for this specific action, distinct from the species-classification run) — 0 gap-fills is
        // the correct, honest result today, not a regression: `reconcile.py`'s own "no entry, not a
        // made-up one" rule for real. Closing this gap for real needs a fresh Checkpoint-8a-style
        // live-model vote against the new, larger candidate pools — tracked, not silently skipped.
        var path = Path.Combine(RepoRootForCommittedSeed(), "data", "generated", "demons", "_fusion-recipes.json");
        var committed = FusionRecipeSeedReader.Parse(File.ReadAllText(path));

        using (DemonSpeciesCatalog.UseScoped(RealCorpusFixture.Snapshot))
        {
            var deficits = DemonRecipeCatalog.UnresolvedOutputs();
            Assert.Equal(16, deficits.Count); // today's real, live-verified number (T8.1/T8.3)

            var gapFillByOutput = committed.Where(r => r.CrossRungGapFill).ToDictionary(r => r.OutputSpeciesId, StringComparer.Ordinal);
            Assert.Empty(gapFillByOutput); // honestly zero — see the comment above for why
            foreach (var deficit in deficits)
                Assert.DoesNotContain(deficit.SpeciesId, committed.Select(r => r.OutputSpeciesId));

            Assert.Equal(775 - 16, committed.Count); // every eligible output minus every named deficit
        }
    }

    // NOTE: "a missing seed file refuses at load, naming the reconcile command" is Program.cs's own
    // 3-line `if (!File.Exists(...)) throw` block (Core deliberately does no file I/O of its own,
    // tunables-ssot.md §7.2 — FusionRecipeSeedReader stays a pure parser, so this exact check has no
    // testable seam short of either violating that boundary or standing up the real host, which
    // e2e-tests-dungeon-registry-broken.md already tracks as independently, pre-existingly broken
    // for an unrelated reason). Verified by direct code reading instead: see the `if (!File.Exists`
    // block immediately before the `DemonRecipeCatalog.Configure` call in Program.cs.
}
