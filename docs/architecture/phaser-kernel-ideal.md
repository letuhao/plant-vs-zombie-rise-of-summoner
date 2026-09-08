# Phaser kernel — the ideal

**Status:** Specify + Plan 2026-09-06 — open questions cleared (locks 1a/2a/3a/4c/5a); capability map
**approved** ([phaser-kernel-map.md](phaser-kernel-map.md)); module specs under
[phaser-kernel/](phaser-kernel/); tasks [phaser-kernel-plan.md](../../tasks/phaser-kernel-plan.md) /
[phaser-kernel-todo.md](../../tasks/phaser-kernel-todo.md). Build starts at T0 — no further spec gate.

**Program id:** `phaser-kernel`  
**Map (approved):** [phaser-kernel-map.md](phaser-kernel-map.md)  
**Module specs:** [phaser-kernel/](phaser-kernel/) (index [phaser-kernel/README.md](phaser-kernel/README.md))  
**Tasks:** [phaser-kernel-plan.md](../../tasks/phaser-kernel-plan.md) /
[phaser-kernel-todo.md](../../tasks/phaser-kernel-todo.md) — Wave 0 (`lock-doc-sync`) first; then Wave 1 code.

**Owner grain (2026-09-06):** a **full** architecture refactor. Build the new kernel **alongside**
the old paths; test it; then retire clones on a named deadline. **Reuse** live `src/game` modules
when they already work; treat clones and inert code as **blueprints** (copy the idea, not the pixels
or the lawn-typed call sites).

**Strengthened against:** five adversarial reviews (locks, code inventory, dual-track, consumers,
scope) plus [fe-phaser-architecture-audit.md](fe-phaser-architecture-audit.md) and
[research/phaser-architecture-prior-art-2026-09-06.md](../research/phaser-architecture-prior-art-2026-09-06.md).

**Inputs already locked (do not reopen):**

- Dual-plane (DPLP): React chrome / pure fold / Phaser island; Phaser never owns HTTP/SignalR.
- One React stage at a time; `Phaser.Game` created on enter, destroyed on leave (GG-11 / D2). The
  **world stage remains a Phaser island** (already shipped) — lock 1a does **not** mean “rebuild
  world in React.” It means: lawn Game and world Game **never coexist**, and entering siege/battle
  **destroys** the world `Phaser.Game` the same way leaving `#/world` already does; leaving siege
  and returning to world **recreates** the world Phaser island. While on siege, React may still hold
  world *data* (queries / SignalR / shell) — that is not a substitute for the world canvas.
  Evidence: scene-switch POC Pass 2026-09-06 (see §Locked answers).
- React owns HUD — no Phaser `UIScene` for Almanac chrome.
- Phaser **4.2** (`"phaser": "^4.2.1"`).
- No FE prediction (RT-15).
- Phaser under `src/game/` — **no React imports in `game/`** (RT-09). Shared React host helpers live
  in `src/game-host/` (never pull Phaser into the entry chunk — GG-38).
- `board-render` Decision 40: grid layer serves **siege + battle**, not the world map. **This
  program does not discharge Decision 40** — lawn may be a live grid consumer first (wave 1.1);
  Decision 40 stays undischarged until siege + battle both call the layer (locked 2026-09-06).
- Siege 21.3+ / battle 22.x unpause: kernel `/spec` freezes API names; **base-defense** owns the
  todo resume and thin scenes — kernel does not write `SiegeBoardScene` from this idea alone.

**Companions (evidence, not this program’s spec):**
[fe-phaser-architecture-audit.md](fe-phaser-architecture-audit.md),
[research/phaser-architecture-prior-art-2026-09-06.md](../research/phaser-architecture-prior-art-2026-09-06.md),
[fe-game-foundation.md](fe-game-foundation.md),
[base-defense/spec-board-render.md](base-defense/spec-board-render.md).

---

## Which loop this extends

This is **not a new loop**. It is how several **place** loops are *seen* on the web canvas:

| Loop ([the-loops.md](../guide/the-loops.md)) | Stage | Kernel role |
|---|---|---|
| **1. Lawn — first core** | `#/lawn/{matchKey}` | Island runtime + grid primitives; lawn paint adapter later |
| **4. World map — adventure** and **5. World stage — empire** | `#/world` | Island runtime; graph camera stays world-owned |
| **3. Farm / hunt / defend** (sieges) | `#/siege/{id}` | **Consumer** after named APIs exist — unpause is base-defense’s job |
| Combat depth / expedition fights | `#/battle/{id}` | Same — playback grammar, not live lawn sync |
| **6. Delve** | `#/delve/{id}` | **Later wave** — island + VFX/SFX/input facades; **not** Decision 40; **not** v1 unpause |

Spine loops stay in Core/Data. Sanctum has no canvas today.

Rise of Summoner is an RPG plus empire-building game. The lawn is the first core loop, not the whole
war. This kernel is the shared **presentation runtime** for canvas stages. It does not invent a
parallel pitch.

---

## Load-bearing principles (restated, not linked)

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** Phaser is
   a web view. Fusion’s write surface does not decide whether a siege board can share a destroy helper
   with the lawn.
2. **The PvZ write surface constrains only persistent vanilla stat changes.** This program writes
   none. The canvas shows a lagged projection.
3. **Two async systems, deltas, record-then-drain.** Phaser must not predict the living set (RT-15).
4. **Gameless-first is capability, not the pitch.** Canvas stages stay playable with Fusion closed.
5. **One power ladder.** No `f(level)` here. Phaser may display numbers; it does not own curves.
6. **The balance surface is data.** Combat/economy numbers live in `data/tuning/`. Cell pixels,
   contain-fit margins, and pool sizes are visual-design / structural (module-local constants with a
   comment) — `spec-board-render.md` already drew that line.
7. **No hard progression ceilings.** Do not add canvas-side caps on HP, XP, or roster size. A pool
   `maxSize` that drops a spark is a **presentation** limit (exempt; say so in a comment).
8. **A game interface is a stage with layers.** Opening a panel must not destroy the Game (GG-11).
   Esc and HUD stay React (GG-18). Phaser keyboard is gated when a layer owns input.
9. **Unity VFX is not web VFX.** Injector `VfxCatalog` / `VfxDirector` ≠ Phaser `FxPool`. Never merge
   the two runtimes.
10. **Definitions and bytes are global per `Phaser.Game`; display-list and tweens are scene-local.**
    Because we destroy the Game on stage leave, **share plain TS modules**, not Game-scoped Scene
    Plugins that die with the island.

---

## What this is

The player travels between places. Each canvas stage is one `Phaser.Game` that dies when they leave.

Today those islands were grown in ship order, so lawn and world each cloned a host, a destroy
checklist, a camera, a registry, and a pick system. A generic grid layer was built for siege/battle
(`src/game/board/`) and left almost unused. Siege canvas work is paused so a third clone does not land.

**The feature:** one **Phaser kernel** — shared TypeScript that outlives a Game destroy — that every
canvas stage configures. **Two kernels, not one mega-engine:**

| Kernel | Who uses it | What it is |
|---|---|---|
| **Island runtime** | Every canvas stage | `createGame` (already live), `destroyGame` + DESTROY mutex, generation bus factory, React host **hook** outside `game/`, `EntityRegistry` |
| **Grid board layer** | Lawn (adapter later), siege, battle | Existing `board/` primitives — not a drop-in lawn paint |

World keeps a **graph** camera and pin/lane/force factories. Delve is a later consumer of island +
facades, not of `GridSpec`, unless a delve spec proves a cell board.

**Player sentence (full target):** the lawn and a siege feel like the same kind of board; the rift
feels like a map; hits and sounds can recur across places.

**v1 delivery sentence (honest):** one destroy path, one host-hook skeleton, one stage-bus factory,
and a **named** import list so siege does not open `LawnWorldScene`. Shared VFX/SFX and the lawn
paint flip are **later waves** of the same program — not wave-1 claims.

**Engineering sentence:** share plain modules; one live Game per stage; dual-track means two TS
implementations tested then switched — never two concurrent WebGL Games.

---

## Debate outcome — what stood and what died

### Stood

- Two kernels (island vs grid). World stays off `GridSpec`.
- Dual-track = two TypeScript paths, **one** live `Phaser.Game` per current stage.
- Phaser 4.2, DPLP, GG-11, RT-15, no UIScene, Phaser under `src/game/` with no React there.
- `board/` is the only grid folder.

### Died (first-draft overconfidence)

| Claim | Why it died |
|---|---|
| “Byte-identical lawn” gates `BoardLayers` | That named test **does not exist**. `ensureGrid` is inset checkerboard `Graphics`; `terrainCache` is full-cell `RenderTexture`, no stroke. Task 20.6 deferred paint because jsdom cannot verify it |
| “React hosts are clones” / `PhaserGameHost` in `island/` | Skeleton only. World has lens, `ignoreRects`, targeting, fingerprint. React in `game/` breaks RT-09 and world GG-38 |
| “Open questions: none” | Buried owner picks (coexistence, Decision 40, host location, paint golden) |
| `FxPool` as built end-to-end | `acquireRing` / `release` have **zero** callers; `StatusFxSystem` voids the pool |
| `WorldRegistry` on one `EntityRegistry` as cutover | Three id spaces; `delete` vs dispose mismatch |
| Delve + VFX/SFX as v1 unpause surface | Pause is siege/battle; delve is party-dungeon Wave 5 |
| “No production importer until tests” **and** “wrappers become one-liners” **and** “old bodies stay live” | Cannot all be true — strangler failure written as the plan |

### Inversion — written, then rejected

**Inversion:** do **not** start `phaser-kernel`. Extract `destroyGame`, leave hosts alone, unpause
siege against the names `board-render` already shipped (`createGame`, `GridSpec`, `BoardLayers`,
`pickCell`).

**Rejected because:** the owner asked for a **full** architecture (island, named buses, host hook,
grid primitives, later VFX/SFX/input, retire plan). Existing `board/` APIs are **unwired**, and
lawn-typed paint/pick (`SyncFromModelSystem`, `PickSystem`, `ActorHudDisplay`) would still be copied
“by feel” — the pause’s third stack. Naming folders is not an import list. A paragraph in
`base-defense-todo.md` does not name `BoardActorRecord`, `playbackCursor`, or `createStageBus`.

---

## Reuse vs blueprint vs new

```text
reuse as-is          createGame.ts (do NOT move into island/)
                     EventBus private on/emit → extract createStageBus
                     EntityRegistry + PtrEntityRegistry
                     bindCamera (contain-fit) · worldCameraSystem (pan/zoom) — two adapters
                     board/ primitives (GridSpec, pickCell, BoardLayers, terrainCache,
                       keyboardNav, visualFor) — types, not lawn paint
                     destroy ORDER (tweens → shutdown → bus → destroy(true))

blueprint            ensureGrid → Graphics-compatible lawn adapter OR golden-approved new look
                     LawnGameHost / WorldGameHost → usePhaserIslandHost (hook outside game/)
                     FxPool rings → wire or delete; FxService later
                     BootScene → Boot({ nextScene }) blueprint; world keeps no Boot (D10)
                     SyncFromModel / Pick / ActorHud → applyLiveBoard / cell-or-actor select /
                       structure HP bar — siege MUST NOT import the lawn files

new                  destroyGame + page DESTROY mutex before next createGame
                     createStageBus + siege:* / battle:* when those stages exist
                     sync grammar: revision | modelSeq | playbackCursor
                     BoardActorRecord, applyPlaybackFrame(BoardView, cursor)
                     lawn importGuard (React owns HTTP/icon epoch; Phaser only lawn:*)
                     focusGate before wireKeyboardNav on lawn
```

Do **not** invent a second folder beside `src/game/board/`. Do **not** relocate wired
`createGame.ts` into `island/` — that is third scatter dressed as a move.

---

## What already exists

### Built

| What | Proof |
|---|---|
| Phaser 4.2 pin | `package.json` `"phaser": "^4.2.1"` |
| Generic `createGame` / `buildGameConfig` | `createGame.ts:26–51` |
| Lawn / world facades over that factory | `createLawnGame.ts:22–24`, `createWorldGame.ts:19–27` |
| Dual EventBus maps + `allocGameGeneration` | `EventBus.ts:5–7`, `:123–124`, `:176–178` |
| Destroy checklists (cloned bodies, correct order) | `createLawnGame.ts:26–47`, `createWorldGame.ts:30–46` |
| Lawn / world hosts (lifetime skeleton) | `LawnGameHost.tsx:24–63`, `WorldGameHost.tsx:53–56` |
| Contain-fit camera live on lawn | `LawnWorldScene.ts:167–178` → `bindCamera` |
| Pan/zoom/edge-scroll camera live on world | `worldCameraSystem.ts` (+ `worldCameraMath.ts`) |
| Cell pick via `GridSpec` / `pickCell` | `gridMath.ts:27–38` → `PickSystem` |
| Generic `EntityRegistry`; lawn wrapper | `EntityRegistry.ts:21–61`, `PtrEntityRegistry.ts:24–28` |
| World registry (three Maps — intentional multi-space) | `WorldRegistry.ts:5–8` |
| World import guard | `game/world/importGuard.test.ts` |
| Siege **shell** only — zero Phaser | `SiegeStage.tsx` |
| Operational canvas budget | `PHASER_OCCUPANT_BUDGET = 96` (`pickPhaserOccupants.ts:5`) |
| `game.destroy(true)` with `noReturn` default false | Phaser 4.2; wrappers pass only `removeCanvas` |
| Pause of siege 21.3+ / battle 22.x | `tasks/base-defense-todo.md` ~1045–1058 |

**Real orchestration (comments are not evidence):** lawn `update` only ticks StatusFx
(`LawnWorldScene.ts:280–282`). Sync / layout / pick run from EventBus handlers in `create`. World’s
four-system allow-list is live systems; the lawn DPLP “update orchestrates four systems” sentence
describes intent, not today’s call graph.

### Wiring gap

| What | The inert / cloned line |
|---|---|
| `BoardLayers` / `terrainCache` / `keyboardNav` / `visualFor` | **Zero** production importers outside `board/*.test.ts`. Lawn still paints in `ensureGrid` (`LawnWorldScene.ts:203–219`). **Not** also “Built end-to-end” |
| `FxPool` rings | Constructed and drained; `acquireRing` / `release` never called; `StatusFxSystem` voids `fx` |
| Destroy checklist cloned | Same order, different scene key / bus emit |
| Host lifetime skeleton duplicated | Props diverge: lawn `viewMode` vs world `lens` / `ignoreRects` / `targeting` / `overlayEpoch` / fingerprint |
| Host folder split | `features/lawn/LawnGameHost` vs `stages/world/host/WorldGameHost` |
| BootScene lawn-hardcoded | `BootScene.ts:17–19` |
| World generation set in `create` not `init` | `WorldMapScene.ts:39–40` |
| Lawn Scene constructor-held registry | `LawnWorldScene.ts:82` — restart bug if scene restarts without Game destroy |
| Lawn Phaser imports `@/lib/bus` (+ deeper) | `LawnWorldScene.ts:19`; also `iconUrl.ts`, `SyncFromModelSystem.ts` — world forbids this |
| `board-render` “closed” with no live board consumer of layers | Module 20 closed; Decision 40’s two callers paused |
| Stale docs | *(cleared T0)* was: Lawn projector “deferred”; “exactly one Phaser”; DPLP §8 missing world/board/camera |

### Real gap

| What | What would have to be built |
|---|---|
| `destroyGame` + DESTROY / page mutex | Refuse next `createGame` until previous Game’s `DESTROY` (or equivalent). Hosts today null the ref while destroy is **next-frame** |
| `createStageBus<E>()` | Typed maps without copy-paste; third `siege:*` / `battle:*` when needed |
| `usePhaserIslandHost` outside `game/` | Frozen `[]` mount: generation, ResizeObserver, buffer-until-ready, DESTROY wait. Facades keep island props |
| Graphics-compatible lawn terrain adapter **or** golden-approved new look | Not `createRenderTextureTerrainSink` as-is |
| Sync grammar + `BoardActorRecord` + `applyPlaybackFrame` | See §Named unpause surface |
| Lawn `importGuard` (no allow-list fork) | React owns HTTP / icon epoch → `lawn:*` only |
| Focus-gate before keyboard on lawn | GG-18 |
| Audio Director / FxService / generic Boot | Later waves of this program’s **target**, not wave 1 |
| Quantified create/destroy memory cost | Probe item |

### Missing from the first inventory (now in scope awareness)

| Item | Why it matters |
|---|---|
| `ActorHudDisplay` + tokens | Live lawn HUD chrome — **do not import from siege** |
| `stackLayout` | Grid + stack view offsets |
| `iconUrl` + `@/lib/bus` | Epoch/cache coupling; importGuard conflict |
| `lawnSyncGate` | Revision / canvas gate |
| `PHASER_OCCUPANT_BUDGET` (=96) | Operational soft canvas budget (`pickPhaserOccupants.ts`) |
| `window.__fusionRpg*` | `__fusionRpgHasHudChild`, world probe/gen cleanup, log append — e2e/debug contract |
| `lawnHostBuffer` / `worldModelProjection` | Host emit policy — not “buffer-until-ready” alone |
| Existing dual-track **unit** tests | `createGame.test.ts`, `board/*.test.ts`, `EntityRegistry.test.ts`, `bindCamera.test.ts` — API shadow only, **not** a paint golden |

---

## The shape (full target, waved)

```text
src/game/                    # Phaser only — no React
  createGame.ts              # REUSE — do not relocate
  destroyGame.ts             # NEW (wave 1)
  EventBus.ts                # REUSE maps; extract createStageBus (wave 1)
  board/                     # REUSE primitives — only grid folder
  camera/                    # contain-fit REUSE; pan/zoom stays world/systems
  entities/                  # EntityRegistry REUSE
  fx/                        # FxPool blueprint → FxService later wave
  scenes/ systems/ world/    # mode islands (thin)

src/game-host/  (or stages/*/host helpers)   # React — OUTSIDE game/
  usePhaserIslandHost.ts     # NEW (wave 1) — frozen [] mount + DESTROY wait
  LawnGameHost / WorldGameHost stay as facades with island-specific props
```

Later waves (still this program’s target architecture — module-id in `/spec`, do not build in wave 1):

- Lawn paint adapter + Playwright canvas golden → delete `ensureGrid`
- Focus-gate → optional `wireKeyboardNav` on lawn
- `FxService(scene)` / Audio Director when audio exists
- Generic Boot for cell stages only (world keeps no Boot until D10 reopens)
- Delve island consumer (party-dungeon owns the stage)

**Helpers vs systems:** a new named step in `update` / the world pipeline needs RT-08 ADR or a
world-spec sentence. `input/` helpers are not a fifth world system. Overlay drawing stays
`worldOverlaySystem`.

### Dataflow

```text
Server / fold (SSOT) → React host → EventBus <stage>:model → Scene systems → GameObjects
                                 ↑
                    <stage>:select (intent / inspector only)
React → lib/bus → Intent     Phaser never fetch/SignalR
```

Model payload carries a **sync grammar**:

```text
seq: { kind: "revision" | "modelSeq" | "playbackCursor"; value: number }
```

Interpolation between two **server-confirmed** positions is rendering. Extrapolating past the last
confirmed state is prediction and stays rejected. The `noClientPrediction` tripwire on `board/` +
`camera/` must be rewritten **before** any allowed lerp lands (confirmed vs extrapolation), with
fixtures — not a silent pattern delete. Prefer keeping pan/zoom under `game/world/systems/` so
`camera/` stays contain-fit-only until that rewrite exists.

---

## Dual-track (executable)

**Rule:** never leave two live bodies for the same duty. Never two concurrent `Phaser.Game`s.

| Wave | Commit shape | In | Out |
|---|---|---|---|
| **1** | Add then switch-and-delete | `destroyGame` + DESTROY/mutex (tests first, **zero** production importers); then switch lawn wrapper **and delete** old body in the **same** commit; world second; `createStageBus`; `usePhaserIslandHost` with `[]` mount; GG-11 tests still mock named facades | Grid flip, keyboard, `island/` relocation of `createGame`, WorldRegistry rewrite, siege unpause, VFX/SFX |
| **1.1** | Paint | Graphics-compatible lawn adapter **or** owner-approved new look; Playwright `canvas.screenshot()` golden; then delete `ensureGrid` in the flip commit | Treating Vitest fakes as byte-identical; `createRenderTextureTerrainSink` as the lawn path |
| **Later** | Facades + consumers | Focus-gate then keyboard; FxService / Audio; Boot for cell stages; named siege/battle scenes **in base-defense** after `/spec` freezes signatures | Runtime kill-switch of two paint paths; second Game under siege |

**Shadow paint** = same Game, two paint functions compared by golden / Graphics command list, then
delete the old function. Re-running already-green `BoardLayers.test.ts` is **not** a shadow run.

**WorldRegistry:** do **not** compose onto one `EntityRegistry` Map in this program. Optional later:
three typed registries with named getters — not a dual-track slice.

**Retire plan:** after both islands use `destroyGame` + host hook; after lawn paint golden (1.1);
delete leftover clones. A permanent façade is a defect. Dates are module ids in the capability map,
not a calendar.

---

## Named unpause surface

Minimum so siege 21.3 writes `import { … } from "@/game/…"` **without opening**
`LawnWorldScene.ts`. This list is the pause’s “names what siege builds against.” Unpause itself
remains **base-defense** after `/spec` freezes signatures — this idea does not write `SiegeBoardScene`.

### Island

- `destroyGame({ game, sceneKey, shutdown, emitDestroyed, generation })` — tweens → shutdown → bus →
  `destroy(true)`, `noReturn` false; wait for `DESTROY` / mutex before the next create
- `usePhaserIslandHost(...)` — props: parent, create/destroy facades, generation, buffer-until-ready,
  resize; **not** lens / targeting / ignoreRects / overlayEpoch / viewMode
- `createStageBus<E>()` → `{ on, emit, clearAll }` + shared `allocGameGeneration`
- Required events: `{stage}:model`, `:select`, `:interaction`, `:ready`, `:resized`, `:destroyed` —
  each carries `generation` (copy world’s table shape, not `<stage>` prose alone)

### Grid (already on disk — name as the import list)

- `makeGridSpec` / `GridSpec` / `pickCell` / `createBoardLayers` / `createTerrainCache` /
  `bindCamera` / `wireKeyboardNav` / `visualFor` / `EntityRegistry<TKey, TRecord>`

### Siege-only types the kernel must name

- `BoardActorRecord`: `{ key: string; row; col; kind: string; hp: bigint; isStructure: boolean; showInitiative: boolean }`
- Structure HP bar ≠ lawn `ActorHudDisplay` (identity / shield / status)
- Overlay painters into `layers.overlays` (range / path / cover) — container only today
- Thin `createSiegeGame` = `createGame({ scenes: […] })`
- Select: `{ generation, kind: "cell" | "actor" | "structure", actorKey?, row?, col? }` — **not** `ptr`

### Battle-only

- `BoardView` produced **outside** the kernel (`stages/battle/`); kernel consumes it
- `applyPlaybackFrame(view: BoardView, cursor: number)` — **not** `syncFromModel(LawnViewModel)`
- `battle:*` uses `playbackCursor`; no intent-shaped resolve; Phaser **must not** re-resolve combat

### Do not import from siege / battle

- `SyncFromModelSystem`, `PickSystem`, `ActorHudDisplay`, lawn `PtrEntityRegistry` as the record shape

### Explicitly out of the v1 named unpause surface

- `vfx/`, `sfx/`, delve room-graph, HUD copy, `projectReportToBoard` body (stays in `stages/battle/`)

**Unpause bar (one line):** 21.3 can write a Siege scene that imports `destroyGame`,
`usePhaserIslandHost`, `siegeBus*`, `BoardActorRecord`, `createBoardLayers`, and a live-board apply
helper — without opening `LawnWorldScene.ts`. Until that list is frozen in `/spec`, the refactor does
not exist in the sense the pause meant.

---

## Decision 40 honesty

Decision 40 (`spec-board-render.md`): the generic layer is proven by **two** callers — **siege and
battle**. A layer with one consumer is a layer only by assertion.

This program:

- May deliver a lawn paint **adapter** (the 20.6 deferral) as wave 1.1.
- Does **not** rewrite Decision 40 as “lawn first, then name APIs.”
- Does **not** discharge the two-caller proof. Naming imports is the pause-resume sentence, not
  Decision 40.
- Unpause of siege/battle remains base-defense’s after signatures freeze.

---

## Prior art (condensed)

- Global per Game vs scene-local: [Scenes](https://docs.phaser.io/phaser/concepts/scenes).
- `Game.destroy(removeCanvas, noReturn)` is **async** (next frame); `noReturn: true` blocks another
  Game on the page — keep default false.
  ([Game.destroy](https://docs.phaser.io/api-documentation/class/game))
- Official React template: EventBus; React never owns GameObjects.
- Strangler: grow beside, switch, **delete** the host. Permanent façade is the known failure mode.
  Dual-write of *data* is fragile; dual *paint paths* in production drift the same way.
- Pools: Group `maxSize`, particle `reserve`; `killAndHide` + `killTweensOf`. No magic pool size for
  this game.
- Concurrent multi-Game WebGL: historically fragile; **unverified** for 4.2; we avoid it (GG-1).

Full index: [phaser-architecture-prior-art-2026-09-06.md](../research/phaser-architecture-prior-art-2026-09-06.md).

---

## Tunables

No `data/tuning/phaser-kernel.v1.json` in the idea phase.

| Number | Class | Where |
|---|---|---|
| Entry chunk ≤180 KB gz | Structural (GUI lock) | `decisions.md` Game GUI; `check:bundle` |
| Cell pixels, contain-fit margins | Visual-design | Module-local constants |
| `PHASER_OCCUPANT_BUDGET` (96) | Soft canvas guidance | `pickPhaserOccupants.ts` — not a progression ceiling |
| FX `maxSize` / `reserve` | Presentation structural | Named const + comment; never Core `vfx.v{n}.json` |
| `noReturn` | Structural | Always false while another stage Game can follow |

---

## What this deliberately does not decide

- Siege HUD, battle playback UX, delve room-graph visuals — those programs’ specs.
- Whether delve is a cell board or a follow-camera crawl.
- Art, atlases, audio corpus — D10 framed placeholders remain valid.
- Moving lawn React from `features/lawn/` to `stages/lawn/`.
- Lifting textures outside Phaser for zero-reload stage travel.
- Injector VFX, Funnel, turn engine.
- Writing `SiegeBoardScene` / unpausing in `base-defense-todo.md` from this idea alone.

---

## Locked answers (cleared 2026-09-06)

Owner cleared these after the scene-switch POC **PASS**
([phaser-scene-switch-poc-2026-09-06.md](../research/phaser-scene-switch-poc-2026-09-06.md):
Track A p95 1.4 ms; Track B p95 33.7 ms; GG-11 identity stable). Dual-plane stays; do **not**
rebuild all FE chrome in Phaser on this evidence.

| # | Question | Lock |
|---|---|---|
| **1** | Siege vs world Game | **1a** — One `Phaser.Game` at a time. **World stage stays Phaser** (do not convert the map island to React). Entering siege/battle destroys the world Game (same lifetime as leaving `#/world` today); returning to world recreates it. React may keep world *data* while on siege — not a live second WebGL world under the siege canvas. No dual-Game waive. |
| **2** | Decision 40 vs lawn paint | **2a** — Lawn may consume `BoardLayers` as wave 1.1 without discharging Decision 40. Discharge still requires siege **and** battle live callers. |
| **3** | Host hook location | **3a** — Shared React helpers in `src/game-host/` (outside `src/game/`). Only lazy stage hosts import `create*Game`; never StageHost / entry barrel (GG-38). ScenePOC under `dev/` was measurement-only. |
| **4** | Paint flip look | **4c** — Defer paint flip; island runtime first. Match-or-golden Graphics look is a later wave, not a `/spec` blocker. |
| **5** | Who unpauses siege/battle | **5a** — Kernel freezes API names in `/spec`; **base-defense** owns todo resume and thin scenes. Kernel does not write `SiegeBoardScene` from the ideal alone. |

Program id remains **`phaser-kernel`**.

**Follow-up (does not reopen the locks above):** the scene-switch POC did **not** drive a ticking
model through generation/buffer/ready. Before trusting real stage travel, add a data-lifecycle bar
(fake revision from React → bus → active scene only; buffer across Track B destroy/create). That is
host-contract work, not a reason to reopen 1a–5a or escalate to Phaser UI.

---

## Gate before `/spec` (stale lock docs)

**Patched 2026-09-06 (T0 / `lock-doc-sync`).** Historical table kept for provenance:

| Doc | Was stale | Fix landed |
|---|---|---|
| `decisions.md` Lawn projector row | “Implementation deferred” | Aligned with W6–W7 shipped + world island note |
| `spec-board-render.md` opening | “Exactly one Phaser integration … lawn-shaped” | Lawn **and** world islands; `board/` for cell stages; Decision 40 undischarged |
| `fe-game-foundation.md` §8 folder tree | `createLawnGame` only | `createGame`, `world/`, `board/`, `camera/`, `game-host/` |
| Occupant budget prose (DPLP / lawn-projector) | ≤50 / ≤80 as if live canvas constant | Cite `PHASER_OCCUPANT_BUDGET = 96` |

Idea-phase honesty is not a license to leave contradictory locks in the path of implement.

---

## Alternatives rejected

| Rejected | Why |
|---|---|
| No new program — only `destroyGame` + unpause on existing board names | Owner asked for full architecture; unwired board + lawn-typed paint/pick still clones a third stack |
| One app-lifetime Game + `scene.start` lawn↔world | Fights GG-11 / D2 |
| Mega-Scene / deep BaseScene | Mode rules creep in |
| World into `GridSpec` | Graph ≠ cell board |
| Second folder beside `board/` | Unused kernel *is* the scatter |
| `PhaserGameHost` React component inside `src/game/` | RT-09 + GG-38 |
| One universal camera class | Write-only contain-fit vs read-back pan/zoom are incompatible |
| ParticleEmitter / tween handles on module singletons | Scene-local; leaks across destroy |
| Client prediction | RT-15 |
| Two concurrent Games for A/B pixel compare | GG-1 / WebGL |
| Runtime kill-switch of two paint paths | Dual implementations drift |
| Scanned allow-list for lawn `@/lib/bus` | Plane lock becomes a comment |
| Phaser 3 migration | Pin is 4.2 |

---

## DESIGN-GATE §5 (strengthen pass)

```
[x] Subsystems: DPLP, Game GUI, IA stages, world-map Phaser, board-render / paused siege-battle,
    product vision / loops, party-dungeon delve boundary.
[x] Evidence from code grep/read this session (FxPool callers, PHASER_OCCUPANT_BUDGET=96,
    EventBus maps, ensureGrid vs terrainCache, destroy async).
[x] Five adversarial reviews reconciled into this rewrite.
[x] Vitest + Playwright measure: `phaser-scene-poc` (2026-09-06) — see research POC Results.
[x] No §2 invariant contradicted. Dual-track is two modules + DESTROY mutex, not two Games.
[x] Stale lock docs listed as a /spec gate, not silently ignored.
```
