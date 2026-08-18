# Effect runtime surface — index

Own-game research: what FusionRpg can **capture** and **write** in a live PVZ Fusion 3.8.1 run, aimed at the Foundation Effect system (onHit / status / spawn-from-proc).

**Foundation Effect architecture:** [`../../architecture/effect-system.md`](../../architecture/effect-system.md), testing [`../../architecture/effect-testing.md`](../../architecture/effect-testing.md).  
LIVE checklist: [`_checklist-effect-foundation-live.json`](_checklist-effect-foundation-live.json).

**Not** a product ADR. **Not** ARPG peer inspiration (that lives under [`../arpg-effects/`](../arpg-effects/00-index.md)).

## How to read

1. [`01-capability-matrix.md`](01-capability-matrix.md) — Effect-event × CAPTURE/WRITE status
2. [`02-hit-pipeline-candidates.md`](02-hit-pipeline-candidates.md) — HitZombie / TakeDamage / banned hot-paths
3. [`03-status-and-spawn-surface.md`](03-status-and-spawn-surface.md) — freeze/butter/factories
4. [`04-proof-results.md`](04-proof-results.md) — P1–P6 evidence
5. [`05-effect-system-prerequisites.md`](05-effect-system-prerequisites.md) — prerequisites + Foundation progress (bag shipped)
6. [`06-combat-metrics-hit-vs-speed.md`](06-combat-metrics-hit-vs-speed.md) — hit damage vs attack interval vs derived DPS (**LIVE**)
7. [`07-effect-opportunities.md`](07-effect-opportunities.md) — richer Effect list from game WRITE/CAPTURE surface
8. Live controllable tests: [`../../runbook/debug-pipeline.md`](../../runbook/debug-pipeline.md)

## Document map

| File | Topic |
|---|---|
| [01-capability-matrix.md](01-capability-matrix.md) | Effect-event matrix |
| [02-hit-pipeline-candidates.md](02-hit-pipeline-candidates.md) | Hit hooks |
| [03-status-and-spawn-surface.md](03-status-and-spawn-surface.md) | Status + factories |
| [04-proof-results.md](04-proof-results.md) | P1–P6 |
| [05-effect-system-prerequisites.md](05-effect-system-prerequisites.md) | Foundation progress + prereqs |
| [06-combat-metrics-hit-vs-speed.md](06-combat-metrics-hit-vs-speed.md) | Hit vs speed vs DPS |
| [07-effect-opportunities.md](07-effect-opportunities.md) | Effect list from game surface |

## Legend (same as modifiable-gameplay)

| Status | Meaning |
|---|---|
| **WRITE-proven** | FusionRpg or live capture proved gameplay impact |
| **WRITE-claimed** | Cheat / third-party mod exposes it; FusionRpg unverified |
| **MOD-proven** | Local third-party BepInEx decomp shows the API works (not FusionRpg) |
| **CAPTURE-only** | We emit events; we do not mutate |
| **DOC** | Exists on 3.8.1 cecil dump; unused by injector |
| **OUT** | Banned for FusionRpg product hooks |
| **needs-probe** | Remaining live playtest |

## Pipeline sketch

> **W0-D:** product `combat.hit` comes from TakeDamage (Bullet) + AttackPlant, not Hit* Harmony.

```text
CreateBullet.SetBullet → Bullet.InitData
       ↓
OnTriggerEnter2D / Update     [OUT for product Harmony]
       ↓
TakeDamage (Bullet cast) / AttackPlant   ← combat.hit SSOT (Hit* off)
       ↓
Zombie.Buttered / SetCold / SetFreeze / SetPoison   ← /api/debug/apply-status
       ↓
Zombie.TakeDamage(…, damageFrom, …, reportType, …)  ← FusionRpg DEF + damage log today
```

## Related

- [`../modifiable-gameplay.md`](../modifiable-gameplay.md) — general WRITE/CAPTURE inventory
- [`../harmony-hook-map.md`](../harmony-hook-map.md) — in-use patches
- [`../mod-loaders.md`](../mod-loaders.md) — BepInEx vs MelonLoader (host only; same Harmony depth)
- [`../../architecture/pvz-intent.md`](../../architecture/pvz-intent.md) — `pvz.spawn.extra`
- [`../arpg-effects/06-fusionrpg-mapping.md`](../arpg-effects/06-fusionrpg-mapping.md) — peer EffectBag sketch
