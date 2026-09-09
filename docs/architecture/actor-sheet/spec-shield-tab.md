# Spec: `shield-tab`

**Module id:** `shield-tab` · **Program:** [../shield-sheet-map.md](../shield-sheet-map.md) (surface host)  
**Parent map (sheet):** [../actor-sheet-map.md](../actor-sheet-map.md)  
**Depends on:** `actor-sheet-shell`, `shield-recipe-wire`, `shield-stack-projection`  
**Ideal:** [../shield-sheet-ideal.md](../shield-sheet-ideal.md)  
**Design SSOT:** [../../design/spec-shield-and-elements.md](../../design/spec-shield-and-elements.md) §3.1  
**Status:** Spec rewritten 2026-09-10 — full Lego + Hot; prior chrome-only draft superseded.

---

## Assumptions

1. Noun is **Shield**, never Ward.
2. Max three layers — `ShieldPolicy`; drain order is reading order.
3. **Visual grammar:** one **segmented** stack bar (design §3.1), not three permanent empty radials.
4. Hot layers from `shield-stack-projection`; summary from shared `sheet-hot-projection`.
5. Full element×family shield matrix stays on **Derived**; this tab is instances + omni rows.
6. Surface = recipe + fold + bus — thin host only.

---

## Objective

Player-readable shield stack: segmented bar + layer inspect + omni shield StatRows, Hot when live,
honest Pending when unwired.

**Success:** Hot layers match runtime; cold/pending not faked as empty stack; paint from
element-paint-ssot; totals agree with Condition `shield-status`.

---

## Tech Stack / Commands / Structure

```text
web/.../ui/actor/ShieldTab.tsx     # thin host
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
- F9 lawn mute is HUD-only — sheet shows numbers when data exists.
- Empty wells only when Hot and count &lt; 3.

---

## Tunables

Shield max / priorities — existing shield tuning. No new power curve.

---

## Testing / Boundaries / Success

- Unit: pending vs 0–3 layers; Ward string absent; summary≈layer sums.
- Always: catalog/pack colors; RPG-layer only.
- Never: FE invent stacks; recompute absorb remainder; god TSX empty wells as “done.”
- Success: matches design §3.1 + Hot proof with Condition glance.

---

## Open Questions (resolved here)

| Was | Now |
|---|---|
| Server shields GET shape | `shield-stack-projection` Option A or B |
| Three radials | **Superseded** by segmented bar |
| Tab out of condition-glance | **In** — program `shield-sheet` |
