using FusionRpg.Data;
using FusionRpg.Contracts;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Effects.Atoms;
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
        var soulsAfterFirstSettlement = _store.GetSoulBalance(1).Balance;
        _store.InsertEvents(new[] { events[1] });
        Assert.Equal(soulsAfterFirstSettlement, _store.GetSoulBalance(1).Balance);
        Assert.True(soulsAfterFirstSettlement > 0);

        var player = _store.GetCurrentPlayer()!;
        var row = Assert.Single(_store.ListOnboardingCheckpoints(player.Id)!);
        Assert.Equal("first-win-dave", row.CheckpointId);
        Assert.Equal("earned", row.State);
        Assert.Equal(1, row.Revision);
        Assert.StartsWith("fact:", row.RewardRef);
    }

    [Fact]
    public void Level_three_general_species_checkpoint_uses_validated_source_and_is_idempotent()
    {
        const string matchKey = "onboarding-species";
        var species = CreatureSpeciesCatalog.All.First(x => x.Side == "plant");
        var events = new List<EventEnvelope>
        {
            new EventEnvelope
            {
                Game = RpgConstants.GameId, Kind = "board.start", MatchKey = matchKey,
                T = "2026-01-02T00:00:00Z", Payload = new { levelName = "lawn", levelType = "classic", boardLevel = 1 }
            }
        };
        // The configured player curve needs 245 XP to reach level 3; 21 kills provide 252.
        for (var i = 0; i < 21; i++)
        {
            events.Add(new EventEnvelope
            {
                Game = RpgConstants.GameId, Kind = "zombie.die", MatchKey = matchKey,
                T = $"2026-01-02T00:{i + 1:00}:00Z",
                Payload = new { ptr = $"kill-{i}" }
            });
        }
        events.Add(new EventEnvelope
        {
            Game = RpgConstants.GameId, Kind = "plant.place", MatchKey = matchKey,
            T = "2026-01-02T01:00:00Z",
            Payload = new
            {
                ptr = "general-plant", type = species.GameTypeId, typeName = species.Name,
                sourceKind = CreatureProgressionSource.ContractKind,
                sourceId = $"general:{species.SpeciesId}"
            }
        });
        events.Add(new EventEnvelope
        {
            Game = RpgConstants.GameId, Kind = "match.result", MatchKey = matchKey,
            T = "2026-01-02T01:01:00Z", Payload = new { result = "victory" }
        });

        _store.InsertEvents(events);
        _store.InsertEvents(new[] { events[^1] });

        var player = _store.GetCurrentPlayer()!;
        var state = _store.GetRpgActor(player.Id, RpgActorKinds.Player, 0);
        Assert.True(state!.Level >= 3);
        var rows = _store.ListOnboardingCheckpoints(player.Id)!;
        Assert.Equal(2, rows.Count);
        var speciesRow = rows.Single(x => x.CheckpointId == "level-3-general-species");
        Assert.Equal("earned", speciesRow.State);
        Assert.Contains($"\"speciesId\":\"{species.SpeciesId}\"", speciesRow.PayloadJson);
        Assert.Contains("\"allocationMode\":\"auto-primary-stats\"", speciesRow.PayloadJson);
        Assert.Equal(1, speciesRow.Revision);
    }

    [Fact]
    public void Level_four_mints_one_player_owned_dave_item_and_replays_without_duplicates()
    {
        var repo = Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;
        var atomPath = Path.Combine(repo, "data", "seed", "atoms", "fx-core.json");
        var containerPath = Path.Combine(repo, "data", "seed", "containers", "first-clear-grants.json");
        var rarityPath = Path.Combine(repo, "data", "seed", "rarity", "ladder.v1.json");
        var collected = AtomSeedFile.Collect(new[]
        {
            (atomPath, File.ReadAllText(atomPath)),
            (containerPath, File.ReadAllText(containerPath)),
            (rarityPath, File.ReadAllText(rarityPath)),
        });
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        var imported = _store.ImportContent(collected.Content);
        Assert.True(imported.Committed, string.Join("; ", imported.Errors));

        var species = CreatureSpeciesCatalog.All.First(x => x.Side == "plant");
        var key = "onboarding-level-four";
        var events = new List<EventEnvelope>
        {
            new() { Game = RpgConstants.GameId, Kind = "board.start", MatchKey = key,
                T = "2026-01-03T00:00:00Z", Payload = new { levelName = "lawn", levelType = "classic", boardLevel = 1 } }
        };
        for (var i = 0; i < 42; i++)
            events.Add(new() { Game = RpgConstants.GameId, Kind = "zombie.die", MatchKey = key,
                T = $"2026-01-03T00:{i + 1:00}:00Z", Payload = new { ptr = $"kill-{i}" } });
        events.Add(new() { Game = RpgConstants.GameId, Kind = "plant.place", MatchKey = key,
            T = "2026-01-03T01:00:00Z", Payload = new { ptr = "general-plant", type = species.GameTypeId,
                typeName = species.Name, sourceKind = CreatureProgressionSource.ContractKind,
                sourceId = $"general:{species.SpeciesId}" } });
        events.Add(new() { Game = RpgConstants.GameId, Kind = "match.result", MatchKey = key,
            T = "2026-01-03T01:01:00Z", Payload = new { result = "victory" } });

        _store.InsertEvents(events);
        _store.InsertEvents(new[] { events[^1] });

        var player = _store.GetCurrentPlayer()!;
        var rows = _store.ListOnboardingCheckpoints(player.Id)!;
        var item = Assert.Single(_store.ListItemsByPlayer(player.Id.ToString()));
        var assignment = Assert.Single(_store.ListPlayerItemAssignments(player.Id.ToString()));
        var reward = rows.Single(x => x.CheckpointId == "level-4-dave-equipment");
        Assert.Equal("earned", reward.State);
        Assert.Equal(item.InstanceId, assignment.RefId);
        Assert.Equal(FusionRpg.Core.Items.ItemRole.Standard, assignment.Role);
        Assert.Contains(item.InstanceId, reward.PayloadJson);
        Assert.Equal(1, _store.ListItemsByPlayer(player.Id.ToString()).Count);
    }
}
