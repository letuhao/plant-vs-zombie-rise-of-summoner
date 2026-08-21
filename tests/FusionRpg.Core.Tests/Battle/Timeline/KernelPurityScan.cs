namespace FusionRpg.Core.Tests.Battle.Timeline;

/// <summary>
/// The kernel purity scan, extracted from its test so the scan itself can be tested.
///
/// A guard that would stay green while the thing it guards is violated is worse than no guard —
/// it manufactures confidence. Keeping the logic here lets one test point it at the real kernel
/// and another point it at a planted violation, so we know it can actually fail.
/// </summary>
static class KernelPurityScan
{
    /// <summary>
    /// Tokens that must not appear in kernel code: wall clock, RNG, floating point — and
    /// dictionary enumeration.
    ///
    /// <c>.Keys</c>/<c>.Values</c> are on the list because that is the shape of the real bug this
    /// codebase already shipped: <c>_byHost.Keys.ToList()</c> in StatusRuntime let Dictionary's
    /// internal ordering reach report bytes in a system that advertises byte-identical replay.
    /// Keyed lookup (<c>TryGetValue</c>) is fine; walking the collection is not.
    /// </summary>
    public static readonly string[] Banned =
        { "DateTime", "DateTimeOffset", "Random", "float ", "double ", "Stopwatch", ".Keys", ".Values" };

    /// <summary>Returns "file:line → token" for every offending line. Empty means clean.</summary>
    public static List<string> Scan(string dir)
    {
        var offences = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i].TrimStart();
                // Prose may name these; code may not.
                if (code.StartsWith("//", StringComparison.Ordinal)) continue;
                foreach (var token in Banned)
                    if (lines[i].Contains(token, StringComparison.Ordinal))
                        offences.Add($"{Path.GetFileName(file)}:{i + 1} → {token}");
            }
        }

        return offences;
    }
}
