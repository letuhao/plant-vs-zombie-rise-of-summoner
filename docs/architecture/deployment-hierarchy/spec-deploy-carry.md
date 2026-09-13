# Spec: `deploy-carry`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `deploy-carry`, row 1 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, no dependency — the foundation every other module in the map builds on). Ideal:
[deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"The shape" (Inherit/Settle),
decisions.md row **Deployment hierarchy SSOT (2026-09-13)**.

## Objective

The **carry-in** half of the deployment tree is data and pure functions that exist, are unit-tested
in isolation, and are never called by anything that resolves a real battle. The **carry-out**/settle
half already works end to end for delve rooms. This module wires carry-in so it does: a party's
second delve room starts from the pools and statuses its members actually have, not rest-max and a
clean slate, while changing nothing about how a room settles today.

Success looks like: a delve party that enters room 2 with 40% stamina and a `wound.minor` status
enters room 2's battle already at 40% stamina with `wound.minor` applied at the first tick — proven
by a property test that resolves two rooms back to back and asserts the second room's opening state
equals the first room's `CarryOut`, byte-identical on replay. No other deployment kind (lawn, siege,
expedition) changes behavior in this module — they gain the same `CarryInPools` field but nothing yet
populates it for them (real gap, named and left to whichever program adds their first non-full
deploy).

## Locked anchors

- **Snapshot, never a live reference** (decisions.md, Deployment hierarchy SSOT, 2026-09-13): a child
  carries pool *values*, status *specs*, and sticky *flags* — never a live `StatusInstance` (keyed
  `entity:{ptr}`, dies with the match) and never a reference back to the parent's own state.
- **`hp` is the one seat, never duplicated** (`PartyPoolsCarry.cs:12-17`): `CurrentHp`
  (`BattleActorSetup`) / `HpRemaining` (`BattleActorResult`) are the delve-battle-profile module's own
  fields; this module's pure functions never touch them directly — `CarryOut`'s own assertion
  (`hpRemaining == pools["hp"]`, `PartyPoolsCarry.cs:66-68`) is the seam that would catch a second hp
  writer.
- **Resource pools are `delve-attrition`'s seat, not `delve-carry`'s** (`DelveCarry.cs:21-22`
  docstring, verified this session): `DelveCarryIn.Apply` deliberately does not read `CarryInPools` —
  that responsibility sits on whichever caller constructs the next room's `BattleActorSetup` from
  `DelveMemberState.Pools`, i.e. this module's own job, kept out of `DelveCarryIn.Apply` by design so
  the status/shield mapping and the pool mapping stay two separately-testable seams.
- **Troop representation is locked, not this module's problem.** `WorldEntityMember.Hp/Wounds` stays
  headcount arithmetic (`WorldState.cs:281-282`); this module changes nothing there (map's own
  "wiring tasks, not owner questions" line).

## What already exists

### Built (pure, unit-tested, zero production callers)

| Finding | Evidence |
|---|---|
| `DelveMemberState` — one party member's carry state: `Pools` (all six resource ids incl. `hp`), `Statuses`, `Shield`, `NerveStacks`, `Downed`, `DownedOnce` | `src/FusionRpg.Core/Delve/Attrition/DelveMemberState.cs:18-25` |
| `PartyPoolsCarry.SplitForCarryIn` — `DelveMemberState.Pools` (six ids) → `(CurrentHp, CarryInPools)` (five ids), throws on any missing id | `src/FusionRpg.Core/Delve/Attrition/PartyPoolsCarry.cs:24-39` |
| `PartyPoolsCarry.BuildForBattle` — `(CurrentHp, CarryInPools, atTick)` → a seeded `ActorResourcePools` via `FromStored`, merging the seat back into the six-id contract | `PartyPoolsCarry.cs:44-50` |
| `PartyPoolsCarry.CarryOut` — `ActorResourcePools.SettleAll` → the six-id dictionary written back into `DelveMemberState.Pools`, asserting `hp` never drifted from `HpRemaining` | `PartyPoolsCarry.cs:58-71` |
| `ActorResourcePools.FromStored` — seeds all six pools from a caller-supplied dictionary, throws if any of the six ids is missing (same closed-set discipline everywhere) | `src/FusionRpg.Core/Actions/Cost/ActorResourcePools.cs:32-42` |
| `BattleActorSetup.CarryInPools` field — `IReadOnlyDictionary<string, long>?`, null means "no carry-in, seed at rest-max" (today's behavior, kept as the explicit default) | `src/FusionRpg.Core/Battle/BattleModels.cs:187-192` |
| `DelveCarryOut` / `DelveCarryIn.Apply` — maps a previous room's `Statuses`/`Shield`/`Retreated` onto a fresh next-room `BattleActorSetup`'s `InitialStatuses`/`InnateShield`/`CurrentHp`; pure, no store, no clock | `src/FusionRpg.Core/Delve/Battle/DelveCarry.cs:13,24-36` |
| Both pure modules have dedicated unit test files | `tests/FusionRpg.Core.Tests/Delve/Attrition/PartyPoolsCarryTests.cs`, `tests/FusionRpg.Core.Tests/Delve/Battle/DelveCarryTests.cs` |
| A room's status-outcome effects already append onto party state | `src/FusionRpg.Core/Delve/Events/EventOutcomeDispatch.cs:243` — `var statuses = member.Statuses.Append(spec).ToList();` (nerve refused at `:232` — nerve stays derived from `NerveStacks`, never a carried status spec, unchanged by this module) |

### Wiring gap (the exact lines that are inert, confirmed this session)

| Gap | The inert line |
|---|---|
| `DelveCarryIn.Apply` never sets `CarryInPools` on the setup it returns | `DelveCarry.cs:30-36` — the `with` expression sets `CurrentHp`/`InitialStatuses`/`InnateShield` only; `CarryInPools` is absent, by the design note at `:21-22` (it is this module's own job, not `DelveCarryIn`'s) |
| `PartyPoolsCarry.BuildForBattle`/`SplitForCarryIn` have zero callers outside their own test file | confirmed this session — `grep -rn "PartyPoolsCarry\." src/FusionRpg.Server src/FusionRpg.Core/Delve/Battle` returns only the pure module's own file |
| `DelveCarryIn.Apply` has zero production callers | confirmed this session — `grep -rn "DelveCarryIn\." src/` returns only its own file and its test |
| The live delve battle path passes no carry state at all | `src/FusionRpg.Server/DelveBattleSession.cs:169-172` — `DelveBattle.Run(_setup, _seed, actionCatalog:, containerResolver:, intentSource: raid)`; `_setup` is deserialized whole from `entry.SetupJson` (`DelveBattleSessionManager.cs:217`, `WebMatchService.cs`/`RpgStore.WebMatches.cs` own the write side) — wherever that JSON is first built for a non-opening room is where `CarryInPools`/`InitialStatuses` must be populated and currently are not |
| Battle actors start at rest-max, not from any parent, when no carry-in is supplied | `src/FusionRpg.Core/Battle/BattleEngine.cs:37` — `Hp = setup.CurrentHp ?? setup.MaxHp;` (today's fallback, kept as the explicit "no carry-in" default — never removed by this module, only bypassed once a real value is supplied) |
| `EventOutcomeDispatch.cs:243`'s appended statuses have no path into the next room's setup | confirmed this session — nothing constructs a `DelveCarryOut` from `DelveMemberState.Statuses` and hands it to `DelveCarryIn.Apply` |

### Real gap (out of this module's scope, named so it is not silently assumed done)

Lawn, siege, and expedition deploys gain the `CarryInPools` field (it is already declared on the
shared `BattleActorSetup`, not delve-only) but **nothing populates it for them** — no parent state to
carry from exists for those kinds yet (siege has no `instanceId ↔ ptr` binding at all, per the ideal
doc's own "Real gap" table). This module wires the **delve** path only, because it is the one
deployment kind that already round-trips a specimen through multiple children in one run
(`delve-attrition`'s existing `DelveMemberState`/`CloseDelve` machinery). Lawn/siege/expedition
carry-in is each of those programs' own follow-on, consuming the same `CarryInPools` field this
module proves out.

## Design

### 1. Carry-in: `DelveMemberState` → the next room's `BattleActorSetup`

At the point a delve room's `BattleActorSetup` list is assembled for a party's members (today: whole
JSON built once, ahead of `DelveBattleSession`'s construction — the exact file is `delve-attrition`'s
integration seam, not renamed by this module), for every member with a live `DelveMemberState`:

```
(currentHp, carryInPools) = PartyPoolsCarry.SplitForCarryIn(member.Pools)
carryOut = new DelveCarryOut(member.Statuses, member.Shield, Retreated: false)
setup = DelveCarryIn.Apply(nextRoomSetup, currentHp, carryOut) with { CarryInPools = carryInPools }
```

`DelveCarryIn.Apply` stays exactly as shipped — it is not modified to read pools (Locked anchors,
above). The `with { CarryInPools = carryInPools }` line is this module's one new production call,
composing two already-correct pure functions rather than teaching either one the other's job. A
member with **no** prior `DelveMemberState` (the party's first room) supplies no override — `Hp =
setup.CurrentHp ?? setup.MaxHp` (`BattleEngine.cs:37`) and a null `CarryInPools` fall through to
today's rest-max behavior unchanged, which is why room 1 of every existing delve fixture stays
byte-identical.

### 2. Carry-out: unchanged, confirmed correct

`PartyPoolsCarry.CarryOut` already reduces `ActorResourcePools.SettleAll` into the six-id dictionary
and asserts the one-hp-seat invariant; whatever already calls this at room-clear time (the map's
`delve-attrition` prerequisite, not re-specced here) keeps doing so. This module's job is entirely on
the **inherit** side — the settle side needs no new call.

### 3. Status carry-in, precisely

`member.Statuses` (a `IReadOnlyList<BattleStatusSpec>`, appended at `EventOutcomeDispatch.cs:243` and
by whatever `injury-tiers` adds later) becomes `nextRoomSetup.InitialStatuses` via `DelveCarryIn.Apply`
— applied through `StatusRuntime.Apply` at the child's first tick with full resistance (Locked
anchors: never pre-applied silently, ideal doc §Inherit item 2). `NerveStacks` is **not** part of
`Statuses` and is never carried this way — nerve is exclusively derived from stacks + spirit
(`NerveLadder.StageFor`/`NervePolicy.Sync`), a `delve-attrition` seam this module does not touch.

### 4. What does not change

`ActorHub` compose is untouched — pool/status inheritance is a **snapshot handed to the existing
compose**, never a second combat-derived fold (DESIGN-GATE §5 ActorHub gate: this module neither
contributes nor consumes a new Hub channel; it only changes which starting values `ActorResourcePools`
and `StatusRuntime.Apply` are seeded with, both already-registered seams). No Unity write changes —
the injector's bind path (`UniqueBoundLoadout.TryApply`) is lawn-only and out of scope. `WorldEntityMember`
is untouched (Locked anchors).

## Tunables

This module introduces **no new balance number**. `CarryInPools`'s values are always read from
existing state (`DelveMemberState.Pools`, itself populated by `delve-attrition`'s own tuned hunger/
rest/nerve numbers) — there is nothing here a balance pass would touch directly.

## Numeric types

Every id in `CarryInPools`/`DelveMemberState.Pools` is `long` (`ActorResourcePools`'s existing
six-pool contract; `DerivedStatChannels.ResourceIds`). No `float`, no `double`, no new magnitude.
`atTick` parameters are `long` ms (the existing virtual-time contract, `battle-turn-ideal.md` §4). This
module performs no multiplication and introduces no overflow-relevant arithmetic — it only routes
already-typed values between two existing shapes (a `DelveMemberState`'s dictionary and a
`BattleActorSetup`'s fields).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Attrition.PartyPoolsCarry"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Battle.DelveCarry"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve"   # full delve suite, no regressions
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~Delve" # the new integration call site
.\scripts\guard-actor-hub.ps1        # no new composer introduced
.\scripts\guard-dal.ps1              # unaffected — this module touches no SQL
```

## Structure

```
src/FusionRpg.Core/Delve/Attrition/PartyPoolsCarry.cs   UNCHANGED (already correct)
src/FusionRpg.Core/Delve/Battle/DelveCarry.cs            UNCHANGED (already correct)
src/FusionRpg.Server/<the next-room setup builder>       gains the three-line composition in §Design 1
                                                          — exact file named once the delve-attrition
                                                          integration seam (SetupJson's first writer,
                                                          WebMatchService.cs / RpgStore.WebMatches.cs
                                                          neighborhood) is located during implementation;
                                                          this spec does not rename or move that file.
tests/FusionRpg.Core.Tests/Delve/                         gains the two-room round-trip property test
tests/FusionRpg.Server.Tests/Delve/                       gains the end-to-end setup-construction test
UNTOUCHED: BattleEngine.cs, ActorResourcePools.cs, DelveMemberState.cs, StatusRuntime.cs, ActorHub.cs
```

## Code style

Pure functions over records, composed at the one production call site — no new class, no new
abstraction over the two already-correct modules.

```csharp
// The one new production line this module adds, at the delve host's next-room setup builder.
// Both PartyPoolsCarry and DelveCarryIn stay exactly as shipped; this composes them.
var (currentHp, carryInPools) = PartyPoolsCarry.SplitForCarryIn(member.Pools);
var carryOut = new DelveCarryOut(member.Statuses, member.Shield, Retreated: false);
var setup = DelveCarryIn.Apply(nextRoomSetup, currentHp, carryOut) with { CarryInPools = carryInPools };
```

## Testing strategy

- **Property — two-room round trip:** resolve room 1 of a solo delve to completion, `CarryOut` its
  surviving member(s), feed the result through this module's composition into room 2's setup, and
  assert room 2's *opening* `ActorResourcePools`/`InitialStatuses` equal room 1's `CarryOut` exactly —
  byte-identical across 32 seeds (the existing sweep width).
- **Room 1 stays untouched:** a party's first room, with no prior `DelveMemberState`, resolves
  byte-identical to today's fixtures (no `CarryInPools`, `Hp = MaxHp`) — the existing delve golden
  suite must not move.
- **A wounded carry is visible:** a member entering room 2 with a `wound.*` status (once `injury-tiers`
  ships) fights measurably weaker in room 2 than an unwounded member of the same setup — the concrete
  proof that `injury-tiers` (module 2) actually depends on this module, not just organizationally.
- **`hp` seat assertion still fires:** `PartyPoolsCarry.CarryOut`'s existing `InvalidOperationException`
  on hp drift remains reachable from the real call path (a regression test that a future refactor
  cannot silently bypass the assertion by writing `hp` through a second route).
- **Retreated/dead carries nothing:** a retreated or dead member is never composed into the next
  room's actor list (`DelveCarryIn.Apply`'s own doc comment, `DelveCarry.cs:26-29`) — asserted at the
  call site, not just documented.

## Boundaries

- **Always:** compose the two existing pure functions at the one new call site; keep room 1 (no prior
  state) byte-identical to today; assert the hp-seat invariant reaches production, not just the unit
  test.
- **Ask first:** any change to `PartyPoolsCarry.cs`/`DelveCarry.cs` themselves — both are shipped,
  tested, and correct; this module's job is calling them, not editing them. Lawn/siege/expedition
  carry-in wiring (real gap, explicitly out of scope — ask before pulling it forward).
- **Never:** a second pool-seeding path bypassing `ActorResourcePools.FromStored`; a live
  `StatusInstance` reference crossing the room boundary (specs only, per Locked anchors); touching
  `WorldEntityMember`/troop representation; a new ActorHub composer or private derived fold.

## Success criteria

1. A two-room solo delve run's room-2 opening state equals room-1's `CarryOut`, byte-identical on
   32-seed replay. 2. Every existing delve golden/fixture unchanged (room 1 behavior is untouched).
3. `PartyPoolsCarryTests`/`DelveCarryTests` still pass unmodified (the pure functions are not edited).
4. `guard-actor-hub.ps1`/`guard-dal.ps1` green — no new composer, no new SQL. 5. `injury-tiers` (module
   2) can write a test proving a carried wound status is visible on the *next* deployment, not just
   the current one.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| The composition pattern in §Design 1 (`SplitForCarryIn` → `DelveCarryOut` → `DelveCarryIn.Apply` `with { CarryInPools }`) | `injury-tiers` (module 2) — a carried `wound.*` status rides `Statuses` exactly like any other status spec, no new carry mechanism needed |
| `CarryInPools`/`InitialStatuses` now populated end to end for delve | `corpse-cache` (module 3) — settlement events this module does not create or change, but the carry proof establishes the tree is real before corpse-cache starts writing to it |

## Design-gate checklist

```
[x] Subsystems: delve attrition/carry (Core), delve battle session (Server), battle engine setup
    construction — no Status/Item/World subsystem touched by this module.
[x] Read this session: deployment-hierarchy-ideal.md §Inherit/Settle; deployment-hierarchy-map.md
    row 1; decisions.md "Deployment hierarchy SSOT (2026-09-13)".
[x] Code cited by file:line, opened this session: DelveMemberState.cs (:18-25), PartyPoolsCarry.cs
    (:12-17, :24-71), ActorResourcePools.cs (:32-42), BattleModels.cs (:187-192), DelveCarry.cs
    (:13, :21-22, :24-36), EventOutcomeDispatch.cs (:243), DelveBattleSession.cs (:169-172),
    DelveBattleSessionManager.cs (:217), BattleEngine.cs (:37); test files confirmed present
    (PartyPoolsCarryTests.cs, DelveCarryTests.cs).
[x] Drift reported: none — the ideal doc's wiring-gap citations for this module all matched code
    exactly on a fresh open this session.
[ ] The exact file that first constructs a non-opening room's `SetupJson` (where the new composition
    line lands) was not pinpointed this session — grep narrowed it to the `WebMatchService.cs` /
    `RpgStore.WebMatches.cs` neighborhood; naming the precise call site is implementation's first task,
    not asserted here as already known.
[x] No §2 invariant contradicted: no cap on a magnitude, no `f(Θ)`, SQL untouched (this module is
    Core/Server only), no second ActorHub composer, Foundation/PvZ untouched.
[x] ActorHub gate: this module neither contributes nor consumes a new Hub channel — it only changes
    which starting values already-registered seams (`ActorResourcePools`, `StatusRuntime.Apply`) are
    seeded with.
```
