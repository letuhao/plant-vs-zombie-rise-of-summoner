using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// lawn-combat-wire L-N1, owner decision 2026-09-15: the basic-attack feature ships behind its switch,
/// default OFF, after a measured 300-zombie frame-budget breach. Pinned because it is a recorded owner
/// decision, not a reading — flipping it back is a new decision with its own perf evidence. The env var
/// is read at process start and is the only sanctioned way a live proof enables the feature (a mid-match
/// toggle leaves bound grants live, L-N8). Source scan: <c>LawnBasicAttackFeatureFlagTests</c> lives in
/// Injector.Tests, which CI does not build.
/// </summary>
public class LawnBasicAttackDefaultOffGuardTests
{
    static string Feature() => File.ReadAllText(Path.Combine(RepoRoot(), "src", "FusionRpg.Injector", "Effects", "LawnBasicAttackFeature.cs"));

    [Fact]
    public void The_feature_default_is_off()
    {
        var text = Feature();
        Assert.Contains("public const bool DefaultEnabled = false;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultOn", text.Replace("Default ON", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void Env_zero_forces_off_env_one_forces_on_and_only_then_a_debug_override_applies()
    {
        var text = Feature();
        Assert.Contains("static readonly bool EnvForcedOff = string.Equals(EnvValue, \"0\", StringComparison.Ordinal);", text, StringComparison.Ordinal);
        Assert.Contains("static readonly bool EnvForcedOn = string.Equals(EnvValue, \"1\", StringComparison.Ordinal);", text, StringComparison.Ordinal);
        Assert.Contains("public static bool Enabled => !EnvForcedOff && (EnvForcedOn || (DebugOverride ?? DefaultEnabled));", text, StringComparison.Ordinal);
    }

    static string RepoRoot()
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
