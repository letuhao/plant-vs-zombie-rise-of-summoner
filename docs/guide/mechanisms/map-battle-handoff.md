# Map to battle handoff

**Status:** WIP  
**Loop:** World-map adventure · Combat — see [The loops](../the-loops.md)  
**Pillar:** [The rift](../the-rift.md)  
**HTML guide:** [site/mechanisms/map-battle-handoff.html](../site/mechanisms/map-battle-handoff.html)

---

## In one sentence

**WIP:** commit a legion into a fight — the map hands a battle a request and consumes an outcome. Not finished / not fully playable yet.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **WIP** | Not finished. Full fantasy needs interactive battles you play yourself. |
| **Handoff** | Map → battle request → outcome back to the map. Not a separate save and not a lawn launch by itself. |
| **Commit** | Sending a legion into the test — travel into a fight, not only standing on a node. |
| **Interactive battle** | A fight you play (initiative, action bar, targets) — WIP as a stage; expeditions stay auto-resolve. |

**Also true:**

- Lane-board presentation hangs on the [three-layer world](three-layer-world.md) Vision.
- You can still march, claim, and End Turn without this handoff.

---

## What it will feel like

You commit a legion into a fight from the graph.

The map hands the battle a request; when the fight ends, the map consumes the outcome and the war continues.

```text
  legion on map  ->  commit  ->  battle request
                                  |
                                  v
                            play / resolve fight
                                  |
                                  v
                            map consumes outcome
```

---

## What you do today

Advance the shipped map without expecting a finished playable battle stage from every contact.

Treat missing commit-to-battle prompts as unfinished work.

---

## What it depends on

Interactive battles you play yourself are WIP — same combat language, your hands on the action bar.

Until that stage hardens, the handoff cannot feel finished.

> See [interactive battles](interactive-battles.md) when that teach page exists; combat pillar covers the language.

---

## What you do while it is WIP

1. Keep using march, claim, and End Turn on the world map.
2. When a commit-to-battle control appears, follow it and read the outcome back on the map.
3. Do not confuse this with launching a casual lawn match from Sanctum.

> Stop when handoff ≠ lawn shortcut. Related: [world map](world-map.md), [three-layer world](three-layer-world.md).

---

## Common mix-ups

**Why doesn’t contact always open a battle I play?**  
Map→battle handoff and interactive battles are WIP.

**Is this a lawn match?**  
No. It is a battle stage fed by the map — lawn remains its own first core loop.

**Do expeditions use this handoff?**  
No. Expeditions stay async auto-resolve.

**Does the map ignore the fight result?**  
Design consumes the outcome back into the war — that loop is what “handoff” means.

---

## Related

- Next: [Three-layer world](three-layer-world.md)
- [Rift world map](world-map.md)
- [Interactive battles](interactive-battles.md)
- [Three-layer world](three-layer-world.md)
- Pillar: [The rift](../the-rift.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
