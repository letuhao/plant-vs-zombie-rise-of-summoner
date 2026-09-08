# Passive trees — the cross-program dependency map (2026-09-05)

**What this answers.** `passive-tree-ideal.md` carries 32 decisions and its §11.4 says three of them are
*"blocked on other programs, tracked not open."* Nobody had checked which program actually owns each
piece, whether the work is specced, planned, built or absent, or in what order it has to land. Without
that, the passive-tree program cannot be planned — it has an ideal and nothing else (`ls tasks/ | grep
passive` returns nothing; there is no `passive-tree-map.md`).

**Method.** Every claim below was read in `src/`, `data/`, `tasks/` or `docs/` **this session** and
cites `file:line`. Marked **FACT** (read directly), **INFERENCE** (drawn from a fact), or **RECALL**
(not verified here). Design-gate reading done this session: `DESIGN-GATE.md`, `decisions.md` (rows 103,
112 and the class-system / derived-write-lawn rows), `passive-tree-ideal.md` in full, research 06 and 09
in full, 01–05 / 07–08 by targeted read.

> **⛔ The framing rule this document is written under.** An inert path — a default-off toggle, a null
> delegate, a built API with zero production callers — is a **WIRING GAP**, not an architectural wall.
> Four of the blockers below turned out to be wiring gaps, and **two of them are already built and were
> being reported as missing.** Only one is a genuinely new capability.

---

## 1. The dependency table

Sizes use the repo's own S/M/L task convention.

| # | Blocker | Owning program | State | Size | Hard / Soft |
|---|---|---|---|---|---|
| **B1** | **D14** — a tag vocabulary for property-keyed exclusion | `content-stack` (effect-pipeline **ep-8**) | ⚠️ **The derived-tag rule is BUILT** (`AffixTags.cs`, 124 lines). Zero production callers. The **vocabulary** is 3 values | **S** (call site) + **M** (vocabulary) | **Soft** — exclusions can key on posture plus 3 tags; D14's 2%-of-nodes target is unreachable |
| **B2** | **D16** — a 17th atom kind that writes an element payload | `content-stack` (effect-atom, E-series) | ⛔ **ABSENT.** No spec, no task, no map row | **M** + an uncosted pipeline change | **Soft** — allocate zero budget to conversion nodes |
| **B3** | **D31** — `AllocationScope` slot 5 | ⛔ **UNOWNED** | ⛔ **ABSENT.** The item program **defers** it; class-system has no task; effect-atom does not track it | **S** enum + **M** consumers | **HARD** for the 21 status trees |
| **B3b** | `AptitudeAllocation.Single` rejects any non-aptitude id | `class-system` | BUILT; the restriction is deliberate | **S** | **HARD** for status trees |
| **B4a** | Status → derived never composes (no 4th `IActorStatSubsystem`) | `effect-atom` / stats layer | ⛔ **ABSENT.** Wiring gap | **S** (~90 lines by precedent) | **HARD** — mechanism nodes |
| **B4b** | `stat.derived` declares `AtomTriggers.None` | `effect-atom` | BUILT as designed — permanent modifiers only | **M** | **HARD** for conditional-scaling nodes |
| **B4c** | Battle's derived recompose seam never runs mid-fight | `aura-skill` (T13) | Seam **BUILT**; called once at construction | **S** | **HARD** — mechanism nodes in Battle |
| **B4d** | The overlay combat resolver is default-off | injector / `combat-unification` | BUILT, default-off toggle | **S** | **Soft** — lawn only |
| **B4e** | `stat.derived` is `RuntimeState.None` in **Sim** | `effect-atom` | Quarantined deliberately | **M** | **HARD** for the balance sweep, not for shipping |
| **B5** | `PowerLadderKMilli` is per-mille (17% error at tier 1) | `content-stack` (effect-atom, `ValueSpec`) | ⛔ **ABSENT** | **S** (3 files) | **Soft → HARD** — it silently destroys D26 |
| **B6** | Migration: one retired node id makes an actor unloadable | `class-system` + `FusionRpg.Data` | BUILT, throws per row | **S** | **HARD** from the first catalog regeneration |
| **B7** | Battle fires no `OnDamageTaken` / `OnSpawn` / `OnDeath` | `battle-timeline` / `action` | ⚠️ **`OnDamageDealt` and `OnActivate` ARE fired.** The other three are absent | **M** | **HARD** for reflect / on-kill nodes; satisfied for on-hit |
| **B8** | Three `ssot-power-scale.md` §10 rows: `req(t)`, `W(T)`, `Ws` | `power` | ⛔ **ABSENT — and the power program is closed** (`tasks/power-todo.md` has zero open tasks) | **S** | **HARD** — no permission to exist without them |
| **B9** | **D9/D27** — a closed demon-family roster | `seedsmith` / `demon-seed` | Tools **BUILT** (D2.1/D2.2, 2026-08-31); the corpus is still open at **699 tokens** | **M** | **HARD** for family trees only |
| **B10** | `element_mastery` and almanac XP have zero `src/` hits | demon program (`aspect-scope`) / almanac | ⛔ **ABSENT** | **M** each | **HARD** for elemental and family tree gates |
| **B11** | `RespecPolicy` has zero production callers | `class-system` | BUILT, inert | **S** | **Soft** — parallel |
| **B12** | `affix-power-class` / `affix-channel-weights` (ep-11/12) | `effect-pipeline` | Specced, unbuilt, **and in no task list** | **M** | **Soft** — but it is B1's only named call site |

**Unowned, in one line:** **B3** (the fifth `AllocationScope`), **B8** (three §10 rows against a closed
program), and **B12** (two specced modules tracked in no plan).

---

## 2. The build order

The passive-tree program splits along its roster (D27), and the roster decides the order — each category
has a different gate quantity, so each has a different blocker set.

```text
WAVE 0  — permission to exist (nothing can be specced without these)
  B8   three §10 rows: req(t) = 5·t(t+1)/2, W(T) = b·T(T+1)/2, Ws (soul → Θ)   [power, S, UNOWNED]
  B5   PowerLadderKMicro — a per-million sibling on ValueSpec                  [content-stack, S]
  B6   reject a retired node id ONCE at an import boundary, never per row      [class-system, S]

WAVE 1  — the twelve primary trees (the only category whose gate quantity ships today)
  B4a  a fourth IActorStatSubsystem that folds the status bag into Derived     [effect-atom, S]
  B4c  call RecomposeDerived mid-fight (aura-skill T13's own deferred job)     [aura-skill, S]
  B7   raise OnDamageTaken / OnSpawn / OnDeath in BattleEngine                 [battle, M]
  B4e  a Sim consumer for stat.derived, so the sweep can score mechanism nodes [effect-atom, M]
        └─► primary trees are BUILDABLE, and measurable
  B4b  conditional scaling (a trigger-capable derived write)                   [effect-atom, M]
  B4d  flip the overlay resolver on by default, or ship the lawn degraded      [injector, S]

WAVE 2  — the six elemental trees
  B10a element_mastery — the Aspect scope's source value                       [demon program, M]

WAVE 3  — the 21 status trees
  B3   AllocationScope slot 5, then slot 6                                     [UNOWNED, S + M]
  B3b  let a scope name something that is not one of the twelve aptitudes      [class-system, S]

WAVE 4  — the demon-family trees
  B9   consolidate 699 family tokens into a closed roster                      [seedsmith, M]
  B10b almanac XP — the DemonType scope's source value                         [demon program, M]

WAVE 5  — the species trees (D30, ~24,000 nodes) and conversion nodes
  B2   a 17th atom kind that writes an element payload                         [effect-atom, M]
  B1   a tag vocabulary worth keying exclusions on                             [content-stack, S + M]
  B12  ep-11 / ep-12 — the only named call site AffixTags has                  [effect-pipeline, M]

PARALLEL, any time
  B11  price the respec (D18) — RespecPolicy is built and has no caller        [class-system, S]
```

**One line:** *Wave 0 (three §10 rows, `PowerLadderKMicro`, the import-boundary migration fix) → Wave 1's
four mechanism wirings → the twelve primary trees → then one wave per gate quantity as it lands.*

**Critical path for the mechanism nodes specifically: B4a.** §3.5 of the ideal proves a focus build
cannot be rescued with magnitude, so mechanism nodes are the entire point of the layer — and doc 05
shows the only node class that works end to end today is the derived-channel one. **B4a is the single
missing piece between "a status writes a `combat.*` channel" and "that channel has the value."** It is
~90 lines by the `AtomDerivedSubsystem` precedent, needs no new vocabulary and no new attach point, and
it unblocks Erosion, layer parity and conditional scaling at once.

---

## 3. B1 — D14 has more to key on than the red team found, and less than it needs

### 3.1 The derived-tag rule is not pending. It shipped.

**FACT.** `docs/architecture/effect-pipeline/spec-eligibility-tags.md:32-64` decides the rule:
`tagsOf(affixId)` is the union, over the affix's concrete refs, of `AtomRow.TagsJson`. **That module is
built.** `tasks/content-stack-todo.md:3031` reads `- [x] ep-8 eligibility-tags · M`, and the file it
names exists: `src/FusionRpg.Core/Effects/Atoms/AffixTags.cs`, 124 lines, with
`AffixTags.ProductionSupplier` returning the exact curried delegate the shipped resolver expects
(`tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs:233`).

So `passive-tree-ideal.md:494`'s fix — *"Land `spec-eligibility-tags.md`'s derived-tag registry first"* —
**is already done.** That moves B1 from a hard blocker to a soft one.

### 3.2 What is missing is two things, and they are different sizes

**(a) A call site.** **FACT** — `grep -rn "AffixTags" --include=*.cs src/ tools/` returns only the
declaration. `EligibilityResolver` likewise has no production caller
(`src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs:30-95`; every caller is
`tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs`). This is a **wiring gap**, and the task list
already names its owner: ep-8's own entry says the wiring *"belongs to the still-unbuilt
`spec-affix-channel-weights.md`"* — which is **B12**, and which is in no task list at all.

**(b) A vocabulary.** **FACT, measured this session.** `AtomRow.TagsJson` is documented as *"Element,
family, category — for AI, UI, and cost lookup"* (`src/FusionRpg.Core/Effects/Atoms/AtomRow.cs:40`) and
is a free-form JSON string with no enum behind it. Parsing every tag value in the 15 authored atom
family files under `data/seed/items/affix-families/` gives **three distinct semantic values**:

| value | count |
|---|---|
| `offensive` | 41 |
| `defensive` | 40 |
| `utility` | 17 |

The wider `data/` tree holds 64 distinct tag values, but the rest are item **material** tags (`organic`
650, `rooted` 511, `metal` 177 …) and generator provenance (`generator=E43`), not atom semantics.

**INFERENCE.** D14's escalation ladder (Reroute → Precedence → Nullification) at a target rarity of ~2%
of nodes needs a property space with real cardinality. Three values plus posture gives roughly six
distinguishable predicates. Across ~1,560 generic nodes that is not a 2% exclusion rate — it is either
far too coarse, or it excludes nothing at all.

**Verdict: SOFT blocker.** Trees can ship with exclusions keyed on posture and the three tags; D14's
stated rarity target cannot be hit. And the vocabulary work has **no owning module** — ep-8 explicitly
scoped tags out to *"whichever module writes them — `affix-library`, `affix-authoring`; not this
module's concern"* (`EligibilityRule.cs:3-9`), and neither of those specs mints a vocabulary either.

---

## 4. B2 — D16 is the one genuinely new capability in the set

**FACT.** The atom vocabulary is 16 kinds, from
`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:31` (`KindCount = 16`), confirmed by listing the
registry's own kind literals: `stat.modify`, `stat.derived`, `resource.delta`, `resource.economy`,
`status.apply`, `status.clear`, `shield.grant`, `spawn.entity`, `board.action`, `grid.spawn`,
`grid.clear`, `box.set`, `match.modify`, `wave.control`, `bullet.modify`, `ui.present`.

**None writes an element payload.** The payload is an *input* to the resolver:
`OverlayCombatCalculator.cs:128-172` iterates `request.Components` and reads `c.Element` for every
element-keyed channel — power, defense, penetration, absorption, amplification, reduction, accuracy,
dodge, crit rate, crit damage. `ElementPayloadComponent` is constructed in exactly two production
places: `src/FusionRpg.Core/Battle/HybridPayload.cs:33-52` (from the attacker's own primary/secondary
element) and `DamagePacketBuilder.ParseElementPayload` (`:89-104`, from the overlay dictionary). There is
no atom-authorable path to either.

**The failure is silent.** A player converting their damage to fire keeps every fire-keyed affix reading
against a payload that is still, say, `earth`. Nothing throws; the affix contributes zero. That is
precisely what D16 (`passive-tree-ideal.md:48`) exists to prevent.

### 4.1 What "adding a kind" costs — measured from the four times it was done

**FACT.** `decisions.md:112` ("Atom attach points", 2026-09-04) governs the **attach point** list and
says *"Growing this list is a reviewed change to this row."* The **kind** count is guarded separately —
`AtomKindRegistryTests.cs` asserts `KindCount == AtomKindRegistry.All.Count` — and each addition was
carried by its own module spec. The record, from `AtomKindRegistry.cs:22-31`'s comment trail and
`docs/architecture/effect-atom-map.md:342-349`:

| Module | Kind added | Attach point | Task size (`content-stack-todo.md`) |
|---|---|---|---|
| E35 `match-modify` | `match.modify` | **new** — `Match` | **L** — line 2511, and it *created* the `decisions.md` row |
| E36 `wave-control` | `wave.control` | reuses `Match` | **M** — line 2596 |
| E37 `projectile-control` | `bullet.modify` | existing `Board` | **M** — line 2683 |
| E41 `ui-attach-point` | `ui.present` | **new** — `Ui` | **M** — line 2940 |

**INFERENCE.** A 17th kind on an existing attach point is an **M**: one module spec, one registry row
with a param schema and a runtime support matrix, an executor per runtime that claims `Full`, and a
`decisions.md` amendment only if a new attach point is needed. A conversion kind attaches to combat, not
to a new seam, so it should not need one.

**There is a second half nobody has costed.** The kind is only the authoring surface; the *executor* has
to rewrite `request.Components` before `OverlayCombatCalculator.Compute` runs, on both hosts. That is a
change to the damage pipeline, not to the atom layer. **Verdict: a genuinely new capability, size M for
the kind plus an unmeasured pipeline change.** The ideal's own fix — *"allocate no budget to conversion
nodes until a 17th kind is reviewed"* — is the right call and is what keeps this **soft**.

---

## 5. B3 — the booking D31 relies on does not exist, and nobody owns slot 5

This is the most important finding in the document.

**FACT.** `AllocationScope` has exactly four values —
`src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs:8`:

```csharp
public enum AllocationScope { Commander, DemonType, Aspect, UniqueDemon }
```

**FACT.** `passive-tree-ideal.md:59` (D31) says slot 6 comes *"after the item program takes 5"*, citing
`item-ideal.md:1443`. **Read that line in its section.** It sits under the heading **"Needs another
program — four"** (`item-ideal.md:1440`), and its Owner column reads **effect-atom + class-system**:

> `| 2 | A 13th atom kind or aptitude.* channel family, and a fifth AllocationScope, for D8 | effect-atom + class-system |`

The item program is not *taking* slot 5. It is **requesting** it from two other programs. This is
evidence rule 3 in `DESIGN-GATE.md` §3 — read the section, not the line.

**FACT, and worse.** The request is scheduled nowhere:

- `tasks/item-todo.md:2857` lists it under **"Carried, not scheduled"**: *"D8 — a 13th atom kind or
  `aptitude.*` channel family, and a fifth `AllocationScope` (effect-atom + class-system)"*.
- `tasks/item-todo.md:820` records the consequence: *"D8's aptitude-affix gate stays inert — correctly."*
- `tasks/class-system-todo.md` mentions `AllocationScope` five times, all describing the shipped four
  (`:103` P1.2, `:437`, `:448`, `:518`). **No task adds a fifth.**
- `tasks/content-stack-todo.md` has no entry for it either.

**Verdict: UNOWNED.** Three programs each name a different other program as owner, and no task list
carries it. Unowned work never happens, so D31 as written waits forever.

### 5.1 There is already a live guard waiting for it

**FACT.** `src/FusionRpg.Core/Items/Power/AptitudeAffixPrice.cs:30-32`:

```csharp
const bool AptitudeVocabularyLanded = false;
public static bool VocabularyReady => AptitudeVocabularyLanded && Enum.GetValues<AllocationScope>().Length > 4;
```

A **default-off toggle plus a self-updating count check** — a wiring gap by the exact definition, and a
well-built one: a fifth scope landing without the flag being flipped is caught by a test rather than
silently believed. Nothing here resists slot 5; it is waiting for it.

### 5.2 The second change, stated exactly

**FACT.** `AptitudeAllocation.cs:36-39`:

```csharp
public static AptitudeAllocation Single(AllocationScope scope, string aptitudeId, long points)
{
    if (!AptitudeCatalog.IsAptitudeId(aptitudeId))
        throw new ArgumentException($"unknown aptitude id '{aptitudeId}'", nameof(aptitudeId));
```

A scope is keyed `(AllocationScope, string)`, and the string is validated against the twelve aptitudes
and nothing else. **The exact change that lets a scope name a status:** make the id vocabulary a function
of the scope rather than a constant — `IsAptitudeId` for the four aptitude scopes, and
`StatusCatalogBootstrap.CreateDefault().All()` for a `StatusMastery` scope. One predicate swap in one
method, plus the same widening at the store's read side (§8). **Size S.** It does **not** mean weakening
the check: a status scope carrying an aptitude id must still throw.

---

## 6. B4 — the four mechanism-node wiring gaps, verified one by one

These gate the only node class doc 05 §3.5 proved actually works, so they are the critical path, not the
nice-to-have list.

### 6.1 Status → derived never composes — CONFIRMED, and it is a wiring gap

**FACT.** A status's derived-channel `StatMods` are written into the **primary** stat bag —
`src/FusionRpg.Injector/Effects/EffectRuntime.cs:81`:

```csharp
CheatState.Stats.Upsert(Core.Status.StatusStatPayload.ToModifiers(inst));
```

**FACT.** `ActorHub.ResolveDerived` folds only registered `IActorStatSubsystem`s, and the lawn registers
exactly **three** — `src/FusionRpg.Core/Stats/Derived/ActorHub.cs:145-155`:

```csharp
hub.Register(new Subsystems.RpgProgressionSubsystem(powerIndex));
if (aptitudeTuning is not null) hub.Register(new Subsystems.AptitudeSubsystem(...));
if (boundDerivedAtoms is not null) hub.Register(new Subsystems.AtomDerivedSubsystem(boundDerivedAtoms));
```

All three are constructed at `src/FusionRpg.Injector/CheatState.cs:47-55`. **None reads the status bag.**
So a status naming `combat.dodge.omni` writes into a bag `ActorDerivedSnapshot` never sees.

**Owner: `effect-atom` / the stats layer. Size: S.** `AtomDerivedSubsystem.cs` is 89 lines and is the
exact precedent — a fourth subsystem of the same shape, reading the status runtime instead of bound
atoms, needing no vocabulary change and no new attach point. **Verdict: WIRING GAP, hard blocker,
critical path.**

### 6.2 `stat.derived` declares `AtomTriggers.None` — CONFIRMED, and it is deliberate

**FACT.** `AtomKindRegistry.cs:535` gives `stat.derived` `AtomTriggers.None`. The rule behind it is at
`AtomKind.cs:129`: *"A permanent modifier declares no trigger at all — it is not event-driven."*

**INFERENCE.** So a node saying *"your damage scales with damage taken"* cannot be a `stat.derived` atom:
it must read per-hit state, and `stat.derived` is by construction the kind that cannot. The path that
does exist is `status.apply` on `OnDamageDealt` carrying a `ModifyStat` payload — which is exactly §6.1's
blocked path. **The two are one gap: close 6.1 and 6.2 stops mattering for most nodes.**

**Owner: `effect-atom`. Size: M** if a genuinely trigger-capable derived write is wanted — `stat.modify`
already took that route via `TriggerOptional: true` (`AtomKind.cs:152-160`), so the precedent exists.
**Verdict: WIRING GAP once 6.1 lands; a design question, not a wall.**

### 6.3 Battle's derived recompose seam is never called mid-fight — CONFIRMED

**FACT.** `BattleDerivedModifierLedger` ships (`src/FusionRpg.Core/Battle/BattleDerivedModifierLedger.cs`,
65 lines, idempotent by construction). `BattleRunState.RecomposeDerived` wraps it (`:147-150`). It has
**one** production call site — `BattleRunState.cs:313`, inside the aura loop — and the comment directly
above it says so (`:302-306`):

> *"Delivered once, at construction — a live mid-match toggle is T13's own job, not this one's."*

`BattleEffects.cs:228`'s `Ledger.Recompose` is a **different ledger** (`BattleStatModifierLedger`,
primary channels) and fires only for `defense`.

**Owner: `aura-skill`, T13, named by that comment. Size: S. Verdict: named deferred work, not a wall.**

### 6.4 The overlay resolver is default-off — CONFIRMED

**FACT.** `src/FusionRpg.Injector/Effects/OverlayCombatFeature.cs:13`:

```csharp
public static bool Enabled => EnvEnabled || CheatState.On(CheatToggleId);
```

with `EnvEnabled` reading `FUSIONRPG_OVERLAY_COMBAT == "1"` (`:10-11`). **A default-off toggle, so a
wiring gap.** It is lawn-only — Battle and Sim run the resolver unconditionally, so measurement is
unaffected. **Owner: injector / `combat-unification`. Size: S. Verdict: SOFT** — passive trees work in
Battle and in the web RPG regardless, and standalone-first (invariant 9) says the lawn may enrich a
feature but never gate one.

### 6.5 One more, not in the charter: `stat.derived` is `None` in Sim

**FACT.** `AtomKindRegistry.cs:534` — `new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full,
RuntimeState.None)`, with the comment *"SIM stays None — `SimEffectHost` still has no consumer."*

**INFERENCE, and it matters more than it looks.** The balance proof lives in Sim.
`DominanceGuard.Measure` takes `IReadOnlyList<AptitudeAllocation>`
(`src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:38`), so a mechanism node is not expressible as an
input to it — that is red-team F2, and it is a type-level fact, not a coverage gap.
`passive-tree-ideal.md:486` answers it correctly (*"`tools/CombatSim` and `BattleEngine` already drive
the real resolver — F2 is a wiring gap"*), and `tools/CombatSim` does exist. But driving mechanism nodes
through it needs a Sim consumer for `stat.derived`, which is this cell. **Owner: `effect-atom`. Size: M.
HARD for the measurement gate, not for shipping.**

---

## 7. B5 — `PowerLadderKMilli`, three files and one real risk

**FACT.** The field: `src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs:92` — `int PowerLadderKMilli = 0` —
documented at `:64-69` as *"Per-mille multiplier applied to `PowerLadder.Value(Θ)` — the balance
number."*

**FACT.** The read site: `src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs:463-464`:

```csharp
var pThetaValue = new PowerLadder(powerTuning).Value(theta);
result[key] = checked((int)((long)spec.PowerLadderKMilli * pThetaValue / 1000));
```

**FACT.** The authoring site: `src/FusionRpg.Core/Effects/Atoms/AtomJson.cs:66-70` — `kMilli` is required
and never defaulted.

**The arithmetic** (from `04-number-and-atom-binder.md:303-313`): at the tree's own share, the exact
coefficients are 1.205‰ / 3.616‰ / 6.027‰ / 8.438‰ at tiers 1 / 3 / 5 / 7. Stored as integers they become
1 / 4 / 6 / 8 — errors of **−17.0% / +10.6% / −0.4% / −5.2%**.

**Why that is not a rounding detail.** D26 (`passive-tree-ideal.md:62`) makes reward-per-point exactly
`b/5` at every tier, *by construction*. A −17% error at tier 1 against a +10.6% error at tier 3 is larger
than one whole tier step, so the property D26 exists to guarantee is destroyed at the shallow end — **and
it fails silently**, because the number on the sheet still goes up.

**The fix.** A `PowerLadderKMicro` sibling, mutually exclusive with the existing sources exactly as
`PowerLadder` already is (`ValueSpec.Validate`), dividing by 1,000,000 instead of 1,000. **Three files:**
`ValueSpec.cs`, `AtomJson.cs`, `AtomCompiler.cs`, plus `PowerLadderMagnitudeTests.cs`. **Owner:
`content-stack` (effect-atom; `ValueSpec` is T6.2's own surface — `tasks/seed-to-concrete-todo.md:2177`).
Size: S.**

> ⚠️ **One thing to carry into the change.** `result[key]` is `checked((int)…)` — an `int` magnitude on a
> `P(Θ)` path. `CLAUDE.md`'s table puts `int` per-mille at Θ=3,213, and the divide-by-1000-last rule
> already applies. Going per-million multiplies the intermediate by 1000, so the widen-before-multiplying
> discipline is doing real work here, not ceremony. It throws rather than wraps today; keep that.

---

## 8. B6 — the migration boundary, and the fix belongs one level up

**FACT.** `src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs:38-39` throws
`ArgumentException` on an unknown id.

**FACT.** It is reached **per row** from the store —
`src/FusionRpg.Data/Sqlite/RpgStore.Aptitudes.cs:130-132`:

```csharp
var allocation = AptitudeAllocation.Empty;
while (r.Read())
    allocation += AptitudeAllocation.Single(scope, r.GetString(0), r.GetInt64(1));
```

**INFERENCE, on D29/D30's own numbers.** Today the id space is twelve aptitudes and they never retire, so
this has never fired. Under D29 the generic corpus is 39 trees × 40 nodes = **1,560 nodes**, and D24
makes the catalog regenerable committed content. **One retired node id in one row makes the whole
`LoadAllocation` call throw**, so the actor does not load at all — rather than the node rendering red,
which is what D11 already promises for the analogous gear case (*"displayed as invalid (red), never
silently repaired"*).

**The fix, as the ideal already states it** (`:500`): reject once at an import boundary, never lazily per
load. Concretely — `LoadAllocation` skips-and-reports unknown ids (returning allocation plus an
unknown-id list), while a separate import/validation pass refuses a catalog that orphans a saved id.
**Owner: `class-system` (it owns `AptitudeAllocation`), with one `FusionRpg.Data` edit. Size: S.**

D18 makes this cheap to get right — respec is a full reset, so the escape hatch is a single transaction.
It does not remove the need for the rule.

---

## 9. B7 — Battle's trigger set, and doc 05 is half wrong here

**The charter asked me to verify by grepping `src/FusionRpg.Core/Battle/`. I did, and the answer needs a
correction.**

**FACT.** `grep -rn "OnDamageTaken\|OnDamageDealt\|OnSpawn\|OnDeath" src/FusionRpg.Core/Battle/` returns
**nothing**. That is the grep doc 05 ran, and taken alone it says Battle fires no triggers at all.

**FACT, and it changes the answer.** `src/FusionRpg.Core/Actions/BasicAttack.cs` declares
`public static partial class BattleEngine` in `namespace FusionRpg.Core.Battle` (`:8-18`, with a comment
explaining exactly why the file lives in a different folder). It raises two triggers:

- `BasicAttack.cs:124` — `Trigger = AtomTriggers.OnActivate`
- `BasicAttack.cs:174-181` — `Trigger = AtomTriggers.OnDamageDealt`, into `state.Host.Bag.OnEvent`,
  followed by `state.Host.Flush()`

A repo-wide grep for trigger raises (`Trigger = AtomTriggers.`) finds **exactly these two sites** outside
`FusionRpg.Contracts`.

**Corrected finding.** BattleEngine fires **`OnDamageDealt` and `OnActivate`** — landed by A18c, the
basic-attack adoption. It fires **no `OnDamageTaken`, no `OnSpawn`, no `OnDeath`**. Those three exist
only on the lawn, where the adapter maps them (`src/FusionRpg.Injector/Effects/EffectRuntime.cs:324,
327, 333`; producer gates at `:175-189`).

**What that means for passive trees.** An *on-hit* rider — the most common mechanism-node shape — is
**measurable in Battle today**. A reflect / thorns / *"scales with damage taken"* node is not, because
`OnDamageTaken` never fires there. On-kill nodes likewise.

**Owner.** The battle engine's trigger surface belongs to `battle-timeline`; A18c's precedent shows the
raise is a few lines at the site that already has the numbers in hand. **Size: M** — three triggers, and
`OnDamageTaken` in particular needs a decision about double-firing against the attacker's own
`OnDamageDealt`. The lawn already solved that at `EffectRuntime.cs:319` (*"when TakeDamage will also emit
`combat.hit` (bullet), skip `OnDamageTaken` from `*.damage`"*), so the prior art exists.

**Verdict: HARD for measuring reflect / anti-turtle / on-kill nodes; already satisfied for on-hit nodes.**

---

## 10. B8–B12 — what the decision set needs that no program owns

### 10.1 B8 — three `ssot-power-scale.md` §10 rows, against a closed program ⛔ UNOWNED

**FACT.** §10's inventory runs rows 1–28
(`docs/architecture/power/ssot-power-scale.md:600-641`). **There is no row for a passive tree** —
grepping the file for "passive" or "skill tree" returns nothing.

**FACT.** The rule is absolute: *"a power-shaped number that is not in this table does not have permission
to exist"* (`DESIGN-GATE.md:38`, and `AGENTS.md`'s Hard boundaries).

Three passive-tree quantities are power-shaped and need rows:

| Quantity | Shape | Precedent to argue from |
|---|---|---|
| `req(t) = 5·t(t+1)/2` — the tier requirement | triangular **cost** ladder | §10 row 6's cost-ladder exemption may already cover it — **check, do not assume** |
| `W(T) = b·T(T+1)/2` — tree power at tier `T` | triangular magnitude | Rows 20 / 21 (`PowerLadder`, `ChannelLadder`) |
| `Ws` — the soul → `Θ` weight, `Θ_node = Θ_actor + Ws·soulLevel` | linear offset into `Θ` | **Row 18** (`thetaOffset`, the species threat rung) — `passive-tree-ideal.md:480` already cites this precedent |

**FACT.** `tasks/power-todo.md` has **zero open tasks** (`grep -n "^- \[ \]"` returns nothing). The power
program is closed, so there is no active plan to hang these on. **⛔ UNOWNED — and it is simultaneously
the cheapest item on the list (three table rows, size S) and the one that gates specification itself.**
Nothing in the passive-tree program may be specced before these rows are reviewed.

### 10.2 B9 — the family roster: the tools shipped, the corpus did not

**FACT, counted this session.** Parsing all 503 files under `data/seed/demons/species/*/*.json`:
**841 entries, 699 distinct `family` tokens.** Top values: `undead` 64, `artillery-flora` 17,
`fungal-artillery` 16, `explosive-flora` 14, `unclassified` 13. `spec-roster-metrics.md:38` expected 19.

**FACT, and this is the part the red team missed.** A **closed 19-family roster already exists** —
`data/seed/demons/_generated/family-assignments.json`, 53 species keys, 19 distinct families: `base`,
`bucket`, `cactus`, `cherry`, `chomper`, `corn`, `dolls`, `double`, `fire`, `fruit`, `garlic`, `hypno`,
`ice`, `light`, `line`, `nut`, `pea`, `sun`, `sunflower`. It is the projection `decisions.md`'s
action-eligibility row already depends on, through `data/seed/actions/_generated/family-map.json`.

So there are **two family vocabularies**: an open one over the whole 841-entry corpus, and a closed one
over the 53-species subset the shipped 84-row `DemonSpeciesCatalog.Generated.cs` covers.

**FACT.** `family` is **open by contract**, not by accident —
`docs/architecture/demon-seed/spec-anchor-contract.md:58` marks it *CLASSIFIED, open — grows organically*.

**FACT.** The consolidation machinery is **BUILT**: `tasks/seedsmith-todo.md:1249` (`D2.1
family-extract`, **M**) and `:1291` (`D2.2 family-consolidate`, **M**), both `[x] BUILT + VERIFIED
2026-08-31`.

**Verdict: a run-and-decide gap, not absent work.** What is missing is (a) a decision to close the axis,
or to derive a closed roster beside it — which amends `spec-anchor-contract.md:58` and is `demon-seed`'s
call — and (b) one consolidation run over all 841 entries. **Owner: `seedsmith` / `demon-seed`. Size: M.
HARD for family trees only**, which blocks no other category — exactly why D27 (*"the roster ships
whole… curation is a build-order task"*) is safe.

### 10.3 B10 — two of the four gate quantities do not exist in `src/` at all

**FACT.** `grep -rn "element_mastery\|elementMastery" --include=*.cs src/` returns **four hits, all doc
comments** (`AptitudeTuning.cs:20`; `PointBudget.cs:13,15,22`). `grep -rni "almanacXp\|almanac_xp"
--include=*.cs src/` returns **nothing**.

**FACT.** `PointBudget.cs:13-15` names the owner in writing: *"Aspect's own source (`element_mastery`) is
owned by the demon program's `aspect-scope` module and does not exist yet."*

**INFERENCE.** `passive-tree-ideal.md:285` maps Aspect → elemental trees and DemonType → family trees, so
**both non-primary generic categories gate on a quantity with no producer.** The Commander scope
(`Θ_player`) and the UniqueDemon scope (specimen level) do ship, so the twelve primary trees and the
species trees have real gates today.

**Owner: the demon program (`aspect-scope`) for `element_mastery`; the almanac for almanac XP. Size: M
each. HARD for waves 2 and 4.**

> **And the deeper problem the red team's F5 raises is real:** one `req(t)` ladder facing four quantities
> that grow at different exponents. `08-effort-power-reconciliation.md` half-closed it — specimen levels
> now read the shared arithmetic curve — but two of the four still have no shape at all, because they
> have no code. **Gate on ONE index and convert the other three into it.** That is a passive-tree design
> decision, not a blocker on anyone else.

### 10.4 B11 — respec is free today

**FACT.** `RespecPolicy` (`src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs:24`) has **zero production
callers** — every reference is a test (`RespecPolicyTests.cs`) or a doc comment (`DiscardPolicy.cs:10`,
`AptitudeTuning.cs:40-41`). `pointEconomy.respecPrice` is parsed, and nothing charges it.

**FACT.** D18 also contradicts a lock: `decisions.md`'s class-system row says respec is *"available,
unlimited, and priced in a resource fighting also costs"*, while D18 prices it in **souls**.

**Owner: `class-system`. Size: S. Verdict: SOFT and PARALLEL** — trees ship; respec stays free until
someone charges for it.

### 10.5 B12 — two specced modules in nobody's plan ⛔ UNOWNED

**FACT.** `effect-pipeline-map.md` §3 rows 11 and 12 (`affix-power-class`, `affix-channel-weights`) were
added 2026-09-03 by owner decision, with full specs at
`docs/architecture/effect-pipeline/spec-affix-power-class.md` and `spec-affix-channel-weights.md`.

**FACT.** `grep -rn "power-class\|channel-weights" tasks/*.md` finds **two hits, neither a task**:
`content-stack-todo.md:3057` (prose inside ep-8's entry) and `item-todo.md:18` (*"X4 — L0 pool
composition … specced/unbuilt. Owner: effect-pipeline"*). **They appear in no program's task list**,
including their own program's combined plan.

That matters here for one specific reason: ep-8's own entry names `affix-channel-weights` as the module
that will supply `EligibilityResolver`'s call site. **B1's wiring gap is waiting on a module nobody has
scheduled.**

---

## 11. What I could not tick

Per `DESIGN-GATE.md` §5, stated rather than hidden:

- **I did not read research docs 01, 02, 03, 05, 07 and 08 end to end.** I read 06 and 09 in full and
  targeted the others by grep against the specific claims the charter named. Every claim I report from
  them was re-verified against `src/` here; claims I did not re-verify are not repeated.
- **I ran no test suite.** Sizes are estimated from comparable shipped modules (line counts, task-list
  S/M/L labels), not from doing the work. Where I say "three files", I named the three files.
- **I did not cost B2's second half** — rewriting `request.Components` before `Compute` runs, on both
  hosts. It is flagged as unmeasured rather than guessed.
- **`decisions.md` was read at rows 100–120 and the class-system / derived-write-lawn rows**, not end to
  end.
- **Nothing here rests on RECALL.** Every number was counted or read.

### Corrections to propagate

`passive-tree-ideal.md` §11.2 and D31 should be amended:

- `:494` — *"Land `spec-eligibility-tags.md`'s derived-tag registry first"* → **it landed** (ep-8, built,
  `AffixTags.cs`). The remaining work is a tag vocabulary and a call site (§3 above).
- `:496` and D31 (`:59`) — *"after the item program takes 5"* → **the item program does not own slot 5
  and has parked it.** The fifth `AllocationScope` is unowned (§5 above).
- `:500` is correct as written; §8 above only adds the call site (`RpgStore.Aptitudes.cs:130-132`).

Docs `02`, `03` and `04` each flag `DESIGN-GATE.md:40` as stale on the atom counts. **That has since been
fixed** — the gate now reads *"7 attach points, 16 kinds, 13 triggers … verified by counting,
2026-09-05"*, matching `AtomKindRegistry.cs:21,31,36` exactly. No action needed.
