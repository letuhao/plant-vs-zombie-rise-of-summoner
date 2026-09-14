using FusionRpg.Core.Overlay;
using Xunit;

namespace FusionRpg.Core.Tests.Overlay;

public sealed class RiftMenuOverlayLayoutTests
{
    [Fact]
    public void Top_middle_left_uses_the_source_aspect_ratio_and_percentage_placement()
    {
        var rect = RiftMenuOverlayLayout.TopMiddleLeft(2043, 1002);

        Assert.Equal(1f, RiftMenuOverlayLayout.AspectRatio, 3);
        Assert.Equal(rect.Width, rect.Height, 3);
        Assert.Equal(0.42f * 2043f, rect.X + rect.Width / 2f, 3);
        Assert.Equal(0.06f * 1002f, rect.Y, 3);
        Assert.True(rect.X < 2043f / 2f, "Rift should sit left of the true screen center.");
    }

    [Fact]
    public void Ultra_wide_and_small_displays_keep_the_rift_square_and_on_screen()
    {
        foreach (var (width, height) in new[] { (3840, 1080), (1280, 720), (0, 0) })
        {
            var rect = RiftMenuOverlayLayout.TopMiddleLeft(width, height);
            Assert.Equal(rect.Width, rect.Height, 3);
            Assert.True(rect.X >= 0f);
            Assert.True(rect.Y >= 0f);
            Assert.True(rect.X + rect.Width <= Math.Max(0, width));
            Assert.True(rect.Y + rect.Height <= Math.Max(0, height));
        }
    }

    [Fact]
    public void Mini_companion_keeps_its_source_ratio_and_stays_inside_the_main_rift()
    {
        foreach (var (width, height) in new[] { (3840, 1080), (2043, 1002), (1280, 720), (0, 0) })
        {
            var primary = RiftMenuOverlayLayout.TopMiddleLeft(width, height);
            var mini = RiftMenuOverlayLayout.MiniCompanion(primary);

            Assert.Equal(1f, RiftMenuOverlayLayout.MiniAspectRatio, 3);
            Assert.Equal(mini.Width, mini.Height, 3);
            Assert.True(mini.Width <= primary.Width);
            Assert.True(mini.X >= primary.X);
            Assert.True(mini.Y >= primary.Y);
            Assert.True(mini.X + mini.Width <= primary.X + primary.Width);
            Assert.True(mini.Y + mini.Height <= primary.Y + primary.Height);
        }
    }
}
