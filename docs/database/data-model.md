# Data model (play-scene ingest)

Capture only for combat. Player XP lives in RpgProgression (see [rpg-progression.md](../architecture/rpg-progression.md)), not on `players` columns.

**Capture SSOT** is the dump JSON: `events.payload` and `spawn_stats.stats_json` (same keys). `types` is catalog/baseline only — **not** combat truth.

```text
types        catalog + first-seen sample_json     NOT SSOT
runs         match + modifiers_json + snapshot_json
  ├── events       append-only envelopes          capture log
  ├── spawn_stats  append-only full dumps         combat SSOT
  ├── entities     one row per ptr                identity / death
  ├── mowers       one row per ptr                identity
  └── recipes      fusion graph
```

| Table | Role |
|---|---|
| `players` | Save slot. `id` + `name` only |
| `pvz_stat_*` | **PvzStats** — player-bound modifier SSOT + derived sheet/contributions (not RPG progression) |
| `pvz_activity_*` | **PvzActivity** — append facts + rollup cache (progression substrate; not RPG quests) |
| `rpg_actor_progression` / `rpg_xp_ledger` | **RpgProgression** — per-save actor levels + XP ledger |
| `runs` | One match. Keyed by `match_key` |
| `events` | Raw envelope. Spawn payloads **are** the full dump |
| `spawn_stats` | One row per capture (`initHealth` then `setHealthInTravel` = two rows). Never overwrite |
| `entities` | Identity + death `(run_id, ptr)`. HP columns = denormalized **latest** dump, not SSOT |
| `mowers` | Grasscutters `(run_id, ptr)` |
| `types` | Names + `display_name` + first-seen `sample_json` (write if null, never overwrite combat) |
| `recipes` | `(game, parent_a, parent_b, result)` |
| `metrics` | Global rollups |
| `settings` | `stats`, `current_player_id` |

## Foundation trust

RPG **reads** `events` and `spawn_stats.stats_json` (and `runs.modifiers_json` / `snapshot_json`). It does **not** read `types` combat columns.

`(run_id, ptr)` dies with the match. Cross-match keys: `player_id` + `type` + that run’s `spawn_stats`.

Dual write: spawn/recapture `events.payload` **is** `stats_json`. Missing hook = missing fact — do not invent HP from the catalog.

Match XP later uses `match.result` (`GameOver`), not `board.end` alone (ghost boards after win).

## Invariants

1. Every match-scoped row has `player_id` NOT NULL.
2. `board.start` creates the run. Spawns without `matchKey` drop unless a run exists for that key.
3. Second `plant.spawn` / `zombie.spawn` with same `(run_id, ptr)` does not insert a second `entities` row (`ON CONFLICT DO NOTHING`). It **does** insert another `spawn_stats` row only via `entity.stats` recapture.
4. `*.die` sets `died_utc`; does not insert a second entity.
5. Duplicate `match.result` is ignored.
6. `board.end` sets `ended_utc` if still open.
7. `runs.plants_planted` counts `plant.place`, not `plant.spawn`.
8. `types.sample_json` / first-seen `hp_base` write only when null.

## Players

Seed `Player 1`. Server stamps current player on `board.start`. Injector never sends player id. Mid-match switch keeps the open run’s player.
