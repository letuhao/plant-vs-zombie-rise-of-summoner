# Spec: `aptitudes-surface-vm`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal:** `aptitudes-console` recipe · GUI Lego fold+bus · **D6–D8** · preset.open  
**Depends on:** `aptitude-pieces` · `aptitudes-live-bus` · `aptitude-auto-assign` (rules)  
**Parent:** [../gui-lego/spec-composition.md](../gui-lego/spec-composition.md)

---

## Objective

Pure fold + recipe so Aptitudes is never a god TSX: one `aptitudes-console` surface with mode
discriminator A/B/C, binding payloads for leftover / decision / auto-assign / preset-entry, and a
closed bus catalog. Nested preset console is a **separate** recipe opened via bus — not inlined as
god chrome.

---

## Tech stack

- Pure TypeScript fold (no React inside fold)
- Recipe JSON under `docs/design/gui-lego/recipes/` (or project convention)
- `RecipeMount` thin host (see host-role-gate / species-host)

---

## Commands

```powershell
npm test -- --run aptitudes
# fold unit tests with fixtures for modes A/B/C
```

---

## Project structure

| Path | Duty |
|---|---|
| `web/.../fold/aptitudesCook.ts` (name flexible) | `foldAptitudesSurfaceVm(input) -> Vm` |
| Recipe `aptitudes-console` | Slot tree: scope, leftover, auto, preset-entry, bands, inspect, decision |
| Bus catalog | Closed event names |

---

## Inputs

| Field | Source | Modes |
|---|---|---|
| `mode` | host | `unique` \| `species` \| `commander` |
| `instanceId` / `speciesId` / `playerId` | host | per mode |
| Allocation state | unique/species/commander GET | shares, budget, leftover |
| Catalog | aptitude-catalog | names, icons, postures, readings |
| Edges → fed families | aptitude tuning edges joined to displayNames | inspect |
| Species chrome | baseline, hasOverride, price | Mode B |
| Active preset name | preset-api | preset-entry |
| Auto-assign rule availability | auto-assign module | favour only if species known |
| `ui` | selectedAptitudeId, draft shares | |
| `availability` | query status | |
| `revision` | monotonic | gauges |

---

## Outputs (sketch)

| Slice | Piece |
|---|---|
| `scopeChip` | aptitude-scope-chip |
| `leftover` | leftover-gauge (**hero**, D6) |
| `autoAssign` | control binding + available rules |
| `presetEntry` | preset-entry (active name) |
| `speciesChrome?` | species-build-chrome (Mode B only) |
| `bands[]` | posture-band ×3 → aptitude-tile ×4 |
| `inspect` | aptitude-inspect |
| `decision` | allocate-decision-strip (D7; draft Confirm path E1; **Cancel** fiction S8) |
| `phase*` | loading/error when needed |

Fold emits **displayNames** for postures and fed families — never raw `force` / channel ids on band.

---

## Bus (closed)

| Event | Duty |
|---|---|
| `aptitude.select` | Select tile → inspect |
| `aptitude.step` | Draft +/- |
| `aptitude.reset` | Cancel / revert draft (decision **Cancel** fiction — S8; bus name stays reset) |
| `aptitude.confirm` | Host commits **draft** write (E1) |
| `aptitude.autoAssign` | Fill draft via auto-assign rules — **no POST** |
| `preset.open` | Open nested preset console |
| `aptitude.retry` | Refetch |

Pieces never call mutations directly. **Activate** is owned by preset-console bus → host — not by
aptitudes Confirm.

---

## Testing strategy

- Fold goldens: Mode A empty leftover; Mode B hasOverride; Mode C commander chip title.
- leftover + decision slices present for all modes when allocate open.
- autoAssign favour rule omitted in Mode C fixtures.
- No engine vocabulary assertions on emitted labels.
- Confirm bus does not fire on Activate path fixtures.

---

## Boundaries

- **Always:** Pure fold; mode-exclusive write left to host; leftover in-band.
- **Never:** Second VM for species; fetch inside fold; Activate via `aptitude.confirm`.

---

## Success criteria

- [ ] Recipe + fold land; three modes produce valid payloads from fixtures.
- [ ] leftover, decision, autoAssign, presetEntry slices present.
- [ ] God AptitudesTab logic moves behind RecipeMount (host specs).
- [ ] A3 fed families present on inspect payload when edges exist.
