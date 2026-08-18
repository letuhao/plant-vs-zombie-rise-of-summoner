# Effect system prerequisites

Ordered checklist of what must exist before implementing `EffectBag`. Maps to Activity / Intent / Stats. **Not** an ADR.

Architecture (Foundation design, docs): [`../../architecture/effect-system.md`](../../architecture/effect-system.md), [`effect-data.md`](../../architecture/effect-data.md), [`effect-runtime.md`](../../architecture/effect-runtime.md), [`effect-testing.md`](../../architecture/effect-testing.md).  
Peer inspiration: [`../arpg-effects/06-fusionrpg-mapping.md`](../arpg-effects/06-fusionrpg-mapping.md). Runtime surface: [`01-capability-matrix.md`](01-capability-matrix.md).  
LIVE checklist: [`_checklist-effect-foundation-live.json`](_checklist-effect-foundation-live.json).

## Ready now (do not block on)

| # | Prerequisite | Evidence |
|---|---|---|
| R1 | Flat → Increased → More compose + ModifierBag | `stat-system.md` |
| R2 | EntityApply → EntityStatWriter (HP/ATK) + TakeDamage DEF | injector Stats |
| R3 | Spawn / die / damage capture events | GameHooks / GameCaptureHooks |
| R4 | Intent `pvz.spawn.extra` + Activity `ExtraSpawnFired` / `ZombieKilled` | pvz-intent / pvz-activity |
| R5 | Dump knowledge of HitZombie + status methods | this folder + cecil |
| R6 | Single Unity writer law (Effects must not bypass Writer/Intent) | middle-layer constitution |
| R7 | **`combat.hit` identity** via TakeDamage Bullet + `AttackPlant` (plant melee) | LIVE F4 / hit-capture-plant — Hit* Harmony not required |
| R8 | **Status apply** via debug/`Buttered`/`SetFreeze`/… (method path) | LIVE F5–F10; onhit/onkill status arms |
| R9 | **Foundation EffectBag + FA1–FA9** | Core bag + injector sink; `FoundationContractVersion = 1` |
| R10 | **Offline IntentPlan harness** | `FoundationHarness` + goldens; Secondary must not open PVZ |

## Progress (Foundation coded)

| Item | Status |
|---|---|
| B3 Proc policy (ICD/chance/stacks on grant) | **Done** — Core `EffectProcPolicy` (damage ICD default 250ms) |
| B2 `pvz.status.apply` audit emit | **Done** — injector sink tags `effect_id` / `grant_id` |
| B5 Effect host | **Done** — injector bag + adapters; `effects.reload` push |
| LIVE L1–L14 scenarios | **Shipped** — Bep lawn PASS; **Melon 3.9 also PASS** 2026-08-16 ([`_prove-melon39-foundation-live.json`](_prove-melon39-foundation-live.json); [`melon-live-checklist.md`](../../runbook/melon-live-checklist.md)) |
| FA1 ModifyStat apply scope | **Done** — `StatModifier.ApplyOwnerKey` + Resolve filter; LIVE read via `debug.board-stats`; see [`unique-entity-effects.md`](../../architecture/unique-entity-effects.md) + [`smoke-effect-scoped-atk.ps1`](../../../scripts/smoke-effect-scoped-atk.ps1) |
| Secondary plugins | **Out of scope** (grant/overlay only when coded later) |

## Explicitly out until later

- Crit / Lucky Hit budgets (inspiration only)
- OA/DA miss chance (collision is hit)
- Ground-effect entity graph / reliable `combat.hitland`
- Items / power curve / Progression→damage
- Secondary Effect content (attrs, type buffs) — grant/overlay only when coded
- MatchRuntime board/caps lifecycle (design: [match-runtime.md](../../architecture/match-runtime.md); effect-testing A8) — implementation deferred

## Suggested build order (historical)

```text
1. StatusExecutor + pvz.status.apply Intent (wrap LIVE methods)     ✅
2. EffectBag Grant/OnEvent + adapters (combat.hit, die, spawn)     ✅
3. Executors FA1–FA9 → Writer / status / board / economy             ✅
4. ICD/chance on grant; harness goldens; LIVE scenarios              ✅
5. Secondary IEffectGrantPlugin samples                              ⏳ later
```

## Ready vs blocked (one-liner)

**Ready:** Foundation Effect is the sole game-touching apply path; contract v1 frozen for Plan/Grant/Event shapes; upper layers test via harness.  
**Still open:** operator lawn PASS for L1–L14 rows in `_checklist-effect-foundation-live.json`; Secondary content plugins.
