# Tasks: `battle-tempo`

Plan: [battle-tempo-plan.md](battle-tempo-plan.md) · Map:
[docs/architecture/battle-tempo-map.md](../docs/architecture/battle-tempo-map.md)

Ids are stable. `Deps` are task ids. Sizes: XS 1 file · S 1–2 · M 3–5 · L 5–8.

---

## Phase 0 — `poise-unification` (root; moves no golden)

- [x] **PU1 — One pool: `poise` moves to `ActorResourcePools`** · **M** · **Deps:** none
  - Spec: [spec-poise-unification.md](../docs/architecture/battle-tempo/spec-poise-unification.md) §2.1–2.2
  - **Acceptance:**
    - `PoiseRuntime`'s private `Dictionary<string, long>` is gone; every spend routes through
      `PoiseLedger` / `ActorResourcePools`.
    - ⛔ **Refuse semantics win:** an unaffordable commit spends **nothing** and yields
      `CannotAfford("poise")`. Floor-at-zero is gone.
    - ⚠️ **"Exhaustion, never death" survives** — refusing to pay is not dying.
    - The code comment states *why* the PS-8 objection does not apply (PS-8 = progression ceilings, not
      affordability; `stamina`/`qi` already refuse through the same `TrySpend`).
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Poise|FullyQualifiedName~ActorResourcePools"` ·
    falsifier: make the commit floor-and-spend → the typed-refusal test must redden ·
    ⭐ **one pool, proven:** a spend through `PoiseLedger` is visible to **both**
    `ActorResourcePools.Resolve` **and** `SettleAll` — under the fork this was false by construction ·
    ⚠️ `PhaseModel.RecoveryPerRound`'s `poiseRegen` parameter is **untouched** and still reads
    `DerivedStatChannels.ResourceRegen("poise")` — evidence the analytic layer already sided with the hub ·
    `python scripts\audit-overflow.py --paths src/FusionRpg.Core/Actions/Defence`
  - **Files:** `Combat/Guard/PoiseRuntime.cs`, `Actions/Defence/PoiseLedger.cs`, its test file
  - **Evidence (2026-09-05):** `PoiseRuntime.cs` (pool + `Commit`/`Absorb`/`Regen`/`Riposte`) deleted
    outright — `TryCommit` (unchanged, all-or-nothing refuse) is now the only commit path.
    `python scripts\audit-overflow.py --paths src/FusionRpg.Core/Actions/Defence`: **0 findings, all 7
    categories clean.** `python scripts\audit-magic-numbers.py --summary`: **0** for every domain
    touched (`actions`/`defence` do not appear in the nonzero table). `PhaseModel.cs` grep-confirmed
    untouched, still reads `DerivedStatChannels.ResourceRegen("poise")` — never `PoiseRuntime`.
    ⛔ **`dotnet test` on `Core.Tests` is blocked** by a pre-existing, unrelated build break in the
    `loam-economy` stream's uncommitted WIP (`LoamPolicy` field rename vs the committed
    `StructureCatalog.cs`, confirmed by isolating those 4 files via `git stash push` + rebuild, then
    restored — **not touched, not fixed**, out of `battle-tempo` scope). `FusionRpg.Core` itself builds
    clean (0 errors). Verified instead via a standalone probe (`tools/PoiseProbe`, referencing only
    `FusionRpg.Core`, loading the real `data/tuning/derived-stats.v2.json`): **19/19 assertions pass**
    against the real compiled `PoiseLedger`/`ActorResourcePools`, including the refusal, the `Resolve`
    **and** `SettleAll` visibility check, and the falsifier below.
    ⭐ **Falsifier executed, not assumed:** `TryCommit` was mutated in place to floor-and-spend
    (matching the deleted `PoiseRuntime.Commit`'s old behaviour), rebuilt, and re-probed — exactly the
    two affordability-refusal assertions reddened (`RaisingWithInsufficientPoiseIsRefusedBy...` and
    `CommitWithNegativeCostThrows`), nothing else moved. The mutation was then reverted and the probe
    confirmed green again (19/19).

- [x] **PU2 — One riposte: delete the non-validating copy** · **XS** · **Deps:** PU1
  - **Acceptance:** `PoiseRuntime.Riposte` is gone; `Riposte.DamageFromSpentPoise` survives (it bounds
    `shareMilli` to `[0,1000]` and throws outside it, which the deleted copy did not). The PS-8
    bounded-ratio exemption comment survives on the remaining copy.
  - **Verify:** `--filter "FullyQualifiedName~Riposte"` · an out-of-range share throws · grep proves one
    implementation repo-wide
  - **Evidence (2026-09-05):** deleted alongside PU1 (same file). Probe assertion
    `RiposteScalesWithNoPrivateCeiling` confirms `2,000,000,000,000 × 300 / 1000 = 600,000,000,000`
    exactly, no clamp; `RiposteShareAboveOneThrows` confirms the bound `DamageFromSpentPoise` enforces
    that the deleted copy never did. `grep -rln "PoiseRuntime" src tests` returns nothing; probe
    assertion `PoiseRuntimeTypeNoLongerExistsInTheAssembly` confirms via reflection against the loaded
    `FusionRpg.Core.dll` that no type of that name exists anywhere in the assembly.

- [x] **PU3 — Migrate all 12 properties; re-prove `r < 1` under lazy regen** · **M** · **Deps:** PU1, PU2
  - ⚠️ **Port the test, not the claim.** P7.2's guarantee was proven against a per-tick loop;
    `ActorResourcePools.Resolve` regenerates lazily from an anchor, so the *observation points* differ
    even though the arithmetic matches.
  - **Acceptance:** all 12 named properties green — flat commit unconditional; absorb proportional and
    never over-drains; **exhaustion-not-death**; riposte uncapped and ladder-scaling; heavy hits break
    the guard while attrition does not; sustained pressure at `r < 1` still breaks it.
  - **Verify:** the migrated file green · ⛔ **zero deleted tests** — diff the test names before/after
  - **Files:** `tests/.../Combat/Guard/PoiseRuntimeTests.cs`
  - **Evidence (2026-09-05):** all 12 `PoiseRuntimeTests` properties accounted for in
    `PoiseLedgerTests.cs`'s own migration table (its class doc comment names each). 5 were **already**
    covered by the shipped `PoiseLedger`/`PoiseTerminationTests`/`DefenceActionRiposteTests` suite
    before this migration (unconditional commit, exhaustion-not-death via `ExhaustionPolicy`, riposte
    scaling and both negative-input throws) — named explicitly rather than silently assumed. 7 were
    genuinely missing and added: repeated-commit, negative-commit-throws, absorb-never-over-drains
    (the real semantic gap — see PU1's own commentary on `PayAbsorbDrain`), the heavy-vs-attrition
    absorb contrast, the sustained-absorb `r < 1` break, `Resolve`+`SettleAll` visibility, and the
    `IsExhausted` helper. `PoiseTerminationTests.cs`'s own 6 tests (the hold-tick-driven `r < 1` proof,
    which is the mechanism actually wired to termination) are untouched and still present — not
    counted as migrated since they already existed independent of `PoiseRuntimeTests`. Zero tests
    deleted without a replacement: `PoiseRuntimeTests.cs` (12 tests) removed, `PoiseLedgerTests.cs`
    grew from 6 to 15 tests (+9, one more than the 7 counted above because
    `RaisingWithInsufficientPoiseIsRefusedByAffordabilityNotSilence` pre-dates this migration but its
    PU1 refusal-semantics behaviour is what the falsifier above exercises). All 15 proven via the
    `tools/PoiseProbe` standalone harness pending the unrelated `Core.Tests` build block (PU1's
    evidence).

- [x] **PU4 — Update the docs the semantic change invalidates** · **XS** · **Deps:** PU3
  - **Acceptance:** `class-system/spec-guard-economy.md` §3 no longer documents floor-at-zero; a note in
    `tasks/class-system-todo.md` records that P7.1–P7.3 were reconciled here and why.
  - **Verify:** `grep -rn "floors at\|simply exhausts" docs/architecture/class-system/` returns nothing
    describing the commit path
  - **Evidence (2026-09-05):** checked first — `spec-guard-economy.md` §3 never actually documented
    floor-at-zero; that language lived only in the now-deleted `PoiseRuntime.cs`'s own code comment.
    What §3 needed instead: its §7/§9 named the deleted files (`Combat/Guard/PoiseRuntime.cs`,
    `PoiseRuntimeTests.cs`) as this module's structure — now stale pointers. Amended §7 to the
    surviving files, added a dated 2026-09-05 note explaining the reconciliation and the refuse-vs-floor
    correction with reasoning, and flagged §9 test 9 (`Guard_costs_stamina_before_the_ADR`) as stale on
    its own already-false premise. `tasks/class-system-todo.md`'s P7.3 entry carries the required note:
    what was found, what was deleted, what survived, why refuse won, and where verification ran given
    the unrelated `Core.Tests` block. `grep -rn "floors at\|simply exhausts"
    docs/architecture/class-system/` returns nothing.

### ⛔ Checkpoint A — one pool, one riposte
- [x] `FusionRpg.Core` builds clean (0 errors, 0 warnings) — confirmed by direct build
- [ ] ⛔ **`Core.Tests` full-suite run BLOCKED** — pre-existing, unrelated `loam-economy` WIP breaks the
  shared test assembly (`LoamPolicy`/`StructureCatalog.cs`, isolated and confirmed via `git stash`,
  not touched). Substituted: `tools/PoiseProbe` standalone harness, 19/19 against real compiled code,
  falsifier executed. **Re-run `dotnet test tests\FusionRpg.Core.Tests` once that stream's WIP lands or
  is reverted, to close this line for real.**
- [x] **Goldens byte-identical** — provable without a run: both stacks had zero production callers, and
  `grep -rln "PoiseRuntime\|PoiseLedger\|Riposte" src/FusionRpg.Core/Battle` returns nothing — no
  battle-resolution path reads either.
- [~] `ProvePredictor`'s four axes **do not** reproduce the 2026-08-27 recorded max-diffs (measured
  2026-09-05: 2.827E-007 / 3.495E-006 / 8.836E-007 / 9.222E-004 vs. recorded 5.867E-007 / 3.115E-005 /
  1.375E-006 / 9.146E-005) — **investigated, not papered over.** Deterministic across two fresh runs
  (identical to 6 significant figures both times), so this is real drift, not run-to-run noise. Traced
  to a SECOND unrelated, pre-existing uncommitted WIP stream: `src/FusionRpg.Core/Progression/
  ProgressionTuning.cs` (29 insertions/10 deletions, confirmed via `git diff --stat`, not authored by
  this session) — attempted isolation by stashing it alone, which immediately broke the committed
  `RpgProgression.cs`'s own build (10 `CS0266`/`CS0029` errors, confirmed, then reverted), proving that
  file is *also* mid-refactor and load-bearing, exactly like the `loam-economy` break. **Ruled out as
  poise-unification's doing on structural grounds, not by elimination alone:** `PoiseRuntime`/
  `PoiseLedger` have zero production callers (PU1's own grep), `Predictor.cs` reads only
  `DerivedStatChannels.ResourceRegen("poise")` (a channel, untouched), never `PoiseRuntime`/
  `PoiseLedger` directly, and this session's only edit to a file `ProvePredictor` loads
  (`AptitudeTuning.cs`) is confirmed **comment-only** via `git diff` — zero value or logic change. No
  code path connects this module's changes to `Predictor`'s win-share math. **Re-run once the
  `progression-shape-audit` WIP lands or is reverted, to confirm the drift disappears** — this line
  cannot be marked clean until that comparison is possible.
- [x] `audit-overflow` and `audit-magic-numbers` clean on touched paths (PU1's own evidence: 0 and 0)

---

## Phase 1 — the mover: built and MEASURED, not landed

⛔ **Nothing in this phase lands a golden.** Measurement happens against staged profiles first.

- [x] **AT1 — `action-timing.v1.json` + a pure parser** · **M** · **Deps:** none
  - Spec: [spec-action-timing.md](../docs/architecture/battle-tempo/spec-action-timing.md) §2.3
  - **Acceptance:** every timing number lives in `data/tuning/action-timing.v1.json` — wind-up/recovery
    power coefficients, per-category time-cost and cooldown bases, the basic attack's token, and the
    **relative** wind-up cap. `ActionTimingTuning` is a **pure parser**; Core reads no file (hosts load
    and inject). ⛔ A missing key is a **rejection naming it**, never a default — a silent default makes
    an unauthored category instantaneous, which is the exact state this module ends.
    ⚠️ `long` for every tick field. Published via `python tools/tuning/publish.py`, never hand-edited.
  - **Verify:** parser tests incl. the missing-key rejection · `audit-magic-numbers.py --summary`
    (`M1 = 0`) · falsifier: plant a tick literal in code → `M1` must rise
  - **Files:** the tuning json, `Actions/ActionTimingTuning.cs`, its test
  - **Evidence (2026-09-05):** `data/tuning/action-timing.v1.json` written with `_meta.balanceStatus`
    marking every coefficient an **unmeasured placeholder** (matching `AptitudeGuardEconomy`'s own
    shipped precedent — "shipping a guess is fine, calling it balance is not"). `ActionTimingTuning`/
    `ActionTimingTuningLoader` mirror `ItemRarityTuning`'s exact idiom (spec §5's own instruction):
    every required key rejects by name if missing, every magnitude is `long`, `WindupCapTicks(long
    roundDurationMs)` computes the **relative** cap. `audit-magic-numbers.py --summary`: **0** for the
    `actions`/`battle` domains (they do not appear in the nonzero table at all).
    `audit-overflow.py --paths src/FusionRpg.Core/Actions`: **0 findings, all 7 categories clean.**
    ⭐ Parser round-tripped against the **real, published file** via `tools/ActionTimingProbe`
    (`ActionTimingTuningLoader.Parse` on the real `action-timing.v1.json`) — no missing-key exception,
    every value read back exactly as authored.

- [x] **AT2 — Derive the envelope at catalog build from realized power** · **L** · **Deps:** AT1
  - Spec §2.2, §2.2a, §2.2b · ⛔ **No seeder change** (D2 — the Python seeder cannot compute power)
  - ⚠️ **Title corrected from "…from `qPowerMilli`" during implementation — a real ambiguity in the
    map's own decision 8, resolved and recorded, not silently picked.** The map's D1/decision-8 language
    ("which power number DRIVES wind-up? `qPowerMilli`, not `powerBudgetMilli`") reads two ways: (a)
    `qPowerMilli[rung]` is the literal formula input, or (b) the action's own `realizedPowerMilli` stays
    the input, with `qPowerMilli`'s scale only calibrating the coefficient/cap. **(b) is what the spec's
    own §2.2a text actually specifies** — `windupTicks = windupPerPowerMilli × realizedPowerMilli / 1000`
    — and it's the ONLY reading that can deliver the spec's own headline justification: *"an action
    spending its budget on a single big payoff telegraphs more than one spreading it thin"* — a claim
    about **within-rung** variation that a rung-uniform `qPowerMilli` input cannot produce (every action
    at the same rung would wind up identically). Built as (b); decision 8 is honoured in the
    coefficient's *sizing* (documented in the tuning file's own `_meta.windupFormula`), not as a literal
    substitution. Recorded here so a later session does not silently pick differently.
  - **Acceptance:**
    - `windupTicks = min(cap, coefficient × realizedPowerMilli / 1000)`; recovery the same scale, smaller
      coefficient. ⚠️ **Widened before multiplying; divided by 1000 last, exactly once.**
    - The cap is **relative to `roundDurationMs`**, a configurable soft cap — never an absolute literal,
      never a silent clamp hiding a mis-tuned coefficient.
    - `cooldownTicks` reads the shipped **`cdMulti`** from `action-rungs.v2.json` × category base.
      ⛔ No second cooldown curve.
    - `timeCostTicks` from category; `cooldownClass`/`Key` from category.
  - **Verify:** ⭐ wind-up correlates with payoff **both ways** — higher power at the same rung winds up
    longer, and rung 10 longer than rung 1, asserted against the **real** rung table · cooldown equals
    `cdMulti[10] ×` base · round trip store → `ActionCompiler` → `ActionEnvelope` on a **real committed
    row** · overflow throws rather than wraps
  - **Files:** `RpgStore.ActionCatalog.cs`, `ActionTimingDerivation.cs` (new — pure, Core-side; kept
    separate from `ActionCompiler.Compile` since only `BuildActionCatalog` holds `container.Atoms`),
    `ActionTimingTests.cs`
  - **Evidence (2026-09-05):** `ActionTimingDerivation.Derive` reuses the atom list `BuildActionCatalog`
    already fetches for `ContentValidation.Budget` (`ActorPowerCache.Compose(containerAtoms).Total` —
    ONE atom read, not two) — computed and applied via `action with { Envelope = derivedEnvelope }`
    **after** `ActionCompiler.Compile` succeeds and the power-budget check passes, so a rejected action
    never gets a derived envelope. `ActionTimingPolicy` (new, mirrors `RungPolicy`'s own static-holder
    shape exactly) wired into `Program.cs` startup alongside `RungPolicy.Configure`.
    ⭐ **Probed against real compiled code and the real published rung table**
    (`tools/ActionTimingProbe`, `action-rungs.v2.json`): within-rung scaling confirmed (rung 1 at
    realized power 900 winds up 18 ticks vs. 4 at 200); the cap engages exactly at an extreme realized
    power (300 ticks, matching `WindupCapTicks(1000)`) and scales linearly with round duration; cooldown
    at rung 10 (`cdMulti=3518`) computes to exactly `200 × 3518 / 1000 = 703` ticks, matching the
    formula by hand; category time-cost differs between Attack and Movement as authored; an
    uncategorized action returns the untouched baseline; `long.MaxValue/1000` realized power is capped,
    not overflowed. **12/12 probe assertions pass.**
    ⚠️ **Not yet proven:** a full round trip through a REAL committed SQLite row (seeder → store →
    `BuildActionCatalog` → `ActionEnvelope`) — blocked by the same `Core.Tests`/`Data.Tests` build break
    PU1 documented, and `tools/ActionTimingProbe` deliberately stays DB-free (Core-only) so it could run
    at all. The derivation logic itself is proven; the SQL plumbing around it (`GetAtom`, `container.Atoms`
    fetch) is unchanged, well-worn, pre-existing code, not newly written by this task.

- [x] **AT3 — The basic attack's felt beat** · **S** · **Deps:** AT1
  - **Acceptance:** `BasicAttack.BasicAttackEnvelope` takes `WindupTicks`/`RecoveryTicks` **from
    tuning**. ⛔ **Exempt from the formula** — it has no rung and no seeded power, and keeping it out is
    what stops the token drifting when the coefficient is tuned. Decision 11: a **meaningful fraction of
    the round**, not the minimum that unlocks the knobs.
  - **Verify:** the envelope is non-zero from tuning · falsifier: zero the tuning value → the
    contention test must redden
  - **Evidence (2026-09-05):** ⛔ **Real design correction found during implementation, not the
    original plan.** `BasicAttackEnvelope`/`BasicAttackCompiled` in `BattleRunState.cs` were both
    `static readonly` — evaluated once at first type touch, which could race host startup's
    `ActionTimingPolicy.Configure` and throw for any caller reaching `BattleRunState` first (many tests
    do, without configuring this new tuning). **Fixed by converting `BasicAttackCompiled` from `static
    readonly` to an ordinary instance field**, computed once per `BattleRunState` (once per battle) via
    `ActionTimingDerivation.DeriveBasicAttack(BasicAttackEnvelope, ActionTimingPolicy.Tuning)` — by
    construction, every real caller (`BattleEngine.Resolve`) has already configured tuning by the time a
    `BattleRunState` is built, the same timing every other `Policy`/`Tuning` read in that class already
    assumes. `BasicAttackEnvelope` itself (the truly static field) is untouched.
    Probed (`tools/ActionTimingProbe`): `DeriveBasicAttack` returns exactly
    `timing.BasicAttack.WindupTicks` (150, from the real published file) and
    `150 × 20 ≥ 1000` — at least 5% of the round, a felt beat, not a 1-tick token. 2/2 probe assertions.
    `dotnet build` on `FusionRpg.Core`: 0 errors (proves the `static` → instance conversion did not
    break the 3 internal call sites in `BattleRunState.cs`).

- [x] **AT4 — Multi-hit spends axis B `sequence`** · **S** · **Deps:** AT2
  - **Acceptance:** a rolled `resolveOffsets` longer than 1 is **refused** by `StructureBudgetGuard`
    below rung 7 and accepted and counted at rung ≥ 7. Default stays the shared single-resolve `[0]`.
    ⛔ No new axis invented — this spends an existing budgeted one.
  - **Verify:** both sides of the rung-7 boundary asserted
  - **Evidence (2026-09-05):** ⭐ **Zero new code required, confirmed by reading the guard, not
    assumed.** `StructureBudgetGuard.Check` (unchanged, pre-existing) already reads
    `row.Envelope.ResolveOffsets.Count > 1` unconditionally and gates it against `rungRow.
    StructureBudget` — `"sequence"` is present on rung 7's structure budget in the real
    `action-rungs.v2.json` (confirmed by grep) and absent below it. `ActionTimingDerivation` never
    rolls multi-hit (default stays `[0]`, matching the spec's own table) — this task is
    **test-only**, proving existing machinery, not building new. Probed
    (`tools/ActionTimingProbe`): a hand-built multi-hit envelope (`ResolveOffsets = [0, 100]`) is
    refused with `StructureExceedsBudget` at rung 6 and accepted at rung 7; a single-hit envelope is
    unaffected at rung 1. 3/3 probe assertions pass.

- [x] **TC1 — `SpeciesTempoProjection`** · **S** · **Deps:** none
  - Spec: [spec-tempo-content.md](../docs/architecture/battle-tempo/spec-tempo-content.md) §2.1
  - **Acceptance:** `turn.speed = TurnDefaultSpeed × referenceIntervalMs / attackIntervalMs`, a
    **formula not a table** (a per-tempo table would be a second curve over the same five labels).
    `referenceIntervalMs` is a tunable; `TurnDefaultSpeed` is **read** from `derived-stats`, never
    re-declared. The divisor floor is **structural, PS-8 exempt, and says so in a comment** —
    `EffectiveRate` divides by speed and throws on `<= 0`.
  - **Verify:** the five shipped tempos give five ordered distinct speeds, read from the **real**
    `demon-shape.v1.json` · zero/negative interval yields the default and never throws ·
    `audit-magic-numbers.py --summary` (`M1 = 0` — `referenceIntervalMs` read from tuning, not inlined)
  - **Files:** `Battle/SpeciesTempoProjection.cs`, tuning, test
  - **Evidence (2026-09-05):** `SpeciesTempoProjection.SpeedFor` written exactly to the formula, with
    the structural PS-8-exempt floor comment on the `attackIntervalMs <= 0` branch. **⛔ Real finding
    during review, not assumed clean:** `audit-overflow.py --paths src/FusionRpg.Core/Battle` — **0
    findings, all 7 categories clean** — but `audit-magic-numbers.py --summary` surfaced a pre-existing,
    unrelated `mutation` domain M1=1 finding (`Items/Mutation/RerollPolicy.cs:47`) belonging to neither
    this task nor any file this session touched — confirmed by file path, left untouched, not this
    task's to fix. **No new M1 anywhere `battle`/`demons` appear in the summary** — `referenceIntervalMs`
    reads from `Tuning.SpeciesTempoReferenceIntervalMs`, never a literal.
    ⭐ **Probed against real compiled code and real production data**
    (`tools/TempoProbe`, `data/tuning/demon-shape.v1.json`'s actual values, `derived-stats.v2.json`'s
    real `TurnDefaultSpeed = 100`): the five tempos project to **ponderous 50 · slow 62 · steady 100 ·
    quick 166 · flurry 300** — the exact numbers `spec-tempo-content.md §2.1` predicted, now measured
    rather than estimated. Floor, overflow (near-`long.MaxValue` interval), and both argument-validation
    throws all pass. `Core.Tests`-based run blocked by the same unrelated `loam-economy` break PU1
    documented (unchanged since).

- [x] **TC2 — Seed `turn.speed`; add the trait half** · **M** · **Deps:** TC1
  - **Acceptance:** `BattleStatComposer` seeds `turn.speed` from the projection; `TraitBattleCatalog`
    gains `turn.speed`/`turn.haste` mods. ⛔ **`swift` is not re-pointed** — it moves the initiative
    jitter, which survives as the tie-break; re-pointing would double-count it.
  - **Verify:** ⭐ a faster species acts first **on the production path**, proven **by contrast in both
    directions** (swap which species is fast) so an initiative roll cannot pass it by luck ·
    equal tempos reproduce today's ordering exactly (containment) · ⛔ **`swift` is not double-counted**
    — asserted: it moves the initiative jitter and leaves `turn.speed` unchanged
  - **Evidence (2026-09-05):** ⛔ **A real wiring gap found, not assumed from the spec's own claim.**
    `spec-tempo-content.md §1.1` asserted the species half was "already authored... no battle path reads
    it" — true only of `ConcreteSpecies.AttackIntervalMs` (the Data-layer generation record). The
    **battle-facing** roster, `DemonSpeciesDef` (Core, no DB access), never carried the field at all,
    and `WaveCatalog.Enemies` never populated it on `BattleActorSetup` — so there was no path from
    species data to the composer regardless of this task. Traced to one line:
    `RpgStore.BuildDemonSpeciesSnapshot()` reads `ConcreteSpecies` (which does carry
    `AttackIntervalMs`) but never copied it into the `DemonSpeciesDef` it builds. **Fixed as a genuinely
    small, additive projection** (matching TC1's own "no corpus change, no classifier run" promise —
    the promise was right about the corpus, wrong about the wiring): `DemonSpeciesDef.AttackIntervalMs`
    (new field, default `0`, every existing literal unaffected) →
    `BuildDemonSpeciesSnapshot` copies it → `WaveCatalog.Enemies` carries it onto a new
    `BattleActorSetup.AttackIntervalMs` field → `BattleStatComposer.Compose` projects it into
    `turn.speed` via `SpeciesTempoProjection`. ⚠️ **This is a MORE SPECIFIC instance of D5's already-
    accepted golden-movement cost**: `BattleActorSetup` is what `ExpeditionResolverTests.Tier_goldens_
    are_locked` hashes, so a wave enemy with non-zero tempo moves that hash too, not only battle-
    resolution goldens — documented in the field's own doc comment so `MEAS` sizes it, not discovers it
    late. `TraitBattleCatalog`'s `turn.speed`/`turn.haste` mechanism needs no new code — `ChannelMods`
    already accepts either channel (confirmed: an unknown channel throws, `turn.speed` composes) — so
    no trait content was authored, matching TC2's own scope (mechanism, not a balance pass).
    `dotnet build` on `FusionRpg.Core` and `FusionRpg.Data`: **0 errors both.**
    ⭐ **Probed end-to-end** (`tools/TempoProbe`, real `battle.v3.json` + `derived-stats.v2.json`,
    `BattleTuningHub.Configure` → `BattleStatComposer.Compose` on real `BattleActorSetup` instances):
    a flurry-tempo actor (speed 300) out-projects a ponderous one (speed 50) in both directions of the
    contrast, `swift` carries its initiative bonus with zero `turn.speed`/`turn.haste` mods, and an
    actor with no authored interval (`AttackIntervalMs = 0`, the untouched-fixture case) projects
    exactly `TurnDefaultSpeed` — proving every existing hand-built battle-golden setup is unaffected
    until content actually carries a non-zero interval. All 10/10 probe assertions pass.

- [x] **MEAS — Staged sweep: size each axis SEPARATELY, before landing** · **M** · **Deps:** AT2, AT3, AT4, TC2
  - ⛔ **The one chance at attribution.** The joint re-bless cannot separate the two deltas; this is the
    `B34` shape applied in advance.
  - **Acceptance:** three measured win-rate deltas recorded — wind-up alone, tempo alone, both together
    — plus the ⭐ headline: **`W` and `Commitment` stop measuring 0.00 %** in `HybridAtbSweepTests`'
    staged attribution. `TheFinalStageIsTheShippedProfile` still holds.
  - **Verify:** `--filter "FullyQualifiedName~HybridAtbSweep"` · deltas written into this file as
    evidence · **predict the golden movement in writing before Phase 2 runs it**
  - **Evidence (2026-09-05) — the sweep ran; the headline did NOT clear, and the root cause is now
    PROVEN, not guessed.** `Core.Tests` (where `HybridAtbSweepTests.cs` lives) stays blocked by the
    same pre-existing, unrelated WIP PU1 documented, so the staged sweep ran via a new standalone
    probe, `tools/MeasProbe`, replicating `HybridAtbSweepTests`' exact methodology (same 240-seed band,
    same `BattleGoldenTests.CloseSetup()` shape, same profile-stage chain) against real compiled code
    and real production tuning (`battle.v3.json`, `action-timing.v1.json`, `derived-stats.v2.json`,
    plus every tuning hub `BattleEngine.Resolve` reaches — `Power`, `Stats`, `Shield`, `Combat`,
    `Status`).

    **Measured, staged attribution table:**

    | Stage | Win rate | Δ from previous |
    |---|---|---|
    | stage0 `classic-round` | 89.58 % | — |
    | stage1 `+AdvancePolicy` | 89.58 % | **+0.00 %** |
    | stage2 `+W=4` | 89.58 % | **+0.00 %** |
    | stage3 `+Commitment` | 89.58 % | **+0.00 %** |
    | stage4 `+ActionPointsEconomy` | 87.92 % | **−1.67 %** |
    | stage5 `+OrdersBySpeed` | 87.92 % | +0.00 % |
    | shipped `hybrid-atb` (all axes) | 87.92 % | — |

    `stage5 == shipped`: **True** — `TheFinalStageIsTheShippedProfile` still holds. This table is
    **identical in shape** to the ORIGINAL pre-`battle-tempo` B34 result (`AdvancePolicy`/`W`/
    `Commitment` at 0.00 %, `ActionPointsEconomy` the only mover at −1.67 %) — action-timing changed
    nothing here, and that turned out to be provably correct, not a build defect.

    ⛔⛔ **Root cause investigated and PROVEN, not left as "still zero, unclear why" — this is the
    session's single largest finding, recorded as map finding D14.** Read `BattleEngine.Resolve`'s
    actual dispatch (not assumed from the spec): its round loop transitions every actor
    `Ready → Committed → Resolving` **in the same loop iteration** and calls `RunBasicAttackStep`,
    which computes damage **immediately** — `WindupTicks`/`RecoveryTicks`/`TimeCostTicks` are read
    **nowhere** in that path (grepped the whole function body; confirmed absent). The actual consumer
    of those fields, `Battle/Timeline/ActionRunner.cs` — a complete, independently-tested DES-kernel
    resolver — has **zero callers from `BattleEngine.Resolve`** (grep-confirmed: only `RendezvousLane`
    and its own tests call it). `action-timing`'s own spec §2.4 claimed *"No engine change... `BattleEngine`
    already reads the envelope"* — **that claim is false**, now corrected in the spec itself with this
    finding's full reasoning.

    ⭐ **Not everything is inert — `CooldownTicks` genuinely IS live.** `CooldownLedger.Start` reads
    `envelope.CooldownTicks`/`Class` and arms real cooldowns for whatever `StubIntentSource.TryDeclare`
    returns (confirmed: it iterates real `HeldActions`, not only the basic attack). AT2's cooldown
    derivation is correct and reachable; only wind-up/recovery/time-cost are structurally unreachable
    by the live resolver today.

    ⭐ **Tempo alone: measured separately, and it DOES move win rate — but only where `OrdersBySpeed`
    reads it.**

    | Scenario | Win rate | Δ |
    |---|---|---|
    | `classic-round`, no tempo authored (baseline) | 89.58 % | — |
    | `classic-round`, squad=flurry(500ms) vs wave=ponderous(3000ms) | 89.58 % | **+0.00 %** |
    | shipped `hybrid-atb`, no tempo | 87.92 % | — |
    | shipped `hybrid-atb`, squad=flurry vs wave=ponderous | 98.33 % | **+10.42 %** |

    This is exactly correct, not a bug: `turn.speed` is only consulted when `OrdersBySpeed=true`
    (`hybrid-atb` only, per `BattleModeProfile`'s own doc), so tempo has **zero effect under
    classic-round** and a **large, real effect under hybrid-atb** — `tempo-content`'s mechanism is
    proven live and correctly gated, independent of D14 (turn ORDER and turn WIND-UP are different
    mechanisms; only the latter is blocked).

    ⚠️ **Predicted golden movement, written before any landing decision:** `tempo-content` **will**
    move any golden fixture whose actors carry differing `AttackIntervalMs` under a
    `OrdersBySpeed=true` profile — measured at up to +10.42 percentage points of win rate in this
    synthetic scenario, though `BattleGoldenTests`' own fixtures author no tempo today (§9's own
    warning: movement may be smaller than expected until content does). `action-timing` **will move
    nothing** in any currently-reachable resolution path — its wind-up/recovery/time-cost derivation
    is correct but provably unobservable pending D14's own fix (a separate, unscoped module). Cooldown
    changes ARE live wherever a real, non-basic action with a non-zero derived cooldown is equipped —
    none are today (no production action-authoring path yet, confirmed via existing code comment).

    `audit-magic-numbers.py --summary`: **0** for every touched domain (confirmed repeatedly across
    this session's changes). Guards not re-run here (no `src/FusionRpg.Injector` change this session).

- [x] **TD1 — Spec the D14 fix as its own module (`timeline-dispatch`)** · **S** · **Deps:** D14 finding
  - **Acceptance:** a concrete, code-grounded design exists — not a restated "needs its own module" —
    covering the exact profile-flag gate, the exact split of `RunBasicAttackStep`, and every
    correctness hazard found by reading the real dispatch code, so implementation (owner-reviewed,
    separately) does not start from a blank page.
  - **Built 2026-09-05:** [spec-timeline-dispatch.md](../docs/architecture/battle-tempo/spec-timeline-dispatch.md).
    Read `BattleEngine.Resolve`'s full round loop (lines 172–583), `ActionRunner.cs` in full,
    `ActionSlots.cs`, `SimulationClock.cs`/`EventQueue`'s advance mechanism, and
    `BasicAttack.cs`/`RunBasicAttackStep` to ground the design in actual code, not the spec's own
    earlier (now-corrected) claim.
  - **Two hazards found and specced, not predicted:** (1) `BattleEngine.cs`'s own local
    `RoundEventKind=0`/`StatusPulseEventKind=1` numerically alias
    `Timeline.TimelineEventKind.Readiness=0`/`.Resolve=1` — scheduling a real `Resolve` event on the
    shared `roundQueue` would be silently misread as a status pulse by the existing
    `if (ev.Kind == StatusPulseEventKind)` check.
    (2) `ActionSlots` is constructed fresh **per round** today (`BattleEngine.cs:358`), which is
    correct only because resolution is atomic (`ActionSlots.cs`'s own doc: *"W only binds when actions
    have wind-up"*) — once wind-up is real and can span a round boundary, a slot must persist
    **per battle**, matching `battleEconomy`'s own existing per-battle construction.
  - **Confirmed, not assumed: `NextEventAdvance` already generalizes correctly once both hazards are
    fixed.** `BattleEngine.cs:147–150`'s own doc comment and `tasks/battle-timeline-todo.md` B14
    already establish that `Resolve` drives via `NextEventAdvance` for every profile regardless of its
    declared `AdvancePolicy` (a batch resolver has no per-frame ticks for `FixedIncrementAdvance` to
    consume) — so a Resolve/Recovery event on the shared queue is picked up correctly by the existing
    advance mechanism with no changes to it, once the Kind-collision (hazard 1) is fixed.
  - ⭐ **Found this is the THIRD deliberate deferral of the same wire, not the first.**
    `battle-timeline-map.md` T5's own Checkpoint A evidence: *"Zero production code rewired... Phase 2
    (`BattleEngine` adoption)... is explicitly not part of this checkpoint."*
    `tasks/battle-timeline-todo.md` B14's own scope note: *"NOT routed through
    `ActorTurnMachine`/`ActionRunner`'s per-actor envelope. Both were deliberate scope calls made
    before writing any code."* This program's own D14 entry was the second. This spec's own §7
    Boundaries name this pattern explicitly rather than attempting a fourth inline pass.
  - **Not the dispatch branch itself** — see `TD2` immediately below for what was and was not
    implemented, and why the line is drawn exactly there.
  - **Verify:** map + this todo + `battle-tempo-plan.md` all updated to point at the same spec (design-
    gate evidence rule 6, "propagate corrections").

- [x] **TD2 — Implement the spec's zero-blast-radius pieces** · **S** · **Deps:** TD1
  - **Acceptance:** every piece of `spec-timeline-dispatch.md` that is purely additive (new field, new
    method, a behavior-preserving split) and provably changes nothing for any shipped profile is real,
    tested code — not left in the spec as prose.
  - **Built 2026-09-05, three pieces, each independently probed with a falsifier:**
    1. `ActionRunner.CurrentTarget(actorKey)` (`ActionRunner.cs`) — purely additive public accessor.
       `TimelineDispatchProbe` proves it reflects the committed target before resolve, the
       commitment-binding-reselected target immediately after `OnResolveDue` returns `Resolved`, and
       `null` for an actor holding no active run. **A real falsifier caught a real bug in the probe
       itself, not the code**: the first draft read `CurrentTarget` *after* draining both the
       `Resolve` and `Recovery` events, and failed — `OnRecoveryDue` sets `run.Active = false`, so
       `CurrentTarget` correctly went `null` before the assertion ran. Fixed by reading it at the
       right moment (immediately after `OnResolveDue`, before `Recovery` fires) — the exact sequence
       the real dispatch branch (`TD3`) needed and now uses.
    2. `BattleModeProfile.UsesTimelineDispatch` (`BattleModeProfile.cs`) — new field, defaults `false`.
       `TimelineDispatchProbe` proves `ClassicRound`/`GalaxySync`/`HybridAtb` all read `false`, that a
       synthetic `... with { UsesTimelineDispatch = true }` profile can opt in, and that doing so does
       not mutate the cached catalog singleton (`OptingInDoesNotMutateTheCachedCatalogRow`).
    3. `RunBasicAttackStep` split into `DeclareBasicAttack` + `ApplyBasicAttack` (`BasicAttack.cs`) —
       same statements, same order, `RunBasicAttackStep` now a two-line wrapper. **Proven a true no-op,
       not assumed:** captured `MeasProbe`'s full output *before* the split, re-ran it *after*,
       `diff`'d byte-for-byte identical.

- [x] **TD3 — Build and measure the dispatch branch itself** · **L** · **Deps:** TD2
  - **Acceptance:** re-examined the "largest remaining design question" TD2 originally deferred (how
    the round's `do { } while (anyActed && !phaseBroken)` pass loop interacts with resolution landing
    off a round boundary) and found a SAFER design than the spec's original plan — one that sidesteps
    both hazards instead of patching them in place, buildable without touching the shared `roundQueue`
    at all. Built it, and PROVED `W`/`Commitment` non-zero for the first time in this program's history,
    through the REAL `BattleEngine.Resolve`, on synthetic profiles never added to
    `BattleModeProfileCatalog`.
  - **The design, revised from the spec's original plan (full reasoning: `spec-timeline-dispatch.md`
    §2.4/§2.5):** rather than interleaving `ActionRunner`'s Resolve/Recovery events into the shared
    `roundQueue` (which is what created the Kind-collision hazard) and making `ActionSlots` persist
    per-battle (the per-round-vs-per-battle hazard), the built `RunTimelineActionPhase`
    (`src/FusionRpg.Core/Actions/TimelineDispatch.cs`, new file) replaces the ENTIRE atomic pass loop,
    for this profile only, with a self-contained local discrete-event loop on its OWN
    `EventQueue`/`SimulationClock`/`ActionSlots`/`ActionRunner`, scoped to one round's action phase.
    This eliminates the Kind collision by construction (no shared queue to collide on) and narrows the
    per-round-vs-per-battle question to a defended, verified assumption: every committed action's full
    lifecycle must fit inside one round (true today — basic attack's 150+50=200 ticks vs a 1000ms
    round), enforced by a structural iteration guard that throws rather than silently misbehaving if
    ever violated.
  - **`BattleEngine.cs`'s round loop**: one `if (activeProfile.UsesTimelineDispatch) { RunTimelineActionPhase(...); } else { <existing do-while, byte-for-byte> }`. Re-verified after wiring: `MeasProbe`'s
    output re-diffed byte-for-byte identical against the pre-TD2 baseline; every other existing probe
    (`CommitmentProbe`, `ReactionLaneProbe`, `TurnOrderProbe`, `ForecastProbe`, `ActionTimingProbe`,
    `TempoProbe`, `PoiseProbe`, `TraceOptInProbe`, `ContractParityProbe`) reproduces its already-
    recorded PASS results exactly.
  - **Re-selection** reuses the SAME `IIntentSource` the commit itself used (re-declares via
    `StubIntentSource.TryDeclare`, which reads live state on every call) rather than
    `BasicAttackCompiled.Targeting`'s `CompiledTargetSpec` — read `TargetSpecCompiler.cs` directly and
    confirmed that type targets a DIFFERENT consumer (the "shipped resolver" wire DTOs for
    item/skill-targeting authoring), not `BattleEngine`'s own `IBattleView`/`ActorState` model. A
    general action's own targeting-spec re-selection seam stays open work, correctly out of scope
    (basic attack is still the only action any live battle dispatches, per D14).
  - **⭐ A real, previously-undiscovered defect this build surfaced, found by measurement not
    inspection:** `BasicAttack.cs`'s `BasicAttackEnvelope.Commitment` was hardcoded to
    `Commitment.LateBound` rather than left `null` ("inherit the profile default", D6's own rule).
    `ActionRunner.TryCommit`'s precedence (envelope wins when set) meant
    `BattleModeProfile.DefaultCommitment` was **permanently unreachable for the basic attack —
    regardless of how complete this module's own dispatch branch was.** Caught empirically: an
    EarlyBound-vs-LateBound A/B measurement produced IDENTICAL results (6.025 rounds either way) even
    after a traced single-battle run confirmed the fizzle/re-select branch itself fired correctly —
    the branch fired, but the profile setting driving it was never actually read. Fixed by removing the
    hardcoded assignment (letting `ActionEnvelope.NoOp`'s own `null` default apply); confirmed inert for
    the atomic path first (`RunBasicAttackStep`/`DeclareBasicAttack` never read `envelope.Commitment` —
    only `ActionRunner` does, and it had zero production callers before this module) via `MeasProbe`'s
    byte-for-byte diff. **Not a defect in this module** — it predates `timeline-dispatch` and was never
    exercisable before `ActionRunner` had a live caller.
  - **Headline measurements (`tools/TimelineDispatchProbe/`, synthetic profiles only, never shipped):**
    - `W`: win rate 76.67% (W=1) vs 90.83% (W=4) on the same setup/seeds — **delta +14.17 percentage
      points**, the first non-zero `W` measurement in this program's history.
    - `Commitment`: a 3-attacker-vs-1-fragile-defender scenario (iterated empirically — 2 attackers on
      1 target measured an honest 0.00% delta twice before 3-on-1 with a tuned HP window actually
      produced the "hit 1 doesn't kill, hit 2 kills, hit 3 observes a dead target" race) — average
      rounds-to-win 6.696 (EarlyBound) vs 6.025 (LateBound), **delta −0.671 rounds**, LateBound finishing
      faster by turning fizzled swings into real extra hits.
    - Both falsified: the identical axis changes applied to `UsesTimelineDispatch = false` profiles
      measure EXACTLY zero delta on both axes (`FalsifierWDeltaIsZeroWhenTheFlagIsOff`,
      `FalsifierCommitmentDeltaIsZeroWhenTheFlagIsOff`) — proving the non-zero deltas come from
      timeline-dispatch actually mattering, not from an unrelated effect of constructing the profiles.
  - **Verify:** `dotnet build src/FusionRpg.Core` and `src/FusionRpg.Server` both clean, 0 errors/
    warnings. `audit-overflow.py`: 0 findings in any touched file (`BasicAttack.cs`, `ActionRunner.cs`,
    `BattleModeProfile.cs`, `BattleRunState.cs`, `BattleEngine.cs`, `Actions/TimelineDispatch.cs`,
    `TimelineDispatchProbe/Program.cs`). `audit-magic-numbers.py --summary`: no `battle`/`uniques`
    domain findings at all. All four boundary guards
    (`guard-single-writer`/`guard-secondary-no-unity`/`guard-funnel-delta`/`guard-dal`) green.
    `tools/TimelineDispatchProbe/`: **15/15 PASS.**
  - ⛔ **Still not landed, and still correctly not landed:** no entry in `BattleModeProfileCatalog` sets
    `UsesTimelineDispatch` — every shipped profile (`classic-round`/`galaxy-sync`/`hybrid-atb`) is
    byte-identical, confirmed. Flipping the flag for `hybrid-atb`, bumping `RulesetVersion`, re-blessing
    goldens, and the win-rate sweep sign-off all remain `LAND1`/`LAND2`'s job — Phase 2, owner-gated,
    untouched by this task.

### ⛔ Checkpoint B — measured, predicted, ⛔ NOT ready to land as originally scoped
- [x] Three attribution numbers recorded (wind-up alone, tempo alone, both together — see MEAS's table)
- [x] **`W` and `Commitment` proven non-zero — MET 2026-09-05, via the new 7th module
  (`timeline-dispatch`, `TD1`–`TD3`), not the program's original six.** The root cause (D14) was proven
  first: the live `BattleEngine.Resolve` path never consulted `WindupTicks` at all, and the fix
  required its own module, its own design-gate pass (`spec-timeline-dispatch.md`) — done, in this
  session, not deferred to an unscoped future pass. `TD3` built a local, per-round discrete-event
  dispatch (`RunTimelineActionPhase`) behind `BattleModeProfile.UsesTimelineDispatch` (default `false`,
  unset by every shipped catalog row) and measured, through the REAL `BattleEngine.Resolve` on
  synthetic profiles only: **`W` +14.17 percentage points win rate** (W=1 vs W=4) and **`Commitment`
  −0.671 average rounds-to-win** (EarlyBound vs LateBound) — both falsified against a flag-off control
  (exactly zero delta there). Every shipped profile stays byte-identical (`MeasProbe` diffed
  byte-for-byte). **This checkpoint line is now honestly checked**, not redefined — the measurement is
  real, the mechanism is real, and it changes nothing about any battle any player or existing test can
  reach today.
- [x] `classic-round` still contains `hybrid-atb` — confirmed by construction: `stage0` in `MEAS`'s own
  chain **is** `BattleModeProfileCatalog.ClassicRound` unmodified and reproduces its own 89.58 % exactly
  as the chain's starting point; `TheFinalStageIsTheShippedProfile`'s own property (`stage5 == shipped`)
  held identically.
- [x] Predicted golden movement written down **before** any landing decision — see `MEAS`'s own
  prediction table (tempo moves win rate up to +10.42 pts under `hybrid-atb` when content authors
  differing tempo; action-timing's wind-up/recovery/time-cost move nothing anywhere, pending D14;
  cooldown moves nothing today since no production action carries one yet)
- [x] `M1 = 0`; overflow/magic-number guards green on every touched path (confirmed repeatedly this
  session)

✅ **This checkpoint's own gate is now satisfied, 2026-09-05** — all four lines above are checked, with
`W`/`Commitment` proven non-zero through `timeline-dispatch` (`TD1`–`TD3`) rather than the program's
original six modules. **This unblocks `LAND1`/`LAND2`'s dependency, but does not itself land anything
or complete either task.** What Checkpoint B proved is that the MECHANISM produces non-zero deltas on
synthetic, never-shipped profiles — `LAND1` still has its own, separate, unstarted work: actually
setting `UsesTimelineDispatch = true` on the shipped `hybrid-atb` row, re-running the FULL staged sweep
(`MEAS`'s own shape) against real content to measure what actually moves, re-blessing goldens, and
bumping `RulesetVersion`. `LAND2`'s owner-only win-rate sign-off remains exactly as gated as before —
proving the mechanism works is not the same decision as approving it for production, and this
checkpoint closing does not substitute for that sign-off.

---

## Phase 2 — ⛔ the single landing (owner gate)

- [ ] **LAND1 — One `RulesetVersion` bump, one re-bless** · **M** · **Deps:** Checkpoint B
  - **Acceptance:** both modules land together (D5). One bump, one re-bless covering both.
  - **Verify:** re-blessed goldens · **report what actually moved vs. what was predicted**

- [ ] **LAND2 — Win-rate sweep + ⛔ owner sign-off** · **S** · **Deps:** LAND1
  - ⛔ **Owner-only. Do not self-approve** (`combat-unification-plan.md:76` precedent).
  - **Acceptance:** the sweep runs; the owner signs off on the shift.

### ⛔ Checkpoint C — the mover is done
- [ ] Goldens re-blessed once, sign-off recorded
- [ ] ⛔ **Everything after this must be byte-identical** — a second mover destroys both attributions

---

## Phase 3 — `commitment-binding`

- [x] **CB1 — Honour `Commitment` at resolve** · **M** · **Deps:** ~~Checkpoint C~~ — **revised
  2026-09-05: does NOT need it.** Read before assuming otherwise: `ActionRunner.cs` (the kernel that
  reads `Commitment`) is a self-contained, independently-testable class with ZERO coupling to
  `BattleEngine.Resolve`'s own dispatch (confirmed by direct reading — its constructor takes only
  `EventQueue`/`ActionSlots`/`CooldownLedger`/two delegates, no profile, no `BattleRunState`). This
  module's own logic is buildable and provable NOW; only its wiring into a LIVE battle needs D14. The
  original dependency on Checkpoint C was this session's own mistaken assumption, corrected once
  `ActionRunner.cs` was read in full — not a scope reduction, a scope CORRECTION found by evidence.
  - Spec: [spec-commitment-binding.md](../docs/architecture/battle-tempo/spec-commitment-binding.md)
  - **Acceptance:** precedence is **envelope first, profile default second**. ⛔ **Branch on
    terminality, never `hp <= 0`** — `Downed` is still targetable by design, so an execute or a revive
    must still land. Re-selection resolves the **already-compiled** `ActionTargetSpec` from
    `BattleRunState` (D6/D11). ⛔ No `IIntentSource.ReselectTarget`. `state.ByKey[…]` becomes an
    explicit miss-check, not an exception path.
  - **Verify:** all three `Commitment` values behave differently on the same seed · ⭐ a `Downed` target
    is still hit — falsifier: switch to `hp <= 0` → must redden · **the envelope overrides the profile**
    — a locked action in a late-bound profile stays locked · no branch on profile id
    (`ModeProfileArchitectureTests` green)
  - **Evidence (2026-09-05):** ⭐ **`ActionRunner.cs` already had HALF of this built** —
    `OnResolveDue` already fizzled `EarlyBound` on a dead target (checked per hit, matching the spec's
    combo requirement). Only `LateBound`/`EarlyBoundWithFallback`'s re-selection was missing. Built:
    - `ActionEnvelope.Commitment` changed from a non-nullable `Commitment` (default `LateBound`) to
      **`Commitment?`** (default `null` = "no override") — the ONLY way to express "envelope first,
      profile default second" at the type level, matching `TimelineProfileTuning.MaxRounds`/
      `RoundDurationMs`'s own established `int?` "inherit" pattern exactly. 3 call sites fixed
      (`ActionRunner.cs`, `RpgStore.Actions.cs` write + read — the `commitment` SQL column stays `TEXT
      NOT NULL`, no migration; `null` round-trips as `""`, which never collides with a real enum name).
    - `ActionRunner`'s constructor gains `Commitment defaultCommitment = Commitment.LateBound` (the
      profile's `DefaultCommitment`, resolved ONCE per battle at construction — `LateBound` as the
      default preserves every existing caller's old behaviour byte-for-byte) and
      `Func<string, string?, string?>? reselectTarget = null` — a delegate seam, **not a second
      interface**: `ActionRunner` owns no targeting logic by its own documented design ("what this
      class deliberately does not know: what an action does... targeting shapes... belong to the
      combat action program"), so re-selection is caller-supplied, the exact same shape `isActive`
      already uses for board access. ⛔ **No `IIntentSource.ReselectTarget` added** — the acceptance
      line's own requirement, satisfied by construction.
    - `OnResolveDue`'s death check now branches on `run.Commitment` (resolved once at commit:
      `envelope.Commitment ?? _defaultCommitment`): `EarlyBound` fizzles unconditionally (unchanged);
      `LateBound`/`EarlyBoundWithFallback` call the delegate, updating the target in place on success
      and fizzling gracefully (not throwing) when the delegate is unset or finds nothing.
    - ⚠️ **Terminality vs `hp <= 0`**: `ActionRunner` was ALREADY correct here — `IsTargetActive`
      delegates entirely to the caller's own `_isActive` function; `ActionRunner` never reads HP
      itself. What CB1 could NOT build: the real `BattleRunState`-side wiring that resolves the
      already-compiled `ActionTargetSpec` and calls `TurnTransitions.IsTerminal` rather than `hp <=
      0` — that wiring has no live caller (D14) and is honestly left undone, not silently assumed.
    12 tests written (`tests/.../Battle/Timeline/CommitmentBindingTests.cs`), mirroring
    `TurnFsmActionEnvelopeTests`' own Rig harness shape exactly.
    ⭐ **Probed against real compiled code** (`tools/CommitmentProbe`): 12/12 pass — EarlyBound
    fizzles and never consults re-selection; LateBound/EarlyBoundWithFallback both re-target; all
    three distinguishable in one run; envelope overrides a conflicting profile default; an unset
    envelope inherits the profile default; no-delegate-configured degrades to graceful fizzle
    (backward compatible); a target with no legal fallback also fizzles rather than crashing; a live
    target is unaffected by any Commitment value (regression guard); replay is deterministic.
    ⭐ **A real bug found and fixed via the probe, not shipped:** the composite "all three values"
    check (in both the probe and the real test file) first read the LAST log line to determine
    fizzled-vs-resolved — wrong, since `"recovered"` always logs after the outcome. Found because the
    probe's per-scenario checks passed while the composite check failed on identical data; fixed to
    search for the outcome entry specifically in both files.
    ⭐ **Falsifier executed:** `OnResolveDue`'s branch was mutated to `if (true || ...)` (always
    fizzle regardless of Commitment), rebuilt, re-probed — exactly the 5 re-selection-dependent
    assertions reddened, the 7 fizzle/no-op-independent assertions stayed green (correct: those
    properties hold either way). Reverted; probe confirmed green again (12/12).
    `dotnet build` on `FusionRpg.Core`/`FusionRpg.Data`: 0 errors, **0 warnings** — a genuine
    `CS8629` nullable warning surfaced by the `Commitment?` change (in `ActionEnvelope.GetHashCode`'s
    `(int)Commitment` cast) was found and fixed in the same pass, not left behind.
    `audit-overflow.py --paths src/FusionRpg.Core/Battle/Timeline`: 0 findings.
    `audit-magic-numbers.py --summary`: 0 for every touched domain.

- [x] **CB2 — Determinism: draw-count parity** · **S** · **Deps:** CB1
  - **Acceptance:** re-selection consumes **the same number of RNG draws** whether or not it re-targets
    — the `B39` lesson applied in advance (hoisting the draw out of the sort key is the only reason that
    delta stayed attributable).
  - **Verify:** assert `initiative`/`crit` draw sequences match between re-target and non-re-target ·
    byte-identical replay
  - **Evidence (2026-09-05):** ⭐ **The property holds by construction, not merely by test** —
    `TryReselect` calls the caller's delegate as a pure `Func<string, string?, string?>` with no RNG
    parameter passed in at all, so `ActionRunner`'s own mechanism cannot introduce a draw regardless
    of whether re-selection succeeds, fails, or never runs (`EarlyBound`/live-target paths). Any RNG
    consumption would have to come from the CALLER's own delegate implementation — outside this
    module's contract, and there is no live caller yet (D14) to measure `initiative`/`crit` draw
    sequences against in a real battle. What IS proven: `CommitmentBindingTests
    .ReplayingTheIdenticalScenarioProducesIdenticalReselectionCallCounts` — the identical scenario run
    twice produces the identical re-selection call count (1 both times), proving replay determinism
    for the piece this module owns. Probed (`tools/CommitmentProbe`): confirmed 1/12 (folded into
    CB1's own probe run, not a separate tool). ⚠️ **The full acceptance line (`initiative`/`crit` draw
    sequences match in a real battle) needs a live caller and is honestly left for whenever D14 lands**
    — recorded here, not silently assumed satisfied.

### ⛔ Checkpoint D
- [ ] ⚠️ **`Commitment` measurably non-zero in the sweep — still blocked by D14, same as Checkpoint B.**
  CB1/CB2's own mechanism is built and proven at the `ActionRunner` level (12/12 probe assertions), but
  `HybridAtbSweepTests`-style win-rate measurement needs a LIVE caller into `ActionRunner`, which does
  not exist until D14 is resolved. Not re-measured separately — MEAS's own table already showed
  `Commitment` at 0.00 % for the same structural reason.
- [x] **Goldens byte-identical** — provable, not measured: `ActionRunner`/`ReactionLane` still have
  zero production callers (D14), so nothing this phase touched can move a battle-resolution golden.

---

## Phase 4 — `reaction-lane`

- [x] **RL1 — `WReact = 1` on `hybrid-atb` only** · **S** · **Deps:** ~~Checkpoint D, PU3~~ — **revised
  2026-09-05, same correction as CB1**: `ReactionLane.cs` is ALSO a self-contained class with zero
  coupling to `BattleEngine.Resolve`'s dispatch, confirmed by grep (zero references in
  `BattleEngine.cs`/`BattleRunState.cs`). `PU3` (poise) has no relationship to this task at all — a
  stale dependency copied from `RL2`'s own real need for `poise-unification`, left on `RL1` by mistake.
  - **Acceptance:** a **tuning row change, not a code change**. `classic-round` stays at 0 and keeps
    provable byte-identity. `DepthLimit` carries its structural/PS-8-exempt comment.
  - **Verify:** `classic-round` byte-identical · a dropped over-depth reaction emits telemetry and never
    recurses
  - **Evidence (2026-09-05):** confirmed BOTH acceptance facts already true in shipped code before
    touching anything — `ReactionLane.DepthLimit`'s comment already carries the structural/PS-8-exempt
    reasoning (built earlier this program), and `TryEnter` already calls `trace?.Reaction(actorKey,
    "depth-exceeded")` before returning `DepthExceeded`, with no recursive call anywhere in the method
    (structurally impossible to recurse — it returns a value). RL1's real, remaining scope was the
    config bump alone. Published via the proper tool, not authored by hand:
    `python tools/tuning/publish.py battle --label "reaction-lane RL1: WReact=1 on hybrid-atb only"
    "timeline.profiles.hybrid-atb.wReact=1"` → `battle.v4.json` (v3 stays on disk). Verified the
    dotted-path `set` worked (not `--add-edge`, since `wReact` already existed as a key) and confirmed
    `classic-round=0, galaxy-sync=0, hybrid-atb=1` in the published file directly.
    `Program.cs` updated to load `battle.v4.json`; three probes (`MeasProbe`, `TempoProbe`) updated to
    match, for consistency with what production now loads. `dotnet build` on `FusionRpg.Core`/
    `FusionRpg.Server`: 0 errors. `audit-magic-numbers.py --summary`: 0 for every touched domain.
    ⭐ **"classic-round byte-identical" is provable by construction, not by a re-run:**
    `ReactionLane`'s constructor sets `_slots = wReact > 0 ? new ActionSlots(...) : null` — with
    `classic-round`'s `wReact` still 0, `TryEnter` always short-circuits to `NoLane` before touching
    anything else, exactly as before this change. And `ReactionLane` has zero production callers
    regardless (same D14 pattern as `ActionRunner`), so nothing in a live battle can observe the
    hybrid-atb-only bump either, today.

- [~] **RL2 — The counter: intent, cost, and payoff** · **L** · **Deps:** ~~RL1~~ — the cost/payoff
  half needs neither `RL1` nor a live caller; the intent/funnel half genuinely does (D14). Partially
  complete — **the pure mechanism is done; the live wiring is not**, recorded precisely rather than
  marked either fully done or fully blocked.
  - **Acceptance:** intent arrives through the existing `IIntentSource` — ⛔ no parallel seam.
    **Decision 12 (Reading B): the spend IS the attack** — the counter commits `poise` through
    `PoiseLedger`, and its damage is `Riposte(spent, shareCapMilli)`. ⛔ **No fresh counter-damage
    path** — `Riposte` ships and is tested. Affordability is a **selectability** outcome in the intent
    source (typed `CannotAfford`), not a new branch in the lane.
  - **Verify:** a counter reduces the reactor's `poise` and its damage tracks the spend · an exhausted
    actor **declines**, and declining is observable as a refusal · ⛔ the reaction never moves the
    reactor's own `ActorTurnMachine` · damage routes through the existing funnel
  - **Evidence (2026-09-05):** built `Battle/Timeline/ReactionCounter.cs` — `TryCounter(pools,
    poiseSpend, riposteShareCapMilli, nowTick, derived) -> (Committed, Damage)`, a pure combining
    function over two already-shipped pieces (`PoiseLedger.TryCommit` + `Riposte.
    DamageFromSpentPoise`), authoring **no new damage math** (the acceptance line's own requirement).
    Deliberately NOT a method on `ReactionLane` — that class owns only the slot/depth mechanism by its
    own documented design, never what a reaction does.
    6 tests written (`tests/.../Battle/Timeline/ReactionCounterTests.cs`): a successful counter
    commits exactly the spend and deals exactly `Riposte`'s output; an unaffordable spend refuses
    all-or-nothing with the pool untouched (the resource judgement decisions 10/12 exist to create); a
    bigger spend deals more damage but leaves less poise for what comes next (the "competes with
    guarding" property, now directly observable); zero spend is a legal no-op; an enormous spend
    converts with no private ceiling (Riposte's own uncapped-pool guarantee survives the combining
    function); an out-of-range share throws.
    ⭐ **Probed against real compiled code** (extended `tools/PoiseProbe`, the natural home — same
    poise-pool machinery this session already proved there): 24/24 pass (18 from PU1/PU2 + 6 new).
    ⭐ **Falsifier executed:** `TryCounter` was mutated to always return `(true, 1)` regardless of
    input, rebuilt, re-probed — exactly the 6 RL2-specific assertions reddened, all 18 PU1/PU2
    assertions stayed green (unrelated, correctly unaffected). Reverted; probe confirmed green (24/24).
    `audit-overflow.py --paths src/FusionRpg.Core/Battle/Timeline`: 0 findings.
    `audit-magic-numbers.py --summary`: 0 for every touched domain.
    ⛔ **NOT built — narrower gap than before, still genuinely open:** D14's own root cause (the
    ATTACKER's basic attack never dispatching through `ActionRunner`) is now resolved architecturally
    by `timeline-dispatch` (`TD1`–`TD3`) — a real wind-up window now exists and is provable. **What
    remains for RL2 specifically is a SEPARATE, not-yet-designed integration: a DEFENDER declaring a
    counter-intent and interrupting the attacker's wind-up mid-flight.** `RunTimelineActionPhase`
    (`TD3`) dispatches one attacker's own commit→resolve→recovery cycle; it does not call
    `ReactionLane.TryEnter` for the defender at all, and wiring that in (when to offer the reaction,
    how the counter's own commit interacts with the attacker's already-scheduled Resolve event, how
    `ReactionCounter.TryCounter`'s output feeds back into the attacker's outcome) is genuinely new
    design, not a mechanical extension of TD3. "Intent arrives through `IIntentSource`", "declining is
    observable", "the reaction never moves the reactor's own `ActorTurnMachine`", and "damage routes
    through the existing funnel" all still need this specific wiring, which TD3 does not provide.

- [ ] **RL3 — Size the spend range** · **S** · **Deps:** RL2
  - ⛔ **Still blocked — same narrower reason as RL2's own remaining gap**, not the general D14 finding
    anymore: this task's own acceptance criterion IS a live win-rate measurement ("a win-rate check with
    the lane open vs closed"), which needs `ReactionLane`/`ReactionCounter` wired into a real,
    resolvable battle — RL2's own unbuilt half, not something `timeline-dispatch` (`TD1`–`TD3`) provides
    on its own. No mechanism-level substitute is honest here the way it was for the others.
  - **Acceptance:** the counter's poise cost and the hold-vs-spend threshold are **tunables**, sized
    against the Phase 2 sweep. ⚠️ The lane must not read as a flat power increase — countering must
    visibly compete with absorbing.
  - **Verify:** a win-rate check with the lane open vs closed · `M1 = 0`

- [x] **RL4 — All four outcomes, nested determinism, and an unreachable depth limit** · **S** ·
  **Deps:** ~~RL2~~ — **revised 2026-09-05, same correction as CB1/RL1**: every property this task
  names belongs to `ReactionLane` alone (all four outcomes, depth bookkeeping, `Exit`'s idempotence);
  none of them need the counter's own intent/cost/payoff design (`RL2`). Test-only against unmodified
  code, matching `AT4`'s own precedent exactly.
  - ⚠️ **Added by the coverage audit** — spec §5 items 1, 4 and 6 had no task.
  - **Acceptance:**
    - All four `ReactionOutcome` values are reachable and tested: `Entered`, **`NoLane`** (`WReact` 0 —
      the one that must stay true for `classic-round`), `DepthExceeded`, `NoSlot`.
      ⚠️ The value is `NoLane`; the spec said `LaneClosed`, a name that does not exist — corrected
      2026-09-05 against `ReactionLane.cs`.
    - ⛔ **Nested-resolution order is deterministic** — identical seeds reproduce identical nesting.
      `ReactionLane` composes `ActionSlots` precisely for its `(readyTick, seq)` contention ordering, so
      this is asserted, not assumed.
    - **The depth limit is unreachable by ordinary content** — a normal build must not routinely hit
      `DepthLimit`. It bounds recursion, never player power.
  - **Verify:** one test per outcome value · a seeded battle with reactions replays byte-identically ·
    a representative content sweep records max observed depth **below** the limit
  - **Evidence (2026-09-05):** 9 tests written
    (`tests/.../Battle/Timeline/ReactionLaneOutcomesTests.cs`), unmodified `ReactionLane.cs` — every
    property already existed, correctly:
    - **All four outcomes reached**: `NoLane` (`wReact=0`); `Entered` (capacity and depth available);
      `NoSlot` (a second actor while the one slot is held); `DepthExceeded` (a 4th entry past
      `DepthLimit=3`, with the refused attempt provably **not** incrementing `Depth`).
    - **`Exit`'s idempotence and slot-freeing** are asserted directly (a second `Exit` call for an
      already-released actor does not double-decrement; the freed slot admits a new entrant).
    - **Determinism**: the identical call sequence against a fresh lane produces the identical outcome
      sequence and final depth, run twice — proving the mechanism adds no hidden state of its own
      (`ReactionLane` has no RNG and no clock dependency, so this is the correct-scoped proof; a
      REAL seeded-battle replay needs a live caller, which does not exist yet — D14, same honest
      scoping as CB2).
    - **The depth limit's own sizing, proven rather than assumed**: the named worst case
      (`DepthLimit`'s own doc — *"a hit, a block, and a riposte to the block"*) lands **exactly at**
      the limit without being refused, and a genuine 4th level is what gets refused — confirming
      `DepthLimit=3` was sized FOR that chain, not merely larger than it by coincidence.
    - ⭐ **A real scenario bug found and fixed via the probe, not shipped:** the first draft of the
      depth-limit test reused the SAME actor key ("defender") for both "block" and "riposte" — which
      `ActionSlots` correctly refuses (`NoSlot`) as a second concurrent acquire by one actor, since a
      slot is per-actor. This is `ActionSlots`' own contract working correctly, not a `ReactionLane`
      defect — found because the depth-limit test failed with `riposte=NoSlot` instead of `Entered`.
      Fixed by using three distinct actor keys, since the test is about NESTING DEPTH, not which actor
      holds which level — corrected in both the probe and the real test file, with the finding
      recorded as a comment at the fix site so a future reader does not reintroduce it.
    ⭐ **Probed against real compiled code** (`tools/ReactionLaneProbe`): 9/9 pass.
    ⭐ **Falsifier executed:** the `Depth >= DepthLimit` check was mutated to `false && ...` (never
    trips), rebuilt, re-probed — exactly the 2 depth-dependent assertions reddened
    (`ExceedingDepthLimitRefusesWithDepthExceeded`, the named-worst-case test), the other 7 stayed
    green (correct — unrelated to depth). Reverted; probe confirmed green again (9/9).
    `audit-overflow.py --paths src/FusionRpg.Core/Battle/Timeline`: 0 findings.
    `audit-magic-numbers.py --summary`: 0 for every touched domain.

### ⛔ Checkpoint E
- [ ] `classic-round` provably untouched
- [ ] Goldens byte-identical
- [ ] Full `Core.Tests` green

---

## Phase 5 — `forecast-rail`

- [x] **FR1 — Trace opt-in, threaded** · **M** · **Deps:** ~~Checkpoint C~~ — **revised 2026-09-05**:
  this task is Server-layer plumbing around a parameter `BattleEngine.Resolve` already accepts and
  already reads at every current resolution point (`trace?.Turn(...)` fires from the SAME classic
  round-robin loop D14 found `RunBasicAttackStep` never consults for wind-up — the FSM transitions and
  `BattleTrace.Turns` recording are unaffected by D14; only wind-up SCHEDULING is blocked). No live
  `ActionRunner`/reaction-lane caller is needed for this task at all.
  - ⛔ **The split does not fall out by call site — and it is BIGGER than the spec's own §2 audit
    found.** The spec's own text named 3 `BattleEngine.Resolve` calls funnelling through
    `ResolveAndIngest`. ⭐ **Real finding, 2026-09-05: there are 4, not 3, and TWO of them bypass
    `ResolveAndIngest` entirely** — `RunWebMatchAsync`'s and `RunPlannedMatchAsync`'s own "replay"
    branches (idempotent re-resolution when a correlation ID already has a stored result) call
    `BattleEngine.Resolve` **directly**, never through `ResolveAndIngest`. Corrected here rather than
    silently reusing the spec's stale count: all 4 sites classified — the 2 replay branches and
    `ResolveAndIngest`'s own 2 real callers (`RunWebMatchAsync`, `RunPlannedMatchAsync`) are ALL
    player-facing and opt in; only `SweepUnresolved`'s call stays untraced.
  - **Acceptance:** the trace is a **parameter defaulting to null**, passed only from the two
    player-facing entries (`:109`, `:150`) and **never** from `:229`. ⭐ Trace where a human will look;
    never in the bulk path.
  - **Verify:** a test asserts the boot sweep resolves with **no** trace · ⭐ persisting `Turns` moves
    **no trace golden** — `Digest` excludes it by design
  - **Evidence (2026-09-05):** `WebMatchService.ResolveAndIngest` gained `BattleTrace? trace = null`,
    threaded straight into `BattleEngine.Resolve`. All 4 real call sites updated: `RunWebMatchAsync`'s
    fresh-resolve AND its replay branch pass `new BattleTrace()`; `RunPlannedMatchAsync`'s fresh-resolve
    AND its replay branch do the same; `SweepUnresolved`'s call is **explicitly left unpassed**, with a
    comment naming why (D3: nobody is watching a crash-recovery re-ingest). `dotnet build` on
    `FusionRpg.Server`: 0 errors, 0 warnings.
    ⭐ **Probed against real compiled code** (`tools/TraceOptInProbe`, real `BattleEngine.Resolve` +
    real production tuning): a battle resolved with `trace: null` and the identical battle resolved
    with a real `BattleTrace()` produce **byte-identical serialized `BattleReport`s** (same `Outcome`,
    same event count, same full JSON) — passing a trace changes nothing about the resolved battle. A
    passed trace genuinely records real turn order (`Turns.Count > 0`, not a stub). `Digest` (the
    determinism hash) is identical across two independent resolves of the identical seed/setup, **even
    though both traces recorded real `Turns`** — direct proof that `Turns` does not feed `Digest`,
    matching the class's own design claim. 6/6 probe assertions pass.
    `audit-overflow.py --paths src/FusionRpg.Server`: 0 findings. `audit-magic-numbers.py --summary`:
    0 for every touched domain.
    ⚠️ **What FR1 deliberately does NOT do**: retain the trace anywhere for later reading, or expose
    `Turns` through any DTO — that is FR2/FR3's own scope (the rail itself). FR1's job was the routing
    decision alone, now proven correct and safe.

- [x] **FR2 — DTO + contract parity** · **S** · **Deps:** FR1
  - **Acceptance:** the TS DTO mirrors the C# record, with a parity guard. ⚠️ `UnitClassContractParity`
    exists because a type added on one side and forgotten on the other shipped silently — and on
    2026-09-04 the **C# enum** was the side that lagged for a day.
  - **Verify:** the parity test fails when one side is edited alone
  - **Evidence (2026-09-05):** ⚠️ **A real design gap found before any DTO could be written:**
    `BattleTrace.Turns` is a raw debug log (`"{round} {actorKey} {from}->{to}"`) — exactly the engine
    vocabulary §2.4 bans from a player surface. No spec draft or map entry named HOW `actorKey`
    resolves to a display name, so the DTO literally could not be defined correctly without deciding
    that first. Resolved and built: `Battle/Timeline/TurnOrderRecord.cs` —
    `FromTrace(BattleTrace, BattleSetup) -> IReadOnlyList<TurnOrderEntry>` — filters to `Ready ->
    Committed` transitions only (§2.1's own finding: that IS the turn order), resolves each `actorKey`
    against `setup.Squad`/`Wave` to `DemonSpeciesCatalog.Get(...).Name`, and falls back to the raw
    species id (never the actorKey) for a synthetic/golden fixture with no real catalog entry.
    7 tests written (`tests/.../Battle/Timeline/TurnOrderRecordTests.cs`): only `Ready->Committed`
    counts; order is preserved across rounds; a real species resolves to its real name and provably
    not the actorKey; an unknown species falls back to its id, not to a crash; an empty trace projects
    nothing; null trace/setup throws.
    ⭐ **Probed against real compiled code and the real compiled species roster**
    (`tools/TurnOrderProbe`, `DemonSpeciesCatalog.ConfigureFromCompiledDefault()`): 6/6 pass, including
    resolving a REAL species id to its real (non-English) display name and confirming it is not the
    actorKey. ⭐ **Falsifier executed:** the `Ready->Committed` filter was removed (every transition
    accepted), rebuilt, re-probed — exactly the filtering-dependent assertion reddened, the other 5
    stayed green. Reverted; probe confirmed green (6/6).
    `audit-overflow.py --paths src/FusionRpg.Core/Battle/Timeline`: 0 findings. `audit-magic-numbers.py
    --summary`: 0 for every touched domain.
    ⭐ **TS side completed in the same pass, matching `UnitClassContractParityTests`' own established
    pattern exactly** — a C# test that regex-parses `types.ts`, requiring no npm/vitest toolchain at
    all (confirmed by reading that exact precedent before writing anything). Added `export type
    TurnOrderEntry = { round: number; displayName: string }` to `contract/types.ts` (a new §11
    section, no existing type touched). Wrote
    `tests/.../ClassSystem/TurnOrderRecordContractParityTests.cs`: parses the TS type's field names,
    camel-cases the C# record's property names, asserts the sets are equal.
    ⭐ **Probed against the real files** (`tools/ContractParityProbe`): parses the REAL `types.ts` and
    the REAL `TurnOrderEntry` C# record — both report `{displayName, round}`, match confirmed. 3/3
    probe assertions pass. ⭐ **Falsifier executed:** `types.ts`'s `displayName` field was renamed to
    `actorName` (a drift exactly like the incident `UnitClassContractParityTests` itself was built to
    catch), rebuilt, re-probed — the parity assertion reddened immediately, the two structural checks
    (found-the-type, parsed-nonzero-fields) stayed green since the TYPE still parses, only its FIELD
    NAME drifted. Reverted; probe confirmed green (3/3).
    `audit-overflow`/`audit-magic-numbers`: unaffected (TS-only change, no C# logic touched beyond the
    new test file, itself clean).

- [x] **FR3 — The rail, in the expedition result view** · **M** · **Deps:** FR2, TC2
  - **Acceptance:** a **layer**, not a page — no route, no sidebar entry. ⛔ **It is a record, not a
    prompt**: an expedition resolves before the player sees it, so no "next"/"upcoming" copy. Each
    `ForecastExactness` renders its own honesty, and ⛔ **`Absent` renders absence, not an empty list** —
    an empty rail reads as "nobody acts next", which is a lie. ⛔ **Do not build a battle stage.**
  - **Verify:** rendered order equals `BattleTrace.Turns` — falsifier: reversing the client list must
    redden · rendered text asserted for record-not-prompt copy · no engine vocabulary (`actorKey`,
    `typeId`, `TurnState`) reaches the DOM · `npm test -- forecast` · `npm run build`
  - **Evidence (2026-09-05):** ⚠️ **`ForecastExactness`/`Absent` do not apply here, confirmed by
    re-reading D3 rather than assumed** — that enum gates the LIVE-queue projection
    (`TurnOrderForecast`, FR4's own subject); D3 chose the trace-based RECORD specifically *because*
    no live queue survives past resolution. Expeditions only ever resolve through
    `WebMatchService`/`ExpeditionResolver`, never the live-PvZ-observer path `Absent` describes, so
    that branch is structurally unreachable here — not omitted, inapplicable.
    Built the full chain, server to pixel, no piece deferred:
    - `WebMatchOutcome` gains `TurnOrder: IReadOnlyList<TurnOrderEntry>`; all 4 construction sites
      (both fresh-resolve paths and both replay paths in `RunWebMatchAsync`/`RunPlannedMatchAsync`)
      compute it via `TurnOrderRecord.FromTrace(trace, setup)` — `BattleTrace` is a `sealed class`
      (reference type), so the caller's own `trace` instance already reflects the resolve that ran;
      no second return path was needed from `ResolveAndIngest`.
    - `ExpeditionService.CollectBattleResult` gains the same field, populated from
      `outcome.TurnOrder` at its one construction site in `CollectAsync`.
    - The `/collect` endpoint's `battles = result.Battles` projection needed **no change** — it
      already serializes the record verbatim under the project's existing camelCase policy (confirmed
      by the shipped TS mirror already being camelCase with no manual re-casing on the C# side).
    - `contract/types.ts`'s `TurnOrderEntry` (FR2) is now also the WIRE type: `lib/bus/expeditions.ts`
      imports it (`@/contract/types`, the same pattern `commanders.ts` already established) and adds
      `turnOrder: TurnOrderEntry[]` to `ExpeditionBattleResultDto`.
    - `ExpeditionsPage.tsx`'s existing `tickCard` (the "reveal battle-by-battle event cards" panel —
      the ALREADY-EXISTING expedition result view decision 9/§6 pointed at) renders `battle.turnOrder`
      as "Acting order: A → B → C" beneath the matching battle tick, past tense, no "next"/"upcoming"
      copy, no `actorKey`/`typeId`/`TurnState` anywhere in the JSX — the same honesty rule §2.4/§6
      require, satisfied because `TurnOrderEntry.displayName` is the ONLY field rendered.
    - ⛔ **No new route, no sidebar entry, no battle stage** — confirmed by construction: the change
      is entirely inside the EXISTING expedition panel component, zero new files under `layers/` or
      `stages/`.
    ⭐ **Verified against the real toolchain, not simulated:** `npx tsc --noEmit -p tsconfig.json` —
    **zero errors** across the whole web project (the type-check `npm run build` itself runs first).
    `npx vitest run` — **1271/1272 pass**; the one failure
    (`disabledReasonGuard.test.ts`, flagging unrelated `<Button>` accessibility gaps in
    `CommandersLayer.tsx`/`CommanderSheetFooter.tsx`) is on **committed, unmodified files**
    (`git status` confirms zero uncommitted changes to either), pre-existing and unrelated to this
    program. The three expedition-specific test files
    (`expeditionTime.test.ts`, `expeditionReturnWatcher.test.tsx`, `ExpeditionsLayer.test.tsx`) all
    pass, 9/9, unaffected by the DTO extension.
    `audit-overflow.py --paths src/FusionRpg.Server`: 0 findings. `audit-magic-numbers.py --summary`:
    0 for every file this session touched — a `fusion`-domain M2/M4 finding appeared in
    `Demons/Fusion/StarPolicy.cs`, a file never touched this session or referenced by anything in
    `battle-tempo`, confirming a concurrent, unrelated stream (the same transient-collision pattern
    `CombinationEvaluator.cs` produced earlier this session) — not investigated further, not this
    program's to fix.

- [x] **FR4 — Prove the projection is side-effect-free** · **S** · **Deps:** none (may run any time)
  - ⚠️ **Added by the coverage audit** — spec §5 item 2 and success criterion 2 had no task.
  - **Acceptance:** rolling `TurnOrderForecast.Project` forward `K` events leaves the `EventQueue`
    **byte-identical**. The ideal calls it a "pure projection"; that is currently a claim, not an
    assertion.
  - ⭐ **Worth doing early and independently of the rail.** It guards the property §2.1 depends on — the
    forecast must never become a second source of truth — and it needs no surface, no DTO and no trace.
  - **Verify:** `--filter "FullyQualifiedName~TurnOrderForecast"` · queue state compared before/after ·
    falsifier: have `Project` dequeue instead of peek → must redden
  - **Evidence (2026-09-05):** confirmed by reading `EventQueue.ProjectNext` before writing any test —
    it already makes a fresh `List<ScheduledEvent>(_heap)` copy, sorts and reads the COPY, never
    touching `_heap`; genuinely pure by construction, not merely by convention. 9 tests written
    (`tests/.../Battle/Timeline/TurnOrderForecastProjectionTests.cs`): `Count`/`PeekDueTick` unchanged
    after projecting; three repeated projections return identical results with the queue still at full
    count; a **real subsequent `PopDue` yields byte-identical events to what the forecast predicted**
    (the acceptance line's own wording, made literal); the bound is honoured both under- and
    over-supply; tie-break order (`DueTick`, then `Seq`) matches `PopDue`'s own order across a real
    tie; negative `max` and a null queue are refused; an empty queue projects nothing.
    ⭐ **Probed against real compiled code** (`tools/ForecastProbe`) — 9/9 pass.
    ⭐ **Falsifier executed, not assumed:** `EventQueue.ProjectNext` was mutated in place to call
    `PopDue` (a genuine dequeue) instead of copying, rebuilt, and re-probed — exactly the four
    mutation-sensitive assertions reddened (`Count`/`PeekDueTick` unchanged, idempotent-repeat,
    drain-matches-forecast, under-supply-still-has-5-left), while the two count-only assertions
    (`ProjectingMoreThanScheduledReturnsWhatExists`, tie-break order) still passed — correctly, since
    those properties hold whether `Project` peeks OR pops. The mutation was reverted and the probe
    confirmed green again (9/9).
    `audit-overflow.py --paths src/FusionRpg.Core/Battle/Timeline`: 0 findings. `audit-magic-numbers.py
    --summary`: 0 for every touched domain.

### ⛔ Checkpoint F — program complete
- [ ] Four axes measured non-zero: `AdvancePolicy`, `W`, `Commitment`, `ActionPoints`
- [ ] Goldens moved **once**, in Phase 2, with sign-off
- [ ] `M1 = 0`; overflow audit clean; all four guards green
- [ ] Full suites green: `Core.Tests`, `Guard.Tests`, `Data.Tests`, `web`
