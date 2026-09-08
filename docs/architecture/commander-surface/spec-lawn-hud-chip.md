# Spec: `lawn-hud-chip`

**Module id:** `lawn-hud-chip` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `match-snapshot` · **Blocks:** nothing
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Plate 04 §A** — commander chip before deployed specimen chips: Dave + active aura (`04-run-stages.html`).
2. **Read-only this match** (ideal §3 option A default) — GG-60 legibility; snapshot from
   `MatchCommanderSnapshotHolder` via observe fold, not live server poll mid-wave.
3. **Observe contract owned by `match-snapshot`** — `debug.snapshot` → `match.commander` → lawn projector
   fold; no separate transport TBD.
4. **Changing default in web during match** does not update chip until next `board.start` (ideal §2.1).
5. **Degrade aligned with snapshot** — poll failure → **Dave chips** (display name + optional null aura chip),
   not omit — per Boundaries in `match-snapshot`.
6. **Optional tap** (ideal §3 option B): opens commander sheet with "this match" banner — **not required
   for v1 module done**; document as follow-up if omitted.

---

## Objective

Lawn stage HUD shows who led **this** match and which aura was active at wave start — glance readout only.

**Success:** Match starts with Dave + Might snapshot → HUD shows both chips → user changes default in
Commanders layer mid-match → HUD unchanged → next match shows new leader.

---

## ⛔ Program acceptance share

E2E via **real `debug.snapshot` fold** (not inject-only test hook): commander + aura chips visible after
lawn enter; mid-run default change does not alter chips until next match. Part of
`e2e/commander-surface.spec.ts`.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- LawnHud
npm run build
npx playwright test e2e/commander-surface.spec.ts
```

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter MatchCommanderSnapshot
```

---

## Project structure

| Path | Change |
|---|---|
| `web/fusion-rpg-web/src/features/lawn/LawnHudCommander.tsx` | **new** — chip cluster |
| `web/fusion-rpg-web/src/features/lawn/LawnHud.tsx` | edit — mount chips before deployed cluster |
| `web/fusion-rpg-web/src/features/lawn/LawnPage.tsx` | edit — ensure HUD mounted on lawn route |
| `web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.ts` | edit — fold `match.commander` from debug snapshot |
| `web/fusion-rpg-web/src/contract/types.ts` | edit — `matchCommander?: { displayName, auraName }` on lawn view |
| `src/FusionRpg.Injector/Debug/DebugRuntime.cs` | edit — with `match-snapshot` deliverable |

**Retarget from earlier draft:** chips live in `LawnHud.tsx` / `LawnPage.tsx`, not `LawnStage.tsx`.

---

## Design

### Layout (plate 04)

```text
[sun] [wave] [timer]     [commander: Dave] [aura: Might]     [deployed: …] [transport]
```

- Label readouts `commander` / `aura` optional — plate uses cluster with chips only
- Plain readout sizing per GG-60 — no ornament on run stage

### Observe contract (depends on `match-snapshot`)

Flow:

1. Injector `DebugRuntime.Snapshot()` includes `match.commander` when holder has `Current`.
2. Server debug event poll delivers payload to web lawn projector.
3. `lawnProjectorFold.ts` maps to `LawnViewModel.matchCommander`.
4. `LawnHudCommander.tsx` renders chips from view model.

Fields:

| HUD | Observe / snapshot field |
|---|---|
| Commander chip | `leadingCommanderDisplayName` |
| Aura chip | `activeAuraDisplayName` — hide chip if null |

**No** per-frame REST poll. **No** live list API on lawn route during match.

### Degradation (aligned with snapshot)

| Snapshot state | HUD |
|---|---|
| Full snapshot | Dave + Might chips |
| Dave + null aura | Commander chip + hide aura chip |
| Poll failure (Dave fallback) | Commander chip "Crazy Dave" + no aura chip |
| Outside match / null holder | Hide commander cluster (pre-start lawn UI) |

### Interactions (v1)

- **No click** — read-only (ideal §3 A)
- v2 optional: tap → commander sheet with next-run labeling (cross-link `commander-sheet-role`, GG-60)

### Note (plate 04)

HelpText: identity frozen for this match; change default in Commanders layer for **next** run.

---

## Governance

| GG | Application |
|---|---|
| GG-60 | Plain readouts on run stage; frozen-this-match copy |
| GG-20 | No Set default on lawn HUD |

---

## Code style

- **HUD:** presentational chips in `LawnHudCommander.tsx`; parent `LawnHud.tsx` orders before deployed row.
- **Fold:** extend existing lawn projector — same path as other debug snapshot fields.
- **Tests:** unit fold test with fixture snapshot JSON; component test with mock view model.

---

## Testing strategy

| Level | Test |
|---|---|
| Unit fold | `match.commander` fields appear in HUD model |
| Component | Chips render name + aura; aura hidden when null |
| E2E | After lawn enter + debug snapshot event, chips visible |
| E2E | Mid-match Set default in web → chips unchanged (program acceptance) |
| Core integration | Coordinates with match-snapshot mid-match freeze test |

---

## Boundaries

- **Always:** read snapshot/observe only; chips before deployed row; GG-60 plain readouts; Dave fallback
  chips on degrade
- **Never:** mid-run Set default on lawn HUD; aura toggle on lawn; poll server each frame; omit chips on
  degrade when snapshot says Dave

---

## Success criteria

- [ ] HUD shows commander + aura when snapshot present in fold
- [ ] Degrade shows Dave commander chip when snapshot used Dave fallback
- [ ] ⛔ share: E2E via real fold; mid-match default change does not alter chip
- [ ] Program acceptance slice satisfied with sanctum-readout + this module

---

## Open questions

None — observe transport closed: `debug.snapshot` fold owned by `match-snapshot` + this module's projector.
