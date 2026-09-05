using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Data.Abstractions;
using FusionRpg.Data.Policies;
using FusionRpg.Data.Sqlite;
using FusionRpg.Data.Sqlite.Migrations;
using Microsoft.Data.Sqlite;

namespace FusionRpg.Data;

public sealed partial class RpgStore : IRpgDb
{
    private readonly string _dataDir;
    private readonly string _hotPath;
    private readonly string _mediaPath;
    private readonly object _gate = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DateTimeOffset? LastHeartbeatUtc { get; private set; }
    public string Source { get; private set; } = RpgConstants.SourceNone;
    public string DataDir => _dataDir;
    public string HotPath => _hotPath;
    public string MediaPath => _mediaPath;
    public string ArchiveDir => Path.Combine(_dataDir, "archive");
    HashSet<long>? _activityNotifyBatch;
    List<RpgProgressionDirty>? _progressionNotifyBatch;
    HashSet<long>? _closedRunNotifyBatch;

    /// <param name="dataDir">Directory holding <c>rpg-hot.sqlite</c> + <c>rpg-media.sqlite</c>.</param>
    public RpgStore(string dataDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        _dataDir = Path.GetFullPath(dataDir);
        _hotPath = Path.Combine(_dataDir, LegacyMonoMigrator.HotFileName);
        _mediaPath = Path.Combine(_dataDir, LegacyMonoMigrator.MediaFileName);
    }

    public static readonly string[] MetricNames =
    {
        "plants_spawned", "plants_died", "zombies_spawned", "zombies_killed",
        "bullets_spawned", "mowers_used", "injector_connected", "runs_started", "runs_ended"
    };

    public void Init()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(ArchiveDir);
        LegacyMonoMigrator.TryMigrate(_dataDir, Console.Out);
        LegacyMonoMigrator.HealOrphanMediaTables(_dataDir, Console.Out);

        using (var db = Open())
        {
            EnsureHotSchema(db);
            ShardRungs.Migrate(db, Console.Out);
        }
        using (var media = OpenMedia())
            EnsureMediaSchema(media);

        using (var db = Open())
        {
            SeedPlayerIfEmpty(db);
            BackfillWorldSeedsUnlocked(db);
            EnsurePvzStatsRevisionForAllPlayers(db);
            EnsurePvzActivityRevisionForAllPlayers(db);
            var pid = GetCurrentPlayerIdUnlocked(db);
            Exec(db, $"UPDATE events SET player_id = {pid} WHERE player_id IS NULL;");
            Exec(db, $"UPDATE runs SET player_id = {pid} WHERE player_id IS NULL;");

            if (GetSettingUnlocked(db, "stats") is null)
                PutStatsUnlocked(db, new StatsConfig());
            foreach (var name in MetricNames)
            {
                if (name == "injector_connected")
                    UpsertMetricUnlocked(db, name, InjectorConnected ? 1 : 0);
                else
                    SeedMetricIfMissingUnlocked(db, name);
            }

            // W5-E: stale ActiveBound (no open run / missing match_key) → Roster on boot.
            var swept = SweepStaleActiveBoundUnlocked(db);
            if (swept > 0)
                Console.WriteLine($"[unique] swept {swept} stale ActiveBound → Roster");
        }
    }

    void EnsureHotSchema(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS players (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              name TEXT NOT NULL,
              created_utc TEXT NOT NULL,
              world_seed INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
              key TEXT PRIMARY KEY,
              json TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS events (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              t TEXT NOT NULL,
              game TEXT NOT NULL,
              kind TEXT NOT NULL,
              payload TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_events_kind ON events(kind);
            CREATE TABLE IF NOT EXISTS runs (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              started_utc TEXT NOT NULL,
              ended_utc TEXT,
              level_name TEXT,
              summary TEXT
            );
            CREATE TABLE IF NOT EXISTS entities (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL,
              ptr TEXT NOT NULL,
              side TEXT NOT NULL,
              type INTEGER NOT NULL,
              type_name TEXT,
              hp_base INTEGER,
              hp INTEGER,
              max_hp_base INTEGER,
              max_hp INTEGER,
              attack_base INTEGER,
              attack INTEGER,
              armor_base INTEGER,
              armor INTEGER,
              col INTEGER,
              row INTEGER,
              spawned_utc TEXT NOT NULL,
              died_utc TEXT,
              die_reason TEXT,
              payload TEXT,
              UNIQUE(run_id, ptr)
            );
            CREATE TABLE IF NOT EXISTS types (
              game TEXT NOT NULL,
              side TEXT NOT NULL,
              type INTEGER NOT NULL,
              type_name TEXT,
              hp_base INTEGER,
              max_hp_base INTEGER,
              attack_base INTEGER,
              armor_base INTEGER,
              armor_max_base INTEGER,
              seen_count INTEGER NOT NULL DEFAULT 0,
              killed_count INTEGER NOT NULL DEFAULT 0,
              first_seen_utc TEXT,
              last_seen_utc TEXT,
              PRIMARY KEY (game, side, type)
            );
            CREATE TABLE IF NOT EXISTS mowers (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL,
              ptr TEXT NOT NULL,
              type INTEGER NOT NULL,
              type_name TEXT,
              row INTEGER,
              placed_utc TEXT,
              started_utc TEXT,
              died_utc TEXT,
              UNIQUE(run_id, ptr)
            );
            CREATE TABLE IF NOT EXISTS spawn_stats (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL,
              ptr TEXT NOT NULL,
              side TEXT NOT NULL,
              type INTEGER NOT NULL,
              source TEXT NOT NULL,
              captured_utc TEXT NOT NULL,
              stats_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS recipes (
              game TEXT NOT NULL,
              parent_a INTEGER NOT NULL,
              parent_b INTEGER NOT NULL,
              result INTEGER NOT NULL,
              parent_a_name TEXT,
              parent_b_name TEXT,
              result_name TEXT,
              PRIMARY KEY (game, parent_a, parent_b, result)
            );
            CREATE TABLE IF NOT EXISTS metrics (
              name TEXT PRIMARY KEY,
              value REAL NOT NULL,
              ts TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS almanac_seed (
              side               TEXT NOT NULL,
              type_id            INTEGER NOT NULL,
              type_name          TEXT,
              display_name       TEXT,
              flavor_info        TEXT,
              flavor_introduce   TEXT,
              sun_cost           INTEGER,
              cooldown_sec       REAL,
              cost_status        TEXT NOT NULL DEFAULT 'absent',
              hp                 INTEGER,
              attack             INTEGER,
              armor              INTEGER,
              armor_max          INTEGER,
              stats_observed     INTEGER NOT NULL DEFAULT 0,
              stats_sample_utc   TEXT,
              almanac_captured_utc TEXT,
              contract_version   INTEGER NOT NULL,
              rebuilt_utc        TEXT NOT NULL,
              PRIMARY KEY (side, type_id)
            );
            CREATE TABLE IF NOT EXISTS almanac_seed_enrichment (
              side              TEXT NOT NULL,
              type_id           INTEGER NOT NULL,
              qualities_json    TEXT,
              unlock_condition  TEXT,
              type_class        TEXT,
              weaknesses_text   TEXT,
              damage_vs_text    TEXT,
              description_text  TEXT,
              source            TEXT NOT NULL,
              matched_by        TEXT NOT NULL,
              imported_utc      TEXT NOT NULL,
              PRIMARY KEY (side, type_id)
            );
            PRAGMA journal_mode=WAL;
            """);
        // world-seed (T5.1, spec-world-seed.md) — "the whole save"'s own per-player root. Created
        // once at player creation, never regenerated; a legacy row (pre-dating this column, or
        // SeedPlayerIfEmpty's own direct INSERT) defaults to 0, which BackfillWorldSeeds treats as
        // the sentinel for "not yet assigned" and fixes in Init(), never left at 0 permanently — two
        // players sharing 0 would derive identical rosters.
        EnsureColumn(db, "players", "world_seed", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "almanac_seed_enrichment", "description_text", "TEXT");
        EnsureColumn(db, "events", "player_id", "INTEGER");
        EnsureColumn(db, "events", "run_id", "INTEGER");
        EnsureColumn(db, "events", "match_key", "TEXT");
        EnsureColumn(db, "runs", "player_id", "INTEGER");
        EnsureColumn(db, "runs", "match_key", "TEXT");
        EnsureColumn(db, "runs", "result", "TEXT");
        EnsureColumn(db, "runs", "mowers_used", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "runs", "plants_planted", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "runs", "plants_died", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "runs", "zombies_killed", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "runs", "duration_sec", "REAL");
        EnsureColumn(db, "runs", "sun_final", "INTEGER");
        EnsureColumn(db, "runs", "wave", "INTEGER");
        EnsureColumn(db, "runs", "max_wave", "INTEGER");
        EnsureColumn(db, "runs", "level_type", "TEXT");
        EnsureColumn(db, "runs", "board_level", "INTEGER");
        EnsureColumn(db, "runs", "modifiers_json", "TEXT");
        EnsureColumn(db, "runs", "snapshot_json", "TEXT");
        EnsureColumn(db, "runs", "archive_uri", "TEXT");
        // Standalone-first (decisions.md 2026-08-21): per-run game profile — NULL = legacy pvzrh.
        EnsureColumn(db, "runs", "game", "TEXT");
        EnsureColumn(db, "types", "display_name", "TEXT");
        EnsureColumn(db, "types", "sample_json", "TEXT");
        Exec(db, """
            CREATE TABLE IF NOT EXISTS archive_catalog (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              uri TEXT NOT NULL UNIQUE,
              kind TEXT NOT NULL,
              run_id INTEGER,
              player_id INTEGER,
              created_utc TEXT NOT NULL,
              meta_json TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_archive_catalog_kind ON archive_catalog(kind);
            """);
        try { Exec(db, "CREATE INDEX IF NOT EXISTS ix_spawn_stats_run_ptr ON spawn_stats(run_id, ptr);"); } catch { /* old db */ }
        // almanac_seed's baseline lookup filters side+type+source per rebuilt row (RpgStore.AlmanacSeed.cs)
        // — without this, a rebuild against a real-sized DB (hundreds of thousands of spawn_stats rows
        // across many runs) does a full table scan per type, ~900 times. Confirmed live 2026-08-23: a
        // rebuild against a 520MB hot.sqlite never returned within 30s before this index existed.
        try { Exec(db, "CREATE INDEX IF NOT EXISTS ix_spawn_stats_side_type_source ON spawn_stats(side, type, source, captured_utc);"); } catch { /* old db */ }
        try { Exec(db, "CREATE INDEX IF NOT EXISTS ix_events_run ON events(run_id);"); } catch { /* old db */ }
        try { Exec(db, "CREATE INDEX IF NOT EXISTS ix_events_player ON events(player_id);"); } catch { /* old db */ }
        try { Exec(db, "CREATE UNIQUE INDEX IF NOT EXISTS ix_runs_match_key ON runs(match_key) WHERE match_key IS NOT NULL;"); } catch { /* old db */ }
        EnsureColumn(db, "entities", "type_name", "TEXT");

        Exec(db, """
            CREATE TABLE IF NOT EXISTS pvz_stat_revisions (
              player_id INTEGER PRIMARY KEY,
              revision INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS pvz_stat_modifiers (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              plugin_id TEXT NOT NULL,
              source_kind TEXT NOT NULL,
              source_id TEXT NOT NULL,
              channel TEXT NOT NULL,
              op TEXT NOT NULL,
              value REAL NOT NULL,
              priority INTEGER NOT NULL DEFAULT 0,
              enabled INTEGER NOT NULL DEFAULT 1,
              detail_json TEXT,
              UNIQUE(player_id, plugin_id, source_kind, source_id, channel, op)
            );
            CREATE INDEX IF NOT EXISTS ix_pvz_stat_modifiers_player ON pvz_stat_modifiers(player_id);
            CREATE TABLE IF NOT EXISTS pvz_stat_snapshots (
              player_id INTEGER PRIMARY KEY,
              revision INTEGER NOT NULL,
              finals_json TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS pvz_stat_contributions (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              revision INTEGER NOT NULL,
              channel TEXT NOT NULL,
              plugin_id TEXT NOT NULL,
              source_kind TEXT NOT NULL,
              source_id TEXT NOT NULL,
              op TEXT NOT NULL,
              value REAL NOT NULL,
              priority INTEGER NOT NULL DEFAULT 0,
              detail_json TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_pvz_stat_contrib_player_ch ON pvz_stat_contributions(player_id, channel);
            CREATE TABLE IF NOT EXISTS pvz_activity_revisions (
              player_id INTEGER PRIMARY KEY,
              revision INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS pvz_activity_facts (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL DEFAULT 0,
              t TEXT NOT NULL,
              kind TEXT NOT NULL,
              plugin_id TEXT NOT NULL,
              source_kind TEXT NOT NULL,
              source_id TEXT NOT NULL,
              payload_json TEXT,
              match_key TEXT,
              dedupe_key TEXT NOT NULL DEFAULT '',
              UNIQUE(player_id, run_id, kind, dedupe_key)
            );
            CREATE INDEX IF NOT EXISTS ix_pvz_activity_facts_player ON pvz_activity_facts(player_id, id DESC);
            CREATE INDEX IF NOT EXISTS ix_pvz_activity_facts_kind ON pvz_activity_facts(player_id, kind);
            CREATE TABLE IF NOT EXISTS pvz_activity_rollups (
              player_id INTEGER PRIMARY KEY,
              revision INTEGER NOT NULL,
              counters_json TEXT NOT NULL,
              updated_utc TEXT NOT NULL,
              through_fact_id INTEGER NOT NULL DEFAULT 0,
              schema_version INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS rpg_actor_progression (
              player_id INTEGER NOT NULL,
              kind TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              level INTEGER NOT NULL DEFAULT 1,
              xp INTEGER NOT NULL DEFAULT 0,
              highest_level INTEGER NOT NULL DEFAULT 1,
              demotion_count INTEGER NOT NULL DEFAULT 0,
              revision INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL,
              through_ledger_id INTEGER NOT NULL DEFAULT 0,
              xp_by_reason_json TEXT,
              PRIMARY KEY (player_id, kind, type_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_xp_ledger (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              kind TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL DEFAULT 0,
              t TEXT NOT NULL,
              delta INTEGER NOT NULL,
              reason TEXT NOT NULL,
              activity_fact_id INTEGER,
              level_before INTEGER NOT NULL,
              -- INTEGER since 2026-09-05: XP is an integer magnitude, and the 2026-09-04 pass
              -- migrated rpg_actor_progression.xp but not the ledger that mirrors it.
              xp_before INTEGER NOT NULL,
              level_after INTEGER NOT NULL,
              xp_after INTEGER NOT NULL,
              demotion_before INTEGER NOT NULL,
              demotion_after INTEGER NOT NULL,
              payload_json TEXT,
              dedupe_key TEXT NOT NULL,
              UNIQUE (player_id, kind, type_id, reason, dedupe_key)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_xp_ledger_player ON rpg_xp_ledger(player_id, id);
            CREATE TABLE IF NOT EXISTS rpg_unique_actors (
              instance_id TEXT NOT NULL PRIMARY KEY,
              player_id INTEGER NOT NULL,
              side TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              phase TEXT NOT NULL,
              level INTEGER NOT NULL DEFAULT 1,
              xp INTEGER NOT NULL DEFAULT 0,
              match_key TEXT,
              last_ptr TEXT,
              deploy_correlation_id TEXT,
              revision INTEGER NOT NULL DEFAULT 0,
              created_utc TEXT NOT NULL,
              updated_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_unique_actors_player ON rpg_unique_actors(player_id);
            CREATE INDEX IF NOT EXISTS ix_rpg_unique_actors_corr ON rpg_unique_actors(deploy_correlation_id);
            CREATE INDEX IF NOT EXISTS ix_rpg_unique_actors_ptr ON rpg_unique_actors(last_ptr);
            CREATE INDEX IF NOT EXISTS ix_rpg_unique_actors_match ON rpg_unique_actors(match_key);
            CREATE TABLE IF NOT EXISTS rpg_unique_equipment (
              instance_id TEXT NOT NULL,
              slot TEXT NOT NULL,
              item_id TEXT NOT NULL DEFAULT '',
              PRIMARY KEY (instance_id, slot)
            );
            CREATE TABLE IF NOT EXISTS rpg_unique_stat_mods (
              instance_id TEXT NOT NULL PRIMARY KEY,
              mods_json TEXT NOT NULL DEFAULT '{}'
            );
            CREATE TABLE IF NOT EXISTS rpg_demon_profiles (
              instance_id TEXT NOT NULL PRIMARY KEY,
              species_id TEXT NOT NULL,
              rarity TEXT NOT NULL,
              variant TEXT NOT NULL DEFAULT 'normal',
              element_primary TEXT NOT NULL,
              element_secondary TEXT,
              traits_json TEXT NOT NULL DEFAULT '[]',
              origin TEXT NOT NULL,
              nickname TEXT,
              locked INTEGER NOT NULL DEFAULT 0,
              created_utc TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS rpg_demon_codex (
              player_id INTEGER NOT NULL,
              species_id TEXT NOT NULL,
              state TEXT NOT NULL,
              first_utc TEXT NOT NULL,
              updated_utc TEXT NOT NULL,
              PRIMARY KEY (player_id, species_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_demon_lineage (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              instance_id TEXT NOT NULL,
              event TEXT NOT NULL,
              detail_json TEXT NOT NULL DEFAULT '{}',
              t TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_demon_lineage_instance ON rpg_demon_lineage(instance_id, id);
            CREATE TABLE IF NOT EXISTS rpg_fusion_log (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              correlation_id TEXT NOT NULL,
              mode TEXT NOT NULL,
              inputs_json TEXT NOT NULL,
              output_json TEXT NOT NULL,
              seed TEXT NOT NULL,
              t TEXT NOT NULL,
              UNIQUE(player_id, correlation_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_fusion_discovery (
              player_id INTEGER NOT NULL,
              recipe_id TEXT NOT NULL,
              t TEXT NOT NULL,
              PRIMARY KEY (player_id, recipe_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_patron (
              player_id INTEGER NOT NULL PRIMARY KEY,
              instance_id TEXT NOT NULL,
              set_utc TEXT NOT NULL,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS rpg_demon_contracts (
              instance_id TEXT NOT NULL PRIMARY KEY,
              player_id INTEGER NOT NULL,
              bound INTEGER NOT NULL DEFAULT 0,
              loyalty INTEGER NOT NULL DEFAULT 0,
              personality TEXT NOT NULL,
              bound_utc TEXT,
              released_utc TEXT,
              gain_day TEXT NOT NULL DEFAULT '',
              gain_today INTEGER NOT NULL DEFAULT 0,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_demon_contracts_bound
              ON rpg_demon_contracts(player_id) WHERE bound = 1;
            CREATE TABLE IF NOT EXISTS rpg_contract_state (
              player_id INTEGER NOT NULL PRIMARY KEY,
              purchased_slots INTEGER NOT NULL DEFAULT 0,
              last_settled_utc TEXT NOT NULL,
              migrated_utc TEXT,
              revision INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS rpg_soul_ledger (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              run_id INTEGER NOT NULL DEFAULT 0,
              delta INTEGER NOT NULL,
              reason TEXT NOT NULL,
              ref_kind TEXT,
              ref_id TEXT,
              dedupe_key TEXT NOT NULL,
              t TEXT NOT NULL,
              payload_json TEXT,
              UNIQUE(player_id, reason, dedupe_key)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_soul_ledger_player ON rpg_soul_ledger(player_id, id);
            CREATE INDEX IF NOT EXISTS ix_rpg_soul_ledger_earn ON rpg_soul_ledger(player_id, reason, run_id);
            CREATE TABLE IF NOT EXISTS rpg_soul_balances (
              player_id INTEGER NOT NULL PRIMARY KEY,
              balance INTEGER NOT NULL DEFAULT 0,
              earned_total INTEGER NOT NULL DEFAULT 0,
              spent_total INTEGER NOT NULL DEFAULT 0,
              through_ledger_id INTEGER NOT NULL DEFAULT 0,
              revision INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS rpg_summon_log (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              correlation_id TEXT NOT NULL,
              banner_id TEXT NOT NULL,
              count INTEGER NOT NULL,
              focus_element TEXT,
              rng_seed TEXT NOT NULL,
              results_json TEXT NOT NULL,
              t TEXT NOT NULL,
              UNIQUE(player_id, correlation_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_summon_pity (
              player_id INTEGER NOT NULL PRIMARY KEY,
              pulls_since_epic INTEGER NOT NULL DEFAULT 0,
              pulls_since_legendary INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS rpg_web_match_log (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              correlation_id TEXT NOT NULL,
              match_key TEXT NOT NULL UNIQUE,
              setup_json TEXT NOT NULL,
              seed TEXT NOT NULL,
              engine_version INTEGER NOT NULL,
              ruleset_version INTEGER NOT NULL,
              rng_algo_version INTEGER NOT NULL,
              environment_stamp TEXT,
              sweep_refused TEXT,
              run_id INTEGER,
              t TEXT NOT NULL,
              UNIQUE(player_id, correlation_id)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_web_match_log_unresolved ON rpg_web_match_log(id) WHERE run_id IS NULL;
            CREATE TABLE IF NOT EXISTS rpg_expeditions (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              player_id INTEGER NOT NULL,
              correlation_id TEXT NOT NULL,
              state TEXT NOT NULL,
              tier_id TEXT NOT NULL,
              squad_json TEXT NOT NULL,
              seed TEXT NOT NULL,
              dispatched_utc TEXT NOT NULL,
              due_utc TEXT NOT NULL,
              collected_utc TEXT,
              UNIQUE(player_id, correlation_id)
            );
            CREATE TABLE IF NOT EXISTS rpg_expedition_members (
              expedition_id INTEGER NOT NULL,
              instance_id TEXT NOT NULL,
              active INTEGER NOT NULL DEFAULT 1,
              PRIMARY KEY (expedition_id, instance_id)
            );
            CREATE INDEX IF NOT EXISTS ix_rpg_expedition_members_active
              ON rpg_expedition_members(instance_id) WHERE active = 1;
            CREATE TABLE IF NOT EXISTS rpg_demon_materials (
              player_id INTEGER NOT NULL,
              material_id TEXT NOT NULL,
              qty INTEGER NOT NULL DEFAULT 0,
              updated_utc TEXT NOT NULL,
              PRIMARY KEY (player_id, material_id)
            );
            """);
        EnsureColumn(db, "pvz_activity_rollups", "through_fact_id", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "pvz_activity_rollups", "schema_version", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_actor_progression", "through_ledger_id", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_actor_progression", "xp_by_reason_json", "TEXT");
        // species-build T1.1 (spec-species-xp.md §1 Option A): kind='species' rows key on
        // DemonSpeciesDef.DemonTypeId in the existing type_id column (already unique per species) —
        // this nullable text column carries the human-readable speciesId alongside it, so a row can be
        // read back without a roster round-trip. Every other kind leaves it NULL.
        EnsureColumn(db, "rpg_actor_progression", "scope_key", "TEXT");
        EnsureColumn(db, "rpg_demon_profiles", "star", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(db, "rpg_demon_profiles", "promoted", "INTEGER NOT NULL DEFAULT 0");
        // Wardens (spec-loam-texture.md): a permanent, non-releasable bind — the same capacity slot
        // as an ordinary contract, flagged so ReleaseContract can refuse it unconditionally.
        EnsureColumn(db, "rpg_demon_contracts", "warden", "INTEGER NOT NULL DEFAULT 0");
        // battle-adoption: platform stamp for the cross-arch replay guard, and the sweep's
        // terminal state. Both live HERE, after rpg_web_match_log's own CREATE — an ALTER
        // above it would throw "no such table" on every fresh database.
        EnsureColumn(db, "rpg_web_match_log", "environment_stamp", "TEXT");
        EnsureColumn(db, "rpg_web_match_log", "sweep_refused", "TEXT");
        // E8: the content stamp a resolve ran against, so the boot sweep can refuse to
        // re-resolve across edited effect content instead of silently producing a different report.
        EnsureColumn(db, "rpg_web_match_log", "content_hash", "TEXT");
        // B21 (spec-interactive-turns.md §3): the decision trace — the fourth member of the
        // determinism tuple beside setup_json, seed and the version stamps, so it belongs on this
        // table rather than in one of its own. NULL means "not an interactive match", which is every
        // match today; an interactive match with a NULL or partial trace is REFUSED by the sweep
        // rather than re-resolved, because re-resolving substitutes AI decisions for a player's.
        EnsureColumn(db, "rpg_web_match_log", "decisions_json", "TEXT");
        // World map (spec-world-model.md) — its DDL lives beside its store partial.
        EnsureWorldSchemaUnlocked(db);
        // Atom effect curves (spec-value-spec-and-curve.md, E2) — Core cannot hold SQL, so the
        // table three modules depend on lives beside its store partial.
        EnsureCurveSchemaUnlocked(db);
        // effect_atom + content_meta (spec-atom-schema.md, E4).
        EnsureAtomSchemaUnlocked(db);
        // effect_container + its atom/pool children + rarity (spec-container-schema.md, E5).
        EnsureContainerSchemaUnlocked(db);
        // effect_instance / effect_instance_atom / effect_binding (spec-instance-and-binding.md, E6).
        EnsureAtomInstanceSchemaUnlocked(db);
        // rpg_item — the second reachability root beside effect_binding (item-ideal.md, durable-ownership).
        EnsureRpgItemSchemaUnlocked(db);
        // rarity_budget — item-ideal.md, rarity-bands (module 7).
        EnsureRarityBudgetSchemaUnlocked(db);
        // item_display_template — item-ideal.md, item-card (module 10).
        EnsureItemDisplaySchemaUnlocked(db);
        // loot_source / drop_table[_group|_entry] / item_drop_log / item_generation /
        // item_loot_pity / item_first_clear — item-ideal.md, drop-volume (module 11).
        EnsureLootSchemaUnlocked(db);
        // material_recipe / material_recipe_cost / rpg_material_spend_log — I9 §6.1–6.2,
        // salvage-craft (module 14). The material INVENTORY table (rpg_demon_materials) is DDL'd
        // above with the demon tables and is deliberately not renamed here.
        EnsureMaterialSchemaUnlocked(db);
        // effect_instance_op + the five mutation head columns + effect_instance_atom.suppressed --
        // D2 §9, enhance-reroll (module 15). Must run AFTER EnsureAtomInstanceSchemaUnlocked, whose
        // tables it adds columns to.
        EnsureInstanceOpSchemaUnlocked(db);
        // item_socket (THE SSOT for socket state, D2 §6) + socket_combo_recipe / _ingredient —
        // spec-sockets.md §5.2, sockets (module 16). Must run AFTER EnsureAtomInstanceSchemaUnlocked,
        // whose effect_instance it references.
        EnsureSocketSchemaUnlocked(db);
        // item_set / item_set_member / item_set_tier — ssot-sets.md §4.2, threshold-grants (module 12).
        EnsureItemSetSchemaUnlocked(db);
        // item_unique — ssot-uniques.md §5.2, uniques (module 17). Must run AFTER the container schema
        // it keys on, and it is read alongside item_set_member for §3.8's mutual exclusion.
        EnsureItemUniqueSchemaUnlocked(db);
        // effect_element + both matchup matrices (spec-element-roster-data.md, E18).
        EnsureElementSchemaUnlocked(db);
        // power_coefficient + power_trigger_frequency + the sweep's proposal table (E9).
        EnsurePowerSchemaUnlocked(db);
        // effect_channel_policy — a channel's direction, never its identity (E16). Caps/defaults
        // columns retired T1.4 (cap-consolidation, 2026-08-25): they were dead, direction is the only
        // live column.
        EnsureChannelPolicySchemaUnlocked(db);
        // rpg_aptitude_allocation — class-system P6.2, spec-point-economy.md. Inputs only, one row
        // per (scope, scopeKey, aptitude) with a nonzero spend.
        EnsureAptitudeAllocationSchemaUnlocked(db);
        // rpg_action + cost/scope/grant/species-basics (spec-action-model.md, A1).
        EnsureActionSchemaUnlocked(db);
        // rpg_run_pool — persisted resource pools across a run's encounter boundaries (spec-action-costs.md §9, T18).
        EnsureRunPoolSchemaUnlocked(db);
        // rpg_actor_loadout — the equipped-skill set (spec-loadout.md §1, T21).
        EnsureLoadoutSchemaUnlocked(db);
        // rpg_player_commander — default lawn commander (commander-surface default-persistence).
        EnsurePlayerCommanderSchemaUnlocked(db);
        // demon_species + demon_species_magnitude — species-generator's committed output, imported
        // (spec-species-generator.md, demon-seed module 12/13, T4.6).
        EnsureSpeciesSchemaUnlocked(db);
        // player_species — the rolled roster per player, append-only (spec-player-materialise.md,
        // demon-seed module 16, T5.6).
        EnsurePlayerSpeciesSchemaUnlocked(db);
    }

    void EnsureMediaSchema(SqliteConnection db)
    {
        Exec(db, """
            CREATE TABLE IF NOT EXISTS type_icon_layers (
              side TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              layer TEXT NOT NULL,
              source TEXT,
              width INTEGER,
              height INTEGER,
              png BLOB NOT NULL,
              captured_utc TEXT NOT NULL,
              PRIMARY KEY (side, type_id, layer)
            );
            CREATE INDEX IF NOT EXISTS ix_type_icon_layers_side ON type_icon_layers(side, type_id);
            CREATE TABLE IF NOT EXISTS type_icons (
              side TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              png BLOB NOT NULL,
              recipe_json TEXT,
              updated_utc TEXT NOT NULL,
              PRIMARY KEY (side, type_id)
            );
            CREATE TABLE IF NOT EXISTS type_almanac_dump (
              side TEXT NOT NULL,
              type_id INTEGER NOT NULL,
              fields_json TEXT NOT NULL,
              sources_json TEXT,
              captured_utc TEXT NOT NULL,
              PRIMARY KEY (side, type_id)
            );
            CREATE INDEX IF NOT EXISTS ix_type_almanac_dump_side ON type_almanac_dump(side, type_id);
            PRAGMA journal_mode=WAL;
            """);
    }

    public void Reset()
    {
        lock (_gate)
        {
            LastHeartbeatUtc = null;
            Source = RpgConstants.SourceNone;
            _killEarnMemo.Clear();
            using (var db = OpenUnlocked())
            {
                foreach (var sql in new[]
                         {
                             "DELETE FROM events;", "DELETE FROM entities;", "DELETE FROM spawn_stats;", "DELETE FROM mowers;",
                             "DELETE FROM types;", "DELETE FROM recipes;", "DELETE FROM runs;", "DELETE FROM metrics;", "DELETE FROM settings;",
                             "DELETE FROM almanac_seed;", "DELETE FROM almanac_seed_enrichment;",
                             "DELETE FROM pvz_stat_contributions;", "DELETE FROM pvz_stat_snapshots;",
                             "DELETE FROM pvz_stat_modifiers;", "DELETE FROM pvz_stat_revisions;",
                             "DELETE FROM pvz_activity_facts;", "DELETE FROM pvz_activity_rollups;",
                             "DELETE FROM pvz_activity_revisions;",
                             "DELETE FROM rpg_xp_ledger;", "DELETE FROM rpg_actor_progression;",
                             "DELETE FROM rpg_unique_equipment;", "DELETE FROM rpg_unique_stat_mods;",
                             "DELETE FROM rpg_demon_profiles;", "DELETE FROM rpg_demon_codex;",
                             "DELETE FROM rpg_soul_ledger;", "DELETE FROM rpg_soul_balances;",
                             "DELETE FROM rpg_summon_log;", "DELETE FROM rpg_summon_pity;",
                             "DELETE FROM rpg_web_match_log;",
                             "DELETE FROM rpg_expeditions;", "DELETE FROM rpg_expedition_members;",
                             "DELETE FROM rpg_demon_materials;",
                             "DELETE FROM rpg_demon_lineage;", "DELETE FROM rpg_fusion_log;",
                             "DELETE FROM rpg_fusion_discovery;", "DELETE FROM rpg_patron;",
                             "DELETE FROM rpg_player_commander;",
                             "DELETE FROM rpg_demon_contracts;", "DELETE FROM rpg_contract_state;",
                             "DELETE FROM rpg_unique_actors;",
                             "DELETE FROM rpg_aptitude_allocation;",
                             "DELETE FROM archive_catalog;",
                             // world-stage W21: found missing here while building an E2E fixture
                             // test — a world created in one test class outlived every later
                             // `/api/test/reset`, so any subsequent test reusing the same world id
                             // (a natural choice, e.g. "first-light") hit `world.exists` against an
                             // orphaned row whose owning player this same reset had already deleted.
                             "DELETE FROM rpg_world_turn_log;", "DELETE FROM rpg_world_turn_commits;",
                             "DELETE FROM rpg_world_commands;", "DELETE FROM rpg_world_entity_members;",
                             "DELETE FROM rpg_world_faction_intel;", "DELETE FROM rpg_world_entities;",
                             "DELETE FROM rpg_world_lanes;", "DELETE FROM rpg_world_slots;",
                             "DELETE FROM rpg_world_sectors;", "DELETE FROM rpg_world_factions;",
                             "DELETE FROM rpg_worlds;",
                             "DELETE FROM players;"
                         })
                {
                    try { Exec(db, sql); } catch { /* table may not exist yet */ }
                }
                try { Exec(db, "DELETE FROM sqlite_sequence;"); } catch { /* not created yet */ }
            }
            using (var media = OpenMediaUnlocked())
            {
                foreach (var sql in new[]
                         {
                             "DELETE FROM type_almanac_dump;", "DELETE FROM type_icon_layers;", "DELETE FROM type_icons;"
                         })
                {
                    try { Exec(media, sql); } catch { /* table may not exist yet */ }
                }
                try { Exec(media, "DELETE FROM sqlite_sequence;"); } catch { /* not created yet */ }
            }
            if (Directory.Exists(ArchiveDir))
            {
                SqliteConnection.ClearAllPools();
                foreach (var f in Directory.EnumerateFiles(ArchiveDir))
                {
                    try { File.Delete(f); } catch { /* locked sidecar */ }
                }
            }
        }
        Init();
    }

    public bool InjectorConnected =>
        LastHeartbeatUtc is { } t && DateTimeOffset.UtcNow - t < TimeSpan.FromSeconds(5);

    public bool LiveInjector =>
        Source == RpgConstants.SourceInjector && InjectorConnected;

    public void Heartbeat(string? source = null)
    {
        var s = NormalizeSource(source);
        LastHeartbeatUtc = DateTimeOffset.UtcNow;
        if (s != RpgConstants.SourceNone)
            Source = s;
        UpsertMetric("injector_connected", 1);
    }

    public HealthDto ToHealth(bool simEnabled) => new()
    {
        Ok = true,
        InjectorConnected = InjectorConnected,
        LastHeartbeatUtc = LastHeartbeatUtc?.ToString("o"),
        SimEnabled = simEnabled,
        Source = InjectorConnected ? Source : RpgConstants.SourceNone,
        CurrentPlayerId = GetCurrentPlayerId(),
        // E46 (player-content-boot): imported vs. shipped code fallback, on the one surface both the
        // player and the owner already read. ContentSource/ContentImportError are set once at startup
        // by RecordContentBootOutcome; CatalogRevision is read live since it can only ever move up.
        ContentSource = ContentSource,
        CatalogRevision = GetCatalogRevision(),
        ContentImportError = ContentImportError,
    };

    static string NormalizeSource(string? source)
    {
        if (string.Equals(source, RpgConstants.SourceSim, StringComparison.OrdinalIgnoreCase))
            return RpgConstants.SourceSim;
        if (string.IsNullOrWhiteSpace(source) ||
            string.Equals(source, RpgConstants.SourceInjector, StringComparison.OrdinalIgnoreCase))
            return RpgConstants.SourceInjector;
        return RpgConstants.SourceNone;
    }

    public long GetCurrentPlayerId()
    {
        using var db = Open();
        return GetCurrentPlayerIdUnlocked(db);
    }

    public PlayerDto? GetCurrentPlayer()
    {
        using var db = Open();
        return GetPlayerUnlocked(db, GetCurrentPlayerIdUnlocked(db));
    }

    public List<PlayerDto> ListPlayers()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_utc, world_seed FROM players ORDER BY id;";
        var list = new List<PlayerDto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadPlayer(r));
        return list;
    }

    public PlayerDto CreatePlayer(string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText =
            "INSERT INTO players(name, created_utc, world_seed) VALUES($n,$t,$s); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", trimmed);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        // world-seed (T5.1): rolled once, here, at player creation (spec-world-seed.md's own
        // "created at player creation... never regenerated"). [1, long.MaxValue) excludes the 0
        // sentinel BackfillWorldSeedsUnlocked treats as "not yet assigned."
        cmd.Parameters.AddWithValue("$s", System.Random.Shared.NextInt64(1, long.MaxValue));
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        EnsurePvzStatsRevisionUnlocked(db, id);
        EnsurePvzActivityRevisionUnlocked(db, id);
        return GetPlayerUnlocked(db, id)!;
    }

    public bool SetCurrentPlayer(long id)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, id) is null) return false;
        PutSettingUnlocked(db, "current_player_id", id.ToString());
        return true;
    }

    // --- PvzStats (player-bound Xi SSOT + derived sheet cache) ---

    public bool PlayerExists(long playerId)
    {
        using var db = Open();
        return GetPlayerUnlocked(db, playerId) is not null;
    }

    public PvzStatsSheetDto? GetPvzStatsSheet(long playerId)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null) return null;
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        EnsurePvzStatsSnapshotUnlocked(db, playerId);
        return ReadPvzStatsSheetUnlocked(db, playerId);
    }

    public PvzStatsChannelDetailDto? GetPvzStatsChannel(long playerId, string channel)
    {
        var canon = FusionRpg.Core.Stats.PvzStatsSheetComposer.TryCanonicalizeChannel(channel);
        if (canon is null) return null;
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null) return null;
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        EnsurePvzStatsSnapshotUnlocked(db, playerId);
        var rev = GetPvzStatsRevisionUnlocked(db, playerId);
        double final = 0;
        using (var snapCmd = db.CreateCommand())
        {
            snapCmd.CommandText = "SELECT finals_json FROM pvz_stat_snapshots WHERE player_id=$p;";
            snapCmd.Parameters.AddWithValue("$p", playerId);
            var raw = snapCmd.ExecuteScalar() as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty(canon, out var el) && el.TryGetDouble(out var d))
                        final = d;
                }
                catch { /* ignore */ }
            }
        }
        var contribs = new List<PvzStatContributionDto>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT channel, plugin_id, source_kind, source_id, op, value, priority, detail_json
                FROM pvz_stat_contributions
                WHERE player_id=$p AND revision=$r AND channel=$c
                ORDER BY plugin_id, source_kind, source_id, op;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$r", rev);
            cmd.Parameters.AddWithValue("$c", canon);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                contribs.Add(ReadPvzContribution(r));
        }
        return new PvzStatsChannelDetailDto
        {
            PlayerId = playerId,
            Revision = rev,
            Channel = canon,
            Final = final,
            Contributions = contribs
        };
    }

    public PvzStatsModifiersDto? GetPvzStatsModifiers(long playerId)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null) return null;
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        return new PvzStatsModifiersDto
        {
            PlayerId = playerId,
            Revision = GetPvzStatsRevisionUnlocked(db, playerId),
            Modifiers = ListPvzModifiersUnlocked(db, playerId)
        };
    }

    public PvzStatsSheetDto UpsertPvzStatModifier(long playerId, PvzStatModifierDto mod)
    {
        if (mod == null) throw new ArgumentNullException(nameof(mod));
        var channel = FusionRpg.Core.Stats.PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel(mod.Channel)
                      ?? throw new ArgumentException("unknown channel", nameof(mod));
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null)
            throw new InvalidOperationException("player not found");
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        var pluginId = string.IsNullOrWhiteSpace(mod.PluginId) ? "rpg.item" : mod.PluginId.Trim();
        var sourceKind = string.IsNullOrWhiteSpace(mod.SourceKind) ? "item" : mod.SourceKind.Trim();
        var sourceId = string.IsNullOrWhiteSpace(mod.SourceId) ? "unknown" : mod.SourceId.Trim();
        var op = string.IsNullOrWhiteSpace(mod.Op) ? "Flat" : mod.Op.Trim();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO pvz_stat_modifiers(player_id, plugin_id, source_kind, source_id, channel, op, value, priority, enabled, detail_json)
                VALUES($p,$pl,$sk,$sid,$ch,$op,$v,$pri,$en,$dj)
                ON CONFLICT(player_id, plugin_id, source_kind, source_id, channel, op) DO UPDATE SET
                  value=excluded.value,
                  priority=excluded.priority,
                  enabled=excluded.enabled,
                  detail_json=excluded.detail_json;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$pl", pluginId);
            cmd.Parameters.AddWithValue("$sk", sourceKind);
            cmd.Parameters.AddWithValue("$sid", sourceId);
            cmd.Parameters.AddWithValue("$ch", channel);
            cmd.Parameters.AddWithValue("$op", op);
            cmd.Parameters.AddWithValue("$v", mod.Value);
            cmd.Parameters.AddWithValue("$pri", mod.Priority);
            cmd.Parameters.AddWithValue("$en", mod.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$dj", (object?)mod.DetailJson ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        BumpPvzStatsAndRebuildUnlocked(db, playerId);
        return ReadPvzStatsSheetUnlocked(db, playerId);
    }

    public PvzStatsSheetDto WithdrawPvzStatModifiers(long playerId, string? sourceKind, string? sourceId, string? channel = null, string? op = null, string? pluginId = null)
    {
        var hasFilter = !string.IsNullOrWhiteSpace(sourceKind)
                        || !string.IsNullOrWhiteSpace(sourceId)
                        || !string.IsNullOrWhiteSpace(channel)
                        || !string.IsNullOrWhiteSpace(op)
                        || !string.IsNullOrWhiteSpace(pluginId);
        if (!hasFilter)
            throw new ArgumentException("withdraw requires at least one filter; use reset to clear all");
        var canonChannel = string.IsNullOrWhiteSpace(channel)
            ? null
            : FusionRpg.Core.Stats.PvzStatsSheetComposer.TryCanonicalizeOrDerivedChannel(channel)
              ?? throw new ArgumentException("unknown channel");
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null)
            throw new InvalidOperationException("player not found");
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        using var cmd = db.CreateCommand();
        var sql = "DELETE FROM pvz_stat_modifiers WHERE player_id=$p";
        cmd.Parameters.AddWithValue("$p", playerId);
        if (!string.IsNullOrWhiteSpace(pluginId))
        {
            sql += " AND plugin_id=$pl";
            cmd.Parameters.AddWithValue("$pl", pluginId);
        }
        if (!string.IsNullOrWhiteSpace(sourceKind))
        {
            sql += " AND source_kind=$sk";
            cmd.Parameters.AddWithValue("$sk", sourceKind);
        }
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            sql += " AND source_id=$sid";
            cmd.Parameters.AddWithValue("$sid", sourceId);
        }
        if (canonChannel != null)
        {
            sql += " AND channel=$ch";
            cmd.Parameters.AddWithValue("$ch", canonChannel);
        }
        if (!string.IsNullOrWhiteSpace(op))
        {
            sql += " AND op=$op";
            cmd.Parameters.AddWithValue("$op", op);
        }
        cmd.CommandText = sql + ";";
        cmd.ExecuteNonQuery();
        BumpPvzStatsAndRebuildUnlocked(db, playerId);
        return ReadPvzStatsSheetUnlocked(db, playerId);
    }

    public PvzStatsSheetDto ResetPvzStats(long playerId)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null)
            throw new InvalidOperationException("player not found");
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM pvz_stat_modifiers WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.ExecuteNonQuery();
        }
        BumpPvzStatsAndRebuildUnlocked(db, playerId);
        return ReadPvzStatsSheetUnlocked(db, playerId);
    }

    public PvzStatsSheetDto SeedPvzStatsDemo(long playerId)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null)
            throw new InvalidOperationException("player not found");
        EnsurePvzStatsRevisionUnlocked(db, playerId);
        using (var del = db.CreateCommand())
        {
            del.CommandText = "DELETE FROM pvz_stat_modifiers WHERE player_id=$p;";
            del.Parameters.AddWithValue("$p", playerId);
            del.ExecuteNonQuery();
        }
        void Insert(string sourceId, string channel, double value, string label)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pvz_stat_modifiers(player_id, plugin_id, source_kind, source_id, channel, op, value, priority, enabled, detail_json)
                VALUES($p,'rpg.item','item',$sid,$ch,'Flat',$v,0,1,$dj);
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$sid", sourceId);
            cmd.Parameters.AddWithValue("$ch", channel);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.Parameters.AddWithValue("$dj", $"{{\"label\":\"{label}\",\"href\":null}}");
            cmd.ExecuteNonQuery();
        }
        // Pair hp + maxHp so combat write does not leave current > max.
        Insert("demo-ring", "hp", 10, "Ring of Life");
        Insert("demo-ring", "maxHp", 10, "Ring of Life");
        Insert("demo-curse", "hp", -5, "Cursed Band");
        Insert("demo-curse", "maxHp", -5, "Cursed Band");
        BumpPvzStatsAndRebuildUnlocked(db, playerId);
        return ReadPvzStatsSheetUnlocked(db, playerId);
    }

    void BumpPvzStatsAndRebuildUnlocked(SqliteConnection db, long playerId)
    {
        Exec(db, "BEGIN IMMEDIATE;");
        try
        {
            var now = DateTime.UtcNow.ToString("o");
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "UPDATE pvz_stat_revisions SET revision = revision + 1, updated_utc=$t WHERE player_id=$p;";
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$t", now);
                cmd.ExecuteNonQuery();
            }
            var rev = GetPvzStatsRevisionUnlocked(db, playerId);
            RebuildPvzStatsSnapshotUnlocked(db, playerId, rev, now);
            Exec(db, "COMMIT;");
        }
        catch
        {
            try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
            throw;
        }
    }

    void RebuildPvzStatsSnapshotUnlocked(SqliteConnection db, long playerId, long revision, string updatedUtc)
    {
        var rows = ListPvzModifiersUnlocked(db, playerId);
        var mods = rows.Where(m => m.Enabled).Select(m =>
            FusionRpg.Core.Stats.PvzStatsSheetComposer.ToStatModifier(
                m.PluginId, m.SourceKind, m.SourceId, m.Channel, m.Op, m.Value, m.Priority)).ToList();

        var sheet = FusionRpg.Core.Stats.PvzStatsSheetComposer.Build(mods);
        var finals = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var ch in sheet.Channels)
            finals[ch.Channel] = ch.Final;

        using (var del = db.CreateCommand())
        {
            del.CommandText = "DELETE FROM pvz_stat_contributions WHERE player_id=$p;";
            del.Parameters.AddWithValue("$p", playerId);
            del.ExecuteNonQuery();
        }
        foreach (var m in rows.Where(x => x.Enabled))
        {
            using var ins = db.CreateCommand();
            ins.CommandText = """
                INSERT INTO pvz_stat_contributions(player_id, revision, channel, plugin_id, source_kind, source_id, op, value, priority, detail_json)
                VALUES($p,$r,$ch,$pl,$sk,$sid,$op,$v,$pri,$dj);
                """;
            ins.Parameters.AddWithValue("$p", playerId);
            ins.Parameters.AddWithValue("$r", revision);
            ins.Parameters.AddWithValue("$ch", m.Channel);
            ins.Parameters.AddWithValue("$pl", m.PluginId);
            ins.Parameters.AddWithValue("$sk", m.SourceKind);
            ins.Parameters.AddWithValue("$sid", m.SourceId);
            ins.Parameters.AddWithValue("$op", m.Op);
            ins.Parameters.AddWithValue("$v", m.Value);
            ins.Parameters.AddWithValue("$pri", m.Priority);
            ins.Parameters.AddWithValue("$dj", (object?)m.DetailJson ?? DBNull.Value);
            ins.ExecuteNonQuery();
        }

        using (var snap = db.CreateCommand())
        {
            snap.CommandText = """
                INSERT INTO pvz_stat_snapshots(player_id, revision, finals_json, updated_utc)
                VALUES($p,$r,$j,$t)
                ON CONFLICT(player_id) DO UPDATE SET
                  revision=excluded.revision,
                  finals_json=excluded.finals_json,
                  updated_utc=excluded.updated_utc;
                """;
            snap.Parameters.AddWithValue("$p", playerId);
            snap.Parameters.AddWithValue("$r", revision);
            snap.Parameters.AddWithValue("$j", JsonSerializer.Serialize(finals));
            snap.Parameters.AddWithValue("$t", updatedUtc);
            snap.ExecuteNonQuery();
        }
    }

    void EnsurePvzStatsSnapshotUnlocked(SqliteConnection db, long playerId)
    {
        var rev = GetPvzStatsRevisionUnlocked(db, playerId);
        long? snapRev = null;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT revision FROM pvz_stat_snapshots WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            var o = cmd.ExecuteScalar();
            if (o is not null and not DBNull)
                snapRev = Convert.ToInt64(o);
        }
        if (snapRev == rev) return;
        RebuildPvzStatsSnapshotUnlocked(db, playerId, rev, DateTime.UtcNow.ToString("o"));
    }

    PvzStatsSheetDto ReadPvzStatsSheetUnlocked(SqliteConnection db, long playerId)
    {
        var rev = GetPvzStatsRevisionUnlocked(db, playerId);
        var updated = "";
        var finals = new Dictionary<string, double>(StringComparer.Ordinal);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT revision, finals_json, updated_utc FROM pvz_stat_snapshots WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                rev = r.GetInt64(0);
                updated = r.IsDBNull(2) ? "" : r.GetString(2);
                try
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, double>>(r.GetString(1));
                    if (map != null)
                        foreach (var kv in map) finals[kv.Key] = kv.Value;
                }
                catch { /* ignore */ }
            }
        }
        var sourceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                SELECT channel, COUNT(DISTINCT source_kind || '|' || source_id)
                FROM pvz_stat_contributions WHERE player_id=$p AND revision=$r
                GROUP BY channel;
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$r", rev);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                sourceCounts[r.GetString(0)] = r.GetInt32(1);
        }
        var channels = new List<PvzStatsChannelSummaryDto>();
        foreach (var kv in finals.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sourceCounts.TryGetValue(kv.Key, out var sc);
            channels.Add(new PvzStatsChannelSummaryDto
            {
                Channel = kv.Key,
                Final = kv.Value,
                SourceCount = sc
            });
        }
        // Include channels that have contribs but zero final missing from map
        foreach (var kv in sourceCounts)
        {
            if (finals.ContainsKey(kv.Key)) continue;
            channels.Add(new PvzStatsChannelSummaryDto { Channel = kv.Key, Final = 0, SourceCount = kv.Value });
        }
        return new PvzStatsSheetDto
        {
            PlayerId = playerId,
            Revision = rev,
            UpdatedAt = updated,
            Channels = channels.OrderBy(c => c.Channel, StringComparer.Ordinal).ToList()
        };
    }

    List<PvzStatModifierDto> ListPvzModifiersUnlocked(SqliteConnection db, long playerId)
    {
        var list = new List<PvzStatModifierDto>();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT plugin_id, source_kind, source_id, channel, op, value, priority, enabled, detail_json
            FROM pvz_stat_modifiers WHERE player_id=$p
            ORDER BY plugin_id, source_kind, source_id, channel, op;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PvzStatModifierDto
            {
                PluginId = r.GetString(0),
                SourceKind = r.GetString(1),
                SourceId = r.GetString(2),
                Channel = r.GetString(3),
                Op = r.GetString(4),
                Value = r.GetDouble(5),
                Priority = r.GetInt32(6),
                Enabled = r.GetInt32(7) != 0,
                DetailJson = r.IsDBNull(8) ? null : r.GetString(8)
            });
        }
        return list;
    }

    static PvzStatContributionDto ReadPvzContribution(SqliteDataReader r) => new()
    {
        Channel = r.GetString(0),
        PluginId = r.GetString(1),
        SourceKind = r.GetString(2),
        SourceId = r.GetString(3),
        Op = r.GetString(4),
        Value = r.GetDouble(5),
        Priority = r.GetInt32(6),
        DetailJson = r.IsDBNull(7) ? null : r.GetString(7)
    };

    long GetPvzStatsRevisionUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT revision FROM pvz_stat_revisions WHERE player_id=$p;";
        cmd.Parameters.AddWithValue("$p", playerId);
        var o = cmd.ExecuteScalar();
        return o is null or DBNull ? 0 : Convert.ToInt64(o);
    }

    void EnsurePvzStatsRevisionUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO pvz_stat_revisions(player_id, revision, updated_utc)
            VALUES($p, 0, $t);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    void EnsurePvzStatsRevisionForAllPlayers(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id FROM players;";
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read()) ids.Add(r.GetInt64(0));
        r.Close();
        foreach (var id in ids)
            EnsurePvzStatsRevisionUnlocked(db, id);
    }

    // --- PvzActivity (append facts + rollup cache) ---

    public PvzActivityRollupDto? GetPvzActivityRollup(long playerId)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null) return null;
        EnsurePvzActivityRevisionUnlocked(db, playerId);
        EnsurePvzActivityRollupUnlocked(db, playerId);
        return ReadPvzActivityRollupUnlocked(db, playerId);
    }

    public PvzActivityFactsPageDto? ListPvzActivityFacts(long playerId, string? kind = null, long? runId = null, int limit = 100)
    {
        using var db = Open();
        if (GetPlayerUnlocked(db, playerId) is null) return null;
        EnsurePvzActivityRevisionUnlocked(db, playerId);
        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;
        var items = new List<PvzActivityFactDto>();
        using (var cmd = db.CreateCommand())
        {
            var sql = """
                SELECT id, player_id, run_id, t, kind, plugin_id, source_kind, source_id, payload_json, match_key, dedupe_key
                FROM pvz_activity_facts WHERE player_id=$p
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                sql += " AND kind=$k";
                cmd.Parameters.AddWithValue("$k", kind.Trim());
            }
            if (runId is { } rid)
            {
                sql += " AND run_id=$r";
                cmd.Parameters.AddWithValue("$r", rid);
            }
            sql += " ORDER BY id DESC LIMIT $lim;";
            cmd.Parameters.AddWithValue("$lim", limit);
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read())
                items.Add(ReadPvzActivityFact(r));
        }
        return new PvzActivityFactsPageDto
        {
            PlayerId = playerId,
            Revision = GetPvzActivityRevisionUnlocked(db, playerId),
            Items = items
        };
    }

    public readonly record struct PvzActivityAppendResult(
        PvzActivityRollupDto Rollup,
        IReadOnlyList<RpgProgressionDirty> Progression);

    public PvzActivityAppendResult AppendPvzActivityFact(long playerId, PvzActivityAppendRequest req)
    {
        if (req == null) throw new ArgumentNullException(nameof(req));
        if (string.IsNullOrWhiteSpace(req.Kind)) throw new ArgumentException("kind required");
        var kind = req.Kind.Trim();
        if (!FusionRpg.Core.Activity.PvzActivityKinds.IsKnown(kind))
            throw new ArgumentException("unknown activity kind");
        lock (_gate)
        {
            using var db = OpenUnlocked();
            Exec(db, "BEGIN IMMEDIATE;");
            try
            {
                if (GetPlayerUnlocked(db, playerId) is null)
                    throw new InvalidOperationException("player not found");
                EnsurePvzActivityRevisionUnlocked(db, playerId);
                var t = DateTime.UtcNow.ToString("o");
                var dedupe = string.IsNullOrWhiteSpace(req.DedupeKey)
                    ? Guid.NewGuid().ToString("N")
                    : req.DedupeKey.Trim();
                var inserted = InsertPvzActivityFactUnlocked(
                    db, playerId, req.RunId, t, kind,
                    string.IsNullOrWhiteSpace(req.PluginId) ? "rpg.feature" : req.PluginId.Trim(),
                    string.IsNullOrWhiteSpace(req.SourceKind) ? "feature" : req.SourceKind.Trim(),
                    string.IsNullOrWhiteSpace(req.SourceId) ? "manual" : req.SourceId.Trim(),
                    req.PayloadJson, req.MatchKey, dedupe);
                IReadOnlyList<RpgProgressionDirty> progression = Array.Empty<RpgProgressionDirty>();
                if (inserted.Inserted)
                {
                    BumpAndApplyPvzActivityDeltaUnlocked(db, playerId, inserted.FactId, kind, req.PayloadJson);
                    progression = ApplyRpgProgressionFromActivityUnlocked(
                        db, playerId, req.RunId, t, kind, req.PayloadJson ?? "{}", dedupe, inserted.FactId);
                }
                else
                    EnsurePvzActivityRollupUnlocked(db, playerId);
                var rollup = ReadPvzActivityRollupUnlocked(db, playerId);
                Exec(db, "COMMIT;");
                return new PvzActivityAppendResult(rollup, progression);
            }
            catch
            {
                try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
                throw;
            }
        }
    }

    public PvzActivityRollupDto SeedPvzActivityDemo(long playerId)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            Exec(db, "BEGIN IMMEDIATE;");
            try
            {
                if (GetPlayerUnlocked(db, playerId) is null)
                    throw new InvalidOperationException("player not found");
                EnsurePvzActivityRevisionUnlocked(db, playerId);
                var t = DateTime.UtcNow.ToString("o");
                var any = false;
                foreach (var (kind, payload, dedupe) in new[]
                         {
                             (FusionRpg.Core.Activity.PvzActivityKinds.MatchStarted, """{"demo":true}""", "seed-match"),
                             (FusionRpg.Core.Activity.PvzActivityKinds.ZombieKilled, """{"type":1}""", "seed-zk-1"),
                             (FusionRpg.Core.Activity.PvzActivityKinds.ZombieKilled, """{"type":2}""", "seed-zk-2"),
                             (FusionRpg.Core.Activity.PvzActivityKinds.MatchEnded, """{"result":"victory"}""", "seed-match-end")
                         })
                {
                    var inserted = InsertPvzActivityFactUnlocked(
                        db, playerId, null, t, kind, "pvz.activity", "seed", "demo", payload, null, dedupe);
                    if (!inserted.Inserted) continue;
                    any = true;
                    BumpAndApplyPvzActivityDeltaUnlocked(db, playerId, inserted.FactId, kind, payload);
                }
                if (!any)
                    EnsurePvzActivityRollupUnlocked(db, playerId);
                var rollup = ReadPvzActivityRollupUnlocked(db, playerId);
                Exec(db, "COMMIT;");
                return rollup;
            }
            catch
            {
                try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
                throw;
            }
        }
    }

    /// <summary>Returns rollup and whether a new ExtraSpawnFired fact was inserted (gates Intent command).</summary>
    public (PvzActivityRollupDto Rollup, bool Inserted) RecordExtraSpawnIntent(long playerId, string correlationId, int typeId, string reason, string side)
    {
        lock (_gate)
        {
            using var db = OpenUnlocked();
            Exec(db, "BEGIN IMMEDIATE;");
            try
            {
                if (GetPlayerUnlocked(db, playerId) is null)
                    throw new InvalidOperationException("player not found");
                EnsurePvzActivityRevisionUnlocked(db, playerId);
                var payload = JsonSerializer.Serialize(new
                {
                    typeId,
                    reason,
                    side,
                    correlationId,
                    source = "extra"
                });
                var inserted = InsertPvzActivityFactUnlocked(
                    db, playerId, null, DateTime.UtcNow.ToString("o"),
                    FusionRpg.Core.Activity.PvzActivityKinds.ExtraSpawnFired,
                    "pvz.spawn", "intent", "extra", payload, null, correlationId);
                if (inserted.Inserted)
                    BumpAndApplyPvzActivityDeltaUnlocked(db, playerId, inserted.FactId,
                        FusionRpg.Core.Activity.PvzActivityKinds.ExtraSpawnFired, payload);
                else
                    EnsurePvzActivityRollupUnlocked(db, playerId);
                var rollup = ReadPvzActivityRollupUnlocked(db, playerId);
                Exec(db, "COMMIT;");
                return (rollup, inserted.Inserted);
            }
            catch
            {
                try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
                throw;
            }
        }
    }

    void ProjectPvzActivityFromCapture(SqliteConnection db, string kind, string payload, string t, long playerId, long runId, string? matchKey, bool pvzGame = true)
    {
        var factKind = FusionRpg.Core.Activity.PvzActivityKinds.FromCaptureKind(kind);
        if (factKind is null) return;
        EnsurePvzActivityRevisionUnlocked(db, playerId);
        var dedupe = FusionRpg.Core.Activity.PvzActivityKinds.DedupeKeyForCapture(
            factKind,
            TryString(payload, "ptr"),
            TryInt(payload, "col"),
            TryInt(payload, "row"),
            t);
        var inserted = InsertPvzActivityFactUnlocked(
            db, playerId, runId, t, factKind,
            "pvz.capture", "capture", kind, payload, matchKey, dedupe);
        if (inserted.Inserted)
        {
            BumpAndApplyPvzActivityDeltaUnlocked(db, playerId, inserted.FactId, factKind, payload);
            _activityNotifyBatch?.Add(playerId);
            ApplyRpgProgressionFromActivityUnlocked(
                db, playerId, runId, t, factKind, payload, dedupe, inserted.FactId, pvzGame);
            // Soul earns ride the same transaction as the fact — a crash can never lose one (spec-soul-economy.md).
            ApplySoulEarnFromActivityUnlocked(db, playerId, runId, t, factKind, payload, inserted.FactId);
        }
    }

    (bool Inserted, long FactId) InsertPvzActivityFactUnlocked(
        SqliteConnection db, long playerId, long? runId, string t, string kind,
        string pluginId, string sourceKind, string sourceId, string? payloadJson, string? matchKey, string? dedupeKey)
    {
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO pvz_activity_facts(
                  player_id, run_id, t, kind, plugin_id, source_kind, source_id, payload_json, match_key, dedupe_key)
                VALUES($p,$r,$t,$k,$pl,$sk,$sid,$pj,$m,$d);
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$r", runId ?? 0L);
            cmd.Parameters.AddWithValue("$t", t);
            cmd.Parameters.AddWithValue("$k", kind);
            cmd.Parameters.AddWithValue("$pl", pluginId);
            cmd.Parameters.AddWithValue("$sk", sourceKind);
            cmd.Parameters.AddWithValue("$sid", sourceId);
            cmd.Parameters.AddWithValue("$pj", (object?)payloadJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$m", (object?)matchKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$d", string.IsNullOrWhiteSpace(dedupeKey) ? "" : dedupeKey);
            if (cmd.ExecuteNonQuery() <= 0)
                return (false, 0);
        }
        using var idCmd = db.CreateCommand();
        idCmd.CommandText = "SELECT last_insert_rowid();";
        return (true, (long)(idCmd.ExecuteScalar() ?? 0L));
    }

    void BumpAndApplyPvzActivityDeltaUnlocked(
        SqliteConnection db, long playerId, long factId, string kind, string? payloadJson)
    {
        var now = DateTime.UtcNow.ToString("o");
        using (var bump = db.CreateCommand())
        {
            bump.CommandText = "UPDATE pvz_activity_revisions SET revision = revision + 1, updated_utc=$t WHERE player_id=$p;";
            bump.Parameters.AddWithValue("$p", playerId);
            bump.Parameters.AddWithValue("$t", now);
            bump.ExecuteNonQuery();
        }

        if (!TryReadPvzActivityRollupStateUnlocked(db, playerId, out var counters, out var throughFactId, out var schemaVersion)
            || schemaVersion != SealedCompactionPolicy.ActivitySnapshotSchemaVersion
            || factId <= throughFactId)
        {
            RebuildPvzActivityRollupUnlocked(db, playerId, now);
            return;
        }

        FusionRpg.Core.Activity.PvzActivityRollupBuilder.ApplyDelta(counters, kind, payloadJson);
        UpsertPvzActivityRollupUnlocked(db, playerId, counters, factId, SealedCompactionPolicy.ActivitySnapshotSchemaVersion, now);
    }

    void RebuildPvzActivityRollupUnlocked(SqliteConnection db, long playerId, string? updatedUtc = null)
    {
        var pairs = new List<(string Kind, string? Payload)>();
        long maxFactId = 0;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT id, kind, payload_json FROM pvz_activity_facts WHERE player_id=$p ORDER BY id;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                maxFactId = Math.Max(maxFactId, r.GetInt64(0));
                pairs.Add((r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
            }
        }
        var counters = FusionRpg.Core.Activity.PvzActivityRollupBuilder.Build(pairs);
        var now = updatedUtc ?? DateTime.UtcNow.ToString("o");
        UpsertPvzActivityRollupUnlocked(db, playerId, counters, maxFactId, SealedCompactionPolicy.ActivitySnapshotSchemaVersion, now);
    }

    void UpsertPvzActivityRollupUnlocked(
        SqliteConnection db, long playerId,
        FusionRpg.Core.Activity.PvzActivityRollupCounters counters,
        long throughFactId, int schemaVersion, string updatedUtc)
    {
        var rev = GetPvzActivityRevisionUnlocked(db, playerId);
        using var snap = db.CreateCommand();
        snap.CommandText = """
            INSERT INTO pvz_activity_rollups(player_id, revision, counters_json, updated_utc, through_fact_id, schema_version)
            VALUES($p,$r,$j,$t,$tf,$sv)
            ON CONFLICT(player_id) DO UPDATE SET
              revision=excluded.revision,
              counters_json=excluded.counters_json,
              updated_utc=excluded.updated_utc,
              through_fact_id=excluded.through_fact_id,
              schema_version=excluded.schema_version;
            """;
        snap.Parameters.AddWithValue("$p", playerId);
        snap.Parameters.AddWithValue("$r", rev);
        snap.Parameters.AddWithValue("$j", JsonSerializer.Serialize(counters, Json));
        snap.Parameters.AddWithValue("$t", updatedUtc);
        snap.Parameters.AddWithValue("$tf", throughFactId);
        snap.Parameters.AddWithValue("$sv", schemaVersion);
        snap.ExecuteNonQuery();
    }

    bool TryReadPvzActivityRollupStateUnlocked(
        SqliteConnection db, long playerId,
        out FusionRpg.Core.Activity.PvzActivityRollupCounters counters,
        out long throughFactId,
        out int schemaVersion)
    {
        counters = new FusionRpg.Core.Activity.PvzActivityRollupCounters();
        throughFactId = 0;
        schemaVersion = 0;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT counters_json, through_fact_id, schema_version
            FROM pvz_activity_rollups WHERE player_id=$p;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<FusionRpg.Core.Activity.PvzActivityRollupCounters>(r.GetString(0), Json);
            if (parsed is null) return false;
            counters = parsed;
        }
        catch
        {
            return false;
        }
        throughFactId = r.IsDBNull(1) ? 0 : r.GetInt64(1);
        schemaVersion = r.IsDBNull(2) ? 0 : r.GetInt32(2);
        return true;
    }

    void EnsurePvzActivityRollupUnlocked(SqliteConnection db, long playerId)
    {
        var rev = GetPvzActivityRevisionUnlocked(db, playerId);
        long? snapRev = null;
        int schemaVersion = 0;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT revision, schema_version FROM pvz_activity_rollups WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                snapRev = r.GetInt64(0);
                schemaVersion = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            }
        }
        if (snapRev == rev && schemaVersion == SealedCompactionPolicy.ActivitySnapshotSchemaVersion)
            return;
        RebuildPvzActivityRollupUnlocked(db, playerId);
    }

    PvzActivityRollupDto ReadPvzActivityRollupUnlocked(SqliteConnection db, long playerId)
    {
        var rev = GetPvzActivityRevisionUnlocked(db, playerId);
        var updated = "";
        var counters = new FusionRpg.Core.Activity.PvzActivityRollupCounters();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT revision, counters_json, updated_utc FROM pvz_activity_rollups WHERE player_id=$p;";
            cmd.Parameters.AddWithValue("$p", playerId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                rev = r.GetInt64(0);
                updated = r.IsDBNull(2) ? "" : r.GetString(2);
                try
                {
                    counters = JsonSerializer.Deserialize<FusionRpg.Core.Activity.PvzActivityRollupCounters>(r.GetString(1), Json)
                               ?? counters;
                }
                catch { /* ignore */ }
            }
        }
        return new PvzActivityRollupDto
        {
            PlayerId = playerId,
            Revision = rev,
            UpdatedAt = updated,
            MatchesStarted = counters.MatchesStarted,
            MatchesEnded = counters.MatchesEnded,
            Victories = counters.Victories,
            Defeats = counters.Defeats,
            ZombiesKilled = counters.ZombiesKilled,
            PlantsLost = counters.PlantsLost,
            PlantsPlaced = counters.PlantsPlaced,
            ExtraSpawnsFired = counters.ExtraSpawnsFired
        };
    }

    static PvzActivityFactDto ReadPvzActivityFact(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        PlayerId = r.GetInt64(1),
        RunId = r.IsDBNull(2) || r.GetInt64(2) == 0 ? null : r.GetInt64(2),
        T = r.GetString(3),
        Kind = r.GetString(4),
        PluginId = r.GetString(5),
        SourceKind = r.GetString(6),
        SourceId = r.GetString(7),
        PayloadJson = r.IsDBNull(8) ? null : r.GetString(8),
        MatchKey = r.IsDBNull(9) ? null : r.GetString(9),
        DedupeKey = r.IsDBNull(10) ? null : r.GetString(10)
    };

    long GetPvzActivityRevisionUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT revision FROM pvz_activity_revisions WHERE player_id=$p;";
        cmd.Parameters.AddWithValue("$p", playerId);
        var o = cmd.ExecuteScalar();
        return o is null or DBNull ? 0 : Convert.ToInt64(o);
    }

    void EnsurePvzActivityRevisionUnlocked(SqliteConnection db, long playerId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO pvz_activity_revisions(player_id, revision, updated_utc)
            VALUES($p, 0, $t);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    void EnsurePvzActivityRevisionForAllPlayers(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id FROM players;";
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read()) ids.Add(r.GetInt64(0));
        r.Close();
        foreach (var id in ids)
            EnsurePvzActivityRevisionUnlocked(db, id);
    }

    public StatsConfig GetStats()
    {
        var json = GetSetting("stats");
        if (json is null) return new StatsConfig();
        return JsonSerializer.Deserialize<StatsConfig>(json, Json) ?? new StatsConfig();
    }

    public void PutStats(StatsConfig stats)
    {
        using var db = Open();
        PutStatsUnlocked(db, stats);
    }

    /// <summary>Stored cheats JSON (migrated), without response-only <c>mods</c>.</summary>
    public string? GetCheatsJsonRaw()
    {
        using var db = Open();
        var raw = GetSettingUnlocked(db, "cheats");
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var migrated = MigrateCheatsJson(raw, out var changed);
        if (changed)
            PutSettingUnlocked(db, "cheats", migrated);
        return migrated;
    }

    public string? GetCheatsJson()
    {
        var migrated = GetCheatsJsonRaw();
        if (string.IsNullOrWhiteSpace(migrated)) return migrated;
        return EnrichWithMods(migrated);
    }

    /// <summary>Adds ModDocument-shaped <c>mods</c> for RPG/future consumers; keeps entries for FE.</summary>
    static string EnrichWithMods(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            long revision = root.TryGetProperty("revision", out var rev) && rev.TryGetInt64(out var r) ? r : 0;
            var updatedAt = root.TryGetProperty("updatedAt", out var ua) ? ua.GetString() : null;
            var tuples = new List<(string id, bool enabled, double floatValue, string? kind)>();
            if (root.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var enabled = item.TryGetProperty("enabled", out var en) && en.GetBoolean();
                    var fv = item.TryGetProperty("floatValue", out var fvel) && fvel.TryGetDouble(out var d) ? d : 0d;
                    var kind = item.TryGetProperty("kind", out var k) ? k.GetString() : null;
                    tuples.Add((id, enabled, fv, kind));
                }
            }
            var modDoc = CheatDocumentCodec.FromEntries(revision, "web", tuples, updatedAt);
            var dict = new Dictionary<string, object?>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("mods")) continue;
                dict[prop.Name] = prop.Value.Clone();
            }
            dict["mods"] = modDoc.Mods;
            return JsonSerializer.Serialize(dict);
        }
        catch
        {
            return json;
        }
    }

    public void PutCheatsJson(string json, bool bumpRevision = true)
    {
        using var db = Open();
        var migrated = MigrateCheatsJson(json, out _);
        if (bumpRevision)
            migrated = BumpRevisionJson(migrated);
        PutSettingUnlocked(db, "cheats", migrated);
    }

    public void MergeCheatField(string id, bool? enabled, double? floatValue)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        using var db = Open();
        var raw = GetSettingUnlocked(db, "cheats") ?? """{"menuEnabled":false,"revision":0,"entries":[]}""";
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement.Clone();
        var dict = new Dictionary<string, object?>();
        long revision = 0;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.NameEquals("entries")) continue;
            if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r)) revision = r;
            else dict[prop.Name] = prop.Value.Clone();
        }
        var entries = new List<Dictionary<string, object?>>();
        var found = false;
        if (root.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var eId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var e = new Dictionary<string, object?>
                {
                    ["id"] = eId,
                    ["kind"] = item.TryGetProperty("kind", out var k) ? k.GetString() : "toggle",
                    ["enabled"] = item.TryGetProperty("enabled", out var en) && en.GetBoolean(),
                    ["floatValue"] = item.TryGetProperty("floatValue", out var fv) && fv.TryGetDouble(out var d) ? d : 0,
                    ["isSet"] = true
                };
                if (string.Equals(eId, id, StringComparison.Ordinal))
                {
                    if (enabled is { } on) e["enabled"] = on;
                    if (floatValue is { } v) e["floatValue"] = v;
                    e["isSet"] = true;
                    found = true;
                    var enTarget = e["enabled"] is bool ebT && ebT;
                    var fvTarget = e["floatValue"] is double ddT ? ddT : 0;
                    // Updating to identity/unset → remove (absence = unset).
                    if (CheatSchema.ShouldStripFromDocument(eId, enTarget, fvTarget, e["kind"]?.ToString()))
                        continue;
                }
                else
                {
                    var enB = e["enabled"] is bool eb && eb;
                    var fvD = e["floatValue"] is double dd ? dd : 0;
                    if (CheatSchema.ShouldStripFromDocument(eId, enB, fvD, e["kind"]?.ToString()))
                        continue;
                }
                entries.Add(e);
            }
        }
        if (!found)
        {
            var kind = floatValue.HasValue ? "number" : "toggle";
            var enNew = enabled ?? true;
            var fvNew = floatValue ?? 0;
            if (!CheatSchema.ShouldStripFromDocument(id, enNew, fvNew, kind))
            {
                entries.Add(new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["kind"] = kind,
                    ["enabled"] = enNew,
                    ["floatValue"] = fvNew,
                    ["isSet"] = true
                });
            }
        }
        dict["entries"] = entries;
        dict["revision"] = revision + 1;
        dict["updatedAt"] = DateTime.UtcNow.ToString("o");
        dict["menuEnabled"] = false;
        PutSettingUnlocked(db, "cheats", JsonSerializer.Serialize(dict));
    }

    public void ClearCheatField(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        using var db = Open();
        var raw = GetSettingUnlocked(db, "cheats") ?? """{"menuEnabled":false,"revision":0,"entries":[]}""";
        using var doc = JsonDocument.Parse(raw);
        var dict = new Dictionary<string, object?>();
        long revision = 0;
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("entries")) continue;
            if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r)) revision = r;
            else dict[prop.Name] = prop.Value.Clone();
        }
        var entries = new List<Dictionary<string, object?>>();
        if (doc.RootElement.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var eId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (string.Equals(eId, id, StringComparison.Ordinal)) continue;
                entries.Add(new Dictionary<string, object?>
                {
                    ["id"] = eId,
                    ["kind"] = item.TryGetProperty("kind", out var k) ? k.GetString() : "toggle",
                    ["enabled"] = item.TryGetProperty("enabled", out var en) && en.GetBoolean(),
                    ["floatValue"] = item.TryGetProperty("floatValue", out var fv) && fv.TryGetDouble(out var d) ? d : 0,
                    ["isSet"] = true
                });
            }
        }
        dict["entries"] = entries;
        dict["revision"] = revision + 1;
        dict["updatedAt"] = DateTime.UtcNow.ToString("o");
        dict["menuEnabled"] = false;
        PutSettingUnlocked(db, "cheats", JsonSerializer.Serialize(dict));
    }

    /// <summary>
    /// Injector mirror: update catalog only. Never overwrite entries (web/server is SoT for cheat values).
    /// </summary>
    public void MergeCheatsCatalog(JsonElement mirrorBody)
    {
        if (!mirrorBody.TryGetProperty("catalog", out var catalog) ||
            catalog.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return;
        using var db = Open();
        var raw = GetSettingUnlocked(db, "cheats") ?? """{"menuEnabled":false,"revision":0,"entries":[]}""";
        using var doc = JsonDocument.Parse(raw);
        var dict = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.NameEquals("catalog")) continue;
            dict[prop.Name] = prop.Value.Clone();
        }
        dict["catalog"] = catalog.Clone();
        PutSettingUnlocked(db, "cheats", JsonSerializer.Serialize(dict));
    }

    static string MigrateCheatsJson(string json, out bool changed)
    {
        changed = false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var dict = new Dictionary<string, object?>();
            long revision = 0;
            var hadRevision = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals("entries")) continue;
                // Codec output is response-only; never persist into SSOT.
                if (prop.NameEquals("mods")) continue;
                if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r))
                {
                    revision = r;
                    hadRevision = true;
                    continue;
                }
                dict[prop.Name] = prop.Value.Clone();
            }
            var list = new List<Dictionary<string, object?>>();
            if (root.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var eId = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var enabled = item.TryGetProperty("enabled", out var en) && en.GetBoolean();
                    var fv = item.TryGetProperty("floatValue", out var fvel) && fvel.TryGetDouble(out var d) ? d : 0d;
                    var kind = item.TryGetProperty("kind", out var k) ? k.GetString() : null;
                    if (CheatSchema.ShouldStripFromDocument(eId, enabled, fv, kind))
                    {
                        changed = true;
                        continue;
                    }
                    list.Add(new Dictionary<string, object?>
                    {
                        ["id"] = eId,
                        ["kind"] = kind ?? "toggle",
                        ["enabled"] = enabled,
                        ["floatValue"] = fv,
                        ["isSet"] = true
                    });
                }
            }
            if (!hadRevision)
            {
                changed = true;
                revision = Math.Max(revision, 0);
            }
            if (changed) revision++;
            dict["entries"] = list;
            dict["revision"] = revision;
            dict["menuEnabled"] = false;
            if (!dict.ContainsKey("updatedAt") || changed)
                dict["updatedAt"] = DateTime.UtcNow.ToString("o");
            return JsonSerializer.Serialize(dict);
        }
        catch
        {
            return json;
        }
    }

    static string BumpRevisionJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, object?>();
            long revision = 0;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r))
                {
                    revision = r;
                    continue;
                }
                dict[prop.Name] = prop.Value.Clone();
            }
            dict["revision"] = revision + 1;
            dict["updatedAt"] = DateTime.UtcNow.ToString("o");
            dict["menuEnabled"] = false;
            return JsonSerializer.Serialize(dict);
        }
        catch
        {
            return json;
        }
    }

    public long InsertEvent(EventEnvelope e)
    {
        InsertEvents(new[] { e });
        return e.Id ?? 0;
    }

    /// <summary>Persists batch. Returns hub notify sets for Activity + RpgProgression.</summary>
    public EventInsertNotify InsertEvents(IReadOnlyList<EventEnvelope> batch)
    {
        if (batch.Count == 0)
            return new EventInsertNotify(Array.Empty<long>(), Array.Empty<RpgProgressionDirty>(), Array.Empty<long>());
        lock (_gate)
        {
            _activityNotifyBatch = new HashSet<long>();
            _progressionNotifyBatch = new List<RpgProgressionDirty>();
            _closedRunNotifyBatch = new HashSet<long>();
            try
            {
                using var db = OpenUnlocked();
                Exec(db, "BEGIN IMMEDIATE;");
                try
                {
                    foreach (var e in batch)
                        InsertOneUnlocked(db, e);
                    Exec(db, "COMMIT;");
                }
                catch
                {
                    try { Exec(db, "ROLLBACK;"); } catch { /* ignore */ }
                    _killEarnMemo.Clear(); // in-memory memo must not outrun the rolled-back ledger
                    throw;
                }
                return new EventInsertNotify(
                    _activityNotifyBatch.ToList(),
                    _progressionNotifyBatch.ToList(),
                    _closedRunNotifyBatch.ToList());
            }
            finally
            {
                _activityNotifyBatch = null;
                _progressionNotifyBatch = null;
                _closedRunNotifyBatch = null;
            }
        }
    }

    void InsertOneUnlocked(SqliteConnection db, EventEnvelope e, long? explicitPlayerId = null)
    {
        var payload = e.Payload is null ? "{}" : JsonSerializer.Serialize(e.Payload, Json);
        var t = string.IsNullOrWhiteSpace(e.T) ? DateTime.UtcNow.ToString("o") : e.T;
        var matchKey = string.IsNullOrWhiteSpace(e.MatchKey) ? null : e.MatchKey.Trim();
        long playerId;
        long? runId;

        if (e.Kind == "board.start")
        {
            matchKey ??= Guid.NewGuid().ToString();
            // Explicit player (web ingest): never stamp current_player_id on a web run — a mid-
            // resolution player switch would mis-credit the save (audit precondition 4).
            playerId = explicitPlayerId ?? GetCurrentPlayerIdUnlocked(db);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO runs(player_id, match_key, started_utc, level_name, level_type, board_level, summary, modifiers_json, game)
                    VALUES($p,$k,$t,$n,$lt,$bl,'{}',$mod,$g);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$g",
                    string.IsNullOrWhiteSpace(e.Game) ? RpgConstants.GameId : e.Game);
                cmd.Parameters.AddWithValue("$p", playerId);
                cmd.Parameters.AddWithValue("$k", matchKey);
                cmd.Parameters.AddWithValue("$t", t);
                cmd.Parameters.AddWithValue("$n", (object?)TryString(payload, "levelName") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$lt", (object?)TryString(payload, "levelType") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$bl", Db(TryInt(payload, "boardLevel")));
                cmd.Parameters.AddWithValue("$mod", (object?)TryObjectJson(payload, "modifiers") ?? DBNull.Value);
                runId = (long)(cmd.ExecuteScalar() ?? 0L);
            }
        }
        else
        {
            runId = FindRunId(db, matchKey);
            if (runId is { } rid)
                playerId = GetRunPlayerId(db, rid) ?? explicitPlayerId ?? GetCurrentPlayerIdUnlocked(db);
            else
                playerId = explicitPlayerId ?? GetCurrentPlayerIdUnlocked(db);
        }

        long id;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO events(player_id, run_id, match_key, t, game, kind, payload)
                VALUES($p,$r,$m,$t,$g,$k,$payload);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$p", playerId);
            cmd.Parameters.AddWithValue("$r", (object?)runId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$m", (object?)matchKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$t", t);
            cmd.Parameters.AddWithValue("$g", string.IsNullOrEmpty(e.Game) ? RpgConstants.GameId : e.Game);
            cmd.Parameters.AddWithValue("$k", e.Kind);
            cmd.Parameters.AddWithValue("$payload", payload);
            id = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        // Pollution guards (audit 2026-08-21): web-mode events must not touch the pvzrh
        // catalog, global metrics, or almanac type XP — runs/facts/souls still project.
        var pvzGame = IsPvzGame(e.Game);
        if (runId is { } run)
            Project(db, e.Kind, payload, t, playerId, run, matchKey, pvzGame);
        else
            ProjectGlobal(db, e.Kind, payload, t);

        if (pvzGame)
            BumpFromKindUnlocked(db, e.Kind);
        e.Id = id;
        e.PlayerId = playerId;
        e.RunId = runId;
        e.MatchKey = matchKey;
    }

    public long CountEvents()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM events;";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    /// <summary>The current tip of the event log — for an in-process caller (e.g. a debug-orchestration
    /// endpoint) that needs to remember "everything after this point" before triggering new events, the
    /// same role the live-test scripts' own binary-search-over-HTTP `Get-MaxEventId`/`max_event_id`
    /// approximates externally. In-process, a direct query is simply correct instead of a workaround.
    /// Returns 0 when the log is empty (matches `ListEvents(limit, afterId: 0)`'s own "from the start"
    /// convention).</summary>
    public long GetMaxEventId()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(id), 0) FROM events;";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    public Dictionary<string, long> CountByKind()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT kind, COUNT(*) FROM events GROUP BY kind;";
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            map[r.GetString(0)] = r.GetInt64(1);
        return map;
    }

    public List<object> ListEntities(long? playerId = null)
    {
        using var db = Open();
        var pid = playerId ?? GetCurrentPlayerIdUnlocked(db);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_id, run_id, ptr, side, type, type_name, hp_base, hp, died_utc
            FROM entities WHERE player_id=$p ORDER BY id ASC LIMIT 200;
            """;
        cmd.Parameters.AddWithValue("$p", pid);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new
            {
                id = r.GetInt64(0),
                playerId = r.GetInt64(1),
                runId = r.GetInt64(2),
                ptr = r.GetString(3),
                side = r.GetString(4),
                type = r.GetInt32(5),
                typeName = r.IsDBNull(6) ? null : r.GetString(6),
                hpBase = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),
                hp = r.IsDBNull(8) ? (int?)null : r.GetInt32(8),
                diedUtc = r.IsDBNull(9) ? null : r.GetString(9)
            });
        }
        return list;
    }

    public List<object> ListMowers(long? playerId = null)
    {
        using var db = Open();
        var pid = playerId ?? GetCurrentPlayerIdUnlocked(db);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_id, run_id, ptr, type, type_name, row, placed_utc, started_utc, died_utc
            FROM mowers WHERE player_id=$p ORDER BY id ASC LIMIT 200;
            """;
        cmd.Parameters.AddWithValue("$p", pid);
        var list = new List<object>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new
            {
                id = r.GetInt64(0),
                playerId = r.GetInt64(1),
                runId = r.GetInt64(2),
                ptr = r.GetString(3),
                type = r.GetInt32(4),
                typeName = r.IsDBNull(5) ? null : r.GetString(5),
                row = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                placedUtc = r.IsDBNull(7) ? null : r.GetString(7),
                startedUtc = r.IsDBNull(8) ? null : r.GetString(8),
                diedUtc = r.IsDBNull(9) ? null : r.GetString(9)
            });
        }
        return list;
    }

    public List<TypeItem> ListTypes(string? side = null)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT game, side, type, type_name, display_name, sample_json, hp_base, max_hp_base, attack_base, armor_base, armor_max_base,
                   seen_count, killed_count, first_seen_utc, last_seen_utc
            FROM types
            WHERE ($side IS NULL OR side=$side)
            ORDER BY side, type;
            """;
        cmd.Parameters.AddWithValue("$side", (object?)side ?? DBNull.Value);
        var list = new List<TypeItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new TypeItem
            {
                Game = r.GetString(0),
                Side = r.GetString(1),
                Type = r.GetInt32(2),
                TypeName = r.IsDBNull(3) ? null : r.GetString(3),
                DisplayName = r.IsDBNull(4) ? null : r.GetString(4),
                SampleJson = r.IsDBNull(5) ? null : r.GetString(5),
                HpBase = r.IsDBNull(6) ? null : r.GetInt32(6),
                MaxHpBase = r.IsDBNull(7) ? null : r.GetInt32(7),
                AttackBase = r.IsDBNull(8) ? null : r.GetInt32(8),
                ArmorBase = r.IsDBNull(9) ? null : r.GetInt32(9),
                ArmorMaxBase = r.IsDBNull(10) ? null : r.GetInt32(10),
                SeenCount = r.GetInt32(11),
                KilledCount = r.GetInt32(12),
                FirstSeenUtc = r.IsDBNull(13) ? null : r.GetString(13),
                LastSeenUtc = r.IsDBNull(14) ? null : r.GetString(14)
            });
        }
        return list;
    }

    public List<EventEnvelope> ListEvents(int limit, long afterId, long? playerId = null)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = playerId is null
            ? "SELECT id, t, game, kind, payload, match_key, player_id, run_id FROM events WHERE id > $a ORDER BY id ASC LIMIT $l;"
            : "SELECT id, t, game, kind, payload, match_key, player_id, run_id FROM events WHERE id > $a AND player_id = $p ORDER BY id ASC LIMIT $l;";
        cmd.Parameters.AddWithValue("$a", afterId);
        cmd.Parameters.AddWithValue("$l", Math.Clamp(limit, 1, 500));
        if (playerId is { } pid)
            cmd.Parameters.AddWithValue("$p", pid);
        var list = new List<EventEnvelope>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            object? payload = null;
            try { payload = JsonSerializer.Deserialize<JsonElement>(r.GetString(4)); }
            catch { payload = r.GetString(4); }
            list.Add(new EventEnvelope
            {
                Id = r.GetInt64(0),
                T = r.GetString(1),
                Game = r.GetString(2),
                Kind = r.GetString(3),
                Payload = payload,
                MatchKey = r.IsDBNull(5) ? null : r.GetString(5),
                PlayerId = r.IsDBNull(6) ? null : r.GetInt64(6),
                RunId = r.IsDBNull(7) ? null : r.GetInt64(7)
            });
        }
        return list;
    }

    public List<RunItem> ListRuns(long? playerId = null)
    {
        using var db = Open();
        var pid = playerId ?? GetCurrentPlayerIdUnlocked(db);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_id, match_key, started_utc, ended_utc, level_name, result,
                   mowers_used, plants_planted, plants_died, zombies_killed, summary,
                   level_type, board_level, modifiers_json, snapshot_json, archive_uri, game
            FROM runs WHERE player_id = $p ORDER BY id DESC LIMIT 100;
            """;
        cmd.Parameters.AddWithValue("$p", pid);
        var list = new List<RunItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            object? summary = null;
            if (!r.IsDBNull(11))
            {
                try { summary = JsonSerializer.Deserialize<JsonElement>(r.GetString(11)); }
                catch { summary = r.GetString(11); }
            }
            object? modifiers = null;
            if (!r.IsDBNull(14))
            {
                try { modifiers = JsonSerializer.Deserialize<JsonElement>(r.GetString(14)); }
                catch { modifiers = r.GetString(14); }
            }
            object? snapshot = null;
            if (!r.IsDBNull(15))
            {
                try { snapshot = JsonSerializer.Deserialize<JsonElement>(r.GetString(15)); }
                catch { snapshot = r.GetString(15); }
            }
            list.Add(new RunItem
            {
                Id = r.GetInt64(0),
                PlayerId = r.GetInt64(1),
                MatchKey = r.IsDBNull(2) ? null : r.GetString(2),
                StartedUtc = r.GetString(3),
                EndedUtc = r.IsDBNull(4) ? null : r.GetString(4),
                LevelName = r.IsDBNull(5) ? null : r.GetString(5),
                Result = r.IsDBNull(6) ? null : r.GetString(6),
                MowersUsed = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                PlantsPlanted = r.IsDBNull(8) ? 0 : r.GetInt32(8),
                PlantsDied = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                ZombiesKilled = r.IsDBNull(10) ? 0 : r.GetInt32(10),
                Summary = snapshot ?? summary,
                LevelType = r.IsDBNull(12) ? null : r.GetString(12),
                BoardLevel = r.IsDBNull(13) ? null : r.GetInt32(13),
                Modifiers = modifiers,
                ArchiveUri = r.IsDBNull(16) ? null : r.GetString(16),
                Game = r.IsDBNull(17) ? RpgConstants.GameId : r.GetString(17)
            });
        }
        return list;
    }

    public List<MetricItem> ListMetrics()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT name, value, ts FROM metrics ORDER BY name;";
        var list = new List<MetricItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MetricItem
            {
                Name = r.GetString(0),
                Value = r.GetDouble(1),
                Ts = r.GetString(2)
            });
        }
        return list;
    }

    public void UpsertMetric(string name, double value)
    {
        using var db = Open();
        UpsertMetricUnlocked(db, name, value);
    }

    public void AddMetric(string name, double delta)
    {
        using var db = Open();
        AddMetricUnlocked(db, name, delta);
    }

    static bool IsPvzGame(string? game) => RpgConstants.IsPvzGame(game);

    void Project(SqliteConnection db, string kind, string payload, string t, long playerId, long runId, string? matchKey, bool pvzGame = true)
    {
        switch (kind)
        {
            case "plant.place":
                ExecParam(db, "UPDATE runs SET plants_planted = plants_planted + 1 WHERE id=$id;", "$id", runId);
                break;
            case "plant.spawn":
                InsertEntity(db, payload, t, playerId, runId, "plant");
                InsertSpawnStats(db, payload, t, playerId, runId, "plant");
                if (pvzGame) UpsertTypeFromSpawn(db, payload, t, "plant");
                break;
            case "zombie.spawn":
                InsertEntity(db, payload, t, playerId, runId, "zombie");
                InsertSpawnStats(db, payload, t, playerId, runId, "zombie");
                if (pvzGame) UpsertTypeFromSpawn(db, payload, t, "zombie");
                break;
            case "entity.stats":
                InsertSpawnStats(db, payload, t, playerId, runId, TryString(payload, "side") ?? "zombie");
                UpdateEntityLatest(db, payload, runId);
                break;
            case "plant.die":
                MarkDied(db, payload, t, runId, "reason");
                if (pvzGame) BumpTypeKilled(db, payload, t, "plant");
                ExecParam(db, "UPDATE runs SET plants_died = plants_died + 1 WHERE id=$id;", "$id", runId);
                break;
            case "zombie.die":
                MarkDied(db, payload, t, runId, "reason");
                if (pvzGame) BumpTypeKilled(db, payload, t, "zombie");
                ExecParam(db, "UPDATE runs SET zombies_killed = zombies_killed + 1 WHERE id=$id;", "$id", runId);
                break;
            case "mower.place":
                InsertMower(db, payload, t, playerId, runId);
                break;
            case "mower.start":
                UpdateMower(db, payload, runId, "started_utc", t);
                ExecParam(db, "UPDATE runs SET mowers_used = mowers_used + 1 WHERE id=$id;", "$id", runId);
                break;
            case "mower.die":
                UpdateMower(db, payload, runId, "died_utc", t);
                break;
            case "match.result":
                {
                    var result = NormalizeResult(TryString(payload, "result"));
                    if (result is null) break;
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "UPDATE runs SET result=$r WHERE id=$id AND (result IS NULL OR result='');";
                    cmd.Parameters.AddWithValue("$r", result);
                    cmd.Parameters.AddWithValue("$id", runId);
                    cmd.ExecuteNonQuery();
                    break;
                }
            case "board.snapshot":
            case "board.end":
                ApplySnapshot(db, payload, t, runId, kind == "board.end");
                break;
            case "wave.change":
                {
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "UPDATE runs SET wave=COALESCE($w, wave), max_wave=COALESCE($m, max_wave) WHERE id=$id;";
                    cmd.Parameters.AddWithValue("$w", (object?)TryInt(payload, "wave") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$m", (object?)TryInt(payload, "maxWave") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$id", runId);
                    cmd.ExecuteNonQuery();
                    break;
                }
            case "catalog.types":
                if (pvzGame) ProjectCatalog(db, payload, t);
                break;
            case "catalog.recipes":
                if (pvzGame) ProjectRecipes(db, payload);
                else Console.WriteLine("[recipes] catalog.recipes event dropped: pvzGame=false");
                break;
            case "pet.spawn":
                if (pvzGame) UpsertTypeFromSpawn(db, payload, t, "pet");
                break;
            case "grid.place":
                if (pvzGame) UpsertTypeFromSpawn(db, payload, t, "grid");
                break;
            case "level.name":
                {
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "UPDATE runs SET level_name=COALESCE($n, level_name) WHERE id=$id;";
                    cmd.Parameters.AddWithValue("$n", Db(TryString(payload, "levelName")));
                    cmd.Parameters.AddWithValue("$id", runId);
                    cmd.ExecuteNonQuery();
                    break;
                }
            case "board.economy":
                {
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "UPDATE runs SET sun_final=COALESCE($sun, sun_final), wave=COALESCE($w, wave), max_wave=COALESCE($mw, max_wave) WHERE id=$id;";
                    cmd.Parameters.AddWithValue("$sun", Db(TryInt(payload, "sun") ?? TryInt(payload, "theSun")));
                    cmd.Parameters.AddWithValue("$w", Db(TryInt(payload, "wave") ?? TryInt(payload, "theWave")));
                    cmd.Parameters.AddWithValue("$mw", Db(TryInt(payload, "maxWave") ?? TryInt(payload, "theMaxWave")));
                    cmd.Parameters.AddWithValue("$id", runId);
                    cmd.ExecuteNonQuery();
                    break;
                }
        }

        ProjectPvzActivityFromCapture(db, kind, payload, t, playerId, runId, matchKey, pvzGame);
    }

    void ProjectGlobal(SqliteConnection db, string kind, string payload, string t)
    {
        if (kind == "catalog.types")
            ProjectCatalog(db, payload, t);
        else if (kind == "catalog.recipes")
            ProjectRecipes(db, payload);
    }

    void InsertSpawnStats(SqliteConnection db, string payload, string t, long playerId, long runId, string side)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO spawn_stats(player_id, run_id, ptr, side, type, source, captured_utc, stats_json)
            VALUES($p,$r,$ptr,$side,$type,$src,$t,$json);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        cmd.Parameters.AddWithValue("$side", side);
        cmd.Parameters.AddWithValue("$type", TryInt(payload, "type") ?? 0);
        cmd.Parameters.AddWithValue("$src", (object?)TryString(payload, "source") ?? "spawn");
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$json", payload);
        cmd.ExecuteNonQuery();
    }

    void UpdateEntityLatest(SqliteConnection db, string payload, long runId)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE entities SET
              hp=COALESCE($hp, hp), max_hp=COALESCE($mh, max_hp),
              attack=COALESCE($a, attack), armor=COALESCE($ar, armor), payload=$payload
            WHERE run_id=$r AND ptr=$ptr;
            """;
        cmd.Parameters.AddWithValue("$hp", Db(TryInt(payload, "hp") ?? TryInt(payload, "theHealth") ?? TryInt(payload, "thePlantHealth")));
        cmd.Parameters.AddWithValue("$mh", Db(TryInt(payload, "maxHp") ?? TryInt(payload, "theMaxHealth") ?? TryInt(payload, "thePlantMaxHealth")));
        cmd.Parameters.AddWithValue("$a", Db(TryInt(payload, "attack") ?? TryInt(payload, "theAttackDamage") ?? TryInt(payload, "attackDamage")));
        cmd.Parameters.AddWithValue("$ar", Db(TryInt(payload, "armor") ?? TryInt(payload, "theFirstArmorHealth")));
        cmd.Parameters.AddWithValue("$payload", payload);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        cmd.ExecuteNonQuery();
    }

    void ProjectRecipes(SqliteConnection db, string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("[recipes] ProjectRecipes: payload has no 'entries' array — dropped: " +
                    (payload.Length > 200 ? payload[..200] : payload));
                return;
            }
            var written = 0;
            foreach (var item in entries.EnumerateArray())
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO recipes(game, parent_a, parent_b, result, parent_a_name, parent_b_name, result_name)
                    VALUES($g,$a,$b,$r,$an,$bn,$rn)
                    ON CONFLICT(game, parent_a, parent_b, result) DO UPDATE SET
                      parent_a_name=COALESCE(excluded.parent_a_name, recipes.parent_a_name),
                      parent_b_name=COALESCE(excluded.parent_b_name, recipes.parent_b_name),
                      result_name=COALESCE(excluded.result_name, recipes.result_name);
                    """;
                cmd.Parameters.AddWithValue("$g", RpgConstants.GameId);
                cmd.Parameters.AddWithValue("$a", JsonInt(item, "parentA") ?? 0);
                cmd.Parameters.AddWithValue("$b", JsonInt(item, "parentB") ?? 0);
                cmd.Parameters.AddWithValue("$r", JsonInt(item, "result") ?? 0);
                cmd.Parameters.AddWithValue("$an", (object?)JsonStr(item, "parentAName") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$bn", (object?)JsonStr(item, "parentBName") ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$rn", (object?)JsonStr(item, "resultName") ?? DBNull.Value);
                cmd.ExecuteNonQuery();
                written++;
            }
            Console.WriteLine($"[recipes] ProjectRecipes: wrote {written} rows this batch");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[recipes] ProjectRecipes: malformed payload — " + ex.Message);
        }
    }

    public List<RecipeItem> ListRecipes()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT parent_a, parent_a_name, parent_b, parent_b_name, result, result_name
            FROM recipes ORDER BY parent_a, parent_b, result LIMIT 5000;
            """;
        var list = new List<RecipeItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new RecipeItem
            {
                ParentA = r.GetInt32(0),
                ParentAName = r.IsDBNull(1) ? null : r.GetString(1),
                ParentB = r.GetInt32(2),
                ParentBName = r.IsDBNull(3) ? null : r.GetString(3),
                Result = r.GetInt32(4),
                ResultName = r.IsDBNull(5) ? null : r.GetString(5)
            });
        }
        return list;
    }

    public List<SpawnStatItem> ListSpawnStats(long runId)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, run_id, ptr, side, type, source, captured_utc, stats_json
            FROM spawn_stats WHERE run_id=$r ORDER BY id ASC LIMIT 5000;
            """;
        cmd.Parameters.AddWithValue("$r", runId);
        var list = new List<SpawnStatItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            object? stats = null;
            try { stats = JsonSerializer.Deserialize<JsonElement>(r.GetString(7)); }
            catch { stats = r.GetString(7); }
            list.Add(new SpawnStatItem
            {
                Id = r.GetInt64(0),
                RunId = r.GetInt64(1),
                Ptr = r.GetString(2),
                Side = r.GetString(3),
                Type = r.GetInt32(4),
                Source = r.GetString(5),
                CapturedUtc = r.GetString(6),
                Stats = stats
            });
        }
        return list;
    }

    public List<SpawnStatItem> ListSpawnStatsForPlayer(long? playerId = null)
    {
        using var db = Open();
        var pid = playerId ?? GetCurrentPlayerIdUnlocked(db);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT id, run_id, ptr, side, type, source, captured_utc, stats_json
            FROM spawn_stats WHERE player_id=$p ORDER BY id DESC LIMIT 200;
            """;
        cmd.Parameters.AddWithValue("$p", pid);
        var list = new List<SpawnStatItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            object? stats = null;
            try { stats = JsonSerializer.Deserialize<JsonElement>(r.GetString(7)); }
            catch { stats = r.GetString(7); }
            list.Add(new SpawnStatItem
            {
                Id = r.GetInt64(0),
                RunId = r.GetInt64(1),
                Ptr = r.GetString(2),
                Side = r.GetString(3),
                Type = r.GetInt32(4),
                Source = r.GetString(5),
                CapturedUtc = r.GetString(6),
                Stats = stats
            });
        }
        return list;
    }

    void InsertEntity(SqliteConnection db, string payload, string t, long playerId, long runId, string side)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entities(player_id, run_id, ptr, side, type, type_name, hp_base, hp, max_hp_base, max_hp,
              attack_base, attack, armor_base, armor, col, row, spawned_utc, payload)
            VALUES($p,$r,$ptr,$side,$type,$tn,$hpb,$hp,$mhb,$mh,$ab,$a,$arb,$ar,$col,$row,$t,$payload)
            ON CONFLICT(run_id, ptr) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        cmd.Parameters.AddWithValue("$side", side);
        cmd.Parameters.AddWithValue("$type", TryInt(payload, "type") ?? 0);
        cmd.Parameters.AddWithValue("$tn", (object?)TryString(payload, "typeName") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hpb", (object?)TryInt(payload, "hpBase") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hp", (object?)TryInt(payload, "hp") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mhb", (object?)TryInt(payload, "maxHpBase") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mh", (object?)TryInt(payload, "maxHp") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ab", (object?)TryInt(payload, "attackBase") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$a", (object?)TryInt(payload, "attack") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$arb", (object?)TryInt(payload, "armorBase") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ar", (object?)TryInt(payload, "armor") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$col", (object?)TryInt(payload, "col") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$row", (object?)TryInt(payload, "row") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$payload", payload);
        cmd.ExecuteNonQuery();
    }

    void ProjectCatalog(SqliteConnection db, string payload, string t)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                var side = TryString(payload, "side") ?? "plant";
                foreach (var item in entries.EnumerateArray())
                    UpsertTypeName(db, side, item, t);
                return;
            }
            if (root.TryGetProperty("plants", out var plants) && plants.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in plants.EnumerateArray())
                    UpsertTypeName(db, "plant", item, t);
            }
            if (root.TryGetProperty("zombies", out var zombies) && zombies.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in zombies.EnumerateArray())
                    UpsertTypeName(db, "zombie", item, t);
            }
        }
        catch { /* malformed catalog payload */ }
    }

    void UpsertTypeName(SqliteConnection db, string side, JsonElement item, string t)
    {
        var type = JsonInt(item, "type") ?? 0;
        var name = JsonStr(item, "typeName");
        var display = JsonStr(item, "displayName");
        UpsertType(db, side, type, name, display, null, null, null, null, null, null, seen: 0, killed: 0, t);
    }

    void UpsertTypeFromSpawn(SqliteConnection db, string payload, string t, string side)
    {
        var type = TryInt(payload, "type") ?? 0;
        UpsertType(db, side, type, TryString(payload, "typeName"), TryString(payload, "displayName"),
            TryInt(payload, "hpBase"), TryInt(payload, "maxHpBase"), TryInt(payload, "attackBase"),
            TryInt(payload, "armorBase"), TryInt(payload, "armorMaxBase"),
            payload, seen: 1, killed: 0, t);
    }

    void BumpTypeKilled(SqliteConnection db, string payload, string t, string side)
    {
        var type = TryInt(payload, "type") ?? 0;
        UpsertType(db, side, type, TryString(payload, "typeName"), TryString(payload, "displayName"),
            null, null, null, null, null, null, seen: 0, killed: 1, t);
    }

    /// <summary>
    /// FillEmpty: only set names when the catalog row is blank (spawn/catalog).
    /// PreferIncoming: overwrite with non-empty incoming names (almanac promote).
    /// </summary>
    enum TypeNameMode { FillEmpty, PreferIncoming }

    void UpsertType(SqliteConnection db, string side, int type, string? typeName, string? displayName,
        int? hpBase, int? maxHpBase, int? attackBase, int? armorBase, int? armorMaxBase,
        string? sampleJson, int seen, int killed, string t, TypeNameMode nameMode = TypeNameMode.FillEmpty)
    {
        var typeNameSql = nameMode == TypeNameMode.PreferIncoming
            ? """
              type_name=CASE WHEN excluded.type_name IS NOT NULL AND excluded.type_name != ''
                THEN excluded.type_name ELSE types.type_name END,
              display_name=CASE WHEN excluded.display_name IS NOT NULL AND excluded.display_name != ''
                THEN excluded.display_name ELSE types.display_name END,
              """
            : """
              type_name=CASE WHEN (types.type_name IS NULL OR types.type_name = '')
                AND excluded.type_name IS NOT NULL AND excluded.type_name != ''
                THEN excluded.type_name ELSE types.type_name END,
              display_name=CASE WHEN (types.display_name IS NULL OR types.display_name = '')
                AND excluded.display_name IS NOT NULL AND excluded.display_name != ''
                THEN excluded.display_name ELSE types.display_name END,
              """;
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO types(game, side, type, type_name, display_name, hp_base, max_hp_base, attack_base,
              armor_base, armor_max_base, sample_json, seen_count, killed_count, first_seen_utc, last_seen_utc)
            VALUES($g,$side,$type,$tn,$dn,$hpb,$mhb,$ab,$arb,$armb,$sj,$seen,$killed,$t,$t)
            ON CONFLICT(game, side, type) DO UPDATE SET
              {typeNameSql}
              hp_base=COALESCE(types.hp_base, excluded.hp_base),
              max_hp_base=COALESCE(types.max_hp_base, excluded.max_hp_base),
              attack_base=COALESCE(types.attack_base, excluded.attack_base),
              armor_base=COALESCE(types.armor_base, excluded.armor_base),
              armor_max_base=COALESCE(types.armor_max_base, excluded.armor_max_base),
              sample_json=COALESCE(types.sample_json, excluded.sample_json),
              seen_count=types.seen_count + excluded.seen_count,
              killed_count=types.killed_count + excluded.killed_count,
              last_seen_utc=excluded.last_seen_utc;
            """;
        cmd.Parameters.AddWithValue("$g", RpgConstants.GameId);
        cmd.Parameters.AddWithValue("$side", side);
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$tn", (object?)typeName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$dn", (object?)displayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hpb", Db(hpBase));
        cmd.Parameters.AddWithValue("$mhb", Db(maxHpBase));
        cmd.Parameters.AddWithValue("$ab", Db(attackBase));
        cmd.Parameters.AddWithValue("$arb", Db(armorBase));
        cmd.Parameters.AddWithValue("$armb", Db(armorMaxBase));
        cmd.Parameters.AddWithValue("$sj", (object?)sampleJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$seen", seen);
        cmd.Parameters.AddWithValue("$killed", killed);
        cmd.Parameters.AddWithValue("$t", t);
        cmd.ExecuteNonQuery();
    }

    static int? JsonInt(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out n)) return n;
        return null;
    }

    static string? JsonStr(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var p)) return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    void MarkDied(SqliteConnection db, string payload, string t, long runId, string reasonKey)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE entities SET died_utc=$t, die_reason=COALESCE($reason, die_reason)
            WHERE run_id=$r AND ptr=$ptr AND died_utc IS NULL;
            """;
        cmd.Parameters.AddWithValue("$t", t);
        var reason = TryString(payload, reasonKey) ?? TryInt(payload, reasonKey)?.ToString();
        cmd.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        cmd.ExecuteNonQuery();
    }

    void InsertMower(SqliteConnection db, string payload, string t, long playerId, long runId)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mowers(player_id, run_id, ptr, type, type_name, row, placed_utc)
            VALUES($p,$r,$ptr,$type,$tn,$row,$t)
            ON CONFLICT(run_id, ptr) DO UPDATE SET
              type=excluded.type, type_name=excluded.type_name, row=excluded.row, placed_utc=excluded.placed_utc;
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        cmd.Parameters.AddWithValue("$type", TryInt(payload, "type") ?? 0);
        cmd.Parameters.AddWithValue("$tn", (object?)TryString(payload, "typeName") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$row", (object?)TryInt(payload, "row") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t", t);
        cmd.ExecuteNonQuery();
    }

    void UpdateMower(SqliteConnection db, string payload, long runId, string column, string t)
    {
        var ptr = TryString(payload, "ptr");
        if (string.IsNullOrWhiteSpace(ptr)) return;
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"UPDATE mowers SET {column}=$t WHERE run_id=$r AND ptr=$ptr;";
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$r", runId);
        cmd.Parameters.AddWithValue("$ptr", ptr);
        var n = cmd.ExecuteNonQuery();
        if (n == 0)
        {
            using var ins = db.CreateCommand();
            ins.CommandText = """
                INSERT INTO mowers(player_id, run_id, ptr, type, type_name, row, placed_utc, started_utc, died_utc)
                SELECT player_id, id, $ptr, 0, NULL, NULL, NULL,
                       CASE WHEN $col='started_utc' THEN $t END,
                       CASE WHEN $col='died_utc' THEN $t END
                FROM runs WHERE id=$r
                ON CONFLICT(run_id, ptr) DO UPDATE SET
                  started_utc=COALESCE(mowers.started_utc, excluded.started_utc),
                  died_utc=COALESCE(mowers.died_utc, excluded.died_utc);
                """;
            ins.Parameters.AddWithValue("$ptr", ptr);
            ins.Parameters.AddWithValue("$col", column);
            ins.Parameters.AddWithValue("$t", t);
            ins.Parameters.AddWithValue("$r", runId);
            ins.ExecuteNonQuery();
        }
    }

    void ApplySnapshot(SqliteConnection db, string payload, string t, long runId, bool closing)
    {
        var summary = TryObjectJson(payload, "summary") ?? payload;
        using var cmd = db.CreateCommand();
        cmd.CommandText = closing
            ? """
              UPDATE runs SET
                ended_utc = COALESCE(ended_utc, $t),
                summary = $s,
                snapshot_json = $s,
                level_name = COALESCE($n, level_name),
                duration_sec = COALESCE($d, duration_sec),
                sun_final = COALESCE($sun, sun_final),
                wave = COALESCE($w, wave),
                max_wave = COALESCE($mw, max_wave)
              WHERE id=$id;
              """
            : """
              UPDATE runs SET
                summary = $s,
                snapshot_json = $s,
                duration_sec = COALESCE($d, duration_sec),
                sun_final = COALESCE($sun, sun_final),
                wave = COALESCE($w, wave),
                max_wave = COALESCE($mw, max_wave)
              WHERE id=$id;
              """;
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$s", summary);
        cmd.Parameters.AddWithValue("$n", Db(TryString(payload, "levelName")));
        cmd.Parameters.AddWithValue("$d", Db(TryDouble(payload, "duration") ?? NestedDouble(payload, "summary", "duration")));
        cmd.Parameters.AddWithValue("$sun", Db(TryInt(payload, "sun") ?? NestedInt(payload, "summary", "sun")));
        cmd.Parameters.AddWithValue("$w", Db(TryInt(payload, "wave") ?? NestedInt(payload, "summary", "wave")));
        cmd.Parameters.AddWithValue("$mw", Db(TryInt(payload, "maxWave") ?? NestedInt(payload, "summary", "maxWave")));
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.ExecuteNonQuery();
        if (closing)
            _closedRunNotifyBatch?.Add(runId);
    }

    static object Db(string? v) => (object?)v ?? DBNull.Value;
    static object Db(int? v) => v.HasValue ? v.Value : DBNull.Value;
    static object Db(double? v) => v.HasValue ? v.Value : DBNull.Value;

    static string? NormalizeResult(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "victory" or "win" => "victory",
            "defeat" or "lose" or "loss" => "defeat",
            "surrender" => "surrender",
            "timeout" => "timeout",
            "none" => null,
            _ => s
        };
    }

    long? FindRunId(SqliteConnection db, string? matchKey)
    {
        if (!string.IsNullOrWhiteSpace(matchKey))
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id FROM runs WHERE match_key=$k ORDER BY id DESC LIMIT 1;";
            cmd.Parameters.AddWithValue("$k", matchKey);
            var v = cmd.ExecuteScalar();
            if (v is long l) return l;
            if (v != null && v is not DBNull) return Convert.ToInt64(v);
        }
        return null;
    }

    static long? GetRunPlayerId(SqliteConnection db, long runId)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT player_id FROM runs WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", runId);
        var v = cmd.ExecuteScalar();
        if (v is long l) return l;
        if (v != null && v is not DBNull) return Convert.ToInt64(v);
        return null;
    }

    void BumpFromKindUnlocked(SqliteConnection db, string kind)
    {
        var name = kind switch
        {
            "plant.spawn" => "plants_spawned",
            "plant.die" => "plants_died",
            "zombie.spawn" => "zombies_spawned",
            "zombie.die" => "zombies_killed",
            "bullet.init" => "bullets_spawned",
            "mower.start" => "mowers_used",
            "board.start" => "runs_started",
            "board.end" => "runs_ended",
            _ => null
        };
        if (name != null) AddMetricUnlocked(db, name, 1);
    }

    static int? TryInt(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out n)) return n;
        }
        catch { }
        return null;
    }

    static double? TryDouble(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n)) return n;
        }
        catch { }
        return null;
    }

    static string? TryString(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var p)) return null;
            return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
        }
        catch { }
        return null;
    }

    static int? NestedInt(string json, string obj, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object) return null;
            if (!o.TryGetProperty(prop, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n)) return n;
        }
        catch { }
        return null;
    }

    static double? NestedDouble(string json, string obj, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object) return null;
            if (!o.TryGetProperty(prop, out var p)) return null;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n)) return n;
        }
        catch { }
        return null;
    }

    static string? TryObjectJson(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var p)) return null;
            return p.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? p.GetRawText() : null;
        }
        catch { }
        return null;
    }

    static PlayerDto ReadPlayer(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Name = r.GetString(1),
        CreatedUtc = r.GetString(2),
        WorldSeed = r.GetInt64(3),
    };

    static PlayerDto? GetPlayerUnlocked(SqliteConnection db, long id)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_utc, world_seed FROM players WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadPlayer(r) : null;
    }

    static long GetCurrentPlayerIdUnlocked(SqliteConnection db)
    {
        var raw = GetSettingUnlocked(db, "current_player_id");
        if (long.TryParse(raw, out var id) && GetPlayerUnlocked(db, id) is not null)
            return id;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT id FROM players ORDER BY id LIMIT 1;";
        var v = cmd.ExecuteScalar();
        return v is long l ? l : Convert.ToInt64(v ?? 1L);
    }

    void SeedPlayerIfEmpty(SqliteConnection db)
    {
        using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM players;";
        var n = Convert.ToInt64(count.ExecuteScalar() ?? 0L);
        if (n == 0)
        {
            using var ins = db.CreateCommand();
            ins.CommandText = "INSERT INTO players(id, name, created_utc) VALUES(1,'Player 1',$t);";
            ins.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            ins.ExecuteNonQuery();
        }
        if (GetSettingUnlocked(db, "current_player_id") is null)
            PutSettingUnlocked(db, "current_player_id", "1");
    }

    /// <summary>Assigns a real, distinct world seed to every player row still at the 0 sentinel — a
    /// legacy row from before this column existed, or one <see cref="SeedPlayerIfEmpty"/> just
    /// inserted directly (bypassing <see cref="CreatePlayer"/>'s own seed generation). Never touches
    /// a player that already has one, matching Q5's "existing rolls frozen forever" rule one layer up
    /// — a seed change here would silently re-roll everything downstream that already derived from it.</summary>
    static void BackfillWorldSeedsUnlocked(SqliteConnection db)
    {
        var pending = new List<long>();
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT id FROM players WHERE world_seed = 0;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) pending.Add(r.GetInt64(0));
        }

        foreach (var id in pending)
        {
            // 0 is excluded from the range (never re-produce the sentinel) and NextInt64's own upper
            // bound is exclusive, so this draws from [1, long.MaxValue) — a real, never-repeating
            // 63-bit-ish space, plenty for "the whole save"'s own identity.
            var seed = System.Random.Shared.NextInt64(1, long.MaxValue);
            using var upd = db.CreateCommand();
            upd.CommandText = "UPDATE players SET world_seed = $s WHERE id = $id;";
            upd.Parameters.AddWithValue("$s", seed);
            upd.Parameters.AddWithValue("$id", id);
            upd.ExecuteNonQuery();
        }
    }

    static string? GetSettingUnlocked(SqliteConnection db, string key)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT json FROM settings WHERE key=$k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    static void PutSettingUnlocked(SqliteConnection db, string key, string json)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings(key, json, updated_utc) VALUES($k,$j,$t)
            ON CONFLICT(key) DO UPDATE SET json=$j, updated_utc=$t;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$j", json);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    void PutStatsUnlocked(SqliteConnection db, StatsConfig stats) =>
        PutSettingUnlocked(db, "stats", JsonSerializer.Serialize(stats, Json));

    static void SeedMetricIfMissingUnlocked(SqliteConnection db, string name)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO metrics(name, value, ts) VALUES($n, 0, $t)
            ON CONFLICT(name) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    static void UpsertMetricUnlocked(SqliteConnection db, string name, double value)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO metrics(name, value, ts) VALUES($n,$v,$t)
            ON CONFLICT(name) DO UPDATE SET value=$v, ts=$t;
            """;
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    static void AddMetricUnlocked(SqliteConnection db, string name, double delta)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO metrics(name, value, ts) VALUES($n,$v,$t)
            ON CONFLICT(name) DO UPDATE SET value = value + $v, ts=$t;
            """;
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$v", delta);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    static void ExecParam(SqliteConnection db, string sql, string name, object value)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    static void EnsureColumn(SqliteConnection db, string table, string column, string def)
    {
        try { Exec(db, $"ALTER TABLE {table} ADD COLUMN {column} {def};"); }
        catch { /* already exists */ }
    }

    private string? GetSetting(string key)
    {
        using var db = Open();
        return GetSettingUnlocked(db, key);
    }

    private SqliteConnection Open()
    {
        lock (_gate)
        {
            return OpenUnlocked();
        }
    }

    private SqliteConnection OpenMedia()
    {
        lock (_gate)
        {
            return OpenMediaUnlocked();
        }
    }

    private SqliteConnection OpenUnlocked() => SqliteConnectionFactory.Open(_hotPath);

    private SqliteConnection OpenMediaUnlocked() => SqliteConnectionFactory.Open(_mediaPath);

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
