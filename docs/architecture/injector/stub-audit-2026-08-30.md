# Injector / RPG-layer stub audit — 2026-08-30

**Status:** audit complete; removal implemented same session (S1/S2).

Cross-links:

- [effect-adoption-audit-2026-08-22.md](../effect-adoption-audit-2026-08-22.md) row **A4** (`UniqueEquipmentCatalog` stub items)
- [effect-funnel.md](../effect-funnel.md), [effect-system.md](../effect-system.md), [event-pipeline-v2-ssot.md](../event-pipeline-v2-ssot.md)

## Confirmed root cause — butter CC on every plant hit

Every lawn match auto-granted butter-on-hit at `board.start` via `MatchButterSecondaryPlugin` in
`SecondaryPluginRegistry.CreateDefault()`. The effect atom `fx.butter_on_hit` is real; the **grant
source** was the stub plugin, not equip/aura/content.

```
board.start → MatchHost → EffectRuntime.NotifyMatchStart → EffectPluginHost
  → MatchButterSecondaryPlugin.OnMatchStart → Funnel.EnqueueModifier (golden-butter)
combat.hit → EffectBag → ApplyStatus → InjectorEffectActionSink → Zombie.Buttered()
```

## Inventory

| Id | Site | What it does in play | Visible symptom | Class | Recommendation | Tests/fixtures | Owner program |
|---|---|---|---|---|---|---|---|
| S1 | `MatchButterSecondaryPlugin` + default registry | Grants `fx.butter_on_hit` (`golden-butter`, ICD 0) on every match start | All plant hits butter zombies | **REMOVE** | Drop from `CreateDefault()`; keep type for explicit test register | Lifecycle, funnel, offline kit, secondary fixtures | injector-stub |
| S2 | `MatchPassiveAtkSecondaryPlugin` + default registry | Grants `fx.passive_atk_flat` (+10 ATK match-wide) on every match start | Hidden +10 ATK to all entities each match | **REMOVE** | Same as S1 | Same blast radius as S1 | injector-stub |
| S3 | `PatronSecondaryPlugin` + default registry | Patron aura grants when patron loadout set | Patron buff when configured | **KEEP** | Production plugin — only unconditional match grant that stays in default registry | Patron-specific tests | aura-skill |
| S4 | `UniqueEquipmentCatalog` stub items (`stub.butter_bead`, etc.) | Equip API writes grant templates into `ModsJson`; applied when operator equips | Butter/ATK only when stub item equipped | **KEEP_UNTIL_item** | Cross-ref adoption audit A4; not auto-applied | `UniqueEquipmentCatalogTests`, Data E2E | items |
| S5 | `DebugRuntime` `ArmOnHitStatus` / `ArmOnHitExtra` / on-kill arms | Applies status/extra damage when explicitly armed | Debug/scenario-only butter | **KEEP_DEBUG** | Gated by arm flags; defaults `"butter"` when param omitted | `DebugScenarios` `effect-butter-hit` | cheat/debug |
| S6 | `InjectorEffectActionSink.ExecApplyStatus` `?? "butter"` | Fallback status id when param missing | Wrong status if malformed plan reaches lawn | **REVIEW** | Prefer empty/reject after S1/S2 removal — no production path should omit `status` | Sink unit coverage via scenario goldens | injector-stub |
| S7 | `DebugActions.ApplyStatusToZombie` `?? "butter"` | Same fallback on debug cheat path | Debug-only | **KEEP_DEBUG** | Acceptable on debug entry; document | Debug cheat tests | cheat/debug |
| S8 | `TravelBuffStub` (`CheatActions`) | No-op note for travel.buff API variance | None | **KEEP_DEBUG** | Unused gameplay stub; cheat command only | None | cheat/debug |
| S9 | `StubStatPlugins` (`IDeclaredInertContributor`) | Declared inert stat contributors for registry completeness | None on lawn | **KEEP_SEAM** | Documented seam — not auto-behaviour | Stat plugin tests | core/stats |
| S10 | `StubIntentSource` | Battle AI placeholder intent | None on lawn overlay path | **KEEP_SEAM** | Battle sim seam | Battle AI tests | battle |
| S11 | `ActorDerivedSnapshot.StubNeutral()` | Neutral derived snapshot fallback | None unless resolver misses | **KEEP_SEAM** | Resolver fallback | Derived tests | core/stats |
| S12 | `StubPowerIndexProvider` / default `IPowerIndexProvider` | Θ=0 when power index not hydrated | Flat magnitudes until hydration | **KEEP_SEAM** | Documented contract | `PowerIndexHydrationTests` | class-system |
| S13 | `AlwaysRelationOracle` (injector test harness) | Forces relation for scenario tests | Test-only | **KEEP_DEBUG** | Not registered in LIVE default | Injector scenario tests | cheat/debug |
| S14 | `EffectPluginHostFactory.CreateDefault` prove split | `CreateProve()` for butter/passive in tests only | N/A after removal | **KEEP_SEAM** | Explicit prove registry for tests | Updated lifecycle tests | injector-stub |

## Verdict (by class)

| Class | Count | Action |
|---|---|---|
| REMOVE | 2 | S1, S2 — removed from default registry this session |
| KEEP | 1 | S3 patron |
| KEEP_DEBUG | 4 | S5, S7, S8, S13 |
| KEEP_SEAM | 5 | S9–S12, S14 |
| KEEP_UNTIL_item | 1 | S4 |
| REVIEW | 1 | S6 — revisit after live path no longer depends on implicit butter |

## Removal order

1. **S1 + S2** — `SecondaryPluginRegistry.CreateDefault()` (no behaviour change to Funnel/sink)
2. **Tests/fixtures** — explicit `CreateProve()` register or scenario `grant` / `plugins` steps
3. **S6** (optional follow-up) — tighten `InjectorEffectActionSink` missing-status handling once goldens stable

## Verification

Audit phase (read-only scans + doc):

```powershell
rg "MatchButter|golden-butter|sec\.match\.butter" src tests
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~EffectPlugin"
```

Post-removal:

```powershell
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Guard.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\deploy-play.ps1 -NoServer   # owner: vanilla hits must not butter unless granted
```

## What this audit is not

Not a hunt to delete every `Stub*` symbol. Declared seams, battle AI placeholders, and catalog stubs
used only through explicit equip/debug paths stay until their owning program replaces them.
