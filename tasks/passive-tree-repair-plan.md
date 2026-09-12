# Repair plan — passive tree (the corpus is unplayable)

**Program:** `passive-tree-repair`. Capability map: [docs/architecture/passive-tree-map.md](../docs/architecture/passive-tree-map.md).
Parent plan: [passive-tree-plan.md](passive-tree-plan.md). Task list: [passive-tree-repair-todo.md](passive-tree-repair-todo.md).
Workflow skill: [.agents/skills/seedsmith-passivetree-repair/SKILL.md](../.agents/skills/seedsmith-passivetree-repair/SKILL.md).

**Status:** opened 2026-09-11 after a measured baseline. Not a new feature program — a repair of the
`tree-language → tree-binder → tree-resolve` seam, which today produces a corpus that **binds almost
nothing and, where it binds, contributes nothing to combat.**

**Read first, this session, before touching anything:** [DESIGN-GATE.md](../docs/DESIGN-GATE.md) §1 row
*Stats* and row *Power / scaling*; [spec-tree-binder.md](../docs/architecture/passive-tree/spec-tree-binder.md) §3–§4;
[spec-tree-language.md](../docs/architecture/passive-tree/spec-tree-language.md) §3, §4.2, §5.1;
[spec-tree-resolve.md](../docs/architecture/passive-tree/spec-tree-resolve.md) §2;
[spec-tree-catalog.md](../docs/architecture/passive-tree/spec-tree-catalog.md) §2.3;
[ssot-power-scale.md](../docs/architecture/power/ssot-power-scale.md) §10.2.

---

## 1. The measured distribution (2026-09-12)

Commands actually run, not quoted:

```powershell
# the one command every later phase diffs against (task P0.1)
$env:PYTHONPATH="tools/seedsmith"; python -m seedsmith trees census          # frozen baseline below
$env:PYTHONPATH="tools/seedsmith"; python -m seedsmith trees census --json   # same numbers, machine-readable
dotnet run --project tools/TreeBinder -- --check      # crash, exit -532462766
dotnet run --project tools/FamilyExpandGen -- --check # 3 stale files, exit 1
python -m seedsmith check --family PassiveTree        # corpus-side metrics
```

**Frozen baseline revision:** `0dc1bf3f` (the commit this census first ran at). The numbers below are
a READING of the corpus at that revision — never a constant a test may assert. Re-run `trees census`
to get the current reading; every phase reports its delta against this one.

> **Baseline (revision 0dc1bf3f)**
> ```
> trees=42  expected=1680  bound=266  refused=1414  unaccounted=0  overall=15.8%
> boundWithPricedAtoms=158  boundWithoutPricedAtoms=108  pricedAtoms=206  unspentBudgetShareMilli=75697
>
> by node class:  mechanism 18/840 (2.1%)   magnitude 248/840 (29.5%)
> by category:    elemental 15.4%  primary 19.1%  status 14.2%
> mechanism by tier:  t3 28→1  t4 84→2  t5 84→1  t6 84→0  t7 84→1  t8 140→3  t9 168→7  t10 168→3
> refusal buckets:    affixNotGenerated 1356  ·  opMore 54  ·  other 4
> orphans:            none (the 2026-09-11 "wither orphan" was a not-yet-generated plan node, class E)
> never-generated:    wither: skill.wither-def-t9-n1 (planned but never accepted)
> ```

### 1.1 Headline

| Measure | Value |
|---|---|
| Trees in the committed corpus | 42 generic (+5 species PoC, unplanned) |
| Trees whose binder verdict is `Fail` | **42 / 42** |
| Nodes bound / refused | **266 / 1,414** (of 1,680) = **15.8%** |
| Bound nodes carrying **zero priced atoms** | **108 / 266** (40.6% of bound) |
| Priced atoms, by kind | `stat.modify` **206** — `stat.derived` **0** |
| Priced atoms, by channel | `atk` **155** · `defense` **51** — nothing else |
| Total unspent budget share | **75,697** (of ~84,000) |
| Binder exit | **crash** at `AffixComposer.cs:69` |
| `FamilyExpandGen --check` | **stale**: 3 generated family files drift |
| Seed vintage | `mixed` 20 · `tree-language/1` 22 · `tree-language/2` 5 · **`/3` 0** |

**The load path is healthy. The generation seam is not.** A catalog of 266 inert nodes imports
cleanly, which is exactly why this went unnoticed: nothing refused, nothing threw, nothing changed a
number.

### 1.2 Bind rate by category — flat and low everywhere

| Category | Trees | Bound / expected | Bind rate |
|---|---|---|---|
| primary (aptitudes) | 12 | 92 / 480 | **19.2%** |
| elemental | 6 | 37 / 240 | **15.4%** |
| status | 24 | 137 / 960 | **14.3%** |

No category is healthier than another; the failure is systemic, not a bad subset.

### 1.3 Bind rate by node class — the mechanism collapse

| Class | Total | Bound | Refused | Bind rate | Bound with priced atoms |
|---|---|---|---|---|---|
| magnitude | 841 | 248 | 593 | **29.5%** | 147 |
| **mechanism** | **839** | **18** | **821** | **2.1%** | **11** |

**Every deep tier is mechanism.** Tier composition of the 1,679 generic seed nodes:

| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| mechanism % | 0 | 0 | 13 | 51 | 51 | 50 | 49 | **100** | **100** | **100** |

**Tiers 8–10 are 100% mechanism and bind at 2.1%.** The authored structure deliberately puts the
pay-off at depth (`passive-tree-ideal.md` §3.5: *a focus build cannot be rescued with MAGNITUDE, it
can only be rescued with MECHANISM*) and the pipeline deletes exactly that. A player who reaches tier
10 reaches an empty node.

### 1.4 Bind rate by tree — best is 42.5%, three trees bind nothing

| Worst trees (0%) | Bind rate | Best trees | Bind rate |
|---|---|---|---|
| `agility`, `leech`, `spark` | **0.0%** | `earth` | 42.5% |
| `cold`, `freeze` | 2.5% | `fortitude`, `jala` | 37.5% |
| `air`, `poison` | 5.0% | `bond`, `rally` | 32.5% |
| `hypno`, `ice`, `shatter` | 7.5% | `ferocity` | 30.0% |

`agility` is a **primary aptitude tree** and binds **zero nodes**. Unspent budget is 2,000‰ (fully
unspent) for `agility`, `spark`, `leech`.

### 1.5 Vocabulary coverage — 75 of 101 chosen affixes cannot resolve

| Measure | Value |
|---|---|
| Distinct affix ids chosen by the language stage | 101 |
| Resolvable (a generated atom exists) | **26** |
| Unresolvable | **75** |
| Generated families | 30 (19 `stat.modify` + 7 `stat.derived` + drift) |
| Pickable families across all authored files | 125 |

Concentration of picks: Herfindahl **0.027** (effective distinct ≈ **37**), top-10 affixes =
**42.8%** of all picks. The language stage is choosing from a vocabulary the binder cannot see.

### 1.6 The upstream truth: `FamilyExpandGen` refuses 94 of 125 families

The binder's 1,356 `does not exist` refusals are a *symptom*. The cause is that the item-side
expander refuses 94 families before atoms ever exist:

| Expansion refusal class | Count | What it means |
|---|---|---|
| **A** — `no supported tier-magnitude formula` (mechanism/no-op kinds) | **40** | `status.apply`, `resource.delta`, `resource.economy`, `spawn.entity`, `board.action`, `shield.grant`, `status.clear`, `grid.*`, `box.set` — the expander only implements primary/flat-derived magnitude formulas |
| **B** — `no referenceBaseGameUnits` for channel | **20** | no `BattleRuleset`/power-scale curve published for `combat.accuracy.*`, `combat.dodge.*`, `combat.crit.*`, `combat.shield.*`, `arm1Max`, `arm2Max`, `attackInterval`, `produceInterval`, `status.power`, `status.resist` |
| **C** — `no opWeightPermille entry` | **19** structural (`Replace` 12, `Flag` 7) + **11** authored (`add` 7, `doom`/`cherry`/`freeze`/`fireline` 4) | `Replace`/`Flag` carry no tier-band magnitude by `bands.v1.json`'s own words — a structural gap; `add`/`doom`/… are op strings that are not ops at all |
| **D** — `no matching E30 channel pool` | **4** | `combat.power.pierce.{variant}`, `combat.power.overflow.{variant}` — not registered channel families |

`bands.v1.json` is **frozen** and already specifies the status/magnitude formulas
(`statusMagnitudeAndDuration`), so class A is a **generator gap**, not missing design data.

### 1.7 Newly found defects

- **Corpus drift (class D).** `family-expand.g-armour.json`, `.g-precision.json`, `.g-tempo.json` are
  stale against their source families — `FamilyExpandGen --check` exits 1. The committed atom corpus
  does not match the committed family corpus.
- **Not-yet-generated plan node (class E, not a defect).** `skill.wither-def-t9-n1` is in
  `wither`'s plan but in no seed document (39 of 40). The 2026-09-12 pass measured the whole corpus in
  both directions and found **zero** true orphans (no generated id outside a plan) and exactly this
  **one** ungenerated node. The binder correctly refuses it (`affixIds must be 1..3, got 0`); the
  census now reports it separately as `never_generated_node_ids`, and the 2026-09-11 baseline's
  "orphan" label was wrong.
- **Zero v3 seeds.** `PROMPT_VERSION` is `tree-language/3`, but no seed document carries it — 20
  `mixed`, 22 `/1`, 5 `/2`. The provenance fix from the prior session has never been exercised on the
  corpus.

---

## 2. Root causes, classified

Classification follows the repair skill's layering table. **Class A/B defects are fixed in code; seed
JSON is never hand-edited.**

### R1 — Class A: the language vocabulary is disjoint from the binder vocabulary

`vocab.py:84-94` (`permitted_for_branch`) returns every affix family tagged `offensive`/`defensive`/
`utility` — 125 ids. `AffixComposer.Resolve` (`AffixComposer.cs:26-53`) resolves only families that
have a generated `AtomRow` under `data/seed/atoms/generated/family-expand.*.json` — **26** families
across 7 files. **99 pickable ids resolve to nothing**, and they are 96% of all refusals.

The module's own docstring already names this blocker: `vocab.py:17-25` — *"a predicate can key on
`posture` and nothing else … the day the [atom-tag registry] lands, this function's body is the one
place that gets narrower."* **That registry was never built.**

**Fix surface:** `tools/seedsmith/seedsmith/adapters/trees/nodegen/vocab.py` and `quota.py`. The
permitted `affixIds` enum must be restricted to affixes whose own bound atoms the binder can resolve
and price. This is the single highest-leverage change in the program: it removes 1,356 of 1,414
refusals.

### R2 — Class C: `NodeAtomOp` cannot express `More`, which `stat.modify` legitimately uses

`NodeAtom.cs:9-15` declares `Flat | Increased | Replace | Flag`. `AtomKindRegistry.cs:517` states
`stat.modify` ops are **`Flat|Increased|More`**. Six real generated families carry `op:"more"`
(`atom.life-surge`, `atom.savagery`, `atom.arm-hardening`, `atom.tempo-stampede`, `atom.bulwark`,
`atom.tempo-wildgrowth`), so `Enum.TryParse<NodeAtomOp>` throws at `TreeBinderRun.cs:84`.

The catalog's own comment (`NodeAtom.cs:5-8`) claims it matches `AtomRowValidator.DerivedOps` exactly —
but the binder prices **primary** (`stat.modify`) atoms through the same enum, where `More` is legal.
The enum is right for the derived side and wrong for the primary side.

**Fix surface:** `src/FusionRpg.Core/PassiveTree/Catalog/NodeAtom.cs` (+ `TreeBinderRun.cs`,
`TreeAtomSource.cs`, `TreeBinderExplain.cs`, `PassiveTreeCatalogLoader.cs`). **Ask first** — it widens
a shipped catalog contract, and `More` has no derived-side reader (`AtomDerivedSubsystem.TryParseOp`).
Either add `More` for the primary path, or narrow the tree vocabulary to `more`-free families. The
choice belongs to the owner; the plan's default is *narrow the vocabulary first* (R1), *then* decide
`More` on evidence.

### R3 — Class B: the binder crashes on pool-shaped channels

`AffixComposer.ParseAtom` (`AffixComposer.cs:67-69`) does `root.GetProperty("channel").GetString()`.
For 7 families (`atom.evd-*`, `atom.shld-*`, `atom.ward-*`) the authored `channel` is an **object**,
`{"pool": "...", "count": 1, "allowRepeat": false}` — an E30 pool reference, not a channel id.
`GetString()` on a JSON object throws `InvalidOperationException`, which is **not** a `BindRefusal`,
so `BindTree`'s catch (`TreeBinderRun.cs:112`) does not catch it and the whole run dies.

Reproduced 2026-09-12: exit `-532462766`, `AffixComposer.cs:69` → `TreeBinderRun.cs:110` →
`Program.cs:115`. The run dies on the **first** pool-shaped family, so the 266/1,414 census in §1.1
is itself the output of an older, non-crashing run.

**Fix surface:** `AffixComposer.cs`. A pool must be resolved to a concrete channel deterministically,
or refused cleanly — never crash. This is why the committed `data/generated/` cannot be reproduced
today (R6).

### R4 — Class B: the binder prices `stat.modify`; the resolver reads only `stat.derived`

`TreeBinderRun.BindNode` prices a resolved atom whenever `ChannelUnits.For` returns a `GameUnits`
family — which includes the **primary** channels `atk`, `defense`, `maxHp`, `hp`, `arm*`
(`ChannelUnits.cs:18-31`). It then hardcodes `AttachPoint.Stat` and emits a `NodeAtom` with the atom's
real `kindId` (`stat.modify` for `atom.might`).

`TreeAtomSource.BoundAtomsFor` (`TreeAtomSource.cs:66-71`) then skips **everything that is not
`stat.derived`**. So all 206 priced `stat.modify` atoms are dropped, and every "bound" magnitude node
contributes exactly zero.

**This is the defect that makes the tree unplayable**, and it is a spec-vs-code gap:
`spec-tree-binder.md` §4.1 lists primary channels as legal binder targets, while
`spec-tree-resolve.md` §2.1 routes tree power through `stat.derived` only. Both cannot be true.

**Fix surface:** decide the one kind (the plan's recommendation: `stat.derived`, matching the resolve
spec's worked example `combat.power.fire` and the shipped `AtomDerivedSubsystem` fan-in), then make
`TreeBinderRun` emit only that kind — or add a primary-channel producer if the owner chooses the other
direction. **Ask first:** it changes which channels a tree may target.

### R5 — Class B: mechanism atoms are never carried, so deep tiers are inert

108 of 266 bound nodes have **zero priced atoms**: their affixes are `status.apply` (mechanism) or
non-ladder channels, which `BindNode` skips at `TreeBinderRun.cs:49-61`. The ideal §3.5 constraint is
explicit — *"a focus build cannot be rescued with MAGNITUDE; it can only be rescued with MECHANISM"* —
and deep tiers are supposed to be mechanism-heavy (`archetypes[].mechNodes[]`). Today no mechanism
atom survives the binder at all.

**Fix surface:** `TreeBinderRun.cs` + the `mechanism-wiring` seam. A mechanism node must be carried
into `NodeAtom` even when it has no ladder-scaled amount, and `TreeAtomSource` must not drop it.

### R6 — Class D: the committed generated corpus is stale and not reproducible

`data/generated/passive-tree/*.json` is tracked (42 files). It was written before R3's crash path was
reachable, and `--check` cannot reproduce it now. **Regeneration is required after R1–R5, and only
after.**

Additional evidence measured 2026-09-12: `skill.wither-def-t9-n1` is in the generated tree but in no
seed document, and `family-expand.g-armour/.g-precision/.g-tempo.json` drift from their sources —
three independent signs that the committed corpus predates its inputs.

### R7 — Class A: the item-side expander refuses every non-magnitude kind

`FamilyExpansion.Expand` (`FamilyExpansion.cs:253-284`, `TryReferenceBaseM1`) implements **only** the
`primaryChannel`/`flatDerivedChannel` formulas: `Flat` reads a `BattleRuleset` base, `Increased`/`More`
read the identity ratio, and **everything else is refused** with
*"op '{op}' has no supported tier-magnitude formula … Replace/Flag carry no tier-band magnitude"*.
The kind is never consulted, so `status.apply`, `resource.delta`, `resource.economy`, `spawn.entity`,
`board.action`, `shield.grant`, `status.clear` and the `grid.*`/`box.set` kinds all fall into the same
refusal — **40 families**, and they are the mechanism content tiers 8–10 are made of.

`bands.v1.json` already specifies the missing formulas (`statusMagnitudeAndDuration`: chance at
`r = 1.75`, duration at `r = 1.4`, mandatory; `familiesOutOfFourWaySplit` resolves the remaining kinds
by analogy). The registry is **frozen v1**, so this is a **generator gap** (class A), not missing
design data and not a registry change.

**This is why R1's symptom and R5's dead deep tiers both exist.** Aligning `vocab.py` to the binder
would bind `status`/`mechanism` families to **nothing**, because no atom row was ever generated for
them. The real fix is upstream: expand non-magnitude kinds, publish the missing channel curves, and
only then narrow the vocabulary.

**Fix surface:** `FamilyExpansion.cs` (+ `FamilyExpansionTypes.cs`, `TierBandsFile.cs`), the tier-bands
and power-scale balance surfaces, and `tools/FamilyExpandGen`. **Ask first:** publishing new channel
curves and `opWeightPermille` rows is a balance-surface change with its own reviewed owner.

### R8 — Class C: `Replace`/`Flag` have no defined tier magnitude anywhere

19 expanded-families are refused because their op is `Replace` (12) or `Flag` (7), and
`bands.v1.json` states these *"carry no tier-band magnitude at all and are out of powerBand's scope
(structural, not scaled)"*. That is a coherent design position — but it leaves the tree with no
defined behavior for those families, and the current code expresses "no magnitude" as a **refusal**
rather than a structural flag. Either a structural op is bindable with no magnitude (the mechanism
path, R5), or those families are excluded from the tree vocabulary. **Ask first.**

### R9 — Class A: four authored `params.op` strings are not ops

`atom.cherry-bloom` (`op:"cherry"`), `atom.firelining` (`op:"fireline"`), `atom.flash-freeze`
(`op:"freeze"`), `atom.dooming` (`op:"doom"`) are `board.action` families whose `op` names **which
board action to take**, not a `Flat|Increased|More|Replace|Flag` modifier. `atom.midas` and the other
`resource.economy` families use `op:"add"`, which is a verb, not a modifier op. The expander reports
them as missing `opWeightPermille` entries, which is a misleading message for a field that means
something else on those kinds.

**Fix surface:** `FamilyExpansion` must read `params.op` only for kinds that own a modifier op, and
carry the board-action/economy verb through unchanged. `FamilyExpanentry`/`FamilyExpansionTypes`
should not conflate the two.

### R10 — Class B: 20 channels have no published curve, so Flat families refuse

`FlatReferenceBase` (`tools/FamilyExpandGen/Program.cs:135-137`) returns a base only for the two
channels `power-scale.v2.json` publishes (`atk`, `defense`). Every other Flat channel refuses:
`combat.accuracy.*`, `combat.dodge.*`, `combat.crit.rate/damage/resist*`, `combat.shield.*`,
`arm1Max`, `arm2Max`, `attackInterval`, `produceInterval`, `status.power`, `status.resist` — **20
families**. These are real, registered, player-facing channels; the curve is missing, not the channel.

**Ask first:** adding a curve to `power-scale.v{n}.json` is a power-ladder change owned by the
power program, and the file is versioned (publishing `v3` is the sanctioned path, never editing v2).

---

## 3. Fix order (this is the whole method)

Per the repair skill: **pipeline first → regenerate evidence → re-audit.** A change set that only
edits `data/seed/**` or only re-runs generation is not a repair.

```
Phase 0  reproduce + freeze the baseline          (no code change)
Phase 1  corpus drift + orphan repair             R6-drift, R9   ← the pipeline must be self-consistent
Phase 2  expander: non-magnitude kinds             R7, R8         ← the real upstream fix; makes mechanism atoms exist
Phase 3  expander: missing channel curves          R10            ← 20 more families become expandable
Phase 4  binder robustness + kind parity           R2, R3, R4     ← "binds" becomes "works"
Phase 5  vocabulary alignment (LAST, now safe)     R1             ← narrows to what actually resolves
Phase 6  mechanism carriage                        R5             ← deep tiers stop being dead
Phase 7  resolve proof (lawn + battle parity)      acceptance
Phase 8  regenerate the corpus + re-audit          R6
Phase 9  balance measurement (squad-harness S4)    the actual balance goal
```

**Phase 5 moved to after Phase 2–3, reversing the 2026-09-11 order.** Aligning `vocab.py` first would
have been a repair in appearance only: it removes the *symptom* (1,356 refusals) by making the
language stage stop asking for families the expander refuses — leaving the tree with 26 magnitude
families, no mechanism content, and dead tiers 8–10. The refusals are the honest signal. Fix the
expander, then narrow the vocabulary to the genuinely-resolvable set.

**Phase 9 is the point of the program, but it is last on purpose.** Measuring balance against a
corpus that binds nothing measures nothing. `passive-tree-plan.md`'s F7/F8 already found the
squad-harness model is not representative of the direct-channel pipeline; that reconciliation cannot
be re-run honestly until Phase 7 is green.

---

## 4. Acceptance gates

Each phase has a machine-checkable gate. A phase is not done because its tasks are checked — it is
done when its gate passes on real data.

| Gate | Passes when |
|---|---|
| **G0 — baseline frozen** | The §1 distribution table is reproducible from a committed command; the binder crash, the drift, and the orphan are each recorded with exact evidence |
| **G1 — corpus self-consistent** | `FamilyExpandGen --check` exits **0**; no generated node exists that is absent from its seed document; `PROMPT_VERSION` provenance is uniform and current |
| **G2 — mechanism atoms exist** | `status.apply`/`resource.delta`/`resource.economy`/`spawn.entity`/`board.action`/`shield.grant` families expand to real atom rows with their `bands.v1.json`-specified magnitudes; the 40 class-A refusals drop to **0** |
| **G3 — curves published** | Every registered channel a Flat family names has a published curve; the 20 class-B refusals drop to **0**; `power-scale` is a **new published version**, never an in-place edit |
| **G4 — binder runs to completion** | `tools/TreeBinder --check` exits 0 or 1 (never a crash) on all 42 trees; a pool-shaped channel is either resolved to a concrete channel or refused as a `BindRefusal` naming the rule |
| **G5 — kind parity** | For every bound node, the emitted `NodeAtom.KindId` is a kind `TreeAtomSource.BoundAtomsFor` actually reads; a test proves a bound magnitude node moves a real `combat.*` channel through the shipped fan-in (the B6 `TreeFanInTests` shape) |
| **G6 — vocabulary closed** | Every id in a node's permitted `affixIds` enum resolves to a generated `AtomRow`; a test asserts `permitted_for_branch(b) ⊆ resolvable_family_ids` for all three tags; the `does not exist` refusals drop to **0** *by expansion, not by exclusion* |
| **G7 — mechanism carriage** | A mechanism-class node's atom survives the binder and reaches `NodeAtom`; a deep-tier (8–10) mechanism node is proven live in the resolver; the bound-but-inert count is **0** |
| **G8 — resolve parity** | One owned, gate-open node changes a number on the lawn **and** in battle, byte-identical totals, through no new subsystem (spec-tree-resolve §12 test 15) |
| **G9 — corpus regenerated** | `data/generated/passive-tree/*.json` is byte-reproducible from `--check`; `check --family PassiveTree` runs with real wired data, not `NOT_MEASURED` |
| **G10 — bind rate by class** | Magnitude **and** mechanism node classes both bind at a high rate; the mechanism class is no longer the worst; no tree binds **0%** |
| **G11 — balance measured** | `squad-harness` S4 re-run against the real direct-channel model; `F ∈ [1, Fmax]`; no tree is OP; the `ExclusionRate`/`NearDuplicate` GAP findings from the parent plan are either closed or explicitly deferred with a named owner |

**G10 is the distribution-specific gate this program adds.** A repair that raises the aggregate bind
rate while leaving mechanism nodes at 2.1% and tiers 8–10 empty has not fixed the tree — it has fixed
the metric.

---

## 5. What this program will NOT do

- **Never hand-edit `data/seed/**` or `data/generated/**` to make a metric pass.** Generated data is
  evidence; the pipeline is the system.
- **Never regenerate as the sole response to an R1–R5 defect.** Regeneration is Phase 5 only, after
  the code fix.
- **Never weaken a threshold or delete a hard case** to get a green `check --family PassiveTree`.
- **Never convert `NOT_MEASURED` into `PASS`.**
- **Never widen the atom vocabulary** (attach points/kinds/triggers) — that is a reviewed
  `decisions.md` change, and none of R1–R5 needs one.
- **Never write to `tasks/plan.md`, `tasks/todo.md` or `SPEC.md`** — those belong to other streams.
- **Never touch `Battle*` files by line** — `battle-tempo` edits them; cite by symbol (R9).

## 6. Ask-first items (owner decisions this plan cannot make alone)

1. **Publish new channel curves (R10).** 20 families need a curve for `combat.accuracy.*`,
   `combat.dodge.*`, `combat.crit.*`, `combat.shield.*`, `arm1Max`, `arm2Max`, `attackInterval`,
   `produceInterval`, `status.power`, `status.resist`. Adding a curve is a power-ladder change owned
   by the power program, published as a **new version** (`power-scale.v3.json`), never an in-place
   edit of v2. This is the single largest unblock in the program.
2. **`Replace`/`Flag` tier semantics (R8).** A structural op carries no magnitude — either bind it as
   a mechanism with no amount, or exclude those 19 families from the tree vocabulary. `bands.v1.json`
   is frozen and says "out of scope", so this is a decision, not a derivation.
3. **Which kind does a tree node target?** The owner already answered *both, unified*: the repo has
   one battle engine, so a tree contributes through both the `stat.derived` fan-in and the
   `stat.modify` producer path, and the lawn and battle read modes are not split. **R4's fix is
   therefore: emit both kinds correctly and prove both reach the shared engine** — not "pick one".
4. **Add `More` to `NodeAtomOp`, or keep excluding `more`-op families?** R2. The owner's "everything,
   unified" direction favors adding `More` (it is legal for `stat.modify` and priced at 550‰), threaded
   through the resolve path. Confirm before widening the shipped enum.
5. **Mechanism-carriage shape (R7/R5)** — does a mechanism node carry its atom with `kMicro = 0` and a
   real `kindId`, or a new field? Needed once R7 makes mechanism atoms exist.
6. **The cross-program `tier-bands`/`power-scale` ownership** — publishing versions is the power
   program's call; this program requests them and consumes them, never edits them unilaterally.

---

## 7. Pre-proposal checklist (DESIGN-GATE §5)

```
[x] I identified the subsystem(s) this touches: tree-language, tree-binder, tree-resolve, tree-catalog.
[x] I read every doc in the §1 row(s) for those subsystems, this session.
[x] I checked decisions.md for a lock covering this (D22, D24, D40, D42, D53, D56 — via citing docs).
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — the binder was run and crashed.
[x] I read the surrounding section of every rule I quoted (§4.1 vs §4.2; §2.1 vs §3.5).
[x] I tested (not assumed) the constraint — ran the real binder and the corpus census.
[x] Nothing contradicts a §2 invariant: these are wiring/vocabulary gaps, not architectural changes.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks — this plan and its
    todo are the start; the specs' stale counts (16 kinds / 98 families / 21 statuses) still need a
    propagation pass, tracked as task P0.3.
[x] ActorHub rule: every fix contributes through the shipped `IActorStatSubsystem`/atom reader; no
    private fold is proposed.
```
