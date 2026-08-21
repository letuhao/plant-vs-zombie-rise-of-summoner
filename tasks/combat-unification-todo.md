# Tasks: combat unification + battle enrichment

Plan: [combat-unification-plan.md](combat-unification-plan.md) · Map: [../docs/architecture/combat-unification-map.md](../docs/architecture/combat-unification-map.md)
**Gate:** U9+ edits `Core/Battle` — blocked until the owner confirms the battle stream is finished.

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

- [~] **Task U14: Golden re-baseline + expedition sweep — DONE except owner sign-off.** Re-blessed: 4 battle hashes (shape tests held WITHOUT seed re-selection: stomp Victory, wipe Defeat + coward retreat all survived the re-tune — the 90% target preserved the golden shapes), 4 expedition hashes (named serialization-shape churn; resolver byte-stability proven by its own determinism tests). Sweep report: `docs/research/combat-unification-v2-sweep.txt` — mirror-match symmetry 48–56% (engine balanced); bare-stat squad table shows content difficulty cliffs (0%/100%) from determinism, NOT an adoption regression (v1 baseline not reconstructible — battle code was never committed). **⛔ Awaiting owner sign-off on the sweep before this task closes.**
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
- [ ] **Owner sign-off on the win-rate sweep** (the one open item — see U14).

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

- [ ] **Wave E1: On-hit status riders (v3)** — rider specs on setups/traits; dedicated `riders` RNG stream; `StatusInstance` element (typed DoTs → gate, both modes); zero-rider battles byte-identical v2→v3.
- [ ] **Wave E2: Species skills (v4)** — `SkillCatalog`, deterministic selection, actions via resolver/pipeline/ShieldRuntime/StatusRuntime only; `skill.used` events; zero-skill invariant.
- [ ] **Wave E3: Hybrid payloads (v5)** — secondary element as weighted component (policy constant, ask-first to change); dual-type matchup tables golden.

### Checkpoint 5 — Program complete
- [ ] All waves on stamped RulesetVersion history; ban test green; expeditions resolve on v5; commit drafts handed to owner per task group (no git writes).
