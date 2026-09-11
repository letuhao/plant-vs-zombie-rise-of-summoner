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

## 1. The measured baseline (2026-09-11)

Commands actually run, not quoted:

```powershell
dotnet run --project tools/TreeBinder -- --check      # crashes
python -m seedsmith check --family PassiveTree        # corpus-side metrics
```

| Measure | Value |
|---|---|
| Trees in the committed corpus | 42 |
| Trees whose binder verdict is `Fail` | **42** |
| Nodes bound / refused | **266 / 1,414** (of 1,680) |
| Bound nodes carrying **zero priced atoms** | **108 / 266** |
| Priced atoms, by kind | `stat.modify` 206 — **no `stat.derived` at all** |
| Refusal buckets | `affix id not generated` **1,356** · `op 'more'` **54** · other 4 |
| Pickable affix-family ids | 125 |
| Families with generated atom rows | 26 |
| Pickable ids with **no** generated atom | **99** |
| Generated families using unsupported `op:"more"` | 6 |
| Generated families with object-shaped `channel` | 7 |
| Live-boot result (2026-09-07, for continuity) | `imported the passive-tree catalog — 42 tree(s)` |

**The load path is healthy. The generation seam is not.** A catalog of 266 inert nodes imports
cleanly, which is exactly why this went unnoticed: nothing refused, nothing threw, nothing changed a
number.

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

---

## 3. Fix order (this is the whole method)

Per the repair skill: **pipeline first → regenerate evidence → re-audit.** A change set that only
edits `data/seed/**` or only re-runs generation is not a repair.

```
Phase 0  reproduce + freeze the baseline        (no code change)
Phase 1  vocabulary alignment                   R1          ← removes 96% of refusals
Phase 2  binder robustness + kind parity        R2, R3, R4  ← "binds" becomes "works"
Phase 3  mechanism carriage                     R5          ← deep tiers stop being dead
Phase 4  resolve proof (lawn + battle parity)   acceptance
Phase 5  regenerate the corpus + re-audit       R6
Phase 6  balance measurement (squad-harness S4) the actual balance goal
```

**Phase 6 is the point of the program, but it is last on purpose.** Measuring balance against a
corpus that binds nothing measures nothing. `passive-tree-plan.md`'s F7/F8 already found the
squad-harness model is not representative of the direct-channel pipeline; that reconciliation cannot
be re-run honestly until Phase 4 is green.

---

## 4. Acceptance gates

Each phase has a machine-checkable gate. A phase is not done because its tasks are checked — it is
done when its gate passes on real data.

| Gate | Passes when |
|---|---|
| **G0 — baseline frozen** | `tools/TreeBinder --check`'s crash and the 266/1,414 census are recorded in the todo, and the exact commands are in the evidence paragraph |
| **G1 — vocabulary closed** | Every id in a node's permitted `affixIds` enum resolves to a generated `AtomRow`; a test asserts `permitted_for_branch(b) ⊆ resolvable_family_ids` for all three tags; the 1,356 `does not exist` refusals drop to **0** |
| **G2 — binder runs to completion** | `tools/TreeBinder --check` exits 0 or 1 (never a crash) on all 42 trees; a pool-shaped channel is either resolved to a concrete channel or refused as a `BindRefusal` naming the rule |
| **G3 — kind parity** | For every bound node, the emitted `NodeAtom.KindId` is a kind `TreeAtomSource.BoundAtomsFor` actually reads; a test proves a bound magnitude node moves a real `combat.*` channel through the shipped fan-in (the B6 `TreeFanInTests` shape) |
| **G4 — mechanism carriage** | A mechanism-class node's atom survives the binder and reaches `NodeAtom`; `PassiveTree/MechanismRamp` still matches `archetypes[].mechNodes[t]`; a deep-tier mechanism node is proven live in the resolver |
| **G5 — resolve parity** | One owned, gate-open node changes a number on the lawn **and** in battle, byte-identical totals, through no new subsystem (spec-tree-resolve §12 test 15) |
| **G6 — corpus regenerated** | `data/generated/passive-tree/*.json` is byte-reproducible from `--check`; `check --family PassiveTree` runs with real wired data, not `NOT_MEASURED` |
| **G7 — balance measured** | `squad-harness` S4 re-run against the real direct-channel model; `F ∈ [1, Fmax]`; no tree is OP; the `ExclusionRate`/`NearDuplicate` GAP findings from the parent plan are either closed or explicitly deferred with a named owner |

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

1. **Which kind does a tree node target?** `stat.derived` only (the plan's default, matching the
   resolve spec and the shipped fan-in) or primary `stat.modify` too (needs a new producer). R4.
2. **Add `More` to `NodeAtomOp`, or exclude `more`-op families from the tree vocabulary?** R2. Default:
   exclude first, decide on evidence.
3. **Mechanism carriage shape** — does a mechanism node carry its atom with `kMicro = 0` and a real
   `kindId`, or a new field? R5.
4. **The cross-program `tier-bands.v1.json` pricing gap** (parent plan D2) — still the disclosed cap
   on full binding, and not this program's to resolve unilaterally.

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
