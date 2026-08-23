using System.Text.Json;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

/// <summary>
/// World map storage (spec-world-model.md §Data). The store persists the world; it never
/// reinterprets it — a load must round-trip byte-identically through <see cref="WorldCanonical"/>,
/// and every read comes back in stable id order because ordering is a validation rule.
/// </summary>
public sealed partial class RpgStore
{
    /// <summary>The seven world tables. Called from EnsureHotSchema so a fresh database has them.</summary>
    void EnsureWorldSchemaUnlocked(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS rpg_worlds (
              world_id TEXT NOT NULL PRIMARY KEY,
              player_id INTEGER NOT NULL,
              template_id TEXT NOT NULL,
              seed TEXT NOT NULL,
              mode TEXT NOT NULL DEFAULT 'turn',
              turn_period_seconds INTEGER,
              catch_up_cap INTEGER,
              current_turn INTEGER NOT NULL DEFAULT 0,
              last_advanced_utc TEXT,
              engine_version INTEGER NOT NULL DEFAULT 1,
              ruleset_version INTEGER NOT NULL DEFAULT 1,
              state TEXT NOT NULL DEFAULT 'active',
              created_utc TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_worlds_player ON rpg_worlds(player_id, state);
            CREATE TABLE IF NOT EXISTS rpg_world_factions (
              world_id TEXT NOT NULL,
              faction_id TEXT NOT NULL,
              kind TEXT NOT NULL,
              name TEXT NOT NULL,
              policy_id TEXT,
              PRIMARY KEY (world_id, faction_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_sectors (
              world_id TEXT NOT NULL,
              sector_id TEXT NOT NULL,
              type_id TEXT NOT NULL,
              climate TEXT,
              danger_band INTEGER NOT NULL DEFAULT 0,
              phase TEXT NOT NULL,
              owner_faction_id TEXT,
              stability_milli INTEGER NOT NULL DEFAULT 0,
              pressure_milli INTEGER NOT NULL DEFAULT 0,
              depletion_milli INTEGER NOT NULL DEFAULT 0,
              development_level INTEGER NOT NULL DEFAULT 0,
              intel TEXT NOT NULL,
              last_seen_turn INTEGER NOT NULL DEFAULT 0,
              layout_x INTEGER NOT NULL DEFAULT 0,
              layout_y INTEGER NOT NULL DEFAULT 0,
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (world_id, sector_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_slots (
              world_id TEXT NOT NULL,
              sector_id TEXT NOT NULL,
              slot_index INTEGER NOT NULL,
              slot_type_id TEXT NOT NULL,
              element TEXT,
              state TEXT NOT NULL,
              owner_faction_id TEXT,
              guard_wave_id TEXT,
              guard_state TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (world_id, sector_id, slot_index)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_lanes (
              world_id TEXT NOT NULL,
              lane_id TEXT NOT NULL,
              from_sector_id TEXT NOT NULL,
              to_sector_id TEXT NOT NULL,
              type_id TEXT NOT NULL,
              length INTEGER NOT NULL DEFAULT 1000,
              width INTEGER NOT NULL DEFAULT 1000,
              hazard_milli INTEGER NOT NULL DEFAULT 0,
              ward_level INTEGER NOT NULL DEFAULT 0,
              gate_key_id TEXT,
              state TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (world_id, lane_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_entities (
              world_id TEXT NOT NULL,
              entity_id TEXT NOT NULL,
              kind TEXT NOT NULL,
              owner_faction_id TEXT NOT NULL,
              at_sector_id TEXT,
              on_lane_id TEXT,
              on_lane_toward_sector_id TEXT,
              lane_progress_milli INTEGER NOT NULL DEFAULT 0,
              stance TEXT NOT NULL,
              movement_remaining INTEGER NOT NULL DEFAULT 0,
              routed INTEGER NOT NULL DEFAULT 0,
              revision INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (world_id, entity_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_faction_intel (
              world_id TEXT NOT NULL,
              faction_id TEXT NOT NULL,
              sector_id TEXT NOT NULL,
              last_seen_turn INTEGER NOT NULL,
              detail TEXT NOT NULL,
              owner_faction_id TEXT,
              phase TEXT NOT NULL,
              climate TEXT,
              danger_band INTEGER NOT NULL,
              slots_json TEXT NOT NULL,
              forces_json TEXT NOT NULL,
              PRIMARY KEY (world_id, faction_id, sector_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_world_entity_members (
              world_id TEXT NOT NULL,
              entity_id TEXT NOT NULL,
              member_index INTEGER NOT NULL,
              instance_id TEXT,
              species_id TEXT NOT NULL,
              level INTEGER NOT NULL DEFAULT 1,
              hp INTEGER NOT NULL DEFAULT 0,
              wounds INTEGER NOT NULL DEFAULT 0,
              PRIMARY KEY (world_id, entity_id, member_index)
            );
            """);

        // Additive columns go through EnsureColumn, not the CREATE above: an existing database
        // never re-runs CREATE TABLE, so a field added there alone would be missing for anyone who
        // already has a world.
        EnsureColumn(db, "rpg_world_entities", "on_lane_toward_sector_id", "TEXT");
        EnsureColumn(db, "rpg_world_entities", "routed", "INTEGER NOT NULL DEFAULT 0");

        // spec-loam-model.md: an existing saved world reads back as "no stock, baseline Fracture,
        // no handicap" — exactly the pre-loam world, so this is the correct migration.
        EnsureColumn(db, "rpg_world_sectors", "loam_stock", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_world_sectors", "fracture_intensity_milli", "INTEGER NOT NULL DEFAULT 1000");
        EnsureColumn(db, "rpg_world_factions", "upkeep_handicap_milli", "INTEGER NOT NULL DEFAULT 1000");

        // Post-gate L25 (spec-loam-legions.md, spec-structure-substrate.md, spec-loam-texture.md):
        // an existing saved world reads every one of these back at its shipped default — no
        // structure, no construction in progress, no warden, no neglect — exactly the world before
        // this batch of fields existed.
        EnsureColumn(db, "rpg_world_slots", "structure_id", "TEXT");
        EnsureColumn(db, "rpg_world_slots", "construction_turns_remaining", "INTEGER");
        EnsureColumn(db, "rpg_world_sectors", "warden_binding_id", "TEXT");
        EnsureColumn(db, "rpg_world_sectors", "neglected_turns", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_world_entities", "carried_loam", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_world_entity_members", "role", "TEXT NOT NULL DEFAULT 'Fighter'");

        EnsureWorldTurnSchemaUnlocked(db);
    }

    /// <summary>
    /// Creates a whole world in one transaction. Validation runs BEFORE any write, so a malformed
    /// world leaves the database untouched rather than half-populated.
    /// </summary>
    public (bool Ok, string Reason, WorldState? World) CreateWorld(
        long playerId, WorldState world, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(world.WorldId)) return (false, "world.id-missing", null);

        try
        {
            WorldValidation.Validate(world);
        }
        catch (InvalidOperationException ex)
        {
            return (false, "world.invalid: " + ex.Message, null);
        }

        lock (_gate)
        {
            using var db = OpenUnlocked();
            if (GetPlayerUnlocked(db, playerId) is null) return (false, "player.unknown", null);
            if (ReadWorldHeaderUnlocked(db, world.WorldId) != null) return (false, "world.exists", null);

            var now = (utcNow ?? DateTimeOffset.UtcNow).ToString("o");
            using var tx = db.BeginTransaction();

            Insert(db, tx, """
                INSERT INTO rpg_worlds (world_id, player_id, template_id, seed, mode, current_turn,
                                        engine_version, ruleset_version, state, created_utc, revision)
                VALUES ($w, $p, $t, $seed, 'turn', $turn, 1, 1, 'active', $now, 0);
                """,
                ("$w", world.WorldId), ("$p", playerId), ("$t", world.TemplateId),
                ("$seed", world.Seed.ToString()), ("$turn", world.CurrentTurn), ("$now", now));

            WriteWorldGraphUnlocked(db, tx, world);
            tx.Commit();
            return (true, "ok", world);
        }
    }

    /// <summary>
    /// Writes the whole graph (factions, sectors, slots, lanes, entities, members) for a world.
    /// Used by creation and by every turn commit: the turn engine hands back a complete world, so
    /// the store replaces the graph rather than diffing it. At six sectors that is far cheaper than
    /// the bug surface a partial-update path would carry; revisit if a world ever reaches hundreds.
    /// </summary>
    static void WriteWorldGraphUnlocked(SqliteConnection db, SqliteTransaction tx, WorldState world)
    {
        {
            foreach (var f in world.Factions)
                Insert(db, tx, """
                    INSERT INTO rpg_world_factions (world_id, faction_id, kind, name, policy_id, upkeep_handicap_milli)
                    VALUES ($w, $f, $k, $n, $pol, $handicap);
                    """,
                    ("$w", world.WorldId), ("$f", f.FactionId), ("$k", f.Kind.ToString()),
                    ("$n", f.Name), ("$pol", (object?)f.PolicyId), ("$handicap", f.UpkeepHandicapMilli));

            foreach (var s in world.Sectors)
            {
                Insert(db, tx, """
                    INSERT INTO rpg_world_sectors (world_id, sector_id, type_id, climate, danger_band,
                        phase, owner_faction_id, stability_milli, pressure_milli, depletion_milli,
                        development_level, intel, last_seen_turn, layout_x, layout_y,
                        loam_stock, fracture_intensity_milli, warden_binding_id, neglected_turns, revision)
                    VALUES ($w, $s, $type, $climate, $danger, $phase, $owner, $stab, $press, $depl,
                            $dev, $intel, $seen, $x, $y, $loam, $intensity, $warden, $neglected, 0);
                    """,
                    ("$w", world.WorldId), ("$s", s.SectorId), ("$type", s.TypeId),
                    ("$climate", (object?)s.Climate?.ToString()), ("$danger", s.DangerBand),
                    ("$phase", s.Phase.ToString()), ("$owner", (object?)s.OwnerFactionId),
                    ("$stab", s.StabilityMilli), ("$press", s.PressureMilli), ("$depl", s.DepletionMilli),
                    ("$dev", s.DevelopmentLevel), ("$intel", s.AuthoredIntel.ToString()),
                    ("$seen", s.LastSeenTurn), ("$x", s.LayoutX), ("$y", s.LayoutY),
                    ("$loam", s.LoamStock), ("$intensity", s.FractureIntensityMilli),
                    ("$warden", (object?)s.WardenBindingId), ("$neglected", s.NeglectedTurns));

                foreach (var sl in s.Slots)
                    Insert(db, tx, """
                        INSERT INTO rpg_world_slots (world_id, sector_id, slot_index, slot_type_id,
                            element, state, owner_faction_id, guard_wave_id, guard_state,
                            structure_id, construction_turns_remaining, revision)
                        VALUES ($w, $s, $i, $type, $elem, $state, $owner, $guard, $gstate,
                                $structure, $construction, 0);
                        """,
                        ("$w", world.WorldId), ("$s", s.SectorId), ("$i", sl.SlotIndex),
                        ("$type", sl.SlotTypeId), ("$elem", (object?)sl.Element?.ToString()),
                        ("$state", sl.State.ToString()), ("$owner", (object?)sl.OwnerFactionId),
                        ("$guard", (object?)sl.GuardWaveId), ("$gstate", sl.GuardState.ToString()),
                        ("$structure", (object?)sl.StructureId),
                        ("$construction", (object?)sl.ConstructionTurnsRemaining));
            }

            // Belief. Slots and forces go in as JSON rather than as sub-tables because a snapshot
            // is always read whole for one sector and never queried by slot — two more tables would
            // buy nothing and cost a join on every projection.
            foreach (var intel in world.Intel)
            foreach (var snap in intel.Sectors)
                Insert(db, tx, """
                    INSERT INTO rpg_world_faction_intel (world_id, faction_id, sector_id, last_seen_turn,
                        detail, owner_faction_id, phase, climate, danger_band, slots_json, forces_json)
                    VALUES ($w, $f, $s, $turn, $detail, $owner, $phase, $climate, $danger, $slots, $forces);
                    """,
                    ("$w", world.WorldId), ("$f", intel.FactionId), ("$s", snap.SectorId),
                    ("$turn", snap.LastSeenTurn), ("$detail", snap.Detail.ToString()),
                    ("$owner", (object?)snap.OwnerFactionId), ("$phase", snap.Phase.ToString()),
                    ("$climate", (object?)snap.Climate?.ToString()), ("$danger", snap.DangerBand),
                    ("$slots", JsonSerializer.Serialize(snap.Slots)),
                    ("$forces", JsonSerializer.Serialize(snap.Forces)));

            foreach (var l in world.Lanes)
                Insert(db, tx, """
                    INSERT INTO rpg_world_lanes (world_id, lane_id, from_sector_id, to_sector_id,
                        type_id, length, width, hazard_milli, ward_level, gate_key_id, state, revision)
                    VALUES ($w, $l, $from, $to, $type, $len, $width, $haz, $ward, $gate, $state, 0);
                    """,
                    ("$w", world.WorldId), ("$l", l.LaneId), ("$from", l.FromSectorId),
                    ("$to", l.ToSectorId), ("$type", l.TypeId), ("$len", l.Length), ("$width", l.Width),
                    ("$haz", l.HazardMilli), ("$ward", l.WardLevel),
                    ("$gate", (object?)l.GateKeyId), ("$state", l.State.ToString()));

            foreach (var e in world.Entities)
            {
                Insert(db, tx, """
                    INSERT INTO rpg_world_entities (world_id, entity_id, kind, owner_faction_id,
                        at_sector_id, on_lane_id, on_lane_toward_sector_id, lane_progress_milli,
                        stance, movement_remaining, routed, carried_loam, revision)
                    VALUES ($w, $e, $kind, $owner, $at, $lane, $toward, $prog, $stance, $move, $routed,
                            $carried, 0);
                    """,
                    ("$w", world.WorldId), ("$e", e.EntityId), ("$kind", e.Kind.ToString()),
                    ("$owner", e.OwnerFactionId), ("$at", (object?)e.AtSectorId),
                    ("$lane", (object?)e.OnLaneId), ("$toward", (object?)e.OnLaneTowardSectorId),
                    ("$prog", e.LaneProgressMilli),
                    ("$stance", e.Stance), ("$move", e.MovementRemaining),
                    ("$routed", e.Routed ? 1 : 0), ("$carried", e.CarriedLoam));

                for (var i = 0; i < e.Members.Count; i++)
                {
                    var m = e.Members[i];
                    Insert(db, tx, """
                        INSERT INTO rpg_world_entity_members (world_id, entity_id, member_index,
                            instance_id, species_id, level, hp, wounds, role)
                        VALUES ($w, $e, $i, $inst, $sp, $lvl, $hp, $wounds, $role);
                        """,
                        ("$w", world.WorldId), ("$e", e.EntityId), ("$i", i),
                        ("$inst", (object?)m.InstanceId), ("$sp", m.SpeciesId),
                        ("$lvl", m.Level), ("$hp", m.Hp), ("$wounds", m.Wounds),
                        ("$role", m.Role.ToString()));
                }
            }
        }
    }

    /// <summary>Clears a world's graph rows so the next write is a clean replace.</summary>
    static void ClearWorldGraphUnlocked(SqliteConnection db, SqliteTransaction tx, string worldId)
    {
        foreach (var table in new[]
                 {
                     "rpg_world_faction_intel", "rpg_world_entity_members", "rpg_world_entities",
                     "rpg_world_lanes", "rpg_world_slots", "rpg_world_sectors", "rpg_world_factions"
                 })
        {
            using var del = db.CreateCommand();
            del.Transaction = tx;
            del.CommandText = $"DELETE FROM {table} WHERE world_id = $w;";
            del.Parameters.AddWithValue("$w", worldId);
            del.ExecuteNonQuery();
        }
    }

    /// <summary>The player's active world header, without loading the graph.</summary>
    public WorldHeaderRow? GetActiveWorld(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                SELECT world_id, player_id, template_id, seed, current_turn, state, created_utc, revision
                FROM rpg_worlds WHERE player_id = $p AND state = 'active'
                ORDER BY world_id LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadHeader(r) : null;
        }
    }

    /// <summary>
    /// Loads the whole graph in stable id order. Ordering is enforced by SQL, not by trusting
    /// insertion order — a load out of order would fail world validation, which is the point.
    /// </summary>
    public WorldState? LoadWorldState(string worldId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            var header = ReadWorldHeaderUnlocked(db, worldId);
            if (header is null) return null;

            var factions = new List<WorldFaction>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT faction_id, kind, name, policy_id, upkeep_handicap_milli FROM rpg_world_factions WHERE world_id = $w ORDER BY faction_id;";
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    factions.Add(new WorldFaction
                    {
                        FactionId = r.GetString(0),
                        Kind = Enum.Parse<WorldFactionKind>(r.GetString(1)),
                        Name = r.GetString(2),
                        PolicyId = r.IsDBNull(3) ? null : r.GetString(3),
                        UpkeepHandicapMilli = r.GetInt32(4)
                    });
            }

            var slotsBySector = new Dictionary<string, List<WorldSlot>>(StringComparer.Ordinal);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT sector_id, slot_index, slot_type_id, element, state, owner_faction_id,
                           guard_wave_id, guard_state, structure_id, construction_turns_remaining
                    FROM rpg_world_slots WHERE world_id = $w ORDER BY sector_id, slot_index;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var sectorId = r.GetString(0);
                    if (!slotsBySector.TryGetValue(sectorId, out var list))
                        slotsBySector[sectorId] = list = new List<WorldSlot>();
                    list.Add(new WorldSlot
                    {
                        SlotIndex = r.GetInt32(1),
                        SlotTypeId = r.GetString(2),
                        Element = r.IsDBNull(3) ? null : Enum.Parse<ElementTypeId>(r.GetString(3)),
                        State = Enum.Parse<SlotState>(r.GetString(4)),
                        OwnerFactionId = r.IsDBNull(5) ? null : r.GetString(5),
                        GuardWaveId = r.IsDBNull(6) ? null : r.GetString(6),
                        GuardState = Enum.Parse<GuardState>(r.GetString(7)),
                        StructureId = r.IsDBNull(8) ? null : r.GetString(8),
                        ConstructionTurnsRemaining = r.IsDBNull(9) ? null : r.GetInt32(9)
                    });
                }
            }

            var sectors = new List<WorldSector>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT sector_id, type_id, climate, danger_band, phase, owner_faction_id,
                           stability_milli, pressure_milli, depletion_milli, development_level,
                           intel, last_seen_turn, layout_x, layout_y,
                           loam_stock, fracture_intensity_milli, warden_binding_id, neglected_turns
                    FROM rpg_world_sectors WHERE world_id = $w ORDER BY sector_id;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var sectorId = r.GetString(0);
                    sectors.Add(new WorldSector
                    {
                        SectorId = sectorId,
                        TypeId = r.GetString(1),
                        Climate = r.IsDBNull(2) ? null : Enum.Parse<ElementTypeId>(r.GetString(2)),
                        DangerBand = r.GetInt32(3),
                        Phase = Enum.Parse<SectorPhase>(r.GetString(4)),
                        OwnerFactionId = r.IsDBNull(5) ? null : r.GetString(5),
                        StabilityMilli = r.GetInt32(6),
                        PressureMilli = r.GetInt32(7),
                        DepletionMilli = r.GetInt32(8),
                        DevelopmentLevel = r.GetInt32(9),
                        AuthoredIntel = Enum.Parse<IntelState>(r.GetString(10)),
                        LastSeenTurn = r.GetInt32(11),
                        LayoutX = r.GetInt32(12),
                        LayoutY = r.GetInt32(13),
                        LoamStock = r.GetInt64(14),
                        FractureIntensityMilli = r.GetInt32(15),
                        WardenBindingId = r.IsDBNull(16) ? null : r.GetString(16),
                        NeglectedTurns = r.GetInt32(17),
                        Slots = slotsBySector.TryGetValue(sectorId, out var slots)
                            ? slots
                            : new List<WorldSlot>()
                    });
                }
            }

            var lanes = new List<WorldLane>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT lane_id, from_sector_id, to_sector_id, type_id, length, width,
                           hazard_milli, ward_level, gate_key_id, state
                    FROM rpg_world_lanes WHERE world_id = $w ORDER BY lane_id;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    lanes.Add(new WorldLane
                    {
                        LaneId = r.GetString(0),
                        FromSectorId = r.GetString(1),
                        ToSectorId = r.GetString(2),
                        TypeId = r.GetString(3),
                        Length = r.GetInt32(4),
                        Width = r.GetInt32(5),
                        HazardMilli = r.GetInt32(6),
                        WardLevel = r.GetInt32(7),
                        GateKeyId = r.IsDBNull(8) ? null : r.GetString(8),
                        State = Enum.Parse<LaneState>(r.GetString(9))
                    });
            }

            var membersByEntity = new Dictionary<string, List<WorldEntityMember>>(StringComparer.Ordinal);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT entity_id, member_index, instance_id, species_id, level, hp, wounds, role
                    FROM rpg_world_entity_members WHERE world_id = $w ORDER BY entity_id, member_index;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var entityId = r.GetString(0);
                    if (!membersByEntity.TryGetValue(entityId, out var list))
                        membersByEntity[entityId] = list = new List<WorldEntityMember>();
                    list.Add(new WorldEntityMember
                    {
                        InstanceId = r.IsDBNull(2) ? null : r.GetString(2),
                        SpeciesId = r.GetString(3),
                        Level = r.GetInt32(4),
                        Hp = r.GetInt32(5),
                        Wounds = r.GetInt32(6),
                        Role = Enum.Parse<WorldEntityMemberRole>(r.GetString(7))
                    });
                }
            }

            var entities = new List<WorldEntity>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT entity_id, kind, owner_faction_id, at_sector_id, on_lane_id,
                           on_lane_toward_sector_id, lane_progress_milli, stance, movement_remaining,
                           routed, carried_loam
                    FROM rpg_world_entities WHERE world_id = $w ORDER BY entity_id;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var entityId = r.GetString(0);
                    entities.Add(new WorldEntity
                    {
                        EntityId = entityId,
                        Kind = Enum.Parse<WorldEntityKind>(r.GetString(1)),
                        OwnerFactionId = r.GetString(2),
                        AtSectorId = r.IsDBNull(3) ? null : r.GetString(3),
                        OnLaneId = r.IsDBNull(4) ? null : r.GetString(4),
                        OnLaneTowardSectorId = r.IsDBNull(5) ? null : r.GetString(5),
                        LaneProgressMilli = r.GetInt32(6),
                        Stance = r.GetString(7),
                        MovementRemaining = r.GetInt32(8),
                        Routed = r.GetInt32(9) != 0,
                        CarriedLoam = r.GetInt64(10),
                        Members = membersByEntity.TryGetValue(entityId, out var members)
                            ? members
                            : new List<WorldEntityMember>()
                    });
                }
            }

            var intel = new List<FactionIntel>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT faction_id, sector_id, last_seen_turn, detail, owner_faction_id, phase,
                           climate, danger_band, slots_json, forces_json
                    FROM rpg_world_faction_intel WHERE world_id = $w
                    ORDER BY faction_id, sector_id;
                    """;
                cmd.Parameters.AddWithValue("$w", worldId);
                using var r = cmd.ExecuteReader();

                var byFaction = new Dictionary<string, List<IntelSnapshot>>(StringComparer.Ordinal);
                var order = new List<string>();
                while (r.Read())
                {
                    var factionId = r.GetString(0);
                    if (!byFaction.TryGetValue(factionId, out var list))
                    {
                        byFaction[factionId] = list = new List<IntelSnapshot>();
                        order.Add(factionId);
                    }

                    list.Add(new IntelSnapshot
                    {
                        SectorId = r.GetString(1),
                        LastSeenTurn = r.GetInt32(2),
                        Detail = Enum.Parse<SectorSight>(r.GetString(3)),
                        OwnerFactionId = r.IsDBNull(4) ? null : r.GetString(4),
                        Phase = Enum.Parse<SectorPhase>(r.GetString(5)),
                        Climate = r.IsDBNull(6) ? null : Enum.Parse<ElementTypeId>(r.GetString(6)),
                        DangerBand = r.GetInt32(7),
                        Slots = JsonSerializer.Deserialize<List<RememberedSlot>>(r.GetString(8))
                                ?? new List<RememberedSlot>(),
                        Forces = JsonSerializer.Deserialize<List<RememberedForce>>(r.GetString(9))
                                 ?? new List<RememberedForce>()
                    });
                }

                foreach (var factionId in order)
                    intel.Add(new FactionIntel { FactionId = factionId, Sectors = byFaction[factionId] });
            }

            return new WorldState
            {
                WorldId = header.WorldId,
                TemplateId = header.TemplateId,
                Seed = header.Seed,
                CurrentTurn = header.CurrentTurn,
                Factions = factions,
                Sectors = sectors,
                Lanes = lanes,
                Intel = intel,
                Entities = entities
            };
        }
    }

    static WorldHeaderRow? ReadWorldHeaderUnlocked(SqliteConnection db, string worldId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT world_id, player_id, template_id, seed, current_turn, state, created_utc, revision
            FROM rpg_worlds WHERE world_id = $w;
            """;
        cmd.Parameters.AddWithValue("$w", worldId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadHeader(r) : null;
    }

    static WorldHeaderRow ReadHeader(SqliteDataReader r) => new(
        r.GetString(0), r.GetInt64(1), r.GetString(2),
        ulong.TryParse(r.GetString(3), out var seed) ? seed : 0UL,
        r.GetInt32(4), r.GetString(5), r.GetString(6), r.GetInt64(7));

    static void Insert(SqliteConnection db, SqliteTransaction tx, string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}

/// <summary>World header without the graph — enough to list, resume, or route a turn.</summary>
public sealed record WorldHeaderRow(
    string WorldId, long PlayerId, string TemplateId, ulong Seed,
    int CurrentTurn, string State, string CreatedUtc, long Revision);
