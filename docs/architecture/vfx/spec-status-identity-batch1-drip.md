# Spec: status VFX identity — batch 1 (drip cluster)

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §17 · Audit: [status-identity-audit-2026-08-30.md](../../research/vfx/status-identity-audit-2026-08-30.md)

## Problem

`wither`, `blight`, and `rot` shared `VfxAuraStyle.Drip` with only RGB/tint differences. Static identity audit predicted **Fail** at sustain-glance; `blight` ↔ `rot` were structural color-only duplicates.

## Solution

Split the drip cluster into three unique motion grammars:

| Status | Aura style | Motion read | Contagion echo |
|---|---|---|---|
| `wither` | `WispOut` | Ash wisps drift up/out (life leaving) | Actor DoT — no spread hint |
| `blight` | `BubbleRise` | Green bubbles rise from feet + horizontal sway | Row spread |
| `rot` | `ChunkFall` | Heavy umber chunks fall in narrow column | Column spread |

Apply bursts also diverge (shape/count/size), not color alone.

## Catalog (SSOT)

[`VfxSeedCatalog.StatusSustainFx`](../../../src/FusionRpg.Core/Vfx/VfxCatalog.cs):

- `wither`: `WispOut`, tint 25%, aura `SizeScale` 0.9
- `blight`: `BubbleRise`, tint 20%
- `rot`: `ChunkFall`, tint 20%, aura `SizeScale` 1.25

Apply burst overrides via `StatusApplyBurst()`:

- `wither`: Radial, 10 particles, 0.35s
- `blight`: Rising, 12 particles, 0.5s
- `rot`: Radial, 8 particles, 0.45s, burst `SizeScale` 1.35

## Math

[`VfxAuraMath`](../../../src/FusionRpg.Core/Vfx/VfxAuraMath.cs) — pure samplers for enum values 6–8. Envelope tests in `VfxAuraMathTests`.

## Verification (source-only)

- `StatusVfxIdentityCollisionTests.Batch1_drip_trio_has_no_shared_motion_grammar`
- `StatusVfxIdentityScoring` predicts Pass sustain-glance for all three
- Motion-grammar pair count drops from 10 → 7 repo-wide
- `.\scripts\audit-status-vfx-identity.ps1` (static)

## Grammar note

SPEC §4 originally mapped all DoT to `Drip`. Batch 1 supersedes that for this trio only — readability requires distinct silhouettes. Generic `Drip` remains for future statuses if needed.

## Out of scope

LIVE eyeball, Crackle/Orbit/PulseRing batches, budget cap changes.
