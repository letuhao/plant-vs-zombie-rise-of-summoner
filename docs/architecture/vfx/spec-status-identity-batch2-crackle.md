# Spec: status VFX identity — batch 2 (crackle cluster)

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §17 · Audit: [status-identity-audit-2026-08-30.md](../../research/vfx/status-identity-audit-2026-08-30.md)

## Problem

`spark`, `expose`, and `shatter` shared `VfxAuraStyle.CrackleJitter`. `expose` already passed sustain-glance via the gold **TriangleDown** marker; `spark` ↔ `shatter` were structural color-only duplicates (yellow vs cyan jitter) — **predicted Fail** at board glance.

## Solution

Split the markerless crackle pair into two unique motion grammars; keep `expose` on generic `CrackleJitter` + marker:

| Status | Aura style | Motion read | React-to |
|---|---|---|---|
| `spark` | `SparkStrobe` | Yellow-white sparks teleport in a tight body box | — |
| `expose` | `CrackleJitter` | Gold glints (sparser aura span) | TriangleDown |
| `shatter` | `ShardGlitter` | Cyan shard glints with horizontal bias | — |

Apply bursts also diverge for the trio (shape/count/life).

## Catalog (SSOT)

[`VfxSeedCatalog.StatusSustainFx`](../../../src/FusionRpg.Core/Vfx/VfxCatalog.cs):

- `spark`: `SparkStrobe`
- `expose`: `CrackleJitter`, marker TriangleDown, aura `SizeScale` 0.85
- `shatter`: `ShardGlitter`, tint 15%

Apply burst overrides via `StatusApplyBurst()`:

- `spark`: Radial, 16 particles, 0.30s
- `shatter`: Directional, 10 particles, 0.40s, burst `SizeScale` 1.25
- `expose`: Rising, 10 particles, 0.40s

## Math

[`VfxAuraMath`](../../../src/FusionRpg.Core/Vfx/VfxAuraMath.cs) — pure samplers for enum values 9–10. Envelope tests in `VfxAuraMathTests`.

## Verification (source-only)

- `StatusVfxIdentityCollisionTests.Batch2_crackle_trio_has_no_shared_motion_grammar`
- `StatusVfxIdentityScoring` predicts Pass sustain-glance for `spark` and `shatter`
- Motion-grammar pair count drops from 7 → 4 repo-wide
- `.\scripts\audit-status-vfx-identity.ps1` (static)

## Grammar note

SPEC §4 originally mapped all armor/electric to `CrackleJitter`. Batch 2 supersedes that for `spark`/`shatter` only. Generic `CrackleJitter` remains for `expose` and future statuses.

## Out of scope

LIVE eyeball, Orbit/PulseRing batches, budget cap changes.
