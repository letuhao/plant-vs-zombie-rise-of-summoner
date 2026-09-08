# 19 — Adversarial pass over the eleven passive-tree specs

**Scope:** `docs/architecture/passive-tree/spec-*.md` (eleven files), against
[`passive-tree-ideal.md`](../../architecture/passive-tree-ideal.md) (36 decisions),
[`passive-tree-map.md`](../../architecture/passive-tree-map.md), and research 01–16 in this
directory. Read → verified against code and against the specs' own citations → argued.

**Not this document's job.** Decision coverage and interface seams are two sibling audits running in
parallel. This one asks a different question: **are these the right specs — buildable, testable,
honest, and worth building.**

**What I did not do.** I ran no test suite and built nothing; no build is authorized. Every number
below is either quoted from a spec with its line, arithmetic I computed here and showed, or a fact
read out of `src/` this session. Where a claim is arithmetic rather than measurement, it says so.

**What I verified in code this session**, because several arguments turn on it: `ActorHub.cs:145,148,155`
registers three subsystems and two of the three are guarded by a null delegate; `AtomKindRegistry` is
**7 / 16 / 13** by counting (`AtomKind.cs:8-31`, sixteen `new(…)` rows at `AtomKindRegistry.cs:476-869`,
`AtomKind.cs:97-101`); `stat.derived` is `RuntimeState.None` in Sim at `:534` and `AtomTriggers.None`
at `:535`; `CombatDamageDispatcher.TryReflect` (`:85`) has exactly one caller, `DispatchInstant` at
`:71`, gated on `actorResolve != null` at `:70` — and Battle's bag (`BattleEffects.cs:55`) never sets
`ActorResolve`, so **reflect is unreachable from Battle**; Battle applies HP at `BattleRunState.cs:497`;
`RecomposeDerived` (`:157`) has one call site, `:323`, in the constructor's aura loop;
`BasicAttack.cs:184` raises `OnDamageDealt` and nothing in `src/` raises `OnDamageTaken`, `OnSpawn` or
`OnDeath`; `AffixTags.cs` is 124 lines with **zero** production callers; `PowerLadderKMicro` **does not
exist** anywhere in the repo; `ssot-power-scale.md` §10 and `inventory.json` both carry **27 rows** and
neither mentions a passive tree, `req(t)`, `W(T)` or `nodesOwned`; `data/tuning/passive-tree.v1.json`
and `passive-tree-targets.v1.json` **do not exist**; exactly **one** seedsmith metric gates today
(`DemonRoster/UnresolvedCount`, `metrics/demon_roster.py:370`). The program has design documents and
no code, no tuning data, no SSOT row, and a locked placeholder tab.

---

## Verdicts

| # | Thesis | Verdict | One line |
|---|---|---|---|
| 1 | Unbuildable in the order the map claims | **THESIS HOLDS, narrowly** | `tree-language` declares its own input interface unfrozen and was written against an unspecced `tree-plan`; two wave-0 prerequisites doc 15 names are in no module's scope |
| 2 | Open measurements laundered as settled | **SPECS HOLD, with one exception** | `tree-resolve`, `squad-harness` and `tree-plan` name every open number honestly — but `tree-plan`'s headline "no tree is OP" rests on a scalar the program measured is not value, and only `squad-harness` says so |
| 3 | The generation gates are unfalsifiable | **SPECS HOLD** | The gates are structural, the machinery is shipped, and the specs say out loud which gate reports `NOT_MEASURED` rather than passing |
| 4 | The mechanism-node story is still circular | **THESIS HOLDS** | The premise "mechanism rescues focus" is unmeasured, its acceptance test is A10, and A10 lands in the same wave as the plan that commits 40–60% of the corpus to it — with no gate between them |
| 5 | Nobody priced the second time | **THESIS HOLDS on three of five change types** | `O(diff)` is proven for a magnitude retune. Adding one element or one status re-cells the whole corpus, and an atom-kind or resolver change has no price at all |
| 6 | *(mine)* D36's flat unlock cost holds for one of three archetypes | **THESIS HOLDS** | `first = 5, step = 2` is derived from a constant `k = 4` that only `broad-and-flat` produces; across the shipped archetype set reward-per-skill-point spans 6× |
| 6d | *(mine)* The node potency ceiling refuses nothing | **THESIS HOLDS** | It is derived from the topology's own maximum, so `R-P1` and `R-P2` are tautologies, and two of the spec's own tests describe different implementations |
| 6e | *(mine)* `RespecPolicy` is not what four documents say it is | **THESIS HOLDS** | It returns **Soul**, not Hunger, and it has a **production caller** — so `tree-state`'s three-way contradiction is a two-way agreement, and its stated reason to defer is already moot |

---

## 1. "Unbuildable in the order the map claims"

### The attack

`passive-tree-map.md:33-37` gives five waves. Wave 0 is `squad-harness` · `mechanism-wiring` ·
`tree-plan`, "fully parallel, no shared files". Wave 1 is `tree-catalog` · `tree-language`.

Three problems.

**(a) `tree-language` says its own input is not frozen.** `spec-tree-language.md:565-567`:

> **Interface not yet frozen:** `tree-plan` is wave 0 and **unspecced**. Every plan field this module
> reads (`quotaCell`, `requiredProperties`, `propertyVocabulary`, `mechanismFloor`,
> `budgetShareMilli`) is the interface this module *requires*; the names must be reconciled when
> `spec-tree-plan.md` lands.

`spec-tree-plan.md` landed the same day (its mtime is later). Nothing was reconciled, and the
divergence is not cosmetic:

| | `tree-language` | `tree-plan` |
|---|---|---|
| Mechanism selection | `cell.nodeClass := "mechanism" if tier >= t.mechanismFloor` (`:157`) — a floor tier | `mechNodes[t]`, an exact per-tier count from a monotone ramp (`:288-302`) |
| Corpus size | `N := 1,560` as a literal (`:144`), asserted by test at `:504` | `N := Σ over trees of nodesPerTree` (`:530`), and `:766` forbids hardcoding a roster count |
| Who emits the plan file | `emit data/seed/passive-tree/plan/<treeId>.json` (`:165`) | `data/seed/passive-tree/plan/<treeId>.v1.json` (`:620`) |

The floor reading is strictly weaker than the ramp. Under `broad-and-flat` the ramp puts one
mechanism node in tiers 4–7 and two in tiers 8–10; a floor check at tier 8 passes a tree that carries
zero mechanism nodes at tiers 4–7. `tree-language`'s gate 16 and its test
`mechanism_floor_holds_at_deep_tiers` (`:508`) both check the floor. So wave 1's gate cannot enforce
wave 0's quota.

**(b) Two wave-0 prerequisites are in no module's scope.**
[15-dependency-map.md](15-dependency-map.md) puts three items in its own wave 0 — the
`ssot-power-scale.md` §10 rows (B8), `PowerLadderKMicro` (B5), and the import-boundary migration fix
(B6) — and says *"Nothing in the passive-tree program may be specced before these rows are
reviewed."* The map's wave 0 contains none of them. B5 surfaces in `tree-binder` (wave 2, `:207-219`,
an **Ask first** `src/` change), B6 in `tree-catalog` (wave 1, R5), B8 in `tree-plan` and `tree-state`
as things owed "before this module ships".

**(c) `squad-harness` cannot answer its own question in wave 0.** Its §4 model needs D25's ownership
cost and the soul track before any claim above Θ ≈ 300 means anything (`:164-176`), and both are
deferred to S2/S3. Its §10 lists six mechanism classes it cannot score until `mechanism-wiring`
lands. A wave-0 harness therefore re-measures the magnitude-only model that §3.5 already closed.

### The rebuttal

**(a) is real but small, and its cost is bounded.** `tree-language` is a `tools/seedsmith` adapter
that reads a JSON plan. Reconciling five field names before a line is written is a paragraph of work,
not a re-design, and `tree-plan` is the later and better-argued document on every point of
disagreement (its `mechNodes[t]` derivation shows the arithmetic; the floor does not appear in it at
all). Nothing here is a wrong boundary — it is an un-run diff.

**(b) is the weakest limb.** Doc 15's *"nothing may be specced before B8"* was a research
recommendation, not a lock, and `tree-plan:1021-1028` and `tree-state:512-519` both carry the §10
rows as named, argued obligations with the reason `guard-power.ps1` cannot catch their absence. The
gate moved from "before spec" to "before ship" — a defensible relaxation. B5 and B6 have owners
inside the spec set; what they lack is a *wave*, and both are size S.

The obligation is real, though, and I checked it rather than assuming: `ssot-power-scale.md` §10 and
`docs/architecture/power/inventory.json` both carry **27 rows**, and neither file contains the string
`passive`, `req(t)`, `W(T)` or `nodesOwned` outside a citation path. `PowerLadderKMicro` has zero hits
repo-wide. So all three items are genuinely open, and `tree-plan`'s note that `guard-power.ps1` stays
green without them is correct — its G2/G3 checks key on a parameter named `level`/`lvl`/`index`, and
`req(t)`'s is `t`.

**(c) is answered inside the spec.** `squad-harness:365-377` stages exactly this: S1 answers D33 at
the calibration point, S2 adds the concentration model and D25, S3 adds the soul track and the Θ
range. It never claims S1 settles `w` or `Fmax`. And it exists in wave 0 precisely because it is
cheap and nothing depends on it (`map:44-48`).

### Verdict — **THESIS HOLDS, narrowly**

The build order is right about dependencies and wrong about readiness. Wave 1's `tree-language` was
written against an interface that did not exist, says so, and nobody closed the loop. That is one
reconciliation pass, but it must happen before a line of the adapter is written, because the quota
cell is what the whole no-numbers fence hangs off. The two orphaned wave-0 prerequisites are a
scheduling gap, not an architectural one.

---

## 2. "The specs encode unresolved measurements as if settled"

### The attack

Five open numbers, all load-bearing:

- **`Fmax`** — retained provisionally, and *"every measurement of it predates D25"* (ideal `:698`).
- **`w`** — unmeasured, and past Θ ≈ 300 it is the only thing separating builds
  ([16](16-depth-exhaustion.md)); at `w = 1` there is no late game.
- **The potency ceiling** — recomputed here from 125 to 91 because doc 02's constant was derived at
  seven tiers.
- **D28** — measured, then bounded to Θ ≲ 300 by a later re-measurement, and still reading
  *"pending a D25 re-run"*.
- **D15's central claim** — that equal `PowerVector.Total` is equal value — was measured **false**:
  the twelve corners span 0.3%–97.9% mean win share at identical Θ and identical budgets
  ([11](11-adversarial-debate.md) §6b).

The charge is that a spec set turns each of these into a requirement with a key, a unit and a test,
and the open question disappears into a config file.

### The rebuttal

On four of five, the specs are better than the ideal.

- `spec-tree-resolve.md:278-286` is a section titled *"`F` is provisional, and this module must not
  hide that"*, and it converts the provisionality into a design constraint: `Fmax = 1000‰` must be a
  legal, tested configuration, *"so if `F` is withdrawn later it is a tuning change, not a
  refactor."* That is the correct engineering response to an unresolved number.
- `spec-tree-resolve.md:175-196` records D28's bound as a table of measured cells, under a heading
  that says exactly what it is doing: *"The measured range, recorded rather than the claim."*
- `spec-squad-harness.md:218` names `concentration.w` as the module's primary output and repeats the
  `w = 1` consequence. `:308` goes further and admits the harness may not be able to resolve it: the
  crossover gap is *"0.5pp or less — below the noise floor of a 3,000-trial run"*, and designs a
  refine pass and a `transfers: false` rule around it.
- The potency ceiling is re-derived in the open at `spec-tree-plan.md:367-391`, with the stale
  constant named and the 1.37× consequence stated.

**The exception is D15, and it is at the centre of the spec set.** `spec-tree-plan.md`'s stated
objective (`:21-29`) is:

> Every tree in the corpus costs the same and awards the same, in one conserved scalar … That is
> D15's *"equal expected value, not equal shape"* … *"No tree is OP"* is machine-checkable and is
> asserted by a test (§Testing, `C1`).

`C1` asserts equal `Σ budgetPoints` — `PowerVector.Total`. The program's own research measured that
this scalar is not value, by a factor of ~300 at the extremes. `spec-tree-plan.md` never mentions
that finding. It defends `Total` against `PowerScalar.Of` (`:172-173`) and it concedes `CostFunction`
is *"knowingly wrong on multiplicative pairs"* (`:323-325`) — but the reader is left with
"machine-checkable no-OP-tree" as a shipped property.

The one spec that says it plainly is `squad-harness`, at `:379-384` and `:649-654`, and its own words
are the indictment: *"**No module in `passive-tree-map.md` is scoped to produce that evidence** —
`tree-plan` owns the budget *rule*, not the measurement."* Whether D15 is re-opened is offered as
that module's open question 3, i.e. as an optional S4 mode.

### Verdict — **SPECS HOLD, with one exception**

Four of the five open numbers are handled better in the specs than in the ideal. D15 is laundered —
not by concealment, but by placement: the honest sentence lives in the one module that has no
dependents, and the claim it undercuts is the headline of the module everything else reads.

**The cheap fix:** one paragraph in `spec-tree-plan.md` §3 stating that `Total` conserves *cost*, not
*value*, that the 0.3%–97.9% measurement is why, and that `C1` is therefore a fairness property over
authoring effort rather than a balance proof. It costs a paragraph and it stops the phrase
*"no tree is OP"* from being quoted later as a shipped guarantee.

---

## 3. "The generation pipeline cannot be tested before it exists"

### The attack

`tree-plan`, `tree-language` and `tree-binder` each specify validation gates over a corpus that does
not exist. `tree-language` alone names 24 (`:388-414`). Two of them are load-bearing and both are
hollow today:

- **Gate 19, `ExclusionResolvable`**, checks every exclusion predicate against the plan's
  `propertyVocabulary`. D14's whole mechanism is property-keyed exclusion — and the atom-tag corpus
  carries **three** semantic values (`spec-tree-language.md:107`, `spec-tree-plan.md:426-429`).
- **Gate 16, `MechanismFloor`**, checks `nodeClass` at deep tiers. `nodeClass` is derived from the
  plan and re-derived from bound atoms; nothing in it asks whether the mechanism does anything.

A gate with nothing to check is a checkbox, and a threshold nobody can name in advance is not a gate.

### The rebuttal

This one collapses on contact with the specs.

**The gates are not hypothetical machinery — they are shipped machinery pointed at a new family.**
Gate 1 is `audit_schema`, which raises at `Pipeline.__post_init__` before a model call is made
(`:390`); gate 6 is `run_preflight`; gates 7/9 are `run_g1`/`run_g2`; gate 11 is `verify_permutation`;
gate 14 is `should_generate` over `ProvenanceLedger`; gate 20 is `setgen/dedup.py`'s local Jaccard,
chosen deliberately over the shared MinHash because *"the shared MinHash over-reports 7× on real
pairs"* (`:409`). Every one of these has run against the 840-entry demon corpus.

**The specs already say which gates cannot pass yet, in the required word.**
`spec-tree-language.md:229-234`: *"until an atom-tag registry lands … a predicate can key on
`posture` and nothing else, and this module's exclusion gate must report that as a `NOT_MEASURED`
rather than a pass."* And `spec-tree-review.md:413-416` adopts `RunReport.verdict`'s discipline
whole: **`FAIL` beats `NOT_MEASURED`, and a single held partition denies a pass.** An unrunnable gate
is therefore not a green checkbox — it blocks the lot.

**Thresholds are deliberately unset, and the precedent is exact.**
`spec-tree-language.md:421-434` promotes exactly one metric to `gates=True` and explains why the rest
start `False`: *"a threshold promoted before a real run is a threshold nobody can name in advance."*
Counted this session, **that is exactly the shipped posture**: of 47 registered seedsmith metrics,
precisely one gates — `DemonRoster/UnresolvedCount` (`metrics/demon_roster.py:370`) — and
`metrics/distribution.py:8-10` states the reason in its own words. `DemonQualityReport` likewise
reports and does not gate (`Program.cs:23-24`). The specs are copying a working discipline, not
inventing a permissive one.

**And the behavioural half is not claimed.** `spec-tree-plan.md:333-337` states the mechanism quota's
weakness in its own boldface — *"The quota is checked structurally; the value it stands in for is
behavioural"* — lists four mitigations, and then names what remains: *"Residual risk, owned and not
hidden."* `spec-tree-review.md:213` repeats it: `nodeClass` *"is a plan-side label, so the gate checks
the plan against itself."*

**Sampling is real and priced.** `tree-review` §3 uses the shipped `stratified_sample`, computes
Clopper–Pearson bounds rather than tabling them (60 clean trees ⇒ ≤ 4.87%), states the design effect
against it (`:172-183`), and separates what each design proves from what it does not (`:99-113`).
Its test fixtures are *"synthetic lots with a deliberately injected defect … the only way to prove a
check would notice"* (`:609-611`).

### Verdict — **SPECS HOLD**

The gates are falsifiable because the machinery is shipped, the fixtures are adversarial, and the two
gates that genuinely cannot run today are required to report `NOT_MEASURED`, which denies a pass.
This attack is worth stating only so the record shows it was made and failed.

---

## 4. "The mechanism-node story is still circular"

### The attack

Four facts, each from a different spec, and they close a loop.

1. **Only mechanism nodes rescue a focus build.** Ideal §3.5 swept `b ∈ {0,2,5,10,20}` ×
   `Fmax ∈ {1.0,1.25,1.5}` and *"not one cell reverses the ordering"*. The conclusion is a design
   constraint, restated as `mechanism-wiring`'s charter (`:41`).
2. **The class is unscorable until G3 lands.** `stat.derived` is `RuntimeState.None` in Sim
   (`AtomKindRegistry.cs:534`), and `RuntimeState.None` is *"a **rejection**, not a degradation"* —
   `BindGate` and `Compilability` both refuse with `RuntimeUnsupported`
   (`spec-mechanism-wiring.md:306-308`).
3. **One of the two classes ranked "ship content today" is not reachable from Battle at all.**
   `spec-squad-harness.md:342` corrects the ideal: reflect lives in
   `CombatDamageDispatcher.TryReflect`, reached only from `DispatchInstant`; Battle applies HP through
   `DamageApplyPipeline.Apply`, and `reflect` has zero hits in `src/FusionRpg.Core/Battle/`.
   **"Reflect is not measurable at squad scope today."**
4. **The plan commits budget anyway.** `spec-tree-plan.md:295-302` puts 8–12 of 20 nodes per branch
   into the mechanism class, and pins the deepest tier — the largest budgets in the tree, up to 91‰
   of `budgetTotal` — at 100% mechanism, as a structural refusal (`R-M1`, `:306-307`).

**And the two wave-0 specs contradict each other on point 3.**
`spec-mechanism-wiring.md:54` still reads:

> | **2** | **Retaliation** | — | Already live (`EffectRuntime.cs:491`). Content, not code |

That citation is the one the ideal already retracted at `:648` (*"this row previously cited
`EffectRuntime.cs:491`, which is the *ShieldGate* wiring, not reflect. **Battle does NOT reflect**"*),
and the one its wave-0 sibling refutes again in more detail. So `mechanism-wiring`'s own table of
"what lands when this module lands" carries a class that lands nowhere Battle or Sim can see —
without a scope qualifier, in a row that says "Content, not code".

**The circle:** the plan reserves the corpus's largest budgets for a node class whose value is
unmeasured, whose measurement is blocked on a sibling module in the same wave, and one of whose two
"already live" exemplars is live only on the lawn.

### The rebuttal

Most of this is stated by the specs themselves, and the residual is smaller than it looks.

- **`mechanism-wiring` is in wave 0 for exactly this reason**, and says so: *"If the wiring never
  lands, that budget buys nodes that measurably do nothing, and nobody finds out until
  `tree-resolve`"* (`:58-60`). The critical path is one file, ~90 lines by the shipped
  `AtomDerivedSubsystem` precedent.
- **G3's fix order is copied from a decision that already fixed this exact failure once.**
  `decisions.md:106` made the lawn cell move *"deliberately the LAST step of that change, not the
  first"*, and `spec-mechanism-wiring.md:320-339` adopts the same four-step order and refuses to
  write `Full` into the cell without running the four derived ops.
- **On-hit mechanisms are measurable in Battle today.** Battle raises `OnDamageDealt` from
  `BasicAttack.cs`, which is `partial class BattleEngine` in `Actions/` — the reason a Battle-folder
  grep keeps missing it (`spec-mechanism-wiring.md:80-90`, `spec-squad-harness.md:327`). So Erosion,
  the top-ranked class, is reachable once G1 and G2 land.
- **The plan's quota is a count, not a budget, and that was a deliberate choice.**
  `spec-tree-plan.md:315-331`: two budgets would owe an exchange rate between mechanism and magnitude
  points, *"precisely what cannot be computed today"*; a quota owes nothing. Being wrong about
  mechanism value therefore costs a regeneration, not a re-derivation of the budget math.
- **Node ids do not move if the quota is wrong.** Ids are positional
  (`<treeId>/<off|def>/t<tier>/<index>`, `spec-tree-plan.md:829`), so revising `mechNodes[]` changes
  cells and content, never ids. There is no migration in being wrong.

### Verdict — **THESIS HOLDS**

The rebuttal disposes of the scariest reading — this is wiring, the fix order is known, the cost of
being wrong is a regeneration — but it does not dispose of the thesis, because of what
`mechanism-wiring` itself says about A10:

> **A10 is the one that matters.** A1–A9 prove the wiring; A10 proves the wiring was worth doing. If
> Erosion costs a spread build no more than it costs a corner, §4c's claim is INFERENCE that did not
> survive measurement, **and `tree-plan` needs to know that before it reserves deep-tier budget.**
> — `spec-mechanism-wiring.md:714-716`

The spec names the correct gate and then the map removes it. `tree-plan` and `mechanism-wiring` are
declared "fully parallel, no shared files" (`map:33`, `:40-42`), so `tree-plan` **cannot** know. The
premise that justifies the entire layer — that mechanism does what magnitude provably does not — is
inference, and the acceptance test for it is scheduled beside, not before, the commitment it is
supposed to gate.

Two concrete defects fall out:

1. **`spec-mechanism-wiring.md:54` must be corrected.** It reproduces a citation the ideal retracted
   and its wave-0 sibling refutes. Checked in code this session, `squad-harness` is right and the
   mechanism is worth naming precisely: `TryReflect` (`CombatDamageDispatcher.cs:85`) is called only
   from `DispatchInstant` at `:71`, behind `if (actorResolve != null && …)` at `:70`; the only two
   assignments of `bag.ActorResolve` in `src/` are `FoundationHarness.cs:118` and the injector's
   `EffectRuntime.cs:496`; Battle builds its bag at `BattleEffects.cs:55` and never sets it. So the
   gate is *always false* on the Battle path — reflect is not merely unwired there, it is switched
   off by a null check. As written, a builder reading that table concludes reflect is available
   content. Row 3 (threshold triggers, "Already live") carries the same unstated lawn-only scope.
2. **A10 is not a testable success criterion.** It asks for a *"different* win share for a spread
   defender than for a corner defender"* — with no magnitude and no half-width, against a trial
   harness whose own noise floor is 0.9pp at 3,000 trials (`spec-squad-harness.md:297`). "Different"
   is an outcome, not an acceptance bar. See §Six areas.

---

## 5. "~35,000 nodes is a maintenance liability nobody priced"

### The attack

`tree-review` prices generation and the first review well: ≈34 h per full pass, ≈78–117 h for the
first catalog, plus 49–91 machine-hours (`spec-species-tree.md:315-360`). It then claims steady state
is `O(diff)`.

The five change types that will actually happen are a rebalance, a new element, a new status, an atom
kind, and a resolver change. The `O(diff)` table (`spec-tree-review.md:479-483`) has five rows and
covers **one** of them.

### The rebuttal, tested row by row

**(1) A rebalance — the claim holds, and it is better than it looks.** A magnitude retune touches
`data/tuning/` only: ids do not move, and what a human judged (name↔effect coherence, flavour,
species recognition, corpus sameness) does not change. It has a test —
`a_magnitude_retune_produces_an_empty_review_diff` (`:602`). And the program's largest pending
rebalance — `budget.treeTotalPoints`, shipped as a flagged guess (`spec-tree-plan.md:920`,
`:1064-1069`) — is scale-invariant in the plan: `budgetShareMilli` and `potencyBand` are per-mille
shares, so scaling the total moves no cell and no band. `tierLadder.kPoints` is the same. **Concede
this row fully.**

**(5) A resolver change** is not a re-review problem at all: it changes what nodes *do*, not what
they *say*. Its cost is a re-measurement (`squad-harness`, `CombatSim`), not a human pass.
**Concede.**

**(2) and (3) — a new element, a new status — the claim fails, and the mechanism is in the plan.**
`spec-tree-review.md:480` prices these as *"new trees … full protocol over the new lot only. The lot
is its own population."* That is true of the new tree's forty nodes. It is not true of the corpus,
because of `spec-tree-plan.md:529-545`:

```text
1  N := Σ over trees of nodesPerTree                       # 39 × 40 = 1,560 today
2  quota[a] := largest_remainder_count(targets[a].weightsMilli, ORDER[a], N)
3  seq[a]   := expand_counts(quota[a], ORDER[a])
4  for each tree in roster order, each (branch, tier, index) slot in canonical order:
       cell := { a: seq[a][cursor] for a in AXES } ; cursor += 1
```

A 22nd status does three things at once: `N` goes 1,560 → 1,600; the `status` axis gains a member, so
`quota[status]` re-apportions; and every axis's `seq[a]` is rebuilt over a different total. The
cursor then walks a different sequence, so **every existing node's `quotaCell` can move.** `cell`
drives `permittedIds` (`:552-554`), `permittedIds` is the schema `enum`, and a committed node whose
`affixIds` now sit outside its new cell fails gate 9 (brief conformance) and gate 15 (`QuotaDrift`).
Under `spec-tree-review.md:482`, a plan change is *"full re-review of the affected trees"* — and the
affected set is all 879.

`spec-tree-plan.md:251-259` proves append-safety for exactly two things — archetype assignment and
node ids — using `Aptitude.cs:16-17`'s append-only ordinals. It is silent on quota cells. Worse,
`:454-456` presents the re-derivation as a virtue: *"A thirteenth aptitude changes this grid by
construction."* It does, and that is the hazard, not the feature.

This is not hypothetical shape. D27 explicitly ships the roster whole and sequences family trees as
*build-order work* (`ideal:67`), so a roster append is a planned event, not an edge case. The first
demon-family roster lands `F` trees at once.

**(4) An atom-kind change has no price anywhere.** The 17th kind (D16/B2) is the one change that
alters what a node can express. `spec-mechanism-wiring.md:608-647` scopes the code cost carefully
(kind count, executor per runtime, `ParamSchema`, `PowerCategory`, `RuntimeSupportMatrix`, a reviewed
`decisions.md` row, propagation to `DESIGN-GATE.md` §1). Nobody costs what it does to the corpus:
`conversionState` is already an emitted axis with **zero** budget (`spec-tree-plan.md:460-465`), so
landing the kind means allocating that budget, which means new quota weights, which is a plan change
— i.e. rows 2 and 3's problem again, with a full re-cell.

### Verdict — **THESIS HOLDS on three of five change types**

`O(diff)` is proven for the change class that will happen most often and is genuinely free. It is
false for a roster append, unpriced for an atom kind, and irrelevant for a resolver change (which is
fine, and should be said).

**The fix is one design rule, and it is cheap now and expensive later:** make quota-cell assignment
append-stable — apportion per tree from a per-tree total, or freeze a tree's cells in its committed
plan file and apportion only the new lot against the residual. Then a roster append really is "the
lot is its own population", which is what `tree-review` already assumes.

---

## 6. My own objections

Three, and none of them appears in the ideal, the research, or any of the eleven specs. The first is
the one I would fix first: it is pure arithmetic over two shipped specs, and I have shown the working
so it can be checked in five minutes.

### 6a. The headline — D36's flat unlock cost is flat for one archetype in three

`spec-tree-state.md:143` derives the unlock curve:

> With `k = 4` nodes per tier (D29: 40 nodes / 10 tiers), `first = 2.5·step`, so `step = 2` gives
> `first = 5`. **That condition is what makes reward-per-skill-point flat at *every* tier**, exactly
> as D26 did for reward-per-aptitude-point.

`k = 4` is 40 ÷ 10 — the corpus **average**. But `spec-tree-plan.md:221-243` ships three archetypes
whose per-tier widths are deliberately *not* uniform, because non-uniformity is the entire mechanism
by which D15 gets "equal value, not equal shape". Per tier, across both branches:

| archetype | nodes per tier, t = 1..10 |
|---|---|
| `broad-and-flat` | 4 4 4 4 4 4 4 4 4 4 |
| `gated-deep` | 6 6 6 4 4 4 4 2 2 2 |
| `late-crown` | 2 2 4 4 4 4 4 4 6 6 |

So `k` is 4 only for one of the three, and ranges 2–6 in the other two.

Work the consequence. With `first = 5, step = 2`, the cumulative skill-point cost of owning `N` nodes
is `5N + 2·N(N−1)/2 = N² + 4N = N(N+4)`. Tree power to tier `T` is `W(T) = b·T(T+1)/2`. Reward per
skill point is `W(T) / cost(N(T))`:

| tier | `broad-and-flat` | `gated-deep` | `late-crown` |
|---:|---|---|---|
| 1 | b/32 | b/60 | b/12 |
| 2 | b/32 | b/64 | **b/10.7** |
| 3 | b/32 | **b/66** | b/16 |
| 5 | b/32 | b/52 | b/21 |
| 7 | b/32 | b/46 | b/24 |
| 10 | b/32 | b/32 | b/32 |

`broad-and-flat` is exactly `b/32` at every tier — D36's derivation is correct, for `k = 4`. The other
two are not:

- **`gated-deep`** runs `b/66` at tier 3 to `b/32` at tier 10 — a **2.06× gradient favouring depth**.
- **`late-crown`** runs `b/10.7` at tier 2 to `b/32` at tier 10 — a **3.0× gradient favouring shallow**.
- Cross-archetype at tier 2, two trees the plan certifies as equal value differ by **6.0×** in reward
  per skill point (`b/10.7` against `b/64`). At tier 7 — where an all-in build sits at Θ = 100
  (`spec-tree-plan.md:138`) — the spread is still 1.92×.

**All three agree exactly at tier 10 and nowhere else.** That is why it was not caught: the endpoint
is identical, the *whole tree* costs the same 1,760 skill points everywhere, and the derivation was
checked at the endpoint.

### 6b. Why it matters

Three separate claims break, and none of them is decorative.

1. **D26's flatness is halved.** `spec-tree-plan.md:101-126` proves reward-per-*aptitude*-point is
   `b/k` at every tier, exactly, and that proof is archetype-independent (`req(t)` is per tier, not
   per node). `tree-state` was written to give the *skill-point* currency the same property. It does
   not, and the failure is the shape of D20's original tier-1 defect — with the sign reversing
   between archetypes.
2. **D15 is false in the currency the player spends.** `C1` conserves budget points. Nothing
   conserves skill points per unit of power at partial depth — and partial depth is the whole game
   below Θ ≈ 170.
3. **D25's concentration reward is measured against the wrong `k`.** Doc 12's second derivation,
   `g = 3·s·step·k²/5 = 10.40`, is quadratic in `k`. At `k = 6` it gives ~23; at `k = 2`, ~2.6. So
   `grant.skillPointsPerThetaMilliByScope` at commander — the 1 → 11 change D34 requires — is
   calibrated on the average archetype and is off by a factor of ~2 either way for the other two.

And the test will not catch it:
`reward_per_skill_point_is_flat_when_first_equals_step_times_k_plus_one_over_two`
(`spec-tree-state.md:463`) asserts the algebra for a given `k`. It passes on a `k = 4` fixture and
never sees the archetype set.

### 6c. The fix, in order of cost

1. **Cheapest, and it costs nothing else:** add one test in `tree-state` that computes
   reward-per-skill-point over `tree-plan`'s **actual** archetype width vectors, at every tier, and
   asserts a stated tolerance band. It will fail today. That converts an invisible defect into a
   named number.
2. **Then choose.** Either derive `first` per archetype (it is already per-tree data), or constrain
   the archetype set to a constant per-tier node count and find the archetype variation somewhere
   that does not sit in a cost ladder — `gated-deep` and `late-crown` already differ 2.5× in strongest
   single node, which is D15's stated payoff and is untouched by this.
3. **Either way, restate `spec-tree-state.md:143`.** *"With `k = 4` nodes per tier"* must read *"with
   a constant `k` nodes per tier"*, and must name the archetype constraint it depends on. It already
   says the right thing one paragraph later — *"Changing D29's tree shape means re-deriving `first`"*
   — without noticing that `tree-plan` ships three shapes.

### 6d. A second finding: the node potency ceiling refuses nothing

Smaller, but it is worth a line because the ceiling carries a tunable key, a `_note`, two refusals,
two tests and a success criterion.

`spec-tree-plan.md:367`: `maxNodeShareMilli = round_half_up(1000 / ((tierCount + 1) · minTerminalWidth))`,
with `minTerminalWidth = 1` → **91‰** at ten tiers.

The largest node the plan can emit is `nodeBudget[t] = 1000·t / (110·w[t])` per mille of
`budgetTotal`, maximised over `t`. Since `w[t] ≥ 1` (enforced — `node_budget_milli` raises below 1,
`:678`) and `t ≤ 10`, the maximum of `t/w[t]` is `10/1`, giving **90.909‰ → 91‰**. That is the
ceiling.

So:

- **R-P1** ("no emitted node budget may exceed the ceiling") compares a construction against its own
  supremum. It cannot fire at the shipped topology.
- **R-P2** ("every archetype must satisfy `1000/((T+1)·w[T]) ≤ maxNodeShareMilli`") reduces to
  `w[T] ≥ 1`, which is already a hard precondition.

The spec notices the tightness and reads it as confirmation: *"the admitted archetype set touches the
ceiling exactly"* (`:389-391`). It touches it because the ceiling was derived from it.

Two of the spec's own tests cannot both hold, which is the cleanest way to see this:

| `:713` | `every_shipped_archetype_is_admissible` | "a `w[T] = 1` archetype on a deeper ladder fails" |
| `:714` | `potency_ceiling_is_recomputed_not_read` | "the emitted `91` is derived from `tierCount`" |

If the ceiling is recomputed from `tierCount`, a `w[T] = 1` archetype on a fifteen-tier ladder gives
`1000/16 = 63‰` against a recomputed ceiling of `63‰` — and passes. The two tests describe different
implementations.

**The consequence.** Eleventh Hour Games' rule — *"individual nodes should not be so potent that you
feel forced to build it in a particular way"* — is the reason R7 exists. As specified, the answer to
*"is a 91‰ capstone too potent?"* is *"no, because we defined too-potent as above 91‰."* The ceiling
is a **reporting** value, and calling it a refusal is the overclaim. Making it bite means setting it
below the topological maximum — at which point R-P2 becomes a real admissibility test that
`gated-deep` would have to pass on its merits.

### 6e. A third finding: `RespecPolicy` is not what four documents say it is

Small, verifiable, and it closes an open question rather than opening one — which is the mirror of
the defect this repo's *no manufactured uncertainty* rule names.

Four documents carry the same two claims. `spec-tree-state.md:521-528` (open question 1):

> `decisions.md:103` locks *"a resource fighting also costs"*, D18 says souls, and
> `RespecPolicy.PriceOf` returns **Hunger** with **zero production callers**
> (`RespecPolicy.cs:33-37`). One of the three has to move. **Respec must not gain a production caller
> before this is settled**, because the shipped allocate path is already an unpriced full reset.

`spec-mechanism-wiring.md:766` repeats *"returns Hunger and has zero callers (**B11**)"*; so does the
ideal `:669` and `:512`, and [06-red-team.md](06-red-team.md) F7, which is where it originates.

**Both halves are false, read this session:**

- `src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs:45` returns
  `new RespecPrice(RespecResource.Soul, amount)`. **Souls, not Hunger.**
- `src/FusionRpg.Data/Sqlite/RpgStore.SpeciesRespec.cs:154` calls it — the only production caller, and
  a real one: it reads the soul balance, returns `"souls.insufficient"` below price, appends a
  `SoulEarnPolicy.Reasons.Respec` debit to `rpg_soul_ledger`, and is deduped by correlation id.

The policy's own doc comment (`:29-34`) even carries the design argument D18 wants — linear, not
geometric, *"because geometric escalation against a flat soul faucet is how a price becomes a
ceiling"* — and *"always available, always priced, never refused."*

**What changes.** The three-way contradiction is a two-way agreement: the shipped policy and D18 both
say souls, and only the `decisions.md:103` reading is in tension. `tree-state`'s stated reason to
defer — *"respec must not gain a production caller before this is settled"* — is moot, because it has
one and has had one. And *"respec is free today"*, which is load-bearing for red-team F7's
mispricing argument, is true only of the aptitude allocate endpoint, not of species respec.

**The fix is a re-read, not a decision.** Open question 1 should be narrowed to the one thing that is
genuinely open: whether the *tree* respec D18 describes reuses `RespecPolicy`'s soul price and its
escalation curve, or gets its own. That is a small question with an obvious first answer.

**This is evidence rule 2 in the other direction:** a claim was made once from a stale reading, four
documents inherited it by citation rather than by opening the file, and it hardened into an open
question with a hold attached. It cost nothing to check.

---

## What a critic should concede

Specific, cited, and each one is settled as far as I can tell.

1. **`tree-plan` §2's flatness proof is exact and general, not a coincidence at ten tiers.**
   `W(T)/req(T) = [b·T(T+1)/2] / [k·T(T+1)/2] = b/k` — the index cancels, so extending the ladder
   never breaks it (`:116-126`). The equal-value identity is the same shape: `Σ_{t≤T} t` **is**
   `T_tri` by definition, so `Σ tierBudget[t] = B_b` for any width vector and any tier count
   (`:186-203`). Both are asserted as identities over generated inputs, not as instances
   (`:702`).

2. **The G9 correction is right and was found here, not inherited.** Doc 02's
   `nodesPerTree = 2 × nodesPerBranch + 1` is always odd and gives 41 against D29's 40. The spec
   removes the shared root with three independent reasons — it has no budget and no branch to draw one
   from, it gates nothing because reachability is `req(t)` and not edges, and it forces `G1` and `G6`
   to contradict each other (`:59-70`). The result is even by construction.

3. **`tree-binder` found a defect the research had understated by 3.7×, by re-deriving instead of
   quoting.** Doc 04 measured a −17.0% tier-1 rounding error at seven tiers. At D29's ten tiers the
   same budget spreads over 220 weight units instead of 112, so *"the tier-1 error is +63%, not 17%"*
   — and tiers 1 and 2 both store `1`, making two adjacent tiers arithmetically indistinguishable
   (`spec-tree-binder.md:187-205`). *"Fixing tree size made this defect worse, and nothing in the
   program would have surfaced that without re-deriving."* That is the design gate working.

4. **`squad-harness` refuses to claim a result it cannot resolve.** It measures the noise floor from
   `Marginal.cs:21-23` rather than assuming one, computes the trial counts for 1.8/1.0/0.5pp, notices
   that doc 16's crossover is *"0.5pp or less — below the noise floor of a 3,000-trial run"*, and makes
   `transfers: false` mandatory whenever an ordering rests inside its own half-width (`:296-314`).
   It also uses common random numbers so the difference estimator is not √2 worse than each arm.

5. **The three-column transfer design is the right instrument.** A naive 1v1-vs-6v6 comparison
   confounds scope with engine; `duelClosedForm` / `duelTrials` / `squadTrials` separates them, and
   `duelClosedForm` is recomputed in process rather than read from the checked-in artifact (`:182-194`).

6. **`squad-harness` corrected two claims by opening the files.** Reflect's production caller is the
   injector's, not Battle's (`:342`); and doc 05 §6.4 step 3's `TerminationGuard.ToActor` visibility
   blocker does not apply, because the trial path builds a `BattleActorSetup` from public
   `AptitudeResolver.ResolveForBattle` and never constructs a `Predictor.Actor` (`:358-363`). That
   removes the only flagged blocker on the module's first stage.

7. **`tree-review` states its population, its claim and its confidence together, and refuses the
   overclaim by name.** *"Say this in the acceptance record, in these words: 'Every tree was judged.
   Individual nodes carry the machine's gates plus a sampled human rate with a ±5% margin.' Do **not**
   write 'the catalog was reviewed' unqualified"* (`:111-113`). It also states the cluster design
   effect against itself (`:172-183`) and adopts `FAIL` beats `NOT_MEASURED` (`:413`).

8. **The `_`-prefix blind spot is closed with a metric defined by not having an exclusion.**
   `PassiveTree/HiddenFileCount` walks every seed root without the skip that hid the stale
   `SnorkleZombie` from the tool that then reported "840 indexed — clean"
   (`spec-tree-review.md:431-465`), and the lesson is generalised: *"A gate with an exclusion rule has
   a blind spot the size of that rule."*

9. **The no-numbers fence is enforced by shipped code, not by policy.** `MAGNITUDE_DENY_NAMES`
   already contains `tier`, `duration`, `chance`, `weight` and anything ending `Milli`, and
   `audit_schema` raises from `Pipeline.__post_init__` — so a schema with a numeric field fails at
   construction, before a model call. `spec-tree-language.md:89-90` draws the right conclusion:
   *"'the language stage can never move a balance number' is not a policy in this document. It is an
   unsampleable state."*

10. **The quota is a count, not a second budget, and the reason is stated.**
    *"If you make them two budgets you owe an exchange rate. A quota owes nothing"*
    (`spec-tree-plan.md:331`). Given that `CostFunction` is knowingly wrong on multiplicative pairs
    and E10's marginal read is deferred, that is the correct call.

11. **`tree-resolve` makes `F`'s provisionality a code requirement.** `Fmax = 1000‰` must be a legal,
    tested configuration that removes `F` from the arithmetic without removing a path, *"so if `F` is
    withdrawn later it is a tuning change, not a refactor"* (`:284-286`). It also proves
    `F ∈ [1, Fmax]` from the Herfindahl bounds rather than asserting it, and shows `F` cannot violate
    the linear-contest theorem because it is `Θ`-invariant (`:229-233`, `:266-272`).

12. **D8's exploit is not hidden.** `spec-tree-resolve.md:235-258` states the dominance argument in
    full — self-spend one tree for `F = Fmax`, take all breadth from gear, aptitude thresholds and
    demon aspect — notes that the amendment named only gear, and defines the `selfSpent` projection as
    an owner ruling rather than an implementation detail, with a test asserting the exclusion is a
    stated rule.

13. **`tree-state`'s sparse-storage and derive-on-read design is proved, not asserted.**
    `cumulative(N)` has no order term, so a set's cost is purchase-order independent — which is what
    makes derive-on-read agree with pay-as-you-go and removes any stored price to refund at
    (`:118-125`). PS-8 is satisfied *provably*: `N(Θ)` is strictly increasing and unbounded, so every
    node is reachable at some finite Θ (`:154-160`), with the three things that would void the proof
    listed and forbidden.

14. **`species-tree` reads the corpus rather than the ideal, and corrects both.** 840 species, not
    841 (`spec-tree-review.md:42-53`, with §9's skew table shifted by two cells); `DemonType` is wired
    end to end so two `AllocationScope`s are reached, not one (`spec-species-tree.md:565-577`). It
    also decouples the thematic favour from the mechanical lock, which is the ideal §9 corollary that
    keeps flavour honest without turning "plants are earthy" into "everyone plays earth".

15. **Machine time and human time are both costed from measured rates in this repo.** Three real runs
    bracket the species wall clock at 49–91 hours (`spec-species-tree.md:328-334`), and the human
    figure is stated with its sensitivity — 15 s / 30 s / 60 s per node — rather than dressed up as
    evidence (`spec-tree-review.md:61-69`).

---

## Six areas, plus success criteria and open questions

All eleven carry all eight sections. Ratings below are about substance, not presence.

| Spec | Obj | Cmds | Structure | Style | **Testing** | Bounds | Success | Open Qs |
|---|---|---|---|---|---|---|---|---|
| `tree-plan` | ✅ | ✅ | ✅ | ✅ | ✅ **strongest** — 22 tests, properties over generated inputs | ✅ | ✅ | ✅ 2, both real |
| `squad-harness` | ✅ | ✅ | ✅ | ✅ | ✅ determinism asserted three ways; golden policy argued | ✅ | ✅ | ✅ 3 |
| `tree-binder` | ✅ | ✅ | ✅ | ✅ | ✅ incl. `per_mille_would_break_tier_one` as documentation | ✅ | ✅ | ✅ |
| `tree-catalog` | ✅ | ✅ | ✅ | ✅ | ✅ 18 tests, several by reflection | ✅ | ✅ | ✅ 3 |
| `tree-state` | ✅ | ✅ | ✅ | ✅ | ✅ 23 tests + a named mutation set | ✅ | ✅ | ✅ 3 |
| `tree-review` | ✅ | ✅ | ✅ | ✅ | ✅ adversarial fixtures by design | ✅ | ⚠ two process criteria (below) | ✅ 4 |
| `tree-surface` | ✅ | ✅ | ✅ | ✅ | ✅ 28 tests, all mechanical | ✅ | ✅ | ✅ 3 |
| `species-tree` | ✅ | ✅ | ✅ | ✅ | ✅ 23 tests over injected defects | ✅ | ✅ | ✅ 3 |
| `tree-language` | ✅ | ✅ | ✅ | ⚠ one function | ✅ 16 tests | ✅ | ✅ | ✅ 2 |
| `tree-resolve` | ✅ | ✅ | ✅ | ✅ | ⚠ **thinnest of the eleven** | ✅ | ✅ | ✅ 3 |
| `mechanism-wiring` | ✅ | ✅ | ✅ | ✅ | ✅ incl. the falsifier arm | ✅ | ⚠ A10 (below) | ✅ 2 |

**Testing strategy is the strongest area across the set, not the weakest** — which is unusual and
worth saying. Three habits carry it: falsifier arms (`spec-mechanism-wiring.md:514-516` — run the
same fixture against the three shipped subsystems and assert the channel does *not* move, *"without
that arm, a green test proves the fixture, not the fix"*); property tests over generated inputs
rather than instances (`spec-tree-plan.md:702`); and adversarial fixtures with injected defects
(`spec-tree-review.md:609-611`, `spec-species-tree.md:509-511`).

**The one that is thin: `tree-resolve`.** It is the only module that multiplies by `P(Θ)`, it owns
`F`, `H`, the tier gate, cross-unlock and the soul→Θ read — and its §12 is the shortest testing
section in the set. Two properties it argues carefully in prose have no named test: `F ∈ [1, Fmax]`
proved from the Herfindahl bounds (`:229-233`), and the `Θ`-invariance argument that keeps `F` inside
the linear-contest theorem (`:266-272`). Both are exactly the kind of claim that is true when written
and false after one refactor.

### Success criteria that are not testable

Three, out of roughly ninety.

1. **`mechanism-wiring` A10** (`:712`) — *"Erosion is measurable. A node applying a flat subtraction
   across the defensive vector produces a **different** win share for a spread defender than for a
   corner defender."* No magnitude, no half-width, no direction. Against a harness with a measured
   0.9pp noise floor at 3,000 trials, "different" is unfalsifiable: any two cells differ. **This is
   the criterion the spec itself calls the one that matters.** It needs the shape `squad-harness`
   already uses: a stated effect size, a stated confidence, and a refusal when the gap sits inside its
   own half-width.

2. **`tree-review`** (`:651`) — *"The corpus sheet's name-token panel is **read** before any census
   begins."* An assertion about a person, not about an artifact. It is a good practice and it belongs
   in the protocol; it is not a criterion a run can be judged against.

3. **`tree-review`** (`:646`) — *"`PassiveTree/HiddenFileCount` is green, **and it is green because
   the files are empty — not because nobody looked**."* The first clause is testable and has a test.
   The second is the interesting half and has no mechanism. It could get one: assert the metric
   visited a known-parked fixture in the same run.

Everything else resolves to a run, a hash, a count, a reflection scan, a grep test, or a rendered DOM
query. `tree-plan`'s and `tree-state`'s criteria are the model — *"proven by a test that counts rows
after a realistic build"*, *"asserted as exact integer equality, never a tolerance"*.

### Open questions

The set is disciplined here, and it is worth recording because it is the failure mode this repo has
named. Nobody manufactured uncertainty to fill a slot. Three specs open their section by saying so
explicitly (`spec-tree-language.md:551-552`, `spec-species-tree.md:556-558`,
`spec-tree-plan.md:1062`), and `tree-plan` goes further by listing the four items that are **not**
open — tasks with owners — so they are not mistaken for questions. Every open question I checked is
either an owner decision the module may not make (which resource respec costs; whether `nullification`
exists at all; what goes in `legitimateSkew`) or a measurement nobody has taken.

The one gap in that discipline is the reverse of the usual one: **D15 is a genuine open question that
is filed as an optional mode** (`spec-squad-harness.md:649-654`) rather than as an open question of
the module whose headline claim it undercuts.

---

## If I could make only one change

**Gate `tree-language --write` on `mechanism-wiring`'s A10, and say so in the map's build order.**

Not `tree-plan --emit`. The plan is cheap, reversible and mints no content: node ids are positional,
so revising `mechNodes[]` moves cells and text, never ids. Emitting it against an unvalidated
mechanism premise costs nothing.

The expensive, irreversible step is the one after it. `tree-language --write` is ~4,680 model calls
for the generic corpus and ~105,840 for species, and it produces the artifact a human then spends
34 hours per pass reviewing. Committing that against a premise nobody has measured is how a program
buys 35,160 nodes and finds out in wave 3 that the deep tiers do nothing.

A10 is already written, already scoped, and already named by its own spec as the acceptance test for
the whole design (`spec-mechanism-wiring.md:714-716`). All that is missing is an arrow. Give it an
effect size and a half-width so it can pass or fail, land G1 and G3, run it, and *then* spend the
calls. Everything else in this report is a paragraph, a test, or a re-read.

---

## Citations worth fixing while the specs are still text

Cheap, and each one is a claim a later session will inherit by citation rather than by opening the
file.

| Where | Says | Actually |
|---|---|---|
| `spec-mechanism-wiring.md:54` | Retaliation *"Already live (`EffectRuntime.cs:491`)"* | That line is ShieldGate wiring in the injector. Reflect is `CombatDamageDispatcher.cs:85`, unreachable from Battle |
| `spec-tree-state.md:523`, `spec-mechanism-wiring.md:766` | `RespecPolicy.PriceOf` returns Hunger, zero callers | Returns `RespecResource.Soul` (`:45`); one production caller, `RpgStore.SpeciesRespec.cs:154` |
| `spec-tree-catalog.md:113`, `spec-species-tree.md:393` | tier-1 rounding error ~17% | `spec-tree-binder.md:189` re-derived it at D29's ten tiers: **+63%**. Two of three specs carry the stale figure the third corrected |
| `spec-tree-language.md:144`, `:504` | `N := 1,560` as a literal | `spec-tree-plan.md:530` derives it, and `:766` forbids the literal |

---

## Related

- [`passive-tree-ideal.md`](../../architecture/passive-tree-ideal.md) · [`passive-tree-map.md`](../../architecture/passive-tree-map.md)
- [11-adversarial-debate.md](11-adversarial-debate.md) — T3 and T6b are the two theses this pass carries forward
- [13-review-pipeline.md](13-review-pipeline.md) — the change-class table §5 tests
- [15-dependency-map.md](15-dependency-map.md) — the build order §1 compares against
- [16-depth-exhaustion.md](16-depth-exhaustion.md) — the Θ ≈ 300 bound §2 checks the specs against
