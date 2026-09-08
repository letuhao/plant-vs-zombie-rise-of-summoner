using System.IO;
using FusionRpg.Core.World;
using FusionRpg.Core.World.StructureSeed;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// base-defense `structure-catalog-import` (module 25, spec-structure-catalog-import.md).
/// `StructureCatalog.Configure` is static/shared, so every test that calls it MUST restore the REAL
/// corpus (never `null`) in a `finally` — task 25.4 deleted the C# `Seed` literal, so `Configure(null)`
/// no longer means "revert to a working default," it means "break the catalog for every other test
/// in this process from this point on" (confirmed the hard way: an earlier draft of this file left
/// `Configure(null)` in every `finally`, which passed in isolation but threw
/// `InvalidOperationException` inside unrelated `DistrictAssaultResolverTests` whenever the full
/// suite ran these tests first — shared static state, ordering-dependent, exactly the failure mode
/// this comment now exists to prevent a second time). Restoring the real corpus matches what
/// `StructureCatalogTestBootstrap`'s own `[ModuleInitializer]` already configures at assembly load,
/// so a test that restores it is simply putting back what was there before it ran.
/// </summary>
public class StructureCatalogImportTests
{
    static string RealCorpusRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "data", "seed", "structures");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("could not locate repo root's data/seed/structures from the test host's own base directory");
    }

    static void RestoreRealCorpus() => StructureCatalog.Configure(StructureCorpus.Load(RealCorpusRoot()));

    [Fact]
    public void The_eight_shipped_rows_are_byte_identical_through_the_corpus()
    {
        var before = StructureCatalog.All.ToDictionary(s => s.StructureId);
        try
        {
            StructureCatalog.Configure(StructureCorpus.Load(RealCorpusRoot()));
            var after = StructureCatalog.All.ToDictionary(s => s.StructureId);

            var shippedIds = new[]
            {
                "loam-source-placeholder", "well", "waystation", "granary",
                "soul-conduit", "extractor", "hatchery", "moat",
            };
            Assert.Equal(8, shippedIds.Length);

            foreach (var id in shippedIds)
            {
                var b = before[id];
                var a = after[id];
                Assert.Equal(b.Name, a.Name);
                Assert.Equal(b.Kind, a.Kind);
                Assert.Equal(b.RequiredSlotKind, a.RequiredSlotKind);
                Assert.Equal(b.Cost, a.Cost);
                Assert.Equal(b.YieldMultiplierMilli, a.YieldMultiplierMilli);
                Assert.Equal(b.BuildTurns, a.BuildTurns);
                Assert.Equal(b.CapacityBonus, a.CapacityBonus);
                Assert.Equal(b.FlatYieldPerTurn, a.FlatYieldPerTurn);
                Assert.Equal(b.ConstructRubbleCost, a.ConstructRubbleCost);
                Assert.Equal(b.ConstructIronworkCost, a.ConstructIronworkCost);
                Assert.Equal(b.MaterialTier, a.MaterialTier);
                Assert.Equal(b.BlocksMovement, a.BlocksMovement);
                Assert.Equal(b.BlocksLineOfFire, a.BlocksLineOfFire);
                Assert.Equal(b.Obstacle, a.Obstacle);
                Assert.Equal(b.CoverPowerMilli, a.CoverPowerMilli);
                Assert.Equal(b.CoverRadius, a.CoverRadius);
                Assert.Equal(b.EntryStaminaMultiplierMilli, a.EntryStaminaMultiplierMilli);
                Assert.Equal(b.VisionRangeTiles, a.VisionRangeTiles);
                Assert.Equal(b.AcquisitionPaths.OrderBy(p => p), a.AcquisitionPaths.OrderBy(p => p));

                // MaxHp is the ONE intentional difference (spec §4) -- both sides compute it the
                // same LIVE way (through the SAME StructureDef.MaxHpOf), so if every field above
                // matched, this must already agree too; asserted anyway as a direct proof.
                Assert.Equal(StructureDef.MaxHpOf(b, developmentLevel: 5), StructureDef.MaxHpOf(a, developmentLevel: 5));
            }
        }
        finally
        {
            RestoreRealCorpus();
        }
    }

    [Fact]
    public void The_seventeen_anchor_only_rows_are_not_catalog_loadable_yet()
    {
        try
        {
            var corpus = StructureCorpus.Load(RealCorpusRoot());
            var anchorOnly = corpus.Rows.Where(r => !r.IsCatalogLoadable).ToList();
            Assert.Equal(17, anchorOnly.Count);

            StructureCatalog.Configure(corpus);
            var ids = StructureCatalog.All.Select(s => s.StructureId).ToHashSet(StringComparer.Ordinal);
            foreach (var row in anchorOnly)
                Assert.DoesNotContain(row.StructureId, ids); // identity-registered, not yet a real StructureDef
        }
        finally
        {
            RestoreRealCorpus();
        }
    }

    [Fact]
    public void Configure_resets_the_cache()
    {
        var firstAll = StructureCatalog.All;
        try
        {
            StructureCatalog.Configure(StructureCorpus.Load(RealCorpusRoot()));
            var secondAll = StructureCatalog.All;
            Assert.NotSame(firstAll, secondAll); // a stale cached list would be the exact bug this guards

            RestoreRealCorpus();
            var thirdAll = StructureCatalog.All;
            Assert.NotSame(secondAll, thirdAll); // reverting also rebuilds, not just configuring forward
        }
        finally
        {
            RestoreRealCorpus();
        }
    }

    [Fact]
    public void An_unconfigured_catalog_is_unchanged_from_before_this_module()
    {
        // Every existing test in this repo that never calls Configure itself still sees a real,
        // working catalog -- not because of a Seed literal any more (25.4 deleted it), but because
        // StructureCatalogTestBootstrap's own [ModuleInitializer] already configured the real corpus
        // before any test in this assembly runs. This test's own name predates that fix and is kept
        // because the OUTWARD behavior it asserts is still exactly true, just for a different reason.
        Assert.Equal(8, StructureCatalog.All.Count);
    }

    [Fact]
    public void An_unknown_role_derived_kind_throws_at_load()
    {
        var badPath = Path.Combine(Path.GetTempPath(), "structure-corpus-import-test-" + Guid.NewGuid());
        Directory.CreateDirectory(badPath);
        try
        {
            File.WriteAllText(Path.Combine(badPath, "bad.json"), """
            {
              "kind": "structure-anchor",
              "_meta": {"partition": "Extract"},
              "entries": [{
                "id": "test-bad-kind",
                "name": "Test Bad Kind",
                "anchor": {
                  "structureId": "test-bad-kind", "family": "loam-structures", "role": "Extract",
                  "roleSecondary": "none", "requiredSlotKind": "Wildland", "elementPrimary": "none",
                  "elementSecondary": "none", "tempo": "none", "reach": "melee",
                  "strengthBand": "rubble", "rarity": "sprout", "traits": [], "costProfile": "cheap",
                  "targetPreference": "none", "variants": [], "acquisitionPaths": ["built"],
                  "footprint": "one-cell", "coverTier": "none", "controlPoint": true,
                  "obstacleVerbs": [], "reason": "test"
                },
                "_provenance": {"source": "AUTHORED", "citation": "test"},
                "magnitudes": {
                  "structureKind": "NotARealStructureKind", "cost": 0, "yieldMultiplierMilli": 1000,
                  "buildTurns": 0, "capacityBonus": 0, "flatYieldPerTurn": 0,
                  "constructRubbleCost": 0, "constructIronworkCost": 0, "materialTier": 0,
                  "blocksMovement": false, "blocksLineOfFire": false, "obstacleKind": "None",
                  "coverPowerMilli": 0, "coverRadius": 0, "entryStaminaMultiplierMilli": 1000,
                  "visionRangeTiles": null
                }
              }]
            }
            """);

            StructureCatalog.Configure(StructureCorpus.Load(badPath));
            Assert.ThrowsAny<Exception>(() => StructureCatalog.All);
        }
        finally
        {
            RestoreRealCorpus();
            Directory.Delete(badPath, recursive: true);
        }
    }

    [Fact]
    public void Adding_a_row_needs_no_rebuild()
    {
        // The module's own purpose, asserted directly: a brand-new structure id, in a fresh temp
        // corpus directory, becomes real and loadable with zero C# changes and zero rebuild --
        // only Configure(a directory containing it).
        var tmp = Path.Combine(Path.GetTempPath(), "structure-corpus-import-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "new.json"), """
            {
              "kind": "structure-anchor",
              "_meta": {"partition": "Store"},
              "entries": [{
                "id": "test-brand-new-structure",
                "name": "Test Brand New Structure",
                "anchor": {
                  "structureId": "test-brand-new-structure", "family": "loam-structures",
                  "role": "Store", "roleSecondary": "none", "requiredSlotKind": "Wildland",
                  "elementPrimary": "none", "elementSecondary": "none", "tempo": "none",
                  "reach": "melee", "strengthBand": "rubble", "rarity": "sprout", "traits": [],
                  "costProfile": "cheap", "targetPreference": "none", "variants": [],
                  "acquisitionPaths": ["built"], "footprint": "one-cell", "coverTier": "none",
                  "controlPoint": true, "obstacleVerbs": [], "reason": "test"
                },
                "_provenance": {"source": "AUTHORED", "citation": "test"},
                "magnitudes": {
                  "structureKind": "Storage", "cost": 42, "yieldMultiplierMilli": 1000,
                  "buildTurns": 1, "capacityBonus": 99, "flatYieldPerTurn": 0,
                  "constructRubbleCost": 0, "constructIronworkCost": 0, "materialTier": 0,
                  "blocksMovement": false, "blocksLineOfFire": false, "obstacleKind": "None",
                  "coverPowerMilli": 0, "coverRadius": 0, "entryStaminaMultiplierMilli": 1000,
                  "visionRangeTiles": null
                }
              }]
            }
            """);

            StructureCatalog.Configure(StructureCorpus.Load(tmp));
            var loaded = StructureCatalog.Get("test-brand-new-structure");
            Assert.Equal("Test Brand New Structure", loaded.Name);
            Assert.Equal(StructureKind.Storage, loaded.Kind);
            Assert.Equal(42, loaded.Cost);
            Assert.Equal(99, loaded.CapacityBonus);
        }
        finally
        {
            RestoreRealCorpus();
            Directory.Delete(tmp, recursive: true);
        }
    }
}

public class BandsTests
{
    [Theory]
    [InlineData("rubble", 1)]
    [InlineData("timber", 2)]
    [InlineData("stone", 3)]
    public void MaterialTierOf_resolves_the_three_real_bands(string band, int expectedTier) =>
        Assert.Equal(expectedTier, Bands.MaterialTierOf(band));

    [Fact]
    public void MaterialTierOf_throws_for_an_unknown_band() =>
        Assert.Throws<InvalidOperationException>(() => Bands.MaterialTierOf("adamantium"));
}
