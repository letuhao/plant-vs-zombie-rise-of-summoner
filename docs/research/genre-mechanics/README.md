# Genre mechanics research — PvZ, tower defense, RPG, RTS

**Captured 2026-09-02.** Eight parallel research passes, ~7,900 lines, roughly 900 web queries and
several first-party binary and datamine reads. **[09-feature-comparison.md](09-feature-comparison.md)
is the distilled version** — read that first if you only read one file. These eight are the evidence
behind it.

**Read this before commissioning any research on PvZ mechanics, tower-defense design, summoning and
fusion systems, or endless scaling.** Every file ends with a **"What I could not find"** section, and
those sections are the point: they record what was searched for and does not exist or is unreachable,
so the same budget is not spent twice. Between them they list **over 120 named gaps**.

---

## Method, and why these numbers are trustworthy

Findings came from **shipped data, decompiled binaries and source repositories** wherever they were
reachable — not from wiki prose. Where only a wiki was available it is labelled second-tier inline.

| Source | Used for | File |
|---|---|---|
| **`Assembly-CSharp.dll`, PVZ Fusion 3.8.1 + 3.9, read with Mono.Cecil** | Host-game enum counts, the fusion API, difficulty ladder, rarity enums | [02](02-pvz2-chinese-and-fusion.md) |
| `dnartz/PvZ-Emulator` (reverse-engineered PvZ 1 source) | PvZ 1 spawn, projectile and zombie constants | [04](04-pvz-franchise-siblings.md) |
| `dannyguy253/PvZHeroes-Database` (503 cards from `card_data_173`) | The full PvZ Heroes stat/cost/keyword table | [04](04-pvz-franchise-siblings.md) |
| `Btd6ModHelper/btd6-game-data` (live BTD6 model export) | Paragon degree data, bloon model fields, freeplay scaling | [05](05-tower-defense-genre.md) |
| `Kengxxiao/ArknightsGameData_YoStar` | 374 Operators, 1,552 enemy records, shipped level files | [05](05-tower-defense-genre.md) |
| `megaten-fusion-tool` (fusion code + per-game datamined JSON) | The SMT fusion algorithm and demon tables | [06](06-summoner-minion-fusion-rpg.md) |
| Path of Building `Minions.lua` / `Misc.lua` (generated from GGG data) | Minion life and damage curves | [06](06-summoner-minion-fusion-rpg.md) |
| `pret/pokeemerald` disassembly | Obedience formula, IV inheritance | [06](06-summoner-minion-fusion-rpg.md) |
| Riot's own TFT export (`communitydragon`, 24 MB) | Trait tables, breakpoints, magnitudes | [07](07-rts-and-autobattler.md) |
| Darkest Dungeon MediaWiki API, 110 skill records | Rank legality masks | [07](07-rts-and-autobattler.md) |
| `aoe2techtree.net/data/data.json` | Age costs, per-civ tech and unit spreads | [07](07-rts-and-autobattler.md) |
| Blizzard first-party (Arreat Summit, `classic.battle.net`, patch notes) | D2 skill tables, WC3 heroes and upkeep, SC2 co-op | [06](06-summoner-minion-fusion-rpg.md), [07](07-rts-and-autobattler.md) |
| Warner Bros patent US10926179B2 | The Nemesis System, as filed | [06](06-summoner-minion-fusion-rpg.md) |
| RePoE / `default_monster_stats`, published D3 and D4 scaling tables | Endless curve mathematics | [08](08-endless-scaling-meta-progression.md) |

Numbers marked **(computed)** are tallies over primary data, not quotes.

---

## The files

| File | What it answers |
|---|---|
| [01-pvz2-international.md](01-pvz2-international.md) | **What is PvZ2 actually made of?** Plant Food, seed-packet levels and Mastery, the sun curve, per-world tile gimmicks, level objectives, Endless Zones / Arena / Penny's Pursuit, the zombie attribute frame |
| [02-pvz2-chinese-and-fusion.md](02-pvz2-chinese-and-fusion.md) | **The host game, read from its own binaries.** PVZ Fusion's fusion API, tree structure, rarity enums and difficulty ladder; plus Chinese PvZ2's five-tier plant progression and PvZ Online's awakening system |
| [03-pvz2-mods.md](03-pvz2-mods.md) | **What modders build when nobody is stopping them.** Twelve-project feature matrix, the toolchain's hardcode wall, what recurs across independent teams, what mods reliably fail at |
| [04-pvz-franchise-siblings.md](04-pvz-franchise-siblings.md) | **Every other attempt to add depth to the lawn.** PvZ 1's constants, PvZ Heroes' full keyword and rarity analysis over 503 cards, Garden Warfare's variant ratio, what survived into later titles |
| [05-tower-defense-genre.md](05-tower-defense-genre.md) | **The TD mechanics vocabulary.** Upgrade topologies and their authoring leverage, placement scarcity as the real difficulty dial, enemy field lists, endless formulas, roguelite TD |
| [06-summoner-minion-fusion-rpg.md](06-summoner-minion-fusion-rpg.md) | **This project's own genre.** The SMT fusion algorithm in full, fusion input→output across nine games, how minion power derives from summoner power, loyalty systems and why they get deleted, upkeep, roster caps |
| [07-rts-and-autobattler.md](07-rts-and-autobattler.md) | **Commanders and synergy arithmetic.** SC2 co-op commanders, TFT trait breakpoints from Riot's export, aura failure modes, supply as a currency, Darkest Dungeon rank legality |
| [08-endless-scaling-meta-progression.md](08-endless-scaling-meta-progression.md) | **How endless is actually built, and how it breaks.** Real curve tables, integer inflation and squishes, prestige formulas, endless-via-multiplier vs endless-via-rules, soft-cap algebra |
| [09-feature-comparison.md](09-feature-comparison.md) | **The synthesis.** Feature-by-feature comparison across PvZ 1 / PvZ2 / PvZ2C / the mod scene / the host game / TD / RPG / RTS, with the cross-file agreements and contradictions |

---

## The seven findings that mattered most

1. **The absence of RPG layers in the PvZ2 mod scene is weak evidence about player demand and strong
   evidence about tooling.** Plant behaviour is compiled into `libPVZ2.so`, so resource modding can only
   re-point data at behaviours the binary already has. Across twelve projects: explicit stat channels
   **1/12**, elemental matrix **0/12**. **The moment a team escapes that engine — PvZ Fusion,
   Gardendless — an RPG layer appears within two major versions.** [03](03-pvz2-mods.md) §5
2. **Difficulty is bought with the player's economy, not the enemy's statline.** PvZ2's Penny's Pursuit
   exposes eight knobs and **six are the player's sun and setup budget**. The host game's own N0–N6
   ladder multiplies spawn count, speed and damage reduction but **deliberately leaves the
   `zombieHealthMultiplier` it already exposes alone.** Two independent sources, same conclusion.
   [01](01-pvz2-international.md) §7, [02](02-pvz2-chinese-and-fusion.md) §6
3. **Rarity buys text, not stats — measured over a full card set.** Across all 503 PvZ Heroes cards,
   stat-per-sun is flat (1.47–2.29) with no trend across six rarities, while mean rules text grows
   monotonically **20 → 70 characters**. This independently reproduces the finding already in
   [`game-design/03-roster-scale.md`](../game-design/03-roster-scale.md) from inside the franchise.
   [04](04-pvz-franchise-siblings.md) §2
4. **Fusion at scale is a table that picks the family and a rule that picks the individual** — and
   authored recipes always beat computed ones in precedence. SMT's algorithm only works because
   `(race, level)` is a primary key: **zero collisions across all five games' demon tables (computed).**
   [06](06-summoner-minion-fusion-rpg.md) §1, §3
5. **Enemy durability outgrows enemy lethality in every endless game examined.** Diablo III Greater
   Rifts diverge by a factor of **5.15 million** over 149 tiers; Path of Exile's `×352` damage growth
   over 99 levels independently matches the figure already recorded in
   [`game-design/03-roster-scale.md`](../game-design/03-roster-scale.md). The counter-case, Last Epoch,
   scales both together and its community thread is titled *"how high corruption removes the desire to
   play."* [08](08-endless-scaling-meta-progression.md) §1
6. **`X/(X+K)` is the soft cap that needs no cap.** Four independent shipped instances. The algebra is
   why: `EHP = HP × (1 + X/K)` — **the displayed percentage diminishes forever while survivability stays
   exactly linear and unbounded.** [08](08-endless-scaling-meta-progression.md) §5
7. **Every decaying-meter loyalty system in the survey was deleted by its own studio**, and WoW's patch
   note gives the reason away: removing the chore granted everyone the *top* of the band, so it had
   never been a bonus — it was a tax on inattention. Surviving agency systems express agency as an
   **unlock**, never as runtime unreliability. [06](06-summoner-minion-fusion-rpg.md) §6

---

## Three things this research is *not*

**It is not a spec, and it is not a proposal.** Nothing here says what this game should do. Each file's
"Hooks for this project" section is explicitly marked non-normative and un-vetted; it names mechanics,
it does not design. [09-feature-comparison.md](09-feature-comparison.md) draws comparisons, not
conclusions about what to build. Design work still goes through
[`docs/DESIGN-GATE.md`](../../DESIGN-GATE.md).

**It is not evenly sourced.** Fandom returned HTTP 402 to every request across all eight passes; Reddit,
Discord archives, Moegirl, Zhihu and Baidu Baike were unreachable throughout. Where a claim rests on a
wiki or on TV Tropes it says so inline. **[02](02-pvz2-chinese-and-fusion.md) is the best-sourced file
in the set** because it read the game's own binaries; **[03](03-pvz2-mods.md) is the weakest** because
the mod scene lives on Discord.

**It is not complete on designer intent.** Consistent with the earlier
[`game-design/`](../game-design/README.md) passes: studios almost never publish design rationale.
The single first-party design-rationale document found across all eight passes was Blizzard's page on
Warcraft III's upkeep system. Expect to reason from shipped data.
