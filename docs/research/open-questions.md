# Open questions

Collection only. No decisions.

See also: [effect-runtime/04-proof-results.md](effect-runtime/04-proof-results.md) for P1–P6 evidence tiers.

## Confirmed

- Harmony injection into `Plant` / `Zombie` / `Board` / `Bullet` works; those types exist on 3.8.1.
- HP and speed can be edited by writing properties after spawn.
- Match cache = `Board.Awake` / `OnDestroy`.
- Entity lists = `Start` + `Die` (+ `DestoryZombie`).
- Run totals live on `Board.boardStatistics`.
- Plugin IO works from BepInEx IL2CPP because the plugin is CoreCLR.
- Dual-host is two artifacts (BepInEx + MelonLoader), never dual-load. See [mod-loaders.md](mod-loaders.md) and [injector/dual-host-roadmap.md](../injector/dual-host-roadmap.md).
- `BoardStatistics.GameOver` fires on both win and lose (live capture 2026-08).
- Live wave is `Board.theWave` / `theMaxWave`; `boardStatistics.currentWave` stayed stale until GameOver in early polls.
- 3.8.1 spawn factories (Simple Spawner): `CreatePlant.SetPlant(col, row, PlantType)`, `CreateZombie.SetZombie(row, type, x)`.
- Fusion recipes live on `PlantMixTreeManager.ChildToParents` (read from the running game).
- Product DEF scales only `TakeDamage`; `RealTakeDamage` / `BodyTakeDamage` / `ApplyDamage` bypass % DEF (**CODE**, effect-runtime P6).
- Intent `pvz.spawn.extra` → `CreateZombie.SetZombie` + ack is implemented (**CODE**, P4).
- `zombie.die` is a usable onKill signal (**CODE** + prior live, P5).
- `Bullet.HitZombie` / `HitPlant` / `HitLand` emit `combat.hit` / `combat.hitland` when debug session or logDamage (**CODE**, 2026-08).
- Debug status apply calls `Buttered` / `SetFreeze` / `SetCold` / `SetPoison` via `/api/debug/*` (**CODE**).
- At hit time, some projectiles pass `Bullet.Damage` into `TakeDamage` (**MOD**, butter-style). **Pea + cabbage/fume/threepeater LIVE:** hit amount follows **`Plant.attackDamage`**, not `Bullet.Damage` alone ([06-combat-metrics](effect-runtime/06-combat-metrics-hit-vs-speed.md)).
- Controllable test surface: [runbook/debug-pipeline.md](../runbook/debug-pipeline.md).
- Debug LIVE checklist F1–F23 mostly proven 2026-08-16 ([runbook/debug-live-checklist.md](../runbook/debug-live-checklist.md)): status methods, DEF scaling, onkill/onhit arms (onhit via TakeDamage), kills, spawn-matrix.
- **W0-D closed:** product `combat.hit` = TakeDamage Bullet + `AttackPlant` (not base Hit*); emit when LogDamage/hit-capture **or** OnDamageDealt grant. F4/F4b PASS. HitLand / `combat.hitland` remains W12 triage.

## Not confirmed at runtime

- Does `Plant.Start` postfix see final HP, or does something overwrite HP after Start?
- Does `Plant.LimHealth` clamp max HP back to PlantData after FusionRpg Writer apply? **W11-B Bend** (no LIVE prove this wave). Observe via `stat.limhealth`; `SYS-LIMHEALTH-GATE` stays default off until an operator sees `revertedVsWriter=true`.
- Is `Zombie.InitHealth` before or after `Start`? Which is the last write?
- ~~Does changing only `Plant.attackDamage` change bullet hits, or must `Bullet.Damage` change?~~ **Closed for pea + several shooters** — plant ATK = hit damage; `Bullet.Damage` alone does not (2026-08-16 LIVE).
- Does base `Bullet.HitZombie` virtual run for all common peas? (**Likely no** — `Bullet_pea` overrides; base Hit* patches unsafe/crashed. **W0-D:** do not use base Hit* as FT* primary.)
- ~~Does `IDamageMaker damageFrom` on TakeDamage point at Bullet, Plant, or neither consistently?~~ **Partial LIVE:** pea→zombie Bullet; plant melee often plant-self (use AttackPlant for attacker).
- ~~Does float-only `Z-SLOW-*` produce visible CC vs calling `Buttered`/`SetFreeze`?~~ **Closed LIVE:** method butter **stops + butter look**; float-butter **stops without butter look**; clear → walks again (2026-08-16 operator).
- Does `Plant.GetDamage` / `Zombie.GetDamage` already apply game multipliers we would double-apply?
- Does `ModifyDamage` / `ModifyHealth` persist correctly vs raw field writes?
- Do 149 existing plugins already patch `Plant.Start` / `Zombie.Start` in a way that fights extra HP writes?

## RPG ideas mentioned in chat (not designed)

These are reminders of intent, not a spec:

- Collect data per match
- Player level, random loot
- Fill plants by **percent or flat** HP / ATK / DEF
- Buff zombies the same way
- Store outside the game (SQLite or text)

Unresolved on purpose:

- JSON vs SQLite vs JSONL
- One global multiplier vs per-`PlantType` table
- When loot rolls (level start vs win)
- How DEF maps (damage Prefix vs HP-only)
- UI vs log-only first test

## Next doc work (still not code)

Useful to add later, still as notes:

5. Live expansion F24+ (speed, economy, onSpawn/onDeath inspect, produce, board.config, MC) via [runbook/debug-live-checklist.md](../runbook/debug-live-checklist.md) §6 — see [effect-runtime/07-effect-opportunities.md](effect-runtime/07-effect-opportunities.md).
6. Re-dump `Board` fields without the Health/Die filter (`theWave`, `zombieSpawnHealth`).
7. Dump `Plant.DieReason` enum values.
8. Dump `PlantType` / `ZombieType` counts on 3.8.1.

## Design (moved)

Architecture, protocol, schema, and v1 specs now live under `docs/architecture/`, `docs/protocol/`, `docs/database/`, and the per-module spec folders. This file stays a research risk list.

Effect-oriented inject/capture inventory: [effect-runtime/00-index.md](effect-runtime/00-index.md).
