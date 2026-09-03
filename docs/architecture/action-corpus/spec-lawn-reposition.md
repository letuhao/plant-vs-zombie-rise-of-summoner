# Spec: `lawn-reposition` (A-M2)

**Module id:** `lawn-reposition` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: no**
**Depends on:** `A-M1 movement-payload` · **⛔ BLOCKED on effect-atom `E33` (the activation edge) AND on a
lawn-side production producer** — ⛔ **CORRECTED 2026-09-03 (second pass):** that producer is **not**
`A9 movement-actions`, which is **battle-grid only** (`action-map.md:294`); see §7 hazard 4's
⛔ DECIDED note, which also records that this module **ships knowingly inert** rather than waiting — ⛔ **CORRECTED 2026-09-03 (review):** this spec named
E33 as the whole block, because it was written without reading
[`spec-activation-edge.md`](../effect-atom/spec-activation-edge.md). That spec says in its own words
that E33 *"does **not** own a game-facing producer — that is `A9 movement-actions`"*
(`spec-activation-edge.md:13-14`) and lists `A9` under **Unblocks**, *"which supplies the production
producer"* (`:277-278`). So E33 ships a **seam with no production caller of its own**, and its own
hazard table names that as the D6 recurrence — *"a path with no consumer is accepted and then does
nothing forever"* — with the mitigation that if A9 does not follow in the same window the map row
must say **inert**, in that word (`spec-activation-edge.md:282`). **A-M2 is downstream of both**, and
recording only E33 would have had this module waiting on a seam that still could not fire it.
⚠️ The capability map's gate still stands — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). Written ahead of approval on the owner's instruction.

**What it owns.** The **lawn enrichment half** of a movement action: a fifth Unity write path, deliberately
the narrowest one in the repo — **ONE guarded entry point, *move actor to cell*, routed through
`EntityApply`, owned by a single writer, recorded in the hook and applied in the budgeted drain**, with
`scripts/guard-single-writer.ps1` extended so nothing else may assign a `Plant`/`Zombie` transform
(`decisions.md:105`, *"Lawn position write (2026-09-02)"*, status **DRAFTED, not built**;
`action-corpus-ideal.md:1040-1067`). It enriches a movement action and never gates one — the payload half
(`A-M1`) is legal today and works with the game closed.

**⛔ Binding constraints, restated inline — a downstream session reads this file, not its links.**

1. **The LLM writes identity. Deterministic code writes magnitude.** No model picks a number, weight,
   probability, duration, tier or rung — and no model touches this module at all.
2. **Three pipelines, not one parameterised stage** (P-general, P-family, P-signature). A-M2 is consumed by
   none of them directly; it is the runtime half of what they author.
3. **Permute every enum**, seeded from `(entity_id, field, sample_index)` with `sample_index` inside the
   seed — applies to the generation side, never here.
4. **Majority-vote only load-bearing fields;** 1-1-1 → `unresolved`, never the first option.
5. **Every enum description carries a negative clause.** `none` is a value; a missing key is a defect.
6. **TRANSIENT ≠ QUALITY** on any run that reaches a model. Not this one.
7. **Small-batch proof first** — the reposition ships behind a default-off toggle and is proved live on a
   handful of moves before anything else depends on it.
8. **Tests never call a model** — the transport stub raises. Vacuous here by construction, and **not
   waived**: if tooling for this module ever lands in seedsmith, its transport stub raises.
9. **The roster is 84 species (53 with family assignments), not 904.**

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `EntityApply` — the single entry for Resolve → `EntityStatWriter`; spawn, `PushScales`, reapply and Tab B all call `Run*` | `src/FusionRpg.Injector/Stats/EntityApply.cs:13-17`, `RunPlant` at `:18`, `RunZombie` at `:131` |
| The write decision is a **value** question, centralised in `EntityWriteGate.ShouldWrite` | `EntityApply.cs:80-86` |
| `guard-single-writer.ps1` — 10 combat-field patterns, a filename allow-list, a `Bridges/` path exemption | `scripts/guard-single-writer.ps1:11-42` |
| Record-then-drain, already built and already the house pattern | `src/FusionRpg.Injector/Effects/EventDrainHost.cs:7-29`, drained from `Host/InjectorLoop.cs:79` under the frame budget |
| `LawnCoords.CellCenter(col,row)` → world position, with `ClampCol`/`ClampRow` | `src/FusionRpg.Injector/Lawn/LawnCoords.cs:55-56,59-72` |
| Cell state is readable today — `thePlantColumn`, `thePlantRow`, `theZombieRow` | `Effects/InjectorEntityRegistry.cs:179-180,210`; `Fx/UnitFrameResolver.cs:65,74` |
| `ActionCategory.Movement`, `ActionTag.Movement` | `src/FusionRpg.Core/Actions/ActionEnums.cs:26-33,37-47` |

### Wiring gap

| Thing | Evidence |
|---|---|
| **`OnActivate` is authorable but raised nowhere in the injector** — so no lawn action can fire at all | `decisions.md:97` (amended 2026-09-03); `effect-atom-map.md:337` (E33) |
| **E33 supplies the seam, not a producer** — `A9 movement-actions` supplies the production producer, and E33 ships with no production caller of its own | `spec-activation-edge.md:13-14`, `:277-278`, `:282` |
| **There is no `HasOnActivateGrant()` fast gate** — the injector has four `HasOn*Grant()` helpers and no `OnActivate` one, so a producer written today would have to fire unconditionally | `EffectRuntime.cs:145-159`, via `spec-activation-edge.md:38`; E33's acceptance criterion **6** requires the helper and requires every later producer to call it first (`:263`; ⛔ corrected 2026-09-03 — was cited as AC5 at `:159`) |
| `OnActivate` **can** already be fired by hand on a live lawn via `debug.effect.fire-synthetic` — a debug-only entry point, evidence the plumbing works, **not** a producer | `CheatCommandRunner.cs:364-368`, `:2052`; `EffectRuntime.cs:330-338`, via `spec-activation-edge.md:39` |
| `Instantiator.TryInstantiate` — doc-comment references only, no production caller | `Instantiator.cs:92`; `InstanceProducer.cs:22`, `Resolver.cs:28`, `RpgStore.AtomInstances.cs:104` |
| `move.range` is registered and has **no production reader** | `DerivedStatRegistry.cs:237`; `DominanceGuard.cs:103` is its only other mention |

### Real gap — verified line by line, 2026-09-03

- **No plant or zombie position write exists anywhere in the injector.** Every `transform.position =` is in
  `Fx/AuraPool.cs:80,117`, `Fx/BurstPool.cs:57` — VFX GameObjects — **or `Hud/ActorHudPool.cs:170`**, a HUD
  root, with two further HUD `localPosition` writes at `:225` and `:243`.
- **No write to `thePlantRow`, `thePlantColumn` or `theZombieRow` exists either.** All 20 references in the
  injector are reads or comparisons (`DebugActions.cs:1183,1221,1269`,
  `DebugRuntime.cs:198-199,348,382,417`, `InjectorEntityRegistry.cs:179-180,210`,
  `UnitFrameResolver.cs:65,74`, `GameCaptureHooks.cs:51`, `GameDumps.cs:47-48,235`,
  `LawnCoords.cs:118,144,157`).
- **⚠️ The ADR's own enumeration was incomplete and the same wrong sentence is committed.**
  `decisions.md:105` now carries the 2026-09-03 correction naming `Hud/`, but the guard has not been
  extended, so **the `Hud/` exemption still exists only as prose.** Writing it into the guard is part of
  this module, not a follow-up.

## 2. The API and the write path

### The one entry point

```csharp
// src/FusionRpg.Injector/Stats/EntityApply.cs — the ONLY public way to move an actor.
public static void MoveToCell(Plant p,  int col, int row, string source);
public static void MoveToCell(Zombie z, int col, int row, string source);
```

`source` is the same free-form provenance tag `RunPlant`/`RunZombie` already take (`EntityApply.cs:18,131`),
so a move is attributable in the same way a stat apply is. Both overloads:

1. null-check and return, as every `Run*` does;
2. clamp the destination with `LawnCoords.ClampCol` / `ClampRow` (`LawnCoords.cs:55-56`) — an out-of-board
   cell is a **clamp**, not an exception, because the destination is a coordinate and not a magnitude;
3. **record** the move; they never write inside the call.

### The single writer

A new file, `src/FusionRpg.Injector/Stats/EntityPositionWriter.cs`, is **the only file in the injector
permitted to assign a `Plant`/`Zombie` transform or cell field** — exactly the relationship
`EntityStatWriter` has to combat fields. It converts the cell to world space with
`LawnCoords.CellCenter(col,row)` (`LawnCoords.cs:59-71`) and performs the assignment. Nothing else calls it;
`EntityApply.MoveToCell` is its only caller, and the drain is `MoveToCell`'s only caller.

**⛔ `CellCenter` has a null-`Mouse` fallback that teleports an actor to near-origin — added
2026-09-03 (review).** This spec cited `CellCenter` as a solved cell-to-world conversion. Read line by
line, it is solved **only while `Mouse.Instance` is live**:

```csharp
// LawnCoords.cs:59-71
var mouse = Mouse.Instance;
if (mouse != null)
    return new Vector2(mouse.GetBoxXFromColumn(col), mouse.GetBoxYFromRow(row));
// ... catch { } ...
return new Vector2(col, row);          // <- col/row as WORLD coordinates
```

The fallback returns the **grid indices as a world position**. For a VFX burst or a probe that is a
harmless degradation — the effect lands somewhere wrong and vanishes. For an **actor** it is a
teleport to within nine world units of the origin, off the lawn, permanent, and silent: nothing
throws, nothing logs, and the actor keeps playing from there.

**So `EntityPositionWriter` must not call `CellCenter` and trust the result.** The rule, and it is
this module's, not `LawnCoords`':

- Resolve the world position through a **fallible** path — `Mouse.Instance` read once, explicitly —
  and when it is null or throws, **drop the move and count it**, exactly as a dead or mid-spawn actor
  is dropped (§3). A move is enrichment; not moving is always a legal outcome.
- **Never "fix" `LawnCoords.CellCenter`.** Its fallback is correct for its existing 20 read-only
  callers and changing it would move behaviour this module has no business moving. The narrowing
  belongs at the one write site.
- The drop is visible: the same counter the ring overflow uses, with its own reason, so a lawn where
  `Mouse` is absent reads as *"moves dropped: N (no coordinate source)"* rather than as a feature that
  silently did nothing.

### Record-then-drain

Modelled directly on `EventDrainHost` (`Effects/EventDrainHost.cs:7-29`), for the reason its own header
gives: a move performed inside a Harmony hook is a frame-budget bug waiting to happen.

- A hook (or an effect atom reacting to `OnActivate`) calls `MoveDrainHost.TryRecordMove(ptr, side, col,
  row, source)`, which appends to a bounded ring and returns whether it was recorded.
- `MoveDrainHost.Tick(dt)` runs from `InjectorLoop.Tick`, in the same slot the event drain already occupies
  (`Host/InjectorLoop.cs:79`), under the frame budget, and calls `EntityApply.MoveToCell` per record in
  recorded order.
- A ring overflow **drops and counts**, never blocks — the same contract `EventDrainHost` states.
- A master switch, default **off**, with an env kill (`FUSIONRPG_LAWN_MOVE=0`), mirroring
  `EventDrainHost.Enabled` (`EventDrainHost.cs:20`).

### Deltas-not-absolutes does not apply — stated so a later session does not "fix" it

A cell is a **destination**, not a magnitude. There is no baseline to preserve, no ratio to carry, no
contributor to compose. `EntityWriteGate.ShouldWrite` (`EntityApply.cs:80-86`) is a value comparison over
combat fields and is **not** consulted for a move; the move's own equivalent is "the actor is already in
that cell", which the writer checks and skips.

### The guard extension — and the `Hud/` exemption, written down

`scripts/guard-single-writer.ps1` gains:

- **new patterns**: `thePlantRow\s*=`, `thePlantColumn\s*=`, `theZombieRow\s*=`, `transform\.position\s*=`,
  `\.localPosition\s*=`;
- **allow-list**: `EntityPositionWriter.cs` added to `$allowed` (`guard-single-writer.ps1:24-28`);
- **path exemptions**, alongside the existing `Bridges/` one (`:34`): **`Fx/`** — `AuraPool.cs:80,117`,
  `BurstPool.cs:57` are VFX GameObjects — and **`Hud/`** — `ActorHudPool.cs:170,225,243` are HUD objects,
  **not `Plant`/`Zombie` transforms**. Each exemption carries a one-line comment saying which files it
  covers and why, in the same style as the existing allow-list comments.

Without the `Hud/` exemption the extended guard fails on the first run against a clean tree, which is how a
correct guard gets weakened in a hurry instead of scoped in advance.

## 3. What it must NOT do

- **Never add a second write path.** One entry point, one writer. A second is how the single-writer
  invariant dies.
- **Never write inside a Harmony hook.** Record only; the drain applies.
- **Never gate a movement action on the board.** With no lawn, the action is its `A-M1` payload and that is
  a complete action. In web battle the board is `A10`'s.
- **Never turn the destination into a delta.** A cell is not a magnitude.
- **Never widen the exemptions to silence the guard.** `Fx/` and `Hud/` are exempt because their objects are
  not actors; any new exemption is a reviewed change with the same justification written next to it.
- **Never read PvZ state to decide the move.** The overlay observes and contributes; the destination comes
  from the action, not from an inspection of the board's current arrangement.
- **Never run before E33.** Without `OnActivate` on the lawn nothing raises the edge that would call this,
  so shipping it earlier ships an untestable path behind a default-off flag.
- **Never move an actor the game considers dead**, and never move one mid-spawn — both are drop-and-count
  cases, not exceptions.

## 4. Testing strategy

1. **A stub that RAISES on the unexpected call.** The Core-side move queue is tested against a writer stub
   whose only behaviour is `throw` — so *"recording performs no write"* is proven rather than assumed, the
   same discipline the seedsmith transport stub encodes
   (`tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8`). Recording N moves must leave the throwing stub
   untouched; only `Tick` may reach it. **The model-transport rule is vacuous here** — this module has no
   model — and is not waived: any future tooling for it stubs its transport to raise.
2. **Determinism / replay.** The same recorded sequence drained twice → the same writer calls in the same
   order, asserted on a recording stub. Clamping is deterministic: the same out-of-board cell always yields
   the same clamped cell. A drain interrupted by the frame budget resumes at the next record and never
   re-applies one already applied.
3. **Planted violations**, each its own test:
   - a source file outside `EntityPositionWriter.cs` assigning `thePlantRow` → **`guard-single-writer.ps1`
     exits 1** and names the file (a `FusionRpg.Guard.Tests` case, so CI catches it, not a manual run);
   - the same for `transform.position =` outside the writer and outside `Fx/`/`Hud/`;
   - **an inverse test**: the guard run against the clean tree with `Fx/` and `Hud/` present exits 0 — this
     is the test that would have caught the ADR's original omission;
   - a move recorded inside a hook while the drain is disabled → nothing is written;
   - a ring overflow → the excess is dropped and counted, and no exception escapes;
   - a move to an out-of-board cell → clamped, not thrown;
   - a move to the cell the actor already occupies → the writer is not called at all;
   - **a move drained while `Mouse.Instance` is null → dropped and counted, and the actor's position
     is unchanged.** The test fails if the actor lands at `(col, row)` in world space — that is what
     `LawnCoords.cs:71`'s fallback would produce, and it is a teleport to near-origin.
4. **A blocked-dependency test.** While `OnActivate` is not raised, a test asserts the feature is
   default-off, so "unbuilt" and "shipped but inert" cannot be confused in a report.

## 5. Acceptance criteria

1. `EntityApply.MoveToCell` is the only public move entry point, and `EntityPositionWriter` is its only
   writer — asserted by the extended guard, not by review.
2. `scripts/guard-single-writer.ps1` carries the five new patterns, the `EntityPositionWriter.cs`
   allow-list entry, and **explicit `Fx/` and `Hud/` path exemptions each with a comment naming the files
   they cover**.
3. The extended guard exits 0 against the clean tree and exits 1 against each planted violation, both under
   `FusionRpg.Guard.Tests`.
4. `decisions.md:105`'s corrected enumeration and this spec agree; the row moves from **DRAFTED** to built
   only when criteria 1-3 hold.
5. Moves are recorded in the hook and applied only from `InjectorLoop.Tick`; no write occurs inside a
   Harmony hook, proven by the throwing-writer stub.
6. The feature is default-off with an env kill switch, and turning it off restores byte-identical behaviour.
7. Out-of-board destinations clamp; same-cell moves write nothing; dead or mid-spawn actors are dropped and
   counted; **a null or throwing `Mouse.Instance` drops and counts the move rather than writing
   `LawnCoords.CellCenter`'s `new Vector2(col, row)` fallback** (`LawnCoords.cs:71`), and
   `LawnCoords` itself is unchanged.
8. A movement action with an `A-M1` payload and **no** reposition still validates, compiles and runs — the
   standalone-first invariant, asserted as a test rather than as prose.
9. `guard-funnel-delta.ps1`, `guard-secondary-no-unity.ps1` and `guard-dal.ps1` stay green.
10. The drain's per-frame cost is measured with `PerfProbe` before the feature is enabled by default.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| **`OnActivate` raised on the lawn** | **effect-atom E33** | ⛔ **hard block** — absent from `EffectDtos.EffectTriggers`, raised nowhere (`effect-atom-map.md:337`; `action-corpus-map.md:136`) |
| The payload half | **A-M1** `movement-payload` | specced, unbuilt |
| `move.range` as a reader-backed channel | **A-M1 / effect-atom** | registered, **no production reader** (`DerivedStatRegistry.cs:237`) |
| Cell → world conversion | `LawnCoords` | built (`LawnCoords.cs:59-71`) — but its **null-`Mouse` fallback returns `new Vector2(col, row)`** (`:71`), a near-origin teleport if an actor write trusts it. §2 states the drop-and-count rule |
| **The production producer** | **a lawn-side `OnActivate` caller — NOT `A9`** | ⛔ **CORRECTED 2026-09-03.** This row said `A9 movement-actions`. `A9` is **battle-grid only** and cannot be this module's producer — see hazard 4 |
| `HasOnActivateGrant()` fast gate | **effect-atom E33** | does not exist; four `HasOn*Grant()` helpers, none for `OnActivate` (`EffectRuntime.cs:145-159`) |
| Record-then-drain host pattern | `EventDrainHost` | built (`EventDrainHost.cs:7-29`, `InjectorLoop.cs:79`) |

**Hazards.**

1. **The `Hud/` exemption is the known trap.** `decisions.md:105`'s original enumeration missed
   `ActorHudPool.cs:170,225,243`, and the correction is prose only. Extending the guard without writing the
   exemption produces an immediate red on a clean tree, and the natural reaction — loosening the pattern —
   would weaken the invariant the guard exists for.
2. **A fifth Unity write path is the first since the overlay principle was written** (`decisions.md:105`).
   Its narrowness is the entire safety argument; any widening reopens the ADR.
3. **Two guard tests are already red for an unrelated reason.** `Overlay_*` in `FusionRpg.Guard.Tests`
   reference `DamageFxOverlay.cs`/`OverlayWorldFx.cs` while the VFX migration deletes them — a pre-existing
   failure owned by the VFX stream. Do not read a red guard suite as evidence about this module.
4. **`A9` `movement-actions` and `A10` `battle-board` both depend on this row** (`decisions.md:105`), so a
   shortcut taken here propagates into two named-deferred modules.

   **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — A-M2 SHIPS KNOWINGLY INERT, and the
   map row reads `inert`, in that word. `A9` is NOT pulled in, because pulling it in would not help.**

   The choice was framed as *"ship inert, or pull `A9` in"*. Reading the action program settles it,
   and it settles it the other way from how this spec's own header read:

   - **`A9` is battle-grid only.** `action-map.md:294`: *"This map contradicted that in two places:
     `A8 defence-actions` and `A9 movement-actions` both referenced lawn geometry. **Corrected
     2026-08-22** — `A9` is battle-grid only, and the grid is owned by the new `A10 battle-board`."*
     A-M2 is the **lawn** half. So `A9` landing produces a producer for a different board, and this
     module would still have none. ⛔ This corrects the header block's *"A-M2 is downstream of
     both"* — it is downstream of E33, and of a lawn-side producer that `A9` is not.
   - **`A9` is not "in no plan".** It is `tasks/action-todo.md:1703`, under *Deferred — specced, not
     scheduled*: *"waits on `A10`. One row, no new runtime."* And `A10` is an **owner deferral**
     (`:1704`, *"built with the board map / battle area"*). Pulling `A9` in means pulling `A10` in,
     which is a battle-area decision far outside this module's scope.
   - **Shipping inert is the prescribed outcome, not an improvised one.** E33's own hazard table
     already says that if the producer does not follow in the same window, the map row must read
     **inert**, in that word (`spec-activation-edge.md:282`). Doing that is following a rule already
     written.
   - **This module was designed for exactly this state.** The reposition ships behind a **default-off
     toggle** *"so 'unbuilt' and 'shipped but inert' cannot be confused in a report"* (§4's own
     words). Inert is the state the toggle exists to name.

   **The producer is therefore a named, criteria-stated task that blocks no other module:** a
   lawn-side caller that raises `OnActivate` for an actor's movement action, so E33's seam has a
   production caller. **What a pass looks like:** on a live lawn, a movement action activated by a
   real actor produces one `OnActivate` grant, the reposition entry point receives one *move actor to
   cell* request, and the drain applies it inside the frame budget with `guard-single-writer.ps1`
   green. Until that exists, this module is **shipped, toggled off, and reported inert** — never
   *"built"*.

   **What would overturn it:** `A9`'s scope widening back to lawn geometry (which `action-map.md:294`
   corrected away on purpose), or a lawn-side producer landing in E33's own window.
5. **`LawnCoords.CellCenter`'s null-`Mouse` fallback is the quiet one.** It is not a crash, not a log
   line and not a clamp — it is a correct-looking `Vector2` that puts an actor near the origin, and
   the only place it becomes dangerous is the one write path this module adds (§2).
