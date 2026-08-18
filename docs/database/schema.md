# SQLite schema

## Live (current)

Files under `{ServerExeDir}/data/` (override dir with `FUSIONRPG_DATA`):

| File | Contents |
|---|---|
| `rpg-hot.sqlite` | Players, events, runs (incl. `archive_uri`), progression (XP buckets/watermarks), activity rollups, `archive_catalog`, … |
| `rpg-media.sqlite` | `type_icons`, `type_icon_layers`, `type_almanac_dump` |
| `archive/*` | Cold capture / Activity / XP segment SQLite files (written before hot delete) |

WAL on hot/media. SQL lives in `FusionRpg.Data`. Legacy mono `rpg.sqlite` (if present and hot missing) migrates once via `LegacyMonoMigrator` → `.pre-dal.bak`; original is **not** auto-deleted.

## Live file layout

See [ledger-snapshot.md](ledger-snapshot.md) and [persistence-refactor-blast-radius.md](persistence-refactor-blast-radius.md):

| File | Contents |
|---|---|
| `rpg-hot.sqlite` | Tables below (except media BLOBs) — **live** |
| `rpg-media.sqlite` | `type_icons`, `type_icon_layers`, `type_almanac_dump` — **live** |
| `archive/*` | Cold capture / ledger segments — **live**; user purge via Storage (W12); no auto GC |

`ALTER TABLE` adds new columns on existing files. Do not drop old `entities` hp columns this pass. See [data-model.md](data-model.md).

```sql
CREATE TABLE IF NOT EXISTS players (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  created_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
  key TEXT PRIMARY KEY,
  json TEXT NOT NULL,
  updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS runs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id INTEGER NOT NULL REFERENCES players(id),
  match_key TEXT NOT NULL UNIQUE,
  started_utc TEXT NOT NULL,
  ended_utc TEXT,
  level_name TEXT,
  level_type TEXT,
  board_level INTEGER,
  result TEXT,
  mowers_used INTEGER NOT NULL DEFAULT 0,
  plants_planted INTEGER NOT NULL DEFAULT 0,
  plants_died INTEGER NOT NULL DEFAULT 0,
  zombies_killed INTEGER NOT NULL DEFAULT 0,
  duration_sec REAL,
  sun_final INTEGER,
  wave INTEGER,
  max_wave INTEGER,
  summary TEXT,
  modifiers_json TEXT,
  snapshot_json TEXT,
  archive_uri TEXT
);

CREATE TABLE IF NOT EXISTS archive_catalog (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  uri TEXT NOT NULL UNIQUE,
  kind TEXT NOT NULL,
  run_id INTEGER,
  player_id INTEGER,
  created_utc TEXT NOT NULL,
  meta_json TEXT
);

CREATE TABLE IF NOT EXISTS events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id INTEGER NOT NULL REFERENCES players(id),
  run_id INTEGER REFERENCES runs(id),
  match_key TEXT,
  t TEXT NOT NULL,
  game TEXT NOT NULL,
  kind TEXT NOT NULL,
  payload TEXT NOT NULL
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
CREATE INDEX IF NOT EXISTS ix_spawn_stats_run_ptr ON spawn_stats(run_id, ptr);

CREATE TABLE IF NOT EXISTS entities (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id INTEGER NOT NULL REFERENCES players(id),
  run_id INTEGER NOT NULL REFERENCES runs(id),
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

CREATE TABLE IF NOT EXISTS mowers (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id INTEGER NOT NULL REFERENCES players(id),
  run_id INTEGER NOT NULL REFERENCES runs(id),
  ptr TEXT NOT NULL,
  type INTEGER NOT NULL,
  type_name TEXT,
  row INTEGER,
  placed_utc TEXT,
  started_utc TEXT,
  died_utc TEXT,
  UNIQUE(run_id, ptr)
);

CREATE TABLE IF NOT EXISTS types (
  game TEXT NOT NULL,
  side TEXT NOT NULL,
  type INTEGER NOT NULL,
  type_name TEXT,
  display_name TEXT,
  hp_base INTEGER,
  max_hp_base INTEGER,
  attack_base INTEGER,
  armor_base INTEGER,
  armor_max_base INTEGER,
  sample_json TEXT,
  seen_count INTEGER NOT NULL DEFAULT 0,
  killed_count INTEGER NOT NULL DEFAULT 0,
  first_seen_utc TEXT,
  last_seen_utc TEXT,
  PRIMARY KEY (game, side, type)
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

-- PvzStats: player-bound Xi SSOT + derived monitor cache (not RPG progression)
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
```

-- PvzActivity: append-only facts + rollup cache (not RPG quests)
```sql
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
CREATE TABLE IF NOT EXISTS pvz_activity_rollups (
  player_id INTEGER PRIMARY KEY,
  revision INTEGER NOT NULL,
  counters_json TEXT NOT NULL,
  updated_utc TEXT NOT NULL,
  through_fact_id INTEGER NOT NULL DEFAULT 0,
  schema_version INTEGER NOT NULL DEFAULT 0
);
```

-- RpgProgression: per-save actor XP (not combat power)
```sql
CREATE TABLE IF NOT EXISTS rpg_actor_progression (
  player_id INTEGER NOT NULL REFERENCES players(id),
  kind TEXT NOT NULL,
  type_id INTEGER NOT NULL,
  level INTEGER NOT NULL DEFAULT 1,
  xp REAL NOT NULL DEFAULT 0,
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
  delta REAL NOT NULL,
  reason TEXT NOT NULL,
  activity_fact_id INTEGER,
  level_before INTEGER NOT NULL,
  xp_before REAL NOT NULL,
  level_after INTEGER NOT NULL,
  xp_after REAL NOT NULL,
  demotion_before INTEGER NOT NULL,
  demotion_after INTEGER NOT NULL,
  payload_json TEXT,
  dedupe_key TEXT NOT NULL,
  UNIQUE (player_id, kind, type_id, reason, dedupe_key)
);
CREATE INDEX IF NOT EXISTS ix_rpg_xp_ledger_player ON rpg_xp_ledger(player_id, id);
```

## settings rows

| key | json |
|---|---|
| `stats` | Body of GET `/api/stats` |
| `current_player_id` | Integer id as JSON number |

Default stats: percents `1.0`, flats `0`, `logDamage: true`, `applyStats: true`.

## runs.result

`victory` / `defeat` / `surrender` / `timeout` / `unknown` (or null until `match.result`).

## Retention

No auto-delete of legacy `{ServerExeDir}/data/rpg.sqlite` (or hot/media). Ask before deleting data files.
