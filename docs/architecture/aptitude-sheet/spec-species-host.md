# Spec: `species-host`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **A8** · **A8b** · Mode B · **D4** · **E1** · **S2** · **S3**  
**Species BE (unchanged ownership):** `GET /api/aptitudes/species/...` · `POST /api/species-build/respec` ·
price GET · [../species-build/spec-allocation-surface.md](../species-build/spec-allocation-surface.md)  
**Door:** `PactsLayer` → `AptitudesLayer` (`speciesId`) — **locked first door**  
**Depends on:** `aptitudes-surface-vm` · `aptitude-preset-console` · `aptitude-preset-api` (Activate)

---

## Objective

Collapse `SpeciesBuildPanel` god UI into Mode B of the shared `aptitudes-console` under the existing
Pacts/`AptitudesLayer` door. Keep priced respec / free revert; never reopen free species allocate.
**Price before Confirm and before Activate** on the in-console strip — **retire ConfirmDialog** (S2).

---

## Tech stack

- FE: AptitudesLayer + RecipeMount Mode B + nested preset console
- Existing `useSpeciesBuild` / respec mutations adapted to bus confirm
- Activate: **`POST /api/aptitude-presets/activate`** only (S3) — server calls into species-build respec

---

## Commands

```powershell
npm test -- --run species-build
npm test -- --run AptitudesLayer
```

---

## Project structure

| Path | Duty |
|---|---|
| `AptitudesLayer.tsx` | Mount shared console Mode B when species tab/speciesId set |
| `SpeciesBuildPanel.tsx` | Delete or reduce to thin deprecated wrapper → RecipeMount; **remove ConfirmDialog** |
| `useSpeciesBuild.ts` | Keep price/respec logic; feed fold input; no parallel silent Save |

---

## Behavior

| Action | Duty |
|---|---|
| Open from Pacts with `speciesId` | Mode B console |
| Adjust draft | Within DemonType budget |
| Confirm | `allocate-decision-strip` → respec (or free first-override) with price shown in **`species-build-chrome`** — **no band-3 ConfirmDialog** (S2/GG-63) |
| Cancel | Discard draft (`aptitude.reset`); fiction **Cancel** (S8) |
| Free first-override / free revert | Still go through Confirm/Cancel; price chip shows **Free** — no silent parallel Save POST |
| Activate (preset) | Price shown before Activate; host calls **`POST .../activate`** (S3) |
| Apply to draft | No respec until Confirm |
| New preset / Auto-assign favour | Favour GET permille (S1); empty → Even (S7) |

Commander tab inside AptitudesLayer = Mode C (same recipe) — A4 collapse.

**Out of Wave 1:** lawn species glance door (ideal Wave 2).

---

## Testing strategy

- Respec price shown before confirm **and** before Activate on strip/chrome — not in a dialog.
- No `species-build-respec-confirm` (or equivalent) ConfirmDialog after collapse.
- Free first-override requires Confirm; does not POST on a side button alone.
- Layer mounts Mode B recipe; no parallel NumberInput god grid as SSOT.
- Activate does not require aptitudes Confirm after success.

---

## Boundaries

- **Always:** Respec economy; effective+baseline honesty on read; price gate on both commit verbs;
  decision-strip Confirm (S2).
- **Ask first:** New empire nav door beyond Pacts layer.
- **Never:** `POST /api/aptitudes/species/allocate`; edit species from UniqueActor sheet;
  Hub UniqueDemon fill from favour; ConfirmDialog for draft Confirm; split Activate client sequence.

---

## Success criteria

- [ ] Species build UX is the shared console Mode B under AptitudesLayer.
- [ ] `SpeciesBuildPanel` is not a second product surface; ConfirmDialog retired (S2).
- [ ] Generals still receive EffectiveSpeciesAllocation on lawn (unchanged BE).
- [ ] Activate uses transactional preset activate API (S3).
