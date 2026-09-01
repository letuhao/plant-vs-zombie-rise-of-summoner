# What fields does a unit carry?

Consolidated across 7 RTS families and 9 RPG systems, 2026-09-01.
**(U)** universal · **(C)** common, 3+ families · **(R)** rare, 1–2 families.

---

## 1. The RTS checklist

### Identity
| Field | | Notes |
|---|---|---|
| Unique id, display name, description, icon | U | |
| Race / faction / culture | U | |
| Unit class (taxonomic, ≠ armour class) | C | AoE2 `Class`, TW `class`+`category`+`caste` |
| **Attribute / tag list** | C | SC2 `Attributes[]` (8–11) · CoH3 `unit_type_list` (**191 tags**) · TW `attribute_group` (**66**) |

### Vitals
| Field | | Notes |
|---|---|---|
| Max HP | U | SC1 stores it **×256** as fixed-point |
| HP regen rate **and regen type** | C | WC3 has a type enum (always / night / blight) |
| **Shields as a second pool** | R | SC1/SC2 only. Archon is 10 HP / 350 shields |
| Shield regen rate **+ delay** | R | SC2 |
| Energy / mana max, start, regen | C | SC2, WC3 |
| **Morale / leadership** | R | Total War only |
| **Fatigue** | R | Total War only |
| **Ammunition count** | R | TW, C&C |

### Defence
| Field | | Notes |
|---|---|---|
| Armour value | U | |
| **Armour class / type** | C | **Absent in CoH3 and AoE4** |
| Armour upgrade link | C | |
| **Per-face armour (front/side/rear)** | R | **CoH only** — 873 uniform, 361 front/rear/side, 62 front/rear |
| **Target size** (accuracy multiplier) | R | **CoH only.** Riflemen 1.0 · sniper 1.33 · Sherman 20.0 · Tiger 26.0 |
| Melee defence / evasion | R | TW |
| Per-damage-type resistances | R | TW `damage_mod_flame/magic/physical/missile` |
| Conversion resistance | R | AoE2 |

### Offence
| Field | | Notes |
|---|---|---|
| Damage amount | U | |
| **Damage as dice** (base + R dice of M sides) | R | **WC3 only.** All heroes use 2 dice (triangular) |
| **Damage type** | C | SC1 (5) · WC3 (7) · C&C warhead · CoH3 (5) · AoE4 (4) |
| **AP damage as a second additive channel** | R | Total War |
| Bonus damage vs class/attribute | C | |
| **Armour reduction** (subtract N from target armour) | R | SC2 — Immortal, Hellbat, Tempest, Adept |
| **Attack cooldown / rate of fire** | **U** | |
| Attacks per shot | C | Colossus 10×2 |
| **Max range** | **U** | |
| Minimum range | C | AoE2, SC1, SC2, C&C, TW, AoE4 |
| **Range bands near/mid/far** | R | **CoH** — accuracy, penetration, cooldown, reload, aim and AoE each a near/mid/far triple, linearly interpolated |
| Accuracy percent | C | |
| **Scatter geometry** | R | **CoH has 21 scatter fields**; TW has spread, marksmanship_bonus, calibration_distance/area |
| **Penetration as a probability** | R | CoH only |
| Splash radii + per-ring factor | C | SC1 inner/median/outer; WC3 full/medium/small |
| Windup / damage point / backswing | C | WC3, SC2, AoE2 `FrameDelay`, CoH |
| Moving-fire modifiers | C | |
| Two weapons (ground + air) | C | SC1, SC2, WC3, C&C |
| Charge bonus | R | TW; AoE4 with Spearwall negation (2.5 s stun) |

### Targeting
| Field | | Notes |
|---|---|---|
| **Targets-allowed mask** | **U** | SC1 9-bit · **SC2 mandatory on 57/57 weapons** · WC3 · OpenRA |
| **Target priority** | C | WC3 `Priority` 0–20 · SC2 uses only 4 values (10 eggs / 11 production / 19 defensive / 20 army) · C&C `ThreatPosed` + 7 coefficients |
| Acquisition range, separate from weapon range | C | |
| Auto-acquire / retaliate / guard-mode flags | C | |

### Movement and space
| Field | | Notes |
|---|---|---|
| **Speed** | **U** | **SC1 puts speed/accel/turn on a shared `flingy.dat` archetype, not the unit** |
| Acceleration / deceleration | C | Absent in WC3 |
| Turn rate, turn radius | C | |
| **Movement class / locomotor** | **U** | WC3 Foot/Horse/Fly/Hover/Float/Amphibious |
| Collision size / footprint | U | SC2 has Radius, InnerRadius, SeparationRadius, MinimapRadius, Footprint, PlacementFootprint, DeadFootprint |
| Collision *classes* it collides with | R | SC2 `Collide[]` — 12 indices incl. Colossus, ForceField, Larva |
| Mass | R | TW, SC2 |
| Crushable / crusher | R | C&C, OpenRA |
| Formation rank depth / spacing | R | TW |

### Vision
| Field | | Notes |
|---|---|---|
| **Sight radius** | **U** | |
| Day/night sight split | R | WC3 |
| Detector flag | C | SC2 implements it as a **named behavior**, not a flag |
| Cloak / camo + detection distance | C | |
| Sight **cone angle** | R | CoH |
| Per-terrain spotting distance | R | TW |

### Economy
| Field | | Notes |
|---|---|---|
| Resource costs (up to 3 slots) | U | AoE2 has a "paid vs merely required" flag |
| **Supply / population cost** | C | SC1 stores it in halves · SC2 `Food` is negative · **AoE2 has no pop field** (it is a resource slot) · TW has none |
| Build / train time | U | **In SC2 this lives on the producing building's train ability, not the unit** |
| Tech prerequisite | U | **AoE2: not on the unit record at all** |
| Build limit / cap | C | |
| Upkeep | R | TW, CoH |

### Squad / progression
| Field | | Notes |
|---|---|---|
| Cargo size required / provided | C | SC2 Hellbat costs 4; SC1 255 = untransportable |
| **Squad as a list of entities** | R | CoH `unit_list[]` — mixed-model squads fall out free |
| Reinforce cost as **% of entity cost** | R | CoH |
| XP / bounty on kill | C | SC2 has KillXP + DamageDealtXP + DamageTakenXP |
| Veterancy ranks | R | CoH (3 ranks × 651 squads), C&C |
| AI role hint flags | C | SC2 `AIThreatGround/AIThreatAir/AISplash/AIDefense/AISupport/AICaster/AIHighPrioTarget` |

**Presentation fields dominate the count.** In C&C's `TechnoTypes` a large fraction of ~330 keys is
audio. In TW's `land_units_tables` only **~15 of 62 columns are combat**.

### The structural observation

**The big engines decompose; they do not inline.** C&C splits unit → weapon → warhead → projectile as
four independently-named tables. CoH splits weapon → entity → squad. Total War splits across ~8 joined
tables. AoE2 layers by type. **Only Warcraft III inlines the weapon into the unit (Attack 1 / Attack
2), and pays with two complete duplicate field blocks.**

Total War historically fixed its own version of this: `unit_stats_land_tables` was a **113-column
monolith** through Shogun 2, decomposed into ~8 normalised tables from Rome 2 onward.

---

## 2. The RPG creature checklist

### Arknights — the fullest formalized schema found

Top-level keys on an operator record:
```
name, appellation, description, trait, position, profession, subProfessionId,
rarity, nationId, groupId, teamId, tagList, maxPotentialLevel, potentialRanks,
talents, skills, allSkillLvlup, phases, favorKeyFrames, displayNumber,
isNotObtainable, isSpChar, mainPower, subPower, itemUsage, itemDesc
```

Per-phase numeric block — **26 fields**:
```
maxHp, atk, def, magicResistance, cost, blockCnt, moveSpeed, attackSpeed,
baseAttackTime, respawnTime, hpRecoveryPerSec, spRecoveryPerSec,
maxDeployCount, maxDeckStackCnt, tauntLevel, massLevel, baseForceLevel,
stunImmune, silenceImmune, sleepImmune, frozenImmune, levitateImmune,
disarmedCombatImmune, fearedImmune, palsyImmune, attractImmune
```

Three things worth stealing:

- **Eleven status-immunity booleans live in the stat frame**, not as statuses.
- **`tauntLevel` and `massLevel` are first-class** — aggro and weight (for shift/push) are unit
  fields, not skill side-effects.
- **Faction is three orthogonal fields**: `nationId` (19), `groupId` (14), `teamId` (11). 347 of 425
  operators have no `groupId`; 368 have no `teamId`. One flat enum could not express *"Ægir national,
  Abyssal Hunter group, no squad."*
- **There is no `damageType` field.** Physical / Arts / True / Elemental are properties of the branch
  trait string and of individual skills.

### D&D 5e / Pathfinder 2e

Fields: creature **type** and **subtype** tags, size category, damage
resistances/immunities/vulnerabilities, condition immunities, senses, **speed modes**
(walk/fly/swim/burrow), CR or level, alignment, and a family/tag taxonomy used for targeting rules.

**Taxonomy sizes:** 5e uses **14 creature types** — for 322 SRD creatures **and** for all 3,207.
PF2e uses a flat trait model, ~180 traits of which ~24 are type-like.

### FGO — class is five simultaneous knobs

Before a single Servant is designed, its class sets a base damage multiplier, a matchup row, a matchup
column, a star-economy pair, and a death resistance:

| Class | Base dmg | Star gen | Star absorb | Death rate |
|---|---|---|---|---|
| Saber | 1.00× | 10% | 100 | 35% |
| Archer | 0.95× | 8% | 150 | 45% |
| Lancer | 1.05× | 12% | 90 | 40% |
| Rider | 1.00× | 9% | 200 | 50% |
| Caster | 0.90× | 11% | 50 | 60% |
| Assassin | 0.90× | **25%** | 100 | 55% |
| **Berserker** | **1.10×** | **5%** | **10** | **65%** |
| Ruler | 1.10× | 10% | 100 | 35% |
| Avenger | 1.10× | 6% | 30 | 10% |
| Moon Cancer | 1.00× | 15% | 50 | **1%** |
| Alter Ego | 1.00× | 10% | 100 | 50% |
| Foreigner | 1.00× | 15% | 150 | 10% |
| Pretender | 1.00× | 20% | 100 | 30% |

**FGO's real trick: everything is a trait.** 580 distinct traits over 469 Servants. Class is a trait,
attribute is a trait, alignment is a trait, gender is a trait, **and rarity is a trait**. Any skill can
key a bonus off any of them through one uniform mechanism — including
`skyOrEarthExceptPseudoAndDemiServant` (218 holders), which exists purely to exclude two edge cases
from one bonus.

---

## 3. Derived vs authored — nobody derives unit stats from cost

**Across all seven RTS franchises, unit stats are hand-authored.** No studio derives combat stats from
a cost curve, and the shipped data rules it out.

**The proof from SC2's data model:** `LifeMax`, `LifeArmor`, `Speed`, `Sight`, `Radius`, `CargoSize`,
`Food`, `CostResource`, `KillXP` are all independent literals. **No formula node, no cost→stat
reference, no shared curve object anywhere in `UnitData.xml`.**

**The empirical proof** — DPS per 100 mineral-equivalents (gas at 1.5×):

| Unit | DPS / 100 m.e. | EHP / supply |
|---|---:|---:|
| Zergling | **40.23** | 70.0 |
| Marine | 19.52 | 45.0 |
| Zealot | 18.67 | 75.0 |
| Roach | 9.96 | 72.5 |
| Marauder | 6.79 | 62.5 |
| Immortal | 4.37 | 75.0 |
| Colossus | **3.11** | 58.3 |

**A 13× spread. No formula survives that.** What *is* nearly flat is **effective HP per supply** —
45–83, a ~1.8× band — so supply, not cost, is the axis SC2 balances durability on, and even that is
deliberately violated.

### What *is* derived

- **Upgrade costs.** SC1's `upgrades.dat` uses `base + factor × level`. The only built-in curve.
- **Reinforce cost.** CoH stores it as a **percentage of the entity's own cost**.
- **Build time** in Total Annihilation / Supreme Commander, from build points ÷ builder rate.
- **Mineral:gas ratio** — the closest thing to a maintained invariant in SC2. *"Vespene Geyser value
  increased from 2000 to 2250 to maintain the mineral to gas ratio."*

### The one shipped counter-example

**Warzone 2100** (1999, source released). A unit is *composed* of body + propulsion + turret, and
HP, weight, speed and cost are **computed by summing components**:

```cpp
// Speed = propulsion power-ratio × body power ÷ total weight
speed = asPropulsionTypes[...].powerRatioMult * bodyPower(...) / MAX(1, weight);
// Propulsion weight is a percentage of the body weight
return bodyStat.weight * (100 + propStat.weight) / 100;
```

**It is also the one design where the designer cannot hand-tune an individual unit.** That is the
trade a derived-stat generator makes.

---

## 4. Role taxonomies — there is no industry standard

**No engine has a closed combat-role enum.** The "tank / raider / siege / support" vocabulary is
critical language, not a schema.

| Game | Closed? | What exists | Count |
|---|---|---|---|
| **StarCraft II** | **No** | `CostCategory` (Army/Technology/Economy) = 3; `AttackTargetPriority` = 4 tiers; ~11 AI hint flags. **Blizzard's actual answer to "what is this unit for" is a list of other units** | — |
| **Warcraft III** | No | `Unit Classification` (Melee, Ranged, Caster, Siege, Suicidal, Peon, Sapper, …) drives **ability targeting filters, not balance math**. Role is implicit in the attack-type/armour-type *pairing* | ~12 |
| **Age of Empires II** | Yes, separate from armour | Infantry · Mounted · Archer · Siege · Ships · Civilians · Religious. *"Unit classes and armor classes are completely different."* | 7 / ~17 |
| **Age of Empires IV** | **Two conflicting lists** | 7 UI classes vs ~14 engine types. **"Unit types are not additive — they must be explicit."** The Condottiero has `infantry_light` and `melee_infantry` but *not* `light_melee_infantry`, so archers get no bonus. **There is no general `light` type at all** | 7 / ~14 |
| **Company of Heroes 3** | **No `role` field** | Directory level: aircraft · emplacements · infantry · team_weapons · vehicles = 5. Below: `weapon_class` = **27**, `unit_type_list` = **191 open tags** | 5 / 27 / 191 |
| **Total War: WH3** | **Yes — six independent taxonomy keys** | `unit_category`, `unit_class`, `ai_usage_group`, `ui_unit_group_land`, `unit_castes`, `autoresolver_unit_group_categories`. ~12 classes; **66 attributes**, ~⅓ composite bundles, several pure lookup keys with no payload | 12 / 66 |
| **Arknights** | **Yes** | **8 classes / 72 branches** — see [03-roster-scale.md](03-roster-scale.md) | 8 / 72 |

**Blizzard's official Game Guide disclaims completeness twice**: *"Here are **some** of the unit
types"* and *"This is **not** a complete list."*

**Nobody balances off the role name.** Games with a closed enum use it for presentation and AI; games
without encode role in tags plus an authored counter list.

---

## 5. The counter graph is authored, not derived

**SC2 ships `GlossaryStrongArray` / `GlossaryWeakArray`** — 76 units each carry a hand-written list of
up to 3 units they beat and 3 they lose to:

| Unit | STRONG vs | WEAK vs |
|---|---|---|
| Marine | Marauder, Hydralisk, Immortal | Siege Tank (sieged), Baneling, Colossus |
| Marauder | Thor, Roach, Stalker | Marine, Zergling, Zealot |
| Immortal | Siege Tank (sieged), Stalker, Roach | Marine, Zealot, Zergling |
| Dark Templar | SCV, Drone, Probe | Raven, Overseer, Observer |

**It is not symmetric or self-consistent** — the Adept lists Stalker as strong-vs with no reciprocal
entry on the Stalker. **The counter graph is data, not a derivation.**

AoE2 says the same from the other side: its own documentation has a section headed *"Not every unit
with an attack bonus against another unit can be called a counter"* (Spearman +1 vs Shock Infantry —
*"they get wrecked by them"*), and the converse, *"Despite not having bonuses, a unit can be called a
counter"* (Knights vs siege). **The bonus table is not the counter chart.**

### Typical advantage ratios

| Game | Mechanism | Range | Cluster |
|---|---|---|---|
| SC2 | additive vs attribute | ×1.20 – ×2.50 | ×1.4 – ×2.0 |
| SC1 | multiplicative penalty | ×0.25 – ×1.00 | — |
| WC3 | matrix cell | ×0.35 – ×2.00 | — |
| AoE2 | additive vs class | Halberdier **×6.3** over base | ×5.0 – ×5.5 for the spear line |
| AoE4 | additive, clean multiples | **×2.0 / ×3.5** | exactly ×2 |

**No studio has published a target counter strength.** See [06-unsourced.md](06-unsourced.md).
