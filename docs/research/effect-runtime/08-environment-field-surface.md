# Environment / field surface inventory

Cecil dump of `Assembly-CSharp` interop (PvZ Fusion 3.8.1). Feeds LIVE env scenarios and Effect ship/skip.

Legend: **CALLABLE** mid-match · **LEVEL-BOUND** scene/level start · **UNKNOWN** needs LIVE / risky.

---

## 1. `BoardAction` (via `Board.Instance.boardAction`)

| Method | Signature (abbrev) | Class |
|---|---|---|
| `CreateFreeze` | `(Vector2 pos, float timer)` | **CALLABLE** |
| `SetDoom` | `(col, row, setPit, iceDoom, Vector2 pos, damage, effect, Action, existParticle, PlantType fromType)` → `Crater` | **CALLABLE** |
| `SetSmallDoom` | `(pos, row, Team, damage)` | **CALLABLE** (unused in v1 probes) |
| `CreateFireLine` | `(row, damage, fromZombie, fix, shake, Action, PlantType fromType)` | **CALLABLE** |
| `CreateFireLineVision` / `CreateFireAnim` / `FireLineDamage` | helpers | DOC |
| `CreateCherryExplode` | `(Vector2, row, CherryBombType, damage, PlantType, Action, immediately)` → `BombCherry` | **CALLABLE** |
| `SetPit` | `(col, row)` → `Crater` | DOC |

Safe probe defaults (from mod packs): `damage ≈ 1800`, `Action = null`, `PlantType(-1)`, `CherryBombType.Normal`, freeze `timer ≈ 3`.

Position helper: `Mouse.Instance.GetBoxXFromColumn(col)` / `GetBoxYFromRow(row)`.

Debug: `debug.board-action` `{ op, row, col, … }` → event `debug.board.action`.

---

## 2. Grid / graves

### `GridItemType`

| Id | Name |
|---|---|
| 0 | CraterDay |
| 1 | CraterNight |
| 3 | Ladder |
| 4–6, 9–12 | ScaryPot* |
| **7** | **Grave** |
| 8 | IceBlock |

(No enum member for `2`.)

### `GraveType`

| Id | Name |
|---|---|
| 0 | Default |
| 1 | Sunflower |
| 2 | Sunshroom |

API: `GridItem.SetGridItem(col, row, GridItemType, GraveType)` — **CALLABLE**.  
Cheat already: `CheatActions.SpawnGrid`. Debug: `debug.spawn-grid` → `grid.place`.

`/api/types?side=grid` may be empty until catalog enqueue succeeds; enum dump above is authoritative for probes (`typeId=7`).

---

## 3. Scene / weather

### `SceneType` (level / map)

Includes classic + Fusion extras: `Day`, `Night`, `Pool`, `NightPool`, `Roof`, `Snow`, `Snow_6`, `SnowPool`, `NightSnow`, `NightWinter`, `LavaBeach`, `River`, … (0–42). Full list in Cecil dump session.

`GameAPP.theBoardType` / `theBoardLevel` — level identity.  
`Board.sceneType` — has getter **and** setter; `Board.SmoothlyChangeMap(SceneType)` exists.

| Surface | Class | Effect action? |
|---|---|---|
| Pick/play a fog/pool/snow level | **LEVEL-BOUND** | Design note only — **NOT SHIPPED** as mid-match Effect |
| `Board.sceneType` / `SmoothlyChangeMap` | **UNKNOWN** (map swap mid-match; crash/visual risk) | **NOT SHIPPED** until dedicated LIVE |
| Classic fog / pool water / roof snow as shaders | tied to `SceneType` assets | **LEVEL-BOUND** |

### Fog runtime (`FogMgr`)

`FogMgr.Instance`: `AppearFog` / `MoveFog` / `FadeFog` / `Blown` / `Light` — **CALLABLE** if fog objects exist (typically fog scenes).  
`Board.fog` is a `GameObject` reference.

Class: **UNKNOWN** on daytime lawn (likely no-op or NRE). Do **not** ship as Effect until proven on a fog level.

### Light / ice board fields

| Member | Notes | Class |
|---|---|---|
| `Board.AddLightLevel` / `TempAddLightLevel` | night lantern-style light | **CALLABLE** DOC — not LIVE this pass |
| `Board.iceRoads` / `iceDoomFreezeTime` | ice doom leftovers | DOC |

No dedicated mid-match “enable fallout / pool water / snow particles” API found beyond scene load + fog mgr.

---

## 4. Ship matrix (post LIVE F37+ / F42+)

| Candidate Effect action | Surface | Status |
|---|---|---|
| `createFreeze` AOE | `CreateFreeze` | **READY** F37 |
| `setDoom` | `SetDoom` | **READY** F38 (+ crater grid) |
| `createFireLine` | `CreateFireLine` | **READY** F39 |
| `createCherryExplode` | `CreateCherryExplode` | **READY** F40 |
| `spawnGrave` | `SetGridItem` Grave | **READY** F41 / F42 |
| `clearGrave` | `GridItem.Die` + `RemoveGrave` | **READY** F43 |
| `spawnIceBlock` | `GridItemType.IceBlock` | **READY** F44 |
| `setBoxType` (Water/Grass/Lava…) | `BoardGrid.boxType` + `UpdateBox` | **READY** F45–F47 (script readback) |
| Nuclear scorched grass → Dirt (+ pit) | `boxType=Dirt` / alias `nuclear` + `SetPit` | **READY** LIVE (`tile-box-dirt`) |
| Ice trail (Zomboni / Sledge) | `DriverZombie.CreateIceRoad` | **NOT SHIPPED** — LIVE fail: probe only spawns Sledge/Driver; no reliable ice-trail Effect (`tile-ice-road` F51) |
| OnKill → spawn grave | arm `onkill-grave` | **READY** F48 |
| OnKill → clear random grave | arm `onkill-clear-grave` | **READY** F49 |
| `Board.roadType` cell paint | array len≈12 | **FAIL** — not full lawn |
| Scene weather / fallout | SceneType / FogMgr | **NOT SHIPPED** |

Debug: `debug.spawn-grid`, `debug.clear-grid`, `debug.set-box`, `debug.grid-query`, `debug.ice-road`.  
Scenarios: `tile-*`, `tile-box-dirt`, `tile-ice-road`, `onkill-grave`, `onkill-clear-grave`.  
Raw: [`_checklist-tile-live.json`](_checklist-tile-live.json), [`_checklist-dirt-ice-live.json`](_checklist-dirt-ice-live.json). Checklist §10.
