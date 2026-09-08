# Base-defense module specs

**Twenty-nine** specs, one per module id in [base-defense-map.md](../base-defense-map.md). The map is
the index of what exists; **never guess which spec is active from a filename**.

⛔ **Read [_completeness-audit.md](_completeness-audit.md) before building anything.** The first
seventeen specs missed 7 owner decisions and got 3 things outright wrong; the audit records what
changed and why, including the two corrections this session had already made once before.

**Ideal:** [base-defense-ideal.md](../base-defense-ideal.md) — **46** owner decisions across eleven
rounds, plus a four-lens audit.
**Structure content:** [structure-seed-ideal.md](../structure-seed-ideal.md) — ⛔ **decision 45 folded
this in as modules 23–28.** The ideal stays as the design record; only the program boundary changed.
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
| 8 | [board-render](spec-board-render.md) → [siege-stage](spec-siege-stage.md) · [battle-stage](spec-battle-stage.md) | FE only |
| c0 | [structure-schema](spec-structure-schema.md) | none — **zero tokens** |
| c1 | [structure-corpus](spec-structure-corpus.md) | none — **zero tokens** |
| c2 | [structure-catalog-import](spec-structure-catalog-import.md) | ⚠️ the one join with the siege family |
| c3 | [structure-instantiate](spec-structure-instantiate.md) · [structure-planner](spec-structure-planner.md) | none — **zero tokens** |
| c4 | [structure-pipeline](spec-structure-pipeline.md) | ⭐ the **first model call** in the program |
| c5 | [structure-metrics](spec-structure-metrics.md) | none |

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

## Open questions

**None.** All seven were cleared by the owner on 2026-09-04 as **decisions 35–46** (ideal §0, round 9).
Every spec's *Open questions* section now reads *None*.

| Was open | Decision | Answer |
|---|---|---|
| Cover: dodge or accuracy? | **35** | ⛔ **Neither** — the **HoMM3 shooting model**: cover area, range penalty, obstruction penalty, projectile kind, and targetable obstacles. `siege-cover` was **rewritten**, not edited |
| Diagonal move cost? | **36** | Legal, same cost. Chebyshev already means it |
| Where do defenders start? | **37** | **Player-placed pre-battle**; the AI places by policy at the same step, so step 7 still needs no FE |
| Is `ironwork` tradeable? | **38** | **Freely.** ⚠️ Gives up material denial as a siege lever — the blockade's teeth are **loam and board income** instead |
| Cover area shape? | **39** | Authored **radius per obstacle kind** |
| Serve `battle` too? | **40** | **Both** — retires the declared-but-unbuilt `battle` id and removes the amendment's third cost |
| Abandoned in-progress siege? | **41** | ⭐ **The engine pauses.** A **wiring gap**, not a build — `BattleSessionState.Disconnected` is already *"preserved and resumable"*, `Resume()` ships at `BattleSessionRegistry.cs:119` |
| Ration a besieged garrison? | **42** | **Yes**, defaulting to `1000` (no-op) so the defect fix stays separately verifiable |
| **Do we store battle state to pause?** | **46** | ⛔ **No.** A paused siege is a **persisted decision log replayed on resume** — following HoMM3's actual reason (a battle is *re-derivable*, so it need not be stored). Makes §2 rule 7 unconditionally true, survives a server restart, and **closes a wiring gap rather than adding a mechanism**. ⚠️ Needs a `decisions_json` **writer**, which does not exist |

**Two of these added real scope.** Decision 35 spans the **battle engine and the action system** (a new
`ProjectilePenalties` flag through all five plumbing sites `RequiresLineOfSight` occupies). Decision 40
means `board-render` is proven by **two** consumers rather than one.
