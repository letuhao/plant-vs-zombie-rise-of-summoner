# Passive-tree content localization — the ideal

**Status: SUPERSEDED 2026-09-08** by
[seedsmith-content-standard-ideal.md](seedsmith-content-standard-ideal.md) — the same continuous
idea-phase session, minutes later: the owner directed that this stop being passive-tree-only and
become a repo-wide seedsmith standard ("we will cover everything here, not only passive skills").
Kept, not deleted — this doc's own passive-tree-specific inventory (file:line findings) is still
correct and is cited directly from the superseding doc rather than re-derived. Read the superseding
doc first; come back here only for the passive-tree-specific detail it doesn't repeat.

**Original status:** idea phase, 2026-09-08. Not a spec. No build authorized. Extends
[passive-tree-ideal.md](passive-tree-ideal.md) / [passive-tree-map.md](passive-tree-map.md) — this
doc does not repeat that program's other 45 decisions, only the ones this feature touches.

## Which loop this extends

**Spine loop A — Level up and power.** `the-loops.md`'s own row for this loop names passive trees
directly: *"species builds and meters WIP · passive trees Vision."* This feature does not add a new
loop; it is the part of the *existing* passive-tree Vision item that makes the tree's own content
readable by a player at all — right now a player allocating points sees raw internal ids, not the
game the tree is supposed to be teaching them.

## What this is

Passive-tree's own generation pipeline (`tools/seedsmith`) already writes real, model-generated,
player-facing English text for every node — a `name`, a `nameKey`, and a `flavor` sentence — into
`data/seed/passive-tree/nodes/<treeId>.json`. **None of it reaches a player.** The catalog schema
that ships to the game (`TreeRecord`/`NodeRecord`, `spec-tree-catalog.md` §2) has no field for it,
the server's wire DTOs have no field for it, and the web UI's own code says so out loud
(`PathLattice.tsx:316-318`: *"No authored name/effect exists yet for any node in the corpus ... the
real node id stands in"*). A player today sees `might-off-t1-n0`, not a name.

This feature is the missing link: get that already-generated (or, for whole categories, not-yet-
generated) text from seedsmith's own output into the game, in a shape that can carry more than one
language later without re-touching the mechanical catalog. The user's own framing settles the two
questions that matter most up front, stated here because a downstream reader must not re-litigate
them: **this is not a Lingui/FE concern** (Lingui is `docs/web/spec.md` §6's "Chrome text" system —
hand-authored UI labels extracted from source code; this is seedsmith-generated content, the OTHER
of the spec's own "two text systems," which it calls "server data, locale-tagged" and currently
means only the base game's own pre-existing multi-language creature/almanac strings) — and **it
lives in a data subfolder, produced by a seedsmith sub-pipeline that runs after the tree/node already
exists**, not folded into the original generation pass.

## What already exists

**Built:**
- The English text itself, for nodes, already exists in the real committed corpus.
  `data/seed/passive-tree/nodes/ferocity.json:27-30` — a real node with `"name": "Thickened
  Marrow"`, a real `flavor` sentence, and a `nameKey` (`tree.node.<slug>` grammar,
  `nodegen/emit.py`). 1677/1680 real nodes across all 42 generic trees carry this today (this
  session's own H9 work), plus a growing real species batch.
- `nameKey` is already a stable, corpus-wide-unique machine key
  (`nodegen/run.py`'s `known_name_keys`, seeded from the whole ledger, not just one tree — fixed
  2026-09-06 after a real cross-tree collision). It is the right SHAPE for a translation-catalog
  key (stable, unique, independent of the display text) even though nothing treats it as one today.
- Species trees have a real, generated, player-facing REWARD sentence —
  `codexSummary` (`tools/seedsmith/seedsmith/adapters/trees/species/generate_codex.py`),
  persisted to `data/seed/passive-tree/species/<speciesId>.json`. This is the closest existing
  analog to "tree description," for one of four categories.
- The web app's own general i18n system (Lingui, `web/fusion-rpg-web/src/i18n/`) is real, working,
  English-first, and explicitly built to make a second locale "cheap" (`docs/web/spec.md` §6) —
  for Chrome text. Not this feature's mechanism, but proof the product already intends to ship more
  than one language eventually, so this feature is not inventing a requirement from nothing.

**Wiring gap** (the content exists; the pipe to the player does not):
- `NodeRecord.cs:37-52` (the catalog schema `tools/TreeBinder` writes and
  `PassiveTreeCatalogLoader`/`PassiveTreeImportRunner` read) has no name/flavor field at all — the
  seed document one directory up already has this text, and the catalog assembly step
  (`tools/TreeBinder/ReportWriter.cs`, rebuilt this same session for the mechanical fields) simply
  never carries it through. Adding the field is additive to a pipeline that already threads
  identity data from plan → seed → catalog for every other field.
- `PassiveTreeEndpoints.cs`'s `ProjectState` / `TreeNodeSummaryDto`
  (`src/FusionRpg.Contracts/PassiveTreeDtos.cs:25-27`, own comment: *"no node anywhere in the
  corpus has a player-facing name or effect sentence today"*) — stale as of this session (nodes
  now DO carry names), but the DTO shape itself was never extended once they did.
- `web/fusion-rpg-web/src/lib/bus/types.ts:516-521`'s `TreeNodeSummary` type and
  `PathLattice.tsx:319`/`TraitDetail.tsx:133`'s own fallback-to-raw-id rendering — the FE component
  layer already has the exact slot (a label) this content would fill; it currently fills it with
  the id because nothing upstream has ever sent it anything else.
- `codexSummary` (species reward text) is fully generated and **fully inert** — zero consumers in
  `src/` or `web/` (confirmed by direct grep). Same shape of gap as the node text, one level up.

**Real gap** (nothing exists yet):
- **Tree-level name/description for the other three categories** (primary, elemental, status).
  `tree_reading` — the one field that looked like it might already be a "tree's own reading" —
  is confirmed, at every real call site (`report/cli.py:1176`, `species/generate_tree.py:120`), to
  always equal the tree/species id verbatim, never a distinct authored string
  (`cli.py:1156-1159`'s own comment: *"a shared/mechanical tree like `might` carries no authored
  display name yet"*). There is no generation stage, no schema, no storage for this at all —
  species' `codexSummary` is the only precedent, and it answers a narrower question ("what does
  building into this reward") than a general tree name/description would.
- **Any locale dimension anywhere in the pipeline.** No `locale` parameter on a brief, a schema, a
  prompt, or a stored record. No per-locale storage split — `name`/`flavor` sit inline in the
  single, English-only mechanical seed document today.
- **A re-entrant "generate more locales for an already-shipped tree" sub-pipeline** — the user's
  own explicit ask. The closest existing sibling mechanism is J4's `diff.py`
  (`tools/seedsmith/seedsmith/adapters/trees/review/diff.py`), which already proves the shape
  "revisit an already-generated tree without re-touching its mechanics" is buildable in this
  program (it does it for CONTENT REVISION, not translation) — a real, reusable precedent for HOW
  a post-hoc pass would be structured, not a component to reuse directly.
- **A locale-aware review/QA gate.** J2/J3's census and escalation ladder judge English generation
  quality only; nothing today would catch a translation that overflows a lattice cell's label
  width or drifts a proper noun's spelling across 40 nodes of the same tree.

## Prior art

Web-sourced, dated 2026-09-08, cited for the numbers/patterns, not repeated as fact where a source
could not be found:

- **The industry's two-system split matches this repo's own, by different names.** Unity's
  documented distinction is *static strings* (fixed UI text, pre-extracted into a String Table)
  vs. *dynamic strings* (runtime-generated/variable content, requiring "runtime-resolved
  localization" — [Unity Localization docs](https://docs.unity3d.com/Packages/com.unity.localization@1.2/manual/QuickStartGuideWithVariants.html)).
  Unreal's is `FText` **Namespace/Key/Source-String** triples managed through a Localization
  Dashboard ([Epic docs](https://dev.epicgames.com/documentation/en-us/unreal-engine/text-localization-in-unreal-engine)).
  This repo's own "Chrome text vs. content text" (`docs/web/spec.md` §6) is the same split under a
  third name — passive-tree content is squarely the "dynamic strings" / "content text" side in
  every naming scheme found.
- **For genuinely TEMPLATE-generated text** (an item name built from `<prefix> <base> of <suffix>`
  grammar), the documented industry pattern is NOT "translate the English output" — it is
  **re-implementing the generation grammar itself per language**, so word order and grammatical
  gender/case can differ per locale
  ([AllCorrect Games / MultiLingual, July 2023](https://multilingual.com/issues/july-2023/how-to-localize-a-game-with-procedurally-generated-text/)).
  Cited concrete cases: *Rogue Legacy 2*'s "Material + Equipment" name reorders per language
  (English "Leather Cape" vs. Italian "Mantello di cuoio," noun-first); *Star Dynasties* uses real
  gender/case-declension functions per language for its generated character text. **This pattern
  does not map cleanly onto passive-tree's content**, which is free LLM prose, not slot-filled
  grammar — there is no template to re-implement per locale, only a sentence to translate or
  re-generate. Flagged as the one piece of prior art that does NOT transfer directly, stated so a
  downstream reader does not assume it does.
- **Text-expansion budgets are real and documented, with numbers**: German averages 30-40% longer
  than English overall, but SHORT strings expand far more — 100-200%, up to 300% in extreme cases
  for strings under ~10 characters, only settling near 30% past ~70 characters
  ([SimpleLocalize, citing W3C/IBM guidance](https://simplelocalize.io/blog/posts/text-expansion-ui-localization/)).
  A node's `name` (short, e.g. "Thickened Marrow") is in the worst expansion band; its `flavor`
  sentence (longer) is in the mild band. This is a real constraint on the LATTICE UI's own label
  width, independent of which translation strategy is chosen.
- **LLM-driven translation for generated content is real but unsettled industry-wide.** Vendor
  claims (not independently verified — flagged as such) cite AI-augmented localization as "up to
  80% faster" and "2-4x cheaper" than pure human translation
  ([Gridly](https://www.gridly.com/blog/ai-translation-game-localization/)). The one *consistently
  repeated* real failure mode across sources: **an LLM translating invented/coined terms (a
  proper noun, a made-up ability name) drifts without a maintained glossary** — i.e. the same
  species/node name can translate inconsistently across 40 nodes of the same tree without an
  enforced term list. No source settled whether translating the English OUTPUT vs. re-prompting
  NATIVELY per target language is the better practice for LLM-generated fantasy content — flagged
  as genuinely unverified/unsettled, not defaulted either way here.

## The shape

Not decided here — this is a "which shape" question this doc surfaces rather than resolves (see
Open questions) — but the real candidates, narrowed by what the inventory above already rules out:

1. **A parallel, per-locale JSON catalog under a new `data/seed/passive-tree/i18n/<locale>/`
   subtree, keyed by the SAME `nameKey`/a new tree-level key**, mirroring this program's own
   established "content lives under `data/seed/<program>`, keyed by a stable id" convention used
   everywhere else in this session's own work (H1-H9, J1-J12) — not a `.po`/Lingui catalog, which
   is a developer-source-code-extraction format with no natural fit for 35,000+
   dynamically-generated entries. The mechanical seed document (`nodes/<treeId>.json`) would carry
   `nameKey` only (already true) and drop `name`/`flavor` from it once a locale catalog exists —
   or keep English inline as the `en` catalog's own source-of-truth copy, never duplicated by hand.
2. **A tree-level equivalent of `codexSummary`** for the three categories that lack one — a real,
   new generation stage (schema + brief + review), not a translation concern by itself. This may
   be its own, separate feature ahead of localization, since translating a tree description that
   does not exist yet is not meaningful — the "real gap" section above names this explicitly so it
   is not silently assumed already solved.
3. **The re-run/second-pass mechanism**, structurally close to J4's `diff_tree` (read the already-
   committed English record, emit a per-locale catalog entry alongside it, never re-mint an id or
   touch the mechanical fields) — but answering a different question (produce a NEW locale entry,
   not detect a CHANGE to an existing one).

## Tunables

None yet — this phase introduces no numeric balance surface. If a review-pass acceptance threshold
(e.g. a maximum allowed length-overflow ratio for a translated label, echoing the real 100-300%
short-string expansion band cited above) is later adopted, it belongs in
`data/tuning/passive-tree-i18n.v1.json` under this program's own existing tunables convention
(`tunables-ssot.md`) — not a `const`, since a later balance/UX pass would want to move it.

## What this deliberately does not decide

- Whether translation is human, LLM-translate-the-English-output, or LLM-re-prompt-natively per
  locale — genuinely unsettled in the prior art, and a real cost/quality tradeoff, not a detail.
- Whether tree-level name/description (the real gap, category-wide) ships as part of this feature
  or as its own prerequisite feature first.
- The exact catalog file format/key scheme (candidate named above, not locked).
- Whether the review pipeline (J2/J3) gains a locale-aware gate in this same feature or later.
- Which second locale, if any, is targeted first, or on what schedule — this doc is about the
  ARCHITECTURE that makes a second locale possible, not about committing to producing one (mirrors
  `docs/web/spec.md` §6's own "a second locale is enabled by this work, not delivered by it").

## Open questions

Each answerable by the owner, none blocking a future `/spec` pass on its own (a default could be
named for each, but none is defaulted here since the shape itself is still open):

1. **Does this feature include building tree-level name/description content itself** (the real gap
   for primary/elemental/status categories), or is that scoped as a separate, prior feature this
   one depends on?
2. **Translation strategy**: translate the already-generated English text (cheaper, more
   consistent id-to-id mapping, but a known LLM proper-noun-drift risk per the prior art above), or
   re-generate natively per locale (more idiomatic per language, but full "days of machine time"
   cost again, per locale, and its own review pass)?
3. **Catalog key scheme**: reuse `nameKey` as-is for nodes, and invent a parallel `tree.<treeId>.name`
   /`.description` key scheme for trees — or something else?
4. **Does a translated entry need to pass through an equivalent of J2/J3's review ladder** before
   shipping, or does translation quality get a lighter/no gate at first, matching the "English only
   at launch" precedent `docs/web/spec.md` already set for Chrome text?
