# Audit — the atom effect pool: what selects it, and what is actually in it

**Measured 2026-09-02 against shipped code and shipped data.** Every number here is counted, not
estimated; the method is stated next to each so it can be re-run.

Commissioned by the owner's question, which is the right one:

> *"i don't know how current seedsmith select atom effect from where, what engine select them, LLM join
> it or not? … how many effect? did the system include primary stats modifier and other atom effect that
> affect game mechanism yet? did it include all derived stats yet? … without explicit definition, we
> don't know what in the atom effect pool and how can agent generate without an explicit pool?"*

**The short answer: the machine is complete and correct, the pool file exists and is wired — it just
holds 21 rows; and the SSOT document that was supposed to index it is three revisions out of date.**

**The explicit pool the owner asks about is `data/seed/atoms/*.json`**, read by seedsmith at run time
(2.3). So the shape of the problem is not *"the agent has no pool"* — it is *"the agent's pool is a
21-row FX demo set."* That is a content gap, not an architecture gap, which makes it far cheaper to fix.

---

## 1. ⭐ The headline, in one table

| Layer | State | Measured |
|---|---|---|
| **The machine** — kinds, attach points, triggers, validation, roll | ✅ **built and closed** | 12 kinds · 5 attach points · 8 triggers |
| **The channels it can address** | ✅ **built** | **11** primary · **267** registered derived |
| **The pool file + its read path** | ✅ **built and wired** | `data/seed/atoms/*.json`, loaded by `generate_affixes.load_eligible_atoms` — proven by dry run |
| **The atom content in it** — actual rows to draw from | ⛔ **essentially empty** | **21 atoms** in 17 families, all FX demo rows + 1 trait |
| **The designed library** — what the atoms *should* be | 📋 **authored, unbuilt** | ~83 families designed, **0 emitted** |
| **The SSOT index** | ⛔ **stale** | says 7 triggers / 8 primary / 99 derived — code has 8 / 11 / 267 |

**`stat.modify` has exactly one shipped atom, and `stat.derived` has exactly one.** So of 11 primary
channels **1** is reachable by shipped content, and of 267 derived channels **1** is.

---

## 2. How selection actually works — and where (if anywhere) the model joins

Traced through code, 2026-09-02. **Four steps, and the model is in exactly one of them.**

```
 1. ATOM        data/seed/atoms/*.json          kind + params{channel, op, amount}     AUTHORED DATA
      |                                          21 rows today
      v
 2. AFFIX       a NAMED bundle of >=2 atom ids                                         <-- THE MODEL
      |         seedsmith/adapters/effects/affix/                                          joins HERE
      v
 3. CONTAINER   pool[]{affixId, weight} + prefixRolls/suffixRolls                      AUTHORED DATA
      |         6 rows today
      v
 4. DRAW        Instantiator.Draw(container, lookupAtom, lookupAffix, rollSeed)        PURE CODE
                weighted sample, seeded, one-per-group exclusion                       no model
```

### 2.1 What engine selects — `Instantiator.Draw`, and it is pure

[`Instantiator.cs:180-231`](../../src/FusionRpg.Core/Effects/Atoms/Instantiator.cs#L180-L231). Not a
model, not a heuristic — a **seeded weighted sample** over `container.Pool`:

- Two independent budgets, `prefixRolls` and `suffixRolls`, each on its **own named RNG stream**
  (`AtomStreams.Pool + "." + budgetName + "." + containerId`) *"so the prefix draw and the suffix draw
  never share a sequence, and the same container always replays identically."*
- **One-per-group exclusion** — when a row is picked its whole group is removed, so a second tier of the
  same variant cannot come up.
- `ActionSeeder.Generate` ([`ActionSeeder.cs:35-73`](../../src/FusionRpg.Core/Actions/Seeding/ActionSeeder.cs#L35-L73))
  is a thin wrapper: `Draw` for the atoms, then a board-gated weighted pick for the target shape, then a
  composed name. Its own doc comment is explicit that the atom half is *"unchanged, only its visibility
  widened."*

**So the answer to "what engine selects them" is: deterministic code, from a container's own pool.**

### 2.2 Where the model joins — one step earlier, and it never picks a magnitude

`tools/seedsmith/seedsmith/adapters/effects/affix/`. The model authors a **named bundle**:

```python
AFFIX_SCHEMA = {"properties": {"name": {...}, "refs": {"type":"array","minItems":2}}, ...}
#: No magnitude field anywhere — `name` and `refs` only. `refs` names EXISTING atom ids from the
#: shared library; the model never invents an atom, only bundles ones that already exist.
```

Three properties worth stating, because together they are why this is a safe use of a model:

1. **It picks identity, not magnitude** — Law 2 held. The system prompt says outright: *"You never write
   a weight, a tier, or a magnitude; those are decided by tables you never see."*
2. **`affix_class` is derived, never authored** ([`derive.py`](../../tools/seedsmith/seedsmith/adapters/effects/affix/derive.py)):
   no trigger → `prefix`, trigger → `suffix`, both → `mixed`. The docstring gives the reason —
   *"a model that names its own class can contradict the bundle it just picked."*
3. **Both judgement fields are 3-way voted** — name and bundle composition, canonicalised by sorted atom
   id so *"the same set of picks in a different sampled order still counts as agreement."*

### 2.3 ✅ CORRECTED — the pool IS a JSON file, and seedsmith DOES read it

> **⛔ This section originally said *"there is no global atom pool anywhere in the system … nothing
> computes that list"*, and sorted it as a real gap. That was wrong.** Corrected 2026-09-02 on the
> owner's follow-up question (*"is it a json file or something that seedsmith read?"*), which is what
> prompted actually opening `generate_affixes.py` instead of reasoning from `prompts.py`'s docstring.
> The original claim was made from the *consumer* signature (`build_context(eligible_atoms=…)`) without
> reading the *caller*. **The caller computes it from disk.**

**The pool is `data/seed/atoms/*.json` — the shipped seed tree itself.**
[`generate_affixes.py:31-62`](../../tools/seedsmith/seedsmith/adapters/effects/affix/generate_affixes.py#L31-L62)
sets `ATOMS_ROOT = REPO_ROOT / "data" / "seed" / "atoms"` and `load_eligible_atoms()` walks it, returning
*"every atom id the real shipped seed tree carries, mapped to whether ITS OWN row declares a trigger —
read fresh each call."*

Three details that make this a *finished* seam rather than a stub:

- **Ids are derived, not read.** `derive_atom_id` mirrors `AtomRow.DeriveId` exactly —
  `family.t{tier}` or `family.{variant}.t{tier}` — *"so an id computed here always matches what the C#
  importer derives from the identical seed row."*
- **The trigger flag comes from the row's own `when.trigger`**, never a kind-level default, which is what
  lets `derive_affix_class` run on real data instead of a guess.
- **It refuses rather than degrading:** fewer than two eligible atoms is a `SystemExit`, not an empty run.

Its own docstring records that this was a known open question and that this module closed it:

> *"Neither this module's own spec nor any earlier task named where a run's own `eligibleAtoms` should
> come from — recorded as a genuine open question in `tasks/seed-to-concrete-todo.md`. Resolved here …
> the pool is every atom id the REAL shipped seed tree actually carries."*

**Verified by running it** — `python -m seedsmith.adapters.effects.affix.generate_affixes --dry-run`:

```
21 eligible atoms; no model calls made.
Eligible atoms this run may bundle (pick two or more): atom.critical-hunter.t1,
atom.fx-board-cherry.t1, atom.fx-butter-on-hit.t1, … atom.fx-spawn-zombie-ondeath.t1
```

**Sorted correctly: BUILT.** The pool mechanism is complete, wired, and provably running.

**The real gap is one level down, and it is a much better problem than the original finding claimed: the
tree it reads has 21 rows.** The generator is not missing a pool *mechanism* — it is missing a pool
*corpus*. The fix is content generation, not plumbing.

---

## 3. How many effects — the counts, and where each comes from

### 3.1 The machine — built, closed, correct

| Thing | Count | Source of truth (code) |
|---|---:|---|
| Attach points | **5** | `AtomKindRegistry.AttachPointCount`, structural |
| **Kinds** | **12** | `AtomKindRegistry.KindCount`, structural |
| Triggers | **8** | `AtomTriggers.All` — `OnActivate` added by A18b |
| Ops (`stat.modify`) | 3 | `Flat` · `Increased` · `More`; `Override` refused at bind |
| Ops (`stat.derived`) | 4 | `Flat` · `Increased` · `Replace` · `Flag` — **no `More`** |

The 12 kinds: `stat.modify` · `stat.derived` · `resource.delta` · `resource.economy` · `status.apply` ·
`status.clear` · `shield.grant` · `spawn.entity` · `board.action` · `grid.spawn` · `grid.clear` ·
`box.set`.

### 3.2 The channels — this is the answer to "did it include all derived stats yet?"

| | Count | Source |
|---|---:|---|
| **Primary** channels (`stat.modify` may address) | **11** | `StatChannels.All` ([`ModifierOp.cs:26`](../../src/FusionRpg.Core/Stats/ModifierOp.cs#L26)) — `hp` `maxHp` `atk` `defense` `arm1` `arm1Max` `arm2` `arm2Max` `attackInterval` `produceInterval` `zombieSpeed` |
| **Derived** channels registered (`stat.derived` may address) | **267** | `DerivedStatRegistry.CreateDefault().AllRegistered.Count`, asserted in **three** tests |

**Enumeration cross-check:** expanding `data/seed/derived-stats/catalog.json`'s 53 families over their
declared axis widths (`none`=1, `element`=7, `status-category`=4, `action-category`=5, `resource-id`=6)
gives **exactly 267** — so the catalog and the registry agree, and the method below is sound.

### 3.3 ⛔ The content — 21 atoms, and only one touches each stat kind

Counted from `data/seed/atoms/*.json`:

| File | Atoms |
|---|---:|
| `fx-board.json` | 7 |
| `fx-core.json` | 6 |
| `fx-status.json` | 7 |
| `trait-critical-hunter.json` | 1 |
| **Total** | **21** in **17 families** |

**One genuinely good thing:** all **12 kinds** have at least one atom, so the demo corpus exercises the
whole machine — every kind is proven executable end to end. That is why the machine can be called built.

**And the defect, in two rows:**

| Kind | Shipped atoms | Channels it could address | Reached |
|---|---:|---:|---:|
| `stat.modify` | **1** (`atom.fx-passive-atk-flat` → `atk` Flat 10) | 11 | **1 (9%)** |
| `stat.derived` | **1** (`atom.critical-hunter.t1` → `combat.crit.rate.omni` Flat 150) | 267 | **1 (0.4%)** |

Containers are the same story: **6 rows total** (`patron.aura`, `trait.critical-hunter`, and four
`item.fx-*` demos).

**So: does the system include primary stat modifiers? The KIND does, fully — the CONTENT is one atom.
Does it include all derived stats? The kind can address all 267 — one is authored.**

---

## 4. ⭐ The designed library, and the 63 channels it still does not reach

[`atom-family-library.md`](../architecture/effect-atom/atom-family-library.md) §3 is the authored design
— **not shipped**, and its own §3.2 carries a quarantine banner. Designed families:

| Section | Kind | Families |
|---|---|---:|
| §3.1 | `stat.modify` | 14 |
| §3.2 | `stat.derived` | 28 (+4 status-channel) — *"~980 rows"* |
| §3.3 | `resource.delta` | 6 |
| §3.4 | `status.apply` | 21 |
| §3.5 | board + economy | 14 |

**Coverage against the 267 registered channels** — method: extract every `` `family.*` `` and
`` `status.x.{...}` `` token named in the library, match against `catalog.json`'s 53 families, weight by
axis width:

| | Channels | Share |
|---|---:|---:|
| Reachable by a **designed** atom family | **204** | 76% |
| **Not reachable by any atom family** | **63** | **23%** |

### The 23 uncovered families, and why this matters right now

| Channels | Family | Why it hurts |
|---:|---|---|
| 6 | `resource.max` | |
| 6 | `resource.regen` | |
| 6 | `resource.efficiency` | |
| 6 | `resource.restore` | **All four are this session's Phase 0 work.** The stat layer now governs six resources correctly — and no atom can address any of it |
| 5 | `skill.cooldown` | **The action corpus prices cooldown through the rung table.** No atom can modify one |
| 5 | `skill.effectiveness` | same — the action program's own scaling channel |
| 4×4 | `status.duration` · `.durationReduction` · `.intensity` · `.intensityReduction` | status *magnitude* control, entirely unaddressable |
| 1 | `turn.speed` · `turn.haste` | the battle-timeline tempo levers |
| 1 | `move.range` | the movement-action channel |
| 1 ea | 7 × `progression.*` | `bonus.maxHp/atk/defense/arm1/arm2`, `power`, `realm`, `xpRate`, `breakthroughSuccess` |
| 1 | `combat.heal.power` | expected — retired 2026-09-02, kept registered as a shim |

**The action-corpus program is the immediate victim.** A generated support action that restores qi, a
movement action that changes `move.range`, a Focus-flavoured action that cuts a cooldown — **none of the
three has an atom family to be built from**, even in the design document.

---

## 5. Real gaps found while auditing

Sorted with the words the design gate requires.

### 5.1 ⛔ REAL GAP — `stat.derived` never checks that its channel is registered

`stat.modify` is protected. `AtomKindRegistry.Validate` rule **G6** rejects a channel that is not one of
the 11 primary channels, and its own comment explains why: *"an unknown PRIMARY channel used to pass
validation and then write nothing, because `ModifierBag.Upsert` only checks for a non-empty name."*

**`stat.derived` has no equivalent, and the code says so out loud.**
[`AtomRowValidator.cs:296`](../../src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs#L296):

```csharp
var kind = composeKindOf(channel);
if (kind is null) return AtomRejection.Ok; // unregistered channel is G6's job, not this check's
```

But G6 is scoped `if (string.Equals(kindId, "stat.modify", ...))` — **twice**, at
`AtomKindRegistry.cs:64` and `:79`. It never runs for `stat.derived`. A grep for any registered-channel
membership check across `src/FusionRpg.Core/Effects/` returns nothing.

**Consequence:** a `stat.derived` atom naming `combat.crit.rat.omni` (one letter off, out of 267 valid
ids) validates, binds, compiles, and **writes nothing forever.** That is verbatim the failure the kind's
own D6 quarantine note says the module exists to prevent:

> *"A bind would have been accepted and then done nothing forever, which is the exact failure this
> module exists to prevent."*

Cheap to close: the same shape as G6, against `DerivedStatRegistry`. It is more valuable here than on
`stat.modify` by a factor of **24** — 267 ids to typo instead of 11.

### 5.2 ⛔ REAL GAP — the SSOT index is stale, and nothing pins it

[`atom-catalog-ssot.md`](../architecture/effect-atom/atom-catalog-ssot.md) is the document that is
supposed to answer *"what is in the pool."* Measured against code:

| Claim in the SSOT | Code | Drift |
|---|---|---|
| §4.1 *"Primary — **8**, and only these"*, growing to 11 | **11**, shipped | the growth landed; doc still describes it as pending |
| §4.2 *"Derived — **99** pre-registered"* | **267** | **+168** |
| §9 *"Triggers — **7**"* | **8** | `OnActivate` (A18b) missing |

**The root cause is structural, not clerical.** `spec-derived-stat-sheet.md` carries the same numbers and
**cannot** drift, because `ElementHubDocDriftTests.StatSheetCountsMatchGeneration` pins it to
`registry.AllRegistered.Count` and has a companion planted-drift test. `atom-catalog-ssot.md` has **no
such test** — grepping the suites for it returns only incidental mentions. It drifted from 99 to a
reality of 267 across three separate channel expansions, unnoticed, because nothing was watching.

### 5.3 Not a gap — the model's role is correctly bounded, and so is its pool

Worth recording so a later session does not "fix" it. The affix adapter is the only model stage that
touches atoms, and it is well-shaped: identity only, no magnitudes, derived class, 3-way vote on both
judgement fields, an id derivation that mirrors the C# importer, and a refusal rather than a degraded run
when the pool is too small.

**The pool wiring is equally fine** (2.3). **The only thing wrong is that the seed tree it reads holds 21
rows.** Nothing here needs redesigning — it needs feeding.

---

## 6. What this means for the action-corpus program

The action-corpus idea phase assumes actions are assembled from atoms. That assumption is **sound in
architecture and unfunded in content**:

- ✅ The **machine** is ready — 12 kinds, deterministic seeded roll, `ActionSeeder` already wraps it.
- ⛔ The **pool** is 21 FX demo atoms. `S1 distribution-planner` would be planning quotas over a corpus
  that cannot express attack, defense, support, movement **or** status at any variety.
- ⛔ The **channels the action program specifically needs** — `skill.cooldown`, `skill.effectiveness`,
  all four `resource.*` families, `turn.*`, `move.range` — are the exact ones with **no designed family
  at all** (§4).

**A model-free build step is missing from the pipeline, and it belongs before every other stage:** emit
the atom library from the registry, deterministically, the way `AffixLibraryGenerator`'s own comment
already prescribes for one level up —

> *"do not hand-author what a pure function can generate"* — `atom-family-library.md` §2

That step costs **zero tokens**, produces a reviewable diffable corpus, and is the thing that turns
*"the agent has no explicit pool"* into *"the agent has 267 channels' worth of pool, generated."*

---

## 7. What I could not find

- ~~*"No evidence of any caller that computes `eligible_atoms`."*~~ **Withdrawn 2026-09-02 — it was
  wrong.** `generate_affixes.load_eligible_atoms` computes it from `data/seed/atoms/*.json`. The original
  search matched on the module *name* and never opened the module. Kept here rather than deleted, because
  the failure mode is the reusable lesson: **a grep for references is not a substitute for reading the
  entry point.**
- **No test anywhere pins `atom-catalog-ssot.md`'s counts.** Confirmed by grepping `tests/` for the
  filename — the only hits are `SpecChannelClaimTests` (which scans it for channel tokens, not counts).
- **I did not determine whether the 63 uncovered channels are uncovered deliberately.** Some plausibly
  are — `progression.realm` and `progression.power` are documented stubs returning 1.0. The four
  `resource.*` families and `skill.*` are almost certainly not, since they postdate the library document.
  **That call belongs to the effect-atom program's owner**, and §4's table is the input to it.
