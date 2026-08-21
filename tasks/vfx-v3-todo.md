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
  - Pure `VfxAuraMath` (Drip/Orbit/RiseSparkle/CrackleJitter/PulseRing/StreamOut samplers, envelope-tested); `AuraPool` (24 systems, emission off, soft-disc, explicit colors, ≤6 particles/aura, ~0.3s pulses, per-tick position follow, host-gone reap); recipe kind `Aura`; seed wither (ash-brown Drip).
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
  **Offline caveat:** `FusionRpg.Core.Tests` is still uncompilable from the parallel Battle round's stale RED (`BattleReportEmitter` missing since 05:59) — the V6 catalog assertions (13-sustained / 8-vanilla-none / marker set) have not run offline; LIVE played every recipe as partial substitution. Run the suite once Battle lands.

### Final gate
- [x] Event-asserted LIVE gate PASSED (46/46, `_prove-vfx.json`). **Owner eyeball pending** — the 13-identity visual checklist printed by the prove run.
  - Seed all 13 per SPEC §4 (grammar: Drip=DoT, Crackle=armor/electric, Orbit=passive, Rise=buff, PulseRing=mark; markers only pact_mark/expose/bond/command); catalog test pins 13 sustained sets + zero for engine-wrapped 8; prove-vfx lifecycle cases (started / expired / host-gone / refresh-no-end / master-off); SSOT + SPEC + docs sync.
  - Accept: full prove PASS; 13-row eyeball checklist; owner verdict.
  - Scope: M.

### Final gate
- [ ] Full LIVE run + owner visual confirmation; verdict JSON appended; vfx.tick budget re-checked at next perf stress run.
