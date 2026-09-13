using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Tools.ProveLiveProbe.Tests;

/// <summary>
/// Guards the tool's own boundary rule (spec-live-probe-tool.md "Boundaries"; tasks/live-probe-todo.md
/// Task 8): the ONLY <c>/api/debug/*</c> routes <c>tools/ProveLiveProbe</c>'s own source may reference
/// are the identity-only <c>spawn-unique-actor</c> acquire shortcut and the read-only
/// <c>board-stats</c> send — the same class of guard <c>guard-debug-scope.ps1</c> applies to
/// <c>DebugEndpoints.cs</c> itself, applied here to this tool's own client code, so a future change
/// cannot quietly add a fabrication-shaped debug call with nothing catching it.
///
/// <para>Scans actual C# STRING LITERALS only (a quoted <c>"/api/debug/..."</c> substring), never bare
/// text — a doc comment is free to discuss a route by name (e.g. explaining why
/// <c>/api/debug/events</c> is deliberately NOT called, in favor of the plain, non-debug-scoped
/// <c>/api/events</c>) without being mistaken for a real call site.</para>
/// </summary>
public class DebugRouteSourceScanTests
{
    static readonly Regex DebugRouteLiteral = new("\"(/api/debug/[^\"]*)\"", RegexOptions.Compiled);

    static readonly string[] AllowedRoutes =
    {
        "/api/debug/spawn-unique-actor",
        "/api/debug/board-stats",
    };

    /// <summary><c>[CallerFilePath]</c> gives this file's own absolute path at compile time — a
    /// reliable way to locate <c>tools/ProveLiveProbe</c> regardless of the test runner's working
    /// directory (never relies on the current directory or an assumed repo-root).</summary>
    static string FindProveLiveProbeSourceDir([CallerFilePath] string thisFile = "")
    {
        var testsDir = Path.GetDirectoryName(Path.GetFullPath(thisFile))!;
        var srcDir = Path.GetFullPath(Path.Combine(testsDir, "..", "ProveLiveProbe"));
        if (!Directory.Exists(srcDir))
            throw new DirectoryNotFoundException($"expected tools/ProveLiveProbe next to tools/ProveLiveProbe.Tests, found none at '{srcDir}'");
        return srcDir;
    }

    static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(FindProveLiveProbeSourceDir(), "*.cs", SearchOption.TopDirectoryOnly);

    [Fact]
    public void Source_references_exactly_the_two_allowed_debug_routes()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in DebugRouteLiteral.Matches(text))
                found.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(found); // sanity: the scan itself must have found the two real call sites
        Assert.Equal(new SortedSet<string>(AllowedRoutes, StringComparer.Ordinal), found);
    }

    [Fact]
    public void Source_directory_actually_contains_the_two_call_sites_this_test_expects()
    {
        // Guards the guard: if someone deletes both real calls and the scan above finds nothing to
        // compare (an empty set trivially "equals" nothing meaningful), this fails loudly instead.
        var text = string.Concat(SourceFiles().Select(File.ReadAllText));
        Assert.Contains("\"/api/debug/spawn-unique-actor\"", text);
        Assert.Contains("\"/api/debug/board-stats\"", text);
    }
}
