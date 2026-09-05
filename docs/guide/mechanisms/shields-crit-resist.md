# Shields, crit, and resistance

**Status:** Shipped  
**Loop:** Combat (every place) — see [The loops](../the-loops.md)  
**Pillar:** [Combat](../combat.md)  
**HTML guide:** [site/mechanisms/shields-crit-resist.html](../site/mechanisms/shields-crit-resist.html)

---

## In one sentence

**Shields**, **crits**, and **resistance** are how hits fail, land soft, or land hard — the same combat language on the lawn and in later battles.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **Shield** | An element-typed absorb pool that takes RPG damage before HP. Not a flavour bar and not the lawn plant’s armour sprite. |
| **Crit** | A hit that lands hard when the roll says so — not a separate damage type. |
| **Miss** | Hits can fail. Absence of damage is a real outcome, not a UI glitch. |
| **Resistance** | Two-phase: a status can fail to apply, or apply weaker. Not “immune forever” by default. |
| **One resolver** | One damage path produces the number. Lawn and server battles share that honesty. |

**Also true:**

- Internal cooldowns stop the same interaction from chaining into soup.
- You read shields and statuses under pressure on the [lawn unit HUD](lawn-mirror-hud.md).

---

## What it is

Layered defence in front of health, plus hit outcomes that can miss or crit.

Resistance is about statuses failing or landing soft — not a second health bar.

---

## How it works

Three layers most players watch:

```text
  hit  ->  (miss?)  ->  shields absorb  ->  remaining to HP
       +->  crit can land hard
  status apply  ->  resist fail / weaker / full
```

| Piece | What it does |
|---|---|
| **Shields** | Element-typed pools absorb RPG damage before it reaches HP. |
| **Hits** | Can miss. Can crit. One resolver writes the damage number. |
| **Resistance** | A status may fail to stick, or stick weaker. Cooldowns keep chains honest. |

---

## Where you find it

### On the board and in builds

1. During a lawn match, open Lawn and read shield and status chips on the unit HUD.
2. Build for crit and resist through gear, [aptitudes](aptitudes.md), and specimen traits — not by inventing a private chart.
3. In later battles, expect the same words: shield, miss, crit, resist.

> Exact rates move with balance. Trust the feedback on screen.

---

## What you do (first time)

1. Start a lawn match with the control room open on Lawn.
2. Find a unit with a shield chip and watch it take hits before HP moves.
3. Note a status that fails to stick or lands soft — that is resistance talking.

> Statuses themselves: [statuses](statuses.md). Element story: [six elements and omni](element-ring.md).

---

## Common mix-ups

**Is a shield the same as plant armour in the base game?**  
No. Overlay shields are RPG absorb pools typed by element — read them on the unit HUD.

**Do lawn and web fights use different damage math?**  
No. One resolver — same honesty in both places.

**Does resist mean immune?**  
Not by default. Resist can mean fail-to-apply or apply-weaker.

**Can I stack the same proc forever?**  
Internal cooldowns exist so the same interaction does not soup the board.

**Where do I build for crit?**  
Gear, aptitudes, and specimen traits — watch combat feedback rather than a secret table.

---

## Related

- Next: [Statuses](statuses.md)
- [Six elements and omni](element-ring.md)
- [Statuses](statuses.md)
- [Live mirror and unit HUD](lawn-mirror-hud.md)
- [Free-build aptitudes](aptitudes.md)
- Pillar: [Combat](../combat.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
