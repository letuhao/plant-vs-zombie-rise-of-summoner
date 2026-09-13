# Tasks: first-session progression

Plan: [first-session-progression-plan.md](first-session-progression-plan.md) · Spec:
[spec-first-session-progression.md](../docs/architecture/standalone/spec-first-session-progression.md).

All tasks below are implementation work for the current three-checkpoint sequence. The old FE essentials
onboarding task is historical and superseded; do not extend its sunflower/bind acceptance criteria.

## Phase 0 — contract and persistence foundation

### Task 1: checkpoint contract and eligibility evaluator — DONE

**Description:** Add stable checkpoint ids, ordered state/reward DTOs, named refusal reasons, and a pure
Core evaluator that accepts player level, settled run metadata, normalized victory, PvZ profile status, and
validated source facts. Keep eligibility separate from grant/claim side effects.

**Acceptance criteria:**
- [x] Evaluator returns only `first-win-dave`, then `level-3-general-species`, then
      `level-4-dave-equipment` in order.
- [x] Defeat, abandon, stalemate, web simulation, missing claim, unique claim, commander claim, and
      contradictory claim are rejected by named reason.
- [x] Level jumps do not skip prerequisites; repeated evaluation is stable.

**Verification:** `dotnet test tests/FusionRpg.Core.Tests --no-restore --filter "FullyQualifiedName~Onboarding"` — 6 passed.

**Dependencies:** None.

**Files likely touched:** `src/FusionRpg.Core/Progression/`, `src/FusionRpg.Contracts/`,
`tests/FusionRpg.Core.Tests/`.

**Estimated scope:** Medium.

### Task 2: checkpoint schema and Data primitives — DONE

**Description:** Add the additive `rpg_onboarding_checkpoint` table and indexes in the existing hot-schema
path. Implement locked reads, ordered projection, insert-if-absent earned rows, and claim-only updates.
Player 1 boot returns an empty view and never creates fake earned rows.

**Acceptance criteria:**
- [x] Fresh and existing databases create/read the table without losing prior rows.
- [x] `(player_id, checkpoint_id)` prevents duplicate earned rows and preserves the first reward receipt.
- [x] Claim changes only `claimed_utc`, rejects locked/unknown/already-claimed rows with stable reasons, and
      increments the projection revision once.

**Verification:** `dotnet test tests/FusionRpg.Data.Tests --no-restore --filter "FullyQualifiedName~OnboardingCheckpoint"` — 3 passed.

**Dependencies:** Task 1.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`, `tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

### Task 3: atomic settlement and reward receipts

**Description:** Extend the existing single-writer ingest/result settlement so the source fact/result,
existing Souls and XP, checkpoint rows, reward references, and level-4 assignment all share one SQLite
transaction. Use existing correlation/dedupe conventions; never perform grants from a GET or claim.

**Acceptance criteria:**
- [x] First settled PvZ victory earns Souls and `first-win-dave` exactly once.
- [x] A replayed result/fact changes no ledger count, checkpoint row, receipt, or item count.
- [x] Any invalid content/owner path leaves the checkpoint unearned and emits no item/assignment.

**Verification so far:** `dotnet test tests/FusionRpg.Data.Tests --no-restore --filter "FullyQualifiedName~Onboarding"` — 8 passed; the combined settlement test now asserts Souls are unchanged on replay and the level-4 test covers item/assignment replay. Item receipt/rollback coverage remains part of that focused path.

**Dependencies:** Tasks 1–2.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

## Checkpoint A — foundation

- [x] Core/Data focused suites pass.
- [x] Transaction replay tests prove no duplicate checkpoint, Soul, item, or assignment state.
- [x] `git diff --check` passes; DAL guard remains part of final sweep.

## Phase 1 — evidence and species progression

### Task 4: typed normal PvZ spawn producer

**Description:** Update the ordinary plant/zombie spawn producer to carry canonical
`creature.progression.v1` `sourceKind`/`sourceId` fields into the existing spawn payload. Preserve
record-then-drain and avoid scans, waits, or player-id transport. Keep extra/unique/commander producers
on their own classifications.

**Acceptance criteria:**
- [x] Ordinary lawn spawns emit a matching `EmpireGeneral` claim for their side/type species.
- [x] Unique, commander, malformed, and legacy opaque-source paths do not emit a general claim.
- [x] Existing capture projection accepts the typed claim and existing spawn hooks remain non-blocking.

**Verification:** injector source/projection tests, injector build, and relevant guard scripts.

**Dependencies:** Task 1 (contract); can proceed in parallel with Tasks 2–3 after contract shape is fixed.

**Files likely touched:** `src/FusionRpg.Injector/EntityApply.cs` or the active spawn bridge,
`src/FusionRpg.Injector/GameCaptureHooks.cs`, `tests/FusionRpg.Injector.Tests/`,
`tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

**Implementation note:** `EntityApply` now emits a typed `creature.progression.v1:general:<species>` claim
only from the vanilla `start`/`initHealth` lifecycle entry points. Debug, recapture, extra, unique,
commander, and other opaque paths stay unclassified. The deploy path now builds the injector and
passes its freshness guard; producer behavior is also confirmed by live typed spawn telemetry.

### Task 5: concrete species and event fixtures

**Description:** Select one shipped ordinary species and one real lawn spawn path for acceptance. Add only
the minimum seed/catalog fixture needed to resolve its canonical species id and a deterministic settled
PvZ run fixture. Make the fixture prove that a same-type unique extra spawn is not empire progression.

**Acceptance criteria:**
- [x] The fixture resolves through the production `LawnElementIndex`/species catalog.
- [x] Valid general placement produces a species fact; same-run unique/commander/untrusted facts do not.
- [x] No new XP curve or hardcoded balance value is introduced.

**Verification:** `dotnet test tests/FusionRpg.Data.Tests --no-restore --filter "FullyQualifiedName~SpeciesProgression|FullyQualifiedName~Onboarding"` and content validators.

**Dependencies:** Task 4.

**Files likely touched:** `data/tuning/` or `data/seed/` fixture, `tests/FusionRpg.Data.Tests/`,
`tests/fixtures/`.

**Estimated scope:** Small–Medium.

### Task 6: authoritative species reveal projection

**Description:** Project the actual applied species XP delta, resulting level, and automatic primary-stat
allocation for the qualifying species into the checkpoint reward payload. Read existing progression rows
and allocation projection; do not persist a second manual allocation.

**Acceptance criteria:**
- [x] Level-3 checkpoint requires player level ≥3 and qualifying general evidence in the same settled PvZ run.
- [x] Payload values equal the post-transaction XP ledger/progression/allocation values.
- [x] A unique or commander creature with the same species/type cannot satisfy the checkpoint.

**Verification:** Core/Data projection tests and a web-mode negative test proving `webrpg-1` cannot satisfy
      the lawn checkpoint; the focused onboarding Data suite now reports 8 passed.

**Dependencies:** Tasks 1, 3–5.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`src/FusionRpg.Contracts/OnboardingDtos.cs`, `tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

**Implementation note:** Capture settlement now derives the first deterministic source-validated general
species from the settled run, snapshots its species row and auto-allocation map, and inserts the
checkpoint in the same transaction. Focused coverage: 5 `OnboardingCheckpointStoreTests` pass;
production allocation values are computed when the server's aptitude/plan hubs are configured.

## Checkpoint B — species evidence

- [x] A settled PvZ fixture produces `level-3-general-species=earned` with real delta/allocation.
- [x] Missing, malformed, unique, commander, and web facts remain locked.
- [x] Full progression/source-focused suites pass.

## Phase 2 — Dave equipment reward

### Task 7: commander-owned equipment scope

**Description:** Resolve and document the owner choice. Under the recommended default, add a commander
assignment scope keyed by `OwnerKind.Player` + player id + `standard`, while Dave remains `commander:dave`.
Reuse item ownership/assignment projections and keep unique-specimen equipment routes unchanged.

**Acceptance criteria:**
- [x] A commander-owned item can be listed from Dave's sheet without a fabricated unique actor id.
- [x] Unique specimen equip still requires a persistent specimen and cannot write the commander cell.
- [x] Assignment writes are atomic and idempotent on the item/reward identity.

**Verification:** Data item-assignment tests, commander endpoint tests, and a fabricated-Dave-id refusal test.

**Dependencies:** Checkpoint A; owner confirms the recommended default before implementation begins.

**Files likely touched:** `src/FusionRpg.Core/Items/`, `src/FusionRpg.Data/Sqlite/RpgStore.Item*`,
`src/FusionRpg.Server/CommanderEndpoints.cs`, `src/FusionRpg.Server/ItemEquipEndpoints.cs`,
`tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

### Task 8: deterministic basic item content

**Description:** Author one real basic equipment item, its role/container/effect projection, display data,
and deterministic reward seed in existing item content surfaces. Validate it with the real item loaders and
keep all tunable values out of C#.

**Acceptance criteria:**
- [x] Item validates in the normal item seed/catalog pipeline and has a stable id/name/role.
- [x] The item is owned by Player 1 only through the onboarding reward path and has no random reroll on replay.
- [x] The commander sheet renders its authored stable container/role data.

**Verification:** item validator, item card tests, and focused Data tests against a fresh store.

**Dependencies:** Task 7 for the selected owner role; content authoring may start with a fixture before the
scope decision is finalized.

**Files likely touched:** `data/seed/items/`, `data/tuning/`, `tests/FusionRpg.Data.Tests/`,
`tests/FusionRpg.Core.Tests/`.

**Estimated scope:** Medium.

### Task 9: level-4 mint, assignment, and replay

**Description:** Integrate the deterministic item mint/acquire/assignment into level-4 checkpoint settlement.
Persist `reward_ref` to the item/assignment identity, rebuild the normal item/effect projection, and make
restart/replay return the same item.

**Acceptance criteria:**
- [x] Level-4 checkpoint requires both earlier checkpoints and player level ≥4.
- [x] Exactly one item and one Dave assignment are created; a repeated result creates neither another.
- [x] Fresh-store replay test preserves item, assignment, reward payload, and commander projection.

**Verification:** Data transaction/restart tests, commander projection tests, and item projection tests.

**Dependencies:** Tasks 3, 7–8.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`src/FusionRpg.Server/CommanderEndpoints.cs`, item projection service, `tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Large; keep mint/assignment in Data and keep endpoint changes thin.

## Checkpoint C — Dave reward

- [x] Commander scope is recorded in `decisions.md`/spec using the recommended default.
- [x] Real fixed-container mint and projection pass.
- [x] Fresh-store replay test proves one item, one assignment, no duplicate.

## Phase 3 — API and player surface

### Task 10: onboarding GET and claim API

**Description:** Add `GET /api/onboarding/{playerId}` and
`POST /api/onboarding/{playerId}/checkpoints/{checkpointId}/claim` as thin Server adapters over Data.
Return ordered checkpoint state, authoritative reward payloads, current player level, and revision. Claims
only acknowledge an earned row.

**Acceptance criteria:**
- [x] Unknown player returns 404; GET is safe to poll and reflects durable state.
- [x] Claim is idempotent with named refusal reasons for unknown, locked, or already-claimed ids.
- [x] No endpoint, DTO, or client code can grant Souls, XP, or items.

**Verification:** Server endpoint tests plus a real HTTP integration test covering GET/claim/replay.

**Dependencies:** Tasks 2–3 and 6/9.

**Files likely touched:** `src/FusionRpg.Contracts/OnboardingDtos.cs`,
`src/FusionRpg.Server/OnboardingEndpoints.cs`, `src/FusionRpg.Server/Program.cs`,
`tests/FusionRpg.Server.Tests/`.

**Estimated scope:** Medium.

### Task 11: in-stage FE reveal queue

**Description:** Add a hook and reveal layer over the current Sanctum or settled run-result stage. Render
one current checkpoint at a time with loading/error/retry, authoritative values, immediate acknowledgement,
and links into the existing Dave commander sheet/species progression surface. Preserve the old stage and
never add a top-level onboarding route.

**Acceptance criteria:**
- [x] Fresh Player 1 has an empty queue; earned checkpoints appear in order and later locked checkpoints stay absent.
- [x] Reload/temporary API failure shows retry/error states without inventing values or losing earned state.
- [x] Claiming acknowledges exactly once and links to the existing sheet surface; no legacy sunflower copy is
      presented as the current flow.

**Verification:** focused React tests, `npm run build`, and Playwright tests at desktop/tablet/mobile sizes.

**Dependencies:** Task 10 and existing commander/species sheet routes.

**Files likely touched:** `web/fusion-rpg-web/src/lib/bus/`, `web/fusion-rpg-web/src/stages/sanctum/`,
`web/fusion-rpg-web/src/contract/`, focused tests/e2e.

**Estimated scope:** Medium.

### Task 12: legacy branch gate and documentation sync

**Description:** Remove or explicitly gate `FirstRunReveal`'s old sunflower/bind branch so it cannot claim
or impersonate `first-win-dave`. Update the FE map, standalone map, idea, spec implementation status, and
task list with shipped paths and remaining acceptance evidence.

**Acceptance criteria:**
- [x] Repo-wide production grep finds no current-flow use of the old bind copy or checkpoint grant logic.
- [x] Existing Creatures navigation still works for any deliberately retained legacy compatibility branch
      (`SanctumStage.test.tsx` covers the rail and first-run CTA; `FirstRunReveal` remains compatibility-only).
- [x] Spec, map, decision, plan, and todo agree on implementation status and remaining live evidence.

**Verification:** FE unit/e2e regression, docs grep, `git diff --check`, and stale-reference audit.

**Dependencies:** Task 11.

**Files likely touched:** `web/fusion-rpg-web/src/stages/sanctum/FirstRunReveal.tsx`, related tests,
`docs/architecture/standalone/`, `docs/architecture/decisions.md`, `tasks/`.

**Estimated scope:** Small–Medium.

## Checkpoint D — player surface

- [x] API and FE tests prove authoritative GET, acknowledgement-only claim, retry, and ordered queue.
- [x] Accessibility/vocabulary/stage-persistence surfaces remain within existing stage components; build passes.
- [x] Legacy branch is no longer used by the current first-run flow.

## Phase 4 — live acceptance and closeout

### Task 13: fresh SQLite integration/replay harness

**Description:** Add a repeatable harness/script that creates an empty data directory, starts the real
server path, injects deterministic test events where permitted, and records player/run/checkpoint/Souls/XP/
species/allocation/item/assignment evidence. Include duplicate settlement and restart assertions.

**Acceptance criteria:**
- [x] Empty DB creates Player 1 and no fake checkpoint rows.
- [x] The focused harness drives all three checkpoints without direct SQL outside Data or client-side grants.
- [x] Evidence output makes duplicate receipts and source contamination visible.

**Verification:** `scripts/first-session-progression-harness.ps1 -NoBuild` creates fresh fixture stores;
the latest run reported Data 8 passed, Server 4 passed, and the simulator E2E victory test 1 passed.

**Dependencies:** Tasks 3, 5, 9, 10.

**Files likely touched:** `tests/`, `scripts/`, `docs/runbook/` or `docs/contributing/`.

**Estimated scope:** Medium.

### Task 14: real deploy-play acceptance and evidence capture

**Description:** Deploy the current build through `scripts/deploy-play.ps1` for injector smoke evidence;
use the existing simulator engine for the game-driven `match.result` and deterministic checkpoint path.
Finish with full suite/build/guard runs and a stale-code/stale-document sweep; do not patch the game binary.

**Acceptance criteria:**
- [x] Simulator E2E drives a game-shaped victory, typed general spawns, and the first two ordered reveals;
      the focused Data harness covers the level-4 item reward in the same settlement pipeline.
- [x] Reload/claim/replay idempotence is covered by the fresh-store harness and API/UI tests; live deploy
      smoke verifies injector connectivity and typed general-spawn telemetry.
- [x] Final report names the optional real-window limitation; no game binary is patched and no HP polling is
      used as onboarding evidence.

**Verification:** `scripts/deploy-play.ps1`, full .NET suites, Web unit/Playwright suites, builds, guards,
and `git diff --check`.

**Live probe evidence (2026-09-09):** MelonLoader deploy completed with injector build and freshness
checks; the importer reported `26 file(s): 164 atom(s)` with a stable catalog revision. The real game
reported `injectorConnected=true` with `simEnabled=false`, and `POST /api/debug/lawn/quick-start` returned
`ok=true`, `levelType=Advanture`, plus live target and plant pointers. A fresh run emitted real
`zombie.spawn` rows carrying `sourceKind=creature.progression.v1` and `sourceId=general:normalzombie`,
followed by `debug.run-steps.done` and `debug.effect.board-snapshot`. The simulator now covers the
game-driven `match.result` settlement path and the durable checkpoint sequence; a real victory window
remains optional smoke coverage.

**Dependencies:** Tasks 11–13.

**Estimated scope:** Medium (live smoke plus simulator verification; no new player-facing loop).

### Task 15: full regression and closeout audit

**Description:** Run the complete verification matrix after the live path is green. Audit stale production
references, stale documentation, source-provenance fallbacks, and accidental edits outside the initiative;
record the final evidence and leave no unchecked implementation blocker in the spec.

**Acceptance criteria:**
- [ ] Core, Data, Guard, Server, Web unit/build suites and relevant Playwright suites pass.
- [ ] DAL, source, vocabulary, accessibility, viewport, and `git diff --check` guards pass.
- [ ] The final report links the live evidence and explicitly names any environment-only limitation.

**Verification:** repository CI-equivalent commands documented in the handoff and the final task report.

**Regression evidence (2026-09-09, refreshed):** Atom importer 33/33 tests pass; the onboarding,
quick-start, and stale-board Server slice passes 16/16; the onboarding reveal component passes 3/3;
the Web production build passes; and the simulator onboarding E2E passes 1/1. The complete Server
suite is now 381/381 and the complete Guard suite is 243/243. Restore-sensitive child-process tests
now use `--no-restore`, and source scans ignore inaccessible cache folders. The Web unit suite is
2,466 passed / 8 failed; its remaining failures are existing contract/theme/band/accessibility
guards outside this initiative.
The onboarding control remains clean. Core is 13,286 passed /
61 failed; Data's onboarding/import slices are green, while full runs stop on the concurrently edited
item corpus. Those remaining failures are named below and do not identify an onboarding regression.
`git diff --check` is clean; only Git's normal LF-to-CRLF notices remain.

**Dependencies:** Task 14.

**Estimated scope:** Medium (verification/documentation only).

## Final checkpoint — first-session progression complete

- [ ] Tasks 1–15 have command evidence.
- [ ] All three rewards are durable, ordered, idempotent, and source-correct.
- [ ] No live blocker remains in the spec's final checklist.

## Current verification blockers

- Core/Data full suites remain red in the concurrently edited item corpus, not in onboarding code:
  generated set members in `data/seed/items/sets/*.json` omit required `baseType` values (the strict
  `SetCorpus` rejection is correct), and the charm, consumable, gem, affix-family, and display-template
  counts no longer match their checked-in baselines. The item-seedgen work must finish its P3.3 member
  binding and then refresh those baselines; do not weaken the parser or silently fill identities here.
- The Web suite still has 8 pre-existing contract/theme/band/accessibility guard failures in dev and
  shell surfaces; none touch the onboarding files. Server and Guard are fully green after the
  stale-test fixes.
- The seed importer now routes dungeon-specific envelopes to their dedicated loaders; the deployed
  server imports the atom corpus cleanly (26 files, 164 atoms) and reports a stable catalog revision.
  The real game/injector reaches a connected heartbeat and `lawn/quick-start` returns a live board with
  target and plant pointers. Remaining live-only work is to drive a victory and observe all three durable
  onboarding checkpoints; no game binary was patched.
