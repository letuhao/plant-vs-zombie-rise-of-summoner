# Spec: `stage-bus`

**Module id:** `stage-bus` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1.  
**Depends on:** `lock-doc-sync` · **Blocks:** `island-host`, `board-contract` (bus names)

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) §Named unpause Island / Dataflow.  
**Code:** [EventBus.ts](../../../web/fusion-rpg-web/src/game/EventBus.ts)

---

## Objective

Stop copy-pasting EventBus maps. One factory builds typed `{ on, emit, clearAll }` buses; lawn and
world keep their event unions; **names** for `siege:*` / `battle:*` are frozen so base-defense can
import without inventing a third map by feel.

**Success:** `createStageBus` exists; lawn/world use it (or thin wrappers); siege/battle bus **symbols
or factories** are named even if stubbed; `allocGameGeneration` stays shared.

---

## What already exists (verified)

**Built:** dual maps in `EventBus.ts` (`lawn:*`, `world:*`), `allocGameGeneration`, generation on
payloads.

**Gap:** adding `siege:*` today means a third hand-copied map; no generic factory.

---

## Contract

### API

```ts
export type StageBus<E extends string> = {
  on: (event: E, handler: (payload: unknown) => void) => () => void;
  emit: (event: E, payload: unknown) => void;
  clearAll: () => void;
};

export function createStageBus<E extends string>(): StageBus<E>;

// Keep shared:
export function allocGameGeneration(): number;
```

### Required event stems (each stage)

For every canvas stage bus, the following stems exist and payloads carry `generation: number`:

`model` · `select` · `interaction` · `ready` · `resized` · `destroyed`

Lawn today may use additional events (icon epoch, etc.) — those stay lawn-only and must move toward
React ownership under `lawn-plane` (not this module’s delete duty).

### Frozen names for consumers (lock 5a)

| Export (name freeze) | Status in this module |
|---|---|
| `lawnBusOn` / `lawnBusEmit` (or equivalent wrappers) | Keep working |
| `worldBusOn` / `worldBusEmit` | Keep working |
| `siegeBusOn` / `siegeBusEmit` (or `createSiegeBus`) | **Name + stub OK** — no scene |
| `battleBusOn` / `battleBusEmit` (or `createBattleBus`) | **Name + stub OK** — no scene |

Select **payload shapes** are owned by `board-contract`, not redefined here.

### Folder / import law

| May | Must not |
|---|---|
| Stay in `src/game/EventBus.ts` or `src/game/stageBus.ts` | Import React / `lib/bus` |
| Phaser-free | Own HTTP |

---

## Dual-track

1. Add `createStageBus` + unit tests.
2. Rewire lawn then world maps to the factory; delete duplicated private map machinery **same
   commits** (no permanent twin emitters).

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game/EventBus.test.ts src/game/stageBus.test.ts
```

---

## Testing strategy

- Factory: on/emit/clearAll; handlers removed after clear.
- Generation allocator monotonic.
- Import/name test: siege/battle bus symbols resolve (even if emit is no-op stub).

---

## Tunables

None.

---

## Boundaries

- **Always:** every required stem carries `generation`; shared `allocGameGeneration`.
- **Ask first:** renaming public `lawnBus*` / `worldBus*` (breaks hosts/e2e).
- **Never:** Phaser predicts; bus owns Intent HTTP; implement siege scene here.

---

## Success criteria

1. `createStageBus` is the single map implementation path for new stages.
2. Lawn and world continue to compile and their existing bus tests pass.
3. `siegeBus*` and `battleBus*` names exist for import from `@/game/…` without opening lawn scenes.
4. No third hand-rolled `Map` copy for siege appears in Wave 1 without going through the factory.

---

## Out of module

`usePhaserIslandHost`; `BoardActorRecord`; destroy mutex; paint.
