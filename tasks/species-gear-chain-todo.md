# Tasks: `species-gear-chain`

**Plan:** [species-gear-chain-plan.md](species-gear-chain-plan.md) · **Specs:**
`docs/architecture/species-gear-chain/spec-<module-id>.md` · **Map:**
`docs/architecture/species-gear-chain-map.md`

Each task cites its owning spec — read that file's Design/Code style/Testing strategy/Boundaries
sections before starting; this list gives acceptance criteria, verification and scope, not full
design detail.

---

## Phase 1 — foundations (parallel-safe, no dependencies)

### Task T1: `tier-propagation-contract` a — derive `RungCount`, guard the identity
**Description:** `CreatureRarityLadder.RungCount` is a `const` while `All` derives from
`Enum.GetValues`; they disagree by construction the moment the enum widens. Make `RungCount` derived.

**Acceptance criteria:**
- [ ] `RungCount => All.Count`, no `const` left
- [ ] `OneRungAbove`'s throw is proven correct at a *widened* enum width via a test double, not at the
      literal width 10

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LadderDeclaration"`
- [ ] `.\scripts\guard-actor-hub.ps1` (no behaviour change to composition)

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Creatures/CreatureRarityLadder.cs`,
`tests/FusionRpg.Core.Tests/Creatures/CreatureRarityLadderTests.cs`
**Size:** XS (1 file + 1 test file)

---

### Task T2: `tier-propagation-contract` b — remove restatements, add the guard, split T-5
**Description:** Remove the three in-repo restatements this spec covers (site 1
`EncounterTuning.ThreatRungIds`, sites 2–3 the Python `RARITY` tuples in
`creatures/anchor/schema.py` and `structures/anchor/schema.py`), add a guard test that fails on a new
restatement, and add the T-5a/T-5b split (actor-magnitude `thetaOffset` vs. economy-cost coefficient)
to `ssot-power-scale.md` §10. Leave site 4 (`Items/RarityLadder.RungIds`) **allowlisted**, not
converted, per the spec's Open question 1.

**Acceptance criteria:**
- [ ] Sites 1–3 read from tuning instead of restating ids; no behaviour change
- [ ] A new test fails if a fourth restatement is added anywhere the guard scans (C# and Python)
- [ ] `ssot-power-scale.md` §10 states T-5a/T-5b explicitly, replacing the single-clause version

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q`
- [ ] `python scripts/audit-magic-numbers.py --summary` (no new bare literal introduced)

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Dungeon/Tuning/EncounterTuning.cs`,
`tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py`,
`tools/seedsmith/seedsmith/adapters/structures/anchor/schema.py`,
`tests/FusionRpg.Guard.Tests/`, `docs/architecture/power/ssot-power-scale.md`
**Size:** M (5 files)

---

### Task T3: `threat-band-fill` — score first, default second, with provenance
**Description:** `resolve_unresolved_threat_band` returns the flat `inferredDefaultRung` for every
unresolved value without trying the scorer first. Try `classify()` first; fall back to the sanctioned
default only when the seed is not scoreable; record which happened.

**Acceptance criteria:**
- [ ] Every species with a scoreable power seed (`observed`/`stated` basis) carries a **scored**
      `threatBand`, not the flat default
- [ ] Every species carries provenance distinguishing `scored`/`default`/`authored`
- [ ] `SpeciesExpander.cs:31-33`'s exclusion of `threatBand` from the batch-refusal list is
      **preserved** (it is correct — see the spec's own correction to the ideal), with a comment
      recording why

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_anchor_derive.py tools/seedsmith/tests/test_run_runner.py -q`
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Threat"`
- [ ] `dotnet run --project tools/CreatureQualityReport` — print, don't assert, the new histogram

**Dependencies:** None
**Files:** `tools/seedsmith/seedsmith/adapters/creatures/anchor/derive.py`,
`tools/seedsmith/seedsmith/adapters/creatures/run/runner.py`,
`tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py`,
`data/seed/creatures/species/**` (regenerated output)
**Size:** M (3 source files + a regenerated corpus)

---

### Task T4: `wave-species-roll` — seeded weighted draw, acquisition filter
**Description:** Replace `WaveCatalog`'s `pool[i % pool.Count]` with a seeded weighted draw over a
rarity **window**; add the missing acquisition filter (`EventOnly` refused, unconditionally, first).

**Acceptance criteria:**
- [ ] Draw is seeded and replay-stable; a rarity window replaces the four hardcoded single-rung bands
- [ ] `EventOnly` refused by rule, proven with a synthetic `Summonable | EventOnly` species (no shipped
      species carries both today, so the population alone would pass a weaker filter)
- [ ] `waves.v1.json` gains `schemaVersion`/`version` (the file has neither today — this is an **add**,
      not a version bump) plus the window/weight tables; no weight is a `const` in C#

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~WaveCatalog"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`, `tests/FusionRpg.Data.Tests`
- [ ] Confirm no golden re-blessed (the four `rift-*` wave ids are outside the `golden-*` fixture set)

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Battle/WaveCatalog.cs`, `data/tuning/waves.v1.json`,
`tests/FusionRpg.Core.Tests/Battle/`
**Size:** M (2 source + tests)

---

### Task T5: `wild-species-spawn` — shared admission policy + sector roll
**Description:** Add the one-declaring-site `CreatureAdmission` policy type (per-context member, not
prose duplicated across specs), and replace `SpawnTheUnmade`'s `"normalzombie"` literal with a
sector/climate-weighted roll using `ForWildMap` (admits `CaptureOnly`, refuses `EventOnly`).

**Acceptance criteria:**
- [ ] `CreatureAdmission.ForWave` / `ForWildMap` / `ForDelve` exist in one file; `wave-species-roll`
      (T4) and this task both read it (coordinate: whichever lands first creates the type)
- [ ] Wild warband composition is rolled, seeded from `(worldSeed, sectorId, turn)`, replay-stable
- [ ] `CaptureOnly` admitted, `EventOnly` refused, each proven with a species carrying that flag
- [ ] Spawn cadence, occupancy guarding, and the `unmade.spawned` turn-report event are unchanged

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Loam"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~WorldTurn"`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`, `tests/FusionRpg.Data.Tests`

**Dependencies:** None (coordinate `CreatureAdmission`'s single declaring site with T4)
**Files:** `src/FusionRpg.Core/World/Loam/LoamPhases.cs`,
`src/FusionRpg.Core/Creatures/CreatureAdmission.cs` (new), `data/tuning/` (new world-spawn domain file),
`tests/FusionRpg.Core.Tests/World/`
**Size:** M (3 files + tests)

---

### Task T6: `socket-allowance-by-kind` — the per-kind table, shift-before-roll
**Description:** Add `socketAllowanceByKind` to `sockets.v1.json` (version bump 2→3); shift the
rarity window before the roll; add all four rejections the derived table needs (inverted,
non-overlapping, non-monotonic, negative) — not just the inverted case.

**Acceptance criteria:**
- [ ] Composition order is: window shift → roll → base `socketMax` clamp → role ceiling → structural
      bound (throws above `structuralCeiling`)
- [ ] All four rejections load-reject naming kind and rung; OD4 overlap and monotonicity are
      preserved for the derived table, not just the authored one
- [ ] `socketAllowanceByKind` rows are **append-only, pinned to `catalog_revision`** — same contract as
      `rarityGrant`; an ordinary item's socket count is byte-identical to today for every rung/role

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSocket"`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`

**Dependencies:** None
**Files:** `data/tuning/sockets.v1.json`, `src/FusionRpg.Core/Items/Sockets/SocketTuning.cs`,
`tests/FusionRpg.Core.Tests/Items/Sockets/`
**Size:** M (2 source + tests)

---

### Task T7: `craft-risk-ladder` — Stage 1 only (potential, always-succeeds)
**Description:** Add `craft_potential_max`/`craft_potential_current` columns to `effect_instance`
(idempotent `ALTER TABLE`, nullable, lazy backfill); derive potential from `class`/`rarity`/`tags`
with an explicit-or-absent authored override (never a sentinel). **Stages 2–4 (durability decay,
break, repair-loss) are explicitly out of scope for this task** — see the plan's § Deferred.

**Acceptance criteria:**
- [ ] Every rolled equipment instance carries a derived potential pair; pre-existing instances
      backfill lazily and are never mistaken for exhausted
- [ ] The override is explicit-or-absent; an expected-but-missing one throws naming the base type
- [ ] While potential remains, every craft verb succeeds — proven across the priced operations
- [ ] No decay, no destroy path, no `EnhanceOutcome` member added — this task stops at "assured"

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Potential"`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-test-substrate.ps1`
- [ ] `python scripts/audit-overflow.py` (potential is `long`; verify no int/float slip)

**Dependencies:** None
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`,
`src/FusionRpg.Core/Items/*/PotentialTable.cs` (new), `tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2-3 files + tests)

---

### Task T8: `gem-tier` — derive tier from `powerBand`, stop hardcoding 1
**Description:** Replace the hardcoded `UnauthoredInsertTier = 1` at the three socket call sites with
`UniqueBudget.TierOfPowerBand(gem.PowerBand)` — the same function `GemContainerBuild.cs:49` already
uses in production. **Do not author a `tier` field on a gem entry** — `entry-shapes.md:81` makes that
an explicit `OwnershipViolation`.

**Acceptance criteria:**
- [ ] Every shipped gem reports a tier derived from its own `powerBand`, at all three call sites
- [ ] An unknown `powerBand` is a rejection naming the gem; no silent fallback to tier 1
- [ ] Every combination whose ingredient families are all present in the gem corpus becomes
      satisfiable, proven by a test computing both sides from the corpus (not a pinned count)
- [ ] No gem entry gains a `tier` field; `ItemSeedValidator`'s `OwnershipViolation` check stays green

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Gem"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** None
**Files:** `src/FusionRpg.Server/ItemCardEndpoints.cs`, `ItemSurfaceEndpoints.cs`, `ItemWorkbench.cs`
(the three hardcoded-1 sites), `tests/FusionRpg.Core.Tests/Items/`
**Size:** S (3 call sites + tests)

---

### Task T9: `enhance-track-wiring` a — generate the missing `atom.enhance-*` rows
**Description:** `FamilyExpandGen` reads only `affix-families/`, top-directory-only, so
`atom.enhance-*` has zero expanded atom rows anywhere — there is no magnitude source for the
milestone atoms to append even once wiring exists. Widen the generator's scope; regenerate.

**Acceptance criteria:**
- [ ] `atom.enhance-*` families produce real rows in `data/seed/atoms/generated/**`
- [ ] `FamilyExpandGen --check` is green; the diff is a pure regeneration (no hand edits)
- [ ] Every shipped `enhanceTrack[].family` resolves against the regenerated corpus, asserted as a
      join, not a count

**Verification:**
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EnhanceTrack"`

**Dependencies:** None
**Files:** `tools/FamilyExpandGen/Program.cs`, `data/seed/atoms/generated/**` (regenerated)
**Size:** S (1 generator file + regenerated corpus)

---

### Task T10: `enhance-track-wiring` b — wire the milestone append
**Description:** `ItemWorkbench.Enhance` passes `Array.Empty<AtomAppend>()` at `:272`, and
`RpgStore.AppendMutationOpUnlocked` applies `result.Suppressed` but never inserts for
`result.Appended` — a second inert seam beyond the one the ideal named. Fix both in the same change so
a milestone level actually grants its atom.

**Acceptance criteria:**
- [ ] An enhancement to an authored milestone level appends a real atom, recorded in the op ledger
      **and** present on `effect_instance_atom` — proven on a live item, not only a unit test
- [ ] `EnhancePolicy.IsMilestoneLevel` gains a production caller (today: test-only)
- [ ] The ladder is unbounded above — a milestone still grants at a level far past the last authored
      `atLevel`, asserted by test (no hard ceiling)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-dal.ps1`

**Dependencies:** T9 (needs real atom rows to append)
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`, `tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2 files + tests)

---

## Checkpoint — Phase 1
- [ ] `dotnet test tests/FusionRpg.Core.Tests` green
- [ ] `dotnet test tests/FusionRpg.Guard.Tests` green
- [ ] `dotnet test tests/FusionRpg.Data.Tests` green
- [ ] `dotnet test tests/FusionRpg.Server.Tests` green
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q` green
- [ ] No golden re-blessed, or a separately reviewed commit names which moved and why
- [ ] **Review with owner before Phase 2**

---

## Phase 2

### Task T11: `ladder-consistency-repair` a — publish `themes.v2.json`
**Description:** The 84 retired rarity ids in `themes.v1.json` survive because the registry is
deliberately append-only, and `--rebuild`'s sanction expired the moment 844 set entries bound to
`creature.*` keys. Publish v2 with `rarity` re-derived from the current anchor and every other field
byte-identical; migrate `set-charm-gen` and the other v1 readers.

**Acceptance criteria:**
- [ ] `themes.v2.json` has 904 rows, zero retired rarity ids, every non-`rarity` field byte-identical
      to v1 (asserted as a closure property, not "84 were fixed")
- [ ] No `themeKey` renamed — the 844 bound set entries are unaffected
- [ ] `set-charm-gen` and the other v1 consumers read v2; v1 is left in place, unretired

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k theme`
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures`

**Dependencies:** T2 (`tier-propagation-contract` — this module depends on it per the map)
**Files:** `tools/seedsmith/seedsmith/adapters/creatures/generate_themes.py`,
`data/seed/creatures/_registry/themes.v2.json` (new),
`tools/seedsmith/seedsmith/adapters/creatures/generate_families.py`, `theme_enrich.py`
**Size:** M (1 generator + 2 consumer files + new registry)

---

### Task T12: `ladder-consistency-repair` b — FE roster sort by ordinal
**Description:** Four FE sites key on the retired four-value rarity vocabulary and silently fail to
sort. Replace with an ordinal comparator that surfaces an unknown id instead of defaulting.

**Acceptance criteria:**
- [ ] The FE roster sort visibly sorts, proven with species spanning several rungs
- [ ] An unknown rarity id renders a visible "unknown" marker rather than silently sorting to a
      default
- [ ] `npm run build` (`tsc --noEmit`) passes — no site still references a retired id

**Verification:**
- [ ] `cd web/fusion-rpg-web; npm test`
- [ ] `npm run build`
- [ ] `npm run check:bundle`

**Dependencies:** None (independent of T11)
**Files:** `web/fusion-rpg-web/src/features/creatures/rosterSplit.ts`,
`src/lib/bus/creatures.ts`, `src/layers/pacts/PactsLayer.tsx`, `src/pages/CreaturesPage.tsx`
**Size:** M (4 files)

---

### Task T13: `species-magnitude-synth` — synthesize containers at import time
**Description:** `ReconcileCreatureMagnitudeBindingsUnlocked` already fails closed correctly at
`RpgStore.UniqueActors.cs:1619-1620` because zero `trait.species-magnitude-*` containers exist.
Synthesize one per species in `ImportCreatureSpecies`, from rows already in
`creature_species_magnitude` — upsert, never a committed corpus.

**Acceptance criteria:**
- [ ] A deployed creature specimen binds its species-magnitude container instead of taking the
      fail-closed return — proven end to end
- [ ] Every species with magnitudes has exactly one container; every species without has none
- [ ] Container members reconcile exactly against `creature_species_magnitude`; import is idempotent
- [ ] Zero species-magnitude files committed to `data/`; values are `long` end to end

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Species"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueActor"`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`, `.\scripts\guard-test-substrate.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T3 (`threat-band-fill` rewrites every magnitude — must land first)
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Species.cs`, `tests/FusionRpg.Data.Tests/`
**Size:** M (1 file + tests) — **do not modify** `RpgStore.UniqueActors.cs:1619-1620`

---

### Task T14: `delve-species-wiring` a — fix the `CreatureTypeId` collision
**Description:** `SlotFilter.CreatureTypeId` omits the plant offset the other two sites apply,
colliding 102 measured plant/zombie `gameTypeId` pairs onto the same id. Extract one
`CreatureTypeIdFor(side, gameTypeId)` function; convert all three sites to call it.

**Acceptance criteria:**
- [ ] `CreatureTypeId` is unique across the full catalog, enforced by a test (not just at the
      catalog's own load-time guard, which `SlotFilter`'s computed property bypasses today)
- [ ] The plant offset lives in exactly one function, called by all three sites
- [ ] A plant and a zombie sharing a `gameTypeId` get different ids

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureTypeId"`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Creatures/CreatureSpeciesCatalog.cs`,
`src/FusionRpg.Core/Delve/Encounter/SlotFilter.cs`,
`src/FusionRpg.Core/Creatures/Generation/ConcreteSpeciesSeedReader.cs`, `CreatureSpeciesGenerator.cs`
**Size:** S (4 files, one function extraction)

---

### Task T15: `delve-species-wiring` b — production caller for `Encounter.Build`
**Description:** `Encounter.Build` has no production caller anywhere. Wire the Delve's room resolution
to call it for a real room; surface `EncounterRefusal` rather than swallowing it.

**Acceptance criteria:**
- [ ] A real delve room resolves through `Encounter.Build`
- [ ] `EncounterRefusal` surfaces and is reported, never silently defaulted
- [ ] `offClimateMilli`, `sameSpeciesMaxMilli`, `threatWindow` become live for the first time
- [ ] Encounter selection is deterministic for a given seed

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Encounter"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-actor-hub.ps1` (this module adds a *caller*, not a contributor — must not
      deepen the `BattleStatComposer` fork)

**Dependencies:** T3 (`threat-band-fill` — the null-`ThreatBand` refusal is correct and load-bearing),
T14
**Files:** `src/FusionRpg.Server/DelveBattleSessionManager.cs`
**Size:** S (1 file, wiring only)

---

### Task T16: `socket-combat-wiring` a — the `EquipProjector` contribution seam
**Description:** Sockets reach no combat today — every production reader is card/surface/workbench.
`ApplyEquipProjection` reaps any binding not in its own `desired` set, so the seam **must** be
`EquipProjector`, not a second composer. Wire a socketed insert's contribution through it into
`ActorHub`; file the GG-49 `ContributionSourceIds` grammar amendment.

**Acceptance criteria:**
- [ ] A socketed gem measurably changes a combat number, proven end to end: socket → deploy →
      `ActorHub.ResolveDerivedWithContributions` → the channel moved — **this has never been true**
- [ ] The contribution is attributed (insert, host item, role, socket index) under the amended §8.1
      grammar
- [ ] The contribution survives a second `MaterializeRolledEquipRuntime` (comes from the projection,
      so the reaper does not eat it)
- [ ] No second `*Composer*` introduced anywhere

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~EquipProjection"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T8 (`gem-tier` — a tier-less insert has nothing meaningful to contribute)
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (`ApplyEquipProjection`/`EquipProjector`),
`docs/architecture/actor-hub-ssot.md` §8.1 (amendment), `tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2 source files + doc amendment + tests)

---

### Task T17: `socket-combat-wiring` b — withdraw, orphan, refusal
**Description:** Complete the lifecycle: unequip/remove withdraws the contribution; an unresolvable
insert refuses the socket-insert by name rather than writing partial state.

**Acceptance criteria:**
- [ ] Unequipping the host, and removing the insert, each withdraw the binding — no orphan contribution
- [ ] `item_socket.insert_instance_id` holds a real instance for every filled socket; `""` no longer
      appears on that column in production
- [ ] An unresolvable insert refuses the socket-insert by name and writes no partial state

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~SocketInsert"`
- [ ] `.\scripts\guard-test-substrate.ps1`

**Dependencies:** T16
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs` (correct the stale "equip path reads it"
comment at `:32-33` in the same change), `tests/FusionRpg.Data.Tests/Items/`
**Size:** S (1-2 files + tests)

---

### Task T18: `rarity-promotion` a — item-side rung arithmetic + the `op_kind` ask
**Description:** `Items/RarityLadder` has no `IsTopRung`/`OneRungAbove`/`RungCount` — only `RungIds`,
`PromoteFrom`, `IsPityGuarded`. Build the item-side rung arithmetic this module needs (mirroring
`CreatureRarityLadder`'s shape without sharing its type), and file the `MutationOpKind` amendment
against `ssot-enhancement.md` §5.3 in the same commit.

**Acceptance criteria:**
- [ ] `Items/RarityLadder` gains `IsTopRung`/`OneRungAbove` over the string-keyed, 10/20-ordinal item
      ladder
- [ ] `MutationOpKind` gains exactly one new member (promotion), under a reviewed `ssot-enhancement.md`
      §5.3 amendment landed in the same commit
- [ ] `ssot-enhancement.md` §5.3's table also gains the missing `socket-imbue` row found this session
      (a pre-existing drift, reconciled here rather than separately)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RarityLadder"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Mutation"`

**Dependencies:** T7 (`craft-risk-ladder` Stage 1 — promotion's risk defers to it)
**Files:** `src/FusionRpg.Core/Items/RarityLadder.cs`,
`src/FusionRpg.Core/Items/Mutation/MutationOp.cs`, `docs/architecture/item/ssot-enhancement.md`
**Size:** M (2 source files + doc amendment)

---

### Task T19: `rarity-promotion` b — the executor + the card mark
**Description:** Copy the shipped `ItemWorkbench` verb pattern for the new promotion verb; surface
`promoted_from_ordinal` on the card.

**Acceptance criteria:**
- [ ] All ten authored `elevate` recipes execute end to end — the thing that has never been true
- [ ] Promotion is provably additive: every affix survives identically
- [ ] `promoted_from_ordinal` is written, correct across multiple promotions, visible on the card
- [ ] Promotion has no private failure chance (defers entirely to `craft-risk-ladder`); top-rung
      promotion refuses cleanly, never reaching the ladder's throw

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T18
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`,
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2 source files + tests)

---

## Checkpoint — Phase 2
- [ ] All ten authored `elevate` recipes execute end to end (T19's own acceptance test)
- [ ] A socketed gem measurably changes a combat number, proven through `ActorHub` (T16–17)
- [ ] `guard-actor-hub.ps1`, `guard-dal.ps1` green — no second composer anywhere in Phase 1–2 work
- [ ] `themes.v2.json` published; 844 bound set entries unaffected
- [ ] **Review with owner before Phase 3**

---

## Phase 3

### Task T20: `set-species-binding` a — schema + forward emission
**Description:** Add `speciesId`/`setClass` to the set entry schema; extend `set-charm-gen` to emit
both fields going forward.

**Acceptance criteria:**
- [ ] Every newly generated set entry carries `speciesId` (a real id or explicit absent) and
      `setClass`
- [ ] `SetEvaluator` behaviour is unchanged (class-agnostic today, stays so)

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_items_adapter.py -q`
      (confirm this test's pre-existing failure baseline first, per `AGENTS.md`)
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Set"`

**Dependencies:** T11 (`ladder-consistency-repair` — reads `themes.v2.json`)
**Files:** `tools/seedsmith/seedsmith/adapters/items/setgen/`
**Size:** S (schema + generator)

---

### Task T21: `set-species-binding` b — deterministic backward repair
**Description:** Repair the existing 910 entries, reading `themes.v2.json`'s `speciesId` field as the
primary join (case-normalising cross-check against the `themeKey` suffix parse — the registry's
`speciesId` is lower-case, the runtime catalog is PascalCase). Persist the **runtime catalog's**
spelling. No model call anywhere on this path.

**Acceptance criteria:**
- [ ] Every `creature.*` themeKey resolves, or the run fails naming the unresolved key
- [ ] `build.*`/`theme.*` keys (no species) get an explicit absent value, never a guess
- [ ] `ItemSeedValidator` asserts every non-absent `speciesId` resolves in `CreatureSpeciesCatalog`
- [ ] No set bonus magnitude changes — proven by a diff of evaluated bonuses before/after
- [ ] The repair is idempotent and deterministic (byte-identical on a second run)

**Verification:**
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/items --adapter items`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T20, T11
**Files:** `tools/seedsmith/seedsmith/adapters/items/` (new repair pass), `data/seed/items/sets/**`
(regenerated)
**Size:** M (1 new module + regenerated corpus)

---

### Task T22: `creature-drop-tables` a — E2 material-shelf credit
**Description:** A drawn `Material` entry is drawn and the grant emitted, but the shelf is never
credited. This is **2–3 Data files + tests**, not one call — includes fixing the
`PersistLootUnlocked`/credit-helper `string`/`long` player-id type mismatch the audit found.

**Acceptance criteria:**
- [ ] A drawn `Material` entry credits the shelf, proven end to end
- [ ] The player-id type mismatch is resolved (one type, used consistently on this path)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Loot"`
- [ ] `.\scripts\guard-dal.ps1`

**Dependencies:** T13 (`species-magnitude-synth`)
**Files:** `src/FusionRpg.Core/Items/Drops/LootMintAt.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.*` (`PersistLootUnlocked` + credit helpers),
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2-3 files + tests)

---

### Task T23: `creature-drop-tables` b — E1 the ninth source kind + paired arm
**Description:** Add a ninth `source_kind` for a creature kill, its authored tables in
`data/seed/loot/` (the **runtime** corpus — not `droptablegen`'s output tree,
`data/seed/items/drop-tables/`), and the paired `LootCorrelation.Derive` arm
`DropTableValidator.cs:52-59`'s own comment warns is required.

**Acceptance criteria:**
- [ ] A ninth `source_kind` id exists in the closed vocabulary, with authored tables in the correct
      corpus (`data/seed/loot/`)
- [ ] The matching `LootCorrelation.Derive` arm exists — neither list is complete without the other
- [ ] Kill attribution is keyed on a source the expedition/delve/wild paths can supply (not the lawn —
      `KillerActorKey` carries nothing there today)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~DropTable"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Loot"`

**Dependencies:** T13, and at least one of {T4, T5, T15} (a species-selection path must exist to kill
something)
**Files:** `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs`, `LootPipeline.cs`,
`data/seed/loot/**` (new tables)
**Size:** M (2 source files + new content)

---

### Task T24: `creature-drop-tables` c — E3a shard-by-rung at plan time
**Description:** Replace `ExpeditionResolver`'s `isBoss ? ShardRare : ShardCommon` ternary with a
lookup on the killed species' own rarity rung, computed **at plan time** from the planned wave's
species — preserving the manifest's determinism contract.

**Acceptance criteria:**
- [ ] The shard a creature yields follows its rung, not `isBoss`
- [ ] Expedition manifests remain byte-identical for a given seed, before and after — proven by a
      regression test, not asserted by inspection

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition"`

**Dependencies:** T23
**Files:** `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs`,
`tests/FusionRpg.Core.Tests/Expeditions/`
**Size:** S (1 file + a determinism regression test)

---

### Task T25: `craft-executor-completion` a — the `Forge` executor
**Description:** `ItemWorkbench.cs:189-193`'s "forge cannot run" comment is stale —
`EquipmentContainerBuild.From` already builds the container on the fly. Wire the seventh recipe verb.

**Acceptance criteria:**
- [ ] All 7 authored `forge` recipes execute end to end and mint a real, saved instance with
      `InstanceOrigin.Craft` — computed from the corpus, not a pinned count
- [ ] `ItemWorkbench.cs:189-193`'s comment is corrected, with the evidence cited
- [ ] Zero members added to `CraftOperation`/`MutationOpKind` by this task

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`

**Dependencies:** T19 (`rarity-promotion` — builds the pattern this task copies)
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`
**Size:** M (2 files)

---

### Task T26: `craft-executor-completion` b — `RerollOne`/`RerollAll`
**Description:** Wire the remaining two verbs through the already-built `RerollPolicy`.

**Acceptance criteria:**
- [ ] All authored `reroll-one` and `reroll-all` recipes execute end to end
- [ ] `reroll-all`'s suppress and append both reach `effect_instance_atom`
- [ ] `ItemWorkbench` exposes nine verb methods total, in one consistent shape, each with a POST
- [ ] The debit and the product commit together; replay is idempotent for all three new verbs (this
      task's two plus T25's forge)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-dal.ps1`

**Dependencies:** T25
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`,
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M (2 files + tests)

---

## Checkpoint — Phase 3
- [ ] Every set entry carries `speciesId`/`setClass`; `ItemSeedValidator` green; no set bonus
      magnitude changed (diffed before/after)
- [ ] A drawn `Material` entry credits the shelf end to end; expedition manifests unchanged for a
      fixed seed
- [ ] `ItemWorkbench` exposes 9 verb methods; zero new `CraftOperation`/`MutationOpKind` members from
      this phase
- [ ] **Review with owner before Phase 4**

---

## Phase 4

### Task T27: `species-cost-shaping` — the per-rung multiplier, gated
**Description:** Add `speciesCostMultiplierMilli` + `speciesCostThresholdRung`, applied **beside**
(never folded into) `costBandMultiplierPerMille`.

**Acceptance criteria:**
- [ ] A set piece whose species sits at or above the threshold rung costs more, by the tunable
      per-rung multiplier
- [ ] Below the threshold, and for species-less pieces, costs are byte-identical to today
- [ ] `MaterialCorpusTests`' band-multiplier mirror assertion (against the frozen `bands.v1.json`
      registry) is still green, proving nothing was folded in
- [ ] Missing section, unknown rung, out-of-range multiplier all reject at load
- [ ] The shared `ssot-power-scale.md` §10 row is filed (see power-map's ask, covers this module,
      `rarity-promotion`, and `item-upgrade-tree` in one row when the last of the three lands)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostClass"`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `python scripts/audit-magic-numbers.py --targets M1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T21 (`set-species-binding` — needs the queryable species field)
**Files:** `data/tuning/materials.v1.json`, `src/FusionRpg.Core/Items/Materials/MaterialTuning.cs`,
`tests/FusionRpg.Core.Tests/Items/Materials/`
**Size:** M (2 files + tests)

---

## Checkpoint — Phase 4
- [ ] Below the threshold rung, costs are byte-identical to pre-change
- [ ] `MaterialCorpusTests`' band-multiplier mirror assertion still green
- [ ] **Review with owner before Phase 5**

---

## Phase 5

### Task T28: `species-materials` a — file the sixth `MaterialClass`, fix the `27` pins
**Description:** Read `deployment-hierarchy-map.md`'s own contested filing for a new material class
first; propose one reconciled shape before implementing. Replace the three live `27` pins in
`materialgen` (two of which are module-level `assert`s that hard-crash seedsmith on import) with a
reconciliation canary.

**Acceptance criteria:**
- [ ] The sixth `MaterialClass` (provenance) is added under a filed, answered ask, with its question
      stated in its doc comment — the "which of these five is unanswerable for my spend?" test
- [ ] `CostClassMatrix.Allows` has a narrow arm (only the *improve* verbs spend it); every operation
      still resolves without throwing
- [ ] `materialgen/vocab.py:120`/`:124` and `test_recipes_gen.py:193` are replaced with
      `len(ISSUABLE) == len(MaterialCatalog.All)` — no literal `27` remains as a pin
- [ ] No new `ContentRuleViolated` code added; the closed 33-code list is unchanged

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostClass"`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k material`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T27
**Files:** `src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs`, `CostClassMatrix.cs`,
`tools/seedsmith/seedsmith/adapters/items/materialgen/vocab.py`,
`tools/seedsmith/tests/test_recipes_gen.py`,
`docs/architecture/item/ssot-materials-crafting.md` (the §3.1 amendment)
**Size:** M (3 source files + 1 doc amendment)

---

### Task T29: `species-materials` b — the general + species-unique layers
**Description:** Generate the two material layers (general carries the bulk; species-unique is 1–2
per species, tunable, settable to 0 for general creatures) in `creature-yield.v1.json` — **confirm
this file was not already created independently by T23** before adding to it. D5's deterministic
exchange ships with this task (already decided; not reopened).

**Acceptance criteria:**
- [ ] A species-bound set piece above the threshold rung is enhanced with its own species' material,
      end to end — the decision this whole initiative exists to deliver
- [ ] Below the threshold, and for species-less pieces, nothing changes
- [ ] Per-species count is tunable in 1–2, settable to 0 for general creatures; general layer carries
      the volume
- [ ] One `creature-yield.v1.json`, shared with `creature-drop-tables` (T23) — not a second file
- [ ] D5's deterministic exchange (`exchangePriceSouls`/`exchangeTokensPerGrant`) ships in this task

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k material`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"`

**Dependencies:** T28, T24 (needs `creature-yield.v1.json` in its T23/T24 shape)
**Files:** `data/tuning/creature-yield.v1.json`, `tools/seedsmith/seedsmith/adapters/items/` (layer
generation)
**Size:** M (1 tuning file + generator work)

---

## Checkpoint — Phase 5 / Complete
- [ ] A species-bound set piece above the threshold rung is enhanced with its own species' material,
      end to end
- [ ] All 18 active modules green on their own spec's Success criteria
- [ ] Full regression: Core/Data/Guard/Server suites + seedsmith pytest + `npm test`/`build` all green
- [ ] **Ready for owner sign-off**

---

## Deferred — not scheduled, each with a trigger

| Item | Blocked on | Trigger |
|---|---|---|
| `craft-risk-ladder` Stages 2–4 (durability decay, break, repair-loss) | `deployment-hierarchy` module 7 — zero implementation | Module 7 ships |
| `item-upgrade-tree` (E5, armour successor edge) | `item` module 23 `requirement-profiles` — zero implementation; also needs T18–19 and an 11th `CraftOperation` member | Module 23 ships |
