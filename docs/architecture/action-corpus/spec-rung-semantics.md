# Spec: rung-semantics (A-U1)

**Status: DRAFTED 2026-09-03** — owner decision, in answer to *"four orphans still have no owner"*:
**one new spec for the rung questions.** Module **A-U1**, action-corpus. **No dependencies.**

**What it owns: making the rung ladder mean what every other spec assumes it means.** Three separate
findings turned out to be one question — *does a rung mean the same thing to the author, the holder and
the guard?* Today it does not, and five specs are written against the assumption that it does.

---

## 1. The three, and why they are one module

| Finding | The defect |
|---|---|
| **Authored vs effective rung** | `StructureBudgetGuard.Check` reads **`row.Rung`** — the authored column — while `effectiveRung` is derived per holder from `earnCount`. **Clamping the derived rung never reaches the guard.** So §5's claim that *"a scope's rung ceiling already gates its structure ceiling as a side effect"* is false |
| **`minRung` unpriced** | The signature window's floor of 5 expands to `max(5, min(earnCount, 10))`, which is **constant at 5 across `earnCount ∈ [0,5)`** — a piecewise function differing from `rung(n)` over a whole interval, for one content class. §4's own table calls that shape *"a second curve. Rejected, and stays rejected."* And rung 5 carries **`costMulti: 3627`**, so a player's first-ever unlock pays **3.6×** |
| **Cap-10 double-use** | `action-unlock.v1.json`'s own `_meta`: *"**cap 10 is both the max held count and the rung ceiling** — one number, two uses."* §4's argument that the ladder can extend rests on the cap being freely tunable. **Raising it 10 → 15 also hands every player 15 held unlocks instead of 5.** A balance change of a different kind, never mentioned |

**They are one module because they are one question.** Fix the guard's rung without pricing `minRung` and
the floor still ships an unpriced 3.6×. Price `minRung` without splitting the cap and the fix is
untunable. Split the cap without fixing the guard and nothing downstream notices.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `rung(n) = min(earnCount, cap)` — one curve, one input | **built** | `UnlockLadder.cs:56-61` |
| `ActionRow.Rung` is a single authored `int` | **built** | `ActionRow.cs:23` — *"Never a magnitude"* |
| `StructureBudgetGuard.Check` resolves `rungTable.TryGet(row.Rung)` | **built** — and reads the **authored** value | — |
| `UnlockLadder.Rung` reachable only via `HeldUnlock.EarnCountAtAcceptance` | **built** | `UnlockState.cs:44` |
| Rung 5 `costMulti` | **3627‰** | `data/tuning/action-rungs.v1.json` |
| `cap: 10` serving two meanings | **built**, and its `_meta` says so | `data/tuning/action-unlock.v1.json` |
| A row for the rung ladder in `ssot-power-scale.md` §11 | ⛔ **absent** | §10: *"a power-shaped number not in this table does not have permission to exist"* |

**Sorted: real gap** on all three. Nothing here is inert wiring — the semantics were never pinned.

---

## 3. The contract

### 3.1 One rung, two readings, both named

**The authored `Rung` and the holder's `effectiveRung` are different values and must stop sharing a
word.** This module names them apart everywhere they are read:

- **`Rung` (authored)** — what the *content* was written for. Fixes the structure budget, because
  structure is a property of the action, not of who holds it.
- **`effectiveRung` (derived)** — `min(earnCount, cap)` for this holder. Fixes magnitude and cost,
  because those scale with the holder's progression.

**`StructureBudgetGuard` reading the authored rung is therefore CORRECT** — and the specs claiming
otherwise are wrong. **What is false is the inference** that clamping the derived rung gates structure.
This module deletes that claim rather than "fixing" a guard that is already right.

### 3.2 `minRung` — priced or dropped, and the default is dropped

A floor that makes `rung(n)` constant over an interval **is** a second curve by §4's own table.

**Default: drop it.** A signature action earned as a first unlock arrives at rung 1, which is flavour the
tier window was never needed to fix — the *window's ceiling* already does the differentiating work
(§36.2's 2.315× per tier), and the floor only prevents a low-rung *instance*, which is exactly what the
ladder is for.

**If it is kept, it must be priced**, and the price stated: at rung 5 the holder pays `costMulti: 3627`
with zero earn history. **That cost is the argument against the floor, and it must appear next to the
floor wherever the floor appears.**

### 3.3 The cap, split

`cap` becomes two named tunables with the same starting value:

| Name | Meaning | Today |
|---|---|---|
| `heldCap` | max simultaneously held unlocks | 10 |
| `rungCap` | the ladder's ceiling — `min(earnCount, rungCap)` | 10 |

**Splitting them at equal values is behaviour-neutral by construction**, which is the point: it changes
nothing now and makes §4's ladder extension possible without silently handing every player five more
held unlocks.

### 3.4 The register entry

The rung ladder and its per-scope windows get their row in
[`ssot-power-scale.md`](../power/ssot-power-scale.md) §10/§11, with the derivation. **§5 constraint 2
promised this and it was never written.** A-G1 also claims the window's entry — **A-U1 owns the ladder's
row, A-G1 owns the budget's; they must cross-reference rather than both claim it.**

---

## 4. What this module must NOT do

- **"Fix" `StructureBudgetGuard`.** It reads the authored rung and that is correct (§3.1). The defect is
  in the specs' inference, not the guard.
- **Invent a third rung reading.** Two, named.
- **Change any shipped coefficient.** Splitting the cap at equal values is behaviour-neutral; a value
  change is a separate, owner-visible decision.
- **Put a rung number in code.** `data/tuning/`, published via `publish.py`.
- **Keep `minRung` silently.** Drop it, or price it where it is stated. Not a third option.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | `StructureBudgetGuard` resolves the **authored** rung, asserted explicitly | §3.1, so the correct behaviour is pinned rather than accidental |
| 2 | `effectiveRung` and `Rung` are **distinct in the type system**, not two uses of one `int` | The names cannot re-merge |
| 3 | With `minRung` dropped, a first-ever unlock of a signature action arrives at **rung 1** | §3.2 |
| 4 | **Planted violation:** re-introducing a `minRung` floor without a stated price **fails** | The rejected shape stays rejected |
| 5 | `heldCap` and `rungCap` are separate keys; setting `rungCap = 15` leaves held capacity at 10 | §3.3 |
| 6 | Splitting the cap changes **no** shipped behaviour — a golden or a snapshot before/after | Behaviour-neutral by construction |
| 7 | The ladder's row exists in `ssot-power-scale.md` §11, pinned by a doc-drift test | §3.4 |
| 8 | **Planted violation:** a rung-derived number with no register row **fails** | §10's rule, mechanically |

---

## 6. Acceptance criteria

1. Authored `Rung` and derived `effectiveRung` are distinctly named and distinctly typed.
2. `StructureBudgetGuard`'s use of the authored rung is **asserted as correct**, and the five specs
   claiming the clamp gates structure are corrected.
3. `minRung` is dropped — or kept with its `costMulti: 3627` cost stated wherever it is stated.
4. `heldCap` and `rungCap` are separate tunables at equal values, with zero behaviour change.
5. The rung ladder has its row in the caps register, held by a drift test.
6. No shipped coefficient moves.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing |
| **A-G1** `tier-access-gate` | Also touches the register. **A-U1 owns the ladder's row; A-G1 owns the budget's.** Cross-reference, do not both claim |
| **A-S1** · **A-S4** · **A-S6** · **A-P3** | All read a rung. Each must say **which** after §3.1 |
| **power** | `ssot-power-scale.md` §11 gains a row; that file is another program's |
| **class-system** | Uses the same `min(earnCount, cap)` shape elsewhere. **Splitting the cap must not leak into its reading** — check before publishing |
