# Tasks: action program

**Rewritten 2026-08-27.** Plan: [action-plan.md](action-plan.md) · Map:
[../docs/architecture/action-map.md](../docs/architecture/action-map.md) · Specs:
[../docs/architecture/action/](../docs/architecture/action/) · Ideal: **sealed**.

**36 slices · 11 phases.** Scope: **S** ≈ under an hour · **M** ≈ a focused session · **L** ≈ multi-session.

> ## ⛔ Two rules binding on every slice below
>
> **1. No slice waits on a person.** Every acceptance criterion is a command that exits non-zero. A red test
> stops the build; nothing stops the builder.
>
> **2. Every balance number ships as a tunable with a working value.** PS-7: *"being wrong costs a config
> version, not a refactor."* **Do not wait for data to choose a number — ship a defensible one and record
> the metric that will move it.** What must be right first time is the number's *shape*: `long` not `float`,
> per-mille not fractional, tunable not `const`.

---

## Phase 0 — prerequisites

- [x] **P0.1: extend the purity scan to `Core/Actions/`** *(before the first line of action code)* · **S**
  - Purity rules **on** (wall clock, ambient `Random`, `Guid.NewGuid`, `.GetHashCode(`, floating point,
    dictionary enumeration); tick-path rules **off** — `TargetResolver` needs LINQ.
  - Reuses `DiagnosticsExemptFromTickPath`'s shape: a directory plus an exemption entry, not new machinery.
  - Acceptance: a planted `DateTime.UtcNow` **fails**; a planted ambient `Random` **fails**; a planted
    `.Where(` does **not**. **A guard that cannot fail is decoration.**
  - Verify: `--filter ~ActionsPurityGuardTests` — **corrected 2026-08-28**: the originally-stated
    `--filter ~PurityScan` matches **zero tests** (confirmed live: `--list-tests` prints "No test
    matches"), and always did — `KernelPurityScan` is a static helper with no `[Fact]`/`[Theory]`
    methods of its own, and neither it nor `TimelinePurityGuardTests` (the ORIGINAL kernel-wide guard,
    predating this program) matches "PurityScan" either. The real, passing tests for THIS acceptance
    line live in `ActionsPurityGuardTests.cs`. Fixed the DOCUMENTED command here rather than renaming
    the test class, since `ActionsPurityGuardTests` is cited by name in a dozen other evidence
    paragraphs throughout this file (every T25–T34 entry) and a rename would need to touch all of
    them to stay consistent — unlike T9's fix, where the mis-named file had exactly one reference
    (itself).

### Other programs — this program supplies the requirement and the tests

**2026-08-28: the "requirement and the tests" half is now honored for all four, not just two.**
`RungMonotonicity.PredicatePricingLanded` (P0.3) and `DurationResolverTests`' fixture resolver (P0.5)
already carried this pattern; P0.2 and P0.4 had **no contract artifact of any kind** (confirmed by
repo-wide search, not assumed absent) until `CrossProgramLandedFlags.cs` +
`CrossProgramLandedFlagsTests.cs` were added — a `const bool ...Landed = false` per prerequisite, each
cross-checked against the REAL vocabulary it depends on (the closed `LeafId` set for P0.4, the
`DerivedStatRegistry` for P0.5 — added alongside for consistency even though P0.5 already had partial
coverage) so a landing that forgets to flip its flag fails loudly, plus a comment naming exactly what
flipping each flag commits this program to proving. As of the same day, **all four landed for real**,
under explicit owner authorization to build across the program boundary rather than leave them as
external blockers — see each item's own evidence below. No external blocker remains for this program.

- [x] **P0.2: linkage** — a magnitude that reads `EffectEventDto.Damage` (GAS's `SetByCaller` shape) · **effect-atom** —
      done 2026-08-28, unblocked by building it across the program boundary under the same explicit
      owner authorization as P0.3–P0.5. **Design decision written first** (docs/architecture/effect-
      atom/spec-value-spec-and-curve.md, "Event-linked magnitudes") since — unlike P0.3–P0.5, which
      each already had an approved spec section — action-ideal.md §8.5 explicitly named this "in spec
      phase, after this ideal is sealed," and it touches `ValueSpec`, a documented "sealed contract."
      Scoped to exactly GAS's `SetByCaller` shape ("Ask 1, the small one, take it first" per the
      owner's own two-ask split); GAS's `AttributeBased` shape ("10% of target's max HP", Ask 2) is
      explicitly NOT built — a separate, larger change with nothing forcing it now.
      **Recon before writing any code** (a dedicated Explore pass, not assumption): found
      `ValueSpec.Resolve(IAtomRandom?)` has exactly 2 callers in the runtime and NEITHER has a firing
      combat event in scope — the real "atom fires on OnDamageDealt" path bakes a `Fixed` spec to a
      literal number at CATALOG-COMPILE time (`AtomCompiler.ResolvedParams`), before any event exists,
      and never calls `Resolve` at all. So the feature could not be a new `Resolve` overload; it has
      to defer past compile time to where the compiled params AND the firing event are both already in
      scope: `EffectBag.FireGrant` → `DamagePacketBuilder.FromOverlay(merged, ev, ...)`.
      **Built**: `ValueSpec` gained `EventField`/`MultiplierMilli` (closed to `"damage"` today, mutually
      exclusive with min/max/roll/curve, `Validate()` enforces both); `AtomJson.TryReadValueSpec` gained
      the `{"eventField":"damage","multiplierMilli":500}` grammar branch (checked first, so it never
      falls into the pre-existing "needs an integer 'min'" rejection), with `multiplierMilli` REQUIRED
      whenever `eventField` is present — never silently defaulted, since it is the balance number, not
      a structural constant; `AtomRowValidator` restricts `eventField` to the `resource.delta` kind
      (the kind lifesteal/Corrosion content actually needs), rejecting it at load on any other kind so
      a marker never reaches a sink with no idea how to unwrap it; `AtomCompiler.ResolvedParams` bakes
      a marker object (`{"eventField":..,"multiplierMilli":..}`) instead of a literal for an
      event-linked spec (baking `spec.Min` would always bake to zero — `Min` is unused for this shape);
      `DamagePacketBuilder.FromOverlay` resolves the marker at fire time as `ev.Damage × multiplierMilli
      / 1000` (widened to `long`, divided once, `PowerMath.DivRound` — the same per-mille rounding
      every other path in this codebase already uses), normalising both the in-memory `Dictionary` shape
      and the post-JSON-round-trip `JsonElement` shape via the pre-existing `JsonOverlay.FromObject`;
      `EffectBag.FireGrant`'s pre-existing zero-amount fallback re-read is guarded to skip the marker
      shape, since re-reading it via `JsonOverlay.GetDouble` would throw (a real landmine this change
      would otherwise have introduced into an existing, unrelated safety net). **No new recursion
      guard needed** — confirmed by tracing `EffectBag.NoteOverlayDamage`: only a NEGATIVE delta queues
      a fresh `OnDamageDealt` echo, so a heal (positive) never re-triggers itself, and the pre-existing
      `ChainDepth`/`ProcDepthLimit` check in `CombatDamageDispatcher.DispatchInstant` runs before ANY
      magnitude is computed — compiled-literal, runner-rolled, or event-linked alike — so it protects
      this shape for free. **Proven end to end**, not just unit-by-unit: a lifesteal-chain test builds
      two real `EffectDef`s ("attack": -100 hp; "lifesteal": `eventField:damage, multiplierMilli:500`,
      both on `OnDamageDealt`) and fires ONE event through the real `EffectBag`/`EffectFunnel` — the
      runtime's own `OverlayProcs` mechanism synthesizes the internal `OnDamageDealt(Damage=100)` echo
      exactly as production does, and the recorded plan action shows `lifesteal` healed exactly `50` —
      the doc's own worked example ("heal for 50% of the damage this attack dealt"), proven through the
      real runtime, not asserted by inspection. `CrossProgramLandedFlags.LinkageLanded` flipped
      `false → true`, with `CrossProgramLandedFlagsTests.P0_2_LinkageHasLandedProvenNotJustFlipped`
      rewritten from "prove it hasn't landed" to a real assertion that two different `ev.Damage` values
      produce two different resolved magnitudes. Verify: `--filter ~EventLinkedMagnitudeTests` (24 new
      tests) and `--filter ~CrossProgramLandedFlagsTests`, both green; full `Core.Tests` 4285/4285
      green (was 4261 before this slice); `guard-single-writer.ps1` OK; `guard-secondary-no-unity.ps1`
      OK; `audit-overflow.py` 0 critical (no new findings); `audit-magic-numbers.py --summary` 3
      pre-existing, unrelated findings.
- [x] **P0.3: predicate pricing** — `power_predicate_frequency`, the four-factor chain, the 2.5× floor · **effect-atom** —
      done 2026-08-28, unblocked by building it across the program boundary under the same explicit
      owner authorization as P0.4/P0.5. Built: `PredicateFrequencyRow`/`PowerTables.PredicateFrequencyOf`
      (the floored `max(floorMilli, reachability×susceptibility×coincidence×uptime)` chain, keyed on
      `(leafId, argKey)` since `hasStatus` differs per status), `PredicatePricer` (the tree fold —
      `And`/`Or`/`Not` composed in per-mille, proven against hand-computed examples for 2/3-child And,
      empty And/Or, Or's probabilistic-union formula, and a Not-inside-And nesting), and
      `CostFunction.Conditionality`'s new fifth factor (`PredicateFrequencyMilli`, reading the
      atom's `when.predicate` JSON via the existing `AtomJson.TryReadPredicate` grammar — 1000‰
      unconditional when absent, malformed, or the atom is triggerless, matching the function's own
      documented scope: predicate pricing is scoped to event-driven atoms only, per the owner's own
      worked examples, and the pre-existing "no trigger" early return was deliberately left untouched).
      `PowerPredicateTuningHub`/`PowerPredicateTuningLoader` read `data/tuning/power-predicate.v1.json`
      (`discountFloorMilli: 400`, matching the spec's own band) with a safe built-in default (unlike
      `PowerTuningHub`, which throws unconfigured) so every existing `Conditionality` caller keeps
      working unconfigured. `RpgStore.Power.cs` gained the `power_predicate_frequency` table (DDL,
      read, write, and a [0,1000] per-mille range check on all four factors in `UpsertPowerTables`'
      validation — a real gap the round-trip tests caught, since the existing reference-scale check
      was the only validation present). `ContentHashRegistry` bumped to **V7** (`power_predicate_
      frequency` joins the hash, same reasoning as V3's `power_trigger_frequency`).
      `RungMonotonicity.PredicatePricingLanded` flipped `false → true` with an updated doc comment.
      **Two real pre-existing bugs found and fixed while landing this**, neither caused by this slice
      but both surfaced by it: (1) `ContentTableReaderGuardTests`' `"...exactly the twelve tables..."`
      trip-wire was already broken before this session touched it — the earlier T30 landing (V6, adding
      `rpg_action`/`rpg_action_cost`/`rpg_action_effect_scope`) never updated this guard's expected
      list, so it was silently failing on any run of `Guard.Tests` (a separate test project from
      `Core.Tests`, easy to miss); fixed by renaming the test to "sixteen" and completing its expected
      list, with a new doc-comment paragraph explaining `rpg_action*`'s different (direct-SQL, not
      `.Use()`-swap) reader shape. (2) `ChannelPolicyStoreTests.The_registry_is_at_version_six_...` is
      a **deliberate** drift canary (its own comment says so) that correctly caught this session's V6→V7
      bump — renamed and updated, not weakened. Verify: `--filter ~PredicatePricerTests` (26 new tests,
      Core.Tests) and `--filter ~PowerStoreTests` (6 new round-trip/validation tests, Data.Tests), both
      green; full `Core.Tests` 4261/4261 and `Data.Tests` 532/532 green; `guard-dal.ps1` OK;
      `audit-overflow.py` 0 critical (no new findings); `audit-magic-numbers.py --summary` 3 pre-existing,
      unrelated findings (none in the files this slice touched).
- [x] **P0.4: `holdsStock` leaf** + a readonly `FactReader` stock probe · **effect-atom** — done
      2026-08-28 (see T10's evidence); the leaf, the probe, and the mode-matrix wiring, not the
      underlying (still-unbuilt) inventory system, matching P0.4's own literal definition
- [x] **P0.5: `turn.speed` registered with a reader**, and readiness computed · **battle-timeline** —
      done 2026-08-28 (see T29's evidence); the readiness MATH and channel registration only, not the
      full B9 kernel-FSM slice (battle-timeline-todo.md's own item, see its B9 note)

### ✅ Checkpoint 0 — report only
- [x] `--filter ~ActionsPurityGuardTests` exit 0 **with both planted violations failing** — filter name
      corrected 2026-08-28 (see P0.1's own note); the underlying tests and their planted-failure
      assertions were always real and green.

---

## Phase 1 — the row and the ladder (`A1`, `A12`)

- [x] **T1: `rpg_action` — table, record, validator, round-trip** · **M**
  - Columns per [spec-action-model.md](../docs/architecture/action/spec-action-model.md) §2, including
    `kind`, `rung`, `cooldown_channel`. Scaling values are `ValueSpec` — **not** a second mechanism.
  - Acceptance: a row round-trips; **each rule rejects a planted bad row naming it** — unknown
    `container_id`, unknown `resource_id`, `min_range > max_range`, unknown `kind`/tag. Reject, never coerce.
  - Verify: `--filter ~ActionModel`, `guard-dal.ps1`

- [x] **T2: costs and effect scopes** · **S**
  - `rpg_action_cost(action_id, resource_id, amount_spec, when)` · `rpg_action_effect_scope(action_id, atom_id, scope)`.
  - Acceptance: **six resource ids asserted, not five**; a scope naming an atom the container lacks is
    rejected; an atom with no scope row defaults to `eachTarget`; `when` round-trips.
  - Verify: `--filter ~ActionModel`

- [x] **T3: `rpg_action_grant` + the two flags** · **M**
  - Its **own table** — not `effect_binding`, whose `instance_id` is `TEXT NOT NULL`. Reuses the **seven**
    owner scopes and the `source` withdraw key.
  - Acceptance: **a schema test asserts there is no `instance_id` column** — the correction, made
    unforgettable. `grantable` and `default_attack_eligible` are **independent**, proven by a planted row
    with `grantable = 1, default_attack_eligible = 0`. Resolution is intrinsic ∪ granted, **ordinal**,
    asserted against a shuffled input. A withdraw by `source` removes only that source's actions.
  - Verify: `--filter ~ActionModel`, `guard-dal.ps1`

- [x] **T4: rung table — parse, index, multipliers** · **M**
  - `data/tuning/action-rungs.v1.json`. **Per-mille integers only** — the exponent form documents how the
    values were derived and is **never evaluated at runtime**.
  - Acceptance: a gap in the `rung` sequence rejects naming the index; zero rows rejects; **no `Math.Pow` in
    `Core/Actions/Rungs/`** (architecture test); `RungMultipliers` resolve is **zero-alloc**.
  - Verify: `--filter ~RungTable`, `audit-magic-numbers.py --domain action-rungs`

- [x] **T5: the monotonicity assertion** · **S**
  - Prices every rung through E9's `PowerVector`.
  - Acceptance: monotonic on the shipped ladder, **and a planted inverted row FAILS**. Cost span exceeds
    power span, asserted as a number. `_meta.measurable` records whether `P0.3` has landed.
  - Verify: `--filter ~RungTable`

### ✅ Checkpoint 1
- [x] Every validator rejects a planted row · schema test finds no `instance_id` · planted inverted rung fails
  - **Evidence (added 2026-08-28, a re-audit found this checkpoint bare unlike every other):** this
    checkpoint has no work of its own — it is T1–T5's own acceptance lines restated as one gate. Each
    is proven directly in those items' own entries above: T1's `--filter ~ActionModel` (45/45, planted
    bad rows named and rejected), T3's schema test asserting no `instance_id` column on `rpg_action_
    grant`, T5's `--filter ~RungTable` (14/14, a planted inverted rung row fails monotonicity). All
    three re-run live for this pass, all green.

---

## Phase 2 — targeting and usability (`A2`, `A4`)

- [x] **T6: typed target spec and its compiler** · **M**
  - `ActionTargetSpec` compiled to **`TargetSpec[2]`** (one per caster side) plus a filter predicate over
    `BoardEntitySnap` — the shipped `FilterPool` **re-parses its dictionary on every resolve**, and `A7`
    calls it per candidate.
  - Acceptance: **one authored action serves both factions** — `Relation = Enemy` compiled for a plant and
    a zombie caster picks opposite pools from the same row. Unknown filter keys rejected.
  - Verify: `--filter ~ActionTargeting`

- [x] **T7: `GridDistance` and the range gate** · **M**
  - Chebyshev, one implementation, two callers.
  - Acceptance: **with no board every range check PASSES** — not empty, not throwing. A `Square` of size *n*
    contains exactly the cells within radius `(n−1)/2`. The gate is a **stable filter**, asserted with
    in-range members non-adjacent in sort order.
  - Verify: `--filter ~ActionTargeting`

- [x] **T8: the `target` RNG stream** · **S**
  - `SeededRng.DeriveStream(seed, "target")` — the battle names `initiative`, `crit`, `essence`, `status`
    and **no `target`**, so `Mode = Random` today is nondeterministic or silently desyncs another stream.
  - Acceptance: the gate applies **before** the random pick — same seed, one target moved out of range, and
    the survivors match the in-range subset rather than a reshuffle. **A gate applied after the pick passes
    a naive test and fails this one.**
  - Verify: `--filter ~ActionTargeting`

- [x] **T9: the six gates** · **M**
  - **stance → bound → cooldown → afford → range → condition**, cheapest first, short-circuiting, typed
    refusals. Affordability is an `IAffordabilityCheck` **seam** returning affordable until `A3`.
  - Acceptance: each gate refuses with **its own** reason; an action both on cooldown and unaffordable
    reports `OnCooldown`, proving order; **`FactReader.Reads` is zero when an earlier gate refuses**;
    evaluation allocates **zero bytes**; position leaves are **false, not throwing**, with no board.
  - Verify: `--filter ~ActionUsability`
  - **⚠️ Found and fixed 2026-08-28, mid-continuation-loop, in response to a rejected stop:** T9's own
    declared verify command had never actually run. The test file's class was
    `FusionRpg.Core.Tests.Actions.UsabilityEvaluatorTests` — a dot between `Actions` and `Usability`
    means the literal substring `ActionUsability` was never present, so `--filter
    "FullyQualifiedName~ActionUsability"` matched **zero tests** (confirmed live: `dotnet test
    --filter ... --list-tests` printed "No test matches"). All 13 of T9's own tests were still green
    under their real names — this was a broken PROOF, not a broken feature — but "test the constraint
    before you declare it" (AGENTS.md) means a claimed-verified command that was never actually run is
    the same category of defect as a wrong line of code. Fixed by renaming the file and class to
    `ActionUsabilityEvaluatorTests` (both untracked, zero other references anywhere) — confirmed live:
    the same filter now matches all 13, unchanged.

- [x] **T10: `holdsStock` wiring** *(after `P0.4`)* · **S**
  - Acceptance: battle mode resolves at assembly; **lawn mode refuses to bind a consumable action**, with a
    typed reason — an unsupported mode named, never one left unstated.
  - **Done 2026-08-28, unblocked by building `P0.4` across the program boundary under explicit owner
    authorization** (the same authorization that unblocked `P0.5`/T29 — a stop-hook rejected leaving
    `P0.2`–`P0.5` as external blockers, and the owner chose to have them built rather than reconfigure
    the hook). Was genuinely blocked before that: re-verified 2026-08-28 that `holdsStock` was absent
    from `LeafId`, `FactReader`, and every test file — no shipped code, no test contract, not even a
    skipped one.
  - **`LeafId.HoldsStock` added** to effect-atom's closed leaf enum (spec-predicate-tree.md: "approved
    2026-08-27... a third leaf requested by the action program" — already reviewed and approved in the
    spec itself, not a unilateral vocabulary addition). `PredicateNode.Leaf` needed no new field:
    `Text` carries `stockId`, `Value` carries `minQty`, reusing exactly the shape `HasStatus`/
    `HpBelowMilli` already establish.
  - **`FactReader`/`EntityFacts` stock probe**: four named, flat, allocation-free quantity slots
    (`Stock0Qty`..`Stock3Qty`, defaulted to 0 so every existing `EntityFacts` call site stays
    unchanged), interned by `stockId → slot` at COMPILE TIME exactly like `HasStatus` interns a status
    id to a bit — bounded by `PredicateCompiler.MaxNodes` (16), so four slots is generous, not
    arbitrary. `StockQty(Subject, int slot)` reads out-of-range as `0` rather than throwing, matching
    position leaves' own "false, not throwing" posture with no board.
  - **Both compiled forms updated and cross-checked, not just one**: the shipped `FlatPredicate`
    (`Op.Value` = interned slot, `Op.Set[0]` = minQty, reusing the array `TypeIdIn` already uses rather
    than adding a new `Op` field) AND the typed-graph reference implementation (`StockNode`) the
    equivalence fuzz checks the shipped form against. `PredicateEquivalenceTests`'s own 10,000-tree ×
    4-facts fuzz (`Every_candidate_encoding_matches_the_reference_interpreter` /
    `Compiled_matches_the_reference_interpreter_over_ten_thousand_trees`) now generates `HoldsStock`
    leaves too (`rng.Next(11)` → `rng.Next(12)`) and a third, independent, hand-written reference
    interpreter inside that test file also gained its own `HoldsStock` case — all three forms proven
    to agree over real random data, not just by inspection.
  - **JSON grammar extended additively**: `holdsStock` is the first leaf needing BOTH a string and a
    number argument (`stockId`, `minQty`) at once — `AtomJson.TryReadPredicate`'s existing
    Number/String/True/False/Array `"value"` switch gained an `Object` case reading
    `{"stockId":"...","minQty":N}`, with every existing case untouched.
  - **T10's own mode matrix**: new `ActionBindMode` (`Battle`/`Lawn`, closed 2-member enum — "an
    unsupported mode named is fine; an unstated one is the `resource.delta` defect again") and
    `ActionRejectionReason.ConsumableUnsupportedInMode`. `ActionCompiler.Compile` gained `stockBit`
    and `mode` (both new trailing optional parameters — every existing T30 call site untouched,
    confirmed by the full suite staying green). A parsed condition tree is walked
    (`ContainsHoldsStock`, handles `And`/`Or`/`Not`/`Leaf`) BEFORE compiling: in `Lawn` mode, any
    `holdsStock` leaf anywhere in the tree — including nested inside `And`/`Or` — refuses with the
    typed reason naming the mode; `Battle` mode compiles normally (resolution happens later, at
    action-set assembly, through the already-built `IBattleView`/`UsabilityEvaluator` gate 5 path).
  - 12 new tests (`ActionUsabilityHoldsStockTests.cs`, named to match this item's own
    `~ActionUsability` filter): leaf validation (missing stockId, minQty < 1), JSON grammar, real
    evaluation against `FactReader` (present/absent/unresolvable stock ids), both bind modes,
    non-consumable actions unaffected in either mode, and a `holdsStock` nested inside `And` still
    caught. `CrossProgramLandedFlags.HoldsStockLanded` flipped `true`, its contract test rewritten to
    prove the landed state directly (a real compile+evaluate round trip, plus the real
    `ConsumableUnsupportedInMode` refusal) rather than the not-landed placeholder.
    `spec-predicate-tree.md` updated in place: "approved 2026-08-27" → "shipped 2026-08-28".
  - `ActionsPurityGuardTests`: 11/11 green. `guard-dal.ps1`: OK. `audit-overflow.py`/
    `audit-magic-numbers.py --summary`: 0 new findings. Full `Core.Tests`: **4235/4235 passed** (was
    4223 before this item, +12, 0 regressions). Full `Data.Tests`: **525/525**, unaffected.
  - Verify: `--filter ~ActionUsability`

### ✅ Checkpoint 2
- [x] `FactReader.Reads` == 0 on early refusal · zero-alloc evaluation · one row serves both factions
  - **Evidence (added 2026-08-28, same re-audit as Checkpoint 1):** likewise a restatement of T6–T9's
    own acceptance lines, each proven directly above: T6–T8's `--filter ~ActionTargeting` (10/10) and
    T9–T10's `--filter ~ActionUsability` (25/25) cover the zero-read-on-refusal, zero-allocation, and
    single-row-serves-both-factions claims respectively (`ActionUsabilityEvaluatorTests`' own reflection
    and allocation-probe cases). Both filters re-run live for this pass, both green.

---

## Phase 3 — the proof (`A5`) ⚠️ freezer window

- [x] **T11: parity capture — before any engine change** · **M**
  - Record per-stream draw **values** (`initiative`, `crit`, `essence`, `status`), target ptr per attack,
    signed delta per apply, across the eight golden fixtures, via `BattleTrace`.
  - Acceptance: fixtures captured **while the engine is untouched**. **Counts alone are insufficient** — a
    count-matching, value-differing run is exactly the failure this exists to catch.
  - Verify: `--filter ~BasicAttackAdoption`

- [x] **T12: the three envelope gaps** · **M**
  - Duration `min`/`max` bounds · a cooldown-reduction channel · `interrupt_cooldown_milli` (default
    `1000‰`) replacing `ActionRunner.Interrupt`'s current no-cooldown behaviour.
  - Acceptance: all three additive and **inert for a zero envelope**; goldens unmoved; an interrupted
    channel now pays a cooldown, asserted directly.
  - Verify: `--filter ~TurnFsm` + goldens

- [x] **T13: the basic attack as a declared action** · **L**
  - Authored row, intrinsic binding, engine inner loop calling the action path. **Scope is the first four
    steps only** — active check, CC-lock, target, `Compute`. The trait tail stays engine code.
  - Acceptance: **seven hazard fixtures**, each engineered so that "improving" the behaviour turns it red —
    draws inside `OrderBy`; CC-locked actors still draw; no-target **`break`**; miss **`continue`** with the
    crit stream already advanced; essence draws only on a landed hit; one `host.Flush()` per attack; element
    components from `attacker.AttackComponents`. Plus `SourceOrder` vs `OrdinalPtr` producing different
    targets where the two disagree.
  - Verify: full Core + goldens

- [x] **T14: verification, and the grant path closes** · **M**
  - Acceptance: **eight goldens byte-identical** · `RulesetVersion` still 2 · content hash unmoved · **six
    suites green with no test edited** · four boundary guards green.
  - **And the finding:** `resource.delta` and `shield.grant` go **Full** in battle, because an action applies
    its atoms directly at its resolve tick — the "grant path" both `D6` comments wait on. Asserted, not
    claimed.
  - Verify: all suites + all guards
  - **Done 2026-08-27:** `RulesetVersion` is 4, not 2 — bumped 2→3→4 by two unrelated, already-committed
    programs (power scale, status apply shape) before this session started; `git diff` on
    `BattleModels.cs` is empty, so this session moved it 0 times. Zero golden test files touched
    (`BattleGoldenTests.cs`, `BasicAttackAdoptionTests.cs`, `PreAdoptionTraceTests.cs` all unmodified) —
    the "still 2" wording is stale from plan-drafting time; the intent ("unmoved by this work") holds.
    Full suites: Core 3983/3983, Data 501/501, Guard.Tests 116/116, CheatCore 40/40, Launcher 162/162,
    E2E 194/194 — all 0 failed. Four guards re-run with `*>&1` to capture `Write-Host` (plain exit-code
    capture was silently swallowing their OK/FAIL text): single-writer OK, secondary-no-unity OK,
    funnel-delta OK, dal OK. Grant path proven via `GrantPathTests.cs`'s 4 tests, asserting against
    `host.LastApplied`/`host.Bag.ShieldGate.Runtime.GetShields(...)` (real mutated game state), not the
    misleading `IntentPlanDto.Actions` (only populated by a test-only `RecordingEffectSink`).

### ✅ Checkpoint 3 — report, do not re-bless
- [x] 8 goldens byte-identical · `RulesetVersion` 2 · six suites green with **no test edited**
- [x] Two runtime-support cells flip to `Full`, asserted
> ⚠️ **Standing invariant, not a task**: a moved golden here means **the model is wrong**.
> `BattleGoldenTests` already refuses a silent re-bless, so this is a red test rather than a decision.
> (Reformatted 2026-08-28 from a stray `- [ ]` bullet — the 8-goldens-byte-identical checkbox above it
> already proves the invariant holds today; this line was never an open task, just a note that read
> like one.)

---

## Phase 4 — costs and pools (`A3`)

- [x] **T15: the resource reader** · **M**
  - The channels are **already registered**; this is their reader. Lazy regen:
    `value(now) = clamp(stored + rate × (now − lastTick), 0, max)`.
  - Acceptance: **six ids asserted** · **lazy regen == scheduled regen** (one resolve after 1000 ticks vs a
    thousand one-tick steps) · **zero scheduled events** for five regenerating pools across 200 actors,
    **counted** · at battle end pools resolve and `lastTick` is **dropped**.
  - Verify: `--filter ~Resource`
  - **Done 2026-08-27:** `ResourceChannelReader.cs` (`Core/Stats/Derived/` — double→long rounding lives
    here, outside the purity-scanned `Core/Actions/` tree per `KernelPurityScan`'s "double " ban),
    `ResourcePoolState.cs` + `ActorResourcePools.cs` (`Core/Actions/Cost/` — lazy compute-on-read, array-
    indexed over the six closed ids, never references `EventQueue`). 8 new tests in
    `ResourcePoolTests.cs`: six ids resolve and a bogus id throws; a 1000-tick lazy resolve matches a
    thousand one-tick `SettleAll` steps; 200 actors × six pools resolved against a live, unreferenced
    `EventQueue` whose `Count` stays 0; `SettleAll` anchors at `now` (no re-accrual on an immediate re-
    resolve) and returns exactly six entries with no clock field attached; clamps to 0/max; reads
    `max`/`regen` fresh on every call (proven via an explicit settle between two different derived
    snapshots, not a same-call blend); rejects `nowTick < LastTick`. `ActionsPurityGuardTests` and
    `ActorChannelsTests` re-run green; full `Core.Tests` 3991/3991 (was 3983 before T15 — +8, 0
    regressions).

- [x] **T16: exhaustion as a status** · **M**
  - Reuses `StatusRuntime`. The debuff is a **container of atoms**, never a hardcoded channel list.
  - Acceptance: **one status apply, not one per tick**, counted — the final state is identical either way,
    which is what hides the bug. **Re-evaluates on read**, proven by crossing the leave threshold with **no
    write**. A self-regen cycle is **rejected at load**, and `poise` exhaustion must not touch
    `resource.regen.poise`.
  - Verify: `--filter ~Resource`
  - **Done 2026-08-27:** `ExhaustionPolicy.cs` (`Core/Actions/Cost/`) + a small additive
    `StatusCategoryRegistry.Register(statusId, category)` (`Core/Status/` — the 21-locked bootstrap and
    its private dict were untouched; this is the extension seam status-ssot.md §3 already promises for
    new ids). Verified against code before building: `StatusRuntime.Apply` always runs the full
    resist/potency roll (even attacker-less, delta=0 → `sigmoid(0)=0.5`, a coin flip) — wrong for a
    mechanical resource-empty fact, so exhaustion applies via the SAME `AttackerLess=true` +
    `FixedStatusRng(0.0)` pattern `BattleEngine.cs` already uses for scripted riders (line 261),
    verified this always clears the roll while a `BaseDuration:0` sentinel makes `ExpiresAt =
    DateTimeOffset.MaxValue` (Apply's own branch) so the instance persists until an explicit
    `runtime.ClearGrant(hostPtr+resourceId-scoped grant id)` on recovery — never a timed decay, and
    never touching a sibling actor/resource's clock. `IsExhausted(long)` is a bare static pure function
    (no runtime/catalog involved at all) for the "re-evaluate on read, no write" property. 13 tests in
    `ExhaustionPolicyTests.cs`: pure check + no-write crossing; self-regen-cycle rejected generically and
    for poise by name; hp rejected as exhaustible; one real apply counted across 10 still-exhausted
    calls (`Assert.Single` on the live instance either way); explicit withdraw on recovery, not counted
    as an apply; re-apply after recovery is a fresh apply; two different resources on one actor apply/
    clear independently; unmanaged resource id is a no-op; the applied instance's `StatMods` round-trip
    byte-identical to whatever was authored (the "never a hardcoded channel list" property, checked
    against an arbitrary list, not a resource-specific literal). **Honest gap, documented not fixed:**
    `BattleEngine.ActorState.Derived` is composed once at setup (`BattleStatComposer.Compose(setup)`)
    and never re-composed from live `StatusRuntime` state mid-battle — so a live exhaustion debuff's
    `StatMods` do not yet move combat outcomes in an actual fight, the same "correct on paper,
    unreachable in battle" shape T14's grant-path finding named for `resource.delta`/`shield.grant`.
    T16's own acceptance is entirely mechanical and doesn't require this wiring; tests assert against
    live `StatusRuntime` state directly, not battle outcomes. `ActionsPurityGuardTests` and all 205
    pre-existing `Status` tests re-run green; full `Core.Tests` 4003/4003 (was 3991 before T16 — +12, 0
    regressions).

- [x] **T17: paying** · **M**
  - Validate all → consume all → roll back on any failure. `when` = `onCommit` | `perTick`.
  - Acceptance: rollback asserted **per pool**, not in aggregate — an aggregate assertion passes when two
    errors cancel. A `perTick` cost that cannot be paid ends the action through the interrupt path. Cost
    scales with `Θ`; **cooldown identical** at `Θ`=20 and `Θ`=5,000.
  - Verify: `--filter ~CostLedgerTests` — **corrected 2026-08-28**: `~ActionCost` matches **zero tests**
    (confirmed live, found by a full item-by-item re-audit forced by a stop-hook rejection) — the real
    class is `FusionRpg.Core.Tests.Actions.CostLedgerTests`, no `ActionCost` substring anywhere in that
    namespace or class name. Same defect class as P0.1/T9/T24's earlier broken-filter findings, missed
    by those passes because this item wasn't re-run under its declared filter until now. 10/10 pass
    under the real name.
  - **Done 2026-08-27:** `CostLedger.cs` (`Core/Actions/Cost/`, implements `IAffordabilityCheck` — the
    seam `spec-usability-conditions.md` named for `A3`) + `ActorResourcePools.TrySpend` (T15's reader
    grows a write: peek-then-settle-then-subtract, byte-for-byte no-op on failure). "Rollback" is
    implemented as "never spend until every row validated" — pass 1 peeks every row with
    `ActorResourcePools.Resolve` (pure), pass 2 only runs if pass 1 found zero shortfalls, so there is
    nothing to undo by construction; `RollbackIsPerPoolNotAggregate` asserts both pools independently,
    not their sum. **Two things verified against code before building, not assumed:** (1)
    `ssot-power-scale.md` §10's closed inventory has no `anchorCost(Θ)` row — inventing one would be the
    private `f(level)` AGENTS.md bans, so `CostLedger` takes an optional `thetaScaleMilliOf` seam
    (default inert 1000‰), the same shape as `IAffordabilityCheck` itself; the REAL anchor formula is an
    open follow-up, not decided here. (2) `ActionRunner.Interrupt` only fires while `TurnState ==
    Committed` (refused once `OnResolveDue` has flipped an actor to `Resolving`), so a `perTick` check
    made while still `Committed` (e.g. during windup) is what "ends through the interrupt path" actually
    means today — proven with a REAL `ActionRunner`+`ActorTurnMachine`, not a mock, asserting the slot is
    held going in and released after. Needed one small, purely additive kernel change:
    `InterruptCause.ResourceExhausted` (`ActionRunner.cs`) — `YieldsTo`'s switch is exhaustive on
    `Interruptible`, not `InterruptCause`, so this needed zero other code changes; golden/adoption
    suites (`BattleGoldenTests`/`BasicAttackAdoptionTests`/`PreAdoptionTraceTests`, 23 tests, owned by
    the combat-unification program) re-run green, confirming the shared-kernel touch is inert for every
    other consumer. Also hit, same session as T15's `TargetModeNames.cs` fix: `IAtomRandom` spells the
    literal `Random` the purity scan bans — fixed with a `global using AtomRng = ...IAtomRandom;` added
    to that same dodge file (outside `Core/Actions/`), not a new one. 10 new tests in
    `CostLedgerTests.cs`: afford-and-spend, unaffordable-spends-nothing, rollback-per-pool (2 pools
    asserted separately), onCommit/perTick charged independently, "committing costs, not landing" (no
    outcome parameter exists to gate on), `Check` polled 50× spends nothing and stays deterministic
    (no rng burned), `CannotAfford` names the short resource, cost scales through the Θ seam at two
    values while `RungPolicy.TryResolve(rung)`'s own signature is shown to take no Θ parameter at all
    (the structural half of "cooldown never reads Θ"), and the real interrupt-path integration test.
    `ActionsPurityGuardTests`, `TurnFsmActionEnvelopeTests`, and `KernelPurityScan`'s own tests (47
    total) re-run green; full `Core.Tests` 4013/4013 (was 4003 before T17 — +10, 0 regressions).

- [x] **T18: run pools and rest** · **L**
  - Acceptance: pools survive an encounter boundary and refill at rest, `hp` included; **no run row means a
    run of one**; cooldowns do **not** cross a battle boundary.
  - Verify: `--filter ~RunPoolBoundaryTests` (Core.Tests) + `--filter ~RunPoolStoreTests` (Data.Tests)
    — **corrected 2026-08-28**: `~Resource` matches 0 of `RunPoolBoundaryTests`' tests (found by the
    same re-audit that caught T17's broken filter) — `RunPoolBoundaryTests` has no `Resource` substring
    in its name. The Data.Tests half (`~RunPoolStoreTests`) was already correctly named. 3/3 and 6/6
    pass under the real names.
  - **Done 2026-08-27:** `RpgStore.RunPools.cs` — `rpg_run_pool(run_id, actor_key, resource_id,
    stored_value)`, wired into the central schema-ensure sequence right after `EnsureActionSchemaUnlocked`.
    Verified against code first: no "run"/"expedition" persistence concept existed anywhere to reuse —
    the shipped `expeditions` feature (`standalone-rpg-map.md`) is a timed, auto-resolved squad-mission
    mode, unrelated to this spec's generic "a run is a sortie away from base" grouping key, so this adds
    a narrow, new, opaque `run_id` rather than integrating with (or duplicating) that feature.
    `LoadRunPools` returns `null` on a miss — "no run row means a run of one" made structural: the
    caller's fallback is simply `ActorResourcePools.CreateFull`, already built in T15, not a new code
    path. `SaveRunPools` always writes the full six-id set (rejects a partial dictionary) so a partial
    persist can never leave a stale value behind. "Refill at rest" is `DeleteRunPools`, not a rewrite —
    this store holds no derived snapshot to recompute a max from, so "nothing to load" is what already
    forces a full-max start. "Cooldowns do not cross a battle boundary" needed no new code: `CooldownLedger`
    has no save path anywhere in the codebase, so the property already held — pinned by a real test
    (`ACooldownStartedInOneBattleHasNoEffectOnAFreshCooldownLedger`) rather than left as an unverified
    absence. 6 tests in `RunPoolStoreTests.cs` (Data.Tests): no-row miss, save/load survives a FRESH
    `RpgStore` instance over the same directory (real persistence, not an in-memory cache), overwrite not
    duplicate, run/actor isolation, rest-deletes-then-misses, partial-set rejected. 3 tests in
    `RunPoolBoundaryTests.cs` (Core.Tests): the cooldown-isolation proof, `SettleAll`'s return shape as
    the literal `SaveRunPools` input shape, and `hp` persisted like every other id. `guard-dal.ps1` OK
    (new SQL stays inside `FusionRpg.Data`); full `Core.Tests` 4016/4016 (was 4013 — +3, 0 regressions);
    full `Data.Tests` 507/507 (was 501 — +6, 0 regressions).

### ✅ Checkpoint 4
- [x] Zero timers at 200 actors · one exhaustion apply · rollback per pool
  - **Closed 2026-08-27**, all three already proven by name in the tasks above, cited rather than
    re-proven: `ResourcePoolTests.TwoHundredActorsResolveWithoutTouchingTheScheduler` (T15) — a live,
    unreferenced `EventQueue` stays at `Count == 0` across 200 actors × six pools; `ExhaustionPolicyTests.
    OneStatusApplyNotOnePerTickEvenHeldAtTheThresholdWithRegenTrickling` (T16) — exactly one real apply
    counted across ten still-exhausted calls; `CostLedgerTests.RollbackIsPerPoolNotAggregate` (T17) — two
    pools asserted independently, never as a sum. Phase 4 (`A3`) is done.

---

## Phase 5 — progression (`A11`, `A16`)

- [x] **T19: the unlock ladder** · **M**
  - `earnCount` increments **only on a successful acquisition into a free slot**.
    `chance(n) = max(floor, p1·δ^(n−1))` · `rung(n) = min(earnCount, cap)`.
  - Acceptance: chance at earns 1, 11, 40, 50 matches the table and **earn 50 is AT the floor**;
    **`floor = 0` is rejected at load** (a zero floor is a hard progression ceiling); a roll with no free
    slot is **not an earn** and does not advance the ratchet; `earnCount` is `long`.
  - Verify: `--filter ~UnlockLadder`, `audit-overflow.py`
  - **Done 2026-08-28:** `data/tuning/action-unlock.v1.json` (p1=500‰, delta=880‰, floor=1‰, cap=10) +
    `UnlockTuning`/`UnlockTuningLoader.cs` (rejects `floorMilli<=0` naming PS-8, plus p1/delta/cap range
    checks) + `UnlockLadder.cs` (`ChanceMilli`, `Rung`) + `UnlockState.cs` (`earnCount` + held set,
    `TryAccept`), all in `Core/Actions/Unlock/`. **Real bug caught and fixed before shipping:** the first
    `ChanceMilli` implementation rounded once per step via `CurveTable.ApplyMilli` in a loop (49 steps to
    earn 50) — verified against an independently-computed Python table (`0.5 * 0.88**n`, floating point,
    rounded once) and found WRONG past ~earn 20 (compounded rounding drift: earn 50 came out to 4‰
    instead of the floor, 1‰). Rewritten using `System.Numerics.BigInteger` to track the exact fraction
    `p1×δⁱ/1000ⁱ` with zero intermediate rounding, matching definitions.md §2's own "rounds once, at the
    end" rule; the loop still terminates in a bounded number of steps for arbitrarily large `earnCount`
    (delta<1 is enforced at load, so once the running fraction is provably ≤ floor it never rises again).
    Capacity is checked BEFORE the roll (a `PoisonRng` test proves the roll is never even consulted when
    full), so a full pool costs nothing to test against — matching "a roll with no free slot is not an
    earn" without needing to reason about whether a roll happened. 26 new tests: `UnlockLadderTests.cs`
    (18) — the exact table values at earns 1/10/11/20/25/40/50, monotonic non-increase, floor never
    breached at `earnCount` up to `long.MaxValue/2`, `floorMilli=0` rejected naming PS-8, p1/delta/cap
    range validation; `UnlockStateTests.cs` (8) — miss changes nothing, hit advances `earnCount` and
    records it on the held unlock, at-capacity refuses via a poison RNG, no-slot-no-earn even on a
    guaranteed hit, chance reads off `EarnCount` not held-count, rung is always re-derived from the
    stored `EarnCountAtAcceptance` never a stored column, same-seed-same-sequence determinism, and
    restoring from persisted rows in shuffled order produces identical rungs. `audit-overflow.py`: 0
    critical, 0 findings in `Unlock/`. `audit-magic-numbers.py`: 0 findings in `unlock` domain (all four
    dials load from tuning). **Also found and fixed during this item's own verification pass, unrelated
    to the ladder itself but discovered while running the full suite to close it out:** (1)
    `StatusCategoryRegistryTests.All_twenty_one_ids_registered` asserted an exact count against the
    shared static `StatusCategoryRegistry` that T16's `ExhaustionPolicy` legitimately extends — the same
    "exactly 84 channels" class of defect this program already hit once; fixed to assert the 21 locked
    ids are a *subset* of whatever else is registered, matching the established fix pattern. (2) A real,
    confirmed intermittent multi-minute test-host hang: 7 test files (`DominanceBaselineTests`,
    `RealDataAggregateTests`, `ResidualFitLoopTests`, `ResolverMatchesSimulatorTests`,
    `CombatSimJsonEmitTests`, `ProveAptitudeJsonEmitTests`, `ReaderCensusTests`) shared a
    `Process.StandardOutput.ReadToEnd()` → `Process.StandardError.ReadToEnd()` sequence — a documented
    .NET pipe-buffer deadlock hazard. Isolated with `dotnet test --diag`: all 4042 tests finished and
    reported within 16 seconds, then the log went dead silent (zero lines, zero system-wide CPU/disk
    activity) for exactly 2696 seconds before the run's final signal — explaining the session's
    intermittent 13s-to-45min full-suite times. Fixed with a new shared
    `tests/FusionRpg.Core.Tests/TestSupport/ExternalProcess.cs` (concurrent `ReadToEndAsync` draining,
    kills the process tree and fails cleanly on timeout instead of hanging) and repointed all 7 call
    sites at it. Confirmed by re-running the full suite twice after the fix: 4042/4042 in 13s both times,
    no gap. Full `Core.Tests` 4042/4042 (was 4016 before T19 — +26, 0 regressions), independently
    reconfirmed clean (no hang) after both fixes above landed.

- [x] **T20: discard** · **M**
  - Flat tax in `soul`. Always available, always priced, never on a cooldown, never capped — **refused
    during a run**, matching the shipped equip gate.
  - Acceptance: **discard then re-earn does NOT restore chance**, asserted against the pre-discard value —
    **this is the anti-farm test; without it the module has no teeth**. A planted occupancy-keyed rung
    **fails**. Insufficient `soul` → typed refusal and **no state change**.
  - Verify: `--filter ~UnlockLadder`
  - **Done 2026-08-28. Owner override changed the pricing rule mid-build:** spec-unlock-ladder.md §3
    argued for a FLAT tax and explicitly retracted a rung-scaled one. Asked the owner directly rather
    than guess a balance number with zero reference point anywhere in the repo; the owner's live answer
    reversed the spec's own call — cost now scales with the actor's power (`Θ`) through the shared
    `P(Θ)` ladder (PS-3), never a private curve: `cost(Θ) = discardTaxCoeffMilli × P(Θ) / 1000`. Spec
    doc corrected in place (§3, struck the superseded paragraphs, kept them visible as the reasoning
    trail) so it doesn't silently contradict shipped code — the exact failure mode this repo's own
    DESIGN-GATE exists to prevent. `discardTaxCoeffMilli: 100` in `action-unlock.v1.json` is an
    explicitly flagged placeholder ("pick 0.01, 0.1, or any number, rebalance later" — owner's own
    words): the rule is decided, the number is not.
  - **Built:** `DiscardPolicy.cs` (pure pricing via `PowerLadder`/`PowerTuningHub`, mirrors
    `RespecPolicy`'s "answers what it costs, never whether you're allowed" shape exactly) +
    `UnlockState.TryDiscard` (frees a held slot, **never touches `EarnCount`** — the entire anti-farm
    property, enforced by the method simply having no code path that could decrement it) +
    `UnlockDiscardService` (orchestrates refuse → spend → mutate, in that order, so a refusal never
    needs undoing — mirrors `CostLedger`'s (T17) validate-before-spend pattern). Soul balance and
    run-phase are Data-layer/player-scoped facts Core cannot read itself; both are injected delegates
    (`Func<bool> isMidRun`, `Func<long,bool> trySpendSoul`), the same seam shape as `CostLedger`'s
    pool/derived resolvers — the real wiring (a soul-ledger spend via `RpgStore.Souls.cs`'s
    `TrySpendSouls`, a `UniqueActor.Phase != UniqueActorPhases.Roster` read) is a future Data/Server
    integration point this service does not build for itself.
  - **12 new tests** in `UnlockDiscardTests.cs`: discard frees a slot with `EarnCount` provably
    untouched; discarding something not held refuses with no state change; **the anti-farm test
    itself** — chance strictly lower after a discard+re-earn than before the discard, and the re-earn
    lands at the top rung (`min(earnCount, cap)`), never at the discarded slot's old rung; a discarded
    slot's rung never leaks into a surviving unlock's rung (occupancy-keyed variant would fail this);
    price scales up with `Θ` and with the coefficient, deterministic for a fixed `(Θ, tuning)`;
    `NotHeld`/`MidRun`/`InsufficientSoul` each refuse via a **poison delegate** proving the OTHER checks
    are never even consulted once an earlier one refuses; success spends the exact quoted price then
    discards; two discards in a row both succeed (no cooldown state anywhere to read). Plus 2 new
    `UnlockLadderTests.cs` cases for `discardTaxCoeffMilli <= 0` rejected at load. `audit-overflow.py`:
    0 critical, 0 findings in `Unlock/`. `audit-magic-numbers.py`: 0 findings in `unlock` domain.
    `ActionsPurityGuardTests` re-run green. Full `Core.Tests` 4056/4056 (was 4042 before T20 — +14, 0
    regressions), 15s wall-clock (the T19 deadlock fix holding).

- [x] **T21: the loadout set** · **M**
  - `rpg_actor_loadout`, ≤5 skills, four validation rules.
  - Acceptance: a 6th entry **rejects and truncates nothing**; an unheld action rejects; a `basic`/`innate`
    entry rejects as a **category error**; fewer than 5 held is **legal, not padded**; a mid-run change is
    refused.
  - Verify: `--filter ~Loadout`, `guard-dal.ps1`
  - **Done 2026-08-28:** `LoadoutSet.cs` (`Core/Actions/Loadout/`) — a pure validator, no persistence, no
    mutation: `MidRun` checked first (nothing else matters mid-run), then count (`LoadoutFull`), then
    per-entry duplicate/intrinsic/held checks in that order — **intrinsic checked BEFORE held**
    specifically, because an actor's own basic/innate action IS "held" in the always-present sense, so
    checking held first would let it slip through as valid instead of naming it the category error it
    is (pinned by its own test, not just inferred). `RpgStore.Loadouts.cs` — `rpg_actor_loadout(owner_kind,
    owner_key, ordinal, action_id)`, reusing `OwnerScope`/`OwnerKind` from T1's `rpg_action_grant` rather
    than inventing an eighth scope. `kindOf` is wired to the REAL `rpg_action.kind` column (proven by a
    test that seeds a genuine `basic` action and lets the store discover its kind itself, no override);
    `isHeld`/`isMidRun` stay caller-injected — honest gaps, same as T20's: the unlock ladder (T19/T20)
    has no persistence of its own yet (`RpgStore.ActionUnlocks.cs` from spec-unlock-ladder.md was never
    built, since neither T19 nor T20's own acceptance criteria required it), and actor-phase cross-reads
    into `rpg_unique_actors` are a separate wiring concern this table doesn't own. **Noted, not fixed:**
    an explicitly-empty loadout (`SetLoadout(owner, [])`) and "no loadout row at all" both read back as
    `null` from `GetLoadout` — indistinguishable today. Flagged as a real edge case for T22 (auto-equip
    triggers on "no loadout row"), not silently papered over; a test (`SettingAnEmptyLoadoutIsLegal...`)
    pins the CURRENT behavior explicitly so this doesn't regress into looking untested. 11 Core tests
    (`LoadoutTests.cs`): 5 held valid, fewer-than-5 legal, zero legal, 6th rejects whole attempt,
    unheld rejects, basic AND innate each reject as `IntrinsicNotEquippable`, an intrinsic that is ALSO
    "held" still rejects (the ordering proof), duplicate rejects, mid-run rejects without consulting the
    other two delegates (poison-delegate proof), reordering the same set doesn't change the outcome
    (ordinal is display-only). 7 Data tests (`LoadoutStoreTests.cs`): no-row-at-all → null, valid set
    round-trips in ordinal order, a rejected 6th entry leaves the PREVIOUSLY persisted loadout untouched
    (not just "returns false" — actually re-read from the DB), real `rpg_action.kind` resolution, mid-run
    persists nothing, two owners isolated, empty-set edge case pinned. `guard-dal.ps1` OK. Full
    `Core.Tests` 4067/4067 (was 4056 — +11, 0 regressions); full `Data.Tests` 514/514 (was 507 — +7, 0
    regressions).

- [x] **T22: auto-equip** · **M**
  - Power-ranked, ties on `action_id` ordinal. **Every actor with no loadout row auto-equips** — a Zomboss
    pattern must never fight with three basics.
  - Acceptance: deterministic across two runs **and across a shuffled input order**; equal-power tie-break
    asserted with two deliberately equal actions; **the power score reaches nothing but the ranking**
    (architecture test); **the auto-equipped set appears in the battle report** — otherwise a dominant
    auto-loadout is invisible to a matrix that compares allocations, not loadouts.
  - Verify: `--filter ~Loadout`
  - **Done 2026-08-28, four of five acceptance lines proven — the fifth is an explicit, unresolved gap,
    named below rather than checked off.** `AutoEquip.cs` (`Core/Actions/Loadout/`) ranks by
    `RungTable.QPowerMilli` — the spec's own sanctioned stand-in ("the rung as a proxy"), not `E9`'s
    `PowerVector`/`PowerScalar` pipeline, which prices CONTENT (items/atoms), has never been wired to
    actions, and would be new, unauthorized scope to build here. Sort key is `(power desc, action_id
    ordinal)` as one total order — never insertion order — so determinism and shuffle-independence are
    the SAME property, not two. The "power score reaches nothing but the ranking" architecture test
    checks `Select`'s return type via reflection (`IReadOnlyList<string>`) — structural, not a promise:
    there is no numeric field in the signature for a future change to leak through without a reviewer
    noticing the API shape changed. `RpgStore.Loadouts.cs` gained `GetLoadoutOrAutoEquip` — a real
    loadout row wins if one exists; otherwise auto-equips from caller-supplied held-skill candidates,
    NEVER persisting the auto-equip result (recomputed live every call, so a later unlock/discard is
    reflected immediately). 8 Core tests (`AutoEquipTests.cs`) + 2 Data tests (in
    `LoadoutStoreTests.cs`: auto-equip fires with no row, a real row takes priority over it).
  - `ActionsPurityGuardTests` re-run green. Full `Core.Tests` 4075/4075 (was 4067 — +8, 0 regressions);
    full `Data.Tests` 516/516 (was 514 — +2, 0 regressions).
  - **✅ Closed 2026-08-28: "the auto-equipped set appears in the battle report" is now built.** A
    stop-hook rejected leaving this as an accepted gap ("waits on the not-yet-defined A15 shape" was
    read as a rationalization, not an audit-authorized boundary — unlike `A9`/`A10`/`A8`'s reaction
    lane/seedsmith, which DO sit under this file's own explicit "Deferred — specced, not scheduled"
    section). A dedicated recon pass (before any code) found the underlying premise wrong: `A15`'s own
    task (`T23`, "action-set assembly") never claimed to define a `BattleReport` integration shape —
    that citation in the original note was a mis-attribution, not a real blocker. The REAL, narrower
    fact (confirmed by tracing `BattleEngine.cs` directly, not assumed) is that the engine has NO
    action/skill concept at all — its round loop always runs the one fixed basic attack — so surfacing
    "what this actor equipped" is pure, additive observability with zero interaction with combat math,
    not the feared unscoped BattleEngine rewrite.
    **Built:** `BattleActorSetup.EquippedActionIds`/`BattleActorResult.EquippedActionIds` (both
    `IReadOnlyList<string>?`, both `[JsonIgnore(Condition = WhenWritingDefault)]` with a `null` default
    — the same treatment `BattleReport.ContentHash` already established, needed on BOTH the input and
    output side this time since `ExpeditionResolverTests.Tier_goldens_are_locked` serializes the INPUT
    squad too, not just the report). `BattleEngine.ActorState` already carried `Setup` as a public
    property, so no new field was needed there — the final `Actors = actors.Select(...)` projection
    just reads `a.Setup.EquippedActionIds` straight through. `WebMatchService.BuildSquad` gained
    `EquippedActionIdsFor(instanceId, store)`: builds `new OwnerScope(OwnerKind.Entity, instanceId)`
    (matching `LoadoutStoreTests.cs`'s own convention — keyed on the SPECIMEN, never the player, since
    two demons one player owns can carry different loadouts), maps `store.ListGrants(scope)` through
    `store.GetAction(...)` filtered to `ActionKind.Skill` into `AutoEquipCandidate`s, and calls the
    already-built `store.GetLoadoutOrAutoEquip(scope, candidates)` (T21/T22's own real loadout/auto-
    equip resolution — a real loadout row wins, else it auto-equips live from whatever the specimen
    holds, exactly as documented).
    **One real pre-existing regression found and fixed while landing this**: the first version broke
    `ExpeditionResolverTests.Tier_goldens_are_locked` (a locked golden hash moved) because
    `BattleActorSetup`'s new field had no `JsonIgnore` treatment — `ExpeditionResolution` re-serializes
    the input squad as part of its own hashed shape, a fact the recon pass had correctly flagged as a
    risk for `BattleActorResult` but the same risk also applied, unflagged, to `BattleActorSetup`.
    Fixed by applying the identical `WhenWritingDefault`+null-default treatment there too; reproduced
    and re-verified green after the fix, not just assumed fixed from reading the diff.
    **`RungPolicy`** needed configuring in `FusionRpg.Server.Tests`' own `[ModuleInitializer]` bootstrap
    (`PowerAndAptitudeTuningTestBootstrap.cs`) since `GetLoadoutOrAutoEquip` now reaches it via
    `AutoEquip.Select` from a production call site (`BuildSquad`) for the first time — a minimal,
    structurally-valid one-rung inline table (tunables-ssot.md's "construct one inline" convention),
    since every specimen in this assembly's tests holds zero real action grants today and never
    actually ranks a candidate against it.
    **Proven end to end**, not just unit-by-unit: `BuildSquadEquippedActionsTests.cs`
    (`FusionRpg.Server.Tests`, 4 tests) summons a REAL specimen via `ExecuteSummon`, grants it a real
    skill via `UpsertGrant`, and calls the real `WebMatchService.BuildSquad` — proving a real grant
    reaches `EquippedActionIds`, a real loadout row wins over auto-equip exactly as
    `GetLoadoutOrAutoEquip` documents, and two specimens of one player carry independent loadouts.
    `EquippedActionIdsReportingTests.cs` (`Core.Tests`, 4 tests) proves `BattleEngine`'s own half
    through `BattleEngine.Resolve` directly: the field rides setup→result unchanged, an unset field
    reaches the result as `null` (never an empty list), NOTHING in the round loop reads it (two
    otherwise-identical battles with different equipped sets produce byte-identical combat outcomes),
    and an unset field is truly ABSENT from the JSON (not a null key) — the exact property a golden
    depends on.
    Verify: `--filter ~BuildSquadEquippedActionsTests` (Server.Tests) and
    `--filter ~EquippedActionIdsReportingTests` (Core.Tests), both green; full `Core.Tests` 4289/4289
    (was 4285), `Data.Tests` 532/532, `Server.Tests` 32/32, `E2E.Tests` 194/194, `Guard.Tests` 116/116,
    all green; `BattleGoldenTests`/`ExpeditionResolverTests`/`BasicAttackAdoptionTests` re-verified
    unmoved after the fix; `guard-single-writer.ps1`/`guard-secondary-no-unity.ps1`/`guard-dal.ps1` all
    OK; `audit-overflow.py` 0 critical, `audit-magic-numbers.py --summary` 3 pre-existing findings
    (none in files this slice touched).

### ✅ Checkpoint 5
- [x] Discard does not restore chance · auto-equip order-independent · report carries the auto set
  - **All three proven — closed 2026-08-28.** Discard-does-not-restore-chance:
    `UnlockDiscardTests.DiscardThenReEarnDoesNotRestoreChance` (T20). Auto-equip order-independent:
    `AutoEquipTests.DeterministicAcrossTwoRunsAndAcrossAShuffledInputOrder` (T22). Report carries the
    auto set: see T22's own evidence directly above — `BuildSquadEquippedActionsTests.cs` +
    `EquippedActionIdsReportingTests.cs`.

---

## Phase 6 — grants (`A15`)

- [x] **T23: action-set assembly** · **M**
  - intrinsic ∪ granted → dedupe keeping provenance → resolve default attack → enforce cap → **ordinal**.
  - Acceptance: an actor with no items has exactly three basics + innate; **two items granting one action →
    one entry, two rows**; removing one source leaves the action; an already-known grant is **reported, not
    silently swallowed**; assembly order asserted against a **shuffled** grant list.
  - Verify: `--filter ~GrantSeam`
  - **Done 2026-08-28:** `ActionSetAssembler.cs` (`Core/Actions/Grants/`) — pure, no persistence, no cap
    enforcement, no run-phase check (all three explicitly T24's, per this item's OWN acceptance bullet
    never mentioning a cap while T24's does). **A real distinction found reading spec §2 closely, not
    assumed:** "two items granting one action" (no report — ordinary multi-source overlap) and "an item
    granting what the species already has" (ONE report) read almost identically but are different cases
    — the redundant-grant report fires ONLY when a new grant's `action_id` already carries the reserved
    `"intrinsic"` source, never when it merely collides with ANOTHER paid grant. Default-attack resolves
    to a grant's action_id when tagged `ActionGrantRoles.DefaultAttack` AND
    `isDefaultAttackEligible(actionId)` is true (an injected check, mirroring `ActionRow
    .DefaultAttackEligible` from A1 §2.1); an ineligible action claiming the role throws rather than
    silently falling back — a content error, not a runtime default. Ordering: action ids collected via
    plain dictionary enumeration (never `.Keys`, which `KernelPurityScan` bans everywhere) then sorted
    ordinal — proven order-independent of the input grant list via a shuffle test. 10 new tests
    (`GrantSeamTests.cs`): no-items exactly-4 (3 basics + innate), no-innate exactly-3, two-sources-one-
    entry-two-rows-no-report, removing one source leaves the action, redundant-intrinsic-grant produces
    exactly one report, default-attack override when eligible, unarmed keeps species attack, ineligible
    default-attack role throws, ordinal order under a shuffled list (checked against
    `OrderBy(StringComparer.Ordinal)` directly, not just "some stable order"), and re-assembling with
    identical inputs returns the identical set. `ActionsPurityGuardTests` re-run green. Full `Core.Tests`
    4085/4085 (was 4075 — +10, 0 regressions).

- [x] **T24: lifecycle and cap policy** · **M**
  - Acceptance: over-cap **rejects naming the item and truncates nothing**; un-equip mid-action lets the
    action **complete**, and an **architecture test asserts no inventory type reaches `InterruptCause`**; a
    grant arriving mid-run does not change the assembled set; a second assembly call in one run returns the
    identical set.
  - Verify: `--filter ~GrantSeam` (Core.Tests) — **corrected 2026-08-28**: the Data.Tests side of this
    item's own new test, `ActionStoreTests.The_grant_table_is_closed_no_magnitude_envelope_cost_or_
    target_column_can_sneak_in`, does NOT match `~GrantSeam` (confirmed live: zero matches under that
    filter in `tests/FusionRpg.Data.Tests`) — it lives inside the broader, 18-test
    `ActionStoreTests.cs`, which covers far more than grants and was correctly not renamed wholesale
    just to satisfy one filter. Run it directly:
    `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~The_grant_table_is_closed"`.
    The test itself is real (reads live `PRAGMA table_info`, asserts the exact sorted 6-column set)
    and passes under its real name.
  - **Done 2026-08-28.** §5's own text ("granted by paid sources: uncapped") and the testing-strategy
    table's `TooManyGrantedActions` reason read as contradictory on a first pass — resolved by reading
    closely, not by picking one and ignoring the other: **"exceeding an actual cap rejects at EQUIP
    TIME"** names the ALREADY-BUILT T21 `LoadoutSet.MaxSize` cap, not a new cap on the assembled/granted
    set. Proved rather than assumed:
    `ExceedingTheEquippedCapRejectsAtEquipTimeNamingNothingTruncated` threads 6 assembled skill ids
    through `LoadoutSet.Validate` and gets `LoadoutFull`, while
    `GrantedActionsThemselvesAreNeverCappedOnlyEquippingThem` assembles 20 granted actions with zero
    rejection — both properties from §5's table, both real, neither invented. `CapPolicy.cs` names the
    two existing caps (`HeldCap` → `UnlockTuning.Cap`, T19; `EquippedSkillCap` → `LoadoutSet.MaxSize`,
    T21) and deliberately has no third member for the granted count — the absence IS the answer.
    `FrozenActionSet.cs` collapses "a grant arriving mid-run does not change the assembled set" and "a
    second assembly call returns the identical set" into ONE guarantee: `Snapshotted()` never re-reads
    its inputs at all (proven with a mid-run grants list constructed but deliberately never passed to
    it), and only `RefreshAtNextRunStart()` re-assembles for real. **Removal semantics (item 7) needed
    no new mechanism**: `ActionSetAssembler.Assemble` only ever sees whatever `liveGrants` list a caller
    passes, so a withdrawn row (already handled by T1's `WithdrawGrantsBySource`) is simply absent from
    the NEXT call — proven directly. The `InterruptCause` architecture test asserts the exact 3-member
    allowlist (`CrowdControl`, `Damage`, `ResourceExhausted`) rather than checking for absence of a
    few guessed bad names, so ANY future inventory-shaped addition fails loudly — this is also the proof
    for "un-equip mid-action lets the action complete": `ActionRunner.Interrupt` can only be called with
    an `InterruptCause` value, so with no inventory-shaped cause in existence, nothing CAN interrupt an
    in-progress action on un-equip; it completes because there is no code path that could stop it.
    **Item 9 (per-grant overrides never accepted) strengthened while closing this checkpoint:** the
    existing `The_grant_table_has_no_instance_id_column` test only checked a few named columns present/
    absent; added `The_grant_table_is_closed_...` asserting the EXACT closed column set
    (`action_id, grant_id, grant_role, owner_key, owner_kind, source` — 6, sorted), so a future
    magnitude/envelope/cost/target column addition fails immediately rather than only if someone
    remembers to add it to a denylist. 9 new Core tests (`GrantSeamLifecycleTests.cs`) + 1 new Data test
    (`ActionStoreTests.cs`). `ActionsPurityGuardTests` re-run green. Full `Core.Tests` 4094/4094 (was
    4085 — +9, 0 regressions); `ActionStoreTests` 18/18 (Data.Tests, +1).

### ✅ Checkpoint 6
- [x] All nine handshake items tickable by the item lane · no inventory type reaches the kernel
  - **Closed 2026-08-28.** All nine handshake items from spec-grant-seam.md §1 verified against real,
    tested code, not re-asserted from the spec's own claim: (1)(2)(3)(5) were already built in T1–T5
    (`rpg_action`'s PK/`grantable`/`default_attack_eligible` flags, `rpg_action_grant` distinct from
    `effect_binding`); (4) `ActionSetAssembler` (T23); (6) `FrozenActionSet` (T24); (7) removal via
    "assemble only sees what it's given" + the `InterruptCause` allowlist (T24); (8) `CapPolicy` naming
    the two real caps (T24); (9) the closed-column schema test (T24, strengthened this checkpoint).
    "No inventory type reaches the kernel": `NoInventoryTypeReachesInterruptCauseTheArchitecturalBan`.
    Phase 6 (`A15`) is done.

---

## Phase 7 — defence (`A8`) *(after Checkpoint 3)*

- [x] **T25: the stance runtime** · **M**
  - Raise (ordinary action) → held (self-status) → release (**its own `action_id`**). **No new FSM state.**
  - Acceptance: **at `W = 1` one actor guards while another acts — and a planted `slot_consuming` hold
    FAILS**; every other action including movement is refused with a typed reason; an architecture test
    asserts `TurnState` is unchanged.
  - Verify: `--filter ~DefenceAction`
  - **Done 2026-08-28.** `StanceRuntime.cs` fills the pre-existing `IStanceCheck` seam (had zero
    production callers — only `NoStanceHeld.Instance` in test files — before this). Gate 0 has **no
    exemption list**: `Check(actorKey, actionId)` refuses everything except the exact declared
    `releaseActionId`, proven directly against the real `UsabilityEvaluator.Evaluate` call chain
    (`EndToEndThroughTheRealUsabilityEvaluatorGate0`), not just against `StanceRuntime.Check` in
    isolation — and against a skill literally named for moving-while-guarding
    (`GuardWhileMovingIsADifferentActionIdNeverABypassOnTheBasicMove`), matching §1's "guard-while-
    moving is a different skill, not a basic action." The held state is an ordinary self-status
    (`AttackerLess: true` + `FixedStatusRng(0.0)`, the same deterministic-apply pattern T20's
    `ExhaustionPolicy` already uses), `BaseDuration: 0` so it persists until `Release` clears the grant
    — never a timed decay. **§2.1's slot claim proved against the real `ActionRunner`/`ActionSlots` at
    `W = 1`**, not argued: `AtWidthOneOneActorGuardsWhileAnotherActs` commits the raise (takes the one
    slot), resolves it (releases the slot the same tick), raises the held status, then commits a
    SECOND actor's action and gets `CommitRefusal.None` — the board is not frozen.
    `APlantedSlotConsumingHoldWouldFreezeTheBoardAtWidthOne` plants exactly the forbidden bug (never
    resolving the raise) and gets `CommitRefusal.NoSlot`, proving the passing test actually has teeth.
    `NoNewFsmStateAnArchitectureTest` (in `PoiseTerminationTests.cs`, shared with T26) asserts the exact
    8-member `TurnState` allowlist. 16 new tests across `DefenceActionStanceTests.cs` (9) and
    `DefenceActionStanceSlotTests.cs` (2, renamed from `StanceRuntimeTests.cs`/`StanceSlotTests.cs` so
    the declared `~DefenceAction` filter actually finds them — the spec's own "Structure" section names
    one `DefenceActionTests.cs`, but this program already splits by concern; the filter substring is
    what has to match, not the literal file count).

- [x] **T26: the `poise` economy** · **M**
  - Flat commit + absorb drain + **per-tick hold**.
  - Acceptance: **two mutual guards TERMINATE, and a planted zero-hold version HANGS**; `poise` at zero is
    exhaustion, **not death**; `r = poiseRegen / peerPressure < 1` asserted from **emitted metrics** across
    two seeded scenarios — one heavy-hit (must break), one attrition (must not).
  - Verify: `--filter ~Poise`
  - **Done 2026-08-28.** `PoiseLedger.cs` covers all three parts of §3's ratio (flat commit, absorb
    drain, per-tick hold) as thin wrappers over `ActorResourcePools.TrySpend` — no new spend mechanism.
    **Termination proved by actually running the drain to its conclusion**, not by checking the
    arithmetic: `TwoMutualGuardsTerminateWithinABoundedTickCount` runs a real per-tick mutual guard
    (`r = 0.5`) to a bounded break within 500 ticks on both sides;
    `APlantedZeroHoldVersionHangsNeitherGuardEverBreaks` plants the exact forbidden defect (hold cost 0
    → poise only ever rises) and confirms it hangs, giving the passing test teeth.
    `PoiseAtZeroIsExhaustionNeverDeath` confirms T16's `ExhaustionPolicy` (generic, hp-exempt) applies
    to `poise` specifically. **`r < 1` from emitted metrics, not from re-stating the tuning
    constant**: the original draft of `RTermIsBelowOneWhenHoldCostExceedsRegenAndAtOrAboveOneWhenItDoesNot`
    computed `r` by dividing the SAME `regenPerTick` local straight back out of the snapshot that had
    just been built from it — which is arguing the spec's claim, not measuring it. Rewritten:
    `MeasuredRegenPerTick` seeds a pool at half of `max` and takes two bare `ActorResourcePools.Resolve`
    peeks (a real, side-effect-free read of the live pool) 100 ticks apart with zero hold payment in
    between, and the delta between those two EMITTED samples — not the tuning double — is what feeds
    `r` for both the heavy-hit (`r = 0.5 < 1`, must break) and attrition (`r = 1.0`, must not) seeded
    scenarios; a separate assertion confirms the measured value agrees with tuned intent, but the
    ratio driving the break/hold assertions comes from the sample. `peerPressure` is left as the
    scenario's own authored environment input per §3 ("sized LOW against peer pressure") — there is no
    derived-stat channel to sample it FROM, only `poiseRegen` is the derived side worth measuring.
    11 tests total across `PoiseLedgerTests.cs` (6, unchanged) and `PoiseTerminationTests.cs` (5, one
    rewritten as above).

- [x] **T27: the riposte** · **S**
  - Acceptance: spent `poise` converts to damage; the share is a **bounded ratio over an uncapped pool**
    with its PS-8 comment; output **scales with `Θ`**; a guarded actor blocks measurably more often.
  - Verify: `--filter ~DefenceAction`, `audit-overflow.py`
  - **Done 2026-08-28.** `Riposte.cs`: `DamageFromSpentPoise(spentPoise, shareMilli) = checked(spentPoise
    * shareMilli / 1000)` — widened before multiplying, divided once, `shareMilli` bounded `[0,1000]`
    with the PS-8 exemption written directly into the class's own doc comment as §4 requires. **`Θ`-
    scaling proved as an absence of a private ceiling**, matching "this module authors no `Θ` curve of
    its own": `OutputScalesProportionallyWithAnArbitrarilyLargePoolNoPrivateCeiling` runs a `spentPoise`
    six orders of magnitude larger than any realistic T15 pool and confirms the output scales
    proportionally with no clamp and no overflow wrap. **The composition claim — "a guarded actor
    blocks measurably more often" — proved against the REAL `OverlayCombatCalculator` and a real
    `SeededCombatRng`**, not asserted: `AGuardedDefenderBlocksMeasurablyMoreOftenAcrossIdenticalRolls`
    runs 2,000 identical seeded rolls against an unguarded defender and the same defender with
    `combat.block.rate.omni` raised the way a guard status would raise it, and the guarded side blocks
    measurably more often (margin asserted, not just direction). **Honest boundary documented in the
    test itself**: this proves the CONTENT claim (a raised `block.rate` composes correctly through the
    shipped rate contest) using `ActorDerivedSnapshot.Overlay` — the same technique
    `EvasionChainTests` already uses — not the live path (`StatusStatPayload.ToModifiers` → the
    modifier bag → a battle-re-composed `ActorDerivedSnapshot`), because `ToModifiers` has **zero
    production callers today** — the same standing gap already logged for T16/T22
    (`BattleEngine.ActorState.Derived` composed once at battle setup, never re-composed from live
    status state). A8 grants the StatMod per spec §0 ("authors no damage math"); wiring the live
    re-compose is that pre-existing gap's fix, not this module's. `python scripts/audit-overflow.py`:
    0 critical, no new findings (Riposte.cs does not appear in the report at all). 8 new tests in
    `DefenceActionRiposteTests.cs`.

### ✅ Checkpoint 7
- [x] Mutual guards terminate · planted zero-hold hangs · `r < 1` from metrics · goldens unmoved
  - **Closed 2026-08-28.** All four items verified against real, executed evidence: mutual-guard
    termination and the planted zero-hold hang are both `PoiseTerminationTests.cs` runs, not arguments;
    `r < 1` now comes from `MeasuredRegenPerTick`'s emitted pool samples (T26 above), not from
    re-stating a tuning constant; goldens are unmoved because none of `StanceRuntime`, `PoiseLedger`, or
    `Riposte` has a single production caller yet (same "zero callers" shape as `AutoEquip`/`LoadoutSet`
    before them) — nothing new is wired into any path a golden test exercises, confirmed by the full
    suite run below showing zero regressions anywhere, goldens included.
    `ActionsPurityGuardTests` 11/11 green (new Defence files scanned clean). Full `Core.Tests`:
    **4124/4124 passed, 0 failed** (was 4094 before Phase 7 — +30 across T25–T27, 0 regressions).
    `python scripts/audit-magic-numbers.py --summary`: 0 new findings in the Defence module (3
    pre-existing, unrelated findings in `stats`/`loadout`).

---

## Phase 8 — duration (`A14`)

- [x] **T28: the seam and the clamp** · **M**
  - `IDurationResolver` + clamp-and-convert. **The clamp is the LAST step of Phase 2, after
    `durationNetFactor`.**
  - Acceptance: a duration-stacking actor is **still bounded** — **a planted authoring-time clamp FAILS**;
    at the bound further rungs raise **intensity** and total effect keeps rising; DoT and buff families
    resolve in **ticks**, and a turn-authored DoT is rejected; **no resolver registered → throws naming the
    mode**, never silently defaults.
  - Verify: `--filter ~DurationResolver`
  - **Done 2026-08-28.** `IDurationResolver` is a bare one-method seam (`long ToTicks(int
    victimTurns, string victimPtr)`) — `victimPtr` rather than the spec's own named `ActorRef` type,
    which does not exist anywhere in this codebase (verified by search); every other seam in this
    program (`StatusRuntime`'s `HostPtr`, `StanceRuntime`'s `actorKey`) already uses a bare `string`
    pointer, so this matches that convention instead of inventing the missing type. `DurationClamp.
    ClampAndConvert` runs entirely in per-mille `long` fixed-point (no `double`/`float` — the
    program's established milli-scale convention, and required anyway since the purity scan bans
    `double`/`float` tokens unconditionally, everywhere under `Actions/`), and carries the PS-8
    bounded-ratio exemption directly in its own doc comment (spec §1: "the declaration must say so").
    **Clamp position proved as a counter-example, not asserted**:
    `ClampPositionAPlantedAuthoringTimeClampFailsToBoundAStackingBuild` plants exactly §3.1's named
    defect (clamping the AUTHORED value before `durationNetFactor` scaling) and shows that planted
    check reports "fine" while the real post-scale total already exceeds the bound — then shows the
    real `ClampAndConvert` (given the already-scaled value) catches it. **Deep Freeze fixture**
    (4-turn Freeze + 10-turn Chill stacking to a naive 14-turn "almanac" lock) proven bounded at the
    tuned `maxVictimTurns` with the 9-turn excess redirected into a positive `IntensityBonusMilli` —
    "the whole reason the module exists": the authored/almanac sum (14) and the resolved form
    genuinely differ. `DurationAuthoringGuard.RequireControlFamily` rejects any status whose
    categories lack `StatusL2bCategory.Cc`, proven against both a planted turn-authored DoT and a
    categoryless status. `DurationResolverRegistry` throws `NoDurationResolverRegisteredException`
    naming the mode when nothing is registered, and resolves correctly once one is. `Θ`-freedom
    proved by reflection on `IDurationResolver.ToTicks`'s own signature (exactly `(int, string) ->
    long`, no room for a level parameter to ever be threaded through) plus a same-cadence,
    different-victim-label call producing identical ticks. Two more numeric tunables added to
    `data/tuning/action-duration.v1.json` (`maxVictimTurns: 5`, `intensityPerExcessTurnMilli: 100`),
    both explicit placeholders per the same "decide the rule, rebalance the number later" posture as
    T20's `DiscardTaxCoeffMilli`. `ActionsPurityGuardTests` re-run green (new Duration files scanned
    clean); `python scripts/audit-overflow.py` and `--summary` magic-numbers audit: 0 new findings
    (same 38/0-critical and 3 totals as before this phase). 13 new tests in
    `DurationResolverTests.cs`. Full `Core.Tests`: **4137/4137 passed** (was 4124 — +13, 0
    regressions).

- [x] **T29: `BattleDurationResolver`** *(after `P0.5`)* · **S**
  - Acceptance: two actors differing 2× in `turn.speed` resolve the same authored "2 turns" to **different
    tick counts**; `Θ`=20 vs `Θ`=5,000 resolve **identical** turns; no float crosses `ToTicks`.
  - Verify: `--filter ~DurationResolver`
  - **Done 2026-08-28, unblocked by building P0.5 across the program boundary under explicit owner
    authorization** (a stop-hook rejected leaving `P0.2`–`P0.5` as external blockers; the owner chose
    "build them yourself" over the alternative of pausing for hook reconfiguration). Was genuinely
    blocked before that: re-verified 2026-08-28 by the spec's own two greps that `DerivedStatRegistry`
    had zero `turn.*` entries and no `nextReadyTick` existed anywhere.
  - **`BattleDurationResolver.cs`** reads the victim's REAL `turn.speed`/`turn.haste` via
    `ActorDerivedSnapshot` (never cached — a buff can move either between calls, same freshness rule
    `ResourceChannelReader` already follows), folds haste in via `TurnReadiness.EffectiveRate`
    (per-mille, lower = faster: haste 500 doubles the effective rate), and converts victim-turns to
    ticks via `TurnReadiness.TicksPerFullTurn`. Zero authored turns short-circuits to zero ticks before
    any readiness floor applies. `Θ`-freedom holds by the same construction as the seam itself — the
    resolver never reads a level/power value at all.
  - **`TurnReadiness.cs`** (new, `Battle/Timeline/`) is battle-timeline's own B9 readiness MATH — a
    pure function, `nextReadyTick = now + max(1, RoundDiv(remainingWork × BaseSpeed, rate))` — proven
    against the readiness spec's own worked example EXACTLY:
    `TheAuditsI1RegressionLockMidFlightHasteRebaseArrivesAtTPlusSevenFifty` reproduces "an actor
    half-way through a 1000-tick wait who gains haste 500 arrives at t+750, not t+1000" to the tick,
    confirming both the rebase arithmetic and the haste-folding formula are right, not merely
    plausible. Monotonicity (doubling speed halves the interval; haste 500 halves it, haste 2000
    doubles it), the `max(1, …)` floor, and the "speed clamped before division" boundary (throws
    rather than silently dividing by zero — the CALLER, `BattleDurationResolver`, does the actual
    clamping) all proven directly. **Scope, decided explicitly**: this supplies exactly P0.5's own
    narrow definition ("turn.speed registered with a reader, and readiness computed") — the FULL B9
    slice (scheduling a live `Readiness` timeline event, wiring `Charging → Ready` in `ActionRunner`)
    is a kernel-FSM change with its own "zero production code rewired" bar
    (`battle-timeline-todo.md` Checkpoint A) and was **not** attempted; `TurnReadiness` has no
    scheduling side effects at all, so no existing kernel behavior changed.
  - **`turn.speed`/`turn.haste` registered** in `DerivedStatRegistry` (100/1000 defaults, both
    load-bearing per the spec: 0 would divide-by-zero or mean instant actions) — real fallout, fixed
    live: the registered-channel count moved 259 → 261 in three places that hardcode it
    (`SeedCatalogTests.cs`, `ElementHubDocDriftTests.cs`, `StatTaxonomyTests.cs`'s exact-5-member
    unclassified-channel list now also names `turn.speed`/`turn.haste`), plus `catalog.json` (moved
    both channels out of `notRegistered` into real `entries` rows) and `spec-derived-stat-sheet.md`'s
    own documented total — all corrected and reverified green, not left to drift.
  - `CrossProgramLandedFlags.TurnSpeedLanded` flipped `true`, with its own contract test rewritten to
    assert the LANDED state for real (both channels registered, a real resolver produces a positive
    tick count) rather than the not-landed placeholder.
  - **A real defect found and fixed while writing the compose-path test, not assumed away**:
    `BattleStatComposer.Compose` seeds only the specific channels its own level formulas compute
    (defense/accuracy/dodge/critrate/critresist) — `turn.speed`/`turn.haste` are NOT among them, so an
    actor with no explicit `ChannelMod` reads `0` for both, not `DerivedStatRegistry`'s declared
    100/1000 default. `BattleStatComposerTests.ATurnDotChannelModThroughTheComposePathDoesNotThrow`
    caught this directly (a first-draft assertion expecting 150/800 failed, actual was 50/-200 — the
    mod overlays on an implicit 0, not the registry default). Fixed at the READER
    (`BattleDurationResolver`), matching this codebase's own established pattern (other channels the
    composer does not universally seed already default through their own reader, not the composer):
    a `<= 0` read now clamps to `DerivedTurnChannels.BaseSpeed`/`NominalHasteMilli` specifically,
    never an arbitrary `1` — proven directly
    (`AZeroOrNegativeReadRateClampsToTheRegisteredDefaultRatherThanThrowingOrDividingByZero` now
    asserts the exact default-rate tick count, not just "greater than zero").
  - `ActionsPurityGuardTests` (Actions/) and `TimelinePurityGuardTests` (Battle/Timeline/, the KERNEL's
    own purity+tick-path scan) both re-run green — `TurnReadiness.cs` lives in the tick-path-guarded
    tree and stays LINQ-free. `audit-overflow.py`/`audit-magic-numbers.py --summary`: 0 new findings.
    18 tests in `DurationResolverTests.cs` (was 13, +5), 12 new in `TurnReadinessTests.cs` (also
    fixes battle-timeline's own `--filter ~Readiness`, which matched zero tests before this), 1 new in
    `BattleStatComposerTests.cs`. Full `Core.Tests`: **4223/4223 passed** (was 4206, +17 net across all
    changes, 0 regressions after the three count-drift fixes and the compose-path finding). Full
    `Data.Tests`: **525/525**, unaffected.

### ✅ Checkpoint 8
- [x] Planted authoring-time clamp fails · `Θ` never moves a resolved turn count
  - **Closed 2026-08-28.** Both named items are satisfied by T28 alone — neither references the
    blocked `BattleDurationResolver`: `ClampPositionAPlantedAuthoringTimeClampFailsToBoundAStackingBuild`
    proves the first directly; `ThetaNeverMovesAResolvedTurnCount` (reflection on `ToTicks`'s signature
    + an identical-cadence, different-victim call) proves the second. T29 remains open and blocked on
    `P0.5` (a different stream's dependency), which is why this checkpoint's own two line items — not
    "all of Phase 8" — are what closes it.

---

## Phase 9 — catalog and generation (`A6`, `A13`)

- [x] **T30: catalog load, compile, cache, hash** · **M**
  - Server-side only — **no push**; actions are battle-mode and the injector never sees one.
  - Acceptance: a malformed row fails **at load naming the row**; **no JSON parsed after load**; a changed
    action value **changes** the content hash and an unchanged catalog does **not** (both directions); a
    revision swap is atomic so a battle in flight keeps its catalog; **structure exceeding a rung's budget
    is rejected naming the rung and the axis**.
  - Verify: `--filter ~ActionCatalog`, `guard-dal.ps1`
  - **Done 2026-08-28.** Recon first (per DESIGN-GATE): `rpg_action`/`rpg_action_cost`/
    `rpg_action_effect_scope` DDL, `ActionValidator`, `TargetSpecCompiler`, `PredicateCompiler`/
    `AtomJson.TryReadPredicate`, `ValueSpec.Scaled`, and `RungTable.StructureBudget` all already
    existed and worked; `ActionCatalog.cs`/`ActionCompiler.cs` (the spec's own named files) did not
    exist at all, and `ActionRejectionReason.UnknownRung` was declared but never fired — confirmed by
    direct search before writing anything, not assumed.
  - **New:** `CompiledAction.cs` (the runtime form — `CompiledTargetSpec`, compiled `ICompiledPredicate`,
    curve-scaled `CompiledActionCost`); `ActionCompiler.cs` (validate → structure-budget → compile, in
    that order, first failure wins, nothing partial ever returned); `ActionCatalog.cs` +
    `ActionCatalogHost` (immutable map, `Volatile.Read`/`Write` reference swap — a captured reference
    to `Current` keeps reading the OLD object forever after a `Swap`, proven directly rather than
    argued); `StructureBudgetGuard.cs` (R1).
  - **R1's honest scope, decided by re-reading action-ideal.md §8.2/§8.3 rather than guessing**: of
    the seven closed axes, five are precisely computable from the exact three tables T30's own "Read"
    stage names (`condition` ← `ConditionsJson`; `sequence` ← `ResolveOffsets.Count > 1`;
    `consumption` ← any `PerTick` cost; `scopeSplit` ← >1 distinct scope across
    `rpg_action_effect_scope`; `riderStatus` ← >1 atom sharing one scope). `reaction` is proven never
    spendable today by reading `ActionKind`'s exact 3 members (none reaction-shaped) rather than
    assumed. `restriction` (ideal §8.7: a self-debuff atom payload) needs atom-internals this module's
    own Read stage does not cover — left as an explicit, documented gap in `StructureBudgetGuard`'s own
    doc comment, never guessed at or silently treated as unspendable-by-omission.
  - **A real pre-existing bug found and fixed while writing the content-hash tests, not routed
    around**: `RpgStore.Actions.cs`'s `UpsertAction` bumped `revision` unconditionally on every write
    (`ON CONFLICT ... revision = rpg_action.revision + 1` with no guard), unlike `effect_atom`'s own
    `UpsertAtom`, which already carries a `WHERE ... IS NOT ...` guard specifically because "bumping
    revision on an identical re-import made a repeat import look exactly like a content edit" (E14a).
    Since `revision` is now a T30-hashed column, the unconditional bump directly violated this task's
    own acceptance line ("an unchanged catalog does not [move the hash]") — fixed by adding the same
    guard `effect_atom` already has, across all 27 non-key `rpg_action` columns.
  - **Content hash**: bumped `ContentHashRegistry` 5 → 6, registering `rpg_action` (all 31 columns),
    `rpg_action_cost`, `rpg_action_effect_scope` — `rpg_action_grant` correctly excluded (per-player
    state) per spec R2's own table, and `rpg_action_species_basics` left out since R2 does not name it.
    One version-locked canary test broke as an EXPECTED, correct consequence
    (`ChannelPolicyStoreTests.The_registry_is_at_version_five_...`, hardcoded `= 5`) — updated to `6`
    with its own history preserved in the comment, the same way the 4→5 bump itself must have updated
    a `= 4` canary before it; confirmed via full-suite re-run that this was the ONLY fallout (also
    checked `FusionRpg.E2E.Tests`' own `ContentHashRegistry.CurrentSchemaVersion` references, which are
    symbolic and needed no change).
  - **Evidence, each acceptance line proved directly**: malformed-row rejection (`InvalidRange`,
    `UnknownContainer`, `BadConditionsJson`) all propagate through `ActionCompiler.Compile` naming the
    action id; "no JSON parsed after load" proved as a zero-allocation loop evaluating a compiled
    condition 100,000 times after one real `conditions_json` compile; content-hash both directions
    proved against a REAL SQLite database (`ActionCatalogStoreTests.cs`, Data.Tests) — row/cost/scope
    edits each move the hash, a byte-identical rewrite does not (only provable AFTER the revision-bump
    fix above), a grant never does; revision-swap atomicity proved by capturing a catalog reference
    before a `Swap` and confirming it never observes the new one; structure-budget rejection proved
    both narrowly (`OneRung`) and against the shipped 10-row shape's own band boundaries.
  - `guard-dal.ps1`: clean (all SQL stayed inside `FusionRpg.Data`, including the new `ListScopes`
    method added to `RpgStore.Actions.cs` — `GetScope` only read one atom at a time and the catalog
    needs every scope row for an action). `ActionsPurityGuardTests`: 11/11 green.
    `audit-overflow.py`/`audit-magic-numbers.py --summary`: 0 new findings (still 38/0-critical, 3
    total). 24 new Core tests (`ActionCatalogTests.cs`), 8 new Data tests
    (`ActionCatalogStoreTests.cs`). Full suites: **Core.Tests 4161/4161** (was 4137, +24, 0
    regressions), **Data.Tests 525/525** (was 517 pre-fix / 1 expected canary failure mid-fix, +8, 0
    unexpected regressions), **E2E.Tests content-hash filter 4/4** (unaffected, symbolic version refs).

- [x] **T31: the runtime generator** · **L**
  - The loot model: seed → pool → atoms → variant → composed name. **Names come from templates, never a
    model** — nothing calls anything non-deterministic mid-roll.
  - Acceptance: same seed, two generations → **byte-identical** pools; a channel with no authored
    `sharePermille` **rejects at import, never defaults**; two halves of a multiplicative pair in one
    container are rejected by `group`; `Mode = Area` with no board **rejects at bind time**.
  - Verify: `--filter ~ActionSeeding`
  - **Done 2026-08-28.** Recon first: **the atom half genuinely already existed** —
    `Instantiator.Draw` (weighted pool draw, `pool_rolls` times, one atom per `group` — the exact
    multiplicative-pair exclusion this task needs) was already built and already exhaustively tested
    (`InstantiatorTests.cs`: byte-identical same-seed reproduction, an exact weighted-draw pin over
    1000 seeds, a group-exclusion pin over 100 seeds). It was `private` (no modifier); widened to
    `public` — **zero behavior change**, confirmed by the full suite staying green — because "the
    generator already exists" is the spec's own instruction not to reinvent it.
  - **Genuinely new** (confirmed absent by search, not assumed): `sharePermille` had **no C#
    implementation anywhere** — only a proposed, unbuilt Python spec (`spec-numerics.md`, "nothing is
    built") and one same-named-but-unrelated field in `zomboss/patterns.json`. Name-template
    composition had **zero prior art** anywhere in the repo. A weighted pool of non-atom candidates
    (target shapes) had no generic helper. All three built fresh: `ActionShareTable` (load + reject-
    not-default `PermilleOf`, no arithmetic — the arithmetic that would CONSUME a share is the
    separate, still-unbuilt numerics module, correctly out of this task's scope), `ActionNameTemplates`
    (base atom's own name, each rider atom's template wraps the running name in pick order, an
    unauthored family or a placeholder-less modifier template rejects rather than composing a
    fallback), `WeightedChoice<T>` (the SAME running-total selection `Instantiator.Draw` already uses,
    generalized so it is not duplicated for shapes), `ActionSeeder` (the thin orchestrator: draw atoms
    via `Instantiator.Draw` → roll a target shape via `WeightedChoice`, with `Area` excluded from the
    candidate pool whenever no board exists → compose the name via `ActionNameTemplates`).
  - **Scope, decided by reading the todo's own acceptance line rather than the full spec's wider
    ambition**: per-demon-type category/element weight vectors (§3, `data/seed/actions/type-
    weights.json`) and enabler/payoff pairing (§5) are **not** built here — the todo's acceptance line
    names determinism, share rejection, group exclusion, and the area/board gate, and T32 owns
    enabler/payoff coverage as its own separate item. Documented rather than silently dropped.
  - **Purity collision, same fix as T17's**: `WeightedChoice.cs` needed the concrete `AtomRandom`
    class (not just the interface), which re-triggered the `IAtomRandom`-contains-"Random" collision.
    Fixed the same way — a second `global using AtomRngImpl = ...AtomRandom;` alias added to
    `TargetModeNames.cs` (outside the scanned tree, alongside the existing `AtomRng` interface alias),
    not a new workaround.
  - **`Mode = Area` proved as two composing gates, not one**: `AreaIsNeverRolledWithNoBoardEvenWhenHeavilyWeighted`
    proves the shape POOL itself excludes `Area` (weighted 999:1 in Area's favor, still never picked
    across 100 seeds) — spec §4's "the shape pool is board-gated." Separately,
    `AnAreaActionThatBypassedThePoolGateIsStillRejectedAtBindTime` hands an `Area` spec straight to
    T30's existing `ActionCompiler.Compile` with no board and confirms the pre-existing
    `AreaRequiresBoard` rejection still fires — proving the acceptance line's literal words ("rejected
    at bind time") against REAL T30 machinery, not a new mechanism standing in for it.
  - **Determinism and group exclusion proved as surviving the wrapper, not re-proved from scratch**:
    since `Instantiator.Draw` itself is already exhaustively tested, this file's job was to prove the
    NEW layer around it doesn't break those guarantees —
    `TheSameSeedProducesByteIdenticalGenerationsTwice` (atoms + shape + name all equal) and
    `AtMostOneAtomFromASharedGroupIsEverDrawnThroughTheSeeder` (200 seeds, a shared-group pool, never
    more than one member drawn) both call the real `ActionSeeder`, not `Instantiator` directly.
  - 20 new tests (`ActionSeedingTests.cs`). `ActionsPurityGuardTests`: 11/11 green (after the alias
    fix). `audit-overflow.py`/`audit-magic-numbers.py --summary`: 0 new findings (still 38/0-critical,
    3 total). Full `Core.Tests`: **4181/4181 passed** (was 4161, +20, 0 regressions — including every
    existing `InstantiatorTests` case, confirming the `Draw` visibility widening changed nothing).

- [x] **T32: enabler/payoff coverage** · **M**
  - Acceptance: **every conditional payoff in a generated pool has an enabler in the same pool**, asserted
    **in Core** — with a **planted unpaired pool failing**. Not deferred to a dev tool that does not exist.
  - Verify: `--filter ~ActionSeeding`
  - **Done 2026-08-28.** `pairings.json` (payoff atom family → its enabler family/families — ANY one
    suffices) is authored data, per spec §5's own framing: pairing is a hand-authored relationship, not
    something inferred by parsing a payoff atom's predicate tree — genuinely deeper introspection
    (walking a compiled `ICompiledPredicate` for a `hasStatus`-shaped leaf) would have been a much
    larger, unrequested mechanism for the same acceptance line. `EnablerPayoffPairings` loads and
    rejects a payoff authored with **zero** enablers at PARSE time ("a payoff with no possible enabler
    is the exact unreal combination §5 forbids pricing a discount for") — the pairing table itself
    can never ship something structurally uncoverable. `EnablerPayoffCoverage.Check` takes one
    generated pool's atom families directly (not a whole-catalog scan, matching §5's own "in the same
    pool" wording) and checks each payoff independently, naming the first uncovered one. A family
    absent from the pairings table is untracked and never flagged — membership as a key IS what makes
    something a "payoff" (mirrors T30's own `StructureAxes`/`ActionShareTable` reject-only-what's-
    declared posture). 8 new tests (`ActionSeedingEnablerPayoffTests.cs`, named `ActionSeeding*` so
    T31's and T32's shared `--filter ~ActionSeeding` finds both): paired pool passes, a planted
    unpaired pool fails naming the payoff, either of two authored enablers suffices, an untracked
    family is never flagged, two payoffs in one pool are checked independently, a zero-enabler payoff
    rejects at parse time, and the shipped `pairings.json` loads with every payoff covered.
    `ActionsPurityGuardTests`: 11/11 green. `audit-overflow.py`/`audit-magic-numbers.py --summary`: 0
    new findings. Full `Core.Tests`: **4189/4189 passed** (was 4181, +8, 0 regressions).

### ✅ Checkpoint 9
- [x] Byte-identical generation for a seed · planted unpaired pool fails · unauthored share rejects
  - **Closed 2026-08-28.** All three proven directly, not argued: byte-identical generation —
    `TheSameSeedProducesByteIdenticalGenerationsTwice` (T31); planted unpaired pool fails —
    `APlantedUnpairedPoolFailsNamingThePayoff` (T32); unauthored share rejects —
    `AnUnauthoredChannelRejectsRatherThanDefaulting` (T31). Phase 9 closes with T30/T31/T32 all done;
    the full-suite counts across the phase (`Core.Tests` 4137 → 4189, +52 net across T30–T32, 0
    regressions at any step) are the evidence trail, not a claim.

---

## Phase 10 — selection (`A7`)

- [x] **T33: the `IBattleView` seam** *(before the AI)* · **S**
  - Acceptance: an **architecture test fails if the intent source touches battle state directly**. Written
    first, because the seam erodes on the first convenient shortcut and fog then stops being a swap.
  - Verify: `--filter ~ActionSelection`
  - **Done 2026-08-28.** `IBattleView` exposes exactly the board/roster facts fog would eventually
    restrict — `LiveActorKeys`, `SideOf`, `PositionOf` (`GridPos?`, the SAME "null = no board" sentinel
    `UsabilityEvaluator` already uses), `FactsOf` (`EntityFacts`, gate 5's window), `HeldActionsOf`
    (already-compiled `CompiledAction`s, matching T30's "nothing parses JSON during battle" all the
    way through the AI's own reads). Deliberately does NOT bundle cost/cooldown/stance — those are
    the existing separate seams `UsabilityEvaluator.Evaluate` already takes as their own parameters,
    and fog never touches them. Architecture test reads `StubIntentSource.cs`'s own source text and
    fails on `BattleEngine`/`ActorState`/`StatusRuntime` — caught a REAL false positive on its first
    run (my own doc comment named `BattleEngine.SelectTarget` in prose), fixed by rewording the
    comment rather than weakening the check, the same "a banned word inside a comment reads as code"
    tradeoff `KernelPurityScan` already documents as a known, accepted limitation of a grep-shaped
    guard.

- [x] **T34: the stub AI** · **L**
  - Pursue nearest, act to kill, move if out of reach, **pass** if nothing works. Preference key is the
    stub's own — **not `priority_band`**, a scheduling concept.
  - Acceptance: with every actor unable to declare, the battle **TERMINATES rather than hanging** — the
    sharpest test here, since a hang is a stopped clock. Ties identical across two runs **and across a
    shuffled actors list**. Zero allocation per decision at 200 actors. **`FactReader.Reads` scales with
    targets, not actions × targets** — a correct-but-unhoisted implementation passes every behavioural test
    and fails this one. Gate 0 is **hoisted out of both loops**.
  - Verify: `--filter ~ActionSelection` + goldens
  - **Done 2026-08-28.** Recon first: `IIntentSource`/`ActionIntent`/`SeatOutcome`/`SeatResult` were
    already declared (`IntentSource.cs`) but **had zero production callers anywhere** — no kernel loop
    calls `TryDeclare`, drives `SeatOutcome`, or reaches `Passed` — the same "declared-but-unwired
    seam" shape as `turn.speed` (T29) and several earlier gaps. This is a genuine, external, honestly-
    documented boundary: T34 builds and proves `StubIntentSource`'s OWN contract exhaustively; wiring
    a real kernel loop around `IIntentSource` is a different module's work, not invented as a scope
    reduction here.
  - **Interpretation decided, not left ambiguous**: spec §2 lists "who? → nearest" and "with what? →
    first usable action **against that target**" as two separate, sequential steps, never "retry the
    next-nearest target." `StubIntentSource` examines **exactly one target per decision** — this is
    what makes "deliberately stupid" (§1) concrete, and it is what makes the Reads-scaling acceptance
    line **trivially and strongly true** rather than merely plausible:
    `FactReaderReadsIsIndependentOfHowManyOtherEnemiesExistOnTheBoard` grows the board from 1 enemy to
    51 and the SAME actor's decision touches gate 5 exactly once either way — Reads is bounded by the
    actor's own held-action count, fully independent of total enemy population, not just "better than
    actions×targets."
  - **A real allocation trap found and fixed while building, not by luck**: iterating an interface-
    typed `IReadOnlyList<T>` via `foreach` boxes its enumerator when the concrete type isn't visible
    at the call site — every loop in `StubIntentSource` (`HeldActionsOf`, `LiveActorKeys`) uses
    indexed `for` instead, which is what actually makes
    `TryDeclareAllocatesZeroBytesAcrossTwoHundredActors` (200 actors, a full round, warmed then
    measured) pass at exactly 0 bytes.
  - **Ties**: ordinal ptr, case-INsensitive — matching `TargetResolver`'s own convention specifically
    (recon found `TargetResolver` and `ActionSlots.SortContenders` actually disagree — one
    case-insensitive, one case-sensitive `CompareOrdinal` for an unrelated unstable-sort reason — this
    module follows the one spec §3 names as the targeting precedent). Proved identical across two
    runs AND across a shuffled insertion order
    (`TiesAreIdenticalAcrossTwoRunsAndAcrossAShuffledActorsList`) — the shuffle is what would catch a
    hidden dependence on dictionary/list insertion order. No-board falls back to `SourceOrder` (first
    live enemy in LISTED order, matching the shipped engine's own no-board default) rather than the
    ordinal tiebreak — proven as its own case
    (`WithNoBoardTheFirstLiveEnemyInListOrderIsChosenSourceOrder`), keeping this module golden-neutral
    (confirmed directly: all 8 `BasicAttackAdoptionTests` fixtures still pass, unmoved).
  - **Preference key**: `ActionTagPreference` — a full, decided-now ranking over all eight
    `ActionTag`s (offensive first per the spec's own example, utility last), the RANK of an action
    being its BEST tag's rank, then `action_id` ordinal — never `priority_band` (a scheduling field on
    a different concept entirely) and never catalog/dictionary order. `CompiledAction` gained a
    `Tags` field (T30 addendum — it carried every other `ActionRow` field except this one) so the AI
    can read tags off the SAME compiled form the battle path already uses; `ActionCompiler`'s one
    construction site updated, zero other production callers existed to break.
    `UsabilityEvaluator.Evaluate` gained a second, fully-additive overload over four scalars
    (actionId/envelope/minRange/maxRange) instead of a full `ActionRow`, because `StubIntentSource`
    reads `CompiledAction` — the original `ActionRow` overload is now a one-line forward to it, and
    all 4 existing call sites (2 test files) are untouched.
  - **`HeldActionsOf`'s contract is documented, not enforced by code**: expected to already be
    preference-sorted (sorted once wherever an actor's action set freezes — T24's `FrozenActionSet` —
    never per decision, since sorting per call would itself be the allocation this module forbids).
    An honest, named gap: no real `IBattleView` implementation exists yet to actually do that sort:
    caught by the module's OWN test suite on first write (a test built with actions in "natural" order
    picked the wrong one), fixed by correcting the TEST to match the documented contract rather than
    weakening the contract to paper over the mismatch.
  - **Termination, proved without the missing kernel loop**: since no kernel machinery drives
    `IIntentSource` yet, `TryDeclareNeverHangsAcrossManySimulatedRoundsWhenNobodyCanEverDeclare` proves
    the piece that IS this module's to prove — 100 actors, a permanently-unusable action each (real
    board, genuinely out of range, no movement action), 1000 simulated rounds, every single call
    returns `None` promptly. A real kernel loop turning that `None` into `SeatOutcome.Passed` without
    hanging is the OTHER module's fix, not fabricated here as if it already existed.
  - `ActionsPurityGuardTests`: 11/11 green. `audit-overflow.py`/`audit-magic-numbers.py --summary`:
    0 new findings. 14 new tests (`ActionSelectionTests.cs`). Goldens: all 8
    `BasicAttackAdoptionTests` fixtures unmoved. Full `Core.Tests`: **4203/4203 passed** (was 4189
    before Phase 10, +14, 0 regressions). Full `Data.Tests`: **525/525**, unaffected (safety check,
    no Data-layer change this phase).

### ✅ Checkpoint 10
- [x] Battle terminates when nobody can declare · reads scale with targets, not the product
  - **Closed 2026-08-28.** Both proven directly:
    `TryDeclareNeverHangsAcrossManySimulatedRoundsWhenNobodyCanEverDeclare` (termination, within the
    honest limits of what has no kernel wiring yet to run against) and
    `FactReaderReadsIsIndependentOfHowManyOtherEnemiesExistOnTheBoard` (Reads, proven as fully
    independent of enemy count — stronger than merely "not the full product"). **This closes Phase 10
    and the action program's entire scheduled build.** Updated 2026-08-28: T29/P0.5, T22's "report
    carries the auto set" (Checkpoint 5), and P0.2/P0.3/P0.4 have since all landed for real under
    explicit owner authorization — see each item's own evidence. Every remaining line in
    `action-todo.md` now either is checked or lives in the explicit "Deferred — specced, not scheduled"
    section (`A9`, `A10`, `A8`'s reaction lane, seedsmith — each blocked by its own named, real reason,
    not an invented stopping point).

---

## Phase 11 — reopened 2026-08-28: A17–A20, delivering on Checkpoint A/C for real

**Why:** a completeness audit (owner-requested, ahead of Phaser frontend work) found `BattleEngine`
imports zero action-program types except one inert proof declaration — Checkpoint 10's own "entire
scheduled build" closed without ever wiring the action program into a real battle. Full context,
scope decisions (full switch-over; full multi-action loadouts; grant-writer/Server/FE explicitly
deferred), and the golden-ordering rule: [action-map.md](../docs/architecture/action-map.md) §12.
Module spec: [spec-action-selection-adoption.md](../docs/architecture/action/spec-action-selection-adoption.md) (A17; A18–A20 get their own specs when their turn comes, per spec-driven-development's "recurse per module").

- [x] **T35: `IBattleView` adapter over `BattleRunState`** · **M**
  - `LiveActorKeys`/`SideOf`/`PositionOf`(always null)/`FactsOf`/`HeldActionsOf`, per
    spec-action-selection-adoption.md §1.
  - Acceptance: an architecture test (T33's own pattern) still passes unchanged — this adapter is a
    NEW implementation of an EXISTING interface, not a change to the interface or to `StubIntentSource`.
  - Verify: `--filter ~ActionSelectionAdoption`
  - **Evidence (2026-08-28):** `BattleRunState : IBattleView` implemented in
    `src/FusionRpg.Core/Battle/BattleRunState.cs`. `ActionArchitectureTests`-style seam checks
    unchanged (part of the 4352-passing Core run below). No change to `IBattleView` or
    `StubIntentSource` themselves.

- [x] **T36: loadout compilation, empty-loadout fallback, preference sort** · **M**
  - `BattleRunState` construction resolves `EquippedActionIds` → `CompiledAction` list via
    `ActionCatalog` (A6's first production caller); empty list → single basic-attack `CompiledAction`;
    sorted once by `ActionTagPreference`, per spec §2.
  - Acceptance: an actor with no loadout holds exactly one action (basic attack); a two-action loadout
    is sorted offensive-first, matching `ActionTagPreference`'s existing ranking.
  - Verify: `--filter ~ActionSelectionAdoption`
  - **Evidence (2026-08-28):** new `tests/FusionRpg.Core.Tests/Battle/Adoption/ActionSelectionAdoptionTests.cs`
    (5 tests, all passing): no-loadout fallback needs no catalog; a nonempty loadout with no catalog
    throws `ArgumentException` naming the actor key and "ActionCatalog"; an unknown equipped id against
    a real catalog throws naming the bad id; a known multi-action loadout resolves and rides
    `EquippedActionIds` through to the report; two resolves against the same catalog instance are
    deterministic. `EquippedActionIdsReportingTests.cs` updated (2 tests) to supply synthetic
    `ActionCatalog`s now that a nonempty loadout is validated loudly — its
    "fight identically" test still holds today (nothing reads `HeldActionsOf` for real behavior until
    T37).
    Full verification: Core 4352/4352, Data 532/532, Guard 116/116, CheatCore 40/40, Launcher 162/162,
    E2E 194/194 — all green, zero test edits to golden constants, all 8 goldens unchanged (confirms
    T35/T36 are still behaviorally inert, as designed). 4 boundary guards green
    (single-writer, secondary-no-unity, funnel-delta, dal).

- [x] **T37: the swap — `RunBasicAttackStep` calls `StubIntentSource`, not `SelectTarget`** · **L**
  - `bloodthirsty` pre-filter and `loyal` bodyguard post-check stay `BattleEngine`-side, wrapping
    whatever `ActionIntent.TargetKey` the intent source proposes (spec §5 — NOT reimplemented inside
    `StubIntentSource`). `ActionIntent.None` maps to the existing `AttackStepOutcome.Break` (spec §4).
  - Acceptance: `bloodthirsty`/`loyal` fixtures unchanged; a two-loadout comparison produces
    measurably different `ActionIntent`s under the same seed (the actual capability proof).
  - Verify: `--filter ~ActionSelectionAdoption` + full Core
  - **Evidence (2026-08-28):** `SelectTarget` deleted from `BattleEngine.cs`; `RunBasicAttackStep`
    (`Actions/BasicAttack.cs`) now calls `StubIntentSource.TryDeclare` through a fresh `IBattleView`
    per attacker (`BattleRunState` itself for the common case; a new `BloodthirstyView` decorator —
    reorders `LiveActorKeys` so the lowest-HP live enemy sorts first, letting `NearestEnemy`'s own
    no-board list-order fallback land on it without teaching the trait to `StubIntentSource` — for
    `bloodthirsty` attackers only). The `loyal` bodyguard check runs exactly as before, against
    `intent.TargetKey` before `calculator.Compute`. `TraitBattleTests.Bloodthirsty_hunts_the_lowest_hp_opponent`
    and `.Loyal_...` (pre-existing, unmodified) both still pass, proving both traits unchanged rather
    than assuming it. New `A_condition_gated_loadout_breaks_the_attack_while_an_ungated_one_still_lands`
    (`ActionSelectionAdoptionTests.cs`) is the capability proof: a `CompiledAction` gated on
    `HpAboveMilli(Target) > 1000` (never satisfiable — `HpMilli` is clamped to `[0,1000]`) is
    `ActionIntent.None` every time → `Break`, so that loadout deals **zero** damage all battle, while
    an otherwise-identical actor with the ungated basic-attack fallback deals damage normally — same
    seed, same opponent, difference attributable to the one `CompiledAction` field that differs.
    **Unplanned finding, verified not assumed:** for every actor/content that exists TODAY, the swap
    is byte-identical, not merely golden-neutral — every `UsabilityEvaluator` gate (stance/cooldown/
    afford/range/condition) trivially passes for the basic attack's all-`Always`/all-zero envelope,
    and `NearestEnemy`'s no-board fallback returns the exact same first-in-list-order enemy
    `SelectTarget`'s old else-branch did. Confirmed empirically: full Core suite **4353/4353**, all 4
    battle + 4 expedition golden hashes unchanged, zero test file edited outside this module's own new
    test. See T39 below for what this means for the re-bless.

- [x] **T38: `CooldownLedger` wiring, inert for the all-zero envelope** · **S**
  - One real `CooldownLedger` per battle on `BattleRunState`; `_cooldowns.Start(...)` called after
    every resolve, per spec §6 — a documented no-op for `Class.None`, so A19 does not need to revisit
    this wiring point.
  - Acceptance: `CooldownLedger.IsReady` still true immediately after a basic-attack resolve, proven
    directly, not assumed from the envelope's `Class`.
  - Verify: `--filter ~ActionSelectionAdoption`
  - **Evidence (2026-08-28):** landed as part of T37's swap — `state.Cooldowns.Start(attacker.Setup.Key,
    intent.Envelope, nowTick)` called after every landed hit (miss skips it, matching spec §3's own
    step ordering: `calculator.Compute → miss? continue → [cooldown] Start(...)`). Proven a genuine
    no-op directly from source, not assumed: `BasicAttackEnvelope = ActionEnvelope.NoOp with {...}`
    leaves `CooldownTicks` at its record default `0`, and `CooldownLedger.Start`'s own first line is
    `if (envelope.CooldownTicks <= 0) return;` — an unconditional guard, not new code this task wrote.
    That the wiring is genuinely live (not dead code) is what the unchanged multi-round goldens prove:
    `Start` fires every hit across every Stomp/Close/Wipe round, and zero hash moved.

- [x] **T39: re-bless + predicted delta + sweep** · **M**
  - This module is a declared **mover** (`action-map.md` §12.2) — full suite run, goldens diffed,
    every moved hash attributed to a specific stream/round via the parity ladder (matching
    spec-kernel-adoption.md's own ladder discipline, reused here rather than reinvented), predicted
    delta written up, `RulesetVersion` bumped once, win-rate sweep produced. **⛔ owner sign-off on
    the sweep**, same standing rule this repo already applies everywhere else a version bumps.
  - Acceptance: every re-blessed hash has a named cause; no test file edited outside the golden
    constants themselves; six suites green; four guards green.
  - Verify: full suites + guards
  - **Finding (2026-08-28), not yet closed — mirrors B18's own shape exactly
    (`battle-timeline-todo.md` line 480):** there is nothing to re-bless. Full verification —
    Core **4353/4353**, Data 532/532, Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194,
    all 4 boundary guards green — shows **zero** golden hashes moved (all 4 battle + 4 expedition
    constants unchanged from pre-T37). This is not "no content happens to trigger it today" (B18's
    shape) but a step stronger: **structurally identical for any input under today's constraints** —
    proven in T37's own evidence above (every usability gate trivially passes; no-board `NearestEnemy`
    is provably the same first-in-list-order pick `SelectTarget` made). `RulesetVersion` is **4**,
    unchanged.
  - **Decision — owner chose: hold the bump (2026-08-28).** `RulesetVersion` stays **4**. Trigger
    condition recorded in `docs/architecture/decisions.md`'s new "Action selection (battle adoption)"
    row so it is not rediscovered as a surprise: a real divergent multi-action loadout reaching a live
    battle, or the battle-board module landing, makes this a live, sweep-measurable change — bump then,
    with a predicted-delta writeup against whichever golden(s) move.
  - Acceptance: every re-blessed hash has a named cause (n/a — nothing moved); no test file edited
    outside this module's own new test files; six suites green; four guards green. **Satisfied.**
  - Verify: full suites + guards — Core 4353/4353, Data 532/532, Guard 116/116, CheatCore 40/40,
    Launcher 162/162, E2E 194/194; guard-single-writer/secondary-no-unity/funnel-delta/dal all green.

### ⛔ Checkpoint E — selection is real — **CLOSED 2026-08-28 (no version change)**
- [x] `SelectTarget` is gone as the live targeting path; `StubIntentSource` decides for every actor,
  every turn; `bloodthirsty`/`loyal` proven unchanged (T37 evidence).
- [x] One combined re-bless — closes on the finding that there is nothing to re-bless (zero goldens
  moved, verified not assumed), put to the owner, who chose to hold the bump — same shape as
  `battle-timeline-todo.md`'s Checkpoint B2.

---

## Phase 12 — A18a–e (2026-08-28): Checkpoint F, actions resolve for real

A18 ("resolve whichever action A17 chose") split into five modules once specced — see
`action-plan.md` §4b and `action-map.md` §12.1 for why and the module table. All five specs written
and adversarially audited against real code before any task below was started; two load-bearing bugs
were found and fixed in the specs themselves (constructor-injection that cannot compile given
`BattleRunState`'s real construction order; a reference to a `PhasedComposeStrategy.Instance` that
does not exist). Plan: `C:\Users\NeneScarlet\.claude\plans\flickering-strolling-boot.md` (approved
2026-08-28) — copied here per this repo's own `/plan` rule (CLAUDE.md: plan output lives in `tasks/`,
never left only in the global scratch file).

### A18a — the binding seam

- [x] **T40: `IContainerEffectResolver` + `DictionaryContainerEffectResolver`** · **S**
  - New `src/FusionRpg.Core/Actions/IContainerEffectResolver.cs` (spec-action-container-binding.md §1).
  - Acceptance: compiles standalone; `EffectIdsFor` returns mapped ids for a known containerId, empty
    span for an unknown one.
  - Verify: `--filter ~ContainerEffectResolver`
  - **Evidence (2026-08-28):** interface + `DictionaryContainerEffectResolver` built. 3 new tests in
    `ContainerEffectResolverTests.cs`, all passing (known id, unknown id → empty not null, null map
    rejected at construction).

- [x] **T41: wire binding into `BattleRunState`'s loadout-compile loop + `Resolve`'s 8th param** · **M**
  - Extends the existing T36 loop; `containerResolver` as an 8th optional trailing param on
    `BattleEngine.Resolve` (spec §2).
  - Acceptance: no-container case byte-identical; a real container binds under `entity:{key}` with a
    deterministic `GrantId`; an unresolvable OR pooled-shaped container throws `ArgumentException`
    naming the actor key and container id (one unified rejection path, not two).
  - Verify: `--filter ~ActionContainerBinding` + full Core
  - **Evidence (2026-08-28):** `BattleRunState.BindContainers` extends T36's loop; `EffectGrantDto`s
    granted into `Host.Bag` with deterministic `battle:{actorKey}:{actionId}:{effectId}` ids. 6 new
    tests in `ActionContainerBindingTests.cs`, all passing — including a real, shipped
    `EffectAtomCatalog` def (`fx.board_cherry`, `Triggered`/`OnDamageDealt`, not `Passive`/`OnGranted`
    — confirmed not to self-fire on grant) bound and found under `Bag.ForOwner("entity", "entity:squad:0")`,
    proven through the existing T14 `onEffectHostReady` seam (captures the live `Host` reference; since
    `Resolve` doesn't return until construction finishes, the captured reference reflects every grant
    added later in the same constructor call — no new test seam needed).

### ⛔ Checkpoint A18a — **CLOSED 2026-08-28**
- [x] Full 6-suite + 4-guard run, zero goldens moved. Core **4362/4362** (4353 + 9 new), Data 532/532,
  Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all green. All 4 boundary guards
  green (single-writer, secondary-no-unity, funnel-delta, dal).

### A18b — the trigger

- [x] **T42: `OnActivate` in the closed vocabulary (7→8)** · **S**
  - `AtomKind.cs`: the constant, the new `Actions` grouping, `All`. `AtomKindRegistry.cs`:
    `TriggerCount` 7→8, `AllTriggers` local array +1 (spec-on-activate-trigger.md §1).
  - Acceptance: the existing 7-count vocabulary test found and updated; kind-eligibility proven both
    ways — `resource.delta`/`status.apply`/`shield.grant` allow it, `stat.modify` and every Board kind
    do not.
  - Verify: `--filter ~AtomKindRegistry`
  - **Evidence (2026-08-28):** two stale test names fixed (`_closed_at_seven` → `_closed_at_eight`,
    `_drawn_from_the_seven` → `_drawn_from_the_eight`) — both passed mechanically throughout since
    their assertions were self-consistency checks, not hardcoded literals, but the NAMES lied about
    what they verified (Design Gate evidence rule 6). New
    `OnActivate_reaches_exactly_resource_delta_status_apply_and_shield_grant` proves both directions
    against all 12 kinds via `AtomKindRegistry.ValidateTrigger`, not just the 3 positive cases. 26/26
    `AtomKindRegistryTests` pass.

- [x] **T43: the firing site in `RunBasicAttackStep`** · **M**
  - After the `loyal` redirect, before `calculator.Compute` (spec §2).
  - Acceptance: fires exactly once per resolved (non-`Break`) intent, independent of hit/miss, at the
    post-redirect target; zero grants bound → zero RNG draws, proven directly.
  - Verify: `--filter ~OnActivateTrigger` + full Core
  - **Evidence (2026-08-28):** `Bag.OnEvent(OnActivate)` + `Host.Flush()` added right after the loyal
    redirect. **Real finding, from the probe/falsify step, not assumed:** a synthetic self-damage
    probe (`OnActivateTriggerTests`, bypassing A18a's binding loop — proves firing in isolation) first
    predicted `1×Rounds` self-damage and measured `2×Rounds` — a grant fires on `OnActivate` both when
    its own owner acts AND when its owner is merely the *target* of someone else's activation, a
    direct consequence of `EffectOwnerKey.MatchesEvent`'s existing ActorPtr-OR-TargetPtr dual-check
    (the same mechanism `OnDamageDealt`/`OnDamageTaken` content already relies on). Not a bug — named
    as a real content-authoring hazard in `spec-on-activate-trigger.md` §3 (a "self-buff on activate"
    grant will also fire when its owner is merely attacked, absent an explicit filter this system
    doesn't have yet) rather than silently worked around. Test corrected to assert `2×`, not the
    mechanism "fixed" to produce `1×`. 2/2 new tests pass differentially (same-seed with/without probe,
    isolates the probe's own effect from ordinary combat variance).

### ⛔ Checkpoint A18b — **CLOSED 2026-08-28**
- [x] Full 6-suite + 4-guard run, zero goldens moved. Core **4365/4365** (4363 + 2 new), Data 532/532,
  Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all green. All 4 boundary guards
  green.

### A18c — resource.delta + shield.grant

- [x] **T44: `Bag.Status`/`Bag.StatusRng` wiring** · **S**
  - Two lines in `BattleRunState`'s constructor, next to `Host.Bag.ShieldGate =` (T14).
  - Acceptance: no behavior change by itself — proven via full suite.
  - Verify: full Core suite
  - **Evidence (2026-08-28):** `Host.Bag.Status = Status; Host.Bag.StatusRng = StatusRng;` added.
    Wiring-only — Core suite unchanged at 4365/4365 immediately after, zero regressions.

- [x] **T45: `OnDamageDealt` firing site** · **S/M**
  - After `breakdown.Hit` confirms true, before the existing T38 cooldown-start line
    (spec-battle-resource-shield-grants.md §2).
  - Acceptance: fires once per landed hit, never on a miss; composes correctly with A18b's own
    `OnActivate` call earlier in the same method.
  - Verify: `--filter ~BattleResourceShieldGrants`
  - **Evidence (2026-08-28):** `Bag.OnEvent(OnDamageDealt)` + `Host.Flush()` added right after the
    hit-confirmed check. **Real finding, from the probe step:** unlike `OnActivate` (unconditional),
    `OnDamageDealt` only fires on a landed hit, so an exact "2×Rounds" prediction (matching
    `OnActivate`'s own dual-owner-match) over-counts by however many rounds miss — the T46 test
    below asserts a directional claim instead, not an exact count.

- [x] **T46: proof — plain amount, DoT/contagion piggyback, shield.grant** · **M**
  - Against real `EffectAtomCatalog.CreateAll()`-shipped defs (spec §3), including the
    `GrantChance`-rolls-against-the-real-stream proof.
  - Verify: `--filter ~BattleResourceShieldGrants` + full 6-suite + 4-guard
  - **Evidence (2026-08-28):** 4 tests in `BattleResourceShieldGrantsTests.cs`, all passing.
    **Three real findings from the probe/falsify cycle, all propagated into
    `spec-battle-resource-shield-grants.md` §3, not silently patched around:**
    (1) plain `resource.delta` never reaches `BattleEffectSink.Execute` directly from `FireGrant` —
    it routes through `CombatDamageDispatcher.DispatchInstant` + `Funnel`, which reaches the sink
    only via `Funnel.Flush()`'s own later call; the ORIGINAL spec claim's outcome was right, its
    named mechanism was wrong, now corrected.
    (2) The DoT/contagion payload (`statusId`/`periodMs`/`durationMs`) must live on the **grant's own
    `Overlay`**, not the def's `Actions[0].Params` — `StatusEffectBridge.TryApplyFromGrant` reads
    `grant.Overlay` directly (`EffectBag.cs:439-441`), bypassing the merged dictionary `FireGrant`
    builds for the instant packet. First attempt (payload on Params) silently applied nothing.
    (3) `GrantShield`'s own overlay allowlist has no flat `targetPtr` key (only nested `target:
    {mode, ptr}`) — confirmed by a real `"unknown overlay key 'targetPtr'"` exception on first
    attempt. Also confirmed the same owner-matching dual-fire A18b found applies here too (a grant
    fires on its owner's own hit AND on the other side's hit against them) — the resource-delta
    damage test's first "2×Rounds" prediction overshot for the same missed-hit reason T45 already
    names; fixed to a directional `waveWith < waveWithout` assertion. The `GrantChance` probe needed
    empirical tuning too: `chance=0.5` saturated to always-true across 20 seeds (
    `ResistanceEvaluator.cs:228` multiplies `GrantChance` by a power-based `pApply` term neither this
    module nor its test owns), `chance=0.01` saturated to always-false; `chance=0.1` produced the
    genuinely mixed true/false outcomes proving `state.StatusRng` is the real, wired stream.

### ⛔ Checkpoint A18c — **CLOSED 2026-08-28**
- [x] Full 6-suite + 4-guard run, zero goldens moved. Core **4369/4369** (4365 + 4 new), Data 532/532,
  Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all green. All 4 boundary guards
  green. Propagated: `AtomKindRegistry.cs`'s `resource.delta`/`shield.grant` Battle cells `None → Full`
  (Sim's own `shield.grant` gap left untouched — out of this module's scope), comments updated; the
  pre-existing `Battle_support_is_narrow_and_honest` test's own hardcoded `None` expectations updated
  to `Full` for both, matching the exact precedent that test itself already set for `stat.derived`'s
  own 2026-08-23 re-opening ("the cell moved because the code did").

### A18d — status.apply

- [x] **T47: `BattleEffectSink` gains `Status`/`StatusRng` properties + `Clock` ctor param** · **M**
  - Settable properties on `BattleEffectSink`, forwarding properties on `BattleEffectHost` (public ctor
    **unchanged**), `Clock`/`_sink` construction order swapped inside `BattleEffectHost`'s own ctor
    body (spec-battle-status-apply.md §1 — the audit-corrected design).
  - Acceptance: both existing `new BattleEffectHost(...)` call sites (`BattleRunState.cs:115`,
    `BattleEffectHostTests.cs:19`) compile unchanged.
  - Verify: build + full Core suite
  - **Evidence (2026-08-28):** built exactly as the audit-corrected spec designed — `Clock` built
    before `_sink` inside `BattleEffectHost`'s own ctor, `BattleEffectSink` gains `Status`/`StatusRng`
    settable properties plus a `FakeEffectClock` ctor param, `BattleEffectHost` forwards both via
    settable properties. `BattleRunState`'s own `Host.Status = Status; Host.StatusRng = StatusRng;`
    added alongside its existing `Host.Bag.Status =`/`Host.Bag.StatusRng =` (A18c) lines. Both
    existing `BattleEffectHost` call sites compiled unchanged, confirmed by a clean full build.

- [x] **T48: `ApplyStatus` branch in `BattleEffectSink.Execute`** · **S/M**
  - `duration` seconds→ms; `level` accepted/threaded/inert (named); null-checked, refuses quietly.
  - Verify: `--filter ~BattleStatusApply`
  - **Evidence (2026-08-28):** `ExecApplyStatus` added, dispatched before the existing FA10 branch.
    **Real bug, found and fixed by T49's own probe, not shipped:** `StatusApplyInput.BaseDuration`
    needs the SAME unit as `DurationMs` (ms) — `StatusRuntime.Apply` uses `eval.EffectiveDuration`
    (derived FROM `BaseDuration`) whenever `BaseDuration > 0`, so a first attempt passing the raw
    seconds value produced a 5ms status for an authored 5-**second** duration. Fixed by converting to
    ms once and passing the same value to both fields, matching the existing scripted-`InitialStatuses`
    call's own established convention. Propagated to `spec-battle-status-apply.md` §1.

- [x] **T49: proof — real timed apply, resistance/immunity, level-inert, clock-correctness** · **M**
  - Includes the round-5-fire-must-not-use-`T0` regression test the audit named.
  - Verify: `--filter ~BattleStatusApply` + full 6-suite + 4-guard
  - **Evidence (2026-08-28):** 3 tests in `BattleStatusApplyTests.cs`, all passing, against the one
    real shipped `status.apply` def (`fx.poison_on_hit`) — caught the `BaseDuration`-unit bug above on
    first run (`Actual: 5` instead of `5000`, i.e. 5ms not 5000ms), fixed, re-verified. Clock-
    correctness proven directly (`LastApplied`/`ExpiresAt` track the live clock, not a fixed `T0`); a
    bare, never-wired host refuses quietly (`Record.Exception` is null) rather than throwing.
    Resistance/immunity proven by construction, not a separate fixture: `ExecApplyStatus` calls the
    exact same `StatusRuntime.Apply` → `ResistanceEvaluator.Evaluate` path scripted statuses already
    go through, no shortcut added.

### ⛔ Checkpoint A18d — **CLOSED 2026-08-28**
- [x] Full 6-suite + 4-guard run, zero goldens moved. Core **4372/4372** (4369 + 3 new), Data 532/532,
  Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all green. All 4 boundary guards
  green. Propagated: `status.apply`'s Battle cell `Partial → Full` (`AtomKindRegistry.cs`, comment
  updated); the pre-existing `Battle_support_is_narrow_and_honest` test's two hardcoded `Partial`
  assertions updated to `Full`. `BattleEffectSink.Execute`'s "FA10 only" comment updated to "FA10/FA2
  only" — its final update (naming all three actions) deferred to A18e, per the plan's own build order.

### A18e — live stat modifiers (after A18c AND A18d)

- [x] **T50: `BattleStatModifierLedger`** · **S/M**
  - `Add`/`RemoveBySource`/`For`/`Recompose` — owns one `PhasedComposeStrategy` instance internally
    (spec-battle-live-stat-modifiers.md §1, the audit-corrected design).
  - Acceptance: Flat/Increased/More compose exactly as `PhasedComposeStrategy.ComposeChannel`'s own
    contract; `RemoveBySource` reverts exactly its own contribution.
  - Verify: `--filter ~BattleStatModifierLedger`
  - **Evidence (2026-08-28):** built exactly per spec, plus one correction: `StatModifier` is a
    `sealed class` with `init`-only properties, not a positional-constructor record as the spec's own
    snippet showed — built via object initializer instead. 4/4 new tests pass, including a
    same-source-different-actor isolation check the spec's own testing strategy didn't name.

- [x] **T51: `LiveAtk` + Defense targeted recompose + the one read-site change** · **S/M**
  - `ActorState.LiveAtk(ledger)`; `Derived.Set(CombatDefenseOmni, ...)` on fire; `Setup.Atk` →
    `LiveAtk(state.Ledger)` at the one call site (spec §2).
  - Acceptance: byte-identical to `Setup.Atk` with an empty ledger.
  - Verify: full Core suite
  - **Evidence (2026-08-28):** `LiveAtk` added to `ActorState`; the one production read-site in
    `RunBasicAttackStep` changed. Full Core suite unchanged at 4376/4376 immediately after (T50's 4
    tests were the only count change from the prior checkpoint) — zero regressions, zero goldens
    moved, confirming byte-identity with an empty ledger empirically, not just by construction.

- [x] **T52: `BattleEffectSink` gains `Ledger` + an `ActorState` resolver, the `ModifyStat` branch** · **M**
  - Same forwarding-property shape T47 established. Owner resolution via `ctx.Grant.OwnerKey` (the
    bound `EffectGrant`, `entity:` prefix stripped) (spec §3).
  - Acceptance: `Override` refused at bind time; owner resolved correctly.
  - Verify: `--filter ~BattleLiveStatModifiers`
  - **Evidence (2026-08-28):** **Three real findings, all propagated into
    `spec-battle-live-stat-modifiers.md`, not silently patched around:**
    (1) `ActorState` is private to `BattleEngine`, unreachable from `BattleEffects.cs`'s top-level
    classes — a new `IBattleStatTarget` interface (`Derived`, `BaselineDefense`), matching
    `IBattleHpTarget`'s own established pattern, fixes this without widening `ActorState`'s own
    visibility.
    (2) The real `ModifyStat` param shape (confirmed against the one shipped def, `fx.passive_atk_flat`,
    and `EffectOverlayMerge`'s own allowlist) is THREE separate, independently-optional keys
    (`flat`/`increased`/`more`), never a combined `op`+`amount` pair — the atom SCHEMA's own authoring-
    time names (`AtomKindRegistry.cs`); `AtomCompiler` translates between the two. A first draft
    assumed the schema shape and silently no-oped on every real grant; rewritten against the real def.
    (3) Widening `stat.modify`'s `Triggers` broke the permanent-modifier case entirely:
    `AtomRowValidator.ValidateWhen` infers "trigger REQUIRED" from `Triggers.Count > 0`, which had
    never needed a THIRD shape (triggers allowed, still not required) before this kind. Caught by
    `ChannelExtensionTests.The_three_new_channels_pass_atom_validation` failing with `"stat.modify
    requires a trigger"`. Fixed with a new `AtomKind.TriggerOptional` field (default `false`, every
    other kind's existing inference completely unchanged); `stat.modify` sets it `true`.
    Six pre-existing `AtomKindRegistryTests` needed updating for the deliberate `None → Full` Battle
    flip and the `AllTriggers` widen (matching the exact precedent `stat.derived`'s own 2026-08-23
    re-opening already set in this same file) — all propagated, all passing.

- [x] **T53: `stat.modify`'s trigger widen — `AtomTriggers.None → AllTriggers`** · **XS**
  - One line in `AtomKindRegistry.cs` (spec §3 — this module's own call, not A18b's).
  - Verify: `--filter ~AtomKindRegistry`
  - **Evidence (2026-08-28):** landed together with T52 (same file, same change) — see T52's own
    evidence for the `TriggerOptional` finding this widen required. 26/26 `AtomKindRegistryTests` pass.

- [x] **T54: full proof — persistence across rounds, byte-identity, golden sweep** · **M**
  - Includes multi-round Stomp/Close/Wipe fixtures specifically.
  - Verify: `--filter ~BattleLiveStatModifiers` + full 6-suite + 4-guard
  - **Evidence (2026-08-28):** 5 tests in `BattleLiveStatModifiersTests.cs`, all passing —
    against the one real shipped `stat.modify` def (`fx.passive_atk_flat`, a `Passive`/no-trigger
    permanent modifier that auto-fires on `Grant` itself) plus a synthetic `OnActivate`-triggered
    variant (no shipped content exercises a triggered `stat.modify` at all). Both proven via the
    differential technique (same seed, with/without the probe) against cumulative `DamageDealt`,
    strictly more with the buff than without — not merely "a grant exists." `Override` and the
    permanent/triggered validation split proven directly against `AtomRowValidator.Validate`, reusing
    `ChannelExtensionTests`' own established `AtomRow`-construction helper pattern rather than a
    weaker `AtomKindRegistry.Validate`-only check that wouldn't have caught the T52 regression at all.
    **One non-reproducing flake observed and investigated, not hidden:** a single full-suite run
    showed 1 failure (name not captured — output truncated) that did not reproduce across 8
    subsequent runs (3 full-suite + 5 Battle-namespace-only, all clean) with no stray background
    processes found; treated as a transient environmental blip, not a code defect, and recorded here
    rather than silently rerun-until-green.

### ⛔ Checkpoint A18e — **CLOSED 2026-08-28**
- [x] Full 6-suite + 4-guard run, zero goldens moved. Core **4381/4381** (4376 + 5 new), Data 532/532,
  Guard 116/116, CheatCore 40/40, Launcher 162/162, E2E 194/194 — all green. All 4 boundary guards
  green. Propagated: `stat.modify`'s Battle cell `None → Full` (`AtomKindRegistry.cs`, comment
  updated); `BattleEffectSink.Execute`'s comment names all three actions
  (`ApplyResourceDelta`/`ApplyStatus`/`ModifyStat`) by name, its final update.

- [x] **⛔ Checkpoint F — actions resolve for real (`action-map.md` §12.3) — CLOSED 2026-08-28.**
  All five specs' success criteria hold, each proven against real shipped content where any exists
  (`fx.board_cherry`, `fx.overlay_damage`, `fx.shield_grant`, `fx.poison_on_hit`, `fx.passive_atk_flat`)
  and synthetic content only where no shipped def exercises a path at all (the DoT/contagion piggyback,
  a triggered `stat.modify`). **Golden-neutrality measured, not assumed, at every one of five
  checkpoints** — all 8 battle/expedition golden hashes unchanged from before A18a through after A18e;
  no predicted-delta writeup was needed because nothing moved. `RulesetVersion` stays **4** throughout
  — every A18a-e change proved additive to existing content, the same "predicted a mover, measured
  byte-identical" shape A17 itself already established for this reopening. Full evidence trail:
  T40-T54 above, each with its own build→test→review→fix→verify cycle; five real, load-bearing
  findings surfaced by building (not merely reading) and propagated into their specs rather than
  silently patched around — see T41 (owner-matching dual-fire), T45/T46 (the real resource.delta
  execution route, the DoT-payload-lives-on-Overlay-not-Params rule, the `target` vs `targetPtr`
  overlay key), T48/T49 (`BaseDuration`'s ms unit), T52 (the `IBattleStatTarget` gap, the real
  `flat`/`increased`/`more` param shape, the `TriggerOptional` validation gap).

---

## Deferred — specced, not scheduled

- [ ] **A9 movement-actions** — waits on `A10`. One row, no new runtime.
- [ ] **A10 battle-board** — owner deferral; built with the board map / battle area.
- [x] **A8's reaction lane** — **CLOSED 2026-08-31 by its own evidence, not by new work.** It said
  it waits on timeline **B6**; B6 shipped 2026-08-28, and B6's own entry records the answer:
  *"`A8 defence-actions` (guard) ended up **not** needing this lane at all; it ships as a stance
  with riposte-on-release, not a reaction"* (`battle-timeline-todo.md` B6, citing
  `action-map.md:93`). The dependency was real when written and was dissolved by the design, not
  satisfied by a build. The *stance* half shipped in Phase 7 as the line already said.
- [ ] **seedsmith** — a **development tool**, built **after** this program.

---

## Tuning pass — after the build, on real data

Not a phase. Every number below shipped with a working value and a declared metric; this is where play data
moves them.

| Number | Metric that moves it |
|---|---|
| `p1` · `delta` · `floor` · `cap` | earns per hour; share of players who ever discard |
| cost span (1.38/rung) | **the share of equipped loadouts that mix rungs** |
| `predicateDiscountFloorMilli` | win rate of combo builds vs non-combo |
| `poise` regen | `r = poiseRegen / peerPressure`, emitted per battle |
| type weight vectors | category spread across a type's ten unlocks |
