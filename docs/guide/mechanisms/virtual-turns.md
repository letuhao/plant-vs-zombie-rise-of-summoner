# World virtual turns

**Status:** Shipped  
**Loop:** World-map adventure · World-stage empire — see [The loops](../the-loops.md)  
**Pillar:** [The rift](../the-rift.md)  
**HTML guide:** [site/mechanisms/virtual-turns.html](../site/mechanisms/virtual-turns.html)

---

## In one sentence

**Virtual turns** are the world clock — everyone commits orders, then **End Turn** resolves them for every commander at once.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Virtual turn** | A shared resolve step on the map. Not a live match timer and not a real-life diary day. |
| **End Turn** | The button (or control) that commits the turn. The barrier waits until you and the other commanders are ready. |
| **Orders** | What you set for legions before the turn resolves — march, claim, and later richer tools. |
| **Diary clock** | A minute→day→week meta calendar. This game does not use one for the world — only lawn time, expedition wall-clock, and virtual turns. |

**Also true:**

- Expeditions use wall-clock because they are idle. The map does not — it waits on End Turn.
- Same clock applies when the finished [world stage](world-stage.md) arrives.

---

## What it is

Time on the map is turn-based. There is no clock bullying you while you think.

When orders are set, press End Turn. You, Zomboss, and neutrals resolve together.

---

## How it works

One commit, one resolve:

```text
  set orders  ->  End Turn  ->  resolve for all commanders
                                  |
                                  +->  you / Zomboss / neutrals
```

| Piece | What it does |
|---|---|
| **Set** | Issue orders for your legions while the turn is open. |
| **Commit** | Press End Turn when you are ready. |
| **Resolve** | Everyone’s orders fire — including Zomboss evaluating from his fog. |

---

## Where you find it

### On the World map

Open World travel and look for End Turn once you can issue orders.

1. Set at least one legion order.
2. Press End Turn.
3. Read what moved — yours and theirs.

> If nothing resolves, you have not committed the turn yet.

---

## What you do (first time)

1. Open the world map.
2. Give one clear order (march or claim).
3. Press End Turn and watch the resolve — including enemy motion.

> Stop when End Turn ≠ “wait five real minutes.” Related: [world map](world-map.md), [Zomboss](zomboss-commander.md).

---

## Common mix-ups

**Does the map tick while I am AFK?**  
No. It waits for End Turn. Expeditions are the wall-clock loop.

**Is this the same as lawn time?**  
No. Lawn is a live match. The map is virtual turns.

**Do I need to End Turn for expeditions?**  
No. Expeditions use their own dispatch timers.

**Does Zomboss move on my End Turn?**  
Yes. Everyone resolves together when you commit.

---

## Related

- Next: [Fog of war](fog-of-war.md)
- [Rift world map](world-map.md)
- [Zomboss as commander](zomboss-commander.md)
- [World-stage empire](world-stage.md)
- Pillar: [The rift](../the-rift.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
