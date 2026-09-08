# spec — `granted-action-text`

Part of [item-content-map.md](../item-content-map.md). Source findings:
[item-content-ideal.md](../item-content-ideal.md) §6.2.

## 1. Objective

`ssot-presentation.md` §9.14/§4.1 already commits to rendering a granted action's name **and
description** as the item card's block 9 — but 114 seeded actions have a name and zero description, and
`rpg_action`'s own schema has no description column to put one in. This module adds the schema, authors
the content, and confirms the card block that already expects it actually renders it.

**Target users:** any player viewing an item that grants an action.

## 2. Acceptance criteria

1. `rpg_action` (`RpgStore.Actions.cs:22-60`) gains a description column (a key, per L3 —
   `ssot-presentation.md` §3.6's rule that content-authored display text is a key plus an argument bag,
   never a literal — matching the pattern already used for `rarity.display_key` and `flavour_key`).
2. Every one of the 114 actions in `data/seed/actions/committed-round-*.json` and `_generated/` gets a
   real, authored description — a sentence a player can act on, not a restatement of the action's
   mechanical parameters. Grounded in the action's real effect (read its real atom/effect definition
   before writing a sentence about it — the same discipline every other module in this program applies).
3. Card block 9 (`ItemCard.cs`, per `ssot-presentation.md` §9.14) renders the description alongside the
   existing name, battle-only tag, and already-known state — confirmed with a real granted action on a
   real item, not a synthetic fixture.
4. The compact list line (armoury row) also carries the battle-only tag, per `ssot-presentation.md`
   §9.14's own explicit ask — "a player scanning an armoury should not have to open each item to learn
   that half of them are inert on the lawn." Confirm this is already true; if not, it is in scope here
   too, since it is the same card-block-9 contract.

## 3. Commands / interfaces touched

- `src/FusionRpg.Data/Sqlite/RpgStore.Actions.cs` — schema addition
- `data/seed/actions/committed-round-*.json`, `_generated/` — content addition (114 entries)
- `src/FusionRpg.Core/Items/Display/ItemCard.cs` — block 9's rendering, if it does not already read the
  new field
- `content/display/en.json` or its DB equivalent — the new description keys

## 4. Project structure

No new files expected beyond the content additions themselves.

## 5. Code style

Match the existing action-seed JSON shape exactly (whatever `committed-round-*.json` already uses for
`name`) — a sibling `description`/`descriptionKey` field, not a new nested structure.

## 6. Testing strategy

- A schema migration test confirming the new column lands cleanly against the existing 114-action
  corpus with no null/empty description after the content pass.
- A card-render test: a real item granting a real action renders its real description in block 9.
- A guard test, matching this program's own established pattern elsewhere (`ItemCardTests.cs:949`'s
  shape): iterate every granted action in the corpus, assert every one has a non-empty description that
  resolves to real text, not a raw key.

## 7. Boundaries

- **Always:** write descriptions against the action's real, verified effect — pull the actual atom/effect
  definition an action grants before describing it, never infer from the action's name alone.
- **Ask first:** whether 114 descriptions is authored by hand in this pass or via a small, scoped
  generative pass (matching this repo's own `seed-to-concrete`/seedsmith conventions) — given the
  volume, a hand-authoring cost estimate should be checked against the seedsmith brief-and-answer pattern
  before committing to one approach over the other.
- **Never:** synthesize a description from the action's mechanical parameters at render time (e.g.
  "deals X damage") as a substitute for real authored prose — that reproduces exactly the "tooltips only
  a designer can read" failure mode `ssot-presentation.md` §8.1 already names and this program exists to
  avoid.
