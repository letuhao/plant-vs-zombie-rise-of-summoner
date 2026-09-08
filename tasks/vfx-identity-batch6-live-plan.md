# VFX identity batch 6 — LIVE gate plan

**Program:** vfx-v3 / status identity  
**Prerequisite:** Batches 1–5 static identity complete (unit tests + audit script static phase green)  
**Date:** 2026-08-30

## Goal

Human eyeball + event-backed harness prove that all 13 custom statuses start sustained VFX on a live lawn, stress cap/eviction behaves, and forced-choice pairwise trials validate distinguishability.

## Preflight (MelonLoader 3.9 default)

1. `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe` (assistant sessions)
2. `.\scripts\deploy-play.ps1 -LoaderHost MelonLoader -NoServer` + launch game
3. **Single command (all-in-one setup inside script):**

```powershell
.\scripts\audit-status-vfx-identity.ps1 -Live -Stress
```

Uses `Ensure-LiveLabBoard` — enter level 1 if needed, `lab-overlay`, assert living zombie. No manual Adventure click. Skill: [`.claude/skills/live-lawn-quick-start/SKILL.md`](../.claude/skills/live-lawn-quick-start/SKILL.md).

Mid-match only (legacy): `setup-lab-run.ps1` when operator is already in Adventure day.

## Harness fixes (this batch)

| Artifact | Change |
|---|---|
| [`scripts/lib/LiveLawnSetup.ps1`](../scripts/lib/LiveLawnSetup.ps1) | `Ensure-LiveLabBoard` — all-in-one enter + lab + ptr |
| [`scripts/lib/DebugStatusApply.ps1`](../scripts/lib/DebugStatusApply.ps1) | Status apply + delegates ptr to LiveLawnSetup |
| [`scripts/audit-status-vfx-identity.ps1`](../scripts/audit-status-vfx-identity.ps1) | `-Live` calls Ensure-LiveLabBoard; throws on failure |
| [`tools/live_test/live_test/status_apply.py`](../tools/live_test/live_test/status_apply.py) | `ensure_lab_board()` Python parity |
| [`.claude/skills/live-lawn-quick-start/`](../.claude/skills/live-lawn-quick-start/) | Cold-start skill (was missing) |

## LIVE acceptance

### Automated (must pass)

```powershell
.\scripts\audit-status-vfx-identity.ps1 -Live -Stress
```

- 13/13 `sustainedStarted: true` in `_status-identity-audit.json` live array
- Stress block records `fx/state` after pact_mark+wither and after third apply (spark eviction)

Optional Python smoke:

```powershell
cd tools\live_test
python -m live_test run status.l2.apply
python -m live_test run status.l2.organic
```

### Manual (owner)

1. **Screenshots** — per [`status-audit-captures/README.md`](../docs/research/vfx/status-audit-captures/README.md): 13 statuses × 3 frames (apply, sustain, marker if any)
2. **Forced-choice** — 12 P0 pairs × 5 trials blind; record `humanCorrect` in JSON forcedChoiceMatrix
3. Update [`status-identity-audit-2026-08-30.md`](../docs/research/vfx/status-identity-audit-2026-08-30.md) LIVE gate row

## Out of scope

- New motion grammar code (batches 1–5 shipped)
- New catalog effect defs (`fx.rot_on_hit`) — use `fx.overlay_damage` overlay + fire-synthetic

## References

- Audit doc: [`docs/research/vfx/status-identity-audit-2026-08-30.md`](../docs/research/vfx/status-identity-audit-2026-08-30.md)
- Prove VFX organic path: [`scripts/prove-vfx.ps1`](../scripts/prove-vfx.ps1)
- Full L2 matrix: [`scripts/prove-status-full.ps1`](../scripts/prove-status-full.ps1)
