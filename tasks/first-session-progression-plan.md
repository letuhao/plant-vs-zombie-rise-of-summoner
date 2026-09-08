# Implementation Plan: first-session progression

Source of truth: [spec-first-session-progression.md](../docs/architecture/standalone/spec-first-session-progression.md).
Task list: [first-session-progression-todo.md](first-session-progression-todo.md).

This plan implements the approved, server-owned onboarding sequence over the existing lawn-first loop:
first settled PvZ victory → Souls and Crazy Dave sheet → level 3 plus a source-validated general spawn →
empire species progression and automatic allocation → level 4 → one deterministic Dave equipment reward.
It does not revive the legacy sunflower/bind flow, add a save system, add a world-map unlock, or create a
second progression curve.

The plan is grounded against the design gate's architecture, standalone, demon-source, data, economy,
commander/item, and FE rows. The key existing seams are `RpgStore.Init`/`EnsureHotSchema` and the single
Data ingest transaction (`src/FusionRpg.Data/Sqlite/RpgStore.cs:46-98`), existing progression and XP
projection (`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs`), typed activity provenance
(`src/FusionRpg.Data/Sqlite/RpgStore.cs:1690-1734`), player bootstrap (`RpgStore.cs:63`), commander list
(`src/FusionRpg.Server/CommanderEndpoints.cs:43-82`), and the existing item assignment boundary
(`src/FusionRpg.Server/ItemEquipEndpoints.cs:30-72`).

## Architecture decisions

1. **One durable checkpoint ledger.** Add `rpg_onboarding_checkpoint` in `FusionRpg.Data`. Absence is
   locked; rows move `earned → claimed`. The `(player_id, checkpoint_id)` primary key is the idempotency
   gate. Browser state is only presentation state.
2. **Settlement is the grant boundary.** Checkpoints are evaluated in the same SQLite transaction that
   persists the source fact/result, Souls, player/species XP, reward receipt, and Dave item assignment.
   A retry therefore returns the same durable result and cannot mint a second reward.
3. **PvZ-only evidence.** The evaluator accepts only a settled victory from a PvZ game profile and only
   a matching typed `EmpireGeneral` claim for the level-3 checkpoint. `typeId`, payload instance ids,
   opaque injector source tokens, and web simulations are never evidence.
4. **Normal spawns declare their source.** The injector producer carries `sourceKind` and `sourceId` in
   the existing spawn event payload while preserving record-then-drain. Unique and commander paths keep
   their own source classifications and never fall back to empire species progression.
5. **Dave remains a commander id.** Prefer a player-owned commander item scope (`OwnerKind.Player`, the
   player's id, `standard` role) rather than fabricating a unique-actor instance. The scope must reuse the
   existing item instance/assignment/effect projection and be visible from the commander sheet.
6. **Content is data.** The basic item, role, display name, effect/container, deterministic reward seed,
   and any XP/Souls values live in existing tuning/catalog/seed surfaces. No balance number or private
   level curve is introduced in code.
7. **One in-stage reveal.** The FE adds a server-backed queue layer over Sanctum or the settled run result,
   with loading/error/retry and acknowledgement. It does not add a top-level onboarding route or silently
   reuse the legacy `FirstRunReveal` as an authoritative grant surface.

## Dependency graph

```text
checkpoint contract + evaluator
          │
          ├── SQLite schema/read/write primitives ──┐
          │                                          ├── atomic settlement + reward receipts
typed normal-spawn source producer ──────────────────┘
          │
          ├── species checkpoint projection
commander item scope + concrete item content ────────┤
                                                     ├── onboarding GET/claim API
                                                     └── in-stage FE reveal queue
                                                              │
                                                     legacy branch gate/removal
                                                              │
                                                     fresh-DB + replay live acceptance
```

## Phases and checkpoints

### Phase 0 — contract and persistence foundation

- T1: checkpoint domain contract and pure eligibility evaluator
- T2: checkpoint schema, migration, and Data projection primitives
- T3: atomic result settlement and checkpoint receipt orchestration

**Checkpoint A:** core/Data tests prove PvZ gating, ordering, level jumps, fail-closed sources, and
transaction idempotence before any UI is wired.

### Phase 1 — evidence and species progression

- T4: typed `EmpireGeneral` producer for ordinary PvZ spawns
- T5: concrete species/event fixtures and source-conformance regression suite
- T6: authoritative level-3 species reveal payload and automatic allocation projection

**Checkpoint B:** a deterministic settled PvZ run can produce a real species checkpoint; unique, commander,
untrusted, and `webrpg-1` facts cannot.

### Phase 2 — Dave equipment reward

- T7: commander-owned equipment scope decision and persistence path
- T8: deterministic basic item content/catalog/validator fixture
- T9: level-4 mint, assignment, projection, and replay-safe reward receipt

**Checkpoint C:** Dave's item is a real persisted item in the selected owner scope, survives restart, and is
not visible through the unique-specimen route by accident.

### Phase 3 — server API and player surface

- T10: onboarding GET and claim endpoints with stable DTOs/reason codes
- T11: in-stage FE reveal queue and navigation into existing sheets
- T12: legacy first-run branch removal/gating and documentation synchronization

**Checkpoint D:** browser reload/kill/retry preserves the same ordered queue; acknowledgement never grants.

### Phase 4 — live acceptance and closeout

- T13: fresh SQLite integration/replay harness
- T14: real deploy-play acceptance and evidence capture
- T15: full regression, stale-code sweep, and closeout

**Completion checkpoint:** required focused suites, full regression suites, build/guard checks, and live
acceptance all pass; no unchecked spec conformance item remains.

## Verification policy

Every task must run its focused tests and a build appropriate to its module. The final sweep runs Core,
Data, Guard, Server, and Web unit suites, the relevant Playwright suites, `git diff --check`, the DAL/source
guards, and the repository's normal build commands. Live testing uses a fresh SQLite directory and the
existing `scripts/deploy-play.ps1` workflow; it never edits the game binary and never uses HP polling as
onboarding evidence.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---:|---|
| A replay settles facts twice and mints duplicate Souls/item rewards | High | Source-result correlation plus checkpoint/reward receipt uniqueness inside one SQLite transaction; replay tests before API work |
| A missing or forged source claim awards empire species XP | High | Require parsed `demon.progression.v1:general:<species>` and verify side/type species match; store invalid claims as `untrusted` |
| Web/simulation results satisfy a lawn checkpoint | High | Central PvZ profile predicate in evaluator and joined-run tests for `webrpg-1` |
| Dave item is forced through a unique specimen API | High | Implement the commander-owned scope first; explicit refusal test for fabricated Dave instance ids |
| Existing saves lack the new table or have partial old state | Medium | Additive `CREATE TABLE IF NOT EXISTS` migration, empty view for Player 1, and boot/restart tests |
| FE shows authority before settlement or loses a reveal on reload | Medium | GET is authoritative, claim is acknowledgement-only, four-state UI tests, Playwright reload/kill checks |
| Spawn payload change increases hook work | Medium | Add transport fields at record/drain boundary only; no synchronous network or state scan; producer timing regression |
| Content fixture becomes an accidental balance SSOT | Low | Keep item and reward values in existing catalog/tuning/seed files and validate through the real loaders |

## Open decisions and defaults

The only product decision required before T7 implementation is the recommended commander-owned item scope.
Default: `OwnerKind.Player` + player id + `standard` role, with Dave remaining `commander:dave`. If the owner
chooses the alternative (Dave as a persistent unique actor), T7 must stop and split that larger design into
its own program; all tasks that do not depend on Dave equipment continue.

No other hard pre-work gate is introduced. Unknown content can use a clearly marked, reversible fixture
until T8's real catalog validation; unanswered optional FE copy or animation choices default to the existing
stage/layer motion and player vocabulary rules.

## Definition of done

- All fifteen tasks and four checkpoints in the task list are checked with command evidence.
- The three checkpoint ids and their reward receipts are durable, ordered, monotonic, and idempotent.
- Normal PvZ producer emits typed general provenance; unique/commander/untrusted paths remain isolated.
- Dave's first item is real, deterministic, persisted, and visible in the commander sheet.
- The legacy sunflower/bind branch cannot claim or impersonate the current sequence.
- Fresh-database live replay proves first victory, level-3 species, level-4 equipment, restart, reload, and
  duplicate settlement behavior.
