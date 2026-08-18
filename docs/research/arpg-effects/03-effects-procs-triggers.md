# Effects, procs, and triggers (inspiration)

Largest observation doc: chance-based procs, cast-on-X, buffs/debuffs as events, and chance-to-spawn. Not a FusionRpg product ADR.

## Mechanism summary

ARPGs separate **stat modifiers** (Flat/Inc/More) from **effect graphs** that fire on combat events.

### Event taxonomy (cross-game)

| Event | Examples | Notes |
|---|---|---|
| `onHit` | D2 CtC, PoE on-hit, D4 Lucky Hit carriers | Needs hit confirmation (see hit pipeline doc) |
| `onCrit` | PoE Cast on Crit | Often gated by ICD + crit chance |
| `onKill` | Explode on death, gain buff | Easy to over-reward clear speed |
| `onBlock` / `onGetHit` | Retaliate, thorns-like | Defender-side |
| `onTimer` / tick | Auras, CwC sampling, DoT ticks | Server/sim tick budget matters |
| `onCast` / `whileChannelling` | PoE CwC | Continuous skills sample events |
| `onStatusApplied` | Shatter frozen, consume Vulnerable | Chains effects |

### Diablo IV — Lucky Hit

Conceptual model used by community tools and skill tooltips:

```text
P(proc) ≈ L_skill × E × (1 + Σ lucky_hit_chance_bonus)
```

- `L_skill` — skill’s inherent Lucky Hit chance (often low on fast hits).
- `E` — effect’s proc coefficient / chance listed on the legendary/aspect.
- Bonuses are mostly **Increased-like** on the Lucky Hit chance stat.
- DoTs often use a **budgeted** Lucky Hit contribution so ticks do not spam procs every frame.

### Path of Exile — triggers

| Pattern | Behavior |
|---|---|
| **Cast on Crit (CoC)** | Crit → cast linked spell; **ICD** (e.g. 150ms class) prevents infinite loops |
| **Cast while Channelling (CwC)** | Channel ticks → cast |
| **Cast on Death / Kill / Hit** | Item or jewel wording; still ICD-gated when supported |
| Support gems | Add tags, More/Less, and sometimes trigger conditions |

Triggers are first-class **skills with cooldowns**, not raw RNG spam.

### Diablo II — Chance to Cast

- Item mod: “X% chance to cast level Y skill on striking / when struck.”
- Level is fixed on the item; scales poorly into late ladder without itemization care.
- No unified “Lucky Hit” layer — each mod rolls independently (multi-proc competition).

### Last Epoch — proc-style skills & overfill

- Many skills are “on hit, Y% chance…” with clear ICD or GCD-like limits.
- **Chance > 100%** often means guaranteed proc + chance at **extra stacks** / extra hits rather than wasted %.

### Chance to spawn (entities / projectiles / ground)

| Pattern | Game examples | Design knobs |
|---|---|---|
| Extra projectile / pierce | PoE GMP-like, LE bow skills | Count caps, damage More penalty |
| Summon on kill / on hit | PoE spectres triggers, D4 companions procs | Duration, cap, AI cost |
| Ground DoT / desecrate | PoE ground effects, D4 pools | Tick rate, stack policy |
| Split / nova on death | Clear-speed explode metas | Anti-chain radius / ICD |

## Probability & competition models

1. **Independent rolls** — each effect rolls `onHit` (D2-like). High variance; many weak procs.
2. **Shared budget (Lucky Hit)** — one roll feeds many listeners weighted by `E`.
3. **Trigger skill + ICD** — event queues a cast; cooldown owns rate (PoE).
4. **Overfill → stacks** — LE-like; rewards investment past 100%.

Multi-proc competition: decide whether two on-hit uniques both roll, or share one Lucky-Hit-like resource.

## Buff / debuff as effect payloads

Procs often **apply** a timed modifier bag to self or enemy:

- Self buff: “on kill, +More damage 4s”
- Enemy debuff: “on hit, apply Chill”
- Aura: continuous `onTimer` re-apply to allies in radius

Treat buffs as **temporary ModifierBag sources** with duration/stack policy (see ailments doc).

## PvZ / lawn metaphors (sketches only)

| ARPG idea | Lawn sketch |
|---|---|
| Extra projectile | Extra pea / kernel on proc |
| Ground DoT | Butter puddle / ice trail on tile |
| Spawn on kill | Imp / spore on zombie death |
| Cast on Crit | Critical sunflower beam → cast lobbed shot |
| Aura | Torchwood lane aura = More fire in column |
| Lucky Hit | “Lucky chomp / lucky pea” shared proc budget |

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | Explicit event enum + ICD on trigger skills; LE overfill→stacks |
| **Adapt** | D4 Lucky Hit as optional shared budget for plant on-hit clutter |
| **Avoid** | Unbounded independent on-hit explosions (D2 CtC + modern clear-speed); trigger loops without ICD |

## Sources

- [Diablo IV Lucky Hit (Fextralife)](https://diablo4.wiki.fextralife.com/Lucky+Hit)
- [PoE Cast on Critical Strike Support](https://www.poewiki.net/wiki/Cast_on_Critical_Strike_Support)
- [PoE Cast while Channelling Support](https://www.poewiki.net/wiki/Cast_while_Channelling_Support)
- [Diablo II Chance to Cast](https://diablo.fandom.com/wiki/Chance_to_Cast)
- [Last Epoch ailments / ailments chance guides](https://www.lastepochtools.com/guide/ailments)

## Open questions for FusionRpg

1. Shared Lucky-Hit budget vs independent rolls for plant on-hit mods?
2. Are spawns (`CreatePlant` / zombie spawn intents) allowed from procs in v1 of an Effect system?
3. Where does ICD live — Core, feature plugin, or server sim?
4. How do board entities (lawn tiles) host ground effects without Unity ownership fights?
