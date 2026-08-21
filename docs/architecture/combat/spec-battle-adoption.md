# Spec: battle-adoption

Module id `battle-adoption` in the [combat unification map](../combat-unification-map.md). Depends on `combat-resolver-core` + `damage-apply-pipeline`. **Build held** until the owner confirms the battle stream is done. Audited 2026-08-21; this revision folds the audit's two criticals (balance cliff, mapping contradiction) and the owner's re-tune/floor/stamp decisions.

## Objective

`BattleEngine` stops computing combat and starts consuming it: attacks resolve through the SSOT resolver and apply through the shared pipeline — making **shields fully functional in battle** with the shipped 164-test `ShieldRuntime` unchanged. Battle's parallel formulas retire. `RulesetVersion` → **2**; goldens re-baseline once against **re-tuned baselines whose acceptance is rate-tested, not eyeballed**.

## Design (locked on approval)

### Resolution — replace, don't wrap

- One `OverlayCombatRequest` per swing: `BaseOverlayDamage = attacker.Setup.Atk`; components = primary element at weight 1.0 (untyped → empty list → omni fallback); snapshots from `BattleStatComposer`; rng = `SeededRngCombatAdapter` over the `crit` stream. `ForceHit`/`ForceCrit` never set in production or goldens (they skip RNG draws — forced ≠ natural streams; ban-tested).
- **Retired:** `BattleRuleset.{Hit*,Crit*}` constants, `ShareMilli` (+ its mirror test), `RollDamage` matchup/±20 % variance, the `damage` stream's variance draw (stream *names* stay reserved for v1 decode).
- **Composer mapping table (audit fix — the draft was half-wrong):**

| Setup stat | Goes to |
|---|---|
| `Atk` | `BaseOverlayDamage` only — `combat.power.omni` baseline **removed** (was double-counted) |
| `Atk/4`, `Atk/8` element affinity | stays on `combat.power.{elem}` (adjustment, not base) |
| `Defense` | **stays** on `combat.defense.omni` (its only consumer — removing it kills the stat) + affinity unchanged |
| accuracy/dodge/critRate/critResist baselines | stay, but **re-expressed** (next section) |
| crit-damage families | new explicit baselines = 0 (locked ×1.5 anchor); `ChannelMods` may move them within the resolver's (1.0, 2.0) bound |

  Behavior note, stated so it's a choice: matchup bonus now scales with `Atk` (±0.25 × Atk) instead of with `(power − defense)`. `BattleStatComposerTests`' `power.omni == Atk` assert inverts; the defense asserts stay green.

### Baseline re-tune (owner decision: preserve today's feel — the Chaos-style balance mechanism)

The sigmoid at battle's current stat magnitudes yields ~56 % hit / ~52 % crit (vs 99.5 % / 10 % today) and halves per-point stat sensitivity. Dedicated subtask, with **rate-test acceptance criteria**, not golden inspection:

- `BattleRuleset.BaseAccuracy/BaseDodge/BaseCritRate/BaseCritResist` re-expressed in resolver-scale points such that **level-parity P(hit) = 0.90 ± 0.02 and P(crit) = 0.05–0.10**, asserted by statistical rate tests over the resolver at representative levels (1/5/10/20).
- Per-level growth targets stated in the plan (e.g. hit vs equal-level flat; hit vs −5-level target ≥ 0.97).
- Trait/`ChannelMod` re-costing in the same pass (dodge/accuracy/crit mods are worth half per point at the sigmoid center — swift, dodge mods, crit mods all re-valued).
- Min-chip floor active (battle profile `MinChipShareK = 0.05`, min 1 — owner decision): the stat-check stalemate class is closed; a max-defense matchup grinds on chip damage instead of freezing at 0.

### Shields — the shipped runtime, mounted per battle

- One `ShieldRuntime` + gate per `Resolve`; owner keys are actor keys (verified: `CombatPtr.Normalize` is identity on `squad:0`); **setup validation additionally rejects keys starting `entity:` or `0x`** (Normalize would mangle them silently).
- **All battle HP deltas route through the pipeline** — attacks, DoT pulses, regenerator, immortal revive, soul-eater, guardian share — satisfying the pipeline's one-key discipline (no split FA10 slots; invariant-tested). Positive deltas bypass the gate by pipeline rule.
- Guardian semantics, stated: one swing → two pipeline calls; the target's shield absorbs the target slice, the guardian's shield absorbs the share slice, each with its own `shield.absorbed` aggregate. The essence rider stays merged into the target's single delta (one gate pass). `DamageDealt` tallies **resolver output** (pre-absorb); `ShieldAbsorbed` is its own per-actor tally from `DrainEvents`.
- **DoT parity (audit fix):** pulses go through the pipeline with `hitCount = 1` (matches overlay's per-pulse `HitCount` default) and an **empty component list** — element-neutral absorption is the overlay's current behavior too (`StatusFunnelPulseSink` sends no payload), so this is exact parity. Typed DoTs arrive program-wide in enrichment Wave R (requires `StatusInstance` to carry an element — flagged there).
- **Round upkeep:** `runtime.Tick(round, RoundDurationMs = 1000 ms, …)` at round end, after all dispatch — regen math is `rate × deltaMs` with carry, so 1000 ms rounds are exact (verified). Death/retreat: `RemoveAll(key)` beside `status.WithdrawEntity`.
- **Innate shields:** `BattleActorSetup.InnateShield` (`BaseHp`, `Element?`, priority default 10, **durations in ms** at the content boundary — converted `ticks = ceil(ms / RoundDurationMs)` per host, closing the 100 ms-vs-round unit fork). Applied by **direct `Runtime.Apply` at setup** (composed snapshots exist before round 1 — the queue barrier and granted-once markers are overlay-resync machinery a battle never needs).
- **Report (audit fix — the vocabulary must grow, it can't "forward"):** `BattleEventRec` gains optional `Amount/Element/ShieldId` fields; `BattleEventKinds` += the four `shield.*` kinds; `BattleReportEmitter`'s Die-only whitelist expands **deliberately** (its "no per-attack events" stance holds — absorbed events are per-round aggregates from `DrainEvents`, whose insertion order follows the runtime's ordinal owner sort; golden-locked). `BattleActorResult` += `ShieldAbsorbed`. All three are serialization-shape changes — see golden churn below.

### Versioning, determinism, goldens

- `RulesetVersion = 2`. **Platform stamp (owner decision):** `BattleReport` gains an environment stamp; `WebMatchService.SweepUnresolved` refuses cross-platform re-resolution exactly like version mismatches — closing the `Math.Exp` cross-arch hole the four existing stamps don't cover. Three rules make the stamp actually do that job (all learned in the post-build review, all test-locked):
  - **Composition is `architecture / OS / runtime-major`.** OS is load-bearing: on CoreCLR x64 `Math.Exp` calls the platform libm (ucrtbase vs glibc vs Apple), so architecture alone lets Windows-x64 and Linux-x64 collide — a blind spot on exactly the case being guarded. The runtime **major** only: a servicing bump (8.0.11 → 8.0.30) rebinds nothing here, and including it would strand logged matches behind a routine `dotnet` upgrade.
  - **The stamp is excluded from golden hash input.** It is a property of the machine, not the battle; leaving it in binds every golden to the box that blessed it, so CI reads a portability failure as a determinism break. Locked by `BattleGoldenTests.Goldens_do_not_depend_on_the_platform`.
  - **A refusal is terminal, not a skip.** Refused rows are stamped into `rpg_web_match_log.sweep_refused` (kept for forensics) and excluded from the unresolved query. Unmarked, they are re-listed every boot, and since that query is `ORDER BY id ASC LIMIT n`, enough of them crowd out every newer row — crash recovery dies silently while still reporting a clean sweep.
- Golden re-baseline, scoped honestly: hash goldens re-bless; **seed-pinned shape tests get re-selected seeds** (retreat/immortal/loyal scenarios can flip at 90 % hit, let alone during tuning); `WaveCD` saturation test re-verified against the resolver's double math (`Math.Round`, not `MulMilli` saturation); **expedition hashes churn too** — `InnateShield` on `BattleActorSetup` changes serialized plans even with no expedition behavior change (named as shape-churn in the re-bless review).
- **Expedition outcome sweep (audit fix):** green tests ≠ unchanged economy. Acceptance includes a seeded before/after win-rate sweep over the wave matrix with owner sign-off on the delta (expeditions pay out on Victory only).
- Accepted consequence, recorded: with variance retired, per-matchup landed damage is a fixed set (base, base+chip, crit values) — legible, bimodal, intentional. Variance returns only as shared resolver policy (ask-first).
- Shield determinism replay (shield spec §7) closes here: a battle with granted + innate shields replays byte-identically.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Shield|FullyQualifiedName~Expedition"
.\scripts\guard-funnel-delta.ps1   # Core/Battle naming discipline incl. comments
```

## Structure

```
src/FusionRpg.Core/Battle/BattleEngine.cs         (resolver swap, pipeline routing, shield mount, key validation)
src/FusionRpg.Core/Battle/BattleModels.cs         (InnateShield, event fields, ShieldAbsorbed, RulesetVersion 2,
                                                   re-tuned Base* functions, platform stamp)
src/FusionRpg.Core/Battle/BattleStatComposer.cs   (mapping table above)
src/FusionRpg.Core/Battle/BattleReportEmitter.cs  (shield kinds; stamp)
src/FusionRpg.Server/WebMatchService.cs           (sweep guard: platform stamp check)
tests/FusionRpg.Core.Tests/Battle/                (rate tests, parity, shield E2E, re-baselined goldens)
tests/FusionRpg.Core.Tests/Expeditions/           (hash re-bless — shape churn; win-rate sweep)
```

## Testing strategy

Rate tests (the re-tune acceptance: 0.90 ± 0.02 hit, 0.05–0.10 crit at parity, growth targets); resolver parity (battle swing ≡ direct resolver call, natural rolls); shield-in-battle E2E goldens (absorb vs traits, guardian two-slice, break events in report order, innate boss shield, regen across rounds, retreat/death flush); one-mutation-slot invariant; chip-floor grind golden (max-defense matchup progresses); re-baselined battle + expedition goldens with predicted-delta review; win-rate sweep report; ban test armed.

## Boundaries

- **Always:** all HP deltas through the pipeline; shields before HP; natural rolls in goldens; owned-PRNG adapter; rate-tested re-tune before re-bless; single re-baseline for this version.
- **Ask first:** variance reintroduction; guardian shield-share; overlay chip floor; any trait moving into the resolver; report vocabulary beyond the four shield kinds.
- **Never:** host-local hit/crit/matchup/floor math; funnel enqueues bypassing the pipeline; `ForceHit` in production; breaking v1 report decodability; `entity:`/`0x` actor keys.

## Success criteria

1. Rate tests green at the owner's targets (90 ± 2 % hit, 5–10 % crit at parity). 2. Battle swings ≡ resolver (parity). 3. Shields absorb end-to-end with report events and correct guardian two-slice semantics. 4. Retired symbols gone; ban test green. 5. RulesetVersion 2 + platform stamp live; sweep guard refuses mismatches. 6. Chip floor closes the stalemate class (grind golden). 7. Expedition hashes re-blessed as shape-churn + win-rate sweep signed off. 8. Shield determinism replay closes.
