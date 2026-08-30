# Spec: `sanctum-readout`

**Module id:** `sanctum-readout` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `default-persistence`, `commander-list-api` · **Blocks:** nothing
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Plate 01** — under **Defend the lawn**: `Leading: Crazy Dave · Might` + ghost link
   `Change commander` → Commanders layer (`01-shell-home.html` Sanctum shelf).
2. **Integrate into `SanctumHome.tsx`** — compose Leading line on the existing Defend the lawn shelf in
   [`SanctumHome.tsx`](../../../web/fusion-rpg-web/src/stages/sanctum/SanctumHome.tsx), not a misleading
   standalone footnote elsewhere.
3. **No web gate** — Defend the lawn CTA navigates to lawn without picker or loadout confirm (IA §9,
   ideal §5).
4. **Readout shows next-run default** from list API — not mid-match snapshot (snapshot is lawn HUD job).
5. **N=1:** single line readout; when N>1, same line updates to current default name + aura from API.
6. **URL navigation:** use `setSearchParams` / router pattern consistent with other Sanctum layer opens.

---

## Objective

Sanctum **Defend the lawn** shelf shows who will lead the next run and optional link to change default —
without blocking play.

**Success:** Fresh save shows Dave + aura label from API; after Set default elsewhere, Sanctum line updates
on next visit; Defend the lawn still one click to lawn; Change commander opens `?panel=commanders`.

---

## ⛔ Program acceptance share

**Mandatory E2E** (not optional): Leading line visible; Change commander link opens Commanders layer; Defend
the lawn never gated. Part of `e2e/commander-surface.spec.ts`.

---

## Commands

```powershell
cd web\fusion-rpg-web
npm run test -- SanctumHome
npm run build
npx playwright test e2e/commander-surface.spec.ts
```

---

## Project structure

| Path | Change |
|---|---|
| `web/fusion-rpg-web/src/stages/sanctum/SanctumHome.tsx` | edit — Leading line + Change commander on Defend shelf |
| `web/fusion-rpg-web/src/stages/sanctum/SanctumHome.test.tsx` | edit — readout assertions |
| `web/fusion-rpg-web/src/lib/bus/useCommanders.ts` | reuse — read default row from list query |

No separate `DefendLawnShelf.tsx` unless `SanctumHome` grows unwieldy — prefer inline shelf section first.

---

## Design

### UI copy (player vocabulary)

```text
Defend the lawn                                    [↵]
Leading: Crazy Dave · Might    Change commander
```

- **Leading line:** `{displayName} · {activeAuraName ?? "No aura"}` from default commander row
- **Change commander:** `setSearchParams({ panel: "commanders" })` or equivalent — never required before ↵
- **Defend the lawn:** existing travel to `#/lawn/...` — unchanged except **no** confirmation dialog

### Data

Single query: `GET /api/commanders/{playerId}` — pick row where `isDefault` or match
`defaultLawnCommanderId`.

| State | UI |
|---|---|
| Loading | Skeleton on Leading line only; CTA enabled |
| Error | Show error indicator + retry — **do not silently fake Dave without indicator** |
| Success | Leading line from API |

CTA **never** disabled for commander pick (no gate).

### Note (plate 01)

HelpText: default applies to next match; web and game menu are independent (ideal §0).

---

## Governance

| GG | Application |
|---|---|
| GG-3 | Sanctum shelf hierarchy — Leading subordinate to primary Defend CTA |
| GG-9 | Change commander opens same Commanders layer as rail `K` |
| GG-23 | Player vocabulary on Leading line |

---

## Code style

- **Composition:** extend existing Defend the lawn block in `SanctumHome.tsx`; match plate 01 structure.
- **Navigation:** `setSearchParams` for `panel=commanders`; avoid hard navigation away from Sanctum.
- **Error:** visible retry state — distinct from successful Dave display.

---

## Testing strategy

| Test | Assert |
|---|---|
| Default display | Leading shows Dave when API returns default Dave |
| Active aura | Shows Might when API returns `activeAuraName: "Might"` |
| Change link | Opens commanders layer / URL `panel=commanders` |
| Defend the lawn | Navigates to lawn without modal |
| API pending | CTA still enabled (no gate) |
| API error | Error indicator shown; not silent fake Dave |

---

## Boundaries

- **Always:** read-only summary on Sanctum; CTA never gated; integrate in SanctumHome
- **Never:** pre-run picker modal; loadout confirm for commander; creature berth UI here; silent error →
  fake Dave

---

## Success criteria

- [ ] SanctumHome test: Leading line visible; Defend the lawn not blocked
- [ ] Change commander opens Commanders layer
- [ ] ⛔ share: E2E Leading + Change + ungated Defend in `commander-surface.spec.ts`
- [ ] Matches plate 01 mock structurally
- [ ] Error state does not silently impersonate Dave

---

## Open questions

None — copy tweaks at implement time only.
