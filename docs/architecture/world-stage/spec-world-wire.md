# Spec: world-wire

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-wire` in the
[world-stage capability map](../world-stage-map.md). **Level 1, no dependencies** — it parallelises
with `world-contract` and `world-commands`, because the contract may declare a field `Pending`
before this module fills it.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §2.2, §4.9, §4.10, §4.11, §8c.3, §8c.4.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §M.

---

## Objective

Make the world's server projections carry what the stage has to draw, and stop the turn report
narrating the wrong half of the map to the wrong people.

Every gap below names a line. That is the point of §8c.4's finding: *"the server is not the problem;
the contract is"* is true of the **engine**, and it hid a projection layer with nine holes in it. The
engine already computes all of this. `WorldDtos.cs` and `WorldEndpoints.cs` throw most of it away.

Three examples, because they are the shape of the whole module:

- `WorldSectorDto.PressureMilli` is **declared** (`WorldDtos.cs:72`) and **never assigned**. It is not
  speculative state: `LoamPhases.NextPressure` writes it every single turn from fade contagion
  (`LoamPhases.cs:266-283`, called at `:169`). The projection's own comment — *"Pressure and depletion
  stay zero: nobody glances at a sector and reads its depletion off"* (`WorldEndpoints.cs:304-308`) —
  was written before that feature existed and is now wrong about half its subject.
- `ConstructionTurnsRemaining` is **read and discarded**. `WorldEndpoints.cs:300` passes it into
  `Habitability.For` and then drops it, so the client can see that a sector is habitable but never
  that a structure is three turns from finishing.
- A UI showing `loamStock` has **no denominator**. `LoamPhases.EffectiveCapacity`
  (`Loam/LoamPhases.cs:58`) is public and shared with the forecast, and it is on no DTO.

**Success is that every number `world-contract` declares `pending` has a value, and no report line
reaches a player who should not see it — or fails to reach one who should.**

## Design

### 1. The batched projection change — nine additions, one re-bless

These land **together**, in one change, for a reason `decisions.md` already records: L25 batched six
hashed field additions into a single golden re-bless *after an adversarial audit caught five specs
each independently reopening the same budget one field at a time*. This module does not repeat that.

| Addition | Source, verified | DTO |
|---|---|---|
| `CarriedLoam` | `WorldState.cs:262` | `WorldEntityDto` |
| member `Role` | `WorldState.cs:220` (`WorldEntityMemberRole`, `:206-210`) | `WorldEntityMemberDto` |
| `ConstructionTurnsRemaining` | `WorldState.cs:116`, already on the belief side at `FactionIntel.cs:59`, read and dropped at `WorldEndpoints.cs:300` | `WorldSlotDto` |
| `WardenBindingId` | `WorldState.cs:173` | `WorldSectorDto` |
| `NeglectedTurns` | `WorldState.cs:180` | `WorldSectorDto` |
| `PressureMilli` **assigned** | `LoamPhases.cs:266-283` | `WorldSectorDto` (declared `WorldDtos.cs:72`) |
| `EffectiveCapacity` | `Loam/LoamPhases.cs:58` | `WorldSectorDto`, as `LoamCapacity` |
| `GateKeyId` | `WorldState.cs:198`, hashed at `WorldCanonical.cs:47`, absent from `WorldLaneDto` (`WorldDtos.cs:151-162`) and its projection (`WorldEndpoints.cs:481-492`) | `WorldLaneDto` |
| legion `Capacity` / `Burn` / `Runway` | `LegionSupply.cs:20, 24, 32` — plus `TurnsUntilExhausted` at `:46`, which is what the client actually needs | `WorldEntityDto` |

The last row is in scope because §2.2 lists *"legion capacity / burn / leash / runway as **state**"* as
a wiring gap — they reach a client today only as the narration string `legion.runway:` — and because
`world-contract`'s `LegionView` binds to them. They are **not** derivable client-side: they read
`LoamPolicy.CarryPerBearer` and `BurnPerMember` (`LoamPolicy.cs:91, 94`), which are server tunables,
and `world-contract`'s own boundary forbids deriving a loam number in TypeScript.

**Two of these are owner-gated, and it matters which.** `WardenBindingId` and `NeglectedTurns` are
read from truth and gated exactly the way `StabilityMilli` already is at `WorldEndpoints.cs:309-311`
— present when `sector.OwnerFactionId == view.FactionId`, absent otherwise. Warden presence on
somebody else's ground is a dynamic fact about their economy, and §2 below says nothing dynamic
crosses the fog. Everything else in the table is either the viewer's own force or as public as the
slot it sits on (`WorldDtos.cs:35-38`, `FactionIntel.cs:49-53`).

**`WorldSlotDto.OwnerFactionId` is the one that is not free, and this spec chooses the cheap
resolution.** It is declared (`WorldDtos.cs:31`) and never assigned (`WorldEndpoints.cs:318-327`).
The Core model has it — `WorldCanonical.cs:41` hashes `sl.OwnerFactionId` — but **`RememberedSlot`
does not** (`FactionIntel.cs:31-60`), so it cannot be projected from belief without adding a belief
field, and belief **is** hashed (`WorldCanonical.cs:72-74`). Adding it there moves every world golden
for a value nothing yet reads.

> **Decision:** project it from truth, owner-gated, the `StabilityMilli` pattern
> (`WorldEndpoints.cs:309-311`). A viewer sees the slot owner on ground they hold and null elsewhere.
> This is honest — slot ownership on foreign ground is a fact the observer never surveyed — costs no
> belief field, and moves no state hash. If a later module needs remembered slot ownership, that is a
> `RememberedSlot` addition and it joins the *next* batch, not this one.

### 2. The fog fix — three defects, and a deliberate answer to what an opponent may leak

Report entries are filtered on the structured `SectorId` by `VisibleTo` (`WorldEndpoints.cs:215-219`),
called at `:175`. Two lines of code, three defects, failing in **opposite directions**:

```csharp
static bool VisibleTo(string? sectorId, BelievedWorldView? believed)
{
    if (believed is null || sectorId is null) return true;   // ← leaks to everyone
    return believed.Believed(sectorId) is not null;          // ← ever-seen, not seen-now
}
```

**Defect A — a null `SectorId` is shown to every viewer.** Four production call sites pass null:
every battle line (`BattleReporting.cs:36`), `legion.topup` (`LegionSupply.cs:98`), `loam.handicap`
(`LoamPhases.cs:119`) and `loam.shortfall.unresolved` (`LoamPhases.cs:141`).

**Defect B — a lane id or an event detail in the `SectorId` slot is filtered out for everyone.**
Three sites, not the two §2.2 counted (§8c.3 corrected it):

- `MovementPhase.cs:105` — `legion.runway:` passes `outcome.AtSectorId ?? outcome.OnLaneId`;
- `MovementPhase.cs:123-124` — `Arrival` is scheduled with `ArrivedAtSectorId ?? OnLaneId ?? ""`;
- `MovementPhase.cs:195` — the drain loop passes `evt.Detail` straight into the sector slot, and a
  `Halt` event's detail is `"zoc:" + outcome.AtSectorId` (`:127-128`), which is not a sector id.

`Believed("l-c1-c2")` returns null, so those lines vanish for everybody — **the client's `halt`
keyframe (`turnPlayback.ts:38`) can never fire against a live server.**

**Defect C — `VisibleTo` gates on "have I ever seen this sector", not "can I see it now."** Ground
scouted on turn 6 still reports live battles on turn 80. That contradicts §4.9's own static-vs-dynamic
rule, which the four-state ladder already supports: `StateOf(sectorId)` returns `Watched` exactly when
the faction sees it this turn (`FactionIntel.cs:133-135`, `IWorldView.cs:133-134`) and is already
called by this endpoint at `WorldEndpoints.cs:265`.

#### The decision §8c.3 demands

Fixing the leak and moving the AI-reasons panel to the dev tree would together remove the last channel
through which an opponent is legible — today you watch Zomboss's economy fail only *by accident*,
through that very bug. So this spec decides what an opponent may leak rather than inheriting whatever
survives the fix.

> **Rule W-F1. A report entry reaches a viewer when — and only when — one of these holds:**
>
> 1. **Audience.** The entry names the viewer's own faction. Faction-scoped economy lines have no
>    sector and never did; they belong to their owner, not to everyone.
> 2. **Live sight, for a dynamic fact.** The entry names a sector whose `StateOf` is `Watched`.
>    Battles, marches, halts, arrivals, routs.
> 3. **Remembered sight, for a static fact.** The entry names a sector the viewer has ever seen
>    (`Believed(...) is not null`). A claim, a structure completing, ownership changing — Civ VI's
>    line, and §4.9 already adopted it.
>
> **An opponent leaks exactly this: what you can currently see them do, and what they permanently
> changed on ground you have walked.** Their economy is theirs. That is the deliberate answer, and it
> is stricter than today's behaviour in one direction and looser in the other.

#### How it is implemented

- **`TurnReportEntry` gains one optional member: `string? Audience`** (`TurnReport.cs:24-25`), set by
  the faction-scoped emitters (`LoamPhases.cs:119, 141`, `LegionSupply.cs:98`) to `faction.FactionId`.
  `TurnReport.Add` (`:63-64`) gains the matching optional parameter.
- **`BattleReporting.cs:36` passes `request.LocationId`** — it already has the sector in hand and
  passes it to `ClearGuard` two lines above (`:34`).
- **`MovementPhase` stops putting non-sectors in the sector slot** at `:105`, `:123-124` and `:195`:
  pass `AtSectorId` when there is one, and **null** otherwise, with the lane id staying in `Detail`
  where the client already reads it. A line about a legion mid-lane is a dynamic fact about the
  viewer's own force, so it carries `Audience = entity.OwnerFactionId` and reaches its owner.
- **`VisibleTo` becomes three clauses**, one per rule, keyed on `Kind` for the static/dynamic split.
  The static kinds are a closed, named list — not a prefix guess on free text, which is the failure
  the entry's own doc comment already warns about (`TurnReport.cs:15-19`).

**Determinism: no state golden moves.** `StateHasher.Hash` is taken over `WorldCanonical.Write(world)`
(`StateHasher.cs:17`), and `WorldCanonical` never touches the report. `TurnReportEntry` is a stored
`report_json` blob re-read at `RpgStore.WorldTurns.cs:588`; an old row deserializes with `Audience`
null, which reads as "no audience", which is the behaviour those rows had. `RulesetVersion` is
untouched — no engine *behaviour* changes, only which lines a projection hands out.

**One limitation, stated rather than hidden.** `believed` is built from the world's **current** state
(`WorldEndpoints.cs:159`), so a report about turn 6 is filtered by what the viewer can see *now*. That
is the only answer available without storing per-turn sight, and it is the same approximation the
endpoint already makes. It errs toward showing more, never less, on ground you have since occupied.

### 3. The calendar

The HUD's turn readout needs the calendar (§8b.7) and it is on **neither** `WorldStateDto`
(`WorldDtos.cs:193-202`) nor `WorldHeaderDto` (`:8-16`). It reaches a client only as two report
entries in the `Events` phase (`TurnEngine.cs:228, 230`), on the turn route.

**The client cannot derive it.** `DaysPerWeek` and `WeeksPerMonth` are server tunables read from
`WorldTuningHub` (`TurnCalendar.cs:22-23`), and the roll needs the world seed — which is
**deliberately absent from every projection** (`WorldDtos.cs:3-7`: *"the seed is the input to every
future roll, and a client that knows it can predict outcomes the server has not committed yet"*).

> **Decision:** a `WorldCalendarDto` on **`WorldStateDto`**, not `WorldHeaderDto`. The state route
> (`WorldEndpoints.cs:36`) is what the stage polls every turn; the header route (`:27`) is a listing
> shape whose `CurrentTurn` already answers what a listing needs. Duplicating it would give the HUD
> two sources for one number.

It carries the **current turn's roll only** — `TurnCalendar.Roll(world.CurrentTurn, seed)`
(`TurnCalendar.cs:31`) — plus `DaysPerWeek` and `WeeksPerMonth` so the client can place today inside
the week and month without arithmetic of its own. The seed does not go on the wire and neither does
any future turn's roll: the calendar is *pure* in `(turn, seed)` and would let a client with both
enumerate the whole campaign's plague months.

### 4. The prospected set

`Prospecting.Reveal(world, factionId)` is implemented, returns `IReadOnlySet<string>`
(`IntelRecorder.cs:179`), reaches four lanes (`:174`) and has no DTO carrying it.

`WorldStateDto.ProspectedSectorIds`, computed at projection time — the same shape `Lifelines` already
uses (`WorldEndpoints.cs:382-396`). Unlike lifelines it is **not** opt-in: `Reveal` iterates entities
and skips every one whose stance is not `"dowse"` (`IntelRecorder.cs:187`), so with no dowser the cost
is one pass over a list of three.

It stays a **separate set, never merged into `intel`**. A dowser answers one narrow question — is
there a loam source here — and leaks no owner, no danger band and no forces (`IntelRecorder.cs:160-166`).
Folding it into the intel ladder would silently promote an unknown sector to scouted.

> **Nothing here makes `dowse` orderable.** That is `world-commands`' four-part change (§8c.4), and
> this projection is inert until it lands — correctly so: the set is simply empty.

### 5. The catalogs

`StructureCatalog.All` (`StructureCatalog.cs:53`), `SlotTypeCatalog.All` (`SlotTypeCatalog.cs:54`) and
`StrengthBandCatalog.All` (`Intel/StrengthBandCatalog.cs:35`) are public with **no HTTP caller**. A UI
cannot learn what is buildable, what a slot letter means, or what `"warband"` is worth.

One route: `GET /api/world/catalog`. No world id, no viewer, no fog — these are rules, not state.
Structures (id, name, kind, required slot kind, cost, yield multiplier, capacity bonus), slot types,
strength bands (index, name, floor, ceiling) and lane types.

**The `CostMilli` trap is the reason this route matters and the reason it must be named carefully.**
`StructureDef.Cost` (`StructureCatalog.cs:26`) holds **whole loam units**, not per-mille — it is
compared directly against `CarriedLoam` at `BuildResolver.cs:101` and subtracted at `:115`. The DTO
field is named **`Cost`**, and its XML doc says the unit in words. **Renamed 2026-09-05 (world-map
W57)**: the Core field was `CostMilli`, matching the DTO's own name now instead of lying by 1000×
across the wire boundary — was out of scope when this was written, is not anymore, and GG-46 stays a
Tier-1 gate regardless.

### 6. The turn-report fixture

None exists. `world.spec.ts:91` stubs `**/api/world/first-light/turn/**` as a flat 404, so
`world-playback` — which owes a table for 21 event prefixes, 3 battle kinds, 2 calendar subjects and
**37** drop reasons — has nothing to build against.

Copy the pattern that already works. `WorldFixtureTests.cs:28-50` drives the live API, serializes the
response with `WriteIndented`, and asserts it byte-for-byte against a checked-in file, re-blessing
under `FUSIONRPG_BLESS_WORLD_FIXTURE=1` (`:42`). The new test does the same for
`GET /api/world/{id}/turn/{n}` after playing a scripted handful of turns on `first-light`, writing
`web/fusion-rpg-web/src/features/world/fixtures/first-light-turn.json` — **the name matters: `world-playback` consumes this exact path, and an earlier draft of the two specs named it two different things, which would have met only as a missing import**.

Two properties the scripted turns must have, or the golden proves nothing:

1. **At least one entry of each visibility class** — an own-audience economy line, a live-sight battle,
   a remembered-sight claim, and one line the viewer must *not* see. §2's rule is only tested if the
   fixture contains something it excludes.
2. **A `halt` line that actually appears** — the keyframe `turnPlayback.ts:38` recognises and has
   never once received.

### 7. What this costs, priced honestly

`first-light.json` is byte-pinned (`WorldFixtureTests.cs:17, 48-49`) and consumed by **seven** files:
`e2e/world.spec.ts`, `SectorFog.test.tsx`, `SectorNode.test.tsx`, `SectorPanel.test.tsx`,
`WorldPage.tsx`, `worldSelection.test.ts`, `worldViewModel.test.ts`. §7 of the ideal under-priced this
— the word "golden" did not appear in it.

So the change is: **project the fields → re-bless the fixture → sweep the seven consumers.** That is
one re-bless for nine additions, which is why §1 batches them. The sweep is mechanical (the new fields
are additive; nothing existing changes shape) but it is not zero, and the old `#/world` route must
still render — the map's assumption 2 says it keeps working until its replacement lands.

**No world state golden moves.** `WorldWaveOneAcceptanceTests.GoldenFinalHash`
(`tests/FusionRpg.Data.Tests/WorldWaveOneAcceptanceTests.cs:123`) is a hash over `WorldCanonical`
output, and this module changes no Core state field — the projection reads what the engine already
writes, and the one member added anywhere in Core (`TurnReportEntry.Audience`) is not hashed.
`TurnEngine.RulesetVersion` (`TurnEngine.cs:42`) stays at 5.

## What stays out

- **Making anything orderable.** `dowse`, `sustain`, `build`, `cede` and `ward` are all
  `world-commands`'. This module projects; it never widens the write surface.
- **The translation table.** `world-playback` owns turning `loam.shortfall:340` into a sentence. This
  module owes it a fixture and a report that reaches the right player, nothing more.
- **The FE view types and adapters.** `world-contract`'s. This module fills the fields that contract
  declares `pending`; it does not touch `src/contract/`.
- **The `Growth` no-op** (`TurnEngine.cs:196-200`) and recruitment. `sector-development`'s, per the
  map's "what it is not".
- **Remembered slot ownership.** §1 chose the truth-gated projection deliberately; adding
  `RememberedSlot.OwnerFactionId` is a hashed change and belongs to whatever module first needs it.
- **Routes to list or delete worlds, or fetch a turn range** (§2.3). Real gaps, not this module's.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Server.Tests
dotnet test tests\FusionRpg.E2E.Tests
dotnet test tests\FusionRpg.Guard.Tests

# after the projection change, once, deliberately:
$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1"; dotnet test tests\FusionRpg.E2E.Tests

.\scripts\guard-dal.ps1                 # SQL stays inside FusionRpg.Data
.\scripts\guard-secondary-no-unity.ps1
python scripts\audit-magic-numbers.py --summary
python scripts\audit-overflow.py

cd web\fusion-rpg-web; npm test          # the seven fixture consumers
```

## Project structure

```
src/FusionRpg.Contracts/
  WorldDtos.cs              → 9 field additions; WorldCalendarDto; ProspectedSectorIds;
                              WorldCatalogDto + its structure/slot-type/band rows
src/FusionRpg.Server/
  WorldEndpoints.cs         → ProjectSector, the lane and entity projections, VisibleTo (3 clauses),
                              the calendar roll, the prospected set, GET /api/world/catalog
src/FusionRpg.Core/World/
  Turn/TurnReport.cs        → TurnReportEntry.Audience + Add's optional parameter
  Turn/BattleReporting.cs   → :36 passes request.LocationId
  Movement/MovementPhase.cs → :105, :123-124, :195 stop passing non-sectors as SectorId
  Loam/LoamPhases.cs        → :119, :141 set Audience
  Loam/LegionSupply.cs      → :98 sets Audience
tests/FusionRpg.E2E.Tests/
  WorldFixtureTests.cs      → unchanged; the pattern being copied
  WorldTurnFixtureTests.cs  → new, byte-pins first-light-turn.json
web/fusion-rpg-web/src/features/world/fixtures/
  first-light.json          → re-blessed
  first-light-turn.json          → new
```

No SQL anywhere in this module: the store is read through `RpgStore` exactly as the endpoints already
do (`guard-dal.ps1`).

## Code style

Match the projection's existing voice — every non-obvious field carries an XML doc saying *why* it is
visible to whom, because that is the file's whole subject. Magnitudes are `long`; per-mille stays
`int` and says `Milli` in its name; a whole-unit field never does.

```csharp
/// <summary>
/// The ceiling `LoamStock` is measured against — `LoamPolicy.LoamCapacity` plus any active
/// granary's bonus (`LoamPhases.EffectiveCapacity`). Owner-only, like the stock it denominates:
/// a gauge without a denominator is a number nobody can read.
/// </summary>
public long LoamCapacity { get; init; }

/// <summary>
/// Whole loam units — the model compares it directly against a legion's `CarriedLoam`
/// (`BuildResolver.cs:101`). Named `Cost` here on purpose (matching `StructureDef.Cost` since
/// world-map W57 renamed it off its former, misleading `CostMilli` name): a renderer trusting
/// `Milli` would be wrong by 1000×.
/// </summary>
public long Cost { get; init; }
```

The fog filter reads as three named rules, not one boolean expression, because §2's decision is the
thing a future reader needs to find:

```csharp
static bool VisibleTo(TurnReportEntry e, string? viewer, BelievedWorldView? believed)
{
    if (believed is null || viewer is null) return true;               // SIM / no viewer
    if (e.Audience is { } a) return string.Equals(a, viewer, StringComparison.Ordinal);
    if (e.SectorId is not { } sectorId) return false;                  // W-F1: no audience, no ground
    return IsStaticFact(e.Kind, e.Detail)
        ? believed.Believed(sectorId) is not null                      // remembered sight
        : believed.StateOf(sectorId) == IntelState.Watched;            // live sight
}
```

## Testing strategy

xUnit, in the project that owns the boundary being tested. Five levels:

1. **Projection completeness (Server.Tests).** Every field in §1's table is non-default in a state
   response built from a world where it is non-default in Core. The failure mode this catches is the
   one that produced this module: a field declared and never assigned reads as a zero nobody notices.
2. **Owner gating (Server.Tests).** `WardenBindingId`, `NeglectedTurns` and `WorldSlotDto.OwnerFactionId`
   are null for a viewer who does not own the sector, and populated for one who does — asserted from
   *two* viewers over the same world, not from one.
3. **Fog, all three defects, as three named tests (Server.Tests / E2E).**
   - a `loam.handicap` line reaches its own faction and **not** the other one (Defect A);
   - a `halt` line **reaches its owner at all** — today it reaches nobody (Defect B);
   - a battle on ground scouted long ago and not currently seen is **withheld**, while a claim on the
     same ground is **shown** (Defect C, and the static/dynamic split in one assertion).
4. **Determinism (Core.Tests / Data.Tests).** `WorldWaveOneAcceptanceTests.GoldenFinalHash` is
   **unchanged** — asserted, not assumed. A `report_json` row written before `Audience` existed still
   deserializes and still projects, which is the `stance`-shaped regression this module could
   otherwise introduce on the read path.
5. **The two fixtures (E2E).** `first-light.json` byte-matches after the re-bless;
   `first-light-turn.json` byte-matches and contains at least one entry of each of §6's four visibility
   classes. The second assertion is what makes the fixture a golden for `world-playback` rather than a
   sample.

Coverage is not the bar here — a projection line is trivially covered by any test that calls the
endpoint. The bar is **level 2 and level 3**, which are the only tests that would have caught the
defects this module exists to fix.

## Boundaries

- **Always:** project what the engine already computes; state the unit family in the field name or its
  doc; gate an owner-only number structurally, the way `ComputeLoamReading` does (`WorldEndpoints.cs:279-282`)
  rather than with a per-field ownership check somebody will forget on the next field; add fields in
  one batch and re-bless once.
- **Ask first:** any change to a Core *state* field, which moves the world golden and may need a
  `RulesetVersion` decision — this module is meant to need none. Also any change to
  `RememberedSlot`, which is hashed (`WorldCanonical.cs:72-74`). Also widening `VisibleTo` beyond
  W-F1: §8c.3 made what an opponent leaks a decision, and it stays one.
- **Never:** put the world seed on the wire (`WorldDtos.cs:3-7`) or any future turn's calendar roll.
  Never read `WorldState` for a sector the viewer has not seen — sectors come from belief
  (`WorldEndpoints.cs:366-372`), and the two owner-only exceptions are gated on ownership, not on
  intel. Never write SQL outside `FusionRpg.Data`. Never name a whole-unit magnitude `…Milli`.

## Success criteria

1. All nine additions in §1 reach a client, and `first-light.json` is re-blessed with the seven
   consumers green.
2. `WorldSlotDto.OwnerFactionId` is assigned, owner-gated, with **no** `RememberedSlot` change and
   **no** state-hash movement.
3. `PressureMilli` carries live contagion state, and `WorldEndpoints.cs:304-308`'s comment is
   corrected rather than left contradicting the code beneath it.
4. All three fog defects are fixed, and W-F1 is implemented as three named clauses with a test per
   defect. A `halt` line reaches a player for the first time.
5. `WorldStateDto` carries the calendar; no seed and no future roll leave the server.
6. `GET /api/world/catalog` answers, and its structure cost field is named `Cost` and documented as
   whole loam units.
7. `first-light-turn.json` exists, is byte-pinned by a test in `WorldFixtureTests.cs`'s pattern, and
   contains an entry of each of §6's four visibility classes.
8. `GoldenFinalHash` is unchanged and `TurnEngine.RulesetVersion` is still 5 — asserted.
9. All five .NET suites, the four boundary guards, and `npm test` are green.

## Open questions

**None.** §8c.3 required a deliberate answer on opponent legibility and §2 gives one (W-F1); §8c.4
priced the fixture re-bless and §7 records the seven consumers by name; the one genuine fork this
module contained — how to reach `WorldSlotDto.OwnerFactionId` without moving a golden — is decided in
§1 with the alternative and its cost stated. The prospected set and the `dowse` stance being inert
until `world-commands` lands is a **build-order fact**, not a question: the projection is correct and
empty until the order exists.
