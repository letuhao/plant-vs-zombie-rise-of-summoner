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

- [ ] **AT1 — `action-timing.v1.json` + a pure parser** · **M** · **Deps:** none
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

- [ ] **AT2 — Derive the envelope at catalog build from `qPowerMilli`** · **L** · **Deps:** AT1
  - Spec §2.2, §2.2a, §2.2b · ⛔ **No seeder change** (D2 — the Python seeder cannot compute power)
  - **Acceptance:**
    - `windupTicks = min(cap, coefficient × qPowerMilli / 1000)`; recovery the same scale, smaller
      coefficient. ⚠️ **Widen before multiplying; divide by 1000 last, exactly once.**
    - The cap is **relative to `roundDurationMs`**, a configurable soft cap — never an absolute literal,
      never a silent clamp hiding a mis-tuned coefficient.
    - `cooldownTicks` reads the shipped **`cdMulti`** from `action-rungs.v2.json` × category base.
      ⛔ No second cooldown curve.
    - `timeCostTicks` from category; `cooldownClass`/`Key` from category.
  - **Verify:** ⭐ wind-up correlates with payoff **both ways** — higher power at the same rung winds up
    longer, and rung 10 longer than rung 1, asserted against the **real** rung table · cooldown equals
    `cdMulti[10] ×` base · round trip store → `ActionCompiler` → `ActionEnvelope` on a **real committed
    row** · overflow throws rather than wraps
  - **Files:** `RpgStore.ActionCatalog.cs`, `ActionCompiler.cs`, `ActionTimingTests.cs`

- [ ] **AT3 — The basic attack's felt beat** · **S** · **Deps:** AT1
  - **Acceptance:** `BasicAttack.BasicAttackEnvelope` takes `WindupTicks`/`RecoveryTicks` **from
    tuning**. ⛔ **Exempt from the formula** — it has no rung and no seeded power, and keeping it out is
    what stops the token drifting when the coefficient is tuned. Decision 11: a **meaningful fraction of
    the round**, not the minimum that unlocks the knobs.
  - **Verify:** the envelope is non-zero from tuning · falsifier: zero the tuning value → the
    contention test must redden

- [ ] **AT4 — Multi-hit spends axis B `sequence`** · **S** · **Deps:** AT2
  - **Acceptance:** a rolled `resolveOffsets` longer than 1 is **refused** by `StructureBudgetGuard`
    below rung 7 and accepted and counted at rung ≥ 7. Default stays the shared single-resolve `[0]`.
    ⛔ No new axis invented — this spends an existing budgeted one.
  - **Verify:** both sides of the rung-7 boundary asserted

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

- [ ] **MEAS — Staged sweep: size each axis SEPARATELY, before landing** · **M** · **Deps:** AT2, AT3, AT4, TC2
  - ⛔ **The one chance at attribution.** The joint re-bless cannot separate the two deltas; this is the
    `B34` shape applied in advance.
  - **Acceptance:** three measured win-rate deltas recorded — wind-up alone, tempo alone, both together
    — plus the ⭐ headline: **`W` and `Commitment` stop measuring 0.00 %** in `HybridAtbSweepTests`'
    staged attribution. `TheFinalStageIsTheShippedProfile` still holds.
  - **Verify:** `--filter "FullyQualifiedName~HybridAtbSweep"` · deltas written into this file as
    evidence · **predict the golden movement in writing before Phase 2 runs it**

### ⛔ Checkpoint B — measured, predicted, not yet landed
- [ ] Three attribution numbers recorded
- [ ] `W` and `Commitment` proven non-zero — **the program's premise, verified**
- [ ] `classic-round` still contains `hybrid-atb` (pinning the knobs reproduces round-robin)
- [ ] Predicted golden movement written down **before** the re-bless
- [ ] `M1 = 0`; guards green

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

- [ ] **CB1 — Honour `Commitment` at resolve** · **M** · **Deps:** Checkpoint C
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

- [ ] **CB2 — Determinism: draw-count parity** · **S** · **Deps:** CB1
  - **Acceptance:** re-selection consumes **the same number of RNG draws** whether or not it re-targets
    — the `B39` lesson applied in advance (hoisting the draw out of the sort key is the only reason that
    delta stayed attributable).
  - **Verify:** assert `initiative`/`crit` draw sequences match between re-target and non-re-target ·
    byte-identical replay

### ⛔ Checkpoint D
- [ ] `Commitment` measurably non-zero in the sweep
- [ ] **Goldens byte-identical** — measure, don't assume

---

## Phase 4 — `reaction-lane`

- [ ] **RL1 — `WReact = 1` on `hybrid-atb` only** · **S** · **Deps:** Checkpoint D, PU3
  - **Acceptance:** a **tuning row change, not a code change**. `classic-round` stays at 0 and keeps
    provable byte-identity. `DepthLimit` carries its structural/PS-8-exempt comment.
  - **Verify:** `classic-round` byte-identical · a dropped over-depth reaction emits telemetry and never
    recurses

- [ ] **RL2 — The counter: intent, cost, and payoff** · **L** · **Deps:** RL1
  - **Acceptance:** intent arrives through the existing `IIntentSource` — ⛔ no parallel seam.
    **Decision 12 (Reading B): the spend IS the attack** — the counter commits `poise` through
    `PoiseLedger`, and its damage is `Riposte(spent, shareCapMilli)`. ⛔ **No fresh counter-damage
    path** — `Riposte` ships and is tested. Affordability is a **selectability** outcome in the intent
    source (typed `CannotAfford`), not a new branch in the lane.
  - **Verify:** a counter reduces the reactor's `poise` and its damage tracks the spend · an exhausted
    actor **declines**, and declining is observable as a refusal · ⛔ the reaction never moves the
    reactor's own `ActorTurnMachine` · damage routes through the existing funnel

- [ ] **RL3 — Size the spend range** · **S** · **Deps:** RL2
  - **Acceptance:** the counter's poise cost and the hold-vs-spend threshold are **tunables**, sized
    against the Phase 2 sweep. ⚠️ The lane must not read as a flat power increase — countering must
    visibly compete with absorbing.
  - **Verify:** a win-rate check with the lane open vs closed · `M1 = 0`

- [ ] **RL4 — All four outcomes, nested determinism, and an unreachable depth limit** · **S** · **Deps:** RL2
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

### ⛔ Checkpoint E
- [ ] `classic-round` provably untouched
- [ ] Goldens byte-identical
- [ ] Full `Core.Tests` green

---

## Phase 5 — `forecast-rail`

- [ ] **FR1 — Trace opt-in, threaded** · **M** · **Deps:** Checkpoint C
  - ⛔ **The split does not fall out by call site.** All three `BattleEngine.Resolve` calls funnel
    through `WebMatchService.ResolveAndIngest` (`:241`), and **`SweepUnresolved` calls it too** (`:229`).
  - **Acceptance:** the trace is a **parameter defaulting to null**, passed only from the two
    player-facing entries (`:109`, `:150`) and **never** from `:229`. ⭐ Trace where a human will look;
    never in the bulk path.
  - **Verify:** a test asserts the boot sweep resolves with **no** trace · ⭐ persisting `Turns` moves
    **no trace golden** — `Digest` excludes it by design

- [ ] **FR2 — DTO + contract parity** · **S** · **Deps:** FR1
  - **Acceptance:** the TS DTO mirrors the C# record, with a parity guard. ⚠️ `UnitClassContractParity`
    exists because a type added on one side and forgotten on the other shipped silently — and on
    2026-09-04 the **C# enum** was the side that lagged for a day.
  - **Verify:** the parity test fails when one side is edited alone

- [ ] **FR3 — The rail, in the expedition result view** · **M** · **Deps:** FR2, TC2
  - **Acceptance:** a **layer**, not a page — no route, no sidebar entry. ⛔ **It is a record, not a
    prompt**: an expedition resolves before the player sees it, so no "next"/"upcoming" copy. Each
    `ForecastExactness` renders its own honesty, and ⛔ **`Absent` renders absence, not an empty list** —
    an empty rail reads as "nobody acts next", which is a lie. ⛔ **Do not build a battle stage.**
  - **Verify:** rendered order equals `BattleTrace.Turns` — falsifier: reversing the client list must
    redden · rendered text asserted for record-not-prompt copy · no engine vocabulary (`actorKey`,
    `typeId`, `TurnState`) reaches the DOM · `npm test -- forecast` · `npm run build`

- [ ] **FR4 — Prove the projection is side-effect-free** · **S** · **Deps:** none (may run any time)
  - ⚠️ **Added by the coverage audit** — spec §5 item 2 and success criterion 2 had no task.
  - **Acceptance:** rolling `TurnOrderForecast.Project` forward `K` events leaves the `EventQueue`
    **byte-identical**. The ideal calls it a "pure projection"; that is currently a claim, not an
    assertion.
  - ⭐ **Worth doing early and independently of the rail.** It guards the property §2.1 depends on — the
    forecast must never become a second source of truth — and it needs no surface, no DTO and no trace.
  - **Verify:** `--filter "FullyQualifiedName~TurnOrderForecast"` · queue state compared before/after ·
    falsifier: have `Project` dequeue instead of peek → must redden

### ⛔ Checkpoint F — program complete
- [ ] Four axes measured non-zero: `AdvancePolicy`, `W`, `Commitment`, `ActionPoints`
- [ ] Goldens moved **once**, in Phase 2, with sign-off
- [ ] `M1 = 0`; overflow audit clean; all four guards green
- [ ] Full suites green: `Core.Tests`, `Guard.Tests`, `Data.Tests`, `web`
