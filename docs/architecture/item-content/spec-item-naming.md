# spec — `item-naming`

Part of [item-content-map.md](../item-content-map.md). Source findings:
[item-content-ideal.md](../item-content-ideal.md) §3.

## 1. Objective

A rolled item, a set, and a base type each show a real, human name everywhere a player can see one.
No surface shows a raw container id, instance GUID, channel id, or combo id as if it were a name, and no
surface asks a player to type one. This module wires already-built or already-authored naming machinery
to its real callers — it authors no new naming grammar and no new content.

**Target users:** every player-facing surface in `web/fusion-rpg-web` that shows an item, a set, a base
type, a socket combination, or a comparison delta.

## 2. Acceptance criteria

1. A rolled instance's card (`GET /api/items/{instanceId}/card`) returns a composed item name
   (`ItemNameComposer.Compose`'s real output) as `name`, not a base-type key. This must hold for an
   instance from **either** minter (`Instantiator.TryInstantiate` and `InstanceProducer.Compose`) — §3.3
   of the ideal doc records naming is deliberately render-time, not stored, so both paths must resolve
   identically through the same render call.
2. `ItemCardEndpointsTests.cs:340`'s existing assertion (`Assert.Equal("base.card-proof-blade", args["name"])`)
   is corrected to assert the real composed name, not the raw key — this test currently locks in the bug
   and must be rewritten, not left green by coincidence.
3. `ItemBaseTypeCorpus.Load` (`ItemCardEndpoints.cs:45-59`) carries the base type's authored `name`
   field through to the card, not only its `nameKey`. Same for `SetCorpus` (`SetCorpus.cs:15`) and its
   authored `name`.
4. In the shipped web UI, none of the following shows a raw id where a name belongs:
   - `RelicsLayer.tsx:375`'s equip-target `<option>` (currently a full instance GUID)
   - `CompareView.tsx:59`'s delta-table row labels (currently a raw channel id)
   - `Compendium.tsx:47`, `SocketBench.tsx:67`, `ItemCard.tsx:281`'s combination row titles (currently
     raw `comboId`)
   - `ArmouryList.tsx:109`'s item-name fallback (currently `container.xxx` via `adapt.ts:732`'s
     `?? containerId`)
5. `Workbench.tsx:54,76,143` no longer asks the player to type a raw recipe/container id — it offers a
   real picker (a dropdown or search-select over real names), the same shape `ArmouryList`/`Paperdoll`
   already use elsewhere in this same UI.
6. A red-first test exists for each of 1-5 above, proven red against the current (pre-fix) code before
   being made green.

## 3. Commands / interfaces touched

- `src/FusionRpg.Core/Items/ItemNameComposer.cs` — `Compose` (already correct; gains a real caller, not
  new logic)
- `src/FusionRpg.Core/Items/Display/ItemCard.cs`, `RpgStore.ItemCard.cs` — the assembly path that must
  call `Compose` instead of falling back to `baseType.NameKey`
- `src/FusionRpg.Server/ItemCardEndpoints.cs` — `ItemBaseTypeCorpus.Load`, the `name` field currently
  read and dropped
- `src/FusionRpg.Core/Items/SetCorpus.cs` — same class of fix for `item_set.name`
- `web/fusion-rpg-web/src/features/.../RelicsLayer.tsx`, `CompareView.tsx`, `Compendium.tsx`,
  `SocketBench.tsx`, `ItemCard.tsx`, `layers/relics/ArmouryList.tsx`, `Workbench.tsx`
- `web/fusion-rpg-web/src/contract/adapt.ts` — the `?? containerId` fallback and the raw channel/combo-id
  render paths

No new server route is required — the existing `GET /api/items/{instanceId}/card` and the existing
combination/compare routes already carry (or can trivially carry) the fields this module needs; if a
field genuinely does not exist on a DTO today, add it as an additive field, never a breaking rename.

## 4. Project structure

No new files are expected for the C# half — this is corrections to existing assembly/load code. The web
half may need one new shared component: a "named picker" for `Workbench.tsx`'s recipe/container
selection, reusing whatever list/search pattern `ArmouryList.tsx` already established (do not invent a
second one).

## 5. Code style

Match the file being edited. In particular: `RpgStore.ItemCard.cs` and `ItemCardEndpoints.cs` already
have an established pattern for corpus loading (`ItemBaseTypeCorpus`, `GemInsertCorpus`) — extend that
pattern, don't replace it. On the web side, match `adapt.ts`'s existing adapter-function shape (one pure
function per DTO→view-model mapping) rather than inlining resolution logic into a component.

## 6. Testing strategy

- Red-first, per acceptance criterion, using the real content pipeline (`FamilyExpansion` →
  `AffixLibraryGenerator` → a real minter), matching this session's own established pattern in
  `ItemCardTests.cs` — never a hand-built fake `DisplayModel`.
- A test proving both minters produce an identically-composed name for the same underlying container
  (criterion 1) — this is the test that would have caught naming being minter-specific if it ever became
  so.
- A web test (Vitest, matching the existing `RelicsLayer.test.tsx` pattern) asserting no rendered node's
  text content matches a raw-id shape (a container/instance/channel/combo id pattern) for each of the
  five UI surfaces named in criterion 4.

## 7. Boundaries

- **Always:** render a name via the composer at read time; never store a composed name on the instance
  row (§3.3 of the ideal doc — this is deliberate, so a reroll cannot leave a stale name behind).
- **Always:** treat `ItemNameComposer`/`AffixNameTable` as correct and complete — this module wires them,
  it does not redesign the naming grammar (that is I8's, `ssot-affixes.md` §4.12, already locked).
- **Ask first:** if a DTO needs a genuinely new field to carry a name where none exists today, confirm
  the field's shape against `ssot-presentation.md`'s own data-shape section (§5) before adding it, since
  that document is the presentation layer's own SSOT and a parallel field would drift from it.
- **Never:** invent a new naming convention, a new name-word corpus, or a fallback that shows a
  different kind of id (e.g. swapping a container id for a shorter hash) — a shortened id is still an id.
