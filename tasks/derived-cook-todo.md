# derived-cook — todo

**Plan:** [derived-cook-plan.md](derived-cook-plan.md)  
**Map:** [docs/architecture/derived-cook-map.md](../docs/architecture/derived-cook-map.md)

## Phase 1 — Wave 1 (truth on the wire)

- [x] **DC-1** Widen `ActorSheetChannelDto` + FE twin + ProjectSheet fill (`derived-sheet-projection`)
  - Accept: `unitClass`, `defaultValue`, `cap`, `renderState`; D6 double exempt comment; UniqueDemon lean → Pending
  - Verify: Server.Tests + curl sheet channel sample
- [x] **DC-2** Cook IA: Status variants Omni+L2b; dense families `expand: status-category`; BE OTHER Shared (**D1**/D3)
  - Accept: `status.resist.dot` joins; Shared in cook JSON; no FE invent required
  - Verify: catalog/cook tests; `curl .../catalogs/derived-surface`
- [x] **DC-2b** Delete production FE invent of cook truth: `OTHER_SHARED_VARIANT_ID` prepend; `STATUS_CATEGORY_VARIANTS` / `ACTION_CATEGORY_VARIANTS` expand arrays — consume cook/catalog only
  - Accept: cook owns Shared + category expand; FE does not invent those rows
  - Deps: DC-2 (or land with DC-2 / DC-5)
  - Verify: `rg -n "OTHER_SHARED_VARIANT_ID|STATUS_CATEGORY_VARIANTS|ACTION_CATEGORY_VARIANTS" web/fusion-rpg-web/src` clean of production invent (tests may keep names as negatives)
- [x] **DC-3** Cap SSOT: delete FE `KNOWN_CAPS` / literal CAP paths; wire-only (**derived-cap-ssot**)
  - Accept: omni null; immune at 1; grep clean
  - Deps: DC-1

### Checkpoint 1
- [x] Wire metadata + cook L2b/Shared + no FE CAP literals
- [x] DC-2b: no production FE invent of OTHER Shared / category expand arrays

## Phase 2 — Wave 2 (join + paint)

- [x] **DC-4** Six-state machine + goldens; Show-unchanged default-only (**D4**); remove dead `NO_PRODUCER_HINT` (**derived-render-states**)
  - Verify: `npm test -- --run derivedCook`
- [x] **DC-5** Fold harden: inject `themeRegistry` (**D5**); LadderIndex; consume wire state/cap; drop Shared invent / private paint (**derived-fold-harden**)
  - Deps: DC-2, DC-2b, DC-4
  - Accept checkbox: DC-2b invent paths gone from fold production
- [x] **DC-6** Element paint wire — chip/CSS use `resolveElementPaint` (same path as **CG-A1** / paint SSOT); delete `data-el` paint SSOT
  - Deps: CG-A1 or local same module — do not fork a private twin
- [x] **DC-7** Author action-category + cook-tab theme packs; delete fold statusId→L2b/glyph maps (**derived-theme-packs**)
- [x] **DC-8** Player copy: ban GG-49 / Join channelId; fiction state/compose; unattributed rows (**derived-player-copy**)
  - Accept: compose/unit sentences from catalog/locale keys (not permanent FE-only constants as SSOT); unattributed row present when `SourceId` empty
  - Verify: DerivedCombatConsole.contract

### Checkpoint 2
- [x] Six goldens green; LadderIndex; no GG-49; paint SSOT path only
- [x] Compose/unit from catalog/locale; unattributed when SourceId empty

## Phase 3 — Wave 3 (land)

- [x] **DC-9** Recipe-wire: sheet-only when projection ready; thin host; queue P0 Harden → Done
  - Verify: DerivedTab + contract + fold tests
- [x] **DC-10** Volume guard fixtures `volume:current` + `volume:stress500` (**G6**)

### Checkpoint 3 — program Done
- [x] Map success criteria (without D7)
- [x] Menu queue P0 Harden cleared

## Deferred (not Done gate)

- [ ] **DC-D7** `derived-statrow-gauge` sparks/pips when scheduled

## Follow-ups (non-blocking)

- [ ] Channel `Value` → `long` Core ticket (D6)
- [ ] OverlayAdd / aura idempotence (pipeline audit) — ask to pull
