# RTS and auto-battler mechanics — commanders, synergy arithmetic, auras, supply, positioning

Captured 2026-09-02. Companion to [01-typing-matrices.md](../game-design/01-typing-matrices.md) and
[02-unit-variables.md](../game-design/02-unit-variables.md), which already cover RTS damage/armour
matrices and unit attribute fields. **Nothing here re-derives those.** This document is about the
layer above a single unit: how a game turns *the composition of a group* into a decision.

Numbers marked **(computed)** were tallied by this study from shipped data files; the file and the
method are named at each point. Wiki-sourced numbers are marked **second-tier**. A block headed
**INFERENCE** is this study's reading, not a sourced claim.

---

## The finding in one paragraph

Every game surveyed makes group composition matter by the same three-step trick, and the numbers are
smaller than they look. **First, a unit is given two or three group memberships instead of one** — in
Teamfight Tactics Set 18, 55 of 85 costed champions carry exactly 2 traits and the mean over
trait-bearing champions is 2.12 (computed from Community Dragon `en_us.json`). **Second, the payoff
for a group is a step function, not a slope** — Brawler pays 25% / 40% / 65% team health at exactly 2,
4 and 6 members and nothing in between, so the marginal unit is worth either zero or everything.
**Third, a separate layer that is not a unit at all reshapes what the same units do** — a StarCraft II
Co-op commander, a Warcraft III hero, a Company of Heroes doctrine, all cheap to author because they
multiply an existing roster instead of adding to it. The bounding devices are equally consistent:
identical auras never stack, stacking bonuses carry an explicit count cap (Age of Empires II
Lithuanians: +1 attack per relic, **up to +4**), the meta-layer's upside is paired with a written
downside (all 54 StarCraft II prestige talents), and army size is taxed rather than capped
(Warcraft III upkeep: 100% gold below 51 food, 70% at 51–80, 40% at 81+). Positioning systems are the
most transferable and the most constrained: across all 18 Darkest Dungeon heroes, **110 skills use
only 7 distinct launch-rank masks and 13 target masks, and every single one is a contiguous run of
ranks** (computed) — the designer never wrote "usable from rank 1 and 4".

---

## 1. Commander and meta-layer systems

This is the section that matters most for a summoner: a commander layer is a **ruleset over an
existing roster**, not more roster.

### 1.1 StarCraft II Co-op Commanders — the fullest shipped example

**18 commanders**, each a different ruleset over the same mission set. Blizzard's own 5.0 patch notes
state it: *"With 18 commanders and 3 Prestige Talents per commander, this means there are 54 new
Prestige Talents to try out."*
([news.blizzard.com 5.0 patch notes](https://news.blizzard.com/en-us/article/23482838/starcraft-ii-5-0-patch-notes))

Three stacked progressions, each doing a different job:

| Layer | Range | Scope | What it buys |
|---|---|---|---|
| **Commander level** | 1–15 | per commander | Unlocks units, abilities and upgrades. *"These levels are specific to each commander and are not shared between them."* |
| **Mastery level** | 0–90 | **account-wide** | 1 mastery point per level. *"At max level, you will get a total of 90 Mastery Points to distribute into three different power sets, 30 points each."* |
| **Prestige** | 3 per commander | per commander | Replay 1–15 to unlock one talent; **one equipped at a time, or none** |

Source for levels and mastery: [starcraft2coop.com/resources/levels](https://starcraft2coop.com/resources/levels)
(community reference built on datamined values — second-tier, but it agrees with the Blizzard notes on
every value they both carry).

**The mastery shape is uniform and tiny: 3 sets × 2 competing options × 30 points.** 90 points is
exactly enough to max one option in each set, so every set is a forced either/or. Real values:

| Commander | Set 1 | Set 2 | Set 3 |
|---|---|---|---|
| **Raynor** | Research cost **−2%/pt (−60% max)** *vs* drop-pod unit speed **+2%/pt (+60%)** | Hyperion cooldown **−4 s/pt (−120 s)** *vs* Banshee airstrike cooldown **−4 s/pt (−120 s)** | Medic extra heal target **+3%/pt (+90%)** *vs* mech attack speed **+1%/pt (+30%)** |
| **Zagara** | Zagara/Queen regen **+1%/pt (30%)** *vs* Zagara attack damage **+1/pt (30)** | Intensified Frenzy **+1.5%/pt (45%)** *vs* Zergling evasion **+1.5%/pt (45%)** | Roach damage & life **+2%/pt (60%)** *vs* Baneling attack damage **+1/pt (30)** |
| **Mengsk** | Laborer/Trooper Imperial Support **+1%/pt (30%)** *vs* Royal Guard Imperial Support **+1%/pt (30%)** | Terrible Damage **+1%/pt (30%)** *vs* Royal Guard cost **−0.66%/pt (−20%)** | Starting Imperial Mandate **+1/pt (30)** *vs* Royal Guard XP rate **+0.5%/pt (15%)** |

Sources: [Raynor](https://starcraft2coop.com/commanders/raynor),
[Zagara](https://starcraft2coop.com/commanders/zagara),
[Mengsk](https://starcraft2coop.com/commanders/mengsk).

**Three things to take from that table.** The per-point step is always small and always linear — 1%,
2%, 1.5%, 4 seconds. The *ceiling* carries the weight, and ceilings differ by an order of magnitude
between rows (+30% mech attack speed against −120 s on a hero cooldown), which means Blizzard is not
balancing the two options in a set against each other numerically at all; they are balancing them
against two different playstyles. And **the units never change** — mastery only bends rates, costs and
cooldowns on a roster the commander already had.

**Prestige is the interesting part, because every talent is a written trade.** Blizzard shipped no
pure upgrades. Examples, with the drawback quoted alongside the bonus:

| Commander | Talent | Bonus | Drawback |
|---|---|---|---|
| Raynor | Backwater Marshal | Biological combat units gain **100% increased life** | **MULEs are unavailable** |
| Raynor | Rebel Raider | Starport/Armory/Orbital lose tech requirements; Starport units **−30% gas** | **Combat units cost 50% more minerals** |
| Abathur | The Limitless | Ultimate Evolutions **uncapped in number** | Cost **200 Biomass**; Biomass gives **half** the normal benefit |
| Alarak | Artificer of Souls | Supplicant deaths permanently buff a nearby mech unit | Alarak's active abilities deal **50% less damage** |
| Zagara | Scourge Queen | Higher supply cap, extra spawns from eggs and nests | **Zagara is unavailable** |
| Mengsk | Principal Proletariat | Reduced vespene cost, **doubled XP** for Royal Guards | Royal Guard **mineral cost +100%**, **supply cost +50%** |

Two of these delete the commander's own hero unit or their signature economy button. That is the
strongest single design statement in this document: **the meta-layer's power budget is paid for by
removing something, not by adding a cost.**

- **Problem it solves for the player:** the same 20 missions stay interesting for hundreds of hours,
  because the *rules* change rather than the content.
- **What it costs the designer:** one commander is a full faction's worth of unit/ability tuning.
  Mastery is cheap (six numbers and a cap). Prestige is expensive because each talent invalidates a
  balance assumption somewhere.
- **What breaks when tuned wrong:** a mastery option that scales a multiplicative term makes the other
  option in its set dead; a prestige drawback that the mission structure lets you route around turns
  the talent into a free upgrade.

### 1.2 Warcraft III heroes — the layout every later game copied

From Blizzard's own hero page
([classic.battle.net/war3/basics/heroes.shtml](http://classic.battle.net/war3/basics/heroes.shtml)):

- *"You cannot train more than 3 Heroes."* One of each type.
- *"The maximum Hero level is 10."*
- *"Whenever a Hero gains a new level, they will also gain an ability point."*
- *"Every Hero will have one ultimate ability that they may choose for the first time at level 6."*
- *"Heroes can carry up to 6 items."*
- The first hero costs **5 supply** and no gold.
- Revive: *"approximatively half the cost for building the Hero plus 10% more per level"*, capped at
  **550 gold** at an Altar, **1105 gold + 260 lumber** at a Tavern.
- *"If two Heroes are both commanding a unit that makes a kill, then the experience received from the
  kill will be split evenly between the two Heroes."*

**The 3-plus-ultimate layout is enforced by ability level gates, not by a rule about ability counts.**
Each of the three normal abilities has three ranks, learnable at hero levels **1 / 3 / 5**; the
ultimate has one rank at level 6. Confirmed on two heroes:

| Hero | Ability | Rank 1 (lvl 1) | Rank 2 (lvl 3) | Rank 3 (lvl 5) | Radius |
|---|---|---|---|---|---|
| Paladin | Devotion Aura | +2 armor | +3.5 armor | +5 armor | 90 |
| Tauren Chieftain | Endurance Aura | +10% move, +5% attack rate | +15% / +10% | +20% / +15% | 90 |

Source: [warcraft.wiki.gg Paladin (Warcraft III)](https://warcraft.wiki.gg/wiki/Paladin_(Warcraft_III)),
[Tauren Chieftain (Warcraft III)](https://warcraft.wiki.gg/wiki/Tauren_Chieftain_(Warcraft_III))
(second-tier, but each page reproduces the in-game ability table verbatim, including the patch 1.10
change note *"Devotion Aura now gives 1.5/3/4.5 armor per level"* which the current 2/3.5/5 supersedes).

**The XP curve.** World Editor gameplay constants: `Experience required = previous × 1 + level × 100 +
0`, with a base of 200
([world-editor-tutorials.thehelper.net](https://world-editor-tutorials.thehelper.net/cat_usersubmit.php?view=68382)).
That yields cumulative thresholds:

| Level | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| **Total XP** | 200 | 500 | 900 | 1400 | 2000 | 2700 | 3500 | 4400 | 5400 |
| Increment | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 |

**A pure arithmetic series — the increment grows by exactly 100 per level, so total XP is quadratic in
level and the whole curve is two constants** (computed from the stated formula; the increments are the
formula's `level × 100` term).

Blizzard's stated design intent, reported second-hand: the team had *"nailed"* the StarCraft form and
moved deliberately toward a small-party, hero-centric game; Rob Pardo has described the central problem
as *"how is the hero heroic in let's say a five-unit skirmish versus a 40-unit skirmish"*, and one
designer on the team reportedly refused to playtest with heroes at all. Reported via
[hiveworkshop.com's history piece](https://www.hiveworkshop.com/threads/how-warcraft-iii-birthed-a-genre-changed-a-franchise-and-earned-a-reforge-ing.321817/)
— **second-tier; treat the wording as paraphrase, not a verified quote.**

### 1.3 Company of Heroes — doctrines as an in-match commitment

Three shipped shapes, worth comparing side by side because Relic changed the answer twice.

**CoH1 (2006): pick the tree in-match, spend command points, two branches of three.** Each faction has
three companies, each split into two branches, each branch holding three unlocks bought with Command
Points earned during the game. Exact costs from a community breakdown
([gamereplays.org](https://www.gamereplays.org/community/index.php?showtopic=123619)) — **second-tier**:

| Company | Branch A (CP) | Branch B (CP) |
|---|---|---|
| Infantry | Rapid Response 1 · Rangers 2 · Off-map Combat Group 4 | Defensive Operations 1 · Off-map Artillery 2 · 105mm Howitzer 2 |
| Airborne | Paratroopers 2 · Paradrop AT Gun 2 · Supply Drops 2 | Air Recon 1 · Strafing Run 2 · Bomber Run 3 |
| Armor | Raid 2 · Calliope 3 · M26 Pershing 4 | Fast Deployment 1 · Field Repairs 2 · Allied War Machine 4 |
| Defensive (Axis) | For the Fatherland 1 · Fortify Perimeter 2 · 88mm Flak 2 | Advanced Warning 1 · Registered Artillery 2 · Rocket Artillery 4 |
| Blitzkrieg (Axis) | Infantry Assault Team 2 · Urban Assault Team 3 · Armor Assault Force 3 | Assault Grenadiers 1 · Resource Blitz 2 · Blitzkrieg Assault 3 |
| Terror (Axis) | Zeal 1 · Firestorm 3 · Tiger Ace 5 | Inspired Assault 1 · Propaganda War 3 · V1 Rocket 4 |

**6 unlocks per doctrine, costs 1–5 CP, and the expensive slot is always last in its branch.**

**CoH2: the choice moved out of the match.** Each commander has **5 doctrinal abilities that unlock
automatically at command-point thresholds**; the player picks **three commanders before the match** on
a loadout screen and **deploys one during it**
([coh2.org guides](https://www.coh2.org/guides/19066/u-s-forces-commanders)).

**CoH3: back into the match, with a per-tier fork.** *"Every army has three battlegroups, each of which
is split into two different sub-groups"*; *"Each Battlegroup is comprised of 10 Battlegroup Abilities"*;
*"Battlegroups also have four tiers of abilities, and you can only pick one ability from each tier"*,
with the unchosen one **locked for the remainder of the match**
([PC Gamer battlegroups guide](https://www.pcgamer.com/company-of-heroes-3-factions-and-battlegroups-guide/)).
**That is 2⁴ = 16 distinct paths through one battlegroup, from 10 authored abilities** (computed).

- **Problem it solves:** an army that plays differently game to game without a second unit roster.
- **What it costs:** CoH1's shape needs six abilities per doctrine and 18 per faction. CoH3's shape
  gets 16 outcomes from 10 abilities — a better ratio for the same authoring.
- **What breaks:** if one branch's tier-1 pick is strictly better, the tree collapses to one line and
  the other 15 paths are decoration.

### 1.4 Dawn of War II: Last Stand — level as options, not power

Three players, three heroes, **20 waves**, level cap **20**, and the design point that makes it worth
citing: *"Leveling up does not change a hero's base stats, but whenever a hero levels up, a new piece
of wargear will be added to their inventory. This means even at low levels, heroes can be just as
powerful as those at high levels — but having a higher level hero will give you more options."*
Heroes are **incapacitated rather than killed, and the game only ends when all three are down
simultaneously**. Sources: [dow.fandom Last Stand introduction](https://dow.fandom.com/wiki/The_Last_Stand/Introduction),
[dawnofwar2.fandom Last Stand](https://dawnofwar2.fandom.com/wiki/Last_Stand) — **second-tier**.

**A progression that grants breadth rather than magnitude is the only one in this survey that does not
create a power gap between a new and a veteran participant.** That is a rare property and cheap to
copy.

---

## 2. Auto-battler synergy systems — what the arithmetic actually is

### 2.1 Teamfight Tactics, measured from shipped data

All figures in this subsection are **computed** from Riot's own live trait/champion data, retrieved as
`https://raw.communitydragon.org/latest/cdragon/tft/en_us.json` (24.2 MB, retrieved 2026-09-02) and
tallied per set mutator. This is the shipped table, not wiki prose.

**Set 18 (`TFTSet18`, the live set) shape:**

| Measure | Value |
|---|---:|
| Costed champions (cost 1–5) | **85** |
| Champions carrying at least one trait | 74 |
| Total (champion, trait) memberships | **157** |
| Mean traits per trait-bearing champion | **2.12** |
| Champions with exactly 2 traits | **55 of 85 (65%)** |
| Champions with 3 traits | 14 |
| Champions with 1 trait | 5 |
| Traits with at least one member | **35** |
| Trait membership size: min / median / mean / max | 1 / **5** / 4.49 / 10 |

**The two-traits-per-unit rule is real and it is the load-bearing choice.** A unit that belongs to two
groups is a unit two different plans can recruit, which is what makes a shop roll interesting rather
than a lottery.

**Breakpoint structure per trait, Set 18** (number of tiers each trait has):

| Tiers | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| Traits | 12 | 5 | 10 | 7 | 2 |

**90 breakpoints across 36 trait records — 2.50 per trait** (computed). The classic 2/4/6 is alive but
is now one pattern among several:

| Trait (Set 18) | Breakpoints |
|---|---|
| Brawler, Defender, Juggernaut, Ravager, Spellweaver, Vanguard, Blackthorn | **2 / 4 / 6** |
| Hunter, Invoker, Lunar, Rapidfire | 2 / 3 / 4 / 5 |
| Adaptor, Executioner | 2 / 3 / 4 |
| Sprykin | 3 / 5 / 7 |
| Elderwood, Blossom | 3 / 5 / 7 / 9 / 11 |
| Riftbeast | 3 / 5 / 7 / 10 |
| Coven | 3 / 4 / 5 / 7 |
| Inferno | 2 / 3 / 5 / 7 |
| Fae, Primal | 2 / 4 |
| Solar | 3 |

Breakpoint *values* across six clean sets (3, 7, 13, 14, 17, 18), counting every tier of every trait
(computed):

| minUnits | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Occurrences | 43 | **112** | 77 | **105** | 51 | 68 | 25 | 16 | 13 | 12 | 4 |

**2 and 4 are the spine, 3 and 6 are the second rank, and everything above 7 is a deliberate
"vertical" trait that asks you to give up the rest of the board.** The first breakpoint of a trait is
1, 2 or 3 in every set and never higher (computed: Set 18 first-breakpoint distribution is 12 traits
at 1, 17 at 2, 6 at 3).

**Set-over-set drift** (computed):

| Set | Traits | Total breakpoints | Per trait |
|---|---:|---:|---:|
| 3 (Galaxies) | 26 | 57 | 2.19 |
| 7 | 29 | 102 | 3.52 |
| 13 | 33 | 90 | 2.73 |
| 14 | 26 | 78 | 3.00 |
| 17 | 44 | 109 | 2.48 |
| 18 | 36 | 90 | 2.50 |

**The trait count has grown by about 40% since Set 3; breakpoints per trait have not moved.** The
system scales by adding groups, not by deepening them.

**What the arithmetic actually is.** Real magnitudes, read straight out of the trait effect variables
in the same file:

| Trait | Tier 1 | Tier 2 | Tier 3 | Per-member value |
|---|---|---|---|---|
| **Brawler** (2/4/6) | +25% health to Brawlers, +120 flat to the whole team | +40% | **+65%** | 12.5% → 10.0% → **10.8%** |
| **Vanguard** (2/4/6) | 18% max-health shield at combat start and on dropping below 50% health, 10 s | 32% | **42%** plus 5% durability while shielded | 9.0% → 8.0% → **7.0%** |
| **Blackthorn** (2/4/6) | +175 health (sacrifices the ally on the Blackthorn hex) | +300, bonus 30% stronger | **+550**, bonus 60% stronger | 87.5 → 75.0 → **91.7** |

**Per member, the payoff is close to flat — the superlinearity is not in the numbers, it is in the
shape.** Between breakpoints the marginal unit is worth **exactly zero**; at a breakpoint it is worth
the whole step (the 4th Brawler is worth +15 percentage points of team health to five other units).
That step function is the entire mechanism. A designer who smooths it into a per-unit slope has
deleted the game.

Upper bound on how much of that a board can hold: a level-9 board of 9 units carries about
9 × 2.12 ≈ 19 memberships, and the cheapest breakpoint costs 2 members, so **no more than about 9
traits can be lit at once, and realistically far fewer** (computed upper bound).

**The economy that gates it.** Set 18 shop odds by level, pool sizes and XP costs
([tftflow.com Set 18 tables](https://tftflow.com/tables/set18/shop-odds-pool-size-xp-table) —
second-tier, but consistent with the Set 17 figures reported by
[Esports Tales](https://www.esportstales.com/teamfight-tactics/champion-pool-size-and-draw-chances)):

| Level | 1-cost | 2-cost | 3-cost | 4-cost | 5-cost |
|---:|---:|---:|---:|---:|---:|
| 3 | 75% | 25% | — | — | — |
| 5 | 45% | 33% | 20% | 2% | — |
| 7 | 16% | 30% | 43% | 10% | 1% |
| 9 | 10% | 17% | 25% | 33% | 15% |
| 11 | 1% | 2% | 12% | 50% | 35% |

| Cost | Copies in pool | Distinct champions | Total in game |
|---:|---:|---:|---:|
| 1 | 30 | 14 | 420 |
| 2 | 25 | 13 | 325 |
| 3 | 18 | 14 | 252 |
| 4 | 10 | 14 | 140 |
| 5 | 9 | 10 | 90 |

XP to level: 2, 2, 6, 10, 20, 36, 60, 68, 68 (levels 1→2 through 9→10).

**The pool is shared across all eight players and it is finite.** That is what stops every player
converging on the strongest trait — the 6th Brawler you need may already be on someone else's board.

- **Problem it solves:** it turns a random shop into a plan. Two memberships per unit means most offers
  are relevant to something you are building.
- **What it costs the designer:** a trait table, a membership list per unit, a per-board tally, and a
  UI that shows the tally live. The tally is trivial; the *authoring discipline* — keeping every unit
  at two memberships and every trait at 4–8 members — is the real cost.
- **What breaks when tuned wrong:** a trait whose first breakpoint pays more than the units cost makes
  every other plan wrong. A trait with only two members cannot be built toward, so it is a caption, not
  a mechanic.

### 2.2 Hearthstone Battlegrounds — tribes plus a tavern economy

- **8 players**, board limit **7 minions**
  ([hearthstone.wiki.gg Battlegrounds](https://hearthstone.wiki.gg/wiki/Battlegrounds) — second-tier).
- **10 minion tribes** (Beast, Demon, Dragon, Elemental, Mech, Murloc, Naga, Pirate, Quilboar, Undead)
  plus tribeless minions.
- **A random subset of tribes is present per lobby**, announced at the start. The rule was documented
  as *"with 6 currently active tribes, only 5 of them will be available each game"* when six existed;
  the count has moved with the tribe count and I could not pin the current-patch number — see
  *What I could not find*.
- Gold: start at 3, **+1 per round to a maximum of 10**.
- Tavern upgrade costs are patch-dependent and the two sources disagree: the wiki gives
  5 / 7 / 8 / 11 / 11 for tiers 2–6, another gives 5 / 7 / 9 / 11 / 13 / 15 for tiers 2–7. **Both are
  second-tier and at least one is stale.** What is consistent is the shape: **the price rises per tier
  and drops by 1 for every round you stay put.**
- Shop size grows with tier: starts at 3 offers, **+1 at tiers 2, 4 and 6**.
- *"All minions that are at or below your Tavern Tier level are available to be offered to you in Bob's
  Tavern, with the minions being randomly selected from the shared pool."*

**The economy is the synergy system here.** Tribes are a weaker grouping than TFT traits — most tribal
payoffs live on individual minions rather than in a global trait table — and what carries the format is
the **tempo-versus-economy choice** the tavern price makes explicit: upgrading costs a turn of board
power, and the discount for waiting is exactly 1 gold per round.

### 2.3 Dota Underlords and Super Auto Pets — the two edges of the design space

**Underlords: 23 alliances with 2 / 4 / 6 breakpoints** — Knight, for example, gives 15% / 20% / 25%
reduced physical and magical damage at 2 / 4 / 6
([pcgamesn alliances guide](https://www.pcgamesn.com/dota-underlords/alliances-tier-list-best-synergies) —
second-tier). Structurally the same machine as TFT with a flatter payoff curve.

**Super Auto Pets: no trait table at all.** Team of **5 pets**, bought for **3 gold** each, **10 gold
per turn**, tiers unlock on turn `2X−1` (tier 1 on turn 1, tier 6 on turn 11), and combat resolves
**right to left**, the player's rightmost pet striking the opponent's leftmost
([superautopets.wiki.gg The Basics](https://superautopets.wiki.gg/wiki/The_Basics),
[Pets](https://superautopets.wiki.gg/wiki/Pets) — second-tier). Only one food item per pet, so per-unit
buffs cannot be stacked.

> **INFERENCE.** Super Auto Pets is the control experiment for this whole section: it gets composition
> depth from **ability chains and ordering** — "on faint", "on hurt", "before battle" triggers reading
> neighbours — with **zero** group-membership arithmetic. If a project already has an effect/trigger
> system, that is the cheaper path to composition depth than a trait table, because it reuses machinery
> that exists. The cost is legibility: a trait table shows the player their plan on the HUD, an ability
> chain does not.

---

## 3. Army-wide auras and buffs

### 3.1 How shipped RTS auras are actually specified

Warcraft III is the reference implementation. Liquipedia's mechanics page states the rule in one line:
*"Auras are passive abilities that grant an effect to units in an area around the owner of the ability.
**Auras do not stack with other identical auras.** All auras are positive, and will bestow their
effects to the holder and all allies nearby."*
([liquipedia.net/warcraft/Aura](https://liquipedia.net/warcraft/Aura))

Concrete values (from §1.2 above): **Devotion Aura +2 / +3.5 / +5 armour, radius 90; Endurance Aura
+10/15/20% movement and +5/10/15% attack rate, radius 90.**

That +5 armour is worth reading against the armour curve recorded in
[01-typing-matrices.md](../game-design/01-typing-matrices.md): `reduction = 0.06A / (1 + 0.06A)`.
So a maxed Devotion Aura is **0.06 × 5 / 1.3 = 23.1% damage reduction, army-wide, from one hero ability
learned three times** (computed). That is enormous, and it is exactly why the bounds below exist.

### 3.2 The four bounding devices, each with a shipped example

| Bound | Shipped example | Source |
|---|---|---|
| **Identical auras do not stack** | Warcraft III: two Paladins give one Devotion Aura | Liquipedia (above) |
| **One instance per source** | World of Warcraft Paladin: *"Players may only have one Aura on them per Paladin at any one time."* | [warcraft.wiki.gg Devotion Aura](https://warcraft.wiki.gg/wiki/Devotion_Aura) |
| **Explicit count cap on a stacking bonus** | Age of Empires II Lithuanians: Knight and Leitis lines get **+1 attack per garrisoned relic, up to +4** | [ageofempires.fandom Relic](https://ageofempires.fandom.com/wiki/Relic_(Age_of_Empires_II)) — second-tier |
| **Radius** | Warcraft III auras at radius 90, on a map where armies travel thousands of units | warcraft.wiki.gg ability tables |

Age of Empires II's relic economy is the cleanest "aura as a resource" in the survey: a relic in a
monastery generates **0.5 gold/second = 30 gold/minute**; Aztecs get **+33% (40/minute)**. Same source.
**The relic is a map objective that pays an army-wide dividend, and the Lithuanian version converts
that dividend into a combat aura with a hard cap of four.**

### 3.3 The documented failure: when an aura makes one composition strictly correct

The best-documented case in any shipped game is World of Warcraft's raid-buff system, and it is
documented because Blizzard spent three expansions dismantling it. From the wiki's own history section:

> *"There were **9 standard raid buffs**, which are provided by a number of different abilities.
> **Duplicate abilities with the same effect would not stack**, but you could combine buffs with
> different effects. For example, Mark of the Wild's increased stats did not stack with Legacy of the
> Emperor as both increased stats; but Mark of the Wild did stack with Power Word: Fortitude, as stats
> are a different buff effect to stamina."*
>
> *"When you are in the Proving Grounds, you **automatically receive all 9 buffs** until you leave the
> instance."*
>
> *"Weaker versions of some buffs can be provided by consumable items. While these versions are weaker,
> it is worthwhile to have an inventory of these items when raiding, in case a class capable of
> providing the full version of a particular buff is not available."*

([warcraft.wiki.gg Buff](https://warcraft.wiki.gg/wiki/Buff) — second-tier, but it reproduces the
shipped tooltip logic and the patch history.)

**Read those three passages together and the failure mode and its fixes are on one page:**

1. **The failure.** N distinct non-stacking buffs held by different classes means the correct roster is
   whichever roster covers all N. Composition stops being a choice and becomes a checklist.
2. **Fix one — make duplicates worthless.** If two of the same class add nothing, nobody stacks them.
   That bounds the *magnitude* but makes the *checklist* worse, not better.
3. **Fix two — provide substitutes.** A buff you can buy as a consumable is a buff that no longer
   dictates the roster.
4. **Fix three — grant the whole set by default in tuned content.** Proving Grounds handing out all 9
   is the admission that the content is balanced *assuming* the buffs, so the buffs were never a
   decision at all.

- **Problem an aura solves:** it makes a support unit's value scale with army size, which is the only
  clean way to stop "bring more of the same" being always correct.
- **What it costs the designer:** a membership query every tick, a stacking policy, and a diagnostic
  burden — a player who cannot see why their damage changed will assume a bug.
- **What breaks when tuned wrong:** the checklist above. An aura worth more than a unit is a roster
  requirement wearing a buff icon.

---

## 4. Population, supply and roster caps

### 4.1 The shipped numbers

**StarCraft II** ([liquipedia.net/starcraft2/Resources](https://liquipedia.net/starcraft2/Resources)):

> *"The maximum amount of supply for any player is 200."*

Supply is provided by Supply Depot / Pylon **+8**, Command Center / Nexus **+15**, Hatchery **+6**,
Overlord **+8**. Note the asymmetry: Zerg pays with a *unit* where Terran and Protoss pay with a
building.

**Warcraft III** ([liquipedia.net/warcraft/Food](https://liquipedia.net/warcraft/Food)):

> *"Food cap cannot exceed the food limit of 100."*

Farm **6**, Moon Well / Orc Burrow / Ziggurat **10**. And crucially:

> *"Unlike other resources food does not have to be paid when a unit is trained, but the unit increases
> the current food as long as it is alive."*

**Age of Empires II** puts population in a resource slot rather than on the unit at all — see
[02-unit-variables.md](../game-design/02-unit-variables.md), which records *"AoE2 has no pop field (it
is a resource slot)"*, and that Total War has no supply concept whatsoever.

### 4.2 Upkeep — the exact penalty

Warcraft III taxes **gold income**, not the army:

| Tier | Food | Gold income |
|---|---|---:|
| No Upkeep | 0–50 | **100%** |
| Low Upkeep | 51–80 | **70%** |
| High Upkeep | 81+ | **40%** |

Blizzard's own page ([classic.battle.net/war3/basics/upkeep.shtml](http://classic.battle.net/war3/basics/upkeep.shtml))
and [liquipedia.net/warcraft/Upkeep](https://liquipedia.net/warcraft/Upkeep) agree exactly. Liquipedia
adds two details worth having:

> *"Upkeep is the **taxation of gold income**."*
>
> *"…gold income from Creeping, selling items and Transmute … **is also reduced by upkeep**."*

In *Reign of Chaos* the thresholds were **10 lower** (0–40 / 41–70 / 71–90) with the same percentages —
second-tier, from the same wiki family.

**This is the most transferable idea in the section. There is no hard army cap; there is a penalty
curve with two knees.** A player who wants a bigger army may have one, and pays 30% or 60% of their
income for it. The ceiling is economic, soft and configurable.

### 4.3 What supply cost expresses that raw stats cannot

[02-unit-variables.md](../game-design/02-unit-variables.md) already establishes the key measurement:
DPS per 100 mineral-equivalents spans **13×** across SC2 units (Zergling 40.2, Colossus 3.1), but
**effective HP per supply stays in a 45–83 band** — roughly 1.8×.

> **INFERENCE.** Cost measures how hard a unit is to acquire. Supply measures how much of your finite
> board and attention it consumes. Those are different questions, and shipped games answer them with
> different numbers on purpose. Supply is the axis on which "is this unit efficient *at the cap*" is
> decided, and it only bites once the cap is reached — which is why every RTS has one, and why the
> number is always small enough to reach. A 200-supply cap nobody reaches is not a cap, it is a
> comment.

- **Problem it solves:** it makes the composition question exist at all. Without a cap, "more of
  everything" answers every board.
- **What it costs:** one integer per unit and one global sum. Trivially cheap — which is why it is
  universal.
- **What breaks when tuned wrong:** supply costs that track gold cost make supply redundant. Supply
  costs that ignore board footprint make one unit strictly dominant at the cap.

---

## 5. Production and economy pacing

### 5.1 The shipped base, in numbers

StarCraft II, from [liquipedia.net/starcraft2/Resources](https://liquipedia.net/starcraft2/Resources):

| Thing | Value |
|---|---|
| Normal base | **8 mineral fields**: 4 × 1800 + 4 × 900 = **10,800 minerals** |
| Gas | **2 geysers × 2,000 = 4,000** |
| Ratio | *"a total of 10,800 minerals and 4,000 vespene gas in a ~8:3 ratio (2.7 minerals per gas)"* |
| Worker trip | **5 minerals**, **4 gas** |
| MULE | **25 minerals per trip** |
| Rich fields | **7 per trip**; a gold base is 6 fields, 8,100 total |

### 5.2 The worker curve, stated by the game

From [liquipedia.net/starcraft2/Mining_Minerals](https://liquipedia.net/starcraft2/Mining_Minerals):

> *"The progression of minerals harvested per second is roughly **linear** … **until you have 2
> harvesters per patch**."*
>
> *"Keeping more than **3 workers per patch is a complete waste**. The 4th worker will not give you any
> additional income."*
>
> *"…any patch with 3 workers will yield exactly the same rate regardless of distance. This is because
> the limiting factor becomes the time 'on' the mineral patch."*

So on an 8-patch base: **16 workers is the linear region, workers 17–24 are the diminishing region, and
worker 25 is worth nothing.** A three-segment piecewise curve with published knees.

### 5.3 Why RTS economies feel exponential early and linear late

> **INFERENCE, but the mechanism is visible in the numbers above.** Early, a worker builds workers, so
> income compounds: worker count is the derivative of worker count. That is exponential by
> construction. It stops the moment a base saturates, because the 17th worker on an 8-patch base earns
> strictly less than the 16th and the 25th earns nothing. From then on income grows only when you take
> another base — a step, not a slope. **The "exponential early, linear late" shape is not a designed
> curve; it falls out of a per-patch throughput limit meeting a self-replicating worker.**

The tension that produces is also on that page: workers transferred to a new base spend 15–18 seconds
not mining, *"generally about 10 minerals per worker"*, and Liquipedia publishes a break-even table for
the transfer (from 12 to 45 minerals/minute breaks even in 20.45 s; from 24 to 45 in 32.14 s).
**Every economic action costs army now for army later, and the game publishes the exchange rate.**

- **Problem it solves:** it puts a clock on greed. Expanding is correct *and* it is the moment you are
  weakest.
- **What it costs:** a throughput model per resource node, and a UI that shows saturation, or players
  cannot see the knee.
- **What breaks when tuned wrong:** if saturation is too soft, macro play has no ceiling and the
  richest player simply wins; too hard, and expanding is mandatory on a fixed timer, which is not a
  decision.

---

## 6. Tech trees and unlock gating

### 6.1 Age of Empires II — the measured tree

All figures **computed** from the shipped data behind
[aoe2techtree.net/data/data.json](https://aoe2techtree.net/data/data.json) (926 KB, retrieved
2026-09-02), which is a direct dump of the game's `Tech` / `Unit` / `Building` records.

**Age-up costs and research times:**

| Age | Cost | Research time |
|---|---|---:|
| Feudal (internal name `Middle Age`, id 101) | 500 food | 130 s |
| Castle (internal name `Feudal Age`, id 102) | 800 food + 200 gold | 160 s |
| Imperial (id 103) | 1000 food + 800 gold | 190 s |

**The internal names are off by one age** — id 102 is labelled "Feudal Age" and costs 800F/200G, which
is the Castle Age price. Anyone reading these files as a balance source must key on the id, not the
name. Same class of trap as the Warcraft III `ATTACK_TYPE_NORMAL` naming recorded in
[01-typing-matrices.md](../game-design/01-typing-matrices.md).

**Breadth of the tree** (computed):

| Measure | Value |
|---|---:|
| Civilizations | **53** |
| Global pools | 194 techs · 245 units · 40 buildings · 115 unit upgrade edges |
| Techs available per civ | min 61 · max 79 · **mean 70.8** |
| Units per civ | min 43 · max 55 · **mean 50.1** |
| Buildings per civ | min 23 · max 30 · **mean 27.4** |

**Every civilization gets 36% of the tech pool and 20% of the unit pool, and the spread between
civilizations is only about 1.3×.** Differentiation comes from *which* 70 techs, not from how many.
That is the same conclusion [02-unit-variables.md](../game-design/02-unit-variables.md) reached about
counter graphs: the interesting content is the membership list, not the size.

**Cost growth is sub-exponential**: 500 → 1000 → 1800 total resources across three age-ups, roughly
doubling, while research time grows 130 → 160 → 190, an arithmetic +30 s. **Money scales, time does
not.**

### 6.2 In-match trees versus campaign trees

| | Match tech tree (AoE2, SC2) | Campaign/meta tree (CoH commanders, SC2 mastery) |
|---|---|---|
| Reset | Every match | Never |
| Currency | In-game resources and time | Match outcomes and levels |
| Branching | Wide and mostly re-affordable | Narrow and exclusive |
| What it paces | The arc of one match | The arc of a player's account |
| Shape | AoE2: 4 ages × ~70 techs | CoH3: 4 tiers × 2 choices = **16 paths from 10 abilities** (computed) |

> **INFERENCE.** These do different jobs, and using one shape for the other job is the common mistake.
> A match tree needs to be *re-walkable* — you take the same route most games and the interest is in
> the timing. A campaign tree needs to be *exclusive* — the interest is that you cannot have
> everything, so the choice must be permanent or expensive to reverse. StarCraft II Co-op runs both at
> once and keeps them strictly separate: commander levels 1–15 are the campaign tree, prestige is the
> exclusivity knob, and the in-mission tech tree is untouched by either.

---

## 7. Formation, positioning and lane adjacency

### 7.1 Darkest Dungeon's 4-slot rank system — measured, and the closest analogue to a lane

The most transferable system in this document, and small enough to specify completely.

**The rule.** A party of four occupies ranks 1–4 (1 = front). **Every skill carries two masks: the set
of ranks it can be *used from*, and the set of enemy ranks it can *reach*.** A skill whose owner stands
in an illegal rank is greyed out. Enemy parties use the same four ranks mirrored.

**Measured across all 18 base-game heroes** — computed from the shipped `rank=` and `target=` fields on
110 hero-ability records, retrieved via the `darkestdungeon.wiki.gg` MediaWiki API
(`action=parse&prop=wikitext`); example page
[Vestal](https://darkestdungeon.wiki.gg/wiki/Vestal_(Darkest_Dungeon)):

| Launch mask | Skills | Share |
|---|---:|---:|
| 1234 (anywhere) | 34 | **31%** |
| 12 | 21 | 19% |
| 34 | 20 | 18% |
| 234 | 14 | 13% |
| 123 | 11 | 10% |
| 23 | 5 | 5% |
| 1 | 5 | 5% |

**Seven distinct launch masks out of 15 possible non-empty subsets, and every one is a contiguous
run.** There is no skill usable from ranks 1 and 4 but not 2 and 3. The same holds on the other side:
**13 distinct target masks, all contiguous** (computed). Mean legal launch positions per skill:
**2.80 of 4**.

Legality is not uniform across the four slots (computed):

| Rank | Skills legal from it | Offensive skills that can reach it |
|---|---:|---:|
| 1 (front) | 71 / 110 = 65% | 79 / 100 = 79% |
| 2 | 85 = **77%** | 88 = **88%** |
| 3 | 84 = 76% | 76 = 76% |
| 4 (back) | 68 = **62%** | 57 = **57%** |

**Rank 4 is the safest and the least capable, by design and by exactly measurable amounts** — a back
unit can use 62% of the skill space and can be hit by only 57% of it.

**Position is itself attackable.** 21 of 110 hero skills (**19%**) move somebody: 9 self-`Forward`,
7 `Knockback`, 5 self-`Back`, 3 `Pull`, 2 `Shuffle` (computed). Concrete rows straight from the data:

| Hero | Skill | Launch ranks | Target ranks | Displacement |
|---|---|---|---|---|
| Vestal | Mace Bash | 1, 2 | 1, 2 | — |
| Vestal | Judgement | 3, 4 | any single of 1–4 | — |
| Vestal | Dazzling Light | 2, 3, 4 | 1, 2, 3 | — |
| Vestal | Divine Comfort | 2, 3, 4 | all four allies at once | — |
| Bounty Hunter | Come Hither | — | — | **Pull 2** (100% base) |
| Bounty Hunter | Uppercut | — | — | **Knockback 2** + Stun |
| Bounty Hunter | Flashbang | — | — | **Shuffle Single** |
| Abomination | Slam | — | — | **Knockback 2**, −10 Dodge, −2 Speed |
| Highwayman | Duelist's Advance | — | — | **self Forward 1**, activates Riposte |
| Grave Robber | Shadow Fade | — | — | **self Back 2**, Stealth, +80% damage |

**The design consequence.** A kit is only usable from the ranks it was written for, so *ordering the
party is a build decision made before the fight and defended during it*. Knockback and shuffle are not
damage — they are **attacks on the opponent's legality mask**, a category of counterplay a stat-only
combat model cannot express.

- **Problem it solves:** it turns a roster of four into a *sequence* of four, multiplying the decision
  space at zero content cost.
- **What it costs the designer:** two 4-bit masks per skill, a rule for what happens in an illegal
  rank, and UI showing both masks. Genuinely cheap.
- **What breaks when tuned wrong:** if too many skills are `1234`/`1234`, position stops mattering —
  31% fully flexible appears to be near the ceiling. If too few are, a single shuffle bricks a party
  and the mechanic reads as unfair rather than tactical.

### 7.2 Fire Emblem and Total War

Fire Emblem's weapon triangle values by game are already tabulated in
[01-typing-matrices.md](../game-design/01-typing-matrices.md) §3 (FE7/FE8 ±15 Hit / ±1 Might; Engage
replaces the modifier entirely with the **Break** status, which removes the ability to counterattack).
**Not repeated here.** The point that belongs in *this* document is structural: Fire Emblem layers a
*positional* system — terrain bonuses, adjacency support bonuses, and the paired-unit mechanic in
Awakening and Fates — on top of that triangle, so the same triangle produces different answers
depending on where the units stand. **The triangle is the vocabulary; position is what makes it a
decision.**

Total War's formations and the general's leadership aura are real and central, but **CA has never
published the numbers** — the same conclusion [01-typing-matrices.md](../game-design/01-typing-matrices.md)
already reached about their damage formula. Two dedicated guides were checked and contain no numeric
values at all ([gamepressure](https://www.gamepressure.com/total-war-warhammer-2/leadership-and-exhaustion/zba33d),
[ludo.guide](https://www.ludo.guide/guide/total-war-warhammer-3/morale-leadership)); both tell the
player to read the in-game tooltip instead. What is uncontested and worth recording qualitatively:

- Units have a **Leadership** pool; at zero they rout and stop taking orders.
- **The commanding lord projects a leadership aura whose radius can be increased by skills on the
  lord's own tree** — the aura is itself a progression target, which is the closest thing in this
  survey to a commander whose *reach* levels up rather than whose numbers do.
- **Rout is contagious.** Nearby routing allies push others down, which is what makes a general's death
  army-ending rather than merely expensive.

See *What I could not find* for the specific values.

---

## 8. Reinforcement, reserves and attrition across battles

### 8.1 What persistence costs, in shipped numbers

| System | Rule | Source |
|---|---|---|
| **Warcraft III hero death** | Revive costs *"approximatively half the cost for building the Hero plus 10% more per level"*, capped at **550 gold** (Altar) or **1105 gold + 260 lumber** (Tavern). The hero keeps its level and items. | [classic.battle.net heroes](http://classic.battle.net/war3/basics/heroes.shtml) |
| **Company of Heroes reinforcement** | Reinforce cost is stored **as a percentage of the entity's own cost**, so a squad rebuilds at a fraction of the squad price. | [02-unit-variables.md](../game-design/02-unit-variables.md) |
| **Company of Heroes veterancy** | **3 ranks across 651 squads** — a shallow ladder applied very widely. | [02-unit-variables.md](../game-design/02-unit-variables.md) |
| **Dawn of War II Last Stand** | Heroes are *"incapacitated instead of killed, and the game only ends when all three heroes are simultaneously incapacitated."* | [dow.fandom](https://dow.fandom.com/wiki/The_Last_Stand/Introduction) — second-tier |
| **XCOM 2 recovery** | The Advanced Warfare Center *"increases soldiers' healing rate by +50% (+100% with an assigned engineer)"*; a respec makes a soldier *"unavailable for 5 days"*. | [ufopaedia.org AWC (XCOM2)](https://www.ufopaedia.org/index.php/Advanced_Warfare_Center_(XCOM2)) — second-tier |

### 8.2 What persistence does to risk-taking

> **INFERENCE**, but the shipped rules point one way consistently.
>
> **Persistence converts a tactical decision into a strategic one by making the cost of a loss outlive
> the fight.** Four dials appear across the survey and they produce very different play:
>
> 1. **Priced death (Warcraft III).** The hero returns, keeps everything, and you pay gold plus the
>    walk back. **Risk-taking stays high** because the downside is bounded and denominated in a
>    resource you are already spending. Note the cap — 550 gold — which means a level-10 hero dying is
>    *not* proportionally worse than a level-6 hero dying. That is a deliberate refusal to make
>    late-game death catastrophic.
> 2. **Priced time (XCOM).** The soldier survives but is unavailable for days. **Risk-taking drops
>    sharply**, because the cost is paid in the one resource the player cannot generate — roster
>    availability during the *next* mission. This is what produces the familiar death spiral of
>    fielding a B-team and losing worse.
> 3. **Group-state failure (Last Stand).** Incapacitation with a party-wide fail condition means an
>    individual loss is survivable and *only the group state matters*, so the player is encouraged to
>    take risks with a single hero while still being punished for the team collapsing. **Persistence
>    without a resource cost** — the cleverest of the four.
> 4. **No refund (Fire Emblem permadeath, Battle Brothers).** **Risk-taking collapses**, and the
>    observed player response is not caution but *reloading* — the mechanic converts risk into
>    save-scumming rather than tension, which is why Fire Emblem eventually shipped an opt-out mode.
>
> **The pattern: persistence is worth having exactly to the extent that the thing lost is recoverable
> by spending something the player controls.** Gold, yes. Time in a scheduler, cautiously. Nothing, and
> you have designed a reload button.

---

## What I could not find

Non-empty by requirement, and honestly so — several of these are gaps in what studios publish, not gaps
in searching.

1. **Total War morale and formation numbers.** No published values for leadership modifiers, the
   wavering/routing thresholds, or the general's aura radius. Two dedicated guides
   ([gamepressure](https://www.gamepressure.com/total-war-warhammer-2/leadership-and-exhaustion/zba33d),
   [ludo.guide](https://www.ludo.guide/guide/total-war-warhammer-3/morale-leadership)) contain no
   numbers and refer the player to the in-game tooltip. Consistent with
   [01-typing-matrices.md](../game-design/01-typing-matrices.md)'s finding that CA has never published
   its damage formula either.
2. **Total War unit veterancy chevrons.** The widely repeated "9 chevrons" figure and the per-rank stat
   grants could not be confirmed against any source that survived a fetch. Fandom-hosted wikis
   (`totalwar.fandom.com`, `battlebrothers.fandom.com`, `feheroes.fandom.com`) sit behind a challenge
   page and returned HTTP 403/402 to every attempt; `honga.net` exposes unit tables but no experience
   table. **Treat any chevron number seen elsewhere as unverified.**
3. **XCOM 2 wound-duration formula.** The relationship between HP lost and days in the infirmary is a
   config-driven curve; I found the Advanced Warfare Center's healing-rate modifiers but not the base
   formula, its floor, or its ceiling. `ufopaedia.org` has no `Wounds (XCOM 2)` page.
4. **Battle Brothers injury and permadeath specifics.** No accessible source; the Steam store API
   returned an empty body on two attempts and the game's wiki is Fandom-hosted.
5. **Which Fire Emblem introduced Casual Mode**, and the exact revival rules per game. `serenesforest.net`
   is reachable but I did not locate the specific page, and the Fire Emblem wikis are blocked.
6. **Current Hearthstone Battlegrounds tribes-per-lobby count.** The documented rule ("5 of the 6 active
   tribes") dates from when six tribes existed; with ten shipped, the current subset size is
   patch-dependent and unconfirmed. The two tavern-upgrade cost tables found disagree
   (5/7/8/11/11 versus 5/7/9/11/13/15) and at least one is stale.
7. **A first-party Riot statement on why 2/4/6 was chosen.** The breakpoint *data* is authoritative
   (Community Dragon is Riot's own export), but no developer post explaining the choice was found, so
   the reasoning in §2.1 is read off the data rather than quoted.
8. **Warcraft III creep-XP diminishing table.** The World Editor constant appears as `80, 70, 60, 50, 0`
   but I could not establish whether it indexes hero level or level *difference*, so it is not used as
   a fact anywhere above.
9. **Final Fantasy Tactics directional hit-rate bonuses.** `ffhacktics.com`, the datamining reference
   for that game, returned HTTP 403. Classic Final Fantasy front/back-row damage halving is likewise
   unsourced here, so §7 covers front/back-row systems through Darkest Dungeon only.
10. **StarCraft II tech-building branch counts.** Liquipedia has per-building pages but no consolidated
    tech-tree table comparable to the Age of Empires II data dump, so §6 quantifies AoE2 and describes
    SC2 only qualitatively.
11. **The 18-commander count is current as of patch 5.0 (2020)**, quoted from Blizzard's notes. I did
    not verify that no commander has shipped since.

---

## Hooks for this project

**Non-normative, un-vetted, and explicitly not a design proposal.** These are observations about where
this survey touches the shape of a summoner commanding demons on a 5-lane, 9-column lawn. None of it
has been checked against the codebase, the design gate, or the existing specs, and none of it is a
recommendation.

- **The commander layer is the summoner.** Every system in §1 is a ruleset over an unchanged roster.
  StarCraft II Co-op's split is worth noting as a *shape*: per-commander levels that unlock, an
  account-wide point pool with forced either/or choices, and an exclusivity layer that pairs every
  bonus with a written drawback.
- **Bonus-with-drawback is the cheapest power-budget device found.** All 54 prestige talents do it, and
  two of them delete the commander's own hero unit. It bounds power without a cap and without a number.
- **Two group memberships per unit is the measured norm** — not three and not one; 65% of TFT Set 18
  champions carry exactly two. A demon species with element *and* one other membership sits exactly on
  that norm.
- **Step-function payoffs, not slopes.** Between breakpoints a unit adds nothing; at a breakpoint it
  adds everything. Per-member value stays roughly flat (Brawler: 12.5% → 10.0% → 10.8%) — the drama is
  entirely in the discontinuity.
- **A finite shared pool is what stops convergence in TFT** — 30 copies of each 1-cost across eight
  players. Any synergy system without scarcity has a single correct answer.
- **The four aura bounds in §3.2 read as a checklist**: identical auras never stack, one instance per
  source, an explicit count cap on anything that does stack, and a radius. The WoW nine-buff history is
  the documented failure of ignoring them.
- **Upkeep is a soft ceiling with two knees, not a cap** — 100% / 70% / 40% of gold income at 0–50 /
  51–80 / 81+ food. That shape is compatible with a no-hard-ceilings position in a way a supply cap is
  not.
- **Darkest Dungeon's rank system is the most directly transferable thing here, and a 9-column lane is
  a strictly larger version of its 4 slots.** The measured properties are the interesting part: masks
  for both *where a skill may be used from* and *what it may reach*; every mask a contiguous run; about
  a third of skills fully flexible; the back slot safest and least capable by measurable margins; and
  ~19% of skills existing to move somebody — that is, to attack the opponent's legality mask rather
  than their health.
- **Progression that grants options instead of magnitude** (Last Stand: levelling adds wargear, never
  base stats) is the only mechanism found that lets a new and a veteran participant coexist without a
  power gap.
- **Persistence is worth exactly as much as the recovery currency the player controls.** Warcraft III
  prices hero death in gold *and caps it*, so a level-10 death is not catastrophic. XCOM prices it in
  scheduler time and gets cautious play. Permadeath prices it in nothing and gets reloading.
