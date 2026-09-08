# Spec: status VFX identity — batch 4 (apply burst quartet)

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §17 · Audit: [status-identity-audit-2026-08-30.md](../../research/vfx/status-identity-audit-2026-08-30.md)

## Problem

Four custom statuses still used the default apply template (`Radial|count=14|life=0.45|scale=1.00`) and scored **Fail** on apply-moment: `leech`, `rally`, `pact_mark`, `command`.

## Solution

Extend `StatusApplyBurst()` — no new `VfxAuraStyle` values:

| Status | Shape | Count | Life | SizeScale |
|---|---|---:|---:|---:|
| `leech` | Directional | 10 | 0.40s | 1.0 |
| `rally` | Rising | 13 | 0.50s | 1.05 |
| `pact_mark` | Radial | 12 | 0.30s | 1.1 |
| `command` | Radial | 10 | 0.35s | 0.95 |

Note: `rally` uses count 13 and `SizeScale` 1.05 to avoid colliding with batch-1 `blight` (`Rising|count=12|life=0.50`).

## Verification

- `Predicted_apply_moment_buckets_match_batch4_bar` — 13 Conditional / 0 Fail
- `All_custom_statuses_have_distinct_apply_burst_keys`
- `Batch4_apply_burst_keys_are_pairwise_distinct`
- `Similar_apply_color_excludes_batch4_shape_differentiated_pairs`
- `Similar_apply_color_collisions_are_empty`
- `Engine_wrapped_statuses_use_default_apply_burst`
- Batch 4 apply burst catalog test includes `pact_mark`/`command` aura `SizeScale` 0.9 / 1.05

## Out of scope

Sustain motion changes (batch 5), LIVE eyeball.
