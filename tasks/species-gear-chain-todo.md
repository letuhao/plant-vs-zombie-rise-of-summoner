# Tasks: `species-gear-chain`

**Plan:** [species-gear-chain-plan.md](species-gear-chain-plan.md) · **Specs:**
`docs/architecture/species-gear-chain/spec-<module-id>.md` · **Map:**
`docs/architecture/species-gear-chain-map.md`

**Revision 2** — see the plan's § Round-2 corrections for what changed and why. Each task cites its
owning spec — read that file's Design/Code style/Testing strategy/Boundaries before starting; this
list gives acceptance criteria, verification and scope, not full design detail.

---

## Phase 1 — foundations

### Sub-checkpoint 1a — ladder and tuning fixes

#### Task T1: `tier-propagation-contract` a — derive `RungCount`, guard the identity
**Description:** `CreatureRarityLadder.RungCount` is a `const` while `All` derives from
`Enum.GetValues`; make `RungCount` derived so they cannot disagree.

**Acceptance criteria:**
- [ ] `RungCount => All.Count`, no `const` left
- [ ] `OneRungAbove`'s throw is proven correct at a *widened* enum width via a test double, not at
      the literal width 10

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LadderDeclaration"`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Creatures/CreatureRarityLadder.cs`,
`tests/FusionRpg.Core.Tests/Creatures/CreatureRarityLadderTests.cs`
**Size:** XS

---

#### Task T2: `tier-propagation-contract` b — remove restatements, guard, split T-5
**Description:** Remove the three in-repo rarity/threat restatements (`EncounterTuning.ThreatRungIds`,
the two Python `RARITY`/`THREAT_BAND` tuples), add a guard that fails on a new one, and split T-5 into
T-5a (actor-magnitude `thetaOffset`, additive only) / T-5b (economy-cost coefficient, each owing its
own §10 row) in `ssot-power-scale.md` §10. Leave `Items/RarityLadder.RungIds` **allowlisted**, not
converted, per the spec's Open question 1.

⚠ **Coordinate with T3:** both touch `tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py`.
Land this task first within this sub-checkpoint.

**Acceptance criteria:**
- [ ] Sites 1–3 read from tuning instead of restating ids; no behaviour change
- [ ] A new test fails if a fourth restatement is added anywhere the guard scans (C# and Python)
- [ ] `ssot-power-scale.md` §10 states T-5a/T-5b explicitly, replacing the single-clause version

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q`
- [ ] `python scripts/audit-magic-numbers.py --summary`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Dungeon/Tuning/EncounterTuning.cs`,
`tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py`,
`tools/seedsmith/seedsmith/adapters/structures/anchor/schema.py`,
`tests/FusionRpg.Guard.Tests/`, `docs/architecture/power/ssot-power-scale.md`
**Size:** M

---

#### Task T3: `threat-band-fill` — score first, default second, with provenance
**Description:** Try `classify()` before falling back to the flat `inferredDefaultRung`; record which
happened; re-validate that no rung is empty by construction after the fill.

**Acceptance criteria:**
- [ ] Every species with a scoreable power seed (`observed`/`stated` basis) carries a **scored**
      `threatBand`, not the flat default
- [ ] Every species carries provenance distinguishing `scored`/`default`/`authored`
- [ ] `SpeciesExpander.cs:31-33`'s exclusion of `threatBand` from the batch-refusal list is
      **preserved** (it is correct), with a comment recording why
- [ ] The fitted deciles are re-validated against the post-fill distribution and the result is
      **reported**, not asserted — no rung is empty by construction
- [ ] The corpus diff is a pure regeneration — no hand edits, proven by re-running the generator and
      getting a byte-identical tree

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_anchor_derive.py tools/seedsmith/tests/test_run_runner.py -q`
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Threat"`
- [ ] `dotnet run --project tools/CreatureQualityReport` — print the histogram **including
      zero-occupant rungs** and the provenance split; never assert either

**Dependencies:** None (⚠ shares `anchor/schema.py` with T2 — land after T2 within this sub-checkpoint)
**Files:** `tools/seedsmith/seedsmith/adapters/creatures/anchor/derive.py`,
`tools/seedsmith/seedsmith/adapters/creatures/run/runner.py`,
`tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py`,
`data/seed/creatures/species/**` (regenerated)
**Size:** M

---

#### Task T4: `socket-allowance-by-kind` — the per-kind table, shift-before-roll
**Description:** Add `socketAllowanceByKind` to `sockets.v1.json` (version 2→3); shift the rarity
window before the roll; enforce all four rejections the derived table needs, not just the inverted
case; pin rows to `catalog_revision`, append-only.

**Acceptance criteria:**
- [ ] Composition order: window shift → roll → base `socketMax` clamp → role ceiling → structural
      bound (throws above `structuralCeiling`)
- [ ] All four rejections load-reject naming kind and rung: **inverted**, **non-overlapping**,
      **non-monotonic**, **negative** — OD4's overlap/monotonicity guarantee is preserved for the
      derived table, not just the authored one
- [ ] `socketAllowanceByKind` rows are append-only, pinned to `catalog_revision` — same contract as
      `rarityGrant`; **no shipped `rarityGrant` row is edited**
- [ ] The inversion's boundedness at `sunwoven`/`almanac` (already at the structural ceiling) is
      **reported**, not hidden
- [ ] An ordinary item's socket count is byte-identical to today for every rung/role

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSocket"`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`

**Dependencies:** None
**Files:** `data/tuning/sockets.v1.json`, `src/FusionRpg.Core/Items/Sockets/SocketTuning.cs`,
`tests/FusionRpg.Core.Tests/Items/Sockets/`
**Size:** M

---

### Checkpoint — sub-group 1a
- [ ] `dotnet test tests/FusionRpg.Core.Tests`, `Guard.Tests` green
- [ ] seedsmith pytest for the touched adapters green
- [ ] No golden re-blessed

---

### Sub-checkpoint 1b — species selection admission + gems

#### Task T5: `CreatureAdmission` — the shared admission policy type (new)
**Description:** One declaring site for the per-context creature-acquisition rule, replacing the two
governing specs' inconsistent claims about who owns it. `spec-wild-species-spawn.md` states *"whichever
of the three modules ships first creates it"*; `spec-wave-species-roll.md`'s own code sample instead
inlines the filter — that inline sample is **superseded** by this task.

**Acceptance criteria:**
- [ ] `CreatureAdmission.ForWave` / `ForWildMap` / `ForDelve` exist in one file
- [ ] `EventOnly` is refused first and unconditionally in every context; `ForWave` refuses
      `CaptureOnly`, `ForWildMap`/`ForDelve` admit it
- [ ] A species carrying `Summonable | EventOnly` (synthetic — none shipped carries both today) is
      refused by every context, proven by test — the population alone must not be the only thing
      making the filter correct

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureAdmission"`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Creatures/CreatureAdmission.cs` (new),
`tests/FusionRpg.Core.Tests/Creatures/`
**Size:** XS

---

#### Task T6: `wave-species-roll` — seeded weighted draw, calling the shared admission type
**Description:** Replace `WaveCatalog`'s `pool[i % pool.Count]` with a seeded weighted draw over a
rarity window, calling `CreatureAdmission.ForWave` — **not** the spec's own inline sample, which T5
supersedes.

**Acceptance criteria:**
- [ ] Draw is seeded and replay-stable, proven across a shuffled catalog order (not just a fixed one)
- [ ] `Band` calls `CreatureAdmission.ForWave` rather than an inline predicate
- [ ] `EventOnly` refused by rule, proven with a synthetic `Summonable | EventOnly` species
- [ ] Waves can draw from all ten rungs; the 486-species `Fused` rung is reachable by configuration
      alone
- [ ] `waves.v1.json` gains `schemaVersion`/`version` (an **add**, not a bump — the file has neither
      field today) plus the window/weight tables; no weight is a `const` in C#
- [ ] The stale four-rung comment at `WaveCatalog.cs:121-126` is corrected or deleted

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~WaveCatalog"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`, `tests/FusionRpg.Data.Tests`
- [ ] Confirm no golden re-blessed

**Dependencies:** T5
**Files:** `src/FusionRpg.Core/Battle/WaveCatalog.cs`, `data/tuning/waves.v1.json`,
`tests/FusionRpg.Core.Tests/Battle/`
**Size:** M

---

#### Task T7: `wild-species-spawn` — sector-weighted roll, calling the shared admission type
**Description:** Replace `SpawnTheUnmade`'s `"normalzombie"` literal with a sector/climate-weighted
roll, calling `CreatureAdmission.ForWildMap`. ⚠ **Ships with the flat `LoamPolicy.UnmadeMemberHp`
as an interim value** — the owner decided (2026-09-13) that wild members should ultimately take their
own species' `P(Θ)`, but that needs `species-magnitude-synth` (Phase 2), so it is **T18b**, a small
follow-on task, rather than a dependency that would pull this whole task into Phase 2.

**Acceptance criteria:**
- [ ] Wild warband composition is rolled, seeded from `(worldSeed, sectorId, turn)`, replay-stable —
      proven twice and across a shuffled catalog order
- [ ] `CaptureOnly` admitted, `EventOnly` refused, each proven with a species carrying that flag
- [ ] Climate weighting is live and tunable; `offClimateMilli` reuses the shipped key name
- [ ] Spawn cadence, occupancy guarding, and the `unmade.spawned` turn-report event are unchanged
- [ ] The weight table is data; no species id and no weight is a `const` in C#
- [ ] A sector whose admissible pool is empty does **not** throw — it falls back to a named,
      documented species rather than crashing a turn
- [ ] The spawn table can express a non-recruitable wild creature, even though none is authored yet
      (the demon/void-beast third category the owner introduced — named only, no content here)
- [ ] Each member's `Hp` field is left as a clearly-named interim value (e.g. a local constant
      referencing `LoamPolicy.UnmadeMemberHp` with a comment pointing at T18b), not hardcoded fresh

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Loam"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~WorldTurn"`
- [ ] `dotnet test tests/FusionRpg.Guard.Tests`, `tests/FusionRpg.Data.Tests`

**Dependencies:** T5
**Files:** `src/FusionRpg.Core/World/Loam/LoamPhases.cs`, `data/tuning/` (new world-spawn domain file),
`tests/FusionRpg.Core.Tests/World/`
**Size:** M

---

#### Task T8: `gem-tier` a — derive tier from `powerBand`, stop hardcoding 1
**Description:** Replace the hardcoded `UnauthoredInsertTier = 1` at the three socket call sites with
`UniqueBudget.TierOfPowerBand(gem.PowerBand)`. **Do not author a `tier` field on a gem entry** —
`entry-shapes.md:81` makes that an `OwnershipViolation`.

**Acceptance criteria:**
- [ ] Every shipped gem reports a tier derived from its own `powerBand`, at all three call sites
- [ ] An unknown `powerBand` is a rejection naming the gem; no silent fallback to tier 1
- [ ] `UnauthoredInsertTier` is reached only for a container the corpus does not carry, and its
      comment says so
- [ ] Every combination whose ingredient families are all present in the gem corpus becomes
      satisfiable, proven by a test computing both sides from the corpus (not a pinned count)
- [ ] The mirror/registry reconciliation test is green: `TierOfPowerBand` agrees with
      `bands.v1.json powerBand.tierMap` key-for-key
- [ ] `insertTiers.count` remains a soft axis — a tier above it is accepted, proven by test
- [ ] No gem entry gains a `tier` field; `ItemSeedValidator`'s `OwnershipViolation` check stays green

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Gem"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** None
**Files:** `src/FusionRpg.Server/ItemCardEndpoints.cs`, `ItemSurfaceEndpoints.cs`, `ItemWorkbench.cs`
(the three hardcoded-1 sites), `tests/FusionRpg.Core.Tests/Items/`
**Size:** S

---

#### Task T9: `gem-tier` b — the upcycle verb and `recipegen` content
**Description:** ⚠ **New task — split out of T8 after the coverage audit found the module's own SC6–8
silently dropped.** Wire `SocketTuning.UpcycleInputPerOutput`'s first production reader, add the
sixth `ItemWorkbench` verb (shaped on `Upcycle` — a stock-to-stock mint, no host instance, no new
`MutationOpKind`), and extend `recipegen` to emit `forge-gem` rows.

**Acceptance criteria:**
- [ ] `SocketTuning.UpcycleInputPerOutput` has a real production reader
- [ ] `ItemWorkbench` exposes a new verb with a POST endpoint, consuming exactly
      `upcycleInputPerOutput` input gems per output gem, idempotent on `(playerId, correlationId)`
- [ ] `forge-gem` recipe rows exist because `recipegen` emits them, and the regenerated diff is
      committed — no seed JSON is hand-edited
- [ ] No new `CraftOperation`/`MutationOpKind` member — `ForgeGem` already exists and is already
      priced; this task uses it, it does not widen either enum

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k recipe`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T8
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`,
`tools/seedsmith/seedsmith/adapters/items/recipegen/`, `data/seed/items/recipes/recipes.json`
(regenerated)
**Size:** M

---

### Checkpoint — sub-group 1b
- [ ] `CreatureAdmission` has exactly one declaring site; T6 and T7 both call it, neither inlines
- [ ] `dotnet test tests/FusionRpg.Core.Tests` green
- [ ] Every shipped gem's tier resolves from `powerBand`; `ItemSeedValidator` green

---

### Sub-checkpoint 1c — risk ladder, durability slice, executors

#### Task T10: `craft-risk-ladder` Stage 1 — potential, always-succeeds
**Description:** Add `craft_potential_max`/`craft_potential_current` columns to `effect_instance`
(idempotent, nullable, lazy backfill); derive from `class`/`rarity`/`tags` with an explicit-or-absent
authored override (never a sentinel). **Two of the module's seven cross-program asks are explicit
preconditions and are filed as part of this task, not skipped:**

- Ask #6 (*"File it; do not build through it"*) — reconcile the authored-override design against
  `deployment-hierarchy` module 7's own Never list (`spec-item-durability-repair.md:408` forbids
  per-item authored derived fields) **before** writing the override mechanism.
- Ask #7 (*"Agree the section layout before Stage 1 builds"*) — since T11 (below) now creates
  `data/tuning/deployment-hierarchy.v1.json` in the same sub-checkpoint, agree its section layout
  with T11 before either reads/writes it.

**Acceptance criteria:**
- [ ] Asks #6 and #7 are filed and resolved (a short written reconciliation, reviewed) **before** the
      override mechanism and the shared tuning file are implemented
- [ ] Every rolled equipment instance carries a derived potential pair; pre-existing instances
      backfill lazily and are never mistaken for exhausted
- [ ] The override is explicit-or-absent; an expected-but-missing one throws naming the base type
- [ ] While potential remains, every craft verb succeeds — proven across the priced operations
- [ ] No decay, no destroy path, no `EnhanceOutcome` member added — this task stops at "assured."
      **This is no longer a permanent state** — T24 (Phase 2) wires the decay path once T11's
      durability storage exists, so the hard-stop risk `AGENTS.md` forbids is bounded to this plan's
      own short Phase-1→Phase-2 gap, not an indefinite external wait

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Potential"`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-test-substrate.ps1`, `.\scripts\guard-actor-hub.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** None (coordinate with T11 on the shared tuning file's section layout)
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`,
`src/FusionRpg.Core/Items/*/PotentialTable.cs` (new), `data/tuning/deployment-hierarchy.v1.json`
(new, shared with T11), `tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T11: `durability-slice` a — storage + derivation + at-zero filter (pulled forward)
**Description:** ⚠ **New task — owner-approved 2026-09-13 pull-forward of a minimal slice of
`deployment-hierarchy` module 7**, built exactly to `spec-item-durability-repair.md` §1/§2/§6, so it
is a subset of that module's own eventual full build, not a parallel invention. **Skips:** field
touch-up (needs `PackGrid`, unbuilt), death-drop decay (needs `corpse-cache`, unbuilt), commander-pouch
parity (D6) — those stay `deployment-hierarchy`'s own future work. File a note in
`deployment-hierarchy-map.md` recording that this slice exists, so that program's own plan does not
duplicate it.

**Acceptance criteria:**
- [ ] `durability_max`/`durability_current` columns added to `effect_instance`, idempotent, nullable,
      lazy-backfilled — exact `enhance_level` precedent
- [ ] `DurabilityTable.Build(baseTypeEntries, tuning)` derives `max` from `class`/`rarity`/`tags`,
      `checked`, widen-first, divide-last, refusing at load on an unknown id, never defaulting
- [ ] The at-zero filter is one `.Where(...)` clause in `MaterializeRolledEquipRuntime`'s assignments
      query — reuses the existing withdraw-on-absence machinery, adds no new Hub gate
- [ ] Stock-backed (`ref_kind != "rolled"`) assignments never populate these columns
- [ ] A note is filed in `deployment-hierarchy-map.md` recording this slice's exact scope and what it
      deliberately excludes

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Durability"`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`, `.\scripts\guard-test-substrate.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** None (coordinate with T10 on the shared tuning file's section layout)
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (`MaterializeRolledEquipRuntime`),
`src/FusionRpg.Core/Items/*/DurabilityTable.cs` (new),
`data/tuning/deployment-hierarchy.v1.json` (new, shared with T10),
`docs/architecture/deployment-hierarchy-map.md` (the filed note)
**Size:** M

---

#### Task T12: `enhance-track-wiring` a — generate the missing `atom.enhance-*` rows
**Description:** `FamilyExpandGen` reads only `affix-families/`, top-directory-only, so
`atom.enhance-*` has zero expanded atom rows. Widen the generator's scope; regenerate.

**Acceptance criteria:**
- [ ] `atom.enhance-*` families produce real rows in `data/seed/atoms/generated/**`
- [ ] `FamilyExpandGen --check` is green; the diff is a pure regeneration
- [ ] Every shipped `enhanceTrack[].family` resolves against the regenerated corpus, asserted as a
      join, not a count

**Verification:**
- [ ] `dotnet run --project tools/FamilyExpandGen -- --check`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EnhanceTrack"`

**Dependencies:** None
**Files:** `tools/FamilyExpandGen/Program.cs`, `data/seed/atoms/generated/**` (regenerated)
**Size:** S

---

#### Task T13: `enhance-track-wiring` b — wire the milestone append
**Description:** `ItemWorkbench.Enhance` passes `Array.Empty<AtomAppend>()`, and
`RpgStore.AppendMutationOpUnlocked` applies `result.Suppressed` but never inserts for
`result.Appended` — a second inert seam. Fix both.

**Acceptance criteria:**
- [ ] An enhancement to an authored milestone level appends a real atom, recorded in the op ledger
      **and** present on `effect_instance_atom` — proven on a live item
- [ ] `EnhancePolicy.IsMilestoneLevel` gains a production caller
- [ ] The ladder is unbounded above — a milestone still grants far past the last authored `atLevel`,
      asserted by test (no hard ceiling)
- [ ] Replay appends exactly once and never re-resolves the family, on `(instanceId, correlationId)`
- [ ] Missing tuning section, unknown family, unknown tier all reject by name — no default anywhere
- [ ] Zero members added to `MutationOpKind`/`CraftOperation` by this task

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T12
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`, `tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T14: `craft-executor-completion` a — the `Forge` executor
**Description:** `ItemWorkbench.cs:189-193`'s "forge cannot run" comment is stale —
`EquipmentContainerBuild.From` already builds the container on the fly. Wire the seventh recipe verb.
**No dependency on `rarity-promotion`** — the owning spec's own header corrects the map's stale claim
that it does; this task needs zero enum members.

**Acceptance criteria:**
- [ ] All 7 authored `forge` recipes execute end to end and mint a real, saved instance with
      `InstanceOrigin.Craft` — computed from the corpus, not a pinned count
- [ ] `ItemWorkbench.cs:189-193`'s comment is corrected, with the evidence cited
- [ ] Zero members added to `CraftOperation`/`MutationOpKind` by this task
- [ ] `imbue` and `forge-gem`-as-a-mint (distinct from T9's upcycle verb) are documented as a
      **content** gap with a named owner (`recipegen`, module 16) and a named path — neither is
      closed by hand-editing seed data

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`

**Dependencies:** None
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`
**Size:** M

---

#### Task T15: `craft-executor-completion` b — `RerollOne`/`RerollAll`
**Description:** Wire the remaining two verbs through the already-built `RerollPolicy`.

**Acceptance criteria:**
- [ ] All authored `reroll-one` and `reroll-all` recipes execute end to end
- [ ] `reroll-all`'s suppress and append both reach `effect_instance_atom`
- [ ] `ItemWorkbench` exposes nine verb methods total, in one consistent shape, each with a POST
- [ ] The debit and the product commit together; replay is idempotent for all three new verbs (this
      task's two plus T14's forge)
- [ ] A contract test makes any future priced-but-unroutable verb fail loudly rather than silently
      (the same class of bug `forge`/`reroll-*` were)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `dotnet test tests/FusionRpg.Core.Tests` (full suite — this task claims none stranded remain)
- [ ] `dotnet test tests/FusionRpg.E2E.Tests`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T14
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`,
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T35: `requirement-profiles-pullforward` a — resolver + evaluator (new)
**Description:** ⚠ **New — owner-approved 2026-09-13 pull-forward of `item` module 23
`requirement-profiles`**, built exactly to its own complete, approved spec
(`docs/architecture/item/spec-requirement-profiles.md`), not a parallel invention. A pure,
deterministic resolver: `(rollSeed, resolverRevision, catalogRevision, tuningRevision, contentTheta,
P(Θ), PowerVector, rarityId, buildFavorPool) → RequirementProfile | RequirementProfileRejection`,
plus `RequirementTrialEvaluator` (pure, unassisted actor-input evaluation). **No schema/persistence
change** — the spec's own v1 scope explicitly excludes it, deferring persistence to module 24.

**Acceptance criteria:**
- [ ] Every valid concrete input resolves to one frozen, replayable profile or one named rejection —
      never a fallback
- [ ] A generated requirement never turns an otherwise-legal assignment into a refusal; an unmet
      trial is observable evaluation data only
- [ ] `RequirementTrialEvaluator.EvaluateRatio` uses `checked` `long` arithmetic
      (`aptitudePoints * 1000 >= grandAllocationPoints * minimumShareMilli`), never the `double`
      convenience reader
- [ ] Rarity selects the distribution-matrix row only; threshold/reserve/cost/period lookups never
      receive rarity after a profile kind is selected
- [ ] Six named `SeededRng.DeriveStream` streams (`item.requirements:v1:profile` etc.), ordinally
      sorted, unique candidates before each draw

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RequirementProfile"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~AptitudeAllocation"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Power"`
- [ ] `python scripts/audit-magic-numbers.py`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Items/Requirements/RequirementProfile.cs` (new),
`RequirementProfileResolver.cs` (new), `RequirementTrialEvaluator.cs` (new),
`tests/FusionRpg.Core.Tests/Items/RequirementProfileTests.cs`
**Size:** M

---

#### Task T36: `requirement-profiles-pullforward` b — tuning + Seedsmith validation (new)
**Description:** `data/tuning/equipment-requirements.v1.json` (rarity × power-band × focus-band
profile weights, maintenance eligibility bands, jackpot weight); Seedsmith build-favor-label
classification, validated and resolved to legal aptitude ids, never a model-supplied magnitude.

**Acceptance criteria:**
- [ ] All balance values come from `equipment-requirements.v1.json`; source contains no balance
      literals
- [ ] Lower power bands give maintenance zero weight; rarity alone cannot create upkeep
- [ ] Invalid Seedsmith classification or tuning blocks acceptance with no partial profile
      (`blocked`, writes no seed — the pipeline's validate-before-accept rule)
- [ ] A model never supplies a magnitude, probability, duration, resolver decision, or runtime input

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RequirementProfile"`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k requirement`
- [ ] `python scripts/audit-magic-numbers.py`

**Dependencies:** T35
**Files:** `src/FusionRpg.Core/Items/Requirements/RequirementProfileTuning.cs` (new),
`data/tuning/equipment-requirements.v1.json` (new), `tools/seedsmith/seedsmith/adapters/items/`
**Size:** M

---

### Checkpoint — Phase 1 (complete)
- [ ] `dotnet test tests/FusionRpg.Core.Tests`, `Guard.Tests`, `Data.Tests`, `Server.Tests` green
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q` green
- [ ] No golden re-blessed, or a separately reviewed commit names which moved and why
- [ ] `deployment-hierarchy-map.md` carries the filed note for the durability-slice pull-forward
- [ ] `item-map.md` carries the filed note for the requirement-profiles pull-forward
- [ ] **Review with owner before Phase 2**

---

## Phase 2

#### Task T16: `ladder-consistency-repair` a — publish `themes.v2.json`
**Description:** Publish v2 with `rarity` re-derived from the current anchor and every other field
byte-identical; migrate `set-charm-gen` and the other v1 readers.

**Acceptance criteria:**
- [ ] `themes.v2.json` has 904 rows, zero retired rarity ids, every non-`rarity` field byte-identical
      to v1 (a closure property, not "84 were fixed")
- [ ] No `themeKey` renamed — the 844 bound set entries are unaffected
- [ ] `set-charm-gen` and the other v1 consumers read v2; v1 stays in place, unretired
- [ ] The refresh is idempotent — a second run produces a byte-identical tree

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k theme`
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures`

**Dependencies:** T2
**Files:** `tools/seedsmith/seedsmith/adapters/creatures/generate_themes.py`,
`data/seed/creatures/_registry/themes.v2.json` (new),
`tools/seedsmith/seedsmith/adapters/creatures/generate_families.py`, `theme_enrich.py`
**Size:** M

---

#### Task T17: `ladder-consistency-repair` b — FE roster sort by ordinal
**Description:** Replace the four sites keying on the retired vocabulary with an ordinal comparator
that surfaces an unknown id instead of defaulting; correct the stale "84 rows" comments.

**Acceptance criteria:**
- [ ] The FE roster sort visibly sorts, proven with species spanning several rungs
- [ ] An unknown rarity id renders a visible "unknown" marker rather than silently sorting to a
      default
- [ ] `npm run build` (`tsc --noEmit`) passes — no site still references a retired id
- [ ] The stale *"themes.v1.json (84 rows)"* comments (a different, unrelated 84 from the old catalog
      size) are corrected where encountered, explicitly not conflated with the retired-id count

**Verification:**
- [ ] `cd web/fusion-rpg-web; npm test`
- [ ] `npm run build`
- [ ] `npm run check:bundle`

**Dependencies:** None
**Files:** `web/fusion-rpg-web/src/features/creatures/rosterSplit.ts`,
`src/lib/bus/creatures.ts`, `src/layers/pacts/PactsLayer.tsx`, `src/pages/CreaturesPage.tsx`
**Size:** M

---

#### Task T18: `species-magnitude-synth` — synthesize containers at import time
**Description:** Synthesize `trait.species-magnitude-*` containers in `ImportCreatureSpecies` from
rows already in `creature_species_magnitude` — upsert, never a committed corpus.

**Acceptance criteria:**
- [ ] A deployed creature specimen binds its species-magnitude container instead of taking the
      fail-closed return — proven end to end
- [ ] Every species with magnitudes has exactly one container; every species without has none
- [ ] Container members reconcile exactly against `creature_species_magnitude`; import is idempotent
- [ ] A magnitude change withdraws the stale binding and rebinds — the existing machinery still holds
- [ ] Zero species-magnitude files committed to `data/`; values are `long` end to end

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Species"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueActor"`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`, `.\scripts\guard-test-substrate.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T3 (rewrites every magnitude — must land first)
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Species.cs`, `tests/FusionRpg.Data.Tests/`
**Size:** M — **do not modify** `RpgStore.UniqueActors.cs:1619-1620`

---

#### Task T18b: `wild-species-spawn` — wire species `P(Θ)` for wild member HP (new)
**Description:** ⭐ **Owner decision, 2026-09-13** (`spec-wild-species-spawn.md` Open question 2).
Replace T7's interim `LoamPolicy.UnmadeMemberHp` with each spawned member's own species-derived
`P(Θ)`, now that `species-magnitude-synth` (T18) has landed containers for every species.

**Acceptance criteria:**
- [ ] A spawned wild member's HP comes from its own species' derived magnitude, not one flat value
      shared by all
- [ ] A species with no magnitude container (should not exist post-T18, but defensively) falls back
      to the same named interim constant T7 used, never a crash
- [ ] `LoamPolicy.UnmadeMemberHp` is either removed or explicitly retained only as that fallback, with
      a comment saying so
- [ ] No new ActorHub composer — the magnitude is read from the container T18 already binds through

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Loam"`
- [ ] `.\scripts\guard-actor-hub.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T7, T18
**Files:** `src/FusionRpg.Core/World/Loam/LoamPhases.cs`, `src/FusionRpg.Core/World/Loam/LoamPolicy.cs`,
`tests/FusionRpg.Core.Tests/World/`
**Size:** S

---

#### Task T19: `delve-species-wiring` a — fix the `CreatureTypeId` collision
**Description:** `SlotFilter.CreatureTypeId` omits the plant offset the other two sites apply,
colliding 102 measured plant/zombie `gameTypeId` pairs. Extract one
`CreatureTypeIdFor(side, gameTypeId)` function; convert all three sites to call it.

**Acceptance criteria:**
- [ ] `CreatureTypeId` is unique across the full catalog, enforced by a test
- [ ] The plant offset lives in exactly one function, called by all three sites
- [ ] A plant and a zombie sharing a `gameTypeId` get different ids

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureTypeId"`

**Dependencies:** None
**Files:** `src/FusionRpg.Core/Creatures/CreatureSpeciesCatalog.cs`,
`src/FusionRpg.Core/Delve/Encounter/SlotFilter.cs`,
`src/FusionRpg.Core/Creatures/Generation/ConcreteSpeciesSeedReader.cs`, `CreatureSpeciesGenerator.cs`
**Size:** S

---

#### Task T20: `delve-species-wiring` b — production caller for `Encounter.Build`
**Description:** Wire the Delve's room resolution to call `Encounter.Build`; surface
`EncounterRefusal`; admit via `CreatureAdmission.ForDelve`.

⚠ Confirm the `E2E.Tests` baseline before attributing any failure to this task (a known pre-existing
cluster exists).

**Acceptance criteria:**
- [ ] A real delve room resolves through `Encounter.Build`
- [ ] `EncounterRefusal` surfaces and is reported, never silently defaulted
- [ ] `offClimateMilli`, `sameSpeciesMaxMilli`, `threatWindow` become live for the first time
- [ ] Encounter selection is deterministic for a given seed

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Encounter"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-actor-hub.ps1` — this module adds a *caller*, not a contributor to the
      grandfathered `BattleStatComposer` fork; must not deepen it (remediation program:
      `FUSE-battle-hub`, owed and unwritten)

**Dependencies:** T3, T5, T19
**Files:** `src/FusionRpg.Server/DelveBattleSessionManager.cs`
**Size:** S

---

#### Task T21: `socket-combat-wiring` a — the `EquipProjector` contribution seam
**Description:** Wire a socketed insert's contribution through `EquipProjector` (never a second
composer — `ApplyEquipProjection` reaps any binding outside its own `desired` set) into `ActorHub`;
file the GG-49 `ContributionSourceIds` grammar amendment.

**Acceptance criteria:**
- [ ] A socketed gem measurably changes a combat number, proven end to end: socket → deploy →
      `ActorHub.ResolveDerivedWithContributions` → the channel moved — **has never been true before**
- [ ] The contribution is attributed (insert, host item, role, socket index) under the amended §8.1
      grammar
- [ ] The contribution survives a second `MaterializeRolledEquipRuntime`
- [ ] No second `*Composer*` introduced anywhere
- [ ] An actor with no sockets resolves byte-identically to today, on both the Hub and battle paths

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~EquipProjection"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"`
- [ ] `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T8
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` (`ApplyEquipProjection`/`EquipProjector`),
`docs/architecture/actor-hub-ssot.md` §8.1, `tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T22: `socket-combat-wiring` b — withdraw, orphan, refusal
**Description:** Unequip/remove withdraws the contribution; an unresolvable insert refuses by name.

**Acceptance criteria:**
- [ ] Unequipping the host, and removing the insert, each withdraw the binding — no orphan
- [ ] `item_socket.insert_instance_id` holds a real instance for every filled socket; `""` no longer
      appears on that column in production
- [ ] An unresolvable insert refuses the socket-insert by name and writes no partial state

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~SocketInsert"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `dotnet test tests/FusionRpg.E2E.Tests`
- [ ] `.\scripts\guard-test-substrate.ps1`, `.\scripts\guard-dal.ps1`, `.\scripts\guard-single-writer.ps1`,
      `.\scripts\guard-funnel-delta.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T21
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs` (correct the stale "equip path reads it"
comment at `:32-33`), `tests/FusionRpg.Data.Tests/Items/`
**Size:** S

---

#### Task T23: `durability-slice` b — workbench repair, the `Repair` op_kind
**Description:** The workbench-only half of `spec-item-durability-repair.md` §5: `ItemWorkbench.Repair`
following `Enhance`'s shape; `RepairPolicy.Resolve(current, max, materialCoverageMilli, tierCapMilli,
rng)`; the eleventh `MutationOpKind` **and** eleventh `CraftOperation` member (`Repair` — already
filed by `deployment-hierarchy-map.md:89`, fulfilled here); the shard-leg-at-high-rungs cost shape
matching `elevate`'s precedent. **No field touch-up** (needs `PackGrid`, unbuilt).

**Acceptance criteria:**
- [ ] `Repair` is added to both `MutationOpKind` and `CraftOperation`, under a reviewed
      `ssot-enhancement.md` §5.3 amendment — the ordinal that was reserved for it
- [ ] Workbench repair resolves via `RepairPolicy.Resolve`; material coverage below what
      `missingFraction` needs falls back to a `tierCapMilli`-capped partial result, never a refusal
- [ ] The destruction chance is rolled **before** computing the restore, on the op's own named
      stream; on destruction, `Disposition` becomes `"destroyed"` and the instance is deleted via the
      existing salvage path — never a new deletion path
- [ ] `operations.repair` prices souls+substrate+catalyst.temper at low/mid rungs, +shard at top
      rungs, matching `elevate`'s shape
- [ ] Replay is idempotent per `correlation_id`, exactly like `Enhance`

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-dal.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T11
**Files:** `src/FusionRpg.Core/Items/Mutation/MutationOp.cs`,
`src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs`,
`src/FusionRpg.Core/Items/*/RepairPolicy.cs` (new), `src/FusionRpg.Server/ItemWorkbench.cs`,
`WorkbenchEndpoints.cs`, `data/tuning/materials.v1.json`, `docs/architecture/item/ssot-enhancement.md`
**Size:** M

---

#### Task T24: `craft-risk-ladder` Stage 2–3 — decay past exhaustion
**Description:** ⚠ **Un-deferred by the durability pull-forward.** Past potential exhaustion, a craft
succeeds and decays `durability_current` by a flat per-mille of `max` — the same unit battle wear
uses (`ceil(durability_max × wearPerBattleMilli / 1000)`), so the two decay sources stay comparable.
Stage 3 (broken, unusable, never destroyed by wear) is already enforced by T11's at-zero filter. Stage
4 (repair-can-destroy) needs no new code here — it is inherited from T23's repair executor.

**Acceptance criteria:**
- [ ] While potential remains, every craft succeeds untouched (T10's behaviour, unchanged)
- [ ] Past exhaustion, a craft succeeds **and** decays durability by `craftWearPerAttemptMilli`, in
      battle wear's unit
- [ ] Durability floors at 0; the item is unusable (excluded from assignments) but **never destroyed
      by crafting** — `Disposition` stays `"owned"`, proven by driving durability to zero purely
      through crafting
- [ ] No destroy outcome is emitted on the craft path; `EnhanceOutcome` gains no member
- [ ] Enhancement's Safe/Risk bands (`spec-enhance-reroll.md` §4) are removed, **or** an explicit,
      filed decision records why they stay — one risk vocabulary, not two

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CraftRisk"`
- [ ] `.\scripts\guard-actor-hub.ps1`, `.\scripts\guard-dal.ps1`, `.\scripts\guard-test-substrate.ps1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T10, T11
**Files:** `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs`, `tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T25: `rarity-promotion` a — item-side rung arithmetic + the `op_kind` ask
**Description:** `Items/RarityLadder` has no `IsTopRung`/`OneRungAbove`/`RungCount`. Build the
item-side rung arithmetic (mirroring `CreatureRarityLadder`'s shape without sharing its type), and
file the promotion `MutationOpKind` amendment — the **twelfth** member, since T23's `Repair` now
claims the eleventh.

**Acceptance criteria:**
- [ ] `Items/RarityLadder` gains `IsTopRung`/`OneRungAbove` over the string-keyed, 10/20-ordinal item
      ladder
- [ ] `MutationOpKind` gains exactly one new member (promotion), under a reviewed
      `ssot-enhancement.md` §5.3 amendment landed in the same commit
- [ ] The same amendment adds the missing `socket-imbue` row to §5.3's table (a pre-existing drift
      found this session — `MutationOp.cs:42-47` mints it, §5.3 never listed it)
- [ ] The `promoteCostSoulsMilli` cost ladder's `ssot-power-scale.md` §10 row is filed (shared filing
      with `species-cost-shaping`'s T32, whichever lands second confirms the other's row)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RarityLadder"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Mutation"`

**Dependencies:** T10, T23
**Files:** `src/FusionRpg.Core/Items/RarityLadder.cs`,
`src/FusionRpg.Core/Items/Mutation/MutationOp.cs`, `docs/architecture/item/ssot-enhancement.md`,
`docs/architecture/power/ssot-power-scale.md`
**Size:** M

---

#### Task T26: `rarity-promotion` b — the executor + the card mark
**Description:** Copy the shipped `ItemWorkbench` verb pattern; surface `promoted_from_ordinal`.
⚠ **Owner decided 2026-09-13 that `species-cost-shaping`'s per-rung multiplier applies to `elevate`
too** (`spec-rarity-promotion.md` Open question 2). That does **not** make T32 (Phase 4) a dependency
of this task: `elevate`'s cost must resolve through the **same shared cost-resolution function** every
other verb uses (never a bespoke elevate-only calculation), so when T32 wires the species multiplier
into that shared function, `elevate` picks it up automatically with no further change here. This
task's own job is only to confirm it did **not** write a private cost path.

**Acceptance criteria:**
- [ ] All ten authored `elevate` recipes execute end to end
- [ ] Promotion is provably additive: every affix survives identically
- [ ] `promoted_from_ordinal` is written, correct across multiple promotions, visible on the card
- [ ] Promotion has no private failure chance (defers entirely to `craft-risk-ladder`); top-rung
      promotion refuses cleanly, never reaching the ladder's throw
- [ ] `ItemWorkbench` exposes this as its own verb, in the shape of the shipped ones, with a POST
- [ ] `elevate`'s cost resolves through the shared cost-resolution function, not a bespoke
      calculation — the hook T32 needs later already exists

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`

**Dependencies:** T25
**Files:** `src/FusionRpg.Server/ItemWorkbench.cs`, `WorkbenchEndpoints.cs`,
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

### Checkpoint — Phase 2
- [ ] All ten authored `elevate` recipes execute end to end (T26)
- [ ] A socketed gem measurably changes a combat number, proven through `ActorHub` (T21–22)
- [ ] `craft-risk-ladder` is complete (Stages 1–4, T10/T24) — the hard-stop risk from revision 1 is
      resolved
- [ ] `guard-actor-hub.ps1`, `guard-dal.ps1` green — no second composer anywhere in Phase 1–2
- [ ] `themes.v2.json` published; 844 bound set entries unaffected
- [ ] **Review with owner before Phase 3**

---

## Phase 3

#### Task T27: `set-species-binding` a — schema + forward emission
**Description:** Add `speciesId`/`setClass` to the set entry schema; extend `set-charm-gen` to emit
both going forward.

**Acceptance criteria:**
- [ ] Every newly generated set entry carries `speciesId` (a real id or explicit absent) and
      `setClass`
- [ ] `SetEvaluator` behaviour is unchanged (class-agnostic today, stays so)

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_items_adapter.py -q`
      (confirm this test's pre-existing failure baseline first)
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Set"`

**Dependencies:** T16
**Files:** `tools/seedsmith/seedsmith/adapters/items/setgen/`
**Size:** S

---

#### Task T28: `set-species-binding` b — deterministic backward repair + runtime import
**Description:** Repair the existing 910 entries, reading `themes.v2.json`'s `speciesId` field as the
primary join (case-normalising cross-check against the `themeKey` suffix parse), persisting the
runtime catalog's spelling. ⚠ **Expanded scope after the coverage audit found the module's own
Objective — "make the field queryable" — needs the C# runtime side, which the original task list
omitted:** the `FusionRpg.Data` set-import path, and `ItemSeedValidator`'s closure check.

**Acceptance criteria:**
- [ ] Every `creature.*` themeKey resolves, or the run fails naming the unresolved key
- [ ] `build.*`/`theme.*` keys (no species) get an explicit absent value, never a guess
- [ ] `speciesId` is persisted in the **runtime catalog's spelling** (lower-case, matching
      `CreatureSpeciesCatalog`), not the PascalCase generated-corpus spelling
- [ ] `FusionRpg.Data`'s set import reads and persists the new field — the field is queryable at
      runtime, not just on disk
- [ ] `ItemSeedValidator` asserts every non-absent `speciesId` resolves in `CreatureSpeciesCatalog`
- [ ] No set bonus magnitude changes — proven by a diff of evaluated bonuses before/after
- [ ] The repair is idempotent and deterministic (byte-identical on a second run); no model call
      anywhere on this path

**Verification:**
- [ ] `cd tools/seedsmith; python -m seedsmith check data/seed/items --adapter items`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSet"`

**Dependencies:** T27, T16
**Files:** `tools/seedsmith/seedsmith/adapters/items/` (new repair pass), `data/seed/items/sets/**`
(regenerated), `src/FusionRpg.Data/Sqlite/RpgStore.ItemSets.cs`, `tools/ItemSeedValidator/Program.cs`
**Size:** M

---

#### Task T29: `creature-drop-tables` a — E2 material-shelf credit
**Description:** A drawn `Material` entry is drawn and the grant emitted, but the shelf is never
credited — 2–3 Data files + tests, including the `PersistLootUnlocked`/credit-helper `string`/`long`
player-id type mismatch.

**Acceptance criteria:**
- [ ] A drawn `Material` entry credits the shelf, proven end to end
- [ ] The player-id type mismatch is resolved (one type, used consistently on this path)
- [ ] The shipped drop-rate floor (`drop-rate-floor.v1.json`) is consumed; no second floor introduced

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Loot"`
- [ ] `.\scripts\guard-dal.ps1`

**Dependencies:** T18
**Files:** `src/FusionRpg.Core/Items/Drops/LootMintAt.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.*` (`PersistLootUnlocked` + credit helpers),
`tests/FusionRpg.Data.Tests/Items/`
**Size:** M

---

#### Task T30: `creature-drop-tables` b — E1 the ninth source kind + paired arm
**Description:** Add a ninth `source_kind` for a creature kill, its authored tables in
`data/seed/loot/` (the **runtime** corpus — not `droptablegen`'s output tree,
`data/seed/items/drop-tables/`), and the paired `LootCorrelation.Derive` arm. ⚠ **Scope decided
2026-09-13 (owner):** authored tables carry **both** `Material` and `Equipment` entries — not
materials-only as the spec originally recommended. `DropEntryKind.Equipment` is already a built mint
arm (`LootMintAt.cs:77-87`); no new equipment-roll mechanism is built here.

**Acceptance criteria:**
- [ ] A ninth `source_kind` id exists in the closed vocabulary, with authored tables in
      `data/seed/loot/` carrying both `Material` and `Equipment` entries
- [ ] The matching `LootCorrelation.Derive` arm exists
- [ ] Kill attribution is keyed on a source the expedition/delve/wild path can supply — not the lawn,
      where `KillerActorKey` carries nothing today
- [ ] No new material id; the closed 27-id vocabulary is untouched
- [ ] No new `DropEntryKind` member — `Equipment` already exists and is already a built arm

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~DropTable"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Loot"`

**Dependencies:** T18, and at least one of {T6, T7, T20}
**Files:** `src/FusionRpg.Core/Items/Drops/DropTableValidator.cs`, `LootPipeline.cs`,
`data/seed/loot/**` (new tables)
**Size:** M

---

#### Task T31: `creature-drop-tables` c — E3a shard-by-rung at plan time, shared with the equipment arm
**Description:** Replace `isBoss ? ShardRare : ShardCommon` with a lookup on the killed species' own
rarity rung, computed at plan time from the planned wave's species — preserving manifest determinism.
**The same rung-derived `theta` input feeds `Equipment`-kind entries' `thetaContent`** (T30's scope
addition) — one derivation, two consumers, not two mechanisms.

**Acceptance criteria:**
- [ ] The shard a creature yields follows its rung, not `isBoss`
- [ ] An `Equipment`-kind creature drop mints via `LootMintAt.cs:77-87` with `thetaContent` derived
      from the same species-rung input as the shard — proven with a real minted, saved instance
- [ ] Expedition manifests remain byte-identical for a given seed, before and after — a regression
      test, not an inspection
- [ ] A per-species yield distribution report exists and is printed, covering both entry kinds (the
      "no species strictly dominates" half is `roster-metrics`' obligation at the world-stage hunt
      module, not this one's)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition"`

**Dependencies:** T30
**Files:** `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs`,
`tests/FusionRpg.Core.Tests/Expeditions/`
**Size:** S

---

#### Task T37: `item-upgrade-tree` a — the executor, armour successor edge
**Description:** ⚠ **Un-deferred 2026-09-13** — see T35/T36. An item-typed cost line, a
consume-and-replace `output_kind`, the twelfth `MutationOpKind` member and eleventh
`CraftOperation` member (ask-first, closed enums — file both amendments in the same commit), the
armour class-ladder successor edge, and the two mandatory rules affix-pool legality demands: the
successor's affix set must be legal on its own pool (refuse, never drop or re-tier), and the implicit
change is presented on the card **before** the input is consumed.

**Acceptance criteria:**
- [ ] An armour piece upgrades to the next rung within its own `(ladder, frame)`, consuming the input
- [ ] Every affix carries across identically **and** is legal on the successor's `affix_pool_tag** —
      an illegal combination refuses, it does not launder or re-tier
- [ ] The implicit change is shown on the card before the input is consumed — refuse-never-warn,
      matching the requirement-refusal posture
- [ ] A requirement the owner cannot meet (via T35/T36's `RequirementTrialEvaluator`) refuses, and
      consumes nothing — proven byte-identically
- [ ] The card presents the result as a chassis carrying its own identity, not the named successor
- [ ] Hub bindings against the consumed instance are withdrawn via the existing machinery
- [ ] No reroll, no private failure chance (defers to `craft-risk-ladder`), no hard ceiling

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemUpgrade"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"`
- [ ] `dotnet test tests/FusionRpg.Server.Tests`
- [ ] `.\scripts\guard-dal.ps1`, `.\scripts\guard-actor-hub.ps1`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T26, T24, T36
**Files:** `src/FusionRpg.Core/Items/Mutation/MutationOp.cs`,
`src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs`,
`src/FusionRpg.Core/Items/*/ItemUpgradePolicy.cs` (new), `src/FusionRpg.Server/ItemWorkbench.cs`,
`WorkbenchEndpoints.cs`, `docs/architecture/item/ssot-enhancement.md`
**Size:** M

---

#### Task T38: `item-upgrade-tree` b — `successorOf` for weapon/offhand/jewel
**Description:** A new authored `successorOf` field per base type, read by the same executor as an
alternative to the class-ladder lookup — **never derived from the class ladder itself**, since that
ladder is a style axis for these three roles. **Zero `successorOf` values are authored here** — a
separate content pass, named in `item-map.md`'s filed note.

**Acceptance criteria:**
- [ ] A weapon/offhand/jewel base type carrying `successorOf` upgrades via the same executor T37
      built, with the same affix-legality and implicit-change rules
- [ ] A base type with **no** `successorOf` refuses by name — never derived from
      `blade→blunt→launcher` or any other class-ladder rung
- [ ] Standard (commander gear) stays explicitly refused; it is not a progression ladder
- [ ] The registry change is purely additive; `minCompatibleVersion` semantics hold
- [ ] `ItemSeedValidator` green with zero `successorOf` rows authored — the field's absence is the
      expected, correct state for every non-armour base type today

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemUpgrade"`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T37
**Files:** `data/seed/items/_registry/classes.v{n}.json` or a new base-type field registry,
`src/FusionRpg.Core/Items/*/ItemUpgradePolicy.cs`
**Size:** S

---

### Checkpoint — Phase 3
- [ ] Every set entry carries `speciesId`/`setClass`, queryable at runtime; `ItemSeedValidator`
      green; no set bonus magnitude changed
- [ ] A drawn `Material` entry credits the shelf end to end; expedition manifests unchanged for a
      fixed seed
- [ ] **Review with owner before Phase 4**

---

## Phase 4

#### Task T32: `species-cost-shaping` — the per-rung multiplier, gated
**Description:** Add `speciesCostMultiplierMilli` + `speciesCostThresholdRung`, applied **beside**
(never folded into) `costBandMultiplierPerMille`, through the **one shared cost-resolution function**
every priced verb already calls (T26 confirms `elevate` is one of them). ⚠ **Owner decided 2026-09-13:
`elevate` is included** among the verbs the species multiplier applies to (`spec-species-cost-shaping.md`
Open question 3) — this task's per-verb tunable table must carry an `elevate` row, not omit it as an
oversight.

**Acceptance criteria:**
- [ ] A set piece whose species sits at or above the threshold rung costs more, by the tunable
      per-rung multiplier — **including on `elevate`**, proven with a promoted species-bound piece
- [ ] Below the threshold, and for species-less pieces, costs are byte-identical to today
- [ ] `MaterialCorpusTests`' band-multiplier mirror assertion is still green, proving nothing was
      folded in
- [ ] Missing section, unknown rung, out-of-range multiplier all reject at load
- [ ] Set bonus evaluation is provably unchanged
- [ ] No new material id, no new spend class, no new craft verb
- [ ] The shared `ssot-power-scale.md` §10 row is filed (see T25's note — whichever of the two lands
      second confirms the other's row exists)

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostClass"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~MaterialSpend"`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `python scripts/audit-magic-numbers.py --targets M1`
- [ ] `python scripts/audit-overflow.py`

**Dependencies:** T28
**Files:** `data/tuning/materials.v1.json`, `src/FusionRpg.Core/Items/Materials/MaterialTuning.cs`,
`tests/FusionRpg.Core.Tests/Items/Materials/`
**Size:** M

---

### Checkpoint — Phase 4
- [ ] Below the threshold rung, costs are byte-identical to pre-change
- [ ] `MaterialCorpusTests`' band-multiplier mirror assertion still green
- [ ] **Review with owner before Phase 5**

---

## Phase 5

#### Task T33: `species-materials` a — file the sixth `MaterialClass`, fix the `27` pins
**Description:** File the sixth `MaterialClass` (`Trophy`, provenance) as an ordinary single-owner
ask-first change against `ssot-materials-crafting.md` §3.1 — **not** a contested slot (that claim was
a misreading, corrected in the map). Replace the three live `27` pins in `materialgen` (two of which
are module-level `assert`s that hard-crash seedsmith on import) with a reconciliation canary.

**Acceptance criteria:**
- [ ] The sixth `MaterialClass` is added under a filed, answered ask, with its question stated in its
      doc comment — the "which of these five is unanswerable for my spend?" test
- [ ] `CostClassMatrix.Allows` has a narrow arm (only the improve verbs spend it); every operation
      still resolves without throwing
- [ ] `MaterialCatalog.All` still generates from declared shapes; `ClassOf` still throws outside the
      set
- [ ] `materialgen/vocab.py:120`/`:124` and `test_recipes_gen.py:193` are replaced with
      `len(ISSUABLE) == len(MaterialCatalog.All)` — no literal `27` remains as a pin
- [ ] No new `ContentRuleViolated` code added; the closed 33-code list is unchanged

**Verification:**
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostClass"`
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k material`
- [ ] `dotnet run --project tools/ItemSeedValidator`

**Dependencies:** T32
**Files:** `src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs`, `CostClassMatrix.cs`,
`tools/seedsmith/seedsmith/adapters/items/materialgen/vocab.py`,
`tools/seedsmith/tests/test_recipes_gen.py`,
`docs/architecture/item/ssot-materials-crafting.md`
**Size:** M

---

#### Task T34: `species-materials` b — the general + species-unique layers
**Description:** Generate the two material layers in `creature-yield.v1.json` — **confirm this file
was not already created independently by T30** before adding to it. D5's deterministic exchange ships
with this task (already decided; not reopened).

**Acceptance criteria:**
- [ ] A species-bound set piece above the threshold rung is enhanced with its own species' material,
      end to end — the decision this whole initiative exists to deliver
- [ ] Below the threshold, and for species-less pieces, nothing changes
- [ ] Per-species count is tunable in 1–2, settable to 0 for general creatures; general layer carries
      the volume
- [ ] One `creature-yield.v1.json`, shared with `creature-drop-tables` (T30) — not a second file
- [ ] D5's deterministic exchange (`exchangePriceSouls`/`exchangeTokensPerGrant`) ships in this task
- [ ] Set bonus evaluation is provably unchanged

**Verification:**
- [ ] `$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k material`
- [ ] `dotnet run --project tools/ItemSeedValidator`
- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"`
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~MaterialSpend"`

**Dependencies:** T33, T31
**Files:** `data/tuning/creature-yield.v1.json`, `tools/seedsmith/seedsmith/adapters/items/` (layer
generation)
**Size:** M

---

## Checkpoint — Phase 5 / Complete
- [ ] A species-bound set piece above the threshold rung is enhanced with its own species' material,
      end to end
- [ ] An armour piece upgrades cleanly (T37); a weapon/offhand/jewel base type without an authored
      `successorOf` refuses by name, not by deriving from its class ladder (T38)
- [ ] All 19 modules green on their own spec's Success criteria — **the deferred list is empty**
- [ ] Full regression: Core/Data/Guard/Server/E2E suites + seedsmith pytest + `npm test`/`build` all
      green
- [ ] `deployment-hierarchy-map.md`'s filed note for the durability slice is current
- [ ] `item-map.md`'s filed note for the requirement-profiles pull-forward is current
- [ ] **Ready for owner sign-off**

---

## Deferred

None. Both of this initiative's external blockers (`deployment-hierarchy` module 7 durability;
`item` module 23 `requirement-profiles`) were resolved by pulling a minimal, already-designed slice
of each forward — see T11/T23 and T35/T36. The only remaining content gap is authoring `successorOf`
values for ~800 non-armour base types (T38 builds the mechanism, not the content) — named in
`item-map.md`'s filed note, owed to whichever program picks it up next.
