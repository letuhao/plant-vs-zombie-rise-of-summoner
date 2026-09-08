# Task list: drop-tables

See `tasks/drop-tables-plan.md` for architecture decisions, dependency graph, and risks. Specs:
`docs/architecture/item/spec-rate-floor.md`, `spec-rate-authoring.md`,
`docs/architecture/world-map-runtime/spec-sector-loot-wiring.md`,
`docs/architecture/base-defense/spec-siege-loot.md`.

## Phase 1 — `rate-floor` (item)

### Task 1: Tunable + `DropRateFloor` core logic

**Description:** Add the new tunable file and the two core functions (`ValidateEntry`,
`ValidateGroupAcrossBreakpoints`) exactly as specced in `spec-rate-floor.md`, including the
Enabled-flag short-circuit and the corrected `{lo} ∪ {hi+1}` breakpoint set (both fixed in review —
do not reintroduce the original, wrong `{lo, hi}` framing).

**Acceptance criteria:**
- [x] `data/tuning/drop-rate-floor.v1.json` exists with `minRatePerMillion: 1`, T5/T6-compliant (no
      built-in default anywhere in the loader).
- [x] `DropRateFloor.ValidateEntry` takes the caller-computed effective weight, never re-derives
      `entry.Weight` directly.
- [x] `ValidateGroupAcrossBreakpoints` walks `{every MinIlvl} ∪ {every MaxIlvl + 1}`, not `{MinIlvl,
      MaxIlvl}`.
- [x] A disabled entry (`Enabled == false`) with a real positive `Weight` is never checked.
- [x] A single-entry group's trivial always-pass behavior is proven by a test, not just documented.

**Done 2026-09-07.** 12/12 new tests green, 1020/1020 `Items` namespace regression clean. One real
deviation from the spec's own pseudocode, found reading the real code: `DropVolumeTuning`'s established
convention puts `Parse`/`Validate` directly on the tuning record itself, not a separate
`XxxTuningLoader` class — `DropRateFloorTuning.Parse` matches that, no separate loader class exists.
One test bug self-caught and fixed: an initial "never touches the rarity ladder" guard did a naive
string scan that failed against the class's own doc comment (which legitimately *names*
`dropWeightPer100k` to explain the boundary, matching this codebase's documentation style) — replaced
with a reflection-based check on the type's actual method signatures.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropRateFloor"`
- [ ] Build succeeds: `dotnet build src/FusionRpg.Core/FusionRpg.Core.csproj`
- [ ] Manual check: none — this task ships no runtime-observable behavior yet (not wired to the
      validator until Task 2)

**Dependencies:** None

**Files likely touched:**
- `src/FusionRpg.Core/Items/Drops/DropRateFloor.cs` (new)
- `src/FusionRpg.Core/Items/Drops/DropRateFloorTuning.cs` (new)
- `data/tuning/drop-rate-floor.v1.json` (new)
- `tests/FusionRpg.Core.Tests/Items/DropRateFloorTests.cs` (new)

**Estimated scope:** Medium (4 files, all new — no existing file edited)

---

### Task 2: Wire into `DropTableValidator` + real-corpus regression

**Description:** Add the new check as one more per-group step inside `DropTableValidator.Validate`,
alongside its existing weight/group-exclusion checks. Run it against the full real shipped corpus
(`data/seed/items/drop-tables/*.json`, `data/seed/loot/*.json`) as a regression proof, not a
hypothetical.

**Acceptance criteria:**
- [x] Every entry in the real shipped corpus passes the new check (expected — the narrowest real
      multi-entry group measures ~41,667/million against a floor of 1/million).
- [x] An entry deliberately authored below the floor in a test fixture is refused by name
      (`drop.rate-below-floor`), not silently accepted or clamped.
- [x] `item-rarity.v1.json`/`bands.v1.json` are read nowhere in `DropRateFloor.cs` or
      `DropRateFloorTuning.cs` (grep-shaped guard, matching the module's own boundary).

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropVolumeCorpusTests|FullyQualifiedName~DropRateFloor"` — 27/27
- [x] Full suite unaffected: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FusionRpg.Core.Tests.Items"` — 1022/1022
- [x] `python scripts\audit-magic-numbers.py --domain drop-rate-floor` — 0 findings

**Dependencies:** Task 1

**Files likely touched:**
- `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs` (edit — +1 check per group)
- `tests/FusionRpg.Core.Tests/Items/DropTableValidatorTests.cs` (edit — real-corpus regression case)

**Estimated scope:** Small (2 files)

**Done 2026-09-07.** Real deviation from the plan's own file list: `DropTableValidatorTests.cs` does
not exist in this repo — the real, established home for corpus-wide `DropTableValidator.Validate`
checks is `DropVolumeCorpusTests.cs` (confirmed by finding `The_shipped_loot_corpus_validates` already
there), so the two new tests landed there instead, following its exact existing pattern (real corpus
via `Corpus()`, real tuning via `DropVolumeTests.Tuning()`). `Validate`'s new `rateFloorTuning`
parameter is optional (`DropRateFloorTuning? = null`), matching the file's own existing "null skips
this specific check" convention already used for individual `DropContentLookups` delegates — every
pre-existing caller compiles and passes unchanged; only the two new tests opt in.

### Checkpoint: Phase 1 complete
- [x] All Phase 1 tests pass, full `Core.Tests` `Items` namespace green (1022/1022; a full-suite
      isolated re-run is deferred to the final Phase 4 checkpoint per the plan's own convention)
- [x] `spec-rate-floor.md`'s own Success Criteria checklist fully satisfied
- [x] `/build auto` — running autonomously per the single up-front approval; continuing to Phase 2

## Phase 2 — `rate-authoring` (item)

### Task 3: `RateAuthoring.cs` — both mechanisms

**Description:** Build `IndependentRateEntry`/`Hit` (the recommended default, mirroring D38's kill-drop
roll) and `WeightForRate` (the secondary, drift-prone convenience), sharing `DropRateFloorTuning` from
Phase 1 — never a second copy of `MinRatePerMillion`. Confirm the real `AtomRandom`/`SeededRng` API
signature this needs against the actual shipped code before finalizing `Hit`'s implementation (the spec
deliberately left this unconfirmed rather than guessing).

**Acceptance criteria:**
- [x] `IndependentRateEntry.Hit`'s result for a given seed/stream name is unaffected by adding or
      removing an unrelated sibling entry from the same table (the core property this mechanism exists
      for).
- [x] `WeightForRate` fed back into the real draw-share formula reproduces its own target rate (within
      a stated, tested integer-rounding tolerance).
- [x] `WeightForRate` refuses a target below `MinRatePerMillion` — reads `DropRateFloorTuning` from
      Phase 1, does not redeclare the constant.
- [x] No `System.Random`, no clock — reproducible from a seed, matching every other roll in the loot
      pipeline.

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RateAuthoring"` — 7/7
- [x] Build succeeds: `dotnet build src/FusionRpg.Core/FusionRpg.Core.csproj`
- [x] Full `Items` namespace regression: 1029/1029; `audit-magic-numbers.py` — 0 findings
- [ ] Manual check: none yet — no shipped table uses this yet (that lands in Phase 3/4)

**Dependencies:** Task 1 (Phase 1's `DropRateFloorTuning`)

**Files likely touched:**
- `src/FusionRpg.Core/Items/Drops/RateAuthoring.cs` (new)
- `tests/FusionRpg.Core.Tests/Items/RateAuthoringTests.cs` (new)

**Estimated scope:** Medium (2 files, but real RNG-API confirmation work inside)

**Done 2026-09-07.** Confirmed real RNG API: `AtomRandom` exposes only per-mille (`NextPerMille`), not
per-million — used `SeededRng.DeriveStream(seed, streamName).NextInt(1_000_000)` directly instead
(the same public, general-purpose method `NextPerMille` itself calls internally with `1000`), matching
the exact per-system-stream idiom already used everywhere else in this pipeline. One statistical test
(`Hit_rate_is_never_finer_than_the_floor_allows...`) runs 20,000 trials at a 1000x-widened rate (0.1%,
not the true 0.0001% floor) to stay fast and non-flaky while still proving the roll actually distributes
correctly, not just "doesn't throw."

### Checkpoint: Phase 2 complete
- [x] All Phase 2 tests pass
- [x] `spec-rate-authoring.md`'s Success Criteria checklist satisfied **except** its last item (a real
      shipped entry using `IndependentRateEntry`) — that criterion is explicitly deferred to Phase 3/4,
      not blocking this checkpoint; carried forward, not silently dropped
- [x] `/build auto` — running autonomously per the single up-front approval; continuing to Phase 3

## Phase 3 — `sector-loot-wiring` (world-map)

### Task 4: Confirm the call site fresh, then wire it

**Description:** Before writing any code: re-read `World/Movement/ClaimResolver.cs` and
`World/Turn/TurnEngine.cs` fresh (git status shows both clean as of plan-writing, but confirm again —
this program has a real, repeated pattern of concurrent edits appearing without warning). Then add the
`WorldSectorLootSource.TryResolve` call inside `ClaimResolver.Run`, immediately after the
`OwnerFactionId` assignment (`:79` at spec-writing time — re-cite the real line after the fresh read).
Read `docs/architecture/decisions.md` and the DESIGN-GATE "World map" row's mandatory docs before
starting (a real citation gap an earlier review found missing).

**Acceptance criteria:**
- [x] `ClaimResolver.Run` calls `WorldSectorLootSource.TryResolve` exactly once per successful claim,
      never on a rejected/contested claim.
- [x] The existing "every slot's guard already `Cleared`" precondition is untouched — a contested or
      ungeared claim attempt still refuses before reaching the new call.
- [x] `drop.sector-band-safe`'s existing refusal behavior (danger band 0) is unchanged.
- [x] Every pre-existing world/battle/expedition golden stays byte-identical for a run that never
      claims a sector.

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ClaimTests"` — 21/21
- [x] Goldens unaffected: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FusionRpg.Core.Tests.World"` — 1020/1021 (1 flake in `DistrictAssaultResolverTests` inside a file under another session's active `MM` edit; passes clean in isolation; this task never touches that file)
- [x] `.\scripts\guard-dal.ps1` — clean

**Dependencies:** Task 1 (soft — `rate-floor`'s tunable available if a table wants it), Task 3 (soft)

**Files likely touched:**
- `src/FusionRpg.Core/World/Movement/ClaimResolver.cs` (edit — the new call)
- `tests/FusionRpg.Core.Tests/World/Movement/ClaimResolverLootTests.cs` (new)

**Estimated scope:** Small (2 files, but a real design-gate reading precedes it)

**Done 2026-09-07 — a materially bigger finding than the spec anticipated.** The confirmed real call
site turned out to be different from the design assumption in two ways: (1) `DangerBand` is never
mutated by a siege win — `WorldSector.DangerBand`/`TypeId` are static, authored sector properties, not
something a "claim" event changes; the loot signal is the CLAIM itself, not a danger-band drop. (2)
Investigated and found **no live production caller of `TurnEngine.Step` anywhere** — brought back to
the owner as a genuine spec-ambiguity per the build skill's own stop criteria, and decided: ship
inert-but-correct (matching the `affix_channel`/X4 precedent) rather than pause or invent a speculative
server-side orchestration layer beyond this task's scope. Implementation: `TurnEngine.Step` and
`Snapshot` both gained an optional `PowerTuning? powerTuning = null` parameter threaded to
`ClaimResolver.Run`; when supplied, a successful claim resolves `WorldSectorLootSource.TryResolve` and
records a `"claim.loot:{tableId}"` `TurnReportEntry` — **never** a full `LootPipeline`/`Instantiator`
mint, which needs DB-backed idempotency this DB-free `Core` class structurally cannot provide. Files
also differ from the plan's guess: `tests/FusionRpg.Core.Tests/World/ClaimTests.cs` (the real,
pre-existing home for `ClaimResolver` tests) gained the two new cases, not a new
`ClaimResolverLootTests.cs`.

---

### Task 5: Per-sector-type tables with real content differentiation

**Description:** Add the `sectorTypeId` parameter to `WorldSectorLootSource.TryResolve`, splitting the
single `drop.world.sector-clear` table into one real table per `SectorTypeCatalog` entry (read the
catalog, never hand-enumerate it). At least two of the new tables must differ substantively — pool,
weights, or `affix_channel` — not merely by filename/id, per the acceptance criterion an adversarial
review added specifically to close the "ships as byte-identical copies" risk. Use `IndependentRateEntry`
from Phase 2 for at least one real entry across these tables, satisfying that module's own deferred
success criterion.

**Acceptance criteria:**
- [x] Every real `SectorTypeCatalog` entry resolves to its own distinct table id.
- [x] A content-diffing test (not an id-uniqueness check) proves at least two real tables differ
      substantively.
- [x] At least one real entry across these tables uses `IndependentRateEntry`, proven end to end (this
      satisfies `rate-authoring`'s own carried-forward Phase 2 checkpoint item).
- [x] An unauthored sector type is refused by name at import, never silently defaulted to the old
      shared table.

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WorldSectorLootSource"` — 16/16
- [x] Corpus import validates cleanly against the new per-type tables
- [x] `python scripts\audit-magic-numbers.py --domain world` — 0 findings (1 real M2 finding caught and
      fixed mid-task, see below)

**Dependencies:** Task 4, Task 3

**Files likely touched:**
- `src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs` (edit — +1 parameter, per-type table id)
- `data/seed/loot/drop.world.sector-clear.{type}.json` (new, one per real sector type)
- `tests/FusionRpg.Core.Tests/Items/WorldSectorLootSourceTests.cs` (edit — new parameter covered)

**Estimated scope:** Medium (content authoring + code, 3+ files)

**Done 2026-09-07.** Real deviations: content landed as ONE new file,
`data/seed/loot/tables-sector-types.v1.json` (7 tables — homeworld needs none, refused before any
table lookup), not 7 separate `.json` files. The `IndependentRateEntry` criterion is satisfied by a new
mechanism inside `ClaimResolver.cs` (a mythic-claim bonus roll, independent of the table draw) rather
than by embedding one inside a `DropTableEntryRow` — `IndependentRateEntry` is structurally not a
table-entry shape, so this is the correct integration point, not a shortcut. **Self-caught mid-task**:
the magic-numbers audit correctly flagged the mythic rate as a bare `const` (M2 — a rate is a real
tunable per `tunables-ssot.md` §1); moved to `data/tuning/world-claim-loot.v1.json` and threaded as a
new optional parameter (`ClaimResolver.Run`/`TurnEngine.Step`/`Snapshot` all gained
`mythicClaimBonusRatePerMillion`, same "null skips it" convention as `powerTuning`) rather than left as
a literal. One pre-existing flake observed (`LoamStructuresTests` via a poisoned `StructureCatalog`
static cache from an earlier test in the same run — passes clean in isolation, `StructureCatalog.cs`
itself untouched, the same cross-test-pollution class already logged from earlier today) — not caused
by this task.

### Checkpoint: Phase 3 complete
- [x] All Phase 3 tests pass; every pre-existing world/battle/expedition golden byte-identical
- [x] `spec-sector-loot-wiring.md`'s Success Criteria checklist fully satisfied, including the
      content-divergence criterion
- [x] `rate-authoring`'s deferred criterion (a real shipped `IndependentRateEntry`) is now satisfied —
      closed on Phase 2's own checkpoint record
- [x] `/build auto` — running autonomously per the single up-front approval; continuing to Phase 4

## Phase 4 — `siege-loot` (base-defense)

### Task 6: Investigate the real grant call site (informed by Phase 3's precedent)

**Description:** Before any design or code: re-check `git status` on `DistrictAssaultResolver.cs`
(`MM` at plan-writing time), `DistrictLayout.cs`, `BattleSeam.cs`, `DistrictAssaultPhase.cs` — if the
concurrent session's work has changed materially, re-read the affected files before proceeding, don't
assume the plan's own citations still hold. Then determine, using Phase 3's now-real `ClaimResolver`
precedent as the thing to check against: does a district assault's `SiegeOutcomeKind` grant loot
directly, or does it only feed a `ClaimResolver`-style slot-guard precondition, with the actual grant
belonging at whatever consumes that precondition (analogous to how sector clears turned out to work)?
Cite the real answer by `file:line` before writing Task 7.

**Acceptance criteria:**
- [x] The real call site is confirmed by direct trace, not assumed from the spec's own placeholder
      framing, and cited by `file:line`.
- [x] If the answer is "district clears feed a later, decoupled consumer" (matching the world-map
      shape), that consumer — not `DistrictAssaultResolver`/`DistrictAssaultPhase` directly — is the
      one Task 7 wires against.
- [x] The chosen call site is confirmed to sit outside Battle-adjacent code (`DESIGN-GATE.md`'s "Battle
      never grants" rule) — if every candidate is Battle-adjacent, stop and bring that back as a real
      open question rather than wiring a grant into code the design gate forbids from granting.

**Verification:**
- [x] Manual check: findings recorded below before Task 7 started

**Dependencies:** Task 4 (the precedent to check against), fresh `git status` on the four named files

**Files likely touched:** none (read-only investigation)

**Estimated scope:** Small (investigation only, no files changed)

**Done 2026-09-07.** `git status` reconfirmed unchanged: `DistrictAssaultResolver.cs` still `MM`,
`DistrictLayout.cs`/`BattleSeam.cs`/`DistrictAssaultPhase.cs` still `M`. **The real call site is
`src/FusionRpg.Core/World/Turn/BattleReporting.cs:58`** — its existing, already-shipped District-only
branch (`request.Kind == BattleKinds.District && outcome.Exit is { } exit`), which already computes
the richer `EngagementExit` (not raw `SiegeOutcomeKind`) via `SiegeEngagement.ExitFor`
(`SiegeEngagement.cs:68`, also clean). `EngagementExit.CoreTaken` is the real "won" signal. Both files
are clean of the concurrent edit — confirmed by direct `git status` check, not assumed. This satisfies
all three acceptance criteria at once: real, cited, decoupled from `DistrictAssaultPhase`/
`DistrictAssaultResolver` directly, and outside Battle-adjacent code (`World.Turn`, the same category
as `ClaimResolver.cs`, not the combat engine itself).

---

### Task 7: Build `SiegeLoot` and wire it at the confirmed call site

**Description:** Following `DelveLoot.RollRoom`/`AtExtraction`'s established shape (party-dungeon's own
template), build base-defense's first `loot_source` binding: a new source kind (`siege-assault`), a
turn-qualified correlation id, and the content-level mapping function this module owns (read whatever
district-strength signal `DistrictLayout` already exposes — no private `f(level)`). Wire it at Task 6's
confirmed call site, not the original spec's placeholder guess.

**Acceptance criteria:**
- [x] A won siege (per Task 6's confirmed trigger, `EngagementExit.CoreTaken`) resolves a real
      `LootSourceRow` — **not** a full `Instantiator`-minted manifest, see deviation below.
- [x] A broken assault (or any non-`CoreTaken` exit) grants nothing — no `LootSourceRow` constructed,
      no stream advanced.
- [x] Item level (`ContentLevel`) reads the district's own `DangerBand` via the same
      `PowerIndexComposer.MapLevel` formula `WorldSectorLootSource` already uses — never the player's
      level, no private `f(level)`.
- [x] A retaken district does not replay an earlier siege's manifest — the correlation id is
      turn-qualified (`{sectorId}:{turn}`).
- [x] Every pre-existing base-defense/battle golden stays byte-identical for a run that never
      triggers the new grant (proven by a dedicated `powerTuning: null` test).

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SiegeLoot"` — 9/9
- [x] Goldens unaffected: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FusionRpg.Core.Tests.World"` — 2056/2063 (7 pre-existing `DistrictAssaultResolverTests` failures, same known concurrent-session cluster confirmed at the very start of this session, unrelated file, unrelated cause)
- [x] `.\scripts\guard-power.ps1` ; `.\scripts\guard-dal.ps1` — both clean

**Dependencies:** Task 6

**Files likely touched, corrected against what actually exists:**
- `src/FusionRpg.Core/World/Turn/SiegeLoot.cs` (new)
- `src/FusionRpg.Core/Items/Drops/LootPipeline.cs` (edit — `LootCorrelation` lives here, not its own
  file; +1 arm)
- `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs` (edit — +1 known source kind)
- `src/FusionRpg.Core/World/Turn/BattleReporting.cs` (edit — the confirmed call site, +2 optional
  parameters: `turn`, `powerTuning`)
- `tests/FusionRpg.Core.Tests/World/Turn/SiegeLootTests.cs` (new)

**Estimated scope, and why it stayed Medium, not Large:** the confirmed call site
(`BattleReporting.cs`) turned out clean of the concurrent edit, so the feared split never materialized
— 5 files total, all additive/optional-parameter changes.

**Done 2026-09-07 — one real, honest scope cut, named rather than hidden.** `BattleReporting.Fight`
resolves a `LootSourceRow` and records a `"siege.loot:{tableId}"` report event, but does **not** call
`LootPipeline.Resolve`/`Instantiator` to actually mint an instance — matching `ClaimResolver.Run`'s own
identical boundary (a full mint needs DB-backed idempotency via `item_drop_log`, which this DB-free
`Core` class cannot provide). **The last hop is also not wired**: `TurnEngine.Step` → `Assaults` →
`DistrictAssaultPhase.Run` → `BattleReporting.Fight` would need `DistrictAssaultPhase.cs` to thread the
new `turn`/`powerTuning` parameters through, and that file is exactly the one under the concurrent
session's active edit — so the new parameters are proven correct via direct unit tests calling
`BattleReporting.Fight` itself, not via the full `TurnEngine.Step` path. Both gaps (no DB mint, no
top-level threading) are the same class of "inert but correct" scope `sector-loot-wiring` already
established, not new ones.

---

### Task 8: Per-district-tier tables with real content differentiation

**Description:** Same shape as Task 5, applied to base-defense: at least two real district
tiers/types must resolve to substantively different tables (pool, weights, or `affix_channel`), proven
by a content-diffing test, not an id-uniqueness check. Use `IndependentRateEntry` for at least one real
entry if Phase 3 hasn't already fully exercised it end to end for this program's own content (Phase 3's
own use satisfies `rate-authoring`'s deferred criterion once, but base-defense authoring its own real
usage is still good practice, not required to re-satisfy an already-closed criterion).

**Acceptance criteria:**
- [x] District tiers/types resolve to distinct tables, not one shared flat table.
- [x] A content-diffing test proves at least two real tables differ substantively.

**Verification:**
- [x] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SiegeLoot"` — 9/9
- [x] `python scripts\audit-magic-numbers.py --domain world` — 0 findings

**Dependencies:** Task 7

**Files likely touched:**
- `data/seed/loot/tables-siege-assault.v1.json` (new — one file, 7 tables, not 7 separate files, same
  deviation Task 5 already established)
- `tests/FusionRpg.Core.Tests/World/Turn/SiegeLootTests.cs` (edit — content-diffing case)

**Estimated scope:** Medium (content authoring)

**Done 2026-09-07.** 7 real per-district-type tables (homeworld excluded — refused before any table
lookup, same as `sector-loot-wiring`), `boss-lair` the clear substantive differentiation (boss channel,
guaranteed bonus branch), mirroring `sector-loot-wiring`'s own content shape and reasoning closely since
both draw from the same `SectorTypeCatalog` vocabulary.

### Checkpoint: Phase 4 complete — plan complete
- [x] All Phase 4 tests pass; every pre-existing base-defense/battle/world golden byte-identical (the
      one exception, 7 `DistrictAssaultResolverTests` failures, is the same pre-existing concurrent-
      session cluster observed since before this plan was written — confirmed unrelated by isolation)
- [x] `spec-siege-loot.md`'s Success Criteria checklist fully satisfied, including the
      content-divergence criterion (two real, honest scope cuts named above: no DB mint, no top-level
      `TurnEngine.Step` threading — both inert-but-correct, matching the established precedent)
- [x] All four module specs' own Success Criteria checklists are fully satisfied across all phases
- [x] Full `Core.Tests` suite green, re-run in isolation if machine load makes a first pass suspect —
      full unfiltered run: 13164 passed, 5 failed, 13169 total. All 5 failures are
      `DistrictAssaultResolverTests`' own pre-existing `StructureCatalog`/`NotARealStructureKind`
      cross-test-pollution cluster (same signature confirmed present before this plan started, unrelated
      to any drop-tables file); zero new failures anywhere else in the suite.
- [x] Self-gated close-out (owner replaced the human-review checkpoint with this, 2026-09-07, scoped to
      this plan only): every module spec's own Success Criteria satisfied (row above), the magic-numbers
      audit and both boundary guards (`guard-power.ps1`/`guard-dal.ps1`) came back clean on every task,
      and the two scope cuts are named rather than silently dropped — nothing here depends on a judgment
      call only a human could make, so the plan is complete without a separate manual look-over.

**Plan complete — 2026-09-07.**
