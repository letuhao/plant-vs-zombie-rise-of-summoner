# Spec: `point-economy` — four budgets, four sources, one respec price

**Module id:** `point-economy` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `aptitude-resolve` · **⛔ externally on `aspect-scope`, owned by the demon program**
(owner decision 2026-08-26) · **Blocks:** `residual-fit`

> **Scope 3 waits on another program's queue.** That is the accepted cost of correct ownership —
> every file `aspect-scope` edits is the demon program's. The other three scopes are unblocked, so
> this module can ship three-of-four and light up the fourth when the tier lands.

---

## 1. Objective

Give the four allocation scopes their **point budgets**, their **persistence**, and the **one price
that holds a build together** now that free build removed the class that used to.

**Users:** the player; `zomboss-patterns` (an authored allocation is the same shape); `residual-fit`,
whose first output is the tier weights.

**Success is measurable:** an actor's effective allocation is the sum of four persisted allocations,
each drawn from its own budget, and respec is available, unlimited, and priced.

---

## 2. The four scopes map onto the shipped identity grammar

[unique-actor-runtime.md](../unique-actor-runtime.md) already keeps **three orthogonal ids** —
`typeId` / `ptr` / `instanceId` — and `decisions.md`'s *UniqueActor* row makes the separation load-
bearing. The allocation tiers ride them:

| Tier | Keyed by | Points come from | Reads as |
|---|---|---|---|
| **commander** | player | `Θ_player` — daveLevel, realms advanced, run term | **who you are.** Shared by every demon you field |
| **demon type** | `typeId` | type almanac XP | **what a species is.** Shared by every specimen |
| **aspect** | `(typeId, element)` | `element_mastery` (§10.1) — tier built by the **demon program**, [demons/spec-aspect-scope.md](../demons/spec-aspect-scope.md) | **which strain** |
| **unique demon** | `instanceId` | specimen level | **this one, that you invested in** |

**An actor's allocation is the SUM of four** — and `share` is taken on the sum, never per tier
([spec-primary-stats.md](spec-primary-stats.md) §6 rule 4). It is additive in exactly the way
`omni + category` already is for elements, which is the shipped precedent rather than a new idea.

### 2.1 The weights — decided, and the direction is not arbitrary

> **DECIDED 2026-08-26: the commander tier is the SMALLEST and the unique tier the LARGEST.**

**The commander tier applies to every demon you field**, so a dominant commander allocation is the
worst possible version of the dominance finding — one wrong build, replicated across the whole roster.
The unique tier applies to one specimen, so a strong unique allocation is **specialisation**, which is
what makes a team diverse.

> **Per-demon allocation is also what most blunts the dominance problem.** When you field a mix, *"one
> corner beats all eleven"* stops being the whole game, because the question becomes which **team** to
> bring rather than which build to play. That is the summoner fantasy doing balance work — and it is
> why [spec-balance-guard.md](spec-balance-guard.md)'s soft half is measured per-actor while the game
> is played per-team.

**The four numbers themselves are `residual-fit`'s first output**, not this module's guess. This module
ships the table and the loader; the values are a tuning row like every other.

### 2.2 What used to be one number is now a table

Ideal §7a.2: **3 aptitude points per `Θ`** — 60 points at the `Θ`=20 calibration point, 5 each spread
flat or ~20 each in three if specialised. §7c.3 corrects it: *"per tier now, not per actor. Four
grants, four sources. The single number becomes a table."*

**Two currencies, never interchangeable.** Aptitude points buy **breadth**; skill points buy **element
depth** (`grant.skillPointsPerTheta`). That is the same separation keeping `omni` and element channels
from cannibalising each other (ideal §4.1 rule 2) — **if one pool bought both, the additive
`omni + element` rule would immediately favour whichever was cheaper.**

**No aptitude cap.** PS-8: the pool is the constraint, never a ceiling. A budget an actor earns more of
is not a cap; a maximum it may never exceed would be.

---

## 3. Respec — the only friction left, and it must not be a ban

Free build withdrew the class price (ideal §7a.3), whose job was to make a build a **commitment**.
Nothing replaced it except this.

> **Respec is available, unlimited, and costs a resource that fighting also costs.**

Three things it is deliberately not, each for a stated reason:

| Not | Why |
|---|---|
| A **cooldown** | Punishes being away from the game, which is the wrong thing to punish |
| A **cap** on respecs | PS-8 — a hard progression ceiling |
| **Free** | Then a build is a menu selection, not a commitment, and every fight is fought with the optimal counter |

**"Priced, never banned"** is the shape ideal §7a.3 was reaching for before it was withdrawn, and it
generalises: the same shape prices output rather than investment in `aptitude-tuning`, and prices the
guard rather than forbidding it in `guard-economy`.

**The resource is a tuning row.** Which one, and how much, is a balance decision — but *"a resource
fighting also costs"* is the structural requirement, because a price paid in a currency you get for
free is not a price.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter PointEconomy
dotnet test tests\FusionRpg.Data.Tests --filter Allocation
.\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --domain aptitudes
```

---

## 5. Project structure

```text
src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs          per-scope budget from its own source
src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs         the price. No literals - reads tuning
src/FusionRpg.Data/Sqlite/RpgStore.Aptitudes.cs             persistence, per scope
tests/FusionRpg.Core.Tests/Stats/Aptitudes/PointBudgetTests.cs
tests/FusionRpg.Data.Tests/AllocationStoreTests.cs
data/tuning/aptitudes.v{n}.json                            pointEconomy block gains the four-scope table
```

**⛔ CORRECTED 2026-08-27** — the `AllocationStore`/DAL paths above were written before a survey of
`FusionRpg.Data`'s real conventions: every existing feature (souls, unique actors, demons, contracts,
channel policy) is a `partial class RpgStore` slice sharing ONE connection/lock/`EnsureHotSchema`/
`Reset()` pipeline, not a standalone class with its own path. A standalone `Aptitudes/AllocationStore.cs`
would have forked that pipeline and silently dropped out of `Reset()`. Built as `RpgStore.Aptitudes.cs`
instead, following `RpgStore.ChannelPolicy.cs`'s own template exactly; the public surface
(`RpgStore.SaveAllocation`/`LoadAllocation`/`ScopeToText`/`ScopeFromText`) is what §7's tests exercise.
Test path corrected the same way — every other `FusionRpg.Data.Tests` file is flat, no per-feature
subfolder exists there either.

**SQL lives only in `FusionRpg.Data`** — `guard-dal.ps1` enforces it, and the guard **scans only
`src/`**, so anything this module puts under `tools/` is in a blind spot and must be kept clean by
hand.

**`RespecPolicy` is a balance-surface file by name** (T3), so it carries **no bare literal** — every
number is a named tunable or a named structural constant.

---

## 6. Code style

```csharp
// long: a budget is aptitudePointsPerTheta x Theta, and Theta is uncapped (PS-8).
// int overflows near Theta = 715,000,000 at 3 points/Theta - reachable, not hypothetical.
public long PointsFor(AllocationScope scope, long theta);
```

**Persistence stores the allocation, never the resolved channels.** `stat-system.md`'s invariant:
*"Save inputs (Y0 + bag / feature state), not computed totals."* A stored channel value would be a
second SSOT that goes stale the moment a coefficient moves — and coefficients are meant to move.

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Four_scopes_sum_to_the_effective_allocation` | And `share` is taken on the sum |
| 2 | `Each_scope_draws_from_its_own_budget` | Overspending one scope cannot be covered by another |
| 3 | `Commander_budget_is_smallest_and_unique_largest` | §2.1's ordering, over the shipped tuning |
| 4 | `No_cap_on_an_aptitude` | Every point in one aptitude is legal. PS-8 |
| 5 | `Budget_is_exact_at_high_theta` | `long`; red today with `int` |
| 6 | `Respec_is_always_available_and_always_priced` | Never refused, never free, no cooldown |
| 7 | `Allocation_round_trips_through_the_store` | Per scope, and unknown scope rejects |
| 8 | `Store_persists_inputs_not_totals` | No resolved channel value is written. §6 |
| 9 | `Skill_points_and_aptitude_points_never_convert` | §2.2 — two currencies, one direction each |

---

## 8. Boundaries

**Always** — sum before `share`; store inputs; price respec in a tuning row; keep SQL in
`FusionRpg.Data`.

**Ask first**

- The four tier weights, before `residual-fit` has measured them. Shipping a guess is fine; **calling
  it balance is not**.
- Which resource respec costs.

**Never**

- Cap an aptitude, or cap respecs (PS-8).
- Make respec free, or gate it behind a cooldown (§3).
- Convert skill points to aptitude points or back (§2.2).
- Persist a resolved channel value (§6).
- Take `share` per scope (§2).

---

## 9. Success criteria

1. Four budgets, four sources, summing to one effective allocation.
2. Weights ordered commander < type ≤ aspect < unique, from tuning.
3. No cap anywhere; budgets exact at high `Θ`.
4. Respec always available, always priced, never on a cooldown.
5. Allocation persists per scope, inputs only, `guard-dal.ps1` green.
6. `RespecPolicy` holds no bare literal.

---

## 10. Open

**10.1 ~~The aspect tier's point source~~ — DECIDED 2026-08-26: `element_mastery`.** The other three
tiers already had one (`Θ_player`, almanac XP, specimen level); this was the set's last genuinely
undecided input. `element_mastery` fits because it is **per-element by definition** and the aspect tier
**is** the element typing — [spec-primary-stats.md](spec-primary-stats.md) §3.3 had already assigned it
there, so the point source and the tier's identity are one thing rather than two.

**It arrives with two conditions already attached**, and they carry to whoever builds it: it owes a
[power/ssot-power-scale.md](../power/ssot-power-scale.md) §10 row or a proof it is not power-shaped
(`base + mastery × k` is exactly the shape a private `f(level)` wears), and **PS-3 applies** — if it
feeds a contest it reads linearly, if a magnitude it reads `P(Θ)`, never both through one number.

---

## 11. Design-gate checklist

```
[x] Subsystems identified: stats, data/SQL, power scale, caps, tunables, match/actor lifecycle.
[x] Read this session: DESIGN-GATE.md, decisions.md (UniqueActor, RpgProgression, DAL single gate,
    Caps, Magic numbers, Power scale rows), stat-system.md (the save-inputs invariant),
    tunables-ssot.md (T1/T3/T5/T6), ssot-power-scale.md §11, class-system-ideal.md
    (§7a.2, §7a.3, §7b.5, §7c.1-7c.3).
[x] Every factual claim cites a document section.
[x] Verified against DOCUMENTED CODE constraints: guard-dal.ps1's src/-only scan is quoted from the
    DESIGN-GATE Data row, which states it as a known blind spot.
[x] Read the surrounding section of every rule quoted - §7a.3 in full, including WHY it was
    withdrawn and what job was left behind; PS-8's "budget is not a cap" distinction.
[~] Constraints tested, not assumed. PARTIAL - nothing here is built. The one empirical claim
    (int overflow near Theta 715M at 3 points/Theta) is arithmetic over the shipped grant value,
    computed, not estimated.
[x] Nothing contradicts a §2 invariant. Invariant 6 (SQL only in FusionRpg.Data) shapes §5;
    invariant 11 (no hard ceilings) is why §3 refuses a respec cap and §2.2 refuses an aptitude cap.
[x] Corrections propagated - §2.2 carries §7c.3's correction that the single grant number became a
    four-row table; the map's module 8 row says the same.
```

---

## 12. Related

- [class-system-ideal.md](../class-system-ideal.md) §7a.2 (points per `Θ`), §7a.3 (the withdrawn price), §7b.5 (respec), §7c (the four scopes)
- [unique-actor-runtime.md](../unique-actor-runtime.md) — the three orthogonal ids the tiers ride
- [spec-aspect-scope.md](../demons/spec-aspect-scope.md) — scope 3
- [tunables-ssot.md](../tunables-ssot.md) · [power/ssot-power-scale.md](../power/ssot-power-scale.md) §11
