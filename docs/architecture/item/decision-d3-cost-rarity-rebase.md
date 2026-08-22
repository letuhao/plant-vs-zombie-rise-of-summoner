# Decision D3 — the cost and rarity rebase

**Status:** R2 debate deliverable, decided 2026-08-22. Settles the three interlocking conflicts the
thirteen-lane enrichment round left behind. Bound by
[enrichment-contract.md](enrichment-contract.md); scheduled by
[reconciliation-plan.md](reconciliation-plan.md) §R2.

Read this session, in full: [reconciliation-plan.md](reconciliation-plan.md),
[enrichment-contract.md](enrichment-contract.md), [ssot-rarity.md](ssot-rarity.md),
[ssot-materials-crafting.md](ssot-materials-crafting.md), [ssot-enhancement.md](ssot-enhancement.md),
[ssot-reroll.md](ssot-reroll.md), [ssot-generation.md](ssot-generation.md),
[../effect-atom/definitions.md](../effect-atom/definitions.md) §2 and §4,
[../effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md).

Code opened: `CurveTable.cs`, `ContainerRow.cs`, `AtomCompiler.cs`, `AtomRowValidator.cs`,
`Instantiator.cs`, `BindGate.cs`, `ContentHashRegistry.cs`, `RpgStore.Containers.cs`,
`RpgStore.AtomInstances.cs`, `DemonMaterialCatalog.cs`, `DemonRarity.cs`, `RpgStore.Fusion.cs`,
`ExpeditionResolver.cs`, `FusionEndpoints.cs`, `web/fusion-rpg-web/src/features/fusion/fusionView.ts`.

**Two suites were run for this document**, so the design gate's evidence box is ticked rather than
excused:

```
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Atoms"
  → Passed: 231, Failed: 0
dotnet test tests/FusionRpg.Data.Tests --filter "…Container|…Curve|…ContentHash"
  → Passed: 40, Failed: 0
```

Nothing this document rules on is currently red. Everything it changes is therefore a deliberate move,
not a repair.

---

## The three questions

| Q | Question | Ruling in one line |
|---|---|---|
| **Q1** | Shard per rung (10 ids) or per band (4 ids)? And rebase the cost tables. | **Per band, four ids, the shipped strings — but the strings stop being derived from `DemonRarity`.** |
| **Q2** | `ContainerRow.PoolRolls` vs `RarityRow.PoolRolls` — which is authoritative? | **Neither is the post-op authority.** The container wins the *draw*; the rarity row is an *authoring bound*; the invariant reads the **instance**. |
| **Q3** | `CurveInput.Rarity` exists; E5's Boundaries forbid rarity changing a magnitude. | **Both are right about different consumers, and the code is a third thing: live and wrong.** |

---

## Q1 — shard granularity, and the cost rebase

### Q1.1 Finding — what the round actually produced

Three documents price the same operations in three different rarity vocabularies.

| Doc | Vocabulary it prices in | Rungs |
|---|---|---|
| [ssot-rarity.md](ssot-rarity.md):117-126 | `chaff · sprout · grafted · cultivated · fused · chimeric · heirloom · firstseed · sunwoven · almanac`, ordinals 10–100 | **10** |
| [ssot-materials-crafting.md](ssot-materials-crafting.md):131-132 | `common · rare · epic · legendary` bands | **4** |
| [ssot-enhancement.md](ssot-enhancement.md):437-443 | `Normal · Magic · Rare · Epic · Legendary` | **5** |
| [ssot-generation.md](ssot-generation.md):401-409 | `R1…R7` by ordinal | **7** |

Four, not three. The enhancement lane's `rarity_cap` table (`ssot-enhancement.md:437-443`) is a
five-rung Diablo ladder that matches nothing else in the folder, and I12's illustrative table
(`ssot-generation.md:401-409`) is a seven-rung one. Both are marked illustrative and both say I1 owns
the real ladder, so neither is a competing claim — but both must be rewritten, and saying so is the
point of this section.

**A correction to the brief.** `ssot-rarity.md` does **not** ask for a `material_band` column. The ask
originates in [ssot-materials-crafting.md](ssot-materials-crafting.md):136 and is restated as I9's
hardest dependency at :684-687. What I1 wrote instead is the opposite instruction:

> "**I9 — a salvage material namespace that does not overload `shard.{DemonRarity}`.** … Item salvage
> keyed on a ten-rung ladder needs its own ids — this lane suggests `dust.{item_rarity_id}` — and
> reusing `shard.*` would silently fuse two ladders that §4.3 keeps apart."
> — `ssot-rarity.md:696-700`, and the same instruction as a registry constraint at `:340`

So the two lanes did not merely fail to agree on granularity; **they issued each other contradictory
instructions.** I9 asked I1 for a band column so the four shipped ids could be kept. I1 told I9 not to
touch the four shipped ids at all and to mint a ten-id `dust.*` namespace. That is the real conflict,
and it is sharper than "10 vs 4".

### Q1.2 Finding — what renaming or re-keying `shard.*` actually costs

I checked this rather than taking it on trust. The four ids are **generated, spent, served over HTTP,
dropped by expeditions, and rendered as literal UI labels**:

| Site | What it does | Evidence |
|---|---|---|
| Mint | `DemonMaterialCatalog.Build()` loops the four `DemonRarity` values and emits `$"shard.{rarity.ToId()}"` | `src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:18-19` |
| Gate | `DemonMaterialCatalog.IsKnown` is the validity check every material write passes | `DemonMaterialCatalog.cs:23-25`; enforced at `RpgStore.Expeditions.cs:209` per `ssot-generation.md:604` |
| Spend | fusion builds the cost line `("shard." + cost.ShardRarity.ToId(), cost.ShardCount)` | `src/FusionRpg.Data/Sqlite/RpgStore.Fusion.cs:374` |
| Contract | the REST cost DTO carries `shardMaterialId = "shard." + cost.ShardRarity.ToId()` | `src/FusionRpg.Server/FusionEndpoints.cs:202` |
| Faucet | expedition rewards hold `shard.common` / `shard.rare` as string constants | `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:165-166` |
| UI | the have/need panel prints the id **as the label** — `label: cost.shardMaterialId` | `web/fusion-rpg-web/src/features/fusion/fusionView.ts:24-27` |
| Player data | `rpg_demon_materials` PK is `(player_id, material_id)`, so a rename is a **row migration on live balances**, not a code edit | `src/FusionRpg.Data/Sqlite/RpgStore.cs:520-526` |

Six production sites plus a data migration. Renaming is **not** free, and I9's claim at
`ssot-materials-crafting.md:106-109` — *"I am appending eleven ids and renaming nothing"* — is
verified correct and is worth protecting.

### Q1.3 Ruling — four bands, shipped strings, new owner

> **R1. A shard exists per BAND. Four ids. `shard.common` · `shard.rare` · `shard.epic` ·
> `shard.legendary`, byte-identical to what ships today.**
>
> **R2. The four strings stop being derived from `DemonRarity`.** A new four-value
> `MaterialBand { Common, Rare, Epic, Legendary }` enum in `FusionRpg.Core` becomes the id source;
> `DemonRarity` gains a 1:1 `ToMaterialBand()` map and stops being the keyspace owner. Same four
> strings, same output bytes, no migration, one new file and one edited line
> (`DemonMaterialCatalog.cs:18`).
>
> **R3. `rarity` gains a non-nullable `material_band` column**, values 1–4, populated from I1's own
> `DemonRarity → ordinal band` map, which it already published at `ssot-rarity.md:298-303`. Band is
> **stored, never inferred from an ordinal range** — I9 is right at `:137-138` that inferred
> boundaries drift the moment a rung is inserted.
>
> **R4. `dust.{item_rarity_id}` is rejected.** I1 §9.8 is withdrawn.

R2 is the whole answer to the contradiction. I1's objection at `:696-700` is not about the *strings* —
it is about a ten-rung item ladder being keyed off a four-value **demon enum**, which would fuse two
ladders §4.3 deliberately keeps apart. R2 removes the coupling without touching a single id, a single
row, or a single HTTP field. I9 keeps everything it asked for; I1 gets the separation it asked for.

R4 follows: with the coupling gone there is nothing left for `dust.*` to fix. And I9's salvage function
already returns `shard.{band−1}` plus `substrate` (`ssot-materials-crafting.md:256-260`), so a tenth
namespace would be a fifth material class in a vocabulary that argued itself down to four (§3.1, §8.1).

**The band map, as data.** Directly from `ssot-rarity.md:298-303`, now a column rather than four prose
rows:

| Ordinal | `rarity_id` | `material_band` | Band id |
|---|---|---|---|
| 10 | `chaff` | 1 | `common` |
| 20 | `sprout` | 1 | `common` |
| 30 | `grafted` | 1 | `common` |
| 40 | `cultivated` | 2 | `rare` |
| 50 | `fused` | 2 | `rare` |
| 60 | `chimeric` | 2 | `rare` |
| 70 | `heirloom` | 3 | `epic` |
| 80 | `firstseed` | 3 | `epic` |
| 90 | `sunwoven` | 4 | `legendary` |
| 100 | `almanac` | 4 | `legendary` |

Ten rungs over four bands is **3/3/2/2**, not 2.5 each. The uneven split is deliberate and it is
already implied by I1's map: the top two rungs are drop-only (`ssot-rarity.md:216-218`), so a
narrower legendary band is what makes `shard.legendary` the second bottleneck I9 designed for
(`ssot-materials-crafting.md:516-517`).

**Append-only, both ways.** A rung's band may never change after release (I9 `:139-140`, adopted). A
new rung inserted at ordinal 15 or 85 is free and takes the band of its neighbours. A **fifth** band is
a reviewed change: a new enum value, a new shipped material id, a new expedition faucet, and a content
hash bump.

### Q1.4 The rebase — enhancement

Three things in [ssot-enhancement.md](ssot-enhancement.md) do not line up and must change.

**(a) `item_enhance_cost` is keyed on the wrong column.** As written its key is
`(rarity, level, material_id, qty)` (`ssot-enhancement.md:269`) — one row set per **rung**. Ten rungs
× 20 levels × 3 material lines is 600 authored rows, and 2.5 of every group name the identical
`shard.{band}` id at different quantities for no mechanical reason.

> **R5. `item_enhance_cost` keys on `band`, not `rarity`.** Columns become
> `(band INT, level INT, material_id TEXT, qty INT)`. Four bands × 20 levels × 4 lines = 320 rows.

The rung still reaches the price — through `enhance_cap`, not through the key. A higher rung has a
higher cap (`ssot-enhancement.md:430-443`), so it climbs further up the same per-level curve and costs
more in total without one row per rung. That is I6's own shape; R5 only stops it being paid for twice.

**(b) The five-rung `rarity_cap` table must be re-authored for ten rungs, as a KV row, not a column.**
`ssot-enhancement.md:434` offers `rarity_cap` "as an append-only column on the existing `rarity`
table". I1's registry puts it in `rarity_budget` under the key `enhance_cap`
(`ssot-rarity.md:335`). The registry wins — see §Schema impact for the general rule.

Rebased caps, honouring I1's constraint that total enhancement gain at cap be **≤ one rung step in
expectation** (`ssot-rarity.md:689-691`) and I6's own `+4 … +20` span:

| Ordinal | `rarity_id` | `enhance_cap` |
|---|---|---|
| 10 | `chaff` | 4 |
| 20 | `sprout` | 6 |
| 30 | `grafted` | 8 |
| 40 | `cultivated` | 10 |
| 50 | `fused` | 12 |
| 60 | `chimeric` | 14 |
| 70 | `heirloom` | 16 |
| 80 | `firstseed` | 18 |
| 90 | `sunwoven` | 20 |
| 100 | `almanac` | 20 |

Two rungs share the cap 20 on purpose: `almanac` is already the count-band ceiling, and giving it a
higher enhancement cap too would stack two ladders on one rung. Illustrative, not balanced.

**(c) The cost table has no catalyst line at all**, while I9's vocabulary requires one on every
non-socketing operation (`ssot-materials-crafting.md:94,118`). Adding it is the rebase.

> **The rebased enhancement cost, per attempt.** `L` = the level being attempted (the target, so a
> `+7 → +8` attempt is `L = 8`). `b` = `material_band(rarity_id)`. `ilvl` = item level. `e` = the
> item's dominant concrete element, absent if it has none. Cost is spent whether or not the attempt
> succeeds (`ssot-enhancement.md:496`).
>
> | Line | Expression | Unit |
> |---|---|---|
> | souls | `50 × L` | ledger units |
> | `shard.{b}` | `ceil(L^1.6 / 2)` | count |
> | `essence.{e}` | `ceil(ilvl × L / 40)`, omitted when the item has no element | count |
> | `catalyst.temper` | `ceil(L / 3)` | count |
> | substrate | **none** | — |
>
> The first three are I6's own authoring guidelines (`ssot-enhancement.md:483`), unchanged and
> verified to reproduce its published table: at `ilvl 64`, `L = 4` gives 200 souls · 5 shards ·
> 7 essence, matching `:490`. The fourth line is new and is I9's `ceil((n+1)/3)` temper rate
> (`ssot-materials-crafting.md:535`) written in I6's variable. Only the **shard id** rebased: it was
> `shard.{rarity}` read as a rung, it is now `shard.{band}`.

Total `catalyst.temper` to take one item from `+0` to `+20`, every attempt succeeding:
`Σ ceil(L/3), L = 1…20` = **77**. That is the number I4 and I9 need for faucet pacing, and it did not
exist before this rebase because the line did not exist.

**(d) Substrate is deliberately absent.** I9's reference table charges `1 × (n+1)` substrate for temper
(`ssot-materials-crafting.md:535`). Rejected: substrate is the class I9 itself designates as the one a
player should never hesitate to spend (`:302`, `:510-512`), and putting it on the single
highest-frequency operation in the game turns the generous class into the grinding one. Enhancement's
brake is `catalyst.temper`, which has a strictly-lossy salvage return (`:300`), and one brake per
operation is enough.

### Q1.5 The rebase — reroll

Two ids in [ssot-reroll.md](ssot-reroll.md):306-311 are outside I9's vocabulary.

| Written | Problem | Rebased to |
|---|---|---|
| `shard.{rarity}` | reads as one id per rung | `shard.{b}`, `b = material_band` |
| `catalyst.{rarity}` — *"a material that does not exist yet, named for I9"* (`:310`) | would mint four-to-ten new catalyst ids, and I9 §4.2 rejected a fourth catalyst outright (`ssot-materials-crafting.md:212`) | `catalyst.flux` — the existing "re-randomise" verb |
| `recall-token` (`:311`) | not one of the five classes and not a material | **unsettled** — see Open questions |

> **The rebased reroll cost.** `b` = `material_band(rarity_id)`. `T` = targeted affix slots.
> `K = drawnCount − T` (see Q2 for why it is not `container.PoolRolls`).
> `ANCHOR_MULT = 2^K`; `FOCUS = 3` when a Reforge target is restricted to its own group, else 1;
> `ESCALATION‰ = min(4000, 1000 + 250 × priorOps(instance, op_kind))` — all unchanged from
> `ssot-reroll.md:271-278`.
>
> | Operation | Souls | Shard | Essence | Catalyst |
> |---|---|---|---|---|
> | **Temper** (values only) | `400 × ESCALATION‰ / 1000` | `1 × shard.{b}` | — | `1 × catalyst.flux` |
> | **Reforge** (identity + tier + value, `T` slots) | `1500 × 2^K × FOCUS × ESCALATION‰ / 1000` | `T × shard.{b}` | `1 × essence.{e}` per focused target | `b × catalyst.flux` |
> | **Imprint** (deterministic) | `15000 × 2^K × ESCALATION‰ / 1000` | `T × shard.{b}` | `1 × essence.{e}` per target | `2 × b × catalyst.flux` |
>
> Temper gains a catalyst line it did not have. Imprint's premium moves entirely into souls and flux
> **count**, never into a new id.

**Whose numbers win where two lanes priced the same operation.** I9's §7.4 prices "reroll one affix" at
`80 × b` souls and "reroll all" at `200 × b` (`ssot-materials-crafting.md:536-537`); I7's price
function gives 400 and 1500 before multipliers. Two orders of magnitude apart.

> **R6. I9 owns the cost *vocabulary* — which ids exist, what class each is, and the shape of a cost
> line. The operation's owning lane owns the *quantities*.** I9's §7.4 is explicitly *"illustrative,
> not balanced"* (`:524`) and is demoted to a fallback for operations whose lane has not authored a
> price. Its `elevate` · `temper` · `reroll` rows are superseded by I6 and I7; its `forge` ·
> `upcycle` · `gem` · `bore` · `socket` rows stand.

This is a narrowing of contract cut #7 (*"cost vocabulary — I9; I6, I7, I4 spend in I9's terms"*),
and it matches what the cut says: spending *in someone's terms* is not the same as them setting your
prices.

### Q1.6 The rebase — the recipe cost curve

`ssot-materials-crafting.md:596-600` authors `curve.band-linear` as
`[[1,1000],[2,2000],[3,3000],[4,4000]]` with `input: rarity`, and states it reads the container's
rarity **ordinal**. Under a ten-rung ladder the ordinals are 10…100, so `MultiplierAt` clamps to the
last point (`CurveTable.cs:74`) and **every real rung returns 4000×**. The example is broken by I1's
ladder, silently.

> **R7. Cost curves with `input: rarity` are authored at ordinals, not at band numbers.**
> `curve.band-linear` is re-authored as ten points at `x = 10, 20, … 100`. No new curve input is
> minted.

Rejected: adding `CurveInput.Band`. It is an ask-first change to E2 for something ten authored points
already express, and it would express *less* — ten points can price two rungs inside one band
differently, which is the whole reason the ladder is long.

---

## Q2 — `pool_rolls` and its two, actually three, sources of truth

### Q2.1 Finding — one of them has no reader at all

| Symbol | Written by | Read by | Verdict |
|---|---|---|---|
| `ContainerRow.PoolRolls` (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:64`) | `UpsertContainer` (`RpgStore.Containers.cs:143-155`) | **`Instantiator.Draw`** — the loop bound at `Instantiator.cs:139` and the early-out at `:128` | **live, and the only production reader of any `pool_rolls`** |
| `RarityRow.PoolRolls` (`ContainerRow.cs:93`) | `UpsertRarity` (`RpgStore.Containers.cs:88-95`) | `ListRarities` (`:105-118`) → **three callers, all tests**: `ContainerStoreTests.cs:194,215,224`. Hashed at `ContentHashRegistry.cs:99-104`. | **zero production readers** — the `status.expose.*` shape SC7 names |

The brief's suspicion is confirmed: `RarityRow.PoolRolls` has no reader. Both lane docs found this
independently — `ssot-rarity.md:66-67` and `ssot-generation.md:165-171`.

### Q2.2 Finding — three incompatible reinterpretations, not two

| Lane | Reading of `rarity.pool_rolls` | Evidence |
|---|---|---|
| **I1** | the band **FLOOR**, with a new `pool_rolls_max` as the ceiling | `ssot-rarity.md:402`, `:408` |
| **I12** | the band **CEILING**, floor implied as one less: `env.rolls = NextInclusive(max(1, band.PoolRolls − 1), band.PoolRolls)` | `ssot-generation.md:373`, and the column header *"pool_rolls (band max)"* at `:401` |
| **I7** | the **container's** value as a post-operation invariant: `count(drawn atoms) == container.PoolRolls` | `ssot-reroll.md:228`, flagged as needing a ruling at `:250-252` |

I1's floor and I12's ceiling are the same column read in opposite directions. Whichever the ladder
authors write, the other lane's arithmetic is wrong on every rung.

### Q2.3 Finding — I7's invariant is already false the moment I12 lands

This is the part neither lane saw, because neither read the other.

I12's picked option B puts **one container per base type carrying its full t1–t5 pool**, and hands the
instantiator a drop-time `DrawEnvelope(Rolls, MinTier, MaxTier)` (`ssot-generation.md:178`, `:184-190`).
Under that design the container's `pool_rolls` is a *default*, not the item's affix count — a
`cultivated` and an `almanac` copy of the same base type share one container and one `pool_rolls`
value while carrying 2–3 and 5–6 affixes respectively.

So `count(drawn atoms) == container.PoolRolls` (`ssot-reroll.md:228`) fails on every item that is not
at the container's default rung. I7's guard would reject legal items and refund the operation — the
exact "reject, never ignore" mechanism firing on correct input.

### Q2.4 Ruling

> **R8. The container wins the draw.** `ContainerRow.PoolRolls` stays the instantiator's count and the
> default-path guarantee. This is not a preference; it is what makes I12's own claim true that *"when
> `envelope` is null the code path is byte-identical to today"* (`ssot-generation.md:193`).
>
> **R9. `RarityRow.PoolRolls` is an AUTHORING BOUND, never a draw count. It means the band FLOOR**, and
> I1's `pool_rolls_max` (`ssot-rarity.md:408`) is the ceiling. I12's ceiling-minus-one reading is
> rejected.
>
> **R10. The post-operation invariant reads NEITHER table. It reads the instance's recorded mint
> count.** I12 records it; I6 and I7 compare against it.

**Why floor-plus-max beats ceiling-minus-one**, on evidence rather than taste:

1. `NextInclusive(max(1, PoolRolls − 1), PoolRolls)` cannot express `chaff`, whose band is a literal
   `0` (`ssot-rarity.md:117,128`). The `max(1, …)` guard turns `0` into the malformed range `[1, 0]`.
   I12 patched this with a prose special case — *"(0 for the bottom rung, which has no pool)"*
   (`ssot-generation.md:374`) — which is a special case existing precisely because the encoding cannot
   hold the value.
2. It cannot express a **fixed** band. Rung 20's band in I1's ladder is `1–2` and rung 100's is `5–6`,
   but a designer who wants a rung to roll exactly `3` has no encoding for it. Floor-plus-nullable-max
   does, and I1 designed the nullable default for exactly that (`ssot-rarity.md:408`: *"Null means a
   fixed count, so the shipped behaviour stays reachable"*).
3. Ceiling-minus-one hard-codes a band **width of 2** into arithmetic. Every band in I1's §3.3 table
   happens to be width 2 today, so the two readings agree on today's numbers and would diverge silently
   the first time a band widens. A convention that is right by coincidence is the worst kind.

**Why the invariant reads the instance.** Two reasons, both structural:

- Under R8 + I12's envelope, no table holds "how many affixes does *this item* have". The container
  holds a default and the rarity row holds a band. Only the item knows, and the item does record it —
  `effect_instance_atom` rows are the drawn atoms, appended after the fixed core in draw order
  (definitions §5, `definitions.md:176`; DDL at `RpgStore.AtomInstances.cs:66-72`).
- SC5 survives. The mint count is a pure function of inputs already recorded 1:1 with the instance:
  `item_generation(instance_id, rarity_ordinal, item_level, …)` (`ssot-generation.md:564-572`) plus
  the `rarity` row plus `tierCeiling(ilvl)`. Replaying the drop reproduces the count.

**Store it, do not derive it.** `drawnCount = count(effect_instance_atom) − count(container.Atoms)` is
tempting and it is wrong: the container's fixed core is **mutable content**, so a later authoring edit
that adds one implicit silently changes every existing item's computed affix count, and every reroll
on every old item then fails its invariant. I6 already established that a catalog change must not
retroactively alter an item (`ssot-enhancement.md:599`, `:684-690`).

> **R11. `item_generation` gains one column: `envelope_rolls INTEGER NOT NULL`** — the count drawn at
> mint. The row is written once and never updated (`ssot-generation.md:591-592`), which is exactly the
> right home.

This does **not** contradict I7's own SC7 hygiene argument for deriving `priorOps` from the op log
(`ssot-reroll.md:298-300`). The distinction is what the derivation depends on: `priorOps` derives from
an append-only log that cannot change underneath it; a core-count subtraction derives from mutable
content. Derive from immutable, record from mutable.

### Q2.5 The validation that enforces it

| Check | When | Reason code | Owner |
|---|---|---|---|
| A container naming rung `r` has `pool_rolls ∈ [rarity.pool_rolls, coalesce(rarity.pool_rolls_max, rarity.pool_rolls)]` | container upsert | `RarityBandViolated` (new, I1) | E5 / `ContainerValidator` |
| `DrawEnvelope.Rolls` outside the same band | before the draw, before any spend | `RarityBandViolated` | I12 |
| `pool_rolls_max < pool_rolls` on a rarity row | rarity upsert | `BadParamValue` (existing) | E5 |
| Post-operation `count(drawn atoms) == item_generation.envelope_rolls` | before an op commits | I7's existing rollback path (`ssot-reroll.md:234`) | I6 / I7 |
| A container naming a rarity id absent from `rarity` | container upsert | `UnknownRarity` (new, I1) | E5 |

The first rule is what makes the rarity row's numbers *true* instead of a second authority: the band
constrains what may be authored, and nothing reads it at draw time. That is how a column with zero
runtime readers stops being an SC7 lie without becoming a competing source of truth.

**One guard test this ruling owes**, beyond I1's list:
`Container_pool_rolls_outside_its_rung_band_is_rejected` — the test that makes R9 enforceable rather
than documentary.

---

## Q3 — the `CurveInput.Rarity` contradiction

### Q3.1 Finding — what each side actually says

**definitions.md §2**, the "Curve `input` sources" table (`definitions.md:100-106`):

| `input` | Reads | When absent |
|---|---|---|
| `rarity` | the container's rarity **ordinal** (§4) | container has no rarity → rejected at bind |

**definitions.md §4** (`:148`), the section the row cross-references, says something different about
the same field: *"`rarity` | a label, and the **key budgets are looked up by** (§7)"*. §4 lists exactly
three things rarity governs — `pool_rolls`, `min_tier`/`max_tier`, and budget lookup
(`definitions.md:143-148`). Magnitude is not among them.

**spec-container-schema.md Boundaries** (`:145`), under **Never**:

> "let rarity change an atom's magnitude — rarity picks count and tier, tier carries strength."

The same spec repeats it in its A1 contract at `:98`: *"rarity governs count and tier-window rather
than magnitude."*

So definitions does not uniformly outrank the spec here — **definitions contradicts itself.** §2's
table row is the only sentence in either document that puts rarity on the magnitude path, and §4 of the
same file agrees with the spec against it.

### Q3.2 Finding — the code is neither dead nor correct

| Fact | Evidence |
|---|---|
| `CurveInput.Rarity` exists, is persisted, round-tripped, and content-hashed | `CurveTable.cs:4-9`; `effect_curve.input` is a hashed column at `ContentHashRegistry.cs:90-95` |
| **No production code dispatches on it.** The only production reference to any `CurveInput` member is the D9 check `input == CurveInput.Level` | `AtomRowValidator.cs:131`; grep for `CurveInput.` across `src/` returns that one line |
| The single curve consumer applies **`MultiplierAt(ownerLevel)` unconditionally, ignoring `curve.Input`** | `AtomCompiler.cs:230-236`, called from `:224` (bounds) and `:260` (resolved params) |
| `Instantiator` never touches a curve at all — the mint path, where a container's rarity is in scope, has no curve code | grep for `Curve` in `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs` → no hits |
| The "rejected at bind" behaviour §2 promises **does not exist**. `BindGate`'s only `ScopeUnsupported` rejections are the world-host check and the `defense`-channel check | `BindGate.cs:43-45`, `:100-106` |
| The only tests naming `CurveInput.Rarity` assert store round-trip, not evaluation | `tests/FusionRpg.Data.Tests/CurveStoreTests.cs:56,59` |

**So the code is a third position nobody argued.** `CurveInput.Rarity` is not dead — it is **live and
silently wrong**: an author who writes `input: rarity` today gets a curve evaluated at the *owner's
level*, with no rejection, no warning, and a content hash that says the row is fine. `CurveInput.Tier`
has the identical defect: `MultiplierAt(ownerLevel)` is applied to it too, so a tier curve reads a
level. That is two of the three inputs mis-evaluated.

This is a **defect for R1's register**, and it is independent of every design question below. Whatever
the ruling, `MultiplierFor` must dispatch on `curve.Input`.

### Q3.3 Ruling

> **R12. The E5 Boundary stands, and is the stronger statement: rarity may never scale an atom's
> magnitude.** definitions §2's table row is the losing side — it contradicts definitions §4, it
> describes a reader that does not exist, and it promises a bind-time rejection that is not
> implemented.
>
> **R13. But the two documents ARE talking about different things, and that half of the brief's
> hypothesis is correct.** `CurveTable` is not owned by the atom magnitude path: its own docstring says
> *"The same table serves E9's power reference scale"* (`CurveTable.cs:19-20`), and I9 uses a curve on
> `material_recipe_cost.qty_curve_id` restricted to `input: rarity` and `input: tier`
> (`ssot-materials-crafting.md:336`) to scale a **cost quantity**. A cost quantity is not an atom
> magnitude, and the E5 Boundary — which lives in the *container schema* spec and speaks about atoms —
> does not reach it.
>
> **The rule, stated once:**
>
> > `CurveInput.Rarity` is legal machinery with a **closed consumer set of two**: cost-quantity
> > resolution (`material_recipe_cost.qty_curve_id`) and E9's power reference scale. It may **never**
> > be referenced from a `ValueSpec.CurveId` on any atom, in any container kind, at any scope.
>
> **R14. The magnitude ban is not item-scoped.** I1 bans a rarity curve on `container_kind = 'item'`
> (`ssot-rarity.md:195`, `:445`). Too narrow: a `gem` or `charm` container scaling an atom by its rung
> is the identical defect and I1's own overlap invariant would break the same way. The ban is on the
> **magnitude path**, not on a container kind.

**Why the boundary wins on the magnitude path, from measurement rather than principle.** I1's overlap
invariant — `U(n,1) ∈ [5 %, 30 %]`, measured at 7.9–28.3 % (`ssot-rarity.md:166-179`) — is computed on
the assumption that a rung sets a count band and a tier window and nothing else (`:100-101`). A
per-rung magnitude multiplier is a fourth variance source that dominates the other three, and I1
measured the collapse for a comparable single-source variant at adjacent upsets near 45 %
(`:263`) — outside the band, and the failure mode §8.3 exists to prevent. OD4 is an owner decision;
a rarity magnitude curve deletes it.

### Q3.4 What each losing side must change

| Losing side | What it must change |
|---|---|
| **definitions.md §2** | The `rarity` row of the "Curve `input` sources" table (`:100-106`). It must say: read by cost-quantity resolution and E9's power scale; **never** by `ValueSpec` on an atom. And the "rejected at bind" column is aspirational for both `level` and `rarity` — `BindGate` implements neither (`BindGate.cs:43-45`, `:100-106`). Say so, or build it. |
| **The code** | `AtomCompiler.MultiplierFor` (`:230-236`) must dispatch on `curve.Input` and refuse a curve whose input it cannot supply, rather than passing `ownerLevel` to all three. Today it mis-evaluates both `Rarity` and `Tier`. **Defect, not design** — hand it to R1's register. |
| **spec-container-schema.md** | The Boundary at `:145` stays in **Never** but wants one clause so it is not read as "no consumer may key on a rarity ordinal": rarity may not change an **atom's** magnitude; keying a **cost** or a **power reference** on the ordinal is legal and is E9's and I9's. |
| **ssot-rarity.md** | §3.6 (`:195`) and §6.1 (`:445`) widen the `BadCurve` ban from `container_kind = 'item'` to any `ValueSpec.CurveId`. The guard test `Rarity_curve_input_is_rejected_on_item_containers` (`:479`) is renamed and widened. §10.6 (`:738-741`) is answered and closes. |
| **ssot-materials-crafting.md** | §6.2's `qty_curve_id` restriction (`:336`) is **upheld and becomes the definition** of the legal consumer set. §7.5's `curve.band-linear` (`:596-600`) re-authors at ordinals per R7. |

---

## Consequential edits, by document

Every edit this decision forces. Section-level, so nobody has to re-derive the list.

### [ssot-rarity.md](ssot-rarity.md) — I1

| § | Edit |
|---|---|
| §3.3 ladder table (`:115-126`) | add a `material_band` column, values from R3's table |
| §3.6 (`:195`) | widen the `CurveInput.Rarity` ban from `item` containers to the whole magnitude path (R14) |
| §4.3 (`:295-306`) | the `DemonRarity → ordinal band` map is promoted from prose to the `material_band` column; note the map is now read in the **rung → band** direction by I9 |
| §4.4 registry (`:326-342`) | `salvage_yield` row (`:340`) drops *"must not reuse `shard.{DemonRarity}`"* — R2 removes the coupling. Add `material_band` to the *columns on the rarity row* block. `enhance_cap` (`:335`) gets I6's rebased ten-rung values (Q1.4b) |
| §5.1 / §5.3 (`:387-413`) | `material_band` joins N2 as a new non-nullable column; `pool_rolls` reinterpretation at `:402` is confirmed as **floor** (R9) |
| §6.1 (`:445`) | widen the `BadCurve` row per R14 |
| §6.3 (`:469-481`) | rename/widen `Rarity_curve_input_is_rejected_on_item_containers`; add `Container_pool_rolls_outside_its_rung_band_is_rejected` |
| §9.8 (`:696-700`) | **withdrawn.** `dust.{item_rarity_id}` is rejected (R4) |
| §10.6 (`:738-741`) | **answered** by Q3 |

### [ssot-materials-crafting.md](ssot-materials-crafting.md) — I9

| § | Edit |
|---|---|
| §3.1 (`:88-104`) | shard row: the id source is `MaterialBand`, not `DemonRarity` (R2). Id strings unchanged |
| §3.3 (`:125-143`) | the band request is **granted**; add the concrete rung → band map from R3 |
| §6.2 (`:336`) | `qty_curve_id` restriction upheld and promoted: it is now the *definition* of the legal rarity-curve consumer set (R13) |
| §7.4 (`:522-548`) | demoted to a fallback. Delete the `elevate` · `temper` · `reroll` rows — superseded by I6 and I7 (R6). Keep `forge` · `upcycle` · `gem` · `bore` · `socket` |
| §7.5 example 3 (`:596-600`) | `curve.band-linear` re-authored at ordinals 10…100 (R7) |
| §9.1–9.2 (`:684-689`) | satisfied. Four bands are confirmed sufficient for a ten-rung ladder |

### [ssot-enhancement.md](ssot-enhancement.md) — I6

| § | Edit |
|---|---|
| §5.4 (`:269`) | `item_enhance_cost` keys on `band`, not `rarity` (R5) |
| §7.3 (`:427-447`) | the five-rung `rarity_cap` table is replaced by the ten-rung `enhance_cap` values in Q1.4b, and moves from a `rarity` **column** to a `rarity_budget` **key** |
| §7.5 (`:473-517`) | `shard.{rarity}` → `shard.{band}`; add the `catalyst.temper` line; keep the three published formulas verbatim |
| §9.1 (`:700`) | the ask changes from "a column on `rarity`" to "a `rarity_budget` key" |
| §9.3 (`:708-714`) | resolved — `shard.{band}` is spent by reroll, elevation, gem forging and socket boring too, so enhancement is not its only sink |

### [ssot-reroll.md](ssot-reroll.md) — I7

| § | Edit |
|---|---|
| §4.1 invariant (`:222-232`) | `count(drawn) == container.PoolRolls` → `== item_generation.envelope_rolls` (R10). This is the single most important edit in the list: as written the guard rejects legal items once I12 lands |
| §4.2 hazard 2 (`:250-252`) | answered by R8/R9/R10 |
| §5.1 (`:271-278`) | `K = pool_rolls − T` → `K = drawnCount − T` |
| §5.3 (`:302-315`) | `shard.{rarity}` → `shard.{b}`; `catalyst.{rarity}` → `catalyst.flux`; add the Temper catalyst line; `recall-token` flagged as unsettled |
| §10.1–10.3 (`:541-633`) | the three worked examples re-price against Q1.5's table |
| §12.5a (`:732-733`) | answered by R8/R9/R10 |

### [ssot-generation.md](ssot-generation.md) — I12

| § | Edit |
|---|---|
| §4.2 envelope (`:367-375`) | `env.rolls` reads **floor and max**: `NextInclusive(band.PoolRolls, coalesce(band.PoolRollsMax, band.PoolRolls))`. The `max(1, …)` special case for `chaff` disappears — a `[0,0]` band draws zero without a guard (R9) |
| §4.2 column header (`:401`) | *"pool_rolls (band max)"* → floor, with a `pool_rolls_max` column beside it |
| §4.2 illustrative ladder (`:401-409`) | replaced by I1's ten rungs; keep the draw weights as a seven-to-ten remap |
| §5.1 `item_generation` (`:564-572`) | add `envelope_rolls INTEGER NOT NULL` (R11) |
| §5.2 (`:601`) | I12 is confirmed as the rarity table's first production consumer — of `min_tier`/`max_tier`/`pool_rolls`/`pool_rolls_max` as **envelope inputs**, not as draw authorities |

### [../effect-atom/definitions.md](../effect-atom/definitions.md)

| § | Edit |
|---|---|
| §2 "Curve `input` sources" (`:100-106`) | rewrite the `rarity` row per R13; mark the bind-time rejections for `level` and `rarity` as unimplemented |
| §8 covered tables (`:331-336`) | `rarity` gains `material_band`; see Schema impact |
| §10 reason codes | I1's three new codes (`UnknownRarity`, `RarityBandViolated`, `RarityLadderMutated`) are needed by this decision's validation table and should be reviewed as one batch |

### [../effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md)

| § | Edit |
|---|---|
| Boundaries → **Never** (`:145`) | narrow to the atom magnitude path; cost and power keying on the ordinal is legal |
| Contract block (`:24`) | `rarity` FK becomes real (`UnknownRarity`); add `material_band` to the `rarity` table's description |
| Structure (`:110`) | unchanged — all of this stays inside `RpgStore.Containers.cs` |

---

## Schema impact

**The general rule this decision sets**, because three lanes each proposed a `rarity` column and I1
proposed a KV table:

> **A column on `rarity` when two or more lanes read it and it is non-nullable. A `rarity_budget` key
> when one lane reads it.**

That is the line I1's own §4.4 already draws between *"columns on the rarity row itself"* and *"keys in
`rarity_budget`"* (`ssot-rarity.md:328,332`), made into a test anyone can apply.

| Change | Additive or migration? | Against which spec | Verdict |
|---|---|---|---|
| `rarity.material_band INTEGER NOT NULL` | **Additive DDL** — the table holds zero production rows (`ssot-rarity.md:66-67`) so NOT NULL costs no backfill. But it **must** be hashed, so it is a `ContentHashRegistry` V2 and a `CurrentSchemaVersion` bump | E5 (*"ask first: adding a column"*, `spec-container-schema.md:143`) | **Ask-first** |
| `rarity.pool_rolls_max INTEGER` nullable | Additive, same hash consequence | E5 | Ask-first (already I1's §10.1) |
| `MaterialBand` enum + `DemonRarity.ToMaterialBand()` | **Pure code, no schema, no data, byte-identical ids** | none — new Core type | **Safe. Do it** |
| `item_enhance_cost` keyed on `band` | New table, never shipped | I6's own | Safe |
| `item_generation.envelope_rolls` | New table, never shipped | I12's own | Safe |
| `rarity_budget` table | New table | E5 + content hash registry | Ask-first (already I1's §10.1) |
| `AtomCompiler.MultiplierFor` dispatching on `curve.Input` | Code fix, no schema. **Changes behaviour** for any existing `rarity`/`tier` curve row | E2 | **Defect fix. No content rows use those inputs today** (grep: only `CurveStoreTests.cs:56,59,90`), so no golden moves — but that is read from the code, not executed against the full suite |

**The content-hash collision worth flagging now.** `ContentHashRegistry.CurrentSchemaVersion` is 1, and
its own comment already reserves the next two numbers: *"Bump when a table joins or leaves. E18 → 2,
E9 → 3"* (`ContentHashRegistry.cs:33`). Two `rarity` columns and a `rarity_budget` table need a bump
too, and the item program is not in that sequence. **Somebody has to allocate version numbers across
three programs**, and it is not this decision's call. Named so it does not surface as a merge conflict.

One relieving fact: the registry lists columns **explicitly** rather than reading `PRAGMA table_info`
(`ContentHashRegistry.cs:11-16`). So a column added to the DDL but not to the registry does not move a
single hash. The bump is only owed when the column becomes hashed content — which `material_band` must,
since it re-prices every recipe.

---

## What was rejected, and why

| Rejected | Argued by | Why it lost |
|---|---|---|
| **One shard per rung — ten ids** | the implicit reading of `shard.{rarity}` in `ssot-enhancement.md:475` and `ssot-reroll.md:308-310` | Six production sites bind the four shipped ids, including a REST field and a UI label that prints the id verbatim (`fusionView.ts:24-27`), and `rpg_demon_materials` is PK'd on `material_id` so re-keying migrates live player balances. Against that: zero mechanical gain — the rung already reaches the price through `enhance_cap` and through curve points at ordinals |
| **`dust.{item_rarity_id}` — a parallel ten-id salvage namespace** | `ssot-rarity.md:696-700` | It solves a coupling problem that R2 solves in one file with no new ids. It would also make a fifth material class in a vocabulary that argued itself down to four (`ssot-materials-crafting.md:165-172`, §8.1) |
| **Growing `DemonRarity` to ten values** | considered and rejected by I1 at `ssot-rarity.md:252`; re-checked here | Confirmed: it would move summon rates (`SummonRoller.cs:22-30`), pity thresholds, fusion trait slots, soul earn, and the shard ids — five consumers for zero item-side gain |
| **Inferring `material_band` from an ordinal range** | the cheap alternative to a column | I9 is right at `:137-138`: an inferred boundary drifts the instant a rung is inserted at ordinal 85, and it re-prices every recipe silently. Store the mapping |
| **`rarity.pool_rolls` as the band ceiling** | `ssot-generation.md:373,401` | Cannot encode `chaff`'s `0` without a `max(1, …)` special case; cannot encode a fixed band at all; hard-codes width 2 into arithmetic and is right today only by coincidence |
| **Deriving the mint affix count as `count(instance_atom) − count(container.Atoms)`** | the no-column option | The container's fixed core is mutable content, so one authoring edit retroactively breaks the reroll invariant on every existing item — the failure I6 §8.7 already forbids |
| **A fourth catalyst, `catalyst.rarity` or `catalyst.bore`** | `ssot-reroll.md:310` asks for the first; I9 §4.2 pre-rejected the second | Currency bloat. Imprint's premium is expressible as souls plus flux **count**; a new id carries no information the count does not |
| **`CurveInput.Band` as a fourth curve input** | the tidy fix for `curve.band-linear` | Ask-first against E2 for something ten authored points express, and it expresses *less* — ten points can price two rungs inside one band differently, which is the reason the ladder is long |
| **Deleting `CurveInput.Rarity` as dead code** | the obvious reading of "no production reader" | It is not dead: two live consumers want it (I9's cost quantities, E9's power scale) and the enum value is content-hashed, so removal moves `effect_curve` digests. It is mis-wired, not unwanted |
| **Banning rarity curves only on `container_kind = 'item'`** | `ssot-rarity.md:195,445` | A `gem` or `charm` scaling an atom by its rung is the same defect and breaks the same measured invariant. Ban the path, not the kind |
| **I9's §7.4 prices for other lanes' operations** | `ssot-materials-crafting.md:527-537` | Two lanes priced the same operation two orders of magnitude apart. A lane that cannot set its own curve cannot tune its own system. I9 keeps the vocabulary; the owner keeps the numbers |

---

## What I could not settle from evidence

Stated rather than decided, because each needs an input this debate does not have.

1. **`recall-token`.** `ssot-reroll.md:311` prices Recall in an id that is not one of I9's five classes
   and is not a material by I9's own definition (a material rolls nothing and carries no atoms,
   `ssot-materials-crafting.md:145-149`). It reads like a **consumable**, which is gap lane **G2** in
   the reconciliation plan — and G2 has not been written. I can say it is not a material. I cannot say
   what it is until G2 lands. Deliberately left open rather than forced into `catalyst.flux`, where it
   would silently become a sixth verb.

2. **Whether four bands survive contact with I4's gem ladder.** I9 asks I1 to confirm four bands are
   enough (`:688-689`). For *equipment* the answer is yes — R3's map covers all ten rungs. For **gems**
   I cannot answer: I4 has not declared whether a gem carries an equipment rung or its own scale, and
   `ssot-rarity.md:464` says a `gem` container reads ordinal and colour but explicitly *never* the count
   band. If gems price in `shard.{band}` they need a band, and where that band comes from is I4's.

3. **The content-hash version number.** Three programs need a `CurrentSchemaVersion` bump and the
   sequence is already spoken for two deep (`ContentHashRegistry.cs:33`). Allocating across programs is
   above this decision.

4. **Whether the ten-rung ladder survives at all.** Everything in Q1's rebase assumes I1's ten rungs.
   I1 itself puts ladder length in front of the owner as an open question (`ssot-rarity.md:736-737`),
   and a six-rung answer would change the band map, `enhance_cap`, and the drop weights — though not
   the four shard ids, which is one more argument for R1.

5. **Whether `AtomCompiler`'s mis-evaluation has ever produced a wrong number in shipped content.** I
   verified no content row uses `input: rarity` or `input: tier` by grep, and the atom suites are green
   (231 passed). I did **not** run the full corpus or the E2E suite, so I can say the defect is latent;
   I cannot say it has never fired.

---

## Open questions for the owner

1. **The `material_band` column, and the content-hash bump it forces.** Additive DDL against an empty
   table, but a hashed column and therefore a `contentHashSchemaVersion` bump against a
   Checkpoint-B-published contract — ask-first under `spec-container-schema.md:143`. The bump also has
   to be sequenced against E18 and E9, which already claim versions 2 and 3.

2. **Is `MaterialBand` worth a new Core type, or should `DemonRarity` simply be renamed?** R2 adds an
   enum and a 1:1 map so the item ladder is not keyed off a demon enum. The cheaper alternative is to
   rename `DemonRarity` to `MaterialBand` in place — fewer types, but it makes a demon-summon concept
   carry an item-economy name, and it touches `SummonRoller`, `FusionRoller`, `SoulEarnPolicy` and the
   contracts DTO. My pick is the new type; the rename is defensible and cheaper.

3. **Does enhancement really spend no substrate?** Q1.4d removes I9's substrate line from temper on the
   argument that the generous class must not sit on the highest-frequency operation. That deletes a
   sink from an economy whose inflation risk I9 says concentrates in substrate (`:508-512`). If
   substrate needs a second drain, temper is the obvious place and the upcycle ratio is the
   alternative.

4. **Does the ten-rung ladder collapse to `enhance_cap` steps of 2?** Q1.4b gives each rung +2 cap and
   caps the top two rungs equally. That makes the enhancement ladder a near-linear function of the
   rarity ladder, which is legible but removes any reason to prefer `sunwoven` over `almanac` on the
   enhancement axis. A step of 3 with fewer distinct values is defensible.

5. **`item_generation.envelope_rolls` — one column, or does the item row above the instance take it?**
   R11 puts it on I12's stamp row. Three lanes (`ssot-rarity.md:730-732`, `ssot-generation.md:590-594`,
   I13) still have not settled who owns the item row above `effect_instance`, and if that row lands the
   column may belong there instead. Recorded as a dependency, not a blocker: `item_generation` is
   written once and never updated, so moving it later is a data copy, not a redesign.

---

## Appendix — design-gate checklist

```
[x] I identified the subsystems this touches — effect-atom (curve, container, rarity, content hash),
    demon fusion, expeditions, soul economy, the web fusion panel, item lanes I1/I6/I7/I9/I12.
[x] I read every document named in the brief, in full, this session — including both sides of the
    Q3 contradiction (definitions §2 AND §4, and the container spec's Boundaries AND its A1 contract).
[x] I verified against CODE, not comments or docs: the six shard consumption sites, the zero-reader
    status of RarityRow.PoolRolls, the unconditional MultiplierAt(ownerLevel), the absence of any
    curve-input check in BindGate, and the explicit-column content-hash registry.
[x] I read the surrounding section of every rule I quoted — which is how §4 of definitions turned out
    to contradict §2 of the same file, and how the E5 Boundary turned out to be atom-scoped.
[x] I tested the constraints I report rather than assuming them: two suites run, 271 tests green,
    commands and counts in the status header.
[x] Every factual claim about the repo cites file:line.
[x] I named what I rejected and why — eleven rejected alternatives, each with the argument that beat it.
[x] I stated what I could not settle rather than picking to look decisive — five items.
[ ] Corrections propagated to the affected documents. **Deliberately not done**: the brief forbids
    editing another agent's file. The consequential-edits section is the propagation list, and R4
    (the owner session) applies it.
```
