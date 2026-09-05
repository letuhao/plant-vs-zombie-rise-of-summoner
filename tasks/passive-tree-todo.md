# Task list — passive tree

Plan: [passive-tree-plan.md](passive-tree-plan.md). Map:
[docs/architecture/passive-tree-map.md](../docs/architecture/passive-tree-map.md).
Each task names its spec; **read that spec's section before starting** — this list is the order and
the acceptance bar, not a substitute for the spec.

**Rewritten 2026-09-05** from three coverage audits —
[20-plan-coverage-wave0.md](../docs/research/passive-tree/20-plan-coverage-wave0.md),
[21-plan-coverage-data.md](../docs/research/passive-tree/21-plan-coverage-data.md),
[22-plan-coverage-content.md](../docs/research/passive-tree/22-plan-coverage-content.md).
They found 149 requirements with no delivering task and 16 acceptance criteria that contradicted the
spec they cited. The previous 27-task list was replaced rather than patched.

**Standing verification for every task** (the Definition of Done, not repeated per task): the module's
tests green · `dotnet build` clean · `guard-single-writer`, `guard-secondary-no-unity`,
`guard-funnel-delta`, `guard-dal` pass · `guard-power` where a magnitude is touched ·
`python scripts/audit-overflow.py` shows 0 critical · `python scripts/audit-magic-numbers.py` shows 0
M1/M2 attributable to the task. **For any task touching `web/fusion-rpg-web`, add:** `npm run build`,
`npm run check:bundle`, `npm test -- volumeMatrix diffStateMatrix fourStatesMatrix vocabularyGuard
magnitudeGuard bandGuard xyflowGuard`, `npm run test:e2e` (see I1, which builds that suite).

**Standing rule:** cite `Battle*` files **by symbol, never by line** (R9) — `battle-tempo` is editing
them, and seventeen citations drifted twice during the spec round.

**Standing rule:** `guard-power.ps1` cannot detect a missing `ssot-power-scale.md` row for anything in
this program — its method pattern (`guard-power.ps1:74`) keys on a parameter named `level`, `lvl` or
`index`, and this program's are `t`, `count`, `nodesOwned`, `soulLevel` and `thetaActor`. A green
guard is not evidence that D8, E6 and G8 have been done.

---

## Phase A — foundations

Three files that do not exist and that eleven downstream tasks read. None of them is design work; all
three are blocked on nothing.

### A1: `data/tuning/passive-tree.v1.json` and its loader
**Spec:** `spec-tree-plan.md` §Tunables; `spec-tree-catalog.md` §5; `spec-tree-state.md` §8;
`spec-tree-binder.md` §3.6; `spec-tree-resolve.md` §8; `spec-gate-counters.md` §7 P3, §13.
**Description:** The program's one tuning file under ruling R2's canonical names, each key carrying its
unit, plus `PassiveTreeTuning` and its typed loader. Verified absent 2026-09-05: `data/tuning/` holds
no `passive-tree*` file. Keys: `tierLadder.reqScalePoints`, `budget.treeTotalPoints`,
`budget.branchSplitMilli`, `treeShareMilli`, `potency.maxNodeShareMilli`, `potency.minTerminalWidth`,
`potency.bandEdgesMilli[]`, `mechanism.rampStartMilli`, `mechanism.rampEndMilli`,
`archetype.rewardSpreadMaxRatioMilli`, `exclusion.targetShareMilli`, `archetypeAssignment`,
`designTarget.thetaAllIn`, `concentration.fmaxMilli`, `concentration.wMilli`,
`soulTrack.thetaPerSoulLevelMilli`, `unlockCost.firstPoints`, `unlockCost.stepPoints`, and the whole
`gateCounters` block. **T4 applies from the moment the file exists: never hand-edit, republish `v{n+1}`.**
**Acceptance:**
- [ ] Every key loads through a typed view under the standard `schemaVersion` / `version` / `_meta`
      header; a **missing** key is a load rejection naming it, never a built-in default (T5)
- [ ] `soulTrack.thetaPerSoulLevelMilli = 1000` gives `Ws = 1`, pinned by test; `1` gives a thousandth
- [ ] `gateCounters.statusMasteryRatePoints` defaults to the Aspect rate, and a divergence is refused
      without a `gateCounters.rateDivergenceWhy`
- [ ] `budget.treeTotalPoints` and `treeShareMilli` carry an `UNMEASURED` marker and a `_note` (D42),
      and no superseded spelling appears anywhere in code, config or a fixture: `Fmax`, `w`, `Ws`,
      `concentration.fmax`, `concentration.w`, `ladder.kPoints`, `tierLadder.k`, `soulThetaWeight`,
      `mechanism.floorMilli`, `mechanism.capMilli`, `nodePotencyCeiling`, `unlockCost.first`,
      `passive-tree-gen.v1.json`
**Verification:** a fixture with one key stripped fails naming that key; a text test asserts no
superseded spelling; `audit-magic-numbers.py` shows no M1/M2 in the passive-tree namespace.
**Depends on:** none. **Scope:** M. **Files:** `data/tuning/passive-tree.v1.json`,
`src/FusionRpg.Core/PassiveTree/State/PassiveTreeTuning.cs`.

### A2: `data/tuning/passive-tree-targets.v1.json`
**Spec:** `spec-tree-plan.md` §8; `spec-tree-language.md` §4.3; `spec-tree-review.md` §6.3;
`spec-species-tree.md` §3.2, §5.3.
**Description:** The declared target file, shaped like `data/tuning/demon-roster-targets.v1.json` —
integer per-mille throughout, a `_note` recording provenance, **no axis listing its own members**. It
holds the six quota axes' weights, `legitimateSkew` (empty), the gate thresholds,
`exclusion.targetShareMilli`, `speciesUniqueAffixMin`, the tier-2/3 sample sizes and the acceptance
numbers. Every value is a **starting value** and says so. `tree-plan` §8's quota algorithm cannot run
without it.
**Acceptance:**
- [ ] Aptitudes read from `data/seed/aptitudes/roster.json`, elements from
      `data/seed/elements/roster.json`, statuses from the status mirror (A3) — a thirteenth aptitude
      changes the grid by construction, with no edit here
- [ ] The `_require`/`_validate` load path **refuses to substitute a default**; a missing key is an
      error at load, never a silent zero
- [ ] `legitimateSkew` starts empty and a row without a `_why` is refused
- [ ] Every gate named by `spec-tree-language.md` §7 has a threshold row, or is listed by
      `missing_thresholds()` — no gate is silently unthresholded
**Verification:** the loader raises on a stripped key; `missing_thresholds()` lists every gate with no
number.
**Depends on:** none. **Scope:** S. **Files:** `data/tuning/passive-tree-targets.v1.json`.

### A3: The two roster mirrors `tree-plan` owes
**Spec:** `spec-tree-plan.md` §6, §9 items 1–2, §Reproducibility.
**Description:** `data/seed/statuses/roster.json` (21 statuses) and `data/seed/atoms/vocabulary.json`
(7 attach points / 16 kinds / 13 triggers, 11 authorable) do not exist — verified 2026-09-05:
`data/seed/statuses/` is absent and `data/seed/atoms/` holds only `fx-*.json`, `generated/` and
`trait-critical-hunter.json`. Same `--check`/`--emit` contract as `tools/ElementEnumGen`, so drift
between a mirror and the shipped registry is a failing check rather than a stale file.
**Acceptance:**
- [ ] Both mirrors emit, and `--status-check` / `--atom-vocab-check` exit non-zero on drift
- [ ] Every count is read and counted, never typed — `roster_counts_are_read_never_typed` greps this
      module's source for a bare `12`, `6`, `21`, `53`, `16`, `13`, `7`
- [ ] A missing mirror is `EXIT_CANNOT_RUN` naming the file, never an empty axis
**Verification:** delete a mirror in a temp tree; the planner exits 2 naming it.
**Depends on:** none. **Scope:** S. **Files:** `tools/ElementEnumGen/`, `data/seed/statuses/`,
`data/seed/atoms/`.

---

## Phase B — one trait, end to end

The vertical slice: one hand-authored tree from planner to a changed number in a battle, at 1/40th of a
tree's width. If the coefficient math, the id scheme or the resolver read is wrong, it is wrong here,
at a cost of one tree. Phase C and phase D finish each module behind it.

### B1: `tree-plan` emits one tree, as a seedsmith adapter
**Spec:** `spec-tree-plan.md` §2–§4, §6, §Node ids, §Project structure.
**Description:** The deterministic planner for a single tree (`Might`, `broad-and-flat`): the tier
ladder, the budget column, the archetype width vector, the closed property vocabulary, and `nodeKey`
minting. **The planner is a seedsmith adapter, not a new tool** — §Project structure opens with that
sentence and every command is `python -m seedsmith trees plan …`. A separate tool grows a second copy
of `largest_remainder_count`, which is the integer algorithm §8 depends on.
**Acceptance:**
- [ ] `--emit` produces `data/seed/passive-tree/plan/might.v1.json`: 40 nodes, 20 per branch, rootless,
      ids `skill.<treeSlug>-<branch>-t<tier>-<nodeKey>`
- [ ] The tier budget column sums to exactly 1000‰ with zero residual, and `W/req = b/5` at all ten
      tiers; `R-G0` exits 3 on any `ladder.gateCurrency` other than `aptitudePoints`
- [ ] The **§6 thirteen-axis property vocabulary** is emitted in the plan, with every count read from
      the A3 mirrors and no hardcoded roster count
- [ ] `--emit` **refuses** to mint over an existing `nodeKey`, and reads existing keys back (R3)
**Verification:** `python -m seedsmith trees plan --check` on the emitted plan is green; a
hand-corrupted budget column fails it.
**Depends on:** A1, A2, A3. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/plan/` (`ladder.py`, `archetypes.py`, `vocabulary.py`,
`ids.py`, `emit.py`), `data/seed/passive-tree/`, `tools/seedsmith/tests/test_tree_plan_*.py`.

### B2: `tree-catalog` — the record and the load path
**Spec:** `spec-tree-catalog.md` §2.1–§2.5, §3.
**Description:** `TreeRecord`, `NodeRecord` and `NodeAtom`, the id grammar, and a load path that
refuses rather than clamps. **`gateQuantity` is stored either way** — §2.1 and D37: a tree naming
`element_mastery` or `status_applied.<id>` is *waiting*, not orphaned, and is never disabled for it.
The potency check is `node.budgetShareMilli > potency.maxNodeShareMilli`, both ‰ of one branch; the
`kMicro`-versus-ceiling form §2.5 replaced is a dimensional error and must not be built.
**Acceptance:**
- [ ] The record round-trips one hand-authored tree; `affixIds[]` is 1..3, `kMicro` is `long`,
      `nodeClass`, `unitClass`, `budgetShareMilli` and `whenJson` are carried verbatim
- [ ] A tree whose `gateQuantity` has no producer **loads and stays enabled**, flagged as waiting
- [ ] Load refuses: an id violating the grammar (no dot in the body), and a
      `budgetShareMilli > potency.maxNodeShareMilli` — never a `kMicro` comparison
- [ ] Unknown-id rejection happens **once at import**, never per actor load
**Verification:** load tests for each refusal; a legacy fixture with a retired node renders red rather
than throwing; a fixture tree gated on an unbuilt counter loads clean.
**Depends on:** B1. **Scope:** M. **Files:** `src/FusionRpg.Core/PassiveTree/` (new),
`data/seed/passive-tree/`.

### B3: `PowerLadderKMicro`, and `AtomCompiler`'s result widened to `long`
**Spec:** `spec-tree-binder.md` §3.5, §5.3, §7; `spec-tree-state.md` §7.
**Description:** Three lines beside `PowerLadderKMilli` (`ValueSpec.cs`) plus its read in
`AtomCompiler`. **Not cosmetic:** at per-mille, `gated-deep` stores `kMilli = 0` for **12 of 40 nodes**
and `broad-and-flat` for 6 — silently inert, in the shallow tiers every build buys first. Widen the
compiler's **result** from `int` to `long` in the same change; it moves the first refusal from `Θ`
103,557 to ≈214,748,300 and costs one cast.
**Acceptance:**
- [ ] `PowerLadderKMicro` divides by 1_000_000, widening before the multiply, throwing on overflow
- [ ] A tier-1 `gated-deep` node stores non-zero and round-trips within 0.1%
- [ ] `AtomCompiler`'s result is `long`; a magnitude at `Θ` 150,000 resolves rather than refusing
- [ ] `PowerLadderKMilli` is untouched and its existing consumers are unaffected
**Verification:** a test asserting no shipped archetype produces a zero coefficient at any tier;
`audit-overflow.py` clean.
**Depends on:** none (parallel with B1). **Scope:** S. **Files:**
`src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs`, `AtomCompiler.cs`, tests.

### B4: `tree-binder` binds one node's coefficient
**Spec:** `spec-tree-binder.md` §1, §3.1–§3.4, §7.
**Description:** Budget share → stored `kMicro`, reading the plan's `budgetShareMilli` (**not**
`tierWeight`/`weightTotal`, which R4 deleted — reading the wrong one is the 3.25× defect §3 documents).
A node is 1..3 affixes inside a `skill` container; `AffixComposer` maps affix → atom rows.
**Acceptance:**
- [ ] One node's `kMicro` is reproducible from the plan alone: one division, round half away from zero
      through the shipped helper
- [ ] `CoefficientBinder`'s source contains no `tierWeight`, `weightTotal` or `w[t]` — a source-shape
      test, because a value test cannot see this defect
- [ ] A conversion node is **refused** with the 17th-kind reason, not silently bound
**Verification:** the worked example in §3.4 reproduces exactly — share 45 → 3,038, the sibling at
46 → 3,105.
**Depends on:** B1, B2, B3. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/Binding/AffixComposer.cs`, `CoefficientBinder.cs`, `BoundNode.cs`,
`tools/TreeBinder/`.

### B5: `tree-state` stores one actor's allocation
**Spec:** `spec-tree-state.md` §1, §2, §6.
**Description:** `rpg_tree_node_state`, sparse, inputs only, batch-first read API. Sparsity means
**only non-zero entries persist** — a row exists for every node the actor owns, including one owned at
`soul_level = 0`, which §1.1 names as a real state with its own test.
**Acceptance:**
- [ ] Row presence means owned; no `owned` column; **a node owned but never soul-levelled persists a
      row with `soul_level = 0`**, and nodes the actor does not own have no row
- [ ] `cost(N) = first + (N−1)·step` derives budget and spend **on read**; no stored balance
- [ ] `LoadTreeStateBatch` serves a six-actor squad in one query, one lock, one connection
- [ ] Unlock price derives from the owned-node count, so re-buying the same set costs the same
**Verification:** `owned_with_zero_souls_persists`; the order-independence lemma as a named test; a
respec round-trip costs identically regardless of purchase order.
**Depends on:** B2. **Scope:** M. **Files:** `src/FusionRpg.Data/Sqlite/RpgStore.PassiveTree.cs` (new).

### B6: `tree-resolve` folds one trait into combat
**Spec:** `spec-tree-resolve.md` §2.1, §2.2, §3, §5.1.
**Description:** Tree atoms fan into the existing `AtomDerivedSubsystem` via `boundDerivedAtoms`; tier
gates read **aptitude points**, never the skill wallet; `H` reads the final allocation. The report
shape lands here so I6 has a `gateState` to read (`wired | unproduced`), filled properly by D5.
**Acceptance:**
- [ ] An allocated trait changes a `combat.*` channel on a resolved actor, through no new subsystem,
      no new order band and no eviction of the existing three
- [ ] `req(t)` reads the actor's aptitude allocation and the **catalog's** authored depth, never a
      literal; item bonuses cannot move it
- [ ] `F ∈ [1, Fmax]` and `H` is order-independent — both **named tests**, not prose
- [ ] `TreeResolveReport` exists and carries `gateState` read from the catalog, never inferred from a
      zero
**Verification:** two actors with the same final allocation bought in different orders resolve
identically; `Tier_gate_reads_the_catalog_depth_not_a_literal`.
**Depends on:** B4, B5. **Scope:** M.

### ✅ Checkpoint B — the spine
- [ ] A trait allocated on an actor changes a number in a battle, end to end
- [ ] The coefficient reproduces from the plan; no archetype stores a zero
- [ ] Owner review before phase C

---

## Phase C — the plan corpus, the catalog and the store completed

Phase B proved one tree and one actor. These are the properties that only exist across the corpus, the
migration rules that make a live game tunable, and the store's own hardening.

### C1: The corpus-level plan invariants — `C1`, `R-A1`, `R-M1/M2`, `P-1/P-2`
**Spec:** `spec-tree-plan.md` §3, §3.1, §3.2, §4, §5.1, §5.2, §Testing.
**Description:** B1 proves one tree. These exist only across the corpus and across the ladder, and the
spec says the endpoint check structurally cannot catch them. Includes the mechanism ramp
`archetypes[].mechNodes[]` — the interface `tree-language` consumes as an exact per-tier count — and
registering `PassiveTree/TreeEqualValue` beside `QuotaDrift` and `CellOccupancy` so `tree-review` reads
it through the same registry.
**Acceptance:**
- [ ] `C1`: `Σ budgetPoints` identical across all `n` trees, `Σ off == Σ def` in each; and
      `archetype_shapes_actually_differ` — the strongest node differs by ≥ 2× across the archetype set
- [ ] `R-A1`: `W(t)/cost(N_a(t))` walked at **every** tier as an exact integer ratio, refused above
      `archetype.rewardSpreadMaxRatioMilli`, exactly 1000‰ at `t == tierCount`, with
      `archetypes[].rewardPerPointMilli[]` emitted so the tier-2 gradient is visible in a diff
- [ ] `R-M1` (`mechNodes[tierCount] == w[tierCount]`) and `R-M2` (monotone `mechShareMilli`)
- [ ] `P-1` recomputes `potency.maxNodeShareMilli` from the **emitted** `tierCount` and
      `minTerminalWidth`; `P-2` finds no rounded share above the derived maximum at tier counts 1..40;
      `PassiveTree/TreeEqualValue` runs at `--emit` and `--check`, refuses naming
      tree/branch/tier/node, and never clamps
**Verification:** a hand-authored fourth archetype that widens the gradient is refused naming the tier
and the two archetypes. The two deleted tests stay deleted —
`no_node_exceeds_the_potency_ceiling` and `every_shipped_archetype_is_admissible` compare a
construction against its own supremum.
**Depends on:** B1. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/plan/invariants.py`, `archetypes.py`, tests.

### C2: `R-G1`, `R-G2` and the reproducibility contract
**Spec:** `spec-tree-plan.md` §7, §7.1, §Reproducibility, §Testing.
**Description:** The generation gate that keeps the corpus schedule honest without relying on
discipline, plus the `--check`/`--diff`/`planHash` contract. J1's *"only after their gate quantities
are live"* is a schedule note in prose today; `R-G1` is a refusal in code.
**Acceptance:**
- [ ] Every tree emits `gateQuantity`, `gateIndexKind` and `gateState` (`carrier` | `pending`) from a
      checked-in evidence row, and the planner never resolves a quantity itself
- [ ] `R-G1`: stage 2 exits 3 naming the tree and the missing quantity when asked to generate for a
      `pending` tree; `--emit` on a `pending` tree stays free
- [ ] `R-G2`: `trees[]` ordered by `generationWave` then roster ordinal, the wave **derived** from
      `gateState`, never hand-assigned
- [ ] `planHash` over the canonical manifest minus `_provenance` plus the sorted per-tree hashes,
      `emittedUtc` excluded; canonical JSON (sorted keys, 2-space indent, `\n`, UTF-8 no BOM) is
      byte-identical on a Windows/Linux round trip; `--diff` reports budget deltas, archetype
      reassignments, quota-cell moves and ids added, removed or re-minted
**Verification:** flip one hashed input byte; `--check` exits 1 naming the first differing path.
**Depends on:** B1, C1. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/plan/emit.py`, `invariants.py`, tests.

### C3: Catalog record hardening — the axis, the enums, the reflection sweep
**Spec:** `spec-tree-catalog.md` §2.1 R7, §2.2 D40, §2.3, §2.4, §3, §Success criteria.
**Description:** The parts of the record B2 does not carry. §2.4's axis/class agreement is the refusal
that catches a **silent** failure: a sigmoid channel carrying the `PTheta` axis composes, renders and
does nothing.
**Acceptance:**
- [ ] `scaleAxis` is stored as a function of `UnitClass` and disagreement is refused naming both; a
      sigmoid channel never carries `PTheta`
- [ ] The five-value `category` enum, and the importer's map from the plan's `aptitude`/`demonFamily`
      tokens — any token outside the map is refused naming it
- [ ] `exclusionForm` and `excludeProps` disagreeing is refused; an `IdMismatch` is kept **as
      authored**, never rewritten; `soulCurveId` is a curve reference, never a formula (D3)
- [ ] A reflection sweep proves every stored magnitude field is `long` and that no **resolved**
      magnitude is stored anywhere on the record
**Verification:** `a_plan_category_token_outside_the_five_is_refused_naming_it`; the axis fixture
refuses; the reflection sweep fails when a `float` field is added on purpose.
**Depends on:** B2. **Scope:** M. **Files:** `src/FusionRpg.Core/PassiveTree/`.

### C4: The catalog import transaction and the unknown-id report
**Spec:** `spec-tree-catalog.md` §6, §4 R5; `spec-tree-state.md` §4.
**Description:** The boot-time importer inside `FusionRpg.Data` that turns committed generated files
into rows in one all-or-nothing transaction, bumping `catalog_revision` exactly once. B2 asserts import
behaviour with no importer in any Files line today.
**Acceptance:**
- [ ] Import is all-or-nothing and bumps `catalog_revision` **once** per transaction; a partial failure
      leaves the revision unchanged
- [ ] Every id no catalog revision has ever had fails the import with **every** offender named in one
      report, and every actor stays loadable
- [ ] The remaining §6 refusals each have a test, and none repairs, defaults or clamps
- [ ] All SQL lives in `FusionRpg.Data`; the generator in `tools/` opens no connection
**Verification:** `guard-dal.ps1` green; a fixture with two bad ids names both in one report.
**Depends on:** B2, C3. **Scope:** M. **Files:**
`src/FusionRpg.Data/Sqlite/RpgStore.TreeCatalog.cs`,
`tests/FusionRpg.Data.Tests/PassiveTree/TreeCatalogImportTests.cs`.

### C5: Catalog versioning and migration — R1 through R6
**Spec:** `spec-tree-catalog.md` §4; `spec-tree-state.md` §4.
**Description:** The five migration rules as executable properties, plus the retirement write path.
Today only R5 has a home. R6 — a magnitude retune touches no id and migrates nothing — is the property
that makes a live game tunable, and D42's re-measure depends on it.
**Acceptance:**
- [ ] R1/R2: inserting a node changes no existing id; a retired node keeps its id, sets
      `retiredAtRevision`, renders greyed with its retirement printed, and is never reissued
- [ ] R3: an allocation naming a retired node is displayed invalid, grants nothing, **costs nothing to
      hold**, and is never silently repaired
- [ ] R4: a revision that retires an **allocated** node grants a free full respec, at price zero
- [ ] R6: a magnitude retune changes no id and migrates no per-actor row; the filename's `v{n}` equals
      the `catalogVersion` field, asserted (the `classes.v2.json` trap)
**Verification:** an insert / retire / retune fixture triple, each leaving every surviving id
byte-identical.
**Depends on:** B5, C4. **Scope:** M.

### C6: `skillPointsPerThetaMilliByScope` and `SkillPointsFor`
**Spec:** `spec-tree-state.md` §3, §8 (D34).
**Description:** The scope table on `pointEconomy`, mirroring `AptitudePointsPerThetaMilliByScope` one
line above it, and `PointBudget.SkillPointsFor` as the sibling of `PointsFor`. Without it every actor's
budget reads `Θ_player` and fifty demons own the generic catalog at the calibration point.
**Acceptance:**
- [ ] `pointEconomy.skillPointsPerThetaMilliByScope` ships in `aptitudes.v{n+1}.json` with
      `commander = 11`; the other three carry a stated guess, labelled unmeasured
- [ ] `SkillPointsFor` is the same shape as `PointsFor`: `checked`, `long`, no cap, negative source
      rejected
- [ ] A missing rate is a load rejection naming it
- [ ] `every_actor_reads_its_own_scope_budget` — a demon reading `Θ_player` fails
**Verification:** four scopes resolve to four budgets from one actor set.
**Depends on:** A1, B5. **Scope:** M. **Files:**
`src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs`, `data/tuning/aptitudes.v{n+1}.json`.

### C7: The `selfSpent` projection (D8/D39)
**Spec:** `spec-tree-state.md` §2.4; read by `spec-tree-resolve.md` §5.2.
**Description:** The per-tree `(n_i, s_i)` vector derived from the stored node set: self-bought node
count and self-spent soul levels. Four rules, stated identically in both specs so neither can drift.
B6's `H` reads it; nothing supplies it today.
**Acceptance:**
- [ ] The projection is the **final allocation**, not points paid and not a purchase order
- [ ] A node counts once, at 1 — never weighted by what it cost
- [ ] A tree with no self-bought node is **absent** from the vector, never present at zero
- [ ] The exclusion of item-granted, aptitude-threshold and demon-aspect unlocks is a **stated rule**
      with its own test, so widening it later moves a golden instead of starting an investigation
**Verification:** the same node set built two ways yields one identical vector; the store-side half of
`tree-resolve` test 6c.
**Depends on:** B5. **Scope:** M. **Files:** `src/FusionRpg.Core/PassiveTree/State/TreeNodeSet.cs`.

### C8: `tree-state` hardening — ownership rows, soft bounds, volume
**Spec:** `spec-tree-state.md` §2.1, §2.3, §6, §7, §Success criteria.
**Description:** The five ownership rows as `const`s with reasons, the PS-8 soft-bound proof, and the
two boundary properties. The invalid-node and soul-level exclusions are what stop the rising price from
becoming a pure penalty.
**Acceptance:**
- [ ] The five ownership rows are commented `const`s, and
      `an_item_swap_does_not_change_the_next_node_price_net` holds
- [ ] Three grep tests: no `Math.Min` on the price, no narrowing cast on the budget, no `CanUnlock`
      that can return false — each named, each with the PS-8 exemption comment beside it
- [ ] A seam test proves the battle path never loops the single-key loader, and tree state is not
      joined onto the unpaged `ListDemonRoster`
- [ ] 2,000 actors × 40 nodes stores 80,000 rows, not 3.1 million, proven by a row count; `long` on both
      sides, `checked` products, `GetInt64` never `GetInt32`
**Verification:** the row-count proof runs against a generated fixture; the three greps fail when the
construction is reintroduced.
**Depends on:** B5. **Scope:** M.

### C9: The state reconciler and the never-throws rule
**Spec:** `spec-tree-state.md` §4.
**Description:** `TreeStateReconciler` classifying every stored row live / retired / unknown **in
memory, after the load** — so `LoadTreeState` returns rows and never throws on an unknown id.
**Acceptance:**
- [ ] `an_unknown_node_id_does_not_throw_on_actor_load` — the `AptitudeAllocation.cs:39` defect is not
      repeated at 1,560 ids per actor
- [ ] A retired node loads as invalid and grants nothing
- [ ] The three-way result is what the surface renders, and classification happens once per load
**Verification:** a save with one retired and one unknown id loads, and both render.
**Depends on:** B5, C4. **Scope:** M.

### C10: Tree respec (D18)
**Spec:** `spec-tree-state.md` §5, §5.1.
**Description:** Full reset in one transaction, scoped per `(scope, scope_key)`, never refused, priced
in souls on the shape `RespecPolicy` already ships. B5 tests the cost lemma, not the operation.
**Acceptance:**
- [ ] `respec_clears_one_scope_key_only`; a roster-wide reset is not a single transaction
- [ ] `respec_is_never_refused` — no "cannot respec" return, matching `RespecPolicy.cs:33-35`
- [ ] Priced in souls, `long` throughout, divided by 1000 last, `checked`
- [ ] Re-buying the same set after a respec costs exactly what it cost before
**Verification:** a respec round-trip and a re-buy. **Ask:** own counter or the species counter — see
the asks table; the default is its own counter, and it is answered before the counter persists.
**Depends on:** B5. **Scope:** M.

### C11: The archetype band and the wallet band
**Spec:** `spec-tree-state.md` §2.2c, §2.2d.
**Description:** The two tests that read `tree-plan`'s **actual** width vectors at **every** tier rather
than a `k = 4` fixture. The endpoint-only test is exactly what let a 6.0× spread at tier 2 survive, and
§2.2d's own note is that the existing derivation had no test at all.
**Acceptance:**
- [ ] `reward_per_skill_point_is_within_band_over_every_shipped_archetype_and_every_tier`, against
      `archetype.rewardSpreadMaxRatioMilli`, exactly 1000‰ at tier 10 — green at equality by design
- [ ] `the_skill_wallet_clears_the_tier_it_just_opened_for_every_shipped_archetype`; `g` reproduces from
      the corner-share form `a·corner·step·k²/s`
- [ ] The narrow constant-width test keeps its scope in its own name and is never read as corpus evidence
**Verification:** both bands computed in exact integer ratios; no float anywhere in either.
**Depends on:** A1, B5, C1. **Scope:** S.

---

## Phase D — the binder and the resolver completed

### D1: Channel legality — thirteen `UnitClass` verdicts and the derived anchor
**Spec:** `spec-tree-binder.md` §3.3, §4.1, §4.2, §6 M3, §6 M3a.
**Description:** `ChannelLegality` keyed by `UnitClass` with an explicit verdict for each of the
thirteen. §4.1 counts **3 + 1 + 3 + 6 = 13**: three accept a ladder-scaled `+X`, a fourth accepts a
**flat per-mille** `+X`, **three accept a `Θ`-linear point grant**, and six refuse. Refusing every
non-`GameUnits` class would delete accuracy, dodge, crit and status power from the corpus — the exact
channels `tree-resolve` §6.1 says a node writes.
**Acceptance:**
- [ ] All thirteen classes carry a verdict: the three ✅ classes bind ladder-scaled, `PerMilleRatio`
      binds **flat only**, and the three contest classes bind **`Θ`-linear** and refuse a `P(Θ)` amount
      with the class named
- [ ] All **five** `LowerIsBetter` primaries (`attackInterval`, `produceInterval`, `attackCountdown`,
      `produceCountdown`, `takeDmgMultiplier`) refuse a `+X`; a `More` op on a derived channel is
      refused at bind with the rule named (M3)
- [ ] `combat.parry.break.*` / `combat.block.break.*` are granted flat and refuse a `powerLadder`
      amount — they are switches, not dials
- [ ] `channelAnchorMilli` is derived from `power-scale.v{n}.json`'s own pins at bake time and moves
      when `atk.pinValue` moves, with no source edit
**Verification:** the three silent-failure classes (`SigmoidMultiplierPoints`, capped
`StatusPotencyPoints`, `LowerIsBetter`) refuse loudly instead; the anchor test changes a pin and
watches the anchor follow.
**Depends on:** B4. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/Binding/ChannelLegality.cs`.

### D2: Binder reporting — unspent budget, excluded nodes, `--explain`
**Spec:** `spec-tree-binder.md` §7.2, §7.3, §6 M2, §Commands; `spec-tree-catalog.md` §2.5.
**Description:** A refusal that reports nothing lets a tree quietly ship under its own budget. §7.3
exists specifically so nobody refuses an excluded node by analogy with §7's conversion refusal.
**Acceptance:**
- [ ] A refused conversion slot's **unspent budget is reported** and the run's verdict is `FAIL`;
      `tree-plan` gains a suppression flag for a deliberate hole
- [ ] An excluded node — nullification included — **binds normally**: same `kMicro`, same budget
- [ ] `tools/TreeBinder --explain <nodeId>` prints the whole derivation chain, and `--check` proves the
      emitted `kMicro` is what the chain produces
- [ ] A reflect node is documented as contributing **exactly zero** through the battle/sim path
      (`TryReflect` has one caller, `CombatDamageDispatcher.DispatchInstant`), so F3 never reports a
      missing reader as a balance finding
**Verification:** a fixture tree with one refused slot fails the run and names its unspent points;
`--explain` output reproduces §3.4's worked example line by line.
**Depends on:** B4, D1. **Scope:** M. **Files:** `tools/TreeBinder/`,
`src/FusionRpg.Core/PassiveTree/Binding/`.

### D3: The soul track, end to end (D3)
**Spec:** `spec-tree-binder.md` §5.1–§5.4; `spec-tree-resolve.md` §6.2; `spec-tree-catalog.md` §2.3.
**Description:** `Θ_node = Θ_actor + (soulTrack.thetaPerSoulLevelMilli · soulLevel)/1000`, derived at
the read site and never persisted. **The coefficient never moves** — a soul level offsets `Θ`, it does
not scale `kMicro`. The entire second progression track is unbuilt today.
**Acceptance:**
- [ ] `kMicro` is byte-identical at soul level 0 and 50; only `Θ_node` moves
      (`soul_level_offsets_theta_never_the_coefficient`)
- [ ] `thetaPerSoulLevelMilli = 1000` is one `Θ` per level, and the per-mille divide happens **once**,
      before `P()` is called, with a comment saying why it is legal beside CLAUDE.md rule 4
- [ ] `ΔP / Σcost` is constant across `L` — `power_is_linear_in_souls_spent`
- [ ] The soul read widens before the multiply and **throws** rather than wrapping at `long`
**Verification:** resolve tests 10, 11, 13a; `audit-overflow.py` clean.
**Depends on:** A1, B4, B6. **Scope:** M.

### D4: Cross-unlock (D28)
**Spec:** `spec-tree-resolve.md` §4.
**Description:** `credit(i) = max{ base(j) : j ≠ i, stanceGroup(j) == stanceGroup(i) }`,
`gate(i) = base(i) + credit(i)`. `base` is the tree's own aptitude allocation; `stanceGroup` is a
catalog property, read and never re-declared. A whole mechanism with no home today, and I8 renders it.
**Acceptance:**
- [ ] Three mates at 40/30/20 credit **40**, never 90 — exactly one lender
- [ ] The same mate vector run through `max` and through `sum` gives **different** answers and the
      resolver returns `max` (a swap is invisible on a one-mate fixture)
- [ ] A four-of-one-stance build's total credit is bounded by its own largest tree
- [ ] A tree the catalog gives no stance group gets `credit = 0`
**Verification:** resolve tests 3, 3a, 4, 5; a hand-written `max`→`sum` mutant turns 3 and 3a red.
**Depends on:** B6. **Scope:** M. **Files:** `src/FusionRpg.Core/PassiveTree/CrossUnlock.cs`.

### D5: `TreeResolveReport` — the projection the surface renders
**Spec:** `spec-tree-resolve.md` §3.3, §12 tests 17–18, success criterion 8;
`spec-tree-surface.md` §9.1 rule 5.
**Description:** The report carrying the gate, the lender, `H`, `F`, the excluded nodes, and **which
kind of zero** a tier-0 tree is — read from the catalog, never inferred from the zero. I6 cannot be
built as specified without it.
**Acceptance:**
- [ ] A tier-0 tree reports *no aptitude allocated yet* versus *this tree's gate quantity has no
      producer* as a catalog-read `gateState`, never an inference
- [ ] An excluded node contributes zero and is reported with the winner named — for reroute,
      precedence and **nullification** alike; a nullified node reports **inert**, never un-unlocked
- [ ] A gate that closed invalidates rather than repairing, and the node contributes zero
- [ ] `tree-surface` renders the gate, lender, `H`, `F` and exclusions **without recomputing** any
**Verification:** resolve tests 17 and 18; a surface fixture renders from the report alone.
**Depends on:** B6, D4. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/TreeResolveReport.cs`.

### D6: `TreeAtomSource` — battle parity and attribution
**Spec:** `spec-tree-resolve.md` §2.1, §2.2, §12 tests 15–16.
**Description:** The third source of the shape `TraitAtomSource` and `EquipAtomSource` already ship,
emitting `BattleChannelMod`. B6 proves one path; this proves the two agree. Cite `Battle*` by symbol
(R9).
**Acceptance:**
- [ ] `Lawn_and_battle_resolve_to_the_same_totals` for one actor
- [ ] Every contribution carries `SourceId = tree.{treeId}.{nodeId}` — one row per node (GG-49), so
      `tree-surface` needs no retrofit
- [ ] No new subsystem, no new order band, and the existing three registrations are not evicted
**Verification:** test 15's parity fixture; attribution reaches `ChannelContributions` unchanged.
**Depends on:** B6. **Scope:** M. **Files:** `src/FusionRpg.Core/Battle/TreeAtomSource.cs`.

### D7: The resolver's read rules — PS-3, `F`'s scope, `Fmax = 1000‰`, memoisation
**Spec:** `spec-tree-resolve.md` §5.1, §5.3, §5.4, §6.1, §11, §12.
**Description:** The reads that fail **silently** when they are wrong: a contest channel scaled by
`P(Θ)` makes the sheet number rise while the multiplier does not. Plus the withdrawal path for `F` and
the memoisation the perf SSOT calls for.
**Acceptance:**
- [ ] PS-3 line by line: magnitudes read `P(Θ_node)`, contests read `Θ_node` **linearly** (test 12)
- [ ] `H = w·H_nodes + (1−w)·H_souls` with no `1/n` normalisation; an empty denominator reads **zero**,
      never uniform
- [ ] `F` multiplies every tree-derived contribution and **nothing else**, in both read modes; and
      `Fmax = 1000‰` is a legal, tested configuration that removes `F` byte-identically without
      removing a code path
- [ ] Resolution memoises by reference and re-resolves on a changed state reference (test 20)
**Verification:** the four hand-written mutants — `max`→`sum`, divide order, wallet-in-gate, points-paid
in `H_nodes` — each turn a named test red.
**Depends on:** B6, C7, D3. **Scope:** M.

### D8: The `ssot-power-scale` rows the tree runtime owes
**Spec:** `spec-tree-plan.md` §9 items 3–4; `spec-tree-state.md` §9.1, §9.2;
`spec-tree-binder.md` §5.4; `spec-tree-resolve.md` §6.2.
**Description:** Four §10.2 rows — `req(t)`, `W(T)`, D36's `unlockCost` ladder and `Ws`
(`soulTrack.thetaPerSoulLevelMilli`) — the §11.10 caps-register row for the unlock price, and the
`inventory.json` mirror rows in the same change. **Ordinals are assigned at the moment the rows land,
not reserved:** audit 21 and audit 20 both claimed "row 29", which is a collision, not a spec. Take the
next free ordinals and move the row-count line with them.
**Acceptance:**
- [ ] §10.2 gains the four rows at the next free ordinals (today's highest is 28), each citing its
      source file and its tunable key; row 6's `XpToNext` is the precedent for the two cost ladders
- [ ] §11.10 gains the unlock-price row with its verdict: a soft economic bound, proven, with the three
      forbidden constructions named
- [ ] §10 also gains the authored-depth content-breadth row `tree-plan` §9 item 4 names
- [ ] `inventory.json` mirrors every new row in the same change, and the row-count line moves with them
**Verification:** by reading, then re-grepping each file (evidence rule 6). `guard-power.ps1` cannot
catch any of this — see the standing rule at the top of this file.
**Depends on:** C1, D3, D7. **Scope:** S. **Files:**
`docs/architecture/power/ssot-power-scale.md`, `docs/architecture/power/inventory.json`.

### ✅ Checkpoint D — the runtime
- [ ] Both progression tracks resolve: a node bought and a node deepened each move a channel
- [ ] Lawn and battle agree on the same actor's totals; a retired node neither throws nor repairs
- [ ] The four SSOT rows are in `ssot-power-scale.md` and mirrored in `inventory.json`

---

## Phase E — mechanism wiring

Four inert lines in shipped code. G1 is the critical path — one subsystem, ~90 lines by the shipped
`AtomDerivedSubsystem` precedent, unblocking Erosion, layer parity and conditional scaling at once. G4
stays excluded on purpose (`definitions.md` §14.2 is a design law, not an oversight): no task widens
`stat.derived`'s trigger set, and no task adds a 17th atom kind.

### E1: G1 — the fourth `IActorStatSubsystem`
**Spec:** `spec-mechanism-wiring.md` §3, §4.1.
**Description:** A status's derived-channel writes currently go to the *primary* bag
(`EffectRuntime.cs:81`) which none of the three registered subsystems reads.
**Acceptance:**
- [ ] A status writing `combat.defense.omni` reaches the composed value
- [ ] Registered under its own `SubsystemId` with an opt-in delegate — no eviction of the existing three
- [ ] Two stacks withdraw independently, contributions name the status instance, and an empty delegate
      contributes nothing
- [ ] `StanceRuntime.Raise` and `ExhaustionPolicy.Sync`, which already produce such mods, now compose
**Verification:** the seam test with its three-subsystem **falsifier** arm — it fails against `main` and
passes after. Goldens unmoved (no shipped content authors a status `stat` overlay — verify with a
`data/seed/` grep first).
**Depends on:** none (parallel with A–D). **Scope:** M.

### E1b: the L2b resist feedback path
**Spec:** `spec-mechanism-wiring.md` §4.1, §12 q1 (closed 2026-09-05).
**Description:** Owner decision: a status contributes **everything** it writes, `status.resist.*`
included. `ResistanceEvaluator` already reads `ActorDerivedSnapshot` and already keys on
`StatusImmune(tag)` / `StatusImmuneReduction(tag)`, so after E1 a host carrying a resist-granting
status rolls harder against the *next* application. **No shipped content changes** — verified: no
status in `data/seed/` writes a derived stat. This task makes the new behaviour explicit and tested
rather than emergent.
**Acceptance:**
- [ ] A host carrying a status that raises `status.resist.dot` resists the next DoT measurably more
      than an identical host without it
- [ ] The feedback terminates — the resist read is a dictionary lookup (`ForHost`), never a nested
      resolve; asserted, not assumed
- [ ] Order-sensitivity is pinned by test: `warding` then `wither` differs from `wither` then
      `warding`, and the difference is the documented one
- [ ] `tree-language`'s authoring rules gain the note that a status writing `status.resist.*` makes
      application order significant
**Verification:** the three tests above; existing status suites unmoved (nothing shipped authors a
status `stat` overlay).
**Depends on:** E1. **Scope:** S.
**Files:** `tests/FusionRpg.Core.Tests/Status/`, `spec-tree-language.md` authoring rules.

### E2: G1's injector half and the parse refusal
**Spec:** `spec-mechanism-wiring.md` §3, §4.1 sub-decisions 3 and 4, §6.
**Description:** E1 lands the Core subsystem. The half that makes it reach a live actor is the injector
adapter; the half that stops a wrong number shipping looking correct is the parse refusal. There is no
`More` on the derived side, and the spec's own mutation set targets exactly this.
**Acceptance:**
- [ ] `LiveStatusMods.For` reads the live `EffectRuntime.Status` static inside `try/catch` and returns
      empty on failure, mirroring `GrantedDerivedAtoms.cs`; `CheatState.cs` passes `liveStatuses:`
      alongside the existing `boundDerivedAtoms:` argument
- [ ] `StatusDerivedWiringGuardTests` — a **text** guard, because the injector cannot host a test project
- [ ] `IsDerivedChannel` is extracted to one public predicate read by both the parser and the subsystem,
      and `more` on a derived channel is refused **at parse** with a named error, never coerced to `Flat`
- [ ] `mutate.ps1` over the subsystem: the always-true `IsDerivedChannel` mutant and the `Flat`
      default-arm mutant are both caught
**Verification:** `dotnet test tests/FusionRpg.Guard.Tests`; `.\scripts\mutate.ps1` — the two named
mutants die.
**Depends on:** E1. **Scope:** S. **Files:** `src/FusionRpg.Injector/Stats/LiveStatusMods.cs`,
`src/FusionRpg.Injector/.../CheatState.cs`, `src/FusionRpg.Core/Status/StatusStatPayload.cs`,
`tests/FusionRpg.Guard.Tests/`.

### E3: G2 — Battle recomposes derived mid-fight
**Spec:** `spec-mechanism-wiring.md` §4.2.
**Description:** `BattleRunState.RecomposeDerived` has one production caller, at construction. Add the
per-round call. **Cite by symbol** — this file is being edited by `battle-tempo`, and G1 of
`gate-counters` (task G1 below) also modifies it.
**Acceptance:**
- [ ] A conditional-scaling mechanism changes value between rounds
- [ ] One `RecomposeDerived` per actor per round, and it is idempotent
- [ ] Battle goldens **run**, not reasoned about: re-blessed deliberately with the diff explained, or
      unmoved
**Verification:** the battle suite; a named test for the mid-fight change.
**Depends on:** E1. **Scope:** S.

### E4: G3 — the contribution fold, in both hosts
**Spec:** `spec-mechanism-wiring.md` §4.3 steps 1–3.
**Description:** The contribution fold on `ActorDerivedLookup`, wired into **both** `SimEffectHost`
**and** `FoundationHarness`. `tools/CombatSim` drives `FoundationHarness`, not `SimEffectHost` — fold
only one and the harness still reads a bare pinned snapshot. The registry cell does **not** move in this
task; that is E5.
**Acceptance:**
- [ ] `Sim_folds_bound_derived_contributions_onto_the_pinned_snapshot` passes on **both**
      `SimEffectHost` and `FoundationHarness`
- [ ] A `BindContext(RuntimeId.Sim)` call site exists, so a bind is actually attempted
- [ ] `AtomKindRegistry` is unchanged by this task — the fold is provably in place before the cell moves
**Verification:** the harness folds a contribution with the cell still at `None`, proving the fold and
the cell are independent.
**Depends on:** E1. **Scope:** M.

### E5: G3 — the four-op verdict, and the cell moves last
**Spec:** `spec-mechanism-wiring.md` §4.3 step 4, §6, §11 A5/A6; `decisions.md:106`.
**Description:** `RuntimeState.None` in Sim is a *rejection* at `BindGate`, not a degradation. Whether
the cell becomes `Full` or `Partial` is decided **from the built executor** by exercising all four
derived ops — a fold on `OverlayAdd` honours `Flat`/`Increased` and not `Replace`/`Flag`, so the honest
first landing is `Partial`.
**Acceptance:**
- [ ] `The_four_derived_ops_decide_Full_versus_Partial` — the cell reads what the fold actually honours
- [ ] The registry cell moves **last**, after E4's fold is green
- [ ] `AtomKindRegistryTests` **and** `IlvlTierLadderTests.cs:87` both move with the cell, deliberately,
      with the matrix change explained
- [ ] `decisions.md:106`'s *"Sim stays `None` — it still has no consumer"* is amended in the same change
**Verification:** the harness scores a `stat.derived` node end to end; `guard-power` green.
**Depends on:** E4. **Scope:** M.

### E6: The registry rows `mechanism-wiring` owes
**Spec:** `spec-mechanism-wiring.md` §3, §10, §11 A7.
**Description:** Two documents owe a row under evidence rule 6's *"in the same change"*, and A7's three
counts must be asserted rather than assumed — `DESIGN-GATE.md` §1's atom row has gone stale on them
twice.
**Acceptance:**
- [ ] `actor-hub-ssot.md` §6 carries
      `status.timed | 400 | session bag | timed derived from live statuses`
- [ ] `atom-catalog-ssot.md`'s `stat.derived` runtime row reflects the new Sim cell
- [ ] `KindCount == 16`, `TriggerCount == 13`, `AttachPointCount == 7` are asserted unchanged, and
      `stat_derived_still_refuses_every_trigger` stays green
- [ ] `DESIGN-GATE.md` §1's atom row still reads 7 / 16 / 13, verified by counting
**Verification:** re-grep each file after the edit.
**Depends on:** E1, E5. **Scope:** S.

### ✅ Checkpoint E — mechanism nodes execute
- [ ] A status-granted derived channel reaches a live actor on the lawn and changes mid-fight
- [ ] A `stat.derived` atom binds and is scored in the balance harness
- [ ] The three atom counts are unchanged, asserted

---

## Phase F — `squad-harness` and the measurements

`spec-squad-harness.md` describes its own `tools/SquadHarness/` project with eight modes, two rosters,
three columns, two artifacts, a determinism hash, twenty named tests and a four-stage plan. It rejects
the single-top-level-`Program.cs` shape of `tools/HybridViability` and `tools/CombatSim` **by name**,
because determinism is this module's hard requirement and an untestable tool cannot carry
`DeterminismTests`.

### F1: `squad-harness` S1a — the tool, the two rosters, the determinism hash
**Spec:** `spec-squad-harness.md` §1.2, §7, §9.1, §Project structure, §Testing.
**Description:** `tools/SquadHarness/` as its own project with a thin `Program.cs` over referenceable
types, plus `tests/FusionRpg.SquadHarness.Tests/` taking a `ProjectReference` on it. The 91-build duel
roster (12 + 66 + 12 + 1) and the 23-squad roster from **one** shared corner-shape helper.
`TuningBootstrap` reads the highest `data/tuning/<domain>.v{n}.json` per domain and configures
`BattleTuningHub`.
**Acceptance:**
- [ ] The duel roster is proven to be `tools/HybridViability`'s same 91 builds by **constructing** them,
      never by asserting the number 91; `Every_squad_has_exactly_six_actors` holds
- [ ] `A_second_process_reproduces_the_hash` — SHA-256 with provenance blanked; shuffling the roster
      moves no surviving cell; parallel and serial agree by hash
- [ ] Seeded as `seed(a,d,k)` with common random numbers; counts are `long` and `checked`; no `float`
      anywhere and no `double` in the hash
- [ ] Zero files changed under `src/`, `data/` or `tests/` outside its own test project
**Verification:** `dotnet test tests/FusionRpg.SquadHarness.Tests`; `verify --seed …` run twice.
**Depends on:** none (parallel with A–E). **Scope:** M. **Files:** `tools/SquadHarness/` (new),
`tests/FusionRpg.SquadHarness.Tests/` (new).

### F2: `squad-harness` S1b — the modes, the three columns, the two artifacts
**Spec:** `spec-squad-harness.md` §2, §3, §5, §8, §9.2, §10.
**Description:** Modes `duel`, `squad`, `transfer`, `verify`; the three columns `duelClosedForm` /
`duelTrials` / `squadTrials` with `orderingByColumn` and `transfers`; the two artifacts; two-stage
screening (3,000) then `--refine` (40,000) only on cells inside their own half-width.
**Acceptance:**
- [ ] `transfer` prints three columns and one derived word; `transfers` is `false` whenever an ordering
      rests on a gap inside its own half-width, and reports *"cannot separate"* in those words
- [ ] Squad-vs-squad is the primary mode and `--opponent wave` is reported separately; stalemates leave
      the denominator and a cell over the flag threshold reports `lowConfidence`, refused not scored
- [ ] Elements are neutral in every generated setup, and the `coverage` block names every unexercised
      axis plus §10's six blocked mechanism classes and the A10a/A10b split
- [ ] `_scope-transfer.json` and `_squad-scope.json` are written, and every proposed value is a number
      **and** a half-width — the harness writes no `data/tuning` value
**Verification:** `verify` enumerates the mode table and covers every mode; the Θ ≈ 300 crossover from
doc 16 reports *"cannot separate"* rather than a refutation.
**Depends on:** F1. **Scope:** M.

### F3: A10a — the Erosion differential
**Spec:** `spec-squad-harness.md` §10.1; `spec-mechanism-wiring.md` §11.1.
**Description:** Six-vs-six over `BattleEngine`, four arms, `D` as a difference of differences.
**Needs no wiring at all and depends on neither G1 nor G3** — §11.1 is explicit, and G3 is off A10's
critical path entirely because the harness resolves over `BattleEngine`, not Sim.
`BattleActorSetup.ChannelMods` already carries `(ChannelId, long)` and the composer throws on an unknown
id. A10a's four arms **are** the corner/spread arms; there is no separate mechanism arm.
**Acceptance:**
- [ ] `D` reported with a 95% lower bound above **3.0pp** and its own half-width ≤ **1.0pp**;
      PASS / FAIL / **UNRESOLVED**, and UNRESOLVED holds the checkpoint exactly as FAIL does
- [ ] Direction: `D > 0` and neither arm negative
- [ ] The selectivity bar `ΔW_spread ≥ 2 × ΔW_corner` is reported
- [ ] The 1v1 baseline is shown beside it, so *"does it transfer"* is answerable
**Verification:** same seed, same numbers; `_erosion-differential.json` written. A reflect node scores
exactly zero on this path (D2) — that is a missing reader, never a balance finding.
**Depends on:** F2. **Scope:** M. **Files:** `tools/SquadHarness/Erosion.cs`,
`docs/research/passive-tree/_erosion-differential.json`.

### F4: S2 — concentration and cross-unlock
**Spec:** `spec-squad-harness.md` §4, §11 S2.
**Description:** The `concentration` and `crossunlock` modes over `Fmax × w × Θ`, with D25's ownership
cost folded into the tree model (`req(t)`, `W(T)`, `H`, `F` per actor). Produces
`concentration.fmaxMilli` and D28's four credit rules as evidence.
**Acceptance:**
- [ ] Every proposed value is a number **and** a half-width; nothing is written to `data/tuning`
- [ ] `1000` — no multiplier at all — is inside the `fmaxMilli` sweep, because D5 is provisional
- [ ] D25's ownership cost is in the model, and a run without it is reported as a different cell rather
      than silently substituted
**Verification:** `verify` covers both new modes by enumerating the mode table.
**Depends on:** F2, D4. **Scope:** M. **Files:** `tools/SquadHarness/TreeModel.cs`, `Modes.cs`.

### F5: S3 — the soul track in the model
**Spec:** `spec-squad-harness.md` §11 S3.
**Description:** The soul track taught to the model so `concentration.wMilli` becomes measurable, swept
over `Θ ∈ {100, 150, 200, 300, 400, 600}`. Doc 16: `w` is the load-bearing late-game parameter and is
unmeasurable until the model carries both tracks.
**Acceptance:**
- [ ] `soulTrack.thetaPerSoulLevelMilli` and `concentration.wMilli` are each reported as a value and a
      half-width
- [ ] The Θ ≈ 300 crossover reports *"cannot separate"* in those words when it cannot, and the artifact
      never presents that as a refutation of a closed-form result
- [ ] The soul read in the model matches D3's shipped derivation, asserted against it
**Verification:** the sweep runs at all six Θ values from one seed stream.
**Depends on:** F4, D3. **Scope:** M.

### F6: S4 — the budget mode, and D42's two dials
**Spec:** `spec-squad-harness.md` §11 S4; `spec-tree-plan.md` open question 1; `spec-tree-binder.md` §3.6.
**Description:** The `budget` mode: D15's marginal win share per budget point across the duel roster.
**S4 is claimed, not optional** — no other module in the program is scoped to produce the evidence
`tree-plan`'s *"no tree is OP"* rests on, and it is the only thing that can re-derive
`budget.treeTotalPoints` and `treeShareMilli` (D42, both shipped `UNMEASURED` by A1).
**Acceptance:**
- [ ] S4 reports marginal value per budget point with the same half-widths every other cell carries
- [ ] `budget.treeTotalPoints` and `treeShareMilli` each get a proposed value and a half-width, or an
      explicit *"cannot separate"* — the harness still writes no tuning value
- [ ] The republish is a **tuning** change: node ids, the plan and the catalog are byte-identical
      across it, proven by `--check` (C5's R6)
**Verification:** re-run `--check` on the committed plan and catalog after the tuning republish; both
byte-identical.
**Depends on:** F4, C5. **Scope:** M.

### ✅ Checkpoint F — measurement
- [ ] A10a produces `D` with a half-width, at the effect size the spec names
- [ ] If UNRESOLVED or FAIL: **stop and review** — phase H's corpus is budgeted on this premise
- [ ] S4 has run, so `treeShareMilli` and `budget.treeTotalPoints` are re-derived from real data and
      republished as `passive-tree.v2.json` (D42), with their `UNMEASURED` markers removed

---

## Phase G — the gate quantities

Without these, 27 of 39 trees sit at tier 0 (§13.4). D37 put them in this program; D43 seeds existing
saves.

### G1: The two shipped-code prerequisites (P1, P2)
**Spec:** `spec-gate-counters.md` §7 P1 and P2.
**Description:** G2's fresh-vs-refresh rule and G3's DoT exclusion are both undeliverable without a
change in `src/` that no task owned. **P1:** a defaulted `DamageOrigin origin = DamageOrigin.DirectHit`
parameter on `DamageApplyPipeline.Apply`, with only the pulse construction sites passing anything.
**P2:** a new `OnFreshApplication` property on `StatusRuntime`, fired only when the upsert added a new
instance — `OnApplied` is single-assignment with three assigning sites, one of which chains by hand, so
its signature does not move. **This is the one file crossing another wave-0 module's surface:**
coordinate with E3, which also modifies `BattleRunState`.
**Acceptance:**
- [ ] The origin defaults, so every existing call site is zero lines changed
- [ ] `OnApplied`'s signature and all three assigning sites are untouched
- [ ] A refresh fires `OnApplied` and does **not** fire `OnFreshApplication`
- [ ] Battle's pulse site passes `DamageOrigin.StatusPulse` — **cite by symbol** (R9)
**Verification:** a reapply loop fires one fresh event; a `wither` pulse train reports `StatusPulse`.
**Depends on:** none. **Scope:** S. **Files:**
`src/FusionRpg.Core/Combat/DamageApplyPipeline.cs`, `src/FusionRpg.Core/Status/StatusRuntime.cs`,
`src/FusionRpg.Core/Battle/BattleEngine.cs`.

### G2: `status_applied` counter
**Spec:** `spec-gate-counters.md` §2.1, §4.1, §4.2, §4.3.
**Acceptance:**
- [ ] Credits outbound, landed, fresh applications to a distinct host — never inbound, attempted,
      refreshed or self; **ownership is decided at spawn**, so a charmed or hypnotised actor cannot
      launder credit
- [ ] Accumulates in memory, flushes batched (5 s and match end) in **one** transaction; no write on the
      hot path
- [ ] Persisted raw and sparse in `rpg_gate_counter` with `owner_kind`/`owner_key`, no cap; the index is
      derived on read
- [ ] All SQL in `RpgStore.GateCounters.cs` as a partial slice sharing `_gate`, participating in
      `EnsureHotSchema` and `Reset()` — with a test that proves the `Reset()` participation
**Verification:** a reapply loop on one target earns one credit; a resisted apply earns none; a
`charm_pulse`ed enemy's applications earn its original owner nothing.
**Depends on:** G1. **Scope:** M.

### G3: `element_mastery` counter
**Spec:** `spec-gate-counters.md` §2.2, §2.2b, §12.
**Acceptance:**
- [ ] One credit per element component on a **direct** landed damage event; DoT pulses excluded
- [ ] `Outcome == Applied` **and** `AppliedAmount != 0` — a fully-absorbed hit earns nothing (§11 test 7,
      the pipeline's deliberate zero-delta miss-telemetry parity)
- [ ] Reads its **own** rate key, not the shared Aspect rate
- [ ] Registration is exclusive per family and **throws naming both owners** on a duplicate, with no
      combine path — asserted by a composition-root guard test
**Verification:** a `wither` pulse train earns nothing; a hybrid two-element hit earns two; a hit fully
eaten by a shield earns none.
**Depends on:** G1, G2 (shares the store slice). **Scope:** M.

### G4: The index transform and the gate registry
**Spec:** `spec-gate-counters.md` §3, §5.2, §6, §9, §11.
**Description:** Counters reach the gate as an **index**, never a raw count — the defect
`PointBudget.cs:20-26` records inverted the locked scope ordering **176×**.
**Acceptance:**
- [ ] `IGateQuantitySource` + `GateQuantityRegistry` answer in aptitude-point-equivalents; no
      `AptitudeAllocation` row is constructed
- [ ] The index is the square-root transform with `c = 23`, **integer-only** — no `Math.Sqrt`, no
      `double`, a division-based predicate — and survives a count at `long.MaxValue` (§11 tests 3, 4)
- [ ] Tier 10 opens within 5% of the primary tree's Θ from tier 4 up
- [ ] `Total()` / `GrandTotal()` / `Share()` are provably untouched by crediting — an executable test,
      not an argument; and the tier-0 **reason** distinguishes *no aptitude allocated yet* from *this
      quantity has no producer*
**Verification:** the parity table reproduces; D35 holds as a test; §11 test 9 is green.
**Depends on:** G2, G3, B6. **Scope:** M.

### G5: D43 — seed existing saves from a proxy
**Spec:** `spec-gate-counters.md` §16 OQ1; decision D43.
**Acceptance:**
- [ ] One-time, stamped, auditable; never runs for a new player
- [ ] An existing save shows non-zero counters proportionate to its primary-tree depth
**Verification:** a fixture save before/after; running twice changes nothing.
**Depends on:** G4. **Scope:** S.

### G6: The gate-counter surface and its injector wiring
**Spec:** `spec-gate-counters.md` §10, §15 criterion 9.
**Description:** The counters are invisible without a read path, and `tree-surface` needs one. The
injector is a separate assembly with its own guard-test convention.
**Acceptance:**
- [ ] `POST /api/gate-counters/credit` takes the batched flush; `GET /api/gate-counters/{playerId}`
      returns counts, index **and** equivalents
- [ ] Both counters are subscribed in the injector where the status runtime is already wired
      (`EffectRuntime.cs:59,69`)
- [ ] The lawn's per-hit cost is unchanged within probe noise — a credit is an in-memory increment
- [ ] The tier-0 reason is distinguishable on the wire, not only in Core
**Verification:** a `probe-perf.ps1` window before/after shows no per-hit regression.
**Depends on:** G4. **Scope:** M. **Files:** `src/FusionRpg.Server/GateCounterEndpoints.cs`,
`src/FusionRpg.Injector/Effects/EffectRuntime.cs`.

### G7: The `UniqueDemon` scope binding
**Spec:** `spec-species-tree.md` §8.1 point 2.
**Description:** Nothing in `src/` passes `AllocationScope.UniqueDemon` to `PointBudget.PointsFor` or
`CheckScope`. Its twin already ships — `SpeciesAllocation.cs:35,62` does exactly this for `DemonType`,
including the index transform `PointBudget.DemonTypeSourceFromLevel`. Without it, a reviewer judging 840
species cards against a ladder that reads zero is judging the writing, not the tree.
**Acceptance:**
- [ ] Specimen level reaches an aptitude budget at `UniqueDemon` scope, mirroring the `DemonType`
      transform
- [ ] A species tree's tier ladder reads non-zero on an actor with a levelled specimen
**Verification:** a reviewer opening a species card sees a live ladder, not zeros.
**Depends on:** G4, C6. **Scope:** S.

### G8: The `ssot-power-scale` rows `gate-counters` owes
**Spec:** `spec-gate-counters.md` §6, success criterion 7.
**Description:** Two §10.2 rows — the mastery ladder and the count→equivalents read — at the next free
ordinals, with the row-count line at `:587` moved with them. As with D8, the ordinals are taken when the
rows land; audit 20's "29 and 30" and audit 21's "29" were written against the same free slot.
**Acceptance:**
- [ ] §10.2 carries both rows, each naming its source file and its tunable key
- [ ] The row-count line moves with them (today: 27 rows, highest ordinal 28)
- [ ] `inventory.json` mirrors both rows in the same change
**Verification:** by reading, then re-grepping. `guard-power.ps1` keys on `level`/`lvl`/`index` and this
parameter is `count` — a green guard is not evidence.
**Depends on:** G4, D8. **Scope:** S. **Files:**
`docs/architecture/power/ssot-power-scale.md`, `docs/architecture/power/inventory.json`.

### ✅ Checkpoint G — reachability
- [ ] All 39 generic trees have a live gate quantity, and all 39 are reachable above tier 0
- [ ] An existing save no longer shows 27 trees at tier 0
- [ ] The per-hit lawn cost is unchanged within probe noise

---

## Phase H — generation machinery and the primary corpus

`tree-language` §7 numbers **24 validation gates** and owns them. The previous plan's *"every
validation gate green"* was an acceptance criterion written against a harness nothing built. This phase
builds the harness, then runs it on 12 trees rather than 39.

### H1: `tree-language` contract and schema gates
**Spec:** `spec-tree-language.md` §3–§5; §7 gates 1 and 8.
**Acceptance:**
- [ ] The request/response schema refuses a numeric field **at construction** (`MAGNITUDE_DENY_NAMES`),
      and `audit_schema` passes over the real constant and fails when a numeric field is added
- [ ] Permitted values **are** the schema `enum`, so an out-of-quota value is unsampleable, not rejected
- [ ] The twelve `adapters/trees/nodegen/` modules exist, including `dedup.py` and `exclusion.py`
**Verification:** a brief asking for a magnitude fails to build.
**Depends on:** A2, B1. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/nodegen/`.

### H2: The gate runner and the in-run gates
**Spec:** `spec-tree-language.md` §7 gates 2, 5, 6, 7, 9–14, 23, 24; §Commands; §Project structure.
**Description:** The harness H9's *"every gate green"* is written against. `verdict.py`'s
`GATING_METRICS` and `missing_thresholds()`, the dry-run report, and the shipped in-run gates wired into
the trees adapter: description audit, preflight, contract, brief conformance, text style, vote
resolution, bounded repair, persist-time re-gate, idempotence, run verdict, offline guarantee.
**Acceptance:**
- [ ] `python -m seedsmith check --family PassiveTree --gate` exits 0/1/2/3 on the shipped four codes
- [ ] The dry run prints `gatingMetrics` and `gatesMissingAThreshold` **before** spending a call, and
      prints the ~4,680-call figure for the generic corpus
- [ ] `GATING_METRICS` has **exactly one** entry, and an OPEN-loop metric registered with `gates=True`
      raises; `FAIL` beats `NOT_MEASURED`, and a held partition alone denies a `PASS`
- [ ] The offline transport stub **raises** on any unexpected call
**Verification:** `python -m pytest tools/seedsmith/tests/adapters/trees`; a run that reaches a model
fails the suite.
**Depends on:** A2, H1. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/nodegen/verdict.py`, `run.py`,
`tools/seedsmith/tests/adapters/trees/`.

### H3: The property vocabulary read, and the quota stage
**Spec:** `spec-tree-language.md` §5; `spec-tree-plan.md` §8.
**Description:** The language stage **reads** the closed property set B1 emits — it does not produce it.
Quota cells via `largest_remainder_count`, with step 5's return-to-pool, and `permittedIds` becoming the
schema `enum`.
**Acceptance:**
- [ ] The quota is computed with `largest_remainder_count`; a hard constraint returns its draw to the
      pool, and an overdrawn cell is **refused**, not rebalanced silently
- [ ] Exclusion is property-keyed, all three D40 forms available, nullification printed on both sides
- [ ] The stage refuses to run against a plan carrying no `propertyVocabulary` — it never synthesises one
**Verification:** a corpus-level quota check reproduces the declared target from `passive-tree-targets`.
**Depends on:** H1, B1. **Scope:** M.

### H4: The eight `PassiveTree/*` corpus metrics
**Spec:** `spec-tree-language.md` §7 gates 15–22; §Project structure.
**Description:** `QuotaDrift` (re-derived independently, symmetric), `MechanismRamp` (exact per-tier
count against `archetypes[].mechNodes[t]`, both directions, plus `mechNodes[10] == w[10]`),
`CellOccupancy`, `ExclusionRate`, `ExclusionResolvable`, `NearDuplicate` (local exact Jaccard, **not**
the shared MinHash), `NameCollision`, `UnresolvedCount`.
**Acceptance:**
- [ ] `UnresolvedCount` is the **only** metric at `gates = True`, promoted with
      `demon_roster.py:357-370`'s reason recorded
- [ ] `QuotaDrift` catches a mutated brief because it re-derives rather than reads
- [ ] `MechanismRamp` is a **count**, not a threshold — a threshold implementation fails on
      `broad-and-flat` tiers 4–7
- [ ] `ExclusionResolvable` reports **`NOT_MEASURED`** while the atom-tag registry is unbuilt (§5.1),
      cited by name and never by ordinal; `NameCollision` catches the measured 83-of-83 defect
**Verification:** synthetic corpora with an injected defect per metric — a 166× skew, a missing deep-tier
mechanism, a duplicated name across 300 trees.
**Depends on:** A2, H3. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/metrics/passive_tree.py`.

### H5: The two `tree-review` corpus metrics
**Spec:** `spec-tree-review.md` §4.1, §4.2, §7.
**Description:** `PassiveTree/TreeEqualValue`'s **content-side** half — over `tree-binder`'s prices
rather than the plan's budget column, which is C1's half — plus `PassiveTree/DeepMechanismValue`
(registers `gates = False`, reports) and `PassiveTree/HiddenFileCount` (the walk **without** the `_`
skip, reporting `visitedFileCount`, with a canary fixture root).
**Acceptance:**
- [ ] `TreeEqualValue` reads bound prices and reports through the same registry C1 registered it in — one
      metric, two inputs, never two metrics with one name
- [ ] `DeepMechanismValue` registers `gates = False` and reports rather than blocks
- [ ] `HiddenFileCount` is green over the real seed roots with a **non-zero** `visitedFileCount`, and the
      same run finds the canary parked entry — a green at `visitedFileCount == 0` is distinguishable
      from a green over forty empty files
**Verification:** a fixture root with one `_`-prefixed file is found; a corpus with one over-priced tree
fails `TreeEqualValue`.
**Depends on:** H4, C1, D2. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/metrics/passive_tree.py`.

### H6: `tree-review` — the tree card
**Spec:** `spec-tree-review.md` §5.1–§5.4.
**Description:** One card per tree, one screen: the 2×10 lattice rendered through the **shipped**
`formatMagnitude` (`web/fusion-rpg-web/src/i18n/magnitude.ts:15` takes a `Magnitude` and has no
bare-number overload — that is the GG-46 guard the card depends on), beside the species/tree's own
`reason` sentence, **plus the three nearest sibling trees by fingerprint** — the only way "hundreds of
trees that are all subtly the same" becomes visible.
**Acceptance:**
- [ ] Renders through the shipped TS contract as a Node script in the web package, not a second
      implementation
- [ ] The 2×10 lattice and the species' own sentence are on one screen
- [ ] The sibling panel shows the three nearest by fingerprint
- [ ] The verdict control writes `_review/<lot>.json`, so a reject reason can become the next brief's
      anti-motif (§5.2 rule 6)
**Verification:** `npm test -- magnitudeGuard`; a card renders from a fixture corpus with no network.
**Depends on:** H4. **Scope:** M. **Files:** `web/fusion-rpg-web/scripts/render-tree-cards.mjs`,
`docs/research/passive-tree/_review/`.

### H7: The corpus sheet and the `sheetRead` census gate
**Spec:** `spec-tree-review.md` §5.5, §5.6, §7; §Success criteria.
**Description:** One page per lot, read **before** the cards: quota heat map, name-token frequency,
exclusion census, nearest-neighbour top 20, rejection rate, machine verdict + `missing_thresholds`, and
the hidden-file census. The pilot is the first thing that reads a sheet, so this lands before H8.
**Acceptance:**
- [ ] The sheet carries a `sheetRevision`, and `trees review --census` **refuses** a lot with no
      `sheetRead` row or a row naming a stale revision
- [ ] The row is `{lot, sheetRevision, by, utc}`, written on dismissal
- [ ] The sheet and the verdict queue are **committed**; per-tree cards are regenerated, not committed
**Verification:** a census against a missing row and against a stale row both refuse, by test.
**Depends on:** H5, H6. **Scope:** M.

### H8: The 20-tree review pilot
**Spec:** `spec-tree-review.md` §1.3, §3, §8 open Q1.
**Description:** Every hour figure in this program rests on an unmeasured 60–90 s per card. Half an hour
of measurement, and it also yields the intra-tree defect correlation the sampling design needs.
**Acceptance:**
- [ ] A real per-tree rate, recorded, replacing the assumption
- [ ] The sample size for the full census recomputed from it, and written into the plan before phase J is
      scheduled
**Verification:** the recomputed census cost is in `passive-tree-plan.md` before J2 starts.
**Depends on:** H7. **Scope:** S.

### H9: Emit and generate the 12 primary trees
**Spec:** `spec-tree-plan.md`, `spec-tree-language.md`, `spec-tree-binder.md`, `spec-tree-catalog.md` §5.
**Acceptance:**
- [ ] 480 nodes emitted, generated, bound and committed
- [ ] **Every gate green; the gating metric measured;** any `NOT_MEASURED` named and cited. For
      `PassiveTree/UnresolvedCount` — the one metric at `gates=True` — `NOT_MEASURED` **denies** a pass
      (§7 gate 23; `tree-review` §6.4 rule 1: an absent check is never a pass)
- [ ] Regenerating from the committed plan is byte-identical and re-mints no id
- [ ] The catalog's own `--check` staleness gate runs in CI, distinct from the plan's byte-identity check
**Verification:** `--check` green on both; the catalog loads; a node resolves in a battle.
**Depends on:** Checkpoint F, H2, H3, H4, B6. **Scope:** M (a run, not code).

### ✅ Checkpoint H — primary corpus
- [ ] 480 nodes generated, gated and reviewed at the H8-measured rate
- [ ] The gating metric is measured, not `NOT_MEASURED`
- [ ] Owner review of a sample of cards before phase I

---

## Phase I — the player surface

Standalone-first: every surface renders with the injector absent. **The spec's levels are 0 / 0b / 1 /
2 / 3** (§2.2); the previous todo numbered them 1–4 and every cross-reference between the two documents
was wrong by one. This list uses the spec's numbering.

### I1: The web verification suite
**Spec:** `spec-tree-surface.md` §10, §11, §14.
**Description:** The standing verification block named `dotnet build`, four guards and two Python
audits — **and no web command at all**, so every surface task had no verification bar. This task builds
the suite and adds it to the standing block at the top of this file.
**Acceptance:**
- [ ] The seven guard suites run under `npm test -- volumeMatrix diffStateMatrix fourStatesMatrix
      vocabularyGuard magnitudeGuard bandGuard xyflowGuard`
- [ ] E2E volume fixtures at 10 / 100 / 1000 for the browse, plus the 40-cell lattice at the 1280×720
      floor
- [ ] `Every_surface_renders_with_the_injector_absent` (GG-39) is a named test
**Verification:** all three commands green on `main` before any surface task starts.
**Depends on:** none. **Scope:** S. **Files:** `web/fusion-rpg-web/src/__tests__/`,
`web/fusion-rpg-web/e2e/`.

### I2: The wire, and the shared allocation hook
**Spec:** `spec-tree-surface.md` §12, §10.
**Description:** `PassiveTreeEndpoints.cs` (GET state, POST one whole allocation, the shape
`AptitudeEndpoints.cs:26-57` already ships) and `PassiveTreeDtos.cs`. Plus the `useAllocationDraft`
extraction the spec requires **first** — `ProgressionTab.tsx:7-14` already admits its allocation logic
is a verbatim copy, and a tree spend flow would be the third.
**Acceptance:**
- [ ] GET returns the resolve report; POST takes one whole allocation, never a per-node call
- [ ] The allocation-changed broadcast reaches **both** `WebGroup` and `InjectorGroup`, per
      `AptitudeEndpoints.cs:115-117`
- [ ] `AptitudesPage` and `ProgressionTab` both consume the extracted hook; no third copy is created
- [ ] `guard-dal` green — no SQL outside `FusionRpg.Data`
**Verification:** `dotnet test tests/FusionRpg.Guard.Tests`; `npm run build` clean.
**Depends on:** B5, D5, I1. **Scope:** M. **Files:**
`src/FusionRpg.Server/PassiveTreeEndpoints.cs`, `src/FusionRpg.Contracts/PassiveTreeDtos.cs`,
`web/fusion-rpg-web/src/hooks/useAllocationDraft.ts`.

### I3: Level 0 — *Yours*
**Spec:** `spec-tree-surface.md` §2.1, §2.2 Level 0, §4.1, §8.
**Description:** The Passives **tab**, not a route — it extends the locked placeholder at
`PassivesTab.tsx:12-21` (four `LockedGridSlot`s today), per GG-1.
**Acceptance:**
- [ ] Invested paths, the Focus line's slot, the not-working count and the unspent currencies render;
      the empty state is **content**, not a blank panel
- [ ] Three currencies named distinctly, never the bare word *points* — aptitude points open a tier,
      skill points buy a trait, souls deepen one
- [ ] *"2 of your traits are not working"* filters to exactly those
**Verification:** `npm test -- vocabularyGuard fourStatesMatrix`; the empty state renders on a fresh actor.
**Depends on:** I2. **Scope:** M.

### I4: Level 1 — *All paths*
**Spec:** `spec-tree-surface.md` §2.2 Level 1, §7.3, §9.1 rules 3–5.
**Description:** The browse. §7.3 is explicit that *"ordering is the mitigation"* for 879 trees, so the
five-bucket ordering is the design, not a nicety.
**Acceptance:**
- [ ] Level 1 orders: invested → your own stance's other three → element/status match → everything else
      → the collapsed gate-less bucket
- [ ] Search plus four category filters; query state survives closing the layer (GG-51)
- [ ] The collapsed bucket sorts last, is counted in nothing, and its `gateState` is **read from the
      report, never inferred from a zero**; the count is read, never typed
- [ ] 39 cards render windowed at the volume fixtures I1 builds
**Verification:** `npm test -- volumeMatrix`; tests 30–32 and 36.
**Depends on:** I3. **Scope:** M.

### I5: Level 0b — the bloodline pin and the Codex route
**Spec:** `spec-tree-surface.md` §2.2 Level 0b, §3.
**Description:** The species tree's spend route and its read route. 879 is never a collection anywhere —
a bloodline is pinned to its creature's sheet.
**Acceptance:**
- [ ] A bloodline is pinned to its creature's sheet and **never enters a browse**
- [ ] The Demon Codex read route reaches a species tree from the creature, not from a list
- [ ] The route degrades correctly when the bloodline is undiscovered — silhouette only there
**Verification:** a fixture actor with one discovered and one undiscovered bloodline renders both states.
**Depends on:** I4. **Scope:** M. **Ask first:** the Codex route hangs off `DemonsPage.tsx:367-388`'s
volume defect (840 DOM subtrees against a 240 threshold), which is another program's file — see the asks
table.

### I6: Level 2 — the lattice
**Spec:** `spec-tree-surface.md` §2.3, §9, §9.1 rules 1–2.
**Description:** A 2×10 fixed lattice is **GG-61**, not GG-50.
**Acceptance:**
- [ ] Opens scrolled to the player's own depth, never tier 1
- [ ] A gate-less tree takes the **condition** presentation: no price, no Unlock verb
- [ ] A locked deep tier carries a **distance** — a bar and a `Θ` computed per actor, not stated once —
      and shows its traits in full
- [ ] The locked reason is **visible sibling text** naming **both** routes, through one reason table, and
      is queried by text rather than by `title` (`ActionCluster.tsx:18-29` settled the hover argument)
**Verification:** `npm test -- diffStateMatrix xyflowGuard`; tests 9 and 28.
**Depends on:** I4. **Scope:** M.

### I7: Level 3 — the trait, and both tracks
**Spec:** `spec-tree-surface.md` §4, §8.
**Acceptance:**
- [ ] One verb per cell, three states; the deepen control is a **stepper** — no slider, no raw-id
      `NumberInput` — and it edits the draft
- [ ] A nullified trait renders **inert, never un-unlocked**, printing the rule and naming the winner
- [ ] Exclusions print on **both** sides with the same winner; the Level-0 count and its filter agree
      with what the lattice shows
- [ ] The finding is a toast (GG-16) and **never a modal**
**Verification:** `npm test -- fourStatesMatrix`; all three D40 forms render from a fixture.
**Depends on:** I6. **Scope:** M.

### I8: The Plan object and D28 comprehension
**Spec:** `spec-tree-surface.md` §5.1, §5.2, §5.3, §7.2.
**Acceptance:**
- [ ] A build is laid out without committing: draft / dirty / **Revert** / preview panel, and a Plan that
      outlives the panel
- [ ] The price of a **plan** is shown — three numbers, order-independent — not the price of a node in
      isolation
- [ ] A tier row attributes its requirement naming **exactly one lender, always singular** (the credit is
      `max`, not a sum), and the rule is named in the fiction once, where it first matters
- [ ] A shared plan carries **no price**; an imported plan is priced on arrival, under the §5.3 URL
      grammar; the draft preview reports what a change would **close**
**Verification:** tests 18–19; two orderings of the same plan price identically.
**Depends on:** I7, D4. **Scope:** M.

### I9: Focus, and the distance presentation
**Spec:** `spec-tree-surface.md` §6, §9.
**Description:** The Focus line — `1/H` as prose, the effective number of paths.
**Acceptance:**
- [ ] Focus renders what `tree-resolve` returns and **never re-derives it**, so a dial change moves the
      line with no FE edit
- [ ] Focus **moves while the draft is edited**, both halves together (M8 / GG-33)
- [ ] The `UnitClass` union is **byte-identical** before and after — the fractional path count is prose,
      not a `Magnitude`, and no fourteenth unit class is introduced
**Verification:** `npm test -- magnitudeGuard`; tests 10, 14, 15.
**Depends on:** I6, B6. **Scope:** M.

### I10: The authored naming swap
**Spec:** `spec-tree-surface.md` §15, §17 Q1.
**Description:** §15 files the naming decision under *Ask first*: *"a name is content and the owner's
call, and one is needed before any player text is written."* The default — spec vocabulary until
authored — is workable **only** if a later task applies the authored names. This is that task.
**Acceptance:**
- [ ] Every player-facing string for the three currencies and the two tracks comes from one vocabulary
      module, so the swap is one file
- [ ] `vocabularyGuard` fails when a bare *points* reaches player text
- [ ] The swap moves no test id and no query selector
**Verification:** `npm test -- vocabularyGuard`; a diff of the swap touches one file.
**Depends on:** I3. **Scope:** S.

### ✅ Checkpoint I — playable
- [ ] Browse, plan, spend, and understand why a tier is locked — with the game closed
- [ ] Every web guard suite and the e2e volume fixtures green
- [ ] Owner eyeball pass

---

## Phase J — volume

The only phase whose cost is measured in days of machine time.

### J1: The elemental and status corpus
**Spec:** `spec-tree-plan.md` §7.1; `spec-tree-language.md`.
**Acceptance:**
- [ ] 27 trees × 40 nodes emitted, generated, bound, gated
- [ ] `R-G1` — not a schedule note — refuses any tree whose gate quantity is still `pending`
- [ ] The same gate bar as H9: every gate green, the gating metric measured
**Verification:** `--check` green; all 27 resolve above tier 0 on a seeded save.
**Depends on:** Checkpoint G, Checkpoint H, C2. **Scope:** M (a run).

### J2: The three-tier sampling design and the acceptance numbers
**Spec:** `spec-tree-review.md` §3.1, §3.2, §6.3.
**Description:** Tier 1's four census populations (exclusion nodes, escalated, unresolved votes, review
queue), tier 2's 60-tree stratified cluster sample through the **shipped**
`sampling.stratified_sample`, tier 3's ~200 nodes over rare quota cells — the tier that catches *"every
`frostbite` node is the same sentence"* — and the acceptance table.
**Acceptance:**
- [ ] Draws go through `sampling.stratified_sample` — **no second sampler is written**
- [ ] Every non-empty stratum gets at least one sample, and a rare quota cell appears in the tier-3 draw
- [ ] The same draw twice is identical, seeded from `metric id + corpus revision`
- [ ] Sixty clean trees report the **4.87%** bound, computed not tabled; three rejects in sixty is a
      batch reject; every acceptance number resolves from `data/tuning/`, mechanically
**Verification:** the sampler reproduces a draw from a fixed seed; a stripped acceptance key is refused.
**Depends on:** A2, H8. **Scope:** M.

### J3: Escalation, the verdict queue, and the unshippable list
**Spec:** `spec-tree-review.md` §6.1, §6.2, §6.4.
**Description:** Rungs 0–5 — node reject (~3 calls), tree reject (120 calls), cell reject in the plan,
batch reject → reprompt, owner escalation. **Rung 4 is not hypothetical: the demon corpus took it three
times.** Without the ladder, a rejected tree has nowhere to go but a hand edit, which §6.1 forbids.
**Acceptance:**
- [ ] A rejection **names the rule and regenerates**; nothing mutates a draft into legality, and a
      `manualCorrection` is stamped `from`/`to`/`by`/`why` with its rate reported as a metric
- [ ] The verdict queue is a committed machine-readable artifact whose reject reasons become the next
      run's anti-motifs — a review producing no artifact did not happen
- [ ] An exclusion printed on one side only, naming two different winners, or whose loser is marked
      un-unlocked rather than **inert**, denies the lot a pass (`PassiveTree/ExclusionPresentation`,
      which gates)
- [ ] A well-presented `nullification` **ships** — stated as a test, so the withdrawn rule cannot creep back
**Verification:** the nine unshippable conditions each deny a fixture lot; a fixture rejection walks the
ladder to the right rung.
**Depends on:** J2, H6. **Scope:** M.

### J4: Incremental `O(diff)` re-review, and `provenance-supersede`
**Spec:** `spec-tree-review.md` §8; `spec-species-tree.md` §8.
**Description:** §8's opening line is the module's objective: *"make the second pass cost `O(diff)`."*
The diff card as a second mode of the same card, the `trees review --diff <fromRev> <toRev>` verb, and
the `catalog_revision (from, to)` lot identity. **Raise `provenance-supersede` as a hard blocker at task
start:** `ProvenanceLedger.record` raises on a re-recorded row, and pass two cannot run without it, while
J9 budgets 2–3 passes.
**Acceptance:**
- [ ] A magnitude retune produces an **empty** human review queue, proven by test — this is what makes
      F6's D42 republish cheap
- [ ] A renamed node id produces a **full tree diff** — the id-stability dependency proven, not assumed
- [ ] A changed node is judged **inside its tree**, never as an isolated line
- [ ] `provenance-supersede` is either built or recorded in the plan's Risks table as blocking pass 2
**Verification:** a retune fixture and a rename fixture produce the two opposite queues.
**Depends on:** J3, C5. **Scope:** M.

### J5: The species planner — roster, favour cell, rebalance, drift
**Spec:** `spec-species-tree.md` §2.1, §3.1, §3.2, §4.
**Description:** The deterministic, model-free half of the species pipeline. Roster from `_index.json`
with every file walked **without the `_` skip**; one `mechanicalFavour` cell per species plus 2–3
alternates from the same quota — the shape that makes the 166× defect impossible; the rebalance on a
forced override; `FavourDrift`.
**Acceptance:**
- [ ] A species on disk but unindexed, or indexed twice, **halts the run naming both paths** — never
      *"pick the first one"*
- [ ] `mechanicalFavour` is its **own field**; the anchor's `elementPrimary`/`aptitudePrimary` are inputs
      to the brief and never the lock — asserted by test
- [ ] A forced cell returns its draw to the pool; an **overdrawn** forced quota is **refused with the
      rule named**, not rebalanced silently; every alternate offered is inside the quota
- [ ] `FavourDrift` is symmetric: an injected 30% element skew fails it, and so does overshoot
**Verification:** `the_plan_is_reproducible_from_species_id_alone`; a skewed fixture roster.
**Depends on:** A2, H4. **Scope:** M. **Files:**
`tools/seedsmith/seedsmith/adapters/trees/species/plan.py`, `roster.py`.

### J6: `PassiveTree/SpeciesUniqueness` and the marking rules
**Spec:** `spec-species-tree.md` §5.1, §5.3 rules 3–5.
**Description:** The gate and its reverse index, and the rule that decides **which** nodes carry a
species-namespace affix. Selection happens in the planner, never at generation time, which is what keeps
a later change to `speciesUniqueAffixMin` `O(diff)`.
**Acceptance:**
- [ ] The marked nodes are the **deepest mechanism** nodes, ties on branch order then `nodeKey`, chosen
      in the planner
- [ ] `raising_species_unique_affix_min_never_unmarks_a_marked_node` — the mark set at `k=8` strictly
      contains the set at `k=4`; `speciesUniqueAffixMin = 0` is legal and U1/U2 still gate
- [ ] U1 (no `name`/`flavor` repeats corpus-wide) and U2 (no `(affixIds, quotaCell)` fingerprint in two
      trees) run off the reverse index, and `SpeciesUniqueness` gates none until calibrated
- [ ] U3 reports a finding when any `affix.species.<id>.*` is referenced from another tree
**Verification:** the reverse index over a fixture with two trees sharing a namespace affix.
**Depends on:** J5. **Scope:** M.

### J7: The species-namespace affix corpus (U3's bill)
**Spec:** `spec-species-tree.md` §5.2, §5.3 rule 3.
**Description:** 840 × 8 = **6,720** authored affixes under `affix.species.<speciesId>.*`, against a
shipped authored corpus of **two** in `data/seed/effects/affixes/all.json`. This is the largest
unbudgeted item in the program and it is a run, not a code task.
**Acceptance:**
- [ ] Ids minted once and read back on regeneration — the same R3 contract as node keys
- [ ] The authoring cost is stated in the plan's Risks table **before** the run is scheduled
- [ ] The corpus passes J6's uniqueness gate and the schema audit
**Verification:** a regeneration re-mints no affix id; `--check` byte-identical.
**Depends on:** J6. **Scope:** M (a run).

### J8: `species-tree` — the generation pipeline
**Spec:** `spec-species-tree.md` §3.1, §5.3, §6, §7.1, §7.3.
**Acceptance:**
- [ ] The favour quota assigns **before** generation via `largest_remainder_count`;
      `speciesUniqueAffixMin = 8` is enforced, deepest-mechanism-first
- [ ] One `codexSummary` per species, passing the schema audit (≤140 chars, no number, no channel id)
- [ ] The run is resumable — `run start/pause/resume/rerun` with no duplicate provenance row, proven by a
      mid-run kill test
- [ ] Families are **excluded from the roster** until a closed taxonomy exists (698 open tokens)
**Verification:** a killed and resumed run produces the same output as an uninterrupted one.
**Depends on:** J5, J6, J1. **Scope:** M.

### J9: The species corpus run
**Spec:** `spec-species-tree.md` §7.1, §7.2; success criterion 7.
**Acceptance:**
- [ ] 840 trees × 40 nodes committed as catalog data (D45)
- [ ] The plan regenerates byte-identically (`--check`), for species as well as the generic corpus
- [ ] The uniqueness gate holds across all 840; no near-duplicate cluster
**Verification:** `--check` green; the reverse index reports no cross-namespace reference.
**Depends on:** J7, J8, J4 (pass 2 cannot start without `provenance-supersede`). **Scope:** M (a run —
days of machine time, not of authoring).

### J10: The full census
**Spec:** `spec-tree-review.md` §2, §3; `spec-species-tree.md` §7.2.
**Acceptance:**
- [ ] Every tree judged, at the H8-measured rate, under J2's three-tier design
- [ ] The 39 shared generic trees are their **own** census lot, with their own sheet and queue, in
      category waves
- [ ] The acceptance record says **"every tree was judged"**, never "the catalog was reviewed"
- [ ] Escalations resolve through J3's ladder; no lot ships under any of the nine unshippable conditions
**Verification:** the census refuses any lot with no `sheetRead` row (H7).
**Depends on:** J3, J9, G7. **Scope:** M.

### ✅ Checkpoint J — ship
- [ ] Full corpus reviewed; escalations resolved through the ladder, not by hand edits
- [ ] **This is the irreversible point** (D24) — after players build against these ids, a change is a
      migration. Owner sign-off required.

---

## Non-blocking asks (tracked, not gating)

Every row has a default, so nothing here blocks a task.

| Ask | Default if unanswered | Resolver |
|---|---|---|
| The 17th atom kind (D16) | The binder refuses conversion nodes, as specified (B4) | Owner, via `decisions.md` |
| `demonType` / `aspect` / `uniqueDemon` point rates | Commander's 11 until swept (C6) | `squad-harness` F4 |
| `legitimateSkew` rows | Uniform, with `earth` at D32's worked 1.5× | Owner, after the corpus exists |
| Player-facing naming | Spec vocabulary until authored; **I10 applies the names when they land** | Owner, before I3 ships text |
| Does the L2b resist path read status-granted resist channels after G1? | Contribute everything including `status.resist.*`, with a dedicated feedback-path test | Owner — it changes shipped resist math |
| Does `mechanism-wiring` take `aura-skill` T13's live-toggle scope? | Take the per-round recompose only (E3); leave the toggle to T13 | `aura-skill`'s ack |
| Is the transfer verdict scored against mirror squads or authored waves? | Mirror squads decide; waves reported beside (F2) | Owner |
| Does D15's equal-budget rule change once S4's evidence lands? | Keep the equal-budget rule | Owner, after F6 |
| Is tree respec priced off its own soul counter or the species counter? | Its own counter | Owner, before C10 persists it |
| The `DemonsPage.tsx:367-388` volume defect the Codex route hangs off | I5 ships without the Codex entry point and the route is added after | Owner — another program's file |

**Unowned prerequisite, recorded so it is visible:** A10b — the shipped stacking-status vehicle — needs
G1 and G2 **plus a Battle status → `BattleDerivedModifierLedger` producer that no module's
modified-files table contains.** `BattleStatusSpec` carries no `StatMods` and
`BattleDerivedModifierLedger.Add` has one caller. G1 and G2 are necessary and not sufficient. A10a (F3)
is unaffected and needs none of it.
