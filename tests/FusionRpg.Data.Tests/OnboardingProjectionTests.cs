using FusionRpg.Contracts;
using FusionRpg.Core.Progression;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

public sealed class OnboardingProjectionTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-onboarding-api-" + Guid.NewGuid().ToString("N"));
    readonly RpgStore _store;

    public OnboardingProjectionTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Projection_exposes_authoritative_rows_and_player_level()
    {
        var player = _store.GetCurrentPlayer()!;
        _store.TryEarnOnboardingCheckpoint(player.Id, "first-win-dave", 4, "fact:8", "{\"commanderId\":\"commander:dave\"}");

        var rows = _store.ListOnboardingCheckpoints(player.Id)!;

        var row = Assert.Single(rows);
        Assert.Equal("first-win-dave", row.CheckpointId);
        Assert.Equal("earned", row.State);
        Assert.Equal("fact:8", row.RewardRef);
        Assert.Equal(1, row.Revision);
    }

    [Fact]
    public void Unknown_checkpoint_claim_is_named_and_does_not_write()
    {
        var player = _store.GetCurrentPlayer()!;

        var result = _store.ClaimOnboardingCheckpoint(player.Id, "not-a-checkpoint");

        Assert.False(result.Ok);
        Assert.Equal("onboarding.checkpoint-unknown", result.Reason);
        Assert.Empty(_store.ListOnboardingCheckpoints(player.Id)!);
    }
}
