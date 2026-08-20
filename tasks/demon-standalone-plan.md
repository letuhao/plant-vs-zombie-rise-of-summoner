# Plan: Demon RPG + standalone-first — remaining waves (planned 2026-08-21, after P1–P4 landed)

Specs: [demon-system-map](../docs/architecture/demon-system-map.md) · [standalone-rpg-map](../docs/architecture/standalone-rpg-map.md) · module specs in `docs/architecture/{demons,standalone}/` · rationale: [audit-2026-08-21](../docs/architecture/standalone/audit-2026-08-21.md).
Tasks: [demon-standalone-todo.md](demon-standalone-todo.md). Execution: full-auto (owner); git hands-off; commit drafts at checkpoints.

## Landed so far (baseline for this plan)

P1 charter · P2 element extension (56 channels, light/dark) · P3 demon-core (generated 24-species catalog, atomic mint, codex) · P4 soul-economy (earn v2 in-transaction, spends) · P5 foundations (`SeededRng`, XP-dedupe bug fix). Suites: Core 872 / Data 83 / Guard 38 / E2E 103+1-foreign-failure.

## Dependency graph (what actually blocks what)

```
DONE: demon-core ─┬─ soul-economy ─┬─► WAVE A  summoning (V1 internal gate)
                  │                │      needs: catalogs + mint + spend + SeededRng — ALL DONE
DONE: SeededRng ──┴────────────────┘      needs NOTHING from the pipeline waves
DONE: element-ext ─► (typing consumed everywhere)

WAVE B  pipeline adaptations ──► WAVE C  BattleEngine + WebMatchService ──► WAVE D  expeditions
   (runs.game, pollution guards,      (pure engine → subsystems → goldens      (spec gate, then
    gating, retention — each lands     → service + log + boot sweep)            dispatch/collect + FE;
    with its own regression test)                                               also needs WAVE A demons)
```

**Planning decision — reorder P6 before P5-middle:** summoning's dependencies are entirely shipped; nothing in it touches ingest or the game column. Building it first delivers the declared V1 internal gate earliest, and gives the audit's riskiest module (match-source) an uninterrupted stretch afterward. Expeditions needs both (demons to send + battles to resolve), so it stays last. The combined-roadmap doc order is honored in spirit (dependency order), and this file is the execution SSOT.

## Vertical slicing rule applied

Every task below is one complete path (model → store → API → test, or hook → guard → regression), never a horizontal layer. No task touches more than ~5 files. Each wave ends in a checkpoint whose criteria come from the module spec's success list.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Summon transaction misses a post-crash state (audit F3's ghost) | A2's forced-failure test aborts mid-sequence and asserts zero rows in all five tables |
| Pipeline adaptations regress live PvZ ingest | Wave B lands one adaptation per task, each with a pvzrh-unchanged regression; Checkpoint B runs the full E2E suite |
| BattleEngine goldens lock wrong semantics | C1 locks clock/order *before* subsystems arrive (C2); goldens only in C3 after subsystem tests pass |
| Parallel streams (event-pipeline-v2, VFX) collide | This program touches none of `Core/Events/`, `Fx/`; ingest edits in Wave B are surgical and listed per-file up front |
| FE work stalls the gate | A4 is the only FE task in Wave A; checkpoint A allows API-proven V1 with FE following |

## Checkpoints

- **Checkpoint A (= V1 internal gate):** SIM demo loop offline: seed Souls → ×10 pull → roster/codex/balance/pity counters exact → replay adds nothing → nickname/lock persist. All suites + guards green.
- **Checkpoint B:** a synthetic `webrpg-1` batch through ingest leaves every pvzrh surface byte-identical (types, metrics, XP, grant session, retention); full E2E suite green.
- **Checkpoint C (= match-source success criteria):** 3 golden battles deterministic; SIM e2e web match produces run+facts+XP+Souls with zero injector; replay and concurrent-PvZ tests green.
- **Checkpoint D (= announced ship gate):** expedition dispatch→collect loop playable in FE against SIM; specimens soft-locked while deployed; rewards land through the one economy.
