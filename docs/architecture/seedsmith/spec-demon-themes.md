# Spec: `demon-themes`

Module `demon-themes` in the [seedsmith map](../seedsmith-map.md) §3b. Wave **D4**.
Depends on `motif-derive`. **Gated by `demon-metrics` (D3).**

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md); `A#` = its §6 audit.

**Status: proposed 2026-08-31, awaiting owner review. Not authorized to build.**

---

## 1. Objective

Make a demon usable as a **theme** by the items and action corpora — which is how the owner's
"item specific for each demon" and "action specific for each demon" actually get built.

**Done means:** the items corpus can author a set or unique themed to a demon, using a legal
`themeKey` that carries that demon's motifs and expression rules — with no cross-corpus write and no
change to the items adapter's own rules.

---

## 2. Design

### 2.1 Why items are not a demons kind — audit A3

The obvious design puts an `item` kind in the demons adapter. **It does not survive contact with the
code.** `Corpus.load(root)` is single-root: one corpus, one directory. Items live in
`data/seed/items/`. So a demon "item" would be either:

- a *different thing* from a real item — unequippable, outside the item corpus's role/frame/affix
  rules, and cosmetic; or
- a write into another adapter's corpus — which falsifies the clean adapter story the whole feature
  rests on.

**The way out was already in the data.** The item corpus has a theme axis: `unique` carries optional
`theme`/`themeKey`, and **`set` *requires* `themeKey`**
([`adapters/items/kinds.py:60,63`](../../tools/seedsmith/seedsmith/adapters/items/kinds.py)).
Measured in the live corpus: **31 sets and 8 uniques already carry a theme.**

**So a demon is a theme.** Items stay in the items corpus and reference the demon. Strictly better
than the original framing: no cross-corpus write, the single-root constraint respected rather than
fought, and a demon's signature gear is naturally a **set** — already theme-required, already
carrying members and thresholds, with 31 existing instances to pattern-match against.

### 2.2a ⛔ Two theme populations, split by prefix — audit S5

**The live corpus already has an authored theme vocabulary**, and a first draft of this spec would
have broken it. Measured: **5 distinct `themeKey` values across 31 sets** —
`theme.frostbitten-vanguard`, `theme.rusted-legion`, `theme.sunwoven-almanac`,
`theme.thorned-chassis`, `theme.verdant-graft`. None is a demon. They are deliberate, roughly six
sets each.

That draft asserted both *"the adapter rejects a `themeKey` absent from the registry"* and *"existing
themed content still validates"*. **Those cannot both hold** if the registry is demon-published only.

**The resolution the existing id grammar already implies:** legacy themes are `theme.*`; demon themes
are **`demon.*`**. The `themeKey` vocabulary is a **union of two append-only populations that cannot
collide**, each with its own provenance and rules:

| Population | Prefix | Authored by | Rules |
|---|---|---|---|
| Item themes | `theme.` | humans, pre-demons | append-only; untouched by this feature |
| Demon themes | `demon.` | this module | append-only; carries motifs, expression rules, `basis` |

Coexistence stops being a migration deferred and becomes a namespace split — which is what the
prefix was already saying. Nothing needs migrating, and both populations validate.

### 2.2 The bridge is a registry, and it goes one way

This module emits a **theme registry** that the items adapter reads as a legal `themeKey` vocabulary:

```
data/seed/demons/_registry/themes.v1.json
  themeKey -> { speciesId, displayName, motifs[], antiMotifs[], expression{}, basis }
```

**The direction is one-way and it matters.** Demons *publish* themes; items *consume* them. Nothing
in the demons corpus reads an item, and nothing in the items corpus writes a demon. Two adapters,
one shared vocabulary file, no coupling in either direction beyond that.

This is the same shape `RegistrySet.vocabularies` already has — a closed set one adapter declares and
`is_legal()` validates against. The items adapter gains `themeKey` as a registry-backed vocabulary
instead of the free-text field it is today.

### 2.3 Expression rules travel with the theme

A theme carries not just motifs but **how they are expressed for the consuming kind** —
`adapter-demons` §2.7's rules, narrowed to items and actions:

| Consuming kind | A motif is expressed as |
|---|---|
| `unique` / `set` (items) | material and form — what it is made of, what shape it takes |
| action | tempo and effect shape — how fast, how it lands |

Without this the theme is a word list, and audit A1's failure arrives in the item corpus instead of
the demon one: *Shell Blade*, *Shell Plate*, *Shell Ward* — every check passing, the corpus
unreadable. The expression rule is what makes a shared motif produce a *facet* rather than a
*synonym*.

### 2.4 `basis` travels too, and gates what may be themed

A theme carries the `basis` of the motifs behind it. A demon whose motifs are `basis = "blocked"`
**publishes no theme** — there is nothing to theme *with*, and a theme built on nothing produces
items that merely assert a connection.

A demon with `basis = "name"` publishes a theme **marked as such**. Whether such a theme may be used
for generation is the owner's call (Q6b treats `basis = name` as work remaining), but the marking is
not optional: an item generated from a name-only theme should be identifiable later, because
`lore-enrich` will improve its source and the item will want regenerating.

### 2.4a ⛔ Roster churn — a published theme is retired, never deleted (audit S6)

`DemonSpeciesGenerator` selects the top **24 species by observed HP** and assigns rarity by rank. A
future capture with better `spawn_stats` coverage — which `almanac-spawn-coverage` exists to produce
— **can change which species are selected**. A demon can leave the roster.

This is the one failure in the feature that corrupts silently rather than failing loudly: items
themed to a departed demon would resolve to nothing, and no test would notice, because the items are
still valid items.

**The contract:** the theme registry is append-only **and a departed demon's theme is retired, not
removed** — marked `retired: true`, still resolvable, no longer offered for new generation. Same
discipline already applied to family and motif ids, extended to the one artifact that crosses a
corpus boundary.

### 2.5 Gated by D3, deliberately

`demon-metrics` gates this module. Generating themed content from a family/motif graph that has not
been checked for tautology (A2) means items inherit a structure that looks real and is not — and by
then the wrong structure is in the *item* corpus too, which is a much more expensive place to find
it.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_demon_themes.py -q
python -m seedsmith demons themes            # emit the theme registry
python -m seedsmith report --adapter items   # items now validate themeKey against it
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/
    themes.py        → build the theme registry from motif assignments
tools/seedsmith/seedsmith/adapters/items/
    registries.py    → EDIT: themeKey becomes a registry-backed vocabulary
tools/seedsmith/tests/test_demon_themes.py
data/seed/demons/_registry/themes.v1.json    → emitted, committed, append-only
```

**One file outside `adapters/demons/` changes** — the items registry gains a vocabulary. That is the
single exception to `adapter-demons`'s "no file outside the adapter" rule, and it is a *vocabulary*
addition rather than a concept leak: the items adapter learns that `themeKey` has legal values, not
what a demon is.

### 5. Code style

Pure build function over motif assignments; committed output; sorted keys. The items-side change is
one vocabulary registration, following the existing `RegistrySet` pattern exactly.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| A demon with motifs | publishes a theme carrying motifs, anti-motifs and expression rules |
| A demon with `basis = "blocked"` | **publishes no theme** |
| A demon with `basis = "name"` | publishes a theme **marked** `basis = "name"` |
| The items adapter | accepts a `themeKey` present in the registry |
| The items adapter | **rejects** a `themeKey` in neither population — enforced, not decorative |
| A legacy `theme.*` key | **validates** — the two populations coexist (§2.2a) |
| A demon theme id | always `demon.*`-prefixed; a collision with `theme.*` is impossible by construction |
| Existing themed content (31 sets, 8 uniques) | **still validates** — a test asserts no existing entry breaks |
| Theme registry | append-only; a re-run with a new demon leaves existing keys untouched |
| A demon that leaves the roster | its theme is **retired, still resolvable** — never deleted (§2.4a) |
| An item themed to a retired demon | still validates |
| Direction | nothing in `adapters/demons/` reads the items corpus — asserted structurally |
| Expression rules | present for every published theme; a theme without them fails validation |
| A brief built from a theme | contains the motifs inline and no citation-shaped text |

The "existing themed content still validates" row is the one that decides whether this ships: 39
entries already carry themes that were free text, and turning a free-text field into a closed
vocabulary is exactly how a migration breaks a corpus quietly.

---

## 7. Boundaries

- **Always:** publish one-way (demons → themes → items); carry expression rules and `basis`; keep the
  registry append-only; leave existing themed entries valid.
- **Ask first:** using a `basis = "name"` theme for generation; any second file outside
  `adapters/demons/`; making `themeKey` required on `unique` (it is optional today).
- **Never:** write into the items corpus from the demons feature; read an item from the demons
  adapter; publish a theme for a `blocked` demon; break an existing themed entry.

---

## 8. Success criteria

1. Items can be authored themed to a demon, validated against the registry.
2. All 39 existing themed entries still validate.
3. `blocked` demons publish nothing; `name`-based themes are marked.
4. Expression rules travel with every theme.
5. Exactly one file outside `adapters/demons/` changes, and it adds a vocabulary rather than a
   concept.
6. Full seedsmith suite green.

---

## 9. Open questions

1. ~~Do the existing themes become demon themes, or coexist?~~ **ANSWERED by audit S5**: they
   coexist, split by prefix (§2.2a). The split is principled rather than deferred, because the id
   grammar was already carrying it.
2. **Does the action corpus need the same bridge, or a different one?** Actions are specced but their
   corpus shape is not settled here; this spec assumes the same registry serves both and does not
   prove it.
