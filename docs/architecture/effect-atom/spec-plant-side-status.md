# Spec: plant-side-status (E39)

Module **E39** in the [atom effect map](../effect-atom-map.md) §13 (Wave 8). Depends on **E28**
(`param-parity`). Ideal: [effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 5.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit.
> Where this spec and the definitions disagree, **the definitions win**.

## Objective

Half the board cannot be statused. `ExecApplyStatus` iterates `FindObjectsOfType<Zombie>()` and nothing
else, so a status atom aimed at a plant does nothing on the lawn — while the same atom works in Battle,
whose path resolves a bare ptr and never asks which side it is. **This is a lawn-only executor
asymmetry, not a vocabulary change.** E39 widens the executor's target set and makes the cases the game
genuinely cannot serve refuse loudly instead of silently.

## 1. What exists today

### Built

| Fact | Where |
|---|---|
| 21 statuses registered: **8** `UnityCc` (butter, freeze, cold, poison, hypno, ember, jala, kelp) and **13** overlay-authored | `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:16-58` |
| FA2 is emitted **only** for a status whose payload kinds contain `UnityCc` — the other 13 resolve entirely inside `StatusRuntime` | `src/FusionRpg.Core/Status/StatusEffectBridge.cs:373` |
| Battle's `ExecApplyStatus` resolves `targetPtr` and applies; it never asks the side | `src/FusionRpg.Core/Battle/BattleEffects.cs:154-171` |
| A ptr-keyed registry already resolves **both** sides in O(1) | `src/FusionRpg.Injector/Effects/InjectorEntityRegistry.cs:269` (`FindZombie`), `:272` (`FindPlant`) |
| `ExecApplyResourceDelta` already handles both sides — it iterates plants **and** zombies | `src/FusionRpg.Injector/Effects/InjectorEffectActionSink.cs:179`, `:187` |
| The zombie status switch, including E17's ember/jala/kelp arms | `src/FusionRpg.Injector/DebugActions.cs:854-919` |

### Wiring gap

| Gap | Where |
|---|---|
| `ExecApplyStatus` resolves a target ptr, then looks for it only among zombies | `InjectorEffectActionSink.cs:209-235` |
| A plant ptr therefore misses, `n` stays 0, and the executor **fails closed** (`return n > 0;`) — so the whole sequence stops, with no message saying "that target was a plant" | `InjectorEffectActionSink.cs:248` |
| `ExecClearStatus` has the identical zombie-only shape | `InjectorEffectActionSink.cs:271-333`, scans at `:288` and `:293` |
| G5's unguarded board-wide loop: an empty target applies the status to **every zombie alive** | `InjectorEffectActionSink.cs:251-256`. E1 left this open explicitly — *"it belongs to whoever guards that loop"* (`spec-atom-kind-registry.md`, `status.apply` row) |

### Real gap

| Gap | Note |
|---|---|
| Plant-side vanilla CC methods are unconfirmed | `docs/research/effect-runtime/03-status-and-spawn-surface.md:38-40` records `butterP` and a `SetFreezedPlant` "in dump" and classes plant CC **DOC / later**. That sweep was against 3.8.1. **UNVERIFIED against the 3.9 interop** — an assembly-metadata sweep is a prerequisite, the same discipline E17 used (`StatusCatalogBootstrap.cs:36-50`) |
| The debug status paths are zombie-only too | `DebugActions.ResolveZombies` (`DebugActions.cs:1310`) drives both `ApplyStatus` and `ClearStatus` (`:801`, `:826`), while its plant twin `ResolvePlants` (`:1347`) is used by `Kill` (`:925`) and by no status path |

## 2. The contract

### 2a. Target resolution — registry first, side second

`ExecApplyStatus` and `ExecClearStatus` both become:

1. Resolve the ptr through `InjectorEntityRegistry.FindZombie`, then `FindPlant`. **O(1) both times.**
2. On a registry miss, fall back to a single scan of the resolved side only — never both, never
   unconditionally.
3. A resolved ptr that matches neither side is a **failure** (`return false`), not a silent success.

No `FindObjectsOfType` scan is added by this module. The two that survive are the existing miss-path
fallbacks, and the board-wide loop below is deleted.

### 2b. G5 closes here — "all" must be explicit

The empty-target loop at `InjectorEffectActionSink.cs:251-256` is removed. An atom that means the whole
board says so:

| `target` | Meaning |
|---|---|
| omitted / `event` / `selected` | the event's resolved ptr, either side |
| `all-zombies` | every living zombie |
| `all-plants` | every living plant |
| `all` | both sides |

`status.clear` already declares a `target` string (`AtomKindRegistry.cs:245`); `status.apply` declares
none, deliberately — E1 refused to add one because FA2 reads its target from the event
(`ResolveStatusTargetPtr`, `InjectorEffectActionSink.cs:200-207`). **E39 does not add a `target` param
to `status.apply` either.** The board-wide case reaches the sink through the plan item's `targetPtr`
that `StatusEffectBridge` already writes (`StatusEffectBridge.cs:381`) — one item per resolved host ptr,
which is what `ResolveHostPtrs` (`StatusEffectBridge.cs:355`) is for. An empty ptr becomes a refusal,
not a board-wide broadcast.

### 2c. Side capability — refuse at execute, do not invent a code

| Status class | Plant target |
|---|---|
| The 13 overlay-authored statuses (`StatusCatalogBootstrap.cs:26-58`) | **Work today** once the ptr resolves — they live in `StatusRuntime`, keyed by owner key, and never touch a Unity field |
| The 8 `UnityCc` statuses with a confirmed plant-side method | Call it, exactly as the zombie switch does |
| A `UnityCc` status with **no** plant-side method | `return false`, plus a skip reason in the emit — `"status-side-unsupported"` |

**No new rejection reason code.** The list is closed at 33 (`definitions.md` §10), and this is not a
load-time fact: whether a target is a plant is known only at execute. The skip-reason string follows
the shape `StatusEffectBridge.cs:365-369` already uses (`grant.GrantId + ":status-…"`).

Which of the eight have a plant-side method is **UNVERIFIED** and is the module's first task: sweep the
3.9 `Assembly-CSharp` metadata, record the answer in `03-status-and-spawn-surface.md` §"Plant-side
status", and wire only what the sweep found. E17 is the precedent for why: three statuses were declared
against methods the game did not have, and every application queued an inert plan item that looked like
a working effect in every trace.

### 2d. The emit says which side

`pvz.status.apply` (`InjectorEffectActionSink.cs:237-246`) and `pvz.status.clear` (`:324-331`) gain a
`side` key (`plant` | `zombie`) and, on a refusal, a `reason`. A status that did nothing must be
visible on the wire — that is the whole difference between this and the state it replaces.

## 3. What it must NOT do

- **Do not change the status vocabulary.** 21 ids, code-first, ADR-locked. E39 adds no status, renames
  none, and does not touch `StatusCatalogBootstrap`.
- **Do not add a `target` param to `status.apply`.** E1 refused it for a stated reason that still holds.
- **Do not add a rejection reason code.** Runtime refusals are `return false` plus an emit reason.
- **Do not fake a missing plant method with a float write.** `StatusCatalogBootstrap.cs:44-50` records
  what that costs: the status looks implemented and does something else. Refuse instead.
- **Do not add a `FindObjectsOfType` scan on the per-hit path.** The 2026-08 perf audit named per-hit
  board scans as the cause of combat lag; the registry lookup exists precisely to avoid them.
- **`long` for any magnitude** this path carries (a DoT's per-tick amount rides FA10, not FA2, but the
  rule holds wherever E39 touches one) — **never `float`**, widen before multiplying, divide by 1000
  last, and let overflow **throw**.
- **No hard ceiling.** Nothing here caps duration or stacks; stacking and ICD are the status runtime's,
  and any limit that appears is a **structural** bound on a loop or buffer and must say so in a comment.
  Durations, ICDs and stack limits a balance pass would touch belong in `data/tuning/status.v1.json`.
- **Do not touch Battle.** Its path is already side-agnostic (`BattleEffects.cs:154-171`). Editing it to
  "match" would be a change with no defect behind it.

## 4. Testing strategy

| Case | Expect |
|---|---|
| An overlay-authored status applied to a plant ptr | applies; `StatusRuntime` holds an instance keyed to that owner |
| **Planted violation:** restore the zombie-only resolution in `ExecApplyStatus` | the plant-target test fails. It must not pass through the miss-path fallback |
| **Planted violation:** re-add the empty-target board-wide loop | the G5 test fails — an empty ptr must refuse, not broadcast |
| A `UnityCc` status with no plant method, aimed at a plant | `return false` and an emit carrying `reason: status-side-unsupported` — never a silent success |
| `target: "all"` | both sides; `all-plants` and `all-zombies` each hit exactly one side |
| `ExecClearStatus` on a plant | clears what was applied; a status that can be applied and never cleared fails this test |
| Battle's path | byte-identical behaviour before and after — a regression test, not a new one |
| Per-hit scan count on a 40-zombie board | unchanged or lower than before E39 |

**The injector is not built by CI** (`.github/workflows/ci.yml:75-103` — ten test projects, no injector
build). So: the `StatusRuntime` half, the target vocabulary and the refusal shape assert in
`FusionRpg.Core.Tests` against a fake sink; the two executor rewrites are covered by a text guard in the
`scripts\guard-*.ps1` family and confirmed by an owner-run lawn proof. **The planted-violation tests
must live on the Core side of that line**, or they are not run by anything.

## 5. Acceptance criteria

1. A status atom aimed at a plant ptr applies, for every one of the 13 overlay-authored statuses.
2. A `UnityCc` status aimed at a plant either calls a confirmed plant-side method or refuses with a
   reason on the wire. Neither case is silent.
3. The metadata sweep result is written into `docs/research/effect-runtime/03-status-and-spawn-surface.md`
   with the interop version it was taken from.
4. An empty resolved target refuses; the board-wide `FindObjectsOfType<Zombie>()` loop is gone (G5 closed).
5. `all`, `all-plants` and `all-zombies` each behave as specified, and each is covered by a test.
6. `ExecClearStatus` is symmetric with `ExecApplyStatus` — no status can be applied to a side it cannot
   be cleared from.
7. `pvz.status.apply` / `pvz.status.clear` carry `side`, and a refusal carries `reason`.
8. Battle behaviour is unchanged, proven by the existing battle status suite staying green untouched.

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **E28 `param-parity`** | Owns `status.clear` to 21-status parity — the **switch's status coverage**. E39 owns the **target set** on both executors. Do not build E28's half here (map §16) |
| **E1's G5** | Left open deliberately, assigned to "whoever guards that loop". E39 is that owner and must say so where the code changes |
| **battle-timeline B25/B26** | B26 freezes shield and DoT behaviour while this edits the same `EffectRuntime` drain chain, and the injector is not built by CI. Map §16 H1 recurring — sequence, do not straddle |
| **VFX program** | Status application drives status VFX; a plant now receiving butter or freeze is a new visual case. Coordinate with any open blind-identity trial |
| **Status SSOT** | `status-ssot.md` §9 owns the 21 ids. E39 changes execution, not the roster; if the sweep shows a plant-side method the SSOT does not mention, that is a doc fix, not a new status |
| **E17 precedent** | Three statuses shipped declared against methods that did not exist. The sweep-before-wire rule here exists because of that, and skipping it reproduces it |
