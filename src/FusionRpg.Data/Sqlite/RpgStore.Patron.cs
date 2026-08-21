using FusionRpg.Contracts;
using FusionRpg.Core.Demons.Patron;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed record PatronRow(long PlayerId, string InstanceId, string SetUtc, long Revision);

public sealed partial class RpgStore
{
    /// <summary>
    /// Patron designation (spec-patron-demon.md): first set free, every change spends
    /// PatronPolicy.SwitchCostSouls — one transaction, refusals write nothing. Re-designating
    /// the CURRENT patron is a natural free replay; a correlation reused for a different target
    /// is a mismatch (the soul-ledger dedupe is the switch's replay anchor).
    /// </summary>
    public (bool Ok, string Reason, PatronRow? Patron) SetPatron(long playerId, string instanceId, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return (false, "correlation.missing", null);
        if (string.IsNullOrWhiteSpace(instanceId)) return (false, "specimen.missing", null);
        var id = instanceId.Trim();
        var corr = correlationId.Trim();

        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return (false, "player.unknown", null);
            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");

            var actor = ReadUniqueActorUnlocked(db, id);
            var profile = actor == null ? null : ReadDemonProfileUnlocked(db, id);
            if (actor is null || actor.PlayerId != playerId || profile is null
                || string.Equals(actor.Phase, UniqueActorPhases.Retired, StringComparison.Ordinal))
                return (false, "specimen.missing", null);

            // A patron speaks for the summoner: it must be a demon that actually serves.
            var contract = ContractViewUnlocked(db, playerId, id);
            if (!contract.Bound) return (false, "patron.unbound", null);
            if (!contract.Deployable) return (false, "patron.insubordinate", null);

            var current = ReadPatronUnlocked(db, playerId);
            if (current != null && string.Equals(current.InstanceId, id, StringComparison.Ordinal))
            {
                tx.Commit();
                return (true, "replay", current);
            }

            if (current != null)
            {
                var balance = ReadSoulBalanceUnlocked(db, playerId);
                if (balance.Balance < PatronPolicy.SwitchCostSouls)
                    return (false, "souls.insufficient", null);
                if (!AppendSoulLedgerUnlocked(db, playerId, 0, -PatronPolicy.SwitchCostSouls,
                        Core.Demons.SoulEarnPolicy.Reasons.Patron, "spend", corr, corr, now))
                    return (false, "correlation.mismatch", null); // corr already bought a different switch
            }

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO rpg_patron(player_id, instance_id, set_utc, revision)
                    VALUES($p, $i, $t, 1)
                    ON CONFLICT(player_id)
                    DO UPDATE SET instance_id = $i, set_utc = $t, revision = revision + 1;
                    """;
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$i", id);
                cmd.Parameters.AddWithValue("$t", now);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return (true, "", ReadPatronUnlocked(db, playerId));
        }
    }

    public PatronRow? GetPatron(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadPatronUnlocked(db, playerId);
        }
    }

    PatronRow? ReadPatronUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT player_id, instance_id, set_utc, revision FROM rpg_patron WHERE player_id=$p;";
        cmd.Parameters.AddWithValue("$p", playerId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new PatronRow(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt64(3)) : null;
    }

    internal bool IsPatronUnlocked(SqliteConnection db, long playerId, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM rpg_patron WHERE player_id=$p AND instance_id=$i LIMIT 1;";
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$i", instanceId);
        return cmd.ExecuteScalar() != null;
    }

    /// <summary>PK point lookup — cheap enough for the per-kill earn hook (no scan, review-C1-safe).</summary>
    internal bool HasPatronUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM rpg_patron WHERE player_id=$p LIMIT 1;";
        cmd.Parameters.AddWithValue("$p", playerId);
        return cmd.ExecuteScalar() != null;
    }
}
