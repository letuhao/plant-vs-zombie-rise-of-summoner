# 17 — Spec coverage audit: do the eleven specs cover the idea?

**Status:** audit, 2026-09-05. Research only — no spec, no code and no data was changed by this pass.

**Question asked:** *"audit specs must coverage the idea, add missing/gap"*.

**Method.** Read → verify against code → report, per [DESIGN-GATE.md](../../DESIGN-GATE.md). Read in
full this session: `DESIGN-GATE.md`, `passive-tree-ideal.md` (D1–D36, §13, §14, §15),
`passive-tree-map.md`, all eleven `passive-tree/spec-*.md`, and research docs 06, 09, 10, 11, 15, 16.
Claims are marked **FACT** (read from code or a file this session), **INFERENCE** (derived, with the
derivation shown) or **RECALL**. Every code claim cites `file:line`.

**What this audit did not do:** run a suite. Nothing here needed one — every claim is a read. Where a
claim would need a run to settle, it says so.

**The headline.** The decision tables are close to right; the substance has three holes that matter,
and all three share one shape — a decision that is *named* in a table and *emitted as a string* in a
schema, with nothing downstream that turns the string into behaviour.

---

## 1. Coverage verdict

| Finding class | Verdict | Detail |
|---|---|---|
| **D1–D36 implemented by requirement text** | **31 of 36 clean** · 2 superseded correctly (D19, D31) · **3 with real holes** (D2, D11/D12, D35) | §3 |
| **Decisions silently dropped** | **3** — D2's aptitude-threshold and demon-aspect sources; D35's gate quantity | §2.2, §2.3 |
| **§13.2 wiring gaps owned** | 8 of 10 owned · 2 named as another program's (`AffixTags` call site, Battle's trigger set) | §4.1 |
| **§13.3 real gaps owned** | 4 of 6 owned or honestly declared · **1 orphan named by nobody** (layer denial / bypass) · 1 declared orphan (the 17th atom kind) | §4.2 |
| **§14 tunables named with unit and file** | 12 of 12 named · **2 carry conflicting names, units or files across specs** (`tierLadder.k`, `Ws`) | §5 |
| **§15 deferred questions kept deferred** | **No leaks.** Two declared narrowings await an owner ruling and say so | §6 |
| **Research findings that reached a spec** | The seven the brief named all landed. **4 substantive findings are stranded** | §7 |
| **Reverse coverage (scope creep)** | **Thin and honest.** No invented requirement traces to nothing | §8 |
| **The six map assumptions** | 5 honoured · assumption 6 honoured in form, violated in substance by the two key conflicts | §9 |
| **Cross-spec interface consistency** | **4 mismatches** — gate currency, `mechanismFloor`, the stage-1/stage-2 schema, the stage-3 tool name | §2.1, §2.4 |

---

## 2. Gaps, ranked by severity

### 2.1 ⛔ S1 — The tier gate reads two different currencies in two different specs

**FACT.** `spec-tree-plan.md:494`, under a heading calling it *"the easy mistake"*:

> *"`req(t)` gates on **aptitude points allocated to that tree's gate quantity**; nodes are bought with
> **skill points**. This module touches only the first."*

**FACT.** `spec-tree-resolve.md:141` states the opposite:

> *"**Rule: `tree-resolve` gates on ONE index — skill points spent in the tree.**"*

and `spec-tree-resolve.md:129` widens it further: *"the gate quantity is points spent in this tree,
**whatever their provenance**."*

`tree-state` sides with `tree-plan` — `spec-tree-state.md:144` describes its own coupling as *"reward
per **skill** point flat at every tier, exactly as D26 did for reward per **aptitude** point"*, i.e.
two currencies on two axes. So does the ideal: §4 says a tier opens on *"the actor's own base
allocation in that tree's gate quantity"*, and D12 says *"tier gates read base allocation."*

**Four things break if `tree-resolve`'s reading stands, and each is load-bearing:**

1. **D12 fails.** D12's justification (ideal §5) is *"an aptitude is a SOURCE, not a channel, so items
   cannot feed aptitude points."* That argument holds only while the gate reads aptitude points. If the
   gate reads skill points **and** D11 lets items grant skill points, item bonuses move the gate
   directly. `spec-tree-resolve.md:113-131` calls D12 *"true by construction"* twenty lines before it
   names the construction that breaks it.
2. **D26's flatness changes meaning.** `W(t)/req(t) = b/k` is reward per *aptitude* point —
   `spec-tree-plan.md:919` gives `ladder.kPoints` the unit *"aptitude points"*. `spec-tree-resolve.md:392`
   gives the same `k` the unit *"skill points"*. Under the second reading the gate and the unlock price
   are charged in one currency, and D25's escalation lands on top of D26's flatness rather than beside
   it.
3. **`tree-plan`'s depth arithmetic collapses.** `spec-tree-plan.md:131-141` sizes the ladder against
   `aptitudePoints(Θ) = 3·Θ` — 300 at Θ=100, tier 10 at `s = 1`, `designTarget.thetaAllIn = 92`. At the
   skill-point rate `tree-state` derives (11 per Θ, `spec-tree-state.md:352`) a Θ=100 actor holds 1,100,
   and `req(10) = 275` is cleared four times over. Every Θ figure in the program moves.
4. **D28's credit is undefined.** `credit(i) = max{ base(j) … }` (`spec-tree-resolve.md:155`) reads
   whichever quantity the gate reads. The sweep it cites (`tools/HybridViability/Program.cs:363-372`)
   ran on aptitude-point-equivalents.

**What to add, and where.** `spec-tree-resolve.md` §3.2/§3.3 and §8: state the gate quantity as
**aptitude points allocated to the tree's gate quantity**, matching `tree-plan` §7, the ideal §4 and
D12, and keep `grant.skillPointsPerThetaMilliByScope` where it belongs — as the *purchase* wallet
`tree-state` prices against. If the owner prefers the skill-point gate instead, D12 needs amending and
`tree-plan` §2's whole depth table needs recomputing. That is a decision, not an edit.

### 2.2 ⛔ S1 — 27 of the 39 generic trees have no gate quantity, and no module is scoped to build one

**FACT.** `spec-tree-plan.md:502-507` emits `gateQuantity` as an opaque id and marks two of four
categories unresolvable:

| Category | Trees | `gateQuantity` | State in code |
|---|---:|---|---|
| aptitude | 12 | `aptitude.<Id>@Commander` | shipped |
| element | 6 | `element_mastery.<id>@Aspect` | *"scope exists; source does not"* |
| status | 21 | `status_applied.<id>` (D35) | *"zero `src/` hits"* |
| demonFamily | 0 | `species_level@DemonType` | rate shipped, roster absent |

**FACT, grepped this session.** `status_applied` and `StatusApplied` return **zero** hits across `src/`.
`status_applied.<id>` appears exactly once in the whole spec set — `spec-tree-plan.md:506` — as a string
the plan emits and never resolves. No spec carries a requirement to count status applications, persist
that counter, or read it.

`spec-tree-resolve.md:145-146` states the consequence and accepts it: *"A tree whose gate quantity does
not exist yet is not blocked here — it resolves to zero points, which resolves to tier 0, which resolves
to no contribution. **Inert, not broken.**"*

**INFERENCE, arithmetic shown.** 21 status trees × 40 nodes + 6 elemental trees × 40 nodes = **1,080 of
the 1,560 generic nodes — 69% of the shared corpus** — ship generated, reviewed, committed, and
permanently at tier 0. `tree-language`'s 4,680 calls author them; `tree-review`'s census reviews them;
no player can reach one.

**This is the D35 hole in full.** D35 was adopted to remove the `AllocationScope` slot-5 dependency, and
it did that cleanly — `spec-tree-state.md:499-503` refuses a fifth enum member for the right reason
(`AptitudeAllocation.Total()` sums every member into the aptitude share denominator `decisions.md:103`
locks). But removing the dependency also removed the only place the counter was going to live, and
nothing replaced it. **The decision's table row is honoured; the decision's purpose is not.**

**What to add, and where.**

- `spec-tree-state.md` — a requirement owning `status_applied.<id>`: a per-`(scope, scope_key, statusId)`
  counter, incremented where statuses are applied, stored in the sparse shape §1.1 already argues for,
  read by `tree-resolve` as the gate index. `tree-state` is the module that already owns per-actor tree
  state, so it is the natural home.
- `passive-tree-map.md` — a blocked-on row naming `element_mastery`'s producer, which belongs to the
  aspect/element program, not to this one.
- `spec-tree-plan.md` — emit status and element trees behind the same `_pending` declaration it already
  uses for `demonFamilies`, so a category with no gate is visible in the plan rather than discovered at
  resolve time. Authoring 1,080 nodes for a gate that does not exist is the expensive half of this gap,
  and `_pending` is the cheap fix.

### 2.3 ⛔ S1 — D2 is one-quarter implemented, and D11's carrier does not exist

**D2:** *"All four acquisition sources: skill points · aptitude thresholds · items/affixes · demon
aspect."*

| Source | Requirement anywhere? |
|---|---|
| Skill points | ✅ `spec-tree-state.md` §3 — `skillPointsPerThetaMilliByScope`, the D34 table |
| Items / affixes | ⚠️ **rule stated in four specs, mechanism specified in none** |
| Aptitude thresholds | ⛔ **nothing.** Named once, as a term with no definition (`spec-tree-resolve.md:242`) |
| Demon aspect | ⛔ **nothing.** Same line, same status |

**FACT, grepped this session.** `skillPoint` / `SkillPoint` appears in exactly **two** places in all of
`src/`: `AptitudeTuning.cs:13` (the record field) and `:158` (the parse). There is no channel, no
`UnitClass`, no store column and no affix shape that can carry *"grants N skill points"*.
`spec-tree-binder.md:259` refuses `AptitudePoints` as a channel by construction and lists no skill-point
equivalent among the 13 `UnitClass` values.

Four specs nonetheless write requirements that assume the carrier exists:

- `spec-tree-state.md:133` prices *"a node unlocked with item-granted points, item still equipped"*
- `spec-tree-resolve.md:123-127` and test 18 — *"withdrawn points invalidate rather than repair"*
- `spec-tree-surface.md` §8 — *"needs 3 more points; the gear that gave them is off"*
- `spec-tree-catalog.md:53` — gear-granted points as one of the three homes of per-player variance

**INFERENCE.** D11's *"strictly cleaner — no special case to define, enforce or test"* argument is sound,
and that is exactly why nobody noticed: the rule is so clean that four specs consumed it without anyone
specifying what produces the point.

**What to add, and where.** `spec-tree-state.md` owes one short section: which affix or atom shape grants
skill points, where the grant is read (it must not be a stored balance — §2's derive-on-read contract
forbids one), and how *removal* is detected so `tree-surface` §8's red state has a trigger. If that shape
needs a new channel or a new atom kind, that is a reviewed `decisions.md` change and should be said now
rather than discovered at build time.

For the other two sources: either `spec-tree-state.md` gains a requirement, or the ideal records D2 as
descoped to two sources. `spec-tree-resolve.md:594` already says the right thing about the consequence —
*"if gear stays excluded, the rule must be written over all four D2 sources explicitly, because
'self-spent' has no defined meaning for a threshold grant"* — it simply has no counterpart that defines
the grants.

### 2.4 S2 — Four cross-spec interface mismatches

Each is a one-line fix now and a build-time surprise later.

| # | Mismatch | Evidence |
|---|---|---|
| **a** | **`tierLadder.k` is one number under three names in two files.** `ladder.kPoints`, unit *aptitude points*, in `passive-tree-gen.v1.json` (`spec-tree-plan.md:919`) · `tierLadder.reqScalePoints`, unit *skill points*, in `passive-tree.v1.json` (`spec-tree-resolve.md:392`) · `tierLadder.k` in `passive-tree.v1.json` (ideal §14, `spec-tree-catalog.md:243`, `spec-squad-harness.md:223`). `spec-tree-plan.md:965-968` lists the keys it deliberately does not duplicate, and `k` is not on that list — because it believes it owns it | grep, this session |
| **b** | **`Ws` is one number under two names and two units.** `soulThetaWeight`, *Θ per soul level*, integer (`spec-tree-state.md:355`, `spec-tree-binder.md:237` and §5.1, `spec-squad-harness.md:219`) vs `soulTrack.thetaPerSoulLevelMilli`, *per-mille* (`spec-tree-resolve.md:393`). `tree-resolve` is the module that reads it; the other three name it | grep, this session |
| **c** | **The stage-3 generator has two names.** `tools/PassiveTreeGen` (`spec-tree-catalog.md:299`, `spec-tree-review.md:522`, `spec-species-tree.md:417`) vs `tools/TreeBinder` (`spec-tree-binder.md:529-531,552`). Both write `data/generated/passive-tree/`, both define `--check` and `--explain` | grep, this session |
| **d** | **`mechanismFloor` is read by stage 2 and emitted by nobody.** `spec-tree-language.md:157` sets `cell.nodeClass := "mechanism" if tier >= t.mechanismFloor` — a tier **threshold** — and gate 16 (`:405`) fails *"any deep-tier `magnitude` node"*. `spec-tree-plan.md:288-302` emits per-tier **counts** from a ramp: `broad-and-flat` is `0,0,0,1,1,1,1,2,2,2`, so tier 4 carries one mechanism node **and one magnitude node**. Under a threshold reading, gate 16 fails every tree the planner emits. `tree-plan`'s per-tree schema (`:815-847`) has no `mechanismFloor` field at all | read, this session |

**e — the stage-1 holes and the stage-2 response do not line up.** `spec-tree-plan.md:827-846` declares
the HOLEs as `name`, `flavour`, `text`, `affixIds`, `tags`, `exclusion`, `rationale`.
`spec-tree-language.md:326-353` returns `affixIds`, `affinity`, `exclusion`, `name`, `nameKey`, `flavor`,
`blocked`. So `text` is a hole nobody fills; `affinity`, `nameKey`, `flavor` and `blocked` are answers no
hole receives; and `elementSlot` / `statusId`, listed as CHOSEN at `spec-tree-language.md:66-67`, appear
in neither schema.

**What to add, and where.** One reconciliation pass across `tree-plan` §The plan schema,
`tree-language` §6.3 and the two tunables tables. `tree-language`'s own note at `:565-567` —
*"`tree-plan` is wave 0 and **unspecced**… the names must be reconciled when `spec-tree-plan.md`
lands"* — is now stale; that spec landed the same day. The reconciliation is the task, and it is small.

---

## 3. Decision ledger, D1–D36

Read from requirement text, not from the mapping rows. **Implemented** means some spec carries a
requirement that produces the behaviour; **deferred** means a spec says so and says why; **dropped**
means no spec carries it and no spec says it was left out.

| # | Verdict | Where, or why not |
|---|---|---|
| D1 | **Implemented as a constraint** | `spec-tree-plan.md:976` — *"honoured by omission: the roster has no class category"*, and §1's roster is aptitude/element/status/demonFamily. See §10 |
| D2 | ⛔ **Partial — two sources dropped** | §2.3 |
| D3 | Implemented | `tree-binder` §5, `tree-state` §7, `tree-resolve` §6.2, `tree-surface` §4 |
| D4 | Implemented | `spec-tree-resolve.md` §5.1, with the `F ≤ Fmax` proof and test 6 |
| D5 | Implemented, provisionality preserved | `spec-tree-resolve.md` §5.4 — `Fmax = 1000‰` is a legal, tested configuration, so withdrawing `F` is a tuning change, not a refactor |
| D6 | Implemented | `spec-tree-resolve.md` §5.3, in both read modes, for the Θ-invariance reason. ⚠️ `spec-tree-plan.md:980` maps D6 to `branchSplitMilli = 500`, which is branch symmetry rather than the focus multiplier — harmless, but it inflates apparent coverage |
| D7 | Implemented | `tree-resolve` (nothing taxes breadth), `tree-surface` (*"spreading is a real choice"*), `squad-harness` §5 |
| D8 | Implemented, with the exploit named | `spec-tree-resolve.md` §5.1's blend, §5.2's full statement of F4, §15.1's owner ruling owed |
| D9 | Implemented | `spec-tree-plan.md` §1 — roster read from mirrors, never a literal |
| D10 | Implemented | `tree-plan` §1, `tree-catalog` §2.2, `species-tree` §1 |
| D11 | ⚠️ **Rule implemented, carrier missing** | §2.3 |
| D12 | ⚠️ **Contested by a sibling spec** | §2.1 |
| D13 | Implemented | `spec-tree-plan.md` is the stage; §4 makes the mechanism/magnitude split derived rather than declared |
| D14 | Implemented, blocked on an orphan | `tree-plan` §6 defines the vocabulary, `tree-language` §5 the ladder, `tree-catalog` §2.2 the record, `tree-resolve` test 17, `tree-surface` §8, `tree-review` the census. Blocked on the atom-tag vocabulary (§4.2) |
| D15 | Rule implemented, evidence unowned | `spec-tree-plan.md` §3 proves equal value as an identity. The *"budget is not value"* evidence has no owner, and `spec-squad-harness.md` open question 3 says so |
| D16 | Deferred correctly, everywhere | Every spec refuses budget; `spec-mechanism-wiring.md` §9 scopes the 17th kind and records that it *"has no spec, no task and no map row today"* |
| D17 | Implemented | `spec-species-tree.md` §3 — the planner assigns, the stage picks from alternates already inside the quota |
| D18 | Implemented, pricing contradiction named | `tree-state` §5 plus §5.1's three-way contradiction, `tree-catalog` R4, `tree-surface` §5.1 |
| D19 | Superseded — correctly implemented nowhere | Replaced by D35 |
| D20 | Half-superseded, correctly | Indexing superseded by D26; the **pairing rule survives and is structural** (`spec-tree-plan.md:804`, `spec-tree-binder.md:113`) |
| D21 | Implemented | `tree-state` §1 (sparse storage argued from 3.1M rows), §6 (the batch read), `tree-resolve` (per-actor memo), `tree-surface`, `squad-harness` §4 |
| D22 | Implemented as a build constraint in all eleven | Counted vocabularies; zero new kinds, triggers or attach points |
| D23 | Implemented, with a declared narrowing | `spec-species-tree.md` §5. U3's `speciesUniqueAffixMin = 4` narrows *"nodes no other tree has"* — declared as open question 1, not hidden |
| D24 | Implemented | `tree-catalog` §1's freeze line, `tree-plan` §Reproducibility, `tree-language` §6.4, `tree-review` §1–2, `tree-binder` §1 |
| D25 | Implemented, two consequences stranded | `spec-tree-state.md` §2. See §7 findings 1 and 2 |
| D26 | Implemented, currency disputed | `tree-plan` §2 (flatness computed at every tier), `tree-resolve` §3.1 and test 1. §2.1 |
| D27 | Implemented as declared-partial | 39 of 58 trees; `demonFamilies: []` plus a `_pending` entry, *"never silence"* (`spec-tree-plan.md:721`). D27 itself makes curation a build-order task |
| D28 | Implemented | `spec-tree-resolve.md` §4 with the measured `Θ ≲ 300` bound recorded; `spec-tree-surface.md` §7's five parts |
| D29 | Implemented, with the G9 correction | `spec-tree-plan.md` §1 — rootless, 40 exactly, even by construction |
| D30 | Implemented | `spec-species-tree.md` — 840 × 40, costed at 105,840 calls and ≈33 human hours per pass |
| D31 | Superseded — correctly implemented nowhere | Replaced by D35 |
| D32 | Implemented | `tree-plan` §8, `tree-language` §4, `species-tree` §3.2, `tree-review` §5.5 |
| D33 | Implemented | `spec-squad-harness.md` in full — three columns, a stated resolution, writes no tuning value |
| D34 | Implemented | `spec-tree-state.md` §3, with the arithmetic showing why one scalar breaks D25 |
| D35 | ⛔ **Named, not implemented** | §2.2 |
| D36 | Implemented | `spec-tree-state.md` §2.2, and the coupling `first = step·(k+1)/2` is correct — see §7 finding 2's note on doc 10's own algebra |

---

## 4. §13 coverage — the three-bucket inventory

### 4.1 §13.2, wiring gaps

| Gap | Owner | Note |
|---|---|---|
| A status's derived write never composes | `mechanism-wiring` G1 | The critical path, correctly identified as such |
| `stat.derived` unscored in Sim | `mechanism-wiring` G3 | With the cell-flips-last ordering `decisions.md:106` already established |
| Battle's recompose runs once | `mechanism-wiring` G2 | Cited by symbol, because the file is under concurrent edit |
| `AffixTags` has no production call site | ⚠️ **named, not owned** | `spec-mechanism-wiring.md:20-22` files it as B12 (`effect-pipeline` ep-11/ep-12, *"specced and in no task list"*). Honest |
| `Instantiator` unreached | **refuted** | `spec-tree-binder.md:50-53` — two production callers exist (`SpeciesMaterialiser.cs:55`, `RpgStore.AtomInstances.cs:341`). A correction the ideal should take |
| `PowerLadderKMilli` is per-mille | ⚠️ **ownership unclear** | `spec-tree-resolve.md:370` assigns `PowerLadderKMicro` to `mechanism-wiring`; `spec-mechanism-wiring.md:161` says *"Nothing else"* and its modified-file list has no `ValueSpec.cs`; `spec-tree-binder.md:557` claims it as an Ask-first item. Ideal §12.4 puts it in wave 0. One of the three has to take it |
| Two `AllocationScope`s unreached | `tree-state` §3, `tree-plan` §7, `species-tree` OQ2 | Named as blocked-on, tracked |
| `RespecPolicy` returns Hunger, zero callers | `tree-state` §5.1 and OQ1 | The three-way contradiction stated, not assumed away |
| Battle raises no `OnDamageTaken`/`OnSpawn`/`OnDeath` | ⚠️ **named, not owned** | `spec-mechanism-wiring.md:89-90` files it as B7, owned by `battle-timeline`/`action` |
| Two gate quantities do not exist | ⛔ **named, and nothing closes them** | §2.2 — this is the row that becomes an S1 gap |

### 4.2 §13.3, real gaps

| Gap | Owner |
|---|---|
| Element-payload conversion (D16) | **Declared orphan.** `spec-mechanism-wiring.md` §9 scopes it in four steps and says it has no spec, task or map row. Every spec refuses budget. This is the right handling |
| **Layer denial / bypass** | ⛔ **ORPHAN — no spec names it.** A grep across all eleven returns nothing for *layer denial*, *bypass*, or *dials, no switches*. See §7 finding 3 |
| Squad-scope measurement (D33) | `squad-harness` |
| The soul track in the balance model | `squad-harness` S3 |
| A closed `family` roster | `tree-plan` owed-item 5, `species-tree` §7.3 — build order, per D27 |
| An atom-tag vocabulary | **Declared orphan** (B1, `content-stack`). Named in `tree-plan` §6, `tree-language` §5.1, `tree-catalog` Boundaries, `species-tree` §8, `mechanism-wiring` §13 |

---

## 5. §14 coverage — tunables

Every number in the ideal's table is named by some spec, with a unit and a file. Two carry conflicts.

| Ideal §14 key | Named by | Unit and file agree? |
|---|---|---|
| `concentration.fmax` | `tree-resolve` §8, as `concentration.fmaxMilli` | ✅ per-mille multiplier, `passive-tree.v1.json` |
| `concentration.w` | `tree-resolve` §8, as `concentration.wMilli` | ✅ and marked PRIMARY, as doc 16 requires |
| `unlockCost.first` / `.step` | `tree-state` §8 | ✅ skill points, `passive-tree.v1.json` |
| `grant.skillPointsPerThetaMilliByScope` | `tree-state` §3 and §8 | ✅ lands under `pointEconomy.` beside its shipped sibling, which is the correct home |
| `tierLadder.k` | `tree-plan`, `tree-resolve`, `tree-catalog`, `squad-harness` | ⛔ **three names, two files, two units** — §2.4a |
| `nodePotencyCeiling` | `tree-plan` §5, as `potency.maxNodeShareMilli` | ✅ derived to 91‰ and recomputed at check. It lands in `passive-tree-gen.v1.json` rather than the ideal's proposed file, and `tree-plan` justifies the split |
| `soulThetaWeight` (`Ws`) | `tree-state`, `tree-binder`, `squad-harness`, `tree-resolve` | ⛔ **two names, two units** — §2.4b |
| target distribution | `tree-plan` §8, `tree-language` §4.3, `species-tree` §3.2, `tree-review` §6.3 | ✅ `passive-tree-targets.v1.json` |
| `pointEconomy.respecPrice` | `tree-state` §8 | ✅ and the resource contradiction is an open question, not a guess |
| `b` | `tree-resolve` §8, `squad-harness` §6 | ✅ correctly declared **not** a balance dial |
| tiers = 10 · branches = 2 | `tree-plan` §Tunables, `tree-catalog` §2.1 | ✅ structural, with the reason — a re-minted id is a migration under D24 |

**Three tunables the specs add that §14 does not list**, all named with a unit and a file, all flagged
UNMEASURED rather than guessed: `budget.treeTotalPoints` (`tree-plan`), `treeShareMilli` and
`tierWeightShape` (`tree-binder`). Adding them is correct — §14 is the ideal's list, not a closed set.
But `budget.treeTotalPoints` and `treeShareMilli` are each described as *"the single biggest dial the
tree layer has"* in different files, and one sentence somewhere should say whether they are two dials
or one.

**No tunable in the ideal became a `const` in a spec.** That is the defect `tunables-ssot.md` exists to
prevent, and this spec set avoided it.

---

## 6. §15 coverage — what the ideal deliberately did not decide

| §15 item | Held? |
|---|---|
| The species-tree pipeline's internals | ✅ Deferred *to its own spec round*, and this is that round. Not a leak |
| Whether `F` survives | ✅ `spec-tree-resolve.md` §5.4 keeps it provisional **and makes withdrawal a tuning change**; `squad-harness` reports and never decides |
| How the injector renders any of this | ✅ No spec decides it. `tree-surface` §10 and `mechanism-wiring`'s Never list both hold the injector to enrichment |
| The `family` taxonomy | ✅ No spec decides it. `tree-plan` declares `_pending`; `species-tree` §7.3 refuses to run without a closed roster |
| Any node's text, name or flavour | ✅ `tree-language` specifies the schema and the gates, never a string |

**Two narrowings of locked decisions, both declared rather than smuggled:**

1. **`nullification` removed from the generated corpus.** D14 locks the ladder as Reroute → Precedence →
   Nullification. `spec-tree-language.md:201` removes it from the schema enum and `:554-557` raises it as
   open question 1 needing an owner ruling; `spec-species-tree.md` OQ3 agrees. ⚠️ But
   `spec-tree-review.md:421` already enforces *"any `nullification` exclusion exists"* as a hard
   unshippable condition without noting that the narrowing is pending. One cross-reference fixes it.
2. **`speciesUniqueAffixMin = 4`** narrows D23's *"nodes no other tree has"*. `spec-species-tree.md` §5.2
   shows the cost curve — 840 × k authored affixes against a shipped authored corpus of **two** — and
   files it as open question 1. Honest, and the right call to surface.

---

## 7. Stranded research findings

The seven the brief named all reached a spec — see §11. These four did not.

### 1. ⛔ D25 makes `H` order-dependent (doc 10, A13 and §3.7)

**The finding.** D8 says `H` reads spent points. Under D25 the points-per-node varies with purchase
order, so two players holding an identical final build have different per-tree *point* shares depending
on which tree they filled first — and `H = Σ share²` reads exactly that vector. Doc 10's fix is one
line: *"compute `H` on node budget or node count, never on points paid."*

**FACT.** `spec-tree-resolve.md:205` defines `H_points = Σ_i (p_i / Σp)²` with `p_i = self-spent POINTS
in tree i` — the order-dependent quantity, unchanged.

**FACT.** `spec-tree-resolve.md:631` asserts the opposite in its own decisions table: *"D18 respec is a
full reset — **nothing here is order-sensitive**, so no orphaned-unlock case exists to handle."* That is
true for *unlocks* — `spec-tree-state.md:117-122` proves the total cost is order-free — and false for the
*share vector*, which is what `F` reads.

**Why it matters.** D18 was adopted specifically to dissolve order-sensitivity. D25 reintroduces it one
layer over, on the quantity the focus multiplier is computed from. Two players who followed the same
build guide in a different order get a different `F`.

**Add to `spec-tree-resolve.md` §5.1:** define `H_points` over **owned node count per tree** (or the
plan's `budgetShareMilli` per tree), not points paid, with a test asserting that two shuffled purchase
orders of the same final set produce an identical `H`. Then correct the §16 row.

### 2. ⛔ D25's flatness relation is archetype-dependent, and `gated-deep` becomes the better buy (doc 10 §6.2, consequences 2 and 3)

**FACT.** `spec-tree-state.md:143` derives the coupling from *"`k = 4` nodes per tier (D29: 40 nodes /
10 tiers)"* — a **uniform** width.

**FACT.** `spec-tree-plan.md:229,237` ships width vectors that are not uniform:
`gated-deep` = `[3,3,3,2,2,2,2,1,1,1]`, `late-crown` = `[1,1,2,2,2,2,2,2,3,3]`. Only
`broad-and-flat` = `[2,2,…]` gives four nodes per tier.

**INFERENCE, arithmetic shown.** With `K` nodes per tier and cost `c(N) = c₀ + (N−1)d`, the skill-point
cost of tier `t`'s nodes is `K·c₀ + d·(K/2)·(2Kt − K − 1)`. Reward at tier `t` is `b·t`. Reward per skill
point is flat only when the constant term vanishes — `c₀ = d(K+1)/2`. `tree-state`'s
`first = step·(k+1)/2` is exactly that, and it is correct **for `K = 4`**. It is wrong at every tier
where an archetype's width is not 2 per branch.

**Consequence 3 is the sharper half.** `spec-tree-plan.md:246` states, as a feature, that the strongest
single node differs by **2.5×** across archetypes — `gated-deep`'s capstone is 182‰ of a branch,
`late-crown`'s largest is 73‰. Under D25 every node costs the same escalating price regardless of what
it carries, so `gated-deep` buys 2.5× the value for the same skill point. **D15's "no tree is OP" holds
in budget points and fails in the currency the player actually spends** — and `tree-plan`'s
`archetype_shapes_actually_differ` test *requires* the ≥2× spread that causes it.

**One correction found while verifying this.** Doc 10 §6.2's own relation, `c₀ = d(2w+1)/4`, is off by a
factor of two — with `2w = K`, the correct form is `c₀ = d(2w+1)/2`. D36 and `spec-tree-state.md:143`
both carry the right form. The research note's algebra is what is wrong here, not the spec's.

**Add to `spec-tree-state.md` §2.2 and `spec-tree-plan.md` §3:** state the coupling over the archetype's
own width vector rather than a uniform `k`, or state explicitly that flatness is asserted at the tree
average, that per-tier reward-per-skill-point varies by archetype, and by how much. Then add one
`tree-plan` invariant: no archetype may buy more than a stated margin more value per skill point than
another. This is the cheapest item on this list to close and the most expensive to discover after 25,900
nodes are authored.

### 3. ⛔ §13.3's "layer denial / bypass" reached no spec

**FACT.** The ideal §13.3 lists it as a **real gap**: *"Every shipped 'break their X' is a saturating
contest that provably never reaches zero — `PierceFactor` bounded (0,1], shield pen capped, parry/block
shred clamped. Dials, no switches."*

**FACT, grepped this session.** No spec mentions *layer denial*, *bypass*, *dials, no switches*, or the
never-reaches-zero claim. It is the only entry in the ideal's own real-gap list that nobody names.

**FACT, verified in code this session.** `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:183-184`:

```csharp
var pParryRaw = Math.Max(0.0, CombatDerivedReader.ParryRate(defSnap) - CombatDerivedReader.ParryBreak(atkSnap)) / 1000.0;
var pBlockRaw = Math.Max(0.0, CombatDerivedReader.BlockRate(defSnap) - CombatDerivedReader.BlockBreak(atkSnap)) / 1000.0;
```

Parry and block **break** are plain subtraction floored at zero — they *do* reach zero. So
`spec-tree-binder.md` §6's M3 (*"an anti-turtle punish"*, built on `parry.break` + `block.break` +
`shield.pen`) is right against code, and the ideal's blanket claim is at least partly stale. Note the
ideal's row names *shred*, a different channel from *break*, so the two may not strictly contradict —
but nobody said so, in either direction.

**Why it still matters.** §3.5's conclusion is that a focus build is rescued only by reaching a defensive
layer multiplicatively, and M3 is the one worked example that does it. Whether *full* layer denial — a
switch rather than a dial — exists is the question the ideal flagged and nobody answered, and `tree-plan`
is reserving deep-tier budget on the assumption that it does.

**Add to `spec-tree-binder.md` §6:** one paragraph naming which shipped layers can be driven to zero
(parry and block, via break, cited) and which cannot (`PierceFactor`, shield pen, the `_categoryResistCap`
family §4.2 already names), so `tree-plan`'s deep-tier budget is spent on the ones that work. And correct
the ideal's §13.3 row while you are there.

### 4. ⚠️ D25 is per-actor only, and it has no caps-register row (doc 10, A14 and A2)

**A14.** D25 bounds one actor's breadth. D21 gives every actor its own tree state, so a 2,000-demon roster
restarts the escalation at `first` two thousand times. `spec-tree-state.md` §3 partially closes this by
making the *budget* per-scope — a demon must not read `Θ_player` — which is the right fix for the budget
half. But the spec never states whether legion-scale breadth is deliberately unpriced. D25's own
justification is *"the whole catalog unlocked and the tree stopped being a choice"*, and at roster scale
it still is.

**A2.** Doc 10 grades D25 a cap by the register's own definition (*"a flat rate facing a scaling cost"*,
`ssot-power-scale.md:783`) and asks for **a §11.10 row with a verdict plus a §10 cost-ladder row**.
`spec-tree-state.md` §9 delivers the §10.2 row (row 29) and argues PS-8 compliance in §2.3, but requests
**no §11 register row**. `spec-tree-plan.md`'s owed-item 4 asks for a §11.10 row for the authored
*depth*, which is a different thing.

**Add to `spec-tree-state.md` §9:** the §11.10 register row for `unlockCost`, carrying the soft-bound
verdict §2.3 already argues; and one sentence in Boundaries or Open questions on roster-scale breadth.

---

## 8. Reverse coverage — is anything invented?

**Almost nothing, and what there is, is declared.** Every requirement traced back to a decision, a
research finding, or a shipped precedent the spec cites. Three are worth naming:

1. **`speciesUniqueAffixMin = 4`** narrows D23. Declared as an open question, with its cost curve.
2. **`nullification` removed from the schema** narrows D14. Declared as an open question — though
   `tree-review` already enforces it (§6).
3. **`tree-surface`'s Plan object, share code and `plan=<code>` URL grammar.** In no decision. It traces
   to D24's own stated payoff (*"build sharing becomes possible — and it is the payoff"*, ideal §10.2
   item 4) and to D18/D25 making a wrong build expensive. Legitimate surface design inside the module's
   remit, and §5.3's *"a shared plan may never name a price"* is a genuinely new constraint that falls
   out of D25 correctly.

`tree-plan`'s removal of doc 02's shared root (`G9`) is a change to a research invariant, not scope creep:
it is worked in full at `spec-tree-plan.md:40-83` with the arithmetic shown, and the spec's own checklist
(`:1116-1120`) records that the propagation to the ideal, the map and docs 02/10 is **owed and not done**.
That is the honest handling.

---

## 9. The six map assumptions

| # | Assumption | Held? |
|---|---|---|
| 1 | The generator is a `tools/` program; nothing in `src/` generates a node | ✅ **with a note.** Entry points are `tools/` in all four generation specs. `tree-binder` and `tree-catalog` put the *logic* in `src/FusionRpg.Core/PassiveTree/{Binding,Catalog}/` so it is unit-testable — the `DemonSpeciesGen` shape, and defensible. But see §2.4c: the tool has two names |
| 2 | `tree-resolve` extends the shipped resolver rather than forking it | ✅ `spec-tree-resolve.md` §2.1 — a fan-in on the existing `boundDerivedAtoms` delegate, plus a third `BattleChannelMod` source. No new subsystem, no new order band |
| 3 | Web is primary; the injector enriches, never gates | ✅ `spec-tree-surface.md` §10; `spec-mechanism-wiring.md`'s Never list refuses to gate on the default-off overlay |
| 4 | Species trees reuse the generic node record | ✅ `spec-species-tree.md` §1 names it; `spec-tree-catalog.md` §5 carries it |
| 5 | `squad-harness` is measurement only | ✅ and pinned by a success criterion: *"zero files changed under `src/`, `data/` or `tests/`"* |
| 6 | Every balance number is a tunable key with a unit | ⚠️ **honoured in form, violated in substance** by §2.4a and §2.4b — two numbers each live in two files under different names, which is the copied-number defect `tree-plan` §Tunables itself calls *"a future drift bug with a delay fuse"* |

---

## 10. The two flagged decisions

### D1 — claimed by no spec's table. Verdict: **correctly handled, and `tree-plan`'s framing is the right one.**

`tree-catalog` and `tree-state` both call D1 *"a standing fact about the class system that no
passive-tree module implements — a constraint, not a requirement."* True but incomplete.
`spec-tree-plan.md:976` gives the better answer: *"honoured by omission — the roster has no class
category."* D1 is a **negative** decision, and a negative decision is implemented by a structure that
cannot express the thing it forbids. `tree-plan` §1's roster is four categories — aptitude, element,
status, demonFamily — and none is a class. Nothing anywhere in the eleven specs introduces a player
class, a class container, or a build archetype the player selects. **D1 is honoured.**

**One cheap improvement.** It is honoured and unpinned: no test asserts it. `tree-plan` already greps its
own source for bare roster counts (`roster_counts_are_read_never_typed`); one more invariant — *the
plan's category enum has exactly four members and none of them is a class* — makes D1 checkable instead
of merely true. Add it to `spec-tree-plan.md` §Testing strategy.

### D2 — claimed only by `tree-state`. Verdict: **the flag was right, and it is worse than a single-module claim.**

D2 is not under-covered because one module carries it. It is under-covered because **that module
implements one of the four sources.** See §2.3. Skill points are specified end to end. Items and affixes
have a rule in four specs and a carrier in none — `skillPoints` appears twice in all of `src/`, both in
`AptitudeTuning.cs`. Aptitude thresholds and demon aspect appear once in the whole spec set, at
`spec-tree-resolve.md:242`, quoted from the red team as terms whose meaning is undefined.

This is the highest-value single finding in the audit, because D2 is what gives the program its spender.
The ideal's own line is *"the `skillPointsPerTheta: 1` grant, minted since 2026-08-26 with **zero
consumers**, finally has a spender."* One of the four spenders exists.

---

## 11. Fully covered, verified

Named so the gap list above reads as short rather than selective. Each was checked against requirement
text, not a mapping row.

**The seven research findings the brief asked about all reached a spec:**

| Finding | Where it landed |
|---|---|
| D15's measurement gap — `PowerVector.Total` is not value, 0.3%–97.9% at identical budgets | `spec-squad-harness.md` §11 S4 and open question 3 — named as **owned by nobody**, which is the correct handling of an orphan |
| The F4 self-spent exploit | `spec-tree-resolve.md` §5.2 states it in full, including that it *"strictly dominates an honest pure build"*; §15.1 owes the owner ruling and says the module is buildable while it is open |
| The atom-tag vocabulary absence | `spec-tree-plan.md` §6 (the plan **defines** a vocabulary rather than reading one — the load-bearing move), `spec-tree-language.md` §5.1 (counted: three semantic values across 98 affix families), `spec-tree-catalog.md` Boundaries, `spec-species-tree.md` §8 |
| `w` is the primary late-game parameter | `spec-tree-resolve.md` §8 (*"at `w = 1000‰` the design has no late game"*), `spec-squad-harness.md` §6 and S3 |
| Θ ≈ 300 depth exhaustion | `spec-tree-resolve.md` §4.2 with the measured table; `spec-tree-plan.md:150-154` with the §11.10 row it owes |
| The five `LowerIsBetter` channels | `spec-tree-binder.md` §4.2 item 3 — and it **corrects doc 04**, which named only `takeDmgMultiplier` |
| The `_`-prefix blind spot | `spec-tree-review.md` §7, with a dedicated metric (`PassiveTree/HiddenFileCount`); `spec-species-tree.md` §2.1, with two rules so the pipeline does not inherit it |

**Other things checked and found complete:**

- **The overflow rules.** Every magnitude in every spec is `long`; `float` is refused by name in six of
  them; `tree-resolve` §7.2 adopts `AptitudeReadFunctions`' `decimal` widening verbatim rather than
  re-deriving it; the `int` narrowing at `AtomCompiler.cs:465` is named in three specs, each with the Θ
  at which it bites.
- **No hard ceilings.** Every bound in the set is either a soft economic bound proved unbounded
  (`spec-tree-state.md` §2.3, with its *"no `Math.Min`, no narrowing cast, no boolean `CanUnlock`"*
  triple), a bounded ratio that says so (`potency.maxNodeShareMilli`), a content bound that says so (the
  authored tier depth), or a per-run measurement budget that says so (`--trials`).
- **One power ladder.** No spec writes an `f(level)`. Four `ssot-power-scale.md` §10 rows are requested
  by name and by owner — `req(t)` and the §11.10 depth row (`tree-plan`), `Ws` (`tree-binder`, read by
  `tree-resolve`), `unlockCost` row 29 (`tree-state`) — and every one carries the warning that
  `guard-power.ps1` **cannot detect its absence**, with the blind-spot mechanism cited.
- **The counted vocabularies.** Four specs recount 7 attach points / 16 kinds / 13 triggers from `src/`
  this session rather than quoting, and two note that `AtomKindRegistry.cs:6`'s own comment ("5 attach
  points, 12 kinds") is stale fifteen lines above the correct constants.
- **Node-id stability.** `spec-tree-catalog.md` §3 rules out a content hash and a positional ordinal with
  what each breaks, and `spec-tree-review.md` §8 shows the whole incremental-review design rests on that
  one choice — *"with content-hash ids, every rebalance is a full 35,160-node re-review."*
- **The corpus count.** `spec-tree-review.md` §1.1 and `spec-species-tree.md` §2 both recount 840 species,
  not 841, and both name the two cells of the ideal's §9 table that move.
- **The review claim.** `spec-tree-review.md` §2 states the population, the claim and the confidence
  separately, and forbids the unqualified sentence *"the catalog was reviewed."*
- **Determinism.** Byte-identical regeneration is a success criterion in five specs, with the
  canonical-JSON and excluded-timestamp hazards named rather than discovered.

**One correction to the ideal, worth propagating.** `spec-mechanism-wiring.md:54` and
`spec-tree-resolve.md:78` both cite `EffectRuntime.cs:491` as reflect's production wiring, and
`mechanism-wiring` uses it to rank Retaliation as *"already live — content, not code."*
**FACT, read this session:** `src/FusionRpg.Injector/Effects/EffectRuntime.cs:485-495` is the ShieldGate
and `actorResolve` wiring, on the **lawn**; `reflect` has zero functional hits in
`src/FusionRpg.Core/Battle/`. `spec-squad-harness.md:342` carries the correction, and the ideal §13.1
already made it on 2026-09-05. So Retaliation is live on the lawn and **not measurable in Battle or Sim,
which is where the balance proof runs** — which means `mechanism-wiring`'s scope may be one gap short of
what §3.5's re-measurement needs.
