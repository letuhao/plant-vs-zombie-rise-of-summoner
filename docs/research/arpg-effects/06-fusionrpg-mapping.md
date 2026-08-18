# FusionRpg mapping (inspiration → existing systems)

Synthesis of the ARPG survey for later design. **Not** an ADR; does not change Core compose or schema.

See also: [stat-system.md](../../architecture/stat-system.md), [pvz-stats.md](../../architecture/pvz-stats.md), [rpg-progression.md](../../architecture/rpg-progression.md), [Foundation Effect system](../../architecture/effect-system.md), [own-game effect runtime](../effect-runtime/00-index.md) (what we can inject/capture before EffectBag).

## Vocabulary map

| Peer keyword | FusionRpg today | Future (inspiration only) |
|---|---|---|
| Flat / Added | `ModifierOp.Flat` | Keep |
| Increased | `ModifierOp.Increased` | Keep; UI same word |
| More / Less | `ModifierOp.More` | Keep; rare sources |
| Y0 baseline | Captured Unity baseline | Items may add **local** pre-bag later |
| Modifier bag | `ModifierBag` + source tags | Temporary buffs = timed sources |
| Mainstat | — | Player and/or per-type attrs feeding Flat/Inc |
| Skill tags | PlantType / ZombieType identity | LE-like tags on skills/projectiles |
| Lucky Hit / CtC / CoC | — | `EffectBag` listeners on combat events |
| Ailment | Unity CC / DoT fields | Mirror or wrap as status effects |
| Crit / Vulnerable buckets | — | Closed list of hit tags × fixed baselines |
| Conversion | Elemental plant fantasy | Convert-once on skill definition |
| OA/DA | Collision = hit | Optional miss only if design wants it |

## Recommended inspiration priorities (PvZ metaphors)

| Priority | Steal | Lawn-facing sketch |
|---|---|---|
| P0 | Keep Flat→Inc→More grammar public | Dossier shows the three words only |
| P0 | Event + ICD trigger model | “On crit pea → lob butter” with cooldown |
| P0 | Ailment stats ≠ hit More | Freeze/poison duration & stacks have own mods |
| P1 | Primary→secondary attrs (TQ2) | Might-like → toughness / fire rate secondaries |
| P1 | Skill tags (LE) | `Pea`, `Melon`, `Lobber`, `Mage` tags |
| P1 | Lucky Hit shared budget | Optional later if on-hit mods clutter |
| P2 | Convert-once elements | Fusion recipe changes damage type once |
| P2 | Chance to spawn | Extra spore / imp with hard caps |

## Explicit non-goals (this research pass and near-term)

- No redesign of locked compose math.
- No shipping EffectBag / items / power curve in this documentation pass.
- No copying D4 affix inflation or uncapped multi-explode clear metas.
- No treating Progression XP as combat power until a separate ADR says so.
- No OA/DA complexity while Unity collisions remain binary hits.

## How layers could grow (sketch)

```text
Progression (XP / level)     →  optional attr points / unlocks
     ↓
Primary attrs / tags         →  Flat/Inc into PvzStats or RPG stats
     ↓
ModifierBag (existing)      →  Compose → Y → EntityApply
     ↓
EffectBag (future)          →  onHit/onKill/… → buffs, spawns, casts
     ↓
Status / ailments (future)   →  timed sources back into ModifierBag
```

Power and items should hang off **attrs + effects + composed stats**, not off raw level alone.

## Steal / adapt / avoid (repo-specific)

| | Guidance |
|---|---|
| **Steal** | Peer keyword discipline; ICD on any trigger; stack caps on DoT/spawn |
| **Adapt** | Existing Unity statuses as ailment carriers; Activity events as proc signals |
| **Avoid** | Silent fourth multiplier; procs that bypass Intent/single-writer; level→damage with no bag |

## Sources

Aggregated from sibling docs in this folder; primary peers:

- [PoE Modifiers](https://www.poewiki.net/wiki/Modifier)
- [Last Epoch damage calculation](https://www.lastepochtools.com/guide/damage-calculation)
- [Diablo IV Lucky Hit](https://diablo4.wiki.fextralife.com/Lucky+Hit)
- [Grim Dawn combat](https://www.grimdawn.com/guide/gameplay/combat.php)

## Open questions (design queue)

1. Effect evaluation host: injector tick vs server sim vs both?
2. First combat power feature: attr→Flat ATK/HP, or one proc effect?
3. Per-type Progression level — does it grant attrs, unlocked effects, or neither until items?
4. Fusion plants: inherit parent effects, average tags, or unique effect table?
