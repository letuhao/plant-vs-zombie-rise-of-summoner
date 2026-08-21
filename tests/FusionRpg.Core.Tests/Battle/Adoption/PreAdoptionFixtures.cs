using System.Runtime.CompilerServices;

namespace FusionRpg.Core.Tests.Battle.Adoption;

/// <summary>
/// Pre-adoption trace fixtures on disk (`tests/fixtures/battle-traces/`).
///
/// Capture semantics: on first run a missing fixture is WRITTEN and returned, so the capture run
/// is green and the file lands in the tree for review. Every later run compares. That means a
/// deleted fixture silently re-blesses, so the files are part of the change under review — same
/// discipline as the golden hashes, which are also only as good as the eyes on the diff.
/// </summary>
static class PreAdoptionFixtures
{
    static string Dir([CallerFilePath] string here = "")
    {
        // tests/FusionRpg.Core.Tests/Battle/Adoption/ -> tests/fixtures/battle-traces/
        var adoption = Path.GetDirectoryName(here)!;
        var testsRoot = Path.GetFullPath(Path.Combine(adoption, "..", "..", ".."));
        return Path.Combine(testsRoot, "fixtures", "battle-traces");
    }

    public static string Load(string name)
    {
        var dir = Dir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".trace.txt");
        return File.Exists(path) ? File.ReadAllText(path) : null!;
    }

    /// <summary>Compares against the stored fixture, capturing it on first run.</summary>
    public static string LoadOrCapture(string name, string actual)
    {
        var dir = Dir();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".trace.txt");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            return actual;
        }

        return File.ReadAllText(path);
    }
}
