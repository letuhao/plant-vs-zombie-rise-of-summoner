# Spec: almanac-capture-fix

Module in the [almanac map](../almanac-map.md). No dependencies.

## Objective

The automated almanac sweep (`GameHooks.EnqueueFullAlmanacText`, added 2026-08-23 to iterate every
`PlantType`/`ZombieType` without requiring a per-card click) reuses
`AlmanacTextCapture.TryCapture`, which unconditionally scoops whatever `AlmanacPlantWindow` /
`AlmanacZombieWindow` is currently open (`CaptureWindowTmp`,
[AlmanacTextCapture.cs:97-122](../../../src/FusionRpg.Injector/AlmanacTextCapture.cs)). That's
correct when a click drove the capture — the window is guaranteed to be showing that exact card —
but wrong for the sweep: **for most swept entries a window *is* left open** (whatever the player
last viewed before triggering the sweep), and `CaptureWindowTmp` scoops it regardless of which
entry the sweep loop is currently on. (`CaptureWindowTmp` does early-return when no window exists at
all — [AlmanacTextCapture.cs:115](../../../src/FusionRpg.Injector/AlmanacTextCapture.cs) — so the
failure mode is specifically "a window is open, but showing the wrong card," not "no window is
open." An earlier version of this spec had that backwards.)

**Confirmed live, 2026-08-23** (session: opened almanac, viewed zombie id 6 "舞王撑杆僵尸", then
triggered the sweep):

- Zombie **54** (`TrainingDummy`) — correct `name`/`info` ("用于测试伤害的木桩...韧性：10000") but
  `uiName`/`uiInfo` = "舞王撑杆僵尸(6)..." — zombie 6's window text, mislabeled onto id 54.
- Zombie **247** (`VoodooDollZombie`) — has **no** real `ZombieInfo` entry at all (`displayName`
  falls back to "未命名") yet still got zombie 6's `uiName`/`uiInfo` attached, making an empty
  record look populated.

Done means: a sweep-triggered capture never writes `uiXxx` fields it did not actually observe on
screen, and a manually-clicked capture is byte-for-byte unaffected.

## Design (locked on approval)

Add a parameter to the existing capture entry point rather than a new code path — the click-driven
callers (`AlmanacPlantSelect`/`AlmanacZombieSelect` in
[GameCaptureHooks.cs:527-568](../../../src/FusionRpg.Injector/GameCaptureHooks.cs)) keep the
default and are untouched:

```csharp
// AlmanacTextCapture.cs
public static void TryCapture(string side, int typeId, PlantType? plantType, ZombieType? zombieType,
    bool includeWindowText = true)
{
    ...
    if (includeWindowText)
        CaptureWindowTmp(side, fields, sources);
    ...
}
```

`GameHooks.EnqueueFullAlmanacText` ([GameHooks.cs](../../../src/FusionRpg.Injector/GameHooks.cs))
calls `TryCapture(side, id, ..., includeWindowText: false)` for every entry.

**Not attempting** window-identity verification (checking whether the open window's bound type
equals the id being captured) — no such accessor is referenced anywhere in this codebase or the
`study/` reference mods, and adding one means guessing at undocumented IL2CPP window internals. The
sweep already gets every field that matters (`name`/`info`/`cost`/`introduce`/`seedType`, sourced
directly from `AlmanacDataLoader.plantDatas`/`zombieDatas`, never the window) — the `uiXxx` fields
it loses are UI-label duplicates and feature-toggle text (`ui_text_1_2` = "换肤" etc.), not data. If
window-identity verification becomes useful later (e.g. for the icon-layer sweep), that's a separate
follow-up, not blocking this fix.

**No change to the `Sent` dedup cache**, and a real, permanent consequence follows from it that this
spec must state plainly rather than wave past. `Sent` is process-local
([AlmanacTextCapture.cs:13](../../../src/FusionRpg.Injector/AlmanacTextCapture.cs)) — it resets
every game launch. The **server-side** guard is what actually persists across sessions:
`UploadAlmanacTextDumpAsync` does a `GET` on `/api/almanac/dump/{side}/{typeId}` first and, if that
succeeds (an entry already exists — *any* entry, richer or not), skips the `PUT` entirely
([RpgClient.cs:176-181](../../../src/FusionRpg.Injector/RpgClient.cs)). So:

- Click first (rich), sweep later in a new session (windowless): the sweep's `PUT` is skipped — the
  rich entry is safe. This is the case Success criterion 2 below actually tests.
- **Sweep first (windowless), click later in a new session: the click's `PUT` is also skipped** —
  whichever capture reaches the server first for a `(side, typeId)` wins **permanently**, and a
  richer manual click after a sweep has already touched that type silently adds nothing. This is not
  a database overwrite (the SQL upsert is a full replace, but the injector never issues the `PUT` to
  trigger it) — it's a permanent "first write wins" lock, and it means running the full sweep once is
  effectively a decision to give up on ever enriching those ~900 entries with window text later,
  short of manually deleting the affected `type_almanac_dump` rows.

Not fixing that lock in this module — it predates this bug fix and is a scope decision about the
capture pipeline's caching policy, not about window contamination. Documenting it so the owner can
decide whether it's acceptable before running the sweep broadly.

## Commands

No automated build — Injector code compiles only with `FUSIONRPG_GAME_DIR` set to a real game
install (interop refs). Verify with:

```powershell
$env:FUSIONRPG_GAME_DIR = "H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL"
dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release
```

## Structure

```
src/FusionRpg.Injector/AlmanacTextCapture.cs   (TryCapture gains includeWindowText param)
src/FusionRpg.Injector/GameHooks.cs            (EnqueueFullAlmanacText passes false)
```

No new files.

## Testing strategy

No unit-test surface — `AlmanacTextCapture`/`GameHooks` reference `UnityEngine`/IL2CPP interop types
only resolvable against a real game install, and nothing under `tests/` currently exercises them
(checked: no hits for `AlmanacTextCapture` or `EnqueueFullAlmanacText` under `tests/`). This is a
**live-only** verification, same class as `docs/runbook/melon-live-checklist.md`:

1. In-game: view any almanac card manually (e.g. a zombie), leaving its window open.
2. Trigger the sweep (`POST /api/cheats/action {"action":"almanac-dump-all"}`).
3. `GET /api/almanac/dump?side=zombie` — for every entry captured **during this sweep**
   (`capturedUtc` within the sweep window) whose `typeId` was **not** the one left open, assert
   `uiName`/`uiInfo` are either absent or match that entry's own `name`/`displayName` — never the
   left-open type's name.
4. **In a fresh session** (new game launch — `Sent` must be empty for this type, or the click is a
   local no-op before it even reaches the server), click a card the sweep has **not** yet covered
   and confirm `uiXxx` fields populate exactly as before this change. Testing this within the same
   session as step 2-3 does not work: by then `Sent` already contains every type from the sweep, so
   `TryCapture` short-circuits before computing anything, regardless of this fix.

## Boundaries

- **Always:** keep the default `includeWindowText = true` so every existing click-driven caller is
  byte-identical.
- **Ask first:** any change to the `Sent` dedup semantics (e.g. allowing re-capture to enrich a
  sweep-only entry later) — that's a behavior change beyond this bug fix.
- **Never:** attempt to infer or guess the open window's bound type via reflection/heuristics not
  already proven against real game code — a wrong guess is the same bug with more code.
- **Know before running broadly:** the full sweep permanently forecloses future window-text
  enrichment for every type it touches (server-side "first write wins," see Design above) — this is
  not this module's bug to fix, but running the sweep is a one-way decision worth flagging to the
  owner, not a quiet side effect.

## Success criteria

1. Sweeping the full catalog with any almanac window left open produces zero entries whose `uiXxx`
   fields belong to a different `typeId`.
2. Manually clicking a card produces the exact same `AlmanacTextDumpDto` fields as before this
   change (no regression).
3. `docs/architecture/almanac-map.md` and this file agree on scope; no code touches
   `almanac-seed`/`almanac-spawn-coverage`/`almanac-recipes-fix` files.
