# Module: `tier-bands-coverage`

Source: `docs/architecture/atom-family-expansion-ideal.md` §4, `docs/architecture/atom-family-expansion-map.md`.

## 1. Objective

Close 98 of the 103 `tools/FamilyExpandGen -- --check` refusals shaped
`"no authored sharePermille for family X (channel stem X not in tier-bands.v1.json)"`, by publishing a
`channelWeightPermille` entry for every one of the 98 families that lacks one today, then re-running
`FamilyExpandGen` for real. No new generation engine, no LLM step — this is a data-authoring module on
top of two already-built tools (`seedsmith numerics rebalance`, `FamilyExpandGen`).

**Out of scope**: the 5 remaining refusals (`no referenceBaseGameUnits` — `arm1Max`, `arm2Max`,
`attackInterval`, `produceInterval`, `zombieSpeed`) belong to the sibling module
`battle-ruleset-curve-extension`. This module's own re-run of `FamilyExpandGen` will still report those
5 as refused, by design — that is not a regression, it is this module correctly not overreaching into
the other module's territory.

## 2. The formula — powerBand → `channelWeightPermille`

**No such mapping exists anywhere in the codebase today** (verified this session — `docs/architecture/item/ssot-uniques.md`
has zero mentions of `powerBand`; the only existing numeric interpretation of the 5-value band enum is
`UniqueBudget.TierOfPowerBand`, an unrelated consumer pricing unique/gem/consumable AE budgets). This
module introduces the first one, for this one purpose.

**The closed band enum** (`data/seed/items/_registry/bands.v1.json`, frozen, "reviewed, versioned
change" to widen): `trivial`, `low`, `medium`, `high`, `extreme` — 2/61/40/8/1 families respectively
across the real 112-entry corpus.

**The formula, chosen to introduce zero new tunable constants**: reuse this project's own single,
already-reviewed tier ratio — `MagnitudeRatioPermille = 1750` (`FamilyExpansion.cs:35`,
`bands.v1.json powerBand.tierScaling`, 1.75× per step) — as the band-to-band step, anchored so `medium`
maps to `1000`:

```
weight(band) = round_legible(1000 * 1750^(bandOrdinal - mediumOrdinal) / 1000^(bandOrdinal - mediumOrdinal))
```

using the SAME ordinal `bands.v1.json powerBand.tierMap` already assigns (`trivial=1, low=2, medium=3,
high=4, extreme=5`) and the SAME `round_legible` (round-half-up on an integer ratio) both
`FamilyExpansion.cs:66-73` and `tools/seedsmith/seedsmith/numerics/formulas.py:24` already implement
identically. Concretely:

| Band | Ordinal offset from medium | Weight (‰) |
|---|---|---|
| trivial | −2 | 327 |
| low | −1 | 571 |
| medium | 0 | 1000 |
| high | +1 | 1750 |
| extreme | +2 | 3063 |

**⛔ Corrected by adversarial audit (2026-09-08) — the anchor claim below was checked empirically and
found materially overstated; both points are load-bearing and neither was true as first written:**

- **"Matches every existing entry exactly" is false.** The real `powerBand` of the 14 already-published
  families is mixed, not uniformly `medium`: 8 are `medium` (reproduced exactly), **4 are `low`**
  (`fortitude`, `ferocity`, `resilience`, `mending` — this formula would compute `571` for these,
  a 42.9% cut from their shipped `1000`), and **2 are `high`** (`bulwark`, `savagery` — this formula
  would compute `1750`, a 75% increase). The formula reproduces **8 of 14**, not all 14.
- **"`medium` is the plurality band" is factually wrong** by this spec's own cited counts two lines
  above: `low` (61 families) is the plurality, not `medium` (40). The anchor choice needs a different,
  honest justification: `medium` is chosen not because it's most common, but because it is the band
  every one of this module's own reference points (the 8 already-shipped `medium` families) already
  sits at — the least-disruptive anchor for the families this formula can be checked against today, not
  the modal one.
- **A real band×op interaction was found, not just a rounding quirk.** `tier-bands.v1.json`'s existing
  `opWeightPermille` table already applies its own multiplier (`Flat`/`Increased` = 1000, `More` = 550
  — `More` is the intentionally-discounted, strongest lever). Composing that with this formula:
  `fortitude`/`ferocity`/`resilience`/`mending` (`low`, `Increased`) would land at `571 × 1000 / 1000 =
  571` — cut hard, on top of an op the table does NOT already discount. `bulwark`/`savagery` (`high`,
  `More`) would land at `1750 × 550 / 1000 ≈ 963` — landing almost exactly back at the uniform `1000`
  baseline, effectively **cancelling `More`'s own intended discount**. The two axes were authored
  independently and were never checked to compose correctly; they don't. **This is the module's one
  genuinely open, unresolved question — see §7.**

**Acceptance criteria for this formula:**
- [ ] `weight("medium") == 1000` exactly.
- [ ] Each step is exactly ±1750‰/1000 from its neighbor, using `round_legible`, not float rounding.
- [ ] The formula is expressed as a pure function taking a band name and returning an int, unit-tested
      independently of any file I/O.
- [ ] **Reproduces exactly 8 of the 14 existing entries** (the `medium`-band ones) — a test naming the
      other 6 explicitly and asserting the formula's own computed value for them, so the divergence is
      proven and visible, never silently reintroduced as a false "matches all 14" claim.

## 3. Commands / project structure

**New file**: `tools/seedsmith/seedsmith/numerics/channel_weight_backfill.py` — a small, standalone
module (not an `adapters/items/` adapter — this is purely a `tier-bands.v{n}.json` numerics concern,
same layer as `rebalance.py`/`tier_bands_io.py`, not an affix-authoring one):

- `WEIGHT_BY_BAND: dict[str, int]` — the 5-row table above, computed once at import time from
  `bands.v1.json`'s own ordinal map and `MAGNITUDE_RATIO_PERMILLE = 1750` (mirrored from
  `FamilyExpansion.cs:35`/`bands.v1.json`, cited, never re-derived), asserted against the 14 real
  existing entries at import time (`assert all(v == 1000 for families whose band is medium and are
  already published)` — a drift guard, matching this codebase's own "mirrored, never hand-copied"
  discipline for closed vocabularies).
- `missing_channel_weights(families: list[FamilyEntry], tuning: TierBands) -> dict[str, int]` — pure:
  for every family stem NOT already a key in `tuning.channel_weight_permille`, look up its `powerBand`
  and emit `{stem: WEIGHT_BY_BAND[band]}`. Never touches a family already covered (additive-only,
  proven by a test that an already-covered family's existing value is provably never read).
- `write_set_file(missing: dict[str, int], path: Path) -> None` — writes a `channelWeight.<id>=<value>`
  line per entry, sorted by id, the exact format `seedsmith numerics rebalance --set-file` already
  reads (`cli.py:1481-1485`) — no new CLI flag, no new parser, reuses the existing one exactly.
- `main(argv)` — CLI: reads every family from `data/seed/items/affix-families/*.json`, the current
  `TierBands.load("latest")`, computes `missing_channel_weights`, writes a `--set-file`-compatible
  temp file, and prints the exact `seedsmith numerics rebalance --set-file <path> --publish` command
  for the operator to run — **does not itself call `--publish`**, matching `rebalance`'s own
  dry-by-default posture (`_cmd_numerics_rebalance`'s own doc: "nothing until publish").

**Files touched at publish time** (by the existing `rebalance --publish`, not by new code):
`data/seed/items/_tuning/tier-bands.v2.json` (new version; v1 stays for revert, per its own `_meta`).

**Files touched by the re-run** (by the existing `FamilyExpandGen`, not by new code):
`data/seed/atoms/generated/family-expand.g-*.json` — every group file whose families are now fully
covered gets written or updated; files whose families still have a refusal (the 5 curve-blocked ones,
or any family sharing a source file with one) are unaffected, matching `FamilyExpandGen`'s own
per-family (not per-file) refusal granularity.

## 4. Code style

Python, matching `tools/seedsmith`'s existing conventions exactly: `from __future__ import annotations`,
dataclasses where the sibling `numerics` modules use them, `Path`-based I/O, no bare literals for the
`1750`/`1000` constants without a citation comment (mirrors `FamilyExpansion.cs`'s own citation
discipline). No new dependency on the C# side — this module reads the same closed `bands.v1.json` band
list `UniqueBudget.TierOfPowerBand` reads, cited, not re-derived.

## 5. Testing strategy

New test file: `tools/seedsmith/tests/test_channel_weight_backfill.py`.

- **Formula**: `WEIGHT_BY_BAND["medium"] == 1000`; each adjacent pair differs by exactly the 1750‰
  ratio via `round_legible`; the 5-value band set matches `bands.v1.json` exactly (a drift guard, not
  a hardcoded re-list).
- **`missing_channel_weights`**: a family already in `tuning.channel_weight_permille` is never present
  in the output, even if its own computed weight would differ from its current value (additive-only —
  proven with a family whose current value is deliberately NOT 1000, to prove this isn't silently
  overwritten). A family missing from `tuning` gets exactly the weight its `powerBand` maps to.
- **Known-divergence test, named explicitly, not buried in a parenthetical**: `fortitude`, `ferocity`,
  `resilience`, `mending` (`low`) and `bulwark`, `savagery` (`high`) already have a published
  `channelWeightPermille` of `1000`, which this formula would NOT have produced for them (`571`/`1750`
  respectively). A dedicated test asserts these 6 stay untouched at `1000` (proving additive-only truly
  holds even where the new formula disagrees with history) and separately asserts what the formula
  *would* compute for each, so the known inconsistency this module ships with is a proven, visible fact
  in the suite — not something a future reader has to reconstruct.
- **Real-corpus regression**: run `missing_channel_weights` against the real 112-family corpus and the
  real `tier-bands.v1.json` — assert exactly 98 entries come back (matching `FamilyExpandGen --check`'s
  own live-measured refusal count this session), and that the 5 known curve-blocked families
  (`plating`/`quickening`/`flourishing`/`swiftness`/`carapace`) are **not** among the 98 — they already
  have `sharePermille` inputs available in principle once curves exist; their refusal is a different
  class entirely, and this module's own output must not be conflated with theirs. *(If this assertion
  fails because one of those 5 lacks BOTH a weight AND a curve, note it here rather than silently
  passing — this module still just publishes a weight for it; the curve gap remains module 2's.)*
- **End-to-end acceptance** (manual/integration step, not a unit test — requires a real `--publish` and
  `FamilyExpandGen` run against a scratch copy of the tuning file, never the real repo state inside a
  test): after publishing the computed weights and re-running `FamilyExpandGen` (no `--check`), a fresh
  `FamilyExpandGen -- --check` reports refusals only in the `no referenceBaseGameUnits` class — zero
  `no authored sharePermille` refusals remain. **Build-order note**: the count of remaining refusals is
  **5 if `battle-ruleset-curve-extension` has not yet landed, 0 if it has** — assert on the *reason*
  (no `no authored sharePermille` refusals), never on a hardcoded count, so this test does not need
  updating regardless of which module lands first.

## 6. The band×op interaction — found by adversarial audit, decided by the owner

`tier-bands.v1.json`'s `channelWeightPermille` and `opWeightPermille` are separate, independently-
authored axes that this spec's formula was never checked to compose correctly with — and having
checked, they don't, for at least the two family clusters below:

- `fortitude`/`ferocity`/`resilience`/`mending` (`low` band, `Increased` op — their own authoring notes
  say `low` was chosen *because* `Increased` compounds) would land at `571‰` under this formula: the
  band-derived cut compounds with an op the `opWeightPermille` table does **not** already discount
  (`Increased` = `1000`, same as `Flat`).
- `bulwark`/`savagery` (`high` band, `More` op — the deliberately rarest, most-restricted lever,
  `opWeightPermille["More"] = 550`) would land at `1750 × 550 / 1000 ≈ 963` — landing almost exactly
  back at the uniform `1000` baseline, **cancelling `More`'s own intended discount**.

The net effect: the formula, applied uniformly, makes the "everyday" `Increased` lever the most heavily
cut and the deliberately-rarest `More` lever nearly unaffected — the reverse of the two axes' own
apparent intent, for the 6 families where the two axes overlap in a way that matters today.

**Decision (owner, 2026-09-08): ship the formula as designed, accept the bias for now.** This matches
`tier-bands.v1.json`'s own stated posture verbatim — *"working values chosen to make the corpus
resolvable, not a validated balance decision... telemetry refits `channelWeight` once gameplay data
exists"* — the same posture the file's 14 uniform-`1000` entries already ship under today. The bias is
real and known, not invisible: §5's "known-divergence test" and this section both name it explicitly,
so it is discoverable rather than silently shipped. **Named follow-up, not this module's own scope**:
once real gameplay telemetry exists, revisit `channelWeightPermille` for `More`-op families
specifically — they are the cluster most likely to need a correction, since this formula currently
under-discounts exactly the lever the game's own tuning intends to restrict hardest.

## 7. Boundaries

- **Always**: publish through `seedsmith numerics rebalance --set-file ... --publish` — never hand-edit
  `tier-bands.v1.json` or write a `tier-bands.v2.json` by any other path (the file's own `_meta.rebalance`
  line is explicit: "Never hand-edit this file").
- **Always**: additive-only against the 14 existing entries — never recompute or overwrite a value
  that's already published, even if the formula would produce a different number for it.
- **Ask first**: before running the real `--publish` against the actual repo `tier-bands.v1.json` (an
  action with real, if reversible, effect on shipped tuning) — dry-run (`rebalance` with no `--publish`)
  first and show the diff for review.
- **Never**: touch `power-scale.v2.json`, `BattleModels.cs`'s `BattleRuleset` class, or any
  `ssot-power-scale.md` inventory row — that is `battle-ruleset-curve-extension`'s territory, not this
  module's.
- **Never**: invent a new ratio or anchor other than the one derived here (medium=1000, step=1750‰) —
  if that formula is later found wrong, it is a reviewed change to this spec, not a silent tweak.
