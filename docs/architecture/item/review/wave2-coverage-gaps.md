# Wave 2 coverage-gaps review

**Scope read:** all 132 seed files under `data/seed/items/` — full census of the 144 uniques (18
files), 30 sets (5 files), 740 base types (43 files), 70 charms (4 files), 40 gems (2 files), 60
consumables (3 files), 40 drop-table entries (4 files), 21 materials, 30 recipes, 25 socket-words,
plus the frozen `core.v1.json` / `themes.v1.json` registries and the two content-relevant validator
checks (`UniqueRuleCheck.cs`, `SetRuleCheck.cs`). Every count below comes from a script pass over the
actual JSON, cross-checked against the validator source so nothing already enforced is re-reported.

**Verdict:** the corpus is structurally sound (base types are a clean 12/role/frame/band grid, drop
tables touch all 15 roles, materials are complete) but the two hand-themed classes — uniques and, to
a lesser extent, charms — carry real, quantifiable content-variety gaps that a player would notice:
one frame gets roughly half the signature loot of the other in four specific roles, the top rarity
band is elementally a coin flip between two of six elements, and one counter-pressure flavor
swallowed 89% of all uniques. Two suspected gaps (`ward-array`/`jewel-minor-b` absent from every set,
uniques capped at 8 of 15 roles) turned out to be exactly what `SetRuleCheck.cs`'s companion doc note
and `UniqueRuleCheck.AllowedRoles` already mandate — dropped after checking the code, not reported.

---

## Findings, most severe first

### MAJOR — Humanoid uniques are half as common as plant uniques in 4 of the 8 unique-eligible roles

`UniqueRuleCheck.AllowedRoles` fixes the eight roles a unique may occupy
(`armament-primary, core-guard, ward-array, armament-secondary, jewel-major, manipulator, mantle,
head-guard`), applied identically to both frames — that part is correct and enforced. Nothing checks
whether the two frames get *comparable numbers of items* within those eight roles, and they don't:

| Role | Humanoid slot | Plant slot | Humanoid count | Plant count | Ratio |
|---|---|---|---|---|---|
| `armament-primary` | main-hand | muzzle | 5 | 13 | 1 : 2.6 |
| `jewel-major` | neck | pollen | 5 | 13 | 1 : 2.6 |
| `mantle` | back | canopy | 5 | 13 | 1 : 2.6 |
| `ward-array` | shoulders | sheath | 6 | 12 | 1 : 2.0 |
| `core-guard` | torso | stem | 9 | 9 | 1 : 1.0 |
| `armament-secondary` | off-hand | thorn | 9 | 9 | 1 : 1.0 |
| `head-guard` | head | crown | 10 | 8 | 1.25 : 1 |
| `manipulator` | hands | leaves | 10 | 8 | 1.25 : 1 |
| **Total** | | | **59** | **85** | **1 : 1.44** |

A humanoid demon has 59 unique options across the whole roster against a plant demon's 85 — a 44%
gap — and in the weapon slot specifically (`armament-primary`), the plant frame has 2.6× the unique
choices a humanoid has.

**Root cause, found by reading across the 18 files (not visible from any one of them):** 13 of the 18
theme-batches author all 8 of their entries in a single frame rather than splitting frame within the
batch:

```
Pure plant (8 batches, 64 items):    earthen-bastion-70, ember-harvest-30, hollow-orchard-30,
                                      hollow-orchard-90, sunwoven-almanac-50, sunwoven-almanac-90,
                                      verdant-graft-50, windswept-spore-50
Pure humanoid (5 batches, 40 items): frostbitten-vanguard-30, gilded-porcelain-70, rusted-legion-50,
                                      umbral-swarm-50, umbral-swarm-90
Split 4h/4p or 3h/5p (5 batches):    charnel-bloom-70, charnel-bloom-90, rot-bloom-30,
                                      thorned-chassis-30, verdant-graft-90
```

Each individual batch is internally valid — one item per allowed role, no collisions, quota
respected. The skew only exists in aggregate across the corpus, which is exactly the class of defect
the brief predicts: no single-file author could see it. Since a pure-frame batch necessarily fills
all 8 roles with one frame, and 8 batches went plant against 5 humanoid, the imbalance concentrates in
whichever roles those particular batches happened to cover.

**Fix:** rebalance by adding humanoid entries at `armament-primary`, `jewel-major`, `mantle`, and
`ward-array` — 4 batches worth (roughly `frostbitten-vanguard-30`, `gilded-porcelain-70`,
`rusted-legion-50`, `umbral-swarm-50`/`-90` each contributing one humanoid alternative in those roles)
would close most of the gap without touching the axis-collision or role-quota rules.

### MAJOR — The top rarity band has zero fire/ice/air/earth uniques; it is entirely dark or light

Every uniques file batches to one of the 13 themes in `themes.v1.json`, and each theme carries at
most one `elementAffinity`. Reading the affinity assignment against which rung bands each theme's
files actually cover:

| Element | Themes assigned | Rung bands covered | Unique count |
|---|---|---|---|
| `dark` | umbral-swarm, charnel-bloom | 50, 70, 90 (×2 themes) | 32 |
| `light` | hollow-orchard, sunwoven-almanac | 30, 50, 90 (×2 themes) | 32 |
| `earth` | rot-bloom, earthen-bastion | 30, 70 | 16 |
| `fire` | ember-harvest | **30 only** | 8 |
| `ice` | frostbitten-vanguard | **30 only** | 8 |
| `air` | windswept-spore | **50 only** | 8 |
| *(no affinity)* | thorned-chassis, rusted-legion, verdant-graft, gilded-porcelain | 30, 50, 70, 90 | 40 |

At the rung-90/100 band (`sunwoven`/`almanac` — the pity-guarded and top-rung tiers, ssot-uniques.md
§4.1) the only theme-batches present are `charnel-bloom-90` (dark), `umbral-swarm-90` (dark),
`hollow-orchard-90` (light), `sunwoven-almanac-90` (light). **Every single almanac/sunwoven unique in
the corpus (32 of 32) is dark- or light-flavored.** A player chasing a top-tier fire, ice, air, or
earth signature item has literally none to chase — `ember-harvest` (fire) and `frostbitten-vanguard`
(ice) never advance past `grafted`/`cultivated` (rung 30), and `windswept-spore` (air) never advances
past `fused`/`chimeric` (rung 50).

Confirmed at the item level (element read from `fixedAtoms[].params.element` and the variance slot):
of 48 elemental atom mentions across all 144 uniques, `dark` accounts for 24 (50%), `light` 10 (21%),
`earth` and `air` 4 each (8% each), `ice` 3 (6%), and **`fire` exactly 1** (2%) —
`unique.ember-harvest-30-005`, the only fire-tagged atom in the entire unique corpus.

Nothing in `ssot-uniques.md` or the validator requires elemental spread across rung bands, so this is
not a documented boundary — it is an emergent gap from independent theme-to-band assignment that
nobody compared afterward.

**Fix:** either give `ember-harvest`, `frostbitten-vanguard`, and `windswept-spore` a second batch at
a higher band (mirroring how `charnel-bloom`/`umbral-swarm`/`hollow-orchard`/`sunwoven-almanac` each
got 2), or reassign one of the four elementless themes (`thorned-chassis`, `rusted-legion`,
`verdant-graft`, `gilded-porcelain`) to fire/ice/air at the 70–100 band.

### MAJOR — 89% of uniques declare the identical counter-pressure flavor

`core.v1.json`'s `counterPressure.kinds` closes three vocabularies — `narrow`, `drawback`,
`conditional` — specifically so an author never has to invent a magnitude to satisfy any of them (the
registry's own note: *"these closed vocabularies make all three authorable numberlessly"*). Despite
that, 15 of the 18 theme-batches (128 of 144 items, 89%) use `narrow` for **every single one** of
their 8 entries:

```
Batches using ONLY "narrow" (15 of 18): charnel-bloom-70, charnel-bloom-90, earthen-bastion-70,
  ember-harvest-30, frostbitten-vanguard-30, gilded-porcelain-70, hollow-orchard-30, rot-bloom-30,
  rusted-legion-50, sunwoven-almanac-50, sunwoven-almanac-90, thorned-chassis-30, umbral-swarm-50,
  umbral-swarm-90, verdant-graft-90

Batches that varied it: verdant-graft-50 (3 narrow / 2 drawback / 3 conditional),
  windswept-spore-50 (4 narrow / 2 drawback / 2 conditional),
  hollow-orchard-90 (4 narrow / 2 conditional / 2 drawback)
```

Corpus-wide: `narrow` = 131, `conditional` = 7, `drawback` = 6. The mechanism exists precisely to give
each unique a different *reason* it isn't strictly better than a rare — "it's a worse stat stick" vs
"it costs you something" vs "it's only good in one situation" — and in practice a player reading
tooltips across the roster will see the first reason on 9 items out of 10 and the other two on
essentially one theme's worth of loot each. That is a flattening of the exact axis the mechanism was
designed to create variety on, and — like the frame skew above — it is invisible from inside any
single 8-item batch, since one batch declaring `narrow` eight times is individually valid.

**Fix:** no validator change needed (the registry already supports all three); a targeted revision
pass converting 2–3 items per remaining batch to `drawback`/`conditional` where the fixed atoms
already support it (several `narrow` items already carry a predicate-shaped rider that could be
reclassified as `conditional` with no new content) would restore the intended spread.

### MINOR — The on-hit elemental proc gem exists for 1 of 7 element variants; the raw elemental-power gem exists for all 7

`data/seed/items/gems/g1.json` authors `atom.elemental-power` at all seven variants (`fire, ice, air,
earth, light, dark, omni` — `gem.g1-001` through `-007`). `atom.searing-strike` — the on-hit damage
proc family, also a 7-variant family per `variants.generate: "elements+omni"` in
`g-on-hit.json` — gets exactly one gem: `gem.g1-014`, fire only, `powerBand: high`. A player who wants
to socket an on-hit ice, earth, air, light, dark, or omni proc into gear has no gem for it; they would
have to hope one rolls as a rare affix instead, which defeats the point of a deterministic insert.

This is not caught by `ReferenceCheck.cs` (it only validates that an authored `element` value resolves
against the roster, not that a family's full variant set got gem coverage), and nothing in
`ssot-sockets.md`/`entry-shapes.md` §1 requires per-family variant completeness for gems — so it reads
as an oversight rather than a decision.

**Fix:** author 5 more `atom.searing-strike` gems (ice/earth/air/light/dark) at the same `high` band,
plus optionally `omni`, to match the elemental-power precedent.

### MINOR — Economy is the only charm axis with double the entry count

70 charms split cleanly by source file: `econ.json` is 20 entries, all `axis: economy`; `off-ctrl.json`
splits 20 between `control`/`offense` (10/10); `surv-util.json` splits 20 between
`survivability`/`utility` (10/10); `resonance.json` adds 2 of each axis across 10 entries. Net per
axis: **economy 22**, everything else **12**. `ssot-charms.md` places no authoring-count requirement
per axis (the axis cap of 3 is a per-loadout limit, not an authoring quota), so this isn't a violated
rule — but it does mean a player building around offense, control, survivability, or utility charms
has 45% fewer options than one building around economy, for no stated reason beyond the file split
happening to give economy its own dedicated file while the other four axes shared theirs pairwise.

Real player impact today is nil: `core.v1.json`'s own `authorNowNote` on the `charm` category states
*"no runtime today both binds a charm correctly and executes it"* — charms are content-complete and
consumer-pending. Flagging for whenever that consumer ships, not urgent before then.

### NOTE — Plant-restricted socket-words outnumber humanoid-restricted ones 6:1

Of 25 socket-words, 17 carry no `hostFrame` restriction (usable by either frame) and 8 do; of those 8,
6 are `plant`-only and 1 is `humanoid`-only (the eighth has a `hostRole` restriction but no
`hostFrame`). Sample size is small enough (8 restricted words total) that this could just be where the
dice landed rather than a real thematic gap, but it's the same direction as the unique-frame finding
above, so it's worth a second look if that one gets addressed.

---

## What I could not check

- **Whether the frame/element/counter-pressure imbalances above are visible in the game client's item
  filter UI** — that is presentation, not seed content, and outside this corpus.
- **Power-level parity behind the imbalances** — e.g., whether the 59 humanoid uniques are individually
  stronger to compensate for being fewer. `budget_ae`/power-vector fields aren't present in this seed
  shape (SC9: power model doesn't exist yet), so there's no number to check this against.
- **`ward-array`/`jewel-minor-b` absence from all 30 sets** — verified this is *deliberate*
  (`hybridEligible: false` on both roles per `core.v1.json`, and the `set.exemplar.json` notes confirm
  naming either role fails `SetRoleNotUniversal` at load) even though I could not find that check
  actually implemented in `SetRuleCheck.cs` — the rule is honored by every author but not enforced by
  tooling. That's a validator gap, not a content gap, and outside this review's lane; noting it here
  only so it isn't rediscovered as a false content finding by a later reviewer.
- **Drop-table acquisition paths for uniques** — `ssot-uniques.md` §4.5 requires ordinal-≥90 uniques to
  be `source-locked` or `deterministic`, never plain drop, but no `acquisition` field exists anywhere
  in the unique seed shape (confirmed against `entry-shapes.md`, which has no unique/set sections at
  all), and drop-tables reference generic `(role, frame)` equipment slots, never a specific unique
  container id. This looks like a deferred pipeline stage rather than a content gap, so I did not
  score it, but it means the ordinal-≥90 reachability rule is currently unverifiable from seed content
  alone.
