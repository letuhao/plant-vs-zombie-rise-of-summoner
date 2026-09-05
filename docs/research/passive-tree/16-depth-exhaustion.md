# Depth exhaustion — D29's tier cap has an expiry date (2026-09-05)

**Why this was re-run.** D28 ("cross-unlock credits your largest posture-mate") was adopted on the
strength of [09-crossunlock-sweep.md](09-crossunlock-sweep.md), which ran at **Θ=100**. D29 then set
the authored depth to 10 tiers, and nobody had checked what happens further up the ladder.

> **Correction, 2026-09-05.** An earlier version of this note said doc 09 "ran on D20's superseded
> ladder and assumed 7 tiers". That was wrong, and [10-decision-consistency-audit.md](10-decision-consistency-audit.md)
> caught it: D26's ladder is in the swept build (`Program.cs:311`) and supplies four of doc 09's eight
> rows, and both tier functions are unbounded loops, so no tier count was ever assumed. D29's cap is
> also provably **inert at Θ=100** — the budget is 300 points and `req(10)=275 ≤ 300 < 330`, so nothing
> can exceed tier 10 there, which is why the capped rows reproduce the uncapped ones byte-for-byte.
> What doc 09 actually lacked was **high-Θ coverage**. That is what this note adds, and the finding
> below stands on its own measurement.

Re-run with D26's ladder and D29's cap (`tools/HybridViability --crossunlock`, `b=5`, `Fmax=1.20`).

## The finding

D28 holds — but only for a while.

| Θ | corner | spread | verdict | tree power, in-posture-4 ÷ pure |
|---|---|---|---|---|
| 150 | 49.1% | 48.3% | corner wins | 0.70× |
| 200 | 49.7% | 47.8% | corner wins | 0.75× |
| 250 | 48.2% | 47.1% | corner wins | 0.87× |
| **300** | **47.6%** | **47.6%** | **crossover** | **1.00×** |
| 350 | 47.7% | 48.0% | spread wins | 1.00× |
| 400 | 47.4% | 48.2% | spread wins | 1.00× |
| 600 | 46.3% | 49.6% | spread wins | 1.00× |

> ⛔ **The concentration reward expires at Θ ≈ 300.** Above it, spreading wins again — the exact
> ordering D4–D7 exist to prevent, and the one D28 was adopted to fix.

**At Θ=100 the cap is inert** — capped and uncapped rows are byte-identical, because nothing reaches
tier 10 there. That is why the original sweep could not have found this.

## Why — and it is structural, not a tuning miss

The right-hand column is the whole story. Tree power for a 4-way build against a pure build climbs
**0.70 → 1.00** and then stops dead.

Computed from the model's own shares (an all-in build holds ~0.542 of the share vector; a floored
tree holds ~0.042; 3 points per Θ; `req(10) = 275`):

```text
pure build, its own spike tree      gate = 1.626·Θ   → tier 10 at Θ ≈ 169
pure build, its floored posture-mates (largest-mate credit)
                                    gate = 1.751·Θ   → tier 10 at Θ ≈ 157
four-way in-posture build           gate = 1.000·Θ   → tier 10 at Θ ≈ 275
```

A pure build saturates the authored depth first, around Θ≈170. A spread build saturates later, around
Θ≈275. **In between, the spread build is still climbing while the focused one has nothing left to
buy** — so the gap closes. Past Θ≈275 everyone is at tier 10 in every tree they touch, tree power is
*identical for everyone*, and cross-unlock stops discriminating between builds at all.

**This is not a property of D28 or of the cap's size.** Any finite authored depth saturates under
PS-8's endless Θ. Ten tiers only decides *when*. Twenty tiers moves the crossover; it does not remove
it.

## What it actually means, and it is not "the design is broken"

The model stops growing at the tier cap. **The design does not.** D3's soul track is *unlimited* and
*per-node*: souls buy bonus power scale forever, and `tools/HybridViability` does not model them at
all — it only counts tier-derived power.

So the honest reading is a redirection, not a refutation:

> **Past Θ≈300, every point of build differentiation has to come from the SOUL track, because the
> point track is exhausted for everybody.**

That promotes a parameter the design currently treats as minor. §3.2 blends the two currencies:

```text
H = w · H_points + (1 − w) · H_souls          w tunable, default 0.5 "until swept"
```

§7 lists `w` under *"measurement-gated (solve, don't argue)"* — an open item among others. This sweep
says it is **the load-bearing parameter of the entire late game**. Below the crossover, `H_points`
carries concentration. Above it, `H_points` is identical for every build that has saturated, so only
`(1−w)·H_souls` can tell builds apart. **At `w = 1` the design has no late game at all.**

## What this changes

| | |
|---|---|
| **D28** | Stands, and is correctly chosen — but its stated benefit is **bounded to Θ ≲ 300**, which was not known when it was adopted. Record the bound rather than the claim |
| **D29** | The 10-tier depth is not just a content-volume choice. It sets **when the point track stops differentiating builds** — a balance property nobody costed |
| **`w`** | Promote from "measurement-gated open item" to **a primary design parameter**. It is the only thing carrying concentration past the crossover |
| **The model** | `tools/HybridViability` must learn the soul track before any further sweep means anything above Θ≈300. Today it silently reports a saturated late game because it cannot see the half that still grows |

## What was NOT measured

The soul track, mechanism nodes, and D25's rising unlock cost are all outside this model. This sweep
bounds the *point* track and nothing else. Its own §3.5 caveat still applies and now applies harder:
**a magnitude-only model saturates, and saturation is exactly what it just reported.**
