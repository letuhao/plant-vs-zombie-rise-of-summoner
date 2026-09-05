# Spec: `drop-volume`

**Module id:** `drop-volume` · **Program:** [item](../item-map.md) · **Build order:** 11 of 21
**Depends on:** `base-types` (6), `rarity-bands` (7), `affix-legality` (8), **X4 — L0 pool composition** (effect-pipeline)
**Supplies:** the `drop` and `boss` **channels** to [`affix-channel-weights`](../effect-pipeline/spec-affix-channel-weights.md) (X4)

## Objective

Build I12's pipeline — `loot_source`, the drop tables, the twelve ordered steps, the drop log — and
make **how many items drop** a function of the shipped power ladder rather than a private curve.

```text
D18:  how many drop  →  Θ, read LINEARLY
      how strong they are  →  P(Θ), through the existing rarity / tier path
```

**One arrow, not the loop.** D26 scopes the item system to *generate → drop → apply to actor*. Content
pacing, encounter volume and difficulty belong to the world map, battle engine and event generator
(**X5**).

## Design

### D18 — volume reads `Θ`, and this is not a new curve

> **Owner:** *"scale with player level, number of run in pvz and world stage … this is same formula as
> power scale function."*

Θ is already composed, in shipped code, from exactly the owner's three inputs:

```csharp
// src/FusionRpg.Core/Power/PowerIndexComposer.cs:53-61  (verified)
("dave", w.WdMilli, snapshot.DaveLevel),
("realmsAdvanced", w.WaMilli, snapshot.RealmsAdvanced),
("pvzRuns", w.WrMilli, snapshot.PvzRuns)
```

Read through `IPowerIndexProvider.ActorIndex(ctx)` (`Power/IPowerIndexProvider.cs:15`). The world-stage
term is content-side (`ContentContext.WorldTier`, `Power/ContentContext.cs:15`) and contributes nothing
until the world map ships — a weighted arithmetic sum degrades to its available terms with no
special-casing.

**Why `Θ` and not `P(Θ)`.** PS-3: contests read `Θ`, magnitudes read `P(Θ)`. A drop **count** is
neither — it is a rate. `P(Θ)` is quadratic (`Power/PowerLadder.cs:34-40`, the triangular term), and
quadratic growth in item *count* floods an armoury whose management minigame is deferred by D5.

**⭐ And this does not contradict "item level is content, never player".** They are different axes:

| | Reads | Source |
|---|---|---|
| **How many items** | `Θ_actor` (linear) | the player's own progression |
| **What level they are** | `contentLevel` | the content, `ssot-generation.md` §4.1 |
| **How strong the numbers are** | `P(Θ_content)` via `contentScale` | `InstanceProducer.cs:47` |

*The player moves how many; the content moves how strong.* Neither is a private `f(level)`.

### ⭐ Correction 1 — I12 calibrates in real-world DAYS, and the game has no day axis

I12 §8 states its target as *"**20–30 equipment items per day**"* and its calibration as
*"75 slots ≈ 10 days"* (`ssot-generation.md` §8), with a `40/day` tripwire.

**There is no day-based progression axis in this game.** Verified by search across Core: the only
per-day concepts are the demon-contract timers —
`Demons/Contracts/ContractPolicy.cs:88` (`DecayPerDay`), `:124` (`BaseUpkeepPerDay`), `:133`
(`UpkeepPerDay`), and `ContractTuning.cs:11` (`DailyGainCap`). The one other candidate is gone:
`RpgStore.Souls.cs:67` records *"T3.6 deleted `VictoryFullPerDay`, audit F11."* Θ itself has no time
term — it counts levels, realms and runs (`PowerIndexComposer.cs:58-60`).

A per-day target is therefore uncalibratable: it depends on how long the player plays, which the item
system cannot see and, per **D26**, must not try to meter.

**So the calibration is restated per content event, pinned at `Θ = 20`** — the item corpus's existing
calibration point, where `P(20) = 680` (`data/tuning/power-scale.v2.json`,
`"curve": { "cMilli": 80000, "bMilli": 400, "pinIndex": 20, "pinValue": 680 }`).

| Event | `E[equipment]` **at Θ = 20** | Authored as |
|---|---|---|
| Web battle, normal wave | 0.55 | one group: `nothing` w450 + `equipment` w550 |
| Web battle, boss wave | 1.40 | `gear-1` guaranteed (no `nothing`) + `gear-2` w400 |
| `scout-30m` (1 battle) | 0.70 | battle + small completion group |
| `forage-4h` (2 battles) | 1.60 | |
| `hunt-8h` (3 battles) | 2.60 | |
| `warpath-20h` (4 + boss) | 4.20 | 4 × 0.55 + 1.40 + 0.60 completion |
| World sector clear | 1.50 | scaled by `danger_band` |
| PvZ run | 0.50 | ⚠ see Correction 3 — the old `2/run, 12/day` cap is retired |

**Every number above is I12's own, re-labelled from "per day" to "per event at the pin".** The
behavioural target it was derived from is unchanged and is what a balance pass steers by: *the player
looks at 100% of equipment drops and keeps 20–35% of them.*

### The volume function

```text
volumeScaleMilli(Θ) = max(FloorMilli, VolumeBaseMilli + VolumeSlopeMilli × (Θ − ΘPin))
                                       // linear in Θ. ΘPin = 20 ⇒ scale = 1000‰ = ×1.0
rollsEffective(group, Θ) = (group.rolls × volumeScaleMilli) / 1000
                         + bernoulli(remainder‰)        [stream item.volume.{table}.{group}]
```

Four properties, each load-bearing:

1. **Θ scales the number of group draws, never the weights.** Scaling weights would change *composition*
   — which is L0's job (X4), not volume's. Keeping them separate is what lets a balance pass move one
   without moving the other.
2. **The fractional remainder is a Bernoulli on its own named stream.** Integer-only, unbiased
   (`Effects/Atoms/AtomRandom.cs:54,65`), and a new stream shifts nothing else — *"per-system streams
   derive from one run seed so an extra roll in one system never shifts another"*
   (`Battle/SeededRng.cs:7`).
3. **There is no upper bound.** Per AGENTS.md and **D26**, a ceiling on volume would be metering the
   player. `FloorMilli` is the one bound, and it is **structural and must say so in a comment**: a rate
   cannot be negative, and a Θ below the pin must not produce a negative draw count.
4. **`long` throughout, widen before multiplying, divide by 1000 last, exactly once.**

`VolumeBaseMilli`, `VolumeSlopeMilli`, `FloorMilli` and `ΘPin` live in
`data/tuning/item-drop-volume.v1.json`.

### ⭐ D38 — the kill path is a flat 5 %, and it is two rolls, not one

> **Owner, 2026-09-04:** *"make the game drop rare, 5% drop rate on kill for all rarity — this is just
> drop rate for any item, not 5% chance to drop rarity 10 item, it is different catalog. The number is
> tunable."*

⛔ **The disambiguation is the load-bearing half and it must survive into code.** Two independent rolls:

| Roll | Question | Source |
|---|---|---|
| **1** | **Does anything drop at all?** | `DropChanceOnKillMilli` = **50‰**, tunable |
| **2** | **Which rung?** | the rarity catalog's own weights — a **different table**, untouched by roll 1 |

**So a 5 % kill rate does not mean a 5 % chance at an `almanac`.** Conflating them would make the top
rung 20× more common than the rarity catalog says, and it is the exact misreading the owner pre-empted.
A test asserts the two roll independently.

✅ **This answers the "starting slope" question by removing the slope from the kill path.** The line
that stood here — *"the single number that decides whether a veteran sees twice the drops or ten
times"* — is retired: on kills, a veteran and a beginner see **the same rate**, and progression shows up
as *what* drops, not *how often*.

⚠ **D18 is not repealed.** Its `Θ`-linear volume term still governs **non-kill** sources — chest, boss,
completion. Recorded rather than silently resolved: if the kill path should also scale with `Θ`, that is
a one-line change to this tuning file, not a redesign.

### ⭐ Correction 2 — the `40/day` line asks for a loot filter, not a cap

I12 §8's tripwire reads: *"if measured steady-state inflow exceeds 40 equipment items per player per
day, a filter is required before the next content wave ships."*

**Read as written, that is an interface requirement, and it is already correct.** §9's own failure row
says the same thing from the other side: *"volume outran the interface until the interface moved into a
config file."* **The answer is a loot filter, and the loot filter belongs to module 20
`item-surfaces`** (`item-map.md:154`), which owns the armoury list and filter.

⛔ **It is not a volume cap, and this module must not implement one** — D26. What this module owes the
filter is the **measurement**: `item_drop_log` already carries every mint, so inflow is queryable
without a counter that could become a gate.

### ⛔ Correction 3 — D26: the item system balances items, not the game

> **Owner, 2026-09-03:** *"if user have stronger gear, so they can take advance to higher world realm
> with stronger enemy and can get stronger gear too — that is correct design and item system cannot
> handle it, that is world map need to handle, battle engine need to handle, event generator need to
> handle."*

**Withdrawn from this module's scope, permanently:**

| Withdrawn | Whose |
|---|---|
| Drop caps, per-run or per-period | nobody's — D26 refuses the mechanism |
| Inventory ceilings | D5 — unlimited capacity, module 2 `armoury` |
| A cost curve that rises with player power | D26; enhancement cost is tier-keyed and a **configurable soft cap** (module 15) |
| Faucet/sink balancing against player earning rate | withdrawn by D26 explicitly |
| How deep the content ladder goes | **X5** — world map · wave catalog · event generator |
| How often content is run | content pacing |

I12's §11 Q1 (*"how many actors get geared at once? if twenty, volume is 4× too low"*) and §11 Q5
(*"is 45% of normal battles dropping no gear too harsh?"*) are **dissolved rather than answered**: with
volume on `Θ` there is no fixed ownable figure — what a player can own is whatever their `Θ` has earned
them (D18's own closing paragraph). The old `2/run, 12/day` PvZ cap was already retired 2026-08-24
(`ssot-generation.md` §11 Q4) as *"a stamina gate `standalone-rpg-map.md` already ruled out."*

### Item level — content, never the player

⛔ **Correction 4 — `WaveDef.RecommendedLevel` does not exist, and this spec asserted it under a
heading reading "Verified as written."** The field is **`WaveDef.ContentIndex`**
(`src/FusionRpg.Core/Battle/WaveCatalog.cs:21`), and its own doc comment records the rename and its
completeness: *"`ContentIndex` is Θ_content — same values as the old `RecommendedLevel` name (1/3/6/10),
a vocabulary rename only. No external reader referenced the old name (verified: zero hits for
`RecommendedLevel` anywhere else in the repo), so this is a full rename, not an alias"*
(`WaveCatalog.cs:5-10`). Re-verified 2026-09-04: the only two hits for the old name in `src/` are those
two comment lines. **`ssot-generation.md` carries the stale name 7 times** (§4.1 and §11 Q8) and
`decision-d4-content-budget.md` once; all need the same correction, and they are the lane's, filed
from here.

⭐ **The rename is not cosmetic for this module.** `ContentIndex` **is `Θ_content`** — the same axis
`ContentScale.Milli(thetaContent, …)` reads (`Power/ContentScale.cs:17`). So §4.1's first row is not
"a level that happens to be a number"; it is the content half of the power ladder, and D18's split
(volume on `Θ_actor`, strength on `P(Θ_content)`) is expressed in one vocabulary rather than two.

The block below is `ssot-generation.md` §4.1 **with the rename applied**:

```text
contentLevel:
  web battle       WaveDef.ContentIndex            (src/FusionRpg.Core/Battle/WaveCatalog.cs:21)
  expedition tick  the resolved wave's own ContentIndex — NOT a second formula
  expedition boss  same, at BossWaveId = "rift-tyrant"
  world sector     sectorLevel(danger_band) = Wm · DangerBand(M), Wm = 5   ⭐ SHIPPED 2026-09-05
  PvZ run          ⛔ UNDESIGNED

jitter j:  stream item.ilvl, NextPerMille() → [0,150) = −1 · [150,750) = 0 · [750,1000) = +1
itemLevel  = max(1, contentLevel + j)
level_req  = max(1, itemLevel − 2)
```

⭐ **World-sector `contentLevel` is no longer owed — corrected 2026-09-05.** This row said *"owed by
the world program (X5)"*; it had already been closed by owner decision **2026-08-23**
(`ssot-power-scale.md` §5.3/§10.3: `mapLevel(M) = Wm · DangerBand(M)`, `Wm = 5` derived from the
shipped `SectorTypeCatalog`, *"it no longer owes an unknown"*) and restated for this exact row by
`spec-content-authoring.md` §2.1 (owner approved **2026-08-24**). What was missing was code, and
`PowerIndexComposer.MapLevel` is now it. The `loot_source` row is **resolved at runtime** by
`WorldSectorLootSource` rather than authored in `data/seed/loot/`, because the correlation id derives
from `source_id` — one static row per sector *type* would make two sectors one loot event. A band-0
sector (safe ground) is refused by name, `ContentRuleViolated{drop.sector-band-safe}`, never floored
to 1.

⛔ **PvZ-run `contentLevel` is explicitly undesigned, and this module does not invent one.**
`ssot-generation.md` §4.1's own correction says `mappedRunLevel` *"was never implemented anywhere — grep
finds it nowhere outside this one line."* §11 Q8 names the two candidates (the player's own level, or a
flat session level the PvZ side reports) and picks neither. **Whoever owns standalone-first PvZ drops
decides**; until then a `pvz-run` `loot_source` row cannot resolve an item level and is refused by name,
never defaulted to 1.

**Item level gates affix *strength*, never *variety*.** Every family is reachable at ilvl 1 at t1; only
the tier ceiling moves — t1/t2 @ 1, t3 @ 8, t4 @ 18, **t5 @ 32**. ⚠ **I12's table is the one that
ships** (D29 §2: I8's t5@60 *"is strictly worse — it delays the last band without adding growth"*), and
**tier saturating at t5 is correct**: growth past it is carried by `contentScale`, which is built —
`InstanceProducer.cs:47` computes `ContentScale.Milli(thetaContent, tuning)` and multiplies every rolled
magnitude by it (`Power/ContentScale.cs:15-19`).

### The pipeline — twelve ordered steps, plus one

Steps and their orderings are I12 §2, unchanged. This module adds **5a**.

```text
 0  LOOT EVENT           server-side fact; correlation id derived FROM the source record
 1  IDEMPOTENCY GATE     hit on (player_id, correlation_id) → return the manifest, mint nothing
 2  SEAL THE SEED        loot_seed = DeriveStream(sourceSeed, "loot:"+correlationId).NextULong()
 3  ITEM LEVEL           content level + jitter                      [item.ilvl]
 4  DROP TABLE           loot_source → table_id; reject if its ilvl band excludes it
 5a VOLUME SCALE     ⭐  rollsEffective per group from Θ_actor        [item.volume.{table}.{group}]
 5  GROUP DRAWS          each group draws rollsEffective times       [item.table.{t}.{g}]
 6  BASE TYPE            frame → role → base type                    [item.base.{i}]
 7  RARITY               weighted ladder draw, shifted, floored, pity-checked   [item.rarity.{i}]
 8  ENVELOPE             (rolls, min_tier, max_tier) = band ∩ ilvl cap          [item.rolls.{i}]
 9  AFFIX DRAW + FREEZE  Instantiator.TryInstantiate(...)  ⭐ now carrying `channel`
10  SOCKETS              I4's count rule, last so it can never shift an affix   [item.socket.{i}]
11  PERSIST              ONE transaction: instances + log + materials + souls + pity + first-clear
12  REVEAL               presentation only — the outcome was sealed at step 2
```

The three contested orderings and why they hold: **level before table** (the band is a filter, not a
post-hoc correction); **base before rarity** (uniques and set pieces are defined *on* a base type);
**sockets last** (a socket count consuming a stream earlier would move every affix roll at that band with
no content-hash change).

#### ⚠ Step 10 and two `entry_kind` values are downstream vocabulary — they ship as a documented no-op

This module is **11 of 21**. Step 10 implements module **16**'s socket rule and `entry_kind ∈
{insert, charm}` implement modules **16** and **13**'s payload kinds. Both are gated on **X7** — D27's
four `container_kind` values (`gem` · `set` · `charm` · `combo`) that `ContainerRow.cs:7-14` does not
ship (`item-map.md` §3, X7). **Implementing them here would author a second answer to a question two
later modules own.**

| Surface | Ships as | Flips when |
|---|---|---|
| **Step 10 SOCKETS** | the step **exists and consumes its stream** — `DeriveStream(roll_seed, "item.socket")` — and resolves to **0 sockets** while `rarity.socket_min`/`socket_max` are unseeded | module 7 seeds the two columns after I4, and module 16 lands the count rule |
| `entry_kind = insert` | **rejected at import** by name — `ContentRuleViolated{drop.entry-kind-unavailable}`, never silently dropped | X7 lands `gem` |
| `entry_kind = charm` | same | X7 lands `charm`, module 13 generates the corpus |

⭐ **The step consumes its stream even when it resolves to zero, and that is the whole point.** The
stream is derived and advanced now, so landing the real count later changes **no other draw** — which
is exactly the property `sockets_roll_last_and_shift_no_affix` asserts. A step added later would move
every affix roll at that band with no content-hash change, which is the defect the ordering exists to
prevent. **A no-op that reserves its stream is cheap; a step inserted after the corpus ships is a
migration.**

**Named tests, both written to flip:** `step_10_is_a_documented_no_op_and_reserves_its_stream` and
`an_insert_or_charm_entry_is_refused_by_name_until_x7_lands`.

Step 11 is one transaction because the summoning flow already paid for that lesson —
`spec-demon-summoning.md`'s two-transaction bug — *with one extra hazard: nothing is spent, so a partial
commit mints free items rather than losing paid ones.*

### X4 — this module supplies the `drop` and `boss` channels

`affix-channel-weights` (effect-pipeline module 12) names this module as the supplier of two of its six
channels (`spec-affix-channel-weights.md:68-69`), and states the rule that shapes the schema here:

> **The channel is a call-site fact, never stored on the affix.** Storing it would make an affix
> single-source and rebuild the problem one level down.

So the channel is declared **on the drop-table entry** and threaded through step 9:

```sql
drop_table_entry += affix_channel TEXT NOT NULL DEFAULT 'drop'   -- 'drop' | 'boss'
```

Two consequences worth stating:

- **A `boss` channel is a content-authoring fact, not a detected one.** An "elite" is whatever the
  author marks; there is no runtime heuristic to disagree with.
- ⛔ **Until X4 lands, `affix_channel` is authored and inert.** That is a **wiring gap**, not a wall —
  say so with that word. A trash drop and a boss drop roll the same affixes today (the exact defect
  `effect-pipeline-ideal.md` §5.6 names), and the column is what makes closing it a one-call change.

### ⛔ Correction 5 — pity keys on rung **ids**, and I12's `r4`/`r6` labels are a seven-rung vocabulary

`item_loot_pity(items_since_r4, items_since_r6)` and *"R4 floor at 25, R6 ramp from 150"* are I12's
own, and I12 was authored against **seven** rungs. Module 7 re-derived the ladder to **ten** and seeds
`pity_guarded = 1` at ordinals **70 (`heirloom`)** and **90 (`sunwoven`)** (`spec-rarity-bands.md`,
§3.8). Carrying `r4`/`r6` forward would leave two columns whose names name nothing.

| I12 (7 rungs) | Ten-rung equivalent | Column |
|---|---|---|
| `items_since_r4` — "epic+", natural 10.0% | **`heirloom`+**, ordinal **70** | `items_since_heirloom` |
| `items_since_r6` — "relic+", natural 1.0% | **`sunwoven`+**, ordinal **90** | `items_since_sunwoven` |

**Key on the rung id, never on an ordinal or a positional label.** This is the same discipline module 7
states as a rule for `rarity.ordinal` — the string id is the join, and a positional label is what
survives a ladder-length change with the wrong meaning. `SummonRoller` already made the opposite trade
deliberately (`PityState(PullsSinceHeirloom, PullsSinceSunwoven)` in code over SQL columns that *"kept
their old labels"*, `SummonRoller.cs:6-11`); a table with no rows yet has no reason to inherit that.

**⚠ The thresholds do not carry over unchanged, and this module owns re-deriving them.** I12 sized 25
and 150 against *its own* weights (R4+ at 10.0%, R6+ at 1.0%). Under module 7's re-derived table
`heirloom`-and-above is **5.9%** (2,500 + 1,600 + 1,100 + 700) and `sunwoven`-and-above is **1.8%**
(1,100 + 700) — so the droughts are differently shaped and the 7.2%-at-threshold property I12 tuned for
does not hold at 25. Re-solve against the seeded weights; **`pity_fires_where_the_drought_is_real` is
the test, and it asserts the drought probability, not the threshold**, so it survives a reweight.

**The top rung stays unguarded.** §3.8 puts no counter on ordinal 100 on purpose, and D7's lift of
rule 7 makes promotion a *deterministic* route there instead — see the next section.

### A deterministic source for `almanac` — registered by module 7, **answered here**

`ssot-rarity.md` §3.8: *"Rung 100 must have at least one deterministic source. An unreachable top rung
is a frustration, not a fantasy … **I12 owns which.**"* Module 7 registers the requirement and assigns
it to this module (`spec-rarity-bands.md`, D7 section); this spec never mentioned it. It does now.

| Source | Deterministic? | Owner |
|---|---|---|
| **Promotion to ordinal 100** | ✅ yes — D7 lifted rule 7, `promote_from = 1` on all ten rungs, and the price is a configurable soft cap | module 15 `enhance-reroll` |
| **First clear of a content id** | ✅ yes — `item_first_clear` fires once per `(player_id, source_kind, source_id)` and grants a **fixed, authored** container with no rolls (`ssot-generation.md` §3.5) | **this module** |
| A pity counter at 100 | ❌ — §3.8 leaves ordinal 100 unguarded on purpose | — |

**This module's answer is the first-clear grant, and it is the cheaper of the two.** The mechanism
already exists in §3.5 and needs no new machinery: **at least one authored first-clear item in the
shipped corpus carries `rarity = 'almanac'`.** A first clear is deterministic by construction (it is
recorded, it fires once, and it never rolls), so the requirement is met by content rather than by code.

⚠ **Which content id carries it is the owner's**, because it decides how deep the top rung sits. The
obligation this module accepts is that **the set is non-empty and CI says so** —
`at_least_one_first_clear_grant_is_rung_100` is a corpus test, not a runtime check.

⚠ **Promotion is the *other* source and it is real**, so if the owner would rather the top rung be
purely earned, the first-clear grant can be dropped without leaving §3.8 unsatisfied. Recorded so the
choice stays open rather than being made here by omission.

### Smart loot — ⛔ **deferred**, with the reason and the trigger

I12 §3.3 designs it in full and recommends it: **frame-weighted, role-flat, affix-blind, with a
250-weight floor**, a player-visible toggle recorded in `context_json` as a **replay input**. This spec
carried it as one line in *Ask first* — *"whether smart loot is on by default"* — with no design, no
schema and no test, which is the shape of an accidental omission rather than a decision.

**Made a decision: it does not ship in this module.** Two reasons, both structural rather than
budgetary:

| Reason | Detail |
|---|---|
| **Its input does not exist yet** | `frameWeight(f) = 250 + 750 × squadShareMilli(f) / 1000` reads the **deployed squad's frame mix**, and `frame` exists on no species type today — that is **X1**, resolved 2026-09-03 as a seedsmith `frame-classify` stage and **unbuilt** (`item-map.md` §3.1). A frame-weighted draw over an unclassified roster is a uniform draw with extra code |
| **It is the one bias that can break step 6, and step 6 is X4's supplier** | Smart loot biases the **base-type** draw at step 6. Step 6 feeds step 9's `affix_channel`, and X4 weights composition off that channel. Landing a bias into step 6 before X4's weights exist means the two get tuned against each other later, from opposite sides |

**What ships instead, so this is a deferral and not a hole:**

- Step 6 draws base types **uniform over the legal set**, and the code says *why* it is uniform with a
  pointer to this section — an unexplained uniform draw is how a deferral becomes a permanent default.
- `item_drop_log.context_json` reserves the two keys smart loot will write — `smartLoot` (bool) and
  `squadFrameMix` — and **writes them today** with `smartLoot: false` and the mix it can observe. That
  keeps §4.3's *"a settings change must not alter an already-sealed result"* rule true from the first
  drop, rather than retrofitting a replay input onto a log that never had one.
- `smart_loot_is_off_and_the_draw_is_uniform_over_legal_base_types` is the test, and it is written to
  **flip** when the bias lands.

**Trigger to revisit: X1 built and X4 landed** — whichever is later. **Owner:** this module, in a
follow-up; it is not reassigned elsewhere.

**Not deferred:** the 250-weight serendipity floor's *reason* is recorded now, because it is the part a
later session would drop. I12: *"one drop in six is for a body you may not own … the only reason to
keep hunting a frame you have not unlocked."* A frame-weighted draw with no floor is the D3-style
manufactured-loot failure §9 names.

### Two schema facts the lane has wrong

| Lane says | Shipped | Consequence |
|---|---|---|
| `band.PoolRolls` — one roll count per rung (`ssot-generation.md` §4.2) | `rarity(rarity_id, ordinal, **prefix_rolls**, **suffix_rolls**, min_tier, max_tier)` — `RpgStore.Containers.cs:54-61` | the envelope draws **two** counts, not one. **Module 7 owns the re-derivation**; this module consumes whatever it seeds |
| *"the `rarity` table, ten rungs, per-class bands"* (item-map §2) | **zero rows.** `data/seed/rarity/README.md`: *"Empty on purpose … no shipped container currently names a `rarity` value"* | this module is `rarity`'s **first production consumer**, and it cannot draw until module 7 seeds. Stated as a build-order fact, not a defect |

I12's per-rung drop weights were authored against **7 rungs** and I6's caps against **5**; the ladder is
**ten**. Re-deriving both is module 7's (`item-map.md:126`).

## Data shape

Tables are `ssot-generation.md` §5.1, carried unchanged except where noted.

| Table | Purpose | Change here |
|---|---|---|
| `loot_source(source_kind, source_id, table_id, content_level, first_clear_grant)` | who points at which table | — |
| `drop_table(table_id, source_allow, min_ilvl, max_ilvl, enabled, revision)` | `source_allow` MUST contain `web` — standalone-first, enforced at import | — |
| `drop_table_group(table_id, group_key, seq, rolls)` | an **independent** draw unit — the opposite of `effect_container_pool.group`, which is an *exclusion* unit | `rolls` is now the **pre-scale** count read by step 5a |
| `drop_table_entry(…, entry_kind, ref_id, weight, min/max_count, min/max_ilvl, rarity_floor, rarity_weight_shift_json, enabled)` | typed entries: `equipment\|material\|currency\|insert\|charm\|table\|nothing` | **+ `affix_channel`** (X4) |
| `item_drop_log(...)` | idempotency, replay, and **the inflow measurement the loot filter needs** | — |
| `item_generation(instance_id PK, drop_log_id, base_type_id, rarity_ordinal, item_level, frame, role)` | the per-instance stamp, written once, never updated | ⛔ **`socket_count` DROPPED** — see below |
| `item_loot_pity(player_id, items_since_heirloom, items_since_sunwoven, updated_utc)` | mirrors `rpg_summon_pity` | **renamed** — Correction 5; the `r4`/`r6` labels are I12's seven-rung vocabulary |
| `item_first_clear(player_id, source_kind, source_id, granted_utc)` | first-clear grants | — |

**Reused unchanged:** `effect_container` / `effect_container_pool`, `rarity`,
`effect_instance.roll_seed`/`.catalog_revision`/`.origin`, `Instantiator.TryInstantiate`
(`Effects/Atoms/Instantiator.cs:92`), `AwardSouls`, `SeededRng.DeriveStream`
(`Battle/SeededRng.cs:26`), `AtomRandom.NextInclusive`/`NextPerMille` (`AtomRandom.cs:54,65`).

**`item_generation` deliberately does not settle "what an item row is."** It carries only what the
*pipeline decided*. The durable owned row is module 1's `rpg_item`.

⛔ **`socket_count` is dropped from `item_generation` — it was a third copy of one fact.** Module 16
derives the count from a seeded stream and states that nothing is stored: *"`socketSeed =
SeededRng.DeriveStream(roll_seed, "item.socket")` … **Nothing is stored, so nothing can drift**"*
(`spec-sockets.md:143-145`), and D2 §6 makes `item_socket` **the SSOT** — *"it is not a materialized
view of anything"* (`spec-sockets.md:29`). A stamp here would be a third representation of the same
number, and the two that already exist are both authoritative in their own sense (one derivable, one
durable). **Three copies is how a socket count silently disagrees with the sockets an item has.**

The other `item_generation` columns stay, and the difference is the point: `base_type_id`,
`rarity_ordinal`, `item_level`, `frame` and `role` are **decisions the pipeline made that nothing else
records**. `socket_count` is not one of them.

**Retention:** `item_drop_log` ships with a **watermarked tail-trim on day one**, not as a deferral —
the soul ledger already paid for that lesson. What trims is `context_json` / `result_json` beyond the
horizon; `item_generation` is the permanent record. **The horizon is the owner's** (I12 §11 Q6).

### Validation

| Bad input | Code | Phase |
|---|---|---|
| A `drop_table` whose `source_allow` omits `web` | standalone-first rejection (§4.6 rule 2) | import |
| A PvZ-only entry, or a PvZ source with an equipment-rate or rarity boost | standalone-first rejection | import |
| A `loot_source` of kind `pvz-run` (no `contentLevel` source exists) | **refused by name**, never defaulted | import |
| `entry_kind = equipment` with an unknown base-type-set `ref_id` | `ContentRuleViolated{drop.unknown-base-type-set}` | import |
| An entry naming an unknown rarity ordinal in `rarity_weight_shift_json` | `ContentRuleViolated{rarity.unknown}` | import |
| `affix_channel` not in `{drop, boss}` | `BadParamValue` — a **shipped** member (`AtomRejection.cs:30`), reused as-is | import |
| `entry_kind` in `{insert, charm}` before X7 | `ContentRuleViolated{drop.entry-kind-unavailable}` | import |
| A `(base type × rarity × ilvl band)` combination whose envelope would narrow | **warning lint** — the author sees it before a player does | import |
| A narrowed envelope at drop time | `envelope_narrowed` in the log; **narrow `rolls`, never reject a legal drop** | runtime |
| A drop table referencing disabled content | `enabled = 0` keeps the row and never draws it (E5's rule) | runtime |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropVolume"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~DropTable"

# the replay job: same (loot_seed, catalog_revision, drop_table_revision) ⇒ same manifest
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~LootReplay"
```

## Project structure

```text
src/FusionRpg.Core/Items/Drops/LootPipeline.cs        new — the twelve steps + 5a
src/FusionRpg.Core/Items/Drops/DropVolume.cs          new — volumeScaleMilli(Θ); reads IPowerIndexProvider
src/FusionRpg.Core/Items/Drops/DropTableModel.cs      new — rows, typed entries, the draw
src/FusionRpg.Core/Items/Drops/DropEnvelope.cs        new — band ∩ ilvl cap; the collapse rule
src/FusionRpg.Core/Items/Drops/LootStreams.cs         new — the named stream ids, in one place
src/FusionRpg.Data/Sqlite/RpgStore.Loot.cs            new — the eight tables, one transaction at step 11
data/tuning/item-drop-volume.v1.json                  new — DropChanceOnKillMilli (D38),
                                                       ΘPin, base, slope, floor (non-kill sources)
data/seed/loot/                                       new — drop tables as content, hash-covered
tests/FusionRpg.Core.Tests/Items/DropVolumeTests.cs   new
```

## Code style

```csharp
// Volume is LINEAR in Θ (D18). P(Θ) is quadratic and would flood an armoury whose management
// minigame is deferred (D5) — quality already reads P(Θ) through the rarity/tier path, so the two
// halves stay on the axes PS-3 assigns them.
//
// FloorMilli is a STRUCTURAL bound, not a progression ceiling (AGENTS.md): a draw count cannot be
// negative, and Θ below the pin must not produce one. There is deliberately NO upper bound — a cap
// here would be metering the player, which D26 puts outside this program.
public static long VolumeScaleMilli(int thetaActor, DropVolumeTuning t)
{
    // Widen before multiplying; the product is per-mille and is divided by 1000 exactly once, in
    // RollsEffective, never here.
    long delta = checked((long)thetaActor - t.ThetaPin);
    long scale = checked(t.VolumeBaseMilli + t.VolumeSlopeMilli * delta);
    return Math.Max(t.FloorMilli, scale);
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `volume_is_linear_in_theta` | doubling `Θ − ΘPin` doubles the scale's excess over base — not the quadratic shape |
| `volume_uses_no_private_curve` | `DropVolume` references `IPowerIndexProvider` and nothing that computes a level→number of its own |
| `at_theta_pin_the_shipped_per_event_yields_hold` | every row of the §Correction-1 table, exactly, at Θ = 20 |
| `quality_still_reads_p_theta_through_content_scale` | the rarity/tier path is untouched by the volume change |
| `there_is_no_upper_bound_on_volume` | a very large `Θ` scales without clamping — **D26 as a test** |
| `the_floor_is_structural_and_documented` | `Θ` far below the pin yields a positive rate, and the constant carries its comment |
| `volume_scales_draw_counts_never_weights` | composition at Θ=20 and Θ=200 is statistically identical; only the count moves |
| `the_volume_stream_shifts_no_other_stream` | adding step 5a leaves every step-6…10 draw byte-identical for one `loot_seed` |
| `no_drop_cap_exists_anywhere_in_the_pipeline` | grep-shaped guard over `Items/Drops/` — no per-run, per-period or per-player ceiling |
| `item_level_never_reads_the_player` | `LootPipeline` step 3 takes no actor input — §4.1's rule, as a test |
| `a_pvz_run_loot_source_is_refused_by_name` | undesigned `contentLevel`, never defaulted to 1 |
| `tier_ceiling_is_i12s_table_not_i8s` | t5 at ilvl 32, and D29's reason recorded beside it |
| `content_scale_carries_growth_past_t5` | an ilvl-500 t5 affix is the same tier and a far bigger number |
| `a_retry_mints_nothing` | `UNIQUE(player_id, correlation_id)`; the recorded manifest returns |
| `the_correlation_id_is_server_derived` | no client-reachable knob |
| `persist_is_one_transaction` | a forced failure mid-persist leaves no instance, no log row, no material, no soul |
| `sockets_roll_last_and_shift_no_affix` | adding sockets to a band leaves affix rolls identical |
| `a_narrowed_envelope_narrows_rolls_and_is_recorded` | never a rejection of a legal drop; `envelope_narrowed` in the log |
| `an_envelope_that_would_narrow_lints_at_import` | the author sees it before a player does |
| `pvz_can_never_be_the_best_source` | set containment at import: no PvZ-only entry, no non-web table, no PvZ rate or rarity boost |
| `replay_within_one_revision_pair_is_identical` | `(loot_seed, catalog_revision, drop_table_revision)` ⇒ same manifest; **cross-revision difference is informational, never a failure** |
| `affix_channel_is_authored_and_threaded_to_step_9` | the X4 supply, provable before X4 lands |
| `pity_fires_where_the_drought_is_real` | asserts the **drought probability** at the threshold, not the threshold — so a reweight moves the number and not the test. Re-solved against module 7's seeded weights (`heirloom`+ = 5.9%, `sunwoven`+ = 1.8%), never against I12's 10.0% / 1.0% |
| `pity_counters_are_keyed_on_rung_ids` | Correction 5 — no `r4`/`r6`, no ordinal, no positional label anywhere in `Items/Drops/` |
| `step_10_is_a_documented_no_op_and_reserves_its_stream` | resolves to 0 sockets, still derives `item.socket`, and every other draw is byte-identical to a run with the step removed |
| `an_insert_or_charm_entry_is_refused_by_name_until_x7_lands` | `ContentRuleViolated{drop.entry-kind-unavailable}` — never a silent drop |
| `smart_loot_is_off_and_the_draw_is_uniform_over_legal_base_types` | the deferral, written to flip when X1 and X4 land |
| `context_json_carries_smartLoot_and_squadFrameMix_from_the_first_drop` | the replay input exists before the feature does, so §4.3 never has to be retrofitted |
| `at_least_one_first_clear_grant_is_rung_100` | §3.8's deterministic source for `almanac`, as a corpus test |
| `item_generation_has_no_socket_count_column` | the three-copies defect, closed by schema rather than by convention |
| `pity_cannot_be_banked_in_trivial_content` | item level comes from content, so a forced epic at ilvl 1 is an ilvl-1 epic |

## Boundaries

**Always:** read `Θ` through `IPowerIndexProvider`; keep volume linear and quality on `P(Θ)`; derive the
correlation id server-side; persist in one transaction; roll sockets last; narrow a tight envelope and
record it; keep every volume number in `data/tuning/item-drop-volume.v1.json`.

**Ask first:** ~~the volume **slope**~~ — **settled by D38**: flat 50‰ on kill, and `Θ`-scaling
survives only on non-kill sources. The `item_drop_log` retention horizon (I12 §11 Q6); whether uniques get pity (§11 Q2); which
content id carries the rung-100 first-clear grant; adding a third `affix_channel` value.
**No longer ask-first:** *"is smart loot on by default"* — it is deferred with a reason and a trigger,
and the answer while it is deferred is `false`.

**Never:** re-derive a socket count into `item_generation` — `item_socket` is the SSOT (D2 §6) and the
draw is reproducible from `roll_seed`. Never key a pity counter on a positional label or an ordinal
rather than a rung id. Never bias the step-6 base-type draw before X1 and X4 land. ⛔ Never add a drop
cap, an inventory ceiling, or any cost curve that rises with player power —
**D26**. Never write a private `f(level)` for loot — D18 and `ssot-power-scale.md` §10's closed
inventory. Never let item level read the player. Never default a `pvz-run` content level. Never let a
volume change touch affix **composition** — that is L0's (X4). Never reject a legal drop from legal
content because a pool narrowed.

## Success criteria

- [ ] Volume is a **linear** read of `Θ` through `IPowerIndexProvider`; no `f(level)` is declared in
      `Items/Drops/`.
- [ ] At `Θ = 20` every per-event yield in §Correction 1 reproduces exactly; nothing in the module is
      expressed per day.
- [ ] Quality still reaches the player through the rarity / tier / `contentScale` path, unmodified.
- [ ] **No cap of any kind exists** — proven by a guard, not by review.
- [ ] The `40/day` line is recorded as a **loot-filter requirement filed against module 20**, with the
      measurement query against `item_drop_log` named.
- [ ] Item level reads content only; a `pvz-run` source is refused by name with §11 Q8's two candidates
      cited and neither chosen.
- [ ] Step 5a's stream shifts no other stream, proven byte-identically for one `loot_seed`.
- [ ] `affix_channel` is authored on every equipment entry and threaded into step 9, so X4 lands as a
      one-call change.
- [ ] Replay inside one revision pair is identical; cross-revision difference is informational.
- [ ] Standalone-first is enforced by import-time set containment, in CI, not in prose.
- [ ] `RecommendedLevel` appears nowhere in `Items/Drops/`, and nowhere in this spec or
      `ssot-generation.md` **except** where it is explicitly named as the retired name — the field is
      `WaveDef.ContentIndex` (`Battle/WaveCatalog.cs:21`) and it **is** `Θ_content`. The lane's 7
      occurrences plus `decision-d4-content-budget.md`'s one are filed as a correction from here.
- [ ] Pity counters are keyed on rung ids (`heirloom`, `sunwoven`), and their thresholds are re-solved
      against module 7's seeded weights rather than inherited from I12's seven-rung table.
- [ ] `almanac` has a named deterministic source in the shipped corpus, proven by a CI corpus test.
- [ ] **Smart loot is deferred, not omitted**: the reason, the trigger (X1 and X4), the owner and the
      reserved `context_json` keys are all written down, and the uniform draw carries a comment saying
      it is a deferral.
- [ ] Step 10 and the `insert` / `charm` entry kinds ship as **documented no-ops** — step 10 reserves
      its stream, the two entry kinds are refused by name, and both tests are written to flip.
- [ ] `item_generation` has no `socket_count` column.
- [ ] **No new member of the closed 33-code list** (`AtomRejection.cs`, verified 2026-09-04: 33 codes
      plus `None`, and `ContentRuleViolated` is not yet among them). Shipped codes are reused where they
      fit (`BadParamValue`); everything else this module needs is a namespaced
      `ContentRuleViolated{drop.*}` / `{rarity.*}` per §2b.1.
