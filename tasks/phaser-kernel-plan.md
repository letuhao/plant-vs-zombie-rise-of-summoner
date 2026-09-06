# Implementation plan: phaser-kernel

**Program:** `phaser-kernel`  
**Map:** [docs/architecture/phaser-kernel-map.md](../docs/architecture/phaser-kernel-map.md)  
**Specs:** [docs/architecture/phaser-kernel/](../docs/architecture/phaser-kernel/)  
**Ideal:** [docs/architecture/phaser-kernel-ideal.md](../docs/architecture/phaser-kernel-ideal.md)  
**Tasks:** [phaser-kernel-todo.md](phaser-kernel-todo.md)

**Paths written:** `tasks/phaser-kernel-plan.md` · `tasks/phaser-kernel-todo.md`.  
Never `tasks/plan.md` / `tasks/todo.md`.

**Status:** **Implement complete 2026-09-06** — T0–T12 + CPF + E2E/visual proof.
Wave 0–1b freeze landed; Wave 2+/R shipped same session (Graphics-compatible lawn paint).
Next external: base-defense may unpause siege/battle against frozen import list.

---

## 1. What this plans

**Plans:** every module in the approved map — docs sync, `destroyGame` + mutex, `createStageBus`,
`usePhaserIslandHost`, production host lifecycle suite, full `board-contract` freeze list,
`lawn-plane` hygiene, then deferred paint / focus / boot / fx / retire.

**Does not plan:** `SiegeBoardScene` / unpausing base-defense 21.3+ / 22.x (lock **5a** — resume is
base-defense’s after Freeze checkpoint); Decision 40 discharge; world→React; dual Game; delve;
relocating `createGame.ts`; injector VFX; Core/Data.

**Does not restate specs.** Contracts stay in `docs/architecture/phaser-kernel/spec-*.md`. This file
is order, vertical slices, checkpoints, risks, and defaults.

---

## 2. Dependency graph

```text
T0 lock-doc-sync (docs)
        │
   ┌────┴────┐
   ▼         ▼
T1 destroy-game     T2 stage-bus          (parallel after T0)
   │         │
   └────┬────┘
        ▼
T3 island-host (hook + lawn facade switch)
        │
        ├──────────────────┐
        ▼                  ▼
T4 world facade      T5 host-data-lifecycle
   on island-host         (cutover suite)
        │                  │
        └────────┬─────────┘
                 │
T6 board-contract (∥ after T2; types OK ∥ T3–T5)
                 │
                 ▼
T7 lawn-plane (needs T5 green + T6)
                 │
          *** CPF Freeze ***
                 │
     base-defense may unpause (not this program)
                 │
        ┌────────┼────────┬────────┐
        ▼        ▼        ▼        ▼
   T8 paint  T9 focus  T10 boot  T11 fx     (Wave 2+ deferred)
        │
        ▼
   T12 retire-clones (Wave R)
```

---

## 3. Vertical slicing

| Phase | Vertical outcome |
|---|---|
| **0 Docs** | Stale SSOTs no longer contradict HEAD |
| **1a Island destroy+bus** | Shared destroy + bus factory exist; lawn/world destroy switch-and-delete |
| **1b Island host** | Both facades use `usePhaserIslandHost`; GG-11 green |
| **1c Lifecycle** | Production host suite proves buffer/ready/foreign-gen |
| **1d Board freeze** | Full unpause import list importable; tripwire rewritten before any lerp |
| **1e Lawn plane** | ImportGuard + init hygiene; `ensureGrid` still live |
| **Freeze** | Checkpoint CPF — base-defense unpause allowed (external) |
| **2+ / R** | Paint (default Graphics-compatible), focus, Boot, Fx, retire |

---

## 4. Dual-track rule (every code task)

1. **Add** helper + tests (may be zero production importers).  
2. **Switch-and-delete** in the **same** commit: caller points at helper; old body deleted.  
3. Never two live bodies; never two concurrent `Phaser.Game`s.

---

## 5. Gates vs checkpoints

| Kind | Item | Notes |
|---|---|---|
| **Checkpoint** | CP0 after T0; CP1 after T1–T2; CP2 after T3–T4; CP3 after T5; CPF after T6–T7 | Review work already done |
| **Not a hard gate** | “Owner must re-approve specs before coding” | Map approved; specs Specify-complete — build proceeds |
| **Not a hard gate** | Paint Graphics vs golden | Lock **4c** defers Wave 2; **default when T8 starts:** Graphics-compatible adapter (reversible). Owner golden is a follow-up if they reject the look |
| **Follow-up (non-blocking)** | Tell base-defense freeze landed | After CPF; do not stall T8+ |

No irreversible pre-work halt in this plan.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| DESTROY race leaves two WebGL contexts | T1 mutex + T3 await; unit tests with deferred destroy |
| ScenePOC greens while hosts regress | T5 requires Lawn+World production paths |
| `board-contract` stubs forever | Freeze lists names; T7 may wire `applyLiveBoard` without paint flip; siege owns real board later |
| Lawn importGuard breaks icon epoch | Move epoch to React→bus in T7; mirror world guard pattern |
| Bundle pull Phaser into entry | GG-38: only lazy hosts import create*; `check:bundle` at CP2 |

---

## 7. Commands (repo)

```powershell
cd web/fusion-rpg-web
npm test -- <paths from task>
npm run build
# optional after host work:
npx playwright test e2e/phaser-scene-poc.spec.ts --project=chromium
```

---

## 8. Locked decisions (do not reopen in tasks)

1a world stays Phaser / one Game · 2a lawn adapter OK later / D40 undischarged · 3a `src/game-host/` ·
4c paint deferred · 5a base-defense unpauses after freeze · `createGame.ts` stays put · no
`SiegeBoardScene` in this program.

---

## 9. Task index

See [phaser-kernel-todo.md](phaser-kernel-todo.md): **T0–T12** + checkpoints CP0–CP3 + **CPF**.
