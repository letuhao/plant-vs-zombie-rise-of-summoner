# Tasks: combat unification + battle enrichment

Plan: [combat-unification-plan.md](combat-unification-plan.md) · Map: [../docs/architecture/combat-unification-map.md](../docs/architecture/combat-unification-map.md)
~~**Gate:** U9+ edits `Core/Battle` — blocked until the owner confirms the battle stream is finished.~~
**✅ LIFTED 2026-09-04.** Its condition passed on 2026-08-28 (the battle stream closed T5 and T9), and
its shape was wrong anyway — owner ruling 2026-09-03: *"i don't want to join the gate — if the gate
needs them, remove them."* Restated as dependencies, nothing is held: Wave H depended on nothing here,
Wave R on T9 (closed), `species-skills` on T5 + T19 (both closed). See the plan's own Standing gate
section, which was corrected first; **this header was missed in that pass and is corrected here.**

## Phase 1 — combat-resolver-core (no behavior change)

- [x] **Task U1: Docs unlock — decisions.md "Combat resolution SSOT" row**
  - Description: add the decisions row (resolver + pipeline SSOT, retirement contract, min-chip/profile policy, platform stamp); flip map + module spec statuses to building.
  - Acceptance: row references the map; no contradicting locked row.
  - Verify: doc read-through. Files: `decisions.md`, map. Scope: XS.
  - Dependencies: owner approval to start the build (Phases 1–2 only; U9 gate separate).

- [x] **Task U2: SeededRngCombatAdapter + determinism tests** *(placed in Core/Combat, not Core/Battle — stays clear of the gated folder)*
  - Description: `ICombatRng` adapter over an owned `SeededRng` stream; doc note on the `Next(1_000_000)` granularity change.
  - Acceptance: same seed/stream → same sequence, cross-checked vs SeededRng goldens; `SeededCombatRng` untouched.
  - Verify: `--filter FullyQualifiedName~Combat`. Files: adapter + tests. Scope: S.

- [x] **Task U3: Omni fallback (a stated contract change)** *(deviation, tighter than spec: `ElementPayload.Validate` stays strict — the empty-is-legal branch lives only in the two resolver entry points, so content/DTO validation is unchanged)*
  - Description: empty component list becomes legal — `ElementPayload.Validate` empty-path, `ElementHub` zero-bonus, calculator resolves omni halves (crit families included). Malformed payloads still throw.
  - Acceptance: neutral goldens 0.5/0.5/×1.5; invalid-weight-sum throw test green; overlay dispatcher path byte-identical (pass-through golden — fallback unreachable from dispatcher).
  - Verify: Core suite. Files: `OverlayCombatCalculator.cs`, `ElementPayload.cs`, `ElementHub.cs`, tests. Scope: M.

- [x] **Task U4: Min-chip floor as per-profile policy (owner decision 6)**
  - Description: `CombatProfiles` with `MinChipShareKPm` (battle/sim 50‰ min 1; overlay 0); calculator applies on landed hits only.
  - Acceptance: overlay profile byte-identical (full suite); battle-profile goldens (0-damage → chip; chip < base unaffected); heals/misses never floored.
  - Verify: Core suite. Files: `CombatProfiles.cs` (new), calculator, tests. Scope: S.

- [x] **Task U5: Contracts locked — draw consumption, crit bounds, ban/parity scaffolding** *(ban test self-arms on RulesetVersion 2 — pre-adoption it asserts the arming precondition)*
  - Description: goldens for RNG draw counts (hit 1 + crit 1; saturation consumes none; Force* skips — documented), crit-mult (1.0, 2.0) property, and the ban/parity harness (armed fully at U11).
  - Acceptance: all contracts golden'd; harness runs against overlay as baseline.
  - Verify: Core suite. Files: tests only. Scope: S.

### Checkpoint 1 — Resolver core
- [x] Core 1248/1248 (every pre-existing overlay golden byte-identical — zero modified); all four guards OK.

## Phase 2 — damage-apply-pipeline

- [x] **Task U6: Pipeline + IHpDeltaSink + packet-free gate overload** *(gate core takes nullable snapshots — provided = only source, null = legacy resolve delegate; `noteOverlayDamage` landed as an `onHpDamageApplied` callback so the pipeline stays free of event DTOs)*
  - Description: `DamageApplyPipeline.Apply(ptr, …, sink, noteOverlayDamage)` — pipeline-owned `entity:` prefixing, zero-guard in pipeline, funnel adapter sink; `ShieldGate` packet-free overload (pipeline snapshots are the only source; legacy packet path kept as wrapper). Naming discipline: single-target `ptr`, no `targetPtrs`, no writer-class names in comments.
  - Acceptance: pipeline units (partial/full absorb, heal bypass, zero-to-sink, null gate, hitCount forwarding); funnel-vs-direct sink parity.
  - Verify: Core suite + `guard-funnel-delta.ps1`. Files: `DamageApplyPipeline.cs` (new), `ShieldGate.cs`, tests. Scope: M.

- [x] **Task U7: Dispatcher delegates** *(via the zero-alloc `ApplyPacketToFunnel` entry — no closures on the drain hot path; cross-entry parity test locks it to the general entry)*
  - Description: `DispatchInstant` tail → pipeline (funnel sink, `noteOverlayDamage: true`, gate passthrough).
  - Acceptance: every dispatcher + shield-gate golden unchanged (byte-identity is the whole test).
  - Verify: Core suite + guards. Files: `CombatDamageDispatcher.cs`. Scope: S.

- [x] **Task U8: One-mutation-slot invariant + cross-sink parity suite** *(negative test included: mixed raw/prefixed keys demonstrably split the slot)*
  - Description: the tests that make key-space bugs impossible to ship silently: one actor + several pipeline deltas in a window → one funnel mutation slot; identical inputs through funnel sink vs direct sink → identical applied numbers.
  - Acceptance: both suites green; a deliberately mixed raw/prefixed enqueue fails the invariant (negative test).
  - Verify: Core suite. Files: tests. Scope: S.

### Checkpoint 2 — Apply pipeline
- [x] Core 1258/1258 — overlay byte-identical end-to-end (dispatcher delegation changed zero existing tests); shield suite green untouched; guards OK.

## Phase 3 — battle-adoption (⛔ GATED: owner confirms battle stream done)

- [x] **Task U9: Composer mapping table**
  - Description: drop `power.omni = Atk` (base moves to request); **keep** `defense.omni = Defense`; keep affinity adds; add crit-damage family baselines (0). Invert the one composer test line.
  - Acceptance: mapping table asserted by test; defense asserts stay green.
  - Verify: `--filter ~BattleStatComposer`. Files: `BattleStatComposer.cs`, its tests. Scope: S.

- [x] **Task U10: Baseline re-tune (owner decision 5 — rate-tested)** *(BaseAccuracy 220+26L / BaseDodge 26L → parity σ(2.2)=90.0% at every level; BaseCritRate 10L / BaseCritResist 10L+250 → σ(−2.5)=7.6%; +5-level hit σ(3.5)=97.1%; critical-hunter re-costed +100→+150 → ~27% crit)*
  - Description: re-express `BaseAccuracy/BaseDodge/BaseCritRate/BaseCritResist` in resolver-scale points; re-cost trait `ChannelMods` (sigmoid halves per-point value). Acceptance is computed, not sampled.
  - Acceptance: `Sigmoid(parityDelta(L))` ∈ [0.88, 0.92] and crit ∈ [0.05, 0.10] at L = 1/5/10/20; stated growth targets hold (e.g. hit vs −5 levels ≥ 0.97); trait re-cost table in the commit notes.
  - Verify: new rate tests. Files: `BattleModels.cs`, `TraitBattleCatalog.cs`, tests. Scope: M.

- [x] **Task U11: Engine resolver swap** *(ban test armed itself at RulesetVersion 2 and passes — retired symbols verified gone by reflection)*
  - Description: one `OverlayCombatRequest` per swing (base = Atk, primary element or omni fallback, adapter over `crit` stream, natural rolls only); retire `Hit*/Crit*` consts, `ShareMilli` (+ mirror test), variance + `damage`-stream draw; battle-profile chip floor; trait multipliers stay engine-side on resolver output.
  - Acceptance: parity test (swing ≡ direct resolver call); ban test green (retired symbols gone, no Force* in production); `RulesetVersion = 2`.
  - Verify: `--filter ~Battle` (goldens red until U14 re-bless — expected, listed). Files: `BattleEngine.cs`, `BattleModels.cs`, tests. Scope: M.

- [x] **Task U12: All deltas through the pipeline + shields in battle**
  - Description: battle-local `ShieldRuntime`+gate; **every** HP delta (attack, DoT pulse `hitCount=1` empty components, regenerator, immortal, soul-eater, guardian share) through the pipeline; round-end `Tick(round, 1000)`; `RemoveAll` on death/retreat; setup key validation rejects `entity:`/`0x`; guardian two-slice semantics; `DamageDealt` = resolver output, `ShieldAbsorbed` separate.
  - Acceptance: shield-in-battle E2E goldens (absorb vs traits, guardian two-slice, innate boss, regen rounds, flushes); one-slot invariant in battle; chip-grind golden (max-defense progresses).
  - Verify: `--filter ~Battle|~Shield`. Files: `BattleEngine.cs`, `BattleEffects.cs`, tests. Scope: M.

- [x] **Task U13: Innate seam, report vocabulary, platform stamp** *(stamp persisted end-to-end: `environment_stamp` column via the EnsureColumn migration idiom, logged at append, checked by the sweep guard)*
  - Description: `BattleActorSetup.InnateShield` (ms durations, direct Apply at setup); `BattleEventRec` optional fields + four `shield.*` kinds + deliberate emitter expansion (aggregate-per-round order golden); `BattleActorResult.ShieldAbsorbed`; `BattleReport` platform stamp (arch+runtime); `WebMatchService.SweepUnresolved` refuses stamp mismatch.
  - Acceptance: report goldens carry events + stamp; sweep-guard test; v1 rows still decodable.
  - Verify: `--filter ~Battle|~WebMatch`. Files: `BattleModels.cs`, `BattleReportEmitter.cs`, `WebMatchService.cs`, tests. Scope: M.

- [~] **Task U14: Golden re-baseline + expedition sweep — DONE except owner sign-off.** Re-blessed: 4 battle hashes (shape tests held WITHOUT seed re-selection: stomp Victory, wipe Defeat + coward retreat all survived the re-tune — the 90% target preserved the golden shapes), 4 expedition hashes (named serialization-shape churn; resolver byte-stability proven by its own determinism tests). Sweep report: `docs/research/combat-unification-v2-sweep.txt` — mirror-match symmetry 48–56% (engine balanced); bare-stat squad table shows content difficulty cliffs (0%/100%) from determinism, NOT an adoption regression (v1 baseline not reconstructible — battle code was never committed). **⛔ Awaiting owner sign-off on the sweep before this task closes.** ✅ **Checked, not assumed (2026-09-04): this gate is written into the PLAN, not appended by a session.** `combat-unification-plan.md` line 76 gives the mitigation for *"re-tune lands wrong feel"* as **"win-rate sweep with owner sign-off at U14"** — an audit-defined acceptance criterion. It is also the one kind of item a session cannot discharge for itself: a **balance judgement**, not a measurement.
  - ⚠️ **Reconciled 2026-09-04, because the ground moved under this report.** It was measured with expeditions and web matches on `classic-round`; **they now run `hybrid-atb`** (B36) with readiness ordering wired (B39). Its per-wave win rates and its mirror-match symmetry therefore describe a configuration production no longer runs.
  - ✅ **What is newly known, and it shrinks what the sign-off is deciding.** B34's staged sweep measures the whole `classic-round` → `hybrid-atb` move at **−1.67 %**, attributed to a single axis (the action-points economy) with every other axis measured at exactly **0.00 %** — including B39's readiness ordering, which is inert until content authors a `turn.speed`. So the delta this report's numbers would shift by is one named, measured figure rather than an open question. Table: [`_sweep-hybrid-atb.md`](../docs/research/battle/_sweep-hybrid-atb.md).
  - **What is still owed is a judgement, not a measurement:** whether a 89.58 % → 87.92 % squad win rate, and the content difficulty cliffs this report already named, are the balance the owner wants. **No further run would answer that** — which is why this stays open rather than being closed on evidence.
  - Description: re-bless battle hashes; re-select seeds for shape tests (retreat/immortal/loyal); re-verify WaveCD saturation under double math; re-bless expedition hashes (named shape-churn from `InnateShield`); run the seeded before/after **win-rate sweep** over the wave matrix.
  - Acceptance: every re-bless justified against a predicted delta; sweep report produced; **owner signs off the win-rate delta** before this task closes.
  - Verify: full Core suite green. Files: golden tests, expedition tests, sweep script/report. Scope: M.

### Checkpoint 3 — Battle adopted
- [x] Core **1303/1303**, Data 146, Guard 40; all four guards OK; injector + server build. Parity + armed ban green; shield determinism replay (shield spec §7) **closed** (byte-identical JSON, shields + guardian + coward under seed 12345); shields-in-battle E2E green (innate absorb+tally, break-after-aggregate ordering, 3-round ms→tick expiry, DoT absorption, guardian two-slice, prefix rejection, emitter forwarding + stamp).
- [x] **Post-build review fix pass** (owner-approved 2026-08-21, after `/review`). Both Criticals closed:
  - **Goldens were machine-bound** — `BattleReport.EnvironmentStamp` sat inside the hashed JSON, so the four blessed hashes encoded `X64/.NET 8.0.30`; CI or any teammate would have read a portability failure as a determinism break. Stamp excluded from hash input, hashes re-blessed once, `Goldens_do_not_depend_on_the_platform` added so it cannot regress. Predicted delta held: those two hash tests were the ONLY failures; every shape/rate/shield/expedition test stayed green with no seed re-selection.
  - **Sweep refusals were non-terminal** — refused rows kept `run_id NULL`, so they were re-listed every boot and (query is `ORDER BY id ASC LIMIT n`) enough of them would crowd out every newer row, silently killing crash recovery. New `rpg_web_match_log.sweep_refused` column + `MarkWebMatchSweepRefused`; both guards and the unreadable-setup branch now mark. Two Data tests, incl. a starvation regression.
  - Also: stamp recomposed to **arch / OS / runtime-major** (OS was missing — Windows-x64 and Linux-x64 collided on exactly the `Math.Exp` case the guard exists for; patch number was present and would have stranded matches on a routine `dotnet` upgrade); battle DoT sink now `Math.Round` like the overlay sink (was truncating, and a −0.6 pulse became 0 and skipped the shield gate entirely); `StatusRuntime.Tick` host iteration ordinal-sorted (report event order rested on `Dictionary` internals while battle advertises byte-identical replay); misplaced `EnsureColumn` moved below its own table's DDL; shield-kind identity round-trip removed; drain-order comments corrected (they said "lower drains first" against descending code).
  - **Two tests were passing for the wrong reason and are now rigorous.** `Dot_pulses_drain_shields_before_hp` asserted `>= 25` on a tally that attack absorption alone satisfied — proven by control run (352 absorbed with the DoT deleted vs 324 with it). Rewritten with an unreachable-dodge attacker so the shield's only input is the DoT: control absorbs exactly 0, DoT run exactly 100. That rewrite surfaced a real trap: **`"poison"` is registered `StatusKind.UnityCc`**, which `StatusRuntime.Tick` never pulses (it pulses `OverTime`/`Contagion` only) and `IsCcLocked` treats as a turn lock — so the original test's DoT never existed. Now uses `"wither"` (the overlay-authored `OverTime`/`PulseHp` DoT), which confirms battle DoT→shield absorption genuinely works at 100%. `First_swing_matches_a_direct_resolver_call` had an `Assert.True(true)` branch and inequality assertions; now a branch-free exact-equality lock (1 HP defender ⇒ the winner's tally IS the one swing).
- [x] **Owner sign-off on the win-rate sweep** (the one open item — see U14). — **owner-approved 2026-08-31.** U14 sweep signed off.

## Phase 4 — sim-adoption (parallel with Phase 3 after U8)

- [x] **Task U15: Sim routing + shield mount**
  - Description: `DamagePlant/DamageZombie` → pipeline (direct sink, `ScaleIncoming` before, gate mounted, `noteOverlayDamage: false`); sim-session `ShieldRuntime`; grant method; dump totals live.
  - Acceptance: sim E2E (grant → damage → absorbed remainder + dump totals); no-shield sim byte-identical.
  - Verify: `--filter ~Sim`. Files: `SimEngine.cs`, tests. Scope: M.

- [x] **Task U16: Sim HTTP surface** *(+ `scripts/probe-sim-shield.ps1` — the one-command owner demo)*
  - Description: `POST /api/sim/shield/grant` (ms durations); `/api/sim/state` exposes shield totals.
  - Acceptance: endpoint tests; **owner-visible demo: server-side shield probe with the game closed** (grant → damage → state shows absorb → web bar renders).
  - Verify: server tests + manual curl script. Files: `SimEndpoints.cs`, tests. Scope: S.

### Checkpoint 4 — Server-side probe
- [x] Offline E2E green (grant → absorb → break-remainder → state/dump totals; no-shield byte-identical; board reset clears); server builds; Core 1264/1264, Guard 40, Data 140; all four guards OK. Live demo: start the server, run `.\scripts\probe-sim-shield.ps1` — no game needed.
- [x] Five-axis review pass (18:04): 2 Important regressions found via Prove-It and fixed — U6's gate packet wrapper parsed payloads before the per-owner check (per-hit list allocation for unshielded targets whenever any shield existed; now zero-added-bytes, locked by a differential allocation test) and U15's sim sink clamped damage to MaxHp (overhealed sim entities lost HP to the clamp; damage now floor-0 only, heals clamp up). Also verified clean: `TryParse("omni")` rejects (no omni-shield double-count path), probe-script routes, pipeline prefix guard, chip floor edge at base 0 (min-1 is spec-intended). Core **1271/1271**.
- [x] /test gap pass (17:55): +5 locks — empty-DTO packets stay pass-through (never omni), typed-path chip floor (+ overlay-profile contrast), **natural-roll stream fingerprint golden** (the U14 harness seed — drift detector for draw consumption), gate packet-vs-components typed-absorption equivalence (lands on the shield spec's worked −192), sim `/state` shields JSON shape + empty case. Core **1269/1269**.

## Phase 5 — battle-enrichment (each wave elaborates its own todo at build start)

- [x] **Wave E1: On-hit status riders** — **DONE 2026-09-04, both clauses, shipped inert** — rider specs on traits; dedicated `riders` RNG stream; zero-rider battles byte-identical.
  - ### ✅ Evidence — **MECHANISM DONE 2026-09-04, shipped inert**
    - `TraitBattleDef.OnHitRiders` (a list of `BattleStatusSpec`, empty for every shipped trait),
      applied on a **landed** hit to the actor that was hit, with the attacker carried — unlike the t0
      initial statuses, which land attacker-less. That is the point of a rider: attacker potency meets
      defender resist.
    - **Dedicated `riders` RNG stream**, as the wave's own audit fix requires: the `status` stream is
      already contagion's, and sharing it would make every rider content change a full-battle
      butterfly. One roll per rider on that stream; the evaluator is then handed a scripted `0.0` so it
      cannot roll a second time and consume a draw belonging to contagion.
    - ⭐ **Riders live on the trait def, not on `BattleActorSetup` — and that was decided by
      measurement, not preference.** The spec offers both ("rider specs on setups/traits") and then
      settles it ("Trait-sourced riders come from `TraitBattleCatalog` rows"). I built the
      `BattleActorSetup` version first and **it moved all four expedition tier goldens**: a new
      property lands inside the serialized `BattleSetup` that `ExpeditionBattlePlan` hashes, so the
      hash moves for a purely structural reason. 35 battle goldens stayed green while only the
      expedition hash moved — the exact signature of serialization-shape churn, and the same hazard
      `WaveDef.Profile`'s own doc comment names. **The catalog row is not serialized and moves
      nothing: goldens 36/36.**
    - **Authorable end to end.** `BattleTuningLoader` parses `traits.<id>.onHitRiders`, reusing
      `BattleStatusSpec`'s field names and defaults (`periodMs` 1000, `grantChanceMilli` 1000), so a
      rider is authored exactly the way an initial status already is — no second vocabulary.
    - **Refusals, six of them:** a non-array, a non-object entry, a missing `statusId`, a chance above
      1000 or below 0, and a zero `periodMs` (which would pulse forever). The 0 and 1000 chance bounds
      are themselves legal — a probability's domain is closed, so refusing outside it is structural,
      not a PS-8 progression cap.
    - **Zero-rider byte-identity is structural, not lucky:** the apply loop never runs for an empty
      list, so the `riders` stream is never drawn from and no other stream is perturbed. Asserted
      directly (no shipped trait carries a rider) as well as via the goldens.
    - **Suite:** full Core **15 failed / 5579 passed** against the stable 14 — the single extra is the
      atom stream's in-flight `AtomCatalogSsotDriftTests`. Goldens **36/36**. Four boundary guards
      green. `audit-magic-numbers.py` **M1 = 0**; `audit-overflow.py` A1/A2 clean.
    - ### ✅ Second clause — typed DoTs — also **DONE 2026-09-04**
      - `StatusApplyInput.Element` → `StatusInstance.Element` (both nullable, defaulting null), and a
        single shared `StatusPulsePayload.For(instance)` that turns it into the pulse's component list.
      - ⭐ **One function, deliberately, because parity is the invariant.** Both sinks call it —
        `BattlePulseSink` (battle) and `StatusFunnelPulseSink` (overlay). The program states "both
        modes are element-neutral on DoTs **by parity**", and the cheapest way to keep two
        implementations agreeing is to hand them one rule rather than two copies of it.
      - **Byte-identical until content opts in:** a null element yields an empty component list, which
        is exactly what both sinks passed before E1. Goldens **36/36**; the Golden + Status filter is
        **301/301**.
      - Every element round-trips into the payload as a single full-weight component (6 elements
        asserted), and the apply input's default is asserted to be neutral — that default is what makes
        the byte-identity claim true rather than merely likely.
    - **Suite after both clauses:** full Core **16 failed / 5588 passed** against the stable 14. The
      two extras are both known and neither is this work: the atom stream's in-flight
      `AtomCatalogSsotDriftTests`, and `DemonQualityReportTests`, the parallel-load flake characterised
      under B29. Four guards green; `M1 = 0`; overflow A1/A2 clean.
- [x] ~~**Wave E2: Species skills (v4)** — `SkillCatalog`, deterministic selection, actions via resolver/pipeline/ShieldRuntime/StatusRuntime only; `skill.used` events; zero-skill invariant.~~ ⛔ **SUPERSEDED 2026-09-04 — moved to Phase 6 below.** Not rebased, **replaced**: all five pieces (`SkillDef`, rounds-based cooldown, action kind, targeting policy, `SkillCatalog`) shipped under other names, so building this as drafted would create a fifth content system. See [spec-species-skills.md](../docs/architecture/combat/spec-species-skills.md).
- [x] **Wave E3: Hybrid payloads** — **MECHANISM DONE 2026-09-04 (inert; dial is owner-gated)** — secondary element as weighted component; dual-type matchup tables golden.
  - ### ✅ Evidence — **MECHANISM DONE 2026-09-04, shipped inert; the balance value is owner's**
    - `HybridPayload.Build(primary, secondary, secondaryWeightMilli)` — extracted to its own file
      rather than left inside `BattleEngine`'s private actor state, so it is testable as the pure
      function it is (the same reason `KernelPurityScan` lives outside the tests that use it).
    - ⭐ **The weight is a TUNABLE (`battle.v{n}.json` `hybrid.secondaryWeightMilli`), not the
      hardcoded policy constant the map sketched — and that is a deliberate reading of this task's own
      wording, not a liberty.** The todo says *"policy constant … **ask-first to change**"*, so the
      value is owner-gated; shipping the mechanism at **0** lands everything that is not gated and
      leaves exactly the gated part open. Raising it is then a config edit, not a rebuild.
    - ⛔ **Why that matters more here than it did for the other inert mechanisms: this one MOVES
      GOLDENS when switched on.** `WaveCatalog.cs:115` and `WebMatchService.cs:297` both copy a
      species' real `ElementSecondary` onto wave demons, so a non-zero weight changes every expedition
      resolve — while the hand-built battle goldens, which set no secondary, would not move at all.
      **A predicted-delta write-up and a `RulesetVersion` bump are therefore required before the dial
      is raised**, and neither is required to land the mechanism.
    - **Byte-identical at 0, proven two ways:** an actor *with* a secondary produces the same
      single-component payload as one without (no zero-weight component ever reaches the resolver's
      component loop), and the shipped tuning is asserted to be 0. Goldens **36/36** unmoved.
    - **The mechanism proven when the dial is on:** at 300‰ the payload is `primary @ 0.7` +
      `secondary @ 0.3`, and across 1/250/500/999/1000 the two weights **always sum to exactly 1.0**.
    - **The bound is structural and refuses at both layers** — `HybridPayload` throws and the loader
      rejects outside 0..1000. Not a PS-8 progression cap: above 1000 the *primary* takes a negative
      weight, which is a nonsense payload rather than an aggressive balance choice.
    - **A secondary equal to the primary is not a hybrid** — the engine normalises that one line before
      calling, and the test pins it so the two cannot drift into disagreeing.
    - **Suite:** full Core **15 failed / 5543 passed** against the stable 14; the single extra is the
      atom stream's in-flight `AtomCatalogSsotDriftTests`. `audit-magic-numbers.py` **M1 = 0**,
      `audit-overflow.py` A1/A2 clean.
    - ⚠️ **Operational finding worth carrying:** B28's `derived-stats.v2.json` bump means the nine
      tools that load it **must be rebuilt**, or tests that shell out to them with `--no-build` fail
      against a stale binary. `ProveAptitudeJsonEmitTests` failed exactly this way and went green on a
      rebuild — a false regression that costs real time to diagnose if it is not written down.

### Checkpoint 5 — enrichment waves
- [x] E1 and E3 on stamped RulesetVersion history; ban test green; expeditions resolve; commit drafts handed to owner per task group (no git writes).
  - ### ✅ Checkpoint 5 — **CLOSED 2026-09-04**
    - **`RulesetVersion` is unmoved at 4, and that is the result rather than a compromise.** Both waves
      ship their mechanism **inert** — E1 by no shipped trait carrying a rider and no status carrying an
      element, E3 by `hybrid.secondaryWeightMilli` defaulting to 0 — so neither needed the version bump
      the original plan budgeted for them ("the program plans versions 2–5 up front", now retired).
    - **Goldens 36/36**, including the four expedition tier hashes. Ban test green (four boundary
      guards). Expeditions resolve.
    - ⏳ **What is deliberately left to the owner, not skipped:** E3's `secondaryWeightMilli` is the
      constant this todo marks **ask-first**, and raising it above 0 **moves the expedition goldens**
      (wave demons carry a real `ElementSecondary`). That is a predicted-delta + re-bless decision, and
      it is the only thing standing between E3-inert and E3-live.
    - **Commit draft** for both waves is in the session hand-off; git stays hands-off.

---

## Phase 6 — species-skills (replaces Wave E2)

Spec: [spec-species-skills.md](../docs/architecture/combat/spec-species-skills.md). Depends on
`battle-adoption` and battle T5 + T19 — **all shipped, nothing blocks S1.**
⚠️ **Baseline superseded 2026-09-04 — re-measure, never assume.** The 14/2 figure below was true when
this phase started and is not now: the demon and world-stage streams fixed most of theirs mid-run, so
the tree stands at **2 red Core / 3 red Data**, Guard **171/171**. And **any Core change invalidates
every `tools/` binary that references it** — six tests that shell out with `--no-build` reported false
regressions until the eleven tools were rebuilt. Rebuild tools, then measure, then compare.

Standing baseline: **14 red Core / 2 red Data inherited from other streams — compare against those,
not zero.** `RulesetVersion` stays **4**; this phase re-blesses nothing.

- [x] **S1: the neutral invariant, written first** — **DONE 2026-09-04**
  - A full battle with every actor at neutral `skill.*` produces a byte-identical report against the
    current golden. **Written before either read exists**, so it can fail for the right reason.
  - Neutral is `0‰` reduction and `1000‰` effectiveness — both reads collapse to the arithmetic
    identity. This is what lets the phase ship without a version bump.
  - Acceptance: green before S2 starts, and still green after S4.
  - Verify: `--filter ~Battle`. Scope: S.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - `SkillChannelNeutralityTests` — 4 green, written before either reader existed.
    - ⛔ **Corrected a claim in the spec while writing it: neutral is 0, not 1000.** Both channels
      register `FlatSum` with a default of **0** (`DerivedStatRegistry.cs:186,189`). For cooldown that
      is 0‰ of reduction; for effectiveness the channel is a *bonus* on top of
      `OverlayCombatRequest.EffectivenessMultiplier`'s own 1.0 no-op. `spec-species-skills.md` said
      "1000‰ effectiveness", which is the resulting multiplier, not the channel value. **A reader that
      took 0 to mean ×0 would zero all damage** — worth the correction.

- [x] **S2: the cooldown read** — **DONE 2026-09-04**
  - Where a cooldown is **armed**, resolve `ActionEnvelope.CooldownChannel` against the acting actor's
    sheet and pass the per-mille through `CooldownMath.ApplyReduction` before it reaches the ledger.
  - ⛔ **Arming site, not evaluation site.** `CooldownLedger` stores an absolute tick — its own comment
    explains why: *"An absolute tick has nothing to go stale."* Reducing at read time would let a
    mid-battle haste change retroactively alter a cooldown already running.
  - A null `CooldownChannel` reads nothing and arms at base ticks — the neutral path, allocation-free.
  - Acceptance: **proven by contrast, not existence** — same battle, same seed, one actor given a
    non-zero `skill.cooldown.{category}`; that actor's action recurs measurably sooner and *nothing
    else in the report moves*. Plus a **falsifier**: delete the read, the contrast test goes red.
  - Verify: `--filter ~SkillModifiers` + `--filter ~Battle`. Scope: M.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - `CooldownLedger.Start` gained `long reductionPm = 0` and applies
      `CooldownMath.ApplyReduction`, which had **zero callers** until now. `BasicAttack` resolves the
      channel the envelope names from the attacker's already-composed snapshot, so no resolve is added.
    - **Arming site, not read site** — the ledger stores an absolute tick ("An absolute tick has
      nothing to go stale"), so reducing at read time would retroactively shorten a running cooldown.
    - **Proven by contrast:** base 1000 ticks, 250‰ reduction → ready at **750**, neutral → **1000**.
      Zero reduction is the exact identity, and the omitted default parameter matches it.
    - **The structural floor survives the wired path** — 100%, 5,000% and 100,000,000% reductions all
      land on `CooldownMath.MinTicksFloor`, never 0.
    - ⭐ **Falsifier: deleting the reduction turned 4 of 10 tests red.** Restored → green.

- [x] **S3: the effectiveness read** — **DONE 2026-09-04**
  - `skill.effectiveness.{category}` as a per-mille multiplier on the resolved payload, **inside the
    resolver**, on the stage `OverlayCombatCalculator.cs:403` already names. There is no
    implementation today — the two mentions in that file are both comments.
  - ⛔ Never as a second multiplier applied by the caller afterwards: that puts combat math outside
    the SSOT and trips the parity tests by design.
  - Acceptance: contrast + falsifier, same shape as S2, on damage.
  - Verify: `--filter ~Overlay` + `--filter ~Battle`; `guard-single-writer.ps1`, `guard-funnel-delta.ps1`. Scope: M.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - `OverlayCombatRequest.EffectivenessMultiplier` already existed and already participated in the
      formula (`× BaseOverlayDamage`, before the power/defense delta); all three of its construction
      sites left it at 1.0. **S3 is that one site being set**, not new math.
    - **A layering constraint drove the design, and B31's own guard enforced it.** The multiplier is a
      `double`, but `Actions/` bans floating point — and as of B31 the guard catches the literal, so
      `1.0 + pm / 1000.0` in `BasicAttack.cs` is a purity violation the build would reject. The
      conversion therefore lives in `Combat/` as `OverlayCombatRequest.MultiplierFromPerMille`, and the
      seam across the boundary is **`long` per-mille in, `double` multiplier out**.
    - `ActionEnvelope` gained `EffectivenessChannel`, mirroring `CooldownChannel` exactly.
    - **Proven by contrast end to end:** the same battle at the same seed hashes differently once the
      squad carries `skill.effectiveness.attack = 500`. 0 → exactly 1.0; 250 → 1.25; −250 → 0.75.
    - ⭐ **Falsifier: deleting the read turned the end-to-end contrast red.** Restored → green.

- [x] **S4: category routing, the floor, and the receipt** — **DONE 2026-09-04**
  - `ActionCategory` is a **closed 5-value vocabulary**; an action's category comes from `ActionRow`
    (ordinal 33). An action with no category reads the unsuffixed family or nothing — **decided and
    asserted either way, never defaulted to a category it does not have.**
  - The floor holds under the wired path: an absurd reduction cannot produce a zero-tick cooldown
    (`CooldownMath.MinTicksFloor`, already tested at 100,000,000 % — extend to the live path).
  - Acceptance: an action in category A is unaffected by a channel value on category B; **and
    `CoverageReport` no longer lists either family as reader-less** — that is the phase's real receipt
    and the observable outcome for `class-system`'s readiness gate.
  - Verify: `--filter ~Coverage` + full Core against 14/2. Scope: S.
  - ### ✅ Evidence — **DONE 2026-09-04**
    - **Category routing proven both ways:** a value on `skill.effectiveness.support` leaves the basic
      attack's battle byte-identical, while the same value on `.attack` changes it. An envelope naming
      no channel reads nothing.
    - **The receipt.** Both `UnitClassNote`s in `DerivedStatRegistry` said *"No reader … zero
      callers … unbuilt"*. That is now false for `attack` and was updated — **per category, not
      globally**, which matters: see the finding below.
    - ⛔ **A deliberate tripwire caught an over-broad edit of mine, exactly as designed.**
      `MovementPayloadTests.Inertness_move_range_and_skill_cooldown_effectiveness_movement_have_no_production_reader_today`
      exists to *"FAIL the day someone wires a reader … rather than a stale 'no reader' claim quietly
      rotting."* My first note update marked all five categories as read when only `attack` has an
      opted-in shipped action, and it went red. **Fixed by making the note category-aware**: the reader
      MECHANISM is generic, but shipped CONTENT opts in only for `attack`, so the other four still say
      "No reader in shipped content" and the tripwire stays honest. 34/34 green after.
    - ⚠️ **`DominanceGuard.BuildReservedFamilies` was deliberately NOT changed.** It reserves these
      families because the *balance predictor* — a closed-form duel model — cannot read them, which is
      still true: the new readers are on the battle path. `CoverageReport`'s doc was corrected instead,
      because *"the predictor cannot see it"* and *"nothing reads it"* are different problems with
      different fixes, and the old wording ("unbuilt") conflated them.

- [ ] ⏸ **S5: species → action eligibility content** *(blocked, condition not date)*
  - Which species hold which actions is **eligibility**, and `A-E1 eligibility-axis` already shipped it
    (`content-stack-todo.md:528`) — `ActionRow` carries `scope`/`scope_key`, `ActionEligibility`
    evaluates it. This task **consumes** that; it adds no mapping table.
  - ⛔ **Blocked on `demon-corpus-self-heal` C2/C3/D1.** The species id scheme just changed (186
    deletions / 289 additions uncommitted, 14 Core tests red on renamed anchors) and two model reruns
    are still open. Authoring eligibility rows against those ids today means redoing them after.
    - ⛔ **Blocker verified LIVE and ACTIVE 2026-09-04 20:31 — the corpus is being rewritten as this
      is written.** `find data/seed/demons/species -newermt "-120 minutes"` returns **200 files**, the
      newest stamped **20:27** — four minutes old. `git status` on that tree shows **391 untracked /
      220 deleted / 105 modified**. The demon-corpus `C2` pass (`rerun --pipeline kit-shape --all`) is
      **still in flight** (`C3` and `D1` are `[x]`; `C2` is `[~]`, second pass running). **Authoring
      eligibility rows against these ids right now would mean redoing them** — precisely the condition
      this task is parked on. Strongest form of the check: not "the tests are still red", but "the
      files are changing under us this minute."
    - ⏱ **Polled to completion 2026-09-04 20:34–20:36, per the "a running job is not a stopping
      condition" rule.** Corpus writes **stopped at 20:27** (three polls a minute apart, zero files
      touched in the trailing 3 minutes each time; the only `python` processes on the box date from
      9/3). **So C2's writing is finished — and the condition still did NOT clear.**
    - ⛔ **Re-tested after the writes stopped, and the id scheme is still inconsistent:** the demon
      suite is **4 failed / 179 passed**, and the failure is precisely the anchor churn this task is
      parked on — `Peashooter_carries_every_catalog_runtime_field_straight_from_its_real_anchor` gets
      `["normal","mutated","corrupted","blessed","cursed", …]` where it expects `["normal","mutated"]`.
      **The corpus has settled on disk but not into a state its own consistency tests accept.**
    - ⛔ **Root-caused far enough to prove S5 must stay parked.** All four failures share one cause, and
      it is a *classification* problem rather than a stale test. Traced to the row, correcting my own
      first (wrong) guess of index drift — the index is self-consistent:
      `_index.json` maps `Peashooter → plant/sentinel-flora.json`, and that file really does hold
      `speciesId=Peashooter, rarity=almanac, variants=6`. The 2-variant row I first matched belongs to
      **JalaPeashooter**. **So C2's rerun genuinely promoted the starter plant to the top rarity rung**,
      and the row now **violates its own rarity/variant band** (the band test computes 2–3, finds 6),
      so at least one of `rarity`/`variants` is wrong on the corpus's own terms. **Authoring eligibility rows
      against a corpus that currently mis-rarities its starter species would bake in a regression** —
      the precise harm this task was parked to avoid. Full write-up handed to the owning stream in
      `tasks/demon-corpus-self-heal-todo.md`.
      Authoring eligibility rows against it now would key content to ids the demon stream is still
      reconciling. **The unblock is that suite going green, not the writes stopping.**
    - ✅ **Also re-verified via tests, 2026-09-04** — an unchecked blocker is a claim, and this one
      was tested before being restated. The demon stream is still mid-flight: `SpeciesExpanderTests`
      (3) and `SpeciesCatalogDiffTests` (1) are red in this run's own full-Core output, on exactly the
      renamed-anchor and variant-band assertions this line describes (`Expected: ["normal","mutated"]`
      vs an actual list that now carries `corrupted`/`blessed`/`cursed`). **The condition holds; S5
      stays deferred.** The count moved (4 demon reds, not 14) because that stream has been fixing them
      — which is the blocker resolving, not the blocker being wrong.
  - Acceptance: authored rows resolve, and S1's neutral invariant still holds for actors with none.
  - Verify: `--filter ~Eligibility` + full Core. Scope: M.

### Checkpoint 6 — species-skills complete
- [x] Both channels have a real, **falsifier-proven** reader; the neutral battle is byte-identical and
      `RulesetVersion` is still 4 with no golden re-blessed; `CoverageReport` shows neither family
      reader-less; **the diff adds reads, not vocabulary** — no `SkillDef`, no `SkillCatalog`, no second
  - ### ✅ Evidence — **Checkpoint 6 CLOSED 2026-09-04** (S5's content deferral recorded, per this checkpoint's own final criterion)
    - **Both channels have a real, falsifier-proven reader.** `skill.cooldown.attack` via
      `CooldownLedger.Start`; `skill.effectiveness.attack` via `OverlayCombatRequest`. Both live on the
      shipped basic attack, so they run in **every** battle rather than waiting on content.
    - **The neutral battle is byte-identical and `RulesetVersion` stays 4** — goldens **36/36**, no
      re-bless. This was the whole safety argument and it held.
    - **No new vocabulary.** The diff adds two reads, one envelope field and one conversion helper.
      No `SkillDef`, no `SkillCatalog`, no second description of an action — which is what
      `spec-species-skills.md` replaced Wave E2 to avoid.
    - **Suite:** full Core **15 failed / 5527 passed** against a stable baseline of 14; the single
      extra is the item stream's brand-new `RarityOverlapSimulatorTests.Diagnostic_dump_per_pair_rates`,
      not this work. Four boundary guards green.
    - ⚠️ **One criterion reconciled rather than met literally, and the difference matters.**
      "`CoverageReport` shows neither family reader-less" — `DominanceGuard.BuildReservedFamilies`
      still reserves both, and that is **correct, not an omission**: that list means "no reader in the
      PREDICTION path", and the predictor is a closed-form duel model that never runs a battle. The new
      readers are on the battle path. Removing them would tell the balance guard it can see channels it
      genuinely cannot, and its own history says what that costs — a coverage gap once moved six
      aptitudes to 0/11 wins purely because points landed in channels the predictor could not read.
      **`CoverageReport`'s doc was corrected instead**, to separate "the predictor cannot see it" from
      "nothing reads it" — two different problems with different fixes, which the old wording
      ("unbuilt") conflated.
    - ⏸ **S5 remains open on its stated condition** (`demon-corpus-self-heal` C2/C3/D1), not on a date —
      and this checkpoint's own final criterion is that the deferral is *recorded with the condition
      that releases it*, which it is. Every other criterion is met, so the checkpoint closes and S5
      stays visible as the one piece of content still owed.
      description of an action; S5's deferral is recorded with the condition that releases it.
