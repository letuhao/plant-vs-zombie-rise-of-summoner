# How games price an action — cost, cooldown, tempo and opportunity models, with real numbers

**Research pass, 2026-09-02.** Scope: the cost vocabularies and what each one is for; published and
datamined per-rank cost curves with the power-vs-cost growth computed from them; cooldown design and
the ultimate-vs-basic ratio; action economy in turn-based games with no resource pool; wind-up and
cast time as a price; opportunity cost; and the documented cases where cost scaling was tuned wrong.

**Method.** [docs/research/game-design/06-unsourced.md](../game-design/06-unsourced.md) was read before
the first search, and the searches it records as dead were not re-run. Its central negative finding —
that nobody publishes quantified balance targets — held here too, and is restated with evidence in
**What I could not find**. Sources are marked **[1st]** first-party (developer statement, official
patch note, official API), **[data]** machine-readable game data or community datamine, **[2nd]**
community wiki, **[3rd]** aggregator. Every self-tallied number is marked **(computed)**. Claims are
marked **FACT** or **INFERENCE**.

**Access note.** The `WebSearch` budget for this session was exhausted at 200 calls partway through.
Most of this file is therefore built from **machine-readable game data pulled directly** — Riot's Data
Dragon and CommunityDragon, Meraki Analytics, Valve's own hero scripts, OpenDota constants, RePoE, the
FFXIV `Action` sheet, Scryfall bulk data, HearthstoneJSON, the Guild Wars 2 official API, and the
Dustloop and Wavu frame-data Cargo APIs — which is a stronger evidence base than search prose anyway.
Over 27,000 shipped actions and cards across thirteen games were parsed for this pass. Full access notes,
including which blocked sites the `r.jina.ai` reader proxy does and does not get past, are in
**What I could not find**.

---

## The finding in one paragraph

**Almost no shipped game charges an escalation tax on a rank ladder, and the one family that does is
the card games — where the tax is not geometric but linear with an entry fee.** Measured across nine
shipped ladders in six games (computed), the *rank* step is close to free everywhere: ranking up a League of Legends basic
ability multiplies its damage by ×3.00 while multiplying its mana cost by ×1.25 and *dividing* its
cooldown by 1.33; a Dota basic gets ×2.80 damage for ×1.38 mana; a Path of Exile gem gets ×40 damage
for ×2.6 mana. In 364 League basic abilities, cost outruns power in exactly **one**. What games
actually charge for is the *tier* step, and they charge it in **cooldown, not resource**: the median
League ultimate costs ×1.67 the mana of the median basic but sits on ×**10.00** the cooldown, and
delivers only 0.19× as much base damage per second of downtime. The single closest analogue to this
project's ×1.40 escalation tax is Magic: the Gathering, where 16,960 creatures give a cost-vs-stats tax
of ×1.333 from mana value 1 to 8 — a per-step tax of **1.0420**, against this project's **1.0432**,
the same number to within 0.1%. But the shape underneath differs and that difference is the finding:
Magic's vanilla creatures fit `P+T = 1.602 × MV + 0.625` and Hearthstone's minions fit
`A+H = 1.466 × cost + 1.61` — **linear with a positive intercept**, so the apparent tax is an artifact
of the entry fee and it *converges* (Hearthstone's value-per-mana flattens at ~1.68 stats from cost 6
onward), whereas a geometric quotient pair decays without limit. **No studio publishes a power-vs-cost
ratio or a cooldown-to-power formula**; the industry's one recoverable power formula is WoW's original
spell-power coefficient, which prices **wind-up** — `clamp(cast, 1.5s, 7s) / 3.5s`, halved for area and
shaved 5% per extra rider effect — and even that was reverse-engineered rather than published. And in
the genre that publishes complete timing data, the intuitive story about wind-up is simply wrong: across
1,600 Guilty Gear Strive moves, damage correlates with **recovery** (r = 0.544) and with
**punishability** (r = −0.374), barely with startup (r = 0.301), and not at all for supers
(r = −0.032) — which are on average *faster* off the ground than an ordinary normal (10.4 frames vs
14.6) while dealing 2.5× the damage. The commitment is on the back end. **The price of a strong action,
almost everywhere, is the free turn it hands the opponent.**

---

## 0. The one metric this file uses

Comparing "cost curves" across games is impossible unless the ladders are the same length. They are
not: League ranks an ability 5 times, Dota 4, Magic prices across 8 mana values, a Path of Exile gem
runs 20 levels, this project runs 10 rungs. Two normalisations make them comparable.

```
escalation tax   = (cost growth over the ladder) / (power growth over the ladder)
                   > 1  cost outruns power   — later rungs are worse value
                   = 1  cost and power grow together
                   < 1  power outruns cost   — later rungs are better value

tax per step     = tax ^ (1 / number of steps)          — length-independent

cost elasticity  = ln(power growth) / ln(cost growth)   — "doubling the cost buys 2^E power"
of power           E > 1  power outruns cost
                   E < 1  cost outruns power
```

This project's authored ladder, evaluated exactly (computed):

| span | steps | power `qPower` | cost `qCost` | cooldown `qCd` | tax | tax/step | elasticity |
|---|---|---|---|---|---|---|---|
| rungs 1→10 | 9 | ×12.407 | ×18.151 | ×3.518 | **1.463** | 1.0432 | 0.87 |
| rungs 2→10 | 8 | ×9.379 | ×13.153 | ×3.059 | **1.402** | 1.0432 | 0.87 |

Power delivered per unit of cost, normalised to rung 1: `1.000, 0.959, 0.919, 0.881, 0.844, 0.809,
0.776, 0.744, 0.713, 0.684`. A rung-10 action returns **68.4%** of rung 1's power-per-cost, and 19.4%
once the cooldown multiplier is folded in (computed).

---

## 1. The cost vocabularies

The load-bearing split is **build-and-spend vs pool-and-drain**, and it is not a flavour difference —
the two answer opposite questions.

- **Pool-and-drain** (mana, MP, energy-with-regen) asks *"how long can you keep going?"* The pool is
  full at the start of a fight and the fight is a depletion curve. Its natural failure is the
  **20-minute mana bar**: once regeneration or pool size outgrows spend rate, the resource stops
  existing as a constraint and the action is priced only by its cooldown.
- **Build-and-spend** (rage, fury, combo points, ultimate charge) asks *"have you earned it yet?"* The
  meter starts **empty**, so the strong action is unavailable at the moment of highest tension and must
  be paid for with prior participation. Its natural failure is the opposite: a generator that outpaces
  the spender turns the meter into a formality.

| Vocabulary | Shipped examples | The problem it solves that the others do not | What it costs the designer | Breaks when |
|---|---|---|---|---|
| **Mana / MP** (pool, regen, grows with level and gear) | League of Legends — **144 of 172** champions **[data]** (computed); FFXIV (234 of 936 player actions) **[data]**; Path of Exile; Dota 2; WoW post-3.0.2 | Prices *sustained* output over a long fight, and lets gear buy endurance | The pool grows and the cost does not, so the cost silently deflates | Regen ≥ spend. Then the cost is decoration |
| **Energy** (small **fixed** pool, fast regen, never grows) | League — 6 champions: Akali, Ambessa, Kennen, Lee Sin, Shen, Zed, pool 200 (400 for Shen), `mpperlevel = 0` **[data]**; WoW rogues | Cost that **never deflates** — the same ability is the same fraction of the bar at minute 40 as at minute 1 | Caps how much a build can ever amplify the character's throughput | Regen buffs stack; the fixed pool becomes a fixed *rate*, not a fixed *budget* |
| **Rage / fury / heat** (build-and-spend) | League — 14 distinct bars across 172 champions: Fury (Briar, Renekton, Shyvana, Tryndamere), Rage (Gnar, Rek'Sai), Flow (Yasuo, Yone), Blood Well, Courage, Shield, Ferocity, Heat, Grit, Crimson Rush **[data]** (computed); WoW warriors | Makes the strong action *earned in-fight*, so it cannot open a fight | Needs a decay rule, or the meter carries between fights and the "earned" framing collapses | Generation loops. See §7 |
| **Ultimate charge** (build-and-spend, denominated in your own output) | Overwatch — every hero's ultimate costs a number of charge points; charge accrues at **1 point per point of damage dealt to enemy heroes or healing done**, plus a flat **5 points/second** passive **[2nd]** | The price of the strong action is *prior contribution*: you must have already done the work | The generation rate, not the printed cost, becomes the real dial — and it differs per hero | A hero whose normal rotation generates fast enough turns the ultimate into a rotation ability |
| **Cooldown only, no resource** | League — 6 champions with no bar (Dr. Mundo, Garen, Katarina, Riven, Viego, Zac) and **92 abilities tagged "No Cost"** **[data]**; FFXIV — **458 of 936** player actions cost nothing at all **[data]** (computed) | Removes a whole UI element and a whole failure mode; the price is purely *when*, never *whether* | You lose the ability to make an action situationally unaffordable | Cooldown reduction stacks. See §7.2 |
| **Charges with recharge** | FFXIV — **37 of 936** player actions hold 2+ charges; Surpanakha holds 4 at 30s, Bloodletter and Heartbreak Shot 2–3 at 15s **[data]** (computed) | Converts even spacing into *saved-up burst* without shortening the long-run rate | Two numbers to tune instead of one, and a burst window you must survive-test | Charges + cooldown reduction multiply |
| **Initiative** (a pool, and **no cooldowns at all**) | Guild Wars 2 Thief — every weapon skill costs 1–6 initiative from a 12-point regenerating pool and has `recharge: null` **[1st, official API]** | Lets the player repeat the *same* skill as fast as they can pay, which a cooldown forbids outright | You cannot rate-limit any single skill; balance is entirely in the price | One skill's damage-per-point outruns the rest and the rotation becomes one button |
| **HP as a cost** | League — Olaf's *Reckless Swing* costs current health, Briar's abilities cost a % of current health **[data]**; Path of Exile life-cost gems; Blood for Blood in Slay the Spire | Charges against the resource that *is* the loss condition, so the price grows as the fight goes badly | Interacts violently with lifesteal and healing | Sustain outpaces the cost and it becomes free |
| **Cards in hand + energy** | Slay the Spire — 3 energy per turn, 5-card draw | Two independent scarcities: you must both *have* the card and *afford* it | Variance; a hand can be unplayable through no error | Draw engines remove the hand limit; energy alone is a weak brake |
| **Action points / action count** | D&D 2024 (one Action + at most one Bonus Action + one Reaction per turn) **[1st]**; XCOM (2); Pathfinder 2e (3) | Prices actions against each other with no economy to tune at all | Granularity: with 2–3 slots you cannot express "slightly more expensive" | A free-action loophole; anything that grants extra actions is worth more than anything that adds power |
| **Tempo / turn-order cost** | Shin Megami Tensei Press Turn; Final Fantasy X CTB | The strong action costs *the enemy's opportunity*, which no resource bar can express | Very hard to read; the player must simulate turn order to price a decision | See §4 |

**Two computed facts that sharpen the pool-and-drain critique.**

**FACT (computed).** In League, the median mana ability costs **16.7%** of its own champion's mana pool
at level 1 rank 1, and **6.8%** at level 18 max rank — a ×0.41 change — because the median mana pool
grows from 340 to 1,057 from levels alone, before any mana item **[data]**. The cost number rises;
the *price* falls by more than half.

**FACT (computed).** The only League champions whose resource does **not** deflate — the six energy
users, whose pool is fixed at 200 and never grows — are also the ones whose ability costs *fall* with
rank: Akali's Q goes 110/100/90/80/70, Kennen's 60/55/50/45/40, Zed's 75/70/65/60/55 **[data]**.

**INFERENCE.** Those two facts together say that designers treat the *fraction of the bar* as the real
price, not the printed number. A system whose pool grows must inflate printed costs merely to hold the
price constant; a system with a fixed pool can leave costs flat, or cut them, and the price still
holds. Which pool model this project uses therefore determines how much of the ×1.40 tax is real price
and how much is compensating for pool growth.

---

## 2. Published cost curves — what the numbers actually say

### 2.1 The master comparison

All figures computed from the datasets listed in **Data sources**. Power is base damage (or
power+toughness / attack+health for the card games); cost is the printed resource cost.

| System | ladder | power × | cost × | tax | **tax/step** | elasticity |
|---|---|---|---|---|---|---|
| **This project** | rungs 1→10 | 12.41 | 18.15 | **1.463** | **1.0432** | 0.87 |
| **This project** | rungs 2→10 | 9.38 | 13.15 | **1.402** | **1.0432** | 0.87 |
| Magic: the Gathering, all creatures (n=16,960) | MV 1→8 | 6.00 | 8.00 | 1.333 | **1.0420** | 0.86 |
| Magic, vanilla creatures only (n=526) | MV 1→7 | 4.21 | 7.00 | 1.663 | 1.0885 | 0.74 |
| Hearthstone, collectible minions (n=4,708) | cost 1→8 | 4.39 | 8.00 | 1.824 | 1.0898 | 0.71 |
| Dota 2 ultimates (n=29) | L1→L3 | 2.00 | 1.89 | 0.945 | 0.9721 | 1.09 |
| WoW vanilla *Fireball* | R1→R11 | 31.56 | 13.17 | 0.417 | 0.9163 | 1.34 |
| Dota 2 basic abilities (n=136) | L1→L4 | 2.80 | 1.38 | 0.493 | 0.7899 | 3.20 |
| Path of Exile damage gems (n=78) | L1→L20 | 40.48 | 2.59 | 0.064 | 0.8653 | 3.89 |
| League basic abilities (n=364) | R1→R5 | 3.00 | 1.25 | 0.417 | 0.8034 | 4.92 |
| League ultimates (n=113) | R1→R3 | 2.33 | 1.00 | 0.429 | 0.6547 | ∞ (cost flat) |

**FACT (computed).** Of the ten rows, **only this project's two rows and the three card-game rows have
a tax above 1.0.** Every live-service action game measured has a tax below 1.0 — ranking an ability up
makes it *better value*, not worse.

**FACT (computed).** In 364 League basic abilities with a resource cost, cost outruns power in exactly
**one** (Ekko's *Timewinder*, ×1.80 cost for ×1.75 damage). In 115 ultimates, also exactly one
(Kassadin's *Riftwalk*, and that is a within-combo stacking penalty, not a rank cost — see §7.5). In
78 Path of Exile damage gems, **zero** **[data]**.

**FACT (computed).** This project's per-step tax of **1.0432** and Magic's per-step tax of **1.0420**
agree to within 0.1%. That is the single closest published analogue found.

### 2.2 The shape underneath is different, and that is the real finding

Magic and Hearthstone are not running a geometric quotient pair. Fitting their printed stats against
printed cost (computed):

```
MtG vanilla creatures (no rules text at all, n=526):   P+T = 1.602 × MV   + 0.625
MtG all creatures with printed P/T   (n=16,960):       (median P+T/MV falls 2.00 → 1.50 across MV1→8)
Hearthstone all collectible minions  (n=4,708):        A+H = 1.466 × cost + 1.61
Hearthstone vanilla minions (no text, no mechanics):   A+H = 1.822 × cost + 1.73
```

Each extra mana buys a **constant** 1.47–1.82 stats. The apparent escalation tax is entirely the
positive intercept — the first mana buys the slope *plus* the entry fee, every mana after buys only the
slope. Consequently the tax **converges**:

| model | value-per-cost at cost 1 | at 5 | at 10 | at 20 | at 30 | limit |
|---|---|---|---|---|---|---|
| Hearthstone minions | 1.000 | 0.581 | 0.529 | 0.503 | 0.494 | 0.494 |
| MtG vanilla creatures | 1.000 | 0.775 | 0.747 | 0.733 | 0.729 | 0.729 |
| Geometric pair (this project) | 1.000 | 0.844 | **0.684** | 0.448 | 0.294 | **0** |

**FACT (computed).** Over rungs 1–10 the three models sit in the same band (0.68 / 0.53 / 0.75). Past
rung 10 they separate: the card-game shape flattens onto a floor, the geometric shape keeps decaying
toward zero. Over the shipped 10 rungs, this project's curve is in fact well described by a *linear*
fit too — `power = 0.666 × cost + 0.56`, r² = 0.998 (computed) — so the two shapes are empirically
indistinguishable inside the shipped range and diverge only outside it.

**INFERENCE.** A geometric tax and a linear-with-intercept tax are the same design over a short ladder
and completely different designs over a long one. Which one a project has chosen only becomes visible
when the ladder is extended — which, for a project whose stated principle is endless grind, is the
case that matters.

### 2.3 WoW vanilla *Fireball*: the cleanest published rank ladder in an MMO

Every rank of vanilla Fireball, from the live Classic spell entries **[2nd — Wowhead Classic, but these
are the game's own spell records]**. Damage is the mean of the printed range plus the printed
damage-over-time. All ratio columns computed.

| Rank | Level | Mana | Damage (+DoT) | dmg/mana | Cast | power × | cost × | tax |
|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 30 | 22.5 | 0.75 | 1.5s | 1.00 | 1.00 | 1.000 |
| 2 | 6 | 45 | 44.5 | 0.99 | 2.0s | 1.98 | 1.50 | 0.758 |
| 3 | 12 | 65 | 73.0 | 1.12 | 2.5s | 3.24 | 2.17 | 0.668 |
| 4 | 18 | 95 | 117.5 | 1.24 | 3.0s | 5.22 | 3.17 | 0.606 |
| 5 | 24 | 140 | 190.5 | 1.36 | 3.5s | 8.47 | 4.67 | 0.551 |
| 6 | 30 | 185 | 268.5 | 1.45 | 3.5s | 11.93 | 6.17 | 0.517 |
| 7 | 36 | 220 | 336.5 | 1.53 | 3.5s | 14.96 | 7.33 | 0.490 |
| 8 | 42 | 260 | 416.5 | 1.60 | 3.5s | 18.51 | 8.67 | 0.468 |
| 9 | 48 | 305 | 513.0 | 1.68 | 3.5s | 22.80 | 10.17 | 0.446 |
| 10 | 54 | 350 | 615.5 | 1.76 | 3.5s | 27.36 | 11.67 | 0.426 |
| 11 | 60 | 395 | 710.0 | 1.80 | 3.5s | 31.56 | 13.17 | **0.417** |
| 12 | 60 | 410 | 754.0 | 1.84 | 3.5s | 33.51 | 13.67 | 0.408 |

Source pages: [rank 1](https://www.wowhead.com/classic/spell=133/fireball),
[rank 11](https://www.wowhead.com/classic/spell=10151/fireball) and the ten between them.

**FACT (computed).** Fireball's mana grows ×13.17 across 11 ranks. This project's cost grows ×13.15
across rungs 2–10. **The two cost curves are effectively identical.** The difference is entirely on the
power side: Fireball's damage grows ×31.6 where this project's power grows ×9.38.

**FACT (computed).** Fireball's cast time is also a cost, and it rises from 1.5s to 3.5s and then
**stops** at rank 5 — ranks 5 through 12 all cast in 3.5 seconds while damage nearly quadruples.
Damage per second of cast rises from 15.0 to 202.9, a ×13.5 improvement (computed).

**INFERENCE.** WoW paid for the early ranks with cast time and then froze that price, letting the last
eight ranks be a pure power increase against a sub-proportional mana increase. Cast time was used as a
*ramp*, not as a ladder-long tax.

**Design note on why WoW eventually abandoned printed costs.** As of patch 3.0.2 all WoW spell costs
became a percentage of base mana rather than a fixed number — a level-70 priest's Mind Flay rank 7 cost
9% of base mana, displayed as 235 of 2,620 **[2nd — warcraft.wiki.gg,
[Base mana](https://warcraft.wiki.gg/wiki/Base_mana)]**. **INFERENCE:** a proportional cost is
self-maintaining across levels and expansions, which removes the need for a per-rank cost table
entirely. That is the same problem a per-rung cost quotient is solving, solved without a quotient.

### 2.4 Dota 2 — the one live game whose ultimates charge proportionally

Computed over the OpenDota constants dump: 127 heroes, 172 abilities that publish both a per-level
damage array and a per-level mana cost **[data]**.

| | n | power × | mana × | cooldown × | tax | cost outruns power |
|---|---|---|---|---|---|---|
| Basic abilities (L1→L4) | 136 | ×2.80 | ×1.38 | ×0.67 | 0.493 | 4 of 136 |
| Ultimates (L1→L3) | 29 | ×2.00 | ×1.89 | ×0.74 | **0.945** | 13 of 29 |

Worked examples **[data]**:

```
Lina, Dragon Slave (basic):   damage  65 / 125 / 185 / 245   (×3.77)
                              mana    90 / 100 / 110 / 120   (×1.33)
                              cd      11 /  10 /   9 /   8   (×0.73)   → tax 0.35

Lina, Laguna Blade (ultimate): damage 400 / 580 / 760        (×1.90)
                               mana   150 / 300 / 450        (×3.00)
                               cd      70 /  60 /  50        (×0.71)   → tax 1.58
```

**FACT (computed).** Dota ultimates are the closest shipped analogue to an escalation tax: tax 0.945,
per-step 0.9721, and nearly half of them do charge more than they deliver. Even so the *cooldown falls*
at every rank, which returns most of the price.

**INFERENCE.** Dota's ultimates are the only measured case where cost genuinely tracks power. That the
mechanism appears on ultimates specifically — the tier the player has fewest of, uses least often, and
whose mana cost is most likely to be the binding constraint on a support hero — suggests the escalation
tax is a tool designers reach for on *big infrequent actions*, not on the rotation.

### 2.5 Path of Exile — the extreme case

78 damage gems with both a per-level mana cost and a per-level base damage, levels 1→20 **[data]**:
power ×40.48 median, mana ×2.59 median, tax **0.064**. Zero gems where cost outruns power.

```
Fireball gem, level 1 → 20:   mana 6 → 25 (×4.2)      min base damage 9 → 1,640 (×182)
                              fit: power ≈ cost^3.636  (r² = 0.993)
                              median mana per point of base damage: 0.595 → 0.037 (16× cheaper)
```

**INFERENCE.** Path of Exile has effectively abolished mana as a per-cast price for damage skills; the
cost exists so that *mana reservation* and cast-speed builds have something to interact with. The real
price of a level-20 gem is the level requirement (level 100 for the top gem levels) and the socket.

### 2.6 League of Legends — cost is flat and cooldown carries everything

Computed over Data Dragon 15.24.1 (172 champions, 688 abilities) and Meraki Analytics' per-rank tables
**[data]**.

| | n | power × | cost × | cooldown × | tax |
|---|---|---|---|---|---|
| Basic (Q/W/E), R1→R5 | 364 costed | ×3.00 | ×1.25 | ×0.75 | 0.417 |
| Ultimate (R), R1→R3 | 113 costed | ×2.33 | ×1.00 | ×0.70 | 0.429 |

**FACT (computed).** Of 364 costed basic abilities, **167 have a completely flat cost across all five
ranks** and 12 get *cheaper*. Of 115 costed ultimates, **108 are flat** — the median League ultimate
costs exactly the same mana at rank 3 as at rank 1 while dealing ×2.33 the damage.

Worked examples **[data]**:

```
Annie  Q Disintegrate   80/120/160/200/240 dmg   60/65/70/75/80 mana   4s cd flat
Ashe   R Crystal Arrow  200/400/600 dmg          100 mana flat         100/80/60s cd
Lux    R Final Spark    300/400/500 dmg          100 mana flat         60/50/40s cd
Ryze   Q Overload       75/95/115/135/155 dmg    40/38/36/34/32 mana   5s cd flat
```

**FACT (computed).** 14 League abilities get *cheaper* as they rank up, led by Teemo's ultimate
(75/55/35, ×0.47) and Azir's W (40/35/30/25/20, ×0.50) **[data]**.

### 2.7 Slay the Spire — cost buys density, not efficiency

Computed over all **92 fixed-cost attack cards** in the datamined card list
**[data — [`spire-archive`](https://github.com/nkhoit/spire-archive), `data/sts1/cards.json`, parsed
from the game files]**. Damage-per-energy is printed damage × hit count ÷ cost, deliberately ignoring
text riders.

| Energy | n | mean damage per energy |
|---|---|---|
| 1 | 58 | **7.45** |
| 2 | 29 | **6.14** |
| 3 | 5 | **6.33** |

**FACT (computed).** Damage per energy does **not** rise with cost — it slightly falls. The three
Ironclad reference points **[2nd — wiki.gg card pages; note the Ironclad card *list* page misprints
energy costs, listing Bash and Searing Blow as 1 when both are 2, so individual card pages were used]**:

| Card | Energy | Damage | dmg/energy | Rarity |
|---|---|---|---|---|
| [Strike](https://slaythespire.wiki.gg/wiki/Ironclad_Cards) | 1 | 6 | 6.00 | Basic |
| [Bludgeon](https://slaythespire.wiki.gg/wiki/Bludgeon) | 3 | 32 | 10.67 | Rare |
| Hemokinesis | 1 | 15 (lose 2 HP) | **15.00** | Uncommon |

**FACT.** The most energy-efficient ≥1-cost attack in the game, Hemokinesis, costs 1 energy and pays
its real price in **HP**. The pattern holds across the top of the efficiency table: Signature Move
(2 energy, 30 damage, 15.0) is playable only if it is the only Attack in hand; Unload and Rip and Tear
(1 energy, 14 damage, 14.0) carry their own riders **[data]**.

**FACT.** 68 of 360 cards cost **0 energy**, 20 of them Attacks. Grand Finale deals **50 damage to all
enemies for zero energy** and is playable only when the draw pile is empty — a cost paid in
deckbuilding and timing across the entire run, with nothing in the cost field **[data]**.

**INFERENCE.** What 3 energy buys in Slay the Spire is **concentration, not efficiency**. A turn of
three Strikes is 3 energy plus **3 cards** for 18 damage; Bludgeon is 3 energy plus **1 card** for 32.
The premium is 14 damage and two freed hand slots. The escalation price lives on the second scarcity
axis — draw 5, hand cap 10, everything discarded at end of turn — and on rarity, not on energy. A
system that copies "3 energy per turn" without also copying a hand and draw limit will find its
high-cost actions mathematically pointless.

### 2.8 Does anyone publish a power-vs-cost ratio?

**For resource cost and for cooldown: no. FACT.** Across every source reached in this pass — Riot patch
notes and dev posts, Blizzard class blogs, the official Guild Wars 2 API documentation, Square Enix job
guides, wizards.com articles, and Valve's own shipped script files — no studio publishes a stated target
of the form *"power should grow by X for every Y of resource cost"*, and none publishes a
cooldown-to-power formula or ratio. This is the same negative finding recorded in
[`06-unsourced.md`](../game-design/06-unsourced.md) §1 for counter strength, and it holds here for the
same reason: the ratios in §2.1 are **observed from shipped data, not stated design targets**.

Two near-misses are worth naming precisely, because both are real formulas and neither is what the
question asked for:

1. **Riot's Ability Haste definition** (§7.2) is arithmetic and official, but it prices *cooldown
   reduction*, not power.
2. **WoW's original spell-power coefficient is a genuine, exact power formula — and it is keyed to cast
   time, not to resource cost or cooldown.** `coefficient = clamp(cast time, 1.5s, 7.0s) / 3.5s`, then
   ×0.5 for area effects and ×0.95 per additional rider effect. It is implemented verbatim in an
   open-source server core; Blizzard never published it. Full treatment in **§5.3**.

**The honest summary:** exactly one power-costing formula from the whole industry could be recovered, it
prices **wind-up**, and it dates from the 2004 game. Everything else in this file that looks like a
design rule is a measurement.

---

## 3. Cooldown design

### 3.1 Why cooldowns exist alongside a resource — and the designers say why

Riot has stated the division of labour directly. Riot Axes, *Ask Riot: Manaless Champions*, 2 July 2021
**[1st — [leagueoflegends.com](https://www.leagueoflegends.com/en-us/news/dev/ask-riot-manaless-champions/)]**:

> "Mana lets us calibrate the stakes for each spell without needing to create a lot of additional
> rules." … "Cooldowns are a useful tool for this as well, but **can only be pushed so far** — how long
> would Rocket Grab's cooldown need to be if Blitzcrank didn't use mana?"

Blizzard has stated the complementary case for the *global* cooldown. Ion Hazzikostas on the Battle for
Azeroth change **[1st statement, via 2nd — quoted at
[Ask Mr. Robot](https://blog.askmrrobot.com/battle-for-azeroth-gcd-change/); the original Blizzard forum
thread was unreachable]**:

> "With them off the GCD, talenting into such abilities often just becomes a matter of **adding another
> line to a burst macro** without any additional gameplay as a result."
>
> "Major damage amplifiers can be applied simultaneously with an outgoing damage ability … **heavily
> limits counterplay and makes worst-case burst damage more severe**."
>
> "We see WoW as a series of rapid fire decisions."

**INFERENCE.** Between them these name three jobs a resource cost structurally cannot do:

1. **Spacing.** A cooldown guarantees a minimum gap regardless of how rich you are. A pool only
   guarantees a total.
2. **Serialization.** A shared cooldown makes abilities compete for one slot, turning "use both" into
   "choose one." Mana never does this — with enough mana you press everything.
3. **A counterplay window.** A long cooldown creates a *known* period of vulnerability the opponent can
   play around. Resource depletion is hidden state.

**FACT (computed).** FFXIV demonstrates the separation cleanly: of 936 player actions, **458 have no
resource cost at all** and **340 sit on a personal recast longer than the global cooldown** (median 60s,
90th percentile 120s, maximum 420s) **[data]**. Its strongest defensive buttons are free and priced only
in time — Hallowed Ground: no cost, 420s recast; Benediction (a full heal): no cost, 180s recast
**[data]**.

**INFERENCE.** Once a cooldown is long enough to make an action a once-per-fight decision, a resource
cost on top adds nothing except a second way to fail to press it. That is why the biggest buttons in
FFXIV are free — and, conversely, why Dota *stacks* both prices on ultimates (Enigma's Black Hole costs
500 mana **and** 160 seconds), because either alone would need an absurd value.

### 3.2 Global cooldown — and the proof that GCD placement is itself a price

WoW **[2nd — [warcraft.wiki.gg, Global cooldown](https://warcraft.wiki.gg/wiki/Global_cooldown)]**:

| Quantity | Value |
|---|---|
| Base GCD, most abilities | **1.5s** |
| Rogues, feral druids, monks | 1.0s (haste does not affect it) |
| Haste formula | `GCD = 1.5 / (1 + haste)` |
| Haste floor | **0.75s** (1.0s before Legion; haste has affected the GCD since patch 2.4.0) |

**INFERENCE.** The GCD's whole dynamic range is exactly **2×**. That is a deliberately narrow band: it is
a tempo channel, not a power channel.

**FACT.** Patch 8.0.1 moved most DPS and healing cooldowns and movement abilities *onto* the GCD
**[2nd]**, and the change was **partially reverted after backlash** — Heroic Leap, Infernal Strike and
Disengage went back off it, with the stated player grievance that the game *"felt significantly
slower"* **[3rd — [PCGamesN](https://www.pcgamesn.com/world-of-warcraft/world-of-warcraft-battle-for-azeroth-global-cooldown),
[Blizzard Watch](https://blizzardwatch.com/2018/04/17/blizzard-makes-changes-gcd-battle-azeroths-latest-alpha-build/)]**.

**INFERENCE — the cleanest demonstration in this file that tempo is a real price.** Nothing about any
ability's damage, cooldown or resource cost changed. Only whether it consumed 1.5 seconds of tempo. That
alone was a large enough nerf to force a partial revert.

**FFXIV makes the same idea mechanical rather than conventional.** The global cooldown is not a rule
applied to everything — it is **one shared cooldown group (58)** that Spells and Weaponskills belong to
and Abilities do not **[data]**:

| | n | median recast |
|---|---|---|
| Cooldown group 58 (the global) | **428** actions — 251 Spells, 166 Weaponskills, 7 Abilities | **2.5s** (403 of 428 are exactly 2.5s) |
| Category *Ability* (oGCD), outside group 58 | 404 actions | **30.0s** (quartiles 15 / 30 / 90s) |

**FACT (computed).** The oGCD-to-GCD recast ratio is **12×**, and **403 of the 404 oGCD Abilities are
instant** **[data]**.

**INFERENCE — the key structural idea in this section.** FFXIV separates two independent prices:

```
"How often may I do this?"        → the action's own recast
"Does doing it cost me a turn?"   → membership in the shared cooldown group
```

An oGCD with a 30-second recast costs **nothing in tempo** — you fit it inside the 2.5s window you were
already spending. That is why FFXIV rotations are "weave off-globals into the gaps" rather than "choose
between abilities", and it is the exact opposite of the Battle for Azeroth decision, which deliberately
merged the two prices back together.

### 3.3 Charges are two numbers where a cooldown is one

Blizzard's own API documentation states the contract: charges exist for abilities that can be used
*"rapidly, and then slowly accumulate charges over time"*, and the returned `cooldownDuration` is
*"time (in seconds) required to gain a charge"* — a recharge **rate**, not a lockout
**[1st — [WoW API, GetSpellCharges](https://warcraft.wiki.gg/wiki/API_GetSpellCharges)]**.

Real numbers, from live tooltip data **[data]**:

| Ability | Charges | Recharge | Note |
|---|---|---|---|
| Monk **Roll** | 2 | 20s | Blizzard's own documentation example |
| Mage **Ice Floes** | 3 | 20s | **+100ms inter-charge lockout**; also off the GCD |
| Priest **Angelic Feather** | 3 | 20s | |
| Warlock **Conflagrate** | 2 | 12.96s | non-round → haste-scaled |
| Hunter **Barbed Shot** | 2 | 18s | |

League publishes the same structure as `maxAmmo` / `ammoRechargeTime`. **24 of 688 abilities** declare a
non-default ammo count **[data]**, and three of them show the two levers moving independently:

| Ability | Charges by rank | Recharge by rank | **Per-cast lockout** |
|---|---|---|---|
| Teemo **R — Noxious Trap** | 3 / 4 / 5 | 35 / 30 / 25s | **0.25s** |
| Caitlyn **W — Yordle Snap Trap** | 3 / 3 / 4 / 4 / 5 | 26 / 22 / 18 / 14 / 10s | 0.5s |
| Corki **R — Missile Barrage** | 4 at all ranks | 20s at all ranks | **2s** |

**INFERENCE, and this is the design point.** A charge system has **two independent timers** where a
cooldown has one:

1. **The recharge interval** sets long-run frequency.
2. **The inter-cast lockout** sets *how bursty* the stored uses may be spent.

A plain cooldown is forced to use the same number for both, which is exactly why it enforces even
spacing. The evidence that the two are deliberately separate: Corki holds four charges but a 2-second
lockout, so a full stock still takes 8 seconds to fire — stored ammunition without a burst dump. Teemo
holds five with a 0.25-second lockout and can empty the stock in a second, and *both* his recharge and
his charge cap improve as the ultimate ranks up.

**FACT (computed).** FFXIV has **63** player actions with 2 or more charges, up to 4 (Surpanakha 4 at
30s; En Avant 4 at 10s; Fuma Shuriken, Heartbreak Shot, Double Check, Checkmate at 3; Ley Lines 2 at
120s) **[data]**, and among the two-charge entries the cluster is unmistakable: *Onslaught, Intervene,
Corps-a-corps, Thunderclap, Slither, Shadowstride, Trajectory, Displacement, Engagement* are all
**movement abilities** on 30–35s recharges.

**INFERENCE.** Charges are reached for where the *timing* of the action matters far more than its rate —
overwhelmingly, positioning. A player who needs two dashes in three seconds and then none for a minute
is served by charges and punished by a plain cooldown; a damage cooldown wants the opposite, which is
why the damage buttons in the same dataset almost never carry charges.

**INFERENCE.** Note also that WoW's Ice Floes and Shimmer are *both* charged **and** off the global
cooldown. Two separate prices are being removed at once — good evidence that "the price of an action" is
a vector, not a scalar.

**What breaks:** charges multiply with cooldown reduction. Two charges at 40% reduced recharge is not
40% more uses; it is 40% more uses *and* a shorter time to refill the burst window.

### 3.4 Cooldown length vs power — the ultimate/basic ratio

This is the number the question asks for, computed from two games' full ability sets.

| | League of Legends (172 champions) | Dota 2 (127 heroes) |
|---|---|---|
| Median basic cooldown, max rank | **8.0s** (n=527) | **14s** (n=470) |
| Median ultimate cooldown, max rank | **80.0s** (n=172) | **70s** (n=72) |
| **Ratio** | **×10.00** | **×5.00** |
| Median basic cooldown, rank 1 | 12.0s | — |
| Median ultimate cooldown, rank 1 | 120.0s | — |
| **Ratio at rank 1** | **×10.00** | — |
| Median basic resource cost, max rank | 60 mana | 100 mana |
| Median ultimate resource cost, max rank | 100 mana | 225 mana |
| **Ratio** | **×1.67** | **×2.25** |

All computed **[data]**. Longest League ultimate cooldowns at rank 1: Karthus *Requiem* and Shen
*Stand United* at **200s**, Galio and Pantheon at 180s **[data]**.

**Dota's ratio re-measured against Valve's own classification.** The table above identifies Dota
ultimates by position in the hero's ability list. Using Valve's `DOTA_ABILITY_TYPE_ULTIMATE` flag from
the shipped hero KV files instead **[data —
[Valve hero scripts, mirrored](https://github.com/dotabuff/d2vpkr/tree/master/dota/scripts/npc/heroes)]**:
ultimates median **72s** (n=54), non-ultimates median **18s** (n=303) — **×4.0**. Taken per hero
(longest-cooldown ability against the mean of the rest, n=127) the ratio is median **4.78×**, mean
5.40×. **Treat Dota's ultimate/basic cooldown ratio as ×4–5, and League's as ×10.**

Concrete Dota pairs at max rank, mana in brackets **[data]**:

| Hero | Ultimate | Basic | Ratio |
|---|---|---|---|
| Zeus | Thundergod's Wrath **130s** | Arc Lightning **1.6s** | **×81** |
| Faceless Void | Chronosphere **135s** | Time Walk **6s** | ×22 |
| Enigma | Black Hole **160s** [500 mana] | Malefice **14s** [130 mana] | ×11 |
| Crystal Maiden | Freezing Field **90s** | Crystal Nova **8s** | ×11 |
| Lion | Finger of Death **30s** [600 mana] | Impale **11s** [150 mana] | ×2.7 |

**INFERENCE.** Note that in Dota mana rises *with* cooldown rather than trading against it — Black Hole
costs 500 mana **and** 160 seconds; Malefice costs 130 mana and 14 seconds. The two prices are
**stacked** on ultimates, not substituted. That is the same posture as Riot's "cooldowns can only be
pushed so far": the ultimate pays both because either alone would need an absurd value.

**FACT (computed).** Across the tier step from basic to ultimate in League, median base damage rises
×2.06, median mana cost rises ×1.67, and median cooldown rises **×10.00**. Expressed as taxes: the
resource tax is **0.81** (an ultimate is *cheaper* per point of damage) and the cooldown tax is
**4.86**. Base damage per second of cooldown falls from 21.7 for basics to 4.2 for ultimates — an
ultimate is **0.19×** as efficient per second of downtime.

**FACT (computed).** Within a rank ladder, cooldown length and damage are essentially uncorrelated: a
log-log fit of max-rank base damage against max-rank cooldown over 377 League basic abilities gives
**r² = 0.010**; over 116 ultimates, r² = 0.108.

### 3.5 Overwatch — the tier price paid in your own output

Overwatch prices ultimates in **charge points**, and the exchange rate is published: charge accrues at
**one point per point of damage dealt to enemy heroes or healing done to yourself or allies**, plus a
flat **5 points per second** passively once the spawn doors open
**[2nd — [Overwatch wiki, Ultimate ability](https://overwatch.fandom.com/wiki/Ultimate_ability)]**.

**FACT (computed).** Across 52 listed ultimates the cost ranges from **1,375** (cheapest) to **3,100**
points, median **2,300**, mean 2,271 — a spread of only **×2.25** from cheapest to dearest. At the
passive rate alone that is 4.6 to 10.3 minutes, median 7.7 minutes.

**FACT.** The wiki states the design consequence plainly: *"While ultimates have different costs,
higher costs do not necessarily mean longer charge times when combat-based ultimate generation is
considered... To judge how fast an ultimate charges, the hero's entire kit must be considered."*

**INFERENCE.** Overwatch's printed ultimate cost is deliberately a *narrow* band — a ×2.25 spread
across abilities whose in-fight impact varies far more than that. The real pricing dial is the hero's
generation rate, which is a property of their whole kit rather than of the ultimate. This is the
cleanest example in this file of a game **denominating the price of a strong action in the player's own
prior output**: the cost of the big button is literally "deal 2,300 damage first."

**Supporting mechanics, and a caveat on version drift.** Community sources add that every ultimate cost
is a multiple of **62.5** points, that Mercy receives only **30%** of ult charge from damage she boosts,
and that damage to barriers grants no charge at all
**[3rd — [Blizzard forum thread](https://us.forums.blizzard.com/en/overwatch/t/ult-charge-points/338580),
[Dexerto](https://www.dexerto.com/overwatch/this-overwatch-fan-made-graph-breaks-down-each-heros-ultimate-charge-time-256196/)]**.
**INFERENCE:** at 5 points/second, 62.5 points is exactly **12.5 seconds of doing nothing** — the cost
granularity is denominated in idle-time units.

⚠️ Those same sources give Nano Boost as **1,875** points where the current wiki table gives **2,300**.
Overwatch has rescaled ultimate costs globally more than once, so **point values are version-specific**
and the two figures are almost certainly the same ability before and after a rescale. The *conversion
rate* (1 damage or healing = 1 point, plus 5/second) is stable across both; the absolute costs are not.

**FACT.** Ultimate cost is tuned like any other number, in single-digit percentage steps. Official patch
notes for 11 August 2026: Hazard *"ultimate cost increased by 7%"*, Emre −6%, Genji −6%, Venture −8%,
Winston −6%, Jetpack Cat +7%
**[1st — [Overwatch patch notes](https://overwatch.blizzard.com/en-us/news/patch-notes/)]**.

**Valorant runs the same model on a much tighter band.** Ultimate points by agent
**[2nd — Liquipedia agent pages, which mirror patch notes; 19 of 29 agents retrieved before rate
limiting]**:

| Cost | Agents |
|---|---|
| 6 points | Cypher, Phoenix |
| 7 points | Astra, Deadlock, Gekko, Harbor, Iso, Sage, Omen |
| 8 points | Brimstone, Chamber, Clove, Fade, Jett, Sova, Raze |
| 9 points | Breach, Viper, Killjoy |

**INFERENCE.** The entire design space is **6–9 points** — a ×1.5 spread for abilities whose impact
varies far more. Compare Dota's ×4–5 cooldown spread and League's ×10. A band that tight means ultimate
*cost* is nearly a constant and power differences are absorbed elsewhere, which in turn makes the ±1
point moves visible in the patch history very large relative changes: 7→8 is a **+14%** increase in time
to ultimate.

**INFERENCE.** These two results together say something precise. Cooldown is **not** tuned per point of
damage — an 8-second ability and a 12-second ability of the same damage are both normal. Cooldown is
tuned by *category*: rotation abilities get single-digit seconds, fight-defining abilities get
80–200 seconds, and the tenfold gap between the two bands is where the entire tier-step price lives.
A design that instead prices the tier step in resource is spending its escalation budget on the axis
these games deliberately left flat.

---

## 4. Action economy in turn-based games

The question this section answers: **how does a game make a strong action cost something when there is
no mana?** In every case below the answer is a form of tempo — the action costs the player time, turn
order, position, or the opponent's opportunity. None of the seven systems below needs a pool.

### 4.1 The typed budget (D&D 2024) versus the fungible budget (Pathfinder 2e)

D&D 2024, official free rules **[1st]**:

> "On your turn, you can take one action." — "A Bonus Action is a special action that you can take on
> the same turn that you take an action. You can't take more than one Bonus Action on a turn, and you
> have a Bonus Action to take only if a rule explicitly says so." — "Once you take a Reaction, you
> can't take another one until the start of your next turn."
> ([D&D 2024 free rules glossary](https://www.dndbeyond.com/sources/dnd/free-rules/rules-glossary);
> the SRD 5.2 wording is *"You can take only one action at a time"* and *"You can take a Bonus Action
> only when a special ability, a spell, or another feature of the game states that you can do something
> as a Bonus Action"* — [5e 2024 SRD, Actions](https://5e24srd.com/playing-the-game/actions.html))

Movement is a separate, non-competing budget: *"On your turn, you can move a distance up to your Speed
and take one action"*, and it can be split around the action **[1st —
[5e 2024 SRD, Combat](https://5e24srd.com/playing-the-game/combat.html)]**.

**INFERENCE.** This is a **typed** economy, not a fungible one. A bonus action cannot be traded for an
action, so a character with no bonus-action option simply loses that slot. The design lever is *which
slot an ability is printed in*, not how many units it costs — which means the economy has no
granularity at all.

**Pathfinder 2e replaced that with three interchangeable actions**, and the designer said why.
Jason Bulmahn, Director of Game Design, *All About Actions*, 7 March 2018 **[1st —
[paizo.com](https://paizo.com/community/blog/v5748dyo5lklh)]**: Pathfinder 1e had seven action types —
*"free, full-round, immediate, move, standard, swift, and a nebulously defined 'other' category"* — and
combat *"could become rather bogged down by the weight of options available"*, with new players
*"frequently cit[ing] the complexity of the action system as an issue that made the game slow down as
players looked to maximize their turns."* The replacement is one sentence: **"It's your turn. You get to
take three actions. That's it."** Everyone also gets *"one reaction they can take when the conditions
are right."*

**The mechanism that actually prices the third action is not a cost — it is a penalty on repetition.**
**FACT:** *"The second time you use an attack action during your turn, you take a –5 penalty to your
attack roll. The third time you attack, and on any subsequent attacks, you take a –10 penalty"*
(agile weapons: –4 / –8) **[2nd — [PF2 SRD, Playing the Game](https://pf2.d20pfsrd.com/rules/playing-the-game/)]**.

**INFERENCE, and this is the single most transferable idea in this section.** Pathfinder 2e does not
make the third *action* expensive. It makes the third *repetition of the same action* expensive. The
budget stays uniform and fungible; the anti-degeneracy pressure is a **diminishing-returns curve on
repeats**. Attack/Attack/Attack is legal but bad, so players are pushed toward Attack + Raise Shield +
Step, or Attack + a two-action spell. Power is priced by what else the action could have been, and
repetition is taxed separately from cost.

**How a spell is priced in actions, and what each action buys.** Mark Seifter, *All About Spells*,
16 April 2018 **[1st — [paizo.com](https://paizo.com/blog/all-about-spells)]**: two actions is the
default, and *"the number of actions you spend when Casting this Spell determines its targets, range,
area, and other parameters."* The live rules text for `heal`
**[2nd — [Archives of Nethys](https://2e.aonprd.com/Spells.aspx?ID=1554)]**:

| Actions | What the extra action buys |
|---|---|
| 1 | Range **touch**; 1d8 HP (heightened +1d8 per level) |
| 2 | Range **30 feet**, **and +8 HP** on top of the dice |
| 3 | **30-foot emanation**, hitting every living and undead creature in the area — and the flat +8 is dropped |

`harm` is the exact mirror **[2nd — [Archives of Nethys](https://2e.aonprd.com/Spells.aspx?ID=1552)]**.

**INFERENCE.** The pricing shape is legible and directly copyable: **action 1 buys the effect, action 2
buys reach plus a flat magnitude bonus, action 3 buys area — and trades the magnitude bonus away to get
it.** That is a three-axis ladder (magnitude → reach → area) on a single ability, collapsing what 5e
would print as three separate spells. The other common shape is **action compression**: the fighter's
Sudden Charge costs 2 actions and delivers what Stride/Stride/Strike would cost 3 **[1st]** — so in
Pathfinder 2e "power" frequently means *doing three actions' work in two*, not a bigger number.

**What problem the action-count budget solves:** it prices every action against every other action with
no economy to tune at all.
**What it costs the designer:** granularity, unless the budget is fungible and large enough (3 is
enough; 1 + typed extras is not).
**What breaks:** anything that grants extra actions is worth more than anything that adds power, so the
extra-action tier becomes a second hidden power budget that every feature competes over.

### 4.2 Turn order as the price — Final Fantasy X's CTB

This is the cleanest "power costs tempo" formula found anywhere, and it is datamined rather than
described. **FACT [data — [FFX-Info CTB datamine](https://grayfox96.github.io/FFX-Info/game-mechanics/ctb)]**:

> "Each character and monster has a Base CTB, the higher their Agility, the lower their base CTB. At
> the start of an encounter each character and monster is assigned a CTB Value … then the game starts
> decreasing everyones CTB Value until someone has a CTB Value of 0, at which point that
> character/monster takes their turn and their CTB Value is set to `Base CTB * Action Rank`."

```
next_turn_delay = Base CTB × Action Rank

Base CTB is a lookup on Agility (1–255):
  Agility   1    2    3    4    5   10   15   20   25   30
  Base CTB 28   26   24   20   16   14   12   10    9    8

Haste:  CTB -= Current CTB // 2 ;  when acting, CTB Value is halved (rounding up)
Slow:   CTB doubles             ;  when acting, CTB Value is doubled
Weak Delay (Delay Attack):  += Base CTB * 3 // 2
Strong Delay (Delay Buster): += Base CTB * 3
Revival and Sleep both reset the actor to Base CTB * 3
```

The Rank table, with the proportionality stated outright — *"a rank 4 action results in twice as much
delay as a rank 2 action, and results in four times as much delay as a rank 1 action"*
**[2nd — [Final Fantasy Wiki, Rank (FFX)](https://finalfantasy.fandom.com/wiki/Rank_(Final_Fantasy_X))]**:

| Rank | Representative actions |
|---|---|
| 1 | Weapon/armour switch, Escape, **Quick Hit (original release)**, Quick Pockets |
| 2 | Item, Defend, Flee, Cheer/Aim/Focus, Lancet, **Quick Hit (International/HD)**, Drain, the Nul- spells |
| 3 | **Attack**, Summon, Steal, Pray, Guard, Provoke, **Doublecast**, all Fire/Thunder/Water/Blizzard tiers, Bio, Demi, Death, Cure→Curaga, Esuna, Life, Slow, Shell, Protect, Reflect, Dispel, Regen |
| 4 | Power/Magic/Armor/Mental Break, Haste, Slowga, Holy |
| 5 | Full Break, **Flare**, Grand Summon, Energy Rain |
| 6 | Delay Attack, **Ultima**, Hastega, Banishing Blade |
| 7 | Blitz Ace, Tornado |
| 8 | Delay Buster |

**Worked example (computed from the sourced formula and tables).** A character at Agility 20 has Base
CTB 10:

| Action | Rank | Ticks until next turn |
|---|---|---|
| Quick Hit (original) | 1 | **10** |
| Attack | 3 | **30** |
| Ultima | 6 | **60** — one Ultima costs exactly two Attacks |
| Blitz Ace | 7 | **70** |

**FACT.** Quick Hit's rank-1 pricing is why it is the most notorious ability in the game, and the
International/HD release rebalanced it from rank 1 to rank 2 *and* raised its MP cost from 12 to 36
**[2nd]** — a change on both axes at once.

**FACT.** Doublecast has *"a fixed rank of 3, regardless of the Black Magic spells that are chosen"*
**[2nd]**.

**INFERENCE — a documented pricing hole worth remembering.** Doublecast Ultima + Ultima costs rank 3 of
tempo, where casting Ultima twice costs rank 6 twice. The strongest spell in the game is *cheaper per
cast in tempo when cast twice* than when cast once. If a system prices actions by rank, any "perform
two things as one action" ability must inherit the rank of what it performs, or the economy breaks.

**FACT.** The cost is **previewed before commit**: *"moving the cursor over commands reveals how it
will change the order … this preview assumes everyone else will use a rank 3 action"* **[2nd]**.

**INFERENCE.** A tempo economy is only playable if the price is visible before the decision. FFX shows
the reordered turn queue on hover; without that, rank is invisible arithmetic the player cannot budget
against.

**Grandia** is the aggressive form of the same idea: combatants advance along a shared bar with Wait
and Act phases, and *"combo attacks only stall the enemies in their movement, while heavy attacks push
them back"*, with a Cancel that fires when *"performing a heavy attack while an enemy prepares an
attack during its act phase"* — and *"this mechanic applies reciprocally"*
**[3rd — [rpg-o-mania](https://www.rpg-o-mania.com/coverage_battlesystems_grandia.php)]**.
**INFERENCE:** FFX lets an action cost only *your own* tempo; Grandia makes tempo two-sided and adds a
timing window in which a slow heavy attack is worth far more than its price — converting "which action"
into "which action, right now" at no extra systemic cost.

**What problem it solves:** the price is paid in a currency the player can see change.
**What it costs the designer:** every ability needs a speed number as well as a power number, and the
two interact multiplicatively with the Haste/Slow family.
**What breaks:** Doublecast-shaped abilities that launch several actions under one rank; and Haste
strong enough that every action's tempo price rounds to nothing.

### 4.3 Press Turn — the only economy that can price *being wrong*

Shin Megami Tensei III onward. **FACT [2nd —
[megatenwiki, Press Turn System](https://megatenwiki.com/wiki/Press_Turn_System)]**:

- *"each combatant contributes an amount of Press Turns to the party as a whole; in most cases, each
  combatant only contributes one Press Turn, though some (notably bosses and other powerful enemies)
  may contribute more."*
- *"Taking certain actions will instead convert one of the party's Press Turns into a 'half turn',
  giving the party an extra action before they must pass to the enemy party."* Generated by *"striking
  enemy weaknesses, dealing Critical Hits, passing one's turn and summoning a demon from the stock."*
- **The hard cap:** *"Half turns behave the same as a normal Press Turn, except they cannot grant more
  actions to the party even if more could be generated; this limits a party's maximum amount of actions
  to double their initial amount of Press Turns."*
- **Penalties:** *"certain actions consume additional Press Turns … These include hitting an enemy's
  nullification (or stronger) affinity or missing an attack."*
- **Tie-break:** *"If an action would both add and remove Press Turns (for example, a multi-target
  skill hitting both a weakness and a nullification), only the penalty will apply."*
- **Symmetry:** *"This applies to both sides, making it a double-edged sword."*

The numeric costs, which megatenwiki does not print
**[3rd — [Game8 SMT III guide](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/332175);
corroborated at [Samurai Gamers](https://samurai-gamers.com/shin-megami-tensei-iii-nocturne-hd-remaster/battle-system-4/)]**:

| Outcome | Press Turn cost |
|---|---|
| Successful normal attack or skill | 1 turn |
| **Critical hit** | **½ turn** |
| **Exploit weakness** | **½ turn** |
| Pass | ½ turn |
| Summon demon | 1 turn |
| **Attack dodged or missed** | **2 turns** |
| **Repel** | **2 turns** |
| **Null / Absorb** | **all remaining turns** |

Game8 also gives the spend-order rule: *"the remaining ½ Press Turn is always treated as if it were 1
Whole Turn"* — half icons are spent first, and a half icon still buys a full action.

⚠️ **Unresolved source conflict.** Game8 lists Repel = 2 turns and Null = all turns; the
widely-repeated version has Null = 2 and Repel/Absorb = all. megatenwiki says only *"nullification (or
stronger)"* without the split. **INFERENCE:** the numbers genuinely differ between entries in the
series (Nocturne vs SMT IV vs SMT V vs Digital Devil Saga), so the exact Null/Repel split should be
treated as **game-specific and not settled**. The ordering is stable everywhere —
`null < repel ≤ absorb` in badness, and all three are worse than a miss.

**Atlus's own framing of the goal**, quoted on megatenwiki from atlusnet.jp: *"The name Press Turn
means a system that enables constant pressure on the opponent"* — 「場の支配中は、攻撃行動で、相手を押し
続けられるようなシステムということで、通常のターン制と区別して、プレスターンと呼ぶことにしました。」
**[1st, via 2nd]**.

**Persona's One More / Baton Pass is the softened version**, and the diff is instructive
**[2nd — megatenwiki]**. Persona 3 onward: hitting a weakness or landing a critical Downs the target
and *"will grant the attacker one additional action immediately"*; when all enemies are Down the party
may spend the turn on an All-Out Attack. Persona 5 adds Baton Pass, giving the extra turn to an ally
and *"increasing the recipient's damage and healing"*. Persona 3's original harsh clause —
*"a combatant will take their turn to recover from Down"* — was dropped in Persona 4.

**INFERENCE.** Press Turn is a *shared party pool with real negative values* (miss = −2, null = wipe).
One More is *per-character with no penalty branch at all* — you can gain extra actions but never lose
them. Same upside, completely different downside, and that is the whole reason one series reads as
punishing and the other as a combo game. **To make a tempo economy approachable, keep the bonuses and
delete the penalties.** That is literally the diff Atlus shipped.

**What problem it solves:** it makes *knowledge* the currency. A correct action is cheaper than a
normal one and a wrong action costs double, so the price is set by the target rather than by the
action. A mana pool structurally cannot do this — mana does not know whether the spell was a good idea.
**What it costs the designer:** enormous swing in both directions, and it must apply to the enemy team
identically or it reads as unfair.
**What breaks:** guaranteed weakness coverage. If every action can be made to cost ½, the turn budget
doubles permanently — which is exactly why the hard cap at *"double their initial amount"* exists, and
why the series pairs the system with enemies that null and repel.

### 4.4 XCOM — two action points, and "this ends your turn"

**FACT [2nd — [UFOpaedia, Gameplay Mechanics (EU2012)](https://www.ufopaedia.org/index.php/Gameplay_Mechanics_(EU2012))]**:

> "Time Units have been removed and replaced with a two-part action system, where each unit can perform
> 2 Actions during its turn." … "A unit's turn automatically ends after the unit: Performs any action
> other than Move on its 1st Action. Performs a 2nd Action after moving as the 1st Action."

**INFERENCE.** Read carefully this is not "two free actions". The real menu is *move twice, or move once
and do one thing, or do one thing.* The second action point is only fungible if the first was movement.
Shooting is not one of two actions — shooting **is the end of your turn**, and its true price is the
repositioning you can no longer do.

**FACT.** Overwatch's price is quantified: it *"ends the soldier's turn, but allows them to shoot at
the first enemy that moves without any cover within their vision range during the enemy turn, albeit at
an Aim penalty and without dealing critical hits"*, with *"a 0.7 modifier for Aim … further reduced
again to 0.5 if the target is dashing"* **[2nd —
[UFOpaedia, Overwatch (EU2012)](https://ufopaedia.org/index.php/Overwatch_(EU2012))]**. (The Gameplay
Mechanics page instead states a flat 20% Aim penalty; the two pages disagree and the multiplier version
is the more specific claim.)

**FACT.** The premium abilities in the game are the ones that break the turn-ending rule: Bullet Swarm
(Heavy: Fire + a second action), Double Tap (Sniper: Fire + Fire), Run & Gun (Assault: Dash + Fire), and
Snap Shot (Sniper: move and fire *"at reduced accuracy"*) **[2nd]**.

**INFERENCE.** Every one of those is a purchase of **action economy itself**, and Snap Shot prints the
exchange rate: you buy back the move-and-shoot option by paying accuracy.

**XCOM 2 keeps 2 AP but makes the scale expressive, with one clever permission** **[2nd —
[UFOpaedia, Action Points](https://www.ufopaedia.org/index.php/Action_Points(LWR))]**: all abilities
default to 2 AP; *"units can use abilities that cost 2 AP even if they only have 1 AP left (so the
unit's APs get below zero). A unit's turn will end if their APs have reached zero (or less)."* A dash
costs 2 AP, a half-move 1 AP, and *"certain abilities cost only 1 AP … or 0 AP."*

**INFERENCE — a cheap trick worth stealing.** Allowing AP to go **negative** is what makes "move then
shoot" work while keeping "shoot" a 2 AP action: a 2 AP ability spent from 1 AP overdraws to −1, which
ends the turn but still resolves. You get end-turn semantics with no separate "ends turn" flag, and 1
AP and 0 AP abilities as first-class citizens on the same scale. One number, four behaviours.

**What problem it solves:** it prices a defensive or reactive action at exactly "your whole turn"
without writing a number down.
**What it costs the designer:** an end-turn ability hands the *opponent* the decision of whether your
action resolves at all, which is hard to value.
**What breaks:** any ability that refunds the turn. XCOM 2's Ever Vigilant is the controlled version —
it refunds the Overwatch action to players who accept the positional constraint of an all-move turn.

### 4.5 Darkest Dungeon — position as a second budget, and a randomised turn order

**FACT [2nd — [darkestdungeon.wiki.gg, Combat Mechanics](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon))]**:
*"At the start of each round, each unit is assigned a **round speed**, which is their SPD plus a random
value from 1 to 8."* Ranks run 1–4, *"with 1 being closest to the opponent party and 4 being the
farthest away."*

**INFERENCE.** Hero SPD values sit in the single digits, so a **±8 random swing is larger than most of
the stat spread**. Turn order is *biased*, not determined, by Speed — buying Speed buys a probability
shift, which is why speed-stacking never fully solves a fight and stays a legitimate purchase.

Skills are gated by position on both ends. Highwayman **[2nd —
[wiki.gg hero page](https://darkestdungeon.wiki.gg/wiki/Highwayman)]**:

| Skill | Usable from rank | Targets rank | Move rider |
|---|---|---|---|
| Wicked Slice | 1 | 2–4 | — |
| Pistol Shot | 1–3 | 2–4 | — |
| Point Blank Shot | **4** | **1** | knockback 1 on target |
| Grapeshot Blast | 1–2, 4 | 2–3 | — |
| Duelist's Advance | 2–3 | 2–3 | **forward 1 (self)** |

**INFERENCE.** A hero's *available action set is a function of a positional state the enemy can change*,
and nothing is ever spent. Three consequences follow:

- **The cost of a strong action is a positional commitment.** Point Blank Shot reaches from rank 4 to
  rank 1 — enormous reach — but you must *be* in the back, which is where the Highwayman's other skills
  do not work.
- **Self-move riders are the payment schedule.** Duelist's Advance moves you forward 1, so repeated use
  walks you out of its own legal launch band. The skill expires by being used.
- **Enemy shuffle attacks are pure tempo damage that deal no HP damage.** A shuffle costs the party
  actions to undo while dealing zero damage — a Press Turn penalty expressed spatially.

### 4.6 Slay the Spire — two use-it-or-lose-it budgets

**FACT [2nd — [wiki.gg Mechanics](https://slaythespire.wiki.gg/wiki/Mechanics)]**: at the start of each
player turn *"Energy is set to the base energy (default 3)"*, *"a number of cards are drawn (default
5)"*, and *"the maximum number of cards allowed in hand is 10. There is no way to exceed this limit."*
On ending a turn, *"cards in hand are shuffled into the discard pile."*

**INFERENCE.** The wording *"set to"* rather than *"increased by"* is the proof that **neither budget
carries over**. Unspent energy is destroyed and unplayed cards are discarded, so there is no
"save up for a big turn" pattern except through explicit cards that are themselves priced (Adrenaline,
Offering).

**FACT.** Exactly 10 cards have X cost (`cost: -1` in the datamined data): Whirlwind, Skewer, Malaise,
Transmutation, Doppelganger, Multi-Cast, Tempest, Reinforced Body, Collect, Conjure Blade **[data]**.

**INFERENCE.** X cards are how a fixed 3-energy budget still supports uncapped scaling — they convert
energy into effect at a linear rate with zero rounding waste, and they are the game's only "all-in"
cost, spending the entire remaining pool.

**What problem it solves:** two independent scarcities that bind at *different moments* produce far
more interesting turns than one bigger scarcity. You routinely have energy you cannot spend because you
drew the wrong cards, and cards you cannot play because you are out of energy.
**What it costs the designer:** variance, and a whole class of unplayable-hand complaints.
**What breaks:** draw engines. Once a deck reliably sees its whole library the hand limit stops binding
and only the energy ceiling remains — which, per §2.7, is the weaker of the two brakes.

### 4.7 When an action costs literally nothing but the turn

**Fire Emblem makes the turn scarce retrospectively and globally.** Turn structure is phase-based and
*"a turn has no limit on the real time it takes to accomplish"*; some objectives carry deadlines and
*"in Three Houses, all maps have a hard limit of 99 turns"*
**[2nd — [fireemblemwiki.org, Turn](https://fireemblemwiki.org/wiki/Turn)]**. The scarcity is imposed
by *scoring*: Genealogy of the Holy War's Tactics rank measures total turns across the whole game
**[2nd — [fireemblemwiki.org, Rankings](https://fireemblemwiki.org/wiki/Rankings)]**:

| Rank | A | B | C | D | E |
|---|---|---|---|---|---|
| **Tactics** (total turns) | ≤399 | 400–549 | 550–799 | 800–1099 | ≥1100 |
| **Experience** (levels gained) | 1,000+ | 800–999 | 600–799 | 400–599 | ≤399 |
| Survival (units lost) | 0 | 1 | 2 | 3 | 4+ |
| Combat (battle losses) | ≤3 | 4–10 | 11–30 | 31–50 | 51+ |

**INFERENCE — this table is the design in miniature.** Tactics and Experience are **directly opposed**:
grinding EXP costs turns, rushing costs EXP. Neither is spent from a pool; both are scored at the end.
A player who takes a slow safe turn pays nothing now and something later. That is opportunity cost in
its purest published form.

Thracia 776 is the only entry that makes the turn budget a *stat*: units have zero to five **action
stars**, giving `(stars × 5)%` chance of a complete extra turn after acting, capped at 25% and once per
turn **[2nd — [fireemblemwiki.org, Action](https://fireemblemwiki.org/wiki/Action)]**. **INFERENCE:**
even as a stat it is deliberately unreliable. The series' main tempo mechanic remains the Dancer, which
spends its own turn to refund another unit's — netting zero unless the refunded unit is worth more.

**Into the Breach is the purest case in this whole file: nothing is ever spent.** Every mech gets a move
and one weapon every turn; missions give *"a fixed number of turns to complete that objective"*; enemy
attacks are fully telegraphed; and *"should a civilian structure be damaged or destroyed, the power
grid is weakened"* **[3rd — [Wikipedia](https://en.wikipedia.org/wiki/Into_the_Breach)]**. Community
notes add that the Grid is run-scoped, starts at 5 and caps at 7, and that hitting zero ends the run
**[3rd — [community mechanics notes](https://github.com/hintforge/into-the-breach/blob/main/mechanics.md)]**.

**INFERENCE.** Into the Breach shows you can build an entire game where actions are free, provided
(a) the turn is capped, and (b) there is a **persistent, slowly-depleting stake** that punishes an
imperfect turn. The Grid is the actual currency, and it is spent by *failing to act well*, not by
acting. The cost of a strong action is the enemy attack you did not block with the move you did not
make.

---

## 5. Wind-up, cast time and recovery as cost

**This section's headline reverses the obvious intuition.** Wind-up is not the price of power. In the
one genre that publishes complete timing data for every action, the price is paid on the **back end** —
recovery and punishability — and the strongest actions frequently have *shorter* wind-up than ordinary
ones.

### 5.1 Fighting-game frame data: recovery is the price, not startup

Computed over 1,600 Guilty Gear Strive moves from the Dustloop frame-data database
**[data — [Dustloop Cargo API](https://www.dustloop.com/wiki/api.php?action=cargoquery&tables=MoveData_GGST)]**.

Sol Badguy, concrete **[2nd — [Dustloop, Sol Badguy](https://www.dustloop.com/w/GGST/Sol_Badguy)]**:

| Move | Startup | Active | Recovery | Damage | On block |
|---|---|---|---|---|---|
| 5P (light punch) | 5 f | 5 | 9 | 28 | −2 |
| c.S | 7 f | 6 | 10 | 44 | +3 |
| 5H (heavy) | 11 f | 4 | 20 | 52 | −5 |
| 2H | 11 f | 5 | **31** | 46 | **−17** |
| 6H | 9 f | 3 | **43** | 60 | **−27** |
| **Tyrant Rave** (Overdrive) | **7+2 f** | 3(20)20 | 41 | **140** | **−44** |
| **Heavy Mob Cemetery** (Overdrive) | 13+1 f | 16 | 49 | **270** | — |

Whole-game regressions (computed):

| Relationship | n | slope | Pearson r |
|---|---|---|---|
| damage ~ **startup**, normals + specials | 1,312 | +0.43 dmg/frame | 0.301 |
| damage ~ **startup**, Overdrives only | 84 | −0.13 | **−0.032** |
| damage ~ **recovery frames** | 1,121 | **+1.21 dmg/frame** | **0.544** |
| damage ~ **on-block advantage** | 1,076 | **−0.98 dmg per frame of disadvantage** | −0.374 |

**FACT (computed).** Overdrives — the game's supers — have a **shorter mean startup than the average
normal**: 10.4 frames against 14.6, while dealing **2.5× the damage** (mean 108 vs 44).

**FACT (computed).** Damage sorted by punishability shows where the price actually sits:

| On block | n | median damage |
|---|---|---|
| +/0 (safe) | 239 | 38 |
| −1 to −5 | 301 | 30 |
| −6 to −12 | 278 | 38 |
| −13 to −25 | 187 | 41 |
| **−26 to −45** | 53 | **70** |
| **worse than −45** | 18 | **102** |

Extremes: Queen Dizzy's Gamma Ray — 281 damage, **214 frames of recovery, −74 on block**; Nagoriyuki's
Zansetsu — 200 damage, **−66**; Unika's Arbalest Megadeath Buster — 180 damage, **−75** **[data]**.

**Tekken 8 reaches the same conclusion independently.** Over 1,686 single-hit moves from 16 characters
**[data — [Wavu Cargo API](https://wavu.wiki/t/Special:CargoExport?tables=Move)]**:

```
damage = 0.37 × startup + 13.56        r = 0.415
block advantage ~ startup: slope −0.13 frames per startup frame,  r = −0.126  (no relationship)
```

Median damage is nearly flat from startup i13 through i32 (15, 15, 17, 20, 20, 20, 21, 20, 21, 20, 21,
20, 18, 22, 25, 20, 21, 22) **(computed)**. The sharpest counterexample to any frames-for-power rule is
in the same dataset: Kazuya's Wind God Fist and its just-frame **Electric** version have **identical
startup (i11~12)**, but the Electric deals +3 damage and swings from **−10 to +5 on block** — the price
there is execution difficulty, a currency that does not appear in frame data at all **[data]**.

**INFERENCE, and this is the most useful reframe in the file.** Wind-up and recovery are *different
currencies serving different parties*:

- **Wind-up buys the defender a reaction window.** It is counterplay, priced against how readable the
  action must be.
- **Recovery and on-block disadvantage buy the attacker a punishment if wrong.** That is the commitment,
  and it is denominated in **the opponent's free turn** — which is exactly the tempo currency of §4.

A super in Strive does not make you wait to throw it. It puts you in a hole for 40–214 frames if it
misses. Any design that tries to price power in wind-up is charging on the axis these games left
comparatively flat.

### 5.2 The same result in two games with no fighting-game framing

```
Dota 2 — max-level damage vs ability cast point, log-log fit, n = 132 abilities
    exponent 0.041      r = 0.030      r² = 0.001

Path of Exile — level-20 base damage vs gem cast time, log-log fit, n = 76 gems
    exponent 0.566      r = 0.288      r² = 0.083
```

**FACT (computed).** In Dota, wind-up explains **0.1%** of the variance in ability damage. Across all
669 abilities that declare a cast point, the median is **0.20s for basics and 0.30s for ultimates** — a
0.1-second difference — while the cooldown lever spans 4× **[data]**. The longest cast points in the
game are Nature's Prophet Teleportation (3.0s), Sand King Epicenter (2.0s) and Sniper Assassinate
(2.0s) — abilities whose wind-up *is* their counterplay, not their price.

**FACT (computed).** In Path of Exile the relationship is weakly positive but explains only 8% of the
variance, and the fitted exponent of 0.566 is well below the 1.0 that "damage in proportion to cast
time" would require.

### 5.3 The one recoverable power formula in the industry — and it prices wind-up

There is exactly one exception to §2.8's negative finding, and it is worth the whole section. WoW's
original spell-power coefficient was a **direct function of cast time**, implemented exactly in the
open-source vanilla server core **[data —
[cmangos/mangos-classic, `SpellMgr.cpp`](https://github.com/cmangos/mangos-classic/blob/master/src/game/Spells/SpellMgr.cpp)]**:

```
coefficient = clamp(cast time, 1.5s, 7.0s) / 3.5s

  DoT variant:  duration / 15s, then divided across the number of ticks
  area effect:  × 0.5
  health leech: × 0.5
  each additional rider effect: × 0.95   (stun / root / confuse count as two → ×0.90)
```

So a **3.5s cast scales at 100%** of the caster's spell power, a **1.5s cast at 42.9%**, and a **7s cast
at 200%**.

**FACT.** The emulator implements exactly this. **INFERENCE** — well-established, and it reproduces
vanilla and Burning Crusade numbers — that it was Blizzard's original authoring rule. Blizzard later
moved to hand-authored per-spell coefficients, so the formula is historical rather than current.

**INFERENCE, three observations that make it directly useful:**

1. **The floor is the global cooldown.** 1.5s is exactly the GCD — *no action is credited with less
   wind-up than one turn*, because it cannot cost less than one turn.
2. **The ceiling caps how much power wind-up may buy.** 7 seconds, twice the reference cast, and no
   further.
3. **The formula does not price time alone; it prices time and then discounts for breadth.** Area halves
   it, and every extra attached effect shaves 5%. That is a complete "one action, one budget, spent
   across damage, area and control" model — with the *size* of the budget set by wind-up and the
   *division* of it set by how many things the action does.

That third point is the transferable part. It is the only shipped example found of a single scalar
budget being derived from a timing cost and then split across an action's effect list.

### 5.4 Cast time as contested commitment

**FACT (computed).** FFXIV shows how rarely a modern game takes the commitment cost at all: **725 of 936
player actions are instant**, and only 211 have a cast bar — 109 at 2.0s, 46 at 1.5s, 18 at 1.0s
**[data]**. Among off-global abilities the pattern is near-absolute: **403 of 404 oGCD Abilities have a
cast time of 0.0s** **[data]**. The longest casts in the game are the resurrections — Verraise at 10.0s,
Raise / Resurrection / Ascend / Egeiro at 8.0s **[data]**.

**FACT.** A cast time is not a fixed price; it is a *contested* one. WoW's pushback rules **[2nd —
[warcraft.wiki.gg, Pushback](https://warcraft.wiki.gg/wiki/Pushback)]**: for a cast-time spell, *"the
first and second hit will each add 0.5 second to the cast time. All hits after the second will have no
effect"* — a maximum of **1.0s** of added delay. For a channel, *"the first and second hit each reduce
current duration by 25% of total duration"* — a maximum **50%** loss. Fully absorbed damage prevents
pushback; partial absorption does not.

**INFERENCE.** Pushback and interrupts make the caster's price asymmetric with the interrupter's. The
caster pays the full cast time up front; the interrupter pays a short cooldown to void the entire
investment and deny the school on top. That asymmetry — not the cast bar itself — is what makes a slow
action punishable, and it is the same shape as the fighting-game recovery finding: **the real cost of a
slow action is the free turn it hands the opponent.**

**Where wind-up genuinely is a ladder-long price:** WoW vanilla Fireball (§2.3) raised cast time from
1.5s to 3.5s across ranks 1–5 and then froze it for the remaining seven ranks. That is the coefficient
formula visible in the shipped data — rank 5 hits the 3.5s reference cast, the 100% coefficient point,
and no later rank is allowed to buy more. Damage per second of cast still improved ×13.5 across the full
ladder (computed). Even in the clearest example, wind-up paid for the first third of the ladder and
nothing after.

---

## 6. Opportunity cost as the real price

This is the most transferable framing for a turn-based game with regenerating resources, and the
measured data supports it more strongly than any resource finding in this file.

**FACT (computed).** In League of Legends, a champion reaches level 18 and receives 18 skill points.
The sum of maximum ranks across a champion's four abilities is exactly **18 for 165 of 172 champions**
**[data]**. Every ability reaches maximum rank in a full-length game.

**INFERENCE, and it is the important one.** League's rank ladder has **zero terminal opportunity
cost**. Nothing is given up permanently. The entire cost of ranking an ability is *when* you get it,
never *whether* — and that is sufficient, on its own, to carry a five-rank ladder in a game played
professionally for fifteen years. This is why §2.6 finds flat mana costs and falling cooldowns and no
contradiction: the ladder does not need a price because ordering *is* the price.

**FACT (computed).** Guild Wars 2's Thief runs the opposite experiment: a 12-point regenerating
initiative pool, no cooldowns on any weapon skill, and skills priced at 1–6 points **[1st, official
API]**. Damage per initiative point across 39 damaging skills ranges from **0.042 to 2.240**, a **53×
spread**, and 3-point skills return a median 0.50 damage per point against 4-point skills' 0.221.

**INFERENCE.** Guild Wars 2 does not price damage per initiative at all — a high-cost skill is buying
stealth, evasion, blind or repositioning, and the initiative price is set against the *whole package*.
The 53× spread is not a balance failure; it is evidence that the cost field is pricing opportunity
(what else those 5 points could have bought this second) rather than pricing damage.

**FACT.** FFXIV's strongest actions are free and priced only in the opportunity to have used them
earlier or later — Hallowed Ground at 420s and Benediction at 180s cost no resource whatsoever
**[data]**. The decision is entirely "is this the right moment", which is opportunity cost with no
other cost attached.

### 6.1 What designers have actually written about it

The published commentary is thin but on point.

**FACT.** Sid Meier, GDC 2012, *Interesting Decisions*, as reported by Game Developer
**[3rd — [Game Developer, 7 March 2012](https://www.gamedeveloper.com/design/gdc-2012-sid-meier-on-how-to-see-games-as-sets-of-interesting-decisions)]**:

> "One common characteristic of interesting decisions is that they involve some kind of tradeoff — say,
> the opportunity to get a big sword costs 500 gold, or in a racing game the fastest car may have poorer
> handling."

**INFERENCE.** The racing-car half of that sentence is the important half. The sword costs *gold* — a
resource economy. The car costs *handling* — nothing is spent at all; you simply cannot have both
properties. Meier presents them as the same kind of decision, which is the argument that an
opportunity/tempo cost is not a lesser form of pricing than a resource cost.

**FACT.** Reid Duke, *Tempo*, on Wizards' official site
**[1st — [magic.wizards.com, 22 September 2014](https://magic.wizards.com/en/news/feature/tempo-2014-09-22)]**:

> "Tempo, in the most basic form, is board presence." … "Tempo is a resource, and when it comes to
> modern-day Magic … I personally feel that it's the resource most closely connected with winning and
> losing games." … "you'll sometimes have to choose between gaining tempo at the expense of card
> advantage, or vice versa."

His worked example is exactly the opportunity-cost frame: Divination costs 3 mana and *gains* cards, but
*"you've spent a turn without improving your board presence."* The cost that decided the game was the
turn, not the mana.

**FACT.** Mark Rosewater, *Ten Things Every Game Needs*
**[1st — [magic.wizards.com, 24 October 2011](https://magic.wizards.com/en/news/making-magic/ten-things-every-game-needs-part-1-2011-10-24)]**:
*"Restrictions breed creativity"*, and *"your job as a game designer is to force your players to have to
be creative to overcome the restrictions you create."*

### 6.2 Synthesis

**INFERENCE.** Across every system in this file, the price of a strong action is never really the units
it consumes. It is one of four things:

1. **The alternative action** — Pathfinder 2e (a two-action spell means you Strike once, not twice);
   XCOM (Overwatch means you do not reposition).
2. **The next opportunity** — FFX's Action Rank, Grandia's IP bar: you act now, so you do not act later.
3. **The state you must be in** — Darkest Dungeon's rank gating; Slay the Spire's Clash and Grand Finale;
   XCOM's "must have 2 AP".
4. **The information you hand the opponent** — XCOM Overwatch, D&D reactions, Press Turn's miss penalty,
   and every fighting-game move with long recovery.

None of the four needs a pool. All four need only that the **turn** be scarce, and each game makes it
scarce differently: Pathfinder 2e caps it at 3 and taxes repeats, FFX makes the next one cost more, SMT
lets the opponent take it, XCOM ends it early, Darkest Dungeon randomises who gets it, Slay the Spire
destroys whatever you did not use, and Fire Emblem counts how many you took at the very end.

Three independent shipped systems — a MOBA rank ladder, an MMO cooldown list, and a class with no
cooldowns at all — converge on the same answer: **when a game wants a strong action to feel expensive,
it makes the moment scarce, not the resource.** Resource cost is what these games use to price
*sustain*; scarcity of the moment is what they use to price *power*.

---

## 7. What breaks

> **The long version is [07-cost-mistuning-failures.md](07-cost-mistuning-failures.md)** (added
> 2026-09-02, after the parallel exploit-literature thread returned). This section is the summary; that
> file carries the cases with patch numbers, dates and first-party quotes, plus nine recurring failure
> modes.

*(Research note: this is the thinnest section in the file. A parallel search thread assigned to the
wider exploit literature — Hearthstone loops, Diablo 3 resource-cost-reduction stacking, Path of Exile
mana loops, and Rosewater's mana-cost post-mortems — had not returned when the file was completed.
The four cases below are the ones that could be sourced or computed directly, and they are enough to
establish the pattern; see **What I could not find** item 14 for the remainder.)*

### 7.1 The generic failure: generation outruns spend

**INFERENCE, structural.** Every resource economy has the same single failure: a loop where an action's
output feeds its own cost. It has three forms — a spender that refunds itself on kill, a generator that
produces more per action than the spender consumes, and a cost reduction that reaches or approaches
zero. All three end in the same state: the resource stops being a constraint and the action is priced
only by whatever cooldown remains.

League ships a deliberate, bounded version of the first form: **Annie's Disintegrate refunds its mana
cost and halves its cooldown if it kills the target** **[data]**. The refund is conditional on a kill,
which is exactly the brake — the loop cannot self-sustain against a target that does not die.

**The best-documented real case is Magic's fast mana.** Magic's answer to cards that generate more
resource than they cost was not to re-cost them — it was to remove them from play. The Vintage
restricted list (one copy per deck) and the Legacy banned list (zero copies) between them contain
**Black Lotus, all five Moxen, Mana Crypt, Mana Vault, Sol Ring, Lotus Petal, Time Vault, Tolarian
Academy, Channel, Mind's Desire and Yawgmoth's Will**
**[1st — [Wizards banned & restricted list](https://magic.wizards.com/en/banned-restricted-list)]**.

**FACT.** Almost every card on those lists is there because it breaks the *cost* side of the game
rather than the power side — it produces mana, refunds mana, or takes an extra turn.

**INFERENCE.** Thirty years of the archetypal cost-vs-power game produced exactly one durable fix for
resource generation outrunning resource spend: **remove the generator**. Re-pricing was tried and did
not hold, because a card that produces more than it costs cannot be fixed by making it cost more — the
loop just needs one more iteration.

### 7.2 Cooldown reduction stacking toward zero — the best-documented fix

Riot removed percentage Cooldown Reduction in patch 10.23 and replaced it with Ability Haste
**[1st — [Patch 10.23 notes](https://www.leagueoflegends.com/en-us/news/game-updates/patch-10-23-notes/)]**.
Riot's stated problem: each percentage point of CDR provided *more* value as CDR accumulated, which
forced a hard **40% cap** to stop abilities becoming overpowered — and that cap locked players out of
items they otherwise wanted. Riot's replacement: *"Every point of ability haste lets you cast 1%
faster"*, with

```
cooldown reduction % = ability haste / (ability haste + 100) × 100
```

**FACT.** This is the only explicit cost-related formula any studio was found to have *published* in
this pass. (The WoW spell-power coefficient of §5.3 is more directly a power formula, but Blizzard never
published it — it was recovered from an open-source server core.)

**Why it works (INFERENCE).** The hyperbolic form asymptotically approaches 100% reduction but never
reaches it, so the cap becomes unnecessary — the function itself refuses to reach zero. That is
structurally the same device as an absolute bound that is *derived from the arithmetic* rather than
imposed as a ceiling, and it removed a hard progression stop that players could actually feel.

**The failure it fixed:** a linear percentage reduction has increasing marginal value (going 30%→40% is
worth more than 0%→10%), so it always needs a cap, and a cap on a magnitude is a progression ceiling.

### 7.3 Downranking — when the cost curve makes the *low* rank optimal

Vanilla WoW's rank ladders (§2.3) had a documented second-order failure. Flat spell-damage and
spell-healing bonuses from gear were added to a spell's output based on its cast time, largely
independent of rank. A rank-4 heal and a rank-9 heal therefore received a similar flat bonus while the
rank-4 heal cost a fraction of the mana — so at high gear levels the *low* rank was the efficient one,
and skilled players deliberately used weaker versions of their spells.

**FACT (computed).** The base numbers show why the pressure existed: Fireball's damage per mana rises
only from 0.75 at rank 1 to 1.80 at rank 11 — a ×2.4 improvement across eleven ranks — while its damage
per *cast second* rises ×13.5. A flat additive bonus large relative to the base damage therefore
flattens the mana-efficiency curve and can invert it.

**The fix took three patches and ended by deleting the ladder**
**[2nd — [warcraft.wiki.gg, Downranking](https://warcraft.wiki.gg/wiki/Downranking)]**:

| Patch | Change |
|---|---|
| **2.0.1** (Burning Crusade) | A downranking penalty was added: spells learned more than 11 levels below the caster are multiplied by `Min(1, (sLvl + 11) / cLvl)`, with a further `1 − ((20 − sLvl) × 0.0375)` for spells learned at level 20 or below |
| **3.0.2** (Wrath of the Lich King) | All spell costs became a percentage of the caster's base mana rather than a printed number |
| **4.0.1** (Cataclysm) | Downranking removed outright — spells are learned once and scale with the character's level |

```
downrank penalty (2.0.1) = Min(1, (spell learned level + 11) / caster level)
low-level penalty        = 1 − ((20 − spell learned level) × 0.0375)      for sLvl ≤ 20
```

**FACT.** The first fix was a penalty multiplier, the second replaced absolute costs with proportional
ones, and the third **abolished the rank ladder entirely**. Nine years of ranked spells ended with the
spell scaling off the character instead.

**The lesson (INFERENCE).** A rank ladder is only safe if the ladder is the *only* source of power. The
moment a flat, rank-independent bonus is added on top — gear, a global buff, a flat additive modifier —
the ladder's own cost curve is no longer what determines which rung is optimal, and a sufficiently
large flat bonus makes the cheapest rung the best one. A ladder with an escalation tax is *more*
exposed to this than one without, because its higher rungs are already worse value before the flat
bonus is applied.

### 7.4 Cost scaling too steep — the opposite failure

**FACT (computed).** This is measurable rather than anecdotal. In Dota, 13 of 29 ultimates charge more
mana than they gain in damage per level (tax > 1.0), and Lina's *Laguna Blade* charges ×3.00 mana for
×1.90 damage. Valve's compensation is visible in the same data: the cooldown falls ×0.71 across those
same ranks, so the ability is used more often even though each use is dearer **[data]**.

**INFERENCE.** Where a shipped game does charge an escalation tax, it pairs it with a *falling*
cooldown that returns the value on a different axis. No measured system charges an escalation tax on
resource and a rising cooldown at the same time.

### 7.5 The one live "cost escalates with power" mechanic found

Kassadin's *Riftwalk* is the clearest shipped example of a deliberate escalating cost, and it is worth
reading exactly: the base cost is flat at 40 mana across all three ranks, and *"each subsequent use of
this Ability within the next few seconds doubles the Mana cost"*, stacking up to a limit, with the
damage also increasing per stack **[data — Data Dragon tooltip]**. Meraki's per-cast expansion of that
gives the sequence 40 / 160 / 640 mana **[data]**.

**FACT.** The escalation is per-cast within a window, not per-rank. It exists to bound a *repeat*, not
to price a tier.

**INFERENCE.** This is the only escalating-cost mechanic found in 688 League abilities, and it is
scoped to seconds rather than to the progression ladder. Where designers use cost escalation, they use
it as a burst limiter, and they let it reset.

---

## What I could not find

**Negative findings, recorded so these searches are not repeated.**

1. **No studio publishes a power-vs-cost ratio, target or budget for a resource cost.** This was the
   primary question and the answer is a clean no, consistent with
   [`06-unsourced.md`](../game-design/06-unsourced.md) §1's finding on counter strength. Every ratio in
   §2 is measured from shipped data. If this project wants a number for how steeply cost should outrun
   power, **it is deriving it, not borrowing it.**
2. **No published cooldown-to-power formula or ratio, from anyone.** Searched across Riot's `/dev` blog
   index and numerous Ask Riot slugs (most now 404 at leagueoflegends.com — `ask-riot-balance-changes`,
   `ask-riot-ability-power`, `dev-ability-haste`, `dev-preseason-2021-item-system-and-ability-haste`),
   Blizzard's WoW and Overwatch sites and forums, Square Enix's Lodestone and job guides (403), the Dota
   2 wiki and Liquipedia, and Valve's own shipped script files. What exists instead is **qualitative
   developer statements** (§3.1), **empirical regularities nobody publishes** (Dota's ×4–5, League's
   ×10, FFXIV's ×12 oGCD-to-GCD), and **percentage-step tuning of ultimate prices** (Overwatch ±6–8%,
   Valorant ±1 point).
3. **The one power formula that could be recovered prices wind-up, not cost or cooldown** — WoW's
   spell-power coefficient (§5.3), read out of an open-source server core rather than published.
4. **No Square Enix statement on why the FFXIV global cooldown is 2.5 seconds**, or on the oGCD design
   intent. The often-quoted ~0.6–0.7s animation lock that caps weaving at two off-globals per GCD
   appears only in community sources — **treat it as community-measured, not official.**
5. **Warframe's energy and ability-cost model could not be sourced at all.** `wiki.warframe.com` returns
   Cloudflare 403, `warframe.fandom.com` returns 402, and the community API `api.warframestat.us`
   exposes no cost or cooldown fields (its ability schema is name/description/image only). Nothing about
   Warframe is asserted anywhere in this file.
6. **No complete official or datamined table of Overwatch ultimate point costs.** The figures in §3.5
   come from the community wiki and are **version-specific** — Overwatch has rescaled ultimate costs
   globally more than once, and two reachable sources disagree by ~23% on the same ability (§3.5).
   Reported global rescales of +25% (patch 1.5), +12% (1.39) and +10% (OW2 Season 9) appeared only in
   search snippets and **are not treated as sourced.**
7. **Only 19 of 29 Valorant agents' ultimate point costs were retrieved** before HTTP 429 rate limiting.
8. **Riot's own dev-blog permalinks for the Ability Haste change appear to be dead.** The formula in
   §7.2 was recovered from the Patch 10.23 notes instead, so it stands — but a parallel attempt to reach
   it through Riot's `/dev` articles 404'd on every candidate URL.
9. **The exact Press Turn Null/Repel split is not settled** (§4.3). Game8 gives Repel = 2 turns and
   Null = all remaining; the widely repeated version has them the other way round; megatenwiki gives
   neither. The values genuinely differ between entries in the series. Only the ordering is stable.
10. **Exact Grandia IP-bar numbers** (segment counts, cancel knockback distances) — GameFAQs returns
    HTTP 403 to every access method tried, and that is where the Battle FAQ lives.
11. **Persona 5 Royal's Baton Pass rank bonus percentages** (damage % and SP recovery per rank).
12. **No free transcript of Matthew Davis's GDC 2019 *Into the Breach* design post-mortem** — the GDC
    Vault listing confirms speaker, year and abstract, but the video is gated. Also **not found:** a
    citable statement that a standard Into the Breach mission is exactly five turns; Wikipedia says only
    *"a fixed number of turns."*
13. **No designer essay explicitly theorising opportunity cost.** Searched for Richard Garfield and
    Frank Lantz on the term; nothing usable. The likeliest home for a Garfield treatment is
    *Characteristics of Games* (Elias, Garfield & Gutschera, MIT Press 2012), which is not online.
    Soren Johnson searches surfaced only *"given the opportunity, players will optimize the fun out of a
    game"* — a related but different claim, about optimisation pressure rather than pricing.
14. ~~**Still open on the exploit literature:** individual Magic banning *announcements*, Hearthstone
    loop nerfs, Diablo 3 resource-cost-reduction stacking, Path of Exile mana-loop patches, and Mark
    Rosewater's post-mortems on mis-costed cards.~~ ✅ **CLOSED 2026-09-02 — the parallel thread
    returned after this file was written, and its findings are filed as
    [07-cost-mistuning-failures.md](07-cost-mistuning-failures.md).** All five items are now sourced
    there: the 1998–99 Magic ban wave with Rosewater's *"the free mechanic had the weird property of
    improving as the mana cost was raised"*; seven Hearthstone loop nerfs with patch numbers; Diablo 3's
    multiplicative CDR/RCR stacking **and why its floors do not help**; PoE's 3.15 trigger-cost manifesto
    plus the five-day 3.15.0d walk-back; and six first-party post-mortems including the MtG companion
    re-cost. **§7 below remains the short version — read 07 for the long one.** What is still genuinely
    unsourced is narrower and recorded in that file's own §8 (Skullclamp's development story, the
    1 Mar 1999 DCI announcement, LoL's "45% cap" claim, Slay the Spire 1's infinites).

**Access notes for follow-up**, worth keeping alongside `06-unsourced.md` §2:

- **`fandom.com` returns HTTP 402 to direct fetches but IS reachable through the `r.jina.ai` reader
  proxy.** That is how the Overwatch ultimate-cost table and the FFX Rank table were recovered.
- **`wiki.gg` is inconsistent.** `warcraft.wiki.gg`, `slaythespire.wiki.gg` and `darkestdungeon.wiki.gg`
  served fine; `overwatch.wiki.gg` and `finalfantasy.wiki.gg` return HTTP 401 both directly and through
  the proxy.
- **`megatenwiki.com` returns 403 to a direct fetch and a bot-verification interstitial through the
  proxy**, but was reachable from a real browser.
- **Liquipedia blocks HTML scraping but serves its Cargo/MediaWiki API normally** — same note as
  `06-unsourced.md`. `dustloop.com` and `wavu.wiki` both expose Cargo APIs, and that is the right way to
  pull fighting-game frame data in bulk.
- **The Slay the Spire wiki's Ironclad card *list* page misprints energy costs** (Bash and Searing Blow
  listed as 1; both are 2). Use per-card pages, or the datamined JSON.
- **Data Dragon's `effect` arrays are zeroed for most modern League abilities** — 251 of 688 still carry
  values, and no version back to 9.24.2 restores them. Per-rank League damage must come from Meraki
  Analytics or CommunityDragon.
- **XIVAPI's v1 search endpoint is down** (`No alive nodes found in your cluster`); v2 works. The
  `ffxiv-datamining` CSVs have moved to a per-language path — `csv/en/Action.csv`, not `csv/Action.csv`.
- Also 403 or refused here: `supercombo.gg`, `gamefaqs.gamespot.com`, `us.forums.blizzard.com` HTML,
  `wiki.leagueoflegends.com`, `na.finalfantasyxiv.com`, `subsetgames.com`, and `web.archive.org`
  playback.

---

## Data sources

Machine-readable, pulled directly for this pass. Every computation in this file is reproducible from
these.

| Dataset | Endpoint | Used for |
|---|---|---|
| Riot Data Dragon 15.24.1 **[1st]** | `ddragon.leagueoflegends.com/cdn/15.24.1/data/en_US/championFull.json` | League per-rank costs and cooldowns, resource-bar tally, max-rank sums |
| CommunityDragon **[1st]** | `raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champions/<id>.json` | League `maxAmmo` / `ammoRechargeTime` charge data |
| Meraki Analytics **[data]** | `cdn.merakianalytics.com/riot/lol/resources/latest/en-US/champions.json` | League per-rank base damage |
| Valve hero scripts **[1st]** | `github.com/dotabuff/d2vpkr` — `dota/scripts/npc/heroes` | Dota `AbilityType`, cooldowns, cast points |
| OpenDota constants **[data]** | `raw.githubusercontent.com/odota/dotaconstants/master/build/abilities.json`, `.../hero_abilities.json` | Dota per-level damage, mana, cooldown |
| RePoE **[data]** | `raw.githubusercontent.com/brather1ng/RePoE/master/RePoE/data/gems.min.json` | Path of Exile per-gem-level mana and base damage, cast times |
| FFXIV `Action` sheet **[1st, datamined]** | `raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/en/Action.csv`; `v2.xivapi.com/api/sheet/Action` | FFXIV cost types, MP values, recast, cooldown groups, cast time, charges |
| Scryfall bulk `oracle_cards` **[1st]** | `api.scryfall.com/bulk-data` | Magic creature mana value vs power/toughness (16,960 cards) |
| HearthstoneJSON **[data]** | `api.hearthstonejson.com/v1/latest/enUS/cards.json` | Hearthstone minion cost vs attack/health (4,708 cards) |
| Guild Wars 2 API v2 **[1st]** | `api.guildwars2.com/v2/professions/Thief`, `/v2/skills` | Thief initiative costs and damage coefficients |
| Dustloop Cargo API **[data]** | `dustloop.com/wiki/api.php?action=cargoquery&tables=MoveData_GGST` | 1,600 Guilty Gear Strive moves — startup, recovery, damage, on-block |
| Wavu Cargo API **[data]** | `wavu.wiki/t/Special:CargoExport?tables=Move` | 1,686 Tekken 8 moves — startup, damage, block advantage |
| Slay the Spire card dump **[data]** | `github.com/nkhoit/spire-archive` — `data/sts1/cards.json` | Card cost, damage, hit count, rarity (360 cards) |
| cmangos vanilla server core **[data]** | `github.com/cmangos/mangos-classic` — `src/game/Spells/SpellMgr.cpp` | The WoW spell-power coefficient formula |
| Wowhead Classic spell records **[2nd]** | `wowhead.com/classic/spell=133/fireball` plus the 11 further rank IDs | Vanilla WoW Fireball rank ladder |

**First-party text sources:**
[Ask Riot: Manaless Champions](https://www.leagueoflegends.com/en-us/news/dev/ask-riot-manaless-champions/) ·
[League patch 10.23 notes](https://www.leagueoflegends.com/en-us/news/game-updates/patch-10-23-notes/) ·
[Overwatch patch notes](https://overwatch.blizzard.com/en-us/news/patch-notes/) ·
[WoW API: GetSpellCharges](https://warcraft.wiki.gg/wiki/API_GetSpellCharges) ·
[Wizards banned & restricted list](https://magic.wizards.com/en/banned-restricted-list) ·
[Reid Duke, *Tempo*](https://magic.wizards.com/en/news/feature/tempo-2014-09-22) ·
[Mark Rosewater, *Ten Things Every Game Needs*](https://magic.wizards.com/en/news/making-magic/ten-things-every-game-needs-part-1-2011-10-24) ·
[Bulmahn, *All About Actions*](https://paizo.com/community/blog/v5748dyo5lklh) ·
[Seifter, *All About Spells*](https://paizo.com/blog/all-about-spells) ·
[D&D 2024 free rules glossary](https://www.dndbeyond.com/sources/dnd/free-rules/rules-glossary) ·
[5e 2024 SRD, Actions](https://5e24srd.com/playing-the-game/actions.html)

**Second-tier:** [warcraft.wiki.gg Base mana](https://warcraft.wiki.gg/wiki/Base_mana) ·
[Global cooldown](https://warcraft.wiki.gg/wiki/Global_cooldown) ·
[Downranking](https://warcraft.wiki.gg/wiki/Downranking) ·
[Pushback](https://warcraft.wiki.gg/wiki/Pushback) ·
[megatenwiki Press Turn System](https://megatenwiki.com/wiki/Press_Turn_System) ·
[PF2 SRD](https://pf2.d20pfsrd.com/rules/playing-the-game/) ·
[Archives of Nethys, *heal*](https://2e.aonprd.com/Spells.aspx?ID=1554) ·
[UFOpaedia, Gameplay Mechanics (EU2012)](https://www.ufopaedia.org/index.php/Gameplay_Mechanics_(EU2012)) ·
[darkestdungeon.wiki.gg](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon)) ·
[slaythespire.wiki.gg, Mechanics](https://slaythespire.wiki.gg/wiki/Mechanics) ·
[fireemblemwiki.org, Rankings](https://fireemblemwiki.org/wiki/Rankings) ·
[Final Fantasy Wiki, Rank (FFX)](https://finalfantasy.fandom.com/wiki/Rank_(Final_Fantasy_X)) ·
[FFX-Info CTB datamine](https://grayfox96.github.io/FFX-Info/game-mechanics/ctb) ·
[Liquipedia Valorant agents](https://liquipedia.net/valorant/Jett) ·
[Overwatch wiki, Ultimate ability](https://overwatch.fandom.com/wiki/Ultimate_ability)

**Third-tier:** [Game8 SMT V](https://game8.co/games/Shin-Megami-Tensei-V/archives/348265) ·
[Game8 SMT III](https://game8.co/games/Shin-Megami-Tensei-III-Nocturne/archives/332175) ·
[Ask Mr. Robot on the BfA GCD](https://blog.askmrrobot.com/battle-for-azeroth-gcd-change/) ·
[PCGamesN](https://www.pcgamesn.com/world-of-warcraft/world-of-warcraft-battle-for-azeroth-global-cooldown) ·
[Game Developer on Sid Meier](https://www.gamedeveloper.com/design/gdc-2012-sid-meier-on-how-to-see-games-as-sets-of-interesting-decisions) ·
[rpg-o-mania on Grandia](https://www.rpg-o-mania.com/coverage_battlesystems_grandia.php)
