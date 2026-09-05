# How you play

**Status:** Shipped from source · packaged zip **WIP**

---

## Local control room

Rise of Summoner runs on **your machine**. No account. No cloud. No external service.

The launcher starts the local server and opens the browser UI. Your save lives next to that server. Updating the RPG keeps your data and never touches the lawn game’s own saves.

That local browser UI is the **control room** — Sanctum, lawn, rift, expeditions, everything in this guide.

---

## The loop

1. **Lawn** — play a lawn match. Souls, experience, and almanac entries land in your save. Deploy demons you raised.
2. **Power** — Dave levels. Specimens and species grow from work they did. Spend points into free-build aptitudes.
3. **Summon and fuse** — spend souls at the altar; keep demons loyal with pacts; merge duplicates into stronger forms.
4. **Items** — find relics, vault them, equip and craft (**WIP** as a full armoury; opens as a chapter in the Vision unlock ladder).
5. **Idle** — dispatch spare demons on expeditions; collect later. No stamina.
6. **Empire** — march the map, farm and hunt ground, hold loam, defend the Seat when the war reaches home.
7. **Return home** — every run ends at the Sanctum. Check what came back. Go again.

Same save whether the lawn game is open or closed. Full loop names: [The loops](the-loops.md).

---

## Lawn first, then the rest of the war

| | Lawn (first core) | Web control room |
|---|---|---|
| **What it is** | A live lawn match (Plants vs. Zombies on your machine) — the first core loop | Sanctum, expeditions, rift, summoning, fusion, and more |
| **Why play it** | Feeds souls, levels, almanac, deploy; later capture and blessing | Raise the roster, run idle expeditions, push the empire on your schedule |
| **What it must never do** | Become the only way to keep up forever | Lock a feature behind a live match after that feature has unlocked |

You start on the lawn. After the first chapter, unlocked web features stay fully playable with the lawn game closed. A real lawn run can charge a **blessing** for the next web expeditions (**WIP**) — lawn play makes idle play better instead of competing with it.

---

## First session (when you can run the build)

1. Start the launcher (or your usual local deploy). The browser control room opens.
2. Create or pick a **save** at the door.
3. Land in the **Sanctum** — your hall.
4. When a lawn match is running, open the **Lawn** stage. Play a match. Deploy bound demons when you have them.
5. Spend souls at the altar when you have them. Open layers as they unlock.
6. Travel to the **World** map when you are ready to march a legion (map travel is open in this build; deeper empire verbs and Dave-level chapters are **Vision**).

Hotkeys for the main layers (same on every stage):

| Key | Opens |
|---|---|
| `C` | Creatures |
| `K` | Commanders |
| `R` | Relics |
| `F` | Fusion |
| `P` | Pacts |
| `E` | Expeditions |
| `A` | Almanac |
| `H` | Chronicle |
| `M` | Travel to the world map |
| `Esc` | Close a layer; on an empty stack, open System |

### Unlock ladder — in this build (**Shipped**)

Complexity unlocks as you play. Locked layers say what unlocks them — they are not invisible, and they are not greyed-out dead weight.

Today’s rail opens on **beats**, not Dave’s level:

| Layer | Unlocks |
|---|---|
| Creatures, Commanders | Session start |
| Relics | When you hold your first item |
| Fusion | Once you have a demon to fuse |
| Pacts | When a contract is first offered |
| Expeditions | Once you have a bound demon to field |
| Almanac, Chronicle | After your first run |

### Unlock chapters — Dave’s level (**Vision**)

The product vision drips features by **Dave’s level** so day one is not a spreadsheet. Thresholds will live in balance data later; they do **not** gate the live rail yet.

| Dave | Opens |
|---|---|
| 1 | Lawn, Sanctum, Creatures, deploy, souls; summon altar when you have souls |
| 2 (first level-up) | Almanac, Primary Stats, Chronicle |
| 10 | Item loop — loot and Relics |
| 12 | Pacts |
| 15 | Commanders |
| 20 | Expeditions (idle) |
| 22 | Fusion |
| 30 | World-map adventure |
| 35 | World-stage empire; farm and defend verbs on the map |
| 40 | Quest log as a layer |
| 45 | The Delve |
| 50 | Siege |

Dave’s level has no cap. New worlds raise prestige; they do not wipe who you are.

---

## Install

You do **not** need Node, a .NET SDK, or the Desktop Runtime for a release zip.

Step-by-step (Browse, loader choice, antivirus): **[Player install runbook](../runbook/players.md)**.

**In this build:** the stack runs from source today. The one-click zip is **WIP** with the first tagged release. Until then, contributors use the deploy scripts; players wait on Releases or star the repo for the announcement.

---

## What stays out of this guide

Sandbox cheats, simulator mode, dump pages, and connection telemetry are developer tools. Leave them alone and the lawn game plays as shipped. This guide never documents them as features.

Next: [The loops](the-loops.md) · [Sanctum](sanctum.md) · [Creatures](creatures.md).
