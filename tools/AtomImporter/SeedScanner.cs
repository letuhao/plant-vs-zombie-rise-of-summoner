namespace FusionRpg.Tools.AtomImporter;

/// <summary>
/// Which files an import sweeps.
///
/// <para>The one decision in this tool that can be silently wrong. <c>data/seed/</c> also holds the
/// item seed corpus — a different format read by <c>tools/ItemSeedValidator</c> — so a recursive
/// sweep of the seed root refuses all ~125 of its files and reports the import broken. It is in a
/// class rather than in <c>Program</c>'s top-level statements so a test can hold it to that.</para>
/// </summary>
public static class SeedScanner
{
    /// <summary>The folders the atom importer owns. Nothing else under the seed root is its business.</summary>
    public static readonly string[] OwnedFolders =
        { "atoms", "containers", "curves", "rarity", "elements" };

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
