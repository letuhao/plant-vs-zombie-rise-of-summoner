# Seedsmith spec set — adversarial audit: what's missing, and how this decays

**Scope of this pass:** absences and decay modes only. Not checking arithmetic, buildability, or
game-design taste — other passes own those. Read: `seedsmith-map.md`, all seven files under
`seedsmith/`, `item/build-log.md`, `item/review/README.md`.

**Read this note before the findings.** Every module spec is well-argued *within its own
boundary*. Almost everything below is a boundary problem: a defect class that falls between two
modules' stated scope, a claim of completeness measured against a list the same authors wrote, or
a mechanism that is honestly described as "visible" or "reasoned" without anything that forces a
human to act on the visibility. None of these are arithmetic errors. All of them are the kind of
gap that looks fine in a spec review and costs three waves to notice in practice — which is
exactly the shape of every incident in `build-log.md`.

---

## 1. Appendix A does not enumerate its own build's most frequent defect class

**Severity: BLOCKER**

Appendix A (map, lines 184–206) lists twenty defect classes "the agentic build actually produced."
Re-reading `build-log.md` end to end, the single most frequent recurring event in the whole log is
not on that list: **the checking tool itself was wrong and produced a false result** — either a
false GAP (content rejected that was correct) or a false PASS (a defect that produced zero errors
and zero warnings).

Count the instances, not the paragraphs:

- The "1,092 errors to zero" cleanup: **nine of eleven error classes were validator defects, not
  authoring defects** (build-log.md, "Stage 1b/1c cleanup").
- `{variant}` template rejection, shipped-family exemption, ten kinds stuck `Undefined`, the
  commander-namespace rejection, the markup-in-`notes` false positive, `TagAxisNotApplicable`
  enforced as a gate when the registry itself calls it "not an enforced constraint" — six more,
  each independently discovered, each a case of "the validator was rejecting the design working
  correctly."
- The `idLike` regex silently **not considering** `atom.keen_edge` a reference at all, so ten dead
  references produced zero errors and zero warnings (review/README.md §1) — a false PASS, not a
  false GAP, and structurally identical to the nine empty partitions: absence of red read as green.
- The gem-collision exemption bug (review/README.md §1): one early return collapsed five kinds'
  exemptions into "checked for nothing," and two entries shipped as the literal string "Mending
  Pulse" with the collision checker reporting clean.

That is at least **eleven separate incidents** of the checking tool itself being the defect,
against roughly **six** incidents that were genuinely bad content the tool correctly caught late or
never (`TagAxisNotApplicable`, `mass-class` too narrow, the retinue ladder confusion, gems having no
word pool). By raw count, "the checker is wrong" is the *dominant* defect class this build produced
— and it has no row in Appendix A, no family in `spec-metrics.md`'s ten families, and no place in
`covers`.

This matters specifically for seedsmith because `metrics` **is** the next generation of this exact
failure mode's source: a hand-written predicate that claims to check something and is itself wrong.
`spec-metrics.md` §6 requires each metric to ship "one fixture that must trip it, one that must
not" — which catches a metric that is wrong about *its own* rule, but nothing catches:

- A **shared utility bug** affecting several metrics at once (the `idLike` regex was one function
  read by every reference-shaped check; an equivalent shared helper in seedsmith — say, the id
  namespace matcher `corpus.md` §1 uses to discover edges — has the same blast radius and no
  fixture pins it, because no single metric owns it).
- A metric **enforcing a rule outside the scope the registry itself declares** for that rule (the
  `TagAxisNotApplicable`-as-gate case) — a fixture proves the metric fires on bad input and doesn't
  fire on good input, but does not prove the metric's *scope* matches the registry's stated scope,
  because nothing diffs a metric's enforcement surface against the registry's own `appliesToNote`-
  style carve-outs.

**Recommendation, stated as a gap rather than a redesign:** Appendix A should carry a 21st row —
*"the metric/check itself is wrong (false GAP or false PASS)"* — owned by `metrics`, with the
concrete mitigation this build actually discovered by accident (independent conformance scripts
that trust neither the agent nor the validator — build-log.md, "an independent script recomputes
every unique's (role, axis)... zero deviations"). Nothing in the current spec set proposes running
that kind of cross-check *systematically* rather than once, by hand, when someone happens to think
of it.

---

## 2. The completeness test proves conformance to a fixed historical list, not to defect-space

**Severity: MAJOR**

`spec-metrics.md` §5 and the map's Appendix A header both call the twenty-row table "the
completeness test for the catalogue." Read literally, the test asserts: every row *this build
happened to produce and someone happened to notice* has a claiming metric. That is a real and
useful test. It is not what "completeness" means to a reader who hasn't opened build-log.md, and
nothing in either document states the limit out loud.

Two consequences follow that the spec set doesn't name:

- **Appendix A is a survivorship list.** It contains defects that were caught. The identical-render
  pair (`Increased` vs `More` rendering the same line) was found "while confirming the rename" —
  i.e., by accident, during an unrelated fix. Had that accident not happened, it would not be row
  16, and the completeness test would still report 100% coverage of a twenty-row list that should
  have been twenty-one. A test that is complete against its own known-defects list will always
  read green immediately before the next defect class is discovered — the same shape as the nine
  empty partitions being invisible to two green validators.
- **The list does not grow itself.** The mechanism the map proposes for handling this ("a miss
  found by a human becomes a metric, permanently" — §7b) is a *pledge*, not a mechanism. Nothing in
  `spec-metrics.md` requires that adding a new Appendix-A row and writing its `covers` claim happen
  before the finding is closed, versus being logged as a TODO that competes with the next wave's
  authoring work. See §5 below (decay modes) for what happens to pledges with no forcing function.

**Recommendation, stated as a gap:** state explicitly, next to the completeness claim, what it does
and does not prove — "every defect class we have already seen is claimed" vs. "the catalogue cannot
miss a defect class." The gap between those two sentences is exactly where the nine empty
partitions lived.

---

## 3. Defect classes that cannot be a metric at all — acknowledged only partially

**Severity: MAJOR**

`spec-metrics.md` correctly splits CLOSED (verifiable) from OPEN (sampled) — that is P3, and it is
well specced. But the review lens for this pass is specifically: *what can be neither?* Three
things surfaced in `build-log.md`/`review/README.md` that no metric, closed or open, and no amount
of sampling, can catch:

- **Whether the registry/vocabulary itself is expressive enough for legitimate content**, as
  distinct from whether an agent invented outside it. `tags.v1.json → v2` (gems needed
  `combat-posture` and had no legal way to say it), `classes.v1.json → v2` (commander had no
  ladder at all), `mass-class` widened 3→5 values — three separate registry-completeness gaps,
  found only because an agent hit a wall and either invented a value (caught as `TagUnknown`,
  misclassified as an authoring error) or correctly refused. **`Vocabulary`/`Constraint` catch the
  symptom (an agent went outside the registry, or broke an unenforced rule) but nothing measures
  the registry's own sufficiency** — there is no metric family, closed or open, that asks "is this
  vocabulary big enough for what authors keep needing to say?" A high rate of `TagUnknown`-shaped
  pipeline retries is a real, machine-observable signal of exactly this, and nothing in
  `spec-pipeline.md`'s guardrails or `spec-metrics.md`'s families routes that signal anywhere.
- **Cross-feature interaction.** Every metric in `spec-metrics.md` is `(corpus, budget, numerics,
  adapter) → list[Finding]` for **one** adapter (§6, "Metrics are pure"). Once a second feature
  (world map, demons) exists, nothing in the module list checks whether the *combination* is
  balanced — e.g., item power curves making world-map combat trivial, or demon-contract yields
  interacting with item drop rates in a way neither feature's own budget can see. This is not an
  oversight inside any one spec; it is structurally excluded by every metric's signature, and the
  map's boundaries section (§5) doesn't mention it as an explicit non-goal either — it is simply
  absent.
- **Whether a design constraint is itself good for the game**, as opposed to whether it's held. Map
  §5 states this correctly for `budget` ("if the budget is wrong, the fix is a budget edit, not a
  metric edit") but the same logic applies to every `Constraint`-family rule extracted from a lane
  document, and nowhere is it said that the game-design *goodness* of, say, the 8-of-15 unique role
  quota is permanently outside seedsmith's reach, same as budget targets are. Minor as a gap, but
  worth one sentence since a reader could otherwise mistake `Constraint` for a design-quality gate.

None of these three is a defect in any one spec. They're gaps in the union — three separate
sentences, each in a different document, that individually sound complete and together leave a
hole none of the modules covers or claims.

---

## 4. Quality regression over time has no detector, and the design's own screening proxies could be one

**Severity: BLOCKER**

Direct answer to "what if the local model produces plausible-but-bland flavour that passes every
closed-loop check": `spec-pipeline.md` §6 handles this correctly *for a single run* — an open-loop
metric never reports a pass, only "content written, awaiting review," and pushes a sample to a
human. That part of the design is sound.

What is missing is anything that answers the actual question asked here — **regression over
time**, run after run, prompt-version after prompt-version, as the local model or its prompts
change. Nothing aggregates:

- The per-item screening proxies `spec-analytics.md` §7 already computes (MATTR, hapax rate,
  n-gram entropy) into a **corpus-level trend**. These are explicitly framed as "screening only,
  never a verdict" for a single item, which is right — but nothing stops the same numbers from
  also being tracked as a time series per pipeline/prompt version, which is a different and cheap
  use of data the design already produces. As specced, they are computed once, used to pick what a
  human reads next, and then thrown away. A silent slide in average MATTR across ten runs would be
  invisible to every mechanism in the document.
- The human sampling verdicts themselves (`spec-pipeline.md` §6: "their verdict is recorded against
  the sample"). Recorded where, aggregated how, compared across prompt versions how — not stated.
  If a verdict is only ever consulted for the one finding it closed, "plausible but bland, forever"
  produces the same *shape* of review-queue entries every time and nothing distinguishes that from
  "occasional miss, otherwise fine," because no document proposes comparing verdict rates across
  time.

This is a real, specific, and inexpensive gap to name because the fix is not a new subsystem — it
is treating data the spec already generates as a trend instead of a single-run scratch value. As
written, nothing in the pipeline design would notice the local model's output quality declining
until a human happened to notice by eye, which is precisely the failure mode `spec-analytics.md` §7
exists to reduce reliance on.

---

## 5. Decay modes: measure-only has no expiry, and suppression has no anti-ritual guard

**Severity: MAJOR**

**How this becomes ignored, mode 1 — measure-only forever.** `spec-metrics.md` §4: "New metric →
`gates=False`, runs, reports... *Then* a threshold goes into `budget` and `gates` flips." Correct
sequencing, but nothing states who does the promotion, on what cadence, or what happens if nobody
does. A metric sitting at `gates=False` is indistinguishable, in CI output, from a metric that
passed — it reports, but reports never block anything and (per `spec-foundation.md` §3) "nobody
reads past the summary when the summary is fine." A measure-only metric that nobody calibrates is a
green checkmark that means nothing, which is the exact epistemic failure that let nine partitions
sit empty for three waves. The two are structurally identical; the map even says so about the
*old* validators ("two validators were green because neither was asked...") without noticing that
an uncalibrated `gates=False` metric produces the same green for the same reason.

**How this becomes ignored, mode 2 — suppression as ritual.** `spec-metrics.md` §4 requires
suppression to be "per-finding, expiring and reasoned," with an explicit ban on "permanent, blanket,
unreasoned" suppression. Good design against the failure it names. It does not guard against the
failure one step over: an expiring, reasoned suppression that gets **renewed on schedule** by
whoever is running the next dispatch, with the same reason string, indefinitely. Nothing caps how
many times a given `(metric, subject)` pair may be re-suppressed, and nothing routes a
third-or-more renewal to a different decision (escalate, re-derive the budget row, or actually fix
the content) instead of a fourth extension. "Expiring" only resists decay if something happens
*at* expiry other than a human copy-pasting a new date.

**The most likely permanently-suppressed metric, named concretely.** Four of fifteen item roles
(`ward-array`, `mantle`, `head-guard`, `sense`) are explicitly "hollow until E12" — a module that
does not exist yet and has no date attached anywhere in these seven specs. Any `Coverage` or
`Distribution` finding keyed to full role coverage will flag these four roles on every run, forever,
until E12 ships. The honest, correct suppression reason is "pending E12," and because E12 has no
committed date, that suppression's expiry will simply be pushed forward at every renewal — the
exact "permanent suppression wearing an expiry-date costume" pattern §4 is trying to prevent, and
it is not hypothetical: the corpus already has the four hollow roles today, before seedsmith is
even built.

---

## 6. Budget-target editing: the mitigation shows the change, but the sanctioned calibration method is procedurally the same motion as the failure it warns against

**Severity: BLOCKER**

`spec-budget.md` §6 mitigation for "targets get edited to match reality instead of fixing content":
`budget diff v1 v2` shows which targets moved and which findings would clear as a result, so "the
consequence of a target change is visible before it lands." That's visibility, not a control — it
requires a human to look at the diff and object, with no check enforcing that anyone does, and no
distinction encoded between "this target changed for a documented, reviewed design reason" and
"this target changed to turn a red build green."

The sharper problem: **the spec's own sanctioned method for setting thresholds is the identical
motion to the failure it warns against, one layer down.** §5's evenness bands are set by exactly
this process — "measure, look, set the band from what a healthy corpus actually scores" — deferred
gating until after the number is known, then setting the gate to match what was observed. That is
correct practice for a genuinely uncalibrated statistic (nobody can name a correct Pielou value in
advance) — but it is also, mechanically, "adjust the target until it matches what the content
already does," which is word-for-word the pattern §6 calls "cheating" one section earlier when it
happens to a count instead of a distribution shape. The spec never states what tells these two
motions apart other than the diff being visible, and visibility was already true of the "cheating"
case too — the diff would show a count target moving to match content exactly the same way it shows
an evenness band being set from a first measurement. Nothing but intent distinguishes them, and
intent is not a checkable property.

**Recommendation as a gap, not a fix:** the spec needs a written rule for telling a legitimate
calibration from a rationalized one — e.g., a budget-version bump that *widens* a target or
tolerance to match falling content requires a one-line rationale distinct from "matches the
corpus," while the *initial* calibration of a previously-unset band does not, because there is
nothing yet to have gamed. As written, both look identical in the diff.

---

## 7. Human sampling is stratified by kind × band; the build's single highest-value catch needed a cross-frame comparison the design doesn't structurally produce

**Severity: BLOCKER**

`spec-analytics.md` §8 specifies stratified sampling by "the dimension that matters (kind, then
band)," with proportional or Neyman allocation. This is good practice for "does this partition read
well," and it is explicitly the mechanism the operating model (map §7b) relies on for every
open-loop verdict a human will ever give.

Decision 12 in `build-log.md` — labelled by its own author as **"the single most valuable thing the
pilot found," and one "no schema check could have found"** — was not produced by stratified
sampling within a partition. It was produced by a human reading twelve names from one small pilot
batch and then, on their own initiative, **laying the plant pools and humanoid pools side by side**
to notice that one frame's vocabulary was botanical and the other was European-armoury fantasy, in
a game where zombies wear traffic cones and buckets. That comparison is *across* the one dimension
(`frame`) the stratification scheme does not name, and it required an explicit human decision to
compare rather than just read — nothing in the sampling spec produces or nudges toward that
comparison. A reviewer sampling `humanoid.armament-primary.a` and, separately and much later,
`plant.armament-primary.a` has no structural reason to hold both in mind at once, especially once
review happens per-metric-per-run rather than as one person reading a whole wave.

This is not a claim that stratified-by-kind×band sampling is wrong — it answers "does this
partition read well" correctly. It is a claim that **the specific failure mode the build's own
retrospective calls its most valuable catch is one this sampling design does not structurally
surface**, and nothing in `spec-analytics.md` §8 or `spec-pipeline.md` §6 proposes a paired or
cross-dimension sample (e.g., "when sampling partition X, also sample X's sibling across the
dimension not being stratified on") to compensate.

**On "how much human time does this really cost," also asked by this brief:** nowhere in the seven
specs is a sample size actually fixed. `--sample N` is illustrated with N=8 against 60 items in
prose, never declared as a policy (fixed count, fraction of corpus, or fraction of *new* content
per run). The generator's own stated future scale is ~30,000 rows (map, opening paragraph); if `N`
stays a small fixed constant while the corpus and the number of open-loop metrics both grow, the
fraction of the corpus a human ever sees falls toward zero exactly as volume rises — the opposite of
what "manual work is minimised" (map §7b) is supposed to mean, and there is no stated floor (e.g.,
"never less than X% of new content per run") preventing that collapse. The document that commits to
minimizing manual work never puts a number on the manual work it costs, in either direction — that
absence should be closed before W3 ships anything to review.

---

## 8. Second feature: the stub adapter proves the interface compiles, not that the algorithms generalise, and at least two concrete seams are missing

**Severity: MAJOR**

Map decision 3 and `spec-foundation.md` §2 are honest about scope: the stub "exists only in the
test suite... roughly 5% the cost of a real second feature," proving only that the core doesn't
reach into item concepts *by name*. Two concrete things the stub cannot exercise, and that a real
second feature (world map or demons, per the standing programs) will hit:

- **`legal_combinations()` returns a pairwise boolean legality function** (`spec-foundation.md` §2).
  That is exactly right for items, where illegality is a flat pair like `ward-array × hybrid`. World
  map content (per the shipped `LaneGraph`/`SupplyGraph`/`LaneCost` modules already in
  `src/FusionRpg.Core/World/`) is graph-shaped: legality is a question about topology — "this lane
  exists only if these two nodes are adjacent" — not a fixed table over two flat enum dimensions.
  A stub with "two kinds and two dimensions" cannot reveal whether `Dimension`/`LegalityFn` as
  typed can express a graph-adjacency constraint at all; if it can't, `metrics`' pairwise-coverage
  algorithm (`spec-analytics.md` §2.2) — which assumes `legal(a,b)` is a cheap, static, enumerable
  predicate — may need to become graph-aware for feature two, which is exactly the kind of
  core-level rewrite the whole map exists to prevent, and the stub is too small to have surfaced it.
- **`budget derive`'s SSOT-parsing script has no owner in the interface.** `spec-budget.md` §2
  describes deriving `budget.v1.json` by "walking every SSOT, the fleet plan and the naming
  allocation" — all item-specific document names and structures. `spec-foundation.md`'s
  `SeedAdapter` protocol (`kinds`, `dimensions`, `legal_combinations`, `registries`, `channels`) has
  no method for "where this feature's stated targets live and how to extract them." If that parsing
  logic is written into core `budget`, it is not feature-agnostic despite the module being labelled
  so; if it belongs in the adapter, no spec says so, and the interface as written doesn't have a
  slot for it. Either way, the second feature cannot get a `budget.json` the way items did without
  either extending the adapter protocol (undocumented) or hand-rolling a parallel derivation script
  per feature (contradicts P4's "the plan is deterministic, not per-feature bespoke code" spirit).

The stub is a good, cheap check and worth keeping — the finding here is only that its own stated
limit ("~5% the cost") should be read as "it catches interface-shaped leaks, not algorithm-shaped
or protocol-completeness ones," and nothing currently says that second half out loud.

---

## 9. Operational: a lesson the corpus build already learned is not carried into `budget` versioning

**Severity: MAJOR**

`build-log.md` decision 14 is explicit and hard-won: uncontrolled registry version bumps "invalidated
everything authored before it," breaking convergence for a 125-agent build, fixed by giving every
registry a `minCompatibleVersion` — additive bumps warn, breaking bumps fail. This is presented in
the same document set as a load-bearing lesson.

`spec-budget.md` §6 versions `budget.v{n}.json` with "the same discipline as tier-bands" — a
target change is "deliberate, reviewable, revertible," backed by `budget diff`. Nowhere does it
adopt the additive/breaking distinction decision 14 already proved necessary for a structurally
identical versioned artifact. A `budget` bump that **narrows** a tolerance or **removes** a
dimension a suppression file still references would, on the current spec, either silently
invalidate that suppression or silently keep applying it to a target that no longer means what it
did — the same class of defect `minCompatibleVersion` exists to prevent, reappearing one module
over, unaddressed by a spec written by the same author who documented why it was needed.

Separately, and smaller: **`spec-numerics.md`'s own headline dependency — `BattleRuleset.BaseHp`/
`BaseAtk` in `src/FusionRpg.Core/Battle/BattleModels.cs` — lives in a different toolchain (C#) than
seedsmith (Python, stdlib-only per `spec-foundation.md` §5).** The spec correctly says "if the
ruleset changes, every magnitude moves with it, which is correct" — but nothing describes a CI or
process link that runs seedsmith's metrics when that C# file changes. A combat-balance PR that
changes `BaseHp` has no stated mechanism prompting anyone to re-run `seedsmith metrics` and look at
what moved; the coupling is real (numerics reads that file) but the operational trigger connecting
the two toolchains is not specified anywhere in the seven documents.

---

## 10. Reproducibility: model provenance is a bare string, not a pinned artifact

**Severity: MINOR**

`spec-pipeline.md` §4 provenance fields include `model` (example value: `"local:gemma-26b"`) as
part of "answering 'why does this row exist' months later." A local model's weights can change
(a new checkpoint, a quantization change) without the string changing, unlike a hosted API's dated
model id. If the intent is genuine month-later reproducibility, the provenance record needs a
checkpoint hash or equivalent, not a family name — as specced, two rows generated by materially
different model weights would carry identical provenance and be indistinguishable during a later
investigation.

---

## 11. Aspirational sentences that read as decisions but specify no mechanism

**Severity: NOTE** (each individually small; listed together because the pattern recurs)

- Map §7b: *"Manual work is minimised."* No metric, no baseline, no target — see §7 above for the
  concrete version of this gap (sample size never fixed as a policy).
- `spec-metrics.md` §4: *"Its numbers get looked at against a corpus believed healthy."* Who
  declares health, and against what standard, before using that corpus to set a gating threshold?
  If the corpus used for calibration still carries known defects (the four hollow roles, historically
  the 30 uncompletable sets before that was fixed), "healthy" quietly bakes the surviving defects in
  as the new normal, and nothing flags that the calibration input itself was not clean.
- `spec-planner.md` §5: *"Model tier by rule table, not optimisation."* The table's actual contents
  are never given, in this spec or any other — reasonable to defer, but as written there is no
  owner assigned for writing it, and per §8 above, no stated process for extending it when a second
  feature's kinds need classifying.
- Map §7b, third bullet: *"A miss found by a human becomes a metric, permanently."* No queue, no
  SLA, no owner. As a sentence it reads like a settled process; as a mechanism it is a hope, and
  §5 above shows what a hope with no forcing function decays into.
- `spec-budget.md` §5: *"the band gets set from what a healthy corpus actually looks like"* — the
  same "healthy" ambiguity as above, applied to evenness targets specifically.

None of these is wrong to leave open — some genuinely can't be answered before the corpus exists to
measure. The gap is that they are written in the declarative voice of a decision ("is minimised,"
"gets set," "becomes a metric") rather than the flagged voice the rest of these documents otherwise
use well elsewhere (`spec-numerics.md` §5's "Open, and deliberately so" is the right model for this
and should be applied to the five sentences above too).

---

## Summary table

| # | Finding | Severity |
|---|---|---|
| 1 | Appendix A omits its own most frequent defect class: the checker itself is wrong (false GAP/PASS) | BLOCKER |
| 2 | Completeness test proves conformance to a fixed historical list, not to defect-space; not stated | MAJOR |
| 3 | Registry-sufficiency and cross-feature interaction are un-metricizable and only partly acknowledged | MAJOR |
| 4 | No detector for LLM quality regression over time; existing screening proxies are thrown away per-run | BLOCKER |
| 5 | Decay: measure-only metrics have no calibration deadline; suppression has no anti-renewal guard; the 4 hollow roles are a named, present-day candidate for permanent disguised suppression | MAJOR |
| 6 | Budget-diff visibility doesn't distinguish legitimate calibration from target-editing-to-pass; the spec's own sanctioned method is the same motion | BLOCKER |
| 7 | Sampling stratifies by kind×band; the build's highest-value catch needed a cross-frame comparison this design doesn't produce; sample size is never fixed as a scaling policy | BLOCKER |
| 8 | Stub adapter proves interface compiles, not algorithm generalisation; `legal_combinations` and budget-derivation have no answer for graph-shaped or non-item feature data | MAJOR |
| 9 | `budget` versioning doesn't carry forward the `minCompatibleVersion` lesson `build-log.md` already paid for; no CI link from BattleRuleset changes to a seedsmith re-run | MAJOR |
| 10 | Model provenance is a bare string, not a pinned checkpoint — insufficient for month-later reproducibility | MINOR |
| 11 | Five sentences read as decisions but specify no mechanism (manual work minimised, "healthy" corpus, model-tier table contents, "becomes a metric," evenness band-setting) | NOTE |
