# Audit — seedsmith agent-runtime proposal (v3)

**Lens:** handed the proposal and told to build it — what is wrong, overstated, or unmeasured?
**Method:** adversarial re-reading plus **execution**: dependency inspection, an offline socket guard,
an 8-demon judgement-quality run against the real local model, and a corpus text-composition count.
**Date:** 2026-09-01. **Eight findings.** One invalidates a headline recommendation; two close open
questions with data; one is a defect in already-shipped code.

---

## R1 — ⛔ The proposal's own W-E-first recommendation is **wrong**, and the measurement says why

**The claim (v3 §6):** build `lore-enrich` first, because *"the taxonomy is thin because the input
text is thin."*

**Measured, and the diagnosis is wrong.** Counting `flavorInfo` across all 84 demons:

| | chars | share |
|---|---|---|
| stat / mechanic lines (`韧性：270+2200（一类）`, `伤害：20/1.5秒`, `融合配方：…`) | 4,276 | **70%** |
| prose | 1,815 | 29% |

The text is **not thin — it is diluted.** `motif-derive` draws 70% of its input from a stat table, so
it produces stat vocabulary rather than themes. Real committed output:

| demon | derived motifs | what they actually mean |
|---|---|---|
| `bucketnutzombie` | `一类`, `击杀` | **"armour-class one"**, "kill" |
| `cherrynut` | `伤害`, `僵尸` | "damage", **"zombie"** — a word in nearly every entry |
| `cactus` | `仙人掌`, `优先` | "cactus", **"priority"** (from `优先对空`, a targeting rule) |

**Consequence:** the prescription was an LLM workflow for a problem a deterministic filter solves.
`spec-pipeline.md:109` already states the rule this violates — *"Before writing a pipeline, ask
whether the task needs a model at all… A pipeline for work a script can do is a slow, expensive,
non-reproducible script."*

**Correction:** insert **W-0 — restrict motif derivation to prose** (drop `label：value` lines and
`特点`/`特性`/`弱点`/`融合配方` blocks; prefer `flavorIntroduce`, which is pure lore and present for
**18/84** demons). Deterministic, no model, no cost, testable. **It precedes every generation
workflow**, and it likely shrinks W-E's value substantially — re-evaluate W-E only after W-0's
motifs are visible.

---

## R2 — ✅ Q3 CLOSED by measurement: local Gemma-26B **is** sufficient for judgement work

The proposal deferred this as *"wants measuring."* Measured — 8 demons chosen for having **both**
motifs and anti-motifs (the hard case: a negative constraint), constrained decoding on, running the
same deterministic validators the workflow would:

| metric | result |
|---|---|
| first-attempt validator pass | **8/8 (100%)** |
| anti-motif violated on attempt 1 | **0/8** — the hardest constraint held |
| mean attempts to converge | **1.00** |
| latency mean / median / max | **3.2s / 3.2s / 3.3s** (n=8) |
| JSON parse failures | **0** (constrained decoding) |

**No hosted model is needed for W-B.** `spec-pipeline.md:108` routes cross-file reasoning to a
stronger model; that routing stays available but is **not required by evidence** for this workflow.
Re-measure for W-D, do not assume.

---

## R3 — ⚠️ …but that 100% measures **compliance, not quality**, and the content is mediocre

R2's validators check *presence* of a motif and *absence* of an anti-motif. They cannot check whether
a motif was used **well**. Reading the 8 real outputs, it shows:

> `cherrynut` → *"会以极高的 **伤害** 压制 **僵尸**"* — motifs inserted with spaces around them,
> visibly shoehorned to satisfy the checker.
> `bucketnutzombie` → *"**一类** 行为"* — "armour-class-one behaviour", which is not a concept.

**The validator has a blind spot and it is structural**, not a tuning issue: *"uses the token"* is
mechanically checkable, *"uses it meaningfully"* is not. Two consequences:

1. **A 100% pass rate must never be reported as quality.** This is the field's own
   *benchmark-90% → production-70-80%* gap, reproduced in miniature.
2. **This is what CoVe (v3 §7.3) is actually for** — and it is the strongest argument in the
   proposal for keeping it, now backed by a concrete failure rather than a principle.

R3 is downstream of R1: garbage motifs in, shoehorned prose out.

---

## R4 — ⛔ W-E as specified would **corrupt the `basis` honesty mechanism**

`basis` distinguishes *"supported by real game text"* (`text`) from *"a prior from the name"*
(`name`), and `Distribution/MotifSharing` **depends on that distinction** to exclude tautological
pairs (audit A2).

`lore-enrich` writes **synthetic** flavour text. If a demon then derives motifs from that text and
records `basis="text"`, the corpus can no longer distinguish *evidence* from *invention* — and the
tautology detector silently starts trusting generated text as ground truth.

**Fix, mandatory before W-E is specced:** a third value (`basis="enriched"`) or a separate
provenance flag, and `MotifSharing` must treat enriched text as **not-evidence** for exclusion
purposes. The proposal omitted this entirely.

---

## R5 — ⛔ The SQLite checkpointer contradicts an existing, documented precedent

v3 §5 proposes `SqliteSaver` for checkpointing. But
[`spec-demon-corpus-emit.md:28-30`](../spec-demon-corpus-emit.md) established the opposite reasoning
when it chose C# over Python **specifically to keep SQL out of `tools/`**:

> *"SQL belongs inside `FusionRpg.Data`. `guard-dal.ps1` would not catch a violation here — it scans
> `src/` only, and `tools/` is a documented blind spot — **so this is a case where the guard's
> silence is not permission.**"*

**Honest assessment — the precedent does not strictly forbid this**, because its stated concern is *"a
second SQL dialect for **the same tables**"* — i.e. reading game data from Python. A LangGraph
checkpoint store is unrelated ephemeral state, not game data. **But the proposal should have named
the tension rather than leaving it to be discovered.**

### ✅ RESOLVED — owner, 2026-09-01: `SqliteSaver`, and on a better argument than this finding's

> *"seedsmith is a tool outside the game, it is dev tool not ship in release"*

This finding reasoned narrowly (*same tables*). The owner's reasoning is **more general and more
correct**: `guard-dal` and the SQL invariant protect the **shipped game's** data layer, and
`tools/seedsmith/` never ships — not in the player zip, not in `dist/`, never on a player's machine.
**A tool that does not ship cannot violate the shipped architecture.**

⚠️ **Scope, so it cannot creep:** authorises `sqlite3` for **checkpoint state in `tools/seedsmith/`
only**. Python still may **not** read the game's SQLite (`types`, `almanac_seed`, `recipes`) — that
stays C#-through-the-DAL per `demon-corpus-emit`, for that spec's own stated reason.

---

## R6 — ⚠️ "6 direct deps" understated the real footprint by 5×

v3 §3 cited LangGraph's **6 direct** dependencies. The actual install is **~31 packages**, including
`langchain-core`, `langsmith`, `requests`, `httpx`, `orjson`, `PyYAML`, `aiosqlite`, `pydantic`.

Citing direct-only for a dependency-risk argument is exactly the kind of favourable framing this
repo's evidence rules exist to prevent. **`langsmith` — a telemetry client — is installed
automatically**, which matters against the standing rule *"the suite runs offline with no
credentials."*

**Verified, not assumed:** I ran a graph with a socket guard that raises on any non-loopback
connection. **No outbound call was attempted**; tracing is opt-in via `LANGSMITH_TRACING` /
`LANGCHAIN_TRACING_V2`, both unset. **Mitigation to add:** a test asserting those env vars are unset,
so the offline guarantee is enforced rather than trusted.

---

## R7 — ✅ Q1 CLOSED: Phase 0 first, and it is no longer a judgement call

Three verified facts settle it:

1. `tools/seedsmith/` declares **zero** dependencies — and `jieba` (D2.3) is imported but undeclared,
   so **a fresh clone fails a test today**.
2. The ambient conda env is broken for this work: `pydantic-ai 2.26.0` requires `openai>=2.45.0`
   against `openai 2.30.0` installed — a real `ImportError`.
3. Constrained decoding (§1) is a few lines, no dependency, and **removes a defect class already
   observed in this repo** (the `family-extract` prompt-format bug).

Phase 0 is not "tidy-up before the interesting work" — items 1 and 2 **block** the interesting work,
and item 3 is free risk reduction.

---

## R8 — ⚠️ Minor: a prompt defect visible in every R2 sample

7 of 8 outputs begin `"DOCTRINE: …"` — the model echoing the label from my prompt into the field
value. Harmless, but it is content pollution that no validator catches, and it would have shipped
into the corpus. **Add a validator: a field value must not begin with its own field name.** Cheap,
deterministic, and it generalises.

---

## Disposition

| # | Severity | Lands on | Action |
|---|---|---|---|
| R1 | **invalidates a recommendation** | v3 §6 | Insert **W-0** (deterministic prose filter) ahead of all generation; re-evaluate W-E after |
| R2 | closes Q3 | v3 §11 | Local model sufficient for W-B; re-measure for W-D |
| R3 | risk, structural | v3 §7 | Never report validator pass as quality; CoVe justified by evidence |
| R4 | **blocker for W-E** | v3 §6 | `basis="enriched"` (or a flag) **before** W-E is specced |
| R5 | ✅ **RESOLVED** | v3 §5/§11 | Owner chose `SqliteSaver` 2026-09-01 — seedsmith never ships, so the shipped-game invariant does not reach it. Scope pinned to checkpoints only |
| R6 | understated evidence | v3 §3/§4 | Correct to ~31 packages; add offline env-var assert |
| R7 | closes Q1 | v3 §11 | Phase 0 first — blocking, not preference |
| R8 | minor defect | v3 §7 | Add "field value must not echo field name" validator |

**Two findings (R1, R4) would have cost a rebuild if found during the spec phase instead of now.**
R1 in particular would have spent an entire LLM workflow on a problem a text filter solves.
