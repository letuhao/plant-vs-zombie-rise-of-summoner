# 18 — Spec seam audit: where the eleven passive-tree specs disagree

**Status:** audit, 2026-09-05. Research note for [passive-tree](../../architecture/passive-tree-map.md).
Read-only — this note changes no spec, no code and no data.

The eleven specs under `docs/architecture/passive-tree/` were written in parallel, by different
sessions, from the same source docs, without seeing each other. The map names the dependency
direction; this note reads the arrows and reports where the two ends do not meet.

**Method.** Read all eleven specs and the map in this session, then verify every load-bearing claim
against `src/`, `data/` and `tools/` rather than against another spec. Every finding below quotes both
sides and cites `file:line`. Where two specs agree, that is stated as agreement, not padded into a
risk.

**What was checked in code this session:** `ActorHub.Register` and its three registrations,
`AtomCompiler`'s ladder branch, `ValueSpec.PowerLadderKMilli`, `AtomKindRegistry`'s three counts,
`UnitClass`'s thirteen members (counted), the derived-channel count, the 53 derived-stat families,
the 21 statuses, the 98 affix families, the 840 species ids, the `container_id` grammar,
`AptitudeAllocation.Single`'s throw and its per-row caller, `CombatDamageDispatcher.TryReflect`'s
reachability from Battle, and roughly thirty cited line numbers across `BattleModels.cs`,
`BattleRunState.cs`, `BattleEngine.cs` and `RpgStore.Aptitudes.cs`.

---

## 1. Seam table

| # | Seam | Verdict | One line |
|---|---|---|---|
| S1 | `tree-plan` → `tree-language` | ⛔ **CONFLICT** | Four of the five plan fields `tree-language` declared as a required interface do not exist under those names or those semantics in the schema `tree-plan` since froze |
| S2 | `mechanism-wiring` vs `tree-resolve` — the fourth subsystem | ✅ **AGREE** | Different `SubsystemId`s, so `ActorHub.Register`'s replace-by-id cannot evict either. Both land; order does not matter |
| S3 | `tree-plan` → `tree-catalog` → `tree-binder` — `kMicro`, rounding, `weightTotal` | ⛔ **CONFLICT** | `tree-binder`'s formula distributes tier budget ∝ `w[t]·t`; the plan distributes it ∝ `t`. They agree only for the uniform archetype, which is one of the three that ship |
| S4 | `tree-catalog` → `tree-state` — ids, retirement, "owned" | ✅ **AGREE** | Same id string, same three-way live/retired/unknown classification, same import-boundary rejection. Written by one session and they match |
| S4b | `tree-plan` → `tree-catalog` — the node id | ⛔ **CONFLICT** | The plan mints `<treeId>/<off\|def>/t<tier>/<index>`; the catalog mints `skill.<treeId>-<branch>-t<tier>-<nodeKey>` and refuses positional ordinals. Neither string can survive the other's validator |
| S5 | `tree-state` → `tree-resolve` — what is read vs what is stored | ✅ **AGREE**, with a gap | Nothing contradicts; but no spec names the caller that actually performs `LoadTreeStateBatch` |
| S6 | `tree-catalog`/`tree-plan` → `species-tree` — 40 nodes, 840 species | ✅ **AGREE** between those three | ⛔ but `tree-state` and `tree-surface` carry the superseded 29-node / 841-species figures (see C11) |
| S7 | `tree-review` → `tree-catalog` — id stability | ✅ **AGREE** | `tree-review` quotes the catalog's slug verbatim and its `O(diff)` claim rests on exactly the property the catalog guarantees |
| S8 | `tree-surface` → everything — the magnitude contract | ✅ **AGREE** on the contract | Renders `GameUnits`, `count` and `perMilleRatio` through the shipped thirteen-class union; adds none. Its corpus *counts* are wrong (C11) |
| S9 | Gate quantity — what `req(t)` counts | ⛔ **CONFLICT** | `tree-plan`, `tree-state` and `squad-harness` gate on aptitude points; `tree-resolve` gates on skill points spent in the tree, and contradicts itself two paragraphs apart |
| S10 | Shared tunable keys | ⛔ **CONFLICT** | `fmax` vs `fmaxMilli`, `w` vs `wMilli`, `ladder.kPoints` vs `tierLadder.reqScalePoints`, `soulThetaWeight` vs `soulTrack.thetaPerSoulLevelMilli`, across three tuning files |
| S11 | The stage-3 generator | ⛔ **CONFLICT** | `tools/PassiveTreeGen` (catalog, review, species) vs `tools/TreeBinder` (binder) for the same C# program |
| S12 | The node's effect field | ⛔ **CONFLICT** | `NodeRecord.affixId` is one string; the plan and the language stage both emit `affixIds[]`, 1..3 |
| S13 | Validation-gate list | ⛔ **CONFLICT** | `tree-language` owns a 24-gate list; `tree-review` and `species-tree` both cite "the 29 gates" with numbers from research doc 03 that no longer resolve |
| S14 | Tree category vocabulary | ⛔ **CONFLICT** | 4 values (`aptitude\|element\|status\|demonFamily`) vs 5 (`Primary, Elemental, Status, Family, Species`), with different tokens for the same category |
| S15 | `PowerLadderKMicro` — who owns it, and how bad is it | ⛔ **CONFLICT** | Three specs name three different owners; two carry the superseded 17% figure; and the real worst case is worse than either (C9) |
| S16 | Reflect in Battle | ⛔ **CONFLICT** | `tree-binder` prices its reflect example as Battle-executable; `squad-harness` says it is lawn-only. Code says `squad-harness` is right |
| S17 | The potency ceiling's denominator | ⛔ **CONFLICT** | The plan emits per-node shares in ‰ of *one branch* and the ceiling in ‰ of *the whole tree*; the catalog's load check compares a `kMicro` against "the plan's `nodePotencyCeiling`", which is neither |
| S18 | Plan file path | ⛔ **CONFLICT** | `plan/<treeId>.v1.json` vs `plan/<treeId>.json` |

Counts and citations are broken out in §4, §5 and §6.

---

## 2. C1 — the gate quantity: aptitude points or skill points? (S9)

**This is the most expensive disagreement in the set**, because `req(t)` is the one number four specs
share and every depth table in the program is computed from it.

`tree-plan` §7, under the heading *"The point supply and the gate quantity — read, never assumed"*:

> **Two different quantities, and conflating them is the easy mistake.** `req(t)` gates on **aptitude
> points allocated to that tree's gate quantity**; nodes are bought with **skill points**. This module
> touches only the first.

Its §2 depth table is computed from that: *"`aptitudePoints(Θ) = 3·Θ` at commander scope"*, giving
`T = 10` at `Θ ≈ 92`.

`tree-resolve` §3.3, under *"One index, and the other quantities convert into it"*:

> **Rule: `tree-resolve` gates on ONE index — skill points spent in the tree.**

And `tree-resolve` §3.2, two paragraphs earlier:

> So the gate quantity is *points spent in this tree*, whatever their provenance.

**`tree-resolve` also contradicts itself.** Its §3.2 argues D12 (*"tier gates read base allocation,
never item bonuses"*) is true **by construction**, and the construction it names is that
*"an aptitude is a SOURCE, never a registered channel"* (`Aptitude.cs:12-14`). That argument only
holds if the gate reads aptitudes. If the gate reads skill points, D12 needs enforcement code,
because D11 explicitly lets items grant points.

`squad-harness` §4 models the gate the plan's way, in code shape:

```text
p_i  = share_i · (Θ · aptitudePointsPerTheta)              points in tree i
T_i  = min(10, max{ t : req(t) ≤ p_i }),  req(t) = 5·t(t+1)/2
```

`tree-state` §2.2 sides with the plan too, and it is the one place the two currencies are reconciled
on purpose: it derives the skill-point rate as `g = 3·s·step·k²/5 = 10.40` — the `3` is the aptitude
rate and the `5` is `req`'s `k` — *"rounded up so the wallet clears the gate with a small surplus."*
That derivation is meaningless if the gate spends the same currency as the wallet.

`tree-surface` §7.2 renders the gate as an aptitude-path lend — *"55 from Fortitude · 120 lent by
Might"* — which is cross-unlock over postures, i.e. aptitude points.

**Recommendation.** Adopt `tree-plan`'s reading: **`req(t)` gates on aptitude points; skill points buy
nodes.** Four specs already assume it and one of them (`tree-state` §2.2) derives a shipped constant
from it. Amend `tree-resolve` §3.3's rule sentence, and re-check its §3.2 D12 argument — under the
corrected reading that argument becomes correct rather than accidental.

**Second, smaller finding on the same seam: nobody owns the conversion.** `tree-plan` §7 says the
four-incommensurable-quantities problem *"is **`tree-resolve`'s** to close, not this module's."*
`tree-resolve` §3.3 says *"`tree-state` owns the conversion; this module never sees a specimen level
or a mastery count."* `tree-state` §3 lists it under *"Blocked-on, tracked rather than open"* and says
it *"needs whichever scopes actually ship trees."* The item is passed through three specs and lands in
none. It needs an owner named in the map.

---

## 3. C2 — `tree-binder`'s coefficient formula ignores the archetype's width vector (S3)

This is the defect the `weightTotal` question was pointing at, and it is larger than the units
question that surfaced it.

`tree-plan` §3 distributes budget **per tier**, and the width vector enters one level below:

> ```text
> tierBudget[t] = B_b · t / T_tri ,   T_tri = tierCount·(tierCount+1)/2 ,   B_b = budgetTotal / 2
> ```
> …
> ```text
> nodeBudget[t] = tierBudget[t] / w[t] = B_b · t / (T_tri · w[t])
> ```
> The width vector `w[t]` enters one level down and never enters the sum at all.

`tree-binder` §3.3 distributes budget **per node**, with the tier weight applied to each node:

> ```text
>   tierWeight(t)         D26's binding pairing rule fixes this LINEAR, so tierWeight(t) = t
>   weightTotal           Σ tierWeight over every node in the tree
>
>   num    = treeShareMilli · treeBudgetMilli · tierWeight(t) · channelAnchorMilli
>   denom  = 1000 · weightTotal
> ```

**These are the same function only when `w[t]` is constant.** Under the binder, tier `t` receives
`w[t]·t / Σ(w·t)` of the tree; under the plan it receives `t / T_tri` of a branch regardless of how
many nodes are in it. Worked against the three archetypes `tree-plan` §3 actually ships:

| archetype | `w[]` | `Σ_tree w[t]·t` (= binder's `weightTotal`) | plan's tier-10 node, ‰ of `budgetTotal` | binder's tier-10 node, ‰ of `budgetTotal` |
|---|---|---:|---:|---:|
| `broad-and-flat` | `[2×10]` | **220** | 91 | 91 |
| `gated-deep` | `[3,3,3,2,2,2,2,1,1,1]` | **178** | **91** | **56** |
| `late-crown` | `[1,1,2,2,2,2,2,2,3,3]` | **252** | 30 | 40 |

`gated-deep`'s capstone is the node the whole potency-ceiling derivation is calibrated on —
`tree-plan` §5: *"`gated-deep`'s crown lands on 182‰ of a branch — 91‰ of `budgetTotal` — so the
admitted archetype set touches the ceiling exactly."* Under the binder's formula it lands at 56‰, and
`tree-plan`'s `archetype_shapes_actually_differ` test (*"the strongest node differs by ≥ 2×"*) would
be measuring a shape the shipped catalog does not have.

**The answer to the `weightTotal` question, stated by arithmetic.**

1. **220 is right, and it is right for one archetype only.** It is the per-**tree** weight sum
   `2 × Σ_t w[t]·t` at `w ≡ 2`. `tree-plan`'s per-mille column is per-**branch** (Σ = 1000‰ of `B_b`),
   whose weight sum is **110**. So `220 = 2 × 110` and the two conventions reconcile exactly — for
   `broad-and-flat`.
2. **It is not a constant.** The same expression gives **178** for `gated-deep` and **252** for
   `late-crown`. `tree-catalog` is right to carry it as a per-tree field
   (`TreeRecord.weightTotal: long`, *"Σ tierWeight over every node in the tree — the binder's
   denominator"*). `tree-binder` §3.6 is wrong to list it as **structural**:

   > | `weightTotal`, tiers = 10, branches = 2 | **structural** | the tree's own shape (D29) |

3. **The two conventions do reconcile numerically where they overlap**, which is why this went
   unnoticed. `tree-plan`'s `broad-and-flat` tier-1 node is 9‰ of `B_b` = 4.545‰ of `budgetTotal`;
   × the atk anchor 0.135 = **614 per-million** — exactly `tree-binder` §3.5's own tier-1 `kMicro`.
   The formulas are the same function at `w ≡ 2` and diverge everywhere else.

**Recommendation.** Delete `tierWeight(t)` and `weightTotal` from the binder's formula and read the
plan's own emitted per-node share, which the binder already lists as an input and then never uses
(*"IN from tree-plan … per-node `budgetShareMilli`"*). `budgetShareMilli` is ‰ of one branch, so:

```text
kMicro = round( treeShareMilli · treeBudgetMilli · budgetShareMilli · channelAnchorMilli / (2 · 1e6) )
```

Checked against the binder's own worked example — tier 5, `broad-and-flat`, exact share 45.4545‰ of a
branch, anchor 135 — this gives **3,068**, byte-identical to §3.4. It is correct for every archetype
by construction, it removes a second copy of the plan's distribution arithmetic from a second module,
and it makes `weightTotal` unnecessary rather than merely per-tree.

---

## 4. C3 — the node id: two incompatible strings (S4b)

`tree-plan`'s schema, per-tree table:

> | `nodes[].nodeId` | string | FROZEN | `<treeId>/<off\|def>/t<tier>/<index>` — G8 |

with `treeId` itself defined as `tree.<category>.<subject>` and the module's Reproducibility section
adding *"node ids derived only from position."*

`tree-catalog` §3, having explicitly ruled out that shape:

> | **Positional ordinal** (`fire-tree-node-07`) | ⛔ | **Insertion renumbers everything after it.**
> The repo already refuses this shape for exactly this reason |

> ```text
> skill.<treeId>-<branch>-t<tier>-<nodeKey>
>   nodeKey   allocated ONCE by the plan within (tree, branch, tier), never reclaimed,
>             never derived from the node's effect or its display order
> ```

> **Hard constraint, verified:** `container_id` allows **no dot in its body** — the grammar is
> `^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`

**Verified in code:** that grammar is at `docs/architecture/item/seed-contract.md:132` and it is exactly
as quoted. Three consequences, all mechanical:

1. **The plan's separator is illegal.** `/` is not in `[a-z0-9-]`.
2. **The plan's `treeId` is illegal inside the body.** `tree.aptitude.might` carries two dots, so
   `skill.tree.aptitude.might-off-t1-1` fails the grammar even after the separators are fixed. Either
   `treeId` loses its dots for id purposes, or the composition needs a separate slug.
3. **`index` is not `nodeKey`.** `tree-plan` derives the id from position and says so; `tree-catalog`
   forbids exactly that and requires an allocated-once key. `tree-review` §8's entire `O(diff)`
   argument rests on the catalog's reading:

   > With stable ids, re-review is `O(diff)`. With content-hash ids, every rebalance is a full
   > 35,160-node re-review: 293 hours, i.e. never.

**Recommendation.** `tree-catalog` owns the on-disk record (map §"Boundaries between modules"), so its
format wins. `tree-plan` allocates `nodeKey` — that is the catalog's own requirement,
*"allocated ONCE by the plan"* — and emits the composed `skill.…` string, with a `treeId` slug that
carries no dot. `tree-plan`'s `G8` and its `no_node_has_a_parent_at_tier_one` sibling tests move with
it. This is cheap now and a migration after the first corpus is authored.

---

## 5. C4 — `mechanismFloor` does not exist, and a floor is not a ramp (S1)

`tree-language` §4.2, step 4 of its quota algorithm:

> ```text
>        cell.nodeClass := "mechanism" if tier >= t.mechanismFloor
> ```

and its gate 16:

> | 16 | **`PassiveTree/MechanismFloor`** | `nodeClass` at tiers ≥ the archetype's floor | any deep-tier `magnitude` node |

`tree-plan` §4 emits no such field. It emits a per-tier **count**:

> ```text
> mechShareMilli[t] = mechFloorMilli + (mechCapMilli − mechFloorMilli)·(t−1)/(tierCount−1)
> mechNodes[t]      = round_half_up( w[t] · mechShareMilli[t] / 1000 )
> ```

> | archetype | `mechNodes[t]`, t=1..10 | total |
> | `broad-and-flat` | 0, 0, 0, 1, 1, 1, 1, 2, 2, 2 | 10 / 20 |

**These are not the same rule.** A floor says *every* node at or above tier `f` is mechanism. The
ramp puts **one of two** mechanism nodes at tiers 4–7 of `broad-and-flat` and **one of three** at
tier 3 of `gated-deep`. There is no tier `f` for which "tier ≥ f ⇒ mechanism" reproduces the emitted
`mechNodes[]`. Pick `f = 8` and gate 16 passes while five mechanism nodes per branch sit below it
unexplained; pick `f = 3` and the gate fails against a plan that is correct by its own construction.

Note also that `tree-plan` names `mechanism.floorMilli` as a tunable — *‰ of a tier's nodes, at tier
1*, value **0**. A consumer reading a key called `mechanismFloor` and finding `0` would conclude every
tier is a mechanism tier. The name collides across two different meanings.

**Recommendation.** `tree-language` reads `archetypes[].mechNodes[]` and forces exactly that many
slots per tier — which is what its own step 4 comment ("HARD CONSTRAINTS OVERRIDE THE DRAW") is
already shaped for. Gate 16 becomes an exact per-tier count check, matching `tree-plan`'s `C3`
(*"`mechNodes[t]` matches the ramp at every tier"*), and the "deep tier" success criterion becomes
`mechNodes[T] == w[T]`, which is `tree-plan`'s R-M1.

---

## 6. C5 — the other three fields `tree-language` declared as an interface (S1)

`tree-language`'s Open questions section is explicit that it was writing against an unspecced
producer:

> **Interface not yet frozen:** `tree-plan` is wave 0 and unspecced. Every plan field this module reads
> (`quotaCell`, `requiredProperties`, `propertyVocabulary`, `mechanismFloor`, `budgetShareMilli`) is the
> interface this module *requires*; the names must be reconciled when `spec-tree-plan.md` lands.

Reconciled, field by field, against the schema `tree-plan` has since frozen:

| Field `tree-language` requires | In `tree-plan`'s schema | Verdict |
|---|---|---|
| `quotaCell` | `nodes[].quotaCell` — `{axis: id}`, same six axes | ✅ matches |
| `requiredProperties` | `nodes[].requiredProperties[]` — `string[]`, e.g. `"posture:Force"` | ✅ matches |
| `propertyVocabulary` | `propertyVocabulary.<axis>[]` on the manifest, thirteen axes | ✅ matches |
| `budgetShareMilli` | `nodes[].budgetShareMilli` — int, ‰ of **one branch** | ✅ name and type match; the denominator is a trap, see C14 |
| `mechanismFloor` | **absent** — the plan emits `archetypes[].mechNodes[]` | ⛔ C4 |
| `nodeClass` (used throughout §2 and §4.2) | `nodes[].class` — enum(2) | ⛔ **name mismatch** |
| `shapeArchetype` (declared a node field) | `archetype`, on the **tree**, not the node | ⛔ name and level mismatch |
| `tierRequirement` (declared a node field) | not emitted per node; `ladder.req[]` on the manifest | ⛔ level mismatch |
| `affixIds[]` (1..3) | `nodes[].affixIds[]` | ✅ matches the plan; ⛔ conflicts with `tree-catalog`, see C6 |

So **four of nine** need a rename or a re-level. `nodeClass` vs `class` is the one that will bite
first, because both specs assert it in a test — `tree-plan`'s
`Write a hand-set 'class' flag` boundary and `tree-language`'s `mechanism_floor_holds_at_deep_tiers`
both name the field they expect.

**Recommendation.** `class` is a reserved word in several of the languages this data passes through
and `nodeClass` is what the catalog also uses (`NodeRecord.nodeClass`, `enum { Magnitude, Mechanism }`).
**Rename the plan's field to `nodeClass`.** Move `shapeArchetype` and `tierRequirement` off the node in
`tree-language`'s table — they are tree-level and manifest-level respectively, and duplicating them per
node is 40 copies of one fact.

---

## 7. C6 — one affix per node, or up to three? (S12)

`tree-catalog` §2.2:

> | `affixId` | `string` | **a node is an affix, not a bare atom** — a named bundle drawn together |

`tree-language` §6.3's response schema:

> ```jsonc
> "affixIds": { "type": "array", "minItems": 1, "maxItems": 3, … }
> ```

and its brief: *"1. `affixIds` — {1..3} from the list below. They are this node's whole effect."*
`tree-plan`'s schema agrees with the language stage: `nodes[].affixIds[]`, HOLE, *"must satisfy `class`
and price ≤ `budgetPoints`."*

`tree-binder` sits on both sides: §1 says *"A node is an affix inside a `skill` container"* (singular),
while its `IN` block reads *"from tree-language `affixIds`"* (plural) and its worked examples M2 and M3
are single affixes carrying two and three atoms.

**This matters for pricing, not just for shape.** `ContentValidation.Budget` compares a summed
`PowerVector.Total` against a ceiling (`ContentValidation.cs:62`, tolerance const at `:44` — both
verified). Summing three affixes against one node budget is a different check from pricing one.

**Recommendation.** Allow the array — the language stage's `maxItems: 3` is the one that was reasoned
about, and `definitions.md` §4a makes the affix the *roll* unit, not the *node* unit. Change
`NodeRecord.affixId: string` to `affixIds: string[]` in `tree-catalog` §2.2. If the singular is
preferred instead, `tree-language`'s schema and vote design change, which is the more expensive edit.

---

## 8. C7 — shared tunables: three files, four renamed keys, two unit systems (S10)

Every key below is named by at least two specs. **`data/tuning/passive-tree.v1.json` does not exist
today** — verified, `data/tuning/` carries no `passive-tree*` file — so nothing is locked in yet and
all of this is cheap now.

| Concept | `tree-plan` | `tree-resolve` | `tree-state` | `tree-binder` | `squad-harness` | `tree-catalog` |
|---|---|---|---|---|---|---|
| `req`'s scalar `k` | `ladder.kPoints` **= 5**, in `passive-tree-gen.v1.json` | `tierLadder.reqScalePoints` **= 5**, in `passive-tree.v1.json` | — | — | `tierLadder.k` | `tierLadder.k`, in `passive-tree.v1.json` |
| Focus ceiling | `concentration.fmax` | **`concentration.fmaxMilli`**, per-mille, default **1200** | — | — | `concentration.fmax`, *"multiplier (dimensionless, ≥ 1)"*, 1.15–1.25 | `Fmax` |
| Focus blend | `concentration.w` | **`concentration.wMilli`**, per-mille, default **500** | — | — | `concentration.w`, *"weight, 0..1"*, 0.5 | `w` |
| Soul → `Θ` weight | `soulThetaWeight` | **`soulTrack.thetaPerSoulLevelMilli`**, per-mille | `soulThetaWeight` (`Ws`) | `soulThetaWeight` (`Ws`) | `soulThetaWeight` (`Ws`) | `Ws` |
| Unlock cost | `unlockCost.first`/`.step` | — | `unlockCost.first` = 5, `.step` = 2 | — | `unlockCost.first/step` | `unlockCost.*` |

Three separate defects here:

1. **`k` lives in two files under two names.** `tree-plan` puts it in `passive-tree-gen.v1.json`
   because the planner reads it; `tree-resolve` and `tree-catalog` put it in `passive-tree.v1.json`
   because the runtime gate reads it. Both are right about their own reader and the number must be one
   number — `tree-plan` itself names the hazard, in its own words: *"duplicating them here would create
   the copied number this repo already calls 'a future drift bug with a delay fuse'."*
2. **`Fmax` and `w` are per-mille in one spec and plain multipliers in two others.** This is the exact
   class the brief flagged. `tree-resolve` defines `fmaxMilli = 1200` and its own `Magnitude()` helper
   validates `fMilli >= 1000`; `squad-harness` proposes values on the `1.15–1.25` scale and
   `tree-plan` lists the bare names. A harness that writes `1.2` into a key a resolver reads as
   per-mille produces `F = 1.0012`, which is indistinguishable from "the feature is off" and would
   pass every test either spec writes.
3. **`Ws` has a per-mille name in exactly one spec.** Four specs call it `soulThetaWeight`;
   `tree-resolve` calls it `soulTrack.thetaPerSoulLevelMilli`. `tree-state` §8 already warns about
   precisely this suffix, and its warning is worth repeating because it is the reason to pick the
   plain name:

   > **No `Milli` suffix on `unlockCost.*`, and the absence is deliberate.** The shipped
   > `SkillPointsPerThetaMilli` neither multiplies nor divides by 1000 — the suffix is a naming
   > artifact.

**Recommendation.** One runtime file, `data/tuning/passive-tree.v1.json`, holding `tierLadder.k`,
`concentration.fmaxMilli`, `concentration.wMilli`, `soulTrack.thetaPerSoulLevelMilli` and
`unlockCost.{first,step}` — the per-mille spellings, because `tree-resolve` is the module that
actually does the arithmetic and T6 asks the unit to be in the name. `tree-plan`'s generator file
**reads** `tierLadder.k` from it rather than declaring its own `ladder.kPoints`. `squad-harness`
reports its proposals in per-mille so the value it names is the value someone pastes.

---

## 9. C8, C15, C18 — three smaller interface mismatches

**C8 — the stage-3 generator has two names.** `tree-catalog` §5 and §Commands, `tree-review`
§Commands and `species-tree` §Commands all run
`dotnet run --project tools/PassiveTreeGen -- --check`. `tree-binder` §Commands runs
`dotnet run --project tools/TreeBinder -- --check` and lists `tools/TreeBinder/Program.cs` in its
structure. They describe the same program: deterministic C#, reads the seed, writes
`data/generated/passive-tree/`, `--check` byte-identity, `--explain <nodeId>`. `tree-catalog` even
says *"`tree-catalog` owns the on-disk record shape; this module writes it"* — which `tree-binder`
echoes. Three specs to one: **`tools/PassiveTreeGen`**.

**C15 — `PowerLadderKMicro` has three claimed owners and one stale severity figure.**

| Spec | Who owns the change | Stated tier-1 error |
|---|---|---|
| `tree-binder` §3.5 | itself, as an **Ask first** — *"a change to `src/FusionRpg.Core`"* | **+63%**, re-derived at D29's ten tiers |
| `tree-catalog` §2.3 | *"a wiring gap in the atom layer, **not this module's to land***" | ~17% |
| `tree-resolve` §7.3 | *"**`mechanism-wiring`'s** `PowerLadderKMicro` sibling (three lines)"* | 17% |
| `species-tree` §8 | *"Owned by **`tree-catalog`**"* | ~17% |

`mechanism-wiring` does not mention `PowerLadderKMicro` anywhere, and `ValueSpec.cs` is absent from its
modified-files table — so `tree-resolve` assigns work to a module that has not accepted it and whose
own §3 says *"**Nothing else.** No `data/`, no web, no `FusionRpg.Data`, no new tunable."*

`tree-binder` is right about the number and says why: *"D29's ten tiers spread the same budget over 220
weight units instead of 112, so every coefficient roughly halves and the rounding error roughly
doubles."* The 17% figure is research doc 04's, computed at seven tiers, and `tree-binder` §3.5
explicitly supersedes it. **Two specs and one dependency table still carry the superseded number.**

**And the real worst case is worse than +63%.** Applying the plan's own emitted shares rather than the
uniform archetype:

| archetype, tier 1 | plan share of `budgetTotal` | exact `kMicro` (atk anchor) | stored `kMilli` | error |
|---|---:|---:|---:|---|
| `broad-and-flat` | 4.545‰ | 614 | 1 | **+63%** |
| `late-crown` | 9‰ | 1,215 | 1 | −17.7% |
| **`gated-deep`** | **3‰** | **405** | **0** | **−100% — the node does nothing** |

A `gated-deep` tree has three tier-1 nodes per branch. At per-mille resolution all six store `kMilli =
0` and contribute exactly nothing, with no error and no test failing. That is a stronger argument for
the per-million sibling than the flatness argument, and it should be in the spec that asks for it.

**Recommendation.** `tree-binder` owns the `ValueSpec`/`AtomCompiler` change — it is the module that
writes the number and the only one that re-derived the arithmetic. `tree-catalog`, `tree-resolve` and
`species-tree` cite it rather than restating the figure.

**C18 — the plan file path.** `tree-plan` emits `data/seed/passive-tree/plan/<treeId>.v1.json`;
`tree-language` §4.2 step 7 and §6.4 both read and write `data/seed/passive-tree/plan/<treeId>.json`.
One character, and `plan_read.py` refuses an unfilled hole by opening a file that will not be there.

---

## 10. C9 — reflect is not executable in Battle (S16)

`tree-binder` §6, worked example M2, the spec's own demonstration of why the affix is the unit:

> **Executable on lawn and battle; ⛔ inert in sim.** The reader is live —
> `CombatDamageDispatcher.TryReflect` (`:98-124`) reads rate and resist, clamps linearly, rolls, then
> reads damage and dispatches a real reversed `DamagePacket` through the Funnel.

`squad-harness` §10, correcting the ideal:

> **M7 Retaliation / reflect** — … Reflect lives in `CombatDamageDispatcher.TryReflect`
> (`CombatDamageDispatcher.cs:85`), reached only from `DispatchInstant`; Battle applies HP through
> `DamageApplyPipeline.Apply` instead, and `reflect` has **zero hits** in
> `src/FusionRpg.Core/Battle/`. **Reflect is not measurable at squad scope today.**

**Verified this session, and `squad-harness` is right.** `TryReflect` is at
`src/FusionRpg.Core/Combat/CombatDamageDispatcher.cs:85` and its only caller is `DispatchInstant`
(`:71`). Grepping every `DispatchInstant` call site in `src/` and `tools/` returns `EffectBag`
(`:534`, `:603`), `StatusEffectBridge` (`:86`, `:129`), the injector's `CheatCommandRunner` and
`EffectRuntime`, and `tools/CombatSim`. **None is in `src/FusionRpg.Core/Battle/`.** Battle applies HP
at `BattleRunState.cs:497` through `DamageApplyPipeline.Apply`. A case-insensitive grep for `reflect`
across `src/FusionRpg.Core/Battle/` returns one hit, in a doc comment about action re-selection.

`tree-resolve` §2.3 and `mechanism-wiring` §1 both cite `EffectRuntime.cs:491` for retaliation without
naming a runtime. That file is `src/FusionRpg.Injector/Effects/EffectRuntime.cs` — the lawn. Neither
spec claims Battle explicitly, so neither is wrong; but `mechanism-wiring`'s table row reads
*"Already live … Content, not code"* with no runtime qualifier, next to rows that are about Battle.

**Why it matters.** `tree-binder` uses M2 to price a deep-tier mechanism node, and `mechanism-wiring`
§8 argues that mechanism nodes become scoreable once G1 and G3 land. If reflect is lawn-only, a
reflect node is invisible to `squad-harness` **and** to `tree-review`'s behavioural sample — which is
exactly the residual risk `tree-plan` §4 owns:

> a mechanism node that is structurally legal, novel in its cell, and outside the review sample can
> still be worth nothing.

**Recommendation.** Amend `tree-binder` M2 to *"Executable on lawn; ⛔ inert in Battle and Sim"*, and
add a `reflect` row to `mechanism-wiring`'s §1 table naming the missing Battle path. It is a wiring
gap — `DamageApplyPipeline` is where a Battle reflect hook would go — not a wall, and saying which is
the whole point of that spec's framing.

---

## 11. C11 — the corpus counts (S6, S8)

Five specs count the same corpus and three answers are in circulation. **The verified numbers, counted
in `data/` this session:** `data/seed/demons/species/_index.json` holds **840** keys; there are **502**
non-`_` species files plus `_index.json` and `_needs-review.json`.

| Quantity | Verified | `tree-review` | `species-tree` | map | `tree-catalog` | `tree-state` | `tree-surface` |
|---|---:|---:|---:|---:|---:|---:|---:|
| Species | **840** | 840 ✅ | 840 ✅ | 840 ✅ | — | — | **841** ⛔ |
| Nodes per species tree | **40** (D30 as amended) | 40 ✅ | 40 ✅ | 40 ✅ | 40 ✅ | **29** (implied) ⛔ | **29** (implied) ⛔ |
| Species nodes | **33,600** | 33,600 ✅ | 33,600 ✅ | 33,600 ✅ | — | *"~24,000"* ⛔ | — |
| Trees, whole corpus | **879** | 879 ✅ | 879 ✅ | — | — | — | **880** ⛔ |
| Nodes, whole corpus | **35,160** | 35,160 ✅ | 35,160 ✅ | 35,160 ✅ | *"~35,200"* ⚠ | *"~25,900"* ⛔ | *"~25,900"* ⛔ |
| Shared trees / nodes | **39 / 1,560** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

`tree-review` §1.1 states the correction that the other two missed:

> **FACT, counted 2026-09-05.** `data/seed/demons/species/` holds **840** anchor entries across **502**
> non-`_` files … Species tree size is 40, not 29: D30 as amended defers to D10's one-shape rule and
> D29's 10 tiers × 2 branches.

`tree-state` §2.3 and §7 both carry the old figure — *"D30's ~24,000 species nodes"*, *"~25,900 nodes
(D29's 1,560 + D30's ~24,000)"* — and it feeds a real calculation: §7's marginal price
`5 + 2·25,899 = 51,803` and cumulative `670,913,600`. At the corrected 35,160 the marginal is
**70,325** and the cumulative **1,236,700,000** — still inside `long`, so §7's conclusion survives, but
the numbers in the spec are wrong by ~35% and they are the ones someone will paste into a test.

`tree-surface` §1's headline table is built on 25,900 and 841, and its §2.3 volume row reads
*"All paths + species in one browse | 880"*. The 880/879 difference is immaterial to the GG-50 argument
(both are far above the 240 search-first threshold, verified at
`web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx:21-22`), but the counts are quoted as facts
in a spec that will be read as one.

**Recommendation.** One line each: **840 species · 879 trees · 40 nodes each · 33,600 species nodes ·
35,160 total.** `tree-review` §1.1 is the source; the other three cite it rather than restating it.
`tree-catalog`'s *"~35,200"* is close enough to be harmless and should still move, because a review
pipeline that cannot count its own population is exactly what `tree-review` §1.1 was written about.

---

## 12. C12 — the gate list: 24 or 29? (S13)

`tree-language` §7 owns the gate list for this program and numbers **24** gates, 1 through 24.
`tree-review` §4.1:

> `03-llm-stage-contract.md` §7 lists 29 gates.

and it then cites individual numbers — *"gate 14"* for the mechanism floor, *"gate 19"* for
near-duplication, *"gate 22"* for anti-motifs, and gates 1/6/7/8/9/11/12/13/17/18/20/21/23/27/29 in its
"closed by machine" table. `species-tree` §1 inherits *"the **29** validation gates"* verbatim.

**Verified:** research doc `03-llm-stage-contract.md` §7 does list 29 rows. But `tree-language`
renumbered them — it merged some, dropped others, and added `CellOccupancy` and `NameCollision` — so in
the spec that now owns the list, the mechanism floor is **gate 16**, near-duplication is **gate 20**,
and there is no gate 22 for anti-motifs (that check lives inside `run_g2`, gate 9).

So every per-gate number in `tree-review` §4.1 and every reference to *"the 29 gates"* points at a
research note rather than at the module spec, and the two numberings disagree on almost every index.
`tree-review`'s §6.4 unshippable list is fine — it names metrics, not numbers.

**Recommendation.** `tree-language` §7 is the list. `tree-review` and `species-tree` cite gates **by
metric id** (`PassiveTree/MechanismFloor`, `PassiveTree/NearDuplicate`) rather than by ordinal, which
survives the next time the list changes. Drop "29" from both.

---

## 13. C13, C14 — two vocabulary mismatches

**C13 — the tree category.** `tree-plan`'s per-tree schema: `category | enum(4) | aptitude | element |
status | demonFamily`. `tree-catalog`'s `TreeRecord`: `category | enum { Primary, Elemental, Status,
Family, Species }` — five values, and three of them are different tokens for the plan's four.
`tree-review` §3.2's stratum axis agrees with the catalog: *"Tree category | 5 (primary / elemental /
status / family / species)"*. The plan's set is defensible — it emits no species trees, `species-tree`
does — but `aptitude` vs `primary` and `demonFamily` vs `family` are a straight rename that will
surface as a failed enum check at import.

**Recommendation.** The catalog's five, with the plan emitting only the first four.

**C14 — the potency ceiling's denominator, and a check that cannot run.** `tree-plan` emits per-node
shares and the ceiling against **two different denominators**, and says so:

> | `nodes[].budgetShareMilli` | int | FROZEN | ‰ of one branch budget |
> | `potency.maxNodeShareMilli` | int | FROZEN — 91, recomputed at check, ‰ of `budgetTotal` |

So `gated-deep`'s capstone reads `budgetShareMilli = 182` against a ceiling of `91`, and a consumer
comparing the two directly is wrong by exactly 2×. `tree-plan` itself is careful (R-P1 says *"of
`budgetTotal`"*), but `tree-catalog`'s load-path table is not:

> | `kMicro <= 0`, or above the plan's `nodePotencyCeiling` | reject |

Two problems. The key is called `potency.maxNodeShareMilli`, not `nodePotencyCeiling`. And `kMicro` is
a post-anchor per-million share of `P(Θ)` — the atk anchor alone is a factor of 0.135 — so it is not
comparable to a per-mille budget share in any denominator. As written the check cannot be implemented.

**Recommendation.** Emit `budgetShareMilli` in ‰ of `budgetTotal`, the same denominator as the ceiling,
so R-P1 is a direct comparison and a consumer cannot pick the wrong one. Move `tree-catalog`'s ceiling
check off `kMicro` and onto the plan's own share, before the anchor is applied — which is where the
binder has both numbers anyway.

---

## 14. Counts and vocabularies — the register

Every count any two specs both state, checked by counting in code or data this session.

| Vocabulary | Verified | Where | Specs agreeing |
|---|---:|---|---|
| Atom attach points | **7** | `AtomKindRegistry.cs:21` | plan, language, binder, catalog, mechanism-wiring — all ✅ |
| Atom kinds | **16** | `AtomKindRegistry.cs:31` | all five ✅ |
| Atom triggers | **13** declared, **11** authorable | `AtomKindRegistry.cs:36`; `AtomKind.cs:87-88` (`OnGranted`/`OnRemoved`) | all five ✅ |
| Elements | **6** + `omni` | roster | plan, language, binder ✅ |
| Statuses | **21** | `StatusCatalogBootstrap.cs:16-58` — 8 UnityCc + 8 overlay + 5 contagion, counted | plan, language, binder ✅ |
| Aptitudes / postures | **12 / 3** | `Aptitude.cs` | plan, language, binder ✅ |
| Registered derived channels | **267** + 9 open prefix families | `AtomCatalogSsotDriftTests.cs:46`, `ElementHubDocDriftTests.cs:73`, `SeedCatalogTests.cs:28` | language, binder, mechanism-wiring ✅ |
| Derived-stat **families** | **53** | `data/seed/derived-stats/catalog.json`, `entries` | plan (the only spec that uses families) ✅ |
| Primary stat channels | **23** | `ModifierOp.cs` | language, binder, mechanism-wiring ✅ |
| `UnitClass` (C#) | **13** | `src/FusionRpg.Core/Stats/Derived/StatClass.cs:29-98`, members counted | language, binder, catalog ✅ |
| `UnitClass` (TS union) | **13** | `web/fusion-rpg-web/src/contract/types.ts:33-55`, counted | surface ✅ |
| `StatClass` | **4** | same file, `:7-22` | language, binder ✅ |
| Affix families | **98** across 15 files | `data/seed/items/affix-families/` | language, binder, species-tree ✅ |
| Authored affixes | **2** | `data/seed/effects/affixes/all.json`, `entries` | species-tree ✅ |
| Semantic atom tag values | **3** | quoted by four specs | plan, language, catalog, species-tree ✅ |
| Shared trees / nodes | **39 / 1,560** | derived from 12+6+21 | all seven that state it ✅ |
| Species | **840** | `_index.json`, counted | ⛔ C11 |
| Nodes per species tree | **40** | D30 as amended | ⛔ C11 |
| Whole corpus | **879 trees / 35,160 nodes** | | ⛔ C11 |
| Validation gates | 29 in research 03, **24** in the owning spec | | ⛔ C12 |
| Tree categories | 4 vs 5 | | ⛔ C13 |

**The atom vocabulary is the good news story here.** Five specs independently counted 7/16/13 and
every one is right; the code confirms all three constants. `DESIGN-GATE.md:40` has since been corrected
to the same numbers.

**One near-miss worth naming so it is not read as a conflict.** `tree-plan` §6 lists
`channelFamily | 53` while `tree-language` §3 and `tree-binder` §2 list `267 registered + 9 open prefix
families`. These are different things — 53 *families* in the seed catalog against 267 *registered
channels* in the registry — and both counts are correct. No action; recorded because a future sweep
will otherwise flag it.

---

## 15. Stale `file:line` citations

`mechanism-wiring` §"The four gaps" warned this would happen, and was right:

> ⚠️ **`BattleRunState.cs` is under concurrent edit by another program.** … **Cite this file by symbol,
> not by line.**

Spot-checking the most load-bearing citations across the set found that the drift is wider than one
file. Verified this session:

| Cited as | Actually at | In which spec(s) | Severity |
|---|---|---|---|
| `RpgStore.Aptitudes.cs:132` — *"`LoadAllocation` calls `Single` **per row**"* | **`:149`** | `tree-catalog` R5, `tree-state` §4 | **Material.** It is the evidence for R5, the rule both specs build the migration boundary on |
| `BattleModels.cs:100` / `:101` — `BattleChannelMod(string, long)` | **`:146`** | `tree-resolve` §2.1, `squad-harness` §7 | Material — it is the battle adapter's output type |
| `BattleModels.cs:97` — innate shield | **`:142`** | `squad-harness` §10 | Minor |
| `BattleModels.cs:105-106` — `BattleStatusSpec` | **`:152`** | `squad-harness` §10 | Minor |
| `BattleModels.cs:133-135` — `BattleRuleset.Tuning` throws | **`:179`** | `squad-harness` §Structure | Minor |
| `BattleModels.cs:172-175` — `BaseHp`/`BaseAtk`/`BaseDefense` | **`:218-221`** | `squad-harness` §11 | Material — it is the one-correction argument that removes doc 05's flagged blocker |
| `BattleModels.cs:203-204` — `BattleSetup`'s two lists | `BattleSetup` at **`:248`** | `squad-harness` §2 | Minor |
| `BattleModels.cs:213-215` — `ActiveCommanderAura` | **`:266`** | `squad-harness` §10 | Minor |
| `BattleModels.cs:226` — `BattleOutcome.Stalemate` | **`:272`** | `squad-harness` §8 | Minor |
| `BattleModels.cs:316-320` — `ContentHash`'s doc comment | **`:379`/`:397`** | `squad-harness` §Testing | Minor |
| `BattleRunState.cs:313` — the `RecomposeDerived` call site | **`:323`** | `squad-harness` §10 | **Material** — `mechanism-wiring` had already caught this exact drift and `squad-harness` did not pick it up |
| `BattleRunState.cs:465` / `:465-469` — `DamageApplyPipeline.Apply` | **`:497`** | `squad-harness` §10 (twice) | Minor |
| `BattleEngine.cs:238` — `maxBattleTick` | **`:250`** | `squad-harness` §8 | Minor |
| `AtomCompiler.cs:465` — the `checked((int)…)` narrowing | **`:464`** (`:465` is `continue;`) | `tree-resolve` §7.3 | Minor. `tree-binder` (`:463-464`), `tree-state` (`:464`) and `mechanism-wiring` (`:463-464`) are all correct |
| `StatClass.cs:26` — the *"ten-class"* doc comment | **`:25`** | `tree-language`, `tree-binder` | Cosmetic. The stale comment itself is real and both specs are right about it |
| `HybridViability/Program.cs:373` — *"the GATE reads the credit"* | **`:372`** | `tree-resolve` §4.1 | Cosmetic |
| `CombatDamageDispatcher.cs:99` — the reflect-rate clamp | **`:100`** (`:99` is `rateDelta`) | `tree-binder` §4.1 | Cosmetic |

**The pattern is one file and one program.** `BattleModels.cs` has drifted **~45–55 lines** and
`BattleRunState.cs` **~10–32**, consistent with `battle-tempo`'s ongoing edits. `squad-harness` carries
ten of the seventeen, because it is the spec that reads Battle most.

**What resolves cleanly, spot-checked:** `ActorHub.cs:34` (replace-by-`SubsystemId`) ✅,
`ActorHub.cs:145,148,155` (the three registrations) ✅, `ActorHub.cs:57` (the fold loop) ✅,
`ValueSpec.cs:92` ✅, `AtomKindRegistry.cs:21/:31/:36/:534/:535` ✅,
`AtomKindRegistry.cs:6`'s stale *"5 attach points, 12 kinds"* comment ✅ (the comment really is stale,
and three specs correctly say so), `AptitudeAllocation.cs:39` ✅, `BasicAttack.cs:184`
(`Trigger = AtomTriggers.OnDamageDealt`) ✅ — exact, in both specs that cite it —
`WebMatchService.cs:339` (`const int maxSquad = 6`) ✅, `ContentValidation.cs:44`
(`DriftTolerancePercent = 25`) ✅, `PowerVector.cs:62` (`Total`) ✅,
`item/seed-contract.md:132` (the `container_id` grammar) ✅, `BattleEngine.cs:172` (`Resolve`) ✅,
`Simulator.cs:66` (`new FoundationHarness`) ✅, `HybridViability/Program.cs:363-372` (the
largest-mate arm) ✅.

**Recommendation.** Adopt `mechanism-wiring`'s rule across the program: **cite `BattleModels.cs`,
`BattleRunState.cs` and `BattleEngine.cs` by symbol, not by line**, while `battle-tempo` is open.

---

## 16. Verified consistent

These seams were checked and the two ends meet. Recorded so a later sweep does not re-open them.

**S2 — `mechanism-wiring`'s fourth subsystem and `tree-resolve`'s fan-in both land, in either order.**
This was the suspected genuine conflict and it is not one.

`tree-resolve` §2.2:

> `ActorHub.Register` replaces by `SubsystemId` (`ActorHub.cs:34`), so registering a second
> `AtomDerivedSubsystem` would silently evict the first. … **Take the composing delegate.**

`mechanism-wiring` §4.1:

> **A new subsystem, not a widened `AtomDerivedSubsystem`.** That class *is* the `stat.derived` atom
> executor; a status's `StatMods` are not atoms. A separate `SubsystemId` also keeps attribution
> honest.

**Verified in code:** `ActorHub.Register` (`ActorHub.cs:31-38`) does
`_subsystems.RemoveAll(s => string.Equals(s.SubsystemId, subsystem.SubsystemId, …))` then `Add` then
sorts by `Order`. It replaces **by id**, so two subsystems with different ids never collide.
`mechanism-wiring`'s `StatusDerivedSubsystem` declares `SubsystemId => "status.derived"` at `Order 400`;
the shipped `AtomDerivedSubsystem` is `"…"` at 350 and is what `tree-resolve` fans tree atoms into via
the existing `boundDerivedAtoms` delegate (`ActorHub.cs:155`, and the delegate parameter at `:141`).
**Both land. Order between them is irrelevant** — `mechanism-wiring` §4.1 sub-decision 2 and
`tree-resolve` §2.2 give the same reason, that `FlatSum`/`SumIncreased` are commutative and
`FlatReplace`/`MaxPriorityFlag` order by `Priority`/`SourceId`.

Two cosmetic notes, neither a conflict. `tree-resolve`'s §2.2 heading reads *"Why a fan-in and not a
**fifth** subsystem"* while its own gate checklist correctly says *"ActorHub's **three**
registrations"* — a new one is the fourth. And `mechanism-wiring` §1 lists **layer parity** as *"itself
an `IActorStatSubsystem`"*, which would be a fifth; that is consistent, but the two specs should not
both use the word "fourth" for different classes.

**S4 — `tree-catalog` → `tree-state`.** Same id string, same semantics. `tree-state` §1.1 quotes the
catalog's slug verbatim and derives the *"no `tree_id` column and no per-tree read"* property from it,
which is load-bearing for its §6 batch design. Retirement matches: `tree-catalog` R2/R3 (`enabled:
false`, `retiredAtRevision`, displayed invalid, never repaired) is exactly `tree-state` §4's middle
row, and both agree a retired node **costs nothing to hold**. R5's import-boundary rejection appears in
both, with the same evidence and the same rule. R4's free full respec appears in both.

**S5 — `tree-state` → `tree-resolve`.** Nothing contradicts. `tree-state` §6 makes
`LoadTreeStateBatch` the primary entry point and asserts *"battle setup never calls the single-key
loader"*; `tree-resolve` §10 says *"No SQL. `tree-state` owns persistence … this module reads a state
object handed to it"*, and memoizes per actor keyed on the state reference. The two are compatible.
**The gap:** no spec names the caller that actually performs the batch read for a six-actor squad.
`tree-state` says battle setup does; `tree-resolve` says it receives an object; `tree-surface` is
per-actor by construction. That is a wiring assignment, not a disagreement — but it is the kind that
turns into 234 lock-serialised queries if nobody claims it.

**S6 — the node record is shared.** `species-tree` §1 is explicit that it re-specifies nothing about
the record: *"What does NOT change … the node record (`tree-catalog` §2 — map assumption 4, species
trees reuse it)."* `tree-catalog` §5 agrees (*"one record type serves generic and species trees"*), and
its open question 2 correctly hands the ship-vs-derive decision to `species-tree`. 40 nodes and
10 tiers × 2 branches are consistent across `tree-plan`, `tree-catalog`, `tree-review` and
`species-tree`. Only the *corpus totals* diverge (C11).

**S7 — `tree-review` → `tree-catalog` on id stability.** `tree-review` §8 quotes the catalog's scheme
verbatim, names both rejected alternatives, and states the dependency in its own Boundaries. Its
`O(diff)` claim is exactly the property `tree-catalog` R6 guarantees, and both specs pin it with a test
(`a_magnitude_retune_produces_an_empty_review_diff` / `retuning_a_magnitude_does_not_change_any_id`).
This is the cleanest seam in the set. It does inherit C3 — if the plan's id shape wins, `tree-review`'s
entire §8 budget changes.

**S8 — `tree-surface` renders through the shipped magnitude contract.** Verified:
`web/fusion-rpg-web/src/contract/types.ts:33-55` holds **13** members, matching the C# enum's 13, and
`tree-surface` §6 checks the union and concludes nothing must grow. Its three render paths are real —
`GameUnits` for a node's magnitude, `count` for `Depth 6`, `perMilleRatio`/`absolute` for `F` — and all
three are quantities the other specs produce (`tree-binder`'s `NodeAtom.unitClass`, `tree-state`'s
`soul_level`, `tree-resolve`'s `fMilli`). Souls render as composed prose rather than a `Magnitude`, so
no class grows. `tree-review` §5.2 rule 3 requires the same contract for the review card and even
solves where the renderer lives (a Node script inside the web package) so there is only one
implementation. Both specs land on the same answer independently.

**Also agreeing, checked and unremarkable:** the potency ceiling of 91‰ (`tree-plan` §5, quoted by the
map); the corpus of 39 generic trees and 1,560 nodes (seven specs); the quota algorithm's step-5
rebalance, stated in the same words by `tree-plan` §8, `tree-language` §4.2 and `species-tree` §3.1;
`nullification` being absent from the schema enum rather than discouraged (`tree-language` §5,
`tree-review` §6.4, `species-tree` open question 3, all with the same reason and the same open
question); the conversion-node refusal (`tree-plan`, `tree-binder` §7, `tree-catalog`, `tree-resolve`,
`mechanism-wiring` §9, `species-tree` §8 — six specs, one rule, no drift); and the D25 order-independence
lemma (`tree-state` §2, rendered as a player promise in `tree-surface` §5.2).

---

## 17. Design-gate checklist

```
[x] I identified the subsystems this touches - passive trees, the atom layer, ActorHub/derived
    stats, the power ladder, tunables, battle, and the web surface.
[x] I read every doc in the DESIGN-GATE §1 rows for those subsystems, this session:
    DESIGN-GATE.md (whole), passive-tree-map.md, and all eleven passive-tree/spec-*.md.
[x] I checked decisions.md for a lock covering this - there is still no passive-tree row; this
    note proposes no build and no lock.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments - roughly forty citations were opened and
    checked, and the results are in §14 and §15. Counts were re-counted, not quoted: 7/16/13,
    13 UnitClass (C# and TS), 21 statuses, 53 families, 98 affix families, 840 species,
    502 species files, 2 authored affixes.
[x] I read the surrounding section of every rule I quoted - notably ActorHub.Register's whole
    body (it replaces by id, which is what makes S2 a non-conflict) and tree-plan §7's whole
    "gate quantity" section, which is what makes C1 a real conflict rather than a wording slip.
[x] I tested (not assumed) the constraints I report. C2's archetype table, C9's per-mille error
    table and C11's corrected marginal price were COMPUTED here, not quoted. C10's
    "not reachable from Battle" was established by enumerating every DispatchInstant call site
    in src/ and tools/, not inferred from a folder grep.
[x] Nothing contradicts a §2 invariant. This note asserts no new cap, no new f(level), and no
    new vocabulary; every recommendation removes a duplicate rather than adding one.
[ ] Corrections are propagated - NOT DONE, deliberately. This note edits no spec: the task is
    read-only. Eighteen findings are listed with a recommended owner each; propagating them is
    the follow-up, and it is a tracked line here rather than a forgotten one.
```

---

## 18. Related

- [passive-tree-map.md](../../architecture/passive-tree-map.md) — the module index and the arrows this
  note reads
- The eleven specs: [tree-plan](../../architecture/passive-tree/spec-tree-plan.md) ·
  [tree-language](../../architecture/passive-tree/spec-tree-language.md) ·
  [tree-catalog](../../architecture/passive-tree/spec-tree-catalog.md) ·
  [tree-binder](../../architecture/passive-tree/spec-tree-binder.md) ·
  [tree-state](../../architecture/passive-tree/spec-tree-state.md) ·
  [tree-resolve](../../architecture/passive-tree/spec-tree-resolve.md) ·
  [tree-review](../../architecture/passive-tree/spec-tree-review.md) ·
  [tree-surface](../../architecture/passive-tree/spec-tree-surface.md) ·
  [species-tree](../../architecture/passive-tree/spec-species-tree.md) ·
  [mechanism-wiring](../../architecture/passive-tree/spec-mechanism-wiring.md) ·
  [squad-harness](../../architecture/passive-tree/spec-squad-harness.md)
- [03-llm-stage-contract.md](03-llm-stage-contract.md) §7 — the 29-gate list two specs still cite
- [04-number-and-atom-binder.md](04-number-and-atom-binder.md) §3.5 — the superseded 17% figure
- [16-depth-exhaustion.md](16-depth-exhaustion.md) — the saturation bound both `tree-plan` and
  `tree-resolve` build on, and agree about
