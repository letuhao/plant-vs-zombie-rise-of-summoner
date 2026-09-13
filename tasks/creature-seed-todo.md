# Tasks: `species-rank` (creature-seed)

**Plan:** `tasks/creature-seed-plan.md` · **Spec:** `docs/architecture/creature-seed/spec-species-rank.md`. One complete path per task, not horizontal layers. Checkpoints review done work; no pre-work gates (nothing irreversible — see plan).

## Phase 1: Foundation (parallelizable once vocab ids fixed)

- [ ] Task 1: Rank tuning table + loader validation
  - Acceptance: `creature-rank.v1.json` holds 10 ids + 100 cells diagonal-default; loader rejects unknown id naming table + cell; unresolved inputs never reach the table.
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureRankTuning"`; tuning JSON validates.
  - Files: `data/tuning/creature-rank.v1.json`, `src/FusionRpg.Core/Creatures/CreatureRankTuning.cs`, focused test.
  - Dependencies: None. Scope: M.

- [ ] Task 2: Rank enum + ladder helpers + guard test
  - Acceptance: 10-value `CreatureRank` closed enum; `AtLeast`/`AtMost`/`RungsBelow`/`OneRungAbove`/`All` agree with rarity ladder row-for-row; new guard test forbids bare `(int)rank` outside the ladder (beside existing rarity guard).
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureRank"`; `dotnet test tests/FusionRpg.Guard.Tests`.
  - Files: `src/FusionRpg.Core/Creatures/CreatureRank.cs`, `CreatureRankLadder.cs`, `tests/FusionRpg.Guard.Tests/CreatureRankLadderGuardTests.cs`.
  - Dependencies: None (needs vocab ids from Task 1). Scope: M.

## Checkpoint: Foundation
- [ ] Tuning validates; ladder + guard green; zero behavior change (nothing reads rank yet).

## Phase 2: Seed side

- [ ] Task 3: Seedsmith derive + runner wiring
  - Acceptance: rank joins `DERIVED_FIELDS` + schema property + `descriptions.py` entry (no KeyError); `derive_rank` handles voted, fallback-stamped, and unresolved inputs; wired at `_finalize`, `fix_unresolved` recompute, and scoped-rerun merge; `audit_schema` clean.
  - Verify: `python -m pytest tests/ -q -k "anchor or rank"` in `tools/seedsmith`; scoped-rerun staleness test green.
  - Files: `anchor/schema.py`, `anchor/descriptions.py`, `anchor/derive.py`, `run/runner.py`, seedsmith tests.
  - Dependencies: Task 1 (table shape). Scope: M (5 files, one pipeline).

- [ ] Task 4: Anchor regen, byte-identical rerun
  - Acceptance: every resolved anchor carries the table's rank; unresolved → absent (never defaulted); rerun over unchanged anchors byte-identical; `--check` green post-regen.
  - Verify: regen diff reviewed. (Rank-coverage line is Task 9, not Task 8 — corrected; see GAP-1.)
  - Files: `data/seed/creatures/species/**` (regen only — anchor tree). **Not** `data/generated/creatures/**`: concrete rows cannot carry `rank` until `SpeciesExpander` (Task 5) computes it and the `ConcreteSpecies` triad (Task 6) can serialize it — that regen belongs to Task 6's own verify line. See GAP-2.
  - Dependencies: Task 3. Scope: M.

## Checkpoint: Seed side
- [ ] Anchors carry rank deterministically; rerun identical; review regen diff before proceeding.

## Phase 3: Runtime flow

- [ ] Task 5: AnchorRow + expansion
  - Acceptance: nullable `rank` on `AnchorRow` (`Rarity` nullability untouched); `SpeciesExpander` computes rank post-expansion with theta path byte-identical; unresolved reporting covers rank-skipped.
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~SpeciesExpand"`.
  - Files: `Generation/AnchorRow.cs`, `Generation/SpeciesExpander.cs`, focused tests.
  - Dependencies: Task 4. Scope: S.

- [ ] Task 6: Concrete triad + SeedReader
  - Acceptance: record field + `Canonical` dict + optional-read for rank; Injector committed-tree path carries rank; `data/generated/creatures/**` regenerated here (moved from Task 4, see GAP-2) — `--check` green; fix stale comment `SpeciesSnapshot.cs:78-91` (claims every host calls `ConfigureFromCompiledDefault`; verified false — `Server/Program.cs:370` calls `Configure` with the store-backed snapshot) — doc hazard named in spec §4, bundle here per GAP-3.
  - Verify: Core tests green; regen output carries rank through all three; regen diff reviewed.
  - Files: `ConcreteSpecies.cs`, `ConcreteSpeciesSerializer.cs`, `ConcreteSpeciesSeedReader.cs`, `SpeciesSnapshot.cs` (comment only), `data/generated/creatures/**` (regen), focused tests.
  - Dependencies: Task 5. Scope: M.

- [ ] Task 7: Persist + catalog + Mapper
  - Acceptance: nullable column via `EnsureColumn`; null-safe read-back; `SameContent` compares rank (reimport idempotent); `CreatureSpeciesDef` + `Validate` carry rank; Mapper funnel passes it; old DBs read null without crash.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests`; `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Catalog"`; `guard-dal.ps1` clean.
  - Files: `RpgStore.Species.cs`, `CreatureSpeciesCatalog.cs`, `ConcreteSpeciesMapper.cs`, focused tests.
  - Dependencies: Task 6. Scope: M.

## Checkpoint: Flow
- [ ] Rank queryable at runtime on Server (store-backed) and Injector (committed-tree) paths; suites green.

## Phase 4: Landing gates

- [ ] Task 8: Fusion floors + preview parity
  - Acceptance: rank floor beside `CanPromote`, recipe eligibility, inherit picks (enforcing sites); preview mirrors each exactly (parity tests); null→bottom at each gate; no recompute on promotion (stated + tested).
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Fusion"`; behavior identical to pre-rank.
  - Files: `RpgStore.Fusion.cs`, `FusionEndpoints.cs`, `CreatureRecipeCatalog.cs`, focused tests.
  - Dependencies: Task 7. Scope: M.

- [ ] Task 9: Display payloads + quality line
  - Acceptance: catalog projection, roster/codex/preview/summon payloads carry rank id + display name (copy from catalog file); `ConcreteAnchor` join carries rank (filter + throw unchanged); quality report rank-coverage line, reports-only; confirm whether `CreatureQualityReport`'s existing diversity section (`Program.cs:160-209`) needs an explicit rank dimension added or already reads the new tuning vocab generically — state which, don't skip silently (spec §7 ambiguity, see GAP-5).
  - Verify: endpoint payload checks; `dotnet run --project tools/CreatureQualityReport`.
  - Files: `CreatureEndpoints.cs`, `RpgStore.Creatures.cs`, `SlotFilter.cs` (join only), quality report, focused tests.
  - Dependencies: Task 7 (Task 8 parallel-safe — confirmed by owner 2026-09-12, see GAP-6/7; plan.md's graph corrected to match). Scope: M.

## Checkpoint: Landing
- [ ] Zero golden moves; full Core + Data + Guard suites green; audit-magic-numbers clean; review before follow-ups.

## Phase 5: Follow-up gates (independent of each other; each with own pass-through proof)

- [ ] Task 10: Expedition wild-band rank floor
  - Acceptance: floor on `WildBand` filter; Sunwoven exclusion + EventOnly admission preserved; pass-through proof.
  - Verify: focused expedition tests green.
  - Files: `ExpeditionResolver.cs`, focused tests. Dependencies: Task 9. Scope: S.

- [ ] Task 11: Wave-band rank floor
  - Acceptance: floor on band membership; no-acquisition-filter behavior preserved; pass-through proof.
  - Verify: focused battle/wave tests green.
  - Files: `WaveCatalog.cs`, focused tests. Dependencies: Task 9. Scope: S.

- [ ] Task 12: Cage eligibility rank floor
  - Acceptance: floor on `OccupantEligible`; pricing untouched (rarity-keyed); pass-through proof.
  - Verify: focused Delve/wild tests green.
  - Files: `Delve/Wild/Cage.cs`, focused tests. Dependencies: Task 9. Scope: S.

## Checkpoint: Complete
- [ ] All acceptance criteria met; all suites green; ready for review.

## Gap audit (2026-09-12 — plan/spec cross-check; see also inline GAP-1/2/3/5/6/7 notes above)

- [ ] **GAP-4 — `creature-seed-map.md` §3 module table not updated.** The map's 18-module table,
  dependency graph, and build-order block (all dated up to 2026-09-05) do not list `species-rank` as
  module 19, despite the spec's own header declaring `Program: creature-seed` and depending on modules
  4 (`threat-band`) and 10 (`rarity-migration`). Per this repo's own convention ("the capability map is
  the index — never guess the active spec from filenames"), landing this module leaves the program's
  own index stale from day one. Add a task (fold into Task 9's checkpoint, or its own S-scope task)
  to add module 19, its dependency-graph arrows, and a build-order line before Checkpoint: Complete.
  Dependencies: none (doc-only). Scope: S.

- [ ] **GAP-1 — Task 4's Verify line cites the wrong task for the quality-report line.** Original text
  read *"rank-coverage line arrives in Task 8"*; Task 8 is fusion floors, not the quality report — the
  rank-coverage line is Task 9. Corrected inline above.

- [ ] **GAP-2 — Task 4 listed `data/generated/creatures/**` regen a phase too early.** That regen needs
  `SpeciesExpander` (Task 5) and the `ConcreteSpecies` triad (Task 6) to exist first — the concrete
  schema cannot carry a field that doesn't exist yet in the C# model. Moved to Task 6, which already
  claimed "regen output carries rank through all three" in its own verify line. Corrected inline above.

- [ ] **GAP-3 — Spec-named doc hazard never became a task.** `spec-species-rank.md` §4 explicitly flags
  `SpeciesSnapshot.cs:78-91`'s stale comment (*"every host calls `ConfigureFromCompiledDefault`"*) as a
  hazard to *"bundle with this change"* — verified still false and still present
  (`Server/Program.cs:370` calls the store-backed `Configure`, not `ConfigureFromCompiledDefault`).
  Folded into Task 6 (comment-only edit, same Injector-path task). Corrected inline above.

- [ ] **GAP-5 — Quality-report diversity section left ambiguous.** Spec §7 says the report's existing
  diversity section (`Program.cs:160-209`) "reads the new tuning vocab" alongside the new
  rank-coverage line — unclear whether that means it already picks rank up generically or needs an
  explicit added dimension. Task 9's acceptance now asks to state which, rather than silently doing
  neither. Corrected inline above.

- [x] **GAP-6/7 — RESOLVED 2026-09-12 (owner).** `creature-seed-plan.md` and this file disagreed on
  Task 8/Task 9 ordering (plan said "T8→T9 sequential" + a linear ASCII graph; this file already said
  "Task 8 parallel-safe"). Owner confirmed parallel-safe — file sets are disjoint
  (`RpgStore.Fusion.cs`/`FusionEndpoints.cs`/`CreatureRecipeCatalog.cs` vs.
  `CreatureEndpoints.cs`/`RpgStore.Creatures.cs`/`SlotFilter.cs`), no code dependency forces the order.
  `creature-seed-plan.md`'s prose and dependency-graph ASCII both corrected to show T8/T9 as parallel
  branches after T7. Also fixed: the plan's stale `T5a→T5b→T6→T7` reference (this file only ever had
  a single Task 5) corrected to `T5→T6→T7`.
