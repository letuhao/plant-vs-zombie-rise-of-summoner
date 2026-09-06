# Spec: `cell-boot` (deferred)

**Module id:** `cell-boot` · **Program:** [phaser-kernel-map.md](../phaser-kernel-map.md)  
**Status:** Deferred — Wave 2+.  
**Depends on:** `island-host`

---

## Objective

Replace lawn-hardcoded `BootScene` → `LawnWorldScene` handoff with `Boot({ nextScene })` for **cell**
stages (lawn, later siege/battle). World stays **Boot-less** until D10 art revisit.

---

## Contract (frozen for later)

```ts
// Conceptual
Boot({ nextScene: "LawnWorldScene" | string })
```

- World: framed placeholders in scene `create` (D10) — no Boot required.
- Do not force world onto Boot.

---

## Success criteria (when activated)

1. Lawn Boot no longer hardcodes only `LawnWorldScene` string without injection.
2. Cell stages can pass next scene key.
3. World MapScene still boots without BootScene unless D10 reopened.

---

## Boundaries

- **Never:** require Boot for world in this module’s default path.
