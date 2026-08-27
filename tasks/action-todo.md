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

- [ ] **P0.1: extend the purity scan to `Core/Actions/`** *(before the first line of action code)* · **S**
  - Purity rules **on** (wall clock, ambient `Random`, `Guid.NewGuid`, `.GetHashCode(`, floating point,
    dictionary enumeration); tick-path rules **off** — `TargetResolver` needs LINQ.
  - Reuses `DiagnosticsExemptFromTickPath`'s shape: a directory plus an exemption entry, not new machinery.
  - Acceptance: a planted `DateTime.UtcNow` **fails**; a planted ambient `Random` **fails**; a planted
    `.Where(` does **not**. **A guard that cannot fail is decoration.**
  - Verify: `--filter ~PurityScan`

### Other programs — this program supplies the requirement and the tests

- [ ] **P0.2: linkage** — a magnitude that reads `EffectEventDto.Damage` (GAS's `SetByCaller` shape) · **effect-atom**
- [ ] **P0.3: predicate pricing** — `power_predicate_frequency`, the four-factor chain, the 2.5× floor · **effect-atom**
- [ ] **P0.4: `holdsStock` leaf** + a readonly `FactReader` stock probe · **effect-atom**
- [ ] **P0.5: `turn.speed` registered with a reader**, and readiness computed · **battle-timeline**

### ✅ Checkpoint 0 — report only
- [ ] `--filter ~PurityScan` exit 0 **with both planted violations failing**

---

## Phase 1 — the row and the ladder (`A1`, `A12`)

- [ ] **T1: `rpg_action` — table, record, validator, round-trip** · **M**
  - Columns per [spec-action-model.md](../docs/architecture/action/spec-action-model.md) §2, including
    `kind`, `rung`, `cooldown_channel`. Scaling values are `ValueSpec` — **not** a second mechanism.
  - Acceptance: a row round-trips; **each rule rejects a planted bad row naming it** — unknown
    `container_id`, unknown `resource_id`, `min_range > max_range`, unknown `kind`/tag. Reject, never coerce.
  - Verify: `--filter ~ActionModel`, `guard-dal.ps1`

- [ ] **T2: costs and effect scopes** · **S**
  - `rpg_action_cost(action_id, resource_id, amount_spec, when)` · `rpg_action_effect_scope(action_id, atom_id, scope)`.
  - Acceptance: **six resource ids asserted, not five**; a scope naming an atom the container lacks is
    rejected; an atom with no scope row defaults to `eachTarget`; `when` round-trips.
  - Verify: `--filter ~ActionModel`

- [ ] **T3: `rpg_action_grant` + the two flags** · **M**
  - Its **own table** — not `effect_binding`, whose `instance_id` is `TEXT NOT NULL`. Reuses the **seven**
    owner scopes and the `source` withdraw key.
  - Acceptance: **a schema test asserts there is no `instance_id` column** — the correction, made
    unforgettable. `grantable` and `default_attack_eligible` are **independent**, proven by a planted row
    with `grantable = 1, default_attack_eligible = 0`. Resolution is intrinsic ∪ granted, **ordinal**,
    asserted against a shuffled input. A withdraw by `source` removes only that source's actions.
  - Verify: `--filter ~ActionModel`, `guard-dal.ps1`

- [ ] **T4: rung table — parse, index, multipliers** · **M**
  - `data/tuning/action-rungs.v1.json`. **Per-mille integers only** — the exponent form documents how the
    values were derived and is **never evaluated at runtime**.
  - Acceptance: a gap in the `rung` sequence rejects naming the index; zero rows rejects; **no `Math.Pow` in
    `Core/Actions/Rungs/`** (architecture test); `RungMultipliers` resolve is **zero-alloc**.
  - Verify: `--filter ~RungTable`, `audit-magic-numbers.py --domain action-rungs`

- [ ] **T5: the monotonicity assertion** · **S**
  - Prices every rung through E9's `PowerVector`.
  - Acceptance: monotonic on the shipped ladder, **and a planted inverted row FAILS**. Cost span exceeds
    power span, asserted as a number. `_meta.measurable` records whether `P0.3` has landed.
  - Verify: `--filter ~RungTable`

### ✅ Checkpoint 1
- [ ] Every validator rejects a planted row · schema test finds no `instance_id` · planted inverted rung fails

---

## Phase 2 — targeting and usability (`A2`, `A4`)

- [ ] **T6: typed target spec and its compiler** · **M**
  - `ActionTargetSpec` compiled to **`TargetSpec[2]`** (one per caster side) plus a filter predicate over
    `BoardEntitySnap` — the shipped `FilterPool` **re-parses its dictionary on every resolve**, and `A7`
    calls it per candidate.
  - Acceptance: **one authored action serves both factions** — `Relation = Enemy` compiled for a plant and
    a zombie caster picks opposite pools from the same row. Unknown filter keys rejected.
  - Verify: `--filter ~ActionTargeting`

- [ ] **T7: `GridDistance` and the range gate** · **M**
  - Chebyshev, one implementation, two callers.
  - Acceptance: **with no board every range check PASSES** — not empty, not throwing. A `Square` of size *n*
    contains exactly the cells within radius `(n−1)/2`. The gate is a **stable filter**, asserted with
    in-range members non-adjacent in sort order.
  - Verify: `--filter ~ActionTargeting`

- [ ] **T8: the `target` RNG stream** · **S**
  - `SeededRng.DeriveStream(seed, "target")` — the battle names `initiative`, `crit`, `essence`, `status`
    and **no `target`**, so `Mode = Random` today is nondeterministic or silently desyncs another stream.
  - Acceptance: the gate applies **before** the random pick — same seed, one target moved out of range, and
    the survivors match the in-range subset rather than a reshuffle. **A gate applied after the pick passes
    a naive test and fails this one.**
  - Verify: `--filter ~ActionTargeting`

- [ ] **T9: the six gates** · **M**
  - **stance → bound → cooldown → afford → range → condition**, cheapest first, short-circuiting, typed
    refusals. Affordability is an `IAffordabilityCheck` **seam** returning affordable until `A3`.
  - Acceptance: each gate refuses with **its own** reason; an action both on cooldown and unaffordable
    reports `OnCooldown`, proving order; **`FactReader.Reads` is zero when an earlier gate refuses**;
    evaluation allocates **zero bytes**; position leaves are **false, not throwing**, with no board.
  - Verify: `--filter ~ActionUsability`

- [ ] **T10: `holdsStock` wiring** *(after `P0.4`)* · **S**
  - Acceptance: battle mode resolves at assembly; **lawn mode refuses to bind a consumable action**, with a
    typed reason — an unsupported mode named, never one left unstated.
  - Verify: `--filter ~ActionUsability`

### ✅ Checkpoint 2
- [ ] `FactReader.Reads` == 0 on early refusal · zero-alloc evaluation · one row serves both factions

---

## Phase 3 — the proof (`A5`) ⚠️ freezer window

- [ ] **T11: parity capture — before any engine change** · **M**
  - Record per-stream draw **values** (`initiative`, `crit`, `essence`, `status`), target ptr per attack,
    signed delta per apply, across the eight golden fixtures, via `BattleTrace`.
  - Acceptance: fixtures captured **while the engine is untouched**. **Counts alone are insufficient** — a
    count-matching, value-differing run is exactly the failure this exists to catch.
  - Verify: `--filter ~BasicAttackAdoption`

- [ ] **T12: the three envelope gaps** · **M**
  - Duration `min`/`max` bounds · a cooldown-reduction channel · `interrupt_cooldown_milli` (default
    `1000‰`) replacing `ActionRunner.Interrupt`'s current no-cooldown behaviour.
  - Acceptance: all three additive and **inert for a zero envelope**; goldens unmoved; an interrupted
    channel now pays a cooldown, asserted directly.
  - Verify: `--filter ~TurnFsm` + goldens

- [ ] **T13: the basic attack as a declared action** · **L**
  - Authored row, intrinsic binding, engine inner loop calling the action path. **Scope is the first four
    steps only** — active check, CC-lock, target, `Compute`. The trait tail stays engine code.
  - Acceptance: **seven hazard fixtures**, each engineered so that "improving" the behaviour turns it red —
    draws inside `OrderBy`; CC-locked actors still draw; no-target **`break`**; miss **`continue`** with the
    crit stream already advanced; essence draws only on a landed hit; one `host.Flush()` per attack; element
    components from `attacker.AttackComponents`. Plus `SourceOrder` vs `OrdinalPtr` producing different
    targets where the two disagree.
  - Verify: full Core + goldens

- [ ] **T14: verification, and the grant path closes** · **M**
  - Acceptance: **eight goldens byte-identical** · `RulesetVersion` still 2 · content hash unmoved · **six
    suites green with no test edited** · four boundary guards green.
  - **And the finding:** `resource.delta` and `shield.grant` go **Full** in battle, because an action applies
    its atoms directly at its resolve tick — the "grant path" both `D6` comments wait on. Asserted, not
    claimed.
  - Verify: all suites + all guards

### ✅ Checkpoint 3 — report, do not re-bless
- [ ] 8 goldens byte-identical · `RulesetVersion` 2 · six suites green with **no test edited**
- [ ] Two runtime-support cells flip to `Full`, asserted
- [ ] ⚠️ A moved golden here means **the model is wrong**. `BattleGoldenTests` already refuses a silent
      re-bless, so this is a red test rather than a decision.

---

## Phase 4 — costs and pools (`A3`)

- [ ] **T15: the resource reader** · **M**
  - The channels are **already registered**; this is their reader. Lazy regen:
    `value(now) = clamp(stored + rate × (now − lastTick), 0, max)`.
  - Acceptance: **six ids asserted** · **lazy regen == scheduled regen** (one resolve after 1000 ticks vs a
    thousand one-tick steps) · **zero scheduled events** for five regenerating pools across 200 actors,
    **counted** · at battle end pools resolve and `lastTick` is **dropped**.
  - Verify: `--filter ~Resource`

- [ ] **T16: exhaustion as a status** · **M**
  - Reuses `StatusRuntime`. The debuff is a **container of atoms**, never a hardcoded channel list.
  - Acceptance: **one status apply, not one per tick**, counted — the final state is identical either way,
    which is what hides the bug. **Re-evaluates on read**, proven by crossing the leave threshold with **no
    write**. A self-regen cycle is **rejected at load**, and `poise` exhaustion must not touch
    `resource.regen.poise`.
  - Verify: `--filter ~Resource`

- [ ] **T17: paying** · **M**
  - Validate all → consume all → roll back on any failure. `when` = `onCommit` | `perTick`.
  - Acceptance: rollback asserted **per pool**, not in aggregate — an aggregate assertion passes when two
    errors cancel. A `perTick` cost that cannot be paid ends the action through the interrupt path. Cost
    scales with `Θ`; **cooldown identical** at `Θ`=20 and `Θ`=5,000.
  - Verify: `--filter ~ActionCost`

- [ ] **T18: run pools and rest** · **L**
  - Acceptance: pools survive an encounter boundary and refill at rest, `hp` included; **no run row means a
    run of one**; cooldowns do **not** cross a battle boundary.
  - Verify: `--filter ~Resource` + Data suite

### ✅ Checkpoint 4
- [ ] Zero timers at 200 actors · one exhaustion apply · rollback per pool

---

## Phase 5 — progression (`A11`, `A16`)

- [ ] **T19: the unlock ladder** · **M**
  - `earnCount` increments **only on a successful acquisition into a free slot**.
    `chance(n) = max(floor, p1·δ^(n−1))` · `rung(n) = min(earnCount, cap)`.
  - Acceptance: chance at earns 1, 11, 40, 50 matches the table and **earn 50 is AT the floor**;
    **`floor = 0` is rejected at load** (a zero floor is a hard progression ceiling); a roll with no free
    slot is **not an earn** and does not advance the ratchet; `earnCount` is `long`.
  - Verify: `--filter ~UnlockLadder`, `audit-overflow.py`

- [ ] **T20: discard** · **M**
  - Flat tax in `soul`. Always available, always priced, never on a cooldown, never capped — **refused
    during a run**, matching the shipped equip gate.
  - Acceptance: **discard then re-earn does NOT restore chance**, asserted against the pre-discard value —
    **this is the anti-farm test; without it the module has no teeth**. A planted occupancy-keyed rung
    **fails**. Insufficient `soul` → typed refusal and **no state change**.
  - Verify: `--filter ~UnlockLadder`

- [ ] **T21: the loadout set** · **M**
  - `rpg_actor_loadout`, ≤5 skills, four validation rules.
  - Acceptance: a 6th entry **rejects and truncates nothing**; an unheld action rejects; a `basic`/`innate`
    entry rejects as a **category error**; fewer than 5 held is **legal, not padded**; a mid-run change is
    refused.
  - Verify: `--filter ~Loadout`, `guard-dal.ps1`

- [ ] **T22: auto-equip** · **M**
  - Power-ranked, ties on `action_id` ordinal. **Every actor with no loadout row auto-equips** — a Zomboss
    pattern must never fight with three basics.
  - Acceptance: deterministic across two runs **and across a shuffled input order**; equal-power tie-break
    asserted with two deliberately equal actions; **the power score reaches nothing but the ranking**
    (architecture test); **the auto-equipped set appears in the battle report** — otherwise a dominant
    auto-loadout is invisible to a matrix that compares allocations, not loadouts.
  - Verify: `--filter ~Loadout`

### ✅ Checkpoint 5
- [ ] Discard does not restore chance · auto-equip order-independent · report carries the auto set

---

## Phase 6 — grants (`A15`)

- [ ] **T23: action-set assembly** · **M**
  - intrinsic ∪ granted → dedupe keeping provenance → resolve default attack → enforce cap → **ordinal**.
  - Acceptance: an actor with no items has exactly three basics + innate; **two items granting one action →
    one entry, two rows**; removing one source leaves the action; an already-known grant is **reported, not
    silently swallowed**; assembly order asserted against a **shuffled** grant list.
  - Verify: `--filter ~GrantSeam`

- [ ] **T24: lifecycle and cap policy** · **M**
  - Acceptance: over-cap **rejects naming the item and truncates nothing**; un-equip mid-action lets the
    action **complete**, and an **architecture test asserts no inventory type reaches `InterruptCause`**; a
    grant arriving mid-run does not change the assembled set; a second assembly call in one run returns the
    identical set.
  - Verify: `--filter ~GrantSeam`

### ✅ Checkpoint 6
- [ ] All nine handshake items tickable by the item lane · no inventory type reaches the kernel

---

## Phase 7 — defence (`A8`) *(after Checkpoint 3)*

- [ ] **T25: the stance runtime** · **M**
  - Raise (ordinary action) → held (self-status) → release (**its own `action_id`**). **No new FSM state.**
  - Acceptance: **at `W = 1` one actor guards while another acts — and a planted `slot_consuming` hold
    FAILS**; every other action including movement is refused with a typed reason; an architecture test
    asserts `TurnState` is unchanged.
  - Verify: `--filter ~DefenceAction`

- [ ] **T26: the `poise` economy** · **M**
  - Flat commit + absorb drain + **per-tick hold**.
  - Acceptance: **two mutual guards TERMINATE, and a planted zero-hold version HANGS**; `poise` at zero is
    exhaustion, **not death**; `r = poiseRegen / peerPressure < 1` asserted from **emitted metrics** across
    two seeded scenarios — one heavy-hit (must break), one attrition (must not).
  - Verify: `--filter ~Poise`

- [ ] **T27: the riposte** · **S**
  - Acceptance: spent `poise` converts to damage; the share is a **bounded ratio over an uncapped pool**
    with its PS-8 comment; output **scales with `Θ`**; a guarded actor blocks measurably more often.
  - Verify: `--filter ~DefenceAction`, `audit-overflow.py`

### ✅ Checkpoint 7
- [ ] Mutual guards terminate · planted zero-hold hangs · `r < 1` from metrics · goldens unmoved

---

## Phase 8 — duration (`A14`)

- [ ] **T28: the seam and the clamp** · **M**
  - `IDurationResolver` + clamp-and-convert. **The clamp is the LAST step of Phase 2, after
    `durationNetFactor`.**
  - Acceptance: a duration-stacking actor is **still bounded** — **a planted authoring-time clamp FAILS**;
    at the bound further rungs raise **intensity** and total effect keeps rising; DoT and buff families
    resolve in **ticks**, and a turn-authored DoT is rejected; **no resolver registered → throws naming the
    mode**, never silently defaults.
  - Verify: `--filter ~DurationResolver`

- [ ] **T29: `BattleDurationResolver`** *(after `P0.5`)* · **S**
  - Acceptance: two actors differing 2× in `turn.speed` resolve the same authored "2 turns" to **different
    tick counts**; `Θ`=20 vs `Θ`=5,000 resolve **identical** turns; no float crosses `ToTicks`.
  - Verify: `--filter ~DurationResolver`

### ✅ Checkpoint 8
- [ ] Planted authoring-time clamp fails · `Θ` never moves a resolved turn count

---

## Phase 9 — catalog and generation (`A6`, `A13`)

- [ ] **T30: catalog load, compile, cache, hash** · **M**
  - Server-side only — **no push**; actions are battle-mode and the injector never sees one.
  - Acceptance: a malformed row fails **at load naming the row**; **no JSON parsed after load**; a changed
    action value **changes** the content hash and an unchanged catalog does **not** (both directions); a
    revision swap is atomic so a battle in flight keeps its catalog; **structure exceeding a rung's budget
    is rejected naming the rung and the axis**.
  - Verify: `--filter ~ActionCatalog`, `guard-dal.ps1`

- [ ] **T31: the runtime generator** · **L**
  - The loot model: seed → pool → atoms → variant → composed name. **Names come from templates, never a
    model** — nothing calls anything non-deterministic mid-roll.
  - Acceptance: same seed, two generations → **byte-identical** pools; a channel with no authored
    `sharePermille` **rejects at import, never defaults**; two halves of a multiplicative pair in one
    container are rejected by `group`; `Mode = Area` with no board **rejects at bind time**.
  - Verify: `--filter ~ActionSeeding`

- [ ] **T32: enabler/payoff coverage** · **M**
  - Acceptance: **every conditional payoff in a generated pool has an enabler in the same pool**, asserted
    **in Core** — with a **planted unpaired pool failing**. Not deferred to a dev tool that does not exist.
  - Verify: `--filter ~ActionSeeding`

### ✅ Checkpoint 9
- [ ] Byte-identical generation for a seed · planted unpaired pool fails · unauthored share rejects

---

## Phase 10 — selection (`A7`)

- [ ] **T33: the `IBattleView` seam** *(before the AI)* · **S**
  - Acceptance: an **architecture test fails if the intent source touches battle state directly**. Written
    first, because the seam erodes on the first convenient shortcut and fog then stops being a swap.
  - Verify: `--filter ~ActionSelection`

- [ ] **T34: the stub AI** · **L**
  - Pursue nearest, act to kill, move if out of reach, **pass** if nothing works. Preference key is the
    stub's own — **not `priority_band`**, a scheduling concept.
  - Acceptance: with every actor unable to declare, the battle **TERMINATES rather than hanging** — the
    sharpest test here, since a hang is a stopped clock. Ties identical across two runs **and across a
    shuffled actors list**. Zero allocation per decision at 200 actors. **`FactReader.Reads` scales with
    targets, not actions × targets** — a correct-but-unhoisted implementation passes every behavioural test
    and fails this one. Gate 0 is **hoisted out of both loops**.
  - Verify: `--filter ~ActionSelection` + goldens

### ✅ Checkpoint 10
- [ ] Battle terminates when nobody can declare · reads scale with targets, not the product

---

## Deferred — specced, not scheduled

- [ ] **A9 movement-actions** — waits on `A10`. One row, no new runtime.
- [ ] **A10 battle-board** — owner deferral; built with the board map / battle area.
- [ ] **A8's reaction lane** — waits on timeline **B6**. The *stance* half ships in Phase 7.
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
