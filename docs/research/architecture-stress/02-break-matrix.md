# Break matrix

Attack each situation from [01-situation-catalog.md](01-situation-catalog.md) against locked Hot/Cold/Intent + MatchRuntime + UniqueActor.  
Verdicts: **Hold** | **Bend** | **Break** | **Unknown**. See [00-index.md](00-index.md).

## Combat Hot

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-HIT-PROC-FREEZE | Force Server to roll freeze then POST apply | Hot EffectBag only; Server observe | **Hold** | P1 | — |
| S-HIT-PROC-HEAL | Same with heal 500 via Server RTT | Hot Writer; fail-closed if ptr gone | **Hold** | P1 | — |
| S-HIT-ICD-SPAM | Omit ICD on fast pea | Default ICD 250ms on damage-side | **Hold** | P2 | Tune ICD per weapon speed later |
| S-HIT-SAME-FRAME | Two hits same frame both proc | ICD + chance local | **Bend** | P2 | Per-frame proc budget |
| S-HIT-OVERRIDE-GAP | Rely only on base `Bullet.HitZombie` patch | Research: subtypes override; TakeDamage arm used | **Bend** | P0 | Per-subtype Hit* or TakeDamage-as-SSOT for FT* |
| S-HIT-TAKEDAMAGE-ALT | Procs only on TakeDamage Prefix; RealTakeDamage kills | DEF/product on TakeDamage only | **Bend** | P1 | Enumerate sinks; optional arm |
| S-HIT-MELEE | Melee AttackPlant vs bullet path inconsistency | Both can emit combat.hit (LIVE notes) | **Bend** | P2 | Unify FT* adapter enrichment |
| S-HIT-LAND | Splash land never triggers gear on-hit | HitLand later / optional | **Unknown** | P3 | Prove HitLand Emit coverage |
| S-HIT-ATK-SOURCE | Gear modifies Bullet.Damage only | Plant.attackDamage is hit SSOT for peas | **Hold** | P2 | Document Writer targets for ATK gear |
| S-STATUS-BUTTER-FLOAT | Use float-only for “freeze look” | Method vs float differ LIVE | **Bend** | P2 | FA2 always method path for look |
| S-DEF-BYPASS | Expect DEF on BodyTakeDamage | Spec: TakeDamage only | **Bend** | P2 | Accept or extend DEF surface |

## Identity

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-PTR-REUSE | Die → immediate respawn same ptr with old grants | Withdraw before reuse | **Break** | P0 | Auto withdraw-on-die is **required** before unique gear ships |
| S-HYPNO | Move hypno to plant dict for caps | Spec: stay zombie bucket | **Hold** | P3 | Secondary filter flag |
| S-PLACE-VS-SPAWN | Double-count living on place+spawn | place ignored for BoardProjection | **Hold** | P2 | — |
| S-BULLET-NO-DIE | Cap bullets when die capture missing | MaxLivingBullets=-1 until proven | **Hold** | P3 | Prove bullet destroy kind |
| S-BIND-TIMEOUT | Stuck Deploying forever | Deploying→Roster on fail | **Bend** | P1 | Timeout + correlation GC |
| S-TYPE-VS-INSTANCE | Award specimen XP into type PK | Orthogonal grains | **Hold** | P1 | Separate tables when built |
| S-INSTANCE-KEY-HOT | Pass `instance:` into Resolve | Binder only; ban in Hot | **Hold** | P0 | Guard test for instance: in Resolve |

## Pause / phase

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-PAUSE-ADMIT | Spawn extras during pause menu | phase.paused reject | **Hold** | P2 | Wire NotifyPaused |
| S-PAUSE-EMIT-SILENT | BoardProjection stale during pause | Spec allows stop updating | **Hold** | P3 | — |
| S-ENDING-CLEAR | ClearAll while projectile in flight | Fail-closed apply | **Bend** | P2 | Drain window or ignore post-Ending |
| S-PAUSE-HOOK-MISSING | No pause Emit; Admit still Ok | Research gap; NotifyPaused required | **Bend** | P1 | Add pause capture or Harmony Notify |

## Cold durable

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-EQUIP-MIDRUN | Expect past hits to rewind after re-push | Next hits only | **Hold** | P2 | UX copy: “applies to future hits” |
| S-DEPLOY-FAIL | Leave ActiveBound without ptr | Deploying→Roster | **Hold** | P1 | Implement timeout |
| S-RECOVER-CRASH | ActiveBound forever after crash | Recovering on observe; orphan phases | **Bend** | P1 | Stale ActiveBound sweeper on Server start |
| S-STORAGE-PURGE-BOUND | Purge actor row while on lawn | Not specified deeply | **Break** | P1 | Ban purge while ActiveBound / force retire |
| S-LEVELUP-MODS | Level-up requires Server mid-hit power | Cold push then Hot | **Hold** | P2 | — |

## Caps / Intent

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-CAP-REJECT | Flood Intent; ignore CapPolicy | Admit reject + optional debug.run.cap | **Hold** | P2 | — |
| S-VANILLA-WAVE | Blame CapPolicy for lag from vanilla | Caps our extras only | **Hold** | P1 | Product messaging |
| S-FA4-VS-CAP | FA4 Create without TryAdmitSpawn | Spec requires Admit first | **Break** | P0 | Impl gate must exist before unique deploy volume |
| S-EXTRA-SPAWN-DOUBLE | Double Activity on extra | Middle-layer ban second project | **Hold** | P2 | Guard test |

## Dual host / process

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-DUAL-LOAD | Load both hosts | Never dual-load | **Hold** | P1 | Launcher / docs enforce |
| S-INJ-RECONNECT | Reconnect; empty Effect bag; lawn still has buffed units | Bag is process RAM | **Bend** | P0 | Re-hydrate grants from Server on hello |
| S-SERVER-RESTART | Server down; Hot still procs; Cold stuck | Hot independent; Cold delayed | **Bend** | P1 | Queue Cold ops; UniqueActor observe lag OK |
| S-HOST-SWITCH | 3.8 Int32 vs 3.9 Int64 health fields | game-versioning | **Bend** | P1 | Versioned Writer adapters |

## Data plane

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-COMPACT-MIDRUN | Compact during match | Never mid-run | **Hold** | P1 | guard already sealed |
| S-FE-ROLLUP-GRANT | FE Grants from rollup living counts | FE must not | **Hold** | P2 | FE lint / API shape |
| S-DAL-BYPASS | Sqlite in controller | Data only + guard-dal | **Hold** | P0 | — |
| S-EVENTS-ADMIT | Admit from entities table | Ban | **Hold** | P0 | — |
| S-OBSERVE-LAG | Double deploy from laggy FE | Observe≠control; Server phase gate | **Bend** | P2 | Idempotent deploy correlation |

## Secondary / content

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-GRANT-LEAK-DIE | Same as ptr reuse without withdraw | Invariant documented; **not built** | **Break** | P0 | Ship withdraw-on-die with unique gear |
| S-SECONDARY-UNITY | Secondary calls Unity | Hard ban | **Hold** | P0 | Architecture tests |
| S-SCOPE-FIGHT | match + entity stack | Both apply if match Resolve | **Bend** | P2 | Stacking policy doc |
| S-ONKILL-CHAIN | Kill→spawn→kill loop | ICD / caps | **Bend** | P2 | Cap onKill depth |
| S-DOT-LUCKY | DoT tick spam procs | No DoT budget yet | **Unknown** | P3 | Lucky-Hit-style budget if DoTs added |

## External-pattern stress

| ID | Attack | Expected by spec | Verdict | Risk | later_idea |
|---|---|---|---|---|---|
| S-SRV-PROC-RNG | “Real games put procs on server” | Overlay Hot ban Server-in-hit | **Break*** | P0 | *Break only if product chooses Server RNG; else Hold by rejecting the requirement — see [03](03-external-patterns.md) |
| S-PREDICT-HEAL | Hot heal then LimHealth clamp | Fail-closed; LimHealth Bend | **Bend** | P1 | W11-B: documented Bend, gate still off |
| S-TRUST-KILL-XP | Spam fake kill captures for XP | Activity dedupe keys; injector is trusted local | **Bend** | P2 | Accept single-player trust; harden dedupe |
| S-CMD-NOT-RESULT | Client sends “frozen=true” result | Intent + Hot StatusExecutor | **Hold** | P1 | Keep command pattern |

## Verdict tallies (approx.)

| Verdict | Count (approx.) | Notes |
|---|---|---|
| Hold | many | Locks are coherent for intended model |
| Bend | substantial | Capture gaps, reconnect, pause wire, stacking |
| Break | **S-PTR-REUSE / S-GRANT-LEAK-DIE**, **S-FA4-VS-CAP** (if FA4 ships ungated), **S-STORAGE-PURGE-BOUND**, conditional **S-SRV-PROC-RNG** | Must confront before unique gear |
| Unknown | HitLand, DoT budget | Need research/LIVE |

## Break cluster (callout)

If unique equipment ships **without**:

1. Auto withdraw-on-die / ForgetEntity before ptr reuse  
2. FA4/Intent AdmitSpawn gate  
3. Re-hydrate grants after injector reconnect  
4. Storage purge policy while ActiveBound  

…the dual-FSM architecture **looks** locked in docs but **fails** in the lawn. These are the primary brainstorm targets in [04-enhancement-backlog.md](04-enhancement-backlog.md).
