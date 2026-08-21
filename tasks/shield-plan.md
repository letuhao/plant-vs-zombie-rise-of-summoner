# Implementation Plan: shield-system

Spec: [../docs/architecture/shield-system-spec.md](../docs/architecture/shield-system-spec.md) (Draft v3, two-lens audited). Tasks: [shield-todo.md](shield-todo.md).
Named pair per repo convention — `tasks/plan.md`/`todo.md` hold the perf-v3 stream.

## Overview

Add the shield resource above the Funnel: four `combat.shield.*` derived families (catalog 56 → 84), a `ShieldElementMatrix` + clamped HitCount-aware `ShieldMath`, a `ShieldRuntime` (stacking, merge, regen, expiry) gated into `CombatDamageDispatcher.DispatchInstant`, three grant surfaces (effect action, OnTimer aura, innate content rows), string-stream events with runtime aggregation, and the debug/web surfacing. No vanilla damage is ever absorbed; the no-shield path stays byte-identical.

## Architecture decisions (from the spec — locked)

- Gate sits in `DispatchInstant` after `Finalize`, before `EnqueueMutation`; Funnel stays hp-only FA10. Status DoT is absorbable (verified it already routes through the gate).
- All shield math is permille `long` with the locked clamp `[0.10×, 3×] × input` and `hitCount × breakerDelta`; `ShieldElementMatrix` returns unit relations, K applied once.
- Events go on the string envelope stream (not the v2 ring); `ShieldRuntime` aggregates `shield.absorbed` per `(owner, shieldId)` per flush window.
- Standalone absorption is **out of scope** — blocked on Battle-C2; this stream keeps the runtime engine-agnostic and adds the `BattleActorSetup` seam only.
- `trait.guardian` template wiring stays with the demon stream; this stream ships the aura mechanism (OnTimer + Area targeting).

## Dependency graph

```
T1 docs unlock (decisions.md row)          T3 matrix+policy
        │                                        │
T2 channels (+reader maps) ──────────┐     T4 ShieldMath goldens
        │                            │           │
        └────────► T5 runtime state (apply/merge/admission)
                          │                      │
                   T6 Absorb cascade ◄───────────┘
                          │        T7 Tick (regen/expiry)
                          │              │
                   T8 dispatcher gate    │
                          │        T9 injector tick host
        ┌───────────┬─────┴──────┬───────────────┐
   T10 grant     T13 events   T15 debug      T16 dumps+web
   action           │          probe/boards
        │        T14 vfx cue
   T11 aura
   T12 innate (also needs T9 barrier)
```

Build order follows the graph; T1–T4 are parallel-safe foundations, T10/T13/T15/T16 are parallel-safe after T8.

## Phases (checkpoints in shield-todo.md)

1. **Foundation (T1–T4):** docs unlock, channel expansion, matrix, pure math + goldens. Zero behavior change — nothing calls the new code yet. High-risk math lands first (fail fast on the clamp/rounding design).
2. **Runtime core (T5–T7):** instance state, cascade absorb, tick upkeep. Still unwired.
3. **Gate + host (T8–T9):** the vertical spine — damage actually drains shields end-to-end offline; byte-identical regression lock for the no-shield path; guards green.
4. **Grant surfaces (T10–T12):** one vertical slice per source — effect action, aura, innate.
5. **Observability + surfacing (T13–T16):** events + noisy-kind, VFX cue, debug probe re-route + boards, dump keys + web bar.
6. **Final gate:** full suites + guards + deploy; owner-run stress (no-shield pipeline share unchanged) and live probe absorb proof.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Hot-path regression in the drain (gate on every damage record) | High | No-shield fast path = one `TryGetValue` miss; `PerfProbe.ShieldAbsorb` from T8; stress re-run is the final gate |
| `hitCount` not currently plumbed into `DamagePacket` at the dispatcher | Med | T8 adds the field (additive Contracts change) fed from the coalesced record; default 1 preserves all existing call sites |
| Guard token landmine (`targetPtrs`, `EntityStatWriter` greps span all of Core, comments included) | Med | Naming discipline in every Core/Combat/Shield file; run `guard-funnel-delta.ps1` per task, not just at checkpoints |
| Math ambiguity frozen wrong by goldens | Med | Goldens generated from the spec's locked formula + invariants + the worked cascade example, written in T4/T6 before any consumer exists |
| Innate capacity read racing contributor load | Med | Queue-at-registration, apply-at-first-tick barrier (T12) exactly as specced; barrier test included |
| Web fold merging RPG shield into vanilla armor | Low | Distinct `rpgShieldHp`/`rpgShieldMax` keys; T16 test asserts armor mapping untouched |
| Battle-C2 drift (standalone spec expects shields later) | Low | T12 leaves the `BattleActorSetup` seam + spec §10 records the dependency; no standalone code beyond the field |

## Out of scope (spec-locked)

Vanilla damage absorption (never); standalone absorb E2E (Battle-C2); `trait.guardian` template wiring (demon stream); VFX cue art/tuning (VFX stream); radius shape / ally-relative targeting (ask-first `TargetResolver` extension); reflection/immunity types, percent pen, `bypassShield`, `OnShieldBroken` procs (ask-first).

## Open items

- ~~Spec approval~~ — approved 2026-08-21 with the final decisions folded in (constants confirmed: cap 3 / floor 0.10× / pen cap 3× / K 0.25; drain priorities aura 30 → skill 20 → innate 10; live proof via a debug grant endpoint in T15). No open questions remain.
- Final live verification (stress + probe) is owner-run per the server-lifetime rule.
