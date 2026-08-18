# P0 Hot-path hardening (implementation plan)

**Status:** **W0** + **W1 Core** + **W2 Injector MatchRuntime wire** + **W3 Pause + Snapshot observe** + **W4 UniqueActor Data+Server FSM** + **W5 Bind + binder + ops** + **W6 lawn monitor** + **W7 lawn Intent interact** + **W8 equipment / specimen XP / roster** + **W9 Secondary content kit** + **W10 Dual-host adapters** + **W11 Guards expand** closed. Next implement = **pre-play lawn observe** (HP / ATK / armor / status / tiles). W12 P2–P3 seeds stay **triage / not scheduled**.  
Workshop: [../research/architecture-stress/05-p0-workshop-verdict.md](../research/architecture-stress/05-p0-workshop-verdict.md).  
**Does not** ship gear FE or Server-owned procs.  
**Prerequisite stance:** local overlay Hot/Cold/Intent remains locked.

---

## Goal

Close the Break cluster so unique gear / volume Intent can land later without lawn grant leaks, uncapped FA4, or silent `instance:` Hot misuse.

## Slices (ship order)

### Slice A — Withdraw-on-die (MUST)

| Item | Detail |
|---|---|
| Problem | `ForgetEntity` clears baselines; **entity-scoped Effect grants** may remain → ptr reuse leak (`S-PTR-REUSE`) |
| Work | On `plant.die` / `zombie.die`: **Emit** (OnDeath) → `ForgetEntity` which withdraws `entity:{ptr}` then clears baselines |
| Touch | `EffectRuntime.WithdrawEntity` inside `ForgetEntity`; `GameHooks` Plant.Die / NoteZombieDead; Ending/match ClearAll unchanged |
| Prove | Unit: grant entity → WithdrawForOwner → bag empty for that owner; LIVE: die → respawn same type no inherited FA1 |
| Out | UniqueActor Recovering (Cold observe only) |
| Status | **Shipped (unit)** — LIVE operator gate open; order = Emit → withdraw → Forget |

### Slice B — Admit before FA4 / our Create (MUST)

| Item | Detail |
|---|---|
| Problem | `ExecSpawnEntity` Create without CapPolicy (`S-FA4-VS-CAP`) |
| Work | Gate spawn plant/zombie (and bullet if capped) via CapPolicy / future `MatchRuntime.TryAdmitSpawn` |
| Interim | Core `CapPolicy` + Injector `SpawnAdmit` using `CheatState.Living*` — **same reason codes**; MatchRuntime wraps in W1-C / W2-B |
| Touch | `FusionRpg.Core/Match/CapPolicy.cs`; `SpawnAdmit`; `ExecSpawnEntity`; `DebugActions.Spawn*`; `SpawnExtraZombie` |
| Prove | At max plants, FA4 reject + `debug.run.cap`; vanilla wave still uncapped |
| Out | Capping vanilla Harmony waves |
| Status | **Shipped (unit)** — interim CapPolicy; phase.* deferred to W1/W3 |

### Slice C — `instance:` Resolve guard (MUST, cheap)

| Item | Detail |
|---|---|
| Problem | Premature `instance:{guid}` in Hot Resolve (`S-INSTANCE-KEY-HOT`) |
| Work | `StatApplyScope` / Grant path: reject or no-op `instance:` in Resolve; Core.Tests assert |
| Touch | `StatApplyScope.cs`, `EffectOwnerKey.MatchesEvent`, `EffectBag.Grant`, tests |
| Prove | `dotnet test` guard; no ProjectReference to Data |
| Status | **Shipped (unit)** — `IsInstanceOwnerKey`; Matches/MatchesEvent false; Grant throws |

### Slice D — FT* hit surface lock (MUST decide; small code)

| Item | Detail |
|---|---|
| Problem | Subtype Hit* gaps (`S-HIT-OVERRIDE-GAP`) |
| Decision | **FT* SSOT for on-hit = enriched TakeDamage (+ existing melee `AttackPlant` arm)**; do not rely on base `Bullet.HitZombie` as primary |
| Work | Doc lock in effect-runtime + stress pointer; ensure EffectEventAdapter uses that path for procs; optional enrich `damageFrom` |
| Out | Mass per-bullet Hit* Harmony as architecture |
| Status | **Shipped** — effect-runtime SSOT; adapter maps `source=takeDamage` / `attackPlant`; Hit* off |

### Slice E — Rehydrate grants on hello (MUST before unique gear LIVE)

| Item | Detail |
|---|---|
| Problem | Reconnect empties Effect bag (`S-INJ-RECONNECT`) |
| Work | On injector connect/hello: Server pushes grant snapshot for current match (session grants + later ActiveBound loadouts) |
| Touch | Server hello/effects reload path; injector Grant apply; idempotent grant ids |
| Prove | Grant → disconnect simulator → hello → bag restored; lawn procs return |
| Note | May ship after A–D; **block unique gear lawn prove** until E or equivalent reload |
| Status | **Shipped (session)** — match-scoped `EffectGrantSession` (clear on board.start/end; scenario steps recorded); Hello → `effects.grants.apply`; ActiveBound W5+ |

---

## Explicit non-goals

- UniqueActor schema / deploy FE  
- Pause NotifyPaused (P1)  
- Storage purge while ActiveBound (P1)  
- Server proc RNG  
- Rewriting Foundation FA* opcodes  

---

## Suggested PR order

```text
A (withdraw) → C (guard) → B (Admit interim) → D (docs + adapter) → E (rehydrate)
```

MatchRuntime full FSM can absorb B’s CapPolicy later; do not block A/C on MatchRuntime.

---

## Success criteria

- [x] Die clears entity grants; ptr reuse does not inherit FA1 (unit shipped; LIVE operator)  
- [x] FA4/Intent Create fail-closed at cap (interim CapPolicy / SpawnAdmit; LIVE operator)  
- [x] Tests fail if `instance:` matches in Resolve (Grant reject + Matches/MatchesEvent false)  
- [x] effect-runtime states TakeDamage(+melee) as FT* on-hit SSOT  
- [x] Hello rehydrate restores bag (before unique gear seal) — session debug grants; ActiveBound W5+  
- [x] decisions.md P0 row remains true  

---

## W0 closed — deferred map (scope creep lock)

Do **not** reopen W0 for these. Class = **Next** (build in W1) | **Later** (roadmap wave) | **Ignore** (anti-design unless ADR).

| Out / deferred item | Class | Wave / note |
|---|---|---|
| MatchRuntime FSM absorb CapPolicy | **Next → done (W1-C)** | LIVE wire still **W2-B** |
| UniqueActor schema / Recovering / deploy FE | **Partial** | W4–W5 Data/Server/bind shipped; FE W8 |
| Pause `NotifyPaused` | **Done** | W3-A |
| Storage purge while ActiveBound | **Done** | W5-F (`unique.active_bound`) |
| ActiveBound loadout grant push on Hello | **Partial** | W5 Bound loadout on spawn; full Hello gear catalogs W8 |
| `instance:` → `entity:{ptr}` binder | **Done** | W5-B (`UniqueOwnerBinder`) |
| Pea shooter `attackerPtr` enrichment | **Later** | With unique-gear hit matching |
| HitLand / `combat.hitland` | **Later** | W12 triage B-HITLAND |
| ICD / alt DEF / LimHealth | **Partial** | W11-B LimHealth Bend (gate off); W11-C alt sinks inventoried; ICD W12 |
| Cap vanilla Harmony waves | **Ignore** | Only our Create/FA4/Intent capped |
| Mass `Hit*` Harmony as FT* architecture | **Ignore** | W0-D SSOT = TakeDamage + AttackPlant |
| Server on-hit / mid-hit proc RNG | **Ignore** | Hot lock |
| Admit from SQLite / second physics | **Ignore** | Anti-seeds |
| Replay `entity:{ptr}` across process restart | **Ignore** | Ptrs die |
| Rewrite Foundation FA* opcodes | **Ignore** | Needs ADR |
| W0-A/B LIVE operator prove | **Ops** | No code slice; Melon pack when available |

**Build next:** pre-play lawn observe. W12 P2–P3 product pick is **deferred**. **Do not** pull HitLand, bullets/hypno forward “to finish UniqueActor.” Outs: [match-runtime.md deferred map](match-runtime.md#w1-closed--deferred-map-scope-creep-lock).

---

## See also

- [overlay-control-loops.md](overlay-control-loops.md)  
- [unique-entity-effects.md](unique-entity-effects.md)  
- [match-runtime.md](match-runtime.md)  
- [../research/architecture-stress/00-index.md](../research/architecture-stress/00-index.md)  
- [implementation-roadmap.md](implementation-roadmap.md) — master W0–W12 checklist (W0 = this plan)  
