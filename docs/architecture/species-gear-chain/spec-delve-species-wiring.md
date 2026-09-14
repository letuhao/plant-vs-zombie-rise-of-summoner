# Spec: Delve species wiring (`delve-species-wiring`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `delve-species-wiring`
**Owning program:** `party-dungeon`
**Depends on:** `threat-band-fill`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [species-selection-ideal.md](../species-selection-ideal.md) § The shape 2

---

## Objective

**Give a selector that is already right a production caller — and fix the id bug that would bite the
moment it has one.**

This module writes almost no selection logic. `Encounter`/`SlotFill` already has climate weighting,
an anti-repeat same-species cap, and a real anchor pool. What it does not have is anyone calling it.

⛔ **And it carries the one genuine correctness bug in this initiative**, measured below.

---

## What exists today — verified against code

### Built

- **The selector is complete.** `src/FusionRpg.Core/Delve/Encounter/` ships `Encounter`, `SlotFill`,
  `SlotFilter`, `EncounterCorpusBuilder`, `EncounterCoverage`, `EncounterPreflight`,
  `EncounterRefusal`, `EncounterSeedFile` — a full selection subsystem with its own refusal type.
- **`EncounterRefusal` is a deliberate, named vocabulary** for a slot's filter tuple — the selector
  already distinguishes *"no species matches this slot"* from a programming error.
- **`ConcreteAnchor.From`** joins an expanded species back to its source anchor and throws
  `InvalidOperationException` on a mismatch — *"the same 'not a known X' convention `SpeciesExpander`
  already uses."*
- **The refusal on a null `ThreatBand` is correct**, which is what makes `threat-band-fill` a genuine
  prerequisite rather than a parallel task. `SlotFilter.ThreatRung` is `int?` and the selector
  declines rather than guessing.
- `EncounterTuning` already parses the ten threat rung ids and `threatWindow.bossFloorRung`.

### Wiring gap — self-documented in the repo

`src/FusionRpg.Server/DelveBattleSessionManager.cs:21` records it plainly:

> *"no production callers of `DelveBattle.Run` / `Encounter.Build` exist anywhere today, confirmed by
> a direct [search]"*

`Encounter.Build` (`Encounter.cs:91`) is referenced only by `DomainEncounterCoverage` (a coverage
reporter) and by tests. **The selector runs in analysis and never in a game.**

### ⛔ Real gap — a measured id collision, not a "latent divergence"

`CreatureTypeId` is computed at three sites. **Two agree; one does not.**

| Site | Formula |
|---|---|
| `Creatures/Generation/ConcreteSpeciesSeedReader.cs:91` | `CreatureTypeIdFloor + (Side == "plant" ? 50_000 : 0) + GameTypeId` |
| `Creatures/Generation/CreatureSpeciesGenerator.cs:74` | `CreatureTypeIdFloor + (Side == "plant" ? 50_000 : 0) + TypeId` |
| ⛔ `Delve/Encounter/SlotFilter.cs:49` | `CreatureTypeIdFloor + GameTypeId` — **no plant offset, no side discrimination** |

The ideal called this *"a latent divergence."* **Measuring it makes it concrete:**

> Across the 904-species catalog there are **677 distinct plant `gameTypeId`s and 227 zombie ones**,
> and **102 values appear on both sides** (0, 1, 2, 3, … ).

⭐ **So `SlotFilter`'s formula maps 102 plant/zombie pairs onto the same `CreatureTypeId`.** A plant
and a zombie sharing `gameTypeId = 7` both compute `10,007`. This is not a value that is merely
*different* from the catalog's — it is a value that is **not unique**, in a field used as an identity.

⚠ **Narrowed after re-checking.** The catalog **already enforces uniqueness at load** —
`CreatureSpeciesCatalog.cs:110-111` throws *"Duplicate creatureTypeId {id} ('{speciesId}')"*, and
`:108-109` throws below the floor. An earlier draft said *"nothing checks uniqueness at this site"*,
which read as though the catalog were unguarded.

**The real gap is narrower and still real:** `SlotFilter.CreatureTypeId` is a **computed property**
that never passes through the catalog's guard. The catalog is safe; the delve selector computes its
own colliding value beside it.

⚠ **It does not bite today only because nothing calls the selector.** Wiring a caller is exactly what
makes it bite — which is why the fix and the wiring belong in the same change.

---

## Design

### 1. Fix `SlotFilter.CreatureTypeId` first, in its own commit

Adopt the two agreeing sites' formula. `SlotFilter` does not currently carry `Side`, so this needs the
side available on the filter — which it can take from the anchor it is already built from.

⚠ **The offset must not be restated a fourth time.** Three copies of
`(Side == "plant" ? 50_000 : 0)` is already two too many. **Extract it to one function on
`CreatureSpeciesCatalog` and have all three sites call it** — the same T-2 discipline
`tier-propagation-contract` applies to ladders, applied to an id formula.

### 2. Wire a production caller

The Delve's room resolution calls `Encounter.Build` for a real room and uses its result. This is the
module's actual feature work, and it is small because the selector is complete.

**The caller must surface `EncounterRefusal` rather than swallowing it.** A room that cannot be filled
is a real, reportable condition — and given `threat-band-fill` is the prerequisite, an early refusal
is the signal that the prerequisite did not land, not a reason to fall back to a default species.

### 3. Do not copy the wave's acquisition rule

The Delve is a **third** context, and its admission rule is neither the wave's nor the map's. This
spec does **not** decide it — see Open question 2 — but it records that assuming either would be wrong.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Delve`, `FusionRpg.Server`), xUnit. No new dependency.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Encounter"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CreatureTypeId"
dotnet test tests/FusionRpg.Server.Tests
.\scripts\guard-actor-hub.ps1
```

⚠ `E2E.Tests` has a known pre-existing failure cluster (dungeon registry copy rule). Confirm the
baseline before attributing a failure to this change.

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Delve/Encounter/SlotFilter.cs:49` | The collision fix |
| `src/FusionRpg.Core/Creatures/CreatureSpeciesCatalog.cs` | The single offset function all three sites call |
| `src/FusionRpg.Core/Creatures/Generation/ConcreteSpeciesSeedReader.cs:91`, `CreatureSpeciesGenerator.cs:74` | Converted to call it |
| `src/FusionRpg.Server/DelveBattleSessionManager.cs` | The production caller |

## Code style

One formula, one place, with the collision named so nobody re-inlines it:

```csharp
/// <summary>The disjoint creature id space. The plant offset is NOT optional: without it a plant
/// and a zombie sharing a gameTypeId compute the SAME id — measured, 102 colliding gameTypeIds
/// across the shipped catalog. Three sites computed this independently and one omitted the offset;
/// this is the one place it lives now.</summary>
public static int CreatureTypeIdFor(string side, int gameTypeId) =>
    CreatureTypeIdFloor + (string.Equals(side, "plant", StringComparison.Ordinal) ? PlantOffset : 0) + gameTypeId;
```

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| `offClimateMilli` | Off-climate down-weighting (on 1000 / off `offClimateMilli`) | **Already shipped** in dungeon tuning — becomes live when the caller exists |
| `sameSpeciesMaxMilli` | The anti-repeat cap | **Already shipped**, same file |
| `threatWindow.bossFloorRung` | Which rung a boss floor draws from | **Already shipped** in `encounter.v1.json` |

⭐ **This module authors no new balance number.** Its whole value is making three shipped numbers
live for the first time.

**Structural (stays `const`, with a comment saying why):** `CreatureTypeIdFloor = 10_000` and the
plant offset — they define a **disjoint id space**, which is a structural partition, not a balance
number. The catalog's below-floor throw is the enforcement.

## Numeric types

`CreatureTypeId` is an identity `int`, not a magnitude — it never scales with progression, and the
floor plus offset keep it far below any `int` boundary. **Stated explicitly so no reader assumes the
magnitude rules were overlooked:** the rules do not apply here because this is an id, and widening it
to `long` would change a persisted key for no benefit.

⚠ Selected species' **stats** are magnitudes and are `long`, composed downstream by paths this module
does not touch.

## ActorHub gate

**N/A and checked.** Selecting which species fills an encounter slot produces no actor combat /
derived / AppliedCombat number. The selected creature's stats compose through the existing
`ActorHub` path.

⛔ **One caution specific to this module:** the Delve runs `BattleEngine`, which inherits the
`BattleStatComposer` dual-compose **debt**. ⚠ **§2.15 requires the remediation program to be named,
so it is: `FUSE-battle-hub`** (`decisions.md:51`, owed and unwritten). **This module adds a *caller*,
not a contributor** — it selects which species fills a slot and composes nothing — so it does not
deepen the fork.

That debt is grandfathered **until the fusion `/spec`**
and is **never permission for a new parallel composer** on this path. `guard-actor-hub.ps1` must stay
green, and this module must not deepen the fork — it selects species; it composes nothing.

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit | ⭐ **`CreatureTypeId` is unique across the whole catalog** — the test that makes the 102-way collision impossible to reintroduce. Assert **uniqueness**, a contract, not a count |
| Unit | All three sites produce identical ids for identical `(side, gameTypeId)` inputs |
| Unit | A plant and a zombie sharing a `gameTypeId` get **different** ids |
| Unit | The selector refuses (not throws, not defaults) on a null `ThreatBand` — the existing correct behaviour, pinned |
| Unit | Climate weighting and the anti-repeat cap are live through the production caller |
| Unit | Determinism — the same seed yields the same encounter |
| Integration | A real delve room resolves through `Encounter.Build` and produces a battle |
| Integration | An `EncounterRefusal` **surfaces** and is reported, never swallowed into a default species |

⛔ **No test asserts a species count.** Uniqueness and refusal behaviour are contracts; roster reach
is a reading.

## Boundaries

**Always**
- Fix the id collision **before or with** the wiring, never after.
- Keep the offset in one function; three copies is already the defect.
- Surface `EncounterRefusal`; a refusal is information.
- Confirm the `E2E.Tests` baseline before attributing a failure.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- The Delve's acquisition-admission rule (Open question 2) — it is a third context, and copying
  either existing rule would be a guess.
- Any change to `CreatureTypeIdFloor` or the plant offset — they are a persisted id space, and
  changing either is a migration.

**Never**
- ⛔ Wire the caller while `SlotFilter.cs:49` still omits the offset. That is shipping a 102-way id
  collision into a live path.
- Default a species when the selector refuses. A refusal means the prerequisite did not land.
- Copy the wave's acquisition filter (or the map's) without deciding.
- Add a parallel composer on the `BattleEngine` path — the dual-compose is **debt**, not a template.
- Ship before `threat-band-fill`; the selector will correctly refuse everything.
- Assert a population count.

## Success criteria

1. ⭐ `CreatureTypeId` is unique across the full catalog, enforced by a test.
2. The plant offset lives in exactly one function, called by all three sites.
3. `Encounter.Build` has a production caller and a real delve room resolves through it.
4. `EncounterRefusal` surfaces and is reported.
5. `offClimateMilli`, `sameSpeciesMaxMilli` and `threatWindow` are live for the first time.
6. Encounter selection is deterministic for a given seed.
7. `guard-actor-hub.ps1` green; no new composer.
8. Core and Server suites green against a confirmed baseline.

## Open questions

1. **Does the offset extraction land here or in `tier-propagation-contract`?** It is the same
   no-restatement discipline. **Recommendation: here** — it is a correctness fix with a measured
   collision, and coupling it to a refactor module would delay it behind a broader review.
2. **What is the Delve's acquisition-admission rule?** Waves refuse `CaptureOnly`; the map admits it.
   A delve room is a third context. **Recommendation: admit `CaptureOnly`, refuse `EventOnly`** —
   matching the map, since a delve is somewhere the player travels to and an encounter there is a
   plausible capture route. But it is a real decision, not a default.
3. **Does the Delve share the wave/map slot tables?** **Recommendation: no** — `SlotFill` already has
   its own anchor pool, climate weighting and anti-repeat cap, and replacing that with a shared table
   would discard working machinery to gain uniformity nobody asked for.
