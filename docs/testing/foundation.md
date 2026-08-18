# Foundation test coverage

CI does **not** boot `PlantsVsZombiesRH.exe` or Harmony. A `SimEngine` in `FusionRpg.Core` uses the same stat formulas and event kinds as the injector. xUnit drives the server over REST + SignalR.

Enable sim/probes with `FUSIONRPG_SIM=1` only. Not implied by `Development`. The player zip never sets this flag.

## In CI (`dotnet test` under `tests/`)

| Area | Assert |
|---|---|
| StatMath | HP/ATK `max(1, round(base*p)+flat)`; DEF `max(0, round(dmg/p)-flat)`; `defensePercent <= 0` treated as 1 |
| Apply once | same ptr spawn twice → one `stat.applied`, HP not doubled |
| applyStats false | spawn event, HP unchanged |
| Absolute when scales off | `ApplyStats=false` + absolute Override still resolves finals (`StatSystemTests`) |
| Match lifecycle | `board.start` → plant+zombie spawn → die → `board.end` → run opened/closed; metrics bump |
| Damage log | off → no `*.damage`; on → before/after present |
| PvzStats | Seed demo; sheet hp/maxHp; withdraw; GET never bumps |
| PvzActivity | Capture projects Match/Kill/Place/Die; seed rollups; Intent `pvz.spawn.extra` inbox + ExtraSpawnFired on accept; sim/capture `source=extra`; flush before rollup assert |
| RpgProgression | Activity→XP for player/plant/zombie actors; arithmetic curve; demotion; Almanac dossier FE |
| Web → sim stats | PUT `hpPercent=2`, spawn plant 300 → 600 |
| Live pipe | SignalR `Join("web")` receives `plant.spawn` (`Event` or `EventBatch`); damage/bullet are **not** live-pushed |
| HTTP fallback | POST `/api/events` enqueues; snapshot flush persists |
| Catalog | One sim match fires implemented kinds including `plant.place`; `plantsPlanted` from place; spawn dump has more than 6 ints; `GET /api/types` names; first-seen sample only |
| Spawn stats | Two matches same zombie type different HP → two `spawn_stats` rows; `types.sample_json` unchanged by the second; recapture same ptr inserts a second `spawn_stats` row |
| Snapshot dump | `snapshot_json` / snapshot event has `sunProduced` / `totalZombieDamage` |
| Ingest stress | 2000-event POST enqueue &lt; 100ms; persist &lt; 2s; mixed 5k hits; 120fps-second 9600 persist &lt; 5s |
| Probes | reset clears events; snapshot matches; `test.probe` stored |
| Armor | zombie armor > 0 scales; armor 0 stays 0 |
| Collision | live injector heartbeat → sim POST **409** |
| matchKey | `board.start` + spawn share the same `matchKey`; entity `player_id` matches the run |
| Players | POST player, PUT current, next `board.start` uses the new id; mid-match switch keeps the open run's player |
| Mowers / result | place/start/die project `mowers`; `match.result` sets `runs.result`; `mowers_used` counts `mower.start` |
| **Cheats (CI)** | `FusionRpg.CheatCore.Tests`: registry (incl. `SYS-LIMHEALTH-GATE` default off), packs, catalog, action names. `CheatsE2ETests`: GET/PUT `/api/cheats`, toggle/set-float, **packs** `/api/cheats/packs` + probe start/end |
| **PvzStats (CI)** | `PvzStatsSheetComposerTests` (+10/−5 sheet). `PvzStatsE2ETests`: seed demo, channel detail, withdraw revision, upsert/reset |
| **PvzActivity (CI)** | `PvzActivityRollupBuilderTests` (win/lose aliases, dedupe helper). `PvzActivityE2ETests`: flush after events; plant place/die; match result; append auto-dedupe + unknown kind 400; Intent idempotent one command; Intent+sim `source=extra` |
| **RpgProgression (CI)** | `RpgXpApplyTests`, `RpgProgressionBalanceTests` (first win ≥L2), `RpgXpAwardMapTests`. `RpgProgressionE2ETests`: reason matrix, append, 404s, stats, paging (`offset` / `afterId`), `MAX(highest_level)` |
| **Single writer guard** | `tests/FusionRpg.Guard.Tests` runs `scripts/guard-single-writer.ps1` under `dotnet test` (also from `deploy-play.ps1`) — no combat field assigns outside `EntityStatWriter.cs` |
| **DAL guard** | `tests/FusionRpg.Guard.Tests` runs `scripts/guard-dal.ps1` under `dotnet test` (also from `deploy-play.ps1`) — SQLite/SQL only in `FusionRpg.Data`; deploy-play must call the script and throw on failure |
| **Secondary no-Unity guard** | `tests/FusionRpg.Guard.Tests` runs `scripts/guard-secondary-no-unity.ps1` under `dotnet test` (also from `deploy-play.ps1`) — Secondary plugins / `IEffectGrantPlugin` implementers must not reference Unity apply APIs |
| **Funnel delta guard** | `tests/FusionRpg.Guard.Tests` runs `scripts/guard-funnel-delta.ps1` — Secondary must not `TakeDamage` / `SetHp` / `Bag.Grant` |
| **Launcher Melon safety** | `FusionRpg.Launcher.Tests` / `MelonHostGapTests`: Melon uninstall owned-only; Bep wipe dedicated folder; Melon drop requires injector DLL; Melon PlayAsync happy + missing-drop message; RestartGame refuses Host-null dual-load; Melon LogPath prefers `Logs\Latest.log`; PlayerPackProbe Melon drop; `FileRpgConfig` parse defaults |

Projects: `tests/FusionRpg.Core.Tests`, `tests/FusionRpg.CheatCore.Tests`, `tests/FusionRpg.E2E.Tests`, `tests/FusionRpg.Guard.Tests`, `tests/FusionRpg.Launcher.Tests`.

Web SPA unit/coverage/e2e: [web.md](web.md) (`npm run test:coverage`, `npm run test:e2e` in `web/fusion-rpg-web`).

There is **no** GitHub Actions workflow in-repo yet; “CI” here means the automated `dotnet test` / Vitest / Playwright suite you run locally or in any pipeline that invokes those commands.

## Out of CI (real game only)

- Harmony patch load inside BepInEx **or** MelonLoader (Melon host needs local `FUSIONRPG_ML_GAMEDIR` refs; not built on CI)
- Melon LIVE Pass/Fail: [melon-live-checklist.md](../runbook/melon-live-checklist.md) + `scripts/smoke-melon-live.ps1` (author session; Bep record is [debug-live-checklist.md](../runbook/debug-live-checklist.md))
- Game × loader matrix compile when local refs present (`pvzrh-3.8.1` Bep/Melon + `pvzrh-3.9` Melon); see [game-versioning.md](../architecture/game-versioning.md)
- Melon `Il2Cpp.*` LIVE capture/write parity vs Blooms (see [melonloader-assembly-csharp-p0.md](../research/melonloader-assembly-csharp-p0.md); emergency stub via `FUSIONRPG_MELON_SKIP_HARMONY=1`)
- Whether `Plant.Start` HP is overwritten later (`stat.limhealth` observe; enable `SYS-LIMHEALTH-GATE` only if proven)
- `attackDamage` vs `Bullet.Damage`
- Fights with other plugins on this pack
- F8 IMGUI tabs / `CheatPrefixes` / live spawn factories (see play checklist in [cheat-menu-coverage.md](../research/cheat-menu-coverage.md))

### Single-writer live smoke (proof)

Close the game before redeploying the injector DLL. With `SYS-EMIT-PROOF` on (default):

| Step | Action | Expect |
|---|---|---|
| Tab A | Set `A-P-HP%` = 2, PushScales / A-PUSH-NOW | `stat.writer` with `source=cheat.pushScales`; **max** roughly 2× Y0; **current** is live HP ratio-remapped (not Y0 `y.Hp`) |
| Tab B | Set `P-HP` / `P-MAXHP` = 9999, Apply plants | `stat.writer` with `source=cheat.absolute`; same EntityApply → Writer path |
| LimHealth | Watch after either write | **W11-B Bend:** no LIVE prove in this wave. Writer vs `Plant.LimHealth` is Bend; `SYS-LIMHEALTH-GATE` stays default off. Enable the gate only if an operator later sees `stat.limhealth` `revertedVsWriter=true`. |

Both Tab A and Tab B must only hit `EntityStatWriter` (no separate Absolute field bypass).

See [research/open-questions.md](../research/open-questions.md) and [architecture/stat-system.md](../architecture/stat-system.md).

## Logging / metrics / probes

- **Log** = SQLite `events`. Server `ILogger` for sim/probe actions. No extra log files.
- **Metrics** = `metrics` table via `BumpFromKind`. Sim bullets emit `bullet.init`.
- **Probes** = [probes.md](probes.md). Snapshot is the assertion API.
