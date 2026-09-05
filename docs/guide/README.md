# Rise of Summoner — player guide

**Guide version: 12 — 2026-09-05**

This folder is the **product-vision SSOT** for players: what the game is, which loops exist, and every feature on the road map — not how the software is built. Later features implement or extend the loops named in [The loops](the-loops.md).

For install, antivirus, and launcher steps, see [the player runbook](../runbook/players.md). For bugs and downloads, see [SUPPORT.md](../../SUPPORT.md).

---

## Status legend

Every feature below wears one badge. Thin surfaces that you can already open count as **Shipped**, with a note on the page about what is still rough.

| Badge | Means |
|---|---|
| **Shipped** | You can do this in the current local build |
| **WIP** | Designed and in the forge — not finished yet |
| **Vision** | Locked as fiction or design; not in the current loop |

Notes on **Shipped** are allowed: **Shipped (thin)** means the surface opens but is still rough; **Shipped (fiction)** means the rule is live in the product story (for example prestige across worlds) even when the full stage UI is not.

Nothing here is a date promise. The catalog only grows — Vision stays Vision until it ships.

---

## Start here

| Page | What it is |
|---|---|
| [**Vision site (HTML)**](site/) | Fancy tabbed skim — open `site/index.html` (includes a **Mechanisms** tab that indexes every system) |
| [Feature list (brief)](features.md) | Market skim — short feature intro; open detail when hooked |
| [**Mechanisms**](mechanisms/) | Handbook — every system has a teach guide (HTML under `site/mechanisms/` + markdown); open the Mechanisms tab |
| [The game](the-game.md) | What Rise of Summoner is — RPG + empire, lore, win and lose |
| [The loops](the-loops.md) | Ten named loops — product-vision SSOT every feature hangs on |
| [How you play](how-you-play.md) | Lawn first, first session, unlock ladder (live + Vision chapters) |
| [Glossary](glossary.md) | Player dictionary |

---

## Pillar pages

| Page | What it covers |
|---|---|
| [Sanctum](sanctum.md) | Your home hall and the menus you open over it |
| [Creatures](creatures.md) | Summon, bind, fuse, patron — the summon and fusion loop |
| [Combat](combat.md) | Elements, shields, statuses, meters, skills |
| [Expeditions](expeditions.md) | Idle RPG — dispatch a squad, wait, collect |
| [The rift](the-rift.md) | World-map adventure, world-stage empire, farm / hunt / defend |
| [The lawn](the-lawn.md) | Lawn as the first core loop |
| [Delves and sieges](delves-and-sieges.md) | Party crawls (hunt) and seat defense |
| [Relics and builds](relics-and-builds.md) | Item loop, free-build power, trees, commanders |
| [Almanac](almanac.md) | Dossiers, chronicle, lasting progress |

---

## Feature catalog

Checklist of named features (Shipped / WIP / Vision). For **how each system works**, use the [mechanisms handbook](mechanisms/).

| Feature | In one sentence | Status | Where you meet it | Page |
|---|---|---|---|---|
| Local control room | Run the RPG on your machine — no account, no cloud | Shipped | Launcher opens the browser UI | [Teach guide](site/mechanisms/local-control-room.html) · [How you play](how-you-play.md) |
| Packaged one-click zip | Download, unzip, Play — no SDK | WIP | Releases | [How you play](how-you-play.md) |
| Title and save select | Pick or create a summoner save at the door | Shipped | Title screen | [Sanctum](sanctum.md) |
| Sanctum | Your hall — creatures on display, next steps, travel | Shipped | Home stage | [Sanctum](sanctum.md) |
| First-run naming ritual | Bind and name your first creature in place | WIP | Sanctum | [Sanctum](sanctum.md) |
| Layer rail | Creatures, Commanders, Relics, Fusion, Pacts, Expeditions, Almanac, Chronicle — same keys everywhere | Shipped | Any stage | [Sanctum](sanctum.md) |
| Unlock ladder (beats) | Layers open on play beats — locked ones say what unlocks them | Shipped | Rail on every stage | [Sanctum](sanctum.md) · [How you play](how-you-play.md) |
| Dave-level unlock chapters | Complexity drips by Dave’s level so day one is not a spreadsheet | Vision | Progression | [How you play](how-you-play.md) · [The loops](the-loops.md) |
| Settings and keymap | Rebind keys, display, quit — System on Esc | Shipped | System | [Sanctum](sanctum.md) |
| Level up and power loop | Dave’s level, specimen XP, free-build aptitudes — endless, no cap | Shipped | Chronicle / builds | [The loops](the-loops.md) · [Relics and builds](relics-and-builds.md) |
| Summon and fusion loop | Souls → altar → pacts → fusion; wild joins and capture as other intakes | Shipped | Creatures | [The loops](the-loops.md) · [Creatures](creatures.md) |
| Item collection and progression | Find, vault, equip, craft — opens as a chapter | WIP | Relics | [The loops](the-loops.md) · [Relics and builds](relics-and-builds.md) |
| Item chapter (Dave 10) | Armoury opens when Dave reaches the item unlock; no dead loot UI before | Vision | Relics | [How you play](how-you-play.md) · [Relics and builds](relics-and-builds.md) |
| Creatures roster | Persistent specimens with level, gear, and history | Shipped | Creatures | [Creatures](creatures.md) |
| Creature sheet | Full specimen panel — overview, progression, actions, gear | WIP | Creatures | [Creatures](creatures.md) |
| Souls | Earn from play; spend to summon and bind | Shipped | HUD + altar | [Teach guide](site/mechanisms/souls.html) · [Creatures](creatures.md) |
| Essence | Element-matched materials for fusion and some buildings | Shipped | Fusion / rift | [Teach guide](site/mechanisms/essence.html) · [Creatures](creatures.md) · [The rift](the-rift.md) |
| Summoning altar | Spend souls for new demons; pity shown on the altar | Shipped | Summon / Demons | [Creatures](creatures.md) |
| Pacts and loyalty | Bind demons to slots; neglect them and they refuse | Shipped | Pacts | [Creatures](creatures.md) |
| Tribute on pacts | Keep contracts paid; overdue tribute is leverage | Shipped | Pacts | [Creatures](creatures.md) |
| Fusion | Merge specimens into stronger forms; discover recipes | Shipped | Fusion | [Creatures](creatures.md) |
| Patron demon | One demon’s element colours the army | WIP | Patron choice | [Creatures](creatures.md) |
| Wild joins | Demons that join without an altar pull | Shipped | Creatures / world | [Creatures](creatures.md) · [mechanisms/wild-joins.md](mechanisms/wild-joins.md) |
| In-run capture | Weaken and catch demons during play | WIP | Lawn / delves | [Creatures](creatures.md) · [The lawn](the-lawn.md) |
| Six elements + omni | Fire, ice, air, earth, light, dark — real matchups | Shipped | Combat everywhere | [Combat](combat.md) |
| Shields, crit, resistance | Layered defence and hits that can fail or land hard | Shipped | Lawn and battles | [Combat](combat.md) |
| Statuses | Butter, freeze, chill, poison, hypno, wither, contagions, and more | Shipped | Combat | [Combat](combat.md) |
| Actor meters | HP, stamina, sun/hunger, spirit, yang/yin, poise | WIP | Skill and stance play | [Combat](combat.md) |
| Skills and loadouts | Equip actions with costs, cooldowns, and targets | WIP | Battle / creature sheet | [Combat](combat.md) |
| Expeditions (idle RPG) | Timed squad missions — 30 min to 20 h — collect when ready; core forever | Shipped | Expeditions | [Expeditions](expeditions.md) · [The loops](the-loops.md) |
| Expedition recall and slots | Recall early for pro-rated rewards; grow parallel slots | Shipped | Expeditions | [Expeditions](expeditions.md) |
| Expeditions start quests and events | Idle ticks that open quests and world events | Vision | Expeditions | [Expeditions](expeditions.md) · [The loops](the-loops.md) |
| Interactive battles | Turn-based fights you play yourself — initiative, reactions | WIP | Battle stage | [Combat](combat.md) · [Expeditions](expeditions.md) |
| Live lawn mirror | Watch the 12×5 board in the browser while a match runs | Shipped | Lawn | [The lawn](the-lawn.md) |
| Unit HUD on the lawn | Identity, shield, and statuses above each unit | Shipped | Lawn | [The lawn](the-lawn.md) |
| Bound demons on the lawn | Deploy roster demons into a lawn run | Shipped | Lawn | [The lawn](the-lawn.md) |
| Lawn as first core loop | First thing you play; feeds souls, XP, almanac, deploy | Shipped | Lawn | [The lawn](the-lawn.md) · [The loops](the-loops.md) |
| Lawn blessing | A real lawn run charges stronger soul earn on web play | WIP | After a lawn run | [The lawn](the-lawn.md) |
| Lawn trophies | Cosmetic prestige from lawn play — never stats | WIP | After lawn play | [The lawn](the-lawn.md) |
| In-game open button | Open the RPG without leaving the game window | WIP | In the lawn game | [The lawn](the-lawn.md) |
| Rift world map (adventure) | Sectors, lanes, legions, End Turn — where you go | Shipped | World map | [The rift](the-rift.md) · [The loops](the-loops.md) |
| Fog of war | Your ground is clear; other people’s ground stays uncertain | Shipped | World map | [The rift](the-rift.md) |
| World virtual turns | End Turn commits everyone’s orders — the world clock, not a diary | Shipped | World map | [The loops](the-loops.md) · [The rift](the-rift.md) |
| Zomboss as commander | He runs his own war from his own fog | Shipped | World map | [The rift](the-rift.md) |
| Loam and the Fracture | Ground you hold must stay real; neglect fades | Shipped (thin) | World map | [Teach guide](site/mechanisms/loam.html) · [The rift](the-rift.md) |
| Farm, hunt, and defend | Territorial loop — yield, prey, Seat and homeworld | Shipped (thin) | World / lawn / siege | [The loops](the-loops.md) · [The rift](the-rift.md) · [mechanisms/farm-hunt-defend.md](mechanisms/farm-hunt-defend.md) |
| World stage (empire building) | Camera, HUD, inspector, End Turn — where you run the empire | Vision | World stage | [The rift](the-rift.md) · [The loops](the-loops.md) |
| Sector buildings | Wells, waystations, granaries — economy on the map | WIP | World map | [The rift](the-rift.md) |
| Map tools and orders | Wardens, cede, dowse, lenses, outliner | WIP | World map | [The rift](the-rift.md) |
| Map → battle handoff | Commit a legion into a fight | WIP | World map | [The rift](the-rift.md) |
| Three-layer world | Graph → sector board → lane board | Vision | World | [The rift](the-rift.md) |
| World generator and deeper fog | Procedural maps and richer scouting craft | Vision | World | [The rift](the-rift.md) |
| World events and raids | Blood moons, roaming bosses, ecology | Vision | World | [The rift](the-rift.md) · [Creatures](creatures.md) |
| Enemy counter-development | The war shifts resists and composition from how you play — not matched levels | Vision | World / Zomboss | [The loops](the-loops.md) · [The rift](the-rift.md) |
| Failure branches | Lost sector, failed extract, or failed defense opens different content — not only a wipe | Vision | World / delve / quests | [The loops](the-loops.md) · [Delves and sieges](delves-and-sieges.md) |
| Named combat reactions / combo recipes | A short learnable list on top of the element ring — not a second ring | Vision | Combat | [The loops](the-loops.md) · [Combat](combat.md) |
| Build presets | Save a synergy loadout — patron, relics, aptitudes, who you field | Vision | Relics / builds | [The loops](the-loops.md) · [Relics and builds](relics-and-builds.md) |
| Party formations / combo skills | How the pack stands, and combination skills — when you play | Vision | Delve / battle | [The loops](the-loops.md) · [Delves and sieges](delves-and-sieges.md) |
| Quest log | Track quests and events as a layer | Vision | Sanctum / rail | [The loops](the-loops.md) · [How you play](how-you-play.md) |
| The Delve | Party crawl through seeded rooms to a boss | Vision | Delve stage | [Delves and sieges](delves-and-sieges.md) |
| Pack, extract, oaths, domains | Haul out, hard stakes, found domains | Vision | Delve | [Delves and sieges](delves-and-sieges.md) |
| Siege | Defend or assault the Seat on a tactical board | Vision | Siege stage | [Delves and sieges](delves-and-sieges.md) |
| Relics armoury | One vault of gear — equip, compare, craft | WIP | Relics | [Relics and builds](relics-and-builds.md) |
| Relic crafting | Sockets, sets, charms, salvage, enhance, named uniques | WIP | Relics | [Relics and builds](relics-and-builds.md) |
| Free-build aptitudes | Twelve aptitudes; you have no class | Shipped | Builds | [Relics and builds](relics-and-builds.md) |
| Species builds | Species you field grow their own allocation | WIP | Builds | [Relics and builds](relics-and-builds.md) |
| Passive trees | Spend souls into trees for identity and risk | Vision | Builds | [Relics and builds](relics-and-builds.md) |
| Commanders | Choose who leads the next lawn run | WIP | Commanders | [Relics and builds](relics-and-builds.md) |
| Commander auras | Side-wide presence from the leader you chose | WIP | Commanders | [Relics and builds](relics-and-builds.md) |
| Type and specimen XP | Plants, zombies, and demons grow from work done | Shipped | Chronicle / roster | [Almanac](almanac.md) |
| Almanac dossiers | Meet it in play → it files itself | Shipped | Almanac | [Almanac](almanac.md) |
| Chronicle | Run history, progression, lasting record | Shipped | Chronicle | [Almanac](almanac.md) |
| New world — keep who you are | End a world and start another; roster and souls bank, loam and holdings do not | Shipped (fiction) | Prestige / new map | [The game](the-game.md) · [mechanisms/new-world-prestige.md](mechanisms/new-world-prestige.md) |

---

## The loops in one breath

> Play the lawn → grow power, summon, and gear → dispatch idle expeditions → farm, hunt, and defend the empire → adventure the map and build the stage → delve and quest → do it again on ground that fights back.

Full names and rules: [The loops](the-loops.md).
