using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// E16: once <c>attackInterval</c>, <c>produceInterval</c> and <c>zombieSpeed</c> are composed
/// channels, the extras path must stop writing those fields behind the composer's back.
///
/// <para><b>Two writers to one field is not a style problem.</b> The composer writes what content and
/// cheats jointly resolved to; the extras path wrote a raw cheat key. Whichever ran last won, and
/// which ran last depended on spawn order — so the same board could settle differently twice. That is
/// the single-writer law, and this is the guard that keeps it after the promotion.</para>
/// </summary>
public class ChannelExtensionGuardTests
{
    [Fact]
    public void The_extras_path_no_longer_writes_the_three_promoted_fields()
    {
        var text = ReadInjector(Path.Combine("Stats", "EntityStatWriter.cs"));

        Assert.DoesNotContain("p.thePlantAttackInterval = CheatState.FVal", text, StringComparison.Ordinal);
        Assert.DoesNotContain("p.thePlantProduceInterval = CheatState.FVal", text, StringComparison.Ordinal);
        Assert.DoesNotContain("z.uniqueSpeed = CheatState.FVal", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_writer_does_write_them_from_the_composed_result()
    {
        // The other half. Without it the guard above passes just as well when the fields are never
        // written at all — which would be a silent capability loss, not a fix.
        var text = ReadInjector(Path.Combine("Stats", "EntityStatWriter.cs"));

        Assert.Contains("p.thePlantAttackInterval = (float)y.AttackInterval", text, StringComparison.Ordinal);
        Assert.Contains("p.thePlantProduceInterval = (float)y.ProduceInterval", text, StringComparison.Ordinal);
        Assert.Contains("z.uniqueSpeed = (float)y.ZombieSpeed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cheat_keys_still_reach_the_fields_as_overrides()
    {
        // The operator surface is unchanged: P-ATK-INT still works, it just arrives the way P-HP
        // always has. If this vanished, the promotion would have quietly removed three cheats.
        var text = ReadInjector("CheatState.cs");

        Assert.Contains("P-ATK-INT", text, StringComparison.Ordinal);
        Assert.Contains("P-PROD-INT", text, StringComparison.Ordinal);
        Assert.Contains("Z-SPD-U", text, StringComparison.Ordinal);
        Assert.Contains("BuildPlantAbsoluteReal", text, StringComparison.Ordinal);
        Assert.Contains("BuildZombieAbsoluteReal", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_baselines_are_captured_or_the_composed_value_is_meaningless()
    {
        // Compose reads a baseline. Left at zero, every interval composes to zero and the writer
        // skips it forever — the feature would look wired and do nothing.
        var text = ReadInjector(Path.Combine("Stats", "EntityApply.cs"));

        Assert.Contains("AttackInterval = p.thePlantAttackInterval", text, StringComparison.Ordinal);
        Assert.Contains("ProduceInterval = p.thePlantProduceInterval", text, StringComparison.Ordinal);
        Assert.Contains("ZombieSpeed = z.uniqueSpeed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_other_extras_keys_were_left_alone()
    {
        // Scope discipline: only these three. The extras surface has ~18 keys and Z-TAKEMULT in
        // particular is LIVE-inconclusive — promoting anything else on the way past is how a
        // contained change becomes an incident.
        var text = ReadInjector(Path.Combine("Stats", "EntityStatWriter.cs"));

        Assert.Contains("p.theShieldHealth = CheatState.IVal(\"P-SHIELD\")", text, StringComparison.Ordinal);
        Assert.Contains("z.takeDmgMultiplier = CheatState.FVal(\"Z-TAKEMULT\")", text, StringComparison.Ordinal);
        Assert.Contains("p.theLevel = CheatState.IVal(\"P-LEVEL\")", text, StringComparison.Ordinal);
    }

    // ---- E17: the three CC branches -------------------------------------------------------------

    [Fact]
    public void The_three_declared_but_unwired_statuses_have_branches_now()
    {
        // ember / jala / kelp declared UnityCc and had no case here. The game methods existed the
        // whole time — status-ssot.md was right and our code never caught up.
        var text = ReadInjector("DebugActions.cs");

        Assert.Contains("z.SetEmbered(", text, StringComparison.Ordinal);
        Assert.Contains("z.SetJalaed(", text, StringComparison.Ordinal);
        Assert.Contains("z.SetKelped(", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Charm_pulse_was_not_faked_with_a_float_write()
    {
        // No SetCharm* exists — verified against Assembly-CSharp metadata. Faking it through the
        // applyFloatSlow path would make the status look implemented while doing something else.
        var text = ReadInjector("DebugActions.cs");

        Assert.DoesNotContain("SetCharm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"charm_pulse\"", text, StringComparison.Ordinal);
    }

    static string ReadInjector(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Injector", relative);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
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
