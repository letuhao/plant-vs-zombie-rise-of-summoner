# Capability map: phaser-kernel

**Status:** map approved 2026-09-06 (strengthen pass). Module specs under
[phaser-kernel/](phaser-kernel/).  
**Program id:** `phaser-kernel`  
**Ideal:** [phaser-kernel-ideal.md](phaser-kernel-ideal.md) (locks 1a/2a/3a/4c/5a).  
**Evidence:** [fe-phaser-architecture-audit.md](fe-phaser-architecture-audit.md) ·
[research/phaser-scene-switch-poc-2026-09-06.md](../research/phaser-scene-switch-poc-2026-09-06.md) ·
[research/phaser-architecture-prior-art-2026-09-06.md](../research/phaser-architecture-prior-art-2026-09-06.md).

Plan/todo: `tasks/phaser-kernel-plan.md` / `tasks/phaser-kernel-todo.md` (2026-09-06).
**Not** root `SPEC.md` / bare `tasks/plan.md`.

---

## What this program is

Shared **presentation runtime** for web canvas stages (lawn, world; later siege/battle/delve as
*consumers*). Two kernels: **island runtime** (every canvas stage) and **grid board layer** (cell
stages only — world stays graph Phaser, not `GridSpec`).

Not a new product loop. Not “rebuild world in React” (lock **1a**). Not unpausing siege/battle
inside this program (lock **5a** — base-defense resumes after the **freeze gate** below).

**Owner grain:** full architecture; build alongside; **add → switch-and-delete same commit**; reuse
live modules, blueprint clones. Never two concurrent `Phaser.Game`s. Never two live bodies for the
same duty.

---

## Ownership splits (binding)

| Concern | Owner module | Must not |
|---|---|---|
| DESTROY mutex + `destroyGame` helper | `destroy-game` | Second mutex invent elsewhere |
| Await DESTROY on unmount/remount | `island-host` | Bypass mutex |
| Event maps / `createStageBus` / bus *names* | `stage-bus` | Own select payload *shapes* |
| Select / sync / BoardActorRecord shapes | `board-contract` | Implement `SiegeBoardScene` |
| Production host lifecycle proofs | `host-data-lifecycle` | ScenePOC-only greenwash |

---

## Modules

| Module id | Wave | Responsibility | Dual-track | Depends on | Spec |
|---|---|---|---|---|---|
| `lock-doc-sync` | 0 | **Done T0 2026-09-06.** Stale SSOTs patched: lawn projector shipped; board-render opening honest (lawn+world); DPLP §8 + `game-host/`; canvas budget **96** | Docs only | — | [spec-lock-doc-sync.md](phaser-kernel/spec-lock-doc-sync.md) |
| `destroy-game` | 1 | `src/game/destroyGame.ts` + page DESTROY mutex; lawn then world switch-and-delete | Tests first → switch+delete same commit | — | [spec-destroy-game.md](phaser-kernel/spec-destroy-game.md) |
| `stage-bus` | 1 | `createStageBus<E>()`; keep lawn/world maps; freeze `siegeBus*` / `battleBus*` **names** (stubs OK) | Extract then delete copy-paste twins | — | [spec-stage-bus.md](phaser-kernel/spec-stage-bus.md) |
| `island-host` | 1 | `usePhaserIslandHost` in `src/game-host/`; facades keep island props; await destroy mutex | Hook add → facade switch-and-delete | `destroy-game`, `stage-bus` | [spec-island-host.md](phaser-kernel/spec-island-host.md) |
| `host-data-lifecycle` | 1 | Cutover gate: production Lawn/World hosts pass revision/buffer/ready/foreign-gen suite (ScenePOC may help; not sufficient alone) | N/A (acceptance of hosts) | `island-host` | [spec-host-data-lifecycle.md](phaser-kernel/spec-host-data-lifecycle.md) |
| `board-contract` | 1b | Full §Named unpause import list + thin helpers; prediction-tripwire SC before any lerp; **no** siege scene | Types/helpers only | `board/` on disk; `stage-bus` for bus names | [spec-board-contract.md](phaser-kernel/spec-board-contract.md) |
| `lawn-plane` | 1b | Lawn importGuard; lawn+world generation/`init` hygiene; consume frozen apply name **without** paint flip | Guard/hygiene commits | `island-host`, `board-contract` | [spec-lawn-plane.md](phaser-kernel/spec-lawn-plane.md) |
| `lawn-paint` | 2+ | Graphics adapter **or** owner golden; shadow paint; delete `ensureGrid` in flip commit; no Decision 40 discharge | Shadow then delete | `lawn-plane`, `board-contract` | [spec-lawn-paint.md](phaser-kernel/spec-lawn-paint.md) |
| `focus-input` | 2+ | Focus-gate then optional `wireKeyboardNav` on lawn (GG-18) | Wire after gate | `island-host` | [spec-focus-input.md](phaser-kernel/spec-focus-input.md) |
| `cell-boot` | 2+ | `Boot({ nextScene })` for cell stages; world stays Boot-less (D10) | Replace lawn Boot hardcode | `island-host` | [spec-cell-boot.md](phaser-kernel/spec-cell-boot.md) |
| `fx-facade` | 2+ | Wire or delete unused `FxPool` acquire/release; `FxService` / Audio when audio exists | Wire-or-delete same commit | `island-host` | [spec-fx-facade.md](phaser-kernel/spec-fx-facade.md) |
| `retire-clones` | R | After islands share destroy+host (+ paint when done): delete leftover clone bodies; no permanent façades | Delete-only | `island-host`, `lawn-paint` when paint done | [spec-retire-clones.md](phaser-kernel/spec-retire-clones.md) |

---

## Build order

```text
Wave 0    lock-doc-sync
Wave 1    destroy-game ∥ stage-bus  →  island-host  →  host-data-lifecycle
Wave 1b   board-contract (∥ island-host for types)  →  lawn-plane
Freeze    host-data-lifecycle PASS + board-contract shipped
          → §Unpause import list frozen → base-defense may resume (lock 5a)
Wave 2+   lawn-paint · focus-input · cell-boot · fx-facade
Wave R    retire-clones
```

**Freeze gate (lock 5a):** base-defense must not unpause siege 21.3+ / battle 22.x until
`board-contract` + island Wave 1 modules land and `host-data-lifecycle` passes on **production**
host paths.

---

## Explicitly not in this program

| Out | Owner |
|---|---|
| `SiegeBoardScene` / unpause todos 21.3+ / 22.x | `base-defense` (after freeze) |
| Decision 40 discharge (siege + battle both live) | Proven when those stages ship |
| Converting world map island to React | Forbidden (lock **1a**) |
| One app-lifetime Game for all stages | Rejected vs GG-11 / D2 |
| Dual concurrent Games (world under siege) | Rejected (lock **1a**) |
| Delve room-graph / party-dungeon stage | `party-dungeon` |
| Injector VFX / Funnel / Core combat | Other programs |
| Moving lawn React `features/lawn` → `stages/lawn` | Undecided |
| Relocating wired `createGame.ts` into `island/` | Rejected |
| Create/destroy memory probe | Research later — not a map module |

---

## Locked assumptions

1. FE-only under `web/fusion-rpg-web`.
2. `createGame.ts` stays at `src/game/createGame.ts`.
3. World stage remains Phaser; destroy/recreate on stage leave/enter.
4. Wave 1 does not flip lawn paint (lock **4c**).
5. `host-data-lifecycle` is a hard cutover gate on production hosts.
6. `board-contract` = types + thin helpers; no `SiegeBoardScene`.
7. ScenePOC may extend for harnesses; not sufficient for host cutover alone.
8. No `data/tuning/phaser-kernel.v*.json` in Wave 1.

---

## Anti-patterns (every module forbids)

- “Byte-identical lawn” as a gate / Vitest as paint golden
- “Exactly one Phaser integration … lawn-shaped”
- Claiming Decision 40 discharged
- Writing `SiegeBoardScene` from this program
- Dual concurrent WebGL Games
- Leaving old destroy/host body as a permanent façade

---

## Resolved (2026-09-06 strengthen)

- Map wave skeleton kept; modules thickened for implementability.
- `board-contract` widened to full ideal §Named unpause list.
- `host-data-lifecycle` stays separate (cutover gate, not ScenePOC-only).
- Prediction tripwire is an SC inside `board-contract`, not a separate module.
- `cell-boot` + `retire-clones` added; memory probe left out.
- DESTROY ownership split locked (table above).
