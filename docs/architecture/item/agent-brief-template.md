# Wave-1 agent brief — the template

Every wave-1 authoring agent receives an instantiation of this, and **nothing else**. Placeholders in
`{{BRACES}}` are filled per partition from `naming.v1.json`.

The template exists because 126 hand-composed briefs drift, and a drifting brief is indistinguishable
from a drifting contract until the corpus is finished.

---

## Current registry versions — record these in `_meta.registryVersions`

As of the pilot passing (2026-08-22):

```
core 1 · bands 1 · themes 1 · tags 3 · classes 2 · naming 4 · words 4
```

A registry may move during the run. That is survivable now: each declares a
`minCompatibleVersion`, so an **additive** bump leaves your file valid and only warns, while a
**breaking** one fails and the partition re-runs. Record what you actually read.

## The template

```text
You author ONE partition of game content. Repo: d:\Works\source\plant-vs-zombie-rise-of-summoner

Write EXACTLY ONE file: {{OUTPUT_PATH}}
Touch no other file. Never edit a registry, an exemplar, the contract, or another partition.
No git write commands. No web search.

## Read, in this order

1. docs/architecture/item/seed-contract.md — the contract. Sections 2, 3, 4, 5, 6 and 9 bind you.
2. data/seed/items/_exemplars/{{EXEMPLAR}} — the pattern. Follow its shape exactly.
3. data/seed/items/_registry/ — core, bands, tags, themes, classes, naming, words.
   Every value you write comes from these. You may NAME a registry value; you may never INVENT one.

## Your partition

  partition id : {{PARTITION_ID}}      <- put this EXACT string in _meta.partition
                 It is the canonical id the validator derives from your id namespace:
                 the kind's directory, a forward slash, then the partition key from
                 naming.v1.json — e.g. `gems/1`, not `gems.g1`. If the brief and the
                 registry disagree, the registry wins and you report BLOCKED.
  id namespace : {{ID_PREFIX}}-{{seq}}   sequence 001-899, zero-padded, yours alone
  authoring    : {{ENTRY_COUNT}} entries of kind {{KIND}}
  {{PARTITION_SPECIFICS}}
  word pools   : {{POOL_KEYS}}   — draw names only from these

## The five rules that get broken most

1. NO NUMBERS. You write bands — powerBand, costBand, dropBand, variance — never magnitudes,
   weights, probabilities or quantities. A generator resolves them. Counts that describe
   structure (socketMax, pieces) are the only numerals you may type.
2. NO DERIVED FIELDS. Never write affixClass, atom_id, container_id, tier magnitudes, pool
   weights, or a power vector. The validator rejects the file if you do.
3. IDS STAY IN YOUR NAMESPACE. Your prefix, your sequence, three digits. 900-999 is reserved.
4. NAMES COME FROM YOUR POOLS, built with a pattern from naming.v1.json. Do not invent words
   and do not reach into another partition's pool.
5. TAGS COME FROM tags.v1.json, one per exclusive axis. An unknown tag rejects the file.

   ⚠ **`powerCategories` are NOT tags.** `core.v1.json` carries a five-value power vector —
   offense · survivability · control · utility · economy — used for charm axes and socket
   resonances. The `combat-posture` tag axis is a different, three-value list:
   **offensive · defensive · utility**. The two overlap on the word *utility*, which is exactly
   why agents keep reaching across: `offense` and `economy` have both been written as tags and
   rejected. If the word you want is not literally in `tags.v1.json`, it is not a tag.

   ⚠ **Exclusive means one.** An axis marked exclusive carries at most one tag per entry.
   `sturdy` and `fragile` are the same axis and cannot both apply; neither can `defensive`
   and `utility`. If an entry genuinely feels like both, pick the one a formula should read.

## Scope decisions already made — do not re-derive them

- Content ships at Diablo-2 scale. Sockets, gems, charms and consumables are ALL authored.
  If a design document tells you a category ships at zero, that document predates the decision.
- Primary attributes are deferred. Do not author or reference them.
- The 15 roles in core.v1.json are binding. Older documents use a 12-role set; ignore it.
- The design lane documents are a snapshot. Where one disagrees with a registry, the registry wins.

## Quality

Vary your entries. {{ENTRY_COUNT}} items that differ only in adjective are a failed partition,
even if every one validates. Your pool is large enough to avoid that — use it.

**Specifically, and this is the failure the first pilot actually produced:** do not let your
adjectives cluster on one idea. A partition whose twelve names read Dense, Petrified, Ancient,
Obdurate, Stony, Rough, Gnarled, Cracked, Coarse has used twelve words to say "hard and old"
once. Before you finish, read your twelve adjectives as a list. If you can summarise them in a
single phrase, replace half of them from a different part of the pool — age, origin, condition,
colour, provenance, use, damage, growth stage are all available axes and only one of them is
weight.

The nouns in that pilot were excellent and the adjectives were not, which is the shape this
failure takes: the pool saves you on the head-noun and cannot save you on the modifier.

**Your band sibling shares your noun pool. Read its file before you write.** A role-frame pair is
split into band a and band b, and both draw from the *same* `nounPools` entry — the pools are
disjoint across roles, not across bands. Two agents who could not see each other have already
produced the identical name "Lobbed Gourd" this way, from `gourd` plus a lobbing adjective.

So: open your sibling's file if it exists, list the nouns it used, and take yours from what it
left. Where you must reuse a noun because the pool is small, make the modifier carry a clearly
different idea. The validator normalizes names to their canonical word set before comparing, so
"Lobbed Gourd" and "Gourd of Lobbing" collide too — rearranging will not help.

Write flavour that belongs to this world: plants, zombies and their fusions. Horticulture,
decay, graft, bloom, rot, harvest, hunger. Generic fantasy is a failure of the task.

## Your final message

Exactly one line, nothing else:

    OK {{PARTITION_ID}} <entries-written> {{OUTPUT_PATH}}

If something genuinely blocks you — a registry value you need does not exist, or the contract
and the exemplar disagree — do not improvise. Write nothing and reply:

    BLOCKED {{PARTITION_ID}} <one sentence>

A blocked partition is cheap. A partition that guessed is expensive, because the guess is
invisible until 125 other files have been written against a different assumption.
```

---

## Why the final-message rule is written that way

At 126 agents, a paragraph of summary each is ~80 000 tokens of orchestrator context spent on reports
nobody reads. The file on disk is the deliverable; the validator is the report. So the agent returns a
token.

`BLOCKED` matters more than `OK`. An agent that improvises past a contract gap produces a file that
validates and is wrong, and the gap stays hidden until the next 125 agents have each improvised past it
differently. Making the blocked path cheap and explicit is what surfaces contract defects while they
still cost one agent.

## Instantiation notes

- `{{EXEMPLAR}}` — base-type partitions get `base-type.exemplar.json`; affix groups get
  `affix-family.exemplar.json`; uniques get `unique.exemplar.json`; sets get `set.exemplar.json`.
  Gem, charm, consumable, recipe, drop-table and display partitions take the nearest structural match
  and are told which it is.
- `{{PARTITION_SPECIFICS}}` — the role, frame, class band, theme or affix group, verbatim from the
  registry so the agent never has to look it up and never has to interpret it.
- `{{POOL_KEYS}}` — the exact `words.v1.json` keys the partition may draw from. Naming the keys rather
  than the words keeps the brief short and the pool authoritative.
- Model per the fleet plan's table: consumers of vocabulary get Haiku 4.5, inventors get Sonnet.
