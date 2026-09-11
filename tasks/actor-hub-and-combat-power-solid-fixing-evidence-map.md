# Evidence map: actor-hub-and-combat-power-solid-fixing

**Plan / todo:** [plan](actor-hub-and-combat-power-solid-fixing-plan.md) · [todo](actor-hub-and-combat-power-solid-fixing-todo.md)
**Runbook:** [runbook](actor-hub-and-combat-power-solid-fixing-runbook.md)
**Purpose:** one row per acceptance criterion → the command that proves it → that command's
**executed** result → the artifact it produced. The ledger, not a summary, is the proof of done.

**Status:** all rows `PENDING` (approved 2026-09-12; no build started — owner start required).

> Rules for this file:
> - A row is `PASS` only if the command was run in the cycle that claims it, with a captured exit
>   code / tail. `N/A` needs a one-clause reason. A documented-but-unfixed defect is `FAIL`, never
>   "known".
> - Only `main` edits this file. Fan-out worktrees write `tasks/evidence-fragments/<task-id>.md`;
>   the rows are folded here at integration (see runbook §5).
> - Rows are `PENDING` until their command exists and was executed.

Legend: `PENDING` · `PASS` · `FAIL` · `N/A`

---

## Wave 1a — ChannelMods + Cold equip

### T1 — Star / Loyalty ChannelMods → Hub · spec `channelmods-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 1.1 | Star + Loyalty channels contribute via Hub/atoms (or DEBT shim, deleted in fuse) | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| 1.2 | Parity fixture: channel totals match pre-migration for same star/loyalty/level | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Star\|Loyalty"` | PENDING | — |
| 1.3 | ChannelMods allowlist no longer needs these producers (or lists only shim) | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| 1.4 | Server channel tests green (if applicable) | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ChannelMods\|Star\|Loyalty"` | PENDING | — |

### T2 — aptitude / Zomboss / draught / injury / kit → Hub · spec `channelmods-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 2.1 | UniqueDemon aptitude, Zomboss, draught, expedition injury, boss kit contribute via Hub/atoms | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude\|Draught\|Expedition\|BossBuild\|Zomboss"` | PENDING | — |
| 2.2 | Species aptitude via same Hub aptitude path, or proven unused/deleted | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude"` | PENDING | — |
| 2.3 | Full-set parity fixtures (Zomboss, draught, injury, boss kit, UniqueDemon aptitude) | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude\|Draught\|Expedition\|BossBuild\|Zomboss"` | PENDING | — |
| 2.4 | No production path requires `BattleChannelMod` for these after fuse (shim OK until T6) | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| 2.5 | Server `ChannelMods\|Aptitude\|BuildSquad` green | `dotnet test tests/FusionRpg.Server.Tests` | PENDING | — |

### T3 — Sole Cold equip path = rolled / atom bindings · spec `cold-equip-one`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 3.1 | Documented sole Cold materialize path is rolled/atom bindings | `rg -n "rolled\|EquippedBoundAtoms" src` + doc read | PENDING | — |
| 3.2 | Equip → Hub Derived proves change **without** BattleStatComposer | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom\|EquippedBound"` | PENDING | — |
| 3.3 | No third equip fold introduced | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| 3.4 | SourceIds use `equip:{role}:{itemRef}` (GG-49) on sheet | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom\|EquippedBound"` | PENDING | — |
| 3.5 | Single rebuild: no dual `mods_json` SSOT beside atoms | `.\scripts\guard-single-writer.ps1` | PENDING | — |
| 3.6 | Rolled (`ref_kind = rolled`) equip produces Hub-visible `stat.derived`, ops honored | `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment\|Equipped\|AtomBinding"` | PENDING | — |

### T4 — Retire stub catalog as production SSOT · spec `cold-equip-one`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 4.1 | Stub `Items` deleted or test-only with DEBT + no production caller | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| 4.2 | Player equip/unequip does not depend on stub templates | `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment"` | PENDING | — |
| 4.3 | Player equip API does not default to stub catalog rows | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |

### Checkpoint: Wave 1a

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP1a.1 | ChannelMods combat writers migrated or shimmed with DEBT | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| CP1a.2 | ChannelMods allowlist only DEBT shims (or empty) | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| CP1a.3 | Cold equip path is atom/rolled; stub not SSOT | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| CP1a.4 | Guard + focused tests green | `.\scripts\guard-actor-hub.ps1` + filters above | PENDING | — |

---

## Wave 1b — Fuse + ops

### T5 — BattleEngine compose via ActorHub · spec `battle-hub-fuse`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 5.1 | `BattleEngine` reads Hub Derived only (no Compose call) | `rg -n "BattleStatComposer" src` | PENDING | — |
| 5.2 | Bound vs empire aptitude identity matches `UniqueActorHubCompose` rules | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"` | PENDING | — |
| 5.3 | Delve/siege/web paths inherit Hub | `dotnet test tests/FusionRpg.Server.Tests` | PENDING | — |
| 5.4 | Baseline flats / tempo / resources via Hub; seed parity documented | Compose↔Hub parity fixtures | PENDING | — |
| 5.5 | Pre-delete parity matrix green channel-for-channel | Compose↔Hub parity fixtures | PENDING | — |
| 5.6 | `AptitudeResolver.ResolveForBattle` retired or reduced to Hub-only | `rg -n "ResolveForBattle" src` | PENDING | — |

### T6 — Delete BattleStatComposer + RulesetVersion bump · spec `battle-hub-fuse`

> **Owner sign-off required before this task runs** (only irreversible step).

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 6.1 | No production `BattleStatComposer.Compose` under `src/` | `rg -n "BattleStatComposer" src` | PENDING | — |
| 6.2 | Guard updated; ChannelMods allowlist empty (or justified) | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| 6.3 | One RulesetVersion bump + triage notes; unrelated goldens frozen | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden"` | PENDING | — |
| 6.4 | Docs mark dual-compose debt retired | `rg -n "dual.compose\|composers stay separate" docs` | PENDING | — |
| 6.5 | T5 parity remains green before delete | Compose↔Hub parity fixtures | PENDING | — |
| 6.6 | Goldens re-blessed once | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition\|Golden\|Battle"` | PENDING | — |

### T7 — Battle ops + tree via Hub; retire TreeAtomSource slot · spec `battle-ops-parity`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 7.1 | Battle equip path Hub op-aware only | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|AtomDerived\|Battle"` | PENDING | — |
| 7.2 | `EquipAtomSource.ModsFor` ignore-op battle fold removed | `rg -n "ModsFor" src` | PENDING | — |
| 7.3 | Unknown op skipped visibly — not coerced to flat | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|AtomDerived"` | PENDING | — |
| 7.4 | Tree reaches battle actors via Hub when bindings exist | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TreeAtom\|Battle"` | PENDING | — |
| 7.5 | Unused `TreeAtomSource` Compose slot gone | `rg -n "TreeAtomSource" src` | PENDING | — |
| 7.6 | AtomKind Battle Full for `stat.derived` matches tests | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~AtomKind\|AtomDerived"` | PENDING | — |

### Checkpoint: Wave 1 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP1.1 | Dual compose retired; guard green | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| CP1.2 | Ops parity Done | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip\|TreeAtom\|AtomDerived\|Battle"` | PENDING | — |

---

## Wave 2 — Standing honesty

### T8 — CombatPowerMembership predicate · spec `combat-membership`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 8.1 | `CombatPowerMembership` (or named peer) with include/exclude tests | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CombatPowerMembership"` | PENDING | — |
| 8.2 | No Standing path uses `IsCombatChannel` alone | `rg -n "IsCombatChannel" src` | PENDING | — |
| 8.3 | Documented list matches map assumption §7 | doc read (map §7 vs code) | PENDING | — |

### T9 — ProjectStanding synthetics + filter · spec `standing-compose`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 9.1 | Standing includes aptitude / Hub combat writers via synthetics + filter | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Standing\|ProjectStanding\|ActorPower"` | PENDING | — |
| 9.2 | Double-count proven absent for equip/tree | same filter | PENDING | — |
| 9.3 | `progression.*` / Θ do not raise Standing | same filter | PENDING | — |
| 9.4 | Five-axis DTO unchanged | same filter | PENDING | — |
| 9.5 | Cooldown (or other non-atom Hub membership channel) raises Standing | same filter | PENDING | — |
| 9.6 | Still `ActorPowerCache.Compose(AtomRow[])` only — no Hub-snapshot overload | `rg -n "ActorPowerCache.Compose" src` | PENDING | — |

### T10 — Chip honesty — Lv / Θ, never "power" · spec `chip-honesty` (FE)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 10.1 | `scopeFiction` / aptitude chrome free of level/Θ labeled "power" | `npm test -- --run foldAptitudesSurfaceVm` (cwd `web/fusion-rpg-web`) | PENDING | — |
| 10.2 | Tests lock new copy (`Lv` / optional `Θ` only) | `npm test -- --run foldAptitudesSurfaceVm` | PENDING | — |
| 10.3 | Combat power number stays on Condition / copy-surfaces | `npm test -- --run foldAptitudesSurfaceVm` | PENDING | — |
| 10.4 | Tick HF-chip on aptitude-sheet / combat-power ideal Done checklists | doc read + checkbox diff | PENDING | — |
| 10.5 | Playwright E2E: chip shows `Lv`/`Θ`, never "power" | `npm run test:e2e` | PENDING | — |
| 10.6 | Screenshots inspected at desktop / tablet / mobile widths | Playwright MCP + CV inspection | PENDING | — |

### T11 — Copy surfaces — combat power = O+S+C · spec `copy-surfaces` (FE)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 11.1 | Shared O+S+C helper wired | `npm test -- --run foldConditionSurfaceVm` | PENDING | — |
| 11.2 | Utility/Economy excluded from "combat power" string | `npm test -- --run foldConditionSurfaceVm` | PENDING | — |
| 11.3 | No sheet copy treats `combat.power.omni` alone as combat power | `rg -n "combat.power.omni" web/fusion-rpg-web/src` | PENDING | — |
| 11.4 | Five-axis vector retained for inspect | `npm test -- --run foldConditionSurfaceVm` | PENDING | — |
| 11.5 | No `PowerScalar` on UniqueActor Standing path | `rg -n "PowerScalar" web/fusion-rpg-web/src` | PENDING | — |
| 11.6 | Tick HF-copy on ideal / aptitude-sheet Done checklists | doc read + checkbox diff | PENDING | — |
| 11.7 | Playwright E2E + inspected screenshots (desktop/tablet/mobile) | `npm run test:e2e` + Playwright MCP | PENDING | — |

### Checkpoint: Wave 2 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP2.1 | Standing honest; chip/copy Done | `Standing` filter + `foldAptitudesSurfaceVm` + `foldConditionSurfaceVm` | PENDING | — |

---

## Wave 3 — Lawn / loadout

### T12 — Lawn aptitude parity Done gate · spec `lawn-aptitude-parity` (+ `unique-lawn-wire`)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 12.1 | After unique allocate + AptitudesUpdated, Bound unique Hot includes UniqueDemon shares | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~SpeciesAllocation\|UniqueDemon\|Bound"` | PENDING | — |
| 12.2 | Fetch path is unique GET only (S4) | same filter | PENDING | — |
| 12.3 | General lawn demons unchanged (species path) | same filter | PENDING | — |
| 12.4 | Regression: unique sharing species id with a general does not inherit empire allocation | same filter | PENDING | — |
| 12.5 | Parity prove: Bound lawn input matches Server UniqueDemon compose | same filter + live probe | PENDING | — |
| 12.6 | HF-lawn ticked on ideal / maps | doc read + checkbox diff | PENDING | — |
| 12.7 | aptitude-sheet `unique-lawn-wire` Done before closing | doc read (`aptitude-sheet-todo`) | PENDING | — |
| 12.8 | `guard-secondary-no-unity` green | `.\scripts\guard-secondary-no-unity.ps1` | PENDING | — |

### T13 — Injector PassiveTree → Hub · spec `lawn-tree-hydrate`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 13.1 | Injector Hub includes tree bound atoms when tree state exists | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TreeBound\|PassiveTree\|AtomDerived"` | PENDING | — |
| 13.2 | Parity with Server sheet tree fan-in for same playerId | same filter | PENDING | — |
| 13.3 | Named "injector tree hydrate" gap closed in comments/docs | `rg -n "injector tree hydrate" src docs` | PENDING | — |
| 13.4 | Reload refreshes tree bounds | same filter | PENDING | — |
| 13.5 | Guards green | `.\scripts\guard-secondary-no-unity.ps1` + `.\scripts\guard-actor-hub.ps1` | PENDING | — |

### T14 — Bound loadout via Hub + Funnel · spec `bound-loadout-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 14.1 | `ApplyAbsolutes` combat path removed (or non-combat leftovers with owner sign-off) | `rg -n "ApplyAbsolutes" src` + owner note | PENDING | — |
| 14.2 | Bound loadout visible on Hub Derived / AppliedCombat | `dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~UniqueBound\|Loadout"` | PENDING | — |
| 14.3 | HP via Funnel Add / preserve-ratio — not `mode=set` current HP | `.\scripts\guard-funnel-delta.ps1` | PENDING | — |
| 14.4 | No type-wide `plant:N` loadout keys | `rg -n "plant:" src/FusionRpg.Injector` | PENDING | — |
| 14.5 | Each former absolute key maps to Hub channel or Funnel grant — no silent drop | same filter + mapping table | PENDING | — |
| 14.6 | Guards green | `guard-single-writer` + `guard-funnel-delta` + `guard-actor-hub` | PENDING | — |
| 14.7 | HF-bound-loadout ticked on ideal | doc read + checkbox diff | PENDING | — |

### Checkpoint: Wave 3 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP3.1 | Lawn UniqueDemon + tree + Bound loadout Done | Wave 3 filters + guards | PENDING | — |

---

## Wave 4 — Place-matrix / D4 / prove

### T15 — Sim combat Full via Hub · spec `sim-hub-parity`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 15.1 | Sim combat equip ops match Hub semantics | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorDerived\|SimEffect\|AtomKind\|Sim"` | PENDING | — |
| 15.2 | Named Partial for combat `stat.derived` retired or narrowly exempted | same filter | PENDING | — |
| 15.3 | Ideal place matrix Sim row updated | doc read + diff | PENDING | — |

### T16 — Standing coeff tuning (D4) · spec `standing-coeff-tuning`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 16.1 | Tunable family/mask coeffs live and loaded | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorPower\|Coefficient\|Standing"` | PENDING | — |
| 16.2 | High dodge still raises combat power; Survivability axis improved | same filter | PENDING | — |
| 16.3 | Magic-number audit clean on new Policy surfaces | `python scripts/audit-magic-numbers.py --summary` | PENDING | — |
| 16.4 | Missing coeff row → load reject or documented structural default | same filter | PENDING | — |
| 16.5 | Standing vector fixtures re-blessed once if they moved | same filter + golden note | PENDING | — |

### T17 — Unique Θ on wire · spec `unique-theta-wire` (FE wire)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 17.1 | Unique wire carries real theta when known | `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~Aptitude\|UniqueActor\|Theta"` | PENDING | — |
| 17.2 | Chip shows `Θ` only from wire | `npm test -- --run foldAptitudesSurfaceVm` | PENDING | — |
| 17.3 | No `theta ?? specimenLevel` on aptitude scope path | `rg -n "specimenLevel" src web/fusion-rpg-web/src` | PENDING | — |
| 17.4 | Playwright E2E + inspected screenshots (desktop/tablet/mobile) | `npm run test:e2e` + Playwright MCP | PENDING | — |

### T18 — Stale dual-compose docs · spec `stale-compose-docs`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 18.1 | Blessing phrases gone or clearly historical | `rg -n "composers stay separate\|locked separate from ActorHub\|BattleStatComposer stays\|adapters OK" docs src --glob "!**/bin/**" --glob "!**/obj/**"` | PENDING | — |
| 18.2 | §8.3 / decisions reflect fuse outcome | doc read + diff | PENDING | — |
| 18.3 | Map Done checkbox for stale docs | doc read + diff | PENDING | — |
| 18.4 | Production code comments / `EquipAtomSource` dual-compose prose overturned | same `rg` on `src` | PENDING | — |

### T19 — Prove Hub combat script · spec `prove-hub-combat`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 19.1 | Script exists and documented (`prove-hub-combat.ps1` or successor) | `Test-Path scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.2 | Post-fuse battle Hub totals ≡ sheet Hub for same inputs (equip/aptitude/tree) | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.3 | Standing rises when a membership combat channel rises — not when only Θ rises | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.4 | Bound lawn aptitude input matches Server UniqueDemon compose | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| 19.5 | Ideal handoff prove path checked / runbook linked | doc read | PENDING | — |

### Checkpoint: Wave 4 complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CP4.1 | Sim / coeffs / Θ / docs / prove green | Wave 4 filters + `prove-hub-combat` + audit | PENDING | — |

---

## Wave 5 — Stub hygiene

### T20 — Delete PlaceholderBattleResolver + feature-off assaults · spec `placeholder-battle-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 20.1 | `PlaceholderBattleResolver` removed from production paths | `rg -n "PlaceholderBattleResolver" src` | PENDING | — |
| 20.2 | `PlaceholderBattleTuning` deleted or unread | `rg -n "PlaceholderBattleTuning" src` | PENDING | — |
| 20.3 | No silent Hp×Level combat outcomes | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~PlaceholderBattle\|DistrictAssault\|TurnEngine\|World"` | PENDING | — |
| 20.4 | World tests re-blessed for feature-off / fail-loud | same filter | PENDING | — |
| 20.5 | Guard green | `.\scripts\guard-actor-hub.ps1` | PENDING | — |

### T21 — Drop intel Strength from placeholder weight · spec `placeholder-battle-hub` (B4)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 21.1 | `IntelRecorder` / `IntelSeed` do not call deleted Strength | `rg -n "PlaceholderBattleResolver\.Strength" src` | PENDING | — |
| 21.2 | Bands not fed by Hp×Level fiction | `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Intel"` | PENDING | — |
| 21.3 | Intel tests updated | same filter | PENDING | — |

### T22 — Fold Level-as-Θ aliases (O2) + stub equip leftover sweep

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 22.1 | No production Level-as-Θ alias on battle/delve aptitude paths | `rg -n "ActorThetaSeam\|theta \?\? specimenLevel\|Level as Θ" src` | PENDING | — |
| 22.2 | Stub equip not player-usable SSOT (align T4) | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| 22.3 | Comments point real Θ / Hub only | `rg -n "ActorThetaSeam\|Level as Θ" src` | PENDING | — |

### T23 — Track `world-actor-combat` in docs · spec `placeholder-battle-hub`

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| 23.1 | Map Out of scope / Tracked names `world-actor-combat` | doc read + diff | PENDING | — |
| 23.2 | Ideal / Wave 5 Done checkboxes honest | doc read + diff | PENDING | — |
| 23.3 | No module under this program claims world combat engine Done | doc read + diff | PENDING | — |

### Checkpoint: Program complete

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| CPP.1 | All Wave 1–5 acceptance criteria met | all rows above `PASS`/`N/A` | PENDING | — |
| CPP.2 | Program map "Done when" checkboxes tickable | doc read + diff | PENDING | — |
| CPP.3 | Ideal + aptitude-sheet Done checkboxes cross-linked | doc read + diff | PENDING | — |
| CPP.4 | Goldens re-blessed once under fuse RulesetVersion bump | `Golden` filter row of T6.6 | PENDING | — |
| CPP.5 | `world-actor-combat` tracked only | doc read + diff | PENDING | — |
| CPP.6 | Owner accepts program close | owner note | PENDING | — |

---

## Program Done when (from map)

| # | Criterion | Command | Executed result | Artifact |
|---|---|---|---|---|
| PD.1 | No production `BattleStatComposer.Compose` under `src/` | `rg -n "BattleStatComposer" src` | PENDING | — |
| PD.2 | No new private ChannelMods combat writers; known producers migrated | `.\scripts\guard-actor-hub.ps1` | PENDING | — |
| PD.3 | Cold equip rolled/atom — stub not SSOT | `rg -n "stub\.atk_ring\|butter_bead\|hp_charm" src` | PENDING | — |
| PD.4 | Standing membership + synthetics; chip never labels level "power" | `Standing` filter + `foldAptitudesSurfaceVm` | PENDING | — |
| PD.5 | Bound lawn UniqueDemon + Bound loadout via Hub | Wave 3 filters | PENDING | — |
| PD.6 | Sim Full; D4 coeffs; unique Θ; stale docs gone | Wave 4 rows | PENDING | — |
| PD.7 | `prove-hub-combat` green | `.\scripts\prove-hub-combat.ps1` | PENDING | — |
| PD.8 | Placeholder + intel Strength deleted; `world-actor-combat` tracked | Wave 5 rows | PENDING | — |
| PD.9 | Ideal + aptitude-sheet Done checkboxes cross-linked | doc read + diff | PENDING | — |
| PD.10 | Goldens re-blessed once under fuse RulesetVersion bump | T6.6 | PENDING | — |

---

## Folded fragments

Fragments folded from fan-out worktrees (runbook §5). Each line is a consumed
`tasks/evidence-fragments/<task-id>.md`.

_(none yet)_
