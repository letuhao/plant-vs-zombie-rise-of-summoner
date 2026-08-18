# Situation catalog

Situations the locked architecture must handle. IDs feed [02-break-matrix.md](02-break-matrix.md).  
Sources: internal research + architecture specs. Observation only — not product ADRs.

## 1. Combat Hot

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-HIT-PROC-FREEZE | Gear: 5% freeze on hit fires on `combat.hit` | overlay-control-loops; arpg 03-procs | Hot |
| S-HIT-PROC-HEAL | Gear: 10% restore 500 HP on hit | overlay-control-loops | Hot |
| S-HIT-ICD-SPAM | Fast pea / multi-projectile would spam procs without ICD | arpg 03; effect-system ICD 250ms | Hot |
| S-HIT-SAME-FRAME | Multiple hits same frame / KeepHiting | effect-runtime 02 | Hot |
| S-HIT-OVERRIDE-GAP | `Bullet_pea` overrides HitZombie; base Hit* patch unsafe | open-questions; effect-runtime 02 | Hot |
| S-HIT-TAKEDAMAGE-ALT | `RealTakeDamage` / `BodyTakeDamage` / `ApplyDamage` bypass product DEF / may miss hit enrichment | open-questions; effect-runtime P6 | Hot |
| S-HIT-MELEE | Zombie `AttackPlant` path vs bullet Hit* | effect-runtime 02 LIVE | Hot |
| S-HIT-LAND | Ground HitLand / splash butter path | effect-runtime 02 | Hot |
| S-HIT-ATK-SOURCE | Hit damage follows `Plant.attackDamage` not `Bullet.Damage` alone | effect-runtime 06 | Hot |
| S-STATUS-BUTTER-FLOAT | Float butter stops without look vs method butter | open-questions closed LIVE | Hot |
| S-DEF-BYPASS | DEF only on TakeDamage Prefix; alt sinks unscaled | open-questions; modifiable-gameplay | Hot |

## 2. Identity

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-PTR-REUSE | IL2CPP reuses ptr after die; grants still on `entity:{ptr}` | unique-entity-effects | Hot + Cold |
| S-HYPNO | Hypno flag; still zombie cap bucket | match-runtime | Hot |
| S-PLACE-VS-SPAWN | `place` ignored for living; Activity still awards place | match-runtime; rpg-progression | Hot + Cold |
| S-BULLET-NO-DIE | Bullets tracked spawn-only; MaxLivingBullets=-1 | match-runtime | Hot |
| S-BIND-TIMEOUT | Unique PendingSpawn never gets spawn capture | unique-actor-runtime | Intent + Hot |
| S-TYPE-VS-INSTANCE | Type XP `(kind,type_id)` vs specimen `instanceId` both present | unique-actor; rpg-progression | Cold |
| S-INSTANCE-KEY-HOT | Temptation to Resolve `instance:{guid}` in StatSystem | unique-actor; overlay-control-loops | Hot |

## 3. Pause / phase

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-PAUSE-ADMIT | Player in pause UI; our Intent still tries Admit | match-runtime Paused | Hot |
| S-PAUSE-EMIT-SILENT | Unity pauses Emit entirely | match-runtime | Hot |
| S-ENDING-CLEAR | Ending ClearAll while hits still in flight | match-runtime; effect-runtime | Hot |
| S-PAUSE-HOOK-MISSING | No match.pause Emit yet; only NotifyPaused research note | match-runtime research | Hot |

## 4. Cold durable

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-EQUIP-MIDRUN | Equip item mid-run; re-push grants | overlay-control-loops | Cold → Hot |
| S-DEPLOY-FAIL | Deploy Intent accepted; Unity Create fails | unique-actor Deploying→Roster | Intent |
| S-RECOVER-CRASH | Game/injector crash while ActiveBound | unique-actor; process | Cold |
| S-STORAGE-PURGE-BOUND | User Storage clear while specimen ActiveBound | ledger-snapshot W12 | Cold |
| S-LEVELUP-MODS | Specimen levels; personal mod defs change | unique-actor reserved tables | Cold |

## 5. Caps / Intent

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-CAP-REJECT | Admit rejects at plant/zombie max under load | match-runtime CapPolicy | Intent + Hot |
| S-VANILLA-WAVE | Vanilla wave spawn ignores CapPolicy (by design) | match-runtime locks | Hot |
| S-FA4-VS-CAP | FA4 SpawnEntity without Admit | overlay-control-loops; effect-runtime | Hot |
| S-EXTRA-SPAWN-DOUBLE | ExtraSpawnFired fact + source=extra capture double count | pvz-middle-layer | Intent + Cold |

## 6. Dual host / process

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-DUAL-LOAD | BepInEx + Melon both loaded | mod-loaders | multi |
| S-INJ-RECONNECT | Injector SignalR drops mid-match; reconnects | data-flow; injector | multi |
| S-SERVER-RESTART | Server process restart mid-run; Data OK, RAM bag gone | overlay-control-loops | Cold + Hot |
| S-HOST-SWITCH | Same save played under Melon 3.9 vs BepInEx 3.8.1 field shapes | melon research; game-versioning | multi |

## 7. Data plane

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-COMPACT-MIDRUN | Temptation to compact/archive mid-run | ledger-snapshot | Cold |
| S-FE-ROLLUP-GRANT | FE drives Grant from Activity rollups as living counts | match-runtime FE rules | Cold |
| S-DAL-BYPASS | SQL from Server controller / Injector | guard-dal; architecture-map | Cold |
| S-EVENTS-ADMIT | Use `events` / entities table for AdmitSpawn | match-runtime bans | Hot |
| S-OBSERVE-LAG | FE Snapshot lag behind lawn; player clicks deploy twice | overlay observe≠control | Cold + Intent |

## 8. Secondary / content

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-GRANT-LEAK-DIE | Die without Withdraw; next unit inherits elite mods | unique-entity-effects | Hot |
| S-SECONDARY-UNITY | Secondary plugin calls StatusExecutor / Unity | effect-system hard law | Hot |
| S-SCOPE-FIGHT | `match` grant + `entity:{ptr}` grant stack unexpectedly | StatApplyScope; unique-entity | Hot |
| S-ONKILL-CHAIN | onKill spawn → new kill → infinite | arpg 03; ICD | Hot |
| S-DOT-LUCKY | DoT ticks would spam Lucky-Hit-like procs | arpg 03 D4 budget | Hot |

## 9. External-pattern stress

| ID | Situation | Source | Loop |
|---|---|---|---|
| S-SRV-PROC-RNG | Product asks Server to own 5% freeze roll then apply | Gambetta/AccelByte analogy vs overlay-control-loops | multi |
| S-PREDICT-HEAL | Hot heal then Unity overwrites LimHealth | prediction/reconcile analogy; open-questions LimHealth | Hot |
| S-TRUST-KILL-XP | Injector lies / duplicates kill events for type XP | AccelByte “if player can lie”; Activity dedupe | Cold |
| S-CMD-NOT-RESULT | Client reports “I froze zombie” instead of Intent deploy | Gambetta intent vs result | Intent |

## Coverage check (plan buckets)

| Bucket | IDs present |
|---|---|
| Combat Hot | S-HIT-*, S-STATUS-*, S-DEF-* |
| Identity | S-PTR-*, S-HYPNO, S-PLACE-*, S-BULLET-*, S-BIND-*, S-TYPE-*, S-INSTANCE-* |
| Pause / phase | S-PAUSE-*, S-ENDING-* |
| Cold durable | S-EQUIP-*, S-DEPLOY-*, S-RECOVER-*, S-STORAGE-*, S-LEVELUP-* |
| Caps / Intent | S-CAP-*, S-VANILLA-*, S-FA4-*, S-EXTRA-* |
| Dual host / process | S-DUAL-*, S-INJ-*, S-SERVER-*, S-HOST-* |
| Data plane | S-COMPACT-*, S-FE-*, S-DAL-*, S-EVENTS-*, S-OBSERVE-* |
| Secondary / content | S-GRANT-*, S-SECONDARY-*, S-SCOPE-*, S-ONKILL-*, S-DOT-* |
| External-pattern | S-SRV-*, S-PREDICT-*, S-TRUST-*, S-CMD-* |
