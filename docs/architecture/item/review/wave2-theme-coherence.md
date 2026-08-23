# Wave 2 review — theme coherence

**Scope read:** all 13 theme entries in `themes.v1.json` against every file that carries their
content: 18 `uniques/*.json` files (144 entries) and 5 `sets/*.json` files (30 entries) — the full
174-entry corpus, 100% of what exists. For every theme I compared names, tags, frame/element
choices, and (where present) flavor prose across every partition that theme has, against the
theme's own `identity`, `toneWords`, and `avoid` list in the registry.

**Verdict:** mixed, and the split is not about tone drift — every theme's *names* land on-register.
The real fault line is flavor-text coverage: three themes carry no player-facing prose anywhere in
the corpus, one theme is voiced in one band and silent in the other, and four of five sets go silent
even where their sibling uniques speak clearly. Six themes are clean end to end. No BLOCKERs; the
flavor gaps are MAJOR because a player would notice a tooltip that just stops having prose, and two
tag-flag inconsistencies are minor curation-only NOTEs.

## Findings

### MAJOR — three themes have zero flavor text anywhere in the corpus

`rot-bloom`, `ember-harvest`, and `thorned-chassis` are the only themes where **every** entry that
exists for them — uniques and (for thorned-chassis) its set — has no `flavor`/`flavorKey` field at
all. Every other theme's uniques files carry full flavor on every entry (checked via `grep -c
'"flavor"'` against entry counts across all 18 unique files and all 5 set files).

- `rot-bloom-30` (its only file): `unique.rot-bloom-30-001` through `-008`, 8/8 entries, no flavor.
  No set exists for this theme.
- `ember-harvest-30` (its only file): `unique.ember-harvest-30-001` through `-008`, 8/8, no flavor.
  No set exists for this theme.
- `thorned-chassis-30`: `unique.thorned-chassis-30-001` through `-008`, 8/8, no flavor.
  `sets/thorned-chassis.json`: `set.thorned-chassis-001` through `-006`, 6/6, no flavor.
  This is the only theme where **100% of its footprint in the corpus** (14 of 14 entries) is silent.

The names alone are on-register for all three — rot-bloom's compost vocabulary (Rotwake, Wormtithe,
Mouldreign), ember-harvest's hoarded-warmth vocabulary (Hoarded Thresher, Banked Casing,
Warmthkeeper), thorned-chassis's scrap vocabulary (Gridiron of Salvage, Scrapthorn, Ironstem) all
read correctly against their theme's tone words. But a theme's voice is carried by more than a noun
pool: every other theme in the corpus backs its names with a sentence of prose that does the actual
work of "reads as itself." These three themes have none. Next to a player-facing corpus where 10 of
13 themes speak in full sentences, these three are conspicuously mute — that is the coherence defect,
not the vocabulary.

**Fix:** write flavor for these three files before shipping; `seed-contract.md` itself calls flavor
"expected on anything player-facing" even though the schema keeps it optional.

### MAJOR — umbral-swarm is voiced in one band and silent in the other

`uniques/umbral-swarm-50.json` (8/8 entries) has a strong, consistent "faceless mass" voice —
`unique.umbral-swarm-50-001` "Roiling Chainsaw" ("the whole snarling crowd condensed into one length
of steel"), `-003` "Yoke of the Multitude" ("A hundred necks under it move as one thing, and that
thing does not scatter"), `-006` "Engulfing Fist" ("A thousand hands remember closing"). Every entry
avoids the theme's own "avoid" list cleanly: no named-champion framing, no light imagery, no floral
necrosis borrowed from charnel-bloom.

`uniques/umbral-swarm-90.json` (`unique.umbral-swarm-90-001` through `-008`) has zero flavor on all
8 entries, despite being the same theme, same batch tag (`uniques-1c`), authored the same day. The
names in -90 do continue -50's concept pool correctly (Manhole of the *Multitude* echoes -50's Yoke
of the *Multitude*; Collar of the *Swarm* echoes -50's Collar of the Hunger; Faceless Chainsaw makes
facelessness explicit) — so the two bands were clearly drawing from the same pools and would read as
one theme if -90 had the prose -50 has. As shipped, half the theme is fully voiced and half reads
like a stat sheet.

**Fix:** write flavor for `umbral-swarm-90`'s 8 entries; the naming groundwork to match -50's voice
is already in place.

### MAJOR — 4 of 5 sets are silent even where their sibling uniques speak clearly

Only `sets/sunwoven-almanac.json` has flavor on every entry (6/6). The other four set files are 0/6
each:

- `sets/frostbitten-vanguard.json` — `set.frostbitten-vanguard-001` through `-006`, 0/6.
- `sets/rusted-legion.json` — `set.rusted-legion-001` through `-006`, 0/6.
- `sets/thorned-chassis.json` — already counted above under the total-silence finding.
- `sets/verdant-graft.json` — `set.verdant-graft-001` through `-006`, 0/6.

frostbitten-vanguard, rusted-legion, and verdant-graft all pass the coherence test on their uniques
(see the clean list below) — their names, elements, and flavor prose read as one deliberate voice.
But their sets are the most concentrated, capstone expression of the theme (2/3/4/6-piece thresholds,
the "signature" flagship item) and none of the three says a single sentence. `sunwoven-almanac` is
the control case here: its set (`Ancestral Bine`, `Firstedition`, `Copyhand`, etc.) is exactly as
voiced as its uniques and reads as the same doctrinal-inheritance theme without a seam — proving the
gap in the other four is a coverage miss, not a structural reason sets can't carry flavor.

**Fix:** write flavor for the 24 silent set entries across these four files; `sunwoven-almanac.json`
is the working template for tone and length.

### NOTE — "signature" tag applied inconsistently across unique files (adjacent to this lane)

Every unique file tags all 8 entries `signature` except two: `frostbitten-vanguard-30` (0/8) and
`thorned-chassis-30` (0/8). This isn't a voice problem — it's a curation flag, not prose — but it's
exactly the kind of thing no single author could see (16 other files use it uniformly). Flagging for
the owner in case it's an oversight rather than a deliberate call on these two partitions.

### NOTE — verdant-graft's set tags every entry "signature", its four set siblings tag only the grand set

`sets/verdant-graft.json` marks all 6 entries `signature`. Its three comparison sets
(`frostbitten-vanguard`, `rusted-legion`, `thorned-chassis`) each reserve `signature` for only their
one 6-piece grand set, keeping the other five untagged. If `signature` is meant as a flagship marker,
tagging every entry dilutes it for this one theme relative to its siblings. Could be an intentional
per-theme call (all six really are meant to read as flagships); flagging because it's visibly
different from every other set file's convention.

## Themes that hold together cleanly

Read in full (all their files, uniques and set where one exists) and found internally consistent,
on-register against their own `identity`/`toneWords`, and clear of their `avoid` list:

- **charnel-bloom** (`charnel-bloom-70`, `-90`, no set) — funeral/mortuary vocabulary (cerement,
  ossuary, requiem, wake, barrow, necropolis) stays specific to battlefield death across both bands,
  and never drifts into rot-bloom's more general compost-cycle register or umbral-swarm's faceless-
  horde framing, exactly as its own avoid list requires.
- **earthen-bastion** (`earthen-bastion-70` only) — patient/fortified voice throughout ("It has not
  fired in a season. It has not needed to"), no offense-first framing, no airy/mobile language
  borrowed from windswept-spore. Single file only; see limitations below.
- **gilded-porcelain** (`gilded-porcelain-70` only) — uncanny-doll register held all the way through
  (clockwork, filigreed, lacquered, hinges), zero organic/plant tags anywhere in the file, wry
  detached tone throughout ("It does not fight. It taxes."). Single file only.
- **hollow-orchard** (`hollow-orchard-30`, `-90`, no set) — hushed/solemn voice consistent across
  both bands, including a recurring "fence line" motif that reinforces rather than repeats itself
  (`-30`'s Lastorchard: "Everything past the fence line went quiet"; `-90`'s Wardenbough: "Nothing
  crosses the fence line unlooked-at"). No codified-doctrine vocabulary bleeding in from
  sunwoven-almanac.
- **sunwoven-almanac** (`sunwoven-almanac-50`, `-90`, and its set) — the strongest result in the
  corpus: doctrine/inheritance vocabulary (ledger, margin, footnote, canon, litany, scribe, printing)
  carries identically across all three independently-authored files, explicitly avoids
  chosen-one framing ("not because the aim is perfect, but because the lesson was") and never reads
  as a physical place the way hollow-orchard does. This is the one theme where uniques and set are
  equally voiced — see the set-silence finding above.
- **windswept-spore** (`windswept-spore-50` only) — airy/dispersive voice held precisely, explicitly
  refuses to "commit to a patch of ground" (the theme's own avoid line against earthen-bastion's
  rootedness). Single file only.

Also checked and clean, not written up as findings because nothing was wrong: element-affinity
compliance (every theme with a declared `elementAffinity` uses only that element on its
element-carrying atoms; the four themes with no affinity use no element or the explicit `omni`
signal) and frame-affinity compliance (plant/humanoid/both allocation matches each theme's declared
`frameAffinity` in every file, sampled across all 18 unique files).

## What I could not check

- **rot-bloom, ember-harvest, earthen-bastion, gilded-porcelain, windswept-spore** exist as a single
  file each. The brief's cross-partition method (comparing what independent authors did with the same
  theme) doesn't apply to these five — there is only one author's impression to read, so I fell back
  to checking that impression against the registry's own identity/avoid list rather than against a
  sibling file. A second partition for any of these could still reveal drift this pass can't see.
- I did not open `tools/ItemSeedValidator/Checks/` line by line before writing this report, per the
  brief's instruction to trust that a 0-error gate already covers JSON shape, id/nameKey rules, tag
  vocabulary, cross-references, and the naming/collision rules. If any of my flavor-coverage or
  tag-flag findings turn out to be schema-enforced elsewhere, they are duplicates, not new information.
