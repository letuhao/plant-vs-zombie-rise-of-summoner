# Plan: action program

Map: [../docs/architecture/action-map.md](../docs/architecture/action-map.md) · Specs: [../docs/architecture/action/](../docs/architecture/action/) (ten modules) · Audit: [../docs/architecture/action/audit-2026-08-22.md](../docs/architecture/action/audit-2026-08-22.md)

Task list: [action-todo.md](action-todo.md). Paths are prefixed because `tasks/plan.md` and `tasks/todo.md` hold **Perf v3**.

---

## ⛔ Gate 0 — this program does not start until the effect-atom program has built

Owner decision, 2026-08-22: **spec and plan now, build after they build.** Documents reconcile cheaply; code does not.

`A1` carries a foreign key into `effect_container`, `A4` needs two predicate leaves added to `E3`, and `A6` registers tables into `E8`'s hash. All three are approved and written into their specs, and all three are **theirs to land**. Starting before they do means building against a moving contract and reconciling in code, which is the cost this ordering exists to avoid.

**Nothing below is authorized until that gate clears.**

## Shape of the work

Seven modules build; three wait behind dependencies. The spine is:

```
P1 guard  →  A1 model  →  A2 targeting  →  A4 usability  →  A5 PROOF  →  A3 costs  →  A6 catalog  →  A7 selection
                                                             ⛔ byte-identical
```

Three ordering constraints are not negotiable, and each has a reason that has already cost this repo something:

1. **The guard extension comes before the first line of action code.** Audit C1: `Core/Actions/` has no determinism enforcement today. A file landing before the guard is a file nobody checked, and wall-clock or ambient-RNG damage is invisible until a replay fails.
2. **Parity capture comes before any engine change.** You cannot prove byte-identity against a baseline you did not record. The timeline program learned this at B1 and it is the same lesson.
3. **`A3` lands after `A5`, not before.** Costs inside the byte-identity gate would put a behaviour change inside the one module whose entire job is proving nothing changed. `A4` ships its affordability gate as a seam instead.

## Phases

### Phase 0 — prerequisites *(nothing builds before these)*

`P1` extends the purity scan to `Core/Actions/` — purity rules on, tick-path rules off. The mechanism exists (`DiagnosticsExemptFromTickPath` already does this for `BattleTrace.cs`); this is a directory and an exemption entry. It must be **proven able to fail** against a planted `DateTime.UtcNow` before it is trusted, or it is decoration.

`P0` (decisions rows) is already done — three rows landed 2026-08-22.

### Phase 1 — the foundation (A1)

Three vertical slices, each a complete path from row to read:

- **T1** the `rpg_action` table, its record, its validator, and store round-trip
- **T2** costs and effect-scope tables
- **T3** action binding and resolution — intrinsic ∪ granted, ordinal order

`T3` is easy to skip and expensive to add later: it is the first question `A4` and `A7` ask, and neither can be built without it.

### Phase 2 — targeting (A2)

- **T4** the typed spec, its filters, and compilation to `TargetSpec[2]` plus a filter predicate
- **T5** `GridDistance` and the range gate, with the no-board pass-through
- **T6** the `target` RNG stream

`T5`'s **no-board pass-through is the single line `A5`'s freeze rests on.** It is asserted here and again in `A5` — deliberately twice, because one test proves the rule and the other proves the freeze depends on it.

### Phase 3 — usability (A4)

- **T7** the five gates, ordered and short-circuiting, with typed refusals and the affordability seam

Gate order is not style: it is what lets `A7` hoist per-actor and per-action work out of the target loop. It is asserted by **read count**, not by reading the code.

### Phase 4 — the proof (A5) ⛔

- **T8** parity capture — **before any engine change**
- **T9** the three envelope gaps, additive and inert
- **T10** the basic attack as a declared action, engine calling the action path
- **T11** gate verification

### Phase 5 — costs (A3)

- **T12** resource catalog, channels, lazy pools
- **T13** exhaustion as a status, with hysteresis
- **T14** cost validate / consume / roll back, and `perTick`
- **T15** run-scoped pools and rest

### Phase 6 — catalog (A6)

- **T16** load, compile, cache, and hash registration

### Phase 7 — selection (A7)

- **T17** the `IBattleView` seam
- **T18** the stub AI

`T17` before `T18` matters: the seam is what makes deferred fog an implementation swap instead of an AI rewrite, and it erodes on the first convenient shortcut if the AI is written first.

## Checkpoints

| | Gate |
|---|---|
| **⛔ A — byte-identical** | After `T11`. Eight goldens unchanged, `RulesetVersion` still 2, six suites green **with no test edited**. A re-bless here means the model is wrong: **stop, do not bless.** |
| **✅ B — actions are content** | After `T16`. A second and third action exist as rows, with costs, and a changed value moves the content hash. |
| **✅ C — something chooses** | After `T18`. `SelectTarget` has a replacement both auto-battle and interactive enter through, and replay holds across runs and across list order. |
| **⛔ D — the movers** | `A10` + `A9` + `A7`'s distance targeting + fog, together with timeline T9 and atom E12: **one combined re-bless, one sweep, `RulesetVersion` advances once.** |

## Deferred, and why

| Module | Waits on |
|---|---|
| `A8` defence | Timeline **B6**, the reaction lane — unbuilt |
| `A9` movement | `A10` |
| `A10` battle-board | Owner deferral — built with the board map / battle area |

Specced but not scheduled. Each names its dependency rather than being a hole.

## Risks

**The parity harness is the whole program's insurance.** If `T8` is thin, `T11` can only say "the hashes match" — and when they do not, there is no way to tell *which* draw moved. Record values per stream, not counts.

**`A7` is golden-neutral only while there is no board.** With no coordinates "nearest" falls back to source order, which is what `SelectTarget` already does. The moment `A10` lands, this module starts moving hashes — a transition that needs its own fixture rather than being noticed afterwards.

**This program does not prove `W`.** No wave-1 action consumes a slot (audit R2-2). The action → slot path is the timeline's `B12` to prove, and "the slot tests pass" must not be mistaken for coverage that does not exist here.

**The cost ceiling is the sweep, not a frame.** `A7` runs actions × targets every turn of every battle in the win-rate sweep. The target number comes from a measurement, and it is not yet taken.
