# Feature comparison — PvZ, the mod scene, the genres, and this project

**Compiled 2026-09-02** from the eight research passes in this folder. Evidence only.

> **This is a comparison, not a proposal.** Nothing here says what this game should do, and no row is a
> recommendation. The "here" column records **what exists in this repo today, with a citation**, so that
> a later design conversation starts from fact rather than from memory. Design work still goes through
> [`docs/DESIGN-GATE.md`](../../DESIGN-GATE.md).

---

## The finding in one paragraph

Across the whole franchise and its mod scene, **the systems that add depth to a lawn are almost never
combat-math systems — they are economy, placement, and rule-variation systems.** PvZ2 buys its
difficulty with the player's sun and setup budget rather than enemy health; the host game, PVZ Fusion,
exposes a `zombieHealthMultiplier` in its own binary and its N0–N6 ladder deliberately does not touch it.
The PvZ2 mod scene converges hard on four things — total rebalance, new worlds, a roguelite endless
draft, and deleting the storefront — and almost never builds stat channels, elements or equipment. **That
absence is a tooling fact, not a taste fact:** plant behaviour is compiled into `libPVZ2.so`, and the two
projects that escaped that engine both grew an RPG layer within two versions. This project sits on the
far side of that wall already — it owns its combat pipeline — which means its comparison set is not the
PvZ2 mods at all. **It is PvZ Fusion itself, PvZ Heroes, Arknights and the SMT line**, and against those
its gaps are mostly gaps in *variation vocabulary* (objectives, tile rules, drafts, rank legality) rather
than in numbers, where it is already ahead of every comparator in the set.

---

## How to read the "here" column

| Mark | Meaning |
|---|---|
| **✅ shipped** | In code and proven, per a per-doc status header verified against `src/` |
| **🟡 wiring gap** | Vocabulary or runtime exists but has few or no production callers. **This is an unfinished-wiring state, not an architectural wall** — see the `effect-pipeline` row of [`DESIGN-GATE.md`](../../DESIGN-GATE.md) §1, which records the same distinction after a built-but-uncalled layer was mis-read as unreachable |
| **📋 specced** | Written and reviewed, not built |
| **⬜ absent** | No vocabulary for it exists |

Per-doc status headers win over `decisions.md` where they disagree. **Two headers were found stale
against code during this pass — both recorded in §7.**

---

## 1. Progression and economy

| Feature | PvZ 1 | PvZ 2 (intl) | PvZ 2 (CN) | PvZ 2 mods | **PVZ Fusion (host)** | Genre best-in-class | **Here** |
|---|---|---|---|---|---|---|---|
| **Per-unit levels** | ⬜ (two-tier upgrade plants only) | Seed packets, L1–10, then 200 Mastery levels | 5 tiers: 220 fragments + 850,000 coins, flat 150/200/250/300% | **3 / 12 teams** — ECLISE tiers, Fusion upgrades, Gardendless clones | Abyss upgrades +150/+300/+900%, scaled to 5% at Odyssey rarity | Arknights: level + elite + mastery + module (4 axes) | ✅ `P(Θ)` ladder, owner-committed 2026-08-24 ([power-map](../../architecture/power-map.md)) |
| **Levelling buys behaviour, not just stats** | — | **No** — one exception (Mastery chance-to-boost) | Authored skill rider per tier | ECLISE tiers are explicit **sidegrades** | Rarity-scaled costs | SMT: skill inheritance at fusion | ✅ atoms/affixes are the behaviour layer ([effect-atom-map](../../architecture/effect-atom-map.md)) |
| **Levelling can reduce cost** | ⬜ | **Yes** — `Cost` is a per-level array (Sunflower 50→25 at L8) | — | — | — | — | ⬜ no cost-side channel |
| **Rarity ladder** | ⬜ | ⬜ | Store colour tiers | **2 / 12** | `CardLevel` = White/Green/Blue/Purple/Gold/Red (**6**, verified in binary) | PvZ Heroes: 6 tiers, buys text not stats | ✅ **10 rungs**, Chaff→Almanac, verified in `DemonRarity.cs` |
| **Rarity separate from upgrade state** | — | — | — | — | **Yes** — `CardLevel` *and* a separate `UI.Quality` axis | Genshin: rarity vs refinement | ✅ rarity vs affix roll are independent |
| **Equipment / attachments** | ⬜ | ⬜ | Pendants (4 quality steps) | **1 / 12** (official only) | ⬜ | PoE / Diablo affix items | ✅ affixes built (`AffixLibraryGenerator`, `AffixValidator`, `Resolver`); **📋 sockets/sets are specced only** — [`spec-sockets-and-sets.md`](../../design/spec-sockets-and-sets.md) has no code behind it (corrected 2026-09-02) |
| **Ongoing upkeep on owned units** | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | WC3 upkeep: 3-bracket income tax, **the only first-party rationale doc found** | ✅ contracts: binding slots, loyalty, daily tribute |
| **Storefront / monetisation** | Coins → shop | Gems, packets, premium plants | Aggressive gacha + VIP | **Deleted by 10 / 12** | None | — | ⬜ none, and none planned |

**The row that matters most is "levelling can reduce cost."** Every comparator scales power upward only.
PvZ2 alone also scales the *price* down, which changes what a level means: it buys board space, not
just damage. No equivalent channel exists here.

---

## 2. Combat depth vocabulary

| Feature | PvZ 2 | PvZ 2 mods | **PVZ Fusion (host)** | TD genre | RPG / RTS | **Here** |
|---|---|---|---|---|---|---|
| **Elemental matchup matrix** | ⬜ | **0 / 12.** Nearest is Reflourished's single cold→fire ×1.25 rule | ⬜ (elements are naming prefixes: `Ice*` 30, `Fire*` 18) | **Keyed immunities, not matrices** — camo/lead/purple | Pokémon 18×18, ~37% non-neutral | ✅ Element Hub shipped 2026-08-19, **two** matrices (combat ring + asymmetric shield) |
| **Crit / attack speed / DR as stat channels** | ⬜ | **1 / 12** — Fusion only | ✅ crit, attack speed, DR, healing | Arknights: flat-subtraction armour | ARPG standard | ✅ 261+ registered derived channels, overlay combat default-on 2026-08-30 |
| **Status effects** | Per-plant scripted | Scripted | `PlantStatus` 44 / `ZombieStatus` 49 (verified) | Slow/stun/burn | Ailment systems | ✅ StatusRuntime S0–S7; **21 declared, ~13 functional** ([status-ssot](../../architecture/status-ssot.md)) |
| **Shields as independent HP layers** | ✅ armour with its own HP; 2/3-HP decapitation rule | ✅ | `FirstArmorType` = Nothing/Cone/Bucket/Doll… | Arknights barriers | — | ✅ shield program T1–T16, 164 tests |
| **Damage cap vs stat cap** | — | — | **Per-hit cap of 5,000 damage, not a stat cap** | — | D3: display change, no squish | ✅ no hard ceilings; bounds throw ([ssot-power-scale §11](../../architecture/power/ssot-power-scale.md)) |
| **Soft-cap form** | — | — | — | — | **`X/(X+K)` in 4 shipped games** — `EHP = HP × (1+X/K)`, linear and unbounded | 🟡 no single declared soft-cap form |
| **Positional / rank legality** | Lane + column, no rank rules | — | — | Placement scarcity is the dial | **Darkest Dungeon: only 7 launch masks, all contiguous runs**; 19% of skills move somebody | 🟡 9 columns exist; no rank-legality vocabulary |

**Where this project is unambiguously ahead of every PvZ comparator:** elements, crit/DR channels,
statuses, shields, and an unbounded power ladder. **Where the comparators are ahead:** positional rules.
Darkest Dungeon gets enormous variety from 7 contiguous rank masks over 4 slots; this project has 9
columns and no legality vocabulary over them.

---

## 3. Endless and repeatability

| Feature | PvZ 2 | PvZ 2 mods | **PVZ Fusion (host)** | Genre best-in-class | **Here** |
|---|---|---|---|---|---|
| **Endless mode** | 11 Endless Zones; Arena; Penny's Pursuit | **7 / 12** ship a roguelite endless draft | Odyssey / Odyssey Purgatory / Abyss | BTD6 freeplay: **8 linear brackets**, not a curve | ✅ endless grind is the SSOT |
| **Between-run draft** | ⬜ | **7 / 12** — the single most-copied added mechanic in the scene | ✅ drafted buffs, strong modifiers gated behind clearing level 5 | Hades, StS, Arknights IS | ⬜ no run-scoped draft |
| **Difficulty via player economy** | **6 of 8 Penny's Pursuit knobs** | Sun-meta changes (3/12) | **N0–N6 multiplies spawn ×1→×10, speed +0.2, DR 0/30/60% — never HP** | Slay the Spire: **16 of 20 ascensions are rule changes**; Dead Cells: **zero stat multipliers** | 🟡 difficulty is expressed mainly through `P(Θ)` magnitude |
| **Difficulty via added rules** | Level objectives (see §4) | Per-level patching | Travel debuffs: `TravelDebuff` **142**, grew +56% in one minor version | Hades: 15 orthogonal dials, 63 heat | 🟡 vocabulary exists (atoms), no ladder built on it |
| **Enemy HP vs damage divergence** | — | — | Quarantined to Hell/Abyss | **Universal**: D3 diverges ×5.15M over 149 tiers; Last Epoch is the counter-case and players quit | ⚠️ `P(Θ)` is one curve for both — see §6 |

**Three independent sources — PvZ2's own difficulty knobs, the host game's difficulty ladder, and the
roguelite literature — arrive at the same conclusion: scale the player's constraints, not the enemy's
health bar.** This is the strongest cross-file agreement in the entire research set.

---

## 4. Content generation and authoring leverage

| Lever | Evidence | Ratio achieved |
|---|---|---|
| **Objectives as state predicates** | PvZ2: "don't lose >2 plants", "produce ≥5,000 sun", "never >16 plants" | **Near-zero marginal cost** — no art, no unit, no tile; re-prices existing strategy |
| **Crosspathing** | BTD6: 15 authored upgrades → **64 legal build states** per tower (computed, verified against 63 shipped state files + base). 390 upgrades → 1,664 states | **~4.3× leverage** |
| **Fusion combinatorics** | PVZ Fusion: ~187 base plants → **532 fusions** in v3.9 (wiki tally, tier-B) | **~2.8×**, but 532 things now need balancing |
| **Variants** | Garden Warfare: **~7–9 variants per base class**, stable when class count doubled | ~8× |
| **Difficulty multipliers over one level set** | PVZ Fusion: six global multipliers | **6×** on the same authored levels |
| **Trait membership** | TFT: 85 champions, **55 carry exactly 2 traits**, mean 2.12 memberships | Synergy space from flat authoring |
| **Rank legality** | Darkest Dungeon: 110 skills over **7 launch masks** | Variety from constraint, not content |

**The cheapest lever in the whole set is the objective predicate, and it is the one this project has no
vocabulary for.** The most expensive is authored worlds with tile gimmicks — the mod scene's own
bottleneck is PAM animation, not design.

---

## 5. Fusion — the host game vs the reference implementation

This is the closest comparison in the document: the host game and this project both fuse, and SMT is the
studied reference.

| Property | **PVZ Fusion (from its binary)** | **SMT / Persona** | **Here** |
|---|---|---|---|
| Recipe form | `AddRecipe` (unordered) **and** `AddOrderedRecipe` | Race chart + level rule | `DemonRecipeCatalog` |
| Is it a table or a rule? | **Table**, in named partitions (`FirstMix`, `PuffMix`, `FogPlant`, `RoofPlant`, `InitTravel`, `SpecialPlant`) | **Both** — chart picks race, `levelA + levelB ≤ 2·(levelR − 1)` picks the individual | Table + rarity policy |
| Why the rule works | — | **`(race, level)` is a primary key — zero collisions across all five games' tables (computed)** | — |
| Randomised recipes | ✅ `_recipes_random`, `UpdateRandomMix()` — **regenerated at runtime** | ⬜ (fusion *accidents* only) | ⬜ |
| Reversible | ✅ `TryGetDisMix`, `DieReason.ByDisMix` | ⬜ | ⬜ |
| Recursive / depth-aware | ✅ `PlantMixTreeNode.{Depth, IsBasicPlant, AllDescendants}` | Implicit | ✅ tiered |
| Multiple routes to one result | ✅ `GetMixPaths` returns `List<List<PlantType>>` | ✅ | — |
| Engine-computed roster metrics | ✅ `MixTreeStatistics{TotalMixRecipes, MaxTreeDepth, BasicPlantCount, MaxChildrenCount}` | — | Seedsmith computes externally |
| Result stats | Authored per result (inference) | Authored per demon | **Deterministic species stats; effects roll per player** |

**Three capabilities the host game ships that this project does not have vocabulary for: un-fusion, a
runtime-regenerated recipe table, and multiple routes to the same result.** Recorded as observation.
**And the precedence rule from nine games is the one this project already follows** — the table picks
the family, the rule picks the individual, authored always wins.

---

## 6. Cross-file agreements — where independent passes converged

These carry the most weight, because the passes did not share sources.

1. **Scale constraints, not enemy HP.** [01] §7 (Penny's Pursuit: 6 of 8 knobs), [02] §6 (N0–N6 never
   touches `zombieHealthMultiplier`), [08] §4 (StS 16/20 rule changes; Dead Cells zero multipliers).
2. **Rarity buys breadth and text, never raw power.** [04] §2 (503 cards, flat stat-per-sun, text 20→70
   chars) reproduces [`game-design/03-roster-scale.md`](../game-design/03-roster-scale.md) from inside
   the franchise.
3. **PoE's `×352` damage growth over 99 levels** appears independently in [08] §1 and in
   [`game-design/03-roster-scale.md`](../game-design/03-roster-scale.md). Two passes, same figure.
4. **Counter systems in TD are keyed immunities, not matrices** ([05]), which agrees with
   [`game-design/01-typing-matrices.md`](../game-design/01-typing-matrices.md)'s finding that four AAA
   franchises abandoned N×N matrices and none went back.
5. **Authored beats computed in precedence, everywhere fusion exists** ([06] §3, [02] §3.3).

**The one tension worth naming.** Finding 1 says scale the player's constraints; this project's
`P(Θ)` is a single quadratic curve driving **both** magnitudes and enemy scaling. [08] §1 shows every
endless game that scaled durability and lethality on the same curve had to split them — and names Last
Epoch, which did not, as the counter-case where players quit. **This is an observation about the
research, not a claim about this repo's tuning**, which was not examined in this pass.

---

## 7. Two stale status headers found while writing this

Both are **doc-vs-code discrepancies**, reported because the gate's evidence rule is *code beats
documentation*.

1. **[`resource-hub-ssot.md`](../../architecture/resource-hub-ssot.md) says "Not built — no `resource.*`
   channel family exists yet." That is stale.** `src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:497-499`
   registers `resource.max`, `resource.regen` and `resource.efficiency`; `AtomKindRegistry.cs:167,198`
   declares the `resource.delta` and `resource.economy` atom kinds on `AttachPoint.Resource`;
   `AtomCompiler.cs:180-181` compiles them to `ApplyResourceDelta` / `Economy`; `CoefficientTable.cs:138-139`
   prices them. **What is genuinely missing is a pool runtime — there is no `Core/Resources/`, and
   `AttachPoint.Resource` has 2 references.** That is a **wiring gap**, not an absence.
2. **[`aura-skill-map.md`](../../architecture/aura-skill-map.md) says "pending owner approval, no build
   authorized", while [`combat-damage-ssot.md`](../../architecture/combat-damage-ssot.md) records
   overlay combat as shipped default-on "2026-08-30, aura-skill T8".** One of the two is out of date.
   Not resolved here — flagged only.

---

## 8. What nobody in the comparison set has built

Observation, not a to-do list. Each is absent from **every** column above.

- **An elemental matchup matrix anywhere in the PvZ franchise or its mod scene.** 0/12 mods, 0 official
  titles. This project has two. There is no prior art in the lawn for how it should feel.
- **Upkeep on a collected roster** outside RTS supply. WC3's income tax is the only first-party
  rationale in existence.
- **A published counter-strength target.** Confirmed again this pass; already recorded in
  [`game-design/06-unsourced.md`](../game-design/06-unsourced.md). Any number here is derived, not
  borrowed.
- **A loyalty system that survived contact with players as a decaying meter.** Every one was deleted;
  survivors converted agency into an unlock.

---

## Provenance and weight

| File | Sourcing strength | Why |
|---|---|---|
| [02](02-pvz2-chinese-and-fusion.md) | **Strongest** | Read the host game's own assemblies; enum counts independently re-verified 2026-09-02 |
| [04](04-pvz-franchise-siblings.md), [05](05-tower-defense-genre.md), [07](07-rts-and-autobattler.md) | Strong | Full shipped datasets parsed locally (503 cards, BTD6 export, TFT export, Arknights tables) |
| [06](06-summoner-minion-fusion-rpg.md), [08](08-endless-scaling-meta-progression.md) | Strong | Source repos and published scaling tables; some gaps from search exhaustion |
| [01](01-pvz2-international.md) | Moderate | One verified datamine; **no first-party EA source was reachable** |
| [03](03-pvz2-mods.md) | **Weakest** | Reddit, Discord and Chinese sources all blocked; reception evidence rests on TV Tropes |

**Fandom returned HTTP 402 to every request in all eight passes.** Four mods and one Kingdom Rush mode
named in the original briefs could not be confirmed to exist under those names — recorded in the
relevant files' gap sections.
