# Plan: VFX identity batch 4 — apply burst quartet

Spec: [docs/architecture/vfx/spec-status-identity-batch4-apply.md](../docs/architecture/vfx/spec-status-identity-batch4-apply.md)

## Done (2026-08-30)

1. Added apply burst overrides for `leech`, `rally`, `pact_mark`, `command` in `VfxCatalog.StatusApplyBurst()`.
2. Extended apply-Conditional whitelist in `StatusVfxIdentityScoring` — **13/0/0** apply-moment.
3. Tests: batch-4 apply bursts, distinct keys invariant, apply bar audit test.
4. Fixed `rally` vs `blight` ApplyBurstKey collision (count 13, scale 1.05).

## Verify

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatusVfxIdentity|FullyQualifiedName~VfxAuraMath"
```

## Next batch

PulseRing cluster — see [spec-status-identity-batch5-pulsering.md](../docs/architecture/vfx/spec-status-identity-batch5-pulsering.md).
