using Microsoft.Data.Sqlite;
using FusionRpg.Core.Onboarding;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    public sealed record OnboardingCheckpointRow(
        long PlayerId,
        string CheckpointId,
        string State,
        long? EarnedRunId,
        string? RewardRef,
        string? PayloadJson,
        string EarnedUtc,
        string? ClaimedUtc,
        long Revision);

    public sealed record OnboardingClaimResult(
        bool Ok,
        string Reason,
        OnboardingCheckpointRow? Row);

    public IReadOnlyList<OnboardingCheckpointRow>? ListOnboardingCheckpoints(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return null;
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT player_id, checkpoint_id, state, earned_run_id, reward_ref, payload_json,
                       earned_utc, claimed_utc, revision
                FROM rpg_onboarding_checkpoint
                WHERE player_id=$p
                ORDER BY CASE checkpoint_id
                    WHEN $first THEN 1 WHEN $species THEN 2 WHEN $equipment THEN 3 ELSE 99 END,
                    checkpoint_id;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$first", OnboardingCheckpointIds.FirstWinDave);
            cmd.Parameters.AddWithValue("$species", OnboardingCheckpointIds.Level3GeneralSpecies);
            cmd.Parameters.AddWithValue("$equipment", OnboardingCheckpointIds.Level4DaveEquipment);
            using var reader = cmd.ExecuteReader();
            var rows = new List<OnboardingCheckpointRow>();
            while (reader.Read()) rows.Add(ReadOnboardingCheckpoint(reader));
            return rows;
        }
    }

    /// <summary>Inserts an earned row once. The primary key is the durable idempotency gate.</summary>
    public bool TryEarnOnboardingCheckpoint(
        long playerId, string checkpointId, long? earnedRunId, string? rewardRef, string? payloadJson,
        string? earnedUtc = null)
    {
        if (!OnboardingCheckpointIds.Ordered.Contains(checkpointId, StringComparer.Ordinal))
            throw new ArgumentException("unknown onboarding checkpoint", nameof(checkpointId));
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) throw new InvalidOperationException("player not found");
            return TryEarnOnboardingCheckpointUnlocked(
                db, playerId, checkpointId, earnedRunId, rewardRef, payloadJson, earnedUtc);
        }
    }

    bool TryEarnOnboardingCheckpointUnlocked(
        SqliteConnection db, long playerId, string checkpointId, long? earnedRunId, string? rewardRef,
        string? payloadJson, string? earnedUtc = null)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO rpg_onboarding_checkpoint(
              player_id, checkpoint_id, state, earned_run_id, reward_ref, payload_json, earned_utc, revision)
            VALUES($p,$id,'earned',$run,$ref,$payload,$utc,1);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$id", checkpointId);
        cmd.Parameters.AddWithValue("$run", (object?)earnedRunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ref", (object?)rewardRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$payload", (object?)payloadJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$utc", earnedUtc ?? DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }

    public OnboardingClaimResult ClaimOnboardingCheckpoint(long playerId, string checkpointId)
    {
        if (!OnboardingCheckpointIds.Ordered.Contains(checkpointId, StringComparer.Ordinal))
            return new(false, "onboarding.checkpoint-unknown", null);
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null)
                return new(false, "onboarding.player-not-found", null);
            Exec(db, "BEGIN IMMEDIATE;");
            try
            {
                var row = ReadOnboardingCheckpointUnlocked(db, playerId, checkpointId);
                if (row is null)
                {
                    Exec(db, "ROLLBACK;");
                    return new(false, "onboarding.checkpoint-locked", null);
                }
                if (string.Equals(row.State, "claimed", StringComparison.Ordinal)
                    || row.ClaimedUtc is not null)
                {
                    Exec(db, "COMMIT;");
                    return new(false, "onboarding.checkpoint-already-claimed", row);
                }

                var claimedUtc = DateTime.UtcNow.ToString("o");
                using var update = db.CreateCommand();
                update.CommandText = """
                    UPDATE rpg_onboarding_checkpoint
                    SET state='claimed', claimed_utc=$utc, revision=revision+1
                    WHERE player_id=$p AND checkpoint_id=$id AND state='earned';
                    """;
                update.Parameters.AddWithValue("$utc", claimedUtc);
                update.Parameters.AddWithValue("$p", playerId);
                update.Parameters.AddWithValue("$id", checkpointId);
                if (update.ExecuteNonQuery() == 0)
                {
                    Exec(db, "ROLLBACK;");
                    return new(false, "onboarding.checkpoint-already-claimed", ReadOnboardingCheckpointUnlocked(db, playerId, checkpointId));
                }

                var claimed = ReadOnboardingCheckpointUnlocked(db, playerId, checkpointId);
                Exec(db, "COMMIT;");
                return new(true, "", claimed);
            }
            catch
            {
                try { Exec(db, "ROLLBACK;"); } catch { }
                throw;
            }
        }
    }

    static OnboardingCheckpointRow ReadOnboardingCheckpoint(SqliteDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt64(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetInt64(8));

    static OnboardingCheckpointRow? ReadOnboardingCheckpointUnlocked(
        SqliteConnection db, long playerId, string checkpointId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT player_id, checkpoint_id, state, earned_run_id, reward_ref, payload_json,
                   earned_utc, claimed_utc, revision
            FROM rpg_onboarding_checkpoint
            WHERE player_id=$p AND checkpoint_id=$id;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$id", checkpointId);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadOnboardingCheckpoint(reader) : null;
    }
}
