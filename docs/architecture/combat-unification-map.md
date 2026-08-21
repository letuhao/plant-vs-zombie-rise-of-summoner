# Capability map: combat unification + battle enrichment

**Status:** Map + module specs drafted 2026-08-21 (owner decisions folded in). **Build is held** until the owner confirms the battle stream's waves are finished — spec-now, build-later. The decisions.md amendment row lands as the first build task, mirroring the shield program.
**Why:** RPG damage currently resolves in three scattered places — the overlay pipeline (`OverlayCombatCalculator` → dispatcher → shield gate → Funnel), `BattleEngine`'s inline per-mille math (own hit/crit constants, own `ShareMilli` matchup mirror, funnel enqueue that **bypasses the shield gate**), and `SimEngine`'s direct HP mutation. The scatter is why battle has no shields. Owner ruling: **the combat (overlay) system is the SSOT — battle's parallel formulas were a responsibility violation and are retired.**

## Owner decisions (2026-08-21, binding)

1. **One canonical formula set; combat is the SSOT.** Battle adopts the overlay's resolution math (sigmoid hit/crit via `CombatProbability`, ElementHub matchup, typed power/defense). Battle's `BattleRuleset` hit/crit constants, `ShareMilli`, and the ±20 % damage variance roll are retired (variance may return only as a shared resolver policy — ask first). `RulesetVersion` bumps to 2; battle goldens re-baseline once **per version bump** (the program plans versions 2–5 up front; each wave's re-bless is reviewed against a predicted delta).
2. **Scope = everywhere:** overlay + battle (+ expeditions via battle) **and SimEngine** — sim damage routes through the central pipeline so server-side probes exercise shields with no game running.
3. **Enrichment scope: shields + on-hit status riders + skills** — the full program, in that order.
4. **Spec now, build later.** No Core/Battle edits until the owner green-lights (that stream was active today).
5. **Re-tune preserves today's feel** (post-audit): battle baselines are re-expressed in resolver-scale points with rate-tested acceptance — level-parity P(hit) = 0.90 ± 0.02, P(crit) = 0.05–0.10 — the Chaos-backend-style balance mechanism. Traits/ChannelMods re-costed in the same pass.
6. **Shared min-chip floor** (post-audit): landed hits deal ≥ `ceil(0.05 × base)`, min 1, as resolver policy — battle/sim profiles on, overlay profile 0 (byte-identity; enabling is ask-first). Closes the deterministic 0-damage stalemate class.
7. **Platform stamp** (post-audit): `BattleReport` gains an architecture+runtime stamp; the sweep/replay guard refuses cross-platform re-resolution (closes the `Math.Exp` cross-arch determinism hole the four version stamps don't cover).

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `combat-resolver-core` | Declare + harden the overlay resolution path as the one combat SSOT: deterministic RNG adapter, componentless (omni) attack support, retire-duplicates contract, cross-mode parity tests | — |
| `damage-apply-pipeline` | One apply path (finalized delta → shield gate → Funnel) extracted from the dispatcher; overlay delegates byte-identically; any host can mount it | `combat-resolver-core` |
| `battle-adoption` | `BattleEngine` resolves attacks through the SSOT resolver and applies through the pipeline → shields work in battle (absorb, round regen, innate, death flush); `RulesetVersion` 2 | both above |
| `sim-adoption` | `SimEngine` damage through the pipeline + a sim shield probe → server-side shield verification without the game | `damage-apply-pipeline` |
| `battle-enrichment` | On the unified pipeline: on-hit status riders, per-species cooldown skills, hybrid element payloads | `battle-adoption` |

**Build order:** `combat-resolver-core` → `damage-apply-pipeline` → `battle-adoption` ∥ `sim-adoption` → `battle-enrichment`.

Module specs (this directory's `combat/` folder, named by module id):
[spec-combat-resolver-core.md](combat/spec-combat-resolver-core.md) · [spec-damage-apply-pipeline.md](combat/spec-damage-apply-pipeline.md) · [spec-battle-adoption.md](combat/spec-battle-adoption.md) · [spec-sim-adoption.md](combat/spec-sim-adoption.md) · [spec-battle-enrichment.md](combat/spec-battle-enrichment.md)

## Interfaces at the boundaries (provider-owned)

- `combat-resolver-core` provides: `OverlayCombatCalculator.Compute(OverlayCombatRequest, ICombatRng)` (unchanged shape) + `SeededRngCombatAdapter : ICombatRng` + omni-fallback semantics for componentless requests (a stated contract change: empty payloads stop throwing) + the per-profile min-chip policy.
- `damage-apply-pipeline` provides: `DamageApplyPipeline.Apply(ptr, finalizedSignedAmount, hitCount, components, attackerSnap?, ownerSnap, shieldGate?, sink, meta, noteOverlayDamage) → applied/absorbed` — ptr-space input, pipeline-owned key prefixing, `IHpDeltaSink` abstraction (funnel adapter or direct sink), packet-free gate overload.
- Consumers never re-implement hit, crit, matchup, floor, or shield math — enforced by the parity/ban tests in `combat-resolver-core`.

## Audit trail (2026-08-21)

Two-lens audit (design red-team with computed constants + code-integration verification) on the drafts. Material outcomes folded into the module specs: the adoption balance cliff quantified (56 %/52 % at real constants) → rate-tested re-tune subtask (decision 5); dead-Defense/0-damage mapping contradiction → explicit composer table + min-chip policy (decision 6); omni fallback re-founded (empty components throw today — it's a contract change, `ElementPayload`/`ElementHub` in scope); pipeline API pinned (ptr-space keys + one-key discipline against silent FA10-slot splits, `IHpDeltaSink` for funnel-less sim, `NoteOverlayDamage` as explicit stage, packet-free gate overload); DoT parity + `hitCount = 1` + ms-based durations; riders get their own RNG stream; report vocabulary grows deliberately (`BattleEventRec` fields, emitter whitelist); expedition hash churn named as serialization-shape + win-rate sweep acceptance; RNG draw-consumption and crit-mult (1, 2) bounds locked; guard tripwires (`targetPtrs`, writer-class name in comments) documented; platform stamp (decision 7).

## Program-wide invariants

- Funnel stays hp-only, add-only, FA10; the shield gate is the only pre-Funnel damage stage.
- Overlay behavior is **byte-identical** through the refactor (goldens are the proof); battle/sim change behavior by design, exactly once, at their adoption version bumps.
- Determinism: battle keeps owned-PRNG streams (`SeededRng`) behind `ICombatRng`; `System.Random`-backed `SeededCombatRng` never backs a replayable path. Sigmoid doubles are deterministic per runtime; replay guarantees stay scoped to matching `(engineVersion, rngAlgoVersion, rulesetVersion, seed)` stamps, as the battle report already declares.
- No vanilla PVZ behavior is touched anywhere in this program.
