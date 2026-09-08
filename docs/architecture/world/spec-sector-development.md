# Spec: sector-development (wave 3)

**Status:** Draft — pending owner review. Module id `sector-development` in the [world map program](../world-map-program.md). Depends on `world-movement`, and lands on top of everything the [loam program](../loam-map.md) shipped into the same phases.

**Sequencing inverted 2026-09-03** ([world-stage-ideal.md](../world-stage-ideal.md) §8d.1): recruitment stopped being a later nicety and became a **prerequisite of `world-stage`**. The stage's outliner, unresolved count, cycle-to-next, notification tiers and force-end hatch are all sized against 6–10 legions (§8e.3), and today the player commands exactly one. §8e.4 then dissolved the ordering question — both programs get specced, in either order.

## Objective

Three things the map already promises and none of them delivers: **an army that grows**, **a year that changes**, and **ground that is worth improving.**

Success looks like: a forty-turn run from `first-light` ends with the player commanding several legions they chose to raise rather than one the template handed them; the HUD's season slot reads a season that actually moved a number; and a sector the player spent turns developing earns more than it costs — measurably more, not by assertion.

## What already exists — verified, with lines

The gap here is smaller than "wave 3, not started" suggests. Almost every substrate is shipped and inert.

| Needed | Status |
|---|---|
| A phase to grow in | `Growth` — `report.BeginPhase(Phases.Growth); return world;` **in full**, `TurnEngine.cs:196-200` |
| A deterministic clock with boundaries | `TurnCalendar.Roll(turn, seed)`, `TurnCalendar.cs:31-58` — per-boundary derived streams (`:41`, `:48`), every rate tunable (`:27-29`) |
| The deferral, stated in code | *"Wave 1 records the rolls; the economic effects land with sector-development, which is the module that owns growth"* — `TurnCalendar.cs:6-7`; and `TurnEngine.cs:220` |
| The recruit-pulse intent, stated in code | *"recruits arrive in pulses"* — `TurnCalendar.cs:14` |
| A slot that produces recruits | `SlotKind.Lair`, `Yields = true` — `SlotTypeCatalog.cs:70` |
| A structure substrate | `StructureCatalog`, 4 rows / 2 kinds — `StructureCatalog.cs:63-108`; `WorldSlot.StructureId` + `ConstructionTurnsRemaining` — `WorldState.cs:105-116` |
| A build verb, fully wired | `BuildResolver.Run` — `BuildResolver.cs:21`, called at `TurnEngine.cs:280` |
| Catalog validation for it | `Rule14StructureSlotKindMatches` — `WorldValidation.cs:38, 359` |
| A development term in upkeep | `LoamPolicy.DevelopmentAndDangerUpkeep` — `LoamPolicy.cs:52-53`, read by `LoamUpkeep.cs:44` |
| Runtime entity creation, deterministically | `LoamPhases.SpawnTheUnmade` — `LoamPhases.cs:246-257`: a `WorldEntity` built inside a phase, id derived from the sector, no RNG |
| A per-component pool to spend from | `TerritoryComponents.For` — `TerritoryComponents.cs:17` |
| A purity guard that catches a wall clock | `WorldDeterminismGuardTests` — `tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs:16-47` (banned symbols) and `:51` (no floats in world state) |

**Genuinely missing: the growth phase's contents, a season term, a project concept, and content.**

Two facts that size the work:

- **The player commands exactly one legion and cannot gain another.** Every `WorldEntityKind.Legion` in `src/` is `e-dave-legion-1` — `WorldTemplateCatalog.cs:184` and `WorldTemplateCatalog.TwoHearths.cs:280`, one per template. The whole world holds three mobile entities on `first-light` (`:184`, `:210`, `:220`) and two on `two-hearths` (`:280`, `:294`).
- **`DevelopmentLevel` is a cost with no producer.** It is stored (`WorldState.cs:135`), hashed (`WorldCanonical.cs:35`), projected (`WorldDtos.cs:74`), believed (`FactionIntel.cs:96`) and charged for (`LoamUpkeep.cs:44`) — and **nothing in `src/` ever raises it.** That is exactly the trap [empire-economy-ssot.md](../empire-economy-ssot.md) A8 named: development priced as pure cost quietly kills the builder layer.

## Design

### 1 · Recruitment

#### Why lair-gating does not reach the target on the shipped maps

The obvious design — *lairs release recruits weekly* ([world-graph-ideal.md](../world-graph-ideal.md):164, :322, :384) — cannot produce 6–10 legions by turn 40 on either map that exists. `first-light` has **one** lair slot, and it is guarded (`WorldTemplateCatalog.cs:119`, `GuardState.Intact`); `two-hearths` also has exactly one. A single source, behind a fight, is not a rate that reaches several legions in forty turns. This is the same shape as the loam program's W37 finding — *`first-light` will under-exercise the AI, and it did*.

**So recruitment is seated, not lair-gated:** every held sector with a Seat slot contributes a base pulse, and a cleared lair **multiplies** its sector's pulse. That reuses the exact shape `loam-structures` already ships — a rootbed seeps on its own and a well multiplies it (`StructureDef.YieldMultiplierMilli`, `StructureCatalog.cs:29-32`) — rather than inventing a second economy idiom. Lairs stay worth fighting for; they stop being the only tap.

Seats are dense enough for this to bite: 5 in `first-light`, 9 in `two-hearths`.

#### The pulse

In `Growth`, on a week boundary only (`TurnCalendar.Roll(turn, seed).WeekBoundary`), each held sector accrues recruit points into `WorldSector.RecruitStock`. **Weekly, not per turn**, because the boundary is what gives the campaign a heartbeat — *"hold this lair for three more weeks"* is a plan; a trickle is not. A special week (`CalendarRoll.SpecialWeek`) scales the pulse; a plague month suppresses it, on the same *"the plague wins"* rule `TurnCalendar.cs:52-54` already applies to growth.

Recruit stock is a **stock, not a rate** — a plain `long` count, per the same reasoning `WorldSector.LoamStock`'s own comment gives (`WorldState.cs:137-144`): per-mille means rate or fraction, and a stockpile is neither.

#### Raising a legion

A pulse never spawns a legion by itself. A new `raise` command spends a sector's recruit stock and founds a legion at that sector's Seat:

| Command | Payload | Legality at resolution |
|---|---|---|
| `raise` | `SectorId` | sector is yours **at Snapshot**, has a Seat slot, no hostile entity stands in it, `RecruitStock >= RaiseCostPoints` |

It resolves in `Snapshot`, immediately after `BuildResolver` (`TurnEngine.cs:280`) and for the identical reason that resolver states (`BuildResolver.cs:14-17`): ownership is only decided once the rest of the turn has run, so the order re-validates at resolution rather than trusting Reveal-time admission.

The new entity follows `SpawnTheUnmade` exactly (`LoamPhases.cs:246-257`) — a pure constructor, no RNG, and an id **derived from its cause**: `e-{factionId}-legion-{turn}-{sectorId}`. That is unique by construction, because a raise consumes the sector's stock and so at most one can succeed per sector per turn. A monotonic counter would be a hidden piece of state a replay has to reproduce; a derived id is not.

Members come from the roster the same way template legions do — `WorldEntityMember` with a `SpeciesId` and, per `WorldState.cs:214`, an `InstanceId` when the specimen is a real `rpg_unique_actors` row. **Which species a sector recruits is the sector's climate**, per [world-graph-ideal.md](../world-graph-ideal.md):488 — no new selection mechanism.

#### Why a rate, and never a cap

The 6–10-by-turn-40 target (§8e.3) is a **calibration target read by the acceptance harness, not a limit read by the engine.** A legion count the engine enforces would be a hard progression ceiling, which AGENTS.md forbids outright. The lever is the pulse rate and the raise cost; the target is what the harness tunes them against — the same discipline `LoamPolicy`'s own header already states (`LoamPolicy.cs:5-9`: L9's harness picks the numbers, the policy file only gives it something to run against).

`RecruitStock` likewise carries no hard cap. If accrual needs throttling, it gets a **configurable soft cap** in tuning, declared in [ssot-power-scale.md](../power/ssot-power-scale.md) §11's register like every other one.

### 2 · Seasons — the calendar's economic half

#### A season is derived, not rolled

The clock is already built, deterministic and replayable (`TurnCalendar.cs:31-58`). A season adds **no RNG and no state**: it is a pure function of the turn.

```
season(turn) = (turn / (DaysPerWeek * WeeksPerMonth * MonthsPerSeason)) % SeasonCount
```

`CalendarRoll` (`TurnCalendar.cs:9-10`) gains a `Season` member. Everything else about the roll is untouched, which matters: the derived streams `calendar:week:<turn>` and `calendar:month:<turn>` (`:41`, `:48`) exist so that an extra draw in one never shifts the other, and a season that draws nothing cannot disturb either.

**A season is never fogged.** Nobody is uncertain about what month it is, so belief computes it from the turn like everything else does — the same argument `LoamUpkeep.cs:33-39` already makes for the terms it calls "terrain or self-knowledge".

#### What a season changes

Endless Legend's precedent — the one the owner named — is that seasons change **movement and yields**. This codebase has exactly three seams where that is a one-line multiplier and nothing else:

| Seam | Where | Shape |
|---|---|---|
| Yield | `LoamProduction.For` (read at `LoamPhases.cs:30`) | per-mille scale on production |
| Upkeep | `LoamUpkeep.For` — `LoamUpkeep.cs:40-47` | a fourth per-mille factor |
| Movement | `MovementPolicy.BudgetFor` — `LaneCost.cs:38-43` | per-mille scale on the turn's budget |

Which of the three a season touches, and how hard, is balance-bearing and stays **open** (below). What is *not* open is the arithmetic: every one of them is already per-mille, so a season is a multiplier table in tuning, never a code path per season. Content is data (locked shape rule 5).

#### The upkeep term costs a ruleset bump — priced up front

`LoamUpkeep.For` has **no calendar term today** (`LoamUpkeep.cs:40-47`). Adding one changes upkeep for every sector every turn, which is a hashed-behaviour change: `TurnEngine.RulesetVersion` (`TurnEngine.cs:42`, currently **5**) goes to 6, with a golden re-bless. That is the path [decisions.md](../decisions.md) records for every prior one — `Intel`'s move (2→3), `loam-turn` waking two phases (3→4), `LegionSupply` replacing attrition (4→5).

Two traps, both real:

- **The belief side must get the same term.** `LoamUpkeep.For`'s five-argument overload has four other callers, one of which is the AI's belief path (`FrontierRulesPolicy.cs:189`) and one the player-facing forecast (`LoamForecast.cs:60`). Omit the season from either and the AI plans against an upkeep it does not pay, or the forecast disagrees with the act — the precise failure §8c.6 lists as load-bearing about `Weakest`.
- **The product gets a fourth per-mille factor.** `sum * intensityMilli * handicapMilli / 1_000_000` becomes a four-factor product over `1_000_000_000`. `sum` is already `long` so the chain promotes (`LoamUpkeep.cs:42`), and the divide still happens **exactly once, last**. Per AGENTS.md's overflow rule the product goes in a `checked` block: an overflow here must throw, not wrap into negative upkeep — the defect `WorldState.cs:137-144` records having already happened once with `int`.

Season and plague compose **multiplicatively on the pre-clamp input**, matching `LoamPolicy.SurgeDecayMultiplierMilli`'s own audit-resolved rule (`LoamPolicy.cs:143-148`): a surge pushes more sectors toward `MaxDecayMilli`, never past it.

### 3 · Slot buildings, sector projects, production and upkeep

#### Structures need content, not mechanism

The mechanism is done and wired (see the table above). What is missing is rows and kinds. `StructureKind` has two values and its own comment says the rest belong here: *"more kinds belong to whatever `sector-development` eventually adds, not invented here on spec"* (`docs/architecture/loam/spec-structure-substrate.md:38-39`). **Corrected 2026-09-03 by audit: `StructureCatalog.cs` does not mirror that sentence** — the string `sector-development` appears nowhere in it, and its own comments (`:4-5`, `:44-46`) defer to **`loam-structures`**, which shipped Well and Waystation. The deferral to this module is the substrate spec's, not the catalog's.

This module adds the **yield kinds** the reward layer needs — the soul conduit, extractors, a hatchery on a lair — as catalog rows against a new `StructureKind`, plus the flat structure-only yield field `spec-structure-substrate.md` explicitly deferred to *"a new field added when there is a real row to test it against."* This is that row.

**Naming defect, fixed 2026-09-05 (world-map W57):** every `*CostMilli` in `data/tuning/loam.v{1..3}.json`'s `structures` block (`wellCostMilli` 200, `waystationCostMilli` 300, `granaryCostMilli` 150, plus `soulConduitCostMilli`/`extractorCostMilli`/`hatcheryCostMilli` added by W56 with the identical defect) was a **whole loam unit**, not a per-mille — `StructureDef.CostMilli` was compared directly against `WorldEntity.CarriedLoam` at `BuildResolver.cs:101`, and that is a plain count. The maths was right and the name lied. Renamed via `publish.py loam --rename-key` (`loam.v3.json` → `loam.v4.json`, no value changed) plus a loader edit — `StructureDef.Cost`, `LoamPolicy.WellCost`/`WaystationCost`/`GranaryCost`/`SoulConduitCost`/`ExtractorCost`/`HatcheryCost`, tuning keys `wellCost`/`waystationCost`/`granaryCost`/`soulConduitCost`/`extractorCost`/`hatcheryCost`.

#### Projects raise the sector; buildings raise the slot

The ideal draws the line cleanly ([world-graph-ideal.md](../world-graph-ideal.md):371-372) — slot buildings develop one slot's output; sector projects raise the whole sector: development level, defense, capacity. It also prices them: *"a project is 'this sector is doing this for the next three turns'"* (:374), costing turns and materials, never a hidden industry stat.

So: a `ProjectCatalog` mirroring `StructureCatalog`'s shape exactly (dictionary-backed, eager `Validate()`, `IsKnown`/`Get` — `StructureCatalog.cs:48-140` is the template), new sector state `ProjectId` + `ProjectTurnsRemaining` mirroring `WorldSlot.StructureId` + `ConstructionTurnsRemaining`, and a `develop` command resolving in `Snapshot` beside `raise` and `build`, for the same ownership-race reason.

**Projects advance in `Growth`; structures keep advancing in `Production`** (`LoamPhases.DecrementConstruction`, called at `LoamPhases.cs:28`). The split is deliberate: `Production` counts down a structure, which is `loam-structures`' behaviour, and reusing its loop would make one phase serve two modules and put a second module's fingerprints on a shipped ruleset. The consequence is a stated, testable rule: **`Growth` runs after `Production`, so a project that completes this turn affects next turn's yield, never this turn's.**

Whether projects genuinely need a second catalog rather than a scope field on the first one is **open** (below).

#### What development has to be worth — A8, as a test

Binding constraint, [empire-economy-ssot.md](../empire-economy-ssot.md):318: **development must raise yield faster than it raises upkeep, or nobody will ever develop.** Today only the upkeep half exists (`LoamPolicy.cs:52-53`).

That is an invariant, not a comment. The yield-per-level rows live in `data/tuning/loam.v{n}.json` under a new `development` block — **in the same file as `upkeep.developmentUpkeepPerLevel`**, because the invariant is a comparison between two numbers and splitting them across files makes it unverifiable by reading. The file's `_meta.owner` gains this spec as a second owner.

A test asserts the invariant over the whole authored level range, not at one sample point.

#### `SectorPhase.Developed` stays unused, and should be deleted

`SectorPhase.Developed` is declared (`WorldState.cs:12`) and referenced **nowhere in `src/`** — verified. This module does not make it real: development level is the number, and a phase mirroring it is derived state that rots, which `spec-world-movement.md` already forbids ("no storage of anything recomputable"). Recommend deleting the enum value in a separate change. Removing an unused member is safe here, and verifiably so: `SectorPhase` is persisted and read back **by name**, never by ordinal (`RpgStore.World.cs:230` writes `s.Phase.ToString()`, `:429` reads `Enum.Parse<SectorPhase>`), and `WorldCanonical.Row` hashes the same string form (`WorldCanonical.cs:95-104`) — the identical property `SlotTypeCatalog.cs:25-28` relies on for `SlotKind`.

### Commands added — and the plumbing defect this module inherits

| Command | Payload | Resolves |
|---|---|---|
| `raise` | `SectorId` | `Snapshot` |
| `develop` | `SectorId`, `ProjectId` | `Snapshot` |

**A new command kind must be plumbed through five sites or it is silently lost.** `spec-world-movement.md` records `stance` shipping as a dead letter for exactly this reason, and `RpgStore.WorldTurns.cs:437-441` carries the warning in its own doc comment.

**The same defect is live right now for two shipped kinds, and this module must not build on top of it:**

| Site | `sustain` (`Amount`) | `build` (`StructureId`) |
|---|---|---|
| `WorldCommandKinds.All` — `WorldCommand.cs:36-37` | present | present |
| `WorldCommand` field — `WorldCommand.cs:76`, `:79` | present | present |
| `RpgStore.CommandPayload` — `RpgStore.WorldTurns.cs:442-444` | **missing** | **missing** |
| `WorldCommandRequest` — `WorldDtos.cs:205-217` | **missing** | **missing** |
| Submit mapping — `WorldEndpoints.cs:72-81` | **missing** | **missing** |

So a `sustain` loses its `Amount` and a `build` loses its `StructureId` on the store round trip (`RpgStore.WorldTurns.cs:168-169` writes them out; `:652-662` and `:699-709` read them back), and neither field is reachable from the wire at all. `SustainResolver` and `BuildResolver` are correct; nothing can feed them a complete order except an in-process test. **Fixing those three sites is a prerequisite of this module**, not a bonus — and the durable fix is the round-trip test named under Testing, so the sixth kind cannot repeat it.

### Determinism

Everything here is pure over `(turn, seed)` like `TurnCalendar` already is. New RNG, if any is needed at all, comes from streams derived as `growth:recruit:<turn>` — one stream per concern, following `TurnCalendar.cs:41`'s convention precisely so an extra draw in one never shifts another. No wall clock; `WorldDeterminismGuardTests` scans the world source tree for the banned symbols and picks up new files automatically.

Stable ordering by sector id and entity id everywhere; never dictionary enumeration.

### Numbers — all of them tunable

| Block | File | Rows |
|---|---|---|
| `growth` | `data/tuning/world.v{n}.json` | seat pulse per week, lair multiplier per-mille, special-week multiplier, raise cost, recruit soft cap (if one is needed) |
| `growth.legionTarget` | same | `min: 6`, `max: 10`, `byTurn: 40` — read by the harness, never by the engine |
| `seasons` | same | `count`, `monthsPerSeason`, and per-season yield / upkeep / movement multipliers, per-mille |
| `development` | `data/tuning/loam.v{n}.json` | yield per level, project costs and turns — beside `upkeep.developmentUpkeepPerLevel`, so A8 is readable in one file |

Seasons live beside `calendar` in `world.v{n}.json` because a season **is** the calendar; the upkeep multiplier being applied inside `LoamUpkeep` is the same arrangement `WorldFaction.UpkeepHandicapMilli` already has — a property owned elsewhere, applied there.

Neither file is hand-edited: `python tools/tuning/publish.py world <dotted.key>=<value>` writes `world.v{n+1}.json` and leaves the old version on disk as the revert. **Both hosts pin the filename explicitly** (`src/FusionRpg.Server/Program.cs:36-38`, `src/FusionRpg.Injector/Host/RpgHost.cs:64-66`), so a version bump is two host edits, exactly as the power dial's was.

### Persistence, hashing, and the wire

New hashed state: `WorldSector.RecruitStock` (`long`), `ProjectId` (`string?`), `ProjectTurnsRemaining` (`int?`). `WorldCanonical.Write` hashes sector rows field by field (`WorldCanonical.cs:34-37`), so each one moves every world golden.

**Two golden moves, budgeted here rather than discovered per task** — the loam program's audit caught five specs each independently reopening a budget its plan had already closed:

1. **All new fields land together in one batched re-bless**, `RulesetVersion` unchanged — the L25 precedent recorded in [decisions.md](../decisions.md).
2. **Wiring `Growth` and the season upkeep term bumps `RulesetVersion` 5 → 6**, with the second and last re-bless.

Belief: `RememberedSector` gains the same fields under the rule already in force — full detail at `SectorSight.Full` only (`IntelRecorder.cs:100`, `IntelSeed.cs:81`), and never fogged for your own ground (`world-map-program.md:46`). The DTO follows `WorldSectorDto`'s existing owner-only convention for economy numbers (`WorldDtos.cs:88-102`): recruit stock and project progress are owner-only; a sector's development level already is not, and stays as it is.

No new tables. Commands ride `rpg_world_commands` once `CommandPayload` is fixed.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # growth phase, recruit math, season derivation, A8 invariant
dotnet test tests\FusionRpg.Data.Tests    # command payload round trip for every kind
dotnet test tests\FusionRpg.Guard.Tests   # determinism scan picks up the new files
.\scripts\guard-dal.ps1
```

The forty-turn acceptance run lives with the other world checkpoints in `tests\FusionRpg.E2E.Tests`.

## Structure

```
src/FusionRpg.Core/World/Growth/    → GrowthPhases.cs, RecruitPolicy.cs, RaiseResolver.cs,
                                      ProjectCatalog.cs, DevelopResolver.cs, DevelopmentYield.cs
src/FusionRpg.Core/World/Turn/      → TurnCalendar.cs (Season), TurnEngine.cs (Growth wiring only)
src/FusionRpg.Core/World/Loam/      → LoamUpkeep.cs, LoamProduction.cs (season term)
src/FusionRpg.Data/Sqlite/          → RpgStore.WorldTurns.cs (CommandPayload fields)
src/FusionRpg.Contracts/            → WorldDtos.cs (request + sector fields)
src/FusionRpg.Server/               → WorldEndpoints.cs (submit mapping, owner-only projection)
tests/FusionRpg.Core.Tests/World/   → recruit goldens, season table, A8 invariant, raise legality
tests/FusionRpg.E2E.Tests/          → the forty-turn recruitment acceptance run
```

`GrowthPhases` is its own file for the reason `LoamPhases.cs:8-10` gives for being one: `TurnEngine.cs` is already the busiest file in the module.

## Code style

Pure functions over the world model with an injected seed where a roll is needed; the phase wiring calls them and applies the result. Integer per-mille for rates, `long` for every magnitude, widen before multiplying, divide by 1000 last and exactly once, overflow throws. No logging side effects — the report is the log. Catalogs validate at static init and reject unknown ids at the write gate.

Every number a balance pass would touch is a named accessor over tuning, never a literal — `LoamPolicy.cs` is the pattern, down to each accessor carrying the design claim it encodes in its own doc comment.

## Testing strategy

- **Recruitment:** a pulse fires on week boundaries and only on week boundaries; a cleared lair multiplies its sector's pulse and an intact one does not; a plague month suppresses growth and beats a special week; `raise` is rejected for each illegal case with its own reason; a raised legion's id is derived and stable across replay.
- **The target, as a measurement:** the forty-turn acceptance run reports the player's legion count, and asserts it lands inside `growth.legionTarget`. It is a **calibration assertion over tuning**, not an engine limit — if the count is wrong, the tuning moves, not the test's meaning.
- **Seasons:** `season(turn)` is a table test across a full cycle plus the boundaries either side; the same `(turn, seed)` produces the same roll with and without the season member present; adding the season draws no RNG (the `calendar:week` / `calendar:month` streams produce byte-identical sequences before and after).
- **Upkeep:** the season term is applied by both overloads, so `FrontierRulesPolicy` and `LoamForecast` agree with `LoamPhases` — one test that walks all four call sites of `LoamUpkeep.For` and asserts a single answer. Division happens once; a four-factor product at the top of its legal range does not overflow, and a forced overflow throws.
- **A8, as an invariant:** across every authored development level, marginal yield exceeds marginal upkeep. Not a sample point.
- **Command round trip:** a property test over **every kind in `WorldCommandKinds.All`** — build a command with every payload field populated, submit it, list it back, assert equality. This is the test whose absence let `sustain` and `build` ship lossy, and it is what stops `raise` and `develop` becoming the third and fourth.
- **Determinism:** the forty-turn run replays byte-identically; reversing input entity order changes nothing.
- **Goldens:** the two moves are triaged before re-blessing — the field batch must move hashes and nothing else, and the behaviour bump gets a predicted-delta writeup naming which goldens move and why.

## Boundaries

- **Always:** pure over `(turn, seed)`; new content is a catalog row; every tunable in `data/tuning/`; the season reaches truth, belief and forecast together; a new command kind is plumbed through all five sites in the same change; magnitudes are `long`.
- **Ask first:** the `RulesetVersion` 5 → 6 bump and its re-bless; adding a season term to movement as well as to yield and upkeep; a soft cap on recruit stock; editing either shipped template to add lairs or seats; renaming the `*CostMilli` tuning keys.
- **Never:** a hard cap on legion count or on any magnitude; a wall clock or `System.Random` inside `Growth`; a `switch (id)` over sector, project or structure ids; SQL outside `FusionRpg.Data`; touching the phase *order* (this module fills `Growth`, it does not move it); world-battle rewards paying loam ([empire-economy-ssot.md](../empire-economy-ssot.md):316 makes that `combat-handoff`'s constraint, and this module must not reopen it from the other side).

## Success criteria

1. A forty-turn `first-light` run ends with the player commanding a legion count inside `growth.legionTarget`, raised by their own orders. 2. The season is derived, hashed, replayed, and visible in the turn report; the HUD slot has a real field behind it. 3. The A8 invariant test passes across the full authored level range. 4. Every kind in `WorldCommandKinds.All` survives the store round trip with every payload field intact — `sustain` and `build` included. 5. Two golden re-blesses, both triaged in advance, and `RulesetVersion` advances exactly once. 6. All suites and the four boundary guards stay green.

## Open questions

**The recruit rate.** The 6–10-by-turn-40 target pins the endpoint, not the shape: a steady seat drip and a lair-heavy burst both land there and play completely differently. The method to settle the *numbers* exists (the L9 harness pattern), so those are a scheduled measurement rather than a question — but which shape the game wants is a design call the owner has not made.

**What a season actually changes.** Three seams are available and all three are one-line multipliers; the choice is balance-bearing. Endless Legend's winter changes movement *and* yields, which is the maximal reading; the minimal one is yield alone. Movement is the riskiest of the three, because it interacts with zone of control and with the arithmetic lane-crossing solution, and a seasonal budget change makes a forecast the player already saw go stale.

**Whether a project is genuinely a second concept.** A project and a structure are both "a cost, some turns, then a persistent effect", which argues for one catalog with a scope field. Against that: `RequiredSlotKind` and `YieldMultiplierMilli` are meaningless on a sector-wide project, and a catalog with columns that are null for half its rows is the shape that later grows a `switch`. Two catalogs is the safer default and the more verbose one; the call is worth making deliberately rather than by drift.

**Who owns the reward layer.** [empire-economy-ssot.md](../empire-economy-ssot.md) §5 decides *what* held ground pays — souls, essence and materials through structures, banked per connected component — but names no module. It sits between this one (structures and yields) and `loam-texture` (wave 5). Until that is assigned, the G-F reward hole the loam gate was explicitly narrowed around stays open, and this module's structures produce loam and recruits only.
