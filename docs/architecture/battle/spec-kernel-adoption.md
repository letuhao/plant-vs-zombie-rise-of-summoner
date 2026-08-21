# Spec: kernel-adoption

Module id `kernel-adoption` (T5) in the [battle timeline map](../battle-timeline-map.md). Depends on `mode-profiles` (and transitively the whole kernel). **This is the program's gate — Checkpoint B.**

## Objective

`BattleEngine` gives up its own round loop and runs on the kernel under `classic-round`, producing **byte-identical** reports. Per the owner pick: no golden re-bless, no `RulesetVersion` bump, expedition economy untouched. Any drift is a bug, not a judgment call.

This module adds no features. Its entire value is proving that the kernel can carry the real engine — which is why it must land before any new mode, and before the enrichment waves are rebased onto the timeline.

## Design (locked on approval)

### What must not change

| Surface | Contract |
|---|---|
| Report bytes | All four battle goldens identical, hashes untouched |
| Expedition plans | All four expedition goldens identical |
| Ruleset | `RulesetVersion` stays **2**; the SSOT ban test stays armed and green |
| RNG | Every stream's draw **count and order** unchanged |
| Events | `report.Events` identical in content *and* sequence |
| Apply path | Every HP delta still resolver → `DamageApplyPipeline` → shield gate; funnel guard green |
| **Serialization shape** | **No field may be added to `BattleSetup`/`BattleActorSetup` or `BattleReport`/`BattleActorResult`** |

**That last row is a gate-killer the original spec missed.** The two golden families have *opposite* sensitivities: `ExpeditionBattlePlan` embeds the full `BattleSetup`, and the expedition goldens hash the serialized resolution with default options (`DefaultIgnoreCondition = Never`), so **any** new property serializes on every actor in every plan — even at its default value — and moves all four expedition hashes. The precedent is recorded in the test file itself: adding `InnateShield` did exactly this. Meanwhile `BattleReport` does not contain the setup, so report fields move the four *battle* hashes instead.

**Therefore the mode profile is a parameter, never a field.** And it must arrive as an **overload**: `Resolve(setup, seed)` survives verbatim (defaulting to `classic-round`) because there are **53 call sites across 11 files**, and changing the signature would silently violate this module's "no test edits" criterion. `Resolve(setup, seed, profile)` is new.

### The round skeleton, made explicit

Today's ordering is implicit in statement order inside one 350-line method. Adoption turns it into scheduled events at fixed intra-round offsets. The sequence is transcribed from the current engine and must be preserved exactly:

1. round opens; clock at `t0 + round × 1000`
2. regenerator pulses
3. status ticks (DoT/regen pulses through the pipeline)
4. funnel flush
5. post-flush: immortal revive → death sweep → retreat check
6. early exit if either side is no longer active
7. initiative order computed — **one `NextInt(1000)` draw per active actor**, minus swift bonus, stable-sorted
8. per attacker: CC skip → target select → resolver → berserker → essence riders → guardian split → apply → flush → tallies → revive → kill/death → soul-eater → retreat check
9. death cleanup (status withdraw + shield `RemoveAll`)
10. shield upkeep **after** dispatch, then drain shield events

Making this declared rather than emergent is the one genuine improvement adoption delivers: today a careless statement reorder silently changes the game, and only a golden hash would catch it — with no indication of what moved.

### The seven byte-identity hazards

Named, because these are where drift will actually come from. The first three were in the original spec; the review found four more, **each of which alone would move a golden**:

1. **Initiative draw order.** `OrderBy`'s key selector runs once per element in *source* order, so today's draw sequence is "actors list order, filtered to active." The kernel must consume the `initiative` stream in exactly that order and count. Reordering the actor collection — even to something more "natural" — changes every battle.
2. **Active-set filtering timing.** Step 7 filters on `Active` at that instant. An actor that died in step 5 draws no initiative. Filtering a moment earlier or later shifts every subsequent draw.
3. **Iteration order in target selection.** `SelectTarget` and adjacency scans walk the actors list in order and take the first match. Any set or dictionary substituted for that list changes targeting.
4. **CC-locked actors still draw initiative.** The seating filter is `Active` (alive and not retreated) — **CC is not part of it**. A stunned actor draws its `NextInt(1000)` and is *then* skipped. An FSM that suspends before seating removes that draw and desyncs every subsequent round in the battle from a single stun.
5. **Status pulse under-delivery must be preserved.** `TickBudget` defaults to 1 and `Tick` fires at most one pulse per call, while the clock jumps a whole round — so a `PeriodMs=250, DurationMs=4000` status delivers **4 pulses where the true schedule has 16**. A competent kernel scheduling pulses at their real times delivers 16, which is *correct* and **fails the gate**. `classic-round` must pump `StatusRuntime.Tick` exactly once per 1000-tick round with budget 1. Fixing this is T9's job, deliberately and with a version bump — not a side effect here. **No test covers this today** (every existing caller uses `PeriodMs: 1000`, where one-per-round is accidentally right), so a sub-round fixture lands *before* adoption.
6. **The funnel window count is semantic, not cosmetic.** The HP clamp is applied **per FA10, not per delta**, and the funnel merges everything for one actor into one slot per window. So a round-open window that nets regen against DoT before clamping gives materially different results from two windows: an actor at 1 HP with −5 DoT and +10 regen **survives at 6** merged, and **dies** split (death recorded, shields removed, die event emitted). Two more window-sensitive behaviors: a net-zero merged window emits **no FA10 at all**, and `EffectId`/`GrantId` attribution belongs to whichever call enqueued *first*. The specs already said "one funnel window" — what was missing was why, which is why it is now a Never.
7. **The early exit cancels shield upkeep.** When a battle ends on status damage, that round's shield upkeep and event drain never run. Scheduled upkeep events at fixed intra-round offsets *will* fire unless the exit explicitly cancels them.

### A ladder of parity tests, not just a hash

A golden hash failing says "something changed" and nothing else, which makes it a terrible debugging instrument. Adoption ships a ladder that fails **earlier and more specifically**, ordered cheapest-to-diagnose first:

1. **Stream parity** — per-stream draw **values in sequence** match a recorded pre-adoption trace. **Values, not counts** (audit): `SeededRng.NextUInt` uses rejection sampling, so one logical draw consumes a variable number of generator steps and a count assertion over the underlying generator is meaningless. The `crit` stream is also data-dependent twice over — gated on the hit landing, and short-circuited entirely when `pHit` is 0 or 1 — so a trace assuming two draws per attack produces false failures. The `status` and `proc` streams draw **zero** times in battle (spread requires a non-null board; procs only roll on the Grant path), which the trace must assert rather than assume.
   **This needs a seam that does not exist:** `SeededRng` is `sealed`, has no counter and no interface, and the engine constructs its streams internally, so today's tests can only re-derive a stream and replay draws by hand — which does not scale past a 1v1. Fix: an `internal` draw log on `SeededRng`. Pure observation, no algorithm change, so **`RngAlgoVersion` stays 1** — worth stating so nobody reads it as an algorithm bump.
2. **Phase-order parity** — the intra-round sequence above, asserted as an observed event log
3. **Per-round state parity** — every actor's HP, shield, and status set at each round boundary
4. **Event-sequence parity** — `report.Events` element-wise
5. **Golden hash** — the final gate

The pre-adoption traces are captured **before** any engine change and committed as fixtures. This is the same discipline that made the U14 re-bless honest: predict the delta, then verify it.

### No feature flag

Deliberately no `FUSIONRPG_BATTLE_KERNEL=0` escape hatch. A second live code path for the same battle would double the surface, and the goldens would only ever cover whichever path the tests ran. The goldens **are** the safety net: if they drift, we do not ship, we fix.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Battle/BattleEngine.cs            (round loop → kernel drive; state moves to a run-state object)
src/FusionRpg.Core/Battle/Timeline/BattleRunState.cs (actors, byKey, shields, host, scheduler — extracted from the method's closures)
tests/FusionRpg.Core.Tests/Battle/Adoption/          (the parity ladder + captured pre-adoption fixtures)
```

Note the secondary win: `Resolve` currently holds ~350 lines and eight closures over shared mutable locals. Extracting `BattleRunState` is required by adoption anyway — the kernel needs a state object to call back into — and it retires that structural finding as a side effect rather than as a separate refactor.

## Testing strategy

The ladder above, plus the whole existing suite unchanged across **six** test projects — Core, Data, Guard, CheatCore, Launcher, and **E2E** (131 tests, calls `BattleEngine.Resolve` twice; previously omitted from verification sweeps) — and all four boundary guards. The nine battle-shield E2E tests and the determinism replay must pass **without modification** — if a test needs editing to accommodate adoption, that is drift wearing a disguise, and the edit is the finding.

Plus two fixtures that must exist *before* adoption because nothing covers them today: a **sub-round `PeriodMs`** case (hazard 5) and a **mid-battle summon** case — a summon appended mid-round changes the initiative draw count for that round and every round after, so without a fixture the ladder goes green and the first enrichment wave that summons anything breaks byte-identity with nothing pointing at why.

## Boundaries

- **Always:** byte-identical output; the existing test suite passes unedited; every HP delta still through the pipeline.
- **Ask first:** anything that would move a golden. There is no "small, justified" drift at this gate — a re-bless here costs a win-rate sweep and owner sign-off.
- **Never:** a feature flag creating a second live battle path; changing actor collection ordering; a behavior "improvement" smuggled in with the refactor. Improvements land as their own change, after the gate, with their own predicted delta.

## Success criteria

1. All eight goldens unchanged; `RulesetVersion` still 2. 2. Every suite and guard green with no test edits. 3. The parity ladder localizes any drift to a stream, a phase, or a round — never just "the hash moved." 4. `BattleEngine` no longer owns a loop, and the round skeleton is a declared, tested sequence.
