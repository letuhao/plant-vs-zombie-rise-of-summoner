# Spec: status VFX identity — batch 5 (PulseRing cluster)

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §17 · Audit: [status-identity-audit-2026-08-30.md](../../research/vfx/status-identity-audit-2026-08-30.md)

## Problem

`pact_mark` and `command` shared `PulseRing` — the last same-motion-grammar pair (medium pairwise risk). SPEC §4 calls for feet vs crown vertical reads; injector places auras at body center, so Y bias lives in `VfxAuraMath`.

## Solution

| Status | Aura style | Motion read | Marker |
|---|---|---|---|
| `pact_mark` | `PactFootPulse` | Violet ring at feet | Diamond |
| `command` | `CommandCrownPulse` | Blue halo above head | Ring |

Generic `PulseRing` remains mark fallback for future statuses.

Catalog: `pact_mark` aura `SizeScale` 0.9, `command` 1.05.

## Math

- **PactFootPulse:** PulseRing fork with `PosY` biased down (`-span * 0.32`), tighter radius
- **CommandCrownPulse:** PulseRing fork with `PosY` biased up (`+span * 0.28`), wider halo

## Verification

- Motion-grammar pairs: **0**
- `PairRisk("pact_mark","command")` → **low**
- `Batch5_pact_command_predict_pass_on_sustain_glance` — sustain-glance **13/0/0 Pass**
- `PactFootPulse_sits_below_CommandCrownPulse` — vertical separation
- `PactFootPulse_expands_outward` / `CommandCrownPulse_expands_outward` — anchor-relative radial expansion
- Catalog aura `SizeScale`: `pact_mark` 0.9, `command` 1.05 (in batch-4 apply burst test)

## Out of scope

Injector Y-offset fields, LIVE eyeball, marker position per-status.
