# Game-design research — unit design, counter systems, roster scale

**Captured 2026-09-01** for the `demon-seed` idea phase
([demon-seed-ideal.md](../../architecture/demon-seed-ideal.md) §4 is the distilled version; these
files are the raw material behind it).

**Read this before commissioning any research on unit design, elemental typing, counter matrices,
rarity ladders, or roster scaling.** Eight research passes ran here, several hundred web searches
were spent, and **[06-unsourced.md](06-unsourced.md) records what does not exist** — re-running those
searches will waste the same budget again.

---

## Method, and why these numbers are trustworthy

Findings came from **shipped game data**, not wiki prose, wherever it was reachable:

| Source | Used for |
|---|---|
| PokéAPI `damage_relations`, Pokémon Showdown dex + ladder stats | Pokémon type matrix, ability distributions, usage tail |
| OpenBW `bwgame.h`, BWAPI enums | StarCraft I damage table, exact fixed-point values |
| Blizzard `.sc2mod` catalogs, `s2client-proto` | SC2 attributes, bonus list, `GlossaryStrongArray` |
| Blizzard `classic.battle.net` | Warcraft III and StarCraft I matrices, first-party |
| genieutils (Advanced Genie Editor's library) | AoE2's 38 armour classes and the summed formula |
| EA's GPL Command & Conquer release | `Verses=` armour-type order, the `strtok` parser |
| Relic Essence exports, `coh3-stats` | CoH penetration/accuracy formulas, 191 unit tags |
| RPFM schemas | Total War column inventories |
| Arknights `character_table.json` | 425 operators, 72 branches, full stat frame |
| Atlas Academy FGO export, SWARFARM API | FGO class table, Summoners War families |
| Open5e (3,207 creatures), Archives of Nethys (4,748) | D&D 5e and PF2e distinctness statistics |
| Diablo II 1.13 data files, Ragnarok `mob_db.yml` | Hell immunity counts, RO's four-axis taxonomy |

Numbers marked **(computed)** in the files are tallies over primary data, not quotes.

---

## The files

| File | What it answers |
|---|---|
| [01-typing-matrices.md](01-typing-matrices.md) | How big can an element/type matrix be? Pokémon's full statistics, WC3/SC1/FE/DoW2 matrices with real values, Genshin's reaction system as the alternative |
| [02-unit-variables.md](02-unit-variables.md) | **What fields does a unit carry?** The consolidated checklist across 7 RTS families and 9 RPG systems, marked universal/common/rare |
| [03-roster-scale.md](03-roster-scale.md) | **How many category values keep N units distinct?** The units-per-grid-cell ratio, taxonomy saturation, what rarity actually controls |
| [04-designer-quotes.md](04-designer-quotes.md) | Every verbatim first-party statement found, with attribution and URL |
| [05-failure-modes.md](05-failure-modes.md) | Documented disasters, with causes — the corpses |
| [06-unsourced.md](06-unsourced.md) | **What does not exist.** Read before searching |

---

## The six findings that mattered most

1. **~1–3 units per grid cell is where games stay clean.** Summoners War 1.02, HSR 1.8, Arknights
   1.97, Genshin 3.4, FGO 3.2 — all low creep. **FEH at ~15 (max 129 in one cell) is the worst
   documented power creep in the genre.** [03](03-roster-scale.md)
2. **Stats do not distinguish units; abilities do.** 63% of 3,207 D&D creatures share their exact
   (CR, AC, HP) triple; 71% of trait names appear on exactly one creature. Pokémon's type combo alone
   gives median 3 species per cell, type + ability set gives **median 1, 68% singletons**.
   [03](03-roster-scale.md)
3. **Four AAA franchises abandoned N×N damage matrices and none went back** — SC2, AoE4, Total War,
   Company of Heroes. 42 cells (WC3) is the largest any of them shipped. [01](01-typing-matrices.md)
4. **Reactions cost O(named reactions); matrices cost O(n²).** Genshin covers 26 live interaction
   cells with ~22 learnable facts; Pokémon's 18×18 needs 120. And reactions **compose** — a matrix
   cell is terminal. [01](01-typing-matrices.md)
5. **Rarity buys breadth and ceiling, never power** — in all seven collectible games studied.
   Arknights moves deployment cost 3 points across five rarity tiers; class moves it 11.
   [03](03-roster-scale.md)
6. **Cap the magnitude, creep the effect vocabulary.** FGO's median 5★ ATK moved **32 points in ten
   years across ~450 Servants**; everything that got stronger got stronger in effect text.
   [03](03-roster-scale.md)

---

## Two things this research is *not*

**It is not a spec.** Nothing here says what this game should do. `demon-seed-ideal.md` §4 draws the
conclusions; these files hold the evidence.

**It is not complete on designer intent.** The single most consistent negative finding across every
pass: **studios almost never publish roster or grid design rationale.** Genshin's official
"Developers Discussion" series, enumerated across 18 entries 2023-09 → 2025-07, is *entirely*
quality-of-life — not one entry touches roster growth, duplicate avoidance, grid coverage, or power
creep. Expect to reason from shipped data, not from interviews.
