# Tasks: action program

Plan: [action-plan.md](action-plan.md) · Map: [../docs/architecture/action-map.md](../docs/architecture/action-map.md) · Specs: [../docs/architecture/action/](../docs/architecture/action/)

Scope: S ≈ under an hour, M ≈ a focused session, L ≈ multi-session.

> **⛔ Gate 0 — nothing here starts until the effect-atom program has built.** `A1` needs `effect_container`, `A4` needs two `E3` leaves, `A6` registers into `E8`'s hash. All three are approved and written into their specs; all three are theirs to land.

## Phase 0 — prerequisites

- [x] **P0: `decisions.md` rows** — Resource model, Action model, Golden ordering. Landed 2026-08-22.

- [ ] **P1: extend the purity scan to `Core/Actions/`** *(before the first line of action code)*
  - Purity rules **on** (wall clock, ambient `Random`, `Guid.NewGuid`, `.GetHashCode(`, floating point, dictionary enumeration); tick-path rules **off** — `TargetResolver` needs LINQ.
  - Reuses `DiagnosticsExemptFromTickPath`'s existing shape; a directory plus an exemption entry, not new machinery.
  - Acceptance: a planted `DateTime.UtcNow` and a planted ambient `Random` in `Core/Actions/` each **fail** the scan; a planted `.Where(` does **not**. A guard that cannot fail is decoration.
  - Verify: `--filter ~PurityScan`. Scope: S.

## Phase 1 — the foundation (A1)

- [ ] **T1: `rpg_action` — table, record, validator, round-trip**
  - Columns per [spec-action-model.md](../docs/architecture/action/spec-action-model.md) §1. Scaling values are `ValueSpec`, reused — **not** a second scaling mechanism.
  - Acceptance: a row round-trips through `RpgStore`; **each validation rule rejects a planted bad row naming it** — unknown `container_id`, unknown `resource_id`, `min_range > max_range`, unknown tag. Reject, never coerce.
  - Verify: `--filter ~ActionModel`, `guard-dal.ps1`. Scope: M.

- [ ] **T2: costs and effect scopes**
  - `rpg_action_cost(action_id, resource_id, amount_spec, when)` and `rpg_action_effect_scope(action_id, atom_id, scope)`.
  - Acceptance: a scope naming an atom the container does not hold is rejected; an atom with no scope row defaults to `eachTarget`; `when` round-trips.
  - Verify: `--filter ~ActionModel`. Scope: S.

- [ ] **T3: action binding and resolution**
  - `species.action_ids` (intrinsic) + `rpg_actor_action(owner_kind, owner_key, action_id, source)` (granted), mirroring `effect_binding`'s owner vocabulary and its `source` so a withdraw removes exactly what a grant added.
  - Acceptance: resolution is intrinsic ∪ granted, deduplicated, **ordered by `action_id` ordinal**; an actor with no grants still has its basic attack; a withdraw by source removes only that source's actions.
  - Verify: `--filter ~ActionModel`. Scope: M.

## Phase 2 — targeting (A2)

- [ ] **T4: the typed spec and its compiler**
  - `ActionTargetSpec` + `ActionTargetFilters`, closed enums, compiling to **`TargetSpec[2]` (one per caster side)** and a **filter predicate over `BoardEntitySnap`** — audit C3: `FilterPool` re-parses its dictionary on every resolve, and `A7` calls it per candidate.
  - Acceptance: **one authored action serves both factions** — `Relation = Enemy` compiled for a plant and a zombie caster picks opposite pools from the same row. This is the module's reason to exist, so it is the first test. Unknown filter keys rejected against a planted key.
  - Verify: `--filter ~ActionTargeting`. Scope: M.

- [ ] **T5: `GridDistance` and the range gate**
  - Chebyshev, one implementation, two callers (`A2`'s filter and `A4`'s gate — audit I6).
  - Acceptance: **with no board every range check passes** — not empty, not throwing. A `Square` of size *n* contains exactly the cells within radius `(n−1)/2`, proving the metric matches the shipped shape. The gate is a **stable filter**, asserted with in-range members non-adjacent in sort order.
  - Verify: `--filter ~ActionTargeting`. Scope: M.

- [ ] **T6: the `target` RNG stream**
  - `SeededRng.DeriveStream(seed, "target")`. The battle names `initiative`, `crit`, `essence`, `status` — there is no `target`, so `Mode = Random` today is either nondeterministic or silently desyncs another stream (audit C2).
  - Acceptance: the gate applies **before** the random pick — same seed, one target moved out of range, and the survivors match the in-range subset rather than a reshuffle. A gate applied after the pick passes a naive test and fails this one.
  - Verify: `--filter ~ActionTargeting`. Scope: S.

## Phase 3 — usability (A4)

- [ ] **T7: the five gates**
  - bound → cooldown → affordability → range → condition, cheapest first, short-circuiting, typed refusals. **Affordability is an `IAffordabilityCheck` seam returning affordable until `A3`** (audit R2-1).
  - Acceptance: each gate refuses with **its own** reason, not a shared "unusable"; an action both on cooldown and unaffordable reports `OnCooldown`, proving order; `FactReader.Reads` is **zero** when an earlier gate refuses; evaluation allocates **zero bytes**; position leaves are **false, not throwing**, with no board.
  - Verify: `--filter ~ActionUsability`. Scope: M.

## Phase 4 — the proof (A5) ⛔

- [ ] **T8: parity capture — before any engine change**
  - Record per-stream draw **values** (`initiative`, `crit`, `essence`, `status`), target ptr per attack, and signed delta per apply, across the eight golden fixtures, via `BattleTrace`.
  - Acceptance: fixtures captured and committed **while the engine is untouched**. Counts alone are insufficient — a count-matching, value-differing run is exactly the failure this exists to catch.
  - Verify: `--filter ~BasicAttackAdoption`. Scope: M.

- [ ] **T9: the three envelope gaps**
  - Duration `min`/`max` bounds, a cooldown-reduction channel, `interrupt_cooldown_milli` (default `1000‰`) replacing `ActionRunner.Interrupt`'s current no-cooldown behaviour.
  - Acceptance: all three additive and **inert for a zero envelope**; goldens unmoved. An interrupted channel now pays a cooldown, asserted directly.
  - Verify: `--filter ~TurnFsm` + goldens. Scope: M.

- [ ] **T10: the basic attack as a declared action**
  - Authored row, intrinsic binding, engine inner loop calling the action path. **Scope is the first four steps only** — active check, CC-lock, target, `Compute`. The trait tail stays engine code.
  - Acceptance: **seven hazard fixtures**, each engineered so that "improving" the behaviour turns it red — draws inside `OrderBy`; CC-locked actors still draw; no-target **`break`**; miss **`continue`** with the crit stream already advanced; essence draws only on a landed hit; one `host.Flush()` per attack; element components from `attacker.AttackComponents`. Plus `SourceOrder` vs `OrdinalPtr` producing different targets where the two disagree.
  - Verify: full Core + goldens. Scope: L.

- [ ] **T11: gate verification**
  - Acceptance: **eight goldens byte-identical**, `RulesetVersion` still 2, content hash unmoved, **six suites green with no test edited** (Core, Data, Guard, CheatCore, Launcher, E2E), four boundary guards green.
  - Verify: all suites + all guards. Scope: M.

### ⛔ Checkpoint A — byte-identical
- [ ] A re-bless here means the model is wrong. **Stop, do not bless.**

## Phase 5 — costs (A3)

- [ ] **T12: resources — catalog, channels, lazy pools**
  - Five ids code-first (like `StatusCatalog`); `resource.max.{id}` / `resource.regen.{id}` as their **own** derived family.
  - Acceptance: `AllCombatChannelIds` is **still exactly 84**, tested directly; **lazy regen equals scheduled regen** — one resolve after 1000 ticks against a thousand one-tick steps; **zero scheduled events** for four pools across 200 actors.
  - Verify: `--filter ~Resource`. Scope: M.

- [ ] **T13: exhaustion as a status**
  - Reuses `StatusRuntime` — instances, stacking, resistance, VFX, `icd_ms`. The debuff is a **container of atoms**, never a hardcoded channel list.
  - Acceptance: **one status apply, not one per tick**, for a pool held at the threshold with regen trickling — counted, because the final state is identical either way and that is what hides the bug. **Exhaustion re-evaluates on read**, proven by crossing the leave threshold with no write at all. A self-regen cycle is **rejected at load** against a planted cycle.
  - Verify: `--filter ~Resource`. Scope: M.

- [ ] **T14: paying**
  - Validate all → consume all → roll back on any failure. `when` = `onCommit` | `perTick`.
  - Acceptance: rollback asserted **per pool**, not in aggregate — an aggregate assertion passes when two errors cancel. A `perTick` cost that cannot be paid ends the action through the interrupt path, releasing the slot and charging `interrupt_cooldown_milli`.
  - Verify: `--filter ~ActionCost`. Scope: M.

- [ ] **T15: run pools and rest**
  - Per-member pool rows on the run. **No run row means a run of one** — the skirmish case, which is both correct and the easiest to test.
  - Acceptance: pools survive an encounter boundary and refill at rest, `hp` included; `ExpeditionResolver` threads them through its encounters; **at battle end pools resolve to a concrete value and `lastTick` is dropped**; cooldowns do **not** cross a battle boundary.
  - Verify: `--filter ~Resource` + Data suite. Scope: L.

## Phase 6 — catalog (A6)

- [ ] **T16: load, compile, cache, hash**
  - Server-side only — **no push**; actions are battle-mode and the injector never sees one.
  - Acceptance: a malformed row fails **at load naming the row**; **no JSON is parsed after load**; a changed action value **changes** the content hash and an unchanged catalog does **not** (both directions); a revision swap is atomic so a battle in flight keeps its catalog.
  - Verify: `--filter ~ActionCatalog` + `guard-dal.ps1`. Scope: M.

### ✅ Checkpoint B — actions are content
- [ ] A second and third action exist as **rows**, with costs, and a changed value moves the hash.

## Phase 7 — selection (A7)

- [ ] **T17: the `IBattleView` seam** *(before the AI)*
  - Acceptance: an **architecture test** fails if the intent source touches battle state directly. Written first, because the seam erodes on the first convenient shortcut and fog then stops being a swap.
  - Verify: `--filter ~ActionSelection`. Scope: S.

- [ ] **T18: the stub AI**
  - Pursue nearest, act to kill, move if out of reach, **pass** if nothing works. Preference key is the stub's own — **not `priority_band`**, which is a scheduling concept (audit I2).
  - Acceptance: with every actor unable to declare, the battle **terminates** rather than hanging — the sharpest test here, since a hang is a stopped clock. Ties identical across two runs **and across a shuffled actors list**. Zero allocation per decision at 200 actors. **`FactReader.Reads` scales with targets, not actions × targets** — a correct-but-unhoisted implementation passes every behavioural test and fails this one. With no board, selection matches `SelectTarget` on all eight fixtures.
  - Verify: `--filter ~ActionSelection` + goldens. Scope: L.

### ✅ Checkpoint C — something chooses
- [ ] `SelectTarget` has a replacement both auto-battle and interactive enter through; replay holds across runs and across list order.

## Deferred — specced, not scheduled

- [ ] **A8 defence-actions** — waits on timeline **B6** (the reaction lane, unbuilt). `WReact = 0` must be byte-identical to no lane.
- [ ] **A9 movement-actions** — waits on `A10`. One row, no new runtime.
- [ ] **A10 battle-board** — owner deferral; built with the board map / battle area.

### ⛔ Checkpoint D — the movers
- [ ] `A10` + `A9` + `A7`'s distance targeting + fog, with timeline **T9** and atom **E12**: **one combined re-bless, one sweep, `RulesetVersion` advances once.** Owner sign-off on the sweep.
