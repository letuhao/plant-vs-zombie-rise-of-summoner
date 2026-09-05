# Live mirror and unit HUD

**Status:** Shipped  
**Loop:** Lawn — first core — see [The loops](../the-loops.md)  
**Pillar:** [The lawn](../the-lawn.md)  
**HTML guide:** [site/mechanisms/lawn-mirror-hud.html](../site/mechanisms/lawn-mirror-hud.html)

---

## In one sentence

Watch the live **12×5 mirror** of the board in the browser, with a **unit HUD** above each unit — identity, shield, and statuses at a glance.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Mirror** | A live copy of the 12×5 board in the control room — observation and orders, not a second physics engine. |
| **Unit HUD** | Chips above each unit (identity, shield, statuses). You do not need a second panel to read the board under pressure. |
| **Orders** | High-level commands from the control room. You are not rewriting pea physics. |
| **Closing a menu** | Does not destroy the board. You stay on the Lawn stage. |
| **Sun / wave clock** | Match state on the mirror (sun bank, wave, phase) — separate from RPG stock souls. |

**Also true:**

- Shield and status chips use the shipped combat language — [shields, crit, and resistance](shields-crit-resist.md) · [statuses](statuses.md).
- Deployed specimens and the commander chip show here when present.

---

## What it is

Open the Lawn stage while a match is running.

You see plants, zombies, sun, wave clock, match phase, commander chip, deployed specimens, and statuses — live.

---

## How you use it

Observe first, then issue orders when you need them:

```text
  lawn game board  --live-->  12x5 mirror + unit HUD
                              (observe / high-level orders)
```

| Piece | What it does |
|---|---|
| **Read** | Unit HUD carries identity, shield, and the statuses that matter at a glance. |
| **Order** | High-level orders only — you are not a second physics engine. |
| **Stay** | Closing overlays does not dump you off the stage or wipe the board. |

---

## Where you find it

### In the control room

1. Start a match in the lawn game.
2. Open the control room and select Lawn.
3. Read the mirror and HUD without hunting a second deep panel for every status.

> If the mirror is still, the match may not be running or the injector may not be connected — fix that before blaming the UI.

---

## What you do (first time)

1. Get a match running.
2. Open Lawn and confirm a moving 12×5 mirror.
3. Point at one unit HUD and name identity, shield, or a status chip.

> Parent loop: [lawn match](lawn-match.md). Field demons onto that board: [bound demon deploy](lawn-deploy.md).

---

## Common mix-ups

**Is the mirror a second game I play with the mouse?**  
You observe and issue high-level orders. The lawn game still owns the board physics.

**Do I need to open a detail panel for every status?**  
No. Unit HUD is built for glance reading under pressure.

**Did I break the match when I closed a menu?**  
Closing a menu does not destroy the board — you stay on the stage.

**Is mirror sun the same as souls?**  
No. Match sun is board state. Souls are the RPG stock — see [souls](souls.md).

**Why don’t I see shields?**  
Shield chips appear when overlay shields are present — see [shields, crit, and resistance](shields-crit-resist.md).

---

## Related

- Next: [Bound demon deploy](lawn-deploy.md)
- [Lawn match](lawn-match.md)
- [Statuses](statuses.md)
- [Shields, crit, and resistance](shields-crit-resist.md)
- Pillar: [The lawn](../the-lawn.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
