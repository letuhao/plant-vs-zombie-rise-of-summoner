# Lane I12 — item generation and drop tables

**Status:** Lane I12 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Read this session, in the order the contract names them: [item-ideal.md](../item-ideal.md) §8,
[enrichment-contract.md](enrichment-contract.md), [definitions.md](../effect-atom/definitions.md)
§2/§4/§5/§6/§10, [spec-container-schema.md](../effect-atom/spec-container-schema.md),
[spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md),
[spec-expeditions.md](../standalone/spec-expeditions.md),
[spec-demon-summoning.md](../demons/spec-demon-summoning.md),
[spec-standalone-charter.md](../standalone/spec-standalone-charter.md). Code claims below cite
`file:line` and were read, not recalled.

---

## 1. Scope

### This lane owns

- **The assembly pipeline** — the ordered algorithm that turns one loot event into zero or more
  persisted things. Contract cut #9.
- **Drop tables as data** — their schema, their draw semantics, their validation, their nesting.
- **Item level** — where it comes from and what it gates.
- **The drop-time envelope** — how a rarity band plus an item level become a concrete
  `(rolls, min_tier, max_tier)` handed to the instantiator.
- **Determinism and idempotency of a drop** — the named RNG streams, the sealed loot seed, the
  correlation record, the replay contract.
- **Smart loot, pity, guaranteed drops, first-clear grants, drop volume.**
- **Non-item drops that ride the same event** — materials, currency, inserts, charms.
- **The standalone-first enforcement rules on drop tables.**

### This lane does NOT own

| Thing | Lane |
|---|---|
| The rarity ladder, its rungs and ordinals | **I1** — I consume it and register what I read |
| Base types, implicits, base stats | **I3** — I draw from base-type *sets* it defines |
| The affix pool, tier bands, per-tier value ranges | **I8** — I select a window into it, never author it |
| Socket counts and insert typing | **I4** — I call its rule and roll the count it specifies |
| Post-drop mutation of a frozen instance | **I6** — my log is where its operation chain starts |
| Materials as a system | **I9** — but material *drops* come through my pipeline |
| Equip slots and roles | **I2** |
| Equip gating | **I11** — deliberately not consulted at drop time (§4.5) |
| Bags, stacking, salvage, comparison | **I13** — I hand it an inflow rate and a generation stamp |

---

## 2. The model

A **loot event** is a server-side fact — a web battle ended, an expedition was collected, a world
sector was cleared, a PvZ run posted a milestone. The pipeline reads that fact, seals one seed,
and walks a fixed ordered algorithm. Every step draws from a **named RNG stream** derived from the
sealed seed, so adding a step never shifts the draws of the steps beside it — the discipline
`src/FusionRpg.Core/Battle/SeededRng.cs:8` already states: *"Per-system streams derive from one run
seed so an extra roll in one system never shifts another."*

The pipeline is **twelve ordered steps**. The order is the design; a different order expresses
different things, and §2's three "why" notes plus §3.1–3.2 argue the orderings that were genuinely
contested.

```text
 0  LOOT EVENT           server-side fact; correlation id derived FROM the source record
 1  IDEMPOTENCY GATE     hit on (player_id, correlation_id) → return the recorded manifest, mint nothing
 2  SEAL THE SEED        loot_seed = DeriveStream(sourceSeed, "loot:"+correlationId).NextULong()
 3  ITEM LEVEL           computed from content level + jitter          [stream item.ilvl]
 4  DROP TABLE           resolve loot_source → table_id; reject if the table's ilvl band excludes it
 5  GROUP DRAWS          each group in the table draws `rolls` times   [stream item.table.{table}.{group}]
      ↳ typed entry: equipment | material | currency | insert | charm | table | nothing
 6  BASE TYPE            for each equipment entry i: frame → role → base type
                                                                      [stream item.base.{i}]
 7  RARITY               weighted ladder draw, shifted, floored, pity-checked
                                                                      [stream item.rarity.{i}]
 8  ENVELOPE             (rolls, min_tier, max_tier) = rarity band ∩ ilvl tier cap; rolls drawn
                                                                      [stream item.rolls.{i}]
 9  AFFIX DRAW + FREEZE  Instantiator.TryInstantiate(container, envelope, roll_seed)
                              roll_seed = DeriveStream(loot_seed, "item.rollseed."+i).NextULong()
                              internally uses the shipped atom.pool.* streams
10  SOCKETS              I4's count rule, rolled last so it can never shift an affix
                                                                      [stream item.socket.{i}]
11  PERSIST              ONE transaction: instances + drop log + material adds + soul ledger
                              + pity update + first-clear mark.  Atomic or nothing.
12  REVEAL               presentation only — the outcome was sealed at step 2
```

**Why item level before the drop table** (step 3 before 4): drop tables are ilvl-banded, so a
level-2 scout expedition and a level-14 warpath can share a table id and still offer different
entries. Compute the level first and the band is a filter; compute it after and it is a post-hoc
correction that cannot remove an entry already drawn.

**Why base type before rarity** (6 before 7): a unique, a set piece, and a boss-signature drop are
all defined *on a base type* — "Doomshroud Crown" is a specific plate-crown. Rolling rarity first
would force a parallel unique-only selection table that duplicates every frame and role gate I3
already declares. Rolling base first lets the drop-table entry carry `rarity_floor` and a per-ordinal
weight shift, which expresses "this boss only drops rare-and-better crowns" in two columns instead of
a second table. The cost, stated plainly: a base type cannot influence its own rarity odds *beyond*
what the entry pointing at it declares. That is acceptable — the entry is where the content author is
already standing.

**Why sockets last** (10 after 9): if socket count consumed a stream before the affix draw, adding
sockets to a rarity band later would move every affix roll on every item at that band, and every
recorded drop would replay differently with no content-hash change. Last, on its own stream, is the
only safe position.

**Why the whole thing is one transaction** (11): the summoning spec fixed exactly this bug — *"the old
two-transaction flow had a third post-crash state where Souls were spent but nothing was recorded, and
replay would re-roll a fresh seed"*
([spec-demon-summoning.md](../demons/spec-demon-summoning.md) §Pull flow). Loot has the same shape
with one extra hazard: nothing is *spent*, so a partial commit mints free items rather than losing
paid ones.

---

## 3. Options considered, and the recommendation

### 3.1 Can drop tables reuse `effect_container_pool`?

**No for the table, yes for the algorithm.** This was the question most worth checking against code.

`effect_container_pool` is `(container_id, atom_id, weight, group_key)`
(`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:42`). Three things break if a drop table is
squeezed into it:

1. **`atom_id` is not a free string.** The draw does `lookupAtom(p.AtomId)!`
   (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:160`) — a non-atom id null-derefs immediately.
   The grammar `^atom\.[a-z0-9-]+…$` (definitions §1) also rejects `essence.leaf` and
   `drop.boss-bonus`.
2. **`group` means the opposite thing.** In an affix pool `group` is **exclusion** — at most one atom
   per group, PoE's mod-family rule. In a drop table the useful grouping is **independent draws** — a
   guaranteed-material group *and* a chance-equipment group both resolve in one event. Overloading
   one column with exclusion and independence is precisely the "one word, four meanings" defect the
   enrichment contract §1 exists to cut.
3. **A drop entry needs columns an affix pool has no business carrying** — `entry_kind`, `min_count`,
   `max_count`, `min_ilvl`, `max_ilvl`, `rarity_floor`, a per-ordinal weight shift, and nesting.

What *is* reusable is the **draw itself**: weighted selection with rejection-sampled unbiased bounds
(`AtomRandom.NextBelow`, `src/FusionRpg.Core/Effects/Atoms/AtomRandom.cs:66`) and the running-total
scan in `Instantiator.Draw` (`Instantiator.cs:130-155`). Lift that into a shared `WeightedDraw` helper
and both callers use it. **Reuse the algorithm, not the schema.** That is cheaper than either forcing
the reuse or writing a second draw loop, and a second draw loop is how two subtly different weighted
selections end up in one codebase.

Rejected alternative: *a `drop_kind` discriminator column on `effect_container_pool`.* It buys one
fewer table and pays with a nullable FK, a broken grammar check, and a `group` column that means two
things. Not worth it.

### 3.2 How does a rarity band become a real affix window? (the one shipped-code obstacle)

This is the finding that shapes the whole lane, and it is not what the ideal assumes.

`effect_container.min_tier` / `max_tier` are **authoring assertions, not runtime filters.**
`ContainerValidator` rejects the *entire container* if any pool row's atom falls outside the window:

```text
src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs:88-92
    if (c.MinTier is { } min && atom.Tier < min)
        return Fail(TierOutOfWindow, $"{row.AtomId} is tier {atom.Tier}, below the window minimum {min}");
    if (c.MaxTier is { } max && atom.Tier > max)
        return Fail(TierOutOfWindow, …);
```

And `Instantiator.Draw` never consults the window at all — it filters only on `Weight > 0`
(`Instantiator.cs:135`) and reads the count from `container.PoolRolls` (`Instantiator.cs:131`).

A second finding in the same area: **the `rarity` table has no production consumer.**
`RarityRow(RarityId, Ordinal, PoolRolls, MinTier, MaxTier)`
(`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:93`) is written by `UpsertRarity` and read by
`ListRarities` (`RpgStore.Containers.cs:68,105`), and a grep across `src/` and `tests/` finds callers
only in `tests/FusionRpg.Data.Tests/ContainerStoreTests.cs`. Its `PoolRolls`/`MinTier`/`MaxTier` are
today exactly what SC7 warns about: *a row no code consumes is not content; it is a lie in a table.*
**I12 is the consumer that makes them true**, which is a point in favour of the option below.

Three ways to express (base type × rarity × item level):

| | Option | Verdict |
|---|---|---|
| **A** | **One container per (base type × rarity).** Works against shipped code with zero change. | **Reject.** 200 base types × 7 rungs = 1,400 containers, each duplicating its pool rows — and it still cannot express the *item level* axis, which would need a third dimension and ~20,000 containers. Content-hash churn on every affix edit would be enormous. |
| **B** | **One container per base type carrying its full t1–t5 pool; the pipeline hands the instantiator a drop-time envelope.** Needs one additive, optional parameter on `Instantiator.TryInstantiate`. | **Pick.** |
| **C** | **The drop-table entry carries the `(rolls, min_tier, max_tier)` triple directly.** | **Reject.** Same code change as B, and it relocates the rarity ladder into the drop table — breaking contract cut #2, which gives the ladder to I1. |

**The ask, stated as small as it truly is.** Add an optional parameter:

```csharp
public readonly record struct DrawEnvelope(int Rolls, int MinTier, int MaxTier);

AtomRejection TryInstantiate(
    ContainerRow container, Func<string, AtomRow?> lookupAtom, long rollSeed,
    out InstanceRow? instance, InstanceOrigin origin = InstanceOrigin.Drop,
    long catalogRevision = 0,
    DrawEnvelope? envelope = null);          // ← new, defaults to today's behaviour exactly
```

When `envelope` is null the code path is byte-identical to today, so **no existing caller changes**.
When present, `Draw` additionally filters candidates by `atom.Tier` and takes its count from
`envelope.Rolls`; `ContainerValidator` is untouched and keeps asserting the *authored* window against
the *authored* pool. Two distinct concepts, two distinct code paths — which is the whole reason not to
reuse `MinTier`/`MaxTier` for the runtime narrowing.

I have **not run the suite** to prove "no golden moves"; the claim is read from the code path, and per
[DESIGN-GATE.md](../DESIGN-GATE.md) §3.4 it must be executed before anyone leans on it. It is a
one-command check (`dotnet test tests\FusionRpg.Core.Tests`) and it belongs in the first task, not in
a review conversation.

### 3.3 Smart loot: how much?

| Option | What it costs |
|---|---|
| **None** — pure random | With 3 frames × ~15 roles (OD1/OD2), a random drop is useful to a given actor about 2% of the time. At §8's volume that is one usable item every two days. Unplayable. |
| **Full** (Diablo 3 style — frame, role, *and* affix bias toward the equipped build) | Every rare reads as a scripted reward. Worse here than in D3: it destroys OD4, because a build-aware affix draw stops producing the low rolls that make a high-roll low rarity beat a low-roll high rarity. |
| **Frame-weighted, role-flat, affix-blind, with a floor** | **Pick.** |

The recommendation, concretely:

- **Frame is biased** toward the *deployed squad's* frame mix, never the whole roster:
  `frameWeight(f) = 250 + 750 × squadShareMilli(f) / 1000`, integer weights.
  An all-plant squad gives plant 1000 / humanoid 250 / hybrid 250 → **66.7% / 16.7% / 16.7%**.
  A 3-plant / 2-humanoid squad gives 700 / 550 / 250 → **46.7% / 36.7% / 16.7%**.
- **The 250 floor is the fantasy, and it is not negotiable.** One drop in six is for a body you may
  not own. That is the "found something for a specimen I don't have yet" moment, and it is also the
  only reason to keep hunting a frame you have not unlocked.
- **Role is flat.** No bias whatsoever across the ~15 roles. Role bias is what makes loot feel
  manufactured — it is the difference between *the game gave me gear* and *the game gave me my gear*.
- **Rarity is never biased. Affixes are never biased.** Hard line.
- **A player-visible toggle**, default on, recorded in the drop log's `context_json` so it is a
  **replay input** and not a post-hoc filter. Filtering after the draw would let a settings change
  alter an already-sealed result, which breaks §4.3 outright.

Against the pick: a hybrid-heavy squad still under-serves hybrid base types, because hybrids draw
from both vocabularies (OD3). That is I3's weighting problem, not a smart-loot problem, and it is
named in §10.2.

### 3.4 Pity: reuse the summoning shape, with one deliberate divergence

**Reuse:** per-player counters, advanced inside the mint transaction, reset only on a hit of that
tier, and **visible in the UI**. Summoning's argument transfers verbatim — *"a no-money game has no
reason for opacity; visible counters turn dead pulls into progress"*
([spec-demon-summoning.md](../demons/spec-demon-summoning.md) §Banner catalog). Storage mirrors
`rpg_summon_pity` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:473-478`).

**Diverge:** summoning counts **pulls**, because a pull is a discrete, player-initiated, paid action.
A loot event is neither discrete nor paid — a 20-hour expedition is one event yielding four items, and
a battle is one event yielding zero. Counting events would make expedition players rich and battle
players poor for no design reason. **Loot pity counts equipment items minted, not loot events.**

Also heeded: summoning's own review found the original 10-pull rare floor *"fired only ~5% of the
time — cosmetic."* Same trap here. At §4.2's weights, rare-or-better is 28% of items, so a 30-item
rare drought has probability 0.72³⁰ ≈ 0.009% — a pity counter that fires once per eleven thousand
players is decoration. So the floors sit where they actually bite:

| Counter | Threshold | Natural drought probability | Displayed as |
|---|---|---|---|
| `items_since_r4` (epic+, natural rate 10.0%) | hard floor at **25 items** | 0.90²⁵ ≈ **7.2%** | `18/25 to guaranteed Epic` |
| `items_since_r6` (relic+, natural rate 1.0%) | soft ramp from item 150 (R6/R7 weights ×2 per 10 items), hard ceiling at **400** | the ramp fires for most players before the ceiling | `212/400 to guaranteed Relic` |

The 25 threshold is deliberately the same number summoning uses for its epic hard pity — one concept,
one number, two systems.

**No unique pity, deliberately.** Uniques are hand-authored identity items; a guaranteed unique means
every player converges on the same handful in the same week, and the item stops being a story. Said
out loud rather than left as an omission.

**Pity needs no content-band scoping**, and that falls out of §4.1 rather than being bolted on: item
level comes from the *content*, so a player who banks a pity counter farming level-1 waves cashes it
in on a level-1 epic worth nothing. The exploit does not exist because the level axis already closed
it.

### 3.5 Guaranteed drops and first-clear rewards

- **First clear of any content id** grants one **fixed, authored** item — a container with
  `pool_rolls = 0`, no rolls at all, so it never disappoints. Recorded per
  `(player_id, source_kind, source_id)` in `item_first_clear` so it fires once. This is the tutorial
  for the whole item system, and it is the one drop that should be hand-chosen.
- **Boss ticks and boss waves guarantee at least one equipment item.** Authored as a group with no
  `nothing` row (§5.3), so "guaranteed" needs no special-case code.
- Everything else is weight.

### 3.6 One drop table with typed entries, or one table per payload type?

**One table, typed entries.** Three reasons, in order of weight:

1. **A loot event is one budget.** Separate tables cannot express *"either a rare item or a big pile
   of essence"* — the tradeoff that makes a drop read as a result instead of a checklist. One table
   with an `entry_kind` column makes that tradeoff a weight, which is the only place a designer can
   actually tune it.
2. **Volume control is per-event.** §8's numbers are event-level. Two tables means two volume knobs
   that must be tuned in lockstep, and they will drift the first time someone edits one.
3. **The types differ in *resolution*, not in *selection*.** Selection is one weighted draw for all of
   them. Only the sink differs, and a sink switch is six arms. That is exactly the shape a typed entry
   serves.

Against, honestly: a typed entry is a discriminated union in a relational table, so `ref_id` means
different things per row and only a validator can prove it. That validator is §6, and it is the price.

Detailed per-kind resolution is §5.3, which is the substantive half.

### 3.7 Drop volume: rain, or a trickle?

The named failure is *a pipeline where 99% of drops are instantly salvaged*. That is Path of Exile and
Diablo 3 without a filter (recalled, unverified) — and it is why loot filters became mandatory in PoE
rather than optional: volume outran the interface, so the interface was moved into a config file.

**Commit: low volume, high inspect rate.** The target is stated as a behaviour, not a count — **a
player should look at 100% of equipment drops and keep 20–35% of them.** Numbers in §8 are derived
from that target, along with a falsifiable tripwire.

The counter-argument, which is real: low volume raises the cost of a bad drop, so the *excitement* has
to come from the roll rather than from the pile. That is what OD4's overlap is for, and it puts weight
on I8 authoring genuinely overlapping value bands (§10.3). If I8 cannot, volume has to rise and a
filter arrives with it.

### 3.8 Standalone-first: enforce it in the table, not in a promise

The charter's rule is *"the injector may **enrich** a feature … never **gate** one"*
([spec-standalone-charter.md](../standalone/spec-standalone-charter.md) §2), and the ideal sharpens it:
PvZ *"must never be the best source of anything web mode also provides"* (item-ideal §9). Both are
prose. Prose does not survive a content edit. The enforcement is §4.6 — two set-containment checks at
import that fail the build.

---

## 4. The decisions, one by one

### 4.1 Item level

**Item level is a property of the content, never of the player.** Player level enters the formula
nowhere.

```text
contentLevel:
  web battle        WaveDef.RecommendedLevel        — 1 / 3 / 6 / 10 today
                                                     (src/FusionRpg.Core/Battle/WaveCatalog.cs:32-35)
  expedition tick   the resolved wave's own WaveDef.RecommendedLevel — not a second formula.
                    ExpeditionResolver picks WHICH wave (its wave chain: scout → rift-skirmish;
                    forage → +rift-warband; hunt → +rift-onslaught; warpath → +rift-onslaught,
                    rift-tyrant), then that wave's RecommendedLevel is item level exactly like a
                    web battle (src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs, WaveChain)
  expedition boss   same as above, at the fixed boss wave (BossWaveId = "rift-tyrant") — still
                    WaveDef.RecommendedLevel, not "tier base level + 3"
  world sector      sectorLevel(danger_band) = Wm · DangerBand(M), Wm = 5 — CLOSED, and SHIPPED
                    2026-09-05 as PowerIndexComposer.MapLevel (ssot-power-scale.md §5.3/§10.3, owner
                    decision 2026-08-23; spec-content-authoring.md §2.1). §10.10 below is answered.
                    The loot_source row is resolved at runtime by WorldSectorLootSource, not authored:
                    the correlation id derives from source_id, so the SECTOR's own id has to be the
                    key. Band 0 (safe ground) is refused by name, never floored to 1
  PvZ run           not yet designed — no `mappedRunLevel` concept exists in shipped code. PvZ-
                    sourced drops need a real contentLevel source before this lane can resolve one;
                    tracked as an open question (§11), not a formula to treat as built

jitter j:  on stream item.ilvl, NextPerMille() → [0,150) = −1 · [150,750) = 0 · [750,1000) = +1
itemLevel = max(1, contentLevel + j)
level_req = max(1, itemLevel − 2)
```

**Corrected 2026-08-24.** The original text listed five independent `contentLevel` formulas; three
did not exist in shipped code. Expeditions were never a second formula — `ExpeditionResolver`
dispatches every battle (tick or boss) through the same `BattleSetup{WaveId, Wave}` a web battle
uses, so the wave it picks carries its own `RecommendedLevel` the same way §4.1's first row already
describes. Only the *wave selection* varies by expedition tier (the wave chain); the level mechanism
does not fork. `mappedRunLevel` (PvZ run) was never implemented anywhere — `grep` finds it nowhere
outside this one line — so it is recorded here as undesigned rather than restated as if it shipped.

Why content and not player: *"where do I farm this?"* must always have an answer. The moment item
level tracks player level, every piece of content yields the same gear and the map flattens.

Why `level_req = itemLevel − 2` rather than `= itemLevel`: you should always be able to wear what the
content you just beat dropped. The gate is the shipped one — `level_req` is enforced at bind with
`LevelTooLow` ([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)
§the bind-time gate).

**Item level gates affix *strength*, never affix *variety*.** This is the deliberate divergence from
Diablo 2 and PoE, both of which gate which mods can appear at all by area level, and both of which
have famously dull low-level itemisation as a result (recalled, unverified). Here **every affix family
is reachable at ilvl 1 at tier 1**; only the tier ceiling moves:

| Tier | Minimum ilvl |
|---|---|
| t1 | 1 |
| t2 | 1 |
| t3 | 8 |
| t4 | 18 |
| t5 | 32 |

t2 at ilvl 1 is on purpose. It is the single cheapest fix for *"low-level play never sees an
interesting affix"*: a level-1 rare and a level-1 uncommon differ in both count and tier from the very
first drop of the game.

### 4.2 The drop-time envelope, and OD4's overlap

```text
band            = rarity[ordinal]                          → (PoolRolls, MinTier, MaxTier)
env.maxTier     = min(band.MaxTier, maxTierAt(itemLevel))
env.minTier     = min(band.MinTier, env.maxTier)           ← collapses instead of emptying
env.rolls       = NextInclusive(max(1, band.PoolRolls − 1), band.PoolRolls)   [stream item.rolls.{i}]
                  (0 for the bottom rung, which has no pool)
```

`env.minTier = min(...)` rather than a clamp upward is the anti-double-gating rule. A relic band of
`[3,5]` at ilvl 4 (cap t2) becomes `[2,2]` — six affixes at t2 — rather than an empty window that
would have to reject or silently downgrade.

**If the narrowed envelope leaves fewer drawable groups than `env.rolls`, narrow `rolls` down and
record it.** Rejecting would fail a legal drop from legal content. This is not "ignore, don't reject"
(SC6) because the narrowing is written to the drop log as `envelope_narrowed`, and an **import-time
lint** warns the author whenever any `(base type × rarity × ilvl band)` combination would narrow — so
the author sees it before a player does.

**OD4's overlap is mechanical, from three compounding sources:**

1. **Tier windows overlap between adjacent bands** — R3 `[1,3]` and R4 `[2,4]` share t2 and t3.
2. **Affix count is a range, not a number** — R3 rolls 2–3, R4 rolls 3–4. A 3-affix rare and a
   3-affix epic differ only by window, and the windows overlap.
3. **Value ranges within a tier overlap** — the shipped `OnInstantiate` roll (`spec.Resolve(rng)`,
   `Instantiator.cs:196`) is already a range. If I8 authors t1 as 8–24 hp and t2 as 20–48 hp, a t1
   high-roll beats a t2 low-roll. **Source 3 does not exist unless I8 authors it** — sources 1 and 2
   alone give a weak overlap. Named as a dependency in §10.3 and worked with numbers in §7.2.

Illustrative rarity ladder — **I1 owns the real one; I address rungs by ordinal so it can rename
freely.** Weights are plain integers out of 100,000, not per-mille, because they are drop-table
weights and `weight ≥ 0` is the shipped rule (definitions §2).

| Ordinal | Illustrative | Draw weight | pool_rolls (band max) | min_tier | max_tier |
|---|---|---|---|---|---|
| R1 | common | 41,000 (41.0%) | 0 | – | – |
| R2 | uncommon | 31,000 (31.0%) | 2 | 1 | 2 |
| R3 | rare | 18,000 (18.0%) | 3 | 1 | 3 |
| R4 | epic | 6,800 (6.8%) | 4 | 2 | 4 |
| R5 | mythic | 2,200 (2.2%) | 5 | 2 | 5 |
| R6 | relic | 700 (0.7%) | 6 | 3 | 5 |
| R7 | unique | 300 (0.3%) | 1 | 3 | 5 |

Illustrative, not balanced. R7 at 0.3% is one unique per ~333 items ≈ 13 days at §8's rate.

### 4.3 Named RNG streams

Every stream derives from the sealed `loot_seed` through `SeededRng.DeriveStream`
(`SeededRng.cs:26`). The prefix `item.` is distinct from the shipped `atom.` prefix
(`AtomRandom.cs:30-34`, the three constants `atom.apply` / `atom.proc` / `atom.pool`) so an added item
roll can never shift an atom roll.

| Step | Stream name | Why the name has that shape |
|---|---|---|
| 3 item level | `item.ilvl` | one draw per event |
| 5 group draw | `item.table.{table_id}.{group_key}` | named for the group, so adding a group never shifts another |
| 5 nested table | `item.table.{table_id}.{group_key}.{depth}` | depth disambiguates a table nested twice |
| 5 quantity | `item.qty.{i}` | non-equipment stack sizes |
| 6 base type | `item.base.{i}` | **index, not the drawn id** — a name that depended on the draw would shift later draws when content is added |
| 7 rarity | `item.rarity.{i}` | |
| 8 affix count | `item.rolls.{i}` | separate from rarity so a count-range change never moves a rarity |
| 10 sockets | `item.socket.{i}` | last, per §2 |
| 9 affix draw | **unchanged** — `atom.pool.{container_id}` and `atom.pool.freeze.{atom_id}.{seq}` | shipped, `Instantiator.cs:127,182` |

The instance's `roll_seed` is itself derived —
`DeriveStream(loot_seed, "item.rollseed." + i).NextULong()` — so **two reproduction contracts hold at
once**:

- the atom layer's: same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical instance
  (definitions §5), untouched;
- the lane's: same `(loot_seed, catalog_revision, drop_table_revision, context_json)` ⇒ identical
  manifest.

This is the same relationship the expedition resolver already has with its battles: the expedition
seals one seed and derives `battle:{i}` from it
(`src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:89`), so *"lazy resolution at collect is
provably identical to eager resolution at dispatch"*
([spec-expeditions.md](../standalone/spec-expeditions.md)). A drop is the same construction one level
down.

### 4.4 Idempotency — a retry must never mint a second item

Four mechanisms, all with a shipped precedent:

1. **The correlation id is derived from the source record on the server, never supplied by the
   client.** `loot:{matchKey}` · `loot:exp:{expeditionId}` ·
   `loot:sector:{worldId}:{sectorId}:{turn}` · `loot:pvz:{runId}:{milestone}`. This is a **deliberate
   difference** from summon and expedition dispatch, which do take a client correlation id. Those are
   player-initiated commands; a loot event is a consequence of a recorded fact. A client that can pick
   its own loot correlation can mint on demand. Passing one is `BadParamValue`.
2. **`UNIQUE(player_id, correlation_id)`** on `item_drop_log`, the same key every command table in the
   tree already uses (`RpgStore.cs:405,474,496,510`).
3. **The source record is itself once-per-thing** — `rpg_web_match_log.match_key` is `UNIQUE`
   (`RpgStore.cs:481`), so a derived correlation cannot be re-created by re-posting.
4. **One transaction**, gate-serialized: instance rows, drop log, material adds, soul ledger, pity
   update, first-clear mark. The summoning flow is the model, and the ledger's own
   `UNIQUE(player_id, reason, dedupe_key)` (`RpgStore.cs:449`) is a second net under the currency arm.

A replay of the endpoint returns the stored `result_json` verbatim, advances no counter, writes no
ledger row. Two concurrent calls: the second blocks on the store gate and then reads the committed
row.

### 4.5 Drops are never gated by whether you can equip them

I11 owns the equip gate. The pipeline does **not** consult it. A drop may be unequippable — wrong
frame, level too high, requirement unmet — and that is correct: it is the aspiration that makes
progression legible, and it is the same thing the 250-weight smart-loot floor is buying. The gate
fires at bind, with `LevelTooLow` / `ScopeUnsupported` / whatever I11 adds. Confirmation requested in
§10.6.

### 4.6 Standalone-first, enforced by validation

| # | Rule | Enforcement |
|---|---|---|
| 1 | The injector never rolls a drop. PvZ posts a **loot event**; the server resolves it on the same pipeline. | There is no injector drop path, so there is nothing to be better. Matches charter §3: *"All web-mode outcomes resolve server-side with seeded, recorded RNG"* |
| 2 | Every drop table declares `source_allow`. A table not reachable from `web` is rejected at import. | `StandaloneRuleViolation` |
| 3 | Every `drop_table_entry` reachable from a PvZ source must also be reachable from a web source — a set-containment check over the resolved entry graph. | `StandaloneRuleViolation`. This is the strongest readable form of *"never the best source of anything web mode also provides"*, and it is cheap: two reachability sets and a subset test |
| 4 | The charter adopts *"boosted earn"* as a legal extension role. **It applies to currency and materials, never to equipment drop rate or rarity weights.** | Equipment weight shifts on a PvZ-reachable entry are rejected at import; currency and material shifts are allowed |

**Removed (pre-build reconciliation, 2026-08-24): a per-player rate-parity cap on injector-sourced
equipment (2/run, 12/day, `pvz_loot_budget`).** A daily/per-run cap is a stamina gate, and
[standalone-rpg-map.md](../standalone-rpg-map.md) already ruled *"no stamina system — with no
monetization a stamina gate has no honest job."* Rules 1/2/3 already do the real work rate parity
was standing in for: source `web` reachability, `StandaloneRuleViolation` containment, and rule 4
above (unweighted equipment) together mean PvZ can never be the *best* place to farm equipment —
matching drop tables, matching odds, no separate weight bonus. A count cap on top of identical odds
adds nothing rate parity doesn't already guarantee; it only throttles play. If a PvZ-specific
volume concern shows up in practice, it is a `data/tuning/<domain>.v{n}.json` soft cap (tunables-ssot.md
T1), never a hardcoded 2/12.

Rule 5 is the sharp one. A soul bonus is linear and spends down. An equipment rate or rarity bonus
compounds into permanent build power, which makes the game the best way to play the RPG — the exact
inversion the charter forbids. What PvZ legitimately gives: currency, materials, trophies, and
exclusive capture (the four adopted extension roles), with items riding the same tables as everyone
else.

---

## 5. Data shape

### 5.1 New tables

Consumers named per SC7. Nothing here is a row without a reader.

```sql
-- WHO points at WHICH table, and what level the content is.
-- Consumer: the pipeline (step 4), and the FE's "where does this drop" panel.
CREATE TABLE loot_source (
  source_kind        TEXT NOT NULL,   -- web-wave | expedition-tier | world-sector | pvz-run
  source_id          TEXT NOT NULL,   -- rift-warband | warpath-20h | forest-3 | milestone id
  table_id           TEXT NOT NULL,
  content_level      INT  NOT NULL,
  first_clear_grant  TEXT,            -- container id granted once per player; NULL = none
  PRIMARY KEY (source_kind, source_id));

-- Consumer: the pipeline (steps 4-5) and the import validator.
CREATE TABLE drop_table (
  table_id      TEXT PRIMARY KEY,     -- 'drop.<source>.<name>'
  source_allow  TEXT NOT NULL,        -- CSV of web|injector|sim; MUST contain 'web' (§4.6 rule 2)
  min_ilvl      INT, max_ilvl INT,    -- nullable band
  enabled       INT NOT NULL DEFAULT 1,
  revision      INT NOT NULL DEFAULT 0);

-- A group is an INDEPENDENT draw unit — the opposite of effect_container_pool's `group`,
-- which is an exclusion unit.  Consumer: the pipeline (step 5).
CREATE TABLE drop_table_group (
  table_id   TEXT NOT NULL,
  group_key  TEXT NOT NULL,
  seq        INT  NOT NULL,           -- draw order, stable
  rolls      INT  NOT NULL DEFAULT 1,
  PRIMARY KEY (table_id, group_key));

CREATE TABLE drop_table_entry (
  table_id                 TEXT NOT NULL,
  group_key                TEXT NOT NULL,
  seq                      INT  NOT NULL,
  entry_kind               TEXT NOT NULL,   -- equipment|material|currency|insert|charm|table|nothing
  ref_id                   TEXT NOT NULL DEFAULT '',
  weight                   INT  NOT NULL,   -- >= 0; 0 keeps the row and never draws it (E5's rule)
  min_count                INT  NOT NULL DEFAULT 1,
  max_count                INT  NOT NULL DEFAULT 1,
  min_ilvl                 INT, max_ilvl INT,
  rarity_floor             INT,             -- rarity ordinal floor for this entry
  rarity_weight_shift_json TEXT,            -- {"3": 4000, "4": 1500} integer weight deltas by ordinal
  enabled                  INT  NOT NULL DEFAULT 1,
  PRIMARY KEY (table_id, group_key, seq));

-- Idempotency + replay + support.  Consumers: the pipeline (step 1), the CI replay job, support.
CREATE TABLE item_drop_log (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  player_id           INTEGER NOT NULL,
  correlation_id      TEXT NOT NULL,        -- SERVER-DERIVED (§4.4)
  source_kind         TEXT NOT NULL,
  source_id           TEXT NOT NULL,
  loot_seed           TEXT NOT NULL,
  catalog_revision    INTEGER NOT NULL,
  drop_table_revision INTEGER NOT NULL,
  item_level          INTEGER NOT NULL,
  context_json        TEXT NOT NULL,        -- smart-loot mode, squad frame mix, pity in, caps
  result_json         TEXT NOT NULL,        -- full manifest incl. every minted instance_id
  notes               TEXT NOT NULL DEFAULT '',  -- envelope_narrowed | pity_forced (pvz_cap retired — §4.6, 2026-08-24)
  t                   TEXT NOT NULL,
  UNIQUE(player_id, correlation_id));

-- The per-instance generation stamp, 1:1 with effect_instance.  Consumers: I13's inventory list
-- and comparison UI, I6's mutation chain, I5's set-membership read.
CREATE TABLE item_generation (
  instance_id     TEXT PRIMARY KEY,
  drop_log_id     INTEGER NOT NULL,
  base_type_id    TEXT NOT NULL,
  rarity_ordinal  INTEGER NOT NULL,
  item_level      INTEGER NOT NULL,
  socket_count    INTEGER NOT NULL DEFAULT 0,
  frame           TEXT NOT NULL,
  role            TEXT NOT NULL);

-- Consumer: the pipeline (step 7) and the FE counter display.  Mirrors rpg_summon_pity.
CREATE TABLE item_loot_pity (
  player_id        INTEGER PRIMARY KEY,
  items_since_r4   INTEGER NOT NULL DEFAULT 0,
  items_since_r6   INTEGER NOT NULL DEFAULT 0,
  updated_utc      TEXT NOT NULL);

-- Consumer: the pipeline's first-clear branch (§3.5).
CREATE TABLE item_first_clear (
  player_id    INTEGER NOT NULL,
  source_kind  TEXT NOT NULL,
  source_id    TEXT NOT NULL,
  granted_utc  TEXT NOT NULL,
  PRIMARY KEY (player_id, source_kind, source_id));
```

**On `item_generation` vs. a full `item` row.** item-ideal §6.3 leaves open whether the item entity is
the `effect_instance` or a thin row above it. This lane does **not** settle that — `item_generation`
carries only what the *pipeline decided*, is written once and never updated, and joins 1:1 on
`instance_id`. If I13 needs a bag row with a display name, favourite flag, and bind-on-pickup, that is
a second table beside this one, and it is I13's. Deliberately not claimed.

### 5.2 What is reused, unchanged

| Reused | Where | How I use it |
|---|---|---|
| `effect_container` + `effect_container_pool` | `RpgStore.Containers.cs:19,42` | one container per base type, carrying its full tier range |
| `rarity(rarity_id, ordinal, pool_rolls, min_tier, max_tier)` | `RpgStore.Containers.cs:52` | **I become its first production consumer** (§3.2) |
| `effect_instance.roll_seed`, `.catalog_revision`, `.origin` | `RpgStore.AtomInstances.cs:56` | origin `Drop`; the two revisions make replay meaningful |
| `Instantiator.TryInstantiate` | `Instantiator.cs:68` | the one mint path for equipment, inserts, and charms alike |
| `rpg_demon_materials(player_id, material_id, qty)` | `RpgStore.cs:515` | material entries add atomically; ids validated by `DemonMaterialCatalog.IsKnown` (`RpgStore.Expeditions.cs:209`) |
| `AwardSouls(playerId, delta, reason, dedupeKey)` | `RpgStore.Souls.cs:160` | currency entries, `reason = 'loot'` |
| `SeededRng.DeriveStream` | `SeededRng.cs:26` | every stream in §4.3 |
| `AtomRandom.NextInclusive` / `NextPerMille` | `AtomRandom.cs:52,63` | every draw; integer-only, unbiased |

### 5.3 Non-item drops — one table, seven typed arms

The substantive half of §3.6. Selection is shared; only resolution differs.

| `entry_kind` | `ref_id` resolves to | Quantity | Sink | Stacks | Idempotency | Unknown ref |
|---|---|---|---|---|---|---|
| `equipment` | a **base-type set** id (I3) | always 1 | `effect_instance` + `item_generation` | never — rolled values make it unique by construction (item-ideal §7) | the instance is unique | `UnknownBaseTypeSet` |
| `material` | a material id (`essence.*`, `shard.*` today) | `min..max` on `item.qty.{i}` | `rpg_demon_materials`, atomic `qty +=` | yes | the drop-log row; the add is inside the same transaction | existing catalog refusal |
| `currency` | a currency id (`souls` is the only one today) | `min..max` | `rpg_soul_ledger` via `AwardSouls(..., 'loot', correlationId)` | ledger, not inventory | the ledger's own `UNIQUE(player_id, reason, dedupe_key)` | `UnknownCurrency` |
| `insert` | a `gem.*` container id (I4) | 1 | an `effect_instance` of `container_kind='gem'`, unsocketed | stacks until socketed — I4/I13's call | the instance | `UnknownContainer` (exists) |
| `charm` | a `charm.*` container id (I10) | 1 | an `effect_instance` of `container_kind='charm'` | never — charms roll | the instance | `UnknownContainer` (exists) |
| `table` | another `drop_table.table_id` | n/a | nested draw at depth+1 | n/a | n/a | `UnknownDropTable` |
| `nothing` | `''` | n/a | none | n/a | n/a | n/a |

Five properties worth stating explicitly, because they are what the single-table design buys:

1. **The `nothing` row is not decoration — it is the volume knob.** A group containing a `nothing` row
   is a chance drop; a group without one is a **guaranteed** drop. §8's *"45% of battles drop no
   gear"* is one row with `weight = 450`, and §3.5's boss guarantee is the *absence* of that row. No
   special-case code, no `chance` column, one mechanism.
2. **Inserts and charms mint through the same `Instantiator` call as equipment.** They are containers
   with pools (SC3 reserves `gem` and `charm`), so a dropped gem can carry a rolled magnitude. Only
   the `equipment` arm additionally runs base type, rarity, envelope, and sockets. That keeps one mint
   path and one freeze contract for everything the pipeline creates, which is what SC1 asks for.
3. **Kind is drawn; quantity is rolled; nothing is scaled.** `min_count..max_count`, inclusive
   integers, on `item.qty.{i}`. No float multipliers anywhere, per SC4.
4. **At most one `equipment` entry per group.** Equipment groups are what §8's volume budget counts.
   Materials and currency are uncapped because a stack of 40 essence is one inventory row and a soul
   award is a ledger line — neither costs the player attention. That asymmetry is the whole reason the
   volume commit is expressed in equipment items and not in "drops".
5. **Nesting is for reuse, not for depth.** `entry_kind = 'table'` exists so a "boss bonus" or
   "elemental theme" sub-table is authored once and referenced from many tables. Depth cap 3, cycle
   check, both with reason codes (§6). Anything deeper is a content-modelling mistake, not a feature.

### 5.4 Consumables are deliberately absent

item-ideal §7 makes a consumable *an item that carries an action*, and the action layer is not built.
`entry_kind` has room for one and I do not ship it. Adding `consumable` later is one enum arm and one
sink; adding it now would ship a degenerate action mechanism that the action program then has to
absorb. Confirmation requested in §10.11.

---

## 6. Validation and reason codes

Existing codes reused wherever the semantics match — the closed list is definitions §10, thirty-three
codes, and adding one is a reviewed change.

| Bad input | Phase | Reason code |
|---|---|---|
| `loot_source.table_id` names no table | import | `UnknownDropTable` **(new)** |
| `entry_kind='table'` and `ref_id` names no table | import | `UnknownDropTable` **(new)** |
| `entry_kind='equipment'` and `ref_id` names no base-type set | import | `UnknownBaseTypeSet` **(new)** |
| `entry_kind='material'` and the id fails `DemonMaterialCatalog.IsKnown` | import | existing catalog refusal (`RpgStore.Expeditions.cs:209`) |
| `entry_kind='currency'` and `ref_id` names no currency | import | `UnknownCurrency` **(new)** |
| `entry_kind='insert'` / `'charm'` and `ref_id` names no container | import | `UnknownContainer` (exists) |
| nested `table` chain deeper than 3 | import | `DropTableDepthExceeded` **(new)** |
| nested `table` chain reaches itself | import | `DropTableCycle` **(new)** |
| `weight < 0` | import | `BadParamValue` (exists) — rejected, never clamped, same as `effect_container_pool` |
| every entry in a group has `weight = 0` | import | `UnsatisfiablePool` (exists — identical semantics to the affix-pool case) |
| `min_count > max_count`, or `min_ilvl > max_ilvl` | import | `BadParamValue` (exists) |
| duplicate `seq` within a `(table_id, group_key)` | import | `DuplicateSeq` (exists) |
| `rarity_floor` names an ordinal not in the `rarity` table | import | `BadParamValue` (exists) |
| `drop_table.source_allow` does not contain `web` | import | `StandaloneRuleViolation` **(new)** |
| an entry reachable from a PvZ source and from no web source | import | `StandaloneRuleViolation` **(new)** |
| a PvZ-reachable entry carries an equipment `rarity_weight_shift_json` | import | `StandaloneRuleViolation` **(new)** |
| a client supplies a correlation id on a loot resolve | request | `BadParamValue` (exists) |
| no rarity ordinal survives the entry's floor plus the ilvl clamp | draw | `RarityUnsatisfiable` **(new)** |
| a guaranteed group (no `nothing` row) loses every entry to `enabled = 0` | load | `UnsatisfiablePool` (exists) |
| the narrowed envelope leaves fewer drawable groups than `rolls` | draw | **not a rejection** — `rolls` narrows and the log records `envelope_narrowed`; an import lint warns the author (§4.2) |
| a replay at the **same** `(catalog_revision, drop_table_revision)` differs from `result_json` | CI | `LootReplayMismatch` **(new)** |
| a replay at a **different** revision differs | CI | **not a failure** — informational, see below |
| binding a dropped item whose atom was later disabled | bind | `StaleInstance` (exists, definitions §6) |
| `level_req` above the wearer's level | bind | `LevelTooLow` (exists) |

**Eight new codes.** Two candidates were deliberately *not* minted: an empty drop table reuses
`UnsatisfiablePool` because the semantics are identical, and the three standalone violations share one
code with a detail string rather than three. `DropTableDepthExceeded` and `DropTableCycle` stay
separate for the same reason definitions §10 keeps `UnknownTrigger` and `TriggerNotAllowed` apart —
they are different author mistakes.

### What happens when a drop table references disabled content

Three cases, and the third is the one that gets designed wrong.

1. **Import time.** E14's policy is all-or-nothing: *"One bad row and nothing is imported"*
   (definitions §10). A drop table referencing missing or disabled content fails the whole import.
2. **Draw time**, when content was enabled at import and disabled by a later one. **A disabled entry
   is treated as `weight = 0` at load** — precisely the shipped `effect_container_pool` precedent
   (*"`weight = 0` — row kept, never drawn"*,
   [spec-container-schema.md](../effect-atom/spec-container-schema.md) §Testing strategy). A group
   that then loses every drawable entry falls through to `nothing` **only if it has a `nothing` row**.
   A group that was guaranteed and loses everything is `UnsatisfiablePool` at load, not a silent
   nothing — the reasoning is E5's own: *"silently under-filling is the failure this program exists to
   remove."*
3. **Replay across a content change.** An already-dropped instance is **never re-rolled**: it keeps
   its frozen values, and new binds reject with `StaleInstance` (definitions §6, Stale owners). A
   replay of the *drop log* against a changed catalog is **expected** to differ, which is why the log
   records both revisions. **Replay is asserted only within a revision pair.** Getting this wrong is
   how a CI replay job goes red on every content edit and then gets disabled — which is strictly worse
   than not having one.

---

## 7. Worked examples

Numbers are illustrative, not balanced. Units are stated on every value: game units for primary
channels, integer per-mille for chances, plain integers for drop weights.

### 7.1 A normal web battle, end to end

**Event.** `rift-warband` (`WaveDef.RecommendedLevel = 3`, `WaveCatalog.cs:33`), matchKey `web-4821`,
player 7, deployed squad 3 plant + 2 humanoid, smart loot **on**, pity in `items_since_r4 = 18`.

| Step | Stream | Draw | Result |
|---|---|---|---|
| 1 | – | – | `(7, 'loot:web-4821')` miss — proceed |
| 2 | – | – | `loot_seed = DeriveStream(matchSeed, "loot:web-4821")` → recorded before anything is drawn |
| 3 | `item.ilvl` | `NextPerMille()` = 812 | j = +1 → **ilvl 4**, `level_req 2` |
| 4 | – | – | `loot_source('web-wave','rift-warband')` → `drop.web-battle.normal`, band 1–8 ✓ |
| 5a | `item.table.drop.web-battle.normal.gear` | `NextInclusive(1,1000)` = 683 | `nothing` w450 · `equipment` w550 → **equipment** |
| 5b | `item.table.…​.mat` | 214 | `essence.*` w700 → `essence.leaf`; qty on `item.qty.1` → **2** |
| 5c | `item.table.…​.cur` | – | `souls` w1000; qty on `item.qty.2` → **14** |
| 6 | `item.base.0` | 431 of 1500 | frames plant 700 / humanoid 550 / hybrid 250 → **plant**; role flat over 15 → **girdle-resource** (`soil`); base-type set for (plant, soil, ilvl 4) → **`item.clay-pot`** |
| 7 | `item.rarity.0` | 79,412 of 100,000 | cum. 41,000 / 72,000 / **90,000** → **R3 rare**. R3 < R4 → `items_since_r4` → 19 |
| 8 | `item.rolls.0` | – | band R3 = (3, [1,3]); `maxTierAt(4) = 2` → env `[1,2]`; rolls ∈ [2,3] → **3**. Drawable groups at t1–t2 = 9 ≥ 3, no narrowing |
| 9 | `atom.pool.item.clay-pot` | – | `roll_seed = DeriveStream(loot_seed,"item.rollseed.0")`, `catalogRevision = 141` |
| 10 | `item.socket.0` | – | I4's rule for R3 at ilvl 4 → 0–1 → **1 empty socket** |
| 11 | – | – | one transaction: instance + `item_generation` + drop log + `essence.leaf += 2` + `AwardSouls(7, 14, 'loot', 'loot:web-4821')` + pity |

**Result.** *Clay Pot of the Tide* — R3 rare, `soil`, ilvl 4, req 2.

```text
implicit (fixed core, from the base type)  atom.sun-yield.t1        +1 sun per wave
drawn  atom.vitality.t2                    +34 hp     (range 20–48, rolled 34)   game units
drawn  atom.resource-pool.qi.t2            +12 qi     (range 8–20, rolled 12)    game units
drawn  atom.regeneration.t1                +3 hp / 5 s                            game units
sockets                                    1 empty
```

**Deliberately shippable affixes.** Every atom above is `stat.modify` / `resource.delta` on a primary
channel. A `+fire power` affix would be `stat.derived` on `combat.*`, which is quarantined
`None/None/None` (definitions §D6) and **binds nowhere** until E12 ships a consumer — so a first-wave
drop table that leans on elemental power would mint items that do literally nothing. Drop tables must
be authored against the shippable families until E12 lands. item-ideal §9 says the same thing; this is
the lane where it becomes an authoring constraint with a reviewer.

### 7.2 OD4 — a high-roll uncommon beating a low-roll rare

Both at ilvl 30, where `maxTierAt(30) = t4`. Illustrative `atom.vitality`-class bands from I8, authored
to **overlap** (t1 8–24 · t2 20–48 · t3 44–90 hp, game units):

| | Item X | Item Y |
|---|---|---|
| Rarity | **R2 uncommon** | **R3 rare** |
| Band | (2, [1,2]) | (3, [1,3]) |
| Envelope after ilvl clamp | `[1,2]`, rolls ∈ [1,2] → **2** | `[1,3]`, rolls ∈ [2,3] → **3** |
| Rolled | `vitality.t2` **+46 hp** (of 20–48) · `plating.t2` **+44** (high) | `vitality.t1` **+9 hp** (of 8–24) · `regeneration.t1` **+10** · `plating.t2` **+21** (low) |
| Effective total | **90** | **40** |

The uncommon wins. All three overlap sources contribute: the windows share t2, the counts are ranges,
and the value bands overlap across tiers. **Remove source 3 — non-overlapping value bands — and this
example collapses**, because a t2 would always beat a t1 and the uncommon's only edge would be luck on
count. That is the dependency in §10.3, and it is the difference between OD4 being a mechanism and OD4
being a slogan.

One consequence for I13: the inventory must **show the roll position within the tier** (a fill bar, a
percentile, something), or the player sees "Rare" beside "Uncommon" and never learns that the uncommon
is better. An overlap the player cannot read is an overlap that does not exist.

### 7.3 A retried request, and a `warpath-20h` manifest

**Retry.** `POST /api/loot/resolve { playerId: 7, sourceKind: "web-match", sourceId: "web-4821" }`. No
correlation id is accepted; the server derives `loot:web-4821` from the `rpg_web_match_log` row.

| Call | What happens | Items minted | Souls written | Pity moved |
|---|---|---|---|---|
| 1st | miss → full pipeline → commit | 1 | 14 | +1 |
| 2nd (retry) | `SELECT … WHERE player_id=7 AND correlation_id='loot:web-4821'` hits inside the gate → returns `result_json` verbatim | **0** | 0 | none |
| concurrent 3rd | blocks on the store gate, then reads the committed row | **0** | 0 | none |
| crash between mint and log | **impossible** — one transaction (summoning precedent) | – | – | – |
| client invents a `correlation_id` | `BadParamValue`, request refused | 0 | 0 | none |

**A full `warpath-20h` collect** (4 battles + boss, ilvl 14–17):

```text
battle 1  normal table   gear miss · essence.ash ×2 · 11 souls
battle 2  normal table   gear HIT  → R2 uncommon  muzzle,  ilvl 14, 2 affixes
battle 3  normal table   gear miss · shard.common ×1 · 9 souls
battle 4  normal table   gear HIT  → R1 common    leaves,  ilvl 15, 0 affixes
boss tick boss table     gear-1 guaranteed → R4 epic  crown,  ilvl 17, 4 affixes   [pity 25 not reached]
                         gear-2 w400 HIT   → R3 rare  graft-2, ilvl 17, 3 affixes
                         insert group w150 miss
completion group         w600 HIT → R2 uncommon stem, ilvl 16, 2 affixes
                         materials ×3 stacks · 47 souls
─────────────────────────────────────────────────────────────────────────────
TOTAL   5 equipment · 6 material stacks (~14 units) · ~90 souls · 0 inserts
```

Five equipment items for twenty hours of one expedition slot, against §8's target of **E ≈ 4.2** —
this roll came in slightly hot, which is what a variance-carrying pipeline should look like.

---

## 8. Drop volume, in numbers

The target restated as behaviour: **the player looks at 100% of equipment drops and keeps 20–35%.**
Everything below is derived from that.

| Event | Equipment E[items] | Non-equipment | How it is authored |
|---|---|---|---|
| Web battle, normal wave | **0.55** | 1 material stack, 1 currency | one group: `nothing` w450 + `equipment` w550 |
| Web battle, boss wave | **1.40** | 2–3 materials, insert @ 15% | `gear-1` guaranteed (no `nothing`) + `gear-2` w400 |
| `scout-30m` (1 battle) | **0.70** | 2 materials | battle + a small completion group |
| `forage-4h` (2 battles) | **1.60** | 3–4 materials | |
| `hunt-8h` (3 battles) | **2.60** | 5 materials | |
| `warpath-20h` (4 + boss) | **4.20** | 7 materials, 1 insert guaranteed | 4 × 0.55 + 1.40 boss + 0.60 completion |
| World sector clear | **1.50** | scaled by `danger_band` | |
| PvZ run | **0.50**, hard cap 2/run and 12/day | currency + materials at parity | §4.6 rules 4–5 |

**Session and day math.** A 30-minute active web session runs roughly 15 battles — say 13 normal and 2
boss — giving `13 × 0.55 + 2 × 1.40 = 9.95` ≈ **10 equipment items per half-hour session**. A moderate
day (two such sessions plus expedition collects) lands at **20–30 equipment items per day**. At a 30%
keep rate that is **6–9 keepers per day**.

**Therefore: no loot filter on day one.** What ships instead is a *salvage everything below rarity X*
button, which I13 owns. **The tripwire, stated so it is falsifiable:** if measured steady-state inflow
exceeds **40 equipment items per player per day**, a filter is required before the next content wave
ships. That is a number to instrument, not a hope — and `item_drop_log` is where it is measured.

**The roster problem, honestly.** OD2 puts ~15 slots on a frame. Twenty demons × 15 slots is 300
equipped items before anything sits in a bag, and at 6–9 keepers per day that is a month and a half to
gear one roster — which is item-ideal §8's open economic question arriving with a bill attached.
**These numbers are calibrated for a deployable squad of five gearing at a time (75 slots ≈ 10 days).**
If the answer turns out to be "gear all twenty", my volume is **4× too low** and every number in this
section moves. Named as the first open question in §11, because it is the one that must be answered
before anything here is built.

---

## 9. Failure modes

| Failure, as it shipped elsewhere | What here prevents it |
|---|---|
| **99% of drops instantly salvaged** (PoE, D3 without a filter — recalled, unverified). Volume outran the interface until the interface moved into a config file. | §8's volume commit, the `nothing` row as the single volume knob, and the 40/day tripwire. The honest cost: with fewer drops, the *excitement* must come from the roll, which puts load on I8's overlapping bands (§10.3). |
| **Item level and rarity double-gating so low-level play never sees an interesting affix** (D2 and PoE both gate mod *variety* by area level). | Item level gates **strength only, never variety** — every family is reachable at ilvl 1 at t1, and t2 is reachable at ilvl 1 too (§4.1). The envelope's `min_tier = min(band.min, env.max)` collapses rather than emptying, so a relic at ilvl 4 is six t2 affixes, not a rejection. |
| **Smart loot so aggressive every drop feels manufactured** (D3 post-RoS). | Frame-weighted only, with a hard 250-weight serendipity floor; role flat; rarity and affixes never biased; a player-visible toggle recorded as a replay input (§3.3). |
| **A retry mints duplicate loot.** | Server-*derived* correlation id (never client-supplied), `UNIQUE(player_id, correlation_id)`, a `UNIQUE` source record upstream, and one transaction — the same four nets the summoning flow uses after its own two-transaction bug (§4.4). |
| **Pity that never fires.** Summoning shipped this exact defect: *"the old 10-pull guarantees rare+ fired only ~5% of the time — cosmetic."* | Thresholds sit where the drought probability is real: R4 floor at 25 items ≈ 7.2%, R6 ramp from 150 with a ceiling at 400 (§3.4). |
| **Pity banked in trivial content and cashed in a hard zone.** | Closed by construction, not by a rule: item level comes from the content, so a pity-forced epic at ilvl 1 is an ilvl-1 epic (§3.4). |
| **The loot ledger becomes the largest table in the database.** 25 items/day/player, forever. The soul ledger already hit this — *"Soul-ledger tail-trim (the P4 deferral) lands in this wave — expedition volume makes it real"* ([spec-expeditions.md](../standalone/spec-expeditions.md)). | `item_drop_log` ships with a **watermarked tail-trim on day one**, not as a deferral. The permanent record is `item_generation` (one narrow row per instance); what trims is `context_json` / `result_json` beyond the retention horizon, which costs only the ability to replay old drops. |
| **A content edit silently re-prices items players already own.** | `effect_instance.catalog_revision` (shipped) plus `drop_table_revision` in the log; replay is asserted only within a revision pair (§6). |
| **A CI replay job goes red on every content edit and gets disabled.** | Same rule, stated as policy: a cross-revision replay difference is **informational**, never a failure. |
| **The player farms the highest-value table by re-triggering the source.** | The correlation is derived from a `UNIQUE` source record (`rpg_web_match_log.match_key`, `RpgStore.cs:481`); there is no client-reachable knob. |
| **PvZ becomes the best place to farm gear.** | Import-time set containment: no PvZ-only entry, no non-web table, no equipment rate or rarity boost from a PvZ source (§4.6). Enforced in CI, not in prose. |
| **An affix pool that cannot satisfy its narrowed envelope kills a legal drop.** | Narrow the roll count and record `envelope_narrowed`; an import lint surfaces the combination to the author first (§4.2). |
| **First-wave items that do nothing.** `stat.derived` is quarantined (definitions §D6), so a `+fire power` item binds nowhere. | Drop tables are authored against the shippable families until E12; the constraint is stated in §7.1 and belongs in I8's review, not discovered in play. |

---

## 10. What this lane needs from other lanes

1. **I1 — the rarity ladder.** The rung list with ordinals, and per-rung
   `(pool_rolls, min_tier, max_tier)` written into the shipped `rarity` table. Plus the **base drop
   weight per rung** at a reference item level, and whether those weights shift with item level.
   §4.2's ladder is a placeholder addressed by ordinal so I1 can rename freely. I1 must also register
   the socket counts I4 proposes (contract cut #3), because I roll them at step 10.
2. **I3 — base types, and one thing that does not exist yet: a base-type *set*.** A drop-table entry
   cannot name 200 individual base types. I need a set id (`set.plant.girdle`, or a tag query) with a
   stable weight per member, plus each base type's `frame`, `role`, ilvl band, and **the container id
   its affix pool lives on**. My design assumes **one `effect_container` per base type carrying its
   full t1–t5 pool** (§3.2 option B). If I3 authors one container per (base × rarity) instead, option
   B collapses and we are back to option A's 1,400 containers. Also: hybrid base-type weighting, since
   hybrids draw from both vocabularies (OD3) and my frame bias does not fix that.
3. **I8 — overlapping value bands across adjacent tiers.** §7.2 shows the OD4 overlap failing without
   them. I also need confirmation that **every affix family is authorable at t1**, so §4.1's "gate
   strength, never variety" rule has content behind it. And a list of which families are
   `stat.derived` — those bind nowhere until E12 (definitions §D6) and must be excluded from
   first-wave drop tables.
4. **I4 — the socket-count rule** as a function of `(rarity ordinal, base type, item level)`, whether
   sockets roll at drop or are added later, and whether an unsocketed insert drop is a `gem` container
   instance or a material stack. My step 10 calls the rule and stores the result in
   `item_generation.socket_count`.
5. **I13 — inventory absorption.** The day-one inflow is **20–30 equipment items per player per day**
   (§8). Confirm the bag absorbs it and that a *salvage-all-below-rarity-X* control ships, so no loot
   filter is needed on day one. If I13 would rather have a filter, my volume numbers can rise. I13
   also owns showing **roll position within a tier** — without it OD4's overlap is invisible (§7.2).
6. **I11 — confirmation that a droppable item may be unequippable.** The pipeline deliberately does
   not consult the equip gate (§4.5). If I11 believes drops must always be equippable, that is a real
   disagreement and it changes smart loot.
7. **I9 — the generalised material id space.** `DemonMaterialCatalog` is demon-scoped
   (`essence.{element}`, `shard.{rarity}` — `src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:15-20`)
   and `rpg_demon_materials` is named for demons. My `material` entries point at ids and reject unknown
   ones through that catalog today; tell me the new id space and whether the table is renamed.
8. **I6 — where the mutation chain attaches.** SC5 requires an item's state to be derivable from its
   origin seed plus an ordered list of recorded operations. My `item_drop_log` + `item_generation` are
   the origin. Confirm I6 keys its operation log on `instance_id` (not on a new item id), so the chain
   is `item_generation → operations → current state`.
9. **I5 — how a set piece is drawn.** I assume set membership is a **tag on the base type**, drawn from
   the general table at its natural rarity, with no special group. If sets need
   guaranteed-progress-toward-completion, that is a pity variant: mine to build, I5's to specify.
10. ~~**The world map program — `danger_band → content level`.**~~ ✅ **Answered, and now built.**
    The mapping was published as an owner decision on **2026-08-23** — `ssot-power-scale.md` §5.3/§10.3,
    `mapLevel(M) = Wm · DangerBand(M)` with `Wm = 5` derived from the shipped `SectorTypeCatalog`
    bands — and restated for this exact row by `spec-content-authoring.md` §2.1 on **2026-08-24**. It
    stayed prose until **2026-09-05**, when it shipped as `PowerIndexComposer.MapLevel` with
    `WorldSectorLootSource` as its first caller. §4.1's hole is closed.
11. **The action program — confirm consumables stay out.** `entry_kind` has room; I do not ship one
    (§5.4). If consumables are wanted before the action layer, say so and I will add a degenerate arm.
12. **E5/E6 (effect-atom) — the optional `DrawEnvelope` parameter** on `Instantiator.TryInstantiate`
    (§3.2). Additive, defaults to today's exact behaviour, no existing caller changes. The "no golden
    moves" claim is read from the code path and **has not been executed** — running
    `dotnet test tests\FusionRpg.Core.Tests` is the first task, not a review conversation.

---

## 11. Open questions for the owner

1. **How many actors get geared at once?** §8's volume is calibrated for a deployable squad of five
   (75 slots). If the answer is "the whole roster of twenty", drop volume is 4× too low and every
   number in §8 moves. This is item-ideal §8's open economic question, and it is the one decision that
   must land before this lane is built.
2. **Do uniques get pity?** I said no (§3.4) — a guaranteed unique makes every player's first week
   identical. Reversible in one column of `item_loot_pity`.
3. **Is smart loot on by default?** I said yes, with a visible toggle. Off-by-default is a defensible
   different game.
4. ~~**The PvZ equipment cap — 2 per run, 12 per day.**~~ Retired 2026-08-24 (§4.6) — a daily/per-run
   cap is a stamina gate `standalone-rpg-map.md` already ruled out; rate parity (§4.6 rules 1–4) does
   the real work without it. No longer an open question.
5. **Is 45% of normal battles dropping no gear too harsh?** It is the single number §8 is most
   sensitive to. Raising equipment weight from 550 to 700 takes the session yield from ~10 to ~12.
6. **`item_drop_log` retention horizon.** I proposed a day-one tail-trim of the replay payload. How
   long — 30 days, 90, forever?
7. **Trading.** I found none in the tree and designed as if none exists, which is what makes smart loot
   cheap here (§3.3). If trading is ever intended, smart loot's cost rises sharply and this decision
   should be revisited before it ships, not after.
8. **PvZ run `contentLevel` (§4.1)** — added 2026-08-24, found while correcting §4.1's three
   never-built formulas. A PvZ-sourced drop needs *some* item-level source once standalone-first
   ships item generation; nothing in the tree computes one today. Candidates: the player's own level
   (mirrors "web battle" reading `WaveDef.RecommendedLevel`, i.e. the *content* the player is
   currently running sets it — but PvZ has no wave-def-equivalent to read from), or a flat
   session/run level the PvZ side reports. Neither is designed; this lane cannot resolve PvZ-sourced
   generation until one is.

---

## 12. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — effect-atom container/instance/binding, the
    standalone charter, expeditions, summoning, the soul ledger, materials, the world map.
[x] I read every doc the enrichment contract §5 names, this session, in the order it names them.
[x] I checked the locks that bind this lane via the charter and the contract's §6 owner
    decisions; OD4 and SC5/SC7/SC8 are the binding ones, and none forbid this design.
[x] Every factual claim about the repo cites file:line or a doc.
[x] I verified claims against CODE, not comments — ContainerValidator's tier-window rejection,
    Instantiator.Draw's group rule and stream names, AtomStreams' three constants,
    SeededRng.DeriveStream, the rarity table's absent production consumer, the correlation-id
    UNIQUE keys, DemonMaterialCatalog, WaveCatalog's levels, and AwardSouls were all opened.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no suite was run.** The one
    claim that needs executing is §3.2's "adding an optional DrawEnvelope moves no golden" —
    `dotnet test tests\FusionRpg.Core.Tests` proves or disproves it in one command, and it is
    the first task of any build from this document.
[x] Nothing contradicts a §2 invariant of the enrichment contract.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item-program map, plan, or task list exists yet** — reconciliation into the ideal
    happens in one pass after all lanes land, per the contract's parent-intent note.
```
