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
| **`minRung` unpriced** — ⛔ **DECIDED 2026-09-03: dropped** (§3.2) | The signature window's floor of 5 expands to `max(5, min(earnCount, 10))`, which is **constant at 5 across `earnCount ∈ [0,5)`** — a piecewise function differing from `rung(n)` over a whole interval, for one content class. §4's own table calls that shape *"a second curve. Rejected, and stays rejected."* And rung 5 carries **`costMulti: 3627`**, so a player's first-ever unlock pays **3.6×**. The floor is A-S1's **authored `rungBand`** floor, not a holder-side clamp — see §3.2 |
| **Cap-10 double-use** | `action-unlock.v1.json`'s own `_meta`: *"**cap 10 is both the max held count and the rung ceiling** — one number, two uses."* §4's argument that the ladder can extend rests on the cap being freely tunable. **Raising it 10 → 15 also hands every player 15 held unlocks instead of 5.** A balance change of a different kind, never mentioned |

**They are one module because they are one question.** Fix the guard's rung without settling `minRung` and
the floor still ships an unpriced 3.6×. Settle `minRung` without splitting the cap and the fix is
untunable. Split the cap without fixing the guard and nothing downstream notices.

⛔ **DECIDED 2026-09-03 — the second row is now closed by deletion, not by pricing.** The floor is
dropped, so there is no 3.6× left to price. What remains of that row is the one-line edit to A-S1's
authored window and the correction of every spec that quoted `[5,10]`. §3.2 carries the decision.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `rung(n) = min(earnCount, cap)` — one curve, one input | **built** | `UnlockLadder.cs:56-61` |
| `ActionRow.Rung` is a single authored `int` | **built** | `ActionRow.cs:23` — *"Never a magnitude"* |
| `StructureBudgetGuard.Check` resolves `rungTable.TryGet(row.Rung)` | **built** — and reads the **authored** value | — |
| `UnlockLadder.Rung` reachable only via `HeldUnlock.EarnCountAtAcceptance` | **built** | `UnlockState.cs:40-46` |
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

### 3.2 `minRung` — ⛔ DECIDED 2026-09-03: DROPPED

A floor that makes `rung(n)` constant over an interval **is** a second curve by §4's own table.

**The decision is: drop it.** The earlier wording offered a fork — *"priced or dropped, and the
default is dropped"* — and a default is not a decision. It is decided.

**First, what the floor actually is, because the earlier wording implied a clamp that does not
exist.** ⛔ **CORRECTED 2026-09-03.** `minRung` **never existed in code or data**: grepped
2026-09-03, there are **zero** hits for `minRung` across `src/` and `data/`. The only `MinRung` in
the tree is `AuraTuning.cs:20` (`public const int MinRung = 7;`), the aura ladder's own consumption
floor, guarded at `AuraTuning.cs:81-85` — a different subsystem, unrelated to actions, and untouched
by this decision.

**The floor at issue is the AUTHORED `rungBand` floor** — the `5` in A-S1's signature window
`[5,10]` (`spec-distribution-planner.md` §3 step 4). That is a number in a spec, written into a
brief, and nothing in the shipped runtime ever read it. So this is not a clamp to remove from
`UnlockLadder`; it is a window to widen in the planner, and `UnlockLadder.cs:56-61` stays exactly as
it is — one curve, one input.

**Why it goes.** A signature action earned as a first unlock arrives at rung 1, which is flavour the
tier window was never needed to fix — the *window's ceiling* already does the differentiating work
(§36.2's 2.315× per tier), and the floor only prevents a low-rung *instance*, which is exactly what the
ladder is for.

**What follows, in three parts:**

1. **A-S1's signature window becomes `[1,10]`.** The `[5,10]` in every spec that quoted it is
   corrected — `spec-distribution-planner.md` §3 step 4 and its §2 example, §3 step 5's table, §5 and
   AC4; `spec-corpus-loader.md` §2's envelope example.
2. **The ceiling does not move, so nothing structural does.** The signature ceiling is still 10, so
   A-S1's union-to-ceiling axis assignment is unchanged — general 2 axes, family 5, signature 6, and
   `restriction` stays the signature tier's one exclusive axis.
3. **`costMulti: 3627` is moot.** It was the price of the floor: rung 5 in
   `data/tuning/action-rungs.v1.json`, paid by a holder with zero earn history. With the floor gone a
   first-ever signature unlock arrives at rung 1 and pays `costMulti: 1000`. Wherever that 3.6×
   appears as the argument against the floor — this file's §1 table, `spec-signature-propose.md`
   hazard 2 — it now records a cost nobody pays, and it is kept only as the reason the floor went.

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
- **Re-introduce an authored `rungBand` floor.** ⛔ **DECIDED 2026-09-03:** the floor is dropped
  (§3.2) and the signature window is `[1,10]`. A floor re-appearing in a spec, a brief or a tuning
  row is the rejected shape returning, and §5 test 4 plants exactly that.
- **Describe `minRung` as something the code once had.** It never existed in `src/` or `data/`
  (§3.2); the only `MinRung` is the aura ladder's, `AuraTuning.cs:20`, and this module does not
  touch it.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | `StructureBudgetGuard` resolves the **authored** rung, asserted explicitly | §3.1, so the correct behaviour is pinned rather than accidental |
| 2 | `effectiveRung` and `Rung` are **distinct in the type system**, not two uses of one `int` | The names cannot re-merge |
| 3 | The **authored** signature window — in `data/tuning/` and in A-S1's emitted briefs — is `[1,10]`, and read against it a first-ever unlock of a signature action arrives at **rung 1**. ⛔ **CORRECTED 2026-09-03:** this test read as though a holder-side `minRung` clamp were being removed. There is none; the floor is the authored `rungBand`, so the assertion is against the authored window and the brief, never against `UnlockLadder` | §3.2 |
| 4 | **Planted violation:** a brief or tuning row carrying a signature `rungBand` whose floor is above 1 **fails**, naming §3.2 | The rejected shape stays rejected |
| 4b | `grep -r minRung src/ data/` returns **zero** hits and the only `MinRung` in the tree is `AuraTuning.cs:20`; a drift test pins both, so a future clamp cannot arrive unnamed | §3.2's correction stays true |
| 5 | `heldCap` and `rungCap` are separate keys; setting `rungCap = 15` leaves held capacity at 10 | §3.3 |
| 6 | Splitting the cap changes **no** shipped behaviour — a golden or a snapshot before/after | Behaviour-neutral by construction |
| 7 | The ladder's row exists in `ssot-power-scale.md` §11, pinned by a doc-drift test | §3.4 |
| 8 | **Planted violation:** a rung-derived number with no register row **fails** | §10's rule, mechanically |

---

## 6. Acceptance criteria

1. Authored `Rung` and derived `effectiveRung` are distinctly named and distinctly typed.
2. `StructureBudgetGuard`'s use of the authored rung is **asserted as correct**, and the five specs
   claiming the clamp gates structure are corrected.
3. **`minRung` is dropped.** ⛔ **DECIDED 2026-09-03** — the earlier "or kept with its cost stated"
   fork is closed. Concretely: A-S1's signature window is `[1,10]` and every spec quoting `[5,10]` is
   corrected; the ceiling stays 10 so no axis count moves; `costMulti: 3627` is recorded as **moot**
   wherever it appears, being the price of a case that no longer occurs; and the specs stop implying
   a holder-side clamp — the floor was the **authored `rungBand`**, and `minRung` never existed in
   `src/` or `data/` (the only `MinRung` is `AuraTuning.cs:20`, the aura ladder's).
4. `heldCap` and `rungCap` are separate tunables at equal values, with zero behaviour change.
5. The rung ladder has its row in the caps register, held by a drift test.
6. No shipped coefficient moves.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing |
| **A-G1** `tier-access-gate` | Also touches the register. **A-U1 owns the ladder's row; A-G1 owns the budget's.** Cross-reference, do not both claim |
| **A-S1** · **A-S4** · **A-S6** · **A-P3** | All read a rung. Each must say **which** after §3.1. **A-S1 additionally owns the authored window**, so §3.2's dropped floor is a one-line change there (`[5,10]` → `[1,10]`) — applied 2026-09-03 |
| **power** | `ssot-power-scale.md` §11 gains a row; that file is another program's |
| **class-system** | Uses the same `min(earnCount, cap)` shape elsewhere. **Splitting the cap must not leak into its reading** — check before publishing |
