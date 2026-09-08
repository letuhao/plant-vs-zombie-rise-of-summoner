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
- [ ] First settled PvZ victory earns Souls and `first-win-dave` exactly once.
- [ ] A replayed result/fact changes no ledger count, checkpoint row, receipt, or item count.
- [ ] Any invalid content/owner path rolls back all onboarding writes while preserving the named failure.

**Verification:** focused Data ingest/replay tests plus `dotnet test tests/FusionRpg.Data.Tests --no-restore`.

**Dependencies:** Tasks 1–2.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

## Checkpoint A — foundation

- [ ] Core/Data focused suites pass.
- [ ] Transaction rollback and replay tests prove no partial checkpoint or reward state.
- [ ] `git diff --check` and DAL guard pass.

## Phase 1 — evidence and species progression

### Task 4: typed normal PvZ spawn producer

**Description:** Update the ordinary plant/zombie spawn producer to carry canonical
`demon.progression.v1` `sourceKind`/`sourceId` fields into the existing spawn payload. Preserve
record-then-drain and avoid scans, waits, or player-id transport. Keep extra/unique/commander producers
on their own classifications.

**Acceptance criteria:**
- [ ] Ordinary lawn spawns emit a matching `EmpireGeneral` claim for their side/type species.
- [ ] Unique, commander, malformed, and legacy opaque-source paths do not emit a general claim.
- [ ] Existing capture projection accepts the typed claim and existing spawn hooks remain non-blocking.

**Verification:** injector source/projection tests, injector build, and relevant guard scripts.

**Dependencies:** Task 1 (contract); can proceed in parallel with Tasks 2–3 after contract shape is fixed.

**Files likely touched:** `src/FusionRpg.Injector/EntityApply.cs` or the active spawn bridge,
`src/FusionRpg.Injector/GameCaptureHooks.cs`, `tests/FusionRpg.Injector.Tests/`,
`tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

### Task 5: concrete species and event fixtures

**Description:** Select one shipped ordinary species and one real lawn spawn path for acceptance. Add only
the minimum seed/catalog fixture needed to resolve its canonical species id and a deterministic settled
PvZ run fixture. Make the fixture prove that a same-type unique extra spawn is not empire progression.

**Acceptance criteria:**
- [ ] The fixture resolves through the production `LawnElementIndex`/species catalog.
- [ ] Valid general placement produces a species fact; same-run unique/commander/untrusted facts do not.
- [ ] No new XP curve or hardcoded balance value is introduced.

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
- [ ] Level-3 checkpoint requires player level ≥3 and qualifying general evidence in the same settled PvZ run.
- [ ] Payload values equal the post-transaction XP ledger/progression/allocation values.
- [ ] A unique or commander demon with the same species/type cannot satisfy the checkpoint.

**Verification:** Core/Data projection tests and a web-mode negative test proving `webrpg-1` cannot satisfy
the lawn checkpoint.

**Dependencies:** Tasks 1, 3–5.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`src/FusionRpg.Contracts/OnboardingDtos.cs`, `tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Medium.

## Checkpoint B — species evidence

- [ ] A settled PvZ fixture produces `level-3-general-species=earned` with real delta/allocation.
- [ ] Missing, malformed, unique, commander, and web facts remain locked.
- [ ] Full progression/source-focused suites pass.

## Phase 2 — Dave equipment reward

### Task 7: commander-owned equipment scope

**Description:** Resolve and document the owner choice. Under the recommended default, add a commander
assignment scope keyed by `OwnerKind.Player` + player id + `standard`, while Dave remains `commander:dave`.
Reuse item ownership/assignment projections and keep unique-specimen equipment routes unchanged.

**Acceptance criteria:**
- [ ] A commander-owned item can be listed from Dave's sheet without a fabricated unique actor id.
- [ ] Unique specimen equip still requires a persistent specimen and cannot write the commander cell.
- [ ] Assignment writes are atomic and idempotent on the item/reward identity.

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
- [ ] Item validates in the normal item seed/catalog pipeline and has a stable id/name/role.
- [ ] The item is owned by Player 1 only through the onboarding reward path and has no random reroll on replay.
- [ ] The item card/commander sheet can render its authored display data.

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
- [ ] Level-4 checkpoint requires both earlier checkpoints and player level ≥4.
- [ ] Exactly one item and one Dave assignment are created; a repeated result creates neither another.
- [ ] Restarting the server preserves item, assignment, reward payload, and commander-sheet visibility.

**Verification:** Data transaction/restart tests, commander projection tests, and item projection tests.

**Dependencies:** Tasks 3, 7–8.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`,
`src/FusionRpg.Server/CommanderEndpoints.cs`, item projection service, `tests/FusionRpg.Data.Tests/`.

**Estimated scope:** Large; keep mint/assignment in Data and keep endpoint changes thin.

## Checkpoint C — Dave reward

- [ ] Commander scope is recorded in `decisions.md`/spec if it differs from the recommended default.
- [ ] Real item validator and projection pass.
- [ ] Fresh-store and restart/replay tests prove one item, one assignment, no duplicate.

## Phase 3 — API and player surface

### Task 10: onboarding GET and claim API

**Description:** Add `GET /api/onboarding/{playerId}` and
`POST /api/onboarding/{playerId}/checkpoints/{checkpointId}/claim` as thin Server adapters over Data.
Return ordered checkpoint state, authoritative reward payloads, current player level, and revision. Claims
only acknowledge an earned row.

**Acceptance criteria:**
- [ ] Unknown player returns 404; GET is safe to poll and reflects durable state after restart.
- [ ] Claim is idempotent with named refusal reasons for unknown, locked, or already-claimed ids.
- [ ] No endpoint, DTO, or client code can grant Souls, XP, or items.

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
- [ ] Fresh Player 1 has an empty queue; earned checkpoints appear in order and later locked checkpoints stay absent.
- [ ] Reload/temporary API failure shows retry/error states without inventing values or losing earned state.
- [ ] Claiming acknowledges exactly once and opens the existing sheet surface; no legacy sunflower copy is
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
- [ ] Repo-wide production grep finds no current-flow use of the old bind copy or checkpoint grant logic.
- [ ] Existing Creatures navigation still works for any deliberately retained legacy compatibility branch.
- [ ] Spec, map, decision, plan, and todo agree on implementation status and no stale blocker claims remain.

**Verification:** FE unit/e2e regression, docs grep, `git diff --check`, and stale-reference audit.

**Dependencies:** Task 11.

**Files likely touched:** `web/fusion-rpg-web/src/stages/sanctum/FirstRunReveal.tsx`, related tests,
`docs/architecture/standalone/`, `docs/architecture/decisions.md`, `tasks/`.

**Estimated scope:** Small–Medium.

## Checkpoint D — player surface

- [ ] API and FE tests prove authoritative GET, acknowledgement-only claim, reload, retry, and ordered queue.
- [ ] Accessibility, vocabulary, viewport, and stage-persistence checks pass.
- [ ] Legacy branch is clearly gated or removed.

## Phase 4 — live acceptance and closeout

### Task 13: fresh SQLite integration/replay harness

**Description:** Add a repeatable harness/script that creates an empty data directory, starts the real
server path, injects deterministic test events where permitted, and records player/run/checkpoint/Souls/XP/
species/allocation/item/assignment evidence. Include duplicate settlement and restart assertions.

**Acceptance criteria:**
- [ ] Empty DB creates Player 1 and no fake checkpoint rows.
- [ ] The harness can drive all three checkpoints without direct SQL outside Data or client-side grants.
- [ ] Evidence output makes duplicate receipts and source contamination visible.

**Verification:** integration test command documented in the task output; run twice from clean directories.

**Dependencies:** Tasks 3, 5, 9, 10.

**Files likely touched:** `tests/`, `scripts/`, `docs/runbook/` or `docs/contributing/`.

**Estimated scope:** Medium.

### Task 14: real deploy-play acceptance and evidence capture

**Description:** Deploy the current build through `scripts/deploy-play.ps1`, run the real PvZ lawn path, and
record the five live-test checks from the spec. Finish with full suite/build/guard runs and a stale-code/
stale-document sweep; do not patch the game binary.

**Acceptance criteria:**
- [ ] First real PvZ victory, level-3 general spawn, and level-4 Dave reward all appear through the real API/UI.
- [ ] Reload/kill/replay at every reveal preserves the ordered queue and creates no duplicate receipt/item.
- [ ] Final report names any environment-only limitation; otherwise the spec's implementation checklist is
      fully checked.

**Verification:** `scripts/deploy-play.ps1`, full .NET suites, Web unit/Playwright suites, builds, guards,
and `git diff --check`.

**Dependencies:** Tasks 11–13.

**Estimated scope:** Medium (live verification only; no new product behavior).

### Task 15: full regression and closeout audit

**Description:** Run the complete verification matrix after the live path is green. Audit stale production
references, stale documentation, source-provenance fallbacks, and accidental edits outside the initiative;
record the final evidence and leave no unchecked implementation blocker in the spec.

**Acceptance criteria:**
- [ ] Core, Data, Guard, Server, Web unit/build suites and relevant Playwright suites pass.
- [ ] DAL, source, vocabulary, accessibility, viewport, and `git diff --check` guards pass.
- [ ] The final report links the live evidence and explicitly names any environment-only limitation.

**Verification:** repository CI-equivalent commands documented in the handoff and the final task report.

**Dependencies:** Task 14.

**Estimated scope:** Medium (verification/documentation only).

## Final checkpoint — first-session progression complete

- [ ] Tasks 1–15 have command evidence.
- [ ] All three rewards are durable, ordered, idempotent, and source-correct.
- [ ] No live blocker remains in the spec's final checklist.
