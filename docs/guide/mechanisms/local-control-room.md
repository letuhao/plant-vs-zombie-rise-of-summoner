# Local control room

**Status:** Shipped  
**Loop:** Session — how you play — see [The loops](../the-loops.md)  
**Pillar:** [How you play](../how-you-play.md)  
**HTML guide:** [site/mechanisms/local-control-room.html](../site/mechanisms/local-control-room.html)

---

## In one sentence

The **local control room** is the browser window where you play Rise of Summoner on your own machine — your hall, roster, map, and menus — with no account and no cloud.

---

## Blind spots (if you are new)

These words get guessed wrong. Read them once before the walkthrough.

| Word | What it actually means here |
|---|---|
| **Control room** | The Rise of Summoner interface in your **browser**. Not a Discord lobby, not a public game server, not “someone else’s computer.” |
| **Launcher** | The small desktop app you double-click. It starts the helper that keeps your save, starts the lawn game when you ask, and opens the control room in the browser. |
| **Lawn game** | **Plants vs. Zombies: Fusion** — a fan-made Plants vs. Zombies pack (separate from EA’s official titles). Matches on that board are what this guide calls the **lawn**. |
| **Save** | Your Rise of Summoner progress stored **on this PC**, next to the local helper the launcher starts. It is **not** the lawn game’s own save files. |
| **Local** | Everything above runs on your machine. You do not log into a Rise of Summoner account. You do not need a website hosted by someone else. |

**Also true:**

- Rise of Summoner does **not** replace Plants vs. Zombies: Fusion. You point the launcher at a **legal** install of that pack.
- When a menu says **Fusion**, it usually means merging two demons into a stronger form — not the host pack’s product name.

---

## The three pieces

Think of one session as three windows that work together:

```text
  [ Launcher ]  →  starts helpers, opens the browser, can start the lawn game
        │
        ├─→  [ Browser — control room ]  Sanctum, creatures, map, expeditions, settings
        │
        └─→  [ Lawn game ]  live board matches (plants vs zombies)
```

| Piece | Job |
|---|---|
| **Launcher** | Your front door. Trust prompt on first run, pick the lawn-game folder, Play. Leave it running (or minimized) while you play. |
| **Browser control room** | Where the RPG and empire live when you are not inside a lawn match — and where you watch the lawn mirror when a match *is* running. |
| **Lawn game** | The live board. Play matches here; progress (souls, levels, almanac, deploy) lands in your Rise of Summoner save. |

You can keep the control room open with the lawn game closed once features have unlocked. You cannot “log in from another house” — the save stays on this machine.

---

## What you see (GUI)

### On the launcher

After you unzip and double-click the launcher, expect something like this in plain language:

1. **Trust & security** (first run) — a short explanation that this is an unsigned hobby build. Some antivirus tools may warn. You choose Allow if you accept that. Open that panel again anytime if you need the same explanation.
2. **Browse** — a button to choose the folder that contains your legal Plants vs. Zombies: Fusion install (the folder with the game’s own executable). The launcher does not guess a fixed path on your PC.
3. **Loader / plugin** — buttons to install one loader and the Rise of Summoner plugin into that game folder. You only need one loader path; dual-load is refused.
4. **Play** — starts the local helper, can start the lawn game, and opens the control room in your default browser.

Step-by-step install and antivirus notes live in the [player runbook](../../runbook/players.md). This page teaches *what the control room is*, not every button on first install.

### In the browser

When Play succeeds, a normal browser tab opens on your machine. That tab **is** the control room.

Typical first screens:

1. **Title / save select** — pick an existing summoner save or create one. That choice is “which world you keep.” Detail: [Title / save select](save-select.md).
2. **Sanctum** — your home hall after you enter. Creatures on display, next steps, travel. This is where you *are* when nothing else is open. Detail: [Sanctum](sanctum.md).

From Sanctum you open layers (creatures, fusion, expeditions, and so on) over the stage you are already on — you do not leave the game to visit a separate website.

---

## What you do (first time)

1. Unzip the release (or use your usual local setup) and start the **launcher**.
2. Finish trust / folder / plugin setup if this is a fresh install ([player runbook](../../runbook/players.md)).
3. Click **Play**. Wait for the browser tab.
4. At the door, **create or pick a save**.
5. Land in the **Sanctum**. You are now in the local control room.

Stop here for this mechanism. Next learning: how saves work at the door, then what Sanctum is for.

---

## What stays yours

| Thing | What happens |
|---|---|
| Rise of Summoner save | Lives beside the local helper on your PC. Updating the RPG is meant to **keep** that data. |
| Lawn game saves | Stay the lawn game’s. Rise of Summoner does not rewrite them. |
| While you play | Leave the **launcher** running (or minimized). Closing it can stop the helper the browser needs. |
| After unlock | Unlocked control-room features stay playable with the lawn game closed. The lawn still feeds souls, levels, almanac, and deploy when you play it. |

---

## Common mix-ups

**“Is this online multiplayer?”**  
No. Local means your PC. There is no Rise of Summoner account.

**“I closed the browser — did I quit the game?”**  
You closed the control room window. Open the control room again from the launcher (Play / open UI). Your save is still on disk if the helper was running normally.

**“Can I play only in the browser?”**  
After early unlocks, yes for many RPG and empire features. Lawn matches still need the lawn game when you want board progress.

**“The menu says Fusion — is that the host pack?”**  
Usually no. In Rise of Summoner menus, Fusion is the specimen-merge action. The host pack’s full name is Plants vs. Zombies: Fusion — introduce it once when you install, then treat lawn = those matches.

**“Will an update wipe my demons?”**  
Updating Rise of Summoner is designed to keep your save folder. It does not download or patch the lawn game binary. If something looks wrong after an update, check the runbook and SUPPORT — do not reinstall the lawn pack hoping that fixes the RPG save.

---

## Related

- Next: [Title / save select](save-select.md) · [Sanctum](sanctum.md)
- Install & trust: [Player runbook](../../runbook/players.md)
- Pillar: [How you play](../how-you-play.md)
- Fancy skim: [Vision site — Mechanisms](../site/index.html#mechanisms)
- [Mechanism index](README.md)
