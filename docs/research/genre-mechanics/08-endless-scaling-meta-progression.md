# Endless scaling and meta-progression — the curves, and what breaks

Captured 2026-09-02. Companion to [game-design/05-failure-modes.md](../game-design/05-failure-modes.md)
(documented disasters) and [game-design/03-roster-scale.md](../game-design/03-roster-scale.md)
(rarity, power creep, the dead tail). **Those two are not re-derived here.** This file covers what
they do not: curve mathematics, integer inflation, prestige loop structure, and soft-cap formulas.

---

## The finding in one paragraph

**Every shipped endless ladder splits the curve in two, and the split is always the same direction:
enemy durability grows several orders of magnitude faster than enemy lethality.** Diablo III's
Greater Rifts multiply monster HP by exactly 1.17 per tier for all 150 tiers while damage growth
decays through three brackets — over the full ladder that is **HP ×1.44e10 against damage ×2,800, a
5.15-million-fold divergence (computed)**. Diablo IV's Pit does the same thing more bluntly: at tier
111+ HP grows 32% per tier while damage grows 2.37%. Path of Exile's datamined per-level table shows
+7.93%/level life against +6.10%/level damage. The reason is structural, not aesthetic: **a ladder
whose lethality keeps pace becomes a one-shot ladder and stops being playable, so the survivable
version of endless is a damage check.** The second finding is that the two ways of extending a ladder
are not interchangeable — a multiplier ladder is one formula and infinite, a rule ladder (Slay the
Spire's 20 ascensions, Dead Cells' 5 boss cells, Hades' 15 Pact conditions) is hand-authored, finite,
and loses its population roughly ten times faster per step. The third is that number inflation is a
solved problem with a known cost: World of Warcraft has paid for it **four times** with game-wide
squishes because a raid boss reached 70% of the signed 32-bit ceiling, while the idle-game genre
solved it once by moving to a mantissa/exponent Decimal and never squishing again — Antimatter
Dimensions names its first prestige layer *Infinity* after the IEEE-754 double ceiling of 2^1024, and
names the upgrade that lifts it *Break Infinity*.

---

## How to read this

- **FACT** lines are sourced with an inline URL. **INFERENCE** lines are marked and are mine.
- **(computed)** marks arithmetic I did over sourced inputs. The inputs are always cited.
- Game wikis are **second-tier** and marked ⚠ where they are the only source. Datamined game files,
  first-party dev posts and Steam's own global stat pages are first-tier.
- Steam global achievement percentages were read **2026-09-02** and move slowly but do move.

---

## 1. The real curve shapes

### 1.1 The four families, with the games that ship them

| Family | Form | Shipped in | Per-step rate |
|---|---|---|---|
| **Linear response** | `stat = base × (1 + k·L)` | Risk of Rain 2 monster stats | **+30% HP, +20% damage per level** |
| **Exponential** | `stat = base × r^n` | Diablo III Greater Rifts | **r = 1.17 HP/tier, all 150 tiers** |
| **Exponential, bracketed** | `r` changes by range | D3 damage, D4 Pit | **1.13185 → 1.07177 → 1.02337** |
| **Polynomial / root** | `p = c^(1/k)` | Idle prestige currencies | **square root, cube root, ^0.14** |
| **Multiplicative stacking** | `Π(1 + m_i)` | PoE `More`, D4 damage buckets | unbounded, see [02-modifier-stacking](../arpg-effects/02-modifier-stacking.md) |

**The important structural point is that a game can use two families at once, on different axes.**
Risk of Rain 2 is the clean example and the one worth copying: **the driver is exponential, the stat
response is linear.**

### 1.2 Risk of Rain 2 — the exact published formula

FACT. From the game's wiki, which reproduces the shipped constants
([riskofrain2.wiki.gg/wiki/Difficulty](https://riskofrain2.wiki.gg/wiki/Difficulty)):

```text
coeff        = (playerFactor + timeInMinutes × timeFactor) × stageFactor

playerFactor = 1 + 0.3 × (playerCount − 1)
timeFactor   = 0.0506 × difficultyValue × playerCount^0.2
stageFactor  = 1.15 ^ stagesCompleted

difficultyValue = 1 (Drizzle) | 2 (Rainstorm) | 3 (Monsoon)

enemyLevel   = 1 + (coeff − playerFactor) / 0.33
```

And the stat response is flat-linear
([riskofrain2.wiki.gg/wiki/Level](https://riskofrain2.wiki.gg/wiki/Level)):

> *"Just like players, enemies gain 30% health and 20% damage (compared to their base values shown in
> the Logbook) per additional level."*

**Three properties of this design are worth naming.**

1. **Time and stages are separate terms, and only one of them is exponential.** Standing still costs
   you linearly; advancing costs you 15% compounding. That makes "go faster" and "go further" two
   different strategic axes on one number.
2. **Player count enters twice with different exponents** — additively at `0.3` per extra player and
   as `playerCount^0.2` inside the time term. A fifth player adds 120% to the base but only
   `5^0.2 = 1.38×` to the clock rate (computed).
3. **The stat response being linear is what keeps it survivable.** At level 31 an enemy has
   `1 + 0.30 × 30 = 10.0×` base HP, not `1.30^30 = 2,620×`. Worked example, solo Monsoon at 30
   minutes and 5 stages cleared: `coeff = (1 + 30 × 0.1518) × 1.15^5 = 5.554 × 2.0114 = 11.17`,
   giving `enemyLevel = 1 + (11.17 − 1)/0.33 = 31.8` (computed).

Level is capped at **99** in normal modes and **9999** in the Simulacrum
([riskofrain2.wiki.gg/wiki/Simulacrum](https://riskofrain2.wiki.gg/wiki/Simulacrum)), which uses its
own coefficient:

```text
coeff = (1 + 0.0506 × difficultyValue × 1.5 × wave) × 1.02 ^ wave
```

⚠ The wiki states it takes **213 waves on Monsoon** to reach the 9999 cap.

### 1.3 Diablo III Greater Rifts — exact, and the split is stark

FACT ⚠ (Maxroll, the reference community resource, quoting datamined values —
[maxroll.gg/d3/resources/greater-rift-explained](https://maxroll.gg/d3/resources/greater-rift-explained)):

| Quantity | GR 1–25 | GR 26–70 | GR 71–150 |
|---|---|---|---|
| **Monster health** | **×1.17** | **×1.17** | **×1.17** |
| **Monster damage** | ×1.13185 | ×1.07177 | **×1.02337** |
| Experience | handpicked | ×1.08 | ×1.05 |
| Gold | +1.03%/level | +1.03%/level | +1.03%/level |

Blood shards are the one genuinely linear reward: `127 + (GR × 3)`.

**Health uses one rate for the entire 150-tier ladder. Damage uses three, and each is lower than the
last.** Over the whole ladder (computed from the table above):

| | GR1 → GR150 | Doubles every |
|---|---:|---:|
| Monster HP | **×1.44e10** | **4.41 tiers** |
| Monster damage | **×2,800** | 30.0 tiers at the top bracket |
| **Divergence** | **×5.15 million** | — |

Maxroll's separate pushing guide gives the same 1.17 as a cumulative table — *"+50 Tiers = x2193.35
HP"* — and states a solo GR150 Rift Guardian has *"roughly 500 Quadrillion HP"*
([maxroll.gg/d3/resources/greater-rifts](https://maxroll.gg/d3/resources/greater-rifts)).

**INFERENCE.** The bracketing is a retro-fit, not a design from first principles. A single damage rate
that kept pace with 1.17 would have made GR100 unplayable long before GR150 existed; the brackets are
where the team pinned the curve down each time the cap was raised. The shape of the fix — *lower the
lethality slope, never the durability slope* — is the reusable part.

### 1.4 Diablo IV's Pit — same split, published as brackets from the start

FACT ⚠ ([maxroll.gg/d4/resources/difficulty-overview](https://maxroll.gg/d4/resources/difficulty-overview)),
described by the source as *"rough Pit scaling factors"*:

| Pit tier | Damage dealt per tier | Health points per tier |
|---|---:|---:|
| 2 | 15% | 50% |
| 3 | 13% | 33% |
| 4–10 | 17.4% | 26.5% |
| 11–110 | **4.74%** | **17%** |
| **111+** | **2.37%** | **32%** |

**At tier 111 the two curves move in opposite directions in the same patch note: HP growth nearly
doubles while damage growth halves.** That is the clearest published statement anywhere that a deep
ladder is meant to be a damage check.

Paragon is the counterweight and it is **finite**: 300 points from Paragon progression, up to 342 with
seasonal rank rewards, 5 boards from 9 per class, glyph radius 3 → 4 at level 25 → 5 at level 51, max
glyph level 150 ([maxroll.gg/d4/resources/paragon-boards](https://maxroll.gg/d4/resources/paragon-boards)).
**Diablo IV pairs an unbounded enemy ladder with a hard-capped player ladder.** Diablo III did the
opposite — Reaper of Souls made Paragon account-wide and unlimited
([en.wikipedia.org/wiki/Diablo_III:_Reaper_of_Souls](https://en.wikipedia.org/wiki/Diablo_III:_Reaper_of_Souls)).

### 1.5 Path of Exile — the datamined per-level table

FACT, first-tier. From RePoE's export of the game's own `default_monster_stats`
([raw.githubusercontent.com/brather1ng/RePoE/master/RePoE/data/default_monster_stats.json](https://raw.githubusercontent.com/brather1ng/RePoE/master/RePoE/data/default_monster_stats.json)):

| Level | Life | Damage | Accuracy | Evasion | Ally life |
|---:|---:|---:|---:|---:|---:|
| 1 | 22 | 4.99 | 14 | 67 | 15 |
| 10 | 78 | 11.62 | 23 | 259 | 35 |
| 20 | 207 | 24.47 | 37 | 563 | 75 |
| 30 | 467 | 46.70 | 59 | 994 | 148 |
| 40 | 972 | 84.26 | 92 | 1,597 | 275 |
| 50 | 1,927 | 146.53 | 140 | 2,426 | 493 |
| 60 | 3,698 | 248.37 | 211 | 3,556 | 862 |
| 68 | 6,127 | 373.55 | 290 | 4,739 | 1,329 |
| 70 | 6,937 | 413.01 | 314 | 5,081 | 1,478 |
| 75 | 9,436 | 529.54 | 381 | 6,029 | 1,926 |
| 80 | 12,787 | 676.75 | 462 | 7,124 | 2,500 |
| 83 | 15,319 | 782.94 | 518 | 7,860 | 2,919 |
| 84 | 16,265 | 821.73 | 538 | 8,120 | 3,074 |
| 90 | 23,250 | 1,096.00 | 675 | 9,844 | 4,178 |
| 95 | 31,220 | 1,389.64 | 814 | 11,519 | 5,381 |
| 100 | 41,817 | 1,758.17 | 980 | 13,445 | 6,916 |

Derived (computed over the table):

| | L1 → L100 | Per level | Doubles every |
|---|---:|---:|---:|
| Life | **×1,900.8** | **+7.93%** | 9.09 levels |
| Damage | **×352.3** | **+6.10%** | 11.70 levels |
| **Divergence** | **×5.40** | — | — |

**The 352× damage figure matches the number already in
[03-roster-scale.md §4](../game-design/03-roster-scale.md) exactly**, which is a useful cross-check on
both. That file's 2,989× life figure is larger than the 1,900.8× here; the difference is almost
certainly a rarity multiplier applied on top of the base table (magic and rare monsters carry their
own life multipliers). Both are correct for what they measure.

**The accuracy column is the quiet one and it is the most interesting.** Accuracy goes ×70 while life
goes ×1,901. Path of Exile deliberately lets the *contest* stat grow two orders of magnitude slower
than the *magnitude* stat. That is the same separation this project encodes as `Θ` versus `P(Θ)`.

### 1.6 Last Epoch corruption

FACT (weak — forum, developer participating but not stating numbers):
Corruption is described by players in a thread a developer replied in as giving *"<8% more health and
damage per corruption increase"*, and the developer describes the system as **infinitely scaling,
i.e. eventually unbeatable depending on character strength**, with corruption modifiers applying
multiplicatively against additive enemy modifiers
([forum.lastepoch.com](https://forum.lastepoch.com/t/how-high-corruption-removes-the-desire-to-play/47806)).
⚠ No official per-point formula was reachable. See "What I could not find".

**Last Epoch is the one mainstream ARPG that scales HP and damage together**, and the community
complaint thread this came from is titled *"how high corruption removes the desire to play."*
**INFERENCE:** that is the predicted symptom of not splitting the two curves.

### 1.7 Cost and failure, per curve family

| Curve | What it solves for the player | What it costs the designer | What breaks when tuned wrong |
|---|---|---|---|
| **Linear response** | Numbers stay readable; a 10× is felt as 10× | Needs a separate exponential *driver* or progress stalls | Too shallow and the ladder has no teeth after an hour |
| **Exponential, single rate** | One constant covers infinite content | Nothing — until the cap moves | Lethality outruns survivability; the ladder becomes a one-shot wall |
| **Exponential, bracketed** | Keeps a deep ladder playable at both ends | Every bracket boundary is a balance cliff and a permanent maintenance item | A boundary in the wrong place makes one tier range strictly the best farm |
| **Root / polynomial prestige** | Compresses arbitrary magnitude into a small number | Requires a second currency and a reset ritual | Wrong exponent and resets are either mandatory-every-minute or never worth it |
| **Multiplicative stacking** | Build expression; every source feels good | Combinatorial balance surface | Every build must stack every bucket — see D4, §8 |

---

## 2. Number inflation and how games survive it

### 2.1 Who hit the ceiling

FACT. World of Warcraft stored health in signed 32-bit integers. Ra-den, the heroic-only Throne of
Thunder boss, *"started at roughly 1,500,000,000 health in 25-player mode"*, and mistakes could push
it high enough to *"overflow to a negative value"*
([warcraft.wiki.gg/wiki/Stat_squish](https://warcraft.wiki.gg/wiki/Stat_squish)).
**1.5e9 is 69.9% of 2,147,483,647 (computed).** This is already recorded as
[05-failure-modes §8](../game-design/05-failure-modes.md); it is repeated here because it is the
anchor for everything below.

### 2.2 The squish register

FACT ⚠ ([warcraft.wiki.gg/wiki/Stat_squish](https://warcraft.wiki.gg/wiki/Stat_squish)):

| Patch | Expansion | Year | What was squished |
|---|---|---:|---|
| 6.0.2 | Warlords of Draenor | 2014 | Stats only — item levels untouched |
| 8.0.1 | Battle for Azeroth | 2018 | Stats **and** item levels |
| 9.0.1 | Shadowlands | 2020 | Stats, item levels **and character levels** (120 → 50, new cap 60) |
| 12.0.0 | Midnight | — | Stats and item levels |

The 2014 pass is described as reducing stats to roughly **4%** of prior value and player health to
roughly **8%**; a separate contemporaneous summary put it at *"about 1/10"*. ⚠ The two figures
disagree and I could not resolve which convention each used — treat the ratio as "roughly one order of
magnitude" and not more precisely than that.

Blizzard's stated framing was that the squish *"would not affect the relative difficulty of killing
any creature"*, and that the pre-squish state had reached the point where *"the average
damage-specialized raider had more health than most bosses in Molten Core."*

**The lesson is not "they squished." It is that they squished four times.** A squish is not a fix; it
is a payment on a debt that keeps accruing, and each payment costs a full audit of every number in
the game.

### 2.3 Diablo III did not squish — it changed the display

FACT. Blizzard's own engineering post *Engineering Diablo III's Damage Numbers* (2016-01-22) went with
abbreviation and colour rather than a squish. The reasoning, as reported:

> Blizzard opted **not** to abbreviate in the low millions because seeing **"1,000,000" was more
> satisfying than "1M"**, and they **skipped the billions place** because **"1,000M" tells a much more
> exciting story than "1B."**

⚠ The original Blizzard URL
(`kr.diablo3.blizzard.com/en-us/blog/19996041/engineering-diablo-iiis-damage-numbers-1-22-2016`) now
301s to the site root; the passage survives in coverage at
[blizzardwatch.com](https://blizzardwatch.com/2016/01/25/diablo-damage-numbers-changed/) and
[massivelyop.com](https://massivelyop.com/2016/01/24/diablo-iii-explains-damage-number-abbreviations-and-colors/).
Blizzard also stated that the preference is **personal and cultural** — that different locales read
number length differently.

**This is the most directly useful design statement in this whole file.** Blizzard's engineers
concluded that a player's satisfaction tracks the *rendered length* of the number, not its value —
which is why they refused to compress `1,000M` to `1B` even though `1B` is shorter, more accurate and
easier to read. Their audience wanted the digits.

### 2.4 The design lesson: where a quantity becomes a tier label

**INFERENCE, and it is the answer the brief asks for.** Three thresholds, in order:

1. **Up to ~6 digits a number is a quantity.** The player subtracts, compares, and plans with it.
   "I hit for 4,200 and it has 18,000 HP" is a plan.
2. **From ~7 digits to the first suffix, a number is a magnitude.** The player reads the leading two
   digits and the length. "4.2M against 18M" is still a comparison but no longer arithmetic. Blizzard
   deliberately parked Diablo III's whole endgame in this band by refusing the billions suffix.
3. **Past the second suffix it is a tier label.** Nobody reads `1.44e10` as a count of anything. It is
   a badge that says *how far up you are*. Cookie Clicker's achievement ladder is explicit about this
   — the reward for `1e18` cookies is the word *"Immortal bakery"*, not the number
   ([steamcommunity.com/stats/1454400/achievements](https://steamcommunity.com/stats/1454400/achievements/)).

The practical consequence: **once the display crosses into band 3, the number has already stopped
doing the work, and the tier label should be first-class data rather than a rendering side effect.**
The idle genre reached this conclusion and shipped it — a named tier per magnitude — while the ARPG
genre keeps squishing to stay in band 2.

### 2.5 What the idle genre did instead

FACT. Idle games abandoned native numeric types entirely.

- `break_infinity.js` represents a value as *"a mantissa (absolute value between [1, 10) or exactly 0)
  and an exponent"*, reaching **1e(9e15)**, and is explicitly for *"incremental games which need to
  deal with very large numbers … and want to prioritize speed over accuracy."* Antimatter Dimensions'
  *"script time improved by 4.5x after swapping from decimal.js to break_infinity.js"*
  ([github.com/Patashu/break_infinity.js](https://github.com/Patashu/break_infinity.js/blob/master/README.md)).
- `break_eternity.js` is the sequel and reaches **10^^1e308** — tetration, not exponentiation
  ([github.com/Patashu/break_eternity.js](https://github.com/Patashu/break_eternity.js/)).
- Antimatter Dimensions names the ceiling directly: *"The value of Infinity is 2^1024 ≈ 1.79769e308 …
  It is the Limit of 64-bit IEEE floating point numbers (this applies in JavaScript), and is the
  maximum value that a number can normally get in JavaScript applications."*
  ([antimatterdimensions.wiki.gg/wiki/Infinity](https://antimatterdimensions.wiki.gg/wiki/Infinity))
- Wikipedia records the genre convention of scientific notation, short suffixes, and *"special naming
  schemes for extremely large numbers"* such as *"duoquadragintillion"*, noting this *"sometimes
  requires specialized data types or numerical libraries"*
  ([en.wikipedia.org/wiki/Incremental_game](https://en.wikipedia.org/wiki/Incremental_game)).

**Antimatter Dimensions is the sharpest artefact in this file.** Its first prestige layer is *named
after the float ceiling*. Hitting `1.79e308` is not a bug there — it is the first act break. And the
upgrade that lets you exceed it is called **Break Infinity**, which is literally the moment the game
swaps its number representation in front of the player. A representation limit was converted into
content.

### 2.6 Cost and failure

| Approach | Solves for the player | Costs the designer | Breaks when wrong |
|---|---|---|---|
| **Squish** | Restores readable numbers | A full audit of every number, every squish, forever | Relative difficulty shifts silently; a missed table becomes a balance bug |
| **Abbreviate, don't squish** | Keeps the satisfying digits | Locale-sensitive display rules; the ceiling is still coming | Suffix boundaries chosen badly and the player reads a nerf that never happened |
| **Big-number type** | Genuinely unbounded | Every arithmetic path must use it; performance work; no native comparison | One `double` left in the pipeline silently truncates the top of the range |
| **Named tiers** | Player gets a badge instead of a digit string | A naming vocabulary to author and localise | Runs out of names, or the names imply an ordering the mechanics don't have |

---

## 3. Idle and incremental games — endless done deliberately

This genre is the only one that treats endless as the primary problem. Two of the three parts of
Anthony Pecorella's *The Math of Idle Games* (Kongregate, republished on Game Developer) are the best
first-party-adjacent source that exists.

### 3.1 The base loop

FACT ([gamedeveloper.com — Part I](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i)):

```text
cost_next        = cost_base × growth_rate ^ owned
production_total = (production_base × owned) × multipliers

bulk cost of n   = b × r^k (r^n − 1) / (r − 1)
max affordable   = floor( log_r( c(r−1) / (b·r^k) + 1 ) )
```

Shipped constant, AdVenture Capitalist Lemonade Stands: `growth_rate = 1.07`, base cost 4, base
production 1.67/sec.

**The whole genre rests on one asymmetry: cost is exponential in count, production is linear in
count.** Early on production wins; eventually cost always wins. The reset exists because that
crossover is guaranteed.

### 3.2 The layered alternative — generators that make generators

FACT ([Part II](https://www.gamedeveloper.com/game-platforms/the-math-of-idle-games-part-ii)). If tier
*n* produces tier *n−1*, total production follows successive integrals `1, x, x²/2, x³/6, x⁴/24, …,
xⁿ/n!`, and *"as we get more and more tiers of generators (as n goes up), we approach e^x, which is …
exponential growth!"* Derivative Clicker's costs are `5 × 1.1^n` against that sub-exponential
production.

**This is the trick worth stealing.** A chain of *k* tiers gives you a polynomial of degree *k* for
free — a curve that is steep, tunable by adding a tier, and does not reach the ceiling for far longer
than a raw exponential. And it converges to exponential in the limit, so you never have to choose.

### 3.3 The prestige formulas, as shipped

FACT ([Part III](https://www.gamedeveloper.com/design/the-math-of-idle-games-part-iii)). Prestige is
described there as *"the ability to reset your game with a multiplier to progress"*, serving two
purposes: a ladder-climbing feel, and **"reining growth into manageable numbers."**

| Game | Prestige formula | Based on | **Earnings needed to double prestige** |
|---|---|---|---:|
| **Realm Grinder** | `p = (√(1 + 8·c_M/1e12) − 1) / 2` | max currency earned | **4×** |
| **AdVenture Capitalist** | `p = 150·√(c_L/1e15)` | lifetime earnings | **~3–4×** |
| **Cookie Clicker** | `p = ∛(c_L/1e12)` | lifetime earnings | **~8×** |
| **Egg, Inc.** | `Δp = (c_R/1e6)^0.14` | current-run earnings | **~128× (2^7)** |
| **Clicker Heroes** | based on upgrade count | — | logarithmic; *"substantially reins in numbers"* |

Realm Grinder's is the inverse of a triangular number — `c_M = 1e12 · p(p+1)/2` — so **prestige is the
quadratic-inverse of currency.** That is exactly the shape of a quadratic power ladder read backwards.

**The documented rule of thumb, stated as a range rather than a single number: doubling your permanent
multiplier should cost between 4× and 8× the previous run's output.** Egg, Inc.'s 128× is the extreme
of the deliberate-rein-in end; Clicker Heroes' log-of-upgrades is the other.

**INFERENCE.** The 4×–8× band is a statement about *session count*, not about numbers. If a run's
output grows ~2× per session, a 4× requirement means a reset every two sessions and an 8×
requirement means every three. Egg, Inc.'s 128× means roughly one reset per seven sessions. Pick the
exponent by choosing the reset cadence first, then solving for it.

### 3.4 Layered prestige

FACT ([antimatterdimensions.wiki.gg/wiki/Prestige](https://antimatterdimensions.wiki.gg/wiki/Prestige),
and the developer's own how-to at [ivark.github.io/howto](https://ivark.github.io/howto)):

| Layer | Trigger | Grants | Formula (developer's own wording) |
|---|---|---|---|
| **Big Crunch / Infinity** | reach 2^1024 antimatter | Infinity Points, a new upgrade tree | `10^(log(antimatter)/n − 0.75) × bonuses`, `n = 308` by default |
| **Eternity** | — | Eternity Points, a different upgrade system, a new dimension type | `floor(5^(floor(log10(IP))/308 − 0.7) × bonuses)` |
| **Reality** | — | Reality Machines, the Glyph system, further layers | — |

Big Crunch *"resets most things encountered before Infinity … as well as some things within the
Infinity layer, such as Replicanti, Replicanti Galaxies, Infinity Dimension amounts (not purchases!)"*
— note the parenthesis: **a layer reset is never total; the deliberate exceptions are the design.**

**The mathematical shape of layered prestige is: each layer's currency is a logarithm of the previous
layer's magnitude.** `log10(antimatter)/308` is the operative term. That single division is what
converts an unbounded quantity into a small, readable, linear-feeling number — and it is why AD can
run for years without the player ever seeing an unreadable currency in the layer they are actually
playing.

Other machinery from the same game, all first-party:

```text
Dimensional Sacrifice = (log(n)/10)^m      m = 2 base, 2.2 or 2.5 with achievements
Dimension Boost/Shift = ×2 (base) to first dimension, each
Antimatter Galaxy     = +2% multiplicative to the tickspeed reduction effect, per purchase
Tickspeed             = −11% per purchase before galaxies
```

⚠ Wikipedia names Realm Grinder as the exemplar of *"multiple layers of prestige systems, unlocking
entirely new content, meta-currencies, or gameplay modes"*, and Clicker Heroes as the mobile pioneer
of prestige ([en.wikipedia.org/wiki/Incremental_game](https://en.wikipedia.org/wiki/Incremental_game)).

### 3.5 Cost and failure

| Mechanic | Solves for the player | Costs the designer | Breaks when wrong |
|---|---|---|---|
| **Single prestige** | Escape from a stalled curve; a reason to re-run known content | A second currency, a reset ritual, and every system must know what survives a reset | Too cheap and the game is a reset simulator; too dear and players grind a dead run |
| **Layered prestige** | Genuinely unbounded play; each layer is new content, not the same content | Each layer is a whole new subsystem — AD's three layers are three games | A layer that only multiplies the last one adds no play, just a bigger number |
| **Generator chains** | Steep growth without a steep constant | *k* tiers of UI, balance and naming | Lower tiers become irrelevant — Pecorella's own fix is "+0.05% to all tier-1 per tier-1 owned" |
| **Log-compressed currency** | Numbers stay readable forever | The player must be taught that 10× input is +1 output | Feels like a nerf on the first reset unless the payoff is visible immediately |

---

## 4. Roguelite meta-progression — and the core comparison

### 4.1 Slay the Spire — 20 steps, and almost none of them are multipliers

FACT ([slaythespire.wiki.gg/wiki/Ascension](https://slaythespire.wiki.gg/wiki/Ascension)). The full
ladder:

| A | Change | Kind |
|---:|---|---|
| 1 | Elites spawn more often — *"~60% more Elites will spawn"* | **rule + rate** |
| 2 | Normal enemies deal more damage | multiplier |
| 3 | Elites deal more damage | multiplier |
| 4 | Bosses deal more damage | multiplier |
| 5 | *"Only heal for 75% of your missing health after a boss fight instead of 100%"* | **economy** |
| 6 | Lose 10% of your health at the start of a run | **economy** |
| 7 | *"Normal enemies have more health and sometimes gain more Block"* | multiplier + **rule** |
| 8 | *"Elites have more health and sometimes gain more Block"* | multiplier + **rule** |
| 9 | *"Bosses have more health and sometimes gain more Block"* | multiplier + **rule** |
| 10 | Start each run with an Ascender's Bane curse card | **rule** |
| 11 | One fewer potion slot | **rule** |
| 12 | *"Upgraded cards appear half as often in Act 2 and Act 3"* | **economy** |
| 13 | Bosses drop 25% less gold | **economy** |
| 14 | *"-5 max HP for Ironclad, -4 max HP for Silent, Defect, and Watcher"* | **economy** |
| 15 | *"Many events have less positive outcomes and more severe consequences"* | **rule** |
| 16 | *"Everything costs 10% more"* | **economy** |
| 17 | Normal enemies get new movesets and abilities | **rule** |
| 18 | Elites get new movesets and abilities | **rule** |
| 19 | Bosses get new movesets and abilities | **rule** |
| 20 | **Double boss** — fight two bosses at the end of Act 3 | **rule** |

**Count (computed): 4 of 20 steps are purely a bigger number. 16 change a rule or an economy.** And
the multipliers that do exist are small and mostly unstated — the wiki gives exact values only for the
economy steps (75%, 10%, half, 25%, 10%, −5/−4 HP), never for the damage and health steps. ⚠ The exact
A2/A3/A4 and A7/A8/A9 percentages were not reachable; see "What I could not find."

**The three-way A2/A3/A4 and A7/A8/A9 split is the structural trick.** One conceptual change —
"enemies are deadlier" — is spent as three separate ladder rungs by applying it to normals, then
elites, then bosses. That is how you get 20 rungs out of far fewer ideas, and each rung still reads as
a distinct thing the player must adapt to.

### 4.2 Dead Cells — five rungs, each removing a resource

FACT ([deadcells.wiki.gg/wiki/Boss_Stem_Cell](https://deadcells.wiki.gg/wiki/Boss_Stem_Cell)):

| BC | Named | The change that matters |
|---:|---|---|
| 0 | Normal | baseline |
| 1 | Hard | boss early phases change; **Health Fountains only at every other Passage** |
| 2 | Very Hard | *"Removes all Health Fountains from Passages, but they will still contain one Flask charge each"*; cells ×2 |
| 3 | Expert | *"Passages now only contain 3 Flask charges in total"*; item level +1; scroll fragments unlock |
| 4 | Nightmare | *"Increases the detection range of mobs and gives most of them the ability to teleport"*; **no flask charges at all**; item level +3 |
| 5 | Hell | **The Malaise is activated**; unlocks the seventh stage (Astrolab) |

**Not one of the five rungs is an enemy stat multiplier.** Every rung takes away healing, adds an
enemy behaviour, or adds a systemic pressure (Malaise). And each rung *gives* something back — cell
multipliers ×2/×2/×3, item level +1/+3, new content at BC5.

### 4.3 Hades — the player builds the ladder, out of 15 orthogonal dials

FACT ⚠ ([rpgsite.net](https://www.rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards)).
15 conditions, **63 total Heat, 64 in Hell Mode**:

| Condition | Ranks | Effect per rank | Kind |
|---|---:|---|---|
| Hard Labor | 5 | foes deal **+20%** damage | multiplier |
| Lasting Consequences | 4 | healing **−25%** | **economy** |
| Convenience Fee | 2 | prices **+40%** | **economy** |
| Jury Summons | 3 | **+20%** more enemies | density |
| Extreme Measures | 3 | upgrades one boss with new techniques | **rule** |
| Calisthenics Program | 2 | enemy health **+15%** | multiplier |
| Benefits Package | 2 | armoured enemies gain perks | **rule** |
| Middle Management | 2 | adds an elite or distraction to mini-boss fights | **rule** |
| Underworld Customs | 2 | sacrifice 1 boon on leaving a region | **economy** |
| Forced Overtime | 2 | enemy speed **+20%** | multiplier |
| Heightened Security | 1 | traps and magma deal **+400%** | multiplier |
| Routine Inspection | 4 | **deactivates 3 Mirror of Night talents** | **meta-strip** |
| Damage Control | 2 | enemies gain shielded health | **rule** |
| Approval Process | 2 | one fewer boon choice | **economy** |
| Tight Deadline | 2 | region timer **−2 min** (7 min floor) | **rule** |

The rewards are gated *per Heat point*: adding one point re-opens the run's chance at Titan Blood,
Diamonds and Ambrosia, which is why the guide's advice is *"don't increase your heat meter by more
than one point at a time"* — skipping a level forfeits its drop.

**Hades' answer to endless is combinatorial, not linear.** 15 axes with 1–5 ranks each is a large
space of *shapes* of difficulty, and the player picks which shape. It is also the only design in this
file where the meta-progression layer (Mirror of Night) is directly attackable by the difficulty layer
(Routine Inspection turns off 3 Mirror talents per rank, 12 at max). **The permanent-power system and
the difficulty ladder are wired to each other rather than living in separate silos.**

### 4.4 ⭐ Endless via multiplier versus endless via added rules

**This is the core comparison the document exists for.**

| | **Endless via multiplier** | **Endless via added rules** |
|---|---|---|
| **Examples** | D3 Greater Rifts (150 tiers, one constant), D4 Pit (200 tiers), RoR2 coefficient, Last Epoch corruption | Slay the Spire (20), Dead Cells (5), Hades Pact (15 dials / 63 heat), D4 Torment (4 named tiers) |
| **Length** | unbounded, free | finite; every rung is hand-authored |
| **Cost to build** | one formula, one column | one implementation, one test, one piece of UI copy per rung |
| **Cost to maintain** | rebalances itself when content is added | **every new enemy, item and event must be re-checked against every rung** |
| **What the player learns** | which build scales | which behaviour to change |
| **Content reuse** | total — the same fight, indefinitely | partial — the fight genuinely differs |
| **Signature failure** | the ladder becomes a single gear check; builds converge on the one that scales | the ladder ends, and the player is done |
| **Where the numbers hide** | in the enemy | in the player's resources — healing, potions, gold, boons, max HP |
| **Population depth (§6)** | shallow decay | **steep decay** |

**The single most transferable observation: the rule ladders overwhelmingly attack the player's
*economy*, not the enemy's statline.** Count them — StS removes healing, potion slots, upgrade
frequency, gold and max HP; Dead Cells removes health fountains and then flask charges entirely;
Hades removes healing efficiency, boon choices, shop affordability and Mirror talents. **A multiplier
makes the enemy bigger. A rule makes the player smaller, and "smaller" has far more distinct flavours
than "bigger" does.**

**INFERENCE, and I hold it fairly strongly:** the two are not alternatives, they are different-rate
systems that belong together. Rules are expensive and finite, so spend them where the population
actually is (the first 10–20 rungs). Multipliers are free and infinite, so run them underneath and
past the end of the authored rungs. Diablo IV is the shipped instance of exactly this — 4 named
Torment tiers (rules and rewards) sitting on top of a 200-tier Pit (pure multiplier).

### 4.5 Cost and failure

| Mechanic | Solves for the player | Costs the designer | Breaks when wrong |
|---|---|---|---|
| **Discrete difficulty rungs** | A named, bragging-rights goal; each rung is a fresh problem | Linear authoring cost; a combinatorial re-test cost as content grows | Two adjacent rungs feel identical, and the ladder reads as padding |
| **Player-built ladder (Pact)** | Player tunes which axis hurts; huge replay space | Every dial must be independently balanced *and* sane in combination | One dial is strictly cheapest per point and everyone takes the same five |
| **Permanent meta-currency (Mirror)** | Failure still pays; the floor rises | Must not trivialise the base game; needs a sink at max | Either it never matters, or the first-run experience becomes unwinnable-by-design |
| **Per-step reward gating** | A reason to climb one rung at a time | Reward tables per rung | Skipping is optimal, and the ladder gets speedrun to the top and abandoned |

---

## 5. Soft caps, diminishing returns, and their formulas

### 5.1 Hyperbolic — the workhorse, and the one with the best property

FACT, four independent shipped instances of the same form:

```text
reduction   = X / (X + K)
multiplier  = K / (X + K)
```

| Game | Stat | K | Source |
|---|---|---:|---|
| League of Legends | Armour / MR | **100** | [wiki.leagueoflegends.com/en-us/Armor](https://wiki.leagueoflegends.com/en-us/Armor) |
| Risk of Rain 2 | Armor | **100** | [riskofrain2.wiki.gg/wiki/Armor](https://riskofrain2.wiki.gg/wiki/Armor) |
| Diablo III | Armor | **3,500** | [maxroll.gg/d3/resources/damage-reduction-explained](https://maxroll.gg/d3/resources/damage-reduction-explained) |
| Diablo III | Resistance | **350** | same |

RoR2 also publishes the negative branch, which is the part most games forget to define:

```text
armor ≥ 0 : damage taken × 100/(100 + armor)     → approaches 0, never reaches it
armor < 0 : damage taken × (2 − 100/(100 − armor)) → approaches 2×, never reaches it
```

**Why this form and not another — the property that matters (computed, algebra):**

```text
EHP = HP / (1 − X/(X+K))
    = HP × (X + K)/K
    = HP × (1 + X/K)
```

**Effective HP is exactly linear in the stat.** Every additional `K` points of armour adds one more
copy of your base HP. The percentage displayed diminishes forever, the actual survivability never
does, and the input is unbounded with no cap needed anywhere. **This is the single most reusable
formula in this document**, and it is the direct answer to the subtractive-mitigation wall recorded at
[05-failure-modes §10](../game-design/05-failure-modes.md) — Total War's linear armour hits 100%
reduction at 200 and forces every weapon in the game to carry an armour-piercing channel as a tax.

`K` is the only tuning knob and it has a plain meaning: **K is the amount of the stat that doubles
your effective HP.** Diablo III's choice of 3,500 for armour and 350 for resistance says resistance is
worth exactly 10× armour point-for-point.

### 5.2 Multiplicative stacking — diminishing returns for free, no cap required

FACT ([maxroll.gg/d3/resources/damage-reduction-explained](https://maxroll.gg/d3/resources/damage-reduction-explained)):

```text
damage taken = incoming × (1 − DR₁) × (1 − DR₂) × (1 − DR₃) × …
```

> *"all these sources and different kinds of DR always stack **multiplicatively**"* — each source
> *"reduces damage that you take by stated amount. That doesn't change regardless of what other
> damage reduction you may have."*

**Each new source is worth the same proportion and a smaller absolute amount than the last, with no
threshold, no bracket and no cap.** Total reduction is `1 − Π(1−rᵢ)` — asymptotic to 1, reaching it
only if some source is exactly 1.0. The design cost is zero and the failure mode is precise: **any
single source allowed to reach 1.0 collapses the whole product to immunity.** Diablo III's own
exception proves it — the guide notes block chance *"is possible to stack … all the way to 100%"*
despite a tooltip claiming a 75% cap.

### 5.3 Tiered brackets — the income-tax model

FACT ⚠ ([maxroll.gg/wow/resources/stat-diminishing-returns](https://maxroll.gg/wow/resources/stat-diminishing-returns)).
World of Warcraft applies diminishing returns to secondary stats in percentage-point brackets:

| Bracket (percentage points of the stat) | Penalty |
|---|---:|
| 0 → 30 | none |
| 30 → 39 | 10% |
| 39 → 47 | 20% |
| 47 → 54 | 30% |
| 54 → 66 | 40% |
| 66 → 126 | 50% |
| beyond 126 | **hard cap — no further gain from rating** |

Bracket widths, in order: **30, 9, 8, 7, 12, 60 (computed)**. The critical implementation detail:

> *"Each diminishing return or penalty for secondary stats is only applied to the rating that crosses
> each threshold"*

— that is, it is **marginal**, like a tax band, not a retroactive multiplier on the whole stat. Rating
conversions at level 90: Haste 44 rating per 1%, Crit 46, Versatility 54.

**INFERENCE.** The bracket table is strictly worse than a hyperbolic curve on every axis except one:
it is legible. A player can be told "you are in the 20% band, the next 8 points are worth 80%." The
hyperbolic curve has no bands to name. Blizzard chose seven rows of authored data plus a hard cap over
one formula, and the only thing they bought was explicability — and they still needed the cap at 126,
because brackets do not asymptote on their own.

### 5.4 The opposite — deliberately unbounded stacking

FACT and cross-reference. Path of Exile's `More` multipliers compose as `Π(1 + m)` with no cap, and
Diablo IV historically presented damage as *"many % damage affixes that historically stacked as
near-independent multipliers → exponential power creep"*, forcing a Season 2+ rework toward clearer
`[x]` buckets — recorded in [arpg-effects/02-modifier-stacking.md](../arpg-effects/02-modifier-stacking.md).

The recorded lesson there is verbatim: *"**too many independent mult buckets** ≈ every build must
stack all of them."*

**INFERENCE.** Unbounded stacking is not the failure. The failure is unbounded stacking across *many
independent families*, because then each family is mandatory and build diversity collapses to
"whoever found one of each." One unbounded family is expression; six is a checklist.

### 5.5 Cost and failure

| Form | Solves for the player | Costs the designer | Breaks when wrong |
|---|---|---|---|
| **Hyperbolic X/(X+K)** | Gear never stops mattering; no wall | Explaining why 50% → 66% took as much armour as 0% → 50% | K set too low and the stat is instantly saturated; too high and it feels inert |
| **Multiplicative product** | Every source is worth taking | Nothing | One source at 1.0 = immunity |
| **Tiered brackets** | Legible; nameable bands | An authored table per stat, re-tuned every content patch | Bracket edges become build targets; a hard cap is still needed at the end |
| **Subtractive** | Trivially intuitive | Cheap to build, expensive to live with | Hits 100% and forces a universal piercing channel — see failure-modes §10 |
| **Unbounded stacking** | Maximum expression | Combinatorial balance | Many independent families → every build must have all of them |

---

## 6. Content-per-hour and the grind wall

No studio publishes retention curves for a difficulty ladder. **Steam's global achievement statistics
are the closest thing to a public dataset, and they are first-party.** Percentages below are of all
owners, read 2026-09-02.

### 6.1 Where the population actually stops

| Game | Rung | % of owners | Source |
|---|---|---:|---|
| **Slay the Spire** | Unlock Ascension mode | **66.5%** | [stats/646570](https://steamcommunity.com/stats/646570/achievements/) |
| | Beat the game (Ironclad) | 56.2% | |
| | Beat the game (Watcher, 4th character) | 26.1% | |
| | **Ascension 10** | **14.9%** | |
| | **Ascension 20** | **7.3%** | |
| | Every achievement | 2.7% | |
| **Hades** | Clear Tartarus (zone 1) | **81.9%** | [stats/1145360](https://steamcommunity.com/stats/1145360/achievements/) |
| | Clear Asphodel (zone 2) | 72.4% | |
| | Clear Elysium (zone 3) | 59.0% | |
| | **First full escape** | **46.9%** | |
| | One rank in every Mirror of Night talent | **11.7%** | |
| | Clear Elysium with Extreme Measures | 13.4% | |
| | Reach the epilogue | 8.4% | |
| **Risk of Rain 2** | Loop back to stage 1 | **60.8%** | [stats/632360](https://steamcommunity.com/stats/632360/achievements/) |
| | Beat the game | 51.0% | |
| | Complete 20 stages in one run | **24.4%** | |
| | Beat the game on Monsoon | **23.7%** | |
| **Vampire Survivors** | Hyper Inlaid Library (early) | **77.7%** | [stats/1794680](https://steamcommunity.com/stats/1794680/achievements/) |
| | Hyper Cappella Magna (late) | 38.3% | |
| | Hyper Abyss Foscari (latest) | **6.9%** | |
| | Inverse Mad Forest, level 80 | 13.7% | |
| | Inverse Dairy Plant, level 80 | **3.3%** | |

### 6.2 The magnitude ladder loses people far more slowly

Cookie Clicker's achievement chain is a pure magnitude ladder with nothing else changing
([stats/1454400](https://steamcommunity.com/stats/1454400/achievements/)):

| Cookies baked | Achievement | % of owners |
|---|---|---:|
| 1e3 | Making some dough | 98.7% |
| 1e6 | Fledgling bakery | 95.0% |
| 1e9 | World-famous bakery | 85.8% |
| 1e12 | Galactic bakery | 69.2% |
| 1e15 | Timeless bakery | 50.2% |
| **1e18** | **Immortal bakery** | **43.1%** |
| — | Ascend at least once | 58.0% |

### 6.3 ⭐ The comparison

**Computed from the tables above:**

| Ladder | Span | Retention across the span | Loss per step |
|---|---|---:|---|
| Cookie Clicker magnitude | **15 orders of magnitude** | 98.7% → 43.1% = **×0.437** | **~5% per 10×** |
| Slay the Spire ascensions | **20 authored rungs** | 66.5% → 7.3% = **×0.110** | **~10% per rung** |
| Vampire Survivors Hyper stages | 7 steps, early → latest | 77.7% → 6.9% = ×0.089 | ~29% per stage |
| Hades zone chain | 3 zones to first full escape | 81.9% → 46.9% = ×0.573 | ~17% per zone |

**A magnitude ladder is the cheapest possible retention: fifteen orders of magnitude cost Cookie
Clicker less than 60% of its population, while twenty rule changes cost Slay the Spire 89% of its.**

**INFERENCE, and the caveat is real.** These are not the same population or the same kind of effort —
Cookie Clicker's rungs are waiting, Slay the Spire's are skill. But the direction is not in doubt and
it is the practical point: **bigger numbers keep almost everybody; harder rules keep the top decile.**
A ladder that only has rules will be finished by 7% of players. A ladder that only has numbers will be
climbed forever by half of them and remembered by none.

Two more observations from the same data:

- **The first repeat is the cliff, not the tenth.** Hades: 81.9% clear zone 1, 46.9% complete one full
  escape. Slay the Spire: 66.5% unlock ascension, 14.9% reach A10. Roughly **half the population
  leaves at the transition from "finish it once" to "do it again differently."**
- **Optional meta-completion is a top-decile activity.** One rank in every Hades Mirror talent:
  11.7%. Every Slay the Spire achievement: 2.7%.

### 6.4 Cost and failure

| Mechanic | Solves for the player | Costs the designer | Breaks when wrong |
|---|---|---|---|
| **Magnitude ladder** | Always something next; low skill floor | Almost nothing per rung | Nothing memorable happens; the loop is identical at rung 1 and rung 100 |
| **Rule ladder** | Genuine novelty; the run *feels* different | Authoring + combinatorial re-test | 90%+ of players never see the content you spent the most on |
| **Reward gate per rung** | Reason to climb one at a time (Hades) | Reward table per rung | Skipping becomes optimal and the ladder is speedrun |

---

## 7. Endgame structures that are not a number

| Structure | Shipped example | Does it substitute for a bigger number? |
|---|---|---|
| **Infinite floors** | RoR2 Simulacrum — own coefficient, level cap **9999**, ~213 waves ([wiki](https://riskofrain2.wiki.gg/wiki/Simulacrum)) | **No.** It *is* the bigger number, wearing a floor counter |
| **Capped "endless" ladder** | D3 Greater Rift **caps at 150**; D4 Pit **caps at 200** | **No** — but note both "endless" ladders are finite, and the cap is where the leaderboard lives |
| **Seasonal reset** | PoE temporary challenge leagues; characters migrate to Standard on completion ([wikipedia](https://en.wikipedia.org/wiki/Path_of_Exile)) | **Yes.** Resets the *comparison set*, not the numbers |
| **Leaderboards** | D3 GR leaderboards | **Yes.** Converts an absolute ladder into a relative one — the goal becomes other players, which is self-balancing and free |
| **Collection completion** | StS "Eternal One" 2.7%; Cookie Clicker achievement chain | **Partially.** Bounded, authored, and only the top few percent finish it |
| **Named tiers / mastery** | Cookie Clicker's *"Immortal bakery"*; D4's Torment I–XII | **Yes**, and cheaply — a name over a magnitude is what makes band-3 numbers legible (§2.4) |
| **Permanent meta-power** | Hades Mirror of Night; D4 Paragon (**capped at 300/342**); D3 Paragon (**uncapped**) | **No.** It is a number; the design choice is only whether it is capped |
| **Content vaulting** | Destiny 2's Destiny Content Vault, Nov 2020, game had reached ~115 GB ([wikipedia](https://en.wikipedia.org/wiki/Destiny_2)) | **No** — and see §8 |

**⭐ The two that genuinely substitute for a bigger number are the seasonal reset and the
leaderboard, and they work for the same reason: both replace an absolute target with a relative one.**
A leaderboard is the only endgame structure listed here whose difficulty tunes itself, at zero
authoring cost per unit of content, forever. A season is the only one that makes old content new
without touching a single number.

**INFERENCE.** Everything else in the table is a bigger number with better packaging — which is not a
criticism. Packaging is most of what §2.4 says the player is actually reading.

---

## 8. The documented disasters

Numbered continuing the theme of [05-failure-modes.md](../game-design/05-failure-modes.md), which
holds thirteen more. **Not repeated here:** the Diablo II immunity table, Pokémon Gen I's Ghost cell,
Larian's armour gate, SC2 tag leakage, AoE II's 38 armour classes, C&C's `Verses=` parser, and FEH's
1,410-hero grid collapse. Read that file for those.

### 8.1 ⛔ Four squishes is not four fixes — it is a subscription

**World of Warcraft**, §2.2. Patch 6.0.2 (2014), 8.0.1 (2018), 9.0.1 (2020), 12.0.0 (Midnight) — each
one a game-wide re-derivation of every stat, item level and, by 9.0.1, every character level.

**The cause is the compounding itself.** Each expansion multiplied gear power, so the ceiling was
re-approached on a fixed schedule. **A squish resets the position but not the slope**, so the next one
is always already scheduled. The measured cost is that the game has now squished character levels too
— 120 → 50 — because the number of levels had itself become an inflation axis.

### 8.2 ⛔ "Everything below tier N is worthless", shipped literally

**Destiny 2 weapon sunsetting.** Every non-exotic item received *"a maximum infusion Power level,
generally representing the power level four seasons beyond the season that the piece of gear was
introduced in as a means to 'sunset' these gear items"*
([en.wikipedia.org/wiki/Destiny_2](https://en.wikipedia.org/wiki/Destiny_2)).

Bungie reversed it, and the reversal is the interesting part:

> *"Sunsetting has since been revised. Any gear that has been sunset will continue to be as such, but
> anything not sunset will continue to be not sunset."*

**The reversal could not undo the deletion.** Items already sunset stayed dead. **A power ceiling
applied retroactively to owned content is not reversible — the trust is spent whether or not the
policy survives.**

The stated motivation was real and is worth separating from the outcome: the game had reached ~115 GB
and *"had become too large and unmanageable"*, which is why the Destiny Content Vault existed
alongside it. **The disaster was not the goal; it was choosing the player's inventory as the place to
pay for it.**

### 8.3 ⛔ Steep scaling plus a market equals a game that sells you the answer

**Diablo III at launch.** The real-money and gold auction houses were announced for removal
2013-09-17 and shut 2014-03-18. Jay Wilson, in March 2013, said the auction houses *"really hurt"* the
game and *"I think we would turn it off if we could"*, adding the fix was *"not as easy as that"* —
in 2022 he revealed the delay was legal, because *"the feature was being advertised on the retail
packaging of the game"* ([en.wikipedia.org/wiki/Diablo_III](https://en.wikipedia.org/wiki/Diablo_III)).

Reaper of Souls replaced Normal/Nightmare/Hell/Inferno with eight scaling tiers, made monster level
follow player level, and shipped Loot 2.0 — *"items would now drop in decreased quantity but increased
quality"* — with equipment becoming account-bound
([en.wikipedia.org/wiki/Diablo_III:_Reaper_of_Souls](https://en.wikipedia.org/wiki/Diablo_III:_Reaper_of_Souls)).

**The mechanism: when the difficulty curve is steeper than the drop curve, the gap is a market.** The
fix was not a difficulty change alone — it was binding items to the account so the gap could only be
closed by playing.

### 8.4 ⛔ Independent multiplier families make every build the same build

**Diablo IV's damage buckets** — see §5.4 and
[02-modifier-stacking.md](../arpg-effects/02-modifier-stacking.md). Multiple near-independent
multiplicative families meant a build's power was the product of how many families it had one of, so
the optimal build was "one of each" regardless of theme.

**This is the same shape as the Larian armour gate in failure-modes §3** — *"if a mechanic's rule is
'nothing interesting happens until X', the optimal play is always 'make X happen first'"* — with the
gate replaced by a product. **A product over mandatory families is a gate with more steps.**

### 8.5 ⛔ Corruption without a curve split

**Last Epoch.** The community thread a developer replied in is titled *"how high corruption removes
the desire to play"*, and the acknowledged behaviour is that corruption scales infinitely and *becomes
unbeatable* ([forum.lastepoch.com](https://forum.lastepoch.com/t/how-high-corruption-removes-the-desire-to-play/47806)).

**INFERENCE, and it is the through-line of §1:** an endless ladder that scales lethality at the same
rate as durability converts, at some depth, into a one-shot ladder. Past that point the only variable
left is whether you kill it before it touches you, so build diversity collapses to whatever has the
highest burst. D3 and D4 both avoided this by bracketing damage growth down while leaving HP growth
alone. Last Epoch is the natural experiment for what happens if you do not.

### 8.6 ⛔ Power creep as a content programme

Cross-reference, not re-derived: [03-roster-scale.md §6](../game-design/03-roster-scale.md) records
that HSR, Genshin and ZZZ all independently converged on **retroactive kit rewrites** rather than
restraint, and that FEH's BST ceiling went from ~147–169 at launch to **216** with **129 units in a
single grid cell**. The relevant fact for *this* file is the shape of the industry's answer:
**nobody's answer was to slow the curve. Everybody's answer was to re-issue the old content at the new
curve.** For a generated roster, that is a pipeline re-run — which is cheap here and was not cheap for
them.

---

## What I could not find

**The session's web-search budget (200 queries) was exhausted partway through; the later gaps below
are partly a consequence of that and are worth one more pass, not a conclusion that the data does not
exist.** Items marked ⛔ were actively looked for and blocked or absent.

1. **⛔ Slay the Spire's exact A2/A3/A4 and A7/A8/A9 percentages.** The wiki gives exact numbers for
   every economy rung (75%, 10%, half, 25%, −5/−4 HP, 10%) and only qualitative words — *"deadlier"*,
   *"tougher"* — for the six stat rungs. The values exist in the game's code; no public table was
   reachable. **This is itself suggestive: the developer published the economy numbers and not the
   stat numbers.**
2. **⛔ Last Epoch's official corruption scaling formula.** Only a player estimate (*"<8% more health
   and damage per corruption"*) inside a thread a developer replied to, and the developer described
   the mechanism without giving numbers. No wiki, patch note or tooltip with a per-point rate.
3. **⛔ Diablo III's Greater Rift table straight from game data.** The 1.17 / 1.13185 / 1.07177 /
   1.02337 values are consistent across every source that carries them, but every route to the
   original datamined table was blocked — purediablo (403), diablowiki.net (403), diablobytes (403),
   d3andre (TLS mismatch), Diablo Fandom (402). Maxroll is treated as second-tier here.
4. **⛔ Path of Exile's monster-level page and the rarity life multipliers.** poewiki.net is behind an
   Anubis challenge; the Fandom mirror returns 402. The per-level base table in §1.5 came from RePoE's
   export of the game's own data and is first-tier, but **the magic/rare/unique multipliers on top of
   it were not reachable**, which is why this file's ×1,900.8 and roster-scale's ×2,989 differ.
5. **⛔ Path of Exile's resistance cap and overcap mechanics with the exact formula.** Same access
   block. Widely known values were deliberately not written down here without a source.
6. **⛔ Dota 2's evasion stacking formula.** Liquipedia 403, Fandom 402, and search budget was gone.
   The multiplicative form is well known but is not sourced here, so it is not asserted.
7. **⛔ Kittens Game, NGU Idle and Melvor Idle prestige/mastery formulas.** All three were requested.
   Kittens' official wiki returned 404 on every guessed path, NGU's wiki is Fandom (402), Melvor's
   wiki returned 403/401 on both hosts. **The idle-game section therefore rests on Antimatter
   Dimensions, Realm Grinder, Cookie Clicker, AdVenture Capitalist, Egg Inc. and Clicker Heroes**, all
   of which are sourced.
8. **⛔ Vampire Survivors' exact Hyper/Inverse/Endless multipliers.** The wiki host returned 401. Only
   the Steam achievement percentages in §6 are sourced for that game.
9. **⛔ Dead Cells boss-cell achievement percentages.** Steam's page for the game has 121 achievements
   and none are named for BC tiers, so there is no population-depth row for Dead Cells in §6.
10. **⛔ Exact WoW squish ratios per pass.** The 2014 pass is quoted as "~4% stats / ~8% health" on one
    page and "about 1/10" in a contemporaneous summary; the conventions differ and I could not
    reconcile them. **No ratios at all were reachable for the 8.0.1, 9.0.1 or 12.0.0 passes.**
11. **⛔ The original Blizzard "Engineering Diablo III's Damage Numbers" post.** The first-party URL
    301s to the site root and archive.org is not reachable from this environment. The quotes in §2.3
    are second-hand from two independent outlets that agree.
12. **⛔ Any published retention or session-length data for a difficulty ladder.** Kongregate's
    *Quest for Progress* post is 404 and the GameAnalytics idle-games study was unreachable. **§6 is
    built entirely from Steam global achievement percentages**, which measure "ever did this", not
    time spent, and are of *owners* rather than *players*. Treat them as a floor.
13. **⛔ Any first-party statement of a target for how deep a ladder a population should reach.** This
    matches the existing negative finding in
    [06-unsourced.md](../game-design/06-unsourced.md) — studios publish mechanics, never targets.

---

## Hooks for this project

**Non-normative, un-vetted, and explicitly not a design.** These are the places where the material
above touches something this repo already has. Each one is a question, not a proposal.

- The repo's `long`-everywhere rule and the §2 register agree. **Antimatter Dimensions'
  representation-limit-as-content trick is the part with no equivalent here**: the ceiling is a real
  event in that game rather than a bug class. Worth knowing that the option exists.
- §1's universal finding — **durability grows faster than lethality, in every shipped endless ladder**
  — is a statement about the *ratio between two curves*, and this repo currently has one curve.
  Whether `P(Θ)` should have a lethality sibling with a lower slope is a question the power SSOT
  §10 inventory would have to answer, not this file.
- The hyperbolic `X/(X+K)` result in §5.1 — **percentage diminishes forever, effective HP stays
  exactly linear, input needs no cap** — is the closest thing found to a formula that satisfies the
  no-hard-ceilings rule and the diminishing-returns instinct at the same time. `K` is the only knob
  and it means "how much of this stat doubles your effective HP."
- §4.4's split (rules where the population is, multipliers past the end) maps onto the distinction
  between an authored wave ladder and a generated one. Diablo IV ships both at once: 4 named tiers
  over a 200-tier multiplier.
- §4's strongest single observation — **rule ladders attack the player's economy, not the enemy's
  statline** — lands on this project's actor resources (hp/stamina/hunger/spirit/qi) rather than on
  any enemy table. That is five separate economies to take from, which is more axes than Slay the
  Spire spends across twenty rungs.
- §6's numbers are a sanity check on effort allocation: **~7% of a population reaches rung 20 of a
  rule ladder, and ~43% reaches 1e18 on a magnitude ladder.** If a feature is authored per-rung, it is
  authored for the top decile.
- §7's finding that **leaderboards and seasonal resets are the only two endgame structures that tune
  themselves** is relevant to anything here that would otherwise need a hand-authored ceiling.
- §5.4 and §8.4 restate, from a different genre, the caution already recorded in
  [02-modifier-stacking.md](../arpg-effects/02-modifier-stacking.md) about many independent `More`
  families. The repo's existing "cap named mult buckets to a short closed list" guidance survives this
  research unchanged.
