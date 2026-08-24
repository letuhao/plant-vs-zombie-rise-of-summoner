# Lane I1 SSOT — rarity

**Status:** Lane I1 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Read this session, in the contract's §5 order: [item-ideal.md](../item-ideal.md),
[enrichment-contract.md](enrichment-contract.md),
[../effect-atom/definitions.md](../effect-atom/definitions.md) §§1, 2, 4, 5, 10,
[../effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md),
[../effect-atom/atom-family-library.md](../effect-atom/atom-family-library.md). Code opened:
`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs`,
`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs`,
`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs`,
`src/FusionRpg.Core/Effects/Atoms/CurveTable.cs`,
`src/FusionRpg.Core/Demons/DemonRarity.cs`, `src/FusionRpg.Core/Demons/SummonRoller.cs`,
`src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs`.

**Everything numeric in §3.5 and §7 was simulated, not asserted.** The overlap this lane claims is a
measured property of the proposed bands, and the method is written down so it can be re-run.

---

## 2. Scope

### This lane owns

- The rarity ladder: how many rungs, their ids, display names, **append-only ordinals**, colours, and
  the readability rules that go with them.
- The **overlap mechanism** — the concrete reason a high-roll low rung can beat a low-roll high rung,
  and the measured invariant that keeps the overlap from swallowing the ladder (OD4).
- The **axis split**: what rarity controls versus what item level controls.
- **The registry** — the one place every other lane declares what it reads out of a rung, and who
  proposed the number (§4.4).
- Whether an item's rung may **change** after it drops, and in which direction (the *operation* is I6's).
- Whether **bad-luck protection** exists and what it may key on (the *counters* are I12's).
- Whether rarity is an equip gate. It is not. That negative is a registration, not an omission.

### This lane does NOT own

| Not ours | Owner |
|---|---|
| Affix tier bands, the affix pool, tier weights inside a window | **I8** |
| Socket counts per rung | **I4** proposes; we register |
| Set membership and set-bonus tiers | **I5** |
| The enhancement ceiling and the instance-mutation model | **I6** |
| Reroll cost | **I7** |
| Material and cost vocabulary, salvage yield semantics | **I9** |
| Charm potency | **I10** |
| Equip gating | **I11** |
| Drop rates, drop weights, and the loot-event → instance pipeline | **I12** |
| Bags, stacking, comparison UI | **I13** |
| Base types, implicits, and the hand-authored / unique flag | **I3** |

---

## 3. The model

### 3.1 The substrate — what already exists, verified against code

This lane is unusual: the table it owns **shipped, empty, and unread.**

| Fact | Evidence |
|---|---|
| A `rarity` table exists with `rarity_id`, `ordinal UNIQUE`, `pool_rolls`, `min_tier`, `max_tier` | `RpgStore.Containers.cs:52-58` |
| Ordinals are protected only against **collision**, not renumbering | `RpgStore.Containers.cs:79-82` refuses an ordinal owned by a *different* id; `ON CONFLICT(rarity_id) DO UPDATE SET ordinal = excluded.ordinal` (`:88-92`) moves an existing rung to any free ordinal |
| **No production code reads the table.** `ListRarities()` has exactly four callers, all in tests | `RpgStore.Containers.cs:105`; `tests/FusionRpg.Data.Tests/ContainerStoreTests.cs:194,215,224` |
| **No production code writes it either** — zero rungs are seeded anywhere | grep for `UpsertRarity` returns the definition plus test rows only |
| `effect_container.rarity` is **free TEXT with no foreign key**, and `ContainerValidator` never mentions rarity | `RpgStore.Containers.cs:23`; `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs` |
| The roll path reads the **container's** `pool_rolls`, never the rarity row's | `Instantiator.cs:128,139` |
| `CurveInput.Rarity` exists in shipped code — a curve *can* scale a magnitude by rarity ordinal | `CurveTable.cs:4-9` |
| `effect_instance` has **no rarity column**. Rarity currently lives on the template only | `RpgStore.AtomInstances.cs:56-63` |
| `DemonRarity` is a four-value C# enum, hard-coded into summon rates, pity, fusion trait slots, soul earn, and the `shard.{rarity}` material ids | `DemonRarity.cs:3-9`; `SummonRoller.cs:22-30`; `DemonMaterialCatalog.cs:18` |
| Summon pity already ships: `PityState(PullsSinceEpic, PullsSinceLegendary)`, epic hard 25, legendary soft ramp from 41, hard 55, 10-pull rare floor | `SummonRoller.cs:8-30,63-80` |

Three consequences drive the whole design:

1. **Nothing is locked in.** Reinterpreting a column on this table costs nothing today, because no row
   and no reader exists. Said out loud so nobody treats the shipped shape as a constraint it is not.
2. **`rarity` is today the exact SC7 defect the contract warns about** — a legal, validated table with
   zero consumers, the `status.expose.*` shape. Every column this lane keeps must name its reader.
3. **`effect_container.rarity` is a free string.** The contract says *"`rarity` already exists as a
   table with explicit append-only ordinals. It is not a free string"* (§1). In code, on the container,
   it is. Closing that is this lane's first validation rule (§6.1, `UnknownRarity`).

---

### 3.2 Two axes, and rarity is only one of them

**Item level (ilvl)** decides *how strong an affix may be*. **Rarity** decides *how many affixes, and
how far below the top the pool may reach*. They intersect at the moment of the drop.

```text
   ilvl  ──►  tierCeiling(ilvl) ∈ 1..5          "the world here is this dangerous"
   rung  ──►  [min_tier, max_tier]              "this object is this special"
                     │
   effective window = [min_tier, min(max_tier, tierCeiling)]
   effective count  = a roll inside the rung's [pool_rolls, pool_rolls_max] band
```

Rarity **never touches a magnitude.** That is the E5 boundary and this lane keeps it: the three things
a rung sets are a count band, a tier floor, and a tier ceiling. Nothing else.

Two axes rather than one, for a reason with teeth. With rarity alone, a top-rung drop from the tutorial
lawn carries top-tier affixes, and the drop table has to *fake* progression by choosing not to roll
high rungs early. With ilvl in the picture the refusal is **structural** — a rung whose tier floor sits
above the local ceiling has an *empty* effective window and cannot exist there at all. Rarity inflation
in low-level content becomes impossible rather than merely discouraged (§7.3).

### 3.3 The ladder: ten rungs

Ordinals are **spaced by 10** so a future rung can be inserted at 15 or 85 without renumbering anything
(§6.1, §8.2). The names are horticultural and fusion-flavoured, because a plant/zombie/fusion world
grades objects the way a nursery grades stock, not the way a dungeon grades swords.

| Ordinal | `rarity_id` | Display | What it is | Count band | Tier window | Colour | Pips |
|---|---|---|---|---|---|---|---|
| 10 | `chaff` | **Chaff** | husks, clippings, a dented bucket. Salvage fodder | 0 | — | `#63645d` | 1 |
| 20 | `sprout` | **Sprout** | it works. That is all it does | 1–2 | t1 | `#697a5c` | 2 |
| 30 | `grafted` | **Grafted** | one graft took and held | 1–2 | t1–t3 | `#509639` | 3 |
| 40 | `cultivated` | **Cultivated** | grown on purpose, to a plan | 2–3 | t1–t3 | `#37a39c` | 4 |
| 50 | `fused` | **Fused** | two natures in one object — the game's own word | 2–3 | t2–t4 | `#63a4ed` | 5 |
| 60 | `chimeric` | **Chimeric** | the fusion went further than intended | 3–4 | t2–t4 | `#c994ff` | 6 |
| 70 | `heirloom` | **Heirloom** | a line kept alive longer than its keepers | 3–4 | t3–t5 | `#ff94d2` | 7 |
| 80 | `firstseed` | **Firstseed** | from before the lawn | 4–5 | t3–t5 | `#ffab7a` | 8 |
| 90 | `sunwoven` | **Sunwoven** | made of sun, not of matter | 4–5 | t4–t5 | `#f9d464` | 9 |
| 100 | `almanac` | **Almanac** | it has a page. Everyone knows its name | 5–6 | t4–t5 | `#f3eaa0` | 10 |

**Rung 10 (`chaff`) is the only rung with no pool.** Base type plus implicit, `pool_rolls = 0` —
item-ideal §6.2's "Normal". It exists because salvage, promotion, and crafting all need a bottom.

### 3.4 Why exactly ten — the staircase, and why an eleventh breaks

Take the count bands a six-affix maximum allows — `1-2`, `2-3`, `3-4`, `4-5`, `5-6` — and the tier
windows five tiers allow — `[1,1]`, `[1,3]`, `[2,4]`, `[3,5]`, `[4,5]`. Five of each.

Now walk a monotone chain that changes **exactly one axis per step**. A 5 × 5 grid admits at most
`5 + 5 − 1 = 9` such steps, and with the pool-less rung underneath that is **ten rungs**. Ten is not a
taste call; it is the length of the longest legible chain the shipped machinery supports.

The one-axis-per-step rule is also the legibility rule. Every upgrade reads as exactly one sentence:

- **odd steps widen the pool** — "your affixes can come from a stronger band"
- **even steps add an affix** — "you get one more"

A tooltip never has to say both. The rung above yours is never a mystery.

**An eleventh rung was measured, not assumed.** Splitting a count step in half — inserting band `4-4`
between `3-4` and `4-5` — drove the adjacent upset rate to **37.6 %** and **38.8 %**, above the ceiling
in §3.5, meaning the rung label stopped predicting which of two items was better. That variant is why
the ladder stops at ten.

### 3.5 THE OVERLAP MECHANISM

OD4 requires overlap to be a mechanism, not a claim. It is the **product of three variances that
already live in shipped columns**, and no fourth mechanism is introduced:

| Variance | Range | Lives in | Owner |
|---|---|---|---|
| **Count inside the band** | e.g. 2–3 affixes | `rarity.pool_rolls` … `pool_rolls_max` (§5.3) | I1 |
| **Tier inside the window** | e.g. t2, t3 or t4 | `effect_container_pool.weight` across tiers | **I8** |
| **Magnitude inside the tier** | e.g. 40–50 hp at t3 | the atom's `ValueSpec {Min, Max, OnInstantiate}` | **I8** |

Multiply the three and adjacent rungs overlap heavily, rungs two apart overlap thinly, rungs three
apart barely touch. That is measurable, so it is measured.

**The overlap invariant — the number that makes OD4 real:**

> Let `U(n, k)` be the probability that a randomly rolled rung-`n` item beats a randomly rolled
> rung-`n+k` item, comparing total magnitude within one channel family.
>
> | | Required | Measured on the §3.3 bands |
> |---|---|---|
> | `U(n, 1)` | **5 % – 30 %** | 7.9 % – 28.3 % |
> | `U(n, 2)` | **≤ 10 %** | ≤ 7.9 % |
> | `U(n, 3)` | **≤ 2 %** | ≤ 1.6 % |
> | `U(n, 4)` | **≈ 0** | 0.0 % in 1.5 × 10⁵ trials |
>
> Below 5 % the overlap is decorative and OD4 is not honoured. Above 30 % the rung name stops
> predicting anything, and rarity has stopped meaning something.

Method, so it can be re-run: 2 × 10⁵ rolls per rung; one channel family (`vitality`, `maxHp` flat, hp
game units); tier uniform inside the window; magnitude uniform inside the tier; illustrative bands
t1 10–12 · t2 20–25 · t3 40–50 · t4 85–100 · t5 170–205, anchored on the two real numbers in
[atom-family-library.md](../effect-atom/atom-family-library.md) §2a (`vitality` t1 = 10 fixed,
t3 = 40–50); seed `20260822`.

**Single-family on purpose.** SC4 forbids adding hp to resolver points, so a cross-family total is not
a legal number. Cross-family comparison waits for E9's power vector (§9.13, SC9). Everything in this
document that adds magnitudes adds hp to hp.

### 3.6 What rarity is *not*

| Claim someone will make | This lane's answer |
|---|---|
| "Rarity should scale magnitudes" | No. `CurveInput.Rarity` exists in code (`CurveTable.cs:7`) and is **banned on `container_kind = 'item'`**. A multiplier on the rung makes rarity dominant and destroys the overlap the owner asked for |
| "Unique should be a rung" | No. A unique is a container with a fixed core and `pool_rolls = 0`; it **carries** a rung like anything else, so a rung-40 unique and a rung-90 unique are both real content. Making uniques the top rung is how D2 got uniques that were either mandatory or worthless. The flag itself is **I3's** |
| "Set should be a rung" | No — set membership is **I5's**, and a set piece carries an ordinary rung. Set *eligibility* reads a minimum ordinal from the registry (§4.4) |
| "Rarity should gate equipping" | No. Gating is `level_req` and I11's requirement gate. A rung is a description, never a permission |
| "Rarity should be the sort key" | Only as a tiebreak. I13's comparison sorts by fit, then power, then ordinal — never by ordinal alone (§9.11) |

### 3.7 Promotion — the rules this lane owns

I6 owns the operation and its cost. I1 owns whether it is legal, and these seven constraints are the
legality:

1. **Upward only.** A promotion that lowers the ordinal is `RarityDemotion`.
2. **One rung per operation.** No skipping. It keeps cost curves monotone and the UI honest.
3. **Existing affixes are never re-rolled and never re-tiered.** Promotion only **adds**. Re-rolling is
   I7's separate operation.
4. **New affixes roll in the new rung's window ∩ the item's recorded `tier_ceiling`** — never in a
   fresh ceiling. An item does not get stronger because the player got stronger.
5. **The count target is the new rung's band floor.** Reaching the band ceiling is I7's territory.
6. **A promoted item is marked** (`promoted_from_ordinal`). Measured reason: a Cultivated promoted to
   Fused averages **59 % of a natural Fused** (§7.2). The mark explains the gap instead of hiding it,
   and it preserves *found beats crafted* without a hidden penalty.
7. **Promotion tops out where `promote_from = 0` first appears on the rung table** — today that is
   ordinal 80 (Firstseed), leaving Sunwoven and Almanac drop-only so the top of the ladder stays a
   reason to play content rather than a reason to grind currency. **This is a data row, not a
   constant**: `promote_from` is a per-rung registry entry (§4.4), not a hardcoded ordinal in code, so
   raising or removing the ceiling — including the "90, or no ceiling with a steep cost curve"
   alternative §10 open question 4 raises — is a table edit when the owner answers it, never a code
   change. Reconciled 2026-08-24 against AGENTS.md's no-hard-progression-ceilings rule: the
   *mechanism* is already the required configurable soft cap; only *where the owner sets it* is still
   open (§10 item 4, unchanged by this pass). Promotion's **cost**, separately, is I6's to own (§3.7
   preamble) — the same rule applies there once I6 specs it: a per-rarity table value, not a formula
   constant.

**Where the rung lives, and how SC5 survives.** Rarity moves off the container and onto the **item row**
(`item.rarity_id`, initialised from the container at drop). One rule with no special case: the
container's rarity is the *template's*, the item's rarity is the *item's*. Determinism holds by SC5's
own answer — the item's state is `origin seed + catalog_revision + an ordered, recorded list of
operations`, and every promotion appends one entry. **I6 owns that log**; this lane registers that
promotion must be in it and must never silently re-roll.

### 3.8 Bad-luck protection

The repo already ships the right shape: `PityState(PullsSinceEpic, PullsSinceLegendary)` — per-player,
persisted, cross-banner, visible — with an epic hard pity at 25, a legendary soft ramp from pull 41, a
hard pity at 55, and a 10-pull rare floor (`SummonRoller.cs:8-30,63-80`).

**Items reuse the shape, not the counters, and only on counted sources.**

| Rule | Why |
|---|---|
| Pity may key on **rung only** — never on roll quality, never on tier | A quality pity makes draws non-independent, and §3.5's invariant is measured on independent draws. It also teaches players to bank drops |
| **Two guarded rungs: 70 (Heirloom) and 90 (Sunwoven)** | Mirrors the summon precedent's two counters. Ordinal 100 is deliberately unguarded |
| **Rung 100 must have at least one deterministic source** | An unreachable top rung is a frustration, not a fantasy. If it is not pity-guaranteed it must be quest- or boss-guaranteed. I12 owns which |
| Pity counters apply to **counted sources only** — expedition completion, boss kill, chest open | A shared counter over a high-volume incidental drop stream converts into "farm trash mobs to force a Sunwoven" |
| **Incidental drops get a floor, not a counter** — every Nth incidental drop is at least rung 30 | Same device as the shipped 10-pull rare floor (`SummonRoller.Roll`, the `floorRare` slot). Cheap, and not exploitable |
| Counters, thresholds, and which sources count | **I12 owns.** This lane owns only what pity may key on |

---

## 4. Options considered, and the recommendation

### 4.1 How long a ladder

| Option | Shape | Verdict |
|---|---|---|
| **A — four rungs, shared with `DemonRarity`** | common / rare / epic / legendary | Rejected. One enum for everything and no new table, but it defies OD4's "long ladder", and growing `DemonRarity` to ten breaks summon rates, pity thresholds, `FusionRoller.SlotsFor`, `SoulEarnPolicy.DiscoveryDelta`, and the `shard.{rarity}` material ids — five consumers, for zero item-side gain |
| **B — five rungs, D2 shape** | normal / magic / rare / set / unique | Rejected. Familiar, but it mixes a *power* axis with two *authoring* axes (set, unique), which is exactly the confusion §3.6 exists to prevent |
| **C — ten-rung staircase** ✅ | §3.3 | **Recommended.** Longest legible chain the machinery admits (§3.4), and the alternating steps give a one-sentence upgrade story |
| **D — no rungs, a continuous quality score** | one 0–1000 number | Rejected. Maximum gradation, zero legibility: nothing to name in a tooltip, nothing for I12 to weight, nothing for I4/I6 to key a budget on. A score without rungs moves the whole registry problem into a formula |

### 4.2 How the overlap is produced

| Option | Mechanism | Verdict |
|---|---|---|
| **1 — a separate rolled "quality" axis** | roll 0–1000 at drop, multiply every magnitude | Rejected. It is a **second effect mechanism** (SC1), it needs a new instance column, and multiplying frozen values re-opens the SC5 reproducibility question — for variance that already exists inside the tier bands |
| **2 — overlapping tier windows only** | count fixed per rung | Rejected. Halves the ladder length (5 windows → 6 rungs) and makes every step a window step, killing the one-sentence upgrade story |
| **3 — overlapping count bands only** | one window for everyone, ilvl sets it | Rejected on measurement. Ten count-only rungs put adjacent steps about 12 % apart in expectation and adjacent upsets near 45 % — above §3.5's ceiling. This is the "overlap so wide rarity stops meaning anything" failure, arrived at by construction |
| **4 — all three variances stacked, plus a derived readout** ✅ | §3.5 | **Recommended.** No new mechanism, no new roll, every variance already in a shipped column, and the result is measurable |

**On the "quality roll" the brief asks about:** it ships as a **derived readout, never a roll.**

> **Roll quality `Q`** (integer per-mille) = the mean over an item's rolled atoms of
> `1000 × (value − min) / (max − min)`, taken per atom from its own value spec.
>
> Unit-safe by construction — each term is a dimensionless fraction, so hp and resolver points never
> meet. Computed from frozen `values_json` plus the catalog; stored nowhere; costs no schema.

`Q` is what a player means by "did it roll well". A `Q = 970` Grafted is visibly a trophy, and that is
half of what keeps low rungs from being dead content (§8.1).

A second readout — **rung fill `F`**, where the item sits inside its rung's whole envelope, counting
count and tier as well as magnitude — needs magnitudes summed across families, so it is **not
computable until E9's power vector lands.** Per SC9 that is stated as a want, not a dependency: the
design ships without it (§9.13).

### 4.3 One ladder or several

| Option | Verdict |
|---|---|
| Parallel ladders per category (gear / gem / charm / material) | Rejected. Four palettes, four sort orders, and an unanswerable "is an epic gem better than a Fused sword" |
| **One ladder; every category free to use a subset of the rungs** ✅ | **Recommended.** The `ordinal` orders everything, and the count/tier columns are simply unread by categories with no pool (§6.2) |
| Share `DemonRarity` | Rejected — §4.1 option A |

**Non-equipment, concretely.** Materials, gems, charms and consumables all use the same ten rungs; they
read only `ordinal`, `color_hex`, `pip_count` and `display_key`, because those are the columns whose
consumer (the UI) always exists. Nothing forces every category to use all ten — materials will
plausibly stop at 70.

**Demons keep their own ladder.** `DemonRarity` stays a four-value code enum, and a one-way band map
lets the two be compared without being fused:

| `DemonRarity` | Item ordinal band |
|---|---|
| `Common` | 10 – 30 |
| `Rare` | 40 – 60 |
| `Epic` | 70 – 80 |
| `Legendary` | 90 – 100 |

Four rows, no table needed. The map exists so a legendary demon's drop table can be written in item
rungs and so `shard.legendary` can be priced — **not** so the two ladders can be merged later.

### 4.4 THE REGISTRY — a table, not a widening row

Every consumer lane needs *some* number per rung. Two ways to hold them:

- **Wide columns on `rarity`** — one per consumer. Cheap to read, but it makes a Checkpoint-B-published,
  content-hashed table grow every time a lane lands, and a column whose lane has not shipped is an SC7
  lie sitting in the schema.
- **A key/value table with a closed key registry** ✅ — `rarity_budget(rarity_id, budget_key, value_int)`,
  where `budget_key` validates against a code-side registry naming each key's **consumer lane**. An
  unknown key rejects (`UnknownParam`); a key whose consumer has not shipped rejects
  (`ParamNotImplemented`) rather than sitting inert.

**Recommendation: the KV table.** It *is* the registry, as data, and it makes SC7 enforceable rather
than aspirational — a rung budget cannot be authored before something reads it.

**The registry contents.** Rows marked *awaiting* are that lane's to propose; this lane holds the slot,
the key name, and the constraint the value must satisfy.

| Value | What it means | Read by | Proposed by | Status |
|---|---|---|---|---|
| *(columns on the `rarity` row itself)* | | | | |
| `pool_rolls` / `pool_rolls_max` | affix **count band** | I12 (generation), I8 (pool authoring) | **I1** | set — §3.3 |
| `min_tier` / `max_tier` | the **tier window**, before ilvl narrowing | I12, I8 | **I1** | set — §3.3 |
| `color_hex`, `pip_count`, `display_key` | the three UI channels | I13, launcher overlay | **I1** | set — §3.3 |
| *(keys in `rarity_budget`)* | | | | |
| `socket_min` / `socket_max` | sockets a rung grants | I4 | **I4** | awaiting — must also declare whether the count is *rolled*, because a rolled socket count is a fourth variance and moves every number in §3.5 |
| `set_eligible` | 0/1 — may a piece of this rung belong to a set | I5 | **I5** | awaiting |
| `enhance_cap` | enhancement ceiling | I6 | **I6** | awaiting — constrained: total gain at cap ≤ one rung step in expectation (§9.5) |
| `promote_from` | 0/1 — may an item at this rung be promoted upward | I6 | **I1** | set — 1 for ordinals 10–70, 0 for 80–100 (§3.7 rule 7) |
| `reroll_cost_mult` | reroll price multiplier | I7 | **I7** | awaiting — must also scale with **affix count**, not rung alone (§9.7) |
| `drop_weight_default` | baseline weight per source | I12 | **I12** | awaiting |
| `pity_guarded` | 0/1 — does a counter guarantee this rung | I12 | **I1** picks the rungs, **I12** the thresholds | set — 1 at 70 and 90 (§3.8) |
| `salvage_yield` | material quantity on salvage | I9 / I13 | **I9** | awaiting — must **not** reuse `shard.{DemonRarity}` ids (§9.8) |
| `charm_potency` | charm scaling | I10 | **I10** | awaiting |
| — | **nothing.** Rarity is not an equip gate | I11 | **I1** | set — negative registration |

### 4.5 Colour, and the readability argument

Ten categorical hues exceed what anyone reliably distinguishes, and roughly 8 % of men cannot separate
red from green at all. So **hue is never the channel that carries the ordering.**

Three redundant channels, in priority order:

1. **Lightness (`L*`) is the ladder.** The §3.3 palette is strictly monotone in CIE `L*`:
   42.1 → 49.1 → 55.9 → 61.1 → 66.0 → 70.0 → 74.2 → 77.2 → 86.1 → 91.9. "Brighter is higher" survives
   total colour blindness, and it is the only ordering claim the palette makes.
2. **Pip count.** One to ten pips, countable, no colour involved. This is what a colour-blind player
   actually reads.
3. **The rung name, always in text** in the tooltip header. Never colour alone.

**Measured, not asserted.** Through a Viénot deuteranope transform the palette stays monotone —
42.2 → 48.6 → 54.5 → 59.5 → 65.0 → 71.2 → 76.8 → 79.0 → 87.0 → 92.2 — so the ordering channel does not
invert for the largest colour-vision deficiency.

Testable rules, because a palette without them drifts on the first UI pass:

| Rule | Value |
|---|---|
| `L*` strictly increasing with ordinal | required |
| adjacent-rung `ΔL*` | **≥ 2.5** (min measured 2.9, Heirloom → Firstseed) |
| distance-2 `ΔL*` | **≥ 7** (min measured 7.2) |
| monotone under deuteranope and protanope simulation | required |
| comparison UI encoding "better" in hue alone | **forbidden** |

**Honest limitation:** these hexes are tuned for a dark surface. A light-theme palette is a separate
pass and is not in this document (§10.7).

### 4.6 Can a rung change after the drop

| Option | Verdict |
|---|---|
| **Immutable** — the rung is fixed at drop | Rejected. Every low-rung drop becomes terminal garbage, and the item system loses its single best sink |
| **Free movement** — promote and demote | Rejected. Demotion destroys player property, and a two-way rung makes the label meaningless |
| **Upward only, one rung at a time, recorded** ✅ | **Recommended** — the seven rules in §3.7 |

---

## 5. Data shape

### 5.1 Reused as-is

| Column | Table | Used for |
|---|---|---|
| `rarity_id`, `ordinal` | `rarity` | identity and ordering |
| `min_tier`, `max_tier` | `rarity` | the rung's tier window, before ilvl narrowing |
| `rarity` | `effect_container` | the template's rung — **gains the FK check it never had** (§6.1) |
| `min_tier`, `max_tier`, `pool_rolls` | `effect_container` | the template's concrete values, validated inside its rung's bands |
| `roll_seed`, `catalog_revision` | `effect_instance` | unchanged; SC5 |
| `weight`, `group` | `effect_container_pool` | tier weighting inside the window — **I8's** |

### 5.2 Reinterpreted

| Column | Was | Becomes | Safe because |
|---|---|---|---|
| `rarity.pool_rolls` | a single count | the **band floor** | The table holds zero production rows and has zero production readers (§2). Reinterpretation costs nothing today and will never be cheaper |

### 5.3 New

| # | Change | Consumer | Notes |
|---|---|---|---|
| N1 | `rarity.pool_rolls_max INTEGER`, nullable, defaults to `pool_rolls` | `Instantiator`, I12 | Null means a fixed count, so the shipped behaviour stays reachable |
| N2 | `rarity.color_hex TEXT NOT NULL`, `rarity.pip_count INTEGER NOT NULL`, `rarity.display_key TEXT NOT NULL` | I13, launcher overlay | The three UI channels of §4.5. `display_key`, not a literal, so the name is localisable |
| N3 | `rarity.enabled INTEGER NOT NULL DEFAULT 1` | importer, I12 | Content is **disabled, never deleted** (definitions §6). The table has no way to retire a rung today |
| N4 | `rarity_budget(rarity_id TEXT, budget_key TEXT, value_int INTEGER, PRIMARY KEY (rarity_id, budget_key))` | §4.4's whole right-hand column | Keys validated against a closed code-side registry naming each key's lane |
| N5 | `Instantiator` takes a **`tierCeiling`** and a **count band**, drawing the count from `roll_seed` before drawing atoms | I12 | Additive. Ceiling narrowing must run *before* the drawable-group check, or a narrowed pool under-fills silently |
| N6 | Four columns on the **item row** above the instance: `rarity_id`, `item_level`, `tier_ceiling`, `promoted_from_ordinal` | I6, I12, I13 | **This lane does not own that table.** Ownership is unresolved between I3 and I13 (§9.10, §10.3) — we register the columns rarity needs on it |

**Content hash.** `rarity` is already a covered table (definitions §8). **`rarity_budget` must join the
covered registry, which is an explicit `contentHashSchemaVersion` bump** — precisely the thing
definitions §8 says must not happen silently.

**Ask-first, per E5's boundaries** (*"adding a column"*, *"changing the rarity/tier split"*): N1–N5 and
the `UnknownRarity` FK check all change a Checkpoint-B-published contract. They are additive and
nullable-defaulted, but they are asks, and §10.1 puts them in front of the owner.

**A fallback that needs no schema change at all**, if the asks are refused: drop N1 and N5, fix each
template's count, and carry the whole overlap on the tier window plus the within-tier roll range. The
measured cost is one variance source — adjacent upsets fall toward the bottom of §3.5's band, and the
ladder gets sharper and less forgiving. It works; it is just less of what OD4 asked for.

---

## 6. Validation and reason codes

### 6.1 Bad input → reason code

| Bad input | Reason code | New? |
|---|---|---|
| `effect_container.rarity` names an id not in the `rarity` table | **`UnknownRarity`** | new — the FK the schema never had (`RpgStore.Containers.cs:23`) |
| Container `pool_rolls` outside its rung's `[pool_rolls, pool_rolls_max]` | **`RarityBandViolated`** | new |
| Container `[min_tier, max_tier]` outside its rung's window | **`RarityBandViolated`** | new |
| A drop whose rung floor exceeds `tierCeiling(ilvl)` — empty effective window | **`RarityBandViolated`** | new |
| An existing rarity row's `ordinal` changes on upsert | **`RarityLadderMutated`** | new — today it succeeds (`RpgStore.Containers.cs:88-92`) |
| A rarity row is deleted while a container names it | **`RarityLadderMutated`** | new. Retirement is `enabled = 0` (N3) |
| Two rarity rows share an `ordinal` | `DuplicateKey` | existing — the store returns a prose reason today; promote it to a code |
| `ordinal ≤ 0`, `pip_count ≤ 0`, `min_tier > max_tier`, `pool_rolls_max < pool_rolls` | `BadParamValue` | existing |
| `rarity_id` fails `^[a-z][a-z0-9-]*$` | `BadParamValue` | existing |
| A curve with `input = Rarity` referenced by an `item` container | `BadCurve` | existing — §3.6's ban, using the code for a curve illegal in context |
| A pool atom's tier outside the effective window | `TierOutOfWindow` | existing |
| Count-band floor exceeds drawable groups **after** ilvl narrowing | `PoolRollsExceedGroups` | existing — re-checked post-narrowing, not only at author time |
| `rarity_budget.budget_key` not in the closed key registry | `UnknownParam` | existing |
| A `rarity_budget` row for a key whose consumer lane has not shipped | `ParamNotImplemented` | existing — SC7, enforced |

**Three new codes owned by this lane:** `UnknownRarity`, `RarityBandViolated`, `RarityLadderMutated`.
Definitions §10 calls its 33-code list closed and adding one a reviewed change; this is that request.

**Two more are proposed to I6**, which owns the mutation operation and should name them:
`RarityDemotion` (§3.7 rule 1) and `RarityCeilingExceeded` (§3.7 rule 7).

### 6.2 What each consumer is allowed to read

SC7 in table form. A category with no pool never reads a pool column, and that is not an omission.

| Container kind | Reads | Never reads |
|---|---|---|
| `item` (equipment) | everything | — |
| `gem` (I4 insert) | ordinal, colour, pips, name, socket budgets | count band, tier window |
| `charm` (I10) | ordinal, colour, pips, name, `charm_potency` | count band, tier window |
| Material / consumable / currency *(plain rows, no container)* | ordinal, colour, pips, name | everything else |
| `set` (I5 tier) | ordinal, `set_eligible` | count band, tier window |

### 6.3 Guard tests this lane owes

| Test | Asserts |
|---|---|
| `Rarity_ordinals_match_the_golden_ladder` | the ten `(rarity_id, ordinal)` pairs, exactly, in order — a renumber fails CI |
| `Rarity_ordinal_cannot_be_changed_on_upsert` | `RarityLadderMutated`; closes `RpgStore.Containers.cs:88-92` |
| `Container_naming_an_unknown_rarity_is_rejected` | `UnknownRarity` |
| `Adjacent_rung_upset_rate_is_within_band` | seeded sweep, `U(n,1) ∈ [5 %, 30 %]`, **exact counts** per definitions §11 (*"a tolerance on a seeded test is an invitation to widen it"*) |
| `Distance_three_upset_rate_is_under_two_percent` | seeded sweep |
| `Palette_lightness_is_monotone` | `L*` strictly increasing, adjacent `ΔL* ≥ 2.5`, distance-2 `ΔL* ≥ 7`, monotone under a deuteranope transform |
| `Rarity_curve_input_is_rejected_on_item_containers` | `BadCurve` |
| `Every_rarity_budget_key_names_a_shipped_consumer` | `UnknownParam` / `ParamNotImplemented` — the SC7 guard |
| `Promotion_never_lowers_the_ordinal` | `RarityDemotion` (I6 hosts it; this lane supplies the case) |

---

## 7. Worked examples

**Illustrative, not balanced.** Every number below is hp game units on the `vitality` family, using the
§3.5 bands. I8 owns the real bands, and when they change these numbers change with them.

### 7.1 The OD4 upset — a top-roll Grafted beating a Fused

Two rungs apart: **Grafted (30)** rolls 1–2 affixes in t1–t3; **Fused (50)** rolls 2–3 affixes in t2–t4.
Same item level for both — tier ceiling 5, so neither window is narrowed.

| | Grafted (30) | Fused (50) |
|---|---|---|
| Best possible roll | 2 × t3 at 50 hp = **100 hp** | 3 × t4 at 100 hp = **300 hp** |
| Worst possible roll | 1 × t1 at 10 hp = **10 hp** | 2 × t2 at 20 hp = **40 hp** |
| Mean | **39 hp** | **133 hp** |
| Median | 37 hp | 132 hp |
| 95th percentile | 85 hp | — |
| 5th percentile | — | 48 hp |

**The upset, in units.** A top-roll Grafted carries **100 hp**: two affixes, both drawn at tier 3, both
at the top of the 40–50 band. A bottom-roll Fused carries **40 hp**: two affixes, both drawn at tier 2,
both at the bottom of the 20–25 band. The Grafted is **2.5× the Fused**, two rungs below it.

**And it is not a freak.** Measured over 4 × 10⁵ pairs:

- `P(random Grafted beats random Fused)` = **3.94 %** — about **1 in 25**.
- **29.8 %** of all Fused items fall below the Grafted *ceiling* of 100 hp.
- **49.9 %** of all Grafted items sit above the Fused *floor* of 40 hp.

The rung still means something — Fused averages 3.4× Grafted — while the distributions genuinely
interpenetrate. That is OD4, as a measurement rather than a promise.

Displayed, the top-roll Grafted reads **`Q = 1000`** and the bottom-roll Fused reads **`Q = 0`** (§4.2),
which is how a player is told *why* the worse-looking item is the better one.

### 7.2 Promotion — Cultivated → Fused, against a natural Fused

A **Cultivated (40)** rolled 2 affixes in t1–t3 (mean 65 hp). Promoted one rung to **Fused (50)** per
§3.7: the two existing affixes are untouched, and the operation adds affixes up to Fused's band floor,
drawn in t2–t4 at the item's recorded ceiling.

| | Mean | Share of a natural Fused |
|---|---|---|
| Cultivated before promotion | 65 hp | 49 % |
| The same item, promoted to Fused | **79 hp** | **59 %** |
| A natural Fused | 133 hp | 100 % |

`P(promoted beats natural)` = **19.8 %**.

Two properties fall out, and both are wanted:

- Promotion is **worth doing** — +21 % on the item, and it moves the label.
- Promotion is **not a substitute for finding one** — a promoted Fused averages 59 % of a found Fused,
  because its first two affixes were rolled in a lower window and stay there. *Found beats crafted*,
  with a number behind it. The `promoted_from_ordinal` mark (§3.7 rule 6) is what tells the player so.

### 7.3 Item level as the second axis, and why a high rung cannot drop in low content

Same ladder, four tier ceilings. Only the effective window changes; mean magnitude in hp.

| Ordinal | Ceiling 5 | Ceiling 4 | Ceiling 3 | Ceiling 2 |
|---|---|---|---|---|
| 20 Sprout | t1 · 17 | t1 · 17 | t1 · 17 | t1 · 17 |
| 30 Grafted | t1–3 · 39 | t1–3 · 39 | t1–3 · 39 | t1–2 · 25 |
| 40 Cultivated | t1–3 · 65 | t1–3 · 65 | t1–3 · 65 | t1–2 · 42 |
| 50 Fused | t2–4 · 133 | t2–4 · 133 | t2–3 · 84 | t2 · 56 |
| 60 Chimeric | t2–4 · 187 | t2–4 · 187 | t2–3 · 118 | t2 · 79 |
| 70 Heirloom | t3–5 · 379 | t3–4 · 241 | t3 · 158 | **cannot drop** |
| 80 Firstseed | t3–5 · 487 | t3–4 · 310 | t3 · 203 | **cannot drop** |
| 90 Sunwoven | t4–5 · 630 | t4 · 416 | **cannot drop** | **cannot drop** |
| 100 Almanac | t4–5 · 770 | t4 · 509 | **cannot drop** | **cannot drop** |

Two things this buys:

1. **Rarity inflation in low content is structurally impossible.** A Sunwoven needs t4, a ceiling-3
   world has no t4, so its effective window is empty and the drop is rejected (`RarityBandViolated`).
   I12 does not have to *choose* not to roll high rungs early — it cannot.
2. **Item level is worth about four rungs.** The strongest item that can exist at ceiling 2 is a
   Chimeric at 79 hp, which sits between a Cultivated (65) and a Fused (133) from ceiling 5 — four
   rungs below the top of the ladder. Two real progression axes, not one axis wearing two hats.

The ladder never degenerates: at every ceiling from 2 to 5 the adjacent upsets stay inside §3.5's band
(measured range 7.9 %–29.9 %) and no two rungs collapse into each other.

**A design rejected on exactly this table.** Making the window *relative* to the ceiling — each rung
declaring a width below the top rather than absolute tiers — looks elegant and fails hard: at ceiling 3,
widths 3, 4 and 5 all clamp to `[1,3]`, so rungs 20 and 30 become **identical items with different
names** and their upset rate is 49.8 %, a coin flip. Measured, then discarded.

### 7.4 The registry in use — resolving one Fused item

What a drop of `item.plate-helm.fused` actually reads, and from whom:

```text
rarity row 'fused'         ordinal 50 · count 2-3 · window t2-t4
                           color #63a4ed · pips 5 · display_key rarity.fused
rarity_budget('fused',…)   promote_from     = 1            ← I1
                           pity_guarded     = 0            ← I1
                           socket_min/max   = (awaiting I4)
                           enhance_cap      = (awaiting I6)
                           reroll_cost_mult = (awaiting I7)
                           salvage_yield    = (awaiting I9)
item row                   item_level 52 → tier_ceiling 4
effective window           [2, min(4,4)] = t2-t4
effective count            roll_seed → 3 affixes
```

The four *awaiting* rows are why §4.4 is a KV table and not five nullable columns: nothing is authored
until something reads it, and the reject is `ParamNotImplemented` rather than a NULL nobody notices.

---

## 8. Failure modes

### 8.1 Rarity inflation makes low rungs dead content

**The failure:** by the endgame the drop tables have shifted upward. Chaff, Sprout and Grafted stop
appearing, or appear and are vendored unread. Three-tenths of the ladder is decoration.

**What is designed against it:**

- **Low rungs are the best crafting bases, not the worst items.** A Grafted has one or two affixes, so
  promotion and reroll have almost nothing to fight. This is the mechanism PoE's entire crafting
  economy runs on, where white bases are among the most valuable items in the game *(recalled, not
  verified)*. It only works if **I7 scales reroll cost with affix count, not with rung alone** (§9.7) —
  without that, a low rung is cheap to *own* and expensive to *use*, and the mechanism inverts.
- **`Q` makes a good low roll visible.** A `Q = 970` Grafted is a trophy with a number on it, not a
  green item the player scrolls past.
- **The floor rule** (§3.8) keeps rung 30 permanently in the incidental stream.

**And the honest limit.** Low rungs stay alive as *inputs*; they will not stay competitive as *finished
items* at high ilvl, because their window caps below the ceiling by construction. That is the same
answer every shipped ARPG reached, and pretending otherwise would mean giving low rungs access to top
tiers, which collapses the ladder — measured, in §7.3's rejected relative-window design. The part that
is genuinely I12's: **drop weights must keep low rungs in the stream, not taper them to zero.**

### 8.2 Too many rungs make upgrades illegible

**The failure:** ten names, and no player can say what the next one gives them.

**What is designed against it:** the one-axis-per-step rule (§3.4). Every step is *one more affix* or
*a stronger band*, never both, so the tooltip is one sentence. Ten is the longest chain that rule
admits, and an eleventh rung was **measured** to push adjacent upsets to 37.6 %–38.8 % — past the point
where the label predicts anything. Pips give a countable ordering that needs no name at all. And
because ordinals are spaced by 10, adding a rung later is an insertion, not a renumbering.

### 8.3 Overlap so wide that rarity stops meaning anything

**The failure:** rungs overlap so much that the player learns to ignore the colour.

**What is designed against it:** the §3.5 invariant is a number with a seeded test behind it, not a
feeling — `U(n,1) ≤ 30 %`, `U(n,2) ≤ 10 %`, `U(n,3) ≤ 2 %`. Adjacent rungs genuinely interpenetrate;
three rungs apart they do not. And the invariant is **fragile in a specific, named way**: it depends on
I8's tier weights and within-tier widths (§9.1), which is exactly why the test belongs in CI, where an
affix-band edit trips it instead of shipping.

### 8.4 Colour palettes that fail for colour-blind players

**The failure:** the rung is encoded in hue, ten hues are too many, and 8 % of players cannot separate
the two that matter to them.

**What is designed against it:** hue is never the ordering channel (§4.5). Lightness is, it is monotone,
and it stays monotone through a deuteranope transform — measured. Pips are the redundant discrete
channel, the name is always in text, and encoding "better" in hue alone is a forbidden pattern with a
test attached.

### 8.5 Rarity-as-magnitude creep

**The failure:** somebody attaches a `CurveInput.Rarity` curve to an item affix "so higher rarities feel
better". Rarity becomes multiplicative, dominates count and tier, and the overlap dies overnight.

**Why this is a real risk and not a hypothetical:** the enum value exists in shipped code
(`CurveTable.cs:7`), definitions §2 documents it as legal machinery reading the container's rarity
ordinal, and the prohibition lives only in E5's *Boundaries* section — which definitions outranks. So
the ban is written here as a rule with a reason code (`BadCurve` on `container_kind='item'`) and a guard
test, and the contradiction between the two documents is raised in §10.6 rather than left for someone
to discover at build time.

### 8.6 A higher rung that is strictly worse

**The failure:** rung n+1 offers a *different, worse* pool, so the upgrade is a downgrade. This is how
smart-loot systems produce items whose rarity is an insult.

**What is designed against it:** rarity never changes *which* families the pool offers — only how many
are drawn and which tiers are legal. A rung-n+1 draw is a superset draw from the same pool. The pool is
**I8's**, and preserving that property is the one thing this lane needs I8 to guarantee (§9.2).

---

## 9. What this lane needs from other lanes

1. **I8 — the tier weights inside a window, and the width of a tier band.** §3.5's invariant is
   measured with tiers uniform inside the window and magnitudes uniform inside a ±11 % band. Both are
   assumptions about I8's authoring, and both are load-bearing: halving weights per tier up push
   `U(20,1)` to **41 %** and quartering push it to **47 %**, both outside the band. **I8 must confirm
   near-uniform tier weights and roughly ±10 % band widths, or tell this lane the invariant has to
   move.** This is the largest single dependency in the document.
2. **I8 — the pool must stay a superset across rungs.** Rarity may change count and window; it must
   never change which families are offered, or §8.6 comes back.
3. **I8 — at least six distinct drawable `group`s per role's pool at every ilvl band**, or rung 100's
   `5-6` count band cannot be satisfied and `PoolRollsExceedGroups` fires on a legal drop.
4. **I4 — socket counts as `socket_min` / `socket_max`, and whether the count is *rolled*.** A rolled
   socket count is a **fourth variance source**; it changes every number in §3.5 and §7, and this lane
   would have to re-measure the invariant with sockets included.
5. **I6 — the enhancement ceiling, with a bound.** Total enhancement gain at cap must be **≤ one rung
   step in expectation (~+70 %)**. If a fully enhanced Grafted matches a natural Fused, enhancement has
   replaced the ladder and rarity is cosmetic.
6. **I6 — adopt `RarityDemotion` and `RarityCeilingExceeded`, and put promotion in the recorded
   operation log**, so SC5's *origin seed + ordered ops* reproduction actually holds (§3.7).
7. **I7 — reroll cost must scale with affix count, not with rung alone.** §8.1's entire defence against
   dead low rungs depends on a low-rung item being cheap to craft *on*.
8. **I9 — a salvage material namespace that does not overload `shard.{DemonRarity}`.**
   `DemonMaterialCatalog.cs:18` generates `shard.common|rare|epic|legendary` from the *demon* ladder.
   Item salvage keyed on a ten-rung ladder needs its own ids — this lane suggests
   `dust.{item_rarity_id}` — and reusing `shard.*` would silently fuse two ladders that §4.3 keeps
   apart.
9. **I12 — four things:** the `tierCeiling(ilvl)` function (this lane assumes only that it exists and is
   monotone); drop weights per rung per source that keep low rungs in the stream (§8.1); pity counters
   and thresholds for rungs 70 and 90 (§3.8); and a **deterministic source for rung 100**.
10. **I3 — where the hand-authored / unique flag lives**, since §3.6 makes uniqueness orthogonal to
    rarity rather than a rung. Related: I3 and I13 need to settle who owns the item row above the
    instance, because §5.3 N6 puts four rarity columns on it.
11. **I13 — the comparison UI must show ordinal, pips and `Q`, and must not sort by rarity alone.** An
    inventory sorted by ordinal alone re-teaches the player that colour is the answer, which is exactly
    the habit §8.3 exists to break.
12. **I11 — confirm rarity is not an equip gate.** This lane registers the negative; I11 owns the gate
    and must not add a rung requirement to it.
13. **E9 (power), when it lands — the cross-family comparison.** `Q` is unit-safe but per-item; the `F`
    readout, cross-family item comparison, and per-rung authoring budgets all need the power vector.
    Per SC9 this is a **want, not a dependency**: everything here works without it, and §3.5's
    measurement is deliberately single-family so it does not pretend otherwise.
14. **I5 — the minimum ordinal for set eligibility**, and whether set pieces are rolled. If a set piece
    carries a *fixed* rung, this lane needs to know which, because a set rung that ignores the count
    band is a rung the ladder does not describe.

---

## 10. Open questions for the owner

1. **The schema asks** — `pool_rolls_max`, the three UI columns, `enabled`, `rarity_budget`, the
   `Instantiator` ceiling and count-band argument, and the `UnknownRarity` FK check. All additive and
   nullable-defaulted, all against a Checkpoint-B-published contract, so all ask-first under E5's
   boundaries. §5.3 carries a no-schema-change fallback if the answer is no.
2. **Three new reason codes** — `UnknownRarity`, `RarityBandViolated`, `RarityLadderMutated`.
   Definitions §10 calls its 33-code list closed and adding one a reviewed change.
3. **Who owns the item row above the instance.** item-ideal §6.3 left it open; the contract's cut #10
   gives storage to I13 and cut #4 gives base types to I3, and the row sits between them. This lane
   registers the four columns it needs (§5.3 N6) and cannot resolve the ownership.
4. **Promotion's ceiling at ordinal 80.** Making Sunwoven and Almanac drop-only keeps the top of the
   ladder a reason to play content. It also means a player who never gets lucky never gets there. A
   ceiling of 90, or no ceiling with a steep cost curve, are both defensible.
5. **Ladder length.** Ten is argued from the machinery (§3.4), not from taste. A six-rung ladder where
   every rung is a bigger event is a legitimate different game, and it is cheaper to author.
6. **`CurveInput.Rarity`.** Definitions §2 documents it as legal machinery reading the container's
   rarity ordinal; E5's *Boundaries* says *"never let rarity change an atom's magnitude"*. Definitions
   outranks the spec, so the two disagree, and this lane banned the input on items to keep the overlap
   alive. That ban wants confirming, and the contradiction wants fixing in one of the two documents.
7. **A light-theme palette.** §3.3's hexes are tuned for a dark surface. A light variant needs its own
   pass against the same monotone-`L*` rule.
8. **Rung names.** `almanac` for the top rung is PvZ-native and memorable, and it may also read as a UI
   screen rather than a quality. `firstseed` and `sunwoven` are inventions. All ten are taste, and taste
   is the owner's.
9. **Is item level visible to the player?** A hidden ilvl makes §7.3's "cannot drop" rule feel
   arbitrary; a visible one adds a second number to every tooltip. This lane needs the axis to exist and
   does not need it shown.
