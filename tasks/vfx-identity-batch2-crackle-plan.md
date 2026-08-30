# Plan: VFX identity batch 2 — crackle cluster

Spec: [docs/architecture/vfx/spec-status-identity-batch2-crackle.md](../docs/architecture/vfx/spec-status-identity-batch2-crackle.md)

## Done (2026-08-30)

1. Added `SparkStrobe`, `ShardGlitter` to `VfxAuraStyle` + `VfxAuraMath` with envelope tests.
2. Updated `VfxSeedCatalog` sustain rows (`spark`/`shatter`), apply burst overrides (`spark`/`shatter`/`expose`), `expose` aura `SizeScale` 0.85.
3. Identity tests/scoring updated; static audit shows 4 motion-grammar pairs (was 7); crackle trio has zero shared pairs.
4. All VFX identity + aura tests green (34 in filter scope).

## Verify

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~VfxAura|FullyQualifiedName~StatusVfx"
.\scripts\audit-status-vfx-identity.ps1
```

## Next batch

Orbit cluster — **done 2026-08-30 (batch 3).** See [spec-status-identity-batch3-orbit.md](../docs/architecture/vfx/spec-status-identity-batch3-orbit.md). Identity uniqueness bar met at sustain-glance (13/0/0 predicted). Remaining: LIVE eyeball + forced-choice trials.
