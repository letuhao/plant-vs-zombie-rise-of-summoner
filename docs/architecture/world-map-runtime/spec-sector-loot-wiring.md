# Spec: `sector-loot-wiring`

**Module id:** `sector-loot-wiring` · **Program:** [world-map-runtime](../world-map-runtime-map.md) ·
**Filed via [drop-tables-map.md](../drop-tables-map.md)** (cross-program initiative) · **Build order:**
3 of 4 in the drop-tables map (moved back one slot — `rate-authoring` now builds before either
gameplay-mode module, per the revised build order)
**Depends on:** `rate-floor` (item, soft — see below) · `rate-authoring` (item, soft — new tables built
by this module should use it for any ultra-rare entry) · the shipped `WorldSectorLootSource` (already
built, item-owned, `src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs`)
**Source:** `docs/architecture/drop-tables-ideal.md` §3 W3, §4 R1(b), decided D1 (2026-09-07): base-
defense and world-map are in scope, not deferred.

## Objective

Close a **wiring gap, not a real gap**: `WorldSectorLootSource.TryResolve` is built, tested, and
correct — it resolves a sector-clear event to a real `LootSourceRow` — but has **zero production
callers** (confirmed: the only reference outside the file itself is
`tests/FusionRpg.Core.Tests/Items/WorldSectorLootSourceTests.cs`). This module (a) finds and wires the
real call site where a sector actually transitions to "cleared," and (b) closes the second, smaller gap
the resolver's own doc comment already names: it points every sector, regardless of type, at the same
single table (`drop.world.sector-clear`) — this module splits that into per-sector-type tables so world
map's own gameplay mode stops sharing one undifferentiated pool.

## ⚠ Corrected in review — the original "sector cleared" framing does not match the shipped code

An earlier draft of this spec assumed a siege win **reduces a sector's `DangerBand`**, and that the
loot hook belongs wherever that reduction happens. **Verified false by direct trace:** `DangerBand` is
**never mutated anywhere in `src/`** after world generation — every reference outside generation code
(`upkeep`, intel snapshots, DTOs) is a read. There is no "siege won → danger drops" mechanism in this
codebase today; `WorldSectorLootSource`'s own `dangerBand` parameter should be read as the sector's
fixed, authored danger rating (like a monster level), never something that changes when a sector is
cleared.

**What actually transitions after a fight is sector OWNERSHIP, not danger — and the real call site is
now confirmed:** `src/FusionRpg.Core/World/Movement/ClaimResolver.cs:79`
(`OwnerFactionId = command.CommanderId`, inside `ClaimResolver.Run`), reached from
`TurnEngine.cs:379`. `ClaimResolver.Run` requires the claiming entity to be standing in the sector, not
routed, uncontested, **and every slot's guard already `Cleared`** (`ClaimResolver.cs:66-71`) — it is
gated on an explicit player `Claim` world-command, not a direct `BattleOutcome`/`SiegeOutcomeKind`
callback. `TurnEngine.cs` runs `Phases.Assaults` (`:259`) before `Phases.Snapshot` (`:379`) in the same
turn, so **a same-turn assault that clears the last guard, plus a same-turn claim order, is what
actually flips a sector** — the two are decoupled by a phase boundary and a real command, not one
continuous "clear" event.

**This correction happens to resolve a real architectural conflict a design-gate review found, not
just a factual one.** `docs/DESIGN-GATE.md`'s "Battle / turns" row states *"Battle consumes FA10 only;
it never grants and never calls `OnEvent`."* The original draft's candidate call sites
(`BattleSeam.cs`, `SiegePhase.cs`) are Battle-adjacent and would have put a loot grant inside exactly
the layer that rule forbids from granting anything. `ClaimResolver.cs` lives under `World/Movement/`,
outside Battle entirely, resolving a **world-command**, not a battle result — the corrected call site
is the architecturally correct one, not merely the factually confirmed one.

## Design

### Part A — wire the existing resolver to the real caller

```text
ClaimResolver.Run (World/Movement/ClaimResolver.cs:79), after OwnerFactionId is assigned
    -> WorldSectorLootSource.TryResolve(sectorId, sector's own authored dangerBand, sectorTypeId, tuning, out source)
    -> if Ok: LootPipeline.Resolve(request, view, ...) exactly as party-dungeon's own
       DelveLoot.RollRoom does it (spec-dungeon-loot.md's own code-style block is the template —
       this module is world-map's first host of LootPipeline, the same "first production host"
       framing that spec used for party-dungeon)
    -> if refused (drop.sector-band-safe): no drop, no error surfaced to the player — band 0 is
       safe ground by design (WorldSectorLootSource.cs's own doc comment)
```

**No change to `WorldSectorLootSource.cs`'s own resolution logic** — only the new `sectorTypeId`
parameter (Part B). This module's Part A is: call it from `ClaimResolver.Run` right after the
ownership assignment at `:79`, construct the `LootRequest`/`LootContentView` the same way
`DelveLoot.RollRoom` already demonstrates, and thread the result into whatever reward-presentation
surface world-map already has for a claim (or build a minimal one if none exists).

**Design-gate reading this citation now requires, not yet done this session:** `decisions.md` (the
"Anything at all" row) and the World-map row's own mandatory reads
(`world-map-program.md`, `world-map-runtime-ideal.md`, `spec-world-map-runtime.md`,
`spec-world-map-gaps.md`, `world-map-runtime-map.md`) — read before implementation begins, not assumed
satisfied by this spec's own claim-resolver trace.

### Part B — split the one shared table into per-sector-type tables

Today `SectorClearTableId = "drop.world.sector-clear"` is a single constant, used for every sector
regardless of `SectorTypeCatalog`'s own type vocabulary. This is the same "flat, undifferentiated
pool" shape `drop-tables-ideal.md` names as the whole point of this initiative — a `boss-lair` sector
and a `resource-node` sector should not draw from the same table at the same rates.

```csharp
// WorldSectorLootSource.cs — the ONE new parameter this module adds
public static AtomRejection TryResolve(
    string sectorId, int dangerBand, string sectorTypeId, PowerTuning tuning, out LootSourceRow? source)
{
    // ...unchanged validation...
    var tableId = SectorClearTableIdFor(sectorTypeId); // new: "drop.world.sector-clear.{sectorTypeId}"
    source = new LootSourceRow(SourceKind, sectorId, tableId, contentLevel);
    return AtomRejection.Ok;
}
```

Every real `SectorTypeCatalog` type (read from the shipped catalog, never hand-listed — matching this
program's own "read the real corpus, never transcribe" discipline already established for
`legal_role_frame_pairs()`-style readers elsewhere this session) gets its own authored
`drop.world.sector-clear.{type}` table under `data/seed/loot/`, each free to set its own weights,
`affix_channel`, and — once module 1 ships — reference the shared `rate-floor` validation for any
entry that wants to sit at the system's own 0.0001% floor (e.g. a rare-sector-type-exclusive reward).

**This is a genuine content-authoring pass, not just a code change**: one table per real sector type,
each with a distinct pool/weights — the actual differentiation the user's idea asked for, not merely a
renamed single table.

## Data shape

| Item | Change |
|---|---|
| `WorldSectorLootSource.TryResolve` | **+1 parameter** (`sectorTypeId`); table id becomes per-type |
| `data/seed/loot/drop.world.sector-clear.{type}.json` | **new**, one per real `SectorTypeCatalog` entry |
| `ClaimResolver.Run` (`World/Movement/ClaimResolver.cs:79`) | **new caller** added right after `OwnerFactionId` assignment |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WorldSectorLootSource"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~World.Turn"   # world goldens, must stay byte-identical for every path that doesn't touch a sector clear
```

## Project structure

```text
src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs   EDIT — +sectorTypeId param, per-type table id
src/FusionRpg.Core/World/Movement/ClaimResolver.cs        EDIT — the new call, right after :79's OwnerFactionId assignment
data/seed/loot/drop.world.sector-clear.{type}.json        new — one per real sector type
tests/FusionRpg.Core.Tests/Items/WorldSectorLootSourceTests.cs   EDIT — new param covered
tests/FusionRpg.Core.Tests/World/Movement/ClaimResolverLootTests.cs   new — the real call site's own integration test
```

## Code style

Matches `WorldSectorLootSource.cs`'s own established discipline exactly: no `FusionRpg.Core.World`
dependency (takes `sectorTypeId` as a plain `string`, same as `dangerBand` is a plain `int` today — the
loot lane never loads a world to price a sector); refuse by name, never floor or default
(`drop.sector-band-safe` stays exactly as shipped).

## Testing strategy

| Test | Asserts |
|---|---|
| `ClaimResolver_Run_invokes_TryResolve_after_assigning_ownership` | `:79`'s new call fires exactly once per successful claim, never on a rejected/contested claim |
| `a_claim_that_fails_ClaimResolvers_own_guard_gate_grants_no_loot` | the existing "every slot's guard already Cleared" precondition (`:66-71`) is untouched — a contested or ungeared claim attempt still refuses before reaching the new call |
| `each_real_sector_type_resolves_to_its_own_table_id` | `SectorTypeCatalog.All` × `TryResolve` → distinct table ids, no two types sharing one |
| `an_unauthored_sector_type_is_refused_by_name_not_defaulted_to_the_old_shared_table` | a sector type with no authored per-type table fails import/resolution loudly, never silently falls back |
| `band_zero_still_refuses_exactly_as_before` | Part B changes nothing about the existing `drop.sector-band-safe` refusal path |
| `every_world_golden_stays_byte_identical` | wiring a new caller must not move any existing world/battle/expedition golden for a run that never clears a sector |

## Boundaries

**Always:** read `SectorTypeCatalog` for the real type list, never hand-enumerate it; keep
`WorldSectorLootSource` free of a `FusionRpg.Core.World` dependency; refuse an unauthored sector type by
name.

**Ask first:** what the player-facing reward-reveal surface for a sector clear should look like, if
none already exists — this module's job is the resolution and content, not a new UI unless one is
missing entirely (in which case, ask before building one, since UI surfaces are `game-gui-principles.md`
territory, outside this module's own boundary).

**Never:** revert to one shared table across all sector types once split — a future content-authoring
shortcut ("just point every type back at the default table for now") would silently re-introduce the
exact undifferentiated-pool problem this module exists to close.

## Success criteria

- [ ] `ClaimResolver.Run` (`World/Movement/ClaimResolver.cs:79`) calls
      `WorldSectorLootSource.TryResolve` for the first time in production, right after ownership
      assignment, never before the existing guard-cleared precondition passes.
- [ ] Every real `SectorTypeCatalog` entry has its own authored table; none share `drop.world.sector-
      clear`'s old single id. **Distinct ids are not sufficient on their own** — found in review: a
      minimal-compliance build could ship byte-identical table content under different filenames and
      pass an id-only check. At least one substantive axis (entry pool, weights, or `affix_channel`)
      must differ between at least two real sector types, verified by a test that diffs table content,
      not just table ids — otherwise this module ships zero player-perceptible change despite passing
      every other criterion.
- [ ] `drop.sector-band-safe`'s refusal behavior is unchanged.
- [ ] Every pre-existing world/battle/expedition golden stays byte-identical.
