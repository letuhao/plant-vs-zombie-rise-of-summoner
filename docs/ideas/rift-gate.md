# Rift Gate

**Status:** confirmed product direction; idea only, not implementation authorization.

## Problem statement

How might we make Rise of Summoner continuously reachable from PVZ Fusion without asking a
player to remember a hotkey, leave the game, manually open the Almanac, or invoke a developer API
just to prepare the control room?

## Recommended direction

The **Rift Gate** is the permanent, player-facing doorway into Rise of Summoner. On the PVZ main
menu it is a clickable Rift Tombstone: a cracked stone marker with a violet-blue fracture, designed
as a recognisable piece of PVZ scenery rather than a floating web-app button. Compact versions of the
same silhouette appear in other safe PVZ menus and in the existing in-match corner entry. All three
are affordances for one action: request that the existing overlay host show the control room.

The gate extends the lawn-first core loop: the lawn is still the first place a player plays, while
the Rift is the doorway to the same durable roster, expeditions, and world ([the-game.md](../guide/the-game.md),
[the-loops.md](../guide/the-loops.md)). It does not create a second game or replace the standalone
web capability.

The Rift is a show/hide surface, not a browser lifecycle. It preserves the SPA when hidden and
returns the player to precisely the PVZ view they left. The control room carries a persistent
**Leave the Rift** action that explicitly requests hide; it never navigates away from the current
FE state and never destroys the web view.

## Player journey

```text
PVZ main menu
  → click the animated Rift Tombstone (or press F10)
  → cache check runs before normal FE content needs captured art
  → missing first-install data is captured by a normal injector operation
  → control room opens over PVZ
  → player uses the same roster / expedition / world progression as standalone web play
  → Leave the Rift, Esc, or F10 hides the view
  → exact PVZ menu or lawn view is restored
```

## Entry and exit contract

- **Main menu:** the full tombstone gate is always visible when the injector can reach an overlay
  host. Unlike the current match-only switch, no live board is required.
- **Other PVZ menus:** a compact Rift icon is present only on a reviewed allow-list of safe menu
  surfaces. It must not cover a host-game primary action or consume a gameplay click.
- **Live lawn:** the existing corner entry becomes the compact Rift icon and keeps its current
  pause-while-away contract.
- **Open:** all PVZ affordances, F10, and the FE bridge converge on one host-owned toggle path.
- **Close:** Leave the Rift sends an explicit `rift.hide` request; Esc and F10 use the same host
  hide path. A hide is not a toggle, so a delayed or duplicate FE message cannot reopen the view.
- **Fallback:** the Launcher remains shippable and supported. When `overlayHost=injector`, the
  injector-owned host is the no-Launcher fallback.

### F10 ownership

F10 must be captured natively by the overlay host whenever PVZ is foreground, including while the
view is hidden. That is the only way it can open the Rift before the FE exists on screen. The FE also
captures F10 *while it has focus* and sends the typed bridge message so browser focus cannot create a
different behaviour. The FE does not speak to Unity, named pipes, or the injector directly; it uses
a host-provided bridge, and the host validates and dispatches the request.

## First-Rift asset cache

Today, icon capture occurs only after an Almanac card is selected, and the two dump screens sit in
the developer route set. That makes a player perform development work: browse cards or call a debug
endpoint/Postman before ordinary FE portraits and copy are available. The release flow must remove
that requirement.

On the first successful main-menu Rift open, the host asks the server for a **capture manifest**:

1. The manifest identifies the installed game/content revision and reports whether text and portrait
   capture are complete for that revision.
2. If complete, the Rift opens normally with no capture work.
3. If stale or incomplete, the injector starts a bounded, resumable normal-operation capture job and
   the FE reports preparation progress without exposing a debug surface.
4. Each captured entry is cache-checked before upload. Server-side storage remains the durable
   authority; a failed or interrupted harvest resumes next Rift open.
5. The Rift still opens. Missing art uses an honest fallback state; preparation may not strand the
   player behind a blocking full-screen spinner.

The existing text sweep is a useful precedent: `GameHooks.EnqueueFullAlmanacText()` reads already
loaded dictionaries without opening the Almanac UI. It is not sufficient for portraits. Existing
portrait capture needs a live `AlmanacCardUI` (`TypeIconCapture.TryCaptureAlmanacCard`), so the
implementation must first prove a safe non-interactive source for every card or provide a bounded
host-owned card traversal that leaves the player on the menu they started from. It may not fake
captured images, ask the player to operate developer screens, or use a debug endpoint as release
behaviour.

## Visual and VFX direction

### Hero asset

- **Shape:** squat, asymmetrical PVZ-style tombstone; the crack is the central silhouette and the
  source of the compact icon.
- **Palette:** weathered grey-green stone, a restrained cyan-violet inner fracture, soft pale mote
  highlights. No text baked into the art.
- **Format:** transparent 2D hero art plus a derived compact icon; text and hit target stay
  code-native.

### Motion states

| State | Feedback |
|---|---|
| Idle | Slow fracture pulse and sparse rising motes. |
| Hover | Brighter crack, short outward mote curl, immediate pointer acknowledgement. |
| Click | Brief light expansion and soft portal flash, then immediately queue open. |
| Unavailable | Dim stone plus readable host-unavailable explanation; never a dead click. |

The main menu gets no screen shake, hit stop, or simulation-time manipulation. Motion is cosmetic,
short, and returns to rest. The click action remains responsive even if overlay startup or cache
preparation is still pending.

## Key assumptions to validate

- [ ] A single host bridge can serve `rift.toggle` and `rift.hide` consistently for both Launcher and
  injector-hosted WebView2, without granting the FE arbitrary native commands.
- [ ] Native F10 can open and close the injector-hosted view when the Launcher is absent, while FE
  capture only supplements it when the web view has focus.
- [ ] Every target menu has a stable, reviewed scene anchor and a non-overlapping hit target; missing
  anchors degrade to a logged absence, never an exception in Unity UI.
- [ ] A capture manifest can distinguish fresh, stale, partial, and failed catalog captures without
  treating a current row count as a completion contract.
- [ ] Portrait data can be harvested safely without the player manually visiting every Almanac card.
  The present implementation proves only per-card capture, not a release-ready bulk path.
- [ ] First-open capture is bounded, resumable, cache-aware, and does not block opening the control
  room or introduce a Unity-frame stall.

## MVP scope

- Permanent animated Rift Tombstone on the PVZ main menu.
- Compact Rift icon on the existing live-lawn entry and one reviewed non-gameplay menu surface.
- Shared host bridge with explicit hide and FE Leave the Rift control.
- Native injector-host F10 fallback plus FE F10 bridge handling.
- A first-Rift cache manifest and progress UI.
- Automatic full text capture through the existing safe sweep where cache state requires it.
- A technical spike proving safe, non-interactive portrait capture before promising automatic icon
  harvesting in the player release.

## Not doing

- **Embedding a browser texture inside Unity** — the supported overlay remains a separate hosted
  WebView2 window.
- **A second overlay/browser lifecycle** — all entry points use the existing host show/hide path.
- **Direct FE-to-Unity, FE-to-pipe, or arbitrary native messaging** — the browser bridge is a small,
  validated command vocabulary.
- **Player-facing debug APIs, Postman instructions, or dump routes** — these remain developer tools,
  not release onboarding.
- **Blocking the Rift until every asset is captured** — incomplete content gets honest fallbacks and
  resume-on-next-open capture.
- **Generating the final art before a technical art brief and native-scale preview** — the tombstone
  is a proposed asset family, not yet a production-ready sprite.

## Next extension

Before implementation, turn this idea into a dedicated Rift Gate capability map and module specs:

1. host bridge and F10 ownership;
2. menu-anchor and input contract;
3. first-Rift capture manifest and safe asset-harvest mechanism;
4. FE entry/exit and preparation states;
5. asset brief, generated hero target, VFX integration, and live verification.

## Design-gate evidence

- Subsystems: player-visible PVZ/FE UI, Launcher/injector overlay transport, and release-time
  game-data capture.
- Product reading: `docs/guide/the-game.md` and `docs/guide/the-loops.md` — the Rift extends the
  lawn-first core loop while preserving standalone capability.
- UI/overlay reading: `docs/architecture/game-gui-principles.md`; `docs/launcher/overlay-spec.md`;
  `docs/architecture/software-architecture.md`.
- Code checked: match-only menu gate and host hide on board exit (`docs/launcher/overlay-spec.md:95`,
  `src/FusionRpg.Injector/Hud/OverlaySwitch.cs:54-55`); native close policy
  (`src/FusionRpg.Core/Overlay/OverlayViewPolicy.cs:22-23`); per-card icon capture
  (`src/FusionRpg.Injector/GameCaptureHooks.cs:560-561`, `582-583`); cache-aware uploads
  (`src/FusionRpg.Injector/RpgClient.cs:286-315`); dictionary-backed text sweep
  (`src/FusionRpg.Injector/GameHooks.cs:377-397`).
- No tests run: this idea artifact changes no runtime code. The boundary checker has pre-existing
  unrelated drift; this worktree has an isolated document-only path fence.
