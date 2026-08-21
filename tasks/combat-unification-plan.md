# Implementation Plan: combat unification + battle enrichment

Map: [../docs/architecture/combat-unification-map.md](../docs/architecture/combat-unification-map.md) (audited, owner decisions 1–7). Tasks: [combat-unification-todo.md](combat-unification-todo.md).
Named pair per repo convention — `tasks/plan.md`/`todo.md` hold perf-v3; shield pair separate.

## Overview

One combat SSOT everywhere: harden the overlay resolver (adapter, omni fallback, min-chip), extract the apply tail (shield gate) into a host-mountable pipeline, adopt both in BattleEngine (retiring its parallel math, landing shields in battle at RulesetVersion 2 with a rate-tested re-tune) and SimEngine (server-side shield probe, no game), then enrich battle in three versioned waves (riders v3, skills v4, hybrid v5).

## Standing gate

**U9 onward edits `Core/Battle` — blocked until the owner confirms the battle stream's session is finished** (owner decision 4). Phases 1–2 touch only `Core/Combat`/`Core/Effects` seams and can build immediately on approval. The decisions.md row (U1) is the program unlock, mirroring the shield program.

## Architecture decisions (from the audited specs — locked)

- Two-stage SSOT: pure resolver (hit/crit/matchup/floor) + stateful apply pipeline (shield gate → sink). Shields stay apply-stage by design.
- Overlay byte-identity is an acceptance criterion of every phase-1/2 task; battle/sim change once, at their version bumps.
- Re-tune acceptance is **computed, not sampled**: the sigmoid is deterministic, so rate tests assert `Sigmoid(parityDelta) ∈ [0.88, 0.92]` etc. directly — no statistical flakiness.
- Pipeline API: ptr-space keys, pipeline-owned prefixing, `IHpDeltaSink` (funnel adapter | direct), packet-free gate overload, `noteOverlayDamage` flag.
- Golden churn discipline: each re-bless reviewed against a predicted delta; zero-rider/zero-skill invariants lock cross-version byte-identity for unaffected setups.

## Dependency graph

```
U1 docs unlock
U2 rng adapter ─┐
U3 omni fallback ├─ Phase 1 (resolver-core)
U4 min-chip     │
U5 contracts ───┘
      │
U6 pipeline+sink+gate overload ─┐
U7 dispatcher delegates          ├─ Phase 2 (apply-pipeline)
U8 invariants/parity ───────────┘
      │                                   │
   [GATE: battle stream confirmed]        │
      │                                   │
U9 composer mapping                    U15 sim routing ─┐ Phase 4
U10 baseline re-tune                   U16 sim endpoints┘ (parallel with Phase 3)
U11 engine resolver swap
U12 pipeline routing + shields in battle
U13 innate/report/stamp
U14 golden re-baseline + win-rate sweep
      │
E1 riders (v3) → E2 skills (v4) → E3 hybrid (v5)   Phase 5 (each wave elaborated at its build start)
```

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Battle-stream collision in Core/Battle | High | Hard gate before U9; Phases 1–2 + Phase 4 (sim) proceed independently |
| Re-tune lands wrong feel | High | Deterministic computed rate tests (0.90±0.02 / 0.05–0.10) as U10 acceptance; win-rate sweep with owner sign-off at U14 |
| Golden re-bless rubber-stamping | Med | Predicted-delta review per re-bless; expedition churn pre-named as serialization-shape; zero-rider invariant across versions |
| Silent FA10-slot split in battle | Med | One-mutation-slot invariant test (U8) + all-deltas-through-pipeline rule (U12) |
| Guard token tripwires (`targetPtrs`, writer name in comments) | Med | Named in specs; guard run per task |
| Overlay regression via omni fallback / chip floor | Med | Fallback unreachable from dispatcher (golden), overlay profile chip = 0, full overlay suite byte-identity per task |
| Cross-arch replay divergence | Low | Platform stamp + sweep guard (U13) |

## Out of scope (spec-locked)

Vanilla PVZ behavior; overlay balance changes (curve, chip enablement — ask-first); variance reintroduction; guardian shield-share; skill resource costs (own spec later); `NoteOverlayDamage` for battle/sim.

## Open items

- The build gate above (owner confirmation on the battle stream).
- E1–E3 get their own detailed todos at wave start (progressive elaboration) — the todo lists wave-level acceptance only.
