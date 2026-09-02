# How shipped RPGs taxonomise skills — and how many top-level categories they actually use

**Captured 2026-09-02.** Research only. Nothing here is a proposal.

**Scope question:** is a closed set of 5 top-level action categories enough, too few, or too many for a
roster of ~900 creatures? Answered with counts from shipped data.

---

## The finding in one paragraph

**Top-level "what it does" category counts do not scale with roster size — they cluster at 3 to 8 and
stay there whether the game has 232 creatures or 4,748.** Pokémon ships 3 move categories over 1,025
species and 937 moves. D&D 5e ships 8 schools over 3,207 creatures. Arknights ships 3 skill types over
374 operators and 1,352 skills. Final Fantasy XIV ships 6 action categories over 1,332 player actions.
Persona 5 ships 5 non-element skill classes. Pathfinder 2e's Remaster **deleted** its 8 schools and
replaced them with nothing at the top level. What *does* grow with content is always a **second,
larger, flat vocabulary** underneath: 18 recruitment tags in Arknights, 47 display tags plus 117
internal types in Path of Exile, 262 action traits in PF2e, 278 skill tags in Diablo IV, 665 aura
types in World of Warcraft. And the games that carry the most content per category are the ones that
**cross two orthogonal axes** — what a skill does versus how it is delivered — which Larian documents
first-party as `Ability` × `SkillType`, and which Square Enix ships as `ActionCategory` × `CastType` ×
`AttackType` × `Aspect`. On the evidence, 5 top-level categories at ~900 creatures is **normal to
slightly generous**, and the pressure point is not the category count at all: it is whether the second
vocabulary (this project's 8 tags) can carry the load, and whether the delivery axis is genuinely
separate.

---

## How to read this file

- **FACT** lines carry a source URL inline. **INFERENCE** lines are mine and are labelled.
- Numbers marked **(computed)** are tallies I ran over primary data files, not quotes.
- Wiki prose is marked **[wiki]** and treated as second-tier. Datamines, official API exports, and
  first-party developer documentation are preferred and are used wherever they were reachable.
- Prior rounds already covered roster scale, typing matrices, and rarity ladders. See
  [../game-design/README.md](../game-design/README.md) and
  [../game-design/03-roster-scale.md](../game-design/03-roster-scale.md) — this file cites those
  findings and goes past them rather than re-deriving them.
- [../game-design/06-unsourced.md](../game-design/06-unsourced.md) was read first. Its access notes
  held up and were extended; see §9.

**This project's shipped vocabularies, for reference** (`src/FusionRpg.Core/Actions/ActionEnums.cs`):

| Vocabulary | Values | Count |
|---|---|---|
| `ActionCategory` | Attack, Defense, Support, Movement, Status | 5 |
| `ActionTag` | offensive, defensive, heal, buff, debuff, movement, summon, utility | 8 |
| `ActionKind` | Basic, Innate, Skill | 3 |
| `ActionTargetMode` | Self, Single, Multi, RolledTarget, All, Area | 6 (+4 area shapes) |

---

## 1. The headline table — categories per 100 creatures

Top-level category field, its value count, and the creature roster it serves.

| Game | Top-level field | Values | Of which non-elemental | Creature roster | Values / 100 creatures |
|---|---|---:|---:|---:|---:|
| D&D 5e (SRD) | spell `school` | 8 | 8 | 3,207 | **0.25** |
| Pokémon (Gen 9) | move `damage_class` | 3 | 3 | 1,025 | **0.29** |
| Fire Emblem Heroes | skill category | 5 | 5 | ~1,000 | **~0.5** |
| **This project** | `ActionCategory` | **5** | **5** | **~900** | **0.56** |
| Arknights | `skillType` | 3 | 3 | 374 | **0.80** |
| Shin Megami Tensei V: V | skill element code | 14 | 7 | 306 | **4.6** (2.3 non-elem) |
| Persona 5 Royal | skill element code | 16 defined (13 in use) | 6 | 232 | **6.9** (2.6 non-elem) |
| Pathfinder 2e (Remaster) | *none* | 0 | 0 | 4,748 | **0.00** |

Denominators that are not creature rosters, kept separate because they are not comparable:

| Game | Top-level field | Values | Denominator |
|---|---|---:|---|
| Final Fantasy XIV | `ActionCategory` | 6 in use (4 combat) | 1,332 player actions, 43 class/job rows |
| Diablo IV | `tPrimaryTag` | 23 `Skill_Primary_*` (~6 live per class) | 6 classes |
| Diablo II | *none* — positional only | 0 | 429 skills, 8 class ids |
| Baldur's Gate 3 | spell `school` | 8 | 595 spell pages |
| Divinity: Original Sin 2 | `Ability` (school) | 10 combat schools of a 39-value enum | — |
| Path of Exile | *none* — flat tag bag | 0 | 1,010 active + 448 support gems |
| World of Warcraft | spell school | 7 | hundreds of thousands of spell rows |

**The band is 3–8, and it does not move with roster size.** 3,207 creatures gets 8 categories; 1,025
gets 3; 374 gets 3. The one game that pushes past 8 (SMT/Persona at 14–15) does so only because it
folds **element into the same field** — strip the elements and it is back to 5–7.

**INFERENCE:** 5 categories for ~900 creatures sits in the middle of the observed band and is closer to
generous than to tight. No shipped game in this sample needed more than 8 at the top level, and two of
the largest rosters in the sample (D&D 5e, PF2e) run on 8 and 0 respectively.

---

## 2. One taxonomy or two? Where "what it does" and "how it is delivered" separate

This is the structural question, and the sample splits cleanly.

### Games that ship two crossed axes

| Game | "What it does" axis | "How it is delivered" axis | Evidence |
|---|---|---|---|
| Divinity: Original Sin 2 | `Ability` — 10 combat schools | `SkillType` — **17 values** | First-party: [docs.larian.game](https://docs.larian.game/Skill_creation) |
| Baldur's Gate 3 | `school` — 8 | spell-type prefix — 7 observed on spells | uid field, [bg3.wiki](https://bg3.wiki) [wiki] |
| Final Fantasy XIV | `ActionCategory` — 6 | `CastType` — 10 values in use, `EffectRange`, `TargetArea` | [v2.xivapi.com](https://v2.xivapi.com/api/sheet/Action) |
| Pokémon | `damage_class` — 3 | `target` — 16 | [pokeapi.co](https://pokeapi.co/api/v2/move-target) |
| D&D 5e | `school` — 8 | `casting_time` — 9, `area_of_effect.type` — 5 | [dnd5eapi.co](https://www.dnd5eapi.co/api/2014/spells) |
| SMT / Persona | element code — 14/15 | target string — 5 to 9 values | fusion-tool data export |
| Diablo IV | `tPrimaryTag` — one per skill | `arSkillTags` — bag of 61 `Skill_*` | [blizzhackers/d4data](https://github.com/blizzhackers/d4data) |
| World of Warcraft | school (7) + mechanic (37) | effect (360) + aura type (665) | TrinityCore headers |

**Larian states the separation explicitly and first-party.** From the official Divinity Engine Wiki:

> "Skill Type determines the fundamental behavior of the skill and what parameters are available to
> edit."
> …
> "**Ability**, Which ability tree this skill belongs to, which determines how it is sorted in the
> skill menu and which primary attribute scales its damage."
> …
> "When referencing the skill (e.g., in a skillbook), the skill is referred to by
> ***Skilltype_Skillname***"

— <https://docs.larian.game/Skill_creation>

The delivery type is literally half the skill's identifier. The school is the other axis and drives
menu sorting plus damage scaling. **FACT.**

BG3 kept the same shape: `Fire Bolt` carries `school = Evocation` and `uid = Projectile_FireBolt`
(<https://bg3.wiki/wiki/Fire_Bolt> [wiki] — the uid field is copied from the game's stat entry).

### Games that ship one flat vocabulary

| Game | Shape | Size |
|---|---|---|
| Path of Exile | one tag bag per gem; delivery words (`Projectile`, `Nova`, `Slam`, `Melee`, `Area`, `Chaining`) live in the same bag as function words (`Aura`, `Curse`, `Minion`, `Guard`) | 47 named display tags |
| Pathfinder 2e | one trait bag per action; no top-level category at all | 262 action traits |
| Arknights | no semantic vocabulary on skills at all | 3 activation types |

### The delivery axis is always long-tailed

**FACT (computed).** Pokémon ships 16 target values, but **664 of 937 moves (71%) use exactly one of
them** (`selected-pokemon`). FFXIV ships 10 `CastType` values in use, but 876 of 1,332 player actions
(66%) use `CastType = 1`. BG3 ships at least 7 spell-type prefixes, but 377 of 595 spell pages (63%)
are `Target_`.

**INFERENCE:** a delivery enum earns its size from the tail, not the head. Two thirds of content sits
on one value in every game measured. That is the normal shape, not a sign the enum is oversized.

---

## 3. D&D 5e and Pathfinder 2e

### D&D 5e — 8 schools, and they buy almost nothing

**FACT.** The SRD ships exactly 8 schools of magic:
Abjuration, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation.
<https://www.dnd5eapi.co/api/2014/magic-schools>

**FACT (computed)** over all 319 SRD spells (<https://www.dnd5eapi.co/api/2014/spells>):

| Axis | Values | Distribution |
|---|---:|---|
| School | 8 | Evocation 60, Transmutation 59, Conjuration 52, Abjuration 39, Enchantment 29, Divination 29, Illusion 27, Necromancy 24 |
| Casting time | 9 | 1 action 242, 1 minute 31, 10 minutes 15, 1 bonus action 14, 1 hour 10, 1 reaction 4, 8/12/24 hours 1 each |
| Area shape | 5 (+none) | sphere 39, cube 26, cylinder 9, line 8, cone 6; 231 spells have no area |
| Damage type | 13 (+none) | Fire 16, Radiant 8, Necrotic 7, Force 6, …; 255 spells deal no typed damage |

**Action economy is a separate, tiny enum:** action, bonus action, reaction, free/no-action, plus
movement. The SRD spell list touches only 4 of those (action / bonus action / reaction / longer
rituals).

**What the school buys mechanically:** very little. It is read by a handful of class features
(abjuration wards, necromancy save DCs, illusion dispelling), by *dispel magic*-adjacent checks, and by
character-build gating for specialist wizards. Nothing in the core damage or saving-throw resolution
reads the school.

**What breaks if a school is wrong:** almost nothing. A misfiled spell is a flavour error plus a small
number of specialist-wizard interactions. **This is a taxonomy with near-zero mechanical load — and it
is the one everybody copies.**

### Pathfinder 2e — the schools were deleted

**FACT.** The Remaster removed the 8 schools of magic. Paizo Senior Designer James Case discussed the
removal; the wizard's arcane schools were rebuilt around in-fiction study paths (School of Battle
Magic, School of Civic Wizardry, School of Mentalism) rather than spell categories. Reported at
<https://www.wargamer.com/pathfinder/remaster-changes> and
<https://www.belloflostsouls.net/2023/09/pathfinder-paizo-previews-the-wizard-remastered.html>
[second-tier reporting on a first-party preview]. `Illusion` survives as a *trait*, not a school.

**FACT (computed)** over the Foundry PF2e system's canonical trait config
(<https://github.com/foundryvtt/pf2e/blob/master/src/scripts/config/traits.ts>) — this file is the
machine-readable list the game system actually indexes on:

| Trait group | Count |
|---|---:|
| `magicTraditions` (arcane / divine / occult / primal) | 4 |
| `sanctificationTraits` (holy / unholy) | 2 |
| `elementTraits` | 6 |
| `damageTraits` | 22 |
| `classTraits` | 25 |
| `spellTraits` (includes the above by spread) | 112 |
| `actionTraits` | **262** |
| `creatureTraits` | 223 |
| `featTraits` | 232 |
| `weaponTraits` | 226 |
| `npcAttackTraits` | 280 |
| **Union across every trait group** | **615** |
| Spell schools | **0** |

**Action types** are a separate 4-value enum — Action (costing 1, 2 or 3 actions), Free Action,
Reaction, Passive (`PF2E.ActionType*` in
<https://github.com/foundryvtt/pf2e/blob/master/static/lang/en.json>).

**What traits buy mechanically:** everything. `incapacitation` changes the outcome step for
higher-level targets. `manipulate` triggers reactions. `concentrate` interacts with conditions.
`fortune`/`misfortune` cannot stack. `attack` adds to the multiple-attack penalty. Traits are the rules
engine's dispatch keys.

**What breaks if a trait is wrong:** a real rules bug. A missing `attack` trait silently removes the
multiple-attack penalty; a missing `incapacitation` trait makes a save-or-die work on a boss.

**INFERENCE — the most useful contrast in this file.** Two systems of near-identical size and lineage
took opposite paths. 5e keeps a decorative 8-value top-level category and puts almost no load on it.
PF2e deleted its top-level category and moved *all* the load onto a 262-value flat trait bag. Both
ship. The category count was never the load-bearing decision; **where the mechanical dispatch happens
was.**

---

## 4. Final Fantasy

### The series-level ability taxonomy is four families

**FACT [wiki].** The Final Fantasy Wiki's own definitions:

> "**Command abilities**, also called **action abilities** (アクションアビリティ), are abilities shown
> in the battle command window. Commands that have a sub-menu within battle such as White Magic are
> sometimes called **skillsets**."
> — <https://finalfantasy.fandom.com/wiki/Command_ability>

> "**Reaction abilities** (リアクションアビリティ) are abilities that require a trigger to be
> activated" — <https://finalfantasy.fandom.com/wiki/Reaction_ability>

> "**Support abilities** (サポートアビリティ) are abilities that enhance a character. They are also
> called **passive abilities**, **job traits**, or **auto-abilities**."
> — <https://finalfantasy.fandom.com/wiki/Support_ability>

> "**Movement abilities** (ムーブアビリティ) are abilities that once equipped, are activated when a
> character just takes a step. … Each character can equip one movement ability."
> — <https://finalfantasy.fandom.com/wiki/Movement_ability>

So: **Command / Reaction / Support / Movement — 4 families**, with the *skillset* as an intermediate
grouping between the command menu and the individual ability. Black Magic, White Magic, Time Magic and
Blue Magic are **skillsets**, not top-level categories; they sit one level below the command.

**What it buys:** the equip slots. In the *Tactics* games each unit fills one action-ability slot
(the secondary job's skillset), one reaction, one support and one movement. The family *is* the slot.

**What breaks if a family is wrong:** the ability becomes unequippable or occupies the wrong slot,
which is a build-integrity bug rather than a resolution bug.

### Final Fantasy VII materia — a 5-value closed enum on the item, not the skill

**FACT [wiki].** Five materia types: **Summon, Magic, Command, Support, Independent**.
<https://finalfantasy.fandom.com/wiki/Materia_(Final_Fantasy_VII)>

**What it buys:** the linked-slot rule. Support materia modifies the Magic or Summon materia it is
linked to; Command materia adds a menu entry; Independent materia grants passives. **The type is the
legality rule for pairing.** Same mechanism family as PoE support gems (§6) and Persona fusion
inheritance (§7), at much smaller scale.

### Final Fantasy XIV — the deepest shipped example, and it is crossed

**FACT (computed)** from the game's own `Action` and `ActionCategory` sheets via
<https://v2.xivapi.com/api/sheet/Action> and <https://v2.xivapi.com/api/sheet/ActionCategory>.

`ActionCategory` sheet: 19 rows, 14 distinct names —
`Auto-attack, Spell, Weaponskill, Ability, Item, DoL Ability, DoH Ability, Event, Limit Break, System,
Mount, Special, Item Manipulation, Artillery`.

Across **51,501 `Action` rows**, **1,332 are player actions with names**:

| ActionCategory | Player actions |
|---|---:|
| Ability | 585 |
| Spell | 348 |
| Weaponskill | 229 |
| DoL Ability (gathering) | 84 |
| DoH Ability (crafting) | 58 |
| Limit Break | 27 |
| (blank) | 1 |

**Combat content: 1,189 actions across 4 categories — 297 actions per category.**

**GCD vs oGCD is not a category. It is derived from a shared cooldown group.** Cooldown group 58 is the
global cooldown; 498 player actions share it. Cross-tabulated:

| Category | On GCD (group 58) | Off GCD |
|---|---:|---:|
| Spell | 300 | 48 |
| Weaponskill | 186 | 43 |
| Ability | 8 | 577 |
| Limit Break | 0 | 27 |

**INFERENCE:** the mapping is near-total (Spell/Weaponskill = GCD, Ability = oGCD) but is *not* the
category's definition — it is a separate field that happens to correlate. That is a deliberate
decoupling: an Ability can be put on the GCD without changing what it is.

Other axes carried on the same row, each independent of the category:

| Field | Distinct values in use | Note |
|---|---:|---|
| `CastType` | 10 | delivery shape; 876/1,332 use value 1 |
| `AttackType` | 7 | slashing / piercing / blunt / shot / magic / breath / sonic / limit break |
| `Aspect` | 8 | element; 681 are value 7 (unaspected), 565 are 0 (none) |
| `TargetArea` | 2 | ground-targeted or not — only 24 actions |
| `IsRoleAction` | flag | **62 actions** |
| `ClassJob` `Role` | 5 | none / tank / melee DPS / ranged DPS / healer |

**Role actions are a boolean flag on the action, not a category** (62 of 1,332). Job gauges are per-job
UI state and appear nowhere in the action taxonomy at all.

**What the category buys:** it is the input to the cast/recast and animation-lock model, to
"instant vs cast" handling, and to the buff text vocabulary ("Weaponskill damage +X%",
"Spell Speed"). Item-level and trait-level modifiers are written against it.

**What breaks if the category is wrong:** a whole class of stat modifiers stops applying — a
Weaponskill mislabelled as a Spell scales off the wrong speed stat and is missed by every
"Weaponskill" buff. This is a **live, damaging** miscategorisation, unlike D&D's schools.

---

## 5. World of Warcraft — small school enum, enormous everything else

All enum values below are **FACT**, quoted from the TrinityCore emulator's headers, which are
reverse-engineered directly from the retail client's DBC/DB2 tables and are the most complete public
transcription of them.

### Spell schools — 7, unchanged since 2004

```
enum SpellSchools : uint16 {
    SPELL_SCHOOL_NORMAL = 0, SPELL_SCHOOL_HOLY = 1, SPELL_SCHOOL_FIRE = 2,
    SPELL_SCHOOL_NATURE = 3, SPELL_SCHOOL_FROST = 4, SPELL_SCHOOL_SHADOW = 5,
    SPELL_SCHOOL_ARCANE = 6, MAX_SPELL_SCHOOL = 7 };
```
<https://github.com/TrinityCore/TrinityCore/blob/master/src/server/game/Miscellaneous/SharedDefines.h>

It is a **bitmask**, and that is the whole trick. `SpellSchoolMask` adds three composites —
`MASK_SPELL` (fire|nature|frost|shadow|arcane), `MASK_MAGIC` (holy + those), `MASK_ALL`. Multi-school
spells (Frostfire, Plague, Elemental, Chaos, Holystrike) are **combinations of the same 7 bits, not new
values** (<https://warcraft.wiki.gg/wiki/Magic_schools> [wiki]).

**What the school buys:** damage resistance and immunity, absorb-shield matching, and **interrupt
lockout** — an interrupt locks the school, and a multi-school spell stays castable unless *every* one
of its schools is locked. Talents and passives modify damage by school.

**What breaks if the school is wrong:** the spell becomes resistable/immune to the wrong things, the
wrong absorb shield eats it, and interrupt lockout stops working correctly in PvP. Real bug, real
severity.

### The other enums, and their sizes

| Enum | Count | What reads it |
|---|---:|---|
| `SpellSchools` | 7 | resistance, absorb, lockout |
| `SpellSchoolMask` | 11 members (7 bits + 3 composites + none) | same, as masks |
| `Mechanics` | **37** (`MECHANIC_NONE` … `MECHANIC_TAUNTED`, `MAX_MECHANIC = 37`) | crowd-control immunity, diminishing returns, "removes movement impairing effects" |
| `DispelType` | 12 declared; **4 in `DISPEL_ALL_MASK`** (Magic, Curse, Disease, Poison) plus Stealth, Invisibility, Enrage | who can remove what |
| `SpellEffects` | **360** (0 … 359, then `TOTAL_SPELL_EFFECTS`) | the effect handler dispatch table |
| `AuraType` | **665** (0 … 664, then `TOTAL_AURAS`) | the aura handler dispatch table |
| `SpellFamilyNames` | ~30 named values, sparse, max index 224 | per-class talent/proc matching |
| `SpellImmunity` | 8 axes (effect, state, school, damage, dispel, mechanic, id, other) | immunity checks |

`AuraType` is from
<https://github.com/TrinityCore/TrinityCore/blob/master/src/server/game/Spells/Auras/SpellAuraDefines.h>.

**The aura/buff/debuff typing is genuinely a separate system from the school**, exactly as expected:
`DispelType` answers "who can remove this", `Mechanics` answers "does this count as a stun for immunity
and diminishing returns", `AuraType` answers "which handler runs". A spell carries all four
independently.

### Global cooldown

**FACT [wiki].** Three GCD lengths: 1.5s standard, 1.0s for rogues / cat-form druids / monks, 0.5s for
empower abilities. Haste floors at 0.75s (1.0s before Legion) and does not affect the energy melee
classes. "As of Battle for Azeroth, a significant number of abilities which previously were not on the
global cooldown have been added to it."
<https://warcraft.wiki.gg/wiki/Global_cooldown>

**INFERENCE:** as in FFXIV, GCD membership is a per-spell property rather than a category, and Blizzard
has moved individual spells across the boundary in patches. Anything that made GCD-ness a category
value would have made those patches a data migration.

---

## 6. Path of Exile — the richest shipped skill taxonomy, and the transferable mechanism

All counts here are **FACT (computed)** over RePoE, the community's machine-readable export of the
game's own data files: <https://repoe-fork.github.io/gems.min.json> and
<https://repoe-fork.github.io/gem_tags.min.json>.

### The display tags — 54 keys, 47 with a name

Full key list from `gem_tags.json` (current fork):

```
fire, cold, lightning, chaos, physical, spell, cast, attack, projectile, bow, melee, strike, slam,
area, nova, chaining, duration, movement, travel, blink, channelling, trigger, minion, golem, totem,
trap, mine, aura, herald, banner, curse, hex, mark, brand, link, guard, stance, orb, warcry, vaal,
arcane, critical, exceptional, blessing, retaliation, pact, random_element (displays as "Prismatic"),
support, grants_active_skill, awakened, low_max_level, strength, dexterity, intelligence
```

- **54 keys total.** 7 have no display name (`strength`, `dexterity`, `intelligence`,
  `grants_active_skill`, `low_max_level`, `banner`, `awakened`) — the three attributes are the gem's
  stat-requirement colour, not a player-facing tag. **47 named display tags.**
- **53 distinct tags are actually used on gems** in the shipped data.
- Gem inventory: **1,458 gem entries — 1,010 active skill gems, 448 support gems.**
- **Median 6 display tags per gem** (range 0–12).

### The second, hidden vocabulary — 117 internal types

Every active skill also carries `active_skill.types`, an internal vocabulary that is **not shown to the
player** and is much larger:

- **117 distinct internal types.** Median **9 types per active skill**, mean 9.6, max 19.
- Examples of what only exists internally: `Triggerable`, `Trapped`, `Totemable`, `Mineable`,
  `Multistrikeable`, `MirageArcherCanUse`, `DisallowTriggerSupports`, `SkillGrantedBySupport`,
  `ProjectilesNumberModifiersNotApplied`, `AttackInPlaceIsDefault`, `RequiresShield`, `DualWieldOnly`.

**INFERENCE:** the player-facing tag list is a *readable summary*; the legality engine runs on a
vocabulary more than twice its size. The two are deliberately not the same list.

### How support gems gate — the mechanism worth transferring

A support gem is a predicate over the active skill's internal types. Three fields:

| Field | Meaning | Supports using it |
|---|---|---:|
| `allowed_types` | the skill must carry these | 405 of 448 |
| `excluded_types` | the skill must not carry these | 186 of 448 |
| `added_types` | the support **adds** these types to the skill | 110 of 448 |

And the predicate is **boolean, not a flat set**: `AND`, `OR` and `NOT` appear as pseudo-types inside
the lists. **89 of 448 support gems use a boolean operator.** 43 support gems have no `allowed_types`
at all — they support everything not explicitly excluded.

Most-used gates (**computed**):

| `allowed_types` | Count | `excluded_types` | Count |
|---|---:|---|---:|
| Attack | 126 | SummonsTotem | 111 |
| AND | 101 | RemoteMined | 108 |
| Damage | 100 | Trapped | 103 |
| Spell | 60 | InbuiltTrigger | 76 |
| Triggerable | 40 | Triggered | 54 |
| Projectile | 33 | HasReservation | 43 |
| AppliesCurse | 25 | DisallowTriggerSupports | 42 |
| Melee | 23 | Vaal | 39 |

A worked example — Added Fire Damage Support:

```json
"support_gem": {
  "allowed_types": ["Damage", "Attack"],
  "letter": "F",
  "supports_gems_only": false,
  "support_text": "Supports any skill that hits enemies."
}
```

**The composability is the point.** `added_types` means a support gem can *change what the skill is*,
which changes which other supports become legal. Spell Totem Support adds `SummonsTotem` — and 111
supports exclude `SummonsTotem`, so attaching it invalidates a large slice of the remaining pool. The
legality of a link is computed over a mutated type set, not the gem's printed tags.

**What the tags buy:** (a) support-gem legality, computed as above; (b) the entire modifier surface —
every "increased Projectile Damage", "more Area Damage", "Fire skills have +X% Critical Strike Chance"
affix on every item and passive matches on tags; (c) player search and filtering.

**What breaks if a tag is wrong:** a wrong display tag misleads the player and breaks modifier
matching, so the skill silently scales off the wrong half of a build. A wrong internal type breaks
support legality — either a nonsensical link becomes legal, or a sensible one is refused with no
explanation. Both are shipped-bug territory.

### Growth over time

**FACT (computed).** Comparing the archived `brather1ng/RePoE` snapshot against the current
`repoe-fork` export:

| | Keys |
|---|---:|
| Archived snapshot (`brather1ng/RePoE`, no longer updated) | 51 |
| Current (`repoe-fork`) | 54 |
| Added | `retaliation`, `pact`, `awakened`, `grants_active_skill` |
| Removed | `active_skill` (renamed to `grants_active_skill`) |

Two of the four additions are real player-facing tags (`Retaliation`, `Pact`); two are metadata. Over
the interval between those snapshots, across many content releases, the display vocabulary grew by
**two**. **The tag list is close to stable even in the game with the largest skill taxonomy in the
genre.**

---

## 7. The ARPG family — Diablo II, Diablo IV, and the rest

### Diablo II — zero semantic categories, pure position

**FACT (computed)** over the game's own `skills.txt` and `skilldesc.txt`
(<https://github.com/blizzhackers/d2data>):

- **429 skill rows**; **exactly 30 per class**, across 8 class ids.
- `SkillPage` ∈ {1, 2, 3} — the three skill tabs. `SkillColumn` ∈ {1, 2, 3}. `SkillRow` ∈ {1 … 6}.
- **There is no cross-class skill category field at all.** A skill's identity is
  class × tab × column × row.

**What it buys:** the prerequisite graph and the skill-tree UI, nothing else. **What breaks if it is
wrong:** the tree renders wrong and prerequisites point at the wrong node.

**INFERENCE:** this is the "no taxonomy" extreme, and it worked because Diablo II never needed to write
a rule about "all fire skills" or "all attack skills" that spanned classes. The moment items started
saying "+2 to Fire Skills", the game needed a vocabulary — and D2 supplied it via per-class,
per-tab item modifiers rather than a global tag enum.

### Diablo IV — one primary tag plus a large secondary bag, with cosmetic tags flagged

**FACT (computed)** over `GameBalance/SkillTags.gam.json` in
<https://github.com/blizzhackers/d4data>:

- **278 skill tag entries total.**
- **26 are flagged `bIsPrimary`** — of which **23 are `Skill_Primary_*`**:
  `Basic, Core, Defensive, Ultimate, Mastery, Conjuration, Brawling, Weapon_Mastery, Fury, Agility,
  Subterfuge, Imbuements, Corpse, Curse, Macabre, Summoning, Companion, Wrath, Major_Destruction,
  Minor_Destruction, Spirit, Combat, Essence` (plus `Skill_Dismount` and two monster tags).
- **61 secondary `Skill_*` tags** — elements (`Fire`, `Cold`, `Lightning`, `Shadow`, `Poison`,
  `Physical`), mechanics (`Channeled`, `Chain`, `Mobility`, `Trap`, `Shout`, `Pet`, `Bleed`), themes
  (`Werewolf`-adjacent `Shapeshifting`, `Bone`, `Blood`, `Nature_Magic`).
- **121 `FILTER_*` and 46 `Search_*` entries** exist only for the UI. Individual skills carry these
  with `"bSearchOnly": true` — the data explicitly marks which tags are non-mechanical.

A skill carries **one** `tPrimaryTag` and a bag of `arSkillTags`. Sorcerer Fireball:
`tPrimaryTag = Skill_Primary_Core`, `arSkillTags = [Skill_Fire (mechanical), Search_Damage
(search-only), …]`.

**INFERENCE:** the primary tags are per-class. Basic / Core / Defensive / Ultimate are shared; Brawling,
Weapon Mastery and Fury are Barbarian; Conjuration and Mastery are Sorcerer; Corpse, Curse, Macabre and
Summoning are Necromancer; Agility, Subterfuge and Imbuements are Rogue; Companion, Wrath and the two
Destruction tiers are Druid. **That puts roughly 6 primary categories in front of any one player at a
time, out of 23 across the game.**

**What it buys:** item affixes and paragon nodes ("+X% damage to Core Skills", "+Ranks to Fire
Skills"), skill-tree grouping, and the search UI. **What breaks if wrong:** the affix economy misfires
silently — the most common and most expensive class of ARPG bug.

**INFERENCE — the most reusable idea in the D4 data.** `bSearchOnly` and the `FILTER_*` / `Search_*`
prefixes mean the shipped data draws an explicit line between *tags the rules read* and *tags the UI
reads*. 167 of 278 entries — 60% — are on the cosmetic side of that line and are visibly labelled as
such.

### Diablo III, Last Epoch, Grim Dawn

Not obtained. See §9.

**What is known well enough to state without a source (flagged as such):** Last Epoch uses a flat
PoE-style tag bag. From the one skill-list page that could be retrieved
(<https://www.lastepochtools.com/skills/>, retrieved via reader proxy), tags observed on individual
skills include `Melee, Spell, Bow, Throwing, Physical, Fire, Cold, Lightning, Void, Poison, Area,
Damage Over Time, Minion, Aura, Movement, Traversal, Channeled, Buff, Summon, Ward, Mana` plus the five
attributes `Strength, Dexterity, Intelligence, Attunement, Vitality` — **25 distinct tags on one page
(computed)**. The complete list could not be enumerated.

---

## 8. Shin Megami Tensei and Persona — the closest structural match to this project

A large monster roster, a small closed skill vocabulary, and fusion inheritance. This is the shape this
project has, so the numbers matter more here than anywhere else.

**FACT (computed)** over the shipped compendium data in
<https://github.com/aqiu384/megaten-fusion-tool> (`src/app/smt5/data/`, `src/app/p5/data/`), which
mirrors the games' own skill and demon tables.

### Shin Megami Tensei V: Vengeance

| | |
|---|---|
| Demons | **306** (242 base + 97 Vengeance-added, union of both tables) |
| Skills | **763** (444 base + 352 Vengeance, union) |
| Distinct skill element codes | **14** |

The 14 codes split into three purposes, and the game's own config file names the split:

| Group | Codes | Count |
|---|---|---:|
| `resistElems` — the resistance-table columns | `phy, fir, ice, ele, for, lig, dar` | **7** |
| `affinityElems` — non-resisted functional classes | `alm` (almighty), `ail` (ailment), `rec` (recovery), `sup` (support) | **4** |
| `skillElems` — structural | `spe` (special/unique), `pas` (passive), `inn` (innate) | **3** |

Skill counts by code: `inn` 182, `alm` 32, `sup` 29, `phy` 20, `rec` 20, `ice` 12, `fir` 9, `for` 9,
`ail` 9, `spe` 8, `lig` 7, `dar` 6, `pas` 5, `ele` 4 (Vengeance table).

**Target is a separate field** with 9 distinct strings: `1 foe, All foes, Rand foes, 1 ally,
All allies, Self, All stock, Universal, -`.

Status ailments are their own 6-value enum: `Charm, Seal, Panic, Poison, Sleep, Mirage`.

### Persona 5 / Royal

| | P5 | P5 Royal |
|---|---:|---:|
| Personas | not counted here | **232** |
| Skills | **378** | **234** (Royal-only table) |
| Distinct element codes observed | **15** | **13** |

The config splits them:

| Group | Codes | Count |
|---|---|---:|
| `resistElems` | `phy, gun, fir, ice, ele, win, psy, nuk, ble, cur` | **10** |
| `skillElems` | `alm, ail, rec, sup, pas` (+ `tra` = traits, Royal only) | **5** (6 in Royal) |
| `inheritElems` — the fusion inheritance columns | `phy, gun, fir, ice, ele, win, psy, nuk, ble, cur, ail, rec` | **12** |

**What the element code buys — and this is the whole answer for a monster game:**

1. **It is the resistance-table column set.** A demon's `resists` string is indexed by `resistElems` in
   exactly that order. SMT V demons carry a 7-character string; Persona 5 demons a 10-character one.
   The skill's category and the target's defence grid are **the same enum**. There is no mapping layer.
2. **It gates fusion inheritance.** Persona 5 ships a 14 × 12 bitmask: each Persona has one of 14
   *inheritance types* (`phys, fire, ice, elec, wind, psy, nuke, bless, curse, ailment, healing,
   support, almighty, none`), and that type is a 12-bit mask over the 12 `inheritElems` saying which
   skill classes it may inherit in fusion. `phys` = `110000000011`; `support` and `almighty` =
   `111111111111`; `none` = all zeros.
3. **It drives the Press Turn / One More economy** — hitting a weakness grants an extra action, and
   weakness is read off the resistance grid, which is indexed by the element code.

**What breaks if the code is wrong:** the demon resists the wrong thing, the Press Turn economy breaks
in both directions, and fusion produces a Persona that cannot learn the skills it is supposed to. This
is the highest-stakes categorisation in the whole sample.

**INFERENCE — the number that matters most for this project.** SMT and Persona put **element and
function into one field** and pay for it with 14–15 values. Strip the elements out and the functional
vocabulary is **4 in SMT V (almighty / ailment / recovery / support)** and **5 in Persona 5** (6 in Royal), over
rosters of 306 and 232. That is the direct comparator for a 5-value `ActionCategory`, and it lands on
the same number.

**And crucially, both games hold the vocabulary constant while the roster grows.** SMT V: Vengeance
added 97 demons and 352 skill rows to the base game and added **zero** new element codes.

---

## 9. Arknights, Fire Emblem, Darkest Dungeon, Divinity, Baldur's Gate 3

### Arknights — the "no skill taxonomy at all" case

**FACT (computed)** over the game's shipped `skill_table.json` and `character_table.json`
(<https://github.com/Kengxxiao/ArknightsGameData_YoStar>, `en_US/gamedata/excel/`):

| | |
|---|---|
| Skills | **1,352** |
| `skillType` | **3** — `MANUAL` 624, `AUTO` 379, `PASSIVE` 349 |
| `spType` (charge mode) | **4** — `INCREASE_WITH_TIME` 838, on-deploy (`8`) 391, `INCREASE_WHEN_ATTACK` 102, `INCREASE_WHEN_TAKEN_DAMAGE` 21 |
| `durationType` | **2** — `NONE` 1,322, `AMMO` 30 |
| Operators | **374** (EN data; CN is ahead) |
| `profession` | **8** — Warrior 74, Sniper 53, Caster 52, Special 45, Tank 42, Support 39, Pioneer 36, Medic 33 |
| `subProfessionId` | **67** distinct |
| `position` | **2** — RANGED 187, MELEE 187 |
| `tagList` (recruitment) | **18** |

The 18 recruitment tags: `DPS 202, Support 54, Defense 51, Healing 50, Survival 49, Crowd-Control 48,
DP-Recovery 34, AoE 34, Slow 24, Nuker 23, Fast-Redeploy 23, Debuff 21, Summon 14, Elemental 12,
Shift 11, Robot 7, Starter 5, Soar 2`.

**There is no "what it does" field on a skill.** The only skill-level classification is *when it fires*
and *how it charges*. All semantic role information lives on the **operator** — profession,
sub-profession, and the 18 tags — never on the skill.

**What the taxonomy buys:** `skillType`/`spType` drive the activation loop and the UI button state.
`profession`/`subProfession` drive deployment cost, block count, and attack behaviour.
The 18 `tagList` values are the **recruitment filter** — the gacha's headhunting pool is computed
from tag intersections, which is the only mechanical use of a semantic vocabulary in the whole system.

**What breaks if wrong:** a wrong `spType` makes a skill uncharageable; a wrong recruitment tag lets
players target-pull a 6★ they should not be able to reach — a monetisation bug, and historically the
kind that gets hotfixed fastest.

**INFERENCE:** Arknights is the proof that a 374-creature roster with 1,352 skills can ship with a
3-value skill taxonomy, *provided the role vocabulary lives on the unit rather than the skill*. Note
the shape of its unit-level vocabulary: **18 tags** — remarkably close to this project's 5 categories
plus 8 tags plus 6 target modes.

### Fire Emblem Heroes — the slot is the category

**FACT [wiki].** "Skills are split into 5 categories… A character can only have one weapon, assist
skill, and special skill equipped. For passives, these are divided into 4 categories" — A, B, C, and X
(Attuned). <https://feheroes.fandom.com/wiki/Skills>

So: **Weapon / Assist / Special / Passive / Sacred Seal — 5 top-level categories**, with passives
further split across 4 slots.

**What it buys:** equipping and skill inheritance. A skill can only be inherited into the slot it
belongs to, so the category is the inheritance legality rule. **What breaks if wrong:** the skill
becomes uninheritable or lands in the wrong slot, breaking build legality — the same failure class as
FF's ability families and PoE's support gems.

### Divinity: Original Sin 2 — the clearest first-party statement of a crossed taxonomy

Covered in §2. The full **17-value `SkillType`** from
<https://docs.larian.game/Skill_creation>:

`Target, Shout, Projectile, ProjectileStrike, Cone, Zone, Jump, MultiStrike, Rush, Teleportation,
Summon, Wall, Dome, Rain, Storm, Tornado, Quake`

And the ability enum it crosses with, from <https://docs.larian.game/Scripting_ability_types> — **39
values**, of which the combat schools players know are `FireSpecialist` (Pyrokinetic),
`WaterSpecialist` (Hydrosophist), `AirSpecialist` (Aerotheurge), `EarthSpecialist` (Geomancer),
`Necromancy`, `Summoning`, `Polymorph`, `WarriorLore` (Warfare), `RangerLore` (Huntsman), `RogueLore`
(Scoundrel), plus `Sourcery` — **11**. The other 28 are civil abilities and weapon proficiencies.

**What it buys:** `SkillType` selects the behaviour implementation and which parameters the skill even
has. `Ability` sorts the skill menu and picks the scaling attribute. **What breaks if wrong:** a wrong
`SkillType` means the skill's parameter block does not match its executor — a hard failure, not a
balance slip. A wrong `Ability` means the skill scales off the wrong attribute and appears in the wrong
menu tab.

### Baldur's Gate 3 — 5e's schools plus a delivery prefix plus a flag bag

**FACT (computed)** over 595 spell pages harvested from <https://bg3.wiki> [wiki] (the `uid` field
reproduces the game's stat entry name):

| Axis | Values | Distribution |
|---|---:|---|
| `uid` prefix (SpellType) | **7** in use | Target 377, Shout 118, Projectile 53, Zone 21, Wall 5, Teleportation 4, Throw 1 |
| `school` | **8** | Evocation 132, Conjuration 94, Transmutation 75, Necromancy 74, Enchantment 65, Abjuration 58, Illusion 58, Divination 9 |
| cost | action / bonus / reaction × spell slot level | action 165, action+slot 313, bonus 51, bonus+slot 33, reaction 5 |
| spell flags | **~35** real values | IsSpell 549, HasVerbalComponent 517, HasSomaticComponent 463, IsHarmful 242, IsConcentration 165, IsMelee 101, CannotTargetItems 71, IsEnemySpell 34, … |

**INFERENCE:** BG3 inherits 5e's decorative 8-school taxonomy and adds two vocabularies 5e does not
have — a delivery prefix and a ~35-value flag bag — because a video game has to *execute* the spell,
which a tabletop rulebook does not.

### Darkest Dungeon

Not obtained. See §10.

---

## 10. The JRPG command menu, and where it came from

**FACT [wiki].** Final Fantasy's own terminology, quoted in full in §4: command abilities are
"abilities shown in the battle command window", and a command with a sub-menu — White Magic, Black
Magic, Blue Magic — is a **skillset**. That is a **two-level menu, not a two-level taxonomy**:
`Attack / Magic / Skill / Item / Defend / Run` are menu entries, and only some of them open a
sub-list.

**INFERENCE — what the near-universal command menu actually is.** Across the JRPG and tactics
tradition the top-level command list is a **UI affordance sized to a controller**, not a semantic
classification. It has stayed at roughly 4–6 entries for four decades for the same reason mobile tab
bars have: that is how many top-level choices fit on one screen and in one thumb-reach. Two independent
observations support reading it that way:

1. The entries are not parallel. `Attack` is one action; `Magic` is a *sub-menu*; `Item` is a different
   resource system entirely; `Run` exits the encounter. A real taxonomy would not mix those.
2. The set is per-game and per-job, not per-series. Final Fantasy's own wiki says so directly: "Each
   game has a different set of battle commands"
   (<https://finalfantasy.fandom.com/wiki/Command_ability>).

**INFERENCE:** the number 4–6 recurs across command menus, Diablo IV's per-class primary tags (~6),
FFXIV's combat categories (4), Persona's non-element skill classes (5), and FEH's skill categories (5)
— and that convergence is more likely a **presentation constraint** than a discovered truth about how
abilities decompose. It is nonetheless the strongest empirical anchor available for "how many top-level
buckets a player will hold in their head."

---

## 11. Where taxonomies grew, shrank, or moved — the strongest signal

| Game | Change | Direction | Source |
|---|---|---|---|
| Pathfinder 2e | 8 schools of magic **removed** in the Remaster; replaced by no top-level category, with the load pushed onto ~262 action traits | **Shrink to zero** | [wargamer.com](https://www.wargamer.com/pathfinder/remaster-changes) |
| Pokémon | Physical/Special was **a property of the type** through Gen 3; from Gen 4 it became **a property of the move** | **Moved axes** | [Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/Damage_category) [wiki] |
| Path of Exile | Display tags 51 → 54 keys between the archived and current data snapshots; 2 real new player-facing tags (`Retaliation`, `Pact`) | **Near-flat growth** | RePoE (computed) |
| World of Warcraft | 7 schools unchanged since 2004; multi-schools added as **bit combinations**, not new values. Meanwhile `AuraType` reached **665** and `SpellEffects` **360** | **Top stable, bottom explodes** | TrinityCore headers |
| World of Warcraft | "As of Battle for Azeroth, a significant number of abilities which previously were not on the global cooldown have been added to it" | **Reclassification, not growth** | [warcraft.wiki.gg](https://warcraft.wiki.gg/wiki/Global_cooldown) |
| Shin Megami Tensei V → Vengeance | +97 demons, +352 skill rows, **+0 element codes** | **Flat** | fusion-tool data (computed) |
| Diablo IV | Primary tags grew as classes shipped — Spiritborn's `Skill_Primary_Spirit` / `_Combat` and Druid's two Destruction tiers | **Grows with classes, not with skills** | d4data (computed) |
| Persona 5 → Royal | +1 skill class (`tra`, traits); resistance columns unchanged at 10 | **+1 in a decade** | fusion-tool data (computed) |

**The Pokémon case is the single most instructive.** Bulbapedia, quoting the shipped behaviour:

> "In games prior to Generation IV, the type of a damaging move determines whether the move is
> physical … or special … It was first assigned to individual moves in *Pokémon XD: Gale of Darkness*
> for Shadow moves, and then applied to all moves starting in Generation IV."

The number of values never changed — it was 2 (plus status) before and after. What changed is **which
entity carries the field.** That is the migration that actually happens to skill taxonomies, and it is
much more disruptive than adding a value, because every piece of content has to be re-authored.

**INFERENCE — the summary of this section.** In this entire sample, across roughly two decades of live
service, **no game's top-level skill category count grew by more than one or two.** The observed
changes are: deletion (PF2e), axis migration (Pokémon), reclassification of individual entries across a
fixed boundary (WoW's GCD), and per-class additions bundled with new classes (D4). Growth pressure was
absorbed every time by the **second vocabulary**, which grew freely — WoW's aura types to 665, PF2e's
traits to 615 across all groups, D4's tags to 278, PoE's internal types to 117.

---

## 12. What each taxonomy buys, and what breaks — consolidated

| Game | Taxonomy | Who reads it | What breaks if a value is wrong | Load |
|---|---|---|---|---|
| D&D 5e | 8 schools | a few class features | flavour + specialist-wizard edge cases | **Very low** |
| PF2e | 262 action traits | the rules engine's dispatch | real rules bugs (MAP, incapacitation, stacking) | **Very high** |
| FFXIV | 6 action categories | speed stats, buff text, animation lock | whole classes of modifiers stop applying | **High** |
| FFXIV | cooldown group | the GCD itself | ability fires at the wrong rate | **High** |
| WoW | 7 schools | resistance, absorb, interrupt lockout | wrong immunity, wrong shield, broken interrupts | **High** |
| WoW | dispel type / mechanic | removal and CC immunity + DR | uncleansable debuffs, broken diminishing returns | **High** |
| PoE | 47 display tags | every item and passive modifier | build silently scales off the wrong stat | **Very high** |
| PoE | 117 internal types | support-gem legality predicate | illegal links allowed or legal links refused | **Very high** |
| D4 | 23 primary + 61 secondary tags | item affixes, paragon, tree layout | affix economy misfires silently | **High** |
| D4 | 121 FILTER_* / 46 Search_* | UI only, flagged `bSearchOnly` | cosmetic only | **None, by design** |
| D2 | tab/column/row | tree layout, prerequisites | tree renders wrong | **Low** |
| SMT / Persona | 7–10 resistance elements | the resistance grid, Press Turn | combat economy inverts | **Critical** |
| SMT / Persona | 12–14 inheritance columns | fusion legality | Personas cannot learn intended skills | **Critical** |
| Arknights | 3 skill types / 4 charge types | activation loop | skill never charges | **High** |
| Arknights | 18 operator tags | recruitment pool intersection | players target-pull what they should not | **High (monetisation)** |
| FEH | 5 skill categories | equipping and inheritance | build legality breaks | **High** |
| DOS2 | 17 skill types | which executor runs, which params exist | hard failure | **Critical** |
| DOS2 | 10 combat abilities | menu sort, damage-scaling attribute | wrong scaling attribute | **High** |
| BG3 | 8 schools + 7 type prefixes + ~35 flags | 5e rules + execution | as 5e, plus execution failures | **Mixed** |
| FF7 | 5 materia types | link legality | pairing rule breaks | **High** |
| Pokémon | 3 damage classes | Attack vs Sp. Atk selection | damage formula uses the wrong stat | **Critical** |

**INFERENCE:** the taxonomies carrying the least mechanical load are exactly the ones everyone
recognises by name (5e's schools, FF's command menu). The ones carrying the most are the ones nobody
outside the game's data files has heard of (PoE's internal types, Persona's `inheritTypes` bitmask,
WoW's `Mechanics`). **Recognisability and load are inversely correlated in this sample.**

---

## 13. Read-across: what the numbers say about 5 categories at ~900 creatures

Descriptive only.

**On the count itself.** 5 is inside the observed band of 3–8 and is the modal figure among games with
comparable structure: Persona 5's non-element skill classes (5), Fire Emblem Heroes' skill categories
(5), FFXIV's combat categories (4), Diablo IV's live primary tags per class (~6), SMT V's non-element
functional classes (4). No shipped game in this sample carries more than 8 at the top level, and the
two largest rosters carry 8 and 0.

**On roster size as an argument.** It is not one. Category count is uncorrelated with roster size
across the whole sample: 1,025 species / 3 categories, 3,207 creatures / 8, 374 operators / 3, 306
demons / 7 (element-inclusive), 232 personas / 5. The variable that moves is the **second vocabulary**,
not the first.

**On the second vocabulary.** This is where every game in the sample absorbs growth, and it is where
the comparison is least flattering:

| Game | Top-level | Second vocabulary |
|---|---:|---:|
| Arknights | 3 | 18 (on the unit) |
| **This project** | **5** | **8 tags** |
| Path of Exile | 0 | 47 display / 117 internal |
| Diablo IV | 23 (~6 live) | 61 mechanical + 167 cosmetic |
| Pathfinder 2e | 0 | 262 action traits |
| World of Warcraft | 7 | 37 mechanics + 665 aura types |

Arknights is the only game in the sample whose second vocabulary is anywhere near 8, and it needs only
18 because its skills carry **no** semantic classification and the tags describe *operators*, not
skills. Every game that classifies *skills* semantically has a second vocabulary of 47 or more.

**On the separation this project already has.** Category and target mode are already separate fields,
which puts this project on the DOS2 / FFXIV / BG3 side of §2 rather than the PoE / PF2e side. The
comparable delivery enums are 17 (DOS2), 10 in use (FFXIV), 16 (Pokémon), 7 (BG3) against 6 target
modes plus 4 area shapes here. That is the same order of magnitude, and the long tail observed in every
one of those games (§2) means a low usage count on the rarer modes is expected, not evidence of
over-specification.

**On what the sample says will change first.** Not the category count — §11 shows that essentially
never moves. The changes that actually occurred in shipped games were: values migrating between
entities (Pokémon), individual entries reclassified across a fixed boundary (WoW's GCD), the top-level
being deleted outright (PF2e), and unbounded growth in the second vocabulary (everyone).

---

## 14. What I could not find

**This section is mandatory and is not empty.**

### Access blocks hit in this pass

| Blocked | Effect |
|---|---|
| **`WebSearch` budget exhausted** (200/200) partway through | All later sourcing was done by direct fetch against known URLs, APIs, and datamine repositories. Several targets that would have needed discovery were dropped rather than guessed at. |
| **wiki.gg returns 401** to direct requests for `lastepoch.wiki.gg`, `diablo.wiki.gg`, `megamitensei.wiki.gg`, `persona.wiki.gg`, `smt.wiki.gg` | Last Epoch, Diablo III and the Megami Tensei wikis were unreachable by that route. `warcraft.wiki.gg` did serve content through the fetch tool, so the block is not uniform across wiki.gg. |
| **`megatenwiki.com` behind Cloudflare** (403 to every request, reader proxy returns the challenge page) | SMT V skill data had to come from a fusion-tool data mirror instead. |
| **`amicitia.miraheze.org` interstitial** ("Checking your connection") | The Persona file-format documentation (`SKILL.TBL` layout, the raw element byte enum) was not reachable. The Persona element enum here is from a fusion-tool mirror, not from the game's own table layout. |
| **`bulbapedia.bulbagarden.net` Cloudflare 403** direct; reader proxy worked | Only one Bulbapedia page was retrieved. |
| **`poewiki.net` behind an Anubis challenge** (HTTP error page for every request) | The wiki's own prose description of tag gating was unavailable. Everything in §6 comes from the RePoE data export instead, which is strictly better, but the wiki's editorial framing is missing. |
| **`lastepochtools.com` 403 direct**; reader proxy returned page 1 only | Only a partial Last Epoch tag list (25 tags, computed) — not the complete vocabulary. |
| **`grimtools.com/db/skills` returns only page chrome**; `/skilltree/` 404 | **No Grim Dawn data at all.** |
| **`divinityoriginalsin2.wiki.fextralife.com` 502** | Recovered by going to Larian's own engine documentation instead, which was better. |
| **Fandom `?action=raw` intermittently 403** through the reader proxy | Darkest Dungeon and Dragon Quest pages could not be read; the Final Fantasy Tactics ability page specifically failed while sibling pages succeeded. |
| **GitHub REST API rate-limited** (unauthenticated) partway through | `raw.githubusercontent.com` kept working, so known paths were still fetchable, but repository *discovery* stopped. The Fire Emblem Heroes JSON asset repository was located but not read. |
| **GitHub code search requires authentication** | No way to search inside repositories for enum definitions; every datamine had to be found by known path. |

### Genuine gaps — looked for, not found

- **No Grim Dawn skill taxonomy of any kind.** Not a single reachable source.
- **No Diablo III skill category list from a primary or datamined source.** The per-class UI groups
  (Primary / Secondary / Defensive / …) are well known but are **not stated here**, because nothing
  citable was reachable. Blizzard's own D3 skill calculator is no longer serving static content.
- **No Last Epoch complete tag list.**
- **No Darkest Dungeon combat-skill schema.** The `.info.darkest` file format documentation was not
  reachable through any route tried.
- **No Fire Emblem mainline (Three Houses / Engage) skill taxonomy.** `fireemblemwiki.org` serves its
  API normally and would support this, but the entry-point pages are disambiguations and the search
  budget to find the right titles was gone.
- **No developer statement, from anyone, on why a skill category count was chosen.** Consistent with
  the finding already recorded in [../game-design/06-unsourced.md](../game-design/06-unsourced.md) §1
  ("almost no designer commentary on roster or grid design, anywhere"). Every count in this file is
  observed from shipped data. **Nobody has published a rationale for 3 versus 5 versus 8.**
- **No source for a threshold at which a skill taxonomy becomes too large to learn.** Same negative
  result as the counter-matrix question in the earlier round. The literature does not contain a number.
- **No dated history of PoE's gem-tag list.** The 51 → 54 diff in §6 is between two data snapshots
  whose exact game versions could not be pinned (the archived repository's version file is gone and the
  API call to date it was rate-limited). **Treat the direction as solid and the interval as unknown.**
- **Divinity: Original Sin 2 skill count.** The `SkillType` and `Ability` enums are first-party and
  exact; the number of shipped skills to divide them into is not sourced here.
- **Fire Emblem Heroes roster size.** The "~1,000 heroes" figure in §1 is a rough order of magnitude,
  not a count. It is the least reliable number in that table and should not be quoted.

### Numbers in this file that are derived, not cited

| Figure | Status |
|---|---|
| All PoE tag, type and support-gate counts | Computed over RePoE's `gems.min.json` and `gem_tags.min.json` |
| All FFXIV action tallies and cross-tabs | Computed over 51,501 rows from the game's `Action` sheet |
| All D&D 5e spell distributions | Computed over 319 SRD spells from the SRD API |
| All PF2e trait-group counts | Computed over the Foundry PF2e system's `traits.ts` by resolving object spreads |
| All SMT / Persona counts | Computed over the fusion-tool data mirror, not the games' own tables |
| All Arknights counts | Computed over `skill_table.json` and `character_table.json` (EN branch — **CN is ahead, so operator and skill counts are floors, not totals**) |
| All Diablo II and Diablo IV counts | Computed over the `d2data` and `d4data` exports |
| All BG3 counts | Computed over 595 wiki spell pages, which reproduce but are not the game's stat files |
| D4 primary-tag-to-class mapping | **Inference from tag names.** The `SkillTags` table has `ePlayerClass = -1` on every row, so the class assignment is not in that file |
| "~1,000 FEH heroes" | Order of magnitude only |
| Last Epoch "25 tags" | Computed over one page of a partial retrieval; the real number is larger |

### Reusable access notes for the next pass

- **`https://r.jina.ai/<url>` bypasses Fandom's block** in this environment, which the previous round
  recorded as a sitewide HTTP 402. Plain article URLs work reliably; `?action=raw` works intermittently.
  This reopens the Final Fantasy, Dragon Quest, Fire Emblem Heroes and Megami Tensei wikis.
- **`https://docs.larian.game/api.php` is a live, open MediaWiki API** carrying Larian's own engine
  documentation. It is first-party and was the best single source found in this round.
- **`https://v2.xivapi.com/api/sheet/<Sheet>` serves the FFXIV game sheets directly** with `fields=`
  projection and `after=` cursor pagination. It **requires a `User-Agent` header** — without one it
  returns 403.
- **`https://bg3.wiki/w/api.php` allows `generator=categorymembers` with `prop=revisions`**, so whole
  categories can be harvested 50 pages at a time. Arbitrary Cargo queries are refused, and `insource:`
  search is not enabled.
- **`https://api.github.com/repos/<r>/git/trees/<branch>:<path>`** lists a single directory of a very
  large repository without cloning it — this is how the 4,086-file Diablo IV `Power` directory was
  enumerated. The contents API silently repeats page 1 past 1,000 entries; the trees API does not.
- **`raw.githubusercontent.com` keeps working after the GitHub REST API rate-limits.** Enumerate first,
  fetch later.
- **`pokeapi.co` type/category/target endpoints list their own members**, so distributions can be had in
  3 calls instead of 937.
