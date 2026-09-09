# Module: `condition-layout`

**Program:** `condition-glance` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**CSS:** `web/fusion-rpg-web/src/ui/gui-lego/conditionConsole.css`  
**Depends on:** host pieces (`actor-identity`, `cond-hero`, `stand-row`, gauges) ·
[spec-standing-radar.md](../gui-lego/spec-standing-radar.md) · [spec-stand-row.md](../gui-lego/spec-stand-row.md)

---

## Objective

2×2 Condition surface layout that keeps vitality and Standing on correct rows, gives Standing
column enough width for **recharts** radar, and eliminates surplus horizontal scroll.

---

## Landmark contract

| Area | Piece | Rules |
|---|---|---|
| `prog` | progression-gauge | |
| `identity` | actor-identity | |
| `hero` | cond-hero | radial fixed **128×128**; pool col may scroll **internally** only |
| `stand` | stand-row | **min-width ≥ recharts radar chart width** |

### Width defect to kill

Today: stand column ~**140px** vs radar SVG/chart ~**150px** → clip + surplus scrollbar.
**Contract:** `.stand` / stand-row column `min-width` ≥ chart outer width (use **160px** minimum or
`minmax(160px, 1fr)` — chart may not exceed column). No page-level horizontal scroll from Standing.

### Overflow

- Remove default `.live-col { overflow: auto }` when content fits.
- Internal scroll only when bars+strip truly exceed column height — never as a width bandage.

---

## Success criteria

- [ ] `grid-template-areas`: `"prog identity" / "hero stand"` intact; landmark tests on `data-grid-area`.
- [ ] Stand column min-width ≥ radar chart; no 140-vs-150 clip.
- [ ] No abundant horizontal scrollbar on Standing at desktop width with large Standing ints.
- [ ] Radial box 128×128 not stolen by flex.
- [ ] ≤900px stacking documented and tested (areas reflow intentionally).

---

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run condition
# Landmark / grid-area contract tests (add if missing):
npm test -- --run conditionConsole
# Visual: ActorSheet Condition at desktop — no horizontal scrollbar on Standing
```

## Testing

- DOM contract tests: `>` parents / `data-grid-area` (gui-lego style).
- Cross-check stand-row + standing-radar width golden.

## Boundaries

- **Never:** “hide scrollbar” as the fix; fix width.
- **Always:** architecture layout module — not a one-off margin nudge in ConditionTab.
