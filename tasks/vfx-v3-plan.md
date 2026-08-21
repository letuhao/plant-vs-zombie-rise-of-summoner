# Plan: VFX v3 — sustained status visuals

Spec: [../SPEC.md](../SPEC.md) · SSOT: [../docs/architecture/vfx-ssot.md](../docs/architecture/vfx-ssot.md) · Prior rounds: [vfx-v2-plan.md](vfx-v2-plan.md) (complete, LIVE-proven), perf-v3 in [plan.md](plan.md).

## Context

The 13 custom statuses (wither, blight, rot, spark, spore, pact_mark, leech, expose, shatter, bond, rally, command, charm_pulse) are invisible while active. This round gives each a unique duration-bound visual (aura particles / sprite tint / floating marker — vanilla-style) using the SSOT's reserved extension points: the `status.{id}.expire` cue and a sustained primitive family. The 8 engine-wrapped vanilla statuses stay untouched. Budget locked tight: 24 sustained sets global, 2 per host, marker-priority eviction.

## Locked lifecycle semantics (exploration-verified, StatusRuntime.cs)

New `OnEnded` hook fires at exactly 3 of the 8 instance-removal sites: **Tick expiry prune** (:264, reason expired), **ClearGrant** (:311, per matched), **ApplyFamilyMutex** (:234, displaced by competing elemental). It deliberately does NOT fire on: Refresh/Replace stacking (re-apply refreshes the tracker key — firing would flicker), WithdrawEntity (host dying — tracker's host-gone reap covers), Clear() (match teardown — ClearAll covers). Every sustained visual self-heals via TTL = DurationMs + 2s (infinite statuses: 60s re-confirm) — three independent backstops against stuck visuals. `OnEnded` handlers enqueue only (prune loop can be mid-spread; no runtime re-entry).

Known quirk (documented, unchanged): `debug.clear-status` / `ExecClearStatus` clear native CC only, not L2 instances — sustained visuals follow L2 state.

## Dependency graph

```
V1 end producer ──► V2 state tracker ──► V3 Aura + wither pilot ──► CP2 (first LIVE)
   (core hook)         (pure + director)  ├► V4 Tint + leech pilot ─┐
                                          └► V5 Marker + pact pilot ┴► CP3 ──► V6 13 identities + full gate
```

V3/V4/V5 independent after V2. Each slice = code + tests + build, shipping green alone.

## Risks

| Risk | Mitigation |
|---|---|
| Tint fights vanilla color writes (hurt-flash) | 0.25s re-assert, adopt-external-write-as-base; per-status fallback to aura-only (owner pre-approved) |
| Missed end signal → stuck visual | TTL cap + host-gone reap + match-end ClearAll |
| Horde cost | 24/2 caps, ≤6 particles per aura, pulsed manual emission (emission module off — LIVE lesson); vfx.tick budget re-read at next stress run |
| Refresh flicker | No OnEnded on Refresh/Replace — regression-pinned |
