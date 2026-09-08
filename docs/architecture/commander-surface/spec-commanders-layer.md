# Spec: `commanders-layer`

**Module id:** `commanders-layer` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `commander-list-api`, `default-persistence` · **Blocks:** `commander-sheet-role`
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Copy Creatures layer pattern** — `CreaturesLayer.tsx` props, `PanelShell`, four-state loading,
   `mountedLayers` in `SanctumStage.tsx` (`SanctumStage.tsx:74-216`).
2. **Registration surfaces (no central registry):** `railState.ts`, `keybindings.ts`, `SanctumStage.tsx`,
   `Rail.tsx` icons — all four must stay in sync.
3. **IA-strict rail (owner 2026-08-30):** **Commanders `K`** after Creatures; **Aptitudes demoted off
   rail** to sheet-only (Progression tab). Rail stays **nine player layers** — replace Aptitudes slot with
   Commanders, do not add a tenth layer.
4. **Hotkey:** extend `BindableActionId` with **`commanders: "k"` only** — remove aptitudes from rail
   hotkey (`keybindings.ts`).
5. **URL:** `#/sanctum?panel=commanders&sel=commander:dave` (`information-architecture.md` §8).
6. **N=1 today:** hide seg control; show Dave + `default` badge; no filter bar (plate 09 §B).
7. **Verbs:** **Set default** (POST persistence) · **Defend the lawn** (navigate `/lawn` or Sanctum CTA
   mirror) — never *Set for lawn run* / *Deploy to lawn* (ideal §5).

---

## Objective

Add the Commanders player layer: empire roster, persisted default selection, drill-in to shared
`ActorPanel`, optional travel to lawn — **never required before play**. Demote Aptitudes from rail to
sheet-only in the same change set.

**Success:** `K` toggles layer; rail `data-testid="rail-commanders"`; Aptitudes no longer on rail; row
select + Set default POST updates server; Defend the lawn navigates without picker dialog; Esc closes layer.

---

## ⛔ Program acceptance share

Playwright: press `K` → layer opens; click Set default → POST `/api/commanders/default`; Defend the lawn
navigates without modal picker. Share is part of `e2e/commander-surface.spec.ts`.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- CommandersLayer
npm run test -- railState
npm run build
npx playwright test e2e/commander-surface.spec.ts
```

Live review: `/review-web` per `local-web-review` skill.

---

## Project structure

| Path | Change |
|---|---|
| `web/fusion-rpg-web/src/layers/commanders/CommandersLayer.tsx` | **new** |
| `web/fusion-rpg-web/src/layers/commanders/CommandersLayer.test.tsx` | **new** |
| `web/fusion-rpg-web/src/lib/bus/useCommanders.ts` | **new** — query + setDefault mutation |
| `web/fusion-rpg-web/src/shell/railState.ts` | edit — `"commanders"` after `"creatures"`; **remove `"aptitudes"` from rail order** |
| `web/fusion-rpg-web/src/layers/system/keybindings.ts` | edit — `commanders: "k"`; remove aptitudes rail binding |
| `web/fusion-rpg-web/src/shell/Rail.tsx` | edit — Commanders icon |
| `web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx` | edit — lazy mount, URL `sel`, `K` handler |
| `web/fusion-rpg-web/src/features/lawn/LawnPage.tsx` | edit — `openLayerOnSanctum("commanders")` if needed |
| `web/fusion-rpg-web/e2e/commander-surface.spec.ts` | **new** — program acceptance owner (layer slice) |
| `web/fusion-rpg-web/src/shell/railState.test.ts` | edit — Creatures → Commanders → Relics; no aptitudes rail |

---

## Design

### Layer layout (plate 09 §B)

- Header: title, sub "Your empire · N commanders · {name} is default"
- Deploy strip: default summary, seg when N>1, **Set default**, **Defend the lawn**
- List: `ActorRow` per commander — frame, name, aura chip, `default` badge, location/legion stub links
  (inert v1)
- Footer: mirror strip actions
- Select row → optional detail card or open sheet (same ladder as Creatures)

### Set default flow

1. Row click selects commander (local highlight).
2. **Set default** → `POST /api/commanders/default` → invalidate list query → badge moves.
3. Row select and Set default write **same field** (ideal §3) — **explicit button only**; row click opens
   sheet drill-in, does not auto-POST.

### Defend the lawn

Same handoff as Sanctum CTA — navigate to lawn stage, **no** confirmation dialog (IA §9). Uses persisted
default at match start, not per-run picker.

### Global keys

Register in `SanctumStage` effect (`SanctumStage.tsx:155-168`) — toggle open/close on `K` only.

Unlock: session start with Creatures (IA §7).

### Contract

```typescript
type CommanderListRow = {
  id: string;
  displayName: string;
  isDefault: boolean;
  activeAuraId: string | null;
  activeAuraName: string | null;
  locationStub: string | null;
  legionStub: string | null;
};
```

Adapt from `GET /api/commanders/{playerId}` — never import server DTOs in components.

### E2E (program slice)

In `e2e/commander-surface.spec.ts`:

1. Set default in Commanders layer → assert Sanctum Leading updates on revisit (coordinates with
   `sanctum-readout` share).
2. Assert no pre-run picker on Defend the lawn.

---

## Governance

| GG | Application |
|---|---|
| GG-9 | One ladder — commander list uses same `ActorRow` / sheet drill-in as Creatures |
| GG-20 | Player vocabulary — "Set default", "Defend the lawn"; ban picker verbs |
| GG-44 | Layer hotkeys discoverable — `K` on rail tooltip |
| GG-8 | Esc closes layer; Sanctum stage stays mounted |

---

## Code style

- **Layer:** copy `CreaturesLayer.tsx` structure — `PanelShell`, bus hooks, four-state UI.
- **Rail:** update `railState.test.ts` expected order; grep for `"aptitudes"` rail registration and remove.
- **Bus:** `useCommanders.ts` — query key per player; `setDefault` mutation invalidates list + default.
- **Adapt:** `adapt.ts` row mapper; include `activeAuraId`.

---

## Testing strategy

| Pattern | Source |
|---|---|
| Loading / error / empty | `CreaturesLayer.test.tsx:33-56` |
| Esc closes, stage visible | `CreaturesLayer.test.tsx:103-111` |
| Rail click | `SanctumStage.test.tsx:104-114` |
| Hotkey `K` | `e2e/creatures.spec.ts:54-66` mirror |
| Deep link `?panel=commanders&sel=` | `e2e/creatures.spec.ts:77-85` mirror |
| Rail order | `railState.test.ts` — Creatures → Commanders → Relics |
| Aptitudes off rail | assert no `rail-aptitudes` / no `S` hotkey for layer |
| Set default mutation | unit mock bus POST |

---

## Boundaries

- **Always:** `data-testid="commanders-layer"`; GG-23 player vocabulary; empire-only rows; Aptitudes off rail
- **Ask first:** none — IA-strict demotion is owner-closed
- **Never:** pre-run gate; Zomboss row; aptitude grid inline; duplicate six-tab shell; *Set for lawn run*

---

## Success criteria

- [ ] Vitest + Playwright green for layer open/close/set-default
- [ ] Rail order: Creatures → Commanders → Relics (Aptitudes not on rail)
- [ ] `commanders: "k"` only; aptitudes rail hotkey removed
- [ ] ⛔ share: K + POST Set default + no picker in E2E
- [ ] Defend the lawn does not open picker

---

## Open questions

- **Open sheet from list** — double-click row vs explicit Open button (match Creatures when wired).
