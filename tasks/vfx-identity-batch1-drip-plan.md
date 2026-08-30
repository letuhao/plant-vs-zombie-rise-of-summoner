# Plan: VFX identity batch 1 — drip cluster

Spec: [docs/architecture/vfx/spec-status-identity-batch1-drip.md](../docs/architecture/vfx/spec-status-identity-batch1-drip.md)

## Done (2026-08-30)

1. Added `WispOut`, `BubbleRise`, `ChunkFall` to `VfxAuraStyle` + `VfxAuraMath` with envelope tests.
2. Updated `VfxSeedCatalog` sustain rows and per-status apply burst overrides.
3. Identity tests/scoring updated; static audit shows 7 motion-grammar pairs (was 10); drip trio has zero shared pairs.
4. All VFX identity + aura tests green (36 in filter scope).

## Verify

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~VfxAura|FullyQualifiedName~StatusVfx"
.\scripts\audit-status-vfx-identity.ps1
```

## Next batch

Orbit cluster (`spore`, `bond`, `charm_pulse`) — see audit remediation P3.
