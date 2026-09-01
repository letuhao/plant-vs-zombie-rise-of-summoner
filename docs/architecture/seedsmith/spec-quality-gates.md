# Spec: `quality-gates`

Module `quality-gates` in the [seedsmith map](../seedsmith-map.md) §3d.
Depends on `workflow-runtime`. Consumed by every generator.

`R#` = [audit](review/audit-agent-runtime-proposal.md).

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build.**

---

## 1. Objective

Decide whether generated content is **good**, not merely well-shaped — and be honest about which of
those two questions each check actually answers.

**This module exists because of a measured failure.** An 8-demon run scored **8/8 first-attempt
validator pass, 0/8 anti-motif violations**. Reading the same eight outputs:

> `cherrynut` → *"会以极高的 **伤害** 压制 **僵尸**"* — motifs inserted with spaces around them,
> visibly shoehorned to satisfy the checker.
> `bucketnutzombie` → *"**一类** 行为"* — "armour-class-one behaviour", which is not a concept.

**100% mechanical compliance, mediocre content.** That gap is this module's entire subject.

**Done means:** three tiers of checking exist, each labelled with what it can and cannot prove, and
nothing in the codebase reports a validator pass rate as a quality result.

---

## 2. Design

### 2.1 Three tiers, cheapest and most certain first

| Tier | Mechanism | Proves | Cost |
|---|---|---|---|
| **1. Constrained decoding** | LM Studio JSON Schema at decode time | **Shape.** Invalid output is unsampleable | free |
| **2. Deterministic validators** | pure functions, no model | **Mechanical properties** — a token is present/absent, a field is non-empty | free |
| **3. CoVe** | a second, independent model pass | **Judgement** — is the draft consistent with source | **3–4× calls** (plan + answer + revise on top of generate) — corrected from an earlier "~2×", which understated it |

Ordering is the design. A model is never asked what a `==` can decide.

### 2.2 The deterministic validator library

Reuses what exists; adds what the measured run showed missing.

**Existing, reused unchanged:** `audit_schema` (never a number), `audit_open_loop_schema` (never a
verdict), `CITATION_PATTERNS` (inlined, never cited), `SemanticDedup` (cross-entity duplicates).

**New, each traceable to an observed defect:**

| Validator | Rejects | Observed in |
|---|---|---|
| `motif_coverage` | output using **none** of the subject's motifs | the mechanism that forced attempt 2 in the probe |
| `anti_motif_violation` | output using a word the subject is defined **against** | the hardest constraint; held 0/8 |
| `field_echo` | a field value beginning with its own field name | ⛔ **7 of 8 outputs began `"DOCTRINE:"`** — the model echoing the prompt label into the value. Nothing caught it (R8) |
| `non_empty` | empty/whitespace required fields | shape backstop behind tier 1 |

`field_echo` is small and generalises: any field whose value opens with its own name is prompt
leakage, in any workflow.

### 2.3 ⛔ CoVe — **specified in full, NOT built in this module** (audit S4/S5/S6)

Chain-of-verification, in four steps:

1. **Generate** — the draft (already done upstream).
2. **Plan** — derive verification questions *from the draft*.
3. **Answer independently** — answer each question **against the source material**, in a call not
   shown the draft's justification.
4. **Revise** — if an answer **explicitly contradicts** the draft, escalate.

#### ⛔ The question form is load-bearing, and getting it wrong makes CoVe useless

**Measured on the real shoehorned outputs tier 2 had passed:**

| CoVe form | agreed with human judgement |
|---|---|
| **Subjective** — *"does this use the keyword meaningfully?"* | **1/3.** Passed *both* shoehorned cases, rationalising them: *"'一类' defines a specific category of behavior"* |
| **Source-grounded** — *"what does the source say this demon does? is the draft consistent?"* | **2/3.** Caught **both** shoehorned cases |

**Any text can be rationalised**, so a subjective verifier defaults to charitable and catches
nothing. A verifier answering a question **from source** has something to be wrong against.

⛔ **Therefore, binding:** every verification question **must be answerable from source text alone**.
A question asking for a quality opinion is a defect. *(An earlier draft of this spec said only
"answer against the source material" — ambiguous enough that its own author built the subjective
form on the first attempt. That ambiguity is why this rule is now explicit.)*

#### ⚠️ False positives are real — reject only on explicit contradiction

Source-grounded CoVe's one miss was a **false positive**: it rejected *good* content because the
source said *"nuts have hard shells"* and the verifier objected that it *"does not describe a demon."*

A verifier that rejects good content burns budget and can loop. So: reject **only** on explicit
contradiction, and route a CoVe rejection to **`escalate` (human review), never to auto-repair.** An
unreliable judge must not silently drive the repair loop.

#### ⛔ Why this is specified but not built

Shoehorning happens **because the motifs are bad**. Given `一类` ("armour-class one") there is no
meaningful way to use the token; given `坚果`/`樱桃`/`外壳` the model does not need to shoehorn.
**`motif-prose-filter` removes the cause; CoVe treats the symptom** — at 3–4× calls (§2.1) plus a
false-positive rate.

This is `spec-pipeline.md:109`'s own rule applied to ourselves: do not spend a model where something
cheaper solves it. **Build CoVe only if shoehorning is measured to persist after
`motif-prose-filter` lands** — that measurement is `commander-effect`'s §6 quality row.

### 2.4 ⛔ A pass rate is never a quality claim

R3 is structural, not a tuning issue: *"uses the token"* is mechanically checkable, *"uses it
meaningfully"* is not.

Therefore, binding on this module and everything downstream:

- Every validator reports **which tier** it belongs to.
- A tier-2 pass is reported as **"mechanically valid"**, never "good".
- Any summary mixing tiers must say so.
- This mirrors the field's own *benchmark 90% → production 70–80%* gap, and this repo's own rule that
  a green fixture is not production evidence.

### 2.5 Self-consistency is specified but not enabled

n=3 sampling with agreement scoring is **designed and left off**. It triples cost, and there is no
measurement yet showing CoVe insufficient. Enabling it is a later, evidence-backed act — not a
default. Recording the design now prevents it being re-derived badly later.

### 2.6 CoT is rejected, deliberately

`llm_caller` sends `reasoning_effort: "none"` and `enable_thinking: false` on every call, on purpose.
Constrained decoding also constrains reasoning tokens, so CoT and tier 1 interact badly. Revisit only
with data.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_quality_gates.py -q
python -m pytest tests/test_cove.py -q
python -m pytest -q
```

---

## 4. Project structure

```
tools/seedsmith/seedsmith/workflow/
    validators/          → pure functions, NO langgraph, NO model
        __init__.py  motif.py  field_echo.py  registry.py
    nodes/
        validate.py      → runs the tier-2 battery, returns defects
        cove.py          → tier-3 verification node
tools/seedsmith/tests/
    test_quality_gates.py  test_cove.py
```

Validators live under `workflow/` rather than `metrics/`: a `metrics/` check reports on the **corpus**
(and ships `gates=False` for calibration), while these gate a **single generation** before it is ever
written. Different lifecycle, different consumer.

---

## 5. Code style

One pure function per validator: `(draft, context) -> list[str]` of defect strings. Composable, each
independently testable, and a surviving mutant names one rule rather than "the gate". Defect strings
are the text fed back into the repair prompt, so they must name the field **and** the offending value
— `spec-pipeline.md` §3.6's "name the exact defects".

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Output using no motif | `motif_coverage` rejects |
| Output using one motif | passes |
| Output using an anti-motif | `anti_motif_violation` rejects |
| ⛔ `{"doctrine": "DOCTRINE: ..."}` | `field_echo` **rejects** — the exact observed defect, pinned |
| `{"doctrine": "The doctrine of ..."}` | **passes** — prose mentioning the word is not an echo; over-refusal is its own defect |
| Empty / whitespace-only required field | rejected |
| Defect strings | name the field **and** the offending value |
| CoVe: draft contradicted by source | **escalates** (never auto-repairs — §2.3) |
| CoVe: draft supported by source | passes unchanged |
| ⛔ **CoVe verifier is not shown the draft's justification** | asserted structurally (§2.3) — the independence property, not an implementation detail |
| ⛔ **Every verification question is answerable from source alone** | asserted — a subjective question is a defect (S4: the subjective form scored **1/3**) |
| CoVe schema | carries **no** verdict field (`audit_open_loop_schema`) |
| ⛔ CoVe is **not wired into the default graph** | asserted — specified, not built (§2.3) |
| Any tier-2 result object | carries its tier label; cannot be printed as "quality" (§2.4) |
| Self-consistency | present, **disabled by default**, asserted off |
| Zero real model calls | `MockModelServer` only |

The `field_echo` positive/negative pair is the one worth keeping: a rule that rejects any mention of
the field name would "pass" its rejection test while quietly breaking real prose.

---

## 7. Boundaries

- **Always:** run tiers in order; label every result with its tier; keep CoVe's answering pass
  independent; name field and value in defect strings.
- **Ask first:** enabling self-consistency; adding a tier-3 check where tier 2 could decide; any new
  validator that needs a model.
- **Never:** report a tier-2 pass rate as quality; show the verifier the draft's justification; add a
  verdict field to a CoVe schema; enable CoT by default.

---

## 8. Success criteria

1. All four new validators exist as pure functions with positive **and** negative tests.
2. `field_echo` rejects the exact `"DOCTRINE:"` defect and accepts legitimate prose.
3. CoVe's independence property is asserted structurally.
4. Tier labelling exists and no summary reports tier-2 as quality.
5. Self-consistency is implemented and off.
6. Zero real model calls in the suite.
7. Full seedsmith suite green.

---

## 9. Open questions

**Closed 2026-09-01 by measurement** ([audit S4/S5/S6](review/audit-generation-runtime-specs.md)).

1. ~~Does CoVe measurably improve the shoehorning in §1?~~ ✅ **CLOSED — it depends entirely on the
   question form, and the answer changed the build order.**
   - **Subjective form: 1/3** — it passed both shoehorned cases, rationalising them. Useless.
   - **Source-grounded form: 2/3** — caught both, with one false positive on the control.
   - **Therefore:** CoVe works *only* source-grounded (§2.3, now a binding rule), rejects only on
     explicit contradiction, and escalates rather than auto-repairs.
   - **And it is deferred:** shoehorning is caused by bad motifs, which `motif-prose-filter` fixes at
     the source for zero model cost. CoVe is fully specified and **not built** until shoehorning is
     measured to survive that fix.
