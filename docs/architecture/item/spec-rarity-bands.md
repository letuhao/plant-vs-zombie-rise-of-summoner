# Spec: `rarity-bands`

**Module id:** `rarity-bands` · **Program:** [item](../item-map.md) · **Build order:** 7 of 21
**Depends on:** — (nothing in this program)
**Rulings:** **D7 (rule 7 lifted)**, D8, D15, D18, D26, **D29** · lane [ssot-rarity.md](ssot-rarity.md)

## Objective

Put ten rows in an empty table. Seed the **ten rungs**, each rung's **prefix and suffix bands**, and
the **`rarity_budget`** registry — then **re-derive the two per-rung tables that were authored against
the wrong ladder length**: I12's drop weights (written for **7** rungs) and I6's enhancement caps
(written for **5**).

**Users:** 8 (`affix-legality` — the tier window), 9 (`power-reads` — `ceilingFor`), 11
(`drop-volume`), 14–15 (the sinks price by rung), 17 (`uniques`).

## Design

### The table is real, empty, and narrower than the lane assumes

| Fact | Evidence |
|---|---|
| `rarity` exists with exactly six columns — `rarity_id · ordinal UNIQUE · prefix_rolls · suffix_rolls · min_tier · max_tier` | `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:54-61` |
| `RarityRow(string RarityId, int Ordinal, int PrefixRolls, int SuffixRolls, int MinTier, int MaxTier)` | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:163` |
| **Zero rows ship.** `data/seed/rarity/` holds one `README.md` and nothing else, and says so on purpose | `data/seed/rarity/README.md` |
| A seed reader already exists — `id · ordinal · prefixRolls · suffixRolls · minTier · maxTier` | `AtomSeedFile.ReadRarity`, `src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs:381-389` |
| The power-ceiling **reader** exists as a delegate; the table behind it does not | `ContentValidation.Budget(…, Func<string,int?> ceilingFor)`, `src/FusionRpg.Core/Effects/Atoms/Power/ContentValidation.cs:62-65` |

⛔ **The band is not representable.** `PrefixRolls` and `SuffixRolls` are single `int`s.
`ssot-rarity.md` §3.3's halves are **ranges** (`0–1`, `2–3`, …) and §5.3 N1 asked for a
`pool_rolls_max`; the columns that shipped are `prefix_rolls`/`suffix_rolls`, and no `_max` came with
them. **Seeding a range into this schema is impossible today.** Two ways out, and the second is
recommended:

| | Cost |
|---|---|
| Seed the **floor** only, treat the ceiling as fixed | Loses one of the three variances §3.5's overlap invariant is measured on. `U(n,1)` drops toward the bottom of the 5–30 % band — §5.3's own stated fallback |
| ✅ **Add `prefix_rolls_max` / `suffix_rolls_max`, nullable, defaulting to the floor** | Two nullable columns; NULL reproduces today's behaviour exactly. **Ask-first under E5's boundaries** — `spec-container-schema.md` publishes at Checkpoint B |

### ⛔ Two ordinal spaces, one ladder — say which one `rarity.ordinal` is

This has already produced a wrong claim in a shipped design document, so it is stated as a rule rather
than left to context.

| Space | Values | Where |
|---|---|---|
| **Registry / DB ordinal** | **10, 20, … 100**, spaced by 10 | `data/seed/items/_registry/core.v1.json` → `rarity.ladder[].ordinal`, `frozen: true`, append-only *"pre-spaced by 10 precisely so a future rung can be inserted"* |
| **C# enum member index** | **0 … 9**, consecutive | `src/FusionRpg.Core/Demons/DemonRarity.cs:16-28`; `DemonRarityLadder.RungCount = 10` |

**`rarity.ordinal` is the registry space, 10 … 100.** The enum member index is **not** an ordinal and
must never be written to that column. The enum already carries the warning in its own doc comment —
*"a bare `(int)r-1` meant 'one rung of four' before and 'one rung of ten' after, with no compiler
error either way"* (`DemonRarity.cs:10-15`) — and `DemonRarityLadder` exists so intent survives a width
change (`src/FusionRpg.Core/Demons/DemonRarityLadder.cs:10-53`).

⚠ **item-ideal §2f.1 F4's correction is about the *power-class* enum, not about `rarity.ordinal`.**
F4 is right that no C# roster spaces its members by 10; it does not repeal the frozen registry's
spacing, and D7's *"promotion reaches ordinal 100"* is written in the registry space. The two
statements are consistent once the spaces are named. **Naming them is this module's job.**

The join key between the two is the **string id**, and it already resolves both ways
(`DemonRarityIds.ToId` / `.TryParse`, `DemonRarity.cs:52,67`).

### ⚠ Two §3.3 rows do not sum to their published band — fix before seeding

`ssot-rarity.md` §3.3 publishes a combined *Count band* per rung and, added 2026-09-01, a prefix band
and a suffix band. Eight of ten halves sum correctly. **Two do not** (item-ideal §2g #10):

| Rung | Published band | Halves as written | Sum |
|---|---|---|---|
| `sprout` | **1–2** | `0–1` + `0–1` | **0–2** ✗ |
| `heirloom` | **3–4** | `2–2` + `2–2` | **4–4** ✗ |

⭐ **There is a derivation that fixes both and removes the chance of a third.** §3.4's ladder alternates
strictly — *odd steps widen the pool, even steps add an affix* — and that alternation is exact across
all ten rungs. So:

> **A window step keeps the halves of the rung below it. Only a count step may move them.**

| Step | Rung | Halves |
|---|---|---|
| — | `chaff` | 0 + 0 |
| count | `sprout` | **0–1 + 1–1 = 1–2** ← corrected (was `0–1 + 0–1`) |
| window | `grafted` | 0–1 + 1–1 = 1–2 |
| count | `cultivated` | 1–2 + 1–1 = 2–3 |
| window | `fused` | 1–2 + 1–1 = 2–3 |
| count | `chimeric` | 1–2 + 2–2 = 3–4 |
| window | `heirloom` | **1–2 + 2–2 = 3–4** ← corrected (was `2–2 + 2–2`) |
| count | `firstseed` | 2–3 + 2–2 = 4–5 |
| window | `sunwoven` | 2–3 + 2–2 = 4–5 |
| count | `almanac` | 3–3 + 2–3 = 5–6 |

Every row now sums to its published band, and the halves become **derivable from the alternation**
rather than authored twice. **Fix it before seeding: ordinals are append-only, so a post-seed
correction is a migration.**

⚠ `almanac`'s count step moves **both** halves (prefix `2–3 → 3–3`, suffix `2–2 → 2–3`) where every
other count step moves one. It still adds exactly one affix and still sums to 5–6, so it is legal —
recorded because it is the one row a reader will query.

### D7 lifted rule 7 — no rung is drop-only, on any axis

`ssot-rarity.md` §3.7 rule 7 capped promotion at ordinal 80, leaving `sunwoven` and `almanac`
drop-only. **item-ideal §2f.2 lifts it:** *"Promotion reaches ordinal 100, so no drop-only band exists
on any axis."* The audit's reason is the interesting half — with D8 gating aptitude affixes by rung,
rule 7 put **the strongest affix family behind luck**, which is the exact thing D7's *"cost, never
luck"* forbids.

**Consequences this module seeds:**

- `promote_from = 1` on **all ten** rungs. §4.4's row (*"1 for ordinals 10–70, 0 for 80–100"*) is
  stale.
- §3.7's other six rules stand unchanged, including rule 4 (new affixes roll in the item's recorded
  `tier_ceiling`, never a fresh one) and rule 6 (`promoted_from_ordinal` is marked).
- ⚠ §3.8's *"rung 100 must have at least one deterministic source"* **survives** and is now cheaper:
  promotion is one. **I12 still owns which** — this module registers the requirement, not the answer.
- Cost, not luck, means the promotion price is a **configurable soft cap** in `data/tuning/`, never a
  hard stop (AGENTS.md; `ssot-power-scale.md` §11 — *"a flat rate facing a scaling sink"* counts as a
  cap). Module 15 owns the curve.

### Re-derivation 1 — drop weights, authored against 7 rungs

`ssot-generation.md:416-423` gives seven rows out of 100,000. **One of them is not a rung at all:**
R7 `unique` is a container property, not a rarity (§3.6, D15 — *"a unique carries a rung like anything
else"*). So the source is **six** equipment rungs, not seven, and the unique row must be re-expressed
as a flag on the drop entry rather than a weight on the ladder.

**Method, so it can be re-run:** hold the two properties I12 actually measured — the bottom rung's
share and the rarest equipment rung's share — and interpolate **geometrically** over the nine pooled
rungs.

```text
w(chaff)   = the balancing row
w(sprout)  = a,   w(rung n) = a · rho^(n-1)   for the nine pooled rungs
pinned:    w(almanac) = 700    (I12's rarest equipment rung, R6 `relic` at 0.7%)
solved:    rho = 0.654,  a = 20,916,  sum(pooled) = 59,130
```

| Rung | Weight /100,000 | Share |
|---|--:|--:|
| `chaff` | 40,700 | 40.70 % |
| `sprout` | 21,000 | 21.00 % |
| `grafted` | 13,700 | 13.70 % |
| `cultivated` | 9,000 | 9.00 % |
| `fused` | 5,900 | 5.90 % |
| `chimeric` | 3,800 | 3.80 % |
| `heirloom` | 2,500 | 2.50 % |
| `firstseed` | 1,600 | 1.60 % |
| `sunwoven` | 1,100 | 1.10 % |
| `almanac` | 700 | 0.70 % |
| | **100,000** | |

**Check against I12's own measured property.** `ssot-generation.md:246` states *"rare-or-better is 28 %
of items"* — verifiable as R3+R4+R5+R6+R7 = 28,000. The ten-rung equivalent (`cultivated` and above)
is **24.6 %**, 3.4 points lower, and the gap is the ladder being longer rather than a change of
intent.

**These are starting values in `data/tuning/`, not code.** D18 governs how *many* items drop —
`Θ`, linear — and this table governs only *which rung* one is; D26 keeps both out of the business of
metering the player.

### Re-derivation 2 — enhancement caps, authored against 5 rungs

`ssot-enhancement.md:446-452` gives `Normal +4 · Magic +8 · Rare +12 · Epic +16 · Legendary/Unique +20`
— a **step of +4 per rung** — and says the table is *"open-ended: a future rarity rung above
Legendary/Unique adds a higher row… not a hard stop at +20."*

**The step is the design quantity; the ladder got longer, so the top gets higher.** Keeping +4 per
rung over ten:

| Rung | `enhance_cap` | | Rung | `enhance_cap` |
|---|--:|---|---|--:|
| `chaff` | +4 | | `chimeric` | +24 |
| `sprout` | +8 | | `heirloom` | +28 |
| `grafted` | +12 | | `firstseed` | +32 |
| `cultivated` | +16 | | `sunwoven` | +36 |
| `fused` | +20 | | `almanac` | +40 |

**This creates no ceiling**, and the arithmetic says why: `cap(item) = min(rarity_cap, ilvl_cap,
progression_cap)` with `ilvl_cap(ilvl) = max(4, 4 + ilvl/4)`, unbounded (`ssot-enhancement.md:437-438`).
At v1's content reach of ilvl 32, `ilvl_cap = 12` — so **`rarity_cap` binds only for `chaff`, `sprout`
and `grafted` today**, and the rest of the column is inert until the content ladder grows (**X5**).
Verified, and worth stating: a table that looks generous is mostly unreachable.

⛔ **But I1's registered constraint on `enhance_cap` is violated at the top of the ladder, and this is
a finding, not a re-derivation.** §4.4 constrains the key: *"total gain at cap ≤ one rung step in
expectation"*, and §9.5 sizes a rung step at **~+70 %**. I6 §3 delivers *"roughly 2× the item's own +0
magnitude"* at cap — a single figure. Against §7.3's measured ladder (ceiling 5: sprout 17 · grafted
39 · cultivated 65 · fused 133 · chimeric 187 · heirloom 379 · firstseed 487 · sunwoven 630 · almanac
770 hp) the adjacent-rung ratio is **not constant**: ~2.3× at the bottom, **~1.22× at the top**. So
2×-at-cap is ≈1.2 rung steps low on the ladder and **≈3 rung steps at the top**, where a maxed
`firstseed` would clear a natural `almanac`.

> **The fix is not a number this module owns.** `enhance_cap` must be **per rung and shrinking**, so
> that `gain(rung) ≤ step(rung)`. This module registers the constraint with the measured `step(rung)`
> table above; **module 15 `enhance-reroll` satisfies it**, and its own §7.3 worked example must be
> re-run against ten rungs.

### `rarity_budget` — the KV registry, and SC7 enforced

`rarity_budget(rarity_id, budget_key, value_int)`, keys validated against a closed code-side registry
naming each key's consumer. An unknown key rejects; **a key whose consumer has not shipped rejects**
rather than sitting inert. That is what makes *"a row no code consumes is a lie in a table"*
mechanical instead of aspirational.

| Key | Read by | Seeded now? |
|---|---|---|
| `promote_from` | 15 | ✅ **1 on all ten** (D7) |
| `pity_guarded` | 11 | ✅ 1 at ordinals 70 and 90 (§3.8) |
| `drop_weight_default` | 11 | ✅ the table above |
| `enhance_cap` | 15 | ✅ the table above, **with the shrinking-step constraint attached** |
| `power_ceiling` | 9, via `ContentValidation.Budget`'s `ceilingFor` | ⛔ **blocked on X6** — with all 20 coefficients flat at `CoeffMilli = 1000` a ceiling would be authored against a meaningless scale |
| `socket_min` / `socket_max` | 16 | ⛔ awaiting I4. ⚠ §4.4 requires it to declare whether the count is **rolled** — a rolled count is a fourth variance and moves every number in §3.5 |
| `set_eligible` | 12–13 | ⚠ **D15 makes this near-vacuous** — a set has no rarity and is completed from pieces of any rung. Seed `1` on all ten or drop the key; **module 13 decides** |
| `reroll_cost_mult` | 15 | ⛔ awaiting I7 — must scale with **affix count**, not rung alone (§9.7) |
| `salvage_yield` | 14 | ⛔ awaiting I9 — must **not** reuse `shard.{DemonRarity}` ids (`DemonMaterialCatalog.cs`); §9.8 suggests `dust.{rarity_id}` |
| `charm_potency` | 13 | ⛔ awaiting I10 |
| — | 11 (`level_req`) | **negative registration: rarity is not an equip gate** |

`rarity_budget` must join the content-hash registry, which is an explicit `contentHashSchemaVersion`
bump — the thing `definitions.md` §8 says must never happen silently.

### Two shipped-store defects this module must close

1. **`UpsertRarity` renumbers an existing rung.** It refuses an ordinal owned by a *different* id
   (`RpgStore.Containers.cs:147-151`) but the upsert body is
   `ON CONFLICT(rarity_id) DO UPDATE SET ordinal = excluded.ordinal` (`:159-167`) — so moving `fused`
   from 50 to 55 succeeds. Ordinals are load-bearing for sorting and for the budget lookup; the
   table's own comment says a reorder *"silently re-prices every container naming one."*
   `RarityLadderMutated` is the code (§6.1) and it does not exist.
2. **`effect_container.rarity` is free TEXT with no foreign key** (`:24`), and `ContainerValidator`
   never mentions rarity. `UnknownRarity` is the FK check the schema never had.

Both are **reviewed additions to definitions.md's closed 33-code list**, together with
`RarityBandViolated`. ⚠ §2b.1 resolved the reason-code question the other way — **one namespaced
`ContentRuleViolated`** rather than N new codes. **Take §2b.1**: three codes here become
`ContentRuleViolated{rarity.unknown | rarity.band | rarity.ladder-mutated}`, and no closed list grows.

### What is **not** decided, and who decides

| Open | Owner |
|---|---|
| The two `_max` columns (ask-first, Checkpoint B contract) | **owner**, via effect-atom E5 |
| `power_ceiling` values | **module 9**, after **X6** clears |
| A deterministic source for `almanac` | **module 11** (I12) |
| `set_eligible` — keep the key or drop it under D15 | **module 13** |
| A light-theme palette for the ten colours (§10.7) | **module 20 `item-surfaces`** |

## Commands

```powershell
dotnet run --project tools\AtomImporter -- data\seed\rarity        # 0 files today
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Rarity"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RarityLadder"
```

## Project structure

```text
data/seed/rarity/ladder.v1.json                   new — the ten rows AtomSeedFile.ReadRarity reads
data/tuning/item-rarity.v1.json                   new — drop weights, enhance caps: tunables, not code
src/FusionRpg.Core/Items/RarityLadder.cs          new — id <-> ordinal, the two-space rule, halves
src/FusionRpg.Core/Items/RarityBudgetKeys.cs      new — the closed key registry + consumer names
src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs  EDIT — rarity_budget; the ordinal-mutation refusal;
                                                    prefix_rolls_max / suffix_rolls_max (ask-first)
src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs  EDIT — read the two _max fields when they land
```

## Code style

```csharp
// Two ordinal spaces exist for one ladder and they are 10x apart. `rarity.ordinal` is the REGISTRY
// space (10..100, frozen in core.v1.json, pre-spaced so a rung can be inserted); DemonRarity's member
// index is 0..9 and is NOT an ordinal. The string id is the join. Writing (int)rarity here would put
// `almanac` at ordinal 9, below `chaff` at 10, and every sort in the game would invert.
public static int OrdinalOf(DemonRarity rarity) => Ladder[rarity.ToId()].Ordinal;   // never (int)rarity
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_ten_rungs_match_the_frozen_registry_exactly` | id and ordinal pairs against `core.v1.json`; a renumber fails CI |
| `rarity_ordinal_is_never_the_enum_member_index` | ⭐ the two-space rule, as a test rather than a comment |
| `every_rung_halves_sum_to_its_published_count_band` | the sprout and heirloom defects, **red before the fix** |
| `a_window_step_keeps_the_halves_of_the_rung_below` | the derivation, so a third defect cannot be authored |
| `promote_from_is_one_on_all_ten_rungs` | D7's lift; the old `0 at 80-100` row is gone |
| `an_existing_rungs_ordinal_cannot_be_changed_on_upsert` | closes `RpgStore.Containers.cs:159-167` |
| `a_container_naming_an_unknown_rarity_is_rejected` | the FK `effect_container.rarity` never had |
| `drop_weights_sum_to_100000_and_are_monotone_decreasing` | the re-derived table, exactly — no tolerance |
| `unique_is_not_a_rarity_rung` | I12's R7 was a container flag; §3.6, D15 |
| `enhance_cap_gain_never_exceeds_one_rung_step_at_any_rung` | ⭐ the constraint I1 registered and I6 breaks at the top |
| `ilvl_cap_binds_below_rarity_cap_at_ilvl_32` | the "mostly unreachable" claim, measured not asserted |
| `a_rarity_budget_key_with_no_shipped_consumer_is_rejected` | SC7, enforced |
| `power_ceiling_is_absent_while_the_coefficients_are_flat` | X6 — an authored ceiling on a flat scale is worse than none |
| `palette_lightness_is_monotone_under_a_deuteranope_transform` | §4.5's measured rule, carried forward |
| `no_rarity_id_collides_with_a_power_class_or_a_slot_role` | the two-axes guard (`spec-affix-power-class.md`) |

## Boundaries

**Always:** seed by string id and let the registry supply the ordinal; keep drop weights and enhance
caps in `data/tuning/`; register a `rarity_budget` key only when its consumer ships.

**Ask first:** the two `_max` columns; any `contentHashSchemaVersion` bump; changing a rung's colour,
name or count band after seeding.

**Never:** write a C# enum member index into `rarity.ordinal`. Never renumber a seeded ordinal — it is
a migration, not an edit. Never let rarity touch a magnitude: `CurveInput.Rarity` exists
(`CurveTable.cs`) and is banned on `container_kind = 'item'`. Never re-introduce a drop-only band —
D7 removed the last one.

## Success criteria

- [ ] Ten rows in `rarity`, ordinals `10 … 100` matching the frozen registry, seeded from
      `data/seed/rarity/`.
- [ ] Every rung's prefix and suffix halves sum to its published count band — `sprout` and `heirloom`
      corrected **before** the first seed.
- [ ] `promote_from = 1` on all ten; no rung is drop-only on any axis.
- [ ] Drop weights re-derived over ten rungs, summing to 100,000, with the method written down and
      `unique` removed from the ladder.
- [ ] Enhancement caps re-derived over ten rungs, and the **shrinking-step** constraint is registered
      with the measured `step(rung)` table and handed to module 15.
- [ ] `rarity_budget` exists, keys validate against a closed registry, and an unconsumed key rejects.
- [ ] An existing rung's ordinal can no longer be moved by an upsert.
