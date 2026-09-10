# Spec: `posture-theme-packs`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal:** **A2** three posture packs + VFX  
**Parent:** [../gui-lego/spec-theme-packs.md](../gui-lego/spec-theme-packs.md) · [../gui-lego/spec-theme-bind.md](../gui-lego/spec-theme-bind.md)

---

## Objective

Ship three theme packs — Force / Finesse / Bastion — each with `css` + `paint` hex + **non-null**
`vfx.select`, so posture bands and tiles are not mute bordered boxes.

`bucket.aptitude` remains for Derived source buckets only — do not overload it as posture chrome.

---

## Commands

```powershell
# FE theme registry tests
npm test -- --run theme
```

---

## Project structure

| Path | Duty |
|---|---|
| `web/.../themes/packs/posture-force.json` (names flexible) | Pack files |
| Same for finesse / bastion | |
| Theme registry | Register `kind: "posture", id: "force"|"finesse"|"bastion"` |

---

## Contract

| Slot | Required |
|---|---|
| `css` | Band/tile chrome classes or tokens |
| `paint` | Hex for SVG/icon accents |
| `vfx.select` | Non-null select feedback (motion/css allowed; no engine jargon) |

Player titles: Force / Finesse / Bastion — **never** raw `force` as product chrome (fold supplies displayName).

---

## Boundaries

- **Always:** Theme packs own paint; pieces declare `themeRef`.
- **Never:** Hard-coded posture colors inside tile TSX as SSOT.

---

## Success criteria

- [ ] Three packs registered and bound from posture-band / aptitude-tile.
- [ ] `vfx.select` non-null on each pack.
- [ ] Derived `bucket.aptitude` unchanged in duty.
