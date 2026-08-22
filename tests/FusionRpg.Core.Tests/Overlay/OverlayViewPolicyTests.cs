using FusionRpg.Core.Overlay;
using Xunit;

namespace FusionRpg.Core.Tests.Overlay;

/// <summary>
/// Rules for the in-game view that decide whether a player can get back to their game.
/// Pure, because the alternative is discovering them by being trapped in a covered lawn.
/// </summary>
public class OverlayViewPolicyTests
{
    // ---- there must always be a way out ----

    [Fact]
    public void Escape_closes_the_view()
    {
        // The view covers the game, so the in-game button that opened it is underneath it.
        // Without a key that closes it, the only exit is killing the game.
        Assert.True(OverlayViewPolicy.IsCloseKey(OverlayViewPolicy.VirtualKeyEscape));
    }

    [Fact]
    public void The_configured_toggle_key_also_closes_the_view()
    {
        Assert.True(OverlayViewPolicy.IsCloseKey(OverlayViewPolicy.VirtualKeyF10));
    }

    [Theory]
    [InlineData(0x41)] // A
    [InlineData(0x20)] // space
    [InlineData(0x0D)] // enter — the SPA needs it
    public void Ordinary_keys_are_left_to_the_page(int vk)
    {
        Assert.False(OverlayViewPolicy.IsCloseKey(vk));
    }

    // ---- never strand a topmost window over someone else's desktop ----

    [Fact]
    public void A_visible_view_hides_when_the_game_stops_being_foreground()
    {
        // Topmost + excluded from alt-tab means that if the player switches away, our window
        // would sit over whatever they switched to, unreachable by alt-tab.
        Assert.True(OverlayViewPolicy.ShouldAutoHide(visible: true, foregroundIsOurProcess: false));
    }

    [Fact]
    public void A_visible_view_stays_up_while_the_game_is_foreground()
    {
        Assert.False(OverlayViewPolicy.ShouldAutoHide(visible: true, foregroundIsOurProcess: true));
    }

    [Fact]
    public void A_hidden_view_is_never_asked_to_hide_again()
    {
        Assert.False(OverlayViewPolicy.ShouldAutoHide(visible: false, foregroundIsOurProcess: false));
        Assert.False(OverlayViewPolicy.ShouldAutoHide(visible: false, foregroundIsOurProcess: true));
    }

    // ---- the server may not be up when the game starts ----

    [Fact]
    public void A_view_that_never_loaded_navigates_again_when_shown()
    {
        // Init happens at game start, which can be before the server is listening; navigating
        // once would leave a permanent error page behind the button.
        Assert.True(OverlayViewPolicy.ShouldNavigateOnShow(lastNavigationSucceeded: false));
    }

    [Fact]
    public void A_loaded_view_is_not_reloaded_on_every_show()
    {
        // Reloading would throw away SPA state, which is the whole point of hide-not-destroy.
        Assert.False(OverlayViewPolicy.ShouldNavigateOnShow(lastNavigationSucceeded: true));
    }

    // ---- idle cost inside a game process ----

    [Fact]
    public void The_pump_idles_slower_when_nothing_is_on_screen()
    {
        var hidden = OverlayViewPolicy.PumpIntervalMs(visible: false);
        var shown = OverlayViewPolicy.PumpIntervalMs(visible: true);

        Assert.True(shown < hidden, "a visible view needs a responsive pump");
        Assert.True(shown > 0, "never spin at zero delay inside a game process");
        Assert.True(hidden >= 25, "hidden costs nothing to render, so do not wake 200 times a second");
    }
}
