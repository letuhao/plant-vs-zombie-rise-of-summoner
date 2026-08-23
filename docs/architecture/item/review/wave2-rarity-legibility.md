# Wave 2 review — rarity legibility (does the power ladder read as a ladder?)

**Scope read:** all 18 `uniques/*.json` files (144 entries, 100% of the unique corpus), read across
partitions rather than within one, plus the rarity ladder in `core.v1.json`, the powerBand tier
scale and per-family "anchor band" notes in `_registry/bands.v1.json` and `affix-families/*.json`,
and the worked examples in `docs/architecture/item/ssot-uniques.md` §7 (specifically the rung-90
Brainpan Sigil example, which is the only place in the docs that shows what an "in band" fixed atom
looks like at the top of the ladder). For every one of the five theme-families that carry a unique
at more than one rung — `charnel-bloom` (70/90), `hollow-orchard` (30/90), `sunwoven-almanac`
(50/90), `umbral-swarm` (50/90), `verdant-graft` (50/90) — I compared the two files atom-by-atom,
name-by-name, and flavor-by-flavor. I also compared the three files sharing rung 70
(`charnel-bloom-70`, `earthen-bastion-70`, `gilded-porcelain-70`) and all five files sharing rung 50
against each other, since a rarity badge that means different things depending on theme is the same
defect as a rarity badge that doesn't escalate at all.

**Verdict:** mixed, leaning toward a real problem. Two of the five paired families
(`sunwoven-almanac`, `umbral-swarm`) escalate cleanly and are worth using as the reference pattern.
Two others (`charnel-bloom`, `verdant-graft`) do not escalate at all from their lower band to their
own top band, and `verdant-graft` actually goes backwards. `hollow-orchard` is half-and-half. There
is also a same-rung (not just same-family) inconsistency at rung 70 that undercuts the badge itself.
No BLOCKERs — nothing is unplayable — but three of these would be noticed by a player who collects
across themes, which is the MAJOR bar.

## Findings

### MAJOR — `verdant-graft-90` reads flatter than its own `verdant-graft-50`, on every axis I can check

`verdant-graft-90` (rarity `almanac`/`sunwoven`, ordinals 100/90 — the top two rungs of the ten-rung
ladder) is the single cleanest inversion in the corpus. All eight entries
(`unique.verdant-graft-90-001` through `-008`) use the identical "low + medium" fixed-atom shape:
`might:low/bonding:medium`, `fortitude:low/cleansing:medium`, `vitality:low/sunbloom:medium`,
`might:low/searing-strike:medium`, `fortitude:low/regeneration:medium`, `might:low/retribution:medium`,
`vitality:low/gardener:medium`, `fortitude:low/midas:medium`. None reaches `high`.

Its own `verdant-graft-50` (rarity `fused`/`chimeric`, ordinals 50/60 — two rungs lower) has
`unique.verdant-graft-50-004` "Stitched Proboscis" at `lifesteal:high; vitality:low` — a fixed atom
one full tier above anything in the entire 90-band file above it.

The same direction shows up on the two other levers a player can read:

- **Counter-pressure kind.** `verdant-graft-50` mixes `narrow` (3), `conditional` (3, e.g.
  `unique.verdant-graft-50-004`'s "only feeds harder below half health"), and `drawback` (2).
  `verdant-graft-90` is `narrow` on all 8/8 entries — the least differentiated kind, at the rung that
  should carry the most.
- **Flavor prose.** Every `verdant-graft-50` entry runs two sentences of specific, vivid text
  ("A censer sewn from a feeding tube that never learned to stop. It drinks harder the hungrier its
  host becomes." — `unique.verdant-graft-50-004`). Every `verdant-graft-90` entry is a single short
  sentence ("Two barbs grown from one wound, neither willing to let the graft heal shut." —
  `unique.verdant-graft-90-001`). 8/8 vs 8/8, no exceptions in either file.

A player who found "Stitched Proboscis" at rung 50 and later drops any `verdant-graft-90` item would
be picking up something that reads smaller in stat shape, in counter-pressure, and in prose, despite
the badge saying it is worth two more rungs. Contrast with `sunwoven-almanac` and `umbral-swarm`
below — the escalation is achievable in this same schema; this family just didn't do it.

**Fix:** put at least the signature atom on 3–4 of the eight `verdant-graft-90` entries at `high` (the
family notes confirm `bonding`, `searing-strike`, `retribution`, `midas`, `sunbloom`, `gardener` all
have bands above `medium` available elsewhere in the corpus), and vary the counter-pressure kind the
way `-50` already does.

### MAJOR — `charnel-bloom-90` uses the exact same fixed-atom shape as `charnel-bloom-70`

`charnel-bloom-70` (rungs `heirloom`/`firstseed`, ordinals 70/80): all 8 entries are
`{medium signature, low support}` — e.g. `unique.charnel-bloom-70-001` "Carrion Spitter"
(`searing-strike:medium; ferocity:low`), `-006` "Blightfist" (`lifesteal:medium; might:low`).

`charnel-bloom-90` (rungs `sunwoven`/`almanac`, ordinals 90/100 — the top two rungs in the whole
ladder): all 8 entries are the same `{low, medium}` shape in the other order — e.g.
`unique.charnel-bloom-90-002` "Blightvest" (`almanac`, the single highest rung the game has:
`vitality:low; deathblast:medium`). Zero of the 16 combined entries across both files reaches `high`.

`docs/architecture/item/ssot-uniques.md` §7.3's own worked example ("Brainpan Sigil", rung 90
`sunwoven`) puts its shared identity atom at `atom.vitality.t4` — tier 4, i.e. `high` on
`bands.v1.json`'s tierMap — as the "in band" choice for that rung. `charnel-bloom-90` never reaches
that floor on either fixed atom in any of its 8 entries.

Naming compounds it: `unique.charnel-bloom-90-001` is "Popper of the Wake" — a lighter, almost
gentle name — while the rung directly below carries "Carrion Spitter" and "Blightfist". Flavor
follows the same register drop: "It only blooms at a wake, and it never leaves the ground
empty-handed" (90-001) reads no heavier than "Feeds where it fires. Every seed it launches was grown
on something that stopped moving first" (70-001) one rung down. A player reading name and flavor
blind to the color swatch could easily rank these backwards.

**Fix:** same as `verdant-graft` — push the signature atom to `high` on at least half the `-90`
roster, and check the naming pool for this theme skews toward weightier words at the top band.

### MAJOR — the rung-70 badge means different things depending on theme

Rung 70 (`heirloom`/`firstseed`) is carried by three files: `charnel-bloom-70`, `earthen-bastion-70`,
`gilded-porcelain-70`. I checked every "high" fixed atom against that family's own documented anchor
band in `affix-families/*.json` (so a family that is genuinely floor-locked at `high`, like
`atom.savagery`/`atom.bulwark`, doesn't get counted as a deliberate escalation — I checked, and none
of the three files below lean on a floor-locked family for this count).

- `charnel-bloom-70`: 0 of 8 entries reach `high` on either fixed atom.
- `earthen-bastion-70`: 2 of 8 (`unique.earthen-bastion-70-004` "Buried Caltrop", `-005` "Corymb of
  the Subsoil").
- `gilded-porcelain-70`: **8 of 8**, every single entry — `hit-mend:high` (anchor `low`, +2 tiers),
  `entangling:high` (anchor `low`, +2), `cleansing:high` (anchor `low`, +2), `midas:high` (anchor
  `medium`, +1), `might:high` (anchor `medium`, +1), `mending:high` (anchor `low`, +2),
  `mesmerizing:high` (anchor `low`, +2), `flourishing:high` (anchor `medium`, +1).

Same rung, same colour token, same pip count — and a `gilded-porcelain` heirloom is authored
one-to-two tiers hotter on its signature stat than a `charnel-bloom` heirloom, every single time. A
player using rarity color as a power heuristic (which the whole ladder exists to support) gets a
different real answer depending on which theme dropped.

**Fix:** either walk `gilded-porcelain-70` back toward `medium` on 4–5 of its 8 entries, or bring
`charnel-bloom-70` up to match — but pick one, since right now the two are two different games at the
same rarity.

### MINOR — `hollow-orchard-90` escalates on counter-pressure but not consistently on stat shape

Half of `hollow-orchard-90` (`unique.hollow-orchard-90-002` "Trunk of the Vigil",
`-003` "Wardenbough", `-007` "Cloistered Bough", `-008` "Candlebranch") keeps the exact
`{medium, low}` shape that every `hollow-orchard-30` entry uses (8/8 at band 30, all `medium+low`).
The other half of `-90` (`-001`, `-004`, `-005`, `-006`) does reach `high`.

This one gets partial credit the two MAJORs above don't: counter-pressure kind genuinely
diversifies only at the top band — `hollow-orchard-30` is `narrow` on 8/8, `hollow-orchard-90` mixes
in `conditional` (`-002` "front-row only", `-006`) and `drawback` (`-004` "loses the tempo atom
`quickening` would give back", `-007`). That's a real signal a careful reader gets, even where the
raw stat ceiling doesn't move — and the theme's own `avoid` list explicitly bans "loud, showy
radiance" for this theme, so a muted flavor register here may be deliberate rather than an oversight
(unlike `charnel-bloom`, which has no such brief). Flagged MINOR rather than MAJOR because the
signal exists, just inconsistently (4 of 8 ids) and only on one of the two axes.

### MINOR — a reused base noun gets less embellishment at the top rung, not more

`unique.hollow-orchard-90-004` "Lampfruit" (`almanac`, ordinal 100 — the single highest rarity rung
in the game) is the bare noun. `unique.hollow-orchard-30-001` "Tended Lampfruit" (`grafted`, ordinal
30 — the lowest rung eligible to carry a unique at all) carries the same noun plus a modifier. The
almanac version is textually less dressed than the grafted version of the same object. Worth a naming
pass; not worth a re-run on its own.

### NOTE — `rusted-legion-50` sits at the soft end of its own rung, but the theme brief may want that

Half of `rusted-legion-50` (`unique.rusted-legion-50-001` "Oxidewatch", `-002` "Surplusward", `-005`
"Dogtag of Duty", `-007` "Coldration") pairs two fixed atoms that are each already at their own
documented anchor band with nothing pushed up — no entry in this file reaches `medium` on both
atoms, let alone `high`, and 4 of 8 pair two atoms that are each already at their family's floor. The
other three `rung-50` files (`sunwoven-almanac-50`, `umbral-swarm-50`, `windswept-spore-50` — 24
entries combined) all reach at least one `medium`-anchored family on every single entry.

I'm not calling this a defect: `themes.v1.json`'s own identity for `rusted-legion` says "cohesion
comes from rank structure and drill, never from any one piece of kit actually being good" and lists
"cutting-edge or high-tech equipment" as something to avoid — a humble, unremarkable fixed core is
arguably the theme working exactly as designed. But the item still carries the `fused`/`chimeric`
colour token, and a player using that badge as a power signal gets a quieter answer than the other
three rung-50 themes give. Naming it for the owner to decide whether that tension is acceptable.

### NOTE — `sunwoven-almanac` and `umbral-swarm` show the escalation works when it's authored

Both families do this cleanly and are the reference pattern for the fixes above.

- `sunwoven-almanac-50` → `-90`: every one of the 8 `-50` entries is `{low, medium}`; every one of
  the 8 `-90` entries is `{high, extreme}` — e.g. `unique.sunwoven-almanac-90-001` "Sunlit Maw"
  (`searing-strike:extreme; might:high`). `extreme` does not appear anywhere else in the 144-entry
  unique corpus — this is the only file that reaches the top of the five-tier band scale, and it does
  so on all 8 entries, not a lucky one or two.
- `umbral-swarm-50` → `-90`: every `-50` entry's signature atom is `medium`; every `-90` entry's
  signature atom is `high` (`unique.umbral-swarm-90-001` "Faceless Chainsaw" `summoner:high`,
  through `-008` "Nameless Bucket" `cruelty:high`), checked against each family's own anchor band —
  none of these eight are floor-locked families, so the jump is a deliberate choice, not an artifact.
  Note that this family keeps its naming deliberately plain even at the top rung ("Nameless Bucket",
  "Massed Hamper") — that's on-brief for `umbral-swarm`'s "faceless mass, no single hero" identity,
  not a naming defect; the weight comes through the mechanical jump and the flatter, more dehumanized
  word choices instead of grandiosity.

## What I could not check

- **Actual in-game magnitudes.** Seed content authors `powerBand` labels (`low`/`medium`/`high`/
  `extreme`), not numbers; I compared labels and the qualitative family "anchor band" language in
  each family's own `notes` field, not the generated tier curve itself, since that curve is
  downstream of this seed layer and not present in these files.
- **The 8 single-rung themes' vertical legibility** (`ember-harvest`, `frostbitten-vanguard`,
  `rot-bloom`, `thorned-chassis` at 30; `rusted-legion`, `windswept-spore` at 50; `earthen-bastion`,
  `gilded-porcelain` at 70) — there is no lower/higher sibling file for these to compare against
  within their own theme, so the ladder-legibility question ("does band 90 read bigger than band 30
  in the same idea") doesn't apply to them directly. I used them only as same-rung cross-checks
  (rung 70 and rung 50 above).
- **Sets** (`sets/*.json`) and **charms/gems/consumables** — out of scope for this pass, which was
  scoped to `uniques/*.json` against the rarity ladder specifically.
- **Whether `flourishing`/`sunbloom`/etc. have a documented ceiling above their listed anchor.** I
  relied on cross-corpus attestation (a family appearing at a higher band somewhere else in the 144
  entries) rather than an explicit per-family max, since no registry file states one; this is
  probably right but is inference, not a read rule.
