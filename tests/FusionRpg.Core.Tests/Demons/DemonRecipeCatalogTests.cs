using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>F2: the recipe catalog — deterministic, exhaustive over summonable rare+ species,
/// band-below inputs, capture-only species appearing nowhere.</summary>
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
        var again = DemonRecipeCatalog.BuildForTest();
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
}
