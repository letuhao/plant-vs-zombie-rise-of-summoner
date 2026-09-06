# Spec: `destroy-game`

**Module id:** `destroy-game` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1; build authorized after map+spec owner review via `/plan`.  
**Depends on:** `lock-doc-sync` · **Blocks:** `island-host`

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) §Named unpause / §Dual-track Wave 1.  
**POC:** [destroyGameWhenReady](../../web/fusion-rpg-web/src/game/poc/scene-switch/destroyGameWhenReady.ts) is a
blueprint — production helper must own the **page mutex** and stage bus emit.

---

## Objective

One destroy path for every canvas island: ordered teardown, `noReturn: false`, and a page-level
mutex so the next `createGame` cannot race a dying WebGL context.

**Success:** lawn then world destroy through `destroyGame`; old inline destroy bodies deleted in the
switch commits; mutex unit-tested.

---

## What already exists (verified)

**Built:**

- Order cloned in [`createLawnGame.ts`](../../../web/fusion-rpg-web/src/game/createLawnGame.ts)
  `destroyLawnGame` (tweens → shutdown → `lawn:destroyed` → `destroy(true)`).
- Same shape in [`createWorldGame.ts`](../../../web/fusion-rpg-web/src/game/createWorldGame.ts).
- POC [`destroyGameWhenReady`](../../../web/fusion-rpg-web/src/game/poc/scene-switch/destroyGameWhenReady.ts)
  waits for `destroy` event (no page mutex).

**Gap:** no shared helper; hosts null refs while destroy is next-frame; no refuse-next-create mutex.

---

## Contract

### API

```ts
// web/fusion-rpg-web/src/game/destroyGame.ts
export type DestroyGameOptions = {
  game: Phaser.Game | null;
  sceneKey: string;
  shutdown?: () => void;
  emitDestroyed: (generation: number) => void;
  generation: number;
};

/** Resolves after Phaser DESTROY (or equivalent). noReturn must stay false. */
export function destroyGame(opts: DestroyGameOptions): Promise<void>;

/** Page mutex: createGame paths must await/check before constructing. */
export function assertDestroySettled(): void; // throws if mutex held
export function destroyMutexPending(): boolean;
```

**Order (binding):** kill tweens on active scenes → optional `shutdown()` → `emitDestroyed(generation)`
→ `game.destroy(true)` with default `noReturn` (false) → hold mutex until `destroy` event → release.

**Ownership:** this module owns the mutex. `island-host` awaits it; it must not invent a second one.

### Folder / import law

| May | Must not |
|---|---|
| `src/game/destroyGame.ts` (+ tests) | React imports |
| Called from `createLawnGame` / `createWorldGame` / hosts | `noReturn: true` while another stage Game can follow |

---

## Dual-track commit shape

1. **Add** `destroyGame.ts` + mutex tests — **zero** production importers.
2. **Switch-and-delete (lawn):** `destroyLawnGame` body becomes a one-liner calling `destroyGame`;
   old inline order **deleted in the same commit**.
3. **Switch-and-delete (world):** same for world destroy helper.

Forbidden: “tests land but old bodies stay as permanent façades.”

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game/destroyGame.test.ts
npm test -- src/game/createLawnGame.test.ts src/game/createWorldGame.test.ts
```

---

## Testing strategy

- Fake `Phaser.Game` with deferred `destroy` event: second create refused while pending; allowed after.
- Order spy: tweens → shutdown → emit → destroy(true).
- Source scan after switch commits: no duplicate destroy checklist bodies in lawn/world facades.

---

## Tunables

| Item | Class |
|---|---|
| Mutex safety timeout (if any) | Structural — named const + comment; throw/log, never silent clamp of gameplay |

---

## Boundaries

- **Always:** switch-and-delete same commit; `noReturn: false`; emit generation on destroyed.
- **Ask first:** changing destroy order; adding `noReturn: true` anywhere.
- **Never:** two concurrent Games; leave old destroy body as dead export; React in `game/`.

---

## Success criteria

1. `destroyGame` implements the order above and resolves only after DESTROY (or tested equivalent).
2. Mutex refuses overlapping create while pending (unit test).
3. Lawn production destroy goes through `destroyGame`; old body deleted same commit.
4. World production destroy goes through `destroyGame`; old body deleted same commit.
5. GG-11 stage tests still pass (panel open does not destroy Game).

---

## Out of module

Host hook; bus extract; paint; `SiegeBoardScene`; Decision 40.
