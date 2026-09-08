# Spec: `commander-sheet-role`

**Module id:** `commander-sheet-role` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `commanders-layer`, `actor-sheet-shell` · **Blocks:** nothing (leaf FE composition)
**Defers to:** [aura-surface](../aura-skill/spec-aura-surface.md), [progression-tab](../actor-sheet/spec-progression-tab.md),
[gear-tab](../actor-sheet/spec-gear-tab.md)
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Same `ActorPanel`** for commanders and creatures (GG-9, audit §0) — role branch, not second panel.
2. **`actor-sheet-shell` ships first** — six-tab container must exist before commander role gating is
   meaningful in production (`actor-sheet-map.md`).
3. **Actions tab aura UI** owned by `aura-surface` — this module **composes** `AuraSlot`, does not
   reimplement enable/disable/eviction (`spec-aura-surface.md` §2).
4. **Progression tab canonical** for commander aptitudes — sheet shows Progression tab; **Aptitudes layer
   optional secondary link only** (plate 08 §J). Rail Aptitudes demoted (`commanders-layer`).
5. **Footer verbs** per plate 08 §I/J: **Set default** · **Defend the lawn** · Close — never Deploy/Release
   (creature lifecycle).
6. **◎ Primary Stats shortcut** on sheet = link to **Progression tab**, not Aptitudes layer rail (IA-strict).

---

## Objective

When `ActorPanel` opens for a commander actor, show commander-appropriate tabs, footer, and header tags;
hide creature Deploy/Release; wire footer mirrors to persistence and travel.

**Success:** Open Dave from Commanders layer → Actions shows aura group (from aura-surface when built) →
footer Set default POST persists → Defend the lawn travels without gate → Release/Deploy absent.

---

## ⛔ Program acceptance share

Vitest/Playwright smoke: commander role renders `CommanderSheetFooter` with Set default + Defend the lawn;
creature role still shows Deploy/Release; **no Deploy/Release on commander footer**.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- ActorPanel
npm run test -- CommandersLayer
npm run build
npx playwright test e2e/commander-surface.spec.ts
```

---

## Project structure

| Path | Change |
|---|---|
| `web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx` | edit — `actorRole: "commander" \| "creature"` branch |
| `web/fusion-rpg-web/src/ui/actor/CommanderSheetFooter.tsx` | **new** |
| `web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx` | edit — commander: aura group; creature: locked grid |
| `web/fusion-rpg-web/src/layers/commanders/CommandersLayer.tsx` | edit — open panel with role commander |
| `web/fusion-rpg-web/src/contract/types.ts` | edit — commander actor kind if needed |

---

## Design

### Role detection

Prefer explicit prop from list opener over inferring from id prefix:

```tsx
<ActorPanel actor={commanderView} role="commander" ... />
```

Commander views use stable id `commander:dave` — not `unique_actors` rows.

### Header (plate 08 §I)

- Tags: Commander, default lawn summary, link "change in list" → open Commanders layer
- Overview: location/legion stubs (inert links)

### Tabs

| Tab | Commander behavior |
|---|---|
| Overview | Commander copy, stubs |
| Progression | **Canonical** aptitude editor — delegate to `progression-tab` module |
| Derived Stats | Same as creature when data exists |
| Actions | **Aura loadout group** — `aura-surface` widgets; combat slots locked below |
| Passives | Locked preview |
| Gear | Banner slot — `gear-tab` empty/designed state |

Optional secondary: Progression tab footer link "Open Aptitudes layer" (plate 08 §J) — not required v1.

### Footer (`CommanderSheetFooter`)

```text
[Default lawn commander · change in list]     [Close] [Set default] [Defend the lawn]
```

- **Set default** → `POST /api/commanders/default` with this commander's id
- **Defend the lawn** → same navigate as Sanctum/layer mirror
- **Close** → `onOpenChange(false)`

### Optional lawn HUD drill-in (ideal §3 option B)

If tap-to-sheet from lawn HUD: banner **"This match: Dave · Might"** + label edits **"Next run"** on Set
default / loadout controls (GG-60).

### Minimal Playwright

Set default from sheet → list default badge updates (mock or live API in `commander-surface.spec.ts`).

---

## Governance

| GG | Application |
|---|---|
| GG-9 | One panel, role branch — no second commander detail surface |
| GG-60 | Optional mid-match banner distinguishes this match vs next run |
| GG-20 | Footer verbs only Set default / Defend the lawn |

---

## Code style

- **Footer:** small presentational component; callbacks from `ActorPanel` for POST + navigate.
- **Tabs:** Progression tab import from actor-sheet module when available; do not fork aptitude save.
- **Tests:** role prop drives footer branch — mirror existing `ActorPanel` creature tests.

---

## Testing strategy

| Test | Assert |
|---|---|
| Commander role | No Deploy/Release buttons |
| Creature role | Deploy/Release present (existing) |
| Set default | Mock POST called with commander id |
| Actions tab | Aura slots render when aura-surface wired; locked combat slots below |
| Progression tab | Canonical path; no inline Aptitudes grid duplicate |

---

## Boundaries

- **Always:** compose aura-surface; one ActorPanel; commander footer verbs only; Progression tab canonical
- **Ask first:** optional mid-match banner when opened from lawn HUD
- **Never:** second detail panel; creature footer on commander; re-spec aura math; fork aptitude save;
  Aptitudes layer as required path

---

## Success criteria

- [ ] Commander drill-in from Commanders layer shows correct footer
- [ ] Set default from sheet updates list default badge (Playwright smoke)
- [ ] ⛔ share: commander footer verbs; no Deploy/Release
- [ ] Actions tab does not duplicate aura-surface behavior tests (integration smoke only)

---

## Open questions

- **Gear tab banner** — empty state copy until item program ships (follow `gear-tab` spec).
