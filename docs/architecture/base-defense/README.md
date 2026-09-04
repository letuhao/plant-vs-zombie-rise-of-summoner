# Base-defense module specs

**Twenty-one** specs, one per module id in [base-defense-map.md](../base-defense-map.md). The map is
the index of what exists; **never guess which spec is active from a filename**.

⛔ **Read [_completeness-audit.md](_completeness-audit.md) before building anything.** The first
seventeen specs missed 7 owner decisions and got 3 things outright wrong; the audit records what
changed and why, including the two corrections this session had already made once before.

**Ideal:** [base-defense-ideal.md](../base-defense-ideal.md) — **34** owner decisions across eight
rounds, plus a four-lens audit.
**Structure content:** [structure-seed-ideal.md](../structure-seed-ideal.md) — its own program
(decision 30). This program consumes its corpus and ships against four hand-authored rows until it
lands.
**Plan / tasks:** `tasks/base-defense-plan.md` · `tasks/base-defense-todo.md` (the prefixed pair;
`tasks/plan.md` is the perf stream's and is never a fallback).

---

## Build order

| Level | Modules | Golden risk |
|---|---|---|
| — | **Gate 0** — re-inventory (done, see map) + extend the determinism guard to `Core/Battle`/`Core/Effects` | — |
| 0 | [battle-clock-profile](spec-battle-clock-profile.md) · [siege-supply](spec-siege-supply.md) · [world-graph-diff](spec-world-graph-diff.md) | none |
| 1 | [siege-board](spec-siege-board.md) | none |
| 2 | [siege-pathing](spec-siege-pathing.md) · [district-layout](spec-district-layout.md) · [siege-seam](spec-siege-seam.md) | none |
| — | **Gate A** — the seam holds, zero world goldens moved | |
| 3 | [structure-state](spec-structure-state.md) · [combatant-kind](spec-combatant-kind.md) · [siege-objective](spec-siege-objective.md) | ⛔ **the one golden-locked landing** |
| 4 | [siege-positions](spec-siege-positions.md) · [siege-waves](spec-siege-waves.md) | none |
| 5 | [siege-cover](spec-siege-cover.md) · [siege-construction](spec-siege-construction.md) | conditional rows only |
| 5b | [siege-obstacles](spec-siege-obstacles.md) | none |
| 6 | [siege-economy](spec-siege-economy.md) · [siege-ai](spec-siege-ai.md) | conditional rows only |
| 7 | [siege-resolver](spec-siege-resolver.md) · [siege-engagement](spec-siege-engagement.md) | ⭐ **playable and CI-provable here, with no FE** |
| — | **Gate B** — a siege resolves deterministically, resolver at **both** call sites | |
| 8 | [board-render](spec-board-render.md) → [siege-stage](spec-siege-stage.md) | FE only |

## The nine things most likely to be got wrong

**Four found by the completeness audit** — each was already wrong in a shipped spec:

1. **There is a win condition, and it is not "kill everything".** Clear the **Core** of animate
   defenders. A defender with soldiers in the outer ground has not lost; an attacker who cleared the
   Core has won with enemies still behind them. [siege-objective](spec-siege-objective.md).
2. **The field cap is not a work bound.** It is *"the difficulty dial, a single integer in a config
   file"* — authored per base tier, identical both sides, and **never derived from empty cells**, or
   the defender shrinks the attacker's army by building walls.
3. **A siege is many engagements, one per map turn.** `Spent` is the *normal* outcome of a real siege,
   not an edge case. [siege-engagement](spec-siege-engagement.md).
4. **A unit gets action POINTS, not one action.** Move, attack and build are three peers
   (decision 14); `OneActionPerTurnEconomy` makes crossing a board take 24 turns with no fighting.

**Five from the original set:**

5. **`SiegePhase` already exists and means something else** — clearing a slot guard. The new work is
   `BattleKinds.District` + `DistrictAssaultPhase`. See [siege-seam](spec-siege-seam.md).
6. **The resolver must be supplied at BOTH `RpgStore.WorldTurns.cs:509` AND `:603`.** Wiring only
   `:509` makes every re-derived turn report disagree with what happened, and the bug looks like a UI
   problem. See [siege-resolver](spec-siege-resolver.md).
7. **Board dimensions are NOT on `P(Θ)`.** `P(1) = 106` at the shipped dial, so a Θ-scaled board
   saturates on turn one. They are flat, board-bounded tunables. See
   [district-layout](spec-district-layout.md) §2.
8. **The kind discriminator is plain `[JsonIgnore]`, not `WhenWritingDefault`** — two shipped
   precedents on the same record, both recording the same golden incident. See
   [combatant-kind](spec-combatant-kind.md).
9. **Map step = turn, battle step = round, never converted.** And engagement is the third clock —
   one turn's worth of rounds. See [siege-stage](spec-siege-stage.md) §4.

## Open questions, by module

**Six open**, all recommendation-backed and none blocking a build. **Three resolved** by the owner
2026-09-04 as decisions 31–34.

| Module | Question | Recommendation |
|---|---|---|
| [siege-supply](spec-siege-supply.md) | Ration a besieged garrison's top-up? | No — not on a defect fix |
| [siege-board](spec-siege-board.md) | Diagonal move cost? | Same as orthogonal; dial defaults to 0 |
| [district-layout](spec-district-layout.md) | Where do defenders start? | Deterministic default now, deployment UI later |
| [siege-obstacles](spec-siege-obstacles.md) | — | none open |
| [siege-cover](spec-siege-cover.md) | Cover as defender dodge or attacker accuracy? | Dodge — the channel that already exists |
| ✅ **resolved** | The cover-seeking AI | **Overridden by the owner — decision 31.** Risk term stays |
| ✅ **resolved** | Which Θ for structure HP? | **Sector `DevelopmentLevel` × an authored material tier** — decision 32. Plus decision **33**: `structure-seed` needs a deterministic planner before any model call |
| ✅ **resolved** | The bulk material's name | **`rubble`** — decision 34 |
| [siege-construction](spec-siege-construction.md) | Is `ironwork` tradeable? | Along supply lines only — the blockade then falls out of `siege-supply` |
| [board-render](spec-board-render.md) | Serve `battle` too? | `siege` only, kept generic |
| [siege-stage](spec-siege-stage.md) | Abandoned in-progress siege? | Auto-resolve with `siege-ai` |
