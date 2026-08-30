# Tasks: VFX identity batches 4–5 audit pass

Plan: [vfx-identity-batch45-audit-plan.md](vfx-identity-batch45-audit-plan.md)

- [x] **Harness** — `applyBurstKey` + `predictedApplyMoment` in `audit-status-vfx-identity.ps1`
- [x] **Scoring** — `DefaultApplyBurstKey` constant; derive apply verdict from burst key
- [x] **Collision tests** — batch-4 similar-apply, quartet keys, engine-wrapped default, batch-5 aura SizeScale
- [x] **Aura math tests** — `PactFootPulse_expands_outward`, `CommandCrownPulse_expands_outward` (anchor-relative)
- [x] **Audit tests** — `Batch5_pact_command_predict_pass_on_sustain_glance`
- [x] **Doc sync** — research audit §Tests, batch 4/5 specs
