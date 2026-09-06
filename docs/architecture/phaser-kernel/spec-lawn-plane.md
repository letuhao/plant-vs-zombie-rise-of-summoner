# Spec: `lawn-plane`

**Module id:** `lawn-plane` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Specify complete 2026-09-06 — Wave 1b.  
**Depends on:** `island-host`, `board-contract`, `host-data-lifecycle` (cutover green) · **Blocks:** `lawn-paint`

**Ideal:** [phaser-kernel-ideal.md](../phaser-kernel-ideal.md) lock **4c**, lawn importGuard, generation hygiene.  
**Pattern:** [world importGuard.test.ts](../../../web/fusion-rpg-web/src/game/world/importGuard.test.ts)

---

## Objective

Harden the lawn (and world generation hygiene) **without** flipping paint: Phaser stays on `lawn:*`
only; React owns HTTP / icon epoch; Scene restart bugs fixed; frozen `applyLiveBoard` name may be
wired for live sync. Terrain paint itself is owned by **`lawn-paint`** (`paintLawnTerrainGraphics`
via `BoardLayers`); `ensureGrid` was deleted in that flip (lock **4c**).

**Success:** lawn importGuard green; world generation set in `init` (or equivalent restart-safe path);
lawn registry not stuck on constructor-only state across restart; live terrain path is
`paintLawnTerrainGraphics` after `lawn-paint` (not `ensureGrid`).

---

## What already exists (verified)

**Built:** world `importGuard.test.ts`; lawn still imports `@/lib/bus` from Phaser paths
(`LawnWorldScene`, `iconUrl`, `SyncFromModelSystem` — audit); lawn `PtrEntityRegistry` as class field
(`LawnWorldScene`); world generation set in `create` not `init` (`WorldMapScene`).

**Gap:** lawn plane guard; restart hygiene; apply helper unused.

---

## Contract

### 1. Lawn importGuard

Add `src/game/**` lawn-side guard (mirror world):

| Forbidden in `src/game/` (lawn trees) | Allowed |
|---|---|
| `@/lib/bus` HTTP / icon epoch fetches | `lawn:*` EventBus only |
| `@/features/lawn` React modules from Phaser systems (tighten toward world strictness) | View-model types if already allowed by world-map copy law — **document** |

Move icon epoch ownership to React → emit on bus if needed.

### 2. Generation / restart hygiene

| Bug | Fix |
|---|---|
| Lawn registry held only on Scene constructor | Reset in `init` / destroy path so restart without full Game destroy is safe **or** document that Game destroy is the only restart (and test that path) |
| World generation set in `create` | Set in `init(data)` from registry/preBoot (match lawn) |

### 3. Apply helper (no paint flip)

If live sync is rewired, call **`applyLiveBoard`** (name from `board-contract`) rather than growing
`SyncFromModelSystem` as the siege template. Deleting `ensureGrid` is **`lawn-paint`** work
(`paintLawnTerrainGraphics` + `BoardLayers`) — not this module.

### Folder law

Phaser under `src/game/`; React under features/stages/game-host. No React in `game/`.

---

## Dual-track

ImportGuard and hygiene are additive. If SyncFromModel body moves behind `applyLiveBoard`, 
switch-and-delete the old call sites **same commit** — do not leave two live sync bodies.

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- src/game/world/importGuard.test.ts
npm test -- src/game/**/importGuard*.ts   # once lawn guard exists
npm test -- src/game/scenes/ src/game/world/scenes/
```

---

## Testing strategy

- ImportGuard fails on planted `@/lib/bus` import under `game/`.
- Restart/init tests for generation and registry reset (jsdom Scene mocks).
- `host-data-lifecycle` already green before claiming this module done.

---

## Tunables

None.

---

## Boundaries

- **Always:** importGuard; init/generation hygiene; paint flip belongs to `lawn-paint`, not here.
- **Ask first:** tightening lawn copy-law beyond world’s (features imports).
- **Never:** paint golden here; Decision 40 discharge; siege scene; dual sync paths left live.

---

## Success criteria

1. Lawn Phaser tree has an importGuard test equivalent in spirit to world’s.
2. World generation assigned in `init` (or test proves restart-safe equivalent).
3. Lawn registry/restart hygiene addressed with a test.
4. Live lawn terrain path is `paintLawnTerrainGraphics` (`ensureGrid` deleted by `lawn-paint`, lock 4c).
5. `host-data-lifecycle` suite green (dependency).
6. No claim that Decision 40 is discharged.

---

## Out of module

`lawn-paint` golden; `focus-input`; FxPool; `SiegeBoardScene`.
