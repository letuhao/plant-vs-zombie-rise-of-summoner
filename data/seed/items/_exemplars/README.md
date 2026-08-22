# Worked exemplars — the pattern, not the content

Four files, chosen to cover the variations that actually go wrong. Read the one that matches your
partition kind, then read [seed-contract.md](../../../../docs/architecture/item/seed-contract.md) and the
seven registries in [`../_registry/`](../_registry/). The exemplar shows you the shape; the registries own
every value you are allowed to put in it.

**Exemplar version: 1.** Record it as `_meta.exemplarVersion` in your own file.

## Which one to copy

| Your partition kind | Copy | It is the hard case for |
|---|---|---|
| base types (60 partitions) | `base-type.exemplar.json` | variation *within* one partition — four entries, not one |
| affix families (15) | `affix-family.exemplar.json` | one authored family becoming 35 generated atoms |
| uniques (20) | `unique.exemplar.json` | breaking a rule on purpose without breaking the machine |
| sets (5) | `set.exemplar.json` | referencing four registries at once and inventing nothing |

Gems, materials, curves, charms, socket words, recipes, consumables, drop tables and display templates
have no exemplar of their own yet. The closest match is the base-type file for anything with an identity
and a class, and the affix-family file for anything that generates variants.

## The three mistakes you are most likely to make

**1. Writing a number.** You may write a count, a reference, an enum or a band. Never a magnitude, a
weight, a probability or a quantity. `powerBand: "medium"` is not a polite way of saying 30 — it carries no
unit at all, which is the whole point, because the same band is hit points on one channel family and
resolver points on another. The validator rejects a numeric literal in any field that is not a structural
count (`socketMax`, `pieces`). **Fix shown in:** `affix-family.exemplar.json`, first entry — including why
the required `amount` param is absent while `channel` and `op` are present.

**2. Confusing the role id with the display word — or with the stale one.** `role` is the frame-neutral
id from `core.v1.json` (`head-guard`). The id template uses that role's frame display word verbatim
(`item.plant-crown-a-001`). And `ssot-item-categories.md` still shows a superseded 12-role naming
(`head-protective`, `manipulator-offense`) that is **wrong to copy** — its ⚠ banner says so. Take role ids
from the registry, always. **Fix shown in:** `base-type.exemplar.json`, entry 001.

**3. Inventing a value that belongs to a registry.** Every role, tag, theme, class rung, rarity rung, band
and pool word already exists. An unknown tag rejects; an unknown key rejects; a tag from an axis you have
already used rejects. Base types draw class-rung adjectives and role nouns — never theme adjectives,
concepts or seeds, because a base type has a class and no theme. Uniques and sets draw from their own
theme's pools and no other theme's. **Fix shown in:** `base-type.exemplar.json` entry 004 (one tag per
exclusive axis, and which pools are closed to you) and `set.exemplar.json` (four registry references,
zero invention).

## Standing rules

- **An exemplar is a pattern to follow, never content to copy verbatim.** Do not ship `Downy Blossom`,
  `atom.elpw-amplify`, `Guttering Coronet` or `Riveted Ironstem`. Their ids, names and `nameKey`s are real
  values in real namespaces — reusing one is a corpus-wide collision, and an id is never reused for
  anything, ever.
- **The long `notes` fields here are teaching text.** `notes` is never imported, so it costs nothing at
  runtime, but yours should be a sentence or two explaining a judgement call — not an essay.
- **`_meta` is provenance and is never imported.** Fill in all of it. Six weeks from now it is the only
  record of what your partition consumed, and without it a rerun is a guess.
- **Ids: the namespace is allocated, only the 3-digit sequence is yours.** Start at 001; 900-999 is
  reserved in every partition for later hand-authored corrections. Retire with `enabled: false` and keep
  the file — content is disabled, never deleted, and the sequence continues rather than restarting.
- **References resolve or the file rejects.** No forward references, no cycles, and no cross-partition
  references inside your own sub-wave. Wave 1a sees the frozen registries only; wave 1b additionally sees
  wave 1a's frozen output.
- **`overrides` requires a `note` on every entry that uses one.** None of these four needed one, which is
  the normal case.

## Two gaps these exemplars ran into

Both are contract questions, not author decisions. Flagged here so 124 agents hit them the same way.

1. **Only `narrow` counter-pressure is authorable without a number.** `drawback` is validated by finding a
   core atom with a negative magnitude, and `conditional` by finding a predicate tree — and the unique
   entry shape gives an author no numberless way to express either. Until that is resolved, a unique
   whose drawback is a real cost has nowhere legal to say so.
2. **`iconKey`, `flavorKey` and `flavor` appear only in the base-type shape.** The contract also says
   flavour and `iconKey` are author-now-or-lose-it for every content class, so uniques and sets look like
   they should carry them — but unknown keys reject, and §10 does not illustrate them there. These
   exemplars therefore omit them from the unique and the set. If the shapes are extended, this file and
   both exemplars move to version 2 together.

3. **Affix `nameWords` overlap the flavour pools, and nothing checks it.** The element flavour words the
   contract itself uses — Ember, Frost, Gale, Stone, Radiant, Umbral — include four that are already
   canonical ids in `words.v1.json` (`ember` is an ember-harvest concept, `stone` an armour/plant/heartwood
   adjective, `radiant` and `umbral` are two themes' adjectives). No machine rule breaks: affix display
   words are a separate shipped table, out of `words.v1.json`'s scope, and the collision normalizer runs
   over identity names only. But the two vocabularies were built independently and were never diffed, so
   an affix agent has no way to tell a safe word from one another partition owns.

Related: the contract's illustrative `_meta.registryVersions` names `roles` and `rarity`, which are not
files — both live inside `core.v1.json`. These exemplars record the seven registry files that actually
exist, because the point of the block is to say what was consumed.
