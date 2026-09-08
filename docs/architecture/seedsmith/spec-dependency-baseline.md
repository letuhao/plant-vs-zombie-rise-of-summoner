# Spec: `dependency-baseline`

Module `dependency-baseline` in the [seedsmith map](../seedsmith-map.md) §3d. **Blocking prerequisite
for the whole feature.** Depends on nothing.

Proposal: [seedsmith-agent-runtime-proposal.md](../seedsmith-agent-runtime-proposal.md);
`R#` = [audit](review/audit-agent-runtime-proposal.md) findings.

**Status: SEALED — approved by the owner 2026-09-01. Authorized to build.**

---

## 1. Objective

Make `tools/seedsmith/` installable, reproducible and offline-verifiable — and close the
constrained-decoding gap while we are in there.

**This is not tidy-up before the interesting work. It blocks the interesting work.** Two verified
facts (R7):

1. `tools/seedsmith/` declares **zero** dependencies — no `pyproject.toml`, no `requirements.txt`, no
   lockfile. It has been stdlib-only by convention, and that convention silently broke on
   2026-09-01 when `jieba` was added to `adapters/demons/motifs.py` (owner-approved) and declared
   nowhere. **A fresh clone fails one test today** with `ModuleNotFoundError`.
2. The ambient conda environment is not a viable base: `pydantic-ai 2.26.0` there requires
   `openai>=2.45.0` against `openai 2.30.0` installed — a real `ImportError`, reproduced.

**Done means:** a fresh clone, in a clean venv, installs from a lockfile and passes the full suite.

---

## 2. Design

### 2.1 `pyproject.toml` with exact pins, never ranges

Every dependency is `==`, not `>=`. LangGraph released **10 times in 2026-04 and 1 in 2026-08**
(measured) — it moves, and this program's whole culture is byte-identical reproducibility. A range
means two clones can run different code and both call themselves green.

Declared runtime deps, and why each is present:

| Package | Why | Introduced by |
|---|---|---|
| `jieba` | Chinese word segmentation — a regex cannot segment a language with no spaces (verified: whole clauses were captured as single "motifs") | D2.3, **currently undeclared — this is the debt** |
| `langgraph` | Workflow definition | `workflow-runtime` |
| `langgraph-checkpoint-sqlite` | Crash-resume for 30–90 minute runs | `workflow-runtime` (owner-approved 2026-09-01) |

Nothing else. `pydantic-ai` is **not** adopted (superseded by constrained decoding + LangGraph);
`instructor`, `outlines`, `crewai`, `autogen`, `dspy` are all explicitly rejected in the proposal.

### 2.2 The transitive footprint is ~31 packages, and the spec says so

R6: the proposal originally cited *"6 direct deps"*, which was favourable framing for a
dependency-**risk** argument. The real install includes `langchain-core`, **`langsmith`**,
`requests`, `httpx`, `orjson`, `PyYAML`, `aiosqlite`, `pydantic`. A lockfile records all of them; the
spec does not pretend the number is small.

### 2.3 ⛔ The offline guarantee becomes a test, not a trust

`langsmith` — a telemetry client — is installed transitively. The program's standing rule is
*"the suite runs offline with no credentials."*

**Verified already:** a graph run under a socket guard that raises on any non-loopback connection
attempted **no outbound call**; tracing is opt-in via `LANGSMITH_TRACING` / `LANGCHAIN_TRACING_V2`,
both unset. **That verification becomes a permanent test**, because a guarantee nobody re-checks is a
guarantee that expires quietly — which is this repo's most-repeated failure shape.

### 2.4 Constrained decoding in `llm_caller` — the free risk reduction

LM Studio enforces JSON Schema at **decode time** (llama.cpp GBNF for GGUF), on the endpoint
`llm_caller` already calls. One optional field on the request body:

```python
"response_format": {"type": "json_schema",
                    "json_schema": {"name": ..., "strict": True, "schema": ...}}
```

**Measured, adversarially** — a prompt demanding prose, markdown fences and an out-of-enum value:

| | `json.loads` | latency |
|---|---|---|
| Unconstrained (today) | ❌ returned a prose paragraph, no JSON at all | 3.9s |
| Constrained | ✅ valid JSON, enum respected, no fences | 3.7s (mean 3.2s over 8 further calls) |

**This removes a defect class already observed in this repo** — the `family-extract` prompt-format
bug found 2026-09-01 could not have occurred.

**Optional, never mandatory.** `schema=None` keeps today's behaviour exactly, so no existing caller
changes. `extract_json`'s fence-stripping and regex fallback **stay** as defense-in-depth — the
literature is clear that schema portability across implementations is not guaranteed (whitespace
handling is unspecified in JSON Schema and tokenizers are whitespace-sensitive).

### 2.5 What this module does not do

It does not adopt LangGraph *usage* (that is `workflow-runtime`), does not touch generation logic,
and does not change any existing caller's behaviour.

---

## 3. Commands

```powershell
cd tools\seedsmith
python -m venv .venv; .\.venv\Scripts\Activate.ps1
pip install -e ".[dev]"            # from pyproject, resolved against the lockfile
python -m pytest -q                # must be 395/395 in a FRESH venv
python -m pytest tests/test_offline_guarantee.py -q
```

---

## 4. Project structure

```
tools/seedsmith/
    pyproject.toml            → NEW: metadata, exact pins, [dev] extra
    requirements.lock         → NEW: fully resolved transitive set
    seedsmith/pipeline/llm_caller.py   → EDIT: optional response_format
    tests/test_offline_guarantee.py    → NEW: env-var + socket assertions
    tests/test_llm_caller.py           → EDIT: constrained-decoding cases
```

CI gains one step: install from the lockfile in a clean environment, then run the suite. Per the
known CI defect (only the **last** `dotnet test` exit code is checked in `ci.yml`), this step must be
positioned or written so its failure cannot be masked.

---

## 5. Code style

`pyproject.toml` is data, not code — no dynamic version computation. `response_format` is threaded as
an optional parameter with a `None` default; no global state, no config singleton. The offline test
uses stdlib `socket` patching, no new dependency to test that we have few dependencies.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Fresh venv, install from lockfile, run suite | **the full suite passes, zero failures** — 395/395 at the time of writing, but the criterion is "passes", **not a fixed number** (audit S9: the count drifts the moment any later module adds a test) |
| `import jieba` in a fresh venv | succeeds — the D2.3 debt is paid |
| `LANGSMITH_TRACING` / `LANGCHAIN_TRACING_V2` | asserted **unset** |
| A graph run under a non-loopback socket guard | **zero** outbound connection attempts |
| `call_model(schema=None)` | byte-identical request body to today — **no existing caller changes** |
| `call_model(schema=X)` | body carries `response_format`; response parses with plain `json.loads` |
| A schema with an `enum`, against a prompt demanding an out-of-enum value | the illegal value **cannot** appear |
| `extract_json` | still present and still tested — defense-in-depth is not deleted |

The "no existing caller changes" row is the one that matters most: this module must be provably
inert for every current call site.

---

## 7. Boundaries

- **Always:** exact `==` pins; commit the lockfile; keep `response_format` optional; keep
  `extract_json`.
- **Ask first:** adding any dependency beyond the three in §2.1; making `response_format` mandatory;
  upgrading LangGraph's pin.
- **Never:** a version range; a dependency that phones home by default; deleting `extract_json`;
  letting the offline guarantee become a comment instead of a test.

---

## 8. Success criteria

1. A fresh clone + clean venv + `pip install` + `pytest` → **full suite passes, zero failures**
   (395 at time of writing; the criterion is "passes", not the number — S9).
2. `jieba` is declared; the fresh-clone failure is gone.
3. Offline guarantee is a passing test, not a claim.
4. `response_format` works and is optional; no existing caller's behaviour changes.
5. Lockfile committed and CI installs from it.

---

## 9. Open questions

None. All decisions for this module were closed by the owner on 2026-09-01 (map §3d).
