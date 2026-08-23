# Wave 2 review — name sameness

**Scope read:** every named entry in the seven kinds this lane covers — 740 base types
(`base-types/**/*.json`), 144 uniques, 30 set pieces, 70 charms, 40 gems, 60 consumables, and 25
socket words: 1,109 entries across 61 files, pulled programmatically (id, name, tags, file) and
read as one corpus rather than per-file. For each candidate I cross-checked the driving registries
(`words.v1.json` pools, `themes.v1.json` identities/avoid-lists, `NamingCheck.cs`/`NameNormalizer.cs`)
to confirm the collision normalizer genuinely cannot see the case before writing it up.

**Verdict:** no BLOCKER. One clean MAJOR (a verbatim duplicate name the exempt-kind carve-out lets
through), one MAJOR-leaning cluster (a charm head-noun repeated on five rare, mutually-compared
items), two MINORs, and one NOTE. The corpus is not saturated with sameness — most of the 1,109
names are distinct ideas — but every finding below is a real case the string-level checks structurally
cannot catch, which is exactly this lane's job.

## Findings

### MAJOR — `gem.g1-015` and `consumable.k1-007` are both named "Mending Pulse", verbatim

Not "the same idea under different words" — the identical three-word string, on two mechanically
unrelated items: a permanent socketable gem (`atom.hit-mend`, low power band) and a spent-on-use
potion (`atom.mending`, medium power band, `manifestCost: 1`). `nameKey` differs
(`gem.mending-pulse` vs `consumable.restore-mending-pulse`), so the uniqueness check passes, and a
grep across the full 1,109-name corpus confirms this is the *only* exact string duplicate anywhere
— it isn't a symptom of a sloppy corpus, it's one specific gap.

The gap has a specific cause: `NamingCheck.CheckName` returns immediately for any
`IsPoolExemptKind` (`gem`, `display-template`, `curve`, `recipe`, `material`) —
`tools/ItemSeedValidator/Checks/NamingCheck.cs`, the `if (ctx.Registries.IsPoolExemptKind(...)) return;`
line — before it ever reaches the `seen.TryGetValue(normalized.Key, ...)` collision block later in
the same method. `gems` entry in `words.v1.json`'s `kindsExemptFromPools.exempt` explicitly states
`"stillApplies": "global name and nameKey collision checks, ..."` — the registry's own documented
intent is that gems keep colliding with everything else. The code doesn't do that: a gem is never
inserted into the collision dictionary and never checked against it, so a gem can duplicate any
name in the corpus — including a consumable's — with zero errors reported. This one instance is a
content fix (rename either name); the early-return is a validator gap the registry text already
disagrees with, worth a one-line fix (move the exempt-kind check past the collision block, keep it
before the grammar/pattern checks) so this class of gap closes for good rather than one name at a
time.

**Fix:** rename one of the two — the gem (`Mending Pulse` → e.g. `Restorative Pulse`) is the
lower-friction change since it has no theme tie, versus the consumable, which sits in a
tier-lettered `k1` list of thirty potions that already reads as a closed set.

### MAJOR — the "Signet" charms are a five-item cluster the corpus-wide normalizer cannot flag

`charm.econ-019` "Signet of the Coalbed", `charm.econ-020` "Signet of Annexation",
`charm.surv-util-018` "Signet of the Foundation", `charm.surv-util-019` "Signet of the Diaspora",
and `charm.surv-util-020` "Signet of the Almanac" — five of the seventy charms in the corpus, drawn
from two of its three independently-authored files (`econ.json`, `surv-util.json`), all landing on
the identical head noun.

This isn't a slot noun doing its job (the way `canopy`/`frond`/`yoke` recur across unique themes at
matching role positions — see the NOTE below, which is the benign version of this pattern). All
five of these share `"charmClass": "signet"` and `"uniqueCarry": true` — they're the game's
double-edged, carry-one-at-a-time charm archetype, which means a player evaluating them is
comparing exactly these five against each other by design. And the convergence wasn't required:
`words.v1.json`'s `charmPartition` note already flags this exact risk — *"With three agents
free-ranging over 13 themes, the corpus-wide normalizer is the only automatic protection"* — and
the third file proves the risk was avoidable. `off-ctrl.json` has two more `charmClass: "signet"`
entries, `charm.off-ctrl-010` "Permafrost Marrow" and `charm.off-ctrl-020` "Clockwork Graft",
and neither one reaches for "Signet" at all. Two of three authors made the same reasonable-looking
call in isolation; the one who didn't shows five-for-five wasn't inevitable.

Smaller echoes of the same mechanism, lower stakes because these are ordinary (non-`uniqueCarry`)
charms players compare less deliberately: **Root** (`charm.econ-015` "Root of the Foundation",
`charm.off-ctrl-016` "Root of the Rampart", `charm.surv-util-003` "Root of Bedrock" — one from each
of the three files) and **Husk** (`charm.econ-002` "Gilded Husk", `charm.off-ctrl-001` "Frostbitten
Husk", `charm.off-ctrl-006` "Husk of the Murmuration"). Together with Signet, four head nouns cover
14 of the corpus's 70 charms (20%).

**Fix:** rename at least three of the five Signet charms — `charm.surv-util-019` and
`charm.surv-util-020` are the cheapest cuts since `off-ctrl`'s alternates (Marrow, Graft) show the
class doesn't need the literal word. Root/Husk are lower priority; note them for the next charm
pass rather than re-running this wave.

### MINOR — `charnel-bloom` and `rot-bloom` each reach into the vocabulary the other's `avoid` list reserves

`themes.v1.json` writes the boundary between these two death-adjacent themes explicitly, in each
theme's own entry: `rot-bloom.avoid` lists *"reads the same as charnel-bloom's battlefield-specific
necrosis"*; `charnel-bloom.avoid` lists *"generic ecological rot with no battlefield or corpse
specificity (that is rot-bloom's broader register)"*. The two authors (these are different unique
files, so different sessions) had that instruction and still landed on shared ground twice:

- `unique.charnel-bloom-70-006` "Blightfist" and `unique.charnel-bloom-90-002` "Blightvest" use
  `blighted`/`Blight` — which *is* legitimately in charnel-bloom's own `themeAdjectivePools` entry,
  so no pool-access rule is broken, but "blight" is a generic-crop-disease word with no corpse or
  battlefield content, i.e. textbook rot-bloom register by the theme's own definition of what to
  avoid. The other 14 charnel-bloom-70/90 uniques all reach for specifically funerary/skeletal
  words instead (Carrion, Cerement, Ossuary, Sepulchral, Requiem, Cadaverous, Necropolis) — so
  these two are the outliers inside their own theme, not the norm.
- `unique.rot-bloom-30-003` "Shroud of the Turning" draws `Shroud` from the shared
  `nounPools['ward-array.plant']` (any plant unique may use it — no rule broken), but "shroud"
  denotes burial cloth specifically, which is charnel-bloom's reserved battlefield/corpse register,
  not rot-bloom's generic-ecological one. Charnel-bloom's own `unique.charnel-bloom-70-002` "Bole
  of the Cerement" uses a near-synonym for the same image (a cerement *is* a burial shroud) — so
  the two themes' single closest-sounding pair of names sit on opposite sides of a boundary that
  was written down specifically to keep them apart.

Three items out of 174 uniques+sets, and every individual word is pool-legal — this is exactly the
shape of thing that can't be a validator rule (both pools are legitimately reachable) and can't be
caught by reading either file alone (the reserved-word list lives in the *other* theme's entry).

**Fix:** low-cost swaps, not a re-run — replace "Blight-" in the two charnel-bloom names with a
funerary word already established in that theme's own pool, and swap `unique.rot-bloom-30-003`'s
noun away from `Shroud` (any other `ward-array.plant` noun keeps the item's role-legibility intact).

### MINOR — six near-synonyms for "old and hardened" cover 37 plant base types across 10 files

`gnarled` (9 entries), `hoary` (8), `petrified` (7), `ancient` (6), `timeworn` (4), and `seasoned`
(3) — representative ids: `item.plant-sheath-a-010` "Gnarled Testa", `item.plant-sheath-b-008`
"Hoary Armature", `item.plant-stem-b-008` "Petrified Culm", `item.plant-canopy-b-004` "Ancient Bough" (lives in `plant-mantle-b.json` — see id note below),
`item.plant-stem-b-009` "Timeworn Bast", `item.plant-soil-b-012` "Seasoned
Tilth" — span 10 files (`footing/plant/b`, `girdle/plant/a`, `plant-bract-b`, `plant-head-guard-b`,
`plant-manipulator-b`, `plant-mantle-b`, `plant-soil-b`, `plant-stem-b`, `plant-ward-array-a`,
`plant-ward-array-b`).

This is not an authoring accident in the way the charm and theme clusters above are: all six words
are legitimate members of exactly two `words.v1.json` class-rung pools on one ladder —
`armour.plant.bark` (which also holds nine texture words like `corky`/`rough`/`cracked` that don't
read as "old") and `armour.plant.heartwood` (which also holds `dense`/`obdurate`/`stony`/`oaken`
that read as "hard" without "old"). The pools were built for surface variety *within* one rung, and
that part works — no two of the six words appear on the same item. What the pool structure can't
see is the view from outside it: a player gearing a plant unit through these two rungs meets
`Gnarled`, `Hoary`, `Petrified`, `Ancient`, `Timeworn`, and `Seasoned` — six distinct words, on 5%
of the whole base-type corpus, that all communicate the identical thing ("this is old, tough plant
tissue") with no distinguishable escalation between them. It reads as the corpus repeating one idea
six times rather than presenting six ideas.

*(id note: the file paths use internal role slugs that don't match their filenames one-for-one —
e.g. `item.plant-roots-b-002` "Ancient Holdfast" lives in `footing/plant/b.json`, and
`item.plant-canopy-b-004`/`item.plant-crown-b-009` live in `plant-mantle-b.json` /
`plant-head-guard-b.json` respectively — confirmed by direct lookup, not guessed from the id
string.)*

**Fix:** a polish-pass item, not urgent — if the bark/heartwood rungs are revisited, trim each pool
to 2–3 "age" words and let the remaining slots carry texture/hardness words that don't overlap in
meaning (several already do: `corky`, `stony`, `oaken`).

### NOTE — unique-item role nouns repeat across themes, but this is the pool system working as designed

`Yoke` (5), `Canopy` (5), `Frond` (5), `Bole` (4), and `Claw` (4) each recur across different
unique-set themes — e.g. `unique.charnel-bloom-70-007` "Sepulchral Canopy",
`unique.rot-bloom-30-007` "Overgrown Canopy", `unique.verdant-graft-50-007` "Crossbred Canopy" all
occupy item slot 007 of their respective 8-piece theme sets. `words.v1.json`'s `uniquePartition`
access rule explicitly grants "any nounPools entry" to every theme, and these nouns are role nouns
(the plant frame's core-guard/mantle-family words), not theme concept words — so this is the
noun-per-role convention doing exactly what it's for, distinguished correctly by each theme's own
adjective/concept choice (Sepulchral / Overgrown / Crossbred). Flagging for completeness since it's
the same surface shape as the real findings above (one head noun, many partitions), but it doesn't
rise to a finding: no two Canopy items sit at the same power band or drop table, and a player meets
each one inside its own theme's context rather than side-by-side.

## What I could not check

- **Flavor/prose sameness** is out of this lane — I read `name` fields only, per the brief.
  `wave2-theme-coherence.md` already covers flavor-text presence/voice.
- **Exhaustive manual reading of all 740 base-type names** wasn't done word-by-word; I ran targeted
  semantic sweeps (decay/death vocabulary, hardness/age vocabulary, cross-file head-noun frequency)
  rather than eyeballing every one of the 61 files line by line. A different semantic axis I didn't
  think to search for could still be hiding a cluster the sweeps above didn't surface.
- **Affix families, curves, recipes, materials, display-templates** are outside this lane's stated
  scope (base types, uniques, sets, charms, gems, consumables, socket words) and are also the
  registry's own documented pool-exempt kinds for a different reason (mechanic labels / numeric
  points / template sentences, not assembled item names) — not reviewed here.
- **Drop-table and recipe files** reference existing item ids rather than authoring names, so they
  carry nothing new for this lane.
