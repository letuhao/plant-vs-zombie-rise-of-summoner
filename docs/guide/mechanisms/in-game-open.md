# In-game open control room

**Status:** WIP  
**Loop:** Lawn — first core — see [The loops](../the-loops.md)  
**Pillar:** [The lawn](../the-lawn.md)  
**HTML guide:** [site/mechanisms/in-game-open.html](../site/mechanisms/in-game-open.html)

---

## In one sentence

**WIP:** open the RPG **without leaving** the lawn-game window (button / F10). Esc closes; the board pauses while you are away. Not finished yet.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the rest of the page.

| Word | What it actually means here |
|---|---|
| **WIP** | Not finished. Today the usual path is still launcher + browser — see [local control room](local-control-room.md). |
| **In-game open** | A door from inside the lawn-game window into the control room — not a second install and not alt-tab as the designed end state. |
| **F10** | The planned hotkey alongside an on-screen button. Treat missing F10 as unfinished, not a broken keymap forever. |
| **Pause while away** | Design: the board pauses while the control room is open from in-game. Esc closes it. |
| **Control room** | The local browser shell for Sanctum, Lawn, roster, and more — already playable via launcher + browser. |

**Also true:**

- You can already run the full local control room without this button.
- Lawn play itself is shipped — [lawn match](lawn-match.md).

---

## Not finished yet

The fantasy is one window: fight on the lawn, tap open, manage the RPG, Esc back.

Until it ships, use the launcher and browser path you already have.

---

## Design shape

What the WIP is aiming at:

```text
  lawn-game window  ->  (button / F10)  ->  control room (paused board)
                              Esc closes  ->  back to match
```

| Piece | What it does |
|---|---|
| **Open** | In-game button and F10 open the control room. |
| **Pause** | The board waits while you are in the RPG shell. |
| **Close** | Esc returns you; the match continues from the pause. |

---

## What you do today

### Current path

1. Start the local server / launcher flow you already use.
2. Open the control room in the browser.
3. Keep the lawn game and browser side by side until in-game open lands.

> Full teach page for that shell: [local control room](local-control-room.md).

---

## What you do while it is WIP

1. Use launcher + browser for the control room today.
2. When in-game open appears, try the button or F10 once and confirm Esc pauses/closes as labeled.
3. Do not assume missing F10 means your keymap is wrong — the feature is still WIP.

> Session shell today: [local control room](local-control-room.md). Board loop: [lawn match](lawn-match.md).

---

## Common mix-ups

**Why isn’t F10 opening anything?**  
In-game open is WIP. Use the browser control room for now.

**Do I need a second PC?**  
No. Local launcher + browser on the same machine is the shipped path.

**Will opening the RPG quit my match?**  
Design pauses the board while you are away — it should not trash the run.

**Is this a different control room?**  
No. Same local control room — a shorter door into it.

**Can I play without the lawn game window?**  
Yes for unlocked web features. This page is only about opening from inside the lawn window.

---

## Related

- Next: [Local control room](local-control-room.md)
- [Local control room](local-control-room.md)
- [Lawn match](lawn-match.md)
- Pillar: [The lawn](../the-lawn.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
