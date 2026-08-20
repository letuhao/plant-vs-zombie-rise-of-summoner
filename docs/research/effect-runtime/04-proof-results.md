# Proof results (P1–P6)

Date: **2026-08-15**. Research pass for effect-runtime. No EffectBag shipped.

Evidence tiers:

| Tier | Meaning |
|---|---|
| **LIVE** | Observed in a FusionRpg play session (this or prior documented capture) |
| **CODE** | Proven by reading FusionRpg injector / cecil signatures |
| **MOD** | Proven by local third-party BepInEx decomp calling the API |
| **PENDING** | Needs a FusionRpg live match with controlled cheats |

Game was **not** launched in this pass; LIVE rows cite **prior** documented capture / architecture tests where noted. Remaining PENDING items stay in [`../open-questions.md`](../open-questions.md).

## P1 — ATK path (`attackDamage` vs `Bullet.Damage`) + metrics

See also: [`06-combat-metrics-hit-vs-speed.md`](06-combat-metrics-hit-vs-speed.md) (hit damage ≠ attack interval ≠ DPS).

| Check | Result | Tier |
|---|---|---|
| Projectile HitZombie bodies use `Bullet.Damage` when dealing damage | **Yes for butter-style** — `TakeDamage(…, __instance.Damage, …)` | **MOD** |
| FusionRpg Writer writes `Plant.attackDamage` | **Yes** — `EntityStatWriter` | **CODE** |
| Cheat can set/scale `Bullet.Damage` on `InitData` | **Yes** — `D-DMG-*`; live `bullet.init` showed **999** | **LIVE** |
| Changing **only** plant ATK changes **hit damage** (pea) | **Yes** — baseline 20 → ATK×5 **100**; gap stayed ~1.5s | **LIVE** |
| Changing **only** `P-ATK-INT` changes **fire rate** (pea) | **Yes** — gap ~1.42s → ~0.48s at 0.5; hit stayed **20** | **LIVE** |
| Changing **only** `Bullet.Damage` changes pea **hit damage** | **No** — init 999, `zombie.damage` still **20** | **LIVE** |
| Same ATK vs B999 pattern on cabbage / fume / threepeater | **Yes** — ATK×5 scales hit; B999 does not (see `06-…`) | **LIVE** |
| `Bullet_pea` overrides `HitZombie` | **Yes** (cecil) | **CODE** |

**Verdict:** Product outgoing **hit damage** for pea **and** cabbage / fume / threepeater is **`Plant.attackDamage`**. Kernel/melon follow ATK for main hits but add splash/butter noise. **Attack interval** is a separate axis. **DPS is derived**. `Bullet.Damage` remains valid for **butter/custom** HitZombie bodies that pass it into `TakeDamage`; do not assume it for these shooters.

## P2 — Hit identity (attacker + bullet + target)

| Check | Result | Tier |
|---|---|---|
| `Bullet.HitZombie(Zombie)` exists and is widely patched by mods | **Yes** | **CODE** + **MOD** |
| Bullet exposes `fromType`, `theBulletType`, `Damage` | **Yes** (cecil) | **CODE** |
| `TakeDamage` includes `IDamageMaker damageFrom` + `PlantType reportType` | **Yes** | **CODE** |
| FusionRpg damage emit includes attacker / bullet | **Yes** — zombie: TakeDamage Bullet cast; plant melee: `AttackPlant` (`source=attackPlant`); plant `damageFrom` on bite is often the plant itself | **LIVE** 2026-08-16 |
| Impossible without banned OnTrigger*? | **No** — TakeDamage Bullet cast is enough for pea onHit identity | **LIVE** |

**Verdict:** `combat.hit` is **READY** for pea→zombie (TakeDamage Bullet) and zombie→plant melee (`Zombie.AttackPlant` deferred). Base `HitZombie`/`HitPlant` Harmony remains **off**. Deferred **HitLand** applies without crash but rarely fires (`combat.hitland` still **NOT SHIPPED** — ~134 overrides).

## P3 — Status write (freeze / cold / butter)

| Check | Result | Tier |
|---|---|---|
| Methods `Buttered` / `SetCold` / `SetFreeze` / `SetPoison` exist | **Yes** | **CODE** |
| Mods call `Buttered` from HitZombie successfully | **Yes** | **MOD** |
| FusionRpg `Z-SLOW-*` writes speed floats | **Yes** (Writer extras) | **CODE** |
| Float-only writes produce visible CC in live match | **Movement yes, butter VFX no** — stops without butter look; clear → walks | **LIVE** (operator) |
| FusionRpg calls status **methods** | **Yes** via `/api/debug/apply-status` | **LIVE** + **CODE** |

**Verdict:** Product CC should prefer **methods** (`Buttered` / etc.) for correct VFX. Float writes can stop movement but are not a full butter apply.

## P4 — Spawn-from-command

| Check | Result | Tier |
|---|---|---|
| `CheatActions.SpawnExtraZombie` → `CreateZombie.SetZombie` + `pvz.spawn.extra.ack` | **Yes** | **CODE** |
| Server Intent `POST /api/pvz-intent/spawn-extra` + Activity `ExtraSpawnFired` | **Yes** | **CODE** + prior foundation tests (**LIVE**-adjacent) |
| Factories place entities in-run (Simple Spawner / cheat SP-*) | **Yes** | **LIVE** (prior) + **MOD** |
| `POST /api/debug/fire-spawn-extra` / `spawn-extra` | **Yes** — ack LIVE (default row=2) | **LIVE** |

**Verdict:** **WRITE-proven** for zombie extra spawn via Intent/cheat. Ready as Effect *action*. Missing only combat→enqueue wiring.

## P5 — Kill signal

| Check | Result | Tier |
|---|---|---|
| `Zombie.Die` / `DestoryZombie` → `zombie.die` emit | **Yes** | **CODE** |
| Activity projects `ZombieKilled` | **Yes** | **CODE** |
| Reliable enough for onKill design | **Yes** | **LIVE** (F18) |
| onkill-extra / onkill-status arms | **Yes** | **LIVE** (F14/F17) |
| onhit-extra / onhit-status | **Yes** via TakeDamage bridge | **LIVE** (F15/F16) |

**Verdict:** **CAPTURE-proven**. onKill/onHit debug arms work; killer identity still depends on P2 enrichment if needed.

## P6 — DEF alt paths

| Check | Result | Tier |
|---|---|---|
| Product DEF scales only `Plant`/`Zombie.TakeDamage` | **Yes** — `GameHooks` | **CODE** |
| Live plant DEF×5 | **Yes** — bite 50→10 | **LIVE** (F11) |
| Live zombie DEF×5 | **Yes** — pea 20→4 | **LIVE** (F12) |
| `RealTakeDamage` / `BodyTakeDamage` / `ApplyDamage` — no % DEF stack | **Yes** — capture + god zero only | **CODE** |
| Live path tags observed | `take` / `body` / `apply` emits present | **LIVE** (F13) |

**Verdict:** **Confirmed.** Alt paths bypass product DEF. Effects/onGetHit that assume “all damage” must account for `path=real|body|apply` or extend DEF later.

## Summary table

| ID | Topic | Status |
|---|---|---|
| P1 | Hit dmg / interval / bullet field | **LIVE closed** — plant ATK = hit; see [06](06-combat-metrics-hit-vs-speed.md) |
| P2 | Hit identity | **LIVE** — zombie via TakeDamage Bullet; plant melee via AttackPlant; HitLand rare |
| P3 | Status | **LIVE** methods + float event; StatusRuntime L2 catalog **27/27** 2026-08-19 |
| P4 | Spawn command | **LIVE** |
| P5 | Kill / onhit / onkill | **LIVE** |
| P6 | DEF | **LIVE** + CODE |

## StatusRuntime L2 (2026-08-19)

Full catalog prove on BepInEx lawn (`simEnabled=false`, `injectorConnected=true`).

- Script: [`../../../scripts/prove-status-full.ps1`](../../../scripts/prove-status-full.ps1)
- Dump: [`_prove-status-full.json`](_prove-status-full.json) — **27/27 PASS**
- Checklist: [`../../runbook/debug-live-checklist.md`](../../runbook/debug-live-checklist.md) F52–F78

Harness: `debug.effect.fire-synthetic` with `side=plant` uses the pea at col 2 / row 2 as `actorPtr`; seed zombie is `targetPtr` / `hostPtr`. Poll max event id (not `limit=1`), one scenario at a time, wait for `debug.run-steps.done`.

Resist: `iron-dot` / `iron-cc` → `PotencyFloor`; `immune-poison` → `Immunity`; blight neighbor `iron-contagion` blocked. Actor pin: caster plant `status.power.omni=100`.

## Overlay combat + Element Hub (2026-08-19)

Offline prove (no game required):

- `dotnet test tests/FusionRpg.Core.Tests` — **653/653 PASS** (includes §5 matrix golden tests, hit/crit/miss, heal pass-through, status isolation)
- Filter: `FullyQualifiedName~FusionRpg.Core.Tests.Combat`

LIVE rows (operator): [`../../runbook/debug-live-checklist.md`](../../runbook/debug-live-checklist.md) §10 C1–C6. Script: [`../../../scripts/prove-overlay-combat.ps1`](../../../scripts/prove-overlay-combat.ps1). Enable `OVERLAY-COMBAT` or `FUSIONRPG_OVERLAY_COMBAT=1` before typed hits.

| Check | Result | Tier |
|---|---|---|
| Ring-cycle matrix §8.5 golden table | **653 Core tests green** | **CODE** |
| Hybrid payload weighted matchup §5.3 | **PASS** unit tests | **CODE** |
| Heal bypasses matchup/hit/crit | **PASS** unit tests | **CODE** |
| No payload → pass-through | **PASS** unit tests | **CODE** |
| `debug.combat.overlay` emit on enqueue-delta | Wired in injector C3 | **CODE** |
| fire vs ice/air LIVE matchup bonus | **PENDING** operator | **PENDING** |

## Follow-up live probes (operator)

Checklist filled: [`../../runbook/debug-live-checklist.md`](../../runbook/debug-live-checklist.md).

Remaining:

1. Operator visual: float-butter vs method butter.
2. Reliable `combat.hitland` (subtype HitLand patches or alternate land signal) — base deferred patch alone is insufficient.
3. Butter-style bullet that *does* use `Bullet.Damage` for hit amount.
