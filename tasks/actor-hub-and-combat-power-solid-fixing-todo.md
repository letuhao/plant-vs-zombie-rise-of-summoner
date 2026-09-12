# Todo: actor-hub-and-combat-power-solid-fixing

**Plan:** [actor-hub-and-combat-power-solid-fixing-plan.md](actor-hub-and-combat-power-solid-fixing-plan.md)  
**Map:** [docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md](../docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md)  
**Runbook / evidence:** [runbook](actor-hub-and-combat-power-solid-fixing-runbook.md) · [evidence map](actor-hub-and-combat-power-solid-fixing-evidence-map.md) · command `/solid-run`  
**Status:** Ready for review — specs approved; ACs amended 2026-09-12 to match Success criteria (coverage audit). No build until owner says start.

---

## Ask-first defaults (non-blocking)

| Topic | Ship with |
|---|---|
| RulesetVersion | Single bump on fuse |
| D4 coeffs | Family/mask first |
| Assault off | Fail loud / no invented winner |
| Tracked program id | `world-actor-combat` |
| Tree into injector | Prefer Server-fed or existing seed path over shipping a second tuning fork — document choice in T13 |
| E9 Compose API | Unchanged |
| Baseline seed formulas | Re-home only — do not change formulas without ask |
| New SourceId grammar families | Ask first — reuse GG-49 families already in use |

---

## Wave 1a — ChannelMods + Cold equip

### Task 1: Migrate Star / Loyalty ChannelMods → Hub

**Spec:** `channelmods-hub`  
**Description:** Re-home `WebMatchService.StarChannelMods` / `LoyaltyChannelMods` as Hub subsystems or atom contributions with GG-49 SourceIds so battle and sheet share the same writers.

**Acceptance criteria:**
- [ ] Star and Loyalty combat channels contribute via Hub/atoms (or one-release shim tagged `// DEBT — channelmods-hub` deleted in fuse).
- [ ] Parity fixture: channel totals match pre-migration ChannelMods for same star/loyalty/level.
- [ ] Guard ChannelMods allowlist no longer needs these producers after migration (or lists only shim).

**Verification:**
- [ ] `.\scripts\guard-actor-hub.ps1`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Star|Loyalty"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ChannelMods|Star|Loyalty"` (if applicable)

**Dependencies:** None  
**Files likely touched:** `WebMatchService.cs`, new/updated `IActorStatSubsystem`, tests  
**Estimated scope:** M

---

### Task 2: Migrate aptitude / Zomboss / draught / injury / kit → Hub

**Spec:** `channelmods-hub`  
**Description:** Finish ChannelMods combat writers: UniqueCreature aptitude, Zomboss pattern, draught projection, expedition injury, boss kit — all Hub/atoms.

**Acceptance criteria:**
- [ ] UniqueCreature aptitude, Zomboss, draught, expedition injury, and boss kit contribute through Hub/atoms.
- [ ] Species aptitude (`AptitudeChannelMods`) contributes via the same Hub aptitude path **or** is proven unused/deleted.
- [ ] Parity tests prove channel totals match pre-migration ChannelMods for the same fixtures — coverage for **Zomboss, draught, expedition injury, and boss kit** (full set), plus UniqueCreature aptitude.
- [ ] No production path **requires** `BattleChannelMod` for these after fuse (shim OK until T6).

**Verification:**
- [ ] `.\scripts\guard-actor-hub.ps1`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude|Draught|Expedition|BossBuild|Zomboss"`
- [ ] Server filter `ChannelMods|Aptitude|BuildSquad` green

**Dependencies:** T1 recommended (same seam)  
**Files likely touched:** `WebMatchService.cs`, `AptitudeResolver`, `DraughtProjection`, `ExpeditionResolver`, `BossBuild`, Hub subsystems  
**Estimated scope:** L → keep focused; if overrun, split kit vs injury in session notes without new map module

---

### Task 3: Sole Cold equip path = rolled / atom bindings

**Spec:** `cold-equip-one`  
**Description:** Document and wire player equip materialize through `EquippedBoundAtoms` / store reconcile; Hub reads that path only.

**Acceptance criteria:**
- [ ] Documented sole Cold materialize path is rolled/atom bindings.
- [ ] Equip → Hub Derived combat channel change proven **without** BattleStatComposer.
- [ ] No third equip fold introduced.
- [ ] SourceIds use `equip:{role}:{itemRef}` (GG-49) on sheet (and post-fuse battle via Hub).
- [ ] Single rebuild: equip/unequip reconcile only — no dual `mods_json` SSOT beside atoms.
- [ ] Rolled (`ref_kind = rolled` or successor) equip produces Hub-visible `stat.derived` with ops honored on Hub path.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment|Equipped|AtomBinding"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom|EquippedBound"`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** None (parallel with T1–T2)  
**Files likely touched:** `UniqueActorService`, `EquippedBoundAtoms`, `EquipAtomSource`, Data reconcile  
**Estimated scope:** M

---

### Task 4: Retire stub catalog as production SSOT

**Spec:** `cold-equip-one` (align Wave 5 delete)  
**Description:** Remove production callers of `UniqueEquipmentCatalog` stub Items; stubs test-only with DEBT or deleted.

**Acceptance criteria:**
- [ ] Stub `Items` deleted or test-only with DEBT + no production caller.
- [ ] Player equip/unequip does not depend on `stub.atk_ring` / butter_bead / hp_charm templates.
- [ ] Player equip API does not default to stub catalog rows.

**Verification:**
- [ ] `rg -n "stub\\.atk_ring|butter_bead|hp_charm" src` — no production hits (tests OK)
- [ ] Data/Core equip tests green

**Dependencies:** T3  
**Files likely touched:** `UniqueEquipmentCatalog.cs`, fixtures, Server equip endpoints  
**Estimated scope:** M

---

## Checkpoint: Wave 1a

- [ ] ChannelMods combat writers migrated or shimmed with DEBT
- [ ] ChannelMods allowlist only DEBT shims (or empty for migrated producers)
- [ ] Cold equip path is atom/rolled; stub not SSOT
- [ ] Guard + focused tests green
- [ ] Owner glance before fuse (T5)

---

## Wave 1b — Fuse + ops

### Task 5: BattleEngine compose via ActorHub

**Spec:** `battle-hub-fuse`  
**Description:** Replace `BattleStatComposer.Compose` at `BattleEngine` (and delve/siege/web callers) with ActorHub Resolve/ResolveDerived + AppliedCombat merge. Aptitude identity matches sheet (Bound UniqueCreature vs empire species).

**Acceptance criteria:**
- [ ] `BattleEngine` reads Hub Derived only (no Compose call).
- [ ] Bound vs empire aptitude identity matches `UniqueActorHubCompose` rules.
- [ ] Delve/siege/web paths that inherit BattleEngine follow Hub.
- [ ] Baseline combat flats / tempo / resources contribute via Hub baseline subsystem(s); seed parity with old `BattleStatComposer` seeds documented.
- [ ] Pre-delete parity matrix: old Compose vs Hub Resolve channel-for-channel on fixtures (green before T6 delete).
- [ ] `AptitudeResolver.ResolveForBattle` retired or reduced to Hub-only path when fuse lands.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"`
- [ ] Compose↔Hub parity fixtures green
- [ ] `.\scripts\guard-actor-hub.ps1` (may still allowlist composer until T6)

**Dependencies:** T1–T4  
**Files likely touched:** `BattleEngine.cs`, battle setup / baseline seed helpers, `AptitudeResolver`, Server web/delve/siege  
**Estimated scope:** L

---

### Task 6: Delete BattleStatComposer + RulesetVersion bump

**Spec:** `battle-hub-fuse`  
**Description:** Remove production `BattleStatComposer`; empty ChannelMods combat allowlist; one `RulesetVersion` bump with golden triage; mark dual-compose debt retired in decisions / actor-hub-ssot §8.3.

**Acceptance criteria:**
- [ ] No production `BattleStatComposer.Compose` under `src/`.
- [ ] Guard updated; ChannelMods allowlist empty (or non-combat leftovers justified).
- [ ] One RulesetVersion bump + triage notes; freeze unrelated golden streams per `decisions.md` ordering during the bump.
- [ ] Docs mark dual-compose debt **retired** (final polish may wait T18).
- [ ] T5 Compose↔Hub parity remains green before delete.

**Verification:**
- [ ] T5 parity matrix green (precondition)
- [ ] `.\scripts\guard-actor-hub.ps1` green with empty composer allowlist
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition|Golden|Battle"`
- [ ] Goldens re-blessed once

**Dependencies:** T5  
**Files likely touched:** `BattleStatComposer.cs`, `guard-actor-hub.ps1`, `BattleModels` RulesetVersion, goldens, `decisions.md`  
**Estimated scope:** L

---

### Task 7: Battle ops + tree via Hub; retire TreeAtomSource slot

**Spec:** `battle-ops-parity`  
**Description:** Battle equip uses Hub op-aware atoms; tree via Hub; delete unused `Battle.TreeAtomSource` Compose slot.

**Acceptance criteria:**
- [ ] Battle equip path Hub op-aware only (no flat ignore-op SSOT).
- [ ] Remove `EquipAtomSource.ModsFor` ignore-op battle fold (or equivalent dead path).
- [ ] Unknown op skipped visibly — not coerced to flat.
- [ ] Tree reaches battle actors via Hub when bindings exist.
- [ ] Unused `TreeAtomSource` Compose slot gone.
- [ ] AtomKind Battle Full for `stat.derived` matches tests.
- [ ] No Partial lie in battle equip comments without an owned follow-up.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip|TreeAtom|AtomDerived|Battle"`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T5–T6  
**Files likely touched:** `EquipAtomSource.cs` (`ModsFor` / `DerivedAtomsFor`), `TreeAtomSource`, AtomKind registry, Hub tree readers  
**Estimated scope:** M

---

## Checkpoint: Wave 1 complete

- [ ] Dual compose retired; guard green
- [ ] Ops parity Done
- [ ] Owner review before Standing wave

---

## Wave 2 — Standing honesty

### Task 8: CombatPowerMembership predicate

**Spec:** `combat-membership`  
**Description:** Ship closed include/exclude predicate matching map §7; Standing must not use `IsCombatChannel` alone.

**Acceptance criteria:**
- [ ] `CombatPowerMembership` (or named peer) with include/exclude tests.
- [ ] No Standing path uses `IsCombatChannel` alone.
- [ ] Documented list matches map assumption §7.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CombatPowerMembership|IsCombatChannel|Standing"`

**Dependencies:** T6  
**Files likely touched:** new membership type near `DerivedStatChannels` / power, tests  
**Estimated scope:** S–M

---

### Task 9: ProjectStanding synthetics + filter

**Spec:** `standing-compose`  
**Description:** `ProjectStanding` prices Hub combat writers (incl. aptitude) via synthetics + membership → `ActorPowerCache.Compose`; no Θ; no double-count equip/tree.

**Acceptance criteria:**
- [ ] Standing includes aptitude (and other Hub combat writers) via synthetics + filter.
- [ ] Double-count proven absent for equip/tree.
- [ ] `progression.*` / Θ do not raise Standing.
- [ ] Five-axis DTO unchanged.
- [ ] Cooldown (or other non-atom Hub membership channel) raises Standing.
- [ ] Still `ActorPowerCache.Compose(AtomRow[])` only — no Hub-snapshot overload.

**Verification:**
- [ ] Server/Core tests: `Standing|UniqueActorHub|ProjectStanding|ActorPower`

**Dependencies:** T8  
**Files likely touched:** `UniqueActorHubCompose.cs`, synthetic helpers, tests  
**Estimated scope:** M

---

### Task 10: Chip honesty — Lv / Θ, never “power”

**Spec:** `chip-honesty`  
**Description:** Remove level/Θ labeled “power” from aptitude scope chrome.

**Acceptance criteria:**
- [ ] `scopeFiction` / aptitude chrome free of level/Θ labeled “power.”
- [ ] Tests lock new copy (`Lv` / optional `Θ` only).
- [ ] Combat power number stays on Condition / copy-surfaces.
- [ ] Tick HF-chip on aptitude-sheet / combat-power ideal Done checklists.

**Verification:**
- [ ] `npm test -- --run foldAptitudesSurfaceVm` (from `web/fusion-rpg-web`)
- [ ] `npm test -- --run AptitudesTab` if present
- [ ] HF-chip checkbox ticked on ideal / aptitude-sheet

**Dependencies:** T9 (map order; chip lie can land earlier if needed — prefer after Standing)  
**Files likely touched:** `foldAptitudesSurfaceVm.ts`, `AptitudesPage.tsx` / StatBar, ideal / aptitude-sheet Done lists  
**Estimated scope:** S

---

### Task 11: Copy surfaces — combat power = O+S+C

**Spec:** `copy-surfaces`  
**Description:** Shared O+S+C helper on Condition / standing UI; Utility/Economy on vector only.

**Acceptance criteria:**
- [ ] Shared O+S+C helper wired.
- [ ] Utility/Economy excluded from “combat power” string.
- [ ] No sheet copy treats `combat.power.omni` alone as combat power.
- [ ] Five-axis vector retained for inspect.
- [ ] No `PowerScalar` on UniqueActor Standing path.
- [ ] Tick HF-copy on aptitude-sheet / combat-power ideal Done checklists.

**Verification:**
- [ ] `npm test -- --run foldConditionSurfaceVm`
- [ ] `npm test -- --run ActorPanel` if present
- [ ] HF-copy checkbox ticked on ideal / aptitude-sheet

**Dependencies:** T9  
**Files likely touched:** `foldConditionSurfaceVm.ts`, Condition tab components, ideal / aptitude-sheet Done lists  
**Estimated scope:** S–M

---

## Checkpoint: Wave 2 complete

- [ ] Standing honest; chip/copy Done
- [ ] Owner review before lawn wave

---

## Wave 3 — Lawn / loadout

### Task 12: Lawn aptitude parity Done gate

**Spec:** `lawn-aptitude-parity` (+ `aptitude-sheet` `unique-lawn-wire`)  
**Description:** Ensure Bound Hot = `commander + UniqueCreature(instanceId)`; empire = species. Implementation in unique-lawn-wire; tick this program’s Done gate + HF-lawn.

**Acceptance criteria:**
- [ ] After unique allocate + AptitudesUpdated, Bound unique Hot includes UniqueCreature shares.
- [ ] Fetch path is unique GET only (S4).
- [ ] General lawn creatures unchanged (species path).
- [ ] Regression: unique with same species id as a general does not inherit empire allocation.
- [ ] Parity prove: Bound lawn aptitude input matches Server UniqueCreature compose.
- [ ] HF-lawn ticked on ideal / maps.
- [ ] aptitude-sheet `unique-lawn-wire` Done (or listed open criteria closed) before closing this task.

**Verification:**
- [ ] Core filter `SpeciesAllocation|UniqueCreature|Bound`
- [ ] `.\scripts\guard-secondary-no-unity.ps1`
- [ ] Live Bound unique allocate probe (optional owner step)

**Dependencies:** T6; aptitude-sheet `unique-lawn-wire` Done (or open criteria listed)  
**Files likely touched:** Injector `CheatState` / bindings, aptitude-sheet wire, Done docs  
**Estimated scope:** M (coord with aptitude-sheet)

---

### Task 13: Injector PassiveTree → Hub

**Spec:** `lawn-tree-hydrate`  
**Description:** Configure injector so Hub includes tree bound atoms when player has tree state; parity with Server sheet.

**Acceptance criteria:**
- [ ] Injector Hub includes tree bound atoms when tree state exists.
- [ ] Parity with Server sheet tree fan-in for same playerId.
- [ ] Named “injector tree hydrate” gap closed in comments/docs.
- [ ] Reload refreshes tree bounds.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~TreeBound|PassiveTree|AtomDerived"`
- [ ] `.\scripts\guard-secondary-no-unity.ps1`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T6  
**Files likely touched:** Injector loop / `GateCounterHost`, `TreeBoundAtoms`, `PassiveTreeTuningHub` config  
**Estimated scope:** M

---

### Task 14: Bound loadout via Hub + Funnel

**Spec:** `bound-loadout-hub`  
**Description:** Remove Writer absolute combat path for Bound loadout; contributions via Hub; Funnel deltas for HP.

**Acceptance criteria:**
- [ ] `ApplyAbsolutes` combat path removed or non-combat-safe leftovers only with **owner sign-off**.
- [ ] Bound loadout visible on Hub Derived / AppliedCombat.
- [ ] HP via Funnel Add / preserve-ratio — not `mode=set` current HP.
- [ ] No type-wide `plant:N` (or peer) loadout keys.
- [ ] Each former absolute loadout key maps to Hub channel or Funnel grant — no silent drop.
- [ ] Funnel + single-writer + actor-hub guards green.
- [ ] HF-bound-loadout ticked on ideal.

**Verification:**
- [ ] Injector.Tests filter `UniqueBound|Loadout` (or Core+Guard if Injector suite absent)
- [ ] `.\scripts\guard-single-writer.ps1`
- [ ] `.\scripts\guard-funnel-delta.ps1`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T12  
**Files likely touched:** `UniqueBoundLoadout.cs`, `EntityApply`, tests  
**Estimated scope:** M–L

---

## Checkpoint: Wave 3 complete

- [ ] Lawn UniqueCreature + tree + Bound loadout Done
- [ ] Owner review before Wave 4

---

## Wave 4 — Place-matrix / D4 / prove

### Task 15: Sim combat Full via Hub

**Spec:** `sim-hub-parity`  
**Description:** Retire Named Partial ignore-op for combat `stat.derived` in sim; match Hub op semantics.

**Acceptance criteria:**
- [ ] Sim combat equip ops match Hub semantics.
- [ ] Named Partial for combat `stat.derived` retired or narrowly exempted with comment.
- [ ] Ideal place matrix Sim row updated.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorDerived|SimEffect|AtomKind|Sim"`

**Dependencies:** T6  
**Files likely touched:** `ActorDerivedProfiles.cs`, `AtomKindRegistry`, sim host  
**Estimated scope:** M

---

### Task 16: Standing coeff tuning (D4)

**Spec:** `standing-coeff-tuning`  
**Description:** Family/mask coeffs in tuning JSON so dodge is Survivability-weighted; magic-number audit clean.

**Acceptance criteria:**
- [ ] Tunable family/mask coeffs live and loaded.
- [ ] High dodge still raises combat power; Survivability axis identity improved.
- [ ] `python scripts/audit-magic-numbers.py` clean on new Policy surfaces.
- [ ] Missing coeff row → load reject or documented structural default.
- [ ] Standing vector fixtures re-blessed once if vectors move.

**Verification:**
- [ ] Core filter `ActorPower|Coefficient|Standing`
- [ ] `python scripts/audit-magic-numbers.py --summary`

**Dependencies:** T9  
**Files likely touched:** `data/seed/power/coefficients.v1.json`, E9/CostFunction, RpgStore.Power  
**Estimated scope:** M

---

### Task 17: Unique Θ on wire

**Spec:** `unique-theta-wire`  
**Description:** Expose real `theta` on unique aptitude/GET when known; chip shows `Θ` only from wire — never invent from specimenLevel.

**Acceptance criteria:**
- [ ] Unique wire carries real theta when known.
- [ ] Chip shows `Θ` only from wire.
- [ ] No `theta ?? specimenLevel` on aptitude scope path.

**Verification:**
- [ ] Server filter `Aptitude|UniqueActor|Theta`
- [ ] `npm test -- --run foldAptitudesSurfaceVm`

**Dependencies:** T10  
**Files likely touched:** Aptitude endpoints, DTO, FE fold  
**Estimated scope:** S–M

---

### Task 18: Stale dual-compose docs

**Spec:** `stale-compose-docs`  
**Description:** Overturn “composers stay separate” / adapters-OK forever prose; align §8.3 and decisions with fuse outcome.

**Acceptance criteria:**
- [ ] Blessing phrases gone or clearly historical.
- [ ] §8.3 / decisions reflect fuse outcome.
- [ ] Map Done checkbox for stale docs.
- [ ] Production code comments / `EquipAtomSource` dual-compose prose overturned (not docs-only).

**Verification:**
- [ ] `rg -n "composers stay separate|locked separate from ActorHub|BattleStatComposer stays|adapters OK" docs src --glob "!**/bin/**" --glob "!**/obj/**"` — clean or historical-only

**Dependencies:** T6  
**Files likely touched:** docs under architecture / class-system / actor-hub-ssot / decisions; `EquipAtomSource.cs` and related production comments  
**Estimated scope:** S

---

### Task 19: Prove Hub combat script

**Spec:** `prove-hub-combat`  
**Description:** Operator script: Hub battle ≡ sheet; Standing membership; Bound lawn UniqueCreature — no BattleStatComposer SSOT.

**Acceptance criteria:**
- [ ] Script exists and documented (`prove-hub-combat.ps1` or extend `prove-aptitude.ps1`).
- [ ] Post-fuse battle Hub channel totals ≡ sheet Hub for same UniqueActor inputs (equip/aptitude/tree).
- [ ] Standing rises when a membership combat channel rises via Hub writers — not when only Θ rises.
- [ ] Bound lawn aptitude input matches Server UniqueCreature compose.
- [ ] Ideal handoff prove path checked / runbook linked.

**Verification:**
- [ ] `.\scripts\prove-hub-combat.ps1` (or documented successor) green
- [ ] Core filter `ProveHub|StandingParity|BoundLawn` if tests added

**Dependencies:** T7, T9, T12, T14 (Waves 1–3 Done gates)  
**Files likely touched:** `tools/ProveAptitude`, new/extended script, tests, runbook / ideal handoff prove path  
**Estimated scope:** M

---

## Checkpoint: Wave 4 complete

- [ ] Sim / coeffs / Θ / docs / prove green
- [ ] Owner review before stub hygiene

---

## Wave 5 — Stub hygiene

### Task 20: Delete PlaceholderBattleResolver + feature-off assaults

**Spec:** `placeholder-battle-hub`  
**Description:** Remove resolver and PlaceholderBattleTuning; TurnEngine / DistrictAssault fail loud or combat kinds off — no silent Hp×Level wins.

**Acceptance criteria:**
- [ ] `PlaceholderBattleResolver` removed from production paths.
- [ ] `PlaceholderBattleTuning` deleted or unread.
- [ ] No silent Hp×Level combat outcomes.
- [ ] World tests re-blessed for feature-off / fail-loud.

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~PlaceholderBattle|DistrictAssault|TurnEngine|World"`
- [ ] `rg -n "PlaceholderBattleResolver" src` — gone or test-only
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T19 (map) / T6 minimum  
**Files likely touched:** `PlaceholderBattleResolver.cs`, `WorldTuning`, `TurnEngine`, `DistrictAssaultResolver`, tests  
**Estimated scope:** M–L

---

### Task 21: Drop intel Strength from placeholder weight

**Spec:** `placeholder-battle-hub` (B4)  
**Description:** Remove Intel Strength/bands derived from placeholder formula; presence-only (id/owner/kind) OK.

**Acceptance criteria:**
- [ ] `IntelRecorder` / `IntelSeed` do not call deleted Strength.
- [ ] Bands not fed by Hp×Level fiction.
- [ ] Intel tests updated.

**Verification:**
- [ ] Core filter `Intel`
- [ ] `rg -n "PlaceholderBattleResolver\\.Strength" src` — none

**Dependencies:** T20  
**Files likely touched:** `IntelRecorder.cs`, `IntelSeed.cs`, FactionIntel consumers, tests  
**Estimated scope:** M

---

### Task 22: Fold Level-as-Θ aliases (O2) + stub equip leftover sweep

**Spec:** ideal O2 · `unique-theta-wire` / battle seams · `cold-equip-one` align  
**Description:** Remove or rewire Level-as-Θ battle/delve aliases (`ActorThetaSeam` / composer leftovers); final sweep that no stub equip remains player-usable.

**Acceptance criteria:**
- [ ] No production Level-as-Θ alias on battle/delve aptitude paths (`ActorThetaSeam` / composer leftovers) — wire real Θ or delete the lie.
- [ ] Stub equip not player-usable SSOT (align T4).
- [ ] Comments point real Θ / Hub only.

**Verification:**
- [ ] `rg -n "ActorThetaSeam|theta \\?\\? specimenLevel|Level as Θ" src` triage clean
- [ ] Stub equip rg clean for production

**Dependencies:** T17, T4, T20  
**Files likely touched:** Delve/battle Θ seams, equip catalog leftovers  
**Estimated scope:** M

---

### Task 23: Track `world-actor-combat` in docs

**Spec:** `placeholder-battle-hub`  
**Description:** Ensure map / ideal / DESIGN-GATE pointer name tracked program; no Hub world assault Done claim.

**Acceptance criteria:**
- [ ] Map Out of scope / Tracked names `world-actor-combat`.
- [ ] Ideal / Wave 5 Done checkboxes honest.
- [ ] No module under this program claims world combat engine Done.

**Verification:**
- [ ] Re-read map + ideal + this todo Done section

**Dependencies:** T20–T21  
**Files likely touched:** map, ideal, optional DESIGN-GATE one-liner  
**Estimated scope:** XS

---

## Checkpoint: Program complete

- [ ] All Wave 1–5 acceptance criteria met
- [ ] Program map “Done when” checkboxes tickable
- [ ] Ideal + aptitude-sheet Done checkboxes cross-linked
- [ ] Goldens re-blessed once under fuse RulesetVersion bump
- [ ] `world-actor-combat` tracked only — ready for future `/idea`
- [ ] Owner accepts program close

---

## Program Done when (from map)

- [ ] No production `BattleStatComposer.Compose` under `src/`
- [ ] No new private ChannelMods combat writers; known producers migrated
- [ ] Cold equip rolled/atom — stub not SSOT
- [ ] Standing membership + synthetics; chip never labels level “power”
- [ ] Bound lawn UniqueCreature + Bound loadout via Hub
- [ ] Sim Full; D4 coeffs; unique Θ; stale docs gone
- [ ] `prove-hub-combat` green
- [ ] Placeholder + intel Strength deleted; `world-actor-combat` tracked
- [ ] Ideal + aptitude-sheet Done checkboxes cross-linked
- [ ] Goldens re-blessed once under fuse RulesetVersion bump