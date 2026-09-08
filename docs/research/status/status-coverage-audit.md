# Status coverage audit — status-rail Wave 0

**Date:** 2026-09-09  
**Program:** `status-rail`  
**Scope:** Battle / lawn / derived reach vs the 24-id status catalog.

## Id set

| Source | Count | Role |
|---|---|---|
| `data/tuning/status-catalog.v1.json` | 24 | Surface + combat inject |
| `StatusCatalogBootstrap.CreateDefault()` | 24 | Migration golden / shim |

Parity test: `StatusCatalogParityTests` — JSON ∩ Bootstrap ids equal.

## Gap tracker

| Gap | Status | Wave |
|---|---|---|
| Dual catalog (Bootstrap vs JSON; empty payloadKinds) | **Closed** | B1 |
| Battle status-instance StatMods unread | **Closed** | B2 |
| Lawn UnityCc apply/clear asymmetry | **Closed** (jala unclearable; plant butter-only policy) | B3 |
| Contagion battle board | **Closed** — `BattleEngine` passes `state.CombatBoardSnapshot` when a board exists; boardless battles correctly pass null | B4 |
| `bond` Counter + PulseHp mismatch | **Closed** — strip PulseHp; nested burst only | B4 |
| `StatusDef.Tags` empty vs SSOT | **Closed** — SSOT: immunity is grant-only | B4 |
| Derived Status rail = 4 categories | **Closed** — Omni+24 status-id | A1–A2 |
| `nerve.*` VFX recipes | Open (VFX stream) | Out |

## Reach (post-close)

- **Lawn:** `StatusRuntime` + Funnel pulses; UnityCc via `DebugActions` (8 wraps); StatMods via `EffectRuntime.OnApplied`. Clear covers butter/freeze/cold/poison/ember/hypno/kelp; **jala** refuses Unity clear (RPG instance still ends).
- **Battle:** `status.apply` → `StatusRuntime`; FA1 `ModifyStat` via ledger; **status-instance StatMods** projected in `BattleRunState` OnApplied/OnEnded.
- **Derived:** Status cook rail = Omni + 24 status ids; category channels remain legal in combat math / search.

## Decisions locked this program

1. Status cook expand = **`status-id`** with **Omni + 24** chips (not hybrid with category chips).
2. **`bond`:** no `PulseHp` in catalog — Counter does not tick-pulse; burst remains nested.
3. **Tags:** stay empty on defs; immunity remains **per-grant** (status-ssot amended).
4. Unclearable Unity flags: **jala** only; ember/hypno/kelp clear attempted; plant-side still butter-only by host limit.
