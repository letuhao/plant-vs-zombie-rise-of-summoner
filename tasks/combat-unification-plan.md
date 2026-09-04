# Implementation Plan: combat unification + battle enrichment

Map: [../docs/architecture/combat-unification-map.md](../docs/architecture/combat-unification-map.md) (audited, owner decisions 1–7). Tasks: [combat-unification-todo.md](combat-unification-todo.md).
Named pair per repo convention — `tasks/plan.md`/`todo.md` hold perf-v3; shield pair separate.

## Overview

One combat SSOT everywhere: harden the overlay resolver (adapter, omni fallback, min-chip), extract the apply tail (shield gate) into a host-mountable pipeline, adopt both in BattleEngine (retiring its parallel math, landing shields in battle with a rate-tested re-tune) and SimEngine (server-side shield probe, no game), then enrich battle.

⛔ **"Three versioned waves (riders v3, skills v4, hybrid v5)" is retired as of 2026-09-04**, and the
correction matters in two ways. **The waves are two, not three:** Wave E2 (skills) was not rebased but
**replaced** by `species-skills`, because its `SkillDef` and `SkillCatalog` each re-invented something
that had since shipped (`ActionRow`, `ActionEnvelope` on absolute ticks, `ActionKind`,
`ActionTargetSpec`, `ActionCatalog`) — building it as drafted would have created a fifth content system.
**And none of them is versioned:** every one shipped its mechanism **inert** — no trait carries a rider,
`hybrid.secondaryWeightMilli` is 0, skill channels sit at neutral — so `RulesetVersion` stayed at **4**
and no golden was re-blessed. The version ladder the overview promised was never needed.

## Standing gate — ✅ **LIFTED 2026-09-04**

~~**U9 onward edits `Core/Battle` — blocked until the owner confirms the battle stream's session is finished** (owner decision 4).~~

**The condition passed 2026-08-28** — the battle stream closed T5 (`kernel-adoption`) and T9
(`subsystems-on-timeline`). The gate's shape was also wrong: owner ruling 2026-09-03,
*"i don't want to join the gate — if the gate needs them, remove them."* Restated as dependencies:
Wave H depends on nothing here, Wave R depends on T9 (closed), `species-skills` depends on T5 + T19
(both closed). **Nothing is held.** U1–U16 are built; Phases 5–6 are what remains.

⛔ **`RulesetVersion` is 4, not 2, and the "versions 2–5 up front" ladder is retired.** Two unrelated
committed streams moved it (`decisions.md`, *`RulesetVersion` history (battle)*). Each remaining wave
bumps from wherever the number actually is, **and only if it moves a golden** — `species-skills` is
designed to move none.

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
E1 riders → E3 hybrid          Phase 5 (independent of each other; neither bumps if no golden moves)
      │
S1 neutral invariant → S2 cooldown read → S3 effectiveness read → S4 receipt   Phase 6 (species-skills)
                                                                        │
                                                          S5 species eligibility (⏸ waits on demon corpus)
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

## Phase 6 — species-skills (replaces Wave E2)

Spec: [../docs/architecture/combat/spec-species-skills.md](../docs/architecture/combat/spec-species-skills.md).

**Wave E2 was going to build a fifth content system.** Its `SkillDef` (id, cooldown in *rounds*,
action kind, targeting policy) and code-first `SkillCatalog` each re-invent something that has since
shipped — `ActionRow`, `ActionEnvelope` (absolute **ticks**), `ActionKind`, `ActionTargetSpec`, and
`ActionCatalog`, wired into battle by T19 on 2026-08-30. So the wave is **replaced, not rebased**:
this phase builds no catalog and no vocabulary. It wires **two reads** whose implementations already
exist with zero callers, which `DerivedStatRegistry.cs:179` names in as many words.

**The invariant that makes it safe:** neutral is `0‰` reduction and `1000‰` effectiveness, so a battle
where no actor carries a non-neutral `skill.*` value is **byte-identical**. Same shape as Wave R's
zero-rider invariant. `RulesetVersion` stays 4; no golden is re-blessed.

**Split by dependency, not by convenience:** the two reads depend on nothing and start immediately;
the species→action eligibility *content* waits on `demon-corpus-self-heal`'s four open items, because
the species ids are mid-regeneration and authoring against them means authoring twice.

## Open items

- ~~The build gate above~~ — lifted 2026-09-04.
- E1 and E3 get their own detailed todos at wave start (progressive elaboration) — the todo lists
  wave-level acceptance only. Phase 6 is elaborated in full because it is the one unblocking another
  program (`class-system`'s readiness gate).
- **S5 has no date, only a condition**: `demon-corpus-self-heal` closing C2/C3/D1. Nothing else in
  this program waits on it.
