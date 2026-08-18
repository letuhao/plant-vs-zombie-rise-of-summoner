# Server spec (v1)

ASP.NET Core (net8, fallback net6). Listens `http://127.0.0.1:5088` by default, or `FUSIONRPG_URLS` when set by the launcher.

Serves the Vite static build from `wwwroot` (same process). SPA fallback to `index.html` for non-API routes. Player zip is a **self-contained** `win-x64` publish under `dist/FusionRpg/Server/` so users do not install a .NET SDK. Set `FUSIONRPG_NO_BROWSER=1` to skip opening a browser tab (launcher opens the real URL).

No Unity / BepInEx references. Project `FusionRpg.Server` references `FusionRpg.Contracts` and `FusionRpg.Core`.

When `FUSIONRPG_SIM=1`, also map `/api/sim/*` and `/api/test/*` (see [testing/probes.md](../testing/probes.md)). Player zip does not set this flag.

## Startup

1. Ensure `{exe}/data/` exists; open `rpg-hot.sqlite` + `rpg-media.sqlite` (migrate legacy `rpg.sqlite` if needed); apply [schema](../database/schema.md).
2. Seed `Player 1` and `settings.current_player_id` if missing. Insert default `settings.stats` if missing (`logDamage: true`).
3. Map REST from [protocol/rest.md](../protocol/rest.md) including player create/list/select.
4. Map SignalR hub `/hub/rpg` from [protocol/signalr.md](../protocol/signalr.md).
5. CORS for Vite.

## Persistence

- PUT stats → `settings`
- Events → in-process Channel, then writer `InsertEvents` in one transaction (500–1000). Stamp `player_id` / `run_id` / `match_key`. Broadcast `EventBatch` of non-noisy kinds.
- `board.start` → insert `runs` with **current** `player_id` + `matchKey`
- Child kinds project `entities` / `spawn_stats` / `mowers` / `types` / `recipes` / run rollups from `InsertEvent`
- `catalog.types` upserts names + `display_name` even without an open run
- `catalog.recipes` upserts `recipes`
- `plant.place` increments `runs.plants_planted`
- `plant.spawn` / `zombie.spawn` / `entity.stats` insert `spawn_stats` (append)
- First spawn fills `types.sample_json` if null; never overwrites
- `match.result` → `runs.result` (ignore duplicates)
- `board.end` → `ended_utc`; `snapshot_json` / `summary`
- Metrics upsert (global)

Keep last heartbeat in memory for `/health`. Expose `ingestQueued` and `lastFlushMs`.

Hub `Event` / `Events` enqueue only. Do not open SQLite on the request thread.

## Commands

`POST /api/commands/reload-stats` and PUT `/api/stats` both send `StatsUpdated` to group `injector`.

## Failure modes

- Game closed: web still edits stats.
- Injector missing: `injectorConnected` false; events from HTTP still accepted.
- Corrupt JSON in settings: log and re-seed defaults.

## Logging

ASP.NET console log. Do not write a second event file; SQLite is the log.
