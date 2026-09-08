# Commander surface — task list



**Program:** `commander-surface` · **Plan:** [commander-surface-plan.md](commander-surface-plan.md) ·

**Map:** [docs/architecture/commander-surface-map.md](../docs/architecture/commander-surface-map.md)



**Do not start implementation until the owner approves the map and module specs.**



**Strengthen pass:** 2026-08-30 complete — see map DESIGN-GATE footer.



---



## P0 — Review gate



- [x] Owner review: [commander-surface-map.md](../docs/architecture/commander-surface-map.md) (strengthen pass) — **owner-approved 2026-08-31.**

- [x] Owner review: all specs in [commander-surface/](../docs/architecture/commander-surface/) — **owner-approved 2026-08-31.**

- [x] Cross-program sign-off: aura-skill + actor-sheet boundaries acknowledged — **owner-approved 2026-08-31.**

- [x] **Strengthen pass complete** — SSOT table, E2E gate, Aptitudes demotion, per-module ⛔ shares — **owner-approved 2026-08-31.**



---



## `default-persistence`



Spec: [spec-default-persistence.md](../docs/architecture/commander-surface/spec-default-persistence.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] `CommanderIds.TryParseStableId` in Core

- [x] `RpgStore.PlayerCommander.cs` + schema ensure + Reset (`DELETE FROM rpg_player_commander`)

- [x] GET/POST default in `CommanderEndpoints.cs`

- [x] `FusionRpg.Data.Tests` store tests (implicit Dave, corrupt row fallback)

- [x] `FusionRpg.Server.Tests` endpoint tests

- [x] ⛔ share: snapshot/GET poll reads default without seeded row

- [x] `guard-dal.ps1` green



---



## `commander-list-api`



Spec: [spec-commander-list-api.md](../docs/architecture/commander-surface/spec-commander-list-api.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] `CommanderDtos` + `PlayerEmpireCommanders` filter

- [x] `GET /api/commanders/{playerId}` + loadout ∩ runtime aura algorithm

- [x] Server tests — Dave only, no Zomboss, Might active when enabled

- [x] Web contract types + adapt (`activeAuraId`)

- [x] ⛔ share: Zomboss absent + aura fields integration test



---



## `match-snapshot`



Spec: [spec-match-snapshot.md](../docs/architecture/commander-surface/spec-match-snapshot.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] `MatchCommanderSnapshot` + holder (allocation + revision)

- [x] Session cache + synchronous freeze in `MatchHost.Apply` before `NotifyMatchStart`

- [x] Clear on `board.end`, `match.result`, auto-end-before-start

- [x] `debug.snapshot` → `match.commander` observe DTO

- [x] Bridge immutability documented / cross-linked

- [x] Core/injector tests — mid-match default unchanged

- [x] ⛔ share: automated mid-match freeze test



---



## `commanders-layer`



Spec: [spec-commanders-layer.md](../docs/architecture/commander-surface/spec-commanders-layer.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] **Aptitudes demoted off rail** — Commanders `K` after Creatures; remove aptitudes rail hotkey

- [x] `railState` + `keybindings` + `Rail` icon

- [x] `CommandersLayer.tsx` + bus hooks

- [x] `SanctumStage` mount + URL `?panel=commanders`

- [x] Vitest + ⛔ Playwright: K + POST Set default + no picker



---



## `commander-sheet-role`



Spec: [spec-commander-sheet-role.md](../docs/architecture/commander-surface/spec-commander-sheet-role.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] `actor-sheet-shell` available in production path

- [x] `ActorPanel` role branch + `CommanderSheetFooter`

- [x] Progression tab canonical; optional Aptitudes layer link only

- [x] Compose `aura-surface` on Actions tab (via existing `ActionsTab`)

- [x] Layer close resets commander sheet (`sheetOpen` cleared when `open` is false)

- [x] Deep-link `?panel=commanders&sel=` auto-opens commander sheet

- [x] ⛔ share: commander footer verbs; no Deploy/Release



---



## `sanctum-readout`



Spec: [spec-sanctum-readout.md](../docs/architecture/commander-surface/spec-sanctum-readout.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] Leading line + Change commander in `SanctumHome.tsx`

- [x] Defend the lawn not gated on commander pick

- [x] Error state does not silently fake Dave

- [x] ⛔ mandatory E2E in `commander-surface.spec.ts`



---



## `lawn-hud-chip`



Spec: [spec-lawn-hud-chip.md](../docs/architecture/commander-surface/spec-lawn-hud-chip.md)



- [x] Spec approved — **owner-approved 2026-08-31.**

- [x] `match.commander` on lawn observe fold (`LawnHud.tsx` / `lawnProjectorFold.ts`)

- [x] `LawnHudCommander` chips before deployed row

- [x] Dave fallback chips on snapshot degrade

- [x] ⛔ E2E via real fold; mid-match freeze in program acceptance

- [x] Lawn HUD tap opens commander sheet with GG-60 this-match banner

- [x] Lawn sheet closes when match commander chip clears (board.end / snapshot clear)



---



## Program acceptance



- [x] **`e2e/commander-surface.spec.ts`** — owner file; asserts full program acceptance

- [x] E2E: Sanctum shows default → lawn HUD matches snapshot at start

- [x] E2E: Set default in Commanders layer → Sanctum Leading updates on revisit (spec commanders-layer share)

- [x] Mid-match Set default POST does not alter current lawn HUD chips (snapshot fold unchanged)

- [x] E2E commander sheet: no Deploy/Release; Defend from sheet navigates to lawn

- [x] E2E deep-link sel auto-opens sheet; lawn HUD tap + next-run Set default

- [x] P5 audit: lifecycle reset, id fallback, sel rerender, reopen sheet, matchBanner no aura, change-in-list E2E

- [ ] Until Playwright green, program is **not done** (map ⛔ rule) — **Playwright 10/10 green**; live deploy smoke still owner


