# Spec: Ladder consistency repair (`ladder-consistency-repair`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `ladder-consistency-repair`
**Owning programs:** `seedsmith` (`theme-refresh`) + `fe-essentials` (the roster sort)
**Depends on:** `tier-propagation-contract`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) § The shape 3, DO #4

---

## Objective

**Fix the measured defects already in the ladder chain, before anything is built on top of it.**

These are **bugs, not design.** One of them is visible to players right now. Nothing downstream in
this initiative is worth building on a chain with these in it.

---

## What exists today — measured this session

### Defect 1 ⛔ — 84 retired rarity ids in the creature theme registry

**Located precisely.** The ideal named `themes.v1.json` without saying which; there are two.

**`data/seed/creatures/_registry/themes.v1.json`** holds **904 themes**, one per species, each with a
`rarity` field. Counted directly:

| Value | Count | Status |
|---|---|---|
| `fused` | 442 | current |
| `cultivated` | 143 | current |
| `heirloom` | 48 | current |
| `chimeric` | 67 | current |
| `chaff` 29 · `grafted` 31 · `sprout` 30 · `almanac` 22 · `sunwoven` 4 · `firstseed` 4 | 120 | current |
| ⛔ **`common` 42 · `rare` 21 · `epic` 14 · `legendary` 7** | **84** | **retired four-value ladder** |

**820 current + 84 retired = 904.** The 84 is an independent measurement, not a figure carried
forward.

⚠ **A coincidence worth naming so nobody "reconciles" it.** Several comments in the repo refer to
*"`themes.v1.json` (84 rows)"* — e.g. `seedsmith/adapters/dungeon/planner.py:10`,
`dungeon/registries.py:21`. **That 84 is the old 84-species catalog size, and it is a stale comment**
— the file has 904 rows today. It is *unrelated* to the 84 retired rarity ids. Two different 84s.

⛔ **The mechanism is NOT a generator defect — an earlier draft got this wrong, and the difference
changes the whole module.**

An earlier draft cited `generate_themes.py:33` as *"a defect made `theme-refresh` a no-op over the
real 904-row roster."* **That comment is past tense and describes an already-fixed defect.** Measured:

| Tree | Retired ids |
|---|---|
| `data/seed/creatures/species/**` (904 rows) | **0** — all ten current ids |
| `data/seed/creatures/_registry/themes.v1.json` (904 rows) | **84** |

**The inputs are already clean.** The 84 survive because the registry is **deliberately append-only**:

> `generate_themes.py:42-43` — *"Rarity is a snapshot carried by the anchor seed. **Keep the legacy
> 4-rung values for already published themes through the append-only merge**; new rows use the current
> anchor's rung."*
>
> `:8-9` — *"a published theme is a snapshot, never re-derived (spec-creature-themes.md §2.4a)"*

So *"fix the generator and regenerate"* **does not fix the 84.** A normal re-run preserves them by
design.

### ⛔ And the one escape hatch has an expired sanction

`--rebuild` is the only path that rewrites a published row, and its permission was **explicitly
conditional** (`generate_themes.py:8-14`):

> *"`--rebuild` discards published themes and re-derives them, which append-only normally forbids…
> **permitted here** for the same reason G1.3's was: **nothing is bound to these keys yet.** Measured
> — the items corpus references only legacy `theme.*` keys (38 entries), and **zero `creature.*`
> keys**. **That window closes the moment an item is authored against a creature theme; after that, a
> motif correction needs `themes.v2.json` plus a migration.**"*

**Measured today: 910 set entries — `creature.*` 844, `build.*` 36, `theme.*` 30.**

⭐ **The window is closed.** `--rebuild` is no longer sanctioned for this registry.

**`data/seed/items/_registry/themes.v1.json`** is a different file — `frozen`, with `frozenUtc` and
`minCompatibleVersion`, and it carries **zero** rarity ids. It is **not in scope.**

### ⭐ Defect 1 has a bonus the ideal did not record

Each theme row carries **`speciesId` directly**:

```json
"creature.abyssswordstar": { "speciesId": "abyssswordstar", "displayName": "…", "rarity": "chimeric", … }
```

**This registry is a real `themeKey → speciesId` index.** It is a materially better join for
`set-species-binding` than parsing the key's suffix — a registry lookup that fails loudly instead of
string manipulation that fails silently. **Filed as a cross-module note**, since it strengthens that
module's design rather than this one's.

### Defect 2 ⛔ — the FE roster sort is silently dead

⚠ **An earlier draft asserted "four-plus FE sites" with no cite — for the one defect it called
player-visible. Cites added:**

| Site | What it keys on |
|---|---|
| `web/fusion-rpg-web/src/features/creatures/rosterSplit.ts:5` | `RARITY_ORDER = { legendary: 0, epic: 1, rare: 2, common: 3 }` |
| `web/fusion-rpg-web/src/lib/bus/creatures.ts:16` | `rarity` typed as the four-value union |
| `web/fusion-rpg-web/src/layers/pacts/PactsLayer.tsx:25` | the same vocabulary |
| `web/fusion-rpg-web/src/pages/CreaturesPage.tsx:238` | the same vocabulary |

Because no species carries those ids in the runtime catalog, the comparator matches nothing and the
sort **is inert today** — it does not throw, it does not warn, it just does not sort. **This is the
one defect a player can see right now.**

### Defect 3 — duplicate ladder declarations

Owned by `tier-propagation-contract` (T-2), which is this module's dependency. Listed here only so
the two are not fixed twice: `EncounterTuning.cs:40-43`, the two Python `RARITY` tuples, and
`RarityLadder.RungIds`.

### Deliberately NOT in scope — the 783-vs-19 family vocabularies

The ideal lists reconciling the anchor family labels as part of the consistency pass. **It is
excluded here.** The family layer was **withdrawn** (`tier-system-ideal.md` § DISPOSITION), so
reconciling its vocabulary is work for a problem nobody has. It returns if the family layer does.

---

## Tech stack

Python 3 (seedsmith `theme-refresh`), TypeScript/React (`web/fusion-rpg-web`), pytest + vitest.

## Commands

```powershell
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k theme
cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures
cd web/fusion-rpg-web; npm ci; npm test; npm run build; npm run check:bundle
```

⚠ `npm run build` runs `tsc --noEmit` — **type errors fail the build**, which is the fastest way to
catch a comparator still referencing a retired id.

## Project structure

| Path | Role |
|---|---|
| `tools/seedsmith/seedsmith/adapters/creatures/generate_themes.py` | `theme-refresh` — the generator to fix |
| `data/seed/creatures/_registry/themes.v1.json` | Regenerated, **not hand-edited** (below) |
| `web/fusion-rpg-web/src/**` | The four-plus roster-sort sites |

## Code style

The FE comparator reads the ladder's ordinal rather than a hand-written order:

```ts
// Sort by the ladder's ORDINAL, never by a literal id list. The previous comparator keyed on the
// retired four-value vocabulary (common/rare/epic/legendary); because no species carries those ids,
// it matched nothing and the sort was silently inert — no throw, no warning, just no sorting.
const byRarity = (a: Species, b: Species) => rarityOrdinal(a.rarity) - rarityOrdinal(b.rarity);
```

⚠ `rarityOrdinal` must **throw or surface** on an unknown id rather than returning a default. A
default is what turned this defect into a silent one.

---

## ⭐ The decided path — `themes.v2.json` + migration (owner, 2026-09-13)

With `--rebuild` unsanctioned and a normal re-run preserving the 84 by design, the generator's own
text names the remaining route, and the owner took it: **`themes.v2.json` plus a migration.**

**What that means concretely:**

1. **Publish `themes.v2.json`** — 904 rows, every `rarity` re-derived from the current anchor, every
   `motifs` / `expression` / `speciesId` / `basis` carried across **unchanged**. `themes.v1.json` is
   left untouched and unretired, so nothing that reads it breaks mid-migration.
2. **Migrate the consumers.** `set-charm-gen` (item module 13) *"consumes the creature theme
   registry"*; `generate_families.py:48` and `theme_enrich.py:24` also read it. Each moves to v2 in
   one reviewed change.
3. **The 844 bound `creature.*` keys do not change.** The migration changes a **field inside a row**,
   not a key — so no set entry's `themeKey` moves and no binding is broken. ⭐ **This is what makes the
   migration tractable**, and it should be stated in the migration's own notes so a reader does not
   fear a key rename.
4. **`minCompatibleVersion` semantics** follow the `classes.v*.json` precedent: v2 only corrects a
   field's value within a closed vocabulary, so content authored against v1 stays valid.

⚠ **This is a cross-program ask, not this module's decision to take alone** — it reopens an
append-only rule owned by `seedsmith` (`spec-creature-themes.md` §2.4a) and moves a registry item
module 13 consumes. **File against both before building.**

⚠ **And it makes `set-species-binding`'s ordering load-bearing**, not merely hygienic: that module
reads `speciesId` from this registry. It must read **v2**, and therefore must follow this module.

---

## ⛔ The authored-vs-generated question, answered explicitly

`AGENTS.md` says registries (`**/_registry/**`) are **hand-authored**, and that generated trees are
**never hand-edited**. `themes.v1.json` sits in `_registry/` but is **produced by `theme-refresh`**.

**The generator wins.** A hand edit to the 84 rows would be reverted or diverged from by the next run,
and it would leave `themes.v1.json` and the generator disagreeing with no detector.

**But — and this is the correction — "fix the generator and regenerate" is not the path either**, because
the generator is behaving correctly: append-only preservation is its specified behaviour, not a bug.
**The sanctioned path is the v2 publication above**, which is a generator *run against a new target*,
not a hand edit and not a defect fix.

---

## Tunables

**None.** This module changes no balance number. Every value it touches is an id in a closed
vocabulary.

## Numeric types

**No magnitude is produced or consumed.** Rarity ordinals are small `int`s used for ordering only —
never as a magnitude, never as a multiplier. `ssot-rarity.md` §3.6's ban on `CurveInput.Rarity` for
items stands: *"a multiplier on the rung makes rarity dominant and destroys the overlap."*

## ActorHub gate

**N/A and checked.** A theme's rarity id and an FE sort order produce no actor combat / derived /
AppliedCombat number. No composer, no fold, no contribution.

## Testing strategy

| Level | What it asserts |
|---|---|
| pytest | `themes.v2.json` carries **904 rows**, every `rarity` a current id, computed from the anchor — asserted as a **closure property**, never as "84 were fixed" |
| pytest | Every `motifs` / `expression` / `speciesId` / `basis` value is **byte-identical** between v1 and v2 — the migration changes exactly one field |
| pytest | Every `themeKey` present in v1 is present in v2 — **no key renamed, no binding broken** for the 844 bound set entries |
| pytest | Every emitted `rarity` is one of the ten (closed vocabulary — pinning is correct) |
| pytest | A retired id encountered on input is **mapped forward via the one-way map** and reported, never passed through |
| pytest | The refresh is **idempotent** — a second run produces a byte-identical tree |
| vitest | The roster sort orders by ordinal, proven with species spanning several rungs |
| vitest | ⭐ An **unknown** rarity id surfaces rather than silently sorting to a default — the test that would have caught this defect |
| build | `npm run build` (`tsc --noEmit`) passes; no site references a retired id |
| Contract | `seedsmith check data/seed/creatures --adapter creatures` green |

⛔ **No test asserts "904 themes" or "84 fixed."** Both are readings. Assert the **closure property**:
every emitted rarity is a current id, and every species has exactly one theme row.

## Boundaries

**Always**
- **Publish v2; never hand-edit v1 and never `--rebuild` it.** See the explicit reasoning above.
- Map retired ids through the existing one-way forward map (`LegacyCreatureRarityIds`), never a
  hand-written table.
- Make unknown-id handling loud on both sides.
- Run vitest **and** `npm run build`; type errors fail the build.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- Any change to the items `themes.v1.json`. It is **frozen**, carries `minCompatibleVersion`, and is
  out of scope.
- Reconciling the family vocabularies (withdrawn — see above).

**Never**
- ⛔ Hand-edit the 84 rows. It fixes the symptom and leaves the generator defect that produced it.
- Return a default for an unknown rarity id. That is what made this silent.
- "Reconcile" the stale *"84 rows"* comments with the 84 retired ids. **They are different numbers.**
- Add a legacy id back to any closed vocabulary.
- Assert a population count.

## Success criteria

1. `themes.v2.json` is published with 904 rows and zero retired rarity ids, and every non-`rarity` field is byte-identical to v1.
2. **Zero retired rarity ids** in `data/seed/creatures/_registry/themes.v1.json` — asserted as a
   closure property, not as "84 were fixed."
3. The refresh is idempotent, proven by a second run producing a byte-identical tree.
4. The FE roster sort **visibly sorts**, and an unknown id surfaces instead of silently defaulting.
5. `npm test`, `npm run build`, `npm run check:bundle` green.
6. `seedsmith check --adapter creatures` green.
7. The corpus diff is a pure regeneration.
8. The stale *"84 rows"* comments are corrected where encountered, and explicitly **not** conflated
   with the retired-id count.

## Open questions

1. **Do the 84 species get their *current* rarity from the catalog, or re-derived from the forward
   map?** They are the same value in every case the forward map covers — but if any of the 84 has
   drifted, the two answers differ. **Recommendation: take the catalog's value and report any
   disagreement** — the catalog is what the game runs on, and a disagreement is itself a finding.
2. **Should `rarityOrdinal` throw or render a visible "unknown" state on the FE?** **Recommendation:
   render a visible unknown state.** Throwing in a comparator can blank a whole roster view; a
   visible marker fails loudly without taking the page down.
