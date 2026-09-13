# Spec: `lawn-hit-entry`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `lawn-hit-attribution`, `combat-numerics`

---

## Objective

Turn a captured vanilla lawn hit into an RPG `DamagePacket` that resolves through the existing overlay
stack, keyed on the **MatchRuntime board fold**, with one action trigger per swing.

Everything downstream of the packet already exists and is default-on: `CombatDamageDispatcher →
OverlayCombatCalculator → ElementHub → ShieldGate → EffectFunnel → FA10 Writer Add → elemental VFX`.
This module owns the entry, and the correctness rules that make a *deferred* per-hit pipeline safe.

Success: a vanilla pea or bite produces an elemental RPG delta on the live board, once per swing,
without dropping damage, double-killing, or attaching to a recycled pointer.

## Tech stack

`FusionRpg.Injector` (drain, hooks) + `FusionRpg.Core` (packet build, drain records). Unity-free Core
half. No Server round trip, ever.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EventDrain|DamagePacket"
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-actor-hub.ps1
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Effects/EventDrainHost.cs` | The gates; per-swing dedupe; liveness |
| `src/FusionRpg.Injector/GameHooks.cs` | Caller-side branch ordering (`:750`, `:870`, `:1039`) |
| `src/FusionRpg.Core/Combat/DamagePacketBuilder.cs` | Build from a lawn hit record |
| `src/FusionRpg.Core/Events/EventDrain.cs` | Drop/coalesce policy for effect-bearing records |

## The four gates

A hit is gated four times, not once. All four must be understood before changing any.

```csharp
EventDrainHost.cs:32   static bool Active => Enabled && !DebugRuntime.SessionActive;
EventDrainHost.cs:48   if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;   // bullet
EventDrainHost.cs:72   if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;   // melee
GameHooks.cs:750       ...else if (EventDrainHost.Enabled)   // AFTER a debug/telemetry Emit branch
```

| Gate | State | This module |
|---|---|---|
| `Enabled` | **default ON** (`InjectorLoop.cs:197-198`; opt out with `FUSIONRPG_EVENT_V2=0`). The class header saying *"Off (default until Task 10 wires the tick)"* is **stale** | fix the comment |
| `!DebugRuntime.SessionActive` | A debug session diverts every hit to the legacy path | **must not be "fixed"** — it is why a live probe must run outside a debug session (see `lawn-combat-live-proof`) |
| `HasOnDamageDealtGrant()` | The designed predicate | `basic-attack-grant` makes it true |
| caller-side `else if` ordering | A debug/telemetry branch wins before the recorder is reached | audit all three sites |

## One swing, one trigger

**Owner decision, 2026-09-13: one attack is one action trigger; triggering it multiple times is a
defect.** A piercing pea hitting five zombies is one swing.

- Dedupe the **action trigger** on the swing id (`bullet.Pointer`, supplied by
  `lawn-hit-attribution`). The cost is paid once per swing.
- Still resolve the **elemental rider per victim** — each victim has its own defence and its own
  element matchup, so one shared number would be wrong.
- Melee needs no dedupe: one bite is one victim already.

This is also the shipped slice's only rate control, because the proc coefficient is deferred to the
balance program. Without it, pierce/AoE multiplies value on an axis nothing prices.

## Deferred damage must not be dropped

**Corrected after owner review.** `combat.hit` is droppable today (`event-pipeline-v2-ssot.md:62`)
*because it has no consumer* — §4c's droppable/session-gated kinds are explicitly ones with *"no
consumer anywhere"*. **This feature changes that class.** Once a record carries an elemental delta,
dropping it is dropping gameplay, not telemetry.

That collides with the pipeline's own promise, and the collision must be resolved in favour of G5:

| G5 (`:35`) | §3.3 (`:61-62`) |
|---|---|
| *"worst case degrades to **delayed effects**, never to frame drops"* | *"coalesce harder, then **drop** droppable kinds with a counter"* |

**Rule: carry and coalesce, never shed.** An effect-bearing lawn hit joins the protected class
alongside the lifecycle kinds at §4c.3, whose rationale ("per-instance, per-ptr" integrity) extends
directly. The architecture does not currently describe this case — **this spec is where it gets
described**, and `event-pipeline-v2-ssot.md` should be amended to match.

## Lifecycle correctness

| Case | Today | Required |
|---|---|---|
| Target dies before the deferred delta lands | No liveness check. `InjectorEffectActionSink.cs:176-209` can find a dying-but-not-destroyed object; `EntityStatWriter.cs:176-227` guards only `== null`, and `next <= 0` calls `ForceKill*` (`:263-287`) → a **second `Die()`** | A death-resolution guard: an already-dead-or-dying target absorbs no further delta. Prior art is a death phase with an "already marked dead" flag, not per-hit death checks |
| Re-entrant drain vs ptr reuse | `FlushForPtr` is a no-op inside a nested drain (`EventDrain.cs:277`), so records can drain **after** grants withdraw — and FA10 kills inside a drain are exactly what this feature makes common | Records for a ptr must drain before `ForgetEntity` withdraws its grants, nested or not. Existing ordering: `GameHooks.cs:664/676`, `:1154/1156` |
| Lawnmower / instant kill | A 1,000,000-damage event was observed live | A rider must not scale off an instakill-shaped event |
| Attacker dies mid-flight | `bullet.from` is null | No RPG contribution; never a stub attacker (`lawn-hit-attribution`) |

## Code style

The hook records and returns; **nothing computes in the hook**. All resolution happens in the drain,
under its budget. Never `await` SignalR, HTTP or SQLite on this path — the Server observes
asynchronously and is never a decision gate (`overlay-control-loops.md:87`, Hot rule 3 `:150`).

Re-entry depth stays 0: an overlay apply must not emit a `combat.hit` that nested-flushes the Funnel.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | One swing id with N victim records → exactly **one** action trigger, N damage applications |
| Core unit | An effect-bearing hit is never dropped under budget exhaustion; it carries to the next frame |
| Core unit | Coalescing preserves total damage across merged records |
| Core unit | A dead/dying target absorbs no delta and triggers no second `Die()` |
| Core unit | Records for a ptr drain before that ptr's grants withdraw, including from a nested drain |
| Core unit | An instakill-shaped event produces no proportional rider |
| Guard | `guard-funnel-delta` (deltas only), `guard-single-writer`, `guard-actor-hub` all green |
| Perf | A fresh baseline with the damage trigger-mask **on** — see Boundaries |
| Live | `lawn-combat-live-proof` |

## Boundaries

- **Always:** record-then-drain; signed deltas; `mode=set` on current HP stays rejected; FA10 never
  calls Unity `TakeDamage`.
- **Always:** key on the board fold. A `UniqueBinding` is *enrichment when present*, never a
  precondition — a general creature has no binding and must still work.
- **Ask first:** adding any kind to the never-drop list beyond the effect-bearing lawn hit; changing
  the `!DebugRuntime.SessionActive` gate.
- **Never:** compute in the hook; await the Server on the hit path; modify vanilla projectile or bite
  formulas (`combat-damage-ssot.md:611`); fire more than one action trigger per swing.

## Success criteria

- [ ] A vanilla pea produces an elemental RPG delta on a live board, proven outside a debug session.
- [ ] One swing = one action trigger, N victims, proven by test **and** live.
- [ ] No effect-bearing hit is dropped under budget exhaustion.
- [ ] No double-kill; no delta applied to a recycled pointer.
- [ ] A general creature (no `UniqueActor`) works identically to a Bound specimen.
- [ ] **A fresh perf baseline is recorded with the trigger-mask on.** The existing "300z at 4.44%
      frame share" was measured with it **off** (`event-pipeline-v2-ssot.md` §3.1, `:80`) and does not
      apply — binding an `OnDamageDealt` grant to every lawn actor pins that bit on permanently.
- [ ] `event-pipeline-v2-ssot.md` amended to describe the effect-bearing record class.
