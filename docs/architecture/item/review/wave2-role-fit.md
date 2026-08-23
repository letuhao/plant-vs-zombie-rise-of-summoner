# Wave 2 review — role fit

**Scope read:** all 144 unique entries (18 `uniques/*.json` files), all 715 base-type entries with
a resolvable implicit family (62 `base-types/**/*.json` files, out of 740 total — 25 use one of 7
implicit-only families not carried in `affix-families/`, excluded from the automated pass and not
separately hand-checked), all 98 affix-family entries (15 `affix-families/*.json` files, read in
full for `kindId`/`params`/`roles`/`frames`), and all 5 `sets/*.json` files (one, `verdant-graft`,
read entry-by-entry; the other four cross-checked by family usage across all 30 threshold entries).
This is a full pass, not a sample — every unique's fixed atoms and variance slot were classified by
mechanic and checked against both its declared `powerAxis` and its base type's `frame`, then every
flagged case was read in full JSON (including `notes`/`counterPressure`) before being kept or
dropped.

**Verdict:** mostly sound, with two real, well-evidenced fault lines. First, 4 of 144 uniques carry
at least one atom whose own `frames` field excludes the item's own frame — a hard machine rule the
spec itself calls out and one sibling entry demonstrates dodging — so that line is dead on every
copy that drops, silently, and nothing in the validator catches it. Second, 8 of 144 uniques declare
a power axis their fixed core does not actually serve, concentrated three-of-eight in a single file
(`sunwoven-almanac-90.json`). Base-type implicits (715 checked) and set thresholds (5 files) are
clean. No BLOCKERs.

## Findings

### MAJOR — four uniques carry a fixed or variance atom that cannot function on their own frame

Every affix family declares a `frames` list (98/98 do; none are ambiguous). Cross-checking each
unique's `frame` against every fixed atom and its variance slot's family turns up four violations,
all silent at runtime (`ParamNotHonoured`/`RuntimeUnsupported` — the field the atom writes does not
exist on that frame's Unity actor):

- **`unique.gilded-porcelain-70-008`** ("Glazeface", `frame: humanoid`) — **both** fixed atoms are
  plant-only: `atom.flourishing` (high band, `frames: ["plant"]`) and `atom.quickening` (medium
  band, `frames: ["plant"]`). Only the variance slot (`atom.swiftness`, correctly humanoid-only)
  can ever function. The item's own `counterPressure.note` brags "every fixed and rolled slot
  shortens an interval — production, attack pace, movement" — but two of those three intervals do
  not exist for a humanoid actor. This is the worst case in the set: the entire authored identity
  (both core atoms) is inert on every copy that drops, leaving only the single rolled line.
- **`unique.rot-bloom-30-003`** ("Shroud of the Turning", `frame: plant`) — its higher-band fixed
  atom, `atom.plating` (medium), writes `arm1Max`, declared `frames: ["humanoid","hybrid"]` and
  `side: zombie` in `g-armour.json` — a field that does not exist on a plant-frame actor. The item's
  own `notes` field checks *role* legality for ward-array and calls it clear, but never checks
  *frame* legality, which is the actual hazard here. Tellingly, a sibling entry in the same wave —
  `unique.sunwoven-almanac-90-003` — explicitly avoids `atom.plating`/`atom.carapace` for exactly
  this reason ("both write arm1Max/arm2Max, Unity fields that exist only on humanoid/hybrid
  frames... the exact trap the unique exemplar's own notes call out"). One author caught the trap;
  this one didn't.
- **`unique.sunwoven-almanac-50-006`** ("Longlineage", `frame: plant`) — its variance slot,
  `atom.swiftness` (`frames: ["humanoid"]`, writes `zombieSpeed`), can never bind on a plant-frame
  item. Since `pool_rolls = 1` on a unique, this is the item's *only* rollable line — every copy
  that drops rolls a line that cannot function.
- **`unique.thorned-chassis-30-006`** ("Cobbled Palm", `frame: humanoid`) — its low-band fixed atom,
  `atom.tempo-yield` (`frames: ["plant"]`, writes `produceInterval`), cannot function on a humanoid
  item. The item's own note describes it as making "the plants behind it producing faster" — a
  humanoid-worn glove cannot carry a field that only exists on the plant side.

**Why the validator doesn't catch this:** `tools/ItemSeedValidator/Checks/` resolves `frame` as a
registry id (a real, known frame string) and enforces the unique 8-of-15-roles-per-frame quota
(`UniqueRuleCheck.cs`), but nothing cross-checks a fixed/variance atom's own `frames` list against
the container's frame — there is no `fixedAtoms` reference anywhere in `Checks/`. This is not the
role-legality bypass the design intentionally allows (rung 2 of `ssot-uniques.md` §3.5, which the
corpus exercises correctly 99+ times elsewhere) — frame legality is a *machine* rule the same
document says a unique "may break every rule that lives in the generator, and no rule that lives in
the machine" (§3.5). These four entries cross that line by author oversight, not by design.

**Fix:** re-author the four flagged lines against a frame-legal family (plenty exist per the
`side: both` families used correctly in the other 140 entries), or swap the base type's frame if the
atom is the one thing worth keeping.

### MAJOR — `sunwoven-almanac-90.json`: 3 of its 8 uniques declare an axis their content never touches

At rung 90 there is no low-band raw-stat filler; each entry carries two substantial atoms (`extreme`
+ `high`) and the axis's own defining mechanic is expected to appear in that fixed core, exactly as
it does in the file's other five entries and in every other rung-90 file. In three of eight it
doesn't:

- **`unique.sunwoven-almanac-90-003`** ("Copied Carapace", axis `control`) — fixed core is
  `atom.sust-callus` (extreme, shield-on-damage-taken) + `atom.resilience` (high, `defense`
  Increased); variance is `atom.warding` (defense). All three atoms are pure survivability. Nothing
  in the item applies a status, a snare, or any other form of control to anything.
- **`unique.sunwoven-almanac-90-004`** ("Thicket of the Litany", axis `utility`) — fixed core is
  `atom.warded` (extreme, shield-on-spawn, survivability) + `atom.quickening` (high, attack-speed,
  offense/tempo). The declared axis's own flavor exists only in the once-rolled variance slot
  (`atom.sust-freshgraft`, status.clear) — neither fixed atom is utility.
- **`unique.sunwoven-almanac-90-008`** ("Pagelight", axis `control`) — fixed core is `atom.stalwart`
  (extreme, status-resist) + `atom.warding` (high, defense). Again pure survivability; no CC, no
  debuff, nothing imposed on an opponent.

Compare the same file's other five (`-001` offense/searing-strike, `-002` survivability/
regeneration, `-005` economy/midas, `-006` offense/lifesteal, `-007` survivability/regeneration —
all clean), and every control/utility entry in the sibling rung-90 files, which do carry the right
mechanic at signature band: `charnel-bloom-90-004` (mesmerizing), `charnel-bloom-90-005`
(sust-freshgraft), `hollow-orchard-90-002` (entangling), `hollow-orchard-90-003`/`-008` (cleansing),
`hollow-orchard-90-007` (withering), `umbral-swarm-90-001` (summoner), `umbral-swarm-90-005`
(flash-freeze), `umbral-swarm-90-006` (gravemaking). The defect is specific to this one file, not a
corpus-wide pattern — 3 of `sunwoven-almanac-90`'s 8 entries, 0 of the other 32 rung-90 entries
across the other four files.

**Fix:** swap the fixed-core atom (not the variance slot) on the three flagged entries for a family
that actually carries their declared axis — e.g. an affliction-family atom at medium+ for the two
`control` entries, a status.clear or board-utility family for the `utility` entry — the way the
file's own `-001`/`-002`/`-005`/`-006`/`-007` already do.

### MINOR — five further uniques declare an axis their signature atom doesn't serve

Same shape as the finding above, single instances rather than a concentration, and each defensible
enough on its own that it reads as a labeling slip rather than a broken item:

- **`unique.charnel-bloom-70-005`** ("Pistil of Bonemeal", axis `economy`) — its higher-band fixed
  atom is `atom.gravemaking` (medium, `grid.spawn`, a battlefield board trick); the economy content
  is a low-band `atom.econ-graze` plus a `midas` variance roll. Compare `unique.umbral-swarm-90-006`,
  which uses the same family (`atom.gravemaking`, high band) and correctly declares `utility` — the
  norm for this family everywhere else it's the signature atom (5 other clean instances: `earthen-
  bastion-70-007`, `ember-harvest-30-008`, `hollow-orchard-90-008`, `verdant-graft-50-001`,
  `verdant-graft-90-007`, all `atom.gardener`, all correctly `utility`).
- **`unique.charnel-bloom-90-007`** ("Mourning Deathpetal", axis `offense`) — signature atom
  `atom.gardener` (medium, spawn-a-plant-on-death) is the same device `unique.umbral-swarm-90-001`
  uses (via `atom.summoner`) to justify a `utility` axis in the same rung band. Two uniques with the
  same underlying mechanic land on different axes; this one's `offense` label is carried only by a
  low-band `might` line and a `ferocity` variance roll.
- **`unique.verdant-graft-90-006`** ("Implanted Claw", axis `control`) — signature atom
  `atom.retribution` (medium) is a counter-attack damage proc (deal damage back when hit) — offense-
  flavored retaliation, not crowd control. Nothing in the item imposes a status or restricts the
  target.
- **`unique.verdant-graft-90-001`** ("Twinburr", axis `control`) — signature atom `atom.bonding`
  (medium) is documented elsewhere in the corpus (the `infusion`-role base type
  `item.plant-glands-b-011` "Curdled Latex" carries it as its implicit, tagged `utility`) as a
  graft-cohesion status, not a control effect applied to an opponent. The item's own `tags` array
  agrees (`"utility"`, not an offensive posture), which sits oddly against a `control` power axis.
- **`unique.windswept-spore-50-003`** ("Sheath of Breeze", axis `utility`) — fixed core is
  `atom.shield-capacity` (medium) + `atom.shield-regen` (low); variance is `atom.stoicism`
  (crit-resist). All three atoms are survivability. No utility-tagged mechanic (no board trick, no
  status.clear, no economy, no tempo) appears anywhere on the item.

**Fix:** either relabel the axis to match the content that's actually there (the cheaper option for
all five, since none of these are broken items — they're well-built survivability/offense pieces
wearing the wrong tag), or swap the signature atom for one that matches the declared axis.

## What's clean

**Base-type implicits (715 entries with a resolvable family, across all 62 files / 15 roles).** Zero
frame violations (checked every implicit's family against its base type's frame). Role/axis
alignment matches `ssot-equip-slots.md` §2.3's own stated purpose closely: six single-purpose roles
are 100% on-charter (`armament-primary` 48/48 offense, `core-guard`/`ward-array`/`head-guard`/
`footing` 48/48 survivability, `sense` 48/48 offense), `infusion` is dominated by control-family
status effects (23/48 resolve to `control` in the shared registry; the other 25 use one of the
theme-local status families excluded from this pass, whose names — `blighting`, `chilling`,
`rotting`, `sparking`, `marking` — read as the same status-effect family), and the roles the spec
itself calls dual-purpose (`armament-secondary`: offense 26/survivability 22; `mantle`:
survivability 31/utility 17; `retinue`: utility 32/offense 12/control 4; the jewel roles: a mix of
all three) split in exactly the shape their own role description calls for
("defensive or amplifying" for `armament-secondary`; "wards and status cleansing" for `mantle`). No
findings here.

**Set threshold capabilities (5 files, 30 threshold entries, one file — `verdant-graft` — read in
full).** Every capability atom used across all 30 thresholds is a `frames: [humanoid, plant]`
family; none of the four frame-locked families responsible for this review's MAJOR unique finding
(`plating`, `carapace`, `quickening`/`flourishing`/`tempo-yield`, `swiftness`) appear anywhere in a
set. Every set's member-role list correctly avoids the two roles a hybrid specimen cannot equip
(`ward-array`, `jewel-minor-b`) and claims at most one weapon role, matching the discipline
`ssot-sets.md` documents. No findings here.

## A structural note, not a defect

144 uniques sit on exactly 8 of the 15 roles (`armament-primary`, `armament-secondary`,
`core-guard`, `ward-array`, `manipulator`, `mantle`, `head-guard`, `jewel-major` — 18 each), and each
of those 8 carries all five power axes in roughly even split (4/4/4/3/3). None of the 144 uniques
use `infusion` — the role `ssot-equip-slots.md` §2.3 names as the actual home for status/control
content — or `girdle`, its named home for economy. That's a locked design choice
(`ssot-uniques.md` §3.5's rung-2 bypass: a unique's fixed core ignores the affix pool's role-legality
table entirely, by design, and does so correctly in 99+ other entries in this corpus), not something
this review is positioned to relitigate. But it is the mechanism behind both findings above: forcing
a `control` or `utility` axis onto a role that doesn't naturally carry that flavor (survivability-
heavy roles like `ward-array`/`head-guard`/`mantle`, or the tempo/offense-heavy `manipulator`) means
the author has to reach off-role for the axis's defining atom every time — and the two failures
above are exactly the times that reach came up short.

## What I could not check

- **Counter-pressure/AE budget arithmetic** (whether a flagged item's declared `budget_ae` and
  `counterPressure.kind` actually clear the numeric ceilings `ssot-uniques.md` §3.7/§6.2 describe)
  is a numeric-validation question, not a content-fit one, and belongs to the validator's own budget
  check rather than this lane.
- **The 25 base-type entries using one of the 7 implicit-only families not present in
  `affix-families/*.json`** (`atom.blighting`, `atom.bonding`, `atom.buttering`, `atom.chilling`,
  `atom.marking`, `atom.rotting`, `atom.sparking`) were excluded from the automated frame/role-axis
  cross-check because their `frames`/`roles` data lives outside the registry files this review read;
  `atom.bonding`'s own frame legality was not independently confirmed beyond the base-type implicit
  and set-threshold uses traced in the findings above.
- **Runtime confirmation of the four frame-illegal atoms.** The `ParamNotHonoured`/
  `RuntimeUnsupported` outcome is inferred from the affix family's own declared `frames` field and
  the design doc's explicit statement of the failure mode, not from running the importer or binder
  against these rows — this review is read-only and did not execute any code.
- **Four of the five set files beyond `verdant-graft`** were checked for family usage (all 30
  threshold entries) and member-role legality, but not read entry-by-entry for flavor/theme fit the
  way `verdant-graft` was — a lighter pass than the uniques got.
