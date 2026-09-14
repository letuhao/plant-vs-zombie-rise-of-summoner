# Tasks: onboarding Rift prologue

Plan: [onboarding-rift-plan.md](onboarding-rift-plan.md). Source specs:
[onboarding-gnome-teaser.md](../docs/ideas/onboarding-gnome-teaser.md) ·
[spec-onboarding-rift-assets.md](../docs/design/spec-onboarding-rift-assets.md) ·
[spec-first-session-progression.md](../docs/architecture/standalone/spec-first-session-progression.md).

Implementation status (2026-09-14): the durable story ledger, media provenance registry, server contract,
Sanctum dialog, static/VFX cue treatment, failure fallback, first-user guide, and focused regression
coverage are implemented. The remaining open items are release-gate/manual work only (live injector
proof, accessibility/viewport sweep, generated guide synchronization, and full repository CI). This
program adds a presentation-only story prologue and its durable acknowledgement to the current
SQLite-backed profile; it must not create player-facing save slots or replace/extend the three reward
checkpoints.

Evidence collected:

- `dotnet build src/FusionRpg.Server/FusionRpg.Server.csproj --no-restore` — pass.
- `dotnet test tests/FusionRpg.Server.Tests/FusionRpg.Server.Tests.csproj --no-restore --filter FullyQualifiedName~OnboardingEndpointsTests` — 5 passed.
- `dotnet test tests/FusionRpg.Core.Tests/FusionRpg.Core.Tests.csproj --no-restore --filter FullyQualifiedName~Vfx` — 172 passed.
- `dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --no-restore --filter FullyQualifiedName~OnboardingCheckpointStoreTests` — 9 passed.
- `dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --no-restore --filter FullyQualifiedName~Rift_asset_provenance` — 1 passed.
- `npm test -- --run src/features/onboarding/RiftPrologueDialog.test.tsx src/stages/sanctum/SanctumStage.test.tsx` — 24 passed.
- `npm run build` — pass; the pre-existing missing `features/log/LogPage` import was restored with a bounded developer log page.
- `git diff --check` — pass.

Still open: live `scripts/prove-vfx.ps1`, full Data suite in CI, browser accessibility/320px sweep,
and final guide/link review. Deployment is intentionally not part of this implementation pass.

## Phase 0 — contract and documentation foundation

### Task 1: reconcile source specs and legacy references — OPEN

**Description:** Make the teaser, asset, lore, GUI, first-session progression, and player-guide documents
agree on placement, eligibility, API boundary, copy, and non-goals. Mark the old sunflower/bind flow as
historical wherever it could be mistaken for the current onboarding contract.

**Acceptance criteria:**
- [ ] The teaser is documented as a Sanctum-owned band-3 dialog, not a route or checkpoint.
- [ ] The three reward checkpoint IDs and order remain unchanged.
- [ ] Lore copy labels Rift/Void/quarantine claims as project fiction.
- [ ] `docs/ideas/onboarding-progression.md` and relevant `docs/guide/` pages link to the current contracts.

**Verification:** Markdown link scan and manual cross-document contradiction review.

**Dependencies:** None.

**Files likely touched:** `docs/ideas/`, `docs/design/`, `docs/research/`, `docs/guide/`.

**Estimated scope:** Small.

### Task 2: story domain and API contract — OPEN

**Description:** Define `rift-prologue` version 1, `unseen`/`acknowledged` state, `completed`/`skipped`
outcomes, `eligible`, revision behavior, and named refusal reasons in Core/Contracts terms.

**Acceptance criteria:**
- [ ] Unknown story/version/outcome has a stable refusal reason.
- [ ] Same-outcome acknowledgement is idempotent.
- [ ] Conflicting concurrent outcomes resolve to one stored result.
- [ ] The contract is separate from `OnboardingCheckpointIds` and checkpoint DTO semantics.

**Verification:** Core contract tests and DTO serialization tests.

**Dependencies:** Task 1.

**Files likely touched:** `src/FusionRpg.Core/Onboarding/`, `src/FusionRpg.Contracts/`, focused tests.

**Estimated scope:** Small.

### Task 3: asset and media-dump contract — OPEN

**Description:** Freeze semantic roles, accessible labels, version metadata, square icon handling,
fallback placeholder, display bounds, and PvZ image-dump source keys. The serious Rift VFX contract is
tracked separately in Task 4 against `docs/architecture/vfx-ssot.md`.

**Acceptance criteria:**
- [ ] `storySprite` and `icon` roles are filename-independent.
- [ ] The portrait icon source is never used as an uncropped runtime square.
- [ ] Missing/late assets have a stable placeholder contract.
- [ ] Validation requirements cover dimensions, alpha, transparency, and 32px/64px readability.
- [ ] Reused PvZ imagery resolves through `rpg-media.sqlite` `type_icon_layers`/`type_icons` and `TypeIconStore`.

**Verification:** Spec review against the selected PNG dimensions and asset links.

**Dependencies:** Task 1.

**Files likely touched:** `docs/design/spec-onboarding-rift-assets.md`, future FE asset module.

**Estimated scope:** Small.

### Task 4: Rift VFX cue contract — OPEN

**Description:** Define the serious Rift presentation contract against `docs/architecture/vfx-ssot.md`:
cue ids, layered portal/quarantine recipes, anchors, rate limits, caps, skip reasons, reduced-motion
fallbacks, and live-proof assertions. This is a presentation contract only; it cannot write gameplay state.

**Acceptance criteria:**
- [ ] `rift.portal.open`, `rift.portal.surge`, `rift.quarantine.seal`, and `rift.quarantine.fade` have
  semantic recipes and anchor decisions.
- [ ] Portal and quarantine cues remain visually distinct without relying on color alone.
- [ ] Caps, rate limits, resource-failure skip reasons, and reduced-motion/static fallbacks are explicit.
- [ ] The contract requires `VfxCatalog` → `VfxDirector` → shared primitives and `prove-vfx.ps1` coverage.

**Verification:** Core VFX recipe/rule tests and identity-collision guard.

**Dependencies:** Task 3; existing `docs/architecture/vfx-ssot.md` contract.

**Files likely touched:** `docs/architecture/vfx-ssot.md`, `src/FusionRpg.Core/Vfx/`, VFX tests.

**Estimated scope:** Small–Medium.

## Phase 1 — media provenance and durable story state

### Task 5: PvZ image-dump SQL provenance — OPEN

**Description:** Reuse the existing `rpg-media.sqlite` image-dump pipeline instead of inventing a save-slot
feature. Verify `type_icon_layers`, `type_icons`, `TypeIconStore`, and `/api/icons/dump/*` can identify
captured PvZ imagery by side/type/layer. Add only additive media metadata (for example hash/revision) if
the Rift manifest needs stronger provenance; never copy image BLOBs into `rpg-hot.sqlite`.

**Acceptance criteria:**
- [ ] A reused PvZ image resolves from its `(side, type_id, layer)` source key.
- [ ] If semantic roles need durable mapping, `rpg-media.sqlite` has the metadata-only
  `rift_asset_sources` table with source kind, key, hash, capture time, and revision; `pvz_dump` keys
  validate against `type_icon_layers`.
- [ ] Missing or stale dump rows fail closed to the Rift placeholder.
- [ ] Media DB writes remain independent from player/profile story state.
- [ ] Existing icon-dump API and composed portrait behavior remain unchanged.

**Verification:** `FusionRpg.Data.Tests` icon-dump/media tests plus server endpoint regression tests.

**Dependencies:** Tasks 3–4.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Icons.cs`, media schema/migration,
`src/FusionRpg.Server/Program.cs`, focused tests, `docs/database/schema.md`,
`docs/architecture/data-architecture.md`.

**Estimated scope:** Small–Medium.

### Task 6: story ledger schema and bootstrap — OPEN

**Description:** Add `rpg_onboarding_story` to the additive SQLite hot schema, create new-profile rows,
and backfill existing profiles. Do not create fake reward checkpoints.

**Acceptance criteria:**
- [ ] Fresh profiles receive one `unseen` version-1 story row before Sanctum render.
- [ ] Existing profiles with a settled PvZ victory are backfilled as `skipped`.
- [ ] `(player_id, story_id, version)` prevents duplicates.
- [ ] Restart preserves state and revision.

**Verification:** `FusionRpg.Data.Tests` fresh-db, migration, restart, and uniqueness tests.

**Dependencies:** Task 2.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.cs`, `RpgStore.Onboarding.cs`, Data tests.

**Estimated scope:** Medium.

### Task 7: story read/acknowledgement primitives — OPEN

**Description:** Implement ordered story projection, eligibility calculation, acknowledgement mutation,
revision increments, replay behavior, and named refusal reasons in the Data layer.

**Acceptance criteria:**
- [ ] Reads return state, outcome, eligibility, acknowledgement time, and revision.
- [ ] First acknowledgement writes exactly once.
- [ ] Repeating the same outcome succeeds without changing the stored outcome.
- [ ] Conflicting outcome does not overwrite the first result.

**Verification:** Focused Data tests including concurrent/replay calls.

**Dependencies:** Tasks 2 and 6.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`, Data tests.

**Estimated scope:** Medium.

### Task 8: first-victory housekeeping integration — OPEN

**Description:** When a new player bypasses the prologue and settles the first PvZ lawn victory, mark
the separate unseen story as `skipped` without changing reward checkpoint settlement.

**Acceptance criteria:**
- [ ] Only settled PvZ victory can trigger the housekeeping transition.
- [ ] Defeat, abandon, web simulation, and non-PvZ results do not trigger it.
- [ ] Souls, XP, item, checkpoint, and reward receipts are byte-for-byte unaffected by the story write.

**Verification:** Data settlement replay tests and a negative-result matrix.

**Dependencies:** Tasks 6–7; existing first-session settlement path.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.cs`, onboarding settlement tests.

**Estimated scope:** Medium.

## Phase 2 — server API

### Task 9: onboarding story DTO projection — OPEN

**Description:** Extend the existing onboarding response with a separate `stories` collection while
preserving the existing `checkpoints` JSON contract.

**Acceptance criteria:**
- [ ] Existing checkpoint clients deserialize unchanged.
- [ ] Story rows expose `storyId`, `version`, `state`, `outcome`, `eligible`, `acknowledgedUtc`, `revision`.
- [ ] Unknown players still return 404.

**Verification:** Server endpoint serialization and regression tests.

**Dependencies:** Tasks 2, 6, and 7.

**Files likely touched:** `src/FusionRpg.Contracts/OnboardingDtos.cs`, `src/FusionRpg.Server/OnboardingEndpoints.cs`.

**Estimated scope:** Small.

### Task 10: story acknowledgement endpoint — OPEN

**Description:** Add `POST /api/onboarding/{playerId}/stories/{storyId}/ack` with version/outcome body,
idempotent success, and named 4xx refusal/conflict responses.

**Acceptance criteria:**
- [ ] Completed and skipped outcomes persist.
- [ ] Duplicate same-outcome requests succeed.
- [ ] Unknown story/version/outcome and conflicting outcome return stable reasons.
- [ ] No checkpoint claim or reward grant occurs.

**Verification:** `FusionRpg.Server.Tests` endpoint tests and checkpoint-ledger assertions.

**Dependencies:** Tasks 7 and 9.

**Files likely touched:** `src/FusionRpg.Server/OnboardingEndpoints.cs`, server tests.

**Estimated scope:** Small.

### Task 11: server/API regression gate — OPEN

**Description:** Add a focused server fixture proving old checkpoint GET/claim behavior and new story
GET/ack behavior coexist across reload/restart.

**Acceptance criteria:**
- [ ] Existing onboarding endpoint tests remain green.
- [ ] Story state survives server restart.
- [ ] Checkpoint revisions and rows remain unchanged after story ack.

**Verification:** Server test command with the onboarding filter.

**Dependencies:** Tasks 9–10.

**Files likely touched:** `tests/FusionRpg.Server.Tests/`, fixtures.

**Estimated scope:** Small.

## Phase 3 — serious Rift VFX and frontend story flow

### Task 12: Rift VFX recipes and pure rules — OPEN

**Description:** Extend the VFX SSOT with `rift.portal.open`, `rift.portal.surge`,
`rift.quarantine.seal`, and `rift.quarantine.fade`. Define recipes, anchor kinds, layered rim/tear/depth
grammar, rate limits, caps, and static/reduced-motion fallbacks in Core terms.

**Acceptance criteria:**
- [ ] Each cue has a semantic recipe and no gameplay payload/write path.
- [ ] Portal and quarantine cues are visually distinct without relying on color alone.
- [ ] Reduced motion resolves to static base art/seal with no particle emission.
- [ ] Caps/rate limits and skip reasons are explicit and testable.

**Verification:** `FusionRpg.Core.Tests` VFX recipe/rule tests and identity-collision guard.

**Dependencies:** Task 4.

**Files likely touched:** `src/FusionRpg.Core/Vfx/`, `docs/architecture/vfx-ssot.md`, VFX tests.

**Estimated scope:** Medium.

### Task 13: Rift VFX director integration and live proof — OPEN

**Description:** Wire the four cues through the existing `VfxDirector`, `AnchorResolver`,
`UnitFrameResolver`, `FxResources`, and host tick/draw path. Add resource-failure degradation and proof
coverage; do not add a one-off Unity overlay or bypass the director.

**Acceptance criteria:**
- [ ] Beats 2/3 emit the declared cues through the shared director.
- [ ] Shader/resource failure emits a skip reason and leaves the story usable.
- [ ] VFX caps, match-end clear, and reduced-motion behavior hold.
- [ ] `scripts/prove-vfx.ps1` captures the portal/surge/seal/fade result.
- [ ] No HP polling, gameplay write, or binary patch is introduced.

**Verification:** injector build, VFX guard suite, and live `prove-vfx.ps1` proof.

**Dependencies:** Task 12.

**Files likely touched:** `src/FusionRpg.Injector/Fx/`, `src/FusionRpg.Injector/InjectorLoop.cs` only if existing host registration requires it, VFX proof scripts/research.

**Estimated scope:** Large.

### Task 14: VFX-to-dialog composition seam — OPEN

**Description:** Expose the active beat's semantic cue state to the presentation surface without making
the dialog own Unity/VFX lifecycle. The dialog requests story cues; the shared VFX host renders them and
falls back to static assets when unavailable.

**Acceptance criteria:**
- [ ] Dialog code contains no Unity/VFX primitive construction.
- [ ] Beat transitions trigger at most one cue sequence per beat.
- [ ] Skip/close clears or safely abandons active VFX.
- [ ] Standalone web presentation works with VFX absent.

**Verification:** FE component tests, integration test with a recording VFX sink, and live smoke.

**Dependencies:** Tasks 12–13.

**Files likely touched:** FE story feature, cue bridge/contract, integration tests.

**Estimated scope:** Medium.

### Task 15: FE story contract and bus adapter — OPEN

**Description:** Add story types, query adaptation, story acknowledgement mutation, and query invalidation
without changing checkpoint reveal behavior.

**Acceptance criteria:**
- [ ] Story DTOs adapt nullable timestamps and revisions safely.
- [ ] GET errors expose retry state.
- [ ] ACK mutation invalidates only the onboarding query and is replay-safe.
- [ ] Existing `OnboardingReveal` tests remain unchanged.

**Verification:** Web bus/type tests and existing onboarding reveal tests.

**Dependencies:** Tasks 9–11.

**Files likely touched:** `web/fusion-rpg-web/src/lib/bus/onboarding.ts`, `src/contract/types.ts`, tests.

**Estimated scope:** Small.

### Task 16: Rift prologue dialog — OPEN

**Description:** Build the four-beat dialog using `DialogShell`, static scene/copy data, progress text,
Next/Anchor/Skip controls, and the specified state machine.

**Acceptance criteria:**
- [ ] Beats 1–3 use `Next`; beat 4 uses `Anchor the lawn`.
- [ ] Every beat exposes `Skip intro`.
- [ ] `Esc` performs the same skip outcome.
- [ ] Duplicate actions cannot navigate twice.
- [ ] Loading, submitting, missing asset, and acknowledgement failure states are usable.

**Verification:** Component tests plus band-discipline and shell/focus tests.

**Dependencies:** Tasks 14–15.

**Files likely touched:** `web/fusion-rpg-web/src/stages/sanctum/RiftPrologueDialog.tsx`, tests.

**Estimated scope:** Medium.

### Task 17: Sanctum eligibility gate and lawn handoff — OPEN

**Description:** Integrate the dialog into `SanctumStage` for eligible Sanctum entry only, preserving
the mounted Sanctum, existing rail, current reward reveal, and existing lawn start action.

**Acceptance criteria:**
- [ ] Eligible unseen player sees the dialog before the first lawn.
- [ ] Settled players never see the blocking dialog.
- [ ] Direct lawn deep links and active runs are not trapped.
- [ ] Story failure exposes `Continue to lawn` and retry.
- [ ] `OnboardingReveal` remains the checkpoint queue.

**Verification:** `SanctumStage` integration tests, route/deep-link tests, and browser smoke.

**Dependencies:** Tasks 11, 15–16.

**Files likely touched:** `web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx`, related tests.

**Estimated scope:** Medium.

### Task 18: failure handling and lawn-start handoff — OPEN

**Description:** Implement loading, GET failure, acknowledgement failure, duplicate-navigation guards,
retry behavior, and the existing lawn-start handoff. A failed story read or acknowledgement must never
trap the player before the lawn; no browser-local value is accepted as acknowledgement authority.

**Acceptance criteria:**
- [ ] GET failure exposes retry plus `Continue to lawn`.
- [ ] ACK failure continues to the lawn and keeps the story eligible until first-victory housekeeping.
- [ ] `Next`, `Anchor the lawn`, `Skip intro`, and `Esc` cannot navigate twice.
- [ ] Duplicate result delivery cannot duplicate story or reward settlement.

**Verification:** Web integration tests with failed GET/POST, retry, reload, and duplicate action cases.

**Dependencies:** Tasks 11, 15–17.

**Files likely touched:** FE story feature, Sanctum handoff, web integration tests.

**Estimated scope:** Medium.

### Task 19: first-user guide handoff — OPEN

**Description:** Show the approved “Your first job” copy next to the existing playable prompt after the
story action, without adding a new progression surface.

**Acceptance criteria:**
- [ ] Copy appears after completed or skipped prologue.
- [ ] Copy points to the existing lawn objective and first-win Dave reveal.
- [ ] No Souls, XP, item, or checkpoint is granted by the guide copy.

**Verification:** Sanctum component tests and manual fresh-profile walkthrough.

**Dependencies:** Task 18.

**Files likely touched:** `web/fusion-rpg-web/src/stages/sanctum/FocusCard.tsx` or a small companion component.

**Estimated scope:** Small.

## Phase 4 — assets, accessibility, and documentation

### Task 20: semantic asset manifest and fallback — OPEN

**Description:** Add the v1 Rift asset manifest, stable media frame, square icon composition, and
`rift-placeholder` fallback.

**Acceptance criteria:**
- [ ] Both semantic roles resolve the selected v1 files.
- [ ] Missing/late image never shows a browser broken-image glyph.
- [ ] Placeholder preserves layout and accessible label.
- [ ] Icon is not wired into combat HUD chrome.

**Verification:** Web asset/component tests and visual fixture.

**Dependencies:** Tasks 3 and 16.

**Files likely touched:** `web/fusion-rpg-web/src/features/onboarding/`, selected files under `docs/assets/onboarding/` or FE public assets.

**Estimated scope:** Small–Medium.

### Task 21: asset validation and visual fixtures — OPEN

**Description:** Add repeatable checks for file existence, PNG dimensions, alpha, opaque backgrounds,
transparent compositing, and 32px/64px icon readability.

**Acceptance criteria:**
- [ ] Wrong dimensions or missing alpha fail the validation command.
- [ ] Lawn-colored and dark-dialog composites are covered.
- [ ] A future replacement can be validated without changing story code.

**Verification:** Asset validation command and fixture output.

**Dependencies:** Tasks 3 and 20.

**Files likely touched:** `scripts/`, FE tests/fixtures, asset spec if command details need recording.

**Estimated scope:** Small.

### Task 22: accessibility and responsive acceptance — OPEN

**Description:** Verify focus trap/restore, keyboard controls, screen-reader beat text, reduced motion,
contrast, touch targets, internal scrolling, and 320px behavior.

**Acceptance criteria:**
- [ ] Keyboard-only users can complete and skip the prologue.
- [ ] Screen readers receive beat number, speaker, line, and current action.
- [ ] Reduced motion removes transitions/distortion without removing meaning.
- [ ] Controls remain reachable on narrow viewports.

**Verification:** Web accessibility tests, browser viewport sweep, and manual keyboard/screen-reader pass.

**Dependencies:** Tasks 15–21.

**Estimated scope:** Medium.

### Task 23: player-guide and HTML synchronization — OPEN

**Description:** Update the human-facing guide and product-vision references so the lawn-first journey,
Rift promise, first-user copy, and shipped/WIP boundaries agree with the implementation.

**Acceptance criteria:**
- [ ] `docs/guide/` does not present the Rift as first-minute required gameplay.
- [ ] The first-session reward sequence remains accurate.
- [ ] Markdown/HTML sibling links follow the guide convention.
- [ ] Legacy onboarding references are clearly marked or linked to current docs.

**Verification:** Guide link scan, generated HTML check where applicable, and blind-reader review.

**Dependencies:** Tasks 1, 17–19.

**Files likely touched:** `docs/guide/`, `docs/ideas/onboarding-progression.md`, generated site pages.

**Estimated scope:** Small–Medium.

## Phase 5 — end-to-end acceptance and closeout

### Task 24: fresh-profile and replay E2E — OPEN

**Description:** Exercise the full path from fresh SQLite through prologue, lawn start, first victory,
existing Dave reward reveal, reload, duplicate acknowledgement, and duplicate result delivery.

**Acceptance criteria:**
- [ ] Prologue acknowledgement never creates a checkpoint.
- [ ] First victory still creates exactly the existing first-win reward path.
- [ ] Reload/duplicate result cannot replay rewards or story completion.
- [ ] API failure bypass remains playable.

**Verification:** Focused server/web E2E suites using a fresh database.

**Dependencies:** Tasks 8, 11, 12–23.

**Estimated scope:** Medium.

### Task 25: replay/degradation acceptance — OPEN

**Description:** Exercise interruption, replay, restart, duplicate-result delivery, API failure, missing
assets, injector absence, shader/resource failure, cap exhaustion, and reduced-motion behavior as one
release gate. Confirm every degraded path remains playable and does not mutate reward checkpoints.

**Acceptance criteria:**
- [ ] Restart before acknowledgement reopens at beat 1; restart after acknowledgement does not reopen.
- [ ] Duplicate ACK/result requests are idempotent and preserve the first stored outcome.
- [ ] Missing media, no injector, shader failure, VFX cap, and reduced motion all resolve to readable
  static presentation with usable controls.
- [ ] Existing reward checkpoint rows and receipts are unchanged in every case.

**Verification:** Focused Data/Web/VFX tests plus live degraded-mode smoke matrix.

**Dependencies:** Tasks 13, 18, 20–24.

**Estimated scope:** Medium.

### Task 26: final sweep and closeout — OPEN

**Description:** Run focused and repository-standard builds/tests/guards, check links and asset paths,
review narrative safety, and record evidence in this task pair.

**Acceptance criteria:**
- [ ] All P0 tasks are checked with command evidence.
- [ ] Relevant Core/Data/Server/Web tests pass.
- [ ] Dialog-band, accessibility, asset, and link guards pass.
- [ ] `git diff --check` passes.
- [ ] Deferred final-art/map/combat items are explicitly recorded as P1/P2; serious portal/quarantine VFX are not deferred.

**Verification:** Final CI-equivalent command set and manual closeout review.

**Dependencies:** Tasks 1–25.

**Estimated scope:** Medium.

## Deferred follow-ups

- [ ] Commission or replace v1 art after free-asset research.
- [ ] Add animated pulse/shimmer only after static acceptance.
- [ ] Decide whether the 64px icon becomes a long-term map marker.
- [ ] Add Chronicle/map replay entry if a later story library is approved.
- [ ] Add telemetry only if an existing product sink and privacy review are available.
