# Status VFX identity audit — user perspective

**Date:** 2026-08-30  
**Scope:** 13 custom overlay statuses in the PVZ injector sustained VFX layer  
**Out of scope:** 8 engine-wrapped vanilla statuses (sustained overlay intentionally absent)  
**Machine JSON:** [`_status-identity-audit.json`](_status-identity-audit.json)  
**Harness:** [`scripts/audit-status-vfx-identity.ps1`](../../../scripts/audit-status-vfx-identity.ps1)  
**Core analysis:** [`StatusVfxIdentity.cs`](../../../src/FusionRpg.Core/Vfx/StatusVfxIdentity.cs), [`StatusVfxIdentityScoring.cs`](../../../src/FusionRpg.Core/Vfx/StatusVfxIdentityScoring.cs)

---

## Executive summary

**Verdict (post batches 4–5):** static identity remediation **complete** — **13/0/0 Pass** sustain-glance, **13/0/0 Conditional** apply-moment, **0** motion-grammar pairs.

| Sustain-glance (predicted) | Count | Statuses |
|---|---:|---|
| **Pass** | 13 | all custom statuses |
| **Conditional** | 0 | — |
| **Fail** | 0 | — |

| Apply-moment (predicted) | Count | Statuses |
|---|---:|---|
| **Conditional** | 13 | all custom statuses (batch-4 apply overrides) |
| **Fail** | 0 | — |

All thirteen custom statuses have distinct apply burst keys (batch 4). See [spec-status-identity-batch4-apply.md](../../architecture/vfx/spec-status-identity-batch4-apply.md), [spec-status-identity-batch5-pulsering.md](../../architecture/vfx/spec-status-identity-batch5-pulsering.md).

**LIVE automated harness:** **13/13** `sustainedStarted: true` (2026-08-30, MelonLoader 3.9, `audit-status-vfx-identity.ps1 -Live -Stress` with all-in-one `Ensure-LiveLabBoard`). Stress two-status cap block recorded in JSON.

**LIVE human eyeball (owner):** screenshots (13×3) and forced-choice trials (`humanCorrect` in JSON) still pending before vfx-v3 identity gate is fully green.

---

## Static signature matrix

Source: [`VfxSeedCatalog`](../../../src/FusionRpg.Core/Vfx/VfxCatalog.cs) via `StatusVfxIdentity.AllCustomSignatures()`.

| Status | Apply RGB | Aura | Tint | Marker | Structural key |
|---|---|---|---|---|---|
| `wither` | 140,110,90 | WispOut | 25% | — | WispOut \| tint=0.25 |
| `blight` | 130,160,60 | BubbleRise | 20% | — | BubbleRise \| tint=0.20 |
| `rot` | 120,90,50 | ChunkFall | 20% | — | ChunkFall \| tint=0.20 |
| `spark` | 255,240,120 | SparkStrobe | — | — | SparkStrobe |
| `expose` | 250,250,140 | CrackleJitter | — | TriangleDown | CrackleJitter + marker |
| `shatter` | 200,230,255 | ShardGlitter | 15% | — | ShardGlitter \| tint=0.15 |
| `spore` | 150,200,90 | SporeDrift | — | — | SporeDrift |
| `bond` | 255,170,200 | Orbit | — | Ring | Orbit + Ring |
| `charm_pulse` | 240,120,240 | CharmHeartbeat | 15% | — | CharmHeartbeat \| tint=0.15 |
| `pact_mark` | 170,90,220 | PactFootPulse | — | Diamond | PactFootPulse + Diamond |
| `command` | 120,140,255 | CommandCrownPulse | — | Ring | CommandCrownPulse + Ring |
| `leech` | 180,60,60 | StreamOut | 15% | — | **unique motion** |
| `rally` | 255,200,90 | RiseSparkle | 10% | — | **unique motion** |

**Findings:**

- Zero exact duplicate full signatures (SPEC §4 claim holds at the data level).
- **Zero structural color-only pairs** after batch 1 (pre-batch-1: `blight` ↔ `rot` shared Drip + 20% tint).
- **Zero same-motion-grammar pairs** after batch 5; all clusters split into unique styles.
- Apply RGB Δ=35 for `spark` ↔ `expose` remains at the static threshold, but apply burst templates differ (Radial 16 vs Rising 10) — excluded from `similar-apply-color` collisions via `ApplyBurstKey`.

---

## Cluster verdicts

### Drip cluster (`wither`, `blight`, `rot`) — **Pass (batch 1)**

**Fixed 2026-08-30:** `WispOut` (ash wisps up/out), `BubbleRise` (green bubbles from feet), `ChunkFall` (heavy chunks in narrow column). Zero shared motion-grammar pairs within the trio; apply bursts also differ (radial 10 / rising 12 / radial 8 heavy).

**User read (predicted):** three distinct decay motions — graying wisps, rising sickness bubbles, falling rot chunks.

### Crackle cluster (`spark`, `expose`, `shatter`) — **Pass (batch 2)**

**Fixed 2026-08-30:** `SparkStrobe` (tight electric strobe), `CrackleJitter` + TriangleDown for `expose` (unchanged grammar, sparser aura span), `ShardGlitter` (horizontal cyan shards). Zero shared motion-grammar pairs within the trio.

**User read (predicted):** electric strobe vs gold react-to glints vs icy horizontal shards.

### Orbit cluster (`spore`, `bond`, `charm_pulse`) — **Pass (batch 3)**

**Fixed 2026-08-30:** `SporeDrift` (lime spores drifting upward in wide orbit), `Orbit` + Ring for `bond` (unchanged), `CharmHeartbeat` (magenta orbit with phase-pulsing radius). Zero shared motion-grammar pairs within the trio.

**User read (predicted):** rising lime spores vs pink linked ring vs magenta heartbeat pulse.

### PulseRing cluster (`pact_mark`, `command`) — **Pass (batch 5)**

**Fixed 2026-08-30:** `PactFootPulse` (violet ring at feet), `CommandCrownPulse` (blue crown halo above head). Zero shared motion-grammar pairs.

**User read (predicted):** ground-level pact mark vs overhead command crown.

### Singles (`leech`, `rally`) — **Pass**

`StreamOut` (inward red drain) and `RiseSparkle` (upward gold motes) are the only statuses in their motion families.

---

## Per-status scorecard (predicted)

Scoring rubric from audit plan; values from `StatusVfxIdentityScoring.Score()`. **Human LIVE overrides these.**

| Status | Apply | Sustain idle | Sustain glance | Under stress | Notes |
|---|---|---|---|---|---|
| `wither` | Conditional | Pass | Pass | Pass | WispOut + radial apply burst |
| `blight` | Conditional | Pass | Pass | Pass | BubbleRise + rising apply burst |
| `rot` | Conditional | Pass | Pass | Pass | ChunkFall + heavy radial apply |
| `spark` | Conditional | Pass | Pass | Pass | SparkStrobe + radial 16 apply |
| `expose` | Conditional | Pass | Pass | Pass | CrackleJitter + TriangleDown + rising apply |
| `shatter` | Conditional | Pass | Pass | Pass | ShardGlitter + directional apply |
| `spore` | Conditional | Pass | Pass | Pass | SporeDrift + rising apply |
| `bond` | Conditional | Pass | Pass | Pass | Ring marker + radial apply |
| `charm_pulse` | Conditional | Pass | Pass | Pass | CharmHeartbeat + radial apply |
| `pact_mark` | Conditional | Pass | Pass | Pass | PactFootPulse + radial apply |
| `command` | Conditional | Pass | Pass | Pass | CommandCrownPulse + radial apply |
| `leech` | Conditional | Pass | Pass | Pass | StreamOut + directional apply |
| `rally` | Conditional | Pass | Pass | Pass | RiseSparkle + rising apply |

**Apply column:** all thirteen statuses Conditional (distinct burst shape/count per status).

---

## Forced-choice confusion matrix (predicted risk)

12 P0/P1 pairs prioritized for blind LIVE trials. Human `humanCorrect` column is null until owner runs trials.

| Pair | Predicted risk | Why |
|---|---|---|
| blight ↔ rot | **low** (was critical) | Batch 1: BubbleRise vs ChunkFall |
| wither ↔ blight | low | Batch 1: WispOut vs BubbleRise |
| wither ↔ rot | low | Batch 1: WispOut vs ChunkFall |
| spark ↔ shatter | **low** (was high) | Batch 2: SparkStrobe vs ShardGlitter |
| spore ↔ charm_pulse | **low** (was high) | Batch 3: SporeDrift vs CharmHeartbeat |
| spark ↔ expose | low | expose has marker; different apply burst |
| shatter ↔ expose | medium | expose has marker |
| spore ↔ bond | low | bond has Ring; SporeDrift vs Orbit |
| bond ↔ charm_pulse | low | bond has Ring; Orbit vs CharmHeartbeat |
| pact_mark ↔ command | **low** (was medium) | Batch 5: PactFootPulse vs CommandCrownPulse |
| leech ↔ wither | low | Different motion families |
| rally ↔ spark | low | RiseSparkle vs SparkStrobe |

**Protocol:** two adjacent zombies, randomize left/right, 5 trials per pair, viewer sees lawn only. Target ≥80% correct for Pass.

---

## Stress scenario results

| Scenario | Method | Result |
|---|---|---|
| Two-status cap | Unit: `StatusVfxIdentityAuditTests.Stress_eviction_keeps_marker_over_non_marker_on_same_host` | **Pass** — applying `spark` evicts `wither`, keeps `pact_mark` (marker priority) |
| Eviction order | Unit: existing `VfxStateTrackerTests.Per_host_cap_evicts_non_marker_oldest_first` | **Pass** |
| Refresh no flicker | Unit: `VfxStateTrackerTests.Start_then_reapply_refreshes_without_flicker` + prove-vfx (2026-08-21) | **Pass** (event-proven historically) |
| Global 24 cap | Unit: `VfxStateTrackerTests.Global_cap_holds_at_24` | **Pass** |
| Horde mixed board | LIVE | **Skipped** — no server |
| Vanilla coexistence | LIVE | **Skipped** — no server |
| Budget degradation | LIVE | **Skipped** — no server |

---

## Remediation backlog (ranked)

Changes stay in RPG VFX layer only ([`VfxCatalog.cs`](../../../src/FusionRpg.Core/Vfx/VfxCatalog.cs), [`VfxAuraMath.cs`](../../../src/FusionRpg.Core/Vfx/VfxAuraMath.cs), [`FxResources.cs`](../../../src/FusionRpg.Injector/Fx/FxResources.cs)).

### P0 — react-to marks (must read instantly)

Already Pass predicted; verify at distance in LIVE. If Diamond/Ring blur together, increase marker size contrast or add motion offset (pact at feet vs command above head per SPEC).

### P1 — Drip cluster differentiation

**Implemented 2026-08-30 (batch 1):** `WispOut` / `BubbleRise` / `ChunkFall` — see [spec-status-identity-batch1-drip.md](../../architecture/vfx/spec-status-identity-batch1-drip.md). Static audit: drip trio has zero motion-grammar pairs; predicted Pass sustain-glance for all three.

### P2 — Crackle cluster differentiation

**Implemented 2026-08-30 (batch 2):** `SparkStrobe` / `ShardGlitter` — see [spec-status-identity-batch2-crackle.md](../../architecture/vfx/spec-status-identity-batch2-crackle.md). Static audit: crackle trio has zero motion-grammar pairs; predicted Pass sustain-glance for `spark` and `shatter`.

### P3 — Orbit cluster without markers

**Implemented 2026-08-30 (batch 3):** `SporeDrift` / `CharmHeartbeat` — see [spec-status-identity-batch3-orbit.md](../../architecture/vfx/spec-status-identity-batch3-orbit.md). Static audit: orbit trio has zero motion-grammar pairs; predicted Pass sustain-glance for `spore` and `charm_pulse`.

### P4 — Apply burst (remaining 4)

**Implemented 2026-08-30 (batch 4):** distinct apply overrides for `leech`/`rally`/`pact_mark`/`command` — see [spec-status-identity-batch4-apply.md](../../architecture/vfx/spec-status-identity-batch4-apply.md). All thirteen custom statuses now Conditional at apply-moment.

### P5 — PulseRing cluster polish

**Implemented 2026-08-30 (batch 5):** `PactFootPulse` / `CommandCrownPulse` — see [spec-status-identity-batch5-pulsering.md](../../architecture/vfx/spec-status-identity-batch5-pulsering.md). Zero motion-grammar pairs repo-wide.

---

## Tests added

| Test class | Purpose |
|---|---|
| `StatusVfxIdentityCollisionTests` | Signature uniqueness, cluster counts, batch 1–5 apply burst differentiation, `Similar_apply_color_excludes_batch4_*`, quartet distinct keys, `Engine_wrapped_statuses_use_default_apply_burst`, global `similar-apply-color` empty invariant |
| `StatusVfxIdentityAuditTests` | P0 pair risk, 13/0/0 sustain-glance bar, 13/0/0 apply-moment bar, batch-5 pact/command sustain pass + low pair risk, pact_mark eviction stress |
| `VfxAuraMathTests` | Envelope tests including `SporeDrift_*`, `CharmHeartbeat_*`, `PactFootPulse_*`, `CommandCrownPulse_*` (anchor-relative expand-outward) |

Harness runs the full filter (`StatusVfxIdentity|VfxAuraMath`) — **63 tests** as of batch 4–5 audit pass. JSON export includes `applyBurstKey` per signature and `predictedApplyMoment` (13 Conditional / 0 Fail).

Run:

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatusVfxIdentity|FullyQualifiedName~VfxAuraMath"
.\scripts\audit-status-vfx-identity.ps1
# LIVE (game + server):
.\scripts\audit-status-vfx-identity.ps1 -Live -Stress -TargetPtr <ZombiePtr>
```

---

## vfx-v3 gate status

| Gate item | Status |
|---|---|
| Event prove (46/46) | Done 2026-08-21 |
| Static identity audit | **Done 2026-08-30** (this document) |
| Owner eyeball / screenshots | **Pending LIVE** — captures folder ready at [`status-audit-captures/`](status-audit-captures/) |
| Identity uniqueness bar | **Met** (source-only) — 13/0/0 sustain-glance, 13/0/0 apply-moment, 0 motion-grammar pairs after batch 5 |

**Owner action:** run LIVE harness, capture screenshots, complete forced-choice matrix in JSON. Source identity remediation complete; LIVE trials validate predictions.
