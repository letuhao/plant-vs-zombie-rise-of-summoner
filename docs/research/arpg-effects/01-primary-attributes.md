# Primary / secondary attributes (inspiration)

Observation from Diablo-like ARPGs. Not a FusionRpg product ADR.

## Mechanism summary

| Game | Primary attrs | How they feed combat | Requirements / hybrid notes |
|---|---|---|---|
| **Diablo II** | Strength, Dexterity, Vitality, Energy | Str → melee dmg / equip req; Dex → AR, block, some weapon dmg; Vit → life; Energy → mana | Classic “dump Vit” trap; attrs are both power and **gates** |
| **Diablo III** | Mainstat by class (Str/Dex/Int) + Vit | Mainstat → damage; Vit → Toughness; secondary (CHC/CHD, CDR, etc.) carry most build identity | Mainstat is almost pure damage scaler |
| **Diablo IV** | Strength, Dexterity, Intelligence, Willpower | Class mainstat → damage; each attr also feeds defenses / resources / thresholds (e.g. Willpower healing received) | Heavy **affix inflation** on gear; attrs compete with legendary powers |
| **Path of Exile** | Strength, Dexterity, Intelligence | Life/ES/evasion/armour/accuracy/mana; weapon & gem **requirements**; some notables key off thresholds | Hybrid attrs via tree; requirements punish glass-cannon gem stacking |
| **Last Epoch** | Strength, Dexterity, Intelligence, Attunement | Soft gate + secondary stats; **skill tags** (Melee, Spell, Bow, …) decide what scales a skill more than pure attr | Hybrid builds work when tags + ailments align |
| **Titan Quest 2** | Might, Agility, Knowledge (+ mastery picks) | Primaries feed **derived secondaries** (Fitness / Cunning / Resolve style pools) | Clean “invest primary → get readable secondary” model |
| **Grim Dawn** | Physique, Cunning, Spirit | Physique → HP / OA-DA mix; Cunning → OA / pierce / crit-ish; Spirit → energy / elemental | Masteries add skill points; attrs stay meaningful mid–late |

### Patterns worth naming

1. **Mainstat as damage multiplier** (D3/D4) — simple for players, weak for hybrid fantasy.
2. **Attrs as soft/hard requirements** (D2/PoE) — gate gear and skills; create respec tension.
3. **Primary → secondary derivation** (TQ2, GD) — players invest in 3–4 primaries; UI shows Fitness/Resolve/etc.
4. **Skill tags over attrs** (LE, PoE supports) — “what the skill is” beats “what your mainstat is.”

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | TQ2-style primary→secondary derivation; LE skill tags for plant/zombie identities |
| **Adapt** | PoE-like soft requirements as “lawn / fusion gates” later — not hard D2 Str walls in v1 |
| **Avoid** | D2 forever-Vit dumps; D4-style endless affix inflation before a small closed modifier grammar exists |

## Sources

- [Diablo II attributes (wiki)](https://diablo.fandom.com/wiki/Attributes)
- [Diablo IV attributes](https://diablo4.wiki.fextralife.com/Attributes)
- [PoE Strength / Dexterity / Intelligence](https://www.poewiki.net/wiki/Strength)
- [Last Epoch attributes](https://www.lastepochtools.com/guide/attributes)
- [Titan Quest 2 attributes overview](https://titanquest.fandom.com/wiki/Attributes) (TQ1 heritage; TQ2 continues primary→secondary)
- [Grim Dawn Physique / Cunning / Spirit](https://www.grimdawn.com/guide/gameplay/combat.php)

## Open questions for FusionRpg

1. Do we expose **player** primaries, **per-plant-type** primaries, or both?
2. Should fusion plants inherit tagged attrs from parents or get a new tag set?
3. Is “mainstat → damage” enough for early Progression power, or do we need tags from day one?
4. How do zombie-side attrs mirror plant-side without doubling UI complexity?
