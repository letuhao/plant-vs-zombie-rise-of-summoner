using FusionRpg.Core.Commanders;
using FusionRpg.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>commander-surface default-persistence: implicit Dave, round-trip, corrupt read, reset.</summary>
public class PlayerCommanderStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public PlayerCommanderStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-commander-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static long ReadRevision(string hotPath, long playerId)
    {
        using var db = new SqliteConnection($"Data Source={hotPath}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT revision FROM rpg_player_commander WHERE player_id=$p;";
        cmd.Parameters.AddWithValue("$p", playerId);
        return (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("missing row"));
    }

    static long ReadRowCount(string hotPath)
    {
        using var db = new SqliteConnection($"Data Source={hotPath}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM rpg_player_commander;";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    static bool TableExists(string hotPath, string tableName)
    {
        using var db = new SqliteConnection($"Data Source={hotPath}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", tableName);
        return cmd.ExecuteScalar() != null;
    }

    [Fact]
    public void Init_ensures_rpg_player_commander_schema()
    {
        Assert.True(TableExists(_store.HotPath, "rpg_player_commander"));
    }

    [Fact]
    public void Fresh_save_reads_implicit_Dave_without_seeded_row()
    {
        Assert.Equal(CommanderId.Dave.ToStableId(), _store.GetDefaultLawnCommanderId(1));
        Assert.Equal(0, ReadRowCount(_store.HotPath));
    }

    [Fact]
    public void SetDefault_round_trips_and_increments_revision()
    {
        var stable = CommanderId.Dave.ToStableId();
        var (ok, reason) = _store.SetDefaultLawnCommanderId(1, stable);
        Assert.True(ok, reason);
        Assert.Equal(stable, _store.GetDefaultLawnCommanderId(1));
        Assert.Equal(1, ReadRevision(_store.HotPath, 1));

        Assert.True(_store.SetDefaultLawnCommanderId(1, stable).Ok);
        Assert.Equal(stable, _store.GetDefaultLawnCommanderId(1));
        Assert.Equal(2, ReadRevision(_store.HotPath, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_commander_id_rejected(string commanderId)
    {
        var result = _store.SetDefaultLawnCommanderId(1, commanderId);
        Assert.False(result.Ok);
        Assert.Equal("commander.missing", result.Reason);
    }

    [Fact]
    public void Invalid_write_rejected_and_prior_value_preserved()
    {
        var stable = CommanderId.Dave.ToStableId();
        Assert.True(_store.SetDefaultLawnCommanderId(1, stable).Ok);

        var bad = _store.SetDefaultLawnCommanderId(1, "commander:penny");
        Assert.False(bad.Ok);
        Assert.Equal("commander.unknown", bad.Reason);

        var zomboss = _store.SetDefaultLawnCommanderId(1, CommanderId.Zomboss.ToStableId());
        Assert.False(zomboss.Ok);
        Assert.Equal("commander.not-empire", zomboss.Reason);
        Assert.Equal(stable, _store.GetDefaultLawnCommanderId(1));
    }

    [Fact]
    public void Corrupt_stored_id_reads_as_implicit_Dave()
    {
        Assert.True(_store.SetDefaultLawnCommanderId(1, CommanderId.Dave.ToStableId()).Ok);
        using (var db = new SqliteConnection($"Data Source={_store.HotPath}"))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "UPDATE rpg_player_commander SET default_lawn_commander_id='not-a-commander' WHERE player_id=1;";
            cmd.ExecuteNonQuery();
        }

        Assert.Equal(CommanderId.Dave.ToStableId(), _store.GetDefaultLawnCommanderId(1));
    }

    [Fact]
    public void Reset_clears_player_commander_rows()
    {
        Assert.True(_store.SetDefaultLawnCommanderId(1, CommanderId.Dave.ToStableId()).Ok);
        Assert.Equal(1, ReadRowCount(_store.HotPath));
        _store.Reset();
        Assert.Equal(0, ReadRowCount(_store.HotPath));
        Assert.Equal(CommanderId.Dave.ToStableId(), _store.GetDefaultLawnCommanderId(1));
    }

    [Fact]
    public void Unknown_player_rejected_on_set()
    {
        var result = _store.SetDefaultLawnCommanderId(999, CommanderId.Dave.ToStableId());
        Assert.False(result.Ok);
        Assert.Equal("player.unknown", result.Reason);
    }
}
