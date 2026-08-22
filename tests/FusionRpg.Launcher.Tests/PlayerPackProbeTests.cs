using FusionRpg.Launcher.Services;

namespace FusionRpg.Launcher.Tests;

public class PlayerPackProbeTests
{
    [Fact]
    public void Run_ok_on_minimal_fake_pack()
    {
        var pack = CreateFakePack(includeInjector: true);
        try
        {
            var result = new PlayerPackProbe().Run(pack);
            Assert.True(result.Ok, result.ToJson());
            Assert.Contains(result.Steps, s => s.Name == "layout" && s.Ok);
            Assert.Contains(result.Steps, s => s.Name == "manifest" && s.Ok);
            Assert.Contains(result.Steps, s => s.Name == "loader_plugin" && s.Ok);
            Assert.Contains(result.Steps, s => s.Name == "dual_load" && s.Ok);
            Assert.Contains(result.Steps, s => s.Name == "update_preserve" && s.Ok);
        }
        finally
        {
            TryDelete(pack);
        }
    }

    [Fact]
    public void Run_fails_layout_when_injector_missing()
    {
        var pack = CreateFakePack(includeInjector: false);
        try
        {
            var result = new PlayerPackProbe().Run(pack);
            Assert.False(result.Ok);
            var layout = result.Steps.First(s => s.Name == "layout");
            Assert.False(layout.Ok);
            Assert.Contains("FusionRpg.Injector.dll", layout.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(pack);
        }
    }

    [Fact]
    public void Run_fails_when_pack_dir_missing()
    {
        var result = new PlayerPackProbe().Run(Path.Combine(Path.GetTempPath(), "no-pack-" + Guid.NewGuid().ToString("N")));
        Assert.False(result.Ok);
        Assert.Contains(result.Steps, s => s.Name == "layout" && !s.Ok);
    }

    static string CreateFakePack(bool includeInjector)
    {
        var pack = Path.Combine(Path.GetTempPath(), "FusionRpgFakePack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pack);
        Directory.CreateDirectory(Path.Combine(pack, "Server", "wwwroot"));
        Directory.CreateDirectory(Path.Combine(pack, "DropIntoGame"));

        File.WriteAllText(Path.Combine(pack, "FusionRpg.Launcher.exe"), "launcher");
        File.WriteAllText(Path.Combine(pack, "Server", "FusionRpg.Server.exe"), "server");
        File.WriteAllText(Path.Combine(pack, "Server", "wwwroot", "index.html"), "<html></html>");
        File.WriteAllText(Path.Combine(pack, "PLAYERS.txt"), "players");
        File.WriteAllText(Path.Combine(pack, "LICENSE"), "AGPL");

        // The launcher's own WebView2, without which the F10 overlay cannot open.
        File.WriteAllText(Path.Combine(pack, "Microsoft.Web.WebView2.Core.dll"), "wv2");
        File.WriteAllText(Path.Combine(pack, "WebView2Loader.dll"), "wv2native");

        if (includeInjector)
        {
            // A shippable drop: the injector, the overlay code it must contain, and WebView2 beside
            // it — PluginInstaller copies top-level files only. See PlayerPackOverlayProbeTests.
            WriteInjectorDrop(Path.Combine(pack, "DropIntoGame"));
            WriteInjectorDrop(Path.Combine(pack, "DropIntoGame", "pvzrh-3.9", "MelonLoader"));
        }

        var manifestSrc = FindLoaderManifest();
        File.Copy(manifestSrc, Path.Combine(pack, "loader-manifest.json"), overwrite: true);
        return pack;
    }

    static void WriteInjectorDrop(string dir)
    {
        Directory.CreateDirectory(dir);
        var name = dir.Contains("MelonLoader", StringComparison.OrdinalIgnoreCase)
            ? "FusionRpg.Injector.MelonLoader.39.dll"
            : "FusionRpg.Injector.dll";
        File.WriteAllText(Path.Combine(dir, name), "inj OverlayViewHost OverlaySwitchGui");
        File.WriteAllText(Path.Combine(dir, "Microsoft.Web.WebView2.Core.dll"), "wv2");
        File.WriteAllText(Path.Combine(dir, "WebView2Loader.dll"), "wv2native");
    }

    static string FindLoaderManifest()
    {
        var fromOutput = Path.Combine(AppContext.BaseDirectory, "loader-manifest.json");
        if (File.Exists(fromOutput)) return fromOutput;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FusionRpg.Launcher", "loader-manifest.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("loader-manifest.json not found for test fixture.");
    }

    static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            /* ignore */
        }
    }
}
