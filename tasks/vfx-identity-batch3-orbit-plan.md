# Plan: VFX identity batch 3 — orbit cluster

Spec: [docs/architecture/vfx/spec-status-identity-batch3-orbit.md](../docs/architecture/vfx/spec-status-identity-batch3-orbit.md)

## Done (2026-08-30)

1. Added `SporeDrift`, `CharmHeartbeat` to `VfxAuraStyle` + `VfxAuraMath` with envelope tests.
2. Updated `VfxSeedCatalog` sustain rows (`spore`/`charm_pulse`), apply burst overrides (`spore`/`charm_pulse`/`bond`), `spore` aura `SizeScale` 1.15.
3. Identity tests/scoring updated; static audit shows 1 motion-grammar pair (was 4); orbit trio has zero shared pairs.
4. Predicted sustain-glance: **13 Pass / 0 Conditional / 0 Fail** — identity uniqueness bar met (source-only).
5. All VFX identity + aura tests green (50 in filter scope).

## Verify

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatusVfxIdentity|FullyQualifiedName~VfxAuraMath"
.\scripts\audit-status-vfx-identity.ps1
```

## Next batch

Apply burst quartet — **done 2026-08-30 (batch 4).** PulseRing cluster — **done 2026-08-30 (batch 5).** See [spec-status-identity-batch4-apply.md](../docs/architecture/vfx/spec-status-identity-batch4-apply.md) and [spec-status-identity-batch5-pulsering.md](../docs/architecture/vfx/spec-status-identity-batch5-pulsering.md). Static identity complete; remaining: LIVE eyeball.
