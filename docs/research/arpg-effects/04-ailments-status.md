# Ailments, status, buffs / debuffs (inspiration)

Observation from Diablo-like ARPGs. Not a FusionRpg product ADR.

## Mechanism summary

| Game | Damaging ailments | Control / debuff | Stack / refresh notes |
|---|---|---|---|
| **Last Epoch** | Bleed, Ignite, Poison, etc. as DoT stacks | Slow, chill-like CC via ailments | Base DoT stacks often **add**; duration policies vary; ailments scale from ailment-specific stats more than raw hit More |
| **Diablo IV** | Burn, Bleed, Poison DoTs | Chill → Frozen; Vulnerable; Fortify (player) | Vulnerable historically large damage amp; Fortify as damage-reduction resource; definitions changed across seasons |
| **Path of Exile** | Ignite, Bleed, Poison (ailments) | Chill, Freeze, Shock; **Exposure** (−res) separate from ailments | Ailment chance/effect vs hit damage; shocks/chills have magnitude caps; Exposure is its own family |
| **Grim Dawn** | Burn, Frostburn, etc. | Slow, stun, resist reduction | OA/DA still gate whether hits apply; DoTs often % weapon or flat |

### Buff vs aura vs temporary mod

| Kind | Lifetime | Typical carrier |
|---|---|---|
| Temporary buff | Duration on self | On-kill / potion / skill |
| Debuff | Duration on enemy | On-hit / curse |
| Aura | While source alive / toggled | Radius re-apply on tick |
| Stance / fortify-like | Resource threshold | Player defensive layer |

### What scales ailments vs hits

Cross-game rule of thumb:

- **Hit damage** scales from weapon/skill base × Flat/Inc/More × crit × vulnerability.
- **Ailment damage** often uses a **snapshot or separate coefficient** (ailment effect %, ailment duration, stacks) so stacking “More hit damage” does not infinitely double-dip DoT.

LE’s stack-forward DoTs and PoE’s ailment-specific stats are the cleanest expressions of that split.

## Apply / refresh / stack policies (design knobs)

| Policy | Behavior | Risk |
|---|---|---|
| Refresh duration | Same stack count, timer resets | Favors attack speed |
| Add stacks | Stack count ↑, maybe shared timer | Needs hard cap |
| Intensity overwrite | Stronger replaces weaker | Feels bad if weaker overwrites |
| Independent instances | Multiple timers | CPU + UI noise |
| Consume on use | Vulnerable / freeze shatter | Enables combos |

## Steal / adapt / avoid

| | Guidance |
|---|---|
| **Steal** | Separate **ailment stats** from hit More; hard stack caps; consume-on-combo (Vulnerable/shatter-like) |
| **Adapt** | PvZ statuses already exist (freeze, butter, poison) — map to ailment families rather than inventing parallel CC |
| **Avoid** | Season-era D4 “must have Vulnerable always up”; uncapped independent DoT instances |

## Sources

- [Last Epoch ailments guide](https://www.lastepochtools.com/guide/ailments)
- [Diablo IV Vulnerable](https://diablo4.wiki.fextralife.com/Vulnerable)
- [Diablo IV Fortify](https://diablo4.wiki.fextralife.com/Fortify)
- [PoE Ailments](https://www.poewiki.net/wiki/Ailment)
- [PoE Exposure](https://www.poewiki.net/wiki/Exposure)
- [Grim Dawn combat basics](https://www.grimdawn.com/guide/gameplay/combat.php)

## Open questions for FusionRpg

1. Reuse Unity freeze/butter/poison as ailment SSOT, or mirror into RPG EffectBag?
2. Do plant DoTs snapshot on apply or update every tick from current bag?
3. Player Fortify-like resource — sun? armor layers? — or skip until later?
4. Stack caps global vs per-ailment-family?
