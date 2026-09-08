using FusionRpg.Core.Hud;
using FusionRpg.Injector.Hud;
using Xunit;

namespace FusionRpg.Core.Tests.Hud;

public sealed class ActorHudCacheTests : IDisposable
{
    public ActorHudCacheTests()
    {
        ActorHudCache.Clear();
        ActorHudCache.Build = ptr => new ActorHudSnapshot(
            new ActorHudIdentity(ActorHudTier.Normal, "vanilla", 1, Array.Empty<string>()),
            null,
            Array.Empty<ActorHudStatusToken>(),
            new ActorHudOverflow(0));
    }

    public void Dispose()
    {
        ActorHudCache.Clear();
        ActorHudCache.Build = null;
        ActorHudCache.DeltaEmit = null;
    }

    [Fact]
    public void GetOrBuild_always_rebuilds_from_build_delegate()
    {
        var calls = 0;
        ActorHudCache.Build = _ =>
        {
            calls++;
            return new ActorHudSnapshot(
                new ActorHudIdentity(ActorHudTier.Normal, "vanilla", calls, Array.Empty<string>()),
                null,
                Array.Empty<ActorHudStatusToken>(),
                new ActorHudOverflow(0));
        };

        var first = ActorHudCache.GetOrBuild("ABC");
        var second = ActorHudCache.GetOrBuild("ABC");

        Assert.Equal(2, calls);
        Assert.Equal(1, first!.Identity.LevelBand);
        Assert.Equal(2, second!.Identity.LevelBand);
    }

    [Fact]
    public void GetOrBuild_emits_delta_only_when_dirty()
    {
        var deltaCalls = 0;
        ActorHudCache.DeltaEmit = (_, _) => deltaCalls++;

        ActorHudCache.GetOrBuild("ABC");
        Assert.Equal(0, deltaCalls);

        ActorHudCache.MarkDirty("ABC");
        ActorHudCache.GetOrBuild("ABC");
        Assert.Equal(1, deltaCalls);

        ActorHudCache.GetOrBuild("ABC");
        Assert.Equal(1, deltaCalls);
    }

    [Fact]
    public void Cache_invalidates_on_mark_dirty_rebuild()
    {
        var calls = 0;
        ActorHudCache.Build = _ =>
        {
            calls++;
            return new ActorHudSnapshot(
                new ActorHudIdentity(ActorHudTier.Normal, "vanilla", calls, Array.Empty<string>()),
                null,
                Array.Empty<ActorHudStatusToken>(),
                new ActorHudOverflow(0));
        };

        ActorHudCache.GetOrBuild("ABC");
        ActorHudCache.MarkDirty("ABC");
        var rebuilt = ActorHudCache.GetOrBuild("ABC");

        Assert.Equal(2, calls);
        Assert.Equal(2, rebuilt!.Identity.LevelBand);
    }

    [Fact]
    public void Cache_remove_clears_entry()
    {
        ActorHudCache.GetOrBuild("ABC");
        ActorHudCache.Remove("ABC");
        ActorHudCache.MarkDirty("ABC");

        var calls = 0;
        ActorHudCache.Build = _ =>
        {
            calls++;
            return new ActorHudSnapshot(
                new ActorHudIdentity(ActorHudTier.Normal, "vanilla", null, Array.Empty<string>()),
                null,
                Array.Empty<ActorHudStatusToken>(),
                new ActorHudOverflow(0));
        };

        ActorHudCache.GetOrBuild("ABC");
        Assert.Equal(1, calls);
    }
}
