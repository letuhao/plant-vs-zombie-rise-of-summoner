# Spec: `validate-heal` (A-S4)

**Module id:** `validate-heal` · **Status:** proposed 2026-09-03 · **Program:** [action-corpus](../action-corpus-map.md) · **Model calls: mixed**
**Depends on:** `A-P1`, `A-P2`, `A-P3` · **Feeds:** `A-S3 dedup-select`
⚠️ The capability map's gate still stands — *"Not approved. No module spec may be written until it is"*
(`action-corpus-map.md:3-5`). Written ahead of approval on the owner's instruction.

**What it owns.** The single acceptance point for every candidate the three model pipelines produce: the
**schema audit** (run once per schema, before any call is made), the **per-candidate quality gates**, and the
**bounded self-heal** — two repairs naming the defect, then `unresolved`, never a silent third
(`action-corpus-map.md:64`). It is "mixed" model because a repair is a genuinely new generation; the gates
themselves are deterministic and make no call.

**⛔ A naming collision the map carries, resolved here.** `action-corpus-map.md:64` gives A-S4 *"quality gates
t1–t3"* while `:65` gives A-S3 the dedup tiers t1/t2/t3 defined at `action-corpus-ideal.md:426-434`. These are
**two different ladders sharing a name.** In this spec, A-S4's gates are **per-candidate** (one candidate, no
knowledge of any other) and are numbered **g1/g2/g3**; the dedup tiers stay **cross-candidate** and stay
A-S3's. The map should be corrected to say so; until it is, this paragraph is the reconciliation.

**⛔ Binding constraints, restated inline — a downstream session reads this file, not its links.**

1. **The LLM writes identity. Deterministic code writes magnitude.** No model picks a number, weight,
   probability, duration, tier or rung. **This module is where that is enforced**, by `audit_schema`
   (`tools/seedsmith/seedsmith/pipeline/model.py:53-99`), never by review.
2. **Three pipelines, not one parameterised stage** — P-general, P-family, P-signature. A-S4 audits **three**
   schemas and runs **three** gate sets, never one generic path with a `scope` switch.
3. **Permute every enum**, seeded from `(entity_id, field, sample_index)` — `sample_index` inside the seed
   (`anchor/permute.py:16-30`). A-S4 verifies the permutation happened; it does not perform it.
4. **Majority-vote only load-bearing fields.** 1-1-1 → `unresolved`, never the first option
   (`anchor/vote.py:23-40`). **A-S4 owns the vote resolution and the `unresolved` outcome.**
5. **Every enum description carries a negative clause** saying what the field is NOT. `none` is a value; a
   missing key is a defect. A-S4 asserts this over each schema mechanically.
6. **TRANSIENT ≠ QUALITY.** A pause is transient — replay from checkpoint, **no new call**
   (`workflow/runner.py:11-13,46-51`). A validator rejection is QUALITY — a new generation, defect named.
7. **Small-batch proof first** — `--dry-run` renders and gates recorded candidates with zero calls.
8. **Tests never call a model** — the transport stub **raises**.
9. **The roster is 84 species (53 with family assignments), not 904**, so the gate-failure rates this module
   reports are over hundreds of candidates, not thousands.

## 1. What exists today

### Built

| Thing | Evidence |
|---|---|
| `audit_schema` — walks `properties`, `items`, `anyOf`/`oneOf`/`allOf`; rejects bare numeric types and a missing `blocked` | `tools/seedsmith/seedsmith/pipeline/model.py:53-99` |
| `BLOCKED_FIELD = "blocked"`, `NUMERIC_JSON_TYPES = {number, integer}` | `pipeline/model.py:36-41` |
| `SchemaDefect(path, reason)` with a readable `__str__` | `pipeline/model.py:43-50` |
| `PipelineResult` separating `blocked` (declined, reportable) from `escalated` (retries exhausted, a real problem) | `pipeline/model.py:102-123` |
| `call_with_self_heal(items, system, build_user, verify_fn, ..., max_heal, build_heal_user, default_for)` — hard failures re-prompt **naming the defect per key**, soft failures are reported and never auto-retried | `pipeline/llm_caller.py:207-236` |
| Heal budget is configurable; **default 3** | `pipeline/llm_caller.py:45,236` |
| Vote resolution: 3-0 `high`, 2-1 `split` + minority, 1-1-1 `unresolved` with `value is None` | `adapters/demons/anchor/vote.py:16-40` |
| Disagreement rate per field, reported as a deliverable | `anchor/vote.py:43-67`; `metrics/pipeline_health.py:37-39` |
| TRANSIENT vs QUALITY split, stated in the runner's own docstring; `resume()` replays from checkpoint with `None` input | `workflow/runner.py:11-13,46-51` |
| Offline guarantee as a test, not a claim | `tools/seedsmith/tests/test_classify_pipelines.py:36 (NOT test_offline_guarantee.py — that file PERMITS 127.*/localhost/::1/0.0.0.0, which is exactly where the model runs: llm_caller.py:40 endpoint http://localhost:1234):1-8` |
| Rung table with per-row `structureBudget` (what g2 checks a candidate's axes against) | `data/tuning/action-rungs.v1.json:11-20` |

### Wiring gap

| Thing | Evidence |
|---|---|
| `AFFIX_SCHEMA` has **no `blocked` property**, so the shipped affix pipeline's own schema would fail today's audit | `adapters/effects/affix/prompts.py:26-38` vs `pipeline/model.py:92-97` |
| `audit_schema` catches two of the four documented smuggling shapes — bare numeric type, and it allows numeric `enum` by design (`model.py:60-61`). **A `string` with `"pattern": "^[0-9]+$"` and an enum of numeric strings are not detected** | `pipeline/model.py:65-72` — the check keys on `type`, and `"string"` is not in `NUMERIC_JSON_TYPES` |
| `data/seed/actions/` unreadable by the loader | `corpus/model.py:159-185` |

### Real gap

- **This module** — no action-corpus validator, gate set or heal wrapper exists.
- **The two undetected smuggling shapes above.** A-S4 must extend the audit rather than assume it; that
  extension is this module's first task and it belongs in `pipeline/model.py` so all seedsmith pipelines
  inherit it, not in an action-corpus-local copy.
- **`StructureBudgetGuard` cannot detect `reaction` / `restriction`** (`action-corpus-ideal.md:1462-1468`),
  so g2 is honestly incomplete on the two axes that separate the signature tier. It must **report** that
  rather than pass silently.

## 2. The contract

### Stage 0 — the schema audit, once per run, before any call

For each of the three schemas: `audit_schema(schema)` must return an empty list, **and** three additional
assertions this module adds, because the shipped audit does not cover them:

- **no `string` property carrying a `pattern` that admits a bare number** (`^[0-9]`, `[0-9]+$`, `\d`);
- **no `enum` whose members are numeric strings** (`"1"`, `"2"`);
- **no property whose name is in the magnitude deny-list** — `hp`, `atk`, `damage`, `cost`, `chance`,
  `duration`, `weight`, `rung`, `tier`, or any name ending `Milli`. An integer that is genuinely an
  identifier is allow-listed **by name, with a comment saying why** it never enters arithmetic.

Plus the description rule: **every property has a `description`, and every description contains a negative
clause.** Implemented as a mechanical check (the description must contain a "not"/"never" sentence naming
what the field is not), so it is a test rather than a review habit.

A failure here **aborts the run before a single call is made.** That is the point: a schema that admits a
number is rejected at zero cost.

### Stage 1 — the per-candidate gates (deterministic, no model call)

| Gate | Checks | Verdict | Loop |
|---|---|---|---|
| **g1 · contract** | the draft parses against the schema; every required key present; no extra key; `blocked` present and non-empty only when the model genuinely declined | **hard reject** → repair | closed-loop |
| **g2 · brief conformance** | every `atomFamilies` member is in the brief's `allowedAtomFamilies`; none is in `forbiddenAtomFamilies`; no `motifsExpressed` member is an anti-motif; `structureAxes` claimed are within the **ceiling** row of the brief's rung band (see the F13 note below); for A-P3, `atomFamilies` does not exactly equal any listed family action's set; for A-P1, the brief carried no anchor and the draft names no species/family/element token | **hard reject** → repair | closed-loop |
| **g3 · quality** | `name` is non-empty, not a bare restatement of the atom family ids, and unique within the round's own candidates; `rationale` refers to at least one motif or, for A-P1, to the role. **A-P3's `differentiator == "none"` is RECORDED, never penalised** — see the note below | **advisory** → review queue, never an auto-reject | **open-loop** |

**⛔ g2's rung-band resolution, stated — added 2026-09-03 (review F13).** `ActionRow.Rung` is one
`int` (`ActionRow.cs:23`) and `StructureBudgetGuard.Check` resolves exactly one row from it
(`StructureBudgetGuard.cs:41`), while a signature band spans budgets of 0 and 7 axes — ⛔ **CORRECTED
2026-09-03:** this read `[5,10]` and *"3 and 7"*; the floor is dropped, the band is `[1,10]`, and rung
1 carries `structureBudget: []` (`spec-rung-semantics.md` §3.2). g2 does not
invent its own resolution: it applies **A-S1's collapse rule** — `Rung = rungBand[1]`, the band's
ceiling (`spec-distribution-planner.md` §3 step 4) — and checks the claimed axes against **that**
row's `structureBudget`. Checking any other row checks a budget the brief was never planned against.
**And it is the AUTHORED rung g2 reads, deliberately**: `spec-rung-semantics.md` (A-U1) §3.1 pins
`Rung` (authored, fixes structure) apart from `effectiveRung` (per holder, fixes magnitude and cost),
and records that `StructureBudgetGuard` reading the authored column is **correct** — the false
inference was that clamping a holder's derived rung gates structure. g2 checks structure, so it reads
the authored value and never a holder's.
Two consequences g2 must carry rather than discover:

- **A claimed `reaction` is a HARD REJECT, not a flag.** It is unspendable —
  `StructureBudgetGuard.cs:27-30` verified `ActionKind` has three members and none is reaction-shaped
  — and A-S1 refuses to emit a brief naming it, so a draft claiming it is a draft claiming something
  the shipped action model cannot express (`spec-tier-access-gate.md` AC5).
- **A claimed `restriction` passes g2 and is reported UNCHECKED.** `StructureBudgetGuard.cs:30-34`
  needs the effect-atom program's per-atom payload/target data to detect it. The candidate carries
  `structureEnforced: false` from its brief, and the round report gives the count, so a round never
  claims structural conformance it did not verify.

**⛔ g3 must not penalise an honest `none` — corrected 2026-09-03 (review).** The clause
`differentiator != "none"` treated A-P3's most useful honest answer as a quality defect. A-P3's own
schema note is explicit: *"`none` means it does not meaningfully differ, and saying `none` honestly is
better than inventing a difference"*, and *"a `none` answer is a real, useful signal: it tells A-S3
the candidate is a near-duplicate before the hash sets do"* (`spec-signature-propose.md:160-162`).
Penalising it teaches the pipeline to invent a differentiator, which is the exact failure P3 was split
out to prevent. So: `differentiator == "none"` is **recorded** on the candidate and forwarded to A-S3
as a near-duplicate hint and to the review queue as a count; it contributes **nothing** to a verdict,
in either direction. The rate is a first-class report line beside the disagreement rate — a rising
`none` share is evidence the signature tier is not earning its briefs, which is a plan decision, not a
per-candidate rejection.

**g3 is open-loop and declares it.** *"Is this name good?"* is detectable and not machine-verifiable, so it
produces a review queue and **never contributes to a pass verdict**. A green dashboard over prose nobody read
is a lie with a checkmark on it.

### Stage 2 — vote resolution

For each voted field, A-S4 collects the three permuted samples and calls `resolve_vote`
(`anchor/vote.py:23-40`): 3-0 → `high`; 2-1 → `split`, minority recorded; **1-1-1 → `unresolved`, `value is
None`, and the candidate goes to the review queue.** The candidate's `confidence` is written **here**, never
by the model.

**⛔ CORRECTED 2026-09-03 (review F8) — the permutation check was a gate that raises on legal
input.** It asserted the three samples used **different** permuted orders *"or the run raises"*. With
`k` options there are `k!` orders, drawn independently: for `k ≤ 2` three draws collide with
probability **1**, and for `k = 3` with probability **~44%**. `motifsExpressed`'s enum can be two
members (a family whose intersection is 2 motifs plus `none` is three), so the check would have fired
on correct runs. **A gate that raises on legal input is worse than no gate**, because the first fix
anyone reaches for is to delete it.

**The replacement checks the seed by reproduction, not the outcome by counting, and cannot raise on
legal input:**

1. **Per sample:** recompute `order_for(briefId, field, sampleIndex, options)`
   (`anchor/permute.py:26-33`) and assert the recorded sample's rendered option order **equals** it,
   byte for byte. A mismatch is a real defect — the sample was rendered from a different seed than
   the one claimed — and it is deterministic, so it can never be a false positive.
2. **Once, as a structural unit test on the helper, not as a per-run gate:** assert
   `_seed_int(id, field, 0)`, `_seed_int(id, field, 1)` and `_seed_int(id, field, 2)` are three
   distinct values (`anchor/permute.py:16-23`). That is what *"`sample_index` is in the seed"* means,
   and it is a property of the payload concatenation rather than of any one run's draw.

Together they prove exactly the thing the old check was reaching for — three samples are three
samples, not one with extra steps (`anchor/permute.py:5-6`) — without depending on `k!` being large
enough.

Voted fields, from the pipeline specs: `atomFamilies` for A-P1/A-P2; `atomFamilies` + `differentiator` for
A-P3. Adding a field to this set is an "ask first" boundary — it moves the call budget by a third of the run.

### Stage 3 — bounded self-heal

`call_with_self_heal(..., max_heal=2)` — **passed explicitly**, because the config default is 3
(`llm_caller.py:45`). The loop runs `range(heal_budget + 1)` (`llm_caller.py:242`), so `max_heal=2` is
**three attempts**: one generation and two repairs. The `build_heal_user` callback names the exact
defect per key: *"`atom.cruelty` is not in this brief's eligible atom families"* — ⛔ **CORRECTED
2026-09-03:** the example named `atom.crit-damage`, which exists in none of the three atom-family
namespaces; `atom.cruelty` is the real crit-damage family
(`data/seed/items/affix-families/g-precision.json`, `combat.crit.damage.{variant}`), and every id a
brief can open comes from the 98 authored there (`spec-distribution-planner.md` §2). Never a bare
retry. An unbounded repair loop is how a run silently costs ten times its estimate.

### ⛔ The exhaustion contract, adapted for a GENERATION stage — added 2026-09-03 (review F9)

**`call_with_self_heal` never produces `unresolved`, and this spec said it did.** Its docstring is
explicit: *"On exhausted heal rounds, `default_for(key, original_value)` supplies the no-silent-drop
fallback (default: the original item's own value) … **Never raises** on a model or parse failure"*
(`llm_caller.py:229-234`). The helper was generalised from a **translation** loop, where `items` is
the source text and handing back the original is a sane fallback. **For a generation stage `items` is
the brief**, so the shipped default (`lambda key, original: original`, `llm_caller.py:238`) would hand
a *brief field* back as though it were the model's answer — the planner's own `allowedAtomFamilies`
returned as the model's `atomFamilies`. Adopting the helper for generation needs the contract adapted,
not assumed. It is adapted here:

- **`default_for` returns `None`, always, for every key.** A-S4 passes
  `default_for=lambda key, original: None` explicitly. For a generation stage there is no original
  value, and `None` is the only honest one — it is also the exact shape the vote resolver already
  uses for the same meaning (`VoteResult.value is None` when `confidence == "unresolved"`,
  `anchor/vote.py:18-40`), so `unresolved` looks the same however it arose.
- **`unresolved` is a verdict A-S4 WRITES, not an exception the helper throws.** The helper returns
  `(out, soft)` and on exhaustion stamps every still-hard key into `soft` as `"FAILED:<reason>"`
  (`llm_caller.py:255-258`). A-S4 inspects that dict: **any key whose `soft` entry starts `FAILED:`
  makes the candidate `unresolved`**, with `value is None` for that key and the `FAILED:` reasons
  attached as the defect list. Nothing else is read from `out` for such a key — `default_for` wrote
  it, so it is not an answer.
- **The helper's "never raises" is correct and is kept.** A raise would discard the other keys'
  valid answers and the defect list along with them, which is strictly less information than the
  `unresolved` verdict. The bound is not enforced by an exception; it is enforced by `max_heal=2` and
  by A-S4 refusing to accept a `FAILED:` key.
- **Recorded where the run can see it:** the candidate's emitted verdict is `unresolved`, its
  `_provenance` carries the heal count (exactly 2 on this path), and the round report counts
  `unresolved` candidates per gate and per key — the same first-class treatment the disagreement rate
  gets, for the same reason.

**TRANSIENT is a different path entirely.** Endpoint down, timeout, 5xx, or a user-requested pause →
`resume()` replays from the checkpoint with **no new model call** (`workflow/runner.py:11-13,46-51`). A
transient failure must never consume heal budget, and a pause must never change an answer.

### What it emits

Per candidate: `accepted` | `blocked` | `unresolved` | `escalated`, the gate results, the vote records, and
`_provenance` carrying model id, prompt version, brief hash, schema hash, and the heal count. Per round: the
**disagreement rate per field** (`anchor/vote.py:43-67`) — a deliverable, not diagnostics: a high rate means
a weak description and the fix is known; a near-zero rate means the field is a candidate to drop from the
vote set, halving its cost.

## 3. What it must NOT do

- **Never repair more than twice.** The third failure is `unresolved` — written by this module from
  the helper's `FAILED:` soft entries (§2 Stage 3's F9 note), never by an exception, and never by
  accepting whatever `default_for` left in `out`.
- **Never let `default_for` return the original value.** For a generation stage `items` is the brief,
  so the shipped default would return a planner field as the model's answer (`llm_caller.py:238`).
  It returns `None`.
- **Never regenerate on a transient failure**, and never let a pause cost a call.
- **Never auto-reject on g3**, and never let an open-loop metric contribute to a pass verdict.
- **Never penalise an honest `none`.** A-P3's `differentiator: "none"` is a signal, not a defect
  (§2 Stage 1's note); scoring it down teaches the pipeline to invent a difference.
- **Never invent a rung-band resolution.** g2 uses A-S1's stated collapse rule (`Rung = rungBand[1]`)
  and checks the ceiling row's budget — nothing else.
- **Never resolve a 1-1-1 split to the first option**, or to any option.
- **Never write `confidence` from the model's own answer.**
- **Never dedup.** Cross-candidate comparison is A-S3's, and doing it here would make acceptance
  order-dependent — the defect this repo has already shipped once.
- **Never override a deterministic result with a model's opinion.** When a model disagrees with a table, the
  disagreement is **recorded**, not applied; a pile of them in one range is a signal to retune the table.
- **Never edit a candidate in place** to make it pass. Repair is a new generation, not a patch.
- **Never share one generic gate path across the three pipelines** — three schemas, three gate sets.

## 4. Testing strategy

1. **Stubbed transport that raises.** Every gate, the schema audit, the vote resolution and the
   `unresolved` path are exercised against recorded drafts under a transport whose only behaviour is
   `raise`. Only the heal path may call, and its test asserts the call **count**, never its content.
2. **Determinism / replay.** The same candidate set through the gates twice → identical verdicts, identical
   ordering, byte-identical report under canonical serialisation. A `resume()` test proves a transient
   failure replays from checkpoint with **zero** new calls (asserted on the raising stub: if it is touched,
   the test fails).
3. **Planted violations**, each its own test, each expected to be caught:
   - `{"rung": {"type": "integer"}}` → audit defect;
   - `{"rungMilli": {"type": "string", "pattern": "^[0-9]+$"}}` → audit defect **from this module's own
     extension**, since the shipped `audit_schema` does not catch it (`model.py:65-72`);
   - `{"tier": {"enum": ["1","2","3"]}}` → audit defect from the extension;
   - `{"damage": {"type": "string"}}` → deny-list defect;
   - a schema with no `blocked` → defect;
   - a description with no negative clause → defect;
   - a draft naming a forbidden atom family → g2 hard reject, and the recorded re-prompt contains that
     family id;
   - a draft claiming a structure axis outside its rung band → g2 hard reject;
   - three distinct vote samples → `unresolved`, `value is None`;
   - a sample whose recorded option order does **not** reproduce
     `order_for(briefId, field, sampleIndex, options)` → the run raises (§2 Stage 2's F8
     replacement). ⛔ **CORRECTED 2026-09-03:** the old case here was *"three identical permuted
     orders → the run raises"*, which fires on legal input whenever the enum has two members;
   - a candidate failing g2 three times → recorded `unresolved`, heal count exactly 2, the `soft`
     dict carries `FAILED:<reason>` for the offending key, `out[key]` is **not** read, and the
     candidate is never accepted;
   - a `default_for` that returns the original brief value → the module's own contract test fails
     (§2 Stage 3's F9 note);
   - a draft claiming `reaction` → g2 **hard reject**, not a flag;
   - an A-P3 draft with `differentiator: "none"` → **accepted**, recorded, counted, and the test
     fails if the verdict is anything but a pass on that ground alone.
4. **A "cannot fail" guard.** One test asserts each gate rejects at least one planted input — a gate that
   never rejects anything is a comment, and a test that cannot fail is not a guardrail.
5. **A "cannot falsely fail" guard, the mirror of it.** One test asserts each gate **accepts** a
   legal input that an earlier draft would have rejected — an A-P3 candidate with
   `differentiator: "none"`, and three samples over a two-member enum whose permuted orders
   necessarily collide. ⛔ Added 2026-09-03 (review F8): a gate that raises on legal input is worse
   than no gate, and only a test shaped like this catches one.

## 5. Acceptance criteria

1. All three pipeline schemas pass `audit_schema` **and** this module's three extensions, under a test CI
   runs; the extensions live in `pipeline/model.py` so every seedsmith pipeline inherits them.
2. Every property of every one of the three schemas has a description with a negative clause, asserted
   mechanically.
3. g1 and g2 are closed-loop and may contribute to a pass; **g3 is declared open-loop and cannot**.
4. A 1-1-1 vote yields `unresolved` with `value is None`; a 2-1 records the minority; `confidence` is never
   read from a model field.
5. Each of the three vote samples **reproduces** `order_for(briefId, field, sampleIndex, options)`
   exactly (`anchor/permute.py:26-33`), and a separate structural test asserts
   `_seed_int(id, field, 0..2)` are three distinct values (`:16-23`). ⛔ **CORRECTED 2026-09-03
   (review F8):** this criterion required three *different* orders, which is impossible for a
   two-member enum and ~44% likely to fail for a three-member one — it would have raised on legal
   input.
6. Repairs are bounded at exactly **two** (`max_heal=2` passed explicitly — three attempts, since the
   loop is `range(heal_budget + 1)`, `llm_caller.py:242`), and the third failure records `unresolved`
   with the defect list attached.
6b. `default_for` is passed explicitly and returns `None` for every key; the `unresolved` verdict is
   derived from the helper's `FAILED:` entries in the returned `soft` dict (`llm_caller.py:255-258`),
   and no `out[key]` written by `default_for` is ever read as an answer. ⛔ **CORRECTED 2026-09-03
   (review F9):** `call_with_self_heal` *"never raises"* and had no generation-stage exhaustion
   contract; four specs asserted an `unresolved` the helper does not produce.
6c. g2 resolves a rung band through A-S1's stated collapse rule (`Rung = rungBand[1]`) and checks the
   ceiling row's `structureBudget`; a claimed `reaction` is a hard reject and a claimed `restriction`
   passes and is reported **unchecked** (review F13).
6d. g3 records A-P3's `differentiator: "none"` and never scores it down; the round report carries the
   `none` rate as a first-class line.
7. Every re-prompt names the specific defect; a bare retry is impossible by construction (`build_heal_user`
   is required, not optional).
8. A transient failure consumes **zero** heal budget and makes **zero** new calls, proven against the
   raising stub.
9. `--dry-run` gates a recorded candidate set with zero model calls.
9b. **`AFFIX_SCHEMA` passes the extended audit.** The `blocked` property is added at
   `affix/prompts.py:26-38` with a negative-clause description, nothing else in that pipeline changes,
   and `python -m pytest tools/seedsmith/tests` is green. ⛔ **DECIDED 2026-09-03 (owner removed
   themselves as a gate)** — §6 hazard 1 names the owner and the revert condition.
9c. **Constrained decoding is proven, or the run does not start.** `--preflight` makes one real call
   with `response_format` set and a single-member-enum probe schema; a reply that does not match aborts
   the run naming the endpoint and model id. In `--dry-run` and under the raising stub it is
   **skipped**, and provenance records `preflight: "skipped"` — a test asserts the skip, never the
   call, because tests never call a model. ⛔ **DECIDED 2026-09-03 (owner removed themselves as a
   gate)** — §6 hazard 4. **It blocks a real generation round, never a module's build.**
10. The per-field disagreement rate is emitted every round as a first-class report artifact.
11. The full test module passes with the transport stubbed to raise.
12. A second run over unchanged inputs produces a byte-identical report by hash.

## 6. Dependencies and cross-program hazards

| Needs | From | State |
|---|---|---|
| Candidates and their briefs | **A-P1 / A-P2 / A-P3** | none exist |
| The brief's pool, structure axes and rung band | **A-S1** `distribution-planner` | does not exist |
| Cross-candidate dedup | **A-S3** `dedup-select` (downstream) | does not exist |
| `audit_schema`, `call_with_self_heal`, `resolve_vote`, `resume()` | seedsmith, shipped | built — see §1 |

**Hazards.**

1. **Extending `audit_schema` touches every seedsmith pipeline.** `AFFIX_SCHEMA` has no `blocked` property
   (`affix/prompts.py:26-38`) and will fail the moment the audit is applied to it. That is a real, correct
   finding about shipped content — it must be fixed there, not worked around here, and it should be raised
   with the effect-pipeline program rather than silently exempted.

   **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — A-S4 owns the extension AND the
   one-property `AFFIX_SCHEMA` fix.** The hazard named the problem and no owner, which leaves AC1
   (*"the extensions live in `pipeline/model.py` so every seedsmith pipeline inherits them"*)
   unpassable behind a cross-program handoff — and a gate that cannot go green is a gate that gets
   commented out.

   **Why A-S4 and not effect-pipeline:**

   - **It is not a design change to the affix pipeline; it is that pipeline's own unmet contract.**
     `BLOCKED_FIELD = "blocked"` already ships (`pipeline/model.py:36-41`) and the audit already
     requires the property at the schema root (`pipeline/model.py:92-97`), whose own defect message is
     *"a model with no way to decline invents instead."*
   - **The affix schema is strictly worse than "missing a property".** It carries
     `"additionalProperties": false` with `"required": ["name", "refs"]`
     (`affix/prompts.py:26-38`), so the affix model **cannot emit `blocked` at all** — it has no way
     to decline, by construction. Adding the property is a fix for a live defect, not paperwork.
   - **It is additive and therefore reversible.** A new *optional* property on a schema whose runner
     reads `name` and `refs` changes no existing parse; the revert is deleting one property.

   **Scope, held tight:** A-S4 adds the `blocked` property and its description (a negative clause,
   modelled on the hardened one at `affix/prompts.py:74-82`) and changes **nothing else** about the
   affix pipeline. **Acceptance is that `python -m pytest tools/seedsmith/tests` stays green.** If it
   does not — i.e. the affix runner turns out to depend on the closed property set — the change
   reverts and becomes a named effect-pipeline follow-up, `affix-schema-blocked`, with this paragraph
   as its statement of work. **What would overturn the ownership:** exactly that test result.
2. **g2 is incomplete on `restriction` only — ⛔ CORRECTED 2026-09-03 (review F3/F4).** The earlier
   wording lumped `reaction` in with it and cited the ideal rather than the guard. Read against
   `StructureBudgetGuard.cs:27-34` the two axes are in **different** states, and the difference
   changes what g2 does:
   - **`reaction` is unspendable, not undetectable.** The guard verified `ActionKind` has exactly
     three members and none is reaction-shaped, so it is *correctly* never flagged. A-S1 refuses to
     emit a brief naming it and g2 **hard-rejects** a draft claiming it.
   - **`restriction` is genuinely undetectable** — it needs the effect-atom program's per-atom
     payload/target data, outside the three tables the guard reads. g2 lets it pass and reports it
     **unchecked**, so a round never claims structural conformance it did not verify.

   This also gives the hazard instances. Under A-S1's old *intersection* rule the assignable axis set
   was empty for two of three tiers and `restriction` was unreachable in all three, so this note
   described a case that could never occur; under **union-to-ceiling**
   (`spec-distribution-planner.md` §3 step 5) `restriction` is the signature tier's one exclusive
   axis and the count is real.
3. **C1's family-access widening is gated** on a per-rung `powerBudget` row, a family-aware non-additive
   price (needs D2) and a budget check with a production caller (`action-corpus-ideal.md:707-728`). Until
   then there is **no power budget for g2 to check against** — g2 checks pool membership and structure
   axes, and must not pretend to check power.
4. **Constrained decoding must be proven on**, with one real call at the start of a run checking the reply
   shape. If the server quietly ignores `response_format`, every schema guarantee above becomes decorative
   with no error anywhere. Proving it costs one call; discovering it absent costs the whole run.

   **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — A-S4 owns it, as `--preflight`, and
   it is acceptance criterion 10.** It appeared in no acceptance criterion and had no owner, which is
   how a one-call check that saves a whole run goes unwritten.

   **Where it belongs:** A-S4 already owns **Stage 0**, the once-per-run, pre-call schema audit. The
   preflight is Stage 0's second half — the audit proves the schema forbids a number, the preflight
   proves the server is reading the schema at all. Splitting them would put the only two run-start
   gates in two modules.

   **What it is, exactly.** One call through `llm_caller` with `response_format` set to a two-property
   probe schema, one of them a required single-member enum. **Pass:** the reply is valid JSON, carries
   exactly the declared properties, and the enum property's value is the declared member. **Fail:**
   the run **aborts before any generation call**, naming the endpoint and the model id. Cost: one call.

   **It is a run gate, never a test.** Tests never call a model — the binding law — so the suite
   exercises the preflight against the raising stub and asserts it is **skipped**, not satisfied. In
   `--dry-run` it is skipped too, and **`preflight: "skipped"` is written into provenance**, so a run
   that never proved constrained decoding can never report that it did.

   **This is the one item in this module that needs something outside the repo** (a live model
   server). It therefore blocks **no module's build** — only a real generation round. **What would
   overturn it:** a transport that guarantees constrained decoding structurally (a local grammar-based
   decoder in-process), at which point the preflight is a constant and can be deleted.
