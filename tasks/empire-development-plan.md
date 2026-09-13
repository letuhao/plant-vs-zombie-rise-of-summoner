# Implementation Plan: empire-development

**Program:** `empire-development` (umbrella over `scoped-inventory-hierarchy` + `loam-relics-and-wonders`).
**Specs:** all 8 module specs + both sub-maps + the umbrella map are done and strengthened as of
2026-09-13 — see `docs/architecture/empire-development-map.md`, `docs/architecture/scoped-inventory-hierarchy-map.md`,
`docs/architecture/loam-relics-and-wonders-map.md`, and the 8 `docs/architecture/<sub-program>/spec-*.md`
files they index. This plan does not re-derive design; every task below cites the spec section that
already made the call, and points back to it for full reasoning, code snippets, and Design-gate
citations.
**Task list:** `tasks/empire-development-todo.md` (companion file — checkboxes only, this file carries
the reasoning).

## Overview

Two sub-programs, one dependency edge: `scoped-inventory-hierarchy` (five inventory scopes — legion
cargo, sector storage, cross-scope transfer, legion-death cargo fate) is a hard prerequisite for
`loam-relics-and-wonders`' `wonder-build-flow` module (spending a relic requires a real place to spend
it from). The other three `loam-relics-and-wonders` modules (relics as items, Wonders as structures,
the empire-wide production effect) have no dependency on `scoped-inventory-hierarchy` and build in
parallel with it.

One external prerequisite outside this umbrella: `cargo-fate` (scoped-inventory-hierarchy module 4)
needs `deployment-hierarchy`'s `corpse-cache`/`cache-decay-void`/`cache-field-access` trio to actually
exist in code — that program's specs were amended this session (new `place_kind='world_lane'`, reused
`place_kind='world_sector'`, new `source_kind='legion_death'`) to accept exactly the shape `cargo-fate`
needs. Confirmed this session: none of `deployment-hierarchy`'s modules are built yet either (no
`RpgStore.CorpseCache.cs` exists). Phase 0 below builds the minimum slice of that sibling program this
plan actually needs — see "Gates vs. checkpoints" note in Phase 0 for why this is a real dependency, not
a manufactured one, and why it's small enough to absorb here rather than blocking on a separate plan.

## Architecture decisions this plan builds against (inherited, not re-decided here)

- **One ownership root, scope = overlay.** `rpg_item`/`rpg_item_stock` never gain a second owner
  column; every new scope (legion cargo, sector storage) is a reachability+capacity overlay table with
  no `player_id`/owner column of its own where the design calls for it (`decisions.md` "Scoped
  inventory hierarchy SSOT").
- **Move, never copy**, single transaction, for every scope-to-scope transfer and every death/capture
  event — proven precedent `corpse-cache` already established, reused verbatim by `cargo-fate`.
- **A Wonder is an orthogonal `StructureDef` facet, not a new `StructureKind` value** (`spec-wonder-structure.md`
  §Design 1) — `StructureKind` stays at 5 members.
- **A relic is a new sibling item kind (`DropEntryKind.Relic`), never a `unique` `KindSpec` variant**
  (`spec-relic-item-kind.md` §The schema decision) — `unique`'s 144-row corpus and validator suite are
  untouched by this whole program.
- **Empire-scope Wonder production is SUM, offset by a real per-turn upkeep proportional to the
  benefit** (`spec-wonder-effect-empire.md` §Design 3, §Design 6 — the upkeep term was added this
  session after an adversarial economy audit found the un-offset design risked recreating the exact
  "decay has no effect" failure mode the owner rejected when withdrawing the Warden mechanic).
- **`TurnEngine.Step` stays pure — every world-turn computation reads only already-loaded `WorldState`,
  never a live DB call mid-step.** Binding across every module that touches the turn engine.

## Task List

Tasks are grouped into phases matching the specs' own approved build waves. Within a phase, tasks with
no dependency on each other are parallelizable across agents/sessions (noted per task).

### Phase 0: External prerequisite — the `corpse-cache` trio (`deployment-hierarchy`)

**Why this is here and not a separate plan.** `cargo-fate` (Phase 2) cannot compile or be tested
without `rpg_corpse_cache`/`rpg_corpse_cache_item` existing, and this session amended that sibling
program's specs specifically to accept `cargo-fate`'s shape. This is a genuine code dependency, not an
approval gate (per the planning skill's "Gates vs. checkpoints" — check: is it irreversible? No, but it
is a real compile/runtime dependency, which behaves the same way a gate would if skipped: **it must be
built before Phase 2's `cargo-fate` task starts.** Named resolver: whoever picks up Phase 2. Stated
default: if `deployment-hierarchy`'s own plan has already built this trio by the time Phase 2 starts,
skip Phase 0 entirely and verify with `grep -rn "rpg_corpse_cache" src/FusionRpg.Data` before starting
`cargo-fate`; if not, build it here — it is small, already fully spec'd, and this program is the one
that currently needs it. **Note on Task 0.3b specifically:** it was added late (this session, to close
a real spec gap — see Task 0.3b's own description), so a `deployment-hierarchy` plan written before this
gap was found will not include it even if it already built the rest of the trio — verify
`ClaimCorpseCacheIntoCargoUnlocked` separately with `grep -rn "ClaimCorpseCacheIntoCargoUnlocked" src/FusionRpg.Data`
rather than assuming the trio-skip check above also covers it.

#### Task 0.1: `corpse-cache` — schema + death/wipe move (deployment-hierarchy module 3)

**Description:** Build `rpg_corpse_cache`/`rpg_corpse_cache_item` (two tables, header+contents shape)
and the move-on-`Retired`/move-on-wipe functions, per `docs/architecture/deployment-hierarchy/spec-corpse-cache.md`
§Design 1-3 — including this session's amendment: `place_kind` accepts `'lawn' | 'delve_room' | 'siege'
| 'world_sector' | 'world_lane'`, `source_kind` accepts `'death' | 'wipe' | 'legion_death'`.

**Acceptance criteria:**
- [ ] `rpg_corpse_cache`/`rpg_corpse_cache_item` exist with the exact column shape in §Design 1.
- [ ] `RetireUniqueActorUnlocked`'s both existing callers move the specimen's `rpg_item_assignment` rows
  into a cache in the same transaction; `rpg_player_item_assignment` is never read.
- [ ] `CloseDelve`'s wipe branch moves every party member's assignment + carry-in + haul into a cache,
  gated on the `loot-pack` §7 ask being accepted (spec §3 — verify this ask's status before starting;
  if still open, this sub-task is blocked and must be flagged, not silently skipped).
- [ ] **Core move mechanism vs. anti-fraud guarantee, distinguished explicitly** (`spec-corpse-cache.md`
  §Locked anchors, Structure table): the move-on-`Retired`/move-on-wipe mechanism itself (the two AC
  bullets above) is buildable now and has no external blocker. The **anti-fraud property** — that gear
  moved is provably the gear a specimen had at *deploy time*, never gear stripped mid-deployment to
  dodge the stake — is a second, separate guarantee that is **BLOCKED** on `SaveAssignment`/
  `RemoveAssignment` gaining a `Phase == Roster`-required gate (mirroring the expedition precedent at
  `RpgStore.Expeditions.cs:61-62`), an ask filed on the item program, not yet accepted. Ship the core
  mechanism; flag the anti-fraud guarantee as blocked-and-tracked, matching the same non-blocking
  treatment already given the `loot-pack` §7 ask (see Risks table).

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CorpseCache"`
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve.CloseDelve"`
- [ ] `.\scripts\guard-dal.ps1` and `.\scripts\guard-actor-hub.ps1` green.

**Dependencies:** None.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.CorpseCache.cs` (new),
`src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (where the blocked anti-fraud phase-gate lands, once the
item-program ask is accepted — `SaveAssignment`/`RemoveAssignment`, per `spec-corpse-cache.md`
§Locked anchors).

**Estimated scope:** M (3 files).

#### Task 0.2: `cache-decay-void` — the clock, amended for durable world-map places

**Description:** Build the decay clock and void transition per `docs/architecture/deployment-hierarchy/spec-cache-decay-void.md`,
including this session's V5 amendment: `world_sector`/`world_lane` caches start their decay clock
immediately but **never** flip `in_void` — unlike a lawn match or a closed delve room, a sector/lane is
a durable, revisitable place, so a cache there stays perpetually field-reachable rather than expiring.

**Acceptance criteria:**
- [ ] `TryStartDecayClockUnlocked` branches correctly on `place_kind` per the amended spec.
- [ ] A `world_sector`/`world_lane` cache never reaches `in_void=1` under any test scenario, including
  one that runs the decay clock far past the timeout that would void a `lawn`/`delve_room` cache.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheDecay"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 0.1.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.CacheDecay.cs` (new — schema,
`TryStartDecayClockUnlocked`, `TickCorpseCacheDecayForPlayerUnlocked`); `src/FusionRpg.Data/Sqlite/RpgStore.CorpseCache.cs`
(module 3's file — one new line calling `TryStartDecayClockUnlocked`); **`src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs`**
(one new line inside `CommitWorldTurn` calling `TickCorpseCacheDecayForPlayerUnlocked`, right before
its own `tx.Commit()` — the decay-clock's per-turn tick hook, per `spec-cache-decay-void.md` §Design 3/
§Structure).

**Estimated scope:** S-M.

#### Task 0.3: `cache-field-access` — reachability, amended for `world_sector`/`world_lane`

**Description:** Build field-access reachability per `docs/architecture/deployment-hierarchy/spec-cache-field-access.md`
§1a: a querying legion's live `WorldEntity.AtSectorId` (for `world_sector`) or `.OnLaneId` (for
`world_lane`) must match the cache's `place_ref` for it to be reachable.

**Acceptance criteria:**
- [ ] A legion standing at a sector can list/claim a `world_sector` cache pinned there; one standing
  elsewhere cannot.
- [ ] A legion on a lane can list/claim a `world_lane` cache pinned there; one on a different lane or at
  a sector cannot.
- [ ] The reachability read (§1/§1a) is correct standalone — the actual write into a legion's cargo
  overlay is a separate, sequenced-after task (0.3b, below): a genuine SPEC gap this session's audit
  found (`cache-field-access.md` §1a originally named `cargo-fate` as the write's owner, but
  `spec-cargo-fate.md`'s own Interface table shows nothing depends on it — it was never going to build
  the verb). The spec itself is now fixed (`spec-cache-field-access.md` §2a, amended 2026-09-13, later
  session) to own the write directly; this task builds only the reachability half.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheFieldAccess"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 0.1.

**Estimated scope:** S-M.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.CacheFieldAccess.cs` (new — schema,
`ListClaimableCachesUnlocked`, `ClaimCorpseCacheUnlocked`; per `spec-cache-field-access.md` §Structure).

#### Task 0.3b: `cache-field-access` — claim into legion cargo (world-map claim write, fixes a real spec gap)

**Description:** Build `ClaimCorpseCacheIntoCargoUnlocked`, per `docs/architecture/deployment-hierarchy/spec-cache-field-access.md`
§2a (added this session to close a real gap this plan's own adversarial audit found: §1a originally
named `scoped-inventory-hierarchy/cargo-fate` as the owner of this write, but `spec-cargo-fate.md`'s own
"Interface exposed to dependents" table states plainly that nothing in either program depends on
`cargo-fate` — it was never actually going to build this verb). The function reads a `world_sector`/
`world_lane` corpse-cache row the caller is proven reachable to (reusing §1a's own reachability query),
inserts each claimed row into the claiming legion's `rpg_world_entity_cargo` subject to `legion-cargo`'s
own weight/slot capacity gates (reusing `WeightCapacityUnlocked`/`SlotCapacityUnlocked`, refusing a
single over-capacity row rather than the whole claim), and deletes the claimed rows from the cache in
the same transaction (move-never-copy).

**Acceptance criteria:**
- [ ] A legion with headroom for every row in a reachable `world_sector`/`world_lane` cache claims all
  of them; the cache empties and `rpg_world_entity_cargo` gains an equal number of rows, in one
  transaction.
- [ ] A legion with room for only some rows claims exactly those, in `seq` order; the rest stay in
  `rpg_corpse_cache_item`, unclaimed — never a whole-claim refusal, never a partial-row insert.
- [ ] The reachability check re-runs at claim time, not just at the earlier list time — a stale/forged
  `cacheId` from a legion no longer at the matching sector/lane refuses `cache.unreachable`.
- [ ] Replaying an identical `(cacheId, worldId, entityId, correlationId)` claim is a no-op the second
  time (same replay-safety table shape as the delve claim).
- [ ] `legion-cargo`'s own `rpg_world_entity_cargo` schema and capacity functions are reused, never
  redefined — this task adds no second capacity formula.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheFieldAccess"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 0.3 (extends the same module's reachability read), Task 1.1 (`legion-cargo` —
`rpg_world_entity_cargo`'s schema and `WeightCapacityUnlocked`/`SlotCapacityUnlocked`, a genuine new
cross-program dependency this task introduces, named plainly in the spec's own header/checklist).

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.CacheFieldAccess.cs` (same file as Task
0.3 — `ClaimCorpseCacheIntoCargoUnlocked`, per spec §2a), reading (never modifying)
`src/FusionRpg.Data/Sqlite/RpgStore.LegionCargo.cs`.

**Estimated scope:** S-M.

### Checkpoint: Phase 0 complete

- [ ] All of Phase 0's tests pass in one combined run.
- [ ] `guard-dal.ps1`/`guard-actor-hub.ps1` green.
- [ ] `grep -rn "rpg_corpse_cache" src/FusionRpg.Data` shows the trio wired end-to-end.
- [ ] Review with human before Phase 2's `cargo-fate` task starts (Phase 1 does not depend on this and
  may proceed in parallel with Phase 0).

---

### Phase 1: Wave 1 — four independent modules, fully parallelizable

None of the four tasks below depend on each other or on Phase 0. Assign to separate
agents/sessions freely.

#### Task 1.1: `legion-cargo` — slot+weight cargo overlay

**Description:** Build `rpg_world_entity_cargo` and `LoadCargoUnlocked`/`UnloadCargoUnlocked`/
`TransferCargoUnlocked`, per `docs/architecture/scoped-inventory-hierarchy/spec-legion-cargo.md`.
Capacity = **every** member regardless of role (`memberCount × tuning.CargoWeightPerUnit`/
`CargoSlotsPerUnit`) — a deliberate divergence from `LegionSupply`'s Bearer-only precedent, confirmed
by the owner during `/spec`.

**Acceptance criteria:**
- [ ] `rpg_world_entity_cargo` exists with the exact column shape (`world_id, entity_id, seq, kind,
  instance_id, container_id, qty, weight_each`).
- [ ] `LoadCargoUnlocked` refuses `cargo.not-owned`/`cargo.over-weight`/`cargo.no-slots` correctly,
  before any write.
- [ ] `weight_each` is captured once at load time from the item's derived weight field, never
  re-resolved on read (§Design 4).
- [ ] Capacity uses **all** members, not Bearer-filtered — a regression test proves this explicitly.
- [ ] **`TransferCargoUnlocked` (legion-to-legion), covered explicitly — this is half the module's own
  stated objective, not an afterthought** (`spec-legion-cargo.md` §Design 5, §Testing strategy, §Success
  criteria #2): a transfer either fully succeeds (source loses the row, destination gains it) or fully
  fails (both unchanged) under a forced-failure test — no interrupted-transaction state ever
  observable; a transfer where the two legions' `OwnerFactionId`s resolve to different `player_id`s
  refuses `cargo.cross-empire`; the destination's own capacity gate (`cargo.over-weight`/
  `cargo.no-slots`) is checked before any write, same as `LoadCargoUnlocked`.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~LegionCargo"`
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~LegionCargo"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** None.

**Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.LegionCargo.cs` (new), tests.

**Estimated scope:** M (2-3 files).

#### Task 1.2a: `sector-storage` — `StructureKind.ItemStorage` + capacity axis

**Description:** Add the 6th `StructureKind` value (`ItemStorage`) and `SectorItemCapacity.EffectiveCapacity`,
mirroring `LoamPhases.EffectiveCapacity`'s exact shape, per `docs/architecture/scoped-inventory-hierarchy/spec-sector-storage.md`
§Design 1-3.

**Acceptance criteria:**
- [ ] `StructureKind` has exactly 6 members, `ItemStorage` doc-commented like its siblings.
- [ ] `StructureDef.ItemStorageCapacityBonus: long` exists, optional at parse time (25 existing rows
  load byte-identical).
- [ ] `SectorItemCapacity.EffectiveCapacity` sums correctly across N active `ItemStorage` structures,
  provably independent of `LoamPhases.EffectiveCapacity` (a shared regression test proves both can move
  independently without affecting the other).

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StructureCatalog|SectorItemCapacity"`
- [ ] All 25 existing structure seed rows unchanged (`StructureCatalogImportTests` — see Risks table for
  its own pre-existing pinned-count debt).

**Dependencies:** None.

**Estimated scope:** M (2-3 files).

#### Task 1.2b: `sector-storage` — table + capture hook

**Description:** Build `rpg_world_sector_storage` (no `weight_each`, no owner column) and the
sector-capture hook inside `DiffSectors`'s existing per-sector loop, per §Design 4-5. Confirms the
capture-transfer no-op: because the table has no owner column, reachability flips automatically the
instant `ClaimResolver` reassigns `sector.OwnerFactionId` — no migration code needed.

**Acceptance criteria:**
- [ ] `rpg_world_sector_storage` exists with the exact column shape (no `weight_each`, no owner column).
- [ ] A sector capture test: items already in storage at a captured sector are reachable by the new
  owner and unreachable by the old owner, with **zero** rows touched by the capture itself.
- [ ] **The capture hook fires inside the turn-commit transaction, not after it** (`spec-sector-storage.md`
  §Testing strategy): a forced-failure test, mirroring `legion-cargo`'s own atomicity test, proves a
  crash immediately after `DiffSectors`'s sector-owner write but before `tx.Commit()` leaves the
  sector's `OwnerFactionId` and its storage rows' observable reachability in the **same** pre-capture
  state — never one flipped and the other not.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~SectorStorage"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 1.2a (needs `ItemStorage` kind to exist first).

**Estimated scope:** M (2-3 files).

#### Task 1.3a: `relic-item-kind` — KindSpec + enum + mint arm

**Description:** Register the new `relic` `KindSpec` (seedsmith + C# mirror), `DropEntryKind.Relic`
(10th member), `ContainerKind.Relic` (12th member), and the `MintRelic` arm that persists to `rpg_item`
only, never `item_generation` — per `docs/architecture/loam-relics-and-wonders/spec-relic-item-kind.md`
§Design 1-3.

**Acceptance criteria:**
- [ ] A relic anchor authors and loads without `frame`/`baseType`/`counterPressure`/`powerAxis`; the
  existing `unique` validator suite runs unmodified and green.
- [ ] **`a_relic_kindspec_has_no_reference_fields`** (`spec-relic-item-kind.md` §Testing strategy): the
  `relic` `KindSpec`'s own `refs` set is `{}` — a relic never points at a base type or role.
- [ ] A relic mints to a durable `rpg_item` row with a real `instance_id`; `item_generation` gains zero
  rows from that mint.
- [ ] **`mint_relic_produces_no_base_type_role_or_frame`** (`spec-relic-item-kind.md` §Testing
  strategy): the **persisted `rpg_item` row itself** carries none of the three — not merely that
  `item_generation` gains no row (the bullet above), but that there is nowhere on the `rpg_item` row to
  put them either.
- [ ] `DropTableDraw.UnavailableKinds` carries `Relic` until this task ships, mirroring `Unique`'s own
  pre-`MintUnique` history.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Relic"`
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Relic"`
- [ ] `python -m seedsmith check data/seed/items --adapter items` (once relic anchors exist, Task 1.3c).
- [ ] `.\scripts\guard-dal.ps1` green; existing 144-row unique corpus tests green with zero edits to
  unique's own files.

**Dependencies:** None.

**Files likely touched:** `tools/seedsmith/seedsmith/adapters/items/kinds.py`,
`tools/ItemSeedValidator/Registries/KindCatalog.cs`, `src/FusionRpg.Core/Items/Drops/DropTableModel.cs`,
`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs`, `src/FusionRpg.Core/Items/Drops/LootPipeline.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Loot.cs`.

**Estimated scope:** L (6 files) — **at the edge of this skill's size guideline; if it proves
unwieldy in one session, split along the Core/Data boundary** (enum+KindSpec+mint-signature in one
task, the actual `RpgStore.Loot.cs` host implementation in a second).

#### Task 1.3b: `relic-item-kind` — seedsmith `droptablegen` gains a `relic` `entryKind`

**Description:** The real, scoped generator sub-task this session's strengthen pass found: no existing
seedsmith command can append a new entry to an already-shipped `drop-table` row. Per §Design 4a: a 10th
`EntryKinds` member in `tools/ItemSeedValidator/Checks/DropTableCheck.cs`, a new `_relic_row` emit
function in `droptablegen/emit.py`, a `relic_refs`/`RELIC_SLOT_COUNT` slot in `brief.py`, a
`load_relic_ids()` reader in `tuning.py`, and a genuinely new **targeted append operation** that adds
one new `Relic`-kind group to an existing on-disk row through the same validate-then-`--write` path
every other kind uses.

**Acceptance criteria:**
- [ ] `DropTableCheck.cs`'s `EntryKinds` accepts `"relic"` and requires a resolvable `ref`.
- [ ] `_relic_row` raises `IllegalChoiceError` for a `ref` absent from the real relic corpus, mirroring
  `_insert_row`/`_consumable_row`.
- [ ] The append operation adds exactly one `Relic`-kind group to a named existing table id and leaves
  every other row/group in that file byte-identical.

**Verification:**
- [ ] Tests pass: `python -m pytest tools/seedsmith/tests/test_drop_tables_gen.py -q`

**Dependencies:** Task 1.3a (needs `DropEntryKind.Relic` to exist for cross-checking).

**Estimated scope:** M (4 files, Python-only).

#### Task 1.3c: `relic-item-kind` — content authoring (regeneration, never hand-edit)

**Description:** Author relic anchors and, via the Task 1.3b append operation, add one `Relic`-kind
entry to each of the seven real, wired `SourceKind` drop tables (`web-wave`/`expedition-tier`/
`world-sector`/`dungeon-room`/`dungeon-clear`/`dungeon-quest`/`siege-assault`). **Never a direct JSON
edit of an existing `_meta.model`-stamped file** — this is the exact hard-rule violation this session's
strengthen pass caught and fixed at the spec level; do not reintroduce it at build time.

**Acceptance criteria:**
- [ ] Every one of the seven `SourceKind`s resolves a drop table containing a `Relic` entry.
- [ ] `pvz-run` still refuses by name, unchanged, proving the refusal isn't accidentally bypassed by the
  new kind.
- [ ] Every touched `data/seed/items/drop-tables/*.json` file's diff is the output of the regeneration
  command, never a hand-typed edit — confirm via `git diff` that only the append operation's own
  machine-formatted output changed.

**Verification:**
- [ ] `python -m seedsmith items fill --kind drop-table --entry-kind relic --ref relic.<id> --drop-band
  <band> --write` (one per `SourceKind` table).
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Relic"` (the
  per-`SourceKind` resolvability tests from Task 1.3a's own suite, now exercised against real content).

**Dependencies:** Tasks 1.3a, 1.3b. **Open content call, not architecture:** the exact drop
weight/rate per table is a genuine balance decision, not pre-decided by any spec — pick a low default
matching `unique`'s own low-weight precedent (per `spec-relic-item-kind.md` §Tunables) and flag it for
a balance pass, don't block on it.

**Estimated scope:** S (content only, no code).

#### Task 1.4: `wonder-structure` — vocabulary + `StructureDef` facet + `Validate`

**Description:** Ship `WonderScope`/`WonderRarity`/`WonderEffectKind`/`WonderEffectDef` and the
`StructureDef.WonderScope?`/`.WonderRarity?`/`.WonderEffects` orthogonal facet fields, plus the
`Validate` refusals that keep `World`/`Multiverse` and `DefensePower`/`AuraGrant`/`EmpireBuff`
unreachable, per `docs/architecture/loam-relics-and-wonders/spec-wonder-structure.md` §Design 1-6.
**Note the `Validate` snippet in the spec was corrected this session** (originally cited invalid C#,
`World.Sector` instead of `WonderScope.Sector`) — implement against the corrected version.

**Acceptance criteria:**
- [ ] `StructureKind` unchanged at 5 members.
- [ ] A `Sector`-scope Wonder row loads through the existing `StructureCatalog.All` pipeline and is read
  by the existing, unmodified `LoamProduction.For` — zero engine change needed for this case.
- [ ] `Validate` refuses `World`/`Multiverse` and each of `DefensePower`/`AuraGrant`/`EmpireBuff`
  individually, each with a message naming the reserved member.
- [ ] `Validate` refuses a `WonderScope`/`WonderRarity` pairing mismatch (one set, the other null).
- [ ] `Validate` refuses a `WonderEffectDef.Scope` that disagrees with its own row's `WonderScope`.
- [ ] `Validate` refuses a duplicate `(Kind, Scope)` pair within one row's `WonderEffects`.
- [ ] `WonderPolicy.ExistenceCapFor(scope, Common)` returns `long.MaxValue` for both live scopes, never
  a finite number.
- [ ] `WonderPolicy.ExistenceCapFor(scope, Unique)` reads the tunable file's own number — changing the
  tuning file's value changes the returned cap with no code change, proving it is data, not a constant.
- [ ] `WonderPolicy.ExistenceCapFor` throws before `Configure` runs, matching `LoamPolicy`'s own "no
  built-in default" discipline.
- [ ] All 25 existing structure seed rows remain byte-identical.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderCatalog|StructureCatalogImportTests"`
- [ ] `.\scripts\guard-dal.ps1` green (no SQL touched by this task at all).

**Dependencies:** None.

**Files likely touched:** `src/FusionRpg.Core/World/StructureCatalog.cs`,
`src/FusionRpg.Core/World/StructureSeed/StructureCorpus.cs`, `src/FusionRpg.Core/World/WonderCatalog.cs`
(new), `src/FusionRpg.Core/World/Loam/WonderTuning.cs` (new), `data/tuning/loam-relics-wonders.v1.json`
(new).

**Estimated scope:** L (5 files) — **candidate for splitting** (vocabulary+fields in one task,
`WonderPolicy`+tuning file+tests in a second) if it doesn't fit one session.

### Checkpoint: Phase 1 complete

- [ ] All four modules' test suites pass independently.
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`, `.\scripts\guard-single-writer.ps1`
  all green.
- [ ] `python scripts/audit-overflow.py` and `python scripts/audit-magic-numbers.py` show no new
  critical findings introduced by this phase.
- [ ] Review with human before Phase 2. Phase 0 (if not already done in parallel) must also be complete
  before Phase 2's `cargo-fate` task specifically — the other three Phase 2 tasks do not wait on it.

---

### Phase 2: Wave 2 — four modules, each depends on one or two Phase 1 modules

#### Task 2.1: `cargo-transfer` — deposit/withdraw/legion-to-legion

**Description:** Connect `legion-cargo` and `sector-storage` via `DepositUnlocked`/`WithdrawUnlocked`
and a `SameFaction` helper (plain `FactionId` string equality, deliberately not resolving to
`player_id`), per `docs/architecture/scoped-inventory-hierarchy/spec-cargo-transfer.md`. **Note this
session's correction:** `WithdrawUnlocked`'s weight resolution was fixed — `rpg_world_sector_storage`
has no `weight_each` column (Task 1.2b's own design), so weight must be read fresh from the item's
derived weight field, the same mechanism `LoadCargoUnlocked` already uses, not a column read.

**Acceptance criteria:**
- [ ] `DepositUnlocked`/`WithdrawUnlocked` both refuse `cargo.cross-empire` correctly using
  `SameFaction`'s plain string-equality check.
- [ ] `WithdrawUnlocked` resolves weight fresh from the item's own derived field (regression test
  against the corrected design — reading a nonexistent `weight_each` column must fail loudly if
  reintroduced, not silently return zero).
- [ ] Never touches `rpg_item.player_id`/`disposition`.
- [ ] **Presence gate refuses:** a legion not at the sector refuses `cargo.not-present`, no write on
  either table (`spec-cargo-transfer.md` §Testing strategy).
- [ ] **Faction gate refuses against a same-turn ownership change:** a legion at the sector but
  belonging to a different faction than the sector's *current* owner refuses `cargo.wrong-faction`,
  tested against a sector whose owner just changed this same turn — proving the check reads live
  state, never a stale value.
- [ ] **Both capacity gates refuse independently:** a deposit into a full sector refuses
  `cargo.sector-full` without touching the legion's own cargo; a withdraw that would overweight the
  legion refuses `cargo.over-weight` without touching the sector's storage.
- [ ] **Atomicity under forced failure:** a crash between the delete and the insert (either direction)
  leaves the pre-transfer state fully intact on replay, mirroring both dependency modules' own
  atomicity tests; deposit is separately proven to move (never copy) by row count, not just absence of
  an error.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CargoTransfer"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Tasks 1.1, 1.2a, 1.2b.

**Estimated scope:** M (1-2 files).

#### Task 2.2: `cargo-fate` — legion-death cargo cache

**Description:** Hook `DiffEntities` (before its existing `DeleteMissing` call) to move a destroyed
legion's cargo into a `corpse-cache` row, per `docs/architecture/scoped-inventory-hierarchy/spec-cargo-fate.md`
§Design 1-2 (fully resolved this session — sector death uses `place_kind='world_sector'`, lane death
uses the new `place_kind='world_lane'`, both tagged `source_kind='legion_death'`).

**Acceptance criteria:**
- [ ] A destroyed legion's cargo is provably moved into a `corpse-cache` row **before** the entity row
  itself is deleted — the exact `ON DELETE CASCADE` race this session's audit found and ordered around.
- [ ] Sector-death and lane-death both produce a correctly-keyed cache row; a legion with no cargo
  produces no empty cache row.
- [ ] A legion with neither `AtSectorId` nor `OnLaneId` set is unreachable per `WorldState.cs`'s own
  invariant — no destroyed-outright fallback path exists or is needed.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CargoFate"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 1.1 (`legion-cargo` — the `rpg_world_entity_cargo` schema this module moves rows
out of), **Phase 0 complete** (external — `corpse-cache`'s `ResolveOrCreateCacheUnlocked` must exist).
**Corrected from the map's own wave-2-depends-on-wave-1 framing:** the capability map places this task
after `cargo-transfer` (Task 2.1), but `spec-cargo-fate.md`'s own design (§Design 1-2) never calls any
`cargo-transfer` function — its real code dependency is `legion-cargo`'s schema plus Phase 0, both
Wave-1/Phase-0 items. The wave-based map framing is coarser than the actual code dependency; noted here
so a reader does not wonder why the listed dependency changed from the map's own row.

**Estimated scope:** S-M (1-2 files).

#### Task 2.3a: `wonder-effect-empire` — faction-input plumbing + the SUM

**Description:** Add the `int scopeModifierMilli` parameter to `LoamProduction.For`, restructure
`LoamPhases.Production` into decrement-all → compute-per-faction-sum → compute-yield, and build
`WonderEmpireEffects.ComputeScopeModifierMilli`, per `docs/architecture/loam-relics-and-wonders/spec-wonder-effect-empire.md`
§Design 1-3.

**Acceptance criteria:**
- [ ] Zero-Wonder byte-identity: every existing Loam golden that authors no Empire-scope Wonder is
  unchanged.
- [ ] SUM, not max or replace: two built Empire-scope Wonders, 200 and 300 milli, produce exactly 1500.
- [ ] Same-pass activation: a Wonder finishing construction this exact Production phase already
  contributes to this same phase's sum.
- [ ] Lost-sector drop-out: losing the sector holding a faction's only Empire Wonder returns
  `ScopeModifierMilli` to 1000 the very next Production phase, no residue.
- [ ] **`checked`-overflow regression test, named explicitly** (`spec-wonder-effect-empire.md` §Testing
  strategy) — a synthetic scenario with a sum large enough to overflow the inherited `int`
  `ScopeModifierMilli` field throws `OverflowException` at the narrowing cast (`checked((int)(1000 +
  sum))`), never wraps to a negative or wrapped-around modifier.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderEmpireEffects|LoamPhasesTests|LoamProductionTests"`

**Dependencies:** Task 1.4.

**Estimated scope:** M (3 files: `LoamProduction.cs`, `LoamPhases.cs`, `WonderEffectEmpire.cs` new).

#### Task 2.3b: `wonder-effect-empire` — SQL persistence + three secondary call sites

**Description:** Close the `ScopeModifierMilli` persistence gap (new `scope_modifier_milli` column via
`EnsureColumn`, wired into `WriteWorldGraphUnlocked`/`LoadWorldGraphUnlocked`/`DiffFactions`) and update
`LoamForecast.ProjectedStock`/`LoamBalance.PerSector`/`WorldEndpoints.cs`'s debug view to resolve and
pass the stored modifier rather than silently under-reporting, per §Design 4-5.

**Acceptance criteria:**
- [ ] A world saved with a faction at `ScopeModifierMilli == 1500`, reloaded, reads back 1500 — not the
  record default 1000.
- [ ] The value survives a turn-commit `DiffFactions` pass unchanged when nothing about that faction's
  Wonders changed.
- [ ] All three secondary call sites report the same yield for a sector as `LoamPhases.Production` would
  compute for it, given the same `WorldState`.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WorldGraph"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 2.3a.

**Estimated scope:** M (4 files: `RpgStore.World.cs`, `RpgStore.WorldGraphDiff.cs`, `LoamForecast.cs`,
`LoamBalance.cs`, `WorldEndpoints.cs` — **at the edge of size guideline**, split by Data/Core boundary
if needed).

#### Task 2.3c: `wonder-effect-empire` — the upkeep term (owner-requested addition)

**Description:** Add the fifth `LoamUpkeepBreakdown` term (`WonderUpkeep`), charged to the Wonder's own
hosting sector, proportional to `ValueMilli × EmpireWonderUpkeepRateMilli / 1000`, per §Design 6 (added
this session after the owner explicitly chose to offset the economy risk now rather than defer). This
is the mechanism that makes losing a Wonder's sector a real, felt loss, not just a benefit that vanishes
— it reuses `LoamPhases.Pressure`'s existing per-sector-drawn/per-component-faded mechanism verbatim.

**Acceptance criteria:**
- [ ] `LoamUpkeepBreakdown` gains `WonderUpkeep` as a fifth additive field; `Sum`/`Total` include it.
- [ ] `Breakdown(...)`'s new trailing parameter is defaulted (`= 0`) so every existing call site
  (including the belief-side overload, deliberately left at 0) compiles unchanged.
- [ ] `EmpireWonderUpkeepRateMilli` lives in `LoamPolicy`/`data/tuning/loam.v{n}.json`'s existing
  `upkeep` block — **not** `WonderPolicy`/`loam-relics-wonders.v1.json`.
- [ ] A faction that lets a Wonder's hosting sector/component starve loses the Wonder outright (the
  existing lost-sector branch already clears the slot); `ScopeModifierMilli` recomputes down the very
  next Production phase with no new bookkeeping.
- [ ] **Sum-then-divide, not divide-then-sum** (`spec-wonder-effect-empire.md` §Design 6's own dedicated
  reasoning paragraph and §Testing strategy) — two Empire-scope `LoamGenerationRate` effects on the same
  sector produce the same `WonderUpkeep` as one effect authoring their combined `ValueMilli`, proving
  `Σ(ValueMilli) × rate / 1000`, never `Σ(ValueMilli × rate / 1000)`; use a deliberately non-round
  `ValueMilli`/rate pair so the two orderings would disagree if the wrong one were implemented (mirrors
  `LoamUpkeepTests.The_formula_divides_only_once_not_once_per_multiplier`'s own proof shape).
- [ ] **`checked`-overflow proof for the new multiply:** a synthetic `ValueMilli`/`EmpireWonderUpkeepRateMilli`
  pair large enough to overflow `long` throws `OverflowException` at the multiply, never wraps.
- [ ] **Same-pass activation parity:** a Wonder whose `ConstructionTurnsRemaining` reaches zero this
  exact Production pass already contributes both its `ScopeModifierMilli` share and its `WonderUpkeep`
  charge this same Pressure phase — not the phase after, for either side.
- [ ] **Belief-overload-unaffected regression:** `LoamUpkeep.For(int, int, int, int, int, int)` (and its
  only real caller, `FrontierRulesPolicy.cs:192`) returns the identical value before and after this
  addition, proving the new defaulted trailing parameter changed no existing behaviour.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~LoamUpkeep"`

**Dependencies:** Task 2.3a (reads `WonderEmpireEffects.EmpireLoamGenerationValueMilliFor`).

**Estimated scope:** S-M (2 files: `LoamUpkeep.cs`, `LoamPolicy.cs`). **Open, non-blocking:** the
provisional rate (`50`) named in the spec is a real balance number with no playtest data behind it yet
— ship with it, flag for a balance pass, do not block this task on getting it "right" first.

#### Task 2.4: `wonder-build-flow` — Core-side (buildable now, no external dependency)

**Description:** Per `docs/architecture/loam-relics-and-wonders/spec-wonder-build-flow.md`'s own
"External dependency status" table — everything that touches only `WorldState`/`StructureDef` and needs
neither a relic instance's real ownership nor `scoped-inventory-hierarchy`'s tables: `WorldCommand
.RelicInstanceIds`, `StructureDef.RelicCost` + `Validate` pairing rule, the admission-time count/dup
check, `WonderExistenceScan.CountExisting`, and the `BuildResolver.Run` extension (cap check +
**the pre-existing rubble/ironwork wiring-gap fix**, independent of Wonders entirely).

**Acceptance criteria:**
- [ ] An ordinary, non-Wonder `build` order is unaffected — zero behavior change for the 25 shipped
  structure rows.
- [ ] **The rubble/ironwork fix**: any structure (Wonder or not) with a nonzero `ConstructRubbleCost`/
  `ConstructIronworkCost` now correctly refuses/debits via `BuildResolver`, which never read those
  fields before this task — test this independently of any Wonder content.
- [ ] `WonderExistenceScan.CountExisting` counts a Wonder **the moment its build order is accepted**,
  not at completion — the session's own found-and-fixed existence-cap bypass (a regression test proves
  two `Unique`-rarity Wonders of the same scope cannot both be under construction at once).
- [ ] Admission refuses `relic.count-mismatch`/`relic.duplicate` correctly.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderBuild|BuildResolver"`

**Dependencies:** Task 1.4 (`WonderPolicy.ExistenceCapFor`, `StructureDef.WonderScope`/`.WonderRarity`).

**Files likely touched (corrected — the plan's own list omitted the wire-format parse site):**
`WorldCommand.cs`, `WorldCommandAdmission.cs`, `StructureCatalog.cs` [shared edit with Task 1.4 —
sequence after it], `src/FusionRpg.Core/World/StructureSeed/StructureCorpus.cs` (the `RelicCost`
optional wire-field parse site, per `spec-wonder-build-flow.md` §Design 2/§Structure — missing from
this task's original file list), `BuildResolver.cs`, `WonderExistenceScan.cs` (new).

**Estimated scope:** L (6 files, not 5 — crosses from the plan's own M/edge-of-guideline framing into
L territory once `StructureCorpus.cs` is counted correctly). **Split recommended**: the materials-fix +
cap-check + `StructureCorpus.cs` wire parsing in one task, the admission-time relic count/dup check
(`WorldCommand.cs`/`WorldCommandAdmission.cs`) in a second — do not silently absorb the extra file into
a single session without flagging it, per this plan's own size guideline.

### Checkpoint: Phase 2 complete

- [ ] All Phase 2 tasks' test suites pass.
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1` green.
- [ ] Within `scoped-inventory-hierarchy`: a legion can load cargo, transfer it to another legion or a
  sector, and — if destroyed — its cargo lands in a real corpse-cache row. End-to-end manual/integration
  check, not just per-module unit tests.
- [ ] Within `loam-relics-and-wonders`: a Sector-scope Wonder row (hand-authored test fixture) correctly
  boosts its own sector's loam yield with zero engine change; an Empire-scope one correctly sums into
  `ScopeModifierMilli`, persists across a save/load, and now correctly costs upkeep.
- [ ] Review with human before Phase 3.

---

### Phase 3: `wonder-build-flow` — Data-side (the final integration)

#### Task 3.1: relic reachability pre-check + spend, inside the real turn-commit transaction

**Description:** Per §Design 4, 6-7 — `ValidateWonderRelicCommandsUnlocked` (runs immediately before
`TurnEngine.Step` inside `CommitWorldTurn`, verifying every named relic instance is owned, unassigned,
reachable via `legion-cargo`/`sector-storage`, and not already claimed by an earlier command in the same
batch) and `SpendWonderRelicsUnlocked` (runs immediately after `DiffWorldGraphUnlocked`, deleting the
relic from wherever it sat and marking `rpg_item.disposition = 'consumed'`) — both inside the **same**
transaction `CommitWorldTurn` already opens.

**Acceptance criteria:**
- [ ] A Wonder-shaped `build` order with the correct relic count, affordable materials, an empty
  compatible slot, and headroom under the existence cap succeeds end-to-end, in one turn commit.
- [ ] **Move, never copy, stated explicitly** (`relic_spend_moves_never_copies`,
  `spec-wonder-build-flow.md` §Testing strategy): after a successful Wonder build, the relic's source
  cargo/storage row is gone **and** `rpg_item.disposition = 'consumed'`, asserted as mutually exclusive
  outcomes — never both still present, never neither changed.
- [ ] A relic named by an earlier command in the same batch cannot fund a second Wonder the same turn.
- [ ] A command dropped for any reason (cap, materials, wrong slot, unreachable relic) leaves every
  named relic's row and disposition completely untouched — the relic spend only fires for an accepted
  `"build.started:"` report line.
- [ ] A relic moved out of reach between order-filing and commit (by an unrelated command in the same
  batch) correctly refuses at commit time, not at admission.

**Verification:**
- [ ] Tests pass: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WonderBuild"`
- [ ] `.\scripts\guard-dal.ps1` green.

**Dependencies:** Task 2.4 (this program's own Core-side work), Task 2.1 (`cargo-transfer`, completing
`scoped-inventory-hierarchy`'s own build — the actual hard external block this task has waited on since
the very first `/spec` session), Task 1.3a-c (a real relic instance to spend). **Distinguished
explicitly, same map-coarser-than-code pattern as Task 2.2's own correction (#8 above), but resolved
differently here:** `spec-wonder-build-flow.md` §Design 4/7 reads/deletes `rpg_world_entity_cargo`/
`rpg_world_sector_storage` rows directly and never calls `cargo-transfer`'s own functions, so the
literal compile/schema dependency is only Task 1.1 (`legion-cargo`) + Task 1.2b (`sector-storage`'s
table). Keeping Task 2.1 as a listed dependency here is a **deliberate choice, not an error**: this
task is the final integration of the whole `scoped-inventory-hierarchy` sub-program (per the map's own
sequencing and the spec's own "the actual hard external block" framing), so waiting for `cargo-transfer`
to ship — even though this task's own code never calls it — is "wait for the sibling program to be
done," not a code dependency. Stated so a reader does not mistake this for the same map-vs-code drift
Task 2.2 needed corrected.

**Estimated scope:** M (2 files: new `RpgStore.WonderBuild.cs`, `RpgStore.WorldTurns.cs` edit).

### Checkpoint: Phase 3 complete — full program integration

- [ ] End-to-end: mint a relic via a real drop table (Task 1.3c's content), carry it in a legion's cargo
  or deposit it into a sector's storage, spend it to build a Sector- or Empire-scope Wonder, observe the
  loam-production effect (and, for Empire scope, the upkeep cost) on the very next Production phase.
- [ ] Full test suite green: `dotnet test tests\FusionRpg.Core.Tests`, `tests\FusionRpg.Data.Tests`,
  `tests\FusionRpg.Guard.Tests`.
- [ ] All 5 boundary guards green (`guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`,
  `guard-dal`, `guard-actor-hub`, `guard-test-substrate`).
- [ ] `python scripts/audit-overflow.py` and `python scripts/audit-magic-numbers.py` clean of new
  findings from this program.
- [ ] Ready for owner review / live deploy-play smoke test.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `loot-pack` §7's own ask (Task 0.1's wipe-path sub-item) may still be unaccepted when Phase 0 starts | Med — blocks only the wipe-path half of `corpse-cache`, not the death path `cargo-fate` alone needs | Verify status before starting; if still open, ship the death path and flag the wipe path as a named, tracked follow-up rather than blocking Phase 0 entirely |
| `SaveAssignment`/`RemoveAssignment`'s Roster-phase gate (Task 0.1's own anti-fraud guarantee) is an unaccepted ask on the item program, not this program's to grant | Low — the core move-on-death/move-on-wipe mechanism works without it; only the "gear is provably deploy-time, not death-time" guarantee is unproven until it lands | Ship the core mechanism now; track the phase-gate ask against the item program, matching the `loot-pack` §7 row's own treatment above |
| `StructureCatalogImportTests` pins literal population counts (pre-existing debt, not introduced by this program) | Low — the numbers are correct today; the first real Wonder/`ItemStorage` seed row that ships will need a one-line bump | Not this program's to fix; note it in the PR that adds the first real row so the bump isn't mistaken for a test failure |
| Relic mint volume has no named sink beyond Wonder-spend (ideal doc Open question #9) | Low, deferred | Not blocking; a future balance pass watches relic-pile growth once content ships |
| `EmpireWonderUpkeepRateMilli`'s provisional value (`50`) has no playtest data | Med — could be too cheap (risk not actually offset) or too harsh (Wonders feel punishing) | Ship provisional, tunable (data file, not code), revisit after first real playtest |
| Tasks 1.3a and 1.4 both touch `StructureCatalog.cs`/related files independently (relic vs. Wonder additions are in different files, but both are Wave 1 parallel work) | Low — different files per spec's own Structure tables, but worth a merge-conflict check | Confirm at task-assignment time that 1.3a and 1.4 touch disjoint files before parallelizing; they do per each spec's own file list |
| Phase 0 duplicates work if a separate `deployment-hierarchy-plan.md` also builds the same trio | Low — wasted effort, not a correctness risk | Check for an existing `tasks/deployment-hierarchy-plan.md`/`-todo.md` before starting Phase 0; if one exists and already covers this, skip Phase 0 and link to it instead |

## Deferred, named future work

Three sibling specs each carry a "Notification hook — named, not built (strengthen pass, 2026-09-13)"
section — a real, player-relevant, currently-silent event this program creates with no in-band signal.
None of the three modules that carry these sections builds the hook; each names it so a future
notification-consumer session does not have to rediscover the event independently by re-reading three
specs:

- `spec-sector-storage.md` — a sector's storage becoming reachable/unreachable to a different owner the
  instant `ClaimResolver` reassigns `OwnerFactionId` (§5's own "no migration needed" mechanism); the
  affected player has no signal their stored items silently changed hands.
- `spec-cargo-fate.md` — a legion's cargo silently becoming a decaying-but-never-voiding, revisit-lootable
  cache on death; the player who lost the legion has no in-band signal their haul is sitting recoverable
  at a specific sector or lane.
- `spec-wonder-build-flow.md` — a Wonder finishing construction (a `TurnReportEntry` already fires,
  `"build.started:{structureId}"`, but nothing reads it as a player-facing signal) and a `build` order
  refusing on `wonder.cap-reached` (the player spent a turn filing an order that resolved to nothing,
  with no in-band explanation beyond the turn report).

All three name `docs/architecture/notification-ssot-ideal.md` as the eventual consumer and each states
it would reuse an already-written event (a table write, a `TurnReportEntry`) rather than needing a new
emission path. Not a task in this plan — carried here so a future session picks up all three at once
instead of finding them one spec at a time.

## Open Questions

None blocking — the `/spec` strengthen pass (2026-09-13) already resolved every architectural fork this
plan depends on. Two genuinely open **content/balance** decisions remain, both explicitly non-blocking
per their own specs:

- Exact relic drop weight/rate per `SourceKind` table (Task 1.3c) — pick a reasonable low default, flag
  for balance pass.
- `EmpireWonderUpkeepRateMilli`'s real value (Task 2.3c) — ship the spec's provisional `50`, flag for
  balance pass after playtest.
