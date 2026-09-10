# Spec: `aptitude-preset-console`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D9** · **D12** · **D14** · **E1** · **E3** · **E4** · **E5** · **A13–A17** · **S1/S3/S7**  
**Depends on:** `aptitude-preset-api` · `aptitude-pieces` (donut, preset-entry) · hosts for commit  
**Depth:** stage → sheet/layer → preset console ≤ **3** (GG-10) — nested layer, not `/presets` route

---

## Objective

Own nested **Build presets** console: gallery, editor with dual abs/% constraints, **recharts donut**,
Select / Apply to draft / **Activate** (via transactional API). Species favour is New-preset seed and
gallery system entry — not Hub fill (**E3**).

---

## Tech stack

- React nested layer over ActorSheet / AptitudesLayer (GG-1)
- Recipe `aptitude-preset-console` + fold; pieces never fetch
- **recharts donut** primary chart (**E4**); optional small posture stacked bar only
- Host Activate → **`POST /api/aptitude-presets/activate`** only (**S3**)

---

## Commands

```powershell
npm test -- --run aptitude-preset
# owner visual accept of HTML: docs/design/gui-lego/surfaces/aptitude-preset-console.html
```

---

## Project structure

| Path | Duty |
|---|---|
| `docs/design/gui-lego/recipes/aptitude-preset-console.json` | Slots |
| `docs/design/gui-lego/pieces/preset-*.html` | Gallery, editor, donut drafts |
| FE fold + RecipeMount | Nested host opened by `preset.open` |
| Host Activate handler | `POST .../activate` (S3) — not set-active + allocate client sequence |

---

## Regions

| Region | Job |
|---|---|
| **Gallery** | Player + system + favour seed; name; posture summary; active badge |
| **Editor** | Name; 12 rows (target ‰ + optional min/max abs & ‰); Save blocked unless sum ‰ = 1000 (E5) |
| **Chart** | `preset-distribution-chart` donut; leftover twin on budget preview (E2) |
| **Actions** | Select · Apply to draft · Activate · Save / Delete · Close |

**New preset default (D14 / S1 / S7):**

| Mode | Seed |
|---|---|
| A/B with `speciesId` | Favour GET **`sharesPermille`** — if `{}`, refuse favour seed and offer **Even** |
| C / no species | Even |

Copy-on-edit into player library — never mutate generated plan. Never treat species baseline points
as template ‰.

---

## Verb split (E1 / S3)

| Gesture | Effect |
|---|---|
| **Apply to draft** | Materialize into aptitudes draft; player **Confirm** on aptitudes console |
| **Activate** | Host calls **`POST /api/aptitude-presets/activate`** (set active + commit allocate/respec in one txn); aptitudes Confirm **not** required |
| Mode B Activate | Show **respec price before** Activate (same law as Confirm; no ConfirmDialog — S2) |

---

## Bus (closed additions)

| Event | Duty |
|---|---|
| `preset.open` / `preset.close` | Nested layer |
| `preset.select` | Highlight gallery row |
| `preset.applyDraft` | Materialize → parent draft |
| `preset.activate` | Host → activate API (S3) |
| `preset.save` / `preset.delete` | CRUD via host → API |

---

## Testing strategy

- Save disabled when permille sum ≠ 1000.
- Apply-to-draft does not call allocate mutation; Activate calls activate API only.
- Empty favour → New offers Even, not zero rows.
- Mode B Activate shows price gate before mutate.
- Donut updates on revision; empty budget → honest empty state.
- Depth ≤ 3; no sibling route.

---

## Boundaries

- **Always:** Nested layer; donut; favour seed via permille GET; pieces pure; transactional Activate.
- **Ask first:** Second chart library; Vision synergy fields in this console.
- **Never:** Chip-bar as only preset UI; pie/radar as peer primary chart; silent Activate without
  Mode B price; Hub auto-baseline from favour; split active-then-allocate client sequence.

---

## Success Criteria

- [ ] Player can create, select, Apply-to-draft, and Activate a preset on Modes A/B/C.
- [ ] New defaults to species favour permille when planned; empty → Even (S7).
- [ ] Donut + dual abs/% editor land (D12/D13).
- [ ] Activate uses transactional API; Confirm remains draft path (E1/S3).

## Open Questions

None — A14–A17 / S1–S3 / S7 locked. Exact soft abs default numbers from tuning at implement.
