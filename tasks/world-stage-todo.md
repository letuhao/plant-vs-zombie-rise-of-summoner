# Tasks: world stage

**Status: proposed 2026-09-03, pending owner review. No task is authorized to start by this file.**

Plan: [world-stage-plan.md](world-stage-plan.md) · Capability map:
[world-stage-map.md](../docs/architecture/world-stage-map.md) · Specs:
[docs/architecture/world-stage/](../docs/architecture/world-stage/) · Ideal:
[world-stage-ideal.md](../docs/architecture/world-stage-ideal.md).

**`sector-development` is not in this file.** It is wave 3 of `world-map-program`, and its tasks live
in [world-map-todo.md](world-map-todo.md) as Phase 12. The two programs meet at exactly two points —
the `RulesetVersion` ordering and the legion count — both settled in the plan.

---

## Standing rules for every task below

1. **The capability map's arbitration section wins over any module spec.** Fifteen specs were written
   by four agents working concurrently and then audited five ways; where a spec and the map disagree,
   the map is the corrected version. Individual specs still contain superseded text those rows
   override — that is expected, and it is why this rule is first.
2. **`#/world` keeps working until Phase 4 deletes it.** No flag day. The pure layer
   (`worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts`) *moves* at its
   consuming phase; it does not die early.
3. **Git is hands-off.** No task commits or pushes. Where a phase ends, the last item hands the owner
   a commit message draft and the touched paths.
4. **Never re-bless a golden to make a suite green.** A moved hash is triaged first: a hash that moves
   on a scenario which files no new order is a **defect**, not a golden to bless
   (`decisions.md:103`, where that call was made and zero goldens moved).
5. **Magnitudes are `long`; widen before multiplying; divide by 1000 last, exactly once.** The
   modifier ledger already got this wrong once by rounding twice and printing 60 where the engine
   computes 59.
6. **Any number a balance pass would touch lives in `data/tuning/`**, never a `const`.
7. **Player vocabulary on player surfaces.** Engine tokens appear only in annotation columns and
   developer surfaces. GG-23 is a Tier-1 gate and today's map prints `dave loam.shortfall:340`.

## Task numbering

Sequential, `W1` upward, in phase order. **The numbering is contiguous — an earlier draft of these
fragments was generated in parallel with reserved ranges and renumbered on assembly**, so a task id
here may not match one quoted in an agent report from 2026-09-03.


# Tasks: world stage — Phase 0

Plan: [world-stage-plan.md](world-stage-plan.md) · Map + **arbiter**: [world-stage-map.md](../docs/architecture/world-stage-map.md)
Specs: [world-contract](../docs/architecture/world-stage/spec-world-contract.md) · [world-wire](../docs/architecture/world-stage/spec-world-wire.md) · [world-commands](../docs/architecture/world-stage/spec-world-commands.md)
**Gate: specs are Draft — pending owner review. No task starts until they are approved.**

Standing rules for every task in this phase:

- **The map's arbitration section wins** over any module spec. Where a spec's own text contradicts a
  row in it — the `LoamUnits` brand, the `ward` name, `RulesetVersion`, the calendar's source, the
  golden's name — the task implements the arbitration and fixes the spec's text in the same change.
- **`#/world` keeps working after every task.** The old route is not deleted in this phase; it is
  swept when a fixture moves and left rendering.
- Integer math only · magnitudes are `long` · stable ordering · no wall clock or unowned RNG under
  `Core/World/` · SQL only in `FusionRpg.Data`.
- **Git hands-off** — leave the work in the tree, mark the task done, hand the owner a commit message
  draft and the paths touched. No task commits, stages or pushes.
- **Never re-bless a golden to make a suite green.** Triage first (`decisions.md:103`).

---

## Phase 0 — the seam

The three level-1 modules run in parallel: `world-contract` (W1–W5) touches only `web/`,
`world-wire` (W6–W21) only the server projections and fixtures, `world-commands` (W22–W30) only the
write path. No task in one block depends on a task in another.

### `world-contract` — the sealed FE view contract

- [ ] **W1: The `typeId` ADR and the contract version bump**
  - Description: `contract/types.ts:272` declares `typeId: number`; the wire is `public string TypeId`
    (`WorldDtos.cs:66`), `worldTypes.ts:39` agrees, and the byte-pinned fixture holds strings. This is
    a **narrowing**, which `game-gui-map.md:142` puts behind a contract version bump plus an ADR —
    not the free additive path. It is the first task in the program because nothing FE can be adapted
    until it is settled, and no other view field in W4 costs an ADR.
  - Acceptance: `SectorView.typeId` is `string`; `CONTRACT_VERSION` goes `1 → 2`; a dated row in
    `decisions.md` records the narrowing, the wire evidence, and that adding is free while this was
    not; no other narrowing or rename rides along (each would be its own bump — spec Boundaries).
  - Verify: `cd web\fusion-rpg-web; npm test`, then `npm run build`.
  - Files: `web/fusion-rpg-web/src/contract/types.ts`, `docs/architecture/decisions.md`.
  - Dependencies: None.
  - Scope: XS.

- [ ] **W2: Move the world DTOs to `lib/bus/world.ts`**
  - Description: the world's DTOs live in `features/world/worldTypes.ts`, which is why `contractGuard`
    — matching only imports `from "@/lib/bus` — would pass a rebuilt `stages/world/` that binds
    straight to a REST DTO. Moving them to `lib/bus/world.ts`, where every other domain's already
    live, makes the *existing* guard bite with no guard change and stops the world being the
    exception. `features/world/worldTypes.ts` re-exports during this phase so `WorldPage.tsx` and its
    components keep compiling; it is deleted in Phase 4's retirement task, not here.
  - Acceptance: the DTO types are declared in `lib/bus/world.ts`; nothing outside `src/contract/` and
    the legacy `features/world/` tree imports them; `#/world` still renders (the existing world tests
    are green unchanged); no type is renamed or narrowed in the move — it is a move, not an edit.
  - Verify: `cd web\fusion-rpg-web; npm test`, then `npm run build` and `npm run lint`.
  - Files: `web/fusion-rpg-web/src/lib/bus/world.ts` (new),
    `web/fusion-rpg-web/src/features/world/worldTypes.ts`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W3: Widen `contractGuard` so a feature-local DTO import fails**
  - Description: W2 makes the rule bite for the world; this closes the *class*. The guard scans
    `stages/`, `layers/` and `ui/` and matches only `from "@/lib/bus`, so any future domain that
    parks its DTOs under `features/` reopens the same hole. Widen the scan to catch a DTO import from
    a feature-local module too, per §8e.2's move-**and**-widen decision.
  - Acceptance: a fixture file importing a DTO from `features/` **fails the guard** — this test is the
    module's whole point, and without it §8e.2 is prose again; the guard still passes on every
    shipped `stages/`, `layers/` and `ui/` file; the failure message names the offending import path.
  - Verify: `cd web\fusion-rpg-web; npm test -- contractGuard`, then the full `npm test`.
  - Files: `web/fusion-rpg-web/src/contract/contractGuard.ts`,
    `web/fusion-rpg-web/src/contract/contractGuard.test.ts`.
  - Dependencies: W2.
  - Scope: S.

- [ ] **W4: The six world views, with `Pending` reasons and unit families**
  - Description: `SectorView` (corrected by W1) joined by `LaneView`, `LegionView`, `SlotView`,
    `ForceView` and `TurnEventView`. Every world magnitude carries its unit family in the type so a
    `CostMilli`-shaped 1000× error is unrepresentable upstream; every not-yet-wired field is
    `pending` with **player-facing copy**, not a developer note (`contractGuard.ts:16-46` enforces a
    non-empty reason). `structureId` lands on the TS side, ending a drift invisible to CI since L32.
    Fields declared `pending` here and retired by `world-wire`: `carriedLoam`, member `role`,
    `constructionTurnsRemaining`, `wardenBindingId`, `neglectedTurns`, `pressureMilli`, effective
    capacity, `gateKeyId`, the calendar, the prospected set.
  - Acceptance: **no branded `LoamUnits` type** — the arbitration settled this against the spec's own
    code block, which this task also corrects: whole loam units are a `loamUnits` member on the
    sealed 12-class `UnitClass` union (`contract/types.ts:28-44`), never a third classification;
    all six views compile against the fixture with no `any` and no unchecked cast; every `pending`
    field has a non-empty player-readable reason, asserted by a test that enumerates the world
    fields rather than spot-checking; `ForceView` makes rendering a band as an exact figure
    impossible.
  - **Owner decision:** adding `loamUnits` to `UnitClass` is a change to
    `design/spec-magnitude-and-units.md`'s sealed union — the spec files it **ask-first**, exactly as
    `ladderIndex`, `aptitudePoints` and `reciprocalPoints` were in 2026-08-26. Do not add the member
    without that authorisation; if it is refused, the fallback is mapping loam onto `gameUnits` and
    saying so at the type.
  - Verify: `cd web\fusion-rpg-web; npm test`, then `npm run build`.
  - Files: `web/fusion-rpg-web/src/contract/types.ts`,
    `docs/architecture/world-stage/spec-world-contract.md` (its §Code style block),
    `docs/design/spec-magnitude-and-units.md`.
  - Dependencies: W1.
  - Scope: M.

- [ ] **W5: `adaptWorld*` against the byte-pinned fixture**
  - Description: the six pure adapters, tested against `first-light.json` — which is generated and
    asserted byte-for-byte by `WorldFixtureTests.cs:28-50` — so an adapter and the server cannot
    drift silently. That drift is exactly how `worldTypes.ts` lost `structureId` for two waves.
  - Acceptance: `adaptSector` / `adaptLane` / `adaptLegion` / `adaptSlot` / `adaptForce` /
    `adaptTurnEvent` are pure functions round-tripping the fixture; the **unknown-sector case** is
    covered — an unseen sector serialises every field at its record default
    (`WorldEndpoints.cs:271-277`) and is indistinguishable from a zeroed known one *except by*
    `intel`, so the adapter branches on `intel`, never on emptiness, and a test asserts that; no
    adapter derives a loam number in TypeScript.
  - Verify: `cd web\fusion-rpg-web; npm test -- adapt`, then the full `npm test` and `npm run lint`.
  - Files: `web/fusion-rpg-web/src/contract/adapt.ts`, `web/fusion-rpg-web/src/contract/adapt.test.ts`.
  - Dependencies: W4.
  - Scope: M.

### `world-wire` — the server projections

**Thirteen additions, not nine.** The spec's own nine plus the four the arbitration re-homed here
from `world-targeting`, `world-numbers` and `world-playback`. They are grouped below by DTO so no
task touches more than a handful of files, and **the fixture is re-blessed exactly once**, in W19,
after every field addition has landed — the L25 precedent, where five specs each reopened the same
re-bless budget one field at a time.

- [ ] **W6: `WorldSectorDto` — pressure, warden, neglect, capacity**
  - Description: `PressureMilli` is **declared** (`WorldDtos.cs:72`) and never assigned, though
    `LoamPhases.NextPressure` writes it every turn from fade contagion (`LoamPhases.cs:266-283`,
    called at `:169`) — and the projection's comment at `WorldEndpoints.cs:304-308` was written
    before that feature existed. Assign it; add `WardenBindingId` (`WorldState.cs:173`),
    `NeglectedTurns` (`WorldState.cs:180`) and `LoamCapacity` from `LoamPhases.EffectiveCapacity`
    (`Loam/LoamPhases.cs:58`) — a UI showing `loamStock` has no denominator without it.
  - Acceptance: `WardenBindingId` and `NeglectedTurns` are owner-gated on the `StabilityMilli`
    pattern (`WorldEndpoints.cs:309-311`) — present when `sector.OwnerFactionId == view.FactionId`,
    null otherwise — asserted from **two viewers over the same world**, not one; `PressureMilli`
    carries live contagion state and the stale comment is corrected rather than left contradicting
    the code beneath it; `LoamCapacity` is owner-only like the stock it denominates; every field is
    non-default in a response built from a world where it is non-default in Core.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/` (projection + owner-gating tests).
  - Dependencies: None.
  - Scope: M.

- [ ] **W7: `WorldSlotDto` and `WorldLaneDto` — construction, slot owner, gate key**
  - Description: `ConstructionTurnsRemaining` is read and **discarded** — `WorldEndpoints.cs:300`
    passes it into `Habitability.For` and drops it, so a client sees a sector is habitable but never
    that a structure is three turns out. `GateKeyId` (`WorldState.cs:198`) is hashed at
    `WorldCanonical.cs:47` and absent from both the lane DTO (`WorldDtos.cs:151-162`) and its
    projection (`WorldEndpoints.cs:481-492`). `WorldSlotDto.OwnerFactionId` is declared
    (`WorldDtos.cs:31`) and never assigned.
  - Acceptance: `OwnerFactionId` is projected **from truth, owner-gated** — the spec's decided cheap
    resolution — with **no `RememberedSlot` change** (it is hashed at `WorldCanonical.cs:72-74`) and
    **no state-hash movement**, both asserted rather than assumed; a viewer sees the slot owner on
    ground they hold and null elsewhere; `ConstructionTurnsRemaining` and `GateKeyId` reach a client
    with their XML docs saying who may see them and why.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W8: `WorldEntityDto` — carried loam, member role, supply, and the legion display name**
  - Description: `CarriedLoam` (`WorldState.cs:262`), member `Role` (`WorldState.cs:220`,
    `WorldEntityMemberRole` at `:206-210`), and the supply block — `Capacity` / `Burn` / `Runway`
    (`LegionSupply.cs:20, 24, 32`) plus `TurnsUntilExhausted` (`:46`), which is what the client
    actually needs. None are derivable client-side: they read `LoamPolicy.CarryPerBearer` and
    `BurnPerMember` (`LoamPolicy.cs:91, 94`), which are server tunables, and `world-contract` forbids
    deriving a loam number in TypeScript. **Plus the first re-homed obligation:** a legion **display
    name** — every playback line renders a raw kebab id today (`world-playback`'s ask, arbitration §B).
  - Acceptance: all four supply numbers and both entity fields reach a client and are `long` where
    they are magnitudes; the display name is a projected field, not a client-side prettifier of the
    id; `LegionView`'s `pending` reasons for these fields are retired in the same sweep as W19.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W9: Per-lane march cost for the selected legion** *(re-homed from `world-targeting`)*
  - Description: `world-targeting` needs a route preview with this-turn-vs-later reach and assigned
    the cost to itself; the arbitration moved it here because `LaneCost.For` needs the lane-type
    catalog and the legion's banner element, neither of which is on the wire. Computing it in
    TypeScript would be a private curve, which the one-power-ladder rule forbids.
  - Acceptance: a lane's march cost for a named legion is projected server-side, keyed so the client
    reads a cost per (lane, legion) without arithmetic of its own; no cost is derived in TypeScript;
    the projection is empty rather than wrong when no legion is selected.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W10: The `LoamUpkeep` operand breakdown** *(re-homed from `world-numbers`)*
  - Description: `WorldSectorDto` carries totals only, and `world-numbers`' nested lockable modifier
    ledger cannot decompose what it is not sent. Project the operands behind the upkeep number, in
    the order the engine applies them, so the ledger shows a derivation rather than a result.
  - Acceptance: each operand carries its own label and value; the operands recombine to the total
    exactly, asserted by a test rather than trusted; whole loam units are `long` and no field carrying
    them is named `…Milli`; owner-gated like the reading it decomposes.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `python scripts\audit-overflow.py`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W11: The `supply.restored` engine line** *(re-homed from `world-playback` and ideal §2.3)*
  - Description: `supply.cut:` exists and nothing reports the reverse, so a legion's supply comes back
    silently. `recovery:` is a garrison mending, not this. Emit `supply.restored` from
    `LegionSupply` at the point the cut is lifted, carrying `Audience = entity.OwnerFactionId` so it
    reaches its owner under W14's rule.
  - Acceptance: a legion whose supply is cut and then restored produces exactly one
    `supply.restored` entry, on the turn it is restored and not on subsequent turns; the line reaches
    its owner and no one else; **`GoldenFinalHash` is unchanged** — the report is not hashed
    (`StateHasher.cs:17` hashes `WorldCanonical.Write(world)` only), asserted not assumed.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`, then
    `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `src/FusionRpg.Core/World/Loam/LegionSupply.cs`, `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W12: `TurnReportEntry.Audience` and the faction-scoped emitters — fog defect A**
  - Description: `VisibleTo` returns `true` for a null `SectorId` (`WorldEndpoints.cs:215-219`), so
    four production sites leak to every viewer: every battle line (`BattleReporting.cs:36`),
    `legion.topup` (`LegionSupply.cs:98`), `loam.handicap` (`LoamPhases.cs:119`) and
    `loam.shortfall.unresolved` (`LoamPhases.cs:141`). Add one optional member —
    `string? Audience` on `TurnReportEntry` (`TurnReport.cs:24-25`) with a matching optional
    parameter on `TurnReport.Add` (`:63-64`) — set by the faction-scoped emitters to
    `faction.FactionId`; and make `BattleReporting.cs:36` pass `request.LocationId`, which it already
    has in hand and passes to `ClearGuard` two lines above.
  - Acceptance: the three faction-scoped emitters set `Audience`; battle lines carry a real sector
    id; **an old `report_json` row with no `Audience` still deserializes** and reads as "no audience",
    which is the behaviour those rows had — this is the `stance`-shaped regression on the read path
    and it is tested, not assumed; `RulesetVersion` is untouched by this task.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`, then
    `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `src/FusionRpg.Core/World/Turn/TurnReport.cs`,
    `src/FusionRpg.Core/World/Turn/BattleReporting.cs`,
    `src/FusionRpg.Core/World/Loam/LoamPhases.cs`, `src/FusionRpg.Core/World/Loam/LegionSupply.cs`,
    `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W13: `MovementPhase` stops putting non-sectors in the sector slot — fog defect B**
  - Description: three sites, not the two §2.2 counted (§8c.3 corrected it): `:105` passes
    `outcome.AtSectorId ?? outcome.OnLaneId`; `:123-124` schedules `Arrival` with
    `ArrivedAtSectorId ?? OnLaneId ?? ""`; `:195` passes `evt.Detail` straight into the sector slot,
    and a `Halt`'s detail is `"zoc:" + outcome.AtSectorId` (`:127-128`), which is not a sector id.
    `Believed("l-c1-c2")` returns null, so these lines vanish for **everybody** — the client's `halt`
    keyframe (`turnPlayback.ts:38`) can never fire against a live server.
  - Acceptance: all three sites pass `AtSectorId` when there is one and **null** otherwise, with the
    lane id staying in `Detail` where the client already reads it; each carries
    `Audience = entity.OwnerFactionId`, because a line about a legion mid-lane is a dynamic fact
    about the viewer's own force; a test asserts a `halt` line **reaches its owner at all** — today
    it reaches nobody.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Core/World/Movement/MovementPhase.cs`, `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: W12.
  - Scope: S.

- [ ] **W14: `VisibleTo` as W-F1's three named clauses — fog defect C**
  - Description: today the filter gates on "have I ever seen this sector", so ground scouted on turn 6
    still reports live battles on turn 80 — contradicting §4.9's static-vs-dynamic rule that the
    four-state ladder already supports (`StateOf` returns `Watched` exactly when the faction sees it
    this turn, `FactionIntel.cs:133-135`, and this endpoint already calls it at
    `WorldEndpoints.cs:265`). Rewrite the filter as **three named rules** — audience, live sight for a
    dynamic fact, remembered sight for a static one — because §8c.3's decision about what an opponent
    may leak is the thing a future reader needs to find. The static kinds are a **closed, named list**,
    never a prefix guess on free text.
  - Acceptance: three named tests, one per defect — a `loam.handicap` line reaches its own faction and
    **not** the other (A); a `halt` line reaches its owner (B); a battle on ground scouted long ago
    and not currently seen is **withheld** while a claim on the same ground is **shown** (C, and the
    static/dynamic split in one assertion); the endpoint's own doc records W-F1 and the stated
    limitation that `believed` is built from current state (`WorldEndpoints.cs:159`), which errs
    toward showing more, never less.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.E2E.Tests`.
  - Files: `src/FusionRpg.Server/WorldEndpoints.cs`, `tests/FusionRpg.Server.Tests/`,
    `tests/FusionRpg.E2E.Tests/`.
  - Dependencies: W12, W13.
  - Scope: M.

- [ ] **W15: `WorldCalendarDto` on `WorldStateDto`**
  - Description: the calendar is on **neither** `WorldStateDto` (`WorldDtos.cs:193-202`) nor
    `WorldHeaderDto` (`:8-16`), and a client cannot derive it: `DaysPerWeek` and `WeeksPerMonth` are
    server tunables (`TurnCalendar.cs:22-23`) and the roll needs the seed, which is deliberately
    absent from every projection (`WorldDtos.cs:3-7`). The arbitration puts it on **`WorldStateDto`**,
    not the header: the state route is what the stage polls every turn, and the header is a listing
    shape whose `CurrentTurn` already answers what a listing needs. **The report-entry alternative is
    wrong on its own terms** — `TurnEngine.cs:225-231` emits calendar entries only on a week
    boundary, so that slot would be blank on 6 of every 7 turns.
  - Acceptance: `WorldStateDto` carries the **current turn's roll only**
    (`TurnCalendar.Roll(world.CurrentTurn, seed)`, `TurnCalendar.cs:31`) plus `DaysPerWeek` and
    `WeeksPerMonth`, so the client can place today in the week and month without arithmetic; **no
    seed and no future roll leave the server**, asserted by a test — the calendar is pure in
    `(turn, seed)` and both together would let a client enumerate the campaign's plague months;
    `world-hud`'s spec is corrected to read from here rather than from report entries.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.E2E.Tests`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`, `docs/architecture/world-stage/spec-world-hud.md`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W16: `WorldStateDto.ProspectedSectorIds`**
  - Description: `Prospecting.Reveal` is implemented, returns `IReadOnlySet<string>`
    (`IntelRecorder.cs:179`), reaches four lanes (`:174`) and no DTO carries it. Compute it at
    projection time, the shape `Lifelines` already uses (`WorldEndpoints.cs:382-396`). Unlike
    lifelines it is **not** opt-in: `Reveal` skips every entity whose stance is not `"dowse"`
    (`:187`), so with no dowser the cost is one pass over a list of three. It is inert until
    `world-commands` makes `dowse` orderable (W30), and correctly so — the set is simply empty.
  - Acceptance: the set is **separate, never merged into `intel`** — a dowser answers one narrow
    question and leaks no owner, no danger band and no forces (`IntelRecorder.cs:160-166`), so
    folding it in would silently promote an unknown sector to scouted, and a test asserts a
    prospected sector's `intel` is unchanged; with no dowser the set is empty and the response shape
    is unchanged.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W17: `GET /api/world/catalog`**
  - Description: `StructureCatalog.All` (`StructureCatalog.cs:53`), `SlotTypeCatalog.All`
    (`SlotTypeCatalog.cs:54`) and `StrengthBandCatalog.All` (`Intel/StrengthBandCatalog.cs:35`) are
    public with **no HTTP caller**, so a UI cannot learn what is buildable, what a slot letter means,
    or what `"warband"` is worth. One route, no world id, no viewer, no fog — these are rules, not
    state: structures (id, name, kind, required slot kind, cost, yield multiplier, capacity bonus),
    slot types, strength bands (index, name, floor, ceiling) and lane types.
  - Acceptance: the structure cost field is named **`Cost`**, not `CostMilli`, and its XML doc says
    "whole loam units" in words — `StructureDef.CostMilli` (`StructureCatalog.cs:26`) holds whole
    units and is compared directly against `CarriedLoam` at `BuildResolver.cs:101` and subtracted at
    `:115`, so a renderer trusting the name is wrong by 1000× and GG-46 is a Tier-1 gate; renaming
    the Core constant stays out of scope; the route answers without a world or a viewer.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`, then
    `python scripts\audit-magic-numbers.py --summary`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.E2E.Tests/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W18: The AI-reasons projection becomes developer-tree-only** *(arbitration §C, §8.3)*
  - Description: the orphan the coverage audit found — §8.3 says the AI-reasons panel moves to the
    developer tree, two specs cite it as background, neither owns the move, and
    `WorldEndpoints.cs:185-196` is untouched by all fifteen specs. That projection hands a client
    `Reason` for **every** logged command, including an opponent's, which is one of the two channels
    §8c.3 wants closed alongside the fog fix. Gate it the way the world's other developer surfaces
    already are (`FUSIONRPG_SIM=1`, the precedent at `POST /api/test/world/create`). **The dev-tree
    surface itself is out of scope** — this task closes the leak, it does not build the panel.
  - Acceptance: an ordinary client no longer receives another commander's `Reason`; with the
    developer gate on, the projection is unchanged, asserted by a test in both modes; nothing the
    viewer's **own** commands carry is removed.
  - **Owner decision:** whether a foreign commander's entry disappears entirely from `Commands` or
    only loses its `Reason`. The spec assigns the move and not its granularity; the cheaper reading
    (drop `Reason`, keep the entry) is the default if no answer comes, and the task records which was
    chosen.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Server/WorldEndpoints.cs`, `tests/FusionRpg.Server.Tests/`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W19: Re-bless `first-light.json` once, and sweep its seven consumers**
  - Description: **the single re-bless for every field addition in W6–W11 and W15–W16.** This is the
    L25 precedent stated as a task: `decisions.md` already records six hashed field additions batched
    into one golden re-bless *after an adversarial audit caught five specs each independently
    reopening the same budget one field at a time*. `first-light.json` is byte-pinned
    (`WorldFixtureTests.cs:17, 48-49`) and consumed by **seven** files — `e2e/world.spec.ts`,
    `SectorFog.test.tsx`, `SectorNode.test.tsx`, `SectorPanel.test.tsx`, `WorldPage.tsx`,
    `worldSelection.test.ts`, `worldViewModel.test.ts`. The sweep is mechanical (additive fields,
    nothing existing changes shape) but it is not zero.
  - Acceptance: the re-bless runs **once**, deliberately, with
    `$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1"`; all seven consumers are green afterwards and the old
    `#/world` route still renders — the map's assumption 2 requires it; **no world state golden
    moves** — `WorldWaveOneAcceptanceTests.GoldenFinalHash`
    (`tests/FusionRpg.Data.Tests/WorldWaveOneAcceptanceTests.cs:123`) is asserted unchanged, because
    this module changes no Core state field and `TurnReportEntry.Audience` is not hashed; no task
    after this one in Phase 0 re-opens the budget.
  - Verify: `$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1"; dotnet test tests\FusionRpg.E2E.Tests` once,
    then `dotnet test tests\FusionRpg.Data.Tests` and `cd web\fusion-rpg-web; npm test`.
  - Files: `web/fusion-rpg-web/src/features/world/fixtures/first-light.json`, and the seven consumers
    named above (test expectations only).
  - Dependencies: W6, W7, W8, W9, W10, W11, W15, W16.
  - Scope: M.

- [ ] **W20: `first-light-turn.json` — the turn-report golden**
  - Description: no turn-report fixture exists; `world.spec.ts:91` stubs
    `**/api/world/first-light/turn/**` as a flat 404, so `world-playback` — which owes a table for 21
    event prefixes, 3 battle kinds, 2 calendar subjects and **37** drop reasons — has nothing to build
    against. Copy the pattern that already works: drive the live API after playing a scripted handful
    of turns, serialize with `WriteIndented`, assert byte-for-byte, re-bless under
    `FUSIONRPG_BLESS_WORLD_FIXTURE=1` (`WorldFixtureTests.cs:28-50, :42`).
  - Acceptance: the file is **`first-light-turn.json`** — the arbitration settled the name against
    `turn-report.json`, and `world-playback` consumes this exact path, so a mismatch would meet only
    as a missing import; it sits beside `first-light.json` and is byte-pinned by a test in that
    pattern; the scripted turns produce **at least one entry of each of W14's visibility classes** —
    an own-audience economy line, a live-sight battle, a remembered-sight claim, and one line the
    viewer must **not** see (the rule is only tested if the fixture contains something it excludes);
    and **a `halt` line that actually appears** — the keyframe `turnPlayback.ts:38` recognises and has
    never once received.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldTurnFixture`,
    then `cd web\fusion-rpg-web; npm test`.
  - Files: `tests/FusionRpg.E2E.Tests/WorldTurnFixtureTests.cs` (new),
    `web/fusion-rpg-web/src/features/world/fixtures/first-light-turn.json` (new),
    `web/fusion-rpg-web/e2e/world.spec.ts`.
  - Dependencies: W14, W19.
  - Scope: M.

- [ ] **W21: The two fixtures the plan assumes nobody owns**
  - Description: the plan's risk table names an **18-sector / 10-legion** fixture and a
    **`two-hearths`** fixture and assigns both here, because `world-wire` owns the generator that
    already produces the shipped one. `two-hearths` is Gate B's playtest world
    (`WorldTemplateCatalog.cs:16`, medium tier) and has no web fixture; the 18/10 fixture is what
    sizes the outliner against §8e.3's ~28 rows and the two available map tiers, and is the only
    fixture that proves a collection surface is bounded rather than assumed bounded.
  - Acceptance: both are generated by the same byte-pinning test pattern, never hand-written; the
    `two-hearths` fixture is a real `GET /api/world/{id}/state` response for that template; the 18/10
    fixture reaches the medium tier's sector ceiling and carries ten legions; neither re-opens
    `first-light.json`'s re-bless — they are new files, so W19's budget stays closed.
  - **Owner decision:** whether the 18/10 fixture comes from an authored template addition or from a
    SIM-created world seeded through `POST /api/test/world/create`. The second costs nothing in Core
    and is the default if no answer comes; the first is a `WorldTemplateCatalog` change and would
    need its own validation pass.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldFixture`, then
    `cd web\fusion-rpg-web; npm test`.
  - Files: `tests/FusionRpg.E2E.Tests/WorldFixtureTests.cs`,
    `web/fusion-rpg-web/src/features/world/fixtures/two-hearths.json` (new),
    `web/fusion-rpg-web/src/features/world/fixtures/` (the 18/10 fixture, new).
  - Dependencies: W19.
  - Scope: M.

### `world-commands` — the write surface

- [ ] **W22: `Amount` and `StructureId` through all six round-trip sites**
  - Description: `sustain` is blocked twice over — `WorldCommandRequest` has no `Amount`
    (`WorldDtos.cs:205-217`) so admission refuses `amount.invalid`
    (`WorldCommandAdmission.cs:63`), and `CommandPayload` (`RpgStore.WorldTurns.cs:442-444`) does not
    persist it, so the order comes back amountless when `TurnEngine.cs:134` re-admits it from the log.
    `WorldCommand` already has both (`WorldCommand.cs:76, :79`) and both resolvers are wired
    (`TurnEngine.cs:214, :280`). **The gap is the DTO and the payload, and there are six sites, not
    §8c.4's five.** The sixth is the silent one: `ListWorldCommandsUnlocked` (`:679`) does **not**
    call `ReadCommandRow` despite that method's own doc saying it is shared by both listers
    (`:643-646`) — it inlines the deserialize at `:697-709`, and it is the site the engine re-admits
    from at `:507`. A field added to the other two and missed here survives every listing a client
    sees and vanishes at the moment the turn resolves.
  - Acceptance: all six sites carry both fields; `ListWorldCommandsUnlocked` **calls
    `ReadCommandRow`**, making the comment true and reducing six sites to five for the next field;
    `Amount` is `long` end to end with no `int` introduced anywhere on the path; a `sustain` with an
    `Amount` and a `build` with a `StructureId` are read back through all three hydration paths
    (`:400`, `:430`, `:679`) still carrying both; a `payload_json` row written before this change
    still deserializes with both null.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`, then `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`
    and `python scripts\audit-overflow.py`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs`, `tests/FusionRpg.Data.Tests/`.
  - Dependencies: None.
  - Scope: M.

- [ ] **W23: The property test — every kind × every optional member survives the round trip**
  - Description: **the plan calls this the single highest-value test in Phase 0**, because it closes
    the defect *class* rather than the two known instances. `stance` was lost on this exact trip once
    already, and `ReadCommandRow`'s own comment (`RpgStore.WorldTurns.cs:437-441`) says adding a field
    to `WorldCommand` and forgetting it there "loses it in the round trip and the order comes back
    malformed." So instead of two hand-written cases, enumerate: for **every kind in
    `WorldCommandKinds.All`** × **every optional member of `WorldCommand`**, build the command, submit
    it, list it back through all three hydration paths, and assert equality.
  - Acceptance: the test derives its matrix from `WorldCommandKinds.All` and `WorldCommand`'s members
    by reflection or an exhaustive switch that fails to compile when a member is added — never a
    hand-maintained list, which is the failure mode it exists to catch; a deliberately dropped field
    in any one hydration path makes it red (verified once by hand, not committed); it runs in
    `Data.Tests`, which owns the boundary.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~WorldCommand`, then
    the full `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `tests/FusionRpg.Data.Tests/` (new test file).
  - Dependencies: W22.
  - Scope: M.

- [ ] **W24: The `cede` command kind and its admission arm**
  - Description: `LoamPhases.Pressure` picks the sector to release **itself**, every turn, via
    `LoamForecast.Weakest` (`LoamPhases.cs:133-146`, the call at `:138`); there is no `abandon` /
    `cede` / `release` kind (`WorldCommand.cs:36-37`). §8c.2 named that as the economy's core tension
    existing as a notification rather than a decision, and plate 11 §K.4's *"Give up Hollowmoor
    instead"* is a lie until this lands. `WorldCommandKinds.Cede = "cede"` names a sector and needs no
    entity; the admission arm follows `Claim`'s shape (`WorldCommandAdmission.cs:54-58`) with an
    ownership check instead of an entity one.
  - Acceptance: `cede` is in `WorldCommandKinds.All` with a one-sentence doc saying what the *player*
    is doing; admission refuses a sector this faction does not own and an unknown sector
    (`:45-46`), each with its own reason string; a `cede` order changes no hash **by existing** —
    `WorldCanonical` never hashes commands (`WorldCanonical.cs:30-90`).
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`, then
    `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `src/FusionRpg.Core/World/Turn/WorldCommand.cs`,
    `src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs`, `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W25: Thread the cede preference into the one `Weakest`**
  - Description: §8c.6 lists as **load-bearing** that *"warning and act share `Weakest`, so the
    forecast and the event cannot disagree"* — today they cannot, because `LoamPhases.cs:138` and
    `LoamForecast.cs:62` call the same function. §8d.2 requires that survive: **the player's choice is
    an *input* to `Weakest`, never a second code path.** `Weakest` gains one `ceded` parameter and one
    clause; `LoamPhases.Pressure` takes a `faction id → ceded sector id` map; `TurnEngine.Pressure`
    (`TurnEngine.cs:210-218`) builds it from the turn's commands exactly as it already derives
    `postures` from `stance` orders at `:285-288` — a **plain map**, passed down, never a service or a
    lookup.
  - Acceptance: there is still exactly **one** function answering "which sector fades"; the player's
    choice wins only where the engine could have chosen it anyway — in this component and not warded
    — so ceding warded ground, foreign ground, or a sector in another component simply is not a
    candidate and the default ordering answers, each covered by its own refusal test; a component
    that covers its upkeep releases nothing.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Core/World/Loam/LoamForecast.cs`,
    `src/FusionRpg.Core/World/Loam/LoamPhases.cs`, `src/FusionRpg.Core/World/Turn/TurnEngine.cs`,
    `tests/FusionRpg.Core.Tests/World/Loam/`.
  - Dependencies: W24.
  - Scope: M.

- [ ] **W26: The forecast reads the same preference on the `/state` route**
  - Description: `WorldEndpoints.ComputeLoamReading` (`:420-461`) calls `LoamForecast.WillRelease` at
    `:455`, and unless it passes the same preference, `WillReleaseNextTurn` (`WorldDtos.cs:125`)
    starts naming a different sector than the turn will release. **Corrected 2026-09-03 by audit:**
    the pending orders *are* reachable via `store.ListLoggedWorldCommands` at
    `WorldEndpoints.cs:185` — but that call sits in `MapTurns`' `GET /{worldId}/turn/{turn}` handler,
    while `ComputeLoamReading` runs on `/state`. Threading it needs a **new store read on the state
    route**, not a nearby call reused.
  - Acceptance: a test over a world with a cede order filed asserts `WillRelease` and the sector
    `Pressure` actually fades are the **same id** — that test is the whole reason this design has one
    function instead of two; the new store read is a `RpgStore` call, with **no SQL outside
    `FusionRpg.Data`**; `/state` costs one extra read per request and no more.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `.\scripts\guard-dal.ps1` and
    `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Server/WorldEndpoints.cs`, `tests/FusionRpg.Server.Tests/`.
  - Dependencies: W25.
  - Scope: M.

- [ ] **W27: `RulesetVersion` — read the current value, bump it once, triage before re-blessing**
  - Description: `LoamPhases.Pressure`'s behaviour changes in W25, so per §8d.2 this is a
    `RulesetVersion` decision. All three additions in this module (`cede`, `bind-warden`, `dowse`)
    land under **one** bump, per `decisions.md:98`: *"`RulesetVersion` advances **once** for the
    combined move."* **Read `TurnEngine.RulesetVersion`'s current value and add one — do not hard-code
    6** — because `sector-development` (`world-map` Phase 12) takes the value onward afterwards, and
    the plan's ordering only holds if the second bumper reads rather than assumes.
  - Acceptance: `RulesetVersion` is the previous value plus one, derived from what the file says at
    the time of the change; **`GoldenFinalHash` is unchanged** with no cede order filed
    (`tests/FusionRpg.Data.Tests/WorldWaveOneAcceptanceTests.cs:123`, asserted `:323`) — the world
    hash is over `WorldCanonical.Write(world)` only (`StateHasher.cs:17`) and `RulesetVersion` is not
    in it; **a moved hash on a scenario that files no cede order is triaged as a defect in the
    preference threading, never re-blessed** — `decisions.md:103` is the exact precedent, where
    buff-debuff-scope moved this same golden at a neutral default and the fix was the code, with zero
    goldens moved in the shipped version; report re-derivation across the version boundary refuses
    rather than fabricates, as `RpgStore.WorldTurns.cs:592` already does.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`, then `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `src/FusionRpg.Core/World/Turn/TurnEngine.cs`, `tests/FusionRpg.Data.Tests/`.
  - Dependencies: W25.
  - Scope: S.

- [ ] **W28: The `bind-warden` command kind and `WardResolver`**
  - Description: `WorldSector.WardenBindingId` (`WorldState.cs:173`) is read by `LoamForecast.cs:24`
    and `LoamPhases.cs:162`, hashed at `WorldCanonical.cs:37`, persisted at `RpgStore.World.cs:441`
    and cleared on capture at `ClaimResolver.cs:85` — and set non-null **nowhere in production**; the
    only writers are `LoamTextureTests.cs:355, 378, 413, 430`. The kind is **`bind-warden`**, not
    `ward`: the arbitration keeps `ward` for the **lane** action that raises
    `WorldLaneDto.WardLevel` and stays unbuilt, and the collision was already repaired once in plate
    11 — it must not return through a task title. `WardenResolver` lives in `Movement/` and resolves
    in `Snapshot` beside `Claim` and `Build` (`TurnEngine.cs:274-280`), because ownership is only
    settled once the turn has run and a binding on ground you lost this turn must not stick.
  - Acceptance: the kind is `bind-warden` and the spec's own text is corrected in the same change; a
    warded sector is excluded from `Weakest` (`LoamForecast.cs:24`) and neither fades nor recovers
    (`LoamPhases.cs:162`); capture clears the binding; **the first production path that can change a
    hash without changing a number** is named in a comment where `WorldCanonical.cs:37` emits the
    cell, and no existing golden moves because no shipped scenario binds anything.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`, then
    `dotnet test tests\FusionRpg.Data.Tests`.
  - Files: `src/FusionRpg.Core/World/Turn/WorldCommand.cs`,
    `src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs`,
    `src/FusionRpg.Core/World/Movement/WardenResolver.cs` (new),
    `src/FusionRpg.Core/World/Turn/TurnEngine.cs`, `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: W24.
  - Scope: M.

- [ ] **W29: `POST /api/world/{worldId}/bind-warden` — the first production `BindAsWarden` call site**
  - Description: `FusionRpg.Core.csproj` declares exactly one `ProjectReference` —
    `FusionRpg.Contracts` — and a guard substring-scans it for the data project's name, so Core
    **cannot** call `RpgStore.BindAsWarden` (`RpgStore.Contracts.cs:283`). The orchestration lives in
    the Server layer, which references both: bind the contract, then submit the `bind-warden` order
    through the ordinary command path. `/api/contracts/bind` calls the *ordinary* `BindContract`
    (`ContractEndpoints.cs:31`), so this is `BindAsWarden`'s first production caller. Capacity, the
    soul fee and the non-releasable flag are shipped (`:310-323`) and read as-is.
  - Acceptance: **the two-step failure mode is documented at the endpoint, not engineered around** —
    if step 2 fails, step 1 is not rolled back and the player holds a non-releasable binding with no
    sector; what makes that tolerable is that step 1 is **idempotent**, returning
    `("replay", existing)` for an already-bound instance (`:301-305`), so the correct client response
    is to retry the whole call; a test simulates a step-2 failure, retries, hits the replay path and
    lands the order; `guard-dal.ps1` is green — this is the first place in the world stack where a
    Core concept and a store call meet, and the Server layer is where a guard enforces that.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `.\scripts\guard-dal.ps1` and
    `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `src/FusionRpg.Server/WorldWardenEndpoint.cs` (new),
    `src/FusionRpg.Contracts/WorldDtos.cs`, `tests/FusionRpg.Server.Tests/`.
  - Dependencies: W28.
  - Scope: M.

- [ ] **W30: The `dowse` stance and its missing `BudgetFor` arm**
  - Description: §2.2 called prospecting *"blocked by one line"*; §8c.4 corrected it to four, two of
    which are `world-wire`'s (W16). This task does the two that are here.
    `MovementPolicy.Stances` (`Movement/LaneCost.cs:13`) is `{ March, Scout, Hold }`, so admission
    refuses `stance.unknown` at `WorldCommandAdmission.cs:51`. And **`BudgetFor` (`LaneCost.cs:38-42`)
    has arms for `Hold` and `Scout` and a `_` default returning `PointsPerTurn` (`:23`) — a dowser
    would silently receive the full march budget.** That is the half of the defect no test catches by
    observing that the order was accepted.
  - Acceptance: `MovementPolicy.Dowse == Prospecting.DowserStance` (`IntelRecorder.cs:176`) is
    asserted by a test — one string, not two, or a dowser passes admission and reveals nothing
    (`Reveal` matches on it at `:187`); a `dowse` stance order is **admitted** where today it is
    refused; `BudgetFor("dowse")` returns the tuned budget rather than falling through to
    `PointsPerTurn`; the number is **`movement.dowseBudgetMilli` in `data/tuning/world.v2.json`, not
    a `const`** — published with `python tools\tuning\publish.py`, never hand-edited, leaving
    `world.v1.json` as the revert target; `MovementPolicy`'s existing `const` budgets are
    pre-existing debt this task neither inherits nor fixes.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`, then
    `python scripts\audit-magic-numbers.py --summary` (no new balance literal on this module's files).
  - Files: `src/FusionRpg.Core/World/Movement/LaneCost.cs`,
    `src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs`, `data/tuning/world.v2.json`,
    `tests/FusionRpg.Core.Tests/World/`.
  - Dependencies: W24.
  - Scope: S.

---

### Gate A — the seam holds

Nothing above level 1 is safe to build before every box below is ticked. Drawn from the plan's Gate A
paragraph and the map's own Gate A.

- [ ] The **`typeId` ADR** is recorded in `decisions.md` **with its contract version bump** (W1).
- [ ] **`contractGuard` catches a feature-local DTO import** — proven by a test, not by prose (W3).
- [ ] **All nine `world-wire` additions plus the four re-homed obligations reach a client** (W6–W11,
      W15–W16), and the **fixture is re-blessed once** for all of them (W19).
- [ ] **A command of every kind survives the reveal round-trip** — the property test over
      `WorldCommandKinds.All` × every optional member of `WorldCommand`, green (W23), and a `sustain`
      submitted end-to-end raises the sector's stock.
- [ ] **`first-light-turn.json` exists** under that name and carries **one entry of each visibility
      class**, plus a `halt` line that actually appears (W20).
- [ ] **The fog fix is asserted at all three sites** — one named test per defect (W12, W13, W14).
- [ ] `TurnEngine.RulesetVersion` is the previous value **plus one**, read not hard-coded, and
      `GoldenFinalHash` is **unchanged** — verified before any re-bless was considered (W27).
- [ ] `#/world` still renders against the re-blessed fixture; the three standing exemptions are
      untouched (they retire in Phase 4, not here).
- [ ] All five .NET suites green: `dotnet test tests\FusionRpg.Core.Tests`,
      `...\FusionRpg.Data.Tests`, `...\FusionRpg.Server.Tests`, `...\FusionRpg.E2E.Tests`,
      `...\FusionRpg.Guard.Tests`.
- [ ] Web green: `cd web\fusion-rpg-web; npm test`, `npm run build`, `npm run lint`.
- [ ] The four boundary guards green: `.\scripts\guard-single-writer.ps1`,
      `.\scripts\guard-secondary-no-unity.ps1`, `.\scripts\guard-funnel-delta.ps1`,
      `.\scripts\guard-dal.ps1`.
- [ ] Both audits green: `python scripts\audit-overflow.py`,
      `python scripts\audit-magic-numbers.py --summary` — no new balance literal on Phase 0's files.
- [ ] **Commit message draft handed to the owner**, with the paths touched. Git stays hands-off — the
      work is left in the tree and the owner commits.
- [ ] **Owner review before Phase 1.**

# Tasks: world stage — Phases 1 and 2

Plan: [world-stage-plan.md](../../../../../../../tasks/world-stage-plan.md) ·
Map (**the arbiter**): [world-stage-map.md](../../../../../../../docs/architecture/world-stage-map.md) ·
Specs: `docs/architecture/world-stage/spec-world-{shell,numbers,render,hud,inspector,targeting,playback}.md`.

**Numbering has a deliberate gap.** Phase 0 (`world-contract`, `world-wire`, `world-commands`) ends
around **W12–W16**; this file starts at **W20** so Phase 0 can grow by a few tasks without renumbering
anything downstream. W17–W19 are intentionally unused.

**Standing rules for every task below.**

- **Nothing here deletes `features/world/`.** `#/world` keeps working until the Phase 4 retirement
  task. The pure layer (`worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`,
  `commanderIntent.ts`, both fixtures) **moves** to `stages/world/` at its consuming phase — the map's
  arbitration row, which wins over `world-shell` SC7.
- Every task leaves `npm test`, `npm run build` and `#/world` green. No task commits — git is the
  owner's (AGENTS.md).
- Where a module spec disagrees with the map's arbitration section, **the map wins**, and the task
  says which row it is following.
- Verification commands are literal: `cd web\fusion-rpg-web; npm test` · `npm run build` ·
  `npm run lint` · `npm run test:e2e` · `dotnet test tests\FusionRpg.E2E.Tests`.

---

## Phase 1 — the map is a place

Order: `world-shell` and `world-numbers` in parallel (level 2), then `world-render` and `world-hud`
in parallel (level 3). Every task in this phase depends on Gate A having passed.

### `world-shell`

- [ ] **W31: The camera as pure data — `viewBox` state, pan, zoom-about-pointer, fit**
  - Description: the whole navigation model as one `Camera = {x, y, w, h}` and pure functions over
    it, with no DOM anywhere. This is a unit because it is the piece every gesture, every zoom tier
    and every fit control resolves to, and because `worldViewModel.ts` is already library-agnostic
    (plain `{x,y}` from the authored grid at `:9-11`, applied at `:287-345`) — there is no auto-layout
    to replace, so the camera is the entire port. Extent is the bounding box of the authored grid plus
    a padding margin, never a layout pass.
  - Acceptance: `zoomAbout` keeps the pointed-at world coordinate fixed (one assertion, and it is the
    whole correctness of wheel zoom); `fitToExtent` puts the full extent on screen with padding at
    both 1280×720 and 1440×900; `MIN_SCALE`/`MAX_SCALE` clamp both directions and carry a comment
    saying they are **structural, not tunable** (they change whether the control works, not how the
    game feels — `tunables-ssot.md`'s own test); no import of `react`, `@xyflow/react` or `document`
    in `camera.ts`.
  - Verify: `cd web\fusion-rpg-web; npm test -- camera`; `npm run build`.
  - Files: `src/stages/world/camera.ts`, `src/stages/world/camera.test.ts`.
  - Dependencies: Gate A (`world-contract`).
  - Scope: S.

- [ ] **W32: Gestures — wheel, drag, arrow keys, fit — all driving one camera**
  - Description: map raw events onto camera ops, with the drag-threshold split that separates *pan on
    empty map* from *select on a node*. One meaning per gesture: the page cannot scroll, so the wheel
    has no second interpretation to disambiguate — which is the fix for the two-scroll-model defect
    (`@xyflow/react` defaults `preventScrolling = true` and `WorldPage.tsx` never sets it, so today the
    same wheel gesture means two things depending on pointer position).
  - Acceptance: wheel → zoom about the pointer; drag on empty map → pan; drag beginning on a node →
    select, not pan; **arrow keys pan by a fixed step and `W` is not bound to anything** (the map's
    arbitration row: *arrows pan, `W` cycles* — `WASD` was removed on 2026-09-03 for exactly this
    collision and a test asserts `W` reaches no camera op); `fit` shows the full extent.
  - Verify: `cd web\fusion-rpg-web; npm test -- cameraGestures`.
  - Files: `src/stages/world/cameraGestures.ts`, `src/stages/world/cameraGestures.test.ts`.
  - Dependencies: W31.
  - Scope: S.

- [ ] **W33: `WorldStage` under `StageHost`, with the DOM id scheme it owes the renderer**
  - Description: the stage component — `StageHost` + `useStageMountGuard("world")` + one `<svg>` whose
    `viewBox` is the camera and whose children are a slot for a scene (this module draws nothing).
    It ships with `stageIds.ts`, which is not incidental: `LegionMarker` animates along a `<path>` by
    id (`getElementById` `:46` → `getTotalLength()` `:50` → `getPointAtLength()` `:55`), and that id is
    documented as *"the `<path>` element id React Flow gives this lane's edge"* (`:17-18`). **Removing
    the library removes the supplier with no compile error and no runtime error — markers simply stop
    moving.** `stageIds.lanePath(laneId)` is the replacement contract; `world-render` honours it in
    W33 and the shared assertion lands in W34.
  - Acceptance: the stage renders under `StageHost` and `getStageMountCount("world")` stays at **1**
    across a band-2 layer opening and closing (GG-11); `stageIds.ts` exports `lanePath(laneId)` with
    the migration risk written next to it; the stage is reachable on a temporary route without
    touching `#/world`; nothing in `stages/world/` imports a REST DTO.
  - Verify: `cd web\fusion-rpg-web; npm test -- WorldStage`; `npm run build`.
  - Files: `src/stages/world/WorldStage.tsx`, `src/stages/world/WorldStage.test.tsx`,
    `src/stages/world/stageIds.ts`, `src/app/routes.tsx`.
  - Dependencies: W31, W32.
  - Scope: M.

- [ ] **W34: The page stops scrolling — a non-scrolling outlet mode for stage routes**
  - Description: the defect stated with its numbers: `WorldPage.tsx:222` sizes the canvas `h-[620px]`
    inside `src/app/AppShell.tsx:30`'s `<main className="min-w-0 flex-1 overflow-auto p-5">`. At the
    1280×720 floor the header band takes ~70px and `p-5` takes 20px top and bottom, so the outlet has
    ~680px for a 620px map plus a banner, a title and a control column — **the map's bottom edge is
    below the fold with an empty world**, and `overflow-auto` is the failure GG-36 forbids dressed as
    a feature. The fix is that a stage is measured against the viewport: the outlet gets a
    route-scoped non-scrolling, unpadded mode, and the map's extent is the camera's problem.
  - Acceptance: on the stage route, `document.scrollingElement.scrollHeight` equals the viewport
    height at **1280×720 and 1440×900**, and no element reports horizontal overflow; Sanctum and Lawn
    render byte-identically (their existing tests pass with no edits — this mode is route-scoped, so
    it is not the AppShell-layout change `world-shell`'s **Ask first** covers); the e2e sweep asserts
    both viewports.
  - Verify: `cd web\fusion-rpg-web; npm test`; `npm run test:e2e`.
  - Files: `src/app/AppShell.tsx`, `src/app/routes.tsx`, `src/app/AppShell.test.tsx`,
    `e2e/world-stage.spec.ts`.
  - Dependencies: W33.
  - Scope: M.

- [ ] **W35: Esc and right-click pop one layer — and `select-sector: null` is dispatched at last**
  - Description: the stage claims the dismissal gestures the layers above it depend on.
    `keymap.ts` already has the machinery — `claimStageEscape` (`:113`) and `handleEscape` (`:125`),
    which walks the stack top-down and only falls through to `emptyStackEscapeFallback` when nothing
    is open — so **no `keymap.ts` change is needed**; the stage simply has to be a real stack entry.
    This closes a live dead end: `worldSelection.ts:29` declares
    `{ type: "select-sector"; sectorId: string | null }`, the reducer accepts `null`, and **nothing in
    the feature has ever dispatched it** — a selected sector cannot be deselected at all today.
  - Acceptance: Esc pops exactly one layer and the camera and selection survive; right-click on the
    map pane does the same thing (one gesture set, no exceptions — §4.4); with the stack empty, both
    dispatch `select-sector: null`; with a band-2 layer open, Esc does **not** reach the system menu.
  - Verify: `cd web\fusion-rpg-web; npm test -- WorldStage`.
  - Files: `src/stages/world/WorldStage.tsx`, `src/stages/world/WorldStage.test.tsx`,
    `src/stages/world/stageEscape.ts`.
  - Dependencies: W33.
  - Scope: S.

- [ ] **W36: Cut the stage free of `@xyflow/react`, and stage the package removal honestly**
  - Description: the new stage never imports the library, and a guard test makes that permanent. The
    library cannot leave `package.json` in this phase because the **three** files that still import it
    are the old page's view layer — `WorldPage.tsx:2-3`, `SectorNode.tsx:2`, `LaneEdge.tsx:2` (plus
    two test mocks at `SectorFog.test.tsx:12` and `SectorNode.test.tsx:18`; `routes.tsx:9` only names
    it in a comment) — and the map's arbitration row puts the old page's deletion in the **retirement
    task**, not here. So this task delivers the migration and records the last two lines of it against
    Phase 4 with their file:line, rather than shipping a removal that quietly breaks `#/world`.
  - Acceptance: `grep -r "@xyflow" web/fusion-rpg-web/src/stages` returns nothing, asserted by a guard
    test, not by review; every camera behaviour the library supplied has a successor in W20–W23;
    `grep -r "@xyflow" web/fusion-rpg-web/src` returns **only** the five old-tree references above,
    each listed in the Phase 4 retirement task with `package.json:31`.
  - Verify: `cd web\fusion-rpg-web; npm test`; `npm run build`.
  - Files: `src/stages/world/xyflowGuard.test.ts`, `tasks/world-stage-todo.md` (Phase 4 retirement
    entry).
  - Dependencies: W33, W34.
  - Scope: S.

- [ ] **Owner decision:** does `#/world` flip to the new stage at the end of Phase 2 — so **Gate B is
  played on the real route**, and `@xyflow/react`, the two test mocks and the three old view files go
  early — or does the temporary route carry Gate B and everything old survive to Phase 4? The
  arbitration table settles *who* deletes the old tree and *when* (retirement), not which route the
  playtest runs on. Both are cheap; only the first makes `grep -r "@xyflow"` empty in Phase 2.

### `world-numbers` (parallel with `world-shell`)

- [ ] **Owner decision:** authorise the two sealed-union additions this module needs —
  `UnitClass` gains **`loamUnits`**, and `Magnitude.op` gains **`absolute`**. `spec-world-numbers.md`
  files both under **Ask first** with the precedent named: `ladderIndex` (2026-08-24),
  `aptitudePoints` and `reciprocalPoints` (2026-08-26) were each proposed and authorised the same day,
  and each edit is recorded in `docs/design/spec-magnitude-and-units.md`. W26 and W27 do not start
  until this is ticked; nothing else in Phase 1 is blocked by it.

- [ ] **W37: Fix `formatPerMille` — an absolute per-mille is not a delta**
  - Description: the verified defect. `formatPerMille` (`magnitude.ts:66`) treats a per-mille value as
    a delta over 1000 — `case "more"` returns `×${(1 + value / 1000).toFixed(2)}` (`:69-70`), correct
    for a stat modifier where `+400‰ more` is ×1.40. The world's `FractureIntensityMilli` is
    **absolute**: 1000 is neutral and `WorldSectorDto.FractureIntensityMilli` defaults to `1000`
    (`WorldDtos.cs:80`), so plate §M's `1400 → ×1.40` renders as **×2.40** today. The fix is a fourth
    `op` arm in the one renderer, not a special case at a call site and **not** a divide-at-the-adapter
    (that is a derived number computed in TypeScript, which `spec-loam-fe.md` forbids outright).
    Two smaller per-mille rules ride along, both from plate §M.
  - Acceptance: `1400` with `op: "absolute"` renders `×1.40`; `StabilityMilli 240` renders `24%`, not
    `24.0%` (trailing zero trimmed); a small non-zero per-mille never renders `0%` — rounding happens
    **once**, at the display boundary, away from zero, in the same direction the engine rounds;
    `movementRemaining` renders as a fraction of one march's budget, never as `750 movement`; the
    union's exhaustiveness check (`magnitude.ts:44`) still compiles.
  - Verify: `cd web\fusion-rpg-web; npm test -- magnitude`; `npm run build`.
  - Files: `src/contract/types.ts`, `src/i18n/magnitude.ts`, `src/i18n/magnitude.test.ts`,
    `docs/design/spec-magnitude-and-units.md`.
  - Dependencies: Gate A; the owner decision above.
  - Scope: S.

- [ ] **W38: `loamUnits` — one class for whole loam, and the `…Milli` trap made irrelevant**
  - Description: there is no class for a `long` count of a resource. `gameUnits` is the nearest and is
    wrong twice over: its ledger row requires a `channel` (loam is not a derived channel) and it always
    renders signed (`magnitude.ts:50-55`), which is right for a net and wrong for a stock. The new
    class exists chiefly to make one bug unrepresentable: **four fields named `…Milli` hold whole loam
    units** — `StructureDef.CostMilli` (`StructureCatalog.cs:26`), `LoamPolicy.WellCostMilli` (`:106`),
    `WaystationCostMilli` (`:109`), `GranaryCostMilli` (`:126`), all compared against `CarriedLoam` at
    `BuildResolver.cs:101` and subtracted at `:115`. A renderer trusting the suffix prints *"A Well
    costs 0.2 loam"* and the player cannot see why a legion carrying 180 is refused. The class must
    **not** special-case those four names — the point is that the name is never consulted.
  - Acceptance: `wellCostMilli: 200` renders *200 loam*, and plate §M's three wrong renderings
    (`0.2 loam`, `20%`, `free`) are asserted **not** to occur; a stock renders against its denominator
    or a stated `Pending` reason, never a bare number; a flow carries a period and its sign on three
    channels (arrow **and** minus **and** colour — GG-27/GG-30); the class is added to the union, the
    renderer and `spec-magnitude-and-units.md`'s ledger **in one change**.
  - Verify: `cd web\fusion-rpg-web; npm test -- magnitude`; `rg -n "CostMilli" src\FusionRpg.Core\World`.
  - Files: `src/contract/types.ts`, `src/i18n/magnitude.ts`, `src/i18n/magnitude.test.ts`,
    `docs/design/spec-magnitude-and-units.md`.
  - Dependencies: W37.
  - Scope: M.

- [ ] **W39: The three world figure components**
  - Description: `LoamFigure` (stock · flow · period), `PerMilleFigure` (hold, intensity, hazard,
    march remaining) and `BandFigure` (`◆◆◆ Danger 3 of 5` — an index with its denominator, which is
    what today's `"◆".repeat(n)` at `SectorNode.tsx:104` lacks). Each is a pure function of a
    `Magnitude` plus a sentence template; the family rides in the type, so no component ever asks what
    a number "looks like". These are what `world-hud`, `world-inspector` and `world-playback` compose
    rather than reimplement.
  - Acceptance: a golden per family; no component accepts a bare `number` — proven by `tsc --noEmit`
    failing on a test file that tries, which is how the shipped renderer already enforces GG-46; no
    figure resolves to `--text-2xs`, `--text-xs` or `--faint`.
  - Verify: `cd web\fusion-rpg-web; npm test -- ui/world`; `npm run build`.
  - Files: `src/ui/world/LoamFigure.tsx`, `src/ui/world/PerMilleFigure.tsx`,
    `src/ui/world/BandFigure.tsx`, `src/ui/world/figures.test.tsx`.
  - Dependencies: W38.
  - Scope: M.

- [ ] **W40: `worldEnums.ts` — exhaustive lookups with a loud default**
  - Description: the failure mode here is silent and symptomless. `intel === "watched"` never matches
    because the wire says `"Watched"`; `"rumoured"` never matches because the wire says `"Rumored"`,
    American spelling (`FactionIntel.cs:133-140`). Neither throws — every sector quietly renders as
    unknown, and the only symptom is a map that looks fogged. So the world's four enum surfaces
    (intel, phase, ownership, force kind) get one exhaustive table with a development-time failure on
    an unmapped value, the same discipline `formatMagnitude`'s `const exhaustive: never`
    (`magnitude.ts:44`) already applies to unit classes.
  - Acceptance: all four surfaces map every value the wire can send; an unmapped value throws loudly
    rather than rendering blank; the casing and the `Rumored` spelling each have a named test;
    no enum value reaches a player surface untranslated (GG-23).
  - Verify: `cd web\fusion-rpg-web; npm test -- worldEnums`.
  - Files: `src/ui/world/worldEnums.ts`, `src/ui/world/worldEnums.test.ts`.
  - Dependencies: Gate A.
  - Scope: S.

- [ ] **W41: The modifier ledger — five rows, one division, and they add up**
  - Description: GG-49's answer to *"why did my net income drop?"*. **The rows are not a design
    choice**: they are exactly the five arguments of
    `LoamUpkeep.For(garrisonMembers, developmentLevel, dangerBand, intensityMilli, handicapMilli)`
    (`LoamUpkeep.cs:40`), in that order. **There is no calendar term in that signature** and an
    earlier §M.1 draft drew one anyway (*"this month is heavier ×1.15"*) — corrected on 2026-09-03 to
    the faction upkeep handicap, which is real and which the engine already narrates as
    `loam.handicap:1150`. Depth is capped at three levels; a fourth would be the tuning file.
    Arithmetic is the design: reading down the column must reproduce the total exactly.
  - Acceptance: rows are the five operands and **nothing else**, with a test that fails if a sixth
    appears; the total is computed with **one** division —
    `sum × intensityMilli × handicapMilli ÷ 1_000_000`, never two roundings, never a `float`
    (`long`-shaped arithmetic per `CLAUDE.md`); a property test over generated
    `(garrison, development, danger, intensity, handicap)` proves the rendered rows reproduce
    `LoamUpkeep.For`'s result after one boundary rounding; nesting is exactly three levels.
  - Verify: `cd web\fusion-rpg-web; npm test -- ModifierLedger`.
  - Files: `src/ui/world/ModifierLedger.tsx`, `src/ui/world/modifierLedger.ts`,
    `src/ui/world/ModifierLedger.test.tsx`.
  - Dependencies: W38, W39.
  - Scope: M.

- [ ] **W42: The ledger's WCAG 1.4.13 obligations, asserted rather than claimed**
  - Description: content on hover or focus, all three obligations plus the keyboard route that makes
    the ledger usable by players who do not hover. Also the operand rows the wire does not carry:
    `WorldSectorDto` holds totals only — `LoamProduction` (`WorldDtos.cs:89`), `LoamUpkeep` (`:92`),
    `LoamNet` (`:95`) — and `PressureMilli` is *declared* (`:72`) and **never assigned**, which is
    worse than missing because it looks wired. Until `world-wire` projects the breakdown, each operand
    row renders a player-readable `Pending` reason — never a blank, never a zero, never a client-side
    derivation.
  - Acceptance: **Dismissible** — Esc closes the ledger without moving the pointer and **leaves the
    inspector open**; **Hoverable** — the pointer can travel from the number into the ledger without
    it vanishing; **Persistent** — it never times out, closing only on dismissal, on the pointer
    leaving both, or on the underlying value changing; **Keyboard** — Enter on a focused figure opens
    it locked with its expandable rows in the tab order. Four assertions, one per obligation. Plus: an
    unprojected operand renders its reason in player words.
  - Verify: `cd web\fusion-rpg-web; npm test -- ModifierLedger`.
  - Files: `src/ui/world/ModifierLedger.tsx`, `src/ui/world/ModifierLedger.test.tsx`,
    `src/ui/world/ledgerPending.ts`.
  - Dependencies: W41.
  - Scope: S.

### `world-render` (level 3, parallel with `world-hud`)

- [ ] **W43: `sectorChannels.ts` — channel assignment as a pure function, and no dim is a value**
  - Description: the state → `{shape, border, pattern, glyph, word, token}` map, with **no `opacity`
    field in the type at all**. That is the direct replacement for `SectorNode.tsx:49-52`'s
    `opacity = 0.35 + 0.65 × stability/1000`, which is unreadable *as a value* (38% and 9% must both
    stay legible) and indistinguishable from a card sitting behind a scrim. Four orthogonal state
    groups — ownership, health, content, yield — because a sector can be yours **and** fading **and**
    warded **and** building **and** about to be released, and all five must read at once.
  - Acceptance: for every state in the matrix, `channelsFor` returns at least **two non-colour**
    channels (GG-27); no code path sets an opacity that varies with a value, asserted over the matrix
    rather than spot-checked; **barren is a flat, distinct look, not a deeper fade** —
    `SectorNode.tsx:43-48`'s own comment has the reasoning right and the encoding wrong; the matrix is
    exhaustive, so an added state fails the test until it is drawn.
  - Verify: `cd web\fusion-rpg-web; npm test -- sectorChannels`.
  - Files: `src/stages/world/render/sectorChannels.ts`,
    `src/stages/world/render/sectorChannels.test.ts`.
  - Dependencies: W33, Gate A.
  - Scope: M.

- [ ] **W44: The sector node — four state slots, five silhouettes, tokens only**
  - Description: the node itself, composing W32's channels and `world-numbers`' figures. The content
    row replaces the `S E M V L T ! $` letters at `SectorNode.tsx:29-39`, which cover **9 of the 14**
    slot kinds and are not shapes: five silhouettes (square, circle, hexagon, diamond, octagon) group
    the kinds and a glyph names one, with guarded ⚔ / built ▲ / building ⏳+turns as markers. The
    density ceiling is the fully-populated node, and §4.2's zoom rule binds: at map zoom the slot row
    and flags row drop first; **ownership, health and net never drop**, because each tier is a strict
    superset of the legibility below it.
  - Acceptance: all **14** slot kinds render; every state in plate §A renders; no hex literal (every
    colour is a token); a greyscale render loses no fact; the yield row is owner-only and goes through
    `LoamFigure`.
  - Verify: `cd web\fusion-rpg-web; npm test -- SectorNode`.
  - Files: `src/stages/world/render/SectorNode.tsx`, `src/stages/world/render/SectorNode.test.tsx`,
    `src/stages/world/render/slotSilhouettes.ts`.
  - Dependencies: W43, W39.
  - Scope: M.

- [ ] **W45: Lanes — six kinds × five states, stacked, and the path ids the markers need**
  - Description: kind and state are orthogonal and both must read — a warded, hazardous ley lane is
    drawable and reads as all three. Kinds: corridor (solid), rift (dashed), ley (twin rails), deep
    (solid, marked no-supply), one-way (arrowheads always), gated (long dashes + 🔒). States stack:
    Open, **Severed as a real gap plus ✕** (never a faded line — a faded line reads as *"far away"*),
    Warded (shield + `WardLevel` as a number, *"ward 3"*, never a %), Hazardous (fine dots + ☠ + the
    printed chance, `HazardMilli 400 → 40%`). Width is stroke weight — `strokeWidthFor`
    (`LaneEdge.tsx:24-26`) already does this half right and carries over unchanged; **length is a
    printed number, never drawn length**, because the layout is authored. Every rendered path carries
    `stageIds.lanePath(laneId)` from W22.
  - Acceptance: the kind × state matrix is exhaustive; the severed lane draws a gap, not a fade; every
    lane in the fixture exposes the id `stageIds.lanePath` declares; the six-entry raw-hex palette at
    `LaneEdge.tsx:11-18` has no successor.
  - Verify: `cd web\fusion-rpg-web; npm test -- Lane`.
  - Files: `src/stages/world/render/Lane.tsx`, `src/stages/world/render/laneChannels.ts`,
    `src/stages/world/render/Lane.test.tsx`.
  - Dependencies: W33, W43.
  - Scope: M.

- [ ] **W46: Legion markers — the rAF technique survives, and a test proves the ids do**
  - Description: the marker moves to `stages/world/render/`, keeps the `requestAnimationFrame`
    transform loop (a marching legion costs **zero** React re-renders) and loses its two ownership hex
    literals (`:56`) and its stroke hex (`:73`). Ownership reads as **three shapes before three
    colours**. Position is in-sector or a fraction along a lane at `LaneProgressMilli`. **This task
    carries the migration's one silent failure**: with the library gone nothing supplies the path ids,
    and markers stop moving with no error and no exception — so the shared assertion lands here.
  - Acceptance: for every lane in the fixture there is a DOM element whose id matches
    `stageIds.lanePath(laneId)` **and** `LegionMarker` finds it; a marker at `laneProgressMilli: 500`
    sits at half the path length; enemy strength renders as `BandName` + `BandCeiling` (*"A host —
    plan for 2,400"*) whenever `Exact` is false, and **`Strength 0` is unrepresentable**.
  - Verify: `cd web\fusion-rpg-web; npm test -- LegionMarker`.
  - Files: `src/stages/world/render/LegionMarker.tsx`,
    `src/stages/world/render/LegionMarker.test.tsx`, `src/stages/world/render/ForceChip.tsx`.
  - Dependencies: W45.
  - Scope: M.

- [ ] **W47: Fog — four treatments, and the branch is on `intel`, never on emptiness**
  - Description: the server already answers this in four derived states (`IntelLadder.StateOf`,
    `FactionIntel.cs:133-140`, `FreshTurns = 5` at `:131`); the client renders one well and the rest
    as a question mark. Watched: full clarity, live badge, exact counts. Scouted: doubled border +
    parchment wash + a dated stamp. Rumored: ragged dashed border + torn wash + *"hearsay"*. Unknown:
    **not a card at all** — a different silhouette, no name, no fields. Plus the control case: unowned
    but Watched gets a dashed ownership border and **no wash** — fog and ownership never share a
    channel. **The rule that otherwise ships a silent bug:** an unknown sector serialises every field
    at its record default (`WorldEndpoints.cs:271-277` returns only `SectorId`, `Intel`, `Phase`,
    `LayoutX`, `LayoutY`), so on the wire it is byte-identical to a zeroed known sector — a renderer
    that branches on *"is this empty?"* draws a real, poor, zero-danger sector as unexplored.
  - Acceptance: a fixture sector with `intel: "Watched"` and every other field at its type default
    renders as a **known, poor, zero-danger sector**; an `intel: "Unknown"` sector with an otherwise
    identical payload renders the silhouette; on a Scouted sector, terrain, climate, slots, structures
    and remembered ownership render while forces, guard markers and lane-borne legions do not, and the
    *"who stands here is not known"* strip is **present rather than a gap** (a gap reads as *"nobody is
    there"*); stale body text is `--text`, not `--muted` (§8c.5: at the 13% wash, `--muted` computes
    3.98, below AA).
  - Verify: `cd web\fusion-rpg-web; npm test -- fog`.
  - Files: `src/stages/world/render/Fog.tsx`, `src/stages/world/render/fog.test.tsx`,
    `src/stages/world/render/fogTreatments.ts`.
  - Dependencies: W43, W44.
  - Scope: M.

- [ ] **W48: Supply and lifeline overlays**
  - Description: two overlays for graph properties the player cannot see by looking. **Supply**: the
    connected block that is actually fed, derived from lanes that carry supply plus `ComponentId`; a
    sector outside it draws crossed-out with the words *"cut off"*. **Lifeline**: dashed amber halo +
    ◈ + a sentence naming the cost — *"losing this splits your empire (2 sectors cut off)"* — read
    from `Lifeline` / `LifelineCost`, which are **opt-in on the server** because the reconnection sweep
    is `O(holdings⁴)` and `WorldEndpoints.cs:51` gates it behind `?lifelines=true`. This module draws
    them; the lens picker that turns lifeline on is `world-lenses` in Phase 4, so the overlay takes a
    prop and defaults off.
  - Acceptance: a cut-off sector carries the words as well as the mark; the lifeline sentence names
    the number of sectors cut off; with `?lifelines=true` absent the overlay renders nothing and costs
    no request; an envelope that cannot enclose a non-convex territory falls back to per-lane drawing.
  - Verify: `cd web\fusion-rpg-web; npm test -- Overlay`.
  - Files: `src/stages/world/render/SupplyOverlay.tsx`,
    `src/stages/world/render/LifelineOverlay.tsx`, `src/stages/world/render/overlays.test.tsx`.
  - Dependencies: W44, W45.
  - Scope: M.

- [ ] **W49: Retire the hex-guard exemption and enforce the type floor on the map**
  - Description: `hexGuard.ts:27` carves `features/world/` out of the guard with the reason recorded
    above it — *"excluded this phase (T16, 2026-08-23 owner decision)… until its own plan lands"*
    (`:23-25`). **This is that plan**, and the map's arbitration row assigns the deletion to
    `world-render`, in the change that makes the map token-only (`world-shell` drops the claim). Note
    the exemption covers the **old** tree, so the new stage was never inside it — this task is what
    stops a hex literal re-entering when the old tree goes. The type floor rides along: XAG 101's
    18px at 1080p scales to **12px** at the declared 720p floor, and `--text-2xs` (10px) and
    `--text-xs` (11px) are below it; `--faint` is decorative-only by its own token comment and
    computes 3.22 on `--panel`.
  - Acceptance: `features/world/` is removed from `SKIPPED_PATH_PREFIXES` and the guard passes with
    the old tree still present (if any literal remains there, it is fixed, not re-exempted); no
    fact-bearing map label **or glyph** resolves to `--text-2xs`, `--text-xs` or `--faint`, asserted by
    a scan in the spirit of `ui/disabledReasonGuard.ts`; glyphs scale with text to 200%.
  - Verify: `cd web\fusion-rpg-web; npm test`;
    `rg -n "SKIPPED_PATH_PREFIXES" web\fusion-rpg-web\src\theme\hexGuard.ts`.
  - Files: `src/theme/hexGuard.ts`, `src/stages/world/render/typeFloor.test.ts`,
    `src/features/world/LaneEdge.tsx`, `src/features/world/LegionMarker.tsx`.
  - Dependencies: W44, W45, W46.
  - Scope: M.

- [ ] **W50: The stale-fog legibility check on `two-hearths`, run and recorded**
  - Description: §8.2 decided stale fog errs toward **distinctness** and that decision is not
    reopened — but its known cost is Civ VI's: a strong treatment can make a map harder to *plan on*.
    So the check this module owes is not *"can you tell them apart?"* but **"can you still plan a
    march against them?"**. It runs on **`two-hearths` (16 sectors), not `first-light`**: six sectors
    was reshaped precisely because one march lit the whole map, and Dave still holds at *"4 of 6 known
    across 14 turns"* — a map with two unknown sectors cannot test a stale-fog treatment.
  - Acceptance: the pass is run against a Scouted and a Rumored sector on `two-hearths` and its
    **result is recorded with the answer, including if it fails** — not a ticked checkbox; if it
    fails, the wash caps (13% Scouted / 18% Rumored, under the content layer) are the named lever and
    a change to them is **Ask first**, because §8.2 is an owner decision.
  - Verify: `cd web\fusion-rpg-web; npm run test:e2e -- world-stage`; result recorded in this file.
  - Files: `e2e/world-stage.spec.ts`, `tasks/world-stage-todo.md`.
  - Dependencies: W47.
  - Scope: S.

### `world-hud` (level 3, parallel with `world-render`)

- [ ] **W51: The band-1 frame and the corner-role contract five modules dock into**
  - Description: Amplitude's lesson from both directions at once — they are removing their "Divided
    UI" because players *"didn't know what part of the screen to look at"*, while EL1's *"strict
    division into corners"* is what players name as accessible. Both are true: **per-corner role
    stability is right; splitting one decision across two corners is what failed.** Six anchors: top
    strip (this module), top-left rail (shell, unchanged, `Rail.tsx:31`'s `w-[92px]` icon column),
    right edge (notify + outliner, Phase 3), bottom-right (turn cluster, Phase 3), bottom-left (map
    controls), left edge (**the inspector — the one conditional occupant**, §8e.1, docked *beside* the
    rail, not over it). Screen budget: chrome ~27%, map ~73% at 1280×720 — inside the *measured* band
    for shipped RTS (25–40%), not the *remembered* one (10–25%).
  - Acceptance: every anchor has exactly one occupant and no occupant changes as a function of a
    band-2 layer, except the left edge; **nothing in band 1 scrolls** at 1280×720 or 1440×900, in
    either axis — and per the map's arbitration row, that means *the band never grows or moves the
    stage*, not that a bounded child may never scroll its own body; unoccupied anchors (Phase 3's) are
    reserved, not filled with placeholders.
  - Verify: `cd web\fusion-rpg-web; npm test -- WorldHud`; `npm run test:e2e`.
  - Files: `src/stages/world/hud/WorldHud.tsx`, `src/stages/world/hud/WorldHud.test.tsx`,
    `src/stages/world/hud/anchors.ts`.
  - Dependencies: W33, Gate A.
  - Scope: M.

- [ ] **W52: The top strip — income · upkeep · net · stock, with an honest denominator**
  - Description: §8b.5's *summary up, detail down*, already written into `spec-loam-fe.md:156`. The
    strip carries **only empire scope**, which is what makes it safe under `resource-hub-ssot.md` §4
    (that rule forbids mixing scopes on one surface, not showing empire scope on a stage HUD). Four
    readings are built and on the wire; the **stock's denominator is not**: `LoamPhases.EffectiveCapacity`
    is computed at `LoamPhases.cs:58`, used internally at `:39`, and never projected. So the slot reads
    `1 140 / ? loam` with a player-readable reason rather than a bar that lies about its fullness —
    inferring a capacity on the client is exactly what `spec-loam-fe.md` forbids.
  - Acceptance: each of income · upkeep · net · stock renders through `world-numbers` with its unit
    family and a period on every flow; the missing denominator renders its `Pending` reason in player
    words and **no test fixture makes the client derive it**; no reading uses `--text-2xs`,
    `--text-xs` or `--faint`; the strip survives a 200% text scale without clipping or reordering.
  - Verify: `cd web\fusion-rpg-web; npm test -- TopStrip`.
  - Files: `src/stages/world/hud/TopStrip.tsx`, `src/stages/world/hud/TopStrip.test.tsx`.
  - Dependencies: W51, W39.
  - Scope: M.

- [ ] **W53: The calendar slot — from `WorldStateDto`, and no season vocabulary**
  - Description: **the map's arbitration row wins here over `world-hud`'s own SC3.** The calendar
    comes from **`WorldCalendarDto` on `WorldStateDto`** (`world-wire`'s projection), *not* from
    `calendar` report entries — and the report-entry source is wrong on its own terms:
    `TurnEngine.cs:225-231` emits calendar entries only on a week boundary, so that slot would be
    blank on **6 of every 7 turns**. What is real is a complete clock: `TurnCalendar.cs` runs a turn as
    a day, `DaysPerWeek` days a week, `WeeksPerMonth` weeks a month (`:22-24`), rolled purely from
    `(turn, seed)`. Seasons are the *effects* half of that clock and belong to `sector-development`;
    the plate's §G.1/§G.2 **Season · Long Wither** slot has no field behind it.
  - Acceptance: turn renders from `WorldHeaderDto.CurrentTurn` and week/month with flavour from
    `WorldCalendarDto`; the slot is populated on **every** turn, not one in seven — a test at a
    non-boundary turn; **no season vocabulary appears anywhere**, guarded by a test so the plate's
    uncorrected label cannot be re-imported; `world-hud` SC3 and its test are updated to the wire
    source and the change noted.
  - Verify: `cd web\fusion-rpg-web; npm test -- calendar`.
  - Files: `src/stages/world/hud/calendarLabel.ts`, `src/stages/world/hud/calendarLabel.test.ts`,
    `src/stages/world/hud/TopStrip.tsx`, `docs/architecture/world-stage/spec-world-hud.md`.
  - Dependencies: W52; Gate A (`world-wire`'s `WorldCalendarDto`).
  - Scope: S.

- [ ] **W54: The component-split state — six states, three rows, colour fourth**
  - Description: after the settlement rule, *"my empire is fine"* can be false while half of it
    starves: `TerritoryComponents` makes the empire N **purses**, not N sectors, so at turn 80 with
    fourteen sectors the player manages two or three decision objects. The wire already carries it and
    `LoamGauge.tsx:28-45` already computes it — its own comment names the reason (`:6-8`) — but it has
    never had a place on screen that does not scroll away. Six states, and the empty and collapsed
    ones matter as much as the alarm.
  - Acceptance: one component → the row **collapses entirely**; split and solvent → the split is
    stated with **no alarm** (split is a fact, starving is the event — conflating them trains the
    player to ignore the row); one starving → only that row alarms; both starving → both alarm
    independently; **no territory → a sentence, not four zeroes** (zeroes read as a broken feed); many
    components → starving sorts first and is never folded, solvent folds past two, never exceeding
    `MAX_SPLIT_ROWS = 3` — which is what keeps band 1 a fixed height at the 720p floor. Four channels
    with colour fourth: the state is identifiable with the tint removed.
  - Verify: `cd web\fusion-rpg-web; npm test -- ComponentSplit`.
  - Files: `src/stages/world/hud/componentSplit.ts`, `src/stages/world/hud/ComponentSplit.tsx`,
    `src/stages/world/hud/componentSplit.test.ts`, `src/stages/world/hud/ComponentSplit.test.tsx`.
  - Dependencies: W52.
  - Scope: M.

- [ ] **Owner decision:** sign off the **GG-5 band-table amendment** — *"a band-2 scrim covers band 0
  only; band 1 sits above it, fully legible and interactive; band 3 and above are unchanged."* It is a
  **Tier-1 rule** and it changes the Sanctum and the Lawn as well as the world, so `world-hud` files
  it under **Ask first**. W44 does not land without it; nothing else in Phase 1 is blocked.

- [ ] **W55: Fix the live scrim defect — `PanelShell.tsx:61`, then the kit, then GG-5**
  - Description: **this is the finding most likely to ship a fix that misses.** §8d.3 and
    `world-hud` both target `_kit/kit.css:401`'s `.scrim` — and **the shipped web does not use that
    class at all**; grep returns nothing. The live defect is
    `web/fusion-rpg-web/src/shell/PanelShell.tsx:61`:
    `cn(band === "system" ? "band-system" : "band-panel", "fixed inset-0 bg-black/50")` — a
    full-viewport 50% black overlay at band-panel over a band-hud HUD. With `--band-hud: 100` against
    `--band-panel: 200` (`theme/tokens.css:102-103`), opening any inspector drops `--text` on the rail
    from 14.08:1 to **2.12:1** and a turn-cluster blocker reason to **1.50:1** — the one sentence
    explaining why the player cannot end their turn, made unreadable by the panel that raised it.
    The kit and the GG-5 amendment are still required (they stop the defect being re-authored) but on
    their own they would leave the regression exactly where it is.
  - Acceptance: `PanelShell`'s overlay no longer covers band 1, and **ten shipped surfaces that bind
    to `PanelShell` still pass their tests**; a test mounts the HUD, opens a band-2 layer, and asserts
    the HUD's computed stacking is above the scrim and its text is not composited under it — without
    this the amendment is prose; `_kit/kit.css:401` is corrected at source; GG-5's band table in
    `docs/architecture/game-gui-principles.md` carries the amendment.
  - Verify: `cd web\fusion-rpg-web; npm test`;
    `rg -n "band-hud|band-panel" web\fusion-rpg-web\src\theme\tokens.css docs\design\_kit\tokens.css`.
  - Files: `src/shell/PanelShell.tsx`, `src/shell/shells.test.tsx`,
    `src/stages/world/hud/WorldHud.test.tsx`, `docs/design/_kit/kit.css`,
    `docs/architecture/game-gui-principles.md`.
  - Dependencies: W51; the owner decision above.
  - Scope: M.

---

## Phase 2 — the map is playable, and it speaks

`world-inspector` and `world-targeting` sit on Phase 1; `world-playback` is parallel to both and has
no stage dependency. This phase ends at **Gate B**.

### `world-inspector`

- [ ] **W56: `DockShell` — an edge-anchored band-2 shell beside `PanelShell`, not a copy of it**
  - Description: `PanelShell` satisfies half the contract and violates the other half. **Keep**: the
    bounded height `max-h-[min(720px,82vh)]` (`:81`, already GG-61's bound), the body as the only
    scrolling part (`:93`), layer-stack registration at band `panel` with `close` owned by the stack
    (`:40-44` — this is what makes Esc work at all), and the Radix focus trap with restore-to-opener
    (`:50-54`, `:66-69`). **Wrong**: the centred geometry (`:79`) — a dock is edge-anchored and
    full-height. So this is a sibling in `src/shell/`, and **`PanelShell` itself is not touched**: ten
    shipped surfaces bind to it, which is why `world-inspector` files that change under **Ask first**.
  - Acceptance: the dock is left-anchored and full-height, docking **beside** the `w-[92px]` rail
    (`Rail.tsx:31`), never over it; it registers on the layer stack like `PanelShell` and Esc pops it;
    focus is trapped and restored to the opener; its own height never exceeds the bound while its body
    scrolls; it writes **no `z-index`** — band classes are the only stacking vocabulary (GG-5).
  - Verify: `cd web\fusion-rpg-web; npm test -- DockShell`.
  - Files: `src/shell/DockShell.tsx`, `src/shell/DockShell.test.tsx`.
  - Dependencies: W55.
  - Scope: M.

- [ ] **W57: The inspector shell, the block order, and the GG-61 proof**
  - Description: `SectorInspector` — the dock plus the nine blocks in the plate's order, which is
    deliberate: identity first, then the thing that can take the ground away from you, then the two
    economies, then what is on the ground, then what you can do about it. **The measurement is the
    design constraint**: the plate measured **1,597px of body content in a 400px well**, and this is
    the case GG-61 (Tier-1) was written for. The stage behind the dock must not move a pixel.
  - Acceptance: a **maximal-sector fixture** — every block populated, four slots, multiple forces, a
    warden, a construction in progress — renders at 1280×720 with the shell inside its bound, the
    **body** scrolling (`scrollHeight > clientHeight`), and the stage element behind it measuring
    `scrollHeight − clientHeight === 0`; the same at 1440×900 and at 200% text scale; the block order
    is asserted, so a later addition cannot quietly reorder it.
  - Verify: `cd web\fusion-rpg-web; npm test -- SectorInspector`; `npm run test:e2e`.
  - Files: `src/stages/world/inspector/SectorInspector.tsx`,
    `src/stages/world/inspector/SectorInspector.test.tsx`,
    `src/stages/world/inspector/fixtures/maximalSector.ts`.
  - Dependencies: W56, W44.
  - Scope: M.

- [ ] **W58: Identity and ground blocks — and the intel branch stated once, at the top**
  - Description: blocks 1 and 2. Identity carries `SectorId`, `TypeId`, `Climate`, `Phase`,
    `DangerBand`, `Intel` and `IntelAge`, with the four intel states each getting a distinct header
    treatment and the stale two carrying their age **in words** — *"4 nights old"*, never a bare
    integer. Ground carries `StabilityMilli`, `PressureMilli`, `DevelopmentLevel` and fracture
    intensity, with `PressureMilli` `Pending` (declared at `WorldDtos.cs:72`, never assigned). A
    `Rumored` sector's slot list is empty **by design** (`WorldDtos.cs:134-135`), so the panel says
    *a glimpse sees no slots* rather than drawing an empty list that reads as *nothing here*.
  - Acceptance: four sectors, one per intel state, sharing an **identical zeroed payload**, render
    differently, and the `Unknown` case is reached without reading any field but `intel`; a `Scouted`
    sector shows ground, buildings and remembered ownership and **no forces**; fracture intensity
    renders `×1.40` from `1400` via W26's `absolute` op.
  - Verify: `cd web\fusion-rpg-web; npm test -- inspector`.
  - Files: `src/stages/world/inspector/IdentityHeader.tsx`,
    `src/stages/world/inspector/GroundBlock.tsx`, `src/stages/world/inspector/blocks.test.tsx`.
  - Dependencies: W57, W40.
  - Scope: M.

- [ ] **W59: The next-turn block, under the cede embargo**
  - Description: block 3, the most delicate on the surface, and the one place a drawing is ahead of
    the engine. `spec-loam-fe.md:80` wants a keep/release-first pin and `:82-84` deferred it until
    there was a surface to set it from — **this is that surface** — but the engine does not let you
    choose: `LoamPhases` picks the release target itself every turn via `LoamForecast.Weakest`
    (`LoamPhases.cs:138`), and `WorldCommand.All` is
    `{ StandFast, Move, Clear, Claim, Stance, Sustain, Build }` (`WorldCommand.cs:36-37`) — there is no
    cede kind. Plate 11 §J.1 draws *"Keep this ground"* and *"Give this up first"* **against a verb
    that does not exist**, and shipping them as drawn is a lie the player catches on their first
    shortfall. So the block reads *"here is what will be released next turn, and here is what would
    stop it"* — truthful, and it ships now.
  - Acceptance: with the cede capability **absent**, the pin controls are not in the document and the
    copy contains neither *"choose"* nor *"release first"*; with it **present**, both render and file
    a real order; `WillReleaseNextTurn` (`WorldDtos.cs:125`) renders with its reason either way.
  - Verify: `cd web\fusion-rpg-web; npm test -- NextTurnBlock`.
  - Files: `src/stages/world/inspector/NextTurnBlock.tsx`,
    `src/stages/world/inspector/NextTurnBlock.test.tsx`,
    `src/stages/world/inspector/cedeCapability.ts`.
  - Dependencies: W57.
  - Scope: M.

- [ ] **W60: The cede embargo, enforced by a test that retires itself**
  - Description: §8c.2's finding — *the economy's core tension was a notification, not a decision* —
    re-enters by drift unless something stops it. **No surface in this program may say "choose what to
    release" until `world-commands`' cede order lands**, and the enforcement is a test that reads the
    **command vocabulary** rather than a hard-coded flag: while `WorldCommand.All` carries no cede
    kind the test asserts the embargo copy across every world surface; when the kind appears the test
    inverts and asserts the pin renders. It is written so that landing the order retires the embargo
    automatically instead of leaving a stale prohibition behind.
  - Acceptance: the test derives the capability from the command vocabulary, not from a literal; it
    scans the inspector, the HUD and the targeting surfaces for the forbidden phrasings; it fails
    loudly if the vocabulary gains a cede kind while a surface still shows the embargo copy — the
    self-retirement condition, asserted in both directions.
  - Verify: `cd web\fusion-rpg-web; npm test -- cede`.
  - Files: `src/stages/world/cedeEmbargo.test.ts`, `src/stages/world/inspector/cedeCapability.ts`.
  - Dependencies: W59.
  - Scope: S.

- [ ] **W61: The two economy blocks — sector loam, and the territory reach that can starve alone**
  - Description: blocks 4 and 5. Block 4 is this sector's own `LoamProduction` / `LoamUpkeep` /
    `LoamNet` / `LoamStock` — earns · costs · net · in store. Block 5 is the **detail** half of
    §8b.5's summary-up/detail-down split: `ComponentId`, `ComponentProduction`, `ComponentUpkeep`,
    `ComponentNet`, `ComponentStock`, reading the **same projection** the HUD strip reads so the two
    cannot disagree. The block says plainly which of the two is starving — deriving that from four
    numbers is what today's map makes the player do. Every figure hangs the modifier ledger (W30).
  - Acceptance: the case §4.3 calls first-class renders correctly — **a starving reach while the
    empire total is positive**; every number goes through `world-numbers` with its family; the ledger
    opens from the upkeep figure and its rows sum to it; unprojected operands show their `Pending`
    reason.
  - Verify: `cd web\fusion-rpg-web; npm test -- inspector`.
  - Files: `src/stages/world/inspector/SectorLoamBlock.tsx`,
    `src/stages/world/inspector/ComponentBlock.tsx`,
    `src/stages/world/inspector/economyBlocks.test.tsx`.
  - Dependencies: W57, W41, W54.
  - Scope: M.

- [ ] **W62: Slot rows (seven states) and force rows (exact vs band)**
  - Description: blocks 6 and 7. The slot row is the product of `SlotState`
    (`Intact`/`Claimed`/`Depleted`/`Ruined`), `GuardState` (`Intact`/`Cleared`) and whether a structure
    is present and finished — **and the player never sees either enum** (GG-23). Seven rows result,
    and **two of them (depleted, ruined) have never been drawn anywhere**: a list that silently cannot
    represent two of its own states is a defect that only appears in a save nobody tested.
    `ConstructionTurnsRemaining` has no DTO field, so *"ready in 3 nights"* cannot ship until
    `world-wire` projects it and the row says so in player words rather than blanking. Force rows:
    yours exact, anyone else's a band.
  - Acceptance: all seven slot states render, depleted and ruined included; a guarded slot names its
    `GuardWaveId` **as a force, not an id**; no enum value appears on screen; a force with
    `Exact: false` renders `BandName` + `BandCeiling` and never `Strength 0`.
  - Verify: `cd web\fusion-rpg-web; npm test -- SlotRow ForceRow`.
  - Files: `src/stages/world/inspector/SlotRow.tsx`, `src/stages/world/inspector/ForceRow.tsx`,
    `src/stages/world/inspector/rows.test.tsx`.
  - Dependencies: W57, W40.
  - Scope: M.

- [ ] **W63: Warden and dowsing blocks, both honest about what is not wired**
  - Description: blocks 8 and 9, small and easy to get wrong by drawing them as if they worked.
    `WardenBindingId` has **no DTO field** — the block renders `Pending` with a player-readable reason
    and the binding verb itself belongs to `world-confirms` in Phase 4. Dowsing reads
    `Prospecting.Reveal` (`IntelRecorder.cs:179`), and the `dowse` **stance is missing from**
    `MovementPolicy.Stances` (`Movement/LaneCost.cs:13`) — `world-commands`' gap, so the block states
    what prospecting has found and does not offer a stance the wire cannot carry.
  - Acceptance: neither block renders a blank or a zero for an unwired field — each carries a reason
    a player can read; neither offers a verb the command vocabulary lacks (same derivation as W49);
    the prospected set renders from the wire when Phase 0 has projected it.
  - Verify: `cd web\fusion-rpg-web; npm test -- WardenBlock DowseBlock`.
  - Files: `src/stages/world/inspector/WardenBlock.tsx`, `src/stages/world/inspector/DowseBlock.tsx`,
    `src/stages/world/inspector/wardenDowse.test.tsx`.
  - Dependencies: W57.
  - Scope: S.

- [ ] **W64: The action cluster — every refusal a rendered sentence, never a tooltip**
  - Description: GG-55 is the rule and plate 03 §E settled the wording: *"disabled with its reason
    beside it, always"*. Two properties, and the second is the one that gets lost. **Never hidden**: an
    unavailable verb stays in the cluster, greyed, in its place — hiding it is AoW4's failure, where
    the player concludes the verb does not exist. **The reason is visible, not a tooltip**:
    `ui/disabledReasonGuard.ts:57` accepts `title`/`aria-label`/`aria-describedby` and its scan
    (`:59-75`) will pass a control whose only reason is a hover string — **that guard is the floor,
    not the bar**, because a hover reason is unreachable on touch and invisible to a keyboard user who
    has not focused it. Reasons come from `world-playback`'s **one** table (W61), never a second copy.
  - Acceptance: every verb in plate 11 §J.4's refusal table renders its reason as text **queried by
    text, not by `title`**; no engine token (`claim.contested`, `build.cannot-afford`, …) appears in
    the visible text or the accessible name; a disabled verb keeps its position in the cluster.
  - Verify: `cd web\fusion-rpg-web; npm test -- ActionCluster`.
  - Files: `src/stages/world/inspector/ActionCluster.tsx`,
    `src/stages/world/inspector/ActionCluster.test.tsx`, `src/stages/world/inspector/reasonFor.ts`.
  - Dependencies: W57, W72.
  - Scope: M.

- [ ] **W65: One dismissal gesture, applied without exception**
  - Description: §4.4's rule, closing the dead end W24 opened the door on. Four gestures, one
    outcome: **Esc** pops exactly one layer (the inspector closes, the map keeps its camera and its
    selection); **right-click on the map pane** does the same; the **✕** in the header does the same
    for pointer users who learn neither; **clicking the selected sector again** deselects — and that
    is the dispatch of `select-sector: null` that has never existed in the feature's life
    (`worldSelection.ts:29`). The Esc ordering needs no `keymap.ts` change: `handleEscape` (`:125-135`)
    already walks the stack top-down and only falls through to `emptyStackEscapeFallback`, which
    `SystemHost.tsx:26-29` claims, when nothing is open.
  - Acceptance: all four gestures close the inspector and only the inspector; camera and selection
    survive an open/close cycle (GG-11, `getStageMountCount("world")` stays at 1); clicking the
    selected sector dispatches `select-sector: null`; **with the inspector open, Esc does not open the
    system menu**.
  - Verify: `cd web\fusion-rpg-web; npm test -- SectorInspector`; `npm run test:e2e`.
  - Files: `src/stages/world/inspector/SectorInspector.tsx`,
    `src/stages/world/inspector/dismissal.test.tsx`, `src/stages/world/WorldStage.tsx`.
  - Dependencies: W57, W35.
  - Scope: M.

### `world-targeting`

- [ ] **W66: Widen `PendingOrder` to eight verbs — and round-trip every new field**
  - Description: `kind` is a closed union of three today (`worldSelection.ts:13`) and plate 11 §E.5
    draws **eight**. Each new member arrives with the field the engine reads: `stance` (live on the C#
    wire at `WorldDtos.cs:213-214`, **missing from the TS mirror** at `lib/bus/world.ts:23-30`),
    `sustain` + `amount`, `build` + `structureId` + `slotIndex`, `stand-fast`, and **`ward`, which
    breaks the shape**: a ward sits on a **lane** (`WorldLaneDto.WardLevel`, `WorldDtos.cs:160`), so
    `PendingOrder` gains an optional `laneId` and the click target is a **line**. Smuggling it in as a
    sector order would fight the model and collide with `bind-warden`, which the arbitration table
    keeps as a separate kind. `toRequests` (`:63-73`) widens in lockstep. **The pure layer is reused,
    never re-derived** — `routeBetween` (`:81-125`), `routeForLegion` (`:137-153`), `worldUiReducer`
    (`:39-60`) and `orderId` (`:155-158`) are all correct.
  - Acceptance: for each new kind, `toRequests` emits the field the engine reads and a fixture proves
    it **survives the wire shape** — a field the queue carries and the wire drops is lost silently,
    which is exactly how `stance` was found missing; the mid-lane case has its own test
    (`routeForLegion` puts the current lane at the **head**, or the engine refuses the path as
    `path.not-contiguous` while the queue looks correct); **the 46 existing pure-layer tests
    (`worldSelection.test.ts` 19, `worldViewModel.test.ts` 27) stay green with no edits** — a diff that
    touches them is a re-derivation and is wrong.
  - Verify: `cd web\fusion-rpg-web; npm test -- worldSelection`;
    `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `src/features/world/worldSelection.ts`, `src/features/world/worldSelection.test.ts`,
    `src/lib/bus/world.ts`.
  - Dependencies: Gate A (`world-commands`), W57.
  - Scope: M.

- [ ] **W67: `targetingState.ts` — the transient overlay lifecycle, pure**
  - Description: which verb is being targeted, which overlay it owns, and the **restore contract**:
    range and placement overlays are transient — no picker slot, no hotkey, alive only while the verb
    is — and on Esc or completion they restore the player's chosen lens (`world-lenses` ships the
    picker in Phase 4; this module must not assume it). A reducer, so the lifecycle is testable with
    no DOM.
  - Acceptance: starting a verb activates exactly one overlay; Esc cancels targeting **before** it
    would close the inspector (one Esc, one layer); completing an order restores the prior lens
    selection; no overlay survives a selection change.
  - Verify: `cd web\fusion-rpg-web; npm test -- targetingState`.
  - Files: `src/stages/world/targeting/targetingState.ts`,
    `src/stages/world/targeting/targetingState.test.ts`.
  - Dependencies: W66, W65.
  - Scope: S.

- [ ] **W68: Route preview — this turn, next turn, later, each carrying its turn in text**
  - Description: select a legion and the map answers *where can I go* before any button is pressed —
    replacing `WorldPage.tsx:365-369`'s prose instruction manual printed beside a raw entity id.
    Solid bright = this turn, dashed amber = next, dotted faint = later, and **every one also carries
    `T` / `T+1` / `T+2` in text** (Endless Legend's idea transfers; its reliance on colour does not —
    GG-27, GG-30). **The cost is projected, never derived**: pricing a lane needs `LaneCost.For`
    (`LaneCost.cs:117-131`), the lane-type catalog and the legion's banner element, none of which is on
    the wire — re-implementing it in TypeScript would put a second copy of a hashed engine rule in the
    browser. Until `world-wire`'s per-lane cost lands, the preview draws the **route and its hop
    sequence** (which `routeBetween` already gives) and marks the turn split `pending` with a reason.
  - Acceptance: the route renders from `routeBetween`/`routeForLegion` unmodified; the this-turn/later
    split is carried in **text** as well as style; with no projected cost the split renders its
    `Pending` reason and **no guessed number**; **fog over-prices and stays over-priced** — with an
    unscouted ley lane's endpoints the preview shows the undiscounted cost (`LaneCost.cs:108-116`), and
    a test that "fixes" this is fixing the wrong thing; the preview never paints authority (GG-15).
  - Verify: `cd web\fusion-rpg-web; npm test -- RoutePreview`.
  - Files: `src/stages/world/targeting/RoutePreview.tsx`,
    `src/stages/world/targeting/RoutePreview.test.tsx`.
  - Dependencies: W67, W45.
  - Scope: M.

- [ ] **W69: Range overlays — one grammar for three verbs, with hop numbers**
  - Description: three verbs reach past where you stand and they share a grammar. **Raise a
    waystation**: 3 **plain road hops**, unweighted, measured from any holding of yours that is
    currently **habitable** — `BuildResolver.cs:90-99` calls `WithinWaystationRange`, which walks
    `Hops.Between` against `LoamPolicy.WaystationRangeHops` and skips sectors failing `Habitability.For`
    (`:145-159`); the `3` is a tunable at `data/tuning/loam.v1.json:35`, so it is read, never hard-coded.
    **Raise a well**: no range — the check is gated on `RequiredSlotKind == SlotKind.Seat`
    (`BuildResolver.cs:94`). **Ward a road**: the target is an **edge**. **Take the ground**: range 0,
    and it is **drawn anyway** as a one-cell overlay — silence would make the player wonder whether
    they had missed a target.
  - Acceptance: reachable ground gets a solid ring **plus its hop number** (the number is what makes
    the rule teachable without a manual); out-of-reach ground gets nothing except, on hover or focus,
    the sentence saying why; the hop count comes from the tuning row, not a literal; the ward overlay's
    click target is a line, not a node.
  - Verify: `cd web\fusion-rpg-web; npm test -- RangeOverlay`.
  - Files: `src/stages/world/targeting/RangeOverlay.tsx`,
    `src/stages/world/targeting/RangeOverlay.test.tsx`.
  - Dependencies: W67, W44.
  - Scope: M.

- [ ] **W70: Blocked targets — every refusal a sentence, placed where the decision is made**
  - Description: GG-23 is Tier-1 and this is its second surface. **~37 drop reasons** (33 bare, 4
    carrying an argument), verified against `src/FusionRpg.Core/World/` on 2026-09-03. Two rules, both
    testable: **a reason is a sentence with the subject in it** — *"Ashfoot is carrying 180 loam. A
    waystation costs 300."* beats *"cannot afford"* because it names the shortfall the player has to
    close; and **a reason is shown where the decision is made** — a road refusal on the road, a path
    refusal on the target sector, a slot refusal in the inspector, a legion refusal on the marker.
    Scattering them into one notification string is the current behaviour and it is why the map reads
    as a flowchart. **Blocked is drawn, never hidden and never merely dimmed**: hatched, crossed,
    captioned. And **inert is a third treatment**: `sustain` and `build` run end-to-end in the engine
    and are unreachable because the wire drops one field each — *"the game cannot carry this order
    yet"*, because hiding them would hide the fact that they are two fields from working.
  - Acceptance: a table test over the whole token set asserts (a) no raw token reaches rendered
    output and (b) the sentence is attached to the right subject — road / sector / slot / marker;
    blocked, inert and available are three visually distinct treatments; the sentences come from
    `world-playback`'s table (W61), not a second copy.
  - Verify: `cd web\fusion-rpg-web; npm test -- BlockedTarget`.
  - Files: `src/stages/world/targeting/BlockedTarget.tsx`,
    `src/stages/world/targeting/blockedPlacement.ts`,
    `src/stages/world/targeting/BlockedTarget.test.tsx`.
  - Dependencies: W67, W72.
  - Scope: M.

- [ ] **W71: The queued order — filed, drawn, and takeable back**
  - Description: filing an order and ending the turn are two separate acts, and between them the
    order is **queued**: it exists, it is drawn, nothing has resolved. **On the map the token never
    moves** — the legion is drawn where it actually is and the intent is a dashed flag on the
    destination with a lit route between them; nothing about a queued order may look like it has
    happened. The player-facing promise is exact: **nothing you filed this turn is binding until you
    end the turn** — orders are keyed by `orderId` so filing twice is the same order, and take-back
    removes it and re-submits the remainder (`unqueue`, `worldSelection.ts:56`, already does the work).
    A standing order is **re-issued whole each turn**: the server keeps no multi-turn queue, and the
    interface may make re-issuing nearly free but **must not pretend the server remembers**.
  - Acceptance: queueing a march does not move the marker — the token stays at `atSectorId` and the
    destination carries the flag, asserted by test id, not by class; each queue row names the order in
    player words and carries *take back*; after commit there is no take-back and the stage hands over
    to playback; the Playwright path runs select → highlight → click → queued → take back → empty,
    with the marker never having moved.
  - Verify: `cd web\fusion-rpg-web; npm test -- QueuedOrders`; `npm run test:e2e`.
  - Files: `src/stages/world/targeting/QueuedOrders.tsx`,
    `src/stages/world/targeting/QueuedOrders.test.tsx`, `e2e/world-stage.spec.ts`.
  - Dependencies: W66, W68.
  - Scope: M.

### `world-playback` (parallel — no stage dependency)

- [ ] **W72: The one translation table, and a completeness test that walks the vocabulary**
  - Description: today `classify()` recognises **five** prefixes and falls through on everything else
    (`turnPlayback.ts:33-42`), and the fall-through prints the raw string (`:94-95`), so a turn in
    which the empire starves reads literally `dave loam.shortfall:340` and a refused order reads
    `t3-move-e-dave-legion-1 dropped — path.not-contiguous` (`:91`). **It is one table, not per-prefix
    handling** — per-prefix handling is precisely how the 5-of-21 state arose. The report's shape makes
    a table possible: `WorldTurnEntryDto` is `{SectorId?, Phase, Kind, Subject, Detail}`
    (`WorldDtos.cs:261-270`) and `Kind` is one of five constants (`TurnReport.cs:3-10`), so the key is
    **`(kind, detail-prefix)`** with arguments parsed off the tail. Counted, not estimated:
    **21 event prefixes, 3 battle kinds, 2 calendar subjects, 37 drop reasons.**
  - Acceptance: every one of the 21 + 3 + 2 + 37 tokens has a row, proven by a test that **walks the
    token inventory** — this is the test that makes CI notice a new engine token, which nothing does
    today; each row's args carry a **unit family**, so `loam.handicap:150` renders *15% more* and a
    test asserts `150` never appears (same for `legion.runway:11`, a turn number, and `sustain:120`,
    whole loam); an unmatched token renders a visibly broken row and logs in development, and degrades
    to a neutral player sentence in production — **never the token**.
  - Verify: `cd web\fusion-rpg-web; npm test -- playbackTable`.
  - Files: `src/features/world/playbackTable.ts`, `src/features/world/playbackTable.test.ts`.
  - Dependencies: Gate A.
  - Scope: L.

- [ ] **W73: Delete the `attrition:` dead branch, and do not invent `supply.restored`**
  - Description: two honest notes about the engine's vocabulary, both surfaced only because someone
    sat down to write a player sentence for every token — which is the argument for a table.
    **`attrition:` is a dead branch: delete it, do not translate it.** The client still classifies it
    (`turnPlayback.ts:40`) and renders *"takes attrition"* (`:89`), but **nothing in
    `src/FusionRpg.Core` emits it**: `LegionSupply.Resolve` replaced wound attrition and `SupplyGraph`
    says so itself (`SupplyGraph.cs:42-45`). Its only other reference is a fixture
    (`turnPlayback.test.ts:70`), which goes with it. **There is no `supply.restored` token.** The
    engine emits `recovery:` (`SupplyGraph.cs:111`), which is a **garrison mending**, not a sector
    rejoining supply, so the plate's *"Frost Mire is back in supply"* has nothing behind it — and
    playback **does not infer restoration by diffing two reports**, because deriving an event from an
    absence is the client-side inference GG-15 and §0.13 rule out and would be wrong the first time a
    report is trimmed.
  - Acceptance: `attrition:` has **no** table row and a test asserts it, so re-adding one is a
    deliberate act; the branch, the `describe()` arm and the fixture at `:70` are all gone;
    `recovery:` is translated as a garrison mending; **nothing on the rail claims a sector rejoined
    supply**, and the missing engine line is filed against `world-wire` rather than faked here.
  - Verify: `cd web\fusion-rpg-web; npm test -- turnPlayback`.
  - Files: `src/features/world/turnPlayback.ts`, `src/features/world/turnPlayback.test.ts`,
    `src/features/world/playbackTable.ts`.
  - Dependencies: W72.
  - Scope: S.

- [ ] **W74: `labels.ts` — every id humanised, and the two that cannot be guessed**
  - Description: `sectorLabel()` turns `ember-hollow` into `Ember Hollow`
    (`worldViewModel.ts:197-203`) and is called in **exactly one place** in production (`:300`,
    building a node label), so every playback line shows raw kebab ids today. Four id kinds reach the
    rail and they are not interchangeable. Sector and lane can be humanised from the id. **Legion and
    faction cannot**: a legion's display name is not derivable from `e-dave-legion-1`, and inventing
    one in a `split("-")` is how `Legion I` becomes `E Dave Legion 1`. The faction name is already
    projected (`WorldFactionDto.Name`); the legion name is a `world-wire` field.
  - Acceptance: sector, lane, faction and legion labellers exist and every rendered line uses one; the
    legion labeller returns a **`pending` value with a player-readable reason** until the wire carries
    the name — never a guess; `sectorLabel` moves out of `worldViewModel.ts` with its 27 tests still
    green.
  - Verify: `cd web\fusion-rpg-web; npm test -- labels`.
  - Files: `src/features/world/labels.ts`, `src/features/world/labels.test.ts`,
    `src/features/world/worldViewModel.ts`.
  - Dependencies: W72.
  - Scope: S.

- [ ] **W75: The keyframe rail and its transport — including the phase that emits nothing**
  - Description: the report carries `Phases` in the order they ran (`WorldDtos.cs:276`) and the
    engine's list is closed — Reveal, Movement, Sieges, Production, Growth, Pressure, Events, Snapshot,
    Intel (`TurnEngine.cs:44-63`). Playback is a **straight walk in report order**; re-sorting would
    tell a different story than the one the server recorded. **`Growth` is a named no-op** —
    `report.BeginPhase(Phases.Growth); return world;` (`:196-200`) — so it appears in the phase list
    with zero entries, and a blank gap reads as a loading failure (GG-17). That will change: §8d.1 puts
    recruitment in `Growth`, so phase rendering must accept a phase gaining entries **with no code
    change**.
  - Acceptance: phases render in report order; `Growth` renders a heading with designed copy
    (*"nothing grew this night"*), not a blank, and the rail's length is unchanged; the transport
    (`⏮ ◀ ▶ ⏭`) steps keyframes without re-sorting; every number in a sentence renders through
    `world-numbers` with its family. (The rail's **GG-50 `render-all` declaration** — bounded at one
    turn's transcript, revisit above ~300 entries — lands in Phase 4's shared registry task; it is
    named here so it is not lost.)
  - Verify: `cd web\fusion-rpg-web; npm test -- PlaybackRail`.
  - Files: `src/stages/world/playback/PlaybackRail.tsx`,
    `src/stages/world/playback/PlaybackTransport.tsx`,
    `src/stages/world/playback/PlaybackRail.test.tsx`.
  - Dependencies: W72, W39.
  - Scope: M.

- [ ] **W76: Bind the table to the golden `first-light-turn.json`**
  - Description: the level that makes the rest real. `world-wire` generates the golden in Phase 0 and
    the map's arbitration row fixes its name as **`first-light-turn.json`**, beside `first-light.json`,
    naming the world it came from — this module consumes that exact path. It follows
    `WorldFixtureTests.cs:27-49`'s pattern: create a world, serialise the live route, assert byte
    equality, with the env-var bless switch. It also replaces `world.spec.ts:91`'s flat **404 stub**
    for `/api/world/first-light/turn/{n}`, which is why no golden exists today and why every row in
    plate 11 §L is a design rather than a covered behaviour.
  - Acceptance: the golden's every entry renders through the table with **no fall-through**; a single
    regex over all rendered output finds **no `:`-delimited engine token and no kebab-case id**;
    `world.spec.ts:91`'s 404 stub is gone; the fixture is byte-pinned and re-blessed only via
    `FUSIONRPG_BLESS_WORLD_FIXTURE`.
  - Verify: `cd web\fusion-rpg-web; npm test`; `dotnet test tests\FusionRpg.E2E.Tests`.
  - Files: `src/features/world/fixtures/first-light-turn.json`,
    `src/features/world/playbackTable.test.ts`, `web/fusion-rpg-web/e2e/world.spec.ts`,
    `tests/FusionRpg.E2E.Tests/WorldTurnFixtureTests.cs`.
  - Dependencies: W72, W73, W74; Gate A (`world-wire` generates the golden).
  - Scope: M.

---

### Gate B — the owner plays it

Not a milestone: **a phase boundary.** Phase 3 does not start until these are answered, and
**Phases 3 and 4 are re-argued from the answers** — including dropping or resequencing work that the
playtest shows is not the problem.

- [ ] All web suites green: `cd web\fusion-rpg-web; npm test` · `npm run build` · `npm run lint` ·
  `npm run test:e2e`; `dotnet test tests\FusionRpg.E2E.Tests` green.
- [ ] `#/world` still works, `features/world/` is intact, and the pure layer that moved
  (`turnPlayback.ts`, `labels.ts`) kept its tests green without edits.
- [ ] **Ten turns on `two-hearths`**, played by the owner, orders filed on the map.
- [ ] **Did you scroll?** — the page, at any point, at your actual window size.
- [ ] **Could you tell what happened last turn without reading an engine string?**
- [ ] **Did you ever reach for a control you could not find?**
- [ ] The stale-fog legibility check (W39) result is recorded, pass or fail.
- [ ] Answers written down here, verbatim, before any Phase 3 task is opened.
- [ ] Phases 3 and 4 re-argued against those three answers, and the re-argued order recorded in
  `tasks/world-stage-plan.md`.

# Tasks: world stage — Phases 3 and 4

Plan: [world-stage-plan.md](world-stage-plan.md) · Map (**arbiter**):
[world-stage-map.md](../docs/architecture/world-stage-map.md) · Specs:
[world-turn](../docs/architecture/world-stage/spec-world-turn.md) ·
[world-notify](../docs/architecture/world-stage/spec-world-notify.md) ·
[world-outliner](../docs/architecture/world-stage/spec-world-outliner.md) ·
[world-lenses](../docs/architecture/world-stage/spec-world-lenses.md) ·
[world-confirms](../docs/architecture/world-stage/spec-world-confirms.md)



**Cross-phase dependencies are named by module, not by id**, because the Phase 0–2 ids are that
half's to choose. Where a task says *"Phase 1 `world-hud`"* it means the last task of that module.

**Gate B is a phase boundary.** Nothing here starts until the ten-turn playtest is answered, and
every task below is re-arguable from what it found.

---

## Phase 3 — the empire is legible

The turn cluster that knows what is unresolved; two notification classes that flush on End Turn
except blockers; the outliner that makes 28 rows scannable and gives the map its first keyboard
entry point.

**Two things this phase does not do**, both settled in the map's arbitration and neither
re-arguable in a task: it does not wrap a `registerGlobalVerb` throw (collisions are prevented at
source, and `layers/system/keybindings.ts` is `world-lenses`' edit in Phase 4 — W58), and it does
not reintroduce `WASD`. **Arrows pan, `W` cycles.**

The remaining letter-key exposure — a player rebinding a rail action onto `w` or `o` — is closed by
the `conflictFor` widening that `spec-world-turn.md` §4 and `spec-world-outliner.md` §5 both record
as **ask-first**, because every stage binds through that file. This program states the requirement
and does not make the change; W41 makes registration deterministic, which is the half that is ours.

### `world-turn` — the cluster, not a button

- [ ] **W77: Derive the unresolved-legion set, in exactly one module**
  - Description: write `unresolvedLegions.ts` — the pure predicate `MovementRemaining > 0` (per-mille, `WorldDtos.cs:183`) intersected with the pending-order queue in `worldSelection.ts`. It is the single derivation behind both the turn cluster's count (W43) and the outliner's per-row flag (W56); two derivations is how a count of 2 comes to sit beside three flagged rows. It takes views in and rows out, with no DOM and no store access.
  - Acceptance: over a fixture of **10 legions** across `march` / `scout` / `hold`, with and without filed orders, the per-mille boundaries are asserted explicitly — 1000 and 500 count as unresolved when no order is filed, 0 never does; the module exports one function and holds no state; the same fixture at **6 legions** gives the count 6 minus the ordered ones.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/unresolvedLegions.ts`, `unresolvedLegions.test.ts`, `src/stages/world/turn/fixtures/legions.ts`.
  - Dependencies: Phase 0 `world-contract` (for `LegionView`).
  - Scope: S.

- [ ] **W78: Register the stage's global verbs through one owner**
  - Description: write `worldVerbs.ts` — the world stage registers its whole verb set in a single effect and returns the unregister array from the cleanup, following `stages/sanctum/SanctumStage.tsx:165-177`'s shape exactly, so ordering is deterministic rather than dependent on which component mounted first and leaving the stage frees every key it took. **It does not wrap the throw** (map arbitration §A): a swallowed `registerGlobalVerb` throw is a silently dead hotkey, which is worse than a loud failure.
  - Acceptance: mounting the stage registers its verbs and unmounting frees all of them, proven by mounting twice in one test without a duplicate-key throw; no component in `stages/world/` calls `registerGlobalVerb` directly (`shell/keymapGuard.test.ts` already fails a global verb bound outside `keymap.ts`); no `try`/`catch` around a registration call anywhere in the module.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/worldVerbs.ts`, `worldVerbs.test.tsx`.
  - Dependencies: Phase 1 `world-shell`.
  - Scope: S.

- [ ] **W79: End Turn in its four states, reading `Advanced` from the server**
  - Description: build `TurnCluster.tsx` at the bottom-right anchor `world-hud` owns — Ready, Nag, Hard-blocked and Committed–waiting, each with its own words. The commit names the turn it thinks it is ending (`WorldEndpoints.cs:122-123` refuses `turn.missing`), and the cluster leaves the waiting state only when the response reports `Advanced` (`:129`, `:135`) — never a local timer, never an optimistic advance (GG-15). The barrier is `WaitForAllCommitted` and has **no deadline**, so the waiting state must read as waiting at any duration.
  - Acceptance: each of the four states is asserted by its visible words rather than by a class; the Ready state renders the noun phrase *legions waiting on you* so a bare `0` cannot pass; the hard-blocked state's button **navigates to the blocker** rather than doing nothing, and carries the blocker's own sentence (GG-55); a commit whose response has `advanced === false` leaves the cluster in the committed state.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/stages/world/turn/TurnCluster.tsx`, `TurnCluster.test.tsx`.
  - Dependencies: W77, W78, Phase 1 `world-hud`.
  - Scope: M.

- [ ] **W80: The live count, and cycle-to-next on it**
  - Description: build `UnresolvedCount.tsx` — the count in words, with the cycle control **on** the count so that reading the problem and acting on it are one gesture. Once cycling starts the row names its current subject and that subject's movement (*"Ash Column — 500‰ movement left"*), and `MovementRemaining` renders through `world-numbers` with its per-mille family declared. Cycling is **player-initiated always**: this cluster never takes a selection from the player between actions, which is the Civ VI failure named in the spec. The key is `W`, registered through W41's owner.
  - Acceptance: the count never renders a bare digit; cycling walks the real unresolved set at 6 and at 10 legions and wraps; nothing auto-cycles, proven by a test that files an order and asserts the selection did not move; `W` is bound through `worldVerbs.ts` and no test asserts a `WASD` pan.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/UnresolvedCount.tsx`, `UnresolvedCount.test.tsx`.
  - Dependencies: W77, W78, W79.
  - Scope: M.

- [ ] **W81: The two blocking classes, with the hard list shipping empty**
  - Description: write `blockingClasses.ts` — `NAGGING_EVENTS` populated, `HARD_BLOCKING_EVENTS` an **empty array** with its emptiness stated in a doc comment rather than implied. ES2 shipped a battle notification into the hard class, its community called it a feature not a bug, and Amplitude patched it back out; the default is the lesson. Nagging appears on attempt, relabels the button to *End turn anyway*, and never stops the player.
  - Acceptance: `HARD_BLOCKING_EVENTS` is empty and a test whose failure message points at `spec-world-turn.md` §2 fails the moment an entry is added; the nag path costs exactly one extra keypress and never opens a modal; battle results are **not** in either list — they are a `world-notify` rail category.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/blockingClasses.ts`, `blockingClasses.test.ts`.
  - Dependencies: W79.
  - Scope: S.

- [ ] **W82: Prove the button's state cannot disagree with the world's**
  - Description: property tests over generated worlds, because Humankind's own bug forum describes this defect family as *"not a single bug, but multiple different bugs that have the same symptom"* — alongside the filed *"Turn Button Shows End Turn When Moves Are Still Available."* A single example test cannot close a family. The blocker's correctness is therefore a first-class testable surface, not an incidental of W42.
  - Acceptance: over generated worlds at 6–10 legions, if any legion satisfies the unresolved predicate the button is **never** in the Ready state, and if none does it is never in the nag or blocked state; the generator covers filed-then-withdrawn orders and a legion at exactly 0‰; a deliberately inverted predicate makes the property fail (the test is proven to notice).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/turnStateProperty.test.ts`, `src/stages/world/turn/fixtures/legions.ts`.
  - Dependencies: W77, W79, W81.
  - Scope: M.

- [ ] **W83: The force-end hatch, reachable by pointer**
  - Description: build `forceEnd.ts` and the *end anyway* control beside the blocker's sentence. This is the insurance that a state disagreement can never cost a session, and it is the shipping-critical half of the hatch — the keyboard binding is blocked on a verified fact, not a preference (see the owner decision below). File-orders belongs here too: it commits the pending queue as one batch, shares `worldSelection.ts`'s `PendingOrder` list with `world-targeting`, adds nothing to it, and acknowledges immediately without showing the orders as filed until the server accepts them (GG-15).
  - Acceptance: the hatch ends the turn from a hard-blocked state using the pointer alone; no test asserts a `⇧⏎` binding, and a comment at the binding site names `useGlobalKeys.ts:25` as the reason; file-orders renders an acknowledged-but-not-filed state between the click and the response.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/forceEnd.ts`, `forceEnd.test.tsx`, `src/stages/world/turn/TurnCluster.tsx`.
  - Dependencies: W79, W81.
  - Scope: S.

- [ ] **Owner decision: how the force-end shortcut gets a key** — `web/fusion-rpg-web/src/shell/useGlobalKeys.ts:25` is `dispatchGlobalVerb(event.key)` and carries **no modifier state at all**, so `Shift+Enter` and `Enter` arrive at the registry as the same key `"Enter"` and the plate's `⇧⏎` force-end binding is **not expressible in the shipped keymap**. Two resolutions, both costed in `spec-world-turn.md` §4: **(a)** teach the keymap a canonical modified-key form (`"Shift+Enter"`) produced at the listener and consumed by the registry — correct and small, but it touches every stage's keymap and is therefore ask-first; **(b)** bind the hatch to an unmodified key of its own and keep `⏎` for the ordinary end — ships with no shell change, and costs the gesture's family resemblance to Civ VI's. **The pointer path (W46) ships either way**, so this constrains the shortcut and nothing else. Needed before W46 is called done, not before W40 starts.

### `world-notify` — two classes, and half of it already ships

- [ ] **W84: Give the shipped toast an action button and a category**
  - Description: two additive changes to the working band-4 stack, not a second implementation. `ToastEntry` (`shell/toastStack.ts:5-10`) gains an optional `action: { label, run }` and a `category`; `Toasts.tsx` renders the button. The container is already `pointer-events-none` with `pointer-events-auto` on the card (`Toasts.tsx:11-27`), so a button inside works with no layout change. Timers, cleanup and `clear()` (`toastStack.ts:29-51`) are reused unchanged — `clear()` is what W49's flush calls for the toast half.
  - Acceptance: every existing toast test stays green with no edit (the change is additive); a toast with an `action` renders a button that runs it and dismisses; a toast without one renders exactly as before; the stack still never blocks input.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/shell/toastStack.ts`, `src/shell/Toasts.tsx`, `src/shell/toastStack.test.ts`.
  - Dependencies: None.
  - Scope: S.

- [ ] **W85: The closed category list, and its default channels**
  - Description: write `categories.ts` — the eight categories in `spec-world-notify.md` §4 with their default channels. The rule that makes the list govern itself: **everything below the declared top tier starts on the rail and has to earn a promotion**, so a new category arriving on Toast by default is a spec change rather than a code change. Categories map from `world-playback`'s translation table — one vocabulary, two consumers; this module never parses an engine token.
  - Acceptance: every category has a default channel; a test asserts **no category defaults to Toast unless it is in the declared top tier**, so adding a Toast default is a visible diff on the list; battle results default to the **rail** (the ES2 retraction), and *"ground will be released next turn"* defaults to Toast.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/categories.ts`, `categories.test.ts`.
  - Dependencies: Phase 2 `world-playback`.
  - Scope: S.

- [ ] **W86: The rail store, and the flush that fires on `advanced`**
  - Description: write `notifyRail.ts` — a pure store holding items in five states, with the one rule in one line so it cannot drift: `flush = (items) => items.filter(i => i.blocking)`. It fires on `WorldTurnCommitDto.Advanced`, **not on the button press**, because a commit that did not advance (a resend, a barrier still waiting) has not ended a turn. Dismissing removes an item from the feed and never from the record — `world-playback` holds the record.
  - Acceptance: a commit with a mixed feed leaves only blockers; a commit with `advanced === false` leaves the rail untouched; a dismissed item is still retrievable from the turn report; the store is pure — no React import, no fetch.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/notifyRail.ts`, `notifyRail.test.ts`.
  - Dependencies: W85, W79.
  - Scope: M.

- [ ] **W87: The passive right rail, and its five item states**
  - Description: build `NotifyRail.tsx` and `RailItem.tsx` — band 1, right-anchored above the outliner, scrolling **inside its own bounded shell** (GG-61) so the stage behind it never moves. Five states: unread, opened, dismissed, minimized, blocking. Opening and dismissing are two gestures with two outcomes. A blocker has **no close control** and shows its channel control **visible but locked**, so the player learns the rule instead of wondering why the switch did nothing (GG-55).
  - Acceptance: each state is asserted by a **non-colour** channel — unread carries a dot *and* bold weight *and* a rule — queried by role and accessible name, never by class; the blocking state has no close control and a locked, visible channel control; the rail declares no `z-index` (`shell/bandGuard.test.ts` fails a surface that does); scrolling the rail does not scroll the stage.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/NotifyRail.tsx`, `RailItem.tsx`, `NotifyRail.test.tsx`, `RailItem.test.tsx`.
  - Dependencies: W86, Phase 1 `world-hud` (§8d.3's band-1 scrim exemption, and the `PanelShell.tsx:61` fix).
  - Scope: M.

- [ ] **W88: The channel control, on the notification and in settings**
  - Description: build `ChannelControl.tsx` and `channelSettings.ts` — *"Show skirmish results as… Toast · Rail · Off"*, applied **to the category and not to this one message**, with the category named in the sentence so the scope of the change is never in doubt. This is Amplitude's own correction to ES2's options-menu-only model: the moment a player wants to change this is the moment one is annoying them. The same list appears in settings, which is the only place to find a category already silenced, so it must be complete including locked categories with their reason. These are **player settings**, persisted alongside the tooltip lock gesture — not tunables.
  - Acceptance: changing a channel from a notification changes it for the category and persists across a reload; the settings list and the on-notification control read the same store and cannot disagree, asserted by a test that changes one and reads the other; a silenced category never reaches the toast stack at all.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/ChannelControl.tsx`, `channelSettings.ts`, `channelSettings.test.ts`.
  - Dependencies: W85, W87.
  - Scope: M.

- [ ] **W89: Count the clicks, and prove no notification opens a layer**
  - Description: the module's only quantitative gate, written as counted `userEvent` interactions rather than as prose — the four rows of `spec-world-notify.md` §7, against Endless Legend's audited four-clicks-per-notification. Plus the guard-shaped assertion that keeps D6 honest: **no code path in this module opens a band-3 layer.** A toast may carry a button that opens one; that is the player asking.
  - Acceptance: acknowledge a routine event = **0** interactions, act on an important one = **1**, clear the feed = **0**, change a category's channel = **1**; the fixture is the busiest turn the 6–10-legion target can produce and the visible toast stack stays at the cap of three with the remainder behind a count; driving a turn containing a fade warning leaves the layer stack empty.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/clickBudget.test.tsx`, `src/stages/world/notify/noBandThree.test.tsx`.
  - Dependencies: W87, W88.
  - Scope: M.

### `world-outliner` — 28 rows, and the map's first keyboard entry point

- [ ] **W90: The pure outliner model — grouping, flagged-first sort, three filters**
  - Description: write `outlinerModel.ts`, views in and rows out with no DOM. Two groups with counts, **anything flagged sorts above anything quiet**, stable below that so a row never moves under the pointer for a reason the player cannot see. Three **exclusive** filter chips — *needs orders* (W40's predicate, imported not re-derived), *fading*, *all* — because at 28 rows the player does not know the name they are looking for, they know the condition. §4.3's earlier *"short by construction"* claim is superseded by §8e.3 and must not be reproduced.
  - Acceptance: the model runs over a **10 legion + 18 sector = 28 row** fixture; the sort is stable below the flag and a test proves it by re-running with the input order reversed; each filter predicate is asserted independently; the unresolved flag is `unresolvedLegions.ts`'s export, verified by a test that stubs that module and sees the rows change.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/outlinerModel.ts`, `outlinerModel.test.ts`, `src/stages/world/outliner/fixtures/empire28.ts`.
  - Dependencies: W77, Phase 0 `world-contract`.
  - Scope: M.

- [ ] **W91: The listbox — real options, one roving tab stop**
  - Description: build `Outliner.tsx` and `OutlinerFilter.tsx`. `role="listbox"`, rows `role="option"` with `aria-selected`, group headers as real headings with their counts in the accessible name, and **one roving `tabIndex`** so the whole list is a single tab stop and arrows move within it. No such pattern exists anywhere in the app today, so this module introduces and owns it. The defect to avoid is the one plate §I.1 drew: `<div>`s with `cursor:pointer`, no `role`, no `tabindex`, and a class-driven focus ring on an element the browser will never focus.
  - Acceptance: exactly one row has `tabIndex={0}` at all times — including after a filter changes which rows exist and after the active row is filtered away; no `<div onClick>` remains in the module; the active filter chip is stated **in words**, never by fill alone; the list body scrolls and the stage does not move to compensate (GG-61).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/Outliner.tsx`, `OutlinerFilter.tsx`, `Outliner.test.tsx`.
  - Dependencies: W90, Phase 1 `world-hud`.
  - Scope: M.

- [ ] **W92: The two row types, every fact in a family and a non-colour channel**
  - Description: build `LegionRow.tsx` (stance · movement · supply runway · unresolved flag) and `SectorRow.tsx` (net flow · fade risk · will-release). Three families appear in one row — `500‰`, `4 turns`, `+61 loam` — and they are not interchangeable, so every number goes through `world-numbers` with its family declared. A short supply runway loses **pips**; it does not change hue. Nothing states a fact below 12px, glyph text included. Rows whose field is still a `world-wire` projection render their pending reason, never a zero.
  - Acceptance: every row state — fading, releasing, no-orders, short runway — is findable by **text or glyph** queried by accessible name, with colour removed; no row carries a fifth fact (that is the inspector escaping onto the edge); the outliner lists the player's own legions only; a legion row and the turn cluster's count agree on the same fixture.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/LegionRow.tsx`, `SectorRow.tsx`, `LegionRow.test.tsx`, `SectorRow.test.tsx`.
  - Dependencies: W90, W91, Phase 1 `world-numbers`.
  - Scope: M.

- [ ] **W93: The keyboard path, with the pointer never touching the canvas**
  - Description: wire `O` (through W41's `worldVerbs.ts`), `↑`/`↓`, `⏎` and `Esc`, and the select-and-centre dispatch that has never existed — `worldSelection.ts` already carries `select-sector` and `select-entity` and nothing in the feature ever dispatches them from a list. **Focus and selection are drawn and behave differently**: arrows move focus and change nothing else, `⏎` selects and asks the camera to centre. Centring is a request to `world-shell`'s `viewBox`, never a mutation of it from here and never read back. `Esc` hands focus back to the stage, and `keymap.ts:125-135` already pops an open layer first.
  - Acceptance: a test drives the whole path with **no pointer events at all** — `O` focuses, arrows move focus while asserting selection did not change *and the camera was not asked to move*, `⏎` selects and centres, `Esc` returns focus; focusing four rows down leaves exactly one `aria-selected`, still on the original row.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run lint`
  - Files: `src/stages/world/outliner/Outliner.tsx`, `outlinerKeyboard.test.tsx`, `src/stages/world/turn/worldVerbs.ts`.
  - Dependencies: W78, W91.
  - Scope: M.

---

## Phase 4 — depth, and retirement

Lenses, the band-3 confirms, and then the retirement task: `features/world/` deleted, its **three**
standing exemptions retired in the same change, and the GG-50 registry closed.

### `world-lenses` — six exclusive layers over one map

- [ ] **W94: The closed catalog of six, and the reducer behind them**
  - Description: write `lensCatalog.ts` (id, key, label, encoding contract, server cost) and `lensState.ts` — a pure reducer holding **both** `active` and `playerChosen`, where auto-activation writes only the first. Exclusive, always: a radio group, never checkboxes, because two layers of meaning over one map is how a player stops being able to tell what a colour means. **Ownership is the home lens** — pressing the active lens's own key returns to it, so there is always one key that means *show me the map again*.
  - Acceptance: the catalog has exactly **six** entries and a test asserts the length, which is also the assertion that Placement is not a lens; every reducer path leaves exactly one lens active and the type does not permit zero or two; pressing `1` while on Ownership is a no-op, not a toggle to nothing.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/lensCatalog.ts`, `lensState.ts`, `lensState.test.ts`.
  - Dependencies: Phase 1 `world-render`.
  - Scope: M.

- [ ] **W95: Refuse a rebind onto `1`–`9`, at the source**
  - Description: `layers/system/keybindings.ts` currently lets `rebind` write any key (`:102-112`) and `conflictFor` scans only the eight `BindableActionId`s (`:86-93`), so a player who binds Relics to `3` makes this stage throw on mount — on a code path no test covers. `information-architecture.md:172` already declares `1`–`9` *"Stage-specific hotbar · owned by the current stage"*, so a digit rebind is **already a rule violation**; this task enforces the rule that exists rather than defending against it. **A defensive `try`/`catch` around registration is explicitly not the fix** (map arbitration §A): it would hide a broken rebind behind a silently dead hotkey. `world-lenses` owns this edit; `world-turn` and `world-outliner` consume it.
  - Acceptance: `rebind("relics", "3")` is refused and returns a reason the Controls screen can show (GG-55); the eight existing letter defaults still rebind freely and every existing keybindings test stays green; a test asserts the world stage still mounts after a refused digit rebind.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/layers/system/keybindings.ts`, `keybindings.test.ts`, `src/layers/system/SystemLayer.tsx`.
  - Dependencies: None (lands before W96).
  - Scope: S.

- [ ] **W96: The picker, its readout, and hotkeys `1`–`6`**
  - Description: build `LensPicker.tsx` in the bottom-left map-controls cluster beside zoom and fit, and `useLensHotkeys.ts` registering `1`–`6` through W41's `worldVerbs.ts` owner and freeing them on unmount. The readout **always names the active lens in words** (`1 / 6 · Ownership`), which is the property ES2's zoom-coupled Scan view cannot have: when a layer's identity is only its zoom depth, two layers converging is an invisible bug. Band 1, anchored, and **not scrimmed** when a band-2 inspector opens (§8d.3).
  - Acceptance: the active lens's name is on screen at all times; `1`–`6` select directly; mounting the stage twice in one session does not throw and unmounting frees the digits for the next stage's hotbar; the picker declares no `z-index`.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/stages/world/lenses/LensPicker.tsx`, `useLensHotkeys.ts`, `LensPicker.test.tsx`.
  - Dependencies: W94, W95, W78.
  - Scope: M.

- [ ] **W97: Lens 4 pays for itself — the `?lifelines=true` read, with a designed loading state**
  - Description: lens 4 is the one that costs a network round-trip, and the server says why in its own words: *"Reconnection cost is an O(holdings⁴) sweep and the overlay it feeds is off by default, so it is asked for rather than always paid for"* (`WorldEndpoints.cs:48-51`). The client already threads it — `useWorldState(worldId, { lifelines })` puts the flag in the query key (`lib/bus/world.ts:80`) — so selecting lens 4 is a different cache entry and a fetch, not a re-render. GG-17 makes loading a designed state: the lens-4 chip carries a pending treatment and **the map keeps drawing the previous lens underneath until the data arrives. It must never blank.**
  - Acceptance: selecting lens 4 changes the query key and issues the request; the map renders the previous lens for the whole in-flight window and a test asserts the canvas is never empty; leaving lens 4 and returning within `staleTime` issues no second request; the other five lenses have no loading state.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/useLensData.ts`, `useLensData.test.tsx`, `src/stages/world/lenses/LensPicker.tsx`.
  - Dependencies: W94, W96.
  - Scope: M.

- [ ] **W98: Auto-activation, which announces itself and restores**
  - Description: wire the four triggers in `spec-world-lenses.md` §3 — Raise opens the placement overlay (`world-targeting`'s, **not a lens**), Ward-a-road and an out-of-supply legion select lens 4, a fade warning opened from the rail selects lens 3 centred on its sector. Two promises make unasked activation safe: it **announces itself** (an information layer that swapped silently is indistinguishable from a rendering bug), and it **restores rather than resets** — Esc or completion puts back the lens the *player* chose, not Ownership. Placement draws **over** the current lens and restores it on exit; that restore contract is this module's only obligation to `world-targeting`.
  - Acceptance: choose lens `6`, select an out-of-supply legion, assert `active === "supply"` **and** `playerChosen === "danger"`, Esc, assert `active` is back to `danger` — this is the test that catches the obvious wrong implementation; each of the four triggers changes the picker's visible state and the readout's words; opening a targeting overlay and closing it restores the lens that was showing.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/lensState.ts`, `lensAutoActivate.test.ts`, `src/stages/world/lenses/LensPicker.tsx`.
  - Dependencies: W94, W96, Phase 2 `world-targeting`.
  - Scope: M.

- [ ] **W99: Six lenses, six colour-independence tests**
  - Description: a lens is by nature a re-colouring, so this is where GG-27 and GG-30 are most at risk. The evidence is blunt: the most-subscribed mods for both Endless games are palette expansions, and a 2,697-subscriber ES2 mod exists solely because *"the color of the label indicating a planet is colonizable is exactly the same as the color indicating it is not colonizable."* Per lens: ownership is four **patterns**, loam flow an **arrow plus a signed number**, fade risk a **word**, supply **line weight plus a caption**, intel age a **hatch plus a number of turns**, danger a **count of diamonds**.
  - Acceptance: six tests, one per lens, each asserting the fact is carried by a text or pattern channel queried by role or text rather than by class name — a regression will land in exactly one of them; the loam lens renders `—` and never `0` for ground that is not yours; every lens survives a greyscale rendering with its fact intact.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run lint`
  - Files: `src/stages/world/lenses/lensEncoding.test.tsx`, `src/stages/world/lenses/lensCatalog.ts`.
  - Dependencies: W94, W96, Phase 1 `world-render`.
  - Scope: M.

### `world-confirms` — three dialogs, none of which opens itself

- [ ] **W100: The warden gate, as a pure function of the balance**
  - Description: write `wardenGate.ts` — `needsSayItBack(balance, fee, upkeepPerDay) => balance < fee + upkeepPerDay`. Step 2 is a function of the balance, not a flag someone remembers to set, and the threshold is computed from **the same values the engine charges** (`ContractPolicy.UpkeepPerDay`, taken at bind in `RpgStore.Contracts.cs:316`), never a magic number.
  - Acceptance: the boundary is asserted on both sides and exactly at `fee + upkeepPerDay`; the function has no store access and no React import; the balance comes from `/api/souls/{playerId}` the client already reads (`lib/bus/demons.ts:135-136`).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/wardenGate.ts`, `wardenGate.test.ts`.
  - Dependencies: None.
  - Scope: XS.

- [ ] **W101: Commit a legion — six stakes, and a band is never a count**
  - Description: build `CommitLegionDialog.tsx` over `shell/DialogShell.tsx` (which pushes and pops the layer stack at `:30-37`, so Esc pops one layer and the stage behind it never unmounts). Plate 03 counted one stake; plate 11 §K.1 counts four, plus the two facts needed to judge them. The stake list is **data**, so a missing row is a visible diff rather than a forgotten paragraph. The fade row shows **both numbers** — *"fades faster"* without them is a mood, not a fact. It closes with the truth about timing: *"A fight is likely. Nothing resolves until you end the turn."*
  - Acceptance: all six rows in §1 are present by accessible text — garrison leaving, carried supply, burn clock, runway turn, the fade with before and after, and what is waiting; a `ForceView` with `exact: false` renders the **band name and ceiling** and a test asserts the exact strength never appears; a row whose `world-wire` projection is still pending renders its reason, never a zero; the dialog declares no `z-index`.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/CommitLegionDialog.tsx`, `CommitLegionDialog.test.tsx`, `src/stages/world/confirms/stakeRows.ts`.
  - Dependencies: Phase 2 `world-inspector`, Phase 0 `world-commands`.
  - Scope: M.

- [ ] **W102: Bind a warden — permanent, and the fee is the first day's upkeep**
  - Description: build `BindWardenDialog.tsx` step 1. This is the one act on the stage the rest of the game will not undo: `ReleaseContract` checks the warden flag **before every other release blocker** and refuses unconditionally (`RpgStore.Contracts.cs:351-353`). So the copy states the loss in full with no hedging. **The fee taken now and the daily upkeep are the same number**, because binding charges day one (`fee = ContractPolicy.UpkeepPerDay(...)`, `:316`) — the dialog shows two rows because they are two obligations, shows the same rate twice, and **says so**. The verb is **"Bind a warden here"**, never "Ward": `WardLevel` sits on a lane and `WardenBindingId` on a sector, and an earlier plate called both "Ward" so choosing the irreversible one got you the road overlay.
  - Acceptance: the dialog contains the words *"can never be released"* and *"You do not keep the demon."* — a copy test on purpose, because that is the sentence GG-22 requires and the one a later refactor would soften; the five rows (slot spent, fee, never-ending upkeep, permanence, exemption gained) are all present, with one sentence stating the fee and the daily rate are the same number; the four engine refusals — `capacity.full`, `souls.insufficient`, `contract.already-bound`, `specimen.missing` — render as sentences **before** the act (GG-55); the word "Ward" appears nowhere in this dialog.
  - Verify: `cd web\fusion-rpg-web; npm test` then `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `src/stages/world/confirms/BindWardenDialog.tsx`, `BindWardenDialog.test.tsx`.
  - Dependencies: W100, Phase 0 `world-commands` (the first production `BindAsWarden` call site).
  - Scope: M.

- [ ] **W103: Step 2, and only when the balance cannot carry it**
  - Description: the second confirmation appears **only** when `balance < fee + upkeepPerDay`. It states the arithmetic and requires typing `bind`. With souls to spare, step 1 is the whole confirm — a second step charged on every bind would be trained away within a week and would then be worthless on the one occasion it mattered. Typing `bind` is recall and GG-24 forbids recall in the general case; this is the deliberate exception, and the reason is **stated on the dialog**: the friction *is* the safeguard, and it applies only where an unpayable permanent debt is being taken on.
  - Acceptance: with a comfortable balance the flow completes in one step; below the threshold step 2 appears, the confirm button stays **disabled with its reason attached** until `bind` is typed, and the arithmetic sentence names the balance, the fee and the daily rate; the threshold comes from W63 and is not recomputed here.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/BindWardenDialog.tsx`, `BindWardenDialog.test.tsx`, `src/stages/world/confirms/wardenGate.ts`.
  - Dependencies: W100, W102.
  - Scope: S.

- [ ] **W104: The abandon warning, drawn before the turn**
  - Description: build `ReleaseGroundDialog.tsx`. The engine already computes this a full turn early with the **same selection** it will use to apply the fade — `LoamForecast.Weakest` (`LoamForecast.cs:19-31`) is the function `LoamPhases.Pressure` calls at the moment of the act (`LoamPhases.cs:138`) — so the warning and the event cannot disagree, which is what licenses stating it this bluntly. What is missing is only that nothing surfaces it: a player who first learns about it from `loam.lost:frost-mire` in the turn report has been told **after** the decision was taken for them. The dialog names the reach and its arithmetic, the sector that goes and why it was chosen, what goes with it, whether losing it splits the territory, and then **what would stop it** — pour in the shortfall (with what a legion is actually carrying, so the option is checkable) or bind a warden (with its reason if every slot is taken).
  - Acceptance: the dialog is reachable from the band-4 toast's *Show me* and from the fade-risk lens, and from nowhere else; it names both halves of the arithmetic and the split-territory consequence; every offered option exists today.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/ReleaseGroundDialog.tsx`, `ReleaseGroundDialog.test.tsx`.
  - Dependencies: W101, Phase 2 `world-inspector`.
  - Scope: M.

- [ ] **W105: Two gates — nothing opens itself, and nothing offers a choice that does not exist**
  - Description: the two tests that would otherwise fail silently. **No dialog opens itself**: GG-53 gives exactly one class of event the right to take a blocking layer unprompted and D6 declares it *run-ending results only*; a world notification is never one. The fade warning is the tempting exception — it is the most important thing that can happen in a turn — and it still arrives as a toast. **And no surface says *"choose what to release"***: `LoamPhases.Pressure` picks the victim itself every turn and `WorldCommandKinds` declares exactly seven kinds with no `abandon` / `cede` / `release` among them (`WorldCommand.cs:7-34`), so that copy is a lie the player catches on their first shortfall.
  - Acceptance: a test renders the stage, drives a turn containing a fade warning, and asserts **no band-3 layer is on the stack**; a copy scan asserts *"choose what to release"* and its synonyms appear nowhere, and the scan **reads `WorldCommandKinds` rather than a flag**, so it turns itself off the day the cede order lands.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/noSelfOpen.test.tsx`, `src/stages/world/confirms/forbiddenCopy.test.ts`.
  - Dependencies: W101, W102, W104.
  - Scope: S.

### The two closing tasks

- [ ] **W106: Register five collection surfaces, and move the count to 13**
  - Description: GG-50 is a Tier-1 gate and it was in **zero of the fifteen specs** until the 2026-09-03 audit. `web/fusion-rpg-web/src/ui/volumeMatrix.test.ts` is an *exhaustive* registry closing with `expect(COLLECTION_SURFACES).toHaveLength(8)`, so landing this program without registering its surfaces **turns a shipped, green test red**. Add the five rows from the map's arbitration §E — Outliner, World notification rail, Turn playback keyframe rail, Sector inspector slot rows, Sector inspector force rows — each with the strategy and the reason its own spec declares, and change `toHaveLength(8)` to `toHaveLength(13)`. All five are `render-all`, and that is a real result rather than a convenient one: every world-stage collection is bounded by something structural — a map tier, a per-turn flush, authored sector content, or the fact that enemy forces render as bands rather than per-unit rows. The world stage adds **no** `virtualize` entry.
  - Acceptance: `COLLECTION_SURFACES` has 13 entries and the length assertion reads 13; each new row states a real reason naming its structural bound, and the existing *"every entry states a real reason, not a placeholder"* test passes over all 13; the existing `virtualize` count is still exactly one (Creatures).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/ui/volumeMatrix.test.ts`.
  - Dependencies: W87, W91, W105, Phase 2 `world-inspector` and `world-playback`.
  - Scope: XS.

- [ ] **W107: Retire the three exemptions — and edit a green test to assert its opposite**
  - Description: `#/world` is currently exempt from three things at once, and they retire **in the same change** so the tree is never half-migrated. (1) `src/theme/hexGuard.ts:27` lists `"features/world/"` in `SKIPPED_PATH_PREFIXES`; per the map's arbitration `world-render` deletes that entry in the change that makes the map token-only, and this task confirms it is gone rather than re-deleting it. (2) The **GG-7 reachability exception** — `e2e/checkpoint-f.spec.ts` documents *"all redirect, none 404, except /world (T16 excludes World from this sweep)"* at `:10`. (3) The **shell's redirect exception** — `src/app/routes.tsx:89-96` still serves the legacy `WorldPage` on its own route while `roster`, `expeditions`, `fusion` and `pacts` all `Navigate` away.
    **`e2e/checkpoint-f.spec.ts:231` is a passing test asserting `/world` stays on its own route**, so retiring the exemption means editing a green test to assert its opposite. **The replacement assertion, stated here so it is not improvised at the keyboard:** the test is renamed *"`/world` reaches the world stage, not the legacy page"* and asserts `response.ok()`, that the world stage's own `data-testid` is visible, and that the legacy page's markers (`chunk-fallback-world` and `WorldPage`'s sidebar) are **absent**. That assertion holds whether or not `world-shell` kept the `#/world` URL in Phase 1, so it does not smuggle in a route decision that is not this task's to make. The header comment at `:10` and the `describe` title at `:199` both lose the exception clause.
  - Acceptance: `SKIPPED_PATH_PREFIXES` contains `"game/"` only; `routes.tsx` no longer imports `@/features/world/WorldPage`; the renamed checkpoint-f test passes against the new stage; no test anywhere still asserts the legacy page renders.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`; then the Playwright suite for `checkpoint-f.spec.ts`
  - Files: `src/theme/hexGuard.ts`, `src/app/routes.tsx`, `e2e/checkpoint-f.spec.ts`.
  - Dependencies: W106, and every module task above.
  - Scope: M.

- [ ] **W108: Delete `features/world/` and the E2E spec that drives it**
  - Description: the pure layer has already **moved, not died** (map arbitration §A) — `worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts` and both fixtures relocated to `stages/world/` at their consuming module's phase. What is left is the old page and its components: `WorldPage.tsx`, `SectorNode.tsx`, `LaneEdge.tsx`, `LegionMarker.tsx`, `LoamGauge.tsx`, `SectorPanel.tsx`, `worldTypes.ts` and their colocated tests. Deleting `WorldPage.tsx`, `SectorNode.tsx` and `LaneEdge.tsx` is also what removes the last three production imports of `@xyflow/react` (`decisions.md:93`). **`e2e/world.spec.ts` drives the old page and cannot survive this** — its ten tests either move to the new stage (the fog-treatment and band-name assertions are still the right questions) or are deleted with their reason recorded in the task's done-note; leaving it in place would go red on the same commit.
  - Acceptance: `src/features/world/` does not exist; `grep -r "@xyflow/react" src/` returns nothing and the dependency is dropped from `package.json`; `e2e/world.spec.ts` is replaced or deleted with each of its ten tests accounted for; `npm run build` succeeds with no unresolved import.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build` then `npm run lint`
  - Files: `src/features/world/` (deleted), `e2e/world.spec.ts`, `package.json`.
  - Dependencies: W107.
  - Scope: L (a deletion — many paths, one decision).

### Checkpoint C — complete

- [ ] All **15 modules** built.
- [ ] `features/world/` deleted and its **three** standing exemptions retired **in the same change** — the hex-guard prefix, the GG-7 reachability exception, and the shell's redirect exception.
- [ ] The GG-50 registry stands at **13**, with no `virtualize` entry added by this program.
- [ ] The four boundary guards green: `.\scripts\guard-single-writer.ps1`, `.\scripts\guard-secondary-no-unity.ps1`, `.\scripts\guard-funnel-delta.ps1`, `.\scripts\guard-dal.ps1`.
- [ ] The full web suite green (`cd web\fusion-rpg-web; npm test`, `npm run build`, `npm run lint`) and the full .NET suites green: `dotnet test tests\FusionRpg.Core.Tests`, `...\FusionRpg.Data.Tests`, `...\FusionRpg.Guard.Tests`, `...\FusionRpg.E2E.Tests`.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).
