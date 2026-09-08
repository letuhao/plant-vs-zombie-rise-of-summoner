# Spec: `aura-magnitude`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Depends on:** `derived-modifier-bucket`, `commander-lawn-bridge`
**Status:** specced 2026-08-30, not built.

---

## 1. Objective

How strong an aura is. One formula, two axes, and one invariant that keeps it from going quadratic.

**Owner decision (Q10, 2026-08-30), verbatim:** *"aura level, but aura distribution (effect atom still
depend on how much primary stats we distribute, like a commander that build power attack cannot buff
same defend for unit without any primary stats on defend branch, so this is 2 axis, share both aura
skill level and primary stats — 2 commanders with same aura level but one having higher primary stats
should buff stronger."*

```text
auraContribution(aura A, commander C) = AptitudeReadFunctions.Magnitude(
                                            kMilli: k(rung(A)),
                                            share:  share(C, aptitudeOf(A)),
                                            γ:      Read.Magnitude.ShareExponentMilli,
                                            pTheta: P(Θ))
                                      =  k(rung) · share^γ · P(Θ)
```

- **`k(rung)`** — the aura's own level axis, via a **declared rung mapping** (§3.4), constrained to
  rungs 7–10 by the `consumption` floor. **Not "the rung is the level"** — `ActionRow.Rung` is an
  authored column nobody advances; the mapping is declared at registration per `spec-rung-table.md:137`.
- **`share^γ`** — the commander's specialization in that aura's aptitude.
- **`P(Θ)`** — the ladder. **Not optional**: without it the aura decays to irrelevance as Θ grows
  (§3.2).

The second axis is what makes the owner's rule true: a commander who built power **cannot** buff
defence well, because `share` for Fortitude is near zero for them — and at exactly zero the product is
exactly zero. Identity comes from *where the points went*, not from a per-aura toggle.

> ⚠️ **An earlier revision of this section wrote `flatAdd( f(rung) × g(share) )` with no `P(Θ)` term
> and called the rung "the aura's own level".** Both are retracted — see §3.2 and §3.4. The op is
> still `Flat` (§3.6); the *value* is not.

---

## 2. The two axes, both already shipped

### 2.1 `f(rung)` — the action rung ladder

`RungRow(Rung, QPowerMilli, CostMulti, CdMulti, …)` (`Rungs/RungRow.cs:8-15`), loaded from
`data/tuning/action-rungs.v1.json`, and it arrives with two properties this module gets for free:

- **Monotonic** — `RungMonotonicity.VerifyPowerClimbs` fails if rung `u+1` is not worth more than `u`.
- **Never strictly dominant** — `VerifyCostSpanExceedsPowerSpan` asserts `qCost(cap)/qCost(2) >
  qPower(cap)/qPower(2)`, because *"a regression to a flat tax… is exactly the shape that makes the
  loadout a sort instead of a decision"* (`RungMonotonicity.cs:56-60`).

And `CostLedger.ScaledAmount` **already** multiplies cost by `CostMulti` (`CostLedger.cs:140-148`), so
the *arithmetic* for "a higher-rung aura costs more per tick" exists.

⚠️ **But it has no driver.** Audit **D4**: there are **zero `new CostLedger(` sites in `src/`**, `PerTick`
is read only as a classifier and never charged, and production battle passes `AlwaysAffordable.Instance`.
So "costlier per tick" is a formula waiting for a caller, not shipped behaviour — and the upkeep driver
is its own module-sized piece of work, not a free consequence of the rung table.

`f(rung)` therefore reads `QPowerMilli` and **introduces no new curve**.

### 2.2 `g(share)` — the commander's aptitude share

`AptitudeAllocation.Share(aptitudeId)` (`AptitudeAllocation.cs:81-85`) — a **bounded [0,1] ratio**,
explicitly *"exempt from the long-magnitude rule (CLAUDE.md: bounded ratios are exempt)"*.

**Which allocation — settled by code, not by choice.** The commander's allocation is loaded
commander-scope-only:
`store.LoadAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId))` — the call
`WebMatchService.cs:364-372` already makes. That object contains only commander-scope entries, so
`Share()` over it is **already commander-relative**: `Total` sums four scopes but three are zero.

> This resolves map open question 4. **Do not use `TotalForScope`,** and do not take a per-scope share
> of a merged allocation — `AptitudeAllocation.cs:13-17` is explicit that *"a per-scope share, later
> combined, is a different (and wrong) number."*

**`Empty` reads 0, never `1/12`** (`:19-22`). An unallocated commander's auras contribute nothing —
correct, and it is why `commander-lawn-bridge` must land first for any of this to be observable.

---

## 3. ⛔ The formula — the shared read function, with the rung supplying `k`

> **Rewritten 2026-08-30.** The previous version specified a **ladder-independent flat value**
> (`f(rung) × g(share)` with no `P(Θ)` term) and justified it with a quadratic-overflow argument that
> was **wrong**. Both errors are corrected below, and the retraction is kept because the reasoning is
> the point.

### 3.1 The formula

The aura is **not** a new arithmetic. It calls the same read function every other aptitude consumer
calls — `AptitudeReadFunctions.Magnitude` (`AptitudeReadFunctions.cs:48-69`):

```
Magnitude(kMilli, share, γ, P(Θ))  =  (kMilli/1000) · share^γ · P(Θ)
```

with **the declared rung mapping supplying `kMilli`** (§3.4). So:

```
Leg A (allocation) = k_alloc · share^γ · P(Θ)
Leg B (aura)       = k_rung  · share^γ · P(Θ)
Total              = (k_alloc + k_rung) · share^γ · P(Θ)
```

**`guard-class-system.ps1` G5 fails the build if a second `class AptitudeReadFunctions` appears
anywhere under `src/`** (`AptitudeReadFunctions.cs:7-8`). Reusing it is not a preference; it is
enforced.

This satisfies the owner's two-axis rule exactly: **the rung sets `k`, the share sets `share^γ`**. Same
rung + higher share is stronger; same share + higher rung is stronger; **zero share contributes exactly
zero** (a pure product, no floor).

### 3.2 Why the previous "flat value" version was wrong

`P(Θ)` is quadratic and Θ grows without bound under the endless-grind SSOT. A Leg B with **no `P(Θ)`
term** shrinks as a fraction of the total forever — **a progression ceiling by arithmetic**. That is the
exact defect this spec elsewhere flags for `patron.aura` (clamped at 150‰, irrelevant past ~15 points)
and failed to notice in itself.

### 3.3 Why the "quadratic double-count" argument was wrong

The forbidden shape is a percentage of the actor's *existing derived total*:

```
Leg B_bad = p(share) · (base + k_alloc · share^γ · P(Θ))
          → a  p·k·share^(1+γ)·P(Θ)  cross-term
```

The cross-term is real, but the earlier characterisation of it was not:

- **"Quadratic in commander points" is false.** `share = Total(id)/GrandTotal()` is a **bounded [0,1]
  ratio** (`AptitudeAllocation.cs:79-85`). Points are unbounded; share is not.
- **The cross-term is *smaller* than the linear term beside it.** For `share ∈ [0,1]`, `share² ≤ share`,
  so with `p` a per-mille fraction it is a fraction of a term that already exists. It is **not** an
  overflow path.
- **The real overflow path is different and was missed:** a percentage **re-asserted per tick** against
  an already-buffed total is **geometric in tick count**. `shield-system-spec.md:135` gives shields an
  idempotence guarantee (*"aura re-assert is genuinely idempotent"*); **no equivalent exists for a derived channel** (audit D2).

⛔ **So the rule stands, for a corrected reason:** the aura's value must be a function of
`(k, share, Θ)` **only** — never of the channel's current value. Not because the alternative is
quadratic, but because it is **non-idempotent under re-assertion**.

### 3.4 The rung mapping — declared, not assumed

`spec-rung-table.md:137`: *"A new mechanism that grants actions declares its mapping; it does not invent
a rung scale."* The aura program declares one at registration.

⚠️ **Constrained by the `consumption` floor.** `consumption` first appears at **rung 7**
(`action-rungs.v1.json`), and `StructureBudgetGuard.cs:67` **rejects at load** any action with a
`perTick` cost row whose rung does not budget that axis. Since every aura carries `perTick` upkeep
(`aura-action-shape` §5.3), **no aura can exist below rung 7.**

Usable span: **rungs 7–10** (`action-rungs.v1.json` sets `"cap": 10` — there is no rung 11),
`QPowerMilli` **5359→12407, a 2.3× range**, not the 12.4× the full ladder suggests. The mapping must fit
inside that band, and the tuning must be designed for 2.3×.

**The truncated band is still self-consistent** — verified, not assumed. §3's balance rule (cost span
must exceed power span, or the top rung strictly dominates) survives inside 7–10: power ×2.315 against
cost ×2.628. Restricting auras to the top four rungs does not break the ladder's own invariant.

### 3.5 The anchor — what the per-mille multiplies

An earlier draft left this undefined, which is the "percentage of *what*?" hole one level down.
**`P(Θ)` is the anchor.** `kMilli` is a per-mille coefficient, `share^γ` a bounded ratio, and `P(Θ)` the
magnitude they scale — exactly as every other magnitude edge in `aptitudes.v2.json` works. The result
is a `GameUnits` magnitude, which is what `combat.*` channels are classified as.

### 3.6 Op versus value — two different things

The aura emits **`DerivedModifierOp.Flat`**, because every `combat.*` channel registers `FlatSum` and
`FlatSum` sums only `Flat` ops (`DerivedComposer.cs:42`). **`Flat` describes how the composer combines
it — not that the value is constant.** The value scales with share and `P(Θ)`. The previous draft
conflated the two.

`AptitudeResolver.cs:59` already derives the op from the target channel's compose kind, so this comes
free by reusing the resolver's own pattern.

⚠️ **The op is not load-bearing on the shipped battle path.** `TraitAtomSource.cs:67-84` reads only
`channel` and `amount` and never reads `op`; `BattleStatComposer` applies it additively regardless. So a
test asserting "emits `Flat`" proves something about the modifier object, **not** about behaviour. Keep
the assertion — it documents intent — but do not claim it guards anything downstream.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~AuraMagnitude
dotnet test tests\FusionRpg.Core.Tests
python scripts\audit-magic-numbers.py --targets M1
python scripts\audit-overflow.py
.\scripts\guard-power.ps1
```

`guard-power.ps1` matters here: `ssot-power-scale.md` §10's inventory is **closed**, and its
*"Inventory closed"* check fires on any power-shaped scale the table does not list. **This module must
not add a row** — it composes two existing scales (the rung ladder and the aptitude read functions)
rather than defining a third.

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Actions/Aura/AuraMagnitude.cs` | **new** — the two-axis formula |
| `src/FusionRpg.Core/Actions/Aura/AuraBudget.cs` | **new** — split one budget across signature channels |
| `data/tuning/aura.v1.json` | edit — budgets, splits, share exponent |
| `tests/FusionRpg.Core.Tests/Actions/Aura/AuraMagnitudeTests.cs` | **new** |
| `tests/FusionRpg.Core.Tests/Actions/Aura/AuraDoubleCountTests.cs` | **new** — the §3 invariant |

---

## 6. The budget model

Every aura receives the **same `budgetMilli`**, split differently across its own signature channels.
Taken from FFXIV's Feint/Addle: a mirrored pair of internally-asymmetric debuffs (**10% physical / 5%
magic** and its mirror) at identical duration and cooldown — **one fixed budget split asymmetrically,
then mirrored**.

This solves the identity-vs-parity problem measured from `aptitudes.v2.json`:

- `kMilli` weights span **2200×** (Vigor `shield.capacity` 55000 vs Fortitude `reduction` 25).
- Distinctive-channel counts range **3 (Retribution) to 12 (Bulwark)**.

Under a per-channel magnitude rule Retribution's aura feels empty and Bulwark's is overloaded. Under a
**fixed budget**, Retribution's three channels each take a larger share of the same total. Identity
without unequal totals, and no dead pick.

**Tunables** — all in `data/tuning/aura.v1.json`, integer per-mille, no `const`:

| Key | Meaning |
|---|---|
| `budgetMilli` | the total every aura splits (one number, shared) — this is the `kMilli` an aura has to spend |
| `auras.<id>.split[]` | how that budget divides across the aura's signature channels |
| `rungMapping` | the declared rung→`k` mapping (§3.4), constrained to rungs **7–10** (`action-rungs.v1.json` sets `"cap": 10`; there is no rung 11) |
| `auras.<id>.upkeep{resource, perTickMilli}` | the per-tick cost. ⚠️ **Was missing from every earlier table** — a magic number on the balance surface while both specs claimed `audit-magic-numbers.py` clean |
| `stackRule` | what two active auras on one channel do. ⚠️ **Needed the moment `maxActiveAuras > 1`**, which ships as a tunable on day one; nothing previously specified it |
| `maxActiveAuras` | owned by `aura-action-shape`, same file |

⚠️ **`shareExponentMilli` is NOT an aura tunable.** An earlier draft listed it here *and* assigned the
share→effect curve to `aptitudes.v2.json`'s `read.*` block in the same section — a direct
self-contradiction. The code already has two (`Read.Contest.ShareExponentMilli`,
`Read.Magnitude.ShareExponentMilli`); a third aura-local exponent would either be **a new power-shaped
curve — which §4's own `guard-power.ps1` rule forbids** — or a duplicate of an existing one. The aura
reads `Read.Magnitude.ShareExponentMilli` and authors nothing.

**Four numbers that must NOT be authored here** — other systems own them: the share→effect curve
(`aptitudes.v2.json`'s `read.*` block already implements PS-3 as two tunable functions), `P(Θ)`/`Θ`
(`power-scale.v2.json`), shield drain priority (`decisions.md:41`), and a debuff ratio (deleted — §4.1
of the ideal grants to `Ally` only).

---

## 7. Curve shape

From Blizzard's own D2 tables, with no counterexample across the whole paladin tree:

- **Unbounded magnitude → linear, uncapped.** Might `+10%/level` to +230%; Meditation `+25%/level` to
  +775%.
- **Bounded quantity → asymptotic, self-saturating.** Fanaticism's attack-speed track flattens at 35.

**This satisfies the no-hard-ceilings rule by arithmetic rather than by clamping** — linear-unbounded
channels never need a cap, and bounded ones bound themselves.

**And the corollary that must hold for `g(share)`:** a scaling input whose marginal value is not
roughly constant forces a cap. GGG's `reduced Reservation` and Riot's percentage CDR shipped the same
bug — *"very little benefit at low values, but very powerful when heavily invested in"* — and both were
fixed by changing the arithmetic (`÷(1+efficiency)`, Ability Haste), after which **Riot deleted the
cap**, because it had only ever existed to contain the exponential.

So **`shareExponentMilli` defaults to 1000‰ (linear)**. Any value making `g` convex is a decision that
must be argued, not a default.

---

## 8. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | Same rung, higher share | **strictly stronger** — the owner's stated rule |
| 2 | Same share, higher rung | strictly stronger |
| 3 | Zero share in the aura's aptitude | **contributes exactly 0** (a pure product; no floor) |
| **4** | ⛔ **Base-independence** | Fix rung, share, Θ. Resolve. Then add an unrelated `Flat` contribution `X` to the same channel from a **different `SourceId`** and resolve again. The aura's emitted `DerivedModifier.Value` must be **bit-identical** across both, for several `X` **including one larger than Leg A**. A percentage-of-total fails on the first non-zero `X` |
| **4b** | ⛔ **Second difference in share is zero** | Construct allocations giving exact shares `s ∈ {0.1, 0.2, 0.3}`; hold rung and Θ. Assert `T(0.3) − 2·T(0.2) + T(0.1) == 0` within one per-mille rounding step. A `c·share²` term makes this `2c·(Δs)² ≠ 0`. **Repeat at two Θ** so a Θ-dependent coefficient cannot hide |
| **4c** | ⛔ **Rides the ladder** | `LegB(Θ₁)/P(Θ₁) == LegB(Θ₂)/P(Θ₂)`. Pins §3.2 — catches a regression to a ladder-independent value |
| 5 | Emitted op is `Flat` | documents intent. ⚠️ **Does not guard behaviour** — the shipped battle consumer is op-blind (§3.6) |
| 6 | Budget conservation | splits sum to `budgetMilli`; a split that does not is **rejected at load** |
| 7 | Two auras, same budget | equal totals despite different channel counts (Retribution vs Bulwark) |
| 8 | Rung monotonicity **within 7–10** | magnitude climbs across the *usable* band, not the full ladder |
| 9 | Below-rung-7 aura | **rejected at load** with `StructureExceedsBudget` — the `consumption` floor is real |
| 10 | Commander-relative share | `Share()` on a commander-scoped allocation matches hand-computed values; `TotalForScope` is **not** called |
| 11 | Empty allocation | every aura contributes 0, not `1/12` |
| 12 | `guard-power.ps1` | run it. ⚠️ A green result may mean the heuristic missed a `rung` parameter — see Boundaries |
| 13 | Overflow | `audit-overflow.py` clean; `Magnitude`'s own `decimal` widening and `OverflowException` are inherited, not reimplemented |

> **Tests 4 / 4b / 4c replace a test that could not fail.** The previous test 4 — *"doubling commander
> points at most doubles the total"* — was **vacuous**: doubling *all* points leaves `share` exactly
> unchanged (`AptitudeAllocation.cs:84`), so it returns `1.000` for the required **and** the forbidden
> implementation. Doubling one aptitude's points moves share sublinearly and still lands under the
> bound. It had zero discriminating power while being described as the program's key guard.
>
> **4 is the one that matters.** It tests the functional dependency — *the aura's magnitude must not be
> a function of the channel's current value* — which is the property the design actually needs, and it
> survives coefficient rebalancing.

⚠️ **`long` is not achievable end to end, and no test should claim it.** `DerivedModifier.Value` is
`double` (`DerivedModifier.cs:6`) and `ActorDerivedSnapshot` is `Dictionary<string,double>`. The
magnitude is `long` up to the `DerivedModifier` boundary and `double` thereafter. **Pre-existing, out of
scope, named here so nobody asserts otherwise.**

---

## 9. Boundaries

**Always**
- Emit `Flat`.
- Read `Share()` on a commander-scoped allocation.
- Compose the existing rung ladder and read functions; add no curve.
- `long` magnitudes; per-mille integers; divide last.

**Ask first**
- Any percentage-of-total formulation.
- **Whether `ssot-power-scale.md` §10 needs a row for the rung→`k` mapping.** ⚠️ Do **not** assert it
  does not. `PatronPolicy.AuraMilli(rarity, star, level)` — a private aura magnitude ladder — **was**
  added as **row 16**, so the precedent runs the other way. And `guard-power.ps1`'s heuristic keys on
  parameters named `level|lvl|index`, so a parameter named `rung` **slips past it**: a green guard here
  proves the regex missed it, not that the design is inside the closed inventory. Run the guard, then
  take the row question to the owner.

**Never**
- Multiply a base that contains the commander-scope contribution.
- Emit `Increased` on a `combat.*` channel.
- Restate the share→effect curve, `P(Θ)`, or shield priority.
- Use `float` for a magnitude.

---

## 10. Success criteria

- [ ] Higher share ⇒ stronger; higher rung ⇒ stronger; zero share ⇒ **exactly 0**.
- [ ] **Base-independence holds** (test 4) — the aura's value is bit-identical regardless of what else
      contributes to the channel.
- [ ] **Second difference in share is zero** (test 4b), at two Θ.
- [ ] **`LegB / P(Θ)` is constant** (test 4c) — the aura rides the ladder.
- [ ] Budgets are conserved; a bad split and a below-rung-7 aura are both rejected at load.
- [ ] `audit-magic-numbers.py --targets M1` and `audit-overflow.py` clean — **including the upkeep
      number**, which no earlier draft gave a home.
- [ ] `guard-power.ps1` **run**, and its result **interpreted** (see Boundaries) rather than assumed.
- [ ] ⛔ **Contributes to the program-level acceptance rule** (map): this module's share is a
      hand-computed expected value for one aura at a named `(rung, share, Θ)`, asserted end to end —
      not merely formula properties in isolation.

## 11. Open questions

1. **Where do aura rung points come from?** The ladder exists; what advances an aura along it does not.
   Candidates: the existing action-unlock economy (`UnlockTuning`, `A11`/T19), a commander-specific
   currency, or authored-per-aura with no player choice. **Owner call, and it is the last genuinely
   open design question in the program.**
2. **Should `budgetMilli` be one shared number or per-aura?** Shared gives guaranteed parity (the
   Feint/Addle property); per-aura allows deliberate outliers. Leaning shared, with per-aura as a later
   escape hatch.
3. **`patron.aura` clamps at 150‰ and an aura would not** — patron becomes irrelevant once commander
   investment passes ~15 points. Coherence question for the owner, not a defect.
