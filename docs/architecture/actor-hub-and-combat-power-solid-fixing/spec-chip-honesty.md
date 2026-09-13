# Spec: `chip-honesty`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-chip · D3 chip copy · finding 7  
**Wave:** 2 (after `standing-compose` for “when may show combat power elsewhere”; chip itself must not wait on Standing to fix the lie)  
**Code anchors:** `foldAptitudesSurfaceVm.ts` `scopeFiction` (~80–85) — ``power ${theta}`` · `AptitudesPage.tsx` StatBar `power ${theta}` · unique GET may lack Θ · aptitude-sheet Done checklist

---

## Objective

Aptitudes **scope chip** (and sibling aptitude chrome that repeats the lie) must **not** label specimen/species **level** or bare ladder index as **“power.”**

Locked fiction:
- Primary: **`Lv {specimenLevel}`** (or species/commander level analogue for that mode)
- Optional ladder: **`Θ {n}`** with that letter — only when Θ is actually on the wire
- **Never** ``power ${n}`` for level or for Θ on this chip
- **Do not** put combat-power Standing number on this chip until product asks — Standing lives on Condition / `copy-surfaces`

Success: unique/species/commander scope subtitles pass tests without the word “power” for level/Θ; old ``power ${theta}`` gone.

---

## Tech stack

- FE: `foldAptitudesSurfaceVm.ts`, AptitudesTab / pieces that render `scopeChip`, `AptitudesPage.tsx` if still live
- Server: optional expose `theta` on unique GET only if chip shows Θ — do not invent fake Θ from level

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- --run foldAptitudesSurfaceVm
npm test -- --run AptitudesTab
```

---

## Project structure

| Path | Duty |
|---|---|
| `foldAptitudesSurfaceVm.ts` `scopeFiction` | `Lv …` / optional `Θ …` |
| Tests | Assert subtitle patterns; reject `/power \d/` |
| `AptitudesPage.tsx` | Same vocabulary if still shown |
| aptitude-sheet checklist | Tick HF-chip Done when green |

---

## Code style

- Copy strings centralized in the fold (one function) — no scattered ``power ${``.

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | unique mode: subtitle contains `Lv`, not `power` |
| Unit | theta present → may show `Θ`, not `power` |
| Unit | theta absent → no fabricated power from level |
| Regression | landmarks / AptitudesTab screenshots or string asserts |

---

## Boundaries

- **Always:** Lv / Θ vocabulary; never level-as-power.
- **Ask first:** Showing O+S+C combat power on the aptitude chip.
- **Never:** `theta ?? specimenLevel` labeled power; claim unique GET has Θ when it does not.

---

## Success criteria

- [ ] `scopeFiction` and aptitude chrome free of level/Θ labeled “power.”
- [ ] Tests lock the new copy.
- [ ] Combat power number remains owned by Condition / `copy-surfaces`.
