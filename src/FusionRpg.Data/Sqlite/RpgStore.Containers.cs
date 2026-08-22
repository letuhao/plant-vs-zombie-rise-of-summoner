using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// The tables that let anything own atoms: <c>effect_container</c>, <c>effect_container_atom</c>,
/// <c>effect_container_pool</c>, plus the <c>rarity</c> band table (spec-container-schema.md, E5).
///
/// <para><b>Containers are mechanism, not content.</b> These tables and the published contract ship
/// here; items, traits, skills and world buildings author their own rows when their specs land.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsureContainerSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS effect_container (
              container_id TEXT NOT NULL PRIMARY KEY,
              container_kind TEXT NOT NULL,
              slot TEXT,
              rarity TEXT,
              min_tier INTEGER,
              max_tier INTEGER,
              level_req INTEGER,
              pool_rolls INTEGER NOT NULL DEFAULT 0,
              tags_json TEXT NOT NULL DEFAULT '{}',
              enabled INTEGER NOT NULL DEFAULT 1,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_effect_container_kind ON effect_container(container_kind);

            CREATE TABLE IF NOT EXISTS effect_container_atom (
              container_id TEXT NOT NULL,
              seq INTEGER NOT NULL,
              atom_id TEXT NOT NULL,
              overrides_json TEXT,
              PRIMARY KEY (container_id, seq)
            );

            CREATE TABLE IF NOT EXISTS effect_container_pool (
              container_id TEXT NOT NULL,
              atom_id TEXT NOT NULL,
              weight INTEGER NOT NULL DEFAULT 0,
              group_key TEXT,
              PRIMARY KEY (container_id, atom_id)
            );

            -- Ordinals are explicit and append-only: they are load-bearing for sorting and for the
            -- budget lookup, so a reorder silently re-prices every container naming one.
            CREATE TABLE IF NOT EXISTS rarity (
              rarity_id TEXT NOT NULL PRIMARY KEY,
              ordinal INTEGER NOT NULL UNIQUE,
              pool_rolls INTEGER NOT NULL DEFAULT 0,
              min_tier INTEGER NOT NULL DEFAULT 1,
              max_tier INTEGER NOT NULL DEFAULT 1
            );
            """);
    }

    // ---- rarity ---------------------------------------------------------------------------------

    /// <summary>
    /// Insert or update a rarity band. An ordinal already in use by a <b>different</b> id is refused:
    /// append-only means a band may be added, never renumbered underneath the content that names it.
    /// </summary>
    public (bool Ok, string Reason) UpsertRarity(RarityRow r)
    {
        if (string.IsNullOrWhiteSpace(r.RarityId)) return (false, "rarity_id is empty");
        if (r.MinTier > r.MaxTier) return (false, $"tier window [{r.MinTier}, {r.MaxTier}] is inverted");

        lock (_gate)
        {
            using var db = OpenUnlocked();

            using (var check = db.CreateCommand())
            {
                check.CommandText = "SELECT rarity_id FROM rarity WHERE ordinal = $o AND rarity_id <> $id;";
                check.Parameters.AddWithValue("$o", r.Ordinal);
                check.Parameters.AddWithValue("$id", r.RarityId);
                if (check.ExecuteScalar() is string taken)
                    return (false, $"ordinal {r.Ordinal} already belongs to '{taken}' — ordinals are append-only");
            }

            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO rarity (rarity_id, ordinal, pool_rolls, min_tier, max_tier)
                VALUES ($id, $o, $rolls, $min, $max)
                ON CONFLICT(rarity_id) DO UPDATE SET
                  ordinal = excluded.ordinal, pool_rolls = excluded.pool_rolls,
                  min_tier = excluded.min_tier, max_tier = excluded.max_tier;
                """;
            cmd.Parameters.AddWithValue("$id", r.RarityId);
            cmd.Parameters.AddWithValue("$o", r.Ordinal);
            cmd.Parameters.AddWithValue("$rolls", r.PoolRolls);
            cmd.Parameters.AddWithValue("$min", r.MinTier);
            cmd.Parameters.AddWithValue("$max", r.MaxTier);
            cmd.ExecuteNonQuery();
            return (true, "");
        }
    }

    /// <summary>Bands in ordinal order — the order rarity means.</summary>
    public IReadOnlyList<RarityRow> ListRarities()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "SELECT rarity_id, ordinal, pool_rolls, min_tier, max_tier FROM rarity ORDER BY ordinal;";
            using var r = cmd.ExecuteReader();

            var list = new List<RarityRow>();
            while (r.Read())
                list.Add(new RarityRow(r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
            return list;
        }
    }

    // ---- containers -----------------------------------------------------------------------------

    /// <summary>
    /// Validate then write. A container is stored whole or not at all: its atoms and pool rows are
    /// replaced inside one transaction, so a half-written container cannot be observed.
    /// </summary>
    public AtomRejection UpsertContainer(ContainerRow c)
    {
        var loaded = ListAtoms();
        var byId = loaded.ToDictionary(a => a.AtomId, StringComparer.Ordinal);

        var check = ContainerValidator.Validate(c, id => byId.TryGetValue(id, out var a) ? a : null);
        if (!check.IsOk) return check;

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            ExecIn(db, tx, """
                INSERT INTO effect_container
                  (container_id, container_kind, slot, rarity, min_tier, max_tier, level_req,
                   pool_rolls, tags_json, enabled, revision)
                VALUES ($id, $kind, $slot, $rarity, $min, $max, $lvl, $rolls, $tags, $enabled, 1)
                ON CONFLICT(container_id) DO UPDATE SET
                  container_kind = excluded.container_kind, slot = excluded.slot,
                  rarity = excluded.rarity, min_tier = excluded.min_tier, max_tier = excluded.max_tier,
                  level_req = excluded.level_req, pool_rolls = excluded.pool_rolls,
                  tags_json = excluded.tags_json, enabled = excluded.enabled,
                  revision = effect_container.revision + 1;
                """,
                ("$id", c.ContainerId), ("$kind", KindName(c.Kind)),
                ("$slot", (object?)c.Slot ?? DBNull.Value), ("$rarity", (object?)c.Rarity ?? DBNull.Value),
                ("$min", (object?)c.MinTier ?? DBNull.Value), ("$max", (object?)c.MaxTier ?? DBNull.Value),
                ("$lvl", (object?)c.LevelReq ?? DBNull.Value), ("$rolls", c.PoolRolls),
                ("$tags", c.TagsJson ?? "{}"), ("$enabled", c.Enabled ? 1 : 0));

            // Replace rather than merge: a container's contents are one authored statement, and a
            // stale child row from a previous revision is content nobody wrote.
            ExecIn(db, tx, "DELETE FROM effect_container_atom WHERE container_id = $id;", ("$id", c.ContainerId));
            ExecIn(db, tx, "DELETE FROM effect_container_pool WHERE container_id = $id;", ("$id", c.ContainerId));

            foreach (var a in c.Atoms)
                ExecIn(db, tx,
                    "INSERT INTO effect_container_atom (container_id, seq, atom_id, overrides_json) " +
                    "VALUES ($id, $seq, $atom, $ov);",
                    ("$id", c.ContainerId), ("$seq", a.Seq), ("$atom", a.AtomId),
                    ("$ov", (object?)a.OverridesJson ?? DBNull.Value));

            foreach (var p in c.Pool)
                ExecIn(db, tx,
                    "INSERT INTO effect_container_pool (container_id, atom_id, weight, group_key) " +
                    "VALUES ($id, $atom, $w, $g);",
                    ("$id", c.ContainerId), ("$atom", p.AtomId), ("$w", p.Weight),
                    ("$g", (object?)p.Group ?? DBNull.Value));

            tx.Commit();
            return AtomRejection.Ok;
        }
    }

    public ContainerRow? GetContainer(string containerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();

            ContainerRow? head;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT container_id, container_kind, slot, rarity, min_tier, max_tier, level_req,
                           pool_rolls, tags_json, enabled, revision
                    FROM effect_container WHERE container_id = $id;
                    """;
                cmd.Parameters.AddWithValue("$id", containerId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return null;

                head = new ContainerRow
                {
                    ContainerId = r.GetString(0),
                    Kind = ParseKind(r.GetString(1)),
                    Slot = r.IsDBNull(2) ? null : r.GetString(2),
                    Rarity = r.IsDBNull(3) ? null : r.GetString(3),
                    MinTier = r.IsDBNull(4) ? null : r.GetInt32(4),
                    MaxTier = r.IsDBNull(5) ? null : r.GetInt32(5),
                    LevelReq = r.IsDBNull(6) ? null : r.GetInt32(6),
                    PoolRolls = r.GetInt32(7),
                    TagsJson = r.GetString(8),
                    Enabled = r.GetInt32(9) != 0,
                    Revision = r.GetInt64(10),
                };
            }

            var atoms = new List<ContainerAtomRow>();
            using (var cmd = db.CreateCommand())
            {
                // seq order, always — it is authoring order and it must be stable.
                cmd.CommandText =
                    "SELECT seq, atom_id, overrides_json FROM effect_container_atom " +
                    "WHERE container_id = $id ORDER BY seq;";
                cmd.Parameters.AddWithValue("$id", containerId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    atoms.Add(new ContainerAtomRow(r.GetInt32(0), r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetString(2)));
            }

            var pool = new List<ContainerPoolRow>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT atom_id, weight, group_key FROM effect_container_pool " +
                    "WHERE container_id = $id ORDER BY atom_id;";
                cmd.Parameters.AddWithValue("$id", containerId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    pool.Add(new ContainerPoolRow(r.GetString(0), r.GetInt32(1),
                        r.IsDBNull(2) ? null : r.GetString(2)));
            }

            return head with { Atoms = atoms, Pool = pool };
        }
    }

    /// <summary>Container ids in stable order — E8 hashes these tables.</summary>
    public IReadOnlyList<string> ListContainerIds()
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT container_id FROM effect_container ORDER BY container_id;";
            using var r = cmd.ExecuteReader();

            var list = new List<string>();
            while (r.Read()) list.Add(r.GetString(0));
            return list;
        }
    }

    static void ExecIn(SqliteConnection db, SqliteTransaction tx, string sql,
        params (string Name, object Value)[] args)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    static string KindName(ContainerKind kind) => ContainerRow.PrefixOf(kind);

    static ContainerKind ParseKind(string name) => name switch
    {
        "item" => ContainerKind.Item,
        "trait" => ContainerKind.Trait,
        "skill" => ContainerKind.Skill,
        "species-passive" => ContainerKind.SpeciesPassive,
        "patron" => ContainerKind.Patron,
        "world-buff" => ContainerKind.WorldBuff,
        _ => ContainerKind.Item,
    };
}
