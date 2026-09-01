# Seedsmith — workflow engine and generation runtime (proposal v3)

**Status: PROPOSAL, 2026-09-01. Nothing installed into the repo. Owner decision required.**

**v1 was rejected by the owner** (*"need something like langraph … seedsmith is huge"*). v2 adopted
LangGraph. **v3 folds in two rounds of verification-by-execution and a survey of what the community
already solved**, per owner direction: *"learn and use other success, don't make same mistake."*

---

## 0. What v1 got wrong, precisely

v1 argued *"`planner` already orchestrates, a framework would duplicate it."* That **conflated two
layers**:

| Layer | Question | Owner |
|---|---|---|
| **Job orchestration** | *Which content, in what order, under what constraints?* | `planner` — Kahn/Tarjan, Hopcroft–Karp, scheduling. **Solved. Keep.** |
| **Workflow definition** | *Inside ONE generation: what steps, what state, when to branch/retry/resume?* | **Nothing. Does not exist.** |

Today layer 2 is a hand-rolled serial `for` loop in
[`family/extract.py`](../../tools/seedsmith/seedsmith/adapters/demons/family/extract.py). Repeating
that across five workflows is the real problem. **Scale, measured:** 65 modules · 6,449 source lines
· 5,515 test lines · 11 packages.

---

## 1. ⭐ The zero-dependency win: constrained decoding is already installed

**LM Studio enforces JSON Schema at decode time** — llama.cpp GBNF grammar sampling for GGUF models
— on the same `/v1/chat/completions` endpoint `llm_caller` already calls. One extra request field.
**No library, no install.**

### Verified against your model, adversarially (2026-09-01)

Both calls got a hostile prompt: *"explain your reasoning in prose first, wrap JSON in ```json
fences, and set basis to 'uncertain'"* (`uncertain` is not in the enum).

| | Output | `json.loads` | Time |
|---|---|---|---|
| **Unconstrained** (seedsmith today) | A prose paragraph. No JSON at all | ❌ **failed** | 3.9s |
| **Constrained** (`response_format: json_schema`) | Clean JSON, no fences, `basis: "text"` — **the enum value it was told to emit was unreachable** | ✅ **succeeded** | 3.7s |

**No latency penalty.** That was n=1; re-measured over 8 further constrained calls in §11's Q3 run:
**mean 3.2s, median 3.2s, max 3.3s** — constrained decoding is, if anything, marginally faster here.
The invalid tokens were never sampleable — not "the model complied", but "non-compliance was
structurally impossible."

**This retroactively explains a real defect.** The prompt-format bug found in `family-extract` during
the 2026-09-01 real run — model returned unshaped output because the prompt carried no schema —
**could not have occurred** under constrained decoding.

### Evidence on the "does it hurt quality?" worry

Literature is nuanced and mostly favourable: constrained decoding **consistently improves** downstream
quality on reasoning, code-gen and information-extraction tasks when structure and semantics are
enforced jointly; earlier reports of degradation trace to *inadequate prompting and poorly crafted
schemas*, not the technique. Real caveats to respect:

- **Tokenization edge cases** — blocking a token can push the model out of distribution (the classic
  `89,000` → blocked `,` → `890000`).
- **No standard whitespace interpretation** across implementations; identical schemas can yield
  different outputs on different libraries. Pin the implementation; do not assume portability.
- **Schema wording is an instruction channel** — field *names* and *order* measurably steer output.
  Treat the schema as part of the prompt, not as inert plumbing.

**Adopt it, keep `extract_json` as defense-in-depth**, and keep post-hoc validation. Belt and braces.

---

## 2. What the community already learned (and what we take)

Surveyed 2026-09-01. Each row is a failure the field has measured; the right column is what this
proposal does about it — **the point is not to rediscover any of these.**

| # | Documented failure | What we adopt |
|---|---|---|
| **1** | **Agents that don't stop — 28.1% of all observed failures** (step repetition 15.7%, unaware of termination 12.4%). The single largest category | Bounded loops are **structural**, not advisory: explicit `attempts` in state, LangGraph `recursion_limit`, and an `escalate` terminal node. The probe already exercised this — `attempts: 2` then persist. **No unbounded `while` anywhere** |
| **2** | **Idempotency breaks when output is stochastic.** Retry-for-*transient-failure* (want the exact cached result) and retry-for-*bad-output* (want a genuinely new generation) are **opposite intents**; conflating them is where production bugs are born. A $50 job retried 3× costs $200 | Two distinct mechanisms, never one: **transient retry** = replay from checkpoint, no new model call; **quality retry** = a deliberate new generation with the defect named. G2's `_provenance` already records enough to tell them apart — this makes the distinction explicit in the graph |
| **3** | **Context amnesia / window overflow** from large intermediate outputs accumulating across steps | State is a **typed `TypedDict` with bounded fields**, not an ever-growing message list. Nodes pass IDs and small structs, not transcripts. Seedsmith's per-partition narrow scope (`spec-pipeline.md` §3.2) already aligns |
| **4** | **Cascading errors in multi-agent systems**; inter-agent misalignment is a named failure category | **Confirms rejecting CrewAI/AutoGen.** One graph, one model call per node, deterministic edges. No agent-to-agent negotiation |
| **5** | **Model updates cause silent regression** | Pin the model string in provenance (already done, G2). Add golden-output regression tests over a small fixture set |
| **6** | **Benchmark 90% → production 70-80%** once consistency and faults are counted | Do not trust a passing fixture as production evidence. Every workflow needs a **real-corpus run** before it is called done — the discipline that found 3 real defects on 2026-09-01 |
| **7** | **Evaluation/monitoring drift** — unrefreshed test sets and thresholds rot faster than governance | The eval set is versioned corpus data, and `metrics/` already re-runs against the live corpus |

---

## 3. Recommendation: **LangGraph** for workflow definition — verified by execution

Installed into a throwaway venv and tested against your real model before recommending.

| Claim | Result |
|---|---|
| **C1 — nodes are plain functions; no LangChain LLM abstraction** | ✅ Probe calls LM Studio with **stdlib `urllib`**, exactly like `llm_caller`. LangGraph never sees a LangChain model object |
| **C2 — conditional edges express generate → validate → repair, bounded** | ✅ **`attempts: 2`** — first draft failed the deterministic validator, defect fed back, second passed. Real repair cycle |
| **C3 — checkpoint survives crash, resumes** | ✅ **7 checkpoints in SQLite**, `get_state_history()` replays |
| **C4 — structure inspectable/testable with no model** | ✅ Nodes enumerable, mermaid emitted, assertable offline |

Real motif-constrained output from C2:
```json
{"name": "坚果", "doctrine": "Creates an impenetrable 外壳 to provide 保护 for the rest of the squad through its 坚硬 defense."}
```
The validator *forced* the demon's own motifs (`保护`/`坚硬`/`外壳`). Attempt 1 didn't use them, was
rejected **mechanically**, attempt 2 complied.

### Alternatives at this layer

| Option | Verdict |
|---|---|
| **LangGraph** | ✅ **Recommended.** Graph = workflow. Plain-function nodes. Checkpoint/resume. Offline-inspectable. **~31 transitive packages** (corrected — see §4) |
| LlamaIndex Workflows | Viable; weaker checkpointing story |
| Prefect / Dagster / Temporal | Real engines, but infra-DAG shaped — daemon/ops overhead for a local dev tool |
| Burr | Conceptually good fit, much smaller community |
| CrewAI / AutoGen | ❌ Reject — see §2 row 4 |

---

## 4. Dependency risk (owner: *"langraph has frequently update"*)

Measured: **10 releases 2026-04 → 8 May → 5 Jun → 3 Jul → 1 Aug.** It moves; cadence is settling; on
stable `1.x`.

⛔ **Correction (audit R6): v2/v3 cited "6 direct deps". The real install is ~31 packages** —
`langchain-core`, **`langsmith`** (a telemetry client), `requests`, `httpx`, `orjson`, `PyYAML`,
`aiosqlite`, `pydantic`, and more. Citing direct-only for a dependency-risk argument was favourable
framing, and the evidence rules exist to prevent exactly that.

**Offline guarantee — verified, not assumed.** I ran a graph with a socket guard that raises on any
non-loopback connection: **no outbound call was attempted.** Tracing is opt-in via
`LANGSMITH_TRACING` / `LANGCHAIN_TRACING_V2`, both unset. **Mitigation to build:** a test asserting
those env vars are unset, so *"the suite runs offline with no credentials"* is enforced rather than
trusted.

1. **Narrow API surface, deliberately.** Probe used only `StateGraph`, `add_node`, `add_edge`,
   `add_conditional_edges`, `compile`, `invoke`, `get_state_history`, `SqliteSaver`. **No prebuilt
   agents, no tool abstractions, no `langchain` proper.**
2. **Exact pins + lockfile.** `langgraph==1.2.11`, never `>=`.
3. **A seam, not a spread.** LangGraph imported **only** in `workflow/graphs/`. Node bodies live in
   `workflow/nodes/` and import nothing from it. **Rip LangGraph out and every node survives** — only
   thin wiring is rewritten. This is the most important constraint in the document.
4. **Offline structural tests (C4)** catch a breaking upgrade with no model and no network.

⚠️ **Prerequisite defect — `tools/seedsmith/` declares ZERO dependencies.** No `pyproject.toml`, no
lockfile. **I made this worse**: D2.3's `jieba` is imported and declared nowhere, so **a fresh clone
now fails one test** with `ModuleNotFoundError`. My debt; Phase 0.

⚠️ **Ambient conda env is not a viable base:** `pydantic-ai 2.26.0` there needs `openai>=2.45.0`,
but `openai 2.30.0` is installed — a real `ImportError` I hit. Isolated pinned venv is prerequisite.

---

## 5. Architecture

```
                    ┌─────────────────────────────────────────────┐
  metrics ─────────▶│ planner        (KEEP — job orchestration)   │
  (findings)        │ what to generate, in what order             │
                    └──────────────────┬──────────────────────────┘
                                       │ work order
                    ┌──────────────────▼──────────────────────────┐
                    │ workflow/       (NEW — LangGraph)           │
                    │  START → brief → generate → validate        │
                    │    → (defects? → repair, BOUNDED §2.1)      │
                    │    → cove_verify → persist | escalate       │
                    │  + SqliteSaver checkpoint/resume (§2.2)     │
                    └──────────────────┬──────────────────────────┘
                                       │ node bodies = plain functions
  briefkit ── llm_caller(+response_format §1) ── audit_schema ── metrics ── corpus
                    (ALL KEPT AND REUSED — no rewrite)
```

```
workflow/
  state.py       # TypedDict, BOUNDED fields (§2.3) — the contract between nodes
  nodes/         # plain functions. NO langgraph import. Unit-testable alone
    brief.py  generate.py  validate.py  cove.py  persist.py
  graphs/        # ONLY files importing langgraph — thin wiring
    lore_enrich.py  commander_effect.py  item_theme.py  aspect.py
  runner.py      # fan-out over a work order, bounded concurrency, checkpoint config
```

---

## 6. Workflows — **revised by audit R1**

⛔ **v3's "W-E first" recommendation was WRONG and is withdrawn.** The audit
([review/audit-agent-runtime-proposal.md](seedsmith/review/audit-agent-runtime-proposal.md) R1)
measured the actual corpus: the flavour text is **not thin, it is diluted**.

| `flavorInfo` across all 84 demons | chars | share |
|---|---|---|
| stat / mechanic lines (`韧性：270+2200（一类）`, `伤害：20/1.5秒`, `融合配方：…`) | 4,276 | **70%** |
| prose | 1,815 | 29% |

`motif-derive` draws **70% of its input from a stat table**, so it emits stat vocabulary. Real
committed output: `bucketnutzombie` → `一类` (**"armour-class one"**), `cherrynut` → `僵尸`
(**"zombie"**, a word in nearly every entry), `cactus` → `优先` (**"priority"**, from a targeting rule).

Prescribing an LLM workflow for this would have violated `spec-pipeline.md:109` — *"a pipeline for
work a script can do is a slow, expensive, non-reproducible script."*

| id | Workflow | Model? | Status |
|---|---|---|---|
| **W-0** | **restrict motif derivation to prose** — drop `label：value` lines and `特点`/`特性`/`弱点`/`融合配方` blocks; prefer `flavorIntroduce` (pure lore, present for **18/84**) | **No** | ⭐ **Build first.** Deterministic, free, testable |
| **W-B** | `commander-effect` per demon | Yes | Second — measured 8/8 first-attempt (§R2) |
| **W-D** | item/action content themed to a demon | Yes | Third |
| **W-E** | `lore-enrich` | Yes | **Re-evaluate after W-0** — its value likely shrinks a lot. ⛔ **Blocked on R4**: needs `basis="enriched"` first (see §7a) |
| **W-A** | `aspect` per demon | Yes | ⛔ blocked: `aspect-scope` approved but unbuilt |
| **W-C** | `environment` | **No** | ❌ deterministic mapping — same rule as W-0 |

### 7a. ⛔ W-E prerequisite — `basis` corruption (audit R4)

`basis` distinguishes *"supported by real game text"* from *"a prior from the name"*, and
`Distribution/MotifSharing` **depends on it** to exclude tautological pairs (A2). `lore-enrich`
writes **synthetic** text. If motifs derived from generated prose record `basis="text"`, the corpus
can no longer tell evidence from invention, and the tautology detector starts trusting generated text
as ground truth.

**Mandatory before W-E is specced:** a third value `basis="enriched"` (or a separate provenance
flag), and `MotifSharing` must treat enriched text as **not-evidence**.

---

## 7. Quality layer — cheapest and most certain first

1. **Constrained decoding (§1)** — shape defects structurally impossible. Free.
2. **Deterministic validators** (no model): `audit_schema`, `audit_open_loop_schema`,
   `CITATION_PATTERNS`, motif-coverage, anti-motif violation, `SemanticDedup`, **plus "a field value
   must not begin with its own field name"** (audit R8 — 7 of 8 measured outputs began `"DOCTRINE:"`,
   echoing the prompt label into the value; nothing caught it).
3. **CoVe** — only for what a validator cannot decide. Generate verification questions → answer them
   **against source text independently** → revise. Independence is the mechanism; a verifier shown
   its own answer rubber-stamps it.
4. **Self-consistency (n=3)** — only where measurement shows CoVe insufficient. Triples cost.
5. **CoT — rejected by default.** `llm_caller` already sends `reasoning_effort: "none"` deliberately.

### ⚠️ 7b. A 100% validator pass rate is NOT quality (audit R3)

Measured: 8/8 first-attempt pass, 0/8 anti-motif violations. **Reading the same outputs shows
mediocre content**: `cherrynut` → *"会以极高的 **伤害** 压制 **僵尸**"*, motifs inserted with spaces
around them, visibly shoehorned to satisfy the checker; `bucketnutzombie` → *"**一类** 行为"*
("armour-class-one behaviour"), which is not a concept.

**The blind spot is structural, not tunable:** *"uses the token"* is mechanically checkable, *"uses it
meaningfully"* is not. This reproduces the field's own *benchmark-90% → production-70-80%* gap in
miniature, and it is **the concrete evidence justifying CoVe** — no longer a principle, a measured
failure. **Never report a validator pass rate as a quality result.**

---

## 8. Observability — deferred, deliberately, but named

The field converged on trace-first platforms: **Langfuse** (open-source, self-hostable, OTel-style),
**LangSmith** (LangGraph-native), **Braintrust** (eval-first), Arize, Opik.

**Recommendation: none in Phase 1.** LangGraph's SQLite checkpoints already give per-step state
replay — the debugging need — with zero services. **Revisit Langfuse** (self-hostable, no vendor
lock, matches this repo's offline/no-credentials rule) once there are ≥3 live workflows and a real
regression question. Adding a tracing service before there is traffic to trace is cost without signal.

---

## 9. Build order

- **Phase 0 — dependency hygiene + constrained decoding. Blocking (R7).** `pyproject.toml`, exact
  pins, lockfile, declare the `jieba` debt, isolated venv, CI installs from lockfile, **plus the
  offline env-var assert (R6)**. **Plus §1's `response_format`** — a few lines in `llm_caller`, no
  dependency, strictly reduces risk. **Done when a fresh clone passes 395/395.**
- **Phase 0.5 — W-0, the deterministic motif fix (R1).** No model, no framework, no dependency.
  Restrict motif derivation to prose. **This is the highest value-per-cost item in the whole
  proposal** and it does not need any of Phase 1.
- **Phase 1 — `workflow/` skeleton.** State contract, node library, one graph (W-B, already probed),
  checkpointing (owner's choice per §11), offline structural tests, bounded async fan-out, the §2.2
  dual-retry split.
- **Phase 2 — W-B** end-to-end with the §7 quality layer, on the improved motifs from Phase 0.5.
- **Phase 3 — W-D.** Re-measure local-vs-hosted (R2 covers W-B only).
- **Phase 3.5 — re-evaluate W-E.** Only if W-0's motifs are still insufficient, and only after
  `basis="enriched"` (§7a) exists.
- **Phase 4 — close the measurement loop.** Merge generated content onto corpus entries so
  `Coverage/DemonUncovered` falls from 84 and `Distribution/MotifSharing` can finally measure.
- **W-A** unblocks when the demon program builds `aspect-scope`.

---

## 10. Cost

Local ⇒ cost is wall-clock. Measured: **84 demons / 11 batches / 104s** single-shot. Per-item graphs
with repair + CoVe run **~3–5× calls/item**; 252 items (84 × 3 kinds) ≈ **30–90 min**. This is why
checkpoint/resume is a **requirement** (§2.2), not a nicety — a crash at minute 50 must not restart
from zero.

---

## 11. Open questions — **all three CLOSED by measurement**

Audit: [review/audit-agent-runtime-proposal.md](seedsmith/review/audit-agent-runtime-proposal.md).

1. ~~Phase 0 first, or W-B first?~~ ✅ **CLOSED — Phase 0, and it is not a judgement call** (R7).
   Three verified facts: `jieba` is imported but undeclared so **a fresh clone fails a test today**;
   the ambient env is broken (`pydantic-ai 2.26.0` needs `openai>=2.45.0`, has `2.30.0` — a real
   `ImportError`); and constrained decoding removes a defect class **already observed in this repo**.
   Items 1–2 *block* the work; item 3 is free.
2. ~~W-E or W-B first?~~ ✅ **CLOSED — neither. W-0 first** (R1). Measured 70/29 stat-to-prose split
   means the fix is a deterministic text filter, not an LLM workflow. Then W-B. **W-E is deferred and
   additionally blocked on R4's `basis="enriched"`.**
3. ~~Local Gemma-26B, or hosted?~~ ✅ **CLOSED — local is sufficient for W-B** (R2). 8/8
   first-attempt validator pass, **0/8 anti-motif violations** (the hardest constraint), mean 1.00
   attempts, 3.2s mean latency, 0 JSON failures. Hosted routing stays available but **is not
   required by evidence**. Re-measure for W-D; do not assume.

### ✅ R5 CLOSED — owner decision 2026-09-01: **Python/SQLite checkpointing (`SqliteSaver`)**

> *"seedsmith is a tool outside the game, it is dev tool not ship in release"* — owner

**The owner's reasoning is stronger than the audit's**, and it is the one to record. The audit argued
the precedent narrowly ([`spec-demon-corpus-emit.md:28-30`](seedsmith/spec-demon-corpus-emit.md),
*"a second SQL dialect for **the same tables**"*). The correct, more general reason:

**`guard-dal.ps1` and the "SQL only in `FusionRpg.Data`" invariant exist to protect the *shipped
game's* data layer.** `tools/seedsmith/` is dev-time tooling — it is not in the player zip, not in
`dist/FusionRpg.Server`, not loaded by the injector, and never executes on a player's machine. **A
tool that does not ship cannot violate the shipped architecture.** The `tools/` "blind spot" in
`guard-dal` is therefore not an oversight to tiptoe around here; it is the boundary working as
intended.

**Scope of this decision, stated so it cannot creep:** it authorises `sqlite3` for **LangGraph
checkpoint state inside `tools/seedsmith/` only**. It does **not** authorise Python reading the
game's SQLite (`types`, `almanac_seed`, `recipes`) — that remains C#-through-the-DAL, exactly as
`demon-corpus-emit` established, and for exactly the reason that spec gives.

**Consequence:** `langgraph-checkpoint-sqlite` is a pinned Phase 0 dependency; crash-resume (§10's
30–90 minute runs) is preserved; `MemorySaver` and the custom JSON checkpointer are not needed.

---

## Sources

Constrained decoding / structured output: [LM Studio Structured
Output](https://lmstudio.ai/docs/developer/openai-compat/structured-output) ·
[DeepWiki: LM Studio schema validation](https://deepwiki.com/lmstudio-ai/docs/8.1-structured-output-and-schema-validation) ·
[JSONSchemaBench (arXiv 2501.10868)](https://arxiv.org/pdf/2501.10868) ·
[JSONSchemaBench OpenReview](https://openreview.net/forum?id=FKOaJqKoio) ·
[Constrained Decoding (JSON-mode) overview](https://www.emergentmind.com/topics/constrained-decoding-json-mode) ·
[Top 5 Structured Output Libraries 2026](https://dev.to/thedailyagent/top-5-structured-output-libraries-for-llms-in-2026-48g0) ·
[8 LLM Structured Output Libraries Ranked](https://techsy.io/en/blog/best-llm-structured-output-libraries) ·
[BAML vs Instructor](https://www.glukhov.org/llm-performance/benchmarks/baml-vs-instruct-for-structured-output-llm-in-python/)

Failure modes: [Failure Modes in Production Multi-Agent LLM Systems
(SSRN)](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=7041478) ·
[Failure Modes in LLM Systems: A System-Level Taxonomy (arXiv 2511.19933)](https://arxiv.org/pdf/2511.19933) ·
[Why AI Agents Fail in Production (Winder.ai)](https://winder.ai/why-ai-agents-fail-in-production/) ·
[Why AI Agent Demos Fail in Production (ODSC)](https://opendatascience.com/why-ai-agent-demos-fail-in-production/) ·
[LLM evaluation gaps](https://nhimg.org/articles/llm-evaluation-gaps-show-why-production-monitoring-still-fails/)

Idempotency / cost: [Idempotency Is Not Optional in LLM
Pipelines](https://tianpan.co/blog/2026-04-20-idempotency-llm-pipelines) ·
[LLM Inference at Scale](https://designgurus.substack.com/p/llm-inference-at-scale-batching-caching)

Observability: [Top LLM Observability & Evaluation Platforms 2026
(MarkTechPost)](https://www.marktechpost.com/2026/08/09/top-llm-observability-and-evaluation-platforms-in-2026-langfuse-langsmith-braintrust-arize-and-more-compared/) ·
[Best LLM Observability Tools (Firecrawl)](https://www.firecrawl.dev/blog/best-llm-observability-tools)
