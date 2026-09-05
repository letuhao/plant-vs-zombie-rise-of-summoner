# 21 — Plan coverage against the four core specs

**Status:** audit, 2026-09-05. Read-only. Nothing in `src/`, `tools/`, `tests/`, `data/`, the specs or
the task files was touched.

**What was audited.** [`tasks/passive-tree-plan.md`](../../../tasks/passive-tree-plan.md) and
[`tasks/passive-tree-todo.md`](../../../tasks/passive-tree-todo.md) (27 tasks, 6 checkpoints, phases
A–F) against four module specs, read in full this session:

- [`spec-tree-catalog.md`](../../architecture/passive-tree/spec-tree-catalog.md) (585 lines)
- [`spec-tree-state.md`](../../architecture/passive-tree/spec-tree-state.md) (1,005 lines)
- [`spec-tree-binder.md`](../../architecture/passive-tree/spec-tree-binder.md) (967 lines)
- [`spec-tree-resolve.md`](../../architecture/passive-tree/spec-tree-resolve.md) (859 lines)

**Method.** For each spec: enumerate its requirements (numbered rules, Design sections that describe
work, the Testing/Boundaries/Success blocks, and every row of Decisions-implemented that implies an
implementation), then find the delivering task. A requirement carried only by a checkpoint bullet or
by the standing verification block is **PARTIAL at best** — a checkpoint verifies, it does not build.

**Headline.** 90 requirements enumerated: **15 COVERED, 24 PARTIAL, 51 MISSING.** Three of the four
specs are referenced by exactly one task each (A2, A5, A6), and one task each is far too few. Two
whole mechanisms — **cross-unlock (D28)** and **the soul track (D3)** — have no delivering task at
all, and the tuning file every other task reads (`data/tuning/passive-tree.v1.json`) is created by
nobody. Four acceptance criteria contradict the spec they cite.

---

## 1. `spec-tree-catalog.md` — 2 COVERED · 11 PARTIAL · 12 MISSING

Referenced by one task: **A2**, scoped to §2–§3. §4 (migration), §5 (where it ships) and §6 (the load
path and its importer) are outside that scope and outside every other task.

| # | Requirement (spec §) | Status | Task, or the gap |
|---|---|---|---|
| C1 | §1 freeze line: layer (c) is empty; a `skill` container uses the fixed core alone, so `prefix_rolls = suffix_rolls = 0` and the draw never runs | PARTIAL | A2 builds the record; nothing asserts the no-roll property, which is what D24 rests on |
| C2 | §2.1 `TreeRecord` shape; `gateQuantity` is stored even when its counter does not exist yet (D37) and the tree is never disabled for it | PARTIAL | A2 covers records generally — and its second acceptance bullet **contradicts this** (§6) |
| C3 | §2.1 / R7 five-value `category` enum; the importer maps the plan's `aptitude`/`demonFamily` tokens and refuses any token outside the map, naming it | MISSING | No task. Spec test `a_plan_category_token_outside_the_five_is_refused_naming_it` |
| C4 | §2.2 `NodeRecord` shape — `nodeClass`, `affixIds[]` 1..3, `budgetShareMilli` carried verbatim (R4/R5) | PARTIAL | A2 names `affixIds` 1..3 only; `nodeClass` and `budgetShareMilli` unnamed, though A4 reads the latter |
| C5 | §2.2 / D40 `exclusionForm` enum, and the refusal when form and `excludeProps` disagree | PARTIAL | Named in A2's Description, absent from its acceptance criteria |
| C6 | §2.3 `NodeAtom` shape — `kMicro` `long`, `unitClass` stored, 11 authorable triggers, `whenJson` | PARTIAL | A2 covers `kMicro is long`; the rest unnamed |
| C7 | §2.3 `soulCurveId` — a curve reference, never a formula (D3) | MISSING | No task mentions the soul track's curve reference |
| C8 | §2.4 `scaleAxis` stored as a function of `UnitClass`; the axis/class agreement refusal; a sigmoid channel never carries the `PTheta` axis | MISSING | No task. This is the refusal that catches a **silent** failure (§2.4) |
| C9 | §2.5 the potency ceiling is `budgetShareMilli > potency.maxNodeShareMilli`, both ‰ of one branch | MISSING | A2 asserts the **superseded** `kMicro`-vs-ceiling form the spec calls a dimensional error (§6) |
| C10 | §2.5 `--check` proves the emitted `kMicro` is what the derivation chain produces; `--explain` prints the chain | MISSING | D4's `--check` is the plan's byte-identity gate, not the kMicro reproduction proof. No `--explain` anywhere |
| C11 | §3 id grammar (no dot in the body), slug composition, `IdMismatch` kept as authored | PARTIAL | A2 covers "an id violating the grammar"; `IdMismatch` (kept, not rewritten) unnamed |
| C12 | §3.1 / R3 `nodeKey` minted once and **read back** on regeneration, never recomputed | COVERED | A1 (refuses to mint over an existing key) + D4 (re-mints no id) |
| C13 | §4 R1 an id is never reused and never renumbered; inserting a node changes no existing id | MISSING | No task. Spec tests `inserting_a_node_does_not_change_any_existing_id`, `a_retired_node_keeps_its_id_and_is_never_reissued` |
| C14 | §4 R2 a removed node is retired, not deleted: `enabled: false`, `retiredAtRevision` set, renders greyed with its retirement printed | PARTIAL | A2's verification says a retired node "renders red rather than throwing" — the load behaviour only, not the retirement write path |
| C15 | §4 R3 an allocation naming a retired node is displayed invalid, never silently repaired, grants nothing, costs nothing to hold | MISSING | No task on either side (catalog or `tree-state` §2.1) |
| C16 | §4 R4 a revision that retires an **allocated** node grants a free full respec | MISSING | No task. This is the whole migration escape hatch |
| C17 | §4 R5 an id no catalog revision ever had is rejected **once, at import**, never per actor load | COVERED | A2 bullet 3. (The "every offender named in one report" half is not stated — see A8 below) |
| C18 | §4 R6 a magnitude retune touches no id and migrates nothing | MISSING | No task. This is the property that makes a live game tunable |
| C19 | §4 filename `v{n}` equals the `catalogVersion` field (the `classes.v2.json` trap) | MISSING | No task |
| C20 | §5 `data/seed/` → `data/generated/` layout, committed output, C# generator, `--check` as a CI staleness gate | PARTIAL | A1 creates `tools/PassiveTreeGen`; D4 commits a corpus. No task wires the catalog's own `--check` into CI |
| C21 | §5 the frozen property registry exists **before** the first node text is authored | PARTIAL | D2 ("the plan emits the closed property set before any node text exists"); the frozen `_registry/properties.v1.json` file and its immutability are unnamed |
| C22 | §5 `data/tuning/passive-tree.v1.json` under R2's canonical key names, each carrying its unit | MISSING | **No task creates this file.** Verified absent: `data/tuning/` has no `passive-tree*` today |
| C23 | §6 a boot-time importer inside `FusionRpg.Data`, one all-or-nothing transaction, `catalog_revision` bumped once | MISSING | No task. A2's Files line is `src/FusionRpg.Core/PassiveTree/` and `data/seed/` — no Data-side work anywhere in the plan |
| C24 | §6 the fourteen load-path refusals, each with a test, none of them clamping | PARTIAL | A2 names three, one of which is wrong (C9) and one of which the spec does not have (C2) |
| C25 | Success criteria: reflection proves every stored magnitude field is `long` and no resolved magnitude is stored | PARTIAL | A2 asserts `kMicro is long` for one field; the reflection sweep is the criterion |

---

## 2. `spec-tree-state.md` — 5 COVERED · 4 PARTIAL · 13 MISSING

Referenced by one task: **A5**, scoped to §3–§5 in its header but with acceptance criteria drawn from
§1, §2 and §6 only. §2.4 (the `selfSpent` projection `tree-resolve` depends on), §3
(`skillPointsPerThetaMilliByScope`), §5 (respec), §8 (tunables) and §9 (the two SSOT rows) have no
delivering criteria.

| # | Requirement (spec §) | Status | Task, or the gap |
|---|---|---|---|
| S1 | §1.1 `rpg_tree_node_state`, sparse, inputs only, `(scope, scope_key, node_id)` key + index | COVERED | A5 |
| S2 | §1.1 row presence means owned; **a node owned with `soul_level = 0` persists** | PARTIAL | A5 says "only non-zero soul levels persist", which contradicts §1.1 and its test `owned_with_zero_souls_persists` (§6) |
| S3 | §2 `cost(N) = first + (N−1)·step`; derive budget and spend on read; no stored balance | COVERED | A5 |
| S4 | §2 the order-independence lemma, as a named test | COVERED | A5's verification line |
| S5 | §2.1 the five ownership rows as `const`s with reasons; `an_item_swap_does_not_change_the_next_node_price_net` | MISSING | No task. The invalid-node and soul-level exclusions are what stop a pure penalty |
| S6 | §2.2c the reward-spread band test over `tree-plan`'s **actual** width vectors at **every** tier, against `archetype.rewardSpreadMaxRatioMilli` | MISSING | No task. The old endpoint-only test is exactly what missed a 6.0× spread at tier 2 |
| S7 | §2.2d `the_skill_wallet_clears_the_tier_it_just_opened_for_every_shipped_archetype`; `g` reproduces from the corner-share derivation | MISSING | No task. §2.2d's own note: the existing derivation "had no test at all" |
| S8 | §2.3 the soft-bound proof: no `Math.Min` on the price, no narrowing cast on the budget, no `CanUnlock` that can return false — each a named test, plus the PS-8 exemption comment | MISSING | No task. The standing verification runs audits, not these greps |
| S9 | §2.4 the `selfSpent` projection (D8/D39): `(n_i, s_i)` per tree, four membership rules, three named tests | MISSING | **No task.** A6 says `H` reads the final allocation; nothing supplies the projection it reads |
| S10 | §3 `pointEconomy.skillPointsPerThetaMilliByScope` + `PointBudget.SkillPointsFor`; a missing rate is a load rejection naming it; every actor reads its own scope | MISSING | No task. Without it fifty demons each read `Θ_player` — §3's stated failure |
| S11 | §4 `LoadTreeState` never throws on an unknown id; the three-way live/retired/unknown reconciler | PARTIAL | A2 covers the import boundary; `TreeStateReconciler` is unbuilt |
| S12 | §4 an unknown id rejects the **import**, naming every offender | COVERED | A2 |
| S13 | §5 respec: full reset, one transaction, per `(scope, scope_key)`, never refused, priced in souls off its own counter | MISSING | No task. A5's "respec round-trip costs identically" tests the cost lemma, not the respec operation |
| S14 | §6 `LoadTreeStateBatch` — one query, one lock, one connection for a six-actor squad | COVERED | A5 |
| S15 | §6 the single-key loader exists for the editing surface only; a seam test proves the battle path never loops it | PARTIAL | A5 covers the batch half; the seam test is unnamed |
| S16 | §6 tree state is not joined onto the unpaged `ListDemonRoster` | MISSING | No task states the boundary |
| S17 | §7 `long` on both sides, `checked` products, `GetInt64` never `GetInt32` | PARTIAL | Inside A5's build; asserted only by the standing verification |
| S18 | §8 the tunable table: `unlockCost.firstPoints`/`stepPoints`, `soulTrack.thetaPerSoulLevelMilli`, the scope table in `aptitudes.v{n+1}.json`; T5 rejection; `1000 ⇒ Ws = 1` pinned | MISSING | Same missing file as C22 |
| S19 | §9.1 `ssot-power-scale.md` §10.2 **row 29** for D25's cost ladder, plus the `inventory.json` mirror row | MISSING | No task. Verified: §10.2's highest row is 28 today, and `inventory.json` has 27 rows, none passive-tree |
| S20 | §9.2 the **§11.10** caps-register row for the unlock price | MISSING | No task |
| S21 | Success: 2,000 actors × 40 nodes stores 80,000 rows, not 3.1 million, proven by a row count | MISSING | No task |
| S22 | Success: `every_actor_reads_its_own_scope_budget` — a demon reading `Θ_player` fails | MISSING | Follows S10 |

---

## 3. `spec-tree-resolve.md` — 4 COVERED · 5 PARTIAL · 13 MISSING

Referenced by one task: **A6**, scoped to §3 and §5. §4 (cross-unlock), §6.2 (the soul→`Θ` read), §8
(tunables), §11 (memoisation) and most of §12's twenty tests have no delivering criteria.

| # | Requirement (spec §) | Status | Task, or the gap |
|---|---|---|---|
| R1 | §2.1 a producer of `BoundDerivedAtom`s fanned into the existing `boundDerivedAtoms` delegate (lawn/web compose) | COVERED | A6 |
| R2 | §2.1 `TreeAtomSource` — the battle adapter emitting `BattleChannelMod`; `Lawn_and_battle_resolve_to_the_same_totals` | MISSING | No task. A6 proves one path, not the parity of two |
| R3 | §2.2 no fourth subsystem, no new order band, no eviction by `SubsystemId` | COVERED | A6 ("through no new subsystem") |
| R4 | §2.2 `SourceId = tree.{treeId}.{nodeId}` on every contribution (GG-49) | MISSING | No task; `tree-surface` then needs a retrofit |
| R5 | §3.1 `req(t)`, the ascending integer loop, the authored depth read from the catalog and never typed | PARTIAL | A6 covers `req(t)` reading aptitude points; `Tier_gate_reads_the_catalog_depth_not_a_literal` is unnamed |
| R6 | §3.1 test 1 — `W(t)/req(t)` identical at every tier, in the resolver | PARTIAL | A1 asserts the same property over the plan's column; the resolver-side assertion is a different test |
| R7 | §3.2–§3.3 the gate reads **aptitude points**, never the skill wallet (test 3b) | COVERED | A6 |
| R8 | §3.3 `TreeResolveReport` carries **which kind of zero** a tier-0 tree is, read from the catalog and never inferred | MISSING | No task. Success criterion 8 (surface renders from the report without recomputing) has no producer |
| R9 | §4 cross-unlock (D28): largest posture-mate, exactly one lender, never a sum; `stanceGroup` read from the catalog; no group ⇒ credit 0; no k-way compounding; the max-vs-sum fixture | MISSING | **No task builds it.** E4 renders "exactly one lender… the credit is `max`, not a sum" against a mechanism that does not exist |
| R10 | §5.1 `H = w·H_nodes + (1−w)·H_souls`; no `1/n` normalisation; empty denominators read zero, never uniform | PARTIAL | A6 covers `H` order-independence; the blend, `w`, and the empty-reads-zero rule are unnamed |
| R11 | §5.1 `F = 1 + (Fmax−1)·H`, `F ∈ [1, Fmax]` — both bounds | COVERED | A6 (named test) |
| R12 | §5.2 `H` reads `tree-state`'s `selfSpent` projection; the resolver infers no provenance | PARTIAL | Blocked by S9 — the projection has no supplier |
| R13 | §5.3 `F` multiplies every tree-derived contribution and **nothing else**, in both read modes | MISSING | No task |
| R14 | §5.4 `Fmax = 1000‰` is a legal, tested configuration that removes `F` without removing a code path | MISSING | No task. This is what makes withdrawing `F` a tuning change rather than a refactor |
| R15 | §6.1 PS-3 line by line: magnitudes read `P(Θ_node)`, contests read `Θ_node` linearly (test 12) | MISSING | No task. The failure is silent — the sheet number rises and the multiplier does not |
| R16 | §6.2 `Θ_node = Θ_actor + Ws·soulLevel`; the coefficient never moves; power linear in effort (tests 10, 11) | MISSING | **No task.** D3's deepen track is unbuilt end to end |
| R17 | §6.2 the `ssot-power-scale.md` §10.2 row for `Ws` | MISSING | No task; `guard-power.ps1` provably cannot catch its absence (§7) |
| R18 | §7 `decimal` widening, divide once and last, overflow throws (tests 13, 13a, 14) | PARTIAL | Inside A6's build; asserted only by the standing verification |
| R19 | §8 the four tunables in `data/tuning/passive-tree.v1.json`; T5 rejection; `TreeReadFunctions` is a literal-free balance surface | MISSING | Same missing file as C22 |
| R20 | §11 memoise by reference, re-resolve on a changed state reference (test 20) | MISSING | No task. The perf SSOT says an uncached resolve is the failure mode |
| R21 | §12 tests 17–18: an excluded node contributes zero and is reported (all three D40 forms, nullified ⇒ **inert**); a closed gate invalidates rather than repairing | MISSING | E3 renders inertness; nothing produces it |
| R22 | §12 the four hand-written mutants (max→sum, divide order, wallet-in-gate, points-paid in `H_nodes`) | MISSING | No task |

---

## 4. `spec-tree-binder.md` — 4 COVERED · 4 PARTIAL · 13 MISSING

Referenced by two tasks: **A3** (`PowerLadderKMicro`) and **A4** (the coefficient). §4's thirteen-class
legality table, §5's soul track and §6's mechanism-pricing rules are only fractionally covered.

| # | Requirement (spec §) | Status | Task, or the gap |
|---|---|---|---|
| B1 | §3.1/§3.3 the formula, one division, round half away from zero via the shipped helper | COVERED | A4 |
| B2 | §3.3 / R4 `budgetShareMilli` read as given; **no** `tierWeight`, `weightTotal` or `w[t]` in `CoefficientBinder` (source-shape test) | PARTIAL | A4's Description says it; the source-shape test — the only thing that catches a defect invisible to value tests — is not an acceptance criterion |
| B3 | §3.3 `channelAnchorMilli` derived from `power-scale.v{n}.json`'s own pins at bake time, never authored | MISSING | No task. Without it a dial change leaves the anchor stale |
| B4 | §3.4 the worked example reproduces (share 45 → 3,038; the sibling at 46 → 3,105) | COVERED | A4's verification |
| B5 | §3.5 `PowerLadderKMicro` + the `/1_000_000` arm; no archetype stores `0` | COVERED | A3 |
| B6 | §5.3 / §7 widening `AtomCompiler`'s result from `int` to `long` (moves the first refusal from `Θ` 103,557 to ≈214M) | MISSING | A3 widens the multiply, not the result. `tree-state` §7 names the same wall |
| B7 | §3.6 `treeShareMilli` as a tunable in `passive-tree.v1.json` | MISSING | Same missing file as C22 |
| B8 | §4.1 an explicit verdict for **all thirteen** `UnitClass` values | PARTIAL | A4 covers four and mis-states the split (§6) |
| B9 | §4.2 the three silent-failure classes refuse loudly (`SigmoidMultiplierPoints`, capped `StatusPotencyPoints`, `LowerIsBetter`) | MISSING | Not in A4's criteria |
| B10 | §4.2 all **five** `LowerIsBetter` primaries refuse a `+X` | MISSING | No task |
| B11 | §6 M3 no `More` op on a derived channel — refused at bind, with the rule named | MISSING | No task |
| B12 | §5.1 the soul track: `Θ_node` from `thetaPerSoulLevelMilli`, the second division declared legal in a comment, `theta_per_soul_level_is_read_as_per_mille` | MISSING | No task |
| B13 | §5.2 `power_is_linear_in_souls_spent`; `soul_level_offsets_theta_never_the_coefficient` | MISSING | No task |
| B14 | §5.4 the one `ssot-power-scale.md` §10.2 row for `soulTrack.thetaPerSoulLevelMilli` | MISSING | No task |
| B15 | §6 M2 reflect is lawn-only; `reflect_is_not_priced_as_squad_measurable`, with the missing reader named | MISSING | No task — and B4 (`squad-harness` A10a) scores through the battle/sim path, where a reflect node contributes exactly zero |
| B16 | §6 M3a `parry.break` / `block.break` are granted flat, never ladder-scaled | MISSING | No task |
| B17 | §7 a conversion node is refused with the 17th-kind reason | COVERED | A4 |
| B18 | §7.2 the refusal names the unspent budget; the run's verdict is `FAIL`; `tree-plan` gains a suppression flag | MISSING | A4 refuses; nothing reports the hole, so a tree can quietly ship under its own budget |
| B19 | §7.3 / D40 an excluded node — nullification included — **binds normally**, same `kMicro`, same budget | MISSING | No task. The spec adds §7.3 precisely to stop someone refusing it by analogy with §7 |
| B20 | Commands: `tools/TreeBinder --check` (byte-identity) and `--explain` (the full chain) | PARTIAL | D4's `--check` is corpus-level; no `--explain` |
| B21 | §1/§3.1 a node is 1..3 affixes inside a `skill` container; `AffixComposer` maps affix → atom rows and checks op/kind legality | PARTIAL | A2 covers the `affixIds` count; A4 lists **no files at all**, so the composer has no home |

---

## 5. What the plan claims that its spec does not support

Four acceptance criteria contradict the spec they cite. All four are cheap to fix and all four would
ship a defect the spec explicitly documents.

| Where | The claim | The spec | Consequence |
|---|---|---|---|
| **A2**, bullet 2 | "Load refuses … a `kMicro` over the ceiling" | §2.5: there is no such check. `kMicro` is a **post-anchor per-million** number and the ceiling is a **per-mille budget share** — "comparing them was a dimensional error, not a rounding one". The check is `node.budgetShareMilli > potency.maxNodeShareMilli` | Builds the superseded check the spec was rewritten to remove, and silently applies a per-channel anchor factor (~0.135 on atk) to one side of a comparison |
| **A2**, bullet 2 | "Load refuses … an unknown `gateQuantity`" | §2.1: the catalog "stores the name either way and **never disables a tree for it**" — a tree naming `element_mastery` or `status_applied.<id>` is "waiting, not orphaned" (D37) | Would refuse the 27 trees `gate-counters` exists to unblock, at import, before phase C ships |
| **A4**, bullet 2 | "the four `UnitClass` values that accept a `+X` node pass, **the others are refused**" | §4.1 counts 3 + 1 + 3 + 6 = 13: three accept a ladder-scaled `+X`, a fourth accepts a **flat per-mille** `+X`, and **three accept a `Θ`-linear point grant** (`SigmoidPoints`, `SigmoidMultiplierPoints`, `StatusPotencyPoints`) | Refusing "the others" bans every contest node — the exact channels `tree-resolve` §6.1 says a node writes and reads `Θ_node` linearly. It would delete accuracy, dodge, crit and status power from the corpus |
| **A5**, bullet 1 | "only non-zero soul levels persist" | §1.1: "a node owned but never soul-levelled has a row with `soul_level = 0`" — a real state, with its own test `owned_with_zero_souls_persists` | Owning a node without deepening it would leave no row, so the node is unowned on the next load |

One more, softer: **A5's** "Row presence means owned; no `owned` column" is right, and the sparsity rule
it is trying to state is *"only non-zero **entries** persist"* — rows for nodes the actor does not own.
The fix is one word.

---

## 6. Ranked missing tasks, in the todo's own format

Ranked by what blocks the most downstream work. Paste-ready. Ids continue phase A because every one of
them is spine work that Checkpoint A's claim ("a trait allocated on an actor changes a number in a
battle, end to end") either depends on or overstates without.

### A7: `data/tuning/passive-tree.v1.json` and its loader
**Spec:** `spec-tree-catalog.md` §5, `spec-tree-state.md` §8, `spec-tree-binder.md` §3.6,
`spec-tree-resolve.md` §8. **Rank 1 — three tasks already read keys from a file nobody creates.**
**Description:** The program's one tuning file, under ruling R2's canonical names, each key carrying
its unit: `concentration.fmaxMilli`, `concentration.wMilli`, `tierLadder.reqScalePoints`,
`soulTrack.thetaPerSoulLevelMilli`, `unlockCost.firstPoints`, `unlockCost.stepPoints`,
`potency.maxNodeShareMilli`, `treeShareMilli` — plus `PassiveTreeTuning` and its loader. Verified
absent 2026-09-05: `data/tuning/` holds no `passive-tree*` file.
**Acceptance:**
- [ ] Every key above loads with the standard `schemaVersion` / `version` / `_meta` header
- [ ] A **missing** key is a load rejection naming it — never a built-in default (T5)
- [ ] `thetaPerSoulLevelMilli = 1000` gives `Ws = 1`; writing `1` gives a thousandth, and a test pins it
- [ ] No superseded name (`Fmax`, `w`, `Ws`, `tierLadder.k`, `nodePotencyCeiling`, `unlockCost.first`,
      `soulThetaWeight`, any `passive-tree-gen.v1.json`) appears in code, config or a test fixture
**Verification:** a fixture with one key removed fails naming that key; `audit-magic-numbers.py` shows
no M1/M2 in the passive-tree namespace.
**Depends on:** none. **Scope:** S–M. **Files:** `data/tuning/passive-tree.v1.json`,
`src/FusionRpg.Core/PassiveTree/State/PassiveTreeTuning.cs`.

### A8: The catalog import transaction and the unknown-id report
**Spec:** `spec-tree-catalog.md` §6, §4 R5; `spec-tree-state.md` §4. **Rank 2.**
**Description:** The boot-time importer inside `FusionRpg.Data` that turns committed generated files
into rows in one all-or-nothing transaction, bumping `catalog_revision` exactly once. A2 asserts import
behaviour today with no importer in any task's Files line.
**Acceptance:**
- [ ] Import is all-or-nothing and bumps `catalog_revision` **once** per transaction
- [ ] Every id no catalog revision has ever had fails the import, with **every** offender named in one
      report — and every actor stays loadable
- [ ] The remaining §6 refusals each have a test, and none of them repairs, defaults or clamps
- [ ] All SQL lives in `FusionRpg.Data`; the generator in `tools/` opens no connection
**Verification:** `guard-dal.ps1` green; a fixture with two bad ids names both in one report; a partial
failure leaves the revision unchanged.
**Depends on:** A2. **Scope:** M. **Files:** `src/FusionRpg.Data/Sqlite/RpgStore.TreeCatalog.cs`,
`tests/FusionRpg.Data.Tests/PassiveTree/TreeCatalogImportTests.cs`.

### A9: Channel legality — all thirteen `UnitClass` verdicts, and the anchor
**Spec:** `spec-tree-binder.md` §3.3, §4.1, §4.2, §6 M3a. **Rank 3 — it also fixes A4's wrong criterion.**
**Description:** `ChannelLegality` keyed by `UnitClass`, with an explicit verdict for each of the
thirteen: three ladder-scaled, one flat per-mille, three `Θ`-linear point grants, six refused. Plus
`channelAnchorMilli` derived from `power-scale.v{n}.json`'s own pins at bake time.
**Acceptance:**
- [ ] All thirteen classes have a verdict; the three ✅ classes bind, `PerMilleRatio` binds **flat only**,
      the three contest classes bind `Θ`-linear and refuse a `P(Θ)` amount with the class named
- [ ] All **five** `LowerIsBetter` primaries refuse a `+X`; a `More` op on a derived channel is refused
- [ ] `combat.parry.break.*` / `combat.block.break.*` refuse a `powerLadder` amount (they are switches,
      not dials — `OverlayCombatCalculator.cs:183-184`)
- [ ] `channelAnchorMilli` moves when `power-scale`'s `atk.pinValue` moves, with no source edit
**Verification:** the three silent-failure classes refuse loudly instead; the anchor test changes a pin
and watches the anchor follow.
**Depends on:** A4. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/Binding/ChannelLegality.cs`.

### A10: `skillPointsPerThetaMilliByScope` and `SkillPointsFor`
**Spec:** `spec-tree-state.md` §3, §8 (D34). **Rank 4.**
**Description:** The scope table on `pointEconomy`, mirroring `AptitudePointsPerThetaMilliByScope` one
line above it, and `PointBudget.SkillPointsFor` as the sibling of `PointsFor`. Without it every actor's
budget reads `Θ_player` and fifty demons own the generic catalog at the calibration point.
**Acceptance:**
- [ ] `pointEconomy.skillPointsPerThetaMilliByScope` ships in `aptitudes.v{n+1}.json` with
      `commander = 11`; the other three carry a stated guess, labelled unmeasured
- [ ] `SkillPointsFor` is the same shape as `PointsFor`: `checked`, `long`, no cap, negative source rejected
- [ ] A missing rate is a load rejection naming it
- [ ] `every_actor_reads_its_own_scope_budget` — a demon reading `Θ_player` fails
**Verification:** the four scopes resolve to four budgets from one actor set.
**Depends on:** A7. **Scope:** S–M. **Files:** `src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs`,
`data/tuning/aptitudes.v{n+1}.json`.

### A11: The `selfSpent` projection (D8/D39)
**Spec:** `spec-tree-state.md` §2.4; read by `spec-tree-resolve.md` §5.2. **Rank 5 — A6's `H` reads it.**
**Description:** The per-tree `(n_i, s_i)` vector derived from the stored node set: self-bought node
count and self-spent soul levels. Four rules, stated identically in both specs so neither can drift.
**Acceptance:**
- [ ] The projection is the **final allocation**, not points paid and not a purchase order
- [ ] A node counts once, at 1 — never weighted by what it cost
- [ ] A tree with no self-bought node is **absent** from the vector, never present at zero
- [ ] The exclusion of item-granted, aptitude-threshold and demon-aspect unlocks is a **stated rule**
      with its own test, so widening it later moves a golden instead of starting an investigation
**Verification:** the same node set built two ways yields one identical vector; the store-side half of
`tree-resolve` test 6c.
**Depends on:** A5. **Scope:** S–M. **Files:** `src/FusionRpg.Core/PassiveTree/State/TreeNodeSet.cs`.

### A12: Cross-unlock (D28)
**Spec:** `spec-tree-resolve.md` §4. **Rank 6 — a whole mechanism with no home, rendered by E4.**
**Description:** `credit(i) = max{ base(j) : j ≠ i, stanceGroup(j) == stanceGroup(i) }`,
`gate(i) = base(i) + credit(i)`. `base` is the tree's own aptitude allocation; `stanceGroup` is a
catalog property, read and never re-declared.
**Acceptance:**
- [ ] Three mates at 40/30/20 credit **40**, never 90 — exactly one lender
- [ ] The same mate vector run through `max` and through `sum` gives **different** answers and the
      resolver returns `max` (a swap is invisible on a one-mate fixture)
- [ ] A four-of-one-stance build's total credit is bounded by its own largest tree
- [ ] A tree the catalog gives no stance group gets `credit = 0`
**Verification:** tests 3, 3a, 4, 5; a hand-written `max`→`sum` mutant turns 3 and 3a red.
**Depends on:** A6. **Scope:** M. **Files:** `src/FusionRpg.Core/PassiveTree/CrossUnlock.cs`.

### A13: The soul track, end to end (D3)
**Spec:** `spec-tree-binder.md` §5.1–§5.4, `spec-tree-resolve.md` §6.2, `spec-tree-catalog.md` §2.3.
**Rank 7 — the entire second progression track is unbuilt.**
**Description:** `Θ_node = Θ_actor + (thetaPerSoulLevelMilli · soulLevel)/1000`, derived at the read
site and never persisted; the coefficient never moves; `soulCurveId` carried on `NodeAtom` as a curve
reference.
**Acceptance:**
- [ ] `kMicro` is byte-identical at soul level 0 and 50; only `Θ_node` moves
- [ ] `thetaPerSoulLevelMilli = 1000` is one `Θ` per level, and the per-mille divide happens once,
      before `P()` is called, with a comment saying why it is legal beside CLAUDE.md rule 4
- [ ] `ΔP / Σcost` is constant across `L` — power linear in effort (§5.2, §10.5)
- [ ] The soul read throws rather than wrapping at `long`, widening before the multiply
**Verification:** tests 10, 11, 13a; `audit-overflow.py` clean.
**Depends on:** A4, A6, A7. **Scope:** M.

### A14: `TreeResolveReport` — the diagnostic projection the surface renders
**Spec:** `spec-tree-resolve.md` §3.3, §12 tests 17–18, success criterion 8. **Rank 8 — blocks E2–E4.**
**Description:** The report carrying the gate, the lender, `H`, `F`, the excluded nodes, and **which
kind of zero** a tier-0 tree is — read from the catalog, never inferred from the zero.
**Acceptance:**
- [ ] A tier-0 tree reports *no aptitude allocated yet* versus *this tree's gate quantity has no
      producer* as a catalog-read value, never an inference
- [ ] An excluded node contributes zero and is reported with the winner named — for reroute,
      precedence and **nullification** alike; a nullified node reports **inert**, never un-unlocked
- [ ] A gate that closed invalidates rather than repairing, and the node contributes zero
- [ ] `tree-surface` can render the gate, lender, `H`, `F` and exclusions **without recomputing** any
**Verification:** tests 17 and 18; a surface fixture renders from the report alone.
**Depends on:** A6, A12. **Scope:** M. **Files:**
`src/FusionRpg.Core/PassiveTree/TreeResolveReport.cs`.

### A15: Catalog versioning and migration — R1 through R6
**Spec:** `spec-tree-catalog.md` §4; `spec-tree-state.md` §4. **Rank 9.**
**Description:** The five migration rules as executable properties, plus the retirement write path.
Today only R5 has a task.
**Acceptance:**
- [ ] R1/R2: inserting a node changes no existing id; a retired node keeps its id, sets
      `retiredAtRevision`, and is never reissued
- [ ] R3: an allocation naming a retired node is displayed invalid, grants nothing, **costs nothing to
      hold**, and is never silently repaired
- [ ] R4: a revision that retires an **allocated** node grants a free full respec, at price zero
- [ ] R6: a magnitude retune changes no id and migrates no per-actor row
- [ ] The filename's `v{n}` equals the `catalogVersion` field, asserted
**Verification:** an insert / retire / retune fixture triple, each leaving every surviving id
byte-identical.
**Depends on:** A2, A5, A8. **Scope:** M.

### A16: `TreeAtomSource` — the battle adapter, parity, and attribution
**Spec:** `spec-tree-resolve.md` §2.1, §2.2, §12 tests 15–16. **Rank 10.**
**Description:** The third source of the shape `TraitAtomSource` and `EquipAtomSource` already ship,
emitting `BattleChannelMod`. Cite `Battle*` by symbol, never by line (R9).
**Acceptance:**
- [ ] The same actor resolved on the lawn and in battle produces the same channel totals
- [ ] Every contribution carries `SourceId = tree.{treeId}.{nodeId}` — one row per node
- [ ] No new subsystem, no new order band, and the existing three registrations are not evicted
**Verification:** test 15's parity fixture; attribution reaches `ChannelContributions` unchanged.
**Depends on:** A6. **Scope:** M. **Files:** `src/FusionRpg.Core/Battle/TreeAtomSource.cs`.

### A17: The state reconciler and the never-throws rule
**Spec:** `spec-tree-state.md` §4. **Rank 11.**
**Description:** `TreeStateReconciler` classifying every stored row live / retired / unknown **in
memory, after the load** — so `LoadTreeState` returns rows and never throws on an unknown id.
**Acceptance:**
- [ ] `an_unknown_node_id_does_not_throw_on_actor_load` — the `AptitudeAllocation.cs:39` defect is not
      repeated at 1,560 ids per actor
- [ ] A retired node loads as invalid and grants nothing
- [ ] The three-way result is what the surface renders; classification happens once per load
**Verification:** a save with one retired and one unknown id loads, and both render.
**Depends on:** A5, A8. **Scope:** S–M.

### A18: Tree respec (D18)
**Spec:** `spec-tree-state.md` §5, §5.1. **Rank 12.**
**Description:** Full reset in one transaction, scoped per `(scope, scope_key)`, never refused, priced
in souls on the shape `RespecPolicy` already ships. The one open question — its own counter or the
species counter — is an owner call before the counter is persisted.
**Acceptance:**
- [ ] `respec_clears_one_scope_key_only`; a roster-wide reset is not a single transaction
- [ ] `respec_is_never_refused` — no "cannot respec" return, matching `RespecPolicy.cs:33-35`
- [ ] Priced in souls, `long` throughout, divided by 1000 last, `checked`
- [ ] Re-buying the same set after a respec costs exactly what it cost before
**Verification:** a respec round-trip and a re-buy; the counter question answered before it persists.
**Depends on:** A5. **Scope:** M.

### A19: The archetype band and the wallet band
**Spec:** `spec-tree-state.md` §2.2c, §2.2d. **Rank 13 — cheap, and it catches a 6.0× skew.**
**Description:** The two tests that read `tree-plan`'s **actual** width vectors at **every** tier
rather than a `k = 4` fixture. The endpoint-only test is exactly what let the skew survive.
**Acceptance:**
- [ ] `reward_per_skill_point_is_within_band_over_every_shipped_archetype_and_every_tier`, against
      `archetype.rewardSpreadMaxRatioMilli`, exactly 1000‰ at tier 10 — green at equality by design
- [ ] `the_skill_wallet_clears_the_tier_it_just_opened_for_every_shipped_archetype`; `g` reproduces
      from the corner-share form `a·corner·step·k²/s` = 10.40, rounded up to 11
- [ ] The narrow constant-width test keeps its scope in its own name and is never read as corpus evidence
**Verification:** both bands computed in exact integer ratios; no float anywhere in either.
**Depends on:** A5, A7. **Scope:** S.

### A20: The `ssot-power-scale.md` rows this program owes
**Spec:** `spec-tree-state.md` §9.1/§9.2, `spec-tree-binder.md` §5.4, `spec-tree-resolve.md` §6.2.
**Rank 14 — nothing in CI will ever notice these are missing.**
**Description:** Four §10.2 rows (`req(t)`, `W(T)`, D36's `unlockCost`, and `Ws`), the §11.10
caps-register row for the unlock price, and the `inventory.json` mirror rows in the same change.
**Acceptance:**
- [ ] §10.2 gains the four rows (row 29 onward — verified: today's highest is 28)
- [ ] §11.10 gains the unlock-price row with its verdict: a soft economic bound, proven, with the three
      forbidden constructions named
- [ ] `inventory.json` mirrors every new row in the same commit (verified: 27 rows today, none passive-tree)
**Verification:** by reading. **`guard-power.ps1` cannot catch any of this** — its G2/G3 method pattern
(`guard-power.ps1:74`) keys on a parameter named `level`, `lvl` or `index`, and `nodesOwned`,
`soulLevel` and `thetaActor` all sit outside it. A green guard is not evidence.
**Depends on:** A5, A13. **Scope:** S. **Files:** `docs/architecture/power/ssot-power-scale.md`,
`docs/architecture/power/inventory.json`.

### Smaller gaps — amendments to existing tasks, not new tasks

| Task | Amendment |
|---|---|
| **A2** | Delete the `kMicro`-over-the-ceiling and unknown-`gateQuantity` criteria (§5). Add: `scaleAxis` stored and validated against `UnitClass` (a sigmoid channel never carries `PTheta`); `exclusionForm`/`excludeProps` agreement; `IdMismatch` kept as authored; the five-value `category` enum and the plan's token map (R7); reflection proving every stored magnitude field is `long` and no resolved magnitude is stored |
| **A3** | Add: widening `AtomCompiler`'s **result** from `int` to `long` (`spec-tree-binder.md` §5.3 / §7, `spec-tree-state.md` §7) — it moves the first refusal from `Θ` 103,557 to ≈214,748,300 and costs one cast |
| **A4** | Fix bullet 2 per §5. Add: the R4 source-shape test (no `tierWeight`, `weightTotal` or `w[t]` in `CoefficientBinder`); an excluded node — nullification included — binds normally with the same `kMicro`; a refused conversion slot's unspent budget is **reported**, and the run's verdict is `FAIL`; `--explain` prints the whole chain. Give the task a Files line (`AffixComposer.cs`, `CoefficientBinder.cs`, `BoundNode.cs`, `tools/TreeBinder/`) |
| **A5** | Fix bullet 1 to "only non-zero **entries** persist; a node owned at `soul_level = 0` keeps its row". Add: §2.1's five ownership rows as commented `const`s plus the item-swap invariant; the three PS-8 grep tests (no `Math.Min` on the price, no narrowing cast on the budget, no `CanUnlock` returning false); the batch seam test; the 2,000-actor row-count proof; do not join tree state onto the unpaged `ListDemonRoster` |
| **A6** | Add: `Tier_gate_reads_the_catalog_depth_not_a_literal`; the `w` blend of `H_nodes`/`H_souls` with empty denominators reading zero; PS-3 line by line (contest channels read `Θ_node` linearly); `F` multiplies only tree-derived contributions; `Fmax = 1000‰` removes `F` byte-identically; memoise by reference and re-resolve on a changed state reference |
| **B4** | State that a reflect node contributes **exactly zero** through the battle/sim path — `TryReflect` has one caller, `CombatDamageDispatcher.DispatchInstant`, and nothing in `src/FusionRpg.Core/Battle/` calls it. A sweep reporting reflect as weak is reporting a missing reader, not a balance finding (`spec-tree-binder.md` §6 M2) |
| **D4** | Add the catalog's own `--check` staleness gate to CI, distinct from the plan's byte-identity check |

---

## 7. Fully covered, verified

Fifteen requirements have a task that builds them, with acceptance criteria that match the spec. No
action needed on any of these.

- **`nodeKey` minted once and read back** (catalog §3.1 R3) — A1 refuses to re-mint, D4 asserts a
  regeneration re-mints no id. The `O(diff)` review property rests on exactly this.
- **Unknown ids rejected once, at import, never per actor load** (catalog §4 R5, state §4) — A2.
- **The sparse table, its key and its index** (state §1.1) — A5.
- **The rising unlock cost, derived on read with no stored balance** (state §2) — A5.
- **The order-independence lemma as a named test** (state §2) — A5's verification.
- **`LoadTreeStateBatch` — one query for a six-actor squad** (state §6) — A5.
- **The fan-in producer through the existing `boundDerivedAtoms` delegate** (resolve §2.1) — A6.
- **No fourth subsystem, no new order band** (resolve §2.2) — A6.
- **The gate reads aptitude points, never the skill wallet** (resolve §3.2–§3.3, D12) — A6.
- **`F ∈ [1, Fmax]`, both bounds, as a named test** (resolve §5.1) — A6.
- **The coefficient formula, one division, half away from zero** (binder §3.1/§3.3) — A4.
- **The worked example reproduces exactly** (binder §3.4) — A4's verification.
- **`PowerLadderKMicro` and the `/1_000_000` arm; no archetype stores zero** (binder §3.5) — A3, and
  A3's own description carries the 12-of-40 and 6-of-40 figures straight from the spec's table.
- **Conversion nodes refused with the 17th-kind reason** (binder §7) — A4, with the non-blocking ask
  and its default already tracked in the plan.
- **The `battle-tempo` citation rule** (binder Boundaries R9) — the todo's standing rule.

---

## 8. Design-gate checklist

```
[x] I identified the subsystem(s): passive trees, derived stats, power scaling,
    atoms/effects, data/SQL, battle.
[x] I read every doc in the DESIGN-GATE §1 rows this session: DESIGN-GATE.md (whole),
    all four module specs in full, the plan and the todo in full, passive-tree-map.md's
    module table.
[x] I checked for a lock covering this. There is still no passive-tree row in
    decisions.md; every spec carries "no build authorized". This audit proposes tasks,
    not builds.
[x] Every factual claim cites a spec section, a task id, or a file:line.
[x] I verified claims against artifacts, not summaries: guard-power.ps1:74's
    methodSigPattern (level|lvl|index) confirmed by reading it; data/tuning/ confirmed
    to hold no passive-tree file; docs/architecture/power/ssot-power-scale.md §10.2's
    highest row confirmed as 28; inventory.json confirmed at 27 rows, none passive-tree.
[x] I read the surrounding section of every rule I quoted — §2.5's "the check, stated so
    it can be implemented", §4.1's 3+1+3+6 count, §1.1's owned-with-zero-souls row.
[~] I tested (not assumed) any constraint I report. PARTIAL: no test suite was run. This
    is a document-to-document coverage audit; the four code-level facts above were read
    from the files, and no claim here rests on a test outcome.
[x] Nothing contradicts a §2 invariant. The four contradictions reported in §5 are the
    plan's, against its own specs, and each names the spec section that settles it.
[x] Corrections are stated where they belong — §5 names the exact acceptance bullet to
    change in each case, and §6's amendment table repeats them beside the task.
```
