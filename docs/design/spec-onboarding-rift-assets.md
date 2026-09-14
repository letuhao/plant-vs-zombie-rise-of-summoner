# Spec: onboarding Rift assets v1

**Status:** approved baseline for implementation  
**Version:** v1 — replaceable art  
**Owner surface:** Gnome-quarantine onboarding teaser  
**Related:** [onboarding teaser](../ideas/onboarding-gnome-teaser.md) · [PvZ canon boundary](../research/pvz-lore/canon-boundary-and-onboarding.md)

## Problem statement

The onboarding teaser needs one visual identity for the Rift: recognizable at a glance, readable in a small UI, compatible with the bright, playful PvZ-style presentation, and capable of supporting serious runtime VFX. The art must be replaceable later without changing story flow, layout, or code contracts.

## Selected v1 assets

The approved baseline is the second, brighter cartoon pass. The earlier dark-fantasy files remain available as alternatives and are not the v1 defaults.

| Role | File | Current dimensions | Use |
|---|---|---:|---|
| Story sprite | [`rift-portal-pvz-style.png`](../assets/onboarding/rift-portal-pvz-style.png) | 1254 × 1254 | Beat 2/3 visual scene, large portal or fracture reveal |
| Icon source | [`rift-icon-pvz-style.png`](../assets/onboarding/rift-icon-pvz-style.png) | 1145 × 1374 | High-resolution source for UI export and future variants |
| UI icon | [`rift-icon-pvz-style-64.png`](../assets/onboarding/rift-icon-pvz-style-64.png) | 64 × 64 | Small Rift marker, button, status chip, or map legend |

All three files are PNGs with transparent backgrounds. The v1 art language is thick dark outlines, rounded cartoon shapes, saturated purple, and slime-lime energy. No characters, logos, words, or existing PvZ assets are embedded.

## Goals

1. Establish one Rift visual language for the four onboarding beats.
2. Keep the 64px icon legible without relying on text or fine particles.
3. Let a later art replacement swap files while preserving consumers and story copy.
4. Keep the assets presentation-only; they do not imply a new gameplay rule.
5. Provide a static base layer that can be driven by shared Rift VFX recipes without baking animation into the PNGs.

## Non-goals

- No baked animation atlas or one-off particle implementation in the asset package. Runtime Rift VFX are a separate implementation slice governed by the VFX SSOT.
- No Gnome, Void, Dave, plant, or zombie character art in these files.
- No assertion that the Rift or Void is official PvZ canon; the narrative boundary remains in the research document.
- No new server checkpoint, reward, currency, or progression gate.
- No requirement to use the dark-fantasy first-pass assets after v1 is adopted.

## Requirements

### P0 — must have

| Requirement | Acceptance criteria |
|---|---|
| Shared visual identity | Given either v1 asset is shown, the viewer can identify it as the same Rift family through purple/lime palette, wobbly tear silhouette, and dark outline. |
| Transparent compositing | Given the asset is placed over a lawn, sky, or dialog background, no black/white rectangle or baked-in frame is visible. |
| Small-icon readability | Given `rift-icon-pvz-style-64.png` is rendered at 32–64px, the central tear and bright rim remain distinguishable without text. |
| Replaceable contract | Given a future replacement preserves the role, transparent PNG requirement, and target bounds, the teaser does not need story-copy or checkpoint changes. |
| Presentation-only behavior | The asset cannot award rewards, mutate progression, decide victory, or act as a combat entity. |

### P1 — should have

- Add a 128px icon export for medium-density UI.
- Add a two-frame pulse or shimmer variant after the static teaser is accepted.
- Add a muted/disabled tint in CSS or the presentation layer rather than baking another image.

### P2 — future considerations

- Replace v1 with a final art pass after free-asset research and visual review.
- Add a map-scale Rift marker variant with fewer particles and a wider silhouette.
- Add a Gnome quarantine seal as a separate composited asset; do not bake it into the Rift sprite.

## Consumer contract

The eventual UI consumer should refer to semantic roles, not filenames:

```text
onboarding.rift.storySprite
onboarding.rift.icon
```

The current v1 file mapping is implementation detail. A future art replacement changes the mapping, not the story beats, button labels, onboarding checkpoint sequence, or route.

The sprite belongs to the teaser’s visual beats. The 64px icon may appear beside `Anchor the lawn`, in the first-user guide, or in a later Rift/map legend. Do not place the icon in combat HUD chrome until a separate combat-use decision exists.

The v1 static files are the base art layer, not the complete Rift effect. Beats 2 and 3 must compose them
with the semantic cues `rift.portal.open`, `rift.portal.surge`, `rift.quarantine.seal`, and
`rift.quarantine.fade` through the shared `VfxCatalog`/`VfxDirector` pipeline. A new primitive kind,
shader dependency, cap, or rate-limit change requires a corresponding VFX SSOT decision and live proof.

## PvZ image-dump and SQL provenance

The game has no player-facing save feature. Do not put captured art or story state in a save-slot model.
PvZ image dumps already live in the media database (`rpg-media.sqlite`):

- `type_icon_layers` stores raw layer PNG BLOBs, source, dimensions, and capture time;
- `type_icons` stores composed portraits and their recipe;
- `TypeIconStore` and `/api/icons/dump/*` remain the read/write authority.

If a Rift asset reuses a captured PvZ image, the semantic manifest must record its source key
(`side`, `type_id`, `layer`) and the implementation must validate that the referenced row exists. If a
Rift asset is generated or licensed, record that provenance in the manifest as `generated` or `licensed`
without duplicating PvZ BLOBs into `rpg-hot.sqlite`. The implementation task may add this small,
metadata-only registry to `rpg-media.sqlite` when a durable semantic mapping is needed:

```sql
CREATE TABLE IF NOT EXISTS rift_asset_sources (
  asset_id TEXT NOT NULL,
  role TEXT NOT NULL,
  source_kind TEXT NOT NULL CHECK (source_kind IN ('pvz_dump', 'generated', 'licensed')),
  side TEXT,
  type_id INTEGER,
  layer TEXT,
  source_uri TEXT,
  sha256 TEXT,
  captured_utc TEXT NOT NULL,
  revision INTEGER NOT NULL DEFAULT 1,
  PRIMARY KEY (asset_id, role, revision)
);
CREATE INDEX IF NOT EXISTS ix_rift_asset_sources_pvz
  ON rift_asset_sources(source_kind, side, type_id, layer);
```

For `source_kind = 'pvz_dump'`, `(side, type_id, layer)` is required and must resolve in
`type_icon_layers`; for `generated`/`licensed`, `source_uri` and `sha256` identify the reviewed asset.
This registry tracks provenance only: it never stores image BLOBs, player state, story acknowledgement,
or progression. A missing/stale key fails closed to `rift-placeholder`.

## Runtime asset contract

The semantic map must resolve a role, an accessible label, a fallback, and a version—not just a file
path. A consumer may treat the following as the minimum shape:

```text
onboarding.rift.storySprite = {
  src: "...",
  alt: "A bright purple tear edged with lime energy",
  role: "story-sprite",
  version: 1,
  fallback: "rift-placeholder"
}
onboarding.rift.icon = {
  src: "...",
  alt: "Rift marker",
  role: "status-icon",
  version: 1,
  fallback: "rift-placeholder"
}
```

Missing, late, or rejected images render the designed `rift-placeholder` (a transparent, rounded tear
with the same purple/lime tokens and the text alternative “Rift”), never a browser broken-image glyph,
empty rectangle, or blocking error. The placeholder is presentation-only and is valid in offline/standalone
mode.

### Display bounds

- The story sprite is rendered with `object-fit: contain`, never cropped or stretched. Its media frame is
  square, at most 420×420 CSS px on desktop and 320×320 CSS px on a narrow viewport, with 16px minimum
  insets. The 1254×1254 source is downscaled; it is not enlarged above its intrinsic size.
- The portrait icon source is archival/high-resolution input, not a direct UI shape. Any runtime icon is
  composed into a transparent square canvas before scaling. The 64px export is the canonical small icon;
  a future 128px export must preserve the same square silhouette and safe area.
- At 32px, the tear opening and bright rim must occupy the central 70% of the canvas. Fine particles,
  lettering, and edge detail are optional and must not carry meaning.
- The dialog keeps the image and copy readable at 320px viewport width. Copy may wrap or scroll inside
  the bounded dialog, but the controls remain visible without scrolling on the final beat.

### Visual states

| State | Asset treatment | Interaction |
|---|---|---|
| normal | Full-color v1 asset | Advance/anchor/skip available |
| reduced motion | Same static asset; no pulse, shimmer, zoom, or distortion | Same controls |
| VFX active | Static base plus shared Rift cue recipe | Story controls remain available; VFX may drop safely |
| VFX unavailable | Static base plus seal/placeholder | Story still advances; no gameplay impact |
| loading | Stable placeholder box; no layout shift | Controls remain available when story copy is loaded |
| missing/error | `rift-placeholder` with accessible label | Story may continue; retry is optional |
| disabled/secondary | CSS-muted version of the same asset | Never bake a second tinted PNG for v1 |

## Asset validation and replacement checks

The implementation slice must add a repeatable validation job before wiring the component. It should fail
on missing files, wrong dimensions, non-PNG content, absent alpha, or an opaque full-canvas background;
it should also verify the semantic map resolves both roles and that every consumer has a fallback. A visual
fixture must composite each asset over a lawn-colored background and over a dark dialog background so a
halo, matte, or baked rectangle is caught.

The VFX slice adds a separate proof gate: each Rift cue resolves through the shared catalog/director,
respects caps and reduced motion, emits a skip reason rather than throwing when resources are absent, and
is covered by the repository's live VFX proof tooling.

The replacement checklist is:

1. preserve the semantic role and versioned manifest entry;
2. preserve transparent compositing and the display bounds above;
3. render the 32px/64px icon fixture and the story-sprite fixture without cropping;
4. run the missing-asset fallback fixture; and
5. confirm that story copy, destination, and the server-owned checkpoint ledger are unchanged.

No art replacement is complete if it requires a new route, a new checkpoint, a combat rule, or a
different onboarding acknowledgement contract.

## Success measures

- 100% of v1 consumers load a transparent asset with no fallback rectangle.
- A reviewer can identify the icon at 32px in a quick visual scan.
- Replacing the three mapped files requires no change to story copy or server-owned first-session progression.
- No asset reference introduces a new onboarding reward or progression authority.
- A missing or slow asset never blocks `Skip intro`, `Anchor the lawn`, or the standalone presentation path.
- The 32px icon remains identifiable in both light/lawn and dark/dialog contrast fixtures.

## Open questions

- **Design:** should the final art keep the lime rim, or move toward a more plant-green accent after the free-asset review?
- **Design/engineering:** which portal/quarantine motion grammar best survives the bright PvZ-style base art? Default: layered tear distortion, rim surge, particle streaks, and geometric seal pulse.
- **Engineering:** where should the semantic asset map live when the teaser component is implemented (`public` asset path versus an imported module asset)? The chosen location must still expose the manifest/fallback contract above. Default: imported module plus media-dump source keys.
- **Product:** should the 64px icon appear only in the teaser, or also become the long-term Rift map marker?
- **VFX:** which shader/resource capabilities are available on supported loader hosts? Default: use existing `FxResources`/shader probe and degrade to static layers when unavailable.

## Phasing

**v1 now:** register the static files, SQL/media provenance, and semantic roles.  
**v1 VFX slice:** implement the four-beat teaser plus the serious portal/quarantine cues through the shared VFX pipeline and existing `Anchor the lawn` destination.  
**v2 art:** replace the mapped files after free-asset research or commission a final art pass; preserve the consumer and VFX cue contracts.

## Audit decisions (2026-09-14)

Resolved for implementation: the teaser is a blocking Sanctum-owned band-3 dialog; story acknowledgement
is separate from the reward checkpoint ledger; the current Player 1/profile is tracked in SQLite rather
than a save-slot feature; PvZ image dumps remain in `rpg-media.sqlite`; serious Rift VFX use the shared
VFX SSOT; failed state/asset/VFX loads never trap the player; and the icon has an explicit square-canvas
contract despite the portrait source file. Deferred by design: final commissioned art, long-term map
marker placement, and any combat use.
