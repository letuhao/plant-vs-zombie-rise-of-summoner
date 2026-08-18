# Modifier stacking language (inspiration)

Observation from Diablo-like ARPGs. Not a FusionRpg product ADR.

FusionRpg already locks Flat → Increased → More. This doc situates that model among peers.

## Mechanism summary

### Path of Exile (canonical additive/multiplicative grammar)

Rough mental model (per damage type / package):

```text
damage ≈ (base + Σ Added)
         × (1 + Σ Increased)
         × Π (1 + More)
```

- **Added / Flat** — absolute points into the pool.
- **Increased** — additive with other Increased of the same type.
- **More / Less** — multiplicative layers (supports, unique mods).
- **Local vs global** — local mods on a weapon apply to that weapon’s base before global Increased/More.

### Last Epoch

Same three keywords (**Added / Increased / More**). Skill tags and ailments decide *which* pools receive the mods. Over-100% chance effects often become multi-stacks rather than wasted rolls.

### Diablo IV (bucket history)

- Damage often presented as many **% damage** affixes that historically stacked as near-independent multipliers → exponential power creep.
- Season 2+ balance pushed clearer baselines for Crit / Vulnerable / Overpower style [x] buckets so one “god roll” of every bucket was less mandatory.
- Lesson: **too many independent mult buckets** ≈ every build must stack all of them.

### Local vs global (PoE-shaped)

| Scope | Typical meaning |
|---|---|
| Local | Modifies the item’s own base (weapon DPS, armour base) |
| Global | Modifies the character / skill after local bases are known |

FusionRpg’s `Y0` capture is closer to “local/base from Unity”; bag mods are global-to-that-entity unless we later add item-local bags.

## Keyword glossary

| Keyword | Stacking | FusionRpg today |
|---|---|---|
| Flat / Added | Sum | `ModifierOp.Flat` |
| Increased | Sum inside `(1 + Σ)` | `ModifierOp.Increased` |
| More | Product of `(1 + m)` | `ModifierOp.More` |
| Bucket / [x] | Named multiplicative family (D4) | Prefer **not** adding many More families |
| Local | On-item base | Future items; not in Core yet |
| Global | Character / skill | Current bag semantics |

## Failure modes

1. **Affix inflation** — dozens of “+% damage to X” that each act like More.
2. **Hidden More** — UI says Increased but code multiplies separately.
3. **Double-dip conversion** — convert fire→cold then scale both (see `05-hit-crit-conversion.md`).
4. **Local/global confusion** — players cannot tell whether a plant card mod is baseline or bag.

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | PoE/LE three-word grammar; keep UI labels identical to `ModifierOp` |
| **Adapt** | D4’s lesson: cap named mult buckets (Crit, Vulnerable-like) to a short closed list |
| **Avoid** | Open-ended “% damage” affixes each as independent More; silent fourth compose stage |

## Sources

- [PoE wiki — Damage](https://www.poewiki.net/wiki/Damage)
- [PoE wiki — Modifiers](https://www.poewiki.net/wiki/Modifier)
- [Last Epoch — Damage calculation](https://www.lastepochtools.com/guide/damage-calculation)
- [Diablo IV damage buckets / S2 discussion (community + Blizzard patch notes)](https://news.blizzard.com/en-us/diablo4)

## Open questions for FusionRpg

1. When items arrive, do local flats merge into `Y0` or stay a separate pre-bag stage?
2. Do plant fusion recipes grant Flat, Increased, or More — and is that visible in dossier?
3. Should crit / “vulnerable” / overpower-likes be **tags on hits** with fixed baselines, not free More affixes?
