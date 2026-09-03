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
`data/tuning/item-drop-volume.v1.json`. Starting slope is the owner's — it is the single number that
decides whether a veteran sees twice the drops or ten times.

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

**Verified as written** (`ssot-generation.md` §4.1):

```text
contentLevel:
  web battle       WaveDef.RecommendedLevel        (src/FusionRpg.Core/Battle/WaveCatalog.cs)
  expedition tick  the resolved wave's own RecommendedLevel — NOT a second formula
  expedition boss  same, at BossWaveId = "rift-tyrant"
  world sector     sectorLevel(danger_band)        — owed by the world program (X5)
  PvZ run          ⛔ UNDESIGNED

jitter j:  stream item.ilvl, NextPerMille() → [0,150) = −1 · [150,750) = 0 · [750,1000) = +1
itemLevel  = max(1, contentLevel + j)
level_req  = max(1, itemLevel − 2)
```

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
| `item_generation(instance_id PK, drop_log_id, base_type_id, rarity_ordinal, item_level, socket_count, frame, role)` | the per-instance stamp, written once, never updated | — |
| `item_loot_pity(player_id, items_since_r4, items_since_r6, updated_utc)` | mirrors `rpg_summon_pity` | — |
| `item_first_clear(player_id, source_kind, source_id, granted_utc)` | first-clear grants | — |

**Reused unchanged:** `effect_container` / `effect_container_pool`, `rarity`,
`effect_instance.roll_seed`/`.catalog_revision`/`.origin`, `Instantiator.TryInstantiate`
(`Effects/Atoms/Instantiator.cs:92`), `AwardSouls`, `SeededRng.DeriveStream`
(`Battle/SeededRng.cs:26`), `AtomRandom.NextInclusive`/`NextPerMille` (`AtomRandom.cs:54,65`).

**`item_generation` deliberately does not settle "what an item row is."** It carries only what the
*pipeline decided*. The durable owned row is module 1's `rpg_item`.

**Retention:** `item_drop_log` ships with a **watermarked tail-trim on day one**, not as a deferral —
the soul ledger already paid for that lesson. What trims is `context_json` / `result_json` beyond the
horizon; `item_generation` is the permanent record. **The horizon is the owner's** (I12 §11 Q6).

### Validation

| Bad input | Code | Phase |
|---|---|---|
| A `drop_table` whose `source_allow` omits `web` | standalone-first rejection (§4.6 rule 2) | import |
| A PvZ-only entry, or a PvZ source with an equipment-rate or rarity boost | standalone-first rejection | import |
| A `loot_source` of kind `pvz-run` (no `contentLevel` source exists) | **refused by name**, never defaulted | import |
| `entry_kind = equipment` with an unknown base-type-set `ref_id` | `UnknownBaseTypeSet` | import |
| An entry naming an unknown rarity ordinal in `rarity_weight_shift_json` | `UnknownRarity` | import |
| `affix_channel` not in `{drop, boss}` | `BadParamValue` | import |
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
data/tuning/item-drop-volume.v1.json                  new — ΘPin, base, slope, floor
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
| `pity_fires_where_the_drought_is_real` | R4 floor at 25 items, R6 ramp from 150 with a ceiling at 400 |
| `pity_cannot_be_banked_in_trivial_content` | item level comes from content, so a forced epic at ilvl 1 is an ilvl-1 epic |

## Boundaries

**Always:** read `Θ` through `IPowerIndexProvider`; keep volume linear and quality on `P(Θ)`; derive the
correlation id server-side; persist in one transaction; roll sockets last; narrow a tight envelope and
record it; keep every volume number in `data/tuning/item-drop-volume.v1.json`.

**Ask first:** the volume **slope** (the one number that decides whether a veteran sees 2× or 10× the
drops); the `item_drop_log` retention horizon (I12 §11 Q6); whether uniques get pity (§11 Q2); whether
smart loot is on by default (§11 Q3); adding a third `affix_channel` value.

**Never:** ⛔ add a drop cap, an inventory ceiling, or any cost curve that rises with player power —
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
