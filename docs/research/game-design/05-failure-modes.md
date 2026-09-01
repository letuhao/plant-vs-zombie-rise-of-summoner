# Failure modes — documented disasters, with causes

Captured 2026-09-01. Each entry names the mechanism, not just the symptom.

---

## 1. ⛔ A data table picked the winning build

**Diablo II Hell immunities across 703 monsters:**

| Immunity | Monsters |
|---|---:|
| Cold | **137** |
| Poison | **131** |
| Fire | **113** |
| Lightning | **105** |
| **Magic** | **11** |

> *"which is why Hammerdin won Hell — **a data table picked the winning build, not a designer.**"*

**The mechanism:** an uneven distribution across a resistance vocabulary silently selects the meta.
Nobody authored "Hammerdin is best"; the immunity counts did.

**Why this is the sharpest warning for a generated roster:** LLM selection bias
([01](01-typing-matrices.md) is the matrix data; the bias evidence is in the ideal doc §4.7)
*guarantees* uneven distribution across an enum unless it is measured. At 900 units the resulting
skew would be invisible until players found it.

---

## 2. ⛔ A single wrong cell broke a metagame for a console generation

Pokémon Gen I shipped **Ghost → Psychic at 0×** when it should have been 2×.

The contradicting evidence was already inside the shipped product:
- Nintendo's own strategy guides said Ghost was super-effective
- Two anime episodes depicted it that way
- **An NPC inside the game**, in Saffron Gym, says Psychic Pokémon *"only fear Bugs and Ghosts"* — a
  line removed in *Yellow*

**Consequence:** Psychic's only functional weakness became Bug, and Gen I had no strong Bug moves.
**Psychic was the most dominant type in franchise history because of one wrong cell.**

**The lesson, stated by the research:** *"anyone shipping a matrix needs a test that asserts the table
against declared intent."*

Gen I → II corrections: Bug→Poison 2× → **0.5×**; Poison→Bug 2× → **1×**; Ghost→Psychic 0× → **2×**;
Ice→Fire 1× → **0.5×**. Note Bug and Poison were **mutually super-effective** in Gen I — nonsense on
its face, and it shipped.

**And types were added as balance patches**: Dark got immunity to Psychic, Steel got resistance to
Psychic — both Gen II, both to fix Psychic. Fairy in Gen VI, immune to Dragon, to fix Dragon.

**Second-order effect nobody could have reasoned about locally:** Gen VI *nerfed* Steel (removing its
Ghost and Dark resistances) while adding Fairy. **Steel's usage went up**, because Steel resists Fairy
and Fairy was everywhere. *"You cannot reason about a matrix change locally."*

---

## 3. ⛔ Gates converge every build — and Larian retracted one after nine years

**Divinity: Original Sin 2's armour system.** Physical and Magic Armour had to reach **0** before any
matching status could land.

**Swen Vincke on their own AI finding the dominant line:**
> *"it focused on one character, made sure to destroy their physical and magical armour, and then
> would start to control it, then kill it. That wasn't fun because one guy was dealing with
> everything, but **it was a dominant tactic.** So we had to nerf it a lot."*

**Larian removed it in 2026.** Pechenin's replacement criterion:
> *"There will be ways to protect your characters from harm, but **you will not have to wait before
> you can use your fun skills on enemies.**"*

Press diagnosis: *"you were generally much better off having your party focus on either physical or
magical damage to break your chosen type faster, rather than using a mixed party… Plus, you were often
best off saving most of your fun abilities until after you'd broken the enemies' armour."*

### ⭐ The general rule

> **If a mechanic's rule is "nothing interesting happens until X", the optimal play is always "make X
> happen first, then do everything" — and every build converges.**

**This repo has a mechanic of that shape: `poise`** (`Combat/Guard/PoiseRuntime.cs`), whose depletion
is a guard-broken gate. Mitigation this design has that Larian did not: **riposte** — spent poise
converts to damage, so breaking a guard is not purely free for the attacker.

---

## 4. ⛔ Tag absence is a stat

SC2's **Archon, Ghost, Ravager, Baneling, Queen and Cocoon carry neither Light nor Armored.** That
makes them immune to a large share of every bonus-damage term in the game.

**Omitting a tag is not neutral — it is a defensive buff.**

For a generated contract: **every closed enum needs an explicit `none` value, never an omitted
field**, or a model that is merely unsure hands a unit a hidden defensive stat.

**Tag overlap has the mirror problem.** The Hellbat is `Light, Biological, Mechanical` — breaking both
of SC2's supposed exclusive pairs. It can be **healed by Medivacs *and* repaired by SCVs**, and it is
simultaneously vulnerable on three axes. It is tagged Biological *purely* so Medivacs can heal it.

**Changing a unit's feel changes ~9 weapons' effectiveness at once.** `Armored` is both the most common
tag (123 units) and the most-targeted (9 weapons), so tagging a unit Armored for flavour signs it up to
be countered by half the game's specialists.

---

## 5. ⛔ Closed vocabularies leak under pressure

**SC2 patch 5.0.13:** *"Sentry: Damage increased from 6 to 6 **(+4 vs Shields)**. **Light attribute tag
removed.**"* — the lost interaction was replaced with an **ad-hoc pseudo-attribute outside the closed
eight**.

**`Psionic` was dead vocabulary for an entire expansion cycle.** Flavour-only until patch 1.4.3, then
mechanically relevant, then irrelevant again through all of LotV, then revived in 4.0.0 by Interference
Matrix.

**Four live attribute swaps shipped with no designer note at all:**

| Patch | Change |
|---|---|
| 4.8.2 (2019) | Oracle: **Light removed, Armored added** |
| 4.12.0 (2020) | Creep Tumor: **Armored removed, Light added** |
| 5.0.13 (2024) | Sentry: **Light removed**, +4 vs Shields added |
| 5.0.15 (2025) | Ghost: **now has Light**, supply 3→2 |

**Tags are balance levers, not identity.** A closed contract stays closed only if it has a designated
escape hatch; otherwise one gets improvised.

---

## 6. ⛔ Taxonomy accretion — AoE II's 38 armour classes

The list grew ~4× over 25 years by accretion, not design:

- **AoK:** camels share a class with cavalry; wild boars share one with siege weapons
- **The Conquerors:** camels moved *out of* cavalry and *into the ship class*
- **The African Kingdoms:** camels finally get their own class
- **Dynasties of India:** class 31 declared obsolete — *"The armor values still exist in the game files
  as they were before"*
- **The Last Chieftains:** *"Fishing Ships are readded to the ship armor class and all bonuses against
  the Fishing Ship class are removed, **making it redundant**"* — a class created and decommissioned in
  place

**Resulting shipped bugs:**
- Class 11 is named **"All Buildings (except Port)"** — Port was a hidden AoK building that never shipped
- Class 19 is named **"Unique Units (except Turtle Ship)"** — **Turtle Ships are in class 19**
- **The Mosque has no armour classes at all**, so *"all attacks, even from Trebuchets or Petards with
  Siege Engineers, will do only 1 damage"* — a missing row makes a building near-invulnerable
- Sheep, wolves and trees have base armour 0 and no class, so *"they take all sorts of bonus damage,
  all of which stack"* — which is why a 3-attack Scout one-shots a sheep

**And it was hidden for ~20 years.** The page's own opening line: *"Armor classes are semi-hidden unit
attributes."* Before Definitive Edition the only way to read them was the Genie Editor. Update 141935
added hover tooltips **and forced the classes to acquire official names for the first time.**

**The formula itself changed under the content**, silently re-tuning every unit with non-zero class
armour: update 81058 allowed negative values to contribute; update 66692 reordered bonus-armour
subtraction. Plus the **Gurjara rounding incident**, where staggering a bonus by Age (×1.08 × ×1.08 on
already-rounded integers) produced eight unintended nerfs and two unintended buffs.

---

## 7. ⛔ "A faster Banshee is still a Banshee"

See [04-designer-quotes.md](04-designer-quotes.md) §3 for the full passage.

**The test:** if two units differ only in magnitude, they are not two units. **A pure magnitude axis
does not make a different unit.**

Also cut for role reasons, all with stated causes:
- **Warhound** — *"too much like a small thor"* / *"role overlapped with the Marauder"*. Its
  anti-mechanical counter was implemented as an **auto-cast ability with `AutoCastOn` defaulted true,
  no energy cost and a 6-second cooldown** — zero player input, invisible on the damage tooltip.
  **A unit whose entire counter identity fires itself is definitionally an a-move unit.**
- **Lurker (WoL)** — *"there simply were so many other units stepping on its role"*
- **Shredder** — *"rarely used for its intended role of map control"*
- **Replicant** — *"removing diversity from the game instead of adding diversity"*
- **HERC (LotV)** — *"Too much overlap with Hellbats"*
- **Mothership Core** — removed to concentrate defensive power in the Nexus

---

## 8. ⛔ Integer ceilings are real

**World of Warcraft's Ra-den shipped at ~1.5 billion HP — 70% of the signed 32-bit ceiling.** This
forced **four game-wide stat squishes.**

This repo's rule — `long` for any magnitude, never `float`, widen before multiplying — has a corpse
attached.

---

## 9. ⛔ Emergent systems couple across subsystems

**Company of Heroes**, from Relic's own patch notes:

- **A feature deleted for illegibility** (2.5.0): *"Infantry small arms no longer take
  over-penetration into account when firing on enemies; this previously resulted in weapons like Light
  Machine Guns… to target enemy squad members in the rear of the formation."*
- **A fix in one subsystem silently rebalanced another** (1.3.0): *"our tank guns were too accurate at
  maximum range… **especially after the fix to projectiles that made it easier for scatter hits to
  avoid low obstacles.**"* One projectile-collision change re-tuned effective accuracy across every
  vehicle at once, because accuracy is emergent from interpolation, not authored.
- **A multiplier converted to a deterministic effect**: *"30% received accuracy changed to 15% damage
  reduction."*

**The tuning surface is the problem:** every change is three numbers per weapon per stat
(near/mid/far), and `penetration` spans **1.1 to 380** in one namespace — anti-infantry uses ~1–3,
anti-tank ~100–300. **The field means different things depending on what the weapon shoots at.**

---

## 10. ⛔ Subtractive mitigation walls out, and the counter becomes a tax

**Total War**: armour is linear, producing hyperbolic effective HP with a hard wall at 200 armour
(100% reduction). This is *why* `ap_damage` exists as a separate additive channel that ignores
mitigation.

The wiki's own takeaway states the endgame:
> *"To bring down a unit with an armour value at or approaching this level, Armour-piercing damage is
> necessary. However, **every attack in the game deals some amount of armour-piercing damage.**"*

**When every weapon must carry the counter to stay relevant, the counter has stopped being a choice
and become a tax.**

Stat bloat is visible in the schema: `land_units_tables` has 62 columns of which ~15 are combat;
adding a unit means touching ~8 tables and ~200 columns. CA's own dev blog describes the balance
approach as *"a lot on number nudging"* and admits the trait system is *"largely untouched since IE"* —
**content added over years drifting out of alignment with a baseline nobody re-derives.**

---

## 11. ⛔ Encoding defects in positional vectors

**Command & Conquer's `Verses=`:**

1. Parsed by an **unvalidated `strtok` loop** — a short list silently reads past the end. **Insert one
   armour type and every warhead in the file is off by one.**
2. **The vector conflates damage with targeting rules and with healing** — 0% = invalid target, 1% =
   will not auto-acquire, negative = heals. A balance change to a percentage changes what a unit may
   shoot at.
3. **The engine's own shipped documentation is wrong about its own values.**
4. **No global consistency check is possible** — each warhead owns an independent vector, so there is
   nowhere to ask *"is this unit counterable?"* In Tiberian Sun *"there is no ground unit that
   counters"* Nod's artillery. **One missing row is fatal, not merely suboptimal.**

**OpenRA's re-implementation is the verdict**: `Versus` becomes a **name-keyed dictionary**
(unspecified types default rather than shifting everything), `Armor` becomes multi-valued and
condition-gated, and the 0%/1% targeting semantics move to explicit `ValidTargets` / `InvalidTargets`.

---

## 12. ⛔ A build table that does not describe its own content

**D&D 5e:** the DMG's monster-building table says CR 1/4 should be **36–49 hp**; the measured SRD
median is **13**. The build table does not describe the Monster Manual.

**And the 2024 Monster Manual changed the damage:HP ratio from 0.33 to 0.4**, making 5e incompatible
with itself.

**Diablo II's 58-entry `MonType` taxonomy ships unreadable** because a string lookup reads the wrong
table's row count.

---

## 13. The dead tail is the normal outcome, not the failure case

See [03-roster-scale.md](03-roster-scale.md) §5 for full numbers.

- **Pokémon:** 177 species (36% of everything tiered) in the bottom tier; **18 species fill 50% of all
  competitive team slots**; of 1,025 species only 82 reach 1% ladder usage.
- **Genshin:** among players who **own** the character, eight sit under 4% usage while Kazuha sits at
  95.7% — a **~319× spread**. Klee shows a **69% vacancy rate**.

**The industry's answer was retroactive kit rewrites, not restraint** — Novaflare, Hexerei, ZZZ v2.5.
Each is a content programme; for a generated roster the equivalent is a pipeline re-run.
