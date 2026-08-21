using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

public class DemonCatalogTests
{
    [Fact]
    public void Generated_roster_meets_the_v1_floor()
    {
        var all = DemonSpeciesCatalog.All; // touching All runs Validate()
        Assert.True(all.Count >= 16, $"V1 floor is ≥16 species; got {all.Count}");
        Assert.Contains(all, s => s.ElementPrimary == ElementTypeId.Light);
        Assert.Contains(all, s => s.ElementPrimary == ElementTypeId.Dark);
        Assert.Contains(all, s => s.DeployMode == DemonDeployMode.HypnoAlly);
        Assert.Contains(all, s => s.BaseRarity == DemonRarity.Legendary);

        // Guardrails: ≤15% capture-only, never legendary.
        var captureOnly = all.Where(s => s.Acquisition == DemonAcquisition.CaptureOnly).ToList();
        Assert.True(captureOnly.Count <= Math.Max(1, all.Count * 15 / 100));
        Assert.DoesNotContain(captureOnly, s => s.BaseRarity == DemonRarity.Legendary);
    }

    static List<CapturedTypeSeed> FixedSeeds() => Enumerable.Range(0, 20)
        .Select(i => new CapturedTypeSeed("zombie", i, $"TestZombie{i}", $"Zombie {i}", 100 + i * 37))
        .ToList();

    [Fact]
    public void Generator_is_deterministic()
    {
        var a = DemonSpeciesGenerator.Generate(FixedSeeds());
        var b = DemonSpeciesGenerator.Generate(FixedSeeds());
        Assert.Equal(DemonSpeciesGenerator.EmitCSharp(a), DemonSpeciesGenerator.EmitCSharp(b));
    }

    [Fact]
    public void Generator_output_validates_and_covers_light_and_dark()
    {
        var species = DemonSpeciesGenerator.Generate(FixedSeeds());
        DemonSpeciesCatalog.Validate(species);
        Assert.Contains(species, s => s.ElementPrimary == ElementTypeId.Light);
        Assert.Contains(species, s => s.ElementPrimary == ElementTypeId.Dark);
        Assert.Equal(2, species.Count(s => s.DeployMode == DemonDeployMode.HypnoAlly));
    }

    static DemonSpeciesDef Valid() => new()
    {
        SpeciesId = "test-demon",
        Name = "Test Demon",
        Side = "zombie",
        GameTypeId = 1,
        DemonTypeId = 10001,
        ElementPrimary = ElementTypeId.Fire,
        BaseRarity = DemonRarity.Common,
        DeployMode = DemonDeployMode.PlantAvatar,
        Acquisition = DemonAcquisition.Summonable,
        Variants = new[] { "normal" },
        TraitPool = new[] { "swift" }
    };

    [Fact]
    public void Validation_rejects_bad_species()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid(), Valid() })); // duplicate id
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { DemonTypeId = 5 } })); // below floor
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { Acquisition = DemonAcquisition.None } }));
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { TraitPool = new[] { "nonsense" } } }));
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { ElementSecondary = ElementTypeId.Fire } }));
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { Variants = new[] { "holographic" } } }));
        Assert.Throws<InvalidOperationException>(() =>
            DemonSpeciesCatalog.Validate(new[] { Valid() with { Side = "mower" } }));
    }

    [Fact]
    public void Generator_invariants_hold_across_many_input_shapes()
    {
        // Property-style regression for the ensure-pass bug (review I2): whatever the hash
        // distribution of the input, the roster must end with ≥1 light AND ≥1 dark primary,
        // never primary==secondary, and always validate.
        for (var offset = 0; offset < 20; offset++)
        {
            var seeds = Enumerable.Range(0, 18)
                .Select(i => new CapturedTypeSeed("zombie", offset * 137 + i, $"Z{offset}_{i}", null, 90 + i * 13))
                .ToList();
            var roster = DemonSpeciesGenerator.Generate(seeds);
            DemonSpeciesCatalog.Validate(roster);
            Assert.Contains(roster, s => s.ElementPrimary == ElementTypeId.Light);
            Assert.Contains(roster, s => s.ElementPrimary == ElementTypeId.Dark);
            Assert.DoesNotContain(roster, s => s.ElementSecondary == s.ElementPrimary);
        }
    }

    [Fact]
    public void Demon_type_ids_never_collide_across_sides()
    {
        // Review S5: zombie 10000+t vs plant 60000+t — a mixed roster with overlapping raw
        // type ids must stay collision-free.
        var seeds = Enumerable.Range(0, 10)
            .SelectMany(i => new[]
            {
                new CapturedTypeSeed("zombie", i, $"Zed{i}", null, 200 + i),
                new CapturedTypeSeed("plant", i, $"Plant{i}", null, 100 + i)
            })
            .ToList();
        var roster = DemonSpeciesGenerator.Generate(seeds);
        Assert.Equal(roster.Count, roster.Select(s => s.DemonTypeId).Distinct().Count());
    }

    [Fact]
    public void Trait_catalog_ids_are_stable_kebab_and_unique()
    {
        Assert.Equal(DemonTraitCatalog.All.Count, DemonTraitCatalog.All.Select(t => t.TraitId).Distinct().Count());
        foreach (var t in DemonTraitCatalog.All)
        {
            Assert.Equal(t.TraitId, t.TraitId.Trim().ToLowerInvariant());
            Assert.False(string.IsNullOrWhiteSpace(t.GrantTemplateId));
        }
    }
}
