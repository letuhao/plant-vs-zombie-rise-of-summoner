# Spec: Event Pipeline v2 — Phase 2 (record-then-drain)

Status: **draft — awaiting owner review**. Design SSOT (invariants, audit evidence, record
struct): [`event-pipeline-v2-ssot.md`](event-pipeline-v2-ssot.md). This spec does not repeat
the SSOT; it defines the deliverable, the contract, and how we know it's done.

## Objective

Make the injector's per-frame cost bounded by construction. Today every game event runs the
full effect pipeline synchronously inside a Harmony hook; event rate is unbounded (zombies ×
plants × fire rate × speed) and late waves measured ~75% of wall time in the pipeline. After
this change, hooks only record fixed-size structs; a single frame-budgeted drain processes
them with input coalescing. Player outcome: no mod-caused lag at 10× zombie density and max
game speed, on weak PCs, without requiring an fps cap.

User stories:
- A player on a weak PC runs a 200+ entity level at max speed; the mod adds ≤5% frame time.
- A player with on-hit effect builds sees identical proc rates and counter behavior (per-hit
  math preserved through coalescing).
- The owner runs LIVE prove packs inside a debug session with full event fidelity (budget
  bypass in sessions).

## Tech Stack

C# / .NET 6 (`FusionRpg.Core`, `FusionRpg.Injector`), BepInEx 6 IL2CPP interop, Harmony.
No new dependencies.

## Commands

```powershell
$env:FUSIONRPG_GAME_DIR = "H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL"
# Build injector (validation, no deploy):
dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release -p:GameDir=$env:FUSIONRPG_GAME_DIR -p:OutputPath=artifacts\perf-build\
# Tests (offline SSOT for all drain/coalesce semantics):
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
# Boundary guards:
.\scripts\guard-single-writer.ps1; .\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-funnel-delta.ps1;  .\scripts\guard-dal.ps1
# Deploy + live probe:
.\scripts\deploy-play.ps1
.\scripts\probe-perf.ps1 -Scenario <id> -DurationSec 90
```

## Project Structure (new/changed)

```
src/FusionRpg.Core/Events/GameEventRec.cs        → typed record struct + kind enum (SSOT §4b.6)
src/FusionRpg.Core/Events/GameEventRing.cs       → fixed-capacity ring, single-writer append
src/FusionRpg.Core/Events/EventCoalescer.cs      → coalescing key + HitCount merge (SSOT §4b.2)
src/FusionRpg.Core/Events/EventDrain.cs          → budgeted FIFO drain, cost classes, barriers
src/FusionRpg.Injector/Effects/EventDrainHost.cs → injector wiring: drain in InjectorLoop.Tick,
                                                   record sites in hooks, session bypass
tests/FusionRpg.Core.Tests/Events/               → drain/coalescer/ordering/proc-math tests
docs/architecture/event-pipeline-v2-ssot.md      → updated as decisions land
docs/architecture/decisions.md                   → new row (owner writes/commits)
```

Core stays Unity-free (guard-enforced); ring/coalescer/drain logic is pure C# and fully
testable offline. Injector owns record sites and the drain host.

## Code Style

Match existing Core style. The record is a struct of values, never object references:

```csharp
/// <summary>One recorded game event — values only; IL2CPP refs are use-after-free at drain time.</summary>
public readonly struct GameEventRec
{
    public readonly GameEventKind Kind;
    public readonly int Frame;
    public readonly long Seq;
    public readonly IntPtr ActorPtr;
    public readonly IntPtr TargetPtr;
    public readonly int TypeId;
    public readonly int TargetTypeId;
    public readonly byte Side;
    public readonly long Amount;      // summed when coalesced
    public readonly short HitCount;   // proc math multiplier
    public readonly byte ChainDepth;  // >0 → never coalesce
    public readonly int SourceGrantIdx; // interned; -1 = none
    public readonly int MatchKeyIdx;    // interned at record time (never drain time)
    public readonly int PairId;         // dealt/taken causal pairing
}
```

## Testing Strategy

xUnit in `tests/FusionRpg.Core.Tests/Events/`. All semantics proved offline before any live
deploy — same doctrine as the combat matrix ("complex damage math SSOT is offline").

Required test groups (each maps to an SSOT invariant):
1. **Ordering**: FIFO within ptr; `board.start` before entity records; death record drains
   before its grant-withdraw; lifecycle barriers force drain-to-empty.
2. **Coalescing**: key correctness; chain/`SourceGrantId` records never merge; die/board/match
   never merge; `Amount`/`HitCount` accumulation.
3. **Proc math with HitCount**: EveryHits counters advance by N; one burst per threshold
   crossing; `chance` rolls per hit; `max_stacks` consumes per hit.
4. **Budget**: drain stops at budget, carries over, never drops non-droppable kinds; expensive
   cost classes capped 1/frame; generation cap on chain records.
5. **Re-entrancy**: records appended during drain are processed same-frame under budget,
   inherit depth+1; recorder append is safe from inside the drain.
6. **Session bypass**: debug session drains unbudgeted with no coalescing (prove-pack fidelity).

Coverage bar: every invariant in SSOT §4/§4b/§4c has at least one test naming it in a comment.
Existing 733 Core tests must stay green (EffectBag semantics unchanged downstream of the drain).

## Boundaries

- **Always:** run the 4 boundary guards + Core/Guard test suites before finishing; keep Core
  Unity-free; stamp `MatchKey` at record time; keep per-instance XP events
  (`zombie.die`, `plant.place`, `zombie.spawn`, `mower.start`) uncoalesced and undropped;
  update the SSOT when a decision changes.
- **Ask first:** any player-visible balance change beyond the two already assumed (chain-depth
  tightening, per-hit proc rolls); changing the budget default; adding event kinds to the
  droppable list; anything touching `decisions.md` (owner commits it).
- **Never:** git write commands (owner commits); patch the game binary; ad-hoc stat writes
  outside EntityStatWriter/Funnel; drop or reorder lifecycle/death/XP events; vendor names in
  tree.

## Success Criteria

1. Stress scenario (heaviest available board, max speed, effect grants active):
   injector share ≤ **5% of frame time**; no mod-attributable frame > 30 ms; gen2 GC = 0.
2. `effect.onEvent` + drain sections combined ≤ 2 ms per 5s window per 100 events/s.
3. Effects land ≤ 3 frames after their triggering hit (drain carry-over bounded).
4. Offline: all test groups above green; 733 existing Core tests green; guards green.
5. LIVE checklist F-rows still pass inside a debug session (fidelity bypass works).
6. Probe shows event-record rate ≈ game event rate, but pipeline executions ≈ coalesced rate.

## Resolved decisions (owner, 2026-08-21)

1. **Chain depth**: drain-generated records inherit `ChainDepth+1`; the depth mechanism is
   **hard-coded** (cannot be disabled — infinite spawn loops must be impossible), the limit
   value is configurable but clamped to a sane range (1..8, default 6).
2. **Proc math**: hybrid statistical merge. Counters/stacks accumulate deterministically by
   `HitCount`; chance procs use a **single roll against `1−(1−p)^n`** — exactly equivalent in
   distribution to "at least one of n per-hit rolls" (today's semantics fire at most one proc
   per event per grant), O(1) per merged record, no per-hit loop.
3. **Budget**: adaptive — **10% of the frame budget** per frame for the drain; and the game
   ships with a **default 60 fps cap** (`FUSIONRPG_FPS_CAP` default 60, player-configurable),
   giving a ~1.66 ms drain budget and headroom for future features. Uncapped remains available
   by config.
4. `decisions.md` row: owner writes and commits after this spec review (repo rule).
