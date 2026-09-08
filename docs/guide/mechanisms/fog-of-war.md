# Fog of war

**Status:** Shipped  
**Loop:** World-map adventure — see [The loops](../the-loops.md)  
**Pillar:** [The rift](../the-rift.md)  
**HTML guide:** [site/mechanisms/fog-of-war.html](../site/mechanisms/fog-of-war.html)

---

## In one sentence

**Fog of war** keeps other people’s ground and the past uncertain — your own held ground stays clear.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Fog** | Uncertainty about ground you do not hold (and about memory of what was). Not a weather status on a lawn tile. |
| **Clear sight** | Holding a sector grants full sight of it — home turf is not a guessing game. |
| **Zomboss’s fog** | He evaluates from **his** fog. He does not peek at your save. |
| **Scout** | Moving carefully into uncertain ground so you learn what is there — not automatic full reveal of the map. |

**Also true:**

- Deeper fog craft and procedural generator work are Vision — see [world generator](world-generator.md).
- Fog is about other people’s ground and about memory — not about hiding your own holdings from you.

---

## What it is

Your own ground is clear. Other people’s ground and the past stay uncertain.

That asymmetry is the adventure: expand into fog, hold what you take, and never assume Zomboss shares your eyes.

---

## How it works

Sight follows ownership and contact:

```text
  hold sector  ->  clear sight there
  other ground / past  ->  uncertain (fog)
  Zomboss  ->  decides from his fog
```

| Piece | What it does |
|---|---|
| **Hold** | Claimed sectors you keep give clear sight of that ground. |
| **Push** | March into fog to learn lanes and contacts the hard way. |
| **Opponent** | Zomboss plans under his own fog — fair war, not omniscience. |

---

## Where you find it

### On the World map

Fog is visible as soon as you leave fully held ground.

1. Travel to World.
2. Compare a held sector (clear) with neighbouring uncertain ground.
3. Scout one step carefully, then End Turn and reassess.

> Lenses that recolour fog or supply are WIP map tools — see [map orders](map-orders.md).

---

## What you do (first time)

1. Open the world map on ground you hold.
2. Look outward until the view turns uncertain.
3. March one careful step into fog and End Turn — notice what you learned vs what stayed dark.

> Stop when fog ≠ “the map is broken.” Related: [world map](world-map.md), [Zomboss](zomboss-commander.md).

---

## Common mix-ups

**Why can’t I see the whole map?**  
Fog is intentional. Clear sight comes from holding ground and scouting.

**Does Zomboss cheat and see everything?**  
No. He evaluates from his fog.

**Is my homeworld fogged to me?**  
Holding a sector grants full sight of it — your own land stays clear.

**Is richer fog crafting in this build?**  
Basic fog ships. Deeper craft and generator work are Vision.

---

## Related

- Next: [Zomboss as commander](zomboss-commander.md)
- [Rift world map](world-map.md)
- [Zomboss as commander](zomboss-commander.md)
- [World generator and deeper fog](world-generator.md)
- [Map tools and orders](map-orders.md)
- Pillar: [The rift](../the-rift.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
