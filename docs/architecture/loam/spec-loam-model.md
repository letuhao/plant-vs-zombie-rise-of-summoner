# Spec: loam-model (wave 1)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-model` in the
[loam capability map](../loam-map.md). **No dependencies** — first module in the program.
**Design source:** [empire-economy-ssot.md](../empire-economy-ssot.md) §2–§3 ·
[economy-principles.md](../economy-principles.md).

## Objective

Loam and the Fracture as **state**, and nothing else. Where a sector's loam sits, how much it can
hold, and how hard the Fracture presses on that particular ground — stored, validated, hashed,
persisted, and projected under fog. **Nothing produces, consumes, fades or decides in this module.**

Success looks like: a world can be authored with rootbeds and a chaos gradient, saved, reloaded
byte-identically, and every malformed shape is refused at creation rather than found as a wrong number
four modules later.

This module is deliberately tiny. Per capability-map finding **A10**, almost everything loam needs
already exists — `StabilityMilli`, `DangerBand`, `SupplyGraph`, the `Production`/`Pressure`
pass-throughs, `SectorPhase.Lost`, and `SlotTypeDef.Yields` (shipped, one reader, and that reader is
its own validator). What is genuinely new is **two fields on the sector, one on the faction, and one catalog row.**

## Design

### The two fields on `WorldSector`

| Field | Type | Meaning |
|---|---|---|
| `LoamStock` | **`long`** | What this sector is holding right now — **stored per sector, spent per component** (see below). A plain count, not a per-mille: per-mille in this codebase means *rate or fraction*, and a stockpile is neither. **`long`, not `int` — see below** |
| `FractureIntensityMilli` | `int` | The local strength of the Fracture, as a multiplier where **1000 = baseline**. This is the chaos gradient of ideal §12.6: the map is not uniformly dangerous, and this is the field that says so |

### Why loam quantities are `long` (owner, 2026-08-23)

**Quantities are `long`; bounded per-mille multipliers stay `int`.**

The reason is not that a stockpile gets large — it will not. It is that **one `long` operand promotes
the whole expression**, so `stock × intensityMilli × handicapMilli` is computed in `long`
automatically. There is no cast to remember, therefore no cast to forget.

That matters because the alternative was already caught failing: with `int` quantities, the upkeep
formula's two multipliers reach `int.MaxValue` at legal inputs and wrap to **negative upkeep**, which
does not crash — it reads as *free territory*. The `int` version depended on a human writing `(long)`
in the right place forever. This version removes the class rather than patching the instance.

**It costs nothing.** `WorldCanonical.Row` takes `params object?[]` and formats invariantly, so `5L`
and `5` both write `"5"` — **no hash moves, no golden moves.** SQLite `INTEGER` is variable-length, so
a small `long` occupies the same bytes on disk. `WorldDtos` already carries four `long` fields
(`Revision`, `Strength`, `BandCeiling`, `LifelineCost`), and the JavaScript precision hazard noted on
`CreateWorldRequest.Seed` concerns a full 64-bit random past 2^53 — loam values are thousands.

**Per-mille multipliers stay `int`** — validation-bounded to `[0, 3000]`, never accumulating, and the
whole existing family (`StabilityMilli`, `PressureMilli`, `DepletionMilli`, `LaneProgressMilli`) is
`int`. Widening them would churn the world model for no safety we do not already get from the quantity
leading the expression.

### `WorldFaction.UpkeepHandicapMilli`

A declared balance lever, 1000 = normal, applied inside `LoamUpkeep`. It exists because a thin AI
policy needs to survive its own bad decisions while the real one is built (map §7, S5).

**It is a handicap, not a cheat.** `FusionRpg.CheatCore` already owns that word for debug tooling, and
a hidden fudge could not survive replay in any case. This is ordinary hashed state: it goes in
`WorldCanonical`'s faction row, it replays, and `loam-turn` **names it in the report whenever it is
not 1000**. A visible handicap is a balance lever; a silent one is a bug that explains itself away.

`LoamCapacity` is **deliberately not a field yet.** Until granaries exist (`loam-texture`), capacity is
a single policy constant applied uniformly, and a stored per-sector column would be a column holding
the same number in every row. It arrives with the building that varies it.

### The rootbed slot type

One new row in `SlotTypeCatalog`, and one new member appended to `SlotKind`:

```
Rootbed — "ground where the old world still shows through"
  Buildable = true      (a well goes here later, in loam-structures)
  Yields    = true      (it seeps on its own — A10's wave-1 source)
```

**`SlotKind.Rootbed` must be appended, never inserted.** `WorldCanonical` writes `sl.SlotTypeId` — the
string, not the enum — so appending is safe and inserting would silently renumber nothing that is
hashed but would still churn every switch and default in the codebase. Append.

`SectorTypeCatalog.AllowedSlotTypes` gains `rootbed` on the sector types that may carry one. **Which
types, and how many, is `loam-maps`' decision, not this module's** — this module only makes it
expressible and validates it.

### Stored per sector, spent per component

Owner decision 2026-08-23: loam is **fungible across a connected component** of a faction's
territory. Stock still lives on the sector — so a stockpile has a location, can be raided later, and
severing splits a real thing rather than a number — but **upkeep is charged against the component's
total**, not against each sector's own pocket.

The alternative (each sector pays from its own stock) was specced by accident and would have made
every deficit sector unholdable at any empire wealth, destroying ideal §12.4. See map §7, S3.

`loam-calc` owns the component identification and the draw order. This module only has to store stock
somewhere that a component can be summed from, which the per-sector field already does.

### Gaps closed by walking the build (2026-08-23)

Five rules a builder would have hit and no spec answered.

**G-A · A new world does not start empty.** Templates author a **starting `LoamStock`** per sector.
With a deficit baseline (ideal §12.4) and zero stock, a new world begins fading on turn one, which is
not a difficulty curve, it is a broken opening.

**G-B · Unowned sectors have no economy.** No owner ⇒ no production, no upkeep, no accumulation. A
rootbed sitting in neutral ground does **not** quietly fill up while nobody holds it — otherwise the
optimal play is to wait and then take the windfall, which rewards doing nothing.

**G-C · A faction with no loam source is exempt from loam entirely** — no production, no upkeep, no
fade. This is not a special case invented here; it mirrors the rule `SupplyGraph.cs:18` already
states: *"the wild do not starve for want of a capital they never had."* The wild are a hazard, not
an empire, and an economy that dissolves them turns the map's dangers into a countdown.

**G-D · `first-light` gets its homeworld rootbed in wave 1, not wave 2.** Validation rule 4 refuses a
homeworld without a source, and `loam-maps` does not re-author the template until wave 2 — so shipping
rule 4 first would make the only existing world invalid the moment it loads. The minimum template edit
belongs in **this** module, with the field addition, inside the same re-bless.

**G-E · The handicap is authored in the template**, per faction, like `PolicyId`. It is world state,
so it must exist at creation and be hashed from the start; there is no runtime difficulty switch.

### Fog: intensity is terrain, stock is live

This is the one non-obvious rule in the module, and getting it wrong leaks information.

| | Kind of fact | Who sees it |
|---|---|---|
| `FractureIntensityMilli` | **Terrain.** It is a property of the ground, like a climate or a lane length | Anyone who has scouted the sector. It goes into the intel snapshot and is remembered |
| `LoamStock` | **Live state.** It changes every turn and knowing it is knowing your enemy's readiness | **Only the owner.** Never in an intel snapshot, never in another faction's projection |

A remembered snapshot must not carry a stale stock number either — a number that was true six turns
ago is worse than no number, because it looks current. Stock is simply absent from belief.

### What moves, and once

`WorldCanonical.cs:34` writes the sector row field by field, so adding two fields changes every world
hash. **One re-bless, in this module, with the reason on the constant** — the same discipline W20 and
W37 followed. Doing it here rather than later is deliberate: it is the cheapest point in the program,
before `loam-maps` re-authors the template and moves them a second time for a second reason.

### Validation — new `WorldValidation` rules

1. `FractureIntensityMilli` within `[0, MaxIntensityMilli]` — a negative Fracture is nonsense and an
   unbounded one is an overflow waiting for a multiplication.
2. `LoamStock >= 0` — there is no such thing as owing loam. A shortfall is a *fade*, resolved in
   `loam-turn`; it is never a negative balance.
3. A `rootbed` slot may only appear in a sector type whose `AllowedSlotTypes` includes it — the
   existing catalog rule, extended to the new row.
4. The homeworld must carry at least one `rootbed` — a starting position with no source is a world
   that cannot be played. **Note this is the only rule that mentions the homeworld, and it is about
   playability, not about loam mechanics**: after the S3 resolution, nothing in the loam rules reads
   `Flags.Home` at all.
5. `UpkeepHandicapMilli` within `[MinHandicapMilli, MaxHandicapMilli]` — an unbounded multiplier is an
   overflow, and a zero one is an invulnerable faction.

### Persistence

`EnsureColumn` on the sector table, in the established style: `loam_stock INTEGER NOT NULL DEFAULT 0`
and `fracture_intensity_milli INTEGER NOT NULL DEFAULT 1000`. An existing saved world reads back as
"no stock, baseline Fracture everywhere", which is exactly the pre-loam world and therefore the
correct migration.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/World/WorldState.cs          → two fields on WorldSector, one on WorldFaction
src/FusionRpg.Core/World/SlotTypeCatalog.cs     → SlotKind.Rootbed (appended) + the catalog row
src/FusionRpg.Core/World/SectorTypeCatalog.cs   → rootbed in AllowedSlotTypes where permitted
src/FusionRpg.Core/World/WorldCanonical.cs      → sector fields + the faction handicap
src/FusionRpg.Core/World/WorldValidation.cs     → the five new rules above
src/FusionRpg.Core/World/WorldTemplateCatalog.cs → first-light: homeworld rootbed + starting stock (G-D)
src/FusionRpg.Core/World/Intel/IntelRecorder.cs → intensity into snapshots; stock deliberately not
src/FusionRpg.Data/Sqlite/RpgStore.World.cs     → EnsureColumn + read/write
src/FusionRpg.Contracts/WorldDtos.cs            → intensity always; stock owner-only
tests/                                          → Core.Tests, Data.Tests, Guard.Tests
```

## Code style

Integer only — `WorldDeterminismGuardTests.Game_affecting_world_state_carries_no_floating_point`
enumerates every world source file, so a new folder is covered without touching the guard. Per-mille
for multipliers, plain ints for counts. Records with `init` accessors, `net6`/C# 10 — **no `required`,
no C# 11+ syntax.** Stable ordering everywhere; `WorldValidation` rejects loudly with the offending id
in the message.

## Testing strategy

**Core.Tests** — a world builds twice from the same `(template, seed)` and is canonically identical
with the new fields populated; each new validation rule has a rejecting case; `SlotKind.Rootbed` is
the last enum member (a guard against a future insert); intensity survives a canonical round-trip.

**Data.Tests** — create → reload → deep-equal including both fields; an existing pre-loam row migrates
to `stock 0 / intensity 1000`; `guard-dal` stays green.

**Fog tests** — a faction that has scouted a sector remembers its intensity; a faction that does not
own a sector never receives its stock, in `/state`, in the turn report, or in an intel snapshot. This
is a **property test over every projection**, following W22's shape, not a spot check on one endpoint.

**Golden** — exactly one re-bless, reason recorded on the constant, and the store-versus-engine replay
assertion still passes across it. That replay is the assertion that actually matters.

## Boundaries

- **Always:** integer math; append enum members; one re-bless with its reason; validation before the
  transaction; fog rules asserted as properties, not spot-checked.
- **Ask first:** adding a fourth field to `WorldSector` (the model is hashed and hot — two is the
  budget this module asked for); changing `SlotKind` ordering; anything that would move a golden a
  second time.
- **Never:** floats in world state; SQL outside `FusionRpg.Data`; leaking `LoamStock` to a
  non-owner; giving loam any behaviour in this module — production, upkeep and fade all belong to
  `loam-calc` and `loam-turn`, and a "small" helper here is how a module becomes two.

## Success criteria

1. A world with rootbeds and a non-uniform intensity gradient can be authored, saved, and reloaded
   byte-identically.
2. Every new validation rule has a test that fails without it.
3. No faction ever learns another's `LoamStock`, proven over every projection rather than one.
4. Exactly one golden re-bless, with its reason on the constant.
5. `RulesetVersion` is **unchanged** — this module adds state, not behaviour, so nothing about how a
   turn resolves has moved.

## Decided (2026-08-23) — these were recommendations wearing question marks

- **`MaxIntensityMilli` = 3000.** Three times baseline. Past that, intensity dominates every other
  term in the upkeep formula and the other inputs stop being able to matter — a multiplier that can
  drown its own operands is not a gradient, it is a switch. Implemented at L3 as
  `WorldValidation.MaxIntensityMilli`.
- **`LoamCapacity` stays a policy constant, not a field.** It arrives with the granary that varies it.
  A column holding the same number in every row is not data.
- **`UpkeepHandicapMilli` bounds: `[1, 3000]`** (decided at L3, same reasoning as intensity's
  ceiling — generous until `loam-calc`'s harness has an opinion). Zero would be an invulnerable
  faction; the floor is 1, not 0.
