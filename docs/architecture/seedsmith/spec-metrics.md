# Seedsmith — `metrics`

**Status:** Proposed 2026-08-23. Nothing is built.

The check catalogue. [spec-analytics.md](spec-analytics.md) owns the mathematics; this document owns
the *contract* — what a metric is, what it emits, when it may gate, and how the catalogue proves it
covers what it claims to.

> Written after an audit found this module specced nowhere while every module around it had a
> document. The map listed it; six specs referenced it; none defined it.

---

## 1. What a metric is

```python
@dataclass(frozen=True)
class Metric:
    id:          str            # "Coverage/EmptyPartition" — family/name, stable forever
    family:      str            # Coverage | Linkage | Distribution | Balance | Registration |
                                # Constraint | Feasibility | ExemplarConformance | SemanticDedup | Quality
    loop:        Loop           # CLOSED | OPEN            (P3)
    gates:       bool           # may fail CI
    needs:       Needs          # corpus | budget | numerics | adapter
    covers:      list[str]      # Appendix-A defect ids this metric claims  (§5)
    def run(self, ctx) -> list[Finding]: ...
```

Four fields carry rules rather than data.

**`id` is stable forever.** It appears in findings, in work orders, in `budget` rows and in
suppression files. Renaming one silently breaks that chain, so a rename is a deprecation plus a new
id, never an edit.

**`loop`** is P3. A CLOSED metric can verify its own fix; an OPEN one cannot and therefore **may
never report a pass** — it reports *content written, awaiting review*, and pushes a sample to the
review queue. This is enforced by the base class, not by convention: `Loop.OPEN` with `gates=True`
raises at registration.

**`gates`** starts `False` for every new metric. Promotion to gating is a deliberate act taken once
the target is calibrated (§4). A metric that gates before anyone knows its correct threshold teaches
people to ignore the build, and that habit is expensive to reverse.

**`needs`** is declared so the runner can skip cleanly. A metric needing `budget` when no budget row
exists reports `NotMeasured`, never a pass — silence and success must be distinguishable.

---

## 2. The finding

```python
@dataclass(frozen=True)
class Finding:
    metric:     str          # Metric.id
    severity:   GAP | NOTE | NOT_MEASURED
    subject:    str          # entry id, partition, or dimension cell
    message:    str          # one sentence, names the thing and the rule
    evidence:   dict         # observed, expected, tolerance — the numbers behind the sentence
    assertion:  str | None   # CLOSED only: what must become true. The planner grades on this.
    remedy:     str | None   # machine-readable hint: which pipeline could close it
```

**`evidence` is mandatory and separate from `message`.** "Under target" is unusable; `observed 4,
expected 12, tolerance ±2` can be acted on, diffed between runs, and charted over time. Prose is for
humans, evidence is for tools, and collapsing them loses both.

**`assertion` is what makes a work order gradeable.** After a pipeline runs, the planner re-evaluates
the assertion. True ⇒ the job worked. False ⇒ **pipeline defect, not content defect** — a distinction
that was invisible in the agentic build, where a failed generation and an unattempted partition
looked identical.

**Severity is three-valued on purpose.** `GAP` ships broken. `NOTE` is worth a glance and may be
intentional. `NOT_MEASURED` means the metric could not run — the value that stops an absent check
from reading as a healthy one, which is exactly how nine empty partitions survived three waves.

---

## 3. Families

Ten families, each answering one question. Algorithms in analytics; this is the register.

| Family | Question | Loop | Analytics |
|---|---|---|---|
| **Coverage** | Is anything simply absent? | CLOSED | §2 |
| **Linkage** | Is it reachable and completable? | CLOSED | — (absorbed) |
| **Registration** | Is anything acquirable missing from every table? | CLOSED | — (absorbed) |
| **Distribution** | Is anything over- or under-represented? | CLOSED | §1 |
| **Balance** | Do resolved magnitudes sit in their envelope? Is the ladder monotone? | CLOSED | §5, numerics §3.3 |
| **Constraint** | Are rules that live only in lane documents actually held? | CLOSED | — |
| **Feasibility** | Can the planned allocation be satisfied at all? | CLOSED | §3 |
| **ExemplarConformance** | Does each exemplar validate as real content of its kind? | CLOSED | — |
| **SemanticDedup** | Do two entries say the same thing? | CLOSED | §6 |
| **Quality** | Is the writing present, varied, on-theme? | **OPEN** | §7 |

**Constraint deserves its own note.** It is the family with no clever algorithm and the highest
historical defect count: the `jewel-minor` ban, the 8-of-15 role quota, the one-per-(role, band,
axis) rule, the 6-role set cap, the hybrid-core requirement. Every one lived only in prose, every one
was violated, and each is a five-line predicate. The family exists to make "a rule that is written
down but not checked" impossible to leave lying around — a constraint metric is the cheapest thing
in this document and it prevented none of those defects only because nobody wrote it.

---

## 4. Calibration — how a metric earns the right to gate

New metric → `gates=False`, runs, reports. Its numbers get looked at against a corpus believed
healthy. *Then* a threshold goes into `budget` and `gates` flips.

This matters most for the index-based metrics. Nobody can name a correct Pielou evenness in advance;
a threshold guessed at now is a coin flip that either never fires or fires constantly. Measure, look,
set, gate — in that order, and the order is not optional.

**Suppression is per-finding, expiring and reasoned:**

```json
{ "metric": "Distribution/RoleSkew", "subject": "retinue/plant",
  "until": "2026-10-01", "reason": "retinue thin by design pending the summon rework" }
```

No permanent, blanket, unreasoned suppression — that is how a gate quietly becomes decoration.

---

## 5. Proving the catalogue is complete

Appendix A of the map lists every defect class the agentic build actually produced. Each metric
declares `covers`, and a test asserts:

- Every Appendix-A row owned by seedsmith is claimed by **at least one** metric.
- Every `covers` entry names a **real** Appendix-A row.
- Unclaimed rows are printed by `seedsmith metrics --coverage` as **known gaps**, not silently
  absent.

That last point is the whole discipline. The nine empty partitions were invisible for three waves
because nothing enumerated what *should* have been checked. The catalogue's own coverage is a
first-class output, so a missing metric is visible the same day rather than three waves later.

**Currently unclaimed and known:** `SemanticDedup` conceptual clustering (analytics §6.3) is blocked
on the 516-word adjective `axis` registry addition. It is listed as a gap, not implemented against
the wrong grouping.

---

## 6. Runner

Metrics are pure: `(corpus, budget, numerics, adapter) → list[Finding]`. No I/O, no mutation, no
ordering dependence — the runner may execute them in any order or in parallel, and a metric that
needs another metric's output is a design error.

Cost is bounded by analytics §10; the runner records per-metric wall time and fails the build if the
suite exceeds its budget, because a check nobody can afford to run is a check that gets skipped.

Every metric ships with: one fixture that must trip it, one that must not, and — for CLOSED metrics
— a fixture proving the `assertion` flips true when the defect is fixed. Fixtures are synthetic,
never the live corpus, so tests keep testing after the corpus is repaired.
