# Spec: `rate-authoring`

**Module id:** `rate-authoring` · **Program:** [item](../item-map.md) · **Filed via
[drop-tables-map.md](../drop-tables-map.md)** (cross-program initiative) · **Build order:** 2 of 4 in
the drop-tables map (moved ahead of the two gameplay-mode modules per owner decision)
**Depends on:** `rate-floor` (item, hard — shares `DropRateFloorTuning`/`MinRatePerMillion`)
**Source:** `docs/architecture/drop-tables-ideal.md` §8 (audit follow-up, 2026-09-07). Owner's own
framing for wanting this module: *"this will help us extend drop tables for each game mechanism
easier... still have a lot of drop tables on quests, event."*

## Objective

`rate-floor` (module 1) only **refuses** an entry configured rarer than the floor — it gives a
designer no way to **place** an entry at a chosen rate on purpose. This module is the authoring-side
counterpart, built as a standalone, reusable mechanism so every future gameplay mode's drop tables
(quests, events, and whatever comes after party-dungeon/base-defense/world-map) can adopt one real way
to author "this specific thing is rare on purpose," rather than each program inventing its own.

## Design — a real fork, resolved with a stated reason, not picked by default

**A plain drop-table `Weight` cannot durably express "this entry is at exactly X rate," and that
matters precisely because this module exists to be reused across many programs that will keep adding
content to the same tables over time.** A weighted draw's share is *relative* — adding or removing a
single sibling entry from a group changes every existing entry's effective rate, even though nobody
touched that entry's own `Weight`. An authoring tool built only as "solve for the `Weight` that hits
rate X today" would silently drift the moment quests, events, or any other consumer adds a new entry to
that same group later — the exact kind of fragile, hand-derived number this whole initiative exists to
replace with something durable.

**This codebase already has the right shape for a rate that must stay exact regardless of what else is
in the table: D38's kill-drop roll.** `spec-drop-volume.md` D38 (owner-decided): *"Does anything drop
at all?"* is `DropChanceOnKillMilli`, a **flat, tunable, independent roll** — a *separate* check from
"which rung," on its own named stream, never reweighted by anything else in the pipeline. This module
reuses that exact pattern rather than inventing a second one.

### Mechanism 1 — `IndependentRateEntry`, the recommended default for anything meant to be exact

```csharp
// src/FusionRpg.Core/Items/Drops/RateAuthoring.cs — new
namespace FusionRpg.Core.Items.Drops;

/// <summary>An entry checked on its OWN named stream, at a fixed rate, decoupled from its group's
/// weighted draw entirely — mirrors D38's kill-roll shape (DropVolume.cs, "does anything drop" is its
/// own roll, separate from "which rung"). Adding or removing any other entry from the same table never
/// moves this one's rate, which a plain Weight cannot guarantee.</summary>
public sealed record IndependentRateEntry(string RefId, long RatePerMillion);

public static class RateAuthoring
{
    /// <summary>checked long, same discipline as DropRateFloor. Refuses at construction/validation
    /// time (via DropRateFloor.ValidateEntry-shaped checks) if RatePerMillion is below
    /// DropRateFloorTuning.MinRatePerMillion — the floor and the authoring tool share one convention,
    /// never two.</summary>
    public static bool Hit(IndependentRateEntry entry, ulong rollSeed, string streamName)
    {
        // Named stream per entry, matching AtomStreams/SeededRng.DeriveStream's own established
        // per-system-stream discipline (Battle/SeededRng.cs:7 — "an extra roll in one system never
        // shifts another"). Exact RNG call signature confirmed against the real AtomRandom/
        // SeededRng API at implementation time, not guessed here.
        var rng = /* AtomRandom or SeededRng.DeriveStream(rollSeed, streamName), whichever the real
                     call site already uses for its own table draw */;
        return /* rng's own per-million roll */ < entry.RatePerMillion;
    }
}
```

**How a caller uses it**: check `RateAuthoring.Hit` once, on its own stream, at whatever point the
table's normal weighted draw already happens — if it hits, grant the independent entry *in addition to
or instead of* (the caller's own design choice, not this module's) the group's normal draw. The rate
never moves no matter how many ordinary entries a future content pass adds to the same table.

### Mechanism 2 — `WeightForRate`, a secondary convenience for entries that are OK drifting with their group

```csharp
public static class RateAuthoring
{
    /// <summary>Solves for the Weight that lands a GROUP-MEMBER entry at targetRatePerMillion given
    /// the group's CURRENT total weight (excluding the entry being solved for). Correct only until a
    /// sibling's weight changes — this is why Mechanism 1 is the recommended default for anything a
    /// designer wants to stay exact; this is for an entry that is fine moving with its group (e.g. "make
    /// this roughly as rare as the floor, among today's other entries," not "keep this exactly the
    /// floor forever").</summary>
    public static long WeightForRate(long targetRatePerMillion, long otherEntriesTotalWeight, DropRateFloorTuning tuning)
    {
        if (targetRatePerMillion < tuning.MinRatePerMillion)
            throw new ArgumentOutOfRangeException(nameof(targetRatePerMillion),
                $"cannot author below the floor of {tuning.MinRatePerMillion} per million");
        if (targetRatePerMillion >= 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(targetRatePerMillion),
                "a target rate must be a real fraction of the group, below 1,000,000 per million");

        // weight / (weight + otherTotal) = target / 1_000_000
        // => weight = target * otherTotal / (1_000_000 - target)
        checked
        {
            return targetRatePerMillion * otherEntriesTotalWeight / (1_000_000L - targetRatePerMillion);
        }
    }
}
```

This is a **design-time calculator**, not a runtime draw mechanism — it hands a content author the
exact `Weight` integer to write into the JSON, given today's group total. It does not, and cannot,
guarantee the rate stays exact after the fact — the doc comment says so, and `rate-floor`'s own import
validation is what catches a rate that has since drifted below the floor.

### Which mechanism a new gameplay mode should reach for

| Need | Mechanism |
|---|---|
| "This exact item/effect should be rare at a fixed rate, forever, regardless of what else gets added to this table" (a jackpot, a mythic-tier reward) | `IndependentRateEntry`/`Hit` |
| "This entry should sit near the floor among today's other entries, and re-tuning later is fine" | `WeightForRate` |

Quests and events (named by the owner as upcoming consumers) should default to `IndependentRateEntry`
for anything actually marketed as an ultra-rare reward, and `WeightForRate` only for ordinary
rebalancing within an already-authored table.

## Data shape

| Item | Change |
|---|---|
| `src/FusionRpg.Core/Items/Drops/RateAuthoring.cs` | **new** — both mechanisms |
| `DropTableEntryRow`-consuming draw code (e.g. `DropTableModel.Draw`) | **+1 optional path**: a caller may check `IndependentRateEntry.Hit` alongside the existing weighted draw — no change to the existing weighted-draw logic itself |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~RateAuthoring"
```

## Project structure

```text
src/FusionRpg.Core/Items/Drops/RateAuthoring.cs          new
tests/FusionRpg.Core.Tests/Items/RateAuthoringTests.cs   new
```

## Code style

`long`/`checked` throughout, matching `DropRateFloor.cs` exactly. `IndependentRateEntry.RatePerMillion`
carries its unit in the field name (T6), read from tuning, never inline (T1/T5). No `System.Random`,
no clock — reproducible from a seed, matching every other roll in this pipeline.

## Testing strategy

| Test | Asserts |
|---|---|
| `an_independent_entrys_rate_never_moves_when_a_sibling_entry_is_added` | the core property mechanism 1 exists for — add a new, unrelated group entry, re-run `Hit` at the same seed, same result |
| `weight_for_rate_solves_the_inverse_of_the_real_draw_formula` | `WeightForRate(target, otherTotal, tuning)` fed back into `entry.Weight * 1_000_000 / (entry.Weight + otherTotal)` reproduces `target` (within integer-rounding tolerance, named explicitly) |
| `weight_for_rate_refuses_a_target_below_the_floor` | shares `DropRateFloorTuning.MinRatePerMillion`, never a second, drifted copy of the same number |
| `hit_is_reproducible_for_the_same_seed_and_stream_name` | no `System.Random`, matches this program's determinism discipline everywhere else |
| `an_independent_entry_at_exactly_the_floor_is_authorable` | the actual scenario the owner asked for — a real 0.0001% entry, placed on purpose, proven end to end |

## Boundaries

**Always:** share `DropRateFloorTuning` with `rate-floor`, never a second copy of `MinRatePerMillion`;
recommend `IndependentRateEntry` as the default for anything meant to stay exact; document
`WeightForRate`'s own drift limitation next to the function, not only in this spec.

**Ask first:** whether an `IndependentRateEntry` hit should be additive (grants alongside the group's
normal draw) or exclusive (replaces it) by default — this module provides the mechanism, not the
per-table policy; that is each gameplay mode's own call when it authors a table.

**Never:** let `WeightForRate` silently ship as the ONLY mechanism — the whole reason this module has
two is that a solved-for weight is not durable across future content additions, which is exactly what
the owner's own "quests, events" framing implies will keep happening.

## Success criteria

- [ ] `IndependentRateEntry`/`Hit` exists, is reproducible, and its rate is proven unaffected by adding
      or removing sibling entries from the same table.
- [ ] `WeightForRate` exists, its inverse-formula correctness is proven, and its own doc comment states
      the drift limitation plainly.
- [ ] Both mechanisms share one tunable floor with `rate-floor` — no second `MinRatePerMillion`.
- [ ] At least one real, shipped entry (in whichever table lands first — likely `sector-loot-wiring`'s
      or `siege-loot`'s own per-type/per-tier tables, built after this module per the revised build
      order) uses `IndependentRateEntry` at or near the floor, proving the mechanism end to end rather
      than shipping as an unused library function.
