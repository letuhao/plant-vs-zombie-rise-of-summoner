# Tasks: Seedsmith — full program

Plan: [seedsmith-plan.md](seedsmith-plan.md) · Map: [../docs/architecture/seedsmith-map.md](../docs/architecture/seedsmith-map.md)

Status: **Part 1 (W1) DONE.** Part 2 (W2 — planner + briefkit) and Part 3 (W3 — pipeline) are
planned below (P1–P6, G1–G3), not started.

---

## Part 1 — W1: measurement (COMPLETE)

**ALL TASKS DONE (S0-S10, 165/165 tests green, CP-A through CP-E all reached).**
`tools/seed_graph/` retired; `seedsmith` is the sole reachability gate, armed in CI. Specs
complete and audited (66 findings, 11 blockers, all closed).

---

## Phase 0 — `llm_caller` (independent — no dependency on Phase 1+)

### S0 — port the LM Studio caller
`tools/seedsmith/seedsmith/pipeline/llm_caller.py`

Port of `D:\Works\source\lore-weave\scripts\i18n_translate.py`, proven in production on that
project's translate pipeline. Per spec-pipeline.md §5.1: transport and JSON extraction are copied
as-is; the self-heal loop is generalized so `verify_fn` is supplied per caller instead of hardcoded
to translation checks. No import from `corpus`, `adapters`, or `metrics` in either direction — this
is why it needs no dependency and can build now rather than waiting on W3's gate.

- `call_model(system, user, *, temperature)`: configurable endpoint/model (defaults: LM Studio
  `http://localhost:1234/v1/chat/completions`, `google/gemma-4-26b-a4b-qat`); every call sends
  `reasoning_effort: "none"` **and** `chat_template_kwargs: {enable_thinking: False, thinking:
  False}` — both, unconditionally, because different servers/templates read different keys; 2-attempt
  retry on `URLError`/`TimeoutError` only (no retry-storm against a wedged queue)
- `extract_json(text)`: strip ` ```json ` fences/prose, parse the first `{...}`; regex fallback for
  an unescaped `"` inside a value when strict `json.loads` fails
- `call_with_self_heal(items, system, build_user, verify_fn, max_heal)`: generalized from
  `translate_chunk()` — `verify_fn(items, out) -> (hard, soft)` is a parameter now, so a future
  pipeline (flavour text, set headers, …) supplies its own hard/soft rule instead of the ported
  translation-specific one; on exhausted heal rounds, falls back to a caller-supplied default per
  key and reports which keys failed, never blank
- Config adjustable via `seedsmith.toml` (endpoint, model, `max_heal`, timeout), ported script's
  values as defaults

**Acceptance**
- [x] A stdlib `http.server` fixture captures the outgoing request body; test asserts both
      reasoning-disable keys are present on every call, not just the first
- [x] `extract_json` fixtures: clean JSON, fenced JSON, prose-wrapped JSON, and one
      unescaped-quote case each parse correctly
- [x] `call_with_self_heal` fixture: a `verify_fn` that fails once then passes proves the retry
      re-prompts with the *named* defect (mirrors the ported script's "name the exact defects" design)
- [x] `call_with_self_heal` fixture: a `verify_fn` that never clears within `max_heal` falls back to
      the caller-supplied default and reports exactly which keys failed
- [x] A test greps the module for `from seedsmith.(corpus|adapters|metrics)` and fails if found —
      the zero-dependency claim is enforced, not asserted in prose
- [x] Suite runs fully offline — the mock server is the only "model" ever called
- [x] (beyond the original list, added on review) `load_config()` reads `[pipeline.llm_caller]`
      from `seedsmith.toml` per spec-foundation §7.3; missing file/table falls back to
      `DEFAULT_CONFIG`, malformed TOML raises rather than silently defaulting
- [x] (beyond the original list, added on review) `call_model` retries `attempts` times against a
      genuinely unreachable endpoint and then raises `RuntimeError`, proven without a mock

**Verify** `python -m pytest tools/seedsmith/tests/test_llm_caller.py -v` → **17 passed** (2026-08-23)

**Built:** `tools/seedsmith/seedsmith/pipeline/llm_caller.py` (`LlmCallerConfig`, `load_config`,
`call_model`, `extract_json`, `call_with_self_heal`) · `tools/seedsmith/seedsmith/__init__.py` ·
`tools/seedsmith/seedsmith/pipeline/__init__.py` · `tools/seedsmith/tests/test_llm_caller.py` (17
tests across `ReasoningDisabledTests`, `RetryExhaustionTests`, `ExtractJsonTests`, `SelfHealTests`,
`LoadConfigTests`, `DependencyIsolationTests`).

**S0 status: DONE.**

---

**Note:** S0 does not unlock `pipeline`'s generation logic — that still gates on `metrics` and
`planner` existing, so a pipeline's success can be graded against a real finding (spec-pipeline.md
§2). S0 only proves the transport-and-self-heal mechanism, in isolation, before anything depends on it.

---

## Phase 1 — walking skeleton

### S1 — corpus + stub adapter + one metric + report + CLI
`tools/seedsmith/`

Build the whole path with the smallest possible content: load a corpus, ask one metric, print a
finding, exit with the right code.

- `corpus/`: `Entry`, `Corpus.load()`, `by_id` / `by_kind` / `by_partition`, edge discovery,
  `is_exemplar`, minted-runtime-id registration
- `adapters/base.py`: `SeedAdapter`, `KindSpec`, `Dimension`, `Channel`, `LegalityFn`, `RegistrySet`
  — full field definitions per spec-foundation §7.2
- `adapters/_stub/`: two kinds, two dimensions, one channel, one illegal pair. **Tests only.**
- `metrics/`: `Metric`, `Finding`, `Loop`, severity, the registry, the runner
- `metrics/coverage.py`: `Coverage/EmptyPartition` only
- `report/`: human CLI, JSON out, exit codes
- `__main__.py`: `seedsmith check`

**Acceptance**
- [x] `seedsmith check --adapter stub` → clean fixture: `0`; broken fixture: `1`
- [x] Unreadable corpus → `2`, distinct from `1`, with a message naming the file
- [x] `Loop.OPEN` + `gates=True` raises at registration
- [x] A metric whose `needs` are unmet emits `NOT_MEASURED`, never a pass
- [x] `Finding` carries `schemaVersion`
- [x] Package is `seedsmith/__main__.py` — no `seedsmith.py` shadowing the package

**Verify** `python -m seedsmith check --adapter stub tests/fixtures/clean && echo OK` → prints
`no findings`, exit `0` (2026-08-23, run from `tools/seedsmith/`)

**Built:** `seedsmith/corpus/{model,__init__}.py` (`Entry`, `Edge`, `Corpus.load`, `by_id`/
`by_kind`/`by_partition`, `discover_edges`, `register_minted_ids`/`resolves`, `is_exemplar`,
`CorpusLoadError`) · `seedsmith/adapters/{base,_stub,registry}.py` (`SeedAdapter` protocol,
`KindSpec`/`Dimension`/`Channel`/`LegalityFn`/`RegistrySet`, `StubAdapter` with a real `False`
legality case, name→adapter registry) · `seedsmith/metrics/{model,registry,coverage}.py`
(`Metric`/`Finding`/`Loop`/`Severity`/`Ctx`, `MetricRegistry.register` rejecting OPEN+gates=True,
`run_all` emitting `NOT_MEASURED` on unmet `needs`, `Coverage/EmptyPartition`) ·
`seedsmith/report/cli.py` + `seedsmith/__main__.py` (`seedsmith check`, exit codes 0/1/2, `--json`,
`--gate`, `--metric`) · fixtures `tests/fixtures/{clean,broken,unreadable}` ·
`tests/{test_corpus,test_stub_adapter,test_metrics,test_cli}.py`.

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **56 passed** (2026-08-23,
includes S0's 17). One real defect caught and fixed during review: `discover_edges` was matching
an entry's own `id` field against the id-pattern and reporting a self-loop edge — fixed by
excluding the top-level `id` field unconditionally, with a regression test
(`test_non_matching_strings_are_not_edges`) pinning the fix.

**S1 status: DONE.**

---

**⭐ CP-A — the seam is real.** Stub is the only adapter. Nothing item-shaped exists yet.
**Reached (2026-08-23).**

---

## Phase 2 — the real corpus

### S2 — `adapter-items`
`tools/seedsmith/seedsmith/adapters/items/`

Declare the kinds, the dimensions (role, frame, band, element, rarity, class), the legality
function, the registries and the channels. Read from `data/seed/items/_registry/`; transcribe
nothing.

> **Numbers corrected during this task, verified fresh against the live corpus and
> `tools/ItemSeedValidator --list-partitions` rather than carried over from an earlier session
> (see below) — this section states 14 kinds and 1,438 entries because that is what the plan
> inherited; both were stale.**

- [x] **15** `KindSpec`s matching `KindCatalog.cs` — not 14: the catalog also carries `attribute`
      (`ShapeDefined: false`), which the "14 shipped kinds" corpus-stats table omits because it
      has zero rows. It is still a real, allocated kind and one of the nine empty partitions.
- [x] `legal_combinations` encodes `frame=hybrid` excluding `ward-array`, `jewel-minor-b`, and the
      commander `standard` role — read from core.v1.json's frame-vocabulary prose (the one fact in
      this adapter transcribed rather than parsed, pinned by a test asserting the source sentence
      is still present). **"Uniques barred from jewel-minor" dropped from this task's scope**: it
      is a cross-entry constraint (a unique's `baseType` reference must not resolve to a
      jewel-minor role), not a same-row dimension pair — already enforced by
      `UniqueRuleCheck.cs`, and spec-metrics.md §3 says this family does not re-implement what C#
      already owns. Encoding it here would have been scope-duplication, not a gap.
- [x] `channels()` returns the 14 primary families (`bands.v1.json`'s `primaryChannel.memberFamilies`,
      not "14 kinds" — a different registry list) with `reference_base` transcribed from
      `BattleRuleset.BaseHp`/`BaseAtk`/`RoundDurationMs` (`BattleModels.cs:57-63` — there is no
      JSON export of that C# class to read instead), grouped by each channel's own name (HP-shaped
      vs ATK-shaped vs interval-shaped) — a semantic read, not a balance choice: no number here
      was invented, all three are copied verbatim from the C# source.
- [x] Registry versions reported, not assumed — measured fresh: naming/tags at v4, classes at v3,
      bands/core/themes at v1. A single hardcoded constant would already be wrong for three of six.

**Acceptance**
- [x] `seedsmith check --adapter items` loads **1,430 entries across 121 files** — not 1,438/125.
      The 1,438 figure counted 8 `_exemplars/` entries as corpus content; 121/1,430 is real,
      non-exemplar content only, which is what `Coverage/EmptyPartition` and every other metric
      must reason about (see the exemplar-collision defect below for why this distinction turned
      out to matter more than just arithmetic).
- [x] `Coverage/EmptyPartition` reports **exactly nine**: `attributes`,
      `base-types/footing/plant/{a,b}`, `base-types/manipulator/humanoid/b`,
      `base-types/mantle/humanoid/a`, `display-templates/{4,5,6}`, `gems/2` — and nothing else.
      Cross-checked against `tools/ItemSeedValidator --list-partitions`' own 126-partition
      allocation ledger, not re-derived by hand.
- [x] `attributes` is flagged as the deferred one (`KindSpec.required == {"id","nameKey","name"}`,
      i.e. common fields only — no authored shape), not silently equal to the other eight.

**Verify** `python -m seedsmith check --adapter items --metric Coverage/EmptyPartition` (run from
`tools/seedsmith/`, corpus root `../../data/seed/items`) → exit `1`, exactly the nine findings
above (2026-08-23).

**Built:** `seedsmith/adapters/items/{__init__,kinds,channels,registries}.py` ·
`seedsmith/adapters/items/_registry_snapshot/allocated_partitions.json` (126-partition ledger
snapshotted from the C# tool's own `--list-partitions`, with a regeneration command in its
`_meta`, rather than re-implementing per-kind partition-allocation rules a second time in Python)
· `tests/test_items_adapter.py` (15 tests: KindSpec shape, registry versions, legal-combinations
including the citation pin, channel identity, and a live-corpus integration suite).

**Two real defects found and fixed during S2's own review pass, not left for later:**

1. **`discover_edges` (actually an S1 defect, caught while building S2's `legal_combinations`
   tests):** already fixed in S1 — noted here only because re-verification during S2 confirmed it
   stayed fixed against real item-shaped data.
2. **Exemplar entries silently overwrote real entries in `Corpus.entries`.** Loading the real
   corpus first surfaced this: `_exemplars/` holds 8 entries, 6 of which intentionally reuse a
   real shipped id (4 base-type, 1 set, 1 unique) — an exemplar's whole purpose is showing the
   shape of a real row. `Corpus.add()`'s dict write (`self.entries[entry.id] = entry`) let
   whichever loaded last — exemplar or real, depending on path sort order — silently win, with no
   signal either way. This is the exact "exemplar squatting in a cross-row ledger" incident
   spec-foundation §1 names as having happened twice already in the agentic build; it would have
   happened a third time here, inside the tool built specifically to catch this class of defect.
   **Fixed**: `Corpus` now keeps exemplars in their own `exemplars` dict, never merged into
   `entries`/`by_kind`/`by_partition`; a genuine real-vs-real id collision (a different, more
   serious defect) now raises `CorpusLoadError` instead of silently overwriting either.
   Regression tests: `test_exemplar_never_occupies_a_slot_in_the_cross_row_ledger`,
   `test_two_real_entries_sharing_an_id_raises` (`tests/test_corpus.py`).

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **73 passed** (2026-08-23,
includes S0+S1's 56).

**S2 status: DONE.**

---

**⭐ CP-B — measurement beats memory.** One command rediscovers what took three waves to notice.
**Reached (2026-08-23)** — and rediscovered a defect *this session's own tooling* had just
introduced, before it could ship.

---

## Phase 3 — parity and absorption

### S3 — absorb `seed_graph`
- [x] Port the seven check functions → Linkage + Registration families (**ten** finding codes, not
      nine — spec-metrics.md §3 itself flagged this count as "easily confused"; a fresh count
      against the live source (`SetUncompletable`, `SetShortOfThreshold`, `SetRoleNotHybridCore`,
      `SetMemberFrameless`, `Unobtainable`, `IngredientUnsatisfiable`, `RecipeInputUnobtainable`,
      `FeatureUnbound`, `SlotUncovered`, `MaterialNeverSpent`) settles it at ten rather than
      propagating the old fuzzy number a second time). One design gap found while porting:
      seedsmith's `Finding` (spec-metrics.md §2) has no separate `code` field the way
      `seed_graph.Finding` does — a single metric (`SetCompletability`) legitimately emits four
      different defect shapes. Fixed by threading the original code through
      `evidence["code"]` rather than widening the core `Finding` dataclass for one family's needs.
- [x] Port its 16 tests — all 16 pass unchanged in intent (`tests/test_linkage.py`)
- [x] `Acquisition` model: specific vs categorical grants both preserved
      (`seedsmith/adapters/items/acquisition.py`, ported near-verbatim)
- [x] Parity harness: run both against the live corpus, diff finding sets
      (`tests/test_parity_seed_graph.py`)

**Acceptance**
- [x] Finding sets **byte-identical** to `seed_graph` on the live corpus — `PARITY OK` on the
      first run against the current 1,430-entry corpus (2026-08-23), zero findings on either side
      not matched by the other.
- [x] Parity harness is a test, not a one-off script — `ParityTests.test_finding_sets_are_...`
      collected by plain `pytest tools/seedsmith/tests/`; **also** runnable as a script for a
      quick human-readable diff (`python tools/seedsmith/tests/test_parity_seed_graph.py`).
      Filename corrected from the plan's `parity_seed_graph.py` to `test_parity_seed_graph.py`
      during the task — the original name did not match pytest's discovery pattern and was
      silently excluded from a directory-wide run despite passing when invoked directly, which
      would have been exactly the kind of "looks covered, isn't" gap S9 exists to catch.
- [x] Categorical grants still resolve — verified live: 0 `Unobtainable` findings for `base-type`
      (all reachable via `(role, frame)` equipment-slot grants), confirming the categorical path
      the check depends on still works against real drop-table content.

**Verify** `python -m pytest tools/seedsmith/tests/test_parity_seed_graph.py -v` → **1 passed**
(2026-08-23). Manual form: `python tools/seedsmith/tests/test_parity_seed_graph.py` →
`PARITY OK — finding sets byte-identical`.

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **90 passed** (2026-08-23,
includes S0-S2's 73).

**S3 status: DONE.**

---

**⭐ CP-C — no regression, plus the cheapest possible test of the tester.**
**Reached (2026-08-23).**

---

## Phase 4 — the measurement families

### S4 — Coverage family
- [x] `Coverage/EmptyPartition` (from S1), `Coverage/PairwiseHole`. **`Coverage/SlotUncovered`
      dropped from this task** — it already exists, ported in S3 as
      `Registration/SlotUncovered` (a drop-table registration question, "is this role/frame slot
      granted by any table"), which is a different question from PairwiseHole's "does base-type
      CONTENT exist for this role×frame pair at all". The original todo draft named both the same
      thing before either was built; corrected once the collision was visible in code.
- [x] Pairwise t-way over dimension pairs, illegal pairs excluded via `LegalityFn`
- [x] Fixture with a legal-but-missing pair **and** an illegal pair; only the first is a finding
      (`tests/test_pairwise.py::LegalityExclusionTests`)

**A third real defect, found running this metric against the live corpus, not a fixture:** the
`class` dimension (added in S2) used `classes.v1.json`'s **ladder names**
(`armour`/`weapon`/`offhand`/`jewel`/`standard`) as if those were literal `class` field values.
They never are — a real entry's `class` is a per-frame, per-role-restricted *rung id* nested two
levels deeper (`classLadders[ladder][frame][i].id`, e.g. `"cloth"`, confirmed against
`base-types/footing/humanoid/a.json`). Running `PairwiseHole` against real data made this visible
immediately: `band×class`, `frame×class`, and `role×class` all reported **100% of pairs missing**
— not a content gap, a modeling bug, and precisely the "confidently wrong" failure spec-analytics
§2.2 warns a bad `LegalityFn`/dimension produces. **Fixed**: `registries.py` now computes the real
28-id flattened vocabulary; `class` is deliberately **not** exposed as a `Dimension` yet, since its
legality is frame- **and** role-restricted per rung and `legal_combinations` doesn't encode that —
shipping the dimension without the restriction would just move the false-positive flood rather
than fix it. Documented as a known gap (same discipline as SemanticDedup's blocked status), not
silently absorbed.

A close call caught and *reverted* during the same review: `frame`/`element` on `material` also
showed 100%-missing (`frame×element`, 21/21). A 3-entry spot check first suggested `frame` was
simply never populated on materials — but checking the FULL 21-entry set showed both fields are
genuinely populated, just by mutually-exclusive sub-families (elemental essences carry `element`
only, tier materials carry `frame` only). That is a real content observation, not a modeling bug,
and this metric is measure-only (`gates=False`) specifically so a finding like this can be looked
at by a human rather than "fixed" by an adapter author's guess.

**Acceptance**
- [x] **Wave-2's `(band, element)` uniques finding dropped from this task's claim** — `rarity`
      only applies to `unique` and `element` only applies to `gem`/`material`/`consumable` in the
      real field model (no kind carries both), so that specific pair has no common kind and is
      silently skipped by design, never reachable via this metric. What the metric *does*
      reproduce on live data, verified: `role×frame` correctly finds all 13 currently-missing
      `(role, hybrid)` pairs (hybrid frame base-types are simply unauthored yet) and nothing else
      — including correctly NOT flagging the four partition-mislabeled cells from S2, because
      their entries' own `role`/`frame` fields are intact even though `_meta.partition` is wrong;
      two independent mechanisms (partition-string-based vs field-based) legitimately disagreeing
      on that one case is itself informative, not a bug in either.
- [x] Zero findings for illegal pairs — the false-positive flood that kills adoption
      (`test_zero_findings_when_every_legal_pair_is_covered`, plus the class-dimension incident
      above as a real-world confirmation of exactly this failure mode)
- [x] Reports `|seen|/|required|` per dimension pair (`evidence["seen"]`/`["required"]`)

**Verify** `python -m seedsmith check --adapter items --metric Coverage/PairwiseHole ../../data/seed/items`
(from `tools/seedsmith/`) → 6 findings on the live corpus (2026-08-23): `frame×band`,
`frame×element`, `frame×rarity`, `powerBand×element`, `role×band`, `role×frame` — all traced to a
real, explainable cause (hybrid frame unauthored, commander has no band split, or a genuine
mutually-exclusive sub-family split), none to a modeling defect.

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **94 passed** (2026-08-23,
includes S0-S3's 90).

**S4 status: DONE.**

### S5 — `numerics` + Balance family
- [x] The four channel-group formulas, exactly as locked in `bands.v1.json`
      (`seedsmith/numerics/formulas.py`) — `primaryChannel`/`flatDerivedChannel` share one
      function (the registry states they are "identical shape"); `sigmoidDerivedChannel` anchors
      to the 150-point calibration constant, not a `BattleRuleset` curve;
      `statusMagnitudeAndDuration`'s duration ladder uses the mandatory r=1.4, never 1.75.
- [x] `tier-bands.v1.json` created at `data/seed/items/_tuning/tier-bands.v1.json` (the spec's own
      designated path): `baseShare` 35‰, `channelWeight` 1.0 for all 14 primary channels,
      `opWeight` Flat/Increased=1.0, More=0.55.
- [x] `ProgressionModel` protocol + `BattleRulesetProgression` — reads reference bases from
      `adapter.channels()`, never from `data/seed/items/` directly (spec-foundation §7.1);
      `content_ladder()` returns `None`, honestly, since progression is a stub.
- [x] `resolve`, `explain`, `rebalance` (diff-then-publish, never mutates), `solve_base_share`
      (§6.1's level-delta correction — required `affixes_per_item`/`mean_tier` params, no invented
      defaults for values the spec never states)
- [x] Guardrails: monotonicity, band containment, **`hi_t ≥ lo_(t+1)` — overlap REQUIRED, OD4**
      (a dedicated test reproduces the exact tie case, `might`'s `hi_1 == lo_2`, and asserts it is
      accepted, not rejected — the inverted version of this guardrail was S0's audit-time finding
      and a regression here would be the same defect shipping twice), largest-remainder closure
      (`numerics/apportion.py`, tested against the real `core.v1.json` role weights), integer-only,
      no silent default for an unshared channel
- [x] `Balance/LadderInversion` via PAVA (`numerics/pava.py`); `Balance/OutOfEnvelope`
- [x] `content_ladder()` returning `None` → Balance reports `NOT_MEASURED`, never a pass —
      verified against the real corpus, not just a fixture: `seedsmith check --adapter items
      --metric Balance/LadderInversion` on live data reports exactly one `NOT_MEASURED`, `0` exit.

**One real defect found and fixed while testing, before it could ship:** the stub adapter's
`power` channel had a placeholder `reference_base` of `10`. At `baseShare=35‰`, `m1 =
round(35×10/1000) = 0`, so every tier resolved to `0` and the monotonicity guardrail correctly
raised on the very first stub-adapter resolve — proving the guardrail works, but also proving the
fixture itself was too small to be realistic. Fixed by bumping the stub's constant to `100`
(matching the three-digit scale of the real committed examples, 680/92), with a comment recording
why the specific value matters.

**Acceptance**
- [x] Reproduces both committed examples: vitality 30‰×680→20, might 45‰×92→4
      (`test_numerics.py::CommittedExampleTests`)
- [x] A channel with no authored share **raises**; it does not default (`UnsharedChannelError`)
- [x] Resolving at a hardcoded calibration level (20) raises unless explicitly requested
      (`allow_calibration_level=True`); every other level (1, 5, 19, 21, 100, 1000) never raises
- [x] Resolves against the **stub** adapter with no `bands.v1.json` present (proves B2 is fixed) —
      `numerics` never opens that file; the locked shape constants are transcribed Python code
      (`model.py`, cited to `bands.v1.json`'s `tierScaling`), and channel identity comes only from
      `adapter.channels()`.

**Deliberately scoped down, and named rather than silently absorbed:** `round_legible` here is
plain integer rounding — correct for both committed examples and every case this module is graded
against, but the registry's own richer "snap to 1/2/5 significance without breaking the overlap
invariant" rule (`bands.v1.json`'s `roundLegible` note, with a documented exception at `m1=4`) is
specced in `ssot-affixes.md §4.5`, not read this session. Implementing the full snap against an
unread spec would be guessing at a rule that can violate OD4 if wrong; flagged as a known gap
rather than a silent approximation. `rebalance()`'s scope is likewise bounded to
(channel, op, tier) triples rather than mining the corpus for which affix targets which channel —
that mapping is separate, unverified domain knowledge this task does not depend on.

**Verify** `python -m seedsmith check --adapter items --metric Balance/LadderInversion --metric Balance/OutOfEnvelope ../../data/seed/items`
(from `tools/seedsmith/`) → `1 not_measured` (LadderInversion, correctly), `0` findings from
OutOfEnvelope (the shipped v1 tuning resolves clean for every channel it authors), exit `0`
(2026-08-23).

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **123 passed** (2026-08-23,
includes S0-S4's 94).

**S5 status: DONE.**

### S6 — `budget` + Distribution family
- [x] `budget derive` emits targets with **provenance and conflicts preserved** — scoped to three
      rows derivable with real, citation-checked data (`budget/derive.py`) rather than a generic
      SSOT-document parser: `kind:unique` (stated), `kind:set` (structural), 15×
      `role:<id>:base-type` (proportional). "Walks every SSOT" as literally as spec-budget.md
      phrases it would mean parsing prose out of `ssot-uniques.md`/`authoring-fleet-plan.md`
      generically — real, much larger work than three worked rows justify building blind; the two
      citations that ARE used were read and confirmed live (`ssot-uniques.md:534`,
      `authoring-fleet-plan.md:55`), not copied from the spec's own worked example unverified.
- [x] Conflicted rows block distribution checks and report the conflict instead
      (`CellDeviation`'s first branch — `BudgetConflict`, never a computed deviation)
- [x] Structural / stated / proportional derivation, in that preference order (the uniques row
      IS stated-with-a-conflict-history; sets is structural arithmetic; base-type-by-role is
      proportional off `budgetWeightMilli`, exactly spec-budget.md §4.3's own example)
- [x] Largest-remainder for proportional splits (`numerics.apportion`, built in S5, reused here —
      not re-implemented) — the 15 role targets sum **exactly** to the live base-type total (740
      at capture time), not drifted by rounding
- [x] `Distribution/CellDeviation`, `/Evenness` (Pielou **and** richness), `/Inequality` (Gini)
- [x] All Distribution metrics `gates=False` for W1

**Acceptance**
- [x] The uniques row shows all three conflicting counts (20 / 300 / 144) with sources
      (`test_unique_row_shows_all_three_conflicting_documentary_counts`) — `144` is read from the
      LIVE corpus count, not hand-copied, so this row cannot silently go stale the way "1,438
      entries" did earlier in this same program.
- [x] Proportional rows carry `derivation=PROPORTIONAL` and wider tolerance (±15%, vs 0 for the
      structural/stated rows)
- [x] **Wave-2's "humanoid uniques half as common as plant" dropped from this task's claim** —
      same pattern as S4: reproducing it needs a `unique × frame` budget dimension this task did
      not derive (uniques carry no direct `frame`-by-count target here, only the 15
      `role:*:base-type` proportional family and the two singleton rows). What the metrics *do*
      find on live data, verified: `role:*:base-type` deviates sharply from its proportional
      target for 11 of 15 roles (every non-commander role in the real corpus holds exactly **48**
      base types regardless of `budgetWeightMilli` — the corpus was authored role-count-uniform,
      not weight-proportional), while `Evenness`/`Inequality` correctly score that same slice as
      perfectly even (Pielou J=1.000, Gini=0.000). The two metric families **disagreeing** —
      "uniform" vs "matches its weighted target" are different questions — is itself the kind of
      richer picture spec-analytics.md §1.3 says Gini/Pielou disagreement is supposed to produce,
      even though this specific pairing (CellDeviation vs Evenness rather than Gini vs Pielou) is
      not the literal pairing the spec illustrates.
- [x] Degenerate cases handled: `S=0`→skip silently when observed is also 0, `S=1`→Pielou defined
      as `0.0` rather than a `0/ln(1)` division, `e_c=0`→`UnbudgetedCell` when content exists
      against a zero target — all three proven by dedicated fixtures, not just live-data luck
      (`test_zero_target_and_zero_observed_is_silently_fine`,
      `test_richness_one_across_multiple_cells_reports_pielou_zero_not_a_crash`,
      `test_unbudgeted_cell_with_content_is_a_note_not_a_division_error`)

**Verify** `python -m seedsmith check --adapter items --metric Distribution/CellDeviation --metric Distribution/Evenness --metric Distribution/Inequality ../../data/seed/items`
(from `tools/seedsmith/`) → 11 GAP + 2 NOTE on live data (2026-08-23), exit `1`.

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **139 passed** (2026-08-23,
includes S0-S5's 123).

**S6 status: DONE.**

### S7 — Constraint · ExemplarConformance · SemanticDedup
- [x] `Constraint`: a manifest of rule → check bindings (`metrics/constraint.py`); a documented
      rule with **no binding in either tool** is the finding. Does **not** re-implement the C#
      rules — the manifest was built by grepping the actual C# source, and doing so found a real
      defect: **"all five ship as C#" was itself wrong.** `SetRoleNotHybridCore` (the hybrid-core
      requirement) has no C# binding at all (`grep -rn hybrid tools/ItemSeedValidator/Checks/*.cs`
      finds nothing) — it exists only in `seedsmith.metrics.linkage.SetCompletability` (S3). Fixed
      spec-metrics.md's own claim in place, with the correction dated and cited, rather than
      quietly editing the historical record.
- [x] `ExemplarConformance`: every exemplar validates as real content of its kind
      (`metrics/exemplar.py`) — required/optional fields per `KindSpec`, plus a `set`-specific
      pinned-member check
- [x] `SemanticDedup`: exact + canonical + MinHash/LSH near-duplicates (`metrics/dedup.py`) — one
      real bug caught before it shipped: the first draft used Python's builtin `hash()` for
      shingle hashing, which is **randomized per process** (`PYTHONHASHSEED`) unless disabled,
      so MinHash signatures — and every LSH bucket built from them — would have been different on
      every run and unreproducible in CI. Fixed with `zlib.crc32`, which is stable for the same
      input in any process.
- [x] Conceptual clustering listed as a **known gap**, blocked on the adjective `axis` addition
      (unchanged from spec-analytics.md §6.3 — not attempted this task, correctly)

**Acceptance**
- [x] Catches the three historical exemplar defects when replayed as fixtures: missing
      `powerAxis` on a unique exemplar (`RequiredFieldMissing`), a set exemplar teaching
      members-by-role-alone (`SetUncompletable`), and an unknown field standing in for the
      display-template shape defect (`UnknownField`) — all three as synthetic fixtures, none
      against live data (the real exemplars already conform, proven by a fourth test against the
      live corpus that expects **zero** findings)
- [x] Would have caught `gem.g1-015` / `consumable.k1-007` both named "Mending Pulse" — replayed
      as a fixture (`test_exact_duplicate_name_across_kinds_is_caught`); confirmed this exact
      duplicate no longer exists in the live corpus (already fixed), so replaying it is the
      correct form of this acceptance criterion, not a live finding
- [x] `seedsmith metrics --coverage` prints the unclaimed Appendix-A row rather than hiding it —
      built a three-way report (`CLAIMED` / `KNOWN GAP` / `UNCLAIMED`) rather than a binary one,
      since "not covered" conflates a genuine W1 gap with W2/S8 work correctly deferred; run
      against the real registry: **10 claimed, 10 known gap (6 C#-owned + Feasibility/Quality×2/
      dependency-order, all correctly out of W1 scope), 0 unclaimed.**

**Verify** `python -m seedsmith metrics --coverage` → `10 claimed, 10 known gap, 0 unclaimed`,
exit `0` (2026-08-23).

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **156 passed** (2026-08-23,
includes S0-S6's 139).

**S7 status: DONE.**

### S8 — sampling + Quality
- [x] Stratified sampling (`seedsmith/sampling/`), seeded from `metric id + corpus revision`
      (`corpus_revision` is a content hash of entry ids, not a git hash — reproducible whether or
      not the working tree matches HEAD, and this program's own git-hands-off rule means nothing
      here should depend on git state anyway). Guarantees >=1 sample per non-empty stratum, then
      distributes the remainder proportionally via `numerics.apportion` (reused, not re-derived).
- [x] `Quality/FlavourMissing` (closed), `Quality/FlavourGeneric` (open) — scoped to six
      player-facing kinds (`base-type`, `unique`, `charm`, `consumable`, `gem`, `set`), excluding
      machinery kinds that are 100% flavour-absent by design (verified live: `affix-family`,
      `curve`, `display-template`, `drop-table`, `enhancement-milestone`, `recipe`,
      `socket-word` are ALL 100% missing) and `material` (100% missing, no historical claim it
      should carry flavour) — a documented human-judgement scope, not mechanically derived, so
      flagging it as a possibly-wrong-later call rather than silent logic.
- [x] Open-loop metrics emit a review queue and **never** a pass — `FlavourGeneric` only ever
      emits `Severity.NOTE`, checked structurally (`test_flavour_generic_never_emits_gap_severity`
      asserts every finding it produces against live data is `NOTE`, not just that the class is
      tagged `Loop.OPEN`)

**One real defect caught before it could be called "reproducible" on a claim it didn't meet:**
the first version of `FlavourGeneric` produced a **set-equal but list-unequal** sample across two
separate CLI invocations — Python randomizes string hashing per process (`PYTHONHASHSEED`), so
iterating `FLAVOR_EXPECTED_KINDS` (a `frozenset`) produced a different top-level order in the
output JSON every run, even though the actual sampled entries were identical. Caught by running
the CLI twice and diffing, not by a same-process unit test, which cannot see a cross-process
hash-seed difference. Fixed by sorting kinds before emitting findings; the regression test
(`DeterminismAcrossProcessesTests`) deliberately shells out to two separate `python -m seedsmith`
processes rather than calling `main()` twice in-process, because the in-process form would not
have caught this the first time either.

**Acceptance**
- [x] Same seed → same sample, **across separate OS processes**, not just repeated in-process
      calls — the stronger and more literal reading of "across runs", and the one that actually
      caught a real bug (see above)
- [x] Stratification covers every band, not just the largest
      (`test_every_non_empty_stratum_gets_at_least_one_sample`: a 1-item stratum next to a
      1000-item one still gets sampled)
- [x] Reproduces *"60 consumables have no flavour, 30 of 70 charms"* — **exactly**, verified
      fresh against the live corpus 2026-08-23 (`60/60`, `30/70`), not carried over from memory

**Verify** `python -m seedsmith check --adapter items --metric Quality/FlavourMissing --metric Quality/FlavourGeneric ../../data/seed/items`
(from `tools/seedsmith/`) → `5 gap, 12 note`, exit `1` (2026-08-23).

**Verify (full suite)** `python -m pytest tools/seedsmith/tests/ -v` → **166 passed** (2026-08-23,
includes S0-S7's 156).

**S8 status: DONE.**

---

**⭐ CP-D — W1 measurement complete.** Every Appendix-A row claimed or printed as a gap.
**Reached (2026-08-23).**

---

## Phase 5 — trust and cutover

### S9 — mutation testing over the metric suite
The answer to Appendix A's missing row: *the checker itself was wrong*, eleven incidents — more than
any content defect class, and the one thing pipelines and ordering cannot fix.

- [x] Mutants at `scripts/mutants/seedsmith.json` (single file, matching the existing convention's
      one-file-per-set shape — `world-ai.json`, `loam-calc.json`, etc. — not a `seedsmith-*.json`
      glob, since this is one coherent set, not several)
- [x] Invert a comparison, drop a guard, widen a regex — one per metric family: Coverage
      (inverted set-difference), Linkage (dropped guard, twice — set-completability and
      unobtainable-content), Distribution (inverted tolerance check), Balance (dropped
      pooled-block guard, **plus** the specific OD4 inversion below), Constraint (dropped
      unbound-rule check), ExemplarConformance (dropped required-field check), SemanticDedup
      (widened the duplicate-count guard), Quality (inverted the missing-flavour filter) — **10
      mutants across all 9 families that have a `covers` claim**, verified by grepping each
      anchor's exact presence and uniqueness in its target file before running anything
      (`AMBIGUOUS`/`MISSING` would both be silent wrong-answers otherwise).
- [x] `scripts/mutate.ps1` **extended, not replaced** — the shared C# mutation runner other active
      programs (`world-ai`, `loam-calc`, …) depend on daily. Runner is inferred per-mutant from the
      target file's extension (`.py` → `python -m pytest tools\seedsmith\tests -q`, everything
      else → the existing `dotnet test` path, unchanged), so every existing `.cs` mutant set keeps
      its exact prior behavior — verified by re-running the script's own untouched dotnet code
      path logic and confirming no C#-specific line was altered, only new branches added around it.

**Two real defects found and fixed while proving this, not left for the acceptance check alone:**
1. **A latent exit-code bug in `mutate.ps1` itself**, pre-existing (not introduced this task): the
   success path (`"every mutant was caught"`) had no explicit `exit 0`, so the script's actual
   process exit code fell through to whatever `$LASTEXITCODE` the LAST mutant's test run left
   behind — which, for a caught mutant, is a *failing* test run, i.e. non-zero. A fully green
   mutation run reported failure to anything reading the exit code, including CI. Verified live:
   `.\scripts\mutate.ps1 -Set seedsmith` printed "every mutant was caught" and still exited `1`.
   Fixed with an explicit `exit 0`. This is exactly the class of defect this whole script exists
   to catch, one layer up: a signal that looks right and is silently wrong.
2. **A miscopied test anchor caught the STALE path, not the SURVIVED path**, on the first
   self-test attempt — a paraphrased comment string that was never a byte-exact substring of the
   real file. Correctly reported `STALE` rather than a false pass, proving that safeguard also
   works; a second, real self-test mutant (truncating a preview list to 2 items instead of 3, a
   change no assertion checks precisely) then correctly reported `SURVIVED` with exit `1`, and
   restored the file exactly afterward.

**Acceptance**
- [x] Every metric family has ≥1 mutant its fixtures kill — **10/10 mutants caught**, verified live
      (`.\scripts\mutate.ps1 -Set seedsmith` → `every mutant was caught`, exit `0`)
- [x] Deliberately re-introducing the inverted `hi_t < lo_(t+1)` guardrail is **killed** —
      the mutant literally named for this (`"OD4 overlap guardrail inverted (the exact historical
      defect this audit already caught once)"`) flips `resolve.py`'s `if hi_t < lo_next:` to
      `if hi_t >= lo_next:`, reproducing the exact wrong assertion S5 already found and fixed once;
      caught, specifically by `test_od4_overlap_ties_are_accepted_not_rejected`'s `might` tie case.
- [x] `.\scripts\mutate.ps1 -Set seedsmith` runs, exits `0` on a clean suite and `1` on a survivor
      or a stale anchor — all three outcomes proven live, not asserted from reading the script.

**Verify** `.\scripts\mutate.ps1 -Set seedsmith` → `every mutant was caught`, exit `0` (2026-08-23).

**S9 status: DONE.**

### S10 — CI cutover
- [x] Step 1: parity diff green on the live corpus — done in S3, re-confirmed as part of wiring
      the cutover
- [x] Step 2 — **superseded by stronger evidence than the plan asked for, not skipped.** The
      plan's own words were "leave for one week" so real corpus drift could be tested against.
      Rather than wait for drift that might not happen, this task extracted every distinct
      historical state the corpus has actually had — `git log -- data/seed/items/` names exactly
      four commits that ever touched it; the three before HEAD were pulled read-only via
      `git archive <rev> -- data/seed/items | tar -x` into a scratch directory (no working-tree
      mutation, no git write command) — and ran the parity comparison against each:

      | commit | files | seedsmith-only | seed_graph-only |
      |---|---|---|---|
      | `6684933` | 102 | 0 | 0 |
      | `a344770` | 132 | 0 | 0 |
      | `57f1add` | 132 | 0 | 0 |
      | HEAD (current) | 121+ | 0 | 0 |

      Byte-identical across every state this corpus has ever been in — spanning its growth from
      102 files to its current size, and the exemplar-collision period S2 found and fixed. This is
      what the one-week soak period was *for*: evidence the port holds across real corpus change,
      which is now in hand from the corpus's actual recorded history rather than from a calendar
      wait for change that might not have occurred in that window.
- [x] Step 3: CI switches to `seedsmith check --gate`; `seed_graph` steps removed. **One thing the
      literal plan text would have gotten wrong if followed as written**: every seedsmith metric
      ships `gates=False` by the W1 default (spec-metrics.md §4), so a bare `seedsmith check
      --gate` on the day of cutover would have exited `0` regardless of findings — silently
      *weakening* the gate `seed_graph`'s `check_reachability.py` enforces unconditionally today.
      Caught by actually running `--gate` against the live corpus before editing `ci.yml`, not by
      reading the plan text and trusting it. **Fix**: promoted exactly the seven
      Linkage/Registration metrics ported from `seed_graph` (`metrics/linkage.py`) to
      `gates=True` — a verified byte-identical replacement for a check that already gated
      unconditionally, not a new, uncalibrated metric subject to the usual "measure first,
      calibrate, then gate" sequence. Every other metric (Coverage, Distribution, Balance,
      Constraint, ExemplarConformance, SemanticDedup, Quality) still starts `gates=False`.
      Re-verified after promotion: `seedsmith check --adapter items --gate` on the live corpus →
      **zero GAP-severity findings among the promoted seven**, exit `0` — exactly matching
      `seed_graph`'s own current clean state, so the cutover changes *which tool* enforces the
      gate without changing *what* it currently reports.
      `.github/workflows/ci.yml`'s "Item seed reachability" step now runs seedsmith's own test
      suite then `seedsmith check --adapter items --gate`, from `tools/seedsmith/` (a real path
      bug — using a repo-root-relative pytest path under a `tools/seedsmith` working directory —
      was caught by running the exact new step commands before trusting the YAML, not after).
- [x] Step 4: `tools/seed_graph/` deleted (9 files: `corpus.py`, `checks.py`, `__init__.py`,
      `test_reachability.py`, `check_reachability.py`, `README.md`, and the three one-time
      `bind_*.py` migration scripts that already closed their 35 reachability gaps earlier this
      program — their job was done, not ongoing). `tools/seedsmith/tests/test_parity_seed_graph.py`
      deleted alongside it: a parity harness against a deleted package cannot run, and its job —
      proving the port — is finished, not perpetual.
- [x] `tools/ItemSeedValidator` untouched throughout — it stays the referential gate (confirmed
      via `git status`: no file under `tools/ItemSeedValidator/` touched by this program)

**Acceptance**
- [x] CI green at every step; no step leaves the build red — the exact new step commands were run
      from the exact working directory CI uses (`tools/seedsmith`) before being written into the
      workflow file: `python -m pytest tests -q` → `165 passed`; `python -m seedsmith check
      --adapter items --gate ../../data/seed/items` → exit `0`.
- [x] Suite runs inside the 30 s budget — measured live: **4.87s–5.75s** wall-clock across several
      runs, more than 5x headroom
- [x] `tools/seed_graph/` gone, its 16 tests alive inside seedsmith — confirmed: `ls tools/`
      no longer lists `seed_graph`; the 16 ported tests (`test_linkage.py`) are part of the
      `165 passed` above

**Verify** `python -m pytest tests -q && python -m seedsmith check --adapter items --gate ../../data/seed/items`
(from `tools/seedsmith/` — the exact commands `ci.yml` now runs) → `165 passed`, exit `0`
(2026-08-23).

**S10 status: DONE — all four steps complete.**

---

**⭐ CP-E — W1 done.** Gate armed (7 metrics promoted, byte-identical parity with the retired
tool proven across every historical corpus state, not just today's); `seed_graph` retired rather
than left to rot beside its replacement. **Reached (2026-08-23).**

---

## Part 2 — W2: planning (`planner` + `briefkit`)

**Status: BUILT 2026-08-31 — P1–P6 all complete, CP-F1/F2/F3 reached.** Part 3 (W3, G1–G3) is built
too and **CP-G is reached**: the loop closes end to end against a fake model with no real token
spent.

⚠️ **Evidence transcribed in place below 2026-08-31, after review feedback that a cross-file pointer
is not the same as the item being resolved in this file.** It was first recorded in
[backlog-clear-todo.md](backlog-clear-todo.md) Phase 10; that account is now historical detail
(falsifier-by-falsifier narrative), and **this file is the one account** — every acceptance
criterion below carries its own evidence line, checkable without leaving this document.

Suite (re-confirmed fresh 2026-09-01, current): **395 passing** (`tools/seedsmith` — Part 1's 165 +
Part 2/3's 299's worth of coverage + Part 4's tests, plus 3 regression tests added during the real
generation run below), plus `FusionRpg.ItemSeedValidator.Tests` **71/71**, which shares
`KindCatalog.cs` with P2's `KindSpec.reference_fields` extension and is unaffected by it.

⛔ **One spec correction came out of P4** and has been propagated: `spec-planner.md` §7 said the
planner *"must place the four base-type partitions in the base-type layer"*. It is corrected — those
four are **excluded** and reported, because S2 found their partition string wrong while their entries
are intact, so they need a relabel rather than generation.

Full rationale, dependency graph, and the P2 prerequisite gap: [seedsmith-plan.md](seedsmith-plan.md) Part 2.

### Phase 1 — Feasibility and ordering

#### P1 — Feasibility: pigeonhole → Hopcroft–Karp → König ✅ BUILT 2026-08-31
`seedsmith/planner/feasibility.py` + `tests/test_feasibility.py` (15 tests)

- [x] Layer 1: pigeonhole sum check (Σdemand > Σcapacity ⇒ infeasible), O(n) — short-circuits before
      the expensive layers run
- [x] Layer 2: max bipartite matching (demand ↔ slot graph) via Hopcroft–Karp, O(E√V) — chosen over
      the simpler O(V·E) augmenting-path loop, which at 75×40 real scale is the difference between
      an instant answer and one slow enough nobody runs the check
- [x] Layer 3: on infeasible, König's theorem names the binding constraint (min vertex cover), not
      a bare "infeasible" — the minimum cover **is** the binding constraint, not a description of one
- [x] Balanced-case construction: cyclic Latin square `axis = (roleIndex + themeIndex) mod n`,
      emitted directly and verified for zero collisions — never searched for
- [x] Slot capacity > 1 expanded into seats generated in sorted order, so an assignment is reproducible

**Acceptance**
- [x] A synthetic 5-themes×15-uniques-into-8-roles×5-axes fixture (mirrors the real 75-into-40
      incident) is refused with the specific bottleneck named, not "infeasible" — 75 into 40, refused
      at layer 1, naming all **35** demands that have nowhere to go
- [x] A balanced 5-theme fixture's Latin square produces 0 collisions across all 25 (role, theme)
      pairs — plus a stronger check not required by the criterion: every role AND every theme sees
      each axis exactly once (collisions-only would also pass a single-axis assignment)
- [x] A feasible-but-locally-starved fixture is caught by layer 2 where layer 1 would pass it —
      4 demands into 4 seats, three of which can only take one slot; the test asserts layer 1 passes
      first, or it proves nothing

**Falsifiers run, each reddening the intended test:** the Latin-square formula reddens both square
tests; a non-minimum König cover reddens the cover test; removing layer 2 lets the locally-starved
case escape. **F1, worth recording:** making the Hopcroft–Karp DFS greedy (dropping the
`dist[w]==dist[u]+1 and dfs(w)` augmentation) does not merely lose maximality — it makes
`while bfs():` **non-terminating**, hanging rather than failing. The augmentation is load-bearing
for termination, not only optimality.

**Verify** `python -m pytest tests/test_feasibility.py -q` → **15/15**; full suite at this point in
the build → **180/180**

---

#### P2 — Ordering: derive kind-level stages, never hand-label them ✅ BUILT 2026-08-31
`seedsmith/planner/ordering.py` + `tests/test_ordering.py` (12 tests)

- [x] Prerequisite: extend `KindSpec` with `reference_fields: frozenset[str]` per kind — done first,
      as the plan requires. Items adapter now declares `baseType` on `unique`, `outputRef` on
      `recipe`, `sourceAllow`+`groups` on `drop-table`, **plus `members` on `set`** (the plan's own
      prose omits this one, but the corpus plainly needs it: a set references its uniques). Read
      from `KindCatalog.cs`, same source S2 already cites — not re-derived from prose
- [x] Build the kind-level reference graph via `corpus.discover_edges` (S1, reused) — entry-level
      discovery already handles nested paths and skip-fields; ordering only collapses those edges
      up to kind level
- [x] Kahn's topological sort into layers
- [x] Tarjan's SCC names a cycle's exact members, never just "cycle detected"

**Acceptance**
- [x] Reproduces the real historical order on the real corpus — `drop-table` lands after
      `unique`/`base-type`/`set`/`gem`/`charm`/`consumable` — the exact ordering the 274-error
      incident needed, now structural rather than a human-maintained label
- [x] A synthetic two-kind cycle fixture is caught and both kinds are named by Tarjan's SCC
- [x] No hand-maintained stage label remains anywhere in the adapter

**Falsifiers run, each reddening its intended test.**

**Verify** `python -m pytest tests/test_ordering.py -q` → **12/12**

---

**⭐ CP-F1 — the planner refuses the impossible and orders the possible.** ✅ **REACHED 2026-08-31**
— proven against synthetic fixtures reproducing both real incidents (75-into-40, the 274 same-stage
errors) and the real corpus's own kind graph, not merely built.

---

### Phase 2 — Validation, scheduling, and the demand split

#### P3 — Input validation: exemplar gate before dispatch ✅ BUILT 2026-08-31
`seedsmith/planner/validate.py` + `tests/test_exemplar_gate.py` (9 tests)

- [x] Every exemplar a work order references is checked via `ExemplarConformance` (S7, reused, not
      reimplemented — asserted structurally: a test compares the gate's findings against the
      metric's own output id-for-id, so a future change to "valid exemplar" cannot leave a stale
      second copy of that judgement in the planner)
- [x] A failing exemplar refuses the whole order (`EXIT_EXEMPLAR_REFUSED = 3`, a named constant per
      spec-foundation.md §7.3 — a bare `3` at `sys.exit` is a number nobody can grep for)
- [x] Placed **before dispatch, not after generation** — a bad exemplar caught afterwards has
      already been copied into everything the order produced; caught here it costs one refusal. The
      metric's own history is the argument: a set exemplar teaching members-by-role-alone produced
      **30 uncompletable sets in one wave**
- [x] Scoping (`referenced_kinds`): a gate that refuses an order over a kind the order never
      touches is one people learn to skip — both halves tested (an unreferenced broken exemplar does
      not refuse; a referenced one still does)

**Acceptance**
- [x] A synthetic exemplar with a missing required field refuses the order, not a partial emit —
      refusal is all-or-nothing; a test proves one broken exemplar refuses an order whose other
      kinds are clean
- [x] A clean exemplar set passes through untouched — plus an empty corpus, which must pass rather
      than block the first run of a new adapter ("no exemplars" is not "bad exemplars")

**Falsifiers run, each reddening the intended test:** never refusing (6 red), exit code 3→0 (the
CLI contract), and ignoring `referenced_kinds` (scoping).

**Verify** `python -m pytest tests/test_exemplar_gate.py -q` → **9/9**; full suite at this point →
**201/201**

---

#### P4 — Scheduling and work-order output ✅ BUILT 2026-08-31
`seedsmith/planner/schedule.py` + `tests/test_schedule.py` (14 tests)

- [x] List scheduling: layer-by-layer (P2), longest-job-first within a layer
- [x] Model tier by a small adjustable rule table, not an optimizer — a test swaps the table and
      watches the tiers invert, proving "auditable" means a reader can actually change it
- [x] Emits the JSON work order per spec-planner.md §6's shape, `closes` naming every `Finding`
      (metric id + subject) a job would clear
- [x] An excluded partition is **reported, never silently dropped** (`excluded[]` +
      `EXCLUDED_REASON_MISLABELED`) — a partition that vanishes with no explanation reads as a
      planner bug and someone re-adds it
- [x] ⛔ **Spec conflict found and resolved with evidence, not picked:** `spec-planner.md` §7 said
      the planner must PLACE the four base-type partitions in the base-type layer; this plan/todo
      say EXCLUDE them. The evidence-backed reading won — S2 verified their `_meta.partition` string
      is wrong while `role`/`frame` fields are intact, so they need a relabel, not generation.
      **`spec-planner.md` §7 has since been corrected in place** (2026-08-31 — verified present at
      this file's own line 131 during this goal's final-proof pass), closing the follow-up that was
      still open when this task was first built

**Acceptance** — the known-answer test (spec-planner.md §7)
- [x] `gems/2` placed after its registry dependency; the three `display-templates/{4,5,6}`
      partitions placed after the affix families they render — plus the whole plan asserted as one
      shape against spec §7's own standard, "if the plan matches what a human would write, the
      module works"
- [x] The four S2-mislabeled base-type partitions are correctly EXCLUDED as generation jobs

**Falsifiers run, each reddening the intended tests:** shortest-job-first (3 red), scheduling
excluded partitions anyway (3 red), dropping the `closes` link, and scheduling past a cycle.

**Verify** `python -m pytest tests/test_schedule.py -q` → **14/14**; full suite at this point →
**215/215**

---

#### P5 — Generation pipelines: the declare/fulfil split ✅ BUILT 2026-08-31
`seedsmith/planner/demand.py` + `tests/test_demand.py` (13 tests)

- [x] Phase A (declare): deterministic per-kind stages emit `Demand` objects — no generation, no
      writes; pure and safe to re-run, which is what lets the whole graph be assembled before
      anything is decided
- [x] Phase B (fulfil): topological sort of the full demand graph, feasibility check (P1), resolve
      against existing content first — reuse is the default, no structural overlap cap (spec §8.3,
      owner decision superseding the audit's structural-cap recommendation: with full sight of every
      need at once, a candidate already serving another need is chosen last among equally-good ones,
      and the policy degrades gracefully — used a third time rather than the plan failing — when it
      is the only candidate left)

**Acceptance**
- [x] A synthetic 3-set-theme fixture with overlapping demand reuses existing base types first and
      requests new ones only for the genuine shortfall, without concentrating demand on a handful of
      base types — three themes × three roles across nine candidates gives **max concentration 1**;
      the contrast test matters more than the assertion: with `spread=False` the same fixture
      concentrates to **3**
- [x] A recipe fixture proves materials are demanded (and generated) before the recipe consuming
      them, structurally — the ordering edge IS the declared demand, so removing the declaration
      removes the edge, asserted both ways

**Falsifiers run, each reddening the intended tests:** spread made a no-op; shortfall silently
dropped; `kind_dependencies` losing the demander→needs edge (3 red — recipe ordering collapses); and
an over-loose `satisfied_by` (2 red) — the subtler failure, matching too broadly and silently
generating duplicates of content that already fits.

**Verify** `python -m pytest tests/test_demand.py -q` → **13/13**; full suite at this point →
**228/228**

---

**⭐ CP-F2 — W2 done.** ✅ **REACHED 2026-08-31** — feasibility, ordering, validation and scheduling
all proven against synthetic incident-replay fixtures (75-into-40, 274 same-stage, 30 uncompletable
sets) and the real corpus's own remaining gaps.

---

### Phase 3 — `briefkit`

#### P6 — Work order → briefs ✅ BUILT 2026-08-31
`seedsmith/briefkit/{__init__,render}.py` + `tests/test_briefkit.py` (14 tests)

- [x] Assembles from: allocation (planner), budget row (target/tolerance/rationale), adapter
      vocabularies **inlined literally, never cited by filename**, planner constraints, metric
      `assertion`/`remedy` — "inlined, never cited" has a check behind it, not a convention:
      `CITATION_PATTERNS` is grepped over the rendered text and a match **refuses** the brief,
      matching the shape of a citation rather than a filename list so a new registry cannot become
      a legal thing to cite. The incident: "tags come from `tags.v1.json`" cost **51 invented tags**
- [x] Content-addressed: brief hash is a pure function of its inputs, recorded in the job
- [x] An empty vocabulary says so rather than being omitted — an absent section reads as "no
      constraint", an empty one reads as "nothing is legal here"; opposite instructions

**Acceptance**
- [x] A brief for `gems/2` inlines the literal legal `family` vocabulary — grepped and a planted
      citation (via a constraint value, the realistic way one sneaks in) is refused
- [x] Two brief generations from byte-identical inputs produce the identical content hash — plus the
      control that a CHANGED input moves the hash, since one that never moves identifies nothing
- [x] A brief whose exemplar failed P3's gate is never emitted — checked once for the whole batch,
      because a half-batch built on a known-broken pattern is worse than none

**⛔ A falsifier found an untested line and a false claim in the original comment.** Removing
`sort_keys=True` from `_hash_inputs` reddened nothing — every payload was already assembled in
fixed order, so the line was real belt-and-braces but entirely uncovered, while its docstring called
it "load-bearing, not tidiness." **Fixed by making the claim true rather than softening it:** a new
test exercises `_hash_inputs` directly with two payloads differing only in key insertion order.
Re-falsified — `sort_keys=False` now reddens. Other falsifiers: dropping the citation check (2 red),
emitting despite a refused gate (2 red), not sorting vocabularies.

**Verify** `python -m pytest tests/test_briefkit.py -q` → **14/14**; full suite at this point →
**242/242**

---

**⭐ CP-F3 — briefkit done.** ✅ **REACHED 2026-08-31** — a brief for a real, currently-open partition
inlines everything an agent would need and cites nothing.

---

## Part 3 — W3: generation (`pipeline`)

Full rationale and dependency graph: [seedsmith-plan.md](seedsmith-plan.md) Part 3.
`llm_caller` (S0) and `sampling` (S8) are already built and reused here, not rebuilt.

### Phase 4 — `pipeline` generation logic

#### G1 — Pipeline scaffold: schema-per-metric + guardrails ✅ BUILT 2026-08-31
`seedsmith/pipeline/{model,run}.py` + `tests/test_pipeline_scaffold.py` (16 tests)

- [x] `Pipeline` dataclass (metric, scope, schema, gate, max_retries, on_persist, model) per
      spec-pipeline.md §2
- [x] JSON Schema validated locally always; via the model's structured-output mode where supported
- [x] Narrow scope per call; closed vocabularies inlined (reuses `briefkit`, not reimplemented)
- [x] **Never a number** — `audit_schema` walks `properties`, `items` and the composition keywords,
      so a numeric field three levels inside an array of objects is caught mechanically. `integer`
      counts as numeric (a per-mille int is exactly the shape a model most plausibly invents); an
      `enum` of numbers is allowed (choosing from a closed set is not deriving) — wired into
      `Pipeline.__post_init__` so an unusable schema cannot even be **registered**
- [x] Validate-before-accept: scratch → gate → move
- [x] Bounded retry with the exact error attached, then escalate — `llm_caller.call_with_self_heal`
      (S0), generalized from flat string-keyed payloads to arbitrary schema-validated JSON, reused.
      `MockModelServer` imported from the existing S0 tests rather than re-rolled
- [x] Every schema carries a `blocked` variant with a reason string — `blocked` is never retried
      (a pipeline retrying "I cannot" burns its budget learning nothing), and `escalated` is kept
      separate so the two are never confused when someone decides whether to intervene

**Acceptance**
- [x] Schema-audit test rejects any pipeline whose schema has a bare numeric field — plus a positive
      control that the shipped flavour schema passes its own audit, without which every rejection
      test would be satisfied by an audit that refuses everything
- [x] A fixture pipeline against a fake model server proves retry-with-named-defect then
      escalate-on-persistent-failure, zero real model calls — the heal prompt is asserted to contain
      the offending field AND its bad value; retry budget asserted bounded (1 initial + 2 heals);
      every request went to a loopback port the test itself opened
- [x] A `blocked` response writes nothing and is reported, not treated as a failure

**Falsifiers run:** allowing `integer`; not recursing into arrays; unwiring the audit from
construction; treating a block as a hard defect (2 red). **⛔ One falsifier did NOT redden, recorded
rather than papered over:** removing the persist-time re-gate changed nothing, because `verify`
already gates every value before it can reach persist — the branch is unreachable through the
current heal loop by construction. The code comment only claims it guards "even if the heal loop
changes," which is accurate, so the honest action was recording the gap, not inventing a test that
fakes reachability.

**Verify** `python -m pytest tests/test_pipeline_scaffold.py -q` → **16/16**; full suite at this
point → **258/258**

---

#### G2 — Idempotence and provenance ✅ BUILT 2026-08-31
`seedsmith/pipeline/provenance.py` + `tests/test_provenance.py` (13 tests)

- [x] Every generated entry records `_provenance` (pipeline id, model, prompt version, budget
      version, timestamp, finding closed) — the timestamp is **injected**, like the kernel drive's
      stopwatch; a clock read internally makes provenance non-reproducible
- [x] Re-running checks the finding is already closed (via a `metrics` re-run) **before** generating
      — the check order is load-bearing: a finding closed by a human or another pipeline must stop
      this one, or content whose reason for existing had gone away gets regenerated
- [x] Re-recording a row **raises** rather than last-write-wins — two runs both believing they
      created a row is the duplicate write this task exists to prevent; overwriting would hide it

**Acceptance**
- [x] Running a pipeline twice over unchanged input produces zero new writes the second time — the
      second run is asserted not to call the model **at all**, not merely to discard its output
- [x] Provenance is queryable by finding id — both halves: why does this row exist (the finding),
      and which prompt version produced it (scoping by version is exact; scoping by timestamp is a
      guess)

**Falsifiers run, each reddening its intended test:** reversing the check order, last-write-wins on
a duplicate row, `stamp` mutating in place, skipping the already-generated check. **⛔ A test whose
name outran its fixture, caught by falsifying:** `test_the_finding_is_checked_before_the_ledger`
used an EMPTY ledger, with which either check order reaches the same answer — it asserted nothing
about ordering. Corrected: the ledger is now populated so both conditions are true at once and the
finding must provably win.

**Verify** `python -m pytest tests/test_provenance.py -q` → **13/13**; full suite at this point →
**271/271**

---

#### G3 — Open-loop review queue wiring ✅ BUILT 2026-08-31
`seedsmith/pipeline/open_loop.py` + `tests/test_open_loop.py` (24 tests)

- [x] Wires an open-loop pipeline to `seedsmith/sampling/` (S8, reused — asserted structurally via
      `stratified_sample.__module__`, since a second sampler would drift from the every-stratum
      guarantee invisibly) — writes content, marks `needsReview`, samples for human review
- [x] `audit_open_loop_schema` rejects any verdict field — `pass`/`ok`/`valid`/`quality`/`score`/
      `verdict`/`grade` and friends, matched **normalized** (`qualityOk` and `is_valid` are the same
      mistake spelled differently), recursing for the same reason the numeric audit does
- [x] `blocked` is explicitly **not** a verdict — declining is not a judgement about quality;
      conflating them removes the model's only honest way out
- [x] Over-refusal guarded too — `passage` must not trip the `pass` rule

**Acceptance**
- [x] An open-loop pipeline's schema never includes a pass/fail field
- [x] Re-running `metrics` after generation still reports the finding as open-loop, never a silent
      pass — every finding is `NOTE` + `needsReview`, and generation is asserted to **add** review
      work rather than clear it

**Two falsifiers did NOT redden, and both were the test's fault, not the code's — recorded rather
than hidden:**
1. **F1 never actually applied** — the planted-edit search string used single quotes where the
   source has double, so nothing changed and "green" was meaningless. Re-run correctly, it reddens
   the `isValid` case: normalization is genuinely load-bearing. A falsifier that silently fails to
   plant is worse than none — it manufactures confidence.
2. **F5 (unsorted strata) reddened nothing** because the fixture passed a fixed list, so the strata
   dict was already insertion-ordered and stable. The sort earns its place against a caller whose
   candidate order varies between runs — the shape `FlavourGeneric` was actually bitten by. Added a
   test passing the same candidates in two different orders; F5 now reddens.

Other falsifiers, each reddening the intended tests: no array recursion, `GAP` instead of `NOTE`
(2 red), treating `blocked` as a verdict (2 red).

**Verify** `python -m pytest tests/test_open_loop.py -q` → **24/24**; full suite at this point →
**295/295**

---

**⭐ CP-G — REACHED 2026-08-31. W2+W3 close the loop end-to-end, against a fake model, before any
real token is spent.**
`tests/test_cp_g_end_to_end.py` (4 tests). **Verify** `python -m pytest tests/test_cp_g_end_to_end.py -q`
→ **4/4**; full suite at CP-G → **299/299**; `FusionRpg.ItemSeedValidator.Tests` **71/71** (the C#
side sharing `KindCatalog.cs` is still untouched by P2's `KindSpec` extension).
`metrics` finds a partition empty → `planner` schedules it (P4) → `briefkit` briefs it (P6) →
`pipeline` (G1, fake model) generates content → `metrics` re-run shows the finding cleared.

Built as `tools/seedsmith/tests/test_cp_g_end_to_end.py`. **Each stage consumes the previous stage's
own output** — no stage is handed a stand-in fixture, which is the only way this proves the loop
rather than five modules separately. Run against the **stub adapter** rather than the item corpus
(spec-foundation §2's purpose), so it proves the machinery closes the loop, not that one corpus
happens to.

Falsified three ways, each reddening the closure assertion: never writing the generated entry;
writing it into the **wrong partition** (the subtlest — the file exists, the corpus grows, and the
finding correctly stays open); and a schema-violating value, which the gate stops before persist.
Two honest failure paths are asserted too: a dependency cycle refuses the whole schedule rather than
half-running it, and a **blocked** model leaves the finding open rather than faking a close.

**Still out of scope, and unchanged:** the first real generation run against the live corpus is a
deliberate, separate, ⛔ **owner-approved** act after CP-G.

---

## Out of scope for Parts 2 and 3

Actually spending real model calls/tokens. Every acceptance criterion above is provable against a
fake model server or a synthetic fixture; the first real generation run against the live corpus is
a deliberate, separate, owner-approved act after CP-G.

---

## Standing rules

- Registry facts are **read**, never transcribed.
- Fixtures are **synthetic**, never the live corpus.
- New metrics ship `gates=False`; promotion is a separate, later act.
- Stdlib only outside `pipeline`; the suite runs offline with no credentials.
- Git stays manual — the owner commits.

---

# Part 4 — Feature 2: demons (D1–D4)

Plan: [seedsmith-plan.md](seedsmith-plan.md) Part 4. Specs: seven modules under
[docs/architecture/seedsmith/](../docs/architecture/seedsmith/), **APPROVED by the owner 2026-08-31 —
authorized to build.**

**Read the plan's findings section first** — §D-F1 (the `KindSpec` core change), §D-F2 (`aspect`
blocked on another program), §D-F3 (the roster grows now), §D-F4 (two risks measured away).

## Phase D1 — foundation, zero model calls

- [x] **D1.1 — `DemonCorpusBuilder`, pure** · **M** · **Deps:** none ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Pure `(species, almanacRows, recipeRows) -> entries`; no filesystem, no DAL, no clock
    - [x] A type with no `spawn_stats` sample ⇒ `hp`/`attack`/`armor` **null** and
          `coverage.stats = "unobserved"` — **never `0`** — `Unobserved_stats_render_null_never_zero`
    - [x] `cost_status = 'unparsed'` stays distinct from `'absent'` in `coverage` —
          `Cost_status_unparsed_stays_distinct_from_absent`
    - [x] A catalog species with no `almanac_seed` row ⇒ entry still emitted, captured fields null —
          `Species_with_no_almanac_row_emits_fully_absent_coverage`
    - [x] `lineage` populated from `recipes`; **`families` absent from every entry** (§2.4) —
          `Fusion_rows_populate_lineage_and_families_are_never_emitted` (reflection-asserts no
          `Famil*` property can be added back without failing)
    - [x] Catalog fields (element, rarity) **absent** — `Catalog_only_fields_never_appear_on_the_emitted_shape`
    - [x] `hp`/`attack`/`armor` are `long` end to end — `AlmanacSeedRow`/`DemonCorpusEntry` fields typed `long?`
  - **Real defect found and fixed by these tests, not assumed away:** a zombie sharing a raw
    `type` id with an unrelated plant recipe participant inherited that plant's lineage, because
    `recipes` carries no side column and the first draft looked lineage up by id alone —
    `Zombie_side_demons_never_get_lineage_even_if_a_recipe_id_numerically_collides` caught it; fixed
    by gating lineage lookup on `Side == "plant"`.
  - **Second defect:** the synthesized record equality for `DemonCorpusLineage` compared its
    `List<int>` members by reference, so two builder runs over identical input compared *unequal* —
    would have silently broken the byte-identical guarantee. Fixed with a manual
    `SequenceEqual`-based `Equals`/`GetHashCode` override; `Same_inputs_produce_equal_entries_on_repeat_calls`
    proves it now holds.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter DemonCorpusBuilderTests` → **9/9 passed**
  - Files: `src/FusionRpg.Core/Demons/Generation/DemonCorpusBuilder.cs`,
    `tests/FusionRpg.Core.Tests/Demons/DemonCorpusBuilderTests.cs`
  - ⚠️ Deliberate deviation from spec §4: tests live in `Core.Tests`, not a new
    `FusionRpg.DemonCorpusEmit.Tests` project. The builder is pure Core code; a new project needs a
    `ci.yml` step that the known CI defect (only the last `dotnet test` exit code is checked) would
    mask anyway.

- [x] **D1.2 — `tools/DemonCorpusEmit` + committed corpus** · **M** · **Deps:** D1.1 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] `dotnet run --project tools/DemonCorpusEmit -- <data dir>` writes `data/seed/demons/*.json` —
          ran against `dist/FusionRpg.Server/data`: **84 species, 84/84 almanac rows matched (100%,
          confirms the earlier S3 measurement), 1295 recipe rows, 5 partitions written**
    - [x] Calls `DerivedStatPolicy.Configure` **before** constructing `RpgStore`
    - [x] Run twice ⇒ **byte-identical** files — ran to two independent output roots,
          `diff -rq` reported **zero differences** across all 5 partition files
    - [x] Output loads through seedsmith's `Corpus.load` with **no adapter changes** — loaded live:
          `kinds={'demon'}`, `partitions=['plant/common','plant/epic','plant/rare','zombie/epic','zombie/legendary']`,
          `84 entries`, no `adapters/` file touched
    - [x] All SQL stays inside `FusionRpg.Data` — `.\scripts\guard-dal.ps1` → **DAL GUARD OK**
  - **Deviation applied, not just planned:** `JavaScriptEncoder.Create(UnicodeRanges.All)` — the
    default `Utf8JsonWriter` encoder escapes every Chinese character as `\uXXXX`, which is valid JSON
    but makes the committed corpus's diffs unreadable to a human reviewer. Confirmed on-disk bytes
    carry literal UTF-8 (`桃读报僵尸`), not escapes, while byte-identity across runs still holds.
  - Verify: `dotnet run --project tools/DemonCorpusEmit -- dist/FusionRpg.Server/data` (x2, diffed);
    `.\scripts\guard-dal.ps1` → exit 0; `python -c "Corpus.load(...)"` → loads clean
  - Files: `tools/DemonCorpusEmit/Program.cs`, `tools/DemonCorpusEmit/DemonCorpusEmit.csproj`,
    `data/seed/demons/**` (emitted, committed — 5 files, `demon/{plant,zombie}/<rarity>.json`)

- [x] **D1.3 — `DemonsAdapter`, the five methods** · **M** · **Deps:** D1.2 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] `channels()` returns **empty** — `test_channels_are_empty` (A4)
    - [x] `kinds()` contains **no `item` and no `action`** — `test_item_and_action_kinds_are_absent` (A3)
    - [x] `legal_combinations()`'s **`False` branch is reachable and exercised** —
          `test_legal_combinations_false_branch_is_reachable_and_real`, using a **real, verified**
          fact rather than a synthetic example: `DemonSpeciesGenerator.cs` draws `ElementPrimary`
          only from `ElementRoster.Concrete` (6 elements, excludes omni), so no demon of any rarity
          can ever have `ElementPrimary=omni`
    - [x] `family` dimension declared with **empty values**; partitioning falls back to `side/rarity` —
          `test_family_dimension_declared_with_empty_values_in_d1`
    - [x] `environment` declared but its partitions **excluded from coverage**, with the reason in a
          comment (A7) — `NO_GENERATOR_YET`, asserted by `test_environment_partitions_excluded_from_coverage`
    - [x] Every registry vocabulary non-empty and **inlinable** — checked against `briefkit.render`'s
          own live `CITATION_PATTERNS`, not a re-invented pattern set
    - [x] Motif expression rules present for **every** kind (including `demon` itself, a deliberate
          choice — see `kinds.py`'s comment on why); a kind without one fails an `assert` at import time
    - [x] `reference_fields` declares `demonId` on `aspect`/`commander-effect`/`environment`
    - [x] **`test_stub_adapter.py` still passes** — confirmed twice: once by pytest's own collection,
          once by `TheSeamItselfTests` re-running the whole stub suite from inside `test_adapter_demons.py`
    - [x] ⛔ **§D-F1 applied, not just planned:** `adapters/base.py` gained one additive
          `motif_expression: str | None = None` field. `spec-adapter-demons.md` §1 and §4 both
          **corrected in writing**, not just noted here — see the spec file directly.
  - **Second, independent defect found and fixed:** the first draft hand-declared `dimensions()`'s
    `applies_to` for `rarity`/`element` instead of deriving it from real `KindSpec` field membership.
    Running `python -m seedsmith check ../../data/seed/demons --adapter demons` against the **live
    emitted corpus** (not a synthetic fixture — this is what caught it) produced a confirmed false
    positive: `[GAP] Coverage/PairwiseHole — side×rarity: 8 of 8 legal pairs never co-occur` — the
    exact "confidently wrong" trap `adapters/items/__init__.py` already documents avoiding for its
    own `class` dimension. Fixed by using the same auto-computed `_applies_to()` helper items uses,
    unconditionally, for every dimension. Re-ran `check`: the false positive is **gone**.
  - **Third defect, same live-corpus run:** `registries()` never declared a `"partitions"` vocabulary
    key at all, which silently disables `Coverage/EmptyPartition` (`.get(..., frozenset())` makes
    `allocated - occupied` always empty — no crash, no warning, just permanently zero findings).
    Fixed by declaring all 8 legal side×rarity combinations as `PARTITIONS`. Re-ran `check`: now
    correctly reports **3 real gaps** — `plant/legendary`, `zombie/common`, `zombie/rare` are
    genuinely empty (an artifact of the zombies-first, HP-ranked allocation in
    `DemonSpeciesGenerator`), which is exactly the kind of finding this metric exists to surface.
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_adapter_demons.py tests/test_stub_adapter.py -q`
    → **26/26 passed**; full suite → **315/315 passed**
  - Files: `tools/seedsmith/seedsmith/adapters/demons/{__init__,kinds,registries}.py`,
    `tools/seedsmith/tests/test_adapter_demons.py`, `tools/seedsmith/seedsmith/adapters/base.py` (+1 field),
    `tools/seedsmith/seedsmith/adapters/registry.py` (registered `"demons"`)

- [x] **D1.4 — D1 integration** · **S** · **Deps:** D1.3 ✅ **VERIFIED 2026-08-31 against the live corpus**
  - Acceptance:
    - [x] The emitted corpus loads and **every existing metric runs against it** — zero model calls.
          `python -m seedsmith check ../../data/seed/demons --adapter demons` (the real CLI —
          `report` from the spec's own example doesn't exist; corrected here rather than silently
          worked around) ran clean: **3 real `Coverage/EmptyPartition` gaps** (genuine, not a false
          positive — see D1.3), **0 `Coverage/PairwiseHole` findings** (the false positive found and
          fixed during D1.3), 5 `NOT_MEASURED` for `Balance`/`Distribution` families that need
          `numerics`/`budget` — expected and correct, since `channels()` is empty by design (§2.6)
    - [x] Coverage reports real partitions; `environment` is absent from them — trivially and
          honestly true: `registries()["partitions"]` only ever declares `side/rarity` combinations
          (§9 Q1's decision), so no environment-kind partition exists to report on at all. There is
          no separate "exclude environment" mechanism to test because nothing ever included it.
  - Verify: `cd tools\seedsmith; python -m seedsmith check ../../data/seed/demons --adapter demons`;
    `python -m pytest -q` → 315/315

### ✅ CP-D1 — **REACHED 2026-08-31**
- [x] Full seedsmith suite green **including `test_stub_adapter.py`** — 315/315, `test_stub_adapter.py`
      run twice (pytest's own collection + `TheSeamItselfTests`' internal re-run)
- [x] `dotnet test tests\FusionRpg.Core.Tests` green — **4896/4896** (was 4887; +9 from D1.1's
      `DemonCorpusBuilderTests`); `.\scripts\guard-dal.ps1` → **DAL GUARD OK**. Also ran the other
      three boundary guards (`guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`)
      — all green, though this feature doesn't touch any of their surfaces
- [x] Emitter byte-identical across two runs — `diff -rq` on two independent output roots, **zero
      differences** across all 5 partition files
- [x] §D-F1 finding written into `spec-adapter-demons` §1/§4 — both sections corrected in place,
      not just noted in the plan/todo
- [x] **D1 is shippable alone** — demons queryable by every existing metric, no model calls. Proven
      by a real `check` run against the live 84-species corpus finding 3 real content gaps and zero
      false positives, not merely "the command exited 0"

## Phase D2 — taxonomy (first model calls, all faked)

- [x] **D2.1 — `family-extract`** · **M** · **Deps:** D1.4 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Batching over the same corpus repeatedly ⇒ **identical batches, byte for byte** —
          `test_batching_is_deterministic_across_repeat_calls` (fed input in reverse order too)
    - [x] Batch size is a **structural constant (8)** with a comment saying what it trades; not a tunable
    - [x] Each candidate carries `label` (English), `nativeLabel` (as read), `basis`
    - [x] `basis` ∈ {`text`, `name`, `blocked`}; a `blocked` demon carries **no label at all** and it
          is **not** an error — `test_a_demon_with_neither_gets_no_candidate_and_it_is_not_an_error`
          (represented as an empty list, present under the key, not a missing key a caller re-derives)
    - [x] Three siblings in one batch **can receive one shared label** —
          `test_sibling_demons_can_receive_one_shared_label`, asserted against a scripted response
          AND that the prompt text actually contained all three sibling ids (proves the batch was
          genuinely presented together, not just that labels came back matching by coincidence)
    - [x] **Falsifier:** the same fixture at batch size 1 produces three *distinct* labels — proving
          §2.2's batching prevents something real — `test_falsifier_single_demon_batching_produces_distinct_labels`
    - [x] Two candidates differing only in `nativeLabel` still merge downstream — extraction's own
          duty here is narrower and satisfied structurally: `nativeLabel` is carried but never
          used for grouping (extraction does no merging at all). The merge behavior itself is
          `family-consolidate`'s (D2.2) own test.
    - [x] A demon may receive **more than one** label from one batch —
          `test_a_demon_may_receive_more_than_one_candidate_label`
    - [x] Schema passes `audit_schema` (**no numeric field**) and `audit_open_loop_schema` (**no
          verdict/confidence score**) — both asserted directly against the real functions
    - [x] A label returned for a demon **not in the batch** is rejected, not recorded —
          `test_a_label_for_a_demon_outside_the_batch_is_rejected_not_recorded`: the domain gate
          (`_gate_candidates`) refuses it, causing an escalation for that batch (2 named-defect
          heal attempts, then give up) rather than silently dropping just the bad candidate — a
          real, defensible reading of "rejected, not accepted", not a softened one
    - [x] Brief contains no citation-shaped string — `test_brief_contains_no_citation_shaped_text`,
          plus `test_brief_raises_if_a_citation_pattern_is_injected` exercises the check itself
          (checked against `briefkit`'s own live `CITATION_PATTERNS`, not a re-invented set)
    - [x] **Zero real model calls** — `MockModelServer` imported from `test_llm_caller`, not
          re-rolled; `test_zero_real_model_calls_every_request_hits_loopback` asserts the URL
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_family_extract.py -q` → **15/15 passed**;
    full suite → **330/330 passed**
  - Files: `seedsmith/adapters/demons/family/{extract,schema}.py`, `tests/test_family_extract.py`
  - ✅ **`data/seed/demons/_generated/family-candidates.json` — REAL and COMMITTED as of
    2026-09-01.** Writing it required a real model run, reserved by the program's own standing rule
    for a separate, owner-approved act after CP-D4 — the owner authorized it explicitly; see
    "The real generation run" section below for the full account, including a real prompt-format
    defect this run itself exposed and fixed.

- [x] **D2.2 — `family-consolidate`** · **M** · **Deps:** D2.1 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Same candidates consolidated twice ⇒ **byte-identical** vocabulary and assignments —
          fed in reverse input order too, confirming sort-then-merge really is order-independent
    - [x] `wall-nut`, `defensive-nut`, `nut-type` ⇒ one family, head noun `nut` — matches the
          spec's own illustrative example exactly, via a documented rule: the last normalized
          token that is not in a small fixed suffix set (`type`/`class`/`kind`/`variant`/`family`)
    - [x] `shell` + `armor-plated` in the synonym map ⇒ merged
    - [x] **Empty synonym map ⇒ NOT merged** — `test_shell_and_armor_plated_do_not_merge_with_an_empty_synonym_map`
    - [x] `basis = "blocked"` ⇒ **zero** families, not an error — structural, not a special case: a
          `blocked` demon never produces a `FamilyCandidateInput` at all, so it has no row to merge
    - [x] Candidates from two heads ⇒ **both** families (multi-membership)
    - [x] Adding a demon and re-running: existing ids unchanged, new family **appended at the end**
    - [x] A family with no supporting candidate is **rejected** — consolidation cannot invent (§2.5).
          There is no code path that creates a family without a real candidate driving it (checked
          directly: the output family set is always exactly the set of canonical keys the input
          candidates derive to, nothing more) — and separately, an existing registry entry with NO
          candidate in a given run is *carried forward*, never deleted, which is append-only doing
          its job rather than an exception to "cannot invent"
    - [x] The merged family carries its contributing `nativeLabel`s
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_family_consolidate.py -q` → **16/16 passed**;
    full suite → **346/346 passed**
  - Files: `seedsmith/adapters/demons/family/{consolidate.py,synonyms.json}`,
    `tests/test_family_consolidate.py`
  - ✅ **`families.v1.json`/`family-assignments.json` — REAL and COMMITTED as of 2026-09-01** —
    consolidated from the real `family-candidates.json` above: **19 real families**. A real,
    two-directional stopword defect was found and fixed running this against real model output —
    see "The real generation run" below for the full account and its two regression tests.

- [x] **D2.3 — `motif-derive`** · **M** · **Deps:** D2.2 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Same corpus derived twice ⇒ **byte-identical** assignments — `test_same_corpus_derived_twice_is_byte_identical`
    - [x] Demon in two families **inherits from both** — `test_demon_in_two_families_inherits_from_both`
    - [x] Demon in no family, with text ⇒ motifs from own text, `basis = "text"`
    - [x] Demon in no family, no text ⇒ **no motifs**, `basis = "blocked"` — not padded, not an error
    - [x] Family with `basis = "name"` ⇒ inherited motifs carry `basis = "name"`, not `"text"` —
          `test_family_with_basis_name_propagates_basis_name_not_text`, deliberately constructed so
          the family POOL itself also contains a text-derived token from a different member, proving
          `basis` tracks *this demon's own* membership provenance, not the pool's aggregate content
    - [x] Motif count **≤5** wherever any motif exists; ordering **family-first** and stable
    - [x] Anti-motifs drawn from the contrasting family; non-empty **wherever another family exists
          anywhere in the input**, not just on the demon's own record — both the positive case and
          the negative (`test_anti_motifs_empty_when_no_other_family_exists_anywhere`) are tested
    - [x] `DerivedMotifs` contains **no numeric field** — `test_derived_motifs_carries_no_numeric_field`
          via reflection over the dataclass's own field types
    - [x] ⛔ **A2's tautology case is FLAGGED in the output** — `test_a2_tautology_flagged_when_own_and_every_family_are_basis_name`,
          with a companion `test_not_tautological_when_any_contributing_basis_is_text` proving the
          flag isn't just always-true
  - **Real defect found and fixed during this build:** the first draft's `basis` combination logic
    excluded a demon's own `basis="name"` contribution from the combined `basis` whenever the demon
    had any family — meaning a demon whose OWN name-derived token genuinely survived into `motifs`
    would incorrectly report a basis that ignored that fact. Fixed by tracking whether the own
    token actually survived the final 5-motif trim (`own_contributed`) and only excluding it from
    the basis computation when it truly did not contribute — caught by re-deriving the fix from
    first principles while writing this note, not by a failing test (worth flagging: the test suite
    did not catch this one, so it is not proof the fix is complete — a real limitation of this
    module's current coverage, not silently smoothed over).
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_motif_derive.py -q` → **16/16 passed**;
    full suite → **362/362 passed**
  - Files: `seedsmith/adapters/demons/motifs.py`, `tests/test_motif_derive.py`
  - ✅ **`motifs.v1.json`/`motif-assignments.json` — REAL and COMMITTED as of 2026-09-01** —
    **84/84 demons**, 0 blocked, 0 tautological. A real, more serious defect (whole Chinese
    sentence clauses captured as single "motifs") was found and fixed with an owner-approved
    `jieba` dependency — see "The real generation run" below for the full account.

### ✅ CP-D2 — **REACHED 2026-08-31**
- [x] Every D2 artifact byte-identical across re-runs — proven per-module (D2.1/D2.2/D2.3 each have
      their own repeat-call test); no combined end-to-end artifact exists yet since none of the
      three modules have run against real committed data (deliberately — see each task's own note)
- [x] **Zero real model calls anywhere in the suite** — `test_zero_real_model_calls_every_request_hits_loopback`
      (D2.1) asserts the loopback URL directly; D2.2/D2.3 make no network calls at all (pure functions)
- [x] `blocked` propagates end to end and is never an error — traced through all three modules:
      family-extract (`basis="blocked"` = absent from `candidates`) → family-consolidate (no
      `FamilyCandidateInput` row = zero families, not an error) → motif-derive (`basis="blocked"`,
      empty motifs, `tautological=False`, no exception anywhere)
- [x] Append-only proven: adding a demon leaves existing family and motif ids untouched —
      `test_a_family_id_present_in_the_registry_is_never_renamed_or_repositioned` and
      `test_adding_a_new_demon_and_rereading_leaves_existing_ids_unchanged_new_appended_at_end` (D2.2)
- [x] `python -m pytest -q` full suite green — **362/362**

## Phase D3 — measurement (gates D4)

- [x] **D3.1 — `Coverage/DemonUncovered`** · **S** · **Deps:** D2.3 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Every demon has content ⇒ reports **nothing** — `test_every_demon_has_content_reports_nothing`
    - [x] ⛔ **A5's exact case:** one demon uncovered while **all its families are covered** ⇒ **one
          finding** — `test_a5_one_demon_uncovered_its_families_all_covered_is_one_finding`
    - [x] "Content" = **any** generated artifact; the finding's evidence carries a **per-kind
          breakdown** naming what is absent — corrected in the same pass (see the spec's own
          §2.1 correction note): the breakdown lives inside the ONE finding a zero-content demon
          produces (`absentKinds`), not as a second finding type for partly-covered demons — a
          covered demon (any content at all) still produces silence, matching
          `Coverage/EmptyPartition`'s own convention
    - [x] A demon with a commander effect but no aspect ⇒ **covered**, no finding —
          `test_a_demon_with_commander_effect_but_no_aspect_is_covered`
    - [x] `gates = False`; `loop = CLOSED`; deterministic finding order — `test_finding_ordering_is_stable_across_runs`
    - [x] **Fully generic** (spec §4's own requirement: "work for a non-demon adapter that supplies
          the same strata") — `test_generic_across_a_non_demon_subject_kind` points the SAME class
          at a `widget`/`gadget` fixture with `subject_kind="widget"` and it works unmodified
  - **Verified against the LIVE corpus, not just synthetic fixtures** — registered into the real
    `report.cli.build_registry()` and run via `python -m seedsmith check ../../data/seed/demons
    --adapter demons`: **84 real `Coverage/DemonUncovered` GAP findings, one per demon** — correct
    and expected, since nothing generates aspect/commander-effect content yet. Cross-checked for
    false positives by running `check` against the **items** corpus too: **zero** demons-metric
    findings there, confirming genericity holds on real data, not only in a hand-built fixture.
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_demon_metrics.py -q` → **14/14 passed**;
    `python -m seedsmith check ../../data/seed/demons --adapter demons` → 84 real gaps;
    `python -m seedsmith check ../../data/seed/items --adapter items` → 0 demons-metric findings
  - Files: `seedsmith/metrics/demon_coverage.py`, `tests/test_demon_metrics.py`,
    `seedsmith/report/cli.py` (registered both D3 metrics)

- [x] **D3.2 — `Distribution/MotifSharing`** · **S** · **Deps:** D2.3 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Reports `demonsPerMotif`, **`demonCount`**, `excludedTautological`, `singleUseMotifs` —
          **counts** for `excludedTautological`/`singleUseMotifs`; `demonsPerMotif` itself is a
          ratio by definition (that IS "demons per motif") — omitted entirely, not reported as `0`
          or `1`, when nothing could be measured (`test_a2_entirely_tautological...` asserts its
          **absence** from evidence, not a misleading placeholder value)
    - [x] A demon with motifs **and** families both `basis = "name"` is **excluded** from numerator
          and denominator, and counted in `excludedTautological` — `test_a_tautological_demon_is_excluded_from_both_numerator_and_denominator`
          (2 real demons sharing a motif score `demonsPerMotif == 2.0` exactly — the 3rd,
          tautological one does not inflate it)
    - [x] ⛔ **The decisive test:** a **wholly tautological corpus reports "cannot be measured"**, not
          perfect sharing — `test_a2_entirely_tautological_corpus_reports_cannot_be_measured_not_success`
    - [x] Motifs each used once ⇒ `singleUseMotifs` equals vocabulary size; sharing reported absent —
          `test_motifs_each_used_once_single_use_equals_vocabulary_size`
    - [x] `loop = OPEN`; schema carries **no pass/fail field** — `test_schema_carries_no_pass_fail_field`;
          **every** finding this metric emits is `Severity.NOTE`, never `GAP` — the metric asserting
          nothing is "wrong" is itself part of never grading its own homework
    - [x] `singleUseMotifs` reports **all** — no suppression threshold anywhere in the implementation
    - [x] `demonCount` reported in **every** branch (no-motif-data, all-tautological, and the normal
          case), so two runs over different-sized rosters are always distinguishable
  - **Genericity gap found and closed in the same pass as D3.1:** the first draft returned a
    "nothing to measure" NOTE unconditionally whenever no demon carried motif data — including for
    an adapter (`items`, `_stub`) that has **zero entries of `subject_kind` at all**. That would
    have fired a demons-shaped NOTE on every non-demon `check` run. Fixed with the same
    no-subjects-at-all early return `DemonUncoveredMetric` already has; confirmed live on the
    `items` corpus (zero demons-metric findings; see D3.1's own live-run evidence).
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_demon_metrics.py -q` → **14/14 passed**
    (shared file with D3.1); real `check` run against the live demons corpus →
    `[NOTE] Distribution/MotifSharing — no demon entry carries motif data yet` (correct: D2's real
    output isn't committed, so there is genuinely nothing to measure yet)
  - Files: `seedsmith/metrics/motif_sharing.py`, `tests/test_demon_metrics.py`,
    `seedsmith/report/cli.py` (shared registration edit with D3.1)

### ✅ CP-D3 — THE GATE — **REACHED 2026-08-31**
- [x] Both metrics ship `gates = False` — `test_both_metrics_ship_non_gating`
- [x] Both live in `metrics/` and work for a **non-demon** adapter supplying the same strata —
      `DemonUncoveredMetric` proven directly (`test_generic_across_a_non_demon_subject_kind`);
      `MotifSharingMetric` proven live (silent, correctly, on the real `items` corpus)
- [x] **The tautology test passes — D4 does not start until it does.**
      `test_a2_entirely_tautological_corpus_reports_cannot_be_measured_not_success`: **passed.**
      Full suite: **376/376.** D4 may begin.

## Phase D4 — consumption

- [x] **D4.1 — theme registry + items vocabulary** · **M** · **Deps:** CP-D3 ✅ **BUILT + VERIFIED 2026-08-31**
  - Acceptance:
    - [x] Emits `data/seed/demons/_registry/themes.v1.json`, append-only, sorted keys — **REAL and
          COMMITTED as of 2026-09-01: 84 themes**, all `demon.*`-prefixed, rarity distribution
          7/14/21/42 exactly matching the catalog. Upgraded from `[~]` now that the real model run
          producing its real input (`motif-derive`'s committed output) has actually happened — see
          "The real generation run" section below.
    - [x] Every demon theme id is **`demon.*`**-prefixed; collision with legacy `theme.*` is
          impossible **by construction** — `test_theme_key_vocabulary_is_the_union_and_prefixes_cannot_collide`
          asserts the two prefix-partitioned sets have **empty intersection**, not merely that no
          collision happened to occur in one fixture
    - [x] A theme carries motifs, anti-motifs, **expression rules**, `basis`, and the **`rarity` it
          was published against** — `test_a_demon_with_motifs_publishes_a_theme_carrying_everything`;
          rarity-as-snapshot proven separately by `test_republishing_never_recomputes_an_already_published_theme`
    - [x] A demon with `basis = "blocked"` **publishes no theme** — `test_a_blocked_demon_publishes_no_theme`
    - [x] A demon with `basis = "name"` publishes a theme **marked as such** —
          `test_a_name_basis_demon_publishes_a_theme_marked_as_such`
    - [x] A theme without expression rules **fails validation** — structurally guaranteed (no
          construction path omits them) and asserted directly,
          `test_every_published_theme_carries_expression_rules_structurally`
    - [x] Items' `themeKey` becomes a registry-backed vocabulary; a key in **neither** population is
          **rejected** — `test_a_key_in_neither_population_is_illegal`, via the SAME `RegistrySet.is_legal`
          mechanism `Coverage/EmptyPartition` already relies on for `"partitions"` — this codebase's
          established pattern for "how is a vocabulary enforced", not a new metric invented for
          this one field
  - **Real defect caught before it shipped:** the spec's own citation of "31 sets, 8 uniques, 39
    total" was **wrong**. A fresh `Corpus.load` count against the live corpus gives **30 sets + 8
    uniques = 38**. Corrected in `spec-demon-themes.md` (4 spots) and flagged in
    `review/audit-demons-specs.md` (S5's historical entry left as-is, with a dated correction note
    beside it — measured-at-the-time is not the same as wrong, but the CURRENT number the test
    asserts against is 38, not 39).
  - Verify: `cd tools\seedsmith; python -m pytest tests/test_demon_themes.py -q` → **16/16 passed**;
    full suite → **392/392 passed**
  - Files: `seedsmith/adapters/demons/themes.py`, `seedsmith/adapters/items/registries.py` (EDIT —
    the one permitted file outside `adapters/demons/`; added `load_theme_keys()` and an optional
    `demon_theme_keys` parameter on `load_vocabularies()`, backward-compatible default), `tests/test_demon_themes.py`

- [x] **D4.2 — coexistence and churn proof** · **S** · **Deps:** D4.1 ✅ **VERIFIED 2026-08-31 against the live corpus**
  - Acceptance:
    - [x] ⛔ **All 38 existing themed entries still validate** (corrected count — see D4.1's own
          note) — `test_all_existing_live_themed_entries_still_validate` loads the **real**
          `data/seed/items` corpus with `Corpus.load` (not a fixture) and checks every themed entry
          against `load_vocabularies()` with **no demon keys unioned in at all** — proving the
          legacy population alone, unmodified, still validates everything it always did
    - [x] A legacy `theme.*` key validates alongside `demon.*` keys —
          `test_a_demon_key_becomes_legal_once_unioned_in`
    - [x] A demon that leaves the roster ⇒ its theme is **retired (`retired: true`), still
          resolvable, never deleted** — `test_a_demon_that_leaves_the_roster_is_retired_not_deleted`,
          `test_a_retired_theme_is_still_resolvable_with_its_original_data`
    - [x] A re-run with a new demon leaves existing keys untouched — same mechanism D2.2 already
          proved for family ids, re-exercised here for theme keys
    - [x] **Direction asserted structurally:** nothing in `adapters/demons/` reads the items corpus —
          `test_themes_module_never_imports_from_the_items_adapter` greps the module's own source,
          not just "no test happened to import it"
  - Verify: `cd tools\seedsmith; python -m pytest -q` → **392/392 passed**;
    `python -m seedsmith check ../../data/seed/items --adapter items` → **31 gap, 78 note, 1
    not_measured** — byte-for-byte identical to the pre-D4 baseline, confirming the `registries.py`
    edit introduced zero regressions to existing item reporting

### ✅ CP-D4 — closes Part 4 — **REACHED 2026-08-31**
- [x] Full seedsmith suite green — **392/392**
- [x] `dotnet test` green across Core / Data / Guard — see the full-program sweep below;
      four `scripts\guard-*.ps1` green
- [x] Exactly **one** file outside `adapters/demons/` changed in D4 — `adapters/items/registries.py`,
      adding a vocabulary (`load_theme_keys()`, an optional `demon_theme_keys` param) not a concept
- [x] An item can be authored themed to a demon and validates —
      `test_a_demon_key_becomes_legal_once_unioned_in`

## Part 4 standing rules (in addition to the program's)

- **`basis` is never optional.** It is an input to a correctness check (A2), not an audit trail.
- **`blocked` is an answer, not a failure**, at every stage.
- **Never a number** in demon content — structural, since `channels()` is empty.
- **Append-only means never renumber.** Position feeds derived ids and content hashes.
- **Fixtures synthetic**, and now doubly so: the live roster is no longer a fixed size (§D-F3).
- **Authorized 2026-08-31.** Build proceeded D1 → D2 → CP-D3 (the tautology gate) → D4, all
  reached the same day.

## The real generation run — owner-authorized 2026-09-01, after CP-D4

Every mechanism above was proven against `MockModelServer`, per the standing "no real model calls"
rule. The owner then explicitly authorized spending real calls against the real local model already
running (`google/gemma-4-26b-a4b-qat` via LM Studio, `http://localhost:1234` — the toolchain's own
documented default, confirmed reachable before anything was sent to it). All six generated artifacts
are now **really committed**, not deferred:

- [x] **`family-candidates.json`** — 11 real batches, 84 demons, 104.3s wall-clock. **53/84 demons
      received ≥1 candidate, 31 blocked** (no shared family the model would support — real
      `basis="blocked"` outcomes, not a bug). Real groupings: `cherry-themed` spans 7 demons,
      `matryoshka-dolls` correctly catches `dollgold`/`dollsilver`.
  - ⛔ **Real defect found and fixed before consolidating:** `extract_family_candidates`'s
    `build_user` callback sent ONLY the raw brief — no JSON-shape instructions at all. It only
    "worked" in tests because `MockModelServer` returns its queued response regardless of prompt
    content. A hand-probe against the real model confirmed the gap; fixed by adding
    `_response_format_instructions()`, appended to every batch's prompt. Mock-based tests
    (unaffected, since they don't depend on prompt content) stayed green throughout; the real
    model's shape-compliance was then re-verified live before running the full 11 batches.
- [x] **`families.v1.json` + `family-assignments.json`** — 53 candidates → **19 real families**
      (`bucket`, `cactus`, `cherry`, `corn`, `dolls`, `double`, `garlic`, `hypno`, `ice`, `fire`,
      `light`, `chomper`, `nut`, `pea`, `sun`, `base`, `fruit`, `sunflower`, `line`).
  - ⛔ **Real defect found and fixed, confirmed both failure directions on the same real batch:**
    `_GENERIC_SUFFIXES` (originally 5 words: type/class/kind/variant/family) was far too narrow for
    what the real model actually produced — labels like `fire-based`, `light-based`, `chomper-kin`,
    `nut-kin`, `pea-kin`, `ice-attackers`, `bucket-users`, `sun-producers`. Left unfixed this
    **silently merged unrelated families** (`fire-based`+`light-based` → one false "based" family;
    `chomper-kin`+`nut-kin`+`pea-kin` → one false "kin" family) — the false-merge direction is the
    more dangerous one, exactly what audit A6 named this module to prevent — **and** split identical
    families apart (`ice-attackers` vs `ice-family` never merged). Fixed by expanding the stopword
    set to the realistic vocabulary of generic relational suffixes. Regression tests added and
    passing: `test_generic_relational_suffixes_do_not_become_the_family_head`,
    `test_generic_relational_suffixes_do_not_falsely_merge_different_themes` — both pin the exact
    real labels that triggered the bug.
- [x] **`motifs.v1.json` + `motif-assignments.json`** — **84/84 demons** motif-derived (0 blocked,
      100% flavour coverage confirming the earlier S3 measurement), **0 tautological**.
  - ⛔ **Real, more serious defect found and fixed:** `own_motifs`'s tokenizer treated Chinese text
    as "maximal punctuation-free run" — since Chinese carries no spaces between words, this returned
    **whole sentence clauses** as single "motifs" (e.g. `以下能防止爆炸樱桃产生溅射`, an entire
    clause), unusable as shared vocabulary. No regex can fix this — Chinese word segmentation needs
    linguistic knowledge a regex does not have. **Fix required a new dependency** (`jieba`, real
    Chinese segmentation), which the program's own standing rule ("stdlib only outside `pipeline`")
    does not permit without sign-off — asked the owner directly rather than adding it silently or
    quietly downgrading to a worse dependency-free heuristic; **approved 2026-09-01**, scoped to
    this one module's tokenizer. A curated Chinese stopword list (particles/common verbs, `_CJK_
    STOPWORDS`) filters function words jieba's segmentation alone doesn't remove. Regression test:
    `test_chinese_flavor_text_produces_real_short_words_not_whole_clauses`, using the exact clause
    that triggered the bug. **Verified clean on the full real output**, not just the fixture: every
    one of the 120 distinct real tokens across all 84 demons is ≤4 characters — zero whole-clause
    fragments remain anywhere.
- [x] **`themes.v1.json`** — **84 real themes published**, all `demon.*`-prefixed, 0 blocked.
      Rarity distribution **7 legendary / 14 epic / 21 rare / 42 common** — exact match to the
      catalog's own known split, confirming the rarity-snapshot wiring is correct.
- [x] **All 38 pre-existing legacy `theme.*` themed entries re-verified against the real corpus
      with real demon themes now present** — `python -m seedsmith check ../../data/seed/items
      --adapter items` unchanged from the pre-generation baseline (31 gap, 78 note, 1 not_measured).

**Verify (after the real run):** `python -m pytest -q` (from `tools/seedsmith/`) → **395/395**;
`dotnet test tests\FusionRpg.Core.Tests` → **4896/4896**; all four `scripts\guard-*.ps1` → green.

**One honest limitation, not silently smoothed over:** `Distribution/MotifSharing` still reports
"no demon entry carries motif data yet" when run live, even with `motif-assignments.json` real and
committed — because nothing merges that file's contents back onto the `demon`-kind corpus entries
`Corpus.load` reads. This was never an explicit task in either source-of-truth file (D3.2's own
spec always described reading `entry.get("motifs")` as depending on future wiring, not on this
run); it is a genuine, separate, unspecced integration step, not a gap in what this run was asked
to produce.

---

# Part 5 — Feature 3: generation runtime (G0–G4)

Plan: [seedsmith-plan.md](seedsmith-plan.md) Part 5. Specs: five modules under
[docs/architecture/seedsmith/](../docs/architecture/seedsmith/), **SEALED — approved by the owner
2026-09-01, authorized to build.** Zero open questions: all closed by measurement
([spec audit](../docs/architecture/seedsmith/review/audit-generation-runtime-specs.md), 10 findings).

**Read the locked-decisions table in the plan before starting** — engine, checkpoint store, model,
motif instrument, CoVe status and one-per-demon were each settled with evidence and are not to be
re-argued mid-build.

## Phase G0 — dependency baseline ⛔ BLOCKING

- [ ] **G0.1 — `pyproject.toml`, exact pins, lockfile, isolated venv** · **M** · **Deps:** none
  - Acceptance:
    - [ ] `pyproject.toml` declares `jieba`, `langgraph==1.2.11`, `langgraph-checkpoint-sqlite`
    - [ ] Every pin is `==`, **never** `>=` — LangGraph shipped 10 releases in 2026-04 alone
    - [ ] `requirements.lock` committed with the full transitive set (~31 packages, incl. `langsmith`)
    - [ ] CI installs from the lockfile in a clean environment
    - [ ] ⚠️ CI step positioned so its failure **cannot be masked** by the known `ci.yml` defect
          (only the last `dotnet test` exit code is checked)
  - Verify: fresh clone → `python -m venv` → `pip install -e ".[dev]"` → `pytest` → **full suite passes**
  - Files: `tools/seedsmith/pyproject.toml`, `tools/seedsmith/requirements.lock`, `.github/workflows/ci.yml`

- [ ] **G0.2 — offline guarantee as a test** · **S** · **Deps:** G0.1
  - Acceptance:
    - [ ] `LANGSMITH_TRACING` and `LANGCHAIN_TRACING_V2` asserted **unset**
    - [ ] A graph runs under a socket guard raising on any non-loopback connect → **zero attempts**
    - [ ] Test uses stdlib `socket` patching — no new dependency to test that we have few
  - Verify: `python -m pytest tests/test_offline_guarantee.py -q`
  - Files: `tools/seedsmith/tests/test_offline_guarantee.py`

- [ ] **G0.3 — `response_format` constrained decoding in `llm_caller`** · **S** · **Deps:** G0.1
  - Acceptance:
    - [ ] Optional `schema` parameter, `None` default
    - [ ] ⛔ `call_model(schema=None)` produces a **byte-identical** request body to today — this
          module must be provably inert for every existing caller
    - [ ] With a schema: response parses with plain `json.loads`, no fence stripping needed
    - [ ] An `enum` field cannot produce an out-of-enum value (measured: it could not)
    - [ ] `extract_json` **still present and still tested** — defense-in-depth, not replaced
  - Verify: `python -m pytest tests/test_llm_caller.py -q`
  - Files: `tools/seedsmith/seedsmith/pipeline/llm_caller.py`, `tools/seedsmith/tests/test_llm_caller.py`

### ✅ CP-G0
- [ ] Fresh clone + clean venv + lockfile install + full suite **passes** (criterion is "passes", not a number)
- [ ] `import jieba` succeeds in a fresh venv — the D2.3 debt is paid
- [ ] Offline guarantee is a passing test, not a claim
- [ ] Every existing `llm_caller` caller is provably unchanged

## Phase G1 — motif prose filter (no model, no framework)

- [ ] **G1.1 — four-rule line classifier** · **S** · **Deps:** G0
  - Acceptance:
    - [ ] `韧性：270+2200（一类）`, `伤害：20/1.5秒` → **mechanical** (rule 1)
    - [ ] `特点：…`, `融合配方：…` → **mechanical** (rule 2)
    - [ ] ⛔ `②处于火力覆盖模式时…` → **mechanical** (rule 3) — the 13-line leak the audit found
    - [ ] ⛔ `对于血量高于50%的…` → **mechanical** (rule 4)
    - [ ] ⛔ `可在三种攻击模式之间切换` → **prose** — proves rule 4 is ASCII-only and does not over-filter
    - [ ] A prose sentence with a mid-clause colon → **prose** (the ≤12-char label bound)
    - [ ] `classify_line` is a pure function, exported and tested directly
  - Verify: `python -m pytest tests/test_motif_derive.py -q`
  - Files: `seedsmith/adapters/demons/motifs.py`, `tests/test_motif_derive.py`

- [ ] **G1.2 — POS filtering via `jieba.posseg`** · **M** · **Deps:** G1.1
  - Acceptance:
    - [ ] Keep `n*`/`v*`/`a*`/`i`/`l`; drop `r`/`c`/`d`/`p`/`u`/`t`
    - [ ] ⛔ `为什么`/r, `是因为`/c, `不过`/c **dropped** — the `bucketnutzombie` regression, pinned
    - [ ] ⛔ `铁头功`/n, `坚果`/n, `练成`/v **kept** — proves it is not deleting everything
    - [ ] `_CJK_STOPWORDS` reduced to a small override list, no longer the primary mechanism
  - Verify: `python -m pytest tests/test_motif_derive.py -q`

- [ ] **G1.3 — wire in, regenerate, verify** · **M** · **Deps:** G1.2
  - Acceptance:
    - [ ] `flavorIntroduce` preferred where present (18/84 demons)
    - [ ] ⛔ Three named regression demons: `一类`, `伤害`, `优先` **gone**
    - [ ] Same corpus filtered twice → byte-identical
    - [ ] Max token length still ≤4 (the D2.3 whole-clause guard)
    - [ ] A demon losing all prose falls back to name (`basis="name"`) — **not an error**
    - [ ] Rise in `basis="name"`/`blocked` counts **reported as a result**
  - Verify: `python -m seedsmith demons motifs`; `python -m pytest -q`
  - ⚠️ **Append-only correction, owner-visible:** regeneration **drops** motif ids like `一类` from
    `motifs.v1.json`. Safe **only because nothing is bound to them** — all 84 demons currently have
    zero generated content. **This window closes when G4 writes its first row.** A reviewed
    correction of bad data, not a routine re-run.

### ✅ CP-G1
- [ ] Stat lines contribute **zero** tokens to any demon's motifs
- [ ] Both POS regression sets pinned (dropped connectives, kept content words)
- [ ] Determinism and ≤4-char guarantee hold
- [ ] `motifs.v1.json` regenerated as a reviewed act, before any content binds to it

## Phase G2 — workflow runtime (parallel with G1 once G0 lands)

- [ ] **G2.1 — state + nodes, no LangGraph** · **M** · **Deps:** G0
  - Acceptance:
    - [ ] `GenerationState` TypedDict; every field bounded, **no `messages` accumulator**
    - [ ] `nodes/` are plain `(state) -> dict` functions, unit-testable with a plain dict
    - [ ] ⛔ **Seam test: zero LangGraph imports in `nodes/` or `state.py`** — asserted by grep,
          not left to discipline. This is the deliverable
  - Verify: `python -m pytest tests/test_workflow_structure.py -q`
  - Files: `seedsmith/workflow/{state.py,nodes/*}`, `tests/test_workflow_structure.py`

- [ ] **G2.2 — graph skeleton and bounded loops** · **M** · **Deps:** G2.1
  - Acceptance:
    - [ ] `START → brief → generate → validate → route → {persist|generate|escalate} → END`
    - [ ] Three independent stops: `attempts`, `recursion_limit`, terminal `escalate`
    - [ ] ⛔ A deliberate routing bug is **still stopped** by `recursion_limit` — the backstop is
          exercised, not merely configured
    - [ ] Clean draft → `attempts == 1`; defective → repair carries the **named** defect
    - [ ] Never-clearing draft → **escalates**, writes nothing
    - [ ] **No unbounded `while`** anywhere in the module
  - Verify: `python -m pytest tests/test_workflow_runtime.py -q`

- [ ] **G2.3 — checkpointing and resume** · **M** · **Deps:** G2.2
  - Acceptance:
    - [ ] `SqliteSaver`, thread-id per subject
    - [ ] ⛔ Kill mid-run, re-invoke same thread-id → resumes; **finished nodes do not re-call the model**
    - [ ] ⛔ **Transient** retry = replay from checkpoint, **zero** new model calls
    - [ ] ⛔ **Quality** retry = a genuinely new generation with the defect attached
    - [ ] The two are demonstrably **different code paths**
    - [ ] `sqlite3` used for checkpoints only; Python still never reads the game's SQLite
  - Verify: `python -m pytest tests/test_workflow_runtime.py -q`

- [ ] **G2.4 — bounded fan-out runner** · **S** · **Deps:** G2.3
  - Acceptance:
    - [ ] Bounded worker count, a structural constant with a comment (not a tunable)
    - [ ] Results deterministic per subject regardless of completion order
  - Verify: `python -m pytest -q`

### ✅ CP-G2
- [ ] Zero LangGraph imports outside `graphs/`, asserted
- [ ] Graph structure assertable **offline** — no model, no network
- [ ] Crash-resume works and skips completed nodes
- [ ] `recursion_limit` backstop exercised
- [ ] Transient vs quality retry proven distinct

## Phase G3 — quality gates

- [ ] **G3.1 — deterministic validator library** · **M** · **Deps:** G2
  - Acceptance:
    - [ ] `motif_coverage` rejects output using none of the subject's motifs
    - [ ] `anti_motif_violation` rejects output using a word the subject is defined against
    - [ ] ⛔ `field_echo` **rejects** `{"doctrine": "DOCTRINE: …"}` — the exact observed defect
          (7 of 8 outputs), pinned
    - [ ] ⛔ `field_echo` **accepts** `{"doctrine": "The doctrine of …"}` — over-refusal is its own
          defect; a rule rejecting any mention would pass its rejection test while breaking real prose
    - [ ] `non_empty` rejects empty/whitespace required fields
    - [ ] Defect strings name the **field and the offending value** (they feed the repair prompt)
  - Verify: `python -m pytest tests/test_quality_gates.py -q`

- [ ] **G3.2 — tier labelling** · **S** · **Deps:** G3.1
  - Acceptance:
    - [ ] Every validator result carries its tier
    - [ ] ⛔ No summary anywhere reports a tier-2 pass rate as "quality" — the measured 8/8-on-bad-
          content gap is the reason this rule exists
  - Verify: `python -m pytest tests/test_quality_gates.py -q`

- [ ] **G3.3 — CoVe: specified, wired off** · **M** · **Deps:** G3.2
  - Acceptance:
    - [ ] ⛔ Every verification question **answerable from source alone** — a subjective question is
          a defect (subjective form measured **1/3**, useless)
    - [ ] Verifier is **not shown the draft's justification**, asserted structurally
    - [ ] Rejects only on **explicit contradiction**; rejection → **escalate**, never auto-repair
    - [ ] CoVe schema carries **no verdict field**
    - [ ] ⛔ **Not wired into the default graph**, asserted — specified, not built
    - [ ] Self-consistency implemented and **asserted off**
  - Verify: `python -m pytest tests/test_cove.py -q`

### ✅ CP-G3
- [ ] Four validators with positive **and** negative tests
- [ ] Tier labelling enforced; no pass-rate-as-quality anywhere
- [ ] CoVe present, disabled, asserted
- [ ] Zero real model calls in the suite

## Phase G4 — commander-effect (the first real generator)

- [ ] **G4.1 — brief, schema, gate** · **M** · **Deps:** G1, G2, G3
  - Acceptance:
    - [ ] Brief inlines motifs, anti-motifs and the expression rule **literally**; cites nothing
    - [ ] Schema passes `audit_schema` (**no numeric field**) and `audit_open_loop_schema`
    - [ ] Schema audited at **import time** (an unusable schema cannot be registered)
  - Verify: `python -m pytest tests/test_commander_effect.py -q`

- [ ] **G4.2 — graph wiring** · **S** · **Deps:** G4.1
  - Acceptance:
    - [ ] Thin wiring over G2's shared skeleton; no new control flow
    - [ ] ⛔ A `blocked` demon **generates nothing** — an answer, not a failure
    - [ ] ⛔ An **unprefixed** id (`wallnut` not `commander-effect.wallnut`) **fails corpus load** —
          `Corpus.add` raises on duplicate ids across all kinds; the demon `wallnut` would collide
    - [ ] Re-run over unchanged input → **zero** new writes (G2 idempotence)
    - [ ] Same corpus generated twice against the mock → byte-identical
  - Verify: `python -m pytest -q`

- [ ] **G4.3 — real run + quality sample** · **M** · **Deps:** G4.2
  - Acceptance:
    - [ ] ⛔ **Only after G1 has landed** — generating from `一类`/`僵尸` motifs would bake stat
          vocabulary into committed, append-only content
    - [ ] Every non-blocked demon has a commander effect; committed
    - [ ] `Coverage/DemonUncovered` count **falls** by that number
    - [ ] ⛔ **Quality reported from a read stratified sample**, never from the tier-2 pass rate
    - [ ] If shoehorning **persists** after G1, that is the trigger to build CoVe — record the
          measurement either way
  - Verify: `python -m seedsmith demons generate --kind commander-effect`;
    `python -m seedsmith check ../../data/seed/demons --adapter demons`

### ✅ CP-G4 — closes Part 5
- [ ] Full seedsmith suite green; `dotnet test` green across Core/Data/Guard; four guard scripts green
- [ ] `Coverage/DemonUncovered` reduced, verified by a real `check` run
- [ ] Quality reported from a read sample, separately from the pass rate
- [ ] CoVe build decision recorded with evidence, either way

## Part 5 standing rules

- **The seam is the deliverable.** LangGraph imports live only in `graphs/`; a node is a function
  you can call with a dict.
- **Bound every loop three ways.** Not stopping is 28.1% of field-observed agent failures.
- **Transient ≠ quality retry.** Replay vs regenerate; conflating them is where the cost bugs live.
- **A pass rate is not quality.** Measured 8/8 on visibly shoehorned content.
- **Cheapest instrument first.** No model where a `==` or a POS tag decides it.
- **G1 before G4's real run.** Non-negotiable; append-only content cannot be un-bound.
