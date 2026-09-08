# Tower defense outside PvZ — the genre's mechanics vocabulary, and what transfers to a lane

Captured 2026-09-02. Research only. Nothing here is a proposal.

## The finding in one paragraph

**Tower defense has almost no shared damage math and a very deep shared structure.** Every surveyed
game solves the same four problems — how a tower gets better, where it may stand, what an enemy
carries, and how the difficulty keeps rising forever — and they solve the first two the same way and
the last two completely differently. The convergent part: **placement is always scarce and upgrades
are always a forced exclusive choice.** BTD6 authors 15 upgrades per tower and gets 64 legal build
states out of them (computed) by forbidding the third path; Kingdom Rush authors 3 linear levels plus
a terminal fork and gets 2 end states; Arknights caps a stage at **8 simultaneously deployed
Operators on a map with 39 buildable tiles** (computed from the shipped level file for 1-7) and
regenerates the deploy currency at exactly **1 DP per second** in every main-story stage sampled. The
divergent part: counter systems are **keyed immunities, not matrices.** BTD6 ships 11 damage types
whose only interaction with an enemy is a yes/no column per bloon property (`Sharp` = "not Lead, not
Frozen"); Arknights ships flat DEF subtraction for Physical, percentage RES for Arts, a 5%-of-ATK
damage floor for both, and no attacker-vs-defender table at all; Kingdom Rush ships a single
percentage-reduction number per enemy with an explicit `True Damage` escape hatch. **The one game
that does ship a real elemental cycle — Element TD — uses it to buy combinatorics, not counterplay:
6 authored elements become 41 tower identities.** And the endless problem is solved with a piecewise
linear ramp, not an exponential: BTD6's freeplay HP scaling is **eight linear brackets** whose
per-round increment goes 2% → 5% → 15% → 35% → 100% → 150% → 250% → 500%, with a closed form past
round 501 of `×(5N − 2008.5)`.

---

## Sourcing note

| Tier | What | Used for |
|---|---|---|
| **First tier — shipped data** | `Btd6ModHelper/btd6-game-data` (BTD6 game models exported from the live build), `Kengxxiao/ArknightsGameData_YoStar` (Arknights `character_table`, `enemy_database`, level files) | Every BTD6 and Arknights number below unless marked otherwise |
| **First tier — developer statements** | Ironhide support articles, the Legion TD 2 official manual, the Infinitode 2 dev blog, the Element TD author's own map description, Steam store copy written by the developer | Kingdom Rush armour categories, Legion TD 2 economy, Element TD combination rules |
| **Second tier — wiki prose** | Bloons Wiki (Fandom), Kingdom Rush Wiki (Fandom), Arknights Terra Wiki (wiki.gg), GemCraft Wiki | Marked **(wiki)** inline. Used where no datamine exposes the value — chiefly the BTD6 freeplay ramp, which lives in simulation code rather than an exported model |
| **(computed)** | Arithmetic I did over the above | Marked inline every time |

Two existing repo documents already cover ground this one deliberately does not repeat:
[docs/research/game-design/01-typing-matrices.md](../game-design/01-typing-matrices.md) (Pokémon,
StarCraft, WC3, AoE, CoH, DoW, Total War, Genshin, D:OS2 typing systems) and
[docs/research/game-design/02-unit-variables.md](../game-design/02-unit-variables.md) (the RTS/RPG
unit field checklist, including Arknights' 26-field per-phase stat block and its 8-class / 72-branch
taxonomy). Where a fact is already there, this file cites it and moves on.

---

## 1. Upgrade topologies

### 1.1 The three shipped shapes

| Shape | Example | Authored nodes per tower | Distinct end states | Distinct build states |
|---|---|---:|---:|---:|
| Linear tiers + terminal fork | Kingdom Rush 1 / Frontiers / Origins | 3 levels + 2 terminals = 5 | **2** | 5 |
| Two paths, tier-3 lockout | Bloons TD 5 | 2 × 4 = 8 | 2 | 21 (computed) |
| **Three paths, one-past-tier-2 rule** | **Bloons TD 6** | **3 × 5 = 15** | **15** (computed) | **64** (computed, verified) |
| Per-entity XP tree with exclusive pairs | Kingdom Rush 6: Genesis | 11 per tower | branch-dependent | branch-dependent |
| Level + ability research, no branches | Infinitode 2 | L1–L20+ per tower plus researched abilities | — | continuous |

**BTD6's crosspathing rule, stated exactly.** The Bloons Wiki gives it in one sentence: *"in Bloons TD
6, there are three upgrade paths, only 2 of which are selectable per tower. And only one of these
paths may be upgraded to tier-3 and above."* One of the three coordinates is therefore **always
zero**, and notations like `3-0-3` or `4-1-1` are illegal.
([wiki](https://bloons.fandom.com/wiki/Crosspathing))

**This is inherited, not invented.** BTD5 had 2 paths of 4 tiers with the same "not past tier 3 on
both" rule, so `3/3` did not exist. BTD6 kept the exclusion and added a third path — which is what
multiplies the state count. (Same source.)

**64 legal states per tower (computed, and verified against the shipped data.)** Enumerating all
`(a,b,c)` in `0..5³` subject to "at most two non-zero" and "at most one above 2" yields **64** tuples
including `0-0-0`, of which **15** contain a 5. The exported game data agrees exactly: the
`DartMonkey` directory holds **63 crosspath state files** (`DartMonkey-001.json` … `-520.json`) plus
`DartMonkey.json` (the base) plus `DartMonkey-Paragon.json`.
([data](https://github.com/Btd6ModHelper/btd6-game-data))

**The 15 tier-5 end states decompose cleanly:** 3 paths × 5 crosspath options each — for a path at
tier 5, the two others can only be `(0,0)`, `(1,0)`, `(2,0)`, `(0,1)`, `(0,2)` (computed).

**Scale of the whole system (computed from the exported tree):** 26 towers carry a full three-path
tree, so **26 × 15 = 390 authored tower upgrades produce 26 × 64 = 1,664 distinct tower build
states — a 4.3× expansion for free.** The exported `Upgrades/` folder holds 790 records in total, of
which 209 are hero level-ups.

### 1.2 Paragons — a fourth axis bolted on top

BTD6's Paragon is not a tier 6. It is a **conversion** that consumes towers and produces one tower
whose power is a scored function of what was spent. The scoring model ships as a single file,
`paragonDegreeData.json`, and it is unusually legible:

| Input | Rate | Cap |
|---|---|---:|
| Extra tier-5 towers beyond the 3 required | `tier5TowersMultByX` = **6,000** each | `maxPowerFromTier5Count` = **50,000** |
| Non-tier-5 upgrade tiers | `nonTier5TowersMultByX` = **100** each | `maxPowerFromNonTier5Count` = **10,000** |
| Money spent | scaled by `moneySpentOverX` = **20,000** | `maxPowerFromMoneySpent` = **60,000** |
| Pops | scaled by `popsOverX` = **180** | `maxPowerFromPops` = **90,000** |
| **Total** | — | `MaxInvestment` = **200,000** |

`degreeCount` is **100**, and `powerDegreeRequirements` is a shipped 100-entry threshold array:
degree 1 at 0 power, degree 2 at **2,000**, degree 3 at 2,324, … degree 99 at 192,120, degree 100 at
**200,000**. The gaps grow smoothly — 324 between degrees 2 and 3, 4,356 between 98 and 99 — so the
curve is convex but not steeply so. Per-degree payoff is also shipped:
`damageIncreasePerDegree` = 1.0, `pierceIncreasePerDegree` = 0.1,
`bonusBossDamagePercent` = 0.25 applied every `bonusBossDamagePerDegrees` = 20 degrees.
([data](https://github.com/Btd6ModHelper/btd6-game-data/blob/main/paragonDegreeData.json))

13 of the 26 towers currently have a Paragon (computed from the exported tree:
Dart, Boomerang, Bomb, Tack, Ice, Ninja, Wizard, Druid, Sub, Buccaneer, Ace, Spike Factory,
Engineer).

**Two design properties worth naming.** First, **every input is separately capped**, so no single
resource can carry a Paragon to 100 — the design forces breadth. Second, **degree is a scalar, not a
build**: a degree-84 Paragon and a degree-84 Paragon from totally different sacrifices are the same
tower. Paragons trade the whole decision space of crosspathing for a single number.

### 1.3 Kingdom Rush — the cheapest topology that still decides something

Ironhide's shape has not changed in fourteen years: *"Each tower type can be improved three times
before the player must choose between two final upgrades with their own distinct abilities."*
([Wikipedia](https://en.wikipedia.org/wiki/Kingdom_Rush)) Four base tower types, so **8 terminal
towers** in the base game, and the terminal choice is the only in-match branch that exists.

Its evolution is the more interesting data point. **Kingdom Rush 6: Genesis moved the branching out
of the level and into the meta-tree, and made the meta-tree per-entity and XP-driven:** every tower,
hero and spell has its own tree with its own upgrade points, earned by *using* that entity; trees hold
**11 upgrades (towers/heroes) or 8 (spells)**; each tower tree contains **two mutually-exclusive
pairs** and each hero tree **one**; and a tower's special abilities must be unlocked in the tree
before gold can buy them in a stage. ([wiki](https://kingdomrushtd.fandom.com/wiki/Upgrades))

### 1.4 Which topology produces the most decision per authored node

| | Kingdom Rush (KR1) | BTD6 |
|---|---:|---:|
| Authored nodes per tower | 5 | 15 |
| Legal build states | 5 | **64** |
| States per authored node (computed) | 1.0 | **4.27** |
| Choice is made… | once, at level 4 | continuously, and it is **reversible only by selling** |

**BTD6 wins on decision density by a factor of about four, and pays for it in balance surface, not in
content.** Every one of its 64 states needs to be *tuned*, because a crosspath that is strictly worse
is a dead state — which is exactly why the community maintains "best crosspath for every tier 5"
guides. Kingdom Rush's 5 states are all live by construction.

**Problem / cost / failure mode, per topology:**

| Topology | Problem it solves for the player | What it costs the designer | What breaks when tuned wrong |
|---|---|---|---|
| Linear tiers | "Is this tower worth more gold right now?" — a pure economy question, no build knowledge needed | Cheapest possible: one number per level | Nothing branches, so the whole tower collapses to a gold-efficiency curve and the level plays itself |
| Terminal fork | One memorable, irreversible identity choice per tower | Two full ability kits per tower, both of which must be viable | If one terminal is better on most maps, the other is content that nobody ever sees |
| **Three-path + lockout** | A build language: the same tower is 64 different things, and the player expresses a plan through it | 15 authored upgrades **plus** the interaction tuning of 64 states, forever | Dominant crosspaths. A path whose tier 1–2 are strictly better than the others' collapses 64 states to ~15 |
| Paragon-style conversion | A sink for a runaway economy, and a visible reward for over-investment | A scoring model with per-input caps that must not be gameable | Uncapped inputs. Remove any one cap and the cheapest input becomes the only input |
| Per-entity XP tree | Progression that follows what the player actually plays | Bookkeeping per entity, and a first-hour problem: a new tower starts weak | The tree becomes a use-tax — players stop experimenting because switching towers resets their power |

---

## 2. Placement economy

### 2.1 The three shipped geometries

| Geometry | Example | What is scarce |
|---|---|---|
| **Fixed slots** | Kingdom Rush | The number of build pads on the map — an authored, per-level constant |
| **Free placement** | BTD6, GemCraft (on-path grid), Dungeon Defenders | Space near the path, plus a global budget (money, or Defense Units) |
| **Tile grid with role-typed tiles** | Arknights | Buildable tiles **and** a per-stage concurrent-deployment cap **and** the deploy currency |
| **Lane grid** | Plants vs Zombies, Legion TD | A cell per lane-column; the lane is also the unit of failure |

### 2.2 Arknights, with the numbers

Arknights is the most instructive because its whole difficulty curve is placement, and every constant
is in the shipped level file.

**DP (Deployment Point) economy — from `options` in the shipped level JSON.** Sampling 60 random
main-story stages out of the 367 shipped
([data](https://github.com/Kengxxiao/ArknightsGameData_YoStar/tree/main/en_US/gamedata/levels/obt/main)):

| Field | Value | Frequency in sample (computed) |
|---|---|---|
| `costIncreaseTime` | **1.0 s** | **60 / 60** — one DP per second, universally |
| `maxCost` | **99** | **60 / 60** |
| `characterLimit` | **8** | 52 / 60 (others: 7, 9, 10, 0) |
| `initialCost` | **10** | 44 / 60 (others: 0, 3, 5, 15, 18, 20) |
| `maxLifePoint` | **3** | 50 / 60 (others: 5, 10, 15) |

**The DP curve is the flattest possible: linear, one per second, capped at 99. Every bit of tension
comes from cost, not from income.**

**Deployment cost, from `character_table.json` at each Operator's final phase (computed over 374
obtainable Operators):**

| Rarity | n | Cost min | Cost max | Cost median |
|---|---:|---:|---:|---:|
| 1★ | 9 | 3 | 3 | 3 |
| 2★ | 5 | 7 | 24 | 12 |
| 3★ | 17 | 10 | 30 | 17 |
| 4★ | 59 | 7 | 34 | 18 |
| 5★ | 171 | 7 | 35 | 19 |
| 6★ | 113 | 8 | 36 | **21** |
| **All** | **374** | **3** | **36** | **19** |

**Rarity buys almost no cost discount — the 4★ and 6★ medians differ by 3 DP.** The expensive tail is
a *branch* property, not a rarity property: the three most expensive Operators (Penance 36,
Mudrock 36, Vulcan 35) are all the `unyield` Defender branch with **block 3**.

**Block count is the real class signature (computed, final phase, 374 Operators):**

| Block | Count | Share |
|---:|---:|---:|
| 0 | 6 | 1.6% |
| **1** | **248** | **66.3%** |
| 2 | 71 | 19.0% |
| 3 | 49 | 13.1% |

Per class, the block value is nearly deterministic: Casters, Medics and Snipers are **block 1 with no
exceptions**; Vanguards and Supporters are 1 or 2; Defenders are 1 or 3; Guards span 1–3; Specialists
span 0–2. Enemies that push past a blocker do so purely by count: *"If the amount of enemies on the
same tile as the friendly exceeds their block count, the additional enemies will move past the
friendly."* ([wiki](https://arknights.wiki.gg/wiki/Attribute/Block))

**Redeploy timers (computed — the whole shipped set is seven values):** `{18, 22, 25, 35, 70, 80,
200}` seconds. **70 s is the default.** The two fast-redeploy branches are exact: `executor`
Specialists at **18 s** (Gravel, Phantom, Projekt Red, Waai Fu, Kafka, Kirin R Yato, Texas the
Omertosa) and `merchant` Specialists at **25 s** (Jaye, Lee, Mr. Nothing, Figurino, Swire the Elegant
Wit); Crownslayer sits alone at 22 s; the 200 s tier is the 1★ robots. Splash Casters run **80 s**.

**On top of the timer sits a cost penalty:** an Operator's cost *"will be increased to 1.5× when they
are redeployed for the first time and 2× when redeployed for the second time onwards"*, never above
99; retreating refunds *"DP equal to half of their current cost when retreated, rounded down."*
([wiki](https://arknights.wiki.gg/wiki/Attribute/Cost))

**The scarcity, quantified (computed from the shipped map for stage 1-7).** The map is an **11 × 7
grid = 77 tiles**, composed of:

| Tile | Height | Buildable by | Count |
|---|---|---|---:|
| `tile_road` | LOWLAND | **MELEE** | **23** |
| `tile_wall` | HIGHLAND | **RANGED** | **16** |
| `tile_forbidden` | HIGHLAND | none | 30 |
| `tile_floor` / `tile_start` / `tile_end` / forbidden lowland | LOWLAND | none | 8 |

**39 of 77 tiles are buildable, and the stage lets 8 Operators stand on them at once.** The binding
constraint is not the board and it is not the money — it is the concurrent-deployment cap. That is
the difficulty dial, and it is a single integer in a config file.

### 2.3 Dungeon Defenders — the explicit second budget

Dungeon Defenders is the clearest statement of "money is not the placement constraint": *"Traps and
defenses are limited by the available mana that the character has … as well as a total 'defense
units' for the level, limiting the number of traps that can be placed."*
([Wikipedia](https://en.wikipedia.org/wiki/Dungeon_Defenders)) Two orthogonal budgets — a fungible
currency and a non-fungible per-level allowance — which is structurally the same pair as Arknights'
DP and `characterLimit`.

### 2.4 Why placement scarcity is the difficulty dial

**Because it is the only lever that scales difficulty without scaling numbers.** Every other dial
(enemy HP, enemy count, enemy speed) makes the same defence weaker; placement scarcity makes a
*different defence necessary*. The evidence is that games with a rich upgrade system still reach for
placement to make things hard:

- Kingdom Rush's **Heroic Challenge** is *"one life … six large waves"* with *"a set limit"* on
  available upgrade paths, and on the first nine KR1 levels the hero is removed entirely.
  ([wiki](https://kingdomrushtd.fandom.com/wiki/Heroic_Challenge))
- Kingdom Rush's **Iron Challenge** is *"one life … a long, grueling wave"* where *"certain towers
  are locked … most often it is two towers, but some stages are limited to only one"*, and the
  incoming enemy count is replaced by `??`.
  ([wiki](https://kingdomrushtd.fandom.com/wiki/Iron_Challenge))
- **Neither challenge raises a single enemy stat.** Both are pure constraint.

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | Makes the player choose *what not to build*, which is the only place a defence expresses a plan |
| **Designer cost** | Fixed slots are per-level authoring work forever. Free placement is nearly free to author and nearly impossible to balance, because the best spot on a map is a map property the tuner does not control |
| **Breaks when wrong** | Too loose and the answer is "build the best tower everywhere" — the upgrade system stops mattering. Too tight and the answer is a single memorised solution, which is the same failure with a worse mood |

---

## 3. Enemy attribute vocabulary

The RTS/RPG field checklist is already in
[02-unit-variables.md](../game-design/02-unit-variables.md), including the Arknights 26-field
per-phase block. What follows is the **TD-specific** field set and what actually gets used.

### 3.1 What a BTD6 bloon carries (shipped model, `BloonModel`)

Beyond display and audio, the scalar surface is small:

```
id, baseId, tags[], overlayClass, layerNumber
maxHealth, radius, speed, danger
bloonProperties (bitmask), propertyFlags, basicTypeFlags
isMoab, isBoss, isCamo, isGrow, isFortified, isBossSegment, isInvulnerable
isImmuneToSlow, distributeDamageToChildren, hasChildrenWithDifferentTotalHealths
leakDamage, leakDamageSet, leakDamageMultiplier, coopMultiplier, armourMultiplier
bonusDamagePerHit, isFixedMaxHealth
+ behaviors[]: SpawnChildrenModel{children[]}, DistributeCashModel, DamageStateModel[]
```

Real shipped values ([data](https://github.com/Btd6ModHelper/btd6-game-data/tree/main/Bloons)):

| Bloon | `maxHealth` | `speed` | `radius` | `danger` | `leakDamage` | `bloonProperties` | Children |
|---|---:|---:|---:|---:|---:|---:|---|
| Red | 1 | 25.0 | 8 | 1 | 1 | 0 | — |
| Lead | 1 | 25.0 | 8 | 7 | 1 | **1** | — |
| Black | 1 | 45.0 | 8 | 6 | 1 | **2** | — |
| White | 1 | 50.0 | 8 | 6 | 1 | **4** | — |
| Zebra | 1 | 45.0 | 8 | 7 | 1 | **6** (= 2+4) | — |
| Purple | 1 | **75.0** | 8 | 6 | 1 | **8** | — |
| Ceramic | 10 | 62.5 | 8 | 9 | 10 | 0 | 2 × Rainbow |
| Ceramic (Fortified) | **20** | 62.5 | 8 | 9.5 | 20 | 0 (`propertyFlags` 4) | 2 × Rainbow |
| MOAB | 200 | 25.0 | 21 | 10 | 200 | 0 | 4 × Ceramic |
| DDT (Camo) | 400 | **66.0** | 30 | 13 | 400 | **3** (= 1+2) | 4 × CeramicRegrowCamo |
| BAD | **20,000** | **4.5** | 40 | 15 | 20,000 | 0 | mixed |

**Three things stand out.** (1) `leakDamage` equals `maxHealth` on every line — Ninja Kiwi ties "how
much you lose" to "how big it is" by construction. (2) `speed` and `maxHealth` are **anti-correlated
at the top end** (Purple 75.0 with 1 HP; BAD 4.5 with 20,000) — the tank and the rusher are the same
axis inverted. (3) **Fortified is a flat ×2 on health with no other change** (Ceramic 10 → 20), which
makes it the cheapest possible difficulty modifier: one multiplier, one icon, zero new behaviour.

### 3.2 What an Arknights enemy carries — and which fields actually get filled

Arknights ships an explicit "defined / not defined" flag per attribute, so the vocabulary and its
*usage rate* are both measurable. Over the **1,552 enemy records** in the shipped database
(computed):

| Field | Records where defined | Share |
|---|---:|---:|
| `maxHp` | 1,532 | 98.7% |
| `moveSpeed` | 1,527 | 98.4% |
| `def` | 1,500 | 96.6% |
| `atk` | 1,499 | 96.6% |
| `baseAttackTime` | 1,492 | 96.1% |
| `massLevel` (weight) | 1,482 | 95.5% |
| `magicResistance` | 1,479 | 95.3% |
| `attackSpeed` | 1,394 | 89.8% |
| `silenceImmune` | 840 | 54.1% |
| `stunImmune` | 666 | 42.9% |
| `sleepImmune` | 476 | 30.7% |
| `hpRecoveryPerSec` (regeneration) | 473 | 30.5% |
| `levitateImmune` | 470 | 30.3% |
| `frozenImmune` | 468 | 30.2% |
| `tauntLevel` (aggro) | 394 | 25.4% |
| `epDamageResistance` / `epResistance` (elemental) | 232 / 226 | ~15% |
| `fearedImmune` / `disarmedCombatImmune` | 213 / 205 | ~13% |
| **`damageHitratePhysical` / `damageHitrateMagical` (dodge)** | **46 / 46** | **3.0%** |
| `attractImmune` | 39 | 2.5% |
| `palsyImmune` | 10 | 0.6% |
| `blockCnt` | **1** | 0.06% |

**Eleven of the twenty-five fields are status-immunity booleans, and they are the second-densest
group after the core stats.** A TD enemy's identity is substantially "which of your control tools
does not work on me."

**Dodge exists and is almost never used** — 46 of 1,552 records carry a hit-rate field. It is a
deliberate rarity, not an absence.

Structural fields alongside the stat block: `applyWay` (**MELEE** 744 / **RANGED** 472 / NONE 200 /
ALL 90), `motion` (**WALK** 1,317 / **FLY** 108), `levelType` (**ELITE** 634 / **BOSS** 199 / NORMAL
193 / unset 526), `rangeRadius`, `viewRadius`, `lifePointReduce`, `notCountInTotal`, `enemyTags`,
`skills[]` with per-skill cooldown and an untyped `blackboard` of key/value pairs.

**Leak damage is quantised and heavily defaulted (computed):** `lifePointReduce` is **1 on 1,091
records**, 0 on 144, 2 on 68, 5 on 41, **30 on 24** (the boss tier), 3 on 21, and exactly one record
each at 15 and 999. **A single enemy is worth one life, and bosses jump straight to thirty.** There
is no gradual middle.

**Enemy taxonomy is 11 tags, not a class enum (computed):** `sarkaz` 137, `animated` 96, `infection`
94, `seamonster` 70, `machine` 67, `drone` 45, `wildanimal` 36, `origen` 20, `originiumartscraft` 18,
`collapsal` 14, `mutant` 14. Sparse and flavour-led — this matches the RTS finding in
[02-unit-variables.md](../game-design/02-unit-variables.md) that nobody ships a closed combat-role
enum.

**Magnitude ceilings actually reached (computed):** `maxHp` up to **100,000,000**, `def` up to
**9,999**, `magicResistance` up to **1,000** — the last of which is far past the 0–100% band the
damage formula clamps to, i.e. the data deliberately stores an unreachable value as "immune".
**1,129 of the 1,479 enemies with a defined RES have RES > 0 (76.3%)** — magic resistance is the
common case, not the exception.

### 3.3 Consolidated: which games carry which enemy field

`Y` = present and sourced · `–` = absent and sourced as absent · blank = not established here.

| Field | BTD6 | Arknights | Kingdom Rush | Legion TD 2 | Element TD | Infinitode 2 |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| HP | Y | Y | Y | Y | Y | Y |
| Move speed | Y | Y | Y | | Y | Y |
| Flat armour value | – | **Y** (`def`) | – | | | |
| Percentage armour | – | – | **Y** | | | |
| Magic resist as a separate channel | – | **Y** (`RES`) | **Y** | | | |
| Armour **type** / matrix row | – | – | – | **Y** | **Y** | |
| Keyed immunity flags | **Y** (`bloonProperties`) | – | – | | | |
| Shields as a second pool | Boss-only | – | | | | **Y** (Icy) |
| Flying | Camo/DDT are air-adjacent | **Y** (`motion: FLY`) | Y | | | **Y** (Jets) |
| Cloaked / camo | **Y** (`isCamo`) | – | | | | |
| Regeneration | **Y** (`isGrow` / Regrow) | **Y** (`hpRecoveryPerSec`) | Y | | | |
| Spawn-on-death children | **Y** (`SpawnChildrenModel`) | Y (skills) | Y | | | |
| Boss phases | **Y** (`isBossSegment`, tiers 1–5 + Elite) | **Y** (`levelType: BOSS`) | Y | | | |
| Status immunities as fields | `isImmuneToSlow` only | **Y — 11 booleans** | | | | |
| Weight / mass (for push/shift) | – | **Y** (`massLevel`) | | | | |
| Aggro / taunt | – | **Y** (`tauntLevel`) | | | | |
| Leak damage per enemy | **Y** (`leakDamage`) | **Y** (`lifePointReduce`) | Y | Y (king damage) | Y | Y |
| Dodge / hit rate | – | **Y** (rare, 3%) | | | | |
| Elemental resistance | – | **Y** (`epResistance`) | | | | |
| Speed tiers as a design category | implicit | implicit | **Y** (named enemy roles) | | | **Y** ("Fast" class) |

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | An enemy field is how the designer says "your current answer is wrong here." Without fields, waves can only be bigger |
| **Designer cost** | Every field is a permanent tax on every future enemy *and* on every future tower, because each new tower must have a defined answer to each field. Arknights' own data shows the mitigation: **most fields are optional and default off** — only 8 of 25 are filled on more than 90% of enemies |
| **Breaks when wrong** | A field with no counter is a wall (an immunity nobody can pierce); a field with one counter is a tax (the mandatory camo-detection slot). The healthy middle is a field with three or four partial answers |

---

## 4. Damage types and counters in TD

### 4.1 Arknights — flat subtraction, percentage resist, and a floor

The shipped formulas ([wiki.gg](https://arknights.wiki.gg/wiki/Damage), which reproduces the
in-engine order of operations):

```
Atk       = [FLOOR(Atk_base × Atk_stage) × (1 + Atk+%)] + Atk_ex
Atk_final = FLOOR[ Atk × Π(1 − Atk−%) ]

Physical: Dmg = (Atk_final × Atk_x%) − [ (Def − Ignore_flat) × Π(1 − Ignore%) ]
Arts:     Dmg = (Atk_final × Atk_x%) × [ 1 − ((Res − Ignore_flat) × Π(1 − Ignore%)) ]
          where Res = [(Res_base + Res_flat) × (1 + Res+%) × Π(1 − Res−%)], clamped to [0, 1]
True:     unreduced by DEF or RES
Elemental: reduced by E-RES as a percentage; only lands once an Elemental Injury gauge fills
```

**Both Physical and Arts have the same floor: `Dmg ≥ 5% of Atk_final`.** That single clamp is what
keeps DEF from ever producing a hard immunity — a 9,999-DEF enemy still takes 5% from a
100-ATK attacker. **It is the design decision that lets Arknights ship flat subtraction at all.**

Buffs stack additively, debuffs stack multiplicatively — asymmetric on purpose, so stacking debuffs
has diminishing returns while stacking buffs does not.

Elemental is a **fourth channel with a threshold**: damage only applies *"whenever they are under the
effect of Elemental Injuries"*, and the injuries have fixed payloads — Burn **7,000**, Necrosis
**800/s for 15 s (12,000 total)**, Nervous Impairment **6,000**.
([wiki.gg](https://arknights.wiki.gg/wiki/Elemental_damage)) Before Episode 15 no enemy had E-RES at
all, so the channel shipped years before its counter did.

### 4.2 Kingdom Rush — one percentage, banded for the UI

Ironhide uses **percentage reduction** with named bands rather than a type table:

| Band | Reduction |
|---|---|
| None | 0% |
| Low | 1–30% |
| Medium | 31–60% |
| High | 61–90% |
| Great | 91–99% |
| Max | 100% |
| **Immune** | 100%, **and cannot be reduced to a weaker resistance** |
| **Vulnerable** | **increased** damage |
| Variable | non-fixed |

([wiki](https://kingdomrushtd.fandom.com/wiki/Armor_and_Magic_resistance)) Real shipped values from
KR1: Orc **30%** armour, Brigand **50%**, Marauder **60%**, Shaman **85%** magic resist, Giant
Spiders **65%** MR, Spider Matriarch **80%** MR, Spider Hatchling **50%** MR. Ironhide's own support
article confirms the two-channel framing and the counter routing — physical armour is answered by
magic towers, magic resist by *"ranged and artillery towers."*
([first-party](https://support.ironhidegames.com/support/solutions/articles/4000223666-armor-types-breakdown-kingdom-rush-battles-guide))

**`True Damage` is the escape hatch and it is explicit:** *"True Damage ignores any armor or magic
resistance … the damage inflicted is the same across the board."*
([wiki](https://kingdomrushtd.fandom.com/wiki/True_Damage))

**Two channels plus a bypass is the whole system.** Kingdom Rush ships 100+ enemies with no matrix at
all.

### 4.3 BTD6 — keyed immunities, and why that is not a matrix

BTD6 has **11 damage types** — Normal, Acid, Sharp, Explosion, Cold, Glacier, Shatter, Energy,
Arctic, Plasma, Fire — plus an inoffensive `Passive`. **No type deals more or less damage to
anything.** The entire interaction is a yes/no per bloon property.
([wiki](https://bloons.fandom.com/wiki/Damage_Types))

Reading the wiki's own quick-reference table, keyed on **Black / White / Lead / Ceramic / Glass /
Purple / Frozen / Ghost**:

| Damage type | Shorthand |
|---|---|
| Normal, Acid | **All** |
| Crushing | Not Ghost |
| Plasma | Not Purple; not Glass |
| Fire | Not Purple |
| Arctic | Not Purple or White |
| Shatter | Not Lead or Ghost |
| Glacier | Not White or Lead |
| Metal Freeze | Not White or Frozen |
| **Sharp** | **Not Lead or Frozen** |
| Passive | None |

The shipped data confirms this is implemented as a bitmask on both sides, not a table
([data](https://github.com/Btd6ModHelper/btd6-game-data)). Bloons carry `bloonProperties`, towers
carry `immuneBloonProperties`:

| Bit (derived) | Meaning | Evidence |
|---:|---|---|
| 1 | Lead | `Lead.bloonProperties = 1` |
| 2 | Black | `Black = 2`; DDT = 3 = Lead + Black |
| 4 | White | `White = 4`; Zebra = 6 = Black + White |
| 8 | Purple | `Purple = 8` |
| 16 | **Frozen** | every sharp tower's mask is **17 = 1 + 16**, matching the wiki's "Sharp = not Lead or Frozen" exactly |

Observed tower masks (computed from the exported tower models): **17** for Dart, Sniper, Tack,
Boomerang, Ninja, Super, Druid, Sub, Buccaneer, Ace, Dartling, Spike Factory, Engineer, Desperado;
**2** for Mortar (explosion, blocked only by Black); **5** for Ice (Lead + White); **1** for
Mermonkey; **73** for Wizard and Beast Handler; **64** for Alchemist; **no mask at all** for Banana
Farm, Monkey Village and Glue Gunner.

**Camo is not in this bitmask.** DDT-Camo carries `isCamo: true` and `propertyFlags: 1` while its
`bloonProperties` stays at 3, and Fortified is `propertyFlags: 4` with `bloonProperties: 0`.
**BTD6 runs three parallel keying systems — a damage-immunity bitmask, a visibility flag, and a
health-multiplier flag — and deliberately does not merge them.**

### 4.4 What the two approaches cost

| | Matrix (`N` attack × `M` armour) | Keyed immunity (BTD6, `K` binary keys) |
|---|---|---|
| Authored cells | `N × M`, all of which must be tuned | `K` bits per attack — **11 types × 8 keys = 88 booleans, most of them "yes"** |
| Player-facing rule | "how much" — a multiplier to remember | "can I or can't I" — a checklist |
| Failure mode | a dead row (WC3's Chaos is 0/6 non-neutral — see [01-typing-matrices.md](../game-design/01-typing-matrices.md)) | a **hard wall**: Purple + Lead + Camo on one bloon can be genuinely unanswerable by a whole tower set |
| Combinatorics | multiplicative and swingy (Pokémon's 4× / 0.25×) | additive and legible — properties simply union |

**The transferable observation:** a keyed system is far cheaper to author and far more brittle at the
extremes, and BTD6 manages the brittleness with **two universal types** (Normal and Acid pop
everything) and a large stock of upgrades that *grant* a missing key rather than change the damage
type — the wiki calls out `Hard Tacks` granting Frozen-popping power *"without directly changing its
damage type."* **The escape hatch is a per-upgrade key grant, not a multiplier.**

---

## 5. Endless and scaling

### 5.1 BTD6 freeplay — eight linear brackets, not an exponential

This is the most completely documented endless curve in the genre. It does **not** appear in the
exported model files (it lives in simulation code), so this is the wiki's datamine-derived
description **(wiki)** —
[Late Game and Freeplay (BTD6)](https://bloons.fandom.com/wiki/Late_Game_and_Freeplay_(BTD6)) — which
states it as *"a continuous piecewise linear function in eight brackets."*

| Rounds | MOAB-class HP gain per round | Multiplier at the ends |
|---|---:|---|
| 81–100 | **+2%** | ×1.02 → ×1.40 |
| 101–124 | **+5%** | ×1.45 → ×2.60 |
| 125–150 | **+15%** | ×2.75 → ×6.50 |
| 151–250 | **+35%** | ×6.85 → ×41.5 |
| 251–300 | **+100%** | ×42.5 → ×91.5 |
| 301–400 | **+150%** | ×93 → ×241.5 |
| 401–500 | **+250%** | ×244 → ×491.5 |
| **501+** | **+500%** | ×496.5, then **`f(N) = 5N − 2008.5`** |

Anchors given on the same page: a round-140 Fortified BAD has **200,000 HP and 429,920 RBE**; a
round-200 Fortified BAD has **960,000 HP and 1,995,520 RBE**.

**Three properties are worth naming.**

1. **It is linear in the round number within every bracket, and linear forever after 501.** Growth is
   `Θ(N)`, not `Θ(c^N)`. A tower whose damage also grows linearly stays relevant indefinitely.
2. **Leak damage is explicitly excluded** — the wiki notes *"This does not affect their life lost when
   leaked."* HP ramps; consequence does not. Scaling the punishment alongside the threat would make
   every mistake instantly fatal.
3. **The ramp starts at round 81, which is 20 rounds past the hardest authored difficulty's end
   (Hard ends at round 80).** The endless curve begins exactly where the authored content stops.

**Three other channels ramp on their own schedules, and none of them is HP.**

*Speed* — a piecewise linear function with **deliberate discontinuities**, given on the same page as
runnable pseudocode:

```
if      (r <=  80) v = 1;
else if (r <= 100) v = 1   + (r -  80) * 0.02;
else if (r <= 150) v = 1.6 + (r - 101) * 0.02;   // jump: 1.40 -> 1.60
else if (r <= 200) v = 3.0 + (r - 151) * 0.02;   // jump: 2.58 -> 3.00
else if (r <= 251) v = 4.5 + (r - 201) * 0.02;   // jump: 3.98 -> 4.50
else               v = 6.0 + (r - 252) * 0.02;   // jump: 5.50 -> 6.00
```

**The jumps are the design.** A smooth speed ramp would let a defence drift into failure; a step at
101, 151, 201 and 252 forces a re-evaluation on a known round.

*Cash per pop* decays on its own ladder: **50%** at rounds 51–60, **20%** at 61–85, **10%** at 86–100,
**5%** at 101–120, **4%** at 121–140, **2%** at 141+. **The income curve is throttled harder and
earlier than the threat curve grows.**

*Status resistance* ramps last: all stuns, knockback and sabotage slows lose **10%** duration at round
150, **20%** at 200, **30%** at 250, **40%** at 300, **50%** at 350+. **Control degrades but never
disappears — the floor is 50%, not 0.**

### 5.2 Difficulty as a set of mutators, not a curve

BTD6's difficulty modes are shipped as small lists of typed mutators
([data](https://github.com/Btd6ModHelper/btd6-game-data/tree/main/Mods)) — every one of these numbers
is first-tier:

| Mode | Lives | Start cash | Global cost | Global speed | Start round | End round | Monkey Money |
|---|---:|---:|---:|---:|---:|---:|---:|
| Easy | 200 | 650 | **×0.85** | — | 1 | 40 | 75 |
| Medium | 150 | 650 | — | **×1.10** | 1 | 60 | 125 |
| Hard | 100 | 650 | **×1.08** | **×1.25** | **3** | 80 | 200 |
| **Impoppable** | **set to 1** | 650 | **×1.20** | — | **6** | 100 | **×1.5** |

All modes share `SellMultiplier` 0.7 and `BonusCashPerRound` 100. Easy additionally carries
`SetHealthForBloonModModel{round: 40, bloonId: "Moab", healthMultiplier: 0.6667}` — **a one-line
override for exactly one bloon on exactly one round.** `DoubleMoabHealth` is a single
`BloonHealthModel{healthMod: 2.0, bloonTag: "Moabs"}`.

**The lesson: none of these four difficulties changes an enemy's stats.** They change lives, prices,
game speed, and which rounds you play. Enemy tuning happens once.

### 5.3 The other endless designs

| Game | Endless shape | Notable |
|---|---|---|
| **GemCraft Chapter 0** | Endurance, **511 waves** — finite | Gated behind wizard level 16 |
| **GemCraft: Chasing Shadows** | Endurance, **999 waves** — finite; all enemies gain a 2% mana-burn trait, and calling waves early stops granting bonus spell charges | **The endless mode changes the rules, not just the numbers** |
| **GemCraft: Frostborn Wrath** | **Unbounded.** Starts at 30 waves per field and grows: *"The number of wave stones added when beating endurance is 12% of the number of waves beaten or 5, whichever is higher"* | The endless *length* is itself a meta-progression track |
| **Infinitode 2** | Endless with a difficulty-scaled reward multiplier — *"up to 6x more Bit dust per run"* — and **"Endless leaderboards are now limited to the first 45 minutes of in-game time"** | Solves the "infinite mode rewards infinite patience" problem with a **time cap on the scoreboard**, not a cap on the mode |
| **Kingdom Rush** | **No endless mode at all.** Difficulty extends through Heroic and Iron constraint challenges | Sourced above |
| **Arknights** | No endless mode. **Contingency Contract** instead: the player picks stacking modifiers that *"buff enemies, debuff friendlies, and/or impose various limitations"* and is scored on the total | **Difficulty as a self-selected budget** |

([GemCraft wiki](https://gemcraft.fandom.com/wiki/Endurance) ·
[Infinitode dev blog](https://blog.infinitode.prineside.com/2024/07/infinitode-2-major-update-190-and.html) ·
[Arknights CC](https://arknights.wiki.gg/wiki/Contingency_Contract))

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | Gives a finished player somewhere to go, and turns a binary win into a score |
| **Designer cost** | Nearly free to add, expensive to keep honest: the moment one strategy scales faster than the curve, the mode collapses into a single build and a patience test. GemCraft's finite wave counts and Infinitode's 45-minute leaderboard window are both admissions of that |
| **Breaks when wrong** | Exponential threat with linear defence = a hard wall dressed as endlessness. Linear threat with exponential defence = the mode has no ceiling and the leaderboard measures free time. **BTD6 sits on linear/linear and throttles income instead — the only surveyed design that stays interesting for hundreds of rounds** |

---

## 6. Heroes, commanders, and an avatar on the board

| Game | What the avatar is | Levels | In-match progression | Death |
|---|---|---|---|---|
| **Kingdom Rush** | One player-controlled unit, moved freely, with its own abilities | **1–10** | XP from attacking during the level; 2 abilities unlocked at set levels | Respawns after **15 s at the spot it died** |
| **BTD6** | One hero per player, placed like a tower, **not upgradeable via crosspaths** | **1–20** | XP accrues **automatically at end of round**, split evenly in co-op; can also be bought with cash, and buying raises sell value | Cannot die |
| **Arknights** | No avatar — **every** Operator is a deployable unit with a redeploy timer | operator levels are meta, not in-match | none in-match | Redeploy after 18–200 s at a higher DP cost |
| **Dungeon Defenders** | A third-person character that fights *and* builds | meta levels + gear | mana earned in-match | respawn |
| **Orcs Must Die! / Deathtrap** | A War Mage fighting in third person alongside traps | roguelite run levels | run-scoped | run-scoped |

**BTD6's hero XP has a per-hero rate multiplier, which is the whole balance lever.** Heroes are
grouped into four ratios — **1.0×** (Quincy, Gwendolin, Striker Jones, Obyn, Etienne, Geraldo),
**1.425×** (Ezili, Pat Fusty, Brickell, Sauda, Corvus, Rosalia, Dan D'Monke), **1.5×** (Benjamin, Psi,
Silas), **1.71×** (Churchill, Adora) — where a higher ratio means *more XP needed per level*, so a
stronger hero simply arrives later. ([wiki](https://bloons.fandom.com/wiki/Heroes_(BTD6))) The XP
income side is a flat authored ramp: **40 XP on round 1, +20 per round through round 20, then +40 per
round for rounds 21–50** (wiki). Map difficulty adds **+10% per tier** above beginner.

The shipped data confirms the shape: **17 heroes, each with exactly 20 level records** (computed from
the exported `Towers/` tree), against 26 towers with 64 build states each. **A hero is one linear
track of 20 steps; a tower is a lattice of 64 nodes. They are deliberately different objects.**

**What an avatar changes about the genre.** It converts the game from *"arrange, then watch"* into
*"arrange, then steer."* The measurable consequences in the surveyed games:

- **It adds a time axis the placement puzzle does not have.** Kingdom Rush's 15-second respawn timer
  is a resource: the hero can be spent to plug a leak and be absent for the next fifteen seconds.
- **It makes early placement pay.** BTD6 heroes level from round XP whether or not they are doing
  anything, so hero value is a function of *when you place*, not *where*. That is an economy
  decision wearing a placement decision's clothes.
- **It is the cheapest way to add a power fantasy to a genre that has none**, and the cheapest way to
  break one: an avatar that can carry a level makes every tower decision optional.

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | Gives the player something to *do* during the wave, and a persistent identity across levels |
| **Designer cost** | A second balance domain with its own curve. BTD6's answer — one XP-rate number per hero — is the cheapest version found |
| **Breaks when wrong** | The hero solos the level and the towers become decoration; or the hero is a free stat stick with no decision attached, in which case it is just a slot everyone fills the same way |

---

## 7. Meta-progression outside the match

| System | Shape | Size | What it actually does |
|---|---|---:|---|
| **BTD6 Monkey Knowledge** | Flat point spend into 6 category trees | **134 nodes** (computed: Primary 32, Military 30, Magic 22, Support 22, Powers 15, Heroes 13) | Small permanent percentage buffs and unlocks. 1 point per account level from level 30; **level 155 needed for all 134** (wiki) |
| **Kingdom Rush stars** | 6 trees × **5 linear upgrades** = 30 nodes, in KR1/Frontiers/Origins | 30 | Bought with stars earned from levels and challenges |
| **Kingdom Rush: Vengeance / Alliance** | 4 trees, upgrade points; the Reinforcements tree **branches into two mutually-exclusive arms** | — | The only branch in the meta-tree |
| **Kingdom Rush 6: Genesis** | Per-tower / per-hero / per-spell tree fed by **that entity's own XP**; 11 nodes for towers and heroes, 8 for spells; 2 exclusive pairs per tower tree | 11 / 8 | Abilities must be unlocked in the tree before gold can buy them in-stage |
| **Arknights** | Level cap → Elite 1 → Elite 2 → skill levels 1–7 → **Mastery M1/M2/M3** → **Modules X / Y / Δ, three stages each** | per Operator | Deep and expensive; see below |
| **Dungeon Defenders** | A full ARPG loot layer over the TD: drops, chests, and mana-purchased upgrades to *"a weapon's base damage, secondary/elemental damage, attack rate, projectiles fired per volley, and projectile velocity"* | — | The TD becomes a loot treadmill |

**Arknights' progression gates, exactly** ([wiki.gg](https://arknights.wiki.gg/wiki/Promotion),
[Module](https://arknights.wiki.gg/wiki/Module)):

| Rarity | Max level (E0) | E1 max | E2 max | E2 cost | Module unlock |
|---|---:|---:|---:|---|---|
| 1★–2★ | 30 | — | — | — | — |
| 3★ | 40 | 55 | — | — | — |
| 4★ | 45 | 60 | 70 | 60,000 LMD + 5 Chip Packs | E2, level 40 |
| 5★ | 50 | 70 | 80 | 120,000 LMD + 3 Dualchips | E2, level 50 |
| 6★ | 50 | 80 | 90 | 180,000 LMD + 4 Dualchips | E2, level 60 |

Masteries require E2 and skill level 7. Module stages 2 and 3 additionally require **50%** and
**100%** Trust. **The deployment cost also moves with promotion** — `+2` (or `+3` for some branches)
at E1 and another `+2` at E2 — so **promotion makes an Operator stronger *and* more expensive to
field**, which is the single most transferable idea in this section.

**Which of these keeps players, and which is filler.** Judging by what each system does to the
*decision* rather than the number:

| Keeps players | Why |
|---|---|
| **Arknights promotion + mastery + modules** | Each step changes what the Operator *does* (new trait, new skill tier, higher block, a different branch identity), and the cost side moves with it |
| **Kingdom Rush 6's per-entity XP trees** | Progression follows what you actually play, and the exclusive pairs mean two players with the same hours have different trees |
| **Dungeon Defenders' loot layer** | Genuinely different runs produce genuinely different builds — the classic ARPG hook |

| Filler | Why |
|---|---|
| **Flat percentage meta-buffs (most of Monkey Knowledge, KR's 5 linear star upgrades)** | They change no decision. They gate early content behind account level and then never come up again. The tell: BTD6's own hardest modes (CHIMPS) **disable Monkey Knowledge entirely**, which is the design admitting the layer is not part of the game it wants to be judged on |

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | Gives a losing session a non-zero outcome, and gives a returning player a reason to open the app |
| **Designer cost** | A permanent power inflation the level tuning has to absorb; and if the meta layer is optional, **every level must be beatable without it and not trivial with it** — a two-sided constraint that only gets harder as the layer grows |
| **Breaks when wrong** | Meta power outruns level tuning and early content becomes unplayable-easy; or the layer becomes mandatory and new players hit a wall that is not a skill wall |

---

## 8. Roguelite tower defense

The genre's answer to "authored levels are expensive." **Run-scoped drafting replaces per-level
authoring: the level becomes a seed plus a sequence of choices.**

### 8.1 BTD6 Rogue Legends — a shipped roguelite with all its constants exported

`rogueData.json` is the single most useful roguelite TD document I found, because every tuning
constant is in it ([data](https://github.com/Btd6ModHelper/btd6-game-data/blob/main/rogueData.json)):

| Constant | Value |
|---|---:|
| `campaignStageCount` | **4** |
| `maxLives` | **5**; `restHealAmount` **2** |
| `maxArtifactInventorySize` | **50**; `maxInstaInventorySize` **10** |
| `startingArtifactPower` | 9 |
| `rareChance` / `rareChancePerStage` | **15.0** / **+5.0** per stage |
| `legendaryChance` / `legendaryChancePerStage` | **1.0** / **+0.5** per stage |
| `legendaryTileCount` | 2 |
| `merchantItemCount` | **8** |
| `baseRerollCost` | **300** (token reroll 3) |
| `endOfRoundInstaChance` / `midRoundInstaChance` | **30** / **50** |
| `freeplayBloonHealthPerStage` | **0.9** |
| `upgradeCostMultipliers` | **[1.15, 1.2, 1.3, 1.35, 1.4, 0.5]** |
| `purchaseCostMultipliers` | **[0.9, 0.8, 0.7, 0.65, 0.6, 0.5]** |
| `reverseChance` | 0.2 |
| CHIMPS variant | `startingLivesChimps` **2**, `instaInventorySizeAdditionChimps` **−2**, `rerollCostMultiplierChimps` **0.333**, `goalScalingChimps` **0.15**, `chimpsInstacostMultiplier` **1.08** |

The draft pool ships as **571 artifact records** (computed from the exported `Artifacts/` folder) and
**16 bloon modifiers**, each pinned to a `minimumStage` and to one of seven purpose-built round sets
(`RogueRoundSet`, `RogueImmuneSet`, `RogueLeadSet`, `RoguePurpleSet`, `RoguePinkSet`,
`RogueBloonierSet`, `RogueDenseSet`). **The run's difficulty is expressed as "which round set plays",
not as a stat multiplier** — the same authoring trick as the difficulty mods in §5.2.

Note `upgradeCostMultipliers` and `purchaseCostMultipliers` run in **opposite directions** across the
six entries: upgrades get progressively more expensive (1.15 → 1.4) while purchases get cheaper
(0.9 → 0.5). **Breadth is subsidised and depth is taxed, run-scoped.**

### 8.2 The rest of the field

| Game | Run structure | The draft |
|---|---|---|
| **Arknights: Integrated Strategies** | **Six floors** of branching nodes; run starts with **6 Hope**, squad grows **6 → 13** | Operators are *recruited* with Hope (cost rises with rarity), and **Collectibles** in three rarity grades (Normal / Rare / Super Rare) act as relics. Difficulty is an ascending ladder (Easy/Normal, then levels 1–15 or 1–18 by theme) ([wiki.gg](https://arknights.wiki.gg/wiki/Integrated_Strategies)) |
| **Rogue Tower** | *"a tower defense game with roguelike elements and a continuously expanding path which you can influence"* ([developer, Die of Death Games](https://store.steampowered.com/app/1843760/Rogue_Tower/)) | **The map itself is the draft** — the player picks the next tile of path |
| **Thronefall** | Day/night: build by day, *"choose for night to arrive"*, and after surviving *"the player regain all their destroyed buildings and units, and grants the monarch an amount of gold depending on the number of buildings that survived"* ([Wikipedia](https://en.wikipedia.org/wiki/Thronefall)) | **Reward is proportional to how cleanly you held** — the economy scores the quality of the defence, not just the win |
| **Dome Keeper** | Mine between waves; *"a limited amount of time to mine for resources underneath the dome"*; upgrade weapons or character ([Wikipedia](https://en.wikipedia.org/wiki/Dome_Keeper)) | **The draft currency is time.** Mining longer means a stronger dome but a later return |
| **Orcs Must Die! Deathtrap** | *"Enjoy endless replayability as your War Mage grows stronger through roguelite progression"* ([developer, Robot Entertainment](https://store.steampowered.com/app/2273980/Orcs_Must_Die_Deathtrap/)) | Run-scoped trap and hero upgrades |
| **Kingdom Rush** | No roguelite mode found in the surveyed material — see *What I could not find* | — |

**Problem / cost / failure mode:**

| | |
|---|---|
| **Problem it solves** | Replayability without level authoring, and a reason for a build to be *discovered* rather than looked up |
| **Designer cost** | A large pool (BTD6 ships 571 artifacts) of items that each interact with an already-complex tower system. **The pool is the content, and every entry is a potential combo bug** |
| **Breaks when wrong** | Two failure modes, opposite. Draft variance too high: the run is decided by the first three offers and skill stops mattering. Draft variance too low: every run converges on the same build and the mode is one level replayed. The shipped mitigations found here are **stage-gated rarity** (BTD6's `rareChancePerStage`), **paid rerolls** (300 base), and **run-scoped inventory caps** (50 artifacts, 10 instas) |

---

## 9. Lane defense specifically

### 9.1 Element TD — the one directly relevant system

Element TD (Warcraft III, by Karawasa) is the closest non-PvZ analogue to a build-a-defence game with
an element system, and the author's own description is precise
([first-party](https://www.hiveworkshop.com/threads/element-td-survivor-4-3b.125024/)):

**The element cycle.** *"Each elemental armor takes additional damage from the element preceding it
and retains near-invulnerability from the element following it."*

```
Light > Darkness > Water > Fire > Nature > Earth > Light
```

**Six elements, one cycle, one rule.** No table is authored — the relation is generated from the ring,
so adding a seventh element would cost one entry, not twelve cells. Contrast the 42-cell WC3 matrix
in [01-typing-matrices.md](../game-design/01-typing-matrices.md).

**Combination is where the content comes from.** *"Each element combines with another element to
create a unique dual tower, and each dual tower can combine with a third and separate element to
create a triple element tower. There are 15 dual towers and 20 triple towers in Element TD."*
The full space (computed): `6 + C(6,2) + C(6,3) = 6 + 15 + 20 = 41` **tower identities from 6
authored elements**, and each has three upgrade levels gated on the component elements —
*"Two dead Fire Guardians and two dead Water Guardians allows for a level 2 Fire + Water dual-element
tower."*

**Elements are drafted, not bought.** *"Beginning at level 5 and for every 5 levels after that, the
player chooses an Elemental Guardian to summon … when killed they grant the user the use of a specific
element."* Over 60 waves that is **12 element picks** — a build-defining draft on a fixed schedule,
years before roguelite TD was a category.

**Two economy rules worth recording:**
- **Interest on hoarded gold: 2% of current cash every 15 seconds**, and *"players may also increase
  the initial interest rate once every five levels at the cost of an Element pick"* — an explicit
  trade of power for economy, priced in the same currency as build identity.
- **Leaks are recoverable.** *"In Element TD, leaked creeps respawn at the start of the path. This
  gives you another chance at the gold they carry, but gives the creep another chance at you."*
  The player loses a life but **not the income**, which removes the death-spiral that most TDs have.

### 9.2 Legion TD 2 — the competitive lane defense

From the developer's own manual ([first-party](https://beta.legiontd2.com/manual/)):

- **21 waves**, one lane per player, *"Most games will end before wave 21."*
- **Leaks converge.** *"the different lanes converge into a single one before getting to your team's
  king. Here, all the fighters that cleared their lanes are gathered, so that they can make one last
  defense against the leaked waves."* **A clean lane is not just its own reward — it becomes a
  reserve for a teammate's failure.**
- **Fighters are restored between waves:** *"After each enemy wave, your fighters are fully healed and
  restored to their original positions."* The wave, not the unit, is the unit of attrition.
- **Two currencies, coupled.** Gold builds fighters; **mythium** buys mercenaries sent at opponents;
  *"Income is a number that increases permanently whenever you spend mythium."* Workers cost **50
  gold** and generate **1 mythium per 10 seconds**, with additional mythium after wave 10.
- **Attack/armour types exist but are deliberately opaque.** The manual states multipliers *"ranging
  from 75% to 125%"* and immediately says *"You don't have to know the exact percentages."*
  **A shipped counter system whose designer explicitly declines to publish the table** — the opposite
  of Pokémon's memorisation contract.
- Reroll: swap *"up to 4 fighters"*, once per game.

### 9.3 The rest

| Game | Shape | The transferable idea |
|---|---|---|
| **Anomaly: Warzone Earth** | **Reverse TD.** The player *"set[s] paths for the convoy to follow along city streets"* through enemy towers, buying and equipping *"several different units with varying offensive and defensive attributes to make up the convoy"* and dropping *"power-ups such as decoys or smoke-screens"* ([Wikipedia](https://en.wikipedia.org/wiki/Anomaly:_Warzone_Earth)) | **Routing as the primary verb.** The convoy composition is a lane; the map is the enemy's placement puzzle |
| **Gem TD / GemCraft lineage** | Towers are gems that are *combined and upgraded* rather than replaced; GemCraft's Endurance is the endless mode in §5.3 | A tower is a **mutable object**, not a slot in an upgrade tree |
| **Iron Marines** (Ironhide) | Squad-based real-time strategy with TD DNA — hero units and fixed build pads | Ironhide's own migration path from fixed-slot TD toward controllable squads |
| **Plants vs Zombies** | 5 lanes × 9 columns, one plant per cell, cooldown + sun cost per plant | The reference point for this project; not researched further here |

**Problem / cost / failure mode of lane defense as a geometry:**

| | |
|---|---|
| **Problem it solves** | Makes position legible. A player can see, at a glance, which lane is losing — something a free-placement maze never communicates. It also makes **failure local**: one lane collapsing is a recoverable event, not a game over |
| **Designer cost** | The lane is a very tight constraint on ability design. Anything that is not "in front of me" or "in my lane" needs a bespoke targeting rule, and cross-lane effects are the main source of balance surprises |
| **Breaks when wrong** | Lanes stop being independent (a single cross-lane tower solves all of them, so the geometry is decoration), or they never interact at all (the game is *N* independent one-lane games and the board adds nothing) |

---

## What I could not find

1. **The BTD6 `bloonProperties` bits 32 and 64.** I derived 1 = Lead, 2 = Black, 4 = White,
   8 = Purple and 16 = Frozen with confidence (16 is confirmed by every sharp tower carrying mask
   `17 = 1 + 16`, matching the wiki's "Sharp: not Lead or Frozen"). **Bit 64 is unresolved** — it
   appears on Wizard Monkey and Beast Handler (`73 = 1 + 8 + 64`) and alone on Alchemist (`64`),
   which rules out both Camo (Druid has no camo detection yet is `17`; Ninja has camo detection and
   is also `17`) and Purple. Bit 32 was not observed on any tower or bloon I sampled. The enum is
   presumably named in the BTD Mod Helper source; I did not locate the declaration.
2. **BTD6's freeplay HP ramp in first-tier form.** The eight-bracket function is documented only on
   the Bloons Wiki. It does not appear in any exported model file — it lives in simulation code. The
   anchor values quoted (200,000 HP at round 140; 960,000 at round 200) are internally consistent
   with the brackets, but I could not verify them against the binary.
3. **The BTD6 Paragon degree formula.** The **thresholds** are first-tier (the shipped 100-entry
   array), but the mapping from raw pops and cash to power is not — `popsOverX` (180) and
   `moneySpentOverX` (20,000) are clearly divisors or scale factors, and I could not confirm the
   exact expression that turns them into power.
4. **Dungeon Defenders' actual DU numbers.** Wikipedia confirms the DU budget exists per level; the
   Dungeon Defenders wiki page for `Defense Units` is a stub with no numbers, and I found no source
   giving per-map DU allowances or per-tower DU costs.
5. **Legion TD 2's attack/armour multiplier table.** The developer states the range (75%–125%) and
   deliberately declines to publish the cells. I found no datamine of the actual matrix.
6. **Element TD's exact multipliers.** The author states the *relation* ("additional damage from the
   preceding element", "near-invulnerability from the following") but no percentages. A secondary
   source suggested 200% / 50%; I could not corroborate it against a primary source and have not
   reported it as fact.
7. **GemCraft's endless HP formula.** Community calculators exist and community posts cite
   10³⁶ HP at wave 700 and 10⁵⁰ at wave 999 on the hardest difficulty, but I found no published
   formula or datamine, and I have not repeated the magnitudes as sourced values.
8. **Infinitode 2's endless scaling formula.** The dev blog gives balance *changes* (Fast enemy HP
   −10%, L20 tower PWR 17% → 21.5%) but never the underlying wave scaling function.
9. **Rogue Tower and Orcs Must Die! Deathtrap mechanics beyond store copy.** Neither has a Wikipedia
   article; neither has a reachable wiki. I have the developers' own one-paragraph descriptions and
   nothing deeper — no draft pool sizes, no upgrade counts.
10. **Kingdom Rush "Rift" mode.** Named in the research brief; I found no such mode in any Kingdom
    Rush source I could reach and cannot confirm it exists under that name.
11. **Kingdom Rush total star counts and the exact per-node star costs.** The tree *shape* is
    sourced (6 trees × 5 upgrades in KR1/Frontiers/Origins); the costs are not.
12. **Arknights per-stage `characterLimit` across the full 367-stage main story.** I sampled 60 at
    random; the remaining 307 are unmeasured, so "8 in 52/60" is a sample statistic, not a census.
13. **A Crush the Castle-style lane defense.** The brief named it; Crush the Castle is a projectile
    physics game, not a lane defense, and I found no lane-defense relative of it.

---

## Hooks for this project

**Non-normative, un-vetted, and explicitly not a design. These are observations that happen to touch
this repo's surfaces. Nothing here has been checked against the code, and none of it is a
recommendation.**

- **The 5×9 lawn already is the "placement scarcity" dial that §2 says is the real difficulty lever.**
  The Arknights comparison is the sharp one: 39 buildable tiles, 8 concurrent slots. The repo's lawn
  has 45 cells and no concurrency cap of that kind.
- **BTD6's `immuneBloonProperties` bitmask and this repo's element/status vocabularies are the same
  shape** — a keyed yes/no rather than a multiplier. §4.4's finding (keyed systems are cheap to
  author and brittle at the extremes, mitigated by two universal types plus per-upgrade key grants)
  is the part that would transfer, if anything does.
- **The "leak damage does not ramp" rule** (§5.1) is a separation of *threat* from *consequence* that
  an endless-grind SSOT touches directly.
- **BTD6's four difficulty modes change zero enemy stats** (§5.2) — they change lives, prices, speed
  and which rounds play. That is a different axis from a power ladder.
- **Arknights' promotion raises an Operator's deployment cost as well as its stats** (§7). A
  progression step that costs more to *use* is not a pattern this repo's rarity ladder currently has
  an analogue for, as far as this research went — but I did not read the code, so treat that as an
  open question, not a finding.
- **Element TD gets 41 tower identities from 6 authored elements** via `6 + C(6,2) + C(6,3)` (§9.1).
  That is a combinatorial-generation shape adjacent to what the seedsmith pipeline does with species
  and affixes.
- **BTD6's Rogue Legends expresses run difficulty as "which round set plays", not as a multiplier**
  (§8.1) — content selection rather than stat scaling.
- **Legion TD 2 ships a counter system and refuses to publish the numbers** (§9.2). Whether a counter
  system should be legible to the player at all is a live design question in the genre, not a settled
  one.
