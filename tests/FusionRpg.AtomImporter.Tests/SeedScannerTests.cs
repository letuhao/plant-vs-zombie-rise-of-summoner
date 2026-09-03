using FusionRpg.Data.Seed;
using Xunit;

namespace FusionRpg.AtomImporter.Tests;

/// <summary>
/// Which files an import sweeps (E14a).
///
/// <para>The headline case is the collision that is already in the tree: <c>data/seed/items/</c>
/// holds ~125 files in a different format read by <c>tools/ItemSeedValidator</c>. If the atom
/// importer ever sweeps the seed root recursively it refuses every one of them and reports the
/// import broken — a failure that looks like bad content and is not.</para>
/// </summary>
public class SeedScannerTests : IDisposable
{
    readonly string _root;

    public SeedScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fusionrpg-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp dir */ }
    }

    string Dir(params string[] parts)
    {
        var p = Path.Combine(new[] { _root }.Concat(parts).ToArray());
        Directory.CreateDirectory(p);
        return p;
    }

    string File_(string dir, string name)
    {
        var p = Path.Combine(dir, name);
        File.WriteAllText(p, "{}");
        return p;
    }

    // ---- roots ------------------------------------------------------------------------------------

    [Fact]
    public void The_default_sweep_takes_the_four_owned_folders_and_nothing_else()
    {
        Dir("atoms"); Dir("containers"); Dir("curves"); Dir("rarity"); Dir("items");

        var roots = SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists);

        Assert.Equal(4, roots.Count);
        Assert.DoesNotContain(roots, r => r.EndsWith("items", StringComparison.Ordinal));
    }

    [Fact]
    public void The_item_seed_corpus_is_never_swept_by_the_default_root()
    {
        // The whole reason this class exists. `items/` is another tool's format.
        Dir("atoms");
        File_(Dir("items", "base-types"), "plant-standard.json");
        File_(Path.Combine(_root, "atoms"), "vitality.json");

        var files = SeedScanner.Files(SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists));

        Assert.Single(files);
        Assert.EndsWith("vitality.json", files[0]);
    }

    [Fact]
    public void An_owned_folder_that_does_not_exist_is_simply_absent()
    {
        // A tree holding only atoms is normal; demanding all four would refuse it.
        Dir("atoms");

        var roots = SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists);

        Assert.Single(roots);
    }

    [Fact]
    public void A_seed_root_with_none_of_the_four_folders_yields_no_roots()
    {
        // Which is what makes the tool say "nothing to import" and exit non-zero, rather than
        // reporting a clean run over zero files.
        Dir("items");

        Assert.Empty(SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists));
    }

    [Fact]
    public void An_explicitly_named_root_is_swept_whole()
    {
        // The escape hatch for a migration folder, which has no reason to use the four names.
        Dir("migration-2026-08");

        var roots = SeedScanner.Roots(_root, explicitRoot: true, Directory.Exists);

        Assert.Equal(new[] { _root }, roots);
    }

    // ---- order ------------------------------------------------------------------------------------

    [Fact]
    public void Files_come_back_in_ordinal_order_whatever_the_filesystem_says()
    {
        // A cross-file duplicate reports which file claimed the id first. If the order varied by
        // machine, so would that message — and so would which of two authors gets told to rename.
        var scrambled = new[] { "z.json", "a.json", "M.json", "b.json" };

        var ordered = SeedScanner.Order(scrambled);

        Assert.Equal(new[] { "M.json", "a.json", "b.json", "z.json" }, ordered);
    }

    [Fact]
    public void Underscore_files_are_notes_and_are_skipped()
    {
        var kept = SeedScanner.Order(new[] { "_exemplar.json", "real.json", "_registry.json" });

        Assert.Equal(new[] { "real.json" }, kept);
    }

    [Fact]
    public void An_underscore_inside_a_name_is_not_a_note()
    {
        // The rule is a leading underscore, not any underscore — `fire_rider.json` is content.
        Assert.Equal(new[] { "fire_rider.json" }, SeedScanner.Order(new[] { "fire_rider.json" }));
    }

    [Fact]
    public void Nested_folders_under_an_owned_folder_are_swept()
    {
        // Authors group by family, and a folder per family is the obvious way to do it.
        File_(Dir("atoms", "elemental"), "fire.json");
        File_(Dir("atoms"), "vitality.json");

        var files = SeedScanner.Files(SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists));

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void Files_that_are_not_json_are_ignored()
    {
        var atoms = Dir("atoms");
        File.WriteAllText(Path.Combine(atoms, "notes.md"), "# notes");
        File_(atoms, "vitality.json");

        var files = SeedScanner.Files(SeedScanner.Roots(_root, explicitRoot: false, Directory.Exists));

        Assert.Single(files);
    }

    // ---- the real repo: curves/ and rarity/ (completeness-audit.md C3) -------------------------------

    [Fact]
    public void The_real_curves_and_rarity_folders_exist_and_document_why_they_are_empty()
    {
        // C3: two owned folders declared with no content looked identical to two forgotten ones.
        // The README in each is the distinction — a real, checked-in file, not a synthetic fixture.
        var root = RepoRoot();

        Assert.True(Directory.Exists(Path.Combine(root, "data", "seed", "curves")), "data/seed/curves missing");
        Assert.True(Directory.Exists(Path.Combine(root, "data", "seed", "rarity")), "data/seed/rarity missing");
        Assert.True(File.Exists(Path.Combine(root, "data", "seed", "curves", "README.md")));
        Assert.True(File.Exists(Path.Combine(root, "data", "seed", "rarity", "README.md")));
    }

    [Fact]
    public void The_real_sweep_finds_zero_json_in_curves_and_rarity_the_readmes_do_not_count()
    {
        var root = RepoRoot();
        var roots = SeedScanner.Roots(Path.Combine(root, "data", "seed"), explicitRoot: false, Directory.Exists);
        var files = SeedScanner.Files(roots);

        Assert.DoesNotContain(files, f => f.Replace('\\', '/').Contains("/curves/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.Replace('\\', '/').Contains("/rarity/", StringComparison.Ordinal));
    }

    // ---- E32 test 9: the two halves of the affix write path ------------------------------------

    /// <summary>The test 9 the spec's own §5 names as "the one that would have prevented this
    /// module" — seedsmith wrote to a folder nothing swept and nobody noticed, because no test
    /// compared the two. Reads seedsmith's own `OUTPUT_DIR` line directly (not the spec's prose
    /// claim), so a future path change on either side fails this test rather than silently
    /// reopening the gap.</summary>
    [Fact]
    public void AtomImporter_swept_folder_matches_seedsmiths_own_affix_write_path()
    {
        var root = RepoRoot();
        var generatorPath = Path.Combine(root, "tools", "seedsmith", "seedsmith", "adapters", "effects", "affix", "generate_affixes.py");
        Assert.True(File.Exists(generatorPath), $"seedsmith's affix generator moved or was renamed: {generatorPath}");

        var source = File.ReadAllText(generatorPath);
        var match = System.Text.RegularExpressions.Regex.Match(
            source, @"OUTPUT_DIR\s*=\s*REPO_ROOT\s*/\s*""data""\s*/\s*""seed""\s*/\s*""effects""\s*/\s*""affixes""");
        Assert.True(match.Success,
            "seedsmith's OUTPUT_DIR no longer reads REPO_ROOT/data/seed/effects/affixes — " +
            "update SeedScanner.OwnedFolders's \"effects/affixes\" entry to match, in the same change");

        Assert.Contains("effects/affixes", SeedScanner.OwnedFolders);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("data/seed");
    }
}
