using FusionRpg.Contracts;
using FusionRpg.Core.Demons.Materialise;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>One rolled species effect a player owns — the mapping row `player_species` points at,
/// its instance living in the shared `effect_instance`/`effect_instance_atom` tables so a rolled
/// species effect is the exact same shape any other rolled instance is.</summary>
public sealed record PlayerSpeciesRow(
    long PlayerId, string SpeciesId, string InstanceId, string MaterialisedUtc, long CatalogRevision);

/// <summary>What one materialise call did. `Written` counts only species this call actually rolled —
/// a species already present for this player is neither an error nor rerolled (Q5, append-only).
/// `CatalogRevision` is the revision the compose actually ran against — 0 when the call never reached
/// compose (e.g. the player does not exist), never invented.</summary>
public sealed record PlayerSpeciesMaterialiseOutcome(
    bool Committed, AtomRejection Rejection, int Written, int AlreadyPresent, long ElapsedMs,
    long CatalogRevision = 0)
{
    public bool IsOk => Rejection.IsOk;
}

/// <summary>
/// `player-materialise` (T5.6, `spec-player-materialise.md` §3/§7, demon-seed module 16) — the
/// transactional half. <see cref="SpeciesMaterialiser"/> (Core, pure) does the rolling; this file
/// owns the one place the roster becomes durable: `player_species`, one row per (player, species),
/// pointing at an `effect_instance` row materialised through the exact same tables every other
/// rolled instance uses.
///
/// <para><b>Append-only, all-or-nothing (§3, §7).</b> A species already present for this player is
/// never re-rolled — that is what makes a later catalog retune leave existing rolls untouched, for
/// free, with no version check: the row simply is not a candidate for this call. Every NEW roll for
/// this call is computed first (pure, no I/O) and only written in one transaction after every one of
/// them succeeds — a mid-roster refusal writes nothing, rather than leaving some species with
/// effects and some without.</para>
/// </summary>
public sealed partial class RpgStore
{
    void EnsurePlayerSpeciesSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS player_species (
              player_id INTEGER NOT NULL,
              species_id TEXT NOT NULL,
              instance_id TEXT NOT NULL,
              materialised_utc TEXT NOT NULL,
              catalog_revision INTEGER NOT NULL,
              PRIMARY KEY (player_id, species_id)
            );
            CREATE INDEX IF NOT EXISTS ix_player_species_player ON player_species(player_id);
            """);
    }

    /// <summary>
    /// Roll every species that has a `species-passive.{id}` container today and this player does not
    /// already own, against this player's own `world_seed`. Idempotent by construction — calling it
    /// again with nothing new in the catalog writes nothing and reports zero (matches T5.7's own
    /// "idempotent when the catalog is unchanged" acceptance line, without duplicating logic there).
    /// </summary>
    public PlayerSpeciesMaterialiseOutcome MaterialisePlayerSpecies(
        long playerId, int thetaContent, PowerTuning tuning, string? materialisedUtc = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        PlayerDto? player;
        using (var db = OpenUnlocked())
            player = GetPlayerUnlocked(db, playerId);
        if (player is null)
            return new(false, AtomRejection.Fail(AtomRejectionReason.StaleInstance,
                $"player {playerId} does not exist"), 0, 0, sw.ElapsedMilliseconds);

        var roster = ListSpeciesPassiveContainerIdsUnlocked();
        var existing = ListPlayerSpeciesIdsUnlocked(playerId);

        var toRoll = roster.Where(id => !existing.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

        if (toRoll.Count == 0)
            return new(true, AtomRejection.Ok, 0, existing.Count, sw.ElapsedMilliseconds);

        var catalogRevision = GetCatalogRevision();
        var compose = SpeciesMaterialiser.Materialise(
            toRoll, GetContainer, GetAtom, GetAffix, DomainMembers,
            player.WorldSeed, catalogRevision, thetaContent, tuning, out var rolls);
        if (!compose.IsOk)
            return new(false, compose, 0, existing.Count, sw.ElapsedMilliseconds);

        var utc = materialisedUtc ?? DateTime.UtcNow.ToString("O");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            foreach (var roll in rolls)
            {
                var iid = Guid.NewGuid().ToString("N");

                ExecIn(db, tx, """
                    INSERT INTO effect_instance
                      (instance_id, container_id, roll_seed, catalog_revision, created_utc, origin,
                       theta_content, content_scale_milli)
                    VALUES ($id, $c, $seed, $rev, $utc, $origin, $theta, $scale);
                    """,
                    ("$id", iid), ("$c", roll.Instance.ContainerId), ("$seed", roll.Instance.RollSeed),
                    ("$rev", roll.Instance.CatalogRevision), ("$utc", utc),
                    ("$origin", roll.Instance.Origin.ToString().ToLowerInvariant()),
                    ("$theta", roll.Instance.ThetaContent), ("$scale", roll.Instance.ContentScaleMilli));

                foreach (var a in roll.Instance.Atoms)
                    ExecIn(db, tx,
                        "INSERT INTO effect_instance_atom (instance_id, seq, atom_id, values_json, power_json) " +
                        "VALUES ($id, $seq, $atom, $vals, $power);",
                        ("$id", iid), ("$seq", a.Seq), ("$atom", a.AtomId), ("$vals", a.ValuesJson),
                        ("$power", (object?)a.PowerJson ?? DBNull.Value));

                ExecIn(db, tx, """
                    INSERT INTO player_species (player_id, species_id, instance_id, materialised_utc, catalog_revision)
                    VALUES ($p, $s, $i, $u, $r);
                    """,
                    ("$p", playerId), ("$s", roll.SpeciesId), ("$i", iid), ("$u", utc),
                    ("$r", roll.Instance.CatalogRevision));
            }

            tx.Commit();
        }

        return new(true, AtomRejection.Ok, rolls.Count, existing.Count, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// `dev-reforge` (T5.7, spec-player-materialise.md §6, A4) — re-derive EVERY species this player
    /// owns (plus any new one) against the CURRENT catalog, same world seed. Unlike
    /// <see cref="MaterialisePlayerSpecies"/>, an already-owned species IS a candidate here — that is
    /// the whole point: observing a retuned affix without a new profile. A species already owned
    /// keeps its own `instance_id` (the row is updated in place, not replaced under a new id), which
    /// is what makes two reforges against an unchanged catalog byte-identical, not just
    /// content-equivalent under a different id. Debug surface only — never call this from a player-
    /// facing path (boundaries, §6).
    /// </summary>
    public PlayerSpeciesMaterialiseOutcome ReforgePlayerSpecies(
        long playerId, int thetaContent, PowerTuning tuning, string? materialisedUtc = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        PlayerDto? player;
        using (var db = OpenUnlocked())
            player = GetPlayerUnlocked(db, playerId);
        if (player is null)
            return new(false, AtomRejection.Fail(AtomRejectionReason.StaleInstance,
                $"player {playerId} does not exist"), 0, 0, sw.ElapsedMilliseconds);

        var roster = ListSpeciesPassiveContainerIdsUnlocked();
        var existingInstanceBySpecies = ListPlayerSpeciesInstanceMapUnlocked(playerId);
        var catalogRevision = GetCatalogRevision();

        if (roster.Count == 0)
            return new(true, AtomRejection.Ok, 0, existingInstanceBySpecies.Count, sw.ElapsedMilliseconds,
                catalogRevision);

        var compose = SpeciesMaterialiser.Materialise(
            roster, GetContainer, GetAtom, GetAffix, DomainMembers,
            player.WorldSeed, catalogRevision, thetaContent, tuning, out var rolls);
        if (!compose.IsOk)
            return new(false, compose, 0, existingInstanceBySpecies.Count, sw.ElapsedMilliseconds,
                catalogRevision);

        var utc = materialisedUtc ?? DateTime.UtcNow.ToString("O");

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();

            foreach (var roll in rolls)
            {
                var iid = existingInstanceBySpecies.TryGetValue(roll.SpeciesId, out var owned)
                    ? owned : Guid.NewGuid().ToString("N");

                ExecIn(db, tx, """
                    INSERT INTO effect_instance
                      (instance_id, container_id, roll_seed, catalog_revision, created_utc, origin,
                       theta_content, content_scale_milli)
                    VALUES ($id, $c, $seed, $rev, $utc, $origin, $theta, $scale)
                    ON CONFLICT(instance_id) DO UPDATE SET
                      container_id = excluded.container_id, roll_seed = excluded.roll_seed,
                      catalog_revision = excluded.catalog_revision, origin = excluded.origin,
                      theta_content = excluded.theta_content, content_scale_milli = excluded.content_scale_milli;
                    """,
                    ("$id", iid), ("$c", roll.Instance.ContainerId), ("$seed", roll.Instance.RollSeed),
                    ("$rev", roll.Instance.CatalogRevision), ("$utc", utc),
                    ("$origin", roll.Instance.Origin.ToString().ToLowerInvariant()),
                    ("$theta", roll.Instance.ThetaContent), ("$scale", roll.Instance.ContentScaleMilli));

                ExecIn(db, tx, "DELETE FROM effect_instance_atom WHERE instance_id = $id;", ("$id", iid));
                foreach (var a in roll.Instance.Atoms)
                    ExecIn(db, tx,
                        "INSERT INTO effect_instance_atom (instance_id, seq, atom_id, values_json, power_json) " +
                        "VALUES ($id, $seq, $atom, $vals, $power);",
                        ("$id", iid), ("$seq", a.Seq), ("$atom", a.AtomId), ("$vals", a.ValuesJson),
                        ("$power", (object?)a.PowerJson ?? DBNull.Value));

                ExecIn(db, tx, """
                    INSERT INTO player_species (player_id, species_id, instance_id, materialised_utc, catalog_revision)
                    VALUES ($p, $s, $i, $u, $r)
                    ON CONFLICT(player_id, species_id) DO UPDATE SET
                      instance_id = excluded.instance_id, materialised_utc = excluded.materialised_utc,
                      catalog_revision = excluded.catalog_revision;
                    """,
                    ("$p", playerId), ("$s", roll.SpeciesId), ("$i", iid), ("$u", utc),
                    ("$r", roll.Instance.CatalogRevision));
            }

            tx.Commit();
        }

        return new(true, AtomRejection.Ok, rolls.Count, existingInstanceBySpecies.Count, sw.ElapsedMilliseconds,
            catalogRevision);
    }

    /// <summary>Every species this player owns, in stable order. Each row's `InstanceId` resolves
    /// through <see cref="GetInstance"/> like any other instance — no separate read path.</summary>
    public IReadOnlyList<PlayerSpeciesRow> ListPlayerSpecies(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT player_id, species_id, instance_id, materialised_utc, catalog_revision
                FROM player_species WHERE player_id = $p ORDER BY species_id;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();

            var list = new List<PlayerSpeciesRow>();
            while (r.Read())
                list.Add(new PlayerSpeciesRow(r.GetInt64(0), r.GetString(1), r.GetString(2),
                    r.GetString(3), r.GetInt64(4)));
            return list;
        }
    }

    Dictionary<string, string> ListPlayerSpeciesInstanceMapUnlocked(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT species_id, instance_id FROM player_species WHERE player_id = $p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            while (r.Read()) map[r.GetString(0)] = r.GetString(1);
            return map;
        }
    }

    HashSet<string> ListPlayerSpeciesIdsUnlocked(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT species_id FROM player_species WHERE player_id = $p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();

            var set = new HashSet<string>(StringComparer.Ordinal);
            while (r.Read()) set.Add(r.GetString(0));
            return set;
        }
    }

    /// <summary>Every species with real content today, read off `effect_container` rather than
    /// `demon_species` — the roster to materialise is "what has an effect to roll," and a species can
    /// exist in the shared stat catalog before `species-effects` (T5.3) ships its own container.</summary>
    IReadOnlyList<string> ListSpeciesPassiveContainerIdsUnlocked()
    {
        const string prefix = "species-passive.";

        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT container_id FROM effect_container " +
                "WHERE container_kind = $kind ORDER BY container_id;";
            cmd.Parameters.AddWithValue("$kind", ContainerRow.PrefixOf(ContainerKind.SpeciesPassive));
            using var r = cmd.ExecuteReader();

            var ids = new List<string>();
            while (r.Read())
            {
                var containerId = r.GetString(0);
                if (containerId.StartsWith(prefix, StringComparison.Ordinal))
                    ids.Add(containerId[prefix.Length..]);
            }
            return ids;
        }
    }
}
