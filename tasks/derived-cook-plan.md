# derived-cook — implementation plan

**Program:** `derived-cook`  
**Map:** [docs/architecture/derived-cook-map.md](../docs/architecture/derived-cook-map.md)  
**Ideal:** [docs/architecture/derived-cook-ideal.md](../docs/architecture/derived-cook-ideal.md)  
**Todo:** [derived-cook-todo.md](derived-cook-todo.md)  
**Siblings:** [condition-glance-plan.md](condition-glance-plan.md) · [shield-sheet-plan.md](shield-sheet-plan.md)  
**Queue:** gui-lego P0 → **Harden** until Waves 1–3 land (D7 not required)

## Overview

Harden the shipped Derived Lego cook to design SSOT truth: registry/wire six states and caps,
cook-owned Status L2b + OTHER Shared (**D1**/D3), fold themeRegistry inject (**D5**), element paint
SSOT wire, fiction-only player band. Cheap FE CAP / mute `data-el` passes are out of acceptance.

Consumes shared `element-paint-ssot` from condition-glance Wave 1 (parallel OK; **no** private twin).

## Architecture decisions (locked)

| Id | Decision |
|---|---|
| D1–D7 | See capability map |
| D6 | Channel `Value` stays `double` with exempt comment |
| D7 | StatRow gauge **deferred** — not Done gate |

## Dependency graph

```text
derived-sheet-projection ─┬→ derived-cap-ssot ─┐
derived-cook-ia ──────────┴────────────────────┼→ derived-render-states → derived-fold-harden
element-paint-ssot (sibling) ──→ derived-element-paint-wire ─┤
derived-theme-packs · derived-player-copy ───────────────────┤
                                                              └→ derived-recipe-wire + volume-guard
```

## Delivery phases

### Phase 1 — Truth on the wire (Wave 1)

1. Widen `ActorSheetChannelDto` + ProjectSheet metadata (`unitClass`, `defaultValue`, `cap`, `renderState`) — **D2**/D6.
2. Cook IA: Status Omni+L2b + `expand: status-category` for dense families; BE emits OTHER Shared — **D1**/D3.
3. **DC-2b** — Delete FE invent: `OTHER_SHARED_VARIANT_ID` prepend + `STATUS_CATEGORY_VARIANTS` / `ACTION_CATEGORY_VARIANTS` expand arrays; cook/catalog only.
4. Cap path: FE deletes `KNOWN_CAPS`; paint from wire (immune=1, omni null).

### Phase 2 — Join + paint (Wave 2)

5. Six-state goldens; Show-unchanged = default only (**D4**); kill dead hint branch.
6. Fold harden: themeRegistry inject (**D5**), LadderIndex, no private paint maps, consume Shared from BE.
7. Element paint wire via shared **`resolveElementPaint`** (same as CG-A1) + action-category/cook-tab packs + player-copy (ban GG-49 / Join channelId; compose/unit from catalog/locale; unattributed when SourceId empty).

### Phase 3 — Land (Wave 3)

8. Recipe-wire: sheet-only fetch when ready; contract green; queue Harden → Done.
9. Volume guard fixtures (G6 / Guard 7).
10. **Optional later:** D7 statrow-gauge (not Done).

## Sibling cross-links

| Seam | This program | Sibling |
|---|---|---|
| `resolveElementPaint` | **DC-6** | condition-glance **CG-A1** (same module — no twin) |
| Hot live bag / SignalR | not owned here | CG-A4 / A4b / A5b; shield SS-A0 / A2 on `ActorSheetHotLiveStateTests` |

## Checkpoints

| After | Verify |
|---|---|
| Phase 1 | curl sheet channel has cap/renderState; cook JSON has Shared + L2b; no FE KNOWN_CAPS; no OTHER_SHARED / category-array invent |
| Phase 2 | six goldens; LadderIndex; no data-el paint SSOT; no GG-49; compose/unit catalog/locale; unattributed SourceId empty |
| Phase 3 | DerivedTab contract + volume stress; queue Done |

## Risks

| Risk | Mitigation |
|---|---|
| Catalog expand migration breaks Status rail | Golden join tests for resist.dot; keep status-id expand for open families |
| Paint SSOT not landed yet | Implement resolveElementPaint here or wait CG-A1 — same module path |
| UniqueDemon lean Pending | Sheet-preferred path; lean Pending honesty test |

## Explicitly out

Condition glance · Shield tab · OverlayAdd D1/D2 · channel Value→long · D7 required for Done

## Verification commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerived
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~DerivedStat
cd web\fusion-rpg-web
npm test -- --run foldDerivedSurfaceVm formatDerivedMagnitude DerivedTab DerivedCombatConsole.contract
rg -n "KNOWN_CAPS|GG-49|OTHER_SHARED_VARIANT_ID" web/fusion-rpg-web/src/features/gui-lego
```
