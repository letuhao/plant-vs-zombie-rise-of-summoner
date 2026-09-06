using System.Diagnostics;
using System.Reflection;
using FusionRpg.SquadHarness.Tests.TestSupport;
using FusionRpg.Tools.SquadHarness;
using Xunit;

namespace FusionRpg.SquadHarness.Tests;

/// <summary>
/// spec-squad-harness.md "Testing strategy": "Determinism is the hard requirement, so it is asserted
/// three ways, not one." All tests here run over a small SYNTHETIC roster (three real corner builds,
/// not the full 91/23) at a tiny trial count -- spec's own words for the in-process variant: "run twice
/// in one process at a small --trials." Using the shipped <see cref="BuildFactory"/>/<see cref="SquadMatch"/>
/// machinery on a tiny slice keeps these fast without weakening what they prove: the seed function and
/// the aggregation are the same code path the full 91/23 rosters use.
/// </summary>
public class DeterminismTests
{
    static IReadOnlyList<RosterEntry> TinyRoster() => new[]
    {
        new RosterEntry("Might", new[] { BuildFactory.Build("Might") }),
        new RosterEntry("Agility", new[] { BuildFactory.Build("Agility") }),
        new RosterEntry("Bulwark", new[] { BuildFactory.Build("Bulwark") }),
    };

    static RunSpec TinySpec(ulong seed) => new(Theta: 60, Trials: 3, RunSeed: seed);

    [Fact]
    public void Every_run_repeats_byte_identically_in_process()
    {
        TuningBootstrap.Configure();
        var run1 = Sweep.Run("tiny", TinyRoster(), TinySpec(20260906), parallel: false);
        var run2 = Sweep.Run("tiny", TinyRoster(), TinySpec(20260906), parallel: false);
        Assert.Equal(DeterminismHash.Hash(run1), DeterminismHash.Hash(run2));
        Assert.Equal(DeterminismHash.CanonicalJson(run1), DeterminismHash.CanonicalJson(run2));
    }

    [Fact]
    public void Parallel_and_serial_agree()
    {
        TuningBootstrap.Configure();
        var serial = Sweep.Run("tiny", TinyRoster(), TinySpec(777), parallel: false);
        var parallel = Sweep.Run("tiny", TinyRoster(), TinySpec(777), parallel: true);
        Assert.Equal(DeterminismHash.Hash(serial), DeterminismHash.Hash(parallel));
    }

    [Fact]
    public void Reordering_the_roster_does_not_move_a_cell()
    {
        TuningBootstrap.Configure();
        var spec = TinySpec(555);
        var inOrder = TinyRoster();
        var shuffled = new[] { inOrder[2], inOrder[0], inOrder[1] }; // same SET, different enumeration order

        var runA = Sweep.Run("tiny", inOrder, spec, parallel: false);
        var runB = Sweep.Run("tiny", shuffled, spec, parallel: false);

        // Same set of ids -> the seed-index assignment (Id-sorted position) is identical either way,
        // so every surviving (attacker, defender) cell reports the identical outcome.
        Assert.Equal(DeterminismHash.Hash(runA), DeterminismHash.Hash(runB));
    }

    [Fact]
    public void Reordering_that_changes_which_ids_exist_does_move_which_cells_exist_but_not_their_values()
    {
        TuningBootstrap.Configure();
        var spec = TinySpec(333);
        var full = TinyRoster();
        var subset = new[] { full[0], full[1] }; // drop "Bulwark" entirely

        var fullRun = Sweep.Run("tiny", full, spec, parallel: false);
        var subsetRun = Sweep.Run("tiny", subset, spec, parallel: false);

        // The surviving (Might, Agility) cell must report the identical counts in both runs.
        var fromFull = fullRun.Pairs.Single(p => p.AttackerId == "Might" && p.DefenderId == "Agility");
        var fromSubset = subsetRun.Pairs.Single(p => p.AttackerId == "Might" && p.DefenderId == "Agility");
        Assert.Equal(fromFull, fromSubset);
    }

    [Fact]
    public void The_hash_excludes_provenance()
    {
        // HarnessRun carries no provenance field at all (no `at`/`environmentStamp`/`wallClockMs`) --
        // this is asserted structurally: every field on the hashed records is enumerated and none of
        // them is a timestamp-shaped string. A determinism hash is only "portable" if nothing
        // machine/run-specific ever reaches it.
        var properties = typeof(HarnessRun).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(PairResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();
        foreach (var banned in new[] { "at", "environmentstamp", "wallclockms", "timestamp" })
            Assert.DoesNotContain(banned, properties);
    }

    [Fact]
    public void No_double_reaches_the_hash_input()
    {
        // Reflection over the hashed record graph: every LEAF field is string or long -- no float, no
        // double, anywhere the SHA-256 input reads from (CLAUDE.md: a hash of a floating value is
        // non-deterministic across runtimes). A record field (PairResult inside HarnessRun.Pairs) is
        // allowed as long as its OWN properties recurse down to that same rule -- "a list of those"
        // covers a list of records whose fields are themselves string/long, not only bare primitives.
        bool IsAllowed(Type t, HashSet<Type> seen)
        {
            if (t == typeof(string) || t == typeof(long)) return true;
            if (!seen.Add(t)) return true; // cycle guard; none of these types actually cycle

            var listElement = t.GetInterfaces().Prepend(t)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
            if (listElement is not null) return IsAllowed(listElement.GetGenericArguments()[0], seen);

            // A compound record (PairResult) is allowed only if EVERY one of its own properties is.
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return props.Length > 0 && props.All(p => IsAllowed(p.PropertyType, seen));
        }

        foreach (var prop in typeof(PairResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Assert.True(IsAllowed(prop.PropertyType, new HashSet<Type>()), $"PairResult.{prop.Name} is {prop.PropertyType} -- not string/long/list-of-those");
        foreach (var prop in typeof(HarnessRun).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Assert.True(IsAllowed(prop.PropertyType, new HashSet<Type>()), $"HarnessRun.{prop.Name} is {prop.PropertyType} -- not string/long/list-of-those");
    }

    /// <summary>F1's acceptance bullet: "A second process reproduces the hash." A fresh OS process
    /// (not just a second in-process run) catches static state carried across runs an in-process repeat
    /// cannot see. Scoped to <c>--roster squad --limit 4</c> (an F1-internal cost knob, see Modes.cs) so
    /// the check stays fast: the point is proving determinism, not re-measuring the full 506-cell
    /// matrix.</summary>
    [Fact]
    public void A_second_process_reproduces_the_hash()
    {
        var (exit1, stdout1, stderr1) = Run("verify --seed 20260906 --theta 60 --trials 2 --roster squad --limit 4");
        var (exit2, stdout2, stderr2) = Run("verify --seed 20260906 --theta 60 --trials 2 --roster squad --limit 4");
        Assert.True(exit1 == 0, $"exit {exit1}\n{stdout1}\n{stderr1}");
        Assert.True(exit2 == 0, $"exit {exit2}\n{stdout2}\n{stderr2}");
        Assert.Equal(stdout1, stdout2);
    }

    [Fact]
    public void A_missing_seed_is_a_refusal_naming_it()
    {
        var (exit, stdout, stderr) = Run("verify --theta 60 --trials 2");
        Assert.NotEqual(0, exit);
        Assert.Contains("seed", (stdout + stderr), StringComparison.OrdinalIgnoreCase);
    }

    static (int Exit, string Stdout, string Stderr) Run(string args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(repoRoot, "tools", "SquadHarness")}\" -- {args}",
            CreateNoWindow = true,
            WorkingDirectory = repoRoot,
        };
        return ExternalProcess.Run(psi, 180_000, "SquadHarness invocation timed out");
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
