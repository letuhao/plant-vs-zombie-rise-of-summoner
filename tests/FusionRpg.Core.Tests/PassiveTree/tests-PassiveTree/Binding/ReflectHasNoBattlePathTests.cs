using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Core.Tests.PassiveTree.Binding;

/// <summary>
/// Task D2, acceptance bullet 4: "a reflect node is documented as contributing exactly zero through
/// the battle/sim path (`TryReflect` has one caller, `CombatDamageDispatcher.DispatchInstant`), so F3
/// never reports a missing reader as a balance finding." Re-verifies both source-level facts the doc
/// note on <see cref="FusionRpg.Core.PassiveTree.Binding.BinderRunReport"/> depends on, against the
/// real shipped code rather than trusting the spec's own prose (spec-tree-binder.md §6 M2 already
/// carries this finding; this pins it so the note goes stale LOUDLY, not silently, the day someone
/// wires a battle consumer to reflect).
/// </summary>
public class ReflectHasNoBattlePathTests
{
    static string RepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void TryReflect_has_exactly_one_caller_DispatchInstant_itself()
    {
        var path = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Combat", "CombatDamageDispatcher.cs");
        var source = File.ReadAllText(path);

        // Exactly one call site: TryReflect(...) inside DispatchInstant's own body. The declaration
        // itself ("static void TryReflect(") is excluded by requiring an open-paren call, not a
        // "static ... TryReflect(" declaration prefix.
        var callSites = Regex.Matches(source, @"(?<!static\s+void\s)\bTryReflect\s*\(");
        Assert.Equal(1, callSites.Count);
    }

    [Fact]
    public void Nothing_under_Battle_calls_DispatchInstant_or_TryReflect()
    {
        var battleDir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Battle");
        Assert.True(Directory.Exists(battleDir), $"expected {battleDir} to exist");

        foreach (var file in Directory.GetFiles(battleDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.False(source.Contains("DispatchInstant"), $"{file} references DispatchInstant -- " +
                "the reflect-is-lawn-only finding (§6 M2) is now stale and BinderRunReport's doc note must be updated");
            Assert.False(source.Contains("TryReflect"), $"{file} references TryReflect -- " +
                "the reflect-is-lawn-only finding (§6 M2) is now stale and BinderRunReport's doc note must be updated");
        }
    }

    [Fact]
    public void Every_caller_of_DispatchInstant_is_a_lawn_or_overlay_path()
    {
        var coreDir = Path.Combine(RepoRoot(), "src", "FusionRpg.Core");
        var expectedCallers = new[] { "EffectBag.cs", "StatusEffectBridge.cs", "CombatDamageDispatcher.cs" };

        foreach (var file in Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!File.ReadAllText(file).Contains("DispatchInstant(")) continue;
            var name = Path.GetFileName(file);
            Assert.Contains(name, expectedCallers);
        }
    }
}
