# Spec: Species materials (`species-materials`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `species-materials`
**Owning programs:** `item` module 14 (`salvage-craft`) + `creature-seed`
**Depends on:** `species-cost-shaping`, `creature-drop-tables`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [species-craft-ideal.md](../species-craft-ideal.md) § The shape 3 — and [tier-system-ideal.md](../tier-system-ideal.md) D2 / E3b

---

## Objective

**⭐ DECIDED — a species-bound set piece is enhanced with its own species' material.**

This is the last module in the initiative and the reason the other fourteen are worth building.
844 set entries are themed on a creature; `set-species-binding` makes that queryable and
`species-cost-shaping` makes it legible. **This slice makes it *earned*.**

> An earlier draft demoted this to optional, on the grounds that a declared species field delivers
> *"the same legibility"* more cheaply. **It does not:** a field makes the binding *queryable*, while
> the material is what makes it *earned*. Slice 1 is the prerequisite, not the substitute.

---

## ⛔ The ask-first boundary, and the exact test the repo already states

`MaterialClass` is **closed at five**, and `MaterialCatalog.cs:6-9` states the criterion for a sixth
verbatim:

> *"Closed: a sixth is an **ask-first** boundary, and the question a lane must answer to earn one is
> **'which of these five questions is unanswerable for my spend?'** — not 'I want another currency'."*

The five questions, from the enum's own doc comments:

| Class | The question it answers | Ids |
|---|---|---|
| `Souls` | *"May I act at all?"* — the flat fee | 0 (a ledger balance, *"which is why `All` is 27 and not 28"*) |
| `Shard` | *"How good may it be?"* — the rarity ceiling | `shard.{rung}` — **10** |
| `Substrate` | *"What is it made of?"* — **frame-locked**, graded by item level | `substrate.{frame}.{grade}` — 2 × 4 = **8** |
| `Essence` | *"What flavour?"* — element direction, **no magnitude** | `essence.{element}` — **6** |
| `Catalyst` | *"What am I doing to it?"* | `catalyst.{verb}` — **3** |

**10 + 8 + 6 + 3 = 27.** Verified by counting.

### The answer this module must give

**The species material answers *"what did this come from?"* — and none of the five asks that.**

- Not `Shard`: that is a rarity ceiling, and the species' rung already rides it via
  `species-cost-shaping`.
- Not `Essence`: element direction carries **no magnitude** and no provenance.
- Not `Catalyst`: that is the verb, not the source.
- ⚠ **Closest is `Substrate`** — *"what is it made of?"* — and this is the one that needs arguing
  rather than asserting. **It fails on granularity:** `Substrate` is `{frame}.{grade}` over exactly
  two frames and four grades, deliberately 8 ids, and its cost `variable` is `grade`. A species is
  neither a frame nor a grade. Folding species into it would mean `substrate.{species}.{grade}` —
  **904 × 4 = 3,616 ids**, which is precisely MH's documented `itemData` sprawl (IDs 0–2315) and the
  thing the closed vocabulary exists to prevent.

⭐ **So a sixth class — provenance — earns its place by the repo's own stated test.** This is an
**ask-first change** against `ssot-materials-crafting.md` §3.1, and it must be filed and answered
before any code.

---

## ⛔ And the ban this must not trip

`ssot-materials-crafting.md` §3.4 **bans source-tagged ids** — the *"every recipe becomes a travel
itinerary"* failure it refused for zone-keyed materials.

⚠ **§3.4 carries TWO refusals and an earlier draft answered only one.** Both are owed:

**(a) The zone refusal** — answered below (gating + the general layer).

**(b) ⭐ The ROLE-axis refusal** — `ssot-materials-crafting.md:160` refuses a **role** axis:
*"Twelve roles × anything is the scavenger hunt."* **This proposes a SPECIES axis at 904 — two orders
of magnitude past the twelve that were refused.** `tier-system-ideal.md:990-992` calls this *"a
deliberate reversal of sealed reasoning"* that **must say so**, because *"a reversal that is not named
reads as a defect to the next reviewer."*

**Naming it: the reversal is sound because the two axes fail differently.** A role axis multiplies
**every** recipe by twelve — every craft becomes a scavenger hunt, at every rung. A species axis
multiplies only the **gated top** of a tree, is settable to **0 for general creatures**, and is
carried by a general layer beneath it. The refused shape had no gate and no floor; this one has both.

**(c) The SOURCE refusal** — `MaterialCatalog.cs:117-120` restates it in code (*"a source-tagged id
such as `essence.fire.pvz`"*). ⚠ **The SC8 mode-gate clearance is owed and is NOT free here:** a
species is reachable from expedition, delve and wild map — several modes, none required — **but only
if more than one of those selection modules ships.** If the gating species is reachable from exactly
one mode, that *is* a mode gate. **This module must name which selection modules must have landed
before its gated tier is legal.**

**Why a species material is not that, stated rather than assumed:**

1. **It is gated above a threshold rung** (`species-cost-shaping`). Early crafting stays generic, so
   no recipe becomes an errand until the player is deliberately chasing a top-tier piece.
2. **The general layer carries the volume.** The species layer is 1–2 ids per species, settable to
   **0 for general creatures** — the dial that bounds the count.
3. **MH's `Monster Solidbone` proves the generic layer must survive beside it either way**, and it
   does here.

This is the MH shape: **the rare per-monster part gates the top of a tree while common and generic
parts carry the early steps** — which is exactly what keeps low-tier creatures relevant.

---

## What exists today — verified

### Built

- **`MaterialCatalog.All` builds 27 ids from three id-shape tables** — `SubstrateFrames`
  (`humanoid`, `plant`), `SubstrateGrades` (`crude`, `sound`, `fine`, `prime`), `CatalystVerbs`
  (`forge`, `temper`, `flux`) — plus the ten rungs and six elements. **The catalog is generated from
  shape, not hand-listed**, so a new class with a declared shape extends it cleanly.
- **`MaterialCatalog.ClassOf` throws outside the closed set**, by design
  (`MaterialVocabularyRejection`).
- **`CostClassMatrix.Allows(op, cls)` throws `ArgumentOutOfRangeException` on an unrecognised class**
  — so a sixth class without an arm is a **hard failure, not a silent pass.** That is the correct
  posture and must be preserved.
- **A refusal rides one existing code.** `CostClassForbiddenRule = "material.cost-class-forbidden"`,
  *"raised as the one `ContentRuleViolated` code — **never a new member of the closed 33-code
  list**."* ⭐ **So this module adds no error code.**
- `shard.{rarity}` is minted per rung; `species-cost-shaping` already keys cost on a species' rung.

### Real gap

| Gap | What must be built |
|---|---|
| The sixth `MaterialClass` + its id shape | ⛔ **Ask-first** against §3.1 |
| An arm in `CostClassMatrix.Allows` | Which verbs may spend it. Without one, it throws |
| `data/tuning/creature-yield.v1.json` | Does not exist. ⚠ **Shared with `creature-drop-tables` — one file, never two** |
| Generation of the layers | The general layer and the thin species layer, through seedsmith |

---

## Design

### 1. Two layers, not three — the family layer is withdrawn

| Layer | Volume | Keyed on |
|---|---|---|
| **General** | Carries the bulk | Not species-specific |
| **Species-unique** | **1–2 per species, tunable — settable to 0 for general creatures** | The species |

⛔ **The family layer is withdrawn**, not deferred. D7 (lineage as the family key) was **disproven by
measurement** — 61–113 groups, **median 2**, 49 singletons, 272–333 species with no lineage, and a
cyclic DAG where 80% of species have multiple roots. It is not a grouping. The family layer existed
only to let the species layer stay thin; **the 1–2 cap does that job directly.**

### 2. Scope discipline, from the owner's own arithmetic

> MH affords ~10 materials per monster at ~100 monsters. **At 904 species that ratio is impossible.**

904 × 10 = 9,040 ids. So the general layer carries the volume and the species layer stays deliberately
thin. **1–2 per species, and 0 for general creatures**, is the dial that bounds the id count against
MH's own documented sprawl.

### 3. The escape valve, and the one that does not work

**Prior art settles both:**

- ✅ **MH's documented fix** for species-bound gear whose species is unavailable was to **make the
  species farmable** — never to invent a substitute. That is why this module follows the selection
  modules, and it is the whole reason the initiative is ordered the way it is.
- ✅ **Terraria's boss gear works because the encounter is the renewable resource.**
- ❌ **A trade-in shop does not solve the first-copy problem.** MH's own Elder Melder **requires you
  to already own one.**
- ✅ **If the tail still bites, the documented safety valve is the Artian shape** — a
  species-agnostic parallel path to an equivalent item. ⛔ **Never a wildcard material that quietly
  dissolves the binding.**

### 4. Orthogonality still binds

**Upgrade level and set membership stay independent.** No craft verb may break a set bonus, because
the bonus counts **membership**, not upgrade state.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items/Materials`), Python 3 (seedsmith generation), xUnit + pytest.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostClass"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~MaterialSpend"
dotnet run --project tools/ItemSeedValidator
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q -k material
python scripts/audit-overflow.py
```

## ⛔ Three live `27` pins that BREAK THE BUILD, not a test

`MaterialCatalog.All` widening is not caught by a red test — **two of the three pins are module-level
`assert`s that hard-crash seedsmith on import:**

| Pin | Effect when the vocabulary widens |
|---|---|
| `tools/seedsmith/.../items/materialgen/vocab.py:120` — `assert len(ISSUABLE) == 27` | ⛔ **ImportError-time crash** |
| `tools/seedsmith/.../items/materialgen/vocab.py:124` — `assert len(ISSUABLE_BY_ID) == 27` | ⛔ **ImportError-time crash** |
| `tools/seedsmith/tests/test_recipes_gen.py:193` — `assert len(pool) == 27` | red test |

⭐ **All three must be replaced IN THE SAME CHANGE**, with a **reconciliation canary** rather than a
new literal — `len(ISSUABLE) == len(MaterialCatalog.All)` — because under this module the id count
**grows with content**, and pinning it would convert a closed-vocabulary assertion into a pin on a
derived population (`validation-ssot.md`).

⚠ An earlier draft pinned 27 on the **C# side only** and named `tools/seedsmith/**` merely as
*"layer generation."*

### ⚠ And `materialgen` structurally refuses this ask today

`materialgen/__init__.py:1-10`, in its own words: it authors *"`name` / `flavor` / `tags` for a
material id — **never a new material id**."* And ownership is not seedsmith's to assume:
`materialgen` and `droptablegen` are owned by **`item-seedgen`** (module 3 `materials-gen`,
`item-seedgen-map.md:90`; module 10 `drop-tables-gen`, `:98`). **Both facts belong in the ask.**

---

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs` | The sixth class + its id shape |
| `src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs` | The `Allows` arm |
| `data/tuning/creature-yield.v1.json` | ⚠ Shared with `creature-drop-tables` — **one file** |
| `tools/seedsmith/**` | Layer generation |
| `docs/architecture/item/ssot-materials-crafting.md` §3.1 | The reviewed amendment |

## Code style

The sixth class carries its question in its doc comment, exactly as the five do — that comment *is*
the test it had to pass:

```csharp
/// <summary>"What did this come from?" — provenance. NONE of the other five asks this: Shard is a
/// rarity ceiling, Essence is element direction with no magnitude, Catalyst is the verb, and
/// Substrate is {frame}.{grade} at 8 ids — folding species into it would mean 904 x 4 = 3,616 ids,
/// which is MH's documented itemData sprawl (0-2315) exactly. Volume is bounded by the general
/// layer; this layer is 1-2 per species and 0 for general creatures.</summary>
Trophy,
```

And the `Allows` arm must be **narrow**, not permissive:

```csharp
// Only the verbs that IMPROVE a bound piece may spend a trophy, and only above the threshold rung
// (species-cost-shaping). A permissive arm here would reopen the "every recipe is a travel
// itinerary" failure §3.4 already refused.
MaterialClass.Trophy => op is CraftOperation.Elevate or CraftOperation.Temper,
```

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| **Species material count per species (1–2)** | The thin top layer. Settable to 1, or **0 for general creatures** — the dial that bounds the id count | `data/tuning/creature-yield.v1.json` |
| **`countGeneral`** (whole ids) | How much volume the general layer carries — what makes a 1–2 species layer sufficient | Same file |
| Species-tier → material grade/rung map | Which grade a creature of rung *n* yields (MH's `Scale` → `Scale+` → `Shard`) | Same file — ⚠ **shared with `creature-drop-tables`** |
| Which craft verbs demand a species material, and at what share of the cost | Whether the species leg is required on every enhance or only above a rung | `data/tuning/materials.v1.json` `operations` |
| Deterministic-exchange price — `exchangePriceSouls`, `exchangeTokensPerGrant` (whole units) | **D5**, which rides with this module. Variance insurance priced at ≈ the EV of the random path (PoE: 1/1500 vs flat 1500) | `materials.v1.json` `operations` |

⚠ **`creature-yield.v1.json` is a tuning file — `data/tuning/**` is hand-authored**, and **Core never
reads a file** (T7.2): the host loads and injects. Do **not** put it in the seedsmith mirror.

⚠ **If the exchange price is derived from `P(Θ)` / `contentScale` rather than flat, it is a cost
ladder and owes its own `ssot-power-scale.md` §10 row.**

**Structural (stays `const`, with a comment saying why):** the material class vocabulary itself.
Widening it is a **reviewed change** against §3.1, **not a tunable** — which is exactly why this
module's central act is an ask, not an edit.

## Numeric types

- Material quantities and costs are **`long`** — magnitudes on an endless-progression axis.
- **Widen before multiplying; divide by 1000 last, exactly once; overflow throws, never wraps.**
- **Never `float`** — integer-exactness fails at `Θ` = 232, inside normal play.
- Per-species **counts** (1–2) are small `int`s — a **content cardinality**, not a magnitude.
- ⛔ **The id count is bounded by a tunable, not by a cap on what a player may earn.** The 1–2 dial
  bounds how many *ids exist*; it never bounds how many a player may hold. **No hard progression
  ceiling.**

## ActorHub gate

**N/A and checked.** A material is an economy item. Spending one changes an item's affixes, which
compose through the existing equipment path into `ActorHub`. **Nothing here composes, contributes, or
folds.** `guard-actor-hub.ps1` stays green.

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit | ⭐ **`MaterialCatalog.All` is 27 + the new layer's declared shape**, computed from the shape tables — a **closed vocabulary**, so pinning is correct, and the test says so |
| Unit | `ClassOf` still **throws** outside the closed set |
| Unit | ⭐ **`CostClassMatrix.Allows` has an arm for the sixth class** — proven by asserting it does not throw, for every operation |
| Unit | The arm is **narrow**: verbs outside the improve set refuse a trophy line |
| Unit | A refusal rides `material.cost-class-forbidden` — ⭐ **no new error code**, and the closed 33-code list is unchanged |
| Unit | A species-bound piece at or above the threshold rung **requires** its own species' material |
| Unit | Below the threshold, and for species-less pieces, no trophy is required |
| Unit | Setting the per-species count to **0** for general creatures yields no ids for them |
| Unit | ⭐ **Orthogonality** — spending a trophy never changes set bonus evaluation |
| pytest | Generation is deterministic and idempotent; a second run is byte-identical |
| Contract | `ItemSeedValidator` green; every trophy id resolves; no id is source-tagged by **zone** |
| Report | Total id count by layer — **a reading, printed, never asserted** |

⛔ **No test asserts the total material id count as a growth target.** The 27 is a closed vocabulary
and is pinned; the *species layer's* size is a reading bounded by a tunable.

## Boundaries

**Always**
- **Fix the generator and regenerate.** Layer content is seedsmith output.
- Keep `ClassOf`'s and `Allows`' throwing behaviour.
- Keep the `Allows` arm narrow.
- Bound volume with the general layer, not with a cap on the player.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **The sixth `MaterialClass`.** This is the module's central act and it is ask-first against
  `ssot-materials-crafting.md` §3.1. **File it with the "which of the five is unanswerable?" argument
  above** — that is the form the boundary asks for.
- The per-species count and whether general creatures get zero.
- Creating `creature-yield.v1.json` if `creature-drop-tables` has not — **one file, never two.**
- The D5 exchange price, and whether it earns a §10 row.

**Never**
- ⛔ **A per-species material id for all 904 species** as the default. `ClassOf` throws outside the
  closed set by design, MH's `itemData` sprawl (0–2315) is the documented failure mode, and the vision
  forbids a fourth wallet.
- ⛔ **Source-tagged ids** in §3.4's sense — a **zone**-keyed id. The gating and the general layer are
  what keep a species id from becoming one.
- ⛔ **A wildcard material** that dissolves the binding. If the tail bites, the answer is the Artian
  shape: a species-agnostic parallel path to an *equivalent item*.
- A trade-in shop as the answer to unreachable species — the Elder Melder requires you to already own
  one.
- Add a new `ContentRuleViolated` code. The closed 33-code list is unchanged.
- Reinstate the family layer. It is **withdrawn**, disproven by measurement.
- Let a craft verb break or re-check a set bonus.
- Assert a population count.

## Success criteria

1. The sixth `MaterialClass` is added under a **filed, answered** ask, with its question stated in its
   doc comment.
2. `CostClassMatrix.Allows` has a narrow arm; no operation throws; verbs outside the improve set
   refuse.
3. `MaterialCatalog.All` is generated from declared shapes, and `ClassOf` still throws outside the set.
4. ⭐ **A species-bound set piece above the threshold rung is enhanced with its own species'
   material**, end to end — the decision this whole initiative exists to deliver.
5. Below the threshold and for species-less pieces, nothing changes.
6. Per-species count is tunable in 1–2 and settable to 0 for general creatures; the general layer
   carries the volume.
7. No new error code; the closed 33-code list is unchanged.
8. Set bonus evaluation is provably unchanged.
9. One `creature-yield.v1.json`; `ItemSeedValidator`, Core, Data and pytest suites green.

## Open questions

1. ⭐ **How many species materials per species, and do general creatures get zero?** The decided band
   is **1–2 and tunable**. **Recommendation: 1 for unique species, 0 for general** — it is the
   smallest configuration that delivers the decision, and it keeps the id count at roughly the count
   of unique species rather than 904.
2. **Which verbs may spend a trophy?** **Recommendation: `Elevate` and `Temper` only** — the two
   *improve* verbs. Reroll is re-randomisation, not improvement, and socket work is module 16's.
3. *(Not an open question — it was already decided, and an earlier draft reversed it silently.)*
   **D5's deterministic exchange ships WITH the first tier content.** `tier-system-ideal.md` D5:
   *"A deterministic exchange ships **with** the first tier content (**undisputed**)"*, and
   § DISPOSITION: *"D5 — deterministic exchange | **Rides with D2**."* The map repeats it.
   ⚠ An earlier draft recommended shipping materials first and the exchange later, **without naming
   that it was reversing a settled, undisputed decision.** The decision stands: **D5 rides with this
   module.** If the EV-pricing concern is real it is a reason to *file a reversal*, not to quietly
   re-open it in an open-questions list.
4. **Is the sixth class named `Trophy` or something else?** A naming call, but not a trivial one: the
   name is what the next reader uses to decide whether *their* spend belongs in it. **Recommendation:
   `Trophy`** — it names the provenance question rather than the material's physical nature, which is
   the distinction that keeps it out of `Substrate`.
