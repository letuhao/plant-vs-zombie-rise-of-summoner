namespace FusionRpg.Data.Seed;

/// <summary>
/// Which files an import sweeps.
///
/// <para>Moved here from <c>tools/AtomImporter</c> (E46, player-content-boot) so the server's
/// self-healing startup import can sweep the same folders the CLI always has — a server referencing
/// a <c>tools/</c> project would be backwards, and a second copy of this class is exactly the kind of
/// drift that let seedsmith's affix folder silently stop being swept once already (see the doc comment
/// on <see cref="OwnedFolders"/>). <c>tools/AtomImporter/Program.cs</c> now calls this same class.</para>
///
/// <para>The one decision in this tool that can be silently wrong. <c>data/seed/</c> also holds the
/// item seed corpus — a different format read by <c>tools/ItemSeedValidator</c> — so a recursive
/// sweep of the seed root refuses all ~125 of its files and reports the import broken. It is in a
/// class rather than in <c>Program</c>'s top-level statements so a test can hold it to that.</para>
/// </summary>
public static class SeedScanner
{
    /// <summary>The folders the atom importer owns. Nothing else under the seed root is its business.
    /// <c>effects/affixes</c> (E32, spec-affix-import-path.md §3.1 break 3) is where seedsmith's own
    /// affix stage writes (`tools/seedsmith/seedsmith/adapters/effects/affix/generate_affixes.py`'s
    /// own `OUTPUT_DIR`) — the two halves of this path must name the same folder, or the pipeline can
    /// silently write to a folder nothing sweeps, exactly as it did before this fix. A test
    /// (`SeedScannerTests.cs`) reads that Python file's own `OUTPUT_DIR` line and asserts it names
    /// this exact entry, mechanically, so the two-halves-disagree failure cannot recur unnoticed.
    /// <c>power</c> (E44 criterion 0, spec-power-sweep.md §4.1) is where a `power-coefficient` seed
    /// file goes — `data/seed/power/coefficients.v1.json` is the canonical path the spec names, but
    /// the folder is swept whole, the same as every other owned folder here.</summary>
    public static readonly string[] OwnedFolders =
        {
            "atoms", "containers", "curves", "rarity", "elements", "channel-policy", "channel-pools",
            "effects/affixes", "power",
        };

    /// <summary>
    /// The folders to sweep. A root the caller named explicitly is swept whole — that is the escape
    /// hatch for a migration folder — and the default root expands to the four owned folders that
    /// actually exist.
    /// </summary>
    public static IReadOnlyList<string> Roots(string seedRoot, bool explicitRoot, Func<string, bool> exists)
    {
        if (explicitRoot) return new[] { seedRoot };

        return OwnedFolders
            .Select(d => Path.Combine(seedRoot, d))
            .Where(exists)
            .ToArray();
    }

    /// <summary>
    /// Ordinal-sorted, so a cross-file duplicate names the same two paths on every machine —
    /// <c>Directory.EnumerateFiles</c> order is filesystem-dependent. Files opening with <c>_</c> are
    /// notes and exemplars, matching the item seed tree's convention.
    /// </summary>
    public static IReadOnlyList<string> Order(IEnumerable<string> paths) =>
        paths.Where(f => !Path.GetFileName(f).StartsWith("_", StringComparison.Ordinal))
             .OrderBy(f => f, StringComparer.Ordinal)
             .ToArray();

    /// <summary>Every JSON file under the given roots, in the order an import reads them.</summary>
    public static IReadOnlyList<string> Files(IEnumerable<string> roots) =>
        Order(roots.SelectMany(r => Directory.GetFiles(r, "*.json", SearchOption.AllDirectories)));
}
