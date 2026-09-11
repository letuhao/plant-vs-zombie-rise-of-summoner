# Spec: `aptitude-auto-assign`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D8** · **E1** · **E3** · **D14** · **A12** · **S1/S6/S7**  
**Depends on:** `aptitudes-surface-vm` (bus) · `aptitude-pieces` · favour/active from `aptitude-preset-api`

---

## Objective

Provide **draft-only** auto-assign so players can fill twelve aptitude shares without silent POST.
Rules: Even · Posture lean · Active preset · Species favour (Mode A/B). Player finishes with
**Confirm** on the aptitudes console (E1) — never with an implied save.

---

## Tech stack

- Pure fill helpers (Core and/or FE fold) — same permille → budget scale as preset materialize without
  D13 row clamps unless “Active preset” rule uses the full materialize path
- Host invokes on bus `aptitude.autoAssign`; fold writes draft shares only
- Favour permille from **`GET /api/aptitude-presets/.../favour/{speciesId}`** only (**S1**)

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter AutoAssign
npm test -- --run aptitude-auto
```

---

## Project structure

| Path | Duty |
|---|---|
| Core helper or FE pure fn | `FillDraft(rule, budget, context) → shares` |
| Surface bus | `aptitude.autoAssign` with `{ rule }` |
| UI control | Piece or chrome control listing rules (themeRef) |

---

## Rules (closed)

| Rule id | Fiction | Behavior |
|---|---|---|
| `even` | Even | Equal split of budget across 12; remainder as leftover (legal) |
| `posture-force` / `finesse` / `bastion` | Posture lean | System seed weights for that posture → scale to budget |
| `active-preset` | Active preset | Load binding’s active preset; **materialize** with D13 (leftover legal E2) into draft |
| `species-favour` | Species favour | Mode A/B only: load **favour GET** `sharesPermille`, scale to budget — **E3 seed only** |

Mode C: no `species-favour` (use Even / posture / active). Overspend refused before draft apply.
Empty UniqueDemon / empty commander stays empty until player Confirm or Activate — favour never
writes Hub (**E3**).

### Favour consumption (S1 / S7)

- **Never** call `SpeciesBuildPlanCatalog` from FE.
- **Never** treat species GET `baseline` **points** as template permille.
- If favour GET returns `{}` or missing species → **refuse** `species-favour` with named reason;
  UI offers **Even** — no silent all-zero draft.

---

## Code style

```csharp
checked
{
    long share = (long)budget * permille / 1000L; // widen before multiply; /1000 last (S6)
}
// leftover = budget - sum(shares) is OK
```

No private power curve — PointBudget is SSOT.

---

## Testing strategy

- Unit: each rule sums ≤ budget; leftover ≥ 0; Mode C refuses favour.
- Empty favour → refuse; does not write zeros.
- Fold: bus event dirties draft only — no mutation mock called.
- Active-preset path uses same materialize as preset-api (shared helper preferred).
- Scale math uses `(long)budget *` not `budget *` after a narrow type.

---

## Boundaries

- **Always:** Draft-only; leftover legal after fill; favour via GET permille; Mode B favour allowed.
- **Ask first:** New auto-assign rules beyond the closed set.
- **Never:** Silent POST; Hub EffectiveUnique from favour; Aspect scope; FE `SharesFor`;
  baseline-points-as-‰.

---

## Success Criteria

- [ ] Auto-assign never calls allocate/respec mutations.
- [ ] Species favour available in Mode A/B via favour GET; absent in Mode C.
- [ ] Empty favour refused; Even offered (S7).
- [ ] Active-preset fill respects D13 leftover-legal materialize.
- [ ] Confirm (not Auto-assign) commits the draft.

## Open Questions

None — A12/E1/E3/S1/S6/S7 locked.
