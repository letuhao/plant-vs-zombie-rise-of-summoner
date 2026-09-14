# Seedsmith content-completeness — `core`

**Status:** Proposed 2026-09-08. Module 1 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Idea phase:
[seedsmith-content-standard-ideal.md](../seedsmith-content-standard-ideal.md). Gated on nothing —
first module to build.

The shared engine every domain (`items`, `actions`, `creatures`, `dungeon`, `passive-tree`) adopts once,
instead of each reinventing its own ledger/metric/backfill shape.

---

## 1. Objective

Give every seedsmith domain the same answer to three questions, using one shared implementation:

1. **Is this record's player-facing content missing?** (a name/flavor/description field never
   generated)
2. **Is an existing record's content stale?** (its brief, schema, or model version changed since it
   was generated) — reported, never auto-acted on (see §6 Boundaries).
3. **How does a resumed run fill genuinely missing content, and how does a person force a full
   regeneration when they actually want one?**

**Why one module, not five:** `spec-pipeline.md` §1's own lesson from ~90 real agent runs —
*"effort belongs in the brief, the schema and the gate, not in prompt cleverness"* — generalizes
directly: a missing-content or stale-content gap is a gate nobody built yet, and building it once
here is that lesson applied rather than re-learned five times. The audit backing this spec already
found the five-times version happening: `stale_ids()`-shaped code exists independently in
creatures (×2, both live), dungeon, items, and structures (the latter three dead — zero production
callers) — proof the ad-hoc-per-domain shape doesn't converge on its own.

## 2. What already exists, and what this module builds on vs. replaces

**Builds on, unchanged:**
- `pipeline/run_ledger.py`'s `RunLedger` — already the exact shape this module needs for the
  automatic/manual split: `RunLedger.plan(subject_ids, is_valid)` returns ids needing work,
  `RunLedger.force(subject_ids, scope)` is the **already-real, already-named** manual override
  (`scope="ids"` or the literal `scope="all"`, refused otherwise — `run_ledger.py:83-93`). Nine real
  item generators already depend on it (`recipegen`, `consumablegen`, `combogen`, `droptablegen`,
  `basetypegen`, `sockets`, `materialgen`, `affixfamgen`, `milestonegen` — grep-confirmed). This
  module does not replace `RunLedger`; it standardizes how domains call it for completeness
  specifically, and adds the one thing it doesn't do: staleness *reporting* (below).
- `pipeline/provenance.py`'s `Provenance`/`ProvenanceLedger`/`should_generate` — the
  finding-scoped idempotence layer. Real, but adopted by only one call site
  (`test_cp_g_end_to_end.py`) outside its own test — this module does not require every domain to
  adopt `ProvenanceLedger` specifically; `RunLedger`'s simpler done-map is the one with real,
  multi-domain production adoption today, so it is the one this module standardizes on.
- `metrics/model.py`'s `Metric`/`Finding`/`Ctx`/`Loop`/`Severity` — the existing metric framework.
  Every metric this module adds is `gates = False` (per the owner's own settled decision) and
  `loop = Loop.CLOSED` (missing/stale content is deterministically checkable and the fix is
  provable, matching `FlavourMissing`'s own classification, `metrics/quality.py:27`).
- `workflow/validators/language.py`'s `language_consistency` — real, proven, wired into 2 domains.
  Fixed here (§5), not replaced.

**Names as the field-naming convention, not a new term:** this module's manual-regeneration
parameter is called **`--force`**, matching two existing real precedents exactly — this is a
consistency fix from the plan/todo docs' earlier placeholder `--overwrite`: `RunLedger.force()`'s
own method name (`run_ledger.py:83`) and `generate_commander_effects.py --force`
(`generate_commander_effects.py:68`, *"regenerate every subject, discarding existing entries"*).
Inventing a third name for the same concept is exactly the kind of divergence this program exists
to stop.

**New, built by this module:**
- `pipeline/staleness.py` — a domain-agnostic staleness-key hash function (§4), generalizing
  `adapters/dungeon/provenance.py`'s own `staleness_key`/`stale_ids` shape (`briefHash +
  promptVersions + registryVersions + motifSubsetHash` — real, well-designed, confirmed dead code,
  §"What already exists" in the ideal doc) into a form any domain can call.
- `metrics/content_completeness.py` — the generalized missing-field metric registry (§5),
  generalizing `metrics/quality.py:24`'s `FlavourMissing`/`FLAVOR_EXPECTED_KINDS` from one hardcoded
  frozenset into a per-domain registration.
- The backfill loop's own thin wrapper (§4) — not a new ledger, a documented calling convention over
  `RunLedger` that every domain's own adapter uses the same way.
- The bidirectional fix to `language_consistency` (§5).

## 3. Automatic-backfill contract (resolved 2026-09-08 — this is the load-bearing decision)

A resumed generation run is **fully automatic, no human gate, for genuinely MISSING content only**
— on the first run or any later run. This is `RunLedger.plan(subject_ids, is_valid=lambda id, entry:
entry is not None)`: "needing work" means **no entry exists at all**. Nothing about a record's
staleness key feeds into this decision.

**Existing content is never touched by an automatic run, regardless of whether its staleness key has
changed.** A resumed run treats "exists" as "done," full stop — this is the direct generalization of
`generate_commander_effects.py`'s own stated reason (`generate_commander_effects.py:86-88`):
*"Generation is stochastic, so regenerating an entry that is already correct DESTROYS good content
and costs model time for nothing."* This program keeps that caution rather than overriding it.

Regenerating existing content (stale or not) happens **only** through the explicit, human-invoked
`--force` parameter — `RunLedger.force(subject_ids, scope)`, called by a domain's own CLI when a
person passes `--force`. `scope="all"` regenerates every targeted subject unconditionally; there is
no selectively-diffed "regenerate only the stale ones" automatic mode — that would require trusting
a stochastic model to know when its own prior output was "close enough," which is exactly the risk
the precedent above warns against. A person who wants to refresh only what's stale reads the
staleness-count metric (§5) first, then scopes their own `--only`/`--force` invocation by hand.

The staleness key (§4) is computed and stored purely for **reporting** — a `gates=False` metric
saying "N records are stale, re-run with `--force` to refresh them" — never as an automatic trigger.

## 4. The staleness key

`pipeline/staleness.py`:

```python
def staleness_key(*, brief_hash: str, prompt_version: str, schema_version: str,
                  model_id: str) -> str:
    """A stable hash of everything that would make a freshly-generated record differ from an
    existing one. Order-independent inputs are pre-sorted before hashing so field order in a
    caller's dict never changes the result."""
```

Four inputs, all already real, named things somewhere in this codebase — no new concept invented:
`brief_hash` (a hash of the rendered brief text — the model's actual input), `prompt_version`
(every domain already versions its prompts — e.g. `PROMPT_VERSION` bumped for the tree ExclusionRate
fix this session), `schema_version` (the JSON Schema version a `Pipeline` was constructed with),
`model_id` (the model that produced the content — content generated by a since-replaced model is a
real staleness signal even with an unchanged brief). This is the dungeon precedent's own four-part
shape (`briefHash + promptVersions + registryVersions + motifSubsetHash`,
`adapters/dungeon/provenance.py:37-47`) generalized: `registryVersions`/`motifSubsetHash` were
dungeon-specific vocabulary-freshness signals, folded here into the more general `schema_version` +
`brief_hash` (a brief that inlines a vocabulary, per `spec-pipeline.md` §3.3, already changes
`brief_hash` when that vocabulary changes — no separate field needed).

`is_stale(recorded: Mapping, current: Mapping) -> bool` — a record with no staleness key at all
predates tracking and is reported stale (cannot be proven current, matching
`dungeon/provenance.py:59-61`'s own rule).

## 5. The missing-field metric registry

`metrics/content_completeness.py`, replacing `FLAVOR_EXPECTED_KINDS`'s hardcoded frozenset with a
registration call:

```python
@dataclass(frozen=True)
class CompletenessSpec:
    domain: str                      # "items", "actions", "creatures", "dungeon", "passive-tree"
    kinds: frozenset[str]            # which entry kinds this applies to (may be all of a domain)
    field: str                       # which field counts as "missing" when falsy — domain's choice
    is_missing: "Callable[[Mapping], bool] | None" = None   # override the falsy-check if a domain's
                                                              # own "missing" isn't just "falsy"

_REGISTRY: "list[CompletenessSpec]" = []

def register_completeness(spec: CompletenessSpec) -> None: ...

class ContentFieldMissing(Metric):
    id = "Content/FieldMissing"
    family = "Content"
    loop = Loop.CLOSED
    gates = False   # every new metric starts here, per the owner's own settled decision
    ...
```

`FlavourMissing` (`metrics/quality.py:24`) becomes the FIRST registered spec (items, the six
`FLAVOR_EXPECTED_KINDS` kinds, field `"flavor"`) — proving the generalization is additive. Its own
existing behavior must be byte-identical after the refactor (Task 3's acceptance).

**Each domain's own "missing" definition is that domain's own decision, stated in that domain's own
module spec** (Tasks 5/7/9/11/13) — not inherited blindly from items' `falsy-string` check. The
plan's own Risks table already names why: an intentionally-empty field is not the same defect as a
never-generated one, and only the domain's own schema owner knows which is which.

A companion stale-count metric (`Content/FieldStale`, same shape, reading `staleness_key` instead of
falsiness) reports the §3/§4 staleness signal — also `gates=False`, also never a regeneration
trigger.

## 6. The bidirectional language-contamination check

`workflow/validators/language.py`'s `language_consistency` (line 26) only fires when the subject's
own input `motifs` are CJK (line 32: `if not motifs or not any(_CJK.search(m) for m in motifs):
return []`). The real, already-found dungeon defect
(`data/seed/dungeon/events/event.bargain-creature.allpeater-001.json`'s `flavor` field) ran the other
direction: English motifs, a model output that unexpectedly contains CJK fragments anyway. Add the
reverse check unconditionally — regardless of the subject's own motif language, flag any output
field whose value mixes `_CJK` and `_LATIN_WORD` matches. The existing CJK-motif direction's own real
historical incident (`commander_effect.py`, 87% code-switched on a real 84-draft run) must still be
caught unchanged — this is an addition to the existing check, not a replacement of its logic.

Registered into `content_completeness.py`'s own registry (§5) as a `Content/LanguageContamination`
metric (or as a defect the `ContentFieldMissing` predicate itself surfaces — Task 1's own worked
example against real data decides which shape reads better; not pre-decided here) so every domain
that adopts `core` gets the fixed, bidirectional check for free, rather than each domain importing
`language_consistency` piecemeal the way creatures/passive-tree do today.

## 7. Commands

```powershell
# Run every registered completeness/staleness metric against a real corpus (report-only, exit 0
# regardless of findings — matches FlavourMissing's own current gates=False contract):
python -m seedsmith.report.cli check --adapter <domain>

# A domain's own generator, resumed (automatic, missing-only):
python -m seedsmith.adapters.<domain>.<generator>.run

# The same generator, forcing a full regeneration of named subjects or everything:
python -m seedsmith.adapters.<domain>.<generator>.run --force --only <id1,id2>
python -m seedsmith.adapters.<domain>.<generator>.run --force --all
```

## 8. Project structure

```
tools/seedsmith/seedsmith/pipeline/staleness.py         (new) — §4
tools/seedsmith/seedsmith/metrics/content_completeness.py (new) — §5
tools/seedsmith/seedsmith/workflow/validators/language.py (edited) — §6
tools/seedsmith/tests/pipeline/test_staleness.py         (new)
tools/seedsmith/tests/test_content_completeness.py       (new)
tools/seedsmith/tests/workflow/validators/test_language.py (new — validators/ has no existing
                                                              test subtree; created here)
```

## 9. Code style

Match this program's own established style exactly (visible throughout `pipeline/`, `metrics/`):
frozen dataclasses for value shapes, module-level docstrings that state *why* a design choice was
made (not what the code does), real file:line citations in comments where a decision generalizes an
existing precedent, `from __future__ import annotations`, no bare literals — a domain/kind string is
always a real value passed in, never hardcoded inside `core`'s own module.

## 10. Testing strategy

Every new function proven against a REAL passive-tree fixture (the plan's own Checkpoint 0
requirement), not synthetic data — passive-tree already has the most mature real corpus this program
touches. `python -m pytest tools/seedsmith/tests/pipeline/test_staleness.py
tools/seedsmith/tests/test_content_completeness.py tools/seedsmith/tests/workflow/validators/test_language.py -q`
green, plus a full `python -m pytest tools/seedsmith/tests -q` re-run confirming no new failures
beyond the already-documented pre-existing cluster.

**Worked examples required in this spec's own review, not deferred to Task 2-4's implementation:**
1. A real passive-tree node's brief + `PROMPT_VERSION` + model id → a stable `staleness_key`;
   changing `PROMPT_VERSION` changes the key.
2. `FlavourMissing`'s real items corpus, re-run through the new registry, produces byte-identical
   findings to today's hardcoded version.
3. A fixture reproducing the actual dungeon defect shape (English motifs, CJK output fragment) is
   caught by the fixed `language_consistency`; the existing CJK-motif direction's own regression
   fixture (from `commander_effect.py`'s real historical incident) still passes unchanged.

## 11. Boundaries

- **Always:** keep every new metric `gates=False`; keep the automatic path missing-only per §3; run
  the full seedsmith test suite before considering a task done; cite real file:line evidence for
  every generalization claim, matching this program's own established discipline.
- **Ask first:** promoting any metric to `gates=True` (a separate, later, explicit decision per the
  owner's own settled scope); changing `RunLedger`'s own public API (nine real generators depend on
  it today) instead of building alongside it.
- **Never:** make stale-content regeneration automatic under any condition (§3 is a settled decision,
  not a default open to reinterpretation by a later task); invent a domain-specific "missing" or
  "stale" definition inside `core` itself — those are each domain's own module spec's job (§5).

## Open questions

None — §3's automatic-backfill contract, the prior sharpest open item, is resolved above. The two
items the plan's own "Open questions" section still lists (exact staleness-key inputs for the FIRST
adopting domain; whether `Content/LanguageContamination` is its own metric or folds into
`ContentFieldMissing`) are answered by this spec's own §4/§6 above, pending the worked-example
review in §10 confirming they hold against real data.
