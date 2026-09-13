# Spec: `wonder-build-flow`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this session.
Module id `wonder-build-flow`, row 4 (final) of the
[loam-relics-and-wonders map](../loam-relics-and-wonders-map.md) (wave 2, depends internally on
`relic-item-kind` + `wonder-structure`, both wave 1 — **specced, not yet built**, confirmed fresh this
session by reading `StructureCatalog.cs` in full and finding no `WonderScope`/`WonderRarity`/
`RelicCost` field anywhere on `StructureDef` today; externally hard-blocked on `scoped-inventory-
hierarchy` — **specced, not yet built**, confirmed by the same read against `rpg_item`/`rpg_item_stock`
and the total absence of `rpg_world_entity_cargo`/`rpg_world_sector_storage` anywhere in
`src/FusionRpg.Data`). Ideal: [loam-relics-and-wonders-ideal.md](../loam-relics-and-wonders-ideal.md)
§The owner's third-pass framing (relic vocabulary), §The shape (Wonders — a Structure, not a new
subsystem, §"Relic cost — deliberately not this module's field"). Decisions:
[decisions.md](../decisions.md) "Loam relics and wonders SSOT (2026-09-13)" and "Scoped inventory
hierarchy SSOT (2026-09-13)" (quoted in full below, both re-opened this session).

## Objective

The actual construction verb: a commander spends one or more owned, unassigned relic instances —
reachable either aboard the issuing legion's own cargo or already sitting in the target sector's
storage, once `scoped-inventory-hierarchy` ships either — plus `WorldSector.RubbleStock`/
`IronworkStock`, to raise a Wonder on a compatible, empty `WorldSlot`, subject to `WonderPolicy
.ExistenceCapFor(WonderScope, WonderRarity)`. This module designs three things module 1 and module 2
each deliberately left open for it: (1) the relic-to-Wonder **recipe shape** — what "consume one relic
instance matching some kind/rarity" actually is as data and as a verb parameter (`wonder-structure`
§Design 8); (2) the **existence-cap scan** — "how many Unique-rarity Wonders of this `WonderScope`
already exist for this scope-unit" against real `WorldState`/`StructureCatalog` data, the half
`WonderPolicy.ExistenceCapFor` itself does not implement (`wonder-structure` §Design 6's own
"who calls this... named, not built here"); (3) the **landing site** — which existing peacetime
construction machinery a Wonder actually resolves through, verified fresh this session rather than
assumed from the ideal doc's own siege-construction citation.

Success looks like: a commander files a `build` order naming a Wonder-shaped `StructureId` and exactly
`RelicCost` relic instance ids; at turn commit, if every named relic is still owned, unassigned and
reachable, the sector's `RubbleStock`/`IronworkStock` affordable, the slot compatible and empty, and the
scope-unit's existing-Wonder count under `WonderPolicy.ExistenceCapFor`'s cap, the Wonder occupies the
slot, the material stocks are debited, and every named relic is deleted from wherever it sat and its
`rpg_item` row marked spent — all inside the **one** SQL transaction the turn commit already opens; if
any check fails, nothing moves, no relic is touched, and the order drops with a named reason like every
other `build` order failure already does.

## Locked anchors

- **The `BuildResolver` materials-afford-and-spend fix (§Design 6) is a pre-existing, Wonder-independent
  bug fix, independently shippable — not gated on this module's external `scoped-inventory-hierarchy`
  block.** `BuildResolver.Run` has never once read or spent `ConstructRubbleCost`/`ConstructIronworkCost`
  for **any** structure, Wonder or not, even though every field it needs already exists and a pure,
  ready-made gate (`ConstructionCost.CanAffordBuilt`/`SpendBuilt`) already implements the check-then-spend
  shape (§What already exists, Wiring gap table). Per the External dependency status table, this call
  site (§Design 6) needs neither a relic nor `scoped-inventory-hierarchy` — it is pure `WorldState`/
  `StructureDef` logic. **This fix should land and be tested the moment this module's Core-side work
  builds, regardless of whether the relic/Wonder-specific work in the same module ships at the same
  time or is deferred** — do not let it wait on the external block by association just because it lives
  in the same file/module boundary as the relic-spend work that does.
- **Every RPG feature lives in the RPG layer.** This module touches only `FusionRpg.Core.World`
  (`BuildResolver`, `WorldCommand`, a new `WonderExistenceScan`) and `FusionRpg.Data` (a new commit-time
  gate and spend function) — no PvZ/Unity write of any kind, matching every sibling spec in this
  program.
- **`Sector`/`Empire` scope only, `World`/`Multiverse` refused upstream.** `wonder-structure`
  `StructureCatalog.Validate` already refuses any `StructureDef` naming `World`/`Multiverse`
  (`spec-wonder-structure.md` §Design 5) — this module never needs to special-case them; by the time a
  row reaches `BuildResolver`, its `WonderScope` is provably `Sector` or `Empire`.
- **The existence cap is never a hard-coded `1`.** Every comparison in this module reads
  `WonderPolicy.ExistenceCapFor(scope, rarity)` — `Common` always returns `long.MaxValue`
  (`spec-wonder-structure.md` §Design 6), so this module's own scan is a no-op cost for the common case
  and only does real work for `Unique`-rarity rows.
- **`WonderPolicy.ExistenceCapFor(WonderScope, WonderRarity)` counts by scope+rarity, not by
  structure id — inherited, not this module's call.** The signature module 2 shipped has no
  `structureId` parameter (`spec-wonder-structure.md` §Design 6, quoted in full below) — the cap is "how
  many *any* Unique-rarity Wonders of this scope already exist in this scope-unit," not "how many of
  *this specific* Wonder row" (Civ5's National Wonder is the latter shape; this program's locked cap is
  the former). This module implements the scan against the signature it was handed; a per-Wonder-id cap
  would need a different `WonderPolicy` signature, not a workaround here.
- **Relics are rolled items with no rarity/tier axis today — verified fresh, not assumed from the
  ideal doc's summary.** `relic-item-kind`'s own `KindSpec` (§Design 1) lists `optional: COMMON_FIELDS |
  { "theme", "themeKey", "acquisition", "fixedAtoms" }` — **no `tier`, no `rarity`.** Its own §Design 5
  is titled *"Relic rarity/tier — deliberately NOT the 10-rung item ladder"* and states a coarse
  "how special" axis is *"an open content question for whoever authors the first relic anchors, not
  decided by this module."* This is a **correction to how this module's own assignment brief described
  relics** ("optional theme/themeKey/tier fields") — `tier` is proposed-but-unregistered, not a shipped
  field. See §The recipe-shape decision below for why this drives the recipe to be generic in v1.
- **Move, never copy, one transaction — the same discipline `corpse-cache`/`cargo-transfer` already
  proved.** A consumed relic is deleted from its overlay row and its `rpg_item` row marked spent exactly
  once, in the same SQL transaction the turn commit already opens for the whole turn's diff — never a
  second, later transaction. See §The transaction and atomicity design.
- **`ConstructionCost.CanAffordBuilt`/`SpendBuilt` are reused as pure functions, not reimplemented.**
  Verified fresh this session (`ConstructionActions.cs:295-320`): both take `(long sectorRubble, long
  sectorIronwork, StructureDef def)` with **no board/actor/siege dependency in the signature** — they
  are already engine-agnostic and callable directly from `BuildResolver.Run`. See §Where the Wonder
  actually lands.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `BuildResolver.Run` — the real, live, **peacetime** consumer of `WorldCommandKinds.Build`, resolving in the `Snapshot` phase, spending the issuing legion's own `CarriedLoam` against `structure.Cost` | `src/FusionRpg.Core/World/Movement/BuildResolver.cs:1-163`, read in full this session; the spend at `:101,115` |
| **`BuildResolver.Run` never reads `ConstructRubbleCost`/`ConstructIronworkCost`, and never touches `WorldSector.RubbleStock`/`IronworkStock`, anywhere in the file.** Confirmed by a full read, not a grep miss — the entire cost gate is `entity.CarriedLoam < structure.Cost` (`:101`) | `BuildResolver.cs:1-163` (full file); no occurrence of `RubbleStock`/`IronworkStock`/`ConstructRubbleCost`/`ConstructIronworkCost` anywhere in it |
| `ConstructionCost.CanAffordBuilt`/`SpendBuilt` — the **siege-time** consumer of the same two fields, a pure, board-agnostic gate: `(long sectorRubble, long sectorIronwork, StructureDef def) → bool` / `→ (long, long)`, throwing (never clamping) on an unaffordable spend | `src/FusionRpg.Core/Battle/Siege/ConstructionActions.cs:295-320`, read in full this session |
| `ConstructionActivation.Fire` — the **actual** siege-only machinery in the same file: fires a `structure.place` atom for one actor at one board cell via `BattleEffectHost`/`Bag.OnEvent`, gated on the actor already holding a granted container from `BattleRunState.BindContainers` | `ConstructionActions.cs:226-281`, read in full this session — genuinely board/actor-shaped, no world-map analog |
| `WorldCommandKinds.Build`'s own doc comment names only `CarriedLoam` as the spend — corroborating the code, not contradicting it | `src/FusionRpg.Core/World/Turn/WorldCommand.cs:30-34` |
| `WorldCommandAdmission` — "the cheap gate at submit time," explicitly **not** legality-at-resolution; `Build`'s own admission arm checks only `EntityId`/`SectorId`/`SlotIndex`/`StructureId` shape, nothing DB-resident | `src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs:1-11` (doctrine), `:103-112` (Build arm) |
| `WorldCommand.WardenId`'s own precedent: **"opaque to Core: nothing here validates it against a creature roster, the same way `StructureId` is validated only inside the `build` admission arm, not generically"** — the exact shape a `RelicInstanceIds` field needs | `WorldCommand.cs:128-134` |
| `TurnEngine`'s `Snapshot` phase order: `ClaimResolver` → `BuildResolver` → `RaiseResolver` → `DevelopResolver` → `WardenResolver`, all against the same in-progress `WorldState` | `src/FusionRpg.Core/World/Turn/TurnEngine.cs:377-399` |
| `TurnReportEntry(Phase, Kind, Subject, Detail, SectorId?, Audience?)`; `BuildResolver`'s own accepted-build line is `report.Add(phase, TurnReportKinds.Event, command.CommandId, $"build.started:{structureId}", sectorId)` | `src/FusionRpg.Core/World/Turn/TurnReport.cs:30-31`; `BuildResolver.cs:132-133` |
| `TurnReport.Add`/`.BeginPhase` are **`internal`** to `FusionRpg.Core`, not public — `FusionRpg.Data` cannot append a line to a Core-built report after the fact | `TurnReport.cs:90-93` |
| `CommitWorldTurn`'s transaction boundary: one `tx` opened at `:510`, commands loaded at `:530`, `TurnEngine.Step` called at `:531`, `DiffWorldGraphUnlocked` at `:537` — all inside the same `tx`, matching every sibling program's own hook-site precedent (`sector-storage`'s capture hook, `cargo-fate`'s pre-delete hook) | `src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs:484-537`, read in full this session |
| `WorldSector.OwnerFactionId` (nullable `string?`), `.RubbleStock`/`.IronworkStock` (`long`), `.Slots` (`IReadOnlyList<WorldSlot>`); `WorldSlot.StructureId`/`.ConstructionTurnsRemaining` — the exact fields `LoamPhases.EffectiveCapacity`/`SectorItemCapacity.EffectiveCapacity` already gate on for their own additive scans | `src/FusionRpg.Core/World/WorldState.cs:96-126` (`WorldSlot`), `:148-210` (`WorldSector`) |
| `ListItemsByPlayer(playerId)` — the armoury listing, filtered `disposition = 'owned'`; `Disposition` is a documented free-form string (`'owned'`/`'salvaged'`/`'transferred'`/`'destroyed'`), not a closed C# enum | `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs:639-657` (query), `:72-73` (field), `:636-637` (doc comment naming the known values) |
| `relic-item-kind`'s own Interface row: a relic mints to an `rpg_item` row with `origin_kind = "drop"`, and this module is explicitly named as the reader, "once `scoped-inventory-hierarchy` lands a sector/legion-scoped reachability layer on top" | `spec-relic-item-kind.md` §Interface exposed to dependents |
| `wonder-structure`'s own `WonderPolicy.ExistenceCapFor(WonderScope, WonderRarity)` and its own doc comment: *"who calls this, and when — named, not built here... `wonder-build-flow` (module 4) is the sole caller"* | `spec-wonder-structure.md` §Design 6 |
| `legion-cargo`/`sector-storage`/`cargo-transfer`'s own schemas and verbs (`rpg_world_entity_cargo`, `rpg_world_sector_storage`, `LoadCargoUnlocked`/`UnloadCargoUnlocked`/`TransferCargoUnlocked`, `DepositUnlocked`/`WithdrawUnlocked`/`SameFaction`) — specced, not built | `spec-legion-cargo.md` §Design 2-5; `spec-sector-storage.md` §Design 4-5; `spec-cargo-transfer.md` §Design 1-3 |
| `decisions.md`'s own two locked rows for this program and its external dependency, both reconfirmed this session | `decisions.md:47-48`, quoted in full in §External dependency status |

### Wiring gap

| Gap | The inert/underused line |
|---|---|
| **`BuildResolver.Run` — the real peacetime consumer of `Build` — has never once read or spent `ConstructRubbleCost`/`ConstructIronworkCost`, even though every field it needs already exists on `StructureDef` and on `WorldSector`, and a pure, ready-made gate (`ConstructionCost.CanAffordBuilt`/`SpendBuilt`) already implements the exact check-then-spend shape.** This is a wiring gap, not a wall — the ideal doc's own claim that the "basic material" cost is "RESOLVED, no new engine plumbing" is true only for the **siege** consumer; the **world-map** consumer (the one a Sector/Empire Wonder actually needs) has simply never been given the call. See §Where the Wonder actually lands | `BuildResolver.cs:101` (the single cost check, `CarriedLoam` only); `ConstructionCost.CanAffordBuilt`/`SpendBuilt` (`ConstructionActions.cs:295-320`) sit unused by any world-map caller today |

### Real gap

| Gap | What this module builds |
|---|---|
| No `WorldCommand` field carries which relic instance(s) a `build` order spends | A new, opaque-to-Core `RelicInstanceIds` field, mirroring `WardenId`'s precedent — §Design 1 |
| No `StructureDef` field expresses "how many relics this Wonder needs" | A new `RelicCost` field (only meaningful when `WonderScope` is set) — §Design 2 |
| No admission-time or resolution-time check exists for relic count, and Core has no way at all to check relic *ownership* (that is DB-resident state Core never reads) | §Design 3 (admission), §Design 4 (a Data-side pre-check before `TurnEngine.Step` even runs) |
| No existence-cap scan implements "how many Unique-rarity Wonders of this scope already exist for this scope-unit" against live `WorldState`/`StructureCatalog` data | A new `WonderExistenceScan`, mirroring `LoamPhases.EffectiveCapacity`/`SectorItemCapacity.EffectiveCapacity`'s own gating shape — §Design 5 |
| No function spends a relic exactly once, atomically with the Wonder's construction | A new Data-side spend step, run in the same `tx` as the turn's own diff commit, keyed off `BuildResolver`'s own `"build.started:"` report line — §Design 6 |

## The recipe-shape decision, with evidence

**The question, as the assignment brief posed it:** does a Wonder's `StructureDef`/seed row need to
*name* which relic anchor(s) satisfy it (a `RequiredRelicId`/`RequiredRelicTag`-shaped field), or is it
fully generic ("any relic instance, no matching required")?

**Decision: fully generic in v1 — a Wonder names only a *count* (`RelicCost`), never a required relic
identity, tag, or theme.** Evidence, checked fresh this session rather than assumed:

1. **Relic anchors carry no rarity/tier axis today.** `relic-item-kind`'s own `KindSpec` (§Design 1)
   lists exactly four optional fields — `theme`, `themeKey`, `acquisition`, `fixedAtoms` — and its own
   §Design 5 states plainly that a "how special" axis is *"an open content question for whoever authors
   the first relic anchors, not decided by this module."* A `RequiredRelicRarity`-shaped match has
   nothing to match against; building one now would mean inventing the very axis `relic-item-kind`
   explicitly declined to ship.
2. **`theme`/`themeKey` exist, but as an authoring/flavor grouping, not a designed matching
   mechanism.** Nothing in `relic-item-kind`'s spec treats `themeKey` as a machine-readable "kind" a
   recipe could key off — its own Open question #2 (carried from the ideal doc) is still asking *"how
   many distinct relic identities... versus treating every relic as interchangeable at the same
   rarity"* — i.e., whether relics are even meant to have distinguishable identities yet is itself
   unresolved upstream. Building a `RequiredRelicTag` field against an axis whose own authoring
   discipline is still undecided would be designing on sand.
3. **This repo's own precedent, from a sibling module in the same wave, for refusing an unrequested
   gate.** `sector-storage` (`spec-sector-storage.md` §Design 2) explicitly declined to add a weight
   gate to sector storage — *"Absent an explicit ask for weight here, adding one would be inventing a
   requirement, not building one."* No owner decision anywhere in this program's ideal/map/decisions
   trail asks for relic *matching* — only "spend a relic" (singular, generic) — so the same discipline
   applies here: ship the smallest shape the evidence supports, name the richer shape as a reserved
   extension.
4. **A count-only recipe composes cleanly with the "verb parameter, not a catalog field" framing
   `wonder-structure` §Design 8 already locked.** That section rejected a `long RelicCost` field shaped
   like `Cost`/`ConstructRubbleCost` (a *fungible* stock magnitude) because a relic is not a stock — but
   it did not reject a **count** of distinct instances to select and spend, which is exactly what "a
   verb parameter" describes: the *requirement* (how many) is small, authored, catalog-shaped data; the
   *selection* (which specific instances) is a per-call parameter the player supplies, never derived
   from a catalog rule. §Design 1-2 below implement precisely that split.

**Reserved, not built:** a future wave that wants "this Wonder needs a relic of theme X" needs two
things neither exists today — a real identity/tag axis on relic anchors (real gap, `relic-item-kind`'s
own, not this module's to force) and a matching field here. Named so a future reader does not re-derive
why v1 is generic; not designed further.

## Design

### 1. `WorldCommand.RelicInstanceIds` — a new, opaque-to-Core field

```csharp
// WorldCommand.cs — new field, following WardenId's own precedent verbatim.
/// <summary>Which relic instance(s) a `build` order spends when the named structure is a Wonder
/// (`StructureDef.RelicCost > 0`, loam-relics-and-wonders `wonder-build-flow`). Opaque to Core: this
/// field carries rpg_item.instance_id strings, but Core never reads FusionRpg.Data, so it cannot
/// verify ownership or reachability here — only the STRUCTURAL count (§Design 3). Ownership and
/// reachability are verified in FusionRpg.Data before the command ever reaches TurnEngine.Step
/// (§Design 4). Empty for every non-Wonder build order, unaffected.</summary>
public IReadOnlyList<string> RelicInstanceIds { get; init; } = Array.Empty<string>();
```

**Reuses the existing `build` command kind — does not fork a new `build-wonder` kind.** A Wonder's
placement shares every structural field an ordinary structure's placement already needs (`EntityId`,
`SectorId`, `SlotIndex`, `StructureId`); the only new payload is this one field, empty and inert for
every non-Wonder row. Forking a parallel command kind for a shape that differs by one optional field
would be the "second parallel path" this repo's SOLID discipline already bans — extend the existing
gate.

### 2. `StructureDef.RelicCost` — a new, paired-with-`WonderScope` field

```csharp
// StructureCatalog.cs — new field on StructureDef, following ContainerId's/WonderScope's own
// nullable-optional-facet convention. Deliberately NOT the same shape as Cost/ConstructRubbleCost
// (a fungible stock magnitude) -- see §The recipe-shape decision point 4.
/// <summary>How many DISTINCT relic instances a Wonder-tier row (WonderScope set) requires to
/// construct — a count of items to select and consume, never a magnitude contentScale touches
/// (loam-relics-and-wonders `wonder-build-flow`). Zero for every non-Wonder structure. Validate
/// (below) enforces the pairing: WonderScope set implies RelicCost >= 1 (a Wonder must cost at
/// least one relic — the whole point of the mechanic); WonderScope null implies RelicCost == 0.</summary>
public long RelicCost { get; init; }
```

`long`, matching `Cost`/`ConstructRubbleCost`/`ConstructIronworkCost`'s own type on the same record —
homogeneous typing across every "spend N of X to build this" field on one row, even though realistic
values are small (`CLAUDE.md` "Numeric overflow" rule 1 default: `long` for any magnitude a future
balance pass could touch, and a Wonder's relic cost is exactly the kind of number a balance pass would
raise for a higher tier).

`StructureMagnitudes`/`ParseRow`/`ToStructureDef` (`StructureCatalog.cs:256-283`, `StructureSeed/
StructureCorpus.cs`) gain the identical `TryGetProperty`-optional treatment `wonder-structure` §Design 4
already established for `WonderScope`/`WonderRarity`/`WonderEffects` — a Wonder-only field must be
genuinely optional at parse time so the 25 shipped rows load unmodified:

```csharp
RelicCost: m.TryGetProperty("relicCost", out var rc) && rc.ValueKind == JsonValueKind.Number
    ? rc.GetInt64() : 0,
```

`StructureCatalog.Validate` (`:305-346`) gains, layered onto `wonder-structure`'s own additions to the
same method (multiple modules incrementally extending one `Validate` is this file's own established
pattern — `sector-storage` and `wonder-structure` each add their own checks independently):

```csharp
if (s.WonderScope is not null && s.RelicCost < 1)
    throw new InvalidOperationException($"Structure '{s.StructureId}' is a Wonder but names no relic cost.");
if (s.WonderScope is null && s.RelicCost != 0)
    throw new InvalidOperationException($"Structure '{s.StructureId}' names a relic cost but has no WonderScope.");
```

### 3. Admission-time check — structural only, Core-side

`WorldCommandAdmission`'s `Build` arm (`:103-112`) gains, after the existing `StructureId` check:

```csharp
if (StructureCatalog.Get(structureId).RelicCost is > 0 and var needed)
{
    if (command.RelicInstanceIds.Count != needed) return (false, "relic.count-mismatch");
    if (command.RelicInstanceIds.Distinct(StringComparer.Ordinal).Count() != needed)
        return (false, "relic.duplicate");
}
```

Matches `WorldCommandAdmission`'s own doctrine exactly: *"is this order well-formed... it deliberately
does NOT judge whether the order will still be possible when the turn resolves"* — this is a shape
check only (right count, no duplicates), never an ownership check Core cannot perform.

### 4. The Data-side reachability gate — before `TurnEngine.Step` ever runs

**The load-bearing design call.** Core cannot verify relic ownership (`FusionRpg.Data` is the only place
`rpg_item`/cargo/storage rows exist), but `BuildResolver.Run` resolves purely against the in-memory
`WorldState` it is handed — there is no seam inside Core's own resolution to check DB truth mid-turn.
Rather than inventing one (which would mean giving Core a DB dependency, a hard boundary violation), the
check runs in `FusionRpg.Data`, in `CommitWorldTurn`, in the gap between loading commands and calling
`TurnEngine.Step` (`RpgStore.WorldTurns.cs:530-531`):

```
commands = ListWorldCommandsUnlocked(db, tx, worldId, turn)          // existing line, :530
commands = ValidateWonderRelicCommandsUnlocked(db, tx, worldId, commands)   -- NEW
result   = TurnEngine.Step(world, commands, header.Seed, ...)        // existing line, :531
```

`ValidateWonderRelicCommandsUnlocked`, for every `build` command whose `StructureId` resolves to a
`RelicCost > 0` row:

```
claimed = {}   -- relic ids already spoken for by an earlier command in THIS same commit batch
for each build-for-a-wonder command, in list order:
    ok = every id in command.RelicInstanceIds satisfies ALL of:
           - resolves to a real rpg_item row, disposition = 'owned', player_id = the commander's own
             real player_id (per FactionKindCatalog, the same resolution cargo-transfer's own
             SameFaction helper already performs -- spec-cargo-transfer.md §Design 3)
           - the item's origin traces to a Relic container (relic-item-kind's own Interface row:
             origin_kind = 'drop', origin_ref = a ContainerKind.Relic container id)
           - reachable RIGHT NOW: present in rpg_world_entity_cargo for command.EntityId, OR present
             in rpg_world_sector_storage for command.SectorId (legion-cargo/sector-storage's own
             schemas -- spec-legion-cargo.md §Design 2, spec-sector-storage.md §Design 4)
           - not already in `claimed` (a relic named by an earlier command this same batch cannot
             fund a second Wonder in the same turn)
    if ok: claimed += command.RelicInstanceIds; keep the command unchanged
    else:  replace the command with a copy whose RelicInstanceIds is cleared to empty
return the (possibly rewritten) command list
```

A rewritten (emptied) command reaches `TurnEngine.Step` unchanged in every other field — `BuildResolver`
never re-runs the admission-time count check itself (that would duplicate logic across the Core/Data
boundary), so an emptied list on a `RelicCost > 0` structure needs its own resolution-time refusal,
added to `BuildResolver.Run` directly, immediately beside the existing `CarriedLoam` check (§Design 6):

```csharp
if (structure.RelicCost > 0 && command.RelicInstanceIds.Count != structure.RelicCost)
{ Drop(report, phase, command, "relic.not-reachable"); continue; }
```

This reuses `BuildResolver`'s own existing `Drop`/report machinery — no new report-writing path, no
crossing of `TurnReport.Add`'s `internal` boundary (§Built table). The reason string
(`relic.not-reachable`) is distinct from admission's `relic.count-mismatch` on purpose: the same
structural symptom (count wrong) now means "verified unreachable moments before resolution," a real,
live-state finding, not a malformed order.

**Why this is correct under "legality can change between filing and resolving."** `BuildResolver`'s own
doc comment already states this discipline for ownership/slot-occupancy — re-validate at resolution,
never trust admission. This gate goes one step further and re-validates at the **latest possible
moment before resolution**, inside the same commit that will resolve the turn, which is strictly fresher
than a resolution-time check against a `WorldState` the relic isn't even part of.

### 5. `WonderExistenceScan` — the cap scan

```csharp
namespace FusionRpg.Core.World;

/// <summary>loam-relics-and-wonders `wonder-build-flow`: "how many Unique-rarity Wonders of this
/// WonderScope already exist OR ARE UNDER CONSTRUCTION for this scope-unit" — the half
/// WonderPolicy.ExistenceCapFor's own doc comment names as this module's job (spec-wonder-structure.md
/// §Design 6). Deliberately DOES NOT mirror LoamPhases.EffectiveCapacity's "not under construction"
/// gate — that gate is correct for a capacity/yield axis, wrong for an existence cap (a slot is claimed
/// the moment a build order is accepted, not when it completes; skipping under-construction rows here
/// let two Unique-rarity Wonders of the same scope be built simultaneously, each individually under the
/// cap, both completing over it — found and fixed via strengthen pass, 2026-09-13). Only requirement:
/// a known catalog id on the slot.</summary>
public static class WonderExistenceScan
{
    public static long CountExisting(WorldState world, WorldSector targetSector, WonderScope scope, WonderRarity rarity)
    {
        if (rarity != WonderRarity.Common && rarity != WonderRarity.Unique)
            throw new ArgumentOutOfRangeException(nameof(rarity));
        if (rarity == WonderRarity.Common) return 0;   // Common has no cap -- ExistenceCapFor already
                                                        // returns long.MaxValue; scanning would be wasted work.

        var scopeUnitSectors = scope switch
        {
            WonderScope.Sector => new[] { targetSector },
            WonderScope.Empire => world.Sectors.Where(s =>
                string.Equals(s.OwnerFactionId, targetSector.OwnerFactionId, StringComparison.Ordinal)).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope),
                $"WonderScope.{scope} is reserved and refused at StructureCatalog.Validate -- " +
                "this scan should never be called with it."),
        };

        long count = 0;
        foreach (var sector in scopeUnitSectors)
            foreach (var slot in sector.Slots)
            {
                if (slot.StructureId is not { } id) continue;
                if (!StructureCatalog.IsKnown(id)) continue;

                // Deliberately does NOT skip a structure still under construction
                // (slot.ConstructionTurnsRemaining > 0) -- unlike LoamPhases.EffectiveCapacity's
                // identical-looking gate, which is correct for a CAPACITY/YIELD axis (an unfinished
                // structure legitimately contributes zero capacity), an EXISTENCE cap must claim the
                // slot the moment a build order is accepted, not when it completes. Skipping
                // under-construction Wonders here would let two Unique-rarity Wonders of the same
                // scope be under construction simultaneously (each build order individually sees the
                // cap not yet reached) and both complete, exceeding WonderPolicy.ExistenceCapFor --
                // a real, found-and-fixed defect (strengthen pass, 2026-09-13), not a copy-paste
                // simplification of the capacity gate this scan otherwise mirrors.
                var def = StructureCatalog.Get(id);
                if (def.WonderScope == scope && def.WonderRarity == WonderRarity.Unique) count++;
            }
        return checked(count);
    }
}
```

`long` return, matching `WonderPolicy.ExistenceCapFor`'s own `long` (so the comparison at the call site
never needs a widening cast) even though the count itself is structurally small — the same
type-follows-the-comparison reasoning `legion-cargo`'s own `seq`/`memberCount` typing note already gives
for a bounded structural value compared against a wider one.

**Scope-unit is inherited, not this module's choice.** `WonderPolicy.ExistenceCapFor(WonderScope,
WonderRarity)` has no `structureId` parameter (`spec-wonder-structure.md` §Design 6) — the cap counts
*any* Unique-rarity Wonder sharing this scope, not "how many of this specific Wonder row." §Locked
anchors states this plainly so a future reader does not mistake it for an oversight here.

### 6. Where the Wonder actually lands — `BuildResolver.Run`, extended

**`ConstructionActions.cs` is a fit for exactly one thing in it, and not a fit at all for the rest —
verified fresh, not assumed from the ideal doc's own citation.** `ConstructionCost.CanAffordBuilt`/
`SpendBuilt` (`:295-320`) are pure functions over `(long, long, StructureDef)` with zero board/actor
dependency — genuinely reusable as-is, called directly from `BuildResolver.Run`, **no change to
`ConstructionActions.cs` itself**. `ConstructionActivation.Fire` and everything else in that file is
**not** a fit: it is a per-actor, per-cell, siege-**board** activation path (`BattleEffectHost`,
`actorPtr`, `targetRow`/`targetCol`) for the entirely different game mode a district assault runs in —
a Sector/Empire-scope Wonder is a **world-map, turn-resolved** structure, and its landing site is
`BuildResolver.Run`, not a new file under `FusionRpg.Core.Battle.Siege`.

`BuildResolver.Run` (`:74-134`) gains, between the existing slot-kind check and the existing
`CarriedLoam` check:

```csharp
var structure = StructureCatalog.Get(structureId);
// ...existing slot-kind / range checks, unchanged...

if (structure.RelicCost > 0 && command.RelicInstanceIds.Count != structure.RelicCost)
{ Drop(report, phase, command, "relic.not-reachable"); continue; }   // §Design 4

if (structure.WonderScope is { } wScope && structure.WonderRarity is { } wRarity)
{
    var existing = WonderExistenceScan.CountExisting(next, sector, wScope, wRarity);   // §Design 5
    var cap = WonderPolicy.ExistenceCapFor(wScope, wRarity);
    if (existing >= cap) { Drop(report, phase, command, "wonder.cap-reached"); continue; }
}

if (!ConstructionCost.CanAffordBuilt(sector.RubbleStock, sector.IronworkStock, structure))
{ Drop(report, phase, command, "build.cannot-afford-materials"); continue; }   // NEW -- closes the wiring gap
                                                                                // for EVERY structure, not
                                                                                // only Wonders, since no
                                                                                // peacetime row has ever
                                                                                // exercised this check before

if (entity.CarriedLoam < structure.Cost)   // existing check, unchanged
{ Drop(report, phase, command, "build.cannot-afford"); continue; }
```

...and the existing `next = next with { ... }` block (`:111-130`) gains one more field write, computed
via the same pure `SpendBuilt` function:

```csharp
var (newRubble, newIronwork) = ConstructionCost.SpendBuilt(sector.RubbleStock, sector.IronworkStock, structure);
// ...folded into the existing `Sectors = next.Sectors.Select(...)` transform, setting
// s with { RubbleStock = newRubble, IronworkStock = newIronwork, Slots = ... } for the matching sector.
```

**This closes the wiring gap named in §What already exists for every future structure with a nonzero
`ConstructRubbleCost`/`ConstructIronworkCost` built via the peacetime `build` order — not a
Wonder-specific carve-out.** No existing shipped row exercises this path today (only the siege-only
`moat` authors a nonzero rubble/ironwork cost, and it is placed via `ConstructionActivation.Fire`, never
`BuildResolver`), so this closes a real gap with zero risk of moving any existing golden.

### 7. The relic spend — after `DiffWorldGraphUnlocked`, same `tx`

```
DiffWorldGraphUnlocked(db, tx, world, result.World)              // existing line, RpgStore.WorldTurns.cs:537
SpendWonderRelicsUnlocked(db, tx, worldId, commands, result.Report)   -- NEW, same tx
```

```
SpendWonderRelicsUnlocked(db, tx, worldId, commands, report):
    for each entry in report.Entries where entry.Kind == Event and entry.Detail starts with "build.started:":
        command = commands.First(c => c.CommandId == entry.Subject)
        if command.RelicInstanceIds is empty: continue          -- an ordinary, non-Wonder structure
        for each relicId in command.RelicInstanceIds:
            -- delete wherever it currently sits: legion-cargo's own row (if aboard command.EntityId)
            -- or sector-storage's own row (if already in command.SectorId) -- exactly one of the two
            -- is true, per §Design 4's own reachability proof moments earlier in the same tx
            DELETE FROM rpg_world_entity_cargo  WHERE world_id=$w AND entity_id=$e  AND instance_id=$relicId
            DELETE FROM rpg_world_sector_storage WHERE world_id=$w AND sector_id=$s AND instance_id=$relicId
            UPDATE rpg_item SET disposition = 'consumed' WHERE instance_id = $relicId
```

`'consumed'` is a new value in the same free-form, documented convention `Disposition` already uses
(`RpgStore.Items.cs:636-637` names `owned`/`salvaged`/`transferred`/`destroyed`) — not a closed C#
enum requiring cross-program review, this module's own straightforward addition.

## The transaction and atomicity design

**Everything that changes as a result of one accepted Wonder build — the sector's material stocks, the
slot's new structure, and every spent relic's disposal — commits inside the **single** SQL transaction
`CommitWorldTurn` already opens for the whole turn** (`RpgStore.WorldTurns.cs:510`), never a second,
later transaction:

1. `ConstructionCost.SpendBuilt`'s `RubbleStock`/`IronworkStock` debit and the slot's `StructureId`
   assignment are pure **Core** `WorldState` mutations (§Design 6) — they ride the existing "the whole
   turn's diff commits atomically" machinery `DiffWorldGraphUnlocked` already provides, the identical
   discipline `CarriedLoam`'s own spend already relies on today. No new atomicity work needed for this
   half; it inherits an already-proven guarantee.
2. The relic reachability pre-check (§Design 4) and the relic spend (§Design 6/7) are **Data**-side, and
   both run inside the same `tx` — the pre-check immediately before `TurnEngine.Step`, the spend
   immediately after `DiffWorldGraphUnlocked`. A crash anywhere between `BeginTransaction` (`:510`) and
   `tx.Commit()` rolls back **everything**: the material spend, the slot occupation, and the relic
   deletion, together, exactly the guarantee `cargo-fate`'s own "before the cascade fires" ordering
   proof already established for a structurally identical risk (its own real, found race:
   `rpg_world_entity_cargo`'s `ON DELETE CASCADE` would silently destroy cargo if the cache-move ran
   after the entity delete, not before — `spec-cargo-fate.md` §Locked anchors).
3. **Never a partial spend.** The relic spend function (§Design 7) only fires for commands whose
   `TurnReportEntry` shows `"build.started:"` — a command dropped for *any* reason (cap reached,
   materials unaffordable, relic unreachable, wrong slot) never appears there, so the relic spend never
   runs for a build that did not actually happen. This is "refuse-then-move" by construction: the
   refusal already happened, inside Core's own resolution, before this function is ever called.
4. **Never a double-spend within one turn.** §Design 4's own `claimed` set, threaded through the
   reachability pre-check in command order, prevents one relic instance funding two Wonder-build orders
   filed in the same commit.

## New/changed types

| Type | Change | Owner (which module's file) |
|---|---|---|
| `WorldCommand` | `+ RelicInstanceIds: IReadOnlyList<string>` | `wonder-build-flow` extends a `FusionRpg.Core.World.Turn` type shared by every command kind |
| `StructureDef` | `+ RelicCost: long` | `wonder-build-flow` extends `StructureCatalog.cs`, layered onto `wonder-structure`'s own additions to the same record |
| `StructureMagnitudes` | `+ RelicCost: long? = null` (optional wire field) | same file, mirroring `wonder-structure`'s own `WonderScope?`/`WonderRarity?` optional-parse pattern |
| `WonderExistenceScan` (new, static) | `CountExisting(WorldState, WorldSector, WonderScope, WonderRarity) → long` | `wonder-build-flow`, new file in `FusionRpg.Core.World` |
| `ValidateWonderRelicCommandsUnlocked` (new) | Data-side pre-check, `FusionRpg.Data` | `wonder-build-flow`, new file |
| `SpendWonderRelicsUnlocked` (new) | Data-side spend, `FusionRpg.Data` | `wonder-build-flow`, new file |

## Key functions/verbs

| Verb | Layer | When it runs |
|---|---|---|
| `WorldCommandAdmission.Admit` (extended) | Core | Order submit time — structural count/duplicate check only (§Design 3) |
| `ValidateWonderRelicCommandsUnlocked` | Data | Inside `CommitWorldTurn`, immediately before `TurnEngine.Step` (§Design 4) |
| `BuildResolver.Run` (extended) | Core | `Snapshot` phase — reachability-count re-check, existence-cap check, materials afford+spend, then the existing `CarriedLoam` check (§Design 5-6) |
| `ConstructionCost.CanAffordBuilt`/`SpendBuilt` | Core | Called *from* `BuildResolver.Run` — unmodified, reused as-is (§Design 6) |
| `WonderExistenceScan.CountExisting` | Core | Called *from* `BuildResolver.Run`, per Wonder-shaped command (§Design 5) |
| `SpendWonderRelicsUnlocked` | Data | Inside `CommitWorldTurn`, immediately after `DiffWorldGraphUnlocked` (§Design 7) |

## Tunables

| Number | Home | Notes |
|---|---|---|
| `RelicCost` per Wonder row | `data/seed/structures/**` (generated seed content, via the same `structures` seedsmith adapter) | **Not** `data/tuning/*.json` — mirrors `Cost`/`ConstructRubbleCost`/`ConstructIronworkCost`'s own precedent, already corrected once for a sibling field by `wonder-structure` §Tunables |
| `UniqueExistenceCap.Sector`/`.Empire` | `data/tuning/loam-relics-wonders.v1.json` | **Not this module's own tunable** — already owned by `wonder-structure` (§Design 6 there); this module only reads it via `WonderPolicy.ExistenceCapFor` |
| A move-turn/reachability-distance cost for spending an already-in-sector-storage relic vs. a legion-carried one, if any | Not addressed by any owner decision | Named as an open, non-blocking content question, matching `cargo-transfer`'s own identical "not yet decided" framing for its deposit/withdraw verbs — not invented here |

## Numeric types

`StructureDef.RelicCost` is `long`, matching every sibling "spend N of X" field on the same record
(`CLAUDE.md` rule 1: `long` for any magnitude a future balance pass could raise). `WorldCommand
.RelicInstanceIds` carries opaque strings, no magnitude. `WonderExistenceScan.CountExisting`'s return is
`long`, matching `WonderPolicy.ExistenceCapFor`'s own return type so the comparison at the call site
needs no widening cast, even though the count itself is structurally small (bounded by how many sectors
a faction can plausibly hold — a structural bound, not a `contentScale`-reachable magnitude, the same
reasoning `legion-cargo`'s own `seq`/`memberCount` typing note already gives). `ConstructionCost
.SpendBuilt`'s `(long, long)` return is unchanged, reused as-is.

## Locked anchors (recap, load-bearing)

1. **The recipe is generic-by-count in v1** — no relic kind/tag/theme matching, because no such axis is
   authored on relic anchors today (§The recipe-shape decision).
2. **`BuildResolver.Run` is the landing site, not a new file under `FusionRpg.Core.Battle.Siege`** —
   `ConstructionCost.CanAffordBuilt`/`SpendBuilt` are reused unmodified; `ConstructionActivation` and
   the rest of `ConstructionActions.cs` are untouched (§Design 6).
3. **Relic ownership is verified in `FusionRpg.Data`, once, immediately before `TurnEngine.Step` —
   never inside Core, which has no DB access** (§Design 4).
4. **The existence cap counts by `(WonderScope, WonderRarity)` across the scope-unit, never by specific
   Wonder id** — inherited from `WonderPolicy.ExistenceCapFor`'s own locked signature (§Design 5).
5. **Everything commits in the one transaction `CommitWorldTurn` already opens** — the material spend
   rides Core's existing diff-commit guarantee; the relic spend is a second Data-side touch in the same
   `tx`, never a second transaction (§The transaction and atomicity design).

## External dependency status — cannot run end-to-end yet, stated plainly

**Every one of this module's four dependencies is specced, not built, confirmed fresh this session:**

- **Internal — `relic-item-kind` (module 1) and `wonder-structure` (module 2).** `StructureCatalog.cs`
  was read in full this session: it has exactly five `StructureKind` members and no `WonderScope`/
  `WonderRarity`/`RelicCost` field anywhere. `DropTableModel.cs`'s `DropEntryKind` and `ContainerRow
  .cs`'s `ContainerKind` were not re-opened this session, but `relic-item-kind`'s own spec states them
  as its own not-yet-built additions. This module's design is written **against those two specs'
  types**, exactly the posture `cargo-transfer` already took toward `legion-cargo`/`sector-storage`
  (also specced, not built, at the time `cargo-transfer` was written).
- **External — `scoped-inventory-hierarchy` (all four of its own modules).** The program's own map
  states plainly: *"Idea phase locked 2026-09-13; capability map approved 2026-09-13; no module built
  yet"* (`decisions.md` "Scoped inventory hierarchy SSOT (2026-09-13)", quoted below). Confirmed fresh
  this session: no occurrence of `rpg_world_entity_cargo` or `rpg_world_sector_storage` exists anywhere
  under `src/FusionRpg.Data`. This program's own map names the exact gate this module inherits,
  verbatim: *"the sub-program `loam-relics-and-wonders-ideal.md` is hard-blocked on (a legion cannot
  deposit a carried relic at a sector until this program's sector storage exists)"*
  (`scoped-inventory-hierarchy-map.md`, "Ideal it implements" line).

**What "specced now, built later" means in practice, stated per design section above:**

| Section | Can be designed/reasoned about today | Cannot be tested/wired until the dependency ships |
|---|---|---|
| §Design 1-2 (new fields) | Yes — pure C# additions to Core types this module can build the moment module 1/2 ship, independent of `scoped-inventory-hierarchy` | — |
| §Design 3 (admission check) | Yes — pure structural, no DB dependency at all | — |
| §Design 5 (existence-cap scan) | Yes — reads only `WorldState`/`StructureCatalog`, both Core-only | — |
| §Design 6 (`BuildResolver` extension, materials afford+spend) | Yes — pure `WorldState`/`StructureDef` logic, no relic/item dependency at all | — |
| §Design 4 (Data-side reachability pre-check) | The *shape* and call site are fully specced here | Cannot compile or run against real tables until `legion-cargo`'s `rpg_world_entity_cargo` and `sector-storage`'s `rpg_world_sector_storage` exist — this function's body is written against schemas that do not exist in code yet |
| §Design 7 (relic spend) | The *shape* and call site are fully specced here | Same blocker — the `DELETE`/`UPDATE` statements target tables that do not exist yet |

**Decisions quoted in full, both re-opened this session** (`decisions.md:47-48`):

> **Scoped inventory hierarchy SSOT (2026-09-13)** — "`rpg_item`/`rpg_item_stock` stay the one
> ownership root for every inventory scope — a scope is a reachability + capacity overlay, never a
> second ownership table... Idea phase locked 2026-09-13; capability map approved 2026-09-13; no module
> built yet."

> **Loam relics and wonders SSOT (2026-09-13)** — "...The Wonder *build flow* (spending a relic at a
> sector) is a hard external block on `scoped-inventory-hierarchy` (spec'd, unbuilt): the verb's spec is
> written this wave so the recipe shape is ready the moment that program ships, but it cannot run
> end-to-end before then... Idea phase locked 2026-09-13; capability map approved 2026-09-13; no module
> built yet."

This module's own spec satisfies exactly the mandate in that second quotation — nothing more is claimed
buildable-today than the table above states.

## What this module does not touch

- **The relic minting/drop-table pipeline** — `relic-item-kind`'s own scope; this module only reads the
  resulting `rpg_item` rows, never authors or mints them.
- **The Wonder's own effect vocabulary and its Empire-scope consumption** —
  `WonderEffectKind`/`WonderEffectDef`, the `Empire`-scope SUM rule, and the `LoamProduction.For`
  faction-input plumbing are `wonder-effect-empire`'s (module 3) own job. This module only gets a
  Wonder *built*; what the built Wonder's effect *does* is out of scope here.
- **`World`/`Multiverse` scope** — refused upstream at `StructureCatalog.Validate`; this module's own
  `WonderExistenceScan` throws loudly if ever called with either, rather than silently returning zero.
- **`ConstructionActivation`/the siege-board construction path** — untouched; `ConstructionActions.cs`
  gains zero edits from this module (only `ConstructionCost`'s two pure functions are *called*, not
  modified).
- **`scoped-inventory-hierarchy`'s own schemas and verbs** — consumed via their own named call sites
  (`LoadCargoUnlocked`/`UnloadCargoUnlocked`, `DepositUnlocked`/`WithdrawUnlocked`, `SameFaction`) once
  built; never re-implemented in parallel here.
- **Cross-faction relic spend** — a legion of one faction cannot spend a relic into another faction's
  sector storage; this is the same program-wide real gap `cargo-transfer`/`sector-storage` already name
  and defer (no faction besides `Player` owns a real item store today), not re-solved here.
- **`RaiseResolver`/`DevelopResolver`/`WardenResolver`** — the other `Snapshot`-phase resolvers,
  unmodified; this module's own `BuildResolver` extension does not change their call order or behavior.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderBuild"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BuildResolver"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WonderBuild"
.\scripts\guard-dal.ps1        # every new SQL string lives in FusionRpg.Data
```

## Structure

```
src/FusionRpg.Core/World/Turn/WorldCommand.cs              MODIFIED — RelicInstanceIds field (§Design 1)
src/FusionRpg.Core/World/Turn/WorldCommandAdmission.cs     MODIFIED — Build arm gains relic count/dup check (§Design 3)
src/FusionRpg.Core/World/StructureCatalog.cs               MODIFIED — RelicCost field + Validate pairing rule (§Design 2)
src/FusionRpg.Core/World/StructureSeed/StructureCorpus.cs  MODIFIED — RelicCost optional wire field (§Design 2)
src/FusionRpg.Core/World/WonderExistenceScan.cs            NEW — CountExisting (§Design 5)
src/FusionRpg.Core/World/Movement/BuildResolver.cs         MODIFIED — relic-reachability re-check, existence-cap
                                                            check, materials afford+spend (§Design 6)
src/FusionRpg.Data/Sqlite/RpgStore.WonderBuild.cs          NEW — ValidateWonderRelicCommandsUnlocked (§Design 4),
                                                            SpendWonderRelicsUnlocked (§Design 7); both call
                                                            into legion-cargo/sector-storage's own tables once
                                                            those modules ship — cannot compile against them
                                                            before then (§External dependency status)
src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs           MODIFIED — two new call sites inside CommitWorldTurn's
                                                            existing tx, at :530-531 and after :537
tests/FusionRpg.Core.Tests/World/WonderBuildResolverTests.cs   NEW
tests/FusionRpg.Data.Tests/WonderBuild/                        NEW (blocked until scoped-inventory-hierarchy ships)
UNTOUCHED: ConstructionActions.cs (both classes read from, neither modified); RaiseResolver.cs/
           DevelopResolver.cs/WardenResolver.cs; LoamProduction.cs/LoamPhases.cs; WonderEffectKind/
           WonderEffectDef (wonder-effect-empire's own); every one of the 25 shipped structure seed rows.
```

## Code style

```csharp
// BuildResolver.cs -- the materials gate reuses ConstructionCost's own pure functions verbatim,
// never a second copy of the same afford/spend arithmetic.
if (!ConstructionCost.CanAffordBuilt(sector.RubbleStock, sector.IronworkStock, structure))
{
    Drop(report, phase, command, "build.cannot-afford-materials");
    continue;
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_ordinary_non_wonder_build_is_unaffected` | a `RelicCost = 0` structure's build order still succeeds exactly as before this module's changes — zero behavior change for the 25 shipped rows |
| `admission_refuses_a_relic_count_mismatch` | `WorldCommandAdmission.Admit` refuses `relic.count-mismatch` when `RelicInstanceIds.Count != structure.RelicCost` |
| `admission_refuses_duplicate_relic_ids` | `relic.duplicate` on a repeated id in the same order |
| `resolution_refuses_when_relics_were_cleared_upstream` | `BuildResolver.Run` drops with `relic.not-reachable` when handed a command whose `RelicInstanceIds` was emptied by the Data-side pre-check |
| `existence_scan_counts_only_matching_scope_and_unique_rarity` | `WonderExistenceScan.CountExisting` ignores `Common`-rarity Wonders, ignores a different `WonderScope`, and ignores an ordinary non-Wonder structure on the same slot |
| `existence_scan_counts_a_structure_still_under_construction` | **deliberately diverges from `EffectiveCapacity`'s own gate** — a `Unique`-rarity Wonder with `ConstructionTurnsRemaining > 0` still counts toward the cap, since the slot was already claimed at build-order-acceptance time (strengthen pass, 2026-09-13 — this is the fix, not an oversight to guard against reverting) |
| `two_unique_wonders_cannot_both_be_under_construction_at_once` | the second build order for a `Unique`-rarity Wonder of the same scope refuses at `wonder.cap-reached` while the first is still mid-construction — the exact same-scope-simultaneous-construction bypass the strengthen pass found and this fix closes |
| `empire_scope_scan_sums_across_every_sector_the_faction_owns` | two sectors, same faction, one Unique Empire-scope Wonder each — count is 2, not 1 |
| `sector_scope_scan_only_looks_at_the_target_sector` | a Unique Sector-scope Wonder in a *different* sector owned by the same faction does not count against this sector's own cap |
| `cap_refuses_construction_at_the_configured_limit` | `WonderPolicy.ExistenceCapFor`'s tunable value, not a hard-coded `1`, gates the refusal — changing the tuning file's number changes the outcome with no code change |
| `common_rarity_never_refuses_on_count` | a `Common`-rarity Wonder builds freely regardless of how many already exist |
| `materials_afford_and_spend_debit_the_sector_not_the_legion` | `RubbleStock`/`IronworkStock` decrease by exactly `ConstructRubbleCost`/`ConstructIronworkCost`; `CarriedLoam` decreases by exactly `Cost` — two independent debits, never conflated |
| `insufficient_materials_refuses_before_touching_carried_loam` | `build.cannot-afford-materials` fires and `CarriedLoam` is left untouched when materials are short but loam is not |
| *(gated on `scoped-inventory-hierarchy` shipping)* `relic_spend_moves_never_copies` | after a successful Wonder build, the source cargo/storage row is gone and `rpg_item.disposition = 'consumed'` — never both still present, never neither changed |
| *(gated)* `relic_spend_only_fires_for_an_accepted_build` | a command dropped for any reason (cap, materials, wrong slot) leaves every named relic's row and disposition untouched |
| *(gated)* `two_wonder_orders_cannot_spend_the_same_relic_in_one_turn` | the second command naming an already-claimed relic id is rewritten to an empty `RelicInstanceIds` and refuses at resolution |
| *(gated)* `reachability_check_reads_live_state_not_a_cached_flag` | a relic moved out of reach between order-filing and commit (by an unrelated command in the same batch) correctly refuses at commit time |

## Boundaries

- **Always:** reuse `ConstructionCost.CanAffordBuilt`/`SpendBuilt` unmodified; read
  `WonderPolicy.ExistenceCapFor` for every cap comparison, never a literal `1`; verify relic reachability
  in `FusionRpg.Data`, never inside Core; spend a relic exactly once, in the same `tx` as the turn's own
  diff commit.
- **Ask first:** any change to `unique`/`relic-item-kind`'s own `KindSpec` or `DropEntryKind`/
  `ContainerKind` vocabularies (none needed by this module); any change to `WonderPolicy
  .ExistenceCapFor`'s signature (e.g. adding a `structureId` parameter for a per-Wonder-id cap) —
  that is `wonder-structure`'s own locked type, not this module's to widen unilaterally.
- **Never:** a `RequiredRelicTag`/`RequiredRelicRarity` field before relic anchors actually author such
  an axis (§The recipe-shape decision); a second transaction for the relic spend; a hard-coded existence
  cap of `1`; a new `WorldCommandKinds.BuildWonder` fork of the existing `Build` kind; touching
  `ConstructionActivation` or any board/actor-scoped siege machinery.

## Notification hook — named, not built (strengthen pass, 2026-09-13)

Two events this module creates are real, player-relevant, and currently silent: a Wonder finishing
construction (a `TurnReportEntry` already fires — `"build.started:{structureId}"`, but nothing reads
it as a player-facing signal), and a `build` order refusing on `wonder.cap-reached` (the player spent
a turn filing an order that resolved to nothing, with no in-band explanation beyond the turn report).
`docs/architecture/notification-ssot-ideal.md` is the owner's own sibling idea doc for exactly this
class of event. This module does not build a notification hook — both events are named here as real
future consumers of that program, once it ships, reusing the `TurnReportEntry` this module already
writes rather than inventing a second event-emission path.

## Success criteria

1. A Wonder-shaped `build` order with the correct relic count, affordable materials, an empty
   compatible slot, and headroom under the existence cap succeeds — the slot gains the structure,
   `RubbleStock`/`IronworkStock` and `CarriedLoam` both debit correctly, in the same turn commit.
2. Every one of admission's count/duplicate check, resolution's cap/materials/reachability checks, and
   the relic spend are independently testable and independently refuse without touching any other
   table (§Testing strategy).
3. `WonderExistenceScan` never returns a value that lets a `Unique`-rarity Wonder exceed
   `WonderPolicy.ExistenceCapFor`'s own tunable number.
4. `guard-dal.ps1` green — every new SQL statement lives in `FusionRpg.Data`.
5. Zero changes to `ConstructionActions.cs`, `WonderEffectKind`/`WonderEffectDef`, or any of the 25
   shipped structure seed rows.
6. The Data-side half of this module (§Design 4, §Design 7) is explicitly named as blocked on
   `scoped-inventory-hierarchy` shipping — this criterion is satisfied by the spec stating that
   plainly, not by pretending it can be proven today.

## Interface exposed to dependents

None — this is the final module in the map. `wonder-effect-empire` (module 3) does not depend on this
module (the map's own dependency table shows both wave-2 modules depending only on `wonder-structure`,
never on each other) — a built Wonder's *existence* (this module's own output) and its *effect*
(module 3's own concern) are independent once `WonderEffectDef` is attached to the row at authoring
time, per `wonder-structure`'s own design.

## Design-gate checklist

```
[x] Subsystems: world-map structure/command/turn-commit state (Core, Data), item ownership (Data,
    read-only from this module's perspective) — no Status/ActorHub/Combat subsystem touched.
[x] Read this session: loam-relics-and-wonders-ideal.md (full, both pages, re-opened); loam-relics-
    and-wonders-map.md (full, re-opened); spec-relic-item-kind.md (full); spec-wonder-structure.md
    (full); scoped-inventory-hierarchy-map.md (full); spec-legion-cargo.md, spec-sector-storage.md,
    spec-cargo-transfer.md, spec-cargo-fate.md (all four, full); decisions.md "Loam relics and
    wonders SSOT" and "Scoped inventory hierarchy SSOT" rows (both, verbatim, this session);
    DESIGN-GATE.md §1 (World map, Data/SQL/schema, Any cap/ceiling, Any tunable number rows).
[x] Code cited by file:line, opened fresh this session: StructureCatalog.cs (full file, confirming
    5 StructureKind members and no Wonder field exists yet); ConstructionActions.cs (full file,
    :295-320 ConstructionCost, :226-281 ConstructionActivation); BuildResolver.cs (full file,
    confirming the Rubble/Ironwork gap by its total absence, not a grep miss); WorldCommand.cs
    (:30-34, :98-138); WorldCommandAdmission.cs (:1-11, :95-112); TurnEngine.cs (:377-399);
    TurnReport.cs (:30-31, :90-93); RpgStore.WorldTurns.cs (:484-537); WorldState.cs (:96-146,
    :148-210, :286-342); RpgStore.Items.cs (:72-73, :636-657).
[x] Corrected (strengthen pass, 2026-09-13): `WonderExistenceScan.CountExisting` (§Design 5) originally
    skipped any slot with `ConstructionTurnsRemaining > 0`, copying `LoamPhases.EffectiveCapacity`'s
    gate verbatim without re-deriving whether it still applies to a fundamentally different question
    (a capacity axis vs. an existence cap). It did not: skipping under-construction Wonders let two
    `Unique`-rarity Wonders of the same scope be built simultaneously, each individually seeing the cap
    unreached, both completing over it — directly falsifying this module's own Success Criterion #3.
    Fixed by counting a Wonder from the moment its build order is accepted (slot occupied), not from
    completion; §Testing strategy gained a same-construction-window regression test.
[x] Drift reported: the assignment brief's own description of relic anchors ("optional theme/
    themeKey/tier fields") is corrected — `tier` is a named-but-unregistered open content question
    in `relic-item-kind`'s own spec, not a shipped optional field (§Locked anchors). The ideal doc's
    "basic material cost is RESOLVED, no new engine plumbing" claim is corrected to apply only to the
    siege consumer (`ConstructionCost`) — the world-map peacetime consumer (`BuildResolver`) has never
    read those two fields at all, a real, previously-unflagged wiring gap this module closes for every
    future structure, not only Wonders.
[x] Every RPG feature lives in the RPG layer: Core (World/Turn) + Data only, no Unity/PvZ write.
[x] No magic numbers: `RelicCost` is seed content (mirroring `Cost`/`ConstructRubbleCost`'s own
    precedent); the existence cap is read from `WonderPolicy.ExistenceCapFor`, never a literal.
[x] No hard progression ceilings: the existence cap is tunable, never hard-coded to `1`; `Common`
    rarity is explicitly uncapped by construction (`WonderExistenceScan` returns 0 immediately).
[x] `long` for magnitudes, never `float`: `RelicCost`, `WonderExistenceScan.CountExisting`'s return,
    and every reused `ConstructionCost` field are all `long` (§Numeric types).
[x] One ActorHub compose / one read: not applicable — this module produces and consumes no actor
    combat/derived magnitude of any kind.
[x] SOLID: extends existing gates (`Build` command kind, `ConstructionCost`'s pure functions,
    `StructureCatalog.Validate`'s incremental-check pattern, `BuildResolver`'s own `Drop` machinery)
    rather than forking a parallel command kind, a parallel cost gate, or a parallel report-writing
    path. No second ActorHub composer, no second ownership root, no second transaction.
[x] A guardrail validates the contract, never a population count: §Testing strategy asserts refusal
    reasons, atomicity, and cap-vs-tunable behavior — no test asserts how many Wonders exist in the
    corpus or their authored names.
[x] Generated seed data is never hand-edited: `RelicCost` reaches the 25 shipped rows as an
    optional-with-zero-default field, requiring no edit to any existing `data/seed/structures/**` file.
[ ] The Data-side half (§Design 4, §Design 7) is specced but not implementable/testable until
    `scoped-inventory-hierarchy` ships — named explicitly in §External dependency status, not silently
    assumed solvable today.
[ ] A per-Wonder-id existence cap (Civ5's National Wonder shape, as opposed to the per-scope+rarity
    cap `WonderPolicy.ExistenceCapFor`'s locked signature actually provides) was not designed here —
    correctly deferred as a signature change owned by `wonder-structure`, not invented as a workaround.
```
