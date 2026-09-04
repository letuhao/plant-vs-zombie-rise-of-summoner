using System.Text.Json;
using FusionRpg.Core.Items.Drops;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One <c>item_drop_log</c> row — idempotency, replay, and the inflow measurement module 20's
/// loot filter needs.</summary>
public sealed record ItemDropLogRow(
    long Id, string PlayerId, string CorrelationId, string SourceKind, string SourceId,
    string LootSeed, long CatalogRevision, long DropTableRevision, int ItemLevel,
    string ContextJson, string ResultJson, string Notes, string CreatedUtc);

/// <summary>
/// One <c>item_generation</c> row — the per-instance stamp, written once and never updated.
///
/// <para>⛔ <b>There is no <c>socket_count</c> column, and its absence is the design.</b> It was a
/// third copy of one fact: module 16 derives the count from <c>DeriveStream(roll_seed, "item.socket")</c>
/// and states that nothing is stored ("nothing is stored, so nothing can drift"), and D2 §6 makes
/// <c>item_socket</c> the SSOT — "it is not a materialized view of anything". Three copies is how a
/// socket count silently disagrees with the sockets an item has. The columns that DO stay are
/// decisions the pipeline made that nothing else records.</para>
/// </summary>
public sealed record ItemGenerationRow(
    string InstanceId, long DropLogId, string BaseTypeId, int RarityOrdinal, int ItemLevel,
    string Frame, string Role, string AffixChannel);

public sealed partial class RpgStore
{
    // ---- loot (module 11, drop-volume) --------------------------------------------------------------

    void EnsureLootSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            -- ssot-generation.md §5.1, with spec-drop-volume.md's two corrections applied:
            --   * item_loot_pity keys on RUNG IDS (heirloom/sunwoven), not I12's seven-rung r4/r6 labels
            --   * item_generation drops socket_count (item_socket is the SSOT, D2 §6)
            -- Consumers named per SC7. Nothing here is a row without a reader.

            -- WHO points at WHICH table, and what level the content is.
            -- Consumer: LootPipeline step 4, and the FE's "where does this drop" panel.
            CREATE TABLE IF NOT EXISTS loot_source (
              source_kind        TEXT NOT NULL,
              source_id          TEXT NOT NULL,
              table_id           TEXT NOT NULL,
              content_level      INTEGER NOT NULL,
              first_clear_grant  TEXT,
              PRIMARY KEY (source_kind, source_id)
            );

            -- source_allow MUST contain 'web' -- standalone-first (§4.6 rule 2), enforced at import
            -- by DropTableValidator, not by a promise in prose.
            CREATE TABLE IF NOT EXISTS drop_table (
              table_id      TEXT PRIMARY KEY,
              source_allow  TEXT NOT NULL,
              min_ilvl      INTEGER,
              max_ilvl      INTEGER,
              enabled       INTEGER NOT NULL DEFAULT 1,
              revision      INTEGER NOT NULL DEFAULT 0
            );

            -- A group is an INDEPENDENT draw unit -- the opposite of effect_container_pool.group,
            -- which is an EXCLUSION unit. `rolls` is the PRE-SCALE count step 5a reads.
            CREATE TABLE IF NOT EXISTS drop_table_group (
              table_id   TEXT NOT NULL,
              group_key  TEXT NOT NULL,
              seq        INTEGER NOT NULL,
              rolls      INTEGER NOT NULL DEFAULT 1,
              PRIMARY KEY (table_id, group_key)
            );

            -- affix_channel is X4's supply, declared HERE and never on the affix: the channel is a
            -- call-site fact, and storing it on the affix would make the affix single-source and
            -- rebuild the problem one level down.
            CREATE TABLE IF NOT EXISTS drop_table_entry (
              table_id                 TEXT NOT NULL,
              group_key                TEXT NOT NULL,
              seq                      INTEGER NOT NULL,
              entry_kind               TEXT NOT NULL,
              ref_id                   TEXT NOT NULL DEFAULT '',
              weight                   INTEGER NOT NULL,
              min_count                INTEGER NOT NULL DEFAULT 1,
              max_count                INTEGER NOT NULL DEFAULT 1,
              min_ilvl                 INTEGER,
              max_ilvl                 INTEGER,
              rarity_floor             TEXT,
              rarity_weight_shift_json TEXT,
              enabled                  INTEGER NOT NULL DEFAULT 1,
              affix_channel            TEXT NOT NULL DEFAULT 'drop',
              frame                    TEXT,
              role                     TEXT,
              PRIMARY KEY (table_id, group_key, seq)
            );

            CREATE TABLE IF NOT EXISTS item_drop_log (
              id                  INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id           TEXT NOT NULL,
              correlation_id      TEXT NOT NULL,
              source_kind         TEXT NOT NULL,
              source_id           TEXT NOT NULL,
              loot_seed           TEXT NOT NULL,
              catalog_revision    INTEGER NOT NULL,
              drop_table_revision INTEGER NOT NULL,
              item_level          INTEGER NOT NULL,
              context_json        TEXT NOT NULL,
              result_json         TEXT NOT NULL,
              notes               TEXT NOT NULL DEFAULT '',
              t                   TEXT NOT NULL,
              UNIQUE(player_id, correlation_id)
            );

            -- No socket_count column. See ItemGenerationRow's own doc comment.
            CREATE TABLE IF NOT EXISTS item_generation (
              instance_id     TEXT PRIMARY KEY,
              drop_log_id     INTEGER NOT NULL,
              base_type_id    TEXT NOT NULL,
              rarity_ordinal  INTEGER NOT NULL,
              item_level      INTEGER NOT NULL,
              frame           TEXT NOT NULL,
              role            TEXT NOT NULL,
              affix_channel   TEXT NOT NULL DEFAULT 'drop'
            );

            -- Correction 5: keyed on RUNG IDS. I12's items_since_r4 / items_since_r6 name a
            -- seven-rung ladder that no longer exists; module 7's ten-rung table guards ordinals
            -- 70 (heirloom) and 90 (sunwoven). The string id is the join.
            CREATE TABLE IF NOT EXISTS item_loot_pity (
              player_id              TEXT PRIMARY KEY,
              items_since_heirloom   INTEGER NOT NULL DEFAULT 0,
              items_since_sunwoven   INTEGER NOT NULL DEFAULT 0,
              updated_utc            TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS item_first_clear (
              player_id    TEXT NOT NULL,
              source_kind  TEXT NOT NULL,
              source_id    TEXT NOT NULL,
              granted_utc  TEXT NOT NULL,
              PRIMARY KEY (player_id, source_kind, source_id)
            );

            CREATE INDEX IF NOT EXISTS ix_item_drop_log_player_t ON item_drop_log(player_id, t);
            CREATE INDEX IF NOT EXISTS ix_item_generation_drop_log ON item_generation(drop_log_id);
            """);
    }

    /// <summary>
    /// Replace the loaded loot corpus in one transaction. Validated FIRST and whole — E14's policy is
    /// all-or-nothing: one bad row and nothing is imported.
    /// </summary>
    public void ImportLootCorpus(LootCorpus corpus, DropVolumeTuning tuning, DropContentLookups? lookups = null)
    {
        if (corpus is null) throw new ArgumentNullException(nameof(corpus));

        var check = DropTableValidator.Validate(corpus.Sources, corpus.Tables, tuning, lookups);
        if (!check.IsOk)
            throw new InvalidOperationException($"loot corpus rejected: {check}");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            LootExec(db, tx, "DELETE FROM drop_table_entry;");
            LootExec(db, tx, "DELETE FROM drop_table_group;");
            LootExec(db, tx, "DELETE FROM drop_table;");
            LootExec(db, tx, "DELETE FROM loot_source;");

            foreach (var t in corpus.Tables)
            {
                LootExec(db, tx, """
                    INSERT INTO drop_table (table_id, source_allow, min_ilvl, max_ilvl, enabled, revision)
                    VALUES ($id, $allow, $lo, $hi, $en, $rev);
                    """,
                    ("$id", t.TableId), ("$allow", string.Join(",", t.SourceAllow)),
                    ("$lo", (object?)t.MinIlvl ?? DBNull.Value), ("$hi", (object?)t.MaxIlvl ?? DBNull.Value),
                    ("$en", t.Enabled ? 1 : 0), ("$rev", t.Revision));

                foreach (var g in t.Groups)
                {
                    LootExec(db, tx, """
                        INSERT INTO drop_table_group (table_id, group_key, seq, rolls)
                        VALUES ($id, $gk, $seq, $rolls);
                        """,
                        ("$id", t.TableId), ("$gk", g.GroupKey), ("$seq", g.Seq), ("$rolls", g.Rolls));

                    foreach (var e in g.Entries)
                        LootExec(db, tx, """
                            INSERT INTO drop_table_entry
                              (table_id, group_key, seq, entry_kind, ref_id, weight, min_count, max_count,
                               min_ilvl, max_ilvl, rarity_floor, rarity_weight_shift_json, enabled,
                               affix_channel, frame, role)
                            VALUES ($id, $gk, $seq, $kind, $ref, $w, $minc, $maxc, $lo, $hi, $floor,
                                    $shift, $en, $chan, $frame, $role);
                            """,
                            ("$id", t.TableId), ("$gk", g.GroupKey), ("$seq", e.Seq),
                            ("$kind", LootCorpusReader.KindName(e.Kind)), ("$ref", e.RefId), ("$w", e.Weight),
                            ("$minc", e.MinCount), ("$maxc", e.MaxCount),
                            ("$lo", (object?)e.MinIlvl ?? DBNull.Value), ("$hi", (object?)e.MaxIlvl ?? DBNull.Value),
                            ("$floor", (object?)e.RarityFloor ?? DBNull.Value),
                            ("$shift", e.RarityWeightShift is { Count: > 0 }
                                ? JsonSerializer.Serialize(e.RarityWeightShift.ToDictionary(k => k.Key.ToString(), v => v.Value))
                                : (object)DBNull.Value),
                            ("$en", e.Enabled ? 1 : 0), ("$chan", e.AffixChannel),
                            ("$frame", (object?)e.Frame ?? DBNull.Value), ("$role", (object?)e.Role ?? DBNull.Value));
                }
            }

            foreach (var s in corpus.Sources)
                LootExec(db, tx, """
                    INSERT INTO loot_source (source_kind, source_id, table_id, content_level, first_clear_grant)
                    VALUES ($k, $i, $t, $lvl, $grant);
                    """,
                    ("$k", s.SourceKind), ("$i", s.SourceId), ("$t", s.TableId),
                    ("$lvl", s.ContentLevel), ("$grant", (object?)s.FirstClearGrant ?? DBNull.Value));

            tx.Commit();
        }
    }

    public LootCorpus LoadLootCorpus()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();

            var entriesByGroup = new Dictionary<(string, string), List<DropTableEntryRow>>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT table_id, group_key, seq, entry_kind, ref_id, weight, min_count, max_count,
                           min_ilvl, max_ilvl, rarity_floor, rarity_weight_shift_json, enabled,
                           affix_channel, frame, role
                    FROM drop_table_entry ORDER BY table_id, group_key, seq;
                    """;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (!LootCorpusReader.TryKind(r.GetString(3), out var kind))
                        throw new InvalidOperationException($"drop_table_entry carries unknown entry_kind '{r.GetString(3)}'");

                    Dictionary<int, int>? shift = null;
                    if (!r.IsDBNull(11))
                    {
                        shift = new Dictionary<int, int>();
                        foreach (var kv in JsonSerializer.Deserialize<Dictionary<string, int>>(r.GetString(11))!)
                            shift[int.Parse(kv.Key)] = kv.Value;
                    }

                    var key = (r.GetString(0), r.GetString(1));
                    if (!entriesByGroup.TryGetValue(key, out var list))
                        entriesByGroup[key] = list = new List<DropTableEntryRow>();

                    list.Add(new DropTableEntryRow(
                        r.GetInt32(2), kind, r.GetString(4), r.GetInt32(5), r.GetInt32(6), r.GetInt32(7),
                        r.IsDBNull(8) ? null : r.GetInt32(8), r.IsDBNull(9) ? null : r.GetInt32(9),
                        r.IsDBNull(10) ? null : r.GetString(10), shift, r.GetInt32(12) != 0,
                        r.GetString(13), r.IsDBNull(14) ? null : r.GetString(14),
                        r.IsDBNull(15) ? null : r.GetString(15)));
                }
            }

            var groupsByTable = new Dictionary<string, List<DropTableGroupRow>>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT table_id, group_key, seq, rolls FROM drop_table_group ORDER BY table_id, seq;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var tableId = r.GetString(0);
                    var groupKey = r.GetString(1);
                    if (!groupsByTable.TryGetValue(tableId, out var list))
                        groupsByTable[tableId] = list = new List<DropTableGroupRow>();
                    list.Add(new DropTableGroupRow(groupKey, r.GetInt32(2), r.GetInt32(3),
                        entriesByGroup.TryGetValue((tableId, groupKey), out var es)
                            ? es
                            : new List<DropTableEntryRow>()));
                }
            }

            var tables = new List<DropTableRow>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT table_id, source_allow, min_ilvl, max_ilvl, enabled, revision FROM drop_table ORDER BY table_id;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var tableId = r.GetString(0);
                    tables.Add(new DropTableRow(
                        tableId,
                        r.GetString(1).Split(',', StringSplitOptions.RemoveEmptyEntries),
                        r.IsDBNull(2) ? null : r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt32(3),
                        r.GetInt32(4) != 0, r.GetInt64(5),
                        groupsByTable.TryGetValue(tableId, out var gs) ? gs : new List<DropTableGroupRow>()));
                }
            }

            var sources = new List<LootSourceRow>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT source_kind, source_id, table_id, content_level, first_clear_grant FROM loot_source ORDER BY source_kind, source_id;";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    sources.Add(new LootSourceRow(r.GetString(0), r.GetString(1), r.GetString(2),
                        r.GetInt32(3), r.IsDBNull(4) ? null : r.GetString(4)));
            }

            return new LootCorpus(sources, tables);
        }
    }

    public LootPityState GetLootPity(string playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT items_since_heirloom, items_since_sunwoven FROM item_loot_pity WHERE player_id = $p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? new LootPityState(r.GetInt64(0), r.GetInt64(1)) : LootPityState.Empty;
        }
    }

    public bool HasFirstClear(string playerId, string sourceKind, string sourceId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM item_first_clear WHERE player_id = $p AND source_kind = $k AND source_id = $i;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$k", sourceKind);
            cmd.Parameters.AddWithValue("$i", sourceId);
            return cmd.ExecuteScalar() is not null;
        }
    }

    /// <summary>Step 1's gate, as the store sees it: the recorded manifest for an already-resolved
    /// (player, correlation) pair, or <c>null</c>.</summary>
    public string? RecordedLootManifest(string playerId, string correlationId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT result_json FROM item_drop_log WHERE player_id = $p AND correlation_id = $c;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$c", correlationId);
            return cmd.ExecuteScalar() as string;
        }
    }

    /// <summary>
    /// Step 11 — <b>ONE transaction</b>: the drop log, every <c>item_generation</c> stamp, the pity
    /// update and the first-clear mark, committed together or not at all.
    ///
    /// <para>The summoning flow already paid for this lesson (its two-transaction bug), <b>with one
    /// extra hazard: nothing is spent here, so a partial commit mints FREE items rather than losing
    /// paid ones.</b></para>
    ///
    /// <para>A retry mints nothing: <c>UNIQUE(player_id, correlation_id)</c> is the second net under
    /// the pipeline's own gate, and this returns the recorded id rather than inserting a second row.</para>
    /// </summary>
    public long PersistLoot(
        string playerId, LootManifest manifest, string sourceKind, string sourceId,
        long catalogRevision, long dropTableRevision,
        IReadOnlyList<ItemGenerationRow> generations, string? nowUtc = null)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        generations ??= Array.Empty<ItemGenerationRow>();
        var t = nowUtc ?? DateTime.UtcNow.ToString("o");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            using (var existing = db.CreateCommand())
            {
                existing.Transaction = tx;
                existing.CommandText = "SELECT id FROM item_drop_log WHERE player_id = $p AND correlation_id = $c;";
                existing.Parameters.AddWithValue("$p", playerId);
                existing.Parameters.AddWithValue("$c", manifest.CorrelationId);
                if (existing.ExecuteScalar() is { } already)
                {
                    tx.Commit();
                    return Convert.ToInt64(already);
                }
            }

            LootExec(db, tx, """
                INSERT INTO item_drop_log
                  (player_id, correlation_id, source_kind, source_id, loot_seed, catalog_revision,
                   drop_table_revision, item_level, context_json, result_json, notes, t)
                VALUES ($p, $c, $sk, $si, $seed, $cat, $rev, $ilvl, $ctx, $res, $notes, $t);
                """,
                ("$p", playerId), ("$c", manifest.CorrelationId), ("$sk", sourceKind), ("$si", sourceId),
                ("$seed", manifest.LootSeed.ToString()), ("$cat", catalogRevision), ("$rev", dropTableRevision),
                ("$ilvl", manifest.ItemLevel), ("$ctx", manifest.ContextJson),
                ("$res", JsonSerializer.Serialize(manifest.Grants)),
                ("$notes", string.Join(",", manifest.Notes)), ("$t", t));

            long logId;
            using (var idCmd = db.CreateCommand())
            {
                idCmd.Transaction = tx;
                idCmd.CommandText = "SELECT last_insert_rowid();";
                logId = Convert.ToInt64(idCmd.ExecuteScalar());
            }

            foreach (var g in generations)
                LootExec(db, tx, """
                    INSERT INTO item_generation
                      (instance_id, drop_log_id, base_type_id, rarity_ordinal, item_level, frame, role, affix_channel)
                    VALUES ($iid, $log, $bt, $ord, $ilvl, $frame, $role, $chan);
                    """,
                    ("$iid", g.InstanceId), ("$log", logId), ("$bt", g.BaseTypeId), ("$ord", g.RarityOrdinal),
                    ("$ilvl", g.ItemLevel), ("$frame", g.Frame), ("$role", g.Role), ("$chan", g.AffixChannel));

            LootExec(db, tx, """
                INSERT INTO item_loot_pity (player_id, items_since_heirloom, items_since_sunwoven, updated_utc)
                VALUES ($p, $h, $s, $t)
                ON CONFLICT(player_id) DO UPDATE SET
                  items_since_heirloom = excluded.items_since_heirloom,
                  items_since_sunwoven = excluded.items_since_sunwoven,
                  updated_utc = excluded.updated_utc;
                """,
                ("$p", playerId), ("$h", manifest.PityOut.ItemsSinceHeirloom),
                ("$s", manifest.PityOut.ItemsSinceSunwoven), ("$t", t));

            if (manifest.FirstClearGrant is { Length: > 0 })
                LootExec(db, tx, """
                    INSERT INTO item_first_clear (player_id, source_kind, source_id, granted_utc)
                    VALUES ($p, $k, $i, $t)
                    ON CONFLICT(player_id, source_kind, source_id) DO NOTHING;
                    """,
                    ("$p", playerId), ("$k", sourceKind), ("$i", sourceId), ("$t", t));

            tx.Commit();
            return logId;
        }
    }

    public IReadOnlyList<ItemDropLogRow> ListDropLog(string playerId, int limit = 100)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT id, player_id, correlation_id, source_kind, source_id, loot_seed, catalog_revision,
                       drop_table_revision, item_level, context_json, result_json, notes, t
                FROM item_drop_log WHERE player_id = $p ORDER BY id DESC LIMIT $n;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$n", limit);
            using var r = cmd.ExecuteReader();
            var rows = new List<ItemDropLogRow>();
            while (r.Read())
                rows.Add(new ItemDropLogRow(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetString(4), r.GetString(5), r.GetInt64(6), r.GetInt64(7), r.GetInt32(8),
                    r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12)));
            return rows;
        }
    }

    public IReadOnlyList<ItemGenerationRow> ListGenerations(long dropLogId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT instance_id, drop_log_id, base_type_id, rarity_ordinal, item_level, frame, role, affix_channel
                FROM item_generation WHERE drop_log_id = $id ORDER BY instance_id;
                """;
            cmd.Parameters.AddWithValue("$id", dropLogId);
            using var r = cmd.ExecuteReader();
            var rows = new List<ItemGenerationRow>();
            while (r.Read())
                rows.Add(new ItemGenerationRow(r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetInt32(3),
                    r.GetInt32(4), r.GetString(5), r.GetString(6), r.GetString(7)));
            return rows;
        }
    }

    /// <summary>
    /// The inflow measurement module 20's loot filter needs — I12 §8's `40/day` tripwire read as
    /// written. ⛔ It is a <b>measurement</b>, never a counter that could become a gate: this method
    /// only reads, and nothing in the pipeline consults it.
    /// </summary>
    public int CountEquipmentMinted(string playerId, string sinceUtc)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM item_generation g
                JOIN item_drop_log l ON l.id = g.drop_log_id
                WHERE l.player_id = $p AND l.t >= $since;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$since", sinceUtc);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    /// <summary>
    /// The watermarked tail-trim, shipped on day one rather than deferred — the soul ledger already
    /// paid for that lesson. What trims is <c>context_json</c> / <c>result_json</c> beyond the
    /// horizon; the ROW stays, so <see cref="CountEquipmentMinted"/> keeps working and
    /// <c>item_generation</c> remains the permanent record. The horizon is the owner's
    /// (<c>log.retentionHorizonDays</c> in the tuning file).
    /// </summary>
    public int TrimDropLog(string beforeUtc)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            int affected;
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE item_drop_log SET context_json = '{}', result_json = '[]'
                    WHERE t < $before AND (context_json <> '{}' OR result_json <> '[]');
                    """;
                cmd.Parameters.AddWithValue("$before", beforeUtc);
                affected = cmd.ExecuteNonQuery();
            }

            tx.Commit();
            return affected;
        }
    }

    static void LootExec(SqliteConnection db, SqliteTransaction tx, string sql,
        params (string Name, object Value)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
