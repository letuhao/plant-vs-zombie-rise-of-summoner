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

Full rationale, dependency graph, and the P2 prerequisite gap: [seedsmith-plan.md](seedsmith-plan.md) Part 2.

### Phase 1 — Feasibility and ordering

#### P1 — Feasibility: pigeonhole → Hopcroft–Karp → König
`seedsmith/planner/feasibility.py`

- [ ] Layer 1: pigeonhole sum check (Σdemand > Σcapacity ⇒ infeasible), O(n)
- [ ] Layer 2: max bipartite matching (demand ↔ slot graph) via Hopcroft–Karp, O(E√V)
- [ ] Layer 3: on infeasible, König's theorem names the binding constraint (min vertex cover), not
      a bare "infeasible"
- [ ] Balanced-case construction: cyclic Latin square `axis = (roleIndex + themeIndex) mod n`,
      emitted directly and verified for zero collisions — never searched for

**Acceptance**
- [ ] A synthetic 5-themes×15-uniques-into-8-roles×5-axes fixture (mirrors the real 75-into-40
      incident) is refused with the specific bottleneck named
- [ ] A balanced 5-theme fixture's Latin square produces 0 collisions across all 25 (role, theme)
      pairs
- [ ] A feasible-but-locally-starved fixture is caught by layer 2 where layer 1 would pass it

**Verify** *(fill in once built)*

---

#### P2 — Ordering: derive kind-level stages, never hand-label them
`seedsmith/planner/ordering.py`

- [ ] Prerequisite: extend `KindSpec` with `reference_fields: frozenset[str]` per kind (read from
      `KindCatalog.cs`, same source S2 already cites — not re-derived from prose)
- [ ] Build the kind-level reference graph via `corpus.discover_edges` (S1, reused)
- [ ] Kahn's topological sort into layers
- [ ] Tarjan's SCC names a cycle's exact members, never just "cycle detected"

**Acceptance**
- [ ] Reproduces the real historical order on the real corpus — `drop-table` lands after
      `unique`/`base-type`/`set`/`gem`/`charm`/`consumable`
- [ ] A synthetic two-kind cycle fixture is caught and both kinds are named
- [ ] No hand-maintained stage label remains anywhere in the adapter

**Verify** *(fill in once built)*

---

**⭐ CP-F1 — the planner refuses the impossible and orders the possible.**

---

### Phase 2 — Validation, scheduling, and the demand split

#### P3 — Input validation: exemplar gate before dispatch
`seedsmith/planner/validate.py`

- [ ] Every exemplar a work order references is checked via `ExemplarConformance` (S7, reused)
- [ ] A failing exemplar refuses the whole order (exit code 3, per spec-foundation.md §7.3)

**Acceptance**
- [ ] A synthetic exemplar with a missing required field refuses the order, not a partial emit
- [ ] A clean exemplar set passes through untouched

**Verify** *(fill in once built)*

---

#### P4 — Scheduling and work-order output
`seedsmith/planner/schedule.py`

- [ ] List scheduling: layer-by-layer (P2), longest-job-first within a layer
- [ ] Model tier by a small adjustable rule table, not an optimizer
- [ ] Emits the JSON work order per spec-planner.md §6's shape, `closes` naming every `Finding`
      (metric id + subject) a job would clear

**Acceptance** — the known-answer test (spec-planner.md §7)
- [ ] `gems/2` placed after its registry dependency; the three `display-templates/{4,5,6}`
      partitions placed after the affix families they render
- [ ] The four S2-mislabeled base-type partitions are correctly EXCLUDED as generation jobs — they
      need a corpus relabel, not new content, and the planner must not confuse the two

**Verify** *(fill in once built)*

---

#### P5 — Generation pipelines: the declare/fulfil split
`seedsmith/planner/demand.py`

- [ ] Phase A (declare): deterministic per-kind stages emit `Demand` objects — no generation, no
      writes
- [ ] Phase B (fulfil): topological sort of the full demand graph, feasibility check (P1), resolve
      against existing content first — reuse is the default, no structural overlap cap

**Acceptance**
- [ ] A synthetic 3-set-theme fixture with overlapping demand reuses existing base types first and
      requests new ones only for the genuine shortfall, without concentrating demand on a handful
      of base types
- [ ] A recipe fixture proves materials are demanded (and generated) before the recipe consuming
      them, structurally

**Verify** *(fill in once built)*

---

**⭐ CP-F2 — W2 done.**

---

### Phase 3 — `briefkit`

#### P6 — Work order → briefs
`seedsmith/briefkit/`

- [ ] Assembles from: allocation (planner), budget row (target/tolerance/rationale), adapter
      vocabularies **inlined literally, never cited by filename**, planner constraints, metric
      `assertion`/`remedy`
- [ ] Content-addressed: brief hash is a pure function of its inputs, recorded in the job

**Acceptance**
- [ ] A brief for `gems/2` inlines the literal legal `family` vocabulary — grep for a citation
      string like "see tags.v1.json" and fail if found
- [ ] Two brief generations from byte-identical inputs produce the identical content hash
- [ ] A brief whose exemplar failed P3's gate is never emitted

**Verify** *(fill in once built)*

---

**⭐ CP-F3 — briefkit done.**

---

## Part 3 — W3: generation (`pipeline`)

Full rationale and dependency graph: [seedsmith-plan.md](seedsmith-plan.md) Part 3.
`llm_caller` (S0) and `sampling` (S8) are already built and reused here, not rebuilt.

### Phase 4 — `pipeline` generation logic

#### G1 — Pipeline scaffold: schema-per-metric + guardrails
`seedsmith/pipeline/`

- [ ] `Pipeline` dataclass (metric, scope, schema, gate, max_retries, on_persist, model) per
      spec-pipeline.md §2
- [ ] JSON Schema validated locally always; via the model's structured-output mode where supported
- [ ] Narrow scope per call; closed vocabularies inlined (reuses `briefkit`, not reimplemented)
- [ ] **Never a number** — a static test over the schemas themselves rejects any numeric magnitude
      field
- [ ] Validate-before-accept: scratch → gate → move
- [ ] Bounded retry with the exact error attached, then escalate — `llm_caller.call_with_self_heal`
      (S0), generalized from flat string-keyed payloads to arbitrary schema-validated JSON, reused
- [ ] Every schema carries a `blocked` variant with a reason string

**Acceptance**
- [ ] Schema-audit test rejects any pipeline whose schema has a bare numeric field
- [ ] A fixture pipeline against a fake model server (S0's `MockModelServer`, reused) proves
      retry-with-named-defect then escalate-on-persistent-failure, zero real model calls
- [ ] A `blocked` response writes nothing and is reported, not treated as a failure

**Verify** *(fill in once built)*

---

#### G2 — Idempotence and provenance

- [ ] Every generated entry records `_provenance` (pipeline id, model, prompt version, budget
      version, timestamp, finding closed)
- [ ] Re-running checks the finding is already closed (via a `metrics` re-run) before generating

**Acceptance**
- [ ] Running a pipeline twice over unchanged input produces zero new writes the second time
- [ ] Provenance is queryable by finding id

**Verify** *(fill in once built)*

---

#### G3 — Open-loop review queue wiring

- [ ] Wires an open-loop pipeline to `seedsmith/sampling/` (S8, reused) — writes content, marks
      `needsReview`, samples for human review

**Acceptance**
- [ ] An open-loop pipeline's schema never includes a pass/fail field
- [ ] Re-running `metrics` after generation still reports the finding as open-loop, never a silent
      pass

**Verify** *(fill in once built)*

---

**⭐ CP-G — W2+W3 close the loop end-to-end, against a fake model, before any real token is spent.**
`metrics` finds `gems/2` empty → `planner` schedules it (P4) → `briefkit` briefs it (P6) →
`pipeline` (G1, fake model) generates content → `metrics` re-run shows the finding cleared.

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
