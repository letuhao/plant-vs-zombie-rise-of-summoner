# Implementation Plan: onboarding Rift prologue

Source of truth: [onboarding-gnome-teaser.md](../docs/ideas/onboarding-gnome-teaser.md) and
[spec-onboarding-rift-assets.md](../docs/design/spec-onboarding-rift-assets.md).
Supporting contracts: [canon-boundary-and-onboarding.md](../docs/research/pvz-lore/canon-boundary-and-onboarding.md),
[spec-first-session-progression.md](../docs/architecture/standalone/spec-first-session-progression.md),
[game-gui-principles.md](../docs/architecture/game-gui-principles.md), and
[game-gui-map.md](../docs/architecture/game-gui-map.md).
Task list: [onboarding-rift-todo.md](onboarding-rift-todo.md).

This plan implements the four-beat, presentation-only Rift prologue before the first playable lawn.
It adds a separate durable story acknowledgement to the current SQLite-backed profile, a Sanctum-owned
band-3 dialog, the selected v1 Rift base assets, a serious shared-pipeline VFX treatment, and the
first-user guide handoff. It does **not** add player-facing save slots, alter the three server-owned
progression checkpoints, create a new route, or introduce a combat rule.

The current reward reveal remains authoritative and separate:
`first-win-dave` → `level-3-general-species` → `level-4-dave-equipment`.
The legacy sunflower/bind specification remains historical and is not revived.

## Architecture decisions

1. **One story, separate ledger.** Store `rift-prologue` version 1 in a new
   `rpg_onboarding_story` table. Never encode story acknowledgement in
   `rpg_onboarding_checkpoint`, the media database, or browser storage.
2. **Server-owned eligibility.** The server returns story state and `eligible`; the client additionally
   requires Sanctum as the current stage and no active lawn run before opening the dialog.
3. **Existing shell and destination.** Render a `DialogShell` over the mounted Sanctum. Both
   `Anchor the lawn` and `Skip intro` use the existing lawn-start destination.
4. **Safe bypass.** A failed story GET or acknowledgement never blocks starting the lawn. If a first
   settled PvZ victory occurs while the story is still unseen, the separate story row may be marked
   `skipped` as housekeeping; reward checkpoints remain untouched.
5. **Base-art plus serious VFX.** v1 uses the bright PvZ-style PNGs and a semantic manifest with a designed
   fallback placeholder. Serious portal/quarantine VFX are implemented through the existing VFX SSOT,
   not as one-off component particles.
6. **Existing reward queue remains unchanged.** `OnboardingReveal` continues to render the checkpoint
   queue. The prologue is not a fourth checkpoint and cannot grant Souls, XP, items, or progression.
7. **Narrative boundary is explicit.** Gnomes/Gnomiverse are inspiration from the Garden Warfare branch;
   Rift, Fracture, Void, quarantine, and multiverse-crisis claims are Rise of Summoner fiction.
8. **PVZ image provenance stays in media SQL.** Existing `rpg-media.sqlite` tables
   (`type_icon_layers`, `type_icons`) and `TypeIconStore` track captured PvZ image dumps. If the Rift
   manifest needs a durable semantic mapping, add the metadata-only `rift_asset_sources` registry in the
   media DB with `(asset_id, role, source_kind, side, type_id, layer, source_uri, sha256, captured_utc,
   revision)`; validate `pvz_dump` keys against `type_icon_layers`. Never duplicate image BLOBs or
   progression state into `rpg-hot.sqlite`.

## Dependency graph

```text
source-of-truth reconciliation ──┐
story DTO/domain contract ────────┼── SQLite story ledger + bootstrap/backfill
                                 │             │
asset manifest + media SQL ────┘             ├── story GET/ack API
VFX cue/recipe contract ───────┘             │
                                               ├── FE bus/state adapter
                                               │
                               Rift DialogShell + Sanctum gate
                                               │
                              VfxCatalog + VfxDirector integration
                                               │
                         first-user guide + player-doc synchronization
                                               │
                      fresh-profile / replay / accessibility / VFX gates
```

## Phases and checkpoints

### Phase 0 — contract and documentation foundation

- T1: reconcile all source specs, legacy references, and guide language;
- T2: define story constants, DTOs, outcomes, named refusal reasons, and revision semantics;
- T3: freeze the semantic asset manifest, display bounds, fallback, PvZ media-dump source keys, and validation contract;
- T4: define Rift VFX cue vocabulary, recipes, anchors, caps, reduced-motion fallback, and live-proof requirements against `vfx-ssot.md`.

**Checkpoint A:** no implementation task contradicts the teaser, asset, lore, GUI, or first-session
progression contracts; legacy docs are clearly marked historical.

### Phase 1 — media provenance and durable story state

- T5: verify/extend `rpg-media.sqlite` image-dump metadata and source-key validation through `TypeIconStore`,
  adding the metadata-only `rift_asset_sources` registry when the semantic manifest needs durable mapping;
- T6: add the SQLite story table, indexes, bootstrap row, and existing-profile backfill;
- T7: implement ordered story reads and idempotent acknowledgement writes;
- T8: connect first settled PvZ victory housekeeping without changing checkpoint settlement or rewards.

**Checkpoint B:** media/Data tests prove PvZ image-dump provenance in a fresh `rpg-media.sqlite`,
semantic source-key validation (and `rift_asset_sources` when enabled), fresh-profile story state,
migration/backfill, duplicate acknowledgement, revision behavior, and first-victory skip behavior;
the hot/profile database contains no copied image BLOBs.

### Phase 2 — server API

- T9: extend the onboarding DTO projection with `stories` and `eligible`;
- T10: add the story acknowledgement endpoint and named 4xx reasons;
- T11: add server endpoint tests and backward-compatible checkpoint response coverage.

**Checkpoint C:** the API can be polled after reload, acknowledges exactly once, and never mutates the
checkpoint queue.

### Phase 3 — serious Rift VFX and frontend story flow

- T12: add Core VFX recipes and pure rules for `rift.portal.open`, `rift.portal.surge`,
  `rift.quarantine.seal`, and `rift.quarantine.fade`;
- T13: wire the cues through `VfxDirector`, existing anchors/resources, caps, skip reasons, and
  `scripts/prove-vfx.ps1`; no one-off host or Unity path;
- T14: expose the active beat's semantic VFX cue through a presentation seam, keeping VFX lifecycle
  ownership in the shared host rather than the dialog;
- T15: adapt story DTOs in the FE contract and onboarding bus;
- T16: build the four-beat `RiftPrologueDialog` using the existing `DialogShell` and layer/focus rules;
- T17: integrate the eligibility gate into `SanctumStage` without replacing `OnboardingReveal`;
- T18: implement loading, GET failure, acknowledgement failure, duplicate-navigation guards, and the
  existing lawn-start handoff;
- T19: add first-user guide copy beside the existing playable prompt.

**Checkpoint D:** live VFX proof shows the portal/quarantine read, browser tests show the prologue once,
skip/reload behaves durably, and the existing reward reveal still follows the real lawn victory.

### Phase 4 — assets, accessibility, and guide sync

- T20: add the semantic Rift asset manifest and placeholder fallback;
- T21: add asset dimension/alpha/compositing validation and 32px/64px visual fixtures;
- T22: complete keyboard, screen-reader, reduced-motion, contrast, touch-target, and 320px viewport
  coverage;
- T23: synchronize `docs/guide/` Markdown/HTML and the onboarding progression brief.

**Checkpoint E:** the story remains usable with missing assets, reduced motion, keyboard-only input, and
small viewports; player-facing docs describe the same lawn-first handoff.

### Phase 5 — end-to-end acceptance and closeout

- T24: fresh SQLite profile E2E path: prologue → lawn → first victory → existing Dave reveal;
- T25: replay/restart/duplicate-result/API-failure/VFX-degradation acceptance;
- T26: final source sweep, focused/full test suites, VFX proof, build, guard checks, link checks, and task closeout.

**Completion checkpoint:** all P0 tasks pass with command evidence; P1 telemetry is either implemented or
explicitly deferred; no story behavior changes the three reward checkpoints.

## Verification policy

Each task runs the narrowest focused test first, then the module build. The final sweep runs the relevant
Core/Data/Server/Web suites, dialog-band and accessibility guards, asset validation, guide-link checks,
`git diff --check`, and the repository's normal build commands. Live acceptance uses a fresh SQLite
profile directory and the existing local deploy path; it does not patch the game binary or use browser-local
state as evidence. Serious Rift VFX must also pass the existing VFX proof path.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---:|---|
| Story state is accidentally coupled to checkpoint settlement | High | Separate table/DTOs, dedicated endpoint, and tests asserting unchanged checkpoint rows |
| API failure traps a new player before the lawn | High | Static copy, explicit bypass action, no blocking error path |
| Dialog violates existing band/focus rules | High | Use `DialogShell`, band guard, focus-trap/restore tests, no custom overlay stack |
| Existing profiles replay the prologue or new players miss it | Medium | Bootstrap/backfill plus server `eligible` projection and first-victory housekeeping |
| Portrait icon source is used directly and crops badly | Medium | Square runtime canvas contract and 32px/64px visual fixture |
| Missing PNG produces a broken image or layout shift | Medium | Designed placeholder, stable media frame, missing-asset test |
| Guide and product-vision docs contradict the UI | Medium | Sync `docs/guide/`, `onboarding-progression.md`, and generated HTML in one task |
| Lore copy implies unsupported PvZ canon | Medium | Canon-boundary review and narrative safety acceptance test |
| Rift effect becomes a one-off particle stack outside the VFX SSOT | High | Add cue/recipe/anchor rows, director wiring, caps, skip reasons, and live proof before UI sign-off |
| Captured PvZ image BLOBs are duplicated into hot/profile SQL | Medium | Keep dumps in `rpg-media.sqlite`; store only source keys/provenance in the Rift manifest |

## Open decisions and defaults

- **Asset manifest location:** default to an imported feature module at
  `web/fusion-rpg-web/src/features/onboarding/riftAssets.ts`; a public path is acceptable only if it
  preserves the same versioned manifest/fallback contract.
- **Telemetry:** recommended events are observational only and not a release gate. Default to defer if
  no existing telemetry sink is available.
- **Final art replacement:** defer until free-asset review or commissioning; preserve semantic roles and VFX cues.
- **Replay entry point:** defer a Chronicle/map replay surface; v1 only governs the first eligible
  Sanctum entry.

## Definition of done

- All P0 tasks in `onboarding-rift-todo.md` have command evidence and are checked.
- Fresh and existing profiles produce the correct story eligibility and durable acknowledgement behavior.
- The prologue uses the existing band/layer/focus model and never blocks a lawn start on API or asset failure.
- The three first-session reward checkpoints remain unchanged and replay-safe.
- Selected v1 assets load through semantic roles, pass validation, and render the designed fallback.
- PvZ image-dump references resolve through `rpg-media.sqlite`/`TypeIconStore`, and serious Rift cues pass the VFX proof.
- Keyboard, screen-reader, reduced-motion, contrast, touch, and narrow viewport checks pass.
- Player guide copy, lore boundary, and implementation specs are synchronized.
- Final build, focused tests, guards, link checks, and `git diff --check` pass.
