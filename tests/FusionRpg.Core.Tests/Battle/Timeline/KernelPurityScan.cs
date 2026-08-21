namespace FusionRpg.Core.Tests.Battle.Timeline;

/// <summary>
/// The kernel source guard, extracted from its tests so the guard itself can be tested.
///
/// A guard that would stay green while the thing it guards is violated is worse than no guard —
/// it manufactures confidence. Keeping the logic here lets one test point it at the real kernel
/// and others point it at planted violations, so we know it can actually fail.
///
/// <b>It is a line-based heuristic, not a proof, and should be described that way.</b> A compiler
/// would be exact; this is a grep with judgement. Known blind spots, recorded so nobody mistakes a
/// green scan for a guarantee: a call split mid-expression (<c>items.</c> newline <c>Where(</c>)
/// never forms a contiguous token; block comments (<c>/* … */</c>) are not stripped, so a banned
/// word inside one reads as code; verbatim strings with doubled quotes can confuse the quote
/// tracker. Its value is catching the ordinary case cheaply on every run — the exotic ones are for
/// review to catch.
///
/// Two rule sets, because they protect different properties:
/// <list type="bullet">
/// <item><b>Purity</b> — determinism. No wall clock, no RNG, no floating point, no dictionary
/// enumeration. Applies to every kernel file, with no exceptions.</item>
/// <item><b>Tick path</b> — frame cost. No LINQ, no scene scans, no stat resolves. The kernel
/// runs inside the Unity frame, so these are allocation and latency, not style.</item>
/// </list>
/// </summary>
static class KernelPurityScan
{
    /// <summary>Determinism hazards. No file is exempt.</summary>
    public static readonly string[] BannedEverywhere =
        { "DateTime", "DateTimeOffset", "Random", "float ", "double ", "Stopwatch", ".Keys", ".Values" };

    /// <summary>
    /// Frame-cost hazards on the tick path: LINQ allocates enumerators and delegates, a scene scan
    /// is the exact cost this repo already had to rescue once, and a stat resolve on a per-event
    /// path breaks the standing "per-hit cost is O(1)" invariant.
    /// </summary>
    public static readonly string[] BannedOnTickPath =
    {
        ".Select(", ".SelectMany(", ".Where(", ".OrderBy(", ".OrderByDescending(", ".GroupBy(",
        ".ToList(", ".ToArray(", ".ToDictionary(", ".Any(", ".All(", ".First(", ".FirstOrDefault(",
        ".Last(", ".Single(", ".SequenceEqual(", ".Concat(", ".Distinct(", ".Reverse(",
        ".Skip(", ".Take(", ".Aggregate(", ".Sum(", ".Min(", ".Max(", ".Count(",
        "FindObjectsOfType", "GetComponent", "StatSystem.Resolve"
    };

    /// <summary>
    /// Files exempt from the TICK-PATH rules only — never from purity. Diagnostics may allocate
    /// because they are null in production and every record site is null-conditional; that
    /// exemption is exactly why a diagnostic must never become non-null by default.
    ///
    /// Deliberately a narrow named list rather than a pattern: an exemption that matches by
    /// wildcard grows silently, and the next file to allocate would inherit it for free.
    /// </summary>
    public static readonly string[] DiagnosticsExemptFromTickPath = { "BattleTrace.cs" };

    /// <summary>
    /// Returns "file:line → token" for every offending line. Empty means clean.
    ///
    /// Recursive on purpose. A top-level-only scan silently stops covering the kernel the moment
    /// anyone adds a subfolder — the guard would stay green while new code went unguarded, which
    /// is the failure mode that makes a guard worse than none.
    /// </summary>
    public static List<string> Scan(string dir)
    {
        var offences = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            var tickPathExempt = DiagnosticsExemptFromTickPath.Contains(name, StringComparer.Ordinal);
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripComment(lines[i]);
                if (code.Length == 0) continue;

                foreach (var token in BannedEverywhere)
                    if (code.Contains(token, StringComparison.Ordinal))
                        offences.Add($"{name}:{i + 1} → {token}");

                if (tickPathExempt) continue;
                foreach (var token in BannedOnTickPath)
                    if (ContainsRealCall(code, token))
                        offences.Add($"{name}:{i + 1} → {token} (tick path)");
            }
        }

        return offences;
    }

    /// <summary>
    /// Receivers whose members share a name with a LINQ operator but are cheap intrinsics.
    /// <c>Math.Max</c> is not <c>IEnumerable.Max</c>, and flagging it would train people to
    /// suppress the guard — a guard that cries wolf gets disabled, which is the same outcome as
    /// having no guard, arrived at more slowly.
    /// </summary>
    static readonly string[] SafeReceivers = { "Math", "MathF", "Interlocked", "Volatile" };

    static bool ContainsRealCall(string code, string token)
    {
        var from = 0;
        while (true)
        {
            var at = code.IndexOf(token, from, StringComparison.Ordinal);
            if (at < 0) return false;

            var precededBySafeReceiver = false;
            foreach (var receiver in SafeReceivers)
            {
                if (at >= receiver.Length &&
                    code.AsSpan(at - receiver.Length, receiver.Length).SequenceEqual(receiver))
                {
                    precededBySafeReceiver = true;
                    break;
                }
            }

            if (!precededBySafeReceiver) return true;
            from = at + 1;
        }
    }

    /// <summary>
    /// Removes a line's comment before scanning. Prose must be free to NAME a banned construct —
    /// the doc comments explaining why wall-clock reads are forbidden would otherwise trip the
    /// guard, which would teach people to stop documenting.
    /// </summary>
    static string StripComment(string line)
    {
        // Quote-aware, because a naive "cut at the first //" is a bypass, not just a false
        // positive: a line like  var s = "a//b"; var t = DateTime.UtcNow;  would be truncated
        // inside the string literal and the violation after it would never be seen.
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\\') { i++; continue; }                       // skip an escaped character
            if (c == '"') { inString = !inString; continue; }
            if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                return line[..i];
        }

        return line;
    }
}
