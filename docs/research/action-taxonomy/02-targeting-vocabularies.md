# Targeting and area-shape vocabularies across shipped games

Research pass, 2026-09-02. Research only — no proposals, no specs.

Scope: the complete targeting vocabulary that shipped games express, tested against this project's
closed set of 6 `ActionTargetMode` values, 4 `ActionAreaShape` values, 3 `ActionAnchorSource` values
and 4 `ActionRelation` values.

---

## The finding in one paragraph

The six modes and four shapes cover **geometry-from-an-anchor** well and cover almost nothing else. Every
concept that a shipped game expresses by *pointing at a region* is already reachable — nova, placed
blast, whole-lane line, whole-column, 3×3 crater, cleave-to-neighbours, "all allies", "N random
enemies". Three of the suspected gaps turn out **not to be targeting gaps at all**: persistent ground
zones and caster-following auras are modelled in the reference implementations as *effect lifetime*,
not as target selection (World of Warcraft splits them into `SPELL_EFFECT_PERSISTENT_AREA_AURA` and the
six `SPELL_EFFECT_APPLY_AREA_AURA_*` variants, while the target of those spells stays an ordinary
caster-area), and nova genuinely is just `Area` + `Caster` anchor. What is missing is everything that
is **not geometry**: selecting a target by a *property* rather than a position (the largest gap by a
wide margin — Final Fantasy XII ships roughly forty such predicates as first-class data, and
*Plants vs. Zombies' own Magnet-shroom and Cattail both target by predicate*), anchoring on the
**previous** target so a chain can hop (World of Warcraft has a reference type literally named
`TARGET_REFERENCE_TYPE_LAST`), a **per-target weight** so cleave and beams can fall off with distance
or hop index, a **hit count separate from a target count**, a **launch-position mask** for the caster,
and **cone/diagonal/donut/cross** shapes. The single most awkward finding for this project is that
`Area` is rejected when there is no board — which means the gridless mode is left with only relation
and count, precisely the mode in which every reference gridless game (Hearthstone, Darkest Dungeon,
Slay the Spire, FFXII) does its targeting work through predicates, index adjacency and ordering keys
instead.

---

## Method, and how to read the sources

Tiering used below:

| Tier | What it is | Used here |
|---|---|---|
| **1 — source / engine data** | Emulator source that reproduces the shipped data model, official engine docs, official rules text, official patch notes | TrinityCore C++ headers and tables; `docs.larian.game`; D&D 5.1 SRD text; Steam **Official** Darkest Dungeon modding guide; Slay the Spire update history |
| **1.5 — tooling that encodes shipped shapes** | Third-party tooling whose whole job is to reproduce the game's real geometry | `ffxiv_bossmod` `AOEShapes.cs` |
| **2 — wiki prose** | wiki.gg / community wikis | PvZ, Hearthstone, Warcraft, Darkest Dungeon, Slay the Spire, poedb, jegged |

Per the binding method rule, `docs/research/game-design/06-unsourced.md` was read before the first
search. Its access notes were used directly: Fandom is 402 here, `r.jina.ai` was tried as a reader
proxy, and MediaWiki APIs were preferred over HTML. Two new blocks are recorded in
*What I could not find*.

Claims are marked **FACT** (sourced) or **INFERENCE** (my reading). Self-tallied numbers are marked
**(computed)**.

---

## 1. The gap table

This is the centrepiece. "Expressible" means expressible with the vocabulary as stated in the brief —
6 modes, 4 shapes, 3 anchors, 4 relations, plus `Count`/`MaxTargets`, `Size`/`Width`/`Height`,
`OrdinalPtr`/`SourceOrder` ordering, min/max range and a line-of-sight flag.

Every "would need a new value" row names **what mechanically reads the concept in the source game** —
a cosmetic-only concept is not a gap and is not listed.

| # | Concept | Shipped example, and how that game encodes it | What mechanically reads it there | Verdict |
|---|---|---|---|---|
| 1 | **Nova / burst from self** | WoW target row `15` = object `UNIT`, reference `SRC`, category `AREA`, check `ENEMY` ([TrinityCore `SpellInfo.cpp`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.cpp)); Warcraft wiki calls this PBAoE — *"the caster is the anchor for the spell. The effect/damage radiates outward from the caster"* ([warcraft.wiki.gg](https://warcraft.wiki.gg/wiki/Area_of_effect)) | Radius check against the caster's position | **Expressible** — `Area` + `Square`/`Rectangle` + `Caster` anchor. Suspicion **refuted**. |
| 2 | **Placed ground blast** | PvZ Cherry Bomb: almanac *"all zombies in a medium area"*, stats give a **3×3 area** ([plantsvszombies.wiki.gg](https://plantsvszombies.wiki.gg/wiki/Cherry_Bomb_(PvZ))) | Tile-set enumeration around the planted tile | **Expressible** — `Area` + `Square` size 3 + `ChosenCell`. |
| 3 | **Whole-lane line / beam / pierce** | PvZ2 Laser Bean: *"attacks zombies using a slow firing laser beam that hits every zombie in his lane"*; Plant Food deals 1,800 *"to every zombie in his lane"* ([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Laser_Bean)). PvZ Heroes **Strikethrough**: *hits all fighters on the attacking lane, as well as the opposing hero*. WoW has `TARGET_SELECT_CATEGORY_LINE` as a top-level category with rows `133/134/135` (ally/enemy/default) and a `Width` field on `SpellInfo` ([`SpellInfo.h`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.h)) | Per-cell / per-unit inclusion along the lane | **Expressible** — `Area` + `Row` + `Caster` anchor. |
| 4 | **Line with per-distance falloff, or that stops at a blocker** | Into the Breach **Burst Beam**: *"Fires a piercing beam that decreases in damage the further it goes, starting at 3 damage and decreasing by one per square"* ([GameFAQs weapon guide](https://gamefaqs.gamespot.com/pc/205477-into-the-breach/faqs/76363/weapons)) | Damage is indexed by the target's step number along the beam | **GAP — per-target weight.** The shape is reachable; a value that varies per target along the shape is not. The LoS flag is a boolean per target, not a "stop the beam here" rule. |
| 5 | **Cone / arc / wedge** | WoW: `TARGET_SELECT_CATEGORY_CONE` is one of only eight selection categories, with `TARGET_DIR_*` (`FRONT`, `BACK`, `LEFT`, `RIGHT`, four diagonals, `RANDOM`, `ENTRY`) and a `ConeAngle` field on `SpellInfo`; rows `24`/`54` are `CONE/ENEMY/FRONT`, row `59` is `CONE/ALLY/FRONT`. Larian's official engine docs list **Cone** as its own skill type — *"cause an effect in a cone shape determined by angle and range"*, parameters `Angle`, `Range` ([docs.larian.game](https://docs.larian.game/Skill_creation)). D&D 5.1 SRD makes cone one of exactly five area shapes. WoW **Cleave** is *"damage in a 70-degree arc from the target"* | Angular test between the caster's facing and the vector to each candidate | **GAP — new shape.** A widening wedge on a 5×9 board is not `Row`, `Column`, `Square` or `Rectangle`. |
| 6 | **Diagonal / radial spokes** | PvZ **Starfruit**, in this game's own franchise: almanac *"Starfruits shoot stars in 5 directions"* — left, up, down, and two diagonals with gradient **±1/2** ([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Starfruit_(PvZ))) | Projectile collision along five fixed vectors, two of which cross rows and columns simultaneously | **GAP — new shape.** No combination of `Row`, `Column`, `Square` and `Rectangle` produces a half-slope diagonal. |
| 7 | **Diamond / Manhattan radius** | Final Fantasy Tactics: an effect area is *"determined by counting the number of squares an attack can hit from the center to one corner of the effect area"* ([FFT Battle Mechanics Guide, GameFAQs](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/3876)); D&D's **sphere** is a radial, not square, area | Distance metric used for cell inclusion | **GAP — shape family.** `Square` is Chebyshev; a diamond is Manhattan. Minor but distinct. |
| 8 | **Donut / ring (exclude the centre)** | FFXIV ships donuts and donut-sectors; the shape library written to reproduce them declares `AOEShapeDonut(float InnerRadius, float OuterRadius)` and `AOEShapeDonutSector(InnerRadius, OuterRadius, HalfAngle, DirectionOffset)` ([`ffxiv_bossmod/AOEShapes.cs`](https://raw.githubusercontent.com/awgil/ffxiv_bossmod/master/BossMod/BossModule/AOEShapes.cs)) | Inner-radius exclusion test | **GAP — new shape.** Requires an inner exclusion, which no current shape has. |
| 9 | **Cross / plus** | FFXIV: `AOEShapeCross(float Length, float HalfWidth, Angle DirectionOffset)` (same file) | Union of two perpendicular rectangles about one anchor | **GAP — composition.** Reachable as `Row ∪ Column` at the same anchor, but the vocabulary has no union of two shapes in one action. |
| 10 | **Chain / bounce** | WoW: `SpellEffectInfo` carries `int32 ChainTargets` and `float ChainAmplitude` ([`SpellInfo.h`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.h)). Chain Heal: *"then jumps up to 20 yards to heal the 3 most injured nearby allies. Healing is reduced by 30% with each jump"*; jump radius is measured **from the previous target**, and it *"cannot chain back to heal the same target"* ([warcraft.wiki.gg](https://warcraft.wiki.gg/wiki/Chain_Heal)). PoE: *"a projectile cannot chain to a target it has already hit"*, chain count is a summed stat, and chain is **fourth in behaviour priority — after split, pierce and fork, before return** ([poedb](https://poedb.tw/us/Chain)). WC3 Chain Lightning exposes *Number of Targets* and *Damage Reduction per Target* as editable object-editor fields ([HIVE](https://www.hiveworkshop.com/threads/scaling-ability-damage-on-chain-lightning.267275/)) | Hop counter, per-hop radius from the last hit, a visited-set, and a multiplicative falloff applied per hop | **GAP — new mode**, and it decomposes into four sub-gaps: hop count, previous-target anchor (row 11), no-revisit set, per-hop weight (row 4). |
| 11 | **Anchor on the *previous* target** | WoW's reference-type enum is `NONE, CASTER, TARGET, LAST, SRC, DEST` — `TARGET_REFERENCE_TYPE_LAST` exists as a named value ([`SpellInfo.h`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.h)) | Selection for hop *n* measures from the unit hit at hop *n−1* | **GAP — new anchor value.** `Caster`, `PrimaryTarget` and `ChosenCell` are all fixed for the whole action. |
| 12 | **Predicate targeting ("lowest HP ally", "all burning enemies")** | FFXII gambits are data: `Ally: lowest HP`, `Ally: lowest defense`, `Ally: lowest magick resist`, `Ally: strongest weapon`, `Foe: highest/lowest (stat)`, `Foe: status = (status)`, `Foe: (element)-weak`, `Foe: undead`, `Foe: flying`, `Foe: targeted by ally`, `Foe: party leader's target`, `Foe: nearest/furthest` ([jegged.com gambit list](https://jegged.com/Games/Final-Fantasy-XII/Gambits/)). WoW Chain Heal picks *"the 3 most injured"*. **PvZ itself**: Magnet-shroom *"remove[s] helmets and other metal objects from zombies"* and *"will not affect zombies without metal equipment"* — it selects on a carried-item property, not a position ([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Magnet-shroom_(PvZ))). Cattail *"Fire on closest enemy to the house"* **except** that it prioritises Balloon Zombies ([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Cattail_(PvZ))) | The AI/effect reads a live stat, a status stack, an element tag or a carried-object flag when building the candidate list | **GAP — the largest one.** All six modes select by relation and position only. Nothing in the vocabulary reads target state. |
| 13 | **Ordering by a computed key** | Cattail runs a two-key order: predicate primary (balloons first), distance secondary (closest to the house). Chain Heal orders by health **deficit**. FFXII evaluates gambits **top-down and takes the first match** | The selector sorts candidates by a derived number before applying the count | **GAP.** `OrdinalPtr` and `SourceOrder` are fixed orderings; there is no "order by <key>". |
| 14 | **Self-exclusion — "all allies except me"** | Darkest Dungeon exposes a boolean `.self_target_valid "true or false"` on every combat skill ([official modding parameter list](https://steamcommunity.com/sharedfiles/filedetails/?id=1095670238), read via `r.jina.ai`). Hearthstone ships it as distinct card text — *"Cards like Baron Geddon and Brawl exclude one minion from the effect, but hit all other targets"* ([hearthstone.wiki.gg](https://hearthstone.wiki.gg/wiki/Minion)) | A membership test that removes the caster from an otherwise-complete candidate list | **GAP or ambiguity.** `ActionRelation.Self` and `.Ally` being separate values does not say whether `Ally` includes the caster; the reference games make it an explicit flag rather than leaving it to the relation. |
| 15 | **Splash / cleave to neighbours at reduced value** | WC3 gives every splash attacker three concentric bands — *Full Damage Area*, *Medium Damage Area*, *Small Damage Area* — plus *Damage Factor* per band; the Mortar Team is *"full damage within a 25 radius, 40% damage within a 150 radius, and 10% damage within a 250 radius"* ([HIVE](https://www.hiveworkshop.com/threads/how-to-give-a-unit-splash-damage-via-item-spell-or-aura.311818/)). PvZ Heroes ships a **Splash Damage** trait that hits adjacent lanes for a *specified, separate* amount ([wiki.gg PvZ Heroes](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes)) | Distance band → damage multiplier lookup at damage time | **Geometry expressible** (`Area` + `Square` size 1 + `PrimaryTarget` anchor); **value is a GAP** — same missing per-target weight as row 4. |
| 16 | **Probabilistic extra targets** | Darkest Dungeon: `.extra_targets_chance "value"` and `.extra_targets_count "value"` are per-skill fields (official modding parameter list) | A roll at resolution time that adds targets beyond the declared set | **GAP.** `Count`/`MaxTargets` are deterministic quantities. |
| 17 | **Multi-hit vs multi-target** | D&D Scorching Ray: *"You create three rays of fire... You can hurl them at one target or several. Make a ranged spell attack for each ray."* ([Open5e / SRD](https://api.open5e.com/v1/spells/?search=scorching%20ray)). Slay the Spire Sword Boomerang: *"Deal 3 damage to a random enemy 3 times"* — three separate rolls ([slaythespire.wiki.gg](https://slaythespire.wiki.gg/wiki/Sword_Boomerang)). Hearthstone draws the line explicitly: AoEs *"are distinguished from multi-target spells such as Arcane Missiles and Cleave, which are essentially multiple single-target effects"* ([hearthstone.wiki.gg](https://hearthstone.wiki.gg/wiki/Area_of_effect)) | Hit count is an action property; whether selection re-runs per hit is a separate rule, and it changes how many draws leave the RNG stream | **GAP.** One `Count` cannot carry both "how many targets" and "how many hits", nor the re-roll-per-hit flag. |
| 18 | **Caster launch-position legality** | Darkest Dungeon skills carry **both** masks: `.launch` = which of the user's own ranks may use the skill, `.target` = which enemy ranks it may hit, e.g. `.launch 321 .target 1234` ([official modding guide, via Steam discussion quoting it](https://steamcommunity.com/app/262060/discussions/0/612823460273726401)); the parameter list confirms `.launch "ranks"` / `.target "ranks"` | The UI greys the skill out and the resolver refuses it when the actor stands in an illegal rank | **GAP.** min/max range constrains the *distance to the target*; nothing constrains *where the caster must stand*. On a 9-column lawn this is a natural concept. |
| 19 | **Damage weighted by the caster's own position** | Darkest Dungeon: `.rank_damage_modifiers "rank4 rank3 rank2 rank1"` — different multipliers by the performer's position (official parameter list) | Damage lookup keyed on the caster's rank | **GAP.** Related to row 4 but keyed on the source, not the target. |
| 20 | **Cap behaviour when eligible > cap** | WoW since patch 5.2.0 does **not** pick a subset: the cap is 20 and beyond it *"the total damage done is spread evenly over the actual number of targets present"* ([warcraft.wiki.gg](https://warcraft.wiki.gg/wiki/Area_of_effect)) | Damage per target = capped total ÷ actual count | **GAP.** `MaxTargets` truncates the list; "hit everyone but dilute the value" is a different rule and is not expressible. |
| 21 | **Targeting a cell rather than an actor (summon placement)** | PvZ plants onto tiles; PvZ Heroes: *"A card can be placed in any empty lane"*, with `Team Up` as the documented exception allowing 2 per lane ([wiki.gg PvZ Heroes](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes)). Larian lists **Summon** and **Wall** as skill types whose templates define placement ([docs.larian.game](https://docs.larian.game/Skill_creation)) | An occupancy test on the destination cell before the entity is created | **Partly expressible.** `ChosenCell` gives the *anchor*; but all four `ActionRelation` values (`Self`/`Ally`/`Enemy`/`Any`) describe an actor relationship, so there is no relation meaning "an unoccupied cell". |
| 22 | **Multi-cell occupants and de-duplication** | Darkest Dungeon: large enemies occupy several ranks; *"they will be affected by an attack that hits any of their ranks. However, they can be hit at most once by a given attack, and an attack hitting multiple of their ranks will not deal additional damage"* ([darkestdungeon.wiki.gg](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon))) | The resolver de-duplicates by entity id after building the cell set | **GAP (rule, not vocabulary).** Any shape that enumerates cells needs this the moment one entity spans two cells. |
| 23 | **Persistent ground zone that stays and ticks** | WoW splits it off from targeting entirely: `SPELL_EFFECT_PERSISTENT_AREA_AURA` (handler `EffectPersistentAA`, index 27) and `SPELL_EFFECT_CREATE_AREATRIGGER` (index 179) are **effects**; the *target* of such a spell is an ordinary destination ([TrinityCore `SpellEffects.cpp`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellEffects.cpp)). Larian's `Rain` type carries `ConsequencesStartTime` / `ConsequencesDuration`, again a lifetime, not a target | A world object with a lifetime re-tests occupants on a period | **Not a targeting gap. Suspicion refuted.** In both reference engines this is effect lifetime. Geometry is `Area` + `ChosenCell`. |
| 24 | **Aura that follows the caster** | WoW has six distinct effects — `APPLY_AREA_AURA_PARTY` (35), `_RAID` (65), `_PET` (119), `_FRIEND` (128), `_ENEMY` (129), `_OWNER` (143) — again effects, not target modes (same file) | The aura re-evaluates membership continuously as units enter and leave the radius | **Not a targeting gap. Suspicion refuted.** The difference from a placed zone is *who owns the origin and when membership is recomputed*, both effect-side. |
| 25 | **Line of sight as a per-cell inclusion rule** | D&D 5.1 SRD: *"If no unblocked straight line extends from the point of origin to a location within the area of effect, that location isn't included in the spell's area"* ([5thsrd.org](https://5thsrd.org/spellcasting/casting_a_spell/)) | Occlusion test per cell before the cell joins the area | **Expressible** — this is exactly what the LoS flag does, provided it is applied per cell and not only to the primary target. |

**Tally (computed): 25 concepts examined — 5 fully expressible, 3 suspicions refuted as non-targeting,
1 partly expressible, 16 gaps or partial gaps.** Of the 16, **11 are not shapes at all** — they are
predicates, orderings, weights, counts and anchors.

---

## 2. The closed vocabularies worth comparing against

Three shipped systems publish a *closed* targeting vocabulary. They are the best yardsticks.

### World of Warcraft — the largest one (FACT, tier 1)

Targeting is factored into five orthogonal axes rather than one enum
([`SpellInfo.h`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.h)):

| Axis | Values |
|---|---|
| `SpellTargetObjectTypes` | `NONE, SRC, DEST, UNIT, UNIT_AND_DEST, GOBJ, GOBJ_ITEM, ITEM, CORPSE, CORPSE_ENEMY, CORPSE_ALLY` |
| `SpellTargetReferenceTypes` | `NONE, CASTER, TARGET, **LAST**, SRC, DEST` |
| `SpellTargetSelectionCategories` | `NYI, DEFAULT, CHANNEL, NEARBY, **CONE**, AREA, TRAJ, **LINE**` |
| `SpellTargetCheckTypes` | `DEFAULT, ENTRY, ENEMY, ALLY, PARTY, RAID, RAID_CLASS, PASSENGER, SUMMONED` |
| `SpellTargetDirectionTypes` | `NONE, FRONT, BACK, RIGHT, LEFT, FRONT_RIGHT, BACK_RIGHT, BACK_LEFT, FRONT_LEFT, RANDOM, ENTRY` |

Each concrete implicit target is a row in a table combining those five
([`SpellInfo.cpp`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.cpp));
the source reports the table running to roughly **152 entries, indices 0–151**. Sampled rows:

| Id | Object | Reference | Category | Check | Direction |
|---|---|---|---|---|---|
| 1 | UNIT | CASTER | DEFAULT | DEFAULT | NONE |
| 2 | UNIT | CASTER | NEARBY | ENEMY | NONE |
| 15 | UNIT | SRC | AREA | ENEMY | NONE |
| 16 | UNIT | DEST | AREA | ENEMY | NONE |
| 20 | UNIT | CASTER | AREA | PARTY | NONE |
| 24 | UNIT | CASTER | CONE | ENEMY | FRONT |
| 59 | UNIT | CASTER | CONE | ALLY | FRONT |
| 89 | DEST | DEST | TRAJ | DEFAULT | NONE |
| 133 | UNIT | DEST | LINE | ALLY | NONE |
| 134 | UNIT | DEST | LINE | ENEMY | NONE |

Three things matter for this project. **First**, `CONE` and `LINE` are peers of `AREA`, not variants
of it — this system does not treat a beam as a thin rectangle. **Second**, `LAST` exists as a
reference type, which is the anchor a chain needs. **Third**, the relation axis (`CheckType`) is
richer than four values because it distinguishes `PARTY` from `RAID` from `ALLY` — group membership is
a first-class relation. **INFERENCE:** the factoring itself is the lesson; a single flat mode enum has
to multiply out modes × shapes × anchors × relations, and WoW instead makes them independent columns.

Numeric quantities live beside the target, not in it: `SpellEffectInfo` carries `ChainTargets`,
`ChainAmplitude`, `TargetA`/`TargetB` with separate radius entries, and `SpellInfo` carries `ConeAngle`
and `Width`.

### D&D 5.1 SRD — five shapes, exactly (FACT, tier 1)

*"A spell's description specifies its area of effect, which typically has one of five different shapes:
cone, cube, cylinder, line, or sphere. Every area of effect has a point of origin"*
([5thsrd.org](https://5thsrd.org/spellcasting/casting_a_spell/)).

Mapped onto a 2-D board: cube → square, cylinder/sphere → circle or diamond, line → row/column,
**cone has no counterpart in the four shipped shapes.** The SRD also defines the origin's own
membership as a per-spell choice — *"A cone's point of origin is not included in the cone's area of
effect, unless you decide otherwise"* — which is the same self-exclusion concept as row 14, applied to
cells rather than actors.

### Divinity Engine (Divinity: Original Sin 2) — skill types are the vocabulary (FACT, tier 1)

Larian's official docs make the *shape* the skill type ([docs.larian.game](https://docs.larian.game/Skill_creation)):

| Type | Doc text | Targeting parameters |
|---|---|---|
| `Target` | *"usually affects one target with a non-projectile effect"* | `TargetRadius`, `AreaRadius`, `ExplodeRadius` |
| `Shout` | *"a self-buff or area of effect skill"* | `AreaRadius` |
| `Projectile` | *"fires one or more projectiles along a preset path"* | `TargetRadius`, `AreaRadius`, `ExplodeRadius` |
| `ProjectileStrike` | *"a series of projectiles which rain from the sky rather originating from the caster"* | `AreaRadius`, `ExplodeRadius` |
| `Cone` | *"cause an effect in a cone shape determined by angle and range"* | `Angle`, `Range` |
| `Zone` | *"cause an effect in a rectangular or square shape"* | `Range`, `AreaRadius` |
| `Jump`, `MultiStrike`, `Rush`, `Teleportation` | movement | `TargetRadius`, `HitRadius` |
| `Summon`, `Wall` | entity creation | templates define placement |
| `Dome`, `Rain`, `Storm`, `Tornado`, `Quake` | large areas | `AreaRadius`; `Rain` adds `ConsequencesStartTime`/`ConsequencesDuration` |

Note that **three separate radii** coexist on one skill: `TargetRadius` (what may be picked),
`AreaRadius` (what the effect covers), `ExplodeRadius` (what the impact damages) — *"often but not
always the same as AreaRadius"*. The shipped vocabulary has one `Size`/`Width`/`Height`.

### FFXIV shapes (tier 1.5)

The shape library written to reproduce FFXIV's real telegraphs declares:
`AOEShapeCircle(Radius)`, `AOEShapeCone(Radius, HalfAngle, DirectionOffset)`,
`AOEShapeDonut(InnerRadius, OuterRadius)`,
`AOEShapeDonutSector(InnerRadius, OuterRadius, HalfAngle, DirectionOffset)`,
`AOEShapeRect(LengthFront, HalfWidth, LengthBack, DirectionOffset)`,
`AOEShapeCross(Length, HalfWidth, DirectionOffset)`,
`AOEShapeTriCone(SideLength, HalfAngle, DirectionOffset)`,
`AOEShapeCustom(RelSimplifiedComplexPolygon, DirectionOffset)`
([`AOEShapes.cs`](https://raw.githubusercontent.com/awgil/ffxiv_bossmod/master/BossMod/BossModule/AOEShapes.cs)).

**INFERENCE:** the interesting members are `AOEShapeRect`'s `LengthBack` (a rectangle that extends
*behind* the anchor as well as in front) and `AOEShapeCustom` (an escape hatch to an arbitrary
polygon). A closed shape enum in a game with a long content tail eventually grows an escape hatch.

---

## 3. Lane- and grid-native targeting

### Plants vs. Zombies itself — richer than the four shapes (FACT, tier 2)

This is the most directly load-bearing section, because it is this game's own franchise.

| Plant | Targeting rule | Expressible? |
|---|---|---|
| **Laser Bean** (PvZ2) | *"hits every zombie in his lane"* | Yes — `Area`/`Row`/`Caster` |
| **Threepeater** | Fires in its own lane plus the lanes above and below; *"It actually shoots one pea straight ahead from the square above, one from its square, and one from the square below"*; on the top or bottom row it *"will only shoot peas in two lanes instead of the usual three"*; its attack triggers when a zombie appears *"anywhere within its area of effect (i.e. in any one of the three lanes)"* ([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Threepeater_(PvZ))) | Yes — `Rectangle` 3 tall, clipped at the board edge |
| **Cherry Bomb** | 3×3 anchored on the planted tile | Yes — `Area`/`Square`/`ChosenCell` |
| **Starfruit** | Five fixed directions, two of them **half-slope diagonals** | **No** — row 6 |
| **Magnet-shroom** | *"remove[s] helmets and other metal objects from zombies"*, over a 7×5 tile area, and *"will not affect zombies without metal equipment"*; explicitly cannot take the Gargantuar's sign or affect hypnotised zombies | **Shape yes, selection no** — the predicate is the whole point (row 12) |
| **Cattail** | *"Cattails can attack at any lane and shoot down balloon zombies too"*; base rule *"Fire on closest enemy to the house"*, overridden by a Balloon-Zombie priority | **No** — two-key ordering with a predicate primary (rows 12, 13) |

**Cattail also demonstrates the determinism consequence of a shared ordering key:** because every
Cattail evaluates the same rule against the same board, *"all of your Cattails will typically fire at
the same target"* — a documented, and sometimes undesirable, emergent behaviour of a fully
deterministic selector.

### PvZ Heroes — a 5-lane card game (FACT, tier 2)

Five lanes, some elevated or aquatic; a card is placed in **any empty lane**, with `Team Up` as the
documented exception permitting two per lane. Two targeting traits are shipped as keywords
([wiki.gg](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes)):

- **Splash Damage** — *"does damage to enemies next door equal to its Splash Damage value per attack"*,
  i.e. adjacent lanes, at a **separately specified value**.
- **Strikethrough** — *"hits all fighters on the attacking lane, as well as the opposing hero"*.

**INFERENCE:** these are the same two concepts as rows 15 and 3 — cross-lane splash at a reduced value,
and a full-lane pierce that also reaches a non-board target (the hero). The second one is worth
noting: `Strikethrough`'s tail target is not on the board at all, so a shape alone cannot describe it.

### Darkest Dungeon — the rank-legality masks (FACT, tiers 1 and 2)

The real rule, from the official wiki: *"Heroes and enemies are arranged into **ranks** … from 1 to 4,
with 1 being closest to the opponent party and 4 being the farthest away"*
([darkestdungeon.wiki.gg](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon))).

Every combat skill is a line of data carrying **two independent rank masks**, written as digit strings.
The worked example from the official modding guide:

```
combat_skill: .id "gods_illumination" .level 0 .type "ranged" .atk 80% .dmg -40% .crit 5%
              .launch 321 .target 1234 .effect "Dodge Curse 1" "Light 1"
```

`.launch 321` = usable from the caster's ranks 3, 2 and 1; `.target 1234` = may hit any of the four
opposing ranks ([Steam discussion quoting the official guide](https://steamcommunity.com/app/262060/discussions/0/612823460273726401)).
The parameter reference confirms the field set
([official parameter list](https://steamcommunity.com/sharedfiles/filedetails/?id=1095670238)):

| Field | Meaning |
|---|---|
| `.launch "ranks"` | which of the user's own ranks may perform the skill |
| `.target "ranks"` | which target ranks may be hit |
| `.type` | `melee`, `ranged`, `move`, `""`, `teleport` |
| `.self_target_valid "true or false"` | whether the caster is a legal target of its own skill |
| `.extra_targets_chance "value"` / `.extra_targets_count "value"` | probabilistic splash |
| `.rank_damage_modifiers "rank4 rank3 rank2 rank1"` | damage multiplier by the **performer's** rank |
| `.is_crit_valid`, `.can_miss` | per-skill resolution switches |

Darkest Dungeon II keeps the same two-mask model — a patch note records *"Bleed Out and Bleed Out+ had
their launch ranks increased from 1 to 1-2"*, and the Duelist's *Touche* is documented as usable *from
every rank* to hit *the front two ranks*
([darkestdungeon.com patch notes](https://www.darkestdungeon.com/patch-notes/);
[wiki.gg Duelist strategy](https://darkestdungeon.wiki.gg/wiki/Duelist_(Darkest_Dungeon_II)/Strategy)).

**Two rules the shipped vocabulary has no home for**: the launch mask (row 18), and the multi-rank
occupant de-duplication rule (row 22).

### Into the Breach — an 8×8 grid with per-step falloff (FACT, tier 2)

Fights are on an 8×8 grid. **Burst Beam** is *"a piercing beam that decreases in damage the further it
goes, starting at 3 damage and decreasing by one per square"*; **Vortex Fist** *"damages and pushes all
adjacent tiles"*, dealing 2 damage to all targets and **1 self damage to the user** — a shipped case of
an area that deliberately includes the caster
([GameFAQs weapon guide](https://gamefaqs.gamespot.com/pc/205477-into-the-breach/faqs/76363/weapons)).

**INFERENCE:** Vortex Fist is the mirror of self-exclusion — the caster is *inside* the area on purpose,
at a different value. That is another instance of the per-target-weight gap, and another argument that
caster membership needs to be explicit rather than implied by the relation.

### Final Fantasy Tactics — the metric and the third dimension (FACT, tier 2)

An effect area is *"determined by counting the number of squares an attack can hit from the center to
one corner of the effect area"* — a diamond, not a square. FFT adds a third targeting axis this project
does not have and does not need: **vertical tolerance**, *"the user's current height + or − Vertical"*,
written as e.g. *"2 Vertical 1"*
([FFT Battle Mechanics Guide](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/3876);
[FF Hacktics wiki](https://ffhacktics.com/wiki/Vertical_Tolerance)).

**INFERENCE:** a 5×9 lawn is flat, so vertical tolerance is genuinely out of scope. The Manhattan
metric is not — it is a shape choice, and FFT made the opposite one from `Square`.

---

## 4. Targeting when there is no board

This is the part that bears on the gridless web battle mode, given that `Area` is rejected at bind time
when no board exists.

**FACT — the reference gridless games do not have a geometry substitute; they replace geometry with
set predicates over an ordered list.**

| Game | What replaces "area" | Source |
|---|---|---|
| **Hearthstone** | Card text names a set directly: *"all minions"*, *"all other minions"*, *"adjacent minions"*. The board is a **1-D sequence of up to 7 positions**, so adjacency exists without any grid: *"Explosive Shot deals high damage to a single target with splash damage onto adjacent minions"*, *"Dire Wolf Alpha … provides an Attack buff to adjacent minions"* | [hearthstone.wiki.gg Minion](https://hearthstone.wiki.gg/wiki/Minion) |
| **Slay the Spire** | *"Deal damage to ALL enemies"* vs *"Deal 3 damage to a random enemy 3 times"* — a set literal and a repeated draw | [slaythespire.wiki.gg](https://slaythespire.wiki.gg/wiki/Sword_Boomerang) |
| **Darkest Dungeon** | Four ranks per side is a 1-D position line; the two digit masks are the whole geometry | [darkestdungeon.wiki.gg](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon)) |
| **FFXII** | No board-derived targeting at all; the entire selector is a predicate list evaluated top-down | [jegged.com](https://jegged.com/Games/Final-Fantasy-XII/Gambits/) |

**INFERENCE, and the load-bearing one for a dual-mode project:** in all four, targeting factors into a
pipeline that is *identical* with or without a board —

1. a **relation filter** (ally / enemy / any / self),
2. an optional **predicate filter** (state, status, element tag, carried item),
3. an **ordering key** (position, distance, a computed stat, or a seeded roll),
4. a **count or cap rule** (take N, take all, take all-but-dilute).

Geometry is a *board-only extra pre-filter* that narrows the candidate set before step 1. Under that
reading, the shipped vocabulary has put its expressive weight into the one stage that cannot exist in
the gridless mode, and has no vocabulary for steps 2 and 3 in *either* mode. Hearthstone in particular
shows that **adjacency is a positional concept that survives the loss of a grid** — an index-neighbour
relation, not a shape.

---

## 5. Ordering and determinism

Directly relevant to `RolledTarget` being a seeded stream and to replay safety.

### The Slay the Spire incident is the cautionary tale (FACT, tier 1 + 2)

Slay the Spire runs many named streams, all derived from one run seed: `monsterRng`, `eventRng`,
`merchantRng`, `cardRng`, `treasureRng`, `relicRng`, `potionRng`, `monsterHpRng`, `aiRng`, `shuffleRng`,
`cardRandomRng`, `miscRng`. The defect: they were **all initialised with the same `Settings.seed`**, so
*"the first value produced by `monsterRng` will match the first value produced by `eventRng`"* —
supposedly independent systems were correlated, and knowing one early outcome narrowed a later,
unrelated one ([Correlated Randomness write-up](https://forgottenarbiter.github.io/Correlated-Randomness/);
[sts2-rng-fix](https://github.com/ing-gom/sts2-rng-fix)). Slay the Spire 2 fixed it before v0.107.1 by
**avalanche-mixing each per-stream seed so the streams decorrelate**, keeping determinism.

Separately, the update history records that *"Tingsha, Sword Boomerang, Bouncing Flask, Rip and Tear,
Thunder Orbs, Havoc, and Juggernaut now utilize seeded enemy target randomization"*
([slaythespire.wiki.gg](https://slaythespire.wiki.gg/wiki/Sword_Boomerang)) — i.e. **random target
selection had to be explicitly migrated onto the seeded stream**; it was not there by default, and it
was worth a patch note.

**INFERENCE:** two lessons for a `RolledTarget` mode. One, a stream per concern is right but the
per-stream seeds must be mixed, not shared. Two, the *number of draws* an action takes from the stream
is part of its replay contract — which is why the multi-hit / multi-target distinction in row 17 is a
determinism issue, not only an expressiveness one: three hits with re-rolled targets consumes three
draws, three hits on one rolled target consumes one.

### Deterministic selectors in the wild (FACT)

| Rule | Game | Key |
|---|---|---|
| First matching rule wins, evaluated top-down, re-evaluated every turn | FFXII gambits | Author-declared priority; no RNG |
| Largest health deficit among nearby allies, no revisits, −30% per jump | WoW Chain Heal | A live stat |
| Balloon Zombies first, otherwise closest to the house | PvZ Cattail | Predicate then distance |
| Cannot chain to an already-hit target; behaviour priority split → pierce → fork → chain → return | PoE | A visited set plus a fixed behaviour precedence |
| Beyond the cap, damage is *split evenly over all present targets* rather than a subset being chosen | WoW ≥ 5.2 | No selection at all |

### Resolution order and target-list snapshotting (FACT, tier 2)

Hearthstone's advanced rulebook is unusually explicit and settles a question this project will hit:

- Ordering: *"If multiple events are considered simultaneously … they are Queued by **order of play**"*,
  where order of play is *"the order the Entities each Event/trigger is associated with entered play,
  from oldest to newest"*.
- Immutability: *"A Queue becomes immutable once Hearthstone starts to resolve the first entry in it.
  No new entries can be added to the Queue after this point."*
- **No snapshotting of board state**: *"Minions and effects have no memory of earlier board state. The
  moment an event takes place the board state is updated. Whenever a Queue is populated or an effect
  resolved (or continued), the most up to date board state is used."*
  ([hearthstone.wiki.gg Advanced rulebook](https://hearthstone.wiki.gg/wiki/Advanced_rulebook))

**INFERENCE:** the target *list* is fixed once resolution begins, but every value read during resolution
is live. That is the combination a replay-safe engine wants — the seeded draws all happen at list-build
time, and nothing later can change how many draws were taken. It also matters for row 22: de-duplication
must happen at list-build time, not at damage time.

**Order of play** is worth noting as a third ordering key alongside the project's `OrdinalPtr` and
`SourceOrder` — it is spawn-age order, which on a lawn is neither board position nor spawn index within
a wave.

---

## 6. What I could not find

Non-empty by mandate, and every item below was actually looked for.

**New access blocks, to record alongside `06-unsourced.md`:**

- **`wowdev.wiki` is fully unreachable here.** Direct fetch returns HTTP 403; the MediaWiki
  `api.php?action=parse` endpoint also returns 403; and the `r.jina.ai` reader proxy returns the
  Cloudflare *"Performing security verification"* interstitial rather than content. The canonical
  `Spell.dbc/EffectImplicitTarget` table — the DBC-side names for all ~152 implicit targets — could not
  be read. **Worked around** by using TrinityCore's C++ tables instead, which give the same axes but
  as category tuples rather than DBC enum names.
- **`poewiki.net` is behind Anubis + Cloudflare** — both direct and via `r.jina.ai`. PoE chaining was
  recovered from `poedb.tw` instead, at lower confidence for exact numbers.
- **`liquipedia.net/warcraft` article pages return 403** to direct fetch. Its MediaWiki API answered but
  returned only a one-line stub for Chain Lightning (*"an active damaging ability that jumps to nearby
  enemies of the primary target"*); the numbers live in `SpellCard/*` templates that the API response
  did not expand.
- **`steamcommunity.com` rate-limits direct fetches** (*"You've made too many requests recently"*). The
  `r.jina.ai` proxy worked for the same URL.

**Things that appear not to be published, or that I could not confirm:**

- **The meaning of the `~` prefix in Darkest Dungeon's `.target` strings** (e.g. `.target "~1234"`).
  It appears in worked examples in multiple modding sources, but the official parameter list *"does not
  explain the `~` prefix notation"*, and no first-tier source I could reach defines it. The widely
  repeated community reading is "targets allies rather than enemies" — **do not report that as
  sourced.**
- **Exact WC3 Chain Lightning numbers.** The *fields* are confirmed (*Number of Targets*, *Damage
  Reduction per Target*, editable in the object editor), but the shipped per-level values and the
  reduction factor could not be cited from a reachable source.
- **The FFXIV `Action.exd` `CastType` enum with its numeric values** (the game's own shape ids —
  circle / cone / rect / donut / cross). Three differently-phrased searches found the shapes discussed
  in wikis and reproduced in tooling, but no reachable page publishes the id→shape mapping. The shape
  list here therefore comes from tier-1.5 tooling, not from the game's data schema.
- **How WoW selects *which* targets are hit when eligible targets exceed a cap.** The current answer is
  that it does not select — damage is split. I could not find a patch note or dev statement describing
  a nearest-first or random-subset rule for any era, and the search surfaced none.
- **Whether Slay the Spire's Sword Boomerang re-rolls its target per hit or can hit the same enemy
  twice.** The wiki *"does not explicitly specify"* it; only the seeded-randomisation patch note exists.
  This is the single cleanest test case for the multi-hit/multi-target distinction and it is
  undocumented.
- **Iron Marines and Legion TD targeting vocabularies.** Both were named in the brief. Neither surfaced
  any datamine, official API, or engine export describing how targeting is declared; what exists is
  strategy prose. No usable finding, and none is asserted above.
- **Any designer commentary explaining why a targeting vocabulary was closed at N values.** Consistent
  with the prior pass's most repeated negative finding, nothing was found for any of the games here.
  Every vocabulary above is read off shipped data, not off a design statement.

---

## Sources

- [TrinityCore `SpellInfo.h`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.h)
- [TrinityCore `SpellInfo.cpp`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellInfo.cpp)
- [TrinityCore `SpellEffects.cpp`](https://raw.githubusercontent.com/TrinityCore/TrinityCore/master/src/server/game/Spells/SpellEffects.cpp)
- [Divinity Engine — Skill creation (official)](https://docs.larian.game/Skill_creation)
- [D&D 5.1 SRD — Casting a Spell](https://5thsrd.org/spellcasting/casting_a_spell/)
- [Open5e API — Scorching Ray](https://api.open5e.com/v1/spells/?search=scorching%20ray)
- [ffxiv_bossmod `AOEShapes.cs`](https://raw.githubusercontent.com/awgil/ffxiv_bossmod/master/BossMod/BossModule/AOEShapes.cs)
- [Warcraft wiki — Area of effect](https://warcraft.wiki.gg/wiki/Area_of_effect)
- [Warcraft wiki — Chain Heal](https://warcraft.wiki.gg/wiki/Chain_Heal)
- [PoEDB — Chain](https://poedb.tw/us/Chain)
- [Darkest Dungeon wiki — Combat Mechanics](https://darkestdungeon.wiki.gg/wiki/Combat_Mechanics_(Darkest_Dungeon))
- [Darkest Dungeon wiki — Duelist strategy (DD2)](https://darkestdungeon.wiki.gg/wiki/Duelist_(Darkest_Dungeon_II)/Strategy)
- [Red Hook — Darkest Dungeon II patch notes](https://www.darkestdungeon.com/patch-notes/)
- [Darkest Dungeon — official modding guide](https://steamcommunity.com/sharedfiles/filedetails/?id=819597757)
- [Darkest Dungeon — effects, buffs and skills parameter list](https://steamcommunity.com/sharedfiles/filedetails/?id=1095670238)
- [Darkest Dungeon — combat_skill worked example](https://steamcommunity.com/app/262060/discussions/0/612823460273726401)
- [PvZ wiki — Laser Bean](https://plantsvszombies.wiki.gg/wiki/Laser_Bean)
- [PvZ wiki — Threepeater](https://plantsvszombies.wiki.gg/wiki/Threepeater_(PvZ))
- [PvZ wiki — Starfruit](https://plantsvszombies.wiki.gg/wiki/Starfruit_(PvZ))
- [PvZ wiki — Cattail](https://plantsvszombies.wiki.gg/wiki/Cattail_(PvZ))
- [PvZ wiki — Magnet-shroom](https://plantsvszombies.wiki.gg/wiki/Magnet-shroom_(PvZ))
- [PvZ wiki — Cherry Bomb](https://plantsvszombies.wiki.gg/wiki/Cherry_Bomb_(PvZ))
- [PvZ wiki — Plants vs. Zombies Heroes](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Heroes)
- [Hearthstone wiki — Advanced rulebook](https://hearthstone.wiki.gg/wiki/Advanced_rulebook)
- [Hearthstone wiki — Area of effect](https://hearthstone.wiki.gg/wiki/Area_of_effect)
- [Hearthstone wiki — Minion](https://hearthstone.wiki.gg/wiki/Minion)
- [Slay the Spire wiki — Sword Boomerang](https://slaythespire.wiki.gg/wiki/Sword_Boomerang)
- [Correlated Randomness in Slay the Spire](https://forgottenarbiter.github.io/Correlated-Randomness/)
- [sts2-rng-fix](https://github.com/ing-gom/sts2-rng-fix)
- [FFXII gambit list — jegged.com](https://jegged.com/Games/Final-Fantasy-XII/Gambits/)
- [FFT Battle Mechanics Guide](https://gamefaqs.gamespot.com/ps/197339-final-fantasy-tactics/faqs/3876)
- [FF Hacktics — Vertical Tolerance](https://ffhacktics.com/wiki/Vertical_Tolerance)
- [Into the Breach weapon guide](https://gamefaqs.gamespot.com/pc/205477-into-the-breach/faqs/76363/weapons)
- [HIVE — splash damage fields](https://www.hiveworkshop.com/threads/how-to-give-a-unit-splash-damage-via-item-spell-or-aura.311818/)
- [HIVE — Chain Lightning object-editor fields](https://www.hiveworkshop.com/threads/scaling-ability-damage-on-chain-lightning.267275/)
