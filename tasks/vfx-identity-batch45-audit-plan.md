# Plan: VFX identity batches 4–5 audit completeness

Parent batches: [batch 4 apply](vfx-identity-batch4-apply-plan.md) · [batch 5 pulsering](vfx-identity-batch5-pulsering-plan.md)

## Goal

Close audit harness, test, and scoring gaps for batches 4–5 (mirror batch 2/3 audit passes). No new VFX styles.

## Work items

1. **Harness** — `applyBurstKey` on signatures + `predictedApplyMoment` in JSON export
2. **Scoring** — derive apply verdict from `DefaultApplyBurstKey` (no 13-id whitelist)
3. **Tests** — batch-4 similar-apply guard, quartet distinct keys, engine-wrapped default burst, batch-5 aura scales + anchor-relative expand-outward
4. **Docs** — research audit §Tests, batch 4/5 spec verification lists

## Exit criteria

- `dotnet test --filter StatusVfxIdentity|VfxAuraMath` green (~63 tests)
- `.\scripts\audit-status-vfx-identity.ps1` → 0 motion-grammar pairs, apply bar in JSON
