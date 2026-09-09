# Module: `shield-omni-rows`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Depends on:** `/sheet` derived channels · cook families `combat.shield.*`

---

## Objective

Show **omni** shield StatRows (capacity / toughness / pen / regen and related families) on the Shield
tab by joining sheet Derived — not a second private fold and not Ward copy.

## Rules

- Prefer omni column / `expand:none` shield families as listed in cook; element matrix stays on Derived.
- Magnitudes via `formatMagnitude` / six states when channel present.
- Paint accents from element-paint-ssot only when row is element-typed; omni uses neutral/side.

## Success criteria

- [ ] Rows render from sheet channels; Pending when missing.
- [ ] No duplicate god console.
- [ ] Noun Shield only.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run shield
```

## Boundaries

- **Never:** invent shield magnitudes; show full 28×7 matrix here (Derived owns matrix).
