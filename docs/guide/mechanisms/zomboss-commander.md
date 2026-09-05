# Zomboss as commander

**Status:** Shipped  
**Loop:** World-map adventure · Farm / hunt / defend — see [The loops](../the-loops.md)  
**Pillar:** [The rift](../the-rift.md)  
**HTML guide:** [site/mechanisms/zomboss-commander.html](../site/mechanisms/zomboss-commander.html)

---

## In one sentence

**Zomboss** is the enemy commander — he runs his own war from his own fog while you hunt his fortress and guard the homeworld.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Zomboss** | The opposing commander on the rift — not a lawn-only boss sprite and not a cheat that reads your save. |
| **Fortress** | His capital sector. Taking it is the win condition — see [The game](../the-game.md). |
| **Homeworld** | Dave’s capital. Losing it is losing the world. |
| **His fog** | He decides with incomplete information, same as you — fair opposing play. |

**Also true:**

- Enemy counter-development (the war shifting resists from how you play) is Vision — see [counter-development](counter-development.md).
- Siege of the Seat is Vision as a finished stage — see [siege](siege.md).

---

## What it is

Zomboss is a real opponent on the map: he marches, claims, and pressures under his own fog.

Your job is to find his fortress and take it before yours falls.

> Win and lose are defined on [The game](../the-game.md) — keep who you are; lose where you were.

---

## How it works

He shares the virtual-turn clock:

```text
  you End Turn  ->  resolve
                    |
                    +->  your legions
                    +->  Zomboss (from his fog)
```

| Piece | What it does |
|---|---|
| **Orders** | He commits on the same End Turn resolve you do. |
| **Fog** | He does not peek at your save — he plays from uncertainty. |
| **Pressure** | Defend the homeworld while you hunt his fortress. |

---

## Where you find it

### On the World map

Advance, End Turn, and read what he did.

1. Travel to World and expand through fog.
2. Press End Turn and watch opposing motion.
3. Treat fortress hunt and homeworld defence as the same war — not two separate modes.

> Richer ecology and counter-builds come later as Vision.

---

## What you do (first time)

1. Open the world map and note your homeworld.
2. End Turn at least once and look for opposing movement.
3. Keep expanding with homeworld safety in mind — losing it ends the world.

> Stop when Zomboss ≠ a scripted cutscene villain only. Related: [virtual turns](virtual-turns.md), [farm / hunt / defend](farm-hunt-defend.md).

---

## Common mix-ups

**Does he see my whole roster?**  
No. He evaluates from his fog.

**How do I win?**  
Find and take his fortress sector before the homeworld falls.

**Is siege playable today?**  
Siege as a tactical Seat board is Vision. Map pressure from Zomboss already ships.

**Does he scale to my Dave level only?**  
Matched-level padding is not the design. Counter-development is Vision — see [counter-development](counter-development.md).

---

## Related

- Next: [Farm, hunt, and defend](farm-hunt-defend.md)
- [Rift world map](world-map.md)
- [World virtual turns](virtual-turns.md)
- [Fog of war](fog-of-war.md)
- [Farm, hunt, and defend](farm-hunt-defend.md)
- [Enemy counter-development](counter-development.md)
- Pillar: [The rift](../the-rift.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
