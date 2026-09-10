# Spec: `shield-tab`

**Module id:** `shield-tab` · **Program:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Parent map:** [../actor-sheet-map.md](../actor-sheet-map.md)  
**Depends on:** `actor-sheet-shell`, `shield-recipe-wire`, `shield-stack-projection`  
**Ideal:** [../shield-sheet-ideal.md](../shield-sheet-ideal.md)  
**Design SSOT:** [../../design/spec-shield-and-elements.md](../../design/spec-shield-and-elements.md) §3.1  
**Locks:** **S1** `sheet.shieldLayers` · **S2** · **D8**  
**Status:** Spec rewritten 2026-09-10 — full Lego + Hot; chrome-only Empty wells superseded.

---

## Assumptions

1. Noun is **Shield**, never Ward.
2. Max three layers — drain order is reading order.
3. **Visual grammar:** one **segmented** stack bar (design §3.1).
4. Layers from `sheet.shieldLayers` (**S1**); summary from shared hot-projection.
5. Element matrix stays on **Derived**; this tab is instances + omni rows.
6. Surface = recipe + fold + bus — thin host only.

---

## Objective

Player-readable shield stack: segmented bar + layer inspect + omni shield StatRows.

**Success:** Hot layers match runtime; cold/pending not faked as empty stack; paint from
element-paint-ssot; totals agree with Condition `shield-status`.

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/ShieldTab.tsx     # thin host — replace CatalogTabs Empty wells
docs/design/gui-lego/recipes/shield-console.json
docs/design/gui-lego/pieces/shield-stack-bar.html
npm test -- --run ShieldTab foldShield
```

Charts/fills: **recharts** or locked kit with **paint hex**. Magnitudes `long`.

---

## Design

- Stack: `shield-stack-bar`.
- Inspect: `shield-layer-inspect`.
- Omni: `shield-omni-rows`.
- F9 lawn mute is HUD-only.
- Empty wells only when Hot and count &lt; 3.

---

## Tunables

Existing shield tuning. No new power curve.

---

## Testing / Boundaries / Success

- Unit: pending vs 0–3 layers; Ward absent; summary≈layer sums.
- Always: catalog/pack colors; RPG-layer only.
- Never: FE invent stacks; recompute absorb; god TSX Empty wells as “done.”
- Success: design §3.1 + Hot proof with Condition glance.

## Open Questions

**Resolved 2026-09-10:** Transport = **S1** `sheet.shieldLayers`. Three radials superseded. Tab **in** via `shield-sheet`.
