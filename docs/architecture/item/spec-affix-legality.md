# Spec: `affix-legality`

**Module id:** `affix-legality` · **Program:** [item](../item-map.md) · **Build order:** 8 of 21
**Depends on:** `slot-roles` (3), `rarity-bands` (7) · consumes effect-pipeline `eligibility-tags` (8) and **D28**
**Rulings:** **D29 (the ilvl→tier ladder)**, **D3 (the affix-family relocation)**, D8, D26, §2b.1 · lane [ssot-affixes.md](ssot-affixes.md)

## Objective

Decide **which affix may roll where, how high, and what the result is called**. Four artefacts: the
role × affix-group matrix, the tier bands' ilvl ladder, the frame/side/runtime filters, and **the item
naming grammar** — plus one derived table, `item_role_family`, that nobody authors.

⛔ **Naming was added 2026-09-04 because nothing in the program owned it.** I8 owns *"item naming from
affixes"* (`ssot-affixes.md:25`), I8 is this lane, and this spec mentioned naming zero times while
`spec-item-card.md:302` tested a *"generated name"* it could not produce. **Without it every dropped item
is nameless.** See "⛔ Item naming".

**Users:** 9 (`item-power-reads`), 10 (`item-card` — renders the name this module composes), 11
(`drop-volume`), 13 and 21 (both generators respect legality), 15 (a reroll draws from `weight > 0` only,
and must not rename the item), 17 (`uniques` break these rules and bypass the grammar on purpose),
20 (`item-surfaces` — the inventory row and the loot toast).

## Design

### ⭐ `item_role_family` is DERIVED — the source already exists and is complete

`ssot-equip-slots.md:429-435` declares `item_role_family(role_id, frame, family_id, max_tier)` and
d4 §5.2 sizes it at **~1,100 hand-authored cells**. §2b.1 resolves it: **derived, with a small
override list.** *"One override beats 1,100 cells."*

**Verified: the derivation source is authored, complete, and already in the corpus.** All **98** affix
families in `data/seed/items/affix-families/` (15 files) carry `roles`, `frames` and `side` — 98 of 98,
no gaps — and together they name **656 legal `(role, family)` pairs** out of `98 × 16 = 1,568`
possible cells.

```text
item_role_family := for each family f in affix-families/*.json
                      for each role r in f.roles
                        for each frame m in f.frames
                          emit (r, m, f.id, maxTierFor(r, f))
                    minus the side filter        (f.side vs the base type's side)
                    minus the runtime filter     (f.kindId's RuntimeSupportMatrix)
```

This is the same decision `eligibility-tags` made for tags and `affix-power-class` made for class —
**derive from the one place the fact already lives**, so a second home cannot contradict it. And it is
reversible in the safe direction: a family that resolves nowhere is *excluded*, so the derived table
can only ever be too narrow, and a missing affix is visible where a wrongly-admitted one is not.

⚠ **Zero of the 98 families carry a `maxTier` field**, so `maxTierFor` is a rule plus overrides, not
data.

### ⛔ "Too narrow is safe" is false — the derivation's omissions are the balance surface

**Keep the derivation. Reject the safety claim above it.** *"The derived table can only ever be too
narrow"* is true and it is not a defence, because **the shape of the narrowing is what picks the build.**
This repo's own failure register says both halves of that:

| `docs/research/game-design/05-failure-modes.md` | Says |
|---|---|
| §1 (`:7-22`) | D2 Hell immunities across 703 monsters — Cold **137**, Poison 131, Fire 113, Lightning 105, **Magic 11**. *"which is why Hammerdin won Hell — a data table picked the winning build, not a designer."* **Nobody authored it; the counts did** |
| §4 (`:89-93`) | *"Tag absence is a stat… **omitting a tag is not neutral — it is a defensive buff**"* |

**Measured over the shipped corpus, the derivation is a Hammerdin-shaped distribution.** 656 of
`98 × 16 = 1,568` `(role, family)` cells are legal — **58.2 % excluded by omission**, and the exclusions
are not spread:

| Role | Legal families | Share of the 98 |
|---|--:|--:|
| `jewel-major` | **91** | 92.9 % |
| `jewel-minor-a` · `-b` | **84** each | 85.7 % |
| `armament-secondary` | 54 | 55.1 % |
| `armament-primary` · `sense` · `head-guard` | 45 each | 45.9 % |
| `manipulator` | 40 | 40.8 % |
| `core-guard` · `footing` | 38 each | 38.8 % |
| `mantle` · `girdle` | 31 each | 31.6 % |
| `standard` | 10 | 10.2 % |
| **`ward-array`** | **9** | 9.2 % |
| **`retinue`** | **7** | 7.1 % |
| **`infusion`** | **4** | 4.1 % |

At the group level, **106 of the 240 `(role, group)` cells are occupied — 44.2 %.** Pielou J over roles
is **0.919**; Gini over roles is **0.354**, over groups **0.206**. `jewel-major` sees **23×** the family
breadth of `infusion`, and `infusion` and `retinue` are **both in D3's twelve-role hybrid core** — the
two newest roles are the two thinnest, which is exactly the fingerprint of omission rather than design.

⚠ **This is not yet a claim that the corpus is wrong.** A narrow role can be correct — `jewel-minor-a`/`-b`
are *deliberately* wide and tier-capped instead (§2.5). **The defect is that nobody is measuring**, so a
skew introduced by an authoring fleet is indistinguishable from one a designer chose.

#### What this module ships instead of the safety claim

**A distribution metric over the derived table and the group-weight matrix, published as a CI artefact.**
The metrics already exist and are the right ones — reuse, do not re-invent:

| Metric | Where | Change needed |
|---|---|---|
| `Distribution/Evenness` (Shannon · Pielou · Simpson · richness) | `tools/seedsmith/seedsmith/metrics/distribution.py:95-129` | none to the metric |
| `Distribution/Inequality` (Gini over the sorted count vector) | same file, `:142-175` | none to the metric |
| `_observed_count` | same file, `:18-26` | ⭐ **the one edit** — it understands `kind:*` and `role:*:base-type` only. Add `role:<roleId>:affix-family` and `group:<groupId>:affix-family` dimensions |

- **Both ship `gates = False`**, matching the metric family's own discipline (*"nobody can name a correct
  Pielou value in advance"*, `distribution.py:1-10`). **Measure-only is the point** — a threshold picked
  today would be the same unmeasured guess the corpus already contains.
- **Two dimensions, not one:** the derived `(role, family)` table *and* `role-affix-weights.v1.json`'s
  group-weight matrix. A role can be broad in families and still have every one of them behind a
  near-zero group weight, which is the same defect one layer down.
- **Published as a CI artefact**, not printed and lost: `docs/research/item/_affix-distribution.json`,
  regenerated by `ItemSeedValidator` and uploaded by the workflow that runs it. ⚠ **No `.github/workflows`
  step uploads an artefact today** (`grep upload-artifact .github/workflows/*.yml` → 0 hits), so the
  upload step is new work and is named here rather than assumed.

**Standing: report-only, with a named finding.** `AffixDistributionSkew` is a **lint**, not a reason code
— it names the role, its family count, its Pielou/Gini contribution and its rank. §2b.1's closed 33-code
list does not grow for a measurement (§6.3 is the precedent: authoring lints are warnings and are not
reason codes).

### The one override — and §2.5's third pricing mechanism is FALSE in the corpus

`ssot-equip-slots.md:129` caps the twin minor jewels: *"`item_role_family.max_tier = 3` for every
family on both minor jewels, against `5` on `jewel-major`."* That is the only per-`(role, family)`
granularity I2 actually uses, which is exactly why deriving the rest is safe.

**But §2.5 prices the twins with *three* mechanisms, and the third does not hold.** It claims *"the six
strongest families — `bulwark`, `savagery`, and the four shield families — are **absent from the
minor-jewel family list on every frame**."* Measured against the corpus:

| Family | Legal on `jewel-minor-a` / `-b`? | §2.5 says |
|---|---|---|
| `atom.shield-capacity` · `-regen` · `-toughness` · `atom.shld-cycle` · `atom.shld-surge` | **no** | absent ✅ |
| `atom.shield-pen` | **no** | absent ✅ |
| **`atom.bulwark`** | ⛔ **yes, both** | absent ✗ |
| **`atom.savagery`** | ⛔ **yes, both** | absent ✗ |

Two of the six are legal on both twins today. **The override list therefore has two entries, not one:**
`max_tier = 3` on the twins, **and** `bulwark` / `savagery` removed from both twins' family lists —
or §2.5 is wrong and should be amended instead. ⚠ **Module 3 owns `ssot-equip-slots`; this module
reports the mismatch and applies whichever way it is settled.** Note that `classes.v1.json`'s
`_meta.designNotes` independently bars both families from every *implicit* slate because they use the
`More` operator, which is evidence for removing them rather than amending §2.5.

### D29 — the ilvl→tier ladder is settled, and the window rule travels with it

Two tables existed for one thing (d4's collision **C3**). **D29 ships I12's:**

| Tier | Minimum ilvl (**I12, ships**) | I8 §4.6 (**rejected**) |
|---|--:|--:|
| t1 | 1 | 1 |
| t2 | **1** | 12 |
| t3 | **8** | 25 |
| t4 | **18** | 40 |
| t5 | **32** | 60 |

`ssot-generation.md:370-376` against `ssot-affixes.md:453-459`. D29's reason: *"tier saturates and that
is correct… growth past t5 is carried by `contentScale`"*, which is built —
`InstanceProducer.cs:47` computes `ContentScale.Milli(thetaContent)` and multiplies every rolled
magnitude by it. I8's t5@60 delays the last band without adding growth.

⭐ **And the same ruling settles the *window* rule, which is a second collision nobody named.**

| | I8 §3.5 — **sliding window** | I12 §4.2 — **collapsing envelope** |
|---|---|---|
| At ilvl 40+ | offers the top tier and the two below; **t1 falls out** | keeps everything the rarity window allows |
| Rule | `[ilvlCeil-2, ilvlCeil]` | `env.maxTier = min(band.MaxTier, maxTierAt(ilvl))`; `env.minTier = min(band.MinTier, env.maxTier)` |

**I12's travels with I12's table.** Three reasons: it is the rule the pipeline implements and I12 names
it *"the anti-double-gating rule"*; a slide is a ceiling behaviour on the low end, which is what D29
rejected on the high end; and `contentScale` already makes a t1 roll at ilvl 500 a large number, so
dropping t1 removes variety and removes nothing else. **`ssot-affixes.md` §3.5 and §4.6 are stale.**

⚠ **One I12 behaviour must survive into this module:** if the narrowed envelope leaves fewer drawable
groups than the roll count, **narrow the count and record `envelope_narrowed`** — never reject a legal
drop from legal content, and warn the author at import instead of the player at drop.

### D3's relocation — record the inputs, and the prose overstates the work

D3 drops **`ward-array` (90) + `head-guard` (60) + `sense` (50) = 200‰** for hybrid, and states *"the
families are not lost, only the slots"* — their clusters relocate to new hosts at reduced `max_tier`,
with **I2 choosing the hosts** and this module recording the inputs.

**The inputs, measured from the corpus:**

| Dropped role | Families legal there | Groups |
|---|--:|---|
| `ward-array` | **9** | `g-armour` ×4 (`plating`, `carapace`, `arm-riveting`, `arm-welding`), `g-shield-stat` ×5 |
| `head-guard` | **45** | `g-ward` ×7, `g-precision` ×7, `g-evade` ×7, `g-sustain` ×7, `g-on-death` ×7, `g-life` ×6, `g-armour` ×4 |
| `sense` | **45** | `g-precision` ×7, `g-ward` ×7, `g-evade` ×7, `g-affliction` ×7, `g-on-hit` ×7, `g-life` ×6, `g-armour` ×4 |

⚠ **The `head-guard` row was wrong and the total was right.** An earlier revision printed `g-sustain ×6`
and summed to 44 against a stated 45. Re-measured over `data/seed/items/affix-families/**`: `g-sustain`
on `head-guard` is **7** — `regeneration`, `cleansing`, `warded`, `sust-husk`, `sust-callus`,
`sust-freshgraft`, `sust-grit` — and `7+7+7+7+7+6+4 = 45`. `g-life` is the six-member group (it has six
families in the whole corpus, not seven).

⭐ **No family is orphaned.** Every family legal on one of the three dropped roles is **already** legal
on at least one of the twelve hybrid-core roles — measured across all 98, count of orphans: **0**.

> **So D3's relocation is not "find new hosts". It is "reduce `max_tier` on hosts that already have
> them."** The families survive the drop untouched; what a hybrid loses is only the 200‰ of budget, and
> the *tier* reduction is what prevents a hybrid recovering the lost breadth for free through
> `core-guard` and `jewel-major`. **D3's prose describes a bigger job than the corpus needs**, and this
> spec records that so module 3 does not go looking for hosts that are already there.

`ssot-equip-slots.md` §4.2's existing pattern is the precedent and the shape: `ward-array`'s shield
families relocate to `core-guard` at **`max_tier = 3`** against `ward-array`'s `5`. Applying the same
reduction to the other two clusters is the override list's third and fourth entries — **module 3
picks the reduced tiers; this module applies them, through `role-relocation.v1.json` (see "What is not
decided").** ⚠ `spec-slot-roles.md:80-84` still says module 3 *"chooses the hosts"*; the 0-orphan
measurement above means there are no hosts to choose, only tiers. **That correction is owed to module 3's
spec, not absorbed here.**

### The prefix/suffix split — shipped, and shipped differently from the ask

`ssot-affixes.md` §5.2 asks for *"two new columns, and both nullable… both NULL ⇒ today's behaviour
exactly"*, and §9.12 raises it as the formal ask-first. **It landed, and it landed `NOT NULL`:**

```text
effect_container  … prefix_rolls INTEGER NOT NULL DEFAULT 0,
                     suffix_rolls INTEGER NOT NULL DEFAULT 0    RpgStore.Containers.cs:28-29
rarity            … prefix_rolls, suffix_rolls                  RpgStore.Containers.cs:57-58
ContainerRow      … PrefixRolls, SuffixRolls                    ContainerRow.cs (record body)
```

So **§5.2's whole NULL semantics are moot** — there is no "both NULL" state and no "one set, one NULL
⇒ rejected" case. The default is `0`, and `0 + 0` is the no-pool container. `pool_rolls` no longer
exists as a column; the sum is the count.

**The class itself is derived, not authored**, exactly as §3.1 recommended:
`AffixValidator.ResolveClass` returns an authored class if present and otherwise derives it from the
concrete refs' kinds, collapsing to `Mixed` when they disagree
(`src/FusionRpg.Core/Effects/Atoms/AffixValidator.cs:131-141`). The corpus authors no `affixClass` and
the seed contract rejects one (`seed-contract.md:301-323`).

### Hybrid affixes — I8 §4.10's "not expressible" is FALSE, and the schema is the reason

I8 §4.10 (`ssot-affixes.md:666-686`) rules hybrids a **fixed-core-only** mechanism, on the grounds that
the rolled route is *"not expressible — `effect_container_pool` draws atoms independently; nothing says
these two come together and count once against the quota"*, and asks for a future nullable `bundle_id`
column.

**Opened the schema. The roll unit is not an atom, it is an affix, and an affix already bundles:**

| Claim | Code | Verdict |
|---|---|---|
| the pool draws *atoms* | `effect_container_pool(container_id, affix_id, weight, group_key)` — the row keys on **`affix_id`** | ⛔ **stale** |
| nothing bundles two atoms as one draw | `effect_affix_ref(affix_id, seq, …)` is 1:N, and the schema comment says *"a hand-authored **multi-ref bundle** is the exception"* | ⛔ **stale** |
| nothing makes a bundle count once against the quota | `ContainerValidator.cs:130-137` — a multi-ref affix **must** declare an explicit pool `Group`, and the quota counts groups | ⛔ **stale** |
| `bundle_id` is needed | — | **not needed.** `affix_id` *is* the bundle id |

Schema at `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:44-50` (pool) and `:66-83` (`effect_affix`
/ `effect_affix_ref`). **`ssot-affixes.md` §4.10's route table is stale for the same reason §4.9 is** —
it was written before the affix schema landed as the roll unit.

**So: rolled hybrids are legal here, and this module states their three rules.**

| Rule | Why |
|---|---|
| A hybrid affix is **one** `effect_affix` with 2+ `effect_affix_ref` rows, and **must** carry an explicit `group_key` | `ContainerValidator.cs:130-137` rejects it otherwise with `BadParamValue` |
| Its class is **`Mixed`**, derived not authored | `AffixValidator.ResolveClass:131-141` — two refs of different kinds collapse to `Mixed`. The corpus authors no `affixClass` and `seed-contract.md:301-323` rejects one |
| ⚠ A `Mixed` affix counts as a drawable group **against both the prefix and the suffix quota** | `ContainerValidator.cs:140-143`. A pool of nothing but hybrids satisfies `prefix_rolls` and `suffix_rolls` with the same rows — and then `an_item_with_three_or_more_rolls_has_at_least_one_of_each_class` is met by an item that has neither. **The anti-stat-stick rule must count *resolved refs*, not affix classes** |

⚠ **Legality of a hybrid is the intersection, never the union.** A two-ref affix is legal on `(role,
frame, side)` only where **every** ref's family is legal there. The union reading would let a hybrid
smuggle `swiftness` onto a plant item through a legal partner — the exact hole `AffixNotLegalHere`
exists to close.

**Not decided here:** whether v1's *generated* pools contain any hybrid at all. §4.10's own reason for
declining stands — *"a hybrid roll is mostly a name"* — and the naming grammar below gets that from one
high-tier family word. **This module makes them legal and priced; module 13 and module 17 decide whether
to author any.**

### Crafted-only affixes — `weight = 0`, and every property I8 claims is shipped

I8 §4.11 (`ssot-affixes.md:687-708`) needs no schema change and, unusually, is **correct in every
detail.** Verified rather than assumed:

| §4.11 claims | Code | |
|---|---|---|
| a `weight = 0` row is kept and never drawn | `ContainerValidator.cs:139` — `var isDrawable = row.Weight > 0;` | ✅ |
| `PoolRollsExceedGroups` counts **drawable** groups only, so a crafted-only group cannot cause a silent under-fill | `:151-161`, counting `drawablePrefix`/`drawableSuffix` | ✅ |
| `UnsatisfiablePool` still fires if *every* row is zero | `:147-149` | ✅ |
| a crafted affix is an ordinary atom — same tier, same one-per-group, same naming | it is the same row shape | ✅ |

**This module's three obligations, since nothing else states them:**

1. **A `weight = 0` row is still subject to every legality filter.** Frame, side and runtime run before
   weight, so a craft cannot place `swiftness` on a plant item by zeroing its weight. Otherwise
   crafted-only becomes the legality bypass.
2. **The reroll rule is a legality rule, and it is enforced here.** *"A reroll draws from `weight > 0`
   only"* — module 15 implements it, this module owns the predicate. If a reroll sees the full pool,
   crafted-only affixes become findable by spamming reroll and the whole category evaporates
   (`ssot-affixes.md:704-707`, §9 item 7).
3. **A crafted-only affix may sit outside the item's ilvl window** — the craft is not the pool draw, so
   D29's ladder and I12's envelope constrain the *draw*, not the *placement*. ⚠ It may **not** sit
   outside the `(role, frame, side)` matrix. Strength is I6/I7's to price; legality is not.

### ⛔ `stat.derived` is no longer quarantined — §4.9 is stale, and it is the biggest change

`ssot-affixes.md` §4.9 states the wave-1 constraint at full strength: *"All 12 element/crit/shield
families plus the 4 status-channel families are `stat.derived`, and `stat.derived` is quarantined
`None/None/None` today (D6)"* — 16 of 30 prefix families dead, §9.14 calling the lift *"the single
largest input to this lane."*

**It shipped.** `AtomKindRegistry.cs:255` declares
`RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None)` for the kind declared
at `:226` — Battle **Full**, Lawn **Full**, Sim None, with the executor named in the comment above it
(`AtomDerivedSubsystem` on the injector's `ActorHub` at the order-350 `foundation.effect` slot), and
267 registered derived channels (`:53`).

| Consequence | Where |
|---|---|
| `g.ward`, `g.elem-power`, `g.precision`, `g.evade`, `g.shield-stat` are **live** | §4.1's five ⛔ prefix groups |
| The `RuntimeUnsupported` import rejection for *"every `stat.derived` family in wave 1"* must be **re-derived from `AtomKindRegistry`**, never from the document | §6.1 |
| The same stale fact is baked into a **frozen** registry: `classes.v1.json` excludes 32 families citing D6 | **module 6** owns that repair |
| Sim stays `None`, so a `stat.derived` affix must still be refused for a sim-target container | unchanged |

⚠ **`warding` / `resilience` are a separate rule and are *not* unblocked by this.** G8 is about
`stat.modify` on `defense` at a non-`match` scope, not about `stat.derived`. §4.7's ban stands: they
are legal only on the commander's `standard` slot — which **D14 puts out of scope**, so in practice
they are legal nowhere in v1 and must be refused at **import**, not at bind.

### Tags — module 8 of effect-pipeline is the consumer, and D28 is what makes it true

`eligibility-tags` decided *"an affix's tags are DERIVED from its refs' atoms."* **F5 broke that:**
`AtomRow.TagsJson` carries generator provenance only — `FamilyExpansion.cs:194-198` builds
`{ generatedFrom, generator: "E43" }` and nothing else. The thematic tags exist, but on the affix-family
seed entries: **`offensive` ×41, `defensive` ×40, `utility` ×17** across the 98 families — measured,
and matching §2f.1 F5 exactly.

**D28 fixes the data rather than the derivation:** E43 carries the family's `tags` through alongside
`generatedFrom`/`generator`. This module **consumes** the result and must not build a second tag path.
⚠ **Until D28 lands, every tag-gated rule here is inert** — including D8's aptitude gate, which is
implemented as *"an aptitude affix carries a tag only high rungs admit."*

### D8 — aptitude affixes are rarity-gated, and the mechanism is not free

D8 is legal here and needs nothing new from this module's matrix: `eligibility-tags`' per-container
allow/deny is the gate. **But §2f.2 amended D8 twice and both amendments land on someone:**

- an aptitude affix grants a **share delta**, not points (share-normalised aptitudes decay as
  `P(Θ)/T²`, ~8× from Θ=20 to Θ=200);
- it needs **a 13th atom kind or an `aptitude.*` channel family, and a fifth `AllocationScope`** —
  reviewed vocabulary changes owned by **effect-atom** and **class-system** (§2g #2).

**So no aptitude affix may be authored into a pool until §2g #2 clears.** Recorded here because the
matrix is where someone would try.

### The matrix — 15 groups × **16** roles, re-issued

⚠ **The matrix is 15 × 16, not 15 × 15, and this spec said both.** The `98 × 16 = 1,568` figure two
sections above is the correct one: `core.v1.json` ships **15** body roles in `roles.list` plus the
commander-only `standard` in `roles.commanderOnly`, the 98 shipped families name **16** distinct role
ids between them (`standard` included — `atom.warding`, `atom.resilience` and eight `g-ward`/`g-armour`
families carry it), and this section keeps
`standard`'s row. Sixteen rows of fifteen numbers.

`ssot-affixes.md` §4.3's matrix is written against **12** old role ids plus `standard`. The binding set
is the **15** in `roles.list` of `data/seed/items/_registry/core.v1.json`, plus `standard`. Mapping:
`core-protective → core-guard`,
`head-protective → head-guard`, `manipulator-offense → manipulator`, `mantle-utility → mantle`,
`girdle-resource → girdle`, `sense-utility → sense`, `jewel-minor → jewel-minor-a` + `jewel-minor-b`;
and **three roles are new with no old row** — `ward-array`, `infusion`, `retinue`. Each added role is
one row of 15 numbers.

⚠ **`standard` keeps its row and generates nothing** (D14) — the same disposition seedsmith gave its
`environment` kind. A generated row would make coverage report a partition covered when nothing is
there.

The matrix stays **generator input, not a table** (§5.4): nothing at runtime reads it, the generated
`effect_container_pool` rows are the SSOT, and both `effect_atom` and `effect_container_pool` are
already in the E8 content-hash registry — so a weight change moves the hash with **no
`contentHashSchemaVersion` bump**.

### ⛔ Item naming — this module owns it, and until now nothing did

**This was the program's worst gap.** I8 owns *"item naming from affixes — the grammar, the word tables,
and the plant-frame variant"* (`ssot-affixes.md:25`), I8 is this module's lane, and **every earlier
revision of this spec mentioned naming zero times.** Meanwhile `spec-item-card.md:302` asserts
`display_model_is_byte_identical_for_one_seed` over a *"generated name"* it has no way to produce.
**Every dropped item was nameless, and one test in another module was the only thing that noticed.**

**Claimed here**, because the name is a pure function of `(base type, rolled affix set)` and this module
is what decides which affixes can be in that set. Module 10 **renders** the name; it does not derive it.

#### What already ships — measured, and it is most of the vocabulary

| Artefact | State | Where |
|---|---|---|
| The **grammar** — three authorable patterns (`compound`, `of-construct`, `fusion`) plus a fourth, `generatedOnlyPattern` `<Adjective> <Base> of <Concept>`, `authorable: false`, whose own note says it is *"what I8 §4.12's runtime naming function assembles for a Magic-rarity rolled instance"* | ✅ **frozen, v4** | `data/seed/items/_registry/naming.v1.json` → `namingGrammar` |
| The **word pools** — 1,245 canonical ids, 1,593 surface forms, 95 pools (nouns per `role.frame`, class-rung adjectives per `ladder.frame.rung`, theme adjectives, concepts, unique/set seeds) | ✅ **frozen, v5** | `_registry/words.v1.json` → `pools`, `selfCheck` |
| The **affix flavour words** — `nameWords.{prefix\|suffix}` on **98 of 98** families, **346 words total** | ✅ **authored** | `data/seed/items/affix-families/*.json` |
| The **collision normalizer** and the pattern regexes, `GeneratedOnly` included | ✅ **built** | `tools/ItemSeedValidator/Checks/NamingCheck.cs:15-21`, `Naming/NameNormalizer.cs` |

#### What is missing — and the first row is why items are nameless

| Missing | Evidence |
|---|---|
| ⛔ **The naming function itself.** `nameWords` has **zero consumers in the whole tree** — the only three code hits are an allow-list entry and two comments that deliberately drop it: *"Everything else on an entry (roles, frames, **nameWords**, tags, notes…) is the item program's own authored surface and out of scope here"* | `AffixFamilyFile.cs:6-15`, `FamilyExpansionTypes.cs:5-10`, `KindCatalog.cs:68`, `adapters/items/kinds.py:55`. **E43 carries the words nowhere; the item program never picked them up. 346 authored words go to no consumer** |
| **The plant-frame variant has zero rows.** `word_plant` exists in three documents and in no data file. The affix-family entry shape has no field for it, and two entries say so in their own `notes` | `g-economy.json:37`, `g-life.json:82` — both name the exact override they could not encode (`of Photosynthesis`, `Verdant`) |
| **The rare two-word head/tail table does not exist** | `grep -rn "headWords\|tailWords\|head_word\|rareName" data/seed/items/` → 0 hits |

#### ⚠ Two lane numbers the corpus overtook

| `ssot-affixes.md` §4.12 says | Shipped corpus |
|---|---|
| *"70 families × 3 bands is 210 words"* | **98 families, 346 words.** The lane's 70 predates `g.*`'s expansion to 98 |
| Three words per family, one per name band (A = t1–t2, B = t3, C = t4–t5) | ⛔ **True for 71 of 98 only.** 15 families carry **6** words — the element sets (`Ember·Frost·Gale·Stone·Radiant·Umbral` and its seven siblings), which are **one word per variant, not per band**. 9 carry 4, one carries 5, and `atom.bulwark` / `atom.tempo-stampede` carry **1** |

⭐ **So band cannot be read off list position, and a naming function that indexes `nameWords[band]` is
wrong for 27 of 98 families.** The shape is keyed by *class*, and what varies inside it differs by
family. **This is the one thing about naming that needs deciding rather than describing**, and it is
cheap: give the list an explicit key.

#### The data shape — one home, not two

`ssot-affixes.md` §5.3 asks for a new runtime table, `item_affix_name(family_id, variant, band, class,
word, word_plant)`. **Do not mint it as a second home.** The words are already authored on the family
entry, and this is the same question §2b.1 answered for `item_role_family`: *derive from the one place
the fact already lives, so a second home cannot contradict it.* An authored table beside an authored
`nameWords` block is two vocabularies that drift.

**Committed:**

| | Ruling |
|---|---|
| **Source of truth** | `nameWords` on the affix-family entry. `item_affix_name` is a **projection** built at import, exactly as `item_role_family` is derived — a read model, not an authoring surface |
| **Keying** | ⭐ **Re-key `nameWords` from a bare list to `{ band: "A"\|"B"\|"C", variant?: string, word: string, wordPlant?: string }`.** It makes the 27 irregular families explicit instead of positional, and it is where `word_plant` finally gets a field. **Additive to the entry shape** — an ask on `seed-contract.md:301-323` / `adapters/items/kinds.py:55`, and it is this module's to raise |
| **Plant override** | one optional `wordPlant` per row, **sparse**. §4.12 names four (`quickening`→Blooming, `mending`→Verdant, `sunbloom`→of Photosynthesis, `evasion`→Deep-rooted). NULL means *use the humanoid word*, which is right for most: *"Sturdy", "Ember", "of the Wellspring" all read fine on a turnip* |
| **Never stored on the instance** | §4.12's own rule, and it survives an I7 reroll for free. No `name` column, nothing to migrate, nothing that can disagree with the item's contents |

#### The function

```text
name(instance) :=
  rarity = 0 affixes      -> baseType.name
  rarity = 1..2 affixes   -> generatedOnlyPattern:  <prefixWord> <baseType.name> of <suffixWord>
  rarity = 3+ affixes     -> twoWord(roll_seed)     -- head + tail, NOT the affix words
  unique                  -> hand-authored; the grammar is bypassed entirely

prefixWord := nameWords of the highest-tier PREFIX affix, at that affix's band
suffixWord := nameWords of the highest-tier SUFFIX affix, at that affix's band
tie-break  := (tier DESC, seq ASC)      -- content-derived, NEVER instance_id or binding_id
frame      := wordPlant ?? word
```

- **The tiebreak is load-bearing and is not a detail.** `seq` is the container-atom ordinal
  (`effect_container_atom(container_id, seq, …)`, `RpgStore.Containers.cs:36-42`). Breaking on
  `instance_id` would give **two byte-identical items two different names** — `definitions.md` §5 makes
  exactly this mistake explicit for the effect list, and the same trap applies here.
- **Determinism falls out of the tiebreak, not from a separate mechanism.** Same
  `(container_id, catalog_revision, roll_seed)` ⇒ same draw ⇒ same tier/seq ordering ⇒ same name, byte
  for byte. **That is what `spec-item-card.md:302` asserts, and this is what makes it passable.**
- **A `Mixed` (hybrid) affix supplies whichever half it is chosen for**, once — it may not name both ends
  of the same item.

#### ⚠ The rare name is the one genuinely new authoring ask

3+ affixes get a generated two-word name (`Bramble Bite`, `Havoc Root`) because *"naming a six-affix item
after two of its affixes is a lie about what the item does"* — and **the head/tail pools do not exist.**

**Recommendation: draw them from `words.v1.json`, do not author a third vocabulary.** `pools.nounPools`
(515 ids) and `pools.themeAdjectivePools` (156) already carry exactly the register a two-word rare name
needs, they are frozen, and they are disjoint by construction (`selfCheck.canonicalIdUniqueness`,
`surfaceFormUniqueness`) — which is what makes a seeded draw collision-safe. A new pool would be a fourth
place item words live. ⚠ **`poolAccess.affixFamilyPartitions` currently says word pools are "out of scope"
for affix partitions**, so reading them from the naming function is an ask on `words.v1.json`'s access
table — named here, not assumed.

**Not this module's:** the tooltip, the inventory row and the loot toast that call the function
(**module 10** and **module 20**); base-type nouns (**module 6** — they come from `nounPools`, keyed
`{roleId}.{frame}`); unique names (**module 17** — hand-authored, grammar bypassed).

### Reason codes — take §2b.1, not two new codes

Three lanes propose three codes for one failure: I8's **`AffixNotLegalHere`** (§6.2), I2's
**`RoleFamilyIllegal`** (`ssot-equip-slots.md:575`), and I8's `AffixClassRollsMismatch`. §2b.1 resolves
the whole class of question — **one namespaced `ContentRuleViolated`**, because *"101 codes is a
vocabulary to maintain, document, and keep in sync with the FE forever."*

**Take it.** `ContentRuleViolated{affix.not-legal-here | affix.class-rolls | affix.role-family}`. The
closed 33-code list does not grow, and the payload carries what a bare code would have.

### What is **not** decided, and who decides

⚠ **Two of these were handed to module 3 without telling module 3.** `spec-slot-roles.md:80-84`
acknowledges the relocation in prose (*"this module chooses the hosts; module 8 applies them"*) but
carries **no test row, no success criterion and no named artefact** for it — and it does not mention
`bulwark`/`savagery` at all. A handoff nobody can fail is not a handoff. **Both are made explicit and
testable below, with a default this module ships if module 3 stays silent.**

| Open | Owner | The artefact | If module 3 is silent |
|---|---|---|---|
| Whether `bulwark`/`savagery` leave the twin jewels, or §2.5 is amended | **module 3** (`ssot-equip-slots` is its lane) — ⚠ **not currently in its spec; raise it there** | two rows in `family-overrides.v1.json`, or an amended `ssot-equip-slots.md:131-132` | ⭐ **Remove them.** `classes.v1.json`'s `_meta.designNotes` independently bars both from every *implicit* slate because they use the `More` operator, and `spec-base-types.md` tests that (`no_More_op_family_appears_on_any_implicit_slate`). Two documents already treat them as top-shelf; §2.5 is the outlier |
| The reduced `max_tier` for `head-guard`'s and `sense`'s relocated clusters | **module 3** picks; this module applies | ⭐ **`role-relocation.v1.json`** — `(droppedRole, familyId, hostRole, maxTier)`, one row per relocated family. **Named here because it did not exist**, and "module 3 picks" with no artefact means nothing is picked | ⭐ **`max_tier = 3`**, following `ssot-equip-slots.md` §4.2's shipped precedent exactly — `ward-array`'s shield families already relocate to `core-guard` at 3 against `ward-array`'s 5. A default the precedent supplies is not a decision this module is making |

**Both are testable either way** — `a_relocated_family_carries_a_reduced_max_tier_on_its_host` and
`bulwark_and_savagery_are_absent_from_both_minor_jewels` are red today and stay red until one of the two
paths is taken. ⚠ **`role-relocation.v1.json` missing is itself a failure**, not a skip:
`the_relocation_artefact_exists_and_covers_every_dropped_family` fires when module 3 has not shipped it.

| Open | Owner |
|---|---|
| 3+3 or 2+2 as the class ceiling (§10.1) | **owner**, on measured evidence — §9.15 asks the perf stream for a fully-geared roster scenario, and the lane says it would move to 2+2 on data rather than on feel |
| The four constants — `r = 1.75`, band `[0.67m, 1.33m]`, tier weights `1000/600/300/120/35` (§10.3) | **owner**; they are the first numbers a sweep moves. `r` and the band width are already frozen in `data/seed/items/_registry/bands.v1.json` |

## Commands

```powershell
dotnet run --project tools\ItemSeedValidator
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AffixLegality"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RoleFamily"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemNaming"
python -m seedsmith metrics    # Distribution/Evenness + /Inequality — measure-only, gates=False
```

## Project structure

```text
data/seed/items/_registry/role-affix-weights.v1.json  new — 16 roles x 15 groups, generator input
                                                        (15 body roles + standard, which keeps a row
                                                        and generates nothing per D14)
data/seed/items/_registry/family-overrides.v1.json    new — the max_tier override list (twins + D3)
data/seed/items/_registry/role-relocation.v1.json     new — (droppedRole, familyId, hostRole, maxTier);
                                                        MODULE 3 authors it, this module applies it
data/seed/items/affix-families/**                     EDIT — re-key nameWords from a bare list to
                                                        {band, variant?, word, wordPlant?}. Additive;
                                                        no id changes, no word deletions
src/FusionRpg.Core/Items/RoleFamilyTable.cs           new — the DERIVED item_role_family + overrides
src/FusionRpg.Core/Items/IlvlTierLadder.cs            new — D29's ladder + I12's collapsing envelope
src/FusionRpg.Core/Items/AffixFilters.cs              new — frame / side / runtime, runtime read from
                                                        AtomKindRegistry, never from a document
src/FusionRpg.Core/Items/ItemNameComposer.cs          new — ⛔ THE NAMING FUNCTION. Nothing owned this.
                                                        Pure; never stored; tie-break (tier DESC, seq ASC)
src/FusionRpg.Core/Items/AffixNameTable.cs            new — the item_affix_name PROJECTION built from
                                                        nameWords at import. Not an authoring surface
src/FusionRpg.Core/Effects/Atoms/AffixValidator.cs    SHIPPED — ResolveClass (incl. Mixed, the hybrid
                                                        case); verify, do not rebuild
src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs SHIPPED — drawable-group counting, the multi-ref
                                                        Group requirement; verify, do not rebuild
src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs   SHIPPED — the consumer seam
tools/ItemSeedValidator/Checks/RoleFamilyCheck.cs     new — legality at seed time
tools/ItemSeedValidator/Checks/NameWordCheck.cs       new — every family's nameWords cover every band
                                                        its tier range can reach
tools/seedsmith/seedsmith/metrics/distribution.py     EDIT — _observed_count learns the
                                                        role:<r>:affix-family / group:<g>:affix-family
                                                        dimensions. The metrics themselves are unchanged
```

⚠ **The 740-row base-type corpus is NOT touched by this module** — `nameWords` lives on affix families,
98 files. Module 6 owns the base-type migration.

## Code style

```csharp
// Runtime legality is read from the registry, never from a lane document. ssot-affixes.md §4.9 still
// says stat.derived is quarantined None/None/None; AtomKindRegistry.cs:255 says Full/Full/None. The
// registry is the code, the document is a description, and a description went stale without anyone
// noticing for a week. Read the matrix.
static bool RuntimeAllows(AtomRow atom, TargetRuntime target) =>
    AtomKindRegistry.For(atom.KindId).Support.In(target) != RuntimeState.None;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `item_role_family_is_derived_from_the_family_roles_lists` | ⭐ no authored cells; 656 pairs reproduced from the corpus |
| `an_authored_item_role_family_row_is_rejected` | there is no second home for the fact |
| `the_only_max_tier_overrides_are_the_declared_list` | the override list is small and enumerated, not open |
| `both_minor_jewels_cap_every_family_at_tier_3` | `ssot-equip-slots.md:129` |
| `bulwark_and_savagery_are_absent_from_both_minor_jewels` | ⭐ §2.5's third mechanism. **Red today** |
| `the_ilvl_tier_ladder_is_1_1_8_18_32` | D29; I8's `1/12/25/40/60` must not reappear |
| `t1_never_falls_out_of_the_window_at_high_ilvl` | the collapsing envelope, not the sliding one |
| `a_narrowed_envelope_narrows_the_count_and_records_it` | never reject a legal drop from legal content |
| `no_family_is_orphaned_by_the_three_dropped_hybrid_roles` | ⭐ D3's relocation premise, measured — 0 orphans |
| `a_relocated_family_carries_a_reduced_max_tier_on_its_host` | the 200‰ price is not refunded through breadth. **Red until `role-relocation.v1.json` exists** |
| `the_relocation_artefact_exists_and_covers_every_dropped_family` | ⭐ the module-3 handoff, as a failing fixture — a handoff nobody can fail is not a handoff |
| `head_guard_legal_families_are_45_over_7_groups` | the corrected breakdown (`g-sustain` is 7, `g-life` is 6); an arithmetic slip here mis-sizes D3's relocation |
| `the_role_group_matrix_has_sixteen_rows` | 15 body roles + `standard`; `98 × 16 = 1,568` is the cell count the derivation is measured against |
| `stat_derived_families_are_legal_on_battle_and_lawn` | the lifted quarantine, asserted against the registry |
| `a_stat_derived_affix_is_refused_for_a_sim_target` | Sim is still `None`; the half that did **not** lift |
| `warding_and_resilience_are_refused_at_import_for_every_slot` | G8 + D14: `standard` is out of scope, so nowhere |
| `an_aptitude_tagged_affix_cannot_enter_a_pool_before_the_vocabulary_lands` | §2g #2 — the gate is inert without it |
| `a_group_emptied_by_a_filter_redistributes_and_logs_the_triple` | §4.4; silently shrinking a pool by a third is the worst outcome |
| `prefix_rolls_plus_suffix_rolls_is_the_count_and_neither_exceeds_3` | the quota, against `NOT NULL DEFAULT 0` |
| `an_item_with_three_or_more_rolls_has_at_least_one_of_each_class` | the anti-stat-stick / anti-proc-storm rule |
| `affix_class_is_derived_and_an_authored_one_is_rejected_in_seeds` | `AffixValidator.ResolveClass`; `seed-contract.md` |
| `standard_keeps_a_matrix_row_and_generates_nothing` | D14 |
| `legality_failures_report_one_namespaced_ContentRuleViolated` | §2b.1 — the closed 33-code list does not grow |

**Naming — newly owned, so every row here is new.**

| Test | Asserts |
|---|---|
| `a_magic_item_name_is_prefix_word_plus_base_name_plus_of_suffix_word` | ⭐ the grammar. `Sturdy Bark Helm of Embers` |
| `a_normal_item_is_named_by_its_base_type_alone` | 0 affixes, no grammar |
| `a_rare_item_gets_a_seeded_two_word_name_not_an_affix_name` | 3+ affixes; naming a six-affix item after two of them is a lie |
| `the_name_is_a_pure_function_and_is_never_stored` | ⭐ no `name` column on the instance; **this is what makes `spec-item-card.md:302` passable** |
| `the_same_roll_seed_produces_a_byte_identical_name` | same `(container_id, catalog_revision, roll_seed)` ⇒ same name |
| `the_name_tiebreak_is_tier_desc_then_seq_asc` | never `instance_id`/`binding_id` — two byte-identical items must not get two names |
| `a_reroll_of_a_non_naming_affix_leaves_the_name_unchanged` | I7 must not rename your item |
| `every_family_has_a_name_word_for_every_band_its_tier_range_reaches` | **red today** — 27 of 98 are not band-keyed at all |
| `an_element_family_supplies_one_word_per_variant_not_per_band` | the 15 six-word families are variants, not bands; a positional index is wrong for them |
| `a_plant_frame_item_uses_wordPlant_where_present_and_the_humanoid_word_otherwise` | sparse override, NULL means fall through |
| `swiftness_never_names_a_plant_item` | the name cannot leak an affix the frame filter refused |
| `an_item_affix_name_row_authored_by_hand_is_rejected` | it is a projection of `nameWords`, not a second home |
| `a_unique_bypasses_the_grammar_entirely` | module 17's hand-authored names are not recomposed |

**Hybrid, crafted-only and distribution.**

| Test | Asserts |
|---|---|
| `a_multi_ref_affix_without_an_explicit_group_key_is_rejected` | `ContainerValidator.cs:130-137` — verify the shipped behaviour, do not rebuild it |
| `a_hybrid_is_legal_only_where_every_ref_family_is_legal` | ⭐ intersection, never union — the `swiftness`-on-a-plant smuggling hole |
| `a_mixed_affix_counts_once_toward_the_resolved_class_mix` | ⚠ `Mixed` satisfies both drawable-group counts (`:140-143`); the anti-stat-stick rule must count refs |
| `a_weight_zero_row_is_kept_and_never_drawn` | `ContainerValidator.cs:139` |
| `a_weight_zero_row_still_fails_every_legality_filter` | crafted-only is not a legality bypass |
| `a_reroll_never_draws_a_weight_zero_row` | `ssot-affixes.md:704-707`; module 15 implements, this module owns the predicate |
| `a_pool_of_only_weight_zero_rows_is_UnsatisfiablePool` | `:147-149` |
| `the_affix_distribution_artefact_is_written_on_every_validator_run` | the metric is useless if it is printed and lost |
| `the_distribution_metric_reports_and_never_gates` | `gates = False`; nobody can name a correct Pielou value yet |

## Boundaries

**Always:** derive `item_role_family`; read runtime support from `AtomKindRegistry`; keep the matrix
as generator input so the generated pool rows stay the SSOT and the content hash keeps working; keep
the name a pure function of `(base type, rolled affix set)`; measure the derived table's distribution
and publish it.

**Ask first:** adding a `max_tier` override beyond the declared list; changing the class ceiling from
3+3; adding a sixteenth affix group; **re-keying `nameWords`** (additive, but it moves
`seed-contract.md:301-323` and `adapters/items/kinds.py:55`); **reading `words.v1.json`'s pools from the
naming function** (`poolAccess.affixFamilyPartitions` currently says out of scope); **authoring a rolled
hybrid into a generated pool** — legal is not the same as wanted.

**Never:** hand-author a role × family cell. Never fork a shared affix to change its legality — that
is what `eligibility-tags`' allow/deny exists to avoid. Never quote a runtime-support claim from a
lane document. Never let a family be legal on a role where its group weight is 0. **Never store a
composed name on the instance** — an I7 reroll would leave it lying. **Never mint `item_affix_name` as
an authoring surface** — it is a projection of `nameWords`, and a second vocabulary drifts. **Never
gate on a distribution threshold nobody has calibrated.**

## Success criteria

- [ ] `item_role_family` is derived from the 98 families' own `roles`/`frames`/`side`, with an
      enumerated override list and **zero** authored cells.
- [ ] The ilvl→tier ladder is **1 / 1 / 8 / 18 / 32**, and the collapsing envelope — not a sliding
      window — is the window rule.
- [ ] D3's relocation is recorded with its measured inputs, including the finding that **no family is
      orphaned**, and each relocated cluster carries a reduced `max_tier`.
- [ ] Every `stat.derived` group is live on battle and lawn, refused for sim, and the legality check
      reads `AtomKindRegistry` rather than a document.
- [ ] `bulwark` and `savagery` no longer sit on the twin minor jewels, or `ssot-equip-slots.md` §2.5
      is amended to say they may.
- [ ] Legality failures surface as one namespaced `ContentRuleViolated`; no new reason code ships.
- [ ] `ItemSeedValidator` reports 0 legality errors across all 98 families and **16** roles.
- [ ] ⛔ **Every dropped item has a name.** `ItemNameComposer` composes normal / magic / rare from
      `(base type, rolled affix set)`, is never stored, and `spec-item-card.md:302`'s
      `display_model_is_byte_identical_for_one_seed` passes with the generated name included.
- [ ] `nameWords` is band-keyed on all 98 families, `wordPlant` has a field, and the 27 families whose
      lists are not three-per-band are explicit rather than positional.
- [ ] `item_affix_name` exists only as an import-time projection; a hand-authored row is rejected.
- [ ] Rolled hybrids are legal, class-derived `Mixed`, group-keyed, and legal only on the
      **intersection** of their refs' `(role, frame, side)`.
- [ ] Crafted-only affixes are `weight = 0`, invisible to the draw and to reroll, and still subject to
      every legality filter.
- [ ] `Distribution/Evenness` and `Distribution/Inequality` run over the derived `(role, family)` table
      **and** the group-weight matrix, report-only, and land in a CI artefact on every run.
- [ ] `role-relocation.v1.json` exists and covers every family on the three dropped roles, or the
      module-3 handoff test is red and named.
