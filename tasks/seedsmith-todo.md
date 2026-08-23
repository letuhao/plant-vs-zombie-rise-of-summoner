# Tasks: Seedsmith W1 — measurement

Plan: [seedsmith-plan.md](seedsmith-plan.md) · Map: [../docs/architecture/seedsmith-map.md](../docs/architecture/seedsmith-map.md)

Status: **S0, S1 done (56/56 tests green, CP-A reached).** S2 next. Specs complete and audited
(66 findings, 11 blockers, all closed).

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

Declare the 14 kinds, the dimensions (role, frame, band, element, rarity, class), the legality
function, the registries and the channels. Read from `data/seed/items/_registry/`; transcribe
nothing.

- [ ] 14 `KindSpec`s matching `KindCatalog.cs`
- [ ] `legal_combinations` encodes: `hybridEligible` false for `ward-array`/`jewel-minor-b`;
      uniques barred from `jewel-minor`; commander `standard` role
- [ ] `channels()` returns the 14 primary families with `reference_base` reading `BattleRuleset`
- [ ] Registry versions reported, not assumed

**Acceptance**
- [ ] `seedsmith check --adapter items` loads **1,438 entries across 125 files**
- [ ] `Coverage/EmptyPartition` reports **exactly nine**: `attributes`, four base-type partitions,
      `gems/2`, three display-template partitions — and nothing else
- [ ] `attributes` is flagged as the deferred one, not silently equal to the other eight

**Verify** `python -m seedsmith check --adapter items --metric Coverage/EmptyPartition`

---

**⭐ CP-B — measurement beats memory.** One command rediscovers what took three waves to notice.

---

## Phase 3 — parity and absorption

### S3 — absorb `seed_graph`
- [ ] Port the seven check functions → Linkage + Registration families (nine finding codes)
- [ ] Port its 16 tests
- [ ] `Acquisition` model: specific vs categorical grants both preserved
- [ ] Parity harness: run both against the live corpus, diff finding sets

**Acceptance**
- [ ] Finding sets **byte-identical** to `seed_graph` on the live corpus
- [ ] Parity harness is a test, not a one-off script
- [ ] Categorical grants still resolve — 740 base types are reachable, not falsely orphaned

**Verify** `python -m seedsmith check --adapter items --json out.json && python tools/seedsmith/tests/parity_seed_graph.py out.json`

---

**⭐ CP-C — no regression, plus the cheapest possible test of the tester.**

---

## Phase 4 — the measurement families

### S4 — Coverage family
- [ ] `Coverage/EmptyPartition` (from S1), `Coverage/PairwiseHole`, `Coverage/SlotUncovered`
- [ ] Pairwise t-way over dimension pairs, illegal pairs excluded via `LegalityFn`
- [ ] Fixture with a legal-but-missing pair **and** an illegal pair; only the first is a finding

**Acceptance**
- [ ] Reproduces the wave-2 finding *"top rarity band has zero fire/ice/air/earth uniques"* as
      missing `(band, element)` pairs
- [ ] Zero findings for illegal pairs — the false-positive flood that kills adoption
- [ ] Reports `|seen|/|required|` per dimension pair

### S5 — `numerics` + Balance family
- [ ] The four channel-group formulas, exactly as locked in `bands.v1.json`
- [ ] `tier-bands.v1.json`: `baseShare` 35‰ (provisional), `channelWeight` all 1.0, `opWeight`
- [ ] `ProgressionModel` protocol + `BattleRulesetProgression`
- [ ] `resolve`, `explain`, `rebalance` (diff-then-publish), `solve_base_share`
- [ ] Guardrails: monotonicity, band containment, **`hi_t ≥ lo_(t+1)` — overlap REQUIRED, OD4**,
      largest-remainder closure, integer-only, no silent default for an unshared channel
- [ ] `Balance/LadderInversion` via PAVA; `Balance/OutOfEnvelope`
- [ ] `content_ladder()` returning `None` → Balance reports `NOT_MEASURED`, never a pass

**Acceptance**
- [ ] Reproduces both committed examples: vitality 30‰×680→20, might 45‰×92→4
- [ ] A channel with no authored share **raises**; it does not default
- [ ] Resolving at a hardcoded calibration level raises unless explicitly requested
- [ ] Resolves against the **stub** adapter with no `bands.v1.json` present (proves B2 is fixed)

### S6 — `budget` + Distribution family
- [ ] `budget derive` walks the SSOTs, emits targets with **provenance and conflicts preserved**
- [ ] Conflicted rows block distribution checks and report the conflict instead
- [ ] Structural / stated / proportional derivation, in that preference order
- [ ] Largest-remainder for proportional splits
- [ ] `Distribution/CellDeviation`, `/Evenness` (Pielou **and** richness), `/Inequality` (Gini)
- [ ] All Distribution metrics `gates=False` for W1

**Acceptance**
- [ ] The uniques row shows all three conflicting counts (20 / 300 / 144) with sources
- [ ] Proportional rows carry `"derivation": "proportional"` and wider tolerance
- [ ] Reproduces wave-2's *"humanoid uniques half as common as plant across four roles"*
- [ ] Degenerate cases handled: `S=0`, `S=1`, `e_c=0` → `UnbudgetedCell`, not a division

### S7 — Constraint · ExemplarConformance · SemanticDedup
- [ ] `Constraint`: a manifest of rule → check bindings; a documented rule with **no binding in
      either tool** is the finding. Does not re-implement the five rules C# already enforces.
- [ ] `ExemplarConformance`: every exemplar validates as real content of its kind
- [ ] `SemanticDedup`: exact + canonical + MinHash/LSH near-duplicates
- [ ] Conceptual clustering listed as a **known gap**, blocked on the adjective `axis` addition

**Acceptance**
- [ ] Catches the three historical exemplar defects when replayed as fixtures
- [ ] Would have caught `gem.g1-015` / `consumable.k1-007` both named "Mending Pulse"
- [ ] `seedsmith metrics --coverage` prints the unclaimed Appendix-A row rather than hiding it

### S8 — sampling + Quality
- [ ] `report --sample N --metric X`, **stratified**, seeded from `metric id + corpus revision`
- [ ] `Quality/FlavourMissing` (closed), `Quality/FlavourGeneric` (open)
- [ ] Open-loop metrics emit a review queue and **never** a pass

**Acceptance**
- [ ] Same seed → same sample, across runs
- [ ] Stratification covers every band, not just the largest
- [ ] Reproduces *"60 consumables have no flavour, 30 of 70 charms"*

---

**⭐ CP-D — W1 measurement complete.** Every Appendix-A row claimed or printed as a gap.

---

## Phase 5 — trust and cutover

### S9 — mutation testing over the metric suite
The answer to Appendix A's missing row: *the checker itself was wrong*, eleven incidents — more than
any content defect class, and the one thing pipelines and ordering cannot fix.

- [ ] Mutants under `scripts/mutants/seedsmith-*.json`, matching the repo's existing convention
- [ ] Invert a comparison, drop a guard, widen a regex — one per metric family
- [ ] A survivor needs a written explanation next to the code

**Acceptance**
- [ ] Every metric family has ≥1 mutant its fixtures kill
- [ ] Deliberately re-introducing the inverted `hi_t < lo_(t+1)` guardrail is **killed**
- [ ] `.\scripts\mutate.ps1 -Set seedsmith` runs in CI

### S10 — CI cutover
- [ ] Step 1: parity diff green on the live corpus
- [ ] Step 2: CI runs **both** tools, fails on disagreement — leave for one week
- [ ] Step 3: CI switches to `seedsmith check --gate`; `seed_graph` steps removed
- [ ] Step 4: delete `tools/seed_graph/`
- [ ] `tools/ItemSeedValidator` untouched throughout — it stays the referential gate

**Acceptance**
- [ ] CI green at every step; no step leaves the build red
- [ ] Suite runs inside the 30 s budget
- [ ] `tools/seed_graph/` gone, its 16 tests alive inside seedsmith

---

**⭐ CP-E — W1 done.** Gate armed, old tool retired, W2 unblocked.

---

## Standing rules

- Registry facts are **read**, never transcribed.
- Fixtures are **synthetic**, never the live corpus.
- New metrics ship `gates=False`; promotion is a separate, later act.
- Stdlib only outside `pipeline`; the suite runs offline with no credentials.
- Git stays manual — the owner commits.
