# Seedsmith spec set — adversarial audit: game design and balance

**Lens for this pass:** does this produce a good ARPG, judged the way an experienced ARPG systems
designer would judge it — against Diablo 2, Path of Exile, Last Epoch as genre touchstones.
Explicitly not checking code structure, statistics rigour, or module boundaries; other passes own
those. Read: `spec-numerics.md`, `spec-budget.md`, `spec-analytics.md`, `seedsmith-map.md`,
`item/ssot-uniques.md` §3.5–3.7, `item/ssot-sets.md` §3, plus (to ground several findings in real
numbers rather than the documents' own summaries) `data/seed/items/_registry/core.v1.json`,
`item/enrichment-plan.md` §5, `item/atom-family-library.md`, and `item/review/wave2-role-fit.md`.

**Read this before the findings.** The arithmetic in these specs is careful and the honesty about
what is unknown (§5 of `spec-numerics.md`, the whole of `spec-budget.md` §2) is genuinely good
practice — most of what follows is not "the maths is wrong," it is "the maths is confidently
precise about a design assumption that a systems designer would not grant for free." A model that
resolves a wrong assumption to four significant figures is more dangerous than one that admits it
is guessing, because the confidence transfers to the number and the number is what everyone quotes
later.

---

## 1. `baseShare` is solved for a target that is never checked against the thing it has to survive

**Severity: BLOCKER**

`spec-numerics.md` §6 derives `baseShare = 35‰` by solving `solve_base_share(4.5)` — a real, honest
piece of engineering: the constant is answerable, not chosen by feel. But the equation it solves
only has player-side terms (`SLOTS`, `affixesPerItem`, `effectiveChannels`, `r`). Nowhere in this
spec, `spec-budget.md`, `spec-analytics.md`, or the map is there a term for what the player is
*fighting*. An ARPG power-gap target is only meaningful relative to the opposition's own scaling —
"4.5× naked" means something completely different in a game where zombie HP/DPS grows 2× per wave
tier than in one where it grows 10×. In Diablo 2 and Path of Exile the analogous number (how much
a build must scale to keep pace with monster life/damage at the content's top end) is derived *from
monster tables*, not chosen in isolation and then hoped to fit. This spec set has no monster-side
document in scope, and no cross-reference to one exists in the parts that are in scope.

This is not "go verify a number" — it is that the number **cannot currently be verified**, by
anyone, from the documents that exist. A designer asked "is 4.5× right?" today can only answer
"right relative to what?" That is a BLOCKER on the load-bearing constant of the entire `numerics`
module, not a nice-to-have: every other number in this spec (`sharePermille`, the whole
`channelWeight` vector, the rebalance tooling) is denominated against `baseShare`, so an
unfalsifiable anchor makes everything downstream unfalsifiable with it.

**What would close it:** state, even roughly, what zombie/wave power-scaling curve this is being
weighed against, and show the same solver applied to *that* target rather than to an aesthetic
judgement about where "gear stops mattering."

---

## 2. The "below 3×, above 6×" reasoning is an assertion wearing the clothes of a derivation

**Severity: MAJOR**

The paragraph that picks the 4.5× target reads as reasoned — "below ~3× gear stops mattering and
the game is a level-treadmill; much above ~6× base stats are noise" — but neither boundary has an
argument attached, only the assertion. Compare against what actually happens in the genre:

- **Diablo 2**: a BiS-geared character at a fixed level is not 3–6× a naked one on the stat sheet —
  build-defining uniques (Enigma's teleport, Infinity's -elemental resist) change *what the
  character can do* more than they scale a number, and the numeric gap between "just found the
  game" and "fully geared" for the same character level is commonly far north of 6× *(recalled from
  play, not re-verified)*. The 3–6× band asserted here reads like it was picked for a game whose
  power gain is meant to be almost entirely additive stat growth — which may be correct for *this*
  game's design, but that is a different argument than "this is where gear stops/starts mattering
  in an ARPG," which is the argument actually written down.
- **Path of Exile**: the genre's most loot-driven title deliberately targets gear-driven power gaps
  of one, sometimes two, orders of magnitude across a single character's endgame progression
  *(recalled, not verified)* — because the loot loop *is* the game, and a narrow band would make the
   hundreds of hours of post-campaign play numerically pointless.
- **Last Epoch**: leans harder on the passive/skill tree than gear alone for scaling, which is the
  closest analogue to this game's apparent lack of a described skill-point system — but even there,
  gear is not capped anywhere near 4–6×; the multiplicative interaction of uniques and legendaries
  routinely produces far larger swings *(recalled, not verified)*.

The document's own caveat — the 4.5× figure excludes sets, uniques, sockets and the `+X`
enhancement track, and "a fully-optimised endgame character lands nearer 5–5.5×" — is doing more
work than it's given credit for. 5–5.5× including every system is on the *low* end even for a
restrained ARPG, and this game has explicitly narrower systems than any of the three comparators
(no described passive tree, no gem/support-gem layer, 15 flat equip slots). A narrower system
needing a *smaller* multiplier to feel the same is plausible, but the spec asserts the number
instead of making that comparison, so the reasoning reads as post-hoc justification for a round
number one notch under the solver's 36.6‰, not as a genre-grounded target.

---

## 3. The progression curve is monotonic, not smooth — and the two extremes are where it goes flattest

**Severity: MAJOR**

The claimed curve — `1.28 → 1.80 → 2.13 → 2.97 → 4.35 → 5.09` — is described as "smooth... with no
cliff between adjacent rungs." Check the relative step, which is what a player actually feels
(nobody perceives an absolute multiplier delta; they perceive "that upgrade felt big" or "that
upgrade felt like nothing"):

| Step | Absolute Δ | Relative Δ |
|---|---|---|
| grafted → fused | +0.52 | **+40.6%** |
| fused → chimeric | +0.33 | **+18.3%** |
| chimeric → heirloom | +0.84 | **+39.4%** |
| heirloom → sunwoven | +1.38 | **+46.5%** |
| sunwoven → almanac | +0.74 | **+17.0%** |

That is not a smooth curve, it is a sawtooth — big, small, big, biggest, small. Two things follow:

1. **`fused → chimeric` is a dead zone right in the middle of the ladder** (+18%, the smallest jump
   besides the very last one), sitting exactly where a player is deciding whether continuing to
   grind this rarity band is worth it before the campaign's harder content presumably opens up.
2. **`sunwoven → almanac` — the top rarity, the one that costs the most grind time by every ARPG's
   own design convention — delivers the *flattest* relative payoff in the entire ladder (+17%).**
   That is backwards for a chase-item curve: the tier a player farms longest for should not be the
   one that feels smallest per farm-hour. D2's own late-game complaint about certain unique tiers
   (marginal upgrades that cost disproportionate grind) is the exact failure mode this curve
   reproduces by construction, not by bad luck.

The document's "monotonic, therefore smooth" framing conflates two different properties.
Monotonicity (`m_1 < m_2 < … < m_5`) is what §5's Spearman/PAVA check needs and is guaranteed by the
formula's positive terms — it says nothing about whether consecutive steps feel comparable, and
this curve's own numbers show they do not.

**Also worth flagging as a scope gap, not a new defect:** the table samples 6 of the 10 named
rarity tiers (`chaff`, `sprout`, `cultivated`, `firstseed` are omitted with no stated reason). A
"no cliff between adjacent rungs" claim resting on 6 of 10 points is not yet a claim about the
ladder — it is a claim about the ladder's most convenient half.

---

## 4. `channelWeight = 1.0` for all 14 channels is an honest null that ships without the two or three corrections a designer already knows are needed

**Severity: MAJOR**

The spec's framing — "we do not yet know, encoded honestly" — is a legitimate default for most of
the 14 primary channels. But two of the named examples in this very spec set are `vitality`
(`maxHp`, Flat) and `might` (`atk`, Flat) — survivability and offense, respectively
(`atom-family-library.md` lines 83–86). Treating +3.5% maxHp and +3.5% attack as worth the same
thing per affix is not an *unknown*, it is a *known-questionable* assumption in this genre:
survivability stats have a floor-effect utility curve (life matters up to "I stop dying," and very
little past it, because death is usually binary per engagement), while offense stats compound
directly into clear speed and are almost never "enough." This is why %-damage mods are consistently
chased harder than %-life mods of equal magnitude across the genre's actual player-facing balance
history *(pattern recalled from community/theorycraft discussion across D2/PoE, not independently
re-verified here). A defensible v1 does not need to solve this — but it does not need to *pretend*
it is symmetric-until-proven-otherwise when the asymmetry is a standing prior, not a coin flip.

The bigger problem is sequencing, not magnitude: §4 places the telemetry refit **after** live play
generates battle records, and the map's build order (W1 → W2 → W3) puts the LLM generation wave
(the one that actually authors thousands of rows using these weights) ahead of any telemetry
existing at all. That means every row generated in W3 is authored against a pricing model admitted
to be wrong, and the correction only becomes possible once a population of players has already
formed opinions (and, if there is any trade/economy system, prices) around the mispriced channels.
Rebalancing edits no content per §3.1 — but it does not un-ring the bell of a player base that
learned "channel X is the good one" during the uncorrected window. Recommend seeding `channelWeight`
with a handful of directional priors (offense > pure-survivability at equal AE, at minimum) rather
than uniform 1.0, specifically for channels where genre consensus is strong enough to act on before
data exists, and reserving true 1.0-ignorance for channels with no such prior.

---

## 5. `opWeight[More] = 0.55` is a constant standing in for a relationship that is not constant

**Severity: BLOCKER**

This is the most concrete, checkable finding in this review, so it is worth doing the arithmetic
rather than asserting a genre feeling.

In a PoE-style stacking model, total damage is `D × (1 + ΣIncreased) × Π(1 + More_i)`. Take two
affixes of equal nominal size, x = 0.10 (10%), one `Increased`, one `More`, and compare their
marginal contribution at two different points on a build's power curve:

**No other modifiers stacked (early game / a "naked" comparison, which is exactly what §6 uses to
derive `baseShare`):**
- +10% Increased alone: `D × 1.10` → **+10%**
- +10% More alone: `D × 1.10` → **+10%**
- Ratio: **1.0** — Increased and More are worth the *same* thing.

**Heavily stacked (a plausible endgame state: +300% Increased already on the sheet, one existing
×1.5 More already applied):**
- Adding +10% Increased: `(1+3.0+0.10) × 1.5 = 6.15` vs `(1+3.0) × 1.5 = 6.0` → **+2.5%** marginal.
- Adding +10% More: `(1+3.0) × 1.5 × 1.10 = 6.6` vs `6.0` → **+10%** marginal.
- Ratio: **~4.0** — More is worth *four times* an Increased of the same number.

The true More:Increased value ratio is not a constant — it is a function of how much Increased is
already on the sheet, and it visibly ranges from **1:1 to 4:1+** across a plausible span of the
game's own progression (which §6 already establishes spans several multiplicative rungs). A single
`opWeight[More] = 0.55` (implying a flat ~1.82:1) **overprices More at the low end** (grafted/fused
gear, where a More-bearing affix would be worth roughly what an Increased of the same size is worth,
not 1.8× as much) and **underprices it at the high end** (heirloom/sunwoven/almanac gear, where a
More-bearing affix is worth several times what the flat conversion factor credits it for).

This is why it is a BLOCKER rather than a MAJOR: the task specifically asks whether the ratio "holds
at low stacking as well as high," and the honest answer, worked with the model's own formula, is no
— by a factor that grows with the very progression this spec is built to describe. A scalar cannot
be *refit* into correctness by telemetry, because the phenomenon it is modeling is not scalar; the
fix is a shape change (make `opWeight[More]` a function of the actor's existing Increased total, or
price the AE cost of a More affix contextually rather than with one constant), not a recalibration
of 0.55 to some other single number.

**Where this bites concretely:** grafted/fused-tier items that happen to carry a `More` op (rare
tier bands per `atom-family-library.md:85,88` — `bulwark`, `savagery`) will play *stronger* than
their AE cost implies at that stage of the game, then progressively feel closer to "correctly
priced" as the character accumulates other Increased sources — exactly the opposite of the
smooth-power-curve goal in finding 3.

---

## 6. Freely allowing one base type into multiple sets creates a jail risk the five documented anti-jail devices were never built to see

**Severity: BLOCKER**

`ssot-sets.md` §3.5 lists five deliberate anti-set-jail mechanisms (capability at the floor
threshold, no `More`-op tiers, a 1.5 AE/piece budget cap, no set owning both weapons, and by
implication the role-cap at 6). All five are analyzed as if a base type's set membership is a
partition — one piece belongs to at most one set, so the worst case is "this set is mandatory."
`enrichment-plan.md` §5.1 then changes the underlying structure to a hypergraph — "a base type MAY
belong to several sets... reuse pieces where the theme fits" — **after** the anti-jail analysis was
written, and none of the five devices was re-examined against the new shape.

The failure mode a hypergraph produces is qualitatively worse than ordinary set jail: instead of
"this one set is mandatory," you get **a small number of base types that are mandatory across
*several* sets simultaneously**, because every set that lists the piece gets partial credit toward
completion the instant a player equips it. A piece authored into, say, four different theme sets is
now four times as likely to be "the" godroll drop for that role, compresses the effective diversity
of 30 sets down toward however many of them share their heaviest-weighted roles, and — because
completion credit is per-set, not per-item — a player converging on the overloaded piece is not even
making a build *choice*; they are following the arithmetic.

The plan's own risk acceptance — "the risk this accepts is that one strong piece becomes
near-mandatory; that is a balance question the win-rate sweep can answer later" — reaches for the
wrong instrument. A win-rate sweep measures whether builds succeed, not whether players are
*converging on the same few items to succeed*. A corpus where every high-win-rate build wears the
same three shared base types can post a perfectly flat, healthy-looking win-rate distribution while
having zero actual build diversity — which is precisely the failure `ssot-sets.md`'s own five
mechanisms were built to prevent, and precisely the kind of thing this seedsmith spec set's own
`Distribution`/evenness machinery (`spec-analytics.md` §1.2) is designed to catch **for corpus
content**, but nothing in `budget` or `metrics` proposes measuring it **for player equip choices**,
which is a different population than anything in this spec set touches.

**What would close it:** either (a) bound how many sets a single base type may join (a structural
cap, cheap to check), or (b) make a piece's AE contribution scale down for each additional set it
serves (so reuse is not free), and separately, add "equip-frequency across the theoretical
option-space" as a declared metric family distinct from win rate — it is a different question and
currently has no home in the catalogue.

---

## 7. "Four heaviest and four lightest" is the right instinct, applied with three loose ends

**Severity: MAJOR**

Judged as a design call in isolation, replacing "the eight heaviest" with "four heaviest, four
lightest" (`enrichment-plan.md` §5 item 5) is a genuinely good move, and it matches real genre
precedent in both directions: heavy slots (weapon, chest, off-hand/shield) are the classic home for
stat-check uniques (D2's Windforce, Enigma), while the lightest, lowest-opportunity-cost slots are
the classic home for *build-enabling* uniques precisely because equipping them costs almost nothing
on the stat sheet (PoE's Watcher's Eye, Mark of the Elder — both jewels) *(recalled, not
independently re-verified)*. "Eight heaviest" alone would have clustered every unique into
stat-stick territory and produced none of that second archetype. This review should say plainly:
the decision itself is sound and better than what it replaced.

Three loose ends stop it from being clean, checked against `core.v1.json`'s actual
`budgetWeightMilli` values (160/120/90/**80/80**/70/60/60/60/50/**50/50/50**/40/15/15):

- **The tie at rank 4 is undocumented.** `armament-secondary` and `jewel-major` both sit at 80‰.
  "Four heaviest" needs a tiebreak the spec never states, and picking one over the other is a real
  design choice (a shield/secondary-weapon unique plays very differently from an amulet unique) that
  currently has no rationale attached to it anywhere in the read set.
- **Five roles are permanently unique-less by construction**: `manipulator` (70), `mantle` (60),
  `head-guard` (60), `girdle` (60), and whichever of the rank-4 tie loses. `head-guard` — the
  helmet slot — is one of the most iconic unique homes in the entire genre (Harlequin Crest, Andariel's
  Visage in D2 *(recalled, not verified)*); a v1 that structurally cannot ever put a unique on a
  helmet is giving up a genre expectation for an allocation-arithmetic reason, and nothing in the
  read set weighs that cost against the convenience of a clean 4+4 split.
- **The same flat 1.5 AE premium is applied to roles spanning a 10.7× range of budget weight**
  (160‰ down to 15‰, though jewel-minor itself is separately banned — 40‰ to 160‰ is the real span
  within the unique-eligible set). Giving up `armament-primary`'s 160‰ stat budget for a capability
  plus a flat 1.5 AE topper is a much larger *relative* sacrifice than giving up `retinue`'s 40‰ for
  the same topper. That asymmetry means heavy-slot uniques are effectively taxed harder to be worth
  equipping (they compete against a much stronger rare baseline for the same capability budget)
  while light-slot uniques are close to a free splash pick — which is not necessarily wrong (D2 and
  PoE both tolerate this), but it is an uncosted side effect of applying one number across an
  8-role set whose weights vary by an order of magnitude, and the spec presents the 1.5 AE constant
  as if it means the same thing on every role it touches.

---

## 8. The corpus a player would meet today still reflects the allocation this decision explicitly rejected

**Severity: MAJOR**

`item/review/wave2-role-fit.md:164-165` documents the shipped 144 uniques sitting on exactly
`armament-primary, armament-secondary, core-guard, ward-array, manipulator, mantle, head-guard,
jewel-major` — 18 each. Cross-checked against `core.v1.json`'s weights, that is precisely the
**eight heaviest roles by budget weight** — the option `enrichment-plan.md` §5 item 5 explicitly
says it is *replacing*. `enrichment-plan.md` itself says the move to "four heaviest, four lightest"
"[c]osts an 18-partition re-run, which **is in flight**" — meaning, as of the documents read for
this review, the re-run has not landed.

This does not make finding 7 wrong, but it means finding 7's analysis is of a *target*, not of
anything a player, a metric, or a rebalance pass would currently observe. Two consequences worth
stating plainly for whoever picks this up next:

- Every game-design judgement in this review about "four heaviest + four lightest" — including the
  praise in finding 7 — applies to a state that does not exist in the corpus yet.
- `budget`/`metrics` tooling built against a target of "uniques on 8 specific roles, 18 each"
  (`spec-budget.md` §4.2's "18 unique partitions × 8 allocated roles = 144, exactly") will report a
  clean, zero-tolerance pass against the *current* corpus, because the current corpus also happens
  to have exactly 8 roles × 18 — just the wrong 8. A distribution check keyed only on *count per
  role-slot* rather than *which* roles are in the eligible set would not catch that the re-run never
  happened. Worth a specific regression fixture once `metrics` exists: assert the *identity* of the
  8 unique-eligible roles, not just their cardinality.

---

## 9. 144 has an owner-confirmed count and no owner-confirmed density

**Severity: MINOR**

`spec-budget.md` §2 is right to treat 144 as authoritative — it is derived structurally
(8 roles × 18 partitions, zero tolerance) and the owner has confirmed it. But the question this
review was asked — "is 144 the right number for 740 base types, 30 sets" — cannot actually be
answered from anything in this spec set, because **nothing ties the unique count to the size of the
rest of the corpus.** 144 falls out of an allocation grid (roles × rung-band partitions) that would
produce exactly 144 regardless of whether the base-type corpus were 400 or 4,000. That is fine as
far as it goes — the allocation is a legitimate way to derive *a* number — but it means "uniques as
a fraction of the obtainable item pool" (currently ≈19.5% of base types, before sets or the general
affix pool are even counted) is an emergent fact nobody chose, not a target anyone set. If the base
corpus grows in a later wave and the unique allocation grid does not grow with it (nothing in
`spec-budget.md` §4.2's derivation implies it would), unique density quietly drops and "how often do
I find something exciting" — the actual player-facing question behind "is 144 enough" — degrades
without any metric noticing, since no metric in `spec-analytics.md` measures density against total
obtainable pool, only distribution *within* the unique population itself.

**Recommendation:** add a declared density band (uniques ÷ total equip-eligible base types, or
similar) as its own budget row, separate from the raw count, so a future corpus-size change is
checked against a ratio a designer actually intended, not just against whatever the allocation grid
happens to produce.

---

## 10. The rung-band unique shape thins out exactly where the power curve already goes flat

**Severity: NOTE**

`spec-budget.md` §5's worked example targets `{30: 40, 50: 40, 70: 24, 90: 40}` — rung 70 gets 24
uniques against 40 at every other listed rung, a visible dip rather than a uniform 36-per-rung
split. Cross-referencing finding 3, rung `70` sits inside `heirloom`, one end of the flattest
relative jump in the whole progression curve (`chimeric → heirloom`, +39%, immediately followed by
the biggest jump of the whole ladder into `sunwoven`). Thinning the unique pool exactly at the rung
that already has the least exciting stat-only progression compounds the dead-zone feeling rather
than compensating for it — fewer "found something exceptional" moments land exactly where the raw
gear math already feels flattest. This may be entirely deliberate (perhaps 70 is a narrower content
band for an unrelated reason not visible in these three files), but the coincidence is worth a
deliberate check rather than being carried forward silently as "the example in the spec."

---

## 11. The degenerate strategy this model produces, named concretely

**Severity: MAJOR**

Findings 4 and 5 compose into a specific, predictable outcome rather than a vague "balance may be
off" risk. Every channel and every op-type is priced today by a formula admitted to be wrong in two
independent, stackable ways: `channelWeight` treats offense and survivability as interchangeable
when genre history says they are not, and `opWeight[More]` treats a stack-dependent value ratio as a
constant. Whatever the *actual* combat math ends up rewarding most — almost certainly some
combination of a `More`-tagged offense channel, since that is the channel type both findings say is
most likely underpriced by the current formula at typical late-game stacking levels — will be the
cheapest AE-for-power purchase available, for every unique and set author (human or pipeline) who
optimizes within the stated budget rules, for the entire window between "content ships" and
"telemetry exists to refit it." That is not a hypothetical exploit; it is the predictable output of
authoring content against a pricing function with two known, named, stackable blind spots, and it
will look like healthy content in every `metrics` check this spec set proposes, because none of
those checks price power against actual combat outcomes — only against the same formula that
produced the content in the first place. The rebalance tooling in `spec-numerics.md` §3 is the right
long-term fix; the risk is entirely in the gap between "ships" and "has enough telemetry to refit,"
which nothing in the build order (map §4) shortens or flags as a live-content risk.

---

## Summary

| # | Finding | Severity |
|---|---|---|
| 1 | `baseShare`/4.5× target never checked against the opposing (zombie/wave) power curve | BLOCKER |
| 2 | The 3×/6× floor-ceiling reasoning is asserted, not derived, and likely undersells genre norms | MAJOR |
| 3 | Curve is monotonic but not smooth — sawtooth relative deltas, flattest exactly at the two chase points | MAJOR |
| 4 | `channelWeight = 1.0` skips known ARPG priors (offense vs. survivability) and the refit arrives after content ships | MAJOR |
| 5 | `opWeight[More] = 0.55` is a constant modeling a stack-dependent ratio (1:1 to 4:1+ across the game's own progression) | BLOCKER |
| 6 | Multi-set base types create a jail risk the five documented anti-jail devices were never built to see | BLOCKER |
| 7 | "Four heaviest + four lightest" is sound in principle, with an undocumented tie, five permanently unique-less roles including helmet, and an uncosted opportunity-cost asymmetry | MAJOR |
| 8 | The shipped corpus still reflects the old "eight heaviest" allocation; the re-run is in flight, not landed | MAJOR |
| 9 | 144 has a confirmed count but no confirmed density relative to corpus size | MINOR |
| 10 | Unique rung-band shape thins exactly at the flattest part of the power curve | NOTE |
| 11 | Named degenerate strategy: findings 4+5 compose into predictable, exploitable mispricing for the entire pre-telemetry window | MAJOR |

3 BLOCKER, 6 MAJOR, 1 MINOR, 1 NOTE.
