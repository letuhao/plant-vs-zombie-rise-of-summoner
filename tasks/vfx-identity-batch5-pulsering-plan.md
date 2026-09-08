# Plan: VFX identity batch 5 — PulseRing cluster

Spec: [docs/architecture/vfx/spec-status-identity-batch5-pulsering.md](../docs/architecture/vfx/spec-status-identity-batch5-pulsering.md)

## Done (2026-08-30)

1. Added `PactFootPulse`, `CommandCrownPulse` to `VfxAuraStyle` + `VfxAuraMath` with envelope tests.
2. Updated sustain rows for `pact_mark`/`command`; aura SizeScale 0.9 / 1.05.
3. Motion-grammar pairs **0**; `pact_mark`↔`command` pair risk **low**.
4. All VFX identity + aura tests green (58 in filter scope).

## Verify

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StatusVfxIdentity|FullyQualifiedName~VfxAuraMath"
.\scripts\audit-status-vfx-identity.ps1
```

## Next batch

None planned for motion/apply static identity. Remaining: LIVE eyeball + forced-choice trials per audit doc.
