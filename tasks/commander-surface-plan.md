# Commander surface — implementation plan



**Program:** `commander-surface` · **Map:** [docs/architecture/commander-surface-map.md](../docs/architecture/commander-surface-map.md) ·

**Ideal:** [docs/architecture/commander-surface-ideal.md](../docs/architecture/commander-surface-ideal.md) ·

**Tasks:** [commander-surface-todo.md](commander-surface-todo.md)



**Status:** proposed — pending owner review. **No build authorized** until map + specs approved.



**Strengthen pass:** 2026-08-30 — SSOT endpoints, E2E gate, snapshot/bridge rules, Aptitudes demotion.



---



## Goal



Ship the commander surface: persisted default lawn commander, Commanders layer (`K`), Sanctum readout,

lawn HUD identity chip, match snapshot at `board.start`, and commander role on shared `ActorPanel` —

**without** a pre-run web gate and **without** duplicating aura-skill or actor-sheet programs.



**IA-strict:** Commanders replaces Aptitudes on the rail (nine layers); Aptitudes is sheet-only via

Progression tab.



---



## Program acceptance (end-to-end)



Starting a lawn run with saved default Dave and an active aura shows Dave + aura on Sanctum and lawn HUD.

Changing default in the Commanders layer updates the **next** run only. Mid-run HUD stays on the snapshot

from `board.start`.



> Until **Playwright** `e2e/commander-surface.spec.ts` asserts this exactly, the program is **not done**.



---



## Endpoint SSOT



See [commander-surface-map.md](../docs/architecture/commander-surface-map.md) **Endpoint and DTO SSOT**

table. Summary:



- Single `CommanderEndpoints.cs`: `GET /api/commanders/{playerId}`, `POST /api/commanders/default`

- Active aura in list = **loadout ∩ AuraRuntime** (real catalog ids: `"Might"`, …)

- Snapshot fields: `LeadingCommanderId`, display name, aura, **allocation + revision**



---



## Phases



### P0 — Owner review (gate)



- Review [commander-surface-map.md](../docs/architecture/commander-surface-map.md) module boundaries

- Review seven module specs in [commander-surface/](../docs/architecture/commander-surface/) (strengthen pass)

- Confirm cross-program seams with aura-skill and actor-sheet owners

- Confirm **IA-strict Aptitudes demotion** (Commanders `K` on rail; Aptitudes sheet-only)



**Exit:** owner approves map + specs; todo checkboxes for "spec approved" ticked



### P1 — Persistence + list API



Modules: `default-persistence` → `commander-list-api`



| Step | Work |

|---|---|

| P1.1 | `rpg_player_commander` table + DAL get/set + `CommanderIds.TryParseStableId` |

| P1.2 | `GET/POST` default in `CommanderEndpoints.cs` |

| P1.3 | `GET /api/commanders/{playerId}` + empire filter + active aura algorithm + DTOs |

| P1.4 | Data + server tests; `guard-dal.ps1` |



**Exit:** list API returns Dave + default + Might when equipped/enabled; POST default round-trips



### P2 — Match snapshot (injector)



Module: `match-snapshot` (parallel with P1 after P1.1)



| Step | Work |

|---|---|

| P2.1 | `MatchCommanderSnapshot` + holder (allocation + revision) |

| P2.2 | Session cache refresh + synchronous freeze in `MatchHost.Apply` before `NotifyMatchStart` |

| P2.3 | Clear on `board.end`, `match.result`, auto-end-before-start |

| P2.4 | `debug.snapshot` → `match.commander` observe DTO |

| P2.5 | Bridge immutability: ignore live aptitude cache during `InMatch` |

| P2.6 | Core + injector tests — mid-match freeze |



**Exit:** snapshot frozen per match; mid-match default/allocation change ignored until next start



**Soft dependency:** `/api/loadout` + `/api/aura-runtime` for aura on snapshot; full lawn effect requires aura-skill delivery



### P3 — FE layer + readouts + program E2E



Modules: `commanders-layer`, `sanctum-readout`, `commander-sheet-role`, `lawn-hud-chip`



| Step | Work | Depends |

|---|---|---|

| P3.1 | Rail demotion: Commanders `K`; remove Aptitudes from rail + hotkey | P1 |

| P3.2 | `CommandersLayer` + bus hooks | P1 |

| P3.3 | Sanctum Leading line in `SanctumHome.tsx` | P1 |

| P3.4 | Commander `ActorPanel` footer/role | P3.2 + `actor-sheet-shell` |

| P3.5 | Lawn HUD chips via `debug.snapshot` fold in `LawnHud.tsx` | P2 |

| P3.6 | **`e2e/commander-surface.spec.ts`** — program acceptance owner | P3.1–P3.5 |



**Exit:** Playwright program acceptance green; lawn HUD shows snapshot chips in live deploy



### P5 — Polish (deferred from P3/P4)



| Step | Work |

|---|---|

| P5.1 | Deep-link `?panel=commanders&sel=` auto-opens commander sheet |

| P5.2 | Lawn HUD tap-to-sheet + GG-60 this-match banner / next-run Set default label |



---



## Build order (dependency)



```text

default-persistence

  → commander-list-api ─┬→ commanders-layer → commander-sheet-role

                        └→ sanctum-readout

  → match-snapshot → lawn-hud-chip

```



---



## Cross-program coordination



| Partner program | Module | Relationship |

|---|---|---|

| aura-skill | `aura-surface` | Commander Actions tab composes aura UI — do not fork |

| aura-skill | `aura-equip-path` | Loadout writes; commander-surface reads into snapshot |

| aura-skill | `commander-lawn-bridge` | Allocation from snapshot holder during `InMatch` |

| aura-skill | `aura-delivery-path` | Aura grants consume snapshot active aura |

| actor-sheet | `actor-sheet-shell` | Prerequisite for production commander sheet role |

| actor-sheet | `progression-tab` | Commander aptitudes canonical on Progression tab |

| T21 / plate 07 | — | Creature berths separate; not a commander gate |



---



## Verification (run after each phase)



```powershell

dotnet test tests\FusionRpg.Data.Tests

dotnet test tests\FusionRpg.Server.Tests

dotnet test tests\FusionRpg.Core.Tests

dotnet test tests\FusionRpg.Guard.Tests

.\scripts\guard-dal.ps1

.\scripts\guard-single-writer.ps1

.\scripts\guard-funnel-delta.ps1



cd web\fusion-rpg-web

npm run test

npm run build

npx playwright test e2e/commander-surface.spec.ts

```



Live (owner): `.\scripts\deploy-play.ps1 -NoServer` after injector changes; server from owner terminal

if REST changed.



Post-strengthen grep (docs): no `"Sun Blessing"`, no GET/PUT default drift, no banned commander verbs.



---



## Refusals (carry through implement)



- No pre-run commander picker or web gate

- No *Set for lawn run* / *Deploy to lawn* on commander surfaces

- No Zomboss in player list API

- No aura math or delivery in this program's code paths

- No Aptitudes on rail when Commanders ships (IA-strict)


