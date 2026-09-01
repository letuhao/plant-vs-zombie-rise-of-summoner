# Spec: `power-parse`

**Module id:** `power-parse` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 3 of 16
**Model calls:** none. This module is the reason most of the roster costs nothing to classify.

## Objective

Produce, for every species in the dump, a **numeric power seed** and the four-value `basis` that says
where it came from — using only the structured capture and a deterministic parse of the almanac text.

Owner, Q6: *"drop hp gate, but hp still exist in zombie description in almanac, can use it as optional
seed."*
Owner, Q16: *"Number wins, and the LLM audits the result."*

## Design

### 1. Measured, not assumed — what the corpus actually contains

Run against the committed 84-species corpus (`data/seed/demons/demon/**`), 2026-09-01:

| Signal | Count | Share |
|---|---|---|
| `coverage.stats == "observed"` (structured `hp` **and** `attack`) | **82 / 84** | 97.6% |
| flavour text present | **84 / 84** | 100% |
| `伤害：N` parseable from text | 50 / 84 | 59.5% |
| `韧性：N` parseable from text | 24 / 84 | 28.6% |
| an interval `N秒` parseable from text | 55 / 84 | 65.5% |

**Two corrections this measurement forces.**

**① The primary source is the structured capture, not a regex.** `AlmanacSeedDto` already carries `Hp`,
`Attack`, `Armor`, `ArmorMax` and `StatsObserved` as typed fields (`RpgStore.AlmanacSeed.cs:20-24`).
The ideal doc framed this module as a 韧性/伤害 parser; the parser is the **fallback**, not the path.

**② That 97.6% is an upper bound and must not be quoted as a projection.** The 84 species in the
committed corpus are exactly the ones the old C# generator selected — and it selected them behind an
HP gate, which is the gate Q6 removed. **They are a sample biased toward being observed.** The real
coverage over all ~904 is unknown until `corpus-dump` runs, and reporting it is one of this module's
outputs, not one of its assumptions.

### 2. The four bases, in strict precedence

| `basis` | Condition | Value used |
|---|---|---|
| `observed` | `StatsObserved` and `Hp`/`Attack` present | the captured integers |
| `stated` | not observed, but `伤害：N` or `韧性：N` parses from flavour text | the parsed integer |
| `inferred` | neither, but flavour text exists | **none — `classify-pipelines` supplies the band from lore (Q26)** |
| `blocked` | no signal and no text at all | none; `roster-metrics` reports the count |

Precedence is absolute: a species with an observation never falls through to a parse, even when the
text disagrees. **A disagreement is recorded, not resolved here** — it goes to the audit pipeline as
evidence, because a text number and a live sample disagreeing is interesting data, not an error.

### 3. The parse, and the bonus nobody planned for

The shipped text format is richer than a bare number:

```text
伤害：20×6/1.5秒
```

That is damage-per-shot, shot count, **and an interval**. So the parse yields three fields, not one:

| Extracted | Pattern | Feeds |
|---|---|---|
| damage | `伤害[:：]\s*(\d+)` | `threat-band` |
| toughness | `韧性[:：]\s*(\d+)` | `threat-band` |
| shot count | `×(\d+)` immediately following damage | recorded |
| interval seconds | `([\d.]+)\s*秒` | **`attackTempo` gets a *stated* basis for 65.5% of the sample** |

**This is the module's most valuable side effect.** `attackTempo` was introduced in ideal §6.2 ② as a
purely classified ordinal because `DerivedStatChannels` registers `move.range` and nothing else for
tempo. Two thirds of the sample state their interval in text. **A stated tempo beats a guessed one,
and it costs one more capture group.** `classify-pipelines` only judges tempo where the parse is silent.

Interval seconds are held as an **integer of milliseconds**, never a float — `1.5秒` becomes `1500`.
The repo's numeric rule is not negotiable for a value that will later multiply into a magnitude.

### 4. What this module does not do

It does not turn a number into a band. `threat-band` owns that, because the mapping is a tuning table
and this module must stay a pure, testable extraction with no balance surface in it.

### 5. Overflow

`hp` and `attack` come from a game whose values reach the tens of thousands and are multiplied
downstream by `P(Theta)`. **Every extracted magnitude is carried as `int` at the boundary and widened
to 64-bit before any arithmetic**, per `CLAUDE.md`'s rule 3. Python has no fixed width, so the guard is
a range assertion at emit time: a value that would not survive a C# `long` round-trip is a defect,
raised, never clamped.

## Commands

```powershell
python -m seedsmith demons power-parse --dump data/seed/demons/_dump
python -m seedsmith demons power-parse --dump ... --report   # basis histogram, disagreement list
python -m pytest tools/seedsmith/tests/test_power_parse.py
```

`--report` is the deliverable that answers "how much of the roster is actually observed?" — the number
this spec deliberately refuses to guess.

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/power/parse.py     the patterns and precedence
tools/seedsmith/seedsmith/adapters/demons/power/model.py     PowerSeed dataclass, frozen
tools/seedsmith/tests/test_power_parse.py
tools/seedsmith/tests/fixtures/power_text/*.txt              real captured strings, verbatim
```

Fixtures are **real captured text copied verbatim**, never hand-written approximations. A parser tested
only against text its author invented passes on a format that does not exist.

## Code style

```python
# 伤害：20×6/1.5秒  - damage, shot count, and interval in one line. The interval is the
# reason this module also feeds attackTempo: a stated tempo beats a classified one, and
# 55 of the 84 committed species state theirs.
DAMAGE = re.compile(r"伤害[:：]\s*(\d+)")
```

## Testing strategy

| Test | Asserts |
|---|---|
| `observed_beats_stated` | a species with both takes the observation; the disagreement is recorded, not dropped |
| `parses_every_committed_flavor_string` | run over all 84 real entries; the counts in §1 are the assertion, so a format change fails loudly |
| `interval_is_milliseconds_integer` | `1.5秒` -> `1500`, never `1.5` |
| `no_text_and_no_stats_is_blocked` | the `blocked` case is reachable and labelled |
| `text_only_is_stated_not_inferred` | the two are not conflated |
| `out_of_range_magnitude_raises` | overflow throws, never clamps |
| `half_width_and_full_width_colons_both_parse` | `:` and `：` both appear in real data |

## Boundaries

**Always:** prefer observation; record disagreements; keep intervals as integer milliseconds; use real
captured strings as fixtures.

**Ask first:** adding a new extraction pattern (it changes reported coverage, which other modules trust).

**Never:** map a number to a band here; invent a number for a `blocked` species; use a float for any
extracted magnitude; quote the 84-species coverage as a projection for 904.

## Success criteria

- [ ] Every dump row leaves this module with exactly one `basis`.
- [ ] The report states real observed / stated / inferred / blocked counts over the **full** dump.
- [ ] `attackTempo` receives a stated interval wherever the text contains one.
- [ ] Every fixture is a verbatim captured string.
- [ ] No band, rung, or `Theta` value is produced by this module.
