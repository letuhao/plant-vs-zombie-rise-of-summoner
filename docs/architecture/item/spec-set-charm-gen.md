# Spec: `set-charm-gen`

**Module id:** `set-charm-gen` · **Program:** [item](../item-map.md) · **Build order:** 13 of 21 · ⭐ **model calls**
**Depends on:** `affix-legality` (8), `threshold-grants` (12), **X4** (L0 pool composition) · upstream [seedsmith/spec-demon-themes.md](../seedsmith/spec-demon-themes.md)
**Rulings:** D12, D15, D17, D27 · lanes [ssot-sets.md](ssot-sets.md), [ssot-charms.md](ssot-charms.md)

## Objective

The seedsmith pipeline that produces **36 build set families** (12 aptitudes × 3 archetypes) and **one
set plus one charm per demon species** (84 today → ~904 at the full roster), consuming the demon **theme
registry** one-way.

**And it owns set/charm atom effect distribution** — [item-map.md](../item-map.md) §7 row 2: *"no lane
owned it."* Membership, thresholds and the bind lifecycle are ssot-sets'; *which atoms a threshold
carries, and how strong* was ownerless. It lands here because it is a generator input, not a runtime rule.

## Design

### ⛔ P1 is the whole shape: the model writes identity, deterministic code writes magnitude

`seedsmith-map.md` P1, without amendment. The model picks **which capability**, **which stat families**,
**which member roles**, the name and the flavour. It never emits a number.

**And that is enforced mechanically, not by review.** `audit_schema`
(`tools/seedsmith/seedsmith/pipeline/model.py:53`) walks `properties` / `items` / `anyOf` / `oneOf` /
`allOf` and rejects any bare `number` or `integer` field (`NUMERIC_JSON_TYPES` at `:42`, `integer`
included deliberately — *"a per-mille integer is exactly the shape a model most plausibly invents"*).
`Pipeline.__post_init__` (`:143-149`) **raises at construction** if the schema has a defect, so a numeric
field cannot reach a model call at all. Every schema also needs a `blocked` variant (`BLOCKED_FIELD` at
`:39`, checked at `:92-97`).

Magnitudes come from `seedsmith.numerics.resolve` — bands → channel share → tier → number, with
`UnsharedChannelError` refusing to guess a channel with no authored weight
(`tools/seedsmith/seedsmith/numerics/resolve.py:12-19`).

| The model emits | Deterministic code resolves |
|---|---|
| `capability`: one family id from a closed list | its tier and value, from the threshold's band |
| `atoms[]`: family ids per threshold | each magnitude, via `numerics.resolve` |
| `members[]`: `(role, frame)` pairs | the concrete `baseType` id, by lookup |
| `thresholds[].pieces`: **an enum**, not an integer | — an `enum` of numbers is a vocabulary, and `audit_schema` allows it (`model.py:60-61`) |
| `name`, `nameKey`, `flavor` | id, `seq`, partition |

⚠ **`pieces` is the one place a number is legal, and only as a closed enum** (`{2,3,4,6}`). Written as a
bare `integer` the schema is rejected at construction. Say it in the schema, not in a comment.

### ⛔ Cap to the twelve hybrid-core roles **before** generating, not after

[item-map.md](../item-map.md) §7 row 1 and [item-ideal.md](../item-ideal.md) §2g #6. The reason is
mechanical: `SetRoleNotUniversal` fires **at load** (ssot-sets §3.7), so ~1,000 generated sets validated
after the fact is ~1,000 rejections and a re-run, not a lint pass.

**The twelve, enumerated — D3's prose names eleven** (it says *"both jewels"* where three jewel roles are
kept):

`armament-primary` `core-guard` `armament-secondary` `jewel-major` `manipulator` `mantle` `girdle`
`footing` `infusion` `retinue` `jewel-minor-a` `jewel-minor-b` — **800‰ exactly**, per
`budgetWeightMilli` in `data/seed/items/_registry/core.v1.json`.

⛔ **Three shipped sources say thirteen roles, and one of them gates CI.**

| Source | Says | Status |
|---|---|---|
| `core.v1.json` `roles.list[].hybridEligible` | drops `ward-array` + `jewel-minor-b` → 13 roles, 895‰ | **stale**, and the file is `"frozen": true` |
| `tools/seedsmith/seedsmith/adapters/items/registries.py:111` `HYBRID_FRAME_EXCLUDED_ROLES` | same two | stale; its `HYBRID_FRAME_CITATION` (`:105`) is asserted substring-present by `tools/seedsmith/tests/test_items_adapter.py:85` |
| `tools/seedsmith/seedsmith/metrics/linkage.py:28` `NON_HYBRID_ROLES` | same two, feeding `SetCompletability` whose **`gates = True`** (`:61`, not `:60`) | stale — the gate cannot currently see the defect |

**Measured consequence, run 2026-09-03:** `python -m seedsmith check data/seed/items --adapter items
--metric Linkage/SetCompletability` reports **no findings** today. Corrected to D3's three drops,
**18 of the 30 shipped sets go red on a gating metric** — every set using `head-guard` (10) or `sense`
(11). That is not a generator bug; it is the legacy corpus meeting a newer ruling.

> ✅ **RULED 2026-09-04 — D30: regenerate.** The 18 go under the twelve-role cap; grandfathering is
> declined. ⭐ **It costs no extra pass** — this module already regenerates the ~904, so the legacy sets
> ride along. Silently leaving the gate blind was never an option.
> ⚠ The registry edit is `registryVersion 2` plus *"an explicit decision on which partitions re-run"* —
> `core.v1.json`'s own `frozenNote`. **Module 3 (`slot-roles`) owns issuing it**; this module consumes it.

### The populations, and why there are 36 rather than 360

| Population | Grid | Today | Full roster |
|---|---|---:|---:|
| Build set families | 12 aptitudes × 3 archetypes | **36** | 36 |
| Species sets | 1 per species | 84 | ~904 |
| Species charms | 1 per species | 84 | ~904 |
| | | **204** | **~1,844** |

**D15 puts rarity on the member pieces**, not on the set: a *Might / offense* set is **one** set completed
from whatever rungs you hold. Ten-per-build would make nine of ten dead the moment you out-level them.

Aptitudes are shipped and closed — twelve, `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:40-51`, computed
as `PostureCount × PerPosture` (`:36`) so a thirteenth changes the count by construction. **Three
archetypes** (offense · defense · balance) are the only new authored vocabulary D12 introduces.

### ⭐ The capability vocabulary — enumerated and counted, because the alternative is reskins

ssot-sets §3.2 inverts the genre: **every set grants exactly one capability atom at its *lowest*
threshold** — a non-`stat.*` kind — and higher thresholds grant plain numbers. That makes the capability
the set's identity, so **the size of the capability vocabulary is the ceiling on how many genuinely
different sets can exist.**

**Counted against the live corpus** (`data/seed/items/affix-families/*.json`, 98 families, read 2026-09-03):

| Kind | Families | | Kind | Families |
|---|---:|---|---|---:|
| `resource.delta` | 9 | | `status.clear` | 3 |
| `status.apply` | 7 | | `shield.grant` | 3 |
| `resource.economy` | 7 | | `grid.spawn` | 1 |
| `spawn.entity` | 6 | | `grid.clear` | 1 |
| `board.action` | 4 | | `box.set` | 1 |
| | | | **total** | **42** |

Params are **fixed per family** (`atom.venomous` is always `poison`), so a family is one pick — except
three that generate variants (`atom.deathblast`, `atom.searing-strike`, `atom.warded`, each
`variants: {generate: "elements+omni"}` = 7). So:

```text
39 element-free families  +  3 × 7 variant families  =  60 distinct capability picks
```

**At ~904 species sets that is ~15 sets per capability.** The build-set grid is comfortable (36 over 60);
**the species tail is not, and the number should be said rather than discovered.**

**D17 accepts this, and its reasoning is what makes 15-to-1 defensible:** *"the bar for a species set is
recognition, not differentiation. It does not need to be distinguishable from 903 others; it needs to feel
like **that demon**."* Differentiation is carried by the **theme** — motifs, anti-motifs, expression rules,
name, flavour — not by the capability.

⚠ **But the honest reading is that the capability is nearly a *category*, not an identity, at roster
scale.** Two mitigations, both cheap, and neither invented here:

1. **Higher thresholds vary independently** — 33 `stat.derived` + 23 `stat.modify` families over 2–3
   threshold rows is a far larger space than 60.
2. **Capability families carry `roles`** (e.g. `atom.terraforming` is `retinue`/`jewel-major`/both jewel
   minors), so a set's member roles already narrow the legal capability pool. That is a constraint, and it
   is also what stops the picker collapsing onto the three or four most flattering capabilities.

### ⛔ The acceptance bar measured the wrong axis — the gate is the **cell**, not the capability

**This spec previously read: *"if fewer than ~40 of the 60 capabilities are used, the sets are
reskins."*** That is not a bar. At ~904 species sets, **passing it means 904 / 40 = 22.6 sets per
capability** — worse than the 15-to-1 this same section already flags as the honest problem. A gate
whose pass condition is worse than the concern that motivated it is measuring the wrong axis.

**The repo's own research settles it, and it was in the tree before this spec was written**
([docs/research/game-design/03-roster-scale.md](../../research/game-design/03-roster-scale.md) §2):

| Measured | Result |
|---|---|
| Pokémon keyed on **type combination alone** | 154 cells · **median 3** · max 75 |
| Pokémon keyed on **type + ability set** | **730 cells · median 1 · max 7 · 68% singletons** |
| A 900-unit roster's ability requirement (D&D 5e / PF2e) | **1,500–3,500 named ability instances** |
| This spec's capability vocabulary | **60** |

> *"Adding abilities alone takes the median cell from 3 species to 1. **Type is the coarse axis and was
> never doing the distinctness work.**"*

**The capability *is* the type here** — the coarse axis — and 60 of it against ~904 sets was never
going to carry distinctness. That is what mitigation #1 above already says and does not measure:
*"33 `stat.derived` + 23 `stat.modify` families over 2–3 threshold rows is a far larger space than 60."*
**Counted 2026-09-04, the same way the capability vocabulary was counted:**

```text
capability picks : 39 element-free  +  3 x 7 variant                        =  60
stat picks       :  2 element-free  + 31 x 7 variant  + 23 stat.modify      = 242
```

**So the gate becomes the cell, and the cell is what a player can actually tell apart:**

> ⭐ **Cell key = `(capability, sorted multiset of the stat families granted at every threshold above
> the lowest)`.** Over the generated species-set population the **median cell occupancy must be ≤ 2**;
> the singleton share and the max are reported beside it.

| Band | Roster | Units/cell | Median |
|---|---:|---:|---:|
| Summoners War | 832 | 1.02 | **1** |
| Honkai: Star Rail | ~95 | 1.7–1.8 | **2** |
| Arknights | 425 | 1.97 | **2** |
| ⛔ Fire Emblem Heroes — *"the worst documented in the genre"* | 1,410 | 15.3 | 7 (max **129**) |

**Median ≤ 2 is the band every well-regarded roster in the measurement sits in** (03-roster-scale.md
§1), and it is reachable by construction rather than by luck: two higher thresholds drawing one family
each from 242 is already `C(242,2) + 242 = 29,403` multisets per capability. The `roles` constraint on
each family narrows that, which is the point — it narrows a space that is orders of magnitude too
large, not one that is too small.

**Capability usage stays measured and reported** — it is the diagnostic that shows the picker
collapsing onto the three or four most flattering capabilities — but **it is no longer the gate**,
because passing it proves nothing about distinctness.

⚠ **`Distribution/CellOccupancy` over this dimension does not exist.** It is a module-13 build item,
not an existing check pointed at a new column. `Distribution/Evenness` and `Distribution/Inequality`
(`tools/seedsmith/seedsmith/metrics/distribution.py:100,147`) measure spread over *one* dimension's
cells and are the right shape to extend, not the right metric to reuse unchanged.

### The theme bridge — one-way, and 31 of 84 need an owner decision

`data/seed/demons/_registry/themes.v1.json` ships **84 themes** (`schemaVersion 1`, `registryVersion 1`),
each carrying `speciesId`, `displayName`, `rarity`, `motifs`, `antiMotifs`, `expression.item`,
`expression.action`, `basis`, `retired`. Demons publish; items consume; nothing in the items corpus writes
a demon (`spec-demon-themes.md` §2.2).

| Fact | Measured |
|---|---|
| Published themes | **84**, all `retired: false`, all with ≥1 motif — ⛔ **stale: `data/seed/demons/species/` holds 386** (292 plant + 94 zombie, counted 2026-09-04) |
| `basis` split | **53 `text` · 31 `name`** |
| `rarity` split | 42 common · 21 rare · 14 epic · 7 legendary |
| Motif language | **Chinese** — `displayName` and `motifs` are the species' own zh tokens (`"分配"`, `"火力"`), while set names in the corpus are English (`"Stillmarch"`) |

✅ **RESOLVED 2026-09-04 — D34, and the question was malformed.** The owner: *"84 number is a defect
… why don't you use pipeline to make LLM generate the name? other feature like demon species
generator, action generator do that."*

**`basis = "name"` is not a property of a species — it is a record of what the pipeline had when it
ran, and this pipeline generates the missing input.** So there is no Ask-first to answer and no
53-species wave-1 compromise to make: seedsmith's new **`theme-enrich`** stage raises name-basis themes
to text-basis before this module runs ([seedsmith-map.md](../seedsmith-map.md) §3c-ter), and
**`theme-refresh`** republishes the registry over the whole 386-species corpus first.

> ⚠ **Both numbers in the paragraph this replaces were wrong in the same way.** *"31 of 84 — 37%"* is a
> proportion of a **stale snapshot of a generated corpus**; the corpus is 386 and grows every run. The
> rate was never an input to a decision. **Filed as a seedsmith defect, not designed around.**

**Dependency added:** this module now waits on `theme-refresh` + `theme-enrich` rather than on an owner
flag per run.

⚠ **`rarity` on a theme is a snapshot, not an attribute** (§2.4a): `RarityForRank` is proportional in
`count`, so a species moves tier as the roster grows *without moving rank*. **Nothing this generator emits
may key on theme rarity.** A theme records the rarity it was published against precisely so a later reader
sees the drift instead of inheriting it.

### ⛔ Two id defects that would ship broken

**1. A build set has no theme, and `themeKey` is required.**
`tools/seedsmith/seedsmith/adapters/items/kinds.py:62-65` makes `set` require `{"themeKey", "members",
"thresholds"}`. The 36 build sets are keyed on `(aptitude, archetype)` and belong to no species.

> **Recommended: a third append-only theme population, prefix `build.`** — `build.might-offense`, 36 keys,
> collision-free against `theme.*` (legacy, 5 in use) and `demon.*` by construction, exactly the namespace
> split §2.2a already established. **Alternative:** make `themeKey` optional on `set` — rejected, because
> `spec-demon-themes.md` §7 lists making it *required* on `unique` as the intended direction, and
> loosening it here reverses that. ✅ **RULED 2026-09-04: the third `build.` namespace.** `themeKey`
> stays required on `set`.

**2. A demon `themeKey` cannot go into a set id.**
`naming.v1.json` (registryVersion **4**, `"frozen": true`) gives sets
`idTemplate: "set.{themeId}-{seq:03}"` with `partitionKey: "themeId"` and **five** pinned `themeIds`. A
demon theme key is `demon.allpeater` — substituting it yields `set.demon.allpeater-001`, **two dots**,
which fails `definitions.md` §1's grammar (body is `[a-z0-9-]+`, no dot;
`ContainerValidator.cs:17-19` mirrors it). Composed with ssot-sets §4.3's tier suffix it is worse.

> **The id uses the theme's `speciesId`, never its `themeKey`.** `set.allpeater-001`, tier
> `set.allpeater-001-04`. **Verified safe:** all 84 `speciesId`s are kebab-legal, and none collides with
> the five legacy `themeId`s. ⚠ `naming.v1.json` is frozen at v2 of its own note — *"from here a required
> change is v3 plus an explicit re-run decision"* — so widening `partitionCount` from 5 to ~904 is a
> registry bump, not an edit.

### Atom effect distribution — the capability this module claims

The rule set, stated as constraints a deterministic distributor enforces after the model has chosen
identity:

| Constraint | Source |
|---|---|
| Exactly **one** capability atom, at the **lowest** threshold | ssot-sets §3.2 |
| Every higher threshold: `stat.modify` / `stat.derived` **only** | §3.2 |
| **No `More`-op modifier on any set tier** | §3.5 rule 2 — `More` is multiplicative (`bulwark`, `savagery`) |
| Every set has a threshold at **2**; top threshold ≤ member count | §3.4 |
| A set claims **at most 6 roles**, and all of them are in the twelve-role core | §3.4, §3.7 |
| Charms: `Flat` only — **never `Increased` or `More`** | ssot-charms §3.4 |
| Charms: `max_tier` at most **one band below** an equip container of the same rarity | ssot-charms §3.4 |
| A family may not appear on both a `jewel-minor` base type and a charm | ssot-charms §3.6 — *at all*, not at a different tier |
| Magnitudes resolve through `numerics`, never the model | P1 |
| ⛔ **Total set value ≤ 1.5 AE per member piece** — a 4-piece set caps at 6.0 AE | ssot-sets §3.5 rule 3 — **handed here by module 12**; the distributor prices what it emits |
| ⛔ **No set owns both weapons** — at most one of `armament-primary` / `armament-secondary`; refusal `SetRoleForbidden` | ssot-sets §3.5 rule 4 — **handed here by module 12**. *"Weapons are where build identity lives; a set that owns both owns the build"* |
| A set piece rolls **2 fixed atoms + `pool_rolls = 2`** over a 2-tier window | ssot-sets §3.9 — see below |

### ⭐ I5 §3.9 — how a set piece rolls, and why a rare stays worth picking up

**Uncovered until now**, and it is a generator input: the distributor fixes a member piece's rolled
half at the same moment it fixes its identity half.

| Piece property | Value | Column |
|---|---|---|
| Fixed identity atoms | **2**, at a fixed tier | `effect_container_atom` |
| Rolled affixes | **2** | `pool_rolls = 2` |
| Tier window | **2 tiers wide**, from a set-specific pool | `min_tier` / `max_tier` |
| A rare of the same rung, for comparison | **4** rolled affixes, general pool, wider window | — |

**The deficit is flexibility, not raw power.** A set piece carries about as many modifier lines as a
rare, but two are fixed, so on any given build roughly 40% of the piece is off-plan. **Expected cost ≈
1.0–1.5 AE per piece** — exactly what the 1.5-AE-per-member budget above is sized to buy back.

**Both alternatives lose, and avoiding them is this generator's job, not the runtime's:**

| Alternative | Failure |
|---|---|
| **Fixed like a unique** (`pool_rolls = 0`) | once you own the set, every drop in those roles is dead — the loop stops for a quarter of the body |
| **Rolled like a rare** (`pool_rolls = 4`, general pool) | a set piece becomes a rare *plus* a set bonus — strictly better in isolation. **That is set jail arriving through the item layer**, where none of §3.5's five rules can reach it |

⭐ **This is what keeps rares relevant under D15** (rarity lives on the pieces): the two rolled affixes
vary, so there is a real chase for a good copy with a bounded tail, and *"a well-rolled rare beats a
badly-rolled set piece as an item, and the set bonus pays the difference back."*

⚠ **`pool_rolls` does not exist as a field** ([item-ideal.md](../item-ideal.md) §2g #12) — `Instantiator.Draw`
runs `DrawBudget` twice over `PrefixRolls` / `SuffixRolls`. Emit the two rolls as that pair; do not
author a column that is not there.

⚠ **`set_eligible` is settled, and not by this module.** It was deferred here twice; **module 7
resolved it by dropping it** — D15 makes the key vacuous (a set has no rarity and completes from
pieces of any rung), and module 7's **SC7** makes a registered key with no shipped consumer *reject at
seed load*, so a deferral would have shipped a seed file that fails. **This module's only obligation
is not to ask for it back**: re-adding `set_eligible` (or `charm_potency`, dropped for the same
reason) is an SC7 violation unless it arrives together with the code that reads it.

⚠ **The charm population is additive to what ships**, and the previous numbers here mixed two
populations. `data/seed/items/charms/` holds **70** entries: **60 authored charms** plus **10 resonance
containers** (5 axes × counts 2 and 3). The sentence separated them and then quoted the 70-row totals.
**Re-counted 2026-09-04:**

| | 60 authored | all 70 rows |
|---|---|---|
| Class split | **21 minor / 32 standard / 7 signet** | 31 / 32 / 7 |
| `ap_cost` | **1×21 · 2×21 · 3×11 · 5×7** | 1×31 · 2×21 · 3×11 · 5×7 |
| Axis skew (`economy` : each other) | **20 : 10** | 22 : 12 |

The ten resonance rows are all class `minor` at `ap_cost` 1, which is the entire difference. **The
authored-only figures are the ones this generator is measured against** — the resonance containers are
not charms a player carries.

⚠ **The axis skew is 2.0×, not 1.83×**, and *"this generator must not deepen it"* now has a number:
the authored population's axis Gini is **0.133**, and that is the ceiling in the verification table
below.

⭐ **X4 / L0 is a two-way dependency, not a consumer relationship.** [item-map.md](../item-map.md) §3 X4:
sets and charms **supply** the `set` and `charm` channels to effect-pipeline's pool composition, which is
*"what makes a set bonus worth collecting"*. L0 is **unspecced and unbuilt**. Generation can proceed —
channels are a weighting layer over an already-legal pool — but **the run's value is not provable until L0
lands**, and that should be said before tokens are spent.

### ⭐ Verification — every metric gets a threshold, and an unevaluated partition is `NOT_MEASURED`

**`SemanticDedup`, `Distribution/Evenness` and `Distribution/Inequality` appeared in *Commands* below
with no test row and no threshold.** A command with no threshold is something you run and then argue
about: nothing distinguished a **good** run from a merely **complete** one. All three ship
`gates = False` today and say so in their own output — *"measure-only — no gating threshold set yet"*
(`distribution.py:125,173`).

| Metric | Dimension | Threshold | Derived from |
|---|---|---|---|
| ⭐ **`Distribution/CellOccupancy`** — new | `(capability, higher-threshold family multiset)` | **median ≤ 2**; max and singleton share reported | Arknights 1.97 · HSR 1.7–1.8 · Summoners War 1.02, medians 2/2/1 (03-roster-scale.md §1) |
| `SemanticDedup/NearDuplicate` | generated names | **zero** `ExactDuplicateName` (already `GAP` severity); lexical near-duplicate pairs **≤ 0.5%** of the population | 0.5% is Pokémon's measured true near-duplicate rate — 18 pairs of 1,025, *every one a deliberate designed twin* (§2) |
| `Distribution/Inequality` | charm `axis` | **Gini ≤ 0.133** | the authored corpus's own axis skew (`economy` 20 : 10 × 4). *"A skew this generator must not deepen"* is now a number it cannot exceed |
| `Distribution/Inequality` | set `capability` | ⚠ **report only in wave 1**, promoted to a gate against the first run's baseline | no prior exists — `distribution.py:173` says so itself, and inventing one is the mistake the metric discipline names |
| `Distribution/Evenness` | set `capability` | ⚠ **report Pielou J + richness**, same disposition | *"nobody can name a correct Pielou value in advance"* (`distribution.py:95-98`) |
| `Linkage/SetCompletability` | generated sets | **zero findings** | already `gates = True` (`linkage.py:61`) |

⛔ **A partition that did not run reports `NOT_MEASURED`, never a pass.** `Severity.NOT_MEASURED`
already exists (`tools/seedsmith/seedsmith/metrics/model.py:34`) and the tool states the discipline:
*"a metric whose needs are unmet reports NOT_MEASURED, never a pass, and never a false GAP"*
(`adapters/actions/coverage_report/derive.py:292-293`). **The run verdict is `pass` only when every
gating metric both ran and cleared** — an empty species partition, an absent `budget` context, or a
held `basis = "name"` population each make the verdict `not_measured`, which is the honest answer and
the one this module must not launder into a green run.

⚠ **The two report-only rows are the honest half of this table.** They are not gates, and saying so is
cheaper than defending a threshold nobody can derive — but they must still appear in the run report. A
metric that runs and is never read is the same as one that never ran.

## Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_set_charm_gen.py -q

# generate (model calls) - see note below
python -m seedsmith items generate --kind set   --population build   --dry-run
python -m seedsmith items generate --kind set   --population species --dry-run
python -m seedsmith items generate --kind charm --population species --dry-run

# verify before believing the output
python -m seedsmith check ..\..\data\seed\items --adapter items --gate
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Linkage/SetCompletability
python -m seedsmith check ..\..\data\seed\items --adapter items --metric SemanticDedup/NearDuplicate
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Distribution/Evenness
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Distribution/Inequality
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Distribution/CellOccupancy
```

⚠ **Read a clean report only after checking the metric ran.** `SetCompletability` returns zero
findings today *because the gate is blind to D3*, and `Evenness` / `Inequality` return `NOTE` rows
that are not a verdict. The thresholds above are what turn these commands into a decision.

⚠ **No `items` subcommand exists today.** `build_parser` (`tools/seedsmith/seedsmith/report/cli.py:776-901`)
registers `check`, `report`, `metrics`, `demons`, `effects` and nothing else. The `demons run
start|pause|resume|cancel|rerun|status` harness (`:869-871`) is the pattern to mirror — a ~1,000-entry run
is exactly the shape that needs resume, and the resume path already holds a real atomic file lock.

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/setgen/           new
  brief.py         theme + aptitude + archetype -> a brief; motifs inline, no citation text
  schema.py        the closed-enum output schemas; audit_schema-clean by construction
  roles.py         THE TWELVE, enumerated - the cap applied before the call, not after
  distribute.py    capability at the lowest threshold; stats above; numerics for every magnitude
  emit.py          id = set.{speciesId}-{seq:03}; NEVER the themeKey
tools/seedsmith/seedsmith/adapters/items/charmgen/         new — same shape, charm rules
tools/seedsmith/seedsmith/report/cli.py                    EDIT — the `items` subcommand group
data/seed/items/_registry/core.v1.json                     registryVersion 2 (module 3 issues it)
data/seed/items/_registry/naming.v1.json                   registryVersion 3 — set partitions 5 -> ~904
data/seed/items/_registry/build-themes.v1.json             new — the 36 `build.*` keys, if approved
data/tuning/set-charm-gen.v1.json                          new — threshold ladders, band targets
data/seed/items/sets/, charms/                             generated output, partitioned by theme
```

## Code style

```python
# The cap is a GENERATOR INPUT, not a validation afterthought: SetRoleNotUniversal fires at LOAD
# (ssot-sets.md 3.7), so ~1,000 sets checked after the fact is ~1,000 rejections and a re-run.
# Enumerated, never derived from core.v1.json's hybridEligible flags - those still say thirteen
# roles (ward-array + jewel-minor-b), which D3 superseded and the frozen registry has not caught up to.
HYBRID_CORE_ROLES: tuple[str, ...] = (
    "armament-primary", "core-guard", "armament-secondary", "jewel-major",
    "manipulator", "mantle", "girdle", "footing",
    "infusion", "retinue", "jewel-minor-a", "jewel-minor-b",
)  # 800 permille exactly - asserted against core.v1.json's budgetWeightMilli, not assumed

# `pieces` is the ONLY numeric field in the schema and it is a closed enum. Written as
# {"type": "integer"} audit_schema rejects the schema at Pipeline construction (model.py:53,143).
THRESHOLD_PIECES = {"type": "integer", "enum": [2, 3, 4, 6]}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_schema_is_audit_schema_clean` | `audit_schema(SET_SCHEMA) == []` — mechanical P1, not review |
| `a_bare_integer_magnitude_field_fails_pipeline_construction` | the enforcement is the check, proven |
| `pieces_is_a_closed_enum_and_not_a_bare_integer` | the one legal numeric shape |
| `blocked_is_a_legal_answer_and_writes_nothing` | `on_persist` injected, never called |
| `every_generated_member_role_is_in_the_twelve` | ⭐ the cap, applied **before** the call |
| `the_twelve_sum_to_800_permille_against_the_registry` | not a transcribed constant |
| `a_generated_set_never_claims_head_guard_sense_or_ward_array` | D3's three drops, asserted by name |
| `SetCompletability_reports_zero_findings_over_the_generated_corpus` | the gating metric, on real output |
| `exactly_one_capability_atom_and_it_sits_at_the_lowest_threshold` | ssot-sets §3.2's inversion |
| `no_set_tier_carries_a_More_op_modifier` | §3.5 rule 2 |
| `every_set_has_a_threshold_at_two` | §3.4, no exceptions |
| `the_capability_vocabulary_is_60_picks_and_the_stat_vocabulary_is_242` | both counted from the corpus, not transcribed |
| `the_median_cell_occupancy_over_capability_plus_higher_thresholds_is_at_most_two` | ⭐ **the reskin bar, on the axis that carries distinctness** — replaces the old ≥40-of-60 gate, which passed at 22.6 sets per capability |
| `capability_usage_is_reported_and_does_not_gate` | the diagnostic stays; it is no longer the bar |
| `no_two_generated_entries_share_an_exact_name` | `SemanticDedup` `ExactDuplicateName`, zero tolerance |
| `the_lexical_near_duplicate_rate_is_at_most_half_a_percent` | Pokémon's measured true-twin rate as the ceiling |
| `the_charm_axis_gini_does_not_exceed_the_authored_corpus_value` | 0.133 — *"must not deepen"*, as a number |
| `an_unevaluated_partition_reports_NOT_MEASURED_and_the_run_verdict_is_not_pass` | ⭐ an unrun check may never read as a green run |
| `every_gating_metric_named_in_this_spec_has_a_threshold` | the meta-test: a command without a threshold is not verification |
| `a_set_piece_emits_two_fixed_atoms_and_two_rolls_over_a_two_tier_window` | I5 §3.9, the rolling rule |
| `no_set_piece_is_fixed_like_a_unique_or_rolled_like_a_rare` | both named failure modes, refused by construction |
| `no_set_claims_both_armament_roles` | ssot-sets §3.5 rule 4 — `SetRoleForbidden` |
| `a_sets_total_tier_value_never_exceeds_one_and_a_half_AE_per_member` | ssot-sets §3.5 rule 3 — the piece budget |
| `the_authored_charm_split_is_21_32_7_excluding_the_ten_resonance_rows` | the corrected counts, as a fixture over the shipped corpus |
| `set_eligible_is_never_requested_back_into_the_rarity_budget_registry` | module 7 dropped it under SC7; re-adding it without a consumer fails seed load |
| `no_charm_carries_Increased_or_More` | ssot-charms §3.4 |
| `no_family_appears_on_both_a_jewel_minor_base_and_a_charm` | ssot-charms §3.6 |
| `a_species_set_id_uses_speciesId_never_themeKey` | ⭐ `set.demon.allpeater-001` is ungrammatical |
| `a_tier_container_id_composes_to_one_dot_and_a_zero_padded_suffix` | `set.allpeater-001-04` |
| `no_generated_id_collides_with_a_legacy_theme_partition` | the five in-use `theme.*` ids |
| `no_theme_reaches_generation_at_basis_name` | ⭐ **D34** — `theme-enrich` ran; the population is empty, so the old flag test is replaced by an emptiness assertion |
| `the_theme_registry_covers_every_shipped_species` | ⛔ **D34** — 84-vs-386 staleness cannot recur silently |
| `nothing_generated_keys_on_theme_rarity` | §2.4a — rarity is a roster snapshot |
| `a_build_set_has_a_legal_themeKey` | the `build.*` population, or the approved alternative |
| `nothing_in_the_generator_writes_the_demons_corpus` | the one-way bridge, asserted structurally |
| `re_running_over_unchanged_themes_is_byte_identical` | seedsmith's own content-addressing law |
| `the_run_resumes_after_an_interrupt_without_duplicating_entries` | ~1,000 entries; resume is not optional |

## Boundaries

**Always:** apply the twelve-role cap before the model call; carry the theme's motifs, anti-motifs and
`expression.item` into the brief inline; resolve every magnitude through `numerics`; mark provenance
(`themeKey`, `basis`, the theme's published `rarity`) on every generated entry; id from `speciesId`.

**Ask first:** the `build.*` theme population vs. loosening `themeKey`; any `registryVersion` bump on a
frozen registry.

~~generating from a `basis = "name"` theme~~ — **closed by D34**: `theme-enrich` removes the population.
~~the disposition of the 18 legacy sets~~ — **closed by D30**: re-authored under the twelve-role cap, in
the same generation run this module already performs for the ~904.

**Always (verification):** record a threshold beside every metric this spec runs, and treat
`NOT_MEASURED` as a non-pass.

**Never:** read a clean report from a metric that did not run — or from one that is blind to the
ruling it is meant to enforce — as evidence of anything. Never let the model emit a weight, rate,
duration or magnitude — `audit_schema` rejects a numeric
field mechanically, and **that check is the enforcement, not review**. Never write into the demons corpus
or read a demon row from the items adapter. Never key generated content on a theme's `rarity`. Never
generate into `standard` (D14). Never put a set's member role outside the twelve. Never emit an id built
from a `themeKey`.

## Success criteria

- [ ] 36 build set families + one set and one charm per species — **no `basis = "name"` remains** (D34); the old `basis = "name"`
      population explicitly included or explicitly held by owner decision.
- [ ] Every generated member role is in the twelve-role hybrid core, **enforced before generation**, and
      `Linkage/SetCompletability` reports zero findings over the generated corpus.
- [ ] Exactly one capability atom per set, at the lowest threshold; no `More` op on any tier; no
      `Increased`/`More` on any charm.
- [ ] ⭐ **The distinctness gate is the cell, not the capability**: median occupancy of
      `(capability, sorted higher-threshold family multiset)` is **≤ 2** over the generated species
      sets, with singleton share and max reported. Capability usage is reported and does not gate.
- [ ] Every metric this spec runs has a **threshold** recorded beside it, and a partition that did not
      run reports **`NOT_MEASURED`** — the run verdict is `pass` only when every gating metric both ran
      and cleared.
- [ ] Set pieces roll to I5 §3.9 (2 fixed + 2 rolled over a 2-tier window), no set claims both armament
      roles, and no set exceeds 1.5 AE per member piece.
- [ ] Every magnitude comes from `numerics`; a numeric field in any schema fails `Pipeline` construction,
      proven by test.
- [ ] Ids derive from `speciesId`, compose to a legal `container_id` with its tier suffix, and collide
      with no legacy partition.
- [ ] The bridge stays one-way, asserted structurally; the 84 published themes and 38 legacy themed
      entries all still validate.
- [ ] A re-run over unchanged themes is byte-identical, and an interrupted run resumes without duplicates.
