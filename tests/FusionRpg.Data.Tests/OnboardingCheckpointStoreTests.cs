using FusionRpg.Data;
using FusionRpg.Contracts;
using Xunit;

namespace FusionRpg.Data.Tests;

public sealed class OnboardingCheckpointStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-onboarding-" + Guid.NewGuid().ToString("N"));
    readonly RpgStore _store;

    public OnboardingCheckpointStoreTests()
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
    public void Player_one_starts_with_an_empty_checkpoint_view()
    {
        var player = _store.GetCurrentPlayer();

        var rows = _store.ListOnboardingCheckpoints(player!.Id);

        Assert.NotNull(rows);
        Assert.Empty(rows!);
    }

    [Fact]
    public void Earn_is_idempotent_and_preserves_the_first_reward_payload()
    {
        var player = _store.GetCurrentPlayer()!;
        var first = _store.TryEarnOnboardingCheckpoint(player.Id, "first-win-dave", 12, "soul:12", "{\"souls\":20}", "2026-01-01T00:00:00Z");
        var replay = _store.TryEarnOnboardingCheckpoint(player.Id, "first-win-dave", 99, "soul:99", "{\"souls\":99}", "2026-01-02T00:00:00Z");

        Assert.True(first);
        Assert.False(replay);
        var row = Assert.Single(_store.ListOnboardingCheckpoints(player.Id)!);
        Assert.Equal(12, row.EarnedRunId);
        Assert.Equal("soul:12", row.RewardRef);
        Assert.Equal("{\"souls\":20}", row.PayloadJson);
        Assert.Equal("earned", row.State);
        Assert.Equal(1, row.Revision);
    }

    [Fact]
    public void Claim_acknowledges_only_an_earned_row_and_replays_without_writing_again()
    {
        var player = _store.GetCurrentPlayer()!;
        _store.TryEarnOnboardingCheckpoint(player.Id, "first-win-dave", 12, "soul:12", "{}", "2026-01-01T00:00:00Z");

        var claimed = _store.ClaimOnboardingCheckpoint(player.Id, "first-win-dave");
        var replay = _store.ClaimOnboardingCheckpoint(player.Id, "first-win-dave");
        var locked = _store.ClaimOnboardingCheckpoint(player.Id, "level-3-general-species");

        Assert.True(claimed.Ok);
        Assert.Equal("claimed", claimed.Row!.State);
        Assert.Equal(2, claimed.Row.Revision);
        Assert.False(replay.Ok);
        Assert.Equal("onboarding.checkpoint-already-claimed", replay.Reason);
        Assert.False(locked.Ok);
        Assert.Equal("onboarding.checkpoint-locked", locked.Reason);
    }

    [Fact]
    public void Settled_pvz_victory_earns_first_win_inside_capture_transaction_once()
    {
        const string matchKey = "onboarding-first-win";
        var events = new[]
        {
            new EventEnvelope
            {
                Game = RpgConstants.GameId,
                Kind = "board.start",
                MatchKey = matchKey,
                T = "2026-01-01T00:00:00Z",
                Payload = new { levelName = "lawn", levelType = "classic", boardLevel = 1 }
            },
            new EventEnvelope
            {
                Game = RpgConstants.GameId,
                Kind = "match.result",
                MatchKey = matchKey,
                T = "2026-01-01T00:01:00Z",
                Payload = new { result = "won" }
            }
        };

        _store.InsertEvents(events);
        _store.InsertEvents(new[] { events[1] });

        var player = _store.GetCurrentPlayer()!;
        var row = Assert.Single(_store.ListOnboardingCheckpoints(player.Id)!);
        Assert.Equal("first-win-dave", row.CheckpointId);
        Assert.Equal("earned", row.State);
        Assert.Equal(1, row.Revision);
        Assert.StartsWith("fact:", row.RewardRef);
    }
}
