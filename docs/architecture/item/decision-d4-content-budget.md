# D4 — content budget and gear cadence

**Status:** R2 debate deliverable, decided 2026-08-22. Answers the two questions in
[reconciliation-plan.md](reconciliation-plan.md) §R2 row D4. Bound by
[enrichment-contract.md](enrichment-contract.md), which is not amended here.

**Method.** Every number below is totalled from a lane document and cited to its section. Where a lane
gave no number I say **UNSIZED** rather than inventing one. Where two lanes gave different numbers for
the same content I show both and call it a collision. Arithmetic is shown; nothing is asserted.

**What I rejected before starting.** The tempting answer to Q1 is "it's a lot, cut the scope." That is
not an answer — it is the question restated. The answer that survives is a per-lane row count with a
generated/hand-authored split, because the split is the only thing that turns the total into a
schedule. The tempting answer to Q2 is "items last a progression tier." That is one of **five**
cadences the committed numbers produce, and picking it without saying which lanes' numbers contradict
it would move the problem instead of solving it.

---

## 1. Q1 — the totals table

### 1.1 Hand-authored — a human types each one

| Lane | Artefact | Rows / cells | Source |
|---|---|--:|---|
| **I3** | Base-type identity definitions (class + role + implicit family + noun) | **43** | [ssot-item-categories.md](ssot-item-categories.md) §7.6 |
| I3 | Class ladders (4 armour rungs × 2 frames, weapons, jewels, off-hands) | **24** | §5.3 |
| I3 | Role guard/damage shares | **12** | §5.4 |
| I3 | `item_category` rows (10 declared, 4 authored) | **10** | §3 "the scale, up front" |
| **I8** | Family → affix-group mapping | **70** | [ssot-affixes.md](ssot-affixes.md) §4.1, §4.2 |
| I8 | Role × affix-group weight matrix | **195** (13 × 15) | §3.6, §4.3 |
| I8 | Tier-band `share`, one per family | **70** | §4.5, §5.4 |
| I8 | `item_affix_name` words (70 families × 3 bands) | **210** + plant overrides | §4.12, §5.3 |
| I8 | Rare two-word head/tail name tables | **UNSIZED** | §4.12 |
| **I1** | Rarity rungs (id, ordinal, count band, tier window, hex, pips, display key) | **10** | [ssot-rarity.md](ssot-rarity.md) §3.3 |
| I1 | `rarity_budget` rows (13 keys × 10 rungs, SC7-gated) | **≤130** | §4.4 |
| **I2** | `item_role` (15 + commander `standard`) | **16** | [ssot-equip-slots.md](ssot-equip-slots.md) §2.3, §5.2 |
| I2 | `item_role_frame` (16 × 3 frames) | **48** | §5.2 |
| I2 | `item_role_family` (role × frame × family × max_tier) | **~1,100** — lane gives no count; derived below | §5.2 |
| **I4** | Insert family definitions | **~8** | [ssot-sockets.md](ssot-sockets.md) §8.4 |
| I4 | Resonance shapes → combo containers + their atoms | **25** containers, ~50 atom picks | §4.4 |
| I4 | Words (recipe + ingredients + atoms), 12 proposed / ≤20 | **12–20** + ~64 ingredient rows | §4.4, §10.2 |
| I4 | `socket_max` per role; `socket_min/max` per rarity band | **15** + **20** | §4.1 |
| **I5** | Sets | **UNSIZED** — [ssot-sets.md](ssot-sets.md) never states a v1 count | §3.4, §9 |
| I5 | Per set: header + 4 members + 2–3 tiers + tier atoms | **~11 rows/set** | §4.2 |
| **I10** | Charms | **UNSIZED** — [ssot-charms.md](ssot-charms.md) never states a v1 count | §3.4 |
| I10 | `charm_resonance` (5 axes × 2 breakpoints) + their atoms | **10** + atoms | §6.2 |
| **I9** | Material ids (10 shipped, 11 new) | **21** | [ssot-materials-crafting.md](ssot-materials-crafting.md) §3.1 |
| I9 | Reference cost rows (9 operations) | **9** | §7.4 |
| I9 | Mutation recipes (7 verbs × band/grade) | **UNSIZED**, est. 20–50 | §6.1 |
| **I6** | Enhancement milestone picks (43 identities × 5 milestones) | **215** | [ssot-enhancement.md](ssot-enhancement.md) §5.4, §3.3 |
| I6 | `item_enhance_rules` (20 levels) | **20** | §7.6 |
| I6 | Reserved milestone atom families (3 × 5 tiers) | **15** | §5.5 |
| **I7** | Operations + base cost rows | **7** | [ssot-reroll.md](ssot-reroll.md) §3.1, §5.3 |
| **I11** | Attributes (code catalog, not data) | **5** | [ssot-requirements.md](ssot-requirements.md) §5.4, §6.1 |
| I11 | Species attribute base vectors (24 species × 5) | **120** | §6.4 |
| I11 | Per-species-per-attribute growth curves | **≤120** | §6.4 |
| **I12** | `loot_source` + `drop_table` + groups + entries | **UNSIZED**, est. 150–400 | [ssot-generation.md](ssot-generation.md) §5.1 |
| **I13** | Content | **0** — pure machinery | [ssot-inventory.md](ssot-inventory.md) §4 |
| **G1–G4** | Uniques, consumables, presentation, granted actions | **NOT YET WRITTEN** (R3) | reconciliation-plan §R3 |

**Hand-authored subtotal: ≈ 3,100 rows/cells**, of which **~1,100 is one table** (`item_role_family`)
and **~600 is unsized guesswork** (sets, charms, drop tables, mutation recipes). Excluding both, the
firmly-committed hand-authored floor is **≈ 1,400**.

**How `item_role_family` reaches ~1,100.** I8 §4.3's matrix gives each role 8–15 non-zero groups. Taking
`head-protective` — `g.life` 4 families + `g.armour` 4 + `g.ward` 6 + `g.precision` 3 + `g.evade` 1 +
`g.shield-stat` 4 + `g.on-death` 4 + `g.sustain` 3 = **29 families**. Fifteen roles at ~25 average, over
~2.5 effective frames after the frame/side filters, is **≈ 1,050–1,150 rows**, each carrying a
hand-set `max_tier`. Neither I2 nor I8 counted this table. It is the largest single hand-authoring
commitment in the entire round and it arrived by accident.

### 1.2 Generated — a rule emits each one

| Lane | Artefact | Rows | Source |
|---|---|--:|---|
| **I3** | Base-type containers (43 × 2 frames × 4 bands) | **344** | ssot-item-categories §7.6, §4.D |
| I3 | Fixed-core atom rows (2 per container: base stat + implicit) | **688** | §3 |
| I3 | Base-stat atom rows (10 guard classes × 4 bands + 6 weapon × 4) | **64** | §5.3 |
| **I8** | Affix atom rows — authored families × 5 tiers | **~355** | [atom-family-library.md](../effect-atom/atom-family-library.md) §6 |
| I8 | Affix atom rows — element expansion (12 families × 7 slots × 5 tiers) | **~420** | atom-family-library §2, §6 |
| I8 | `effect_container_pool` rows | **4,128** (I3) *or* **16,512** (I8) — see collision C2 | §7.6 vs ssot-affixes §4.8 |
| **I4** | Gem containers (6 elements × ~8 families × 5 tiers) | **≤240** | ssot-sockets §8.4 |
| I4 | Resonance containers | **25** | §4.4 |
| **I6** | `item_enhance_track` (5 milestones × 344 containers) | **1,720** | ssot-enhancement §5.4 |
| I6 | `item_enhance_cost` (rungs × 20 levels × ~2 lines) | **≤400** | §5.4, §7.5 |
| **I9** | Forge recipes generated from the base-type catalog | **344** + ~1,000 cost rows | ssot-materials-crafting §9.3 |
| **I11** | `container_requirement` frame rows | **≥344** | ssot-requirements §5.1 |
| **I12** | `item_generation`, `item_drop_log` | runtime, not content | ssot-generation §5.1 |

**Generated subtotal at wave 1: ≈ 11,100 rows** on I3's pool estimate, **≈ 22,500** on I8's.

**Post-E12 the pool term explodes.** The 12 element-expanded families are all `stat.derived`, quarantined
today (ssot-affixes §4.9). Lifting the quarantine multiplies eligible family-variants per role by roughly
2.5–3×, taking `effect_container_pool` to **the order of 40,000–90,000 rows**. No lane computed this.
It is one import, not one authoring pass — but nothing today can produce it.

### 1.3 The headline

| | Wave 1 | Post-E12 |
|---|--:|--:|
| **Hand-authored** | ≈ 3,100 (firm floor ≈ 1,400) | + ~48 name words, + 16 family shares |
| **Generated** | ≈ 11,100 – 22,500 | ≈ 45,000 – 95,000 |
| **Ratio** | **1 : 4** to **1 : 7** | **1 : 20** or worse |

**The distinction is the whole ballgame, and the round mostly got it right.** 22,000 generated rows is a
weekend of generator work. The problem is not the 22,000; it is the **3,100 hand-authored cells**, of
which ~1,500 sit in three tables (`item_role_family` ~1,100, `item_affix_name` 210, the role × group
matrix 195) — and two of those three do not have to be typed at all if they are derived from the third.

**And it is the ~600 UNSIZED rows that will actually hurt**, because "how many sets" and "how many
charms" are not row counts — they are *design decisions, each requiring a named fantasy, a capability
atom, and a balance pass*. A set is ~11 rows and roughly a day of design. Twenty sets is not 220 rows;
it is a month.

---

## 2. Critical path, and what can ship empty

### 2.1 On the critical path for a first playable

| Lane | Why it cannot be empty |
|---|---|
| **I2 — equip slots** | Nothing has anywhere to go. Also owns retiring `UniqueEquipmentCatalog.DefaultSlots = { weapon, armor, trinket }` (§5.7), which today is a hard three-slot allowlist that throws |
| **I3 — base types** | The container an item *is*. 344 rows or 172, but not 0 |
| **I1 — rarity** | `effect_container.rarity` has no FK today (§6.1), and the whole affix envelope is looked up by rung |
| **I8 — affixes** | Without a pool an item is a base stat and an implicit. That is not an item system |
| **I12 — generation** | No drop, no items. Also the only lane that enforces standalone-first in the table (§4.6) |
| **I13 — inventory** | The armoury. A drop with nowhere to land is a bug, not a feature |
| **The durable per-actor owner scope** | **No lane owns this.** ssot-sets §8.12 states it plainly: the seven scopes are `match` · `plant:N` · `zombie:N` · `entity:HEX` · `player:N` · `sector:N` · `slot:N`, and **none is durable per-specimen**; `entity:` is session-scoped. Every lane that binds equipment blocks on it. It is not in any lane's scope and it is not in R3 |

### 2.2 Can ship empty at v1

| Lane | Cost of shipping empty |
|---|---|
| **I4 — sockets** | Zero at v1's item levels. The lane's own §8.7: *"At tiers 1–3, with no combination in reach, sockets are a stat tax."* v1 lives entirely in that band (§6.1) |
| **I5 — sets** | Loses the only source of a *capability* atom a rare cannot roll (§3.2). Real, but survivable |
| **I10 — charms** | Zero. The lane says so itself: *"If one had to be cut, cut charms"* (§3.6), and it is blocked twice — `player:` scope resolves match-wide, and `charm` is not an accepted `ContainerKind` in shipped code (§4.3) |
| **I7 — reroll** | Zero at v1 churn rates. See §6.3 |
| **I6 — enhancement above +8** | Zero, and provably: `ilvl_cap = clamp(4 + ilvl/4, 4, 20)` (§7.3), and the highest shipped content level is 10 (`src/FusionRpg.Core/Battle/WaveCatalog.cs:32-35`, verified). `ilvl_cap(11) = +6`. **Bands 9–20 are unreachable in v1 content by I6's own formula** |
| **I9 — forge recipes** | Crafting-as-creation defers. Salvage must still ship — it is the only sink |
| **I11 — the five attributes** | Zero at v1; OD7 already marks them a proposal awaiting sign-off |

---

## 3. The recommended v1 cut, with the cost of each cut named

Ordered by rows saved per unit of design loss.

| # | Cut | Rows saved | The cost, named |
|---|---|--:|---|
| **1** | **Derive `item_role_family` from I8's 195-cell matrix + the three filters (§4.4) instead of hand-authoring it.** I2 keeps the schema; I8's generator emits the rows | **~1,100 hand-authored** | None. It removes a second source of truth rather than creating one. `max_tier` per (role, family) becomes a rule — `3` on `jewel-minor-*`, `5` elsewhere — instead of 1,100 typed integers. **The single largest saving in the round, and it costs nothing** |
| **2** | **Sockets: ship zero.** `base_type.socket_max = 0`, `rarity.socket_min/max = 0` everywhere | ~180 hand-authored, ~265 generated | Defers 240 gem containers, 25 resonances, 12 words, and `catalyst.forge`'s only real sink. The mechanic has no design value in the band v1 occupies, by I4's own §8.7. Sockets return as pure data plus one derived-count function (§5.3) |
| **3** | **Charms: ship zero.** No `charm_def`, no pouch, no resonance | ~44, plus 5 tables and 4 code sites | None today. Two blockers already stand in the way (§4.3, §2) |
| **4** | **Base types: 2 bands (b1, b3), not 4.** 43 × 2 frames × 2 bands | 172 containers, 344 core atoms, 32 base-stat atoms, ~8,300 pool rows | The guard span narrows from ~15× to ~6.1× (15^⅔), and two of four visible base-stat upgrade steps disappear. **I3 names this cut itself and calls it safe** — bands are append-only in the way rarity ordinals are not (§7.6) |
| **5** | **Affixes: author the 54 live families, hold the 16 `stat.derived` at weight 0.** Atom rows 775 → **270** | ~505 generated, ~48 words | The prefix pool is 7–9 families, and the item's identity comes from its suffix. `+armour` does not exist. This is I8's own wave-1 plan (§4.9), and lifting it later is *"an import plus one number per rarity rung, not a redesign."* Also set `prefix_rolls = 1`, `suffix_rolls ≤ 3` |
| **6** | **Enhancement: cap at +8, safe band only.** No risk band, no peril band, no pity, no `ward.enhance`, no transfer. Milestones at +4 and +8 only | `item_enhance_rules` 20 → 8; `item_enhance_cost` ≤400 → ~80; `item_enhance_track` 1,720 → **344** (from 86 hand-authored picks) | **Zero at v1 item levels** — `ilvl_cap(11) = +6`, so levels 9–20 cannot be reached from any shipped source. The D3/D4 risk design is authored but unexercised; ship it when the ilvl ladder does |
| **7** | **Reroll: ship zero operations** (or Temper alone) | 7 | Loses a material sink. Justified by §6.3: at v1 churn nobody rerolls an item they will replace in two days. `catalyst.flux` should then not ship either — a material nothing spends is the SC7 defect |
| **8** | **Sets: ship 2, four pieces each, thresholds at 2 and 4** | ~22 hand-authored, plus 8 base types | With only 2 sets the §3.2 inversion (capability at the *lowest* threshold) buys nothing yet, because the two-partial-sets build space needs ≥3 sets. Author it that way anyway, so it never has to be re-cut. No grand 6-piece sets |
| **9** | **Requirements: frame gate and `level_req` only; hold the five attributes** | 245 hand-authored | OD7 already calls attributes a proposal. `container_requirement` collapses to generated frame rows |
| **10** | **Materials: 19 ids, no catalysts.** Substrate 8 + shard 4 + essence 6 + souls | 2 ids, ~35 recipe rows | With cuts 2, 6 and 7, no operation spends a catalyst. Shipping three of them would be three rows nothing consumes — exactly SC7's `status.expose.*` failure. Catalysts arrive with their verbs |
| **11** | **Rarity: keep all 10 rungs; author only the `rarity_budget` keys whose consumer shipped** | ~70 of 130 | Zero — SC7's `ParamNotImplemented` rule already forces this (§4.4). Keeping 10 rungs is right: ordinals are spaced by 10 so insertion is free, but *removing* a rung later is not |
| **12** | **Drop tables: 3 tables against the 4 shipped `WaveCatalog` levels; no smart loot, no pity, no first-clear** | ~235 | World sectors and PvZ runs drop nothing at v1. Pity has nothing to guard while the top rungs are unreachable (§6.1) |

### 3.1 The v1 budget after the cut

| | Before | After the cut |
|---|--:|--:|
| Hand-authored | ≈ 3,100 | **≈ 880** |
| Generated | ≈ 22,500 | **≈ 9,700** |
| Base-type containers | 344 | **172** |
| Affix atom rows | ~775 | **270** |
| Pool rows | ~16,500 | **~8,300** |
| Lanes shipping content | 13 | **8** (I1 I2 I3 I6 I8 I9 I12 I13) |
| Lanes shipping schema only | 0 | **5** (I4 I5 I7 I10 I11) |

880 hand-authored cells is a real, finishable number. 3,100 with ~600 of it unsized is not a budget; it
is a hope.

---

## 4. Generator gaps — what must exist before authoring starts

Seven generators are implied by the round. **Zero of them exist.**

| # | Generator | Input | Output | Exists? |
|---|---|---|---|---|
| **G-base** | Base-type emitter | 43 identities × 2 class ladders × N bands | 344/172 containers + core atoms + 64/32 base-stat atoms | **No** |
| **G-affix** | Atom-band emitter | 70 `share` values + `r` + the two-ladder overrides | ~775/270 `effect_atom` rows with `values_json` | **No** |
| **G-pool** | Pool emitter | role × group matrix × frame/side/runtime filters × window | 4k–17k (→90k) `effect_container_pool` rows, plus weight redistribution (§4.4) and 6 lints (§6.3) | **No** |
| **G-gem** | Insert emitter | ~8 families × 6 elements × 5 tiers | ≤240 gem containers + 25 resonances | **No** |
| **G-recipe** | Forge-recipe emitter | the base-type catalog | 344 recipes + ~1,000 cost rows | **No** |
| **G-track** | Enhancement-track emitter | 43 identities × milestones | 1,720/344 rows | **No** |
| **G-cost** | Enhance-cost emitter | 3 formulas (`shard = ceil(L^1.6/2)` etc., §7.5) | ≤400/80 rows | **No** |

**G-pool must exist before the first base type is authored, not after.** A container whose pool cannot
satisfy its own `pool_rolls` rejects at import with `PoolRollsExceedGroups`, and a container with no
drawable rows rejects with `UnsatisfiablePool`. Hand-authoring even one base type without the generator
means hand-authoring ~48 pool rows just to make it importable.

### 4.1 The precedent in the tree — two shapes, both usable

The owner asked where the demon species catalog is generated. There are two distinct patterns, and the
item program needs a third that borrows from both.

**Shape A — offline emitter, checked-in output.**
`tools/DemonCatalogGen/Program.cs` reads captured types through the DAL (no SQL of its own), calls
`DemonSpeciesGenerator.Generate(seeds)`, runs `DemonSpeciesCatalog.Validate(species)`, and writes
`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.Generated.cs` — 24 species, with an `<auto-generated>`
header naming the regeneration command and the rule *"Do not hand-edit — rebalance via the generator,
then re-emit."* It prints a distribution summary so a bad emit is visible at the console. Deterministic
tie ordering is explicit and commented (`(side, type, game)`), because SQLite's duplicate order is
unspecified.

**Shape B — runtime deterministic build, no file.**
`DemonRecipeCatalog.All = Build()` (`src/FusionRpg.Core/Demons/Fusion/DemonRecipeCatalog.cs:47`) derives
every fusion recipe from `DemonSpeciesCatalog.All` at first touch — one recipe per summonable rare+
species, pairs forced unique, capture-only species excluded — validated eagerly at startup, with
`BuildForTest()` as an explicit determinism seam.

**What the item generators need is Shape A's discipline with a SQL-content target.** They emit
`effect_container`, `effect_container_pool`, and `effect_atom` rows, not C#. I8 §5.4 already picked
exactly this and gave the reason: the generated rows are the SSOT, the input files are just the
importer's input, and `effect_atom` / `effect_container_pool` are **already in the E8 covered-table
registry** — so a band or weight change moves the content hash with no `contentHashSchemaVersion` bump.
Making the *inputs* tables would need registering them, repeating the unregistered
`effect_channel_policy` defect the catalog SSOT already flags.

**Recommendation:** one `tools/ItemContentGen` console project, Shape A's header/validate/summary
discipline, seven emitters behind one entry point, emitting content rows into the importer's input. The
test of whether the split was drawn in the right place: the generator should be smaller than the content
it emits.

---

## 5. Collisions — two lanes authoring the same content from different directions

| # | Collision | The two directions | Severity |
|---|---|---|---|
| **C1** | **Role × family legality** | I2's `item_role_family(role, frame, family, max_tier)` — ~1,100 hand-authored rows (§5.2). I8's 15-group × 13-role weight matrix — 195 cells, plus three filters (§4.3, §4.4). Same content, two grains, two owners. Contract §4 cut #5 gives I8 "affix tier bands and the pool"; cut #1 gives I2 "equip slots". **Neither cut names this table** | **High.** Fixed by cut #1 in §3 |
| **C2** | **Pool-row volume per container** | I3 §7.6: *"1 container row + 2 core rows + ~12 pool rows each… roughly 5,200 rows."* I8 §4.8's worked example: **48 pool rows** for one plant `core-protective` at ilvl 45, wave 1. A **4× disagreement**, diverging further post-E12 | **High.** Two lanes sizing the same table an order of magnitude apart |
| **C3** | **The ilvl → tier unlock table** | I12 §4.1: t1 @ 1, t2 @ 1, t3 @ 8, t4 @ 18, t5 @ 32. I8 §10.3: *"ilvl unlocks 1/12/25/40/60."* One table, two values, ~2× apart. Every band boundary in the game moves with it | **High.** Nobody owns it — cut #5 gives I8 the tier bands, cut #9 gives I12 the drop, and the *unlock schedule* falls between them |
| **C4** | **How many rarity rungs** | I1: **10** rungs, named, ordinals spaced by 10 (§3.3). I12: **7** illustrative rungs R1–R7 with concrete draw weights (§4.2). I6: **5** (Normal/Magic/Rare/Epic/Legendary) for `rarity_cap` (§7.3). item-ideal §6.2: **4**. I12 addresses rungs by ordinal so I1 can rename — but the *count* is not a name, and drop weights and enhancement caps are per rung | **High.** Three per-rung tables of three different lengths exist right now |
| **C5** | **Set member base types** | I5's members are `effect_container` rows of kind `item` with role + frame (§4.2). I3's generator emits 344 from 43 identities (§7.6). **Nobody says whether a set piece is one of the 43 or an extra identity.** If extra, each 4-piece set adds 4 identities × 2 frames × N bands | **Medium.** Silently changes I3's base-type count |
| **C6** | **`item_enhance_track` is authored by I3, per I6** | I6 §5.4 lists `item_enhance_track(base_type_id, at_level, atom_id, seq)` with **Author: I3**. ssot-item-categories never mentions it. 1,720 generated rows from 215 hand-authored milestone picks, assigned to a lane that did not count them | **Medium** |
| **C7** | **Forge recipes** | I9 §9.3 asks that forge recipes be *generated* from the base-type catalog. I3 never mentions recipes. 344 recipes + ~1,000 cost rows are unowned | **Medium** |
| **C8** | **Frame legality declared twice** | I3 puts `frame` on `item_base_type` (§5.2). I11 puts frame membership in `container_requirement(axis='frame')` (§5.1). I8 §9.4 already flags that the frame filter and the equip gate *"must read the same column"* — they currently read two | **Medium** |
| **C9** | **Do socket-granted atoms count against the affix class quota?** | I4 §9.12: *"a `vitality` gem in an item that already rolled `vitality` is legal and both apply"* — `group` is per container. I8 §9.9: *"a 3-socket item with 3 proc gems is a 6-suffix item… the single biggest interaction risk between our two lanes."* Both positions stated; they contradict | **Medium.** Moot only while sockets ship empty |
| **C10** | **Three mechanics called "resonance" / "combination"** | I4's element resonance (socket multiset), I10's axis resonance (pouch), I5's set tiers. I10 §3.5 drew the I5 boundary explicitly; nobody drew the I4/I10 one. Three combination layers, two sharing a word | **Low** — naming and UI, not schema |
| **C11** | **`gem` container kind reused for combination containers** | I4 §4.5 uses `gem` for both inserts and their combos; SC3 reserves it *"for a socket insert."* The lane flags this itself as a reading, not an application | **Low** — self-declared |

---

## 6. Q2 — gear relevance cadence

### 6.1 What each source implies, worked

**Source 1 — drop volume (ssot-generation §8).** Committed: *"a player should look at 100% of equipment
drops and keep 20–35% of them."* Thirteen normal + two boss battles per 30-minute session gives
`13 × 0.55 + 2 × 1.40 = 9.95` ≈ **10 items/session**; **20–30/day**; at 30% keep, **6–9 keepers/day**.
Spread across I2's 15 roles, a given equip slot receives an upgrade candidate every **1.7–2.5 days**.

> **Implied cadence: an item in a given slot lives about two days of play. Slot churn every session
> or two.**

**Source 2 — the item-level curve (ssot-generation §4.1).** `itemLevel = contentLevel + jitter(−1/0/+1)`,
with tier unlocks at **t1 @ 1 · t2 @ 1 · t3 @ 8 · t4 @ 18 · t5 @ 32**.

> **Implied cadence: four wholesale refresh events across the whole game — one per tier unlock. Once
> per progression tier.**

**But the shipped content ladder does not reach them.** Verified against code, not the doc:
`WaveCatalog` ships four waves at `RecommendedLevel` **1 / 3 / 6 / 10**
(`src/FusionRpg.Core/Battle/WaveCatalog.cs:32-35`). `ExpeditionTierDef` is
`(TierId, Name, DurationMinutes, TickCount, BattleCount, SquadSlots, HasBossWave)` — it has **no level
field at all** (`src/FusionRpg.Core/Expeditions/ExpeditionTierCatalog.cs:7-21`), so I12's *"scout 2 ·
forage 5 · hunt 9 · warpath 14"* is a proposed addition, not a shipped source. The highest reachable
item level today is therefore **11** (content 10 + jitter 1).

> **t3 is the ceiling. t4 and t5 are unreachable — 40% of the authored tier ladder cannot drop.** So is
> the entire enhancement risk band: `ilvl_cap(11) = clamp(4 + 11/4, 4, 20) = 6`.

**Source 3 — the rarity ladder's power spread (ssot-rarity §3.5).** Measured adjacent-rung upset rate
`U(n,1)` = 7.9–28.3%; `U(n,2)` ≤ 7.9%; `U(n,3)` ≤ 1.6%; `U(n,4)` ≈ 0. So a rung-*n* item is only
*reliably* beaten by a rung-*n+2* item. With 10 rungs that is **five reliable replacement steps** across
the whole progression. On I12's illustrative weights at 25 items/day: R4+ ≈ 2.5/day, R5 ≈ 0.55/day, R6 ≈
one per 5.7 days, R7 ≈ **one per 13 days**.

> **Implied cadence: five meaningful replacements over a campaign, plus a chase tail measured in weeks.
> For the top two rungs — effectively never.**

**Source 4 — the enhancement ceiling (ssot-enhancement §7.5).** Reaching **+8** costs **52 `shard.epic`
— 5% of the ladder.** Reaching **+20** costs **~960 expected**, including failures and pity — **18×**.
Marginal power per level is roughly flat (a constant 20‰ plus lumpy milestones) while cost grows
superlinearly.

> **Implied cadence: +8 gear is disposable (5% of the ladder). Anything past +12 (~215 shards) is a
> terminal item held for weeks. The lane says so: *"the ladder self-terminates well before the cap."***

**Source 5 — the affix tier bands (ssot-affixes §4.5).** `r = 1.75` per tier; band `[0.67m, 1.33m]`;
`hi/lo = 1.985 > r`, so adjacent bands overlap. t1 → t5 spans **9.4×**. A tier step is **+75%**; your
own within-tier roll range is **±33%**.

> **Implied cadence: a tier step is worth more than the best roll obtainable at your current tier.
> So an item is replaced when a new tier opens, and essentially not otherwise — once per tier unlock.**

### 6.2 They do not imply the same cadence

| Source | Cadence it produces |
|---|---|
| Drop volume (I12 §8) | **Every session or two** |
| Item-level curve + tier bands (I12 §4.1, I8 §4.5) | **Once per progression tier** — 4 events designed, **2 reachable** |
| Rarity ladder (I1 §3.5) | **Five steps a campaign**, plus a weeks-long chase tail |
| Enhancement (I6 §7.5) | **Below +8, disposable. Above +12, never** |
| Reroll escalation (I7 §5.2) | **Never** — the counter is per instance and *"never resets"* |

Five documents, four different answers, and no lane owns the question.

### 6.3 The contradiction, stated exactly

**Item churn of ~2 days per slot is incompatible with three committed investment mechanisms.**

1. **Reroll (I7).** `ESCALATION‰ = min(4000, 1000 + 250 × priorOps(instance, op_kind))`, per instance,
   never reset. Reroll is by construction an investment in *one specific item*. An item you will replace
   in two days is worth exactly zero rerolls. **At v1 churn rates, I7 is dead content for ~90% of the
   game** — which is the lane's own failure mode 11.3 (*"a system so punishing nobody uses it"*)
   arriving through the cadence door rather than the price door.

2. **Sockets (I4 §4.7).** Removal at t4–t5 **destroys the insert**. On a two-day item that is a
   guaranteed loss, so nobody sockets a good gem into churn gear. I4 rejected "removal destroys the
   item" precisely because it would make *"sockets endgame-only and dead content for most of the
   campaign"* — and destroying the *insert* reproduces that outcome exactly, via the cadence instead of
   via the rule. The lane could not see this, because cadence is not in its scope.

3. **Sets (I5).** Four pieces in four specific roles, from a narrower pool (`pool_rolls = 2`, 2-tier
   window, §3.9), so **each piece is a worse item than a rare of the same rung until the set completes**.
   At 6–9 keepers/day across 15 roles, a 4-piece set is a multi-week collection during which the player
   holds four deliberately sub-par items. The lane asked I12 for *"completion bias / duplicate protection
   on set drops"* (§8.8), and I12's §3.3 answer is **frame-weighted smart loot only — role is explicitly
   flat**. The ask is currently refused, and no set completes on schedule.

**The enhancement curve is the one that is *not* contradictory**, and it is worth saying why, because it
is an accident worth keeping. `ilvl_cap(ilvl) = clamp(4 + ilvl/4, 4, 20)` evaluated at the top of the
churn band gives `ilvl_cap(17) = +8` — **exactly the end of the free/safe band, which costs 52 of ~960
shards.** Churn gear can only ever reach the band where enhancement is cheap and cannot fail. That
alignment was not designed; it falls out of two independently chosen formulas. **Do not break it.**

### 6.4 Recommended cadence

**Three bands, keyed on item level, stated once and consumed by every lane.**

| Band | ilvl | Tiers | Item lifetime | Enhancement | Sockets | Sets | Reroll |
|---|---|---|---|---|---|---|---|
| **Churn** | 1 – 17 | t1 – t3 | **1–3 sessions** | ≤ +8, free band only (`ilvl_cap` already enforces this) | t1–t3 inserts only; removal free or costed | none drop | none |
| **Consolidation** | 18 – 31 | + t4 | **~1 week** | +9 … +14, risk band | t1–t3, plus resonance | set pieces begin dropping | Temper only; escalation resets on a new item |
| **Terminal** | 32 + | + t5 | **indefinite** | +15 … +20, peril band | t4–t5 inserts, destructive removal, words | grand sets | all three operations, escalation as written |

Three bands rather than four or five, because the shipped machinery already draws two of the three lines
for free: `ilvl_cap` steps the enhancement bands at ilvl 17 and 47, and the tier unlocks step at 18 and
32. A fourth band would need a fourth number nothing else reads.

The player-facing sentence: **"early gear is weather, mid gear is a wardrobe, late gear is a
possession."**

### 6.5 What must move to hit it

| # | Lane | What moves | Why |
|---|---|---|---|
| **1** | **I12** | **Publish a content-level ladder that reaches ilvl 32+.** Today the maximum reachable item level is **11** (verified above). Add a level field to `ExpeditionTierDef` — it has none — and extend `WaveCatalog` past `RecommendedLevel = 10` | Without this, t4, t5, the consolidation and terminal bands, the risk and peril enhancement bands, words, and rungs 80–100 are all **authored content that cannot drop**. A blocker, not a tuning knob |
| **2** | **I8 + I12** | **Reconcile the ilvl → tier unlock table** (C3): `1/1/8/18/32` vs `1/12/25/40/60` | They differ by ~2×, and every band boundary in §6.4 moves with the answer. Pick one, put it in one place |
| **3** | **I4** | **Gate destructive insert removal on the item's band, not the insert's tier** — or, equivalently, make t4–t5 inserts undroppable below ilvl 18 | Removes the churn-band trap without changing the rule's intent |
| **4** | **I5** | **Add an item-level floor to set membership** — set pieces drop only at ilvl ≥ 18 — and get role-completion bias from I12, which currently refuses it | Otherwise every set piece is salvaged during churn before the fourth arrives |
| **5** | **I7** | **Count escalation only for ops performed while the item is in the consolidation band or above**, or ship nothing at v1 | A per-instance counter that never resets is correct for a terminal item and wrong for a disposable one |
| **6** | **I13** | **Auto-lock items at or above the terminal ilvl floor** against bulk salvage, by default | I13 already has `locked` on `rpg_item` (§4.2). Let the band boundary set it |
| **7** | **I6** | **Register `enhance_cap` in I1's `rarity_budget` with the band alignment written down**, and never let `rarity_cap` exceed the band's `ilvl_cap` | Preserves the §6.3 accident on purpose |
| **8** | **I1** | **Confirm the top two rungs (90 Sunwoven, 100 Almanac) are terminal-band-only.** §3.7 rule 7 already stops promotion at 80; the drop side needs the same floor | Otherwise a chase rung drops into a two-day slot |

---

## 7. Open questions for the owner

1. **The roster-scale gear economy** ([item-ideal.md](../item-ideal.md) §8). **Nine of the thirteen
   lanes name this as their largest unknown** — I12 §11.1, I4 §10.5, I5 §9.5, I9 §10.8, I8 §10.7, I3 §9,
   I6 §7.5, I2 §4.2, I13 §5.10. I12's volume is calibrated for **a deployable squad of five** (75 slots
   ≈ 10 days to gear). If the answer is "all twenty demons", **drop volume is 4× too low, every number
   in §6 of this document moves, and the §6.4 bands compress**. This is the single input that most
   changes both answers here, and nothing should be built before it lands.
2. **Sets and charms have no v1 count.** How many sets ship? How many charms? These are the two UNSIZED
   entries in §1.1, and they are the two where a row count badly understates the work — each is a named
   fantasy plus a capability atom plus a balance pass, not eleven rows.
3. **Which rarity ladder is real** (C4). I1 authored 10 rungs; I12 designed drop weights against 7; I6
   set enhancement caps against 5. Three per-rung tables of three different lengths exist right now.
4. **Is `item_role_family` derived or authored** (C1)? Deriving it saves ~1,100 hand-authored cells and
   removes a second source of truth. The only argument for authoring it is per-(role, family) `max_tier`
   granularity, which I2 uses for exactly one thing — the `max_tier = 3` cap on the twin minor jewels.
5. **Should v1 ship the whole ilvl ladder, or only the churn band?** §6.5 item 1 says content must reach
   ilvl 32 for half the round's design to be reachable. The alternative is a deliberate v1 that lives
   entirely in the churn band and ships the consolidation and terminal machinery unwired. Both are
   defensible; only one of them should be chosen on purpose rather than by omission.
6. **Who owns the durable per-actor owner scope?** §2.1. No lane owns it, R3 does not cover it, and
   every equipment binding depends on it.
7. **Should the four R3 gap lanes be sized before the v1 cut is committed?** Uniques (G1) in particular
   is *"the one that breaks the generator's rules on purpose"* — hand-authored by definition, and
   therefore the most expensive rows per unit in the whole program. Cutting the generated lanes down to
   880 hand-authored cells is worth little if G1 then adds 300 by hand.

---

## 8. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — every item lane I1–I13, the effect-atom
    container/instance/binding layer, the battle wave catalog, and the expedition tier catalog.
[x] I read enrichment-contract.md and reconciliation-plan.md first, then item-ideal.md,
    atom-family-library.md, and all twelve ssot-*.md files, this session.
[x] I checked the contract's §6 owner decisions — OD2 (~15 slots), OD3 (hybrid), OD4 (overlap),
    OD5 (combination bonuses), OD7 (attributes are a proposal) all bear on the cut, and none is
    re-litigated here.
[x] Every factual claim about a lane cites the lane document and section.
[x] I verified claims against CODE, not comments, wherever a lane's cadence depended on it:
    WaveCatalog.cs:32-35 (RecommendedLevel 1/3/6/10), ExpeditionTierCatalog.cs:7-21 (no level
    field at all), DemonSpeciesCatalog.Generated.cs, tools/DemonCatalogGen/Program.cs,
    DemonRecipeCatalog.cs:47. The finding that t4/t5 are unreachable is read from code and
    contradicts a lane's stated input.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no test suite was run.** The
    reachability finding in §6.1 is read from shipped source, not executed. Run the suites before
    it is used to justify a build decision.
[x] Nothing contradicts a §2 invariant — SC1, SC2, SC4 (units named on every value), SC7 (the
    catalyst cut in §3 item 10 is SC7 applied), SC8, SC9 (no recommendation depends on power).
[x] R2 rule: this debate names what it rejected. The Method note states the two rejected framings,
    and §3 names the cost of every cut rather than listing reductions.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item capability map, plan, or task list exists yet.** R4 is where this lands.
```
