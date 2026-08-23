using FusionRpg.Core.Stats;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// One channel's tunable policy — the values, never the identity.
/// </summary>
/// <param name="Direction">0 higher-is-better, 1 lower-is-better.</param>
/// <param name="DefaultValue">The composed baseline when the game reports none.</param>
/// <param name="CapMilli">
/// The ceiling, per-mille. −1 for uncapped. The 0.95 resist cap lived here as a code constant, where
/// changing it moved every battle golden with an <b>unchanged</b> content hash.
/// </param>
public sealed record ChannelPolicyRow(
    string ChannelId, int Direction, int DefaultValue, int CapMilli, string ComposeKind);

/// <summary>
/// <c>effect_channel_policy</c> (E16) — the caps and defaults, as rows.
///
/// <para><b>Values are data; channel identity stays code.</b> That is E1's own rule applied to
/// itself: changing a cap or a default on an existing channel is a value change with a live consumer
/// (<c>DerivedStatRegistry</c>), so it may be a row. Adding a <i>channel</i> needs a new composer
/// case and a new writer case — a new reader — so it stays code. A row that added a channel would be
/// accepted and then do nothing, which is the silent no-op this whole program exists to refuse.</para>
///
/// <para>It joins the content hash at registry <b>v4</b>. Until it did, the 0.95 resist cap was a
/// code constant, and editing it moved every battle golden while the stamp stood still — acceptable
/// only because a constant is visible in a diff, which stops being true the moment it becomes a row.
/// That is exactly why the table and the registry bump ship together.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureChannelPolicySchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS effect_channel_policy (
              channel_id    TEXT    NOT NULL PRIMARY KEY,
              direction     INTEGER NOT NULL DEFAULT 0,
              default_value INTEGER NOT NULL DEFAULT 0,
              cap_milli     INTEGER NOT NULL DEFAULT -1,
              compose_kind  TEXT    NOT NULL DEFAULT 'phased'
            );
            """);
    }

    /// <summary>Policy for every channel, falling back to the shipped defaults for any unwritten one.</summary>
    public IReadOnlyList<ChannelPolicyRow> GetChannelPolicies()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "SELECT channel_id, direction, default_value, cap_milli, compose_kind " +
                "FROM effect_channel_policy ORDER BY channel_id;";
            using var r = cmd.ExecuteReader();

            var stored = new Dictionary<string, ChannelPolicyRow>(StringComparer.Ordinal);
            while (r.Read())
                stored[r.GetString(0)] = new ChannelPolicyRow(
                    r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetString(4));

            return StatChannels.All
                .Select(c => stored.TryGetValue(c, out var row) ? row : ShippedPolicy(c))
                .ToList();
        }
    }

    /// <summary>
    /// Write policy rows. <b>An unknown channel is refused</b>, because a row cannot add one: there
    /// would be no composer case and no writer case to read it, and the row would sit there looking
    /// like a feature.
    /// </summary>
    public (bool Ok, string Reason) UpsertChannelPolicies(IReadOnlyList<ChannelPolicyRow> rows)
    {
        var reason = ValidateChannelPolicyRows(rows);
        if (reason is not null) return (false, reason);

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            var changed = 0;
            foreach (var row in rows) changed += UpsertChannelPolicyRowUnlocked(db, tx, row);

            // C4 (completeness-audit.md): bump only when something actually changed, matching the
            // import path's rule — a bump on a no-op call would make every connected E19 receiver
            // re-download the full push for content that did not move.
            if (changed > 0)
                ExecIn(db, tx, "UPDATE content_meta SET catalog_revision = catalog_revision + 1 WHERE id = 1;");

            tx.Commit();
            return (true, "");
        }
    }

    /// <summary>The check half, on a caller-owned row set — E14a (E22) validates before its own write.</summary>
    static string? ValidateChannelPolicyRows(IReadOnlyList<ChannelPolicyRow> rows)
    {
        foreach (var row in rows)
        {
            if (!Array.Exists(StatChannels.All, c => string.Equals(c, row.ChannelId, StringComparison.Ordinal)))
                return $"'{row.ChannelId}' is not a channel. This table holds a channel's VALUES; adding a "
                       + "channel needs a composer case and a writer case, so it is code (E1's code-or-data rule)";

            if (row.Direction is not (0 or 1))
                return $"{row.ChannelId}: direction {row.Direction} — 0 higher-is-better, 1 lower";
        }

        return null;
    }

    /// <summary>The write half, on a caller-owned transaction. Returns rows actually changed.</summary>
    static int UpsertChannelPolicyRowUnlocked(SqliteConnection db, SqliteTransaction tx, ChannelPolicyRow row)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO effect_channel_policy
              (channel_id, direction, default_value, cap_milli, compose_kind)
            VALUES ($id, $dir, $def, $cap, $kind)
            ON CONFLICT(channel_id) DO UPDATE SET
              direction = excluded.direction, default_value = excluded.default_value,
              cap_milli = excluded.cap_milli, compose_kind = excluded.compose_kind
            WHERE effect_channel_policy.direction IS NOT excluded.direction
               OR effect_channel_policy.default_value IS NOT excluded.default_value
               OR effect_channel_policy.cap_milli IS NOT excluded.cap_milli
               OR effect_channel_policy.compose_kind IS NOT excluded.compose_kind;
            """;
        cmd.Parameters.AddWithValue("$id", row.ChannelId);
        cmd.Parameters.AddWithValue("$dir", row.Direction);
        cmd.Parameters.AddWithValue("$def", row.DefaultValue);
        cmd.Parameters.AddWithValue("$cap", row.CapMilli);
        cmd.Parameters.AddWithValue("$kind", row.ComposeKind ?? "phased");
        return cmd.ExecuteNonQuery();
    }

    /// <summary>What a channel does when nothing has been authored for it.</summary>
    public static ChannelPolicyRow ShippedPolicy(string channelId) => new(
        channelId,
        (int)StatChannels.DirectionOf(channelId),
        DefaultValue: 0,
        CapMilli: -1,
        ComposeKind: "phased");
}
