using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Fusion;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>F2: the recipe catalog — deterministic, exhaustive over summonable rare+ species,
/// band-below inputs, capture-only species appearing nowhere.</summary>
public class DemonRecipeCatalogTests
{
    static DemonRarity BandBelow(DemonRarity r) => (DemonRarity)((int)r - 1);

    [Fact]
    public void Every_summonable_rare_plus_species_has_exactly_one_recipe()
    {
        var eligible = DemonSpeciesCatalog.All
            .Where(s => s.BaseRarity >= DemonRarity.Rare && s.Acquisition != DemonAcquisition.CaptureOnly)
            .Select(s => s.SpeciesId)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var outputs = DemonRecipeCatalog.All
            .Select(r => r.OutputSpeciesId)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(eligible, outputs);
        Assert.Contains(DemonRecipeCatalog.All, r =>
            DemonSpeciesCatalog.Get(r.OutputSpeciesId).BaseRarity == DemonRarity.Legendary);
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
            Assert.Equal(BandBelow(output.BaseRarity), a.BaseRarity);
            Assert.Equal(BandBelow(output.BaseRarity), b.BaseRarity);
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
