# AI-native generation — what a model may decide, and what it may never touch

**Read before designing any seedsmith pipeline, contract, or generator.**

This is the companion to [../game-design/](../game-design/). That folder answers *"what makes a good
roster?"*; this one answers *"what can a language model reliably produce, and how do you build a
contract it cannot get wrong?"*

Every rule here is either a measured finding or a defect this repo has already paid for. Where a
finding is derived rather than cited, it says so.

---

## 1. The one law

> **The LLM writes identity. Deterministic code writes magnitude.**

`seedsmith-map.md` P1 states the reason, and it is a property of the model, not a policy preference:

> *"a model has no calibrated sense of scale, so a number it picks is a plausible-looking guess that
> survives review because nothing looks wrong with it."*

That last clause is the whole problem. A wrong enum is visible — *"this fire creature is classified as
ice"* — and a reviewer catches it. A wrong number is invisible: `hp: 4200` looks exactly as reasonable
as `hp: 2400`, and it ships. Over 900 entries, nobody re-derives them.

**So the enforcement is mechanical, never editorial.** A schema that admits a numeric field is rejected
before a single call is made — `audit_schema` in seedsmith does this today, and
`demon-seed/spec-anchor-contract.md` §4 extends it to the four shapes that smuggle numbers past a naive
type check:

1. a `string` field whose `pattern` admits a bare number (`^[0-9]+$`)
2. an `enum` whose members are numeric strings (`"1"`, `"2"`)
3. a field named in the magnitude deny-list (`hp`, `atk`, `damage`, `cost`, `chance`, `*Milli`)
4. an integer that is genuinely an identifier — **allow-listed by name, with a comment saying why**

An identifier is the one legal integer, and it earns that by never entering arithmetic.

---

## 2. Enum selection is the most bias-prone task shape there is

**This is the single most important finding for this project**, because an enum-only contract is
*entirely* made of this task shape.

Two distinct biases, and they compound:

| Bias | What it is |
|---|---|
| **Position bias** | the model partly answers *"which position is this?"* rather than *"which value is this?"* |
| **Label bias** | some tokens are simply likelier than others regardless of the question |

Magnitudes recorded in `demon-seed-ideal.md` §4.7: reordering the options alone swings measured
accuracy on GPT-4-class models by **up to 75 percentage points**, and majority voting across
permutations recovers **up to 8 points**.

### Why it is dangerous here specifically

A biased classifier produces output where **every individual answer looks right** and the *aggregate*
is skewed. Species by species, a reviewer sees nothing wrong. Across 900 entries, one element holds 30%
of the roster and two hold 2% — which is `game-design/05-failure-modes.md`'s Hammerdin shape arriving
by a completely different route.

### The two mitigations, and they are not interchangeable

| | Applies to | Cost |
|---|---|---|
| **Permute the options** | **every** enum, every call | free — a list reordering |
| **Majority vote (3 samples)** | only the load-bearing fields | 2 extra calls per field per entry |

**Permutation must be deterministic and seeded from the entry's own id**, not from a clock:

```python
seed = blake2b(entity_id + "|" + field + "|" + str(sample_index))
```

Three properties fall out, all required:

- re-running an entry reproduces the same order — so a changed answer means the *model* changed its
  mind, and the disagreement rate measures something real
- two entries never share an order — so a position bias cannot systematically favour the same *value*
- the order is reproducible from the id, so provenance need not store it

**`sample_index` must be inside the seed.** Three votes over three identical orders is one sample with
extra steps, and it is the obvious way to build this wrong.

### Resolving a vote

| Outcome | Result |
|---|---|
| 3-0 | the value, `confidence: high` |
| 2-1 | the majority, `confidence: split`, **minority recorded** |
| 1-1-1 | **`unresolved`** — never silently the first option |

A three-way split is a genuine signal that the entry is ambiguous. Defaulting it to the first option is
exactly where a default does the most damage.

### The disagreement rate is a deliverable, not diagnostics

Per field, over the corpus: how often the samples disagreed. It is the **only direct measurement of how
reliable a contract actually is**, and it closes a feedback loop nothing else closes:

- a high rate means a **weak description** (see §3) — and the fix is known
- a near-zero rate means the field is a **candidate to drop from the vote set**, halving its cost

---

## 3. A closed contract is not a closed enum

Owner, 2026-09-01:

> *"closed contract is not closed enum, it is well defined structure json, so LLM know how to generate
> each attribute in json because it understand the description of each attribute. each pipeline must
> cover 1 or some attributes."*

**Reliability comes from three things, and a frozen vocabulary is not one of them:**

1. **A defined JSON structure**, enforced by constrained decoding, so a malformed answer is
   *unsampleable* rather than merely detected afterwards.
2. **A description per attribute.** The model produces the right value because it understands what the
   field means — this is what JSON Schema `description` is for, and it is the part a
   "just make the list shorter" framing ignores entirely.
3. **Narrow pipelines.** One judgement per call.

### Write the negative clause

The most common enum error is a plausible *neighbouring* value. The sentence that prevents it is the
one saying what the field is **not**:

```json
"reach": {
  "enum": ["melee", "short", "long", "siege"],
  "description": "How far this creature can affect a target. 'melee' touches the adjacent cell only ... This describes REACH, not movement speed and not area of effect - a creature that walks fast but hits only what it touches is 'melee'."
}
```

**A description without a negative clause is half-written.** Make it a mechanical test over the schema,
not a review habit.

### `none` is a value; a missing key is a defect

From `game-design/02-unit-variables.md`: SC2's Archon, Ghost, Ravager, Baneling and Queen carry
**neither** Light nor Armored, which makes them immune to a large share of every bonus-damage term in
the game. **Tag absence is a stat.**

So a model that is unsure must say `none`. It must never be able to hand an entity a hidden advantage
by leaving a field out. Set `additionalProperties: false`, mark every field required, and let the
grammar make omission unsampleable.

---

## 4. Prove constrained decoding is actually on

`response_format: {"type": "json_schema", ...}` is enforced by llama.cpp through **GBNF grammar
sampling — for GGUF models**. A server or model that quietly ignores it returns prose, and **every
schema guarantee above becomes decorative** with no error anywhere.

**Prove it with one real call at the start of a run**, asking for a small object and checking the reply
shape. Proving enforcement costs one call; discovering it absent costs the whole run. This is check 6 in
`demon-seed/spec-dump-preflight.md`, and it is the one that would be skipped.

---

## 5. Retries: two intents, never conflated

`seedsmith/workflow/runner.py` states this in its own docstring, and it is there because the other way
costs real money:

> **TRANSIENT** (endpoint down, timeout, 5xx) → `resume()`: replay from checkpoint, **no new model
> call**. The previous answer is still wanted.
> **QUALITY** (a validator rejected the draft) → a genuinely **new** generation, with the defect named.

> *"idempotency breaks when outputs are stochastic, and a $50 batch retried 3x on a network blip costs
> $200."*

Regenerating on a network blip burns budget and churns output. Replaying a cached bad answer loops
forever. **A user-requested pause is TRANSIENT** — if pausing costs money and changes answers, nobody
pauses twice.

### Name the defect when re-prompting

`llm_caller.call_with_self_heal`: *"a bare retry teaches the model nothing; naming the reason is what
fixes it."* And bound it — **two repairs, then `unresolved`**. An unbounded repair loop is how a run
silently costs ten times its estimate.

---

## 6. Stochastic output breaks idempotency, and you will not notice

**This repo has already shipped this defect.** The commander-effect generator rewrote all 84 entries on
every run, because "already generated" was never checked — the output looked fine each time, and only a
byte-comparison found it.

The fix is a pattern, not a one-off:

1. Record in `_provenance` **what each entry was derived from** — corpus hash, prompt version, inputs.
2. `stale_ids()` compares **recorded value against current value**, never mtime.
3. A non-stale entry is **skipped**, not regenerated.
4. **Prove it by hash**: a second run over unchanged inputs produces byte-identical files. This is a
   test, not an aspiration.

Canonical serialisation is part of it — sorted keys, fixed indent, `\n`, explicit nulls, CJK
unescaped. Without it the hash churns and staleness stops meaning anything.

---

## 7. Every metric declares whether it can verify its own fix

Seedsmith P3, and it is what stops a green dashboard over prose nobody read:

| | Example | Verdict |
|---|---|---|
| **Closed-loop** | *"60 consumables have no flavour"* | detectable **and** the fix is machine-verifiable — may contribute to a pass |
| **Open-loop** | *"the flavour is generic"*, *"is this element actually right?"* | detectable, **not** verifiable by machine — produces a **review queue**, never a pass |

An open-loop metric that contributes to a pass verdict is a lie with a checkmark on it.

**Corollary — the audit does not override.** When a model audits a deterministic result (*"does the
lore support this rung?"*), a disagreement is **recorded**, not applied. A systematic pile of
disagreements in one range is a signal to retune a table — one human edit — not 900 per-entry model
overrides.

---

## 8. Tests never call a model

`tools/seedsmith/tests/test_offline_guarantee.py` exists for this. Every pipeline test stubs the
transport, and the stub **raises** on an unexpected call, so "makes no call" is provable rather than
assumed. A suite that quietly reaches a local model is a suite that fails on a different machine, in
CI, and at the worst possible moment.

---

## 9. Cost is a design input, not a footnote

Work it out before choosing a decomposition, because the numbers decide the shape:

```text
entries x pipelines                        = base calls
entries x voted_fields x (samples - 1)     = vote calls
```

Worked, for demon-seed at 904 species, 8 pipelines, 5 voted fields, 3 samples:

```text
904 x 8              =  7,232
904 x 5 x 2          =  9,040
                       ------
                       16,272 calls  ->  ~14 h on a local 26B model
```

**Three consequences that shape the architecture, not just the schedule:**

- a run that size **needs a state machine** — pause / resume / cancel / rerun — because it does not fit
  in one uninterrupted process
- **the vote set must be small.** Voting every field would triple it. Vote where being wrong is
  expensive to fix later, not everywhere
- **validate on a subset first.** A prompt change reviewed against 20 entries costs minutes; reviewed
  against the corpus it costs the whole run. Ship a `--dry-run` that renders prompts and calls nothing

---

## 10. The checklist

Before any pipeline or contract is built:

- [ ] No numeric field survives the schema audit — all four smuggling shapes tested
- [ ] Every attribute has a description **with a negative clause**
- [ ] Every closed enum admits `none`; `additionalProperties: false`; every field required
- [ ] Options permuted, seeded from the entity id **and** the sample index
- [ ] The vote set is named, small, and justified per field
- [ ] A 1-1-1 split yields `unresolved`, never the first option
- [ ] Constrained decoding proven by a real call before the run
- [ ] TRANSIENT and QUALITY retries are separate code paths; repair is bounded
- [ ] Provenance records what each entry was derived from; a rerun is byte-identical by hash
- [ ] Every metric declares closed-loop or open-loop
- [ ] The test suite passes with the transport stubbed to raise
- [ ] The call budget is computed, and a `--dry-run` exists

---

## 11. Related

- [../game-design/](../game-design/) — the *"what makes a good roster"* half; read both
- [../../architecture/seedsmith-map.md](../../architecture/seedsmith-map.md) — P1-P5
- [../../architecture/seedsmith/spec-pipeline.md](../../architecture/seedsmith/spec-pipeline.md) — guardrails, model selection, cost
- [../../architecture/seedsmith/spec-workflow-runtime.md](../../architecture/seedsmith/spec-workflow-runtime.md) — resume, fan-out, the retry split
- [../../architecture/item/seed-contract.md](../../architecture/item/seed-contract.md) — the four ownership levels
- [../../architecture/demon-seed-ideal.md](../../architecture/demon-seed-ideal.md) §4.7 — the bias research in full
