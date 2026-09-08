# Status coverage audit — status-rail

**Date:** 2026-09-09 (Wave C completeness pass)  
**Program:** `status-rail`  
**Scope:** Battle / lawn / derived reach vs the 24-id status catalog.

## Id set

| Source | Count | Role |
|---|---|---|
| `data/tuning/status-catalog.v1.json` | 24 | Surface + combat inject |
| `StatusCatalogBootstrap.CreateDefault()` | 24 | Migration golden / shim |

Parity: `StatusCatalogParityTests` — ids, kinds, stacking, family, categories, payloads, L2b registry.

## Gap tracker

| Gap | Status | Evidence |
|---|---|---|
| Dual catalog | **Closed (C3)** | Inject via Hub; AtomKindRegistry / FoundationHarness / SimEffectHost use `StatusCatalogHub.Current`; Bootstrap = fallback + golden |
| Battle status-instance StatMods | **Closed (C1)** | OnApplied/OnEnded + **death/retreat** `WithdrawStatusHost` / lawn `TakeHostInstances` StatMod teardown without `OnEnded` (VFX contract) |
| Match ClearAll status mods | **Closed (C1)** | `WithdrawAllBySourceKind("status")` after Bag clear |
| Lawn UnityCc apply/clear | **Closed (C5)** | Clear covers ember/hypno/kelp; **jala** only unclearable (`StatusUnityClearGuardTests`); plant butter-only |
| Contagion battle board | **Closed (C2)** | `RefreshCombatBoardSnapshot` before each Status.Tick; hop tests on adapter sides `squad`/`wave`; boardless → no hop. Overlay filters using `zombie`/`plant` do not see battle neighbors |
| `bond` Counter + PulseHp | **Closed** | No PulseHp; nested burst only |
| `StatusDef.Tags` | **Closed** | Grant-only immunity (status-ssot) |
| Derived Status rail Omni+24 | **Closed (C4)** | Cook 25 variants; FE resist cap for per-id; catalog chip paint; Core/Server asserts 25 |
| `nerve.*` VFX recipes | Open (VFX stream) | Out |

## Decisions locked

1. Status cook = **Omni + 24** (not hybrid).
2. Death does **not** fire `OnEnded`; callers tear down StatMods from `TakeHostInstances`.
3. Battle contagion sides are **`squad` / `wave`** (adapter), not lawn `zombie` / `plant`.
4. Unclearable Unity: **jala** only.
