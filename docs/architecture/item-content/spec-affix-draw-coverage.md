# spec — `affix-draw-coverage`

Part of [item-content-map.md](../item-content-map.md). Source findings:
[item-content-ideal.md](../item-content-ideal.md) §6.3, §6.4.

## 1. Objective

95 of 109 affix families have a real, complete, already-authored display template and cannot appear in
any real dropped item, because `data/seed/items/_tuning/tier-bands.v1.json` only authors a draw weight
for 14. This module is a content-authoring pass against that one file, plus three small, precisely-named
code-adjacent fixes it exposed. It is the one module in this program with a real (if narrow) authoring
cost — everything else in `item-content` is pure wiring.

**Target users:** indirectly, every player — this is what makes "user-friendly descriptions" actually
show up in real drops, not just exist in the template files.

## 2. Acceptance criteria

1. `tier-bands.v1.json` authors a `channelWeightPermille` (or `sharePermille`, matching whichever field
   name the file's own `_meta` currently specifies) row for all 109 families, not 14. Per the file's own
   `_meta` note, this is done via `python -m seedsmith numerics rebalance --publish` — not a hand-edit.
2. `FamilyExpansion.cs:124` no longer refuses any of the 95 previously-missing families — measured by
   drawing a real sample and confirming every family can appear (matching this session's own earlier
   diversity-verification methodology: a real generation sample, not an assumption).
3. `atom.chill-punisher` and `atom.rot-punisher` (authored into `affix-families/g-punisher.json` without
   a matching display-template row) get a real template, grounded in their actual mechanical definition —
   never guessed from the name, matching the standard this session's phantom-family authoring pass
   already set.
4. `atom.elpw-focus`, `atom.elpw-overflow`, `atom.elpw-pierce` get a real `UnitClass` registration
   (`ItemCardTests.cs:1457` already pins the requirement) — grounded in their real channel's actual
   consumer, per `ssot-presentation.md` §2.3's rule that a unit is inseparable from its reader.
5. `ItemCardTests.cs:949`'s `Every_real_atom_renders_at_min_mid_and_max_with_no_raw_id` guard test's scope
   (`RealAtoms`, `:57`) is widened to walk the real, now-larger admitted set once step 1 lands, rather
   than staying scoped to the original 14 — its own doc comment (`:1026`) must be updated to match, so a
   green run means what it claims.
6. `atom.entangling`'s `pending` status (blocked on an unwritten Unity branch for its `UnityCc` status
   payload kind) is **not** touched by this module — it is a mechanism gap, not a tuning gap, and is
   named, not fixed, here.

## 3. Commands / interfaces touched

- `data/seed/items/_tuning/tier-bands.v1.json` — the content file itself
- `python -m seedsmith numerics rebalance --publish` (or whatever the file's current `_meta` names as its
  authoring command — read it fresh before running, since this session's other work may have touched
  neighbouring seedsmith commands)
- `data/seed/items/display-templates/{primary,derived,triggered}.json` — the 2 new template rows
- Wherever `UnitClass` is registered per channel (`ssot-presentation.md` §5.3 N3 —
  `ChannelUnits.For`, prefix-matched) — the 3 new entries
- `tests/FusionRpg.Core.Tests/Items/ItemCardTests.cs` — the widened guard scope

## 4. Project structure

No new files expected — this is content authoring against an existing file plus small, additive
registrations in existing registries.

## 5. Code style

N/A for the content-authoring half (JSON, following the file's own existing shape exactly). For the
`UnitClass` registrations, match the existing prefix-pattern style already used for the other derived
families (`DerivedStatChannels.cs:96-99`'s precedent, cited in `ssot-presentation.md` §5.3).

## 6. Testing strategy

- Before/after corpus measurement: count families refused by `FamilyExpansion` before this module lands,
  confirm it drops to (ideally) zero after — the same measured-not-assumed discipline this session's
  earlier diversity re-checks used.
- The widened guard test (`ItemCardTests.cs:949`) re-run and confirmed green at its new, larger scope —
  and confirmed it would have gone **red** at the old scope's assumptions if any of the 95 families had a
  real defect, proving the widening actually exercises them rather than trivially passing.
- A specific test per new template (chill-punisher, rot-punisher) rendering at Min/Max with no raw id,
  matching the existing per-family test convention.

## 7. Boundaries

- **Always:** ground every new template and every new `UnitClass` entry in the atom's real, verified
  mechanical behaviour — read the consumer code, never infer from the family's name or flavour text. This
  is the exact discipline that closed the 9 phantom implicit families earlier this session, and the exact
  discipline `ssot-presentation.md` §8.1 names as the one thing no validation can catch (authored-but-bad
  prose), so it has to be a practiced discipline, not a checked rule.
- **Always:** treat this as a **content** pass, not a code change, for step 1 — do not hand-edit
  `tier-bands.v1.json`'s numbers directly if the file's own tooling exists to regenerate them; the file's
  own `_meta` already says "never hand-edit."
- **Ask first:** if the real per-family weights the rebalance tool proposes look meaningfully skewed
  (e.g. reintroducing the earlier-found "84 of 98 excluded" class of bug from a different angle) — verify
  against a real generation sample before publishing, the same way this session's earlier tier-bands and
  role×group-matrix defects were caught, not assumed fixed.
- **Never:** invent a draw weight by hand to "unblock" a family faster than the real tool would produce
  one — a wrong weight is exactly how this repo's earlier role×group matrix bug (excluding 84 of 98
  charm families) happened in the first place.
