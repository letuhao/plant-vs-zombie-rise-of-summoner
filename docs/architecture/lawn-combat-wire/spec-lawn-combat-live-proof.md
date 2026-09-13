# Spec: `lawn-combat-live-proof`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** every other module
**Standard:** [../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

Prove on a real board that a vanilla lawn hit now carries RPG elemental damage — and prove it in a way
that could **fail**. This module produces no source; it is an operation plus a recorded result.

It exists as its own module because this program's own founding measurement was invalid, and the trap
that invalidated it is specific to this feature.

## The trap this feature carries

**A debug session disables the exact path under test.**

```csharp
EventDrainHost.cs:32   static bool Active => Enabled && !DebugRuntime.SessionActive;
```

and the obvious instrument is worse than useless: `EmitOverlayBreakdown` only emits **inside** a debug
session (`InjectorCombatBridge.cs:~88-94`), and `SessionMode` additionally bypasses coalescing
(`EventDrain.cs:216`). So the natural way to observe this feature **turns it off and changes the
pipeline being observed**.

This already happened: the probe that founded this program captured 36 hits showing `before == after`,
with every event stamped `scenarioId` — a debug session was active, the record path was off by design,
and the measurement proved nothing. The conclusion survived only because code reading independently
supported it.

**Rule: the probe runs outside a debug session, and reads state through a path that does not flip the
mode.**

## Prerequisites

1. Build + deploy the injector; start the server with `Start-Process` directly — an agent-launched
   server dies when the tool call's process tree is reaped, which has been misread as a mid-run crash
   before.
2. `GET /health` → `Ok: true, InjectorConnected: true`.
3. A real board, entered through real play or the cold-start sequence.
4. **No debug session active.** Confirm rather than assume — a stamped `scenarioId` on emitted events
   is the tell.

## The proofs

Each is a pair: a positive and its falsifier. A passing run with no negative case proves nothing.

| # | Claim | Positive | Falsifier |
|---|---|---|---|
| 1 | Attribution | The recorded attacker is the firing plant's ptr | It is **not** the bullet's ptr, and the resolved attacker is not the `{Hp=100,MaxHp=100,Atk=10}` stub |
| 2 | Element rides the actor | A Fire actor and an Ice actor deal **different** damage to the same target | Same species, same element ⇒ same damage |
| 3 | One swing, one trigger | A piercing shot hitting N zombies fires **one** action trigger | N victims still each take damage |
| 4 | Cost gates the rider | An exhausted actor's shot lands for its vanilla number with **no** elemental delta | After regen, the same actor's shot carries the delta again |
| 5 | Stat bleed independent | While exhausted, `attackDamage`/`maxHp` remain Hub-composed | Only the rider disappears, never the bleed |
| 6 | General creatures covered | A plain PvZ-spawned zombie (no `UniqueActor`) gets an elemental rider | Identical treatment to a Bound specimen |
| 7 | No double-kill | A target killed by a deferred delta dies once | `plant.die`/`zombie.die` emitted once per death |

## Perf — a precondition, not a footnote

**The existing baseline does not apply.** "300z at 4.44% frame share" was measured with the damage
trigger-mask **off** — the hook fast path is `if ((mask & kindBit) == 0) return;`, rebuilt only on
grant change (`event-pipeline-v2-ssot.md` §3.1, `:80`). `basic-attack-grant` binds an `OnDamageDealt`
grant to **every** lawn actor, which pins that bit on permanently and adds a packet plus two ActorHub
resolves per bullet hit.

Re-measure per `runbook/perf-probe-plan.md`, with the mask on, before this program is called done.
Record the new baseline in `docs/research/perf/`.

## Commands

```powershell
Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe
.\scripts\deploy-play.ps1 -NoServer
Invoke-RestMethod http://127.0.0.1:5088/health
.\scripts\probe-perf.ps1 -Scenario <id> -DurationSec 60
```

## Evidence rules

- Report the **persisted-state** half and the **live-engine** half separately, never merged into one
  verdict.
- A response body is never proof — read changed state back through the normal path.
- Never fabricate an actor, a loadout, or a deployment. A debug call may *trigger* a real operation;
  it may never *invent* the record it claims to prove.
- Record real numbers, not "ok". If a claim fails, record the observed values plainly — an honest FAIL
  correctly reported is this module succeeding.

## Boundaries

- **Always:** run outside a debug session; record both halves; state what was run.
- **Ask first:** if a probe needs a code change to run — fix the owning module and re-run, never patch
  around it inline.
- **Never:** cite `EmitOverlayBreakdown` from inside a debug session as evidence this feature works;
  declare the program done on a positive run with no falsifier.

## Success criteria

- [ ] All seven proofs run, each with its falsifier, outside a debug session.
- [ ] Real numbers recorded for each — not a boolean.
- [ ] A fresh perf baseline with the trigger-mask on, recorded in `docs/research/perf/`.
- [ ] Any failure is reported with observed values and left to its owning module, not patched here.
