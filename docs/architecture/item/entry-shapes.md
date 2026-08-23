# Entry shapes for the ten undefined kinds

**Status:** Proposed 2026-08-22. Closes the gap `tools/ItemSeedValidator` reports as
`UnknownKeyShapeUndefined` for every kind [seed-contract.md](seed-contract.md) §10 does not shape:
`gem` · `socket-word` · `material` · `recipe` · `curve` · `enhancement-milestone` · `charm` ·
`consumable` · `drop-table` · `display-template`. This document is an **extension of §10**, in its
style, under its rules — it does not amend seed-contract.md itself.

Every example below obeys seed-contract.md exactly: §2's four ownership levels, §3's ban on
authored magnitudes/weights/probabilities/quantities, §4's namespace-not-sequence id discipline,
§6's `nameKey` + string rule, and §9's common envelope. Registry values are real — pulled from
`data/seed/items/_registry/*.v1.json` and cross-checked against the ten lane SSOT documents named in
the brief. Where a lane document and a registry disagree, the registry wins; three such
disagreements are called out where they occur.

---

## 0. Reading this document

Each section gives, in order: the kind's directory and id namespace; a worked JSON example; a field
table (name, ownership level, required/optional, validating registry); and its rejection rules. A
**Finding** callout marks a place where the kind seems to need a raw number and does not get one —
per the brief, that is reported, not quietly authored around.

Two shapes recur across several kinds and are named once here instead of six times below:

- **Reference-not-magnitude for tier requirements.** Several kinds (socket words, recipes) need to
  say "at least this powerful" without saying "at least tier 3". Every such field in this document is
  named `minPowerBand` and takes a `powerBand` enum value, never a tier integer. A generator resolves
  it via `bands.v1.json`'s `powerBand.tierMap`.
- **Tracking id vs. runtime id.** For five kinds (`gem`, `socket-word`, `material`, `recipe`,
  `enhancement-milestone`), `naming.v1.json`'s allocated id template is a **meaningless, collision-safe
  tracking id** (e.g. `gem.g2-014`), not the runtime family/container id a player or another lane's
  document would recognise (`gem.ember-shard`, `essence.fire`, `atom.enhance-vigor`). Every such
  entry below carries **both**: `id` is the allocated tracking id; a second field (named per kind)
  carries the runtime-facing name the generator mints tiered rows or DB ids from. This is not
  invented here — `naming.v1.json` says as much for uniques and sets already (`idVsContainerIdNote`)
  and the same shape recurs for every kind whose allocation exists purely to prevent sequence
  collision.

---

## 1. `gem` — socket inserts

**Directory:** `data/seed/items/gems/` · **id namespace:** `naming.v1.json` → `idNamespaces.gems`,
template `gem.g{slot}-{seq:03}` · **stage:** 1a.

An insert is a fixed container (`container_kind = 'gem'`, `pool_rolls = 0`) that grants an existing
affix family at a chosen power, per [ssot-sockets.md](ssot-sockets.md) §4.3, §5.1. A gem entry does
**not** invent a new atom family — it references one, exactly as a base type's `implicit` does
(seed-contract.md §10).

```json
{
  "id": "gem.g2-014",
  "nameKey": "gem.ember-shard",
  "name": "Ember Shard",
  "family": "atom.elemental-power",
  "element": "fire",
  "powerBand": "medium",
  "affinityElement": "fire",
  "iconKey": "icon.gem.ember-shard",
  "tags": ["offensive"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (in `gems` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | seed-contract.md §6 |
| `family` | AUTHORED (reference) | required | must resolve to a shipped or sibling-authored `atom.*` family — `ReferenceUnresolved` else |
| `element` | VALIDATED | optional | `core.v1.json` → `elements` (six concrete + `omni`); omitted for a family with no element variant |
| `powerBand` | AUTHORED (band) | required | `bands.v1.json` → `powerBand.enum` |
| `affinityElement` | VALIDATED | optional | same registry as `element`; the socket-affinity hint a base type may copy per [ssot-sockets.md](ssot-sockets.md) §4.2 — usually equal to `element`, but distinct fields because a gem could exist with no natural affinity read |
| `iconKey` | AUTHORED | optional | none — stable key |
| `tags` | VALIDATED | optional | `tags.v1.json`, axes with `appliesTo` including a gem-shaped kind |
| *(GENERATED, never authored)* | `gem.ember-shard.t1`…`.t5` — five tiered containers, `resonances`, `weight` | — | §8's element-variant-explosion / ilvl-band-container-copies rule |

**Rejects:** `family` outside the atom-kind registry or unresolved; `element` or `affinityElement`
outside the six-plus-`omni` set; `powerBand` outside `bands.v1.json`; a `pool_rolls`, `weight`, or
tier field present (OwnershipViolation); the id outside the `gems` prefix or its 001–899 wave-1 range.

**Finding.** `naming.v1.json`'s three-way slot split (`g1`/`g2`/`g3`) has **no thematic or mechanical
meaning** — its own `openAmbiguities` says so. This document does not invent one either; a gem's
`id` slot number is load-bearing only for collision-safety, never for content.

---

## 2. `socket-word` — named ordered socket recipes

**Directory:** `data/seed/items/socket-words/` · **id namespace:** `socketWords`, template
`sockword.{seq:03}` · **stage:** 1b (references frozen 1a gems/materials).

A word is a `gem.word-*` combination container plus a `socket_combo_recipe` + `socket_combo_ingredient`
row set ([ssot-sockets.md](ssot-sockets.md) §5.2, §7.2). The seed authors the recipe and the bonus's
identity; the bonus's own atoms are authored the same way a unique's `fixedAtoms` are.

```json
{
  "id": "sockword.019",
  "nameKey": "sockword.frostfire",
  "name": "Frostfire",
  "runtimeId": "gem.word-frostfire",
  "hostRole": "armament-primary",
  "hostFrame": "plant",
  "minSockets": 3,
  "ingredients": [
    { "position": 0, "family": "atom.searing-strike", "minPowerBand": "high" },
    { "position": 1, "family": "atom.rime-tear", "minPowerBand": "high" },
    { "position": 2, "family": "atom.searing-strike", "minPowerBand": "high" }
  ],
  "fixedAtoms": [
    { "family": "atom.searing-strike", "powerBand": "high", "params": { "element": "fire" } },
    { "family": "atom.cruelty", "powerBand": "medium", "params": { "element": "ice" } }
  ],
  "tags": ["offensive"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`socketWords` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6; collision-normalized against the whole corpus |
| `runtimeId` | AUTHORED (reference-shaped, `gem.word-{slug}`) | required | must match `gem.word-[a-z0-9-]+`, hyphen-only body |
| `hostRole` | VALIDATED | optional | `core.v1.json` → `roles.list` |
| `hostFrame` | VALIDATED | optional | `core.v1.json` → `roles.frames` |
| `minSockets` | AUTHORED (structural count) | required | integer 1–4 — see Finding below |
| `ingredients[].position` | AUTHORED (structural count) | required | 0-based, consecutive from 0, no gaps |
| `ingredients[].family` | AUTHORED (reference) | required | resolves to a shipped/sibling `atom.*` family |
| `ingredients[].minPowerBand` | AUTHORED (band) | required | `bands.v1.json` |
| `fixedAtoms[].family` / `.powerBand` / `.params` | AUTHORED, same shape as unique's `fixedAtoms` | required (≥1) | as seed-contract.md §10's unique example |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(GENERATED)* | the `gem.word-*` container's tier magnitudes, `socket_combo_recipe.threshold` | — | §8 |

**Rejects:** `minSockets` > 4; non-consecutive `position` values; more than one word naming the same
`(hostRole, hostFrame)` position-set with the same ingredients (`NameCollision` on the runtime id, by
analogy); a family in `ingredients` that does not resolve.

**Finding.** `minSockets` (an integer 1–4) is not literally a magnitude in the balance sense — it is
structurally identical to `socketMax`, already on the allowed structural-count list. It is not,
however, on `OwnershipCheck.StructuralCountFields` today (`socketMax`, `pieces`, `pool_rolls`,
`poolRolls`, `rung`, `ordinal`, `seq`, `sequence`). Recommend the validator's allowlist widen to admit
`minSockets` (and, from §3–§7 below, `apCost`, `outputQty`) rather than growing a bespoke exception
per kind — a single documented rule ("structural counts bounded by a small closed range, distinct
from any magnitude/weight/probability/quantity axis") would cover all four without repeated asks.

---

## 3. `material` — the 21 fixed material ids

**Directory:** `data/seed/items/materials/` · **id namespace:** `materials`, template
`material.{seq:03}` · **stage:** 1a.

[ssot-materials-crafting.md](ssot-materials-crafting.md) §3.1 already fixes the runtime vocabulary
completely — 21 named ids across four classes (`essence.*`, `shard.*`, `substrate.{frame}.{grade}`,
`catalyst.*`). The authoring partition supplies **flavour, not identity**: a name, an icon, and tags
for each already-fixed id.

```json
{
  "id": "material.007",
  "nameKey": "material.essence-fire",
  "name": "Ember Essence",
  "runtimeId": "essence.fire",
  "materialClass": "essence",
  "element": "fire",
  "iconKey": "icon.material.essence-fire",
  "tags": ["arcane"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`materials` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `runtimeId` | AUTHORED (reference-shaped) | required | must be one of the 21 ids in §3.1's list — **Finding**, below |
| `materialClass` | VALIDATED | required | closed 4-value enum: `essence` \| `shard` \| `substrate` \| `catalyst` |
| `element` | VALIDATED | required only when `materialClass = essence` | `core.v1.json` → `elements.concrete` (six, `omni` excluded — §3.1 is explicit) |
| `frame`, `grade` | VALIDATED / AUTHORED (structural rung) | required only when `materialClass = substrate` | `frame` from `core.v1.json`; `grade` an ordinal 1–4 (crude/sound/fine/prime) |
| `iconKey` | AUTHORED | optional | — |
| `tags` | VALIDATED | optional | `tags.v1.json` |

**Rejects:** `runtimeId` not one of the 21 fixed ids; `materialClass` disagreeing with `runtimeId`'s
own prefix; two entries claiming the same `runtimeId`; an `element` on a non-essence row.

**Finding — a genuine registry gap, not a design choice.** No file under `data/seed/items/_registry/`
enumerates the 21 legal material ids, so the validator cannot check `runtimeId` against anything today
— it can only be checked by hand against ssot-materials-crafting.md §3.1's prose list. Recommend a
`materials.v1.json` registry (21 rows: id, class, element/frame/grade as applicable) be minted from
that list before this partition runs, exactly the way `themes.v1.json` was minted from the theme lane
document. Until it exists, `runtimeId` is AUTHORED-but-unvalidated — a contract gap, not a shape
decision this document can close on its own.

**Registry-vs-lane-document disagreement, noted per the brief.** `naming.v1.json`'s allocated
`material.{seq:03}` id (e.g. `material.007`) has no relationship to the runtime id
(`essence.fire`). The registry (naming.v1.json) governs the authored `id`; the runtime id lives in
`runtimeId`, per the tracking-id-vs-runtime-id split in §0.

---

## 4. `recipe` — craft / salvage / enhance / reroll shapes

**Directory:** `data/seed/items/recipes/` · **id namespace:** `recipes`, template `recipe.{seq:03}` ·
**stage:** 1b (references frozen base types, gems, materials).

Mirrors `material_recipe` / `material_recipe_cost`
([ssot-materials-crafting.md](ssot-materials-crafting.md) §6.1–§6.2), with every quantity replaced by
a band, per §3's rule and per `bands.v1.json`'s own stated purpose for `costBand` ("what an author
writes on a recipe … instead of a souls count or a material quantity").

```json
{
  "id": "recipe.027",
  "nameKey": "recipe.forge-thorn-briar",
  "name": "Forge: Thorn Briar",
  "operation": "forge",
  "outputKind": "container",
  "outputRef": "item.plant-thorn-b-011",
  "outputQty": 1,
  "frame": "plant",
  "costLines": [
    { "material": "substrate.plant.sound", "costBand": "modest" },
    { "material": "catalyst.forge", "costBand": "cheap" }
  ],
  "soulsCostBand": "modest",
  "tags": ["crafted-only"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`recipes` namespace) | required | `naming.v1.json`; prefix must match `operation` per `IdMismatch` |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `operation` | VALIDATED | required | closed 7-verb enum: `forge` `upcycle` `elevate` `temper` `reroll` `bore` `socket` |
| `outputKind` | VALIDATED | required | `container` \| `material` \| `mutation` |
| `outputRef` | AUTHORED (reference) | required when `outputKind ≠ mutation` | resolves to a sibling base-type/gem/consumable id, or a material's `runtimeId` |
| `outputQty` | AUTHORED (structural count) | optional, default 1 | integer ≥ 1 — see the §2 Finding; the batch-output pattern ([ssot-consumables.md](ssot-consumables.md) §7.5) needs this to express "one recipe → five potions" without a raw magnitude elsewhere |
| `frame` | VALIDATED | required | `humanoid` \| `plant` \| `any` |
| `costLines[].material` | AUTHORED (reference) | required, 1–4 lines | resolves to a `material.*` entry's `runtimeId` |
| `costLines[].costBand` | AUTHORED (band) | required | `bands.v1.json` → `costBand.enum` |
| `soulsCostBand` | AUTHORED (band) | optional | same enum; §3.1's souls fee is "any other spend" per `bands.v1.json`'s own scope note |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(DERIVED/GENERATED, never authored)* | `souls_cost`, `qty` per cost line, `qty_curve_id` | — | resolved by the generator from `costBand`'s `multiplierTable` against a base quantity the reference cost table (ssot-materials-crafting.md §7.4) supplies |

**Rejects:** `operation` outside the seven-verb enum; a `costLines[].material` whose class the
operation may not spend (`CostClassForbidden` — e.g. `forge` spending `catalyst.temper`, per
ssot-materials-crafting.md §3.2's verb table); `outputRef` unresolved; `id` prefix disagreeing with
`operation`.

---

## 5. `curve` — the numeric resolution tables

**Directory:** `data/seed/items/curves/` · **id namespace:** `curves`, template `curve.{seq:03}` ·
**stage:** 1a.

**This is the kind the brief calls out by name, and it deserves the direct answer.** A curve file's
whole content is numbers — an ordered list of `(input, multiplierPerMille)` points, matching the
shipped `effect_curve` / `CurveTable` mechanism (`CurveInput.Rarity`/`.Level`/`.Tier`, cited in
[ssot-rarity.md](ssot-rarity.md) §3.6, §8.5, and used directly in
[ssot-materials-crafting.md](ssot-materials-crafting.md) §7.5's `curve.band-linear` example). There is
no way to author a curve without typing a number, because a curve *is* the formula every other kind
defers to instead of typing one.

**Who owns the numbers, and why that does not break §3.** Exactly **one** wave-1a partition — one
agent, per [authoring-fleet-plan.md](authoring-fleet-plan.md) §3's table — authors all ~25 curve
files, and it is not one of the 124 volume-authoring partitions in the sense §3 worries about. The
rule §3 exists to enforce is "no two of 125 independent authors invent 125 different numeric
systems." A single, reviewed, narrowly-scoped partition producing the *one* numeric resolution table
everyone else references by id is not that failure mode — it is exactly the shape
`data/seed/items/_registry/bands.v1.json` already takes: `bands.v1.json` is itself full of real
numbers (`multiplierTable`, `weightTable`, `widthTable`), authored once by a single wave-0 owner
(F7), frozen, and referenced everywhere else by band *name*, never by number. The `curve` kind is
that same pattern instantiated inside wave-1a instead of wave-0: **every other kind in this corpus
writes a `curveId` reference or a band; only this one partition, reviewed as a single artefact,
writes the numbers those references resolve to.** Collision and drift are impossible by
construction, the same way `bands.v1.json`'s numbers cannot drift into 125 different interpretations
— there is exactly one file per curve, one owner, one review pass.

```json
{
  "id": "curve.014",
  "nameKey": "curve.band-linear",
  "name": "Band Linear",
  "input": "rarity",
  "points": [
    { "atOrdinal": 1, "multiplierPerMille": 1000 },
    { "atOrdinal": 2, "multiplierPerMille": 2000 },
    { "atOrdinal": 3, "multiplierPerMille": 3000 },
    { "atOrdinal": 4, "multiplierPerMille": 4000 }
  ],
  "tags": []
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`curves` namespace) | required | `naming.v1.json`; matches the shipped `curve_id` grammar `^curve\.[a-z0-9-]+(\.[a-z0-9-]+)*$` directly (no translation layer, per `naming.v1.json`'s own note) |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `input` | VALIDATED | required | closed 3-value enum: `level` \| `rarity` \| `tier` — `ScopeUnsupported` on any other value, per ssot-materials-crafting.md §6.2's `qty_curve_id` restriction |
| `points[].atOrdinal` | AUTHORED (structural count) | required, ≥2 points | an ordinal in the domain named by `input` (a rarity ordinal, a level, or a tier 1–5) |
| `points[].multiplierPerMille` | **AUTHORED — exempt from §3's magnitude ban for this kind only** | required | integer, ≥0; this is the one field in the entire corpus where a magnitude is the content, not a violation of it |
| `tags` | VALIDATED | optional | `tags.v1.json` |

**Rejects:** `input` outside the three-value enum; fewer than two points; non-monotone `atOrdinal`
sequence; two curves sharing a `nameKey`.

**Validator amendment implied, stated so it is not a silent exception.** `OwnershipCheck.CheckMagnitude`
today scans every field in every kind for a bare JSON number outside the structural-count allowlist,
with no per-kind carve-out. This kind needs exactly one: `kind == "curve"` must exempt
`points[].multiplierPerMille` (and only that field) from the magnitude scan. Every other kind in this
corpus, including this one's own `atOrdinal`, stays subject to the existing rule. This is a narrow,
named exception — not a general escape hatch — and it should be implemented as a single `if (kind ==
"curve" && key == "multiplierPerMille") return;` guard, not a blanket kind-level bypass.

---

## 6. `enhancement-milestone` — reserved atom families for the `+X` track

**Directory:** `data/seed/items/enhancement-milestones/` · **id namespace:** `enhancementMilestones`,
template `enh.{seq:03}` · **stage:** 1b (references frozen 1a affix-family output for its atom-kind
vocabulary, though it mints its own family stems).

Per [ssot-enhancement.md](ssot-enhancement.md) §5.5, a milestone is an ordinary five-tier atom family
— shaped exactly like an affix family — drawn from a **reserved stem space no affix pool may ever
draw from** (`atom.enhance-*`), so a rolled item can never collide with a milestone on
`(family_id, variant)`. This kind authors the family; **which base type grants it at which milestone
level** is `item_enhance_track`, authored on the `base-type` kind (out of this document's scope — the
brief names ten kinds and base-type already has a shape).

```json
{
  "id": "enh.005",
  "nameKey": "affix.enhance-vigor",
  "name": "Enhancement Vigor",
  "runtimeFamily": "atom.enhance-vigor",
  "kindId": "stat.modify",
  "params": { "channel": "maxHp", "op": "Flat" },
  "powerBand": "medium",
  "tags": ["defensive"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`enhancementMilestones` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `runtimeFamily` | AUTHORED (reference-shaped) | required | must match `^atom\.enhance-[a-z0-9-]+$` — the reserved stem, per ssot-enhancement.md §5.5 |
| `kindId` | VALIDATED | required | `AtomKindRegistry` — same closed vocabulary affix families use |
| `params` | AUTHORED | required | same shape as an affix family's `params` |
| `powerBand` | AUTHORED (band) | required | `bands.v1.json`; the five tiers this generates map 1:1 to the five milestone rungs (+4/+8/+12/+16/+20) via `bands.v1.json`'s own `tierMap` |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(DERIVED)* | `affixClass` | — | never authored; milestones are neither prefix nor suffix and are excluded from the pool entirely, not merely classified |
| *(out of this kind's scope)* | which base type grants this family at which of +4/+8/+12/+16/+20 | — | `item_enhance_track`, authored on `base-type` entries |

**Rejects:** `runtimeFamily` outside the `atom.enhance-` stem; a `runtimeFamily` colliding with an
existing affix family's stem; `kindId` outside the registry; two milestone entries sharing a
`runtimeFamily`.

---

## 7. `charm` — carried, unequipped bonuses

**Directory:** `data/seed/items/charms/` · **id namespace:** `charms`, template
`charm.{axisGroupId}-{seq:03}` · **stage:** 1b.

Per [ssot-charms.md](ssot-charms.md) §3.4, §4.2: a `charm.` container with a `charm_def` row. Three
classes by AP cost — Minor (1), Standard (2–3), Signet (5, hand-authored, `unique_carry = 1`).

```json
{
  "id": "charm.surv-util-011",
  "nameKey": "charm.hardened-seedcase",
  "name": "Hardened Seedcase",
  "charmClass": "standard",
  "apCost": 2,
  "axis": "survivability",
  "frameHint": "any",
  "fixedAtoms": [
    { "family": "atom.vitality", "powerBand": "medium" }
  ],
  "roleGroups": ["atom.mending"],
  "poolRolls": 1,
  "tags": ["defensive"]
}
```

A Signet (fixed-unique, no roll, mandatory drawback per ssot-charms.md §6.1 charm 3):

```json
{
  "id": "charm.off-ctrl-014",
  "nameKey": "charm.signet-hollow-crown",
  "name": "Signet of the Hollow Crown",
  "charmClass": "signet",
  "apCost": 5,
  "axis": "offense",
  "frameHint": "any",
  "uniqueCarry": true,
  "fixedAtoms": [
    { "family": "atom.might", "powerBand": "high" },
    { "family": "atom.vitality", "powerBand": "medium", "params": { "sign": "negative" } }
  ],
  "tags": ["offensive", "signature"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`charms` namespace, `axisGroupId` prefix) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `charmClass` | VALIDATED | required | closed 3-value enum: `minor` \| `standard` \| `signet` |
| `apCost` | AUTHORED (structural count) | required | closed set {1, 2, 3, 5} — never rolled, per ssot-charms.md §3.3's explicit rule; see the §2 Finding on widening the structural-count allowlist |
| `axis` | VALIDATED | required | **Finding, below** |
| `frameHint` | VALIDATED (charm-local enum) | required | `any` \| `humanoid` \| `plant` — a charm-specific vocabulary, not `core.v1.json`'s `frames` list, because `any` is not a real frame |
| `uniqueCarry` | AUTHORED (flag) | optional, default `false` | boolean only — `unique_carry = 1` iff `charmClass = signet` |
| `fixedAtoms[].family` / `.powerBand` / `.params` | AUTHORED, same shape as unique's `fixedAtoms` | required, ≥1 | as seed-contract.md §10 |
| `roleGroups` | AUTHORED (reference list) | optional | family ids naming the rolled pool group, for `charmClass = standard` only |
| `poolRolls` | AUTHORED (structural count) | required when `roleGroups` present | 0 for `minor`/`signet`, 1–2 for `standard` |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(DERIVED, never authored)* | resonance tier magnitudes (`charm_resonance` rows) | — | GENERATED, one set per axis, per ssot-charms.md §6.2 |

**Rejects:** `apCost` outside {1, 2, 3, 5}; `apCost` disagreeing with `charmClass` (minor≠1,
signet≠5); `uniqueCarry: true` on a non-signet; a `fixedAtoms` family carrying `op: Increased` or
`op: More` (`CharmAtomNotPermitted` per ssot-charms.md §5.2 — charms are Flat-only); a family also
legal on a `jewel-minor` base-type's implicit slate (the §3.6 family-split rule — a shared family
between the two is an authoring collision this validator should flag, though it is not yet a named
reason code anywhere read this session).

**Finding.** No registry in `data/seed/items/_registry/` owns the five-axis charm/resonance
vocabulary (`offense`, `survivability`, `control`, `utility`, `economy`). It is not the same
vocabulary as `tags.v1.json`'s three-value `combat-posture` axis (`offensive`/`defensive`/`utility`)
— a charm's `axis` and an item's `combat-posture` tag are different closed sets that happen to share
two words. Recommend a small `axes.v1.json` (or an extension to `themes.v1.json`, which already owns
the twenty registered-elsewhere-in-code five power categories per
[atom-family-library.md](../effect-atom/atom-family-library.md) §3) before this partition runs. Until
it exists, `axis` is AUTHORED-but-unvalidated here.

---

## 8. `consumable` — spent-on-use items

**Directory:** `data/seed/items/consumables/` · **id namespace:** `consumables`, template
`consumable.k{slot}-{seq:03}` · **stage:** 1b.

Per [ssot-consumables.md](ssot-consumables.md) §5.2: a `consumable.` container plus a
`consumable_def` row, fixed core only (`pool_rolls = 0`, no rarity). Six classes; v1 authors
`restore`, `draught`, `ward` and declares `board`/`revive`/`utility` only (§3.1).

The `draught` class avoids the one open question in this kind (§4.2's `OnUse` trigger request is
still unresolved, so an instant-fire consumable cannot yet be authored without naming a trigger the
atom-kind registry does not accept):

```json
{
  "id": "consumable.k2-013",
  "nameKey": "consumable.draught-fire-power",
  "name": "Ember Draught",
  "classId": "draught",
  "useContext": ["dispatch"],
  "family": "atom.elemental-power",
  "element": "fire",
  "powerBand": "medium",
  "manifestCost": 1,
  "tags": ["offensive"]
}
```

A declare-only class (§3.1's `board`, authored to exist but not yet consumable):

```json
{
  "id": "consumable.k1-021",
  "nameKey": "consumable.board-lightning",
  "name": "Storm Flask",
  "classId": "board",
  "useContext": ["lawn"],
  "family": "atom.dooming",
  "powerBand": "medium",
  "manifestCost": 1,
  "tags": ["utility"]
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`consumables` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6 |
| `classId` | VALIDATED | required | closed 6-value enum: `restore` `draught` `ward` `board` `revive` `utility` |
| `useContext` | VALIDATED (list) | required | closed set: `menu` `dispatch` `battle` `lawn`. v1 authors only `menu`/`dispatch`/`lawn` (declare-only) |
| `family` | AUTHORED (reference) | required | resolves to a shipped/sibling `atom.*` family |
| `element` | VALIDATED | optional | `core.v1.json` → `elements`, when the family carries a variant |
| `powerBand` | AUTHORED (band) | required | `bands.v1.json`; a generator derives `grade` (1–5) from this band's tier — `grade` is never authored directly |
| `manifestCost` | AUTHORED (structural count) | optional, default 1 | how many of the `N`-place manifest this consumable occupies — see the §2 Finding |
| `grantsActionId`, `cooldownKey` | AUTHORED (reference) | optional, `NULL` in v1 | the seam to the unbuilt action layer; both stay absent until `A1` lands |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(DERIVED, never authored)* | `grade`, `exclusionGroup` | — | `grade` from `powerBand`'s tier; `exclusionGroup` defaults to `(family, element)` per the shipped `group` default |

**Rejects:** `classId` outside the six-value enum; `useContext` naming `battle` before the action
layer exists (`UseContextUnsupported`, per ssot-consumables.md §6.2); any authored `chance` or
`icd_ms` field (`ParamNotHonoured` — the lifecycle grant path bypasses both, so authoring either is a
lie about what fires); a `pool_rolls`, `rarity`, or tier window present (`ConsumableRolls`).

---

## 9. `drop-table` — loot tables

**Directory:** `data/seed/items/drop-tables/` · **id namespace:** `dropTables`, template
`droptable.d{slot}-{seq:03}` · **stage:** 1b.

The authored seed is **generator input**, per seed-contract.md §1, for the DB shape
[ssot-generation.md](ssot-generation.md) §5.1 already fixes (`drop_table` /
`drop_table_group` / `drop_table_entry`). The generated rows carry real integer weights and
counts; the seed file never does — every weight becomes a `dropBand`, and every count becomes a
reference to a `curve` entry rather than a literal `min`/`max`.

```json
{
  "id": "droptable.d1-006",
  "nameKey": "droptable.forest-general",
  "name": "Forest General Drops",
  "sourceAllow": ["web"],
  "groups": [
    {
      "groupKey": "gear",
      "entries": [
        { "entryKind": "equipment", "role": "girdle", "frame": "plant", "dropBand": "occasional" },
        { "entryKind": "material", "ref": "essence.fire", "dropBand": "frequent", "qtyCurve": "curve.qty-material-standard" },
        { "entryKind": "nothing", "dropBand": "staple" }
      ]
    }
  ],
  "tags": []
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`dropTables` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6 — the table itself is not player-facing, but the envelope requires a key + string |
| `sourceAllow` | VALIDATED | required | closed set `web` \| `injector` \| `sim` — **must contain `web`**, the standalone-first rule (ssot-generation.md §4.6 rule 2) |
| `groups[].groupKey` | AUTHORED (reference id) | required | free identifier, unique within the file |
| `groups[].entries[].entryKind` | VALIDATED | required | closed 9-value enum: `equipment` `material` `currency` `insert` `charm` `consumable` `unique` `table` `nothing` |
| `groups[].entries[].role` / `.frame` | VALIDATED | required for `entryKind = equipment` | `core.v1.json` |
| `groups[].entries[].ref` | AUTHORED (reference) | required for `material`/`currency`/`insert`/`charm`/`consumable`/`unique`/`table` | resolves to a `material.runtimeId`, a currency id (`souls`), a sibling `gem.*`/`charm.*`/`consumable.*`/`unique.*` id, or another `droptable.*` id |
| `groups[].entries[].dropBand` | AUTHORED (band) | required | `bands.v1.json` → `dropBand.enum` |
| `groups[].entries[].qtyCurve` | AUTHORED (reference to a `curve` entry) | optional, only for stacking classes | resolves to a `curve.*` id whose `input` the generator applies |
| `groups[].entries[].rarityFloor` | VALIDATED | optional | a `rarity_id` (never an ordinal) from `core.v1.json` → `rarity.ladder` |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(GENERATED, never authored)* | `weight` (a plain integer), `min_count`/`max_count`, `rarity_weight_shift_json` | — | resolved from `dropBand`'s weight table and `qtyCurve`'s points, exactly the way `costBand` resolves a recipe's raw quantity |

> **Added 2026-08-23 (wave R2).** `unique` and `consumable` join the enum. Before them the corpus
> had 144 uniques, 70 charms and 60 consumables that no table could yield — every one referentially
> perfect and unobtainable, which is the class of defect `tools/seed_graph` exists to catch.
> `charm` was already in the enum and simply never used.
>
> A unique is granted **by id and never categorically**. `ssot-uniques.md` §4.5 puts it plainly —
> *"a drop-table entry naming a unique's container is the same entry shape as one naming a base
> type"* — and a categorical grant would make every unique in a rung band interchangeable, which is
> exactly the convergence §3.7 spends three rules preventing. Consumables and charms may be granted
> by id too; they carry no identity that a category would flatten.
>
> The band → channel mapping the corpus uses, from §4.5 and its rung table:
>
> | Rung band | `acquisition` | Channel |
> |---|---|---|
> | 30 | `drop` | d1, general table, low weight |
> | 50 | `source-locked` | d2 — §4.5 calls this *the primary channel* |
> | 70 | `source-locked` | d2 |
> | 90 | `deterministic` | d4, crafted — I1 requires the top rung have a deterministic source |
>
> `acquisition = 'drop'` at ordinal ≥ 90 is the refusal `UniqueUnreachable`, so band 90 never
> appears in d1.

**Rejects:** `sourceAllow` omitting `web`; an entry reachable only from `injector`/`sim` and not from
`web` (`StandaloneRuleViolation`); `entryKind = equipment` with no `role`/`frame`; `ref` unresolved
(`UnknownContainer`/`UnknownDropTable` per entry kind); a nested `table` entry deeper than 3 or
cyclic (`DropTableDepthExceeded` / `DropTableCycle`).

**Finding.** `min_ilvl`/`max_ilvl` on the generated `drop_table` row (ssot-generation.md §5.1) are a
genuine magnitude with no band today. This document deliberately does **not** invent one: per
ssot-generation.md §4.1, item level is a property of the *calling content* (`loot_source.content_level`),
not of the drop table itself, so the seed file should not author an ilvl band at all — the generator
resolves the table's reachable band from every `loot_source` row that points at it, which is
infrastructure outside this corpus. If a table genuinely needs its own narrower band independent of
any caller, that is an open gap this document surfaces rather than closes.

---

## 10. `display-template` — card-line templates

**Directory:** `data/seed/items/display-templates/` · **id namespace:** `displayTemplates`, template
`disptpl.p{slot}-{seq:03}` · **stage:** 1b.

Mirrors `item_display_template` ([ssot-presentation.md](ssot-presentation.md) §5.3, N1): one row per
family, a template key plus its string, authored together per seed-contract.md §6.

```json
{
  "id": "disptpl.p1-009",
  "nameKey": "tpl.affix.vitality",
  "name": "+{value} {unit}",
  "runtimeFamily": "atom.vitality",
  "plantOverrideKey": null,
  "groupId": "g.life",
  "status": "live",
  "tags": []
}
```

An `of-hit` template naming a substitution range, and its plant-frame override:

```json
{
  "id": "disptpl.p1-010",
  "nameKey": "tpl.affix.evasion",
  "name": "{sign}{value} chance to dodge",
  "runtimeFamily": "atom.evasion",
  "plantOverrideKey": "tpl.affix.evasion.plant",
  "plantOverrideName": "{sign}{value} chance to sway aside",
  "groupId": "g.evade",
  "status": "pending",
  "tags": []
}
```

| Field | Level | Req | Validated by |
|---|---|---|---|
| `id` | AUTHORED (`displayTemplates` namespace) | required | `naming.v1.json` |
| `nameKey`, `name` | AUTHORED | required | §6. `name` is the template string itself — placeholders only (`{value}`, `{sign}`, `{unit}`, `{element}`), never a literal number, per ssot-presentation.md §3.6 rule S1 |
| `runtimeFamily` | AUTHORED (reference) | required | resolves to a shipped/sibling `atom.*` family, one row per family (`family_id` is the template lookup key) |
| `plantOverrideKey` / `plantOverrideName` | AUTHORED | optional, both-or-neither | present only when the family names a body part, a hand action, or walking (ssot-presentation.md §3.5's rule); `null`/absent means "use `nameKey`" for both frames |
| `groupId` | VALIDATED | required | an affix group id from `naming.v1.json`'s `affixFamilies.groups` list (`g.life`, `g.attack`, …) |
| `status` | VALIDATED | required | `live` \| `pending` — `pending` only for a family whose channel semantics are unresolved (ssot-presentation.md §3.2.5) |
| `tags` | VALIDATED | optional | `tags.v1.json` |
| *(DERIVED/GENERATED, never authored)* | the rendered magnitude, the roll-quality bar, the sigmoid context read (`≈ +7.4 pp vs neutral`) | — | computed at render time from the frozen instance, never from this template |

**Rejects:** `name` containing a positional placeholder (`{0}`) instead of a named one
(`BadParamValue` — S1); `name` referencing `{element}` on a family with no element variant
(`UnknownParam`); a family with rows in the atom table and no `display-template` entry at all
(`MissingDisplayTemplate`); `plantOverrideKey` present with no `plantOverrideName` or vice versa; a
`groupId` not in `naming.v1.json`'s group list; markup (`<`, `[`, backtick, `**`) in `name` — forbidden
per seed-contract.md §6, same rule as any other localized string.

---

## 11. The authoritative kind → directory → namespace table

Nothing in seed-contract.md states this mapping; it lives today only in
`tools/ItemSeedValidator/Registries/KindCatalog.cs`, which exists precisely because nothing else does.
This is that table, sourced from the shipped code and cross-checked against every directory named in
the validator's own README. It covers all fifteen kinds the fleet plan schedules — fourteen active
partitions plus `attribute`, conditional and not yet authored.

| `kind` | Directory | `idNamespaces` key (`naming.v1.json`) | Stage | Shape source |
|---|---|---|---|---|
| `base-type` | `base-types/` | `baseTypes` | 1a | seed-contract.md §10 |
| `affix-family` | `affix-families/` | `affixFamilies` | 1a | seed-contract.md §10 |
| `gem` | `gems/` | `gems` | 1a | this document, §1 |
| `material` | `materials/` | `materials` | 1a | this document, §3 |
| `curve` | `curves/` | `curves` | 1a | this document, §5 |
| `attribute` | `attributes/` | `attributes` | 1a, **conditional** | not authored — gated on the five-or-none decision (ssot-requirements.md); no shape given here or elsewhere |
| `unique` | `uniques/` | `uniques` | 1b | seed-contract.md §10 |
| `set` | `sets/` | `sets` | 1b | seed-contract.md §10 |
| `charm` | `charms/` | `charms` | 1b | this document, §7 |
| `socket-word` | `socket-words/` | `socketWords` | 1b | this document, §2 |
| `recipe` | `recipes/` | `recipes` | 1b | this document, §4 |
| `enhancement-milestone` | `enhancement-milestones/` | `enhancementMilestones` | 1b | this document, §6 |
| `consumable` | `consumables/` | `consumables` | 1b | this document, §8 |
| `drop-table` | `drop-tables/` | `dropTables` | 1b | this document, §9 |
| `display-template` | `display-templates/` | `displayTemplates` | 1b | this document, §10 |

**Reading the table.** `kind` is the seed file's own `"kind"` value (seed-contract.md §9); directory
is where the file must live, checked against `kind` at import (`KindDirectoryMismatch` on a
mismatch); the `idNamespaces` key is the exact key `naming.v1.json`'s `idPolicy.idNamespaces` object
uses for that partition's allocation. All three must agree for every file — that three-way agreement
is `KindCatalog`'s entire job, and this table is now where a human (or the next registry) can read it
without opening the validator's source.

---

## 12. Summary of open findings

Collected here so a reviewer does not have to hunt through ten sections for them:

1. **§5 (`curve`)** — a curve file's numbers are the one deliberate, narrow exception to §3's
   magnitude ban, owned by a single reviewed partition, exempted in the validator by field name
   (`points[].multiplierPerMille`) and kind (`curve`) together, not by kind alone.
2. **§3 (`material`)** — no registry owns the 21 fixed material ids; `runtimeId` is
   AUTHORED-but-unvalidated until a `materials.v1.json` is minted from ssot-materials-crafting.md
   §3.1's list.
3. **§7 (`charm`)** — no registry owns the five-axis charm/resonance vocabulary
   (`offense`/`survivability`/`control`/`utility`/`economy`); `axis` is AUTHORED-but-unvalidated
   until one exists.
4. **§9 (`drop-table`)** — a drop table's own `min_ilvl`/`max_ilvl` band has no author-facing
   representation here; item level is argued to belong to the calling content
   (`loot_source.content_level`) rather than to the table, but that argument is not verified against
   how a `drop_table.min_ilvl` row is actually populated today.
5. **Cross-cutting** — `minSockets` (§2), `apCost` (§7), `manifestCost` (§8), and `outputQty` (§4) are
   all structural counts in the same sense `socketMax` already is, but none is on
   `OwnershipCheck.StructuralCountFields` today. One documented rule for "a small, closed,
   non-balance integer" would cover all four instead of four separate asks.
