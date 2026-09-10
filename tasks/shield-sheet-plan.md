# shield-sheet — implementation plan

**Program:** `shield-sheet`  
**Map:** [docs/architecture/shield-sheet-map.md](../docs/architecture/shield-sheet-map.md)  
**Ideal:** [docs/architecture/shield-sheet-ideal.md](../docs/architecture/shield-sheet-ideal.md)  
**Todo:** [shield-sheet-todo.md](shield-sheet-todo.md)  
**Siblings:** [condition-glance-plan.md](condition-glance-plan.md) · [derived-cook-plan.md](derived-cook-plan.md)  
**Queue:** gui-lego **P1b** (pulled from P4)

## Overview

Ship ActorSheet **Shield** tab as GUI Lego: ordered `sheet.shieldLayers` from `ShieldRuntime` in the
**same** Hot `ProjectSheet` flush as `shieldSummary` (**S1**), segmented drain-order bar (design §3.1),
omni shield StatRows, honest Pending vs Hot-empty. Kill three permanent “Empty layer” wells.

Depends on condition-glance `sheet-hot-projection` / live bag (**S3**) — do not fork ProjectSheet.

## Sibling cross-links

| Seam | Path |
|---|---|
| Shared Hot fixture | **`ActorSheetHotLiveStateTests`** — condition-glance CG-A4 owns; **SS-A2** asserts layers |
| Injector fill | **SS-A0** ↔ CG-A4b (same transport; do not fork) |
| SignalR invalidate | CG-A5b emit; FE host invalidate shared with Condition |
| Element paint | CG-A1 / `resolveElementPaint` — SS-B2 consumes; same as DC-6 |

## Architecture decisions (locked)

| Id | Decision |
|---|---|
| S1 | Layers on `sheet.shieldLayers` — reject player `/shields` as tab SSOT |
| S2 | Summary elementId / stacks (condition-glance fills summary; layers here) |
| S3 | Shared live bag — default per condition-glance plan |
| D8 | Cascade / apply-outcome inspect **P3 defer**; regen only if runtime exposes |

## Dependency graph

```text
condition-glance: ActorLiveState bag
        │
        ▼
ProjectSheet ── shieldSummary + liveStatuses (sibling)
             └── shieldLayers (this program) ← GetShields
                        │
                        ▼
        drafts → stack-bar / layer-inspect / omni-rows
                        │
                        ▼
        foldShieldSurfaceVm → shield-console recipe → thin ShieldTab
```

## Delivery phases

### Phase A — Hot layers (Wave 1)

1. Coordinate Injector bag fill for shields (**SS-A0** / CG-A4b) — same transport as statuses.
2. DTO `ActorShieldLayerDto` + `ActorSheetDto.ShieldLayers`.
3. ProjectSheet fills layers from live bag / GetShields; parity with Totals/summary.
4. Shared Hot fixture **`ActorSheetHotLiveStateTests`** (condition-glance owns class; SS-A2 asserts layers).

### Phase B — pieces (Wave 2)

5. HTML drafts: `shield-stack-bar`, `shield-layer-inspect`; omni-rows Fields.
6. Factories + paint SSOT (`resolveElementPaint` / CG-A1); drain-order segments; broken layer keeps empty slot; priority fiction labels; ban three permanent Empty wells.

### Phase C — surface (Wave 3)

7. `foldShieldSurfaceVm` with explicit **Pending vs Hot-empty vs N-segments** unit matrix.
8. Recipe `shield-console.json`: slots for `shield-stack-bar` + `shield-layer-inspect` + **omni region** (mount from SS-B3) + assembled surface.
9. Replace `CatalogTabs` ShieldTab Empty wells with RecipeMount thin host.

## Checkpoints

| After | Verify |
|---|---|
| Phase A | curl Hot sheet has layers+summary; `ActorSheetHotLiveStateTests`; cold empty |
| Phase B | Drafts exist; drain order; broken empty-slot + priority labels |
| Phase C | No Empty layer labels; Pending/Hot-empty/N-segments matrix; recipe omni region |

## Risks

| Risk | Mitigation |
|---|---|
| Live bag not ready | Ship DTO+compose reading empty bag; Hot fill lands with CG-A4 |
| Regen missing on instance | Omit regenText (D8) |
| FE number vs long | Parse carefully; wire long |

## Explicitly out

Condition layout · Derived cook · cascade trainer · apply-outcome toast (D8) · player GET /shields

## Verification commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Shield
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
curl -s http://127.0.0.1:5088/api/actors/<id>/sheet | ConvertFrom-Json |
  Select-Object shieldSummary, shieldLayers
cd web\fusion-rpg-web; npm test -- --run foldShield ShieldTab
```
