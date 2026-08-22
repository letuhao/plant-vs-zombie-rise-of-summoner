using System.Text.RegularExpressions;
using Xunit;

namespace FusionRpg.Guard.Tests;

/// <summary>
/// The overlay pipe contract is duplicated on purpose: the launcher (net8.0-windows WPF) and the
/// injector (net6.0, loaded into the game) share no assembly, so the pipe name and verbs exist as
/// literals on both sides. Nothing at compile time links them — if one drifts, the in-game button
/// simply stops reaching the launcher and hides itself, with no error anywhere. These tests are
/// that missing link. Contract: docs/launcher/overlay-spec.md §Transport.
/// </summary>
public class OverlayPipeContractGuardTests
{
    const string ServerFile = @"src\FusionRpg.Launcher\Services\OverlayPipeServer.cs";
    const string ClientFile = @"src\FusionRpg.Injector\Hud\OverlaySwitch.cs";

    [Fact]
    public void Both_sides_declare_the_same_pipe_name()
    {
        var server = PipeNameIn(ServerFile);
        var client = PipeNameIn(ClientFile);

        Assert.False(string.IsNullOrEmpty(server), $"no PipeName literal found in {ServerFile}");
        Assert.False(string.IsNullOrEmpty(client), $"no PipeName literal found in {ClientFile}");
        Assert.Equal(server, client);
    }

    [Fact]
    public void The_pipe_name_still_matches_the_spec()
    {
        Assert.Equal("FusionRpg.Overlay", PipeNameIn(ServerFile));

        var spec = ReadRepoFile(@"docs\launcher\overlay-spec.md");
        Assert.Contains(@"\\.\pipe\FusionRpg.Overlay", spec, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_verb_the_client_sends_is_one_the_server_accepts()
    {
        var server = ReadRepoFile(ServerFile);
        var client = ReadRepoFile(ClientFile);

        // Verbs the client actually puts on the wire: Send("toggle", ...) / Send("ping", ...)
        var sent = Regex.Matches(client, @"Send\(""(?<verb>[a-z]+)""")
            .Select(m => m.Groups["verb"].Value)
            .Distinct()
            .ToList();

        Assert.NotEmpty(sent);
        foreach (var verb in sent)
        {
            Assert.True(
                server.Contains($"\"{verb}\" => OverlayPipeCommand.", StringComparison.Ordinal),
                $"the injector sends \"{verb}\" but the launcher's ParseCommand does not map it — " +
                "the button would silently do nothing");
        }
    }

    [Fact]
    public void The_launcher_stays_the_default_host()
    {
        // overlayHost=injector must stay opt-in until the in-game view is proven live.
        var sel = ReadRepoFile(@"src\FusionRpg.Core\Overlay\OverlaySwitchState.cs");
        Assert.NotNull(sel);

        var mode = ReadRepoFile(@"src\FusionRpg.Core\Overlay\OverlayHostSelection.cs");
        Assert.Contains("Launcher = 0", mode, StringComparison.Ordinal);
        Assert.Contains("return OverlayHostMode.Launcher;", mode, StringComparison.Ordinal);

        foreach (var host in new[]
                 {
                     @"src\FusionRpg.Injector.BepInEx\Plugin.cs",
                     @"src\FusionRpg.Injector\Host\FileRpgConfig.cs"
                 })
        {
            var text = ReadRepoFile(host);
            Assert.True(
                text.Contains("\"launcher\"", StringComparison.Ordinal),
                $"{host} should default OverlayHost to \"launcher\"");
        }
    }

    [Fact]
    public void The_in_game_view_is_torn_down_by_both_hosts()
    {
        // A browser process outliving the game is the spike's stated no-go.
        foreach (var host in new[]
                 {
                     @"src\FusionRpg.Injector.BepInEx\Plugin.cs",
                     @"src\FusionRpg.Injector.MelonLoader\MelonFusionRpgMod.cs"
                 })
        {
            var text = ReadRepoFile(host);
            Assert.True(
                text.Contains("OnApplicationQuit", StringComparison.Ordinal)
                && text.Contains("OverlaySwitch.Shutdown", StringComparison.Ordinal),
                $"{host} must tear the overlay view down on quit");
        }
    }

    [Fact]
    public void The_webview_payload_stays_trimmed_in_both_hosts()
    {
        // Everything referenced here lands in the player's game folder.
        foreach (var proj in new[]
                 {
                     @"src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj",
                     @"src\FusionRpg.Injector.MelonLoader\FusionRpg.Injector.MelonLoader.csproj"
                 })
        {
            var text = ReadRepoFile(proj);
            Assert.Contains("Microsoft.Web.WebView2", text, StringComparison.Ordinal);
            Assert.Contains("TrimWebView2Payload", text, StringComparison.Ordinal);
            Assert.Contains("TrimWebView2Natives", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_in_game_view_keeps_a_way_out()
    {
        // The view covers the game, so the button that opened it is underneath it. Wave 1 has Esc,
        // WPF chrome and a launcher-registered hotkey; the in-game host has none of those, so the
        // key handler is the only exit. Losing it strands the player in a covered lawn.
        var host = ReadRepoFile(@"src\FusionRpg.Injector\Hud\OverlayViewHost.cs");

        Assert.True(
            host.Contains("AcceleratorKeyPressed", StringComparison.Ordinal),
            "OverlayViewHost must handle keys, or the view cannot be closed from inside");
        Assert.True(
            host.Contains("OverlayViewPolicy.IsCloseKey", StringComparison.Ordinal),
            "the close-key rule belongs to OverlayViewPolicy, where it is unit-tested");
        Assert.True(
            host.Contains("OverlayViewPolicy.ShouldAutoHide", StringComparison.Ordinal),
            "a topmost, non-alt-tabbable window must hide when the game loses foreground");
    }

    static string PipeNameIn(string relativePath)
    {
        var text = ReadRepoFile(relativePath);
        var match = Regex.Match(text, @"PipeName\s*=\s*""(?<name>[^""]+)""");
        return match.Success ? match.Groups["name"].Value : "";
    }

    static string ReadRepoFile(string relativePath)
    {
        var path = Path.Combine(FindRepoRoot(), relativePath);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
