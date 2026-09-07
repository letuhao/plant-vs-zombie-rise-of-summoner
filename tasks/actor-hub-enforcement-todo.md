# ActorHub enforcement — todo

## Gate (shipped)

- [x] Persist audit `docs/research/actor-hub-enforcement-audit-2026-09-07.md`
- [x] ADR row — sole Hot compose gate + FULL GG-49 SourceId grammar
- [x] DESIGN-GATE Stats row + SPEC skill checklist item
- [x] `ContributionSourceIds` grammar + producers
- [x] `scripts/guard-actor-hub.ps1` + Guard.Tests + CI / deploy-play
- [x] Server Hub fan-in (equip + tree + aptitude + progression) on `/derived` and `/sheet`
- [x] `GET /api/actors/{id}/sheet` + Contracts DTO
- [x] GameHooks / SimEngine → `ActorHub.Resolve`
- [x] BattleStatComposer exception documented (ADR + class header)

## Completeness (2026-09-08)

- [x] `EquippedBoundAtoms` shared; Program battle uses `FromEquippedResolver`
- [x] `/derived` ships `composeKind`
- [x] Empty SourceId skipped on `AtomDerivedSubsystem`
- [x] Guard pins GameHooks/SimEngine/`Stats.Resolve` + negative test
- [x] HTTP prove equip `equip:` + multi-source FlatSum + sheet 404 (this pass)
- [x] Unit: `FromResolver` → `equip:unknown:`; ResolveDerivedWithContributions asserts `aptitude.Might`

## Named follow-ons (not this gate)

- [ ] Injector `PassiveTreeTuningHub` hydrate + tree into `CheatState.ActorHub`
- [ ] Lawn equip SourceIds as `equip:` without double-counting grants
- [ ] FE InspectSplit consumes sheet contributions (presentation only)
