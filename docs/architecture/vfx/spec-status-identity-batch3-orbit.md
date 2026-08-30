# Spec: status VFX identity — batch 3 (orbit cluster)

**Status:** Implemented (2026-08-30)  
**Parent:** [vfx-ssot.md](../vfx-ssot.md) §17 · Audit: [status-identity-audit-2026-08-30.md](../../research/vfx/status-identity-audit-2026-08-30.md)

## Problem

`spore`, `bond`, and `charm_pulse` shared `VfxAuraStyle.Orbit`. `bond` already passed sustain-glance via the pink **Ring** marker; `spore` ↔ `charm_pulse` were markerless orbits (lime vs magenta) — **predicted Fail** at board glance.

## Solution

Split the markerless orbit pair into two unique motion grammars; keep `bond` on generic `Orbit` + Ring marker:

| Status | Aura style | Motion read | React-to |
|---|---|---|---|
| `spore` | `SporeDrift` | Lime spores drifting upward in a wide orbit | — |
| `bond` | `Orbit` | Pink motes, slow orbit | Ring |
| `charm_pulse` | `CharmHeartbeat` | Magenta orbit with radius modulated by heartbeat phase | — |

SPEC §4 listed charm as "Orbit + PulseRing beat." The injector renders **one** aura primitive per recipe; batch 3 encodes the beat **inside** the `CharmHeartbeat` sampler (RPG layer only).

Apply bursts also diverge for the trio (shape/count/life).

## Catalog (SSOT)

[`VfxSeedCatalog.StatusSustainFx`](../../../src/FusionRpg.Core/Vfx/VfxCatalog.cs):

- `spore`: `SporeDrift`, aura `SizeScale` 1.15
- `bond`: `Orbit`, marker Ring (unchanged)
- `charm_pulse`: `CharmHeartbeat`, tint 15%

Apply burst overrides via `StatusApplyBurst()`:

- `spore`: Rising, 12 particles, 0.45s
- `charm_pulse`: Radial, 14 particles, 0.35s, burst `SizeScale` 0.9
- `bond`: Radial, 10 particles, 0.40s

## Math

[`VfxAuraMath`](../../../src/FusionRpg.Core/Vfx/VfxAuraMath.cs) — pure samplers for enum values 11–12:

- **SporeDrift:** wider orbit (`r ≈ 0.55×span`), positive `VelY` bias (upward drift)
- **CharmHeartbeat:** orbit path with radius modulated by `sin(phase × 5.5)` (heartbeat expand/contract)

Envelope tests in `VfxAuraMathTests`.

## Verification (source-only)

- `StatusVfxIdentityCollisionTests.Batch3_orbit_trio_has_no_shared_motion_grammar`
- `StatusVfxIdentityScoring` predicts Pass sustain-glance for `spore` and `charm_pulse`
- Motion-grammar pair count drops from 4 → 1 repo-wide (`pact_mark`/`command` only)
- Predicted sustain-glance: **13 Pass / 0 Conditional / 0 Fail**
- `.\scripts\audit-status-vfx-identity.ps1` (static)

## Grammar note

SPEC §4 originally mapped all passive/link statuses to `Orbit`. Batch 3 supersedes that for `spore`/`charm_pulse` only. Generic `Orbit` remains for `bond` and future passive/link statuses.

## Out of scope

LIVE eyeball, dual-aura injector support for charm_pulse, beam/link line between bonded units, PulseRing distance polish for `pact_mark`/`command`.
