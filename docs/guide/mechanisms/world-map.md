# Rift world map

**Status:** Shipped  
**Loop:** World-map adventure · Farm / hunt / defend — see [The loops](../the-loops.md)  
**Pillar:** [The rift](../the-rift.md)  
**HTML guide:** [site/mechanisms/world-map.html](../site/mechanisms/world-map.html)

---

## In one sentence

The **rift world map** is where you go — sectors joined by lanes, legions you march, ground you claim, and fog you push through.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Rift / world map** | The adventure stage: a network of sectors and lanes. Not the lawn, and not the finished empire stage HUD. |
| **Sector** | A node of ground you can claim, hold, or lose. Your capital sector is the homeworld. |
| **Legion** | A force you march along lanes. Movement and fights hang on legions, not on a free camera fly-to. |
| **End Turn** | The world clock. Orders wait until you press it — see [virtual turns](virtual-turns.md). |

**Also true:**

- You are Dave. Find Zomboss’s fortress and take it before your homeworld falls.
- Buildings, richer map tools, and map→battle handoff are still WIP. Three-layer presentation and the generator stay Vision.

---

## What it is

A graph of sectors joined by lanes. You scout through fog, march legions, claim ground, and read Zomboss as a real opponent.

Holding a sector grants full sight of it — standing on your own land is never a guessing game.

> Win and lose live on [The game](../the-game.md): take Zomboss’s fortress, or lose the homeworld.

---

## How it works

Adventure verbs on the graph:

```text
  Sanctum  ->  World map  ->  march / claim / End Turn
                    |
                    +->  fog, loam, Zomboss
```

| Piece | What it does |
|---|---|
| **March and claim** | Move legions along lanes; claim sectors; watch supply and contact. |
| **Hold** | Ground is not free to keep — [loam](loam.md) and neglect make holdings real. |
| **Opponent** | [Zomboss](zomboss-commander.md) runs his own war from his own fog. |

---

## Where you find it

### Travel to World

Open the World stage from the Sanctum when travel is available.

1. Travel to World.
2. Select a legion and move along lanes.
3. Claim when contact allows, then press End Turn when orders are set.

> Later doors into delves, cede, dowse, lenses, and the outliner are WIP / Vision — see [map orders](map-orders.md).

---

## What you do (first time)

1. Travel to World from the Sanctum.
2. Move one legion one step and notice fog vs clear ground.
3. Press End Turn once so you see everyone’s orders resolve.

> Stop when map ≠ lawn and End Turn ≠ a diary clock. Next: [virtual turns](virtual-turns.md) and [fog of war](fog-of-war.md).

---

## Common mix-ups

**Is this real-time?**  
No. Time is virtual turns — you press End Turn.

**Is the full empire HUD here?**  
Thin map surface is playable. Finished world-stage place is Vision — see [world stage](world-stage.md).

**Does Zomboss see my whole save?**  
No. He evaluates from his fog — see [Zomboss](zomboss-commander.md).

**Can I enter a delve from a sector today?**  
Delve doors are Vision. Map play today is march, claim, End Turn, fog, Zomboss.

---

## Related

- Next: [World virtual turns](virtual-turns.md)
- [World virtual turns](virtual-turns.md)
- [Fog of war](fog-of-war.md)
- [Loam and the Fracture](loam.md)
- [Zomboss as commander](zomboss-commander.md)
- [Farm, hunt, and defend](farm-hunt-defend.md)
- Pillar: [The rift](../the-rift.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
