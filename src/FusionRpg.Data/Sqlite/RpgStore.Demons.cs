using System.Text.Json;
using FusionRpg.Contracts;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed partial class RpgStore
{
    /// <summary>
    /// Atomic demon mint: UniqueActor (Roster) + demon profile + codex upsert in ONE transaction —
    /// a failure anywhere leaves nothing (spec-demon-core.md). Returns the specimen and whether
    /// this mint newly discovered the species for the player.
    /// </summary>
    public (DemonSpecimenDto Specimen, bool NewlyDiscovered) MintDemon(long playerId, DemonMintSpec spec)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null)
                throw new InvalidOperationException("player not found");
            using var tx = db.BeginTransaction();
            var specimen = MintDemonUnlocked(db, playerId, spec, DateTime.UtcNow.ToString("o"), out var newlyDiscovered);
            tx.Commit();
            return (specimen, newlyDiscovered);
        }
    }

    /// <summary>Mint inside an open transaction — used by MintDemon and the summon pull.</summary>
    DemonSpecimenDto MintDemonUnlocked(
        SqliteConnection db, long playerId, DemonMintSpec spec, string now, out bool newlyDiscovered)
    {
        // Catalog discipline at the last write gate (spec-demon-core.md): unknown ids reject.
        var speciesId = (spec.SpeciesId ?? "").Trim();
        if (!Core.Demons.DemonSpeciesCatalog.IsKnown(speciesId))
            throw new ArgumentException($"Unknown demon species '{spec.SpeciesId}'.");
        if (spec.Side is not ("plant" or "zombie")) throw new ArgumentException("side must be plant|zombie");
        if (!Core.Demons.DemonRarityIds.TryParse(spec.Rarity, out _))
            throw new ArgumentException($"Unknown rarity '{spec.Rarity}'.");
        if (spec.Variant != "normal" && !Core.Demons.DemonSpeciesCatalog.KnownVariants.Contains(spec.Variant, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown variant '{spec.Variant}'.");
        foreach (var trait in spec.TraitIds)
            if (!Core.Demons.DemonTraitCatalog.IsKnown(trait))
                throw new ArgumentException($"Unknown trait '{trait}'.");
        spec.SpeciesId = speciesId;
        var id = Guid.NewGuid().ToString("N");

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO rpg_unique_actors(
                  instance_id, player_id, side, type_id, phase, level, xp,
                  match_key, last_ptr, deploy_correlation_id, revision, created_utc, updated_utc)
                VALUES($id, $pid, $side, $type, $phase, 1, 0, NULL, NULL, NULL, 0, $now, $now);
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$pid", playerId);
            cmd.Parameters.AddWithValue("$side", spec.Side);
            cmd.Parameters.AddWithValue("$type", spec.GameTypeId);
            cmd.Parameters.AddWithValue("$phase", UniqueActorPhases.Roster);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO rpg_demon_profiles(
                  instance_id, species_id, rarity, variant, element_primary, element_secondary,
                  traits_json, origin, nickname, locked, created_utc, revision)
                VALUES($id, $species, $rarity, $variant, $ep, $es, $traits, $origin, $nick, 0, $now, 0);
                """;
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$species", spec.SpeciesId);
            cmd.Parameters.AddWithValue("$rarity", spec.Rarity);
            cmd.Parameters.AddWithValue("$variant", spec.Variant);
            cmd.Parameters.AddWithValue("$ep", spec.ElementPrimary);
            cmd.Parameters.AddWithValue("$es", (object?)spec.ElementSecondary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$traits", JsonSerializer.Serialize(spec.TraitIds));
            cmd.Parameters.AddWithValue("$origin", spec.Origin);
            cmd.Parameters.AddWithValue("$nick", (object?)NullIfEmpty(spec.Nickname) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }

        newlyDiscovered = UpsertCodexUnlocked(db, playerId, spec.SpeciesId, DemonCodexStates.Discovered, now);
        // Contracts ride the same transaction: a new demon takes a free slot, or arrives unbound
        // when capacity is full (spec-demon-contracts.md).
        AutoBindNewSpecimenUnlocked(db, playerId, id, now);
        return new DemonSpecimenDto
        {
            Actor = ReadUniqueActorUnlocked(db, id)!,
            Profile = ReadDemonProfileUnlocked(db, id)!
        };
    }

    /// <summary>
    /// Codex upsert with the monotonic lattice: discovered &gt; seen, never downgrade
    /// (spec-demon-core.md — a second copy must not turn `discovered` back into `seen`).
    /// Returns true when this call is the first-ever `discovered` for the species.
    /// </summary>
    public bool UpsertDemonCodex(long playerId, string speciesId, string state)
    {
        if (state is not (DemonCodexStates.Seen or DemonCodexStates.Discovered))
            throw new ArgumentException("state must be seen|discovered");
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var tx = db.BeginTransaction();
            var now = DateTime.UtcNow.ToString("o");
            var result = UpsertCodexUnlocked(db, playerId, speciesId.Trim(), state, now);
            tx.Commit();
            return result;
        }
    }

    bool UpsertCodexUnlocked(SqliteConnection db, long playerId, string speciesId, string state, string now)
    {
        string? existing = null;
        using (var read = db.CreateCommand())
        {
            read.CommandText = "SELECT state FROM rpg_demon_codex WHERE player_id=$pid AND species_id=$sid;";
            read.Parameters.AddWithValue("$pid", playerId);
            read.Parameters.AddWithValue("$sid", speciesId);
            existing = read.ExecuteScalar() as string;
        }

        var newlyDiscovered = state == DemonCodexStates.Discovered && existing != DemonCodexStates.Discovered;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_demon_codex(player_id, species_id, state, first_utc, updated_utc)
            VALUES($pid, $sid, $state, $now, $now)
            ON CONFLICT(player_id, species_id) DO UPDATE SET
              state = CASE WHEN rpg_demon_codex.state = 'discovered' THEN 'discovered' ELSE excluded.state END,
              updated_utc = excluded.updated_utc;
            """;
        cmd.Parameters.AddWithValue("$pid", playerId);
        cmd.Parameters.AddWithValue("$sid", speciesId);
        cmd.Parameters.AddWithValue("$state", state);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.ExecuteNonQuery();
        return newlyDiscovered;
    }

    public DemonProfileDto? GetDemonProfile(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        lock (_gate)
        {
            using var db = OpenUnlocked();
            return ReadDemonProfileUnlocked(db, instanceId.Trim());
        }
    }

    public DemonRosterDto ListDemonRoster(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var items = new List<DemonSpecimenDto>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT a.instance_id, a.player_id, a.side, a.type_id, a.phase, a.level, a.xp,
                       a.match_key, a.last_ptr, a.deploy_correlation_id, a.revision, a.created_utc, a.updated_utc,
                       p.species_id, p.rarity, p.variant, p.element_primary, p.element_secondary,
                       p.traits_json, p.origin, p.nickname, p.locked, p.created_utc, p.revision,
                       p.star, p.promoted
                FROM rpg_unique_actors a
                JOIN rpg_demon_profiles p ON p.instance_id = a.instance_id
                WHERE a.player_id = $pid AND a.phase != 'Retired'
                ORDER BY a.created_utc ASC;
                """;
            cmd.Parameters.AddWithValue("$pid", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                items.Add(new DemonSpecimenDto
                {
                    Actor = MapUniqueActor(r),
                    Profile = new DemonProfileDto
                    {
                        InstanceId = r.GetString(0),
                        SpeciesId = r.GetString(13),
                        Rarity = r.GetString(14),
                        Variant = r.GetString(15),
                        ElementPrimary = r.GetString(16),
                        ElementSecondary = r.IsDBNull(17) ? null : r.GetString(17),
                        TraitIds = JsonSerializer.Deserialize<List<string>>(r.GetString(18)) ?? new List<string>(),
                        Origin = r.GetString(19),
                        Nickname = r.IsDBNull(20) ? null : r.GetString(20),
                        Locked = r.GetInt64(21) != 0,
                        CreatedUtc = r.GetString(22),
                        Revision = r.GetInt64(23),
                        Star = r.GetInt32(24),
                        Promoted = r.GetInt64(25) != 0
                    }
                });
            }

            return new DemonRosterDto { PlayerId = playerId, Items = items };
        }
    }

    public DemonCodexDto ListDemonCodex(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var entries = new List<DemonCodexEntryDto>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT species_id, state, first_utc, updated_utc
                FROM rpg_demon_codex WHERE player_id = $pid ORDER BY species_id;
                """;
            cmd.Parameters.AddWithValue("$pid", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                entries.Add(new DemonCodexEntryDto
                {
                    SpeciesId = r.GetString(0),
                    State = r.GetString(1),
                    FirstUtc = r.GetString(2),
                    UpdatedUtc = r.GetString(3)
                });
            }

            return new DemonCodexDto { PlayerId = playerId, Entries = entries };
        }
    }

    public (bool Ok, DemonProfileDto? Profile) SetDemonNickname(string instanceId, string? nickname)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                UPDATE rpg_demon_profiles SET nickname = $nick, revision = revision + 1
                WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$nick", (object?)NullIfEmpty(nickname) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", instanceId.Trim());
            var n = cmd.ExecuteNonQuery();
            return n > 0 ? (true, ReadDemonProfileUnlocked(db, instanceId.Trim())) : (false, null);
        }
    }

    public (bool Ok, DemonProfileDto? Profile) SetDemonLocked(string instanceId, bool locked)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                UPDATE rpg_demon_profiles SET locked = $locked, revision = revision + 1
                WHERE instance_id = $id;
                """;
            cmd.Parameters.AddWithValue("$locked", locked ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", instanceId.Trim());
            var n = cmd.ExecuteNonQuery();
            return n > 0 ? (true, ReadDemonProfileUnlocked(db, instanceId.Trim())) : (false, null);
        }
    }

    DemonProfileDto? ReadDemonProfileUnlocked(SqliteConnection db, string instanceId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT instance_id, species_id, rarity, variant, element_primary, element_secondary,
                   traits_json, origin, nickname, locked, created_utc, revision, star, promoted
            FROM rpg_demon_profiles WHERE instance_id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", instanceId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new DemonProfileDto
        {
            InstanceId = r.GetString(0),
            SpeciesId = r.GetString(1),
            Rarity = r.GetString(2),
            Variant = r.GetString(3),
            ElementPrimary = r.GetString(4),
            ElementSecondary = r.IsDBNull(5) ? null : r.GetString(5),
            TraitIds = JsonSerializer.Deserialize<List<string>>(r.GetString(6)) ?? new List<string>(),
            Origin = r.GetString(7),
            Nickname = r.IsDBNull(8) ? null : r.GetString(8),
            Locked = r.GetInt64(9) != 0,
            CreatedUtc = r.GetString(10),
            Revision = r.GetInt64(11),
            Star = r.GetInt32(12),
            Promoted = r.GetInt64(13) != 0
        };
    }
}
