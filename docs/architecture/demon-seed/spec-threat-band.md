# Spec: `threat-band`

**Module id:** `threat-band` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 4 of 16
**Model calls:** none.

## Objective

Turn `power-parse`'s number into one of ten **threat-noun rungs**, and turn that rung into a `Theta`
offset — through a tuning table, never a formula.

Owner, Q3: *"Band → a Theta offset, ~10 rungs."*
Owner, Q14: threat-scale nouns.
Owner, Q11: *"Same length, but a vocabulary that can't be confused."*

## Design

### 1. The rename, and why it is not cosmetic

The ideal doc calls this `powerBand`. **That name is taken.**
[PowerBandDisplay.cs:7](../../../src/FusionRpg.Core/Hud/PowerBandDisplay.cs#L7) maps pinned `Theta` to
a lawn HUD badge in `1..BadgeMax` — a *display* band for a live actor, unrelated to a species' inherent
threat. Two things called `powerBand`, one derived from the other's input, is a defect waiting for a
tired reader.

**Renamed to `threatBand` program-wide.** Q14 already chose threat nouns, so the name now matches the
vocabulary and the collision is gone before it ships.

### 2. Two ten-rung ladders that cannot be confused — Q11

| Ladder | Vocabulary | What it answers |
|---|---|---|
| `rarity` | chaff · sprout · grafted · cultivated · fused · chimeric · heirloom · firstseed · sunwoven · almanac | *how special is this?* — botanical, from `ssot-rarity.md` §3.3 |
| `threatBand` | nuisance · pest · marauder · raider · warden · scourge · tyrant · harbinger · cataclysm · calamity | *how dangerous is this?* — threat nouns |

**No word appears in both, and the two registers do not rhyme** — one is horticultural, one is martial.
Q11's requirement was that a reader seeing a bare word knows which ladder it belongs to without a
label, and these two satisfy that by construction.

### 3. The mapping is a table, and the table is tuning

`data/tuning/demon-threat.v1.json`:

```json
{
  "thresholds": [
    { "rung": 1,  "id": "nuisance",  "maxScore": 120,   "thetaOffset": 0 },
    { "rung": 2,  "id": "pest",      "maxScore": 300,   "thetaOffset": 2 },
    ...
    { "rung": 10, "id": "calamity",  "maxScore": null,  "thetaOffset": 40 }
  ],
  "scoreWeights": { "toughnessMilli": 600, "damageMilli": 400 },
  "inferredDefaultRung": 4
}
```

Every number here is a **tunable**, per [tunables-ssot.md](../tunables-ssot.md): a balance pass would
change all of them, and changing one must cost a file save, not a rebuild. There is no `const` in this
module's code holding a threshold — the audit
(`python scripts/audit-magic-numbers.py --targets M1`) must find nothing.

**Why a table and not a curve.** A formula would make every rung's boundary a consequence of a shape
nobody chose. The distribution of captured `hp`/`attack` in PvZ Fusion is not smooth — it clusters
hard around a few stock values — so a fitted curve would put most of the roster in two rungs and leave
four empty. `roster-metrics` measures exactly this, and the table is what lets a balance pass fix it in
one edit.

### 4. `Theta` needs a species term, and it does not have one

[ssot-power-scale.md](../power/ssot-power-scale.md) §5.3 composes `Theta` from six axes: Dave level,
realms advanced, PvZ runs, Zomboss level, map depth, world size. **None of them is a species offset.**

So `thetaOffset` is an **addition to a closed inventory**, and §10 of that document says adding one is
a reviewed change to it. **This module does not ship until that amendment lands.** Stated here rather
than discovered during the build, because the ladder document wins over this spec.

The offset is additive on the content side and is bounded by the same integer rules: it is a
`long`-safe small integer, it never multiplies anything directly, and `P(Theta)` is applied once,
downstream, by `species-generator`.

### 5. The score

One number in, one rung out. The score combines toughness and damage by the tunable per-mille weights
above, widened before multiplying and divided by 1000 exactly once, last:

```text
score = (toughness * toughnessMilli + damage * damageMilli) / 1000
```

Both operands widen to 64-bit first. A species with only one of the two uses that one at full weight
rather than half a score — a missing signal must not read as weakness.

### 6. `inferred` and `blocked`

A species whose `basis` is `inferred` gets no score, and its rung comes from `classify-pipelines`
reading the lore (Q26). A `blocked` species takes `inferredDefaultRung` and is flagged so
`roster-metrics` can report the pile. **Neither case silently becomes rung 1** — that would make
"unmeasured" and "harmless" the same value, which is exactly the ambiguity `basis` exists to prevent.

## Commands

```powershell
python -m seedsmith demons threat-band --dump data/seed/demons/_dump
python -m seedsmith demons threat-band --histogram        # rung occupancy, the balance-pass view
python -m pytest tools/seedsmith/tests/test_threat_band.py
python scripts/audit-magic-numbers.py --targets M1        # must find nothing in this module
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/power/bands.py    table load + lookup, no literals
data/tuning/demon-threat.v1.json                            the balance surface
tools/seedsmith/tests/test_threat_band.py
```

## Code style

```python
# The ladder is a table because the captured stat distribution is lumpy, not smooth:
# a fitted curve puts most of the roster in two rungs. See spec-threat-band.md section 3.
TUNING_KEY = "demon-threat.v1"
```

## Testing strategy

| Test | Asserts |
|---|---|
| `no_threshold_literal_in_code` | greps this module's source; a bare number fails |
| `rung_is_monotonic_in_score` | a higher score never yields a lower rung |
| `boundary_scores_land_on_the_named_rung` | each `maxScore` is tested at exactly the boundary and one either side |
| `single_signal_uses_full_weight` | toughness-only does not score half |
| `no_word_shared_between_the_two_ladders` | Q11, mechanically |
| `blocked_does_not_become_rung_one` | the ambiguity guard |
| `theta_offset_survives_long_roundtrip` | overflow rule |
| `histogram_reports_empty_rungs` | an unoccupied rung is visible, not silent |

## Boundaries

**Always:** read thresholds from tuning; widen before multiplying; divide by 1000 once, last; report
empty rungs.

**Ask first:** changing the threat vocabulary (it is the anchor's designated growth axis, so widening
is cheap — but the *words* are a naming decision).

**Never:** put a threshold in code; fit a curve; let `blocked` collapse to rung 1; ship before the
`ssot-power-scale.md` §5.3/§10 amendment lands.

## Success criteria

- [ ] `scripts/audit-magic-numbers.py` reports zero findings in this module.
- [ ] The ten threat nouns share no word with the ten rarity rungs.
- [ ] Every rung boundary is tested at the boundary and both sides.
- [ ] The histogram command names any rung with zero occupants.
- [ ] `ssot-power-scale.md` §5.3 and §10 name the species offset before this module is built.
