using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// `catalog-runtime`'s Injector-side flip (2026-09-06) — <see cref="ConcreteSpeciesSeedReader"/> is
/// the read half of <see cref="ConcreteSpeciesSerializer.Canonical"/>, proven two ways: a direct
/// parse-shape test against a hand-built fixture (every field, both element slots, both
/// present/absent), and a real end-to-end diff against the Server's own SQL-backed roster for the
/// FULL real committed tree — the same <see cref="SpeciesDiff"/> mechanism
/// <c>SpeciesCatalogDiffTests</c> already established, run here at the real 829-species scale rather
/// than two hand-picked anchors.
/// </summary>
public class ConcreteSpeciesSeedReaderTests
{
    const string FullFixture = """
        {
          "speciesId": "TestSpecies",
          "rarity": "Cultivated",
          "theta": 13,
          "pTheta": 452,
          "attackIntervalMs": 1500,
          "attackIntervalSource": "classified",
          "rangeCells": 5,
          "variantCount": 2,
          "side": "plant",
          "gameTypeId": 7,
          "elementPrimary": "Earth",
          "elementSecondary": "Fire",
          "deployMode": "PlantAvatar",
          "acquisition": "Summonable",
          "variants": ["mutated", "normal"],
          "traitPool": ["defensive-line", "projectile-launching"],
          "magnitudes": {
            "resource.max.hp": 2712,
            "resource.regen.hp": 136
          }
        }
        """;

    [Fact]
    public void Every_field_the_writer_serialises_round_trips_through_the_reader()
    {
        var s = ConcreteSpeciesSeedReader.Parse(FullFixture);

        Assert.Equal("TestSpecies", s.SpeciesId);
        Assert.Equal(Core.Demons.DemonRarity.Cultivated, s.Rarity);
        Assert.Equal(13, s.Theta);
        Assert.Equal(452, s.PTheta);
        Assert.Equal(1500, s.AttackIntervalMs);
        Assert.Equal("classified", s.AttackIntervalSource);
        Assert.Equal(5, s.RangeCells);
        Assert.Equal(2, s.VariantCount);
        Assert.Equal("plant", s.Side);
        Assert.Equal(7, s.GameTypeId);
        Assert.Equal(ElementTypeId.Earth, s.ElementPrimary);
        Assert.Equal(ElementTypeId.Fire, s.ElementSecondary);
        Assert.Equal(Core.Demons.DemonDeployMode.PlantAvatar, s.DeployMode);
        Assert.Equal(Core.Demons.DemonAcquisition.Summonable, s.Acquisition);
        Assert.Equal(new[] { "mutated", "normal" }, s.Variants);
        Assert.Equal(new[] { "defensive-line", "projectile-launching" }, s.TraitPool);
        Assert.Null(s.Name); // never written by the serialiser — see the class doc
        Assert.Equal(2712, s.Magnitudes["resource.max.hp"]);
        Assert.Equal(136, s.Magnitudes["resource.regen.hp"]);
    }

    [Fact]
    public void ToDemonSpeciesDef_carries_Magnitudes_through_not_just_the_reader_side()
    {
        // demon-lawn-deploy T1.5: unlike this file's own read-side round-trip above,
        // ConcreteSpeciesMapper.ToDemonSpeciesDef is the SEPARATE step that turns a parsed
        // ConcreteSpecies into the LIVE DemonSpeciesDef DemonSpeciesCatalog.Get actually serves —
        // BuildDemonSpeciesSnapshot's own diff test compares two calls of this SAME method against each
        // other, so it can never catch a bug in the method's own field-copy; this proves that directly.
        var concrete = ConcreteSpeciesSeedReader.Parse(FullFixture);
        var def = ConcreteSpeciesMapper.ToDemonSpeciesDef(concrete);

        Assert.Equal(2712, def.Magnitudes["resource.max.hp"]);
        Assert.Equal(136, def.Magnitudes["resource.regen.hp"]);
        Assert.Equal(concrete.Magnitudes.Count, def.Magnitudes.Count);
    }

    [Fact]
    public void A_null_secondary_element_parses_to_null_not_an_exception()
    {
        var json = FullFixture.Replace("\"elementSecondary\": \"Fire\"", "\"elementSecondary\": null");
        var s = ConcreteSpeciesSeedReader.Parse(json);
        Assert.Null(s.ElementSecondary);
    }

    [Fact]
    public void A_combined_flags_acquisition_value_round_trips_through_Enum_Parse()
    {
        // Acquisition is [Flags] — ToString() on a combined value comma-joins ("Summonable,
        // CaptureOnly"), and Enum.Parse must accept that same shape back, not just a single value.
        var json = FullFixture.Replace("\"Summonable\"", "\"Summonable, CaptureOnly\"");
        var s = ConcreteSpeciesSeedReader.Parse(json);
        Assert.True(s.Acquisition.HasFlag(Core.Demons.DemonAcquisition.Summonable));
        Assert.True(s.Acquisition.HasFlag(Core.Demons.DemonAcquisition.CaptureOnly));
    }

    [Fact]
    public void A_missing_required_field_refuses_with_the_fields_own_name_not_a_generic_error()
    {
        var json = FullFixture.Replace("\"speciesId\": \"TestSpecies\",", "");
        var ex = Assert.Throws<FormatException>(() => ConcreteSpeciesSeedReader.Parse(json));
        Assert.Contains("speciesId", ex.Message, StringComparison.Ordinal);
    }

    // ---- the real end-to-end proof: every one of the 829 real committed files, mapped two ways ------

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static IReadOnlyList<Core.Demons.Generation.ConcreteSpecies> RealCommittedSpecies()
    {
        var dir = Path.Combine(RepoRoot(), "data", "generated", "demons");
        var files = Directory.EnumerateFiles(dir, "*.json")
            .Where(p => !Path.GetFileName(p).StartsWith('_')) // skip _species-build-plan.json, _fusion-recipes.json
            .ToList();
        Assert.True(files.Count > 800, $"expected the real ~829-species tree, found {files.Count} files under {dir}");
        return files.Select(ConcreteSpeciesSeedReader.ParseFile).ToList();
    }

    [Fact]
    public void Every_real_committed_species_file_parses_without_throwing()
    {
        var species = RealCommittedSpecies();
        Assert.True(species.Count > 800);
    }

    [Fact]
    public void The_reader_plus_mapper_path_matches_the_real_store_backed_roster_for_every_real_species()
    {
        var concrete = RealCommittedSpecies();

        var dir = Path.Combine(Path.GetTempPath(), "fusionrpg-injector-flip-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new FusionRpg.Data.RpgStore(dir);
            store.Init();
            var outcome = store.ImportSpecies(concrete);
            Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

            var serverRoster = store.BuildDemonSpeciesSnapshot();
            var injectorRoster = concrete.Select(ConcreteSpeciesMapper.ToDemonSpeciesDef).ToList();

            Assert.Equal(serverRoster.Count, injectorRoster.Count);

            var diffs = Core.Demons.Generation.SpeciesDiff.Compare(serverRoster, injectorRoster);
            // "name" is the one documented, expected divergence: BuildDemonSpeciesSnapshot's own
            // ConcreteSpecies.Name comes from a database join (almanac_seed) this reader never
            // performs — both sides fall back to the raw SpeciesId identically when Name is null,
            // so a real divergence here would mean the Server actually resolved a DIFFERENT display
            // name for some species, a fact worth knowing, not a bug in either mapping.
            var nonNameDiffs = diffs.Where(d => d.Field != "name").ToList();
            Assert.Empty(nonNameDiffs);

            var (onlyServer, onlyInjector) = Core.Demons.Generation.SpeciesDiff.Coverage(serverRoster, injectorRoster);
            Assert.Empty(onlyServer);
            Assert.Empty(onlyInjector);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* temp dir */ }
        }
    }
}
