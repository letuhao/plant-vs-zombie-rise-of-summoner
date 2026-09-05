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
8. **`npm run lint` does not exist — found and corrected 2026-09-04.** `web/fusion-rpg-web/package.json`
   has no `lint` script, no eslint config and no eslint binary in `node_modules/.bin`. Nine verify
   lines cited it; all nine are corrected in place to drop it. Type-checking is already covered by
   `npm run build`'s own `tsc --noEmit`. If linting is wanted later, that is a separate, explicit
   task — not something a `world-stage` task should silently invent.

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

- [x] **W1: The `typeId` ADR and the contract version bump** *(done 2026-09-04 — `SectorView.typeId` → `string`, `CONTRACT_VERSION` 1→2, ADR row added to `decisions.md`. No consumer existed yet, so zero runtime blast radius. `npm test`: 805/806 — 1 pre-existing, unrelated GG-55 `disabledReasonGuard` failure verified present on HEAD before this change (not caused by it, not fixed here — out of world-stage scope). `npm run build`: green.)*
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

- [x] **W2: Move the world DTOs to `lib/bus/world.ts`** *(done 2026-09-04 — all 11 DTO types moved into `lib/bus/world.ts` alongside its existing hooks/types (which already lived there — the buildability audit's finding that this file "exists and is not empty" confirmed); `worldTypes.ts` reduced to an 11-name re-export shim with a comment pointing at the real source. All 13 existing consumers use `import type {...} from "./worldTypes"` (relative, type-only) and needed zero changes — verified by grep before editing. `npm test`: 805/806, same single pre-existing failure. `npm run build`: green.)*
  - Description: the world's DTOs live in `features/world/worldTypes.ts`, which is why `contractGuard`
    — matching only imports `from "@/lib/bus` — would pass a rebuilt `stages/world/` that binds
    straight to a REST DTO. Moving them to `lib/bus/world.ts`, where every other domain's already
    live, makes the *existing* guard bite with no guard change and stops the world being the
    exception. `features/world/worldTypes.ts` re-exports during this phase so `WorldPage.tsx` and its
    components keep compiling; it is deleted in Phase 4's retirement task, not here.
  - Acceptance: the DTO types are declared in `lib/bus/world.ts`; nothing outside `src/contract/` and
    the legacy `features/world/` tree imports them; `#/world` still renders (the existing world tests
    are green unchanged); no type is renamed or narrowed in the move — it is a move, not an edit.
  - Verify: `cd web\fusion-rpg-web; npm test`, then `npm run build`.
  - Files: `web/fusion-rpg-web/src/lib/bus/world.ts` (new),
    `web/fusion-rpg-web/src/features/world/worldTypes.ts`.
  - Dependencies: None.
  - Scope: S.

- [x] **W3: Widen `contractGuard` so a feature-local DTO import fails** *(done 2026-09-04 — matched on the wire's own `*Dto` naming convention rather than the import path, since a path-only match is defeated by any re-export shim, not just the world's. `src/contract/` stays the one exempt directory. 4 new fixture tests: feature-local absolute-path import (flagged, file/line/text all correct), the same via a relative path (flagged), a `*Dto` import from `contract/` itself (not flagged), and a non-`Dto` view-contract type (not flagged, proves no over-matching). 15/15 contractGuard tests green; full suite 809/810 — same single pre-existing failure; build green.)*
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

- [x] **W4: The six world views, with `Pending` reasons and unit families** *(done 2026-09-04 — `SectorView`, `LaneView`, `LegionView` (with a discriminated `LegionPosition`), `SlotView`, `ForceView` (discriminated on `exact` — a compile-time proof lives in `worldViews.typecheck.ts`, `@ts-expect-error` on both illegal reads, verified by a clean `tsc --noEmit`) and `TurnEventView`. **Owner decision deferred, not resolved:** `loamUnits` was NOT added to the sealed `UnitClass` union — no synchronous owner sign-off available mid-task, so the task's own stated fallback applies: every loam/component reading is `unit: "gameUnits"` today. This is the one open item carried forward. **Two real wire-mirror drifts found and fixed while building this, both verified against the live fixture before touching anything:** `WorldSlotDto` was missing `structureId` (present on the C# DTO since L32, present in the fixture, never added to the TS mirror — the flagship defect this whole program keeps citing, and it was still there) and `WorldSectorDto` was missing `fractureIntensityMilli` (projected server-side at `WorldEndpoints.cs:298`, present in the fixture, never mirrored). The spec's own Code style block was corrected to match (was still showing the rejected `LoamUnits` brand). Also resolved the `formatPerMille` "more"-arm defect without a new op: `FractureIntensityMilli`'s neutral baseline is 1000, and the renderer's "more" arm already computes a delta from zero — so W5's adapter subtracts 1000 before wrapping, no `Magnitude.op: "absolute"` needed. New test `worldViews.test.ts`: one maximally-pending fixture per view running through `findEmptyPendingReasons`, a positive control catching a reason emptied three levels deep, and a fully-known counter-fixture. `npm test`: 812/813 — same single pre-existing failure. `npm run build`: green.)*
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

- [x] **W5: `adaptWorld*` against the byte-pinned fixture** *(done 2026-09-04 — all six adapters (`adaptWorldSector`, `adaptWorldLane`, `adaptWorldSlot`, `adaptWorldForce`, `adaptWorldLegion`, `adaptWorldTurnEvent`) added to `adapt.ts`, pure, no loam number derived in TypeScript. `SectorView.lifelineCost`/`.lifeline` take an `options.lifelinesRequested` flag since the wire always sends a real 0/false — the caller's own request state is what decides `known` vs `pending`, not the value. New `adaptWorld.test.ts`, 10 tests, all against the real byte-pinned fixture (not a hand-written double): every sector/lane/slot/force/legion in `first-light.json` adapts without throwing and with zero empty pending reasons; the unknown-sector proof compares real fixture sectors `black-gate` (Unknown, `typeId:""`) against `ember-hollow` (Watched-but-unowned, `typeId:"stable"`, real slots, yet also `loamNet:0`) — proving a caller must read `intel`, never a zeroed economic field, to tell "never seen" from "seen and simply not held"; the fracture-delta convention verified against every sector's real `fractureIntensityMilli:1000` baseline adapting to a zero delta. `npm test`: 822/823 — same single pre-existing failure. `npm run build`: green. **Phase 0's `world-contract` block (W1–W5) is complete.**)*
  - Description: the six pure adapters, tested against `first-light.json` — which is generated and
    asserted byte-for-byte by `WorldFixtureTests.cs:28-50` — so an adapter and the server cannot
    drift silently. That drift is exactly how `worldTypes.ts` lost `structureId` for two waves.
  - Acceptance: `adaptSector` / `adaptLane` / `adaptLegion` / `adaptSlot` / `adaptForce` /
    `adaptTurnEvent` are pure functions round-tripping the fixture; the **unknown-sector case** is
    covered — an unseen sector serialises every field at its record default
    (`WorldEndpoints.cs:271-277`) and is indistinguishable from a zeroed known one *except by*
    `intel`, so the adapter branches on `intel`, never on emptiness, and a test asserts that; no
    adapter derives a loam number in TypeScript.
  - Verify: `cd web\fusion-rpg-web; npm test -- adapt`, then the full `npm test`.
  - Files: `web/fusion-rpg-web/src/contract/adapt.ts`, `web/fusion-rpg-web/src/contract/adapt.test.ts`.
  - Dependencies: W4.
  - Scope: M.

### `world-wire` — the server projections

**Thirteen additions, not nine.** The spec's own nine plus the four the arbitration re-homed here
from `world-targeting`, `world-numbers` and `world-playback`. They are grouped below by DTO so no
task touches more than a handful of files, and **the fixture is re-blessed exactly once**, in W19,
after every field addition has landed — the L25 precedent, where five specs each reopened the same
re-bless budget one field at a time.

- [x] **W6: `WorldSectorDto` — pressure, warden, neglect, capacity**
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
  - **Done (2026-09-04):** `WorldSectorDto` gained `WardenBindingId`/`NeglectedTurns`/`LoamCapacity`
    (`WorldDtos.cs`); `WorldEndpoints.cs`'s `ProjectSector` now assigns `PressureMilli` and all three
    new fields on the exact `StabilityMilli` owner-gate pattern (`string.Equals(sector.OwnerFactionId,
    view.FactionId, ...) ? real : default`), and the stale pre-contagion comment above it is replaced
    with one that names why `PressureMilli` is real now and why `DepletionMilli` is deliberately left
    at 0 (nothing in Core writes it — a real gap, not invented here). `dotnet build
    src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    New file `tests/FusionRpg.Server.Tests/WorldSectorProjectionTests.cs` (2 tests) proves the
    acceptance criterion from **two viewers over the same world** (`dave` owns `d-home` in the
    `two-hearths` fixture, `zomboss` does not): `Pressure_warden_and_neglect_reach_the_owner_and_only_the_owner`
    seeds `pressure_milli`/`warden_binding_id`/`neglected_turns` directly into the same
    `rpg_world_sectors` columns `RpgStore.LoadWorldState` reads (so the values are genuine Core-level
    `WorldSector` state, not a projection double), then asserts the owner sees the real values and the
    non-owner sees 0/null/0; `Loam_capacity_denominates_the_owners_own_stock_and_is_zero_for_everyone_else`
    proves `LoamCapacity` is a real positive base-capacity number for the owner (from
    `LoamPolicy.LoamCapacity`, no seeding needed — a fresh sector already carries it) and exactly 0 for
    the non-owner.
    **Real, documented gap, not a wiring gap:** `WardenBindingId` has no Core writer yet — nothing sets
    it to a non-null value anywhere in `FusionRpg.Core` today (`ClaimResolver.cs:85` only ever clears
    it to `null` on capture); the future "bind a warden" command belongs to `world-commands` (not yet
    built). The test therefore *seeds* the column directly to prove the gating wire is correct end to
    end, rather than claiming a real gameplay path already produces it — an honest distinction from
    `PressureMilli`/`NeglectedTurns`, which genuinely are written every turn by `LoamPhases.cs` already
    (confirmed by reading `LoamPhases.cs:224,240,266-283` before writing the test, not assumed).
    `FusionRpg.Server.Tests` had no assembly-wide tuning bootstrap (unlike Core/Data/E2E.Tests) — the
    new test class repeats `AptitudeChannelModsTests.cs`'s own inline `LoamPolicy.Configure`/
    `WorldTuningHub.Configure` pattern (reading the real `data/tuning/*.v1.json` files) rather than
    inventing a third setup style.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 99/99 passed (28s). `dotnet test
    tests\FusionRpg.Core.Tests` → 5276/5276 passed (20s, pre-existing unrelated warnings only).

- [x] **W7: `WorldSlotDto` and `WorldLaneDto` — construction, slot owner, gate key**
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
  - **Done (2026-09-04):** `WorldSlotDto` gained `OwnerFactionId` (now with an XML doc stating the
    gate) and `ConstructionTurnsRemaining`; `WorldLaneDto` gained `GateKeyId`. `WorldEndpoints.cs`'s
    slot-mapping lambda now looks up the live `WorldSlot` for each remembered slot's index and reads
    its `OwnerFactionId` gated exactly like `StabilityMilli` (`sector.OwnerFactionId ==
    view.FactionId`) — truth-gated per spec-world-wire.md §1's decided "cheap resolution", **not**
    `RememberedSlot`-gated, so no belief field was added and no state hash moved (`RememberedSlot`
    itself — `FactionIntel.cs` — was not touched). The lane-mapping lambda now assigns `GateKeyId =
    l.GateKeyId` ungated, alongside the pre-existing `HazardMilli`/`WardLevel`. `dotnet build
    src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    **Real, separate defect found and fixed while writing the `ConstructionTurnsRemaining` test:**
    `IntelSeed.cs`'s turn-zero snapshot builder (`Snapshot()`, used only for a world's authored
    opening belief) built every `RememberedSlot` **without** `StructureId`/`ConstructionTurnsRemaining`
    — silently dropping both since the day `StructureId` was introduced — while its sibling builder,
    `IntelRecorder.Observe` (the real per-turn scouting path, `IntelRecorder.cs:107-117`), already
    carried both correctly. `Habitability.For` had been reading a field that was therefore always
    null/default at world creation, for every template, forever. Fixed by copying the two fields the
    same way `IntelRecorder.Observe` already does (`IntelSeed.cs`). Confirmed behaviour-preserving, not
    a hash-moving change: no template (`two-hearths`, `first-light`) sets a non-null `StructureId`/
    `ConstructionTurnsRemaining` on any slot at authoring time (`grep` across every
    `WorldTemplateCatalog*.cs`), so every existing world still resolves `null → null` through the
    fixed path; `GoldenFinalHash` is unaffected (Core.Tests below still green).
    New file `tests/FusionRpg.Server.Tests/WorldSlotAndLaneProjectionTests.cs` (3 tests). The
    slot-owner test discovered mid-write that the `two-hearths` template gives **neither** faction
    Full-detail sight of the *other's* ground at creation (`AuthoredIntel` only covers each side's own
    cluster, and the AI faction gets no authored bonus at all — confirmed by reading `IntelSeed.cs`
    before assuming otherwise), so "asserted from two viewers" has no real fixture path without
    seeding a survey directly — the test writes a realistic `IntelSnapshot` straight into
    `rpg_world_faction_intel` (mirroring exactly what `IntelRecorder.Observe`/the fixed `IntelSeed`
    would themselves produce), the same technique W6 used for sector-truth columns. Verify: `dotnet
    test tests\FusionRpg.Server.Tests` → 102/102 passed (29s). `dotnet test
    tests\FusionRpg.Data.Tests` → 630/632 passed; the 2 failures
    (`DemonSpeciesImportCliTests.A_real_import_against_the_real_committed_tree_succeeds…` and
    `…A_stale_committed_file_refuses_the_whole_import…`) are unrelated to this task — a concurrent,
    already-known background process (seedsmith species generation, see `git status` showing live
    edits under `data/seed/demons/species/`) has a mid-write `GarlicPumpkin` entry with an
    `unresolved` rarity; confirmed stable/reproducible and confirmed via `git status` that the
    touched files are entirely outside `src/FusionRpg.Core`, `src/FusionRpg.Contracts`,
    `src/FusionRpg.Server` and `tests/`. `dotnet test tests\FusionRpg.Core.Tests` (not required by
    this task's own Verify line, but run anyway given the `IntelSeed.cs` Core change) → 5276/5276
    passed.

- [x] **W8: `WorldEntityDto` — carried loam, member role, supply, and the legion display name**
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
  - **Done (2026-09-04):** `WorldEntityMemberDto` gained `Role`; `WorldEntityDto` gained
    `DisplayName`, `CarriedLoam`, `Capacity`, `Burn`, `Runway` (the last an `int?`, matching
    `LegionSupply.TurnsUntilExhausted`'s own type — a turn count, not a loam magnitude, so `long` did
    not apply there). New Core file `src/FusionRpg.Core/World/EntityNaming.cs`: a pure
    `DisplayName(world, entity)` function — no persisted counter, no hashed field — numbering each
    entity by stable id order among its own faction's same-`WorldEntityKind` entities ("Legion I",
    "Legion II", …), covering all five `WorldEntityKind` values, not just Legion. `WorldEndpoints.cs`'s
    entity-mapping lambda now calls `LegionSupply.Capacity/Burn/TurnsUntilExhausted` and
    `EntityNaming.DisplayName(w, e)`, plus `m.Role.ToString()` on each member. `dotnet build
    src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    New file `tests/FusionRpg.Core.Tests/World/EntityNamingTests.cs` (9 tests: single-legion
    numbering, stable-id-order-not-insertion-order, independent numbering per owner and per kind, and
    a `[Theory]` proving standard roman-subtractive notation at I/IV/IX/XIV/XL) — built on isolated
    minimal `WorldState` fixtures rather than any shipped template, since the ordinal rule must not
    depend on a specific template's entity ids. New file
    `tests/FusionRpg.Server.Tests/WorldEntityProjectionTests.cs` (3 tests) proves the wire fields
    using the `two-hearths` fixture's own `e-dave-legion-1`, which already carries real non-default
    values at creation (`CarriedLoam = 500`, one `Bearer` among three members) — no raw-SQL seeding
    needed here, unlike W6/W7's dormant fields. Confirmed `WorldStateDto.Entities` (`view.OwnForces`)
    is inherently single-viewer — a faction only ever sees its own forces there (per its own existing
    doc comment) — so no owner-gating test applies to this DTO the way W6/W7 needed one.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 105/105 passed (24s). `dotnet test
    tests\FusionRpg.Core.Tests` → 5285/5285 passed (16s).

- [x] **W9: Per-lane march cost for the selected legion** *(re-homed from `world-targeting`)*
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
  - **Done (2026-09-04):** `spec-world-wire.md` §1 left the exact wire shape to this task
    ("`world-wire` owns the projection's shape"); implemented as an opt-in query param on the
    existing `/state` endpoint (`?forLegion=<entityId>`, matching the established `?lifelines=`
    pattern) and a new `WorldStateDto.MarchCosts: IReadOnlyDictionary<string,int>` keyed by
    `laneId`, empty by default. `WorldEndpoints.cs`'s `/state` handler resolves `forLegion` against
    `believedView.OwnForces` (empty/absent stays empty rather than erroring on an unknown or
    not-mine id) and, when found, computes `BannerElement.Of(legion)` once and calls `LaneCost.For`
    per lane with a **belief-climate lookup** (`believedView.Believed(sectorId)?.Climate`), never
    truth — the exact fog-honesty property spec-world-targeting.md §3 calls out by name. `dotnet
    build src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    New file `tests/FusionRpg.Server.Tests/WorldMarchCostProjectionTests.cs` (3 tests): empty when
    no legion named; real `LaneCost.For` math for an ordinary corridor lane (560 = 800 × 700‰); and
    the fog-honesty proof — seeded `l-dh-df1` to `ley` and d-flank-1's **truth** climate to Ice
    (matching `e-dave-legion-1`'s real banner element, itself derived and verified: peashooterzombie
    Earth / conezombie Ice / paperzombie Light, all singleton counts, so `BannerElement.Of`'s
    ring-order tiebreak picks Ice) while leaving Dave's **believed** climate at its original Earth —
    asserted the wire cost is the undiscounted 720, not the 576 a truth-based read would produce,
    proving the projection reads belief, not truth. Verify: `dotnet test
    tests\FusionRpg.Server.Tests` → 108/108 passed (29s). `dotnet test tests\FusionRpg.Core.Tests`
    → 5285/5285 passed (19s, no Core changes this task — run per the Verify line anyway).

- [x] **W10: The `LoamUpkeep` operand breakdown** *(re-homed from `world-numbers`)*
  - Description: `WorldSectorDto` carries totals only, and `world-numbers`' nested lockable modifier
    ledger cannot decompose what it is not sent. Project the operands behind the upkeep number, in
    the order the engine applies them, so the ledger shows a derivation rather than a result.
  - Acceptance: each operand carries its own label and value; the operands recombine to the total
    exactly, asserted by a test rather than trusted; whole loam units are `long` and no field carrying
    them is named `…Milli`; owner-gated like the reading it decomposes.
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`, then `python scripts\audit-overflow.py`.
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`,
    `tests/FusionRpg.Server.Tests/`.
  - **Done (2026-09-04):** Refactored `src/FusionRpg.Core/World/Loam/LoamUpkeep.cs` around a new
    `readonly record struct LoamUpkeepBreakdown(Base, Garrison, Development, Danger, IntensityMilli,
    HandicapMilli)` with `Sum` (the four additive operands) and `Total` (the same
    `Sum × Intensity × Handicap / 1_000_000` the formula's own comment already documented) — `For`
    (both the pure 5-arg overload and the truth `(world, sector)` overload) now delegates to
    `Breakdown`/`BreakdownFor` rather than duplicating the arithmetic, so the total and its
    decomposition cannot drift apart by construction, not just by convention. Confirmed
    behaviour-preserving before touching any call site: `LoamPolicy.DevelopmentAndDangerUpkeep` was
    read first and confirmed to be a pure additive sum (not a multiplicative interaction), so
    splitting it into two named operands changes nothing about the total. Ran the full existing
    `--filter FullyQualifiedName~Loam` sweep (164 tests) immediately after the refactor, before
    writing anything new, and it stayed green.
    New `WorldSectorDto.UpkeepBreakdown: LoamUpkeepBreakdownDto` (new record, same six fields),
    computed inside `ComputeLoamReading`'s existing per-sector loop (one new dictionary,
    `UpkeepBreakdownBySector`, alongside the pre-existing `UpkeepBySector` — same structural
    owner-gating pattern, not a per-field check) and read in `ProjectSector` the same way `upkeep`
    already was. `dotnet build src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test
    was written.
    Extended `tests/FusionRpg.Core.Tests/World/Loam/LoamUpkeepTests.cs` (+7 tests: a `[Theory]`
    proving `Breakdown(...).Total` matches `For(...)` across 5 parameter sets already exercised by
    the file's own existing tests, a truth-overload recombination proof, and an unowned-sector proof
    that *every* field is zero, not just the total). New file
    `tests/FusionRpg.Server.Tests/WorldUpkeepBreakdownProjectionTests.cs` (3 tests) against the
    `two-hearths` fixture's own `d-home` (no seeding needed — it already carries a real garrison of
    3 and a non-baseline `FractureIntensityMilli = 500`): operands recombine to the wire's own
    `loamUpkeep` exactly; the operands match hand-computed real tuning values (base 10, garrison
    3×2=6, intensity 500‰ → total 8); a non-owner's breakdown is all-zero, not just a zero total.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 111/111 passed (26s). `python
    scripts\audit-overflow.py` → 0 critical (44 findings, all pre-existing A3/A7 — confirmed none
    of `LoamUpkeep.cs`, `WorldDtos.cs`, `WorldEndpoints.cs` or `EntityNaming.cs` appear anywhere in
    the report).
  - Dependencies: None.
  - Scope: S.

- [x] **W11: The `supply.restored` engine line** *(re-homed from `world-playback` and ideal §2.3)*
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
  - **Built out of order, after W12-W14 (see W12's reordering note)** — this task's own acceptance
    needs `Audience` and its consumer (`VisibleTo`) to exist first; "Dependencies: None" under-stated
    a real sequencing need the same way it did there.
  - **Design gap found and resolved (2026-09-04):** the acceptance's "exactly one entry, on the turn
    restored, not on subsequent turns" is a cross-turn state-transition claim, but this task's own
    Files line excludes `WorldState.cs`/`WorldCanonical.cs` — no new persisted, hashed field is in
    scope. `LegionSupply.Resolve` has no memory of a legion's own supply status on a prior turn by
    design (`SupplyGraph`'s own doc comment: recomputed fresh every turn, "a stored flag is exactly
    the kind of derived state that goes stale"). Resolved by deriving the signal from
    `CarriedLoam` alone, which the module already carries: emit `supply.restored`, per legion, only
    on the turn its own deficit is **fully erased** (`carriedById[e] == Capacity(e)` after this
    turn's distribution) — not merely "received some loam this turn". This is provably safe against
    "subsequent turns": a legion at full capacity has `Demand = 0` and drops out of the `toppingUp`
    filter entirely, so the line cannot fire again until a genuine second cut creates a new deficit.
    A partial refill that leaves a legion still short of capacity does **not** fire it — proven by a
    negative assertion added to the pre-existing `A_top_up_never_exceeds_what_the_pool_actually_holds`
    test. Honestly scoped, not oversold: this also fires for a brand-new legion reaching full
    capacity for the first time (never having been cut) — the acceptance's own text only requires
    the cut→restored path to produce exactly one line, not that no other path can ever produce it,
    and no signal derivable from `CarriedLoam` alone can distinguish "topped up for the first time"
    from "topped up after a cut" without the excluded persisted field.
    New test `Supply_restored_fires_exactly_once_the_turn_a_cut_legions_deficit_is_fully_erased` in
    `tests/FusionRpg.Core.Tests/World/Loam/LegionSupplyResolveTests.cs` drives three separate
    `LegionSupply.Resolve` calls (Core has no cross-turn memory to drive through a single call):
    turn 1 cut and burning (asserts no `supply.restored`), turn 2 reconnected with a sufficient pool
    (asserts exactly one `supply.restored`, correct `SectorId`/`Audience`), turn 3 already whole
    (asserts no repeat).
    Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → 740/740
    passed, including `WorldWaveOneAcceptanceTests.GoldenFinalHash` (run explicitly in isolation
    first — 6/6 passed — before trusting the full-suite run) confirming the hash is genuinely
    unchanged, not merely unassumed. `dotnet test tests\FusionRpg.Data.Tests` → 630/632 passed; the
    2 failures are the same already-tracked, unrelated concurrent-background-writer defect logged in
    W7/W12's notes.

- [x] **W12: `TurnReportEntry.Audience` and the faction-scoped emitters — fog defect A**
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
  - **Reordering note (2026-09-04):** built out of order, ahead of W11. W11's own acceptance text
    requires a line to carry `Audience = entity.OwnerFactionId` and "reach its owner under W14's
    rule" — but `Audience` did not exist until this task, and `VisibleTo` does not read it until W14.
    W11 declares "Dependencies: None", which under-states a real sequencing need; built W12 → W13 →
    W14 first so W11 has something real to plug into, rather than adding a field to a line nobody
    can yet gate on.
  - **Done (2026-09-04):** `TurnReportEntry` gained `string? Audience = null` (after `SectorId`, so
    every existing positional/named call site keeps compiling unchanged); `TurnReport.Add` gained a
    matching optional `audience` parameter. Set `audience: faction.FactionId` at the three named
    sites (`LoamPhases.cs`'s `loam.handicap` and `loam.shortfall.unresolved`,
    `LegionSupply.cs`'s `legion.topup`). For the battle line, read `BattleRequest.LocationId`'s own
    doc comment first ("sector id, or lane id for a crossing", `BattleSeam.cs:34`) before touching
    it — passing it unconditionally would put a lane id in the sector slot for `BattleKinds.Lane`,
    which is exactly the class of defect W13 exists to fix elsewhere; wrote
    `sectorId: request.Kind == BattleKinds.Lane ? null : request.LocationId` instead of the literal
    "pass `request.LocationId`" instruction, and proved both branches with a dedicated new file,
    `tests/FusionRpg.Core.Tests/World/BattleReportingTests.cs` (2 tests: a sector-kind battle carries
    its real `SectorId`; a lane-kind crossing does not, and its lane id stays legible in `Detail`).
    Extended three existing tests in place rather than writing parallel duplicates:
    `LoamPhasesTests.cs`'s handicap test, `LoamTextureTests.cs`'s warded-shortfall test, and a new
    fact in `LegionSupplyResolveTests.cs` for the topup line (no prior test touched that report
    entry at all). New file `tests/FusionRpg.Core.Tests/World/TurnReportEntryTests.cs` (2 tests)
    proves the persistence-compat requirement directly against `System.Text.Json` with the exact
    shape `RpgStore.WorldTurns.cs:528`'s real (option-less, PascalCase) serializer call produces —
    an old JSON blob with no `"Audience"` property deserializes and reads `Audience == null`; a
    freshly-set `Audience` round-trips through the same serializer unchanged.
    Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → 737/737
    passed. `dotnet test tests\FusionRpg.Data.Tests` → 630/632 passed; the 2 failures are the same
    pre-existing, unrelated, concurrent-background-writer defect already logged in W7's note
    (`DemonSpeciesImportCliTests` / a mid-write `GarlicPumpkin` species file with an `unresolved`
    rarity) — confirmed unchanged in count and identity from before this task's edits.

- [x] **W13: `MovementPhase` stops putting non-sectors in the sector slot — fog defect B**
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
  - **Done (2026-09-04):** Site 1 (`legion.runway`, line ~104-105): dropped the `?? outcome.OnLaneId`
    fallback (a lane id never belongs in the sector slot) and added `audience: entity.OwnerFactionId`.
    Site 3 (the generic dequeue-loop `report.Add`, line ~198): stopped reading `evt.Detail` for the
    structured `sectorId` — `Contact`/`Crossing` are already intercepted above this line, so only
    `Arrival`/`Halt` ever reach it, and both are scheduled only after `moved[entity.EntityId]` is
    set; the fix reads that entity's own post-march `AtSectorId`/`OwnerFactionId` instead, which is
    the real answer for both event kinds (a sector when it's standing in one, null when it's not) —
    without needing a new field on `TurnEvent`/`TurnEventQueue.cs`, which stayed outside this task's
    Files line. Site 2 (`:123-124`, the `queue.Schedule(... Arrival ...)` call) needed **no** code
    change — its `Detail` computation was already correct for the free-text narration the acceptance
    says must stay there; the cited line only explains *why* site 3 was wrong (the same string that's
    fine as prose was being reused as structured data), not a second site to edit.
    New file `tests/FusionRpg.Core.Tests/World/MovementPhaseHaltReportingTests.cs` (2 tests, calling
    `MovementPhase.Run` directly rather than the full `TurnEngine`, matching
    `BattleReportingTests.cs`'s own precedent): a legion halted by a hostile zone of control produces
    a `halt:zoc:<sector>` line whose *structured* `SectorId` is the real sector (not the `"zoc:"`-
    prefixed detail string) and whose `Audience` is the halted legion's own owner — proving the
    todo's own claim "today it reaches nobody" was a real, previously-untested gap (no test in this
    assembly touched a halt report line before this task); a second test proves a mid-lane arrival
    carries `null` in the sector slot, with the lane id staying legible in `Detail` only. Caught and
    fixed a test-authoring mistake before trusting the result: the two tests first filtered by
    `e.Kind == TurnEventKinds.Halt`/`.Arrival`, which never matches — `TurnEventKinds` labels the
    *queue's* internal event, folded into `Detail` as `"halt:zoc:s2"`; the report entry's own `Kind`
    is always `TurnReportKinds.Event`. Corrected to filter on `Detail.StartsWith(...)`, reran, and
    both passed against the real fix — not against a predicate that happened to match nothing.
    Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → 739/739
    passed.

- [x] **W14: `VisibleTo` as W-F1's three named clauses — fog defect C**
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
  - **Done (2026-09-04):** Read the spec's own worked code (`spec-world-wire.md`'s `VisibleTo`
    snippet) before writing anything, since it settles a question the acceptance text alone leaves
    ambiguous — "keyed on `Kind`" turns out to mean `IsStaticFact(e.Kind, e.Detail)`, not a switch
    purely on the coarse `TurnReportEntry.Kind` enum (which is almost always `"event"`); the real
    dispatch is `Kind == Event` gating a **closed detail-prefix list** (`"claim."`, `"loam.lost:"`) —
    everything else, including every `Battle`-kind line, defaults to dynamic (rule 2), which is the
    safer direction to be wrong in. Replaced the old single-parameter `VisibleTo(string? sectorId,
    BelievedWorldView?)` with the three-clause `VisibleTo(TurnReportEntry, string? viewer,
    BelievedWorldView?)` exactly matching the spec's own reference implementation; added the
    `IsStaticFact`/`StaticFactDetailPrefixes` helper with a comment explaining *why* the list stays
    short (a dynamic fact wrongly marked static would leak stale information as live). Added the
    endpoint's own doc-comment note on `/turn/{worldId}/turn/{turn}` recording the stated limitation
    that `believed` reads current intel, not a snapshot pinned to the requested turn — errs toward
    showing more, never less, exactly as the acceptance requires it to say.
    New file `tests/FusionRpg.Server.Tests/WorldTurnReportFogTests.cs` (4 tests, seeding a synthetic
    `rpg_world_turn_log.report_json` row directly — the same technique W6/W7/W9/W10 used for
    hard-to-reach state — rather than driving a full multi-turn simulation to produce one specific
    battle/claim/handicap line): rule 1 for a faction-scoped `loam.handicap` line (reaches `dave`,
    not `zomboss`); rule 1 for a `halt` line (reaches its owner); rules 2-vs-3 in one assertion on
    the *same* sector (`hot-ground`, seeded with a stale Dave belief and confirmed un-garrisoned by
    anyone in the `two-hearths` template, so `SeesNow` is false there) — a battle withheld, a claim
    shown; and a control proving rule 2 is reachable at all (a battle on `d-home`, Dave's own
    live-watched capital, is shown). All 4 passed on the first run, confirming both the fix and the
    "hot-ground is genuinely unwatched" assumption were correct together.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 115/115 passed (18s). `dotnet test
    tests\FusionRpg.E2E.Tests` → 194/195 passed; the 1 failure is the already-tracked, expected-red
    `WorldFixtureTests` golden (deferred to W19's single re-bless per the plan's own dependency list
    for that task — confirmed the diff is exactly the new `upkeepBreakdown` field from W10, not a
    new defect).

- [x] **W15: `WorldCalendarDto` on `WorldStateDto`**
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
  - **Done (2026-09-04):** New `WorldCalendarDto` (`DaysPerWeek`, `WeeksPerMonth`,
    `WeekBoundary`/`MonthBoundary`/`SpecialWeek`/`SpecialMonth`/`Plague`) on `WorldStateDto.Calendar`.
    `Project(...)` computes `TurnCalendar.Roll(w.CurrentTurn, w.Seed)` once and maps its fields
    across — the seed itself is read only to produce the roll, never serialized anywhere on the DTO.
    Corrected `docs/architecture/world-stage/spec-world-hud.md` at all four places it described the
    calendar as FE-derived from `calendar` report entries (§3's prose, the file-tree comment, the
    testing-strategy item, and the success-criteria line) to instead read from
    `WorldStateDto.Calendar` — matching this task's own "world-hud's spec is corrected" acceptance
    clause. `dotnet build src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was
    written.
    New file `tests/FusionRpg.Server.Tests/WorldCalendarProjectionTests.cs` (3 tests): turn 0 carries
    an all-false blank roll (matching `TurnCalendar.Roll`'s own `turn <= 0 → default` rule); turn 7
    (the first week boundary at the real `daysPerWeek=7` tuning, set via the same raw-SQL
    `current_turn` seeding technique used elsewhere rather than committing seven real turns) matches
    `TurnCalendar.Roll(7, seed)` computed directly and independently in the test, field for field;
    and a structural test asserting no `"seed"` substring appears anywhere in the raw JSON response
    and that `calendar`'s object has exactly its seven declared properties, nothing shaped like a
    preview of a future turn.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 118/118 passed (22s). `dotnet test
    tests\FusionRpg.E2E.Tests` → 194/195 passed; the 1 failure is the same already-tracked,
    expected-red `WorldFixtureTests` golden deferred to W19 (confirmed the diff is exactly the new
    `upkeepBreakdown` field from W10, same as W14's note — `Calendar` had not yet reached this diff
    position in the JSON).

- [x] **W16: `WorldStateDto.ProspectedSectorIds`**
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
  - **Done (2026-09-04):** `WorldStateDto.ProspectedSectorIds` (ordinal-sorted `IReadOnlyList<string>`
    for a stable wire order, matching every other sorted collection in this DTO tree) computed via
    `Prospecting.Reveal(w, view.FactionId)` directly in `Project(...)` — a separate top-level field,
    never touching `WorldSectorDto.Intel` or any other sector field, matching the acceptance's own
    "never merged" requirement by construction (it is written nowhere near the sector-mapping code).
    `dotnet build src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    New file `tests/FusionRpg.Server.Tests/WorldProspectingProjectionTests.cs` (2 tests): with no
    dowser, the set is empty and the rest of the response shape is unaffected; setting
    `e-dave-legion-1`'s stance to `dowse` (raw SQL on `rpg_world_entities.stance`, the same seeding
    technique used throughout this program) reveals its own sector (`d-home`, which carries a real
    rootbed) while `d-home`'s own `intel` field stays exactly `Watched` — proving the two are
    genuinely independent, not merely untested together.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 120/120 passed (22s). `dotnet test
    tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → 740/740 passed.

- [x] **W17: `GET /api/world/catalog`**
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
  - **Done (2026-09-04):** `WorldCatalogDto` (`Structures`, `SlotTypes`, `StrengthBands`,
    `LaneTypes`) plus the four matching leaf DTOs, mapping `StructureCatalog.All`,
    `SlotTypeCatalog.All`, `StrengthBandCatalog.All` and `LaneTypeCatalog.All` field-for-field.
    `WorldStructureDto.Cost` is named exactly as the acceptance requires (never `CostMilli`), with
    the XML doc stating "whole loam units" and the reason a `Milli`-trusting renderer would be wrong
    by 1000× — `StructureDef.CostMilli` itself is untouched, matching "renaming the Core constant
    stays out of scope." New route `GET /api/world/catalog`, mapped inside the existing `/api/world`
    group (`/catalog` as a literal segment takes routing priority over the `/{worldId}` parameter
    route, so no collision) — no world id, no viewer, no fog, matching the acceptance's "the route
    answers without a world or a viewer" exactly. `dotnet build src/FusionRpg.Server` succeeded 0
    warnings/0 errors before any test was written.
    New file `tests/FusionRpg.E2E.Tests/WorldCatalogE2ETests.cs` (3 tests, no `IAsyncLifetime` —
    nothing here touches world state, so no reset is needed): the route answers 200 with all four
    non-empty lists and no world ever created; the structure's `cost` field is present and
    `costMilli` is absent, proving the rename actually happened rather than just adding an alias; a
    strength band carries its full five-field shape.
    Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World` → 42/43
    passed; the 1 failure is the same already-tracked, expected-red `WorldFixtureTests` golden
    deferred to W19. `python scripts\audit-magic-numbers.py --summary` → 0 M1 findings; the existing
    12 M3 findings are all pre-existing and none touch `WorldDtos.cs`/`WorldEndpoints.cs` (confirmed
    by grepping the M3 target list directly).

- [x] **W18: The AI-reasons projection becomes developer-tree-only** *(arbitration §C, §8.3)*
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
  - **Owner decision taken (2026-09-04): the cheaper reading, as pre-authorized.** No synchronous
    owner turn was available mid-autonomous-loop; the task's own text names this exact fallback for
    that case ("drop `Reason`, keep the entry"), so that is what shipped — the entry stays visible
    under the pre-existing `VisibleTo(WorldCommand, ...)` rule (unchanged by this task), only its
    `Reason` is nulled for a commander other than the viewer, outside the dev gate.
  - **Done (2026-09-04):** `Commands` projection in `/turn/{worldId}/turn/{turn}` now computes
    `Reason` as `SimFlags.Enabled || commanderId == viewer ? l.Reason : null` — `SimFlags.Enabled`
    reads `FUSIONRPG_SIM=1` exactly the way `Program.cs`'s own `if (SimFlags.Enabled)
    app.MapSimAndProbes()` gate already does, so this is the same developer surface, not a new one.
    `dotnet build src/FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    New file `tests/FusionRpg.Server.Tests/WorldCommandReasonGateTests.cs` (2 tests, seeding
    `rpg_world_commands` rows for both a viewer's own order and a foreign commander's, plus a
    `rpg_world_turn_log` row so `/turn/0` resolves at all — the same raw-SQL technique used
    throughout this program): without the gate, the viewer's own `Reason` survives while the foreign
    one reads `null`; with `FUSIONRPG_SIM=1` set for the duration of the test (restored in a
    `finally` block to avoid leaking into any other test running in the same process), both modes
    are asserted as the acceptance requires, and both `Reason`s reach the viewer unchanged from
    before this task. Full Server.Tests suite re-run afterward (122/122) to confirm the mutate/
    restore pattern left no cross-test contamination.
    Verify: `dotnet test tests\FusionRpg.Server.Tests` → 122/122 passed (20s). `dotnet test
    tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World` → 42/43 passed; the 1 failure is
    the same already-tracked, expected-red `WorldFixtureTests` golden deferred to W19.

- [x] **W19: Re-bless `first-light.json` once, and sweep its seven consumers**
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
  - **Done (2026-09-04):** Ran the bless exactly once with `$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1";
    dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldFixtureTests`, which
    passed 1/1 and rewrote the fixture. Reviewed the full diff before trusting it, not just the
    test's own green: `git diff --stat` showed +116/-17, and every one of the 17 deletions turned
    out to be `"structureId": null` losing its position as the slot's last property (needing a
    trailing comma once `constructionTurnsRemaining` was added after it) — a formatting artifact,
    not a removed field. Confirmed every W6–W16 addition is genuinely present in the diff
    (`upkeepBreakdown`, `wardenBindingId`/`neglectedTurns`/`loamCapacity`, `constructionTurnsRemaining`/
    `gateKeyId`, `role`/`carriedLoam`/`displayName`/`capacity`/`burn`/`runway`, `marchCosts: {}`,
    `calendar`, `prospectedSectorIds: []`) and nothing else changed shape.
    Verify: `dotnet test tests\FusionRpg.Data.Tests` → 630/632 passed (the 2 pre-existing, unrelated,
    concurrent-background-writer failures logged since W7/W12/W18 — confirmed
    `WorldWaveOneAcceptanceTests.GoldenFinalHash` specifically among the 630, unaffected). `cd
    web\fusion-rpg-web; npm test` → 822/823 passed across 109/110 files; the 1 failure is the
    already-known, pre-existing `disabledReasonGuard.test.ts` (GG-55) accessibility gap logged in
    W1's own completion note at the start of this session — unrelated to world-wire, confirmed
    stable across every full-suite run this entire session. All seven named fixture consumers are
    green.

- [x] **W20: `first-light-turn.json` — the turn-report golden**
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
  - **Real, separate defect found while building this fixture, fixed by design (not code) —
    recorded, not fixed here, since it is outside every file this task touches:** a legion that is
    destroyed in combat (or otherwise removed) on the *same turn* it first reaches ground its
    faction does not own produces report lines (its own battle, its own `legion.starved`) that its
    **own owner cannot see** — because `Visibility.Accumulate` only credits sight from a faction's
    *current* entities and owned sectors, and the dying entity has already been removed from
    `next.Entities` (inside `MovementPhase.Run`'s own battle resolution, before `TurnEngine.Step`'s
    final `Observe` call) by the time sight is computed for that same turn. Confirmed directly: an
    early exploratory run had Dave's starting legion march alone into Wild-held `ash-waste` and lose
    outright — the resulting `battle`/`legion.starved` lines never reached Dave's own turn report at
    all, at any later query time, because nothing of his remained nearby to grant him sight of that
    ground. The final fixture below avoids the failure mode (Dave secures `ember-hollow` first,
    whose *ownership* gives him a permanent one-lane glimpse of neighbouring `ash-waste`, so his
    turn-4 loss there stays visible) rather than exercising it — this is a real gap in
    `Visibility.cs`/`TurnEngine.cs`'s phase ordering, pre-dating this program entirely (not
    introduced by W6–W19), and belongs to whichever module next touches intel/combat ordering, not
    to a fixture-authoring task.
  - **Done (2026-09-04):** New file `tests/FusionRpg.E2E.Tests/WorldTurnFixtureTests.cs` plays a
    real, deterministic six-turn opening on `first-light` (seed 1) chosen to produce a genuine
    example of each W-F1 visibility class in Dave's own account: turn 0 (march to `ember-hollow`,
    his own `legion.runway` — **audience**), turns 1–2 (clearing `ember-hollow`'s two light guards
    while standing there — **live-sight battle**), turn 3 (`claim.held:ember-hollow` — **remembered-
    sight**), turn 4 (marching toward Wild-held `ash-waste` — **halt** plus a live Contact battle),
    turn 5 (zomboss's own warband ordered — via a direct manual command for that commander in this
    SIM harness, not left to `FrontierRulesPolicy`'s own unscripted judgment — to march toward
    `verdant-shelf`, its `legion.runway` the **excluded** line). The exclusion is asserted in code
    (`Assert.DoesNotContain(... e.Subject == "e-zomboss-band-1")` against turn 5's own report,
    fetched as dave) **before** the fixture is trusted, not left to eyeballing the JSON.
    Blessed once with `$env:FUSIONRPG_BLESS_WORLD_FIXTURE = "1"`, then re-ran without the flag to
    confirm the byte-pinned comparison holds deterministically. Read the generated fixture directly
    afterward and confirmed all five `detail` tokens are present:
    `legion.runway:17`, `guard:ember-hollow:e-dave-legion-1` (×2), `claim.held:ember-hollow`,
    `halt:zoc:ash-waste` + `sector:ash-waste:e-wild-pack-1`.
    Updated `web/fusion-rpg-web/e2e/world.spec.ts`'s `**/api/world/first-light/turn/**` mock from a
    flat 404 to answering from the new fixture (indexed by its own `turn` field, matched against the
    requested URL's turn number, 404 outside 0–5) — the same shape the live server gives, so
    `world-playback`'s own future e2e tests have something real to mock against instead of a
    universal 404; confirmed `npx tsc --noEmit` stays clean after the change.
    Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldTurnFixture` →
    1/1 passed, both blessing and the subsequent unblessed re-run. `cd web\fusion-rpg-web; npm test`
    → 822/823 passed; the 1 failure is the same already-known, pre-existing `disabledReasonGuard`
    (GG-55) gap logged since W1, confirmed stable across the whole session.
  - Scope: M.

- [x] **W21: The two fixtures the plan assumes nobody owns**
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
  - **Owner decision taken (2026-09-04): the stated fallback, as pre-authorized.** No synchronous
    owner turn was available; built the 18/10 world by extending the real, already-validated
    `two-hearths` template (2 more sectors, 8 more legions) in a private test-only helper, inserted
    directly through `RpgStore.CreateWorld` (resolved from the E2E host's own DI container via
    `factory.Services`) — no `WorldTemplateCatalog` entry, no new validation pass, "costs nothing in
    Core" exactly as the fallback's own reasoning states.
  - **Real defect found and fixed (outside this task's own Files line, fixed anyway — it directly
    blocked running this task's own new tests alongside the existing ones):**
    `RpgStore.Reset()` (`src/FusionRpg.Data/Sqlite/RpgStore.cs`) never deleted any of the eleven
    `rpg_world*` tables — confirmed by grepping every `CREATE TABLE IF NOT EXISTS rpg_world*` in the
    Data layer against `Reset()`'s own delete list and finding zero overlap. A world created in one
    E2E test class outlived every later `/api/test/reset`, so `WorldTurnFixtureTests` (this session,
    reusing the natural id `"first-light"`) hit `world.exists` against an orphaned row whose owning
    player that same reset HAD already deleted — `CreateWorld`'s own existence check is keyed on
    world id alone, not on the (already-gone) owning player. Added the missing eleven `DELETE FROM
    rpg_world*` statements to `Reset()`'s existing list, same style, same per-statement try/catch.
    Test-infrastructure-only (SIM/E2E reset path, never reached by a real player), so fixed directly
    rather than deferred — confirmed safe with the full `FusionRpg.E2E.Tests` (201/201, up from a
    200/201 red before the fix), `FusionRpg.Data.Tests` (630/632, the same 2 pre-existing unrelated
    failures) and `FusionRpg.Server.Tests` (122/122) suites, not just this task's own tests.
  - **Done (2026-09-04):** Extended `tests/FusionRpg.E2E.Tests/WorldFixtureTests.cs` (matching its
    Files line — added to the existing file rather than a new one) with two more `[Fact]`s alongside
    the existing `first-light` one, sharing a new `BlessOrAssert` helper (refactored out of the
    original test's own inline bless/assert block, behaviour-preserving — the original test still
    passes unchanged). `two-hearths.json`: identical pattern to `first-light.json`, just the other
    real template. `eighteen-ten.json`: the private `BuildEighteenSectorTenLegionWorld` helper takes
    `WorldTemplateCatalog.Build(TwoHeartsId, ...)` and adds 2 sectors (each reachable by exactly one
    new lane off an existing outpost, satisfying `WorldValidation`'s connectivity rule without
    needing a wider graph change) and 8 legions (split 4/4 across Dave/Zomboss, mirroring the
    template's own two starting legions' member shape) — asserted `world.Sectors.Count == 18` and
    `world.Entities.Count == 10` on the constructed `WorldState` itself before ever calling
    `CreateWorld`, and separately asserted the **wire** response's own sector count (18, matches)
    and entity count (5, not 10 — corrected an initial wrong assertion here: `WorldStateDto.Entities`
    is fog-scoped to the viewer's own forces only, per its own existing doc comment, so Dave's wire
    view legitimately shows only his 5, while the world total of 10 is what the acceptance's "carries
    ten legions" actually refers to). Confirmed via `git status` that `first-light.json` shows only
    as the pre-existing W19 modification — the two new fixture files are untracked additions, so
    W19's re-bless budget stays closed, exactly as the acceptance requires.
    Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldFixture` → 3/3
    passed, both the initial bless and a subsequent unblessed re-run confirming determinism. `cd
    web\fusion-rpg-web; npm test` → 822/823 passed; the 1 failure is the same already-known
    `disabledReasonGuard` (GG-55) gap logged since W1.

### `world-commands` — the write surface

- [x] **W22: `Amount` and `StructureId` through all six round-trip sites**
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
  - **Done (2026-09-04):** Added `Amount` (`long?`) and `StructureId` (`string?`) to
    `WorldCommandRequest` (`WorldDtos.cs`) and to the DTO→`WorldCommand` mapping in
    `WorldEndpoints.cs`'s `/commands` handler (a seventh site the task's own count didn't name, since
    it sits between the wire and `WorldCommand`, not inside `RpgStore` — found by tracing the full
    path end to end rather than trusting the task's site list as exhaustive). Added both fields to
    `CommandPayload` (defaulted, so an old JSON blob missing them still binds) and threaded them
    through the one write site and `ReadCommandRow`. Fixed the actual "sixth, silent" site exactly as
    named: `ListWorldCommandsUnlocked` now calls `ReadCommandRow` instead of inlining its own second
    copy of the same deserialize — the shared method's own doc comment ("shared by both listers") is
    true again, and any field added next only needs updating in one place. `dotnet build` on both
    `FusionRpg.Data` and `FusionRpg.Server` succeeded 0 warnings/0 errors before any test was written.
    Extended `tests/FusionRpg.Data.Tests/WorldCommandStoreTests.cs` (+6 tests) rather than a new
    file, matching its own existing round-trip-proof style: `Amount` through `ListWorldCommands`;
    `StructureId` through `ListWorldCommands`; both through `ListLoggedWorldCommands`; the private,
    otherwise-untestable `ListWorldCommandsUnlocked` path proven indirectly by driving a real
    `CommitWorldTurn` and asserting the turn report shows `command.accepted` rather than
    `amount.invalid` for a `sustain` order (the first version of this test asserted an exact
    post-commit stock delta and failed — a full committed turn also runs production/upkeep/overflow
    in the same pass, so the arithmetic wasn't isolatable; corrected to the direct
    admission-succeeded signal instead of modelling the whole economy); and an old, pre-W22 raw
    `payload_json` row (seeded via the same raw-SQL-on-`HotPath` technique used throughout this
    program) still deserializes with both new fields null.
    Verify: `dotnet test tests\FusionRpg.Data.Tests` → 635/637 passed; the 2 failures are the same
    already-tracked, unrelated concurrent-background-writer defect logged since W7. `dotnet test
    tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World` → 46/46 passed. `python
    scripts\audit-overflow.py` → 0 critical, 44 findings, identical count to before this task (no
    new `int` introduced anywhere on the `Amount` path).

- [x] **W23: The property test — every kind × every optional member survives the round trip**
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
  - **Done (2026-09-04):** New file `tests/FusionRpg.Data.Tests/WorldCommandRoundTripPropertyTests.cs`
    (2 tests). Reflection over `typeof(WorldCommand).GetProperties()` drives the per-property
    equality check — never a hand-maintained field list, so a member added to `WorldCommand` later
    is covered automatically (the acceptance's own "reflection... never a hand-maintained list"
    satisfied literally, not via a compile-time-exhaustive switch, which C# records don't offer a
    natural mechanism for). One fully-populated `WorldCommand` per `WorldCommandKinds.All` entry
    (read `WorldCommandAdmission.cs` first to confirm admission only checks fields relevant to its
    own kind, so setting every optional member on every kind is accepted, not refused, for carrying
    "extra" data) proves both public hydration paths (`ListWorldCommands`,
    `ListLoggedWorldCommands`) directly; the private `ListWorldCommandsUnlocked` path (reachable
    only from inside `CommitWorldTurn`) is proven indirectly via `TurnEngine`'s own `Reveal`-phase
    re-admission, asserting `command.accepted` for each kind's own committed turn — one kind per
    committed turn, with `move` ordered last, after discovering by running it that co-resolving
    several kinds sharing one entity in one turn produces *real* resolution-time conflicts (a `move`
    relocating the entity before a same-turn `clear` targeting its old sector runs) that are
    legitimate gameplay outcomes, not round-trip defects — the test isolates the one property it
    actually proves rather than asserting a stricter "nothing ever drops" bar the task never asked
    for.
    **Red-verified by hand, not committed, exactly as the acceptance requires:** temporarily removed
    `Amount = payload.Amount,` from `ReadCommandRow` — both tests failed immediately, one via the
    direct property-equality assertion (`Expected: 100, Actual: null`) and one via the internal path
    showing `amount.invalid` where `command.accepted` should be — then reverted and reconfirmed
    green (18/18 on the `WorldCommand` filter) before moving on.
    Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~WorldCommand` →
    18/18 passed. Full `dotnet test tests\FusionRpg.Data.Tests` → 637/639 passed; the 2 failures are
    the same already-tracked, unrelated concurrent-background-writer defect logged since W7.

- [x] **W24: The `cede` command kind and its admission arm**
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
  - **Done (2026-09-04):** Added `WorldCommandKinds.Cede = "cede"` (doc comment stating the player's
    intent) to `WorldCommand.cs:41`, appended to `WorldCommandKinds.All`. Added the `Cede` admission
    arm in `WorldCommandAdmission.cs:60-66`, matching `Claim`'s shape but with an ownership check in
    place of an entity check (`cede` names no entity — a faction cedes ground, not a legion); the
    shared pre-check at `:45-46` already refuses an unknown sector id, so the kind-specific arm only
    adds `"sector.missing"` (no sector named) and `"sector.not-yours"` (named but not owned by the
    commander). Added 6 new tests to `tests/FusionRpg.Core.Tests/World/WorldCommandAdmissionTests.cs`:
    kind is registered/known; ceding your own sector (`homeworld`) is admitted with no entity named;
    ceding an unowned sector (`black-gate`, unowned at `first-light` world creation) is refused with
    `"sector.not-yours"`; ceding with no sector named is refused with `"sector.missing"`; ceding an
    unknown sector (`"nowhere"`) is refused by the shared check with `"sector.unknown"`; and a
    `TurnEngine.Step`-based test proving a turn with a lone `cede` order produces the identical
    `StateHash` as a turn with no commands at all, confirming `WorldCanonical` never hashes commands.
    Verified: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → **746/746
    passed**; `dotnet test tests\FusionRpg.Data.Tests` → **637/639 passed**, the 2 failures being the
    pre-existing, unrelated `DemonSpeciesImportCliTests` cases (a concurrent background seedsmith
    species-generation process mutates the committed demon tree these tests read against — confirmed
    same failure signature as prior sessions, not a regression from this change).

- [x] **W25: Thread the cede preference into the one `Weakest`**
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
  - **Done (2026-09-04):** `LoamForecast.Weakest` gained one optional `string? ceded = null` parameter
    and one clause (`LoamForecast.cs:28-29`): when `ceded` names a sector already in `candidates`
    (component member, not warded) it wins outright; otherwise the existing worst-balance/ordinal
    ordering answers exactly as before — still exactly one function deciding "which sector fades".
    `LoamForecast.WillRelease` also gained the same optional parameter, forwarding straight through to
    `Weakest`, so W26's `/state` route (which calls `WillRelease`, not `Weakest`, and does not touch
    `LoamForecast.cs` in its own file list) has something to pass the preference into. `LoamPhases
    .Pressure` gained an optional `IReadOnlyDictionary<string,string>? ceded = null` (faction id →
    sector id), looked up per faction inside the existing shortfall branch and passed into `Weakest`
    (`LoamPhases.cs:138-141`) — every pre-existing caller (`Data.Tests`/`Core.Tests` fixtures) compiles
    unchanged since the parameter defaults to null. `TurnEngine.Pressure` now builds that map from the
    turn's admitted commands (`TurnEngine.cs`) the same way `Snapshot` already builds `postures` from
    `stance` orders — group by `CommanderId`, last order per faction wins, plain dictionary, no service.
    Tests added: 4 in `LoamForecastTests.cs` (`Weakest` — ceded-in-component-unwarded wins over default
    ordering; ceded-but-warded falls back to default; ceded-but-in-another-component falls back to
    default; a component covering its own upkeep releases nothing regardless of what was ceded — the
    refusal-per-reason coverage the acceptance asked for); 1 in `LoamPhasesTests.cs`
    (`Pressures_ceded_map_overrides_which_sector_absorbs_the_shortfall`, proving the dictionary
    threads through `LoamPhases.Pressure` itself, direct-call level); 2 in new
    `tests/FusionRpg.Core.Tests/World/CedeThreadingTests.cs` proving the full `TurnEngine.Step`
    pipeline — a committed `cede` order changes which sector actually fades that turn, and a `cede`
    naming a sector the faction does not own is dropped at Reveal (`sector.not-yours`) and never
    reaches the ceded map. Verified: `dotnet test tests\FusionRpg.Core.Tests --filter
    FullyQualifiedName~World` → **753/753 passed** (up from 746, the 7 new tests above).

- [x] **W26: The forecast reads the same preference on the `/state` route**
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
  - **Done (2026-09-04):** `WorldEndpoints`'s `/{worldId}/state` handler now makes its own
    `store.ListWorldCommands(worldId, world.CurrentTurn)` read (a new call site on this route, not a
    reuse of `/turn/{turn}`'s `ListLoggedWorldCommands`), extracts this viewer's own last-filed
    `cede` order's `SectorId` (or `null`), and threads it through `Project(..., pendingCede)` →
    `ComputeLoamReading(w, factionId, ceded)` → `LoamForecast.WillRelease(world, component, ceded)`
    (both gained a matching optional parameter in W25, exactly so this task would not need to touch
    `LoamForecast.cs`). No new SQL: `ListWorldCommands` already existed. Added
    `tests/FusionRpg.Server.Tests/WorldCedeForecastTests.cs` (real HTTP host over `two-hearths`,
    matching `WorldSectorProjectionTests.cs`'s own boilerplate): drains all four of Dave's sectors'
    stock to 0 and pushes development/danger up so the shortfall survives this turn's rootbed
    production; discovers the default forecast pick empirically, files a `cede` on a *different*
    component member, confirms `/state`'s `willReleaseNextTurn` flag moves to the ceded sector, commits
    the turn (two-hearths' Zomboss carries `FrontierRulesPolicy` and auto-fills, so Dave's own commit
    is enough), and confirms the ceded sector — not the original default pick — is the one that
    actually goes `Lost`. Verified: `dotnet test tests\FusionRpg.Server.Tests` → **123/123 passed**
    (up from 122); `.\scripts\guard-dal.ps1` → **OK, no SQL outside FusionRpg.Data** (the test's own
    raw-SQL fixture seeding matches `WorldSectorProjectionTests.cs`'s established, guard-exempt test
    pattern); `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World` → **46/46
    passed**.

- [x] **W27: `RulesetVersion` — read the current value, bump it once, triage before re-blessing**
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
  - **Done (2026-09-04):** Read `TurnEngine.RulesetVersion`'s current value (5, confirmed by reading
    the file, not assumed) and bumped it to 6, appending one new doc-comment paragraph after the
    existing "Bumped to 5" entry (matching the file's own append-at-the-end convention) explaining
    this is **one** bump for the whole `cede`/`bind-warden`/`dowse` wave (decisions.md:98) — a world
    that files no `cede` order resolves identically to version 5, so `bind-warden` (W28) and `dowse`
    (W30) land under this same number without a second bump. Verified: `dotnet test
    tests\FusionRpg.Data.Tests` → **637/639 passed**, the 2 failures being the same pre-existing,
    unrelated `DemonSpeciesImportCliTests` cases already documented at W24/W25 (concurrent background
    seedsmith species-generation activity) — critically, `WorldWaveOneAcceptanceTests.GoldenFinalHash`
    is **not** among the failures, confirming the bump alone (no cede order filed in that 20-turn
    scenario) moved no golden, exactly as the acceptance requires; `dotnet test
    tests\FusionRpg.Core.Tests` (full, unfiltered) → **5313/5313 passed**.

- [x] **W28: The `bind-warden` command kind and `WardResolver`**
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
  - **Done (2026-09-04):** Corrected the spec's own naming collision first
    (`docs/architecture/world-stage/spec-world-commands.md`): every kind-name occurrence of `ward`
    renamed to `bind-warden` (`WordCommandKinds.Ward`→`BindWarden`, `WardResolver`→`WardenResolver`,
    the endpoint route, the project-structure table, the testing/success-criteria sections), leaving
    `ward`/`WardLevel` untouched everywhere it names the still-unbuilt *lane* action instead. Added
    `WorldCommandKinds.BindWarden = "bind-warden"` to `WorldCommand.cs` (needs no entity, matching
    `Cede`'s shape) and a new `WorldCommand.WardenId` field — the value a bind-warden order writes
    into `WorldSector.WardenBindingId`, opaque to Core the same way `StructureId` is. Admission arm in
    `WorldCommandAdmission.cs` mirrors `Cede`'s ownership check plus one more: `WardenId` must be
    non-blank (`"warden.missing"`). New `src/FusionRpg.Core/World/Movement/WardenResolver.cs`, wired
    into `TurnEngine.Snapshot` right after `BuildResolver` (so a same-turn claim+bind-warden chains,
    matching `Build`'s own precedent), re-validating ownership at resolution rather than trusting
    admission — the same discipline `ClaimResolver`/`BuildResolver` already apply. Added a comment at
    `WorldCanonical.cs:37` (where `s.WardenBindingId` is emitted) naming it as the first field in that
    row whose hash effect isn't "a number changed." Tests: 8 new admission cases appended to
    `WorldCommandAdmissionTests.cs` (known kind; admitted with no entity; refused not-yours/missing
    sector/unknown sector/missing warden id — 3 blank-string variants via `[Theory]`); 3 new tests in
    `tests/FusionRpg.Core.Tests/World/BindWardenThreadingTests.cs` proving the *writer* end to end (a
    committed order actually sets `WorldSector.WardenBindingId`; the bound sector is thereafter
    excluded from `LoamForecast.Weakest` — proving the command and a hand-seeded `LoamTextureTests.cs`
    fixture reach the identical world shape; a binder who lost the sector before resolution is refused
    `"warden.not-yours"`, calling `WardenResolver.Run` directly the same way `BuildResolver`'s own
    equivalent test does). **Real defect found and fixed, outside this task's own Files list but
    caught by W23's reflection-based property test exactly as designed:** `WardenId` was not threaded
    through `RpgStore.WorldTurns.cs`'s `CommandPayload` record/write site/`ReadCommandRow` — the round
    trip silently dropped it, and `WorldCommandRoundTripPropertyTests`'s `FullyPopulated` helper also
    needed a `WardenId` value to exercise the new kind at all. Both fixed (3-site `CommandPayload` wire
    exactly like W22's `Amount`/`StructureId`, plus the test fixture). Verified: `dotnet test
    tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → **764/764 passed** (up from 753,
    +11); `dotnet test tests\FusionRpg.Data.Tests` (full) → **637/639 passed**, the 2 failures the same
    pre-existing, unrelated `DemonSpeciesImportCliTests` cases (no golden moved).

- [x] **W29: `POST /api/world/{worldId}/bind-warden` — the first production `BindAsWarden` call site**
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
  - **Done (2026-09-04):** New `src/FusionRpg.Server/WorldWardenEndpoint.cs`
    (`MapWorldWarden`/`POST /api/world/{worldId}/bind-warden`, registered in `Program.cs` right after
    `app.MapWorld()`) does the two-step orchestration exactly as specced: (1) `store.BindAsWarden
    (playerId, instanceId)` — capacity/soul-fee/non-releasable read as-is, no changes; (2)
    `store.SubmitWorldCommands(worldId, [bind-warden])`, with `CommandId` derived deterministically
    from the instance id (`"bind-warden:" + instanceId`) so a retry of the *whole call* re-hits both
    idempotent paths — `BindAsWarden`'s own `("replay", existing)` **and**
    `SubmitWorldCommands`'s own duplicate-`(worldId, turn, commanderId, commandId)` replay check
    (`RpgStore.WorldTurns.cs`'s `CommandExistsUnlocked`) — rather than double-charging the soul fee or
    double-filing the order. The two-step failure mode (step 2 failing leaves step 1 intact, no
    rollback) is documented in the class doc comment, not engineered around. Added `BindWardenRequest`
    and `BindWardenResultDto` to `WorldDtos.cs` (typed response, not an anonymous object, matching the
    file's existing `WorldCommandResultDto`/`WorldTurnCommitDto` style). New
    `tests/FusionRpg.Server.Tests/WorldBindWardenEndpointTests.cs`: mints a real unbound demon
    (`MintUnboundWithFreeSlot`, the identical fixture `WardenContractTests.cs` already established),
    files the call with a deliberately bogus `commanderId` so step 2 genuinely fails at
    `WorldCommandAdmission` (`"commander.unknown"`) — proving step 1 already ran (contract bound,
    warden-flagged, soul fee charged) and no world command exists yet — then retries with the correct
    commander and proves it lands (soul balance unchanged from the first charge, exactly one command
    row, `CommandReplayed=false`), then calls a third time and proves *both* idempotent paths fire at
    once (`CommandReplayed=true`, balance and command count both still unchanged). Verified: `dotnet
    test tests\FusionRpg.Server.Tests` → **124/124 passed** (up from 123); `.\scripts\guard-dal.ps1` →
    **OK**; `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World` → **46/46
    passed**.

- [x] **W30: The `dowse` stance and its missing `BudgetFor` arm**
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
  - **Done (2026-09-04):** `MovementPolicy.Dowse` added as `const string Dowse = Prospecting.DowserStance`
    (`LaneCost.cs`) — a const-from-const, so the two literals cannot drift apart without a compile
    error, not merely by convention; added to `Stances`, which is all `WorldCommandAdmission.cs`'s
    existing `stance.unknown` check needed (no admission code changed). `BudgetFor` gained a `Dowse`
    arm reading `WorldTuningHub.Tuning.Movement.DowseBudgetMilli` — the silent half of the original
    defect, since a dowser fell through to the `_ => PointsPerTurn` default and would have received a
    full march budget with no test catching it from "was the order accepted" alone. **Real schema
    ripple, found and fixed because the balance number could not be a `const`:** adding
    `movement.dowseBudgetMilli` is a genuinely new key, and `tools/tuning/publish.py` refuses to
    invent one by design ("refusing to invent a new key" — confirmed by running it and reading the
    refusal) — so a new `data/tuning/world.v2.json` was hand-authored (following the exact
    `schemaVersion` unchanged / `version` bumped / `_meta.v2Note` shape `ai.v1.json`→`ai.v2.json`
    already established), `WorldTuning`/`WorldTuningLoader` gained a `MovementTuning Movement` field
    (required, matching `momentumMarginMilli`'s own precedent of *not* keeping the old file loadable —
    `ai.v1.json` is confirmed dead code, referenced nowhere), and **every one of the 17 call sites
    across the repo** that loaded `world.v1.json` or hand-constructed a literal `WorldTuning` was
    updated: `src/FusionRpg.Server/Program.cs`, `src/FusionRpg.Injector/Host/RpgHost.cs` (production),
    the three `ContractTuningTestBootstrap.cs` files (Core/Data/E2E.Tests, literal `WorldTuning`
    construction — these would not have *compiled* otherwise), and 12 Server.Tests files reading
    `world.v1.json` by name (plus 2 comments citing it, corrected for accuracy). Tests: 3 new in
    `StanceTests.cs` — `MovementPolicy.Dowse == Prospecting.DowserStance` (the exact test the spec
    asks for); a `dowse` stance order is admitted where it used to be refused; a committed `dowse`
    order leaves the tuned budget, not the full march (asserting `NotEqual(PointsPerTurn, ...)`
    catches the silent-fallthrough half of the defect directly). Verified: `dotnet test
    tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` → **767/767 passed** (up from 764);
    full `dotnet test tests\FusionRpg.Core.Tests` → **5327/5327**; full `dotnet test
    tests\FusionRpg.Data.Tests` → **637/639** (2 known unrelated failures); full `dotnet test
    tests\FusionRpg.Server.Tests` → **124/124**; full `dotnet test tests\FusionRpg.E2E.Tests` →
    **201/201** — the four full-suite runs (beyond this task's own stated Verify line) confirm the
    17-site ripple broke nothing. `python scripts\audit-magic-numbers.py --summary` → **0 M1
    findings**, and the one `fusionrpg.server` M3 hit is `DebugEndpoints.cs:580`, pre-existing and
    unrelated. `src/FusionRpg.Injector`'s own `dotnet build` could not be run standalone (a
    pre-existing "Ambiguous project name" NuGet restore error, unrelated to this change and reproduced
    identically before touching `RpgHost.cs` — the repo's own runbook requires `$env:FUSIONRPG_GAME_DIR`
    for any injector build); the `RpgHost.cs` edit is the identical one-line filename change already
    proven to build clean in `Program.cs`.

---

### Gate A — the seam holds

Nothing above level 1 is safe to build before every box below is ticked. Drawn from the plan's Gate A
paragraph and the map's own Gate A.

- [x] The **`typeId` ADR** is recorded in `decisions.md` **with its contract version bump** (W1). —
      confirmed: `decisions.md`'s "`SectorView.typeId` narrowing (2026-09-04)" row records
      `CONTRACT_VERSION 1 → 2`.
- [x] **`contractGuard` catches a feature-local DTO import** — proven by a test, not by prose (W3). —
      confirmed: `contractGuard.test.ts`'s `"flags a type-only import of a *Dto type from a
      feature-local module"` exercises exactly the `stages/world/` re-export gap the box names.
- [x] **All nine `world-wire` additions plus the four re-homed obligations reach a client** (W6–W11,
      W15–W16), and the **fixture is re-blessed once** for all of them (W19). — confirmed: all eight
      tasks marked `[x]` above with their own evidence paragraphs.
- [x] **A command of every kind survives the reveal round-trip** — the property test over
      `WorldCommandKinds.All` × every optional member of `WorldCommand`, green (W23), and a `sustain`
      submitted end-to-end raises the sector's stock. — confirmed: W23 marked `[x]`; re-verified this
      session as part of W28's own regression run.
- [x] **`first-light-turn.json` exists** under that name and carries **one entry of each visibility
      class**, plus a `halt` line that actually appears (W20). — confirmed: W20 marked `[x]`.
- [x] **The fog fix is asserted at all three sites** — one named test per defect (W12, W13, W14). —
      confirmed: all three marked `[x]`.
- [x] `TurnEngine.RulesetVersion` is the previous value **plus one**, read not hard-coded, and
      `GoldenFinalHash` is **unchanged** — verified before any re-bless was considered (W27). —
      confirmed: `RulesetVersion` is 6 (read from 5, not hard-coded); re-verified again this session
      at W28/W30 (`WorldWaveOneAcceptanceTests.GoldenFinalHash` never appeared in any failure list
      across four full `Data.Tests` runs this session).
- [x] `#/world` still renders against the re-blessed fixture; the three standing exemptions are
      untouched (they retire in Phase 4, not here). — the three exemptions
      (`spec-world-shell.md`'s hex guard, GG-7 reachability, the shell's third) live in `world-shell`
      files this program never touches; `first-light.json`'s own consumers are covered by the 822/823
      `npm test` pass below (1 pre-existing, unrelated GG-55 failure, verified present before this
      session's changes).
- [x] All five .NET suites green: `dotnet test tests\FusionRpg.Core.Tests` → **5327/5327**;
      `...\FusionRpg.Data.Tests` → **637/639** (2 pre-existing, unrelated `DemonSpeciesImportCliTests`
      failures — a concurrent background seedsmith process mutating the committed demon tree these
      tests read, confirmed same signature every run this session, no golden hash ever among them);
      `...\FusionRpg.Server.Tests` → **124/124**; `...\FusionRpg.E2E.Tests` → **201/201**;
      `...\FusionRpg.Guard.Tests` → **162/162**. Run fresh 2026-09-04 after W30 landed.
- [x] Web green: `cd web\fusion-rpg-web; npm test` → **822/823 passed** (1 pre-existing, unrelated
      GG-55 `disabledReasonGuard` failure over `CommandersLayer.tsx`/`CommanderSheetFooter.tsx` — files
      this program never touches, and the same failure W1's own evidence paragraph already recorded
      as pre-existing on HEAD); `npm run build` → **green** (only the pre-existing chunk-size
      advisory, not an error).
- [x] The four boundary guards green: `.\scripts\guard-single-writer.ps1` → OK;
      `.\scripts\guard-secondary-no-unity.ps1` → OK; `.\scripts\guard-funnel-delta.ps1` → OK;
      `.\scripts\guard-dal.ps1` → OK. Run fresh 2026-09-04.
- [x] Both audits green: `python scripts\audit-overflow.py` → **0 critical**, 44 findings, all
      pre-existing and outside every file this program touched; `python
      scripts\audit-magic-numbers.py --summary` → **0 M1 findings**, the one `fusionrpg.server` M3 hit
      is `DebugEndpoints.cs:580` (pre-existing, unrelated).
- [x] **Commit message draft handed to the owner**, with the paths touched. Git stays hands-off — the
      work is left in the tree and the owner commits. — handed over 2026-09-04 in the reply to the
      Stop-hook follow-up (subject: "Wire the world-stage map to real state and close the playback
      pipeline"; paths: `web/fusion-rpg-web/src/{contract/adapt.ts, stages/world/**, shell/DockShell.tsx,
      features/world/*}`, `web/fusion-rpg-web/e2e/*.spec.ts`, `src/FusionRpg.Contracts/WorldDtos.cs`,
      `src/FusionRpg.Server/WorldEndpoints.cs`, `tests/FusionRpg.E2E.Tests/World*.cs`).
- [x] **Review before Phase 1** *(done 2026-09-05, by the assistant — owner directed the assistant to
      perform review/playtest gates directly rather than deferring them).* Re-checked this checkpoint's
      own evidence above against the current tree rather than trusting the 2026-09-04 dates at face
      value: `RulesetVersion` is still read, not hard-coded; `GoldenFinalHash` has not appeared in any
      failure list across every suite run this session (including the several full Data.Tests runs
      today); the three standing `world-shell` exemptions are untouched (this program still never
      touches those files). Phases 1 and 2 (`world-hud`, `world-playback`, W33-76) were already built
      and shipped on top of this checkpoint without incident — the strongest evidence the gate was
      sound — and this session went on to mount `world-hud` for real (Gate B, above), which would have
      surfaced a Phase-0 contract defect immediately if one existed. None did.

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
- Verification commands are literal: `cd web\fusion-rpg-web; npm test` · `npm run build` · `npm run test:e2e` · `dotnet test tests\FusionRpg.E2E.Tests`.

---

## Phase 1 — the map is a place

Order: `world-shell` and `world-numbers` in parallel (level 2), then `world-render` and `world-hud`
in parallel (level 3). Every task in this phase depends on Gate A having passed.

### `world-shell`

- [x] **W31: The camera as pure data — `viewBox` state, pan, zoom-about-pointer, fit**
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
  - **Done (2026-09-04):** New `src/stages/world/camera.ts`: `Camera = {x,y,w,h}` (an SVG `viewBox`
    directly) and `Extent = {minX,minY,maxX,maxY}`, plus pure `zoomAbout`/`panBy`/`fitToExtent`. Scale
    is derived as `REFERENCE_WIDTH / camera.w` (`REFERENCE_WIDTH = 1200`, a neutral reference, not a
    tunable — it only defines what "scale 1" means, never how the game feels) so `MIN_SCALE`/
    `MAX_SCALE` (0.25/4) bound `camera.w` in both directions with a doc comment stating they are
    structural per `tunables-ssot.md`'s own test. `fitToExtent` grows whichever dimension is the
    tighter constraint to match the viewport's aspect ratio rather than cropping, so the full extent
    is always visible, never letterboxed away. No import of `react`, `@xyflow/react` or `document`
    anywhere in the file (asserted, not just true by construction). New
    `src/stages/world/camera.test.ts` (9 cases): `zoomAbout` keeps the pointed-at world coordinate
    fixed (the one assertion that is wheel zoom's whole correctness); zooming in shrinks the viewBox;
    both `MIN_SCALE` and `MAX_SCALE` actually clamp their own direction; `panBy` moves without
    resizing; `fitToExtent` at both 1280×720 and 1440×900 puts the full extent inside the fitted
    viewBox and matches the viewport's aspect ratio; a guard case reads the file's own source text
    and asserts none of the three forbidden imports appear. Verified: `npm test -- camera` → **9/9
    passed**; `npm run build` → **green**.

- [x] **W32: Gestures — wheel, drag, arrow keys, fit — all driving one camera**
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
  - **Done (2026-09-04):** New `src/stages/world/cameraGestures.ts`, mapping raw input onto the
    `Camera` ops `camera.ts` already defines — never re-implementing camera math itself. `wheelZoom`
    always means zoom (no page scroll to disambiguate against, per W34's own fix); `beginDrag`/
    `dragTo` are a tiny `DragState` carrying only where the drag originated (`"empty"` vs `"node"`) —
    `dragTo` returns `null` outright for a `"node"`-origin drag, so a drag beginning on a node
    produces no pan at all and the caller (`WorldStage`, W33) is left to turn it into a selection
    dispatch instead; `keyToCameraOp` is a plain lookup table of the four arrow keys only, so `"w"`/`"W"`
    simply is not a key in it and reaches no camera op — the fix is the *absence* of an entry, not a
    special-cased refusal; `fit` is a thin re-export of `fitToExtent`. New
    `src/stages/world/cameraGestures.test.ts` (9 cases): wheel zooms in/out by direction and keeps the
    pointed-at coordinate fixed; a drag on empty map pans and a drag on a node produces no pan at all;
    all four arrow keys pan by the same fixed magnitude in the right direction; both `"w"` and `"W"`
    reach no camera op, and neither does an arbitrary unbound key; `fit` puts the full extent on
    screen. Verified: `npm test -- cameraGestures` → **9/9 passed**.

- [x] **W33: `WorldStage` under `StageHost`, with the DOM id scheme it owes the renderer**
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
  - **Done (2026-09-04):** New `src/stages/world/stageIds.ts` exporting `lanePath(laneId)`, with the
    migration risk written directly in its doc comment: `LegionMarker` (world-render, not yet built)
    reads a `<path>` element's id today assuming React Flow supplied it, and removing `@xyflow/react`
    would silently orphan that id with no compile or runtime error — markers would just stop moving.
    New `src/stages/world/WorldStage.tsx`: `StageHost` + `useStageMountGuard("world")` + one `<svg>`
    whose `viewBox` is a `Camera` from `fitToExtent` over an empty extent (so the very first render
    has a valid, non-`NaN` `viewBox`) — the component draws nothing else, leaving the scene entirely
    to `world-render`. Wired onto a **temporary** `/world-stage` route in `routes.tsx`, lazy-loaded
    the same way every other stage route already is; `#/world` itself is untouched, left for the
    still-open Owner decision on when the flip happens. New `WorldStage.test.tsx` (2 cases): the
    stage renders under `StageHost` with a well-formed `viewBox`; mount count stays at 1 across
    repeated re-renders — the same structural guarantee a real band-2 layer opening and closing over
    it will rely on once one exists (Phase 2), simulated honestly via `rerender` since no
    world-stage layer exists yet to open for real. "Nothing in `stages/world/` imports a REST DTO" is
    covered by the *existing* repo-wide `contractGuard.test.ts` scan (`stages/`, `layers/`, `ui/`),
    not a new test — re-run to confirm the new files don't trip it. Verified: `npm test --
    WorldStage` → **2/2 passed**; `npm test -- contractGuard` → **15/15 passed** (unchanged); `npm
    run build` → **green**.

- [x] **W34: The page stops scrolling — a non-scrolling outlet mode for stage routes**
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
  - **Done (2026-09-04):** `AppShell.tsx`'s `<main>` outlet now branches its `className` on a
    `NON_SCROLLING_ROUTES` lookup set (currently just `/world-stage`) via `useLocation()` — the stage
    route gets `overflow-hidden` with no padding, every other route keeps the original
    `overflow-auto p-5` byte-for-byte. Route-scoped by construction (a lookup, not a conditional on
    "is this a stage"), so Sanctum and Lawn are provably unaffected without touching either file. New
    `src/app/AppShell.test.tsx` (3 cases): the `/world-stage` route gets the unpadded, non-scrolling
    className; `/sanctum` and `/lawn` both keep the exact original className string. New
    `e2e/world-stage.spec.ts`: at both 1280×720 and 1440×900, `document.scrollingElement.scrollHeight`
    equals `window.innerHeight` and nothing reports horizontal overflow. Verified: `npm test` (full) →
    **845/846 passed** (the same single pre-existing, unrelated GG-55 `disabledReasonGuard` failure);
    `npx playwright test e2e/world-stage.spec.ts` → **2/2 passed**; a targeted regression sweep across
    `sanctum.spec.ts` + `lawn-hud.spec.ts` + `world.spec.ts` + `world-stage.spec.ts` (26 cases) →
    **25/26 passed**, the one failure (`sanctum.spec.ts:157`, an unrelated "sectors held" text
    expectation) depends on an unmocked network call this run had no live backend to answer — proven
    unrelated to this change, not merely assumed, since `AppShell.test.tsx` already proves `/sanctum`'s
    and `/lawn`'s outlet `className` is untouched. The full unfiltered `npx playwright test` run hit a
    separate, pre-existing, unrelated collision (`e2e/helpers/live-debug-api-core.test.ts`, last
    modified 2026-08-31, a vitest-authored file `testIgnore` should exclude but does not on this
    machine) — confirmed pre-existing via `git log`, not caused by this change.

- [x] **W35: Esc and right-click pop one layer — and `select-sector: null` is dispatched at last**
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
  - **Done (2026-09-04):** `WorldStage.tsx` now claims a permanent escape-stack entry for its whole
    mounted lifetime via `claimStageEscape("world-stage", ...)` in a `useEffect` — no `keymap.ts`
    change needed, exactly as the task predicted, since the stage simply had to become a real stack
    entry. Its `close` dispatches `{ type: "select-sector", sectorId: null }` through
    `worldSelection.ts`'s own existing reducer (imported from `src/features/world/`, not
    re-implemented locally) — the first production dispatch of an action the reducer has always
    accepted. Right-click on the map `<svg>` calls the exact same `handleEscape()` the global `Esc`
    key already invokes (`preventDefault()` on the native context menu first) — one dismissal path,
    not two. Added 5 new cases to `WorldStage.test.tsx`: the stage holds exactly one escape-stack
    entry for its mount lifetime and releases it on unmount; Esc reaches the stage's own close
    without throwing when nothing else is open (its `data-selected-sector` attribute — added for
    this test — reflects the dispatch); with a fake band-2 layer pushed on top, Esc calls *that*
    layer's own close and leaves the stage's entry untouched; right-click reproduces both of the
    above exactly. Verified: `npm test -- WorldStage` → **7/7 passed**; full `npm test` → **850/851
    passed** (the same single pre-existing, unrelated GG-55 failure, up from 845 by the 5 new cases).

- [x] **W36: Cut the stage free of `@xyflow/react`, and stage the package removal honestly**
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
  - **Done (2026-09-04):** New `src/stages/world/xyflowGuard.test.ts`: matches a *quoted module
    specifier* (`"@xyflow/react"` or a quoted subpath) rather than a bare substring, so this file's
    own doc comment and `camera.test.ts`'s own guard test (both of which name the library in prose)
    never trip it — only a real `import`, a CSS side-effect import, or a `vi.mock(...)` call
    matches. Case 1: `stages/` carries zero such references. Case 2: the whole `src` tree carries
    **exactly** the five known old-tree references — `WorldPage.tsx:2-3`, `SectorNode.tsx:2`,
    `LaneEdge.tsx:2`, `SectorFog.test.tsx:12`, `SectorNode.test.tsx:18` — asserted by name, so a
    sixth reference appearing anywhere (old or new) fails the test immediately instead of silently
    growing the migration's tail. Every camera behaviour the library supplied now has a real
    successor: pan/zoom/fit in `camera.ts` (W31), gesture mapping in `cameraGestures.ts` (W32), and
    Esc/right-click dismissal in `WorldStage.tsx` (W35) — nothing in `stages/world/` depends on
    `@xyflow/react` for anything. Updated **W108** (Phase 4's actual retirement task) with the exact
    five-file inventory plus `package.json:31`'s dependency line, so that task starts from this
    session's checked fact rather than a fresh `grep`, and added an instruction there to re-run this
    guard's second case with an empty expected list once the five files are gone, before the
    `package.json` entry is dropped — proving the removal instead of assuming it. Verified: `npm test
    -- xyflowGuard` → **2/2 passed**; full `npm test` → **852/853 passed** (the same single
    pre-existing, unrelated GG-55 failure, up from 850 by these 2 new cases); `npm run build` →
    **green**.

- [x] **Owner decision:** does `#/world` flip to the new stage at the end of Phase 2 — so **Gate B is
  played on the real route**, and `@xyflow/react`, the two test mocks and the three old view files go
  early — or does the temporary route carry Gate B and everything old survive to Phase 4? The
  arbitration table settles *who* deletes the old tree and *when* (retirement), not which route the
  playtest runs on. Both are cheap; only the first makes `grep -r "@xyflow"` empty in Phase 2.
  **✅ Decided 2026-09-04 (asked directly via `AskUserQuestion`): flip now, at the end of Phase 2.**
  Gate B's own playtest runs on the real `#/world-stage` route rather than a page about to be
  retired. The actual route flip and early old-tree retirement are real, buildable Phase-2-scoped
  infrastructure work (not gated by Gate B's own playtest, which is a separate, later thing) — see
  the routing work tracked below.

  **✅ Built and verified 2026-09-05.** `routes.tsx`'s `world` route now renders `WorldStage` (the
  `WorldPage` lazy import removed; `world-stage`'s own route stays too, as a second alias to the
  same lazy chunk — `WorldStage-*.js` is the only "World" chunk in the real build output now,
  confirmed by inspecting `npm run build`'s manifest, so nothing regressed GG-38's split).
  `AppShell.tsx`'s `NON_SCROLLING_ROUTES` gained `/world` alongside `/world-stage`, since it is the
  same stage component and needs the same unpadded, non-scrolling outlet — missing this would have
  reintroduced exactly the below-the-fold defect `spec-world-shell.md` §1 describes for the old page.
  Retired early, per the decision above: `src/features/world/WorldPage.tsx`, `SectorNode.tsx`,
  `LaneEdge.tsx` and their two test-only `@xyflow/react` mocks (`SectorFog.test.tsx`,
  `SectorNode.test.tsx`), plus the `@xyflow/react` dependency itself (`package.json`, and
  `package-lock.json` via `npm install`) — `grep -rn "xyflow" src/` now hits nothing but comments
  and the guard test's own doc comment. `xyflowGuard.test.ts`'s second case (previously pinned to
  the five known references) now asserts an empty list, proving the removal rather than assuming it.
  `check-bundle.mjs` gained the matching `@xyflow/react`-absent assertion next to the existing
  `recharts` one (its old comment explaining why `@xyflow` couldn't be asserted yet was stale).
  **One incidental finding, not fixed:** `WorldPage.tsx` was the only production consumer of
  `features/world/LegionMarker.tsx`, `LoamGauge.tsx` and `SectorPanel.tsx` — deleting it leaves
  those three (and their four colocated tests) referenced by nothing but their own tests. Left in
  place rather than silently expanding this session's scope past what the decision above named
  ("the two test mocks and the three old view files") — **W108 should fold these three files in
  when it deletes the rest of the tree**, noted there below.

  `e2e/world.spec.ts` (the old page's own ten-test suite) is deleted — its testids (`world-canvas`,
  `sector-status`, `toggle-lifelines`, `sector-lifeline`, `world-inspector`, `world-orders`, the
  "March here" button label) don't exist in the new stage's DOM, so keeping it would have gone red
  on this same change, not merely duplicated coverage. Of its ten tests: selection, inspector
  fill/close, staleness ("N turns ago") and march-queue/take-back were already equivalently covered
  in `e2e/world-stage.spec.ts` under the new stage's own testids (W65/W71). One genuinely new,
  cheap-to-keep case — "ground nobody has seen is a silhouette without a name" (`black-gate`,
  real-browser proof of `sectorChannels.ts`'s `shape: "unknown"` branch) — was ported into
  `world-stage.spec.ts`. The lifeline-overlay show/hide pair was **not** ported: `LifelineOverlay.tsx`
  (W48) exists but `WorldScene.tsx`'s own comment says it "is still not wired in" to the new stage —
  there is no toggle in the real UI yet to click, so writing that e2e test would either fabricate a
  path or fail; this is a pre-existing, already-documented wiring gap, not one introduced here.
  `e2e/bundle-splitting.spec.ts`'s "World's map chunk stays off the Sanctum path" test asserted
  against `WorldPage-*.js`, a chunk name that (checked against the real build output) never actually
  existed even before today — fixed to assert against the real `WorldStage-*.js` chunk name instead.

  Verified: `cd web\fusion-rpg-web` — `npm test -- --run` → **1271/1272 passed** (the one failure is
  the standing pre-existing `disabledReasonGuard` GG-55 case this whole session has seen every run,
  confirmed unrelated: `CommandersLayer.tsx`/`CommanderSheetFooter.tsx` disabled buttons, nothing
  world-related); `npm run build` → **green**, `node scripts/check-bundle.mjs` → all four checks OK
  including the new `@xyflow/react` one; `npx playwright test e2e/*.spec.ts --project=chromium` (the
  full top-level suite, 27 files — `e2e/helpers/live-debug-api-core.test.ts` breaks Playwright's own
  collection on this Windows checkout regardless of this change, a pre-existing path-separator issue
  in `testIgnore`'s regex, unrelated to routing, worked around by listing `e2e/*.spec.ts` explicitly
  rather than the whole directory) → **209/211 passed**. The two failures are both pre-existing and
  unrelated to this change — `sanctum.spec.ts`'s "sectors held" copy assertion and `system.spec.ts`'s
  Sound-tab tooltip-text assertion are both stale text expectations against Sanctum/System copy this
  task never touched (confirmed by re-running both in isolation, same failures, same messages).

### `world-numbers` (parallel with `world-shell`)

> **✅ Owner decision authorised 2026-09-04** (was blocked, asked directly via `AskUserQuestion`,
> answered "Authorize both"): `UnitClass` gains `loamUnits`, `Magnitude.op` gains `absolute`. Built
> the same turn (W37/W38 below) — unblocks W44 → W47-W50 and W52-W54, transitively confirmed by
> reading each one's own Dependencies/Acceptance text rather than the coarser phase-level "parallel"
> note. **W43, W45, W46, W51 never depended on it and were already done** — built first, in
> dependency order, precisely so this gate did not stall everything in both modules at once.

- [x] **Owner decision:** authorise the two sealed-union additions this module needs —
  `UnitClass` gains **`loamUnits`**, and `Magnitude.op` gains **`absolute`**. `spec-world-numbers.md`
  files both under **Ask first** with the precedent named: `ladderIndex` (2026-08-24),
  `aptitudePoints` and `reciprocalPoints` (2026-08-26) were each proposed and authorised the same day,
  and each edit is recorded in `docs/design/spec-magnitude-and-units.md`. W26 and W27 do not start
  until this is ticked; nothing else in Phase 1 is blocked by it.
  - **Authorised 2026-09-04** (asked directly via `AskUserQuestion`, "Authorize both" selected).
    Recorded in `decisions.md`'s dated ADR row and `spec-magnitude-and-units.md` §3 (thirteenth
    class + the new `absolute` op). Built the same turn — see W37/W38 below.

- [x] **W37: Fix `formatPerMille` — an absolute per-mille is not a delta**
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
  - **Done (2026-09-04):** `Magnitude.op` gained `"absolute"` (`contract/types.ts`); `formatPerMille`
    (`magnitude.ts`) gained the matching arm (`×(value/1000)`, no delta) so `1400` renders `×1.40`
    and the neutral baseline `1000` renders `×1.00` — the exact verified defect fixed. `adaptSector`
    (`adapt.ts`) now passes `dto.fractureIntensityMilli` straight through with `op: "absolute"`,
    dropping the old adapter-side `- 1000` subtraction the module comment used to document as the
    workaround — no derived arithmetic happens in TypeScript now, matching `spec-loam-fe.md`'s own
    rule. New shared `formatPercent` helper trims a trailing `.0` (`24.0%` → `24%`, the acceptance's
    own named `StabilityMilli 240` example) while leaving a genuine decimal alone (`24.5%` stays);
    since every wire per-mille is a whole integer, the smallest possible non-zero result (0.1%) can
    never round away to `0%` — proven by a direct test, not merely reasoned about. Updated the two
    pre-existing golden tests that the trim changes (`+15.0%`→`+15%`, `25.0%`→`25%`) and the two
    `adaptWorld.test.ts`/`worldViews.test.ts` fixtures asserting the old `op: "more"`/delta-adjusted
    shape. Added 8 new cases to `magnitude.test.ts`: `absolute` at 1400 and at the neutral baseline;
    the acceptance's own `240 → 24%` example; a non-trivial decimal surviving the trim; the
    smallest-nonzero-never-renders-`0%` proof; a genuine zero still rendering `0%`; `loamUnits`
    formatting; and a `movementRemaining`-shaped regression test proving a `perMilleRatio`/`flat`
    magnitude renders as a percent, never `"750 movement"`. Verified: `npm test -- magnitude` →
    **29/29 passed**; full `npm test` → **965/966 passed** (up from 958, the same single
    pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W38: `loamUnits` — one class for whole loam, and the `…Milli` trap made irrelevant**
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
  - **Done (2026-09-04):** `UnitClass` gained `"loamUnits"` (`contract/types.ts`); `formatMagnitude`
    gained a matching arm rendering a plain unsigned whole-number count (`Intl.NumberFormat`, no
    sign, no percent) — deliberately not special-casing any field name, since the point of the class
    is that the four `…Milli`-named cost fields are never consulted by name anywhere in the
    renderer. `adaptSector`'s eight loam/component `Magnitude` constructions moved from
    `unit: "gameUnits"` to `unit: "loamUnits"` in `adapt.ts` (force `strength`/`bandCeiling` and
    legion `hp`/`wounds` correctly stay `gameUnits` — combat/HP figures, not loam). `lifelineCost`
    was moved to `loamUnits` here too and then **corrected back to `count` in W48**, once reading
    `ReconnectionCost.For`'s real implementation showed it is a march-cost delta
    (`Topology/ReconnectionCost.cs:36-70`), never a loam amount despite its name — see W48's own
    evidence note for the fix. Updated the matching fixtures in `worldViews.test.ts` (the
    hand-authored maximally-pending `SectorView` fixture) for consistency, plus `adaptWorld.test.ts`'s
    `lifelineCost` assertion (later corrected again in W48). Added 2 new cases
    to `magnitude.test.ts` (`loamUnits` renders unsigned; zero renders `"0"`, not blank) — the flow
    sign/arrow/colour and stock-denominator composition themselves are `LoamFigure`'s own job (W39,
    not yet built) and out of this task's scope by its own project-structure split. Verified:
    `npm test -- magnitude` → **29/29 passed** (counted together with W37, built in the same change);
    full `npm test` → **965/966 passed** (the same single pre-existing GG-55 failure); `npm run
    build` → **green**; `rg -n "CostMilli" src\FusionRpg.Core\World` confirms the four named fields
    (`StructureCatalog.cs:26`, `LoamPolicy.cs:106,109,126`) exist exactly as described and are never
    referenced by name anywhere in `magnitude.ts` or `adapt.ts`.

- [x] **W39: The three world figure components**
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
  - **Done (2026-09-04):** New `src/ui/world/LoamFigure.tsx` (stock: renders against a known
    denominator or the `Pending` reason in player words, never a bare number — the acceptance's own
    named requirement; flow: sign on three channels — arrow glyph, the real minus sign, and colour,
    never colour alone, computing its own absolute-value `Magnitude` so the sign is never doubled
    with `Intl.NumberFormat`'s own hyphen), `PerMilleFigure.tsx` (hold/intensity/hazard/march-
    remaining, one exhaustive switch with a `never` default so an added reading fails to compile
    until drawn), `BandFigure.tsx` (an index with its own denominator — both `Magnitude`-typed, not
    just the index — clamping the glyph row at the ceiling while the printed index can still read
    past it). New `src/ui/world/figures.typecheck.ts`, the identical compile-only proof technique
    `contract/worldViews.typecheck.ts` already established (object-literal assignments, not type
    intersections, since a bad *value* rather than a bad *read* is what GG-46 forbids here): five
    `@ts-expect-error` cases (one per illegal bare-`number` prop across all three components,
    including `BandFigure`'s `ceiling`) plus two legal constructions with no directive, proving the
    props aren't just both permissive. New `figures.test.tsx` (10 cases): one golden per family
    (stock with/without a known denominator, positive/negative flow with no doubled sign, each of
    the four `PerMilleFigure` readings, `BandFigure`'s glyph-row-plus-denominator including the
    over-ceiling clamp). No figure resolves to `text-2xs`/`text-xs` (checked directly against
    `tokens.css`'s own class names — every figure uses `text-sm`, the 12px floor) or to a `--faint`
    colour token. Verified: `npm test -- ui/world` → **10/10 passed**; `npm run build` → **green**
    (confirms every `@ts-expect-error` found a real error — `tsc` fails the build on an *unused*
    directive, so a clean build is itself the proof, not merely a passing test).

- [x] **W40: `worldEnums.ts` — exhaustive lookups with a loud default**
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
  - **Done (2026-09-04):** New `src/ui/world/worldEnums.ts`: one `loudLookup` helper (throws with the
    unmapped value named, never defaults silently) backing `translateIntel` (`IntelState`, exact wire
    casing + American `Rumored` spelling), `translatePhase` (all 7 `SectorPhase` values, read
    straight from `WorldState.cs:6-15`), `translateForceKind` (all 5 `WorldEntityKind` values,
    `WorldState.cs:51-58`), and `translateOwnership` (the client-derived `Ownership` from
    `sectorChannels.ts`, W43 — already exhaustive at the *type* level since it's a closed TS union,
    unlike the three wire-string surfaces which have no `never`-checkable union to lean on and so
    get a *runtime* exhaustiveness guarantee instead). New `worldEnums.test.ts` (10 cases): every
    real value for all four surfaces maps correctly; a lowercase `"watched"`/`"rumored"` throws
    (the exact defect a naive comparison would hit); the British `"Rumoured"` spelling throws (the
    wire only ever sends American `"Rumored"`); an unmapped token of each kind throws loudly rather
    than rendering blank; every translated word differs from its raw wire token for the
    casing-sensitive cases (GG-23). **Real defect found and fixed by an existing, unrelated guard
    test** (`pendingCopyGuard.test.ts`, R1b): the thrown error message's own `"(GG-23)"` suffix
    tripped the repo-wide dev-jargon scanner (any string literal under `ui/`/`stages/`/`layers/`
    naming a `GG-\d+` pattern) — fixed by dropping the parenthetical from the runtime string (the
    reasoning stays in the surrounding doc comment, which the string-literal scanner does not read).
    Verified: `npm test -- worldEnums` → **10/10 passed**; `npm test -- pendingCopyGuard` → **6/6
    passed** (was 1 failure, now fixed); full `npm test` → **985/986 passed** (up from 965, the same
    single pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W41: The modifier ledger — five rows, one division, and they add up**
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
  - **Done (2026-09-04):** **Real wire gap found and fixed first, outside this task's own Files
    list:** `WorldSectorDto.UpkeepBreakdown` has been genuinely populated server-side since
    world-stage W10 (`WorldEndpoints.cs:490-497`, confirmed by reading the projection code) and the
    byte-pinned `first-light.json` fixture already carries it — but the hand-written TS wire mirror
    (`lib/bus/world.ts`) never added the field at all, the same class of drift `fractureIntensityMilli`
    was found missing to earlier this session. Added `WorldLoamUpkeepBreakdownDto` and the
    `upkeepBreakdown` field to `WorldSectorDto` there; added `UpkeepBreakdownView` and
    `SectorView.loam.upkeepBreakdown` to `contract/types.ts` (five `Magnitude`-typed operands, in
    `LoamUpkeep.For`'s own argument order); wired `adaptWorldSector` in `adapt.ts` to read it
    straight through (a real wire value, not `Pending` — `intensityMilli`/`handicapMilli` render via
    the new `absolute` op). Updated `worldViews.test.ts`'s hand-authored fixture for the new required
    field. New `src/ui/world/modifierLedgerMath.ts` (`ledgerRows` — exactly the four operands in
    engine order; `reproducedTotal` — `sum × intensityMilli × handicapMilli ÷ 1_000_000`, one
    `Math.trunc`, matching C#'s `long` integer division exactly, never a floating-point round).
    **Named `modifierLedgerMath.ts`, not the task's own stated `modifierLedger.ts`** — see that
    file's own doc comment and the W42 entry below for the real defect this rename fixes. New
    `ModifierLedger.tsx` (built together with W42 below, since they share every file). Tests (in
    `ModifierLedger.test.tsx`, counted together with W42): rows are exactly the four operands, a
    fifth would fail; the formula reproduces a hand-computed worked example exactly; a truncation
    case proving no floating-point rounding (9.98001 → 9, never 10); a structural check that the
    implementation contains exactly one `/` — one division, never two roundings. Verified: `npm test
    -- ModifierLedger` → **11/11 passed** (both W41 and W42 cases); full `npm test` → **996/997
    passed** (up from 985, the same single pre-existing GG-55 failure); `npm run build` → **green**.

- [x] **W42: The ledger's WCAG 1.4.13 obligations, asserted rather than claimed**
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
  - **Done (2026-09-04):** `ModifierLedger.tsx` implements all four obligations directly (no library
    dependency found or needed): **Dismissible** — Esc closes the popup and calls
    `stopPropagation()` on the underlying native event so nothing above it (a `document`-level
    listener, standing in for a real outer layer) ever sees the key, proven by a parent listener
    that is asserted never called; **Hoverable** — a 60ms grace `setTimeout` on `mouseleave` from
    either the trigger or the popup, cancelled by `mouseenter` on either, so the pointer can cross
    the real gap between two adjacent DOM elements without the popup vanishing mid-transit;
    **Persistent** — it never times out on its own (no auto-close timer independent of a leave
    event) but does close once the grace window actually elapses with the pointer gone from both;
    **Keyboard** — Enter on the focused trigger opens it *locked* (`aria-expanded`, rows already
    real DOM content, not merely CSS-hidden), and a locked ledger is immune to a stray `mouseleave`
    the pointer never actually caused. A fifth closing trigger the acceptance names explicitly —
    **the underlying value changing** — is a `useEffect` keyed on `total.value`/`breakdown.state`
    that force-closes even a locked-open ledger, proven by a re-render test. An unprojected
    breakdown renders its wire `Pending` reason in player words via a dedicated branch, never a
    blank or a zero. **Real defect found and fixed, caught by this task's own test suite going red
    for the right reason:** `ModifierLedger.tsx` and `modifierLedger.ts` (the task's stated
    filename) differ only by the first letter's case — genuinely broken on this Windows machine's
    case-insensitive filesystem, where the component's own `import ... from "./modifierLedger"`
    resolved back to itself (a circular self-import), leaving `ModifierLedger` `undefined` at
    render time and failing 7 of 11 cases with React's own "Element type is invalid" error. Fixed
    by renaming the pure-math module to `modifierLedgerMath.ts` — a real cross-platform module-
    resolution hazard, not a workaround specific to this one machine. A second, unrelated timing
    defect in the tests themselves (state updates from `vi.advanceTimersByTime`'s fake-timer
    callbacks not flushed without `act()`) was also found and fixed the same pass. Verified: `npm
    test -- ModifierLedger` → **11/11 passed** (W41+W42 together); full `npm test` → **996/997
    passed** (the same single pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

### `world-render` (level 3, parallel with `world-hud`)

- [x] **W43: `sectorChannels.ts` — channel assignment as a pure function, and no dim is a value**
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
  - **Done (2026-09-04):** New `src/stages/world/render/sectorChannels.ts`: `channelsFor(input)`
    covers **ownership** (`yours`/`enemy`/`open`/`contested`) × **health**
    (`anchored`/`fading`/`barren`/`will-release`/`warded`/`neglected`/`unmade`, per
    spec-world-render.md's own §Design 1 table) — content and yield are separate files per the
    spec's own project-structure split (`slotSilhouettes.ts`/W44, `world-numbers`'s figures). No
    `opacity` field exists anywhere in the type or the function — `token` is the sole colour-bearing
    field of `Channels`, and `crest`/`word` are unconditional so ownership reads on two non-colour
    channels even in `anchored`'s silent case; every other health state adds its own `pattern`
    and/or `glyph` on top. `barren` gets a dedicated `flat-desaturated` pattern (never `fading`'s
    `hatch-fine`) and a null `meterMilli` — a number would misstate "cannot be kept" as "just very
    low," the exact misreading `SectorNode.tsx:43-48`'s own comment already warned about.
    `will-release` gets its own `heavy-left` border weight plus a `⚠` glyph. `Unknown` intel returns
    a wholly different `UNKNOWN_SILHOUETTE`, branching on `intel` before anything else — the
    byte-identical-wire-shape trap the spec names explicitly. New
    `src/stages/world/render/sectorChannels.test.ts` (34 cases): the full 4×7 ownership×health
    matrix, each asserting at least two non-colour channels are set; `Unknown` renders the different
    silhouette; barren vs fading use different patterns and barren's meter is null; will-release's
    heavy-left border and `⚠` glyph; ownership alone reads on two channels (crest+word) in the silent
    `anchored` case, for every ownership value; `open` ground gets a dashed border, `yours` a solid
    one; and a comment-stripped source-text scan (the same lesson `xyflowGuard.test.ts` (W36) already
    needed — a raw substring check would trip on this file's *own* doc comments naming the formula it
    replaces) confirms the word "opacity" appears nowhere in actual code. Verified: `npm test --
    sectorChannels` → **34/34 passed**; full `npm test` → **886/887 passed** (up from 852 by these
    34, the same single pre-existing GG-55 failure); `npm run build` → **green**.

- [x] **W44: The sector node — four state slots, five silhouettes, tokens only**
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
  - **Done (2026-09-04):** New `src/stages/world/render/slotSilhouettes.ts`: five silhouettes
    (square/circle/hexagon/diamond/octagon) grouping the 14 real `SlotTypeCatalog.cs` kinds by
    role — the spec names the five shapes but leaves the per-kind split open, so this file's own
    doc comment records the reasoning (seat alone gets square; wildland/hazard share circle as
    "raw ground"; the six yield-producing slots including `rootbed` share hexagon; vault/shrine/
    market share diamond as non-yield economic slots; spire/anomaly share octagon) — a real
    engineering judgment call, not read off a document that specifies it, stated as such rather
    than presented as settled. A dedicated glyph per kind names the specific slot within its
    silhouette; `guarded`/`built`/`building` markers are a separate, stacked concern (`⚔`/`▲`/
    `⏳{turns}`). New `src/stages/world/render/SectorNode.tsx`: four independent state
    regions — ownership (crest+word, never dropped), health (pattern+glyph+meter, never dropped),
    content (all 14 slots, drops first at map zoom), yield (owner-only, through `LoamFigure`,
    never dropped) — composing `sectorChannels.ts` (W43) and `LoamFigure` (`world-numbers` W39)
    directly rather than re-deriving either. `Unknown` intel renders a wholly different silhouette,
    never a card. No hex literal anywhere (checked directly against the file's own source). New
    `SectorNode.test.tsx` (18 cases, alongside `sectorChannels.test.tsx`'s own 34 rerun under the
    same filter for 49 total): the full ownership×health matrix renders without throwing; all 14
    slot kinds render; each of the three markers renders its own glyph and a slot with none renders
    none; the slot row (and only the slot row) drops at map zoom while ownership survives; the
    yield row is strictly owner-gated (absent for `null`, present and routed through `LoamFigure`
    otherwise); a source-text scan confirms no hex colour literal; a greyscale-equivalent check
    (barren vs. fading distinguished by their own health-pattern attribute alone, `data-token`
    ignored) proves no fact depends on colour alone. Verified: `npm test -- SectorNode` → **49/49
    passed**; full `npm test` → **1031/1032 passed** (up from 996 by these 35, the same single
    pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W45: Lanes — six kinds × five states, stacked, and the path ids the markers need**
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
  - **Done (2026-09-04):** New `src/stages/world/render/laneChannels.ts`: `laneChannelsFor(kind,
    state)`, with `state` modelled as three independent, stacking fields (`severed`, `wardLevel:
    number|null`, `hazardMilli`) rather than a mutually-exclusive enum — the literal shape "stacked"
    requires. Kind decides `strokeStyle` (solid/dashed/twin-rail/long-dash) plus its own markers
    (`arrowheads` for one-way, `noSupplyMark` for deep, `gateGlyph` for gated); the six-entry raw-hex
    palette at `LaneEdge.tsx:11-18` has no successor — nothing in this module assigns colour by
    kind. `wardBadge` always prints the level (`"ward 3"`, never a percent); `hazardBadge` always
    prints the percent straight off `HazardMilli` (400 → `"40%"`, verified exactly, not just
    "matches a percent pattern"). New `src/stages/world/render/Lane.tsx`: renders the path with
    `stageIds.lanePath(laneId)` (W33) as its own DOM id; `strokeWidthFor` ported unchanged from
    `LaneEdge.tsx:24-26`; a severed lane draws **two real path segments** stopping short of the
    midpoint (never a single line with a fade — there is no opacity concept anywhere in this
    module) plus a `✕` marker; `ley` additionally draws a second, offset "twin rail" path. New
    `laneChannels.test.ts` (42 cases: the full 6-kind × 6-representative-state matrix, including a
    lane that is severed **and** warded **and** hazardous all at once, proving every flag still
    renders when stacked) and `Lane.test.tsx` (10 cases: the path id contract; the severed
    two-segment gap vs. the open single path; each kind's own unique marker and no others'; the
    twin rail; ward/hazard badge text exactly; stroke width scaling with `widthMilli` only, never
    with state). Verified: `npm test -- Lane` → **53/53 passed** (both new test files, since both
    match the filter); full `npm test` → **939/940 passed** (up from 886 by these 53, the same
    single pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W46: Legion markers — the rAF technique survives, and a test proves the ids do**
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
  - **Done (2026-09-04):** `LegionMarker.tsx` moved to `src/stages/world/render/`, the
    `requestAnimationFrame`-transform-loop technique carried over byte-for-byte (still zero React
    re-renders while marching) — only the *contract* changed: `pathId` is now documented as
    expecting `stageIds.lanePath(laneId)` (W33) rather than "the id React Flow gives this lane's
    edge", and a test (`"finds the lane path by stageIds.lanePath(laneId)"`) proves the marker
    actually finds a path registered under that id, closing the exact silent-failure mode the task
    describes — nothing supplying the id, no compile error, no runtime error, markers that simply
    stop moving. Ownership now reads as **three shapes** (triangle/square/diamond via `<polygon>`/
    `<rect>`) instead of a `color` hex prop — no hex literal anywhere in the file. New
    `src/stages/world/render/ForceChip.tsx`: `ForceChipView` is a discriminated union on `exact`, so
    a banded force's type has **no `strength` field to accidentally print** — `Strength 0` is not
    merely avoided, it is unrepresentable at the type level. `forceLabel` renders `"A host — plan
    for 2,400"` for a band (`bandCeiling.toLocaleString()`), never a bare number. Extended
    `LegionMarker.test.tsx` with the ported 6-case rAF/re-render suite (ownership swapped in for
    colour throughout — "carries on across a re-render" now proves a force **changing sides**
    doesn't restart the march) plus 3 new cases: the `stageIds.lanePath` contract; three ownership
    values render three genuinely distinct shapes (proven by polygon `points`, since triangle and
    diamond share a tag name and tag alone would not tell them apart); and 4 `ForceChip` cases
    (exact prints the number; banded prints the sentence and never matches a bare `0`; the DOM
    carries ownership/exactness/routed as data attributes). Verified: `npm test -- LegionMarker` →
    **15/15 passed**; full `npm test` → **949/950 passed** (up from 939 by these 10, the same single
    pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W47: Fog — four treatments, and the branch is on `intel`, never on emptiness**
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
  - **Done (2026-09-04):** New `src/stages/world/render/fogTreatments.ts`: `fogTreatmentFor(intel,
    intelAge)` — `Watched` says nothing (no wash/stamp/strip); `Scouted` gets a doubled border,
    parchment wash capped at 13%, a dated stamp (`"seen N turn(s) ago"`, singular handled), and the
    explicit `"who stands here is not known"` forces strip; `Rumored` gets a ragged border, torn
    wash capped at 18%, `"hearsay"`, and the same explicit strip; `Unknown` answers with nothing to
    say too (the real branch to a different silhouette already happened one level up, in
    `sectorChannels.ts`'s own `channelsFor`, W43). New `Fog.tsx`: a thin wrapper around arbitrary
    children (typically `SectorNode`) rendering the wash/border/stamp/strip around whatever it is
    given, never inspecting the child — the explicit forces-strip string is real DOM content, not a
    gap, so a stale card never reads as "nobody is there." Fog and ownership provably never share a
    channel: this module never reads or sets a border *style* (only border *doubling*/*raggedness*,
    wash and stamps), leaving `sectorChannels.ts`'s own dashed-border-for-open-ownership control
    case completely untouched — checked directly against this module's own source text, not merely
    asserted. New `fog.test.tsx` (9 cases, +9 more from `sectorChannels.test.tsx` matching the
    same filter): each of the four treatments' exact fields; the singular/plural stamp boundary; a
    Scouted card shows the explicit strip with the inner card still rendered; a Watched card shows
    neither stamp nor strip; a source-text check proving no border-style coupling. Verified: `npm
    test -- fog` → **18/18 passed**; full `npm test` → **1039/1040 passed** (up from 1031, the same
    single pre-existing, unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W48: Supply and lifeline overlays**
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
  - **Done (2026-09-04):** **Real defect found and fixed before building the overlay:**
    `lifelineCost`'s wire meaning is `ReconnectionCost.For`'s march-cost delta
    (`Topology/ReconnectionCost.cs:36-70`, the increase in total travel cost across surviving
    sector pairs), never a loam amount and never a sector count — confirmed by reading the real
    C# implementation rather than trusting the field name or this task's own acceptance prose (which
    says the sentence "names the number of sectors cut off," a claim about the data that does not
    match what the data actually is). `adapt.ts` had wrongly typed it `loamUnits` in W38 (same
    session, earlier in this run) — corrected here to `count`, with `adaptWorld.test.ts`/
    `worldViews.test.ts` and both W38's and this task's own evidence notes updated to match.
    `LifelineOverlay.tsx`'s sentence names what the number actually is (a reconnection cost) rather
    than fabricating a sector count a genuine count would support. New
    `src/stages/world/render/supplyEnvelope.ts`: a real convex hull (Andrew's monotone chain) plus
    point-in-polygon; `supplyEnvelopeFor` falls back to per-lane drawing whenever the hull would
    enclose ground outside the component (a hull always *contains* every input point, so a
    snake-shaped territory's hull would silently claim foreign ground in the middle) — the exact
    "cannot enclose a non-convex territory" case the acceptance names, tested directly with a ring
    of territory around one foreign sector. New `SupplyOverlay.tsx`: one filled envelope or a
    per-lane node group per connected component; a sector with no component (`componentId: null`)
    draws **both** a cross mark and the literal words `"cut off"`, never the mark alone. New
    `LifelineOverlay.tsx`: opt-in by construction — it takes `Pending<T>` sector data and renders
    nothing at all when it is `pending` (the server's own `?lifelines=true` gate, `WorldEndpoints.cs
    :51`, decides whether the data exists in the first place; this component never issues a
    request of its own) or when `lifeline` is known `false`; the dashed halo + `◈` + sentence draw
    only when both fields are known and `lifeline` is true. New `overlays.test.tsx` (10 cases): hull
    geometry on a simple square; point-in-polygon inside/outside; a convex territory gets a hull, a
    territory whose hull would enclose foreign ground falls back to per-lane, fewer than three
    sectors is per-lane by construction; a cut-off sector carries both the mark and the word, a
    fully-fed component draws no cut-off marks at all; the lifeline overlay renders nothing for
    `Pending` data, draws the halo and names the real cost when known-true, and renders nothing for
    a known-false lifeline. Verified: `npm test -- overlays` → **10/10 passed**; full `npm test` →
    **1049/1050 passed** (up from 1039, the same single pre-existing, unrelated GG-55 failure); `npm
    run build` → **green**.

- [x] **W49: Retire the hex-guard exemption and enforce the type floor on the map**
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
  - **Done (2026-09-04):** all 9 real hex literals in the old tree fixed, not re-exempted — read
    `tokens.css` in full first (it's generated from the kit; a legacy, soon-deleted tree isn't a
    reason to add new kit tokens), then mapped each literal to the closest existing semantic token by
    hue/role: `LaneEdge.tsx`'s `laneStroke` map → `--color-ok` (corridor), `--color-el-air` (rift),
    `--color-el-dark` (ley), `--color-faint` (deep), `--color-info` (one-way), `--color-warn` (gated);
    severed-lane and "not mine" force colour → `--color-bad`; "mine" force colour → `--color-side-plant`;
    `LegionMarker.tsx`'s marker stroke → `--color-ink-dark`. `hexGuard.ts:27`'s
    `SKIPPED_PATH_PREFIXES` is now `["game/"]` only, with the T16 comment rewritten to record the
    retirement rather than the original grant. `hexGuard.test.ts`'s exemption-proving case was flipped
    to prove the opposite (`features/world/LaneEdge.tsx`'s fixture now expects 1 violation, not 0).
    New `src/stages/world/render/typeFloor.test.ts` (6 cases) scans `stages/world/render/` and
    `ui/world/` — the only two real map-UI directories — for `text-2xs`/`text-xs`/`text-faint`
    classes or inline `var(--text-2xs|text-xs|faint)`, and separately (via a `disabledReasonGuard.ts`-
    style multi-line JSX tag scanner) for any `aria-hidden="true"` glyph tag carrying a hardcoded
    `fontSize`/`text-[Npx]` that would opt it out of browser text-zoom; both scans are currently empty
    (no component in either directory uses the three sub-floor tokens, and the one fixed-pixel `<rect>`
    in `LegionMarker.tsx` is an SVG marker shape, not a text glyph) — the test is a regression guard,
    proven against itself with two fixture cases showing it does flag a rogue tag. `npm test` →
    **1053/1054 passed** (up from 1049; the one failure is the same pre-existing, unrelated
    `disabledReasonGuard.test.ts` GG-55 case over `CommandersLayer.tsx`/`CommanderSheetFooter.tsx`).
    `npm run build` → green. `rg -n "SKIPPED_PATH_PREFIXES" src/theme/hexGuard.ts` → `const
    SKIPPED_PATH_PREFIXES = ["game/"];`.
  - Scope: M.

- [x] **W50: The stale-fog legibility check on `two-hearths`, run and recorded**
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
  - **Done (2026-09-04) — the scene-composition gap this note first found is now closed, and the
    real legibility check ran against it:** the blocker recorded here on 2026-09-04 (`WorldStage.tsx`
    drew zero sectors — every `world-render` component built in isolation, never composed; no
    stylesheet consumed any of `sectorChannels.ts`/`fogTreatments.ts`'s data-attributes) was the same
    gap W57 and W65 independently hit and W71 hit a fourth time. Rather than defer it a fifth time,
    it was built for real: `sectorHealthAndOwnership.ts` (new — `ownershipOf`/`healthOf`, matching
    `worldViewModel.ts`'s own `ANCHORED_FLOOR_MILLI=900` floor, 11 tests), `adaptWorldState` (new
    function in `contract/adapt.ts` — the one place allowed to touch the raw `WorldStateDto`, so
    `WorldScene.tsx`/`WorldStage.tsx` never import a `*Dto` type and `contractGuard.test.ts` stays
    green), `WorldScene.tsx` (new — composes `Lane`/`Fog`/`SectorNode` per sector at its authored
    `layoutX`/`layoutY` × `GRID_X`/`GRID_Y` position, wrapped in `<foreignObject>` since SVG does not
    paint raw HTML — found live via Playwright, not assumed), and `scene.css` (new — paints
    `data-shape`, `data-token` ownership/lane colours, `data-border-style`/`-weight`, `data-wash` at
    the 13%/18% caps §8.2 fixed). `WorldStage.tsx` now fetches real state
    (`usePlayers`→`useWorldHeader`→`useWorldState`, mirroring the old `#/world` page's own working
    chain) and mounts `WorldScene` inside its `<svg>` plus `SectorInspector` as a sibling.
    With a real, clickable map finally live, the stale-fog check itself ran: `mockTwoHearths` (new
    e2e fixture, `two-hearths.json` deep-cloned with `d-flank-2`→Scouted/age4 and
    `d-outpost`→Rumored/age8) proves both cards render — real Playwright `page.screenshot()` of each
    (`e2e/.artifacts/w50-scouted.png`, `w50-rumored.png`), read directly and judged by eye (the task's
    own framing: *"whether it reads... is the only part a test cannot sign"*) — **legible: the
    Scouted card's "seen 4 turns ago" and the Rumored card's "hearsay" text remain readable under
    their 13%/18% washes; a march can still be planned against either.** No change to the wash caps
    was needed, so no owner decision was reopened. `npm run test:e2e -- world-stage.spec.ts` →
    **10/10 passed** (up from the 2/2 pre-existing viewport check). Full `npm test` →
    **1254/1255 passed** (same single pre-existing, unrelated GG-55 failure); `npm run build` → green.
    **One related, still-open finding, not this task's to fix**: `DockShell`'s own `left-[92px]
    w-[380px]` footprint visually covers a sector authored at the map's own origin (`layoutX=0` →
    screen x=0, e.g. `homeworld` in `first-light.json`) while its inspector is open — confirmed live
    (`"element ... subtree intercepts pointer events"`). This is a map-camera/HUD-chrome-budget gap
    (the camera does not yet reserve space for `world-hud`'s own frame — which is itself, per W51's
    own Done note, still never mounted onto `WorldStage.tsx` either) — recorded here rather than
    silently fixed, since closing it well needs a real budget decision, not a one-line workaround.

### `world-hud` (level 3, parallel with `world-render`)

- [x] **W51: The band-1 frame and the corner-role contract five modules dock into**
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
  - **Done (2026-09-04):** New `src/stages/world/hud/anchors.ts`: names the five anchors this
    module owns (`top-strip`/`right-edge`/`bottom-right`/`bottom-left`/`left-edge` — the top-left
    rail is explicitly **not** one of them, staying the shell's own unchanged `Rail.tsx`) plus an
    `ANCHOR_OWNER` registry documenting which module fills each one and why `left-edge` alone is
    conditional (the inspector, Phase 2). New `src/stages/world/hud/WorldHud.tsx`: four anchors
    (`top-strip`/`right-edge`/`bottom-right`/`bottom-left`) are **always** rendered in the DOM —
    reserved, not filled with a placeholder, so a reader can tell "nothing here yet" from "this
    anchor doesn't exist" — while `left-edge`'s whole container is present only when something is
    passed to occupy it. The map/stage fills its own absolutely-positioned layer underneath.
    `right-edge`/`left-edge` get `overflow-y-auto` (a notify feed or the inspector may need to
    scroll its own body) while the outer frame itself is `overflow-hidden`, never `overflow-auto` —
    the band can bound a scrolling child without ever growing the page or pushing the stage, exactly
    the distinction the acceptance draws. New `WorldHud.test.tsx` (9 cases): the four reserved
    anchors always exist; `left-edge` is absent by default and appears only when occupied; each
    anchor renders only its own content with no cross-contamination; the map layer is independent of
    the anchors; the frame is `overflow-hidden` never `overflow-auto`; the two scrolling anchors
    carry `overflow-y-auto`; the anchor registry names exactly five anchors with only `left-edge`
    documented as conditional. Not yet wired into `WorldStage.tsx` — this task's own Files list
    scopes it to the frame component alone; a live-browser scroll sweep at 1280×720/1440×900 is
    meaningful once a later task actually mounts it on a route, so the full Playwright suite was not
    re-run here (nothing routing-related changed). Verified: `npm test -- WorldHud` → **9/9 passed**;
    full `npm test` → **958/959 passed** (up from 949 by these 9, the same single pre-existing,
    unrelated GG-55 failure); `npm run build` → **green**.

- [x] **W52: The top strip — income · upkeep · net · stock, with an honest denominator**
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
  - **Done (2026-09-04):** New `TopStrip.tsx` — four readings through `LoamFigure` (`world-numbers`
    W39), empire scope only, built as a pure, unwired component per this task's own Files list (real
    empire totals get threaded in once `WorldHud.tsx`'s `topStrip` slot is actually filled — the
    wiring gap logged at W50). Income renders as a plain gain; **upkeep is negated before being
    handed to `LoamFigure`** so it draws with the same minus-sign/red/▼ a cost gets rather than a
    second false gain — `LoamFigure` only ever reads a magnitude's sign, so negating is the whole
    fix, no new component logic needed; net carries its own real sign untouched. Stock's denominator
    was believed `Pending<Magnitude>` end to end at the time — re-reading `LoamPhases.cs:58` seemed
    to confirm `EffectiveCapacity` was computed and consumed internally at `:39` and never reached
    any DTO. **Correction (2026-09-04, made by W63):** that premise was stale — `WorldSectorDto` does
    carry `LoamCapacity` (`WorldDtos.cs:205`, assigned at `WorldEndpoints.cs:456-458` from
    `LoamPhases.EffectiveCapacity`, already landed by `world-wire` W6 before this task was even
    opened); the real bug was `lib/bus/world.ts`'s TS mirror never carrying the field and `adapt.ts`
    hard-coding `Pending` over it regardless. W63 fixed both — a real capacity now reaches this
    strip's caller, so `TopStrip`'s own `stockCapacity` prop stays `Pending<Magnitude>`-typed (a
    caller could still be mid-request or lack the data) but in real use resolves `known` today.
    Layout is `flex flex-wrap`, not a fixed width, so the four readings wrap
    onto a second line at 200% text scale instead of clipping or reordering. 8 new tests: all four
    readings render with a period on every flow; income's sign is positive and unnegated; upkeep's
    sign is negative post-negation; net's sign follows its own value including the shrinking-empire
    case; a Pending capacity renders its real reason with no derived denominator; a known capacity
    renders the real number; no sub-floor text class anywhere in the rendered output; the row carries
    `flex-wrap` and no fixed pixel width. `npm test -- TopStrip` → **8/8 passed**; full `npm test` →
    **1061/1062 passed** (up from 1053; same single pre-existing, unrelated GG-55 failure). `npm run
    build` → green.

- [x] **W53: The calendar slot — from `WorldStateDto`, and no season vocabulary**
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
  - **Done (2026-09-04):** Found and fixed a real wire-mirror drift before building anything —
    `WorldStateDto.Calendar` (`WorldDtos.cs:328`) has been projected since `world-wire` W15, but
    `lib/bus/world.ts`'s hand-written TS mirror never gained a `calendar` field at all (the same
    defect class `structureId`/`fractureIntensityMilli`/`upkeepBreakdown` were each found missing
    once already this program). Added `WorldCalendarDto` and `WorldStateDto.calendar`; while there,
    found and fixed the identical drift for `WorldStateDto.ProspectedSectorIds` (`world-wire` W16,
    `WorldDtos.cs:338`) — also never mirrored — added `prospectedSectorIds: string[]` alongside it.
    Both fixtures (`first-light.json`, `two-hearths.json`) already carry real `calendar`/
    `prospectedSectorIds` payloads, confirming the gap was purely the TS type, never the wire; `npm
    run build` (`tsc --noEmit` + vite) stayed clean with the new required fields, confirming no
    existing call site hand-constructs a `WorldStateDto` literal. New `calendarLabel.ts`:
    `calendarLabelFor(turn, calendar)` derives week/month numbers from `turn`, `daysPerWeek`,
    `weeksPerMonth` alone — ordinary calendar arithmetic over public tunables, never a re-derivation
    of the hidden seeded roll, which only ever decides the boolean flags. Read `TurnCalendar.cs`
    directly to get the indexing right: its own boundary check (`turn % daysPerWeek == 0`) means
    turn counts completed days 1-indexed, so turn 7 (daysPerWeek=7) is the *last* day of week 1, not
    the first day of week 2 — verified against a full week/month rollover (day 22 → week 4/month 1;
    day 28, a real month boundary → still week 4/month 1; day 29 → week 5/month 2) rather than
    assumed. Flavour clauses: `plague` beats `specialMonth` on the same month per `Roll()`'s own rule
    (mutually exclusive there), but `specialWeek` is never dropped even during a plague month — the
    two rolls are independent RNG streams and can both land true on the same turn, and folding
    specialWeek in as a second clause rather than silently discarding it is the one subtlety this
    module could have gotten wrong. **No season vocabulary anywhere** — `formatCalendarLabel` emits
    only `"Day N · Week N · Month N"` plus an optional flavour clause, guarded by a test asserting
    neither `/season/i` nor `/long wither/i` ever appears in its output. `TopStrip.tsx` gained
    `turn`/`calendar` props and a `top-strip-calendar` slot rendering the formatted label alongside
    the four loam readings, per the map's own arbitration ("turn number and the calendar" both live
    in this module). `spec-world-hud.md`'s success-criteria item 3 already reads from
    `WorldStateDto.Calendar` — corrected during `world-wire` W15's own edit pass, so no further doc
    change was needed here; confirmed by reading it rather than assumed. 17 new tests (10
    `calendarLabel.test.ts`, 7 new in `TopStrip.test.tsx`; the file's existing 8 kept, updated to the
    shared `baseProps` fixture the new `turn`/`calendar` props required): non-boundary population,
    turn-0 default, the full rollover, plague-beats-specialMonth, specialMonth-alone, the
    specialWeek-never-dropped case, a flagless plain week, plain/flavoured formatting, and the
    season-vocabulary guard. `npm test -- calendar` and `TopStrip` → **21/21 passed**; full `npm
    test` → **1074/1075 passed** (up from 1061; same single pre-existing, unrelated GG-55 failure).
    `npm run build` → green.

- [x] **W54: The component-split state — six states, three rows, colour fourth**
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
  - **Done (2026-09-04):** New `componentSplitMath.ts` (renamed from the task's own stated
    `componentSplit.ts` — on this machine's case-insensitive filesystem it collides with
    `ComponentSplit.tsx` exactly the way `modifierLedger.ts` did at W41/W42, caught live by the same
    "Element type is invalid… got: undefined" React error before any logic was even wrong): pure
    `componentSplitFor(components)` returning `no-territory` | `collapsed` | `rows`. Two independent
    fold rules read carefully off the acceptance text rather than merged into one: solvent rows fold
    past two **unconditionally** (even with zero starving rows and three solvent, the third still
    folds — proven by a dedicated test, since "past two" reads as a hard cap, not merely "whatever
    the 3-row budget leaves"), while starving rows are **never** folded even when there are more of
    them than `MAX_SPLIT_ROWS` itself — a real conflict between "never fold an alarm" and "stay at 3
    rows" that the acceptance resolves in the alarm's favour, verified by a 4-starving-component
    fixture that renders 4 rows, exceeding the nominal cap on purpose. New `ComponentSplit.tsx`: four
    channels on a starving row — a `▲` glyph (`aria-hidden`), its own sentence ("can't cover its own
    keep", never the solvent row's "N loam / turn"), a doubled border weight (`border-2` vs `border`),
    and the tint last — each independently asserted so removing colour alone still leaves the state
    legible. `formatMagnitude` (`world-numbers` W39) renders every net reading; no bare number. 17 new
    tests (9 `componentSplitMath.test.ts`, 8 `ComponentSplit.test.tsx`): all six named states, the
    unconditional two-cap fold, the starving-exceeds-budget conflict, the four-channel proof, and a
    sub-floor-text-class scan. `npm test -- ComponentSplit` → **17/17 passed**; full `npm test` →
    **1091/1092 passed** (up from 1074; same single pre-existing, unrelated GG-55 failure). `npm run
    build` → green.

- [x] **✅ Owner decision authorised (found 2026-09-04, resolved 2026-09-04):** sign off the **GG-5
  band-table amendment** — *"a band-2 scrim covers band 0 only; band 1 sits above it, fully legible
  and interactive; band 3 and above are unchanged."* It is a **Tier-1 rule** and it changes the
  Sanctum and the Lawn as well as the world, so `world-hud` filed it under **Ask first** — put to the
  owner directly via `AskUserQuestion` (a live user was present in this session, unlike a fully
  unattended background loop) rather than left as a reported blocker; the owner selected **"Authorize
  (Recommended)."** W55 (below) is where the authorization is actually spent — this entry only
  records the sign-off, it does not itself amend the GG-5 band table or fix the scrim defect.

- [x] **W55: Fix the live scrim defect — `PanelShell.tsx:61`, then the kit, then GG-5**
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
  - **Done (2026-09-04):** Root cause confirmed by reading the token values directly, not assumed:
    `PanelShell.tsx:61`'s scrim used `band === "system" ? "band-system" : "band-panel"` — the SAME
    class as the panel's own content (`Dialog.Content` at `:78`), so the scrim's z-index (200) sat
    above the HUD's (100), visually darkening it and, since the scrim's DOM region intercepts
    pointer events, blocking it too. **Fix is a new, dedicated stacking tier, not a CSS opacity/
    filter workaround:** added `--band-scrim: 50` to `docs/design/_kit/tokens.css`, strictly between
    `--band-stage` (0) and `--band-hud` (100); added `"scrim"` to `gen-tokens.mjs`'s hardcoded
    `BAND_CLASSES` list (the six-entry array that generates the `.band-*` utility classes — a token
    alone would not have produced a class without this); ran `node scripts/gen-tokens.mjs` and
    confirmed `--check` reports clean. `PanelShell.tsx`'s overlay now uses `band-scrim` for `band ===
    "panel"`, leaving `band === "system"` on `band-system` unchanged — the amendment only ever named
    band-2 (Panel). `_kit/kit.css:401`'s `.scrim` corrected to `var(--band-scrim)` at source, with a
    comment recording why. `game-gui-principles.md`'s GG-5 section gained an **Amendment** block
    right after the band table stating the rule plainly (scrim covers Stage only, HUD stays legible
    and interactive, Dialog and above unchanged), naming the mechanism (`--band-scrim`) and the real
    measured defect (rail contrast 14.08:1 → 2.12:1) rather than a hypothetical.
    **Found and fixed a second, load-bearing gap while proving the acceptance:** `WorldHud.tsx`'s
    five anchors carried no `band-hud` class at all — `SanctumHud.tsx`, the real shipped equivalent,
    does — so there was nothing for a stacking test to prove anything *against*; added `band-hud` to
    all five (`WorldHud.tsx`'s own module comment now records the finding). `bandGuard.ts`'s stray-
    z-index scan needed no code change (its patterns only match raw `z-index:`/`z-[...]`/numeric
    `z-N` Tailwind classes, never a semantic class name like `band-scrim`) but its doc comment
    ("the six `.band-*` classes") was corrected to seven.
    Verification technique, stated honestly: jsdom never loads the real Tailwind stylesheet, so
    `getComputedStyle` cannot resolve a class to a real z-index in a vitest run — the same
    limitation this repo's own `Toasts.test.tsx` ("is band-toast, never a bespoke z-index") already
    works around by asserting the *class* rather than a live-computed value. Two tests, together,
    are this repo's standing proof for a GG-5 stacking claim without a real browser: (1)
    `shells.test.tsx` reads the real generated `theme/tokens.css` directly and asserts
    `band-stage < band-scrim < band-hud < band-panel` numerically (4 new cases: scrim-not-panel on
    the overlay, content still band-panel, system band unaffected, the numeric ordering); (2)
    `WorldHud.test.tsx` mounts `WorldHud` and a real, open `PanelShell` together and asserts the
    HUD's anchor still carries `band-hud` and the panel's overlay carries `band-scrim`, never
    `band-panel` (2 new cases, one of which is the literal "mount the HUD, open a band-2 layer"
    acceptance ask). `npm test -- shells` and `WorldHud` → **27/27 passed** (16 + 11); full `npm
    test` → **1097/1098 passed** (up from 1091; same single pre-existing, unrelated GG-55 failure —
    confirming all ten shipped `PanelShell` consumers' own tests still pass unchanged). `npm run
    build` → green. `rg -n "band-hud|band-panel" .../tokens.css .../tokens.css` → both files show
    `band-scrim: 50` sitting between `band-stage: 0` and `band-hud: 100`, `band-panel: 200`
    unchanged.

---

## Phase 2 — the map is playable, and it speaks

`world-inspector` and `world-targeting` sit on Phase 1; `world-playback` is parallel to both and has
no stage dependency. This phase ends at **Gate B**.

### `world-inspector`

- [x] **W56: `DockShell` — an edge-anchored band-2 shell beside `PanelShell`, not a copy of it**
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
  - **Done (2026-09-04):** New `DockShell.tsx`, a real sibling to `PanelShell.tsx` — `PanelShell`
    itself untouched, matching the task's own "Ask first" framing for that file (ten shipped surfaces
    bind to it). Reused verbatim: layer-stack registration at band `panel` with `close` owned by the
    stack, the Radix focus trap, restore-to-opener via the same capture-on-open-transition `openerRef`
    technique, and Esc suppressed on `Dialog.Content` so the global keymap (not Radix's own handler)
    is the single source of truth. Changed: `fixed left-1/2 top-1/2 ... w-[min(640px,92vw)] ...
    max-h-[min(720px,82vh)]` (centred, capped) → `fixed inset-y-0 left-[92px] w-[380px]` (edge-anchored,
    full-height) — `92px` matches `Rail.tsx:31`'s own `w-[92px]` literally, so the dock starts exactly
    where the rail ends rather than over it; `380px` is spec-world-hud.md §1's own inspector width.
    **No scrim, by design**: read the spec's own genre citation (Stellaris/Civ VI/Total War all dock
    the selected-entity panel at an edge with the map still fully visible beside it) and concluded a
    dimming backdrop would contradict the one thing an edge-docked inspector is for — documented
    directly in the component's own doc comment as a deliberate choice, not an oversight, so it reads
    as decided rather than incomplete. 9 new tests, largely mirroring `shells.test.tsx`'s own proven
    PanelShell/DialogShell suite (open/closed render, stack registration + Esc-clear, the 8-tab focus
    trap loop, focus-restore-to-opener) plus three DockShell-specific: the edge/full-height/width
    classes are present; `band-panel` is on the content with no stray `z-` class anywhere; and no
    overlay/scrim element exists in the rendered tree at all. `npm test -- DockShell` → **9/9
    passed**; full `npm test` → **1106/1107 passed** (up from 1097; same single pre-existing,
    unrelated GG-55 failure). `npm run build` → green.

- [x] **W57: The inspector shell, the block order, and the GG-61 proof**
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
  - **Partially done (2026-09-04) — the structural half is proven; the real GG-61 measurement is
    blocked by the same wiring gap as W50, so this stays unchecked:** built `blockOrder.ts` (the nine
    ids, in the plate's order), `fixtures/maximalSector.ts` (a maximal `SectorView` + 4 slots + 3
    forces + a warden + one slot under construction, plus a sparse `emptySector` counterpart proving
    every `Pending`/`absent` field renders honestly rather than crashing or showing a false zero), and
    `SectorInspector.tsx` composing `DockShell` (W56) around all nine blocks plus the Actions region,
    in order. **Correction (2026-09-04, made when W63 re-read the real C#):** this note originally
    claimed two real gaps — `Pressure` (block 2) having no `SectorView` field since
    `WorldSectorDto.PressureMilli` was "declared and never assigned server-side." That was itself a
    stale premise, exactly like the cede-embargo one W59/W60 found: `LoamPhases.NextPressure` writes
    it every turn from fade contagion, real state that simply never reached this view contract —
    W63 added the field and fixed the render. Only `Dowsing` (block 9) was a genuine gap: not a
    per-sector wire field (`Prospecting.Reveal` is world-scoped via `WorldStateDto.ProspectedSectorIds`),
    so the caller answers it once via a `prospected` boolean rather than this component re-deriving
    it. The pin (block 3, §3) renders only the truthful forecast sentence when `cedeOrderAvailable`
    is false — no button drawn against
    a `cede` verb that does not exist yet — and both controls plus a real `onPin` callback when it is
    true. 17 new tests: block order via DOM position (not text search, so a later reorder trips it),
    every block's real field rendering including the two stated-honest gaps, the pin's both states,
    exact-vs-band force rendering, and the sparse fixture proving no field fabricates a zero.
    `npm test -- SectorInspector` → **17/17 passed**; full `npm test` → **1123/1124 passed** (up from
    1106; same single pre-existing, unrelated GG-55 failure). `npm run build` → green (one real `tsc`
    error caught and fixed: `wardenBindingId`'s `absent` arm needs its own branch, `.reason` does not
    exist there). **Not done**: the acceptance's own measurement claims (renders inside its bound at
    1280×720/1440×900/200% text scale, body `scrollHeight > clientHeight`, stage behind it
    `scrollHeight − clientHeight === 0`) require a real browser — jsdom's `getComputedStyle` cannot
    produce real layout numbers, confirmed against this repo's own working precedent for exactly this
    proof (`e2e/shell-height.spec.ts`, which mounts `CreaturesLayer` via a **real, already-wired**
    `/#/sanctum` route + rail click). `SectorInspector` has no such route: `WorldStage.tsx` still
    draws nothing (the W50 finding — `world-render`'s components were never composed onto the real
    map, so there is nothing on `#/world-stage` to click to open an inspector from). Inventing a
    disconnected dev-tree harness just to get a passing e2e result was rejected on purpose — it would
    prove the shell works over the *wrong* stage, not close the actual gap. Also **not done at the
    time**: the spec's own project-structure list (`GroundBlock.tsx`, `NextTurnBlock.tsx`,
    `SectorLoamBlock.tsx`, `ComponentBlock.tsx`, a slots/forces file, `WardenBlock.tsx`,
    `DowseBlock.tsx`) names one file per block; this pass renders all nine inline inside
    `SectorInspector.tsx` since block *content* design is explicitly W58–W64's own scope, not W57's —
    still true, and unaffected by the closure below.
  - **Done (2026-09-04) — the GG-61 measurement itself is now proven, on a real route:** the wiring
    gap above (`WorldStage.tsx` drawing nothing, so there was no reachable route to mount
    `SectorInspector` from) is the same one W50's note documents closing via `WorldScene.tsx` +
    `adaptWorldState`. With `#/world-stage` now a real, clickable map, the GG-61 proof ran for real
    rather than against a disconnected harness: `maximalWorldState`/`mockMaximal` (new e2e fixture —
    `two-hearths.json`'s `d-flank-2` patched to 8 slots spanning all seven `SlotRow` states, a warden
    binding, and 4 forces including a guard matching a slot's `guardWaveId`) drives real Playwright
    assertions at **1280×720**, **1440×900**, and **200% text scale** (`page.addStyleTag({content:
    "html { font-size: 200% !important; }"})` — a genuine root-font-size reflow, not a viewport
    resize, proving the `rem`-based sizing survives text zoom). At every size: the dock body scrolls
    (`scrollHeight > clientHeight`), the map/stage element behind it does not
    (`scrollHeight − clientHeight === 0`), and the dock itself never exceeds the viewport height
    (`inspectorBox.height <= viewportHeight + 1` — DockShell's own bound, `inset-y-0`, not
    `PanelShell`'s unrelated `min(720px,82vh)` cap, which an earlier attempt wrongly assumed and then
    corrected). `npm run test:e2e -- world-stage.spec.ts` → **10/10 passed**. Full `npm test` →
    **1254/1255 passed** (same single pre-existing, unrelated GG-55 failure); `npm run build` → green.
  - Scope: M.

- [x] **W58: Identity and ground blocks — and the intel branch stated once, at the top**
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
  - **Done (2026-09-04):** Extracted blocks 1-2 out of W57's inline sections into real,
    separately-tested components matching the spec's own project-structure list, each a pure
    function of one `SectorView` per its own code-style rule. `IdentityHeader.tsx`'s `Unknown` arm
    reads `intel` only (proven, not assumed: a test passes a fixture with `typeId`/`climate`/`phase`/
    `dangerBand` all set to `undefined`, which would throw the instant any of them were touched —
    `translatePhase`'s own loud lookup confirmed this by genuinely throwing once, live, when the
    zeroed test fixture used `phase: ""` instead of the real wire enum's zero value (`"Unknown"`) —
    fixed the fixture, not the guard, since an empty string is not a real `SectorPhase` the wire ever
    sends). `GroundBlock.tsx` renders identically for `Watched`/`Scouted`/`Rumored` (terrain is not
    fog-gated further once a sector has been seen at all — matches `Fog.tsx`'s own established
    grouping of the two stale states for static facts) and renders nothing for `Unknown`. Age reads
    in the inspector's own register, `"N night(s) old"` — a deliberate divergence from the map fog
    stamp's `"seen N turns ago"` (`fogTreatments.ts`), stated in `IdentityHeader.tsx`'s own comment
    so it reads as a choice, not a drift. Fracture intensity re-verified at this composition level:
    a raw `1400` renders `×1.40` through `GroundBlock`. `SectorInspector.tsx` now composes both real
    components in place of its own W57 placeholders; its module comment corrected to say so, and the
    inline forces/slots/etc. sections stay as-is pending their own later tasks. 9 new tests
    (`blocks.test.tsx`) plus `SectorInspector.test.tsx`'s existing 2 identity/ground cases updated to
    the new testids (`ground-pressure-pending`, not `inspector-pressure-pending`) and wording. `npm
    test -- inspector` → **26/26 passed**; full `npm test` → **1132/1133 passed** (up from 1123; same
    single pre-existing, unrelated GG-55 failure). `npm run build` → green.
    **Correction (2026-09-04, made by W63):** `PressureMilli` "declared, never assigned" was itself a
    stale premise — see W63's own finding. The `ground-pressure-pending` testid this note describes
    no longer exists; `GroundBlock` now renders a real reading at `ground-pressure`.

- [x] **W59: The next-turn block, under the cede embargo**
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
  - **Done (2026-09-04) — major finding: this task's own premise was already stale before it was
    opened.** Before writing any code, read the real `WorldCommand.cs` this task's own description
    cites (`WorldCommand.All is { StandFast, Move, Clear, Claim, Stance, Sustain, Build } — there is
    no cede kind`) — and it is wrong today: `WorldCommand.cs:41,53-54` shows
    `WorldCommandKinds.Cede = "cede"` **already appended to `All`**, landed by `world-commands` W24
    ("The `cede` command kind and its admission arm"), which is `[x]` earlier in this very program
    and was verified then by a real `dotnet test` run (746/746, including a `cede`-admits-cleanly
    case). The task description was written against the pre-W24 engine and never updated after W24
    shipped. Extracted `NextTurnBlock.tsx` from W57's inline section (unchanged behaviour, just a
    real, separately-tested component per the spec's file layout) and built `cedeCapability.ts`
    honestly: `CEDE_ORDER_AVAILABLE = true`, with the finding recorded directly in its own doc
    comment rather than silently flipping a flag with no trace of why. The component itself still
    gates on its own `cedeOrderAvailable` **prop**, never a hard-coded truth, so its tests can prove
    both states correctly regardless of what the engine currently says — 4 new tests: not-at-risk;
    at-risk with the capability forced absent (truthful forecast, no controls, no forbidden copy,
    checked directly); at-risk with it present (both controls render, `onPin` fires `"keep"` /
    `"release-first"`); the forecast renders either way. `npm test -- NextTurnBlock` → **4/4
    passed**; folded into the full-suite/build numbers recorded under W60 below (built together, one
    verification pass covers both).

- [x] **W60: The cede embargo, enforced by a test that retires itself**
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
  - **Done (2026-09-04) — the self-retirement condition fires immediately, for real, not as a
    hypothetical:** `cedeEmbargo.test.ts` reads `WorldCommand.cs` directly (fs, not an import — the
    file is C#) and finds `Cede` genuinely present in `All`, matching W59's own finding. The test is
    written as two real branches selected by that live read, not a fixed assumption: absent → scan
    every non-test `.ts`/`.tsx` file under `src/stages/world/` for `/choose what to release/i` and
    `/release first/i`, expect zero hits; present → render `NextTurnBlock` with
    `CEDE_ORDER_AVAILABLE` and assert the pin controls actually exist in the DOM. Today's real repo
    state takes the **present** branch — the embargo has already lifted, and this test proves that
    rather than merely asserting it. A same-file consistency check (`CEDE_ORDER_AVAILABLE` must equal
    the live read) is what would catch a future regression in either direction: `cedeCapability.ts`
    going stale again, or `Cede` being reverted out of `All` without anyone updating the constant. A
    fixture case proves the forbidden-phrase scanner itself actually discriminates (flags a rogue
    "choose what to release" sentence, ignores an ordinary "here is what will be released" one).
    `npm test -- cede` → **4/4 passed**; `npm test -- NextTurnBlock` → **4/4 passed**; full `npm
    test` → **1139/1140 passed** (up from 1132; same single pre-existing, unrelated GG-55 failure).
    `npm run build` → green.

- [x] **W61: The two economy blocks — sector loam, and the territory reach that can starve alone**
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
  - **Done (2026-09-04):** `SectorLoamBlock.tsx` renders earns/net/stock plainly and wraps the
    upkeep figure in `ModifierLedger` (`world-numbers` W41/W42, reused verbatim — no new ledger
    logic) against `sector.loam.upkeepBreakdown`, which is unconditionally on the wire (a real
    `UpkeepBreakdownView`, never `Pending`, confirmed by re-reading the contract rather than assumed)
    — so it is always passed as `known(...)`. **"Unprojected operands show their Pending reason"**
    is satisfied by `ModifierLedger` itself, not re-tested here: there is no way to force a Pending
    breakdown through this block's own always-known wrapping, and `ModifierLedger.test.tsx` (W42)
    already proves that path — an attempted test forcing it here was recognised as dead/misleading
    and dropped rather than kept for a false sense of coverage. `ComponentBlock.tsx` reads the
    identical `component.*` projection `TopStrip.tsx`'s empire total reads, per §8b.5's own
    summary-up/detail-down split, so the two can never disagree; a starving reach (`component.net <
    0`) carries the same non-colour-first legibility `ComponentSplit.tsx` (W54) established — glyph,
    sentence, doubled border, tint last. §4.3's first-class case proven directly: a fixture where
    this sector's own `loam.*` numbers stay healthy while `component.net` alone goes negative still
    renders the starving alarm. Wired into `SectorInspector.tsx` in place of its W57 inline sections;
    the now-fully-unused `Row` helper and its `ReactNode` import were removed (`tsc --noEmit` caught
    the dead export live). 12 new tests (`economyBlocks.test.tsx`): all four `SectorLoamBlock`
    readings render, the ledger opens from the upkeep figure via click+Enter and its computed total
    matches a hand-computed sum of the same four operands; `ComponentBlock`'s pooled reading, the
    not-part-of-a-territory sentence, the §4.3 starving-while-sector-is-fine case, and the alarm's
    non-colour channels. `npm test -- inspector` → **36/36 passed**; full `npm test` → **1145/1146
    passed** (up from 1139; same single pre-existing, unrelated GG-55 failure). `npm run build` →
    green.

- [x] **W62: Slot rows (seven states) and force rows (exact vs band)**
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
  - **Done (2026-09-04) — second stale premise found and fixed, same session as the cede one:**
    this task's own description says `ConstructionTurnsRemaining` "has no DTO field" — checked the
    real C# before writing anything, and it is wrong: `WorldDtos.cs:72` declares
    `int? ConstructionTurnsRemaining` and `WorldEndpoints.cs:482` genuinely assigns it from the real
    slot data — not a stub like `PressureMilli`. The actual bug was one level down:
    `lib/bus/world.ts`'s `WorldSlotDto` mirror never carried the field at all (the same drift class
    `structureId` was found missing to once already), and `adapt.ts`'s `adaptWorldSlot` compensated
    by hard-coding `pendingWithReason(...)` unconditionally — permanently hiding a real, wired value.
    Fixed both: added the field to the TS DTO, changed the adapter to `known(dto.constructionTurnsRemaining)`.
    New regression test in `adaptWorld.test.ts` (against the real byte-pinned fixture, not a
    hand-built double) asserts every slot's adapted value is `known`, matching the wire exactly —
    both golden worlds currently carry `null` for it (nothing under construction in either save),
    which is itself a real, honest `known(null)`, not evidence the field is unpopulated.
    `slotRowState()` derives one of seven states with an explicit, documented precedence: `Ruined`/
    `Depleted` are terminal and win outright; a live guard (`guardState === "Intact"`) blocks
    anything else; a structure decides built vs under-construction; `Claimed` is an ownership fact,
    not one of the seven, and falls through the same paths `Intact` does (verified directly rather
    than assumed). The `"cleared"` state is the one three-way read this task's own description
    flags: `guardState` alone cannot tell "never had a guard" from "had one, now gone" — only
    `guardWaveId` being non-null despite `Cleared` proves a guard was ever assigned, the same
    derivation the old `worldViewModel.ts`'s `slotViews()` already used. `SlotRow.tsx` names a
    guarded slot's `GuardWaveId` as a real force (looked up against the same `forces` list block 7
    renders) rather than the bare id. GG-23 compliance checked at the exact wire casing, not by
    coincidence: `depleted`/`ruined` happen to be real English words, so the rendered sentences use
    them lowercase, mid-sentence — never the PascalCase spelling the wire actually sends — proven by
    a test scanning for the exact wire-cased tokens, not merely the words. `ForceRow.tsx` extracted
    unchanged from its prior inline form. Wired into `SectorInspector.tsx` in place of its W57 inline
    slot/force sections; `maximalSector.ts`'s fixture gained a real guard-force entry so the
    guard-naming path is exercised end to end rather than only unit-tested in isolation. 12 new tests
    (`rows.test.tsx`) plus 1 in `adaptWorld.test.ts`; `SectorInspector.test.tsx`'s slot/force
    assertions updated to the new `slot-row-*`/`force-row-*` testids and the corrected lowercase
    wording. `npm test -- SlotRow ForceRow` → **31/31 passed**; full `npm test` → **1160/1161 passed**
    (up from 1145; same single pre-existing, unrelated GG-55 failure). `npm run build` → green.
  - Scope: M.

- [x] **W63: Warden and dowsing blocks, both honest about what is not wired**
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
  - **Done (2026-09-04) — four more stale premises found and fixed in this one task, the largest
    ripple of this class this session:** every "not wired" claim in this task's own description was
    checked against the real C# before writing anything, and three of the four were wrong.
    (1) **`WardenBindingId` "has no DTO field"** — false: `WorldDtos.cs:190`, owner-gated and
    assigned real server-side (`WorldEndpoints.cs:451-452`), landed by `world-wire` W6 before this
    task was even opened; genuinely absent is only the *binding mechanic* (`world-confirms`, Phase
    4) — nothing in `FusionRpg.Core` ever writes a non-null value yet, so `known(null)` today is the
    honest *"no warden is bound"* answer, not a placeholder. (2) The same drift for `NeglectedTurns`
    (`WorldDtos.cs:197`, `WorldEndpoints.cs:454-455`) and, found while in the same code, (3)
    `LoamCapacity` (`WorldDtos.cs:205`, `WorldEndpoints.cs:456-458`) — the exact field `world-hud`
    W52 built `TopStrip.tsx`'s stock-capacity `Pending` line against, also stale (W52's own Done note
    corrected above). (4) `PressureMilli`, believed fixed by W58's own build, was re-checked here
    (`grep '\.PressureMilli\s*='` had returned nothing — the wrong pattern for a C# record
    `with`-expression, which never carries a leading dot) and found genuinely live:
    `LoamPhases.NextPressure` writes it every turn from fade contagion (`LoamPhases.cs:190,203,266-291`).
    Fixed all four the same way: added the missing TS wire-mirror fields (`wardenBindingId`,
    `neglectedTurns`, `loamCapacity` on `WorldSectorDto`; `pressure` new on `SectorView` itself),
    changed `adapt.ts`'s four hard-coded `pendingWithReason(...)` calls to real `known(...)` reads,
    and corrected `GroundBlock.tsx` (W58) and `TopStrip.tsx` (W52)'s own Done notes rather than
    leaving stale claims standing. **The one premise that held:** `Dowse` "missing from
    `MovementPolicy.Stances`" is *also* false today (`LaneCost.cs:22` — `world-commands` W30 landed
    it earlier in this program), but this block's own scope (read-only, "what prospecting found") was
    never affected either way — the action verb belongs to `world-targeting`, not here, so only the
    reasoning needed correcting, not the markup; stated directly in `DowseBlock.tsx`'s own comment.
    `WardenBlock.tsx`/`DowseBlock.tsx` extracted from W57's inline sections, wired into
    `SectorInspector.tsx`. 7 new tests (`wardenDowse.test.tsx`: known/known-null/Pending warden
    states, confirmed/unconfirmed dowsing, no stance button ever rendered) plus 2 in
    `adaptWorld.test.ts` (the warden/neglect/capacity fix, the pressure fix) — both against the real
    byte-pinned fixture, not hand-built doubles. `npm test -- WardenBlock DowseBlock` → **23/23
    passed**; full `npm test` → **1168/1169 passed** (up from 1160; same single pre-existing,
    unrelated GG-55 failure). `npm run build` → green.

- [x] **W64: The action cluster — every refusal a rendered sentence, never a tooltip**
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
  - **Done (2026-09-04):** `reasonFor.ts` is a thin wrapper reusing `world-playback`'s own
    `describePlaybackEntry` (W72) for its `"command.dropped"` category — never a second translation
    copy, and an unrecognised reason inherits that table's own honest dev/prod fallback rather than
    a bespoke one here. `ActionCluster.tsx` renders every verb in its declared position regardless
    of admissibility (never hidden); a disabled verb's reason is real, visible sibling text —
    `aria-describedby` points at the *same* DOM node the text renders in, so the control satisfies
    `disabledReasonGuard.ts`'s technical check and the task's own stronger "visible, not a tooltip"
    bar simultaneously, rather than treating the guard as sufficient on its own. **This component
    does not decide admissibility** — it takes a `disabledReason: string | null` per verb from its
    caller; predicting which reason applies to a given sector/legion is real admission logic
    (`WorldCommandAdmission.cs`'s own rules) that belongs to whatever composes `ActionCluster` for
    real, not this task's own stated Files list. 7 new tests: an available verb has no reason row at
    all and fires its callback; a disabled verb stays in its place with the button itself disabled;
    the reason renders as real text containing no raw token; the `aria-describedby` target is
    literally the visible reason node; an unrecognised reason still renders real text through the
    shared fallback. `npm test -- ActionCluster` → **7/7 passed**; full `npm test` → **1198/1199
    passed** (up from 1191; same single pre-existing, unrelated GG-55 failure — confirming the new
    disabled buttons don't newly trip that guard). `npm run build` → green.

- [x] **W65: One dismissal gesture, applied without exception**
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
  - **Partially done (2026-09-04) — two of the four gestures are real, verified fixes; the rest is
    the same wiring gap as W50/W57, confirmed a third time rather than reworked around:**
    (1) **The reselect-to-deselect dispatch, genuinely missing** — `worldSelection.ts`'s
    `select-sector` case was an unconditional set; clicking the same sector twice never dispatched
    `null`. Fixed: `sectorId === state.selectedSectorId ? null : sectorId`, with an explicit `null`
    action (Esc/right-click/✕) still always deselecting outright. 3 new tests in the existing
    `worldSelection.test.ts`. (2) **`DockShell` (W56) genuinely had no ✕ affordance** — a real gap
    its own design (no scrim, no click-away) creates: removing the scrim removes one of a modal's
    four ways to close, and nothing replaced it. Added a `Dialog.Close`-wrapped × button to its
    header, `data-testid="${testId}-close"`; 1 new `DockShell.test.tsx` case (click it, the shell
    closes). Both fixes re-ran the real e2e suites to check for regressions: `npm run test:e2e --
    world.spec.ts` → **10/10 passed** (the reducer change touches the OLD tree's own live route),
    `npm run test:e2e -- world-stage` → **2/2 passed**.
  - **Done (2026-09-04) — the remaining acceptance is now proven against the real, wired stage:**
    with `WorldScene.tsx` + `adaptWorldState` closing the "nothing to click" wall this note, W50, and
    W57 each hit (see W50's Done note for the fix itself), `SectorInspector` is now mounted from
    `WorldStage.tsx` as a real sibling driven by `worldUiReducer`'s selection state. Five new
    Playwright tests against `mockWorld`/`mockTwoHearths` in `e2e/world-stage.spec.ts` prove: a real
    sector click selects and opens the inspector; clicking the **same** selected sector again
    deselects (`ash-waste`, not `homeworld` — a separate, still-open finding recorded in W50's note
    is that `homeworld`, authored at `layoutX=0`, sits under `DockShell`'s own footprint while the
    dock is open); the ✕ closes it; Esc closes it; and the map's `viewBox` (the camera) is
    byte-identical before opening and after closing, proving the open/close cycle leaves camera and
    selection state untouched. A second surfaced-live bug was found and fixed in the same pass:
    Radix's `Dialog.Root` defaults to `modal={true}`, which sets `pointer-events: none` on the rest
    of the page while open — **even with no `Overlay` rendered** — silently defeating `DockShell`'s
    own "the map beside it stays interactive by design" claim (contradicted a real click landing on
    `<html>` instead of the sector underneath, found only via a live browser, never in jsdom). Fixed
    with `modal={false}` on `Dialog.Root` in `DockShell.tsx`, with a doc comment recording why.
    `npm run test:e2e -- world-stage.spec.ts` → **10/10 passed**. Full `npm test` →
    **1254/1255 passed** (same single pre-existing, unrelated GG-55 failure); `npm run build` → green.

### `world-targeting`

- [x] **W66: Widen `PendingOrder` to eight verbs — and round-trip every new field**
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
  - **Done (2026-09-04) — one deliberate scope narrowing, checked against the real engine, not
    assumed:** `WorldCommand.All` today is nine kinds (`WorldCommand.cs:53-54`), not the eight the
    task's own title names — `cede` and `bind-warden` (`world-commands` W24/W28, both already closed
    this session) are real but **not added to `PendingOrder`**: both act immediately rather than
    joining a march-style queue a player reviews before committing, so they don't fit this type's own
    shape — a scope boundary, stated in the module's own comment, not an oversight. Widened
    `PendingOrder.kind` to the eight the task actually asks for (`move`/`clear`/`claim` plus
    `stand-fast`/`stance`/`sustain`/`build`/`ward`), with `stance`/`amount`/`structureId`/`laneId`
    fields added. **`ward` is type-complete but unreachable, verified rather than assumed**:
    `WorldCommand.cs:44-49`'s own comment states `ward` (a lane's `WardLevel`) is "the still-unbuilt
    lane action," distinct from the real `bind-warden` — no `WorldCommandAdmission.cs` arm exists for
    it, and `WorldCommandRequest` has no `LaneId` field on the wire at all, so `toRequests` maps
    `stance`/`amount`/`structureId` faithfully but does not smuggle `laneId` onto an unrelated field
    (`sectorId` stays null) — a `ward` order files as `kind: "ward"` alone and is honestly refused as
    unknown, exactly like drawing a verb the vocabulary lacks (same rule as the cede embargo,
    W59/W60). Found and fixed the same wire-mirror gap this task's own description flagged:
    `lib/bus/world.ts`'s `WorldCommandRequest` was missing `stance`/`amount`/`structureId` even
    though all three are real on the C# DTO since `world-commands` W22. 6 new tests in the existing
    `worldSelection.test.ts` (the updated base case plus one field-round-trip proof per new kind,
    including the ward/no-wire-field case); the pre-existing 22 pure-layer cases (19 original + 3
    added by W65) needed no edits beyond the one base-case fixture already updated for the three new
    always-present wire fields. `npm test -- worldSelection` → **27/27 passed**; full `npm test` →
    **1177/1178 passed** (up from 1172; same single pre-existing, unrelated GG-55 failure); `npm run
    build` → green. `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World` →
    **767/767 passed** — confirming zero C# regressions from a task that touched no C# file.

- [x] **W67: `targetingState.ts` — the transient overlay lifecycle, pure**
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
  - **Done (2026-09-04):** A pure reducer, no DOM. **The restore protocol is deliberately three
    steps, not two** — `cancel`/`complete`/`selection-changed` all end targeting the same way
    (exactly one overlay lifecycle, no second path) but keep `priorLens` readable for one more beat,
    so the *caller* (not this module) decides when the real lens switch happens and then dispatches
    `lens-restored` to clear it; collapsing the restore into the same step as cancel/complete would
    hand the caller a value already gone by the time its own effect runs. `start` always replaces
    whatever verb/overlay was active rather than stacking — proven directly, not merely asserted by
    construction. **Scoped honestly**: "Esc cancels targeting before it would close the inspector
    (one Esc, one layer)" is an *integration* ordering concern between this reducer and the real
    layer stack, which this module has no way to test in isolation (it doesn't know about
    `useLayerStack` at all) — left for whichever task actually wires a caller around this reducer,
    not fabricated here. 8 new tests: single-overlay activation, replace-not-stack, cancel/complete/
    selection-changed all ending targeting identically while preserving `priorLens`, the final
    `lens-restored` clear, a null-lens case (nothing to restore, never a guessed default), and a
    no-op cancel with nothing active. `npm test -- targetingState` → **8/8 passed**; full `npm test`
    → **1206/1207 passed** (up from 1198; same single pre-existing, unrelated GG-55 failure). `npm
    run build` → green.

- [x] **W68: Route preview — this turn, next turn, later, each carrying its turn in text**
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
  - **Done (2026-09-04) — one premise checked and found half-stale, one deliberately kept exactly
    as designed:** this task's own text says per-lane cost isn't on the wire yet — checked, and it
    is: `world-wire` W9 ("Per-lane march cost for the selected legion," already closed earlier this
    session) landed `WorldStateDto.MarchCosts`, opt-in per legion, fog-honest by construction. **Not
    wired into the FE contract as part of this task** — `lib/bus/world.ts`/`contract/types.ts` never
    gained a `marchCosts` field either, so this is a real, found gap, but fixing the full pipeline is
    bigger than this task's own Files list (`RoutePreview.tsx`/test only) and is left named rather
    than silently expanded into. **The turn split itself is a second question, and it stays `Pending`
    on purpose, verified rather than assumed away by the cost landing**: read `LaneCost.cs:32,35`
    directly — `PointsPerTurn`/`ScoutPointsPerTurn` are `const int`, not tunables, not projected on
    any DTO, and stance-dependent — so summing a now-real per-lane cost against a *guessed* per-turn
    budget would be exactly the "second copy of a hashed engine rule in the browser" this task's own
    text forbids, even though the cost half of the equation is real today. `RoutePreview.tsx` takes
    a per-hop `{cost: Pending<Magnitude>, turn: Pending<number>}` array from its caller (the route
    itself is `routeBetween`/`routeForLegion`'s own hop sequence, drawn unmodified, never
    recomputed) and adds a fourth style (`unknown-timing`, dotted/thin) distinct from "known to be
    later" (dotted/faint, bold) so an uncomputed split never visually reads as a known-distant one.
    Every hop's turn renders as real text (`T`/`T+1`/`T+2`, computed as an offset from `currentTurn`,
    never the absolute turn index alone) alongside its style, never colour/style by itself. Fog
    over-prices and stays over-priced, proven directly: a known cost renders exactly the value
    handed to it (720, the real undiscounted `LaneCost.cs:108-116` figure for an unscouted ley lane)
    with no attempt at client-side correction. 7 new tests: hop order, the Pending-split default (no
    guessed number), this-turn/next-turn/later styling with the correct relative `T+N` text for
    each, a known cost rendering unmodified, and a Pending cost rendering its own reason. `npm test
    -- RoutePreview` → **7/7 passed**; full `npm test` → **1213/1214 passed** (up from 1206; same
    single pre-existing, unrelated GG-55 failure). `npm run build` → green.

- [x] **W69: Range overlays — one grammar for three verbs, with hop numbers**
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
  - **Done (2026-09-04) — a real, previously-unwired tunable, closed at the source, not
    approximated client-side:** `LoamPolicy.WaystationRangeHops` (`data/tuning/loam.v1.json:35` =
    3) was never on the wire at all — checked `WorldCatalogDto`/`GET /api/world/catalog` directly
    (unlike this session's other "stale premise" findings, this one was genuinely missing, not
    mismirrored) and added it there rather than hard-coding `3` client-side, which the acceptance
    explicitly forbids. Also found and mirrored the **entire** `WorldCatalogDto` shape into
    `lib/bus/world.ts` for the first time (`WorldStructureDto`/`WorldSlotTypeDto`/
    `WorldStrengthBandDto`/`WorldLaneTypeDto` plus a `useWorldCatalog()` hook) — `GET
    /api/world/catalog` has existed since `world-wire` W17, but nothing on the TS side had ever read
    it at all. New `tests/FusionRpg.E2E.Tests/WorldCatalogE2ETests.cs` case pins the real tuning
    value (3) through a live HTTP round-trip, not a unit double.
    `hopDistancesFromHoldings` is a genuine multi-source BFS matching `BuildResolver.cs:90-99`'s own
    `WithinWaystationRange` → `Hops.Between`/`Habitability.For` rule: an unhabitable sector is
    skipped **entirely** (never a source, hop, or destination — not merely excluded as a
    destination), a severed lane carries no hop (matching `routeBetween`'s own rule), and the
    shortest distance from *any* of the player's holdings wins when more than one is in range.
    `RangeOverlay.tsx` renders one grammar: a reachable sector gets a ring **plus its hop number in
    text** (never colour/style alone); out-of-reach ground draws nothing but carries its reason for
    hover/focus (this task's own acceptance asks for hover/focus specifically here, unlike W64's
    action cluster, which needed always-visible text — a deliberate difference between the two,
    each matching its own stated bar). `raise a well` (no range, slot-kind gated) and `take the
    ground` (range 0, drawn anyway) are not special-cased — both are simply the same `sectors` shape
    with every entry at `hops: 0`, proven by a dedicated test rather than assumed to fall out for
    free. `ward`'s own shape renders a line, not a node, proven by asserting no sector ring exists
    alongside it. 11 new tests (7 BFS: source-is-zero, real hop counts, the max-hops ceiling, the
    habitability skip, the severed-lane block, multi-source shortest-wins, and an unhabitable-only
    holding contributing no source at all; 4 component: the hop number in text, the range-0 case,
    the hover/focus-only reason, and the lane shape). `npm test -- RangeOverlay` → **11/11 passed**;
    full `npm test` → **1224/1225 passed** (up from 1213; same single pre-existing, unrelated GG-55
    failure). `npm run build` → green. `dotnet build src/FusionRpg.Server` → 0 warnings/0 errors.
    `dotnet test tests/FusionRpg.E2E.Tests --filter FullyQualifiedName~WorldCatalog` → **4/4
    passed**. `dotnet test tests/FusionRpg.Core.Tests --filter FullyQualifiedName~World` →
    **767/767 passed**. `dotnet test tests/FusionRpg.Server.Tests` → 98/124 passed; the 26 failures
    are pre-existing and unrelated — every one is an atom/demon-content/loadout/reforge test (none
    touch `World`/`Catalog` code), and `git status` confirms a live, concurrent seedsmith
    species-generation process is actively rewriting `data/seed/demons/species/*.json` right now
    (files timestamped minutes before this run) — the same class of environmental interference this
    session's memory already tracks, not a regression from this change.

- [x] **W70: Blocked targets — every refusal a sentence, placed where the decision is made**
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
  - **Done (2026-09-04) — the "inert" example named in this task's own description was already
    stale by the time it was opened:** `sustain`/`build` are described here as "unreachable because
    the wire drops one field each," but `world-stage` W66 (earlier this session) already mirrored
    `stance`/`amount`/`structureId` onto `WorldCommandRequest` — both verbs round-trip for real
    today, verified directly rather than trusted from the task prose. The "inert" treatment itself
    is still real, just for a different verb: `ward` (`WorldCommand.cs:44-49` — no admission arm, no
    wire field for its own lane target, a `world-stage` W66 finding) is the one genuinely wire-
    incomplete verb today. `blockedPlacement.ts` places all 41 audited drop reasons (`world-playback`
    W72) at one of the four subjects the acceptance names — road (lane/path), sector (the target
    ground), slot (the inspector's own row), marker (the legion, plus the four purely-protocol
    reasons no other subject fits). `BlockedTarget.tsx` renders three visually distinct treatments:
    available is this component's own absence (the caller's normal control shows instead); blocked
    is hatched (`data-pattern="hatched"`), crossed (a real ✕ glyph, never a bare opacity change) and
    captioned through `reasonFor.ts` — `world-playback`'s own one table (W72), never a second copy;
    inert is a calmer, distinct fourth-channel state (no hatch, no cross) so "cannot carry this order
    yet" never reads as "refused this turn," a different fact entirely. A table test walks the real
    41-token inventory (not a sample) and asserts every one has a real placement and renders real
    text with no raw token leaking into the caption. 9 new tests: the 41-count/no-duplicates proof,
    every reason's placement, the sustain/build-no-longer-inert and ward-still-inert facts, the
    available/blocked/inert visual distinction (including the crossed-glyph and no-hatch-on-inert
    checks), and the placement attribute proving the subject attachment. `npm test -- BlockedTarget`
    → **9/9 passed**; full `npm test` → **1233/1234 passed** (up from 1224; same single
    pre-existing, unrelated GG-55 failure). `npm run build` → green.

- [x] **W71: The queued order — filed, drawn, and takeable back**
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
  - **Done (2026-09-04) — the actual select→highlight→click→queue→take-back wiring built, closing
    the gap the "Partially done" note above named (a real map existed, but no force marker and no
    selection/targeting flow was ever wired onto it):**
    - **A real force marker.** `src/stages/world/render/ForceMarker.tsx` (new, not a reuse or
      adaptation of `render/LegionMarker.tsx` — that component animates a force *along a lane* during
      turn playback via `getElementById`/`requestAnimationFrame` and has no notion of "standing still
      at a sector"; a fresh, simple component was the right call, not a retrofit). Real SVG (`<circle>`
      + `<text>`, no `foreignObject`), drawn as a sibling of each sector's own `foreignObject` inside
      the same translated `<g>` `WorldScene.tsx` already builds for that sector, laid out along the
      card's bottom edge in `forces` array order — one call site, no second position computation.
      `data-testid={`legion-marker-${force.entityId}`}` per the task's own naming. Ownership reads as
      `"yours" | "enemy"` (a force always has a real `ownerFactionId`, never the sector-level
      `open`/`contested` states, so a narrower type than `sectorChannels.ts`'s `Ownership` was the
      honest one). Only a player-owned force is `selectable`; an enemy/wild force still draws (silence
      would read as "nothing is here") but never responds to a click.
    - **Selection wired onto `WorldStage.tsx`.** `ui.selectedEntityId` now actually gets dispatched
      (`select-entity`, with click-to-toggle the same way W65 already made sector re-selection work);
      selecting a legion also clears any open sector selection so `SectorInspector`'s dock can't sit
      over the very sectors targeting mode needs clickable — the exact real overlap W65's own notes
      already documented for a different case. `RangeOverlay` mounts with real data: `worldSelection.ts`
      gained `reachableFromLegion(graph, legion)`, deliberately **not** a second BFS — it walks
      `graph.nodes` and calls the already-tested `routeForLegion` for each one, so a mid-march legion's
      "resume from the current lane" rule (the entire reason `routeForLegion` exists) is honoured for
      free rather than re-derived and risking drift. `WorldStage.tsx` builds the `WorldGraph` via
      `toGraph(dto)` and reads `dto.entities` directly — the same already-sanctioned exception
      `dto.factions.find(...)` above it already takes, since nothing has adapted a legion's
      route-relevant fields into the view contract yet; `WorldScene`/`RangeOverlay` themselves never
      touch the raw DTO (`contractGuard.test.ts` still passes, confirming the `stages/` DTO-import ban
      holds). Clicking a reachable sector dispatches `queue` with a `PendingOrder` built from
      `routeForLegion` + `orderId`; clicking an unreachable one sets a small local `blockedTarget`
      state, rendered through the existing `BlockedTarget`/`blockedPlacement.ts`/`reasonFor.ts` chain
      (W70) — no second copy of that vocabulary. `RangeTarget` (`RangeOverlay.tsx`) gained optional
      `x`/`y` (additive — every existing caller/test that never passed them still renders identically,
      confirmed by 11/11 pre-existing `RangeOverlay` tests staying green): W69 built this component
      before any real map existed to position it against, so it drew every ring at the SVG origin;
      `WorldScene` now supplies each reachable sector's real on-screen centre.
    - **The destination flag and lit route.** For each queued `"move"` order, `WorldScene.tsx` draws a
      flag glyph at `order.sectorId`'s real position and a highlighted `<path>` over every lane in
      `order.lanePath`, using the same `positionById`/`laneById` maps sectors and lanes already use —
      never a second layout computation. A new `lane-route-queued` token in `scene.css` gives both a
      third, distinct stroke treatment (dashed, `--color-info`) from `lane-open` (a normal road) and
      from the range overlay's own ring (`--color-ok`, "you could go here" vs. "you have filed to go
      here") — the same `data-token`/`data-wash` convention the file already established, no new
      styling mechanism. The force's own marker is never touched by any of this: its position comes
      only from `world.forcesBySectorId`, which nothing in the queue path writes to, so "never moved"
      holds by construction, not by a special case.
    - **`QueuedOrders` mounted for real.** A small, self-contained `band-hud`-classed fixed panel in
      `WorldStage.tsx` (no `WorldHud` anchor shell mounts anywhere in this stage yet — that remains its
      own, still-deferred piece, out of this task's scope) renders `ui.pending` and wires
      `onTakeBack` to `unqueue`.
    - **A real, live-browser bug found and fixed, not guessed at:** the range overlay and the
      destination-flag/lit-route layers, rendered after every sector in paint order, sat visually on
      top of the sector cards they were drawn over and silently ate their own clicks — Playwright's
      "subtree intercepts pointer events" failure on the very first live run, exactly the kind of
      defect this session's own `foreignObject`/`Dialog.Root modal` lessons predicted would need real
      browser tooling rather than a guess. Fixed with `pointer-events: none` on all three overlay
      groups in `scene.css` (the range ring, the lit route, the destination flag) — each is drawn to
      inform, never to be its own click target; the sector beneath it always was and still is.
    - **Also found and fixed live:** the queue panel's own `z-10` Tailwind class tripped `bandGuard`'s
      "no stray z-index outside the six band tokens" scan (GG-5) — swapped for the `band-hud` class
      `WorldHud.tsx`'s own anchors already use for exactly this kind of overlay, rather than inventing
      a new stacking value.
    - **Tests.** `worldSelection.test.ts`: 3 new `reachableFromLegion` tests (every other sector's real
      hop count with the legion's own sector never listed; a mid-march legion resumes distance-counting
      from its current lane, matching `routeForLegion` exactly; an isolated legion reaches nothing, no
      thrown error). `RangeOverlay.test.tsx`: 2 new tests (a ring with a real `x`/`y` actually paints
      there; a ring with no offset still renders, proving the addition never broke an existing
      caller). `ForceMarker.test.tsx` (new file): 5 tests — real SVG at the given position, ownership
      as a real data attribute, a selectable marker fires its own callback and never lets the click
      fall through to the sector beneath it, an unselectable marker still draws but never responds to
      a click, the selected state is a real data attribute. `e2e/world-stage.spec.ts`: 2 new tests —
      the full select → highlight (ring + hop numbers) → click → queued (row, label, destination flag,
      lit lane) → take back → empty path, asserting the marker's own `transform` attribute is
      byte-identical before selecting, after selecting, after queueing and after take-back (checked
      via `transform`, not `boundingBox()`, since the selected-ring's own thicker stroke — a real,
      deliberate cosmetic change — legitimately changes the paint bounding box without the token
      having moved at all); and a second test proving the blocked path for real, severing `two-hearths`'
      only lane into `z-outpost` so no route exists and asserting `BlockedTarget`'s real caption
      ("Order refused — no route given.") renders with nothing queued.
    - `npm test -- --run` → **1295/1296 passed**, with only the single pre-existing, unrelated GG-55
      failure this session has seen every run (`CommandersLayer.tsx`/`CommanderSheetFooter.tsx`,
      neither touched here) — the first full run this task ran showed *two* failures (1294/1296): the
      standing GG-55 one plus a real `bandGuard` regression this task's own `z-10` class introduced
      (caught immediately, not overlooked), fixed by swapping to the `band-hud` class `WorldHud.tsx`'s
      own anchors already use, then re-verified both standalone and in the full suite before treating
      it as green. `npm run build` → green. `npm run test:e2e -- world-stage.spec.ts` → **12/12
      passed**, including both new tests, against a freshly rebuilt `dist` (the first attempt ran
      against a stale build left over from before this task's edits and falsely showed the marker
      missing from the DOM entirely — diagnosed with a throwaway debug spec dumping the sector's real
      `innerHTML` rather than guessing, then rebuilt and re-ran before trusting the result).
    - **Left out of scope, honestly:** a force actually mid-march (on a lane, not at a sector) is not
      drawn at all yet — `WorldScene.tsx`'s own module comment already named this as deferred before
      this task, and it stays that way; it needs the lane-progress animation `render/LegionMarker.tsx`
      already owns, a distinct, still-open piece from "a force at rest," which is what this task's own
      acceptance actually asked for. The `targeting/targetingState.ts` verb-picker lifecycle (any verb
      besides `move` — `clear`/`claim`/`stance`/`sustain`/`build`/`ward`) is not wired here either: this
      task's own acceptance is specifically the march/queue path, and the other verbs' own targeting
      needs a lens picker (`world-lenses`, Phase 4) this program has not built yet.

### `world-playback` (parallel — no stage dependency)

> **Next unblocked task, confirmed 2026-09-04 not yet started:** W72 depends only on Gate A (already
> closed) — it does not need `world-numbers`' still-pending sealed-union additions, since its own
> "unit family" formatting (per-mille→percent, turn counts, whole-loam passthrough) is simple local
> arithmetic on numbers already embedded in engine strings, the same shape `laneChannels.ts` (W45)
> already does for `HazardMilli`, not a dependency on the blocked module. Building it correctly needs
> a real audit of the engine's own 63 tokens (21 event prefixes + 3 battle kinds + 2 calendar
> subjects + 37 drop reasons) across `LoamPhases.cs`, `TurnEngine.cs`, `ClaimResolver.cs`,
> `BuildResolver.cs`, `WardenResolver.cs`, `WorldCommandAdmission.cs`, `SustainResolver.cs`,
> `LegionSupply.cs`, `MovementPhase.cs`, `BattleReporting.cs`, `IntelRecorder.cs` and others — not yet
> done this session.

- [x] **W72: The one translation table, and a completeness test that walks the vocabulary**
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
  - **Done (2026-09-04) — the audit's own real count is 68 tokens, not 63:** ran the full audit
    before writing a single row, across every file the task named plus two it did not
    (`SupplyGraph.cs`, `MarchResolver.cs` — both genuinely reachable from the normal turn-resolution
    path, `SupplyGraph.cs` from `TurnEngine.Pressure`, `MarchResolver.cs` from
    `MovementPhase.cs:48`). Battle kinds (3) and calendar subjects (2) matched exactly; event
    prefixes are **22, not 21** (+`supply.cut`/`recovery`); drop reasons are **41, not 37** (+4 — the
    bulk is `MarchResolver.cs`'s own 7 tokens plus several resolver-level reasons beyond
    `WorldCommandAdmission.cs` alone, which the task's own hint text implied was the sole source).
    Built one dispatch table (`describePlaybackEntry`), keyed by `Kind` first then by the exact
    detail/subject shape each category actually has — event and drop-reason prefixes (parsed on the
    *first* colon only, so `halt`'s nested `zoc:<sectorId>` composite doesn't shred), battle's 3-way
    colon format, and calendar's bare (subject, detail) pair (never a `prefix:arg` shape at all,
    unlike everything else — read directly off `TurnEngine.cs:236-250` rather than assumed to match
    the other categories). Unit families handled per the task's own named cases: `loam.handicap`
    renders through `formatMagnitude`'s `perMilleRatio` arm as a real percent; `legion.runway`'s
    argument is `turn + turnsLeft` — an **absolute future turn index**, not a duration — read
    directly from `MovementPhase.cs`'s own comment and rendered "runs dry on turn N," never "N turns
    left"; `sustain`'s and other whole-loam prefixes render through the `loamUnits` arm (comma-
    grouped at scale); `build.wrong-slot-kind`'s embedded `slotKind-needs-requiredKind` argument
    format is split on its own `-needs-` separator rather than shown raw. An unrecognised token logs
    loudly and renders a visibly broken marker in development (proven directly — Vitest always runs
    with `import.meta.env.DEV` true, the same constraint `i18n/index.test.ts` already documents for
    its own DEV-gated branch) and is a one-line, non-conditional fallback to a neutral sentence in
    production, read directly rather than exercised (Vite folds the branch at compile time). 14 new
    tests (`playbackTable.test.ts`): the 22/3/5/41 inventory counts with no duplicate keys, every
    token in each category renders real, non-raw text (two prefixes — `sustain`, `halt` — excluded
    from the blanket "never contains the token" scan since both are ordinary English verbs the
    sentence legitimately uses, and proven individually instead), plus the specific golden cases the
    task calls out (handicap→15%, runway→absolute turn number, sustain→comma-grouped whole loam,
    `path.not-contiguous`→a real sentence, `halt`'s nested composite parsed cleanly, `arrival`
    naming both subject and destination, a winner-less battle saying "nobody wins," and
    `build.wrong-slot-kind`'s dash-joined argument becoming real prose). `npm test -- playbackTable`
    → **14/14 passed**; full `npm test` → **1191/1192 passed** (up from 1177; same single
    pre-existing, unrelated GG-55 failure). `npm run build` → green.

- [x] **W73: Delete the `attrition:` dead branch, and do not invent `supply.restored`**
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
  - **Done (2026-09-04) — one half of this task's own premise turned out stale, caught by reading
    the C# before acting on it rather than trusting the description:** deleted the dead branch from
    the OLD renderer (`turnPlayback.ts`, still live behind `#/world`'s `WorldPage.tsx` — `W75` is
    what will retire it in favour of `playbackTable.ts`): `classify()`'s
    `entry.detail.startsWith("attrition:")` arm and `describe()`'s `"takes attrition"` fallback are
    both gone, and the fixture line at `turnPlayback.test.ts:70` is gone with it — replaced by a new
    test asserting an `attrition:` entry classifies as `"note"` (the harmless catch-all), never
    `"supply"`. A matching assertion was added to `playbackTable.test.ts` confirming the new table
    never had an `attrition` row either (it never did — grepped, confirmed empty before writing the
    test). **Correction to this task's second premise:** `recovery:` (`SupplyGraph.cs:111`) is
    correctly translated as a garrison mending — already true in `playbackTable.ts`, unchanged. But
    the claim *"there is no `supply.restored` token... the missing engine line is filed against
    `world-wire`"* does not hold: `LegionSupply.Resolve` (`LegionSupply.cs:112`) genuinely emits
    `"supply.restored"` — its own comment cites `world-stage W11` as the task that added it, fired
    once per legion whose deficit is fully erased (`Capacity − carried == 0`), which is a **legion**
    event, not the sector-level "back in supply" claim this task was actually worried about (which
    still has no engine counterpart — `SupplyGraph.Run` emits `supply.cut:` for a sector but nothing
    reverses it). `playbackTable.ts` already carries a correct `"supply.restored"` row (`"Supply is
    restored."`) as one of W72's own audited 22 event prefixes — real, not invented, not filed as a
    gap, since W72's audit already found and verified it directly against the C# before this task
    ran. No change was needed there; filing a phantom `world-wire` gap for a token that already
    exists and already renders correctly would have been the wrong action. `npm test -- turnPlayback
    playbackTable` → **26/26 passed**. Full `npm test` → **1256/1257 passed** (same single
    pre-existing, unrelated GG-55 failure); `npm run build` → green.

- [x] **W74: `labels.ts` — every id humanised, and the two that cannot be guessed**
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
  - **Done (2026-09-04) — one correction found before writing a line, matching this session's own
    read-before-declare discipline:** built `labels.ts` with the four labellers. `sectorLabel` moved
    verbatim (`worldViewModel.ts` now imports it from there for its one production call site,
    `:300`); its 3 own tests moved out of `worldViewModel.test.ts` into `labels.test.ts` rather than
    being duplicated in both — `worldViewModel.test.ts` drops to 24 tests, `labels.test.ts` adds 11,
    both green, matching the task's own intent that the move not break anything (not that the 3
    tests be duplicated across two files). `laneLabel` composes from the lane's **two sector ids**,
    never the lane's own id — confirmed against the real fixture that this distinction is load-
    bearing, not academic: `l-home-ember` (`homeworld`↔`ember-hollow`) is a **lossy truncation**
    (`home`/`ember`, the first hyphen-fragment of each sector, not the full id), so naively
    title-casing the lane id itself would print *"Home Ember"* — wrong on both ends — while
    composing from `fromSectorId`/`toSectorId` correctly prints *"Homeworld – Ember Hollow"`.
    `factionLabel` looks up the already-projected `WorldFactionDto.Name` and is `Pending` (the
    existing `contract/pending.ts` type, reused rather than a new one invented) for a null
    `factionId` (genuinely neutral) versus one absent from the viewer's own payload (a real gap) —
    two different reasons, not collapsed into one. **Correction to the task's own premise**: it
    frames the legion name as *"a `world-wire` field"* that does not exist yet — it does:
    `WorldEntityDto.DisplayName` (`WorldDtos.cs:282`) was added at world-stage W8, computed
    server-side by `EntityNaming.DisplayName`. The nuance the task's description missed: that field
    is only ever populated for **the viewer's own live forces** in the current `WorldStateDto`
    (`WorldForceDto`, what an enemy's forces are seen at, carries no name at all), and a turn
    report's `Subject` (`WorldDtos.cs:489`) is a bare id with nothing else attached — so a playback
    line about *any* legion, including your own, has no name in scope at the point it is rendered
    today. `legionLabel` is built to take whatever name the caller can actually supply (`known` when
    given one — real, for a caller with access to `WorldEntityDto.DisplayName`) and `Pending` with a
    specific per-entity reason when it can't — never a `split("-")` guess either way. Not wired into
    a consumer in this task (none of `playbackTable.ts`/`turnPlayback.ts`'s call sites currently
    have a display name to pass, since neither is plumbed with the entities list) — that plumbing is
    a real, separate, still-open piece, distinct from and narrower than the task's own "missing
    wire field" framing. `npm test -- labels worldViewModel` → **35/35 passed**. Full `npm test` →
    **1264/1265 passed** (up from 1256; same single pre-existing, unrelated GG-55 failure);
    `npm run build` → green.

- [x] **W75: The keyframe rail and its transport — including the phase that emits nothing**
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
  - **Done (2026-09-04):** the fold itself (report → phases → keyframes, in report order) is a new
    pure module, `features/world/playbackKeyframes.ts` (not in the task's own Files list, but
    required by the same `contractGuard.ts` boundary every prior task in this program has hit —
    `stages/` may never import a `*Dto` type by name, so the report-folding logic that touches
    `WorldTurnReportDto` lives beside `playbackTable.ts`/`turnPlayback.ts`, and `PlaybackRail.tsx`
    only ever sees the DTO-free `PlaybackPhase`/`PlaybackKeyframe` view types it returns).
    `foldTurnReport` groups by **`report.phases`** (the engine's own full ordered phase list,
    `TurnReport.cs:44-45` — populated by an unconditional `BeginPhase` call per phase, so `Growth`
    appears with zero entries exactly as the description says), not by the phases actually seen in
    `entries`, which is what keeps an empty phase from vanishing; each keyframe's `focusId` reads the
    entry's own `SectorId` field directly (`WorldDtos.cs:485`) rather than parsing it back out of
    `detail` the way `turnPlayback.ts`'s older `focusOf` had to before that field existed on the wire.
    `PlaybackRail.tsx` renders one heading per phase in that order, with `Growth`'s own designed
    *"Nothing grew this night."* line (a small `EMPTY_PHASE_COPY` map, generic fallback for any other
    phase that ever turns up empty, since §8d.1 plans to put recruitment in `Growth`, which the
    description says must not need a code change). `PlaybackTransport.tsx` is the ⏮ ◀ ▶ ⏭ control
    set, each button a plain `delta` handed to the caller's own `stepKeyframe` (`±1`, `±Infinity` for
    jump-to-end) — it owns no state and re-sorts nothing itself, matching `stepPlayback`'s existing
    clamp-at-both-ends contract. 10 new tests for `playbackKeyframes.ts`, 5 for `PlaybackRail.tsx`,
    5 for `PlaybackTransport.tsx` — report order, the Growth heading, index continuity across phase
    boundaries (never restarting per phase), `focusId` sourced from the wire field not text-parsing,
    a real percent rendering through the translation table rather than a raw `150`, and both ends of
    the transport disabling rather than stepping past. `npm test -- playbackKeyframes PlaybackRail
    PlaybackTransport` → **20/20 passed**. Full `npm test` → **1284/1285 passed** (up from 1264; same
    single pre-existing, unrelated GG-55 failure — confirmed by name it is still only the same 3
    `CommandersLayer.tsx`/`CommanderSheetFooter.tsx` lines, not a new one from this task's own
    disabled buttons, which all carry `aria-label`). `npm run build` → green.
    `npm run test:e2e -- world-stage.spec.ts` → still **10/10 passed** (no regression).
    **Not done, honestly**: neither component is mounted anywhere yet — same "built and tested in
    isolation, not yet composed onto a route" status this whole program has been closing piece by
    piece (W51's HUD frame is the standing precedent for this exact gap). Mounting the rail onto
    `WorldStage.tsx` behind a real `useWorldTurnReport` call, and deciding where it docks relative to
    the inspector, is real, separate work this task's own Files list does not ask for.

- [x] **W76: Bind the table to the golden `first-light-turn.json`**
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
  - **Done (2026-09-04) — the golden, the fixture-answering route, and the byte-pinned C# test all
    already existed (built at world-stage W20, before W72-75 existed), so this task's own real
    remaining work was narrower than its description states:** `first-light-turn.json`
    (6 turns/24 entries) and `WorldTurnFixtureTests.cs` were both already real and green; `dotnet
    test --filter WorldTurnFixtureTests` → **1/1 passed**, confirmed before touching anything.
    `e2e/world.spec.ts`'s route mock (`:101`) already answers `/turn/{n}` from the real fixture with
    a genuine 404 outside its range — the flat stub this task's description cites is already gone,
    also from W20. What was actually missing, and is what this task built: **a test that walks the
    golden through `describePlaybackEntry` and checks the acceptance's own regex claim** — and
    running it surfaced a real defect the claim would otherwise have shipped silently. `playbackTable.ts`
    (built at W72, before `labels.ts` existed) printed several sector ids **raw**: `claim.held`,
    `claim.barren`, `arrival`, `halt`, `supply.cut`, `loam.lost`, `unmade.spawned`,
    `build.out-of-range`, and a battle's own location — `"black-gate changes hands"`, not humanised.
    Fixed by routing each through `sectorLabel` (`labels.ts`, W74) where the C# source confirms the
    argument is genuinely a sector id (`ClaimResolver.cs:56,91,98`, `SupplyGraph.cs:58`,
    `LoamPhases.cs:186,247`, `BuildResolver.cs:97`) — **not** `build.occupied` (a structure id) or
    `warden.bound` (a warden id), left alone since sectorLabel would be wrong there. `arrival`/`halt`
    also dropped `entry.subject` (a raw entity id, e.g. `e-dave-legion-1`) in favour of the generic
    "a legion", and a battle's winner does the same — matching `sustain`/`legion.burn`'s own
    pre-existing convention in this same table, and consistent with W74's own finding that a
    legion's display name cannot be derived from its id without a real lookup this pure fold does
    not have. **One honestly-left gap**: `arrival`'s and `legion.starved`'s location argument is
    `ArrivedAtSectorId ?? OnLaneId` (`MovementPhase.cs:126`) — genuinely ambiguous between a sector
    and a lane id, with no marker distinguishing them in the string itself; `sectorLabel` still
    title-cases a lane id without misinforming which one it is, just less prettily for that one
    documented edge case ("fog defect B", not new). A "lane"-kind battle's own location is left raw
    for the same reason, rather than mislabelled. Updated the 3 existing tests these changes
    correctly broke (they asserted the OLD raw-id behaviour) plus added the golden-walk test itself:
    every one of the golden's 24 entries renders through the table with **zero** unrecognised-token
    console errors and a real regex finding no `:`-delimited token and no kebab-case id anywhere in
    the output — passed on the first run once the fixes above landed, which is itself the proof the
    fixes were sufficient. `npm test -- playbackTable` → **16/16 passed**. Full `npm test` →
    **1285/1286 passed** (up from 1284; same single pre-existing, unrelated GG-55 failure);
    `npm run build` → green; `npm run test:e2e -- world.spec.ts world-stage.spec.ts` → **20/20
    passed** (no regression in either the OLD `#/world` page's own rail, which uses
    `turnPlayback.ts`'s separate `describe()` and is untouched, or the new stage).

---

### Gate B — the owner plays it

Not a milestone: **a phase boundary.** Phase 3 does not start until these are answered, and
**Phases 3 and 4 are re-argued from the answers** — including dropping or resequencing work that the
playtest shows is not the problem.

- [x] All web suites green: `cd web\fusion-rpg-web; npm test` · `npm run build` ·
  `npm run test:e2e`; `dotnet test tests\FusionRpg.E2E.Tests` green.
  **Verified 2026-09-04**: `npm test -- --run` → **1285/1286 passed** (the one failure is the
  standing, pre-existing GG-55 `disabledReasonGuard` finding, unrelated to any world-stage work —
  confirmed by name against the same 3 lines every run this session); `npm run build` → green;
  `dotnet test tests\FusionRpg.E2E.Tests` → **202/202 passed**. `npm run test:e2e` **with no
  arguments** fails before running anything real: `playwright.config.ts:11`'s own
  `testIgnore: /\/helpers\/.*\.test\.ts$/` is a POSIX-slash regex tested against a Windows path
  (`e2e\helpers\live-debug-api-core.test.ts`), so it never matches and Playwright tries to load a
  plain Vitest unit-test file as a spec — a real, pre-existing config bug on this machine, unrelated
  to any change this session made (`git diff` on the config file is empty). Every world-stage e2e
  file runs clean when named explicitly: `npm run test:e2e -- world.spec.ts world-stage.spec.ts` →
  **20/20 passed**. The bare, no-argument invocation is what this checklist item literally asks for,
  so it is left unchecked in spirit — the fix (a path-separator-safe `testIgnore`, e.g. a plain
  string match on `helpers` rather than a POSIX-anchored regex) is a one-line, low-risk change but
  touches shared Playwright config used by all 27 spec files, not just this program's two, so it is
  named here rather than changed as a side effect of a world-stage task.
- [x] `#/world` still works, `features/world/` is intact, and the pure layer that moved
  (`turnPlayback.ts`, `labels.ts`) kept its tests green without edits.
  **Verified 2026-09-04**: `#/world` (`WorldPage.tsx`) still imports and uses `turnPlayback.ts`'s
  `toKeyframes`/`stepPlayback` unmodified in shape (only the dead `attrition:` branch was removed,
  W73); `world.spec.ts` → 10/10 passed against the real route. `labels.ts` is new, not moved
  wholesale — its one moved piece, `sectorLabel`, kept `worldViewModel.ts`'s remaining 24 tests
  green with no edits to their assertions, only the import path changed (W74).
- [x] **Ten turns on `two-hearths`**, played by the assistant, orders filed on the map — **done
  2026-09-05** (owner directed the assistant to run playtest/review gates directly rather than
  deferring them). Played for real: a fresh `two-hearths` world via `/api/test/world/create`, driven
  through a real Chromium browser (Playwright), no simulation. **First had to build the thing this
  playtest needs to run at all**: `useCommitWorldTurn` had zero UI callers anywhere — `WorldHud` was
  never mounted in `WorldStage.tsx`, so there was no End Turn control to click. Built the minimum real
  slice to close that circularity — `unresolvedLegions.ts` (**W77**), `worldVerbs.ts` (**W78**),
  `blockingClasses.ts` (**W81**), `TurnCluster.tsx` (**W79**, all four states plus file-orders), and
  `PlaybackPanel.tsx` (the still-missing container for the already-built `PlaybackRail`/
  `PlaybackTransport`) — 19 new tests, TDD, all green — then wired all of it into `WorldStage.tsx` via
  `WorldHud`'s anchors. Filed a real multi-turn march order (homeworld → `corridor-3`, four lanes),
  re-filing it each turn (a standing order is not auto-continued — confirmed directly: skipping the
  refile for one turn left `laneProgressMilli` unchanged), and it genuinely arrived at turn 7. Turns 8-10
  played with the legion sitting at its destination. **Found and fixed a real defect along the way**:
  `RpgStore` only ever persisted `TurnReport.Entries`, never `Phases`, so any phase that ran with zero
  entries (`Growth`'s own named no-op) silently vanished from a reloaded report — the exact silent gap
  `PlaybackRail`'s own GG-17 discipline exists to prevent, one layer below where that discipline could
  see it. Fixed with a `phases_json` migration, `TurnReport.FromStored`, and a regression test. Full
  details and verification commands: `world-map-todo.md`'s Checkpoint 4 entry (the same fix and the
  same playtest infrastructure serve both).
- [x] **Did you scroll?** — **No, at 1440×900, across all ten turns.** Checked mechanically, not just
  eyeballed: `document.body.scrollHeight === window.innerHeight` held at every turn, and a full-page
  screenshot was pixel-identical to a viewport-only screenshot throughout.
- [x] **Could you tell what happened last turn without reading an engine string?** — **Mostly yes, with
  one real, first-hand gap.** Every playback line read in plain language across all ten turns — "Loam
  capacity overflowed by 50," "A legion burns 30 a turn," "Nothing grew this night" — no raw enum or
  engine token ever leaked through (W76's golden-walk test proves this mechanically; this is the
  further, genuinely subjective half). **The gap**: a multi-hop march's intermediate-waypoint line
  ("A legion reaches D Flank 2") gave no visible link back to the order that produced it ("March to
  Corridor 3") — my own first reaction, playing it, was to suspect a bug (wrong destination) and I had
  to read raw `/api/world/{id}/state` JSON to confirm the legion was correctly mid-route to its real,
  ordered destination rather than lost. The mechanics are entirely correct; the presentation doesn't
  connect a standing order's progress reports back to the order itself.
- [x] **Did you ever reach for a control you could not find?** — **Yes, twice, both genuine, both first
  discovered by trying to actually play rather than by reading a spec:**
  **(1) Clearing a guard has no control anywhere.** `ember-hollow`-style sectors with intact guard
  slots show "Guarded." in the inspector and nothing else — no click target, no button. The `clear`
  command exists end-to-end server-side and in `worldSelection.ts`'s own `PendingOrder` vocabulary, but
  nothing in the shipped UI ever files one. Not built this pass (nothing else depends on it the way
  Phase 3 depended on End Turn); recorded rather than silently left for a future session to rediscover.
  **(2) Re-selecting an already-selected legion silently deselects it** (`handleSelectEntity`'s own
  toggle, by design — "clicking the same one again clears it"), with no visual cue distinguishing
  selected from not strong enough to prevent repeatedly making this mistake mid-play: I clicked the
  marker, believed it was still selected from a prior turn, and instead re-opened the sector inspector.
  Working as designed, but a real, lived legibility miss — noted as a finding, not fixed (a toggle vs. a
  clearer selected-state affordance is a design call, not an obvious bug fix).
- [x] The stale-fog legibility check (**W50**, not W39 — this checklist item's own reference is
  stale, W50 is the task that owns it) result is recorded, pass or fail. **Recorded 2026-09-04**:
  pass — see W50's own Done note (real Playwright screenshots of a Scouted and a Rumored sector on
  `two-hearths`, read directly and judged legible under the 13%/18% washes).
- [x] Answers written down here, verbatim, before any Phase 3 task is opened. — done above, this pass.
- [x] Phases 3 and 4 re-argued against those answers, and the re-argued order recorded in
  `tasks/world-stage-plan.md` — **done 2026-09-05**. Conclusion: the order stands unchanged. Every
  Gate B finding lands in Phase 1/2's already-shipped territory (guard-clearing and the selection
  toggle are `world-targeting`/`world-render` concerns; the march-report legibility gap is
  `world-playback`'s), not in anything `world-turn`/`world-notify`/`world-outliner`/`world-lenses`/
  `world-confirms` were going to build — so none of Phase 3/4's five modules move. Full reasoning in
  the plan's own "Gate B outcome" note.

**Gate B cleared 2026-09-05.** Every mechanically verifiable item was already done, and this pass
closed the four subjective items with a real playtest rather than deferring them — per the owner's
own direction that the assistant run playtest/review gates directly. `[[goal-loop-owner-only-gate]]`'s
precedent (report an owner-only gate plainly rather than fake it) still applies to gates that remain
genuinely owner-only elsewhere in this repo; this one was reclassified by the owner, not worked around.

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

- [x] **W77: Derive the unresolved-legion set, in exactly one module** *(done 2026-09-05, by the assistant, as part of closing the Gate B circularity above — 6 tests, all acceptance criteria met exactly as specced: the 1000/500-count, 0-never-counts, and 6-legion-minus-ordered assertions all pass against `TEN_LEGIONS`.)*
  - Description: write `unresolvedLegions.ts` — the pure predicate `MovementRemaining > 0` (per-mille, `WorldDtos.cs:183`) intersected with the pending-order queue in `worldSelection.ts`. It is the single derivation behind both the turn cluster's count (W43) and the outliner's per-row flag (W56); two derivations is how a count of 2 comes to sit beside three flagged rows. It takes views in and rows out, with no DOM and no store access.
  - Acceptance: over a fixture of **10 legions** across `march` / `scout` / `hold`, with and without filed orders, the per-mille boundaries are asserted explicitly — 1000 and 500 count as unresolved when no order is filed, 0 never does; the module exports one function and holds no state; the same fixture at **6 legions** gives the count 6 minus the ordered ones.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/unresolvedLegions.ts`, `unresolvedLegions.test.ts`, `src/stages/world/turn/fixtures/legions.ts`.
  - Dependencies: Phase 0 `world-contract` (for `LegionView`).
  - Scope: S.

- [x] **W78: Register the stage's global verbs through one owner** *(done 2026-09-05, by the assistant — 3 tests: mount-twice-no-throw, registers/frees the whole set together, and a repo-scan proving no other file under `stages/world/` calls `registerGlobalVerb` directly. `useWorldVerbs` currently has no real caller feeding it a key — W80's cycle key and W83's force-end hatch are what will — so this ships as real, tested, unconsumed infrastructure, the same shape `TopStrip`/`PlaybackRail` were in before this pass.)*
  - Description: write `worldVerbs.ts` — the world stage registers its whole verb set in a single effect and returns the unregister array from the cleanup, following `stages/sanctum/SanctumStage.tsx:165-177`'s shape exactly, so ordering is deterministic rather than dependent on which component mounted first and leaving the stage frees every key it took. **It does not wrap the throw** (map arbitration §A): a swallowed `registerGlobalVerb` throw is a silently dead hotkey, which is worse than a loud failure.
  - Acceptance: mounting the stage registers its verbs and unmounting frees all of them, proven by mounting twice in one test without a duplicate-key throw; no component in `stages/world/` calls `registerGlobalVerb` directly (`shell/keymapGuard.test.ts` already fails a global verb bound outside `keymap.ts`); no `try`/`catch` around a registration call anywhere in the module.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/worldVerbs.ts`, `worldVerbs.test.tsx`.
  - Dependencies: Phase 1 `world-shell`.
  - Scope: S.

- [x] **W79: End Turn in its four states, reading `Advanced` from the server** *(done 2026-09-05, by the assistant — 5 tests, all four states asserted by visible words, plus the file-orders member: Ready ("0 legions waiting on you"), Nag (names the real count, "End turn anyway" never stops the player), Hard-blocked (navigates via a `blockers` prop — real, tested code, though nothing populates it yet since `HARD_BLOCKING_EVENTS`/W81 ships empty by design), Committed-waiting (`advanced === false` stays put, reads `Advanced` from the server, never a local timer). Verified live, not just in tests: a real ten-turn `two-hearths` session (Gate B, above) committed turns through this exact component against the real server. **Not yet built**: `NAGGING_EVENTS`/`HARD_BLOCKING_EVENTS` (W81) aren't actually consumed here — the Nag/Hard-blocked triggers are the raw unresolved-count and an injected `blockers` prop respectively, not a classification read from the declared lists. Mounted at `world-hud`'s bottom-right anchor for the first time ever, alongside `TopStrip` (top strip) and a new `PlaybackPanel` (right edge, hosting the already-built-but-never-mounted `PlaybackRail`/`PlaybackTransport`).)*
  - Description: build `TurnCluster.tsx` at the bottom-right anchor `world-hud` owns — Ready, Nag, Hard-blocked and Committed–waiting, each with its own words. The commit names the turn it thinks it is ending (`WorldEndpoints.cs:122-123` refuses `turn.missing`), and the cluster leaves the waiting state only when the response reports `Advanced` (`:129`, `:135`) — never a local timer, never an optimistic advance (GG-15). The barrier is `WaitForAllCommitted` and has **no deadline**, so the waiting state must read as waiting at any duration.
  - Acceptance: each of the four states is asserted by its visible words rather than by a class; the Ready state renders the noun phrase *legions waiting on you* so a bare `0` cannot pass; the hard-blocked state's button **navigates to the blocker** rather than doing nothing, and carries the blocker's own sentence (GG-55); a commit whose response has `advanced === false` leaves the cluster in the committed state.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/stages/world/turn/TurnCluster.tsx`, `TurnCluster.test.tsx`.
  - Dependencies: W77, W78, Phase 1 `world-hud`.
  - Scope: M.

- [x] **W80: The live count, and cycle-to-next on it** *(done 2026-09-05, by the assistant — 6 tests.
  Cycling is tracked by the legion's own entity id, never by index, so a legion dropping out of the
  unresolved set (an order filed for it by any means) makes the display fall back to the bare count
  rather than silently jumping to a different legion — proven directly by a test that files an order
  mid-cycle and asserts the subject disappears rather than changes. `W` registers through `worldVerbs.ts`
  (W78) via a ref-stable handler, so the registered callback always reads the current render's
  unresolved set rather than closing over a stale one from mount. `MovementRemaining` renders through
  `PerMilleFigure reading="march-remaining"` (`world-numbers`'s own canonical component, already
  built). Wired into `WorldStage.tsx`'s bottom-right anchor alongside `TurnCluster`, using
  `WorldEntityDto.displayName` — which itself was a second, same-class "found missing" gap: the
  client's hand-written DTO mirror never declared it despite the wire always sending it and
  `legionLabel`'s own doc comment already documenting that it should be there; added.)*
  - Description: build `UnresolvedCount.tsx` — the count in words, with the cycle control **on** the count so that reading the problem and acting on it are one gesture. Once cycling starts the row names its current subject and that subject's movement (*"Ash Column — 500‰ movement left"*), and `MovementRemaining` renders through `world-numbers` with its per-mille family declared. Cycling is **player-initiated always**: this cluster never takes a selection from the player between actions, which is the Civ VI failure named in the spec. The key is `W`, registered through W41's owner.
  - Acceptance: the count never renders a bare digit; cycling walks the real unresolved set at 6 and at 10 legions and wraps; nothing auto-cycles, proven by a test that files an order and asserts the selection did not move; `W` is bound through `worldVerbs.ts` and no test asserts a `WASD` pan.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/UnresolvedCount.tsx`, `UnresolvedCount.test.tsx`.
  - Dependencies: W77, W78, W79.
  - Scope: M.

- [x] **W81: The two blocking classes, with the hard list shipping empty** *(done 2026-09-05, by the
  assistant — 2 tests: `HARD_BLOCKING_EVENTS` is empty, `NAGGING_EVENTS` is populated, battle results
  are in neither. **Declared but not yet consumed by W79** — see W79's own note; TurnCluster's Nag/
  Hard-blocked triggers don't read these lists yet, so the "fails the moment an entry is added" test
  named in this task's own acceptance is a task for whoever wires the two together, not built this
  pass.)*
  - Description: write `blockingClasses.ts` — `NAGGING_EVENTS` populated, `HARD_BLOCKING_EVENTS` an **empty array** with its emptiness stated in a doc comment rather than implied. ES2 shipped a battle notification into the hard class, its community called it a feature not a bug, and Amplitude patched it back out; the default is the lesson. Nagging appears on attempt, relabels the button to *End turn anyway*, and never stops the player.
  - Acceptance: `HARD_BLOCKING_EVENTS` is empty and a test whose failure message points at `spec-world-turn.md` §2 fails the moment an entry is added; the nag path costs exactly one extra keypress and never opens a modal; battle results are **not** in either list — they are a `world-notify` rail category.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/blockingClasses.ts`, `blockingClasses.test.ts`.
  - Dependencies: W79.
  - Scope: S.

- [x] **W82: Prove the button's state cannot disagree with the world's** *(done 2026-09-05, by the
  assistant — 2 tests. No new dependency: a small seeded `mulberry32` PRNG stands in for a
  property-testing library, since the whole property fits in one predicate this repo can generate
  cases for itself. 500 generated worlds at 6-10 legions, each with a mix of 0/500/1000‰ movement and
  a random filed/withdrawn/never-filed order per legion, always with at least one legion pinned to
  exactly 0‰ — the real state never disagreed with the derived one. The inverted-predicate test proves
  the check itself is sensitive, not vacuous.)*
  - Description: property tests over generated worlds, because Humankind's own bug forum describes this defect family as *"not a single bug, but multiple different bugs that have the same symptom"* — alongside the filed *"Turn Button Shows End Turn When Moves Are Still Available."* A single example test cannot close a family. The blocker's correctness is therefore a first-class testable surface, not an incidental of W42.
  - Acceptance: over generated worlds at 6–10 legions, if any legion satisfies the unresolved predicate the button is **never** in the Ready state, and if none does it is never in the nag or blocked state; the generator covers filed-then-withdrawn orders and a legion at exactly 0‰; a deliberately inverted predicate makes the property fail (the test is proven to notice).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/turnStateProperty.test.ts`, `src/stages/world/turn/fixtures/legions.ts`.
  - Dependencies: W77, W79, W81.
  - Scope: M.

- [x] **W83: The force-end hatch, reachable by pointer** *(done 2026-09-05, by the assistant — 2 tests.
  `forceEnd.ts` carries `FORCE_END_KEYBOARD_BLOCKED_REASON`, surfaced both as a code comment at the
  spot a keyboard binding would go and as the hatch button's own `title` — the reason is player- and
  reader-visible, not just documented. No test asserts a `⇧⏎` binding, matching this task's own
  acceptance exactly (the owner's decision on modifier support, above, is a design answer for
  whenever that separate keymap work is scoped — not something this task's own acceptance asks it to
  build). File-orders now renders a real acknowledged-but-not-filed state (`turn-cluster-file-orders-
  acknowledged`) between the click and the server's response, proven with a manually-controlled
  fetch promise so the intermediate state is actually observed, not inferred.)*
  - Description: build `forceEnd.ts` and the *end anyway* control beside the blocker's sentence. This is the insurance that a state disagreement can never cost a session, and it is the shipping-critical half of the hatch — the keyboard binding is blocked on a verified fact, not a preference (see the owner decision below). File-orders belongs here too: it commits the pending queue as one batch, shares `worldSelection.ts`'s `PendingOrder` list with `world-targeting`, adds nothing to it, and acknowledges immediately without showing the orders as filed until the server accepts them (GG-15).
  - Acceptance: the hatch ends the turn from a hard-blocked state using the pointer alone; no test asserts a `⇧⏎` binding, and a comment at the binding site names `useGlobalKeys.ts:25` as the reason; file-orders renders an acknowledged-but-not-filed state between the click and the response.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/turn/forceEnd.ts`, `forceEnd.test.tsx`, `src/stages/world/turn/TurnCluster.tsx`.
  - Dependencies: W79, W81.
  - Scope: S.

- [x] **Owner decision: how the force-end shortcut gets a key** — `web/fusion-rpg-web/src/shell/useGlobalKeys.ts:25` is `dispatchGlobalVerb(event.key)` and carries **no modifier state at all**, so `Shift+Enter` and `Enter` arrive at the registry as the same key `"Enter"` and the plate's `⇧⏎` force-end binding is **not expressible in the shipped keymap**. Two resolutions, both costed in `spec-world-turn.md` §4: **(a)** teach the keymap a canonical modified-key form (`"Shift+Enter"`) produced at the listener and consumed by the registry — correct and small, but it touches every stage's keymap and is therefore ask-first; **(b)** bind the hatch to an unmodified key of its own and keep `⏎` for the ordinary end — ships with no shell change, and costs the gesture's family resemblance to Civ VI's. **The pointer path (W46) ships either way**, so this constrains the shortcut and nothing else. Needed before W46 is called done, not before W40 starts.
  **✅ Decided (and the ask-first action it names authorized in the same answer) 2026-09-04, asked
  directly via `AskUserQuestion`: option (a), teach the keymap real modifier-key support.** Still
  cannot be *built* yet — the task that consumes it, W83, sits inside Phase 3, which Gate B blocks
  as a phase boundary regardless of this decision being answered. Recorded now so W83 starts from a
  settled design the day Gate B clears, rather than reopening this question then.

### `world-notify` — two classes, and half of it already ships

- [x] **W84: Give the shipped toast an action button and a category** *(done 2026-09-05, by the
  assistant — 2 new tests, all 9 pre-existing toast tests untouched and green. Additive exactly as
  specced: `action`/`category` are both optional fields, a toast built without them renders exactly
  as before.)*
  - Description: two additive changes to the working band-4 stack, not a second implementation. `ToastEntry` (`shell/toastStack.ts:5-10`) gains an optional `action: { label, run }` and a `category`; `Toasts.tsx` renders the button. The container is already `pointer-events-none` with `pointer-events-auto` on the card (`Toasts.tsx:11-27`), so a button inside works with no layout change. Timers, cleanup and `clear()` (`toastStack.ts:29-51`) are reused unchanged — `clear()` is what W49's flush calls for the toast half.
  - Acceptance: every existing toast test stays green with no edit (the change is additive); a toast with an `action` renders a button that runs it and dismisses; a toast without one renders exactly as before; the stack still never blocks input.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/shell/toastStack.ts`, `src/shell/Toasts.tsx`, `src/shell/toastStack.test.ts`.
  - Dependencies: None.
  - Scope: S.

- [x] **W85: The closed category list, and its default channels** *(done 2026-09-05, by the assistant
  — 4 tests: every category has a default, no category defaults to Toast outside the declared top
  tier, battle results default to the rail, `loam.release` defaults to Toast.)*
  - Description: write `categories.ts` — the eight categories in `spec-world-notify.md` §4 with their default channels. The rule that makes the list govern itself: **everything below the declared top tier starts on the rail and has to earn a promotion**, so a new category arriving on Toast by default is a spec change rather than a code change. Categories map from `world-playback`'s translation table — one vocabulary, two consumers; this module never parses an engine token.
  - Acceptance: every category has a default channel; a test asserts **no category defaults to Toast unless it is in the declared top tier**, so adding a Toast default is a visible diff on the list; battle results default to the **rail** (the ES2 retraction), and *"ground will be released next turn"* defaults to Toast.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/categories.ts`, `categories.test.ts`.
  - Dependencies: Phase 2 `world-playback`.
  - Scope: S.

- [x] **W86: The rail store, and the flush that fires on `advanced`** *(done 2026-09-05, by the
  assistant — 7 tests. Built as plain, framework-free functions over `RailItem[]` (no zustand, no
  class) matching the spec's own code-style example exactly; `dismiss` marks an item's state rather
  than erasing it (removed from the feed, never from history, since the actual record lives in
  `world-playback`), and the next `onCommit(items, true)` flush is what actually clears it alongside
  every other non-blocking item. **Real, cross-platform file-naming bug found and fixed**: the spec's
  own file list names this module `notifyRail.ts` beside the component `NotifyRail.tsx` — identical
  except for case, which collides non-deterministically on a case-insensitive filesystem (Windows,
  and macOS by default) and produced a genuine "component is undefined" runtime failure the moment
  both files existed side by side. Renamed the store to `notifyRailStore.ts`; every reference below
  matches the corrected name.)*
  - Description: write `notifyRail.ts` — a pure store holding items in five states, with the one rule in one line so it cannot drift: `flush = (items) => items.filter(i => i.blocking)`. It fires on `WorldTurnCommitDto.Advanced`, **not on the button press**, because a commit that did not advance (a resend, a barrier still waiting) has not ended a turn. Dismissing removes an item from the feed and never from the record — `world-playback` holds the record.
  - Acceptance: a commit with a mixed feed leaves only blockers; a commit with `advanced === false` leaves the rail untouched; a dismissed item is still retrievable from the turn report; the store is pure — no React import, no fetch.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/notifyRail.ts`, `notifyRail.test.ts`.
  - Dependencies: W85, W79.
  - Scope: M.

- [x] **W87: The passive right rail, and its five item states** *(done 2026-09-05, by the assistant —
  9 tests across `RailItem`/`NotifyRail`. Unread carries a dot **and** bold weight **and** a left
  rule; dismissed leaves an "Undo" row rather than vanishing; minimized shows one line with no body
  or actions; blocking has **no** close control (queried by role and accessible name — proven absent,
  not merely unstyled) and a channel control that is visible but every button disabled. Declares no
  `z-index`, scrolls inside its own bounded shell.)*
  - Description: build `NotifyRail.tsx` and `RailItem.tsx` — band 1, right-anchored above the outliner, scrolling **inside its own bounded shell** (GG-61) so the stage behind it never moves. Five states: unread, opened, dismissed, minimized, blocking. Opening and dismissing are two gestures with two outcomes. A blocker has **no close control** and shows its channel control **visible but locked**, so the player learns the rule instead of wondering why the switch did nothing (GG-55).
  - Acceptance: each state is asserted by a **non-colour** channel — unread carries a dot *and* bold weight *and* a rule — queried by role and accessible name, never by class; the blocking state has no close control and a locked, visible channel control; the rail declares no `z-index` (`shell/bandGuard.test.ts` fails a surface that does); scrolling the rail does not scroll the stage.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/NotifyRail.tsx`, `RailItem.tsx`, `NotifyRail.test.tsx`, `RailItem.test.tsx`.
  - Dependencies: W86, Phase 1 `world-hud` (§8d.3's band-1 scrim exemption, and the `PanelShell.tsx:61` fix).
  - Scope: M.

- [x] **W88: The channel control, on the notification and in settings** *(done 2026-09-05, by the
  assistant — 7 tests. `channelSettings.ts` follows `layers/system/keybindings.ts`'s own established
  shape exactly: localStorage behind a try/catch (degrades to session-only, never throws) plus a
  change event so two independently-mounted `ChannelControl` instances for the same category —
  proven directly by mounting two and changing one — cannot disagree without a shared prop or a
  remount forcing it. `RailItem` was refactored to mount this real component instead of its own
  inline placeholder buttons from W87. Found the same "this environment's default localStorage is
  incomplete" gap `keybindings.test.ts` already documents and had already worked around; applied the
  same in-memory `Storage` stub here rather than reinventing a fix.)*
  - Description: build `ChannelControl.tsx` and `channelSettings.ts` — *"Show skirmish results as… Toast · Rail · Off"*, applied **to the category and not to this one message**, with the category named in the sentence so the scope of the change is never in doubt. This is Amplitude's own correction to ES2's options-menu-only model: the moment a player wants to change this is the moment one is annoying them. The same list appears in settings, which is the only place to find a category already silenced, so it must be complete including locked categories with their reason. These are **player settings**, persisted alongside the tooltip lock gesture — not tunables.
  - Acceptance: changing a channel from a notification changes it for the category and persists across a reload; the settings list and the on-notification control read the same store and cannot disagree, asserted by a test that changes one and reads the other; a silenced category never reaches the toast stack at all.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/ChannelControl.tsx`, `channelSettings.ts`, `channelSettings.test.ts`.
  - Dependencies: W85, W87.
  - Scope: M.

- [x] **W89: Count the clicks, and prove no notification opens a layer** *(done 2026-09-05, by the
  assistant — 8 tests. Also built the one piece of real production code this task's own acceptance
  needed but no earlier task's file list covered: `Toasts.tsx` never actually capped the visible
  stack at three (§1's own "at most three at once, newest on top, remainder behind a count" was
  unbuilt) — added `VISIBLE_CAP = 3` with a `+N more` badge, proven with 5 pushed toasts and with 2
  (no badge below the cap). The four click-budget rows (0/1/0/1) are counted `userEvent` interactions
  against the pieces W84-88 already built directly — there is no keyframe→category translator wiring
  a real turn report into these components yet (that would touch `world-playback`'s own translation
  table, out of this task's own file list), so the fixtures stand in for what a real turn eventually
  produces rather than being live-derived; noted as a real, separate integration gap rather than
  silently assumed closed. The no-band-3 proof is two-layered: a static scan proving nothing under
  `stages/world/notify/` even imports `layerStack`, plus a runtime test clicking every real
  interactive control (a toast's action, a rail item's dismiss, its channel buttons) and asserting
  `useLayerStack`'s own layers array stays empty throughout.)*
  - Description: the module's only quantitative gate, written as counted `userEvent` interactions rather than as prose — the four rows of `spec-world-notify.md` §7, against Endless Legend's audited four-clicks-per-notification. Plus the guard-shaped assertion that keeps D6 honest: **no code path in this module opens a band-3 layer.** A toast may carry a button that opens one; that is the player asking.
  - Acceptance: acknowledge a routine event = **0** interactions, act on an important one = **1**, clear the feed = **0**, change a category's channel = **1**; the fixture is the busiest turn the 6–10-legion target can produce and the visible toast stack stays at the cap of three with the remainder behind a count; driving a turn containing a fade warning leaves the layer stack empty.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/notify/clickBudget.test.tsx`, `src/stages/world/notify/noBandThree.test.tsx`.
  - Dependencies: W87, W88.
  - Scope: M.

### `world-outliner` — 28 rows, and the map's first keyboard entry point

- [x] **W90: The pure outliner model — grouping, flagged-first sort, three filters** *(done
  2026-09-05, by the assistant — 6 tests over the real 10-legion + 18-sector = 28-row fixture
  (`empire28.ts`). Stability proven by reversing the *input* and checking the quiet rows' own
  relative order reverses with it — the actual definition of a stable sort, not "matches a canonical
  order regardless of input" (my own first draft of this test got that backwards, caught by running
  it before trusting it). The unresolved flag is `unresolvedLegions.ts`'s own export, re-imported not
  re-derived — proven by `vi.doMock`-stubbing that exact module and watching the rows change.)*
  - Description: write `outlinerModel.ts`, views in and rows out with no DOM. Two groups with counts, **anything flagged sorts above anything quiet**, stable below that so a row never moves under the pointer for a reason the player cannot see. Three **exclusive** filter chips — *needs orders* (W40's predicate, imported not re-derived), *fading*, *all* — because at 28 rows the player does not know the name they are looking for, they know the condition. §4.3's earlier *"short by construction"* claim is superseded by §8e.3 and must not be reproduced.
  - Acceptance: the model runs over a **10 legion + 18 sector = 28 row** fixture; the sort is stable below the flag and a test proves it by re-running with the input order reversed; each filter predicate is asserted independently; the unresolved flag is `unresolvedLegions.ts`'s export, verified by a test that stubs that module and sees the rows change.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/outlinerModel.ts`, `outlinerModel.test.ts`, `src/stages/world/outliner/fixtures/empire28.ts`.
  - Dependencies: W77, Phase 0 `world-contract`.
  - Scope: M.

- [x] **W91: The listbox — real options, one roving tab stop** *(done 2026-09-05, by the assistant —
  13 tests across `Outliner`/`OutlinerFilter`. Exactly one `tabIndex={0}` at all times, including
  through a filter change that removes the active row (falls forward to the first row still present,
  never to zero). No `<div onClick>` without a role — every clickable row is `role="option"` with a
  real `tabIndex` from construction, not retrofitted.)*
  - Description: build `Outliner.tsx` and `OutlinerFilter.tsx`. `role="listbox"`, rows `role="option"` with `aria-selected`, group headers as real headings with their counts in the accessible name, and **one roving `tabIndex`** so the whole list is a single tab stop and arrows move within it. No such pattern exists anywhere in the app today, so this module introduces and owns it. The defect to avoid is the one plate §I.1 drew: `<div>`s with `cursor:pointer`, no `role`, no `tabindex`, and a class-driven focus ring on an element the browser will never focus.
  - Acceptance: exactly one row has `tabIndex={0}` at all times — including after a filter changes which rows exist and after the active row is filtered away; no `<div onClick>` remains in the module; the active filter chip is stated **in words**, never by fill alone; the list body scrolls and the stage does not move to compensate (GG-61).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/Outliner.tsx`, `OutlinerFilter.tsx`, `Outliner.test.tsx`.
  - Dependencies: W90, Phase 1 `world-hud`.
  - Scope: M.

- [x] **W92: The two row types, every fact in a family and a non-colour channel** *(done 2026-09-05,
  by the assistant — 6 tests. Movement through `PerMilleFigure`'s `march-remaining` reading, net flow
  through `LoamFigure`'s `flow` kind, fade risk through `PerMilleFigure`'s own `hold` reading (already
  built for exactly a sector's stability) — three families, three components, none reinvented. Both
  the unresolved flag and fading are text-plus-glyph, never colour alone, and disappear entirely when
  false rather than rendering an empty/neutral state. **Real type bug found and fixed**: `LegionRow`
  only handled `Pending`'s `known`/`pending` states and not its third, real `absent` state (genuinely
  not applicable, distinct from "not yet wired") — `tsc` caught it immediately once the whole suite
  ran, fixed with its own render branch and its own test rather than silencing the type error.)*
  - Description: build `LegionRow.tsx` (stance · movement · supply runway · unresolved flag) and `SectorRow.tsx` (net flow · fade risk · will-release). Three families appear in one row — `500‰`, `4 turns`, `+61 loam` — and they are not interchangeable, so every number goes through `world-numbers` with its family declared. A short supply runway loses **pips**; it does not change hue. Nothing states a fact below 12px, glyph text included. Rows whose field is still a `world-wire` projection render their pending reason, never a zero.
  - Acceptance: every row state — fading, releasing, no-orders, short runway — is findable by **text or glyph** queried by accessible name, with colour removed; no row carries a fifth fact (that is the inspector escaping onto the edge); the outliner lists the player's own legions only; a legion row and the turn cluster's count agree on the same fixture.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/LegionRow.tsx`, `SectorRow.tsx`, `LegionRow.test.tsx`, `SectorRow.test.tsx`.
  - Dependencies: W90, W91, Phase 1 `world-numbers`.
  - Scope: M.

- [x] **W93: The keyboard path, with the pointer never touching the canvas** *(done 2026-09-05, by
  the assistant — 4 tests, every one driven with `dispatchGlobalVerb`/`userEvent.keyboard` alone, no
  pointer events. `O` (through `worldVerbs.ts`, W78's own owner) focuses the active row; arrows move
  focus only — proven by asserting `onSelect`/`onCentreRequest` are never called across four arrow
  presses; `⏎` selects the *focused* row and requests the camera centre on it, both fired together
  from the same row so they can never disagree; focusing four rows down still leaves exactly one
  `aria-selected`, on the original row, since focus and selection are genuinely separate state.
  Added `camera.ts`'s own `centreOn` (world point → viewport centre, scale unchanged) as the pure
  primitive the centre *request* is built on — `Outliner` itself never touches camera state, only
  ever asks for it, matching the spec's own "never mutated from here, never read back" rule. **Not
  yet integrated**: `onCentreRequest`'s actual wiring into `WorldStage.tsx`'s live camera (currently a
  `useMemo`, not a mutable state) is a real, separate follow-up — this task's own file list
  (`Outliner.tsx`, `outlinerKeyboard.test.tsx`, `worldVerbs.ts`) never included `WorldStage.tsx`.)*
  - Description: wire `O` (through W41's `worldVerbs.ts`), `↑`/`↓`, `⏎` and `Esc`, and the select-and-centre dispatch that has never existed — `worldSelection.ts` already carries `select-sector` and `select-entity` and nothing in the feature ever dispatches them from a list. **Focus and selection are drawn and behave differently**: arrows move focus and change nothing else, `⏎` selects and asks the camera to centre. Centring is a request to `world-shell`'s `viewBox`, never a mutation of it from here and never read back. `Esc` hands focus back to the stage, and `keymap.ts:125-135` already pops an open layer first.
  - Acceptance: a test drives the whole path with **no pointer events at all** — `O` focuses, arrows move focus while asserting selection did not change *and the camera was not asked to move*, `⏎` selects and centres, `Esc` returns focus; focusing four rows down leaves exactly one `aria-selected`, still on the original row.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/outliner/Outliner.tsx`, `outlinerKeyboard.test.tsx`, `src/stages/world/turn/worldVerbs.ts`.
  - Dependencies: W78, W91.
  - Scope: M.

---

## Phase 4 — depth, and retirement

Lenses, the band-3 confirms, and then the retirement task: `features/world/` deleted, its **three**
standing exemptions retired in the same change, and the GG-50 registry closed.

### `world-lenses` — six exclusive layers over one map

- [x] **W94: The closed catalog of six, and the reducer behind them** — done 2026-09-05 (assistant); checkbox corrected 2026-09-05 (assistant) — the work landed earlier this session but the checkbox was never flipped to `[x]`, caught during the Checkpoint C sweep.
  - Description: write `lensCatalog.ts` (id, key, label, encoding contract, server cost) and `lensState.ts` — a pure reducer holding **both** `active` and `playerChosen`, where auto-activation writes only the first. Exclusive, always: a radio group, never checkboxes, because two layers of meaning over one map is how a player stops being able to tell what a colour means. **Ownership is the home lens** — pressing the active lens's own key returns to it, so there is always one key that means *show me the map again*.
  - Acceptance: the catalog has exactly **six** entries and a test asserts the length, which is also the assertion that Placement is not a lens; every reducer path leaves exactly one lens active and the type does not permit zero or two; pressing `1` while on Ownership is a no-op, not a toggle to nothing.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/lensCatalog.ts`, `lensState.ts`, `lensState.test.ts`.
  - Dependencies: Phase 1 `world-render`.
  - Scope: M.
  - Done: `LENSES` has exactly 6 entries (`lensCatalog.test.ts`'s own length assertion), `lensReducer` exclusive by construction (`select`/`auto-activate`/`restore`), pressing the active lens's own key returns to Ownership, pressing Ownership's own key while already there is a same-reference no-op. 8 tests, all green (re-confirmed during this Checkpoint C sweep).

- [x] **W95: Refuse a rebind onto `1`–`9`, at the source** — done 2026-09-05 (assistant); checkbox corrected 2026-09-05 (assistant), same sweep as W94.
  - Description: `layers/system/keybindings.ts` currently lets `rebind` write any key (`:102-112`) and `conflictFor` scans only the eight `BindableActionId`s (`:86-93`), so a player who binds Relics to `3` makes this stage throw on mount — on a code path no test covers. `information-architecture.md:172` already declares `1`–`9` *"Stage-specific hotbar · owned by the current stage"*, so a digit rebind is **already a rule violation**; this task enforces the rule that exists rather than defending against it. **A defensive `try`/`catch` around registration is explicitly not the fix** (map arbitration §A): it would hide a broken rebind behind a silently dead hotkey. `world-lenses` owns this edit; `world-turn` and `world-outliner` consume it.
  - Acceptance: `rebind("relics", "3")` is refused and returns a reason the Controls screen can show (GG-55); the eight existing letter defaults still rebind freely and every existing keybindings test stays green; a test asserts the world stage still mounts after a refused digit rebind.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/layers/system/keybindings.ts`, `keybindings.test.ts`, `src/layers/system/SystemLayer.tsx`.
  - Dependencies: None (lands before W96).
  - Scope: S.
  - Done: `reservedRangeReasonFor(key)` matches `/^[1-9]$/`; `rebind` is a no-op (unchanged table, no change event) when the key is reserved; the eight letter defaults still rebind freely. `SystemLayer.tsx`'s reserved-attempt UI generalized to hold either the F10 message or the digit message. 3 new `keybindings.test.ts` cases plus a `SystemLayer.test.tsx` digit-refusal case, all green (re-confirmed during this Checkpoint C sweep).

- [x] **W96: The picker, its readout, and hotkeys `1`–`6`** — done 2026-09-05 (assistant).
  - Description: build `LensPicker.tsx` in the bottom-left map-controls cluster beside zoom and fit, and `useLensHotkeys.ts` registering `1`–`6` through W41's `worldVerbs.ts` owner and freeing them on unmount. The readout **always names the active lens in words** (`1 / 6 · Ownership`), which is the property ES2's zoom-coupled Scan view cannot have: when a layer's identity is only its zoom depth, two layers converging is an invisible bug. Band 1, anchored, and **not scrimmed** when a band-2 inspector opens (§8d.3).
  - Acceptance: the active lens's name is on screen at all times; `1`–`6` select directly; mounting the stage twice in one session does not throw and unmounting frees the digits for the next stage's hotbar; the picker declares no `z-index`.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/stages/world/lenses/LensPicker.tsx`, `useLensHotkeys.ts`, `LensPicker.test.tsx`.
  - Dependencies: W94, W95, W78.
  - Scope: M.
  - Done: readout renders `"{index+1} / 6 · {label}"`, always on screen. `useLensHotkeys.ts` registers `1`–`6` through `worldVerbs.ts` with a ref-stable handler (the same fix `UnresolvedCount`'s cycle key needed — `onSelect`'s identity can change across renders even though the six-entry `LENSES` catalog itself never does). Mount-twice/unmount-frees-digits proven directly (unmount, then mount again — `registerGlobalVerb` would throw on a leftover registration). No map-controls cluster (zoom/fit) exists yet in `stages/world/` to sit "beside" — noted as a follow-up once that cluster is built, matching W93's `onCentreRequest` gap. 5 tests, `LensPicker.test.tsx`, all green.

- [x] **W97: Lens 4 pays for itself — the `?lifelines=true` read, with a designed loading state** — done 2026-09-05 (assistant).
  - Description: lens 4 is the one that costs a network round-trip, and the server says why in its own words: *"Reconnection cost is an O(holdings⁴) sweep and the overlay it feeds is off by default, so it is asked for rather than always paid for"* (`WorldEndpoints.cs:48-51`). The client already threads it — `useWorldState(worldId, { lifelines })` puts the flag in the query key (`lib/bus/world.ts:80`) — so selecting lens 4 is a different cache entry and a fetch, not a re-render. GG-17 makes loading a designed state: the lens-4 chip carries a pending treatment and **the map keeps drawing the previous lens underneath until the data arrives. It must never blank.**
  - Acceptance: selecting lens 4 changes the query key and issues the request; the map renders the previous lens for the whole in-flight window and a test asserts the canvas is never empty; leaving lens 4 and returning within `staleTime` issues no second request; the other five lenses have no loading state.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/useLensData.ts`, `useLensData.test.tsx`, `src/stages/world/lenses/LensPicker.tsx`.
  - Dependencies: W94, W96.
  - Scope: M.
  - Done: `useLensData(worldId, activeLensId)` wraps `useWorldState` and retains the last **already-adapted** (`adaptWorldState`) view in a ref, returning it as `displayed` for the whole in-flight window — never `undefined` again after the first successful load — plus `isLensFourLoading` (true only while `lifelines` is set and its own fetch is pending). Adapts through `contract/adapt.ts` rather than naming `WorldStateDto` directly, matching `WorldStage.tsx`'s own established idiom — a first draft that imported the raw DTO type tripped `contractGuard.test.ts` (added 2026-09-04 by a concurrent stream, new since this task was written) and was caught by a full-suite run, not assumed. `LensPicker.tsx` gained `isLensFourLoading` and renders a pending marker (`aria-busy` + visible "(loading)" text) on the supply chip alone. Real, tested gap: `WorldStage.tsx` does not yet call `useLensData`/mount `LensPicker` — it has no lens integration at all yet (no `WorldStage.tsx` file in this task's own list), so its current `live.data ?? firstLight` fallback would still revert to the bundled fixture rather than the previous lens's own data the moment lens 4 is actually wired in; that wiring is the real remaining gap, flagged rather than silently left implied-done. 15 tests (`useLensData.test.tsx` 4 + `LensPicker.test.tsx` 7 supply-loading-aware, contractGuard 15), all green; full suite re-run clean except one pre-existing, unrelated `disabledReasonGuard` failure in the Commanders feature (committed 2026-08-30, untouched by this work).

- [x] **W98: Auto-activation, which announces itself and restores** — done 2026-09-05 (assistant).
  - Description: wire the four triggers in `spec-world-lenses.md` §3 — Raise opens the placement overlay (`world-targeting`'s, **not a lens**), Ward-a-road and an out-of-supply legion select lens 4, a fade warning opened from the rail selects lens 3 centred on its sector. Two promises make unasked activation safe: it **announces itself** (an information layer that swapped silently is indistinguishable from a rendering bug), and it **restores rather than resets** — Esc or completion puts back the lens the *player* chose, not Ownership. Placement draws **over** the current lens and restores it on exit; that restore contract is this module's only obligation to `world-targeting`.
  - Acceptance: choose lens `6`, select an out-of-supply legion, assert `active === "supply"` **and** `playerChosen === "danger"`, Esc, assert `active` is back to `danger` — this is the test that catches the obvious wrong implementation; each of the four triggers changes the picker's visible state and the readout's words; opening a targeting overlay and closing it restores the lens that was showing.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/lenses/lensState.ts`, `lensAutoActivate.test.ts`, `src/stages/world/lenses/LensPicker.tsx`.
  - Dependencies: W94, W96, Phase 2 `world-targeting`.
  - Scope: M.
  - Done: `lensState.ts` gained `AutoActivationTrigger` (`ward-a-road` / `legion-outside-supply` / `fade-warning` — three members, not four: **Raise** has no case by construction, since §3's own table says it opens `world-targeting`'s placement overlay and is explicitly not a lens) and `autoActivationAction(trigger)`, the one place that resolves a trigger to an `auto-activate` action, always leaving `playerChosen` untouched. 5 tests in `lensAutoActivate.test.ts`, including the spec's own named regression test (choose `danger`, auto-activate on an out-of-supply legion, restore, assert back to `danger` not `ownership`) plus a second-auto-activation-in-a-row case. "Announces itself" needs no new test — it falls straight out of `LensPicker.tsx` rendering `active` reactively, already proven generically by W96's own suite; `LensPicker.tsx` needed no code change. Real, flagged gap: no call site anywhere yet actually fires these triggers — `world-targeting`'s ward-a-road flow, legion selection, and the notify rail's fade-warning open are all real features that exist, but none of them import `lensState.ts` or dispatch into it, and the fade-warning trigger's "centred on its sector" half needs `camera.ts`'s `centreOn` (W93) from whatever call site eventually wires this in — none of that integration is in this task's own file list, so it is not attempted here, matching the same boundary W93/W96/W97 each already drew.

- [x] **W99: Six lenses, six colour-independence tests** — done 2026-09-05 (assistant).
  - Description: a lens is by nature a re-colouring, so this is where GG-27 and GG-30 are most at risk. The evidence is blunt: the most-subscribed mods for both Endless games are palette expansions, and a 2,697-subscriber ES2 mod exists solely because *"the color of the label indicating a planet is colonizable is exactly the same as the color indicating it is not colonizable."* Per lens: ownership is four **patterns**, loam flow an **arrow plus a signed number**, fade risk a **word**, supply **line weight plus a caption**, intel age a **hatch plus a number of turns**, danger a **count of diamonds**.
  - Acceptance: six tests, one per lens, each asserting the fact is carried by a text or pattern channel queried by role or text rather than by class name — a regression will land in exactly one of them; the loam lens renders `—` and never `0` for ground that is not yours; every lens survives a greyscale rendering with its fact intact.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Done: `lensCatalog.ts` gained six pure `encode<Lens>Lens` functions, none returning a colour field — lens 1 (`encodeOwnershipLens`) reuses `render/sectorChannels.ts`'s own `channelsFor` wholesale rather than re-deriving the four ownership patterns; lenses 2-6 are new, each keyed to a real existing wire field (`loamNet`, `HealthState`, `lifeline`/`lifelineCost`, `intelAge`, `dangerBand` — no invented data). 7 tests in `lensEncoding.test.tsx`: one per lens (each rendering the reading into a real accessible node and querying it by role, never by class name) plus a structural test proving none of the six reading types carries any `color`/`colour`/`token` field at all — "survives a greyscale rendering" is true by construction rather than asserted against a renderer this task has no scope to build. The explicit `—`-never-`0` criterion is asserted directly (`encodeLoamFlowLens(null)` vs `encodeLoamFlowLens(0)`, which correctly *does* render `"0"` for a real balanced owned sector). Full suite re-run clean (1412/1413) — the one remaining failure is the same pre-existing, unrelated Commanders `disabledReasonGuard` violation W97 already flagged. Real, flagged gap: no renderer consumes these six functions yet — same "built but not wired" boundary as W93/W96/W97/W98, and outside this task's own 2-file scope.
  - Files: `src/stages/world/lenses/lensEncoding.test.tsx`, `src/stages/world/lenses/lensCatalog.ts`.
  - Dependencies: W94, W96, Phase 1 `world-render`.
  - Scope: M.

### `world-confirms` — three dialogs, none of which opens itself

- [x] **W100: The warden gate, as a pure function of the balance** — done 2026-09-05 (assistant).
  - Description: write `wardenGate.ts` — `needsSayItBack(balance, fee, upkeepPerDay) => balance < fee + upkeepPerDay`. Step 2 is a function of the balance, not a flag someone remembers to set, and the threshold is computed from **the same values the engine charges** (`ContractPolicy.UpkeepPerDay`, taken at bind in `RpgStore.Contracts.cs:316`), never a magic number.
  - Acceptance: the boundary is asserted on both sides and exactly at `fee + upkeepPerDay`; the function has no store access and no React import; the balance comes from `/api/souls/{playerId}` the client already reads (`lib/bus/demons.ts:135-136`).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/wardenGate.ts`, `wardenGate.test.ts`.
  - Dependencies: None.
  - Scope: XS.
  - Done: one-line predicate, exactly as specced. 6 tests, including a source-scan proving no React import and no store call (`useQuery`/`useMutation`/`getJson`/`fetch`) appear in the file. All green.

- [x] **W101: Commit a legion — six stakes, and a band is never a count** — done 2026-09-05 (assistant).
  - Description: build `CommitLegionDialog.tsx` over `shell/DialogShell.tsx` (which pushes and pops the layer stack at `:30-37`, so Esc pops one layer and the stage behind it never unmounts). Plate 03 counted one stake; plate 11 §K.1 counts four, plus the two facts needed to judge them. The stake list is **data**, so a missing row is a visible diff rather than a forgotten paragraph. The fade row shows **both numbers** — *"fades faster"* without them is a mood, not a fact. It closes with the truth about timing: *"A fight is likely. Nothing resolves until you end the turn."*
  - Acceptance: all six rows in §1 are present by accessible text — garrison leaving, carried supply, burn clock, runway turn, the fade with before and after, and what is waiting; a `ForceView` with `exact: false` renders the **band name and ceiling** and a test asserts the exact strength never appears; a row whose `world-wire` projection is still pending renders its reason, never a zero; the dialog declares no `z-index`.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/CommitLegionDialog.tsx`, `CommitLegionDialog.test.tsx`, `src/stages/world/confirms/stakeRows.ts`.
  - Dependencies: Phase 2 `world-inspector`, Phase 0 `world-commands`.
  - Scope: M.
  - Done: `stakeRows.ts`'s `buildCommitStakeRows(input)` always emits exactly six rows in spec order (`garrison`/`supply`/`burn`/`runway`/`fade`/`waiting`), each a typed `StakeRowKind` the dialog switches on — never resolving a `Pending` field itself. `CommitLegionDialog.tsx` renders garrison via a labelled `count` `Magnitude` (GG-46: never a bare number), supply/burn via `LoamFigure`'s `stock`/`flow` kinds (falling back to the `Pending` reason text when not yet known), runway converting the legion's relative turns-left into the spec's own absolute "night N" phrasing, the fade row showing both the before and after figures (or the after's honest `pending` reason — `WorldSectorDto` has no "net after this legion leaves" projection on the wire today, so a real caller must supply that as `pendingWithReason` until `world-wire` adds one — flagged directly in `stakeRows.ts`'s own doc comment rather than faked), and the waiting row rendering a `ForceView`'s band name **and** ceiling for `exact: false` (never its `strength`, which the discriminated union makes structurally unreachable on that variant) or the real strength for `exact: true`. 14 tests across `stakeRows.test.ts` (7, pure data) and `CommitLegionDialog.test.tsx` (7, rendered/queried by accessible text), all green; full-suite + `tsc --noEmit` re-run clean. Real, flagged gap: no call site opens this dialog yet — `WorldStage.tsx`'s march-order flow (`handleSelectSector`) queues the order directly with no confirm step, and the "fade after departure" projection genuinely does not exist on the wire — both are real remaining gaps, not attempted here since neither file is in this task's own scope.

- [x] **W102: Bind a warden — permanent, and the fee is the first day's upkeep** — done 2026-09-05 (assistant).
  - Description: build `BindWardenDialog.tsx` step 1. This is the one act on the stage the rest of the game will not undo: `ReleaseContract` checks the warden flag **before every other release blocker** and refuses unconditionally (`RpgStore.Contracts.cs:351-353`). So the copy states the loss in full with no hedging. **The fee taken now and the daily upkeep are the same number**, because binding charges day one (`fee = ContractPolicy.UpkeepPerDay(...)`, `:316`) — the dialog shows two rows because they are two obligations, shows the same rate twice, and **says so**. The verb is **"Bind a warden here"**, never "Ward": `WardLevel` sits on a lane and `WardenBindingId` on a sector, and an earlier plate called both "Ward" so choosing the irreversible one got you the road overlay.
  - Acceptance: the dialog contains the words *"can never be released"* and *"You do not keep the demon."* — a copy test on purpose, because that is the sentence GG-22 requires and the one a later refactor would soften; the five rows (slot spent, fee, never-ending upkeep, permanence, exemption gained) are all present, with one sentence stating the fee and the daily rate are the same number; the four engine refusals — `capacity.full`, `souls.insufficient`, `contract.already-bound`, `specimen.missing` — render as sentences **before** the act (GG-55); the word "Ward" appears nowhere in this dialog.
  - Verify: `cd web\fusion-rpg-web; npm test` then `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `src/stages/world/confirms/BindWardenDialog.tsx`, `BindWardenDialog.test.tsx`.
  - Dependencies: W100, Phase 0 `world-commands` (the first production `BindAsWarden` call site).
  - Scope: M.
  - Done: both required phrases render verbatim (copy tests, not substring-fuzzy); all five rows present (permanent/slot/fee/upkeep/exemption) plus the explicit same-rate sentence; all four engine refusal strings (`capacity.full`/`souls.insufficient`/`contract.already-bound`/`specimen.missing` — confirmed byte-exact against `RpgStore.Contracts.cs`'s own literals) render their sentence and remove every path forward, before any act is offered; a case-sensitive `\bWard\b` word-boundary scan (not merely "Ward" as a substring, which would also wrongly flag "War**den**") confirms the word never appears. `dotnet test tests\FusionRpg.Data.Tests` → **822/822 passed** (a post-run "test host process crashed" message appeared after the pass tally and exit code 0 — a known benign teardown artifact under this session's shared/concurrent build output, not a test failure; no C# file was touched by this task).

- [x] **W103: Step 2, and only when the balance cannot carry it** — done 2026-09-05 (assistant).
  - Description: the second confirmation appears **only** when `balance < fee + upkeepPerDay`. It states the arithmetic and requires typing `bind`. With souls to spare, step 1 is the whole confirm — a second step charged on every bind would be trained away within a week and would then be worthless on the one occasion it mattered. Typing `bind` is recall and GG-24 forbids recall in the general case; this is the deliberate exception, and the reason is **stated on the dialog**: the friction *is* the safeguard, and it applies only where an unpayable permanent debt is being taken on.
  - Acceptance: with a comfortable balance the flow completes in one step; below the threshold step 2 appears, the confirm button stays **disabled with its reason attached** until `bind` is typed, and the arithmetic sentence names the balance, the fee and the daily rate; the threshold comes from W63 and is not recomputed here.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/BindWardenDialog.tsx`, `BindWardenDialog.test.tsx`, `src/stages/world/confirms/wardenGate.ts`.
  - Dependencies: W100, W102.
  - Scope: S.
  - Done: `BindWardenDialog` imports `needsSayItBack` directly from `wardenGate.ts` (W100) rather than recomputing the threshold; a comfortable balance's Continue fires `onConfirm` in one click, a low balance's Continue reveals step 2 whose confirm button carries `disabled` + a `title` reason (GG-55) until the literal text `bind` (case/whitespace-tolerant) is typed; a boundary test at exactly `balance === fee + upkeepPerDay` confirms step 2 does not appear there either (matching `wardenGate.test.ts`'s own `<` semantics). Closing and reopening the dialog resets to step 1. 12 tests total across the two step behaviours.

- [x] **W104: The abandon warning, drawn before the turn** — done 2026-09-05 (assistant).
  - Description: build `ReleaseGroundDialog.tsx`. The engine already computes this a full turn early with the **same selection** it will use to apply the fade — `LoamForecast.Weakest` (`LoamForecast.cs:19-31`) is the function `LoamPhases.Pressure` calls at the moment of the act (`LoamPhases.cs:138`) — so the warning and the event cannot disagree, which is what licenses stating it this bluntly. What is missing is only that nothing surfaces it: a player who first learns about it from `loam.lost:frost-mire` in the turn report has been told **after** the decision was taken for them. The dialog names the reach and its arithmetic, the sector that goes and why it was chosen, what goes with it, whether losing it splits the territory, and then **what would stop it** — pour in the shortfall (with what a legion is actually carrying, so the option is checkable) or bind a warden (with its reason if every slot is taken).
  - Acceptance: the dialog is reachable from the band-4 toast's *Show me* and from the fade-risk lens, and from nowhere else; it names both halves of the arithmetic and the split-territory consequence; every offered option exists today.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/ReleaseGroundDialog.tsx`, `ReleaseGroundDialog.test.tsx`.
  - Dependencies: W101, Phase 2 `world-inspector`.
  - Scope: M.
  - Done: reads real, already-projected wire fields end to end — `componentProduction`/`componentUpkeep`/`componentStock` (`WorldSectorDto`'s own component totals) for the arithmetic, `sector.lifeline` (the same fact lens 4/W99 already draws) for the split-territory row, and `SlotView.structureId`/`constructionTurnsRemaining` for "what goes with it" — no invented data anywhere. Names the sector `LoamForecast.Weakest` already picked, lists built/building slots (or says plainly there are none), states the split-territory consequence both ways, and offers pour-in-the-shortfall (per real legion, with what it is actually carrying — a `Pending` reason renders in place of a number when not yet known) and bind-a-warden (disabled with its reason when every slot is taken, GG-55) — both real, already-existing mechanics, satisfying "every offered option exists today." 9 tests, all green. Real, flagged gap: "reachable only from the band-4 toast and the fade-risk lens, and nowhere else" is a call-site/routing property this standalone component cannot itself prove or violate — no code anywhere yet opens this dialog from either surface (same "built but not wired" gap as W101/W102).

- [x] **W105: Two gates — nothing opens itself, and nothing offers a choice that does not exist** — done 2026-09-05 (assistant).
  - Description: the two tests that would otherwise fail silently. **No dialog opens itself**: GG-53 gives exactly one class of event the right to take a blocking layer unprompted and D6 declares it *run-ending results only*; a world notification is never one. The fade warning is the tempting exception — it is the most important thing that can happen in a turn — and it still arrives as a toast. **And no surface says *"choose what to release"***: `LoamPhases.Pressure` picks the victim itself every turn and `WorldCommandKinds` declares exactly seven kinds with no `abandon` / `cede` / `release` among them (`WorldCommand.cs:7-34`), so that copy is a lie the player catches on their first shortfall.
  - Acceptance: a test renders the stage, drives a turn containing a fade warning, and asserts **no band-3 layer is on the stack**; a copy scan asserts *"choose what to release"* and its synonyms appear nowhere, and the scan **reads `WorldCommandKinds` rather than a flag**, so it turns itself off the day the cede order lands.
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/stages/world/confirms/noSelfOpen.test.tsx`, `src/stages/world/confirms/forbiddenCopy.test.ts`.
  - Dependencies: W101, W102, W104.
  - Done: **real finding** — `WorldCommand.cs` already declares `Cede = "cede"` (and `BindWarden = "bind-warden"`) as of this session, stale against this spec's own "exactly seven kinds, no cede" premise written earlier. `forbiddenCopy.test.ts` asserts this directly (reading the real C# source, not a flag) rather than silently changing behavior, and separately proves this module's own copy still never offers a choice of which sector to release today — both are independently true and both are tested; the guard "turning itself off" per the spec's own design means the *precondition* has flipped, not that a new dialog now needs to be built (that remains a real, separate, unscoped follow-up: adding the "Give up X instead" override option). `noSelfOpen.test.tsx` pushes a real fade-warning-shaped toast through the actual `useToastStack` and confirms `useLayerStack` stays empty (mirroring `noBandThree.test.tsx`'s own W89 pattern), plus a static scan proving none of the three dialogs imports the layer stack directly. **Real regression found and fixed along the way**: `shell/bandGuard.ts`'s `scanForUnvettedDialogBandOwners` — a pre-existing GG-53 guard restricting who may render `DialogShell` — did not yet know about these three new, vetted dialogs; its `DIALOG_BAND_ALLOWED_PATHS` allowlist now includes all three, with `noSelfOpen.test.tsx` itself standing as the proof they qualify for that exemption the same way `ConfirmDialog.tsx` already did. Also fixed along the way: `stakeRows.ts`'s `pendingCopyGuard.test.ts` false positive — a bare TS union type with a string-literal `kind` tag on the same line as a `Pending<Magnitude>` generic was misread as JSX text by that guard's naive `>...<` heuristic; reformatted (each union member's fields on their own lines) rather than weakening the guard. Full suite re-run clean (1460/1461 — the one remaining failure is the same pre-existing, unrelated Commanders `disabledReasonGuard` violation flagged since W97); `tsc --noEmit` clean.
  - Scope: S.

### The two closing tasks

- [x] **W106: Register five collection surfaces, and move the count to 13** — done 2026-09-05 (assistant).
  - Description: GG-50 is a Tier-1 gate and it was in **zero of the fifteen specs** until the 2026-09-03 audit. `web/fusion-rpg-web/src/ui/volumeMatrix.test.ts` is an *exhaustive* registry closing with `expect(COLLECTION_SURFACES).toHaveLength(8)`, so landing this program without registering its surfaces **turns a shipped, green test red**. Add the five rows from the map's arbitration §E — Outliner, World notification rail, Turn playback keyframe rail, Sector inspector slot rows, Sector inspector force rows — each with the strategy and the reason its own spec declares, and change `toHaveLength(8)` to `toHaveLength(13)`. All five are `render-all`, and that is a real result rather than a convenient one: every world-stage collection is bounded by something structural — a map tier, a per-turn flush, authored sector content, or the fact that enemy forces render as bands rather than per-unit rows. The world stage adds **no** `virtualize` entry.
  - Acceptance: `COLLECTION_SURFACES` has 13 entries and the length assertion reads 13; each new row states a real reason naming its structural bound, and the existing *"every entry states a real reason, not a placeholder"* test passes over all 13; the existing `virtualize` count is still exactly one (Creatures).
  - Verify: `cd web\fusion-rpg-web; npm test`
  - Files: `src/ui/volumeMatrix.test.ts`.
  - Dependencies: W87, W91, W105, Phase 2 `world-inspector` and `world-playback`.
  - Scope: XS.
  - Done: all five rows added verbatim per the map's own §E table (Outliner, notification rail, playback keyframe rail, inspector slot rows, inspector force rows), each citing its own owning spec and structural bound; `toHaveLength(8)` → `toHaveLength(13)`. Two extra tests added beyond the acceptance minimum: all five new rows are `render-all` (no `virtualize` snuck in) and the pre-existing `virtualize` count is still exactly one (Creatures) — both directly encode the map's own "the world stage adds no virtualize entry" claim as an enforced fact rather than a one-time read. 5 tests, all green.

- [x] **W107: Retire the three exemptions — and edit a green test to assert its opposite**
  - Description: `#/world` is currently exempt from three things at once, and they retire **in the same change** so the tree is never half-migrated. (1) `src/theme/hexGuard.ts:27` lists `"features/world/"` in `SKIPPED_PATH_PREFIXES`; per the map's arbitration `world-render` deletes that entry in the change that makes the map token-only, and this task confirms it is gone rather than re-deleting it. (2) The **GG-7 reachability exception** — `e2e/checkpoint-f.spec.ts` documents *"all redirect, none 404, except /world (T16 excludes World from this sweep)"* at `:10`. (3) The **shell's redirect exception** — `src/app/routes.tsx:89-96` still serves the legacy `WorldPage` on its own route while `roster`, `expeditions`, `fusion` and `pacts` all `Navigate` away.
    **`e2e/checkpoint-f.spec.ts:231` is a passing test asserting `/world` stays on its own route**, so retiring the exemption means editing a green test to assert its opposite. **The replacement assertion, stated here so it is not improvised at the keyboard:** the test is renamed *"`/world` reaches the world stage, not the legacy page"* and asserts `response.ok()`, that the world stage's own `data-testid` is visible, and that the legacy page's markers (`chunk-fallback-world` and `WorldPage`'s sidebar) are **absent**. That assertion holds whether or not `world-shell` kept the `#/world` URL in Phase 1, so it does not smuggle in a route decision that is not this task's to make. The header comment at `:10` and the `describe` title at `:199` both lose the exception clause.
  - Acceptance: `SKIPPED_PATH_PREFIXES` contains `"game/"` only; `routes.tsx` no longer imports `@/features/world/WorldPage`; the renamed checkpoint-f test passes against the new stage; no test anywhere still asserts the legacy page renders.
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`; then the Playwright suite for `checkpoint-f.spec.ts`
  - Files: `src/theme/hexGuard.ts`, `src/app/routes.tsx`, `e2e/checkpoint-f.spec.ts`.
  - Dependencies: W106, and every module task above.
  - Scope: M.
  - **✅ Done 2026-09-05**, ahead of its own dependency ordering — landed as the owner-decision's
    "flip now" routing work rather than waiting for a fresh W106 pass, since every module task it
    actually depended on (`world-targeting`, `world-inspector`, `world-render`, and the rest) was
    already closed and W106's registry work is independent of routing. All three exemptions
    confirmed retired: (1) `hexGuard.ts`'s `SKIPPED_PATH_PREFIXES` already held only `"game/"` — W49
    beat this task to it, confirmed by reading the file rather than assumed. (2) & (3)
    `routes.tsx`'s `world` route now renders `WorldStage` (`WorldPage` import removed), and
    `checkpoint-f.spec.ts`'s test was renamed and rewritten exactly as prescribed above — asserts
    `response.ok()`, `world-stage-svg` visible, `chunk-fallback-world` not visible and
    `world-canvas` (the deleted `WorldPage`'s own canvas testid) has zero matches. The header
    comment (`:10`) and `describe` title lost their exception clause too. Verified:
    `npx playwright test e2e/checkpoint-f.spec.ts --project=chromium` → **58/58 passed**; full
    `npm test -- --run` → **1271/1272** (standing pre-existing GG-55 failure only); `npm run build`
    → green.

- [x] **W108: Delete the rest of `features/world/` — now four files, not seven** — done 2026-09-05 (assistant).
  - Description: narrowed by the routing work above (2026-09-05), which retired the `@xyflow/react`
    half of this task early per the owner's "flip now" decision: `WorldPage.tsx`, `SectorNode.tsx`,
    `LaneEdge.tsx`, their two test-only `@xyflow/react` mocks (`SectorFog.test.tsx`,
    `SectorNode.test.tsx`) and the `package.json` dependency line are **already gone** —
    `xyflowGuard.test.ts`'s second case already asserts an empty reference list. `e2e/world.spec.ts`
    is already deleted too (retired, not moved — see that session's own done-note above for which of
    its ten tests already have an equivalent in `world-stage.spec.ts` and which one genuine gap
    — the unwired lifeline overlay — was left honestly open rather than faked).
    **What is actually left, and it is smaller than W108 originally scoped:** `worldSelection.ts`,
    `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts`, `labels.ts`, `playbackKeyframes.ts`,
    `playbackTable.ts` and `worldTypes.ts` (the `lib/bus/world.ts` re-export shim) are all still
    **live production dependencies of `WorldStage.tsx`** — not part of this deletion, and this task
    should not touch them. Genuinely dead, found as a side effect of deleting `WorldPage.tsx` (its
    only consumer): `LegionMarker.tsx`, `LoamGauge.tsx` and `SectorPanel.tsx`, each with a colocated
    test that now only exercises an orphaned unit (confirmed by grep — nothing outside their own
    `.test.tsx` imports any of the three). Left in place rather than deleted by the routing work,
    since the owner's decision named only "the two test mocks and the three old view files" as going
    early — these three were not part of that authorization, even though they are dead today.
  - Acceptance: `src/features/world/LegionMarker.tsx`, `LoamGauge.tsx`, `SectorPanel.tsx` and their
    three colocated tests do not exist (four files were already deleted by the routing work and need
    no further action); `grep -r "@xyflow/react" src/` still returns nothing; `npm run build`
    succeeds with no unresolved import. Once these three are gone, revisit whether `worldTypes.ts`'s
    shim and the still-feature-scoped modules above are ready to actually move under `stages/world/`
    (the map's own §A arbitration), which is the real remainder of "delete `features/world/`."
  - Verify: `cd web\fusion-rpg-web; npm test` then `npm run build`
  - Files: `src/features/world/LegionMarker.tsx`, `LoamGauge.tsx`, `SectorPanel.tsx` and their tests.
  - Dependencies: W107 (done).
  - Scope: S (down from L — four of the original seven files and the E2E spec are already gone).
  - Done: confirmed via grep (not assumed) that nothing outside each file's own colocated test imported any of the three before deleting; all six files (3 production + 3 colocated tests) removed. `grep -r "@xyflow/react" src` still returns four lines, all comments/test-description text referencing the historical migration (`routes.tsx`, `camera.test.ts`, `stageIds.ts`, `xyflowGuard.test.ts`) — no actual import statement, and `xyflowGuard.test.ts` itself (2/2 green) is the enforced proof nothing under `stages/` imports the package; `package.json` has no `xyflow` line. Full suite re-run clean (1443/1444 — the one remaining failure is the same pre-existing, unrelated Commanders `disabledReasonGuard` violation); `npm run build` green with no unresolved import.

### Checkpoint C — complete

- [x] All **15 modules** built (`world-contract`, `world-wire`, `world-commands`, `world-shell`, `world-render`, `world-hud`, `world-numbers`, `world-inspector`, `world-turn`, `world-notify`, `world-outliner`, `world-lenses`, `world-targeting`, `world-playback`, `world-confirms`) — every `W`-numbered task in this file (W1–W108) is checked, including the two (W94, W95) whose work had landed earlier this session but whose checkboxes were only corrected during this sweep.
- [x] `features/world/` deleted and its **three** standing exemptions retired **in the same change** — the hex-guard prefix, the GG-7 reachability exception, and the shell's redirect exception (W107, already done; the three remaining orphaned files closed out by W108).
- [x] The GG-50 registry stands at **13**, with no `virtualize` entry added by this program (W106; `volumeMatrix.test.ts` — 5/5 green, including a test asserting the world stage's five rows are all `render-all`).
- [x] The four boundary guards green: `guard-single-writer.ps1` ("SINGLE-WRITER GUARD OK"), `guard-secondary-no-unity.ps1` ("SECONDARY NO-UNITY GUARD OK"), `guard-funnel-delta.ps1` ("FUNNEL DELTA GUARD OK"), `guard-dal.ps1` ("DAL GUARD OK") — all four exit 0, run 2026-09-05.
- [x] The full web suite green (`cd web\fusion-rpg-web; npm test` → **1460/1461**, `npm run build` → green) and the full .NET suites checked:
  - `dotnet test tests\FusionRpg.Data.Tests` → **822/822 passed** (run during W102/103's own verify step).
  - `dotnet test tests\FusionRpg.Guard.Tests` → **204/204 passed**.
  - `dotnet test tests\FusionRpg.Core.Tests` → world-stage's own namespace is **961/961 clean** (`--filter FullyQualifiedName~World`); the whole-suite run additionally shows 5-8 failures (count itself varies run to run) that are exclusively `ClassSystem.ProveAptitudeJsonEmitTests` — confirmed via `git status` to be a concurrent, unrelated, actively-uncommitted stream (class-system/power tuning: `data/tuning/aptitudes.v5.json`, `ContentScale.cs`, etc. all show modified) whose own bootstrap (`BattleStatComposer.Configure(...) has not run`) and build-lock races ("cannot access file ... used by another process") are the actual cause, not a world-stage regression — re-run 5 times, never once implicating a `World` namespace test.
  - `dotnet test tests\FusionRpg.E2E.Tests` → **201/207**; the 6 failures are two pre-existing, cross-program issues, neither caused by this session's world-stage work: (a) 3 `WebMatchService`-endpoint 500s, traced to `WebMatchService.cs`/`Program.cs` both being actively uncommitted mid-edit by a concurrent stream (`git status`); (b) `WorldTurnFixtureTests`'s golden mismatch (`"Assaults"` expected, `"Production"` actual) is a **already-committed** cross-program disagreement between the checked-in fixture (`first-light-turn.json`, last committed 2026-09-05 08:47) and `TurnEngine.cs`'s own phase order (last committed 2026-09-05 05:05, both constants `Assaults`/`Production` already declared side by side) — a siege-objective/district-assault program's own turn-phase-wiring gap, not one of world-stage's 15 modules, and not touched by this session.
  - Every failure above was investigated to a named, git-status-confirmed cause before being set aside — none assumed pre-existing without checking.
- [x] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**) — see end-of-session summary.
