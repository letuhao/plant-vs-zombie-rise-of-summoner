# spec — `item-lore`

Part of [item-content-map.md](../item-content-map.md). Source findings:
[item-content-ideal.md](../item-content-ideal.md) §4. **Owner decision, 2026-09-06: sets get a lore
surface too** — extending the existing "uniques only" scope in `ssot-presentation.md` §4.1's card block
10, not replacing it.

## 1. Objective

An authored flavour sentence — already written, for 112 of 144 uniques and (once this module ships) for
sets too — reaches the player it was written for, instead of being dropped at import or faked at render.
This module authors no new prose; the writing already exists and an internal review already rates it
highly (`docs/architecture/item/review/wave2-flavour-quality.md`). The job is entirely: read the field
that exists, store it, and render it for real.

**Target users:** any player viewing a unique or set's item card.

## 2. Acceptance criteria

1. `UniqueCorpus.cs:210` reads and carries forward the authored `flavor` **text**, not only the
   `flavorKey`. A real unique with real authored flavour (e.g. `charnel-bloom-70`) renders its actual
   sentence on the card, not a key-fragment placeholder.
2. `adapt.ts:964`'s `keyTail(flavour.args.flavourKey)` placeholder is replaced by a real lookup into the
   string catalog (`content/display/en.json` or wherever the resolved flavour text is now served from).
   `adapt.ts:812-818`'s own comment ("a placement, never a translation") is removed once it is no longer
   true.
3. `content/display/en.json` (or the DB-backed equivalent this module chooses — see §7) carries a real
   entry for every unique's `flavorKey` that has authored text, so `MissingDisplayKey` (§6.1 row 10 of
   `ssot-presentation.md`) would fire honestly if one were ever missing, rather than silently resolving
   to nothing.
4. **Sets gain the same card block.** `SetCorpus.cs:15` reads the set's authored `flavor` field (already
   present in the seed contract for every set, per `seed-contract.md:277-278`, and already used by 6 of
   30 sets); the card's Flavour block (currently scoped "uniques only" in code) is widened to also render
   for a set that has one. A set with no authored flavour renders no block at all — this is not a case
   that needs a placeholder or a `pending` marker; an empty optional field is simply absent.
5. The 32 uniques with neither key nor text (`UniqueRow.cs:113-115`) are left exactly as they are — this
   module does not author missing prose, only wires the prose that exists. They render no Flavour block,
   correctly, the same as any other item with nothing authored.
6. A red-first test exists for each of 1-4, proven red against the current (pre-fix) code.

## 3. Commands / interfaces touched

- `src/FusionRpg.Core/Items/UniqueCorpus.cs` — the import-time reader that currently drops `flavor`
- `src/FusionRpg.Core/Items/SetCorpus.cs` — gains the same field
- `src/FusionRpg.Core/Items/Display/ItemCard.cs` — the Flavour block's rendering, widened from
  uniques-only to "uniques or sets, whichever the instance has"
- `web/fusion-rpg-web/src/contract/adapt.ts` — `keyTail` replaced with a real lookup for this specific
  field (do not remove `keyTail` globally if other callers still legitimately use it — check before
  deleting)
- `content/display/en.json` or its DB equivalent — gains real flavour entries

## 4. Project structure

No new files expected. This is corrections inside the existing corpus-loader and renderer files named
above.

## 5. Code style

Match `ItemCard.cs`'s existing block-rendering pattern (each of the eleven blocks is its own function or
clearly delimited section per `ssot-presentation.md` §4.1's own table) — the Flavour block gains a second
data source (set as well as unique), not a second code path. On the web side, the real lookup this module
adds should live beside `adapt.ts`'s other resolution helpers, matching its existing pure-function shape.

## 6. Testing strategy

- Red-first: seed a real unique with authored flavour, assert the card's rendered text contains the real
  sentence (not the key, not a fragment of the key). Repeat for a set.
- A negative test: an item/set with no authored flavour renders no Flavour block at all — this proves the
  "absent is fine" case doesn't regress into rendering an empty block or a placeholder string.
- `ItemCardTests.cs:602-616`'s existing `Flavour_is_uniques_only` test is renamed and widened (it is now
  testing the wrong invariant given the owner's decision) — do not leave a passing test asserting the
  behaviour this module deliberately changes.

## 7. Boundaries

- **Always:** treat the 898 already-authored sentences (uniques, base types, charms) as content to wire,
  not content to write. If this module's own investigation finds a sentence that reads as genuinely
  unfinished or placeholder-quality, name it rather than ship it — but the existing internal quality
  review already found the corpus strong, so this should be rare.
- **Ask first:** whether the string catalog stays a flat JSON file (`ssot-presentation.md` §5.3 N2, §10
  Q3 — explicitly left open by the presentation SSOT itself) or becomes a table, if the volume of real
  flavour entries makes the file unwieldy to review as a diff. The presentation SSOT already names this
  as reversible and owner-choosable; don't silently pick one without checking whether this module's
  volume changes the calculus.
- **Never:** invent flavour text for the 32 uniques or the 24 sets that have none. An absent block is
  honest; a generated placeholder sentence is not, and would contradict `ssot-presentation.md`'s own
  L3/L4 rule that authored display text is never synthesised at render time.
