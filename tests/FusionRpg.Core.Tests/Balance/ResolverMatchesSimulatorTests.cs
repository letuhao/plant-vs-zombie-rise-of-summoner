using System.Diagnostics;
using System.Text.Json;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Tests.TestSupport;
using Xunit;

namespace FusionRpg.Core.Tests.Balance;

/// <summary>class-system-todo.md P3.4 — FusionRpg.Core.Stats.Aptitudes.AptitudeResolver.Resolve (the
/// shipped in-game resolver) against tools/CombatSim's own AptitudeModel.Resolve (its independent
/// prototype; the `resolve` CLI command was added to that tool 2026-08-27 for exactly this comparison
/// — no existing command exposed the raw per-channel dictionary, only fully-simulated duels), over the
/// SAME seeded allocation and the SAME live data/tuning/aptitudes.v*.json config. Runs CombatSim as a
/// real subprocess (same cold-start-fixture pattern as DominanceBaselineTests/ResidualFitLoopTests),
/// never a re-implementation of its arithmetic inside the test.
///
/// <para><b>Tolerance, not bit-for-bit equality — deliberate, not a shortcut.</b> Core's own
/// <see cref="AptitudeReadFunctions.Magnitude"/> rounds <c>share^gamma</c> to the nearest per-mille
/// BEFORE multiplying by <c>P(Theta)</c> (a documented exactness step — <c>decimal</c> widening,
/// throws-never-wraps), and <see cref="AptitudeResolver"/>'s own <c>EffectiveKMilli</c> applies the
/// recovery/mitigation scale via INTEGER division (<c>checked(edge.KMilli * tuning.Recovery.ScaleMilli)
/// / 1000</c>), truncating any remainder. CombatSim's resolver does neither — it carries
/// <c>share^gamma</c> and the scale factor as full-precision doubles throughout. Both are internally
/// correct; they buy determinism differently, one via deliberate discretization (Core, which ships in
/// the game and must reproduce exactly) and one via float precision (CombatSim, an offline prototyping
/// tool that never ships — tools/CombatSim/CombatSim.csproj's own comment: "a reimplementation of the
/// math here would drift from src/ and make every balance reading a lie", which is exactly what this
/// test exists to keep honest). The tolerance below is sized to the MEASURED gap this causes on the
/// live shipped config, not guessed.</para></summary>
public class ResolverMatchesSimulatorTests : IDisposable
{
    readonly string _tempDir = Path.Combine(Path.GetTempPath(), "fusionrpg-resolvermatch-" + Guid.NewGuid().ToString("N"));

    public ResolverMatchesSimulatorTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    // Every one of the twelve funded, unevenly, so every edge's share is > 0 (exercising the full
    // shipped edge set, including every recovery- and mitigation-family channel) and no two aptitudes
    // share an accidental symmetry that could hide a per-source bug.
    static readonly (string Id, long Points)[] Seed =
    {
        ("Might", 30), ("Fortitude", 25), ("Vigor", 20), ("Onslaught", 15),
        ("Agility", 12), ("Composure", 10), ("Pierce", 8), ("Focus", 6),
        ("Bulwark", 5), ("Retribution", 4), ("Precision", 3), ("Ferocity", 2)
    };

    [Fact]
    public void ResolverMatchesSimulator_forASeededAllocation_onTheLiveShippedConfig()
    {
        const int theta = 100;
        var repoRoot = FindRepoRoot();
        var tuningPath = LatestAptitudesPath(repoRoot);

        // ── Core side: the real, shipped resolver ──────────────────────────────────────────────
        var tuning = AptitudeTuningLoader.Parse(File.ReadAllText(tuningPath));
        var ladder = new PowerLadder(PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(repoRoot, "data", "tuning", "power-scale.v2.json"))));
        var registry = DerivedStatRegistry.CreateDefault();
        var allocation = Seed.Aggregate(AptitudeAllocation.Empty,
            (acc, s) => acc + AptitudeAllocation.Single(AllocationScope.Commander, s.Id, s.Points));

        var coreChannels = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var mod in AptitudeResolver.Resolve(allocation, tuning, ladder, theta, registry))
            coreChannels[mod.ChannelId] = coreChannels.GetValueOrDefault(mod.ChannelId) + mod.Value;

        // ── CombatSim side: the independent prototype, run as a real subprocess ────────────────
        var seedBuildPath = Path.Combine(_tempDir, "seed.json");
        File.WriteAllText(seedBuildPath, JsonSerializer.Serialize(new
        {
            name = "seed",
            points = Seed.ToDictionary(s => s.Id, s => (double)s.Points)
        }));

        var (exit, stdout, stderr) = RunCombatSim(repoRoot,
            $"resolve --models \"{tuningPath}\" --archetypes \"{seedBuildPath}\" --theta {theta}");
        Assert.True(exit == 0, $"exit {exit}\n{stdout}\n{stderr}");

        using var doc = JsonDocument.Parse(stdout);
        var simChannels = doc.RootElement.GetProperty("channels").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetDouble(), StringComparer.Ordinal);

        // ── Compare ─────────────────────────────────────────────────────────────────────────────
        Assert.NotEmpty(coreChannels);
        Assert.Equal(coreChannels.Count, simChannels.Count); // same edge set fired on both sides

        // Measured, not guessed (2026-08-27): the worst observed gap over this seeded allocation, on
        // the live shipped config, is 1.03% on combat.parry.strength.omni (a mitigation-family channel
        // — the integer-division truncation in EffectiveKMilli is the larger of the class doc's two
        // discretization sources, as expected). 1.5% clears that with headroom while staying tight
        // enough to fail hard on a real divergence — a missing mitigation port, for comparison, was
        // off by the full mitigation scale factor (~3.3x) before this task ported it.
        const double relativeTolerance = 0.015;
        foreach (var (channel, coreValue) in coreChannels)
        {
            Assert.True(simChannels.TryGetValue(channel, out var simValue),
                $"CombatSim produced no value for channel '{channel}', which Core resolved to {coreValue}");
            var scale = Math.Max(Math.Abs(coreValue), 1.0);
            var relError = Math.Abs(coreValue - simValue) / scale;
            Assert.True(relError <= relativeTolerance,
                $"channel '{channel}': core={coreValue} sim={simValue} relError={relError:P2} (tolerance {relativeTolerance:P0})");
        }
    }

    static (int Exit, string Stdout, string Stderr) RunCombatSim(string repoRoot, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "CombatSim")}\" -c Release --no-build -- {args}",
            CreateNoWindow = true,
            WorkingDirectory = repoRoot
        };
        return ExternalProcess.Run(psi, 120_000, "CombatSim invocation timed out");
    }

    static string LatestAptitudesPath(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "data", "tuning");
        var best = Directory.EnumerateFiles(dir, "aptitudes.v*.json")
            .Select(Path.GetFileName)
            .Select(n => (Name: n!, Match: System.Text.RegularExpressions.Regex.Match(n!, @"^aptitudes\.v(\d+)\.json$")))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => int.Parse(x.Match.Groups[1].Value))
            .First();
        return Path.Combine(dir, best.Name);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
