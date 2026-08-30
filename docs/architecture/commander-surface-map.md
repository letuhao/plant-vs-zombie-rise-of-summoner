# Capability map: commander-surface

Source: [commander-surface-ideal.md](commander-surface-ideal.md) (strengthened 2026-08-30) · plates
01/04/08/09 · [information-architecture.md](../design/information-architecture.md) ·
[commander-fe-audit-2026-08-30.md](../research/commander-fe-audit-2026-08-30.md).

**Status: proposed — pending owner review. No build authorized.**

Module specs live in [commander-surface/](commander-surface/), one per module id, written in dependency
order once this map is approved.

**Strengthen pass:** 2026-08-30 — multi-perspective audit (REST, injector snapshot, FE/IA, cross-program,
spec quality). Debated gaps closed in module specs below.

---

## What this program is

The **commander surface** — UI, persistence, and async handoff for who leads the next lawn run. The
player empire's commanders (Dave today; roster grows later) get a **Commanders layer** (`K`), a
persisted **default lawn commander**, Sanctum readout, lawn HUD identity chip, and commander role
extensions on the shared `ActorPanel`. **No pre-run web gate** — async default read at `board.start`.

**Explicitly not this program:** aura magnitude math, delivery atoms, aptitude resolve wiring (aura-skill);
six-tab shell container (actor-sheet); creature deploy berths / T21 (plate 07); legion menu (stubs only);
Zomboss in the player list.

---

## ⛔ The acceptance rule that governs every module

**No module may be marked done on internal criteria alone.** The first spec pass could tick every module
while Sanctum still showed stale copy, the lawn HUD polled live server state mid-wave, and the rail still
had Aptitudes instead of Commanders.

> **Program-level acceptance:** *Starting a lawn run with saved default Dave and an active aura shows Dave
> + aura on the Sanctum readout and lawn HUD; changing default in the Commanders layer updates the
> **next** run without a pre-run dialog; mid-run HUD stays on the snapshot taken at `board.start`.*
> Until **Playwright** (`e2e/commander-surface.spec.ts`) asserts that program acceptance exactly, the
> program is **not done**, regardless of module status.

Each module below names its **⛔ acceptance share** — one concrete automated slice. A module whose
criteria can all pass while the end-to-end assertion still fails is mis-specified.

---

## Decisions this map is built on (ideal §0 — do not reopen)

| Decision | Source |
|---|---|
| No pre-run web gate; persisted default; optional prep in Commanders layer | Owner 2026-08-30 |
| Match snapshot at `board.start` — leader + loadout/aura/allocation frozen for this match | Ideal §2.1 |
| Active aura ≠ default commander | Ideal §4 |
| Standard verbs: **Set default** · **Defend the lawn** only | Ideal §0 |
| Commanders layer `K`; rail after Creatures | IA §3 |
| v1 handoff: poll/freeze at `board.start` only | Ideal §4 |
| Empire list only; Zomboss on world map | Ideal §4 |
| **Aptitudes demoted to sheet-only** — Progression tab canonical; optional link only | Owner IA-strict 2026-08-30 |
| Default REST: **POST** (not PUT); single `CommanderEndpoints.cs` | Audit 2026-08-30 |

---

## IA-strict rail decision (owner 2026-08-30)

Shipped code today still registers **Aptitudes (`S`)** on the rail. This program **replaces** that slot
with **Commanders (`K`)** after Creatures — **nine player layers** total. Aptitudes moves to sheet-only
(Progression tab on `ActorPanel`; optional secondary link per plate 08 §J). See plate 09 §A and
[information-architecture.md](../design/information-architecture.md) §3.

| Surface | Aptitudes | Commanders |
|---|---|---|
| Rail hotkey | **Remove** (`S` off rail) | **`K`** after Creatures |
| Canonical editor | Progression tab | Commanders layer + sheet footer |
| Primary Stats (◎) shortcut | Sheet-only link to Progression | N/A |

---

## Endpoint and DTO SSOT

Single route group: `CommanderEndpoints.cs` maps list + default (persistence module owns route bodies;
list module owns the file).

| Field / path | Owner | Shape / rule |
|---|---|---|
| `GET /api/commanders/{playerId}` | `commander-list-api` | `{ defaultLawnCommanderId, commanders[] }` |
| `POST /api/commanders/default` | `default-persistence` | body `{ playerId?, commanderId }` → `{ defaultLawnCommanderId }` |
| `GET /api/commanders/{playerId}/default` | `default-persistence` | `{ defaultLawnCommanderId }` — optional convenience |
| `defaultLawnCommanderId` | persistence + list envelope | stable id, e.g. `"commander:dave"` |
| `CommanderListRow.id` | list-api | same stable id |
| `CommanderListRow.displayName` | list-api | e.g. `"Crazy Dave"` |
| `CommanderListRow.isDefault` | list-api | `id === defaultLawnCommanderId` |
| `CommanderListRow.activeAuraId` | list-api | catalog id, e.g. `"Might"` — see active aura algorithm in spec |
| `CommanderListRow.activeAuraName` | list-api | display label from `AuraContentCatalog` |
| `LeadingCommanderId` | `match-snapshot` | stable id frozen at `board.start` |
| `LeadingCommanderDisplayName` | `match-snapshot` | display name at freeze time |
| `ActiveAuraId` / `ActiveAuraDisplayName` | `match-snapshot` | from loadout ∩ runtime at freeze |
| allocation + revision | `match-snapshot` | copy of commander aptitude allocation at freeze |

**WHO vs WHAT:** `defaultLawnCommanderId` (persistence) names **who** leads the next run. Loadout +
`AuraRuntime` name **what** aura is active for that commander. The snapshot copies both at `board.start`.

---

## Cross-program snapshot rule

During `InMatch`:

1. **`MatchCommanderSnapshotHolder`** is the allocation + leader + aura source for lawn HUD and
   `commander-lawn-bridge` (cross-link in [spec-commander-lawn-bridge.md](../aura-skill/spec-commander-lawn-bridge.md)).
2. **Bridge ignores live aptitude cache** — mid-match `AptitudesUpdated` / loadout saves do not change lawn
   stats until the next `board.start`.
3. **Sanctum readout** reads live list API (next-run default). **Lawn HUD** reads snapshot only (this match).

---

## Modules

| Module id | Responsibility | Depends on | Defers to | ⛔ Acceptance share |
|---|---|---|---|---|
| `default-persistence` | `rpg_player_commander` table; `default_lawn_commander_id`; GET/POST default; implicit `commander:dave` when no row | — | loadout rows → `aura-equip-path` | Snapshot poll test reads implicit or saved default |
| `commander-list-api` | `GET /api/commanders/{playerId}` — empire roster, `isDefault`, aura summary, location/legion stubs | `default-persistence` | aura enable UX → `aura-surface` | Integration test: Zomboss absent; `activeAuraId`/`Name` from loadout ∩ runtime |
| `match-snapshot` | `MatchCommanderSnapshot` frozen in `MatchHost.Apply` at `board.start`; session cache + allocation copy | `default-persistence` | delivery → `commander-lawn-bridge`, `aura-delivery-path` | Automated mid-match default change does not alter `Current` |
| `commanders-layer` | FE layer `K`, rail, list, Set default, Defend the lawn mirror, `?panel=commanders` | `commander-list-api`, `default-persistence` | shell → `actor-sheet-shell` | Playwright: `K` opens layer; POST Set default; no picker |
| `commander-sheet-role` | `ActorPanel` commander branch: footer verbs, role gating, compose aura-surface | `commanders-layer`, `actor-sheet-shell` | Actions aura → `aura-surface`; aptitudes → `progression-tab` | Commander footer verbs only; no Deploy/Release |
| `sanctum-readout` | Sanctum Defend the lawn: Leading line + Change commander link | `default-persistence`, `commander-list-api` | list UI → `commanders-layer` | E2E: Leading line + Change link; Defend the lawn never gated |
| `lawn-hud-chip` | Lawn HUD commander + aura chips from snapshot; read-only this match | `match-snapshot` | optional drill-in → `commander-sheet-role` | E2E via real `debug.snapshot` fold; mid-run freeze asserted |

---

## Build order

```text
default-persistence
  → commander-list-api ─┬→ commanders-layer → commander-sheet-role
                        └→ sanctum-readout
  → match-snapshot → lawn-hud-chip
```

**Parallel-safe after `default-persistence`:** `commander-list-api` and `match-snapshot` (snapshot soft-depends
on loadout REST existing — today Dave-only via `/api/loadout`).

**Cross-program prerequisites (not owned here):**

- `actor-sheet-shell` before `commander-sheet-role` footer/tab composition is meaningful in production.
- `aura-surface` before commander Actions tab shows real aura slots (commander-sheet-role composes it).
- `commander-lawn-bridge` + `aura-delivery-path` before snapshot changes affect entities on lawn (snapshot
  can ship first; delivery is aura-skill acceptance).

---

## Deliberately deferred (not in any module here)

| Topic | Owner program |
|---|---|
| Aura magnitude, tick cost, channel mapping, R4 delivery | aura-skill |
| Enable/disable, eviction copy, GG-49 contributions | `aura-surface` |
| Aptitude allocation UI and save flow | `progression-tab` / `AptitudesPage` |
| Six-tab shell, generic tab bodies | actor-sheet modules |
| Pre-run creature dialog (07 §A) | T21 / separate async concern |
| Legion menu, map deep links beyond stub fields | world-map / legion |
| Multi-commander roster content (Penny) | empire program — plate fixture only today |
| Push-on-save cross-client sync | future match-session FSM |

---

## Module spec index

| Module | Spec |
|---|---|
| `default-persistence` | [spec-default-persistence.md](commander-surface/spec-default-persistence.md) |
| `commander-list-api` | [spec-commander-list-api.md](commander-surface/spec-commander-list-api.md) |
| `match-snapshot` | [spec-match-snapshot.md](commander-surface/spec-match-snapshot.md) |
| `commanders-layer` | [spec-commanders-layer.md](commander-surface/spec-commanders-layer.md) |
| `commander-sheet-role` | [spec-commander-sheet-role.md](commander-surface/spec-commander-sheet-role.md) |
| `sanctum-readout` | [spec-sanctum-readout.md](commander-surface/spec-sanctum-readout.md) |
| `lawn-hud-chip` | [spec-lawn-hud-chip.md](commander-surface/spec-lawn-hud-chip.md) |

Tasks: [tasks/commander-surface-plan.md](../../tasks/commander-surface-plan.md) ·
[tasks/commander-surface-todo.md](../../tasks/commander-surface-todo.md)

Program E2E owner: `web/fusion-rpg-web/e2e/commander-surface.spec.ts` (see plan P3).

---

## DESIGN-GATE checklist

- Read authoritative docs for UI + actor sheet + data + match + aura boundary: **yes**
- Verified against code (`CommanderId.cs`, `LoadoutEndpoints.cs`, `MatchHost.cs`, `CreaturesLayer.tsx`,
  `AuraRuntimeEndpoints.cs`, `RpgStore.Patron.cs`, `LawnHud.tsx`, `SanctumHome.tsx`): **yes**
  (agent verification 2026-08-30; strengthen pass 2026-08-30)
- Proposed changes: **map + module specs only** — no implementation in this pass
- Constraint tested (async default, no pre-run gate, empire boundary, snapshot, IA rail): **yes** — ideal §0
- Propagated to sibling artifacts: map, seven specs, plan, todo, bridge cross-link
- Strengthen pass complete: **yes** (2026-08-30)
