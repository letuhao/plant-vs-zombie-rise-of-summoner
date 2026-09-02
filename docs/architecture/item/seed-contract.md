# The item seed contract — what gets authored, and what gets computed

**Status:** Proposed 2026-08-22, revised the same day after a fan-out safety audit. **Contract for a
data build, authoring authorized as of 2026-09-01** (`seed-to-concrete` T0.6/Phase 1 — `demon-seed`
and `effect-pipeline` author against this contract starting Phase 1). The prior wording — *"Nothing is
authorized to be authored from it yet"* — was found by the `seed-to-concrete` plan audit to contradict
its own dependents: Phases 1-2 of that plan author seeds against this contract, which the old line
forbade outright. Everything else this document decided stands; only the authoring gate changes.

**Purpose:** define the JSON that human and agent authors write, so that a large parallel authoring
effort produces content that imports cleanly, hashes stably, and does not have to be re-authored when a
later mechanism (price, weight, durability, crafting) arrives.

**Extends, does not replace:** [../effect-atom/spec-authoring-and-validation.md](../effect-atom/spec-authoring-and-validation.md)
(E14a) already owns the seed file format, `tools/AtomImporter`, all-or-nothing import, and the content
lints. Item seed files are **more of the same corpus under a new subtree**, validated by the same
importer. Minting a second format would fork the content hash and the import transaction. **The schema,
registries and validator described here live where the importer reads them** — not in a parallel
authoring tree that would drift from it.

Design source: the seventeen lane documents indexed at [README.md](README.md). Fleet: [authoring-fleet-plan.md](authoring-fleet-plan.md).

---

## 1. The one law

> **The seed is generator input. It is not rows.**

An author writes judgement. A generator expands it into rows. The importer validates and upserts.

```text
data/seed/items/**.json   →   generators   →   data/generated/items/**.json   →   AtomImporter   →   SQLite
   (authored, reviewed)         (formulas)      (checked in, reviewed, diffed)     (validate+upsert)
```

Three consequences:

1. **Anything a formula can compute is not authored.** Otherwise two sources of truth exist and diverge.
2. **Generated output is checked in.** The precedent is `tools/DemonCatalogGen`, whose emitted catalog is
   committed and reviewable. A generated row nobody can diff is a row nobody can review.
3. **Adding a computed field later costs zero authored files.** A new formula reads existing authored
   inputs and emits a new column. Every seed file on disk stays valid and untouched. §8 is the list.

---

## 2. Field ownership — four levels, not two

Every field in this contract carries exactly one ownership level. **A field with no declared level is a
contract defect, not an author's judgement call** — that ambiguity is precisely how one agent decides
role legality is obvious and includes it while another assumes it is derived.

| Level | Who sets it | Where the value lives |
|---|---|---|
| **AUTHORED** | the agent chooses it | the seed file |
| **DERIVED** | the importer computes it from authored fields | a column, never the seed |
| **GENERATED** | a generator emits whole rows from authored input | `data/generated/`, checked in |
| **VALIDATED** | the author names it, a frozen registry owns it | the seed file, checked against the registry |

`VALIDATED` is the level the audit was right to separate out: an author writes `role: "core-protective"`,
but does not get to invent roles. Naming a value and owning a value are different rights.

### 2.1 The ownership matrix

| Field / concept | Level | Note |
|---|---|---|
| `id` | AUTHORED (within an allocated namespace — §4) | agent owns the sequence, never the prefix |
| `nameKey`, `name`, `flavor` | AUTHORED | §6 |
| `iconKey` | AUTHORED | stable key; no art pipeline needed |
| `tags` | VALIDATED | closed registry; unknown tag rejects |
| `role`, `frame`, `class`, `rarity`, `theme`, `element` | VALIDATED | frozen wave-0 registries |
| `kindId`, `channel` | VALIDATED | the atom layer's closed vocabulary |
| `powerBand`, `costBand`, `dropBand`, `variance` | AUTHORED | **bands, never numbers** — §3 |
| structural counts (`socketMax`, `pieces`, `pool_rolls`) | AUTHORED | a count is structure, not balance |
| every **magnitude**, **weight**, **probability**, **quantity** | DERIVED | §3. An author may never type one |
| `affixClass` (prefix/suffix) | DERIVED from `kindId` | permanent-modifier kinds are prefixes; triggered kinds are suffixes. **Present in a seed file → reject.** **Added 2026-09-01 (T0.6):** a **mixed bundle** — an affix whose atom refs span both classes (e.g. `master of fire and ice`'s power+defense atoms) — derives `affixClass` **per atom**, never once for the whole bundle, and at roll time it **consumes one prefix roll and one suffix roll simultaneously** rather than doubling either count (`spec-container-schema.md`'s validation table). Still never authored — the rule only widens from "one kindId → one class" to "N atom refs → N per-atom classes, still all derived" |
| role × family legality | DERIVED | from each family's declared role groups — worth ~1 100 cells |
| `atom_id`, `container_id` | DERIVED | computed from columns and validated (`IdMismatch`) |
| tier magnitudes, band min/max | GENERATED | from `powerBand` × the channel family's curve |
| element variant explosion | GENERATED | families × 7 slots |
| pool rows and weights | GENERATED | from the role×group matrix |
| socket resonances | GENERATED | all 25 are rule-derived |
| ilvl-band container copies | GENERATED | one authored identity → N containers |
| power vector | DERIVED | the power model owns it |
| price · weight · durability · salvage yield | DERIVED | §8 — none exist yet, all arrive free |

---

## 3. The numeric rule — authors write bands, never numbers

> **An author may write a count, a reference, an enum, or a band. Never a magnitude, never a weight,
> never a probability, never a quantity.**

This is the rule that stops seventy-nine independent agents inventing seventy-nine balance systems. Each
file would be individually valid and the corpus collectively incoherent, and no schema validator would
catch it, because `damage: 47` is as well-formed as `damage: 31`.

| Instead of | Author writes | Who resolves it |
|---|---|---|
| `t1: 10, ratio: 1750` | `powerBand: "medium"` | the curve table, per channel family |
| `damage: 47` | `powerBand: "high"` + `class` + `role` | the base-stat generator |
| `weight: 250` (drop) | `dropBand: "uncommon"` | the drop-weight curve |
| `shards: 12` | `costBand: "steep"` | the cost curve |
| `spreadMilli: 150` | `variance: "narrow"` | the variance table |

**Bands are a closed enum per axis**, frozen in wave 0. The validator rejects any numeric literal in a
field not on the structural-count allowlist. That check is mechanical and it is the corpus's main defence
against balance drift.

It also closes the units trap by construction: a band carries no unit, so an author physically cannot
copy a tier band from `crit.rate` (resolver points) onto `combat.power` (flat game units) — the channel
family decides the unit downstream. See [atom-layer-handoff.md](atom-layer-handoff.md) §1, where six of
twelve derived families turned out to be the opposite of what the SSOT claimed.

---

## 4. Ids — the namespace is allocated, the sequence is the agent's

Global uniqueness is a **safety net, not the mechanism.** Collision must be structurally impossible, the
same way one-file-per-agent makes merge conflicts structurally impossible.

- **Wave 0 allocates a namespace prefix per partition.** No two partitions share one.
- **The agent owns only the sequence within its prefix**, zero-padded to three digits.
- Global uniqueness validation then catches operational mistakes rather than doing the work.

```text
item.plant-crown-a-007      base type, plant frame, crown role, band A, seventh
atom.searing-strike         affix family (the family id is the namespace)
unique.ember-014            unique, ember theme, fourteenth
```

⚠ **`container_id` allows no dot in its body** — the grammar is
`^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`. Separate segments with hyphens,
never dots. Two lanes discovered this the hard way.

**Ids are never reused.** Not after a deletion, not after a rejected review, not ever — a reused id
silently repoints every reference that already resolved to it. See §7.

---

## 5. Naming — the grammar, and collision normalization

Reserved word pools stop *thematic* collision. They do not stop **Ashen Fang / Ash Fang / Fang of Ash /
Ashfang**, which are four names and one idea.

**Collision normalization**, applied by the validator before comparing:

1. lowercase
2. strip punctuation and whitespace
3. drop articles and connectives (`of`, `the`, `a`, `and`)
4. sort the remaining tokens
5. compare

All four examples above normalize to `ash fang` and the last three reject. This is deterministic and
belongs in the validator, not in a reviewer's judgement — reviewers should be spending attention on
whether a theme *reads* as that theme, not on spotting anagrams.

**The construction grammar is frozen in wave 0** and closed: which patterns are legal
(`<Adjective> <Base>`, `<Base> of <Concept>`, …), whether plurals are allowed, whether an agent may
invent connective words. An author picks a pattern; it does not invent one.

---

## 6. Locale

Every entry carries a `nameKey` **and** its string, in the same file, authored together. A separate
translation pass would need every partition finished first and would ask an agent to name things it
never designed.

| Rule | |
|---|---|
| `nameKey` uniqueness | **global**, not per category |
| Default locale | one ships; keys exist from row one so a second is a file drop, not a re-author |
| Allowed characters | the key is `^[a-z0-9.-]+$`; the string is free text |
| Markup in strings | **forbidden** — display formatting belongs to the presentation layer |
| Flavour text | localized, same rule |
| Strings after extraction | immutable under a given key; a changed string is a new key |

The tree currently carries bare English *and* Chinese literals with no indirection. Keys from row one is
the cheapest line in this contract and the most expensive thing to retrofit across thousands of rows.

---

## 7. References, deletion, and rerun

### 7.1 What a seed file may reference

**Wave 1a authors may reference frozen wave-0 registries only.** Wave 1b authors may additionally
reference **wave 1a's frozen output** — uniques name a base type, drop tables name both. See the fleet
plan's staged wave 1.

- Forward references: **forbidden**. A reference resolves at validation or the file rejects.
- Cyclic references: **forbidden**.
- Cross-partition references within the same sub-wave: **forbidden**. That restriction is what makes
  "independent" true rather than aspirational.

### 7.2 Deletion and correction

| Situation | Action |
|---|---|
| Entry is wrong, same identity | edit in place, **same id** |
| Entry should not exist | `enabled: false`, **file kept, id retired forever** |
| Entry is a different thing now | **new id**; the old one is retired |
| Reviewer rejects a whole file | the partition is re-run; **the id sequence continues, it does not restart** |

Content is disabled, never deleted — the same rule the atom layer already enforces for rows.

### 7.3 Rerun semantics

A batch is re-runnable. What that guarantees, precisely:

- A rerun **replaces only files owned by that batch**, never another batch's.
- It **never reclaims another partition's vocabulary** and **never reuses retired ids**.
- The sequence continues from the high-water mark; it does not restart.

**It does not guarantee identical output.** Model output is not reproducible, and writing that guarantee
down would commit us to something we would quietly break. Provenance (§9) is what makes a rerun
*auditable*; determinism is not on offer and should not be claimed. *(The same distinction the mutation
contract reached: reconstruct from record, do not promise replay.)*

---

## 8. What is deliberately absent, and how it arrives later

Each of these can be added **without touching one authored file**, because its inputs are already
authored.

| Later mechanism | Formula reads | Present? |
|---|---|---|
| Sell / buy price | rarity ordinal · ilvl · affix count · `class` | ✅ |
| Weight / encumbrance | `class` · role · frame · `tags` | ✅ |
| Durability / repair | `class` · rarity · `tags` | ✅ |
| Craft cost | `costBand` · rarity band · ilvl · curve | ✅ |
| Salvage yield | rarity band · ilvl · enhancement level | ✅ |
| Power vector | kind · params · magnitude · conditionality | ✅ |
| Vendor stock · loot filters · market value | `class` · `tags` · derived price | ✅ |

**`tags` is what makes this work** — semantic intent recorded before a mechanism exists to read it.
Writing `"heavy"` today costs nothing; recovering *which of 750 base types should be heavy* from a
formula later is impossible.

### Not safe to defer — author now or lose it

A formula can never recover judgement: names, flavour, theme; tags; **thematic fit** (which element,
frame and role an affix belongs to — the actual design work, with everything numeric downstream of it);
unique and set identity; `iconKey`; and `class`, which is the input half the later formulas read.

---

## 9. The common envelope

```json
{
  "schemaVersion": 1,
  "kind": "base-type",
  "_meta": {
    "batch": "base-plant-crown-a",
    "partition": "plant/crown/a",
    "contractVersion": 1,
    "registryVersions": { "tags": 1, "themes": 1, "roles": 1, "rarity": 1, "naming": 1 },
    "exemplarVersion": 1,
    "promptVersion": 1,
    "model": "claude-haiku-4-5-20251001",
    "authoredUtc": "2026-08-22T00:00:00Z",
    "sourceRef": "ssot-item-categories.md#5.3"
  },
  "entries": [ /* … */ ]
}
```

`_meta` is **never imported** — authoring provenance only. It records everything needed to regenerate a
partition six weeks later knowing exactly what it consumed: contract version, every registry version,
the exemplar, the prompt, and the model. Without those, a rerun is a guess.

Per entry: `id` · `nameKey` · `name` · `tags` · `notes` (never imported) · `enabled` · `overrides`
(each requiring a `note`).

**`iconKey`, `flavorKey` and `flavor` are available on every entry kind**, not only base types. §8 lists
flavour among the things a formula can never recover, so anything a player can see by name can carry
one. They are optional per entry and expected on anything player-facing.

**Markup rules apply to display strings only.** `name` and `flavor` are localized output and reject
markup. `notes` and `identity` are authoring fields the importer never reads — markup there is a
warning, not an error, because a backtick around a field name in a review note is legitimate.

**Unknown keys reject.** Forward compatibility comes from `schemaVersion`, not lenient parsing.

---

## 10. Entry shapes

Illustrative. Note that **no example below contains a magnitude** — that is the point of §3.

> **The four shapes below are not the whole set.** Ten further kinds — `gem`, `socket-word`, `material`,
> `recipe`, `curve`, `enhancement-milestone`, `charm`, `consumable`, `drop-table`, `display-template` —
> are specified in [entry-shapes.md](entry-shapes.md), together with the authoritative
> `kind` ↔ directory ↔ `idNamespaces` mapping. That gap was found by the validator, which refused to
> invent schemas for kinds the contract had not defined and emitted `UnknownKeyShapeUndefined` instead.
> **Both documents bind equally**; they are split only because this one was written before the partition
> list was final.

### Affix family

```json
{
  "id": "atom.elemental-power",
  "nameKey": "affix.elemental-power",
  "name": "Elemental Power",
  "kindId": "stat.derived",
  "params": { "channel": "combat.power.{variant}" },
  "variants": { "generate": "elements+omni" },
  "frames": ["humanoid", "plant"],
  "side": "both",
  "roles": ["armament-primary", "jewel-major", "manipulator"],
  "powerBand": "medium",
  "nameWords": { "prefix": ["Ember", "Frost", "Gale", "Stone", "Radiant", "Umbral"] },
  "displayTemplate": "+{value} {element} power",
  "tags": ["offense", "elemental"]
}
```

No `affixClass` — derived from `kindId`, and rejected if present. No tier magnitudes — generated from
`powerBand` and the channel family.

### Base type

```json
{
  "id": "item.plant-crown-a-007",
  "nameKey": "base.heartbloom-crown",
  "name": "Heartbloom Crown",
  "frame": "plant",
  "role": "head-protective",
  "class": "heartwood",
  "band": "a",
  "implicit": { "family": "atom.regeneration", "powerBand": "low" },
  "socketMax": 2,
  "iconKey": "icon.base.heartbloom-crown",
  "flavorKey": "flavor.base.heartbloom-crown",
  "flavor": "It blooms once, at the end.",
  "tags": ["organic", "light", "rooted"]
}
```

### Unique

```json
{
  "id": "unique.ember-014",
  "nameKey": "unique.thornmantle",
  "name": "Thornmantle",
  "frame": "plant",
  "baseType": "item.plant-core-a-003",
  "rarity": "heirloom",
  "fixedAtoms": [
    { "family": "atom.warded", "powerBand": "high", "params": { "element": "earth" } },
    { "family": "atom.retribution", "powerBand": "medium" }
  ],
  "varianceSlot": { "family": "atom.vitality", "variance": "narrow" },
  "counterPressure": { "kind": "conditional", "note": "only below half health" },
  "tags": ["defensive", "signature"]
}
```

`counterPressure` is validated, not decorative — a unique with no drawback, condition or narrowness
fails import. That is what stops the class becoming a strictly-better tier.

### Set

```json
{
  "id": "set.ember-legion",
  "nameKey": "set.ember-legion",
  "themeKey": "theme.ember",
  "members": [
    { "role": "head-protective" }, { "role": "core-protective" },
    { "role": "manipulator" }, { "role": "footing" }
  ],
  "thresholds": [
    { "pieces": 2, "capability": { "family": "atom.searing-strike", "powerBand": "low" } },
    { "pieces": 3, "atoms": [{ "family": "atom.elemental-power", "variant": "fire", "powerBand": "medium" }] },
    { "pieces": 4, "atoms": [{ "family": "atom.ferocity", "powerBand": "high" }] }
  ]
}
```

The **capability sits at the lowest threshold** — inverting genre convention so a two-piece splash is
always worth taking. A set whose capability is not at its lowest threshold **fails import**: the
anti-jail rule is enforced, not advised.

---

## 11. Gaps and open questions

| # | Item | State |
|---|---|---|
| 1 | `sfxKey` | Deferred — the VFX program owns cue vocabulary and should be asked before keys are minted |
| 2 | `unlockGate` | Nullable hook now, so a progression gate later is not a re-author |
| 3 | Content-hash version | Two versions are reserved by other programs; the item corpus needs an allocation across three |
| 4 | Shared `catalog_revision` with the atom corpus? | One transaction is simpler; separate lets item content ship without re-hashing effect content |
| 5 | Display templates inline or in a parallel tree? | Inline keeps one family in one file; parallel lets a translator work without touching mechanics |

**Closed by this revision:** `affixClass` is derived and rejected if authored; the tag vocabulary is a
wave-0 registry with a named owner; locale strings are authored alongside their keys.

---

## 12. Before any of this is authored

- **The freeze checklist** in [authoring-fleet-plan.md](authoring-fleet-plan.md) §8 must be complete. It
  is the gate this contract exists to make possible.
- **The validator must exist and run**, because the pilot is a test *of this contract*, and a contract
  test with no mechanical check is an opinion.
- **No generator exists.** Seven are implied; the pool generator must exist before the first base type is
  authored, or a container cannot pass import.
- **The importer does not exist either** — E14a is unbuilt, so nothing today can read a seed file.

None of that blocks agreeing the contract, which is exactly why agreeing it first is the right order.
