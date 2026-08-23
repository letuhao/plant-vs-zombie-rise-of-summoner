# Seedsmith spec set — adversarial audit: factual grounding

**Scope of this pass:** does every claim the specs make about the existing repo match the actual
files? Not checking arithmetic, buildability, or game-design taste — other passes in this folder
own those. Read: `seedsmith-map.md`, all seven files under `seedsmith/` (including
`spec-metrics.md`, which exists but is not listed in the map's own §8 spec index), and verified
every cited constant, section number, test count and code claim against the real file it names.

**Read this note before the findings.** Most of what these specs assert about locked registry
constants is correct, often to the exact character — the `bands.v1.json` table in
`spec-numerics.md` §1 checks out line for line. The failures cluster in two places instead:
**section citations that point at the wrong heading or a heading that does not exist**, and **one
claim about what the shipped C# validator does not enforce, which is simply false** and undercuts
part of the map's own justification for building a new module.

---

## 1. The map's own founding evidence is internally inconsistent about a number it uses twice

**Severity: BLOCKER** — `seedsmith-map.md` lines 17-18, 90, 146, 162, 198; `spec-analytics.md`
line 103; `spec-foundation.md` lines 134, 138; `spec-metrics.md` lines 74, 134;
`spec-planner.md` lines 126-127

The map's §1 "Why, in one paragraph of evidence" — the passage that motivates the whole
`Coverage` metric family — states:

> "while nine of its 126 allocated partitions were **empty**, and nobody noticed for three waves"

Appendix A row 13 repeats **nine**: *"Allocated partition with zero entries — nine of them,
unnoticed for three waves."* §6's metric-catalogue table also says **nine** ("would have caught
the nine empty partitions on day one"). `spec-analytics.md` §2.1 and `spec-metrics.md` §1/§5 all
inherit **nine**.

But the map's own §7 decision 4 says:

> "**The 8 empty partitions** → left open, as seedsmith's first work order. Known-answer
> end-to-end test: `metrics` must find exactly those eight…"

`spec-planner.md` §7 and `spec-foundation.md` §6 both cite this decision and both say **eight**
("The eight empty partitions are deliberately left open (map §7.4)"; "finds the eight known-empty
partitions and nothing spurious").

Two module specs (`planner`, `foundation`) build their stated acceptance test — a hard pass/fail
gate for W1 — around a number (eight) that contradicts the map's own headline claim (nine) and its
own Appendix A. This is not a rounding-off-by-one in prose; it is the literal count a known-answer
test must match exactly, cited twice each way from the same source document.

**No document anywhere in the repo outside the seedsmith folder mentions "empty partitions" at
all** — not `build-log.md`, not `authoring-fleet-plan.md`, not any `ssot-*.md`. A repo-wide search
for "empty partition", "zero entries" (partition-scoped), and "zero-entry" turns up nothing outside
`docs/architecture/seedsmith*`. So the number that anchors the map's whole motivating paragraph is
not independently checkable against any other artifact in this repository — it is asserted, and
then the assertion disagrees with itself.

There is no "eight, after excluding some category such as attributes" reconciling language
anywhere in the map or any spec. The two numbers simply both appear, unreconciled, treated as
interchangeable by different specs.

**What should have happened:** either fix the map to state one number consistently and cite where
it came from (build-log.md line-range, a script's output file, whatever it actually was), or if the
map's §7.4 deliberately narrowed nine down to eight for a stated reason, that reason belongs in the
same sentence, not left to be inferred.

---

## 2. `spec-foundation.md`'s claim about `tools/seed_graph`'s check count is wrong

**Severity: MAJOR** — `spec-foundation.md` lines 123, 134; actual: `tools/seed_graph/seedgraph/checks.py` lines 43-55

> "`tools/seed_graph` is absorbed: `Corpus`, `Acquisition`, `Finding` and **the nine existing
> checks** move in with their 16 tests" (line 123)
> "`metrics` — absorb **the nine existing checks**…" (line 134)

`checks.py`'s own `run_all` (lines 43-55) iterates exactly seven check functions:
`set_completability`, `unobtainable_content`, `socket_word_ingredients`, `recipe_inputs`,
`enhancement_track_bound`, `equipment_slot_coverage`, `dead_end_materials`. That is **seven**, not
nine.

The **16 tests** half of the same sentence is correct — `test_reachability.py` has exactly 16
`def test_...` methods.

The likely source of the error: `tools/ItemSeedValidator/Checks/` (the unrelated C# validator)
holds exactly **nine** `.cs` check files (`DropTableCheck.cs`, `IdentityCheck.cs`, `LintCheck.cs`,
`NamingCheck.cs`, `OwnershipCheck.cs`, `ReferenceCheck.cs`, `SetRuleCheck.cs`,
`StructuralCheck.cs`, `UniqueRuleCheck.cs`) — `audit-buildability.md` line 280 independently cites
this correctly as "the nine `Checks/*.cs`". `spec-foundation.md` appears to have carried that
number over onto the wrong tool.

The map itself (`seedsmith-map.md` §5, "Boundaries") never states a check count for
`seed_graph` — only the spec does, and only there is it wrong.

---

## 3. A claim central to the map's justification for the `Constraint` family is false: the cited rules are already enforced in shipped, tested C# code

**Severity: BLOCKER** — `seedsmith-map.md` Appendix A row 7 (line 202); `spec-metrics.md` lines
95-100; actual: `tools/ItemSeedValidator/Checks/UniqueRuleCheck.cs` (all), `SetRuleCheck.cs` lines
1-17, `tools/ItemSeedValidator/Validator.cs` lines 70-71

Appendix A row 7 states, as one of the twenty defect classes and the reason `metrics` needs a
`Constraint` family at all:

> "Content rules that live only in a lane document, never enforced (**jewel-minor ban**,
> **8-of-15 role quota**, **one-per-(role,band,axis)**) | Constraint | seedsmith"

`spec-metrics.md` §3 repeats the same three examples plus two more (the 6-role set cap, the
hybrid-core requirement) as illustrating "the family with … the highest historical defect count …
Every one lived only in prose."

This is false for four of the five named examples. Reading `UniqueRuleCheck.cs` (wired into the
main validator run at `Validator.cs:70`, and exercised by tests in
`tests/FusionRpg.ItemSeedValidator.Tests/ContractCorrectionTests.cs`):

- Line 62-65: rejects a unique on either `jewel-minor` role → `UniqueRoleForbidden` — **the
  jewel-minor ban, enforced.**
- Lines 69-86: rejects two uniques sharing `(rung band, role, power axis)` → `UniqueAxisCollision`
  — **the one-per-(role,band,axis) rule, enforced.**
- Lines 97-109: rejects a unique on a role outside the allocated eight →
  `UniqueRoleQuota` — **the 8-of-15 role quota, enforced.**

And `SetRuleCheck.cs` (wired at `Validator.cs:71`), `const int MaxRolesPerSet = 6`, enforces
**the 6-role set cap.**

Only the fifth example — a "hybrid-core requirement" — has no matching check anywhere under
`tools/ItemSeedValidator/Checks/` (confirmed by grep for "hybrid" across that directory), so that
one example is accurately unenforced.

`UniqueRuleCheck.cs`'s own header comment explains why this matters for the audit: it describes
the exact incident the map is drawing on ("nothing mechanical enforced them and eighteen
partitions authored against eighteen readings … the difference between a rule that is written down
and a rule that is checked") — but that comment is describing a defect that **was already fixed**,
not a live gap. The map is citing a *historical* incident as though it were a *current* one, and
using it to justify new work that, for four of its five examples, has already shipped.

**Consequence for the spec set:** this doesn't mean the `Constraint` family is unnecessary — the
hybrid-core example still stands, and a metric family that watches for *future* prose-only rules
has independent value. But the map's own evidentiary paragraph for this family overstates the gap
by counting four already-closed incidents as open ones, and no seedsmith spec notes that the C#
side already covers most of row 7's named examples. `budget`/`metrics` design should account for
"already enforced elsewhere" as a legitimate `NOT_MEASURED`-adjacent state so seedsmith doesn't
re-implement a check the C# validator already runs in CI.

---

## 4. `ssot-uniques.md §5.33` does not exist; the 20/144 figures and the "1.5 AE" premium sit in different sections than cited

**Severity: MAJOR** — `spec-budget.md` lines 16, 44; `spec-numerics.md` line 28;
`seedsmith-map.md` line 140; actual: `docs/architecture/item/ssot-uniques.md` §4.6 and §3.7

`spec-budget.md` §1's table cites the unique-count conflict as:

> `ssot-uniques.md §5.33 | 20 (5 per rung band) | superseded, banner added`

`ssot-uniques.md` has no §5.33 — its heading numbering runs `## 5. Data shape`, with subsections
`5.1`–`5.4`; nothing under `5` goes past `5.4`. The "20 uniques, 5 per rung band, superseded by the
owner's 144 decision" content the citation is trying to point at actually lives in **§4.6 "How many,
and what one costs"** (line 499 onward), which itself carries the exact superseded banner
(line 525): *"Superseded 2026-08-23 by owner decision — the shipped count is 144, not 20."*

This is not a fresh error invented by `spec-budget.md` — the identical wrong citation,
`§5.33`, already appears in `build-log.md` line 598 ("Below §5.33's 'roughly 200 rows'"), and the
map itself uses it at line 140 ("20 (ssot §5.33) vs 300 (fleet plan) vs 144 (shipped)"). So the
error is repo-wide and predates the seedsmith spec set; `spec-budget.md` faithfully propagated an
existing bad citation rather than inventing a new one. It is still wrong, and `budget`'s whole
pitch (§2's worked example) is to be the place that stops this kind of drift — so a `budget.v1.json`
built by literally deriving from this citation would embed a dead section reference.

**The 300 figure is correctly cited.** `spec-budget.md`'s row for `authoring-fleet-plan.md §2`
checks out: line 55 of that file ("G1 uniques | 300 hand-authored items | W1 · 20 agents") and line
147 ("Uniques — by theme | 20 | ~15 uniques") both sit inside `## 2. Coverage audit`, and 20 × 15 =
300 matches the "(20 agents × 15)" gloss in `spec-budget.md` line 17.

**The 144 figure is correct and verified against the actual corpus**, not just the doc's own claim
— `data/seed/items/uniques/` holds 18 partition files with exactly 144 `"id": "unique...."` entries
counted directly.

**`spec-numerics.md`'s "1.5 AE" row cites the wrong section for the unique half.** Line 28:

> `Unique / set-member premium | 1.5 AE each | ssot-uniques §3.5, ssot-sets §3.5`

`ssot-sets.md §3.5` is correct — it is titled "Set jail — what actually prevents it" and states
(line 190) *"Sum of all a set's tier atoms ≤ **1.5 AE per member piece**."* But `ssot-uniques.md
§3.5` is "THE RULE-BREAKING LADDER", a table about what a unique may override structurally; it does
not contain the "1.5 AE" premium at all. That figure — *"A unique's total value ≤ the rung's rolled
baseline + 1.5 AE"* — is in **§3.7 "THE MUTUAL-RELEVANCE MECHANISM", Device 2** (line 233 area).
Citing `§3.5` for the uniques premium points a reader at the wrong section of the right document.

**The "1 AE" definition citation is exact and verified correct**, character for character:
`spec-numerics.md` line 29 cites `ssot-sets.md:187`, and line 187 of that file reads *"one AE is one
rolled affix at the middle of the set's tier window"* — matching the spec's paraphrase precisely.

---

## 5. `spec-analytics.md` §6.3's denominator is wrong: 1,245 canonical entries, not 1,293

**Severity: MAJOR** — `spec-analytics.md` line 230; actual: `data/seed/items/_registry/words.v1.json` line 12179

> "**Scope: 516 words.** Of **1,293** canonical entries, 516 carry an adjective surface form…"

`words.v1.json`'s own `selfCheck` block (line 12179) states `"totalCanonicalIds": 1245`. Summing
the file's own per-group breakdown at lines 12187-12193 (`nounPools: 515 + classRungAdjectivePools:
288 + themeAdjectivePools: 156 + conceptPools: 156 + uniqueSetSeedPools: 130`) gives 1,245,
confirming the file's self-check rather than a typo in it.

**516 is correct** — a direct count of `"adjective"` keys in the file returns exactly 516, matching
the spec's numerator. Only the denominator is wrong: the true proportion is 516/1,245 ≈ 41.4%, not
516/1,293 ≈ 39.9%. The qualitative conclusion (the axis field is genuinely missing on a bounded,
findable set of entries) is unaffected, but a reader who cites "1,293 canonical entries" downstream
will be citing a number that does not exist anywhere in the registry it is supposedly drawn from.

The rest of §6.3's grounding is solid and directly verified: `classRungAdjectivePools`
`["armour.humanoid.plate"]` really does hold `soldered`, `rigid`, `ponderous`, `beaten` (confirmed
at `words.v1.json` lines 5221-5253, plus further entries the spec doesn't quote), and the pool
really is grouped by "where a word may be used" — construction/stiffness/weight/damage-state mixed
in one pool, not by concept — exactly as claimed.

---

## 6. `spec-numerics.md`'s `primaryChannel` formula is presented as the registry's formula but is not a verbatim quote

**Severity: MINOR** — `spec-numerics.md` lines 34-39 vs. `bands.v1.json` line 107

The spec writes:

```
m1   = round_legible(sharePermille × referenceBaseGameUnits(20) / 1000)
m_t  = round_legible(m1 × 1750^(t-1) / 1000^(t-1))
lo_t = round_legible(670  × m_t / 1000)
hi_t = round_legible(1330 × m_t / 1000)
```

`bands.v1.json`'s actual `formula` string (line 107) is parametric — `referenceLevel`,
`ratioPerMille`, `bandFloorPerMille`, `bandCeilingPerMille` as named fields, not the literal
numbers `20`/`1750`/`670`/`1330`. The spec substitutes the current locked constants into the
formula rather than quoting the registry's own string. The substitution is arithmetically faithful
— every constant used (20, 1750, 670, 1330) matches the registry's locked values exactly, verified
in finding-adjacent checks below — so nothing here is *wrong*. But it is not what §1's framing
implies ("Almost all of them already exist, locked, in `bands.v1.json`" immediately followed by
this block) — a reader who diffs the block against the registry text will find no matching string,
because none was quoted. Worth a one-line disclaimer ("constants substituted for the current v1
values") so the next reader does not go looking for this exact string in the JSON and conclude it
was invented.

The two worked-example checks the spec cites do verify: vitality 30‰ × 680 ÷ 1000 → t1 mid 20
(registry's own worked example agrees, `bands.v1.json` lines 118-124), and might 45‰ × 92 ÷ 1000 →
t1 mid 4 (registry lines 162-168, with the same "below the 5-unit legibility floor" note the spec
implicitly relies on).

---

## 7. `referenceBaseGameUnits` code citation is off by one line (inherited from the registry, not introduced here)

**Severity: MINOR** — `spec-numerics.md` lines 44-46; actual:
`src/FusionRpg.Core/Battle/BattleModels.cs` lines 61-62

> "`BattleRuleset.BaseHp(20)` = 680, `BattleRuleset.BaseAtk(20)` = 92
> (`src/FusionRpg.Core/Battle/BattleModels.cs:60-61`)"

In the actual file, `BaseHp` is declared at **line 61** (`public static int BaseHp(int level) =>
80 + 30 * level;`) and `BaseAtk` at **line 62** — one line later than cited in each case. The
values themselves are exactly right: `BaseHp(20) = 80 + 30×20 = 680`; `BaseAtk(20) = 12 + 4×20 =
92`.

This is not a fresh error: `bands.v1.json`'s own `workedExamples` (lines 115, 159) cite the same
off-by-one line numbers (`:60` for `BaseHp`, `:61` for `BaseAtk`) — `spec-numerics.md` just
compressed the registry's two separate (and already wrong by one) citations into a single
"60-61" range. Low severity because the values that actually matter for the arithmetic are
correct and the file+method name resolve unambiguously; still, a line-number citation that sends a
reviewer to the wrong line is exactly the kind of thing a "read before proposing" culture should
catch, and it's been sitting in the frozen registry since 2026-08-22 without correction.

---

## 8. Confirmed accurate, verified directly against source (no action needed)

Listed so the next reader does not re-check these:

- **`bands.v1.json` locked constants** (`spec-numerics.md` §1 table) — `r = 1.75`
  (`magnitudeRatioPerMille: 1750`), duration `r = 1.4` (`durationRatioPerMille: 1400`),
  `bandFloorPerMille: 670`, `bandCeilingPerMille: 1330`, `referenceLevel: 20`, and the
  `tierMap` (`trivial→1 … extreme→5`) all match the registry file exactly, including the "duration
  ratio, mandatory, never 1.75" framing (registry's own `durationRatioNote`).
- **`core.v1.json`** — every rung's `countBand`/`tierWindow` pair matches the ladder as shipped;
  `budgetWeightMilli` across the 15 roles sums to exactly 1000 (160+120+90+80+80+70+60+60+60+50+
  50+50+40+15+15 = 1000, matching the file's own `budgetWeightMilliTotal`); the five
  `powerCategories` (`offense`, `survivability`, `control`, `utility`, `economy`) match exactly.
- **The 144-unique count** — verified two ways: the doc's own claim and a direct count of
  `data/seed/items/uniques/*.json` (18 files, 144 `unique.*` entries).
- **The 300-unique fleet-plan figure and its §2 citation** — `authoring-fleet-plan.md` line 55 and
  147, both inside `## 2`.
- **`tools/ItemSeedValidator`: 71 tests, wired to CI** — 61 `[Fact]` methods + 2 `[Theory]`
  methods × 5 `[InlineData]` cases each = 71 total test cases; `.github/workflows/ci.yml`
  references the project.
- **`tools/seed_graph`: 16 tests** — `test_reachability.py` has exactly 16 `def test_...` methods
  (the check-count half of this claim is wrong; see finding 2).
- **The "three invented elements" claim** (`spec-pipeline.md` §3.3) — `build-log.md` line 498:
  "three invented elements" — matches exactly.
- **`1 AE` definition and its `ssot-sets.md:187` citation** — exact line match, exact wording
  match.
- **`ssot-sets.md §3.5`'s "1.5 AE per member piece"** — exact match, correct section.

---

## 9. Should have been grounded and was not: the "51 invented tags" figure

**Severity: MAJOR** — `spec-foundation.md` line 92; `spec-pipeline.md` line 69 (same claim, same
number, both uncited)

> "'Tags come from `tags.v1.json`' cost 51 invented tags." (`spec-foundation.md`)
> "…the earlier waves lost 51 tags that way." (`spec-pipeline.md`)

Neither spec cites a source. A repo-wide search for "51 tag" or "51 invented" turns up only these
two specs repeating the identical figure — no `build-log.md` entry, no other `item/` doc, matches
it. The one place `build-log.md` actually narrates an incident about invented tags (line 280) gives
a different, much smaller number: **"6 invented tags."** It's possible 51 is a real corpus-wide
total from a script run that was never written down anywhere durable — but as it stands, this is a
specific, load-bearing number (used twice, verbatim, as the motivating example for "inline the
vocabulary, don't cite it") that cannot be checked against anything in the repository, and the one
adjacent number that *can* be checked is off by roughly 8×. Either cite where 51 came from, or
correct it to whatever `build-log.md` (or a rerun of whatever produced it) actually supports.

---

## 10. The map's own spec index omits a spec that exists

**Severity: MINOR** — `seedsmith-map.md` §8 (lines 187-195); actual:
`docs/architecture/seedsmith/spec-metrics.md`

The map's table of specs lists six rows (`numerics`, `analytics`, `budget`, `planner`, `pipeline`,
and the combined `corpus`/`adapter`/`report`/`briefkit` foundation doc) but the `seedsmith/`
directory holds **seven** spec files — `spec-metrics.md` is not in the map's index at all, despite
being referenced by name from `spec-analytics.md`, `spec-budget.md`, `spec-planner.md`, and being
the document that opens by noting *"the map listed it; six specs referenced it; none defined it"* —
i.e., `spec-metrics.md` itself documents that this exact gap existed before it was written, and the
map's §8 table was never updated to add the seventh row once the gap was closed. A reader relying
on the map's index alone would not know this file exists.
