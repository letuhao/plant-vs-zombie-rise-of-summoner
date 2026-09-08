# Plants vs. Zombies 2 (international / EA release) — progression and mechanics inventory

Research note. Evidence only — no proposals, no design.
Compiled 2026-09-02. Game version referenced by most sources: 10.x–11.x (the wiki plant count cites update 10.9.1).

## The finding in one paragraph

PvZ2 keeps the original game's tactical core untouched — sun, a seed bank, five lanes, waves — and bolts a
free-to-play RPG economy onto the *outside* of it. Sun stays the in-match difficulty dial and is never
inflated; instead the designers added three orthogonal layers that each solve a different retention problem.
**Plant Food is a per-match burst currency with a hard carry cap of three, which turns every plant into two
plants — a sustained one and a scripted one-shot — for the cost of one seed slot.** **Plant levelling is a
long, coin-and-packet sink that buys mostly linear stat drift (damage, hitpoints, recharge, and at the top a
percentage chance to fire Plant Food for free), then continues for another 200 Mastery levels of pure
+1%-per-level with no new behaviour at all.** **Level objectives ("don't lose more than two plants",
"produce at least 5,000 sun", "never have more than 16 plants") and per-world tile gimmicks are the content
multiplier: the same 188 plants and the same zombie roster get re-used across roughly a dozen distinct rule
sets, which is far cheaper than authoring new units.** The endgame is entirely repeatable content — 11
Endless Zones, a PvP-by-score Arena with eight leagues, and a weekly Penny's Pursuit whose difficulty knobs
are literally *starting sun*, *sun dropper rate*, *maximum sun*, *seed slots*, *lawn mowers* and *first wave
delay*. That last list is the strongest single piece of evidence in this document: when the designers wanted
a difficulty slider, they reached for the sun economy and the setup budget, not for zombie numbers alone.

---

## Sourcing and how to read it

| Tier | Source | Weight |
|---|---|---|
| 1 | Shipped data files (RTON→JSON datamines) | **Highest — actual shipped values.** One caveat below. |
| 1 | Wikipedia (release, monetisation, reception; itself press-cited) | High for history, not for numbers |
| 2 | `plantsvszombies.wiki.gg` (the community wiki fork the PvZ community migrated to) | **Second tier. Used for most numbers here because it is the only place many of them exist.** |
| 3 | Fandom mirrors, forum posts | Only where nothing better exists; flagged inline |

**The datamine caveat matters.** The only reachable decoded `PlantLevels.json` lives in a repository
explicitly named "PVZ2-Modifications" (<https://github.com/Kenny3-2/PVZ2-Modifications/blob/main/PlantLevels.json>),
i.e. some entries in it are *edited* cheat values, not shipped ones. I cross-checked before using it:

- The **sunflower** entry's `LevelXP` and `LevelCoins` arrays sum to exactly 7,185 packets and 291,000 coins
  (computed), which independently matches the wiki's stated total for a level-cap-10 plant. **Treat the
  sunflower entry as authentic.**
- The **peashooter** entry in the same file reads `Cost: [100, 25, 25, …]`, `Hitpoints: [300, 900, 900, …]`,
  `PacketCooldown: [5, 2, 2, …]`, `LevelCoins: [5, 20, 15, …]`. **That is a modded entry — do not cite it.**
  The wiki's Peashooter page gives the shipped curve instead and disagrees on every one of those numbers.

Wherever a number below is my own arithmetic over sourced values it is marked **(computed)**.
FACT vs INFERENCE is marked per claim; unmarked prose in a FACT block is sourced.

---

## 1. Plant Food

### FACT — the resource

| Property | Value | Source |
|---|---|---|
| Carry cap | **3 at first, 4 after an Ancient Egypt upgrade, 5 via in-app purchase** | [Plant Food](https://plantsvszombies.wiki.gg/wiki/Plant_Food) |
| Purchase price mid-level | **1,000 coins** (8 gems in the Chinese build) | same |
| Earned drop | Kill a **glowing green zombie**; break a Plant-Food-marked tombstone in Dark Ages; plant on a Taiji Tile (Kongfu World, China); plant a Power Lily | same |
| Carry-over between levels | **None, except in Endless Zones** where unspent Plant Food persists into the next level | same, and [Endless Zone](https://plantsvszombies.wiki.gg/wiki/Endless_Zone) |
| Side effects on use | Damaged plant is **restored to full health**; the plant is **invincible and shovel-immune while glowing** | [Plant Food](https://plantsvszombies.wiki.gg/wiki/Plant_Food) |
| Secondary use | After Wild West Day 20, dragging Plant Food onto a **recharging seed packet finishes that recharge instantly** | same |
| No effect | Single-use instants — Cherry Bomb, Jalapeno, Blover, E.M.Peach, Tile Turnip — **have no Plant Food effect at all** | same |
| Endless Zone drip | From roughly **level 20 upward, exactly one Plant Food per wave** is guaranteed from zombies | [Endless Zone](https://plantsvszombies.wiki.gg/wiki/Endless_Zone) |

### FACT — per-plant effects are hand-authored, not a formula

There is no generic "Plant Food multiplier". Each plant gets a scripted burst, and the shapes differ in kind:

| Plant | Plant Food effect | Numbers |
|---|---|---|
| Peashooter | Becomes a Gatling Pea | **60 peas in 2 seconds — 1,200 damage at level 1** (60 × 20 dmg/pea, computed) ([Peashooter](https://plantsvszombies.wiki.gg/wiki/Peashooter_(PvZ2))) |
| Sunflower | Instant sun dump | **150 sun at L1, 180 at L5, 225 at L10** ([Sunflower](https://plantsvszombies.wiki.gg/wiki/Sunflower_(PvZ2))) |
| Wall-nut | Gains metal armour | **absorbs 8,000 damage — exactly one Gargantuar smash** at L1; the armour reaches **16,000 at max level**; armour **cannot be healed by Aloe** ([Wall-nut](https://plantsvszombies.wiki.gg/wiki/Wall-nut_(PvZ2)), [Damage](https://plantsvszombies.wiki.gg/wiki/Damage)) |
| Tall-nut, Infi-nut, Endurian, Pea-nut, Pumpkin | Permanent-until-destroyed armour or stat boost | ([Plant Food](https://plantsvszombies.wiki.gg/wiki/Plant_Food)) |
| Sun-shroom, Kiwibeast | Partially permanent — grows to full size | same |
| Potato Mine, Chili Bean, Lily Pad, Celery Stalker, Radish | **Spawn duplicates of themselves** | same |
| Ultomato, Bzzz Button | **The only two whose Plant Food effect destroys the plant** | same |

Most other effects are temporary and the plant "returns to its initial planting state with full health once
it ends."

### FACT — Plant Food can also be bought as a persistent per-level state

The **Plant Food boost** is bought before a level starts: "any plant from that seed packet will
automatically use its Plant Food effect every time upon being planted", for the whole level.
Cost is **10, 12 or 15 gems depending on the plant's tier** (10 for Peashooter/Sunflower/Repeater, 12 for
Twin Sunflower/Torchwood/Winter Melon/Puff-shroom, 15 for Potato Mine/Snapdragon/Banana Launcher). A free
route exists: fully growing a plant in the Zen Garden spawns an activatable boost packet.
A boosted plant placed on a Power Tile **does not** trigger the Power Tile chain.
([Plant Food boost](https://plantsvszombies.wiki.gg/wiki/Plant_Food_boost))

### The three questions

- **Problem it solves for the player:** it converts a losing board state into a recoverable one without
  adding a plant, and it gives a low-skill player a button that visibly works. It also makes a defensive
  plant's death timer resettable (full heal) at no sun cost.
- **What it costs the designer:** one bespoke ability, one animation, one balance pass **per plant**. With
  188 plants that is 188 hand-authored effects that all have to stay non-degenerate against every world
  gimmick. This is the single most expensive mechanic in the game to maintain, and the evidence is that
  instants were simply exempted rather than given effects.
- **What breaks when tuned wrong:** the cap and the drop rate are the whole balance. Raise the cap or the
  drop rate and the scripted bursts become the primary damage source, which flattens plant choice — every
  loadout converges on the plants with the best burst. Lower it and Plant Food becomes a coin sink nobody
  uses, since 1,000 coins is roughly one Piñata Party's entire coin payout (see §7).

---

## 2. Plant levelling and Mastery

### FACT — two different level curves ship simultaneously

Plants have a `LevelCap` of either 10 or 20. The totals differ sharply:

| Curve | Total seed packets to cap | Total coins to cap | Source |
|---|---|---|---|
| `LevelCap` 10 | **7,185** | **291,000** | [Plant upgrade system](https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system); matches the datamined sunflower arrays exactly (computed) |
| `LevelCap` 20 | **5,788** | **354,750** | [Plant upgrade system](https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system) (trivia section) |

The shipped per-level arrays for a `LevelCap` 10 plant (sunflower), verbatim from the datamine and
corroborated by the totals above:

```
LevelXP    (seed packets, for levels 2..10):
  [10, 75, 200, 400, 750, 1000, 1250, 1500, 2000]       -> 7,185 total (computed)
LevelCoins (coins,        for levels 2..10):
  [1000, 5000, 10000, 20000, 30000, 40000, 50000, 60000, 75000] -> 291,000 total (computed)
```

**The seed-packet cost roughly doubles per level early and then flattens; the coin cost is the steeper of
the two and never flattens.** Coin cost grows about 75× across nine levels while packet cost grows 200×
early then plateaus (computed from the arrays above).

### FACT — what a level actually buys

Levelling edits the plant's own stat arrays, not a global multiplier. The distinct per-level stat keys in
the shipped `PlantLevels` schema are:

`ActionChillDuration`, `ActionCooldownMax`, `ActionCooldownMin`, `ActionDamage`, `ActionDamageNormal`,
`ActionDamagePF`, `ActionDamagePlantfood`, `ActionExplodeRadius`, `ActionFreezeDuration`, `ActionXVelocity`,
`Bloomerang_HitCount`, `Bloomerang_PlantfoodProjectileCount`, `Cost`, `Gravebuster_EatTime`, `Hitpoints`,
`IntensiveCarrot_TargetHealth`, `PacketCooldown`, `PlantFoodDurationSeconds`, `PlantFoodPlayCount`,
`PlantTier`, `Potatomine_ArmingTime`, `StartingCooldown`.

Two things stand out. **`Cost` is a per-level array — levelling can make a plant cheaper in sun**, not just
stronger (sunflower's `Cost` array drops from 50 to 25 at level 8). And **plant-specific keys exist**
(`Bloomerang_HitCount`, `Potatomine_ArmingTime`), so the levelling system is not a closed stat vocabulary;
it is a per-plant patch table with a shared shell.

Worked examples from the shipped curves:

| Plant | L1 | L5 | L10 | L20 |
|---|---|---|---|---|
| Peashooter | 100 sun, 5 s recharge, 300 HP, 20 dmg/pea | **75 sun**, 5 s, 500 HP, **40 dmg** | 75 sun, **3.5 s**, 750 HP, **65 dmg** | 75 sun, **2.5 s**, 1,050 HP, **90 dmg** |
| Sunflower | 50 sun, 5 s, 300 HP, 50 sun/cycle | 50 sun, **4 s**, 550 HP, **60 sun/cycle** | **25 sun**, 2.5 s, 900 HP, — | (cap 10) |
| Wall-nut | 50 sun, 20 s, **4,000 HP** | — | — | **12,000 HP, 10 s recharge** |

([Peashooter](https://plantsvszombies.wiki.gg/wiki/Peashooter_(PvZ2)),
[Sunflower](https://plantsvszombies.wiki.gg/wiki/Sunflower_(PvZ2)),
[Wall-nut](https://plantsvszombies.wiki.gg/wiki/Wall-nut_(PvZ2)), datamined sunflower arrays)

**Levels buy stats and cost reductions. They do not buy new behaviour** — with one exception, below.

### FACT — Mastery: 200 more levels of pure linear stat

Mastery starts once a plant hits its normal cap and runs **M1 to M200**. Three things scale, and only three:

| Mastery reward | Rate | Ceiling |
|---|---|---|
| Damage Pierce | **+1% per level** | **200%** |
| Toughness Bonus | **+10 per level** | **2,000** |
| Chance to Boost — free Plant Food on planting | **+1% per 10 levels** | **21%** |

Cost: seed packets rise from **10 at M1 to 290 at M200**; coins step up at milestones to **20,000** at M50,
M100, M150 and M200. Total: **30,000 seed packets and 972,000 coins for M1→M200**.
([Plant upgrade system](https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system),
[Template:MasteryUpgrades](https://plantsvszombies.wiki.gg/wiki/Template:MasteryUpgrades))

**"Chance to Boost" is the only place levelling buys a new behaviour**, and it buys it as a probability, not
a rule — a Mastery-200 plant fires its Plant Food effect free on 21% of plantings.

### FACT — where the currencies come from, and the exclusions

Seed packets come from piñatas, which come from quest completion, level replays (**capped at five per
level**), and store purchases. Bonus packets can be assigned to premium plants the player does not own.
Adventure plants unlock at **10, 40, 60 or 100 packets** as of update 9.9.1; premium plants at
**100/150/200/250**. **Imitater and Marigold cannot be upgraded at all**; eight plants (Gold Leaf, Gold
Bloom, Thyme Warp and others) **have no Mastery track**; **Vasebreaker levels force every plant to level 1**.
([Plant upgrade system](https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system),
[Seed packet](https://plantsvszombies.wiki.gg/wiki/Seed_packet))

### INFERENCE

- The two coexisting level caps (10 and 20) with *inverted* packet/coin ratios look like a migration
  artefact rather than a design: the cap-20 curve is cheaper in packets and dearer in coins. I did not find
  a patch note confirming a re-tuning, so this is inference, not fact.
- Mastery exists to make the currency sink unbounded without authoring content. 972,000 coins per plant
  across a 188-plant roster is not a number anyone is expected to complete; it is a number designed to never
  be completed.
- **Vasebreaker forcing level 1 is a tell**: the designers kept one mode where levelling is switched off,
  which is what you do when you need at least one surface where the tuning is stable.

### The three questions

- **Problem it solves for the player:** it gives a reason to replay finished content, and it gives a
  spending outlet that never expires. It also lets a stuck player brute-force a wall by grinding.
- **What it costs the designer:** a per-plant stat table with per-plant custom keys, plus the obligation that
  every level of every plant stays balanced against every level design — including Arena, where opponents'
  plant levels are the actual matchmaking variable.
- **What breaks when tuned wrong:** if levels are too strong, authored levels stop being challenges and
  become a check on grind; if too weak, the currency sink has no pull and the store dies. PvZ2's answer —
  small linear steps plus a very long tail — is a hedge, and the visible cost of that hedge is that
  Mastery 1 through 200 contains zero new player decisions.

---

## 3. Sun economy

### FACT — the numbers

| Quantity | Value | Source |
|---|---|---|
| Starting sun, ordinary level | **50** (more with the extra-sun power-up or the Sun Bank Penny Perk) | [Sun](https://plantsvszombies.wiki.gg/wiki/Sun) |
| Sun drop denominations (PvZ2) | **tiny 5, small 25, normal 50, large 75** | same |
| Sky sun value change | **normal sky sun was raised from 25 to 50 in the 1.7 update** | same |
| Worlds with no sky sun | **Dark Ages** (and night/fog levels generally) — producers are the only sun source | same |
| Sunflower | **50 sun cost, 300 HP, 5 s recharge; first sun after 4.5–18 s, then one 50-sun drop every 32–36 s** | [Sunflower](https://plantsvszombies.wiki.gg/wiki/Sunflower_(PvZ2)) |
| PvZ1 comparison | 50 starting sun; sky sun every ~10 s worth 25; Sunflower 25 sun per cycle | [Sun](https://plantsvszombies.wiki.gg/wiki/Sun) |
| Lost City Gold Tile | **50 sun immediately on planting, then 50 sun every 20 s until the plant is removed** | [Lost City](https://plantsvszombies.wiki.gg/wiki/Lost_City) |

### FACT — the plant cost curve

188 plants as of update 10.9.1: **70 unlocked through story progression, 118 premium** (22 for real money,
13 for gems, 70 for seed packets, 13 for mints, 1 Zen Garden exclusive).
([Plants (PvZ2)](https://plantsvszombies.wiki.gg/wiki/Plants_(PvZ2)))

| Sun cost | Plants at that cost |
|---|---|
| 0 | 8 |
| 25 | 4 |
| 50 | 6 |
| 75 | 7 |
| 100 | 9 |
| 125 | 11 |
| **150** | **18** |
| **175** | **14** |
| 200 | 10 |
| 225 | 4 |
| 250 | 6 |
| 275 | 1 |
| 300 | 3 |
| 325 | 1 |
| 350 | 2 |
| 400 | 2 |
| 500 | 3 |

**The distribution peaks hard at 150–175 sun** — 32 of 188 plants, 17% of the roster, sit in two adjacent
price points (computed). Costs are a 25-sun lattice from 0 to 500 with no value off-grid.

### FACT — recharge is a raw float, not a tier

The shipped data stores recharge as **`PacketCooldown`** (seconds) and **`StartingCooldown`** (seconds
before the packet is first available in a level), both as per-level float arrays. Observed shipped values
run **5 s to 90 s**, with clustering at 5, 7, 8, 10, 12, 15, 20, 25, 30, 35, 45, 50, 75, 85, 90.
([Plants (PvZ2)](https://plantsvszombies.wiki.gg/wiki/Plants_(PvZ2)), `PlantLevels` schema)

**The named tiers ("Fast", "Mediocre", "Sluggish", "Slow", "Very Slow") are a wiki convention, not a game
field, and the sources disagree with each other about their boundaries.** The only recharge tier values I
could source cleanly are PvZ**1**'s: Fast = 7.5 s, Slow = 30 s, Very Slow = 50 s
([Recharge](https://plantsvszombies.wiki.gg/wiki/Recharge) — the PvZ2 section of that page is marked TBA).
Forum-level claims for PvZ2 put "Mediocre" at 15 s and "Sluggish" at 20 s; **treat those as tier 3 and
unverified.**

### FACT — the designers themselves treat sun as the difficulty dial

Penny's Pursuit exposes a fixed list of modifiers that scale with the chosen difficulty. The list is:
**Sun Dropper (how often sun falls), Starting Sun, Maximum Sun (the bankable cap), Plant Food (count given
at level start), Seed Slots, Lawn Mowers, First Wave Delay, Zombie Strength.**
([Penny's Pursuit](https://plantsvszombies.wiki.gg/wiki/Penny%27s_Pursuit))

**Six of the eight knobs are the player's economy and setup budget. One is zombie strength.**

### INFERENCE

- Sun is the only resource that is simultaneously the build cost, the tempo clock and the mistake tax.
  Every other system in the game (Plant Food, mints, power-ups, levels) is deliberately kept *outside* it,
  so that adding an economy layer never inflates the in-match currency. That separation looks intentional
  and it is the reason plant sun costs did not creep upward across eight years of content.
- The 1.7 change from 25-sun to 50-sun sky drops halved the number of clicks per unit of sun without
  changing the sun-per-second much. That reads as an ergonomics fix on a touchscreen, not a difficulty
  change — but I could not source a patch note saying so.

### The three questions

- **Problem it solves for the player:** it makes every second of the level a decision, and it makes an early
  mistake legible (you are behind on sun) without being immediately fatal.
- **What it costs the designer:** almost nothing structurally — it is one integer and one drop timer — but
  it costs a great deal in *level* authoring, because every level has to be tuned against a sun curve that
  the player partly controls.
- **What breaks when tuned wrong:** too much sun and the whole game collapses into "plant the best plant in
  every tile", which is exactly what Last Stand levels demonstrate when they hand over 600–3,250 sun and
  then have to **ban every sun producer and every 0-cost plant** to stay a puzzle
  ([Last Stand](https://plantsvszombies.wiki.gg/wiki/Last_Stand_(PvZ2))). Too little and the level becomes a
  memorised opening with no recovery path.

---

## 4. Level and world gimmicks

### FACT — the per-world rule sets

| World | Gimmick | What it does to the core loop |
|---|---|---|
| **Ancient Egypt** | Tombstones present from level start; daytime with normal sky sun (unlike PvZ1 night levels). Grave Buster removes them. Ra Zombie steals sun; Tomb Raiser resurrects fallen zombies. | **Removes tiles from the build grid at t=0** and adds a plant whose only job is reclaiming them. ([Ancient Egypt](https://plantsvszombies.wiki.gg/wiki/Ancient_Egypt)) |
| **Pirate Seas** | Lawn splits into normal ground, **planks** (plantable but no underground plants), and **water** (unplantable; a zombie that falls in **dies instantly**). Swashbucklers swing in from the water past the front line. Cannons Away: shoot Seagulls with five Coconut Cannons for a target score. | **Cuts the usable board and adds a free kill zone**; the plank/water boundary becomes the real defensive line. ([Pirate Seas](https://plantsvszombies.wiki.gg/wiki/Pirate_Seas)) |
| **Wild West** | **Minecarts on rails** — a plant placed in a cart can be slid along its rail at will. Nothing can be planted on a rail itself. | **Adds a mobile tile.** One plant covers several lanes at the cost of a permanently dead row of tiles. ([Wild West](https://plantsvszombies.wiki.gg/wiki/Wild_West)) |
| **Frostbite Caves** | Chilling wind gusts progressively freeze plants until fully **encased in an ice block that behaves like a tombstone**. Hot Potato and other thawing plants free them. **Power Flame replaces Power Snow** in this world only. Slider tiles push zombies one lane up or down. | **Puts a decay timer on the whole board** and forces a slot for a non-combat maintenance plant. ([Frostbite Caves](https://plantsvszombies.wiki.gg/wiki/Frostbite_Caves)) |
| **Lost City** | **Gold Tiles**: planting on one yields 50 sun immediately, then 50 sun every 20 s until that plant is eaten, crushed or moved. Excavator removes plants; Porter Gargantuar carries other zombies. | **Ties the economy to holding specific ground.** Losing a tile is a compounding loss, not a one-off. ([Lost City](https://plantsvszombies.wiki.gg/wiki/Lost_City)) |
| **Far Future** | **Power Tiles** in six colours: Plant Food given to a plant on a coloured tile fires the Plant Food effect of *every* plant on tiles of that colour, free. Tile Turnip creates new (magenta) Power Tiles. A boosted plant on a matching set **does not** trigger the chain. Zombies ride vehicles that are destroyed instead of the zombie dying. | **Multiplies the Plant Food burst by board layout** — makes placement, not plant choice, the burst decision. ([Far Future](https://plantsvszombies.wiki.gg/wiki/Far_Future), [Power Tile](https://plantsvszombies.wiki.gg/wiki/Power_Tile)) |
| **Dark Ages** | **No sun falls from the sky at all.** Tombstones spawn mid-level and trigger a "Necromancy!" ambush; a grave appearing under a plant **pushes that plant one column forward**. Marked tombstones yield **100 sun or one Plant Food** when broken. Wizard summons; Jester **reflects projectiles**. Dark Alchemy potions buff zombies — orange raises speed, fuchsia grants **+25% health**. | **Deletes the passive half of the economy**, so every sun slot is a real slot. Jester inverts the "shoot it" default. ([Dark Ages](https://plantsvszombies.wiki.gg/wiki/Dark_Ages)) |
| **Neon Mixtape Tour** | Six **jams** that change global zombie speed and switch on type-specific abilities: **Punk 150%** speed (Punk Zombie moshes plants back a tile); **Metal 125%**; **Pop 80%** (Glitter Zombie's rainbow trail **removes all negative effects** from zombies inside it); **Rap 100%** (MC Zom-B kills all non-defensive plants in a 3×3); **8-Bit 100%** (Arcade Zombie machines spawn 8-bit zombies); **Power Ballad 100%** (**tranquilises every plant on the lawn for ~8 s per Boombox Zombie**, overrides other jams, does not affect plants placed after it started). | **A global, telegraphed, repeating multiplier on the whole enemy side** — the same wave is easy or lethal depending on the track. ([Neon Mixtape Tour](https://plantsvszombies.wiki.gg/wiki/Neon_Mixtape_Tour)) |
| **Jurassic Marsh** | Five dinosaurs act as neutral board-movers; Perfume-shroom charms them and **inverts their effect**. See the table below. | **A third faction that moves units** — the player's decision is whether to spend a slot flipping it. ([Dinosaurs](https://plantsvszombies.wiki.gg/wiki/Dinosaurs)) |
| **Big Wave Beach** | A **tide line whose column varies per level (one to seven columns of water)**; water needs Lily Pads. Low Tide surprise attack drops Imp Mermaids into random lanes. **Surfer Zombie destroys a land plant and leaves a surfboard, permanently removing a plantable tile.** | **Makes board size a per-level parameter** rather than a constant. ([Big Wave Beach](https://plantsvszombies.wiki.gg/wiki/Big_Wave_Beach)) |
| **Modern Day** | **Scripted portals** summon zombies from any previous world; "the tiles where portals appear and the zombies summoned are scripted and never change." Power Tiles on Days 10, 24, 33. Rated **five jalapenos** difficulty. | **A remix world** — zero new units, maximum recombination. ([Modern Day](https://plantsvszombies.wiki.gg/wiki/Modern_Day)) |

Jurassic Marsh dinosaurs, exact effects (all quotes [Dinosaurs](https://plantsvszombies.wiki.gg/wiki/Dinosaurs)):

| Dinosaur | Uncharmed | Charmed (Perfume-shroom) |
|---|---|---|
| Raptor | Kicks a zombie **up to four tiles forward, not past column 3**; leaves after **5** kicks | Kicks a zombie **off the lawn**; leaves after 5 |
| Stegosaurus | Snares **up to three** zombies and flings them **up to column 4, possibly into other lanes**; leaves after 3 | Smashes them for **2,700 damage** to the three, plus **200** splash to others; leaves after 3 |
| Pterodactyl | Carries a zombie **to the last tile** (not Imps/Gargantuars); leaves after 3 | Abducts the Jurassic zombie **closest to the house** and drops its head, armour and arm; leaves after 3 |
| T. rex | Roars: **speeds up zombies in its lane** and turns backward-facing zombies around; leaves after roaring | **Chomps and instantly kills** passing zombies; leaves after 5 |
| Ankylosaurus | Knocks zombies forward into the first plant (**to column 2 if no plant blocks**) | **Tosses all zombies on and behind its tail off-screen** |

### The three questions

- **Problem it solves for the player:** it stops a 300+ level game from being one level. Each world resets
  what the player knows about the board, so old plants get re-evaluated instead of retired.
- **What it costs the designer:** a per-world systems build — new tile types, new interaction rules against
  *every existing plant and zombie*. Big Wave Beach's water and Frostbite Caves' ice both required special
  cases on every plant ("can this be planted here", "does thawing apply"), and the wiki records at least one
  shipped bug from exactly that surface (thawing plants not defrosting non-block-state plants).
- **What breaks when tuned wrong:** a gimmick that removes tiles (tombstones, ice, surfboards) taxes the sun
  economy indirectly, and the two are tuned independently. When the tile tax and the sun budget disagree the
  level is unwinnable rather than hard — which is the failure mode Modern Day's five-jalapeno rating and its
  reputation reflect.

---

## 5. Power Ups, Power Mints, boosts, premium

### FACT — consumable Power Ups (coins)

| Power Up | Effect | Cost | Duration |
|---|---|---|---|
| Power Snow | Hold on a zombie to throw chilling snowballs | **1,150 coins** | 6 s |
| Power Toss | Swipe to toss a zombie; swipe twice to throw it off screen | **950 coins** (8 diamonds, China) | 6 s |
| Power Zap | Hold to electrocute | **800 coins** (was 1,000) | 4 s |
| Power Flame | Thaw ice and burn zombies; **Frostbite Caves only**, replaces Power Snow there | **1,200 coins** (was 1,500) | 4 s |
| Power Pinch | Destroy a zombie on contact | **removed in 1.9**; was 800 coins | — |

**All main Power Ups were cut 20% in update 5.8.1.** Power Toss cannot touch Zomboss, and can only launch a
Gargantuar if it has been shrunk. ([Power Ups](https://plantsvszombies.wiki.gg/wiki/Power_Ups))

Mode-specific: **Security Gourds** (Penny's Pursuit) push zombies back and **add 30 seconds to the timer**
for **25 gems**. Vasebreaker: Reveal Vase 200 coins, Butter Zombie 300, Move Vase free (was 400). Beghouled:
Power Shuffle 200, Power Shovel 100. China only: **Tactical Cuke — 7,200 damage to all zombies (1,800 to
Zombots) for 15 diamonds plus 10 more per prior use in the same level.**

### FACT — Power Mints (a separate currency entirely)

**13 Power Mints in the international build**, one per plant family: Appease-mint, Arma-mint, Bombard-mint,
Conceal-mint, Contain-mint, Enchant-mint, Enforce-mint, Enlighten-mint, Fila-mint, Pepper-mint,
Reinforce-mint, Spear-mint, Winter-mint. (Ail-mint and Hurricane-Mint also appear in family/mint listings —
see *What I could not find*.)

- **100 mints to unlock a Power Mint. Its seed-packet piñatas cost 25 mints each** (a separate listing gives
  20 gems for a Power Mint piñata). ([Mint](https://plantsvszombies.wiki.gg/wiki/Mint), [Gem](https://plantsvszombies.wiki.gg/wiki/Gem))
- Mints are earned from **Arena streaks**, **finishing top 10 in an Arena tournament** (top 9 in Silver
  League and above), and Travel Log quests (**2–3 mints** for a Power Mint showcase, **3 or 5** for an Arena
  practice quest, **2** for a daily Arena match). A consistent player can reach **~62 mints per availability
  week from Travel Log quests alone** — i.e. **a Power Mint is roughly a two-week earn.** (computed from the
  same page's figures)
- Appease-mint's shipped stat block: **0 sun, 85 s recharge, boost lasts 6 s at level 1 rising to 15 s at
  level 10**; it "fire[s] a volley of huge peas that break into smaller peas and provide a temporary boost to
  all Appease-mint Family plants on the lawn". Sample boost magnitudes: **Peashooter +150 damage,
  Threepeater +120 damage per pea.** ([Appease-mint](https://plantsvszombies.wiki.gg/wiki/Appease-mint))

### FACT — gems and the direct-purchase layer

Gem income: **3 gems per ad, 3 ads per day** (5 per ad during Feastivus); 3 gems for a Penny's Pursuit win
quest, 2 for a Piñata Party, 2 for an adventure level, 1 for the Zen Garden; **80 gems per two-week Epic
Quest**; **100 gems for creating an EA account**; Arena placement rewards. Premium plants are **100 gems**
(some sold at 179 during events). Gem bundles ship at 20 / 50 / 110 / 250 / 700 / 1,800.
([Gem](https://plantsvszombies.wiki.gg/wiki/Gem))

### FACT — reception of the monetisation

PvZ2 launched free-to-play on iOS on **15 August 2013** (soft launch AU/NZ 9 July 2013) and Android on
**2 October 2013**, and topped the free charts in **137 countries** within five days. Press was split:
*Wired*'s review ran as "it's about in-app payments ruining sequels" and *Macworld*'s as the "paywall leaves
us feeling dead inside", against a Metacritic of **86/100**. The Arena mode and the plant levelling system
both arrived in later updates. ([Wikipedia](https://en.wikipedia.org/wiki/Plants_vs._Zombies_2))

### The three questions

- **Problem it solves for the player:** Power Ups are a bail-out for a level the player has already invested
  minutes into; Mints are a reason to play Arena; boosts are a way to convert money into a shortcut past a
  grind wall.
- **What it costs the designer:** four parallel currencies (coins, gems, seed packets, mints) that all have
  to stay non-substitutable, plus per-mode power-up availability rules, plus the Power Mint family
  taxonomy — 14 families over 188 plants, every one of which must be re-tagged when a plant ships.
- **What breaks when tuned wrong:** a bail-out priced below its value turns every hard level into a coin
  cost, which is exactly the complaint the launch reviews made. The 20% price cut in 5.8.1 is the visible
  correction in the other direction.

---

## 6. Level objectives

### FACT — objectives are per-level extra win conditions layered on the survive condition

Documented objective forms, with real shipped instances:

| Objective form | Real instance | Source |
|---|---|---|
| Don't lose more than N plants | **Pirate Seas Day 34: one.** Wild West Day 15 and Day 23: **two.** Modern Day Day 12: **ten.** | wiki.gg level pages via [site search](https://plantsvszombies.wiki.gg/index.php?title=Special:Search&search=extra+objective+don%27t+lose+more+than+plants&fulltext=1) |
| Don't spend more than N sun | **Wild West Day 19: 2,000 sun**, combined with "don't lose more than 2 plants" | same |
| Produce at least N sun | **Far Future Day 21: 5,000 sun** | wiki.gg site search |
| Never have more than N plants | **Far Future Day 21: 16 plants** | same |
| Don't let the zombies trample the flowers | **Wild West Day 17** | [Wild West](https://plantsvszombies.wiki.gg/wiki/Wild_West) |
| Beat a target score | Cannons Away (Pirate Seas): shoot Seagulls with five Coconut Cannons, **target rises per level** | [Brain Busters](https://plantsvszombies.wiki.gg/wiki/Brain_Busters) |
| Make N matches | Beghouled, Modern Day: **Day 8 = 100 matches, Day 13 = 150, Day 22 = 75** | [Modern Day](https://plantsvszombies.wiki.gg/wiki/Modern_Day) |

Some of the "produce at least / never have more than" instances surfaced by search sit in Chinese-version
worlds (Back to Far Future, Darker Ages, Romeward Bound, Legions of the Undead). **Far Future Day 21 is the
international instance** and is the one to cite.

### FACT — Brain Busters: whole rule sets, not just win conditions

| Brain Buster | Worlds | Rule change |
|---|---|---|
| **Save Our Seeds** | every world | Pre-planted endangered plants sit on **yellow-and-black striped tiles**. Losing one **by any means** — eaten, crushed, burned — fails the level. Since 1.9 the shovel simply does nothing on them (before 1.9, shovelling one lost the level). Plant Food restores them to full health; Wall-nut First Aid works on endangered Chard Guards and Primal Wall-nuts. Each world has its own designated plant (Egypt: Sunflower/Bonk Choy; Dark Ages: Puff-shroom/Magnet-shroom; Jurassic Marsh: Primal Wall-nut/Toadstool; and so on). ([Save Our Seeds](https://plantsvszombies.wiki.gg/wiki/Save_Our_Seeds)) |
| **Locked and Loaded** | every world | The seed selection is **pre-chosen by Crazy Dave**; the player survives with what they are given. |
| **Special Delivery** | every world | Plants arrive on a **conveyor belt**; **no sun is produced at all**, and the plant list is fixed. |
| **Last Stand** | most worlds | The player is handed a lump of sun — **600 to 3,250 depending on level** — builds during a setup phase, then survives. **All sun producers are banned, and so are all 0-sun plants** (exceptions: Power Mints, plus Thorns and Gold Bloom in China). Plant Food is capped at **one to five per level, usually two to four**. Sun is refunded on shovelling during setup. Since 1.9, keeping the lawn mowers is no longer required to progress. ([Last Stand](https://plantsvszombies.wiki.gg/wiki/Last_Stand_(PvZ2))) |
| **Cannons Away** | Pirate Seas | Five Coconut Cannons, shoot down Seagulls to a target score. |
| **Not OK Corral** | Wild West | Plant one plant, one zombie leaves the corral, repeat. |
| **Sun Bombs** | Far Future | Sun producers banned except Gold Bloom, Sun Bean, Toadstool, Solar Sage; **sun falls twice as often**; purple sun explodes if touched before it lands. |
| **Dark Alchemy** | Dark Ages | Potions buff zombies — orange raises speed, **fuchsia grants +25% health**. |
| **Bulb Bowling** | Big Wave Beach | Four bulb types from a conveyor; roll them forward to ricochet off zombies. |
| **Beghouled** | Modern Day | Match-3 on the lawn; hit a match quota. |

China-only additions include Mummy Memory (flip Camel Zombie signs; mowers, Plant Food and Cuke disabled),
Vasebreaker, Dodo Adventures (dodo has **3 lives**), Bevegetabled, All by Oneself (**one plant with 10×
damage that gains XP mid-level**) and Whack-a-Mole (**10 points per Industrial Imp, −10 for a Flat-shroom**).
([Brain Busters](https://plantsvszombies.wiki.gg/wiki/Brain_Busters))

### INFERENCE — why objective variety substitutes for content

Every objective above is **a constraint on the player's own actions, not a new enemy**. "Don't lose more than
two plants" makes a Wall-nut a liability rather than an asset. "Never have more than 16 plants" turns the
board from a filling exercise into a selection one. "Produce at least 5,000 sun" inverts the sun economy from
a means into an end. None of them required a new zombie, a new plant, a new animation or a new tile type —
they are **predicates over game state**, and the game already tracks that state for other reasons.

**That is the cheapest content in the game per unit of novelty, and PvZ2 uses it more heavily than any other
lever.** The expensive levers (worlds, plants, zombies) shipped roughly annually; objectives and Brain
Busters are sprinkled through every world at a rate of several per 30-day world.

### The three questions

- **Problem it solves for the player:** it stops optimal play from converging. The dominant strategy for
  "survive" is not the dominant strategy for "survive with at most 16 plants", so the player's existing
  knowledge is re-priced rather than invalidated.
- **What it costs the designer:** one state predicate plus one UI string per objective *type*, then near-zero
  per instance. The real cost is the failure UX — a player who fails at wave 9 for an objective reason needs
  to understand which of two conditions they lost to.
- **What breaks when tuned wrong:** an objective that conflicts with the level's own difficulty (a "don't
  lose plants" objective on a level whose gimmick destroys plants — Frostbite ice, Surfer boards, Excavators)
  becomes a coin-flip. Last Stand shows the containment strategy: when the objective breaks the economy,
  ban the economy plants outright rather than re-tune the numbers.

---

## 7. Endless and repeatable modes

### FACT — Endless Zones (11 of them)

| Endless Zone | World |
|---|---|
| Pyramid of Doom | Ancient Egypt |
| Dead Man's Booty | Pirate Seas |
| Big Bad Butte | Wild West |
| Icebound Battleground | Frostbite Caves |
| Temple of Bloom | Lost City |
| Terror from Tomorrow | Far Future |
| Arthur's Challenge | Dark Ages |
| Greatest Hits | Neon Mixtape Tour |
| La Brainsa Tarpits | Jurassic Marsh |
| Tiki Torch-er | Big Wave Beach |
| Highway to the Danger Room | Modern Day |

Rules ([Endless Zone](https://plantsvszombies.wiki.gg/wiki/Endless_Zone), [Pyramid of Doom](https://plantsvszombies.wiki.gg/wiki/Pyramid_of_Doom)):

- The player starts with **three or four plants given automatically**, then picks more each level from
  **four random plants — three face-up and one face-down, revealable for 2,000 coins**.
- **Plant Food carries between levels** (the only place in the game it does). **Lawn mowers carry and can be
  replenished.** The **sun bank resets to 50, 75 or 100 each level.** Placed plants are cleared entirely.
- Of a **12-zombie pool** (11 in Arthur's Challenge) the game picks **five to six per level**, seven in Big
  Bad Butte. "The number of zombies increases with every level, and the combination of zombies is random."
  Levels can have **three or four flags**.
- **From about level 20 upward, exactly one Plant Food drops per wave.**
- Certain plants are excluded outright (Pyramid of Doom bans Blover, E.M.Peach, Lily Pad and others).
- Since 5.5.1 a lost run can be replayed after watching a video.

### FACT — Zomboss

Dr. Zomboss appears as a boss in **every world except the China-only Kongfu World**, in a world-themed
Zombot each time. Concrete numbers exist for the Ancient Egypt boss:

**Zombot Sphinx-inator — approximately 18,500 total damage absorbed**, with appearance changes at 4,000 and
12,000. Its phase thresholds on Ancient Egypt Day 25 are **4,000 / 8,000 / 6,500**. Attacks: warps in
zombies; fires a targeted missile that destroys the plant on a marked tile and **creates two tombstones on
adjacent tiles (four on Day 35)**; and a charge that **destroys all plants and zombies in two rows**.
It occupies two rows at once and jumps rows occasionally.
([Zombot Sphinx-inator](https://plantsvszombies.wiki.gg/wiki/Zombot_Sphinx-inator))

The Zomboss overview page gives one aggregate figure — "all of them … absorbing a total of 88000 damage" —
which I could not decompose per boss. ([Dr. Zomboss (PvZ2)](https://plantsvszombies.wiki.gg/wiki/Dr._Zomboss_(PvZ2)))

### FACT — Arena (renamed from **Battlez** in version 7.5.1)

Asynchronous score-vs-score PvP.

| Element | Value |
|---|---|
| Scoring base | **Per-zombie point values from 100 (Basic Zombie, Imp) up to 10,000+ (Zombots)** |
| Zone multiplier | Zombies killed in the **yellow zone score 100%; red 60%; blue 40%; brown 20%** of their value |
| Penalties | A non-defensive plant eaten reduces the zombie's health and therefore its score; a **sun-producing plant eaten** or a **manually launched lawnmower** is a big penalty; **a zombie triggering a mower is a huge penalty** |
| Crowns | **5 for a win, 1 for a loss, 0 for a surrender** |
| Leagues | **Soil → Wood → Brick → Iron → Bronze → Silver → Gold → Jade** |
| Promotion | **Top 3 in your league by crowns** |
| Streaks | Reward streak under the play button; a broken streak can be **bought back for 20 gems** |
| Tournaments | 7-day tournaments pay full rewards; **3-day ≈ 40%, 4-day ≈ 60%** |

([Arena](https://plantsvszombies.wiki.gg/wiki/Arena))

**INFERENCE:** the zone multiplier is the whole design. It converts "did you survive" into "how far forward
did you hold the line", which makes the same level scoreable on a continuum instead of pass/fail — a
requirement for asynchronous PvP against a stored score. It also makes plant level directly monetisable,
because a higher-level plant kills further right.

### FACT — Penny's Pursuit (weekly)

- Three difficulties: **Mild, Spicy, Extra Hot.** Spicy raises zombie health and eating rate and swaps weak
  zombies for tougher variants (Imp Cannon → Zcorpion). Extra Hot goes further and **removes lawn mowers and
  seed slots** while adding bonus objectives.
- The modifier list is fixed and difficulty-scaled: **Sun Dropper, Starting Sun, Plant Food, Seed Slots,
  Maximum Sun, Lawn Mowers, First Wave Delay, Zombie Strength.**
- **5 Fuel per level attempt; Fuel caps at 15** (can exceed via gems) and regenerates over time; **an ad
  grants 5 Fuel.**
- Event shapes: **Regular = 5 levels / 1 week; Special = 10 levels / 2 weeks with event-only zombies;
  Worldly = a whole world's levels minus the Zomboss / 3–4 weeks.**
- First-clear rewards: **Mild 1,000 coins; Spicy 1,500 coins + 10–15 seeds of the featured plant + 4 gems;
  Extra Hot 2,000 coins + 10–15 seeds + 4 gems.** Repeats pay coins only.
- **ZPS meter: Mild +20%, Spicy +25%, Extra Hot +30% per clear.** At 100% the player may fight Zomboss
  **up to 3 times within a 12–24 hour window** before the meter resets.
- Each event has a **featured plant**, whose seeds are the Spicy/Extra Hot reward.

([Penny's Pursuit](https://plantsvszombies.wiki.gg/wiki/Penny%27s_Pursuit))

### FACT — Piñata Party (daily)

Playable **every day at 2:00 AM** (originally Mondays and Thursdays only until update 2.2). Levels are
daily-rotated Brain Busters, mostly Special Delivery, sometimes with zombies from any world and sometimes in
a different world. **Premium and limited-edition plants are sometimes free to use here even if unowned.**
Winning breaks three piñatas: **two sets of seed packets in quantities of 3, 5, 7, 10 or 25**, coin sets of
**1,000 / 1,500 / 2,000 / 2,500**, and a jackpot slot of **4,000 coins or a costume**. **Five consecutive
parties trigger Señor Piñata** (a costume or 4,000 coins). One free replay by watching an ad, further
replays **10 gems**. ([Piñata Party](https://plantsvszombies.wiki.gg/wiki/Pi%C3%B1ata_Party))

### The three questions (all four modes)

- **Problem it solves for the player:** a reason to open the app on a day when no new content shipped, and a
  legible weekly/daily rhythm — daily Piñata, weekly Pursuit, multi-day Arena tournament, open-ended Endless.
- **What it costs the designer:** Endless Zones cost close to nothing after the world exists (they are
  procedural recombinations of an existing zombie pool). Penny's Pursuit costs a weekly authoring slot plus a
  live-ops pipeline. Arena costs the most: it needs score balancing across the entire plant roster forever,
  because every new plant is immediately a scoring tool.
- **What breaks when tuned wrong:** the Arena zone multiplier makes score scale roughly with plant power, so
  a single mispriced plant becomes mandatory and the leaderboard stops measuring skill. The Fuel cap (15,
  5 per attempt = 3 attempts banked) is the throttle that keeps Pursuit rewards from being farmable; if that
  cap moves, the seed-packet economy behind plant levelling moves with it.

---

## 8. Plant families and designed synergy

### FACT — 14 families, one per Power Mint

| Family | Members | Defining trait (quoted) |
|---|---|---|
| Enforce-mint | **19** | "melee plants, or plants that attack up close" |
| Contain-mint | **18** | "plants that cripple zombies's stats" |
| Appease-mint | 17 | "peashooter plants and plants that shoot projectiles in one direction" |
| Reinforce-mint | 17 | "defensive plants and support plants" |
| Enchant-mint | 16 | "plants that utilize magic in some capacity" |
| Arma-mint | 15 | "lobbed-shot, launching, and cannon plants" |
| Spear-mint | 15 | "plants with spikes or piercing attacks" |
| Pepper-mint | 15 | "heat-based plants, as well as Ghost Pepper" |
| Ail-mint | 15 | "poisoning plants and mushrooms" |
| Enlighten-mint | 14 | "sun producing plants" |
| Bombard-mint | 14 | "entirely composed of explosive plants" |
| Fila-mint | 14 | "electric plants, as well as Magnifying Grass and Ultomato" |
| Winter-mint | 12 | "entirely composed of ice-based plants" |
| Conceal-mint | 11 | "entirely composed of shadow plants" |

([Plant Family (PvZ2)](https://plantsvszombies.wiki.gg/wiki/Plant_Family_(PvZ2)))

### FACT — what a family tag actually does

Families exist "mostly … to indicate which plant is influenced by which Power Mint". Planting a Power Mint
deals damage and "boost[s] the plants on the lawn that are members of the appropriate family", raising
"damage, toughness, sun-producing, and/or unique abilities" for a **level-scaled duration (6 s at L1 → 15 s
at L10 for Appease-mint)**. Concrete magnitudes are per-plant, not per-family: Appease-mint gives Peashooter
**+150 damage** and Threepeater **+120 damage per pea**.

Families are also the featuring axis for Arena and Penny's Pursuit — events surface a family, and premium
plants of that family become temporarily free to use.

### INFERENCE

- **The taxonomy is descriptive, not mechanical.** A family is a *selector*, not a rule: nothing happens
  because two Appease-mint plants are adjacent. The only synergy the tag creates is "one card buffs this set
  for N seconds". Compare Winter-mint (chills all zombies *and* buffs its family) — the mint carries both the
  board effect and the buff; the family carries nothing on its own.
- The trait definitions leak. "Fila-mint: electric plants, **as well as Magnifying Grass and Ultomato**";
  "Pepper-mint: heat-based plants, **as well as Ghost Pepper**"; "Contain-mint: mainly composed of plants
  that typically don't belong in any other family". **Contain-mint is the overflow bucket** and it is the
  second-largest family. That is what happens when a taxonomy is applied retroactively to a roster that was
  not designed against it.
- **Family size correlates with which mints are worth owning**, so the taxonomy is also a monetisation
  surface: a 19-member family mint is a better 100-mint purchase than an 11-member one, all else equal.

### The three questions

- **Problem it solves for the player:** it makes 188 plants navigable, and it gives a reason to build a
  themed deck rather than a best-of list.
- **What it costs the designer:** every new plant needs a family assignment that does not break 13 existing
  mints, and every mint needs a per-plant boost value for every member — that is the **+150/+120** style
  authoring, per plant, per mint.
- **What breaks when tuned wrong:** an overflow family (Contain-mint) has no coherent boost to give, so
  either its mint is weak or its boost is generic; and a family that grows past the point where its mint's
  buff was tuned quietly becomes the best purchase in the store.

---

## 9. Zombie design

### FACT — the attribute vocabulary

PvZ2 zombies carry a **toughness class** and a **speed class**, both drawn from closed lists, plus a raw HP
number, plus zero or more independently-HP'd armour layers.

Toughness classes ([Zombies (PvZ2)](https://plantsvszombies.wiki.gg/wiki/Zombies_(PvZ2))):

| Class | Damage absorbed |
|---|---|
| Fragile | 1–100 |
| Average | 101–200 |
| Solid | 201–320 |
| Protected | 321–600 |
| Dense | 601–1,000 |
| Hardened | 1,001–1,700 |
| Machined | 1,701–2,500 |
| Great | 2,501–8,000 |
| Undying | 8,001–29,500 |
| Ultra-Undying | 29,501+ |

Speed classes — expressed as **seconds to cross one tile**, which is the useful form:

| Class | Seconds per tile |
|---|---|
| Creeper | 7.5 |
| Stiff | 6.75 |
| Basic | 5.0 |
| Hungry | 3.75 |
| Speedy | 2.5 |
| Flighty | 0.5 |

**The spread from Creeper to Speedy is exactly 3× and Flighty is 15× the slowest** (computed) — so speed is
a coarse, heavily quantised axis with one outlier class for fliers/dashers.

### FACT — the damage unit

- **One pea = 20 damage.** ([Damage](https://plantsvszombies.wiki.gg/wiki/Damage))
- **A zombie bite deals 100 damage, counted per second of attacking** — i.e. **almost every zombie is 100
  DPS.** A 4,000 HP Wall-nut is therefore eaten in **40 seconds** and a 300 HP Peashooter in **3 seconds**
  (computed / stated). ([Damage](https://plantsvszombies.wiki.gg/wiki/Damage),
  [True Health System](https://plantsvszombies.wiki.gg/wiki/User_blog:1Zulu/PvZ2%27s_True_Health_System))
- A Gargantuar does not bite; it **crushes for 1,500 damage per hit** — enough to one-shot most plants, and
  exactly matched by the Wall-nut's 8,000 Plant Food armour absorbing "one Gargantuar smash".

### FACT — armour layers and the decapitation rule

Armour is not a damage reduction; it is **a separate HP pool consumed first**.

| Zombie | Layers | Total |
|---|---|---|
| Basic Zombie | 270 body (older listing) / **190** on the current infobox — see the discrepancy note | Average toughness |
| Conehead Zombie | **370 cone + 270 body** (blog) / "560 total, 370 roadcone, 190 zombie" (wiki table) | Protected |
| Buckethead Zombie | **1,100 bucket + 270 body**; wiki infobox states **1,290**, with visual breaks at **350, 700, 1,100 (bucket destroyed), 1,195 (arm falls off)** | Hardened |
| Gargantuar | **3,600**, no armour; **throws its Imp at 1,800 damage absorbed (90 pea shots)** | Great |

**A "critical point" exists at 2/3 HP lost: past it, any hit decapitates.** Some zombies (Gargantuars) have a
critical point of zero and never lose their heads. This is why "perceived HP" — how much damage a player
appears to need — differs from true HP: Basic Zombie has **HP 270, perceived HP 181**; Buckethead **HP 1,370
across layers, perceived 1,281**; Gargantuar **3,600 / 3,600**.
([True Health System](https://plantsvszombies.wiki.gg/wiki/User_blog:1Zulu/PvZ2%27s_True_Health_System))

**Discrepancy, flagged not resolved:** the current wiki.gg infoboxes give Basic Zombie 190 and Buckethead
1,290; the True Health blog on the same wiki gives 270 and 1,100+270. Both are on the same site. **I could
not determine which reflects the current build** — likely a rebalance between the blog's writing and now.
Do not treat either number as settled.

### FACT — gimmick behaviours are the real vocabulary

Beyond stats, zombies carry scripted behaviours that change the board rather than the arithmetic. From the
worlds above: **resurrect fallen zombies** (Tomb Raiser), **steal sun** (Ra), **reflect projectiles**
(Jester), **summon** (Wizard, Zombie King), **remove a plantable tile permanently** (Surfer), **remove plants
from underground** (Excavator), **push other zombies forward** (Breakdancer), **strip all debuffs from
zombies in a trail** (Glitter), **tranquilise every plant on the lawn** (Boombox), **kill all non-defensive
plants in a 3×3** (MC Zom-B), **be destroyed as a vehicle rather than a zombie** (all Far Future mechs).

### The three questions

- **Problem it solves for the player:** the toughness/speed class pair makes an unfamiliar zombie readable at
  a glance — the almanac tells you the two numbers that determine whether your current line holds. Armour
  layers make progress visible frame by frame.
- **What it costs the designer:** each gimmick is an interaction against every plant. The 100-DPS-for-nearly-
  everything convention is the cost control: it means a plant's HP *is* its survival time in seconds, so a
  designer can reason about the board without a simulation.
- **What breaks when tuned wrong:** the moment a zombie deviates from 100 DPS, every plant's HP stops being
  readable as seconds, and the player's learned intuition breaks silently. The same applies to the toughness
  bands — they are wide (Great spans 2,501–8,000, a 3.2× range), so two "Great" zombies can differ by more
  than the whole Fragile-to-Dense range, and the label stops predicting anything.

---

## Hooks for this project

**Non-normative and un-vetted. Nothing here has been checked against this repo's code, specs, or
`docs/DESIGN-GATE.md`. This is a list of things worth *looking at*, not things to do.**

- **Plant Food as a capped burst currency** — a hard cap of 3 with an in-match purchase price is a different
  shape from a cooldown or a mana pool, and it might be worth comparing against how the effect/atom layer
  currently gates burst.
- **Per-plant scripted burst rather than a generic multiplier** — 188 hand-authored effects is exactly the
  authoring cost a seedsmith generator exists to avoid; the interesting question is what the *seed contract*
  for a burst effect would have to carry.
- **Mastery's shape (200 levels of +1% pierce, +10 toughness, +1% proc per 10)** — a worked example of an
  intentionally unbounded, behaviour-free tail sitting on top of a bounded, behaviour-rich head.
- **`Cost` as a per-level array** — levelling that reduces a unit's resource cost rather than only raising
  its output is a lever this project's power ladder may or may not express.
- **Penny's Pursuit's eight difficulty modifiers** — six of the eight are player-economy knobs, not enemy
  knobs. Relevant to how a difficulty/wave system decides what to scale.
- **Arena's positional score multiplier (100/60/40/20 by zone)** — turns a binary win into a continuous
  score, which is what any asynchronous or leaderboard mode needs.
- **Objectives as state predicates** — "don't lose more than N", "never have more than N", "produce at least
  N" cost almost nothing per instance and re-price existing strategies. Cheapest content lever documented
  here.
- **The toughness/speed class vocabulary** — closed, coarse, published to the player in the almanac. A
  comparison point for how this project surfaces enemy stats.
- **The 100-DPS convention** — one convention that makes plant HP readable as survival seconds. Worth
  comparing against how many independent damage rates exist in the overlay combat math.
- **Family tags as pure selectors** — a taxonomy that buffs a set for N seconds and does nothing else. Also a
  cautionary example: the overflow family (Contain-mint) is the second-largest.
- **Endless Zone carry-over rules** (Plant Food persists, mowers persist, sun resets, board clears) — an
  explicit statement of which resources are run-scoped vs level-scoped.

---

## What I could not find

Listed so nobody re-spends the budget on these.

**Hard blockers hit during this pass:**

- **Fandom (`plantsvszombies.fandom.com` and all Fandom mirrors) returned HTTP 402 on every fetch.** Every
  Fandom page in this document was reached via its `plantsvszombies.wiki.gg` equivalent instead. If a number
  exists only on Fandom, it is not in this document.
- **The Cutting Room Floor (`tcrf.net`) returned HTTP 403.** Its PvZ2 unused-content and internal-level-name
  pages were not readable.
- **`pvzge.com` (PvZ2 Gardendless documentation, which indexes level-file schemas) returned HTTP 403.**
- **`help.ea.com` timed out** on the official Arena help page. No first-party EA page was successfully read.
- **The web search budget for this session ran out at 200 queries**, so the last portion of the pass was
  fetch-only against URLs already known. Several items below failed for that reason rather than because the
  information does not exist.

**Specific things searched for and not found:**

1. **Plant Food drop rate.** No source gives the probability, the per-wave count, or the level-JSON field
   that governs how often a glowing zombie spawns in a normal level. Only the Endless Zone rule ("about
   level 20+, exactly one per wave") is documented. Searched: wiki Plant Food page, level-JSON tutorials,
   datamine repos.
2. **PvZ2 recharge tier names as shipped fields.** The game stores `PacketCooldown` as a float. The named
   tiers are wiki convention and the sources contradict each other. `plantsvszombies.wiki.gg/wiki/Recharge`
   has "TBA" for the entire PvZ2 section. **No authoritative fast/mediumSlow/verySlow mapping exists for
   PvZ2**; the 7.5 / 30 / 50 values in circulation are PvZ**1**'s.
3. **A clean, unmodified `PlantLevels.json` / `PlantProperties.json` dump.** The only reachable decode is in
   a repo of deliberate modifications. I verified one entry (sunflower) against an independent total and
   caught one entry (peashooter) as modded. **Other entries in that file are unverified.** Nineteendo's
   RTON/OFF, PyVZ2, rton2json and TileTurnip's Tools were named repeatedly but I did not locate a hosted
   decoded corpus.
4. **The level JSON schema.** `ObjectiveModuleProperties`, `SurvivalModuleProperties`,
   `WaveManagerModuleProperties` are referenced but I found no page listing their fields. The one tutorial I
   read (Systempaw72's Part 1) covers only `objclass`/`objdata` basics and
   `SpawnZombiesJitteredWaveActionProps`. **The internal representation of an objective is unknown to me.**
5. **How many waves make a flag in PvZ2.** The `Wave`, `Level` and `Levels (PvZ2)` pages all 404 on wiki.gg.
   Only Survival: Endless's "two flags per wave" (PvZ1) and "levels can have three and four flags"
   (Pyramid of Doom) were found.
6. **Per-Zombot HP table.** Only Zombot Sphinx-inator (~18,500) has a sourced figure. The Zomboss overview's
   "88000 damage" aggregate could not be decomposed. Individual Zombot pages were not fetched.
7. **Endless Zone reward schedule.** No source gives coins/gems per level or per milestone flag.
8. **A quantified per-flag difficulty curve for Endless Zones.** Sources say "increases as the player
   progresses" and "number of zombies increases with every level" — no formula, no table.
9. **Arena per-zombie point values as a table.** Only the endpoints (100 for Basic/Imp, 10,000+ for Zombots)
   and the zone multipliers are documented. The middle of the table was not found.
10. **Arena tournament reward tables (gems/coins by rank).** The wiki says a table exists; the fetch returned
    only the 40%/60% proportional note.
11. **Whether Ail-mint and Hurricane-Mint are in the international build.** The family list includes
    Ail-mint (15 members); the mint purchase list names 13 mints without it; a separate Power Mint category
    listing includes Hurricane-Mint. **The exact international Power Mint roster is unresolved.**
12. **Patch note confirming the 1.7 sky-sun change from 25 to 50.** The wiki states it; I could not reach a
    changelog or press release confirming it.
13. **Any first-party developer statement about difficulty, sun pacing, or the levelling design.**
    Wikipedia carries no such quote and no PopCap/EA design post was reached. Everything in the
    "sun is the difficulty dial" argument here is inferred from the shipped Penny's Pursuit modifier list,
    not from a developer saying it.
14. **Basic Zombie / Buckethead current HP.** Two figures from the same wiki (190 vs 270; 1,290 vs
    1,100+270) and no way to date either. Unresolved.
15. **How the "damage pierce" stat from levelling actually applies** — whether it is a flat damage multiplier,
    an armour-ignoring term, or something else. The name suggests armour interaction; no source explains it.
16. **Modern Day Power Tile specifics** (count required, payoff) — the Modern Day page defers to the Far
    Future mechanic without restating it for its own levels.
17. **Frostbite Caves numeric values** — freeze duration, wind gust interval, thaw time. The world page
    explicitly carries no numbers.
18. **Whether plant level affects Arena matchmaking directly.** Strongly implied (higher levels kill further
    right, scoring more) but never stated as a matchmaking rule.
