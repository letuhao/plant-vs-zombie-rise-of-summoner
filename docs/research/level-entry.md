# Level entry (research)

How PVZ Fusion opens a lawn, what FusionRpg observes today, and what a future summoner “custom run” should wrap.

**Status:** observation + gated probe only. Not product UX. Do **not** fabricate a `Board`.

## Audit verdict (2026-08-20)

| Goal | Game support | FusionRpg today |
|---|---|---|
| Fabricate empty sandbox `Board` | **No** — no `CreateBoard` | N/A |
| Start **Adventure** from main menu | **Yes** — vanilla `UIMgr.EnterGame` (used by `Advanture_Btn`) | Gated `debug.enter-level` wraps the same API; **L1 LIVE still pending** |
| Start **Challenge** / TravelSandBox (129) | **Yes** at API | Same probe; LIVE pending |
| Start vanilla **file custom map** (`LevelType.CustomLevel=11`) | **Partial** — `LevelLoader` / `LevelRegistry` / `CustomLevelMenu` exist; bare `EnterGame` without preload is unlikely to work | Probe calls EnterGame only — **not** a full custom-map launcher |
| Mid-match lab on open lawn | N/A | **Yes** — `lab-overlay` / `lab-empty` |

**Bottom line:** the game supports starting levels via `EnterGame`. It does **not** mean “injector invents a custom map.” Custom **file** maps need the vanilla loader/registry first. Our probe is the correct thin wrapper for Adventure/Challenge tests, not a custom-map product.

Setting `GameAPP.theBoardLevel` / `theBoardType` alone does **not** spawn a Board.

---

## Why you can stay on the main menu

Observed LIVE: `POST /api/debug/enter-level` returns `{ ok: true, queued: 1 }` even when the **gate is off**. The injector then emits `debug.level.enter` with `ok:false` and **never** calls `UIMgr.EnterGame`.

Other causes:

1. Gate off (`FUSIONRPG_LEVEL_ENTRY` unset and `DEBUG-LEVEL-ENTRY` off) → reject
2. `GameHooks.Board != null` without `force` → reject
3. Exception inside `EnterGame` → caught; menu unchanged
4. Silent no-op (e.g. `CustomLevel` with empty args / level not in registry)
5. Wrong mode after a partial enter (Explore with zero zombie speed looks “stuck”)

**Correct Adventure probe (main menu, Board null):**

```powershell
# Enable gate (toggle) then call:
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/cheats/toggle `
  -ContentType application/json -Body '{"id":"DEBUG-LEVEL-ENTRY","enabled":true}'
Invoke-RestMethod -Method POST http://127.0.0.1:5088/api/debug/enter-level `
  -ContentType application/json `
  -Body '{"levelType":0,"levelNumber":1,"id":0,"name":""}'
# Pass = debug.level.enter ok=true, then board.start levelType=Advanture
```

Or set process env `FUSIONRPG_LEVEL_ENTRY=1` before launch (injector process).

---

## What FusionRpg already observes

On `Board.Awake` → `board.start` ([`GameHooks.cs`](../../src/FusionRpg.Injector/GameHooks.cs)):

- `matchKey`, `levelName`, `modifiers`
- `boardLevel` ← `GameAPP.theBoardLevel` (read)
- `levelType` ← `GameAPP.theBoardType` (read)

Lifecycle: [events-lifecycle.md](events-lifecycle.md). Mid-match lab: [debug-pipeline.md](../runbook/debug-pipeline.md).

---

## Interop (3.8.1 Cecil)

**Only overload:**

```text
UIMgr.EnterGame(LevelType levelType, Int32 levelNumber, Int32 id, String name) -> Void
```

Also: `EnterTravelGame`, `EnterIZGame`, `GetSceneType`, menu helpers (`EnterChallengeMenu`, `BackToMenu`, …).

### `LevelType`

| Value | Name | Notes |
|---|---|---|
| -1 | `Nothing` | |
| 0 | `Advanture` | Game spelling; normal adventure days |
| 1 | `Challenge` | Challenge ids (not LevelType.CustomLevel) |
| 2 | `IZ` | |
| 3 | `Survival` | |
| 4 | `Explore` | LIVE: empty/slow boards can look “stuck” |
| 5 | `TravelAdvanture` | |
| 6–10 | Skin / Abyss / NewAdv / Tower / Star | |
| **11** | **`CustomLevel`** | File/custom map via loader + registry |

**Challenge ids (not LevelType):** `TravelSandBox = 129`, `CustomMapEditor = 52`, `CustomMap = 53`.

**`id` / `name`:** for Adventure day 1 use `0` / `""`. Travel may use `id` as endless/travel save id; IZ uses name. Survival restore is `SaveMgr.LoadBoard(level, id)`.

### Vanilla custom-map stack (exists; not wired in FusionRpg)

- `CustomLevelMenu` — local/online file UI
- `CustomButton_enterGame` — holds `CustomLevelData` / click → enter
- `GameLevel.LevelLoader.LoadLevelFromFile(string)` / `LoadDynamicLevels`
- `LevelRegistry.RegisterDynamicLevel` / `TryGetLevel`
- `SerializedLevelData.BuildLevel() -> CustomLevelData`

Hypothesized custom-file sequence (unproven LIVE):

1. Main menu  
2. `LevelLoader.LoadLevelFromFile(path)` (register dynamic level)  
3. Resolve `levelNumber` from registry  
4. `UIMgr.EnterGame(CustomLevel, levelNumber, id?, name?)`

Safer product path: drive vanilla UI (`CustomLevelMenu`) rather than invent Board.

`InitBoard` is card bank only — not a lawn factory.

---

## FusionRpg code audit (`debug.enter-level`)

[`DebugActions.EnterLevel`](../../src/FusionRpg.Injector/DebugActions.cs):

| Check | Result |
|---|---|
| Signature matches `UIMgr.EnterGame` | **Correct** |
| Runs on Unity main thread (command drain) | **Correct** |
| Gate default off | **Correct** for safety |
| Reject when Board live | **Correct** for L1 |
| Adventure/Challenge probe args | **Correct** shape (`0,"",1`) |
| Custom file map | **Incomplete** — no loader/registry preload |
| REST `{ok:true,queued:1}` when gate off | **Misleading** — HTTP only means queued; read `debug.level.enter` |

Hard bans unchanged: no `GameAPP.Start` patch; no mid-match second EnterGame until proven; no `SmoothlyChangeMap` as Effect.

---

## LIVE probe matrix

| # | Setup | Call | Pass criteria | Result |
|---|---|---|---|---|
| L0 | Operator | Open Adventure day (not Explore) | `board.start` Advanture/Challenge | Explore `1018` looked stuck (`zombieSpeedMultiplier=0`) |
| L1 | Main menu + gate on | `EnterGame(Advanture, 1, 0, "")` | `debug.level.enter ok` + `board.start` | _pending_ |
| L2 | Board live | enter-level without force | Reject | _pending_ |
| L3 | Main menu | `Challenge, 129` | Sandbox lawn | _pending_ |
| L4 | Main menu | CustomLevel + file preload | Loads map or clear error | _pending_ — needs loader work |
| L5 | After L1 | `lab-overlay` | `run-steps.done` + living fixtures | partial — Admit cap / board issues seen |

---

## Design implication (future summoner run)

Wrap vanilla entry — do not invent a Board:

1. Choose `LevelType` + number (+ custom map asset registered via loader if CustomLevel)  
2. `UIMgr.EnterGame` from a safe UI context (main menu)  
3. On `board.start`, apply overlay lab / progression / effects  

---

## Related

- Lab script: `scripts/setup-lab-run.ps1` (requires live Adventure lawn)  
- Runbook: [debug-pipeline.md](../runbook/debug-pipeline.md)  
- Env LEVEL-BOUND: [08-environment-field-surface.md](effect-runtime/08-environment-field-surface.md)
