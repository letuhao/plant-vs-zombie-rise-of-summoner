# Roster scale, distinctness, and rarity

How many category values keep N units distinguishable? Captured 2026-09-01 from shipped data.

---

## 1. ⭐ The units-per-grid-cell ratio

"Grid cells" = the product of a game's own primary categorical axes. **Rarity is excluded** — it is
an acquisition axis, not an identity one.

| Game | Roster | Elements | Classes/roles | Rarities | Primary grid | Filled | **Units/cell** | Median | Max |
|---|---:|---:|---:|---:|---|---:|---:|---:|---:|
| **Genshin Impact** (7.0) | 118–120 | **7** | 5 weapon types | 2 | 7 × 5 = 35 | **35 (100%)** | **3.37** | 3 | 8 |
| **Honkai: Star Rail** (4.5) | ~92–97 | **7** | **9 Paths** | 2 | 9 × 7 = 63 | 54–56 | **1.7–1.8** | 2 | 5 |
| **Arknights** (CN) | **425** | none | **8 classes / 72 branches** | **6** | 72 × 6 = 432 | 216 (50%) | **1.97** | **2** | 6 |
| **Fate/Grand Order** (NA) | **419** | 5 | **14 playable classes** | 6 | 16 × 5 × 6 = 480 | 131 (27%) | **3.20** | 2 | 13 |
| **Fire Emblem Heroes** | **1,410** | 4 colours | 9 weapon → **24 combos** | 5+ | 24 × 4 = 96 | 92 (96%) | **15.33** | **7** | **129** |
| **Summoners War** | **832** | 5 | 4 roles | 5 | **174 families × 5** = 870 | 821 (94%) | **1.02** | **1** | **1** |
| **Epic Seven** | ~280–300 | 5 | 6 classes + 12 zodiac | 3 | 216 template cells | — | ~1.3–1.4 | — | — |
| **Pokémon** (Gen IX) | **1,025** | **18 types** | none | none | **171 type combos** | 162 (95%) | **7.25** | **4** | **78** |

### The pattern, and it is causal

| Units/cell | Games | Documented creep |
|---|---|---|
| **~1** | Summoners War 1.02 · HSR 1.8 · Arknights 1.97 | Low, structurally constrained |
| **~3** | Genshin 3.4 · FGO 3.2 | Low — FGO's base stats unmoved in ten years |
| ~7 (median 4) | Pokémon | Managed by a **second vocabulary** |
| **~15, max 129** | **FEH** | **Worst documented in the genre** |

**FEH is the counter-example and it is causal.** 1,410 heroes into 96 cells with **129 Red Sword
Infantry in one cell**; BST ceiling went from ~147–169 at launch to **216**. Arena scoring buckets BST
into bins of 5 *before* weapons and merges, and adds +1 per 100 SP of equipped skills and +2 per merge
— **the machine that converts stat creep into revenue.**

FEH ships **~140–147 new heroes every year** with metronome regularity (2017: 203, 2018: 119, 2019:
143, 2020: 140, 2021: 144, 2022: 144, 2023: 147, 2024: 145, 2025: 143). ~702 source characters produce
1,410 records — **~2 records per character**, i.e. re-issuing the same character into a different cell.

### The three ways games hold ~1–3 per cell

1. **Grow the vocabulary with the roster.** Arknights: 72 branches for 425 operators, and **5 branches
   exist on CN that Global has not received**. The class enum is a **content stream**, not a schema.
2. **Make the grid the primary key.** Summoners War's `family_id × element` is filled 821/870 with
   **median exactly 1 and max exactly 1** — no two obtainable monsters share a cell. 153 of 174
   families are complete at all five elements.
3. **⭐ Orthogonal axes beat a long flat list.** **Ragnarok Online: 27 authored values across four axes
   (Race 10 × Element 10 × ElementLevel 4 × Size 3) produce 417 realised mechanical identities for
   2,675 monsters.** A flat list would need 417 maintained entries.

### Adding one closed enum roughly halves occupancy

Measured over 419 NA FGO Servants:

| Key | Cells | Mean | Median | Singletons |
|---|---:|---:|---:|---:|
| class | 16 | 26.2 | 17.5 | 19% |
| + rarity | 52 | 8.1 | 5 | 21% |
| + attribute | 131 | 3.2 | 2 | 31% |
| + **card deck** | 248 | 1.7 | 1 | **66%** |
| + NP card colour | 305 | 1.4 | 1 | **77%** |

### Taxonomy vocabularies saturate at n≈300

A rarefaction test — resample *n* creatures from a full corpus, count distinct categories, 200 draws —
shows **type vocabularies stop growing at n≈300**. D&D 5e uses **14 types for 322 SRD creatures and
for all 3,207**.

At a 900-unit roster a *flat* model would want ~15 hard types, ~33 subtype tags, ~24 PF2e-style type
traits (or ~180 in PF2e's flat model), and **~270 families**. **A multiplicative model wants a few
small axes instead** — see Ragnarok above.

---

## 2. ⭐ Distinctness is carried by abilities, not stats

- **63%** of 3,207 D&D 5e creatures share their exact `(CR, AC, HP)` triple with another. **PF2e: 83%.**
- Adding **type + speed modes + resistances** lifts uniqueness to **93%**.
- **71%** of 5e's 2,472 distinct trait names appear on exactly **one** creature.
  PF2e: **8,429 ability names across 4,748 creatures, 66% used once.**
- **A 900-unit roster needs roughly 1,500–3,500 named ability instances.**

### Pokémon proves it from the other direction

| Key | Cells | Median/cell | Max | Singleton cells |
|---|---:|---:|---:|---:|
| type combination | 154 | **3** | 75 | — |
| type combination **+ ability set** | **730** | **1** | 7 | **493 (68%)** |

**Adding abilities alone takes the median cell from 3 species to 1.** ~310 abilities defined (286 in
use on base species), 934 moves.

**True near-duplicate rate: 0.5%** — 18 pairs of 1,025 species share type + ability set + BST within
20, and every one is a deliberate designed twin (Caterpie/Wurmple, Metapod/Silcoon/Cascoon,
Nidoran♀/♂ lines, Slowbro/Slowking, the Lake Trio + Cresselia, Lunatone/Solrock).

**Type is the coarse axis and was never doing the distinctness work.**

### Genshin makes it literal

119 characters share only **72 distinct HP values** and **68 distinct ATK values**. Exact collisions
six years apart: **Diluc (v1.0, 2020) and Odette (v7.0, 2026) both have HP 12,980 / ATK 334.** Also
Eula≡Sandrone, Baizhu≡Sigewinne, Kaveh≡Razor, Kuki Shinobu≡Yaoyao.

Its densest cell, **Anemo Catalyst (8 units)**, is separated entirely by ascension stat:

| Unit | ★ | Version | Ascension stat |
|---|---|---|---|
| Sucrose | 4 | 1.0 | Anemo DMG 24% |
| Shikanoin Heizou | 4 | 2.8 | Anemo DMG 24% |
| Wanderer | 5 | 3.3 | CRIT Rate 19.2% |
| Xianyun | 5 | 4.4 | ATK 28.8% |
| Yumemizuki Mizuki | 5 | 5.4 | **Elemental Mastery 115.2** |
| Ifa | 4 | 5.6 | Elemental Mastery 96 |

**The record is nearly the same unit; the kit is not.**

### The six mechanisms HSR uses inside one Path

1. **Different scaling stat.** The Hunt is nominally CRIT single-target; **Boothill is a Break Effect
   carry** — his gear, cone pool and teams are disjoint from every other Hunt unit.
2. **Different resource system.** Feixiao is Hunt with `max_sp: 12` against Seele's 120. Six characters
   bypass Energy entirely; Castorice's `max_sp` is literally `null`.
3. **Different trigger condition, so they synergize instead of compete.** Jade charges off *ally*
   attacks.
4. **Buff *verb*, not buff size.** Four Harmony units, four verbs, none strictly better — Robin owns
   advance-all, Sparkle owns skill-point economy (explicitly stackable with Bronya and Sunday), Bronya
   advance-one, Sunday energy.
5. **Team-composition gating.** The Herta's scaling doubles with a second Erudition ally.
6. **A new Path** — when nothing fits the existing structure, add structure.

### FEH's answer, and what it cost them

Because Skill Inheritance lets any hero copy any passive from any other, **two heroes in the same cell
converge on the same generic kit.** The **personal (prf) weapon** is the only non-fungible part, so
FEH's entire distinctness budget is spent there.

**Then they turned the lever itself into a product**: Rearmed and Attuned hero types whose *product is
a tradeable prf*.

---

## 3. ⭐ Rarity buys breadth and ceiling — never power

| Game | Tiers | Stat delta | Structural difference | Verdict |
|---|---|---|---|---|
| **Arknights** | 6 | monotonic | **skills 0/0/1/2/2/3 · talents 1→2 · max level 30/30/55/70/80/90 · elite ceiling · modules 4★+ · mastery 4★+** | **Breadth, decisively** |
| **Genshin** | 2 | 5★/4★ median HP ×1.18, ATK ×1.40, DEF ×1.14 | **None.** Same 3 talents, 3 passives, 6 constellations | Ceiling, not floor |
| **HSR** | 2 | ATK ×1.10, DEF ×1.22, HP ×1.18, **SPD ×0.98** | **None.** Same 5 skills, 6 Eidolons, 18-node tree | Ceiling — then abandoned |
| **FGO** | 6 | 5★/1★ ATK ×2.1 | **None on skills** — every Servant has exactly 3 actives and 5 Appends. Rarity sets party cost 3/4/7/12/16 | Cost + ceiling; **inverted by NP levels** |
| **FEH** | 5 | **~5 BST across the entire range** | Skill *access* only | **Barely power — and mutable** |
| **Summoners War** | 5 | max-level nat5/nat1 HP ×1.61, ATK ×1.63; **SPD flat ~100 at every tier** | Everyone reaches 6★ | Almost pure acquisition rate |
| **Pokémon** | none | — | — | No rarity concept |

### Arknights is the cleanest model

| Rarity | n | Skills | Talents | Elite | Max lvl | **Median DP cost** |
|---|---:|---:|---:|---|---:|---:|
| 1★ | 11 | 0 | 1 | E0 | 30 | 3 |
| 2★ | 5 | 0 | 1 | E0 | 30 | 12 |
| 3★ | 17 | 1 | 1 | E1 | 55 | 15 |
| 4★ | 61 | 2 | 1 | E2 | 70 | 16 |
| 5★ | 195 | 2 | 1 (14 have 2) | E2 | 80 | 17 |
| 6★ | 136 | **3** | **2** | E2 | 90 | 18 |

Same cost figure **by class**: Vanguard 10.5 · Specialist 11 · Supporter 11 · Medic 16 · Sniper 17 ·
Guard 18 · Defender 20 · Caster 21.5.

**Rarity moves median cost 3 points across five tiers. Class moves it 11.** That ratio is the whole
mechanism behind low-rarity viability: a 4★ built for a role pays the same *role* price as the 6★ and
delivers most of the same role output.

### The recurring refusal

**Every game that kept low rarity viable did so by refusing to let rarity buy the thing that matters
most in its own combat model:**

- **SPD in Summoners War and HSR.** SW max-level median SPD is ~100 at *every* natural star. In HSR,
  **4★ mean SPD (103) exceeds 5★ mean SPD (101)**; the same character at two rarities — Herta 4★ vs
  The Herta 5★ — is HP ×1.22, ATK ×1.17, DEF ×1.22, **SPD ×0.99, taunt ×1.00.** SPD sets turn
  frequency and is arguably HSR's most important stat. Rarity cannot buy it.
- **Deployment economy in Arknights** (above).
- **NP level in FGO** — the primary scalar on NP damage, raised **only by fusing duplicates**. Summon
  rates 5★ 1% / 4★ 3% / 3★ 40%, and 1★–3★ also drop from a free currency. **NP5 on a 3★ is a matter
  of time; NP5 on a 5★ is hundreds of dollars.** Bond gives every Servant 420 Servant Coins
  *regardless of rarity*.

### FEH's rarity is mutable

On **2018-04-10** Intelligent Systems demoted **46 heroes** a tier at once. Promotion costs
20/200/2,000/**20,000** feathers — the 10× jump at 4★→5★ is the paywall, not a power gate. Level cap
and merge cap are identical at every rarity.

---

## 4. Cap the magnitude; creep the effect vocabulary

**FGO's median 5★ ATK, bucketed by release order (JP, n=198):**

| collectionNo | n | median atkMax |
|---|---:|---:|
| #0–49 | 4 | 11,750 |
| #100–149 | 15 | 11,556 (**contains the all-time max, 13,244**) |
| #250–299 | 22 | 11,788 |
| #450–499 | 19 | 11,782 |

**Median 5★ ATK moved 32 points across ~450 Servants and ten years, and the all-time highest belongs
to an early-middle release.** All power growth went into *effect text* — 50% NP charge became 80%,
single-target became party-wide, 3-turn became 5-turn.

**Epic Seven goes further and does not author per-unit statlines at all.** Base stats come from a
216-cell (rarity × class × zodiac) template; heroes sharing all three are *numerically identical*
(Vildred and Kise — both 5★ Leo assassins). **E7 cannot creep statlines without moving a cell a dozen
heroes share**, so its creep had to go into skill text, artifacts and Exclusive Equipment.

### Durability inflates far faster than lethality — universal

| System | HP growth | Damage growth |
|---|---:|---:|
| Diablo II Normal → Hell (L85) | 6.2× | 1.85× |
| Path of Exile level 1 → 100 | 2,989× | 352× |
| **Diablo III Torment I → XVI** | **16,958×** | **163×** |

PoE's map-tier table sets boss damage to **+0% at every tier from 66 to 90.**

---

## 5. The dead tail, measured

### Pokémon — the hardest evidence in the genre

Smogon tier assignments (current SV): Uber 52 · **OU 38** · UU 36 · RU 41 · NU 38 · PU 44 ·
**ZU (bottom) 177**. **177 species — 36% of everything tiered — sit in the bottom tier.** In National
Dex, **521 forms are in the bottom bucket against 40 in OU** — ~86% of fully-evolved species.

Ladder usage, gen9ou-1695, **654,262 battles**:
- **762 species appear at all**
- only **82** reach ≥1% usage
- only **37** reach ≥5%
- **18 species account for 50% of all team slots**; 45 cover 80%; 68 cover 90%

In Doubles, **368 of 496 tiered forms (74%)** are below the usage cutoff.

### Genshin — usage among people who *own* the character

Spiral Abyss 2.7, n = **5,611 players who cleared ★36**. "Used %" is conditional on ownership.

| Rank | Character | Own% | Used% |
|---|---|---:|---:|
| 1 | Kazuha | 36% | **95.7%** |
| 2 | Bennett | 100% | 92.0% |
| 3 | Zhongli | 80% | 91.0% |
| … | | | |
| 46 | Lisa | 100% | 1.0% |
| 47 | Amber | 100% | 1.0% |
| 48 | Razor | 97% | 1.0% |
| **49** | **Aloy** | **97%** | **0.3%** |

**A ~319× spread between most- and least-used, among players who own both. Eight characters sit under
4% at 95–100% ownership.** Klee shows a **69% vacancy rate** — levelled past 71, then never fielded.

⚠️ This dataset is frozen at v2.7 (2022). No current replacement was reachable.

**The Zhongli row is its own argument**: the character that triggered HoYoverse's only pre-2025 buff
became the **3rd most-used unit in the game**. The buff worked, permanently.

---

## 6. The industry's answer to power creep: retroactive rewrites, not restraint

All three HoYoverse titles converged independently:

| Game | Programme | Shipped | Shape |
|---|---|---|---|
| **HSR** | **Novaflare** | v3.4, June 2025 | **Toggleable** per character; total kit rewrites — Silver Wolf's random Bug chance → **100%**; Blade and Jingliu converted to **Max-HP scaling** |
| **Genshin** | **Hexerei** / Witch's Revelation | Luna III, Dec 2025 | **Gated behind quest content**; modifies **Constellation effects**; adds a two-unit party resonance, so the buff doubles as a team-building tag |
| **ZZZ** | v2.5 character optimization | Dec 2025 | Dev-talk video exists |

**They did not slow new-character strength.** Each of these is a **content programme**; for a generated
roster the equivalent is a **pipeline re-run**.

Chengnan An (HSR lead designer, GDC 2025) asked directly about power creep:
> *"it is one of the issues that we have been following… as we continue to expand our combat
> experience, it's something that we will be continuously working on."*
And: *"we are considering creating more buffs for the older characters."*

### Axis extension history — every studio widened the cheapest axis

| Game | Field | Extended? |
|---|---|---|
| Genshin | **Element (7)** | **Never in 6 years** |
| Genshin | Weapon (5), Rarity (2) | Never |
| Genshin | **Reaction matrix** | **5× in 13 months** — Lunar-Charged, Lunar-Bloom, Lunar-Crystallize, Stellar-Conduct, Stellar Swirl |
| Genshin | Region | 6× |
| Genshin | Role tags | **Added from nothing** in 4.7/5.2 |
| HSR | Combat Type (7) | Never |
| HSR | **Path** | **Twice** — Remembrance (3.0), Elation (4.0). Both added a new *shape*: a **memosprite** (a fifth acting entity with its own SPD and turn order), and an **eighth skill slot** with four new stats |
| Arknights | **Subclass** | **Continuously** — 67 Global / 72 CN |
| FGO | **Class** | **7×** — Ruler, Avenger, Alter Ego, Moon Cancer, Foreigner, Pretender, Beasts |
| FEH | **Weapon colour** | **5×** |
| FEH | **Movement type (4)** | **Never in 9 years** |
| Pokémon | **Type** | **Twice** — Dark+Steel (Gen II), Fairy (Gen VI) |

**FEH widened weapon colour — which feeds only a ±20% modifier — five times, and never touched
movement type, which ~100 skills key off.** The trick that made it cheap: **colour is a property of
the unit, not of the weapon**, so the same beast weapon is red on one hero and green on another. One
indirection quadrupled five weapon types without touching the weapon catalogue.

**Genshin cannot add an element, so it extended the *interactions between* its seven five times in
thirteen months instead.**

### 4-star abandonment in HSR

| | Genshin | HSR |
|---|---:|---:|
| New 4★ in 2025 | 6 | **0** |
| New 4★ in 2026 to date | 3 | **0** |
| Last new 4★ | Prune, 2026-05-20 | **Moze, 2024-09-10** |

Both new Paths (Remembrance 7 units, Elation 5) are **entirely 5-star.**
