# Lane I9 — materials, crafting, and the cost vocabulary

**Status:** Lane I9 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

> **Boundary cut 7:** this lane owns **the cost vocabulary that I4, I6 and I7 spend in**. Sections 1 and 5
> are the artefact those lanes are waiting on. Everything else in this document exists to make those two
> sections defensible.

---

## 1. Scope

### This lane owns

- **The cost vocabulary** — the closed set of currencies and material classes every other lane expresses
  a price in, and the rule that keeps any two of them from being interchangeable (§3.1).
- **The material taxonomy** — how many material ids exist, what axes they vary on, and what they are named.
- **Crafting** — creating an item from nothing, and the one conversion recipe between materials.
- **Salvage** — the yield function that turns unwanted gear back into materials.
- **Faucets and sinks** — where each class enters the economy and where it leaves.
- **The recipe row shape** and the spend transaction: validation, refusal strings, correlation idempotency.
- **The reference cost table** (§7.4) — the numbers other lanes start from.

### This lane does NOT own

| Thing | Owner |
|---|---|
| What sockets *do*, how many an item has, insert typing | **I4** |
| What enhancement *does*, and the instance-mutation model every operation adopts | **I6** (contract cut 6) |
| What reroll *does* to an affix | **I7** |
| The rarity ladder, its rungs and ordinals | **I1** — I register a band mapping against it (§3.3) |
| Base types, their frames, their implicits | **I3** — forge recipes reference them, never define them |
| The affix pool and tier bands | **I8** — salvage *reads* affix count and element |
| Drop tables and the loot pipeline | **I12** — I say what drops; they own how |
| Storage UI, bag layout, have/need display | **I13** — §8 is a recommendation to them, not a decision |
| Soul earn policy, ledger, watermarks | **shipped and locked** ([spec-soul-economy.md](../demons/spec-soul-economy.md)) — I extend, never redesign |

**Tuning boundary.** Other lanes may tune the *quantities* in §7.4 for their own operation. They may not
introduce a class, split one, or spend a class §3.2 forbids their operation. That is enforced, not asked
(`CostClassForbidden`, §6).

---

## 2. The model

Five things can be spent. One is a currency; four are material classes. Each answers a **different
question** about an operation, which is why none of them substitutes for another:

| Spend | The question it answers |
|---|---|
| **Souls** | *May I act at all?* — the flat fee on every operation |
| **Substrate** | *What is it made of?* — the physical body, frame-specific, graded by item level |
| **Shard** | *How good may it be?* — the rarity ceiling |
| **Essence** | *What flavour?* — the element direction, no magnitude |
| **Catalyst** | *What am I doing to it?* — the verb: make, improve, or re-randomise |

A price is therefore a sentence: *"120 souls, 12 fine heartwood, 2 rare shards, 2 tempers"* reads as
*I paid the fee, I supplied the body, I bought the rarity ceiling, and I performed two improvements.*
A player who cannot afford it knows immediately **which** thing they are short of and **what content**
produces it. That legibility is the whole point of the vocabulary; the closed set is what protects it.

**Souls already exist and already work.** `rpg_soul_ledger` + `rpg_soul_balances`, append-only with a
watermarked projection, `TrySpendSouls(playerId, amount, reason, correlationId)` atomic under the store
gate, correlation-idempotent, no negative balances ever
([spec-soul-economy.md](../demons/spec-soul-economy.md)). I add a spend reason and nothing else. The
materials half already exists too: `rpg_demon_materials(player_id, material_id, qty, updated_utc)` with PK
`(player_id, material_id)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:520-526`), seeded by expeditions
(`RpgStore.Expeditions.cs:232`) and spent by fusion (`RpgStore.Fusion.cs:393-397`). Twenty-one material ids
is a *widening of that catalog*, not a new subsystem.

**Crafting creates bases, never power.** The forge hands you the base type you want at Normal rarity with
zero rolled affixes. Every point of power still comes from loot or from I6/I7 operating on an item you
already have. That single rule is what keeps a crafting system from replacing the game it decorates.

**Salvage is a converter, not a faucet.** It moves value *between* classes at a strict loss and returns
zero of the classes that gate progress. It is the disposal route for trash, and it is the only one — there
is no vendor and no gold.

---

## 3. The cost vocabulary — the artefact

### 3.1 The five spends

**This is the table other lanes cite.** Names here are final; quantities are not.

| # | Class | Id shape | Count | Purpose — the question it answers | Storage | Interchangeable with |
|---|---|---|---|---|---|---|
| 1 | **Souls** | *(no id — a ledger balance)* | — | **Permission.** A flat fee on every operation. Universal, fungible, earned by playing anything | `rpg_soul_ledger` / `rpg_soul_balances` | nothing — it is the only fungible thing in the game |
| 2 | **Substrate** | `substrate.{frame}.{grade}` | 8 | **Body.** What the item is physically made of. Frame-locked, graded by item level | materials table | nothing — frame and grade are both hard gates |
| 3 | **Shard** | `shard.{band}` | 4 | **Ceiling.** The rarity band an operation may reach | materials table | nothing — you cannot buy a ceiling with volume |
| 4 | **Essence** | `essence.{element}` | 6 | **Direction.** Which element an outcome takes. Carries no magnitude | materials table | nothing — an element is not a quantity |
| 5 | **Catalyst** | `catalyst.{verb}` | 3 | **Verb.** Which operation you are performing: make / improve / re-randomise | materials table | nothing — see §3.2 |

**Twenty-one material ids, plus souls.** The full list:

```
essence.fire   essence.ice   essence.air   essence.earth   essence.light   essence.dark
shard.common   shard.rare    shard.epic    shard.legendary
substrate.humanoid.crude   substrate.humanoid.sound   substrate.humanoid.fine   substrate.humanoid.prime
substrate.plant.crude      substrate.plant.sound      substrate.plant.fine      substrate.plant.prime
catalyst.forge   catalyst.temper   catalyst.flux
```

The first ten already exist. `DemonMaterialCatalog.Build()` generates `essence.{element}` over
`ElementRoster.Concrete` — **six elements, `omni` deliberately absent** — and `shard.{rarity}` over the
four `DemonRarity` values (`src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:13-21`). I am **appending
eleven ids and renaming nothing.** Fusion's shipped cost table keeps working byte-identically.

### 3.2 The three catalysts, and why exactly three

A catalyst is the *verb*. Three verbs, and each lane's operations map onto exactly one:

| Catalyst | Verb | Operations that spend it | Owning lanes |
|---|---|---|---|
| `catalyst.forge` | **make** — bring new matter into existence | craft a base type · craft a gem · bore a socket into an item | I9, I4 |
| `catalyst.temper` | **improve** — make the existing thing better along an axis it already has | enhancement (+n → +n+1) · rarity elevation | I6 |
| `catalyst.flux` | **re-randomise** — keep the thing, redraw its values | reroll one affix · reroll all affixes | I7 |

Boring a socket rides on `forge` because boring adds a hole where none was — it is creation. I4 paces
sockets through *quantity*, not through a private currency, which means socket work and base crafting
genuinely compete. That competition is a decision the player makes; a fourth catalyst would remove it.

### 3.3 Registered with I1 — bands, not rungs

**OD4 says the rarity ladder is long and overlapping.** If `shard.{rarity}` had one id per rung, a
twelve-rung ladder would mint twelve shard ids and every recipe would become a rung-specific scavenger
hunt. So:

> **Shards are keyed on a *band*, not a rung.** There are four bands, and they are the four ids already
> shipped: `common` · `rare` · `epic` · `legendary`.

**Request to I1 (three items):**

1. Add a **`material_band`** column to the `rarity` table, mapping every rung to exactly one of the four
   band ids. It is not nullable and it is not inferred from ordinal ranges — inferred boundaries drift the
   moment a rung is inserted.
2. Band assignment is **append-only in the same sense ordinals are**: a rung's band may never change after
   release, because a changed band silently re-prices every recipe that references it.
3. Adding a **fifth band** is a reviewed change (a new material id, a new shipped row, a new content-hash
   digest). Adding a *rung* inside an existing band is free. That asymmetry is deliberate: it makes the
   long ladder cheap and the ceiling expensive, which is the correct incentive.

**Do materials have rarity themselves? No.** Rarity in this tree means *affix count plus tier window*
([definitions.md](../effect-atom/definitions.md) §4, and `effect_container.min_tier`/`max_tier`). A
material rolls nothing, has no affixes, and has no tier window, so hanging the rarity ladder off it would
be a category error and would make the `rarity` FK mean two incompatible things. Two of the four classes
*reference* a rarity concept — shard names a band, substrate names a grade — and neither *is* rarity.

### 3.4 The axes materials vary on, and the ones they do not

| Axis | Varies? | Which class | Why |
|---|---|---|---|
| **Element** | yes | essence | six elements are a shipped, generated roster; element is the one flavour axis the atom layer already understands |
| **Rarity band** | yes | shard | the ceiling has to be purchasable in bands or the ladder is unreachable |
| **Frame** | yes | substrate | plant fibre is not corpse matter. Makes frame a real commitment (§3.5) |
| **Item-level grade** | yes | substrate | the content gate — the thing volume cannot buy (§5.3) |
| **Operation verb** | yes | catalyst | three verbs, §3.2 |
| **Role** (helm vs sword) | **no** | — | role differentiation belongs in the *recipe's quantities*, not in a new id. Twelve roles × anything is the scavenger hunt |
| **Source** (expedition vs battle vs PvZ) | **no** | — | **hard rule.** A source-tagged id would let PvZ gate something, violating SC8. Nothing is ever `essence.fire.pvz` |
| **Zone / sector** | **no** | — | Destiny shipped per-planet materials and removed them; recalled, unverified. Per-zone ids make every recipe a travel itinerary |
| **Grade on essence** | **no** | — | see §3.5 |

**Why 21 is the right number.** A recipe's cost is at most **four material lines plus a souls fee**: one
substrate, one shard, one essence, one catalyst. Every line answers a different question, so no line is
ever redundant and no line is ever ambiguous. Twenty-one ids fit one inventory screen with no paging, and
a player learns the whole vocabulary in one session because it is five ideas, not twenty-one facts.

Fewer would be flavourless: collapse substrate's frame axis and plant and zombie gear cost the same dust,
which erases OD1's whole point. More would be a scavenger hunt: the moment a recipe needs five lines a
player stops reading it and opens a wiki.

### 3.5 Two deliberate non-symmetries

**Essence has no grade.** A fire build farms `essence.fire` once and never re-farms it at a higher tier.
Quantity scales with the *operation's* band, not with the essence. This removes an entire category of
tedium — the "your level-20 mats are worthless now" treadmill — at the cost of essence being slightly
inflationary, which §7.3 accepts and prices.

**Substrate is frame-locked and there is no hybrid substrate.** A hybrid base type is authored as
humanoid-framed *or* plant-framed (I3) and eats that frame's substrate. Consequences worth naming:

- A pure-frame player farms **one** substrate line and never wastes a salvage.
- A hybrid can forge from **whichever line they have more of** — real flexibility, and it is the
  compensation OD3 asks for when hybrids give up slots. Breadth is paid for in depth on the slot axis and
  refunded on the material axis. That is a coherent trade rather than two unrelated penalties.

---

## 4. Options considered, and the recommendation

### 4.1 Crafting as creation versus crafting as modification

| Option | What it is | For | Against |
|---|---|---|---|
| **A — modification only** | No item is ever made from nothing. Materials only feed I4/I6/I7 | Loot stays unambiguously king. Smallest surface. No risk of the forge replacing the game | The most common real frustration — *"I have played forty hours and never seen a plant off-hand drop"* — has no answer. Base-type variety becomes a drop-rate problem instead of a design one |
| **B — creation with rolled outcome** | A recipe names a base type; rarity and affixes roll on completion | Deterministic access to bases *and* a jackpot moment. Very sticky | This is a second loot table with a materials price tag. It competes directly with drops, and the moment it is efficient nobody plays the content that drops things. Also strains SC5 twice — a crafted item is a rolled instance with no drop event behind it |
| **C — creation of Normal bases only** ✅ | A recipe names a base type. Output is **Normal rarity, `pool_rolls = 0`, implicit only**. All rarity and all affixes come from I6/I7 afterwards | Solves the base-type frustration completely. Cannot compete with loot, because it produces the weakest possible version of the item. Every subsequent point of power routes through an existing lane with an existing cost. Uses `effect_container` exactly as shipped — a forged base is a container with no pool rows, which the schema already calls "a plain fixed list" ([spec-container-schema.md](../effect-atom/spec-container-schema.md)) | A crafted item still needs a long, expensive climb to be usable, so the forge alone never feels like a win. Accepted — the forge is a *targeting* tool, not a reward |

**Pick: C.** *The forge sells bases, not power.*

The load-bearing consequence: **there is no "craft a rare" recipe.** Reaching Magic or Rare is an
**elevation**, which is an instance mutation, which is **I6's operation** under contract cut 6. I price it
(§7.4); I6 defines what it does and how the recorded-operation list keeps SC5's reproducibility intact.

### 4.2 How many catalysts

| Option | Shape | Verdict |
|---|---|---|
| **One universal catalyst** | Every operation spends `catalyst.essence-of-work` | Rejected. A single reagent on every operation is just souls with an inventory row. It carries no information, so a player short of it learns nothing about what to go do |
| **Four — one per operation** | forge · temper · flux · bore | Rejected as currency bloat. Four parallel budgets means the player never chooses between operations, and `bore` in particular produces the "I have flux, I need bore" dead-end with no decision attached to it |
| **Three — one per verb** ✅ | make · improve · re-randomise | **Picked.** Three genuinely different questions. Socket-boring rides on `forge` (both add matter), so I4 competes with base crafting — a real decision. I6 and I7 stay separate because "make this stronger" and "make this different" are the two things a player agonises over, and collapsing them would delete the agony rather than resolve it |

### 4.3 Does a vendor exist — is salvage ever better than selling?

**There is no vendor, no gold, and no sell price. I recommend it stays that way.** Verified: no gold or
coin currency exists anywhere in `FusionRpg.Core`; souls are the only currency and they are ledger-only.

The argument, in one step: a sell price makes items mint currency. Souls already occupy the universal-fee
role and their earn policy is carefully capped — 50 counted kills per match, victory decay after three
wins per UTC day ([spec-soul-economy.md](../demons/spec-soul-economy.md)). If gear could be sold for souls,
**gear farming would out-earn playing**, and every one of those caps would be routed around by an item
system that was never balanced against them. Introducing a second currency (gold) instead just moves the
inflation into a closed loop whose only source and only sink are items.

So the answer to *"is salvage ever better than selling?"* is: **salvage is the only route, by design.**
What that costs us is the classic "grey item → a bit of coin" trickle. What it buys is that the soul
economy stays the one economy, which [decisions.md](../decisions.md)'s one-economy rule and the fusion
spec's ledger discipline both already depend on.

### 4.4 Salvage yield — formula versus table

A per-item authored salvage table (one row per base type) was considered and rejected: it is `N` base types
of authoring for zero decisions, and it drifts the instant I3 adds a base. A **formula in code with its
coefficients in a small table** is the shipped pattern here (`SoulEarnPolicy` is a code policy with golden
tests; `power_trigger_frequency` is a hashed coefficient table precisely so a sweep can propose against it,
[definitions.md](../effect-atom/definitions.md) §7). Salvage follows the second shape (§5).

---

## 5. Salvage

### 5.1 The yield function

`SalvagePolicy.Yield(item) → CostLine[]`, pure, integer-only, in `FusionRpg.Core`. Inputs are all readable
off a frozen instance plus its container.

```
band      = rarity.material_band of the item's rarity rung        // 1 common … 4 legendary
grade     = 1 + min(3, itemLevel / 25)                            // 1 crude … 4 prime, integer division
affixes   = count of drawn atoms on the instance                  // I8's number
elemental = count of drawn atoms whose variant is a concrete element
enh       = the item's enhancement level                          // I6's number, 0 if never enhanced

substrate.{frame}.{grade}  ×  substrateBase[band] + affixes
essence.{element}          ×  min(essenceCap[band], elemental)     per distinct element present
shard.{band - 1}           ×  shardBack[band]                      // never the item's own band
catalyst.temper            ×  enh / 3                              // integer division
souls                      ×  0                                    // salvage never mints currency
```

`salvage_coefficient` — four rows, one per band, content-hashed:

| band | `substrateBase` | `essenceCap` | `shardBack` (of band−1) |
|---|---|---|---|
| 1 common | 2 | 0 | 0 |
| 2 rare | 4 | 2 | 1 |
| 3 epic | 6 | 3 | 2 |
| 4 legendary | 9 | 3 | 3 |

### 5.2 The two rules that make it safe

**R1 — the band-1 rule.** Salvage returns a shard of the band **below** the item's own, never its own.
Rarity always flows downhill through recycling. You can never bootstrap a rarity ceiling by feeding the
grinder its own output, and commons return no shard at all.

**R2 — the strict-loss invariant.** *For every class a recipe spends, salvaging that recipe's output
returns strictly less of that class.* This is a **property test over the whole recipe table**, not a
design intention — see §6.3. It is what makes "crafting is not a perpetual motion machine" a fact rather
than a hope.

Note what R2 does **not** say: salvage may return a class the recipe never spent. Elemental affixes return
essence even though elevation costs none. That is deliberate — salvaging elemental gear is the mid-game
essence faucet that feeds I4's gem crafting — and §8.2 walks the loop to show it cannot be farmed.

### 5.3 The grade lock — why farming trash cannot beat farming bosses

`grade` is a function of **item level**, and item level is a function of the content that dropped the item.
A level-10 zone returns `substrate.*.crude` forever, at any volume. `substrate.*.prime` requires item level
75+, which requires content that drops item level 75+. **Volume cannot substitute for difficulty on the
grade axis**, and the upcycle recipe that could break this is capped one grade short of the top (§7.4).

The three classes that gate progress are covered as follows:

| Class | Salvage returns | Therefore |
|---|---|---|
| `catalyst.forge` | **never** | the crafting loop can never sustain itself |
| `catalyst.flux` | **never** | rerolling is always content-funded |
| `catalyst.temper` | only what enhancement already paid in (`enh / 3`, and enhancement costs ≥ 1 per level) | strictly lossy, always |
| `shard.{band}` | only band−1 | ceilings are never recycled |
| `substrate` | generously | **deliberate** — substrate is the cheap class, the one a player should never hesitate to spend |

---

## 6. Data shape — recipes as data under SC7

### 6.1 `material_recipe`

Content table. Registered into the covered-table registry ([definitions.md](../effect-atom/definitions.md)
§8) with a `contentHashSchemaVersion` bump.

| Column | Type | Notes |
|---|---|---|
| `recipe_id` | TEXT PK | `^recipe\.(forge\|upcycle\|elevate\|temper\|reroll\|bore\|socket)\.[a-z0-9]+(-[a-z0-9]+)*$` — prefix must match `operation`, the same derived-id discipline as `atom_id` (§1 of definitions) |
| `operation` | TEXT | **closed enum**, seven verbs: `forge` `upcycle` `elevate` `temper` `reroll` `bore` `socket`. Not content — adding one is code |
| `output_kind` | TEXT | `container` (forge a base or gem) · `material` (upcycle) · `mutation` (everything I4/I6/I7 do — the recipe prices it, the owning lane performs it) |
| `output_ref` | TEXT | `container_id` · `material_id` · `''` for mutations |
| `output_qty` | INT | ≥ 1 |
| `frame` | TEXT | `humanoid` \| `plant` \| `any` |
| `grade_req` | INT | 0 = none; else the minimum substrate grade the target must be at |
| `band_req` | INT | 0 = none; else the minimum rarity band of the target |
| `level_req` | INT | nullable; mirrors `effect_container.level_req` and rejects with the **existing** `LevelTooLow` |
| `souls_cost` | INT | ≥ 0 |
| `discover` | INT | `0` always visible; `1` hidden until first success. Fusion's discovery pattern (`rpg_fusion_discovery`) is the precedent and is reused wholesale |
| `enabled`, `revision` | INT | joins the content hash |

### 6.2 `material_recipe_cost`

| Column | Type | Notes |
|---|---|---|
| `recipe_id` | TEXT FK | |
| `seq` | INT | stable display order **and** the canonical row order for the content hash. `DuplicateSeq` on collision |
| `material_id` | TEXT | must pass `MaterialCatalog.IsKnown`, or `UnknownMaterial`. May be a **template** when `variant_from` is set: `shard.*` / `essence.*` / `substrate.{frame}.*` |
| `qty` | INT | ≥ 1, else `BadParamValue` |
| `qty_curve_id` | TEXT | nullable; FK to the shipped `effect_curve`. **Restricted to `input: rarity` and `input: tier`.** `input: level` reads the owning actor's level and a recipe has no actor — reject with the existing `ScopeUnsupported`. Applied as `qty' = max(1, ceil(qty × multiplierMilli / 1000))` — **always ceiling**, so no curve can ever make a cost free |
| `variant_from` | TEXT | `''` · `output-element` · `output-band` · `target-frame`. Resolves the template at spend time |

`variant_from` exists to move a string concatenation out of code and into data. Fusion builds its essence
id in C# today — `("essence." + resultElementId, cost.EssenceCount)`
(`src/FusionRpg.Data/Sqlite/RpgStore.Fusion.cs:378-381`) — which means the element half of every fusion cost
is invisible to the content hash and unchangeable without a rebuild. Recipes must not repeat that.

### 6.3 Who consumes these tables — SC7's named-consumer requirement

| Table | Consumer | What breaks if it has no consumer |
|---|---|---|
| `material_recipe` | `MaterialRecipeCatalog` (Core, pure) — loads, validates at startup, exposes `Resolve(recipeId, ctx) → CostLine[]` | it becomes `status.expose.*` — a registered, valid, fully-hashed table with zero readers |
| `material_recipe_cost` | same, via `Resolve` | same |
| `salvage_coefficient` | `SalvagePolicy.Yield` | same |
| all three | `RpgStore.Materials.TrySpendRecipe` (execute) and the FE cost panel (preview) — the `#/fusion` have/need panel is the shipped precedent | — |

**The SC7 line, stated exactly.** Adding a new base type's forge recipe is **one `material_recipe` row plus
two or three `material_recipe_cost` rows and no code.** Adding a new *operation verb* is **code**, because a
new verb needs an executor and a lane that owns it. That is the line, and it is where SC7 puts it.

**Not data, deliberately:** the salvage *formula* (a table would need one row per band × item-level ×
affix-count combination), the operation semantics, and any cost that scales with **enhancement level** —
`enhancement_level` is not a legal `effect_curve` input and adding one is an ask-first change against E2.
I6 keeps that in its own coefficient table; see §9.4.

### 6.4 Storage recommendation to I13

`rpg_demon_materials` has **no demon-shaped column** — it is `(player_id, material_id, qty, updated_utc)`
with PK `(player_id, material_id)` (`RpgStore.cs:520-526`). The name is the only thing demon-specific about
it.

| Option | Verdict |
|---|---|
| **A — parallel `rpg_item_materials` table** | **Rejected.** Two tables holding the same shape means two spend paths, two idempotency stories, and the inevitable day a recipe needs a shard from one and an essence from the other |
| **B — keep the name, widen the catalog** | Zero migration, permanently misleading name, and every future reader has to be told that `rpg_demon_materials` is not about demons |
| **C — rename in place** ✅ | `ALTER TABLE rpg_demon_materials RENAME TO rpg_materials`, then repoint **four** SQL sites: `RpgStore.Expeditions.cs:232`, `RpgStore.Expeditions.cs:252`, `RpgStore.Fusion.cs:395`, and the reset path at `RpgStore.cs:612`. Grep-verified — that is the complete list |

**Recommend C**, with two additions:

- `CHECK (qty >= 0)`. The shipped spend is already safe (`WHERE qty >= $q`, `Fusion.cs:394-397`), but the
  check is free defence in depth and makes a hand-edited database fail loudly.
- `DemonMaterialCatalog` → `MaterialCatalog`, same `IsKnown` gate, same throw-on-unknown at the write
  boundary (`Fusion.cs:390-391`). Keep the old type as a one-line alias if anything outside these files
  references it.

**No stack cap and no bag slots for materials.** `qty` is INTEGER (64-bit). Materials must never compete
with gear for inventory space — the same principle [item-ideal.md](../item-ideal.md) §7 applies to quest
items. Twenty-one rows is a shelf, not a bag.

**No material ledger.** Souls need one because the balance is a *watermarked projection* that must be
rebuildable after a trim. Materials store the balance directly with no projection, so a ledger would buy
audit history and nothing else — at expedition-collect volume. What is actually required is idempotency,
and §6.5 gets that from a spend log at a fraction of the rows. The earn side needs nothing: expedition
collect is already state-and-correlation idempotent, so a re-collect adds no materials today.

### 6.5 The spend transaction

`TrySpendRecipe(playerId, recipeId, context, correlationId)` — one gate-serialised store transaction,
copying `ExecuteFusion`'s discipline exactly ([spec-demon-fusion.md](../demons/spec-demon-fusion.md)).

```
1. replay check   — rpg_material_spend_log, UNIQUE(player_id, correlation_id)
                    hit ⇒ return the stored outcome, spend nothing
2. resolve        — MaterialRecipeCatalog.Resolve(recipeId, ctx) → ordered CostLine[]
3. gate           — frame, grade_req, band_req, level_req, discover
4. spend, in a FIXED class order: souls → shard → substrate → essence → catalyst
5. perform        — the owning lane's mutation or mint, inside the same transaction
6. log            — spend log row with the resolved cost lines and the outcome ref
```

Four properties, each inherited rather than invented:

- **Fixed class order** (step 4). A partial failure always fails at the same point, so two logs of the same
  refusal are byte-comparable, and the spend log's canonical form is stable.
- **Souls leg** goes through `AppendSoulLedgerUnlocked` with the recipe correlation as the dedupe key, and
  **throws** on a dedupe collision rather than proceeding — copied from `Fusion.cs:384-386`, where a reused
  correlation outside the fusion log is treated as a bug, not a duplicate.
- **Material legs** use the shipped conditional update, `UPDATE … SET qty = qty - $q WHERE player_id = $p
  AND material_id = $m AND qty >= $q` (`Fusion.cs:393-397`); a zero row count fails the whole transaction
  and rolls earlier decrements back.
- **Refusals write nothing**, so a retried refusal re-evaluates. A replayed *success* returns the original
  outcome without spending again. That is the soul spec's corrected idempotency contract verbatim, and
  matching it is the point.

---

## 7. Validation, reason codes, and the numbers

### 7.1 Two error surfaces, because this tree already has two

Do not unify these. They are different audiences at different times.

**Authoring / import — atom reason codes**, PascalCase, [definitions.md](../effect-atom/definitions.md)
§10. Import is all-or-nothing (E14): one bad recipe row and nothing imports.

| Bad input | Code | New? |
|---|---|---|
| `material_id` not in `MaterialCatalog` | `UnknownMaterial` | **new** |
| Cost or gate references a missing recipe | `UnknownRecipe` | **new** |
| `operation` outside the seven-verb enum | `UnknownOperation` | **new** |
| Recipe spends a class its operation may not (e.g. `forge` spending `catalyst.temper`) | `CostClassForbidden` | **new** — this is the guard that keeps §3.1 meaningful |
| `variant_from = output-element` but the operation supplies no element | `UnresolvedVariant` | **new** |
| `recipe_id` prefix disagrees with `operation` | `IdMismatch` | reuse |
| `qty ≤ 0`, `output_qty ≤ 0`, `souls_cost < 0` | `BadParamValue` | reuse |
| Two cost lines with the same `seq` | `DuplicateSeq` | reuse |
| `output_kind = container` and `output_ref` resolves to nothing | `UnknownContainer` | reuse |
| `qty_curve_id` points at a missing or malformed curve | `BadCurve` | reuse |
| `qty_curve_id` uses `input: level` | `ScopeUnsupported` | reuse |
| Resolved qty overflows `int` after a curve | `MagnitudeOverflow` | reuse |
| Two recipes with the same `recipe_id` | `DuplicateKey` | reuse |

Five new codes against thirty-three existing. Each is an author mistake the existing list genuinely cannot
name, and `CostClassForbidden` is the one that makes the vocabulary enforceable rather than advisory.

**Player action — the shipped dotted-lowercase strings.** Souls refuse with `souls.insufficient` (409) and
fusion with `materials.insufficient` (`RpgStore.Fusion.cs:376, :379`); fusion preview already returns
`recipe.unknown` for an undiscovered recipe. Reuse all three.

| Refusal | String |
|---|---|
| Not enough souls | `souls.insufficient` *(shipped)* |
| Not enough of any material | `materials.insufficient` *(shipped)* |
| Recipe not discovered | `recipe.unknown` *(shipped)* |
| Wrong frame for the target | `frame.mismatch` |
| Target below `grade_req` | `grade.insufficient` |
| Target below `band_req` | `band.insufficient` |
| Target is locked, retired, or on an expedition | `target.unavailable` |
| Correlation reused with different arguments | `correlation.conflict` |

`materials.insufficient` deliberately does **not** name which material. The FE preview panel already shows
have/need per line before the player commits, so the refusal only has to be true; naming the line in the
error would duplicate a surface that is already better.

### 7.2 Startup and property validation

| Check | When |
|---|---|
| Every `material_id` and template resolves against `MaterialCatalog` | catalog load, fail fast — the `DemonRecipeCatalog` discipline |
| Every `output_ref` container exists and is `enabled` | catalog load |
| `operation` ↔ allowed cost classes | catalog load (`CostClassForbidden`) |
| Every rarity rung has a `material_band` | catalog load, cross-checked against I1's table |
| **Strict-loss invariant (R2)** | property test over the whole recipe table |
| Recipe determinism: two builds of the catalog are byte-identical | golden test, the fusion catalog's precedent |
| Spend atomicity: forced mid-sequence failure leaves zero rows across materials, souls ledger, and spend log | Data.Tests, the `ExecuteFusion` forced-failure test's shape |

### 7.3 Faucets, sinks, and the bottleneck

**Faucets.** Standalone-first (SC8) is satisfied class by class: every class has at least two faucets that
work with the game closed.

| Class | Expeditions | Web battles | Salvage | World sectors | PvZ play |
|---|---|---|---|---|---|
| souls | ✅ shipped | ✅ shipped | **never** | later | ✅ enriches |
| `essence.{element}` | ✅ shipped | ✅ | ✅ small | `essence-deposit` slot | ✅ |
| `shard.{band}` | ✅ shipped | boss/final wave only | band−1 only | `shard-vein` slot | ✅ |
| `substrate.{frame}.{grade}` | ✅ | ✅ | ✅ **primary** | `material-seam` slot | ✅ |
| `catalyst.forge` | tier-gated, low | boss only | **never** | `vault` / `shrine` — see §9.7 | ✅ capped |
| `catalyst.temper` | ✅ | ✅ | `enh / 3` only | `vault` / `shrine` | ✅ capped |
| `catalyst.flux` | 8 h + 20 h tiers only | boss only | **never** | `vault` / `shrine` | ✅ capped |

Three of the four classes already have a **shipped world faucet named in shipped design**:
`SlotTypeCatalog` lists `essence-deposit`, `shard-vein`, and `material-seam` among its slot kinds
([spec-world-model.md](../world/spec-world-model.md):22). That is not a coincidence to design around — it is
the world lane having already reserved the right holes.

**The PvZ rule, stated so it cannot drift:** PvZ play pays in the **same ids** at a rate **no better** than
the equivalent web path, and never has an exclusive id. The injector enriches; it never gates (SC8).

**Sinks.** Forge a base · upcycle substrate · elevate rarity · temper (enhancement) · reroll one · reroll
all · bore a socket · socket an insert · forge a gem. Nine sinks, three lanes, one vocabulary.

**The balance argument.** Salvage is a converter, not a faucet, so the only *true* faucets are content. That
means the economy's inflation risk concentrates in whichever class has the highest-volume faucet **and**
receives salvage output — which is **substrate**, and that is deliberate. Substrate is the class a player
should never hesitate to spend, the one that makes the forge feel generous, and the one whose surplus the
upcycle recipe drains.

**The bottleneck is `catalyst.forge` and `catalyst.flux`, deliberately**, because they are the only classes
with **no salvage faucet at all**. The player's rate of *making* and *re-randomising* is therefore pinned
directly to content completed, and cannot be accelerated by inventory management. `shard.legendary` is the
second bottleneck by construction: band-4 content is its only source and R2 forbids recycling into it.

If substrate inflates past usefulness anyway, the drain valve is the upcycle recipe's ratio — a data edit,
not a redesign.

### 7.4 The reference cost table

**Illustrative, not balanced.** Souls are counts against the shipped ledger; material quantities are counts
of rows. `b` = rarity band 1–4, `g` = substrate grade 1–4, `n` = current enhancement level.

| Operation | Owner | Souls | Substrate | Shard | Essence | Catalyst |
|---|---|---|---|---|---|---|
| **forge** a Normal base at grade `g` | I9 | 40 × g | 4 × g of `substrate.{frame}.{g}` | — | — | 1 `forge` |
| **upcycle** substrate `g` → `g+1`, **`g ≤ 2` only** | I9 | 20 × g | 5 of grade `g` → 1 of grade `g+1` | — | — | — |
| **forge a gem** at band `b`, element `e` | I4 | 30 × b | — | 1 `shard.{b}` | 3 × b `essence.{e}` | 1 `forge` |
| **bore** a socket into a band-`b` item | I4 | 50 × b | 3 × b | — | — | 1 `forge` |
| **socket** an insert into an open socket | I4 | 10 | — | — | — | — |
| **elevate** rarity to band `b` | I6 | 60 × b | 2 × b | `b` × `shard.{b}` | — | `b` `temper` |
| **temper** `+n` → `+n+1` | I6 | 15 × (n+1) | 1 × (n+1) | — | — | `ceil((n+1)/3)` `temper` |
| **reroll** one affix on a band-`b` item | I7 | 80 × b | — | — | 2 × b of the affix's element, if it has one | 1 `flux` |
| **reroll** all affixes | I7 | 200 × b | — | 1 `shard.{b}` | — | `b` `flux` |

Notes that are rules, not commentary:

- **Socketing an insert costs 10 souls and nothing else.** Moving a gem you already own between sockets must
  never be a material decision, or players stop experimenting and the socket system dies quietly.
- **Essence is spent only when the operation names an element.** There is no `essence.omni` — `omni` is a
  channel slot, not a material (`DemonMaterialCatalog.cs:16-17` builds over `ElementRoster.Concrete`,
  six ids). A non-elemental reroll spends no essence.
- **Upcycle is capped at `g ≤ 2`** (`crude → sound → fine`). `prime` has **no** upcycle path. Without that
  cap, volume-farming low-level content would manufacture top-grade substrate and break the grade lock
  (§5.3) — which is exactly the failure the cap exists to prevent.

### 7.5 Three worked examples

**Example 1 — forge a plant off-hand base, grade 2.**
The player wants a `thorn` (armament-secondary, plant frame, [item-ideal.md](../item-ideal.md) §5.1) and has
never seen one drop. Recipe `recipe.forge.thorn-briar`:

| Line | Resolved |
|---|---|
| souls | 40 × 2 = **80** |
| `substrate.plant.sound` | 4 × 2 = **8** |
| `catalyst.forge` | **1** |

Output: one `effect_container` instance, base type `thorn-briar`, **Normal** rarity, `pool_rolls = 0`,
carrying only its implicit. Not a good item. It is the *right* item, which is the whole product.

**Example 2 — salvage a level-60 epic humanoid chest.**
Band 3, item level 60 ⇒ grade `1 + min(3, 60/25) = 3`, 5 drawn affixes of which 3 carry a concrete element
(2 fire, 1 dark), enhancement `+7`.

| Line | Arithmetic | Yield |
|---|---|---|
| `substrate.humanoid.fine` | `substrateBase[3] = 6` + `affixes = 5` | **11** |
| `essence.fire` | `min(essenceCap[3] = 3, 2)` | **2** |
| `essence.dark` | `min(3, 1)` | **1** |
| `shard.rare` | `shardBack[3] = 2`, of band **2**, never band 3 | **2** |
| `catalyst.temper` | `7 / 3` | **2** |
| souls | always | **0** |

**Example 3 — elevate to Epic, and what the data actually looks like.**
Elevation is one recipe row for all four bands, because `variant_from` and a rarity curve carry the band.
This is the shape working, not an illustration of it:

```
material_recipe
  recipe_id  = recipe.elevate.rarity
  operation  = elevate
  output_kind= mutation      output_ref = ''      output_qty = 1
  frame      = any           grade_req  = 0       band_req = 0
  souls_cost = 60            discover   = 0

material_recipe_cost
  seq=0  material_id=substrate.{frame}.*  qty=2  qty_curve_id=curve.band-linear  variant_from=target-frame
  seq=1  material_id=shard.*              qty=1  qty_curve_id=curve.band-linear  variant_from=output-band
  seq=2  material_id=catalyst.temper      qty=1  qty_curve_id=curve.band-linear  variant_from=''
```

`curve.band-linear` is `[[1,1000],[2,2000],[3,3000],[4,4000]]` with `input: rarity` — the shipped curve
mechanism, reading the container's rarity **ordinal** ([definitions.md](../effect-atom/definitions.md) §2).
Elevating a plant-frame item to Epic (`b = 3`) resolves to **180 souls · 6 `substrate.plant.{grade}` ·
3 `shard.epic` · 3 `catalyst.temper`**. Four rows of data cover the whole ladder, and adding a fifth band
later is one curve point and one shard id.

---

## 8. Failure modes

### 8.1 Currency bloat — "a material for every purpose, so nothing is ever the right one"

**Where it shipped:** Diablo 3 at its worst carried gold, blood shards, forgotten souls, death's breaths,
five crafting reagents and per-set materials simultaneously; several were later removed. Destiny shipped
per-planet materials and deleted them wholesale. *(Both recalled, unverified.)* The pattern is identical:
each material was added to solve one designer's pacing problem, and the aggregate was a screen of things
the player could not rank.

**What prevents it here:** the vocabulary is **five spends, closed**, and each answers a structurally
different question (§3.1). A lane that wants a sixth must say which of the five questions is
unanswerable — a conversation, not a row. `CostClassForbidden` makes the mapping from verb to spendable
class **enforced at import**, so the vocabulary cannot erode one recipe at a time.

### 8.2 Salvage yields so good that farming trash beats farming bosses

**Where it shipped:** every ARPG that let recycling return its own input tier. PoE's early vendor recipes
made chromatic farming strictly better than mapping for a while; recalled, unverified.

**What prevents it:** three independent locks — the **band-1 rule** (R2 §5.2), the **grade lock** (§5.3),
and **catalysts having no salvage faucet** (§7.3). Any one of the three would be circumventable; together
they are not.

**The loop, walked rather than asserted.** Suppose a player tries to farm essence by manufacturing gear:

| Step | Cost | Return |
|---|---|---|
| forge a grade-3 base | 120 souls, 12 `substrate.*.fine`, 1 `forge` | — |
| elevate to Rare (`b=2`) | 120 souls, 4 substrate, 2 `shard.rare`, 2 `temper` | — |
| salvage the result (2 affixes, ≤2 elemental, enh 0) | — | `substrateBase[2] = 4` + `affixes = 2` = **6** substrate, 1 `shard.common`, ≤2 essence, 0 `temper` |

Net: **−240 souls, −10 substrate, −2 `shard.rare`, −1 `catalyst.forge`, −2 `catalyst.temper`, +1
`shard.common`, +≤2 essence.** The player burned a forge catalyst and two rare shards to manufacture at most
two essence, which a single expedition tick produces for free. The loop is not merely unprofitable; it is
absurd, which is the correct margin for a loop nobody should have to be warned about.

### 8.3 Recipes that are a wiki lookup rather than a decision

**Where it shipped:** FF14's crafting trees, where a single item is a five-deep intermediate chain and the
"decision" is following a list; recalled, unverified.

**What prevents it:** a hard shape constraint — **at most four material lines plus a souls fee**, one per
class, and **exactly one conversion recipe in the entire system** (upcycle), capped at grade 3. There are no
intermediate crafted components. A recipe is never a chain, so there is never a plan to look up. The
have/need panel that fusion already ships (`#/fusion` cost panel) is the display precedent, and it means the
recipe is legible in the app at the moment of decision.

### 8.4 The socket-insert dead end

**Where it shipped:** any game where re-socketing costs a scarce reagent. Players stop experimenting, the
socket system collapses into "install the obvious gem once", and I4's whole lane becomes a stat tax.

**What prevents it:** socketing an insert costs **10 souls and nothing else** (§7.4). Only *boring* a socket
and *making* a gem are gated. Experimentation is free; capacity is scarce.

### 8.5 The forge replacing the game

**What prevents it:** §4.1's pick. Forged output is Normal rarity with `pool_rolls = 0`. A crafted item is
by construction the weakest legal version of itself, so the forge can never be a better source of power
than the content that drops rolled items.

### 8.6 A sink that quietly becomes a faucet

The single most dangerous failure in a crafting economy, and the one that is caught by testing rather than
by design. **R2 is a property test over the entire recipe table** (§7.2), evaluated for every recipe and
every craftable output, so a future recipe that inverts cannot land quietly — it fails CI at authoring
time, which is the only moment the fix is cheap.

### 8.7 Rebalancing the economy retroactively

Cost tuning changes `material_recipe_cost` rows, which are content-hashed, which moves the content hash and
therefore every stamped report keyed on it ([definitions.md](../effect-atom/definitions.md) §8). That is
correct — a re-priced economy **should** be a different catalog revision — but it means cost tuning is a
content release, not a hotfix. Named here so nobody discovers it during a balance pass.

---

## 9. What this lane needs from other lanes

1. **I1 — the band mapping.** A `material_band` column on the `rarity` table, non-nullable, one of four
   shipped band ids, append-only in the same sense ordinals are (§3.3). Without it, a long ladder mints one
   shard id per rung and every recipe becomes a scavenger hunt. **This is the single hardest dependency in
   this lane.**
2. **I1 — confirm the four bands are enough.** If the ladder tops out above `legendary`, I need a fifth band
   id, which is a shipped-material addition and a content-hash schema bump. Better to know now.
3. **I3 — base types must declare `frame` and a substrate `grade`.** Forge recipes should be *generated*
   from the base-type catalog (the `DemonRecipeCatalog` pattern: code-derived, validated at startup), not
   hand-authored one per base. That is only possible if the base type carries both fields.
4. **I6 — two things.** (a) Confirm **rarity elevation is your operation**, since §4.1 routes all rarity
   gain through you. (b) `enhancement_level` must be readable off a frozen instance, because §5.1 reads it.
   And a warning: if your cost must scale continuously with enhancement level, that is **not** expressible
   as an `effect_curve` — `level`, `rarity` and `tier` are the only legal inputs, and `input: level` reads
   an actor that a recipe does not have. Keep a coefficient table or raise an ask-first change against E2.
5. **I7 — the flux rate is a shared budget.** `catalyst.flux` has no salvage faucet by design, so your
   reroll cadence *is* the drop rate of a single material. If your intended cadence is "several per session",
   say so — that is a faucet change, not a cost change.
6. **I4 — three answers.** (a) Confirm gems are **containers** (`container_kind = 'gem'`, reserved in SC3),
   not materials — they carry atoms, and a material never does. (b) Your socket-boring competes with base
   crafting for `catalyst.forge`; if that competition is wrong for your pacing, argue for a fourth catalyst
   now rather than after the vocabulary is published. (c) Confirm re-socketing is free (§8.4).
7. **I8 — salvage reads two of your numbers:** the count of drawn affixes, and how many carry a concrete
   element variant. Both must be derivable from the frozen instance without re-rolling anything.
8. **I12 — materials drop through your pipeline.** I say *what* drops and at which band (§7.3); you own the
   weighted table, the seed, and the event. Two constraints from here: no material id may be source-tagged,
   and `catalyst.forge` / `catalyst.flux` must be weighted to hard content only.
9. **I13 — storage.** §6.4 recommends renaming `rpg_demon_materials` to `rpg_materials` (four SQL sites),
   adding `CHECK (qty >= 0)`, and giving materials a shelf rather than bag slots. The have/need cost panel is
   yours to build; `#/fusion` is the shipped model.
10. **I5 — can set pieces be salvaged, and can a set base be forged?** My position: **forged bases are never
    set bases** (sets are drop-only, or the forge becomes a set vending machine), and set pieces salvage
    normally. Yours to confirm.
11. **I10 — are charms salvageable?** If a charm is a container carried unequipped, §5.1 works on it
    unchanged. If it is something else, tell me what it yields.
12. **I11 — no dependency.** The forge does not consult the equip gate; a player may craft a base they cannot
    yet wear. Stated so the absence is deliberate rather than an oversight.
13. **The world-map lane — `SlotTypeCatalog` has no catalyst-yielding kind.** `essence-deposit`,
    `shard-vein`, and `material-seam` cover three of my four classes ([spec-world-model.md](../world/spec-world-model.md):22).
    `vault` and `shrine` are the natural homes for catalysts, but that is a **request, not an assumption**.
14. **E9 / power (SC9).** I would want a power number to price a recipe against — *"this elevation adds ~N
    budget points, so it should cost ~N × k"*. It does not exist and this lane ships without it: §7.4 is
    hand-set and marked illustrative. When E9 lands, the reference table is the first thing that should be
    re-derived rather than re-guessed.

---

## 10. Open questions for the owner

1. **Should items ever convert to souls?** §4.3 recommends **never** — a sell price would let gear farming
   route around the soul earn caps. This is the one decision in this lane that deletes a feature players
   expect, so it should be made deliberately rather than inherited from a doc.
2. **Upcycle capped at grade 3?** §7.4 caps it to protect the grade lock. The alternative — allowing
   `fine → prime` at a punishing ratio (10:1, plus a catalyst) — is more generous and more inflationary.
   Not decided here.
3. **Three catalysts or four?** §4.2 picks three, putting socket-boring in competition with base crafting.
   If I4 needs independent pacing, `catalyst.bore` is a clean addition — but it must be added **before**
   the vocabulary is published, not after.
4. **Is there a crafting skill?** A commander crafting level gating which grades you may forge is a cheap,
   legible progression axis and this lane deliberately did not design one. It would also give the forge a
   reason to be used at every stage rather than only when a base is missing.
5. **Does PvZ play pay materials at all, or only souls?** SC8 permits materials at a non-superior rate.
   Paying only souls is simpler and completely removes the "is the injector the best farm?" question.
6. **Bad-luck protection on catalysts?** The summon pity counters are the in-tree precedent. A player who
   goes four sessions without a `catalyst.flux` has a bad time that no other part of this design produces.
7. **The `rpg_demon_materials` rename** (§6.4) touches a shipped table and four shipped SQL sites. Cheap and
   grep-verified, but it is a shipped-schema migration and wants the owner's word before anyone runs it.
8. **Roster-scale.** [item-ideal.md](../item-ideal.md) §8 flags that twenty demons × twelve slots is 240
   equipped items, and says the answer must land before slot counts freeze. It also decides this lane's
   volumes: if every specimen is geared, salvage input is enormous and §7.4's quantities are an order of
   magnitude out. **The cost table cannot be tuned until that is answered** — it is currently priced for a
   small deployable squad.

---

## Appendix — design-gate checklist

Required by [DESIGN-GATE.md](../../DESIGN-GATE.md) §5. Not one of the contract's ten sections.

```
[x] I identified the subsystem(s) this touches — effect-atom (container/pool/curve/content-hash),
    soul economy, demon fusion, expeditions, world model slot types, item lanes I1/I3/I4/I6/I7/I8/I12/I13.
[x] I read every required-reading doc named in the contract §5 and my lane brief, this session.
[x] I checked decisions.md's standalone-first and one-economy locks — neither forbids this design;
    §7.3's PvZ rule and §4.3's no-vendor pick are written to satisfy both.
[x] Every factual claim about the repo cites file:line or a doc.
[x] I verified against CODE, not comments — rpg_demon_materials DDL (RpgStore.cs:520-526), the four
    call sites, the conditional-UPDATE spend (Fusion.cs:393-397), the essence string concat
    (Fusion.cs:378-381), the six-element roster (DemonMaterialCatalog.cs:16-17, ActorElementTypes.cs:21-29),
    and the absence of any gold/coin currency in Core.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no suite was run for this document.**
    The salvage arithmetic in §7.5 and §8.2 is hand-computed, not executed. R2 (§5.2) is specified as a
    property test and is not yet written.
[x] Nothing contradicts a §2 invariant of the enrichment contract — no new atom kind, no second
    modifier mechanism, no float in content, SC5 mutation deferred to I6, every table names its consumer.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item map, plan, or task list exists yet.** Reconciliation into item-ideal.md happens in the
    single pass after all lanes land, per the contract's parent-intent rule.
```
