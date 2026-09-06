# spec — `atom-preview`

Part of [item-content-map.md](../item-content-map.md). Source findings:
[item-content-ideal.md](../item-content-ideal.md) §5.

## 1. Objective

A content author can see what an atom or a container actually renders as, before it is saved or shipped,
without hand-editing JSON and running a console validator to find out. This module does not build a
general-purpose editor — it exposes the existing, tested, DB-free `ItemCardRenderer` behind a preview
surface, the same pattern `render-tree-cards.mjs` already proves works for passive-tree content.

**Target users:** whoever authors or reviews `data/seed/items/**` content — today, a developer; the
surface should not assume that stays true forever, but it is not being built as a player-facing feature.

## 2. Acceptance criteria

1. A new route accepts an **unsaved** container definition (not an instance id — the existing
   `GET /api/items/{instanceId}/card` deliberately 404s on anything not persisted and owned,
   `RpgStore.ItemCard.cs:156-163`, "no write path, deliberately," and this module does not change that
   route) and returns the same `DisplayModel` shape `ItemCardRenderer.Render` already produces for a real
   instance.
2. The route requires no database write and no ownership — it is a pure function call over the posted
   definition, matching `ItemCardRenderer.Render`'s own existing DB-free signature
   (`ItemCard.cs:216` — in-memory rows and lookup delegates, exactly as `ItemCardTests.cs:249` already
   constructs them for tests).
3. A web page renders the preview — reusing the existing `ItemCard.tsx` component to display the
   response, not a second renderer. The page may be a standalone dev route or folded into the existing
   `AlmanacDumpPage.tsx` review-and-promote shell (`Program.cs:1040`) — pick whichever this module's own
   build finds cheaper once it reads both, and say which was chosen and why.
4. The preview covers, at minimum, one full container with rolled affixes at `Min`/`Max`, so an author
   can see a family's display template render at both ends of its authored range without needing a real
   drop.
5. This module does not touch `ItemCardRenderer`, `DisplayTemplates.Render`, or any of the 109 shipped
   display templates — it is a new caller of already-correct code, not a rewrite.

## 3. Commands / interfaces touched

- New: a preview endpoint, likely `POST /api/items/preview/card` or similar (match this repo's own
  `ItemSurfaceEndpoints.cs`/`ItemCardEndpoints.cs` naming convention — read both before naming the new
  route)
- Reused, unmodified: `src/FusionRpg.Core/Items/Display/ItemCard.cs`'s `ItemCardRenderer.Render`
- New or extended: a web page under `web/fusion-rpg-web/src/dev/` (matching `DeveloperTree.tsx`'s
  existing dev-surface convention) or an extension of `features/almanac-dump/AlmanacDumpPage.tsx`

## 4. Project structure

- `src/FusionRpg.Server/ItemPreviewEndpoints.cs` (new, one file, matching the one-file-per-concern
  convention every other item route file already follows)
- One new web page or one extension to an existing dev page — not both; pick per criterion 3.

## 5. Code style

The preview endpoint's request DTO should mirror `ContainerRow`/`ContainerAtomRow`'s real shape closely
enough that a raw JSON container (as authored in `data/seed/items/**`) can be posted with minimal
translation — the point of this tool is to preview what is about to be authored, not a different
representation of it.

## 6. Testing strategy

- A real test posting a real, valid container definition and asserting the response matches what
  `ItemCardRenderer.Render` produces when called directly in-process on the same input — this is the
  test that proves the route is a thin wrapper, not a second render path (guarding against
  `ssot-presentation.md` §8.6's named failure mode, "a second renderer appears and the two drift," even
  though this is a preview surface rather than a second production renderer).
- A refusal case: a malformed or referentially-invalid container (an atom id that doesn't exist, a
  missing required field) returns a real, named error, not a 500 and not a silently-empty preview.

## 7. Boundaries

- **Always:** call the real `ItemCardRenderer` — never reimplement rendering logic for the preview
  surface, even partially.
- **Always:** keep this endpoint read-only. It previews; it never writes the posted definition anywhere.
- **Ask first:** whether this surface should ever be reachable by a non-developer (e.g. a future
  in-game crafting/theorycrafting tool). This spec assumes a dev-only surface; broadening its audience
  is a real product decision, not an extension to make silently.
- **Never:** let this module block on or require the `atom-preview` module's own team to also build a
  write/save path — that is explicitly out of scope (the existing card route's "no write path,
  deliberately" stance is intentional and this module does not weaken it).
