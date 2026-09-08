# Spec: `structure-metrics`

**Module 28 of 29 · level c5 · depends on `structure-pipeline` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Folded in by owner decision 45.

---

## Objective

**Say what "good enough" means, in numbers, with declared targets — and say which metrics may never
contribute to a pass.**

Two rules carry this module, and both are absolute:

> *"**Every metric declares closed-loop or open-loop.** An open-loop metric never contributes to a
> pass."*
>
> *"a metric without a declared target is an opinion."*

**Success looks like:** a gate that can fail, a review queue that cannot, and no confusion between them.

---

## The contract

### 1. Every metric declares its loop

| Metric | Loop | Target | Gate? |
|---|---|---|---|
| **Schema conformance** — no numeric field, no missing key | **closed** | 100% | ✅ **fails the build** |
| **Per-role coverage** — actual vs `budget` | **closed** | ±1 of target | ✅ |
| **Grid density** — roles × rows | **closed** | 2.4–4.0 (§4) | ✅ |
| **Tier ladder completeness** — every rung has a row | **closed** | 100% | ✅ |
| **`acquisitionPaths` non-empty** | **closed** | 100% | ✅ |
| **Idempotency** — byte-identical rerun | **closed** | hash equality | ✅ |
| **`unresolved` rate** | **closed** | below a declared ceiling | ✅ |
| **Flavour distinctness** | ⚠️ **OPEN** | — | ❌ **review queue only** |
| **Mode-collapse n-gram overlap** | ⚠️ **OPEN** | — | ❌ **review queue only** |

**The two open-loop rows are the ones a naive quality gate would put first**, because they measure the
thing a reader actually cares about. They are exactly the ones that must never fail a build — an
open-loop metric that gates turns a judgement call into a blocker nobody can clear.

### 2. Distribution skew — the guard with a corpse behind it

> *"D2's Hammerdin — **every individual number defensible, the *offering* degenerate**."*

Per-role **actual vs declared**, checked at the plan (`structure-planner`) and **again** at the output.
Twice, deliberately: the plan catches skew before tokens are spent; the output catches generation
drifting from its own plan.

### 3. A complete anchor is not a complete roster

Restated because it is the single most likely misreading of a green board:

> *"Type + speed modes + resistances lifts creature uniqueness from **63% to 93%**. A 900-unit roster
> needs roughly **1,500–3,500 named ability instances**. **A complete anchor is not a complete
> roster** — say so explicitly, or a downstream session will think the job is done."*

**The metrics report states this in its own header**, not in a footnote: ~36 anchors is identity; traits
and actions per structure are a separate, larger body of work.

### 4. What distinctness is actually carried by

> *"**Distinctness is carried by abilities, not stats.**"*

So a distinctness metric over `strengthBand` and `rarity` measures nothing. It reads **role**,
**`obstacleVerbs`**, **`acquisitionPaths`**, **traits** and **element** — the fields that change what a
structure *does*.

### 5. Rarity buys breadth and ceiling, never power

> *"**Rarity buys breadth and ceiling. In every game studied, never power.** A rung sets a count band
> and a tier window … A multiplier on the rung makes rarity dominant and destroys the overlap that
> makes low rungs live content."*

**A metric asserts this**: rarity must not correlate with `strengthBand` beyond the declared tier
window. A corpus where legendary always means strongest has quietly made rarity a power axis, and every
low rung has stopped being live content.

---

## Tunables

`data/tuning/structure-seed.v{n}.json`:

| Key | Purpose |
|---|---|
| `metrics.unresolvedCeilingMilli` | the `unresolved` gate |
| `metrics.roleCountTolerance` | ±N around each `budget` target |
| `metrics.densityBand` | `2400`–`4000` per-mille (§4) |
| `metrics.collapseNgramThreshold` | **review-queue threshold, never a gate** |

## Numeric types

Counts are `int`; rates are `int` per-mille. **No magnitudes** — this module measures identity.

## Boundaries

**Always:** declare closed or open per metric · declare a target per closed metric · state the
anchor-vs-roster distinction in the report header.

**Ask first:** promoting an open-loop metric to a gate — it needs a closed-loop replacement first, not
a threshold.

**Never:** gate on flavour · a metric with no declared target · measure distinctness over stats · let
rarity correlate with strength beyond its window.

---

## Testing

| Test | Asserts |
|---|---|
| `Every_metric_declares_its_loop` | by construction — a metric with no declaration fails registration |
| `No_open_loop_metric_can_fail_a_build` | **the rule, structurally** |
| `Every_closed_metric_has_a_declared_target` | *"a metric without a target is an opinion"* |
| `Role_skew_is_caught_at_plan_and_at_output` | both, deliberately |
| `Density_gate_rejects_a_taxonomy_out_of_band` | §4 |
| `Rarity_does_not_correlate_with_strength_band` | rarity buys breadth, never power |
| `Distinctness_reads_abilities_not_stats` | source scan over the metric's inputs |
| `Report_header_states_anchor_is_not_roster` | asserted, so it cannot be dropped |
| `Unresolved_rate_gates_at_the_declared_ceiling` | |
| `Transport_stub_raises_if_a_test_calls_a_model` | |

## Success criteria

1. Every metric declares closed or open; **no open-loop metric can fail a build.**
2. Every closed metric has a declared target.
3. Skew is checked at plan and at output.
4. Rarity is proven not to be a power axis.
5. The report says plainly that a complete anchor is not a complete roster.

## Open questions

None.
