using System.Windows.Input;
using FusionRpg.Launcher.Services;

namespace FusionRpg.Launcher.Tests;

public class GameWindowInteropTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NotAKey")]
    public void ParseOverlayKey_falls_back_to_F10(string? name)
    {
        Assert.Equal(Key.F10, GameWindowInterop.ParseOverlayKey(name));
    }

    [Theory]
    [InlineData("F9", Key.F9)]
    [InlineData("f10", Key.F10)]
    [InlineData(" F12 ", Key.F12)]
    [InlineData("Pause", Key.Pause)]
    public void ParseOverlayKey_accepts_key_names(string name, Key expected)
    {
        Assert.Equal(expected, GameWindowInterop.ParseOverlayKey(name));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("LeftCtrl")]
    [InlineData("LWin")]
    public void ParseOverlayKey_rejects_modifier_only_keys(string name)
    {
        Assert.Equal(Key.F10, GameWindowInterop.ParseOverlayKey(name));
    }
}
