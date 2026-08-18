# Combat metrics: hit damage vs attack speed vs DPS

Live probe on **Peashooter (plant type 0) → basic zombie**, 2026-08-16.  
Not EffectBag. Feeds effect-system attribute design.

## Vocabulary (do not conflate)

| Term | Meaning | Game field / signal (pea) | Cheat probe |
|---|---|---|---|
| **Hit damage** | Amount applied on one projectile impact | Observed on `zombie.damage` (`path=take`); driven by **`Plant.attackDamage`** for pea | `A-P-ATK%`, `A-P-ATK+`, `P-ATK` |
| **Attack interval** | Seconds between shots | `Plant.thePlantAttackInterval` (also CD / `attackSpeedAdder`) | `P-ATK-INT`, `P-ATK-CD`, `P-ATK-ADD` |
| **Attack speed** | Informal inverse of interval (shots/sec) | Derived: `1 / interval` | Same as interval knobs |
| **DPS** | Derived throughput | **Not a game field.** `≈ hitDamage / attackInterval` | Never a single cheat |

Wiki lists pea as “20 / 1.5s”. That string is **marketing DPS packaging** (hit × rate), not one modifiable stat.

## Live results (this session)

Method: freeze waves → clear board → spawn pea @ (2,2) + tank zombie → collect `zombie.damage` for ~7s.  
Shot gaps = intervals ≥ 0.2s between damage timestamps (filters multi-emit per shot).  
Raw JSON: [`_p1-metrics-live.json`](_p1-metrics-live.json).

| Probe | Hit dmg (avg / typical) | Shot gap ≈ | Derived DPS ≈ | Interpretation |
|---|---|---|---|---|
| Baseline (identity) | **20** | **~1.42 s** | ~14 | Matches wiki 20/1.5 within noise |
| `A-P-ATK% = 5` | **100** (after settle) | ~1.5 s (unchanged) | ~49 | **Hit damage** scales; rate unchanged |
| `P-ATK-INT = 0.5` | **20** | **~0.48 s** | ~41 | **Rate** scales; hit damage unchanged |
| `P-ATK-INT = 3` | **20** | mixed (~0.5 then ~4.4) | — | Interval write works but settle/noise; still not a damage change |
| `D-DMG-SET = 999` + probe bullet | **20** on hit; `bullet.init` **999** | ~1.5 s | ~14 | Bullet field write OK; **pea HitZombie does not use it for hit amount** |
| `P-ATK = 50` | **50** (after settle) | ~interval default | rises | Absolute plant ATK → hit damage |

### Multi-events per shot

Often **~3 `zombie.damage` rows per pea shot** at ~same amount (e.g. nine rows of 20 ≈ three shots). Asserts should use **amount + shot-gap**, not raw event count as “shots”.

## Mechanism (pea / `Bullet_pea`)

```text
Plant.attackDamage  ──(shoot / AnimShoot / SetBullet)──► projectile
                                                          │
Bullet.Damage  (InitData; D-DMG-* can set this)            │
                                                          ▼
                                              Bullet_pea.HitZombie  (override)
                                                          │
                                                          ▼
                                              Zombie.TakeDamage(amount, …)
                                                          │
                                                          ▼
                                              zombie.damage event (FusionRpg)
```

- Cecil: `Bullet_pea` **overrides** `HitZombie` (base `Bullet.HitZombie` is not the pea path).
- Butter-style mods call `TakeDamage(…, __instance.Damage, …)` — those bullets **do** consume `Bullet.Damage`.
- Live pea: changing **plant ATK** changes hit amount; changing **`Bullet.Damage` alone does not**.

## Effect-system design implications

1. **Separate effect axes** (PoE-style), not one “DPS” modifier:
   - `modifyHitDamage` / outgoing hit amount → plant ATK path (pea family); optionally bullet Damage for types that read it.
   - `modifyAttackSpeed` / `modifyAttackInterval` → `thePlantAttackInterval` / adder / CD.
   - Display DPS in UI only as **derived** from those two.

2. **Do not** implement “+50% DPS” as a single Writer field. Compose as either more hit damage, faster interval, or an explicit product rule in EffectBag.

3. **Targeting rules** (when Effects land):
   - Pea-like shooters: prefer **plant ATK** for hit damage.
   - Butter / custom projectiles that pass `Bullet.Damage` into `TakeDamage`: prefer **bullet Damage** (or both, with clear stacking rules).

4. **Probes / debug**
   - `p1-plant` = hit-damage path (proven).
   - `p1-bullet` = InitData field write only for pea (not hit amount).
   - Add interval scenarios when hardening attack-speed Effects.

5. **Harmony caution**
   - Patching base `Bullet.HitZombie` does not reliably wrap `Bullet_pea` and previously hard-crashed IL2CPP trampolines. Prefer TakeDamage / plant fields for product until subtype-safe hooks exist.

## Other plants (same probes, ~7s windows)

Raw JSON: [`_p1-other-plants-live.json`](_p1-other-plants-live.json).  
Probes: baseline → `A-P-ATK%=5` → `D-DMG-SET=999` + bullet probe.

| Plant | typeId | Base hit | ATK×5 hit | B999 hit | `bullet.init` @ B999 | Verdict |
|---|---|---|---|---|---|---|
| Cabbage-pult | 26 | **40** | **200** | **40** | 999 | Plant ATK; bullet field ignored |
| Kernel-pult | 28 | **20** | **100/120** mix | **20/40** mix | 999 | ATK mostly; butter/kernel secondary amounts |
| Melon-pult | 32 | **80** | **400** (+ splash **26**) | **80/132** mix | 999 | ATK for main; splash / extras muddy B999 |
| Fume-shroom | 7 | **20** | **100** | **20** | *(none)* | Plant ATK; no bullet.init (fume path) |
| Threepeater | 14 | **20** | **100** | **20** | 999 | Same as pea family |

**Takeaway:** Pea is not special — catapults / fume / threepeater also scale hit damage via **plant ATK**, not `D-DMG-*`. Kernel/Melon add multi-hit / splash noise; do not treat raw event avg as a single hit amount.

## Operator board left after probe

**Cabbage-pult** at col 2 / row 2 + high-HP basic zombie, mods reset to identity (wave freeze on).

## See also

- [04-proof-results.md](04-proof-results.md) — P1 table
- [open-questions.md](../open-questions.md)
- [cheat-menu-coverage.md](../cheat-menu-coverage.md) — `A-P-ATK*`, `P-ATK-INT`, `D-DMG-*`
- Cheat UI: tab **A** (ATK %), tab **B** (interval absolutes), tab **D** (bullet Damage field)
