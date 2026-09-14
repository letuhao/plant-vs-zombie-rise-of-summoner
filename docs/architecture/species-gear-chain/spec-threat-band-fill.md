# Spec: Threat-band fill (`threat-band-fill`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `threat-band-fill`
**Owning program:** `creature-seed` (module 4, `threat-band`)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) DO #2 / D8, § The shape 3

---

## Objective

**Give 719 species a real threat rung instead of a sanctioned default, and make the difference
between the two visible in the data.**

`threatBand` is the one creature ladder that reaches a magnitude — it contributes an additive per-rung
`thetaOffset` into `Θ`, and `P(Θ)` is applied once downstream. It is therefore the ladder carrying
the most weight in this initiative, and today it is the ladder carrying the least content: **four out
of five species sit on one rung.**

Nothing here authors content. The scorer exists, the thresholds were fitted from the real
distribution, and the tuning file ships. What is missing is that the score is never **written back**,
so a computable rung and an un-computable default look identical once persisted.

---

## What exists today — measured this session, not recalled

### The distribution, counted directly from `data/seed/creatures/species/**`

**904 distinct species ids. 185 carry a stored `threatBand`. 719 carry no such field at all.**

| Stored rung | Count | | Stored rung | Count |
|---|---|---|---|---|
| `nuisance` | 136 | | `cataclysm` | 4 |
| `tyrant` | 12 | | `pest` | 3 |
| `raider` | 11 | | `marauder` | 1 |
| `warden` | 10 | | `harbinger` | 1 |
| `calamity` | 6 | | `scourge` | 1 |

⭐ **The 719 with no field read as `raider`**, because `creature-threat.v1.json` sets
`inferredDefaultRung: 4` and rung 4 is `raider`. **719 + the 11 stored `raider` = 730 species
effectively on one rung — 80.8% of the roster.** This reproduces the ideal's "730 of 904" figure by
an independent derivation, which is why it is stated as a fact rather than carried forward.

⚠ **These are readings, not constants.** They are printed here as evidence and **must never become a
test assertion** — the corpus grows when content ships (`validation-ssot.md`; `AGENTS.md` Hard
boundaries).

### Built — more than the ideal credited

- **The scorer is real and already correct on the arithmetic.**
  `tools/seedsmith/seedsmith/adapters/creatures/power/bands.py:63-73 score()` computes
  `(toughness × toughnessMilli + damage × damageMilli) / 1000`, and its own docstring records that it
  **widens before multiplying and divides by 1000 exactly once, last** (`CLAUDE.md` rules 3–4). It
  also handles the single-signal case deliberately: *"a missing signal must not read as weakness."*
- **`rung_for_score` (`:76-83`)** is monotonic by construction — ascending rung order, first
  non-exceeded `max_score` wins, the open top rung always matches.
- **`classify` (`:85-95`)** scores `observed`/`stated` seeds and returns `None` for
  `inferred`/`blocked` — by design, with the fallback documented at the call site.
- **`histogram` (`:97-105`)** already includes **zero-occupant rungs**, so an empty rung is visible
  rather than silently absent. This is the reporting surface the fill needs; it does not need writing.
- **The thresholds are fitted, not guessed.** `data/tuning/creature-threat.v1.json`'s own note records
  that rung boundaries 1–9 are *"the real p10..p90 deciles of the (toughness\*600+damage\*400)/1000
  score over the 719 non-blocked species"*, chosen from the actual distribution **specifically so no
  rung starts empty**.
- **T-5 already holds for this ladder.** The file carries a per-rung `thetaOffset` (0, 4, 9, 13, 18,
  22, 27, 31, 36, 40 — an evenly stepped 0–40 ladder), and the note records it was *"amended into
  `ssot-power-scale.md` §5.3/§10 by T0.1 before this file could ship."* **This module must not add a
  second scaling.**
- **`CreatureThreatTuning.cs`** is the C# reader — the parser entry point is `:41`
  (⚠ an earlier draft cited `:57`, which is a `maxScore` ternary *inside* that parser);
  `runner.py:31` already imports `classify as classify_threat`.

### Wiring gap — the actual defect, stated precisely

`runner.py:510` calls `resolve_unresolved_threat_band(before_threat, tuning=threat_tuning)`. That
function (`anchor/derive.py:47-72`) does **not score**. It returns
`tuning.threshold_for_rung(tuning.inferred_default_rung).id` — the flat default — for any unresolved
value, and returns `(value, was_deterministic)` so a caller *can* record honest provenance.

So the chain is: **the scorer exists, the self-heal path bypasses it, and the default it writes
instead is indistinguishable from a classification once persisted.**

### ⚠ A correction to the ideal's DO #2, stated rather than absorbed

The ideal says to *"ship the `SpeciesExpander.cs:31-33` refusal in the same commit so the next gap
cannot hide."* **Reading the surrounding comment rather than the line shows that exclusion is
deliberate and documented**, and its reasoning is sound: `threatBand` is excluded from
`UnresolvedFields` because *"a null/absent value there already has a real, sanctioned fallback
(`CreatureThreatTuning.InferredDefaultRung`), read by `Expand` itself via `threatTuning.OffsetFor` —
never a batch-level skip."*

Flipping that skip would make a batch over the whole corpus refuse 719 species — turning a fill into
an outage. **The defect is not the skip; it is that the fallback leaves no trace.** So this spec
replaces DO #2's second half with: *record provenance, and report the residue.* That is the outcome
DO #2 wanted (the next gap cannot hide) without breaking the batch path.

---

## Tech stack

Python 3 (seedsmith), C# .NET 8 (`FusionRpg.Core` reader), pytest + xUnit. No new dependency.

## Commands

```powershell
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_anchor_derive.py -q
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests/test_run_runner.py -q
cd tools/seedsmith; python -m seedsmith check data/seed/creatures --adapter creatures
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Threat"
dotnet run --project tools/CreatureQualityReport      # the histogram, after the fill
```

## Project structure

| Path | Role |
|---|---|
| `tools/seedsmith/seedsmith/adapters/creatures/power/bands.py` | The scorer — **read, not modified** |
| `tools/seedsmith/seedsmith/adapters/creatures/anchor/derive.py:47` | Where the default is returned; gains provenance |
| `tools/seedsmith/seedsmith/adapters/creatures/run/runner.py:510` | The call site that must try `classify` first |
| `tools/seedsmith/seedsmith/adapters/creatures/anchor/schema.py` | The provenance field's schema |
| `data/seed/creatures/species/**` | **Generated output** — regenerated, never hand-edited |
| `data/tuning/creature-threat.v1.json` | Read-only here |
| `src/FusionRpg.Core/Creatures/Generation/SpeciesExpander.cs:31-33` | Left as-is, with a comment recording why |

## Code style

The one behavioural change is *try the scorer, then fall back, and say which happened*:

```python
def resolve_threat_band(threat_band, seed, *, tuning):
    """Score first, default second — and return WHICH, so the corpus can tell a real
    classification from a sanctioned fallback. The fallback is correct (creature-threat.v1.json's
    own `inferredDefaultRung` exists for exactly this case); what was missing is the trace."""
    if threat_band != "unresolved":
        return threat_band, "authored"
    scored = classify(seed, tuning)          # None for inferred/blocked basis, by design
    if scored is not None:
        return scored.id, "scored"
    return tuning.threshold_for_rung(tuning.inferred_default_rung).id, "default"
```

`SpeciesExpander.cs:31-33` gains a comment, not a code change:

```csharp
// threatBand stays excluded DELIBERATELY (threat-band-fill): a batch-level skip here would
// refuse every species relying on the sanctioned InferredDefaultRung fallback. The gap is
// surfaced by the provenance field and the quality report instead — never by refusing the batch.
```

---

## Tunables

This module **authors no new balance number.** Every value it needs already ships.

| Number | Meaning | Owner |
|---|---|---|
| `scoreWeights.toughnessMilli` / `damageMilli` (600 / 400) | The score's mix | `data/tuning/creature-threat.v1.json` — **shipped** |
| Per-rung `maxScore` (the fitted p10–p90 deciles) | Rung edges | Same file — **shipped** |
| Per-rung `thetaOffset` (0…40) | The one sanctioned tier→magnitude path | Same file — **shipped, and already carries its `ssot-power-scale.md` §10 row** |
| `inferredDefaultRung` (4) | The sanctioned fallback | Same file — **shipped** |
| *(new)* Refit trigger: whether rung edges are re-derived after the fill | See Open question 2 | Same file, `version` bump — **not a new file** |

**Structural (stays `const`, with a comment):** none introduced.

## Numeric types

The score is `(toughness × 600 + damage × 400) / 1000`. Python ints are arbitrary-width, so the
Python side is safe by construction — **and its docstring already flags that a C# port must widen
explicitly.** If any part of this module lands in C#:

- `long` for the score, not `int` — it is a magnitude-shaped number.
- `(long)toughness * milli`, **never** `(long)(toughness * milli)` — the cast binds to the result, so
  the multiply has already overflowed.
- Divide by 1000 **last, exactly once**.
- Overflow throws; no silent `unchecked`.

`thetaOffset` itself is a small signed integer added into `Θ` before `P(Θ)`. Because `P(Θ)` is
quadratic, every magnitude downstream is `long`: a `float` stops being integer-exact at `Θ` = 232,
inside normal play, and per-mille `int` breaks at `Θ` = 3,213 (`CLAUDE.md` "Numeric overflow").

## ActorHub gate

**Contributes via `ActorHub` as a registered atom reader — it does not fold privately.**
The species-magnitude corpus reaches the Hub through `AtomDerivedSubsystem` (`ActorHub.cs:168`). This
module changes **which rung a species is on**, which changes `Θ` and therefore `P(Θ)`; the
composition path is unchanged and no second composer is introduced.

⚠ **This module owes a `ContributionSourceIds` (GG-49 FULL) id** for that reader, filed against
`actor-hub`. Listed in the map's cross-program asks. It is an ask, not an assumption.

## Testing strategy

pytest (`tools/seedsmith/tests`) for the derivation, xUnit for the C# reader.

| Level | What it asserts |
|---|---|
| Unit | `score` widens before multiplying and divides once — proven with values that would overflow a 32-bit intermediate |
| Unit | `rung_for_score` is monotonic: a higher score never yields a lower rung, over the shipped thresholds |
| Unit | `classify` returns `None` for `inferred`/`blocked` basis and a rung for `observed`/`stated` |
| Unit | The resolver returns `"scored"` when a seed is scoreable and `"default"` when it is not — **the provenance contract** |
| Unit | Every rung id in the tuning file is one of the ten, and `thetaOffset` is present on all ten (closed vocabulary — pinning is correct) |
| Contract | `seedsmith check data/seed/creatures --adapter creatures` passes after regeneration |
| Report | The histogram prints per-rung occupancy **including zero-occupant rungs**, and prints the scored/default/authored split |

⛔ **No assertion on how many species land on any rung.** Occupancy is a reading. The acceptance
condition is *"a rung's occupancy is reported and no rung is empty-by-construction"*, proven by the
fitted thresholds, not by pinning 730 → some other number.

## Boundaries

**Always**
- **Fix the generator and regenerate.** `data/seed/creatures/species/**` is seedsmith output.
- Record provenance for every filled value — scored, default, or authored.
- Run the full seedsmith creatures pytest set and `seedsmith check` before committing.
- Commit the generator change and the regenerated corpus **as separate logical commits**, via MCP
  `repo-git.commit` with explicit `paths`.

**Ask first**
- Refitting the rung edges after the fill (Open question 2) — it moves every species' `Θ`.
- Any change to `thetaOffset` values, which are registered in `ssot-power-scale.md` §10.
- Extending the scorer's inputs beyond toughness and damage.

**Never**
- ⛔ **Hand-edit a species JSON to change its `threatBand`.** That forks the corpus from its
  generator: the next run reverts it, the ledger stops describing the file, and the change is
  invisible to every other consumer. This is the repo's most-repeated incident.
- Add a second scaling on top of `thetaOffset`. A tier is additive into `Θ`, once.
- Invent a default where none is sanctioned — `aptitudePrimary` and `elementPrimary` deliberately
  stay unresolved and reported, and this module does not change that.
- Flip `SpeciesExpander`'s `threatBand` exclusion. It would refuse 719 species.
- Assert a population count anywhere.

## Success criteria

1. Every species with a scoreable power seed carries a **scored** `threatBand`, not the default.
2. Every species carries provenance distinguishing `scored` / `default` / `authored`.
3. The quality report prints per-rung occupancy including empty rungs, plus the provenance split.
4. No rung is empty by construction — the fitted deciles are re-validated against the post-fill
   distribution and the result is **reported**, not asserted.
5. `seedsmith check data/seed/creatures --adapter creatures` is green.
6. `dotnet test tests/FusionRpg.Core.Tests` is green; **no golden re-bless is required**, or the
   re-bless is a separately reviewed commit stating which `Θ` values moved and why.
7. The corpus diff is a pure regeneration — no hand edits, verified by re-running the generator and
   getting a byte-identical tree.
8. `SpeciesExpander.cs` carries the comment recording why the exclusion stands.

## Open questions

1. **Do `inferred`-basis species get a lore-derived rung, or the default?** `classify`'s own docstring
   says *"`inferred` gets its rung from `classify-pipelines` reading the lore (Q26)"* — so a path is
   named. Whether this module wires it or leaves it to module 4's own backlog is a scoping call.
   **Recommendation: leave it — score the `observed`/`stated` population first and report the
   `inferred` residue.** That is the whole value of the module and it needs no model call.
2. **Refit the rung edges after the fill, or keep the shipped deciles?** They were fitted over the
   719 non-blocked species' scores, so the fill does not invalidate them — but occupancy will shift
   once the defaults stop piling on rung 4. **Recommendation: keep them, report the new histogram,
   and refit as a separate reviewed `version` bump** if a rung empties.
3. **Does the provenance field ship in the anchor schema or as a sidecar?** Recommendation: the
   anchor schema, so `seedsmith check` can validate it and it cannot be dropped by a later pass.
