# Spec: `retire-clones` (deferred)

**Module id:** `retire-clones` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Deferred — Wave R (after islands share kernel).  
**Depends on:** `island-host`; `lawn-paint` when paint wave done (if paint still pending, retire
destroy/host clones only)

---

## Objective

Delete leftover destroy/host clone bodies after both lawn and world use `destroyGame` +
`usePhaserIslandHost`. A permanent façade is a defect.

---

## Contract

- Grep for duplicate destroy checklists / mount skeletons.
- Delete dead exports; keep thin named facades (`createLawnGame`, `LawnGameHost`) that call kernel.
- Do not relocate `createGame.ts` into an `island/` folder (rejected scatter).

---

## Success criteria (when activated)

1. No second live destroy checklist body in lawn/world facades.
2. No second mount skeleton outside `usePhaserIslandHost`.
3. Facades remain one-liners / thin props wrappers — not re-implementations.

---

## Boundaries

- **Always:** delete clones after switch.
- **Never:** leave “compat” dead copies; invent `src/game/island/createGame`.
