# Plan: Perf v3 — frame-cost residue + server burst stability

Spec: [docs/architecture/perf-v3-spec.md](../docs/architecture/perf-v3-spec.md)
Evidence: [docs/research/perf/00-baseline.md](../docs/research/perf/00-baseline.md)
Prior round (complete, 12/12): event-pipeline-v2 — see spec/SSOT in docs/architecture; this file supersedes its plan after the owner commits.

## Dependency graph

```
Module A: injector-frame-cost                Module B: server-burst (independent)
  A1 instrument loop subsections  ──┐          B1 headless burst repro (crash proven)
  A2 verdict arithmetic fix         │              │
  (both independent, ship first)    │          B2 root-cause doc
                                    ▼              │
  A3 ptr-indexed pending (Core)   informs      B3 bounded ingest fix + test
  A4 incremental snapshot (Core)  A5 priority      │
  A5 auto-collect/continuous fix  (biggest     B4 burst re-run healthy
        (Injector, guided by A1)   offender
                                   first)
  A6 deploy + stress 300/600 re-run gate
  A7 review-findings fold-in (sized when the 5-axis review lands)
```

Vertical slicing note: A3/A4 are each a complete slice (Core change + tests + measurable
effect); A1 is the flashlight that orders A5's work. B runs fully parallel to A — different
process, headless verification, no shared files.

## Ordering rationale

- A1+A2 first: zero-risk, and A1's numbers decide whether A5 targets auto-collect,
  VfxDirector, or something unexpected — don't fix blind.
- A3 before A4: both touch drain/snapshot internals; A3 is smaller and de-risks the shared
  test harness.
- B1 before any server change: the crash must be reproducible on demand or the fix is a guess.
- A7 last-but-flexible: Critical review findings jump the queue.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Incremental snapshot breaks copy-on-freeze semantics (drain holds a frozen board while registry mutates) | High | A4 keeps immutable snapshot instances; incremental = maintain a *dirty template* cheaply, still materialize an immutable copy on freeze; tests assert frozen instance never mutates |
| Ptr index drifts from ring contents | Med | index is derived-only (built during append/pop); invariant test: index == scan of ring |
| Server fix suppresses XP events | High | burst test asserts XP-bearing kinds all present post-ingest |
| Auto-collect behavior change (collect radius/timing) | Low | registry path mirrors scan results; probe-only verification plus manual play check |

## Verification checkpoints

1. After A1+A2: build + suites + guards; deploy; one 300z run shows new sections, no dark cost.
2. After A3+A4: Core tests (new invariants incl. frozen-snapshot immutability) + all suites.
3. After A5: probe shows `cheat.autocollect`/`cheat.continuous` ≈ 0 scans.
4. Gate: stress 300 + 600 both PASS (corrected verdict). Module A done.
5. B1 crash repro documented → B3 fix → B4 same burst healthy + Data.Tests green. Module B done.
6. Final: stress 1000z end-to-end with server alive; 00-baseline.md updated; owner commits.
