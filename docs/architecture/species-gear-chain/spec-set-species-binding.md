# Spec: Set species binding (`set-species-binding`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `set-species-binding`
**Owning program:** `item` module 13 (`set-charm-gen`)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [species-craft-ideal.md](../species-craft-ideal.md) § The shape 1

---

## Objective

**Make a set piece's species a queryable field instead of a string a human can read.**

> **Owner, 2026-09-13:** *"real field, use deterministic engine to repair it, and extend seedsmith
> generator — this should be decided by deterministic engine, LLM shouldn't."*

844 shipped set entries are already themed on a creature. Nothing can *query* that today, so every
species-aware behaviour downstream — cost shaping, species materials, an armoury filter — has nothing
to key on. This module adds the key.

It needs **no new material, no new craft verb, and no evaluator change** — `SetEvaluator` is already
class-agnostic. It is a schema change, a generator change, a deterministic repair, and a
regeneration.

⛔ **The LLM must not decide this, and the repo already enforces why.** Species identity is a
**join**, not a judgement. The binding principle is that the model writes *identity* (names, flavour)
while deterministic code writes everything a system keys on, and `audit_schema` exists precisely to
keep model output out of fields like this. A model-authored `speciesId` would be an unverifiable
guess at a fact the corpus already contains.

---

## What exists today — measured this session

### Built

**910 set entries across 885 files in `data/seed/items/sets/**`.** A shipped entry's fields are
`id`, `nameKey`, `name`, `themeKey`, `members`, `thresholds`, `flavor`, `tags`, `notes`.

⚠ **Corrected: there are three shapes, not one.** 880 of 910 entries carry exactly that nine-field
set; **24 lack `flavor`**, and **5 carry `flavorKey`/`iconKey` while lacking `tags`/`notes`**. The
repair must tolerate all three rather than assume the modal shape.

**884 distinct `themeKey` values**, by prefix (counted over entries):

| Prefix | Entries | Has a species? |
|---|---|---|
| `creature.*` | **844** | yes |
| `build.*` | 36 | no |
| `theme.*` | 31 | no |

- `SetEvaluator` counts **membership**, not upgrade state, and is class-agnostic — so adding a field
  changes no bonus.
- `SetExclusivityValidator.cs:33` is the **only** set-aware craft rule today (D21 suppresses
  Strain/Splice on set pieces); `:41` `MaySocket => true`.

### Real gap

There is **no `speciesId` and no `setClass` field on a set entry.** `themeKey` is a presentation key
that happens to encode the species.

### ⭐ The repair is total — and it has one trap, found by measurement

The ideal expected the repair to refuse a remainder. **It does not need to.** Measured directly:

> **All 844 distinct `creature.*` themeKeys resolve to a species in the 904-species catalog — 844 of
> 844, zero unresolved.**

⚠ **But only case-insensitively.** `themeKey` is lower-case (`creature.abyssswordstar`) while
`speciesId` is PascalCase (`AbyssSwordStar`). **An exact-match repair resolves zero of 844** — it
would silently produce an entirely empty column and look like a content gap rather than a join bug.

This is the single most important line in this spec: **the join is case-normalising, and that must be
explicit, tested, and commented — never incidental.**

### ⭐ A better join than parsing the key — found while specing `ladder-consistency-repair`

`data/seed/creatures/_registry/themes.v1.json` holds **904 theme rows keyed by exactly these
`themeKey` values**, and each row carries `speciesId` **as a field**:

```json
"creature.abyssswordstar": { "speciesId": "abyssswordstar", "displayName": "…", "rarity": "chimeric", … }
```

**So the corpus already ships a `themeKey → speciesId` index.** Preferring it over suffix-parsing is
strictly better: a registry lookup **fails loudly on a missing key**, while string manipulation
quietly produces a plausible-looking wrong answer. It also means the repair reads a *declared*
relationship rather than inferring one from a naming convention that nothing enforces.

⚠ **The case normalisation is still required** — the registry's `speciesId` is lower-case
(`abyssswordstar`) and the catalog's is PascalCase (`AbyssSwordStar`). The registry removes the
*parsing* risk, not the *casing* one.

⚠ **And one ordering constraint:** 84 of that registry's 904 rows carry retired rarity ids, being
fixed by `ladder-consistency-repair`. That module **regenerates the file**. This module reads
`speciesId`, which that fix does not touch — but the two must not regenerate the same tree
concurrently. **Sequence them, or read the catalog directly and use the registry only as a
cross-check.**

**Recommendation: use the registry as the primary join and the suffix parse as a cross-check**, with
a disagreement between the two treated as a refusal. Both are cheap; agreeing is the evidence.

⚠ **A correction to the ideal, stated rather than absorbed:** it carried *"844 of 884 distinct
themeKeys resolve today, so the repair must refuse and report the remainder."* The 884/844 split is
real but describes prefixes, not failures — **844 of 844 `creature.*` keys resolve.** The
refuse-and-report machinery is still required (see Boundaries), because a *future* key may not
resolve; it just has no backlog to work through today.

---

## Tech stack

Python 3 (seedsmith `items` adapter, module 13), C# .NET 8 (the importer/reader), pytest + xUnit.
No new dependency.

## Commands

```powershell
cd tools/seedsmith
python -m seedsmith check data/seed/items --adapter items
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_items_adapter.py -q
dotnet run --project tools/ItemSeedValidator
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSet"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Set"
```

⚠ `test_items_adapter` **fails pre-existing on a clean HEAD** (`AGENTS.md`). Confirm it already fails
before blaming this change.

## Project structure

| Path | Role |
|---|---|
| `tools/seedsmith/seedsmith/adapters/items/setgen/**` | Forward: emit the field |
| `tools/seedsmith/seedsmith/adapters/items/**` (a new repair pass) | Backward: extract from `themeKey` |
| `data/seed/items/sets/**` | **Generated output** — regenerated, never hand-edited |
| `src/FusionRpg.Data/**` set import | Reads and persists the new field |
| `tools/ItemSeedValidator/Program.cs` | Closure gate on the new field |

## Code style

The join is explicit about its normalisation and about absence:

```python
def species_for_theme(theme_key: str, catalog: "dict[str, str]") -> "str | None":
    """themeKey -> speciesId, or None for a theme with no species.

    ⚠ CASE-NORMALISING BY NECESSITY, not by convenience: themeKey ships lower-case
    ('creature.abyssswordstar') and speciesId ships PascalCase ('AbyssSwordStar'). An exact
    match resolves 0 of 844 and looks like a content gap instead of a join bug.

    Returns None ONLY for a non-creature theme (build.*/theme.*, 40 distinct today), which is an
    explicit ABSENT value. A creature.* key that fails to resolve is an ERROR, not a None —
    the caller must refuse and report it, never write absent."""
    prefix, _, rest = theme_key.partition(".")
    if prefix != "creature":
        return None
    return catalog.get(rest.lower())      # catalog keyed by speciesId.lower()
```

⭐ The distinction in that docstring is the whole correctness argument: **"no species" and "species not
found" must never collapse into the same value.** One is a fact about a build-themed set; the other is
a defect.

---

## Tunables

**This module introduces no balance number.** It adds two identity fields and a join.

| Value | Meaning | Owner |
|---|---|---|
| `setClass` templates — member-role count and threshold list per class | `decisions.md:134`'s ten/fifteen-role shapes, as **data** rather than a generator heuristic | A `data/tuning/` set-planning file owned by item module 13 |

**Structural:** none introduced.

## Numeric types

**No magnitude is produced or consumed.** `speciesId` and `setClass` are identity strings. Stated
explicitly so the next module does not assume this one settled a numeric question: the *cost*
shaped by this species is `species-cost-shaping`'s, and it is a per-mille multiplier there.

## ActorHub gate

**N/A and checked.** A set's species binding produces no actor combat / derived / AppliedCombat
number. Set bonuses continue to compose exactly as today, through `SetEvaluator` and the existing
atom path into `ActorHub`. **No private fold is introduced and no bonus magnitude changes** — adding a
field to an entry must not move a single number, which is Success criterion 5.

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit (python) | ⭐ **The case-normalising join resolves a lower-case `themeKey` to a PascalCase `speciesId`** — the test that would have caught the zero-resolution trap |
| Unit (python) | A `build.*` / `theme.*` key yields **explicit absent**, distinguishable from not-found |
| Unit (python) | A `creature.*` key that does not resolve **raises and reports** — it never writes absent |
| Unit (python) | The repair is **idempotent**: running it twice produces a byte-identical tree |
| Unit (python) | The repair is **deterministic** — no RNG, no model call, no ordering dependence |
| Contract | `seedsmith check data/seed/items --adapter items` passes after regeneration |
| Contract | `ItemSeedValidator` closes: every non-absent `speciesId` resolves in the species catalog |
| Unit (C#) | Set bonus evaluation is **unchanged** for every shipped set — the field is inert to `SetEvaluator` |
| Report | Print resolved / absent / refused counts — **readings, printed, never asserted** |

⛔ **No test asserts "844 sets carry a species."** That is a reading and it grows when content ships.
Assert the **closure property**: every `creature.*` key resolves, or the run fails naming the key.

## Boundaries

**Always**
- **Fix the generator and regenerate.** `data/seed/items/**` is seedsmith output — ~1011 of 1041 files
  carry `_meta.model`. This is the repo's most-repeated incident, twice attempted in one week.
- Keep "no species" and "species not found" as **different** outcomes.
- Run `seedsmith check` and `ItemSeedValidator` before committing.
- Commit the generator change and the regenerated corpus as **separate logical commits**, via MCP
  `repo-git.commit` with explicit `paths`.

**Ask first**
- The `setClass` vocabulary — `decisions.md:134` fixes ten/fifteen-role shapes and two thresholds;
  which classes exist is a content decision.
- Changing `themeKey`'s meaning. It stays the **presentation** key; this module adds a field beside
  it, it does not repurpose it.

**Never**
- ⛔ **Let a model author `speciesId`.** It is a join, not a judgement. `audit_schema` exists to keep
  model output out of fields a system keys on.
- ⛔ **Hand-edit a set JSON** to fix a resolution failure. The next generation run reverts it, the run
  ledger stops describing the file, and the change is invisible to every other consumer.
- Write `absent` for a `creature.*` key that failed to resolve. That converts a defect into content.
- Touch set bonus magnitudes. `ssot-sets.md` owns them, and D3's documented set-dominance spiral is
  the reason not to adjust them opportunistically.
- Let a craft verb break or re-check a set bonus — the bonus counts **membership**, not upgrade state.

## Success criteria

1. Every set entry carries `speciesId` (a real id or an explicit absent) and `setClass`.
2. `set-charm-gen` emits both fields for newly generated sets.
3. The deterministic repair fills every existing entry, with **no model call anywhere on the path**.
4. Every `creature.*` themeKey resolves, or the run fails naming the unresolved key.
5. ⭐ **No set bonus magnitude changes** — proven by a diff of evaluated bonuses before and after.
6. The repair is idempotent and deterministic, proven by a second run producing a byte-identical tree.
7. `seedsmith check --adapter items` and `ItemSeedValidator` green.
8. The corpus diff is a pure regeneration — verified by re-running the generator.

## Open questions

1. **Do the 36 `build.*` and 31 `theme.*` sets get a `setClass` too?** They have no species but they do
   have a shape. **Recommendation: yes** — `setClass` is orthogonal to species, and excluding them
   would make the field nullable for two unrelated reasons at once.
2a. ⭐ **Which `speciesId` SPELLING is persisted?** — **decided here, because getting it wrong
   reproduces the casing bug one hop downstream.** The repo carries both: `data/generated/creatures/*.json`
   is **PascalCase** (`AbyssSwordStar`), while `CreatureSpeciesCatalog.Generated.cs`,
   `data/seed/creatures/species/**` and the themes registry are **lower-case** (`abyssswordstar`).
   `species-cost-shaping` looks up `BaseRarity` by this id against the **runtime catalog**, and
   `species-materials` keys material ids on it. **Persist the runtime catalog's spelling (lower-case),
   and assert in `ItemSeedValidator` that every non-absent `speciesId` resolves in
   `CreatureSpeciesCatalog`** — which is success criterion 7's closure check with the spelling pinned.

2. **Is `speciesId` nullable, or is there a sentinel?** **Recommendation: nullable/absent, never a
   sentinel string.** A sentinel would be indistinguishable from a species named after it, and
   `tunables-ssot.md` T5's posture — an expected-but-missing value is a rejection, never a silent
   default — is the precedent this repo already follows.
3. **Does the case-normalising join get pushed upstream** — i.e. should `themeKey` and `speciesId`
   share a casing convention? Recommendation: **no, not in this module.** Renormalising ids across two
   shipped corpora is a migration; a documented, tested join is the honest fix here. Worth filing as
   its own cleanup.
