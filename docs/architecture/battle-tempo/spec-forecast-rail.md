# Spec: `forecast-rail`

Module `forecast-rail` in the [battle-tempo map](../battle-tempo-map.md).
**Depends on `action-timing` and `tempo-content`** — it renders a flat, useless list without them.

**Read before editing:** [battle-turn-ideal.md](../battle-turn-ideal.md) **§7** ·
[game-gui-principles.md](../game-gui-principles.md) · [design/information-architecture.md](../../design/information-architecture.md) ·
[fe-game-foundation.md](../fe-game-foundation.md).

---

## 1. Objective

**Show the player who acts next, so speed becomes a stat they can plan around.**

`TurnOrderForecast` is built, tested, and has **zero consumers**. The ideal calls it *"a free
read-model"* — because the queue *is* the state, "what happens next" is a pure projection: copy the
queue, roll it forward `K` events with no side effects, render the list.

Without this module, `tempo-content` makes speed matter invisibly. A stat the player cannot see is a
stat they cannot build around.

---

## 2. Design

### 2.0 ⛔ D3 — there is NO live queue after a battle resolves. This module renders a RECORD.

**Review finding, 2026-09-04, and it contradicted the first draft of this spec.**

`TurnOrderForecast.Project(EventQueue queue, …)` requires a **live queue**. But `BattleEngine.Resolve`'s
`roundQueue` is a **local variable** — drained and discarded when the method returns — and `BattleReport`
carries `Events`, not a queue. **So an expedition result has nothing to forecast from.** The first draft
specified something impossible.

⭐ **Owner decision (2026-09-04): render the acting order from `BattleTrace`.**
`BattleTrace.Turns` already records every `Ready → Committed` transition in order — which *is* the turn
order, recorded by the engine as it happened.

**This resolves §2.1's rule rather than violating it, and the distinction matters:**

| | Forecast | This surface |
|---|---|---|
| Source | live queue, projected forward | `BattleTrace.Turns`, recorded |
| Tense | what *will* happen | what **did** happen |
| Risk | disagreeing with the queue | none — the engine wrote it |

⛔ **So do not call it a forecast here.** It is a **turn-order record**. §2.1 forbids the *client*
computing order; it does not forbid rendering an order the engine itself recorded. The rule survives
intact because the client still computes nothing.

⚠️ **Cost this module must own: no production caller passes a trace today.** `WebMatchService` and the
expedition resolver both call `Resolve` without one, so it defaults to null. This module must make a
caller construct and retain a trace, and carry `Turns` to the surface.

- ✅ **`Turns` is deliberately excluded from `BattleTrace.Digest`** — its own comment says so, precisely
  so an observability addition cannot be mistaken for a behaviour change. **So persisting it moves no
  trace golden.**
✅ **Settled 2026-09-04 (owner): trace opt-in per battle.** No engine change, no new `BattleReport`
field — a caller that wants the rail constructs a `BattleTrace` and keeps it.

⭐ **The opt-in has an obvious right split, and it removes most of the cost worry:** trace **where a
human will look**, never in bulk resolution.

| Path | Trace? | Why |
|---|---|---|
| A web match resolved for display | ✅ yes | someone is about to read the result |
| Expedition resolution the player opens | ✅ yes | same |
| ⛔ The **boot sweep** re-resolving unresolved matches | **no** | nobody is watching; this is the bulk path, and `spec-virtual-time-core.md` already flags expeditions resolving four at a time server-side |

⚠️ **A full `BattleTrace` records draws, phases, targets and applies** — far more than the rail needs.
Opting in per battle keeps that off the bulk path entirely, which is where the cost would actually have
mattered.

⛔ **Planning finding, 2026-09-05: the split does NOT fall out by call site.** All three
`BattleEngine.Resolve` calls funnel through one private helper, `WebMatchService.ResolveAndIngest`
(`WebMatchService.cs:241`), and **`SweepUnresolved` calls that same helper** (`:229`) — so the boot
sweep and the player-facing resolve share a single resolve. The opt-in must therefore be a **parameter
threaded through `ResolveAndIngest`, defaulting to null**, with a trace passed only from the two
player-facing entries (`:109`, `:150`) and never from `:229`. Choosing "the right call site" would
silently trace the bulk path. **If tracing ever becomes the default, this decision must be revisited**, because the sweep is
exactly the workload it was scoped away from.

### 2.1 ⛔ Client-side computation is still forbidden

The ideal states this as a design rule: *"If the rail and the queue can disagree, we've built the bug
the whole SSOT effort exists to prevent."*

So the rail **must not** compute turn order. It renders what the engine produced — a projection where a
queue is live, `BattleTrace.Turns` where the battle is already resolved — and nothing else. No
client-side ordering, no re-sorting, no "helpful" interpolation.

### 2.2 Fidelity must be shown, because it genuinely degrades

`ForecastExactness` is already a **declared row field** on every profile — deliberately declared rather
than computed, because `ModeProfileArchitectureTests` bans branching on `AdvancePolicyKind`. Its three
values map to three different honesty obligations:

| Exactness | Profile | What the rail must show |
|---|---|---|
| `Exact` | `classic-round`, `galaxy-sync` | The full list, no hedging. Nothing can reorder before the next event. |
| `SoftBounded` | `hybrid-atb` | The first entries firmly, then a **visible soft boundary** — an action resolving inside the window can schedule ahead of a forecast entry. |
| `Absent` | live PvZ | **No forecast at all.** At best a "currently acting" readout. |

⛔ **`Absent` must render as absence, not as an empty list.** An empty rail reads as "nobody acts next",
which is a lie. The PvZ observer projects `ForecastExactness.Absent` for exactly this reason — *"we do
not own the clock, so there is nothing to project"*.

### 2.3 Where it lives — a layer, not a page

`game-gui-principles.md`'s GG-1 is explicit: **a game is a stage with layers, not a document with
pages.** The rail is an overlay on the battle stage, not a route, not a sidebar entry, and not a screen
the player navigates to.

⚠️ **There is no battle stage today.** The web app has `stages/`, `layers/` and `features/`, and the
only battle-adjacent surface is `ExpeditionsPage`. So this module either renders into an existing
stage or is **explicitly deferred until one exists** — see §6.

### 2.4 ⛔ No engine vocabulary on a player surface

The GUI row of the design gate names this as a recurring failure: `typeId`, `Intent`, `UniqueActor` and
friends must never reach a player surface. The rail shows **names and portraits**, never `actorKey`,
never tick numbers, never `TurnState`.

---

## 3. Commands

```powershell
cd web/fusion-rpg-web && npm test -- forecast
cd web/fusion-rpg-web && npm run build
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~TurnOrderForecast"
```

---

## 4. Project structure

```
src/FusionRpg.Core/Battle/Timeline/TurnOrderForecast.cs   EXISTS — no change expected
src/FusionRpg.Server/…                                    an endpoint or SignalR projection
web/fusion-rpg-web/src/contract/types.ts                  the DTO (mirror the C# shape)
web/fusion-rpg-web/src/layers/…                           the rail itself — a LAYER, not a page
```

⚠️ **The contract has a parity guard.** `UnitClassContractParityTests` exists because a type added to
one side and forgotten on the other shipped silently. Any DTO added here must have the same protection —
and note that on 2026-09-04 the C# enum was the side that lagged for a full day.

---

## 5. Testing strategy

1. **The rail equals what the engine recorded.** The rendered order matches `BattleTrace.Turns`'
   `Ready → Committed` sequence exactly — no client-side sort. Falsifier: reversing the client list must
   redden it.
1a. **Persisting `Turns` moves no trace golden** — asserted, since `Digest` excludes it by design.
2. **The projection mutates nothing.** Rolling the forecast forward `K` events leaves the queue
   byte-identical — the ideal's "pure projection" requirement, asserted rather than assumed.
2a. **The expedition rail reads as a record, not a prompt** — no "next"/"upcoming" copy on a resolved
   battle. Asserted on rendered text, because this is the one way this surface can lie.
3. **Each `ForecastExactness` renders its own honesty.** `Absent` renders *absence*, not an empty
   list — asserted distinctly from the empty case.
4. **No engine vocabulary reaches the DOM** — a scan for `actorKey`, `typeId`, `TurnState` in rendered
   output, mirroring the guard-script discipline used elsewhere.
5. **Contract parity** between the TS DTO and the C# record.

---

## 6. ⚠️ Sequencing risk — read before starting this module

This is the only module in the program that needs a **surface that does not exist**. There is no battle
stage in the web app; expeditions are auto-resolved and barred from interactive profiles.

✅ **Settled 2026-09-04 (owner): render into the expedition result view.** Informational, no
interaction, no new surface — speed becomes visible without inventing a stage.

⚠️ **Which makes one thing explicit: an expedition forecast is a REPLAY, not a plan.** The battle is
already resolved before the player sees it, so the rail here shows what *did* happen next, not what
*will*. That is honest and still useful — it is how a player learns that speed reordered a fight — but
the copy must not imply agency. **Do not label it "next turn"**; it is a record.

~~(b) Defer until a battle stage exists.~~ Not taken.

⛔ **Do not build a battle stage as a side effect of this module.** That is a whole surface with its own
design-gate obligations (`game-gui-principles.md`, `information-architecture.md`, `fe-game-foundation.md`)
and it is not what this spec authorises. If a stage is wanted, it is its own program.

---

## 7. Boundaries

- **Always:** render the projection verbatim; show fidelity honestly; keep it a layer.
- **Ask first:** ~~which surface hosts it~~ (settled: expedition result view); showing tick numbers to
  a player in any form; any copy implying the player can act on the forecast.
- **Never:** compute order client-side; render `Absent` as an empty list; put engine vocabulary on a
  player surface; add a top-level route or sidebar entry; build a battle stage under this module.

---

## 8. Success criteria

1. The rail renders exactly `TurnOrderForecast`'s output, proven by a falsifier.
2. The projection is provably side-effect-free.
3. All three exactness modes render distinguishably, with `Absent` showing absence.
4. No engine vocabulary on the surface; contract parity guarded.

---

## 9. Golden movement

**None.** This module reads and renders; it resolves nothing and writes nothing. If a golden moves here,
something is wrong — the forecast has become a second source of truth, which is the exact bug §2.1
forbids.
