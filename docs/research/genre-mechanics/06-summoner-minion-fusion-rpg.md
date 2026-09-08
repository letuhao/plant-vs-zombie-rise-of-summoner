# Summoner, minion, pet and monster-fusion systems in RPGs

**Captured 2026-09-02.** Research only — nothing here is a proposal. Where a number is a tally over
primary data rather than a quote, it is marked **(computed)**. Where a judgement is mine rather than a
source's, it is marked **INFERENCE**.

**Read first, do not re-derive:** [`docs/research/game-design/03-roster-scale.md`](../game-design/03-roster-scale.md)
already establishes roster/grid occupancy, rarity's role, power-creep history and the dead usage tail
across eight collectible games; [`docs/research/game-design/README.md`](../game-design/README.md) is
its index; [`docs/research/arpg-effects/00-index.md`](../arpg-effects/00-index.md) covers modifier
stacking, procs, ailments and crit. This file cites those and goes past them. Its unique contribution
is **fusion algorithms, minion power derivation, creature agency, and upkeep**.

---

## The finding in one paragraph

Every shipped monster-fusion system answers one question — *given inputs, what comes out* — and the
answers split cleanly into **computed rules** and **authored lookup tables**, with the successful games
running both at once. Shin Megami Tensei is the reference implementation and it is a rule engine with a
hand-authored escape hatch: an N×N race chart picks the output *family*, an arithmetic rule
(`result level ≥ (levelA + levelB)/2 + 1`, then take the lowest demon of that family at or above it)
picks the output *individual*, and ~30–70 hand-written "special recipes" per game cover the cases the
rule cannot reach — and the whole thing only works because **(race, level) is a primary key over the
entire roster with zero collisions in all five games measured (computed)**. Minion power derivation is
a genuinely open design space with four incompatible models shipped side by side (skill-level tables in
Diablo II, a monster-level curve indexed by *gem* level in Path of Exile, player-level-as-base-stats in
Cassette Beasts, percentage-of-owner in Diablo III), and Path of Exile is the only one that ships
**two** level tables — allied minions get a life curve worth **16.5% of the same-level hostile monster
curve at level 100 (computed)** while damage stays at 91%. Creature agency — loyalty, obedience,
negotiation — is the rarest of these systems and the evidence for why is unusually direct: World of
Warcraft shipped a six-rank pet loyalty ladder and a three-tier happiness meter, deleted loyalty in
2008 and happiness in 2011, and the patch note explicitly says the happy-pet damage bonus "will now be
baseline" — the entire system's net effect was a −25%/+25% band you paid chores to keep at zero. The
industry's answer to upkeep is Warcraft III's, and Blizzard published the rationale: three flat
income-tax brackets on army size, with a designer statement that "High Upkeep is MEANT to be very
punishing" and that the reason it exists at all is that more units make heroes matter less.

---

## 1. Shin Megami Tensei / Persona demon fusion — the reference implementation

### 1.1 Sources, and why these are trustworthy

The algorithm below is read out of the source of the open-source
[megaten-fusion-tool](https://github.com/aqiu384/megaten-fusion-tool), which is a working
reimplementation driven by per-game datamined JSON tables, not a prose description. Files cited by
path. All tallies over those tables are marked (computed). A long-form mechanics write-up
([eirikrjs, *Fusion Insanity*, 2021-03](https://eirikrjs.blogspot.com/2021/03/nocturne-fusion.html))
supplies the Nocturne-specific numbers the calculator does not model.

### 1.2 The normal (two-ingredient) fusion algorithm — exact form

Two steps, in this order.

**Step 1 — the race chart picks the output family.** A symmetric `race × race → race` table. Not
computed, entirely authored. From
[`src/app/*/data/fusion-chart.json`](https://github.com/aqiu384/megaten-fusion-tool/tree/master/src/app):

| Game | Races in the chart | Chart cells (lower triangle) | Cells filled | Fill rate |
|---|---:|---:|---:|---:|
| Shin Megami Tensei | 22 | 253 | 239 | **94.5%** |
| Shin Megami Tensei II | 23 | 276 | 267 | **96.7%** |
| **SMT III: Nocturne** | **30** | **465** | **370** | **79.6%** |
| **SMT IV** | **47** | **1,128** | **869** | **77.0%** |
| **SMT V** | 32 | 528 | 419 | **79.4%** |
| Persona 3 Reload | 22 (arcana) | 253 | 230 | 90.9% |
| Persona 4 | 21 (arcana) | 441 | 432 | 98.0% |
| Persona 5 | 21 (arcana) | 231 | 206 | **89.2%** |

*(all computed from the shipped chart JSON)*

An empty cell means those two races **cannot** be fused together at all. That is the design lever: the
20% of blank cells in Nocturne and SMT IV is where "you cannot get there from here" lives. SMT IV is
the largest chart any of these games shipped — 47 races, 1,128 cells, 869 authored — and it is
**2.4× the cell count of Nocturne** (computed).

**Step 2 — an arithmetic rule picks the output individual.** From
[`smt-nonelem-fusions.ts`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/compendium/fusions/smt-nonelem-fusions.ts),
the candidate levels of the result race are binned by `2 × (levelR − lvlModifier) − levelA`, and the
chosen result is the first bin the second parent's level falls into. Rearranged, the acceptance
condition is:

```
levelA + levelB  ≤  2 · (levelR − lvlModifier)
```

`lvlModifier` is **1** in every game that sets it — SMT III, SMT IV, SMT IV Apocalypse, SMT V, Strange
Journey, and hardcoded as `const lvlModifier = 1` in the Persona same-arcana path
([`per-nonelem-fusions.ts`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/compendium/fusions/per-nonelem-fusions.ts)).
So the rule in plain terms:

> **The result is the lowest-level demon of the result race whose level is at least
> `(levelA + levelB) / 2 + 1`.**

This is the "average plus one" rule, and the "+1" is doing something specific: it guarantees the child
is strictly *above* the parents' average, so a fusion chain always climbs. The rounding is not a
rounding at all — it is a **ceiling to the next demon that exists in that race**. Fuse a level-12 Pixie
with a level-9 Apsaras, target `10.5 + 1 = 11.5`, and if the Holy race has no demon at 12 you get the
level-13 Shiisaa
([Game8, Nocturne fusion chart guide](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/332176)).

**Step 3 — the whole thing only works because of a primary key.** Tallied over the shipped demon tables:

| Game | Demons | Races | Median demons per race | Max | Distinct `(race, level)` cells | **Collisions** |
|---|---:|---:|---:|---:|---:|---:|
| SMT III: Nocturne | 195 | 35 | 6 | 10 | 195 | **0** |
| SMT IV | 426 | 48 | 9 | 19 | 426 | **0** |
| SMT V | 242 | 36 | 6 | 14 | 242 | **0** |
| Persona 4 | 187 | 22 | 9 | 11 | 187 | **0** |
| Persona 5 | 210 | 21 | 9 | 16 | 210 | **0** |

*(computed from `demon-data.json` per game)*

**No two obtainable demons in any of these games share a race and a level.** `(race, level)` addresses
exactly one creature, always. The fusion rule is a *lookup by computed key* into a table the designers
guaranteed is unique. Remove that guarantee and the "+1 then take the lowest at or above" step becomes
ambiguous and the algorithm stops being a function.

This is the same structural trick [`03-roster-scale.md` §1](../game-design/03-roster-scale.md) records
for Summoners War, where `family_id × element` is filled 821/870 with median exactly 1 and max exactly
1. Two unrelated franchises, same answer: **make the grid the primary key.**

### 1.3 Element / Mitama fusion — a rank shift, not a level shift

Fusing a normal demon with an "element" demon (Erthys, Aeros, Aquans, Flaemis in Nocturne; the eight
Treasure Demons in Persona 5) does not change race. It moves the demon **one slot up or down in that
race's level-ordered list**. From
[`smt3/data/element-chart.json`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/smt3/data/element-chart.json)
the table is `race × element → ±1`, 21 races × 4 elements in Nocturne, values only ever `+1` or `−1`.
The calculator implements it as an index shift into the sorted level list
([`fuseWithElement`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/compendium/fusions/smt-nonelem-fusions.ts)),
padding the list with sentinels at both ends so the ends of a race are dead.

**INFERENCE.** This is a materially different primitive from the level rule: it is a *neighbour walk on
an ordered list*, so its cost is one signed integer per (race, element) cell, and it can never produce
an out-of-family result. It is the cheapest "nudge" operator in the whole system.

### 1.4 Special fusion — the authored escape hatch

Some demons cannot be produced by the rule at all and have a hand-written ingredient list. Ingredient
counts, tallied from `special-recipes.json`:

| Game | Special recipes | 2 ingredients | 3 | 4 | 5 | 6 | 10+ | Unbuildable (0) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| SMT III: Nocturne | 56 | — | — | 1 | — | — | 1 (10) | 36 + 18 with a single named source |
| SMT IV | **67** | 11 | **43** | 13 | — | — | — | — |
| SMT V | 46 | 6 | **31** | 9 | — | — | — | — |
| Persona 3 Reload | 22 | 2 | 7 | 5 | 3 | 5 | — | — |
| Persona 4 | 17 | 3 | 1 | 4 | 4 | 4 | 1 (12) | — |
| Persona 5 | 29 | 3 | 8 | 4 | 3 | 3 | — | 8 (Treasure Demons) |

*(computed)*

The shape differs by sub-series. **SMT IV and SMT V concentrate hard on 3-ingredient triple fusion**
(64% and 67% of specials respectively, computed). **Persona spreads across 2–6**, and its top-end
recipes are deliberately enormous: Lucifer is `Anubis + Ananta + Trumpeter + Michael + Metatron + Satan`,
Satanael is `Arsene + Anzu + Ishtar + Satan + Lucifer + Michael`
([`p5/data/special-recipes.json`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/p5/data/special-recipes.json)).
Michael is itself a 3-part special, and Satan and Lucifer are specials — so Satanael is a
**recipe tree**, not a recipe.

**What this costs the designer:** ~30–70 authored rows per game, each of which must be reachable
(every ingredient must itself be obtainable) and each of which silently overrides the rule for that
output. The calculator has to special-case them in both directions — `fuseWithDiffRace` patches
computed recipes when a special exists for the same pair.

### 1.5 Skill inheritance

Three separate gates, all data-driven.

**Gate 1 — how many skills transfer.** Persona 5's rule is a step function on the *total* skill count
of both parents, reported by players as
3–5 → 1, 6–8 → 2, 9–12 → 3, 13–22 → 4, 23–31 → 5, 32–41 → 6, 42+ → 8
([GameFAQs P5 board thread](https://gamefaqs.gamespot.com/boards/835628-persona-5/75265605); the
published table has an off-by-one ambiguity at 22/23 and I could not resolve it against game data).
Nocturne caps at **5 inherited skills in a normal two-parent fusion and 6 in sacrificial fusion**, with
the count derived from averaging the parents' skill counts (eirikrjs, above).

**Gate 2 — how many slots the child has.** Straight from the calculator configs
(`maxSkillSlots` in each `compendium.module.ts`):

| Game | Skill slots |
|---|---:|
| SMT III, SMT IV, SMT IV Apocalypse, SMT V | **8** |
| Persona 3 / 3 Reload / 4 / 5 | **8** |
| **Persona 5 Royal** | **10** |
| Strange Journey, Persona Q, Persona Q2 | **6** |

*(sourced from the shipped configs)*

Persona 5 Royal raising 8 → 10 is the only slot-count change in the series' modern era, and it is an
enhanced re-release. **INFERENCE:** slot count is the single most load-bearing number in the whole
system, because it sets how many decisions a fusion is, and they moved it exactly once.

**Gate 3 — what a given demon is *allowed* to inherit.** Each demon carries an affinity bitmask over
skill families. Nocturne uses a **9-character `o`/`x` string** per demon over 9 inherit categories;
tallying the whole roster, the distribution of allowed categories is
`0:2, 1:8, 2:17, 3:36, 4:49, 5:63, 6:19, 7:1` with **median 4 of 9 (computed)** — so the average demon
is barred from more than half the skill vocabulary.

Persona 5 replaces the per-demon mask with a **shared 14 × 12 grid**: 14 "inheritance types" a Persona
can have, against 12 skill element families, `o` = allowed. From
[`p5/data/inheritance-types.json`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/p5/data/inheritance-types.json):

| Inheritance type | Allowed of 12 |
|---|---:|
| Support, Almighty | **12** |
| Fire, Ice, Elec, Wind, Psy, Nuke | 11 each |
| Ailment | 10 |
| Healing | 9 |
| Bless, Curse | 8 |
| **Phys** | **4** |
| **None** | **0** |

**129 of 168 cells allowed, 77% (computed).** The elemental types each block exactly one thing — their
own element — which is the whole joke: a fire Persona cannot inherit fire skills, so you must fuse
*across* elements to build a coverage kit. Phys at 4/12 and None at 0/12 are the two hard walls.

**INFERENCE — the design shape.** Nocturne pays `O(demons)` for its mask; Persona 5 pays
`O(types × families)` for a shared grid and then one enum per demon. The shared grid is dramatically
cheaper to maintain and to explain, and it is what a later game in the same studio chose.

### 1.6 Fusion accidents

Sources disagree and I am reporting the disagreement rather than picking.

| Game | Base rate | Boosted | Source |
|---|---|---|---|
| SMT III: Nocturne | **1/256** normal fusion | **1/16 during Full Kagutsuchi** | [eirikrjs](https://eirikrjs.blogspot.com/2021/03/nocturne-fusion.html) |
| SMT III: Nocturne (alt) | **1/64** | +1/64 per dead (0 HP) ingredient; a Foul-race ingredient **doubles** the final chance; **max 6/64** | search-surfaced summary of the Megami Tensei wiki fusion-accident page |
| Earlier SMT | **8/256** off full moon, **16/256** on full moon | — | same |
| SMT V: Vengeance | Full moon only; otherwise **impossible** | "Mutative Element" miracle stacked with full moon reported at **~40–50%+** | [TheGamer](https://www.thegamer.com/shin-megami-tensei-5-vengeance-fusion-accidents-explained-guide/), [Nintendo Everything](https://nintendoeverything.com/how-to-trigger-fusion-accidents-in-shin-megami-tensei-v-vengeance/) |

Structural facts that are not in dispute: **special fusions and Fiend fusions are immune to accidents**;
in SMT V: Vengeance **3 of the 40 new demons are obtainable only through an accident**
([Game8](https://game8.co/games/Shin-Megami-Tensei-V/archives/350405) — accident-exclusive demon
list). In Nocturne the accident result is drawn from a **cursed chart** shipped as its own data file
(`smt3/data/cursed-chart.json`), i.e. accidents are a *second* authored race table, not noise.

**What the accident solves for the player:** it converts a fully solved system into one with a lottery
ticket, and it gates a handful of creatures behind the lottery so the lottery is not optional. **What it
costs:** a second race chart plus a moon-phase clock plus an immunity list. **What breaks when tuned
wrong:** at 1/256 it is invisible and players never learn the rule exists; at 40%+ (Vengeance with the
miracle) it stops being an accident and becomes a mode you toggle.

### 1.7 The Compendium — re-summon what you registered

The Compendium registers a demon's level, stats, skills and affinities at the moment you register it,
and lets you buy that exact snapshot back for money. Registration is the reason fusing a demon is not a
loss: the parent is consumed, the record is not.

Cost is a function of level and stats. In the Nocturne calculator the price is reconstructed as
`100 × floor((sum of stats)² / 20) / 2`
([`smt3/compendium.module.ts`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/smt3/compendium.module.ts)) —
**quadratic in the stat sum**. SMT IV and SMT V ship an explicit `price` per demon; fitting those:

| Game | Median price at level ~10 | ~30 | ~50 | ~70 | ~90 | Implied exponent on level |
|---|---:|---:|---:|---:|---:|---:|
| SMT IV | 1,240 | 4,716 | 10,701 | 15,879 | 26,970 | **≈ 1.40** |
| SMT V | 1,389 | 7,082 | 16,652 | 31,367 | **80,058** | **≈ 1.84** |

*(computed from `demon-data.json`; exponent from the level-10 → level-90 ratio)*

**The re-summon sink is superlinear and close to quadratic, and SMT V made it steeper than SMT IV.**
That is the same shape this project's own power ladder uses.

**What it solves:** it removes the fear of fusing your favourite, which is the single behaviour that
kills a fusion economy. **What breaks when tuned wrong:** too cheap and the roster stops being a
resource; too expensive and players hoard, which is the failure the Compendium exists to prevent.

### 1.8 Negotiation and recruitment

Recruitment is a conversation, not a capture. Reported mechanics
([Game8 Nocturne](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/332036),
[Game8 SMT V](https://game8.co/games/Shin-Megami-Tensei-V/archives/348793)):

- The demon demands **items, money, or HP/MP** and the exchange is not a guaranteed purchase — paying
  can still fail and refusing can still succeed.
- **Moon phase changes demon mood** — some are agreeable at full moon, some are worse.
- Hard preconditions: **a free party slot** and **no demon of the same species already in the party**.
- The money a demon offers correlates with its level and the protagonist's **Luck**.

I could not find a datamined success-probability formula for any modern entry (see
*What I could not find*).

Two structural rules that matter more than the odds:

1. **A demon cannot exceed the protagonist's level.** This is the series' hard ceiling on the whole
   loop, and it is what makes the protagonist's own level the gate on the roster rather than the
   grind.
2. **Persona locks 21 top-tier Personas behind maxed social relationships.** From
   [`p5/data/demon-unlocks.json`](https://github.com/aqiu384/megaten-fusion-tool/blob/master/src/app/p5/data/demon-unlocks.json),
   the "Maxed Confidant" category holds exactly one entry per arcana — Vishnu at Fool Rank 10, Metatron
   at Justice Rank 10, Lucifer at Star Rank 10, Satan at Judgement Rank 10, and 17 more. **A
   relationship stat is a hard acquisition gate on 10% of the roster (computed: 21 of 210).**

---

## 2. Monster collection and breeding elsewhere

### 2.1 Pokémon — breeding, IVs, EVs

Read from the [pokeemerald](https://github.com/pret/pokeemerald) disassembly, which is the actual
Generation III logic.

**Egg production.** Checked once every 256 steps
(`(daycare->mons[1].steps & 0xFF) == 0xFF`), then `if (compatibility > (Random() * 100) / USHRT_MAX)`.
Compatibility is one of four authored constants
([`include/constants/daycare.h`](https://github.com/pret/pokeemerald/blob/master/include/constants/daycare.h)):
**0 / 20 / 50 / 70**. Eggs hatch at level **5** (`EGG_HATCH_LEVEL`).

**IV inheritance (Gen III).** `INHERITED_IV_COUNT` is **3**. The algorithm
([`src/daycare.c`, `InheritIVs`](https://github.com/pret/pokeemerald/blob/master/src/daycare.c)) picks
3 of the 6 stat indices, then independently picks a parent per inherited IV
(`whichParents[i] = Random() % DAYCARE_MON_COUNT`), leaving the other 3 IVs rolled fresh in 0–31.
The decomp comments a **shipped bug**: the removal step deletes list position `i` rather than the
position that was actually drawn, so HP and Defence are under-represented and an IV index can be picked
twice — and the comment notes FireRed/LeafGreen and Ruby/Sapphire got it wrong differently.

**Modern generations** widen the same rule rather than replacing it: a **Destiny Knot raises inherited
IVs from 3 to 5**, and an **Everstone passes the holder's Nature at 100%**
([VGC Guide, IV breeding](https://www.vgcguide.com/iv-breeding)) — in Gen III the Everstone nature pass
was a coin flip (`Random() >= USHRT_MAX / 2` returns "don't inherit"), so the modern version removed the
randomness entirely.

**EVs**, for scale: `MAX_PER_STAT_EVS 255`, `MAX_TOTAL_EVS 510`, IVs 0–31, `MAX_LEVEL 100`
([`include/constants/pokemon.h`](https://github.com/pret/pokeemerald/blob/master/include/constants/pokemon.h)).

**Classification: computed rule, no lookup table.** The output *species* is a lookup (egg group + mother's
species), but everything numeric is a per-instance roll with an inheritance mask.

### 2.2 Monster Rancher — a data blob is the input

The most extreme "seed → concrete creature" system anyone shipped. The input is **a music CD the player
owns**; the game reads its table of contents and generates a monster. Reverse-engineered by
SmilingFaces96 and documented at
[legendcup, MR2 disk read process](https://legendcup.com/mr2researchdiskread.php):

- Three TOC values are read: lead-out time (`LN-PMin`, `LN-PSec`), track-2 start (`T2-PSec`), last-track
  start (`LT-PMin`, `LT-PSec`).
- **Main breed index = `LN-PMin − (LT-PMin % 16)`**, treated as hexadecimal, clamped to 0–127, then
  looked up in a 128-entry table mapping to **28 monster types**.
- **Sub-breed**: `LT-PSec` indexes a 60-entry value table; that value is taken modulo the number of
  sub-breeds the main type has (1–24); the remainder indexes the main-type's sub-breed table.
- **Stats**: `LN-PSec` and `T2-PSec` each index the same 60-row offset table; each row holds **7 integer
  modifiers in the range −10 to +20**; both rows are summed and added to the breed's base stats
  (life, power, intelligence, skill, speed, defence, starting lifespan in weeks).
- **Special monsters**: if all three TOC values match a row in a hard-coded table (separate NTSC and PAL
  tables), the game replaces the *entire* monster record — lifespan, nature, six attributes, gain rates,
  arena speed, guts regen, battle specials, techniques, training bonuses.
- Sector conversion: `PLBA = ((PMin × 60 + PSec) × 75) + PFrame − 150`.

**Classification: hybrid, and it is the cleanest example of the pattern.** A hard-coded table takes
priority; if nothing matches, a small arithmetic index plus two additive offset rows produce a monster.
Note what the offsets *are*: a two-of-sixty roll on a fixed table, not a per-stat random draw.
**INFERENCE:** that makes the whole generator deterministic and reproducible from 5 small integers,
which is exactly why the community could build a "Make-A-Monster" tool that reverses it.

Monster Rancher 2 also has creature-to-creature **combining**: two frozen monsters plus 500 gold.
The **first-chosen parent is the "seed"** and dominates technique inheritance and outcome probability;
techniques transfer at a truncated 2/3 rate when the main breed is unchanged, or up to 4 techniques
(one each of Heavy / Hit / Withering / Sharp) when the main breed changes; stat transfer quality depends
on how well both parents' stat orderings agree with the result's baseline
([legendcup MR2 combining guides](https://legendcup.com/faqmr2combining.php),
[Combining FAQ](https://gamefaqs.gamespot.com/ps/197977-monster-rancher-2/faqs/41787)).

### 2.3 Dragon Quest Monsters — synthesis and the "+" value

From the [Dragon Quest Wiki, Monster synthesis](https://dragon-quest.org/wiki/Monster_synthesis):

- **Inputs**: two monsters at **level 10 or higher**, of **opposite polarity** (`+` with `−`) in Joker 1
  and 2. Joker 3 removed polarity; Dark Prince removed the `+` system entirely.
- **Output species**: a lookup — but with an item override. Equipping one parent with the **phoenix
  sceptre** forces the child to that parent's species. That single item converts the whole system from
  "discover the recipe" to "direct the recipe".
- **Stats**: *"The synthesized monster's stats are 1/4th of both component monsters, rounded down."* So
  the child's base stats are `base(species) + parentA/4 + parentB/4` — an additive inheritance that
  compounds across generations. **Growth rates inherit at 25% from each component's species**, which is
  why you can breed a slime that grows like a Gigantes.
- **The `+` value**: each synthesis raises the higher-level parent's `+` by 1; the weaker parent's `+` is
  wasted (Joker 1–2). The `+` value is a **level-cap key**, not a stat: `+5` raises the cap from 50 to 75
  (and requires the components' combined level to be ≥ 21); `+10` reaches level 100 (combined level > 40).
  Joker 2 Professional dropped the level-sum requirement, moved the cap-75 threshold to `+4`, and made
  the weaker parent's `+` partially inheritable — **half** at combined level 21–40, **all** at 41+.
  Terry's Wonderland 3D turned `+` into a **rank** key instead: `+25` promotes rank D or lower to C,
  `+50` promotes C/B to A.
- **Skills**: a synthesized monster inherits **three Talents**; **half** the accumulated skill points are
  retained and **a quarter** of the unspent points become free to reassign; mastered ranked talents
  upgrade a tier.

**Classification: computed rule for stats, lookup table for species, with a per-item override.**
**What the `+` value solves:** it makes the *number of times you have fused* a first-class, visible,
persistent stat, so a long fusion chain is legible as a number rather than as a stat block. **What breaks
when tuned wrong:** the level-sum requirements. Joker 1–2 required the components' combined level ≥ 21
for `+5` and > 40 for `+10`, then Joker 2 Professional abolished those requirements outright — a
retreat from a gate that made the grind visible before it made it fun.

### 2.4 Digimon — requirement gates instead of recipes

Digivolution is not fusion at all: it is a **conjunctive stat gate** on a single creature. Digimon Story:
Cyber Sleuth ships **249 Digimon** (341 in the Complete Edition with Hacker's Memory)
([Wikipedia](https://en.wikipedia.org/wiki/Digimon_Story:_Cyber_Sleuth)). WarGreymon's gate, as shipped:

> Level **55**, ATK **160**, DEF **130**, HP **1700**, ABI **20**, CAM **80%**
> ([Grindosaur, WarGreymon](https://www.grindosaur.com/en/games/digimon-story-cyber-sleuth/digimon/wargreymon))

Six conditions, all required. **ABI (Ability)** is the interesting one: it is a persistent counter that
rises when you *de-digivolve* and re-raise a Digimon, so the highest forms require you to deliberately
undo your progress several times. **CAM (Camaraderie)** is a relationship stat — see §5.

Each Digimon also carries a **Memory Usage** cost that a party budget must cover. Tallied over all 341:

| Memory cost | 2 | 3 | 4 | 5 | 6 | 8 | 10 | 12 | 14 | 16 | 18 | 20 | 22 | 25 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Digimon | 5 | 12 | 24 | 25 | 46 | 38 | 6 | 45 | 27 | 3 | 41 | 32 | 21 | 16 |

Stage distribution: Mega 98, Champion 79, Ultimate 78, Rookie 50, Ultra 14, Training-2 12, Training-1 5,
Armor 3 *(all computed from the [Grindosaur field guide table](https://www.grindosaur.com/en/games/digimon-story-cyber-sleuth/digimon))*.
Memory cost tracks stage closely — Training-1 is always 2, Ultra is always 25 — but not perfectly: some
Mega Digimon cost 6 and some Rookies cost 18.

**Classification: pure gate, no fusion arithmetic at all.** **What it solves:** it makes a creature's
identity permanent and its *form* a resource you spend stats to change. **What it costs:** one authored
requirement row per evolution edge, and the edges are dense (WarGreymon alone digivolves from three
different Ultimates).

### 2.5 Monster Hunter Stories — genes on a bingo board

The input is a **gene extracted from one creature**; the output is a **slot placement on another
creature's 3×3 board**, and the payoff is geometric rather than additive.

- Every Monstie hatches with a **3×3 gene board**; some slots are open at hatch, some unlock by level,
  some need stimulant items. **Rarer eggs hatch with more open slots and better native stats**
  ([PC Gamer, MHS2 gene guide](https://www.pcgamer.com/monster-hunter-stories-2-gene-rainbow-rite-of-channeling/)).
- The **Rite of Channeling** transfers one gene from a donor Monstie into a chosen slot on the recipient
  — and the donor is consumed.
- **Three genes of the same colour or attribute in a row, column or diagonal is a "Bingo Bonus"**, worth
  up to **+100% damage** for that attribute
  ([Gameranx bingo board guide](https://gameranx.com/updates/id/242545/article/monster-hunter-stories-2-how-to-boost-monsties-with-gene-channeling-bingo-board-guide/)).
- A **Free Bingo Gene** (rainbow) counts as any colour, grants no ability of its own, and **only one may
  be inserted per Monstie**.

**Classification: neither table nor arithmetic — a spatial constraint puzzle.**
**INFERENCE:** this is the cheapest deep system in this whole survey. The designer authors one gene per
ability and one attribute colour per gene; all the depth comes from 8 winning lines on a 9-cell grid,
which is free. The rainbow-gene limit of one exists because without it the grid stops constraining
anything.

### 2.6 Yo-kai Watch, Coromon, Cassette Beasts

**Yo-kai Watch** fusion is a named-pair lookup: a Yo-kai fused with *a specific item* or *a specific
other Yo-kai* becomes *one specific named result*, alongside ordinary level evolution. Recruitment is by
**feeding a Yo-kai its preferred food before or during battle**, after which it *may or may not* offer
its Medal — an explicit non-guaranteed recruit. 8 tribes with strengths/weaknesses; **Legendary Yo-kai
require completing a specific set in the Medallium**
([Wikipedia, Yo-kai Watch](https://en.wikipedia.org/wiki/Yo-kai_Watch_(video_game))).

**Cassette Beasts** is the procedural extreme and the most useful data point in this section.
**120 monsters, "over 14,000 fusions"** — and 120 × 119 = **14,280 ordered pairs (computed)**, so fusion
is over **ordered** pairs, not unordered. The developers state the method plainly: *"In order to create
the 14,000 possible fusions in the game, the developers made the monsters modular, allowing for parts to
be combined automatically by the game."* Fusion is **temporary and in-combat**, not a permanent
creature. Its stat model is also unusual and directly relevant here: *"The player receives experience
points instead of the monsters, meaning that player characters provide base stats that monsters then add
to"* ([Wikipedia, Cassette Beasts](https://en.wikipedia.org/wiki/Cassette_Beasts)).

The reception note is worth recording verbatim as a warning: TheGamer's reviewer *"liked the designs"* of
the base monsters *"but did not feel as positively about the fusions, the majority of which were
procedurally generated."* **120 authored designs read well; 14,000 generated ones did not.**

**Coromon** — I could not source its fusion mechanics; see *What I could not find*.

---

## 3. ⭐ The fusion input → output comparison

This is the load-bearing table. **"Rule"** means the output is computed from the inputs' numbers.
**"Table"** means a designer wrote the answer down. **"Hybrid"** means both, with a stated precedence.

| System | Fusion INPUT | What determines the OUTPUT identity | What determines OUTPUT numbers | Rule / Table | Inputs consumed? | Result is |
|---|---|---|---|---|---|---|
| **SMT / Persona normal fusion** | 2 demons | **Table** (race × race chart, 22–47 races, 77–98% filled) | **Rule**: lowest demon of result race with `level ≥ (lA+lB)/2 + 1` | **Hybrid** | Both | Permanent, new creature |
| **SMT element fusion** | 1 demon + 1 element demon | Same race, always | **Rule**: ±1 **rank** in the race's level-ordered list (table of ±1 per race×element) | Hybrid | Both | Permanent |
| **SMT special fusion** | 2–6 named demons (median 3 in SMT IV/V) | **Table only** — hand-written recipe | Fixed — the named demon | **Table** | All | Permanent |
| **SMT fusion accident** | Any fusion, on the right moon phase | **Table** (a second, "cursed" race chart) | Fixed | Table | Both | Permanent, unpredictable |
| **Pokémon breeding** | 2 compatible parents | **Table** (egg group + mother's species → base-form species) | **Rule**: 3 (or 5 w/ Destiny Knot) of 6 IVs copied from a randomly chosen parent per stat; rest rolled 0–31; nature from Everstone | **Hybrid** | Neither — parents survive | New level-5 creature |
| **Monster Rancher CD** | A music CD's TOC (5 integers) | **Table** first (hard-coded special list), else **rule**: `LN-PMin − (LT-PMin % 16)` → 1 of 28 breeds | **Rule**: two 60-row offset tables summed onto breed base; 7 modifiers each, −10..+20 | **Hybrid, table wins** | N/A | New creature |
| **Monster Rancher combining** | 2 frozen monsters + 500g | **Table** (breed pair → result breed), first parent dominant | **Rule**: stat transfer scaled by how well parent stat orderings match the result baseline; techniques 2/3 truncated | Hybrid | Both | Permanent |
| **Dragon Quest Monsters** | 2 monsters, level ≥ 10, opposite polarity | **Table** (species pair → species), overridable by the **phoenix sceptre** item | **Rule**: `base + parentA/4 + parentB/4`; growth rate 25% from each; `+` value +1 per synthesis | **Hybrid** | Both | Permanent, level reset |
| **Digimon digivolution** | **1** creature meeting a 6-way stat gate | **Table** (edge list per Digimon) | Fixed — the target's own statline | **Table** | None — same creature | Same creature, new form (reversible) |
| **Monster Hunter Stories** | 1 gene from a donor Monstie | Recipient keeps its identity | **Rule**: geometry — 3-in-a-row on a 3×3 grid = bonus up to +100% | **Rule** | Donor | Same creature, modified |
| **Yo-kai Watch** | 1 Yo-kai + item, or 2 named Yo-kai | **Table only** | Fixed | **Table** | Both | Permanent |
| **Cassette Beasts** | 2 monsters, **in combat** | **Rule** — modular part assembly over ordered pairs; 120 → 14,000+ | Derived from both, plus the **player's** level-driven base stats | **Rule** | Neither | **Temporary**, ends with the fight |
| **Summoners War** | 4 specific maxed monsters + mana | **Table only** — 25 recipes, every one exactly 4 ingredients | Fixed — the product's own statline | **Table** | All 4 | Permanent |

*(SMT/Persona rows computed from the fusion-tool data; Summoners War row computed from the
[SWARFARM `/api/v2/fusions/` endpoint](https://swarfarm.com/api/v2/fusions/): **25 recipes, all with
exactly 4 ingredients, costing 100,000 or 500,000 mana**.)*

### What the table says

1. **Nobody ships a pure table at scale and nobody ships a pure rule at scale.** The two systems that
   are pure table (Yo-kai Watch, Summoners War) keep the recipe count tiny — Summoners War has **25
   recipes for an 832-monster roster** (roster figure from
   [`03-roster-scale.md`](../game-design/03-roster-scale.md)). The one system that is pure rule
   (Cassette Beasts) got its 14,000 outputs criticised as procedural.
2. **The table almost always picks the *family* and the rule almost always picks the *individual*.**
   SMT, Pokémon and Monster Rancher independently arrived at that split.
3. **The precedence order is always "authored beats computed."** Monster Rancher checks its hard-coded
   list first. SMT's special recipes override the chart. Dragon Quest's phoenix sceptre overrides
   everything. **INFERENCE:** this is the only ordering that lets a designer fix a bad outcome without
   touching the generator.
4. **Whether the inputs are consumed is the economic switch, and it is independent of everything else.**
   Pokémon consumes nothing and is a breeding treadmill; SMT consumes both and needed a Compendium to
   make that survivable; Digimon consumes nothing because there is only one creature.
5. **Temporary fusion is a live design space with exactly one shipped example here.** Cassette Beasts
   fuses in combat and unfuses after, which is why it can afford 14,000 outputs that are only ever seen
   for 30 seconds.

---

## 4. Summoner and minion classes in ARPGs

**The question for this section:** how does a summoned unit's power derive from the summoner's power?
Four incompatible models ship, sometimes inside the same game.

### 4.1 Diablo II Necromancer — flat tables indexed by skill level

Read from Blizzard's own skill reference,
[classic.battle.net/diablo2exp/skills/necromancer-summoning.shtml](http://classic.battle.net/diablo2exp/skills/necromancer-summoning.shtml).

**Roster caps are a skill-level table, not a constant:**

| Raise Skeleton level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | … | 20 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|
| Skeletons | 1 | 2 | 3 | 3 | 3 | 4 | 4 | 4 | 5 | 5 | … | **8** |

Same curve for Skeletal Mages: **+1 minion every 3 skill levels from level 3 (computed)**. And the
count is *not* capped by the table — Blizzard states plainly:

> *"The number of Skeletons or Skeletal Mages you can have is only limited by the number of skill points
> you have. Wearing a magic item that raises Skill levels or using a Skill Boost Shrine also increases
> the number of Skeletons and Skeletal Mages you can raise. When the Skill Boost Shrine effect wears off
> the extra Skeletons disintegrate."*

**Minion stats come from the skill table and the difficulty, not the player.** Raise Skeleton at level
20: **Life 199, Damage 37–39, Attack Bonus 305, Defense 305**. Skeleton Mastery is a flat additive
synergy: at level 20 it adds **+160 Life and +40 Damage** to skeletons, and **+100% monster Life /
+200% monster Damage** to Revives.

**The one closed-form formula Blizzard published**, for Skeletal Mage hit points:

```
HP = ((SkillLevel − 3) × 0.07 + 1.0) × BaseLife
BaseLife = 61 (Normal) / 88 (Nightmare) / 123 (Hell)
```

**The exact synergy math**, quoted from the same page:

| Synergy | Effect |
|---|---|
| Clay Golem → all golems | **+20 Attack Rating per level** |
| Blood Golem → Iron/Fire Golem | **+5% Life per level** |
| Iron Golem → Blood/Fire Golem | **+35 Defense per level** |
| Fire Golem → Blood/Iron Golem | **+6% Damage per level** |
| Golem Mastery | **+20% HP per level** (400% at 20), +6→33% velocity, +25 attack per level |
| Summon Resist | All resistances **28% at level 1 → 66% at level 20** (heavily diminishing) |
| Skeleton Mastery → Skeletal Mage | Adds directly to a **"Relative Skill Level"** = `max(1, floor(mageLevel/2)) + masteryLevel` (computed from the published cross-table) |

**Revive is the odd one out and is the stat-inheritance model in its purest form:** count = skill level
exactly (1–20, no rounding), **HP = the original creature's HP + 200%**, duration 180 seconds. The
minion's power is the *corpse's* power, scaled.

**What is explicitly not player-derived:** nothing in these tables reads the Necromancer's level,
strength, or gear. The only player-level coupling Blizzard names is incidental — *"these monsters are
generated at the caster's level, and level plays an important part in the 'to hit' formula."*

Flavour detail worth recording because it is the cheapest possible per-instance variance:
**"Skeletons, when created, have a 5% chance to spawn with a shield… skeletons only have a 3% chance to
block."**

### 4.2 Path of Exile — a monster-level curve indexed by *gem* level, and a separate ally table

Read from [Path of Building Community](https://github.com/PathOfBuildingCommunity/PathOfBuilding),
whose `src/Data/Minions.lua` is generated from Grinding Gear Games' own monster data
(*"This file is automatically generated, do not edit! … Monster data (c) Grinding Gear Games"*).

**Each minion is one row of multipliers, not a statline.**

```lua
minions["RaisedZombie"] = {
  life = 3.75, damage = 1.65, damageSpread = 0.4,
  attackTime = 1.17, accuracy = 3.4,
  fireResist = 40, coldResist = 40, lightningResist = 40, chaosResist = 20,
  limit = "ActiveZombieLimit", ...
}
```

**The resolution, from [`CalcPerform.lua`](https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/dev/src/Modules/CalcPerform.lua)
and [`CalcActiveSkill.lua`](https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/dev/src/Modules/CalcActiveSkill.lua):**

```
baseLife   = lifeTable[minion.level]   × minionData.life
baseDamage = damageTable[minion.level] × minionData.damage
PhysicalMin = damage × (1 − damageSpread)
PhysicalMax = damage × (1 + damageSpread)
armour     = monsterArmourTable[minion.level]  × (minionData.armour or 1)
evasion    = monsterEvasionTable[minion.level] × (minionData.evasion or 1)
```

**Minion level has three possible sources, chosen per skill by a data flag:**

| Flag on the skill | Minion level is |
|---|---|
| `minionLevelIsEnemyLevel` | the **area / enemy** level |
| `minionLevelIsPlayerLevel` | `min(character level, a per-skill cap)` |
| (default) | the **gem's level requirement** — i.e. the *gem* level, raised by `+N to Level of Minion Gems` on gear |

then clamped: `minion.level = min(max(minion.level, 1), 100)`.

**Allied minions and hostile monsters read different tables**, and the gap is enormous
(values from `src/Data/Misc.lua`):

| Level | Hostile `monsterLifeTable` | Allied `monsterAllyLifeTable` | Ally as % of hostile |
|---:|---:|---:|---:|
| 1 | 22 | 15 | 68% |
| 20 | 207 | 75 | 36% |
| 50 | 1,927 | 493 | 26% |
| 84 | 16,265 | 3,074 | 19% |
| **100** | **41,817** | **6,916** | **16.5%** |

| Level | Hostile `monsterDamageTable` | Allied `monsterAllyDamageTable` | Ally as % |
|---:|---:|---:|---:|
| 1 | 4.99 | 5.62 | 113% |
| 50 | 146.53 | 111.62 | 76% |
| **100** | **1,758.17** | **1,600.09** | **91%** |

*(all computed from the shipped tables)*

**Allied life growth over 100 levels is 461×; allied damage growth is 285× (computed).** Hostile life
growth is 1,901×. The design statement inside that data: *a player's minion is expected to die,* and the
compensation is that its damage is nearly at parity with a same-level monster.

**Spread across the 64 shipped minion rows (computed):** life multiplier ranges 0.90
(`AncestralAhuanaMinion`) to 12.25 (`SummonedSpectralTiger`), median 2.21; damage multiplier ranges 0.0
(`AnimatedArmour` — a pure body-blocker) to 26.25, median 1.30. Zombie 3.75/1.65, Skeleton 1.05/2.45,
Raging Spirit 2.16/1.02, Phantasm 1.58/1.10.

**The roster cap is itself a stat.** `limit = "ActiveZombieLimit"` is a *named modifier*, and the count
is resolved as `calcLib.val(skillModList, minionData.limit) × More("ActiveMinionLimit")` — so gear and
passives raise it multiplicatively. Named limits in use: `ActiveGolemLimit` (6 minions reference it),
`ActiveSkeletonLimit` (5), `ActiveBeastMinionLimit` (3), `ActiveAnimatedWeaponLimit` (2), and 8 more
with 1 each (computed).

**INFERENCE — the two models compared.** Diablo II ties minion power to *one skill's rank*, so a
summoner's build is a spending decision inside the skill tree and gear only helps via `+skills`. Path of
Exile ties it to a *level index* plus a *modifier database the minion shares with the player*, so a
summoner's build is a gearing problem. The PoE model is strictly more expressive and strictly more
expensive: every minion needs its own actor with its own modifier database, resistances, accuracy and
attack timing.

### 4.3 Cassette Beasts — the summoner *is* the statline

*"The player receives experience points instead of the monsters, meaning that player characters provide
base stats that monsters then add to, providing freedom to try out different setups"*
([Wikipedia](https://en.wikipedia.org/wiki/Cassette_Beasts)). The creature contributes a *delta*; the
summoner contributes the base. This is the only shipped example in this survey where **switching
creatures costs the player nothing in levelling**, and it is a direct consequence of that choice.

### 4.4 Diablo III, Grim Dawn, Last Epoch

I could not source these to the standard of the two above and am recording what I can stand behind, with
the gaps named in *What I could not find*.

- **Diablo III** pets are widely described as scaling by a *percentage of the player's weapon damage*
  (tooltips of the form "attacks for N% of your weapon damage") and a percentage of player Life — a
  **percentage-inheritance** model, structurally the opposite of Diablo II's. **I could not retrieve a
  first-party page or a datamined table**: Blizzard's skill pages are now client-rendered and the Wayback
  Machine rate-limited the request.
- **Grim Dawn** carries a distinct **"pet bonus" stat family** on gear ("to all pets"), which is the
  mechanical statement that the player's own offensive stats do *not* transfer and a parallel stat set
  exists instead. **I could not retrieve a numeric table**; the community database is client-rendered.
- **Last Epoch** — no source retrieved.

**INFERENCE, and flagged as such:** the existence of Grim Dawn's separate "pet bonus" affix family is
itself evidence for the general rule that **a summoner build needs its own stat namespace**, because
otherwise every offensive affix in the game either double-dips or does nothing. Path of Exile solves the
same problem with a `Minion` tag on modifiers; Diablo II solves it by not letting gear touch minions at
all.

### 4.5 The four models, side by side

| Model | Minion power derives from | Shipped in | Consequence |
|---|---|---|---|
| **Flat skill table** | One skill's rank, plus difficulty tier | Diablo II | Gear only matters via `+skills`; minion power is a spend decision |
| **Level-indexed curve** | A monster-level table indexed by gem/player/area level, × a per-minion multiplier | Path of Exile | Needs a full second actor per minion; gear scales via a tagged modifier namespace |
| **Percentage-of-owner** | A stated % of the player's weapon damage / life | Diablo III (reported), Grim Dawn pet bonuses (partial) | Minion power tracks player gear automatically; hard to make a minion *feel* like its own creature |
| **Owner-as-base, creature-as-delta** | Player level sets base stats; creature adds | Cassette Beasts | Swapping creatures is free; creature identity is entirely in the delta and the kit |
| **Corpse-inheritance** | The revived monster's own stats × a multiplier | Diablo II Revive (+200% HP) | Power varies by what you're farming — the only model where *content* sets minion strength |

---

## 5. Gacha creature RPGs with deep upgrade layers

[`03-roster-scale.md` §3](../game-design/03-roster-scale.md) already establishes what *rarity* buys in
seven of these games (breadth and ceiling, never power) and §4 establishes that the magnitude is capped
and the effect vocabulary creeps. **Not repeated here.** What follows is the orthogonal question: how
many *upgrade axes* each game runs, and what each one actually buys.

| Game | Axes | What each axis buys |
|---|---:|---|
| **Summoners War** | **~6** | Level → stats · Star awakening (to 6★) → stat ceiling · **Awakening** → a new skill + name + stat bumps · **Skill-ups** (feed duplicates) → per-skill cooldown/multiplier · **6 runes** → the entire build · Rune upgrade to +15 + gems/grinds → substat quality · (later) artifacts |
| **Arknights** | **5** | Level · **Elite promotion** E0/E1/E2 → new skill, stat ceiling, sometimes a new talent · **Skill mastery** M1–M3 (4★+ only) · **Modules** (4★+ only) → stat + trait change · Trust → small stat bonus |
| **Fate/Grand Order** | **5** | Level · **Ascension** ×4 → level cap + art · **Skill levels** 1–10 × 3 skills · **NP level 1–5** — raised *only* by fusing duplicates · **Append skills** ×5 |
| **Genshin Impact** | **6** | Level · **Ascension** ×6 → cap + a fixed ascension stat · **Talent levels** ×3 · **Constellations** C0–C6 (duplicates) · **Weapon** (level + refinement) · **5 artifacts** with rolled substats |
| **Honkai: Star Rail** | **6** | Level · Ascension · **Traces** (skills + a 18-node bonus tree) · **Eidolons** E0–E6 · **Light Cone** (level + superimposition) · **Relics** with rolled substats |
| **Epic Seven** | **~6** | Level · **Awakening** ★→6★ · **Skill enhancement** · **6 gear pieces** · **Artifact** · **Exclusive Equipment / Imprint** |

*(axis counts are my tally over the systems each game ships — computed. Arknights and FGO per-rarity
details are sourced in [`03-roster-scale.md` §3](../game-design/03-roster-scale.md).)*

### The rolled-affix model, exactly

Genshin's artifact system is the best-documented instance of "rolled affixes on gear", and the data is
in the shipped tables. From
[`ReliquaryAffixExcelConfigData.json`](https://github.com/DimbreathBot/AnimeGameData/blob/master/ExcelBinOutput/ReliquaryAffixExcelConfigData.json),
5★ artifacts (depot 501) have **10 possible substats, and every substat has exactly 4 roll values**:

| Substat | Roll tiers (5★) | Ratio to max |
|---|---|---|
| CRIT DMG | 5.44% / 6.22% / 6.99% / **7.77%** | 0.700 / 0.800 / 0.900 / 1.000 |
| CRIT Rate | 2.72% / 3.11% / 3.50% / **3.89%** | 0.700 / 0.800 / 0.900 / 1.000 |
| ATK% | 4.08% / 4.66% / 5.25% / **5.83%** | 0.700 / 0.800 / 0.900 / 1.000 |
| Elemental Mastery | 16.32 / 18.65 / 20.98 / **23.31** | 0.700 / 0.800 / 0.900 / 1.000 |
| Energy Recharge | 4.53% / 5.18% / 5.83% / **6.48%** | 0.700 / 0.800 / 0.900 / 1.000 |

**Every roll is `max × {0.7, 0.8, 0.9, 1.0}` (computed).** That is the entire variance model — one max
value per (rarity, stat), four fixed multipliers, uniform draw. Rarity scales the max, not the tiers:
4★ CRIT DMG max is 6.22% and 3★ is 4.66%, i.e. **80% and 60% of the 5★ max (computed)**.

**INFERENCE.** This is the minimum viable rolled-affix design: one table of maxima, one shared tier
vector. Every "godroll" story a player tells is generated by 4 substats chosen from 10 and 5 upgrade
rolls each landing on one of 4 tiers — a combinatorial space large enough to sustain years of farming
built from **41 authored numbers per rarity (computed: 10 stats × 4 tiers, plus the pool)**.

### How many axes before players disengage

I found no first-party statement on this and am not going to invent one. What the data *does* support:

- **Every game in the table runs 5 or 6 axes.** None runs 3; none runs 9. That is a narrow band across
  six independently designed products.
- **The axes are not equal.** In Genshin the artifact axis is the only one with unbounded variance;
  everything else is a deterministic ladder. In FGO **NP level is the only axis that is duplicate-gated**
  and it is explicitly the primary scalar on the game's biggest damage number
  ([`03-roster-scale.md` §3](../game-design/03-roster-scale.md)).
- **The industry response to axis fatigue is retroactive rewrites, not fewer axes** — all three
  HoYoverse titles shipped a rewrite programme in 2025
  ([`03-roster-scale.md` §6](../game-design/03-roster-scale.md)).

**INFERENCE, clearly marked:** the constraint is probably not the axis count but how many of them are
*per-creature*. Genshin's six axes are per-character; Summoners War's rune axis is per-*unit-instance*
with six sockets each, and Summoners War is the one with a documented storage problem (§7).

---

## 6. Loyalty, contracts, and creature agency

Systems where the summoned thing has a will of its own. This is the rarest category in the survey, and
the evidence for *why* is unusually good.

### 6.1 World of Warcraft hunter pets — a loyalty system that shipped and was deleted

The most complete case study available, with the numbers and the patch notes that killed it
([Warcraft Wiki: Happiness](https://warcraft.wiki.gg/wiki/Happiness),
[Loyalty](https://warcraft.wiki.gg/wiki/Loyalty)).

**Happiness** — 3 tiers, roughly **350 points each**:

| State | Pet damage |
|---|---:|
| Happy | **125%** |
| Content | 100% |
| Unhappy | **75%** |

**Decay and gain:** idle loss ≈ **50 happiness per 6 minutes**; **dying costs 350** (a full tier);
dismissing cost 50 (removed in 4.0.1); one feeding = 10 bites over 20 seconds at 8 / 17 / 35 happiness
per bite by food quality, i.e. **80 / 170 / 350 per feeding**. Feeding is interrupted by combat.

**Loyalty** — 6 ranks (Rebellious → Unruly → Submissive → Dependable → Faithful → Best Friend), each
requiring **5% of the hunter's XP-to-next-level** *and* a **real-time gate**: 0 / 30 / 45 / 60 / 90
minutes. Higher loyalty reduced food consumption and granted pet training points. At low loyalty the pet
could **leave you permanently**.

**The two patch notes that end the story:**

> **Patch 3.0.2 (2008-10-14):** *"The Loyalty property was removed from the game. Happiness now only
> influences damage done by the pet."*
>
> **Patch 4.1.0 (2011-04-26):** *"The Happiness/Pet Loyalty System has been removed. Hunters will no
> longer have to manage Happiness for their pets, and the previous damage bonus for pets being happy will
> now be baseline for all tamed pets."*

**INFERENCE, and it is the strongest conclusion in this file.** Read the second note carefully: the
entire system's *net* mechanical effect was a −25% to +25% damage band, and when Blizzard removed the
chores they granted everyone the **top** of the band. So the system had never been a bonus — it was a
tax on inattention, and the tax fell on exactly the situation where a player is least able to pay it
(the pet died, the fight is still going, and you cannot feed in combat). It survived seven years and
was removed in two stages: the punitive half first, the entire thing three years later.

### 6.2 Warlock demon control — friction removed one patch at a time

Subjugate Demon (formerly Enslave Demon), from
[Warcraft Wiki](https://warcraft.wiki.gg/wiki/Enslave_Demon):

- Takes a demon **up to (player level + 1)** for **10 minutes**.
- Releasing it flips it hostile: *"subjugating generates a large amount of aggro towards the caster —
  upon release it will likely attack its previous controller."*
- The agency features were removed progressively:
  - **1.1.0 (2004-11):** *added* an increasing break-free chance for repeat casts on the same target.
  - **3.1.0 (2009-04):** *"There is no longer a penalty for repeatedly enslaving the same Demon."*
  - **7.0.3 (2016-07):** *"No longer reduces the enslaved demon's haste."*
  - **2.2.0 (2007-09):** PvP duration cut from 10 minutes to **10 seconds**.

**Twelve years of patches, every one of them subtracting agency.** The remaining agency is entirely
positional: the demon is temporary, level-capped, and hostile the moment you let go.

### 6.3 Pokémon obedience — a hard gate on borrowed creatures, with the exact formula

From the [pokeemerald disassembly, `IsMonDisobedient()`](https://github.com/pret/pokeemerald/blob/master/src/battle_util.c):

**Who it applies to.** Only a **traded** Pokémon (`IsOtherTrainer(otId, otName)`). Never to the opponent,
never in link battles, never in the Battle Frontier, never after the **8th badge**.

**The gate value:**

| Badges | `obedienceLevel` |
|---:|---:|
| 0–1 | 10 |
| 2–3 | 30 |
| 4–5 | 50 |
| 6–7 | 70 |
| **8** | always obedient |

**The check:**

```c
if (level <= obedienceLevel) return OBEDIENT;
rnd  = Random() & 255;
calc = (level + obedienceLevel) * rnd >> 8;
if (calc < obedienceLevel) return OBEDIENT;   // else: disobey
```

which is `P(obey) ≈ obedienceLevel / (level + obedienceLevel)`. A **level-100 traded Pokémon with no
badges obeys 10/110 ≈ 9.1% of turns (computed)**. A second identical roll then selects the failure
flavour: use a random other move, fall asleep (chance `(level − obedienceLevel) / 256`), hit itself with
a 40-power typeless attack, or **loaf around and do nothing**.

**What it solves:** it makes an over-levelled traded creature unusable, which is the only thing standing
between the trading feature and trivialising the campaign. **What it costs:** one function.
**What breaks when tuned wrong:** it is already close to broken at the edges — 9% obedience is not a
soft discouragement, it is a brick wall, and the player is given no meter, no progress bar and no way to
improve it except badges.

### 6.4 Pokémon friendship — the same idea, inverted into a reward

Friendship (0–255) is the positive counterpart: it drives evolution gates, move power (Return /
Frustration), and small flavour effects. It has no obedience component at all. **INFERENCE:** the same
franchise ships both a punitive agency stat (obedience, invisible, gated on badges) and a rewarding one
(friendship, visible via NPCs, gated on care), and only the rewarding one survived into the modern games
as a system players engage with.

### 6.5 Digimon Camaraderie, and SMT/Persona relationship gates

- **Cyber Sleuth's CAM (Camaraderie)** is a hard requirement in a digivolution gate — WarGreymon needs
  **CAM 80%** alongside its five stat requirements
  ([Grindosaur](https://www.grindosaur.com/en/games/digimon-story-cyber-sleuth/digimon/wargreymon)).
  Notably CAM is not a *behaviour* stat — a low-CAM Digimon does not disobey; it simply cannot evolve.
  **The agency is expressed as an unlock, not as unreliability.**
- **Persona 5's 21 Confidant-gated Personas** (§1.8) do the same thing at the acquisition step: a
  relationship rank is the key, and the creature behaves identically once you have it.
- **SMT negotiation** (§1.8) is the only one in the family where the creature can refuse in the moment,
  and even there the demon's demands are transactional (items, money, HP) rather than persistent.

### 6.6 The Nemesis System — agency as the *enemy's* property, and it is patented

Shadow of Mordor's Nemesis System is the deepest creature-agency system anyone shipped, and it runs on
the antagonists rather than the pets — until branding converts them into followers.

From [Wikipedia](https://en.wikipedia.org/wiki/Middle-earth:_Shadow_of_Mordor), on the design:

- The design goal was commercial: *"their goal was to make a gameplay element that would lead players to
  keep the game disc within their library rather than seek second-hand sales."*
- **It was deliberately scaled back:** *"It was made more complex during the game's early development,
  incorporating personal relationships among Orcs, but was later pared down when the studio considered it
  too complicated."*
- It was cut down again for last-gen ports: *"the Nemesis system was too large for older consoles."*
- The online half (Vendetta missions) was **shut off on 2021-01-12** when the servers closed.

The patent, [US10926179B2](https://patents.google.com/patent/US10926179B2/en), *"Nemesis characters,
nemesis forts, social vendettas and followers in computer games"*, assigned to Warner Bros
Entertainment, granted **2021-02-23**, claims: NPCs advancing through a ranked hierarchy
(soldiers → captains → warchiefs → overlord), memory of past player interactions surfaced in dialogue,
factional ripple effects when a member dies or is promoted, procedurally reconfigured strongholds, and
cross-player vendettas.

### 6.7 So what does a loyalty system add, and why do so few ship one?

**What it adds, from the evidence:**

| Mechanism | What the player gets |
|---|---|
| **Acquisition gate** (Persona Confidants, Digimon CAM) | The best creature in the game is a *relationship* outcome, so long-horizon investment has a creature at the end of it |
| **Behavioural unreliability** (Pokémon obedience, warlock break-free) | A hard ceiling on borrowed or over-levelled power that no amount of gear circumvents |
| **Maintenance band** (WoW happiness) | Nothing durable — see below |
| **Persistent memory** (Nemesis) | Stories the player tells other people, which is the only mechanic in this file whose stated purpose was retention |

**Why so few ship one — four reasons the evidence actually supports:**

1. **The maintenance-band version is a tax with no upside, and its own designers said so by deleting it
   and granting everyone the maximum** (WoW 4.1.0, quoted above).
2. **Unreliability is un-fun in the moment it triggers.** A disobeying Pokémon costs the player the turn
   they most needed. That is why the same games' *positive* agency stat (friendship) survived and the
   punitive one (obedience) is scoped to traded creatures only.
3. **It is expensive.** The Nemesis System was cut down mid-development for complexity, cut down again
   for platform reach, and had a whole online subsystem deleted at server sunset.
4. **The deepest version is literally patented until 2036.** US10926179B2 was granted 2021-02-23 and
   Wikipedia records that it has *"never [been] used in any other game."*

**INFERENCE.** The systems that survived are the ones that express agency as an **unlock or a
precondition** — checked once, at acquisition or evolution — rather than as **runtime unreliability** or
a **decaying meter**. Every decaying-meter version in this survey has been removed by its own studio.

---

## 7. Tribute, upkeep and maintenance costs on owned creatures

### 7.1 Warcraft III upkeep — the first-party statement

The clearest designer rationale I found anywhere in this research, from Blizzard's own strategy pages
([classic.battle.net/war3/basics/upkeep.shtml](http://classic.battle.net/war3/basics/upkeep.shtml)):

| Tier | Army food | **Gold income retained** |
|---|---|---:|
| No Upkeep | 0–50 | **100%** |
| Low Upkeep | 51–80 | **70%** |
| High Upkeep | 81–100 | **40%** |

And the rationale, verbatim:

> *"Upkeep has been included to improve tactical management for players while fostering a more aggressive
> style of gameplay. Players are more aggressive and turtle far less in their bases, and it gives players
> real strategic decisions to make about how many units they wish to control with pros and cons to each.
> Upkeep is also instituted to focus the game on smaller numbers of units. **The more units that are
> allowed in the game, the less powerful Heroes will be relative to your army.** This is simple math.
> **High Upkeep is MEANT to be very punishing.** Players should not be in it for long, but we didn't want
> to set the harsh unit cap at 80."*

Four separate design claims in one paragraph, and each one is directly relevant to a summoner game:

1. **Upkeep is a soft cap replacing a hard cap.** They explicitly *did not want* an 80-food wall; the
   tax is what they built instead.
2. **The tax is on income, not on the roster.** It costs nothing to *own* the army; it costs to own it
   *while earning*.
3. **The bracket is meant to be entered deliberately and briefly.** Blizzard's own advice is *"stay in
   Low Upkeep, and just jump to the High Upkeep tier before a major attack."*
4. **The whole thing exists to protect hero relevance.** More units = weaker heroes, stated as simple
   math.

### 7.2 Upkeep in creature games

| System | Ongoing cost | Shape |
|---|---|---|
| **Warcraft III** | Gold income tax, 3 brackets | **Soft cap on fielded army** |
| **Digimon Cyber Sleuth** | **Memory**, 2–25 per Digimon by stage (computed) | **Hard budget on fielded party**, no recurring drain |
| **Arknights** | Deployment Point cost, median 3 points across five rarity tiers vs **11 across classes** ([`03-roster-scale.md` §3](../game-design/03-roster-scale.md)) | **Per-battle budget**, priced by role not power |
| **SMT Compendium** | Re-summon price, superlinear in level (≈level^1.4 in SMT IV, ≈level^1.84 in SMT V, computed) | **Per-retrieval sink**, not recurring |
| **Monster Rancher** | **Lifespan in weeks** — creatures age and die; training costs food and time | **Real decay**, the harshest model here |
| **WoW hunter pets (pre-4.1)** | Food, ~50 happiness lost per 6 idle minutes | **Recurring chore**, deleted |
| **Pokémon** | **None.** No upkeep at any point in the series | — |

**What breaks when upkeep is too high:** Warcraft III's own answer — High Upkeep at 40% income is
described by its designers as something *"players should not be in it for long"*, i.e. a state, not a
mode. And Monster Rancher's lifespan is the extreme: a creature you invested months in **dies on a
timer**, which is the single most-cited friction point in that series.

**What breaks when upkeep is too low or absent:** Pokémon has zero upkeep and 3,000+ storage slots
(§8), and the result is the documented dead tail —
[`03-roster-scale.md` §5](../game-design/03-roster-scale.md) records **177 species, 36% of everything
tiered, sitting in the bottom competitive tier**, and 18 species accounting for 50% of team slots across
654,262 ladder battles. **INFERENCE:** with no cost to holding a creature there is no pressure ever to
revisit one, so the roster stratifies permanently and never re-mixes.

**The Nemesis System's "upkeep" is inverted and worth naming as a distinct pattern.** The rival you did
not kill **gets stronger while you are away** — *"being killed by a leader will cause the current mission
to be cancelled… and the leader will gain additional power, making him more difficult to defeat in the
next encounter"* ([Wikipedia](https://en.wikipedia.org/wiki/Middle-earth:_Shadow_of_Mordor)). The cost
of neglect is paid by the *world* growing, not by the player's roster shrinking. That is the only upkeep
model in this survey that creates content instead of removing it.

---

## 8. Roster size — own versus field

[`03-roster-scale.md` §1](../game-design/03-roster-scale.md) covers how many creatures a game
*designs*. This is how many a player *holds* and how many they *use*.

| Game | Can own | Can field at once | Active in one fight | Own : field |
|---|---:|---:|---:|---:|
| **Pokémon (Gen III)** | **420** (14 boxes × 30) + 6 in party | 6 | 1 (singles) / 2 (doubles) | **70 : 1** |
| **Pokémon Bank** | **3,000** | 6 | 1–2 | **500 : 1** |
| **SMT V: Vengeance** | **26** — 3 party + 23 stock, after all stock Miracles | 3 + protagonist | 4 | **~8.7 : 1** |
| **SMT V (start)** | 6 — 3 party + 3 stock | 3 + protagonist | 4 | 2 : 1 |
| **Digimon Cyber Sleuth** | 341 known; party limited by a **memory budget**, per-Digimon cost **2–25** | ~3 fielded, 11 in reserve | 3 | budget-driven |
| **Diablo II Necromancer** | n/a — resummoned from corpses | **8 skeletons + 8 mages + 20 revives + 1 golem** at max skill (from the published tables) | all simultaneously | — |
| **Path of Exile** | n/a | Per-skill named limits (`ActiveZombieLimit`, `ActiveGolemLimit`, …), **and the limit is a moddable stat** | all simultaneously | — |
| **Warcraft III** | Food cap 100, but **income drops to 40% above 80** | ~50 before any tax | all | — |
| **Cassette Beasts** | 120 species, unlimited tapes | party of monsters, **fusion is 2 → 1 temporarily** | 2 (+ fusion) | — |

*(Pokémon Gen III from `TOTAL_BOXES_COUNT 14` × `IN_BOX_COUNT 30` and `PARTY_SIZE 6` in the
[pokeemerald headers](https://github.com/pret/pokeemerald/blob/master/include/pokemon_storage_system.h);
Pokémon Bank's 3,000 from [Wikipedia](https://en.wikipedia.org/wiki/Pok%C3%A9mon_Bank);
SMT V's 26 from [GameFAQs SMT V: Vengeance Q&A](https://gamefaqs.gamespot.com/switch/450675-shin-megami-tensei-v/answers/624128-how-many-demons-can-you-have-at-once-and);
Digimon memory costs computed; D2 counts from the Blizzard skill tables in §4.1.)*

### Where the tension lives

**The interesting number is the ratio, and it splits the genre in two.**

- **Collection games run ratios of 70:1 to 500:1.** The roster is an *archive*; fielding is a selection
  problem, and the archive is where the collecting satisfaction lives. Pokémon never charges you for the
  archive.
- **Summoner games run ratios near 1:1 to 9:1.** SMT V ends at **26 owned / 3 fielded**, and — the key
  detail — **the stock size itself is an unlockable**, starting at 3 and growing through Miracles. The
  roster cap is progression content.
- **ARPG summoners have no archive at all.** The minion count *is* the roster, it is set by a skill
  level or a moddable stat, and every unit is on the field simultaneously.

**INFERENCE, marked.** The two designs answer different questions. A 500:1 archive makes *acquisition*
the loop and *deck-building* the skill. A 9:1 roster with a growing cap makes *the cap* the loop — every
increase is felt immediately, because 3 → 4 stock slots is a 33% change and 420 → 421 box slots is
nothing. SMT is the only family here that turned roster size into a reward, and it did so precisely
because its roster is small enough for one slot to matter.

**And the pressure that keeps a small roster interesting is that the archive is lossy.** SMT's stock is
tiny *and* fusion consumes both parents *and* the Compendium charges a superlinear price to get one
back. Those three facts are one design: the roster is small, everything in it is spendable, and undoing
a spend costs money that scales.

---

## What I could not find

An honest list. Every item here was searched for and not obtained to the standard used elsewhere in this
file.

1. **Diablo III pet scaling — no first-party or datamined source.** Blizzard's skill pages
   (`us.diablo3.blizzard.com`, `diablo3.blizzard.com`) are now entirely client-rendered and return no
   skill text; the Wayback Machine rate-limited every attempt; `diablo.fandom.com` returns HTTP 402.
   The "% of your weapon damage" model is stated from memory in §4.4 and is **explicitly unsourced**.
2. **Grim Dawn pet formulas.** The community database (grimtools.com) is a client-rendered
   single-page app and returns only navigation chrome; Crate's own forum URLs 404'd; the Steam guide
   returned an error page. The *existence* of a separate "pet bonus" affix family is asserted from
   general knowledge and is **not sourced here**.
3. **Last Epoch minion scaling — nothing retrieved at all.** Official site paths 404'd; the community
   guide site 404'd.
4. **Path of Exile's `ActiveZombieLimit` base value and its per-gem-level progression.** The mechanism
   (a moddable named stat) is sourced; the *numbers* live in the skill gem data, not `Minions.lua`, and
   I did not retrieve them.
5. **A datamined SMT negotiation success formula for any modern entry.** Guides describe the inputs
   (Luck, moon phase, demon mood, alignment, level difference) but no source gave a probability
   expression. Guide prose only.
6. **The Persona 5 skill-inheritance threshold table has an off-by-one I could not resolve.** The
   published table reads "13–23 → 4" and "23–31 → 5"; 23 appears in both bands. Not resolvable against
   game data with the tools available.
7. **SMT fusion accident rates conflict between sources** (1/256 vs 1/64 base for Nocturne). Both are
   reported in §1.6; neither is a datamine.
8. **Digimon Cyber Sleuth's total party memory cap.** Per-Digimon costs are computed from the shipped
   table; the *budget* they are spent against was not sourced.
9. **Coromon fusion mechanics.** No source retrieved (the community wiki returns HTTP 403).
10. **Monster Rancher 2 combining stat formulas in numeric form.** The *shape* is sourced (first parent
    dominant, 2/3 truncated technique transfer, compatibility by stat ordering); the actual percentage
    tables were behind pages I could not retrieve.
11. **Any first-party statement on how many upgrade axes is too many.** Consistent with
    [`03-roster-scale.md`](../game-design/03-roster-scale.md)'s finding that studios almost never publish
    roster or progression design rationale. The Warcraft III upkeep page in §7.1 is the only first-party
    design rationale of this kind found in the entire pass.
12. **Web search budget was exhausted at 200 calls partway through this pass.** Sections 5–8 were built
    from direct fetches against URLs derived from earlier results, decompilations, and datamine
    repositories rather than fresh search. Coverage of gacha upgrade axes (§5) is consequently thinner
    than the other sections and leans on
    [`03-roster-scale.md`](../game-design/03-roster-scale.md) for the per-game rarity detail.

---

## Hooks for this project

**Non-normative, un-vetted, not a design proposal.** These are observations about where this research
touches systems this repo already has. Nothing here has been checked against the code, the design gate,
or `decisions.md`, and none of it is a recommendation.

- **The `(race, level)` primary key.** SMT's fusion rule is only a function because no two demons share
  a race and a level — five games, zero collisions. Any generated roster that wants a computed fusion
  rule inherits that constraint, and a *generator* can violate it silently in a way a hand-authored
  roster cannot.
- **The table-picks-family / rule-picks-individual split** recurs independently in SMT, Pokémon and
  Monster Rancher, and in all three the authored table takes precedence over the computed rule.
- **The `+` value as a visible fusion counter** (Dragon Quest Monsters) is the cheapest way found to make
  a long fusion chain legible, and it is a *cap key* rather than a stat.
- **Monster Rancher's generator is the closest shipped analogue to a seed-to-concrete pipeline**: a
  hard-coded special table checked first, then a small arithmetic index into a breed list, then two
  additive offset rows summed onto the breed's base. Five integers in, one deterministic creature out.
- **Path of Exile ships two level tables** — one for allied minions and one for hostile monsters, with
  the allied life curve at 16.5% of hostile at level 100 and damage at 91%. That is a shipped answer to
  "what should a summoned unit's numbers be relative to an enemy of the same level."
- **The minion cap as a moddable stat** (`ActiveZombieLimit`) rather than a constant — relevant to this
  repo's standing rule that a cap on a magnitude is a soft cap or nothing.
- **The Compendium price curve is superlinear in level** (≈level^1.4 to level^1.84, computed), which is
  the same family of shape as this project's own quadratic ladder.
- **Genshin's rolled affix model costs 41 numbers per rarity** — 10 stats, 4 tiers each at
  `max × {0.7, 0.8, 0.9, 1.0}`, plus the pool — and generates years of farming variance.
- **Persona gates 21 of 210 Personas behind a maxed relationship**, i.e. relationship-as-acquisition-key
  rather than relationship-as-runtime-behaviour. Every runtime-behaviour loyalty system found in this
  pass was later deleted by its own studio.
- **Warcraft III's upkeep is a three-bracket income tax that replaces a hard unit cap**, with a
  first-party statement that the hard cap was deliberately rejected.
- **Cassette Beasts' warning:** 120 authored creature designs reviewed well; the 14,000 procedurally
  fused ones drew the review's only design criticism.
