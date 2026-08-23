# Tasks: Seedsmith W1 — measurement

Plan: [seedsmith-plan.md](seedsmith-plan.md) · Map: [../docs/architecture/seedsmith-map.md](../docs/architecture/seedsmith-map.md)

Status: **not started.** Specs complete and audited (66 findings, 11 blockers, all closed).

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
- [ ] `seedsmith check --adapter stub` → clean fixture: `0`; broken fixture: `1`
- [ ] Unreadable corpus → `2`, distinct from `1`, with a message naming the file
- [ ] `Loop.OPEN` + `gates=True` raises at registration
- [ ] A metric whose `needs` are unmet emits `NOT_MEASURED`, never a pass
- [ ] `Finding` carries `schemaVersion`
- [ ] Package is `seedsmith/__main__.py` — no `seedsmith.py` shadowing the package

**Verify** `python -m seedsmith check --adapter stub tests/fixtures/clean && echo OK`

---

**⭐ CP-A — the seam is real.** Stub is the only adapter. Nothing item-shaped exists yet.

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
