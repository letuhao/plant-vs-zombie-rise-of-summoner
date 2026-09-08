# Phaser 4.2 scene-switch POC — protocol and results

**Date:** 2026-09-06  
**Program:** `phaser-scene-poc`  
**Phaser:** `^4.2.1` (`web/fusion-rpg-web`)  
**Ideal (open questions still open until owner locks):** [../architecture/phaser-kernel-ideal.md](../architecture/phaser-kernel-ideal.md)

This is a **measurement**, not a product lock. It does **not** unpause siege/battle and does **not** start `phaser-kernel` `/spec`.

---

## Architecture clarification

Smooth multi-Scene switching and “build everything in Phaser” are different problems.

| Concern | Phaser 4.2 | This product |
|---|---|---|
| Multiple modes | Many Scenes in one `Phaser.Game` | First-class |
| Stage travel | Destroy Game / create Game | GG-11 / D2 stage-scoped lifetime |
| HUD / panels | Can draw UI without HTML/CSS | **DPLP:** React chrome; keep unless dual-plane Fail bar trips |

**Explicit non-fail:** “HTML/CSS is nicer for UI” is not a Phaser Scene limit.

---

## How to run

1. Enable developer mode (`?devmode=1` once, or System layer).
2. Open `#/sanctum?dev=phaser-scene-poc` (or backtick → **ScenePOC** tab).
3. Pick **Track A** (one Game, `scene.switch`) or **Track B** (destroy → create).
4. Use **World / Siege / Lawn** buttons; optionally **Run 20 switches**.
5. Open/close the GG-11 panel on Track A and confirm **Game identity** stays stable.
6. Copy the on-screen JSON (or use **Copy timings**) into the Results section below.

Automated: `npx playwright test e2e/phaser-scene-poc.spec.ts --project=chromium` (after `npm run build`) writes `web/fusion-rpg-web/tmp/phaser-scene-poc-results.json`.

Surface code: `web/fusion-rpg-web/src/dev/PhaserSceneSwitchPocPage.tsx`  
Scenes: `web/fusion-rpg-web/src/game/poc/scene-switch/`

---

## Success / fail bars

**Pass**

- Track A: mode switch p95 ≤ ~33 ms (2 frames @ 60 Hz) for fake board (colored grid + 20 markers).
- Track B: destroy → DESTROY → createGame p95 ≤ 300 ms for the same content.
- Dual-plane: React panel open/close on Track A does **not** recreate `Phaser.Game`.
- No need for two concurrent Games to feel mode travel.

**Fail → escalate** (only then consider full Phaser UI or dual-Game waive)

- Track A cannot switch cleanly after ≥50 switches (stuck input / leaks / crashes).
- Dual-plane breaks (input steal, canvas death, forced Phaser HUD).
- Product requires live world WebGL under siege **and** Track B unusable **and** Track A cannot model it without a second Game.

---

## Results

### Environment

| Field | Value |
|---|---|
| Date run | 2026-09-06T14:01:48.507Z |
| Browser | Chromium (Playwright `e2e/phaser-scene-poc.spec.ts`) |
| Host | `npm run preview` @ 4173 after production build |
| Phaser | 4.2.1 |
| Raw JSON | `web/fusion-rpg-web/tmp/phaser-scene-poc-results.json` |

### Track A — Scene switch (one Game)

| Metric | Value | Bar | Verdict |
|---|---|---|---|
| Samples | 20 | ≥20 | OK |
| p50 ms | 0 | | |
| p95 ms | **1.4** | ≤33.3 | **PASS** |
| Max ms | 1.9 | | |
| Failures / stuck | 0 | 0 | **PASS** |

### Track B — Game destroy/create

| Metric | Value | Bar | Verdict |
|---|---|---|---|
| Samples | 20 | ≥20 | OK |
| p50 ms | 20.7 | | |
| p95 ms | **33.7** | ≤300 | **PASS** |
| Max ms | 35.7 | | |

### Dual-plane (GG-11 panel on Track A)

| Check | Result |
|---|---|
| Game identity unchanged across panel open/close | **STABLE** |
| Canvas still rendering after panel close | Yes (no recreate) |

### Overall Pass / Fail

**PASS** — Track A and Track B both clear bars; dual-plane identity stable.  
**Do not escalate** to “build everything in Phaser” or dual concurrent Games on this evidence.

---

## Recommendations for open questions — **LOCKED 2026-09-06**

Owner cleared these in [phaser-kernel-ideal.md](../architecture/phaser-kernel-ideal.md) §Locked answers
after this POC Pass. Dual-plane stays; no “everything in Phaser.”

| Open Q | Lock |
|---|---|
| 1 Siege vs world | **1a** — one Game at a time; **world stage stays Phaser**; destroy world Game on enter siege, recreate on return (not “rebuild world in React”) |
| 2 Decision 40 / lawn | **2a** — lawn adapter OK before Decision 40 discharge |
| 3 Host hook | **3a** — `src/game-host/` (lazy; GG-38) |
| 4 Paint flip | **4c** — defer |
| 5 Who unpauses | **5a** — kernel names; base-defense unpauses |

**Not proven here (follow-up bar):** data still correct across switch (generation / buffer / ready /
active-scene-only apply). Does not reopen 1a–5a.

---

## What I could not find

- A Phaser 4.2 official SLA or ms budget for `scene.switch` vs `Game.destroy` — bars are product-chosen for this POC.
- Evidence that Phaser Scene limits force abandoning HTML/CSS React chrome — orthogonal; dual-plane Pass here.
- Concurrent dual-Game WebGL stability numbers for Phaser 4.2 on this machine (intentionally not in scope; GG-1 avoids it).
- Sub-frame hitch under a *full* lawn/world asset load — this POC uses lightweight fake boards only.
