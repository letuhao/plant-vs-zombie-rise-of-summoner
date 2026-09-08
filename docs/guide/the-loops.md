# The loops

**Status:** product vision SSOT — every feature in this guide hangs on a loop named here.

Together with [The game](the-game.md), this page is what Rise of Summoner *is*. A later feature implements or extends one or more of these loops. It does not invent a parallel pitch.

Two families: **spine** (what you grow) and **places** (where you play).

---

## Three clocks (not a fourth)

Do not invent a diary meta-clock (minute → day → week → month). Three clocks already exist:

| Clock | What it is | Status |
|---|---|---|
| **Lawn** | A live lawn match | Shipped |
| **Idle expeditions** | Wall-clock dispatch (30 min to 20 h) — idle RPG on purpose | Shipped |
| **World map / empire** | **Virtual turns** — you press End Turn; everyone’s orders resolve. Not a real-life calendar | Shipped (thin) |

The world clock is already designed and built. Expeditions use wall-clock because they are idle. Lawn uses live time because a match does. That is enough.

---

## Spine — always on

### A. Level up and power

**Status:** Shipped (Dave’s level, type and specimen XP, free-build aptitudes) · species builds and meters **WIP** · passive trees **Vision**

Dave’s level is the main line. Specimens, species, and types also level from work they did. Aptitude points go where you want — you have no class. Later: trees and commander presence.

Endless grind: Dave’s level has no cap. This loop is why complexity can unlock by level without a tutorial campaign.

Where you meet it: [Almanac](almanac.md) · [Relics and builds](relics-and-builds.md) · [How you play](how-you-play.md)

---

### B. Demon summon and fusion

**Status:** Shipped (altar, pacts, tribute, fusion, wild joins from expeditions) · capture and patron **WIP**

Souls → altar pulls. Pacts and tribute keep demons loyal. Fusion spends duplicates and essence into stronger forms. Wild joins and capture are other intakes. Gacha is one path, never the only one. This is the roster RPG.

Where you meet it: [Creatures](creatures.md) · [Expeditions](expeditions.md)

---

### C. Item collection and progression

**Status:** Relics armoury and crafting **WIP** · item chapter by Dave’s level **Vision** · **build presets Vision**

Find, vault, equip, compare, craft, socket, salvage. Relics are the gear loop. The Vision unlock ladder opens this as a **chapter** (proposed Dave 10). Drops before that chapter either do not roll or bank until the armoury opens — no dead loot UI.

**Build presets (Vision):** save a synergy loadout — patron, relics, aptitudes, who you field — so a fire lean is something you keep, not five independent clicks.

Where you meet it: [Relics and builds](relics-and-builds.md)

---

## Places — where you play

### 1. Lawn — first core

**Status:** Shipped (mirror, HUD, deploy, progression from play) · blessing, capture, trophies **WIP**

Legal lawn matches. First thing you play. Feeds souls, XP, almanac, deploy; later capture and blessing. Affects almost every spine loop. Not the win condition. After the first chapter, unlocked web features stay playable with the lawn game closed.

Where you meet it: [The lawn](the-lawn.md)

---

### 2. Idle expeditions — core forever

**Status:** Shipped (dispatch → wait → collect) · interactive battles you play yourself **WIP** · ticks that start quests and world events **Vision**

Monster Hunter–style: pick demons who are not on a live task, dispatch, wait, collect. No stamina. Four duration tiers. Materials for actors and empire (**not** loam — loam never banks). Sibling of the Delve, not a prototype to delete. Uses the wall-clock dispatch clock above.

Where you meet it: [Expeditions](expeditions.md)

---

### 3. Farming, hunting, and defending the empire

**Status:** Shipped (loam pressure, fog, claim) · buildings and wardens **WIP** · sieges and richer ecology **Vision** · **enemy counter-development Vision** · **failure branches Vision**

The territorial loop, named so it cannot hide under “the rift”:

| Verb | Means |
|---|---|
| **Farm** | Hold ground, take sector yield, expedition mats, later buildings. Loam is position, not a wallet |
| **Hunt** | Lawn kills, capture, wild joins, roaming, delves, map prey |
| **Defend** | Fracture and neglect, Seat siege, homeworld as the spine you must not lose |

**Enemy counter-development (Vision):** if you lean fire, the war grows fire-hard; if you lean summons, anti-summon shows up. Not “enemy level = Dave’s level.” Raise demons → a strategy works → the world counters → rebuild.

**Failure branches (Vision):** a lost sector, failed extract, or failed defense can open different ground — bandits, a domain, a new quest — not only a wipe. Consequence as content.

Where you meet it: [The rift](the-rift.md) · [Delves and sieges](delves-and-sieges.md) · [The lawn](the-lawn.md)

---

### 4. World map — adventure

**Status:** Shipped (sectors, lanes, legions, End Turn, fog, Zomboss) · map tools, map→battle **WIP** · deeper generator and fog craft **Vision**

The graph where you **go**: fog, march, claim, cede, dowse, Zomboss in his own fog, doors into delves. Committing a legion is travel into a test.

Time here is **virtual turns** — End Turn commits everyone’s orders. That is the world clock, already built (thin). Not a diary.

Where you meet it: [The rift](the-rift.md)

---

### 5. World stage — empire building

**Status:** Thin map surface **Shipped** · stage HUD, inspector, buildings, recruitment **WIP** · full empire stage as the place you stand **Vision**

Where you **are** when you run the empire: one camera, corner HUD, loam strip, calendar, inspector over the map. Inside a held sector: slots, buildings, recruitment, upkeep. Not a flowchart page with a sidebar of buttons. Same **virtual-turn** clock as the adventure graph.

Where you meet it: [The rift](the-rift.md)

---

### 6. Dungeon crawler — the Delve

**Status:** Vision · interactive battle stage shared with it **WIP** · **party formations / combo skills Vision**

Party crawl: provision parties, enter a domain, room graph, haul, extract or wipe, oaths on hard rungs. Sanctum picker and map door.

**Party formations / combo skills (Vision):** a second axis besides who is in the pack — how they stand, and a few combination skills. Matter when you play (delves and interactive battles). Expeditions stay auto-resolve.

Where you meet it: [Delves and sieges](delves-and-sieges.md)

---

### 7. Quests and events

**Status:** Expedition event ticks **Shipped** (thin) · delve quests, world events and raids, player-facing **quest log** **Vision** · **failure branches Vision** (shared with farm/hunt/defend)

The world has a story clock, not only a loot clock. Delve quests, expedition ticks, world events, turn report. A quest log you can open as a layer is a catalog gap today — named here so it cannot stay invisible. Failure that opens a new questline hangs here as well as on the territorial loop.

Where you meet it: [Expeditions](expeditions.md) · [Delves and sieges](delves-and-sieges.md) · [The rift](the-rift.md)

---

## Combat depth (hangs on places, not a eleventh loop)

Combat is the fight language every place uses — see [Combat](combat.md). Already in: element ring, shields, statuses, crit. **WIP:** meters, skills, interactive battles.

**Named combat reactions / combo recipes (Vision):** a short learnable list on top of the ring — the wet-and-lightning shape — without inventing a second ring or new elements. Interactive battles already mention reaction windows; this is the named catalog players can learn.

---

## What we grow next (Vision holes on existing loops)

No new loop heading. These hang on the loops above:

| Vision | Hangs on |
|---|---|
| Enemy counter-development | Farm / hunt / defend · world events · Zomboss |
| Failure branches | Quests and events · farm / hunt / defend · Delve |
| Named combat reactions / combo recipes | Combat · lawn · delve · interactive battles |
| Build presets | Item collection · level up and power |
| Party formations / combo skills | Delve · interactive battles |

---

## What a feature owes this page

- Name at least one loop above.
- Do not make the lawn the whole game.
- Do not add a fourth stock, a player class, a stamina gate, or a prestige wipe.
- Do not replace expeditions with delves — they are siblings.
- Do not invent a real-life diary meta-clock — use the three clocks above.
- To add a new named loop, grow this page (owner).

Next: [How you play](how-you-play.md) · [The game](the-game.md) · [index](README.md).
