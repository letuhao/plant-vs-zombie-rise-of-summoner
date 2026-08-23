# Wave 2 review — flavour quality (does the writing belong to this world?)

**Scope read:** every entry in `data/seed/items/` with a `name`, `flavor`, or `counterPressure.note`
field — the full 1,438-entry, 125-file corpus, not a sample. In detail: all 740 base-type entries
(66 files), all 144 unique entries (18 files) including every `counterPressure.note`, all 30 set
entries (5 files), all 70 charm entries (4 files), all 60 consumable entries (3 files), all 40 gem
entries (2 files), all 21 material entries (1 file), all 25 socket-word entries (1 file), all 10
enhancement-milestone entries, and the 98 affix-family / display-template / drop-table / recipe /
curve entries that carry a player-visible name. Read across partitions and across kinds, per the
brief, comparing the same naming problem wherever it recurs rather than judging any one file alone.
Also ran an automated sweep cross-checking every entry's declared atom `element` against elemental
vocabulary in its own flavor text, looking for contradictions.

**Verdict:** sharply bimodal. Base-types, uniques, sets-with-flavor, and half the charms are the best
writing in the project — specific, mechanically grounded, and unmistakably Plants vs. Zombies. But an
entire tier of the corpus (consumables, half the gems, most of the materials, all the socket-words,
and half the charms) reads as generic dungeon-crawler loot text with the serial numbers barely filed
off, and it is missing flavor prose almost everywhere it appears. This is exactly the failure mode
`docs/architecture/item/build-log.md`'s Decision 12 already found and fixed once (humanoid weapons
drawing a "historical European armour glossary" instead of household junk) — it just didn't reach
these later, unreviewed kinds. No BLOCKER. Three MAJOR findings, two MINOR, two NOTEs.

Two sibling reviews already cover adjacent ground in more depth than I repeat here:
`wave2-theme-coherence.md` catalogues exactly which unique/set files carry zero flavor text
(rot-bloom, ember-harvest, thorned-chassis, umbral-swarm-90, and 4 of 5 set files), and
`wave2-frame-tone.md` confirms the plant/humanoid vocabulary split holds up across all 740
base-types. I independently re-derived both of those counts while reading and they match; I do not
re-litigate them below except where they change how a MAJOR finding here should be read. My own
findings are concentrated in the kinds neither of those reviews scoped: consumables, gems, materials,
socket-words, and charms.

## Findings

### MAJOR — consumables are generic fantasy-potion shelf-ware, and none of the 60 has flavor

Every one of `consumables/k1.json`, `k2.json`, `k3.json` (60 entries total) has **no `flavor` field
at all** — confirmed by grep, zero hits in the whole `consumables/` directory. The `name` is the only
text a player ever sees, and a large share of those names are stock alchemy-shop vocabulary that
would be unremarkable in any fantasy RPG: `consumable.k2-001` "Draught of Might", `k2-002` "Swiftness
Brew", `k2-007` "Resilient Potion" (the word "Potion" appears nowhere else in the corpus), `k2-013`
"Umbral Distillate", `k2-014` "Phantom Brew", `k2-017` "Prismatic Essence", `k2-020` "Zephyr Essence",
`k3-014` "Keen Precision", `k3-015` "Radiant Evasion". Counting across all three files: **34 of 60**
names lean on one of `Draught / Elixir / Tonic / Brew / Essence / Potion / Distillate /
Distillation` paired with a stock elemental or stat adjective (Ember, Frost, Radiant, Umbral,
Phantom, Zephyr, Prismatic, Cruel, Keen) that carries no reference to horticulture, salvage, rot, or
the undead — the vocabulary that makes every other kind in this corpus unmistakably PvZ. A handful
buck the trend and land well — `k1-004` "Bark Fortitude", `k1-006` "Bloom Mending", `k3-003`
"Heartwood Draught", `k3-013` "Spore Quickening" — which only sharpens the contrast: the team can
clearly write an on-theme potion name, and mostly didn't here.

This is the same defect class `build-log.md` (Decision 12) already caught and fixed once, in
`humanoid-armament-primary-a`, where a "historical European armour glossary" produced "Keen Sword,
Fleet Sabre, Honed Falchion" against the brief's own explicit rule that generic high fantasy is a
task failure. That fix never reached consumables. Given every consumable is used from the same
per-run manifest and is therefore one of the most frequently re-seen strings in the whole item
system, this is a player-visible, repeat-exposure problem, not a background one.

**Fix:** re-pass consumable naming through the same domain-vocabulary exercise Decision 12 already
ran for base-types (salvage/organic nouns for the delivery vessel, PvZ-specific verbs for the effect),
and add `flavor` — the field is free per `seed-contract.md` and every other player-facing kind uses it.

### MAJOR — 30 of 70 charms have no flavor, and the one file that's silent is also the one that's generic

`charms/econ.json` (20 entries) and `charms/off-ctrl.json` (20 entries) are arguably the best short-form
writing in the entire corpus — `charm.econ-006` "Rustloop": *"Rings rust; the habit of keeping one does
not."*; `charm.off-ctrl-020` "Clockwork Graft": *"It detonates exactly once, exactly on schedule, and
the gears never ask what it costs the rest of you."* Every one of these 40 entries has flavor, and
every one of them is specific, mechanically apt, and impossible to mistake for another game.

`charms/resonance.json` (10 entries) and `charms/surv-util.json` (20 entries) — the other 30 of 70 —
have **zero flavor text**, confirmed against the raw JSON, not just the grep count. `resonance.json`'s
names are also the most generic in the whole charm kind: `charm.res-offense-3` "Threefold Assault",
`charm.res-control-2` "Binding Accord", `charm.res-survivability-3` "Guardian Trinity",
`charm.res-control-3` "Tangled Crescendo" — none of these carries a single word tying them to
horticulture, salvage, or the undead; "Guardian Trinity" in particular could be a paladin's charm in
any generic fantasy game, which the brief calls out by name as the failure mode.

The gap in `surv-util.json` is more frustrating because the connective tissue for good flavor is
already sitting unused in the data: every entry there carries a `notes: "themeId: <unique-theme>"`
field pointing straight at one of the corpus's unique-item themes — `charm.surv-util-007`
"Sepulchral Thorn" is tagged `themeId: charnel-bloom`, `charm.surv-util-008` "Verdant Seedling" is
tagged `themeId: verdant-graft`. The uniques for those exact themes (see below) are full of usable
voice. Nobody wrote the one sentence that would have carried it over.

**Fix:** write flavor for `resonance.json`'s 10 entries and `surv-util.json`'s 20; for the latter, the
`themeId` field already names which unique file's voice each charm should borrow.

### MAJOR — half the gems (`g1.json`) are generic RPG loot-speak; the other half (`g3.json`) prove it didn't have to be

Neither gem file has a `flavor` field (0/40, confirmed by grep — gems appear to be a kind where flavor
was never attempted, unlike charms/uniques/sets), so the name carries the entire identity. `gems/g1.json`
(20 entries) reads as an unmodified action-RPG loot table: `gem.g1-001`–`007` "Ember Shard", "Frost
Shard", "Stone Shard", "Gale Shard", "Radiant Shard", "Umbral Shard", "Primal Shard" — the same six
elements as an alphabetized enchant list — followed by `g1-010` "Aegis Gem", `g1-011` "Lifesteal Gem",
`g1-012` "Retribution Shard", `g1-018` "Might Gem", `g1-019` "Fortitude Gem", `g1-020` "Resilience
Gem". Every one of these fourteen names could be pasted into Diablo, Path of Exile, or any other loot
game without a single edit, and none makes contact with PvZ at all.

`gems/g3.json` (20 entries) is the direct rebuttal: `gem.g3-001` "Keen Bloom", `g3-002` "Cruel Thorn",
`g3-003` "Sharp Spore", `g3-005` "Wild Vine", `g3-011` "Regenerant Husk", `g3-012` "Reinforced Graft",
`g3-013` "Chitinous Carapace", `g3-017` "Cultivator Sprout" — every single entry in this file draws
from the exact botanical/decay vocabulary that makes the rest of the corpus work, mechanically
identical role to `g1.json` (same `gem` kind, same socket-insert function) and clearly not held back
by any technical constraint. This is a same-kind, same-mechanic, half-and-half split with no
structural reason for it — the strongest single piece of evidence in this review that the generic
half of the corpus is a coverage gap in authoring attention, not a limitation of the format.

**Fix:** re-name `g1.json`'s fourteen generic entries using the vocabulary `g3.json` already
demonstrates works for the same kind.

### MINOR — materials mix a strong salvage tier with a flatly generic essence/shard tier

`materials/materials.json` (21 entries, no `flavor` field on any of them) splits cleanly into three
tiers by `materialClass`. The `substrate` tier (8 entries: `material.011`–`018`) is genuinely good —
it differentiates humanoid-frame grades from plant-frame grades with distinct vocabulary at every
step (Crude Scrap → Sound Metal → Fine Plating → Prime Forging for humanoid; Green Scraps → Sound
Heartwood → Fine Grain → Prime Heartwood for plant), which is a small piece of real worldbuilding
through naming. The `essence` tier (6 entries: `material.001`–`006`) is plain elemental fantasy —
"Ember Essence", "Frost Essence", "Gust Essence", "Stone Essence", "Radiant Essence", "Shadow
Essence" — indistinguishable from a generic six-element crafting reagent set, and carrying the tags
`arcane` / `necrotic` on top, which are themselves stock D&D-adjacent vocabulary. The `shard` tier (4
entries: `material.007`–`010`) doesn't even reach for a name — "Common Shard", "Rare Shard", "Epic
Shard", "Legendary Shard" simply restate the item's own rarity word as its identity, the least
effort any entry in the corpus makes at flavor.

**Fix:** lower priority than consumables/charms/gems above since materials are crafting-menu
currency rather than an equipped, dwelt-on item, but the essence and shard tiers are exactly the
"any fantasy game" text the brief flags, and the substrate tier proves the fix is cheap.

### MINOR — socket-words have no flavor and split generic/on-theme roughly down the middle

`socket-words/sockwords.json` (25 entries, 0/25 have flavor). Half read as stock action-RPG
combo-word naming — `sockword.002` "Inferno Grip", `sockword.003` "Bastion Core", `sockword.005`
"Lightning Cascade", `sockword.011` "Shield Pact", `sockword.017` "Blaze Tempest", `sockword.024`
"Twilight Ward", `sockword.025` "Endless Onslaught" — while the other half is properly on-theme:
`sockword.004` "Vital Bloom", `sockword.006` "Earthroot", `sockword.007` "Piercing Thorn",
`sockword.008` "Retaliating Sap", `sockword.009` "Rapid Bloom", `sockword.013` "Evading Root",
`sockword.018` "Steadfast Graft", `sockword.023` "Wild Flourish". Same file, same author pass, same
split as the gems above — this reads like naming effort that ran out partway through a 25-entry list
rather than a deliberate choice.

**Fix:** lower priority than the charms/consumables/gems findings — socket-words are a late-game
combination bonus with lower visibility than an equipped item's own name — but worth a pass with the
same botanical/decay word list used successfully in `g3.json` and the base-types.

### NOTE — enhancement-milestones are pure function labels with no attempted personality

All 10 entries in `enhancement-milestones/milestones.json` follow one template: "Enhancement" plus a
stock RPG stat word (Vigor, Edge, Aegis, Quicken, Keen, Fortify, Savagery, Hardy, Recovery, Evasion).
No flavor field, and the name itself makes no attempt at world-voice. This may be intentional — a
milestone is an upgrade-tier label a player sees in a progress list, not a discovered or equipped
item — but `seed-contract.md` calls flavor "expected on anything player-facing," and a milestone name
is exactly that. Flagging as an observation, not a defect, since I can't tell from the data whether
this kind was ever meant to carry voice.

### NOTE — no flavor-contradicts-mechanics case found in an automated element sweep

Ran a script cross-checking every entry's declared atom `element` param against elemental vocabulary
(fire/ice/air/earth/light/dark synonym lists) appearing in its own `flavor` text, across the whole
corpus. It surfaced nine candidate hits; all nine were false positives on inspection — "fires" used
as a verb for shooting, "in the dark" used non-elementally, and one genuine double-image
(`charm.off-ctrl-015`, a fire-element charm whose flavor reads *"It opens once, on the last warm day
before the frost, and it burns like it knows"*) that is a deliberate last-warm-day-before-winter
image, not an error. I did not find a single entry where the flavor text actively describes the wrong
element, damage type, or role for what the item mechanically does — the failure mode in this corpus
is absent or generic prose, never contradictory prose.

## The best of it — the quality bar to hold the rest to

- `set.sunwoven-almanac-005` "Firstedition": *"Every printing since has fixed a typo the first one
  never had, and every printing since has also lost the one thing the first one got right by
  accident."* The best line in the corpus — a whole theme (doctrine/inheritance) compressed into one
  sentence that also works as a joke.
- `unique.gilded-porcelain-70-004` "Lacquered Lid": *"It does not fight. It taxes."* Four words that
  state the mechanic (pure economy item, no combat rider) more precisely than the counterPressure
  note does.
- `unique.rusted-legion-50-002` "Surplusward": *"Crates come stamped PROPERTY OF SOMEONE ELSE. Wear
  one long enough and the stamp stops mattering."* Exactly the scavenger-army bureaucracy voice the
  base-types establish, paid off at unique-item weight.
- `unique.windswept-spore-50-008` "Untethered Thistledown": *"No bud on this crown ever waited to be
  picked. It was already gone before the bloom finished opening."*
- `charm.econ-020` "Signet of Annexation": *"Claim the neighbor's yield, and the strength to defend
  your own thins to pay for it."* A drawback charm whose flavor states the actual tradeoff instead of
  decorating around it.
- `item.humanoid-off-hand-a-003` "Printed Gazette": *"A month-old newspaper, rolled tight. Useless as
  reading material but oddly solid as a tool."* — representative of the 740-entry base-type set, which
  is the deepest and most consistent writing in the corpus and never once reaches for generic fantasy
  vocabulary across either frame.

## What I could not check

- I did not open `tools/ItemSeedValidator/Checks/` before writing this, per the brief's instruction to
  trust the 0-error gate for everything it's already declared to enforce (tag vocabulary, naming
  patterns, cross-references). If flavor-field presence turns out to be schema-checked somewhere I
  didn't find, the coverage counts above still stand as fact; only the "is this a defect" framing
  would need revisiting.
- I did not re-verify every one of the 144 `counterPressure.note` strings for internal mechanical
  accuracy against the atom registry (e.g., whether every claimed affix-count baseline for a given
  rarity rung is correct) — that is a mechanics-correctness question for a different lane, not a
  writing-quality one. I did read all 144 for tone and found the writing strong and consistent
  wherever it exists; the only defect I have standing to report on them is the coverage gap already
  detailed in `wave2-theme-coherence.md`.
- Affix-family, curve, display-template, drop-table, and recipe names are functional/internal labels
  (curves and display-templates are never shown to a player as authored strings; recipes and drop-tables
  are compound labels like "Forge: Cloth Armor" and "Compost Cache" built from a fixed verb list) — I
  skimmed all of them for egregious genericness and found none worth a finding, but did not treat them
  as flavor prose the way I did names+flavor on the other kinds, since that isn't what they are.
