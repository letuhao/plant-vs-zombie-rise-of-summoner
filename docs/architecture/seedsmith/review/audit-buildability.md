# Seedsmith — buildability audit (W1)

**Lens:** handed these seven documents and told to build W1 (`corpus`, `adapter-items`, `numerics`,
`budget`, `metrics`, `report`) — what stops that, or has to be invented?

**Method:** read all seven docs plus the code and data they cite: `tools/ItemSeedValidator/*`
(3,206 lines, C#), `tools/seed_graph/*` (corpus.py, checks.py, test_reachability.py — 16 tests),
`data/seed/items/_registry/{core,bands,tags}.v1.json`, and `.github/workflows/ci.yml`. Every finding
below is checked against what the referenced files actually contain, not against the specs' own
description of them.

**Severity:** BLOCKER (cannot start or cannot finish W1 without inventing a design decision the
specs owe), MAJOR (will cause rework or a wrong build if missed), MINOR (costs a question, not a
redo), NOTE (worth recording, not fixing before build).

---

## Summary

| # | Severity | Finding |
|---|---|---|
| B1 | BLOCKER | Core interface types are named, never defined — `Dimension`, `KindSpec`, `Channel`, `LegalityFn`, `RegistrySet` have zero fields anywhere across all seven docs |
| B2 | BLOCKER | `budget` and `numerics` are diagrammed as feature-agnostic (depend only on `corpus`) but their specs are written entirely in item vocabulary with no adapter indirection — P5 and the "strictly downward" claim both fail on inspection |
| B3 | BLOCKER | No CLI is specified anywhere — `seedsmith.py`, its subcommands, argument shapes, config file and exit codes are all invented by inference from prose examples |
| B4 | BLOCKER | `seed_graph` absorption has no CI cutover plan; CI shells the two seed_graph scripts directly today and nothing replaces those steps |
| M1 | MAJOR | No error/exception model for `Corpus.load`, `Metric.run`, or the CLI — the exact failure mode P3/`NOT_MEASURED` exists to prevent (silent success) is unaddressed for a raised exception |
| M2 | MAJOR | The stub adapter's conformance guarantee is narrower than claimed — nothing states it runs through the full metric suite, and `numerics`/`budget` (the highest-coupling modules) can trivially skip it |
| M3 | MAJOR | `budget derive`'s "reads every count already stated in the SSOTs" is prose-extraction dressed as automation — the build cost differs enormously depending on which it actually is |
| M4 | MAJOR | C# `SetRuleCheck` and the absorbed `seed_graph` `set_completability` check the same fields with different definitions of "member" and no reconciliation — the "one clean boundary" claim doesn't hold for sets |
| M5 | MAJOR | "The runner... fails the build if the suite exceeds its budget" (spec-metrics §6) — no document states what that budget is, in seconds |
| M6 | MAJOR | `adapter-items` is scoped as "port what the C# validator already knows" against a 3,206-line codebase with zero decomposition of what ports vs. re-derives, and no anti-drift mechanism between the two copies |
| M7 | MAJOR | `Corpus`'s edge-discovery algorithm and the "minted runtime id" mechanism are described in prose only; no interface exists for an adapter to declare which fields mint ids |
| M8 | MAJOR | No shared fixture/test-support API for the nine-plus metric families each required to ship synthetic red/green fixtures |
| N1 | MINOR | Test-count citation ("71 tests") does not match the repo (63 `[Fact]`/`[Theory]` attributes found) |
| N2 | MINOR | `Finding`'s machine-JSON contract has no schema version, unlike `budget`/`tier-bands`, despite being a cross-wave (W1→W2) contract |
| N3 | MINOR | `seedsmith.py` (script) beside `seedsmith/` (package) is an import-shadowing risk |
| N4 | MINOR | No Python version is pinned for seedsmith; the repo has two Python versions installed |
| NT1 | NOTE | The 516-word `axis` registry addition (analytics §6.3) is real, unscheduled data-authoring work — correctly flagged as "not yet," but has no owner or task anywhere |
| NT2 | NOTE | No `tasks/seedsmith-plan.md` / `-todo.md` exist yet — expected per the map's own "Next," not a defect |

---

## BLOCKER

### B1 — Named interfaces with no fields

`spec-foundation.md` §2 gives:

```python
class SeedAdapter(Protocol):
    def kinds(self) -> list[KindSpec]: ...
    def dimensions(self) -> list[Dimension]: ...
    def legal_combinations(self) -> LegalityFn: ...
    def registries(self) -> RegistrySet: ...
    def channels(self) -> list[Channel]: ...
```

None of `KindSpec`, `Dimension`, `LegalityFn`, `RegistrySet`, `Channel` is defined anywhere in the
seven documents — not a dataclass sketch, not a field list, nothing beyond the one-line comments in
the snippet itself ("kind → directory, namespace, required fields"). Compare this to the actual C#
`RegistrySet` it is meant to replace/wrap (`tools/ItemSeedValidator/Registries/RegistrySet.cs`,
460 lines): role ids, wave-one role ids, role display names, frame ids, rarity ids, category ids,
power-category ids, band enums, power-band tiers, tag axes, theme ids, element ids, and more. An
implementer building `adapter-items` (spec-foundation §6 step 2) has to invent this shape from
scratch, then hope it matches what `metrics`, `budget` and `briefkit` expect to receive — three
modules specced by different authors, none of whom saw a `RegistrySet` field list either.

Same problem, smaller: `Metric.needs: Needs` (spec-metrics.md §1) is typed but `Needs` is never
shown as an enum or flag set — is it `{corpus, budget, numerics, adapter}` as a `set[str]`, a
bitmask, four booleans? The runner (§6) has to branch on it to decide `NotMeasured`.

**What an implementer needs:** one page per interface type with concrete fields, before `corpus`
or `adapter-items` can be started (spec-foundation §6 step 1 is literally "the seam, proven before
anything depends on it" — it cannot be proven if the seam's own data shapes are undefined).

### B2 — `budget` and `numerics` are not the feature-agnostic modules the map claims

The map's dependency table (§3) lists both modules as depending on `corpus` only, and draws them as
peers of `adapter-items`, not consumers of it:

```
corpus ─┬─ numerics ─┐
        ├─ budget ───┼─ metrics ─┬─ report
        └─ adapter-items ────────┴─ planner ── briefkit ── pipeline
```

P5 (map §2) states plainly: *"The core knows about corpora, budgets, metrics, findings and plans.
It knows nothing about roles, frames, rung bands or drop tables. Everything item-shaped lives in
`adapter-items`."* `budget` and `numerics` are both listed as core.

Checking that claim against what the two specs actually contain:

- `spec-budget.md` §1's motivating table is about **uniques**; §4.3 ("Proportional") derives base-type
  targets by splitting a total **across roles** using **`budgetWeightMilli`** — a field that lives in
  `core.v1.json roles.list`, i.e. inside the item registry. §5's worked example dimension is
  `"unique × rungBand"`. Every substantive example in the document is item vocabulary, used directly,
  with no adapter call in between.
- `spec-numerics.md` is, start to finish, item math: the 14 `stat.modify` primary-channel names
  (vitality, fortitude, bulwark, might, ...), `bands.v1.json`'s `tierScaling` constants, the AE
  premium for uniques and sets. It resolves values by reading `bands.v1.json` / `core.v1.json`
  directly (confirmed present at `data/seed/items/_registry/bands.v1.json`, `core.v1.json`) — not
  through `adapter.channels()`.

But `SeedAdapter.channels()` exists specifically, per its own comment, "for numerics; empty if the
feature has no magnitudes" — meaning the protocol's author *did* intend numerics to go through the
adapter. The spec for `numerics` never does. Two documents in the same set disagree about whether
`numerics` is generic-over-`channels()` or item-specific, and nothing resolves it.

This is not a naming nitpick — it changes what gets built:

- If `numerics`/`budget` are meant to be generic (as the map's module table and P5 both claim), the
  specs as written are wrong and need a rewrite that routes every item-specific constant through
  `adapter.dimensions()` / `adapter.channels()` / `adapter.registries()`.
- If they are meant to be item-specific in practice (as the specs are actually written), the map's
  dependency table and module list are wrong, decision 3's stub-adapter test proves nothing about
  them (see M2), and they should not be presented as reusable core modules a second feature would
  share.

**An implementer cannot start `numerics` or `budget` without the owner picking one of these.**

### B3 — No CLI specification

The map (§8), spec-foundation (§3, §5) and spec-metrics (§4, §5) between them mention, in prose:

- `seedsmith.py` as "entry point" (layout tree, spec-foundation §5)
- `--sample N --metric X` (spec-foundation §3, spec-analytics §8)
- `seedsmith metrics --coverage` (spec-metrics §5)
- `budget derive`, `budget diff v1 v2` (spec-budget §2, §6)
- `rebalance(...).publish(version=...)` as a Python call, not a CLI verb (spec-numerics §3.2)

Nowhere is there a single page describing: the actual subcommand tree, whether these are one
`seedsmith` executable with subcommands (`seedsmith budget derive`, `seedsmith metrics run`) or
separate scripts per module, how configuration (corpus path, registry path, concurrency cap for the
later planner, sample seed) is supplied — flags, env vars, a config file — and what the process exit
codes mean beyond "CI gate: exit non-zero on GAP" (spec-foundation §3). `report` is the module the
map calls "standalone value" and the human-facing surface of the whole W1 deliverable, and it has no
interface spec at all beyond a paragraph of prose per consumer.

**What an implementer needs:** an actual CLI contract — command tree, flags, config schema, exit
codes — the same level of specificity every other module gets for its data shapes.

### B4 — `seed_graph` absorption has no CI plan, and CI runs it directly today

Verified in `.github/workflows/ci.yml` (lines 51–59):

```yaml
- name: Item seed reachability
  shell: pwsh
  run: |
    python tools/seed_graph/test_reachability.py
    if ($LASTEXITCODE -ne 0) { throw "reachability checks failed their own tests" }
    python tools/seed_graph/check_reachability.py
    if ($LASTEXITCODE -ne 0) { throw "item seed corpus has reachability gaps" }
```

Map §3 and §4 state `seed_graph` "is absorbed, not kept alongside... its `Corpus`, `Acquisition`,
`Finding` and check registry become the first cut of `corpus` and `metrics`," and spec-foundation §5
says "the directory is removed rather than left to rot beside its replacement." No document:

- names the replacement CI step (what does `report`'s CLI print, and what's its exit-code contract,
  for CI to call in place of the two lines above — see B3, which this depends on),
- states whether there is a parity period (both tools run in CI until the new one is trusted) or a
  hard cutover the same day `metrics` ships,
- addresses that `check_reachability.py`'s CI comment ("Armed 2026-08-23, wave R4: the enrichment
  waves closed all 35 gaps, so a new one is a regression rather than a known debt") is itself a piece
  of state (a gating decision, made on a specific date) that has to be preserved or consciously
  re-decided when the check moves into `metrics`'s Linkage family.

Deleting `tools/seed_graph` per the plan, without touching `ci.yml`, breaks the build outright. This
has to be sequenced explicitly, and nothing in W1's build order (spec-foundation §6) mentions it.

---

## MAJOR

### M1 — No error/exception model

`Corpus.load(path)` is specced as "pure: no network, no database, no mutation" (spec-foundation §1)
but nothing states what happens on a malformed JSON file, a duplicate id, an entry with no `kind`,
or a partition string that doesn't match any allocated namespace. The seed_graph Python loader it
replaces is silently permissive (`corpus.py`: `if not kind: continue`), which is a defensible choice
for a throwaway checker but an odd one to inherit silently into the tool whose entire premise is "an
absent check must never read as a healthy one" (map §2, P3).

Same gap one level up: `Metric.run(self, ctx) -> list[Finding]` (spec-metrics §1) has no documented
behavior for a metric that raises. Does the runner catch it and emit `NOT_MEASURED` for that metric
alone (consistent with P3's own logic), or does an uncaught exception abort the whole `report` run
and print a stack trace? The second option is exactly the "invisible check" failure mode the module
exists to eliminate, just moved into the runner instead of the corpus. This needs one paragraph
before `report`'s runner can be written with any confidence it matches what `metrics` will throw.

### M2 — The stub adapter proves less than decision 3 claims

Map decision 3: *"the core cannot quietly reach into item concepts if a second, fake adapter
compiles and passes... it fails loudly the moment the interface leaks."* Spec-foundation §2:
"conformance is tested against a stub adapter... if the core reaches into item concepts, the stub
stops passing."

That guarantee only holds if the stub adapter is run through the **same code paths** real item
content goes through — specifically, the full `metrics` suite, not just `corpus.load` +
`adapter` protocol conformance. No document states that the stub corpus is fed through every metric
family in CI. Without that, a metric author can write `if channel == "vitality":` inside a
nominally-generic Balance metric and the stub adapter — which is never actually asked to run that
metric — will never catch it.

It's also, per B2, moot for the two modules with the deepest actual coupling: `SeedAdapter.channels()`
is documented as "empty if the feature has no magnitudes" (spec-foundation §2), so a stub with
`channels() -> []` sidesteps `numerics` and most of `budget` entirely rather than exercising them.
The modules most saturated with item vocabulary are exactly the modules the stub is least likely to
touch. If the stub is meant to prove `numerics`/`budget` are generic too, it needs at least one
non-empty channel and one distribution-shaped budget row of its own — which is more than "a tiny
invented feature with two kinds and two dimensions" (map §7.3) currently describes.

### M3 — `budget derive`'s "automation" is unspecified prose-extraction

Map decision 2 and spec-budget §2 both describe `budget derive` as: *"A script reads every count
already stated in the SSOTs and the fleet plan, emits `budget.json`, and marks each conflict
inline."* The worked example (spec-budget §2) cites `ssot-uniques.md §5.33` (states "20"),
`authoring-fleet-plan.md §2` (states "300"), and the corpus itself (144, counted).

The corpus count is genuinely automatable — walk `data/seed/*` and count. The other two are numbers
stated in prose inside markdown documents, at specific section numbers, with specific surrounding
qualifiers ("5 per rung band," "20 agents × 15"). Extracting "the number a markdown section states"
programmatically, for every one of the ~126 partitions the map mentions, is not a solved problem —
it's either (a) a one-time, hand-curated table of `{source, section, value}` triples checked into
`budget`'s source and merely re-validated by script each run, or (b) an actual markdown-parsing
heuristic that guesses which number in a paragraph is "the" target, which will misfire constantly
and produce wrong conflicts.

These have very different implementation costs (a data-authoring pass vs. an NLP-adjacent parser),
and the spec's phrasing ("a script reads every count") reads as (b) while the actual practical
approach is almost certainly (a). Say which one is intended before someone builds the wrong one.

### M4 — Two tools check the same `set` fields with different definitions, unreconciled

`tools/ItemSeedValidator/Checks/SetRuleCheck.cs` (in CI today) checks, among other things:

```csharp
var roles = members.OfType<JsonObject>()
    .Select(m => m["role"]?.GetValue<string>())
    ...
if (steps.Count > 0 && steps[^1] > members.Count)
    ctx.Error(entry, "SetThresholdUnreachable", ...);
```

— i.e. it compares the top threshold against **every declared member**, regardless of whether that
member names a base type.

`tools/seed_graph/seedgraph/checks.py`'s `set_completability` (slated for absorption into `metrics`,
map §4) instead separates members into `pinned` (name an actual base type) vs. not, and fires
`SetUncompletable` when there are members but none pinned, or `SetShortOfThreshold` when
`top > len(pinned)` — i.e. it compares the top threshold against **only the members that actually
count toward completion**.

These are different questions asked of the same fields (`set.members`, `set.thresholds`), and they
can disagree: a set with 6 declared members, none pinned to a base type, and a top threshold of 6
passes the C# check (`6 > 6` is false) while failing the Python check (`SetUncompletable`, because
zero are pinned). The map's §4 boundary claim — *"Two tools, two questions, one clean boundary"* —
is accurate for referential integrity vs. reachability in general, but not precisely true for `set`,
where both tools independently encode overlapping completeness logic. Nothing states which verdict
wins, or whether `metrics`'s absorbed check should be tightened to also flag what C#'s
`SetThresholdUnreachable` catches (an all-pinned set that's still short), which the Python version as
written does not check for the `pinned == members` case with `top > len(pinned)` — actually it does,
via the same condition, but the *unpinned* case is where the two diverge, and that divergence isn't
called out anywhere.

### M5 — "The suite's budget" is never a number

spec-metrics.md §6: *"Cost is bounded by analytics §10; the runner records per-metric wall time and
fails the build if the suite exceeds its budget, because a check nobody can afford to run is a check
that gets skipped."* Analytics §10 ("Complexity budget") is a Big-O table — `O(n log n)`,
`O(n · d²)`, etc. — not a wall-clock number. No document states the actual CI time budget (30
seconds? 5 minutes?) that "the suite exceeds its budget" is measured against. An implementer has
Big-O reasoning to lean on but no threshold to put in the runner's `if wall_time > BUDGET:` check
that spec-metrics §6 explicitly requires exist.

### M6 — `adapter-items`'s port scope is one bullet against 3,206 lines of C#

spec-foundation §6, step 2: *"`adapter-items` — port what the C# validator and `seed_graph` already
know."* The C# side alone (`tools/ItemSeedValidator/Registries/*.cs` + the nine `Checks/*.cs`
classes) is 3,206 lines across `RegistrySet.cs` (460), `NamespaceAllocation.cs` (292), `KindCatalog.cs`
(162), and the check classes (80–368 lines each). Nothing decomposes this into "port this data
loading, re-derive this rule, leave this one in C# because it's referential-only" — the single
sentence is the entire task description for what is, by line count, the largest module in W1.

There's also no anti-drift mechanism specified. Both the C# validator (staying, per map §4) and the
new Python `adapter-items` will independently load the same registry JSON and encode overlapping
business rules — confirmed concretely: `tools/seed_graph/seedgraph/checks.py` already hardcodes
`NON_HYBRID_ROLES = frozenset({"ward-array", "jewel-minor-b"})` with a comment explaining it *should*
come from `core.v1.json roles.list`'s `hybridEligible` field but doesn't, "because this check must
keep working on the synthetic corpora the tests build, which ship no registry." That's a real,
already-committed instance of the exact drift risk this finding describes, and `adapter-items`
inherits it unless the port explicitly fixes it.

### M7 — Edge discovery and minted-id registration are prose, not interfaces

spec-foundation §1: *"Edges are discovered, not declared. Any string matching an allocated id
namespace is an edge."* No algorithm is given — is this a regex over the id grammar, a
namespace-prefix trie built from `adapter.kinds()`'s declared namespaces, or (worst case) testing
every string-valued field against every known id in `by_id`? At ~1,500 entries today the naive
approach is survivable; the module's own scale target is ~30,000 rows (map §1, analytics §10), where
an O(entries × string-fields × known-ids) approach stops being trivial. The spec needs to say which
approach it means, because the three differ by orders of magnitude in both correctness (prefix
matching can false-positive on the wrong kind) and cost.

Related, same section: *"A milestone mints `atom.enhance-vigor`; a base type points at it; `by_id`
will never contain it... the graph carries both and resolution consults both."* This is presented as
already solved, but no method on `SeedAdapter` (§2's five methods) declares which fields on which
kinds mint runtime ids, or what pattern a minted id follows. Without that, `corpus` cannot actually
implement the "carries both" resolution it promises — it has no way to know a given string is a
*minted* id rather than an unresolved reference to a missing entry, which is precisely the
tracking-id-vs-runtime-id confusion the spec cites as having caused four defects (map Appendix A #6).

### M8 — No shared fixture/test-support API

spec-metrics §6 requires, per metric: "one fixture that must trip it, one that must not, and — for
CLOSED metrics — a fixture proving the assertion flips true when the defect is fixed... synthetic,
never the live corpus." Across ten families (spec-metrics §3) and the C#/seed_graph checks being
absorbed, that's dozens of small synthetic corpora. `tools/seed_graph/test_reachability.py` already
solved a version of this problem for its own 16 tests with local helpers (`build(*specs)`,
`drop_table(*entries)`, etc.) — but nothing in the seven documents says whether those helpers get
promoted into a shared `corpus`-level test-support module that every metric's fixtures build on, or
whether each metric family reinvents synthetic-corpus construction independently. Given `corpus` is
explicitly the shared seam (spec-foundation §1), its test-support helpers are the obvious place for
this, but it isn't named as a deliverable anywhere in the W1 build order.

---

## MINOR

### N1 — Test-count citation doesn't match the repo

Map §4 and spec-foundation both state, in the boundaries section, that the C# validator has "71
tests, wired to CI." Counting `[Fact]`/`[Theory]` attributes in
`tests/FusionRpg.ItemSeedValidator.Tests/*.cs` today gives 63 (plus 10 `InlineData` rows across two
`Theory` methods, so the actual executed-test count is neither 63 nor cleanly 71). Not load-bearing
for the build, but worth a quick `dotnet test --list-tests` before the number is quoted again in a
document someone will cite.

### N2 — `Finding`'s JSON contract has no version field

`budget.v{n}.json` and `tier-bands.v{n}.json` both get explicit versioning with a stated discipline
("a target change is a deliberate, reviewable, revertible act," spec-budget §6). The `Finding`
dataclass (spec-metrics §2) — which is the entire interface `planner` (W2) will be built against —
has no equivalent `schemaVersion`. Adding one now costs nothing; discovering the need for one after
W2 is specced against the current shape costs a renegotiation across two waves.

### N3 — `seedsmith.py` next to `seedsmith/` is an import-shadowing risk

spec-foundation §5's layout:

```
tools/seedsmith/
  seedsmith/
    corpus/ ...
  tests/
  seedsmith.py     entry point
```

A top-level script named `seedsmith.py` sitting beside a package directory also named `seedsmith/`
is a classic Python footgun: depending on how the script is invoked and what's on `sys.path`, `import
seedsmith` from inside `seedsmith.py` (or from a test file) can resolve to the script itself rather
than the package, especially before the package is installed. Rename the entry script (`cli.py`,
`__main__.py` invoked via `python -m seedsmith`) or make `seedsmith.py` a thin `if __name__ ==
"__main__"` shim living outside the directory it names.

### N4 — No Python version stated for seedsmith

The environment this audit ran in has both Python 3.10.11 and 3.13.12 installed, and
`tools/seed_graph`'s committed `__pycache__` is `cpython-313`-compiled. `tools/seed_graph/README.md`
at least states "Python 3.9+, no third-party packages." None of the seven seedsmith documents states
a minimum or CI-pinned Python version. "Stdlib only" narrows the risk but doesn't remove it —
`Protocol` needs 3.8+, structural pattern matching (if used) needs 3.10+, and nothing says which
floor the module targets.

---

## NOTE

### NT1 — The 516-word `axis` field is real, unscheduled work

Analytics §6.3 is candid that SemanticDedup's conceptual-clustering metric is blocked on adding an
`axis` field to 516 canonical adjective entries, and correctly says it "does not ship" until that
lands rather than shipping against the wrong grouping. That's the right call, and W1's acceptance
criterion (spec-foundation §6: "every metric family... implemented or explicitly listed as not-yet")
covers it. But the 516-word tagging pass is itself a bounded, real, one-time authoring job with a
registry-version bump attached (analytics §6.3's own words: "a registry addition, so it needs an
owner bump") — and it appears in no build order, task list, or owner assignment anywhere across the
map or any spec. Worth a line in whichever task list picks this up, so it doesn't fall through
between "not W1's problem" and "somebody's problem."

### NT2 — No task breakdown exists yet

`tasks/seedsmith-plan.md` and `tasks/seedsmith-todo.md` don't exist in the repo yet. The map's own
closing line says this is next ("Next: `tasks/seedsmith-plan.md` and `tasks/seedsmith-todo.md`, then
build W1"), so this isn't a gap in the specs — it's the expected state before that step runs. Noting
it only so this audit isn't read as silent about task planning: right now, spec-foundation §6's
six-step list is the only sequencing that exists, and B1–B4 above are exactly the decisions that list
doesn't yet account for.

---

## What holds up well, for balance

Worth recording since a buildability review that only lists problems reads as harsher than the work
deserves:

- The locked numerics constants (spec-numerics §1) are verified, not asserted — checked directly
  against `data/seed/items/_registry/bands.v1.json`, and the worked examples (vitality, might)
  compute correctly from the file's own `tierScaling` block.
- `seed-contract.md`, `ssot-uniques.md`, `ssot-sets.md`, `entry-shapes.md`, `authoring-fleet-plan.md`
  — every SSOT document cited by name across the seven specs exists at the path cited.
- The C#/Python boundary is real for its stated purpose (referential integrity vs. reachability) even
  though M4 finds one seam (`set` completeness) where it blurs.
- `seed_graph`'s 16 tests and its `Corpus`/`Acquisition` model are exactly as described, and are a
  sound starting point for `corpus`'s first cut, per spec-foundation §5.
- The three artifacts multiple specs flagged as "does not exist yet" (tier-bands, budget.json, the
  `axis` field) are consistently and honestly marked as not-yet across every document that touches
  them — no spec quietly assumes one is already there.
