# Typing and counter matrices — real values

Captured 2026-09-01. All Pokémon figures **computed** from PokéAPI `damage_relations`, cross-validated
against the reciprocal defending-side lists (0 mismatches).

---

## 1. Pokémon — the only shipped 18-type matrix

### Single-type matrix, 18 × 18

| Multiplier | Cells | Share |
|---|---:|---:|
| 0× (immune) | **8** | 2.47% |
| 0.5× (resisted) | **61** | 18.83% |
| 1× (neutral) | 204 | 62.96% |
| 2× (super-effective) | **51** | 15.74% |
| **Non-neutral** | **120** | **37.0%** |

**A player memorises 120 facts, not 324** — 6.7 non-neutral entries per attacking type.

The 8 immunities: Normal→Ghost · Fighting→Ghost · Poison→Steel · Ground→Flying · Ghost→Normal ·
Electric→Ground · Psychic→Dark · Dragon→Fairy.

**11 of 18 self-matchups are non-neutral** — 9 types resist themselves; Ghost and Dragon are 2× on
themselves. The diagonal is not free space.

### Density is a design constant across every expansion

| Gen | Types | Cells | Non-neutral | Density | Combos |
|---|---:|---:|---:|---:|---:|
| I | 15 | 225 | 82 | 36.4% | 120 |
| II–V | 17 | 289 | 110 | 38.1% | 153 |
| VI–IX | 18 | 324 | **120** | **37.0%** | 171 |

Five types added across nine generations; **density never left 36–38%**. A 19th type would add 37
cells (~14 non-neutral) plus 19 combinations.

### Dual typing is the depth engine

Effectiveness is the **product** of each type's multiplier; immunity dominates. Ladder:
**0 / 0.25 / 0.5 / 1 / 2 / 4**.

Full 18 × 171 space = **3,078 cells**:

| Multiplier | Cells | Share |
|---|---:|---:|
| 0× | 144 | 4.68% |
| 0.25× | 97 | 3.15% |
| 0.5× | 697 | 22.64% |
| 1× | 1,489 | 48.38% |
| 2× | 590 | 19.17% |
| 4× | 61 | 1.98% |
| **Non-neutral** | **1,589** | **51.6%** |

**120 authored facts → 1,589 non-neutral interactions. A 13× expansion at zero authoring cost.**

*Legends: Arceus* deliberately **compresses the tail**: super-effective-against-both is **2.5×** (not
4×), resisted-by-both **0.4×** (not 0.25×). A designer deciding the product rule was too swingy.

### Combination space is nearly exhausted

`N + N(N−1)/2` = **171** for N=18. **162 used as of Gen IX (94.7%)**, after ~1,350 species.

The nine unused: Bug/Dragon · Bug/Normal · Fairy/Fire · Fairy/Ground · Ghost/Rock · Ice/Normal ·
Ice/Poison · Normal/Rock · Normal/Steel.

**They are declined on flavour, not mechanics** — Normal/Steel is a top-3 *defensive* typing and has
never been used. The cluster around Normal suggests thematic exclusion. **More types would not help.**

### Asymmetry is half the matrix

Over 153 unordered pairs: **78 symmetric (51%), 75 asymmetric (49%)**.

- **30** clean two-way counters (2× one way, ≤0.5× back)
- **19** one-sided advantages — 2× one way, **1× back**. e.g. Fighting→Steel, Ground→Fire,
  Ice→Dragon, Bug→Dark, Ghost→Psychic
- Psychic→Dark is 0× while Dark→Psychic is 2× — total one-way domination

**Asymmetry is what lets a type be a great attacker and a poor defender.** Ice: 4 super-effective
targets, 4 weaknesses, 1 resistance (worst defensive type). Steel: 3 targets, 10 resistances, 1
immunity. **A symmetric matrix cannot express that**, and it is cited as the main reason Pokémon
roster-building has depth.

---

## 2. The RTS matrices, with real values

### StarCraft I — 3 × 3, from the engine

From OpenBW `weapon_deal_damage` (fixed-point, 256 = 1.0). Blizzard's own page:
`classic.battle.net/scc/GS/damage.shtml`

| Damage type ↓ / Size → | Small | Medium | Large |
|---|---:|---:|---:|
| Normal | 100% | 100% | 100% |
| **Concussive** | 100% | **50%** | **25%** |
| **Explosive** | **50%** | **75%** | 100% |

**9 cells, 4–5 non-neutral.** Buildings count as Large. **Protoss shields always take 100%**
regardless of type or size.

Order of operations (the part most guides get wrong): hallucination ×2 → splash divisor → acid spores
→ floor 0.5 → Defensive Matrix → **shields absorb** → armour subtracted → **only now the size
multiplier** → floor 0.5.

Only three units deal Concussive: Firebat, Ghost, Vulture.

### Warcraft III — 7 × 6, the largest fixed matrix any surveyed game shipped

**The Frozen Throne** (Blizzard's `classic.battle.net/war3/basics/armorandweapontypes.shtml`):

| Attack ↓ / Armor → | Light | Medium | Heavy | Fortified | Hero | Unarmored |
|---|---:|---:|---:|---:|---:|---:|
| Normal | 100% | **150%** | 100% | **70%** | 100% | 100% |
| Pierce | **200%** | **75%** | 100%* | **35%** | **50%** | **150%** |
| Siege | 100% | **50%** | 100% | **150%** | **50%** | **150%** |
| Magic | **125%** | **75%** | **200%** | **35%** | **50%** | 100% |
| Chaos | 100% | 100% | 100% | 100% | 100% | 100% |
| Spells | 100% | 100% | 100% | 100% | **70%** | 100% |
| Hero | 100% | 100% | 100% | **50%** | 100% | 100% |

**42 cells, 18–19 non-neutral (43–45%), 8 distinct multiplier values.**

\* Blizzard's page says 100%; Liquipedia says 90%. **Resolved:** Reforged **Patch 2.0.3** (live
2025-07-17) — *"Piercing damage against Heavy armor decreased from 100% to 90%."* The matrix is still
being tuned 22 years later.

**Reign of Chaos was different, and not only numerically** — Heavy/Medium/Light were *rotated*, and
**unit armour assignments moved** (basic melee was Medium in RoC, Heavy in TFT). Porting a RoC balance
sheet required re-tagging units, not re-numbering the table.

Per-row density is very uneven: Piercing 6/6 non-neutral, Magic 5/6, Siege 4/6, Normal 2/6, Hero 1/6,
Spell 1/6, **Chaos 0/6** — Chaos exists to *opt out* of the matrix.

A separate continuous armour-rating layer sits on top:
`reduction = 0.06A / (1 + 0.06A)`; negative armour amplifies as `2 − 0.94^A`. Exactly linear effective
HP, 6% per point, forever.

**7th armour type, Divine** (campaign only): 5% from everything except Chaos, 50% from spells.

**Naming traps:** in `common.j`, `ATTACK_TYPE_NORMAL` is the in-game **"Spells"** and
`ATTACK_TYPE_MELEE` is the in-game **"Normal"**; `DEFENSE_TYPE_LARGE` is "Heavy". On the unit,
`Armor Type` is a *sound* class while `Defense Type` is the matrix input.

### StarCraft II — not a matrix at all

**8 attributes**: Light, Armored, Massive, Biological, Mechanical, Psionic, Structure, Heroic.
Every unit is Light **or** Armored (exceptions: Archon, Ghost, Ravager, Baneling, Queen, Cocoon —
**none**), Biological **or** Mechanical (exceptions: Archon — neither; SCV and Hellbat — **both**).

Weapons carry `+N vs <attribute>` riders. **Additive, one-sided, sparse — there is no penalty cell.**
All 22 shipped bonuses:

| Ratio | Unit |
|---|---|
| ×2.50 | Immortal +30 vs Armored |
| ×2.20 | Adept +12 vs Light |
| ×2.00 | Marauder +10 vs Armored · Ghost +10 vs Light · Phoenix +5 vs Light · Thor AA +6 vs Light |
| ×1.75 | Siege Tank (sieged) +30 vs Armored · Hellion +6 vs Light |
| ×1.73 | Tempest +22 vs Massive |
| ×1.67 | Siege Tank +10 vs Armored · Viking +8 vs Mechanical |
| ×1.50 | Colossus +5 vs Light · Lurker +10 vs Armored · Spore Crawler +10 vs Biological |
| ×1.20 | Spine Crawler +5 vs Armored |

**Blizzard shipped a self-imposed band and never left it: no ×3, no ×0.5, one attribute per weapon.**
51 of 73 weapons have **no** bonus at all, and only 5 of 9 live attributes are ever targeted.

Two levers people forget: **upgrades scale the bonus too** (Siege Tank +4 base and +1 to its Armored
bonus per level), so counters strengthen over a game; and armour is subtracted per hit, so 3 armour
costs a Marine **50%** of its DPS and a Colossus **20%**.

### Age of Empires II — 38 armour classes, summed not maxed

Attack and armour are both `vector<AttackOrArmor>` of `(Class, Amount)`. Melee and pierce armour are
not fields — they are entries with class 4 and 3.

```
Damage = max( 1, Σ_i max( Attack_i − Armor_i , 0 ) )
```

A class a unit does not carry is treated as armour **10,000**, so unmatched terms zero out.

**38 classes defined, highest id 60**, 28 unit / 7 building / 3 gaia. Several dead or unused.

Real values: Halberdier 6 melee, **+32 vs cavalry**, +28 vs elephant, +26 vs camel → vs War Elephant:
`(6−1) + (28−0) + (32−0)` = **65**. Elite Skirmisher pierce armour 4 vs an Archer's 4 pierce =
`max(1, 0)` = **1 damage**.

Negative armour is a used lever — Elephant Archer has **−4** cavalry-archer armour.

### Age of Empires IV — the matrix was deliberately removed

4 armour types (melee, ranged, fire, siege — **no unit has siege armour > 0**, so effectively 3), 4
damage types, integers ≥ 0, no negative armour.

> *"Unlike in Age of Empires II, attack bonuses do not interact with separate armor classes, and are
> affected by standard melee and ranged armor just like base attack damage."*

### Command & Conquer — a positional vector, not a matrix

`Verses=` position *i* **is** armour type *i*. No keys, no defaults.

- **TD / RA / TS — 5 types**: `none, wood, light, heavy, concrete` (confirmed from EA's GPL
  `TIBERIANDAWN/DEFINES.H`: `ARMOR_NONE, ARMOR_WOOD, ARMOR_ALUMINUM, ARMOR_STEEL, ARMOR_CONCRETE`).
  ModEnc's own page contradicts this; **the source settles it.**
- **RA2 / YR — 11 types**: `none, flak, plate, light, medium, heavy, wood, steel, concrete,
  special_1, special_2`. Shipped example: `Verses=100%,45%,30%,15%,10%,50%,80%,150%,10%,5%,5%`

Parsed by an unvalidated `strtok` loop — **a short list silently reads past the end.** And the values
double as targeting rules: **0%** = invalid target, **1%** = will not auto-acquire, **negative** =
heals. A balance tweak changes what a unit may shoot at.

### Company of Heroes — no matrix; probability and interpolation

```
accuracy     = interp(dist; near, mid, far) × moving × cover × target_table
hit_chance   = min(accuracy × target_size, 1)
pen_chance   = min(penetration / armor, 1)      // < 0.03 → autobounce
pen_dmg_mult = pen_chance + deflection_mult × (1 − pen_chance)
dps          = (rpm/60) × hit_chance × pen_dmg_mult × damage × weapon_count
```

Each weapon carries its **own private** `target_type_table` — 1,222 override rows across 1,080
weapons over 20 `unit_type` values. Entity tags number **191**, mixing categories, behaviour flags and
individual vehicle model names. `armor_type` exists as a field and is **unset on all 1,297 entities**.

Per-face armour (front/side/rear) exists here and nowhere else surveyed. `target_size`: Riflemen 1.0,
sniper 1.33, Sherman 20.0, Tiger 26.0.

### Dawn of War — two different systems

**DoW1 (2004): no compact matrix.** Every weapon carries its own armour-penetration percentage
against each of **15 armour classes**. `DPS = ((min+max)/2 × (AP/100) × accuracy) / reload`.

**DoW2: a genuine matrix, 27 damage types × 10 armour classes = 270 cells**, range **0.01× to 5.0×** —
two orders of magnitude wider than WC3's 0.35–2.00.

### Total War — collapsed to two categories

`melee_weapons_tables` and `projectiles_tables` carry exactly **`bonus_v_infantry`** and
**`bonus_v_large`**. Attila had five. **CA collapsed its anti-X matrix to one boolean.**

```
Chance to hit = (35 + (Melee attack − Melee defence))%      // clamped 8–90%
Total damage  = base + AP damage
Mitigated     = base × (100 − armour × random(0.5, 1))%     // armour capped at 100
```

⚠️ **CA has never published these.** The `random(0.5, 1)` term is community reverse-engineering the
wiki itself flags as possibly incorrect.

**Armour walls out**: effective HP rises hyperbolically toward 200 armour, which is *why*
`ap_damage` exists as a separate channel — and **every attack in the game now carries some AP**, so
the split stopped being a choice and became a tax.

---

## 3. Fire Emblem — the smallest working counter system

Triangle effect by game (advantaged +X / disadvantaged −X):

| Game | Effect |
|---|---|
| FE4 | Hit **±20%** only |
| FE5 | Hit **±5%** only |
| FE6 | Hit ±10%, **Might ±1** |
| **FE7 / FE8** | Hit **±15%**, **Might ±1** |
| FE9 / FE10 | Hit ±10%, Might ±1 |
| FE13 Awakening | rank-scaled, to ±15 Hit / ±1 Atk at A |
| **FE14 Fates** | rank-scaled, to **±20 Hit / ±2 Atk at S** |
| FE15 Echoes | **no triangle at all** |
| **FE16 Three Houses** | **no triangle** — opt-in Breaker skills at rank B (+20 Hit/+20 Avoid) |
| **FE17 Engage** | **no hit or damage modifier** — advantage inflicts **Break** (cannot counterattack) |
| FEH | Attack **±20%**, multiplicative |

**FE6–FE10 apply the ±1 to Weapon Might**, so it is multiplied by effectiveness coefficients. From
FE11 it applies to Attack directly.

**Reaver weapons invert the triangle**, and in FE7/FE8/Fates also **double** it (±2 Mt / ±30 Hit).

**Fates unified the triangle** — the only mainline game to fold bows, daggers and tomes in. 6
categories in 3 colours of 2: Red (Sword, Tome), Green (Axe, Bow), Blue (Lance, Dagger). Red > Green >
Blue > Red; within a colour, neutral. **36 cells: 12 advantage, 12 disadvantage, 12 neutral.**

**Engage adds a one-directional arm**: Arts > Tomes, Bows and Daggers, with **no return edge**.
Break duration: through the combat plus one further round. Armored units are immune to triangle-Break.
Non-triangle Break chances: `Cataclysm` 20%, `Override` 10% (Armored) / 20% (Qi Adept).

---

## 4. Genshin — the reaction system as the alternative to a matrix

**7 elements. No element-vs-element matrix exists.** Enemies carry independent per-element RES; all
interaction is expressed as reactions.

**C(7,2) = 21 unordered pairs. 17 react, 4 are inert** (Cryo–Dendro, Anemo–Dendro, Geo–Dendro,
Anemo–Geo). **81% dense**, against Pokémon's 37%.

Anemo and Geo **cannot hold an aura**, so realistic ordered cells = 5 auras × 6 triggers = **30**, of
which **26 are productive** — covered by **16 named reactions**, i.e. **1.6 cells per name**. Swirl
alone covers 4 cells; Crystallize another 4.

**Amplifying multipliers** — and note the asymmetry:

| Reaction | Trigger | Multiplier |
|---|---|---|
| Melt | Pyro onto Cryo | **2.0×** |
| Melt | Cryo onto Pyro | **1.5×** (reverse) |
| Vaporize | Hydro onto Pyro | **2.0×** |
| Vaporize | Pyro onto Hydro | **1.5×** (reverse) |

**The 1.5× direction is the sustainable one** — it consumes less aura, often preserving it.

**Transformative base coefficients:** Burning 0.25 · Swirl 0.6 · Superconduct 1.5 · Electro-Charged
2.0 · Bloom 2.0 · Overloaded 2.75 · Burgeon 3.0 · Hyperbloom 3.0 · Shatter 3.0. Additive: Aggravate
1.15 · Spread 1.25.

**Elemental Mastery is five distinct saturating curves**, not one stat:

```
A (Vaporize, Melt)          = 2.78 × EM/(EM+1400)
B (most transformative)     = 16   × EM/(EM+2000)
C (Aggravate, Spread)       = 5    × EM/(EM+1200)
D (Lunar reactions)         = 6    × EM/(EM+2000)
E (Crystallize shield)      = 4.44 × EM/(EM+1400)
```

**The hidden layer is where the real complexity lives** — and none of it is shown in-game:
gauge units (1 / 1.5 / 2 / 4 / 8), an **0.8× aura tax**, duration `2.5x + 7`, decay
`35/(4x) + 25/8` (11.875 s per unit at 1U falling to 4.22 at 8U), reaction consumption coefficients
(0.5 / 1 / 2), and per-ability ICD (standard **2.5 s or 3 hits**, with dozens of bespoke exceptions).

**Counter-intuitive and worth stealing:** a bigger gauge decays *faster per unit*, so applying a weak
1U aura first and then a 4U of the same element inherits the slow decay rate — 38 s instead of 17 s.
**Pyro is the sole exception.**

---

## 5. Divinity: Original Sin 2 — surfaces, and the armour gate

**56 surface/cloud entries**, generated by ~11 base materials × 2 phases × 3 blessings, minus 5
ineligible (Lava, Source, Explosion Cloud, Frost Explosion, Deathfog). **22 base + 17 blessed + 17
cursed.** ~16 explicit transformation rules + 2 meta-rules generate all 56.

**The armour gate**: Physical and Magic Armour must reach **0** before any matching status can land.
Exceptions: Piercing damage always hits Vitality; cursed poison and cursed blood are resisted by
*physical* armour; Web, Cursed Oil, Cursed Ice and the Torturer talent bypass armour entirely.

Status gating counted over 70 negative statuses: **20 gated by Magic Armour, 12 by Physical, 33
ungated**.

**Resistance is linear, unbounded and sign-flipping**: *"For every number past 100%, a character will
instead be healed by the attack."* The opposite of Genshin's saturating piecewise multiplier.

**Larian removed the system in 2026.** See [05-failure-modes.md](05-failure-modes.md).

---

## 6. Magic: The Gathering — a permission system, not a damage matrix

**No rule makes White deal more damage to Black.** The colour pie is a design-time permission ladder;
effects are graded primary / secondary / tertiary per colour. 5 colours, each with exactly **2 allies
and 2 enemies**; 2⁵ = 32 subsets.

Rosewater on why five: *"An odd number helps keep it balanced — colors have the same numbers of allies
as enemies."* **Five is the smallest N where every node in a cycle has an equal, non-zero count of
allies and enemies** (at N=4 each node has 2 allies and 1 enemy).

~79 documented "colour pie breaks". Hoser policy is now capped at five per set. Purple was actually
playtested for *Planar Chaos* and abandoned.

And the designer's own limit on the tool: *"The main purpose of the color pie is to make colors feel
and play differently. **It is not the best tool for balancing power.**"*

---

## 7. Monster Hunter — no element matrix at all

**5 elements, no element-vs-element relation whatsoever.** Effectiveness is a **per-monster,
per-hitzone lookup**: each hitzone stores 8 values (Sever, Blunt, Ranged + Fire, Water, Thunder, Ice,
Dragon) on a 0–100+ scale. Rathalos has 10 hitzone rows and is **immune to Fire on every part**.

`Element damage = (Displayed Element / 10) × Sharpness × Hitzone% × QuestDifficulty × Rage` — **not**
multiplied by Motion Value, weapon type or Affinity.

~5,700 hand-authored values for MHW+Iceborne's 71 large monsters (derived: 10 rows × 8 columns × 71).

**The property this buys and no matrix can:** Capcom **anti-correlates the physical and elemental
hitzone maps on the same monster**, so "which part do I hit" stays a live trade-off.

⚠️ Fextralife's "Fire beats Ice" charts for MH are **editorial flavour text with no mechanical
backing.** Do not port them as a matrix.

---

## 8. Comparison

| Game | Types | Shape | Cells | Non-neutral | Symmetric? | Dual? | Values |
|---|---:|---|---:|---:|---|---|---|
| FE (GBA) | 3 | symmetric cycle | 9 | 6 (67%) | Yes | No | ±1 Mt, ±15 Hit |
| FE Engage | 3 + one-way arm | cycle + tier | — | — | **No** | No | **Break status, no modifier** |
| StarCraft I | 3 × 3 | matrix | 9 | 4–5 | N/A | No | 25/50/75/100% |
| MTG | 5 colours | permission | — | — | Yes | Yes (31) | **none — not multipliers** |
| Monster Hunter | 5 | **per-hitzone** | ~5,700 authored | — | N/A | No | 0–100+ hitzone % |
| **Genshin** | **7** | **reactions** | 21 pairs | **17 (81%)** | **No** (order matters) | aura+trigger | 1.5–3.0 coefficients |
| Divinity OS2 | ~7 | surfaces + gate | — | — | No | No | % resist, **armour gate** |
| **Warcraft III** | 7 × 6 | matrix | **42** | **18–19 (45%)** | N/A | No | 35–200%, 8 values |
| StarCraft II | 8 attributes | **riders** | — | 22 sparse | N/A | Yes | **additive only, ×1.2–2.5** |
| AoE II | 38 classes | **summed membership** | — | — | N/A | Yes (many) | flat, stacking |
| Dawn of War II | 27 × 10 | matrix | **270** | — | N/A | No | **0.01×–5.0×** |
| **Pokémon** | **18** | matrix | **324** | **120 (37%)** | **No — 49% of pairs** | **Yes, 162/171** | 0/0.5/1/2 → 0.25/4 |
