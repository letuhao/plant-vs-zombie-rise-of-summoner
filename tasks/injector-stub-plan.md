# Injector stub removal plan

**Program:** injector-stub  
**Audit:** [docs/architecture/injector/stub-audit-2026-08-30.md](../docs/architecture/injector/stub-audit-2026-08-30.md)  
**Scope:** REMOVE rows only (S1, S2)

## Goal

Stop unconditional match-start grants of butter-on-hit and passive ATK flat. Those effects remain
provable via equip (`stub.butter_bead`), debug scenarios, explicit plugin register in tests, and
`SecondaryPluginRegistry.CreateProve()`.

## Work packages

### W1 — Default registry

- `SecondaryPluginRegistry.CreateDefault()` yields **PatronSecondaryPlugin** only.
- Add `CreateProve()` for butter + passive ATK (test/offline explicit opt-in).
- Add `RegisterById(EffectPluginHost, IEnumerable<string>)` for scenario fixtures.

### W2 — Tests

- `EffectPluginHostTests`: default registry count 1, patron only.
- `EffectPluginLifecycleTests`: register prove plugins where match auto-grant is under test.
- `EffectFunnelTests.BeginMatch_stubs_grant_via_funnel`: register prove plugins before `BeginMatch`.
- `EffectOfflineKitTests` SimEffectHost cases: register butter plugin explicitly.
- Fixtures:
  - `effect-secondary-butter-match.json` — explicit `grant` step (mirrors `effect-butter-hit.json`).
  - `effect-secondary-match-cycle.json` — `"plugins": ["sec.match.butter"]` for withdraw cycle.

### W3 — LIVE verification

- `deploy-play.ps1 -NoServer`; plant hits zombie with no equip/debug → no butter.

## Out of scope (this plan)

- S6 `InjectorEffectActionSink` default status fallback (REVIEW — separate small change).
- S4 `UniqueEquipmentCatalog` stub items (items program).
- Deleting `MatchButterSecondaryPlugin` / `MatchPassiveAtkSecondaryPlugin` types (kept for prove path).

## Hard boundaries

Status still flows Funnel → Bag → `InjectorEffectActionSink`. No ad-hoc Unity `Zombie.Buttered()`
outside the sink.
