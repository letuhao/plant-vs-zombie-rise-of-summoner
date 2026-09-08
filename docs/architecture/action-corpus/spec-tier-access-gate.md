# Spec: tier-access-gate (A-G1)

**Status: DRAFTED 2026-09-03** — added by the spec-coverage audit, which found this work **named by five
specs and owned by none**. Module **A-G1**, action-corpus. Depends on **A-S1**.

**What it owns: making decision C1 enableable.** C1 (a tier may gate atom-family access) was adopted
*"with a required, non-negotiable assertion"*, and [`action-corpus-ideal.md`](../action-corpus-ideal.md)
§21.3 rewrote that assertion into three things that must exist first. **Nothing built them.** This module
builds two of the three, names the third's blocker, and owns the second half of the same question — the
structural axes that are equally unenforceable today.

---

## 1. Why this module exists

The coverage audit found `powerBudget` in **five** specs — `distribution-planner`, `general-propose`,
`family-propose`, `signature-propose`, `validate-heal` — and in every one it appears in a *boundaries*
section, as the same restated sentence:

> *"C1's family-access widening is gated on three things that do not exist."*

**Five specs correctly say the gate is closed. No spec opens it.** Until this module lands the generator
emits **structure-gated tiers only**, which is the safe default §21.3 names — and C1, an owner decision,
can never take effect.

**And the failure it guards against is documented, not hypothetical:**

> **The priced thing and the powerful thing were not the same thing.**
> ([`03-composable-skill-systems.md`](../../research/action-taxonomy/03-composable-skill-systems.md) §11 —
> the shape that broke all five studied composition systems.)

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| Rung rows carry `rung, minTier, maxTier, poolRolls, qPowerMilli, costMulti, cdMulti, structureBudget` | **built** | `data/tuning/action-rungs.v1.json` — **no power budget column**; `qPowerMilli` is a *multiplier*, not a ceiling |
| `ContentValidation.Budget(containers, atomsOf, ceilingFor)` | **built, zero production callers** | only `ContentValidationTests`, passing literals (`_ => 100_000`) |
| A rarity-budget table anywhere in `data/` | ⛔ **does not exist** | — |
| E9 pricing keyed on `(kindId, channel)` | **built** | `CoefficientTable.cs:14` — **no concept of an atom family** |
| All 20 coefficients | **flat at `CoeffMilli = 1000`** | `CoefficientTable.cs:125-147`; only `ReferenceScale` varies |
| `RungMonotonicity.VerifyPowerClimbs` | **built — but reads no atoms** | prices one synthetic `PowerVector.FromCategory(Offense, 1000)` per rung. Its own docstring says the real check *"belongs to A13's generation tests"* |
| `StructureBudgetGuard.SpentAxes` detects 5 of 7 axes | **built, incomplete** | `reaction` and `restriction` absent — the guard's own docstring calls it *"an honest, documented gap, never guessed at"* |
| The rung window's row in `ssot-power-scale.md` §11 | ⛔ **does not exist** | §5 constraint 2 promised it; §10 says *"a power-shaped number that is not in this table does not have permission to exist"* |

---

## 3. The contract

### 3.1 A per-rung power budget, as data

A `powerBudgetMilli` column on `action-rungs.v{n+1}.json`, **published through
`tools/tuning/publish.py`, never hand-edited** (`tunables-ssot.md` T4).

**Its derivation must be stated in the file's `_meta`, not left implicit.** The obvious derivation, and
the one to start from because it introduces no new curve:

> `powerBudgetMilli(r) = poolRolls(r) × referencePower × qPowerMilli(r) / 1000`

— the rung's own roll count times its own power multiplier. **This is not a new `f(level)`.** It reads
`qPowerMilli` from the shipped table, so it stays inside the one power ladder; writing an independent
budget curve would be exactly the private-curve defect `ssot-power-scale.md` exists to prevent.

**⛔ DECIDED 2026-09-03 — `referencePower` ships at `1000`, derived from something shipped.** The term
was named and never given a value, which left the derivation unevaluable and the module unbuildable.

**`referencePower = 1000` is `PowerVector.One`** — the repo's own unit of power in per-mille
(`PowerVector.cs:135`, `public const long One = 1000;`), the same unit `CostFunction` and
`ActorPowerCache` normalise magnitudes into (`CostFunction.cs:65`, `ActorPowerCache.cs:94`). So the
value is not chosen, it is **read**: one reference action is worth one unit of power.

What that yields against the shipped table, with `long` arithmetic, widening before the multiply and
**dividing by 1000 last, exactly once** (the divisor is `PowerVector.One`, so the formula stays in
per-mille rather than gaining a factor of 1000):

| Rung | `poolRolls` | `qPowerMilli` | `powerBudgetMilli` |
|---|---|---|---|
| 1 | 1 | 1000 | **1000** — exactly one unit of power |
| 5 | 2 | 3062 | 6124 |
| 10 | 3 | 12407 | 37221 |

**Rung 1 landing on exactly `1000` is the property that makes this the neutral default**, in the same
sense as `spec-innate-picker.md` §3.3's *"multipliers … defaulting to 1000, at which the score
reproduces the lexicographic tuple exactly"* (`spec-innate-picker.md:124-125`): at the default, the
budget *is* the shipped power curve, unscaled. The curve is `qPowerMilli`'s and stays `qPowerMilli`'s
— `referencePower` is a single scalar that moves the whole ladder up or down together, so tuning it
can never introduce a second curve shape.

**What the smoke batch tunes it against.** The batch's accepted containers are priced through
`ContentValidation.Budget` (§3.2) and the report gives the distribution of container cost against
`powerBudgetMilli(r)` per rung. If most accepted content sits far under budget the scalar comes down;
if the check fires on ordinary content it goes up. That is a **config change** — the value lives in
`action-rungs.v{n+1}.json`'s `_meta`-documented column, published through `tools/tuning/publish.py`,
never hand-edited. Until that evidence exists the value is stated as **untuned**, in the file's
`_meta`, in those words.

**And the register entry is part of this module, not a follow-up.** §5 constraint 2 promised the window
*"belongs in the §11 register with that justification written down."* A budget that is not in the caps
register does not have permission to exist.

### 3.2 A budget check with a production caller

`ContentValidation.Budget` already has the right shape and no caller. This module:

- adds a **rung-keyed** overload beside the rarity-keyed one — a generated action has a rung, not a
  rarity, and `effect_container.rarity` is free TEXT with no FK;
- calls it from the generation path, so a container exceeding its rung's budget is a **finding with the
  container id**, never a silent pass;
- keeps the existing rarity path untouched.

### 3.3 The structural half — `reaction` and `restriction`

The two axes that are the signature tier's **only** structural advantage over family (both first
appearing at rung 9) are the two `StructureBudgetGuard` cannot see. **So the family-vs-signature split is
unenforceable today**, and a spec that gates on structure while structure is undetectable is gating on
nothing.

- **`reaction` is UNSPENDABLE, not undetectable — corrected 2026-09-03.** This spec originally called it
  a detection gap. `StructureBudgetGuard`'s own docstring says otherwise: *"`reaction` cannot be spent by
  anything authored today — `ActionKind` has exactly three members (`Basic`/`Innate`/`Skill`), none
  reaction-shaped, verified by reading the enum rather than assumed — so it is **correctly never flagged,
  not merely unchecked**."* **The guard is right and there is nothing to fix in it.** Authoring a
  `reaction` axis authors something the shipped action model cannot express, so a brief naming it must be
  **refused**, not flagged. Five specs (`distribution-planner`, `signature-propose`, `validate-heal`,
  `coverage-report`, and this one) report it as a detection gap; **all five are wrong and must say
  "unspendable"**.
- **`restriction`** — the guard's docstring is explicit that this *"needs the effect-atom program's own
  per-atom payload/target data, which is OUTSIDE the three tables this module reads."* **That is a real
  cross-program dependency, not a gap this module can close alone**, and it is named here rather than
  quietly assumed.

**Where an axis cannot be detected, the guard must report `undetectable` for it — never `0`.** A zero is
indistinguishable from "not spent", and that ambiguity is what let the gap survive.

### 3.4 ⛔ The third assertion this module does NOT close

§21.3's second requirement is **a family-aware, non-additive price**. It is blocked on **D2** —
multiplicative pricing — which `definitions.md` §13 records as open after two failed attempts, and which
needs a **simulation sweep**, not a module.

**So C1's family-access widening stays gated after this module ships.** What changes is that two of the
three gates are open and the third has a named owner and a named blocker, instead of all three being a
sentence in five boundaries sections.

**This module must say so plainly in its own acceptance criteria rather than implying C1 is enabled.**

---

## 4. What this module must NOT do

- **Invent a budget curve.** The budget derives from the shipped `qPowerMilli` and `poolRolls`. A second
  curve is the defect the power SSOT exists to end.
- **Enable C1.** Two of three gates is not three.
- **Report `0` for an undetectable axis.** `undetectable` is a distinct state.
- **Clamp a container to its budget.** Over-budget is a **finding**, not a silent trim — the same rule
  that makes `LoadoutSet` reject rather than truncate: *"truncation silently picks a winner and the
  player never learns which."*
- **Put the budget number in code.** `data/tuning/`, published, with its derivation in `_meta`.
- **Use `float`.** Per-mille integers, widen to `long` before multiplying, divide by 1000 last, overflow
  throws.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | Every rung row has a `powerBudgetMilli`, and it **equals the stated derivation** recomputed from `poolRolls` × `qPowerMilli` | No private curve |
| 2 | The budget is **monotonic across rungs** | A higher rung is never a smaller budget |
| 3 | A container priced above its rung's budget produces a **finding naming the container id** | §3.2 |
| 4 | **Planted violation:** an over-budget container that is **not** reported fails the test | A check with no caller is a comment |
| 5 | `SpentAxes` returns **`undetectable`** for **`restriction` only**, and a test asserts it is not `0` | §3.3's ambiguity is closed |
| 5b | A brief naming **`reaction`** is **refused at authoring**, and `SpentAxes` is left **unchanged** for it | ⛔ **CORRECTED 2026-09-03.** Test 5 originally covered `reaction` too, contradicting §3.3 and AC5 — which say the guard is *already correct* about it. A builder implementing the old test 5 would have changed a guard this spec says not to touch |
| 6 | The rung window appears in `ssot-power-scale.md` §11 with its justification, asserted by a doc-drift test | §5 constraint 2, mechanically |
| 7 | **The generator refuses to widen family access** while assertion 2 is open — asserted, so C1 cannot be switched on by accident | §3.4 |
| 8 | Budget arithmetic **throws** rather than wrapping at `long` boundaries | Numeric rule |

**Test 7 is the load-bearing one.** Everything else here makes the gate real; test 7 stops someone
reading "two of three" as "done".

---

## 6. Acceptance criteria

1. `action-rungs.v{n+1}.json` carries `powerBudgetMilli`, published, derivation in `_meta`.
1b. `referencePower` has a **stated value — `1000`** — derived from `PowerVector.One`
   (`PowerVector.cs:135`), with `_meta` recording that it is untuned and what the smoke batch tunes it
   against (§3.1). A test asserts `powerBudgetMilli(1) == 1000`, the property that makes it the
   neutral default: at the default the budget is the shipped `qPowerMilli` curve, unscaled.
   ⛔ **DECIDED 2026-09-03** — the term was named with no value, which left the derivation
   unevaluable and this module unbuildable.
2. The derivation reads `qPowerMilli` and `poolRolls` — no new curve, and `referencePower` is a
   single scalar that moves the whole ladder together, so tuning it cannot introduce a second shape.
3. `ContentValidation.Budget` has a rung-keyed overload **and a production caller**.
4. An over-budget container is a finding with its id; nothing is clamped.
5. `restriction` reports `undetectable`, never `0`. **`reaction` is refused at authoring** — it is
   unspendable, not undetectable, and the guard is already correct about it.
   ✅ **VERIFIED CONSISTENT 2026-09-03 (second pass).** The review's F4 correction was checked end to
   end: §3.3's `reaction` bullet, testing-table row 5 (`restriction` **only**), the new row 5b
   (`reaction` refused at authoring, `SpentAxes` left unchanged) and this criterion now say the same
   thing, and no row asks a builder to touch `StructureBudgetGuard` for `reaction`. Nothing further is
   owed here.
6. The rung window is in the caps register with its justification, held by a test.
7. **C1's family-access widening remains disabled**, asserted by test 7, until D2 closes.
8. `restriction`'s cross-program dependency on effect-atom's per-atom payload/target data is recorded in
   the map, not just here.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **A-S1** `distribution-planner` | Owns the rung windows this module budgets |
| **effect-atom D2 / E9 sweep** | ⛔ **Blocks assertion 2, and therefore C1.** Not this module's to close |
| **effect-atom (per-atom payload/target data)** | `restriction` detection needs it — `StructureBudgetGuard`'s own docstring says so |
| **power** | The caps register entry lands in `ssot-power-scale.md` §11; that file has no row for the action rung ladder today |
| **The five specs that cite the gate** | `distribution-planner`, `general-propose`, `family-propose`, `signature-propose`, `validate-heal` all restate it. **When this module changes the gate's state, all five must be updated** — they carry the constraint inline by design, and inline copies drift |
