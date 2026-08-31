using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>actor-hud slice 1 — injector host loads actor-hud.v1.json into ActorHudTuningHub at startup.</summary>
public class ActorHudHostInjectionTests
{
    const string WiringNeedle = "ActorHudTuningHub.Configure(";
    const string LoaderNeedle = "ActorHudTuningLoader.Parse(";
    const string FileNeedle = "actor-hud.v1.json";

    [Fact]
    public void InjectorHost_wiresActorHudTuningHub()
    {
        var text = ReadInjector(Path.Combine("Host", "RpgHost.cs"));
        Assert.Contains(WiringNeedle, text, StringComparison.Ordinal);
        Assert.Contains(LoaderNeedle, text, StringComparison.Ordinal);
        Assert.Contains(FileNeedle, text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudTuningHub_throws_when_unconfigured()
    {
        var text = ReadCore(Path.Combine("Hud", "ActorHudTuning.cs"));
        Assert.Contains("ActorHudTuningHub.Configure(...) has not run", text, StringComparison.Ordinal);
        Assert.Contains("there is no built-in default", text, StringComparison.Ordinal);
    }

    static string ReadInjector(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Injector", relative);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string ReadCore(string relative)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Core", relative);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
