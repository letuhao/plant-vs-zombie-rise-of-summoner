# Spec: loam-fe-2 (wave 6)

**Status:** **Draft — Phase 1 (Specify), awaiting owner review.** Module id `loam-fe-2` in the
[loam capability map](../loam-map.md). Depends on `loam-legions`, `loam-structures`, `loam-texture`
(all shipped and tested at the Core/Data layer, none of it reachable by a player). Not yet added to
`loam-map.md`'s module table — pending this spec's approval.

**Why this exists:** a completeness audit of the whole loam program (2026-08-23, `tasks/loam-todo.md`
post-Checkpoint-10 section) found that `loam-fe` (wave 2) never got a second wave. It covers exactly
`loam-turn`'s fields. Everything built after it — legion supply (L26-31), structures (L32-37),
granary/contagion/surge/the Unmade/wardens/prospecting (L38-43) — is fully correct and fully tested at
the Core/Data layer and **entirely unreachable from the actual web client**. This spec is that second
wave.

## Design-gate compliance

Per `docs/DESIGN-GATE.md` §1, this touches: **Economy** (`empire-economy-ssot.md`,
`economy-principles.md`), **UI** (`game-gui-principles.md`, `design/information-architecture.md`,
`fe-game-foundation.md`), **Data** (`data-architecture.md` — DAL boundary), and **Anything at all**
(`software-architecture.md`, `decisions.md`). All read this session; `decisions.md` and `loam-map.md`
were grepped for an existing lock on `WorldEntityDto`/`WorldSectorDto`/wire contracts — none found.

- [x] Subsystems identified.
- [x] Every §1 doc for those subsystems read this session.
- [x] Checked `decisions.md` for a lock — none found on this surface.
- [x] Every factual claim below cites `file:line`.
- [x] Claims verified against code, not comments (see the live-run evidence in `tasks/loam-todo.md`).
- [x] Read the surrounding section of every rule quoted (GG-4's "acting vs. looking" distinction, in
      particular, changed where I put new controls — see Design below).
- [x] Tested, not assumed: the wire gap was confirmed by starting a real server with
      `FUSIONRPG_SIM=1` and reading the actual JSON response, not by reading the DTO source alone.
- [x] Nothing here contradicts a §2 invariant.
- [ ] Not yet propagated: `loam-map.md`'s module table (this spec isn't registered there yet — do
      that on approval, per the module-spec convention, not before).

## Objective

Everything the loam program has built since the ⭐ gate is real, tested, and invisible. A player using
the actual game client cannot see how much loam their legion is carrying, cannot tell a well under
construction from an active one, cannot see a sector is warded, and cannot **do** two of the six
`loam-texture` mechanics at all — wardens and prospecting have no way to be triggered from the client,
only from a raw API call.

**Success looks like:** a player can open `#/world`, select their legion, Sustain a starving sector,
queue a Build order for a well, watch its construction countdown tick down turn by turn, bind a demon
as a warden on threatened ground (with a clear, confirmed, "this is permanent" warning), see their
dowser's revealed sectors highlighted on the map, and read what happened in the turn-playback rail in
plain English — all without touching curl or a test harness.

This is exactly `loam-fe`'s own objective, re-run against everything built since it shipped.

## Design

### 1. The wire — five missing fields (S2's rule, re-applied)

`spec-loam-fe.md` established the rule: derived and raw state the player needs for their decision
gets projected, under the same owner-only/scouted-only fog rule as everything else. Confirmed missing
by reading `WorldDtos.cs` directly and by a live `/api/world/.../state` response:

| Field | Belongs on | Scope | Rule |
|---|---|---|---|
| `carriedLoam` | `WorldEntityDto` | owner-only (it's the viewer's own force — `WorldStateDto.Entities` already carries only the viewer's own, per its own doc comment at `WorldDtos.cs:189`) | What this legion is carrying right now |
| `role` per member | `WorldEntityMemberDto` | owner-only, same as above | Fighter or Bearer — the player's own choice of composition |
| `constructionTurnsRemaining` | `WorldSlotDto` | **anyone who has scouted the slot** — same rule as `structureId` itself (`WorldSlotDto` already carries `structureId` at owner-agnostic visibility; `WorldSlot.ConstructionTurnsRemaining` is documented in Core as "visible on the same terms as the structure itself", `FactionIntel.cs`'s `RememberedSlot.ConstructionTurnsRemaining`) | Whether a structure is active yet |
| `wardenBindingId` | `WorldSectorDto` | **anyone who has scouted the sector** — a warded sector's ward is a property of the ground, not a secret held against the owner's own ally; but see Boundaries: exposing *whose* warden it is is a separate question from exposing *that* one exists | Whether this sector is exempt from fade |
| `neglectedTurns` | `WorldSectorDto` | owner-only if owned; for `Lost`/unowned ground, visible to anyone who has scouted it (it is the countdown to the Unmade, which is public information the moment the ground is visibly abandoned) | How close barren, `Lost` ground is to spawning the Unmade |

`WorldEntityDto`/`WorldSectorDto`/`WorldSlotDto` get these fields; `WorldEndpoints.cs`'s projection
code (wherever it currently maps `WorldEntity → WorldEntityDto` and `WorldSlot → WorldSlotDto`) reads
them straight off Core state — no new calculator, this is pure projection, same discipline as
`spec-loam-fe.md`'s own rule that derived numbers are "computed... never stored."

**The fog rule must be property-tested here too**, the same W22 shape `spec-loam-fe.md` used: nothing
owner-only reaches a faction that does not hold the entity/sector.

### 2. Turn-playback narration — the second thing nobody extended

Found by reading `turnPlayback.ts` directly, not assumed: `classify()` (`turnPlayback.ts:33`)
recognizes exactly the wave-1/2 vocabulary (`arrival:`, `halt:`, `zoc:`, `claim.`, `supply.cut:`,
`attrition:`, `battle`, `calendar`) and falls through to a generic `` `${subject} ${detail}` `` for
everything else. Every event this program has added since — `legion.burn:`, `legion.starved:`,
`legion.topup:`, `legion.runway:`, `loam.overflow:`, `loam.lost:`, `loam.shortfall:`,
`loam.shortfall.unresolved:`, `loam.handicap:`, `unmade.spawned:` — falls through to that default,
which prints the raw engine string (`e-dave-legion-1 legion.runway:17`) straight to the player. This
directly violates GG-23 ("player vocabulary only") and `spec-loam-fe.md`'s own stated rule
("never `componentId`, `StabilityMilli`, or `intensityMilli`" — the same failure mode, on a different
vocabulary that arrived after that rule was written).

`classify`/`describe` gain a `"loam"` `KeyframeKind` and translations for every event above, in player
words:

| Engine detail | Player words |
|---|---|
| `legion.burn:<n>` | "{legion} burns {n} loam, out of supply" |
| `legion.starved:<place>` | "{legion} runs out of loam and is lost" |
| `legion.topup:<n>` | "{faction}'s legions draw {n} loam from the pool" |
| `legion.runway:<turn>` | "{legion} runs dry on turn {turn} at this rate" |
| `loam.overflow:<n>` | "{sector} wastes {n} loam — storage is full" |
| `loam.lost:<sector>` | "{sector} is lost" |
| `loam.shortfall:<n>` | "{sector} fades — the territory is {n} short" |
| `loam.shortfall.unresolved:<n>` | "{faction}'s territory is {n} short and nothing can be done about it this turn" |
| `loam.handicap:<milli>` | "{faction}'s upkeep is adjusted (handicap)" |
| `unmade.spawned:<sector>` | "the Unmade rise at {sector}" |

### 3. Command UI for already-shipped commands — Sustain and Build

`WorldCommandKinds.Sustain` and `.Build` (Core, `WorldCommand.cs`) and their admission/resolvers
(`SustainResolver.cs`, `BuildResolver.cs`) are fully built and tested. **No server or Core change
needed here** — this is purely `WorldPage.tsx` gaining two more `queue*` functions and two more
buttons, in the exact place `queueMove`/`queueClaim` already live (`WorldPage.tsx:333-342`, inside the
`Panel title="Sector"` band-2 layer, next to the existing "March here" / "Claim" buttons) — not a new
panel, not a new route.

- **Sustain**: enabled when a legion is selected, stands at the selected sector, and carries loam > 0.
  Spends up to the legion's full `carriedLoam` (matching `SustainResolver`'s own bound) — no amount
  picker; spending "some" of a legion's carried loam has no acceptance criterion anywhere in
  `spec-loam-legions.md`, and adding one now would be inventing a mechanic this spec does not own.
- **Build**: enabled when a legion is selected, stands at the selected sector, and the sector has an
  empty slot whose `RequiredSlotKind` matches a known structure (well → Rootbed, waystation → Seat,
  granary → Wildland — `StructureCatalog.cs`). **Decided:** the structure picker is an inline dropdown
  next to the Build button, in the same `Panel title="Sector"` band-2 layer — not a separate popover —
  matching the existing dense, single-panel inspector style (GG-26 progressive disclosure: only
  structures whose `RequiredSlotKind` matches an empty slot on the selected sector are offered).

Both follow the shipped "disabled, not hidden" pattern (`WorldPage.tsx:331-332`'s own comment: "an
absent button teaches nothing") and report their own refusal reason via the existing `sendOrders`
notice mechanism (`WorldPage.tsx:145-154`).

### 4. Wardens — the new command, end to end

The bigger gap: no `WorldCommandKinds.Ward` exists, no endpoint calls `RpgStore.BindAsWarden`, and
nothing connects the two even if both existed. Two systems meet here — a **permanent, Data-layer**
demon-contract action (already shipped: `RpgStore.BindAsWarden`) and a **World-layer** effect
(`WorldSector.WardenBindingId`, already shipped, exempts fade). Core must not call `RpgStore` (DAL
boundary, `data-architecture.md` — SQL only inside `FusionRpg.Data`), so the two cannot be one
resolver. The design:

**New Core plumbing** (mirrors `Sustain`/`Build` exactly):
- `WorldCommandKinds.Ward` + `WorldCommand.WardenBindingId` (`string?`) — the caller-supplied opaque id
  (in practice, the demon's `instanceId`; Core never validates it against a real contract — that
  validation already happened one layer down, same as `Build`'s `StructureId` is caller-supplied and
  Core only checks it against the known-structure catalog, not against whether the player can afford
  it).
- Admission (`WorldCommandAdmission.cs`): entity owns the sector, sector has no existing
  `WardenBindingId`, `WardenBindingId` non-empty.
- A `WardResolver.cs` (mirrors `BuildResolver.cs`'s shape), run in `Snapshot` alongside `BuildResolver`,
  sets `sector.WardenBindingId`.

**New Server endpoint**, `POST /api/contracts/bind-warden` (mirrors `ContractEndpoints.cs`'s existing
`/bind` shape exactly, `ContractEndpoints.cs:26-35`), body `{playerId?, instanceId, worldId, sectorId}`:
1. Calls `store.BindAsWarden(pid, instanceId)` first (the irreversible, Soul-priced step). On refusal,
   return that reason and stop — nothing else happens.
2. On success, submits a `Ward` `WorldCommand` to `worldId` for `sectorId` with that `instanceId`.
3. **If step 2 is refused** (sector not owned, already warded, stale world state): report **both**
   results to the client rather than silently losing one — the contract bind stands (it already
   consumed the binding slot, per `empire-economy-ssot.md` §7's cure), the sector is not warded. This
   is a stated, accepted risk (see Boundaries), not a rollback — there is no cross-store transaction
   between SQLite's contract tables and the world-command pipeline, and inventing one is out of scope.

**New Web UI**: a "Bind Warden" button in the Sector inspector (next to Sustain/Build), enabled when
the player owns the sector and it has no `wardenBindingId`. Per **GG-22** (destructive/irreversible
actions confirm and name exactly what is lost) this opens a **band-3 confirm dialog** — not a silent
button click — listing the player's eligible demons (bound, not already a warden — reusing
`useContracts`'s existing data, `contracts.ts:38`) and stating plainly: *"binds {demon} to {sector}
permanently — it can never be released, fielded, or fused again."* A `useBindAsWarden()` hook mirrors
`useBindContract()`'s shape (`contracts.ts:59-61`) exactly.

**Decided:** if the player's Souls balance is below the bind fee, the confirm dialog shows a second,
explicit low-balance warning step before the action fires — not merely a disabled button — since a
warden bind is permanent in a way an ordinary contract re-sign is not, and the stakes justify the
extra beat the ordinary disabled-button pattern skips for reversible actions.

### 5. Prospecting — surfacing a pure query

`Prospecting.Reveal(world, factionId)` is a pure Core function, deliberately not persisted (this
spec's own earlier finding: no new hashed field, no golden move). The wire needs a way to ask it a
question, not a stored field:

- `WorldStateDto` gains `IReadOnlyList<string> ProspectedSectorIds` — computed at projection time by
  calling `Prospecting.Reveal(world, viewerFactionId)`, same "computed, never stored" discipline as
  §1's derived economy numbers.
- Web: sectors in that list get a distinct highlight on the map (a new visual treatment, not reusing
  "habitable" — a prospected barren sector and a prospected owned sector are different things,
  and `spec-loam-fe.md`'s "fading and barren must not look alike" logic applies here too: a *revealed*
  sector must not look like a *scouted* one, or the dowser bought nothing visible).
- Setting the `dowse` stance itself needs no new command — `Stance` is already a generic
  `WorldCommandKinds.Stance` field (`WorldCommand.cs:22`) — just a UI affordance to set it (a stance
  picker next to the existing March/Claim controls, offering `march` / `hold` / `scout` / `dowse`).

## What stays cut

Matching `loam-texture.md`'s own discipline of naming what is deliberately absent rather than silently
missing it:

- **No amount picker for Sustain.** Full-carry-or-nothing, per §3.
- **No rollback for a Ward whose world-command leg fails.** Accepted risk, per §4.
- **No new top-level route, no new panel type.** Every control above lives in the existing
  `Panel title="Sector"` band-2 layer or a band-3 confirm dialog — GG-1.
- **Whose warden a sector belongs to is not exposed to other factions.** `wardenBindingId`'s *presence*
  is scouted-visible (§1); the demon identity behind it stays owner-only — showing it would leak
  roster information across the fog boundary this program has held since wave 1.

## Commands

```powershell
cd web/fusion-rpg-web; npm test; npm run lint; npm run build
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~Contract
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
.\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/World/Turn/WorldCommand.cs            → WorldCommandKinds.Ward, WardenBindingId field
src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs    → Ward admission rule
src/FusionRpg.Core/World/Movement/WardResolver.cs (new)   → sets WorldSector.WardenBindingId
src/FusionRpg.Core/World/Turn/TurnEngine.cs               → wires WardResolver into Snapshot
src/FusionRpg.Contracts/WorldDtos.cs                      → the five §1 fields, ProspectedSectorIds
src/FusionRpg.Server/WorldEndpoints.cs                    → projection for all of the above
src/FusionRpg.Server/ContractEndpoints.cs                 → POST /bind-warden
web/fusion-rpg-web/src/lib/bus/contracts.ts               → useBindAsWarden()
web/fusion-rpg-web/src/features/world/WorldPage.tsx       → queueSustain/queueBuild/queueWard, stance picker
web/fusion-rpg-web/src/features/world/SectorPanel.tsx     → carriedLoam/role/constructionTurnsRemaining/wardenBindingId readout
web/fusion-rpg-web/src/features/world/turnPlayback.ts     → the "loam" KeyframeKind and translations
web/fusion-rpg-web/src/features/world/*.test.tsx          → new fixtures for every field/control above
web/.../world.fixture.json                                → regenerated with the new fields
tests/FusionRpg.Core.Tests/World/Movement/WardResolverTests.cs (new)
tests/FusionRpg.Data.Tests/ContractEndpointTests.cs (or similar, new) → bind-warden's two-step behavior
tests/FusionRpg.E2E.Tests/World*.cs                       → fog property tests over every new field
```

## Code style

Follow `WorldPage.tsx`'s existing conventions exactly — `Button ... disabled={...} title={...}` for
the "why disabled" pattern already established at `WorldPage.tsx:333-336`; player-facing copy in
player words, never `WardenBindingId`/`carriedLoam`/`constructionTurnsRemaining` on a rendered surface.

## Testing strategy

**Fog, as a property** — every new field, every projection, every faction, the same W22 shape
`spec-loam-fe.md` used, not a spot check.

**The two-step Ward endpoint** — both orderings: world-command leg succeeds (normal case) and fails
(the accepted-risk case), asserting the response names both outcomes rather than picking one.

**Turn-playback translation** — one test per new `KeyframeKind` entry, asserting player words appear
and the raw engine string does not.

**Fixture-driven** — the FE builds against `world.fixture.json`, regenerated with
`FUSIONRPG_BLESS_WORLD_FIXTURE=1`, per `spec-loam-fe.md`'s own precedent.

## Boundaries

- **Always:** owner-only/scouted-only gating asserted as a property on every new field; disabled
  (never hidden) controls with a stated reason; player vocabulary on every rendered surface and every
  playback line; GG-22 confirmation before Ward.
- **Ask first:** exposing which specific demon backs a `wardenBindingId` to non-owners (currently
  designed as never — see What stays cut); any rollback mechanism for the Ward endpoint's two-step
  failure case (currently designed as accepted risk, not rollback).
- **Never:** a new top-level route or panel type; deriving loam numbers in TypeScript (the server
  projects what Core computes); a silent Ward failure with no visible result (GG-16).

## Success criteria

1. `carriedLoam`, member `role`, `constructionTurnsRemaining`, `wardenBindingId`, and `neglectedTurns`
   are on the wire, fog-gated, and rendered in the Sector inspector.
2. A player can Sustain, Build, and Bind a Warden entirely from `#/world`, with no raw API call.
3. A Ward action shows a confirm dialog naming the exact demon and sector before it fires.
4. The turn-playback rail never prints a raw engine detail string for any loam/legion/Unmade event —
   every one translates to player words.
5. A dowser's revealed sectors render with their own distinct treatment, never confused with
   "habitable" or "scouted."

## Decided (2026-08-23, owner)

- **Ward's low-Souls case gets a second, explicit confirmation step** inside the band-3 dialog, not
  just a disabled button — the permanence of the action justifies the extra beat.
- **The Build structure picker is an inline dropdown** in the Sector inspector panel, not a separate
  popover.

No open questions remain.
