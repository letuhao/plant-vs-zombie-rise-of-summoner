# Enhancement backlog (brainstorm seeds)

**Not decisions. Not ADRs.** Ordered seeds for a later workshop after reading [02-break-matrix.md](02-break-matrix.md).  
Do not implement from this file without a separate plan.

## Workshop status

**P0 workshop complete** — see [05-p0-workshop-verdict.md](05-p0-workshop-verdict.md).  
Implementation plan: [../../architecture/p0-hot-path-hardening.md](../../architecture/p0-hot-path-hardening.md).  
P1+ seeds below remain brainstorm-only until P0 ships or is explicitly deferred.

## P0 — architecture fails if skipped before unique gear

| Seed | From | One-line idea |
|---|---|---|
| B-WITHDRAW-ON-DIE | S-PTR-REUSE, S-GRANT-LEAK-DIE | Auto Withdraw `entity:{ptr}` + ForgetEntity on die/Ending before ptr reuse |
| B-ADMIT-FA4 | S-FA4-VS-CAP | Hard gate FA4/Intent Create behind MatchRuntime.TryAdmitSpawn |
| B-REHYDRATE-GRANTS | S-INJ-RECONNECT | **Shipped (W0-E session):** Hello → `effects.grants.apply`; match-scoped clear on board.start/end; scenarios recorded; ActiveBound W5+ |
| B-INSTANCE-RESOLVE-GUARD | S-INSTANCE-KEY-HOT | Test/guard: StatSystem.Resolve never sees `instance:` |
| B-HIT-SURFACE | S-HIT-OVERRIDE-GAP | **Shipped (W0-D):** FT* SSOT = enriched TakeDamage + AttackPlant; Hit* not architecture |

## P1 — dual FSM / ops holes

| Seed | From | One-line idea |
|---|---|---|
| B-PAUSE-WIRE | S-PAUSE-HOOK-MISSING | Harmony/Emit `match.pause` or NotifyPaused from pause menu |
| B-BIND-TIMEOUT | S-BIND-TIMEOUT, S-DEPLOY-FAIL | Deploying timeout → Roster; GC PendingSpawn |
| B-STALE-ACTIVE | S-RECOVER-CRASH | Server boot sweeper: ActiveBound with dead matchKey → Recovering |
| B-STORAGE-BOUND | S-STORAGE-PURGE-BOUND | API rejects Storage purge while phase ActiveBound |
| B-LIMHEALTH | S-PREDICT-HEAL | LIVE prove LimHealth vs Writer; gate or document Bend |
| B-ALT-DAMAGE | S-HIT-TAKEDAMAGE-ALT, S-DEF-BYPASS | Inventory which sinks need capture/DEF for gear honesty |

## P2 — feel / content policy

| Seed | From | One-line idea |
|---|---|---|
| B-PROC-BUDGET | S-HIT-SAME-FRAME, S-ONKILL-CHAIN | Per-frame or depth caps for procs |
| B-STACK-POLICY | S-SCOPE-FIGHT | Explicit match vs entity stacking rules |
| B-STATUS-LOOK | S-STATUS-BUTTER-FLOAT | FA2 always method path when VFX required |
| B-EQUIP-UX | S-EQUIP-MIDRUN | FE copy: mid-run equip applies to future hits only |
| B-DEPLOY-IDEMP | S-OBSERVE-LAG | Idempotent deploy correlationId |
| B-HOST-ADAPTER | S-HOST-SWITCH | Versioned health field Writer |
| B-KILL-DEDUPE | S-TRUST-KILL-XP | Harden Activity dedupe under reconnect storms |

## P3 — later / unknown

| Seed | From | One-line idea |
|---|---|---|
| B-HITLAND | S-HIT-LAND | Prove combat.hitland coverage for ground procs |
| B-DOT-BUDGET | S-DOT-LUCKY | If DoTs exist, Lucky-Hit-style tick budget |
| B-BULLET-DIE | S-BULLET-NO-DIE | Bullet destroy capture before enabling bullet caps |
| B-HYPNO-FILTER | S-HYPNO | Secondary filters Hypnotized without moving side |

## Explicit non-seeds (reject unless product ADR)

| Anti-seed | Why |
|---|---|
| Move on-hit RNG to Server | Breaks Hot lock; see [03-external-patterns.md](03-external-patterns.md) |
| Second Unity physics in MatchRuntime | Banned in match-runtime |
| AdmitSpawn from SQLite entities | Banned Data/Hot plane mix |
| Secondary → Unity shortcuts | Effect hard law |

## Suggested workshop agenda (later)

1. Confirm product stance: local overlay vs “Server owns combat” ([03](03-external-patterns.md)).  
2. Triage P0 seeds into one implementation plan (withdraw + Admit + rehydrate).  
3. Schedule LIVE for Unknowns (LimHealth, HitLand).  
4. Only then open UniqueActor schema / gear FE.
