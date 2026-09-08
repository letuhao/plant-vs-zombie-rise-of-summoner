# Spec: `unit-class-close` — give every family the class its formula already implies

**Module id:** `unit-class-close` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** nothing · **Blocks:** `aptitude-tuning`

---

## 1. Objective

Give each of the **29 of 50** catalog families that carry `unitClass: null` its honest answer — **a
class where a reader exists, a documented `null` where none does** — so that every coefficient in this
program is a **derivation** rather than a guess with a measurement attached. Same for the **3**
carrying `statClass: null`.

> **It is 18 classifications and 11 documented nulls, not 29 classifications** (§3, measured
> 2026-08-26). An earlier draft of this spec assumed all 29 were classifiable, which contradicted its
> own §2 step 5.

**Counted this session** from
[data/seed/derived-stats/catalog.json](../../../data/seed/derived-stats/catalog.json), not quoted from
an older document:

```text
entries          50
unitClass null   29        statClass null   3
prefixFamilies    9        of which unitClass null   4
```

**Why it blocks `aptitude-tuning`.** That file's `familyRead` block is *"the `unitClass` decision per
family"* ([spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2) and decides which of the two PS-3 read
functions applies. A family with no class cannot have a `familyRead` row, and an edge into it is
rejected by that module's test 3. **The measured consequence of guessing wrong is not a small error:
matchups fully invert across the ladder** ([class-rps-balance-2026-08-25.md](../../research/class-rps-balance-2026-08-25.md) §3.1).

**Not a balance decision.** The answer is determined by the formula, not chosen by a designer, which is
why this module is first alongside `primary-stats` and depends on nothing in the class system. It is a
`derived-stats` leftover that the class system happens to be the first consumer to need closed.

**Sequence it immediately before `distribution-reconcile` §3.2a** (decided 2026-08-26). That item
widens `BattleStatComposer`'s known-channel set over families this module is already reading the
consumers of — `resource.*`, `skill.*`, `move.range`, `progression.*`, `status.duration/intensity.*`.
**Two distinct gates, one set of consumer readings**: this module blocks `aptitude-tuning`, that item
blocks `aptitude-resolve`, and §3.2a consumes these readings rather than repeating them. **Not merged**
— the overlap is one sub-item of a nine-item register against 29 families, and merging would make an
XL module out of two gates that fire at different times.

**Users:** `aptitude-tuning`; the actor sheet (guard 1 of
[design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §8 refuses a renderable
unclassified channel); anyone authoring an affix on these families.

---

## 2. The decision procedure — read the consumer, never the name

**The rule the ledger is built on** ([spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)
§3): *"the right-hand column is the consumer I read this session; a channel whose consumer I could not
name does not get a class, it gets a rejection."* Guard 1 there puts it as a design constraint rather
than a convention: *"a channel's unit is inseparable from its consumer, and declaring it elsewhere lets
the two drift invisibly — the number still renders, it is just wrong by an order of magnitude."*

So, per family:

1. **Find its reader in `src/`.** `file:line`, not a grep count.
2. **Read what the value is compared against.** Compared against `baseLong` — the hit itself — it is a
   magnitude. Fed through a small scale into a bounded ratio, it is a contest.
3. **Assign from the ten existing classes** if one fits its arithmetic exactly.
4. **If none fits, stop and say so** (§4). Do not stretch a class to cover a shape it does not have —
   that is the failure mode that put `resolverPoints` on six families it was 10× wrong for
   ([spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §2).
5. **A family with no reader gets a rejection, not a class.** `status.expose.*` is the shipped
   precedent — *"registered vocabulary with ZERO readers today"*.

**`statClass` follows the same procedure** against the four-class taxonomy `contest · race · pool ·
feeder` ([derived-stats/spec-stat-taxonomy.md](../derived-stats/spec-stat-taxonomy.md)), including its
pair rule — a contest-class family without a counterpart fails `guard-stat-pairs.ps1`.

---

## 3. ⛔ The 29 are not 29 classifications — 8 of them have no reader at all

**Measured 2026-08-26**, after the owner asked why this spec's *"constraints tested"* box was still
partial. It was partial because §3 was a **prediction**, and running the census showed the prediction
was wrong about its central premise.

**Corrected same day, before build:** the first census pass (below, "11") missed a genuine reader for
four families — `status.duration`/`durationReduction`/`intensity`/`intensityReduction` are read by
[ResistanceEvaluator.cs:331-336](../../../src/FusionRpg.Core/Status/ResistanceEvaluator.cs)'s
`ComputePotencyDelta`, but through a **dynamically built** channel id (`$"status.{family}.{omni|
category|statusId}"`), not the named C# constant — invisible to a constant-name grep, the exact method
this census used. A second pass that also greps for `.Get($"...")` string interpolation found it. Net:
**11 − 4 (now classified `StatusPotencyPoints`, §3.3) = 7**, but `resource.efficiency` — flagged
ambiguous below and treated as reader-less per this section's own recommendation — brings the
documented-null count to **8**. All four corrected families are `StatusPotencyPoints`, not a new class:
they share `status.power`/`status.resist`'s exact formula shape, split into a second potency axis
(T3.2), not a different one.

```text
29  families carry unitClass: null
11  have ZERO reader in src/ by constant-name grep alone -> CORRECTED to 8, see above
18  have a reader by constant-name grep alone            -> CORRECTED to 21 (+4 status, +poise's
                                                              contribution is separate, module 4)
```

**This spec's own §2 step 5 already said what to do**, quoting the ledger: *"A family with no reader
gets a rejection, not a class."* `status.expose.*` is the shipped precedent — *"registered vocabulary
with ZERO readers today."* **I wrote that rule and then predicted a group that violates it.**

### 3.1 The 8 with no reader — documented `null`, with a reason

**Corrected from 11 (§3 above): `status.duration`/`durationReduction`/`intensity`/`intensityReduction`
moved to §3.3 — they have a reader, found on a second census pass.**

| Family | Aptitude edges pointing at it |
|---|---|
| `resource.max` | 60 |
| `resource.regen` | 60 |
| `skill.effectiveness` · `skill.cooldown` | 6 · 5 |
| `move.range` · `progression.xpRate` · `progression.breakthroughSuccess` | 1 each |
| `resource.efficiency` | (ambiguous — see below; its edges are not separately counted in the 486 total's reserved share) |

> **These cannot be classified, and trying is the failure mode this module exists to avoid.** A unit
> class is *"inseparable from its consumer"* (ledger guard 1) — with no consumer there is nothing to be
> inseparable from, and any class assigned is a guess that will render a number wrongly by an order of
> magnitude the day a reader appears.

**`resource.efficiency` is the ambiguous one** and the module should decide it deliberately: its only
two hits are in `DerivedStatTuning.cs`, which **configures its cap** rather than reading its value.
A cap is not a consumer. Recommend treating it as reader-less until something reads it.

> **⛔ Superseded in part, 2026-09-02 (owner).** Reader-less or not, `resource.efficiency` **must cover
> all six resources** — it has edges for only three (`hp`, `spirit`, `poise` have none), and actions can
> cost all six. **That is a defect to fix, not an ambiguity to park.** The reader-less verdict may still
> stand for `UnitClass` purposes; the coverage gap does not. See the six-coverage rule in
> [`../resource-hub-ssot.md`](../resource-hub-ssot.md).

### 3.2 ⛔ 28% of the aptitude distribution points at those 8 families

**Corrected 2026-08-26 (same pass as §3's fix): the first count, 67% / 326, included the 192 edges
(48 × 4) feeding `status.duration`/`durationReduction`/`intensity`/`intensityReduction` — now known to
have a reader (§3 above). Recomputed directly from the shipped edge list, not adjusted by hand:**

```text
486  aptitude edges in the shipped config
138  (28%) land on a family with NO READER   -- was 326 (67%) before the status-family correction
```

Per family: `resource.max` 60 · `resource.regen` 60 · `skill.effectiveness` 6 · `skill.cooldown` 5 ·
`resource.efficiency` 4 · `move.range`/`progression.xpRate`/`progression.breakthroughSuccess` 1 each.

**Still real, just smaller than first measured.** [spec-residual-fit.md](spec-residual-fit.md) §3's
**15–47% reserved by coefficient weight** and
[spec-distribution-reconcile.md](spec-distribution-reconcile.md) §3.2a's **47 of 84 channels outside
the battle known-channel set** are independent measurements on different axes (weight, not edge count;
combat channels only, not the full 29-family set) and are **not** corrected by this section — they
were never derived from the 326/67% figure this section retracts.

> **More than a quarter of what an aptitude buys is written into channels that nothing in `src/`
> reads.** Not *"the mechanism is unbuilt"* — the **channel itself has no consumer**. `residual-fit`
> calls this a RESERVATION and that framing still holds, but the reservation is one layer deeper than
> it looked.

**It does not change this program's design**, and it must not be mistaken for a defect in the
distribution: the coefficients are right about what an aptitude *should* feed. It changes what
`unit-class-close` can honestly deliver, and it tells `residual-fit` exactly which coefficients are
unfalsifiable and why.

### 3.3 The 18 with readers — the real work

**Verified to have a consumer** (hit counts from the census, excluding the two files that define and
register channels):

| Family | Reader | Class |
|---|---|---|
| `combat.penetration` · `absorption` · `amplification` · `reduction` | `CombatDerivedReader` + `OverlayCombatCalculator` (3 hits each) | **`ReciprocalPoints`** — §3.5, verified |
| `combat.parry.{rate,break,shred,strength}` · `combat.block.{...}` | `CombatDerivedReader` | `ClampedContest`; `rate` **confirmed `PerMilleRatio`** — [OverlayCombatCalculator.cs:168](../../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs) divides by `1000.0` |
| `combat.reflect.{rate,damage}` + both resists | `CombatDerivedReader` | `rate` per-mille; `damage` is a `[0,1]`-clamped share |
| `combat.heal.power` | `OverlayCombatMath` | `GameUnits` — flat, unpaired `Pool` |

**All four reciprocal families have readers**, which is what makes §3.5's new class safe to authorise.

### 3.4 Needs care — the consumer's shape is not the obvious one

| Family | Why |
|---|---|
| `combat.parry.strength` · `block.strength` · `shred` · `break` | Consumed by `ClampedContest` ([ClampedContest.cs:10](../../../src/FusionRpg.Core/Combat/ClampedContest.cs)). `decisions.md` records that `strength` was **inert** until `parryNeutralShareKPm` seated the neutral point inside the clamp — the naive reading was wrong once already |
| `combat.reflect.damage` · `reflect.resist.damage` | `reflectShare` is clamped to `[0,1]`, and `decisions.md` records the consequence: *"against an equal-HP attacker a pure thorns build can only ever tie, never win"* |

**`skill.cooldown` and `skill.effectiveness` move to §3.1** — they were in this group as *"needs care"*,
but the census says they have **no reader at all**, so there is no shape to be careful about yet. The
divisor rule and the feeder-placement rule that
[derived-stats-map.md](../derived-stats-map.md) §2.1 and
[spec-skill-modifiers.md](../derived-stats/spec-skill-modifiers.md) already decided stay on record for
whoever gives them one.

### 3.5 `ReciprocalPoints` — a new class, authorised 2026-08-26

**Filled in 2026-08-26** (class-system, `unit-class-close` build pass) — §3.3 referenced this section
before it existed; the table's classification stood on the evidence below, just not written out.

[design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3's ten classes have no
row for `combat.penetration`/`absorption`/`amplification`/`reduction`. `GameUnits` is the *closest*
existing class — both are flat, ladder-scaled point deltas with no context part — but it is the wrong
one: `GameUnits` has **no bounding curve at all** (`+45 hp` just adds), while these four feed a
**reciprocal**, asymptotic factor:

```csharp
// OverlayCombatCalculator.cs — the mitigation chain
PierceFactor(d, s)         = 1.0 / (1.0 + Math.Max(0.0, d) / s);        // → 0 as d → ∞, never reaches it
AmpFactorReciprocal(d, s)  = d >= 0 ? 1.0 + d/s : 1.0 / (1.0 - d/s);    // mirrored, same asymptote shape
```

**Why not `SigmoidPoints`.** That class is defined by a specific curve — `CombatProbability.Sigmoid`,
`(0,1)`-bounded, `0.5` at `delta = 0`. `PierceFactor`/`AmpFactorReciprocal` are a *different* bounded
curve (reciprocal, not logistic) with a different neutral value (`1.0` at `delta = 0`, not `0.5`).
Stretching `SigmoidPoints` to cover this shape is exactly the failure §2 step 4 forbids — it is how
`resolverPoints` ended up 10× wrong on six families before this ledger existed.

**The name follows the `SigmoidPoints`/`SigmoidMultiplierPoints` precedent**: an uncapped point
investment, named for the curve its effect saturates through. `ReciprocalPoints` is that pairing's
reciprocal-curve sibling.

**Render shape**: same estimate/suppression rule as `StatusPotencyPoints` (§4.3) — a raw point delta
(`Onslaught 40 penetration`) says nothing determinate on its own, since the effect depends on the
opponent's `absorption` too. Context part suppressed off an allocation surface, shown as an estimate on
one.

**Verified reader** (class-system P1.5 census, 2026-08-26): `CombatDerivedReader` (accessor) →
`OverlayCombatCalculator.cs` (the mitigation chain above), 3 hits each across the four families —
confirming all four have a live production consumer, which is what makes authorising a new class safe
rather than speculative (§2 step 4's own bar).

**Not extended to `combat.parry.strength`/`shred`/`combat.block.strength`/`shred`** (§3.4's own "needs
care" group), even though they also feed a bounded outcome: their consumer, `ClampedContest.Apply`,
clamps **linearly** to a permille share, not asymptotically — a different shape again, and stretching
`ReciprocalPoints` to cover it would repeat the exact mistake this section exists to avoid. Those four
stay `GameUnits` — the least-wrong existing fit — with the residual gap on record rather than papered
over (`DerivedStatChannels.cs`'s own comment at the `CombatFamilyUnitClass` entries).

## 4. Commands

```powershell
# The count, before and after. Must reach zero.
python -c "import json,io; d=json.load(io.open('data/seed/derived-stats/catalog.json',encoding='utf-8')); print(sum(1 for e in d['entries'] if not e.get('unitClass')), 'unitClass null'); print(sum(1 for e in d['entries'] if not e.get('statClass')), 'statClass null')"

dotnet test tests\FusionRpg.Core.Tests --filter DerivedStat
.\scripts\guard-stat-pairs.ps1
python scripts\audit-magic-numbers.py --domain derived-stats
```

---

## 5. Project structure

```text
data/seed/derived-stats/catalog.json                     the 29 rows filled
src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs  registration stays code-first; the json mirrors it
tests/FusionRpg.Core.Tests/Stats/Derived/UnitClassCoverageTests.cs
web/fusion-rpg-web/src/contract/types.ts                 only if §3.3 adds a class
```

**The json is a mirror, not the driver.** Its own `_meta` says so: *"Registration itself stays
code-first in `Stats/Derived/DerivedStatRegistry.cs` — this file describes it, it does not drive it."*
So a class is assigned in code and the seed file is regenerated, never hand-patched into agreement.

---

## 6. Code style

Each assignment carries its evidence **at the declaration**, because that is what stops the drift
guard 1 exists to prevent:

```csharp
// unitClass: PerMilleRatio - consumed by ClampedContest as a clamped per-mille share,
// neutral point seated by parryNeutralShareKPm (decisions.md, Combat mitigation shapes).
// statClass: Contest - counterpart combat.parry.shred.
```

**A `null` that survives this module must carry a reason**, in the `unitClassNote` field the schema
already has — the `status.expose.*` precedent. *"Not yet decided"* is not a reason; *"no reader in
`src/`"* is.

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `No_family_lacks_a_unit_class_without_a_reason` | Every `null` carries a `unitClassNote` naming why. **This is the test, not the count** — it stays green as families are added |
| 2 | `No_family_lacks_a_stat_class_without_a_reason` | Same for the three |
| 3 | `Seed_catalog_matches_the_registry` | The json mirror equals `DerivedStatRegistry.CreateDefault()`, both classes per family. Stops the mirror drifting |
| 4 | `Every_contest_family_has_a_counterpart` | Existing `guard-stat-pairs.ps1`, re-run over the newly classified families |
| 5 | `Every_bounded_ratio_carries_its_PS8_exemption_comment` | §11.6's rule, over the ratio families this module classifies |
| 6 | `Zero_goldens_move` | Classification is metadata. If a battle hash moves, something was wired rather than labelled |

---

## 8. Boundaries

**Always** — read the consumer at `file:line` before assigning; record the evidence at the declaration;
regenerate the seed mirror rather than hand-editing it.

**Ask first**

- ~~Adding a new `UnitClass`~~ — **granted 2026-08-26** for `ReciprocalPoints` (§3.3). A *further* one
  still needs asking; the grant covers the two named, not the category.
- Assigning a class to a family whose reader you could not find. The answer is a documented `null`.

**Never**

- Assign a class from the family's **name**. `combat.reflect.damage` sounds like `GameUnits` and is a
  clamped share.
- Change a formula while classifying it. This module labels; it does not tune (T7).
- Let the seed json and the registry disagree, even briefly.

---

## 9. Success criteria

1. `unitClass: null` count is **0**, or every remainder carries a `unitClassNote` naming a missing
   reader.
2. `statClass: null` count is **0**, on the same terms.
3. Every assignment cites its consumer at `file:line`, at the declaration.
4. `guard-stat-pairs.ps1` green over the newly classified contest families.
5. **Zero goldens moved.**
6. `aptitude-tuning` can write a `familyRead` row for every family an aptitude edge touches — the
   actual point of the module.

---

## 10. Design-gate checklist

```
[x] Subsystems identified: stats, status, combat damage, resources, power scale, UI contract.
[x] Read this session: DESIGN-GATE.md, decisions.md (Stats, Actor Hub, Combat mitigation shapes,
    Status SSOT, Resource model rows), design/spec-magnitude-and-units.md (full, incl. guard 1 and
    the resolverPoints correction), derived-stats-map.md (full, incl. §2.1's divisor rule),
    resource-hub-ssot.md, tunables-ssot.md, ssot-power-scale.md §4.6/§10.
[x] Every factual claim cites a file, a line or a document section.
[x] Verified against DATA, not documentation: the 29 / 3 / 4 counts were COUNTED from
    data/seed/derived-stats/catalog.json this session. The old map said 29 and it holds.
[x] Read the surrounding section of every rule quoted - guard 1's rationale, not just its row;
    §2.1's divisor rule under its own heading.
[x] Constraints TESTED, not assumed - CLOSED 2026-08-26 by running a reader census over all 29
    families (constant references and literal channel strings across src/, excluding the two files
    that define and register channels). It OVERTURNED this spec's own §3 TWICE: first to 11 of 29
    with no reader (constant-name grep), corrected the same day to 8 once a second pass also checked
    dynamic `.Get($"...")` string-interpolated reads and found status.duration/durationReduction/
    intensity/intensityReduction genuinely read (§3, §3.3). ReciprocalPoints' four families were
    confirmed to have readers (3 hits each), so that authorisation stands. Also measured, then
    corrected alongside: 138 of 486 aptitude edges (28%, not the first pass's 326/67%) point at
    reader-less families.
[x] Nothing contradicts a §2 invariant. PS-8: every ratio family classified here is a BOUNDED RATIO
    and test 5 asserts the exemption comment; skill.cooldown's divisor floor is a STRUCTURAL LIMIT
    and derived-stats-map.md §2.1 already ruled it so.
[x] Corrections propagated - none owed; this module adds metadata and moves no existing claim.
```

---

## 11. Related

- [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 (the ledger), §8 (guard 1)
- [derived-stats/spec-stat-taxonomy.md](../derived-stats/spec-stat-taxonomy.md) · [derived-stats-map.md](../derived-stats-map.md) §2.1
- [spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2 (`familyRead`), §2.2 (the shapes that differ)
- [decisions.md](../decisions.md) — *Combat mitigation shapes* (2026-08-25) is the record of every shape here
