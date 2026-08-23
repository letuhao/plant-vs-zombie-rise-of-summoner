# Wave 2 review: frame tone (plant vs. humanoid)

**Summary.** Read all 740 base-type entries in `data/seed/items/base-types/` (all 15 equip
roles, both humanoid and plant frames, both `a`/`b` bands where present — the full population,
not a sample), plus all 144 unique-item name/frame/baseType triples in `data/seed/items/uniques/`
and the 8 frame-tagged rows in `data/seed/items/materials/materials.json`. Sets, charms, gems,
socket-words, consumables, and drop-tables carry no per-entry frame field and are out of this
lane's scope (confirmed by grep — only `base-types`, `uniques`, `materials`, `drop-tables`, and
`recipes` declare `"frame"`, and drop-tables/recipes reference existing items rather than
authoring new name/flavor text).

**Verdict: clean.** The split holds up under cross-partition reading. Across 740 base-type
entries the botanical vocabulary on the plant side is deep and specific (real anatomical terms —
`phloem`, `xylem`, `caudex`, `hypocotyl`, `pneumatophore`, `spadix`, `involucre`, `laticifer` —
not just "leaf" and "vine" repeated), and the humanoid side stays inside a consistent
scavenger/salvage register (garden tools, car parts, kitchenware, army-surplus, bureaucratic
paperwork) without borrowing plant material words for itself. I found exactly one sentence where
the register breaks, and a small number of borderline cases that read fine on inspection. No
MAJOR or BLOCKER.

## Findings

### MINOR — one plant weapon flavor line names a firearm

`item.plant-muzzle-a-007` ("Laden Popper", armament-primary/plant/band-a):

> "Heavy with seed and spite, it splits the air with the crack of a **fired pistol**."

Every other entry in this 24-item partition (`plant-armament-primary-a.json`) describes its own
report/impact in organic terms — a whip-crack, a shattering husk, a hiss of released pressure.
This is the only one that reaches for a manufactured firearm to describe itself, and it's an
exact-noun reach ("a fired pistol"), not a vague "like a gunshot" aside. It's one line in one of
740 entries, so it's not a pattern — but it's the one line in the whole corpus where a plant item
describes its own voice using humanoid hardware.

**Fix:** swap "a fired pistol" for an organic-mechanism comparison consistent with its siblings
(e.g. a bursting seed-pod, a whip-crack of green wood) — a two-word edit, no other field touched.

### NOTE — retinue role's summon-effect language borrows plant verbs on the humanoid side

`item.humanoid-horn-a-002` ("Engraved Kazoo"): "A humble toy that grows something where it was
played." `item.humanoid-horn-b-004` ("Official Boombox"): "Flowers break ground in rhythm with
the bass."

Both are in the retinue role (`horn`/`runner`), whose mechanical effect is very likely "summon a
plant ally" regardless of which frame is wearing the item — so "grows" / "flowers break ground"
plausibly describes the effect being triggered, not the kazoo's or boombox's own material. I'm
not counting this as a violation for that reason: the object nouns throughout this 24-entry
humanoid partition stay firmly non-botanical (whistle, rattle, bell, pager, chime, gong,
intercom, alarm, siren, drum, klaxon, megaphone), and only the effect-description clause dips
into plant language, twice out of 24. Flagging it because it's the only place in the corpus
where humanoid-frame text uses growth vocabulary at all, and if the mechanic turns out not to be
plant-specific, both lines should be revisited.

### NOTE — one jewel-minor-a/graft-1 name leans on a jewelry term rather than a plant one

`item.plant-graft-1-a-004` ("Ephemeral Inlay"): "Inlaid only for a moment, a brief augmentation
that fades like morning mist."

"Inlay" is a jewelry/woodworking term, not a plant-anatomy word, and it's paired against a role
(`ring-1`/`graft-1`) whose whole premise already straddles jewelry-slot and plant-graft — the
registry names this role `ring-1` for humanoid and `graft-1` for plant specifically because a
graft *is* an insert, so some overlap in "inserted thing" vocabulary looks intentional rather
than a drift. Every other entry in this partition uses plant-graft nouns (cutting, gemma, splice,
callus, rootstock, node). Not asking for a rewrite — noting it because it's the single entry in
the partition where the noun itself isn't botanical.

### NOTE — a small number of plant defensive items use "armor"/"shield"/"weapon" as similes

`item.plant-crown-b-004` ("Gnarled Cupule"): "It wears its deformation like armor."
`item.plant-crown-a-007` ("Layered Tuft"): "Together they overlap like shields."
`item.plant-manipulator-b-001` ("Dense Midrib" — actually role `manipulator`): "The vein itself
becomes the weapon."

These are similes describing the item's protective/offensive function, not its material — the
name and the rest of each flavor line stay botanical (cupule, tuft, midrib/veins). Given every
item here is combat equipment regardless of frame, reaching for "armor" or "weapon" to describe
what a defensive/offensive plant part *does* reads as normal English, not humanoid-register
bleed. Listed only so the owner has the full set of borderline candidates I considered and ruled
out, rather than a silent pass.

## What I did not check

- **Uniques' and sets' `notes` / `counterPressure.note` fields** — these are authoring-rationale
  prose aimed at the validator/reviewer, not player-facing flavor text, so they're not this
  lane's concern; I did not read all of them for tone.
- **Icon keys, tag vocabulary, class-rung ids** — these are validator-governed closed vocabularies
  per the brief's "already checked" list, not free text, so not re-reviewed here.
- **charms, gems, socket-words, consumables, curves, enhancement-milestones, affix-families** —
  none of these declare a `"frame"` field (confirmed by grep across the whole corpus), so there is
  no plant/humanoid split in them to judge for tone.
