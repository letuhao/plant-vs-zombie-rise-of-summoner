using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

/// <summary>class-system-todo.md P2.6 / V3 — `tools/ProveAptitude` drives a real resolve on both
/// engines (overlay `DerivedComposer`, battle `BattleStatComposer`) for the same allocation/Theta and
/// asserts they agree. Runs the real `dotnet run` invocation (shared fixture, same pattern as
/// <see cref="CombatSimJsonEmitTests"/> — a cold `dotnet run` is the expensive part).</summary>
public class ProveAptitudeJsonEmitTests : IClassFixture<ProveAptitudeJsonEmitTests.Fixture>
{
    readonly Fixture _fx;
    public ProveAptitudeJsonEmitTests(Fixture fx) => _fx = fx;

    [Fact]
    public void BothComposersAgree_mightToCombatPowerOmni_checkpointTwoScope()
    {
        // The default invocation IS Checkpoint 2's own proof: `.\scripts\prove-aptitude.ps1` with no
        // args runs exactly this. Exit 0, Pass=true, zero delta.
        Assert.True(_fx.DefaultExit == 0, $"prove-aptitude (default) failed:\n{_fx.DefaultStdout}\n{_fx.DefaultStderr}");
        var root = _fx.DefaultDoc!.RootElement;
        Assert.True(root.GetProperty("Pass").GetBoolean());

        var perChannel = root.GetProperty("PerChannel").GetProperty("combat.power.omni");
        var overlay = perChannel.GetProperty("Overlay").GetDouble();
        var battle = perChannel.GetProperty("Battle").GetDouble();
        Assert.Equal(overlay, battle, 6);
        Assert.True(overlay > 0, "Might funded at 100/100 points should produce a positive combat.power.omni contribution");

        var delta = root.GetProperty("Deltas").GetProperty("combat.power.omni").GetDouble();
        Assert.Equal(0.0, delta, 9);
    }

    [Fact]
    public void DocumentSchema_carriesEveryDocumentedKey()
    {
        Assert.True(_fx.DefaultExit == 0);
        var root = _fx.DefaultDoc!.RootElement;
        foreach (var key in new[] { "Theta", "Source", "Points", "PerChannel", "Deltas", "Pass" })
            Assert.True(root.TryGetProperty(key, out _), $"prove-aptitude document missing key '{key}'");
    }

    [Fact]
    public void UnfilteredRun_surfacesTheKnownCapAsymmetryGap_documentedNotHidden()
    {
        // Deliberately NOT Checkpoint 2's scope -- proves the gap this program's own comment records
        // is real and still open, so a future accidental "fix" that makes it silently pass again is
        // itself something worth noticing. BattleStatComposer applies no cap on ANY ChannelMods
        // contribution (confirmed zero `Cap(` calls in that file) — a SumIncreased-kind capped channel
        // (status.resist.*) will disagree once contribution clears the overlay-side cap. P3.1's to
        // inherit, not fixable by touching BattleStatComposer's compose logic (spec §8).
        Assert.True(_fx.UnfilteredExit != 0, "expected the unfiltered run to fail on the known cap-asymmetry gap");
        var root = _fx.UnfilteredDoc!.RootElement;
        Assert.False(root.GetProperty("Pass").GetBoolean());

        var resistCc = root.GetProperty("Deltas").GetProperty("status.resist.cc").GetDouble();
        Assert.NotEqual(0.0, resistCc);

        // The channel Checkpoint 2 actually cares about must still agree even in the unfiltered run —
        // the gap is scoped to specific (capped) channels, not a wholesale breakdown.
        var powerDelta = root.GetProperty("Deltas").GetProperty("combat.power.omni").GetDouble();
        Assert.Equal(0.0, powerDelta, 9);
    }

    public sealed class Fixture : IDisposable
    {
        public int DefaultExit, UnfilteredExit;
        public string DefaultStdout = "", DefaultStderr = "", UnfilteredStdout = "", UnfilteredStderr = "";
        public JsonDocument? DefaultDoc, UnfilteredDoc;

        readonly List<string> _tempFiles = new();

        public Fixture()
        {
            var repoRoot = FindRepoRoot();

            var defaultPath = TempFile();
            (DefaultExit, DefaultStdout, DefaultStderr) = RunProveAptitude(repoRoot,
                $"--theta 1000 --source Might --points 100 --channels combat.power.omni --out \"{defaultPath}\"");
            if (File.Exists(defaultPath)) DefaultDoc = JsonDocument.Parse(File.ReadAllText(defaultPath));

            var unfilteredPath = TempFile();
            (UnfilteredExit, UnfilteredStdout, UnfilteredStderr) = RunProveAptitude(repoRoot,
                $"--theta 1000 --source Might --points 100 --out \"{unfilteredPath}\"");
            if (File.Exists(unfilteredPath)) UnfilteredDoc = JsonDocument.Parse(File.ReadAllText(unfilteredPath));
        }

        string TempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "fusionrpg-prove-aptitude-" + Guid.NewGuid().ToString("N") + ".json");
            _tempFiles.Add(path);
            return path;
        }

        static (int Exit, string Stdout, string Stderr) RunProveAptitude(string repoRoot, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "ProveAptitude")}\" -- {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            };
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            Assert.True(p.WaitForExit(120_000), "ProveAptitude invocation timed out");
            return (p.ExitCode, stdout, stderr);
        }

        public void Dispose()
        {
            DefaultDoc?.Dispose();
            UnfilteredDoc?.Dispose();
            foreach (var f in _tempFiles)
                try { File.Delete(f); } catch { /* temp */ }
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
