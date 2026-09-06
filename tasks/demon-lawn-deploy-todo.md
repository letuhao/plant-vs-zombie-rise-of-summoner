# Task list: demon lawn deploy

Plan: [demon-lawn-deploy-plan.md](demon-lawn-deploy-plan.md). Specs:
[docs/architecture/demon-lawn-deploy-map.md](../docs/architecture/demon-lawn-deploy-map.md),
[docs/architecture/demon-lawn-deploy/](../docs/architecture/demon-lawn-deploy/) (`spec-lawn-deploy-core.md`,
`spec-lawn-deploy-events.md`, `spec-zomboss-deploy-ai.md`).

Dependency order: Phase 1 (`lawn-deploy-core`) → Checkpoint 1 → Phase 2 (`lawn-deploy-events`) →
Checkpoint 2 → Phase 3 (`zomboss-deploy-ai`) → Checkpoint 3 (program close). T1.1-T1.5 may build in
parallel; T1.6 needs all of them.

## Phase 1 — `lawn-deploy-core`

### T1.1 — Patron deploy refusal · **S** · 2 files — **DONE 2026-09-06**

- `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` (`TryBeginUniqueDeploy`) — refuses with
  `"patron.cannot-deploy"` before admitting any spawn, reusing the same `IsPatronUnlocked`
  (`RpgStore.Fusion.cs:354`) check fusion's own sacrifice-refusal already uses.
- **Commander refusal NOT built — a real, investigated finding, not a skip.** `CommanderId`
  (`Core/Commanders/CommanderId.cs`) is a fixed Dave/Zomboss enum with no demon-instance binding
  anywhere; `PlayerEmpireCommanders.ForPlayer` always returns `[Dave]`. There is no state today meaning
  "this specimen is the Commander," so nothing exists yet to refuse against. A code comment at the real
  call site names this precisely for whoever builds that binding later. See
  `spec-lawn-deploy-core.md`'s own Correction 1 for the full trace.
- Acceptance:
  - [x] Deploying the specimen holding the active Patron aura refuses with `patron.cannot-deploy`.
  - [x] Deploying any other owned demon specimen is unaffected — no false-positive refusal.
  - [x] Switching the Patron away lets the previously-designated specimen deploy again (proves the
        refusal reads live state, not a cached/stale flag).
  - [ ] ~~Deploying the current default Commander's own `instanceId` refuses~~ — inapplicable today, see
        above. Re-open this line once a real demon-to-Commander binding exists.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeployCommanderRefusal` — **39/39
  passing**, confirmed live (3 new tests + the full existing `PatronStoreTests`/`UniqueActorStoreTests`
  suites, zero regressions).
- Files: `RpgStore.UniqueActors.cs` (edit), `tests/FusionRpg.Data.Tests/DemonLawnDeployCommanderRefusalTests.cs` (new).

### T1.2 — Reconciled trait-binding on every deploy · **M** · 3-4 files

- `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` — new `ReconcileDemonTraitBindingsUnlocked`,
  mirroring `ReconcileUniqueEquipmentAtomBindingsUnlocked`'s exact diff shape (wanted-vs-existing,
  idempotent, no blind insert), producing `effect_binding` rows for each of the specimen's `TraitIds`
  mapped to its real `trait.{traitId}` container. Called from `DeployAsync`, always, before the spawn
  command is sent — never at mint time.
- Acceptance:
  - [ ] A fresh specimen's own traits bind correctly on first deploy.
  - [ ] Re-deploying the SAME specimen with unchanged traits writes no new bindings (idempotent).
  - [ ] **A specimen promoted between two deploys (via `PromotionUnlocked`, which appends traits to the
        SAME `instanceId`) gets the NEW trait's binding on its next deploy** — the exact case the
        strengthen pass found the naive "bind at mint" design would silently miss.
  - [ ] `AtomPushService`'s output for that owner includes exactly the specimen's current trait
        containers — nothing stale, nothing missing.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy`
- Files: `RpgStore.UniqueActors.cs` (edit), `tests/FusionRpg.Data.Tests/DemonLawnDeployTests.cs` (new).

### T1.3 — Overflow-safety verification through `AtomCompiler.cs` · **S** · investigation + fix if needed

- Read `AtomCompiler.cs`'s actual numeric handling (confirmed `int`-typed throughout, e.g. `static int
  Int(...)`) against a demon's own `long`-scale magnitudes (`ConcreteSpecies.PTheta`). Determine whether
  a real narrowing risk exists on the path T1.2's bindings will actually exercise, not a hypothetical one.
- Acceptance:
  - [ ] Either: confirmed no demon-scale magnitude actually reaches `AtomCompiler`'s `int`-typed surface
        (traits are flag-shaped grants, not raw magnitudes) — documented with the exact call path checked,
        not asserted from the type name alone.
  - [ ] Or: a real narrowing risk is found, and fixed before T1.2 is considered done — per CLAUDE.md's own
        rule, overflow throws, it never silently wraps or truncates.
- Verify: `python scripts/audit-overflow.py` (re-run against the new code path); a targeted test if a fix
  was needed.
- Files: investigation only, or `AtomCompiler.cs` + a regression test if a fix is required.
- **Sequencing note**: run this BEFORE calling T1.2 complete, not after — it can change T1.2's own
  implementation.

### T1.4 — `DeployMode` → spawn side/type, and the `side`-column decision · **S** · 2 files

- `UniqueActorService.DeployAsync` resolves spawn `side`/`typeId` from the demon's own `DeployMode`
  (`HypnoAlly` vs `PlantAvatar`) rather than blindly trusting the actor's raw `side` column.
- **Default per the plan's own Gates section**: for a `HypnoAlly` deploy, also UPDATE
  `rpg_unique_actors.side` to match the real spawn side, so `GET /api/actors/{id}/derived`
  (`AuraDerivedEndpoints.cs:36-52`) keeps resolving the correct side for that entity. This is a real,
  deliberate mutation of a column every other code path currently treats as immutable — reviewed here,
  not silently introduced.
- Acceptance:
  - [ ] A `PlantAvatar`-mode demon deploys plant-side, matching its owner's own side, column unchanged.
  - [ ] A `HypnoAlly`-mode demon deploys zombie-side regardless of owner's side, AND the `side` column is
        updated to match — `GET /api/actors/{id}/derived` resolves the correct side afterward.
  - [ ] No other code path that reads `rpg_unique_actors.side` (grepped exhaustively, not sampled) is
        broken by this becoming mutable for HypnoAlly demons specifically.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy`, plus a targeted
  `AuraDerivedEndpoints` test for the HypnoAlly case.
- Files: `UniqueActorService.cs` (edit), `RpgStore.UniqueActors.cs` (edit, the side-update call).

### T1.5 — Species-magnitude delivery path · **M** · 2-3 files

- A demon specimen's own base stats (species magnitudes, not just traits) need a delivery path into the
  deploy grant — currently undesigned. Design and build the second binding kind alongside T1.2's trait
  bindings (a second `effect_binding` shape, or a direct `Absolutes` write into the loadout — pick
  whichever keeps `AtomPushService`'s existing contract intact, per this module's own "never invent a
  second wire format" boundary).
- Acceptance:
  - [ ] A deployed demon's live combat stats (not just its trait effects) measurably differ from a
        vanilla `type_id` unit's defaults, traceable to the specimen's own `PTheta`/magnitudes.
  - [ ] The chosen mechanism does not reintroduce the `UniqueLoadoutMerge.Merge` non-additive-merge
        trap this module's own spec already ruled out once.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy`
- Files: TBD once the mechanism is chosen (design task, then implementation).

### T1.6 — Live E2E proof · **S** · 1-2 files

- Extend `tests/FusionRpg.E2E.Tests/StorageE2ETests.cs`-style coverage: deploy a real demon specimen,
  read it back, assert `ActiveBound`; live-lawn check (per `live-lawn-quick-start` skill) confirming the
  spawned entity's combat stats/trait effects are the specimen's own on a real board.
- Acceptance:
  - [ ] E2E test passes in CI.
  - [ ] A real, owner-observable live-lawn check confirms the same, matching this repo's own established
        "a live check is required" rule for a sibling catalog flip.
- Verify: `dotnet test tests/FusionRpg.E2E.Tests --filter DemonLawnDeploy`; `live-lawn-quick-start` skill.
- Files: `tests/FusionRpg.E2E.Tests/DemonLawnDeployE2ETests.cs` (new).

### ✅ Checkpoint 1 — a demon can really deploy and really fight as itself
- [ ] T1.1-T1.6 all done and verified.
- [ ] `FusionRpg.Data.Tests`/`FusionRpg.Core.Tests`/`FusionRpg.E2E.Tests` full suites green.
- [ ] All four boundary guards clean (`guard-single-writer`, `guard-secondary-no-unity`,
      `guard-funnel-delta`, `guard-dal`).
- [ ] A real live-lawn check: deploy a real demon, observe it on the board with its own stats/traits.

## Phase 2 — `lawn-deploy-events`

### T2.1 — Hot/Cold-safe roster snapshot · **M** · 2 files

- A frozen, `board.start`-time snapshot of the player's own deploy-eligible roster (excluding
  Commander/Patron per T1.1's own refusal reasons, so the UI never even offers an ineligible demon),
  mirroring `commander-surface-map.md`'s own Hot/Cold discipline for an analogous problem.
- Acceptance:
  - [ ] The snapshot is taken once, at `board.start`, never re-queried mid-match.
  - [ ] A roster change mid-match (e.g., a fusion completing) does not retroactively change what's
        available to trigger THIS run — only the next one.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployEvents`
- Files: `src/FusionRpg.Core/Match/` (new snapshot type), tests.

### T2.2 — The trigger condition evaluator + tuning file · **M** · 3 files

- A pure function over live match state + the T2.1 snapshot → yes/no + which case, matching
  `AmbushDraw`'s own shape. `data/tuning/lawn-deploy-events.v1.json` (new) carries every numeric knob
  (frequency, HP thresholds, per-run budget) — **default: no cost beyond the frequency cap itself**, per
  the plan's own Gates section.
- Acceptance:
  - [ ] Scenario table, one fact per named case × {fires, does-not-fire-below-threshold,
        does-not-fire-if-already-used-this-run} — not a single smoke test.
  - [ ] Every numeric knob resolves from the tuning file, zero bare literals in the evaluator itself.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployEvents`
- Files: `src/FusionRpg.Core/Match/LawnDeployEventEvaluator.cs` (new),
  `data/tuning/lawn-deploy-events.v1.json` (new), `tests/FusionRpg.Core.Tests/Match/LawnDeployEventsTests.cs` (new).

### T2.3 — Determinism · **S** · included in T2.2's own files

- The evaluator derives its own randomness (if any case needs it) via
  `SeededRng.DeriveStream(matchSeed, "lawn-deploy-event:{caseId}")` — never `System.Random`.
- Acceptance:
  - [ ] Same `(matchSeed, caseId)` fed twice ⇒ byte-identical decision — an explicit test, not an
        inferred property.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployEvents`

### T2.4 — Plant-side prompt/UI surface · **M** · 2-3 files (server + FE)

- A new REST/SignalR surface for the plant-side deploy prompt; exact shape planned once T2.1-T2.3 land
  and the real trigger data exists to design a UI against.
- Acceptance:
  - [ ] A fired trigger reaches the player-facing UI in a live match.
  - [ ] Accepting the prompt calls the T1.x deploy path for the player's own chosen eligible demon.
- Verify: manual live-lawn check; a server-side integration test for the REST/SignalR surface itself.
- Files: TBD once designed.

### ✅ Checkpoint 2 — a real trigger fires in a live run, and the player can act on it
- [ ] T2.1-T2.4 all done and verified.
- [ ] `FusionRpg.Core.Tests` full suite green.
- [ ] A real live-lawn check: play to a triggered case, see the prompt, deploy an owned demon through it.

## Phase 3 — `zomboss-deploy-ai`

### T3.1 — `ILawnBoardView` (the enforcing boundary) · **M** · 2 files

- A narrow, read-only projection of live board state (visible units, HP, wave) that the Zomboss scorer's
  own function signature takes EXCLUSIVELY — never `MatchRuntime`/`Board` directly. Reads
  side/ownership through the already-shipped `SpecimenOwnershipOracle` (`decisions.md`'s "Buff/debuff
  scope" row, 2026-08-29/30) rather than re-deriving ownership logic.
- Acceptance:
  - [ ] A compile-time proof the scorer's own signature cannot accept `MatchRuntime`/`Board` — mirroring
        `spec-ai-commander.md`'s own `IWorldView` leak-proof precedent.
  - [ ] A hypnotized/side-swapped entity resolves to its real owner through `SpecimenOwnershipOracle`,
        not the entity's current on-board side.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/ILawnBoardView.cs` (new), tests.

### T3.2 — Zomboss's own demon roster/pool · **M** · 2 files

- **Default per the plan's own Gates section**: the same summonable species pool the player draws from,
  filtered to the current level's own threat band. `data/tuning/zomboss-deploy-ai.v1.json` (new) carries
  the difficulty→policy-id mapping and any roster-size knob.
- Acceptance:
  - [ ] Zomboss's own available pool for a given level is deterministic and reproducible from the
        level's own data, not hand-authored per level.
  - [ ] The default is explicitly named as a tunable/reversible choice in code comments, not presented
        as a permanent design decision.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/` (new roster-source type), `data/tuning/zomboss-deploy-ai.v1.json` (new).

### T3.3 — The scorer · **M** · 2-3 files

- A deterministic scorer over `ILawnBoardView` (T3.1) + the T3.2 roster, choosing whether and which
  demon to deploy. Weights come from the T3.2 tuning file. Derives its own randomness via
  `SeededRng.DeriveStream(matchSeed, "zomboss-deploy-ai:{caseId}")`.
- Acceptance:
  - [ ] Scenario table: no eligible demon (declines), exactly one (picks it), multiple candidates at
        different board states (ranking is asserted, not just "it picked something").
  - [ ] Determinism test: same `(board snapshot, matchSeed, caseId)` twice ⇒ byte-identical decision.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/ZombossDeployPolicy.cs` (new),
  `tests/FusionRpg.Core.Tests/Match/Ai/ZombossDeployAiTests.cs` (new).

### T3.4 — Wire Zomboss's deploy through the existing path · **S** · 1 file

- Zomboss's own chosen deploy calls the SAME `DeployAsync`/Funnel path a player's deploy uses — no new
  write path, no shortcut.
- Acceptance:
  - [ ] A Zomboss-triggered deploy is indistinguishable, at the deploy-mechanism level, from a
        player-triggered one — same refusal reasons apply (though Zomboss's own roster is pre-filtered
        to exclude anything that would trip T1.1's Commander/Patron refusal, so it should never fire).
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: wherever the event-fired-on-zombie-side handler lives (T2.4's own counterpart for the zombie side).

### ✅ Checkpoint 3 — program closes: both sides work in one live sitting
- [ ] T3.1-T3.4 all done and verified.
- [ ] `FusionRpg.Core.Tests` full suite green; all four boundary guards clean.
- [ ] **The capability map's own program-level acceptance, proven directly**: in a real lawn run, played
      to a triggered event, on both sides — the plant-side prompt fires and the player deploys an owned
      unique demon with its own stats/traits provably live; separately, a Zomboss-triggered event results
      in Zomboss deploying one of its own unique demons, chosen by the real T3.3 policy, not a stub.
- [ ] Every "Deliberately deferred" item from the capability map is still correctly out of scope (no
      scope creep into Commander-picker UI, world-map `ai-commander`, or class-system's own point-economy
      gap during this build).
