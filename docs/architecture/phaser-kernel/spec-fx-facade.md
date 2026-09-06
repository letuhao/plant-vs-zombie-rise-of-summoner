# Spec: `fx-facade` (deferred)

**Module id:** `fx-facade` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Deferred — Wave 2+.  
**Depends on:** `island-host`

---

## Objective

End the half-built `FxPool`: either wire `acquireRing` / `release` through a scene-local
`FxService(scene)`, or **delete** unused pool APIs in the same commit. Audio Director only when
audio assets exist. Never merge with injector `VfxCatalog`.

---

## Contract (frozen for later)

| Decision | When |
|---|---|
| **Wire** | At least one production caller uses acquire/release |
| **Delete** | Zero callers remain after StatusFx stop voiding unused API |

- Pools are presentation structural (`maxSize` + comment) — not Core `vfx.v{n}.json`.
- Scene-local; no module singleton tween handles across destroy.

---

## Success criteria (when activated)

1. No “constructed but never acquire” dead API left without a comment pointing to delete ticket.
2. Wire-or-delete completed in one commit shape.
3. Injector VFX untouched.

---

## Boundaries

- **Never:** share Unity VFX ids with Phaser FX; permanent façade “pool for later” with zero callers.
