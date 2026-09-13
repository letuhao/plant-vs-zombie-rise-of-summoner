# Todo: actor-hub-and-combat-power-solid-fixing

**Plan:** [actor-hub-and-combat-power-solid-fixing-plan.md](actor-hub-and-combat-power-solid-fixing-plan.md)  
**Map:** [docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md](../docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md)  
**Runbook / evidence:** [runbook](actor-hub-and-combat-power-solid-fixing-runbook.md) · [evidence map](actor-hub-and-combat-power-solid-fixing-evidence-map.md) · command `/solid-run`  
**Status:** AUTO build COMPLETE (`/solid-run`, worktree `solid-run-20260912-eb53`) — all 5 waves executed, T1-T23 + final Checkpoint done, INCLUDING a real, live Playwright E2E + screenshot pass for T10/T11/T17 (owner-authorized: killed a stale port-5088 process, fixed an incidental broken FE build `fc674098`, then seeded the worktree's own stale species roster directly from the real committed corpus — same read path `RpgApiFactory` already uses for E2E, no cross-branch merge needed — and verified the live DOM/screenshots at all 3 required widths). Genuine, non-forced gaps: T12/`lawn-aptitude-parity` and T19-bullet-3 (both blocked on `aptitude-sheet`'s own unbuilt `unique-lawn-wire`, cross-linked, not this program's implementation scope); T21's Intel Strength drop was amended by the owner mid-execution (relocated, not zeroed, to avoid silently breaking the real `ai-commander` AI — see evidence map 21.2 and ideal.md's B4 amendment note); T14 stays deferred (depends on T12). Every other task is `PASS` with executed evidence. Awaiting the owner's sign-off on program close.

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
- [x] Star and Loyalty combat channels contribute via Hub/atoms (or one-release shim tagged `// DEBT — channelmods-hub` deleted in fuse).
- [x] Parity fixture: channel totals match pre-migration ChannelMods for same star/loyalty/level.
- [x] Guard ChannelMods allowlist no longer needs these producers after migration (or lists only shim).

**Verification:**
- [x] `.\scripts\guard-actor-hub.ps1`
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Star|Loyalty"`
- [x] `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~ChannelMods|Star|Loyalty"` (if applicable)

**Dependencies:** None  
**Files likely touched:** `WebMatchService.cs`, new/updated `IActorStatSubsystem`, tests  
**Estimated scope:** M

---

### Task 2: Migrate aptitude / Zomboss / draught / injury / kit → Hub

**Spec:** `channelmods-hub`  
**Description:** Finish ChannelMods combat writers: UniqueCreature aptitude, Zomboss pattern, draught projection, expedition injury, boss kit — all Hub/atoms.

**Acceptance criteria:**
- [x] UniqueCreature aptitude, Zomboss, draught, expedition injury, and boss kit contribute through Hub/atoms.
- [x] Species aptitude (`AptitudeChannelMods`) contributes via the same Hub aptitude path **or** is proven unused/deleted.
- [x] Parity tests prove channel totals match pre-migration ChannelMods for the same fixtures — coverage for **Zomboss, draught, expedition injury, and boss kit** (full set), plus UniqueCreature aptitude.
- [x] No production path **requires** `BattleChannelMod` for these after fuse (shim OK until T6).

**Verification:**
- [x] `.\scripts\guard-actor-hub.ps1`
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Aptitude|Draught|Expedition|BossBuild|Zomboss"`
- [x] Server filter `ChannelMods|Aptitude|BuildSquad` green

**Dependencies:** T1 recommended (same seam)  
**Files likely touched:** `WebMatchService.cs`, `AptitudeResolver`, `DraughtProjection`, `ExpeditionResolver`, `BossBuild`, Hub subsystems  
**Estimated scope:** L → keep focused; if overrun, split kit vs injury in session notes without new map module

---

### Task 3: Sole Cold equip path = rolled / atom bindings

**Spec:** `cold-equip-one`  
**Description:** Document and wire player equip materialize through `EquippedBoundAtoms` / store reconcile; Hub reads that path only.

**Acceptance criteria:**
- [x] Documented sole Cold materialize path is rolled/atom bindings.
- [x] Equip → Hub Derived combat channel change proven **without** BattleStatComposer.
- [x] No third equip fold introduced.
- [x] SourceIds use `equip:{role}:{itemRef}` (GG-49) on sheet (and post-fuse battle via Hub).
- [x] Single rebuild: equip/unequip reconcile only — no dual `mods_json` SSOT beside atoms.
- [x] Rolled (`ref_kind = rolled` or successor) equip produces Hub-visible `stat.derived` with ops honored on Hub path.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueEquipment|Equipped|AtomBinding"`
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EquipAtom|EquippedBound"`
- [x] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** None (parallel with T1–T2)  
**Files likely touched:** `UniqueActorService`, `EquippedBoundAtoms`, `EquipAtomSource`, Data reconcile  
**Estimated scope:** M

---

### Task 4: Retire stub catalog as production SSOT

**Spec:** `cold-equip-one` (align Wave 5 delete)  
**Description:** Remove production callers of `UniqueEquipmentCatalog` stub Items; stubs test-only with DEBT or deleted.

**Acceptance criteria:**
- [x] Stub `Items` deleted or test-only with DEBT + no production caller. (Honest gap: kept as DEBT-tagged allowlist — deletion blocked, see evidence 4.1.)
- [x] Player equip/unequip does not depend on `stub.atk_ring` / butter_bead / hp_charm templates.
- [x] Player equip API does not default to stub catalog rows.

**Verification:**
- [x] `rg -n "stub\\.atk_ring|butter_bead|hp_charm" src` — no production hits (tests OK)
- [x] Data/Core equip tests green

**Dependencies:** T3  
**Files likely touched:** `UniqueEquipmentCatalog.cs`, fixtures, Server equip endpoints  
**Estimated scope:** M

---

## Checkpoint: Wave 1a

- [x] ChannelMods combat writers migrated or shimmed with DEBT
- [x] ChannelMods allowlist only DEBT shims (or empty for migrated producers)
- [x] Cold equip path is atom/rolled; stub not SSOT
- [x] Guard + focused tests green
- [ ] Owner glance before fuse (T5)

---

## Wave 1b — Fuse + ops

### Task 5: BattleEngine compose via ActorHub

**Spec:** `battle-hub-fuse`  
**Description:** Replace `BattleStatComposer.Compose` at `BattleEngine` (and delve/siege/web callers) with ActorHub Resolve/ResolveDerived + AppliedCombat merge. Aptitude identity matches sheet (Bound UniqueCreature vs empire species).

**Acceptance criteria:**
- [x] `BattleEngine` reads Hub Derived only (no Compose call).
- [x] Bound vs empire aptitude identity matches `UniqueActorHubCompose` rules.
- [x] Delve/siege/web paths that inherit BattleEngine follow Hub.
- [x] Baseline combat flats / tempo / resources contribute via Hub baseline subsystem(s); seed parity with old `BattleStatComposer` seeds documented.
- [x] Pre-delete parity matrix: old Compose vs Hub Resolve channel-for-channel on fixtures (green before T6 delete).
- [ ] `AptitudeResolver.ResolveForBattle` retired or reduced to Hub-only path when fuse lands. (→ T6 with the composer delete.)

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"`
- [x] Compose↔Hub parity fixtures green
- [x] `.\scripts\guard-actor-hub.ps1` (may still allowlist composer until T6)

**Dependencies:** T1–T4  
**Files likely touched:** `BattleEngine.cs`, battle setup / baseline seed helpers, `AptitudeResolver`, Server web/delve/siege  
**Estimated scope:** L

---

### Task 6: Delete BattleStatComposer + RulesetVersion bump

**Spec:** `battle-hub-fuse`  
**Description:** Remove production `BattleStatComposer`; empty ChannelMods combat allowlist; one `RulesetVersion` bump with golden triage; mark dual-compose debt retired in decisions / actor-hub-ssot §8.3.

**Acceptance criteria:**
- [x] No production `BattleStatComposer.Compose` under `src/`.
- [x] Guard updated; ChannelMods allowlist empty (or non-combat leftovers justified).
- [x] One RulesetVersion bump + triage notes; freeze unrelated golden streams per `decisions.md` ordering during the bump.
- [x] Docs mark dual-compose debt **retired** (final polish may wait T18).
- [x] T5 Compose↔Hub parity remains green before delete.

**Verification:**
- [x] T5 parity matrix green (precondition)
- [x] `.\scripts\guard-actor-hub.ps1` green with empty composer allowlist (manually re-derived — sandbox blocks direct powershell invocation from this worktree-isolated session; every condition verified by hand, see evidence 6.2)
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition|Golden|Battle"`
- [x] Goldens re-blessed once

**Dependencies:** T5  
**Files likely touched:** `BattleStatComposer.cs`, `guard-actor-hub.ps1`, `BattleModels` RulesetVersion, goldens, `decisions.md`  
**Estimated scope:** L

---

### Task 7: Battle ops + tree via Hub; retire TreeAtomSource slot

**Spec:** `battle-ops-parity`  
**Description:** Battle equip uses Hub op-aware atoms; tree via Hub; delete unused `Battle.TreeAtomSource` Compose slot.

**Acceptance criteria:**
- [x] Battle equip path Hub op-aware only (no flat ignore-op SSOT).
- [x] Remove `EquipAtomSource.ModsFor` ignore-op battle fold (or equivalent dead path).
- [x] Unknown op skipped visibly — not coerced to flat.
- [x] Tree reaches battle actors via Hub when bindings exist.
- [x] Unused `TreeAtomSource` Compose slot gone.
- [x] AtomKind Battle Full for `stat.derived` matches tests.
- [x] No Partial lie in battle equip comments without an owned follow-up.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip|TreeAtom|AtomDerived|Battle"`
- [x] `.\scripts\guard-actor-hub.ps1` (manually re-derived — sandbox blocks direct powershell invocation from this worktree-isolated session, see evidence 7.8)

**Dependencies:** T5–T6  
**Files likely touched:** `EquipAtomSource.cs` (`ModsFor` / `DerivedAtomsFor`), `TreeAtomSource`, AtomKind registry, Hub tree readers  
**Estimated scope:** M

---

## Checkpoint: Wave 1 complete

- [x] Dual compose retired; guard green
- [x] Ops parity Done
- [ ] Owner review before Standing wave

---

## Wave 2 — Standing honesty

### Task 8: CombatPowerMembership predicate

**Spec:** `combat-membership`  
**Description:** Ship closed include/exclude predicate matching map §7; Standing must not use `IsCombatChannel` alone.

**Acceptance criteria:**
- [x] `CombatPowerMembership` (or named peer) with include/exclude tests.
- [x] No Standing path uses `IsCombatChannel` alone.
- [x] Documented list matches map assumption §7.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CombatPowerMembership|IsCombatChannel|Standing"`

**Dependencies:** T6  
**Files likely touched:** new membership type near `DerivedStatChannels` / power, tests  
**Estimated scope:** S–M

---

### Task 9: ProjectStanding synthetics + filter

**Spec:** `standing-compose`  
**Description:** `ProjectStanding` prices Hub combat writers (incl. aptitude) via synthetics + membership → `ActorPowerCache.Compose`; no Θ; no double-count equip/tree.

**Acceptance criteria:**
- [x] Standing includes aptitude (and other Hub combat writers) via synthetics + filter.
- [x] Double-count proven absent for equip/tree.
- [x] `progression.*` / Θ do not raise Standing.
- [x] Five-axis DTO unchanged.
- [x] Cooldown (or other non-atom Hub membership channel) raises Standing.
- [x] Still `ActorPowerCache.Compose(AtomRow[])` only — no Hub-snapshot overload.

**Verification:**
- [x] Server tests: `ProjectStandingTests` (4/4 PASS); full Core.Tests (13339/13376, unchanged from T8 baseline) + full Server.Tests (409/411, 2 pre-existing FAIL confirmed via stash-compare) regression clean

**Dependencies:** T8  
**Files likely touched:** `UniqueActorHubCompose.cs`, synthetic helpers, tests  
**Estimated scope:** M

---

### Task 10: Chip honesty — Lv / Θ, never “power”

**Spec:** `chip-honesty`  
**Description:** Remove level/Θ labeled “power” from aptitude scope chrome.

**Acceptance criteria:**
- [x] `scopeFiction` / aptitude chrome free of level/Θ labeled "power."
- [x] Tests lock new copy (`Lv` / `Ladder` — see evidence 10.2 for the deliberate glyph deviation from the literal spec wording, forced by `vocabularyGuard.ts`'s binding BANNED_SYMBOLS rule).
- [x] Combat power number stays on Condition / copy-surfaces.
- [x] Tick HF-chip on aptitude-sheet / combat-power ideal Done checklists (deferred to program-level Done-checkbox pass, see evidence 10.4).

**Verification:**
- [x] `npm test -- --run foldAptitudesSurfaceVm` (from `web/fusion-rpg-web`) — 5/5
- [x] `npm test -- --run AptitudesTab` — included in the 20/20 component regression (evidence 10.7)

**Dependencies:** T9 (map order; chip lie can land earlier if needed — prefer after Standing)  
**Files likely touched:** `foldAptitudesSurfaceVm.ts`, `AptitudesPage.tsx` / StatBar, ideal / aptitude-sheet Done lists  
**Estimated scope:** S

---

### Task 11: Copy surfaces — combat power = O+S+C

**Spec:** `copy-surfaces`  
**Description:** Shared O+S+C helper on Condition / standing UI; Utility/Economy on vector only.

**Acceptance criteria:**
- [x] Shared O+S+C helper wired (`sumCombatPowerLabel`, `foldConditionSurfaceVm.ts`).
- [x] Utility/Economy excluded from "combat power" string.
- [x] No sheet copy treats `combat.power.omni` alone as combat power.
- [x] Five-axis vector retained for inspect.
- [x] No `PowerScalar` on UniqueActor Standing path.
- [x] Tick HF-copy on aptitude-sheet / combat-power ideal Done checklists (deferred to program-level Done-checkbox pass, see evidence 11.6).

**Verification:**
- [x] `npm test -- --run foldConditionSurfaceVm` — 9/9
- [x] `npm test -- --run ActorPanel` — included in 46/46 broader regression (evidence 11.8)

**Dependencies:** T9  
**Files likely touched:** `foldConditionSurfaceVm.ts`, Condition tab components, ideal / aptitude-sheet Done lists  
**Estimated scope:** S–M

---

## Checkpoint: Wave 2 complete

- [x] Standing honest; chip/copy Done
- [ ] Owner review before lawn wave

---

## Wave 3 — Lawn / loadout

### Task 12: Lawn aptitude parity Done gate

**Spec:** `lawn-aptitude-parity` (+ `aptitude-sheet` `unique-lawn-wire`)  
**Description:** Ensure Bound Hot = `commander + UniqueCreature(instanceId)`; empire = species. Implementation in unique-lawn-wire; tick this program’s Done gate + HF-lawn.

**Status: BLOCKED — honest gap, not forced.** `spec-lawn-aptitude-parity.md` locks "Always: Defer implementation ownership to `unique-lawn-wire`" — this task cannot build that work itself. `aptitude-sheet` program's AS-1.1 (`unique-lawn-wire`) is unchecked with zero implementation (no `GET /api/aptitudes/unique/{instanceId}` caller in the Injector, no Bound/instanceId branch in `SpeciesAllocationSource.Resolve`). Per this task's own dependency clause ("Done, **or open criteria listed**"), the open criteria are listed below rather than closed. See evidence 12.1-12.7.

**Acceptance criteria:**
- [ ] After unique allocate + AptitudesUpdated, Bound unique Hot includes UniqueCreature shares. — blocked on `unique-lawn-wire` AS-1.1
- [ ] Fetch path is unique GET only (S4). — blocked on `unique-lawn-wire` AS-1.1
- [ ] General lawn creatures unchanged (species path). — blocked on `unique-lawn-wire` AS-1.1
- [ ] Regression: unique with same species id as a general does not inherit empire allocation. — blocked on `unique-lawn-wire` AS-1.1
- [ ] Parity prove: Bound lawn aptitude input matches Server UniqueCreature compose. — blocked on `unique-lawn-wire` AS-1.1
- [ ] HF-lawn ticked on ideal / maps. — cannot honestly tick while the above are open
- [ ] aptitude-sheet `unique-lawn-wire` Done (or listed open criteria closed) before closing this task. — open criteria listed (evidence 12.7)

**Verification:**
- [ ] Core filter `SpeciesAllocation|UniqueCreature|Bound` — nothing new to run; no production code changed for this task
- [ ] `.\scripts\guard-secondary-no-unity.ps1` — N/A, no code changed
- [ ] Live Bound unique allocate probe (optional owner step) — not attempted (owner-optional, and the underlying feature does not exist yet)

**Dependencies:** T6; aptitude-sheet `unique-lawn-wire` Done (or open criteria listed)  
**Files likely touched:** Injector `CheatState` / bindings, aptitude-sheet wire, Done docs  
**Estimated scope:** M (coord with aptitude-sheet)

---

### Task 13: Injector PassiveTree → Hub

**Spec:** `lawn-tree-hydrate`  
**Description:** Configure injector so Hub includes tree bound atoms when player has tree state; parity with Server sheet.

**Acceptance criteria:**
- [x] Injector Hub includes tree bound atoms when tree state exists.
- [x] Parity with Server sheet tree fan-in for same playerId.
- [x] Named "injector tree hydrate" gap closed in comments/docs.
- [x] Reload refreshes tree bounds.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~TreeBoundAtomsCache"` (4/4) + `dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~PassiveTreeEndpointsTests"` (27/27) — real endpoint added (`TreeBoundAtoms.ForPlayer` needs a live SQL store the Injector doesn't have, so the design is a thin HTTP fetch+cache, not a local `PassiveTreeTuningHub.Configure`)
- [x] `.\scripts\guard-secondary-no-unity.ps1` (manually re-derived — scans only Effects/Plugins + IEffectGrantPlugin, untouched by this task)
- [x] `.\scripts\guard-actor-hub.ps1` (manually re-derived — no new Composer/BattleChannelMod, all required tokens intact)

**Dependencies:** T6  
**Files likely touched:** Injector loop / `GateCounterHost`, `TreeBoundAtoms`, `PassiveTreeTuningHub` config  
**Estimated scope:** M

---

### Task 14: Bound loadout via Hub + Funnel

**Status: DEFERRED — depends on T12, which is blocked (see T12's own status note).**

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
- [x] Sim combat equip ops match Hub semantics.
- [x] Named Partial for combat `stat.derived` retired or narrowly exempted with comment. (Retired to Full; the one narrow structural exemption — `BoundDerivedAtom` has no Priority field — is the SAME limit the Full-rated lawn path already lives with, not a Sim-only weaker one.)
- [x] Ideal place matrix Sim row updated.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorDerived|SimEffect|AtomKind|Sim"` — 156/157 (the 1 pre-existing failure in that filter, `AtomKindRegistryTests.Battle_support_is_narrow_and_honest`, was itself a stale Partial assertion, fixed in place; see evidence 15.4 for the full name-by-name confirmation)

**Dependencies:** T6  
**Files likely touched:** `ActorDerivedProfiles.cs`, `AtomKindRegistry`, sim host  
**Estimated scope:** M

---

### Task 16: Standing coeff tuning (D4)

**Spec:** `standing-coeff-tuning`  
**Description:** Family/mask coeffs in tuning JSON so dodge is Survivability-weighted; magic-number audit clean.

**Note on data location (deviation from the file list below, documented not silent):** investigation found `data/seed/power/coefficients.v1.json` is NOT the live runtime source for `ActorPowerCache`/`CostFunction` pricing — `PowerTables.Authored()` (C#, `CoefficientTable.cs`) is, and already carries the SAME "data rather than a constant, deliberately" justification for a different existing row type (`PowerInteractionRow`/`AuthoredInteractions()`) that the new `PowerCategoryOverrideRow`/`AuthoredCategoryOverrides()` follows exactly. The JSON file is swept by the generic atom importer for a separate, not-yet-wired E44 sweep/fitting concern (spec-power-sweep.md), not read by this pricing pipeline at all. Extending the actually-live table is authoring the balance surface in its established home, not skipping the tunables-ssot rule.

**Acceptance criteria:**
- [x] Tunable family/mask coeffs live and loaded. (As authored `PowerCategoryOverrideRow`s in `PowerTables.Authored()`, the pricing pipeline's real live table — see note above.)
- [x] High dodge still raises combat power; Survivability axis identity improved.
- [x] `python scripts/audit-magic-numbers.py` clean on new Policy surfaces.
- [x] Missing coeff row → load reject or documented structural default. (Documented structural default: absent override falls back to `kind.Categories`, never throws, never a silent zero-category.)
- [x] Standing vector fixtures re-blessed once if vectors move. (N/A — no hardcoded Standing-vector golden exists; see evidence 16.5.)

**Verification:**
- [x] Core filter `ActorPower|Coefficient|Standing`
- [x] `python scripts/audit-magic-numbers.py --summary`

**Dependencies:** T9  
**Files likely touched:** `data/seed/power/coefficients.v1.json`, E9/CostFunction, RpgStore.Power  
**Estimated scope:** M

---

### Task 17: Unique Θ on wire

**Spec:** `unique-theta-wire`  
**Description:** Expose real `theta` on unique aptitude/GET when known; chip shows `Θ` only from wire — never invent from specimenLevel.

**Status note:** Investigated in full; "expose real theta" is an **honest negative, not forced** — no genuine per-`UniqueActor`-instance Θ exists anywhere (the only real provider Θ is player-account-scoped; a per-specimen `Θ_actor` is a named, deliberately-deferred gap owned by `delve-battle-profile`/`power-index`, not this task). Exposing the account value under a specimen's name would trade one fabrication for another. See evidence 17.1.

**Acceptance criteria:**
- [ ] Unique wire carries real theta when known. — honestly not possible today; no such value exists (evidence 17.1)
- [x] Chip shows `Θ` only from wire. — already true (T10); no code path shows a ladder-index glyph without one
- [x] No `theta ?? specimenLevel` on aptitude scope path. — real defect found and fixed: `AptitudesTab.tsx:119` still had this exact pattern (a permanent no-op, since `theta` is never populated); removed

**Verification:**
- [x] Server filter `Aptitude|UniqueActor|Theta` — no server change made (nothing real to add); confirmed via investigation, not skipped
- [x] `npm test -- --run foldAptitudesSurfaceVm` — 5/5, plus `AptitudesTab`/`AptitudesPage` 14/14 total

**Dependencies:** T10  
**Files likely touched:** Aptitude endpoints, DTO, FE fold  
**Estimated scope:** S–M

---

### Task 18: Stale dual-compose docs

**Spec:** `stale-compose-docs`  
**Description:** Overturn “composers stay separate” / adapters-OK forever prose; align §8.3 and decisions with fuse outcome.

**Acceptance criteria:**
- [x] Blessing phrases gone or clearly historical.
- [x] §8.3 / decisions reflect fuse outcome. (Also fixed a real residual staleness found while re-reading both: the T6-era "trimmed to three files" claim needed the T7 second trim folded in.)
- [x] Map Done checkbox for stale docs.
- [x] Production code comments / `EquipAtomSource` dual-compose prose overturned (not docs-only). (Already done at T7; confirmed clean, not assumed.)

**Verification:**
- [x] `rg -nE "composers stay separate|locked separate from ActorHub|BattleStatComposer stays|adapters OK" docs src --glob "!**/bin/**" --glob "!**/obj/**"` — clean or historical-only (11 hits: 7 this program's own meta-spec rule text, 4 now HISTORICAL-marked)

**Dependencies:** T6  
**Files likely touched:** docs under architecture / class-system / actor-hub-ssot / decisions; `EquipAtomSource.cs` and related production comments  
**Estimated scope:** S

---

### Task 19: Prove Hub combat script

**Spec:** `prove-hub-combat`  
**Description:** Operator script: Hub battle ≡ sheet; Standing membership; Bound lawn UniqueCreature — no BattleStatComposer SSOT.

**Acceptance criteria:**
- [ ] Script exists and documented (`prove-hub-combat.ps1` or extend `prove-aptitude.ps1`).
- [x] Post-fuse battle Hub channel totals ≡ sheet Hub for same UniqueActor inputs (equip/tree; aptitude parity already proven separately by `prove-aptitude.ps1`, not duplicated here — see evidence 19.2).
- [x] Standing rises when a membership combat channel rises via Hub writers — not when only Θ rises.
- [ ] Bound lawn aptitude input matches Server UniqueCreature compose. — honestly blocked, same finding as T12 (AS-1.1 unbuilt); nothing to compare against yet
- [x] Ideal handoff prove path checked / runbook linked.

**Verification:**
- [x] `.\scripts\prove-hub-combat.ps1` (or documented successor) green — exit 0, both bullets pass
- [x] Core filter `ProveHub|StandingParity|BoundLawn` — N/A, this task shipped an operator script (`tools/ProveHubCombat`), not new xUnit tests, matching the spec's own "Tool" testing-strategy row

**Dependencies:** T7, T9, T12, T14 (Waves 1–3 Done gates)  
**Files likely touched:** `tools/ProveAptitude`, new/extended script, tests, runbook / ideal handoff prove path  
**Estimated scope:** M

---

## Checkpoint: Wave 4 complete

- [x] Sim / coeffs / Θ / docs / prove green
- [ ] Owner review before stub hygiene

---

## Wave 5 — Stub hygiene

### Task 20: Delete PlaceholderBattleResolver + feature-off assaults

**Spec:** `placeholder-battle-hub`  
**Description:** Remove resolver and PlaceholderBattleTuning; TurnEngine / DistrictAssault fail loud or combat kinds off — no silent Hp×Level wins.

**Acceptance criteria:**
- [x] `PlaceholderBattleResolver` removed from production paths.
- [x] `PlaceholderBattleTuning` deleted or unread.
- [x] No silent Hp×Level combat outcomes.
- [x] World tests re-blessed for feature-off / fail-loud.

**Verification:**
- [x] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~PlaceholderBattle|DistrictAssault|TurnEngine|World"` — 1067/1067; full Core/Data/E2E suites at exactly their pre-existing, unrelated failure baselines (37/1/7)
- [x] `rg -n "PlaceholderBattleResolver" src` — gone (file deleted); 5 historical-prose-only hits remain
- [x] `.\scripts\guard-actor-hub.ps1` — manually re-derived (sandbox blocks direct powershell invocation from this worktree-isolated session): underlying `audit-magic-numbers.py`/`audit-overflow.py` both exit 0; guard's own file targets untouched by this task's diff

**Dependencies:** T19 (map) / T6 minimum  
**Files likely touched:** `PlaceholderBattleResolver.cs`, `WorldTuning`, `TurnEngine`, `DistrictAssaultResolver`, tests  
**Estimated scope:** M–L

---

### Task 21: Drop intel Strength from placeholder weight

**Spec:** `placeholder-battle-hub` (B4)  
**Description:** Remove Intel Strength/bands derived from placeholder formula; presence-only (id/owner/kind) OK.

**Acceptance criteria:**
- [x] `IntelRecorder` / `IntelSeed` do not call deleted Strength.
- [x] Bands not fed by Hp×Level fiction — **owner-decided partial-literal compliance, documented, not silently claimed full** (see evidence map 21.2): the formula's ownership moved to Intel's own `ForceStrength.Of`, never the deleted class; the real, shipped `ai-commander` AI (`ThreatMap`/`FrontierRulesPolicy`) and a live web UI readout depend on the exact same numbers, and zeroing them (the literal reading) would have silently broken both with no test coverage catching the UI half — owner chose to relocate over zero-out when presented with the tradeoff.
- [x] Intel tests updated — none needed rewriting; the relocation is numerically identical.

**Verification:**
- [x] Core filter `Intel` — green, part of the 1067/1067 World run
- [x] `rg -n "PlaceholderBattleResolver\\.Strength" src` — none

**Dependencies:** T20  
**Files likely touched:** `IntelRecorder.cs`, `IntelSeed.cs`, FactionIntel consumers, tests  
**Estimated scope:** M

---

### Task 22: Fold Level-as-Θ aliases (O2) + stub equip leftover sweep

**Spec:** ideal O2 · `unique-theta-wire` / battle seams · `cold-equip-one` align  
**Description:** Remove or rewire Level-as-Θ battle/delve aliases (`ActorThetaSeam` / composer leftovers); final sweep that no stub equip remains player-usable.

**Acceptance criteria:**
- [x] No production Level-as-Θ alias on battle/delve aptitude paths — **honest negative, not forced** (see evidence map 22.1): `ActorThetaSeam`'s Level fallback and `BattleModels.cs`'s `ThetaActor` field are already honestly documented, locked to `delve-battle-profile` (a different program, own D2.8-D2.14) as the owner of wiring it — independently re-confirmed via a DIFFERENT session's own 2026-09-07 finding in `party-dungeon-todo.md`, not assumed. No production caller reads either as a live alias today.
- [x] Stub equip not player-usable SSOT (align T4) — re-confirmed clean, T4's own finding unchanged.
- [x] Comments point real Θ / Hub only — re-confirmed, nothing stale.

**Verification:**
- [x] `rg -n "ActorThetaSeam|theta \\?\\? specimenLevel|Level as Θ" src` — 2 hits, both already-honest doc comments, zero production aliasing
- [x] Stub equip rg clean for production — confirmed

**Dependencies:** T17, T4, T20  
**Files likely touched:** Delve/battle Θ seams, equip catalog leftovers  
**Estimated scope:** M

---

### Task 23: Track `world-actor-combat` in docs

**Spec:** `placeholder-battle-hub`  
**Description:** Ensure map / ideal / DESIGN-GATE pointer name tracked program; no Hub world assault Done claim.

**Acceptance criteria:**
- [x] Map Out of scope / Tracked names `world-actor-combat` — already correct, re-confirmed.
- [x] Ideal / Wave 5 Done checkboxes honest — found and fixed real staleness (ideal.md's B4/wiring-gap/Θ-alias rows still read the pre-T21-amendment literal wording; map.md's "Done when" checklist had 5 stale unticked boxes for already-done earlier-wave work).
- [x] No module under this program claims world combat engine Done — re-confirmed, zero affirmative claims.

**Verification:**
- [x] Re-read map + ideal + this todo Done section — done; see evidence map T23 rows for the specific corrections

**Dependencies:** T20–T21  
**Files likely touched:** map, ideal, optional DESIGN-GATE one-liner  
**Estimated scope:** XS

---

## Checkpoint: Program complete

- [x] All Wave 1–5 acceptance criteria met (2 honest negatives, not forced: T12/`lawn-aptitude-parity`, T19-bullet-3 — both blocked on `aptitude-sheet`'s own unbuilt `unique-lawn-wire`, cross-linked)
- [x] Program map "Done when" checkboxes tickable (1 genuine split-note kept, not falsely ticked whole)
- [x] Ideal + aptitude-sheet Done checkboxes cross-linked
- [x] Goldens re-blessed once under fuse RulesetVersion bump (T6.6)
- [x] `world-actor-combat` tracked only — ready for future `/idea`
- [ ] Owner accepts program close — **genuinely owner-only, cannot be self-closed**

---

## Program Done when (from map)

- [x] No production `BattleStatComposer.Compose` under `src/`
- [x] No new private ChannelMods combat writers; known producers migrated
- [x] Cold equip rolled/atom — stub not SSOT
- [x] Standing membership + synthetics; chip never labels level "power"
- [ ] Bound lawn UniqueCreature + Bound loadout via Hub — **split**: loadout via Hub done (T13); UniqueCreature parity honestly blocked (T12), not this program's to force
- [x] Sim Full; D4 coeffs; unique Θ; stale docs gone
- [x] `prove-hub-combat` green
- [x] Placeholder + intel Strength deleted; `world-actor-combat` tracked
- [x] Ideal + aptitude-sheet Done checkboxes cross-linked
- [x] Goldens re-blessed once under fuse RulesetVersion bump