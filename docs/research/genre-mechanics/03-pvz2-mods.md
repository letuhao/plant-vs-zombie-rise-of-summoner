# The PvZ2 modding scene — what modders build when nobody is selling anything

Research note. Evidence only — no proposals, no design.
Compiled 2026-09-02.

## The finding in one paragraph

**Free of a publisher, PvZ2 modders overwhelmingly build the same four things, and an RPG stat layer is not
one of them.** Every serious mod ships (1) a full plant-by-plant rebalance with written justifications,
(2) new authored worlds and hundreds of hand-made levels, (3) a roguelite endless mode with a
draft-a-card loop, and (4) the deletion of the monetisation layer — gems, arenas, mints, tournaments and
timers get cut, not tuned. What almost nobody builds is stats, equipment, elements or rarity, and the reason
is mechanical rather than aesthetic: **PvZ2's plant behaviour is compiled into `libPVZ2.so`, so resource
modding can only re-point data at behaviours the binary already has**
([TwinStar's modding guide](https://github.com/apples1949/PvZ2ModdingGuide/blob/master/document/english.md)).
The two exceptions prove it — the mods that *did* add real progression either patched the binary
(`Project Paradox` injects `LawnMod.so` and hooks the plant-id table) or abandoned PvZ2's engine entirely
(PvZ Fusion is a Unity fan game; PvZ2 Gardendless is a from-scratch reimplementation with a datapack
system). Where an RPG layer does appear it is remarkably consistent in shape: a small closed set of
multiplicative stat modifiers drafted between levels (crit chance, crit damage, attack speed, max health,
damage reduction, plus "for every X of stat A gain Y of stat B" derived rules), a rarity tier that scales
both the *cost* and the *magnitude* of upgrades, and a run-scoped meta-currency. The community's own
verdict on the whole scene is captured in two TV Tropes entries: Reflourished is loved as *"The Better
Vanilla PvZ2"* and ECLISE — the mod that made overhauls mainstream — was blindly recommended to casual
players as *"The Better PvZ2 that EA couldn't deliver"* and then resented for being a veteran mod.
**Balance ambition is not what kills these projects. Solo maintainership is.**

---

## 0. How to read this, and what the sources are worth

| Tier | What it is | Marked as |
|---|---|---|
| **A — primary** | Mod team's own docs, tool authors' docs, GitHub repos, official mod websites, Internet Archive item metadata written by the uploader/creator | (primary) |
| **B — mod wiki** | Fandom / wiki.gg / Miraheze wikis run by each mod's community. Often reproduce dev changelogs and dev comments verbatim, which makes those passages near-primary; everything else is player-written. | **second-tier** |
| **C — fan encyclopedia** | TV Tropes work pages and YMMV pages. Useful for *reception* and for "what does the community argue about". Not a source for numbers. | **third-tier** |
| **D — aggregator / SEO mirror** | `pvzmods.com`, `modyolo`, APK mirror sites. They restate mod features but are not connected to the teams. Used only where nothing better exists, and flagged. | **low-trust** |

Numbers I counted myself out of a wiki page are marked **(computed)** and I say what I counted.

**This file deliberately does not re-cover** the Chinese official PvZ2 progression ladder or PVZRH/PvZ Fusion's
internals — those are already recorded with tier-A binary evidence in
[`02-pvz2-chinese-and-fusion.md`](02-pvz2-chinese-and-fusion.md), and the international game's own economy is in
[`01-pvz2-international.md`](01-pvz2-international.md). What is here is the *mod scene* around them, plus the
handful of facts about the Chinese official game that only matter because mods port from it or react to it.

**Access caveat, stated up front.** Reddit was unreachable from this session, Discord archives are closed, and
Fandom blocks direct fetches; Fandom content here was read through a text-extraction proxy. The scene's real
centre of gravity is Discord, and that is unreachable. See "What I could not find".

---

## 1. The feature matrix

This is the centrepiece. **FACT** rows unless marked.

| Mod | Base | Status / released | Design posture | Rebalance | New worlds | Endless / roguelite | Progression layer | Monetisation | Content scale |
|---|---|---|---|---|---|---|---|---|---|
| **Reflourished** | PvZ2 international | Released 2022-10-15, still updating (1.4.x) | "Casual, closest to vanilla" — keeps 50-sun meta, Plant Food, Power-Ups, fast pacing | Near-total, with per-change dev comments published | 4+ original worlds and Epic Realms on top of the 11 vanilla ones | **19 Endless Zones**, draft 1-of-4 cards between levels, persistent lawnmower loss | Zen Garden boosts (gem-priced, per-plant perk), no stat levels | Gems kept as a soft currency; **Arena tournaments, leagues, win streaks and Power Mints cut** | ~190 plants, ~385 zombies (computed) |
| **Project ECLISE** | PvZ2 international | Dev ended May 2023; forks *Solstice* and *3.0* continue | Veteran mod. Long, dense, slow levels | Total, built around the tier system | Reordered vanilla worlds + Modern Day minigame hub + Community World | Endless Zones, Survival, Vasebreaker, I-Zombie, Warp Party | **Plant Tiering — 3 tiers per plant, bought with coins, explicit sidegrades** | None (non-profit); Patreon cosmetics were a scandal | **112 plants**, ~580 Beta level entries (computed) |
| **Alternate UniverZ (AltverZ)** | PvZ2 international | Released 2020-12-01, still updating | Casual, PvZ1 nostalgia. **25-sun meta** | Substantial | Crimson Front + ports of Chinese worlds | Piñata Parties, Timeless Avenue (player levels), The Breakroom, Vasebreaker | None | Removed | ~126 plants, ~322 adventure level ids (computed) |
| **Requiem** | PvZ2 international | **Completed 2024-06-06**, source open to modders | Rework-centric. **25-sun meta** | Drastic reworks of existing plants | 14 worlds incl. Rome Remnant, Time Twister, Encore | Endless zones per world | **Buy plants from a shop to build a loadout** | Removed | 14 worlds |
| **Garden Rush** | PvZ2 international | Released 2023-10-01, development halted | Kingdom Rush pastiche. Fast, small | Towers rebuilt as towers | Needleblight Forest only | — | **"Powers" — free instant plants on their own cooldowns**; only 4 seed slots | Removed | 1 world |
| **Addendum** | PvZ2 international | Released 2025-11-23 | Overhaul of unlock order and level structure | Yes | Restructured vanilla worlds | — | Plant unlock relocation as the progression design | Removed | — |
| **Abscension** | PvZ2 international | Overhaul update 2024-09-19 | Meme mod with a real level set | Light | 12 levels per world (14 for three) | — | — | Removed | — |
| **Fallen** | PvZ2 international | Part 1 complete | Casual-friendly unofficial sequel | Zombie reworks | Crystal Temple, Overleaf Jungle | — | — | Removed | — |
| **PvZ2 Gardendless** | **Reimplementation** (desktop + browser) | v0.13.0, active | Rebuild PvZ2 as an open, moddable platform | N/A | Community-authored levels | Daily Level, Piñata Party | **Experimental plant-level system (clone-per-level)** | None | Community-scaled |
| **PvZ Hybrid Version (杂交版)** | **PvZ1** | v3.3 | Hybrid plants; "only mod officially recognised by the franchise" | Yes | 8 extra brutal worlds | — | Hybrid plants as the ladder | None | — |
| **PvZ Fusion (融合版 / PVZRH)** | **Unity fan game** of PvZ1 | Released 2024-06-14, active | Fusion as the core verb | N/A — its own game | Its own | **Odyssey / Odyssey Purgatory / Abyss — full roguelite with drafted buffs** | **Crit, attack speed, damage reduction, healing, plant upgrade levels, rarity-scaled costs** | None | **532 fusions in v3.9** |
| **Project Skill Tree** | PvZ2 international | Active (low-trust source) | Progression-first | Yes | — | — | **Branching skill tree replaces plant unlocks** | Removed | Unknown |

Sources, in order: Reflourished — [TV Tropes work page](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Reflourished) (third-tier) and the [Reflourished wiki](https://reflourished.fandom.com/wiki/Plants_vs._Zombies:_Reflourished_Wiki) (second-tier);
ECLISE — [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsvsZombies2ECLISE), [ECLISE wiki Plants](https://project-eclise.fandom.com/wiki/Plants), [Plant tiering system](https://project-eclise.fandom.com/wiki/Plant_tiering_system);
AltverZ — [AltverZ wiki](https://altverz.wiki.gg/wiki/Plants_vs._Zombies_2:_Alternate_UniverZ_Wiki), [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2AlternateUniverZ);
Requiem, Garden Rush, Addendum, Abscension, Fallen — TV Tropes work pages
([Requiem](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Requiem),
[Garden Rush](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2GardenRush),
[Addendum](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Addendum),
[Abscension](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Abscension),
[Fallen](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Fallen));
Gardendless — [pvzge.com](https://pvzge.com/en/) (primary);
Hybrid — [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombiesHybridVersion);
Fusion — [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombiesFusion) and
[pvzfusion.wiki.gg/wiki/Fusions](https://pvzfusion.wiki.gg/wiki/Fusions);
Skill Tree — [pvzmods.com](https://pvzmods.com/project-skill-tree/) (**low-trust**).

---

## 2. Mod profiles

### 2.1 Reflourished — the balance-overhaul flagship

**What it is (FACT, third-tier).** A PvZ2 mod by the Reflourished Dev Team, led by **PvZABFan** and **Peamix**,
first released **15 October 2022**. It ships as a **separate APK + OBB**, not a patch over the installed game,
which the community notes makes it "more akin to a Fangame"
([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombies2Reflourished)). The stated pitch
is continuation: *"What if PvZ2 didn't end at Modern Day? What if it kept getting new worlds?"* Its wiki lists
**1,722 pages** ([Reflourished wiki, Inzanity page header](https://reflourished.fandom.com/wiki/Inzanity!),
second-tier). The team's own channel is a Discord (`discord.gg/ba9rC7QdKb`, linked from the wiki main page).

**What it keeps.** The 50-sun meta, Plant Food, Power-Ups, vanilla pacing. This is a deliberate positioning
choice — it is the *conservative* mod, and the community reads it that way.

**What it adds.**

| Addition | Detail | Source |
|---|---|---|
| New worlds | Holiday Mashup, Steam Ages, Caliginous Carnival, plus Epic Realms (Hypothermic Hollows, Assault Airspace, Lunar Rainbow Market, Fairytale Forest); Roman Gardens announced | [Plants list](https://reflourished.fandom.com/wiki/Plants) |
| **Endless Zones** | 19 of them. Limited starting seed bank; between levels you pick **1 of 4 cards** — usually a plant you already own, sometimes spare Plant Food, sun, or a replacement lawnmower. **Lost lawnmowers stay lost across levels.** | [Endless Zone](https://reflourished.fandom.com/wiki/Endless_Zone) |
| **Inzanity!** | A daily set of **five procedurally generated levels**; clearing all five pays **25 gems**. Leaving a level rerolls it. | [Inzanity!](https://reflourished.fandom.com/wiki/Inzanity!) |
| Zen Garden boosts | Not a uniform free Plant Food any more: *"some plants still get the instant plant food effect, but others will have a decreased sun cost, decreased recharge, increased health, or nicher perks like exploding when shoveled."* Priced in gems: **First-Strike = 10, Ultomato = 70, everything else = 5**. | [Zen Garden](https://reflourished.fandom.com/wiki/Zen_Garden) |
| Epic Quests / Premium Quests / Thymed Events | Authored side campaigns. Premium Quests cost gems. Thymed Events became **replayable** in 1.3.3 via a Travel Log "EVENT" tab. | [Changelog 1.3.x](https://reflourished.fandom.com/wiki/Changelogs/Version_1.3.x) |
| **Progression Skip** | On new profile creation the player is offered a skip straight to Modern Day - Day 34, *including all quest rewards*. | Changelog 1.3.3 |
| Chinese-version ports | Steam Ages and Sky City (as "Assault Airspace") ported from PvZ2C, with plants and bosses reworked, not copied | TV Tropes; changelog "PvZ2C Ports" section |

**What it cuts (FACT, third-tier).** Plant Families and Power Mints are gone entirely. The Arena survives only
as a sandbox: *"tournaments, leagues, win streaks, and fighting against another player's top score were all
removed. It also no longer needs gauntlets to be accessed, nor does it give out mints as a reward, leading to
those currencies being cut from the mod as well."* A 1.3.3 setting lets the player **disable Plant Food
purchases and Power-Ups outright**.

**The rebalance method is the most transferable thing here.** Reflourished publishes changelogs that
(a) group plants into *families* and rebalance the family as a unit, and (b) attach a signed **Dev Comment**
to nearly every change explaining intent. Section headings from the 1.3.2 changelog alone: *PvZ2C Ports,
Potato Mines, Lobbing Plants, The 45s Gang, Shadow Plants, Fire Plants, Explosive Plants, Freeze/Stun/Slowdown
Appliers.* Representative entries, verbatim (second-tier host, near-primary content):

> **NEW Cold+Hot Interaction!** — NEW: Zombies that are chilled or frozen now take 1.25x more damage from fire
> attacks (first impact).
> *Dev Comment: We implemented this as a kind of "zombie temperature whiplash" system where going quickly from
> cold to hot deals more damage to them due to the sudden celsius shift. It makes pairing cold and hot plants a
> viable synergy as well as enabling a whole grabbag of new strategies. This also works with all damage labeled
> as "fire", which includes instant plants like Cherry Bombs!*

That is the closest thing in the whole PvZ2 scene to an element-matchup matrix, and it is exactly one cell
wide. **INFERENCE:** they could add one interaction cheaply because "chilled", "frozen" and "fire" already
exist as engine-level tags; a genuinely new element would have needed a new tag, which resource modding
cannot create.

> **Potato Mine family** — Explosion Area 1x1 → 1x3; now deals damage to grid items in range; now stuns
> zombies for 2 seconds. *(Primal Potato Mine 2.5s, Potato Chip 1s.)*
> *Dev Comment: … will now apply stun depending on how much damage each mine deals.*

> **Melon-pult / Winter Melon** — Damage 95 → 85, Splash 45 → 30, Firing Rate 4.0–4.05s → 2.85–3.0s;
> Winter Melon chill 3.44s → 1.3s, recharge 25s → 20s. *Dev Comment: … feedback regarding these two will be
> very appreciated.* Then in 1.3.3: chill **1.3s → 2.35s**, *"This shorter uptime didn't quite do what we had
> hoped."*

> **Gatling Pea** — Sun Cost 400 → 450. *Dev Comment: The 400 sun price tag was too efficient for a plant with
> high damage and a powerful secondary ability … with the added bonus of parity with the sun cost of Gatling
> Pea in PvZ1.*

> **Level change, Far Future - Day 35** — *"This level was infamous for its war of attrition style where you
> needed to chip the Zombot down with Power Tiles while also using them to deal with the overwhelming Zombie
> hoard. We've altered the Plants given now so the fight should be more in-line with others."*

Note the last one: **they retune the level, not the numbers, when a single encounter is the problem.** That
distinction shows up repeatedly in their changelog and it is a real design discipline.

**Content scale (computed).** From the navigation box at the foot of the wiki's
[Plants](https://reflourished.fandom.com/wiki/Plants) page: 214 link labels, of which 24 are section headers →
**≈190 plants**. From the [Zombies](https://reflourished.fandom.com/wiki/Zombies) navbox: **≈385 distinct
zombie names**, spread over 14 worlds plus 15 seasonal "Thymed Event" rosters plus bosses. Wiki trivia states
that as of 1.4.2 only one plant from vanilla 9.6.1 (Power Vine) has not been added, excluding Power Mints.

**INFERENCE.** The seasonal rosters are where the zombie count comes from: each Thymed Event re-skins the
basic/conehead/buckethead/flag quartet and adds four to eight originals. That is the cheapest possible
content multiplier — the behaviour is already written, only art and a name change.

---

### 2.2 Project ECLISE — the one that added a real progression system

**What it is (FACT, third-tier + second-tier).** A PvZ2 overhaul by **goodpea2** and **Mine Power**. Split into
an *Alpha* and a *Beta* line with different difficulty ramps. **Development ended in May 2023** when both leads
stepped down; two forks continue, **Solstice** and **3.0**
([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsvsZombies2ECLISE)).

**The Plant Tiering System** is the single most RPG-shaped mechanic anyone has built inside PvZ2's own engine.
Quoting the mod's own in-game Penny dialogue as reproduced on its wiki (second-tier, near-primary):

> *"Tiers are like trades for plants. Some stats will get better, while some will get worse. Think wisely before
> you decide which plant tier you're gonna get. It's crucial to do so as it can have a great impact on your
> strategies."* — Penny

Mechanics ([Plant tiering system](https://project-eclise.fandom.com/wiki/Plant_tiering_system)):

- Most plants have **3 tiers**. A tier is a **sidegrade**, not an upgrade: it moves Toughness, Attack Damage,
  Recharge and/or Sun Cost in both directions.
- Some tiers unlock behaviour rather than numbers — Sun-shroom's max growth stage 1→2→3, Snow Pea's stall
  ladder *Chill → Splash Chill → Freeze*.
- Tier 2 and 3 are **bought with coins** (from kills, Money Bags, or Marigolds in the Zen Garden). *"A plant
  cannot be tiered down without buying all tiers."*
- After purchase the player may switch tiers freely **up to 3,000 times**, tracked by a "Level" counter on the
  almanac icon.
- **Tiering is entirely optional**; the wiki states the game is beatable without changing a tier.

**INFERENCE, and it matters.** A 3,000-switch counter is not a design choice anyone would make on purpose;
it is almost certainly the profile field ECLISE re-purposed to store the current tier — the vanilla plant
*level* counter, which caps at some engine value. That is what "adding an RPG layer without a binary patch"
actually looks like: you find a persisted integer the game already understands and mean something else by it.

**Level structure (FACT, second-tier).** goodpea2's own quote on the wiki:

> *"ECLISE covers all of PvZ2, it's like a new game now. Levels in ECLISE are usually slow paced, longer and
> crowder than levels in vanilla PvZ2. The difficulty slowly ramps up as the process goes on, I suggest that a
> good tower-defense player can make it pass the first four world without retrying too much. The mod is not
> made for lower skilled players though."*

- Worlds are re-ordered (Ancient Egypt → Lost City → Dark Ages → Pirate Seas → Frostbite Caves → Wild West →
  Big Wave Beach → Jurassic Marsh → Far Future → Neon Mixtape Tour).
- **Every 10 normal levels there is an "impossible" Gate level** that must be beaten to continue.
- **20 Secret levels** in Part 1 grant early-access or exclusive plants; exclusives are *rented* for
  **2,000 coins**.
- Modern Day is repurposed as a **minigame hub**; a **Community World** carries levels made by Discord members.
- **112 plants**, all obtainable without real money: 3 via gems, 8 exclusive to Secret levels, 12 from either
  path, 3 from Epic Quests, 1 from the Zen Garden
  ([ECLISE wiki, Plants](https://project-eclise.fandom.com/wiki/Plants)).
- Level navbox tallies **(computed)**: Beta ≈580 level entries across 11 worlds + Modern Day + Vasebreaker +
  Community World; Alpha ≈303 adventure + ≈120 "extra" night/day-variant levels + ≈128 challenge/quest levels
  + ≈166 special-mode levels. A download mirror claims *"over 400 brand-new levels"*
  ([pvzmods.net](https://pvzmods.net/eclise/), **low-trust**); my counts are of wiki link entries and will
  over-count anchors, so treat both as order-of-magnitude only.

---

### 2.3 Alternate UniverZ — the casual counterweight

**FACT (second-tier + third-tier).** Founded by **ItsPForPea** and **KF4**, now developed by **Poss** with
community help; released **1 December 2020**, still updating; distributed as a separate APK+OBB from a
Google Drive folder ([AltverZ wiki](https://altverz.wiki.gg/wiki/Plants_vs._Zombies_2:_Alternate_UniverZ_Wiki)).

Design identity:

- **25-sun meta** restored from PvZ1 — the single biggest economy change any of these mods makes. It rewards
  stalling the early game to build sun production.
- Every world ends in an **"Ultimate Battle" / "Massive Attack"** — a horde assault on the house — instead of a
  Zomboss fight. Zomboss stays in the story and is only rarely fought.
- **Zombie classes are surfaced to the player**: the wiki's own tips page says *"Tank Class zombies have a ton
  of health. Single-target heavy plants excel against them."* That is an explicit archetype vocabulary, which
  vanilla PvZ2 does not present.
- Structure: 20 regular levels per world — **10 Normal + 10 Extra** (harder) — plus bonus levels. Content
  outside adventure: Piñata Parties, **Timeless Avenue** (levels made by other players), Vasebreaker,
  The Breakroom, Old Noon Flashback.
- **(computed)** ~126 plant entries on the wiki's Plants page; ~322 distinct adventure level ids in the levels
  navbox (world-code patterns like `AE-I…AE-X`, `AEX-I…AEX-X`).
- **Friendly Fandoms with Reflourished** — the two teams ran a crossover event where AltverZ zombies appear in
  Reflourished stages, and a Reflourished splash text recommends AltverZ (third-tier).

---

### 2.4 Requiem, Garden Rush, Addendum — three narrower answers

**Requiem** (FACT, third-tier). By **DT_MP** (formerly Mine Power, ex-ECLISE). Rework-centric; **25-sun meta**;
**14 worlds** — a tutorial world, 10 vanilla worlds each with 4 zombies, a basics-and-imps set, 2 gimmicks,
endless zones and a "Timeline Blocker", plus Rome Remnant, Time Twister and Encore. Its progression idea is the
interesting one: *"a progression system where players are given the freedom of purchasing Plants in the shop to
form unique loadouts."* **Completed 6 June 2024**, and *"its contents are open-sourced for modders, though arts
must be used with permission from the artists."* A successor, *Resonance*, followed.

**INFERENCE.** Requiem is the only PvZ2 mod I found that treats *being finished* as a goal and then hands the
content set to other modders. Everything else is either perpetually updating or abandoned mid-stream.

**Garden Rush** (FACT, third-tier). By **Creeps20**, released **1 October 2023**. A genre re-point rather than
an overhaul: Kingdom Rush pacing, **only four seed slots**, sun-from-sky buffed and Sunflower demoted to a
support option, **no lawnmowers and no Plant Food**, and a new **Powers** mechanic — free instant-use plants on
their own cooldowns, deployable anywhere. Some stages have pre-placed "Guest Plants" that must be protected.
Development halted after the first world.

**Addendum** (FACT, third-tier). By the Addendum Team, a merger of two earlier projects (TheShero's *Nightcast*,
Snowie's *Exploration of Time*), hosted in the Floral Federation community. Released **23 November 2025**. Its
overhaul is almost entirely *where plants unlock*: the Player's House roster is greatly expanded, premium
plants become world plants, world plants move worlds. **INFERENCE:** unlock-order is the cheapest overhaul in
PvZ2 — it is a data edit with no art and no new behaviour — and it changes the felt difficulty curve more than
any single stat change would.

---

### 2.5 PvZ2 Gardendless — the reimplementation, and the only documented plant-level system

**FACT (primary).** `pvzge.com` — *"A completely remastered PvZ2 for all desktop platforms"*, by **Gaozih**,
playable in a browser at `play.pvzge.com`, currently **v0.13.0** with build name
`PvZ2_Prerelease_PinataParty_2` ([download page](https://pvzge.com/en/download/)). Not a mod of PvZ2's binary —
a rebuild — which is why it can expose a mod API at all.

Its modding surface, **GP-Next 1.4.6**, is the most explicit public description of a PvZ2-shaped data model:

- Datapacks with `pack.json`, documented **merge rules**, language packs, level patches, a world-map schema,
  and runtime extensions ([MOD guide](https://pvzge.com/en/guide/mod/)).
- Object model mirrors PopCap's: `PlantFeatures.json` (`ID`, multilingual `NAME`, `CODENAME`, `TYPE` tags such
  as `["plant","lastStandDisallowed"]`, `OBTAINWORLD`, `ZENGARDEN`, `COSTUME`), `PlantAlmanac.json`
  (`objclass: PlantAlmanacProperties`), `PlantProps.json` (`objclass: PlantProperties`, with
  `Damage`, `Cooldown`, `CooldownFrom`, `SunCost`, `Toughness`, `Family`, and boolean immunities like
  `ImmuneToIceblock`, `CannotBeSheepenedByWizard`)
  ([Types & Fields](https://pvzge.com/en/guide/mod/format.html)).
- Levels are JSON/JSON5 files, *"similar to the original version"*, with extra descriptive fields
  ([Level Files](https://pvzge.com/en/guide/level/levelguide.html)).

**The plant-level system** ([Plant Levels](https://pvzge.com/en/guide/mod/gp-next-plant-level.html)) is
labelled *Experimental* and its design is instructive:

> The core idea is not to mutate the vanilla plant directly. Instead, you: choose one **base plant**; prepare
> one **clone plant** for each level; bind `base plant ↔ level clones` through
> `jsons/extensions/plant-levels.json`.
> This is useful because: each level can have its own `PlantProps / PlantAlmanac / PlantTypes`; badges,
> almanac level pages, and runtime card replacement all use the same mapping; no modification of game build
> output is required.

And the honest limitation, quoted in full because it is the most useful sentence in the whole scene:

> You should not treat it as a full recreation of the original PvZ2 upgrade economy yet. In particular, these
> parts are still evolving: **upgrade resource sources; full upgrade economy loop; more complete progression UI.**

**INFERENCE.** Clone-per-level is a table of complete stat blocks, not a formula. It is trivially authorable and
trivially debuggable, and it scales badly — N plants × M levels rows, each independently balanceable and
independently wrong. The team shipped the *representation* and explicitly has not shipped the *economy*, which
is a fair summary of how hard the economy is relative to the numbers.

---

### 2.6 PvZ Fusion and PvZ Hybrid — the fan games that escaped the engine

Full internals for PVZRH/PvZ Fusion are in [`02-pvz2-chinese-and-fusion.md`](02-pvz2-chinese-and-fusion.md) §3
with tier-A binary evidence. Recorded here only as **peer-group evidence**: this is what a PvZ modder builds
when the binary is no longer a wall.

**PvZ Fusion (FACT).** A **Unity-based fan game of PvZ1** by Chinese developer **Blue Fly / 蓝飘飘fly**, first
released **14 June 2024** ([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombiesFusion)),
distributed through a Discord (`discord.gg/pvzfusion`) with Internet Archive mirrors uploaded explicitly as
lost-media insurance ([IA item metadata for v2.1.4](https://archive.org/metadata/pvz-super-hybrid-rh-v2.1.4),
creator field `蓝飘飘fly`, primary).

Systems it has that no engine-bound PvZ2 mod has:

| System | Detail | Source |
|---|---|---|
| **Fusion tree** | **532 fusions in v3.9** (514 excluding Infusible): 323 Common (2 base plants), 77 Upgraded, 12 Advanced (3 base plants, gated behind a Fusion Challenge), 102 Odyssey (2 fusions + the Glove), 18 Infusible (no formula — only spawned by other fusions), plus Titan | [Fusions](https://pvzfusion.wiki.gg/wiki/Fusions) |
| **Rarity classes** | Normal / Common / Upgraded / Advanced / Extra / Magic / Odyssey, with Odyssey subclassed into Legendary / Strong (Epic) / Regular / Super | [Odyssey](https://pvzfusion.wiki.gg/wiki/Odyssey) |
| **Difficulty as global multipliers** | Easy = vanilla spawns; Casual ×3; Normal ×5; Veteran ×7; Merciless ×10 **plus 30% zombie damage resistance and +10% speed**; "Are You Sure?" ×10 with **60% damage reduction, +20% speed and hypnotise/charm effectiveness halved**; Skins Challenge = +40% zombie HP, wave interval 15s→3s, natural spawn interval 30s→10s | [Mechanics](https://pvz-fusion.fandom.com/wiki/Mechanics) |
| **In-match economy** | Marigolds produce coins; **1,000 coins → 1 Coffee Bean**; Coffee Beans upgrade silver plants to gold and pay for gold plants' **Ultimate** attacks; every 500 coins spent returns 25 sun. Coins do not persist between matches. | [Mechanics](https://pvz-fusion.fandom.com/wiki/Mechanics) |
| **Abyss Mode** (v2.4+) | Seasonal, 30 levels, meta-currency **Chlorophyll** (start with 2,000). Three shops: Modifiers, Skins, Plant Upgrade. Progress resets every 2–3 major updates. | [Abyss Mode](https://pvz-fusion.fandom.com/wiki/Abyss_Mode) |
| **Abyss Modifiers** | Buy/draft stat modifiers before each level; **modifier cap rises by +3 per level cleared**; a **backpack** stores over-cap modifiers; **5 rerolls** per new clear, 7 per repeat clear; stronger modifiers unlock after level 5; modifiers can be **sold back** for Chlorophyll | ibid. |
| **Abyss Plant Upgrade** | 3 levels per plant. **L1 +150% damage / +75% max HP; L2 +300% / +150%; L3 +900% / +225%.** Odyssey plants receive only **5%** of that; Advanced plants **30%**. Cost by rarity: Normal 50/150/300, mid-tiers 150/450/900, Odyssey 1,500/4,500/9,000 Chlorophyll. Gated on clearing Abyss levels **5 / 15 / 25**. | ibid. |
| **Odyssey / Purgatory roguelite** | 21 escalating levels. Purgatory forces a **zombie buff** pick after every plant buff, and adds a Purgatory Zombie every three levels. **Cursed Mode** adds: no upgrade above 15,000 points, plants take 2× damage, healing −30%, zombies take a further 50% less damage and scale HP faster, and you may take up to two plant buffs but must take a zombie buff for each. | [TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/PlantsVsZombiesFusion) |

The Abyss modifier list is the closest published analogue to a derived-stat channel set. Verbatim examples
(second-tier): *Physical Attack +25% damage (100 Chlorophyll); Physical Defense +30% max HP (50); Critical
Attack +10% crit chance (200); Critical Shot +70% crit damage (200); Haste +25% attack speed (100); Beserker I
"every 10% of current HP missing increases Damage by +30%" (150); Beserker II same for Damage Reduction +8%
(200); Natural Healing 300 toughness to all plants every 15s (100); Discount −10 sun on all plants (100);
Wealthy +150 starting sun (50); Andrealine "for every 150 toughness recovered, +30% damage" (300); Swift Energy
"for every 12.5% attack speed added, +15% damage" (300); Strength Stone I "for every 10% crit chance added,
+45% damage" (300); Strength Stone II "for every 100% damage added, +25% damage" (300); Strength Stone III "for
every 1% crit chance added, +3% crit damage" (2,000); Stone of Sun "for every 300 sun consumed, reduce sun cost
by 5" (300); Raging Firepower "infinite attack speed, every shot costs 1% max HP" (3,000).*

**Three things to notice.** (1) The expensive modifiers are the **cross-stat conversion** ones — stat A feeding
stat B — not the flat ones. (2) There is a hard **5,000 damage-per-hit cap** on Purgatory Zombies (third-tier),
i.e. the game answers unbounded multiplicative stacking with a per-hit ceiling rather than a stat ceiling.
(3) Rarity scales the *magnitude* of a buff downward (Odyssey gets 5%) as well as its cost upward — a
double-brake on top-rarity power creep.

**PvZ Hybrid Version 杂交版 (FACT, third-tier).** A **PvZ1** mod whose core is hybrid plants and hybrid/armour
zombies. TV Tropes states it *"currently holds the distinction of being the only mod officially recognized by
the Plants vs. Zombies franchise."* Current version 3.3. It has eight hidden **Brutal Bonus** worlds reached by
clicking left from world 1, each with its own unlockable plants. Cross-pollination with Fusion is explicit: a
Hybrid plant (Hamburger Shooter) appears in Fusion and a Fusion plant (Phoenix Threepeater) appears in Hybrid,
with the *same* status effect implemented differently in each — Fusion's *Enflamed* is a damage-taken debuff
plus a death explosion that spreads, Hybrid's is damage-over-time scaling with the zombie's max HP.

**INFERENCE.** Two independent teams took the same named status and gave it two different formulas without
either being "wrong". Status semantics are a per-game contract, not a genre constant.

---

### 2.7 Project Skill Tree — the one explicitly RPG mod I could verify only weakly

**Low-trust.** [pvzmods.com](https://pvzmods.com/project-skill-tree/) (an aggregator, not the team) describes a
PvZ2 mod by **Stuff26** that *"transforms the traditional plant unlocking mechanics"* into a skill tree with
branching paths, plant stat upgrades (Sunflower sun output, Peashooter fire rate and damage) and reworked
zombie behaviour. I could not find the team's own site, wiki or repo, and I could not confirm any of the
mechanics independently. Recorded because the category matters, not because the source is good.

---

## 3. The modding toolchain — where the cheap design space is

This section is the reason §5's tally looks the way it does.

### 3.1 The two modding routes, and the wall between them

**FACT (primary).** From the PvZ-2 MOD Technical Overview written by **TwinStar** (author of SPC-Util, Taiji,
PvZTool and TwinStar.ToolKit), mirrored at
[apples1949/PvZ2ModdingGuide](https://github.com/apples1949/PvZ2ModdingGuide/blob/master/document/english.md):

> Depending on the modification target, the game's modification routes are divided into the following two
> categories:
> - **Modification program**: Modify the main body program of the game (i.e. APK, especially the DEX and SO in
>   it), which requires high technical requirements … **can customize the operation logic of the game and can
>   make any effect in theory.**
> - **Resource modification**: modify the main resource data of the game … requires low skills of the modifier
>   … and **can only be modified under the rules set by the program itself, with having a lot of certain
>   limitations due to the hardcode.**

`libPVZ2.so` (arm64 and armeabi-v7a) is *"the compiled product of the game's main source code"*. The guide is
also blunt that source-level modding does not exist: *"no third party can modify the source code of the game,
only the compiled the product is modified."*

**What "hardcode" costs, concretely.** Two worked examples from the same document:

- **0-sun / no-cooldown mod**: done by *removing the `Cost` and `PacketCooldown` string constants from the SO*
  so RTON deserialisation into `RtObject` never finds them and falls back to `0`.
- **`Project Paradox`**: ships a self-compiled **`LawnMod.so`**, patches the DEX to force-load it, and from its
  init hooks *"the snippet of `libPVZ2.so` about the amount of plant ids code … and then defines an additional
  plant ids code, thus adding a custom plant ids code to the game, allowing the player to get and keep the
  custom plants of the MOD in the archive."* TwinStar's own verdict: *"The principle of the mod is nothing
  special … the hardest part is to write the HOOK logic."*

**INFERENCE, and the single most relevant fact in this file for this project.** `Project Paradox` is the same
architecture this repo uses — a side-loaded library that hooks the host game's functions to extend a table the
host owns — arrived at independently, on a different engine, for the same reason: **the host game's data model
is closed, and hooking is the only way to widen it.** The PvZ2 scene has exactly one such project and treats it
as exotic. That is a statement about the scarcity of the skill, not about the soundness of the approach.

### 3.2 The file formats, and what each one gates

**FACT (primary — TwinStar guide, plus the [ErnestoAM PvZ2 Hacking Guide](https://ernestoam.fandom.com/wiki/Plants_vs._Zombies_2_Hacking_Guide), second-tier).**

| Format | What it is | What it gates | Cost to a modder |
|---|---|---|---|
| **RSB / RSG** (`.obb` is an `.rsb`) | Resource Stream Bundle / Group. One RSB per game; RSGs are embedded, never standalone. Version 4 in both international and Chinese builds. | All art, animation, audio, text and data | Unpack/repack whole bundle; damage is easy and needs "RSB Repair" |
| **RTON** | *ReflecTion Object Notation* — binary serialisation of `Sexy::RtObject` from PopCap's SexyFramework. Structure: `version`, `objects[]`, each with `uid`, `aliases`, `objclass`, `objdata`, referenced as `RTID(uid@file)` or `RTID(alias@file)`. **Chinese-version RTONs are additionally Rijndael-encrypted**, key must be extracted from the binary. | Every gameplay value, level, plant/zombie property, almanac string | Decode → edit JSON → re-encode. **Cheap.** |
| **PTX** | PopCap texture. Stores raw pixels only; width, height and format live in the RSB manifest. ~24 format codes including `rgb_etc1_a_8` (international) vs `rgb_etc1_a_palette` (Chinese). | All sprites | Moderate — must supply dimensions and format |
| **PAM** | Frame-by-frame animation, v6 in both regions. Converted to XFL for editing in Flash-lineage tools. | Every plant/zombie animation | **Expensive** — the real bottleneck for new units |
| **POPFX / WEM / BNK** | Particle effects; Wwise audio and sound banks | VFX and sound | Moderate |
| **`pp.dat` / `local_profile`** | Player profile and local config — RTONs without the extension, stored in the app's external data dir (Chinese version puts them in `data/data`, needing root) | Save state, unlocks, currencies | Cheap; this is where save-editors operate |
| **`CDN.<major>.<minor>`** | Server-pushed files that **overwrite the corresponding `Packages` entries at runtime** | Live levels and events | Cheap, and the reason many level tutorials edit `.../No_Backup/CDN.x.y/levels` directly |

Level files are RTON/JSON with a `Modules` array of `RTID(...)` references. The community convention, from the
oldest surviving tutorial ([Systempaw72, 2017-07-01](https://plantsvszombies.wiki.gg/wiki/User_blog:Systempaw72/JSON_editing_-_Part_1:_Simple_Level),
second-tier): *"`@LevelModules` means you can't do anything in it. `@CurrentLevel` means that you need to setup
it by yourself."* — i.e. some modules are engine-owned singletons and some are per-level and authorable.

One hard limit worth recording verbatim from the OBB Packages guide (second-tier): *"You can change counts to
anything you please, but **do not go beyond 2,147,483,647, or the game will overflow and numbers will go
negative.**"* The scene knows about 32-bit overflow because it has hit it.

Editable plant fields documented on the same page: `HomeWorld`, `AlmanacBackdropName`, `Premium`, `Enabled`,
`HideInPlantViewers`, `DenyPlantfoodCooldownReset`, `CannotBeImitated` — and collectible-item fields
`ExpireDuration`, `CoinValue`, `GemValue`, `ContentsType`, `ContentsCount`, `SunValue`. **That list is the whole
design space of a resource-only mod's economy layer.**

### 3.3 The tools

**FACT (second-tier — [ErnestoAM Hacking Tools](https://ernestoam.fandom.com/wiki/Plants_vs._Zombies_2_Hacking_Tools)).**
Active and current: **Sen** (Haruma, [senharuma.com](https://senharuma.com/)) for resources/atlases/PAM;
**TwinStar.ToolKit** ([repo](https://github.com/twinkles-twinstar/TwinStar.ToolKit)) for encode/decode of all
formats; **PyVZ2** (Nineteendo) for fast OBB/RSB patching and RTON conversion; **RETON** (h3x4n1um) for
RTON↔JSON. **No longer supported**: Taiji, SPCUtil, PopStudio, PvZ2Tool. Level authoring: **ELM**, a Google
Sheets level maker; **pvz2-level-maker** by 2001zhaozhao, a Scala/JAR wave editor
([repo](https://github.com/2001zhaozhao/pvz2-level-maker)); Gardendless ships its own browser
[level editor](https://pvzge.com/en/useful-tool/level-editor.html). There is a
[PvZ2LibraryAnalyzer](https://github.com/twinkles-twinstar/TwinStar.ToolKit.PvZ2LibraryAnalyzer) that parses
`libPVZ2.so` to emit the **RTON class property specification** — i.e. the schema is recovered from the binary,
not published.

**INFERENCE.** Every "no longer supported" entry in that list is a solo project. The toolchain has the same
bus-factor problem as the mods.

### 3.4 Where the scene actually lives

**FACT.** GameBanana's PvZ2 game page returns **21 total submissions**
([GameBanana API, game 6114](https://gamebanana.com/apiv11/Game/6114/Subfeed)) — for a scene with mods carrying
hundreds of levels each. The real distribution channels are Discord invites, Google Drive/MEGA folders, and
Internet Archive mirrors uploaded by third parties. Archive.org download counts as a reach proxy
([advanced search](https://archive.org/advancedsearch.php), primary):

| Archived item | Downloads |
|---|---|
| Project Eclise Beta | 14,266 |
| PvZ2 Mod: Requiem | 12,466 |
| PvZ2 Mod: AltVerZ (Updated) | 12,236 |
| PvZ 2 PAK | 5,332 |
| Project Eclise Alpha 3 | 5,235 |
| Reflourished 11/2/22 | 1,036 |
| PvZ Fusion Fanmade Android English (Nvdtn19) | 29,519 |
| PvZ Fusion 3.0.1 Multi-Lang (Blooms) | 26,963 |

An uploader's own note on the Fusion mirror (primary) is the clearest statement of the scene's norms:

> *"NOTE: This and different versions of PvZ Hybrid/Fusion mod uploaded here are duplicates, only for archive
> purposes to avoid becoming lost media. I do not claim ownership … You must visit the official 'PvZ: Fusion
> Fans Official Discord' discord server to get the latest and truest versions."*

**INFERENCE.** Archive counts under-report badly — they are mirrors of the *unofficial* channel. But the
*relative* ordering is informative: the Chinese-origin fan games out-download the English PvZ2 overhauls by
roughly 2:1 even on English-language mirrors.

---

## 4. The system the mods are arguing with — official PvZ2C

Recorded briefly because half the mod scene's design decisions are reactions to it, and because it is the only
lawn RPG layer that was ever shipped commercially. Cross-reference
[`02-pvz2-chinese-and-fusion.md`](02-pvz2-chinese-and-fusion.md) §1 for the tier ladder itself.

**FACT (second-tier — [PvZ2 Chinese version](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_2_(Chinese_version))).**

- **Plants and zombies both have levels.** *"the higher ranking a plant, the more enhanced abilities it has;
  and the higher ranking the zombie, the more health and attack power it has."* Zombie level in hard mode is
  keyed to world order.
- Upgrading is gated on **Puzzle Pieces + coins**
  ([Plant upgrade system](https://plantsvszombies.wiki.gg/wiki/Plant_upgrade_system_(Chinese_version))):
  L1 = 10 pieces; L2 = 30 pieces + 50,000 coins; L3 = 50 + 100,000; L4 = 50 + 200,000; L5 = 80 + 500,000 **plus
  10 units of Culture Medium of the matching rarity**. Level 4 grants a percentage chance to fire Plant Food
  for free.
- **It is not optional.** *"Upgrading plants are required for hard mode levels as non-upgraded plants will fail
  to take care of the most basic zombies … a level 1 Chomper cannot kill a level 4 Conehead Zombie by itself."*
- **Plant rarity is a colour** — White / Green / Blue / Purple / Orange — and it sets the currency: Penny's
  Store sells White/Green pieces for 5,000–10,000 coins and Blue/Purple for 15–30 gems; **Orange plants never
  appear in the store.**
- **Pendants (挂件, internal name `Accessory`) are equipment**
  ([Pendant](https://plantsvszombies.wiki.gg/wiki/Pendant), added in 1.4.0). Each has four quality steps —
  Damaged / Basic / Advanced / Universal — and the step scales one number: Medical Box heals
  **50 / 90 / 140 / 200 HP every 3 seconds**; Bucket gives **+5% defense** at Damaged and more above. Obtained
  by collecting 10 puzzle pieces (15 for Super Pendants) for coins.
- **Plant families** have their own level; a family levels up as you own more of its plants, which increases the
  power of later family upgrades. Three free family upgrades per day, gems after that, **100 gems to unlock an
  extra upgrade slot whose upgrades are stronger**.
- Loot boxes: Common / Rare / Legendary / Costume chests, gem-priced, with one free Ordinary chest daily and
  free Rare/Costume every three days.

**INFERENCE.** Every mod in §1 removes some part of this, and the parts they remove are exactly the parts with a
gem price attached — not the parts with a stat attached. Reflourished kept gems and a store; it cut tournaments
and mints. ECLISE kept coins as the tiering currency. **The scene's objection is to the timer-and-purchase
loop, not to progression as such.** That is a distinction worth holding onto, because it is easy to read the
mods as anti-RPG when they are anti-storefront.

---

## 5. What recurs across independent teams

Tally over the twelve projects in §1. A tick means the mechanic is present in a form the mod's own docs name.
Every mechanic gets the three questions the brief asks.

| Mechanic | Teams that built it | What it solves for the player | What it costs the modder | What breaks when it is tuned wrong |
|---|---|---|---|---|
| **Delete the monetisation layer** | 10 / 12 (all PvZ2 mods) | Removes the "wait or pay" wall; makes the whole roster reachable | Near-zero — it is deletion, plus re-sourcing whatever the cut currency used to gate | Rewards lose meaning; if gems still exist but nothing costs them, the shop UI becomes dead weight (Reflourished kept gems and had to re-price Zen Garden boosts to keep them a sink) |
| **Whole-roster rebalance with published rationale** | 8 / 12 | Makes more than five plants viable; the meta stops being one loadout | Very high **and permanent** — every new zombie re-opens every plant | Two named failure modes below (§6): overcorrection (Pokra) and the untouchable outlier (Winter Melon) |
| **Roguelite endless mode with a between-level draft** | 7 / 12 (Reflourished Endless Zones, ECLISE Endless Zones, Fusion Odyssey/Abyss, AltverZ bonus content, Requiem endless zones) | Infinite content from finite assets; a reason to keep the game installed after the campaign | Low per-run, high once: needs a run-state container, a draft UI and a reward table | If the draft pool is not gated by run progress the first pick decides the run; Fusion gates strong modifiers behind clearing level 5 |
| **Authored new worlds with a tile gimmick** | 8 / 12 | Novelty; a reason for a new plant to exist | **Highest cost in the scene** — PAM animation is the bottleneck, not design | A gimmick that needs one specific counter becomes a plant tax; Reflourished cut Groundcherry, Ampthurium and Flat-shroom precisely because *"both primarily served as extremely specific counters to a single mechanic"* |
| **Port from the Chinese version** | 5 / 12 (Reflourished, AltverZ, Abscension, Requiem via ECLISE, Fusion) | A whole world of content already animated | Moderate — art exists, mechanics often do not survive | Reflourished's Fairytale Forest port had to tone the Magic Fog down; the original is described as *"one of the worst worlds of its version, due to being filled with overpowered zombies and mechanics, atrocious level design and balancing"* |
| **Change the sun meta** | 3 / 12 (AltverZ 25-sun, Requiem 25-sun, Garden Rush sun-from-sky) | Re-times the whole match; makes early game a decision again | Cheap to change, ruinous to validate — every level must be re-tested | Every plant's sun cost is now mis-priced simultaneously; this is the single most expensive "cheap" change |
| **Player-made / procedural level content** | 4 / 12 (ECLISE Community World, AltverZ Timeless Avenue, Gardendless Daily Level, Reflourished Inzanity) | Content the team did not author | Low ongoing, moderate once (a level schema and a submission pipeline) | Procedural levels are unbalanceable by hand; Reflourished's Inzanity is explicitly a *daily* throwaway with a fixed 25-gem payout rather than part of the progression spine |
| **Per-unit progression (levels/tiers/upgrades)** | **3 / 12** — ECLISE tiers, Fusion Abyss upgrades, Gardendless clone-levels (experimental) | A long-term sink; personalisation | High, and the engine fights you unless you own the engine | Makes the campaign either trivial or mandatory-grind; ECLISE's answer was to make it a **sidegrade** and **entirely optional** |
| **Explicit stat channels (crit, attack speed, DR, healing)** | **1 / 12** — PvZ Fusion only | Build variety without new units | Requires owning the combat pipeline | Cross-stat conversion rules stack multiplicatively; Fusion answers with a per-hit damage cap rather than a stat cap |
| **Rarity that changes rules, not just colour** | **2 / 12** — Fusion (upgrade cost ×30 and buff magnitude ÷20 for Odyssey; planting-slot budgets) and PvZ2C officially (store availability by colour) | Makes a collection screen mean something in play | Every unit needs an honest rarity, in every mode that reads it | Mis-rate one unit and it is either priced out of use or a free win |
| **Equipment / attachments** | **1 / 12** — PvZ2C Pendants (official, not a mod) | Per-plant customisation without touching the plant | Needs an item layer and a UI | Four quality steps of one linear number is barely a system; it is a stat multiplier with an inventory screen |
| **Elemental matchup matrix** | **0 / 12** | — | — | The nearest thing anyone shipped is Reflourished's single cold→fire 1.25× rule |
| **Skill tree** | **1 / 12** — Project Skill Tree (unverified) | — | — | — |

**The signal, stated plainly.** Where every team converges — rebalance, worlds, endless draft, no storefront —
is where the engine is permissive and the player demand is obvious. Where almost nobody goes — stats,
elements, equipment, trees — is exactly where `libPVZ2.so` says no. **INFERENCE: the absence of RPG layers in
the PvZ2 mod scene is weak evidence about player demand and strong evidence about tooling.** The moment a team
escapes the engine (Fusion, Gardendless), an RPG layer appears within two major versions.

---

## 6. What mods consistently fail at

### 6.1 Solo maintainership, and what happens when the maintainer leaves

**FACT (third-tier).** ECLISE — the mod that, in TV Tropes' words, *"changed Plants vs. Zombies 2 modding
forever"* — ended development in May 2023. The stated cause is not burnout or scope:

> **Overshadowed by Controversy:** *Despite the general division around the mod, what really soured the mod's
> reputation was the actions of creator. Goodpea2 would engage in controversial behavior such as racism,
> banning people for criticizing his project, Patreon-locked in-game cosmetics and more. As a result, goodpea2
> stepped down from the mod and ended its development after being called out for it.*

Garden Rush: *"development has mostly been halted following the initial Needleblight Forest release due to
Creeps20 having to focus on other affairs."* Fallen: Part 1 only. Four of the twelve projects in §1 are halted
or ended. **INFERENCE:** the modal PvZ2 overhaul is one to three people, distributes through one Discord, and
has no succession plan. Requiem is the only one that ended deliberately and handed over its content.

### 6.2 Audience mismatch — the mod is for veterans, the audience is casual

**FACT (third-tier, ECLISE YMMV):**

> **Misaimed Fandom:** *While Eclise was developed as a "Veteran Mod" first and foremost, it being one of the
> first "overhaul" mods to break into the mainstream garnered it a lot of attention. Due to it ditching the
> microtransactions … many fans blindly recommended it to even the most casual of players by calling it "The
> Better PvZ2 that EA couldn't deliver". However, Eclise is designed for experienced play…*

> **Broken Base:** *Many like ECLISE for its gameplay or how it changed PvZ2 modding forever. Others may hate
> it for its difficulty… The release of the "Alpha" version would further divide the fandom. Some players
> prefer the new approach, while others decry the many changes and claim that goodpea ruined otherwise-perfect
> level design.*

And the mirror image, on Reflourished:

> **Dancing Bear:** *…what the fandom mostly knows it for is the fact that it's "The Better Vanilla PvZ2 Mod".
> Resultingly, many players of the game simply migrated from the Vanilla version in order to use Reflourished
> as a direct substitute… This makes it hard for the aspects of the game outside of its gameplay or base-game
> content to get any appreciation.*

**INFERENCE.** Removing monetisation is such a strong pull that it *overrides* the mod's actual design identity
in how players talk about it. Both the hardest mod and the gentlest mod get recommended for the same reason,
to the same audience, and both get a mismatch complaint.

### 6.3 Balance failures have two named shapes

Both from the Reflourished YMMV page (third-tier), and both worth quoting because they are the two ends of the
same error:

**Overcorrection.**
> **Low-Tier Letdown:** *While she was universally considered the biggest Game-Breaker of the vanilla game, many
> believe the nerfs the Reflourished dev team gave to Pokra were a serious overcorrection. A full volley of her
> attacks falls just short of defeating a Browncoat, thanks to her jabs being half as powerful as a Peashooter
> pea, and her drill attack being reduced to a mere 80 damage… As a result, she's considered strictly worse
> than slightly more expensive melee attacking plant Parsnip, and cheaper, ranged Anti-Armor plant Lily of
> Alchemy.*

The team then partially reverted in 1.3.3: *"In an attempt to make Pokra more satisfying to use and increase
its versatility, we have brought back her stall ability from the vanilla version of PvZ2 for her projectiles."*

**The outlier that survives every nerf.**
> **High-Tier Scrappy:** *Despite the myriad of nerfs applied to him, Winter Melon still remains a monstrous
> powerhouse. He retains his 3x3 splash chill that serves as a powerful crowd-control tool… It's for this reason
> that he is commonly spammed in Penny's Challenges as an almost surefire way to beat them.*

The changelog shows why: they moved his sun cost, recharge, damage, splash and chill duration across four
versions and never touched the **3×3 splash chill** itself. **INFERENCE: the number was never the problem —
the shape of the effect was.** Tuning a stat cannot fix a plant whose *area* is the outlier.

### 6.4 Difficulty spikes are found by players, not by testing

**FACT (third-tier).** Reflourished's *That One Level* entry names Hypothermic Hollows days 6, 12, 14, 19 and
20, with specific causes (endangered plants placed on slider tiles; a mini-boss that buffs its allies while
plants are constantly frozen; a "Locked and Loaded" level with only eight Sunflowers and no way to replace
them; no lawnmowers) and closes: *"It seems like the developers took notice, as they eventually nerfed these
specific levels and zombies in a later update."*

The 1.3.3 changelog corroborates in the team's own voice, e.g. Assault Airspace - Day 24: *"The balance changes
to Gatling Pea and Saucer Squash meant this already difficult level became even harder now. We adjusted waves
before the 2nd flag … as well as giving the home +750 HP."*

**INFERENCE.** This is the second-order cost of a whole-roster rebalance and it is the cost teams under-price:
**every plant change silently re-tunes every level that hands the player that plant.** Reflourished's mitigation
is per-level compensation after the fact, which scales linearly with the level count — and their level count is
in the hundreds.

### 6.5 Endgame emptiness is solved, and solved the same way every time

I found **no** community complaint of "there is nothing to do after the campaign" in any of these mods. Every
one of them shipped a repeatable mode before it shipped its second world. The complaint that *does* recur is
about the **quality** of the repeatable content — Reflourished's *Anti-Climax Boss* entry criticises a boss whose
gimmick (reflect Poker Cards at a shield) is strictly worse than ignoring the gimmick and brute-forcing:
*"trying to reflect back Poker Cards a bigger hassle than just focusing all of your firepower onto the boss due
to zombies constantly body-blocking the projectiles … essentially making the Nightmare-tron a Damage-Sponge
Boss that's more time-consuming than difficult."*

**INFERENCE.** In this genre the failure mode is not "nothing to do", it is "the intended interaction is
optional and the boring one is better". A designed counter must be *strictly* better than brute force, or it
will not be used.

### 6.6 Story is built and then ignored

Reflourished has the most developed narrative in the scene — an ensemble of named Zombie Generals across worlds
and events — and the YMMV page carries **both** of the tropes for this:

> **Enjoy the Story, Skip the Game:** *…This aspect of it can serve as a greater appeal for it to players not as
> fond of the gameplay (such as veteran players) due to it leaning closer to vanilla's PvZ2's style…*
> **Play the Game, Skip the Story:** *…a majority of the fans tend to focus on the gameplay rather than the plot
> itself… This isn't helped by most of the original story elements being posited in the late-game or locked
> behind sidequests and events.*

Plus **Continuity Lock-Out**: characters appear in the main campaign as if already known, so *"if a player
doesn't want to be Locked Out of the Loop, they'll have to either play their debut thymed events or go to the
official Reflourished wiki."*

**INFERENCE.** Narrative content gated behind time-limited events is narrative content most players never see.

---

## 7. Content scale, and how a small team sustains it

| Project | Plants | Zombies | Levels | Team size (where stated) | Sustaining trick |
|---|---|---|---|---|---|
| Reflourished | ≈190 (computed) | ≈385 (computed) | 14 worlds + 4 Epic Realms + 19 Endless Zones + daily Inzanity + rotating Thymed Events | "Reflourished Dev Team", leads PvZABFan + Peamix | **Seasonal re-skins**: 15 Thymed Event rosters, each a re-skinned basic/conehead/buckethead/flag quartet plus a handful of originals. Plus **procedural dailies** and **replayable events** (added 1.3.3) so old content re-enters rotation. |
| Project ECLISE | **112** | — | ≈580 Beta level entries (computed) | goodpea2 + Mine Power | **Gate levels** every 10 stages act as chapter walls, letting difficulty reset; **Community World** offloads authoring to Discord |
| AltverZ | ≈126 (computed) | — | ≈322 adventure ids (computed) | Founded by 2, now 1 + community | **10 Normal + 10 Extra per world** — the Extra set reuses the world's assets at higher difficulty, doubling the level count at near-zero art cost. Plus **Timeless Avenue** player levels. |
| Requiem | — | 4 zombies + basics + imps + 2 gimmicks per world | 14 worlds | DT_MP (solo) | **A fixed per-world template** — 4 zombies, a basics set, 2 gimmicks, an endless zone, a Timeline Blocker — applied 10 times |
| Garden Rush | small (4 seed slots) | — | 1 world | Creeps20 (solo) | Did not sustain it |
| PvZ Fusion | **532 fusions** in v3.9 (323 Common / 77 Upgraded / 12 Advanced / 102 Odyssey / 18 Infusible + Titan); ~187 base plant images on the wiki's Plants list (computed) | many | Adventure + Super Adventure + Tower Defense + Odyssey (21 levels) + Abyss (30/season) + minigames | Blue Fly + team | **Combinatorics**: 187 base plants × pair fusion generates the roster; only the *result* needs art, and many results are recolours. Plus **six global difficulty multipliers** turning one level set into six. |
| PvZ2 Gardendless | — | — | Community + Daily Level | Gaozih + community | **Ship the editor**: browser level editor, datapack system, Discord submissions, and community-authored "Recommended & Challenging Card Decks" and Epic Levels credited by handle in the changelog |
| PvZ2C (official, for scale) | rarity-tiered roster | levelled | 20+ worlds | Commercial studio | Puzzle pieces, chests, dailies |

**The five sustaining tricks, extracted (INFERENCE, from the table above):**

1. **Difficulty multipliers reuse the level set.** Fusion's six difficulties are one content set sold six times,
   and they are *multipliers on spawn volume and damage reduction*, not new authoring.
2. **Re-skin the four basics.** A seasonal roster is a palette swap of Basic/Conehead/Buckethead/Flag plus a
   few originals. Reflourished's ~385 zombies are mostly this.
3. **Author a per-world template once, fill it ten times.** Requiem's explicit formula.
4. **Ship the editor and take submissions.** ECLISE, AltverZ, Gardendless all do this; it is the only trick that
   grows content faster than the team shrinks.
5. **Make the combinatorics the roster.** Fusion's 532 fusions come from ~187 inputs. The cost is that
   532 things need balancing, which is why Fusion's answer is per-rarity blanket scaling rather than
   per-unit tuning.

---

## 8. Hooks for this project

**Non-normative. Un-vetted. These are observations, not recommendations, and nothing here has been checked
against this repo's code or specs.**

- **Published dev comments per balance change.** Reflourished attaches a signed rationale to nearly every
  number it moves, grouped by plant family. It is the only balance process in the scene that survived four
  minor versions without a revolt, and the grouping (families, not individuals) is doing most of the work.
- **The "one interaction, not a matrix" element rule.** Reflourished shipped exactly one elemental
  interaction — chilled/frozen → +1.25× first-impact fire damage — described as "temperature whiplash". Worth
  noting as the minimum viable version of an element system, and as evidence that one well-chosen interaction
  reads as a system to players.
- **Sidegrade tiers instead of upgrade levels.** ECLISE's tiers move stats in both directions and are
  explicitly optional. It is the only per-unit progression in the PvZ2 scene that did not create a mandatory
  grind.
- **Rarity scaling the magnitude *and* the cost.** PvZ Fusion's Abyss gives Odyssey plants 5% of the upgrade
  buff and charges 30× the price. Two brakes on the same axis.
- **Cross-stat conversion modifiers as the expensive tier.** Fusion's costliest Abyss modifiers are all of the
  form "for every X of stat A, gain Y of stat B", which is exactly the derived-stat shape.
- **A per-hit damage cap instead of a stat cap.** Fusion caps damage-per-hit on its boss tier (5,000) rather
  than capping any player stat — the ceiling lives on the receiving end.
- **Run-scoped meta-currency with a reroll and a backpack.** Abyss: Chlorophyll, 5 rerolls per new clear, a
  modifier cap that grows +3 per level, and a backpack to park over-cap modifiers, plus sell-back.
- **Persistent loss inside a run.** Reflourished's Endless Zones make a lost lawnmower stay lost until a card
  grants a replacement — attrition carried across levels rather than reset per level.
- **Procedural dailies kept off the progression spine.** Inzanity generates five levels a day for a flat
  25-gem reward and is not part of the campaign.
- **`Project Paradox` as prior art for the injector pattern.** A side-loaded `.so` that hooks the host's
  plant-id table to widen a closed registry — the same architecture as this repo's Harmony hooks, arrived at
  independently.
- **The "specific counter" trap.** Reflourished cut three plants whose only role was countering one gimmick.
  A unit that exists to answer one mechanic is dead content the moment that mechanic is retuned.
- **The Winter Melon lesson.** Four versions of stat nerfs did not fix a plant whose 3×3 splash chill was the
  actual outlier. When a unit resists tuning, check whether the outlier is a number or a shape.
- **Difficulty as global spawn/DR multipliers.** Fusion's six difficulties: spawn ×1/×3/×5/×7/×10/×10, plus
  zombie damage resistance 0/30%/60% and speed +10%/+20%, plus halved hypnotise effectiveness at the top.
  Enemy HP is not the dial.
- **Ship the level editor.** Every mod that outlived its authors' attention had a community submission
  pipeline.

---

## What I could not find

Mandatory section. Everything below was searched for and not obtained.

**Blocked or unreachable channels**

1. **Reddit — completely unreachable this session.** `r/PlantsVSZombies` search returned a network-policy block
   through every route tried (direct, text-proxy, and the fetch tool, which reports it cannot fetch
   `www.reddit.com`). This is the largest single gap: player-side criticism of Reflourished, ECLISE and AltverZ
   almost certainly lives there. Every reception claim in §6 therefore comes from TV Tropes YMMV pages, which
   are a *summary* of community opinion, not the opinion itself.
2. **Discord — the actual home of this scene.** Named servers I identified but could not read:
   Reflourished (`discord.gg/ba9rC7QdKb`), PvZ Fusion Fans Official (`discord.gg/pvzfusion`), PvZ Fusion Wiki
   (`discord.gg/496hDErYAB`), TwinStar.ToolKit (`discord.gg/v7qvttSX8K`), ECLISE community (referenced by the
   Community World feature), Floral Federation (host of Addendum), AltverZ community. **No announcement
   archive, changelog channel, dev-discussion log or balance-patch note from any of these was obtained.** For
   several mods, Discord is the *only* place a changelog exists.
3. **Fandom direct access** is Cloudflare-blocked from this session; all Fandom content here came through a
   text-extraction proxy. `eclisesolstice.miraheze.org` (the ECLISE Solstice fork wiki) sits behind a CAPTCHA
   and returned nothing.
4. **Chinese-language sources.** Moegirlpedia (`zh.moegirl.org.cn`) returned 403; Baidu Baike, BWIKI/biligame
   and Bilibili articles were not reachable, and the search backends available to this session all served
   bot-challenges for Chinese-language queries. **I therefore have essentially nothing first-hand on the 魔改 /
   修改版 mod scene as it discusses itself** — the Chinese material here is filtered through English wikis. The
   sibling file `02-pvz2-chinese-and-fusion.md` has better Chinese sourcing; treat this file as the English-scene
   view.

**Mods named in the brief that I could not confirm exist**

5. **"PvZ2 Discovery"** — no wiki, no archive item, no TV Tropes page, no GameBanana entry. Candidate Fandom
   subdomains (`pvz2discovery`, `pvz-discovery`) return "This wiki does not exist".
6. **"PvZ2 Reanimated / Reanimation"** — same result. *Reanim* is the name of PvZ's animation format, so the
   phrase may have been a toolchain term rather than a mod name.
7. **"Plants vs Zombies 2: Endless"** — nothing under that name. PvZ2's own Endless Zones and the Chinese
   *PvZ: Endless Edition* (植物大战僵尸无尽版, a separate commercial title) both collide with the search term.
8. **"Springtime Snapshot"** — nothing. Reflourished has a *Thymed Event* called "The Springening", which may
   be what was meant.

**Specific facts I wanted and did not get**

9. **Reflourished's official website or a team-hosted changelog.** Only the Fandom wiki (which appears to
   reproduce the team's changelogs faithfully, including dev comments) and a Discord invite. The many
   `pvz2-reflourished.*` APK sites are unaffiliated mirrors.
10. **Team sizes.** No mod in §1 publishes a contributor count. Credits appear only as scattered handles in
    changelogs (e.g. Reflourished 1.3.2 crediting `NewspaperZombie` for Solar Pea's art, and eight contest
    winners for costumes). I could not put a number on any team.
11. **Download or player counts from any team.** The archive.org figures in §3.4 are third-party mirrors and
    almost certainly under-count by an order of magnitude.
12. **The full ECLISE tier table** — which plants have non-standard tier counts, and the actual coin prices per
    tier. The wiki documents tier data per-plant, which I did not enumerate.
13. **Reflourished Endless Zone card-pool weights** — the wiki describes the 1-of-4 draft qualitatively but
    gives no probabilities.
14. **Whether any mod ships an explicit damage-type or resistance table.** I found none, but I only read the
    wikis; the shipped RTONs were not datamined for this file. A negative from a wiki is weak evidence.
15. **`Project Paradox` itself** — beyond TwinStar's four-paragraph description of its hooking technique, I
    found no repo, release, wiki or feature list. Its actual custom plants are undocumented here.
16. **Project Skill Tree** — no primary source at all. Everything in §2.7 rests on one SEO aggregator page.
17. **PvZ2 Solstice and ECLISE 3.0** (the two forks) — no reachable documentation of what either changed.
18. **Level-JSON module vocabulary.** I have the shape (`Modules`, `objclass`/`objdata`, `RTID(x@y)`,
    `@LevelModules` vs `@CurrentLevel`) but not the enumerated module list — no public schema exists; the
    community recovers it by running `PvZ2LibraryAnalyzer` against `libPVZ2.so`, and I did not run it.
19. **Anything from `senharuma.com`, the current flagship tool** — the site was listed but not fetched, so
    Sen's actual capability set here is second-hand.
20. **PvZ Hybrid's "official recognition" claim** — TV Tropes asserts it is the only mod officially recognised
    by the franchise; I found no EA/PopCap statement confirming this and it should be treated as unverified.
