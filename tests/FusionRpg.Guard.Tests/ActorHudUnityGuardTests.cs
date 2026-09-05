using Xunit;

namespace FusionRpg.Guard.Tests;

public sealed class ActorHudUnityGuardTests
{
    static readonly string[] RenderFiles =
    {
        Path.Combine("Hud", "ActorHudPool.cs"),
        Path.Combine("Hud", "ActorHudRowIdentity.cs"),
        Path.Combine("Hud", "ActorHudRowResources.cs"),
        Path.Combine("Hud", "ActorHudRowStatuses.cs"),
    };

    static readonly string[] BannedRuntimeNeedles =
    {
        "ShieldRuntime",
        "StatusRuntime",
        "EffectRuntime.Bag",
    };

    [Fact]
    public void ActorHudPool_uses_Feet_lane_Y_and_bounds_X()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudPool.cs"));
        Assert.Contains("VfxAnchorKind.Feet", text, StringComparison.Ordinal);
        Assert.Contains("BoundsCenterX", text, StringComparison.Ordinal);
        Assert.Contains("WorldYOffset", text, StringComparison.Ordinal);
        Assert.DoesNotContain("VfxAnchorKind.Crown", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnitFrameResolver_collects_SpriteRenderer_bounds()
    {
        var text = ReadInjector(Path.Combine("Fx", "UnitFrameResolver.cs"));
        Assert.Contains("TryCollectSpriteBounds", text, StringComparison.Ordinal);
        Assert.Contains("GetComponentsInChildren<SpriteRenderer>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GetComponentInChildren<Renderer>()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudPool_uses_UnitFrameResolver_in_pool()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudPool.cs"));
        Assert.Contains("UnitFrameResolver.Resolve", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VfxDirector_wakes_actor_hud_on_live_match_phase()
    {
        var text = ReadInjector(Path.Combine("Fx", "VfxDirector.cs"));
        Assert.Contains("MatchHost.Runtime.Phase", text, StringComparison.Ordinal);
        Assert.Contains("MatchPhase.InMatch", text, StringComparison.Ordinal);
        Assert.Contains("ActorHudDirector.TickSync", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHud_render_files_no_BodyWorld_or_bounds()
    {
        foreach (var relative in RenderFiles)
        {
            var text = ReadInjector(relative);
            Assert.DoesNotContain("LawnCoords.BodyWorld", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".bounds", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ActorHud_render_path_no_direct_runtime_reads()
    {
        foreach (var relative in RenderFiles)
        {
            var text = ReadInjector(relative);
            foreach (var needle in BannedRuntimeNeedles)
                Assert.DoesNotContain(needle, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VfxDirector_ticks_actor_hud_director()
    {
        var text = ReadInjector(Path.Combine("Fx", "VfxDirector.cs"));
        Assert.Contains("ActorHudDirector.TickSync", text, StringComparison.Ordinal);
        Assert.Contains("ActorHudDirector.StopAll", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VfxDirector_does_not_call_ShieldBarPool_TickSync()
    {
        var text = ReadInjector(Path.Combine("Fx", "VfxDirector.cs"));
        Assert.DoesNotContain("ShieldBarPool.TickSync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldBarPool.StopAll", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudRowResources_honors_ShieldBarEnabled_toggle()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudRowResources.cs"));
        Assert.Contains("OverlaySettings.ShieldBarEnabled", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudPool_uses_ActorHudVisibility_ShouldShow()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudPool.cs"));
        Assert.Contains("OverlaySettings.ShieldBarEnabled", text, StringComparison.Ordinal);
        Assert.Contains("ActorHudVisibility.ShouldShow", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static bool ShouldShow(ActorHudSnapshot", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudRowIdentity_uses_display_tokens_and_hides_blank_tier()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudRowIdentity.cs"));
        Assert.Contains("ActorHudDisplayTokens.TierLetter", text, StringComparison.Ordinal);
        Assert.Contains("PlaceLabel", text, StringComparison.Ordinal);
        Assert.Contains("hasLetter", text, StringComparison.Ordinal);
        Assert.Contains("No blank Normal tier frame", text, StringComparison.Ordinal);
        Assert.Contains("Never draw mute level badge", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudRowResources_uses_StackPips_and_maxStackPips()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudRowResources.cs"));
        Assert.Contains("StackPips", text, StringComparison.Ordinal);
        Assert.Contains("maxStackPips", text, StringComparison.Ordinal);
        Assert.Contains("PipGap", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldBarPool_file_removed_from_injector()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Injector", "Fx", "ShieldBarPool.cs");
        Assert.False(File.Exists(path), "ShieldBarPool.cs must be deleted after shield-slot-migration");
    }

    [Fact]
    public void Injector_has_no_ShieldBarPool_references()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "FusionRpg.Injector");
        var hits = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("ShieldBarPool", StringComparison.Ordinal))
                hits.Add(Path.GetRelativePath(root, file));
        }

        Assert.Empty(hits);
    }

    [Fact]
    public void ShieldBarOverlay_capture_delegates_to_ActorHudDirector()
    {
        var text = ReadInjector(Path.Combine("Hud", "ShieldBarOverlay.cs"));
        Assert.Contains("ActorHudDirector.CaptureStatus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldBarPool", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudDirector_exposes_CaptureStatus_for_debug_bar_status()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudDirector.cs"));
        Assert.Contains("public static Dictionary<string, object> CaptureStatus()", text, StringComparison.Ordinal);
        Assert.Contains("ActorHudPool.ShieldBarsDrawn", text, StringComparison.Ordinal);
        Assert.Contains("[\"hudSlots\"]", text, StringComparison.Ordinal);
        Assert.Contains("[\"shieldBars\"]", text, StringComparison.Ordinal);
        Assert.Contains("WorldBars", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorHudPool_TickSync_always_runs_slot_release_loop()
    {
        var text = ReadInjector(Path.Combine("Hud", "ActorHudPool.cs"));
        var tickStart = text.IndexOf("public static void TickSync()", StringComparison.Ordinal);
        Assert.True(tickStart >= 0);
        var tickBody = text[tickStart..];
        const string releaseNeedle = "for (var i = 0; i < Slots.Count; i++)";
        var releaseAt = tickBody.IndexOf(releaseNeedle, StringComparison.Ordinal);
        Assert.True(releaseAt >= 0, "release loop missing from TickSync");
        var beforeRelease = tickBody[..releaseAt];
        Assert.DoesNotContain("return;", beforeRelease, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgetEntity_clears_actor_hud_cache_and_pool()
    {
        var text = ReadInjector("GameHooks.cs");
        Assert.Contains("ActorHudCache.Remove", text, StringComparison.Ordinal);
        Assert.Contains("ActorHudPool.ReleaseOwner", text, StringComparison.Ordinal);
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
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
