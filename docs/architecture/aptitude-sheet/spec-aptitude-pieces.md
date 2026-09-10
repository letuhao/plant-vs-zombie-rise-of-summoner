# Spec: `aptitude-pieces`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal:** piece catalog · HTML draft fidelity · **D6/D7** · **D12/E4** · `preset-entry`  
**Reuse:** [../gui-lego/spec-chip.md](../gui-lego/spec-chip.md) · [../gui-lego/spec-split-inspect.md](../gui-lego/spec-split-inspect.md) ·
[../gui-lego/spec-inspect-pane.md](../gui-lego/spec-inspect-pane.md) · [../gui-lego/spec-source-list.md](../gui-lego/spec-source-list.md)  
**Depends on:** `catalog-icons` · `posture-theme-packs`

---

## Objective

Define player-facing piece contracts for the aptitude console **and** preset console chart/entry.
Each piece has one design job. Author HTML drafts under `docs/design/gui-lego/pieces/` before React
factories.

---

## Commands

```powershell
# After drafts
# owner visual accept of HTML pieces
npm test -- --run aptitude
```

---

## Project structure

| Path | Duty |
|---|---|
| `docs/design/gui-lego/pieces/aptitude-*.html` | Console drafts |
| `docs/design/gui-lego/pieces/preset-*.html` | Preset entry / donut drafts |
| `web/.../pieces/` or feature factories | React after accept |
| Promote `LeftoverBar` → `leftover-gauge` | Keep recharts; **also** mount in console hero (D6) |

---

## Pieces

### `aptitude-scope-chip`

| | |
|---|---|
| **Job** | Seal for active binding (specimen / species / commander) |
| **Payload** | `title`, optional `subtitle`, optional `commanderAddOn` (Mode A A5b), `themeRef` |
| **Never** | Raw scope enum strings as only label |

### `leftover-gauge`

| | |
|---|---|
| **Job** | **Remaining points** in console **hero strip** (D6) — fiction copy, not footer-only |
| **Payload** | `spent`, `budget`, `leftover` (`long`), `revision`, overspend flag for pulse |
| **Lib** | recharts (existing LeftoverBar) |
| **Note** | Shell footer may mirror Confirm; leftover must still appear in-console |

### `allocate-decision-strip`

| | |
|---|---|
| **Job** | In-console **Confirm** + **Cancel** (D7/S8); Cancel = discard draft / revert |
| **Payload** | `dirty`, `withinBudget`, `saving`, disabled reasons; Mode B may show price chip slot |
| **Confirm scope (E1)** | Commits **draft** (tiles / Auto-assign / Apply-to-draft) — **not** required after Activate |
| **Fiction** | Button label **Cancel** (not Reset) — bus remains `aptitude.reset` (S8) |
| **Never** | Nest band-3 ConfirmDialog (GG-63/S2); be the only leftover home |

### `preset-entry`

| | |
|---|---|
| **Job** | Opens build-preset console; shows active preset name when bound |
| **Payload** | `activePresetName?`, `label` (“Build presets…”) |
| **Bus** | `preset.open` |

### `posture-band`

| | |
|---|---|
| **Job** | Chrome + title for Force / Finesse / Bastion; hosts four tiles |
| **Payload** | `postureId`, `displayName`, `themeRef: { kind: "posture", id }`, child tile slot |

### `aptitude-tile`

| | |
|---|---|
| **Job** | Icon + displayName + value + **obvious** steppers; selected state |
| **Payload** | `aptitudeId`, `displayName`, `icon`, `value`, `selected`, `themeRef`, stepper intents via bus |
| **Never** | Fetch; own paint hex; mute steppers that look disabled when legal |

### `aptitude-inspect`

| | |
|---|---|
| **Job** | Dossier: reading, role, fed family **displayNames** (A3) |
| **Payload** | Selected aptitude fiction + `fedFamilies: { displayName }[]` |
| **Reuse** | `inspect-pane` / `source-list` grammar |
| **Never** | Channel ids as product chrome |

### `preset-distribution-chart`

| | |
|---|---|
| **Job** | Live **recharts donut** of twelve aptitudes as % (D12/E4) |
| **Payload** | Segments `{ aptitudeId, displayName, permille|points, postureId }`, optional `leftover`, `revision`, budget context |
| **Colors** | Posture theme packs as segment groups |
| **Optional** | Small posture stacked bar companion — **not** peer pie/radar |
| **Empty** | Honest empty state — not fake equal slices |

### `species-build-chrome` (Mode B only)

| | |
|---|---|
| **Job** | Baseline vs override honesty; respec **price on strip** before Confirm **and** Activate; free revert |
| **Payload** | `hasOverride`, `baselineShares` (display), `priceAmount`, `priceResource`, `everRespecced`, free flags |
| **Never** | Band-3 ConfirmDialog for priced Confirm (S2) — price lives here + decision-strip |

### `aptitudes-layout`

| | |
|---|---|
| **Job** | Landmarks / grid; no surplus scroll |
| **Payload** | Structural regions only |

**Deferred:** `posture-balance` (A6).

---

## Code style

Pieces are pure presenters — no fetch, no draft ownership. Host/fold owns draft; bus events for
stepper / select / confirm / auto-assign / preset.open.

---

## Testing strategy

- Landmark / role tests per piece when React lands.
- HTML draft side-by-side accept before factory.
- leftover-gauge present in console fixtures for A/B/C.
- Donut updates on revision; leftover segment/readout when preview leftover > 0.

---

## Boundaries

- **Always:** HTML draft before React; themeRef for paint; leftover in-band (D6).
- **Ask first:** Second chart library overlapping recharts.
- **Never:** God piece that fetches allocate APIs; pie/radar as primary chart grammar.

---

## Success criteria

- [ ] All Wave-1 + Wave-3 pieces have HTML drafts + payload contracts.
- [ ] Inspect shows fed family displayNames (A3).
- [ ] leftover-gauge + decision-strip + preset-entry + donut contracts land.
- [ ] Species chrome covers price-before-confirm **and** price-before-Activate **without** ConfirmDialog (S2).
- [ ] Decision-strip fiction uses **Cancel** (S8).
