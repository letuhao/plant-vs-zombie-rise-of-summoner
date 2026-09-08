# Spec: `base-types`

**Module id:** `base-types` · **Program:** [item](../item-map.md) · **Build order:** 6 of 21
**Depends on:** `slot-roles` (3) — and through it **X1** (`frame`)
**Rulings:** **D11 (+ the §2f.2 widening and the §2h.1 reframing)**, **D35 (unfreeze and re-derive — approved 2026-09-04)**, D3, D14, D15, D20, D26, D29 · lane [ssot-item-categories.md](ssot-item-categories.md)

## Objective

Make every role's **humanoid and plant base types differ on two axes** — a directional stat profile
inside one budget, and a distinct implicit — and prove it with a **dominance lint**: for every role
there is a build for which the humanoid base is correct and a build for which the plant base is
correct.

**D11 is not flavour. It is the correctness condition D3's whole mechanism rests on.** If one frame's
base wins every role, a hybrid cherry-picks for free, the mix bonus is a gift, and the 800‰ floor is
theatre. ⚠ **The lint has two modes and only one is this module's** — `channel-split` ships here at
position 6; `corner-matrix` needs module 9's power vector and is owed there. See Success criteria.

**Two capabilities were added 2026-09-04 because nothing else owned them**, and both are I3's, this
module's lane:

- ⛔ **`socketMax` per entry**, validated against module 16's per-role ceiling. **Module 21 is inert
  until this ships** — it declares a hard dependency on `socketMax = 4` that this spec previously filed
  under *"Ask first"* and never issued.
- **`item_category`** — the ten-row taxonomy modules 2, 14 and 18 all assume and none defines.

**Users:** 2 (`armoury` — the categories its list groups by), 8 (`affix-legality` — the
`affix_pool_tag`), 9 (`item-power-reads` — the implicit budget cap), 11 (`drop-volume`), 12 (the
frame-mix predicate counts *these* frames), 14 and 18 (both key on `item_category`), 16 and 21 (both
read `socketMax`).

## Design

### ⛔ The corpus already exists — and it fails D11 on both axes

**740 base-type entries ship today** in `data/seed/items/base-types/` (24 per `(role, frame)` across
15 roles, plus 10 per frame for `standard`), inside a 1,438-entry corpus of 126 files
(`dotnet run --project tools/ItemSeedValidator`). This module does **not** author base types from
nothing; it repairs a corpus that was authored 2026-08-22, before D11 existed.

| D11 clause | State today | Evidence |
|---|---|---|
| **Distinct implicit per frame** | ⛔ **fails in 14 of 16 roles** — the humanoid and plant implicit-family sets are *identical* | measured over `data/seed/items/base-types/**` |
| | the two exceptions differ by **one family each**: `armament-secondary` (humanoid-only `atom.mending`), `girdle` (plant-only `atom.vitality`) | same |
| **Directional stat profile** | ⛔ **not expressible.** No entry field carries one | entry keys are `id · nameKey · name · frame · role · class · band · implicit · socketMax · iconKey · flavorKey · flavor · tags · enhanceTrack`; the shape is fixed in `seed-contract.md:324-343` and `tools/seedsmith/seedsmith/adapters/items/kinds.py:49-51` |
| **Correlated across roles** | ⛔ nothing to correlate — see the row above | — |

⭐ **And the root cause is one stale fact, not 740 bad decisions.**
`data/seed/items/_registry/classes.v1.json` (`registryVersion: 3`, `frozen: true`) globally excludes
**32 families** from every implicit slate, most of them on the stated grounds that *"`stat.derived` —
quarantined None/None/None (D6)"*. **That is false today.** `stat.derived` ships
`RuntimeSupportMatrix(Full, Full, None)` — Battle **Full**, Lawn **Full**, Sim None
(`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:255`, kind declared at `:226`), with 267
registered derived channels (`:53`).

The registry's own `_meta.designNotes` says the consequence out loud: four roles — `ward-array`,
`mantle`, `head-guard`, `sense` — have *"their entire §2.3-named family cluster excluded"* and carry
*"a small stopgap slate drawn from generic, already-bindable families."* **Generic stopgap slates are
frame-blind by construction**, which is precisely why those roles show two implicit families per frame
and the same two on both frames.

> **So the first task of this module is not authoring. It is lifting a quarantine that was lifted
> upstream and never propagated**, then re-slating the stopgap roles from their real clusters.

✅ **APPROVED 2026-09-04 — D35: unfreeze and re-derive.** `classes.v1.json` goes to
**`registryVersion 4`**: the 32-family global exclusion is lifted, the stopgap slates are refilled from
each role's real §2.3 cluster, and the entry shape gains the directional-profile field it lacks.
**Authoring happens after that, never before** — writing 740 entries against the current allow-list
would bake an expired quarantine into the shipped corpus.

⭐ **The bump travels with D30's.** `core.v1.json` → v2 (the twelve-role hybrid core) and
`classes.v1.json` → v4 land in **one pass with one re-run decision**, because the corpus regeneration
that re-authors D30's 18 legacy sets is the same regeneration that re-slates against the lifted
quarantine. Splitting them costs two full generation runs and leaves the corpus incoherent in between.

⚠ **It is five stopgap roles, not four.** The registry's `_meta.designNotes` names four; counted
against `implicitSlates`, **`footing` is also on a 2-family stopgap slate** (`ward-array` 2,
`head-guard` 2, `sense` 2, `footing` 2, `mantle` 3). Corrected here and in the success criteria —
[item-ideal.md](../item-ideal.md) §2g inherited the undercount from the same note.

⚠ **The file's own `frozenNote` is stale too** — it reads *"FROZEN v2"* while `registryVersion` is
**3**. The v4 bump rewrites it; flagged so the mismatch is not read as evidence of anything.

### What D11 requires, restated with the §2f.2 widening

Three clauses. The third is the one D11 originally omitted and the one that decides whether D3 works.

| # | Clause | Formally |
|---|---|---|
| 1 | **Distinct implicit** | `implicitFamilies(role, humanoid) ∩ implicitFamilies(role, plant) = ∅` |
| 2 | **Directional profile inside one budget** | the two slates spend the same `budget_permille` on different channels, and neither strictly dominates |
| 3 | ⭐ **Correlated across roles** | one fixed axis assignment `humanoid → A`, `plant → B` such that **every** role in the twelve-role hybrid core leans the same way |

**Why clause 3 is load-bearing, with the arithmetic** (item-ideal §2f.2). If each role's frame
preference were an independent coin flip, the minority count `min(k, 12−k)` for `k ~ Bin(12, ½)`
concentrates near 6 — a hybrid **conceding nothing** averages **958‰**, the 800‰ floor binds on
**0.63 %** of builds, and D3 collapses at any per-role advantage above 4.4 %. Correlated preference is
what makes the floor bind. **D11 as written never said which, and it is the whole difference.**

#### ⭐ §2h.1 — clause 3 dissolves the abuse D3 says it punishes, and the mechanism survives anyway

**Recorded here because this module is the one that builds clause 3, and building it changes what D3
means.** `item-ideal.md` §2h.1 (2026-09-04):

> §2f.2's correlated-directionality widening **dissolves the cherry-pick D3 exists to punish.** D3's own
> words presuppose *uncorrelated* preference — *"if the better pick is humanoid in 10 of 12 roles, you
> take them, the minority count is 2."* **With one fixed lean per frame there is no role where one frame
> is stronger**; there is only *how much of axis A versus axis B*. The cherry-pick cannot occur.

**Restated, and this is the version that survives contact with clause 3:**

> **A hybrid pays 200‰ for the only base profile that spans both axes, and earns it back by actually
> spanning them.** A hybrid that specialises (all one frame) stays at 800‰ — correctly, because it is
> doing what a pure frame does, worse. A hybrid that commits to generalism (6/6) reaches parity.
> **The floor prices generalism; the bonus rewards committing to it.**

| | |
|---|---|
| **Mechanism** | **unchanged.** Nothing in this spec moves. Clause 3 stays HARD, the lean stays per `(ladder, frame)`, the 800‰ floor stays |
| **Rationale** | **changed.** Do not justify clause 3 by "it stops cherry-picking" — clause 3 is what makes cherry-picking impossible, so the two cannot both be true. Justify it by the binomial arithmetic above: uncorrelated preference makes the floor bind on 0.63 % of builds, which is a floor that does nothing |
| **Owed elsewhere** | D3's stated rationale in `item-ideal.md` §2b. Not this module's text to rewrite |

⚠ **And the size of this module's bill is unmeasured.** D11's whole apparatus serves hybrid bodies, and
**the hybrid population has never been counted.** X1's `frame-classify` stage exists only as a proposal
(`seedsmith-map.md:237-245`) — no code, no output, nothing in `data/seed/demons/_registry/`. So the
apparatus currently serves **the commander plus an unknown number of the ~904 species**.

> ⭐ **The cheap insurance is one command, and it is not this module's to run: run X1, count the hybrids,
> then decide how much of D11 to buy.** ⚠ **This is not a reason to defer clause 3** — clause 3 costs
> 8 registry blocks and is structural, so it is cheap at any population. It *is* a reason to sequence the
> 740-entry implicit re-slate behind the count, because that work scales with nothing but itself.

⚠ **D3 also has no player surface.** `frame-mix` appears in modules 3, 6 and 12 and in **none** of module
20's six surfaces. Named here because this module owns the frames; **module 20 owns the gap.**

### Where the direction lives — one registry block, not 740 entries

| Option | Cost | Verdict |
|---|---|---|
| **(a)** a `statProfile` field on every base-type entry | 740 authored blocks; nothing enforces correlation; a per-entry field is 740 chances to break clause 3 | **Reject** |
| **(b)** a `direction` block per class (24 classes) | 24 blocks; correlation holds per ladder but the ladders can still disagree | half right |
| **(c)** ⭐ **one lean per `(ladder, frame)` — 10 pairs, 8 authored** | 10 blocks; **clause 3 holds by construction** because every role drawing from a frame's ladder inherits one lean | **Recommended** |

⚠ **Corrected 2026-09-04 — there are FIVE class ladders, not four, so ten `(ladder, frame)` pairs.**
`data/seed/items/_registry/classes.v1.json` → `classLadders` ships **`armour · weapon · offhand · jewel ·
standard`**. The fifth already exists; it is not a hypothetical.

| Ladder | humanoid rungs | plant rungs | In scope here? |
|---|---|---|---|
| `armour` | cloth · leather · scale · plate | fibre · husk · bark · heartwood | **yes** |
| `weapon` | blade · blunt · launcher | lash · nozzle · seedpod | **yes** |
| `offhand` | bulwark · focus | censer · thornguard | **yes** |
| `jewel` | signet · seal · torc | bulb · graft · spore | **yes** |
| **`standard`** | field-standard · ceremonial-standard | withy-totem · heartwood-totem | ⚠ **declared, unauthored — D14** |

**So: ten pairs exist, this module authors eight, and the `standard` pair is declared and left empty** —
the same disposition `standard` already gets from `spec-slot-roles.md` (*"declared and ungenerated"*),
from `spec-affix-legality.md` (a matrix row that generates nothing) and from seedsmith's `environment`
kind. Declaring the pair and leaving it unauthored keeps the shape stable; **omitting it would make the
lean table's own coverage check report ten of ten when eight are real.**

⚠ **`standard` is also the only ladder no body role draws from.** `words.v1.json` →
`poolAccess.roleToLadders` maps all 15 body roles to `weapon`/`offhand`/`armour`/`jewel` and none to
`standard`, so leaving it unauthored costs clause 3 nothing.

**(c) makes correlation structural rather than checked.** The class ladders are already parallel and
frame-keyed — humanoid `cloth · leather · scale · plate` against plant `fibre · husk · bark ·
heartwood`, same `rung`, same `roles` list, same weight `tags`
(`data/seed/items/_registry/classes.v1.json` → `classLadders.armour`). What they carry is a rung and a
weight tag; **what they do not carry is a direction.** Adding it once per `(ladder, frame)` is the
smallest place the fact can live and the only place it cannot drift per role.

Per-mille shares in a registry are established practice here, not a new pattern:
`core.v1.json.roles.list[].budgetWeightMilli` and
`data/seed/items/_tuning/tier-bands.v1.json.channelWeightPermille` both do it. And it stays inside
`seed-contract.md:88-105` — the numeric rule binds **authors of entries**; a registry share is the
resolver's own input, exactly as `budgetWeightMilli` already is.

```text
frame-lean.v1.json     (new registry, one block per ladder x frame = 10; 8 authored, 2 declared empty)
  armour / humanoid   baseSplitPermille { maxHp 700, combat.dodge 300 }   implicitAxis "burst"
  armour / plant      baseSplitPermille { maxHp 700, combat.regen 300 }   implicitAxis "sustain"
  weapon  / humanoid  …                                                    implicitAxis "burst"
  weapon  / plant     …                                                    implicitAxis "sustain"
  … offhand, jewel
  standard / humanoid  declared, no lean   -- D14: commander gear is out of scope
  standard / plant     declared, no lean   -- ditto. Present so coverage reports 8/8, not 10/10
```

⚠ **The split channels must both be bindable on both frames.** `plating` / `carapace` write
`arm1Max` / `arm2Max`, Unity fields that exist only on zombies
(`ssot-affixes.md:302` — the side filter), so they may **not** be a humanoid lean channel: side is not
frame, and a plant-side humanoid-framed body would get nothing. The derived channels are the safe
axis, and they are legal now that the quarantine is lifted.

### The dominance lint — the thing that makes D11 checkable

A role where one frame dominates across all builds is **a content defect with a name**, not a matter
of taste. The lint is the name.

```text
for each role r in the twelve-role hybrid core:
    H = the humanoid slate's power vector at the role's budget
    P = the plant slate's power vector at the same budget
    assert ∃ corner c in the aptitude corner matrix : score(H, c) > score(P, c)
    assert ∃ corner c'                              : score(P, c') > score(H, c')
    assert lean(r, humanoid) == globalLean(humanoid)      # clause 3
```

- The corner matrix is the class system's, already built and already measured over **144 evaluations**
  (`item-ideal.md` §2f.3 D29). The guards are `TerminationGuard.Assert` and `DominanceGuard.Measure`
  (`src/FusionRpg.Core/Balance/Guards/TerminationGuard.cs:67`,
  `src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:38`).
- `score` reads module 9's power vector (`PowerScalar.Of`,
  `src/FusionRpg.Core/Effects/Atoms/Power/PowerReads.cs:39`, five categories at `:34`).
- ⚠ **The lint therefore cannot run until module 9 lands, and module 9 waits on X6** (`E44
  power-sweep`, all 20 coefficients flat at `CoeffMilli = 1000`). Until then it runs in **channel-split
  mode**: assert the two `baseSplitPermille` blocks differ on at least one channel and that neither is
  a superset. That is weaker, it is honest, and it catches the failure that exists today.

⭐ **So the lint is two deliverables in two modules, and the success criteria say so separately.** An
earlier revision of this spec declared the lint unrunnable at position 6 and then required it to be
green — a criterion this module cannot reach.

| Mode | Asserts | Needs | Owner |
|---|---|---|---|
| **`channel-split`** | the two `baseSplitPermille` blocks differ on ≥1 channel, neither is a superset, and clause 3 holds | nothing beyond `frame-lean.v1.json` | ⭐ **this module, position 6.** Green is required here |
| **`corner-matrix`** | ∃ a corner where H beats P, and ∃ a corner where P beats H, per role | `PowerScalar.Of` → module 9 → **X6** → a seeded `power_ceiling` | **module 9.** Owed there as a named, failing-by-default fixture |

⚠ **`power_ceiling` is deliberately unseeded** (`item-ideal.md` §2h.2 #8), so `corner-matrix` runs in
its weak form for as long as that holds. **That is why the two modes are split rather than sequenced** —
a "temporary" weak mode with no scheduled end is how D3 degrades silently.

**Standing:** SOFT (reports with coverage) for the per-role clause, **HARD** for clause 3. A single
role that fails clause 1 or 2 is content to fix; a broken correlation silently repeals D3.

### The base stat stays an atom, and that is settled

`ssot-item-categories.md` §4.A resolves it and nothing since disturbs it: base damage is
`atom.base-damage.{class}.t{band}`, base guard is `atom.base-guard.{class}.t{band}`, and there is no
`weaponDamage` column because there is no such channel. A column would need a second composer, a
second pricer, a second display path and a hand-written bridge to the same `EntityStatWriter` the atom
already reaches — the exact bypass `scripts/guard-single-writer.ps1` exists to refuse.

### ⛔ `item_category` — I3's taxonomy, unowned until now

**Three modules assume categories exist and none defines them.** `spec-armoury.md:69-72` ships a
*"category and list"* query surface on the owner's own words and never says what a category is;
`spec-salvage-craft.md` and `spec-consumables.md` both key behaviour on one. And **`item_category`
appears nowhere in `src/` or `tools/`** — verified, zero hits.

It is I3's, I3 is this module's lane, and no ruling moves it. **Claimed here**, as data, not as new
behaviour: the row shape and the ten answers are already written at `ssot-item-categories.md:211-266`
and this module seeds them.

| `category_id` | Rolls values? | Stacks | `owner_scope` | `store` | v1 |
|---|---|---|---|---|---|
| `equipment` | **yes** | never | `player-then-actor` | instance | **author** |
| `material` | no | qty | `player` | stack | **author** |
| `quest` | no | qty (usually 1) | `player` | stack | **author** |
| `currency` | no | ledger | `player` | ledger | **exists** — `rpg_soul_ledger` / `rpg_soul_balances` |
| `consumable` | no | qty + charges | `player-then-actor` | stack | declare only |
| `insert` | maybe (I4) | qty until socketed | `player-then-item` | stack → instance | declare only |
| `charm` | maybe (I10) | never if rolled | `player` | instance if rolled | declare only |
| `cosmetic` | no | never | `player` | stack | ⛔ **do not author** — no consumer, none planned |
| `blueprint` | no | never (qty 1 = known) | `player` | stack | declare only |
| `cache` | no | qty | `player` | stack | declare only |

⭐ **The load-bearing column is `consumer`, `NOT NULL` and non-empty** — the code that reads the category.
Empty is a rejection. ⚠ **Not as `CategoryHasNoConsumer`** — I3 §6 proposes it as one of six additions to
the closed 33-code list, and **§2b.1 refuses that whole class of ask**: one namespaced
`ContentRuleViolated{item.category-no-consumer}`, the same disposition `spec-affix-legality.md` took for
I8's two. That is I3 applying the `status.expose.*` lesson before the fact:
*a row no code consumes is not content; it is a lie in a table.* **Four of the ten have no consumer
today** (`consumable` → the unbuilt action layer, `insert` → module 16, `charm` → module 12, `blueprint`
→ module 15), which is exactly why they are *declare only*: the row exists so the shape is stable, and
authoring into it is refused.

⚠ **`unique`, `set` and `legendary` are rarities, not categories** (I1) — a recurring confusion, and the
reason the list is ten rather than thirteen.

**Boundary:** this module seeds `item_category` and nothing more. **It does not build the bag** — module
2 owns the query surface, module 20 owns the screen, and D5 defers the inventory-management minigame.
`stack_intent` is *"the declaration; I13 builds it"* (`ssot-item-categories.md:257`).

### Three lane numbers that the shipped corpus overtook

| `ssot-item-categories` says | Shipped corpus | Disposition |
|---|---|---|
| §4.D / §7.6 — **four** item-level bands, `43 × 2 × 4 = 344` containers | **two** bands, `a` and `b` (380 / 360 entries) | the corpus is the SSOT; §7.6's own "ship bands 1 and 3 only" cut was taken. **Bands are append-only** — c and d are additive |
| §5.4's guard-share table, keyed on the **old twelve** role ids (`core-protective`, `sense-utility`…) | 15 roles with `budgetWeightMilli` summing to 1000 (`core.v1.json`) | **Re-issue §5.4 against the fifteen.** §5.4's own note says the shares re-split rather than inflate |
| §5.2 — `socket_capacity` per base type | `socketMax` ∈ {0, 1, 2}; **24 entries carry none** | ⛔ see the next section — three specs describe this three incompatible ways, and module 21 is inert until it is settled |

### ⛔ `socketMax` — three specs, three answers, and module 21 cannot start

**This is the program's blocking ownership question, and it was left as an "ask first" line.** Stated as
the three claims, side by side:

| Spec | Says | Line |
|---|---|---|
| **21 `strain-splice-gen`** | ⛔ a **hard dependency** on this module issuing `socketMax = 4` on at least `armament-primary` and `core-guard`. *"This module is inert until they are [fixed]"* | `spec-strain-splice-gen.md:72-77` |
| **6 `base-types`** (this spec, before 2026-09-04) | filed *"raising `socketMax` above 2"* under **Ask first**, and issued nothing — so the dependency pointed at a decision nobody was going to make | removed from Boundaries by this section |
| **16 `sockets`** | `socket_max` is a **role** property, *"fixed per role, not varied per base type"*, and asserts it as a test | `spec-sockets.md:108-111`, `:383` |

⛔ **Module 16's invariant is false against the shipped corpus, and not narrowly.** Measured over all
**740** entries in `data/seed/items/base-types/**` (2026-09-04):

| | Measured |
|---|---|
| Roles whose entries carry **more than one** distinct `socketMax` | **16 of 16** — every single role |
| `armament-primary` (48 entries) | `{0: 18, 1: 26, 2: 4}` |
| `core-guard` (48 entries) | `{0: 11, 1: 24, 2: 13}` |
| `jewel-minor-a` | `{0: 15, 1: 6, 2: 3}` + **24 entries with no `socketMax` key at all** |
| Entries with no value | **24**, all `jewel-minor-a` |
| Maximum anywhere | **2** |

So `socket_max_is_fixed_per_role_and_never_varies_by_base_type` (`spec-sockets.md:383`) is red on the day
it is written, in every role, and *"varying `socket_max` within a role"* is filed as an **Ask first** in
module 16 (`:411`) for a thing the corpus already does 740 times.

#### Resolution — module 16 owns the ceiling, this module owns the value

**Recommended, and it is the only split that leaves both specs true:**

| Who | Owns | Artefact |
|---|---|---|
| **Module 16** | ⭐ the **per-role ceiling** `socketCeiling(role)` — its §3 table, already written, already 15 rows, already `armament-primary = 4` and `core-guard = 4` | `data/tuning/sockets.v1.json` (module 16's own project structure already names it) |
| **Module 6** (here) | the **per-entry value**, and **validating it against the ceiling** | `data/seed/items/base-types/**` + `FrameDirectionCheck`'s sibling, `SocketMaxCheck` |

**Why the ceiling belongs to 16 and the value to 6.** §8.1's defence — *"socket count is not one number
compared across the whole loot pool"* — needs a **bound per role**, not a constant per role. A ceiling
gives it that: a 1-socket ring and a 4-socket cuirass stay in different conversations, and two cuirasses
differing by one socket is ordinary base-type variety, exactly like their differing implicits. **A
constant would also make `socketMax` the only base-type property that carries no information** — and this
module exists because base types were not differing enough.

⚠ **Module 16 must restate its invariant.** `socket_max_is_fixed_per_role_and_never_varies_by_base_type`
becomes `no_base_type_exceeds_its_role_socket_ceiling`, and *"varying within a role"* stops being an Ask
first. **That correction is owed to module 16's spec; it is named here, not absorbed.**

#### ⛔ The 740-row migration nobody has named

Issuing the ceiling does not raise a single shipped entry. **Every one of the 740 must be revisited**,
and no spec in the program says so:

| Step | Scope | Note |
|---|---|---|
| 1. Fill the 24 empty values | `jewel-minor-a` | absent ≠ 0. **`ssot-affixes.md`'s own §4-style rule applies: an omitted field is not neutral** — 24 entries currently have an undefined socket count |
| 2. Raise the top of the distribution to each role's ceiling | all 16 roles | today every role tops out at 2 against ceilings of 1–4. At minimum `armament-primary` and `core-guard` need entries at 3 and 4, or D20's 4-ingredient recipe has no chassis |
| 3. Re-shape the distribution inside each role | all 740 | it is currently mass at 0/1 with a thin 2 — plausible for a ceiling of 2, wrong for a ceiling of 4 |
| 4. Validate every entry against `socketCeiling(role)` | seed time | `SocketMaxCheck`, new |

⚠ **Ids are never reused and `socketMax` is not an identity field**, so this is an **in-place edit** of
existing entries, not a re-mint (`seed-contract.md` §7.2 keys id reuse to *identity* changes; the implicit
reassignment this module already performs is the same class of edit). **The 740 rows are edited once, by
this module, and module 21 stops being inert.**

### What is **not** decided, and who decides

| Open | Owner |
|---|---|
| Whether the **five** re-slated roles' entries need **re-flavouring** — an entry keeps its name and prose while its implicit family changes, so `atom.fortitude` prose can end up over an `atom.stoicism` implicit | the **authoring fleet** (`authoring-fleet-plan.md`). This module emits an `ImplicitFlavourDrift` **warning** per entry; it does not call a model |
| The ≤15 % implicit budget cap (`ssot-item-categories.md` §10.7) | **module 9** — it needs a power number, and there is none until X6 clears |
| Which concrete channels each frame leans on | **owner**, at the frame-lean table's first review. This module ships the mechanism and a starting table; the channels are a balance surface |
| The **per-role socket ceiling** — the numbers, not the ownership | **module 16.** Its §3 table already carries fifteen rows (`spec-sockets.md:88-104`); it must restate the invariant as a ceiling and drop *"varying within a role"* from its Ask first |
| ~~**How many species are hybrid** — and therefore how much of the 740-entry re-slate is worth buying~~ — **no longer gates anything (D30)** | **X1 / seedsmith** (`frame-classify`), still unbuilt. ⛔ **Do not run it against `themes.v1.json`** — that registry holds 84 themes against 386 shipped species, so any hybrid proportion taken from it is fiction (this is exactly the error D30 records). X1 runs **after** `theme-refresh` ([seedsmith-map.md](../seedsmith-map.md) §3c-ter). The count is now a *sizing* input for the frame-lean table, not a gate on the re-slate: **D30 ruled the twelve-role core stands regardless of the population** |
| D3's **player surface** — `frame-mix` appears in modules 3, 6 and 12 and in none of module 20's six surfaces | **module 20** |
| Corner-matrix mode for the dominance lint | **module 9**, gated on **X6**, and on someone seeding a provisional `power_ceiling` |

## Commands

```powershell
dotnet run --project tools\ItemSeedValidator                    # 126 files / 1,438 entries today
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BaseType"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~FrameDominance"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SocketMax"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ItemCategory"
```

## Project structure

```text
data/seed/items/_registry/frame-lean.v1.json      new — 10 blocks (5 ladders x 2 frames); 8 authored,
                                                    the standard pair declared and empty per D14
data/seed/items/_registry/classes.v2.json         new version — the 32-family exclusion list
                                                    re-derived against AtomKindRegistry, and the four
                                                    stopgap slates replaced. v1 is FROZEN: never edit
data/seed/items/_seed/item-category.v1.json       new — I3's ten category rows; `consumer` NOT NULL
data/seed/items/base-types/**                     ⛔ EDIT, 740 rows, TWO passes:
                                                    (a) implicit reassignment — ids never reused
                                                    (b) socketMax: fill the 24 absent jewel-minor-a
                                                        values, raise the top of each role's
                                                        distribution to module 16's ceiling, re-shape
                                                        the rest. In place, same ids (seed-contract
                                                        §7.2: "entry is wrong, same identity")
src/FusionRpg.Core/Items/FrameLean.cs             new — the lean table + the clause-3 invariant
src/FusionRpg.Core/Items/BaseTypeSlate.cs         new — slate per (role, frame)
src/FusionRpg.Core/Items/ItemCategoryTable.cs     new — I3's taxonomy; the consumer-non-empty rule
src/FusionRpg.Core/Balance/Guards/FrameDominanceGuard.cs  new — the D11 lint, corner-matrix backed
tools/ItemSeedValidator/Checks/FrameDirectionCheck.cs     new — clauses 1 and 2 at seed time
tools/ItemSeedValidator/Checks/SocketMaxCheck.cs          new — every entry has a value, and no entry
                                                            exceeds module 16's socketCeiling(role)
```

## Code style

```csharp
// Clause 3 (item-ideal §2f.2): the lean is per (ladder, frame), NEVER per role. A per-role lean lets
// each role's preference be an independent coin flip - and then min(k,12-k) for k~Bin(12,1/2)
// concentrates near 6, a hybrid conceding nothing averages 958 permille, and D3's 800 floor binds on
// 0.63% of builds. Structural correlation is the whole point of putting it here.
public static Lean Of(ClassLadder ladder, Frame frame) => _table[(ladder, frame)];

public static bool CorrelationHolds(IEnumerable<ItemRole> hybridCore) =>
    hybridCore.All(r => Of(LadderOf(r), Frame.Humanoid).Axis == GlobalAxis(Frame.Humanoid));
```

## Testing strategy

| Test | Asserts |
|---|---|
| `every_role_pair_has_disjoint_implicit_families` | D11 clause 1, over the real corpus. **Red today in 14 of 16 roles** |
| `every_role_pair_differs_on_at_least_one_base_channel` | D11 clause 2 in channel-split mode — the form that runs before module 9 |
| `the_frame_lean_is_identical_across_every_hybrid_core_role` | ⭐ D11 clause 3, HARD. The §2f.2 widening |
| `a_per_role_lean_table_is_rejected_at_load` | clause 3 cannot be defeated by relocating the field |
| `neither_frame_wins_every_corner_for_any_role` | ⚠ **`corner-matrix` mode — module 9's, not this module's.** Registered here as a failing-by-default fixture so its absence is visible, and it flips when module 9 lands and `power_ceiling` is seeded |
| `a_role_where_one_frame_dominates_is_a_named_finding` | the lint reports the role id, not a boolean |
| `stat_derived_is_not_excluded_from_any_implicit_slate` | the stale-quarantine repair, asserted against `AtomKindRegistry` rather than against a document |
| `no_lean_channel_is_side_restricted` | `plating` / `carapace` write zombie-only Unity fields; a lean on them silently voids one frame |
| `implicit_slates_are_tier_equal_within_a_role` | `classes` `_meta.designNotes`' existing guarantee survives re-slating |
| `no_More_op_family_appears_on_any_implicit_slate` | `bulwark` / `savagery` stay rolled-only — the registry's own rule |
| `base_stat_atoms_never_appear_in_a_container_pool` | `BaseStatInPool`, `ssot-item-categories.md` §6 code 1 |
| `a_base_type_id_is_never_reused_after_an_implicit_change` | `seed-contract.md` §7.2 |
| `band_letters_are_append_only` | `a`/`b` today; `c`/`d` add, never renumber |
| `the_frame_lean_table_has_ten_blocks_and_eight_leans` | ⭐ five ladders × two frames; `standard` declared and empty. A missing pair would make coverage report 8/8 when two are absent |
| `no_body_role_resolves_to_the_standard_ladder` | `words.v1.json` → `poolAccess.roleToLadders`; the empty pair costs clause 3 nothing |
| `every_base_type_carries_a_socketMax` | ⛔ **red today — 24 `jewel-minor-a` entries have no key.** Absent is not 0 |
| `no_base_type_exceeds_its_role_socket_ceiling` | ⭐ this module's half of the split; module 16 owns `socketCeiling(role)` |
| `at_least_one_base_type_per_four_socket_role_reaches_four` | ⛔ **red today — max anywhere is 2.** This is the test that un-blocks module 21 |
| `every_item_category_row_names_a_non_empty_consumer` | `ContentRuleViolated{item.category-no-consumer}`, not a 34th reason code |
| `a_declare_only_category_has_no_authored_content` | four of the ten have no consumer; the row exists, the content must not |

## Boundaries

**Always:** put the lean in the registry, one block per `(ladder, frame)`; re-derive an exclusion list
from `AtomKindRegistry`, never from a document; keep the base stat an atom; give **every** base type an
explicit `socketMax` and validate it against module 16's ceiling; give every `item_category` row a
non-empty `consumer`.

**Ask first:** the concrete lean channels (a balance surface); **authoring a lean for the `standard`
pair** (D14 puts commander gear out of scope — the block stays declared and empty); a **sixth** class
ladder. ⚠ *"Adding a fifth class ladder"* was listed here and is not askable: `classes.v1.json` already
ships five, and the `standard` ladder has shipped since `registryVersion 2`.

**Never:** author a `statProfile` per entry — that is 740 chances to break clause 3. Never edit a
`frozen: true` registry in place; mint `v{n+1}`. Never reuse a base-type id — including for a
`socketMax` change, which is an in-place edit under `seed-contract.md` §7.2 (*"entry is wrong, same
identity"*). Never let one frame's slate be a strict superset of the other's — that is dominance wearing
difference's clothes. **Never leave a `socketMax` absent** — an omitted field is not a zero, it is an
undefined socket count. **Never author content into a `declare only` category.**

## Success criteria

- [ ] Every role's humanoid and plant implicit-family sets are **disjoint** (currently 14 of 16 fail).
- [ ] A directional profile exists as data, in exactly one place, and is **per `(ladder, frame)`**.
- [ ] Clause 3 is a HARD test: one axis per frame across all twelve hybrid-core roles.
- [ ] **The dominance lint is green in `channel-split` mode** — for every hybrid-core role the two
      `baseSplitPermille` blocks differ on at least one channel and neither is a superset. ⚠ **That is
      the whole of this module's obligation**, and it is reachable at build position 6.
- [ ] **The lint's `corner-matrix` mode is registered as owed at module 9, and named as owed** — a
      failing-by-default fixture, not a silent absence. It reads `PowerScalar.Of` and cannot run until
      module 9 lands, which itself waits on **X6** (`E44 power-sweep`, 20 coefficients flat at 1000).
      ⚠ **`power_ceiling` is deliberately unseeded** (`item-ideal.md` §2h.2 #8), so channel-split is the
      mode that runs *indefinitely* unless someone seeds a provisional value — **which is why the split
      above must be written down rather than assumed temporary.**
- [ ] `classes.v2.json`'s exclusion list is derived from `AtomKindRegistry` and no longer cites a
      quarantine that was lifted at `AtomKindRegistry.cs:255`.
- [ ] The **five** stopgap-slate roles (`ward-array`, `mantle`, `head-guard`, `sense`, **`footing`**) carry their real
      clusters.
- [ ] `ItemSeedValidator` reports 0 errors on `base-types/`, and every implicit change that outran its
      prose is a named `ImplicitFlavourDrift` warning rather than a silent edit.
- [ ] ⛔ **`socketMax` is settled and module 21 is no longer inert:** module 16 owns
      `socketCeiling(role)`, this module owns the per-entry value, all 740 entries carry one, the 24
      absent `jewel-minor-a` values are filled, and at least one `armament-primary` and one `core-guard`
      base type reaches **4**.
- [ ] `item_category` ships with all ten rows, a non-empty `consumer` on each, and no authored content
      in a *declare only* category.
- [ ] The frame-lean registry has **ten** `(ladder, frame)` blocks — eight leans, the `standard` pair
      declared and empty per D14.
- [ ] §2h.1's reframing is recorded: **clause 3 dissolves the cherry-pick**, the mechanism is unchanged,
      and clause 3 is justified by the binomial arithmetic rather than by an abuse it prevents.
