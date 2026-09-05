# `gate-counters` — the two gate quantities that did not exist

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

---

## 1. Objective

A tier gate reads a **gate quantity**. Four are named across the program and two of them work:

| Category (R7) | Trees | Gate quantity | State, grepped this session |
|---|---:|---|---|
| `primary` | 12 | `aptitude.<Id>@Commander` | ✅ shipped — `PointBudget.PointsFor(AllocationScope.Commander, …)` (`PointBudget.cs:51`) |
| `family` | `F` | `species_level@DemonType` | ✅ shipped — `PointBudget.DemonTypeSourceFromLevel` (`PointBudget.cs:40`), consumed at `SpeciesAllocation.cs:34-35` |
| `elemental` | 6 | `element_mastery.<id>` | ⛔ **comments only.** All four `src/` hits are XML doc comments (`PointBudget.cs:13,15,22`, `AptitudeTuning.cs:20`), and `PointBudget.cs:15` says outright it *"is owned by the demon program's `aspect-scope` module and does not exist yet"* |
| `status` | 21 | `status_applied.<id>` | ⛔ **zero `src/` hits.** D35 correctly removed the `AllocationScope` dependency, and removed the only place the counter was going to live with nothing replacing it |

That strands **1,080 of the 1,560 generic nodes — 69% — permanently at tier 0** (ideal §13.4).
D37 (owner, 2026-09-05) decided the two missing quantities are built **inside this program** rather
than waiting on unscheduled work. This module is that decision.

**This module ships three things and nothing else:**

1. **The counters** — what is counted, when a credit is earned, and what does not earn one.
2. **Their persistence** — sparse rows of raw counts, inputs only, in `FusionRpg.Data`.
3. **The binding to the gate** — the conversion from a raw count to the one unit `tree-resolve`
   reads, **without entering `AllocationScope`**.

It ships **no tree content, no node, no channel and no combat behaviour.** A tree whose gate quantity
this module supplies still has nothing in it until `tree-language` and `tree-binder` run.

### 1.1 What this module is not allowed to become

The counters are **observations of play**, not a new combat system. Every credit is taken from an
event the shipped code already raises, at a point it already reaches. If a counter needs a new
combat rule to be countable, the counter is wrong, not the combat.

---

## 2. What each counter counts, exactly

Both counters answer the same four questions. Both answer them the same way, on purpose — one
threshold should not mean two different kinds of effort.

| Question | `status_applied.<id>` | `element_mastery.<id>` |
|---|---|---|
| Whose action? | **Outbound** — applied *by* an actor this player owns | **Outbound** — damage dealt *by* an actor this player owns |
| Landed or attempted? | **Landed only** | **Landed only, and non-zero** |
| Per match or lifetime? | **Lifetime, cumulative, never reset** | **Lifetime, cumulative, never reset** |
| Whose progress? | **The player's**, not the individual demon's | **The player's**, not the individual demon's |

Lifetime rather than per-match is not a preference. A per-match counter cannot gate a persistent tree
at all — `tierReached` would oscillate inside a session — and endless grind is the SSOT other systems
reconcile to.

Per-player rather than per-specimen because the 39 generic trees are the commander's build, the same
standing the 12 primary trees already have at `AllocationScope.Commander`. A per-specimen counter
would restart 27 trees at tier 0 for every new demon. The `owner_kind` column in §4.1 is what keeps
that reversible.

### 2.1 `status_applied.<id>` — a fresh landed application, by an actor you own

**One credit when an actor this player owns causes status `<id>` to take hold on a host that was not
already carrying that status from that actor.**

The four sub-decisions, each with what it rejects:

**(a) Outbound, never inbound.** The 21 status trees are *build* trees — mastery of inflicting a
status. *Rejected: counting statuses applied **to** you.* It rewards being hit, which is farmable by
standing still and is the opposite of what a tree called "mastery" should measure. *Rejected: summing
both.* When a counter moves you could not say which half moved it, and a build that could not land
anything would still climb.

**(b) Landed, never attempted.** `StatusRuntime.Apply` runs the resist contest first and returns
early on a resist (`StatusRuntime.cs:216-220` — `RecordResisted`, then `Applied: false`); the credit
hangs off the success path only, beside `OnApplied?.Invoke` at `:265`. *Rejected: counting attempts.*
Status application is contested, so attempts are the one number a build with no status power produces
as fast as a build with all of it — counting them removes the only signal the tree is about. Attempts
are also free against an immune target.

**(c) A fresh application, never a refresh.** `UpsertInstance` (`StatusRuntime.cs:268-297`) replaces
an existing instance under `StatusStacking.Refresh` and `Replace`, and `OnApplied` fires either way.
A refresh earns nothing. *Rejected: counting every successful apply.* With `StatusIcdMs` defaulting to
0 (`StatusApplyInput`, `StatusRuntime.cs:73`), a reapply loop on one target is the cheapest farm in
the game, and the counter would measure tick rate rather than play. **This is the one place the
counter needs information `StatusRuntime` has and does not publish** — §7 P2 says how to publish it
without touching the three existing `OnApplied` subscribers.

**(d) A distinct host, never yourself.** An application to a friendly actor counts; an application to
the applying actor does not. *Rejected: counting self-application.* A self-buff loop is a farm with
no opponent. *Rejected: hostile targets only.* Several of the 21 ids are support-shaped —
`rally`, `command`, `bond`, `leech` (`StatusCategoryRegistry.cs:11-13`) — and refusing friendly
targets would leave those trees at tier 0 for a different reason than the one this module exists to
fix.

**Charmed and hypnotised actors do not launder credit.** `hypno` and `charm_pulse`
(`StatusCategoryRegistry.cs:20,23`) put an enemy on the player's side. Ownership is decided at spawn,
not by current allegiance — otherwise a charm build farms every other tree in the game through
borrowed bodies.

**Roster: 21 ids**, counted from `StatusCategoryRegistry.Map` (`StatusCategoryRegistry.cs:7-28`) —
8 `Dot`, 8 `Cc`, 5 `Contagion`. `StatusCategoryRegistry.Register` (`:41`) can add more at runtime for
the action program's exhaustion debuffs, so the counter validates through `TryGetCategory` (`:49`):
a registered id with no tree counts harmlessly, and an unknown id throws.

### 2.2 `element_mastery.<id>` — a landed elemental hit, counted once per element carried

**One credit per element component carried by a direct damage event this player's actor landed for
non-zero damage.**

**(a) Events, never damage amount.** *Rejected: cumulative elemental damage dealt.* Damage is a
magnitude and reads `P(Θ)`, which is quadratic — a damage-summed counter would grow like `Θ²` on top
of the event rate it is already exposed to, so its index would run away from the aptitude line it has
to match (§3). It is also a pure gear check: two players with identical play get different gates
because one had better drops. A count of *uses* is the same kind of quantity as a count of
applications, and the two categories should not pace differently for a reason as arbitrary as one
being measured in HP.

**(b) Non-zero, never every dispatch.** `DamageApplyPipeline` deliberately lets a zero un-absorbed
delta reach the sink for miss-telemetry parity (`DamageApplyPipeline.cs:44-47`). The credit therefore
requires `Outcome == DamageApplyOutcome.Applied` **and** `AppliedAmount != 0` — a miss is not a use.
A fully absorbed hit (`FullyAbsorbed`, `:36-38`) earns nothing: the element reached nothing.

**(c) Each component once, never weighted.** A packet carrying fire 0.6 / ice 0.4 credits fire +1
**and** ice +1. *Rejected: weighting by share.* `ElementPayloadComponent`'s weight is a `double`, and
CLAUDE.md forbids a float on a magnitude path; a per-mille accumulator would be a second unit for no
gain. *Rejected: crediting only the largest component.* That makes a hybrid-element build strictly
slower at the gate than a mono build, which fights D28's cross-unlock rule instead of supporting it.

**(d) Direct hits, never DoT pulses.** A typed DoT pulses through the same apply tail
(`StatusPulsePayload.For`, `StatusRuntime.cs:104-112`, used at `BattleEngine.cs:125`) at
`PeriodMs = 1000` by default. Crediting pulses would let one applied `wither` earn an elemental credit
every second for its whole duration, and the status counter has already paid for that application
once. **This needs a discriminator the pipeline does not carry today** — §7 P1.

**Roster: 6 ids** — `ElementRoster.Concrete` (`ActorElementTypes.cs:21-29`): fire, ice, air, earth,
light, dark. `omni` (`:19`) is not an element and has no tree.

### 2.3 What both counters refuse, in one line

**No ICD, no per-target window, no dedupe cache.** Every one of those puts per-hit state on the lawn
hot path, which the 2026-08 perf audit already identified as the thing that is slow. Every anti-farm
rule above is decidable from data the event already carries.

---

## 3. Growth rate — the sharpest question, and it has an exact answer

D26's ladder was calibrated against **aptitude points**, which arrive at `3·Θ` at Commander scope
(`aptitudes.v5.json`: `grant.aptitudePointsPerTheta = 3`, and
`pointEconomy.aptitudePointsPerThetaMilliByScope.commander = 3`). If a counter grows at a different
rate, `req(6) = 105` means one thing on a primary tree and something else on a status tree, and the
ladder stops being one ladder.

### 3.1 A raw count is the wrong shape, and this repo has already paid for that once

`PointBudget.cs:20-26` records it. The DemonType source was documented as *"type almanac XP"* — an
**accumulation** — while the other three scopes read an **index**. `PointsFor` multiplies
`sourceValue × rate` with no unit conversion, so the accumulation *"inverted the locked
commander-smallest-to-unique-largest ordering by 176× at ordinary play levels."* The fix was
`DemonTypeSourceFromLevel` (`:40`) — convert to an index first.

`status_applied` and `element_mastery` are accumulations of exactly that kind. **They must reach the
gate as an index, never as a raw count.**

### 3.2 The index is a square root, and that is derived, not chosen

Two shipped facts settle the shape:

1. **Cumulative effort is quadratic in Θ.** `RpgXpCurve.TotalToReach` (`RpgProgression.cs:99-108`) is
   the triangular sum of the arithmetic ladder `first + (L−1)·step`; at the player row
   (`first = 100`, `step = 45`, `progression.v1.json:13`) that is `≈ 22.5·L²`.
   `ssot-power-scale.md` §10 row 27 uses the same figure, and a level reaches the ladder as `Θ`.
2. **XP is itself an event count.** `progression.v1.json:16-22` pays per event — `kill = 12`,
   `plantPlace = 8`, `zombieSpawn = 9`. So a cumulative count of in-match events and cumulative XP
   are **the same shape by construction**, not by analogy.

A raw gate counter therefore grows like `Θ²` while aptitude points grow like `Θ`. The transform that
reconciles them is a square root — and an arithmetic cost ladder **is** that square root, inverted.
So the mastery index is not a new curve: it is `RpgXpCurve`'s shape reading its own `(first, step)`
pair, exactly the precedent `ssot-power-scale.md` §10 row 26 set for `SpeciesXpCurve` (*"a separate
row rather than reusing row 6 because it reads its own tunable pair"*). **One power ladder is not
violated; a second copy of it would be.**

### 3.3 The calibration, worked from shipped constants

```text
aptitude side   base(i) = a_focus · Θ         a_focus = 1.625 = 3 × 0.54163   (the corner share, D29/D38)
                gate at  req(t) = s·t(t+1)/2  s = tierLadder.reqScalePoints = 5

counter side    index    n = M − 1 = ceil(req(t) / r)    r = mastery rate, equivalents per index
                count    C(M) = c·n(n+1)/2               c = masteryCurve first = step
                and      C = A·Θ²                        A = qualifying events per Θ²

                Θ_counter = n·sqrt(c / 2A)               Θ_apt = r·n / a_focus

  parity  ⇒  sqrt(c / 2A) = r / a_focus  ⇒  c = 2·A·r² / a_focus²
```

**`n` cancels.** The ratio between the two Θ curves is independent of `t`, because both sides are
triangular in their own index and the index is the square root of the count. That is the whole answer
to *"does it scale like aptitude points"*: **yes, identically, at every tier — by construction, not by
fit.** It is also why `c` is a single tunable rather than a ten-row table: a table would let a balance
pass break the parity silently, the same argument `tree-resolve` §3.1 makes for `k`.

**The anchor `A`, from shipped numbers.** A focused primary tree reaches tier 10 at
`Θ = 275 / 1.625 = 169`. Cumulative player XP there is `22.5 × 169² ≈ 642,600`, which at
`awards.kill = 12` is **≈ 53,550 kills**. Taking one qualifying application or elemental hit per kill
for a focused build gives `A = 53,550 / 169² = 1.875` events per `Θ²`.

**With `r = 4`** — the Aspect rate, `aptitudes.v5.json`
`pointEconomy.aptitudePointsPerThetaMilliByScope.aspect`:

```text
c = 2 × 1.875 × 4² / 1.625²  =  60 / 2.6406  =  22.72   →   c = 23    (rounded UP: the gate errs strict)
```

### 3.4 What that buys, tier by tier

| `t` | `req(t)` | index `M` | lifetime count | Θ implied | Θ for a focused **primary** tree | ratio |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 5 | 3 | 69 | 6.1 | 3.1 | 1.97 |
| 2 | 15 | 5 | 230 | 11.1 | 9.2 | 1.20 |
| 3 | 30 | 9 | 828 | 21.0 | 18.5 | 1.14 |
| 4 | 50 | 14 | 2,093 | 33.4 | 30.8 | 1.09 |
| 5 | 75 | 20 | 4,370 | 48.3 | 46.2 | 1.05 |
| 6 | 105 | 28 | 8,694 | 68.1 | 64.6 | 1.05 |
| 7 | 140 | 36 | 14,490 | 87.9 | 86.2 | 1.02 |
| 8 | 180 | 46 | 23,805 | 112.7 | 110.8 | 1.02 |
| 9 | 225 | 58 | 38,019 | 142.4 | 138.5 | 1.03 |
| 10 | 275 | 70 | 55,545 | 172.1 | 169.2 | 1.02 |

**Tier 1 diverges, and it is stated rather than hidden.** The 1.97× is entirely the `ceil` on a
two-step integer index — an absolute difference of three levels, at the cheapest tier in the game. It
shrinks monotonically and is inside 5% from tier 4 on. It errs **strict** (the counter tree is the
slower one), which is the safe direction for a gate: a tier that opens late is a pacing complaint, a
tier that opens early is content shipped before it was earned.

**`A = 1.875` is a working value, not balance.** It rests on one assumption no shipped code can
settle — how many qualifying events a focused build produces per kill. `c` is a tunable precisely so
that measuring it later is a config republish and not a spec edit. Calling `c = 23` calibrated would
be the mistake `aptitudes.v5.json`'s own `_weightsWhy` warns about: *"shipping a guess is fine;
calling it balance is not."*

---

## 4. Where the counters are persisted

### 4.1 The table — inputs only, sparse, uncapped

```sql
CREATE TABLE IF NOT EXISTS rpg_gate_counter (
  owner_kind TEXT    NOT NULL,   -- 'player' today; the column is what lets a per-specimen counter arrive later
  owner_key  TEXT    NOT NULL,   -- "player:{id}" -- AptitudeEndpoints.ScopeKey's shape (:93)
  quantity   TEXT    NOT NULL,   -- 'status_applied' | 'element_mastery'
  subject_id TEXT    NOT NULL,   -- a status id (21) or an element id (6)
  count      INTEGER NOT NULL,   -- long, cumulative, NO CAP
  PRIMARY KEY (owner_kind, owner_key, quantity, subject_id)
);
```

**Raw counts only. Never the index, never the equivalents.** `RpgStore.Aptitudes.cs:12-14` states the
rule this follows: *"INPUTS only, never a resolved channel value… a stored channel value would be a
second SSOT that goes stale the moment a coefficient moves."* Here the coefficient is `c`, and
storing the derived index would mean a tuning republish needed a data migration. It does not — which
is the same property `tree-state` gets from storing effort rather than power.

**Sparse: no row until the first credit.** `SaveAllocationUnlocked` skips zero
(`RpgStore.Aptitudes.cs:103-113`, *"no row for an unspent aptitude"*). A counter with no history has
no row, and a read of a missing row is `0` — never an error, never an invented default.

**No cap on `count`.** AGENTS.md: a cap on a magnitude is a progression ceiling. The absolute bound is
`long` overflow, and it throws (§6).

**The `owner_kind` / `owner_key` pair, not a bare `player_id`,** for the reason
`RpgStore.Aptitudes.cs:25-32` gives for `scope`/`scope_key`: a single typed column fits one owner
shape and this table has to survive a second one.

### 4.2 The file, and the boundary

**`src/FusionRpg.Data/Sqlite/RpgStore.GateCounters.cs`** — a partial-class slice on the existing
`RpgStore`, sharing the one connection, one `_gate` lock, one `EnsureHotSchema` dispatch and one
`Reset()`. That is the convention `RpgStore.Aptitudes.cs:16-23` documents, and the reason it gives is
the one that matters: a standalone class *"would fork that pipeline and silently drop out of
`Reset()`."*

**All SQL lives here and nowhere else.** `guard-dal.ps1` enforces it and CI runs it.

### 4.3 Writing without touching the hot path

A credit per landed hit is a database write per hit. That is exactly the shape the 2026-08 perf audit
identified as the lawn's problem, so:

- credits accumulate **in memory**, keyed `(ownerKey, quantity, subjectId) → long delta`;
- the accumulator flushes on a timer (`gateCounters.flushIntervalMs`, default **5000** — the window
  `PerfProbe` already uses) and unconditionally at match end;
- a flush is one batched `UPDATE … SET count = count + $delta` per key inside one transaction, with an
  `INSERT` for keys that have no row yet.

**A crash loses at most one window, and that is acceptable.** A counter is an accumulation: a lost
window costs a little progress, never correctness, and it can never make an open tier close. Losing a
*derived* value would be a different matter — which is another reason §4.1 stores none.

---

## 5. How they reach the tier gate without entering `AllocationScope`

### 5.1 Why the enum is closed to this

`AllocationScope` has exactly four members — `Commander, DemonType, Aspect, UniqueDemon`
(`AptitudeAllocation.cs:8`). D35's ruling is that status trees gate **outside** it, and the reason is
verifiable in six lines: `AptitudeAllocation.Total()` (`:51-57`) loops `AllScopes` and sums **every**
member for one aptitude, and `Share()` (`:81-85`) divides that by `GrandTotal()`. That denominator is
what `decisions.md:103` locks — *"`share` normalises over the actor's own total, so a granted aptitude
would dilute the other eleven."*

**Confirmed: a slot-5 and a slot-6 member are identically broken, and this module adds neither.**

### 5.2 The contract — one unit, one registry, one answer

```csharp
public readonly record struct GateQuantityId(string Family, string SubjectId);
//   ("status_applied", "wither")   ("element_mastery", "fire")   ("aptitude", "might")

public interface IGateQuantitySource
{
    string Family { get; }
    long AptitudePointEquivalents(GateQuantityId id, GateActorContext actor);
}
```

- **`tree-resolve` asks the registry and receives a `long`.** It never sees a count, an index, or an
  `AllocationScope`. `spec-tree-resolve.md` §3.3 already requires this: *"A tree whose category earns
  its gate quantity from a different scope is converted to aptitude-point-equivalents **before** it
  reaches this module."* This module owns that conversion for the two counter-backed families.
- **The unit is aptitude-point-equivalents, always.** That is what keeps `req(t)` meaning one thing.
- **Nothing here constructs an `AptitudeAllocation`.** The returned `long` is a number, not a row, so
  `Total()`, `GrandTotal()` and `Share()` are untouched. Test 9 asserts `GrandTotal()` is
  byte-identical before and after any number of credits.
- **`GateQuantityRegistry` is owned here, in wave 0**, and consumed by `tree-resolve` in wave 3. It is
  named so wave 3 does not define a second one.
- **`tree-resolve` still needs the tier-0 reason field it specs in §3.3** — *no aptitude allocated yet*
  versus *this quantity has no producer*. The registry answers the second directly: a family with no
  registered source is a known content gap, and a family with one that returns zero is a player who
  has not started. Neither is inferred from the zero.

### 5.3 The two families, and the one place `AllocationScope` legitimately appears

| Family | Rate | Route |
|---|---|---|
| `element_mastery` | `PointBudget.PointsFor(AllocationScope.Aspect, index − 1, tuning)` | The **shipped** consumption shape. `PointBudget.cs:15-18` says this type *"ships complete today and 'lights up' for Aspect the moment a caller has a real value to pass."* This module is that caller |
| `status_applied` | `(index − 1) × gateCounters.statusMasteryRatePoints` | D35 forbids an `AllocationScope`, so the rate is this module's own key. Default **4**, deliberately equal to the Aspect rate so the two counter-backed categories pace identically |

**`index − 1`, never `index`.** `PointBudget.DemonTypeSourceFromLevel` (`:40`) is the precedent and
gives the reason in full: *"a never-levelled species… must carry EXACTLY ZERO points."* Here the
consequence is sharper — index 1 is what every existing save has on day one, and a non-zero value
would open tier 1 on all 27 trees for free.

**Calling `PointsFor(Aspect, …)` allocates nothing.** It reads a rate out of
`AptitudePointsPerThetaMilliByScope` and returns a budget-shaped number (`PointBudget.cs:57-58`);
`SpeciesAllocation.cs:35` does exactly this for DemonType. What this module must never do is write
`AptitudeAllocation.Single(AllocationScope.Aspect, …)` — that would put mastery into the share
denominator, which is §5.1's whole point.

**The two rates must move together.** `statusMasteryRatePoints` defaults to the Aspect rate, and the
tuning load **refuses** when they differ and no `gateCounters.rateDivergenceWhy` string is present —
T5's "no built-in default" discipline applied to a coupling instead of a value. See OQ2: the Aspect
rate is an explicitly unmeasured placeholder owned by another program.

---

## 6. Numeric rules

CLAUDE.md's thresholds, applied line by line:

| Rule | Here |
|---|---|
| **`long` for any magnitude** | `count`, `CountToReach`, the index and the equivalents are all `long`. The `int` per-mille ceiling is `Θ = 3,213`; the `float` ceiling is **`Θ = 232`**, inside normal play |
| **Never `float`** | The index is found by integer binary search, never `Math.Sqrt`. `spec-tree-resolve.md` §3.1 ruled that *"a float has no place on a gate that decides whether content exists"*; the quantity the gate is measured against gets the same rule. Element component weights are `double` in the shipped payload and are therefore **read for presence only, never multiplied into a count** (§2.2c) |
| **Widen before multiplying** | `(index − 1) * ratePoints`, both operands already `long`. No `(long)(a * b)` anywhere |
| **Divide by 1000 last, exactly once** | There is no per-mille quantity in this module — counts, indices and equivalents are whole units. Stated so a later change does not quietly introduce one |
| **Overflow throws** | Every accumulate and every ladder sum is `checked`. A counter that would wrap is a thrown `OverflowException`, never a silently reset player |

**The one place overflow needs care is the index search.** `CountToReach` is quadratic in the index,
so a naive doubling search evaluates a product that overflows before it terminates for a count near
`long.MaxValue`. The predicate in §9 avoids the multiply entirely with an exact integer-division
comparison — for non-negative integers `a·b ≤ k` is exactly `b ≤ k / a` under floor division — so the
search never overflows and `checked` stays reserved for the real magnitude path.

**Two new `ssot-power-scale.md` §10.2 rows are owed** — one for the mastery ladder, one for the
count→equivalents read — at the next free ordinals (**29 and 30**; 28 is the highest today), and the
row-count line at `ssot-power-scale.md:587` moves with them. ⚠️ `guard-power.ps1` **cannot** catch
their absence: it keys on a parameter named `level`/`lvl`/`index`, and this module's parameter is
`count`. That is the same blind spot ideal §14 already records for `unlockCost` and `DropVolume`'s
`thetaActor`. **The rows get added deliberately or not at all.**

---

## 7. Prerequisites in shipped code

Three named changes. Nothing else in `src/` moves.

**P1 — the damage origin discriminator.** `DamageApplyPipeline.Apply` (`DamageApplyPipeline.cs:53-67`)
cannot tell a direct hit from a DoT pulse, and §2.2d needs it to. Add a
`DamageOrigin origin = DamageOrigin.DirectHit` **defaulted** parameter, so only the pulse construction
sites pass anything. The lawn pulse path and `BattleEngine.cs:125` (`StatusPulsePayload.For`) are
those sites.

⚠️ **Sequencing, not a blocker.** Threading the origin to battle's apply call may reach
`BattleRunState.cs` (its `DamageApplyPipeline.Apply` call site — **cite by symbol**, R9: the file is
under concurrent edit by `battle-tempo`, and `mechanism-wiring` G2 also modifies it). The defaulted
parameter exists specifically so that edit can be zero lines when the origin is set where the pulse
packet is built. **This is the one file the map's "nothing in wave 0 touches another wave-0 module's
files" claim does not cover, and it is named here rather than discovered in the merge.**

**P2 — a fresh-application event on `StatusRuntime`.** §2.1c needs to know whether `UpsertInstance`
added or replaced. Add a **new** property —

```csharp
public Action<StatusAppliedEvent>? OnFreshApplication { get; set; }
```

— fired only when the upsert added a new instance. **Do not change `OnApplied`.** It is a
single-assignment `Action<StatusInstance>?` (`StatusRuntime.cs:146`) with three assigning sites, one
of which chains by hand (`ActorHudInvalidator.cs:24-25` saves `prevApplied` first). Widening its
payload breaks all three; a separate property breaks none.

**P3 — the tuning file block.** `data/tuning/passive-tree.v1.json` does not exist yet and is named by
every module in this program (R2; `tree-catalog` §324 and `tree-state` §628 both record its absence).
This module contributes the `gateCounters` block only; its top-level keys are disjoint from
`tree-plan`'s and `tree-state`'s, so the file is a merge rather than a conflict. Whoever lands first
creates it with the standard `_meta` header, and **T4 applies from that moment: never hand-edit,
republish `v{n+1}`.**

---

## 8. Commands

```powershell
# Build + the suites this module touches
dotnet build src/FusionRpg.Core
dotnet test tests/FusionRpg.Core.Tests  --filter "FullyQualifiedName~GateCounter"
dotnet test tests/FusionRpg.Data.Tests  --filter "FullyQualifiedName~GateCounter"
dotnet test tests/FusionRpg.Guard.Tests

# Boundary guards -- guard-dal is the one that must pass before any store work is claimed done
.\scripts\guard-dal.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-power.ps1              # green with or without the two SSOT rows -- see 6

# Audits
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --domain passive-tree

# Test quality -- a gate counter is arithmetic, so coverage alone proves nothing
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.PassiveTree.GateCounters
.\scripts\mutate.ps1   -Set gate-counters
```

---

## 9. Code style

Repo house style: doc comments that carry the *reason*, `long` throughout, `checked` on every
accumulate, cited precedent rather than restated principle. One file, real:

```csharp
namespace FusionRpg.Core.PassiveTree.GateCounters;

/// <summary>
/// A gate counter is an ACCUMULATION; a tier gate reads an INDEX. This is the one place the two are
/// converted, and it is the same arithmetic ladder <c>RpgXpCurve</c> already uses to turn accumulated
/// XP into a level (<c>Progression/RpgProgression.cs:80,99</c>) — so the index is the square root of
/// the count.
///
/// <para>That is not a stylistic echo. <c>progression.v1.json</c>'s awards are paid PER EVENT
/// (<c>kill = 12</c>), so a cumulative event count and cumulative XP are the same shape by
/// construction; reusing the shape is what makes one <c>req(t)</c> threshold mean one Θ across every
/// tree category (spec §3.2). It is also not a second power ladder — it is row 6's shape reading its
/// own <c>(first, step)</c> pair, which is exactly the precedent <c>ssot-power-scale.md</c> §10 row 26
/// set for <c>SpeciesXpCurve</c>.</para>
///
/// <para><b>This repo has already paid for getting it wrong once.</b> <c>PointBudget.cs:20-26</c>: an
/// accumulation passed where an index was expected inverted the locked scope ordering by 176× at
/// ordinary play levels. <see cref="Index"/> is this module's <c>DemonTypeSourceFromLevel</c>.</para>
/// </summary>
public static class MasteryIndex
{
    /// <summary>Cumulative qualifying events needed to REACH <paramref name="index"/> — the triangular
    /// sum of <c>first + (m−1)·step</c>, identical in shape to <c>RpgXpCurve.TotalToReach</c>. `long`
    /// and `checked`: a lifetime counter is a magnitude (CLAUDE.md), so a sum that would overflow
    /// throws rather than wrapping a player's progress back to zero.</summary>
    public static long CountToReach(long index, GateCounterTuning tuning)
    {
        if (index <= 1) return 0;
        var n = index - 1;
        // n·(2·first + (n−1)·step) is always even -- if n is odd then (n−1)·step is even and 2·first
        // is even -- so the halving is exact and there is no rounding decision to get wrong.
        checked { return n * (2 * tuning.MasteryCurveFirstCount + (n - 1) * tuning.MasteryCurveStepCount) / 2; }
    }

    /// <summary>
    /// The index a raw count has reached. <b>Integer binary search, never <c>Math.Sqrt</c></b> —
    /// <c>spec-tree-resolve.md</c> §3.1 ruled that "a float has no place on a gate that decides whether
    /// content exists", and the quantity the gate is measured against gets the same rule. About 63
    /// comparisons, exact at every magnitude.
    ///
    /// <para><b>No cap.</b> The upper bound is found by doubling, not declared — a constant ceiling
    /// here would be a progression cap (AGENTS.md). <see cref="Reached"/> compares by DIVISION rather
    /// than multiplying, so the search itself can never overflow even when the count approaches
    /// <c>long.MaxValue</c>.</para>
    /// </summary>
    public static long Index(long count, GateCounterTuning tuning)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "a gate counter cannot be negative");

        long lo = 1, hi = 2;
        while (Reached(hi, count, tuning))
        {
            lo = hi;
            if (hi > long.MaxValue / 2) { hi = long.MaxValue; break; }
            hi *= 2;
        }
        while (hi - lo > 1)
        {
            var mid = lo + (hi - lo) / 2;
            if (Reached(mid, count, tuning)) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>`CountToReach(index) &lt;= count`, decided WITHOUT multiplying. For non-negative
    /// integers `a·b &lt;= k` is exactly `b &lt;= k / a` under floor division, so the comparison is
    /// exact and the search never has to evaluate a product that would overflow before it
    /// terminates.</summary>
    static bool Reached(long index, long count, GateCounterTuning tuning)
    {
        if (index <= 1) return true;
        var n = index - 1;
        long q;
        checked { q = 2 * tuning.MasteryCurveFirstCount + (n - 1) * tuning.MasteryCurveStepCount; }
        // Put the /2 on whichever factor is even, so the halved product is still exact.
        var (a, b) = (q & 1) == 0 ? (n, q / 2) : (n / 2, q);
        return a == 0 || b <= count / a;
    }

    /// <summary>Aptitude-point-EQUIVALENTS — the only unit `tree-resolve` ever sees (spec §5.2).
    /// <c>index − 1</c>, never <c>index</c>, mirroring <c>PointBudget.DemonTypeSourceFromLevel</c>
    /// (<c>:40</c>) and for a sharper version of its reason: index 1 is what every existing save
    /// carries on day one, and a non-zero value there would open tier 1 on all 27 trees for free.
    /// Both operands are already `long`, so the multiply is widened before it happens rather than
    /// after (CLAUDE.md rule 3).</summary>
    public static long Equivalents(long count, long ratePoints, GateCounterTuning tuning)
    {
        checked { return (Index(count, tuning) - 1) * ratePoints; }
    }
}
```

---

## 10. Project structure

### New files

| Path | What |
|---|---|
| `src/FusionRpg.Core/PassiveTree/GateCounters/GateQuantityId.cs` | `(Family, SubjectId)` and the id grammar |
| `src/FusionRpg.Core/PassiveTree/GateCounters/IGateQuantitySource.cs` | The one interface `tree-resolve` consumes |
| `src/FusionRpg.Core/PassiveTree/GateCounters/GateQuantityRegistry.cs` | **Exclusive** registration per family — §12 |
| `src/FusionRpg.Core/PassiveTree/GateCounters/MasteryIndex.cs` | §9 |
| `src/FusionRpg.Core/PassiveTree/GateCounters/GateCounterTuning.cs` | The typed view of the `gateCounters` block; no built-in defaults (T5) |
| `src/FusionRpg.Core/PassiveTree/GateCounters/GateCounterAccumulator.cs` | In-memory deltas plus the flush window (§4.3) |
| `src/FusionRpg.Core/PassiveTree/GateCounters/StatusAppliedCounter.cs` | The §2.1 rules, over `OnFreshApplication` |
| `src/FusionRpg.Core/PassiveTree/GateCounters/ElementMasteryCounter.cs` | The §2.2 rules, over the shared apply tail |
| `src/FusionRpg.Core/PassiveTree/GateCounters/StatusAppliedSource.cs` | `IGateQuantitySource` for `status_applied` |
| `src/FusionRpg.Core/PassiveTree/GateCounters/ElementMasterySource.cs` | `IGateQuantitySource` for `element_mastery`, via `PointBudget.PointsFor(Aspect, …)` |
| `src/FusionRpg.Data/Sqlite/RpgStore.GateCounters.cs` | **The only SQL** — a partial slice on `_gate` |
| `src/FusionRpg.Server/GateCounterEndpoints.cs` | `POST /api/gate-counters/credit` (the batched flush) and `GET /api/gate-counters/{playerId}` (counts, index and equivalents, for `tree-surface`) |
| `tests/FusionRpg.Core.Tests/PassiveTree/MasteryIndexTests.cs` | §11 tests 1–4, 12 |
| `tests/FusionRpg.Core.Tests/PassiveTree/GateCounterRulesTests.cs` | §11 tests 5–8 |
| `tests/FusionRpg.Core.Tests/PassiveTree/GateQuantityRegistryTests.cs` | §11 tests 10, 11, 13, 14 |
| `tests/FusionRpg.Data.Tests/GateCounterStoreTests.cs` | Sparsity, additive upsert, `Reset()` participation |
| `tests/FusionRpg.Guard.Tests/GateCounterAllocationGuardTests.cs` | §11 test 9 |

### Modified files

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Status/StatusRuntime.cs` | P2 — a new `OnFreshApplication` property. `OnApplied` untouched |
| `src/FusionRpg.Core/Combat/DamageApplyPipeline.cs` | P1 — a defaulted `DamageOrigin origin` parameter |
| `src/FusionRpg.Core/Battle/BattleEngine.cs` | P1 — the DoT pulse site passes `DamageOrigin.StatusPulse`. **Cite by symbol** (R9) |
| `src/FusionRpg.Injector/Effects/EffectRuntime.cs` | Subscribe both counters where the status runtime is already wired (`:59,69`) |
| `data/tuning/passive-tree.v1.json` | The `gateCounters` block (P3) |
| `docs/architecture/power/ssot-power-scale.md` | §10.2 rows 29 and 30, and the row-count line at `:587` |

**Nothing else.** No web, no Unity write, no `EntityStatWriter`, no funnel change.

---

## 11. Testing strategy

Coverage on arithmetic proves the line ran. Mutation proves the assertion would notice — and a gate
counter is exactly the shape where a covered line asserted by nothing is worth nothing.

| # | Test | What breaks it |
|---:|---|---|
| 1 | `Index_of_zero_is_one_and_equivalents_are_zero` | Day-one saves opening tier 1 on 27 trees for free (§5.3) |
| 2 | `Index_is_monotone_and_exact_at_every_ladder_boundary` | An off-by-one at `CountToReach(m)` exactly |
| 3 | `Index_never_uses_a_float` | Text guard: no `Math.Sqrt`, `double` or `float` in `MasteryIndex.cs` |
| 4 | `Index_search_survives_a_count_at_long_MaxValue` | The overflow a naive doubling search has (§6) |
| 5 | `A_resisted_application_earns_nothing` | §2.1b — drive `StatusRuntime.Apply` to a resist |
| 6 | `A_refresh_earns_nothing_and_a_fresh_host_earns_one` | §2.1c, the cheapest farm in the game |
| 7 | `A_zero_damage_hit_and_a_fully_absorbed_hit_earn_nothing` | §2.2b, against the pipeline's own miss-telemetry parity |
| 8 | `A_two_element_packet_credits_both_elements_once` | §2.2c |
| 9 | `Crediting_a_counter_never_moves_GrandTotal` | **D35's actual invariant.** Any number of credits; `AptitudeAllocation.GrandTotal()` and all twelve `Share()` values byte-identical |
| 10 | `A_second_producer_for_one_family_throws_naming_both` | §12 — the silent double-count |
| 11 | `Every_registered_family_answers_in_aptitude_point_equivalents` | A producer swap changing what `req(t)` means |
| 12 | `Tier_10_opens_within_five_percent_of_the_primary_tree_Theta` | §3.4's parity — the whole reason `c` has the value it has. Red the moment `c`, `r` or `s` moves alone |
| 13 | `A_missing_gateCounters_key_is_a_load_rejection_naming_it` | T5 |
| 14 | `A_status_and_aspect_rate_divergence_refuses_without_a_stated_why` | §5.3's coupling |

**Mutation set `gate-counters`** targets `MasteryIndex` and the two rule classes: flip `<=` to `<` in
`Reached`, drop the `− 1` in `Equivalents`, drop the refresh check, drop the `AppliedAmount != 0`
check. Each of those must be caught by a named test above. A survivor gets an explanation beside the
code.

---

## 12. The `aspect-scope` collision rule

`element_mastery` has a named future owner: the demon program's `aspect-scope` module
(`PointBudget.cs:15`). D37 does not cancel that module; it removes the dependency on its schedule. So
the two can collide, and **the failure mode to prevent is silent double-counting**, not duplication.

**The rule: exactly one producer answers a family, and a second one throws.**

- `GateQuantityRegistry.Register(IGateQuantitySource)` is **exclusive per `Family`**. A second
  registration throws, naming both owners. It is the *opposite* of `ActorHub.Register`'s
  replace-by-`SubsystemId` behaviour — replacement is right for a subsystem that composes, and wrong
  for a quantity that would otherwise be answered twice.
- **There is no combine path.** The registry has no sum, no max, no precedence chain. Two answers are
  **unrepresentable**, which is a stronger guarantee than a warning nobody reads. Summing is the
  silent double-count, and it is forbidden by name in §14.
- **The handover is one line at the composition root**: delete this module's registration, add
  `aspect-scope`'s. A merge that keeps both fails at startup, loudly, on the first resolve — not three
  weeks later as a tier that opened early.
- **Whoever wins speaks the same unit.** `IGateQuantitySource` returns aptitude-point-equivalents, so
  a swap cannot change what `req(t)` means. If `aspect-scope`'s quantity paces differently, that is a
  **balance** change to be measured, and the handover test reports the ratio of the two producers'
  answers for the same actor before either ships.
- **The rows stay.** `rpg_gate_counter` keeps its `element_mastery` rows through a handover — they are
  raw inputs, they are cheap, and keeping them makes the handover reversible. They simply stop being
  read. A `_meta` note in the tuning file records that.
- **A guard test over the composition root text** keeps the registration from being silently dropped,
  by `mechanism-wiring`'s `StatusDerivedWiringGuardTests` precedent.

---

## 13. Tunables

**File: `data/tuning/passive-tree.v1.json`** (R2), block `gateCounters`. Every key carries its unit
(T6). No built-in defaults — a missing key is a load rejection naming it (T5). Never hand-edited (T4).

| Key | Unit | Default | Why it is a tunable |
|---|---|---:|---|
| `gateCounters.masteryCurveFirstCount` | qualifying events | **23** | The ladder's first step. Derived (§3.3), not guessed — but it rests on an unmeasured anchor `A`, so a balance pass will move it |
| `gateCounters.masteryCurveStepCount` | qualifying events | **23** | Defaulted **equal** to `first`, which makes the ladder exactly triangular and gives the index the same `t(t+1)/2` shape `req(t)` and `W(t)` already use. The pair stays separate so a balance pass can move early pace alone, exactly as `RpgXpCurve` allows per actor kind |
| `gateCounters.statusMasteryRatePoints` | aptitude-point-equivalents per index | **4** | The status half of §5.3. Tracks the Aspect rate; a divergence refuses without `rateDivergenceWhy` |
| `gateCounters.rateDivergenceWhy` | string | *(absent)* | Present only when the two rates are deliberately different. Absent is the normal state |
| `gateCounters.flushIntervalMs` | milliseconds | **5000** | §4.3. Matches the shipped `PerfProbe` window — a balance pass would not move it, a perf pass would |

**Read from elsewhere, never copied:** `tierLadder.reqScalePoints` (owned by `tree-plan` /
`tree-resolve`) and `pointEconomy.aptitudePointsPerThetaMilliByScope.aspect` (owned by the class
system, `aptitudes.v{n}.json`). A copied number is a drift bug with a delay fuse.

**Structural, and each says so in a comment:** each element component credits exactly 1 (a count is a
count); `index − 1` (the index-vs-accumulation contract, `PointBudget.cs:40`'s precedent); the binary
search's doubling bound (an overflow bound, not a progression cap).

---

## 14. Boundaries

### Always

- Take every credit from an event the shipped code **already raises**, at a point it already reaches.
- Return **aptitude-point-equivalents** from `IGateQuantitySource`, always, for every family.
- Store **raw counts only** — inputs, sparse, non-zero only, uncapped.
- Keep all SQL in `src/FusionRpg.Data/Sqlite/RpgStore.GateCounters.cs`; run `guard-dal.ps1`.
- Use `long` and `checked` on every accumulate, and let overflow throw.
- Cite `Battle*` files **by symbol**, not by line (R9).

### Ask first

- **Any change to `c`, `r` or `s` in isolation.** They are calibrated against each other (§3.3);
  moving one alone silently re-paces 27 trees against 12. Test 12 goes red, which is the point.
- **Reading the shared Aspect rate versus owning a key** (OQ2) — a cross-program coupling.
- **Backfilling existing saves** (OQ1).
- **Any per-specimen counter.** The `owner_kind` column exists for it; using it is a product decision
  about whether a new demon starts its status trees at zero.
- **Measuring `A`.** The anchor is a measurement task for `squad-harness` or a telemetry window, and
  its result republishes `c`. It is not a spec edit.

### Never

- **Add a fifth `AllocationScope` member, or write an `AptitudeAllocation` row from a counter.** D35,
  and `AptitudeAllocation.cs:51-57` is why.
- **Sum two producers for one family.** §12 — the registry makes it unrepresentable, and it stays that
  way.
- **Store the index or the equivalents.** A derived value in the store is a second SSOT
  (`RpgStore.Aptitudes.cs:12-14`).
- **Cap a count**, or clamp one at a bound. AGENTS.md: an absolute bound is derived and throws.
- **Use a `float` or `double` on any counter, index or equivalent.** The `float` ceiling is `Θ = 232`.
- **Put per-hit state on the lawn hot path** — no ICD map, no per-target dedupe cache (§2.3).
- **Change `StatusRuntime.OnApplied`'s signature.** Three sites assign it and one chains by hand.
- **Generate tree content for a category whose counter is not wired end to end.** R-G1
  (`tree-plan` §7.1) still holds; this module makes the wait bounded, not zero.

---

## 15. Success criteria

1. `status_applied.<id>` and `element_mastery.<id>` each have a production carrier in `src/` — the
   condition `tree-plan` §7.1's R-G1 gate tests for. Both `gateState` evidence rows move from
   `pending` to `carrier`, unblocking **1,080 nodes** for generation.
2. All **39** generic trees are reachable above tier 0, not 12 (D37).
3. A focused build reaches tier 10 on a status or elemental tree within **5%** of the Θ at which it
   reaches tier 10 on a primary tree, measured, at every tier from 4 up (test 12, §3.4's table).
4. Crediting any counter any number of times leaves `AptitudeAllocation.GrandTotal()` and all twelve
   `Share()` values byte-identical (test 9). D35 holds as an executable property, not an argument.
5. `guard-dal.ps1`, `guard-single-writer.ps1`, `guard-funnel-delta.ps1` and `guard-power.ps1` pass.
6. `audit-overflow.py` reports no new critical finding, and
   `audit-magic-numbers.py --domain passive-tree` reports no M1 in this module's files.
7. Two `ssot-power-scale.md` §10.2 rows exist and the file's own row-count line agrees with them.
8. A duplicate registration for either family throws at startup naming both owners (test 10).
9. The lawn's per-hit cost is unchanged within probe noise — a credit is an in-memory increment and a
   5s batched flush, never a write.

---

## 16. Open questions — owner decisions only

**OQ1 — cold start for existing saves.** These counters have no history, and none can be
reconstructed: there is no event log to replay. On the day this ships, every current player has all 27
counter-backed trees at tier 0 while their primary trees are already deep. Accept the cold start, or
seed an initial count from a proxy the save does have — player level through §3.2's `A`, which would
hand a level-169 player ≈53,000 applications they never made? **This is a product decision about how a
live save feels, not an engineering one.** The spec assumes cold start.

**OQ2 — should `element_mastery` read the shared Aspect rate, or own its own key?** §5.3 routes it
through `PointBudget.PointsFor(AllocationScope.Aspect, …)` because that is the shipped shape and
`PointBudget.cs:15-18` explicitly reserves it for this caller. But that rate is an **unmeasured
placeholder owned by another program** — `aptitudes.v5.json`'s own `_weightsWhy` says so:
*"UNMEASURED… residual-fit (Phase 8) owns the real values."* When the class system republishes it,
6 elemental trees re-pace while the 21 status trees do not. The refuse-on-divergence check makes that
loud rather than silent, but it does not decide **who should own the number**. A separate
`elementMasteryRatePoints` key here costs one line and severs the tie to `AllocationScope` entirely;
reading the shared one keeps a single rate for the Aspect concept.

---

## 17. Decisions implemented

| Decision / ruling | Where it lands here |
|---|---|
| **D37** the two missing gate quantities are built inside this program | The whole module. §1's table is the finding; §2–§5 are the closure |
| **D35** status trees gate outside `AllocationScope` | §5.1 verifies the reason at `AptitudeAllocation.cs:51-57` and `decisions.md:103`; §5.2's contract returns a `long`, never a row; test 9 makes it executable; §14 forbids the fifth member by name |
| **D31** `status_mastery` takes `AllocationScope` slot 6 | ⛔ **Superseded by D35** and not implemented. Recorded so it is not re-derived |
| **D19** `status_mastery` becomes a fifth `AllocationScope` | ⛔ **Superseded by D35.** Its intent — per-status progression earned through use — survives as §2.1's counter |
| **D26** `req(t) = k·t(t+1)/2` | §3.3 calibrates `c` against it so `W(t)/req(t)` means the same thing on a counter-backed tree as on a primary one; §3.4 measures the result; test 12 pins it |
| **D12** tier gates read base allocation, never item bonuses | Free here for the same reason `tree-resolve` §3.2 gives, one step stronger: no item can write a play counter either. §14 forbids any other write path |
| **D28** cross-unlock reads the same base quantity | §2.2c refuses the largest-component rule that would penalise a hybrid build at the gate |
| **D38** `g = 11`, sized against the measured corner build | §3.3 uses the same corner share (`a_focus = 1.625 = 3 × 0.54163`) as its anchor, so the counter is sized against the build `g` is sized against. If `g` is ever republished to the 19.2 side, `c` moves with it |
| **R1** the tier gate reads aptitude points, not skill points | §5.2's unit is aptitude-point-**equivalents**. This module never reads the skill wallet or `grant.skillPointsPerThetaMilliByScope` |
| **R2** tunable names carry their unit, one file | §13 — `…Count`, `…Points`, `…Ms`, all in `passive-tree.v1.json` |
| **R7** five tree categories | §1's table covers all five; two of them are this module's |
| **R9** cite `Battle*` by symbol | §7 P1 and §10 |
| **Corpus numbers** 6 elements · 21 statuses | Counted and cited: `ActorElementTypes.cs:21-29`, `StatusCategoryRegistry.cs:7-28` |
| **`tree-plan` R-G1** a gate quantity must exist before content is generated | Unchanged, restated in §14. This module is what makes the wait bounded |
| **AGENTS.md** no hard progression ceilings | §4.1 (no cap on `count`), §9 (no declared search ceiling), §13 (structural bounds say why) |
| **CLAUDE.md** numeric overflow | §6, and §9's division-based predicate is the non-obvious half |
| **tunables-ssot T1–T7** | §13 |
| **decisions.md:103** the aptitude share denominator | §5.1, §5.3, test 9 |

---

## 18. Design-gate checklist

- [x] `DESIGN-GATE.md` §1 rows for this subsystem read this session, and the documents they name:
      `passive-tree-map.md`, `passive-tree-ideal.md` (§13.4, §14, D19/D26/D31/D35/D37/D38), the
      cross-spec rulings, and the sibling specs that consume this module — `tree-resolve` §3,
      `tree-plan` §7/§7.1, `tree-state` §2.2d, `mechanism-wiring` §3.
- [x] Every claim about shipped code verified by opening the file, not by reading a comment about it.
      Where a comment **is** the evidence (`PointBudget.cs:15`) it is quoted as a comment, and the
      surrounding code is what the design rests on.
- [x] Constraints tested rather than assumed: `AllocationScope` has four members and `Total()` sums all
      of them; `OnApplied` is single-assignment with three assigning sites and one hand-chained;
      `DamageApplyPipeline` lets zero deltas through on purpose; `data/tuning/passive-tree.v1.json`
      does not exist; the highest `ssot-power-scale.md` row ordinal is 28.
- [x] Section read, not line: `PointBudget`'s Aspect comment is read together with its own statement
      that the type *"ships complete today and 'lights up' for Aspect the moment a caller has a real
      value to pass"* — which is what makes §5.3 a wiring step rather than a new capability.
- [x] Every RPG feature in the RPG layer. Nothing here asks PvZ to know what a status count is; both
      observation points are RPG-layer code that already runs during a lawn match.
- [x] Open questions are owner decisions, not filler. Two, each with a stated default and a named
      consequence. The unmeasured anchor `A` is a **measurement task**, so it sits in §3.3 and §14's
      "Ask first" rather than being dressed up as a question.
- [ ] `squad-harness` has not yet proposed a value for `A`. `c = 23` is a working value derived from
      shipped constants (§3.3) and is explicitly not called balance.
