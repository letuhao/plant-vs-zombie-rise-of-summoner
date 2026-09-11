# Spec: `prove-hub-combat`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** handoff prove path · tools/ProveAptitude prior art  
**Wave:** 4 (after Waves 1–3 Done gates)  
**Code anchors:** `tools/ProveAptitude` · Standing / Hub / Bound lawn paths · `scripts/prove-aptitude.ps1`

---

## Objective

Ship an **operator/script prove** that the program’s SSOT holds without a live-game debate:

1. **Post-fuse battle Hub** channel totals ≡ sheet Hub for same UniqueActor inputs (equip/aptitude/tree).
2. **Standing** rises when a membership combat channel (e.g. high dodge / cooldown) rises via Hub writers — not when only Θ rises.
3. **Bound lawn** aptitude input matches Server UniqueDemon compose (`lawn-aptitude-parity`).

Success: one script (extend ProveAptitude or sibling `prove-hub-combat`) exits 0 on green fixtures; documented in runbook; CI optional job or owner pre-merge check.

---

## Tech stack

- `tools/ProveAptitude` pattern (Core-only, load real tuning JSON)
- PowerShell wrapper under `scripts/`
- No live Unity required for (1)(2); (3) may be Core/Injector unit + optional live note

---

## Commands

```powershell
# Proposed — exact name locked in implementation
.\scripts\prove-hub-combat.ps1
# or extend:
.\scripts\prove-aptitude.ps1
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ProveHub|StandingParity|BoundLawn"
```

---

## Project structure

| Path | Duty |
|---|---|
| `tools/ProveHubCombat` or extend ProveAptitude | Emit pass/fail JSON; exit 1 on delta |
| `scripts/prove-hub-combat.ps1` | Thin wrapper |
| Fixtures | High-dodge Standing; Bound vs species isolation |
| Docs/runbook | How to run after fuse |

---

## Testing strategy

| Level | Cases |
|---|---|
| Tool | Battle Hub vs sheet Hub zero delta on fixture |
| Tool | Θ-only change → Standing unchanged; dodge grant → Standing up |
| Unit | Bound UniqueDemon ≠ empire species |

---

## Boundaries

- **Always:** Exit non-zero on fail; long magnitudes; Hub-only after fuse.
- **Ask first:** Adding live lawn REST polling (prefer Core-only).
- **Never:** Prove that reintroduces BattleStatComposer as SSOT.

---

## Success criteria

- [ ] Script exists and documented.
- [ ] Three prove bullets green on fixtures.
- [ ] Ideal handoff prove path checked.
