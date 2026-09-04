# Spec: `salvage-craft`

**Module id:** `salvage-craft` · **Program:** [item](../item-map.md) · **Build order:** 14 of 21
**Depends on:** `armoury` (module 2), `rarity-bands` (module 7)
**Lane:** [ssot-materials-crafting.md](ssot-materials-crafting.md) (I9) · **Rulings:** D7, D23, D24, **D26**, D29

## Objective

The **cost vocabulary** every other sink spends in, plus the two operations that own it end to end:
**crafting** a base from nothing and **salvaging** an unwanted one back into materials. This is the
first sink and the cheapest — modules 15 and 16 express every price in the vocabulary this module
publishes, and neither can be built against a moving one.

Three artefacts, in dependency order: the closed five-spend vocabulary (§1), the recipe/cost tables and
the spend transaction (§4), and the reference cost table extended to **ten** operations — I9 §7.4's nine
plus D24's `socket.imbue`, which has no row anywhere today (§3).

## Design

### ⛔ D26 is the design constraint, not a footnote

**Every cost input is a property of the target. None is a property of the player.**

| Legal cost input | Illegal, and refused by a test |
|---|---|
| the target's rarity rung (ordinal) | the player's `Θ`, level, or power index |
| the target's tier / affix tier | how many items the player owns |
| the target's frame, role, item level | wall-clock time, a daily counter, a session counter |
| the operation verb | how many operations the player ran today |

A t5 affix costs more than a t1 **at every `Θ`**. Content pacing belongs to the world map and the battle
engine ([item-map.md](../item-map.md) §6). This is stated first because it is the one rule that a cost
table drifts away from silently: a coefficient keyed on player power reads exactly like one keyed on
target power until someone opens the file.

### ⛔ Platform correction — shards are already **per-rung**, and there are ten

I9 §3.3 is the lane's hardest stated dependency: *"Shards are keyed on a band, not a rung. There are
four bands, and they are the four ids already shipped."* **Verified false as of today.**

| Lane claim | Verified |
|---|---|
| four shard ids ship | **Ten.** `DemonMaterialCatalog.Build()` iterates `DemonRarityLadder.All` (`src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:19-27`), and `DemonRarityLadder.RungCount = 10` (`src/FusionRpg.Core/Demons/DemonRarityLadder.cs:11`, `:51`) |
| the four band ids are `common/rare/epic/legendary` | Those are the **legacy** ids — resolvable by `IsKnown`, never minted (`DemonMaterialCatalog.cs:30-38`), migrated forward by `Migrations/ShardRungs.cs` |
| I1 must add a `material_band` column to `rarity` | The `rarity` table is `rarity_id · ordinal · prefix_rolls · suffix_rolls · min_tier · max_tier` (`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:54-61`). No band column, and **the table has zero rows** — the live ten rungs are the C# enum `DemonRarity` (`src/FusionRpg.Core/Demons/DemonRarity.cs:18-27`), which is what `DemonMaterialCatalog` reads |

> **Decision: the four-band request is withdrawn. Shards stay per-rung at ten, and `material_band`
> is not asked for.** Collapsing ten shipped, migrated ids back into four is a schema migration bought
> for a scavenger-hunt worry the platform already resolved a different way. Recipes name the **target's
> own rung** through `variant_from = output-rung`, so a recipe is never a hunt across rungs — it is one
> id, determined by the thing you are working on.

Consequence: I9's *"single hardest dependency in this lane"* no longer exists. Module 7 `rarity-bands`
seeds the `rarity` table's rows; this module reads `DemonRarityLadder` for material ids and the seeded
`rarity.ordinal` for curve input. **⛔ **CORRECTED 2026-09-04 — costs key on the RUNG INDEX 0–9, which is *not* `rarity.ordinal`.** `rarity.ordinal` is **10…100** (`_registry/core.v1.json`), and `spec-rarity-bands.md:307` makes writing an enum member index into it a **Never**. Reading `rarity.ordinal` into `b` would make every cost row and every salvage coefficient wrong by **10×**. The field is `TargetRungIndex`, derived via `DemonRarityLadder`, band-linear becomes rung-linear.**

### The five spends — the artefact modules 15 and 16 cite

| # | Class | Id shape | Count | The question it answers | Storage |
|---|---|---|---|---|---|
| 1 | **Souls** | *(ledger balance)* | — | *May I act at all?* — the flat fee | `rpg_soul_ledger` / `rpg_soul_balances` |
| 2 | **Substrate** | `substrate.{frame}.{grade}` | 8 | *What is it made of?* — frame-locked, graded by item level | `rpg_demon_materials` |
| 3 | **Shard** | `shard.{rung}` | **10** | *How good may it be?* — the rarity ceiling | same, **already shipped** |
| 4 | **Essence** | `essence.{element}` | 6 | *What flavour?* — element direction, no magnitude | same, **already shipped** |
| 5 | **Catalyst** | `catalyst.{verb}` | 3 | *What am I doing to it?* — make / improve / re-randomise | same |

**Twenty-seven ids.** Sixteen ship today (`essence.*` ×6, `shard.*` ×10); this module appends eleven
(`substrate.*` ×8, `catalyst.*` ×3) and renames nothing.

**The three catalysts, and which operations may spend which** — the matrix `CostClassForbidden`
enforces at import, which is what makes the vocabulary enforceable rather than advisory:

| Catalyst | Verb | Operations |
|---|---|---|
| `catalyst.forge` | **make** | `forge` · `forge-gem` · `bore` · **`imbue`** |
| `catalyst.temper` | **improve** | `temper` · `elevate` |
| `catalyst.flux` | **re-randomise** | `reroll-one` · `reroll-all` |

`socket.imbue` rides `forge` because imbuing declares what a hole *is* — it is the same act of bringing
matter into existence that boring the hole was, and D24 prices it on the same curve as `bore` anyway
([item-ideal.md](../item-ideal.md) §2f.2, D24 row).

### `operation` ids come from module 15, not from here

`ssot-enhancement.md` §5.3 owns the `op_kind` namespace that I4, I7 and I9 all draw from. **A recipe's
`operation` is not a second vocabulary** — it is the subset of `op_kind` that has a price:

| `material_recipe.operation` | Module 15's `op_kind` | Performed by |
|---|---|---|
| `forge` · `upcycle` · `forge-gem` | *(none — mints, does not mutate)* | this module |
| `elevate` · `temper` | `enhance` | 15 |
| `reroll-one` · `reroll-all` | `reroll-value` / `reroll-affix` | 15 |
| `bore` · `socket` · **`imbue`** | `socket-add` / `socket-insert` / **`socket-imbue`** | 16 |

**`socket-imbue` is a new `op_kind` and it is module 15's to add**, not this module's — same review as
adding a `container_kind`. Named here so it is not invented twice.

### ⚠ The reference cost table, re-issued with `socket.imbue`

`b` = the target's rarity **ordinal + 1** (1–10) · `g` = substrate grade 1–4 · `n` = current enhancement
level. **Illustrative, not balanced** — every quantity lives in `data/tuning/materials.v1.json`.

| Operation | Owner | Souls | Substrate | Shard | Essence | Catalyst |
|---|---|---|---|---|---|---|
| **forge** a base at grade `g` | 14 | 40 × g | 4 × g of `substrate.{frame}.{g}` | — | — | 1 `forge` |
| **upcycle** `g` → `g+1`, **`g ≤ 2` only** | 14 | 20 × g | 5 of grade `g` → 1 of `g+1` | — | — | — |
| **forge-gem** at rung `b`, element `e` | 16 | 30 × b | — | 1 `shard.{rung}` | 3 × b `essence.{e}` | 1 `forge` |
| **bore** a socket | 16 | 50 × b | 3 × b | — | — | 1 `forge` |
| ⭐ **imbue** a crafted socket's affinity | 16 | **50 × b** | **3 × b** | — | **2 × b `essence.{e}`** | **1 `forge`** |
| **socket** an insert | 16 | 10 | — | — | — | — |
| **elevate** rarity to rung `b` | 15 | 60 × b | 2 × b | `b` × `shard.{rung}` | — | `b` `temper` |
| **temper** `+n` → `+n+1` | 15 | 15 × (n+1) | 1 × (n+1) | — | — | `ceil((n+1)/3)` `temper` |
| **reroll-one** affix | 15 | 80 × b | — | — | 2 × b of the affix's element | 1 `flux` |
| **reroll-all** affixes | 15 | 200 × b | — | 1 `shard.{rung}` | `b` `flux` | |

**`imbue` is band-linear like `bore`, plus one essence line** — the essence names *which* element the
socket becomes, and essence is the class whose whole job is direction without magnitude (§1 row 4). The
souls and substrate legs are `bore`'s verbatim, per D24's ruling that it prices on the same curve.

Rules, not commentary:

- **Socketing an insert costs 10 souls and nothing else.** Moving a gem you already own must never be a
  material decision (I9 §8.4).
- **Essence is spent only when the operation names an element.** There is no `essence.omni` —
  `ElementRoster.Concrete` is six ids and `omni` is not one of them.
- **Upcycle caps at `g ≤ 2`.** Without it, volume-farming low-level content manufactures top-grade
  substrate and breaks the grade lock. **This cap is structural, not a progression ceiling** — it bounds
  a *conversion ratio*, and the comment must say so (AGENTS.md's caps rule, exempt category "bounded
  ratios").

### Salvage — a converter, not a faucet

`SalvagePolicy.Yield(instance, container) → CostLine[]`, pure, integer-only, in `FusionRpg.Core`.

```text
rung      = rarity.ordinal of the item's rung                    // 0..9
grade     = 1 + min(3, itemLevel / 25)                           // 1 crude .. 4 prime
affixes   = count of drawn atoms on the instance
elemental = count of drawn atoms whose variant is a concrete element
enh       = the item's enhancement level (module 15), 0 if never enhanced

substrate.{frame}.{grade}  x  substrateBase[rung] + affixes
essence.{element}          x  min(essenceCap[rung], elemental)   per distinct element present
shard.{rung - 1}           x  shardBack[rung]                    // NEVER the item's own rung
catalyst.temper            x  enh / 3
souls                      x  0                                  // salvage never mints currency
```

**R1 — the rung-1 rule.** Salvage returns a shard of the rung **below** the item's own. Rarity always
flows downhill; you can never bootstrap a ceiling by feeding the grinder its own output, and `chaff`
(ordinal 0) returns no shard. *(I9 §5.2's "band-1" restated on the ten-rung ladder.)*

**R2 — the strict-loss invariant.** *For every class a recipe spends, salvaging that recipe's output
returns strictly less of that class.* A **property test over the whole recipe table**, not a design
intention. It is what makes "crafting is not a perpetual motion machine" a fact.

R2 does **not** say salvage may only return classes the recipe spent — elemental affixes return essence
even though elevation costs none. That is the mid-game essence faucet feeding module 16's gem crafting,
and it is deliberate.

**The grade lock.** `grade` is a function of item level, and item level is a function of the content that
dropped the item. A level-10 zone returns `crude` forever, at any volume. **Volume cannot substitute for
difficulty on the grade axis** — and note this is *not* metering the player under D26: it is the salvage
output of a *low-level item* being low-level, a property of the target.

`salvage_coefficient` is **ten rows, one per rung**, in `data/tuning/materials.v1.json`. I9's four-row
table is stale for the same reason its four bands were; module 7 re-derives it against ten alongside
I12's drop weights and I6's caps ([item-map.md](../item-map.md) §4, module 7).

### The spend transaction — copied, not invented

`TrySpendRecipe(playerId, recipeId, context, correlationId)`, one gate-serialised store transaction:

```text
1. replay check   -- rpg_material_spend_log, UNIQUE(player_id, correlation_id)
                     hit => return the stored outcome, spend nothing
2. resolve        -- MaterialRecipeCatalog.Resolve(recipeId, ctx) -> ordered CostLine[]
3. gate           -- frame, grade_req, rung_req, level_req, discover
4. spend, FIXED class order: souls -> shard -> substrate -> essence -> catalyst
5. perform        -- the owning module's mutation or mint, in the SAME transaction
6. log            -- spend-log row with resolved cost lines and the outcome ref
```

Every property is inherited from a shipped path, and each citation is verified:

| Property | Shipped precedent |
|---|---|
| Replay returns the original outcome; a reused correlation with **different** arguments is refused, not replayed | `TrySpendSouls` (`src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs:189-213`) — returns `"replay"` on a match, `"correlation.mismatch"` on a differing amount |
| Refusals write nothing, so a retried refusal re-evaluates | same, `RpgStore.Souls.cs:186-187` (the doc comment states the contract the code keeps) |
| Material legs use a conditional decrement; a zero row count fails the whole transaction | `RpgStore.Fusion.cs:394-400` — `UPDATE … SET qty = qty - $q WHERE … AND qty >= $q` |
| An unknown material id **throws** at the write boundary rather than silently no-op | `RpgStore.Fusion.cs:391-392` — `DemonMaterialCatalog.IsKnown` guard |
| The souls leg throws on a dedupe collision outside its own log | `RpgStore.Fusion.cs:380-382` |

**Fixed class order matters** (step 4): a partial failure always fails at the same point, so two logs of
one refusal are byte-comparable.

### Recipes as data — the SC7 line

`material_recipe` and `material_recipe_cost` per I9 §6.1–6.2, with three changes forced by verification:

| I9 said | Now |
|---|---|
| `operation` is a **seven**-verb enum | **Ten** — `forge · upcycle · forge-gem · bore · imbue · socket · elevate · temper · reroll-one · reroll-all` |
| `variant_from` values include `output-band` | **`output-rung`** — bands are gone |
| `qty_curve_id` restricted to `input: rarity` and `input: tier` | ✅ **Correct and verified.** `CurveInput` is exactly `{ Level, Rarity, Tier }` (`src/FusionRpg.Core/Effects/Atoms/CurveTable.cs:4-9`); `input: level` reads an actor a recipe does not have → `ScopeUnsupported` |

**The SC7 line:** adding a base type's forge recipe is one `material_recipe` row plus two or three
`material_recipe_cost` rows and **no code**. Adding an *operation verb* is **code**, because a verb needs
an executor and a module that owns it.

### ⚠ `rpg_demon_materials` → `rpg_materials` is **ask-first and not scheduled**

I9 §10.7 flags it as *"a shipped-schema migration that wants the owner's word."* It is not in this
module's task list, and this module ships against the shipped name.

⚠ **And I9 §6.4's cost estimate is stale.** It claims *"four SQL sites — grep-verified, that is the
complete list."* Verified today: **nine**, in five files.

| File | Lines |
|---|---|
| `src/FusionRpg.Data/Sqlite/RpgStore.cs` | `573` (DDL), `697` (reset) |
| `src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs` | `232`, `252` |
| `src/FusionRpg.Data/Sqlite/RpgStore.Fusion.cs` | `395` |
| `src/FusionRpg.Data/Sqlite/Migrations/ShardRungs.cs` | `48`, `71`, `89` (+ doc comments at `11`, `16`, `18`) |

Also note the DDL is at `RpgStore.cs:573-579`, **not** `:520-526` as I9 cites.

✅ **The rename is RULED (confirmed 2026-09-04):** `rpg_demon_materials` → `rpg_materials` proceeds.
⚠ **Nine** SQL sites across five files — the table above — not the four I9 §6.4 calls *"the complete
list"*. `Migrations/ShardRungs.cs` post-dates the lane, which is how the count drifted.

### Overflow

`qty` is SQLite `INTEGER` (64-bit) and every material quantity, souls amount and salvage yield is
**`long` in C#**. Widen before multiplying (`(long)qty * multiplierMilli`, never `(long)(qty * mult)`),
divide by 1000 last and exactly once, and let overflow throw. ⚠ Note `ContentScale.Apply` returns `int`
(`src/FusionRpg.Core/Power/ContentScale.cs:24`) — that is an existing A3 audit target on the *instance*
path and this module must not copy it onto the cost path.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Salvage"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~SpendRecipe"
.\scripts\guard-dal.ps1                              # SQL only inside FusionRpg.Data
python scripts\audit-magic-numbers.py --targets M1   # no bare literals in the cost tables
python scripts\audit-overflow.py                     # long on every magnitude
```

## Project structure

```text
src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs      SHIPPED - widen All(), do not rewrite.
                                                        Ten shard ids already correct
src/FusionRpg.Core/Items/MaterialCatalog.cs            new - the 27-id closed vocabulary + IsKnown,
                                                        wrapping DemonMaterialCatalog for the shipped 16
src/FusionRpg.Core/Items/MaterialRecipeCatalog.cs      new - load, validate at startup, Resolve(...)
src/FusionRpg.Core/Items/SalvagePolicy.cs              new - pure Yield(), integer-only
src/FusionRpg.Core/Items/CostClassMatrix.cs            new - operation -> spendable classes
src/FusionRpg.Data/Sqlite/RpgStore.Materials.cs        new - DDL + TrySpendRecipe (guard-dal)
data/tuning/materials.v1.json                          new - every quantity, every coefficient
data/seed/recipes/*.json                               new - recipe + cost rows (content)
tests/FusionRpg.Core.Tests/Items/SalvagePolicyTests.cs new
tests/FusionRpg.Data.Tests/MaterialSpendTests.cs       new - atomicity + idempotency
```

## Code style

```csharp
// D26: every cost input is a property of the TARGET. A term reading the player - theta, level,
// power index, a daily counter - is a metering rule wearing a pricing rule's clothes, and this
// program does not meter the player (item-map.md section 6). Enforced by CostInputGuardTests,
// not by review: the context type simply has nowhere to put a player stat.
public readonly record struct RecipeContext(
    int TargetRarityOrdinal,   // 0..9, the rarity table's own ordinal
    int TargetTier,            // 1..5
    int TargetItemLevel,
    string TargetFrame,
    int EnhanceLevel);         // module 15's number, read off the frozen instance

// Widen before multiplying, divide by 1000 last and exactly once. `(long)(qty * milli)` binds the
// cast to the RESULT - the multiply has already overflowed by then (CLAUDE.md, rule 3).
static long Scale(long qty, int multiplierMilli) =>
    checked(Math.Max(1, (qty * multiplierMilli + 999) / 1000));   // ceiling: no curve makes a cost free
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_vocabulary_is_twenty_seven_closed_ids` | five classes, no sixth, no source-tagged id |
| `ten_shard_ids_exist_and_four_legacy_ids_resolve_but_are_never_minted` | the verified platform state, pinned against regression |
| `no_cost_input_reads_a_player_property` | **D26**, mechanically — `RecipeContext` exposes no player field, and the resolver takes no other argument |
| `a_t5_affix_costs_more_than_a_t1_at_every_theta` | D26's positive half: cost rises with the *target*, and `Θ` is not an input at all |
| `cost_class_forbidden_rejects_a_forge_spending_temper` | the matrix is enforced at import, not asked |
| `socket_imbue_has_a_cost_row_and_prices_like_bore` | ⚠ the gap D24 left — the souls and substrate legs are `bore`'s verbatim |
| `strict_loss_holds_for_every_recipe_in_the_table` | **R2**, as a property test over the whole table, not a spot check |
| `salvage_returns_a_shard_of_the_rung_below_never_its_own` | **R1** on the ten-rung ladder |
| `chaff_salvage_returns_no_shard` | R1's bottom edge |
| `salvage_never_returns_catalyst_forge_or_catalyst_flux` | the two bottleneck classes have no salvage faucet, by construction |
| `upcycle_is_capped_at_grade_two_and_the_cap_is_a_bounded_ratio` | the grade lock, and the comment that says why the cap is exempt |
| `a_replayed_correlation_returns_the_original_outcome_and_spends_nothing` | copied from `RpgStore.Souls.cs:203-212` |
| `a_reused_correlation_with_different_arguments_is_refused` | `correlation.mismatch`, not a silent replay |
| `a_forced_mid_sequence_failure_leaves_zero_rows_across_all_three_stores` | materials, souls ledger, spend log — the `ExecuteFusion` forced-failure shape |
| `spend_order_is_souls_shard_substrate_essence_catalyst` | fixed order, so two refusal logs are byte-comparable |
| `an_unknown_material_id_throws_at_the_write_boundary` | `DemonMaterialCatalog.IsKnown`, `RpgStore.Fusion.cs:391` |
| `two_builds_of_the_recipe_catalog_are_byte_identical` | the fusion-catalog golden precedent |
| `a_quantity_curve_can_never_resolve_a_cost_to_zero` | ceiling, always |
| `every_material_quantity_is_long_and_overflow_throws` | no `int` magnitude on the cost path |

## Boundaries

**Always:** price on the target's properties; resolve costs through `MaterialRecipeCatalog`, never
inline; spend in fixed class order inside one transaction; return the recorded outcome on a replayed
correlation; write nothing on a refusal; keep every quantity in `data/tuning/materials.v1.json`.

**Ask first:** a **sixth spend class** — a lane wanting one must say which of the five questions is
unanswerable; ✅ ~~the `rpg_demon_materials` → `rpg_materials` rename~~ — **confirmed 2026-09-04 and
scheduled** (⚠ nine SQL sites across five files); a fourth catalyst; adding an `operation` verb (it needs an executor and an owning module).

**Never:** a cost term that reads the player's `Θ`, level, power index, item count, or any wall-clock or
per-day counter (**D26**). Never a source-tagged material id (`essence.fire.pvz`) — the injector
enriches, it never gates. Never mint souls from salvage. Never define an `op_kind` here — module 15 owns
that namespace (`ssot-enhancement.md` §5.3). Never let salvage return `catalyst.forge` or
`catalyst.flux`.

## Success criteria

- [ ] Twenty-seven material ids, five classes, closed — with the ten shipped `shard.*` rungs reused, not
      re-minted, and the four legacy ids still resolvable.
- [ ] **No cost input is a player property**, proven mechanically by the context type's shape and by test.
- [ ] `socket.imbue` has a cost row on `bore`'s curve — the gap D24 left is closed.
- [ ] R2 (strict loss) is a property test over the **whole** recipe table and it is green.
- [ ] R1 returns rung−1 on the ten-rung ladder; `chaff` returns no shard.
- [ ] A replayed spend correlation returns the original outcome and spends nothing; a differing one is
      refused — both proven against a real store.
- [ ] A forced mid-sequence failure leaves zero rows in materials, the souls ledger and the spend log.
- [ ] Every quantity lives in `data/tuning/materials.v1.json`; `audit-magic-numbers.py` reports no M1
      target in `MaterialRecipeCatalog`, `SalvagePolicy` or `CostClassMatrix`.
- [ ] Every magnitude on the cost path is `long`, widened before multiplying, divided by 1000 once.
- [ ] The `rpg_demon_materials` rename is **not** in the task list, and the nine SQL sites are recorded
      for the day the owner says go.
