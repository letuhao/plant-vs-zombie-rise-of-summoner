# Spec: `rate-floor`

**Module id:** `rate-floor` · **Program:** [item](../item-map.md) · **New module, filed via
[drop-tables-map.md](../drop-tables-map.md)** (cross-program initiative, this module is item's own
piece) · **Build order:** 1 of 4 in the drop-tables map
**Depends on:** nothing new — reads the shipped `drop-volume` module (11) as-is
**Source:** `docs/architecture/drop-tables-ideal.md` §4 R3/R4, decided D2 (2026-09-07): *"the 0.0001%
floor is a per-entry floor, independent of the rarity ladder. No new rung, no ladder review."*

## Objective

Give every drop-table entry, in every table, a single, tunable, global **floor** on how rare it may
ever be configured to be — expressed finely enough to name **0.0001% (1-in-1,000,000)** without the
authoring convention rounding it to zero. Refuse, at import, any entry whose configuration would make
it rarer than the floor. This protects against an author (or a future generator) accidentally shipping
an effectively-unreachable drop, the same failure class every genre precedent in
`drop-tables-ideal.md` §5.2 confirmed no shipped AAA title risks by staying *more common* than
1-in-a-million even for its rarest item.

**Not in scope, by owner decision:** a new rarity rung (`ssot-rarity.md`'s ten-rung ladder is untouched
— §3.4 already measured why an eleventh rung breaks legibility, and D2 explicitly avoids reopening
that). This module never touches `item-rarity.v1.json`'s `dropWeightPer100k` table or the rarity draw
at all — it is a **drop-table entry** concept (`DropTableModel.cs`), not a **rarity rung** concept
(`LootPity.cs`/`RarityDraw`).

## Design

### Why the existing convention can't already express this

`data/seed/items/_registry/bands.v1.json`'s `dropBand.resolvesTo` field states the shipped convention
in words: *"a plain positive integer weight — never per-mille... Weights are plain integers out of
100,000."* The finest named weight in the shipped `weightTable` is `exceptional: 7` (out of that
~100,000-scale total) — three orders of magnitude coarser than 1-in-1,000,000. `bands.v1.json` is
**FROZEN v1** (`immutability` field, own words: *"never renumbered, rescaled, or reused... after
[freeze]"*) — this module does not touch it. The underlying draw math has no such ceiling: `Weight` on
`DropTableEntryRow` is a plain `int`, summed via a `checked long` cursor
(`DropTableModel.cs:195,219,231,261,271`), so nothing stops an author setting `Weight = 1` against a
`groupTotal` of `1_000_000` today. **The gap is entirely in the naming/validation layer — there is no
tunable floor and nothing checks for one.**

### The mechanism — one tunable, one validator check, applied universally

```jsonc
// data/tuning/drop-rate-floor.v1.json — new
{
  "schemaVersion": 1,
  "version": 1,
  "_meta": {
    "owner": "docs/architecture/drop-tables-ideal.md",
    "note": "The rarest any drop-table entry may ever be configured to be. A balance pass may tighten "
          + "or loosen this; it is never a code constant (tunables-ssot.md T1)."
  },
  "minRatePerMillion": 1
}
```

`minRatePerMillion: 1` **is** 0.0001% (1 / 1,000,000). The unit is explicit in the field name (T6).
Tightening it later (e.g. to `2` — no rarer than 1-in-500,000) or loosening it (e.g. to `0` — no floor
at all, ask-first, see Boundaries) is a config change, never a code change.

```csharp
// src/FusionRpg.Core/Items/Drops/DropRateFloor.cs — new
namespace FusionRpg.Core.Items.Drops;

public sealed record DropRateFloorTuning(long MinRatePerMillion);

public static class DropRateFloorTuningLoader
{
    public static DropRateFloorTuning Parse(string json) { /* T5: missing key is a load rejection */ }
}

public static class DropRateFloor
{
    /// <summary>
    /// checked long throughout (CLAUDE.md numeric rules) — widen before multiplying, divide once.
    /// Refuses rather than rounds: an entry whose weight would resolve to a rate below the floor is a
    /// content-authoring mistake, not a value to silently clamp up (the exact "your gear stopped
    /// mattering with no symptom" failure CLAUDE.md's caps section names for a silent clamp).
    ///
    /// <para>⚠ Takes the CALLER-COMPUTED effective weight, never the raw <c>entry.Weight</c> field —
    /// found in review: a version of this signature that took the raw field let an implementer
    /// evaluate an entry against a breakpoint where that entry is not even in-band (its own effective
    /// weight is 0 there), which is not a rate at all. The caller (the walk in
    /// <see cref="ValidateGroupAcrossBreakpoints"/> below) is the only place `EffectiveWeight` is
    /// computed; this method never re-derives it.</para>
    /// </summary>
    public static AtomRejection ValidateEntry(
        DropTableEntryRow entry, long entryEffectiveWeight, long groupTotalWeight, DropRateFloorTuning tuning)
    {
        // Enabled==false and Weight<=0 both already zero out EffectiveWeight upstream
        // (DropTableModel.cs's own "row kept, never drawn" rule) — an inert row is not this check's
        // job, and re-testing `entry.Enabled`/`entry.Weight` here would double-report what
        // DropTableValidator's own existing rules already cover.
        if (entryEffectiveWeight <= 0) return AtomRejection.Ok;
        if (groupTotalWeight <= 0)
            return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                "a group's total weight must be positive to evaluate a rate floor");

        long ratePerMillion = checked(entryEffectiveWeight * 1_000_000L) / groupTotalWeight;
        if (ratePerMillion < tuning.MinRatePerMillion)
            return AtomRejection.ContentRule("drop.rate-below-floor",
                $"entry '{entry.RefId}' resolves to {ratePerMillion} per million within its group " +
                $"(effective weight {entryEffectiveWeight} / group total {groupTotalWeight}), below the " +
                $"configured floor of {tuning.MinRatePerMillion} per million — widen the weight, shrink " +
                "the group's other weights, or split it into its own table");
        return AtomRejection.Ok;
    }
}
```

**Applied universally, not opt-in per entry.** Every entry in every group is checked — matching the
user's own framing ("the lowest drop cap," a single system-wide floor, not a per-entry marker a content
author might forget to set). An entry that is *already* well above the floor (the overwhelming
majority of the shipped corpus) pays one cheap `checked` division and passes.

**⚠ Named limitation, found in review, not silently omitted: a single-entry group always passes
trivially.** When an entry is alone in its group, `groupTotalWeight == entryEffectiveWeight` by
construction, so `ratePerMillion` is always exactly `1,000,000` regardless of the authored weight. This
gives zero signal for that shape — an author relying on this check to catch an accidentally-too-rare
*solo* entry gets no protection at all. Confirmed against real shipped data: every currency group in
`data/seed/loot/tables.v1.json` (e.g. the entries around lines 416/439/467/522/577/632/671/715/738) is
single-entry, so this check is a structural no-op for all of them today. Not a reason to add scope here
— multi-entry groups are what the check protects — but Boundaries and Success Criteria say this
explicitly rather than implying blanket coverage.

**⚠ Present-day risk to the real corpus, measured, not assumed: essentially none.** The narrowest real
shipped multi-entry group found in review, `drop.shared.hybrid-core-any` (24 entries, weight 1 each,
`DropTableModel.cs:264-267`'s own calibration comment), resolves to `1,000,000 / 24 ≈ 41,667` per
million — four orders of magnitude above `minRatePerMillion: 1`. No table in the shipped corpus is
remotely close to tripping this check. The exposure this module guards against is entirely
**prospective** (future content authored without checking the floor), and Success Criteria says so.

### ⚠ The item-level band complication — named, not hidden, and the breakpoint set corrected in review

`DropTableEntryRow`'s `min_ilvl`/`max_ilvl` gate an entry in or out of a draw entirely
(`DropTableModel.cs`'s `EffectiveWeight(entry, itemLevel)` returns `0` outside the entry's band, its own
authored `Weight` inside it, inclusive on both ends). A group's **total** weight therefore varies by
item level — which other entries are simultaneously in-band changes at every ilvl breakpoint.
**Validating against a single, static, authored `Weight`/declared-group-total is not sufficient.**

**The breakpoint set is `{every entry's MinIlvl} ∪ {every entry's MaxIlvl + 1}` — NOT `{MinIlvl,
MaxIlvl}` as an earlier draft of this section said.** Found in review: `EffectiveWeight`'s band is
inclusive at `MaxIlvl` (the entry is still fully active there), so evaluating only at the literal
`MaxIlvl` value never samples the ilvl one step later, which is exactly where a sibling entry drops out
of band and the surviving entry's group share spikes. An implementation that breakpoints on bare
`{MinIlvl, MaxIlvl}` values will silently miss the one ilvl where the rate is actually at its most
extreme, and — worse — the flagship test named below (`the_floor_is_evaluated_at_every_ilvl_breakpoint`)
would pass against that broken implementation for the wrong reason. The walk:

```csharp
// src/FusionRpg.Core/Items/Drops/DropRateFloor.cs — new, ValidateGroupAcrossBreakpoints
public static IReadOnlyList<AtomRejection> ValidateGroupAcrossBreakpoints(
    IReadOnlyList<DropTableEntryRow> group, DropRateFloorTuning tuning)
{
    var breakpoints = new SortedSet<int>();
    foreach (var e in group)
    {
        if (e.MinIlvl is { } lo) breakpoints.Add(lo);
        if (e.MaxIlvl is { } hi) breakpoints.Add(checked(hi + 1));
    }
    if (breakpoints.Count == 0) breakpoints.Add(1); // no entry declares a band -> one static check

    var results = new List<AtomRejection>();
    foreach (var ilvl in breakpoints)
    {
        long groupTotal = 0;
        foreach (var e in group) groupTotal = checked(groupTotal + DropTableModel.EffectiveWeight(e, ilvl));
        foreach (var e in group)
        {
            var eff = DropTableModel.EffectiveWeight(e, ilvl);
            if (eff <= 0) continue; // not in-band at this breakpoint — nothing to validate here
            results.Add(ValidateEntry(e, eff, groupTotal, tuning));
        }
    }
    return results;
}
```

### Where this plugs into the existing pipeline

`DropTableValidator.Validate` (the existing, shipped corpus validator every drop-table import already
runs through) gains one more check per group, alongside its existing weight/group-exclusion checks —
no new call site, no new import step. A table failing this check fails import exactly like every other
`ContentRuleViolated` today (see `spec-drop-volume.md`'s own Validation table for the existing
pattern this reuses verbatim).

## Data shape

| Table/file | Change |
|---|---|
| `data/tuning/drop-rate-floor.v1.json` | **new** — `minRatePerMillion`, T5/T6-compliant |
| `DropTableEntryRow` (`DropTableModel.cs`) | **unchanged** — no new field. The floor is a validation-time computed property, never stored per-row |
| `DropTableValidator.Validate` | **+1 check** per group: every entry's effective rate at every ilvl breakpoint ≥ `MinRatePerMillion` |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DropRateFloor"
python scripts\audit-magic-numbers.py --domain drop-rate-floor
```

## Project structure

```text
src/FusionRpg.Core/Items/Drops/DropRateFloor.cs          new — ValidateEntry, the ilvl-breakpoint walk
src/FusionRpg.Core/Items/Drops/DropRateFloorTuning.cs    new — record + T5-compliant loader
data/tuning/drop-rate-floor.v1.json                      new
tests/FusionRpg.Core.Tests/Items/DropRateFloorTests.cs   new
```

## Code style

`long` throughout (CLAUDE.md numeric rules — this is a ratio computation over potentially large
weights, never `int`/`float`). Widen before multiplying (`checked(entry.Weight * 1_000_000L)`, never
`(entry.Weight * 1_000_000)` cast after). No bare literal `1_000_000` outside the one place that names
the unit conversion — everywhere else reads `tuning.MinRatePerMillion`, itself never defaulted (T5).

## Testing strategy

| Test | Asserts |
|---|---|
| `an_entry_far_above_the_floor_passes` | a normal, common entry (e.g. weight 40700/100000-scale, matching `chaff`'s own real magnitude) passes cheaply |
| `an_entry_exactly_at_the_floor_passes` | `ratePerMillion == MinRatePerMillion` is not rejected — the floor is inclusive |
| `an_entry_one_per_million_below_the_floor_is_refused_by_name` | `drop.rate-below-floor`, naming the computed rate, the weight, and the group total |
| `the_breakpoint_set_is_lo_union_hi_plus_one_not_lo_and_hi` | a table where entry B's band ends at ilvl 10 and entry A's continues past it — the walk must sample ilvl 11 (A alone, highest rate), not just 10 (B still active) — the exact case an earlier draft's `{lo, hi}` breakpoint set would silently miss |
| `entry_a_alone_at_high_ilvl_is_checked_against_its_own_solo_total_not_the_low_ilvl_shared_total` | entry A is only in-band alongside a heavy entry B at low ilvl, and alone (a much smaller group total, so a much *higher* effective rate) at high ilvl — the check must not reject or pass A using the wrong breakpoint's total |
| `zero_or_negative_weight_is_not_this_checks_job` | `DropRateFloor.ValidateEntry` returns Ok for a weight `DropTableValidator`'s own existing rule already rejects — no double-reporting |
| `a_disabled_entry_with_a_real_weight_is_not_checked` | `Enabled == false` zeroes `EffectiveWeight` upstream (`DropTableModel.cs`'s "row kept, never drawn" rule) — a real, currently-supported authoring pattern; this check must not reject an inert placeholder row |
| `a_single_entry_group_always_passes_and_the_doc_says_so` | confirms the named limitation directly: a solo entry's rate is always exactly 1,000,000/million regardless of its authored weight — no false confidence implied anywhere this is asserted |
| `a_missing_tuning_file_is_a_load_rejection_naming_the_key` | T5 — no built-in default of `MinRatePerMillion` |
| `the_floor_never_touches_the_rarity_ladder` | `item-rarity.v1.json`'s `dropWeightPer100k` values are read nowhere in `DropRateFloor.cs` — grep-shaped guard |
| `groupTotalWeight_accumulation_throws_rather_than_wraps_on_an_absurd_sum` | the overflow risk lives in summing many entries' effective weights (`ValidateGroupAcrossBreakpoints`'s `checked(groupTotal + ...)`), never in `entry.Weight * 1_000_000L` alone — an `int Weight` can never approach the multiply's own `long` headroom, so this test targets the real overflow surface, not an unconstructible one |

## Boundaries

**Always:** evaluate the floor at every ilvl breakpoint a table's entries define, never a single static
total; read `MinRatePerMillion` from tuning, never inline; refuse by name (`drop.rate-below-floor`),
never silently widen a weight to comply.

**Ask first:** setting `minRatePerMillion` to `0` (no floor at all) — a real, reachable configuration
this module must not refuse to load, but the owner's own idea doc frames the floor as a deliberate
design decision, so disabling it entirely is a product call, not a default state to fall into
accidentally.

**Never:** touch `item-rarity.v1.json` or `bands.v1.json` — this is a drop-table-entry concept, not a
rarity-rung one (see Objective). Never clamp an under-floor entry's weight upward silently — refuse and
name it, matching CLAUDE.md's "a silent clamp turns 'stopped mattering' into a bug with no symptom."
Never make the floor visible to players (D3, `drop-tables-ideal.md` §7) — this module ships no
player-facing surface at all; it is import-time validation only.

## Success criteria

- [ ] `data/tuning/drop-rate-floor.v1.json` exists, T5/T6-compliant, no default in code.
- [ ] Every shipped drop-table entry in `data/seed/items/drop-tables/*.json` and
      `data/seed/loot/*.json` passes the new check at import (the corpus as it exists today is already
      far above 1-in-a-million everywhere — this is a regression check, not expected to find anything).
- [ ] An entry deliberately authored below the floor is refused by name, not silently accepted or
      clamped.
- [ ] The floor is evaluated at real ilvl breakpoints, proven by a test where a static-total check
      would have given the wrong answer.
- [ ] `item-rarity.v1.json`/`bands.v1.json` are read nowhere in this module's own files.
- [ ] The floor's existence is not referenced anywhere reachable by the web client or item-card display
      (D3).

**Named plainly, found in adversarial review: this module ALONE changes nothing a player will ever
notice.** It is a backend safety rail — a refusal gate preventing an entry from being configured
rarer than the floor — not an authoring tool for *creating* a rare/exciting drop, and (per D2/D3) it
has no player-visible surface at all. The corpus is already far above the floor everywhere (see the
second criterion above), so shipping this module alone changes zero moment-to-moment play today. That
gap is real and was resolved the same day, not left open: `rate-authoring`
(`docs/architecture/item/spec-rate-authoring.md`, module 2 of this map) is the actual authoring-side
counterpart — it depends on this module's own tunable and ships next, so the two together (not this
one alone) are what deliver a designer's ability to place a real entry at the floor on purpose.
