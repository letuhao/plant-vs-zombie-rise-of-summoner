# Spec: corpus-loader (A-C1)

**Module id:** `corpus-loader` · **Program:** [action-corpus](../action-corpus-map.md) §4.1 · **Build order:** 1 of 7 model-free
**Status: proposed 2026-09-03.** Written against the capability map; no build authorized until the map is approved.
**Model calls: none, ever.** This module parses files and registers vocabularies.

It owns one thing: making `data/seed/actions/` a corpus seedsmith can read. Today both files there are
invisible to `Corpus.load` because neither carries the `kind` + `entries` envelope, so every metric, every
round-trip test and every coverage report over generated actions is unwritable. This module defines the
envelope generated action seeds are written in, registers an `actions` adapter, and — the part that is
easy to get wrong — leaves the two shipped runtime-config files exactly as they are.

## The four constraints this module is bound by (map §3, restated inline)

1. **Seeds, not a cartesian.** An atom names a **pool**; element, tier and cell resolve at layer 4, per
   player, at roll time. A generated action's atoms are pool references. **A cell is a target, never an
   identity** — so no loaded entry may carry a resolved cell, tier or element as part of its id.
2. **Small-batch proof before any full run.** The call budget is a **ceiling, not a plan**. This module
   makes no model calls at all, so it inherits the constraint only as: it must load a 5-entry smoke batch
   as happily as a full one, and it must never require a full run to be valid.
3. **The roster is 84 species, not 904.** `DemonSpeciesCatalog.Generated.cs` carries 84 `SpeciesId` rows
   (counted 2026-09-03); 904 is the almanac row count. Per-species counts are tunables.
4. **C1's family-access widening is gated** on three things that do not exist. Until then the generator
   emits structure-gated tiers only, and this loader must not assume an `allowedAtomFamilies` narrowing
   is present on an entry.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `Corpus.load` — a file is a seed file **iff** its top-level JSON object has a non-empty `kind` **and** a list `entries`; anything else is silently not corpus content | `tools/seedsmith/seedsmith/corpus/model.py:158-200`, classification at `:181-186` |
| Parse failure raises `CorpusLoadError` (exit 2, "the tool could not run") rather than becoming a Finding | `corpus/model.py:24-35`, `:176-179` |
| Duplicate real id raises; exemplars go to a separate ledger keyed off a top-level `_exemplars/` path part | `corpus/model.py:84-104`, `:188` |
| `partition` read from the file's `_meta`, defaulting `"(none)"` | `corpus/model.py:190-191` |
| `discover_edges` records every id-shaped string as an edge whether or not it resolves | `corpus/model.py:139-156` |
| The repo seed envelope: `schemaVersion: 1`, `kind`, `entries` — `kind` comes from the **file**, not the folder | `data/seed/README.md:29-40`; e.g. `data/seed/atoms/fx-core.json:1-4` |
| The atom importer sweeps **only** `atoms/`, `containers/`, `curves/`, `rarity/` — never the seed root | `data/seed/README.md:9-10` |
| Adapter registry, three entries | `tools/seedsmith/seedsmith/adapters/registry.py:10-14` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `data/seed/actions/` holds exactly two files and **neither is loadable** — no `kind`, no `entries` | `data/seed/actions/name-templates.json`, `data/seed/actions/pairings.json` |
| `name-templates.json` **cannot be wrapped**: `ActionNameTemplates.Parse` reads `base` and `modifiers` off the root object and rejects a missing or non-object key | `ActionNameTemplates.cs:68-69`, `:79-82` |
| `pairings.json` **cannot be wrapped**: `EnablerPayoffPairings.Parse` requires the root itself to be the payoff to `[enablers]` map | `EnablerPayoffPairings.cs:47-48` |
| Neither file has a production loader; only a test reads `pairings.json` | `tests/FusionRpg.Core.Tests/Actions/ActionSeedingEnablerPayoffTests.cs:89` |

### Real gap

No `action-seed` kind, no `actions` adapter, no envelope-shaped file in that directory, and therefore no
round-trip test is expressible.

## 2. Inputs and outputs

**Reads:** every `*.json` under `data/seed/actions/` (recursive, sorted — `corpus/model.py:170`).

**Writes:** nothing at load time. The envelope below is the contract every writer in this program
(A-S1 briefs, A-S3 survivors, A-S5 reports, A-S6 picks) emits.

```jsonc
{
  "schemaVersion": 1,
  "kind": "action-seed",
  "_meta": {
    "partition": "species/cherrybomb",       // read by Corpus.load into Entry.partition
    "generator": "action-corpus/dedup-select",
    "round": 1
  },
  "entries": [
    {
      "id": "action.species.cherrybomb.001",
      "scope": "species",                    // general | family | species
      "scopeKey": "cherrybomb",              // null when scope=general
      "name": "...",
      "category": "attack",                  // ActionEnums.cs:26-33
      "tags": ["offensive"],                 // ActionEnums.cs:39-49
      "kindHint": "skill",                   // basic | innate | skill — A-S6 may promote to innate
      "rungBand": [5, 10],                   // Rung = rungBand[1], the ceiling — A-S1 §3 step 4
      "targetMode": "area",                  // ActionTargetModes.Name — ActionTargetSpec.cs:103-112
      "areaShape": "row",                    // only under `area` — ActionTargetSpec.cs:134-141
      "relation": "enemy",                   // RelationKinds.Name — RelationKind.cs:23-26
      "structureAxes": ["riderStatus"],
      "atomFamilies": ["atom.burn", "atom.spread"],  // family = POOL reference — constraint 1
      "pairingRole": "enabler",              // enabler | payoff | none
      "pairedPayoffFamily": "atom.rot-punisher",     // an ATOM FAMILY, never a status
      "motifsUsed": ["铁头功"],
      "_provenance": { "pipeline": "...", "model": "...", "promptVersion": 1, "corpusHash": "..." }
    }
  ]
}
```

**⛔ CORRECTED 2026-09-03 (review F7, F10).** Two fields in the earlier example could not survive
this module's own §3 step 5 cross-check:

- **Casing (F10).** The example emitted `"Area"`, `"Row"`, `"Enemy"` — PascalCase enum member names,
  not the wire strings. The code of record returns `"self" "single" "multi" "rolledTarget" "all"
  "area"` (`ActionTargetModes.Name`, `ActionTargetSpec.cs:103-112`), `"row" "column" "square"
  "rectangle"` (`ActionAreaShapes.Name`, `:134-141`), `"self" "ally" "enemy" "any"`
  (`RelationKinds.Name`, `RelationKind.cs:23-26`) and the `DerivedStatChannels` constants for
  categories (`ActionCategories.Name`, `ActionEnums.cs:96-104`). Step 5 mandates a cross-check that
  refuses an unknown member — it would have refused this spec's own example.
- **`enablesStatus` → `pairedPayoffFamily` (F7).** The pairing surface has **no status in it**:
  `pairings.json` maps `atom.chill-punisher`/`atom.rot-punisher` to enabler *atom families*, and
  `EnablerPayoffPairings.IsPayoff(string atomFamily)` / `EnablersOf(string payoffFamily)`
  (`EnablerPayoffPairings.cs:26,30-31`) take atom families throughout. `pairingRole` admits
  `none` — a value, never an omission — because the table has only two payoff keys (A-S1 §3 step 6).
- **`atomPools` → `atomFamilies`**, the one canonical name (A-S1 §3 step 8's table). The code of
  record calls it a family (`AtomRow.FamilyId`, `ActionSeeder.cs:61`); constraint 1's *"an atom names
  a pool"* is held by the constraint, not by the field name.

**A second, declared kind:** `action-config`, for the two shipped files — see §3 step 2. It is a
*manifest entry*, not an envelope: those files' bytes do not change.

### Id grammars — one per kind, not one for the adapter

**⛔ CORRECTED 2026-09-03 (review).** A single `id_pattern`
(`^action\.(general\.[0-9]{4}|(family|species)\.[a-z0-9-]+\.[0-9]{3})$`) was declared for **all**
of this program's envelope kinds. Nine of them mint ids that do not start `action.` at all —
`brief.species.cherrybomb.002`, `weights.species.cherrybomb`, `lean.cherrybomb`,
`cell.species.attack.5-10.enabler`, `innate.cherrybomb`. `discover_edges` records an edge only where
`id_pattern.match(value)` holds (`corpus/model.py:154`), so with one `action.`-only pattern **every
cross-kind reference in this program is silently never recorded as an edge** — a brief that names a
`avoidNeighbours.actionId`, an innate pick that names its chosen action, a coverage cell that names a
subject.

`id_pattern` is a **per-`KindSpec`** field (`adapters/base.py:30`), so the fix is to declare one per
kind rather than one for the adapter:

| `kind` | `id_pattern` | Written by |
|---|---|---|
| `action-seed` | `^action\.(general\.[0-9]{4}\|(family\|species)\.[a-z0-9-]+\.[0-9]{3})$` | A-S3, A-S6 |
| `action-brief` | `^brief\.(general\|family\|species)\.[a-z0-9-]+\.[0-9]{3}$` | A-S1 |
| `action-reject` | `^reject\.[a-z0-9.-]+$` | A-S3 |
| `action-review` | `^review\.[a-z0-9.-]+$` | A-S3 |
| `action-coverage` | `^(cell\|target)\.[a-z0-9.-]+$` | A-S5 |
| `action-innate` | `^innate\.[a-z0-9-]+$` | A-S6 |
| `action-type-weights` | `^weights\.(species\|family)\.[a-z0-9-]+$` | A-T1 |
| `action-role-lean` | `^lean\.[a-z0-9-]+$` | A-S0 |
| `action-characteristic-pool` | `^pool\.[a-z0-9-]+$` | A-S0 |
| `action-config` | (none — a manifest entry, not an entry graph) | — |

`discover_edges` is called **once per kind with that kind's pattern**, and the union of the results is
the program's edge set. A reference from one kind to another (a brief naming an `actionId`) is matched
by the **target's** pattern, which is why the patterns must all exist before any of them is useful.

## 3. The algorithm

Deterministic, total, and pure — no network, no database, no mutation outside the returned graph.

1. **Enumerate** `sorted(root.rglob("*.json"))`, exactly as `Corpus.load` already does
   (`corpus/model.py:170`), so ordering never depends on the filesystem.
2. **Classify each file** into one of three, and *record* the classification rather than dropping it:
   - **envelope** — top-level object with non-empty `kind` and list `entries`. Loaded.
   - **declared config** — its repo-relative path is listed in `data/seed/actions/_manifest.json`
     (`{"schemaVersion":1,"kind":"action-config","entries":[{"id":"pairings.json","reason":"..."}]}`).
     Skipped **with a reason**.
   - **undeclared** — anything else. **A finding**, not a silent skip.

   The third case is the whole point: the silence in `Corpus.load` is correct for a stray registry
   document and wrong for a seed file that lost its envelope in an edit.

2b. **⛔ Exclude the working rounds from the committed corpus — added 2026-09-03 (review F14).**
   `Corpus.load` walks `sorted(root.rglob("*.json"))` — the **whole tree**
   (`corpus/model.py:170`) — and `Corpus.add` raises `CorpusLoadError` on a duplicate real id
   (`corpus/model.py:92-101`). A-S3 writes survivors under `data/seed/actions/round-<n>/` and A-S6
   writes the committed corpus with the **same ids under the same root**, so a duplicate is
   structurally guaranteed the moment A-S6 promotes. No spec named the move. It is named here,
   because this module owns what `Corpus.load` sees:

   - **`data/seed/actions/_rounds/` is the working root**, and it is **excluded from the corpus load**
     by a declared prefix, listed in `_manifest.json` alongside the two config files. A-S3's outputs
     move from `round-<n>/` to `_rounds/round-<n>/` (see `spec-dedup-select.md` §2's corrected
     table). The leading underscore matches the convention `_exemplars/` already uses
     (`corpus/model.py:188`).
   - **A-S6's promotion is a MOVE, not a copy.** The promoted seed leaves `_rounds/round-<n>/` and
     lands in the committed corpus under the same id; the round file records the id as `promoted`
     rather than keeping the row. One id exists in exactly one place.
   - **Loading `_rounds/` is an explicit, separate call** (`Corpus.load(root / "_rounds" / f"round-{n}")`),
     which is how A-S3 and A-S5 read a round without also loading the committed corpus into the same
     graph.
   - **A duplicate id is still a raise**, and that is correct — this step removes the *guaranteed*
     collision so the raise stays a signal about real content rather than a scheduling artefact.
3. **Build the graph** through the existing `Corpus.add` — duplicate ids raise, exemplars route to their
   own ledger (`corpus/model.py:84-104`).
4. **Validate each entry** against the `KindSpec` for `action-seed`: `required = {id, scope, category,
   rungBand, targetMode, relation, atomFamilies, pairingRole}`; `optional = {scopeKey, areaShape,
   tags, kindHint, structureAxes, pairedPayoffFamily, motifsUsed, name}`;
   `reference_fields = {atomFamilies, pairedPayoffFamily, scopeKey}`.
   ⛔ **CORRECTED 2026-09-03 (review F7):** `atomPools` → `atomFamilies`, `enablesStatus` →
   `pairedPayoffFamily`, and `pairingRole` moves to **required** because `none` is a value and a
   missing key is a defect.
5. **Cross-check the closed vocabularies** against the code of record, never against a re-typed list —
   and against the **`Name` functions**, not the enum member names, because the wire strings are what
   an entry carries: categories `ActionCategories.Name` (`ActionEnums.cs:96-104`), tags
   `ActionTags.Name` (`:128-139`), kinds `ActionKinds.Name` (`:72-78`), target modes
   `ActionTargetModes.Name` (`ActionTargetSpec.cs:103-112`), area shapes `ActionAreaShapes.Name`
   (`:134-141`), relations `RelationKinds.Name` (`RelationKind.cs:23-26`), statuses
   `StatusCatalogBootstrap.cs:15-56`. An unknown member is refused, never skipped. ⛔ **CORRECTED
   2026-09-03 (review F10):** citing the enum declarations rather than their `Name` functions is what
   let this spec's own example emit `"Area"`.
6. **Register the adapter** as `"actions"` in `ADAPTERS` (`adapters/registry.py:10-14`) and nowhere else.

## 4. What it must NOT do

- **Never rewrite `name-templates.json` or `pairings.json`.** Both C# parsers read the root object
  directly (`ActionNameTemplates.cs:68-69`, `EnablerPayoffPairings.cs:47-48`); an envelope makes both throw.
- Never call a model. Never import the LLM transport at all — *tests never call a model, and the stub
  raises.*
- Never write into `data/seed/actions/` during a load.
- Never add a fifth folder to the atom importer sweep — it deliberately reads four
  (`data/seed/README.md:9-10`), and `actions/` is not one of them.
- Never invent the C# eligibility surface. `ActionRow` has no field naming who may hold an action
  (`ActionRow.cs:18-53`), and whether `scope`/`scopeKey` becomes a column or a table is explicitly a
  later decision (`action-corpus-ideal.md` §6). **No module in map §4 owns it** — recorded here so the
  gap stays visible rather than being absorbed by this one.
- Never resolve a pool reference into a concrete atom. Constraint 1.

## 5. Testing strategy

| Case | Expect |
|---|---|
| **Determinism** | `Corpus.load` twice over an unchanged tree gives identical entry ids, identical order, identical hash over the canonical dump. Byte-identical, asserted by hash |
| **Round trip (Checkpoint 1)** | write an `action-seed` file, load it back, get the same entries — the check the map calls Checkpoint 1 |
| **Planted violation — lost envelope** | a file under `actions/` with `entries` but no `kind`, absent from the manifest, produces an **undeclared** finding. The test fails if it is silently skipped |
| **Planted violation — duplicate id** | two entries sharing an id in different files raise `CorpusLoadError` naming both paths (`corpus/model.py:96-101`) |
| **Planted violation — unknown enum** | `category: "economy"` is refused, naming the field and the value |
| **Planted violation — wrong casing** | an entry carrying `targetMode: "Area"`, `areaShape: "Row"` or `relation: "Enemy"` is **refused**, naming the field — the exact shape this spec's own example carried before the F10 correction |
| **Planted violation — a status in a pairing field** | `pairedPayoffFamily: "rot"` is refused; only a key of `pairings.json` is legal (`EnablerPayoffPairings.cs:26`) |
| **Round isolation (F14)** | a survivor under `_rounds/round-1/` and its promoted twin in the committed corpus do **not** both load: the committed load excludes the `_rounds/` prefix, and a test asserts no `CorpusLoadError` is raised over a tree containing both |
| **Per-kind id patterns** | `discover_edges` run once per kind records an edge for a brief's `avoidNeighbours.actionId`, an innate pick's `innateActionId` and a weights row's `scopeKey`; a test fails if any of the ten kinds has no pattern |
| **Config files survive** | `ActionNameTemplates.Parse` and `EnablerPayoffPairings.Parse` still parse the shipped bytes. A regression here is the failure this module exists to avoid |
| **Offline guarantee** | the suite runs with the transport stubbed to **raise**, proving "makes no call" rather than assuming it (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234)`) |

## 6. Acceptance criteria

1. `Corpus.load` over `data/seed/actions/` returns at least one `action-seed` entry from an
   envelope-shaped file, and `by_kind("action-seed")` is non-empty.
2. `name-templates.json` and `pairings.json` are byte-identical to their pre-change state, and both C#
   parsers still accept them under `dotnet test tests\FusionRpg.Core.Tests --filter ActionSeeding`.
3. An undeclared envelope-less file under `actions/` produces a finding naming the path.
4. Two entries sharing an id raise `CorpusLoadError` naming both source paths.
5. `resolve_adapter("actions")` returns the adapter; `known_adapter_names()` includes it.
6. Every closed vocabulary the adapter declares is derived from the C# code of record's **`Name`
   functions**, and a test asserts each member count **and its exact wire string**: 3 kinds · 5
   categories · 8 tags · **6** target modes (`"self" "single" "multi" "rolledTarget" "all" "area"`) ·
   4 area shapes (`"row" "column" "square" "rectangle"`) · 4 relations (`"self" "ally" "enemy"
   "any"`) · 21 statuses.
6b. Every kind this program writes declares its **own** `id_pattern` (§2's table), and
   `discover_edges` is run once per kind — so a cross-kind reference is recorded as an edge rather
   than silently dropped by an `action.`-only pattern.
6c. A committed-corpus load **excludes** the `_rounds/` prefix, and a tree holding both a round
   survivor and its promoted twin loads without a duplicate-id raise (§3 step 2b).
7. A second load over unchanged inputs is byte-identical by hash.
8. `python -m pytest tools/seedsmith/tests` passes with the LLM transport stubbed to raise.

## 7. Dependencies

**Depends on:** nothing (map §5 — `A-C1` stands alone at the head of the build order).
**Depended on by:** every round-trip test in the program; A-S1 brief files, A-S3 survivors, A-S5
reports and A-S6 picks all write through this envelope.
**Cross-program (map §7):** none blocking. `effect-atom` **E30** owns the channel pools an
`atomFamilies` reference points at; an unresolvable reference is recorded as an edge, not an error
(`corpus/model.py:139-145`), so this module lands before E30 without waiting on it.
