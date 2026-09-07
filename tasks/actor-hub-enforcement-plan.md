# ActorHub enforcement — plan

Program id: `actor-hub-enforcement`. Capability map: Hot compose gate + FULL contribution tracing.
Task list: [actor-hub-enforcement-todo.md](actor-hub-enforcement-todo.md).
Audit: [../docs/research/actor-hub-enforcement-audit-2026-09-07.md](../docs/research/actor-hub-enforcement-audit-2026-09-07.md).

## Goal

One Hot compose gate (`ActorHub`) for actor combat / derived / AppliedCombat. Every visible magnitude expands to named contributions (GG-49 FULL). Cold UniqueActor stays Data. BattleStatComposer stays locked-separate.

## Phases

0. Audit artifact (this session) — done in research + this pair.
1. ADR + DESIGN-GATE + SPEC skill + SourceId grammar appendix.
2. CI `guard-actor-hub.ps1` + GameHooks / SimEngine refactor onto Hub; Server fan-in = equip + tree + grants + aptitude + progression.
3. `GET /api/actors/{id}/sheet` — Cold + Hub with contributions + fiction labels + composeKind.
4. FE consumes sheet contributions as presentation only (follow-on; not blocking gate).

## Refuses

- Second composer for FE
- Persisting contribution history as specimen SSOT
- Shipping PARTIAL attribution
- Dropping GG-49 for Hot length
