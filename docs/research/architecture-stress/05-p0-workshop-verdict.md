# P0 workshop verdict (Breaker / Defender / Judge)

**Date:** 2026-08-16  
**Input:** [02-break-matrix.md](02-break-matrix.md), [04-enhancement-backlog.md](04-enhancement-backlog.md), structured debate (Breaker + Defender).  
**Output ADR row:** [../../architecture/decisions.md](../../architecture/decisions.md) — Overlay P0 hardening.  
**Impl plan:** [../../architecture/p0-hot-path-hardening.md](../../architecture/p0-hot-path-hardening.md).

---

## Product stance (locked)

**Local Unity overlay + Cold RPG backend.**  
Reject Server-owned on-hit proc RNG (`S-SRV-PROC-RNG`). Accepting it would Break [overlay-control-loops.md](../../architecture/overlay-control-loops.md) by choice, not by fixing a hole.

---

## Debate summary

| Seed | Breaker | Defender | **Judge** |
|---|---|---|---|
| B-WITHDRAW-ON-DIE | MUST-SHIP — grant leak on ptr reuse | MUST-SHIP — documented; code missing | **MUST-SHIP** |
| B-ADMIT-FA4 | MUST-SHIP — CapPolicy bypass | MUST-SHIP — impl gap | **MUST-SHIP** |
| B-REHYDRATE-GRANTS | MUST-SHIP — bag RAM empty after reconnect | MUST-SHIP — ops gap, not new plane | **Shipped (W0-E session)** — Hello pushes session grants; ActiveBound W5+ |
| B-INSTANCE-RESOLVE-GUARD | NICE with binder | MUST-SHIP cheap guard | **MUST-SHIP** (cheap; ship with binder slice) |
| B-HIT-SURFACE | MUST-SHIP decide FT* SSOT | Decision MUST; Hit* flood REJECT | **Shipped (W0-D)** — TakeDamage (+ AttackPlant) = FT* SSOT; Hit* not primary |

### Code note (Judge)

Today `GameHooks` already calls `ForgetEntity` on plant/zombie die (clears StatSystem baseline). That is **not** full **Effect grant Withdraw** by `ownerKey=entity:{ptr}`. P0 withdraw-on-die must withdraw grants (and then ForgetEntity), or unique gear still leaks FA* bag state.

---

## Won’t-do (this hardening)

- Server mid-hit apply  
- Second physics / HP shadow world  
- Admit from SQLite  
- Primary FT* via base `Bullet.HitZombie` only  
- UniqueActor schema / FE roster (separate plan after P0)

---

## Ordered ship sequence

See [p0-hot-path-hardening.md](../../architecture/p0-hot-path-hardening.md):

1. Withdraw-on-die (entity grants) + Ending ClearAll already partial  
2. Admit gate on FA4 / spawn Intent (MatchRuntime or interim CapPolicy)  
3. `instance:` Resolve guard test  
4. Lock FT* hit surface in docs + adapter — **done (W0-D)**  
5. Rehydrate grants on injector hello — **done (W0-E session)**; ActiveBound loadouts W5+  

Workshop complete for P0. Unique-gear lawn prove still needs ActiveBound grant push (W5) on top of session rehydrate.
