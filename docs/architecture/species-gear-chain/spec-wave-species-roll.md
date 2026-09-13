# Spec: Wave species roll (`wave-species-roll`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `wave-species-roll`
**Owning program:** `creature-lawn-deploy`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [species-selection-ideal.md](../species-selection-ideal.md) § The shape 1

---

## Objective

**Make a wave roll its species instead of taking the first N in alphabetical order.**

This is the cheapest fix in the initiative and the one that restores meaning to a ladder that already
ships. It is also the fix that proves the slot-table shape two other modules will inherit.

**The measurement, reproduced this session by counting the shipped catalog:**

- `WaveCatalog.Band(rarity)` filters the 904-species catalog to one rung and sorts
  `StringComparer.Ordinal` (`WaveCatalog.cs:153-154`). Only **four** rungs are drawn — `Chaff`,
  `Cultivated`, `Heirloom`, `Sunwoven` (`:127-130`) — which is **246 of 904 species** even before the
  pick.
- `Enemies` then picks `pool[i % pool.Count]` (`:165`). **There is no RNG.** `i` starts at 0 and
  `count` never exceeds the pool, so it is *"take the first `count`, alphabetically."*
- Across all four shipped waves the distinct species used are: the first 4 `Chaff`, the first 3
  `Cultivated`, the first 2 `Heirloom`, the first 1 `Sunwoven`.

⭐ **Ten species. 1.1% of the roster, and it is the same ten every run, forever.**

⚠ **With no roll, the rarity ladder is inert at this layer.** A species' rung decides only which
alphabetical bucket it is ignored in. That is the real cost — not variety, but a whole ladder that
means nothing where the player meets it.

---

## What exists today — verified against code, not comments

### Built

- **`data/tuning/waves.v1.json` already exists** and mirrors the four shipped waves with
  `{waveId, name, contentIndex, profile, picks: [{rarity, count}]}`. A data-sourced roster path
  already exists (`WaveCatalog.cs:151-152`: *"widened from `private` for `WaveCatalogLoader`'s own
  use… the data-sourced roster picks by the SAME ordered rarity band, never a second selection
  rule"*). **The tuning file is the extension point, and it is already wired.**
- **`CreatureAcquisition` is a real, closed flags enum** — `None = 0, Summonable = 1, CaptureOnly = 2,
  EventOnly = 4` (`CreatureRarity.cs:32-38`). Measured across the 904 generated species:
  `Summonable` 869, `Summonable, CaptureOnly` 2, `CaptureOnly` 29, `EventOnly` 4.
- **`CreatureRarityLadder`** gives ordinal-safe rung arithmetic, so widening `Band` past four rungs
  needs no bare ordinal comparisons.

### Wiring gap

- `WaveCatalog.cs:165` — `var species = pool[i % pool.Count];` — the missing roll.
- `WaveCatalog.cs:127-130` — four hardcoded `Band(...)` calls.
- `WaveCatalog.cs:153-154` — `Band` applies **no acquisition filter**, so an `EventOnly` species would
  march in a wave the moment its rung is drawn. Today it cannot, only because `EventOnly` species
  happen not to sit in the first N alphabetically. **That is luck, not a rule.**

### ⚠ A stale comment, named

`WaveCatalog.cs:121-126` claims the four bands are complete because *"the 84-species generated
catalog only populates these four rungs today."* **The catalog is 904 species across all ten rungs**
(measured: `Fused` 486, `Cultivated` 159, `Chimeric` 67, `Heirloom` 49, `Sprout` 45, `Chaff` 34,
`Grafted` 33, `Almanac` 23, `Sunwoven` 4, `Firstseed` 4). The comment's own next sentence already
concedes the limit was a migration artefact. **Code beats comments; the comment is corrected, not
trusted.**

Note `Fused` alone holds 486 species — **54% of the roster sits in a rung no wave draws from.**

---

## Tech stack

C# .NET 8 (`FusionRpg.Core`, Unity-free), xUnit. No new dependency. No FE surface.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~WaveCatalog"
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
dotnet test tests/FusionRpg.Guard.Tests
dotnet test tests/FusionRpg.Data.Tests
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Battle/WaveCatalog.cs` | `Band`, `Enemies`, `Build` — the whole change |
| `data/tuning/waves.v1.json` | Gains per-pick weights and a band window; `version` bump |
| `tests/FusionRpg.Core.Tests/Battle/` | Determinism + filter + distribution tests |

## Code style

The roll is seeded and draws without replacement; the filter is a rule, not a coincidence:

```csharp
/// <summary>The pool for one pick: a rarity WINDOW (not a single rung), filtered by acquisition.
/// The filter is the point — an EventOnly species must never march in a wave, and today it is kept
/// out only by alphabetical luck.</summary>
internal static List<CreatureSpeciesDef> Band(CreatureRarity from, CreatureRarity to) =>
    CreatureSpeciesCatalog.All
        .Where(s => CreatureRarityLadder.AtLeast(s.BaseRarity, from)
                 && CreatureRarityLadder.AtMost(s.BaseRarity, to))
        // EventOnly is refused FIRST and UNCONDITIONALLY. A plain HasFlag(Summonable) admits a
        // species carrying Summonable|EventOnly. Zero species carry both today (869/2/29/4), so
        // that filter would be EventOnly-safe BY POPULATION, NOT BY RULE -- which is the exact
        // criticism this spec levels at the shipped pool[i % pool.Count]. Rule, not luck.
        .Where(s => !s.Acquisition.HasFlag(CreatureAcquisition.EventOnly))
        .Where(s => s.Acquisition.HasFlag(CreatureAcquisition.Summonable))
        .OrderBy(s => s.SpeciesId, StringComparer.Ordinal)   // STABLE ORDER FOR THE SEED, not the pick
        .ToList();
```

**`OrderBy` stays.** It stops being the selection rule and becomes the determinism substrate — the
seeded draw must walk a stable order or the same seed yields different species on a different
platform's hash order.

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| Per-pick **rarity window** (`rarityFrom` / `rarityTo`) replacing the single `rarity` key | What widens the wave past four rungs and reaches the 486 `Fused` species | `data/tuning/waves.v1.json` — ⚠ **`version` ADD, not bump** |
| Per-slot weight table (`weightPerMille`) | The long tail. Pokémon's shape — 10–12 slots with a heavy head and a thin tail — is the documented starting point | Same file |
| Slot count per wave | How many independent draws a wave makes | Same file |
| Anti-repeat cap (`sameSpeciesMaxMilli`) | Whether one species may fill two slots in a wave | Same file — ⚠ **the Delve selector already has this name**; reuse the key, do not invent a second |

**Structural (stays `const`, with a comment saying why):** the seeded-RNG stream derivation and the
draw-without-replacement rule. Those are **determinism guarantees**, not balance — a tunable that
could relax them would break reproducibility, which is a different failure than a bad feel.

⚠ **A revision is a `version` field inside the existing file, not a new filename** —
`sockets.v1.json` is at `version: 2` today, and the ideals' `v{n}.json` phrasing would have created
second files.

⚠ **But `waves.v1.json` has no `version` field at all.** Its entire top level is `{"waves": [...]}`
— no `version`, no `schemaVersion`. **So this module ADDS the field rather than bumping it**, and
should add `schemaVersion` at the same time to match the shipped tuning shape. An earlier draft
asserted the bump was "the shipped pattern" for this file; it is the pattern elsewhere, not here.

## Numeric types

Weights are `int` per-mille — a **bounded ratio**, exempt from the magnitude rules and required to say
so in a comment. The draw sums weights across at most a few dozen slots, nowhere near any ceiling.
**No magnitude is produced here:** this module selects *which* species appears; its stats are composed
downstream by paths this module does not touch.

## ActorHub gate

**N/A and checked.** Selecting which species appears produces no actor combat / derived /
AppliedCombat number. The spawned creature's stats compose through the existing `ActorHub` path,
unchanged. No private fold is introduced.

## Testing strategy

| Level | What it asserts |
|---|---|
| Unit | **Determinism** — the same seed yields the same species sequence, twice, and across a shuffled catalog input order |
| Unit | ⭐ **The acquisition filter is a RULE, not a population accident** — construct a species carrying **`Summonable | EventOnly`** and assert it is refused. No shipped species carries both today, so without this case the filter is only EventOnly-safe by luck |
| Unit | An `EventOnly` species placed first alphabetically in a drawn rung is still absent from every wave |
| Unit | `CaptureOnly` is refused in a wave (it is admitted on the **map** — that is `wild-species-spawn`, deliberately a different rule) |
| Unit | A `Summonable, CaptureOnly` species **is** admitted — the flags enum is a bitfield and 2 species carry both |
| Unit | Draw-without-replacement holds within a pick when the anti-repeat cap is 0 |
| Unit | A wave whose window covers a rung with zero occupants does not throw — it draws from the rest of the window |
| Contract | Every `rarity`/`rarityFrom`/`rarityTo` id in `waves.v1.json` is one of the ten (closed vocabulary — pinning is correct) |
| Report | Print the distinct-species count reachable across all waves — **a reading, printed, never asserted** |

⛔ **No test may assert "the player now meets N species."** Roster reach is a reading and grows
whenever content ships. The acceptance condition is *the roll is seeded, filtered and windowed* — the
count is a report.

⚠ **Goldens:** the four wave ids are `rift-*`, and the golden fixtures use `golden-*` wave ids that
are not in this roster (`WaveCatalog.cs:138-144`, a measured claim recorded there). So **this change
should move no golden.** If one moves, that is evidence the isolation claim is stale — investigate,
do not re-bless.

## Boundaries

**Always**
- Keep the ordinal sort as the determinism substrate.
- Read the ladder from its declaration; never restate the rung ids (T-2, `tier-propagation-contract`).
- Run `Category=BalanceGuard` alongside the unit tests.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- A `RulesetVersion` bump. `WaveCatalog.cs:138-144` records that none was needed for the last change
  and **why it was measured rather than assumed**; that reasoning must be re-checked, not inherited.
- Re-blessing any golden.
- Changing the wave `picks` counts — that is wave content, not this module's fix.

**Never**
- Copy `Band`'s missing acquisition filter into a new call site. It is a bug here and would be a bug
  anywhere.
- Apply the wave's acquisition rule to the map. `CaptureOnly` is **admitted** on the map — the wild
  *is* its acquisition route, and refusing it there strands 29 species entirely.
- Use an unseeded RNG anywhere on this path.
- Assert a population count.
- Reach for the injector. The lawn's own spawner is out of scope; gameless-first means this must work
  with Fusion closed.

## Success criteria

1. `WaveCatalog` draws seeded and weighted from a rarity **window**, not `pool[i % pool.Count]`.
2. The same seed reproduces the same wave, proven across a shuffled catalog order.
3. `EventOnly` species are excluded by **rule** — proven with a synthetic `Summonable | EventOnly`
   species, since no shipped species carries both and the population alone would pass a weaker filter.
4. Waves can draw from all ten rungs; the 486-species `Fused` rung is reachable by configuration
   alone.
5. `waves.v1.json` carries the windows and weights; **no weight is a `const` in C#**.
6. `dotnet test` green across Core, Guard and Data; `Category=BalanceGuard` green.
7. No golden re-blessed — or a separate reviewed commit explains which moved and why.
8. The stale four-rung comment at `WaveCatalog.cs:121-126` is corrected or deleted.

## Open questions

1. **Does this ship before or with `threat-band-fill`?** This module needs no `threatBand` — it keys on
   `BaseRarity`. **Recommendation: ship it first**, with `wild-species-spawn`; both are independent
   and cheap, and together they prove the slot-table shape before the Delve inherits it
   (selection ideal, open question 1).
2. **Do the four shipped waves keep their current feel, or widen immediately?** **Recommendation:
   widen the windows but keep the pick counts** — the roll alone changes variety enormously, and
   changing difficulty at the same time makes a regression unattributable.
3. **Slot count per wave — the Pokémon 10–12 shape, or match the existing pick counts?**
   Recommendation: match the existing counts first. The long tail is a weight-table property, not a
   slot-count one, so the tail can be added without changing wave size.
