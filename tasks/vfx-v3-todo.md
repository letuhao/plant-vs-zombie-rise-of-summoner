# Tasks: VFX v3 — sustained status visuals

Plan: [vfx-v3-plan.md](vfx-v3-plan.md) · Spec: [../SPEC.md](../SPEC.md)

- [x] **V1: End producer** (spec M1)
  - `StatusRuntime.OnEnded` + 3 fire sites (Tick prune :264 / ClearGrant :311 / ApplyFamilyMutex :234); NOT Refresh/Replace/WithdrawEntity/Clear; `VfxCueDto.DurationMs` on apply cues; `StatusVfxCues.ExpireCue`; EffectRuntime wiring beside `OnApplied`.
  - Accept: tests pin fire/no-fire per site (refresh-no-flicker regression); DurationMs on apply cues; suites + Melon build green.
  - Files: `Core/Status/StatusRuntime.cs`, `Core/Vfx/StatusVfxCues.cs`, `Contracts/VfxDtos.cs`, `Injector/Effects/EffectRuntime.cs`, tests. Scope: S.

- [x] **V2: Sustained state tracker** (spec M2)
  - Pure `Core/Vfx/VfxStateTracker` keyed `(hostPtr, statusId)`: start/refresh/end(reason)/ttl-cap/evict; `VfxRules.SustainedGlobalCap=24`, `SustainedPerHostCap=2`, marker-priority eviction. Director: sustained specs → tracker start; `.expire` cues routed pre-admission; TTL sweep; idle early-out + ClearAll + master-off integration. Events `debug.fx.state.started/.ended` (reasons: expired|cleared|host-gone|ttl-cap|evicted|match-end|disabled); `debug.fx.state` command + `/api/debug/fx/state`.
  - Accept: tracker state-machine tests (orderings incl. evict-then-reapply, ttl-beats-missing-expire); suites + build green.
  - Scope: M.

### Checkpoint 1
- [x] Lifecycle core offline-green (1,064 core / 40 CheatCore / 40 Guard; Melon + Server builds clean). Note: paused mid-run for the parallel shield session's RED tests to turn green (their T1 landed; not our files).

- [x] **V3: Aura primitive + wither pilot** (spec M3) — offline complete (envelope tests green; LIVE eyeball folded into the final gate)
  - Pure `VfxAuraMath` (Drip/Orbit/RiseSparkle/CrackleJitter/PulseRing/StreamOut samplers, envelope-tested; identity batches added WispOut/BubbleRise/ChunkFall and SparkStrobe/ShardGlitter); `AuraPool` (24 systems, emission off, soft-disc, explicit colors, ≤6 particles/aura, ~0.3s pulses, per-tick position follow, host-gone reap); recipe kind `Aura`; seed wither (WispOut, was Drip at pilot).
  - Accept: math tests green; LIVE pilot — `debug.status.apply wither` shows aura until expire, `state.started/ended` asserted; owner eyeball.
  - Scope: M.

### Checkpoint 2
- [x] (merged into final gate — one deploy cycle for all pilots + roster, fewer game restarts)

- [x] **V4: Tint primitive + leech pilot** (spec M4) — offline complete (VfxTintMath test-pinned; compositor adopt-external-base rules in code)
  - `TintCompositor` (per-renderer stack, ≤35% multiplicative, capture/restore, 0.25s re-assert with adopt-external-base); recipe kind `Tint`; Flash coordinates with composited color; seed leech (15% red).
  - Accept: composite math tests; LIVE pilot; visible vanilla-fight ⇒ that status drops to aura-only (documented).
  - Scope: M.

- [x] **V5: Marker primitive + pact_mark pilot** (spec M5) — offline complete (procedural Ring/Diamond/TriangleDown/Cross textures; single-particle bob via AuraPool)
  - Procedural shape textures in `FxResources` (Ring/Diamond/TriangleDown/Cross); Marker = pooled bobbing particle above host (shares AuraPool); seed pact_mark (violet PulseRing + Diamond).
  - Accept: LIVE pilot readable at a glance; suites + build green.
  - Scope: S.

### Checkpoint 3
- [x] (merged into final gate)

- [x] **V4: Tint primitive** / **V5: Marker primitive** — offline-complete, LIVE-proven via the gate.
- [x] **V6: 13 identities + full gate** — all 13 seeded; SSOT §17 addendum written; **LIVE gate PASSED 46/46 (2026-08-21 ~09:00)**: v2 regression + sustained lifecycle (`started` → `ended(expired)`, refresh-no-flicker started=1/ended=0, host-gone reap). Two harness fixes during the gate: apply-until-started retries (LIVE apply-roll can resist ~50% — v2's single-shot pass was luck) and `Get-FxEvents` whitelist extended with `debug.fx.state.*` (the four "failures" were events the feed filtered out — they had all fired).
  ~~**Offline caveat:** `FusionRpg.Core.Tests` uncompilable from the parallel Battle round's stale RED~~ — **resolved.** Battle landed; the V6 catalog assertions (13-sustained / 8-vanilla-none / marker set) have since run green alongside the identity-batch suites (`staticTestPass: true` in the audit JSON).

### Final gate
- [x] Event-asserted LIVE gate PASSED (46/46, `_prove-vfx.json`).
- [x] **Static identity audit** (2026-08-30, re-run after batches 1–5): [`docs/research/vfx/status-identity-audit-2026-08-30.md`](../docs/research/vfx/status-identity-audit-2026-08-30.md) + [`_status-identity-audit.json`](../docs/research/vfx/_status-identity-audit.json) + `scripts/audit-status-vfx-identity.ps1` + `StatusVfxIdentity*` tests. **Final: 13 Pass / 0 Conditional / 0 Fail sustain-glance, 0 color-only pairs** (the first pass scored 6/2/5 — identity batches 1–5 closed every gap by giving each cluster its own motion, not just its own color). Apply-moment: 13 Conditional by design (whitelisted — apply bursts intentionally share a grammar, the sustained aura carries identity).
- [x] **LIVE identity run** (2026-08-30): `audit-status-vfx-identity.ps1 -Live -Stress` → **13/13 sustainedStarted**, static tests green, stress pass offline.
- [x] **Owner LIVE eyeball / forced-choice trials — WAIVED (2026-09-04).** Owner closed the round on the static + LIVE evidence; `humanCorrect` columns in `_status-identity-audit.json` stay null by decision, not by omission. If a future play session finds two statuses that read alike, reopen with a batch-7 row rather than re-running the whole audit.
- [x] **Round closed (2026-09-04)** — vfx v1+v2+v3 + identity batches 1–6 shipped. Remaining watch item (not a gate): `vfx.tick` budget re-check at the next perf stress run.
