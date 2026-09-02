# 04 — The PvZ franchise siblings, read as experiments in adding depth to the lawn

> Research note. Prior art only. Nothing here is a proposal, a spec, or a decision for this repo.
> Status: research · Written 2026-09-02 · Scope: every shipped Plants vs. Zombies title outside PVZRH.

## The finding in one paragraph

Across seventeen years the franchise ran the same experiment eight times: take a tight, closed
tower-defense loop and bolt a progression system onto it. **The systems that survived are the ones
that added *vocabulary* — new nouns the board could hold — and the ones that died are the ones that
added *ownership curves* — numbers that go up because you played longer.** PvZ 1 shipped with almost
no persistence and is the best-tuned game in the family; its only durable meta is the Zen Garden, a
coin faucet gated by a real-time timer, and nothing in its shop is a stat. PvZ Heroes, the most
RPG-shaped entry, got there not by giving cards levels but by publishing a **closed keyword
vocabulary of ~17 traits** and a **closed class lattice of exactly C(5,2)=10 pairs per side** — and
its rarity tiers, measured against the actual datamined card table, buy **rules text, not stat
efficiency** (stat-per-sun is flat at 1.47–2.29 across all six rarities while rules text grows 20 →
70 characters). Garden Warfare answered "how do we make 100 units from 8" with a **stable ~7–9
authored variants per base class**, then deleted the entire layer in Battle for Neighborville and
replaced it with a 7-point modifier budget — and the modifier budget balanced *worse*. Every title
that added an ownership system on top — Adventures' crop timers, PvZ Online's ten parallel plant
upgrade tracks, All Stars' star ranks and gear, PvZ 2's seed-packet levels and M200 Mastery — either
shut down or drew the franchise's most sustained criticism; PvZ 2's own competitive mode **strips the
upgrades back out**. The pattern is blunt: **depth that changes what a unit *does* compounds; depth
that changes what a unit's *number* is inflates, and then has to be excluded, rebalanced or
abandoned.** The one thing the franchise never successfully changed is the match itself — PvZ 3 has
spent seven years and six public builds failing to move the lawn.

---

## How to read this

| Mark | Means |
|---|---|
| **FACT** | Sourced. URL inline. |
| **FACT [2nd-tier]** | Community wiki prose. Reliable for structure, weaker for exact numbers. |
| **(computed)** | I tallied or derived it myself from data in this document. |
| **INFERENCE** | My reading. Not in any source. |

**Source tiering used here.** First tier: shipped game data and reverse-engineered source
(`dnartz/PvZ-Emulator`, the PvZ Heroes datamined card table, MediaWiki raw wikitext pulls), official
EA/PopCap pages and manuals, named-developer interviews, contemporary press. Second tier:
`plantsvszombies.wiki.gg` — the wiki.gg mirror is fetchable; `plantsvszombies.fandom.com` returns
HTTP 402 throughout and was reachable only via search snippets.

**Two primary datasets do most of the numeric work below:**

1. **`dnartz/PvZ-Emulator`** — a reverse-engineered C++ reimplementation of PvZ 1's Survival Endless.
   `system/spawn.cpp`, `object/projectile.cpp` and `system/zombie/common_zombie.cpp` carry the
   original game's constants verbatim. https://github.com/dnartz/PvZ-Emulator
2. **`dannyguy253/PvZHeroes-Database`** — the complete PvZ Heroes card table (503 collectible cards +
   58 superpowers), community-compiled from the `card_data_173` asset bundle. I downloaded and
   parsed it; every "(computed)" number in §2 comes from that parse.
   https://github.com/dannyguy253/PvZHeroes-Database

**Three corrections to widely-repeated claims**, established below and flagged here because they
change how the record reads: PvZ Adventures ran **17 months**, not 5. PvZ Online launched
**July 2015** and ran to **August 2018**. **PvZ 3 has never had a global launch** — as of
September 2026 it is still in soft launch, in Ireland and the Philippines.

---

## 1. Plants vs. Zombies (2009) — the baseline that needed no progression

**FACT** — 49 plants, 26 zombies, 50 Adventure levels across 6 areas, 20 mini-games (28 counting
version exclusives), 20 puzzle levels, 11 survival levels.
https://en.wikipedia.org/wiki/Plants_vs._Zombies_(video_game) ·
https://plantsvszombies.wiki.gg/wiki/Adventure_Mode · https://plantsvszombies.wiki.gg/wiki/Survival_Mode

### 1.1 The sun economy, exactly

| Quantity | Value | Source |
|---|---|---|
| Starting sun, ordinary level | **50** | https://plantsvszombies.wiki.gg/wiki/Sun |
| Sky sun drop interval (day only) | **~10 s** | same |
| Sky sun value | **25** | same |
| Sunflower cost | **50** | same |
| Sunflower first sun | **25, after 7 s** | https://plantsvszombies.wiki.gg/wiki/Sunflower_(PvZ) |
| Sunflower steady rate | **25 every 24 s** | same |
| Twin Sunflower | **50 every 24 s**; costs 150 *on top of* the 50 Sunflower under it | same |
| Sun-shroom | **15** small, grows to 25 | https://plantsvszombies.wiki.gg/wiki/Sun |
| Peashooter | **100** | same |
| Puff-shroom | **0** | same |
| Cob Cannon | **500** | same |

**The payback arithmetic (computed).** A Sunflower costs 50 and returns 25 every 24 s after a 7 s
warm-up. It pays for itself at **t ≈ 31 s** and clears a Peashooter's 100 sun at **t ≈ 103 s**. A
Twin Sunflower costs 150 on top of the 50 already spent — 200 sun total for **+25 per 24 s** over
the plain Sunflower — so the *upgrade* pays back in **144 s**, roughly 4.7× slower than the base
plant. That is the entire two-tier upgrade economy in one number: **the upgrade tier is deliberately
a bad deal early and a good deal only in modes that run long.**

**What problem it solves for the player.** One resource, one visible number, one decision every few
seconds: economy now or defense now. Nothing to read.
**What it costs the designer.** Everything is coupled. Changing the Sunflower changes every level.
**FACT** — PopCap hit exactly this. Playtesters did not understand sun, so the team **halved the
Sunflower's cost, which forced a rebalance of every plant-zombie interaction in the game**; George
Fan judged it "worth the effort." https://en.wikipedia.org/wiki/Plants_vs._Zombies_(video_game)
**What breaks when tuned wrong.** Too cheap and the correct opening is always max economy, which
deletes the decision. Too expensive and the first rush kills you before you have an engine.

### 1.2 The board and the seed slot economy

**FACT [2nd-tier]** — the lawn is **5 rows**; the Pool adds two water rows, the Roof changes
trajectories. Adventure Mode starts the player with **6 seed slots**.
https://plantsvszombies.wiki.gg/wiki/Seed_slot

**FACT [2nd-tier]** — slots 7–10 are purchased from Crazy Dave's Twiddydinkies:

| Slot | Price | Multiple over previous (computed) |
|---|--:|--:|
| 7 | **$750** | — |
| 8 | **$5,000** | **6.7×** |
| 9 | **$20,000** | **4.0×** |
| 10 | **$80,000** | **4.0×** |

https://plantsvszombies.wiki.gg/wiki/Crazy_Dave's_Twiddydinkies

**The finding: the deck-size axis is priced geometrically, at a ratio of 4.** (computed) The four
slots cost **$105,750 in total** — more than every upgrade plant combined. PvZ 1's designers treated
"how many tools may I bring" as the most expensive thing a player can buy, and priced the tenth slot
as an endgame trophy.

**What problem it solves.** Slot scarcity is what makes 49 plants interesting. With ten slots and 49
plants you are always leaving something out, so every level is a draft.
**What it costs the designer.** Every plant must be worth a slot against every other plant, in every
level layout. This is the hardest constraint in the game and the reason the roster stops at 49.
**What breaks when tuned wrong.** Give slots away and the roster collapses into one dominant
loadout; the puzzle disappears and the other 40 plants become decoration.

### 1.3 The shop and the coin loop

**FACT [2nd-tier]** — the complete Twiddydinkies inventory, all prices:
https://plantsvszombies.wiki.gg/wiki/Crazy_Dave's_Twiddydinkies

| Category | Items and prices |
|---|---|
| Seed slots | 7: $750 · 8: $5,000 · 9: $20,000 · 10: $80,000 |
| Row defenses | Garden Rake $200 · Pool Cleaner $1,000 · Roof Cleaner $3,000 |
| Upgrade plants | Gold Magnet $3,000 · Gatling Pea $5,000 · Twin Sunflower $5,000 · Gloom-shroom $7,500 · Spikerock $7,500 · Cattail $10,000 · Winter Melon $10,000 · Cob Cannon $20,000 |
| Zen Garden | Wheelbarrow $200 · Fertilizer ×5 $750 · Gardening Glove $1,000 · Bug Spray ×5 $1,000 · Marigold sprout $2,500 · Tree Food $2,500 · Golden Watering Can $10,000 · Tree of Wisdom $10,000 · Phonograph $15,000 · Mushroom Garden $30,000 · Aquarium Garden $30,000 |
| Misc | Wall-nut First Aid $2,000 · Imitater $30,000 |

**(computed)** The eight upgrade plants total **$68,000**. The four seed slots total **$105,750**.
The Zen Garden's one-time items total **$96,700**. **The non-combat garden costs almost as much as
the entire combat upgrade path** — a deliberate second sink that does not touch balance.

**FACT [2nd-tier]** — the faucet: on a second Adventure playthrough the final zombie of each level
drops **$250** ($100 for level 4-5). Vasebreaker levels pay **5 gold coins** first time, **2** on
repeats. https://plantsvszombies.wiki.gg/wiki/Adventure_Mode ·
https://plantsvszombies.wiki.gg/wiki/Vasebreaker

**INFERENCE.** The coin loop is not a progression system. It is a *pacing* system: it gates when the
upgrade plants and the tenth slot become available, and it gives finished players a reason to replay
solved levels. Nothing in the shop makes a level winnable that was not winnable before — the upgrade
plants are convenience and Survival enablers. **That is why PvZ 1's balance survives: the shop cannot
break Adventure Mode, because Adventure Mode is beatable before you can afford anything in it.**

### 1.4 The two-tier upgrade system

**FACT [2nd-tier]** — eight upgrades, each consuming a specific base plant already on the lawn:

| Upgrade | Base | In-level sun | Shop | Effect over base |
|---|---|--:|--:|---|
| Gold Magnet | Magnet-shroom | 50 | $3,000 | collects coins instead of metal |
| Spikerock | Spikeweed | 125 | $7,500 | 2 damage/round vs 1; survives being run over |
| Twin Sunflower | Sunflower | 150 | $5,000 | 2 sun/glow vs 1 |
| Gloom-shroom | Fume-shroom | 150 | $7,500 | 4 AoE bursts into all adjacent tiles |
| Winter Melon | Melon-pult | 200 | $10,000 | heavy splash + slow |
| Cattail | Lily Pad | 225 | $10,000 | 2 homing spikes, any lane, hits balloons |
| Gatling Pea | Repeater | 250 | $5,000 | 4 peas/round vs 2 |
| Cob Cannon | 2× Kernel-pult | 500 | $20,000 | reusable Cherry Bomb, ~36.4 s recharge |

https://plantsvszombies.wiki.gg/wiki/Upgrade_plants

**The finding: this is a *tile-compression* mechanic, not a power mechanic.** (INFERENCE, grounded in
the sun costs.) Gatling Pea costs 250 plus the 200 already sunk in two Repeaters = 450 sun for four
peas per round — which is what 450 sun of plain Repeaters already gives, but in **one tile instead of
two**. On a 5×9 board where the real currency is space, doubling density per tile is the only way to
raise a ceiling without raising a number. Cob Cannon is the extreme case: it eats **two** Kernel-pult
tiles and 500 sun to become the only *reusable* instant-kill in the game.

**What problem it solves.** Late-game boards run out of tiles before they run out of sun. Upgrades
convert surplus sun back into board space.
**What it costs the designer.** Every upgrade needs its base plant to still be worth planting alone,
or the base degrades into a crafting reagent.
**What breaks when tuned wrong.** Survival Endless shows it directly — **FACT [2nd-tier]** each time
an upgrade plant is placed its cost rises by **50 sun**, falling by 50 if removed.
https://plantsvszombies.wiki.gg/wiki/Survival:_Endless — that escalator exists because without it the
optimal Endless board is "every tile is the same upgrade plant," and the mode has no decisions left.

### 1.5 Survival Endless — how it actually scales, from the reversed source

The most instructive part of PvZ 1 for anyone building an endless mode, and the wikis get it wrong.
Everything in this subsection is **FACT**, read from `dnartz/PvZ-Emulator`.

**Structure** (`system/spawn.cpp`):
- `MAX_WAVES = 20` — a level is 20 waves; the flag counter increments at wave 10.
- `MAX_ZOMBIES = 50` — **every wave is exactly 50 zombies.** Not a point budget. A flat 50.
- `MAX_ZOMBIE_TYPES = 9` — nine zombie types are enabled per level, drawn **without replacement and
  with equal weight** from the scene-eligible pool. Basic Zombie and Yeti are always on; Conehead is
  on with probability 4/5, else Newspaper (`rng.randint(5)`).
- Waves **9 and 19** are flag waves: 8 basic zombies plus a Flag Zombie are prepended, then the
  remaining 41 slots fill from the weighted pool.

**The spawn weight table** (`ZOMBIE_SPAWN_WEIGHT`, with the two divisor special-cases in
`get_spawn_weight`), mapped through the `zombie_type` enum in `object/zombie.h`:

| Zombie | Effective weight | Zombie | Effective weight |
|---|--:|---|--:|
| Basic Zombie | **400** (4000 ÷ 10) | Balloon | 2000 |
| Conehead | **1000** (4000 ÷ 4) | Dolphin Rider | 1500 |
| Screen Door | **3500** | Catapult | 1500 |
| Buckethead | 3000 | Gargantuar | 1500 |
| Pole Vaulting | 2000 | Newspaper | 1000 |
| Football | 2000 | Dancing | 1000 |
| Snorkel | 2000 | Jack-in-the-Box | 1000 |
| Zomboni | 2000 | Digger / Pogo / Bungee / Ladder | 1000 each |
| Giga-gargantuar | **1000** (hard-set) | Yeti | **1** |

**The finding, and it is counter-intuitive: the basic zombie has the *lowest* weight of any enabled
type (400), and Conehead the second lowest (1000).** (computed, from the divisors in
`get_spawn_weight`.) Endless is not "more zombies" — it is **50 elite zombies per wave, forever**.

**Wave pacing is HP-driven, not timer-driven.** From `next_spawn_countdown_update`:

| Condition | Countdown to next wave |
|---|---|
| Normal wave | `rng.randint(600) + 2500` ticks = **25.00–30.99 s** |
| Flag wave (9, 19) | **4500 ticks = 45 s**, no early trigger |
| Final wave (20) | **5500 ticks = 55 s**, then a 500-tick endgame |
| **Early trigger** | when surviving wave HP ≤ `rng.randfloat(0.50, 0.65) × wave initial HP`, and ≥400 ticks elapsed, and >200 remaining → **countdown snaps to 200 ticks (2 s)** |

`get_current_hp` sums, for the current wave only: zombie HP + first accessory HP + **0.2 ×** second
accessory HP + 20 if ballooned.

**This is the single most transferable mechanism in PvZ 1.** (INFERENCE.) The game measures how fast
you are clearing and hands you the next wave the instant you have killed 35–50% of the current one,
with the exact threshold rerolled per wave. A strong player is not rewarded with idle time — they are
rewarded with **compression**. A weak player gets the full 25–31 s. Difficulty is self-adjusting
without a single stat being scaled, and the randomised threshold stops the pattern being memorised.

**Base numbers** (`common_zombie.cpp`, `object/projectile.cpp`): basic zombie **HP 270**; pea damage
**20**; fire pea **40**; melon **80**; Cob Cannon **300**; full `DAMAGE[14]` table
`{20,20,40,80,20,80,40,20,20,75,20,300,40}`. **(computed)** a basic zombie is **13.5 peas**. Splash
against non-primary targets is `damage/3`, capped so total splash cannot exceed `7 × primary` (or
`1 × primary` for fire peas).

**Where the endless ramp stops.** (INFERENCE, grounded in `MAX_ZOMBIES = 50`.) Because the per-wave
count is a hard 50 and the type pool is fixed at level generation, **Survival Endless stops getting
harder in the "more zombies" sense once the budget saturates those 50** — matching the wiki's
observation that "the number of zombies will stop increasing at around 100 flags"
(https://plantsvszombies.wiki.gg/wiki/Survival:_Endless). After that the only escalating pressure is
the +50 sun upgrade-cost escalator and the player's own board decay. **An endless mode built on a
fixed-size spawn array has a ceiling baked into the array.**

### 1.6 The puzzle modes — Vasebreaker and I, Zombie

**FACT [2nd-tier]** — Puzzle Mode is **two ladders of ten**, each ending in an Endless variant.
https://strategywiki.org/wiki/Plants_vs._Zombies/Puzzle_Mode

**Vasebreaker** (https://plantsvszombies.wiki.gg/wiki/Vasebreaker) — a grid of vases, each holding a
plant or a zombie; green leaf-marked vases always hold plants. Ten levels: Vasebreaker, To the Left,
Third Vase, Chain Reaction, M is for Metal, Scary Potter, Hokey Pokey, Another Chain Reaction, Ace of
Vase, Vasebreaker Endless. **Every ten streaks in Endless drops a present, chocolate, a gold-coin bag
or a diamond bag.**

**I, Zombie** (https://plantsvszombies.wiki.gg/wiki/I,_Zombie) — **the faction inversion.** You play
zombies against a fixed, pre-authored plant layout. Sun comes only from **eating a Sunflower: 200 sun
each**. The full zombie price list:

| Zombie | Sun | Zombie | Sun |
|---|--:|---|--:|
| Zombie / Imp | 50 | Balloon / Ladder | 150 |
| Conehead / Pole Vaulting | 75 | Football | 175 |
| Screen Door | 100 | Gargantuar | 300 |
| Buckethead / Digger / Bungee | 125 | Dancing | 350 |

Ten levels; every three streaks in Endless drops a reward.

**The finding: these two modes add depth with zero new content.** (INFERENCE.) Vasebreaker reuses
every existing asset and adds one noun — the vase — turning a real-time defense game into a
turn-based information-management puzzle. I, Zombie reuses everything and adds *nothing*; it swaps
which side the player controls and replaces the sun faucet with "kill a Sunflower, get 200." **Both
are pure re-framings of the existing simulation, and both shipped in the base game.** That is the
cheapest depth in the entire franchise, and no later PvZ title repeated the trick at this quality.

**What problem it solves.** Gives the mastered simulation a second and third read.
**What it costs the designer.** Almost nothing in content; a great deal in *level authoring* — every
I, Zombie level is a bespoke hand-built plant layout.
**What breaks when tuned wrong.** In I, Zombie the sun economy is a step function (200 per Sunflower
eaten), so a layout that lets you reach two Sunflowers cheaply is trivially solvable. The mode lives
or dies on layout authoring, not on numbers.

### 1.7 Why the game is considered near-perfectly tuned

Four reasons, and only the first is about numbers.

1. **FACT — the tutorial is the game.** George Fan, GDC 2012: new mechanics arrive **roughly every
   five levels**, each introduced with limited features first; the shovel is taught four separate
   times in four contexts; starting resources were deliberately constrained so the player discovers
   the economy rather than being told it. https://www.gdcvault.com/play/1015541/How-I-Got-My-Mom ·
   https://www.gamedeveloper.com/design/video-how-i-got-my-mom-to-play-through-i-plants-vs-zombies-i- ·
   https://notes.hamatti.org/sources/talks/how-i-got-my-mom-to-play-through-plants-vs.-zombies
2. **FACT — the economy was tuned by watching people fail to understand it**, not by spreadsheet.
   The Sunflower halving, and the total rebalance it forced, is documented in §1.1.
3. **The difficulty is elastic without being scaled** — the HP-threshold early-trigger in §1.5.
4. **Nothing persistent can break a level.** Coins buy convenience and a garden; the 50 Adventure
   levels are solvable with the plants you are handed. (INFERENCE, but it follows directly from the
   shop inventory: nothing in it is a stat.)

**FACT** — Metacritic: PC 87, iPhone 92, iPad 93, Xbox 360 89. PopCap's fastest-selling title within
two weeks; the iOS version sold 300,000+ in nine days.
https://en.wikipedia.org/wiki/Plants_vs._Zombies_(video_game)

---

## 2. PvZ Heroes — the most RPG-shaped title in the franchise

Released **10 March 2016** (Australia soft launch), **18 October 2016** worldwide excluding China.
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes

Everything marked **(computed)** in this section comes from my own parse of the datamined card table
(503 collectible cards + 58 superpowers).

### 2.1 The frame

| Rule | Value | Source |
|---|---|---|
| Hero health | **20** + Super-Block Meter | https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes |
| Block meter | **8 blocks**; damage fills **1–3 at random**; fires at most **3× per game** | same |
| Deck size | **exactly 40**, max **4 copies** of a card | same |
| Max hand | **10** | same |
| Superpowers | **4 per hero** — 3 class, 1 signature — do **not** count toward the 40 | same |
| Sun / brains | **+1 per turn**, every turn | same |
| Board | **5 lanes**; in ranked, rightmost is **aquatic**, leftmost is **elevated** | https://en.wikipedia.org/wiki/Plants_vs._Zombies_Heroes |
| Lane capacity | **1**, or **2** with Team-Up | same |
| Turn phases | **Zombies Play → Plants Play → Zombie Tricks → Fight** (lanes resolve left to right; the zombie fighter strikes first in its lane) | https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes |

**The finding: the phase order is the whole game.** (INFERENCE.) Zombies commit fighters *first* and
plants respond, but zombies get the *last* word with tricks and the first swing in combat. That is a
deliberate information asymmetry — the zombie player pays in commitment for the right to answer. It
is also why the zombie side can afford slightly fatter stat lines (§2.5).

### 2.2 The class lattice — five colours a side, and an exhaustive pair enumeration

**FACT [2nd-tier]** — the ten classes and their published identities:
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes

| Plant class | Published identity | Zombie class | Published identity |
|---|---|---|---|
| **Guardian** | "defensively Team-Up, are Armored, and Amphibious" | **Beastly** | "grow large, Frenzy, and will destroy the enemy" |
| **Kabloom** | "EXPLOSIVE and swarming, doing extra damage" | **Brainy** | "Tricky, gain extra Brains, and use Bullseye" |
| **Mega-Grow** | "make Plants HUGE and give Bonus Attacks" | **Crazy** | "aggressive dancers, and do damage directly" |
| **Smarty** | "outsmart foes with Bounce, Freeze, and Amphibious" | **Hearty** | "outlast using high health, Armored, and Healing" |
| **Solar** | "make extra Sun, Heal, and then Strikethrough the enemy" | **Sneaky** | "avoid fighting by moving, being Amphibious, and hiding in Gravestones" |

**The deck construction rule.** Every hero owns **exactly two classes**, and a deck may contain only
cards of those two classes (plus neutral Basic cards). 40 cards, max 4 copies.

**The lattice is exhaustive, and this is the sharpest structural finding in the franchise.**
(computed.) With 5 classes per side there are **C(5,2) = 10** possible pairs per side, **20 in
total**. The game ships **11 heroes per side = 22 heroes**, and **FACT** exactly two pairs are
duplicated — Super Brainz and Huge-Gigantacus are both Brainy/Sneaky; Citron and Beta-Carrotina are
both Guardian/Smarty — "which is why 22 heroes produce 20 entries."
https://pvzhvault.com/best-decks-for-every-hero/

I independently confirm the lattice from the card table: **(computed)** the 29 plant and 29 zombie
superpowers carry class labels covering **all ten pairs per side** (Guardian/Smarty,
Kabloom/Mega-Grow, Mega-Grow/Solar, Smarty/Solar, Kabloom/Smarty, Mega-Grow/Guardian,
Mega-Grow/Smarty, Kabloom/Solar, Kabloom/Guardian, Guardian/Solar — and the zombie mirror). Each
side's superpower list contains **exactly 11 Legendary entries**, one per hero signature.
**(computed)** 18 Super-Rare + 11 Legendary = 29 superpowers per side; **all 58 cost exactly 1**.

**So the hero roster is not a list of characters. It is the complete enumeration of a 5-choose-2
lattice, plus one duplicate per side.** The design does not ask "what hero should we add next" — it
asks "which cell of the lattice is empty."

**What problem it solves for the player.** Two colours is enough to feel like a build and few enough
to explain in one line. The pair, not the hero, is the identity.
**What it costs the designer.** Every card must be balanced in **four** contexts (its own class, plus
the four pairs that class appears in). A sixth class would take pairs from 10 to 15 per side — a 50%
increase in balance surface for a 20% increase in vocabulary.
**What breaks when tuned wrong.** A single overtuned card is felt in four of ten decks at once. This
is why the December 2024 balance patch changed **150+ cards** in one pass
(https://www.youtube.com/watch?v=zgzRNE7uMEg): in a lattice, you cannot nerf locally.

### 2.3 The keyword vocabulary — the single most transferable artefact in the franchise

Definitions are **FACT [2nd-tier]** from
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes/General_terminology and the wiki's
per-card pages. Card counts are **(computed)** from the datamined table — the number of collectible
cards whose rules text names the keyword.

| Keyword | What it does | Plant cards | Zombie cards |
|---|---|--:|--:|
| **Team-Up** | May share a lane — played in front of or behind another fighter. Raises lane capacity from 1 to 2. | **36** | 0 |
| **Amphibious** | May be played in the aquatic lane *as well as* anywhere else. | **28** | **23** |
| **Gravestone** | Played face-down; hidden from the opponent until it reveals at the start of the fight phase. | 6 | **43** |
| **Bullseye** | Damage dealt to the enemy hero does **not** charge their Super-Block Meter. | 8 | **13** |
| **Strikethrough** | Hits every fighter in the lane *and* the opposing hero. | **10** | 7 |
| **Frenzy** | On destroying a plant in combat, attacks again — once per kill, if it survives. | 0 | **15** |
| **Double Strike** | Attacks a second time after surviving combat in its lane. The plant-side mirror of Frenzy. | 5 | 0 |
| **Armored N** | Reduces incoming attack damage by N. | 4 | **9** |
| **Deadly** | Destroys any fighter it damages regardless of health, unless the damage is neutralised. | 0 | 7 |
| **Anti-Hero N** | Gains +N strength when no opposing fighter blocks its lane. | 3 | 5 |
| **Untrickable** | Immune to the **opponent's** tricks. Your own still work on it. | 3 | 2 |
| **Overshoot N** | Excess damage past the blocking fighter carries N through to the hero. | 0 | 5 |
| **Hunt** | Moves to the lane holding the target it wants. | 1 | 5 |
| **Splash Damage N** | Also deals N to the fighters adjacent to the target. | 5 | 0 |
| **Evolution** | Costs less / gains an effect when played onto a fighter of the right tribe already on the board. | 12 | 10 |
| **Dino-Roar** | Triggers whenever *any* card is drawn. The Triassic set's mechanic. | 7 | 5 |
| **Fusion** | Combines with another fighter on its tile. | 5 | 5 |

Adjacent verbs that behave like keywords but are written as effects — **(computed)**: **Conjure**
(add a random card of a named category to hand) 19 plant / 22 zombie; **Bounce** (return a fighter to
its owner's hand) 7 / 11; **Freeze** 5 / 4; **Transform** 8 / 6. Trigger words: **When played**
68 / 43, **When destroyed** 12 / 9, **Start of Turn** 13 / 4, **End of Turn** 4 / 5.

**Keyword ownership is the class identity, measured.** (computed) — every keyword is concentrated in
one or two classes, and the concentration is near-total:

| Plant keyword | Class distribution | Zombie keyword | Class distribution |
|---|---|---|---|
| Strikethrough | **Solar 10 — all of them** | Deadly | **Sneaky 7 — all** |
| Armored | **Guardian 4 — all** | Strikethrough | **Sneaky 7 — all** |
| Double Strike | **Mega-Grow 5 — all** | Hunt | **Beastly 5 — all** |
| Splash Damage | **Smarty 5 — all** | Gravestone | **Sneaky 19**, then Brainy/Crazy/Hearty 7 each |
| Freeze | **Smarty 5 — all** | Armored | **Hearty 8** of 9 |
| Bounce | **Smarty 7 — all** | Frenzy | Beastly 9, Hearty 5 |
| Anti-Hero | **Kabloom 3 — all** | Bullseye | Brainy 9, Crazy 4 |
| Amphibious | Smarty 18, Guardian 10 | Amphibious | Sneaky 11, Beastly 11 |
| Transform | Kabloom 6 of 8 | Overshoot | Crazy 4, Brainy 1 |
| **Team-Up** | **spread across all five**: Guardian 10, Smarty 9, Solar 8, Kabloom 6, Mega-Grow 3 | Bounce | Sneaky 6, Beastly 5 |

**The finding: a class is not a stat profile, it is the set of keywords it is allowed to use.**
Solar owns Strikethrough outright. Sneaky owns Deadly and Strikethrough outright. Guardian owns
Armored, Mega-Grow owns Double Strike, Smarty owns Freeze, Bounce and Splash. **The only keyword
deliberately shared across all five plant classes is Team-Up** — the one that changes board geometry
rather than combat, and therefore the one every deck needs. **Evolution, Dino-Roar and Fusion are
also spread evenly at 1–3 per class, because they are *set* mechanics, not *class* mechanics.** Those
two exceptions are the rule that proves it.

**What problem it solves for the player.** A keyword is a promise: learn "Deadly" once and you know
it on 7 cards, and you know it means "Sneaky deck." The vocabulary is small enough to hold in your
head and large enough to make 503 cards distinguishable.
**What it costs the designer.** Keywords multiply: every new one must be checked against all 17
existing ones. The franchise stopped at ~17 and never added a wide new one after Triassic.
**What breaks when tuned wrong.** Give a keyword to a second class and you have not added variety,
you have deleted an identity. The one keyword that genuinely is shared (Team-Up, 5 classes) is also
the one most often cited as warping deckbuilding — every class needs it, so every class runs the same
few Team-Up bodies.

### 2.4 What rarity actually buys — measured, and it is not stats

**FACT [2nd-tier]** — six rarities: Common, Uncommon, Rare, Super-Rare, Legendary, Event.
Craft/recycle in Sparks:

| Rarity | Craft | Recycle | Ratio (computed) |
|---|--:|--:|--:|
| Uncommon | **50** | **10** | 5:1 |
| Rare | **250** | **50** | 5:1 |
| Super-Rare | **1,000** | **250** | 5:1 |
| Legendary | **4,000** | **1,000** | 5:1 |

https://plantsvszombies.fandom.com/wiki/Spark (search snippet; the wiki.gg mirror of this page was
not reachable — see §9)

**(computed) The craft ladder is 5× / 4× / 4×, and the sink:faucet ratio is a flat 5:1 at every
tier.** Recycling four copies of a Legendary you already own returns 4,000 — exactly one craft. A
spare playset converts to precisely one card of the same tier.

**Now the measured part.** Over all 503 collectible cards, mean stat total (strength + health) and
mean rules-text length per rarity:

| Rarity | n (plant) | mean cost | mean (str+hp) | **stat per sun** | **mean text length** |
|---|--:|--:|--:|--:|--:|
| Common | 29 | 2.38 | 3.92 | **1.97** | **22** |
| Uncommon | 61 | 2.23 | 4.23 | **2.13** | **38** |
| Rare | 45 | 3.49 | 5.08 | **1.47** | **49** |
| Super-Rare | 50 | 3.50 | 5.89 | **1.95** | **55** |
| Legendary | 35 | 5.26 | 8.12 | **1.71** | **62** |
| Event | 31 | 3.06 | 4.83 | **1.95** | **66** |

Zombie side, same shape: Common **1.85** stat/sun at 19 chars → Legendary **1.83** stat/sun at **72**
chars.

**The finding, stated plainly: stat efficiency is flat across every rarity tier — it wanders between
1.47 and 2.29 with no trend — while rules text grows monotonically from ~20 to ~70 characters.**
(computed.) Rarity in PvZ Heroes buys **text**, not numbers. A Legendary is not a better body; it is
a body with a paragraph attached. Corroborating: **(computed)** 11 of 29 plant Commons and 7 of 25
zombie Commons are **vanilla — zero rules text** — and above Uncommon, vanilla cards essentially stop
existing (2 plant Uncommons, 1 zombie Uncommon, then none).

**What problem it solves.** A free player is never statistically outgunned. Rarity gates *options*,
not power, so the collection grind is about deck ideas rather than a wall.
**What it costs the designer.** Every Legendary needs a genuinely novel effect. You cannot ship a
Legendary that is "the Common but bigger" — the economics of the tier forbid it.
**What breaks when tuned wrong.** The moment one Legendary is *also* stat-efficient, the flat curve
is a lie and the free-to-play promise breaks. That is the failure mode the 150-card December 2024
patch exists to correct.

### 2.5 The stat frame, and the plant/zombie asymmetry

**(computed)** Mean (strength + health) by cost, over all fighters with stat lines:

| Cost | Plant mean | n | Zombie mean | n | **Zombie edge** |
|--:|--:|--:|--:|--:|--:|
| 1 | 2.88 | 41 | 3.09 | 33 | +0.21 |
| 2 | 3.77 | 35 | 4.03 | 39 | +0.26 |
| 3 | 4.60 | 45 | 4.92 | 38 | +0.32 |
| 4 | 6.18 | 34 | 6.72 | 32 | +0.54 |
| 5 | 7.42 | 26 | 8.62 | 26 | **+1.20** |
| 6 | 9.41 | 17 | 10.50 | 14 | **+1.09** |

**The finding: the zombie side is systematically fatter at every cost, and the gap widens with
cost.** (computed.) The curve is close to linear at roughly **+1.5 stat per sun** with a small
negative intercept on both sides. **INFERENCE** — this is the price the plant side pays for the phase
order (§2.1): plants see the zombie commitment before they answer, so they get slightly less raw
material. Card games usually pay for information with tempo; PvZ Heroes pays for it in stats, which
is cheaper to tune and easier to read.

**(computed) The cost curve is deliberately front-loaded**: 84% of plant cards and 84% of zombie
cards cost 1–5; only 9 plant and 13 zombie cards cost 7 or more. The mode is cost 3 (plants) and
cost 2 (zombies).

### 2.6 Fighters, tricks, environments — and the tribe system

**(computed)** from the card table:

| Card kind | Plant | Zombie |
|---|--:|--:|
| Fighters | 207 | 195 |
| Tricks | 33 | 45 |
| Environments | 11 | 12 |
| **Total collectible** | **251** | **252** |

Plus 29 + 29 superpowers → **561 distinct card objects (computed)**.

**Fighters** occupy a lane and fight. **Tricks** are spells; the zombie side gets 36% more of them
(45 vs 33), consistent with Brainy's "Tricky" identity. **Environments** claim a *tile* and modify
whatever stands there — 23 in total across both sides, the rarest card kind in the game.

**Tribes.** **(computed)** the plant side runs **23 tribes**, the zombie side **17**:

| Plant tribes (cards) | Zombie tribes (cards) |
|---|---|
| Flower 40, Leafy 29, Fruit 27, Root 26, Bean 21, Mushroom 21, Pea 21, Nut 21, Animal 19, Berry 18, Squash 8, Banana 7, Cactus 5, Flytrap 5, Corn 5, Tree 5, Moss 3, Seed 3, Dragon 2, Pineclone 2, Vine 1, Mime 1, Pinecone 1 | Pet 46, Science 45, Mustache 34, Professional 32, Imp 30, Dancing 25, Sports 24, Gourmet 24, History 24, Pirate 23, Gargantuar 22, Party 19, Monster 17, Barrel 12, Clock 1, Mime 1 |

**Tribes are composite, not exclusive** — a card's type is a space-separated list: "Bean Pea Plant",
"Gourmet Science Trick", "Professional Mustache Zombie". **(computed)** plant tribe counts sum to 291
across 251 cards, so **the average plant card carries 1.16 tribes**; the zombie side sums to 379
across 252 cards, **1.50 tribes per card**.

**The finding: the zombie side is denser in tribe overlap (1.50 vs 1.16), which is why its
tribal-synergy cards hit harder.** (computed / INFERENCE.) A card that says "when you play a Science
Zombie" hits 45 cards, and many of those *also* count as Pet or Mustache, so a zombie tribal deck
gets more triggers per card played. The plant side compensates with a longer tail of tiny tribes
(Vine 1, Mime 1, Pinecone 1) used for one-off jokes rather than synergy.

### 2.7 Sets, and the fact that nothing ever rotated

**FACT** — four sets:

| Set | Released | Cards | Per side |
|---|---|--:|--:|
| Premium (base) | **10 March 2016** | 190 | — |
| Galactic Gardens | **8 June 2017** | 100 | 50 |
| Colossal Fossils | **9 October 2017** | 50 | 25 |
| Triassic Triumph | **30 January 2018** | 50 | 25 — **the last set** |

https://en.wikipedia.org/wiki/Plants_vs._Zombies_Heroes ·
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes/Update_history

**(computed) Every expansion is a rigid rarity skeleton.** From the card table, cards per set per
side by rarity:

| Set | Common | Uncommon | Rare | Super-Rare | Legendary | Event |
|---|--:|--:|--:|--:|--:|--:|
| Basic (plant / zombie) | 28 / 25 | 0 | 0 | 0 | 0 | 0 |
| Premium | 1 / 0 | 31 / 35 | 25 / 25 | 20 / 20 | 15 / 15 | 0 |
| Galactic | 0 | 15 / 15 | 10 / 10 | 15 / 15 | 10 / 10 | 0 |
| Colossal | 0 | 7 / 8 | 5 / 5 | 8 / 7 | 5 / 5 | 0 |
| Triassic | 0 | 8 / 7 | 5 / 5 | 7 / 8 | 5 / 5 | 0 |
| Event | 0 | 0 | 0 | 0 | 0 | 31 / 32 |

**The small expansions are identical templates: 25 cards per side = 7–8 Uncommon, 5 Rare, 7–8
Super-Rare, 5 Legendary.** (computed.) Galactic is exactly 2× that shape. This is a content factory,
not a creative brief — a set is a fixed number of slots at each rarity, and the only design question
is what fills them.

**FACT — no rotation was ever implemented.** Every card ever printed stayed legal.
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes/Update_history

**FACT — the game is content-frozen but still maintained.** No new set since **30 January 2018**.
Client builds have continued — 1.50.2 (April 2024), 1.60.79 (May 2025), 1.61.37 (July 2025), 1.63.37
(November 2025), 1.64.6 (December 2025), **1.66.7 (3 June 2026)**.
https://gameupdatenotifier.com/g/plants-vs-zombiestm-heroes — and EA has published balance updates as
recently as **June 2025**, after the **9 December 2024** patch that changed 150+ cards.
https://www.ea.com/games/plants-vs-zombies/plants-vs-zombies-heroes/news/balance-update-for-december-2024 ·
https://www.ea.com/games/plants-vs-zombies/plants-vs-zombies-heroes/news/balance-update-for-june-2025

**(computed)** Time since the last new card set: **8 years 7 months**.

**The finding: PvZ Heroes stopped printing cards after 22 months and has been balance-patched for
more than eight years since.** That is the opposite of a card game's normal economics, and it is only
possible *because* rarity buys text rather than stats — a frozen pool with a flat power curve can be
tuned indefinitely without a new set to sell.

### 2.8 The pack and quest economy

**FACT [2nd-tier]** — https://plantsvszombies.wiki.gg/wiki/Packs_(PvZH)

| Pack | Cost | Cards | Guarantee |
|---|---|--:|---|
| Basic (retired) | 100 coins / 50 gems | 3 | Basic Commons |
| Premium / Galactic / Colossal / Triassic | **100 gems**, or **1,000 gems for 10+1** | 6 | ≥1 Rare |
| Fertilizer / Brainz Premium (retired) | 150 gems, 1,500 for multipack | 6 | ≥1 Rare or better |
| Hero Pack | **750 gems** | — | guaranteed hero + companion cards |

Multipacks add guaranteed Super-Rares and Legendaries at the 23rd and 50th pack thresholds. **No drop
rates were ever published.**

**FACT [2nd-tier]** — Hero Quests: **10 tasks per hero**, paying in order 10 gems, 10 gems, **50
sparks**, a Premium Uncommon, 10 gems, 10 gems, **250 sparks**, a Premium Rare/Super-Rare, **200
sparks**, and that hero's Premium pack. **Total per hero: 40 gems, 500 sparks, 2 cards, 1 pack.**
https://plantsvszombies.wiki.gg/wiki/Hero_Quests

**(computed)** 22 heroes × 500 sparks = **11,000 sparks** from hero quests alone — **2 Legendaries
and change, or 44 Rares**. **INFERENCE** — the quest ladder is sized to hand a new player a *deck's
worth of Rares* rather than a chase card. It funds breadth.

**FACT [2nd-tier]** — a **free premium pack after 3–5 consecutive losses** (once per acceptance).
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes — an explicit loss-streak mercy
faucet, and the collection-side twin of the block meter's in-match comeback valve.

### 2.9 Superpowers — the block meter as a comeback valve

**FACT [2nd-tier]** — the Super-Block Meter has **8 blocks**; each hit fills **1–3 at random**; a full
meter **blocks that attack entirely** and hands the hero a superpower. It can fire **at most three
times per game**. Each hero has **4 superpowers**: 3 from its classes, 1 **signature** (Legendary).
One is drawn at random when the meter fires.
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes

**(computed)** All 58 superpowers cost exactly 1. 18 per side are Super-Rare (the class pool), 11 per
side are Legendary (the signatures — one per hero, independently confirming 11 heroes a side).

**The finding: the losing player gets a resource the winning player never sees.** (INFERENCE.) The
meter fills from *taking* damage; a player who is ahead never charges it. Combined with **Bullseye** —
21 cards across both sides (computed) whose only job is to **stop the meter charging** — this is a
fully articulated rubber band with a fully articulated counter. **The counter to the comeback
mechanic is itself a printed keyword with a card count.** That is the cleanest example in the
franchise of a system and its answer being designed together.

**What problem it solves.** Stops a snowball from being a twenty-turn foregone conclusion.
**What it costs the designer.** Randomness at the worst moment: which superpower you get is a roll,
and the 1–3 fill is a roll.
**What breaks when tuned wrong.** Too much Bullseye and the valve never opens; too little and
aggressive decks cannot close. 21 Bullseye cards out of 503 is **4.2% (computed)** — deliberately
scarce.

---

## 3. Garden Warfare 1 & 2 and Battle for Neighborville — 100 units from 8 classes, then the retreat

### 3.1 The ratio

| | **GW1 (2014)** | **GW2 (2016)** | **BfN (2019)** |
|---|--:|--:|--:|
| Base classes | **8** (4 a side) | **14** at launch → **16** with DLC | **20** (10 a side) → 21 |
| Variants | **60** | **105** | **0** |
| **Total playable** | **68** | **121** | **20 → 21** |
| **Ratio base : total (computed)** | **1 : 8.5** | **1 : 7.6** | **1 : 1** |

Tallied from the wiki.gg class rosters — **FACT [2nd-tier]**:
https://plantsvszombies.wiki.gg/wiki/Plants_(PvZ:_GW) · https://plantsvszombies.wiki.gg/wiki/Zombies_(PvZ:_GW) ·
https://plantsvszombies.wiki.gg/wiki/Plants_(PvZ:_GW2) · https://plantsvszombies.wiki.gg/wiki/Zombies_(PvZ:_GW2)

**FACT (first-party)** — GW2 creative director **Justin Wiebe**: *"We have over 100 characters. 14
character classes, 100 characters."* and *"Over 4000 collectible customizations."*
https://gamingtrend.com/feature/previews/plants-vs-zombies-garden-warfare-2-justin-wiebe-interview/

**GW1's eight are a mirrored 4×2 archetype grid** — **FACT [2nd-tier]**: *"Peashooter and Foot
Soldier act as generic soldier classes; the Sunflower and Scientist act as support characters; the
Chomper and the All-Star act as 'tanks'; and the Cactus and Engineer act as specialists."*
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies:_Garden_Warfare

**The finding: the multiplier is ~7–9 and it does not move.** (computed.) GW2 nearly doubled the
class count and characters-per-class stayed flat. The four *new* GW2 classes shipped with **5
variants each**; the four legacy classes had accreted to **9–11** over two years of free DLC. **A
fresh base class enters at ~5 variants and matures at ~9–11.** That is the empirical shape of a
variant program.

### 3.2 What a variant actually changes

**The element layer — four global statuses authored once, stamped onto every class.**
**FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Elements

| Element | GW1 | GW2 |
|---|---|---|
| **Fire** | 5 dmg/sec × 4 s (**20 total**) | 4 dmg/sec × 5 s (**20 total**) |
| **Ice** | slow only | progressive slow → **2.1 s hard freeze**; frozen units cannot fire, use abilities, or turn |
| **Toxic** | 2 dmg/sec + passive proximity aura | 2 dmg/sec, and **spreads between adjacent enemies** |
| **Electric** | — | chain/arc splash **4 → 16** depending on the character |

**The Peashooter family, with real numbers** — **FACT [2nd-tier]**, one wiki page per variant:

| Character | HP | Damage (close/mid/long) | Splash | Ammo | Reload | Mode | The twist |
|---|--:|---|--:|--:|--:|---|---|
| Peashooter (GW1, upgraded) | 125 | 37 / 37 / 27 · DPS 65.8 | 10 | 12 | 1.5 s | semi | reference |
| Fire Pea (GW1) | 125 | 26 / 26 / 16 · DPS 51.2 | 10 | 12 | **2.6 s** | semi | burn 5/s × 4; **does not proc on splash** |
| Ice Pea (GW2) | 125 | 15–25 | 10 | 12 | — | semi | stacking slow → hard freeze; **splash can freeze several** |
| Toxic Pea (GW1) | 125 | 26 / 24 / 18 | 10 | 12 | 2.5 s | semi | poison 2/s × 2–5 ticks by range + 5-dmg aura |
| Commando Pea | 125 | 12 / 9 / 6 · **DPS 72.0** | **0** | **30** | 2.0 s | **full-auto** | all splash traded for uptime |
| Agent Pea | **100** | 16 / **33 crit** · **DPS 157.1** | **0** | **15** | **1.5 s** | semi + **zoom** | 2× crit, fastest everything, **−25 HP** |
| Rock Pea (GW2) | **150** | 22.2–32.2 | **12** | **8** | — | **−10% fire rate** | +25 HP paid for in ammo, rate, damage *and* speed |
| Electro Pea (GW2) | 125 | 20–30 + splash 10–25, chain 5–10 | 10–25 | **8** | — | semi + **manual airburst** | chain lightning, one of three mid-air detonators |

https://plantsvszombies.wiki.gg/wiki/Peashooter_(PvZ:_GW) and the linked variant pages.

**The finding: a variant is a pick from about seven closed levers, not a free-form design.**
(INFERENCE, from the table above.) The levers: (1) elemental status, from a set of 4 with
globally-authored numbers; (2) fire mode — semi / full-auto / charge / manual-detonate; (3) splash on
or off, and its radius; (4) the ammo × reload × fire-rate DPS-vs-uptime trade; (5) health ±, always
paired with a movement-speed penalty; (6) crit multiplier + zoom, the "sniper package"; (7) projectile
behaviour — homing, piercing, arcing. **The whole 121-character roster is combinations of those
seven.**

**And every variant pays for what it gains.** Rock Pea's +25 HP costs fire rate, ammo, damage and
speed. Agent Pea's 157 DPS and 2× crit cost 25 HP and all splash. **Metal Petal** (Sunflower, 150 HP
GW1 / 160 GW2) — *"the metal weighs her down—making her slower than other Sunflowers—she has the
added benefit of higher health"* — https://plantsvszombies.wiki.gg/wiki/Metal_Petal — is **Rock Pea's
exact trade on a different class**. **Camo Ranger** (Foot Soldier, 18 ammo / 3.0 s reload / no splash
/ DPS 76) is Agent Pea's package on the zombie generalist.
https://plantsvszombies.wiki.gg/wiki/Camo_Ranger

**What problem it solves for the player.** A hundred names in a menu, each of which plays
differently, with no reading required — you learn the base class once and the variant in one match.
**What it costs the designer.** Art, VO and a balance pass per variant, forever, on a roster that
compounds with every DLC.
**What breaks when tuned wrong.** Nothing catastrophic — trade-off balancing is **self-correcting**: a
variant strong on one lever is weak on another by construction. The problem is not balance, it is
**volume**.

### 3.3 The sticker economy

**FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Stickers

| GW1 pack | Coins | Items | Guarantee |
|---|--:|--:|---|
| Reinforcements | 1,000 | 5 | consumables |
| Super Duper | 5,000 | 5 | ≥1 Uncommon |
| Craaazy | 10,000 | 7 | ≥1 Rare |
| Supremium / Incredi-Plant / Vengeful Zomboss | 20,000 | 9 | ≥1 Super Rare, high chance of character items + **weapon upgrades** |
| Amazing Bling | 30,000 | 5 | cosmetics |
| **Spectacular Character** | **40,000** | 5 | **guaranteed Rare or Super Rare character** |

| GW2 pack | Coins | Items | Guarantee |
|---|--:|--:|---|
| Minions Booster | 2,500 | 5 | consumables |
| Helpful Fun | 7,500 | 5 | ≥1 Uncommon |
| Extraordinary | 20,000 | 6 | ≥1 Rare |
| Wondrous / Fertilizer Fun / Amazing Brainz | 35,000 | 7 | ≥1 Super Rare |
| **Phenomenal Character** | **75,000** | 5 | **guaranteed enough stickers for a Rare/Super Rare character** |
| Infinity | 200,000 | 3 | Infinity Time items |

**FACT [2nd-tier]** — **5 stickers complete a variant** in GW1; GW2 uses 5 pieces for most and **2 for
Legendary characters**.

**(computed) GW2 roughly doubled every price tier while shrinking item counts** — Craaazy 10k/7 →
Extraordinary 20k/6; Spectacular 40k/5 → Phenomenal 75k/5. **Neither game ever published drop rates.**

**The deterministic escape valve: Rux.** **FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Rux
— a vendor whose stock **rotates every two weeks** and is open only briefly. Rare customizations
10,000–50,000 · classic items 50,000 · special customizations 250,000 · Legendary customizations
300,000 · **exclusive Legendary customizations and exclusive abilities 500,000** · **Legendary
character pieces 1,000,000**.

**(computed)** Rux's Legendary character piece at 1,000,000 coins is **~13× the Phenomenal Character
Pack**. **INFERENCE — the game charges an order of magnitude to remove randomness, and puts a
two-week window on the offer.** That is the loot-box-plus-vendor pattern in two numbers.

### 3.4 Abilities and upgrades — the layers under the variants

**FACT [2nd-tier]** — **3 ability slots, each a binary A/B choice.** Peashooter: Chili Bean Bomb *or*
Sombrero Bean Bomb; Pea Gatling *or* Retro Gatling; Hyper *or* Super Pea Jump. Foot Soldier: Stink
Cloud / Super Stink Cloud; Rocket Jump / Rocket Leap; ZPG / Multi-Rocket.
https://plantsvszombies.wiki.gg/wiki/Peashooter_(PvZ:_GW)

**(computed)** 3 slots × 2 options = **8 loadouts per character**, before Rux's exclusive third
options (Super Guided Ultra Ball, Arcane Lotus, Rainbow Flower — 500,000 coins each).

**FACT [2nd-tier]** — **GW2 pulled weapon upgrades out of the loot box and onto the level curve**:
each character has **8 upgrades**, of which **3 may be equipped at once**, granted by character
levelling. Types include Health, Health Regen, Regen Delay, Speed, Reload, Ammo, Damage, Zoom,
Overheat, Digestion, Super Meter, elemental amplification, Vampiric, Penetrate, Homing.
https://plantsvszombies.wiki.gg/wiki/Upgrades_(PvZ:_GW2)

**This is the under-reported part.** (INFERENCE.) GW2 already ran a *"choose 3 of 8 modifiers"* build
system **underneath** 121 variants. BfN's celebrated new upgrade system is that same system scaled up
with the variant layer deleted — not a replacement invented in 2019.

**FACT (first-party, GW1 producer Brian Lindley)** — TF2 was the stated model, and *"We stripped
everything down to basically a primary weapon and three or four abilities for every character,"* with
abilities unlocked *"progressively through simple challenges rather than complex XP systems."*
https://www.killscreen.com/garden-warfare-aims-be-shooter-you-and-your-80-year-old-grandma/

**INFERENCE — this is the closest thing to a stated rationale for variants-over-progression in the
record.** PopCap deliberately removed the XP/skill-tree layer for accessibility. That leaves *the
character itself* as the only carrier of build identity. **Variants are what you get when you refuse
to give the player a skill tree but still need hundreds of hours of differentiation.**

### 3.5 Battle for Neighborville — the retreat, and what replaced variants

**FACT (first-party, EA manual)** — 20 characters, 10 a side, sorted Attack / Defend / Support.
**"Each character has 3 primary abilities."** Characters cap at **level 10**; *"Promoting a character
resets their level back to 1 but earns them a new title and upgrades,"* with **5 promotions** ending
at **Master**. https://www.ea.com/able/resources/pvz/pvz-battle-for-neighborville/pc/manual

**FACT [2nd-tier]** — **7 upgrade points per character**; individual upgrades cost **1 to 5 points**,
and are freely swappable out of combat. A complete tree
(https://plantsvszombies.wiki.gg/wiki/Template:BfN_Upgrades/Scientist) runs 21 entries from 1-point
utilities ("use jump and abilities while reviving"), through 3-point stat modifiers ("+12.9% walk
speed"), to a **5-point upgrade that swaps the primary weapon outright** (Steam Blaster).

**FACT [2nd-tier]** — variants were removed outright: *"In Garden Warfare 2 each class had numerous
elemental variants that would change how characters played, but that system was replaced with a small
upgrade system where you can pick from a few perks."*
https://en.wikipedia.org/wiki/Plants_vs._Zombies:_Battle_for_Neighborville

**FACT (first-party, EA)** — the explicit ceiling on the replacement: a legendary upgrade *"will
change the primary weapon of a character in some way, without causing that character to deviate too
far from their role or weapon archetype."*
https://www.ea.com/games/plants-vs-zombies/plants-vs-zombies-battle-for-neighborville/news/legendary-upgrades-tips-and-tricks

**The structural inversion, and it is the most important lesson in this section.** (INFERENCE.)

| | GW2 variants | BfN upgrade points |
|---|---|---|
| Unit of differentiation | an **authored character** | a **modifier row** on a shared character |
| Acquisition | randomised packs, 5 pieces | deterministic: levels + promotions |
| Balanced by | **trade-off** — every gain has a printed cost | **budget** — you can only afford 7 points |
| Content cost per option | art + VO + balance pass | one data row |
| Failure mode | roster too large to balance | *"the upgrades for Primary weapons had no drawback, they just made you do better"* |

That last quote is community, not first-party:
https://steamcommunity.com/app/1262240/discussions/0/2293968408154511956/

**A trade-off system self-corrects; a budget system does not.** A strong variant is, by construction,
weak somewhere. A budget system's individual upgrades are mostly pure gains, so the seven strongest
points-per-value upgrades are simply the correct build and everyone converges on them. **That is the
concrete mechanical form of the "no drawback" complaint, and it is why 121 characters were more
tuneable than 21 upgrade rows.**

### 3.6 The hub layer

**FACT [2nd-tier]** — GW2's **Backyard Battleground**: a persistent hub with a Sticker Shop, a Quest
Board (daily quests + Epic Quests paying coins, stars and XP multipliers), **stars** that unlock
hidden treasure chests, the **Flag of Power** (raise a flag, survive escalating waves — an endless
coin/XP faucet), Crazy Targets (unlocks at 5 stars), and a Character Stats Room.
https://plantsvszombies.wiki.gg/wiki/Backyard_Battleground

**FACT (first-party, Wiebe)** — *"We've taken the best things we love from RPGs, like the storytelling
and the quests, and then tried to wrap them up under what I would still call an action-oriented
game."* https://gamingtrend.com/feature/previews/plants-vs-zombies-garden-warfare-2-justin-wiebe-interview/

**FACT [2nd-tier]** — BfN replaced the shop with **Prize Bulbs**: **1 bulb per 4,000 XP**, 2 per daily
challenge, 2 per weekly character challenge (5 for the full set), spent on a **monthly-rotating Prize
Map**, with a **hard cap of 50 bulbs held** (999 on Switch).
https://plantsvszombies.wiki.gg/wiki/Prize_Bulbs

**INFERENCE** — the 50-bulb cap is an anti-hoarding device: you cannot bank progress across a map
rotation, so the faucet forces regular spending. BfN swapped a *randomised* content faucet for a
*deterministic, time-gated* one. Both are retention devices; only the second is auditable by the
player.

---

## 4. The base-building, energy-timer and gear experiments

### 4.1 PvZ Adventures (Facebook, 2013–2014) — the crop-timer economy

**FACT** — announced **26 March 2013**; released **20 May 2013**; shutdown announced **14 July 2014**;
retired from Facebook **12 October 2014**. **Lifetime: ~17 months.** It was **the first Plants vs.
Zombies game ever to shut down.** https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Adventures ·
https://www.adweek.com/performance-marketing/ea-announces-closure-of-plants-vs-zombies-adventures-on-facebook/

**FACT — the only verbatim EA statement recovered** is the Facebook notice: *"We are sorry to say that
Plants vs. Zombies Adventures is being retired from Facebook on October 12, 2014."* (Adweek, above.)
No reason was published.

**FACT [2nd-tier]** — a three-mode loop: an isometric **town-building** hub → **Road Trip** (the
tower-defense missions, 15 maps × 5–30 levels, **75–450 stages (computed range)**) → **Brainball**
(PvP raiding, 1 free attack per day paid in coins, more paid in gems; top 5 on the brains leaderboard
earn gems). Raids damaged your town and required **house repairs** afterwards.

**Four currencies** — **FACT [2nd-tier]** + https://www.destructoid.com/reviews/review-plants-vs-zombies-adventures/:
**Coins** (generated on a timer by town buildings), **Zombucks** (dropped by zombies and quests; buys
buildings, decorations, seed slots, zombie summons), **Gems** (real money; skips timers, buys
pre-grown plants mid-battle), **Sun** (battle-scoped, non-persistent).

**The energy gate was not a stamina bar — it was crop timers.** **FACT [2nd-tier]** — plants had to be
grown in planter boxes in your town before a mission, costing coins and real wall-clock time:
**1 minute (Peashooter) up to 18 hours (Bamboom)**.
https://plantsvszombies.wiki.gg/wiki/Plants_(PvZA)

Other exact numbers, **FACT [2nd-tier]**:
- Plant coin costs: Peashooter **25**, Sunflower **50**, Repeater **1,000**, Chilly Pepper **1,500**,
  Bamboom **2,000**
- **16 regular plants**; **9 VIP plants** at **15–360 gems** each
- Battle sun costs **50–175** for regular plants — and **VIP plants cost 0 sun**
- Loadout: max **5 copies** of each plant type
- **5th seed slot: 3,000 Zombucks** (after Cadaver Cavern); 6th unlocked at U of Z
- KO'd plant revive: **15 s + 25 sun**; Wall-nut variants are permanently lost
- **Sprays** (the Plant Food analogue): **25 sun**, activated mid-mission
- Starting gems: **100**; shutdown compensation: every player gifted **100,000 gems**

**The finding: Adventures inverted PvZ's core scarcity from *sun* to *inventory*, and then sold the
bypass.** (INFERENCE, but the mechanism is explicit: VIP plants cost gems to own and **0 sun to
play**.) In PvZ 1 plants are free and sun is scarce; in Adventures plants are consumable stock grown
on a wall clock, and the premium tier removes the in-match resource entirely. **It has no per-unit
vertical progression at all** — no plant levels, no gear, no stats. The whole meta is *which plants
you can afford to have grown*.

Contemporary read, **FACT**: Destructoid — the game *"sleazily tries to get you to buy and spend gems
at every turn, even going so far as allowing you to buy 'that one last extra difference making plant'
in the middle of a stage."*

**What problem it solves for the operator.** A reason to return tomorrow.
**What it costs the designer.** The in-match decision loses its teeth: you no longer pick the *right*
plant, you pick the plant you *have*.
**What breaks when tuned wrong.** If the farm outpaces consumption the timer is invisible; if it lags,
the game is unplayable without paying. There is no wide band between those, and the premium tier sits
exactly in the gap.

### 4.2 PvZ Online (China, 2015–2018) — the maximal RPG experiment

**FACT** — developed by PopCap **jointly with Tencent Games**, China-only, QQ-gated. Betas
**9 December 2013** and **18 July 2014**; **open launch 10 July 2015**; new registrations closed and
delisting announced **15 May 2018**; **operations ceased 7 August 2018**; **servers and website fully
removed 11 August 2018**. https://zh.wikipedia.org/zh-cn/植物大战僵尸Online ·
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Online

**FACT** — 63 plant types, 32 zombie variants (zh.wikipedia).

**Two entirely different games in one client**, and that is the structurally interesting part:

- **Tower-defense side** — five chapters (Qin Shi Huang's Mausoleum, Egyptian Desert, Pirate Bay,
  Future World, Dragon Palace of the East Sea) with main + side levels, plus endless, rampaging
  snowman and time-travel modes.
- **Card/RPG side** — turn-based combat with **decks of up to seven units including a borrowed friend
  plant**. Modes: adventure, **World Boss** (4 bosses, **30,000,000 damage** threshold), 2-player
  co-op treasure hunt, arena, Zombie Island (unlock **level 24**, 16-plant selection, daily reset),
  **Road of Trial (150 levels**, unlock **level 15**), King of the Hill, Across-Service Expedition
  (server-vs-server), **Plant vs. Plant live PvP in a 20:00–20:30 window**, trivia (07:00–24:00),
  weekly TD Challenge.

**The plant progression stack on the card side is ten parallel axes** — **FACT [2nd-tier]**: level ·
star rank · **awakening** (raises the plant's "moon rarity") · puzzle-piece fusion · Tree of Wisdom
(Tree Food stat boosts) · equipment · costumes · chlorophyll · Nutrition Room reagents (unlock level
22) · Laboratory experiment bottles. A card displays as e.g. *"Green +3 tier, Level III, 3-star, Level
44 Pea Pod"* — **four simultaneous progression coordinates printed on one unit**.

**The TD side upgraded separately** via the **Greenhouse** (unlock level 3) and supported **hybrid
planting — planting a Peashooter onto a Peashooter yields a Repeater** (**FACT [2nd-tier]**). *That is
a merge mechanic, shipped in 2015, ten years before "PvZ 3: Evolved" made merging its headline
feature — and it is the same mechanic this repo's base game is built around.*

**Currencies — at least ten (computed)**: Gold Coins, Diamonds, **Gold Gems** (hard currency), Plant
Coins, Mystical Plant Coins, Adventure Medals, Brave Medals (arena), Medals of Honor (guild),
Experience Beans, Wishing Stones, plus Slot Coins.

**Gacha — FACT [2nd-tier]**: Mysterious Card Pack (legendary rewards) · **Super Card Flip — 10 free
per day, more for gold gems** · Slot Fun · **Plant Gifts — 50 gold gems per wheel roll** · Vasebreaker
reward vases · bar draw · fragment-exchange recruitment.

**VIP — and this is the sharpest monetization fact in the franchise.** **FACT [2nd-tier]** — a tiered
subscription. Snow Pea, Squash and Tall-nut were VIP-premium; **Winter Melon required Tier 3
membership**; and VIP subscribers were **auto-granted a 7th seed slot, permanently assigned to Winter
Melon**. **That is a paid lane-slot advantage** — the exact axis PvZ 1 priced at $105,750 of in-game
coins and never sold for money.

**The finding: PvZ Online ran ten plant-power tracks and eleven currencies simultaneously for three
years, then was switched off in a single day with no published reason.** (computed / FACT.) Any one
plant's effective power was the product of ten independent curves plus a deck-of-seven composition.
**INFERENCE — this is the far end of the design space, and there is no public post-mortem.**

**What problem it solves.** Effectively unbounded engagement: there is always a track with room in it.
**What it costs the designer.** Ten curves means ten balance surfaces and ten-way interaction. Any
content's difficulty must be authored against the product of all of them.
**What breaks when tuned wrong.** The curves desynchronise. A player maxed on three tracks and empty
on seven is either overpowered or hard-walled, and there is no single number to look at.

### 4.3 PvZ All Stars (China, 2014–2020)

**FACT [2nd-tier]** — betas from **3 July 2014**; released **17 September 2014** (iOS) / **25
September 2014** (Android). Shut down **January 2017** (iOS) and **11 July 2020** (Android).
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies:_All_Stars

Systems: plant **star upgrades** via Puzzle Pieces + coins; plant **levels** via sun; **tier
promotion** via potions; **equipment with 2-piece and full-set bonuses**, rarity-coded; a **three-tier
evolution chain** (Peashooter → Repeater → Gatling Pea) — *"the first, and currently only, game to
have an evolution system"*; currencies Coins / Sun / Gems / Tickets / Tacos / Potions / Puzzle Pieces;
gacha with **3 free ticket pulls daily** and a gem pull **every 48 hours**.

**The finding: All Stars turned PvZ 1's two-tier *tile-compression* upgrade into a three-tier
*ownership* upgrade, and the meaning inverted.** (INFERENCE.) In PvZ 1, Repeater → Gatling Pea is a
decision made **inside a match**, costs sun, and buys board space. In All Stars the same chain is a
decision made **outside a match**, costs collectible currency, and buys a number. Same fiction,
opposite mechanic. **The chain that made PvZ 1 tight is exactly what made All Stars a collection
game.**

### 4.4 PvZ 2 — a full progression economy grafted onto a finished tower defense

**FACT** — released free-to-play **15 August 2013**. https://en.wikipedia.org/wiki/Plants_vs._Zombies_2

**Every RPG system in PvZ 2 was bolted on years after launch.** **FACT**,
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_2/Update_history:

| System | Update | Date |
|---|---|---|
| **Plant upgrade system + Piñata Quests** | 5.7.1 | **10 January 2017** (caps 3/4/5) |
| Piñata Hunt | 5.9.1 | 14 March 2017 (caps raised to 6/9/12) |
| **Battlez** (PvP) | 6.6.1 | **13 March 2018** (caps settle at 10/15/20) |
| **Mastery levels** | 7.0.1 | **8 November 2018** (cap **M200**) |
| Battlez renamed **Arena** | 7.5.1 | — |
| **Penny's Pursuit** | 7.9.1 | soft **7 February 2020**, wide July 2020 |

**(computed)** The plant upgrade system arrived **~3 years 5 months** after launch; Mastery **~5 years
4 months** after. The game shipped as a pure tower defense and had a vertical-progression economy
grafted on mid-life.

**Cost to max a plant, pre-Mastery** — **FACT [2nd-tier]**
https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system:

| Max level class | Seed packets | Coins |
|---|--:|--:|
| 20 | **5,788** | **354,750** |
| 15 | **9,810** | **426,000** |
| 10 (Adventure Mode plants) | **7,185** | **291,000** |

⚠ **Note the anomaly**: max-15 plants cost **more** than max-20 plants on both axes. Either the
per-level curves differ by class or the wiki totals are wrong. Flagged; do not model from these.

**Mastery costs, from raw wikitext** — **FACT**,
https://plantsvszombies.wiki.gg/api.php?action=parse&page=Template:MasteryUpgrades&prop=wikitext&format=json

| M | Packets | Coins | M | Packets | Coins | M | Packets | Coins |
|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| 1 | 10 | 0 | 11 | 20 | 1,000 | 21 | 30 | 3,000 |
| 5 | 50 | 250 | 15 | 60 | 2,000 | 25 | 70 | 4,000 |
| 10 | 100 | 1,000 | 20 | 110 | 3,000 | 30 | 120 | 6,000 |

Reference points: **M50 = 140 packets / 20,000 coins · M100 = 190 / 20,000 · M200 = 290 / 20,000.**

**The Mastery packet curve is a sawtooth, not a monotone.** (computed) Within each 10-level band
packets climb, then reset to a floor that ratchets up by 10 per band (10→100, then 20→110, then
30→120), while the coin cost climbs monotonically. **INFERENCE — the sawtooth exists so that every
band starts with a cheap, quickly-achieved level.** The band boundary is where a player would
otherwise quit; the design puts the smallest number there.

**What Mastery buys** — **FACT [2nd-tier]**: **Damage Pierce +1%/level (max +200%)**, **Toughness
+10/level (max +2,000)**, and **Chance to Boost +1% per 10 levels, capped at 21%** (a free Plant Food
proc on planting). **Eight plants are excluded from Mastery entirely**: Gold Leaf, Gold Bloom, Thyme
Warp, Intensive Carrot, Perfume-shroom, Power Lily, Power Mints, Tile Turnip.

**Seed packet acquisition** — **FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Seed_packet:
piñatas, Travel Log quests, **up to 5 piñatas per level replay**, or purchase with gems/coins/real
money. Adventure-mode plants unlock at **10, 40, 60 or 100 packets**; others at **100–250**.

**Arena — and this is the finding that matters most.** **FACT [2nd-tier]**
https://plantsvszombies.wiki.gg/wiki/Arena: 8 leagues (Soil → Wood → Brick → Iron → Bronze → Silver →
Gold → Jade), **zombie level = league index**. Last Stand format, **5 plants with one slot forced to
the weekly featured plant**. Zone scoring 100% / 60% / 40% / 20%. Entry via Gauntlets bought with
gems; **1 free play every 4 hours** plus **up to 4 free gauntlets/day from ads**; **20 gems** to
preserve a win streak. Win = 5 Crowns, loss = 1, surrender = 0; top 3 promote, last demotes. Jade 1st
place pays **270 gems + 120 mints**; Soil pays **60 gems + 20 mints**.

> **In Arena, almost every player upgrade is stripped out.** Only Instant Recharge, Wall-nut First Aid
> and Mower Launch survive; Plant Food purchases and Power-Ups are disabled.

**INFERENCE, and it is the sharpest single data point in this document: PopCap built a vertical
progression economy and then deliberately excluded it from their own competitive mode.** That is a
first-party admission, in shipped code, that the progression layer and a fair match do not coexist.
The same page also notes there is *"no real matchmaking system"* and that opponents *"seem to be bots
that pull their performance directly from other players"* — so even the PvP framing is a facade over
a solo score chase.

**Penny's Pursuit — the energy gate.** **FACT [2nd-tier]**
https://plantsvszombies.wiki.gg/wiki/Penny%27s_Pursuit: online-required; **5 fuel per level attempt**;
three difficulties awarding **20% / 25% / 30% ZPS**; fill a **100% ZPS meter** for up to **3 Dr.
Zomboss fights**, on a **12-hour reset**; one free perk per level, extras **5 gems each**. First-clear
rewards 1,000–2,000 coins + 10–15 seeds + 4 gems.

**The Zen Garden's payout changed.** **FACT [2nd-tier]** — PvZ 1's garden pays **coins**. PvZ 2's
garden pays **Plant Food boosts**: zombies drop sprouts, plants have **two growth stages**, stage one
pays 5 silver coins (5 gold — **500** — for Marigolds) and stage two grants a **Plant Food boost
usable in one level**. Bernie the Bee cuts growth times by **10–50%**.
https://plantsvszombies.wiki.gg/wiki/Zen_Garden_(PvZ2) — **the same feature moved from an economy sink
to a power faucet.**

### 4.5 PvZ 2 Chinese — the same name, the opposite ladder

**FACT** — https://www.taptap.cn/moment/427773268418626266. The Chinese build uses **阶 (tiers)**; the
international build uses **levels**. They are not reskins:

| | International levels | Chinese 阶 |
|---|---|---|
| Cap | 10 / 15 / 20, then Mastery to **M200** | most plants **4阶**, newer ones **5阶**; instants cap at **3阶**; some support plants have **no 阶 at all** |
| What a step buys | **lower sun cost, shorter recharge**, boosted exclusive ability | **higher attack/defense**, **higher special-ability trigger probability**, and **entirely new skills** |

**INFERENCE — these optimise for opposite things. The Chinese ladder is a power ladder; the
international one is an efficiency ladder.** The Chinese design is much closer to a conventional
gacha-RPG.

**Tier-5 cost — FACT** https://m.zol.com.cn/article/10542365.html: **500,000 coins + 80 fragments + 10
bottles of the matching-colour cultivation liquid.** Duplicate-plant fragment conversion by current
tier: 1阶 → 10 · 2阶 → 40 · 3阶 → 90 · 4阶 → 140 · 5阶 → **220 fragments**.

**Monetization gap — FACT** https://news.tongbu.com/m/70216.html: international premium plants **6
plants = ¥108**, all one-time purchases **¥180 total**; Chinese **8 diamond-only plants = ¥256**, with
the source estimating total spend at *"3–5× the original."* **(computed)** ¥256 / ¥108 = **2.37× on
the premium-plant line alone**.

**Three further Chinese-only conversions — FACT** (TapTap, above):
1. **Costumes give stat bonuses in the Chinese build** and are **purely cosmetic internationally**. A
   straight cosmetic → power conversion.
2. Chinese players **spend diamonds to buy sun and Plant Food mid-battle**; international players use
   coins and **cannot buy sun at all**.
3. Diamonds **do not drop in battle** in the Chinese build; they **do** internationally.

**The finding: the same tower defense, shipped in two markets, ends up with two incompatible
progression philosophies — and the one that sells power for money also sells the in-match resource
itself.** (INFERENCE.) PvZ Adventures' VIP plants (0 sun) and PvZ Online's VIP seed slot are the same
move. **Every PvZ title that monetized aggressively eventually sold access to sun or to slots — the
two things PvZ 1 held sacred.**

---

## 5. PvZ 3 — seven years, six public builds, and no global launch

**FACT** — as of September 2026, PvZ 3 is in **soft launch in Ireland and the Philippines only**,
with a worldwide release stated as "later in 2026." The widely-reported 2024 global launch was
**announced and then cancelled**. https://en.wikipedia.org/wiki/Plants_vs._Zombies_3:_Evolved ·
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_3:_Evolved

| Phase | Dates | Regions |
|---|---|---|
| Development start | ~2016 | — |
| **Pre-alpha** | **16 July 2019 – 10 Feb 2020** | US/Canada |
| **Soft launch v1** | **25 Feb 2020 – 18 Nov 2020** | Philippines, Romania, Indonesia |
| **Soft launch v2** — "redone from scratch" | **7 Sept 2021** | Australia, Philippines |
| **Soft launch v3** | **5 April 2022** | + Netherlands |
| **Soft launch v4** | **12 October 2022** | — |
| **Rebrand: "Welcome to Zomburbia"** | **17 January 2024** | UK, NL, AU, PH, IE |
| — IAP off, delisted | **15–16 October 2024** | — |
| — servers off | **15 November 2024** | — |
| **Rebrand: "Evolved" — Seedling beta** | **7 Oct – 25 Nov 2025** | select |
| **Sprout Build soft launch** | **7 April 2026** | Ireland, Philippines |
| Current client | **28.1.19** | — |

Sources: https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_3_(Pre-2021) ·
https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_3:_Welcome_to_Zomburbia ·
https://www.gamedeveloper.com/business/plants-vs-zombies-3-is-shutting-down-for-major-overhaul-

**(computed)** From pre-alpha (July 2019) to today: **7 years 2 months in public testing without a
global launch**, across at least **6 distinct public builds** and **2 full rebrands**.

### 5.1 Era 1 — the 2019/2020 build, and what actually caused the backlash

**FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_3_(Pre-2021):
- **Board: 5×8 grid, 5 lanes, portrait orientation.** The lane count was preserved; the **column
  count and the screen axis** changed.
- **Sun became automatic and small-integer.** *"Sun is generated automatically by Sunflower at a rate
  of 1 for every 3 seconds."* Plant costs collapsed to match: **Peashooter 4 sun / 5 s recharge;
  Sunflower 2 sun / 8 s recharge** (https://plantsvszombies.wiki.gg/wiki/Plants_(PvZ3)). A "Sun Boost"
  added **+2 sun at level start**. **(computed)** the sun magnitude fell by roughly **25×** — from
  25/50/100 to 1/2/4.
- **"Taco Time"** split each level into two active phases separated by a passive interval where **no
  zombies spawn but no sun is generated** — a free planting window with "Tacobilities."
- Piñata-based plant levelling carried over from PvZ 2.

**FACT** — the backlash targeted five things: **(1)** 2D → 3D art, **(2)** portrait orientation,
**(3)** non-linear progression, **(4)** the sun-system overhaul, **(5)** unfair difficulty. PopCap
*"decided to redo the game from scratch."* https://en.wikipedia.org/wiki/Plants_vs._Zombies_3

**Correction worth stating plainly: the lane count was never reduced.** Every documented PvZ 3 board
is **5 lanes** — 5×8 in 2020, 5×12 in Evolved. **INFERENCE — "fewer lanes" is a community perception
produced by the portrait framing, which shortens the visible horizontal runway, not a change to the
lane count.** The design lead is reported to have chosen portrait *"to make it more phone-friendly"*
(second-hand via wiki.gg/Rappler; the original was not fetchable).

### 5.2 Era 2 — the 2021 and 2022 rebuilds

**Effectively undocumented.** wiki.gg treats these as timeline points with no mechanical detail. Lane
count, orientation, sun rules and monetization for the September 2021, April 2022 and October 2022
builds are all **NOT FOUND**. Three public builds with no surviving spec.

### 5.3 Era 3 — "Welcome to Zomburbia" (January–November 2024)

**FACT [2nd-tier]** https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_3:_Welcome_to_Zomburbia:
- **Orientation: portrait.** Grid dimensions **NOT FOUND**.
- **Sun: still the small-integer economy.** Sun Boost grants **+2 sun at level start**, unlocked at
  **Dave's House level 15**.
- **22 plantable plants — 17 via seed packets, 5 as power-ups.** Recharge **4 s to 40 s**. Power-ups
  (Grapes of Wrath, Cherry Bomb, Jalapeño, Chilly Pepper) fire off a **meter**, not sun.
- **The energy gate: "Taco Tickets."** Earned **only by completing levels**, spent to **advance the
  story**. https://plantsvszombies.wiki.gg/wiki/Taco_Ticket
- **Town meta: "Rebuild Neighborville"** — explore, renovate, decorate, gated on a **Dave's House**
  XP track; marketed as *"match-3 inspired customization."*
- Three areas with Hard variants; each clear pays **Coins + 1 Taco Ticket**.

**INFERENCE — Taco Tickets invert the usual mobile energy gate.** Rather than metering *how often you
can play*, they meter *how fast the story advances*. You may always battle; battles are the only
faucet for narrative progress. **That is a grind gate, not a session gate** — and it is a genuinely
different design from every other energy system in this document.

**FACT — why it was pulled.** The nine-month soft launch drew an *"unenthusiastic response,"* with
players criticising **"poor technical performance and off-putting aggressive pay-to-win mechanics."**
EA's blog said only *"We're excited to re-tool the game and bring you an even better PvZ3 experience
soon!"* with **no relaunch date**.
https://www.gamedeveloper.com/business/plants-vs-zombies-3-is-shutting-down-for-major-overhaul-

### 5.4 Era 4 — "Evolved" (October 2025 → )

**FACT** — the board is a **5×12 grid** (Wikipedia states it; wiki.gg asset filenames corroborate —
`PvZ3_Neighborville_5x12.png`). **(computed)** that is **60 tiles against PvZ 1's 45**, a **33% larger
board at the same 5 lanes**, and **50% longer than the 2020 build's 5×8**.

**FACT** — **sun is back to classic behaviour**: it *"falls from the sky or from sun-producing plants
like Sunflowers,"* plus Sun-Shrooms for dark tiles. **The 2020 auto-sun experiment has been
reverted.** https://en.wikipedia.org/wiki/Plants_vs._Zombies_3:_Evolved

**FACT** — a level is multiple flag-marked stages; **Mo the Smart Lawn Mower** clears the lane on the
first breach; **a second breach loses the level.**

**The headline system is merging.** **FACT** — *"Two Tier I plants can be combined to form new plants
and two Tier II plants can merge again."* Merge by dragging a placed plant onto an eligible one, or
by planting a new one onto it. Documented chains: Peashooter + Peashooter → Repeater; Repeater +
Repeater → Gatling Pea.

**Roster** — **FACT [2nd-tier]**, from wiki.gg raw wikitext: **13 Tier I base plants, 30 Tier II, 19
Tier III**. ⚠ A separate wiki.gg page read gave **32 Tier II / 16 Tier III**; the raw-wikitext figures
are the better source and the discrepancy is flagged. **(computed) 62 total plant forms from 13
authored bases — a 1 : 4.8 multiplier.**

**INFERENCE — this is the franchise's third distinct answer to "make many units from few."** PvZ 1
used a 1:1 upgrade pair. Garden Warfare authored ~8 variants per class. PvZ 3 combines pairs. Note
that 30 Tier II from 13 Tier I is far short of **C(13,2) = 78**, so **the merge table is hand-authored,
not combinatorial** — the multiplier is a curated subset, and the curation is the design work.

**Also note**: PvZ Online shipped hybrid planting — Peashooter onto Peashooter yields a Repeater — in
**2015** (§4.2). **The merge mechanic is not new to the franchise; Evolved is its third appearance and
its first as a headline feature.** A second-tier report of May 2025 leaks suggests a "Fusions" mod may
have influenced the official implementation — unverified, and flagged, but worth noting given that
this repo's own base game is exactly such a mod.

**Structure and monetization** — **FACT [2nd-tier]**: comic-book "issues" of **25 levels each**, **6
issues** planned across **5 story chapters**; plus **Expeditions** (themed 25-level challenges gated
on adventure level), **Lawn Patrol** (limited-time seasonal), **Zen Garden**, **Odd Jobs**,
**Almanac**. Monetization is a seasonal **"Plant Pass"** yielding plants, cosmetics and vanity items,
plus a store selling virtual currency for *"random item selection."* Coins and Gems appear as
expedition rewards. **No energy/lives system is documented for Evolved.**

**The finding: PvZ 3's instability is a board problem, not a progression problem.** (INFERENCE.) Of
the five criticisms of the 2020 build, three — portrait orientation, the sun overhaul, unfair
difficulty — are about the match itself, and the sun overhaul has since been reverted outright. The
franchise has repeatedly demonstrated it can bolt a meta onto the lawn. What it has not managed since
2009 is to change the lawn.

---

## 6. The Zen Garden and the collection meta across titles

| Title | The non-combat loop | What it pays | Timer |
|---|---|---|---|
| **PvZ 1** | **Zen Garden** — 32 main slots + Mushroom Garden 8 + Aquarium Garden 8 = **48 plants** | **coins** — $10/watering, $50 sprout→small, $100 small→medium, **$2,000** medium→full (Marigolds $1,000) | **30–60 min real time** between waterings; 5 growth phases each needing **3–5 waterings** + fertilizer |
| **PvZ 2** | Zen Garden, reduced | **Plant Food boosts** (combat power) and 5 coins per stage | Bernie the Bee cuts growth time **10–50%** |
| **PvZ Adventures** | town buildings + planter boxes | coins on a timer; **plants themselves** | **1 min – 18 hours** per plant grown |
| **PvZ Online** | 8 plant-development buildings, 7 challenge buildings, 9 social features, Alliance | ten currencies, gacha pulls, plant power on ten tracks | daily reset; **20:00–20:30 PvP window** |
| **All Stars** | gacha + equipment sets | plant power | 3 free ticket pulls/day, 1 gem pull/48 h |
| **GW2** | **Backyard Battleground** hub, Quest Board, Flag of Power, treasure chests | coins → sticker packs; **stars** unlock chests | daily quests; **Rux rotates fortnightly** |
| **BfN** | **Prize Map** | Prize Bulbs → map nodes → Rainbow Stars | **monthly** rotation, **1 bulb / 4,000 XP**, cap 50 |
| **PvZ Heroes** | Hero Quests, the collection itself | 40 gems + 500 sparks + 2 cards + 1 pack **per hero**; free pack after 3–5 losses | quest ladder, no hard timer |
| **PvZ 3 (Zomburbia)** | Rebuild Neighborville | Taco Tickets → **story progress** | earned only from levels |
| **PvZ 3 (Evolved)** | Zen Garden, Odd Jobs, Almanac, town decoration | cosmetics + Sun Boost | Plant Pass season |

Sources as cited in the sections above.

**Three findings.**

1. **PvZ 1's garden is the only one that pays a currency which cannot affect combat.** (computed from
   §1.3 — the shop's combat items are convenience upgrades and none is a stat.) Every later garden
   pays power: Plant Food, plant levels, sticker packs, upgrade points, growable plants. **The moment
   the collection loop pays power, it stops being optional.**
2. **The garden is where every PvZ title puts its real-time timer.** PvZ 1's 30–60 minute watering
   cooldown is the only wall-clock gate in a game that otherwise has none. Everything after it — 18-hour
   crop timers, daily gacha pulls, fortnightly Rux, monthly prize maps, 12-hour Penny's Pursuit
   resets, seasonal passes — is the same idea at a longer period.
3. **(computed)** The PvZ 1 Marigold loop nets **$1,290–1,350 per plant** against a **$2,500** buy-in
   and a multi-hour real-time cycle, **capped at 3 sprout purchases per real calendar day**. That is a
   deliberately *bad* hourly rate. https://plantsvszombies.wiki.gg/wiki/Zen_Garden_(PvZ) — **the garden
   is not an efficient way to earn; it is a reason to open the game.**

---

## 7. What the franchise learned about adding depth

### 7.1 What survived, what was dropped

| System | Introduced | Survived into | Verdict |
|---|---|---|---|
| **Sun as the only in-match resource** | PvZ 1 | every title including PvZ 3 Evolved | **Kept, and every attempt to change it was reverted.** PvZ 3's 2020 auto-sun overhaul was named in the criticism that got the build scrapped; Evolved put sky-sun back. |
| **5 lanes** | PvZ 1 | Heroes (5), PvZ 3 (5×8 then 5×12) | **Kept, never once changed.** The lane count is the franchise's actual constant — not the board size, not the orientation. |
| **Two-tier upgrade plants** | PvZ 1 | PvZ Online hybrid planting (2015), All Stars 3-tier evolution, PvZ 3 Evolved 3-tier merge | **Kept in fiction, mutated in mechanism** — in-match tile compression → out-of-match ownership → back to in-match merge. |
| **Zen Garden** | PvZ 1 | PvZ 2, All Stars, PvZ 3 | **Kept**, but its payout changed from coins to power. |
| **A coin shop that cannot break the game** | PvZ 1 | nothing after it | **Dropped.** Every later shop sells power, and the aggressive ones sell sun or seed slots. |
| **Puzzle re-framings (Vasebreaker, I Zombie)** | PvZ 1 | nothing | **Dropped.** The cheapest depth the franchise ever built was never repeated. |
| **Seed-slot scarcity as the core constraint** | PvZ 1 | Heroes (40-card deck, 4-copy cap) | **Kept, transformed.** The deck *is* the seed slot list. |
| **Keyword vocabulary** | Heroes | nothing after it | **Frozen.** No new card since January 2018 — but still balance-patched in 2025. |
| **Class pairs / class colours** | Heroes | nothing | **Dropped.** |
| **Character variants** | GW1 | GW2 | **Dropped in BfN.** 121 → 20. |
| **Modifier / upgrade budget** | GW2 (3 of 8) | BfN (7 points) | **Kept and scaled — and it balanced worse.** |
| **Loot-box acquisition** | GW1 stickers | GW2 stickers | **Dropped in BfN** for deterministic Prize Maps. |
| **Real-time production timers** | Adventures (1 min – 18 h crops) | nothing | **Dropped**; the game shut down after 17 months. |
| **Multi-track plant power** (level + star + gear + awakening + reagent…) | PvZ Online (10 tracks), All Stars | PvZ 2 (levels + Mastery only) | **Cut from 10 tracks to 2.** |
| **PvP** | Adventures / Online / All Stars / PvZ 2 Arena / Heroes | Heroes only, as the whole game | **Mostly dropped** — and where it survived in PvZ 2, **the progression system was stripped out of it.** |
| **Energy gates** | Adventures (crops) → PvZ 2 (5 fuel/level) → PvZ 3 (Taco Tickets) | still present | **Kept, and reshaped** from session gate to grind gate. |

### 7.2 The eight lessons, stated as findings

**1. Depth that adds nouns compounds; depth that adds numbers inflates.**
Keywords, tribes, environments, lanes, elements, ability slots, vases — each is a new thing the board
can hold, and each multiplies against the others without a rebalance of what already exists. Plant
levels, star ranks, reagents, gear sets, Mastery — each is a new multiplier on the same number, and
each forces every piece of content to be re-authored against a wider range. **Every title that shut
down had many of the second kind. The one title still praised for its tuning has almost none.**

**2. A closed vocabulary is worth more than a large one.**
PvZ Heroes' ~17 keywords cover 503 cards. Garden Warfare's ~7 variant levers cover 121 characters.
PvZ 1's 8 upgrade pairs cover a 49-plant roster. PvZ 3's 13 base plants cover 62 forms. **(computed)**
In all four the vocabulary-to-content ratio sits between **1:5 and 1:30**, and in all four the
vocabulary was frozen early. Adding an eighteenth keyword is not 1/17 more work; it is a check against
seventeen existing ones.

**3. Exhaust the lattice before adding an axis.**
The PvZ Heroes hero roster is exactly C(5,2)=10 pairs per side plus one duplicate. Nobody had to ask
"what hero next" — they asked "which cell is empty." **A closed combinatorial space tells you when you
are finished.** Compare PvZ Online's ten open-ended upgrade tracks, which could never be finished and
were switched off instead.

**4. Trade-off balancing self-corrects; budget balancing does not.**
Every Garden Warfare variant pays for its gain in a printed number. BfN's point-budget upgrades are
mostly pure gains, so the correct build is just the best points-per-value set — and the documented
complaint is exactly that. **121 authored characters were more tuneable than 21 modifier rows, because
the characters were cost-balanced individually and the rows were not.**

**5. Rarity should buy text, not stats.**
Measured over the whole PvZ Heroes card table: stat-per-sun is flat (1.47–2.29) across all six
rarities while rules text grows 20 → 70 characters. **(computed)** That single decision is why a
frozen card pool could be balance-patched for eight years after the last set. A rarity tier that buys
efficiency is a power ladder wearing a collection costume, and it has to keep printing to stay alive.

**6. Difficulty should be elastic without being scaled.**
PvZ 1's next wave arrives when you have killed 35–50% of the current one, with the threshold rerolled
per wave — no stat is touched, the pacing simply compresses for strong play (**FACT**, `spawn.cpp`).
That mechanism is *invisible*, *unpurchasable*, and *un-inflatable*, and it is the main reason the
game is remembered as well-tuned.

**7. An endless mode built on a fixed array has a ceiling in the array.**
PvZ 1's Survival Endless is 20 waves × a hard-coded **50 zombies** (**FACT**, `MAX_ZOMBIES = 50`).
Once the type pool saturates those 50 slots, the mode stops getting harder in the only dimension it
scales, and the +50-sun upgrade escalator was bolted on precisely because the spawn system had run out
of room. **If "endless" is a design commitment, the spawn representation has to be unbounded before
anything else is.**

**8. If your competitive mode has to switch the progression off, the progression is not balanced —
it is tolerated.**
PvZ 2's Arena strips out nearly every plant upgrade the player has spent years earning. That is a
first-party verdict on the retrofit, delivered in code rather than in a blog post, and it is the
strongest single piece of evidence in this document.

### 7.3 The two things the franchise never solved

**The board.** (INFERENCE, from §5.) Seventeen years, eight titles, and every attempt to change the
lawn itself — portrait orientation, 3D art, automatic sun, non-linear level progression — has been
reverted or scrapped. Progression systems were added and removed freely; the match was not negotiable.
**Whatever else the record says, it says the depth belongs *around* the lawn, not *inside* it.**

**The line between monetization and the core resource.** (INFERENCE, from §4.) PvZ 1 priced seed slots
at $105,750 in coins and never sold them for money; sun could not be bought at all. Every aggressively
monetized entry eventually crossed that line: Adventures sold plants that **cost 0 sun**; PvZ Online
sold a **7th seed slot** with a VIP subscription; PvZ 2 Chinese sells **sun and Plant Food mid-battle
for diamonds**. **Those are the two things PvZ 1 held sacred, and selling either is the clearest marker
in the record of a title in trouble.**

---

## 8. Hooks for this project

**Non-normative and un-vetted.** These are observations that happen to touch systems this repo already
has. None is a recommendation, none has been checked against the code, and none should be treated as a
design decision. They are here so that a later design session has somewhere to start reading.

- The PvZ Heroes keyword-to-class ownership matrix (§2.3) is the same shape as a closed effect-atom
  vocabulary partitioned across classes. The measurable claim worth testing against our own data: in
  Heroes, **every combat keyword is owned outright or near-outright by one or two of five classes, and
  the only widely shared keyword is the one that changes board geometry rather than combat.**
- The Heroes rarity finding — flat stat-per-sun, monotonically growing rules text — is a directly
  testable property of any rarity ladder, ours included, and computable from an item/container table
  in one pass.
- `spawn.cpp`'s HP-threshold early-trigger is an elasticity mechanism costing no stat scaling and no
  configuration surface: measure the live wave's remaining HP, compare to a per-wave randomised
  fraction of its initial HP, compress the countdown. It is roughly fifteen lines.
- `MAX_ZOMBIES = 50` is a worked example of the "no hard progression ceilings" rule being violated by
  a *representation* rather than by a constant with a suggestive name — a fixed-size spawn array, not
  a `const int MaxDifficulty`.
- Three measured data points on "how many concrete units per authored template is sustainable":
  Garden Warfare's ~7 closed levers → 121 characters (1 : 7.6); PvZ 3's hand-curated merge table →
  62 forms from 13 bases (1 : 4.8); PvZ Heroes' 17 keywords → 503 cards. All land between 1 : 5 and
  1 : 30.
- The BfN retreat is the clearest available evidence that **budget-balanced modifiers converge while
  trade-off-balanced units do not** — relevant anywhere a system offers points to spend rather than
  packages with printed costs.
- PvZ 2's Arena stripping out its own upgrade system is the sharpest available cautionary case for any
  design that pairs a persistent power ladder with a fair-fight mode.
- PvZ Online shipped plant-onto-plant merging in 2015 and PvZ 3 made it the headline in 2025 — the
  same mechanic this repo's base game is built around. Both official implementations use a
  **hand-curated** merge table far smaller than the full combinatorial space.
- PvZ 1's shop is a worked example of an economy whose sinks cannot affect the difficulty curve: the
  two most expensive purchases are seed slots ($105,750) and a garden ($96,700), and neither is a stat.

---

## 9. What I could not find

This section is mandatory and non-empty by design.

1. **Whether PvZ Heroes' sun/brain ramp caps.** Every source reached says only "+1 per turn." None
   states a cap at 10 or any other value; the wiki.gg page is explicit that it does not specify one.
2. **The per-hero class-pair table for all 22 PvZ Heroes heroes.** I have the *structure* — C(5,2)=10
   pairs per side, 11 heroes per side, one duplicated pair per side, both duplicates named. The one
   page that claimed a full roster returned class names that do not exist in the game ("Warrior",
   "Shadow", "Aquatic", "Freeze"), so it was discarded rather than printed. `wiki.gg/wiki/Hero` 404s.
3. **PvZ Heroes' exact most-recent balance-patch date.** June 2025 is the latest *confirmed* EA
   balance post; client builds continued to 1.66.7 (June 2026) but whether those carried balance
   changes is unestablished. The wiki's own 2024–2026 update history is unwritten.
4. **Published pack drop rates** for PvZ Heroes or Garden Warfare sticker packs. Never published by
   EA or PopCap for either game. Only guarantee floors ("≥1 Rare") exist.
5. **PvZ 2's per-level (L2→L20) seed-packet and coin table.** wiki.gg hosts only totals — confirmed by
   inspecting the page's template list — and the Fandom mirror is 402-blocked. The Mastery table was
   recoverable; the base-level table was not. Compounding this, the recovered totals are internally
   inconsistent (max-15 plants cost more than max-20 on both axes), so even the totals are suspect.
6. **PvZ 1's Adventure-mode wave point budget.** The reverse-engineered emulator models **Survival
   Endless only**, where the count is a hard 50 per wave. Community sources describe an Adventure
   point system (basic zombie 1 point, Conehead 2, Pole Vaulter 3, Buckethead 4, Gargantuar 10; level
   budgets of 1000/1500/2000/3000/3500/4000) but nothing verified it, so it is used nowhere above.
7. **A first-party statement giving the rationale for dropping Garden Warfare variants.** EA's pages
   describe *what* BfN's upgrade system is; none says *why* variants went. The "121 characters was
   unbalanceable" reading is well attested but only in community sources.
8. **George Fan's ten techniques, individually named.** I confirmed there are ten and recovered
   several (new mechanics ~every 5 levels; one tool taught across four contexts; constrained starting
   resources so the player discovers the economy). The GDC Vault video and the Medium sketchnote both
   refused fetch (403). The full enumerated list is not in this document.
9. **PvZ Adventures' player level cap, XP curve, plant gear and crafting.** No source documents a
   level cap. Gear and crafting appear simply not to have existed — but that is proving a negative
   about a dead 2014 Facebook game. Two sources also disagree on the loadout size (5 vs 5–7 plant
   types), unresolved.
10. **A stated reason for the PvZ Adventures or PvZ Online shutdowns.** Neither exists in the
    reachable record. Both games simply stopped. A broader EA statement about Facebook-game culls
    (*"the number of players and amount of activity has fallen off"*) surfaced only as a search
    snippet and could not be confirmed to apply to PvZ Adventures specifically.
11. **PvZ Online's numeric detail.** I have the full *list* of its ten plant-power tracks and eleven
    currencies but almost no numbers: no level cap beyond "at least 55", no star-rank costs, no gacha
    rates. The game has been off since 2018 and its site was deleted.
12. **A "Great Wall" territory mode in PvZ Online.** No such mode appears in any reachable source. The
    closest real analogues are Map Mode (station plant decks on nodes to harvest resources) and
    Across-Service Expedition. The name most likely refers to 植物大战僵尸长城版, a well-known Chinese
    *fan modification of PvZ 1*, not a Tencent product. Flagged as unverified.
13. **A gem/socket system in PvZ 2 Chinese.** No source documents one. The stacked-enhancement systems
    (阶, cultivation liquid, talents) exist; sockets appear to be a PvZ Online card-mode concept
    misattributed to PvZ 2 CN.
14. **All mechanics for the 2021 and 2022 PvZ 3 rebuilds** — three public builds, effectively
    undocumented; and **PvZ 3 Zomburbia's grid dimensions**, and **PvZ 3 Evolved's screen
    orientation** (inferred from the 5×12 grid, not stated anywhere).
15. **Verbatim, complete EA/PopCap statements** for the October 2024 PvZ 3 shutdown, the "2024 in
    Review" post, and the Evolved early-access announcement. Every ea.com news URL is client-rendered
    and returns the franchise hub to a fetch; `forums.ea.com` returns 403; `web.archive.org` is
    blocked in this environment.
16. **Whether any Facebook or social PvZ build other than Adventures existed.** Nothing surfaced.

**Method note.** This session exhausted its 200-call web-search budget partway through. Everything
after that point was gathered by direct fetch against URLs already in hand — including MediaWiki
`api.php` raw-wikitext pulls, which are how the Mastery table in §4.4 was recovered — plus two
datamined datasets parsed locally. Several gaps above (2, 3, 5, 14) are search-shaped and would close
with more search budget rather than more effort. `fandom.com` returned HTTP 402 to every fetch
attempt throughout; `tcrf.net`, `answers.ea.com`, `forums.ea.com`, `gamespot.com`, `pocketgamer.biz`,
`toucharcade.com`, `reddit.com`, `moegirl.org.cn` and `web.archive.org` all refused with 403 or 429.
