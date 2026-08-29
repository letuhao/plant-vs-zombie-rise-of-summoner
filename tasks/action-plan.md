# Plan: action program

**Rewritten 2026-08-27** against the sealed [action-ideal.md](../docs/architecture/action-ideal.md),
the revised [map](../docs/architecture/action-map.md) (16 modules) and
[audit-2026-08-27.md](../docs/architecture/action/audit-2026-08-27.md) (11 findings, all resolved).

Task list: [action-todo.md](action-todo.md). Paths are prefixed because `tasks/plan.md` and `tasks/todo.md`
hold **Perf v3**.

---

## 0. Two owner instructions that shape this plan

**2026-08-27, and they change how it is written:**

> *"Don't add a gate if it blocks the build."*
> *"Better to build then tune later by tunable variable, instead of trying to build a perfect system
> without data to prove it perfect."*

### 0.1 No human gates. Mechanical assertions only.

The previous plan had two ⛔ checkpoints that **stopped and waited for a sign-off**. Those are gone.

| | Old shape | This plan |
|---|---|---|
| Byte-identity | ⛔ *"stop, do not bless"* | `BattleGoldenTests` **already refuses a silent re-bless**. A moved golden is a **red test**, which stops the build without stopping the builder |
| Balance | ⛔ owner sweep sign-off | a **recorded number** in a baseline file, diffed by a script |

**A checkpoint here means: run these commands, record the numbers, continue.** The only stop is a failing
test — and a failing test is information, not a queue.

### 0.2 Every balance number ships as a tunable with a working value

This is not a shortcut; it is the repo's own standard. PS-7: *"being wrong costs a config version, not a
refactor."* `tier-bands.v1.json` says it of itself: *"working values chosen to make the corpus resolvable,
**not a validated balance decision**."*

So every number below — `p1`, `delta`, `floor`, `cap`, the rung multipliers, the cost tax, the predicate
floor — **lands with a starting value and a declared metric**, and is solved from play data later. **No task
waits on a measurement.**

> The one thing that is *not* deferred is a number's **shape**: `long` not `float`, per-mille not
> fractional, tunable not `const`. Getting the shape wrong costs a refactor; getting the value wrong costs a
> file save.

---

## 1. Shape of the work

**16 modules, 11 phases, ~36 vertical slices.** Every slice is one complete path — row to read, or seam to
consumer — never a horizontal layer.

```text
P0 prerequisites (2 other programs + our guard)
    |
P1 the row + the ladder        A1 A12
    |
P2 targeting + usability       A2 A4
    |
P3 THE PROOF                   A5        <- freezer; nothing else lands in this window
    |
P4 costs + pools               A3
    |
P5 progression                 A11 A16
    |
P6 grants                      A15
    |
P7 defence                     A8
    |
P8 duration                    A14
    |
P9 catalog + generation        A6 A13
    |
P10 selection                  A7
    |
deferred                       A9 A10
```

### 1.1 Three orderings that are not negotiable, and why

1. **`P0.1` (purity guard) before the first line of `Core/Actions/`.** Audit C1 of the *previous* audit:
   that directory has no determinism enforcement. A file landing before the guard is a file nobody checked,
   and wall-clock or ambient-RNG damage is invisible until a replay fails.
2. **Parity capture (`T11`) before any engine change.** You cannot prove byte-identity against a baseline
   you did not record.
3. **`A3` after `A5`, and `A8` after `A5`'s window closes.** `decisions.md` Golden ordering: *"freeze first,
   move last — if a mover overlaps a freezer, neither can attribute a hash change to its own work."*
   **This is a sequencing rule, not a gate** — nothing waits on a person.

Everything else may be reordered if it helps.

### 1.2 Phase 0 is work, not a gate

**Owner, 2026-08-27:** *"we will extend atom effect before we build any action — so build order is extend
dependencies first."* That is the order, and the plan follows it.

But three of the five prerequisites have **seams that let the dependent slice ship without them**, which is
what keeps this an order rather than a blocker:

| Prerequisite | If it is late |
|---|---|
| `P0.2` linkage | only linked actions wait. Nothing else in 36 slices touches it |
| `P0.3` predicate pricing | `T5`'s monotonicity assertion ships with `_meta.measurable` recording its state, and is re-run when pricing lands |
| `P0.5` `turn.speed` | `A14` ships `IDurationResolver` + the clamp; only `BattleDurationResolver` waits |
| `P0.4` `holdsStock` | only consumable actions wait |
| **`P0.1` purity guard** | **no seam. This one really is first** |

---

## 2. Phases

### Phase 0 — prerequisites

`P0.1` is ours and blocks the first line of action code. `P0.2`–`P0.5` belong to two other programs; this
program supplies the requirement and the tests.

### Phase 1 — the row and the ladder (`A1`, `A12`)

Five slices, each row → store → read. `A12` lands **with** `A1` rather than after it, because `A3` and `A11`
both read the rung and two readers of one table is why it exists separately.

**`T3` is easy to skip and expensive to add later** — `rpg_action_grant` is the correction another program
found, and the item lane is blocked on it.

### Phase 2 — targeting and usability (`A2`, `A4`)

`T7`'s **no-board pass-through is the single line `A5`'s freeze rests on.** Asserted here and again in `A5`
— deliberately twice, because one test proves the rule and the other proves the freeze depends on it.

Gate order is not style: it is what lets `A7` hoist per-actor and per-action work out of the target loop,
and it is asserted by **read count**, not by reading the code.

### Phase 3 — the proof (`A5`)

The byte-identity slice. **This is the freezer**, so `A3`, `A8` and `A13` all sit outside its window.

`T14` is where the two shipped `D6` comments close: if an action's atoms resolve in battle, `resource.delta`
and `shield.grant` go **Full** there.

### Phase 4 — costs and pools (`A3`)

The pools are **already registered** (`DerivedStatRegistry.cs:165-171`); this phase is their **reader**.

### Phase 5 — progression (`A11`, `A16`)

`A16` lands with `A11` because a held pool nothing can equip from is not testable, and because **auto-equip
is what lets every non-player actor arrive equipped**.

### Phase 6 — grants (`A15`)

Closes the nine-item handshake. `T23`'s assembly is the entry point the item lane is explicitly forbidden
from implementing.

### Phase 7 — defence (`A8`)

Guard as a stance. **Not blocked on timeline B6** — but it lands after `A5`'s window, per §1.1(3).

### Phase 8 — duration (`A14`)

Ships the seam and the clamp; the battle resolver waits on `P0.5`.

### Phase 9 — catalog and generation (`A6`, `A13`)

`A13` is the **runtime** generator — the loot model. Seedsmith is a dev tool and comes **after** this whole
program.

### Phase 10 — selection (`A7`)

`T35` before `T36`: the `IBattleView` seam erodes on the first convenient shortcut if the AI is written
first.

---

## 3. Checkpoints — all reporting, none blocking

| After | Record | Red means |
|---|---|---|
| **P1** | schema round-trips; every validator rejects a planted row | a validator that cannot fail |
| **P2** | `FactReader.Reads` per gate; zero-alloc evaluation | the hoist is not happening |
| **P3** | 8 goldens byte-identical · `RulesetVersion` 2 · six suites green **with no test edited** | the model is wrong — **not** a re-bless |
| **P4** | lazy regen == scheduled regen; **zero** timers at 200 actors | a scheduled-event regression |
| **P5** | discard does not restore chance; auto-equip deterministic across shuffled input | the ratchet leaks |
| **P6** | all nine handshake items tickable by the item lane | the seam is still one-sided |
| **P7** | `r = poiseRegen / peerPressure < 1` from **emitted metrics** | guard is unbreakable |
| **P8** | a duration-stacking build stays bounded | the clamp is in the wrong place |
| **P9** | every conditional payoff has an enabler in its pool | the discount pays for an unreal combo |
| **P10** | battle **terminates** when nobody can declare | a hang, which is a stopped clock |

**None of these waits on a person.** Each is a command that exits non-zero.

---

## 4. Risks

**The parity harness is the whole program's insurance.** If `T11` is thin, `T14` can only say *"the hashes
match"* — and when they do not, there is no way to tell **which** draw moved. Record values per stream, not
counts.

**`A7` is golden-neutral only while there is no board.** With no coordinates, "nearest" falls back to source
order, which is what `SelectTarget` already does. The moment `A10` lands, this module starts moving hashes.

**This program does not prove `W`.** No wave-1 action consumes a slot in a way that exercises concurrency
width. *"The slot tests pass"* must not be mistaken for coverage that does not exist here.

**Auto-equip is invisible to the dominance guard.** That matrix compares **allocations, not loadouts**, so
`T22` records the auto-equipped set in the report — otherwise a dominant auto-loadout ships green.

---

## 4a. Reopened 2026-08-28 — A17–A20 (Phase 11)

The plan above closed with the action program built but never wired into a real battle — proven by
a completeness audit, not assumed. **This reopening's whole point is Checkpoint A/C's own promise,
delivered for real:** `BattleEngine` calling `StubIntentSource`/`ActionCatalog` at runtime instead
of its own hardcoded `SelectTarget`. Full scope, the two explicit owner decisions (full switch-over;
full multi-action loadouts), and what stays deferred: [action-map.md](../docs/architecture/action-map.md)
§12. Tasks: `action-todo.md` Phase 11 (T35–T39). Module spec:
[spec-action-selection-adoption.md](../docs/architecture/action/spec-action-selection-adoption.md).

**Unlike the plan above, this is explicitly a golden-mover**, not gated by "don't block the build" —
it needs its own re-bless, predicted delta, and win-rate sweep (§12.2's golden-ordering rule),
following this repo's own established discipline for a deliberate change rather than skipping it.

**Closed 2026-08-28.** T35-T39 landed; Checkpoint E closed on the finding that the switch-over was
byte-identical for every battle that exists today (zero goldens moved, measured not assumed) —
`RulesetVersion` held at 4 by owner choice. See `action-todo.md` Phase 11 for full evidence.

## 4b. A18 split into A18a–e (2026-08-28, Phase 12)

A18 ("resolve whichever action A17 chose") turned out to bundle five independently testable
capabilities once specced — a genuine Phase 0 case, not a stylistic split. Same shape as
`effect-atom-map.md`'s own E14a/E14b precedent. Capability map: `action-map.md` §12.1 (module table,
dependency order, Checkpoints F). Module specs, all written and adversarially audited against the
real code this session (two load-bearing bugs found and fixed before any code was written — see each
spec's own corrected design, and `spec-battle-status-apply.md` §1 / `spec-battle-live-stat-modifiers.md`
§1 for the specifics):

| id | Spec | Owns |
|---|---|---|
| A18a | [spec-action-container-binding.md](../docs/architecture/action/spec-action-container-binding.md) | The ephemeral binding seam |
| A18b | [spec-on-activate-trigger.md](../docs/architecture/action/spec-on-activate-trigger.md) | New `OnActivate` trigger (7→8) |
| A18c | [spec-battle-resource-shield-grants.md](../docs/architecture/action/spec-battle-resource-shield-grants.md) | `resource.delta` + `shield.grant` execute for real |
| A18d | [spec-battle-status-apply.md](../docs/architecture/action/spec-battle-status-apply.md) | `status.apply` executes for real |
| A18e | [spec-battle-live-stat-modifiers.md](../docs/architecture/action/spec-battle-live-stat-modifiers.md) | Sourced/revertible modifier ledger for `stat.modify` |

**Build order:** A18a → A18b → {A18c, A18d} → A18e → A19. Tasks: `action-todo.md` Phase 12 (T40–T54).
**Architecture decision that binds every module after A18a:** every cross-module dependency is a
settable property forwarded through `BattleEffectHost`, never a constructor parameter — because
`BattleRunState`'s constructor builds `Host` before most of its own other fields exist
(`BattleRunState.cs:115` vs. `Status` at line 117). This is the exact shape T14 already used for
`ShieldGate`; A18d (`Status`/`StatusRng`) and A18e (`Ledger`) both reuse it rather than reinventing a
constructor-injection approach that cannot compile against the real construction order.

## 5. Deferred, and why

| Module | Waits on |
|---|---|
| `A9` movement | `A10` |
| `A10` battle-board | owner deferral — built with the board map |
| `A8`'s reaction lane | timeline **B6** — the *stance* half ships in P7 |
| seedsmith | **after this program**, as a dev tool |
